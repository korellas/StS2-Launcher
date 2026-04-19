#!/bin/bash
set -e

# Resolve NDK path: prefer ANDROID_NDK_ROOT, otherwise auto-pick the highest-
# numbered NDK installed under the SDK root. Covers both Linux (~/Android/Sdk)
# and macOS (~/Library/Android/sdk) defaults.
if [ -z "${ANDROID_NDK_ROOT:-}" ]; then
    if [ -n "${ANDROID_HOME:-}" ] && [ -d "$ANDROID_HOME/ndk" ]; then
        SDK_ROOT="$ANDROID_HOME"
    elif [ -d "$HOME/Library/Android/sdk/ndk" ]; then
        SDK_ROOT="$HOME/Library/Android/sdk"
    elif [ -d "$HOME/Android/Sdk/ndk" ]; then
        SDK_ROOT="$HOME/Android/Sdk"
    else
        echo "ERROR: ANDROID_NDK_ROOT not set and no SDK found in default locations" >&2
        exit 1
    fi
    NDK_VERSION=$(ls -1 "$SDK_ROOT/ndk" | sort -V | tail -1)
    NDK="$SDK_ROOT/ndk/$NDK_VERSION"
else
    NDK="$ANDROID_NDK_ROOT"
fi

case "$(uname -s)" in
    Darwin) HOST=darwin-x86_64 ;;
    Linux)  HOST=linux-x86_64 ;;
    *) echo "Unsupported host: $(uname -s)" >&2; exit 1 ;;
esac

CC="$NDK/toolchains/llvm/prebuilt/$HOST/bin/aarch64-linux-android24-clang"
if [ ! -x "$CC" ]; then
    echo "ERROR: compiler not found at $CC" >&2
    exit 1
fi

cd "$(dirname "$0")"
OUT=out/arm64-v8a
mkdir -p "$OUT"

$CC -shared -o "$OUT/libsteam_api.so" steam_stub.c steam_stub_auto.c -Wl,-soname,libsteam_api.so
$CC -shared -o "$OUT/libsentry.so" sentry_stub.c

ls -lh "$OUT/"
