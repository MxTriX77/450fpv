# Links the main checkout's local store, vendor\ (D-089), into the checkout or worktree that holds this script,
# as a directory junction. Idempotent: an existing junction to the store is left as it is. Needs no admin rights
# and raises no dialog.
#
#   pwsh tools/vendor/link.ps1
$ErrorActionPreference = 'Stop'

function Get-FullPath([string] $path) { [IO.Path]::GetFullPath($path).TrimEnd('\') }

$top = Get-FullPath (git -C $PSScriptRoot rev-parse --show-toplevel)
$main = Get-FullPath (Split-Path (git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir) -Parent)
$store = Join-Path $main 'vendor'
$link = Join-Path $top 'vendor'

if (-not (Test-Path -LiteralPath $store -PathType Container)) {
    throw "No local store at $store. Put the vendor packs there first (docs/final-world-plan.md, W0)."
}
if ($top -eq $main) {
    Write-Output "main checkout: the store is $store"
    exit 0
}
$item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
if ($item) {
    if ($item.LinkType -eq 'Junction' -and (Get-FullPath $item.Target) -eq $store) {
        Write-Output "already linked: $link -> $store"
        exit 0
    }
    throw "$link exists and is not a junction to $store; move it away first."
}
New-Item -ItemType Junction -Path $link -Target $store | Out-Null
Write-Output "linked: $link -> $store"
