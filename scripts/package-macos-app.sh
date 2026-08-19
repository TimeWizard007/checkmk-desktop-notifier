#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <publish-directory> <app-path> [version]" >&2
  exit 1
fi

src=$1
app=$2
root=$(cd "$(dirname "$0")/.." && pwd)
if [[ $# -ge 3 ]]; then
  version=$3
else
  version="$(python3 - <<PY
import re, pathlib
text = pathlib.Path("$root/Directory.Build.props").read_text(encoding="utf-8")
match = re.search(r"<Version>([^<]+)</Version>", text)
if not match:
    raise SystemExit("Version not found in Directory.Build.props")
print(match.group(1).strip())
PY
)"
fi
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
python3 - "$contents/Info.plist" "$version" <<'PY'
import re, sys, pathlib
path = pathlib.Path(sys.argv[1])
version = sys.argv[2]
text = path.read_text(encoding="utf-8")
for key in ("CFBundleShortVersionString", "CFBundleVersion"):
    text, n = re.subn(
        rf"(<key>{key}</key>\s*<string>)[^<]+(</string>)",
        rf"\g<1>{version}\g<2>",
        text,
        count=1,
    )
    if n != 1:
        raise SystemExit(f"Failed to write {key}={version} into Info.plist")
path.write_text(text, encoding="utf-8")
PY
find "$macos" -name '*.pdb' -delete
executable="$macos/CheckmkDesktopNotifier.MacOS"
if [[ -f "$executable" ]]; then
  chmod +x "$executable"
fi

echo "Packaged $app (version $version)"
