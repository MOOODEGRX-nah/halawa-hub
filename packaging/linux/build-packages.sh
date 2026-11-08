#!/usr/bin/env bash
# يبني حزم لينكس من الكود: Halawa-Hub-linux-x64.tar.gz وHalawa-Hub-x86_64.AppImage.
# CI وbuild.yml يستدعيان هذا السكربت نفسه، فما نختبره هو ما يُنشر بالضبط.
# الاستخدام: bash packaging/linux/build-packages.sh <output-dir>
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

OUTPUT_DIR="$(mkdir -p "${1:?usage: build-packages.sh <output-dir>}" && cd "$1" && pwd)"
VERSION="$(tr -d '\r\n' < VERSION)"
PACKAGING="$PWD/packaging/linux"

# أدوات AppImage بإصدار وهاش ثابتين: لا يتغير البناء لأن "continuous" تغيّر
APPIMAGETOOL_URL="https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage"
APPIMAGETOOL_SHA256="ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0"
RUNTIME_URL="https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64"
RUNTIME_SHA256="2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d"

WORK="$(mktemp -d)"
cleanup() { rm -r -- "$WORK"; }
trap cleanup EXIT

download_verified() {
    local url="$1" sha="$2" target="$3"
    curl -fsSL --retry 3 -o "$target" "$url"
    echo "$sha  $target" | sha256sum --check --quiet
}

echo "===== publish linux-x64 v$VERSION ====="
PUBLISH="$WORK/publish"
# ملف واحد للكود، والمكتبات الأصلية (.so) بجانبه: بلا فك ملفات في ~/.net عند كل إصدار.
# InvariantGlobalization: البرنامج لا يعتمد على ثقافة النظام، وبدونه يتوقف عن العمل
# تمامًا على أي توزيعة بلا libicu.
dotnet publish src/HalawaHub.App/HalawaHub.App.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:InvariantGlobalization=true \
    --nologo -o "$PUBLISH"
find "$PUBLISH" -name '*.pdb' -delete

dotnet build src/HalawaHub.Plugins.Sample/HalawaHub.Plugins.Sample.csproj -c Release -o "$WORK/plugin" --nologo
mkdir -p "$PUBLISH/Plugins"
cp "$WORK/plugin/HalawaHub.Plugins.Sample.dll" "$PUBLISH/Plugins/"

echo "===== verify version (prevents E09 infinite update loop) ====="
chmod +x "$PUBLISH/Halawa-Hub"
ACTUAL="$("$PUBLISH/Halawa-Hub" --version)"
echo "Expected: $VERSION"
echo "Actual:   $ACTUAL"
if [ "$ACTUAL" != "$VERSION" ]; then
    echo "error: binary version '$ACTUAL' does not match VERSION '$VERSION'" >&2
    exit 1
fi

echo "===== tar.gz ====="
TAR_ROOT="$WORK/tar/Halawa-Hub"
mkdir -p "$TAR_ROOT"
cp -a "$PUBLISH/." "$TAR_ROOT/"
cp "$PACKAGING/install.sh" "$PACKAGING/uninstall.sh" LICENSE "$TAR_ROOT/"
chmod +x "$TAR_ROOT/install.sh" "$TAR_ROOT/uninstall.sh"
mkdir -p "$TAR_ROOT/share/icons"
cp -a "$PACKAGING/icons/hicolor" "$TAR_ROOT/share/icons/"
# العلامة التي تسمح بالتحديث الذاتي (InstallInfo.TarballMarkerFile)
: > "$TAR_ROOT/.halawa-hub-tarball"
tar --sort=name --owner=0 --group=0 --numeric-owner -C "$WORK/tar" -czf "$OUTPUT_DIR/Halawa-Hub-linux-x64.tar.gz" Halawa-Hub

echo "===== AppImage ====="
APPDIR="$WORK/Halawa-Hub.AppDir"
mkdir -p "$APPDIR/usr/lib/halawa-hub" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons"
cp -a "$PUBLISH/." "$APPDIR/usr/lib/halawa-hub/"
cp -a "$PACKAGING/icons/hicolor" "$APPDIR/usr/share/icons/"
install -m755 "$PACKAGING/AppRun" "$APPDIR/AppRun"
install -m644 "$PACKAGING/halawa-hub.desktop" "$APPDIR/halawa-hub.desktop"
install -m644 "$PACKAGING/halawa-hub.desktop" "$APPDIR/usr/share/applications/halawa-hub.desktop"
install -m644 "$PACKAGING/icons/hicolor/256x256/apps/halawa-hub.png" "$APPDIR/halawa-hub.png"
ln -s halawa-hub.png "$APPDIR/.DirIcon"

download_verified "$APPIMAGETOOL_URL" "$APPIMAGETOOL_SHA256" "$WORK/appimagetool"
download_verified "$RUNTIME_URL" "$RUNTIME_SHA256" "$WORK/runtime-x86_64"
chmod +x "$WORK/appimagetool"

# extract-and-run: أجهزة CI بلا FUSE
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$WORK/appimagetool" --no-appstream \
    --runtime-file "$WORK/runtime-x86_64" \
    "$APPDIR" "$OUTPUT_DIR/Halawa-Hub-x86_64.AppImage"
chmod +x "$OUTPUT_DIR/Halawa-Hub-x86_64.AppImage"

ls -la "$OUTPUT_DIR"
