#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CHECK_DIR="$(mktemp -d)"
trap 'rm -rf "$CHECK_DIR"' EXIT
if [ ! -x "${GODOT_BIN:-}" ]; then
    echo "Set GODOT_BIN to a Godot 4.5 executable for the bootstrap startup check" >&2
    exit 1
fi
python3 "$ROOT/scripts/make-bootstrap-pck.py" --output "$CHECK_DIR/bootstrap.pck"
"$GODOT_BIN" --headless --path "$CHECK_DIR" --main-pack "$CHECK_DIR/bootstrap.pck" --quit
