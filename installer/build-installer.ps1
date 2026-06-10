<#
.SYNOPSIS
  Publishes TNT and builds the Windows installer with Inno Setup.

.DESCRIPTION
  1. Publishes a framework-dependent single-folder build to publish\win-x64.
  2. Optionally copies iperf3.exe into the publish tools folder so it is bundled.
  3. Runs Inno Setup (iscc) to produce installer\Output\TNT-Setup-<version>.exe.

.PARAMETER Version
  Version stamped into the installer (default 2.0.0).

.PARAMETER Iperf3Path
  Optional path to iperf3.exe to bundle.

.PARAMETER IncludeNpcap
  If set, bundles installer\redist\npcap-installer.exe and offers to install it.

.EXAMPLE
  pwsh installer\build-installer.ps1 -Version 2.0.0 -Iperf3Path C:\tools\iperf3.exe
#>
param(
    [string]$Version = "2.0.0",
    [string]$Iperf3Path = "",
    [switch]$IncludeNpcap
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish\win-x64"

Write-Host "Publishing TNT $Version ..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot "NetConfigTray\NetConfigTray.csproj") `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:Version=$Version `
    -o $publishDir

if ($Iperf3Path -and (Test-Path $Iperf3Path)) {
    $toolsDir = Join-Path $publishDir "tools\iperf3"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    Copy-Item $Iperf3Path (Join-Path $toolsDir "iperf3.exe") -Force
    Write-Host "Bundled iperf3.exe" -ForegroundColor Green
}

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
}

$defs = @("/DAppVersion=$Version")
if ($IncludeNpcap) { $defs += "/DIncludeNpcap" }

Write-Host "Building installer ..." -ForegroundColor Cyan
& $iscc @defs (Join-Path $PSScriptRoot "TNT.iss")
Write-Host "Done. See installer\Output." -ForegroundColor Green
