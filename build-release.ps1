# build-release.ps1
# Produces a clean Routimator/ folder SIBLING to this dev folder, containing only the
# files VaM needs:
#   - src/          (all plugin source)
#   - Routimator.cslist
#   - LICENSE
# VAMPM "Add Directory" on the sibling folder packages only runtime files; the path it
# embeds (Custom/Scripts/Lapiro/Routimator/) is where end users will see the plugin.
#
# Uses `git archive HEAD` so the output reflects committed state, not working-tree state.
# Invoked by hooks/pre-push with -Quiet, or run manually: .\build-release.ps1

param(
    # Set by the pre-push hook to skip the summary + pause (push would hang on Read-Host).
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
# Output sibling to repo: .../Lapiro/Project_Routimator/ -> .../Lapiro/Routimator/
$OutDir = Join-Path (Split-Path -Parent $RepoRoot) "Routimator"
$TempZip = Join-Path $env:TEMP "routimator-release-$PID.zip"

if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

Push-Location $RepoRoot
try {
    # Archive tracked paths from HEAD to a zip. Avoids PowerShell's text-mode pipe
    # (which corrupts binary tar output) by writing to a file first.
    git archive --format=zip -o $TempZip HEAD src Routimator.cslist LICENSE
    if ($LASTEXITCODE -ne 0) {
        throw "git archive failed with exit code $LASTEXITCODE"
    }
    Expand-Archive -Path $TempZip -DestinationPath $OutDir -Force
}
finally {
    if (Test-Path $TempZip) { Remove-Item $TempZip }
    Pop-Location
}

$stopwatch.Stop()

if ($Quiet) {
    # Hook context: one line for the push log, no pause.
    Write-Host "Built release: $OutDir" -ForegroundColor Green
}
else {
    # Manual run: show a summary and wait for the user so the window doesn't vanish.
    $files = @(Get-ChildItem -Path $OutDir -Recurse -File)
    $totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
    $sizeKb = [math]::Round($totalBytes / 1KB, 1)

    Write-Host ""
    Write-Host "===== Release built =====" -ForegroundColor Cyan
    Write-Host ("  Location : {0}" -f $OutDir)
    Write-Host ("  Files    : {0}" -f $files.Count)
    Write-Host ("  Size     : {0} KB" -f $sizeKb)
    Write-Host ("  Elapsed  : {0} ms" -f $stopwatch.ElapsedMilliseconds)
    Write-Host ""
    Write-Host "Files:" -ForegroundColor DarkGray
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($OutDir.Length).TrimStart('\')
        Write-Host ("  {0}" -f $rel) -ForegroundColor DarkGray
    }
    Write-Host ""
    Read-Host "Press Enter to close" | Out-Null
}
