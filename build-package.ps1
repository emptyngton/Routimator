# build-package.ps1
# Packages Routimator into a VaM .var file (zip + meta.json) and drops it into
# AddonPackages/ so VaM can load it immediately. Skips opening VAMPM manually.
#
# Version strategy: scans AddonPackages/ for the highest existing
# Voxta.Routimator.<N>.var and proposes N+1. Override with -Version, or press
# Enter at the prompt to accept the suggestion.
#
# Usage:
#   .\build-package.ps1              # interactive: prompts for version
#   .\build-package.ps1 -Version 5   # non-interactive: pins to v5

param(
    [int]$Version = 0   # 0 = auto-scan + prompt
)

$ErrorActionPreference = "Stop"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReleaseDir = Join-Path (Split-Path -Parent $RepoRoot) "Routimator"
$MetaTemplate = Join-Path $RepoRoot "package\meta.template.json"

# VaM root is 4 levels above the dev folder:
#   .../Virt-a-Mate-ES/Custom/Scripts/Lapiro/Project_Routimator/
$VamRoot = (Resolve-Path (Join-Path $RepoRoot "..\..\..\..")).Path
$AddonPackages = Join-Path $VamRoot "AddonPackages"

$PackageName = "Voxta.Routimator"

if (-not (Test-Path $MetaTemplate)) {
    throw "Meta template missing: $MetaTemplate"
}
if (-not (Test-Path $AddonPackages)) {
    throw "AddonPackages folder not found: $AddonPackages"
}

# Always rebuild the release folder from HEAD so the package reflects committed state.
Write-Host "Rebuilding release folder from HEAD..." -ForegroundColor DarkGray
& (Join-Path $RepoRoot "build-release.ps1") -Quiet
if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
    throw "build-release.ps1 failed with exit code $LASTEXITCODE"
}

# --- Resolve version ---
$existingVersions = @()
Get-ChildItem -Path $AddonPackages -Filter "$PackageName.*.var" -ErrorAction SilentlyContinue |
    ForEach-Object {
        if ($_.Name -match "^$([regex]::Escape($PackageName))\.(\d+)\.var$") {
            $existingVersions += [int]$matches[1]
        }
    }

$maxVersion = if ($existingVersions.Count -gt 0) { ($existingVersions | Measure-Object -Maximum).Maximum } else { 0 }
$suggestedVersion = $maxVersion + 1

if ($Version -gt 0) {
    # Explicit version from CLI.
    $resolvedVersion = $Version
    Write-Host "Using explicit version v$resolvedVersion." -ForegroundColor Cyan
}
else {
    if ($maxVersion -eq 0) {
        Write-Host "No existing $PackageName.*.var found. Proposing v$suggestedVersion." -ForegroundColor Cyan
    } else {
        Write-Host "Found existing: v$maxVersion. Suggested next: v$suggestedVersion." -ForegroundColor Cyan
    }
    $input = Read-Host "Press Enter to use v$suggestedVersion, or type a different number"
    if ($input -match '^\s*\d+\s*$') {
        $resolvedVersion = [int]($input.Trim())
    } else {
        $resolvedVersion = $suggestedVersion
    }
}

$varName = "$PackageName.$resolvedVersion.var"
$varPath = Join-Path $AddonPackages $varName

if (Test-Path $varPath) {
    $confirm = Read-Host "$varName already exists. Overwrite? [y/N]"
    if ($confirm -notmatch '^[Yy]') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 1
    }
    Remove-Item $varPath -Force
}

# --- Stage content + meta ---
$StageDir = Join-Path $env:TEMP "routimator-package-$PID"
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

Copy-Item -Path $MetaTemplate -Destination (Join-Path $StageDir "meta.json")

$ContentTarget = Join-Path $StageDir "Custom\Scripts\Lapiro\Routimator"
New-Item -ItemType Directory -Path $ContentTarget -Force | Out-Null
Copy-Item -Path (Join-Path $ReleaseDir "*") -Destination $ContentTarget -Recurse

# --- Zip to .var ---
$TempZip = Join-Path $env:TEMP "routimator-package-$PID.zip"
if (Test-Path $TempZip) { Remove-Item $TempZip }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Build entries manually so paths use forward slashes (ZIP spec, and what VaM expects —
# CreateFromDirectory on Windows writes backslashes which some readers, including VaM,
# may not tolerate).
$zipStream = [System.IO.File]::Create($TempZip)
$zip = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $StageDir -Recurse -File | ForEach-Object {
        $relPath = $_.FullName.Substring($StageDir.Length).TrimStart('\', '/').Replace('\', '/')
        $entry = $zip.CreateEntry($relPath, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        $fileStream = [System.IO.File]::OpenRead($_.FullName)
        try { $fileStream.CopyTo($entryStream) }
        finally {
            $fileStream.Dispose()
            $entryStream.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
    $zipStream.Dispose()
}

Move-Item -Path $TempZip -Destination $varPath
Remove-Item $StageDir -Recurse -Force

$stopwatch.Stop()

# --- Summary ---
$pkgSizeKb = [math]::Round((Get-Item $varPath).Length / 1KB, 1)

Write-Host ""
Write-Host "===== Package built =====" -ForegroundColor Green
Write-Host ("  Version  : v{0}" -f $resolvedVersion)
Write-Host ("  Location : {0}" -f $varPath)
Write-Host ("  Size     : {0} KB" -f $pkgSizeKb)
Write-Host ("  Elapsed  : {0} ms" -f $stopwatch.ElapsedMilliseconds)
Write-Host ""

if (-not $PSBoundParameters.ContainsKey('Version')) {
    Read-Host "Press Enter to close" | Out-Null
}
