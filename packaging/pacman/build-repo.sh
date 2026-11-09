#!/usr/bin/env bash
# يبني حزمة halawa-hub ومستودع pacman موقّعين من tar.gz جاهز. يعمل على Arch فقط (makepkg وrepo-add)
# وبمستخدم عادي لأن makepkg يرفض root. نفس السكربت في CI وفي النشر اليدوي.
#
# الاستخدام: build-repo.sh <tar.gz> <version> <pkgrel> <output-dir> <gpg-key-id>
# المفتاح يؤخذ من الحلقة الحالية (GNUPGHOME). الناتج في output-dir:
#   halawa-hub-<version>-<pkgrel>-x86_64.pkg.tar.zst(.sig)
#   halawa-hub.db(.sig) وhalawa-hub.files(.sig)
set -euo pipefail

TARBALL="$(readlink -f "${1:?usage: build-repo.sh <tar.gz> <version> <pkgrel> <output-dir> <gpg-key-id>}")"
VERSION="${2:?missing version}"
PKGREL="${3:?missing pkgrel}"
OUTPUT_DIR="${4:?missing output dir}"
GPG_KEY="${5:?missing gpg key id}"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

WORK="$(mktemp -d)"
cleanup() { rm -r -- "$WORK"; }
trap cleanup EXIT

SOURCE_NAME="halawa-hub-$VERSION-linux-x64.tar.gz"
cp "$TARBALL" "$WORK/$SOURCE_NAME"
HASH="$(sha256sum "$TARBALL" | awk '{ print $1 }')"

# PKGBUILD واحد لـ AUR والمستودع حتى لا يختلف محتوى الحزمتين: الفروق هنا فقط
sed -E \
    -e "s/^pkgname=.*/pkgname=halawa-hub/" \
    -e "s/^pkgver=.*/pkgver=$VERSION/" \
    -e "s/^pkgrel=.*/pkgrel=$PKGREL/" \
    -e "/^provides=/d" \
    -e "s/^conflicts=.*/conflicts=('halawa-hub-bin')/" \
    -e "s|^source=.*|source=(\"$SOURCE_NAME\")|" \
    -e "s/^sha256sums=.*/sha256sums=('$HASH')/" \
    "$HERE/../aur/PKGBUILD" > "$WORK/PKGBUILD"

(
    cd "$WORK"
    PKGDEST="$OUTPUT_DIR" PKGEXT=".pkg.tar.zst" PACKAGER="Halawa-Hub <https://github.com/MOOODEGRX-nah/halawa-hub>" \
        makepkg --force --nodeps --noconfirm --sign --key "$GPG_KEY"
)

PACKAGE="halawa-hub-$VERSION-$PKGREL-x86_64.pkg.tar.zst"
[ -f "$OUTPUT_DIR/$PACKAGE" ] || { echo "error: $PACKAGE was not built" >&2; exit 1; }
[ -f "$OUTPUT_DIR/$PACKAGE.sig" ] || { echo "error: $PACKAGE is not signed" >&2; exit 1; }

# قاعدة جديدة في كل مرة: المستودع يعرض أحدث إصدار فقط
rm -f -- "$OUTPUT_DIR"/halawa-hub.db* "$OUTPUT_DIR"/halawa-hub.files*
(
    cd "$OUTPUT_DIR"
    repo-add --nocolor --sign --verify --key "$GPG_KEY" halawa-hub.db.tar.gz "$PACKAGE"

    # repo-add يكتب .db كرابط رمزي، وملفات إصدار GitHub لا تقبل الروابط
    for name in halawa-hub.db halawa-hub.db.sig halawa-hub.files halawa-hub.files.sig; do
        cp --remove-destination "$(readlink -f "$name")" "$name"
    done
    rm -f -- halawa-hub.db.tar.gz halawa-hub.db.tar.gz.sig halawa-hub.files.tar.gz halawa-hub.files.tar.gz.sig
    rm -f -- ./*.old ./*.old.sig
)

ls -la "$OUTPUT_DIR"
