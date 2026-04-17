# publish-hub.ps1
# Helper for the manual VaM Hub publish step. Can't upload for you (Hub has no API we can
# use safely), but shrinks the work to paste + drag + Save.
#
# Modes:
#   .\publish-hub.ps1           - Update flow: downloads latest .var from GitHub Release,
#                                 renders package/hub-update.template.bbc with this release's
#                                 changelog (converted from Markdown to BBCode), copies to
#                                 clipboard, opens Hub's add-version page and Explorer at the
#                                 downloaded .var.
#   .\publish-hub.ps1 -Main     - Main-page flow: copies package/hub-main.template.bbc to the
#                                 clipboard so you can paste it into Hub's resource description
#                                 editor.
#   .\publish-hub.ps1 -Tag vX   - Use a specific release tag instead of latest.

param(
    [switch]$Main,
    [string]$Tag
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$HubResourceUrl = "https://hub.virtamate.com/resources/routimator.55321"

# ---------------------------------------------------------------------------
# Mode: main page
# ---------------------------------------------------------------------------
if ($Main) {
    $MainTemplate = Join-Path $RepoRoot "package\hub-main.template.bbc"
    if (-not (Test-Path $MainTemplate)) { throw "Main template missing: $MainTemplate" }

    $content = Get-Content -Raw -Path $MainTemplate
    Set-Clipboard -Value $content

    Write-Host ""
    Write-Host "===== Hub main page BBCode copied to clipboard =====" -ForegroundColor Green
    Write-Host "  Paste into the resource description editor at:"
    Write-Host "  $HubResourceUrl/edit"
    Write-Host ""

    Start-Process "$HubResourceUrl/edit"
    Read-Host "Press Enter to close" | Out-Null
    exit 0
}

# ---------------------------------------------------------------------------
# Mode: update (default)
# ---------------------------------------------------------------------------

# Resolve release tag
if (-not $Tag) {
    $Tag = (& gh release list --limit 1 --json tagName --jq '.[0].tagName' 2>$null).Trim()
    if (-not $Tag) { throw "Could not find a release on GitHub. Pass -Tag vX.Y.Z manually." }
}

Write-Host "Using release: $Tag" -ForegroundColor DarkGray

# Read VaM package version
$VamVersionFile = Join-Path $RepoRoot "package\VAM_VERSION"
if (-not (Test-Path $VamVersionFile)) { throw "VAM_VERSION file missing." }
$VamVersion = (Get-Content -Raw $VamVersionFile).Trim()

# Download .var asset
$DistDir = Join-Path $RepoRoot "dist"
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir -Force | Out-Null }

Write-Host "Downloading .var from $Tag..." -ForegroundColor DarkGray
& gh release download $Tag --pattern "*.var" --dir $DistDir --clobber
if ($LASTEXITCODE -ne 0) { throw "gh release download failed." }

$varFile = Get-ChildItem -Path $DistDir -Filter "*.var" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $varFile) { throw "No .var found after download." }

# Fetch release body (Markdown)
Write-Host "Fetching release notes..." -ForegroundColor DarkGray
$releaseBody = & gh release view $Tag --json body --jq '.body'
if ($LASTEXITCODE -ne 0) { throw "gh release view failed." }
$releaseUrl = & gh release view $Tag --json url --jq '.url'

# -------- Markdown -> BBCode converter (scoped to what GitHub generates) --------
function Convert-MarkdownToBBCode {
    param([string]$md)

    # Split into lines, fold consecutive bullet lines into [LIST]...[/LIST].
    $lines = $md -split "`r?`n"
    $out = [System.Collections.Generic.List[string]]::new()
    $inList = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*[\*\-]\s+(.+)$') {
            if (-not $inList) { $out.Add('[LIST]'); $inList = $true }
            $out.Add("[*]$($matches[1])")
        }
        else {
            if ($inList) { $out.Add('[/LIST]'); $inList = $false }
            $out.Add($line)
        }
    }
    if ($inList) { $out.Add('[/LIST]') }

    $text = $out -join "`n"

    # Headings
    $text = [regex]::Replace($text, '(?m)^###\s+(.+)$', '[HEADING=3]$1[/HEADING]')
    $text = [regex]::Replace($text, '(?m)^##\s+(.+)$',  '[HEADING=2]$1[/HEADING]')
    $text = [regex]::Replace($text, '(?m)^#\s+(.+)$',   '[HEADING=1]$1[/HEADING]')

    # Inline: bold, code, links
    $text = [regex]::Replace($text, '\*\*(.+?)\*\*',    '[B]$1[/B]')
    $text = [regex]::Replace($text, '`([^`]+)`',        '[ICODE]$1[/ICODE]')
    $text = [regex]::Replace($text, '\[([^\]]+)\]\(([^)]+)\)', "[URL='`$2']`$1[/URL]")

    # Bare URLs on their own (rough pass): already handled by [URL] tags or left as-is.
    # Trim trailing horizontal-rules that Markdown uses but BBCode doesn't render.
    $text = [regex]::Replace($text, '(?m)^---+\s*$', '')

    return $text.Trim()
}

$changelogBB = Convert-MarkdownToBBCode $releaseBody

# Strip leading 'v' from the tag when it's a semver like v0.1.0 -> "v0.1.0" displayed as-is.
# Keep as-is; template uses {{SEMVER}} verbatim.

# Render template
$UpdateTemplate = Join-Path $RepoRoot "package\hub-update.template.bbc"
if (-not (Test-Path $UpdateTemplate)) { throw "Update template missing: $UpdateTemplate" }

$rendered = Get-Content -Raw -Path $UpdateTemplate
$rendered = $rendered.Replace('{{SEMVER}}', $Tag)
$rendered = $rendered.Replace('{{VAM_VERSION}}', $VamVersion)
$rendered = $rendered.Replace('{{CHANGELOG}}', $changelogBB)
$rendered = $rendered.Replace('{{RELEASE_URL}}', $releaseUrl)

Set-Clipboard -Value $rendered

# Summary
Write-Host ""
Write-Host "===== Hub update flow ready =====" -ForegroundColor Green
Write-Host ("  Release  : {0} (VaM package #{1})" -f $Tag, $VamVersion)
Write-Host ("  .var     : {0}" -f $varFile.FullName)
Write-Host "  BBCode   : copied to clipboard"
Write-Host ""
Write-Host "Opening Hub add-version page + Explorer at the .var..." -ForegroundColor DarkGray

Start-Process "$HubResourceUrl/add-version"
Start-Process "explorer.exe" -ArgumentList "/select,`"$($varFile.FullName)`""

Write-Host ""
Write-Host "On Hub's form:" -ForegroundColor Cyan
Write-Host "  1. Type version number (e.g. $VamVersion or $($Tag -replace '^v', ''))"
Write-Host "  2. Drag the highlighted .var from Explorer into File Attachments"
Write-Host "  3. Paste (Ctrl+V) into the Update Message editor"
Write-Host "  4. Save"
Write-Host ""
Read-Host "Press Enter to close" | Out-Null
