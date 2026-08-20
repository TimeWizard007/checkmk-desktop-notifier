#!/usr/bin/env bash
set -euo pipefail

# Generate CheckmkDesktopNotifier.icns from the canonical Windows app.ico.
# Usage: scripts/generate-macos-icon.sh --icns <path> [--iconset <dir>]

root=$(cd "$(dirname "$0")/.." && pwd)
exec python3 "$root/scripts/generate-macos-icon.py" "$@"
