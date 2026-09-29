# Orchestrator check (task 5.1)

Date: 2026-09-25. Screenshots of `sample_patch` after 4.3/4.4: an overview looking north, the yard, the belt floor, and the north fields. The World Artist's measurements were run in "Best power efficiency" mode (CPU 2.1–2.6 GHz):
- sample_patch: 374 fps, 1 % low 255 (budget 120 / 90)
- 4 km map: 449 fps, 1 % low 351 (budget 90 / 60)

## Holds
- All 9 surfaces are present. From above, tilled, dry crust, the road, the yard, the meadow and the craters read as distinct areas.
- The banding is gone. The cause was the placeholder stripe pattern; the fix touches rendering only, and the golden file is unchanged apart from the patch re-record.
- The objects read at sensible scale: the belt edge with 30 tree proxies, the 4-span power line, the low garden cable, the gate, the yard junk and the rubble.
- Loader self-checks were run by the orchestrator on the pre-4.3 patch: collider poses 0.000 mm, stem parity 0.0000 mm over 7,395 bases, broken packages fall back.

## For UAT-1 (rendering and art only; none of this changes physics)
1. **Hard edge on near-camera micro-detail:** the drawn grass and straw end in a hard circle about 8 m out. UAT-1 needs a distance fade, plus cheap density-only instancing beyond the ring, so fields don't look bald past 8 m. Parity with physics must stay exact inside the physics radius.
2. **Meadow and weeds look almost the same from above** (#6f6b45 vs #6d6a38, both from footage). Real textures plus the tall-stem silhouette will separate them.
3. **Tilled ridges exist only in the physics ground.** The tilled texture needs furrows along the ridge direction, filtered with distance so they don't alias into bands.
4. **Trees are placeholder proxies** (a sphere on a stick). The real tree-belt assets are UAT-1 content.

Verdict: task 5.1 passes as a format-and-feel check. The four items above go into `build-uat1-parts`.
