#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <inputs|apk> <path>" >&2
    exit 2
fi

MODE="$1"
TARGET="$2"
REQUIRED=(
    "bootstrap.pck"
    "dotnet_bcl/0Harmony.dll"
    "dotnet_bcl/GodotSharp.dll"
    "dotnet_bcl/System.Private.CoreLib.dll"
    "dotnet_bcl/System.Runtime.dll"
)

missing() {
    echo "ERROR: required package asset missing: $1" >&2
    exit 1
}

case "$MODE" in
    inputs)
        [[ -d "$TARGET" ]] || missing "$TARGET"
        for entry in "${REQUIRED[@]}"; do
            [[ -f "$TARGET/$entry" ]] || missing "$entry"
        done
        echo "Package asset inputs verified"
        ;;

    apk)
        [[ -f "$TARGET" ]] || missing "$TARGET"
        APK_ENTRIES="$(unzip -Z1 "$TARGET")"
        for entry in "${REQUIRED[@]}"; do
            packaged="assets/$entry"
            found=false
            while IFS= read -r candidate; do
                if [[ "$candidate" == "$packaged" ]]; then
                    found=true
                    break
                fi
            done <<< "$APK_ENTRIES"
            [[ "$found" == true ]] || missing "$packaged"
        done
        echo "Packaged APK assets verified"
        ;;

    *)
        echo "Usage: $0 <inputs|apk> <path>" >&2
        exit 2
        ;;
esac
