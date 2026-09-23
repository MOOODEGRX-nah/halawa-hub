#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

NEW="${1:?usage: tools/bump.sh X.Y.Z.W}"
[[ "$NEW" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "bad version: $NEW"; exit 1; }

# 1) Directory.Build.props (المصدر الوحيد)
sed -i -E "s#<Version>[0-9]+(\.[0-9]+){3}</Version>#<Version>${NEW}</Version>#" Directory.Build.props
GOT="$(grep -oE '<Version>[^<]+' Directory.Build.props | head -1 | sed 's/<Version>//')"
[ "$GOT" = "$NEW" ] || { echo "BUMP FAILED: props has '$GOT'"; exit 1; }

# 2) VERSION (مشتق)
printf '%s\n' "$NEW" > VERSION

# 3) README (نمط شامل)
sed -i -E "s#v[0-9]+(\.[0-9]+){3} \(Beta\)#v${NEW} (Beta)#" README.md
[ "$(grep -cF "v${NEW} (Beta)" README.md)" = "1" ] || echo "WARN: README line not found/duplicated"

# 4) CHANGELOG.json — إضافة إدخال أعلى الملف (مرساة NR==1)
#    ملاحظة: Placeholder فارغ — المستخدم يملأه يدويًا قبل الرفع
awk -v ver="$NEW" '
NR==1 {
    print
    print "  \"" ver "\": ["
    print "    \"[PLACEHOLDER: أضف ملاحظات الإصدار هنا]\""
    print "  ],"
    next
}
{ print }
' CHANGELOG.json > /tmp/cl_bump.json
mv /tmp/cl_bump.json CHANGELOG.json

echo "bumped to $NEW"
echo ""
echo "⚠️  CHANGELOG entry added with placeholder — fill it manually before commit:"
echo "    \"0.0.10.26\": ["
echo "      \"[PLACEHOLDER: أضف ملاحظات الإصدار هنا]\""
echo "    ]"
echo ""
bash tools/check-meta.sh
