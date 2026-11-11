<#
.SYNOPSIS
One vendor piece end to end (w4b-farmland-ruins, D-089): params -> physics metadata -> review sheet.

.DESCRIPTION
    pwsh tools/vendor_asset.ps1 <ID|variant|all> [-Audit]

The vendor pieces are meshes of the local store (vendor/, linked by tools/vendor/link.ps1) that the map places but
the pipeline never exports. Step 1 runs tools/blender/vendor_build.py, which reads each piece's FBX as Unity imports
it and writes unity/Assets/_Project/Art/Models/<class>/<ID>/<variant>.assetmeta.json (with -Audit, the collision
audit's lines too). Step 2 renders the review tiles in headless Blender (tools/blender/vendor_review.py) and step 3
composes review-sheets/<ID>/<variant>.png (git-ignored). Blender comes from $env:BLENDER, falling back to the path
pinned in docs/environment.md; python is the pinned one on PATH. Stops at the first failing step and exits 1.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Id,
    [switch]$Audit
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$root = Split-Path -Parent $PSScriptRoot
$blender = if ($env:BLENDER) { $env:BLENDER } else { 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' }
if (-not (Test-Path -LiteralPath $blender)) {
    Write-Host "vendor_asset: not found: $blender (set `$env:BLENDER, see docs/environment.md)"
    exit 1
}

Write-Host "vendor_asset: [1/3] physics metadata of $Id"
$buildArgs = @((Join-Path $root 'tools\blender\vendor_build.py'), $Id)
if ($Audit) { $buildArgs += '--audit' }
& python -X utf8 @buildArgs
if ($LASTEXITCODE -ne 0) { Write-Host "vendor_asset: FAILED at step 1 (metadata), exit code $LASTEXITCODE"; exit 1 }

Write-Host "vendor_asset: [2/3] review tiles of $Id (Blender)"
& $blender --background --factory-startup --python-exit-code 1 --python (Join-Path $root 'tools\blender\vendor_review.py') -- $Id |
    Where-Object { $_ -match 'tiles in|Error|Traceback' } | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Host "vendor_asset: FAILED at step 2 (review tiles), exit code $LASTEXITCODE"; exit 1 }

Write-Host "vendor_asset: [3/3] review sheet of $Id"
& python -X utf8 (Join-Path $root 'tools\blender\vendor_review.py') --compose $Id
if ($LASTEXITCODE -ne 0) { Write-Host "vendor_asset: FAILED at step 3 (review sheet), exit code $LASTEXITCODE"; exit 1 }
Write-Host "vendor_asset: OK ($Id)"
exit 0
