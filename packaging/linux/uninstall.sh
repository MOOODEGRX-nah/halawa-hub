#!/usr/bin/env bash
# يزيل ما كتبه install.sh فقط. بيانات المستخدم في ~/.local/share/HalawaHub
# (المفضلة وساعات اللعب والأغلفة) لا تُحذف أبدًا من هنا.
set -euo pipefail

main() {
    local app_dir data_home config_home
    app_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
    config_home="${XDG_CONFIG_HOME:-$HOME/.config}"

    # هذا السكربت يحذف المجلد الذي هو فيه، فلا يعمل إلا من تثبيت حقيقي
    if [ ! -e "$app_dir/.halawa-hub-tarball" ] || [ ! -x "$app_dir/Halawa-Hub" ]; then
        echo "error: $app_dir is not a Halawa-Hub install, nothing removed" >&2
        exit 1
    fi

    # مدخل القائمة والتشغيل التلقائي: فقط إذا كانا يشيران لهذا التثبيت
    local entry
    for entry in "$data_home/applications/halawa-hub.desktop" "$config_home/autostart/halawa-hub.desktop"; do
        if [ -f "$entry" ] && grep -qF "$app_dir/Halawa-Hub" "$entry"; then
            rm -f -- "$entry"
        fi
    done

    local icon
    for icon in "$data_home"/icons/hicolor/*/apps/halawa-hub.png; do
        if [ -f "$icon" ]; then
            rm -f -- "$icon"
        fi
    done

    local link="$HOME/.local/bin/halawa-hub"
    if [ -L "$link" ] && [ "$(readlink "$link")" = "$app_dir/Halawa-Hub" ]; then
        rm -f -- "$link"
    fi

    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$data_home/applications" 2>/dev/null || true
    command -v kbuildsycoca6 >/dev/null 2>&1 && kbuildsycoca6 >/dev/null 2>&1 || true

    cd /
    rm -r -- "$app_dir"

    echo "Halawa-Hub removed."
    echo "Your data was kept in ${HALAWAHUB_DATA_DIR:-$data_home/HalawaHub}; delete that folder yourself if you no longer need it."
}

main "$@"
exit 0
