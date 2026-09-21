#!/bin/bash
set -e

# تحديث الإصدار تلقائيًا (اختياري)
if [ -f VERSION ]; then
    echo "Current version: $(cat VERSION)"
fi

# إضافة جميع التغييرات
git add -A

# التحقق من وجود تغييرات قبل commit
if git diff --cached --quiet; then
    echo "لا توجد تغييرات للرفع"
    exit 0
fi

# commit برسالة تلقائية
VERSION=$(cat VERSION 2>/dev/null || echo "unknown")
git commit -m "v${VERSION}: auto-commit"

# رفع آمن (بدون force — لو فيه تعارض، git بيرفض ونحله يدويًا)
git push origin main

echo "✅ تم الرفع بنجاح"
