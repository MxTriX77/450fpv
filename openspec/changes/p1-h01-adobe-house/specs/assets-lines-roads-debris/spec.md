# Spec Delta

## ADDED Requirements

### Requirement: K02 and K03 debris kit entries
The lines, roads and debris capability SHALL include the K02 tile pieces and the K03 timber pieces as built by `p1-h01-adobe-house` (see the `assets-houses-and-yard` delta of that change for the full requirement): seeded shard and splinter outlines as geometry, one box per piece, `masonry` or `timber`, `edge` and `hook` snags, composable into a host's rubble.

#### Scenario: Kit pieces listed in the catalog
- **WHEN** `assets-src/catalog.json` is read after the change
- **THEN** K02 lists `K02_tile_shard` and `K02_tile_whole` under `debris_tile`, K03 lists `K03_batten_broken`, `K03_board` and `K03_rafter_broken` under `debris_timber`, and each builds by `pwsh tools/build_asset.ps1 <ID>`
