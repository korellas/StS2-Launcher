#!/usr/bin/env bash
set -euo pipefail

seconds=180
serial="${ANDROID_SERIAL:-}"
output=""
usage() {
    echo 'Usage: capture-benchmark-freeze.sh [--serial DEVICE] [--seconds SECONDS] [--out NEW_DIRECTORY]'
}
while [ "$#" -gt 0 ]; do
    case "$1" in
        --serial|--seconds|--out)
            [ "$#" -ge 2 ] || { usage >&2; exit 1; }
            case "$1" in
                --serial) serial="$2" ;;
                --seconds) seconds="$2" ;;
                --out) output="$2" ;;
            esac
            shift 2 ;;
        --help) usage; exit 0 ;;
        *) usage >&2; exit 1 ;;
    esac
done
[[ "$seconds" =~ ^[0-9]{1,4}$ ]] || { echo 'Invalid duration' >&2; exit 1; }
seconds=$((10#$seconds))
[ "$seconds" -ge 1 ] && [ "$seconds" -le 3600 ] || { echo 'Duration must be 1–3600 seconds' >&2; exit 1; }
adb_bin="${ADB:-${ANDROID_HOME:-$HOME/Library/Android/sdk}/platform-tools/adb}"
if [ ! -x "$adb_bin" ]; then
    adb_bin="$(command -v adb || true)"
fi
[ -n "$adb_bin" ] && [ -x "$adb_bin" ] || { echo 'ADB not found. Set ADB to its executable path.' >&2; exit 1; }
devices="$("$adb_bin" devices)"
connected="$(printf '%s\n' "$devices" | awk '$2 == "device" { print $1 }')"
if [ -z "$serial" ]; then
    count="$(printf '%s\n' "$connected" | awk 'NF { n++ } END { print n+0 }')"
    [ "$count" -eq 1 ] || { echo 'Connect one device or select it with --serial.' >&2; exit 1; }
    serial="$connected"
else
    printf '%s\n' "$connected" | awk -v chosen="$serial" '$0 == chosen { found=1 } END { exit !found }' || {
        echo 'Selected device is not connected and authorized.' >&2; exit 1;
    }
fi
adb_device() { "$adb_bin" -s "$serial" "$@"; }
sdk="$(adb_device shell getprop ro.build.version.sdk | tr -d '\r')"
[[ "$sdk" =~ ^[0-9]+$ ]] && [ "$sdk" -ge 29 ] || { echo 'Benchmark tracing requires Android 10 or later.' >&2; exit 1; }
adb_device shell perfetto --help > /dev/null
if [ -z "$output" ]; then
    output="$(mktemp -d "${TMPDIR:-/tmp}/sts2-benchmark-trace.XXXXXX")"
else
    # Refuse to overwrite a previous recording.
    mkdir "$output"
fi
remote="/data/misc/perfetto-traces/sts2-benchmark-$(date +%Y%m%d-%H%M%S)-$$.perfetto-trace"
cleanup() { adb_device shell rm -f "$remote" > /dev/null 2>&1 || true; }
trap cleanup EXIT
cat > "$output/config.pbtxt" <<CFG
buffers { size_kb: 32768 fill_policy: RING_BUFFER }
duration_ms: $((seconds * 1000))
write_into_file: true
file_write_period_ms: 1000
max_file_size_bytes: 134217728
data_sources {
  config {
    name: "linux.ftrace"
    ftrace_config {
      ftrace_events: "sched/sched_switch"
      ftrace_events: "sched/sched_waking"
      ftrace_events: "sched/sched_process_exit"
      ftrace_events: "sched/sched_process_free"
      ftrace_events: "task/task_newtask"
      ftrace_events: "task/task_rename"
      ftrace_events: "power/cpu_frequency"
      ftrace_events: "power/cpu_idle"
      atrace_categories: "gfx"
      atrace_categories: "view"
      atrace_categories: "sync"
      atrace_categories: "dalvik"
      atrace_apps: "com.game.sts2launcher"
    }
  }
}
data_sources {
  config {
    name: "linux.process_stats"
    process_stats_config { scan_all_processes_on_start: true }
  }
}
data_sources { config { name: "android.surfaceflinger.frametimeline" } }
data_sources { config { name: "gpu.renderstages" } }
CFG
{
    printf 'Capture started (UTC): %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    printf 'Serial: %s\nAndroid API: %s\n' "$serial" "$sdk"
    adb_device shell getprop ro.product.model
    adb_device shell getprop ro.product.cpu.abi
    adb_device shell dumpsys package com.game.sts2launcher | awk '/versionName=|versionCode=/ { print }'
} > "$output/device.txt"
echo "Recording ${seconds}s. Start the existing benchmark now and leave the phone untouched."
echo "Output: $output"
if ! adb_device shell perfetto --txt -c - -o "$remote" < "$output/config.pbtxt" > "$output/perfetto.log" 2>&1; then
    cat "$output/perfetto.log" >&2
    echo 'Recording failed; diagnostic files preserved in the output directory.' >&2
    exit 1
fi
adb_device pull "$remote" "$output/benchmark.perfetto-trace"
[ -s "$output/benchmark.perfetto-trace" ] || { echo 'Trace is empty or missing.' >&2; exit 1; }
echo "Saved: $output/benchmark.perfetto-trace"
echo 'Open it in https://ui.perfetto.dev and find STS2Bench slices.'
