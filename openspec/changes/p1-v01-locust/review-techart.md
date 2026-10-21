# Tech-artist reading: the Impostor and Leaf shaders

**Reader:** tech-artist, owner of `Shaders/` (QA 2.1's S4) · **Task:** 1.11 · **Branch:** `unity/art/p1-v01-locust` · **Tip read:** `89112e7`
**Read:** `Shaders/Impostor.shader`, `Shaders/Leaf.shader`, `Shaders/CoverageAlpha.hlsl`; `CatalogImporter.cs` (`EnsureMaterial`, `EnsureImpostor`, `ImpostorMaterial`, `EnsureLodGroup`) and `ModelPostprocessor.cs`; the URP 17.3 sources they wrap (`LitForwardPass.hlsl`, `Lighting.hlsl`, `GlobalIllumination.hlsl`, `ShaderVariablesFunctions.hlsl`, `DrawObjectsPass.cs`); `Settings/URP-*.asset`; the V01 atlases' `.meta`; design 3 and tasks 1.2a–1.6b as built; G1 task 1.1 as built (`unity/world/p1-g1-look-lock-gallery`).
**How:** a code read against URP 17.3's sources, and the vMF closed forms checked by quadrature (a plain-Python scratch script, not committed). No Unity run: no doubt below needs one to be stated. The measurements the findings ask for are tasks of their own.

## Verdict
**Sound for G1. No blocking finding.** The shaders are faithful wraps of URP/Lit, and the card, the view pick, the vMF terms and the atlas reads are correct. Findings 1–4 need work before phase 3's belts. The rest are notes.

## Findings
1. **The leaf's shadow vertex reads the object matrices before the instance id.** *Not blocking now; blocking before any instanced or BRG draw of vegetation (phase 3).*
   - `Leaf.shader:166` and `:172` call `TransformObjectToWorld` and `TransformObjectToWorldNormal` before `ShadowPassVertex` sets up the instance. Under GPU or DOTS instancing, every instance would flip its normal bias by instance 0's matrix, so a yawed tree's back-lit leaves would shadow themselves.
   - It is latent today: the leaf materials draw through the SRP batcher with instancing off.
   - **Fix:** make `UNITY_SETUP_INSTANCE_ID(input);` the first line of `LeafShadowPassVertex`, as `Impostor.shader:406` already does.
2. **The impostor's ambient and sky reflection ignore the texel's spread.** *Not blocking for G1: no impostor shows from G1's viewpoints (V01_M switches at 230 m, V01_L at 290 m). Fix it before phase 3.*
   - `Impostor.shader:364` (`InitializeBakedGIData`) evaluates the SH at the mean's direction with full bands. Over a vMF of mean resultant length R, the L1 band falls to R and the L2 band to A2 = 1 − 3R/κ (0.54 at R 0.8).
   - The review rig's flat ambient and uniform cubemap carry neither band, so every parity row is blind to this. G1's gradient sky probe is mostly L1. Derived: a texel of R 0.8 leaning 30° up takes about 5 % too much overcast ambient, with the ground at a quarter of the sky's radiance.
   - The reflection at the mean direction (`:276–278`) has the same gap. It is smaller at smoothness 0.1–0.2.
   - **Fix:** the impostor's own SH read: `SHA.w + R·L1(n) + A2·L2(n) + (1 − A2)·SHB.z/3`. Unity folds the z² constant into `SHA.w`, which is why the last term is there. Widen the reflection's roughness by the spread (Toksvig: (1 − R)/R).
3. **Parity has been measured under one light and from one look-down.** *Not blocking; do it before phase 3.*
   - Every row of tasks 1.2b–1.9 comes from the review rig: flat ambient, a uniform reflection, and a 20° look-down (`ReviewCapture.cs:29`) at the bake's 15° views (`lib/impostor.py:64`).
   - A drone at 1.5–50 m sees a crown 230–300 m away from −2° to 10° above it, under a gradient sky.
   - **Fix:**
     - Run `LodParity` once more with G1's overcast and day presets and a camera at 2 m and at 30 m.
     - If the impostor rows pop from low, bake the views at the elevation the switch is seen from.
     - Re-measure V13's far cards with this run. They carry finding 2's exposure: task 1.2j's unit normals, leaning 11.4–11.9°, stand for a spread of leaves.
4. **The extra fetch and the crowns' overdraw are unmeasured.** *Not blocking for G1 (six trees); measure before phase 3.*
   - **The extra fetch** (`CoverageAlpha.hlsl:50–51`): each leaf pixel fetches its base map twice, the alpha at no coarser than 1/1024 of the map. Far pixels therefore read mip 1 of a 2k atlas, scattered, in the forward, depth and shadow passes. The impostor reads mip 0 of all three atlases.
   - **The overdraw:** the crowns are alpha-tested and two-sided, and Forward+ runs without depth priming (`URP-High-Renderer.asset:51`). Every card layer shades the full Lit path, 7 fetches per pixel.
   - **Fix:** measure a synthetic belt (rows of V01 and V13 out to 2.5 km) per PLT-07, at 1080p and at the feed's render size, and put the cost in that task's handoff. If the belt is fragment-bound, depth priming is the first lever.
5. **The MSAA path, dormant at 1×.** *Not blocking: MSAA stays at 1×.*
   - Under MSAA, URP's `AlphaClip` sharpens the alpha by `fwidth(α)` (URP 17.3 `ShaderVariablesFunctions.hlsl:186–209`).
   - CoverageAlpha's fine read leaves α uncorrelated across a pixel quad, so `fwidth` reaches 2. The sharpened alpha then puts an opaque pixel at 0.75 and an empty one at 0.25.
   - Alpha-to-coverage's sample masks also nest across stacked layers (equal α, equal samples), so a deep crown covers the maximum of its layers, not their union.
   - **Fix, if MSAA is adopted:** per-sample alpha in both shaders (the alpha read at each sample's uv, written to `SV_Coverage`), not a sharper alpha-to-coverage.
6. **Far crowns sparkle in motion at 1×.** *Not blocking; read it out at G1 as a stand-in.*
   - Each far leaf pixel and each impostor pixel point-samples its footprint, by design, so the mean coverage is right at MSAA 1. The parity's 64 offsets measure that mean and hide the per-frame scatter.
   - A sensor integrates over its photosite (knowledge §11.6: the scene's softness comes from the sensor aperture). The feed (phase 4, rendering at reduced size per D-017) must resolve sub-pixel coverage in linear light before its analog stages.
   - That resolve, not a shader change here, is the fix, and it also sets the band's darkness (next section).
7. **No crown-scale shadowing or sky occlusion.** *Not blocking; after G1, with R1.*
   - The far LODs and the impostor have neither. Past URP-High's 50 m single-cascade shadow (`URP-High.asset:57–58`), no leaf shadows another.
   - At every distance, the occlusion is only the leaf and bark textures' own (the mask's G; for the card `Impostor.shader:182`). A crown's interior takes the full sky, and by day past 50 m the full sun.
   - Parity cannot see this, because every LOD lacks it alike. The 200 m band's darkness can.
   - **Fix (look work):** bake a crown visibility into the asset's vertices and texels: one for the sky, plus a sun term for beyond the shadow distance. Size it against H by eye before any tint is tuned.
8. **Dead pieces.** *Not blocking.*
   - `_ImpostorCard` (`Impostor.shader:54`, `:80`) is never read; the mesh carries the card's size. The importer still sets it (`CatalogImporter.cs:872`) and `ImpostorLodTests.cs:114` checks it.
   - The impostor's `LOD_FADE_CROSSFADE` block (`:355–357`) has no `multi_compile`, so it never compiles.
   - The Leaf compiles crossfade variants in all four passes (`Leaf.shader:93, 154, 195, 222`). `fadeMode = None` (`CatalogImporter.cs:931`) never uses them, and the pipeline keeps them (`URP-High.asset:33`).
   - **Fix:** drop them, or turn crossfade on for every LOD and shader at once.
   - Optionally, trim the impostor's 41-line header to what the code does, and move the tries' numbers to design 3 (D-041).

## MSAA 4× and the far crowns (G1 task 1.1: crown-to-sky 0.36 at 1×, 0.75 at 4×)
**Why MSAA pales them: antialiasing, not alpha-to-coverage.**
- At 200 m, with a 60° vertical FOV over 1080 px, a pixel spans 0.21 m.
- V01_L shows LOD2 there; its impostor starts at 290 m. Everything in the crown except the trunk is smaller than a pixel: leaflets, 3–10 mm twigs, and limbs of a few centimetres to 0.2 m.
- At 1×, each pixel is all leaf or all sky, so the crown's pixels read at the leaf's own value (0.36 of the sky).
- Any resolve gives each pixel the mix of its footprint: MSAA 4× through its samples, SMAA through its post blend.
- SMAA never sees alpha, yet it gives the same result (0.68, and the same −11 % ink as MSAA). The band's darkness is spread over more pixels, not lost.
- The opaque bark tubes resolve the same way.

**Would a sharpened alpha-to-coverage fix it? No.**
- URP already sharpens it (`SharpenAlphaStrict`). Fully sharpened, it becomes a per-pixel alpha test, and MSAA still resolves the sub-pixel edges of the cards and tubes.
- Nothing in the alpha path can make a sub-pixel leaf cover more of a pixel than it does.
- **Strike "or a sharpened alpha-to-coverage" from G1 design 5's as-built note.**

**What it means for the look.**
- The 1× ratio is aliasing, and it flatters V01. A camera integrates over its footprint (finding 6), which is what 4× shows.
- V01's band reaches H's darkness through its crown (R1's twig net, finding 7), not through the shader.
- Judge the band against H by its mean (the ink, in linear light) at the feed's resolution, not by the crown pixels' contrast at 1×. Whether G1 shows 1× or 4× is the orchestrator's call; either way, tune V01 against the resolved band.

**To settle the −11 %:** it may come from the luma's sRGB encoding (a linear blend reads lighter once encoded than the mean of its parts), from alpha-to-coverage's nesting (finding 5), or from both.
- Measure the band's mean in linear light at 1×. LodParity's 64-offset mean is the supersampled reference.
- Compare it with 4×, once with alpha-to-coverage on and once with it off. The difference between those two is alpha-to-coverage's share.

## Checked, no finding
- **The URP 17.3 copies.**
  - `ImpostorFragmentPBR` is `UniversalFragmentPBR` (`Lighting.hlsl:282–373`) without clear coat.
  - Its GI is `GlobalIllumination` (`GlobalIllumination.hlsl:508–539`) with only the Fresnel term replaced.
  - `ImpostorPassFragment` is `LitPassFragment`.
  - The Forward+ cluster loop, light layers, debug display, decals and fog (`MixFog`) are as in URP/Lit. G1's height fog needs nothing.
- **Passes.**
  - Both shaders have forward, shadow caster, depth and depth-normals passes.
  - GBuffer, Meta and MotionVectors are absent and not needed: Forward+, no baked GI, no TAA, static trees. Add them if any of those changes.
- **The card.**
  - It turns about the asset's axis to face the camera's position. That is right for a wide-FOV camera: each crown shows the view from its own azimuth.
  - View k is picked by azimuth.
  - The basis (right (−cos a, 0, sin a), up, toward the camera) is orthonormal and consistent with the uv cell and the bake.
  - The bounds cover the turn, and `DisableBatching` keeps static batching away from its object-space math.
  - The instance is set up before every object-space read.
- **The vMF terms, by quadrature** (vMF at Banerjee's κ):
  - Hill's diffuse is within 0.016 for R ≥ 0.4, and −0.04 to −0.05 below that, where κ is clamped at 1.
  - The Fresnel average always reads high: worst +0.048 (R 0.3–0.4), +0.044 at R 0.5 (the header says 0.04), +0.010 at 0.9.
  - Both are inside the parity limits, so no change (D-041 with D-040).
- **Occlusion and smoothness.**
  - Occlusion (the normal atlas's A) and smoothness (BC4 R × `_Smoothness` 1) are read at the normal's texel.
  - The occlusion applies to the indirect light only, as in URP/Lit.
  - An empty texel cannot give a NaN: the bake push-pull fills the normal atlas (`lib/impostor.py:360`).
- **The importer.**
  - Normal atlas: BC7 RGBA, linear, no mips, point filter.
  - Smoothness atlas: BC4, single channel R, linear, no mips, point filter.
  - Albedo atlas: sRGB with alpha, mips with coverage at 0.5, bilinear.
  - The impostor material: clip, alpha-to-coverage, cull off, no shadows received, instancing, the AlphaTest queue.
  - The leaf materials are set as URP/Lit's inspector sets them, and stay SRP-batcher compatible.
- **The leaf's back face.** It negates the whole frame (normal, tangent and sign), so its mapped normal is the front's negated, as on the back of a thin sheet. The shadow bias runs along the face the light sees.
- **MSAA 1×.** `_AlphaToMaskAvailable` is 0, so `AlphaClip` is a plain clip at the cutoff and `OutputAlpha` writes 1.
