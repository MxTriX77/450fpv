# Design: setup-review-capture

## Context
The capture runs in batchmode **with** graphics (no `-nographics`), because it renders through URP on the GPU. Benchmarks need AC power; captures don't. Prefabs and `.assetmeta.json` files come from `setup-asset-pipeline`.

## Decisions
1. **Entry point:** `namespace Fpv450 { public static class Review { public static void Capture() } }` in the Editor assembly, reading `-asset <ID>` from the command line; exit code 0 on success, 1 on any error.
2. **Review stage:** built in code in a new temporary scene, never saved: a 400 × 400 m flat ground (assumed neutral albedo 0.18 grey, so colours read without a surface bias), the asset at the origin on the ground, and a 1.8 m scale bar (0.05 m square post with 0.1 m bands in alternating colours) 1 m to the side of the asset's bounds.
3. **Lights** (fixed, assumed values, kept in `Settings/Review/` as a profile so they're editable):
   - *day:* sun at 45° elevation and 135° azimuth, warm-white, with shadows; sky ambient.
   - *overcast:* no direct sun; uniform sky ambient of the same total illuminance. Shadows come only from ambient occlusion if URP's SSAO is on.
4. **Cameras:** a 60° vertical FOV (noclip default, assumed), 1024 × 1024 tiles.
   - 1.5 m: pilot height, camera 1.5 m above ground and 1.5 m from the asset (D-020's "pilot height").
   - 10, 50 and 200 m: slant distance to the bounds centre, from a 20° look-down (an assumed typical FPV approach).
   - Turntable: 8 azimuths at 45° steps, at the distance that frames the bounds plus the scale bar, from 20° look-down.
   - Overlay: the 10 m view (or the turntable distance if that's closer) with the primitives drawn on top.
5. **Overlay drawing** reads the metadata JSON and draws line meshes on a layer rendered after the scene: collision in green, gaps in magenta, wind volumes in cyan, snag features in red, with a legend. It doesn't use Unity colliders, because the sim reads the metadata, not PhysX (D-005).
6. **Composition:** one PNG per variant and light (`review-sheets/<ID>/<variant>_<light>.png`), with the tiles in a fixed grid (row 1 the four distances, rows 2–3 the turntable, row 4 the overlay and the legend), and the labels drawn into the image.
7. **build_asset.ps1** appends the capture step after the import, in the same fail-fast style.

## Risks / Trade-offs
- [Batchmode with graphics may pick the iGPU on battery] → the sheet records the GPU name in a corner label; review sheets are visual checks, not benchmarks.
- [Tiny assets vanish at 200 m] → that's correct and informative (K01 culls at about 120 m); the label says "culled" when the LOD group renders nothing.
