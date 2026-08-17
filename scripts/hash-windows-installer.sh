#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
installer="$root/artifacts/CheckmkDesktopNotifier-Setup-x64.exe"
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
