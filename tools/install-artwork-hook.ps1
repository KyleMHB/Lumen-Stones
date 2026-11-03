param([Parameter(Mandatory = $true)][string]$Python)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pythonPath = (Get-Item -LiteralPath $Python).FullName.Replace('\', '/')
& $pythonPath -c 'from PIL import Image'
if ($LASTEXITCODE -ne 0) { throw 'Install tools/requirements-artwork.txt in this Python first.' }
$existingHooks = & git -C $repoRoot config --get core.hooksPath
if ($existingHooks -and $existingHooks -ne '.githooks') {
    throw "Existing hook configuration preserved: $existingHooks"
}
& git -C $repoRoot config --local lumen.artworkPython $pythonPath
if ($LASTEXITCODE -ne 0) { throw 'Could not save artwork Python configuration.' }
& git -C $repoRoot config --local core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) { throw 'Could not enable the artwork hook.' }
Write-Output 'Enabled pre-commit artwork checks against staged files.'
