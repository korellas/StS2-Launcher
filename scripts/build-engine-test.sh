#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENGINE_DIR="$ROOT/vendor/godot"
WORK_DIR="$ROOT/android/build/engine452-test"
BUILD_PYTHON="$ROOT/vendor/godot-build-venv/bin/python"
PATCH_FILE="$ROOT/scripts/godot/4.5.2-mobile.patch"

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export JAVA_HOME="${JAVA_HOME:-/Applications/Android Studio.app/Contents/jbr/Contents/Home}"
export PATH="$JAVA_HOME/bin:$PATH"

bash "$ROOT/scripts/verify-package-assets.sh" inputs "$ROOT/android/assets"
mkdir -p "$WORK_DIR" "$ROOT/vendor"

python3 - "$ROOT" <<'PY'
import pathlib
import runpy
import subprocess
import sys
import tarfile
import urllib.request

vendor = pathlib.Path(sys.argv[1]) / "vendor"

def fetch(url, name):
    target = vendor / name
    if not target.exists():
        temporary = target.with_suffix(target.suffix + ".download")
        urllib.request.urlretrieve(url, temporary)
        temporary.replace(target)
    return target

engine = vendor / "godot"
if not engine.exists():
    archive = fetch("https://codeload.github.com/godotengine/godot/tar.gz/refs/tags/4.5.2-stable", "godot-4.5.2.tar.gz")
    with tarfile.open(archive) as source:
        source.extractall(vendor, filter="data")
    (vendor / "godot-4.5.2-stable").rename(engine)
version = runpy.run_path(str(engine / "version.py"))
if (version["major"], version["minor"], version["patch"]) != (4, 5, 2):
    raise SystemExit("vendor/godot must contain Godot 4.5.2")
swappy = engine / "thirdparty/swappy-frame-pacing"
if not (swappy / "arm64-v8a/libswappy_static.a").exists():
    archive = fetch("https://github.com/godotengine/godot-swappy/releases/download/from-source-2025-01-31/godot-swappy.7z", "godot-swappy.7z")
    subprocess.run(["tar", "-xf", str(archive), "-C", str(swappy)], check=True)
fetch("https://github.com/godotengine/godot-builds/releases/download/4.5.2-stable/godot-lib.4.5.2.stable.mono.template_release.aar", "godot-lib.4.5.2.stable.mono.template_release.aar")
fetch("https://api.nuget.org/v3-flatcontainer/godotsharp/4.5.2/godotsharp.4.5.2.nupkg", "godotsharp.4.5.2.nupkg")
PY

if [ ! -x "$BUILD_PYTHON" ]; then
    python3 -m venv "$ROOT/vendor/godot-build-venv"
    "$BUILD_PYTHON" -m pip install scons==4.10.1
fi

if patch --dry-run --batch --forward -p1 -d "$ENGINE_DIR" < "$PATCH_FILE" >/dev/null 2>&1; then
    patch --batch --forward -p1 -d "$ENGINE_DIR" < "$PATCH_FILE"
elif ! patch --dry-run --batch --reverse -p1 -d "$ENGINE_DIR" < "$PATCH_FILE" >/dev/null 2>&1; then
    echo "ERROR: engine source does not match the complete 4.5.2 mobile patch" >&2
    exit 1
fi

bash "$ROOT/scripts/tests/engine-game-bootstrap-test.sh"

BUILD_NAME=sts2mobile452test "$BUILD_PYTHON" -m SCons -C "$ENGINE_DIR" \
    platform=android arch=arm64 target=template_release module_mono_enabled=yes \
    swappy=yes debug_symbols=no -j"${ENGINE_BUILD_JOBS:-8}"

# Stage the app separately so testing cannot replace production engine inputs.
python3 - "$ROOT" "$WORK_DIR" <<'PY'
import pathlib
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile

root, work = map(pathlib.Path, sys.argv[1:])
project = work / "project"
shutil.copytree(root / "android", project, dirs_exist_ok=True,
                ignore=shutil.ignore_patterns("build", ".gradle", ".cxx", "*.keystore", "local.properties"))
manifest = project / "AndroidManifest.xml"
text = manifest.read_text().replace("Unofficial StS2 Launcher", "StS2 Launcher 4.5.2 테스트")
text = text.replace('android:name=".GodotApp"', 'android:name="com.game.sts2launcher.GodotApp"')
text = text.replace('android:name=".UpdateFileProvider"', 'android:name="com.game.sts2launcher.UpdateFileProvider"')
manifest.write_text(text)

