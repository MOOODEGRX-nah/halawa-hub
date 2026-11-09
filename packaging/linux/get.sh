#!/usr/bin/env bash
# تثبيت Halawa-Hub بسطر واحد على أي توزيعة، للمستخدم الحالي وبدون sudo:
#
#   curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh | bash
#
# الإزالة (بياناتك في ~/.local/share/HalawaHub تبقى كما هي):
#
#   curl -fsSL https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/main/packaging/linux/get.sh | bash -s -- --uninstall
#
# الكود كله داخل main: لو انقطع التنزيل عبر الأنبوب في منتصفه لا يُنفَّذ نصف سكربت.
set -euo pipefail

main() {
    local release_url="${HALAWA_RELEASE_URL:-https://github.com/MOOODEGRX-nah/halawa-hub/releases/latest/download}"
    local asset="Halawa-Hub-linux-x64.tar.gz"
    local app_dir="$HOME/.local/opt/halawa-hub"

    fail() {
        echo "error: $*" >&2
        exit 1
    }

    # root سيثبّت في /root ولن يجد المستخدم البرنامج في قائمته
    if [ "$(id -u)" = "0" ]; then
        fail "run this as your normal user, without sudo"
    fi

    case "${1:-}" in
        "") ;;
        --uninstall)
            if [ ! -x "$app_dir/uninstall.sh" ]; then
                fail "Halawa-Hub is not installed in $app_dir"
            fi
            "$app_dir/uninstall.sh"
            return 0
            ;;
        *)
            fail "unknown option: $1 (the only option is --uninstall)"
            ;;
    esac

    if [ "$(uname -m)" != "x86_64" ]; then
        fail "Halawa-Hub for Linux is only built for x86_64 (this machine is $(uname -m))"
    fi

    local tool
    for tool in tar sha256sum awk; do
        command -v "$tool" >/dev/null 2>&1 || fail "$tool is required"
    done

    local download
    if command -v curl >/dev/null 2>&1; then
        download() { curl -fsSL --retry 3 -o "$2" "$1"; }
    elif command -v wget >/dev/null 2>&1; then
        download() { wget -q -O "$2" "$1"; }
    else
        fail "curl or wget is required"
    fi

    WORK_DIR="$(mktemp -d)"
    trap 'rm -r -- "$WORK_DIR"' EXIT

    echo "Downloading Halawa-Hub..."
    download "$release_url/SHA256SUMS.txt" "$WORK_DIR/SHA256SUMS.txt" || fail "could not download SHA256SUMS.txt"
    download "$release_url/$asset" "$WORK_DIR/$asset" || fail "could not download $asset"

    local expected actual
    expected="$(awk -v asset="$asset" '$2 == asset || $2 == "*" asset { print $1 }' "$WORK_DIR/SHA256SUMS.txt")"
    if [ -z "$expected" ]; then
        fail "$asset is not listed in SHA256SUMS.txt, refusing to install"
    fi
    actual="$(sha256sum "$WORK_DIR/$asset" | awk '{ print $1 }')"
    if [ "$actual" != "$expected" ]; then
        fail "SHA256 mismatch for $asset, refusing to install (expected $expected, got $actual)"
    fi
    echo "SHA256 verified."

    tar -xzf "$WORK_DIR/$asset" -C "$WORK_DIR"
    bash "$WORK_DIR/Halawa-Hub/install.sh"

    case ":$PATH:" in
        *":$HOME/.local/bin:"*) ;;
        *) echo "Note: $HOME/.local/bin is not in your PATH; use the applications menu or the full path." ;;
    esac
}

main "$@"
