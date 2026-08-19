#!/usr/bin/env bash
set -euo pipefail

# Publish, wrap a .app, and zip one macOS RID for the v1.3.0-beta.1 tester artifacts.
# Usage: scripts/build-macos-beta.sh osx-x64|osx-arm64

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 osx-x64|osx-arm64" >&2
  exit 1
fi

rid=$1
case "$rid" in
  osx-x64) zip_arch=x64 ;;
  osx-arm64) zip_arch=arm64 ;;
  *)
    echo "Unsupported RID: $rid" >&2
    exit 1
    ;;
esac

root=$(cd "$(dirname "$0")/.." && pwd)
version=1.3.0-beta.1
publish="$root/publish/macos-${zip_arch}"
app="$publish/Checkmk Desktop Notifier.app"
artifacts="$root/artifacts"
zip_name="CheckmkDesktopNotifier-macOS-${zip_arch}-v${version}.zip"

mkdir -p "$artifacts"

dotnet publish "$root/src/CheckmkDesktopNotifier.App.MacOS/CheckmkDesktopNotifier.App.MacOS.csproj" \
  -c Release \
  -r "$rid" \
  --self-contained true \
  -o "$publish"

"$root/scripts/package-macos-app.sh" "$publish" "$app" "$version"
find "$app" -name '*.pdb' -delete

(
  cd "$publish"
  rm -f "$artifacts/$zip_name"
  zip -r -y "$artifacts/$zip_name" "Checkmk Desktop Notifier.app"
)

echo "Created $artifacts/$zip_name"
