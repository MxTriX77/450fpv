<#
.SYNOPSIS
One asset end to end: params -> Blender build -> Unity import -> review sheet (setup-asset-pipeline, design
decision 9; setup-review-capture, design decision 7).

.DESCRIPTION
    pwsh tools/build_asset.ps1 <ID|all>

Step 1 runs tools/blender/build.py in headless Blender (FBX plus .assetmeta.json). Step 2 runs the catalog
importer in Unity batchmode (materials, prefab, colliders, metadata component). Step 3 renders the AST-12 review
sheet in Unity batchmode with graphics (review-sheets/<ID>/). Blender and Unity come from
$env:BLENDER and $env:UNITY, falling back to the paths pinned in docs/environment.md. The script refuses to run
while an editor holds the project open, stops at the first failing step and exits 1, naming the step.
The full Unity logs go to unity/Logs/build_asset_<ID>.log and unity/Logs/review_<ID>.log; only the importer's
and the capture's lines and errors are echoed.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Id
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'unity'
$blender = if ($env:BLENDER) { $env:BLENDER } else { 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' }
$unity = if ($env:UNITY) { $env:UNITY } else { 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe' }

foreach ($tool in @($blender, $unity)) {
    if (-not (Test-Path -LiteralPath $tool)) {
        Write-Host "build_asset: not found: $tool (set `$env:BLENDER / `$env:UNITY, see docs/environment.md)"
        exit 1
    }
}
if (Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')) {
    Write-Host "build_asset: a Unity editor holds $project open (Temp\UnityLockfile); close it and rerun"
    exit 1
}

Write-Host "build_asset: [1/3] Blender build of $Id"
& $blender --background --factory-startup --python (Join-Path $root 'tools\blender\build.py') -- $Id
if ($LASTEXITCODE -ne 0) {
    Write-Host "build_asset: FAILED at step 1 (Blender build), exit code $LASTEXITCODE"
    exit 1
}

$logDir = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "build_asset_$Id.log"
Write-Host "build_asset: [2/3] Unity import of $Id (full log: $log)"
& $unity -batchmode -nographics -quit -projectPath $project `
    -executeMethod Fpv450.Assets.CatalogImporter.ImportFromCommandLine -asset $Id -logFile - |
    Tee-Object -FilePath $log |
    Where-Object { $_ -match '^\[CatalogImporter\]|error CS\d+|^[\w.]+Exception: |^Aborting batchmode' } |
    Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "build_asset: FAILED at step 2 (Unity import), exit code $LASTEXITCODE; see $log"
    exit 1
}

$reviewLog = Join-Path $logDir "review_$Id.log"
Write-Host "build_asset: [3/3] Review sheet of $Id (full log: $reviewLog)"
& $unity -batchmode -quit -projectPath $project `
    -executeMethod Fpv450.Review.Capture -asset $Id -logFile - |
    Tee-Object -FilePath $reviewLog |
    Where-Object { $_ -match '^\[Review\]|error CS\d+|^[\w.]+Exception: |^Aborting batchmode' } |
    Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "build_asset: FAILED at step 3 (review sheet), exit code $LASTEXITCODE; see $reviewLog"
    exit 1
}
Write-Host "build_asset: OK ($Id)"
exit 0
