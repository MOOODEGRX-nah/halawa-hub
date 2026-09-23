#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
V="$(tr -d '\r\n' < VERSION)"
[ -n "$V" ] || { echo "META FAIL: VERSION فارغ"; exit 1; }
grep -qF "Version = \"$V\"" src/HalawaHub.Core/AppInfo.cs || { echo "META FAIL: ختم AppInfo != VERSION ($V)"; exit 1; }
TOP="$(grep -m1 -oE '"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"' CHANGELOG.json | tr -d '"')"
[ "$TOP" = "$V" ] || { echo "META FAIL: أعلى إدخال CHANGELOG='$TOP' != VERSION='$V'"; exit 1; }
if grep -qE '"[0-9][0-9.]* ": ' CHANGELOG.json; then echo "META FAIL: مفاتيح CHANGELOG بمسافة زائدة"; exit 1; fi
BAL=$(awk '{for(i=1;i<=length($0);i++){c=substr($0,i,1); if(c=="{")b++; else if(c=="}")b--}} END{print b+0}' CHANGELOG.json)
[ "$BAL" = "0" ] || { echo "META FAIL: أقواس CHANGELOG غير متوازنة"; exit 1; }
grep -qF "v${V} (Beta)" README.md || echo "WARN: README ما يذكر v${V} (Beta)"
echo "meta OK: $V"
