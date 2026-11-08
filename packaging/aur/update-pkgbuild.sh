#!/usr/bin/env bash
# يكتب الإصدار وهاش tar.gz الحقيقي في PKGBUILD من SHA256SUMS.txt الخاص بالإصدار المنشور.
# الاستخدام: bash packaging/aur/update-pkgbuild.sh <version> [SHA256SUMS.txt]
# بدون المسار الثاني ينزّل الملف من GitHub (يحتاج gh).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

VERSION="${1:?usage: update-pkgbuild.sh <version> [SHA256SUMS.txt]}"
ASSET="Halawa-Hub-linux-x64.tar.gz"

if [ -n "${2:-}" ]; then
    SUMS="$(cat "$2")"
else
    SUMS="$(gh release download "v$VERSION" --repo MOOODEGRX-nah/halawa-hub --pattern SHA256SUMS.txt --output -)"
fi

HASH="$(printf '%s\n' "$SUMS" | awk -v asset="$ASSET" '$2 == asset || $2 == "*" asset { print $1 }')"
if [ -z "$HASH" ]; then
    echo "error: $ASSET is not listed in SHA256SUMS.txt for v$VERSION" >&2
    exit 1
fi

sed -i -E \
    -e "s/^pkgver=.*/pkgver=$VERSION/" \
    -e "s/^pkgrel=.*/pkgrel=1/" \
    -e "s/^sha256sums=.*/sha256sums=('$HASH')/" \
    PKGBUILD

echo "PKGBUILD updated: pkgver=$VERSION sha256=$HASH"
