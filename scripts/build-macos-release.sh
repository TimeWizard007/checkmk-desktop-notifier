#!/usr/bin/env bash
set -euo pipefail

# Publish and wrap Checkmk Desktop Notifier.app for one macOS RID.
# Usage: scripts/build-macos-release.sh osx-x64|osx-arm64
# Does not create a DMG (see scripts/create-macos-dmg.sh; requires macOS hdiutil).

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
cd "$root"
version="$(python3 - <<'PY'
import re, pathlib
text = pathlib.Path("Directory.Build.props").read_text(encoding="utf-8")
match = re.search(r"<Version>([^<]+)</Version>", text)
if not match:
    raise SystemExit("Version not found in Directory.Build.props")
print(match.group(1).strip())
PY
)"
publish="$root/publish/macos-${zip_arch}"
app="$publish/Checkmk Desktop Notifier.app"

dotnet publish "$root/src/CheckmkDesktopNotifier.App.MacOS/CheckmkDesktopNotifier.App.MacOS.csproj" \
  -c Release \
  -r "$rid" \
  --self-contained true \
  -o "$publish"

"$root/scripts/package-macos-app.sh" "$publish" "$app" "$version"
find "$app" -name '*.pdb' -delete

echo "Packaged $app"
echo "Next (on macOS): scripts/create-macos-dmg.sh \"$app\" \"$root/artifacts/CheckmkDesktopNotifier-macOS-${zip_arch}-v${version}.dmg\""
