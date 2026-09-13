#!/bin/bash
# رفع نسخة جديدة من Halawa-Hub لـ GitHub
# الاستخدام: من داخل مجلد المشروع (اللي فيه HalawaHub.sln)، بـ Git Bash:
#   bash git-push.sh "v0.0.10.2 beta"
# لو ما مررت رسالة، بيستخدم رقم ملف VERSION تلقائيًا

set -e

# حماية من نفس الغلطة اللي صارت قبل: تشغيل الأوامر من مجلد غلط (زي مجلد المستخدم)
if [ ! -f "HalawaHub.sln" ]; then
  echo "خطأ: هذا المجلد ما فيه HalawaHub.sln — يعني ما أنت داخل مجلد المشروع."
  echo "افتح Git Bash من داخل مجلد halawa-hub نفسه (Git Bash Here) وشغّل السكربت من جديد."
  exit 1
fi

MSG="${1:-$(cat VERSION) beta}"

git init
git add .
git commit -m "$MSG"
git branch -M main
git remote remove origin 2>/dev/null || true
git remote add origin https://github.com/MOOODEGRX-nah/halawa-hub.git
git push -u origin main --force

echo "تم الرفع: $MSG"