native = root / "vendor/godot/platform/android/java/lib/libs/release/arm64-v8a"
aar = project / "libs/release/godot-lib.template_release.aar"
with zipfile.ZipFile(root / "vendor/godot-lib.4.5.2.stable.mono.template_release.aar") as original:
    with zipfile.ZipFile(aar, "w", zipfile.ZIP_DEFLATED) as patched:
        for entry in original.infolist():
            if entry.filename.startswith("jni/"):
                continue
            patched.writestr(entry, original.read(entry))
        for name in ("libgodot_android.so", "libc++_shared.so"):
            patched.write(native / name, "jni/arm64-v8a/" + name)
# The same STL may also be present beside the AAR; keep both inputs identical.
shutil.copy2(native / "libc++_shared.so", project / "libs/release/arm64-v8a/libc++_shared.so")
duplicate = project / "libs/release/arm64-v8a/libgodot_android.so"
if duplicate.exists():
    duplicate.unlink()

bindings = work / "GodotSharp.dll"
with zipfile.ZipFile(root / "vendor/godotsharp.4.5.2.nupkg") as package:
    bindings.write_bytes(package.read("lib/net8.0/GodotSharp.dll"))
original_project = root / "src/STS2Mobile/STS2Mobile.csproj"
tree = ET.parse(original_project)
tree.find("./PropertyGroup/EnableDefaultCompileItems").text = "false"
ET.SubElement(tree.find("./PropertyGroup"), "DefineConstants").text = "$(DefineConstants);ENGINE_452_TEST"
for reference in tree.findall("./ItemGroup/Reference"):
    hint = reference.find("HintPath")
    hint.text = str(bindings if reference.get("Include") == "GodotSharp"
                    else (original_project.parent / hint.text).resolve())
sources = ET.SubElement(tree.getroot(), "ItemGroup")
ET.SubElement(sources, "Compile", {
    "Include": str(original_project.parent / "**/*.cs"),
    "Exclude": ";".join(str(original_project.parent / directory / "**/*.cs") for directory in ("bin", "obj")),
})
(work / "patcher").mkdir(exist_ok=True)
for resource in tree.findall("./ItemGroup/EmbeddedResource"):
    relative = pathlib.Path(resource.get("Include"))
    destination = work / "patcher" / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(original_project.parent / relative, destination)
tree.write(work / "patcher/STS2Mobile.csproj", encoding="unicode")
shutil.copy2(bindings, project / "assets/dotnet_bcl/GodotSharp.dll")
PY

python3 "$ROOT/scripts/make-bootstrap-pck.py" --output "$WORK_DIR/project/assets/bootstrap.pck"

dotnet publish "$WORK_DIR/patcher/STS2Mobile.csproj" -c Release \
    -p:NuGetAudit=false -o "$WORK_DIR/publish"
for name in STS2Mobile.dll SteamKit2.dll protobuf-net.dll protobuf-net.Core.dll System.IO.Hashing.dll ZstdSharp.dll; do
    cp "$WORK_DIR/publish/$name" "$WORK_DIR/project/assets/dotnet_bcl/"
done
bash "$ROOT/scripts/verify-package-assets.sh" inputs "$WORK_DIR/project/assets"

if [ ! -f "$WORK_DIR/engine452-test.keystore" ]; then
    keytool -genkeypair -noprompt -keystore "$WORK_DIR/engine452-test.keystore" \
        -storepass android -keypass android -alias engine452-test \
        -keyalg RSA -keysize 2048 -validity 3650 \
        -dname "CN=StS2 Engine 4.5.2 Test"
fi

VERSION_NAME="$(sed -n 's/^export_version_name=//p' "$ROOT/android/gradle.properties")-engine452-test"
(
    cd "$WORK_DIR/project"
    ./gradlew assembleMonoRelease \
        -Pexport_package_name=com.game.sts2launcher.engine452test \
        -Pexport_version_name="$VERSION_NAME" -Pexport_version_code="$(date +%s)" \
        -Pperform_signing=true -Pperform_zipalign=true \
        -Prelease_keystore_file="$WORK_DIR/engine452-test.keystore" \
        -Prelease_keystore_password=android -Prelease_keystore_alias=engine452-test
)

APK="$WORK_DIR/StS2Launcher-engine452-test.apk"
cp "$WORK_DIR/project/build/outputs/apk/mono/release/StS2Launcher-v$VERSION_NAME.apk" "$APK"
bash "$ROOT/scripts/verify-package-assets.sh" apk "$APK"
"$ANDROID_HOME/build-tools/35.0.0/apksigner" verify --verbose --print-certs "$APK"
"$ANDROID_HOME/build-tools/35.0.0/aapt" dump badging "$APK" | sed -n '1,8p'
echo "Test APK: $APK"
