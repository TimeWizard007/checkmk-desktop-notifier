#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
dotnet publish src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o publish/win-x64
echo "Published to $root/publish/win-x64"
