#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

NEW="${1:?usage: tools/bump.sh X.Y.Z.W}"
[[ "$NEW" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "bad version: $NEW"; exit 1; }

# تحديث Directory.Build.props (المصدر الوحيد)
sed -i -E "s#<Version>[0-9]+(\.[0-9]+){3}</Version>#<Version>${NEW}</Version>#" Directory.Build.props

# التحقق الموجب
GOT="$(grep -oE '<Version>[^<]+' Directory.Build.props | head -1 | sed 's/<Version>//')"
[ "$GOT" = "$NEW" ] || { echo "BUMP FAILED: props has '$GOT'"; exit 1; }

# تحديث VERSION (مشتق)
printf '%s\n' "$NEW" > VERSION

# تحديث README (نمط شامل)
sed -i -E "s#v[0-9]+(\.[0-9]+){3} \(Beta\)#v${NEW} (Beta)#" README.md

echo "bumped to $NEW"
bash tools/check-meta.sh
