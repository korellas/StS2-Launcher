#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
export MOCK_ADB_LOG="$test_dir/adb.log"
cat > "$test_dir/adb" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_ADB_LOG"
if [ "${1:-}" = devices ]; then
    printf 'List of devices attached\n'
    case "$MOCK_MODE" in
        none) ;;
        multiple) printf 'phone-a\tdevice\nphone-b\tdevice\n' ;;
        *) printf 'phone-a\tdevice\n' ;;
    esac
    exit 0
fi
[ "$1" = -s ] && [ "$2" = phone-a ] || exit 91
shift 2
case "$*" in
    'shell getprop ro.build.version.sdk') echo 35 ;;
    'shell getprop ro.product.model') echo TestPhone ;;
    'shell getprop ro.product.cpu.abi') echo arm64-v8a ;;
    'shell dumpsys package com.game.sts2launcher') echo versionName=test ;;
    'shell perfetto --help') echo perfetto ;;
    'shell perfetto --txt -c - -o '*)
        cat > "$MOCK_ADB_LOG.config"
        [ "$MOCK_MODE" != perfetto-fail ] || exit 17
        ;;
    'pull '*)
        [ "$MOCK_MODE" = empty ] || printf 'mock-trace' > "$3"
        ;;
    'shell rm -f /data/misc/perfetto-traces/sts2-benchmark-'*) ;;
    *) echo "Unexpected adb action: $*" >&2; exit 92 ;;
esac
SH
chmod +x "$test_dir/adb"
export ADB="$test_dir/adb"
unset ANDROID_SERIAL
run() {
    : > "$MOCK_ADB_LOG"
    MOCK_MODE="$1" bash "$root/scripts/capture-benchmark-freeze.sh" --seconds 1 --out "$test_dir/$1" > "$test_dir/$1.log" 2>&1
}
if ! run success; then cat "$test_dir/success.log"; exit 1; fi
test -s "$test_dir/success/benchmark.perfetto-trace"
rg -q 'atrace_apps: "com.game.sts2launcher"' "$MOCK_ADB_LOG.config"
rg -q 'duration_ms: 1000' "$MOCK_ADB_LOG.config"
rg -q 'shell rm -f /data/misc/perfetto-traces/sts2-benchmark-' "$MOCK_ADB_LOG"
for mode in none multiple perfetto-fail empty; do
    if run "$mode"; then echo "Expected failure: $mode" >&2; exit 1; fi
    case "$mode" in
        none|multiple) if rg -q 'perfetto --txt|pull ' "$MOCK_ADB_LOG"; then exit 1; fi ;;
        perfetto-fail) if rg -q '^.*pull ' "$MOCK_ADB_LOG"; then exit 1; fi ;;
    esac
done
if rg -q 'install|force-stop|logcat -c|pm clear' "$MOCK_ADB_LOG"; then exit 1; fi
echo 'Benchmark trace capture tests passed'
