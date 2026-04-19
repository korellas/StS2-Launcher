#!/usr/bin/env bash
set -euo pipefail

NO_BUMP=false
CONFIG="Release"
for arg in "$@"; do
    case "$arg" in
        --no-bump) NO_BUMP=true ;;
        --debug)   CONFIG="Debug" ;;
        *)
            echo "Unknown flag: $arg" >&2
            echo "Usage: build.sh [--no-bump] [--debug]" >&2
            exit 1
            ;;
    esac
done

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PATCHER_DIR="$ROOT/src/STS2Mobile"
BUILD_DIR="$ROOT/android"
GRADLE_PROPS="$BUILD_DIR/gradle.properties"
APK_DIR="$BUILD_DIR/build/outputs/apk/mono/release"

# 1. Format
echo "Formatting C# code..."
~/.dotnet/tools/csharpier format "$PATCHER_DIR"

# 2. Build patcher
echo "Building patcher (config=$CONFIG)..."
cd "$PATCHER_DIR"
dotnet publish -c "$CONFIG"

PUBLISH_DIR="$PATCHER_DIR/bin/$CONFIG/net9.0/publish"
BCL_DIR="$BUILD_DIR/assets/dotnet_bcl"
mkdir -p "$BCL_DIR"

cp "$PUBLISH_DIR"/STS2Mobile.dll "$PUBLISH_DIR"/SteamKit2.dll \
   "$PUBLISH_DIR"/protobuf-net.dll "$PUBLISH_DIR"/protobuf-net.Core.dll \
   "$PUBLISH_DIR"/System.IO.Hashing.dll "$PUBLISH_DIR"/ZstdSharp.dll \
   "$BCL_DIR/"

cp "$ROOT/upstream/godot-export/.godot/mono/publish/arm64/GodotSharp.dll" "$BCL_DIR/"

# Pick the newest installed Mono.android-arm64 runtime pack version (was hardcoded to 9.0.7).
CRYPTO_PACK_DIR="$HOME/.nuget/packages/microsoft.netcore.app.runtime.mono.android-arm64"
if [ -d "$CRYPTO_PACK_DIR" ]; then
    CRYPTO_VER=$(ls -1 "$CRYPTO_PACK_DIR" | sort -V | tail -1)
    CRYPTO_NATIVE_DIR="$CRYPTO_PACK_DIR/$CRYPTO_VER/runtimes/android-arm64/native"
    if [ -f "$CRYPTO_NATIVE_DIR/libSystem.Security.Cryptography.Native.Android.so" ]; then
        cp "$CRYPTO_NATIVE_DIR/libSystem.Security.Cryptography.Native.Android.so" "$BUILD_DIR/libs/release/arm64-v8a/"
    fi
    if [ -f "$CRYPTO_NATIVE_DIR/libSystem.Security.Cryptography.Native.Android.jar" ]; then
        cp "$CRYPTO_NATIVE_DIR/libSystem.Security.Cryptography.Native.Android.jar" "$BUILD_DIR/libs/release/"
    fi
fi

echo "Copied patcher + dependencies to android assets"

# 3. Bump version (skip with --no-bump)
CURRENT_NAME=$(grep '^export_version_name=' "$GRADLE_PROPS" | cut -d= -f2)
CURRENT_CODE=$(grep '^export_version_code=' "$GRADLE_PROPS" | cut -d= -f2)

if [ "$NO_BUMP" = true ]; then
    NEW_NAME="$CURRENT_NAME"
    NEW_CODE="$CURRENT_CODE"
    echo "Version: $NEW_NAME ($NEW_CODE) (no bump)"
else
    IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_NAME"
    PATCH=$((PATCH + 1))
    NEW_NAME="$MAJOR.$MINOR.$PATCH"
    NEW_CODE=$((CURRENT_CODE + 1))

    sed -i "s/^export_version_name=.*/export_version_name=$NEW_NAME/" "$GRADLE_PROPS"
    sed -i "s/^export_version_code=.*/export_version_code=$NEW_CODE/" "$GRADLE_PROPS"
    echo "Version: $CURRENT_NAME ($CURRENT_CODE) -> $NEW_NAME ($NEW_CODE)"
fi

# 4. Build APK
echo "Building APK..."
cd "$BUILD_DIR"
./gradlew assembleMonoRelease

echo "Done: $APK_DIR/StS2Launcher-v$NEW_NAME.apk"
