#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$props = Get-Content -Raw -Path (Join-Path $root "Directory.Build.props")
$match = [regex]::Match($props, "<Version>([^<]+)</Version>")
if (-not $match.Success) {
    throw "Version not found in Directory.Build.props"
}
$version = $match.Groups[1].Value.Trim()
$installer = Join-Path $root "artifacts\CheckmkDesktopNotifier-Setup-x64-v$version.exe"

if (-not (Test-Path $installer)) {
    Write-Error "Installer not found: $installer`nBuild it first with: powershell -File scripts/build-windows-package.ps1"
}

Get-FileHash -Path $installer -Algorithm SHA256 | Format-List Path, Algorithm, Hash
