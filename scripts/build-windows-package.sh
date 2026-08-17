#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
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
echo "Version: $version"
bash "$root/scripts/publish-win-x64.sh"
mkdir -p "$root/artifacts"
if command -v iscc >/dev/null 2>&1; then
  iscc "/DMyAppVersion=$version" "$root/installer/CheckmkDesktopNotifier.iss"
  echo "Installer: $root/artifacts/CheckmkDesktopNotifier-Setup-x64.exe"
else
  echo "Inno Setup compiler (iscc) is not on PATH."
  echo "On Windows, after the publish above, run:"
  echo "  iscc /DMyAppVersion=$version installer\\CheckmkDesktopNotifier.iss"
  echo "Output: artifacts\\CheckmkDesktopNotifier-Setup-x64.exe"
fi
