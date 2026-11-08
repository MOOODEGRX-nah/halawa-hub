#!/usr/bin/env bash
# يثبّت Halawa-Hub للمستخدم الحالي بدون صلاحيات root:
#   البرنامج:  ~/.local/opt/halawa-hub
#   القائمة:   ~/.local/share/applications/halawa-hub.desktop
#   الأيقونة:  ~/.local/share/icons/hicolor/<size>/apps/halawa-hub.png
#   الأمر:     ~/.local/bin/halawa-hub
# بيانات المستخدم في ~/.local/share/HalawaHub لا تُلمس. التشغيل مرة ثانية يحدّث التثبيت.
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$HOME/.local/opt/halawa-hub"
BIN_DIR="$HOME/.local/bin"
MARKER=".halawa-hub-tarball"

if [ ! -x "$SOURCE_DIR/Halawa-Hub" ]; then
    echo "error: Halawa-Hub executable not found next to install.sh ($SOURCE_DIR)" >&2
    exit 1
fi

# قيمة Exec حسب Desktop Entry Specification، نفس قواعد LinuxDesktopEntry.QuoteExec
quote_exec() {
    printf '"%s"' "$(printf '%s' "$1" | sed -e 's/\\/\\\\\\\\/g' -e 's/["`$]/\\\\&/g' -e 's/%/%%/g')"
}

copy_program() {
    if [ "$SOURCE_DIR" = "$APP_DIR" ]; then
        return 0
    fi

    # لا نستبدل مجلدًا ليس لنا: مسار موجود بدون العلامة قد يكون أي شيء
    if [ -e "$APP_DIR" ] && [ ! -e "$APP_DIR/$MARKER" ]; then
        echo "error: $APP_DIR exists and is not a Halawa-Hub install; move it away first" >&2
        exit 1
    fi

    mkdir -p "$(dirname "$APP_DIR")"
    local staging="$APP_DIR.install-$$"
    local previous="$APP_DIR.previous-$$"
    mkdir -p "$staging"
    cp -a "$SOURCE_DIR/." "$staging/"
    chmod +x "$staging/Halawa-Hub"

    if [ -d "$APP_DIR" ]; then
        # إضافات أضافها المستخدم تبقى مع إعادة التثبيت
        if [ -d "$APP_DIR/Plugins" ]; then
            mkdir -p "$staging/Plugins"
            cp -an "$APP_DIR/Plugins/." "$staging/Plugins/"
        fi
        mv "$APP_DIR" "$previous"
        mv "$staging" "$APP_DIR"
        rm -r -- "$previous"
    else
        mv "$staging" "$APP_DIR"
    fi
}

write_menu_entry() {
    local entry="$DATA_HOME/applications/halawa-hub.desktop"
    mkdir -p "$(dirname "$entry")"
    cat > "$entry.tmp-$$" <<EOF
[Desktop Entry]
Type=Application
Name=Halawa-Hub
Comment=Game library launcher
Comment[ar]=مشغّل مكتبة الألعاب
Exec=$(quote_exec "$APP_DIR/Halawa-Hub")
Icon=halawa-hub
Terminal=false
Categories=Game;
StartupWMClass=Halawa-Hub
EOF
    mv "$entry.tmp-$$" "$entry"
}

install_icons() {
    local size source
    for source in "$APP_DIR"/share/icons/hicolor/*/apps/halawa-hub.png; do
        size="$(basename "$(dirname "$(dirname "$source")")")"
        mkdir -p "$DATA_HOME/icons/hicolor/$size/apps"
        cp -f "$source" "$DATA_HOME/icons/hicolor/$size/apps/halawa-hub.png"
    done
}

refresh_desktop_caches() {
    # تحسينات اختيارية: كل سطح مكتب يوفر بعضها فقط، وغيابها لا يمنع ظهور البرنامج
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$DATA_HOME/applications" 2>/dev/null || true
    command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$DATA_HOME/icons/hicolor" 2>/dev/null || true
    command -v kbuildsycoca6 >/dev/null 2>&1 && kbuildsycoca6 >/dev/null 2>&1 || true
    return 0
}

copy_program
write_menu_entry
install_icons
mkdir -p "$BIN_DIR"
ln -sfn "$APP_DIR/Halawa-Hub" "$BIN_DIR/halawa-hub"
refresh_desktop_caches

echo "Halawa-Hub installed in $APP_DIR"
echo "Open it from the applications menu, or run: $BIN_DIR/halawa-hub"
echo "To remove it: $APP_DIR/uninstall.sh"
