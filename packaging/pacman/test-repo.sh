#!/usr/bin/env bash
# يختبر المستودع كما يستخدمه الناس: إضافة المفتاح والمستودع، تثبيت، تحديث، رفض قاعدة
# معدّلة، إزالة. يعمل كـ root داخل حاوية Arch في CI فقط، لأنه يغيّر pacman.conf والحلقة.
# بمفتاح مؤقت ينشئه بنفسه، فلا يحتاج سرًّا ويعمل على كل PR.
#
# الاستخدام: test-repo.sh <tar.gz> <version>
set -euo pipefail

TARBALL="$(readlink -f "${1:?usage: test-repo.sh <tar.gz> <version>}")"
VERSION="${2:?missing version}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$(id -u)" != "0" ] || [ -z "${HALAWA_TEST_IN_CONTAINER:-}" ]; then
    echo "error: this script changes pacman.conf and the keyring; it only runs as root in a CI container" >&2
    exit 1
fi

fail() { echo "REPO TEST FAIL: $*" >&2; exit 1; }

BUILDER=repotest
id "$BUILDER" >/dev/null 2>&1 || useradd -m "$BUILDER"
WORK="$(mktemp -d)"
chmod 755 "$WORK"
REPO="$WORK/repo"
install -d -o "$BUILDER" "$REPO" "$WORK/src"
cp -r "$HERE/.." "$WORK/src/packaging"
cp "$TARBALL" "$WORK/src/app.tar.gz"
chown -R "$BUILDER" "$WORK/src"
GNUPG="/home/$BUILDER/.gnupg"

as_builder() { runuser -u "$BUILDER" -- env HOME="/home/$BUILDER" GNUPGHOME="$GNUPG" "$@"; }

echo "== temporary signing key"
install -d -m 700 -o "$BUILDER" "$GNUPG"
as_builder gpg --batch --passphrase '' --quick-generate-key "Halawa-Hub CI test" ed25519 sign 1d
KEY="$(as_builder gpg --list-keys --with-colons | awk -F: '/^fpr:/ { print $10; exit }')"

build() { as_builder bash "$WORK/src/packaging/pacman/build-repo.sh" "$WORK/src/app.tar.gz" "$VERSION" "$1" "$REPO" "$KEY"; }

echo "== build pkgrel 1"
build 1

echo "== add key and repository, as in the README"
pacman-key --init >/dev/null 2>&1
as_builder gpg --armor --export "$KEY" > "$WORK/key.asc"
pacman-key --add "$WORK/key.asc" >/dev/null
pacman-key --lsign-key "$KEY" >/dev/null
cat >> /etc/pacman.conf <<EOF

[halawa-hub]
SigLevel = Required DatabaseRequired
Server = file://$REPO
EOF

echo "== install"
pacman -Sy --noconfirm halawa-hub
[ "$(/usr/bin/halawa-hub --version)" = "$VERSION" ] || fail "wrong version after install"
[ "$(pacman -Q halawa-hub | awk '{ print $2 }')" = "$VERSION-1" ] || fail "wrong package version"
[ -f /usr/share/applications/halawa-hub.desktop ] || fail "menu entry missing"
[ -f /usr/share/icons/hicolor/256x256/apps/halawa-hub.png ] || fail "icon missing"
[ ! -e /opt/halawa-hub/.halawa-hub-tarball ] || fail "tar.gz marker would let a system package replace itself"
[ -x /opt/halawa-hub/Halawa-Hub ] || fail "binary lost its executable bit"

echo "== update"
build 2
pacman -Sy --noconfirm halawa-hub
[ "$(pacman -Q halawa-hub | awk '{ print $2 }')" = "$VERSION-2" ] || fail "update did not apply"

echo "== a tampered database is refused"
cp "$REPO/halawa-hub.db" "$WORK/db.good"
printf 'tampered' >> "$REPO/halawa-hub.db"
if pacman -Syy --noconfirm 2>/dev/null; then
    fail "pacman accepted a database that does not match its signature"
fi
cp "$WORK/db.good" "$REPO/halawa-hub.db"
pacman -Syy --noconfirm >/dev/null

echo "== a package signed by another key is refused"
install -d -m 700 -o "$BUILDER" "$GNUPG-other"
runuser -u "$BUILDER" -- env HOME="/home/$BUILDER" GNUPGHOME="$GNUPG-other" gpg --batch --passphrase '' --quick-generate-key "Not Trusted" ed25519 sign 1d
runuser -u "$BUILDER" -- env HOME="/home/$BUILDER" GNUPGHOME="$GNUPG-other" gpg --batch --yes --detach-sign \
    --output "$WORK/src/bad.sig" "$REPO/halawa-hub-$VERSION-2-x86_64.pkg.tar.zst"
cp "$REPO/halawa-hub-$VERSION-2-x86_64.pkg.tar.zst.sig" "$WORK/good.sig"
cp "$WORK/src/bad.sig" "$REPO/halawa-hub-$VERSION-2-x86_64.pkg.tar.zst.sig"
pacman -R --noconfirm halawa-hub >/dev/null
rm -f /var/cache/pacman/pkg/halawa-hub-*
if pacman -S --noconfirm halawa-hub 2>/dev/null; then
    fail "pacman accepted a package signed by an untrusted key"
fi
cp "$WORK/good.sig" "$REPO/halawa-hub-$VERSION-2-x86_64.pkg.tar.zst.sig"
rm -f /var/cache/pacman/pkg/halawa-hub-*

echo "== remove"
pacman -S --noconfirm halawa-hub >/dev/null
pacman -R --noconfirm halawa-hub
[ ! -e /opt/halawa-hub ] || fail "/opt/halawa-hub left behind"
[ ! -e /usr/bin/halawa-hub ] || fail "/usr/bin/halawa-hub left behind"

echo "REPO TEST OK: v$VERSION"
