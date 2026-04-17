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
# Invoked by hooks/pre-push, or run manually: .\build-release.ps1

$ErrorActionPreference = "Stop"

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

Write-Host "Built release: $OutDir" -ForegroundColor Green
