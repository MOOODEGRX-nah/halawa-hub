#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

# المصدر الوحيد
V="$(grep -oE '<Version>[^<]+' Directory.Build.props | head -1 | sed 's/<Version>//')"
[ -n "$V" ] || { echo "META FAIL: no <Version> in Directory.Build.props"; exit 1; }

# VERSION مشتق
[ "$(tr -d '\r\n' < VERSION)" = "$V" ] || { echo "META FAIL: VERSION != $V"; exit 1; }

# CHANGELOG: أعلى إدخال = النسخة
TOP="$(grep -m1 -oE '"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"' CHANGELOG.json | tr -d '"')"
[ "$TOP" = "$V" ] || { echo "META FAIL: CHANGELOG top='$TOP' != $V"; exit 1; }

# JSON صالح (بدون jq)
BAL=$(awk '{for(i=1;i<=length($0);i++){c=substr($0,i,1); if(c=="{")b++; else if(c=="}")b--}} END{print b+0}' CHANGELOG.json)
[ "$BAL" = "0" ] || { echo "META FAIL: JSON unbalanced"; exit 1; }

# README يذكر الإصدار
grep -qF "v${V} (Beta)" README.md || echo "WARN: README ما يذكر v${V} (Beta)"

echo "meta OK: $V"
