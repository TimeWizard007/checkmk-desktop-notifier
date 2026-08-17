#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Get-AppVersion {
    $props = Get-Content -Raw -Path (Join-Path $root "Directory.Build.props")
    $match = [regex]::Match($props, "<Version>([^<]+)</Version>")
    if (-not $match.Success) {
        throw "Version not found in Directory.Build.props"
    }
    return $match.Groups[1].Value.Trim()
}

$version = Get-AppVersion
Write-Host "Version: $version"

dotnet publish (Join-Path $root "src/CheckmkDesktopNotifier.App/CheckmkDesktopNotifier.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o (Join-Path $root "publish/win-x64")

$artifacts = Join-Path $root "artifacts"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    $defaultIscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
    if (Test-Path $defaultIscc) {
        $iscc = Get-Command $defaultIscc
    }
}

if (-not $iscc) {
    Write-Host "Inno Setup 6 compiler not found. Install it, then run:"
    Write-Host "  iscc /DMyAppVersion=$version installer\CheckmkDesktopNotifier.iss"
    exit 1
}

& $iscc.Source "/DMyAppVersion=$version" (Join-Path $root "installer\CheckmkDesktopNotifier.iss")
Write-Host "Installer: $(Join-Path $artifacts 'CheckmkDesktopNotifier-Setup-x64.exe')"
