#!/usr/bin/env bash
set -euo pipefail

# Create a UDZO DMG with Checkmk Desktop Notifier.app and an Applications symlink.
# Requires macOS hdiutil. Do not substitute a Linux-generated disk image.
# Usage: scripts/create-macos-dmg.sh <app-path> <output-dmg>

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <Checkmk Desktop Notifier.app> <output.dmg>" >&2
  exit 1
fi

app=$1
out=$2

if [[ ! -d "$app" ]]; then
  echo "App bundle not found: $app" >&2
  exit 1
fi

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "hdiutil is required. This host is not macOS; refusing to fake a DMG." >&2
  echo "On the Intel Mac, after publishing the .app:" >&2
  echo "  scripts/create-macos-dmg.sh \"$app\" \"$out\"" >&2
  exit 1
fi

if ! command -v hdiutil >/dev/null 2>&1; then
  echo "hdiutil not found." >&2
  exit 1
fi

stage=$(mktemp -d "${TMPDIR:-/tmp}/cdn-dmg.XXXXXX")
cleanup() { rm -rf "$stage"; }
trap cleanup EXIT

mkdir -p "$stage"
cp -R "$app" "$stage/Checkmk Desktop Notifier.app"
ln -s /Applications "$stage/Applications"

mkdir -p "$(dirname "$out")"
rm -f "$out"
hdiutil create \
  -volname "Checkmk Desktop Notifier" \
  -srcfolder "$stage" \
  -ov \
  -format UDZO \
  "$out"

echo "Created $out"
