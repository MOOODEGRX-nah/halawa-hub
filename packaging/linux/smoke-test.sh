#!/usr/bin/env bash
# يفحص حزم لينكس الجاهزة بدون شاشة: التثبيت في HOME مؤقت، الإصدار، مدخل القائمة،
# وأن إلغاء التثبيت يزيل كل شيء ولا يلمس بيانات المستخدم.
# الاستخدام: bash packaging/linux/smoke-test.sh <packages-dir>
set -euo pipefail

PACKAGES="$(cd "${1:?usage: smoke-test.sh <packages-dir>}" && pwd)"
VERSION="$(tr -d '\r\n' < "$(git rev-parse --show-toplevel)/VERSION")"
WORK="$(mktemp -d)"
cleanup() { rm -r -- "$WORK"; }
trap cleanup EXIT

fail() { echo "SMOKE FAIL: $*" >&2; exit 1; }

# مسافة في المسار عمدًا: أكثر ما يكسر سكربتات التثبيت وملفات .desktop
export HOME="$WORK/home with space"
export XDG_DATA_HOME="$HOME/.local/share"
export XDG_CONFIG_HOME="$HOME/.config"
mkdir -p "$HOME" "$XDG_DATA_HOME/HalawaHub" "$XDG_CONFIG_HOME/autostart"
echo '{"keep":"me"}' > "$XDG_DATA_HOME/HalawaHub/favorites.json"

echo "== tar.gz contents"
tar -xzf "$PACKAGES/Halawa-Hub-linux-x64.tar.gz" -C "$WORK"
for file in Halawa-Hub libSkiaSharp.so libHarfBuzzSharp.so libe_sqlite3.so install.sh uninstall.sh .halawa-hub-tarball \
            LICENSE Plugins/HalawaHub.Plugins.Sample.dll share/icons/hicolor/256x256/apps/halawa-hub.png; do
    [ -e "$WORK/Halawa-Hub/$file" ] || fail "missing from tar.gz: $file"
done
[ -x "$WORK/Halawa-Hub/Halawa-Hub" ] || fail "binary lost its executable bit"

echo "== install"
bash "$WORK/Halawa-Hub/install.sh"
APP_DIR="$HOME/.local/opt/halawa-hub"
ENTRY="$XDG_DATA_HOME/applications/halawa-hub.desktop"
[ -x "$APP_DIR/Halawa-Hub" ] || fail "binary not installed"
[ -f "$ENTRY" ] || fail "menu entry not written"
[ -f "$XDG_DATA_HOME/icons/hicolor/256x256/apps/halawa-hub.png" ] || fail "icon not installed"
grep -qF "Exec=\"$APP_DIR/Halawa-Hub\"" "$ENTRY" || fail "wrong Exec in the menu entry: $(grep ^Exec= "$ENTRY")"
if command -v desktop-file-validate >/dev/null 2>&1; then
    desktop-file-validate "$ENTRY" || fail "menu entry is invalid"
fi

echo "== version"
[ "$("$APP_DIR/Halawa-Hub" --version)" = "$VERSION" ] || fail "installed binary has the wrong version"
[ "$("$HOME/.local/bin/halawa-hub" --version)" = "$VERSION" ] || fail "launcher link is broken"

echo "== reinstall keeps user plugins"
echo mine > "$APP_DIR/Plugins/Mine.dll"
bash "$WORK/Halawa-Hub/install.sh"
[ -f "$APP_DIR/Plugins/Mine.dll" ] || fail "reinstall lost a user plugin"

echo "== install refuses a foreign folder"
FOREIGN_HOME="$WORK/foreign"
mkdir -p "$FOREIGN_HOME/.local/opt/halawa-hub"
echo precious > "$FOREIGN_HOME/.local/opt/halawa-hub/file"
if HOME="$FOREIGN_HOME" XDG_DATA_HOME="$FOREIGN_HOME/d" bash "$WORK/Halawa-Hub/install.sh" 2>/dev/null; then
    fail "install.sh replaced a folder it does not own"
fi
[ -f "$FOREIGN_HOME/.local/opt/halawa-hub/file" ] || fail "install.sh damaged a foreign folder"

echo "== uninstall"
cat > "$XDG_CONFIG_HOME/autostart/halawa-hub.desktop" <<ENTRY_EOF
[Desktop Entry]
Exec="$APP_DIR/Halawa-Hub"
ENTRY_EOF
bash "$APP_DIR/uninstall.sh"
[ ! -e "$APP_DIR" ] || fail "program folder left behind"
[ ! -e "$ENTRY" ] || fail "menu entry left behind"
[ ! -e "$XDG_CONFIG_HOME/autostart/halawa-hub.desktop" ] || fail "autostart entry left behind"
[ ! -e "$XDG_DATA_HOME/icons/hicolor/256x256/apps/halawa-hub.png" ] || fail "icon left behind"
[ ! -L "$HOME/.local/bin/halawa-hub" ] || fail "launcher link left behind"
grep -q keep "$XDG_DATA_HOME/HalawaHub/favorites.json" || fail "user data was touched"

echo "== AppImage"
APPIMAGE_FILE="$PACKAGES/Halawa-Hub-x86_64.AppImage"
chmod +x "$APPIMAGE_FILE"
[ "$(APPIMAGE_EXTRACT_AND_RUN=1 "$APPIMAGE_FILE" --version)" = "$VERSION" ] || fail "AppImage has the wrong version"

(cd "$WORK" && "$APPIMAGE_FILE" --appimage-extract >/dev/null)
for file in AppRun halawa-hub.desktop halawa-hub.png .DirIcon usr/lib/halawa-hub/Halawa-Hub usr/lib/halawa-hub/libSkiaSharp.so \
            usr/lib/halawa-hub/Plugins/HalawaHub.Plugins.Sample.dll usr/share/icons/hicolor/256x256/apps/halawa-hub.png; do
    [ -e "$WORK/squashfs-root/$file" ] || fail "missing from AppImage: $file"
done
[ ! -e "$WORK/squashfs-root/usr/lib/halawa-hub/.halawa-hub-tarball" ] || fail "AppImage must not look like a tarball install"

echo "SMOKE OK: v$VERSION"
