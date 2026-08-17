#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $root "artifacts\CheckmkDesktopNotifier-Setup-x64.exe"

if (-not (Test-Path $installer)) {
    Write-Error "Installer not found: $installer`nBuild it first with: powershell -File scripts/build-windows-package.ps1"
}

Get-FileHash -Path $installer -Algorithm SHA256 | Format-List Path, Algorithm, Hash
