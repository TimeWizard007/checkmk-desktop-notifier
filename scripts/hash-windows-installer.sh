#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
version="$(python3 - <<PY
import re, pathlib
text = pathlib.Path("$root/Directory.Build.props").read_text(encoding="utf-8")
match = re.search(r"<Version>([^<]+)</Version>", text)
if not match:
    raise SystemExit("Version not found in Directory.Build.props")
print(match.group(1).strip())
PY
)"
installer="$root/artifacts/CheckmkDesktopNotifier-Setup-x64-v${version}.exe"
if [[ ! -f "$installer" ]]; then
  echo "Installer not found: $installer" >&2
  echo "Build it on Windows with: powershell -File scripts/build-windows-package.ps1" >&2
  exit 1
fi
if command -v sha256sum >/dev/null 2>&1; then
  sha256sum "$installer"
elif command -v shasum >/dev/null 2>&1; then
  shasum -a 256 "$installer"
else
  echo "sha256sum/shasum not found." >&2
  exit 1
fi
