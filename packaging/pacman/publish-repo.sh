#!/usr/bin/env bash
# ينشر ناتج build-repo.sh في إصدار GitHub الثابت "pacman-repo"، وهو عنوان Server في pacman.conf.
# الاستخدام: publish-repo.sh <repo-dir>   (يحتاج gh مع GH_TOKEN أو تسجيل دخول، وGH_REPO خارج المستودع)
#
# prerelease عمدًا: لا يصبح أبدًا "أحدث إصدار"، فلا يمس المحدّث الذاتي ولا رابط releases/latest.
set -euo pipefail

REPO_DIR="${1:?usage: publish-repo.sh <repo-dir>}"
TAG="pacman-repo"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! gh release view "$TAG" >/dev/null 2>&1; then
    gh release create "$TAG" --prerelease --latest=false --title "pacman repository" \
        --notes "مستودع pacman لـ Halawa-Hub، يتحدث تلقائيًا مع كل إصدار. طريقة الإضافة في README. / pacman repository for Halawa-Hub, updated automatically with each release. See the README."
fi

# الحزمة قبل القاعدة: من يقرأ القاعدة الجديدة يجب أن يجد الملف الذي تشير إليه
gh release upload "$TAG" --clobber "$REPO_DIR"/*.pkg.tar.zst "$REPO_DIR"/*.pkg.tar.zst.sig "$HERE/halawa-hub-repo.asc"
gh release upload "$TAG" --clobber \
    "$REPO_DIR/halawa-hub.db" "$REPO_DIR/halawa-hub.db.sig" \
    "$REPO_DIR/halawa-hub.files" "$REPO_DIR/halawa-hub.files.sig"

# نبقي إصدارين: من حدّث قاعدته قبل لحظات يجب أن يجد الحزمة التي طلبها
mapfile -t packages < <(gh release view "$TAG" --json assets --jq '.assets[].name | select(endswith(".pkg.tar.zst"))' | sort -V)
count="${#packages[@]}"
if [ "$count" -gt 2 ]; then
    for package in "${packages[@]:0:count-2}"; do
        gh release delete-asset "$TAG" "$package" --yes
        gh release delete-asset "$TAG" "$package.sig" --yes
    done
fi

gh release view "$TAG" --json assets --jq '.assets[].name'
