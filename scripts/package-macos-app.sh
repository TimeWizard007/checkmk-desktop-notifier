#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <publish-directory> <app-path> [version]" >&2
  exit 1
fi

src=$1
app=$2
version=${3:-1.3.0-beta.1}
root=$(cd "$(dirname "$0")/.." && pwd)
plist_src="$root/src/CheckmkDesktopNotifier.App.MacOS/Bundle/Info.plist"

if [[ ! -d "$src" ]]; then
  echo "Publish directory not found: $src" >&2
  exit 1
fi

if [[ ! -f "$plist_src" ]]; then
  echo "Info.plist template not found: $plist_src" >&2
  exit 1
fi

contents="$app/Contents"
macos="$contents/MacOS"
resources="$contents/Resources"

rm -rf "$app"
mkdir -p "$macos" "$resources"

shopt -s dotglob nullglob
for item in "$src"/*; do
  name=$(basename "$item")
  if [[ "$name" == *.app || "$name" == *.pdb ]]; then
    continue
  fi
  cp -a "$item" "$macos/"
done

cp "$plist_src" "$contents/Info.plist"
find "$macos" -name '*.pdb' -delete
executable="$macos/CheckmkDesktopNotifier.MacOS"
if [[ -f "$executable" ]]; then
  chmod +x "$executable"
fi

echo "Packaged $app (version $version)"
