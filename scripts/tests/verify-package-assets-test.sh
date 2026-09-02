#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VERIFY="$ROOT/scripts/verify-package-assets.sh"
TEST_TMP="$(mktemp -d)"
trap 'rm -rf "$TEST_TMP"' EXIT

REQUIRED=(
    "bootstrap.pck"
    "dotnet_bcl/0Harmony.dll"
    "dotnet_bcl/GodotSharp.dll"
    "dotnet_bcl/System.Private.CoreLib.dll"
    "dotnet_bcl/System.Runtime.dll"
)

make_complete_assets() {
    local target="$1"
    local entry

    for entry in "${REQUIRED[@]}"; do
        mkdir -p "$target/$(dirname "$entry")"
        : > "$target/$entry"
    done
}

assert_missing_error() {
    local mode="$1"
    local target="$2"
    local expected="$3"
    local output

    if output=$(bash "$VERIFY" "$mode" "$target" 2>&1); then
        echo "FAIL: incomplete $mode fixture was accepted" >&2
        exit 1
    fi
    if [[ "$output" != *"ERROR: required package asset missing: $expected"* ]]; then
        echo "FAIL: missing-entry error did not name $expected" >&2
        echo "$output" >&2
        exit 1
    fi
}

complete_inputs="$TEST_TMP/complete-inputs"
make_complete_assets "$complete_inputs"
bash "$VERIFY" inputs "$complete_inputs" >/dev/null

incomplete_inputs="$TEST_TMP/incomplete-inputs"
make_complete_assets "$incomplete_inputs"
rm "$incomplete_inputs/dotnet_bcl/System.Runtime.dll"
assert_missing_error inputs "$incomplete_inputs" "dotnet_bcl/System.Runtime.dll"

complete_apk_root="$TEST_TMP/complete-apk-root"
make_complete_assets "$complete_apk_root/assets"
complete_apk="$TEST_TMP/complete.apk"
(
    cd "$complete_apk_root"
    zip -qr "$complete_apk" assets
)
bash "$VERIFY" apk "$complete_apk" >/dev/null

incomplete_apk_root="$TEST_TMP/incomplete-apk-root"
make_complete_assets "$incomplete_apk_root/assets"
rm "$incomplete_apk_root/assets/bootstrap.pck"
incomplete_apk="$TEST_TMP/incomplete.apk"
(
    cd "$incomplete_apk_root"
    zip -qr "$incomplete_apk" assets
)
assert_missing_error apk "$incomplete_apk" "assets/bootstrap.pck"

echo "PASS: package asset verifier"
