# Links the main checkout's local store, vendor\ (D-089), into the checkout or worktree that holds this script:
# - vendor\ itself, as a directory junction (worktrees only: the main checkout holds the store);
# - the farmland world's local-only parts into unity\Assets, each at the path it has in the friend's project: a folder
#   as a junction, a file as a hard link, each with its .meta (w1-farmland-world design.md). .gitignore lists them all,
#   and the script refuses to link a path git would not ignore.
# Idempotent: links already in place are left as they are. Needs no admin rights and raises no dialog.
#
#   pwsh tools/vendor/link.ps1            link
#   pwsh tools/vendor/link.ps1 -Unlink    remove every link, never the store's files; run it before `git worktree remove`
param([switch] $Unlink)
$ErrorActionPreference = 'Stop'

# The world's local-only parts, as paths in the friend's project (wildcards allowed): the third-party packs, and the
# friend's own binaries (the terrain tiles' data, the generated ground textures, the reduced tree and ruin models).
$WorldProject = 'world_final\Farming_land'
$WorldLinks = @(
    'Assets\Art'                          # Book of the Dead: Environment (the pines and bushes)
    'Assets\TerrainSampleAssets'          # Terrain Sample Asset Pack (the grass and small bushes)
    'Assets\ALP_Assets'                   # Country House FREE (the village's house)
    'Assets\Farm_land\Materials'          # the Poly Haven ground textures and island tree
    'Assets\Farm_land\Terrain\Generated'  # the ground textures FarmLandBuilder derives from them
    'Assets\Farm_land\Terrain\*.asset'    # the TerrainData of the map, the surround and the far ring
    'Assets\Farm_land\Models\*.fbx'       # the reduced island tree, and the ruins cut from the pack's house
    'Assets\Farm_land\Models\*.png'       # the island tree's leaf and impostor atlases
)

function Get-FullPath([string] $path) { [IO.Path]::GetFullPath($path).TrimEnd('\') }

$top = Get-FullPath (git -C $PSScriptRoot rev-parse --show-toplevel)
$main = Get-FullPath (Split-Path (git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir) -Parent)
$store = Join-Path $main 'vendor'
$link = Join-Path $top 'vendor'
$world = Join-Path $store $WorldProject
$unity = Join-Path $top 'unity'

if (-not (Test-Path -LiteralPath $store -PathType Container)) {
    throw "No local store at $store. Put the vendor packs there first (docs/final-world-plan.md, W0)."
}

# Each local-only file or folder in the store, then its .meta.
function Get-WorldSources {
    foreach ($pattern in $WorldLinks) {
        $items = @(Get-Item -Path (Join-Path $world $pattern) -Force)
        if ($items.Count -eq 0) { throw "Nothing in the store matches $pattern." }
        foreach ($item in $items) {
            $item
            Get-Item -LiteralPath "$($item.FullName).meta" -Force
        }
    }
}

function Get-Target($source) { Join-Path $unity $source.FullName.Substring($world.Length + 1) }

function Test-Linked($source, $existing) {
    if ($source.PSIsContainer) {
        return $existing.LinkType -eq 'Junction' -and (Get-FullPath $existing.Target) -eq $source.FullName
    }
    # A hard link is the same file record, so it shares the store file's size and write time.
    return $existing.LinkType -eq 'HardLink' -and $existing.Length -eq $source.Length -and
        $existing.LastWriteTimeUtc -eq $source.LastWriteTimeUtc
}

if ($Unlink) {
    $removed = 0
    foreach ($source in Get-WorldSources) {
        $target = Get-Target $source
        $existing = Get-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        if (-not $existing) { continue }
        if (-not (Test-Linked $source $existing)) { Write-Warning "left $target in place: not a link to the store"; continue }
        # Directory.Delete without recursion removes the junction itself and never follows it into the store.
        if ($source.PSIsContainer) { [IO.Directory]::Delete($target, $false) } else { Remove-Item -LiteralPath $target -Force }
        $removed++
    }
    Write-Output "unlinked $removed world paths from $unity"
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if ($top -ne $main -and $item -and $item.LinkType -eq 'Junction') {
        [IO.Directory]::Delete($link, $false)
        Write-Output "unlinked: $link"
    }
    exit 0
}

if ($top -eq $main) {
    Write-Output "main checkout: the store is $store"
} else {
    $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
    if (-not $item) {
        New-Item -ItemType Junction -Path $link -Target $store | Out-Null
        Write-Output "linked: $link -> $store"
    } elseif ($item.LinkType -eq 'Junction' -and (Get-FullPath $item.Target) -eq $store) {
        Write-Output "already linked: $link -> $store"
    } else {
        throw "$link exists and is not a junction to $store; move it away first."
    }
}

$linked = 0; $kept = 0
foreach ($source in Get-WorldSources) {
    $target = Get-Target $source
    $relative = $target.Substring($top.Length + 1).Replace('\', '/')
    git -C $top check-ignore -q -- $relative
    if ($LASTEXITCODE -ne 0) { throw "$relative is not ignored by .gitignore; list it there before linking it (D-089)." }
    $existing = Get-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
    if ($existing) {
        if (-not (Test-Linked $source $existing)) {
            throw "$target exists and is not a link to $($source.FullName); run link.ps1 -Unlink, move it away, then link again."
        }
        $kept++
        continue
    }
    $type = if ($source.PSIsContainer) { 'Junction' } else { 'HardLink' }
    New-Item -ItemType $type -Path $target -Target $source.FullName | Out-Null
    $linked++
}
Write-Output "world: linked $linked, already linked $kept, into $unity"
