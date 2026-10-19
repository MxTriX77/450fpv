# Object sheet: vegetation (V01–V21)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, `openspec/changes/p1-object-sheets/review-buildability.md`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

**Requirements:** AST-02, AST-04, AST-05, AST-06, REAL-01…05, SEC-01, SEC-02, PRC-06 · **Decisions:** D-013, D-024, D-028 · **Change:** `p1-object-sheets`, task 1.1.

## How this sheet was made

- **Letters read:** F, H, G, D, C, A, L, through contact sheets only (F 24 stills at 2/s, H 24 at 1/s, G 15 at 4/s, D 20 at 2/s, C 18 at 2/s, A 16 at 1/s, L 14 at 2/s), plus four single stills (F 14, H 9, C 2, D 4) for colour sampling. Letters B, E and J are cited where the asset list cites them, from `docs/reference-notes/terrain.md`, not re-read here. S (50 stills at 2/s, added by `p1-letter-s-village-street`, D-038) through its three contact sheets and single stills, seen from about 12 m up; its full reading is terrain.md §12, its sizes derived from the scene through the pinned feed intrinsics, never from the display. Species are read from the look only: none can be told apart at the feed's resolution.
- **Reused, not re-measured:** terrain.md §1 (T4, T5, T7 surfaces), §2 (V1–V4), §7 (micro-detail) and §8 (surface ids); knowledge.md §9.2 and §14.4; `pilot-requirements.md` PR-10. Where this sheet gives a number that terrain.md already gives, terrain.md is the source.
- **Scale cues:** trunks and shrubs against the door leaf (0.8 × 1.9 m) and brick courses (65 mm) of the cellar head in C, the pole (0.20–0.25 m, 7–9 m) in H and L, the trench width (0.6–1.0 m, terrain.md W2) in G, and the drone's own prop line (0.574 m) in the lower corners of every still.
- **Labels:** every number is **measured** (from a letter, with still numbers), **published** (with its source), **derived** (with the derivation) or **assumed**.
- **Feed check values** are mean sRGB of hand-picked regions on the extracted stills (this sheet, and terrain.md). They carry the goggles feed's shift: over-warm and saturated in sun (C, D, F, G), flat and grey in overcast (A, H). Textures are authored at photo-like albedo; the feed effect (tech-artist) adds the shift; the REAL-03 sheets compare the result against these values.

### Reference views for this group (REAL-02)

Viewpoint heights are estimated from the scene (trunk, shrub and pole heights in frame), never from the display.

| Letter | Scene type | Viewpoint | Framing | Light | Time of day | Weather |
|---|---|---|---|---|---|---|
| F | inside a belt: a corridor between thin trunks, ending in a round pit with fallen branches | 1–2 m above bare loam, flying along the corridor; the last stills at 0.5 m into the pit | forward, 0° tilt, trunks on both sides, crowns arching over at 2–4 m, sky through the crowns | low sun through the branches, strong backlight in the crowns, brown dust haze at 0.5–3 m | afternoon | dry, hazy with dust |
| H | along a belt edge over lodged straw and dark soil, past a pole | 0.5–1.5 m, slow | forward, 0° tilt, the belt wall filling the upper half, the straw mat the lower half; a clearing of dark soil mid-clip | overcast, flat, no shadows | daytime | overcast, dry, autumn (mixed green and yellow foliage) |
| G | a trench winding along a narrow, openwork tree strip beside a bare dark field | 3–6 m, fast, descending into a bush | forward-down, the strip on the left, the field on the right, the trench between | low warm sun, long shadows, saturated straw | early evening or morning | clear, dry |
| D | an overgrown plot: tall forbs, sapling thickets, a bare dead tree, stone slabs, a sagging cable | 1–2 m, slow, ending in a shrub | forward into the sun; the dead tree centred against the glare; a belt on the horizon | hard backlight with lens flare, the sun above the horizon | mid-afternoon | clear, late summer |
| C | a village yard: an old fruit tree, a brick-and-plank cellar head, sheds, leaf litter | 1.5–3 m, slow, into the cellar | forward, 0° tilt, the fruit tree at the left edge, the cellar head centre-right | bright sun from the left-behind, hard shadows | midday to afternoon | clear, autumn (sparse yellow leaves, litter) |
| A | open rolling meadow with belts on the horizon, then a long descent to the grass | 30–60 m descending to 1 m | forward-down over the meadow; belts as thin bands at the top of frame | overcast, flat, aerial haze | daytime | overcast, dry |
| L | night over open fields: a pole, a lone tree, bushes and a belt line as silhouettes | 2–5 m | forward, 0° tilt, the horizon mid-frame, the sky brighter than the ground | none (feed in black and white) | night | overcast |
| S | a village from the air: a fallow of tall pale-headed grass and rust-brown seed stands, back gardens, a green with lone shrubs, dead trees on a square, front gardens thick with shrubs and yard trees | about 12.5 m (11–14 m), 10–12 m/s; low over a mown strip at the start (S 1–3) (derived, terrain.md §12.2) | 0° uptilt on a drone pitched about 30° down: the horizon on the top edge, ground 95 % of the frame; S 10: the fallow filling the frame, the village edge along the top; S 40–42: dead pale trees on the square; S 47: front-garden crowns in the right half | bright high overcast, no shadows | daytime | damp after rain; autumn (green regrowth, bleached grass heads, first yellow leaves) |

The side-by-side sheets for these views (REAL-03) are made in `reference/_compare/` once the first vegetation assets exist; nothing exists to compare yet.

## Rules shared by every asset in this group

### Geometry (REAL-05, AST-05)

- **Branches thicker than 10 mm are geometry**, with real thickness, tapering and the real fork angles; the drone touches them and the fiber bends over them. Every such segment gets a collision capsule. Tolerance: the capsule follows the visual within 10 mm of radius.
- **Twigs 3–10 mm and leaves are cards** (leaf and twig atlases) inside the crown; they have no capsules. Their physics is the crown volume: a porous wind volume plus a compliant contact shell (`foliage` or `twig`, below). A card must never be the only thing that stops the drone.
- **Collision follows the visual** at the trunk and the limbs; the crown shell's outer surface follows the card envelope within 0.1 m. Every opening between limbs wider than 0.574 m is a real gap the sim knows.
- **How the generator writes a gap between two members** (asset-artist rule, the pipeline's gap convention): for two neighbouring members leaving one fork (two limbs ≥ 30 mm, a hanging limb and its trunk, the halves of a split trunk, two stems of a clump), the polygon lies in the plane through the fork point and the two member axes, bounded by the two member surfaces and closed by the chord where the crown shell or the next child fork begins; `narrowest_m` is the diameter of its largest inscribed circle. A branch bridging a pit (V19) or a log over the ground (V21) uses the vertical plane through its axis, bounded by the member's underside and the ground. Only polygons with `narrowest_m` ≥ 0.5 m are written.
- **Snag records** (the pipeline's two kinds): every segment under 30 mm is an `edge` along its axis with the capsule's radius; every free tip (a twig end, a splinter, a broken stub) is a `hook` from its base to its tip. The "whole crown shell" as a snag is not a record: it is the `twig` material's hook behaviour (open point for the physics engineer).
- **No texture or normal-map fakes** for anything the drone can touch: furrows in bark are a normal map (the fiber does not read them), but a broken limb's splinters, a split trunk's halves and a stump's jagged top are geometry.
- **Branch diameters follow Leonardo's rule:** at every fork the parent's cross-section equals the sum of the children's, d_p² = Σ d_c² (published: APS DFD 2011 abstract, Eloy, https://meetings-archive.aps.org/dfd/2011/l24/6). The generator takes the trunk diameter and the branching counts and derives every segment diameter from this; the twig threshold (10 mm) then falls out of the order count, not a hand-set value.
- **Taper:** a segment thins linearly between forks; the last 10 mm segment ends in cards.

### Branch and stem stiffness (derived, for the physics ids)

Cantilever k = 3EI/L³ with I = πd⁴/64; E = 10 GPa for green wood (published 8–12 GPa, physics_materials.json `foliage` rationale), E = 1–3 GPa assumed for herbaceous stalks.

| Element | d | L | k | Note |
|---|---|---|---|---|
| live branch | 20 mm | 1.0 m | ≈ 236 N/m | the `foliage` reference case |
| live twig | 10 mm | 0.8 m | ≈ 29 N/m | bends, does not stop the drone |
| live twig | 5 mm | 0.5 m | ≈ 7 N/m | the fiber's main snag; a capstan of radius 2.5 mm |
| forb stalk | 8 mm (pithy), E 2 GPa | 1.0 m | ≈ 1.2 N/m, buckles at π²EI/(4L²) ≈ 1 N | the drone wades through; hundreds together give drag |
| dead twig | 10 mm, dry | 0.5 m | snaps: M = σ_b·πd³/32 with σ_b ≈ 40 MPa (published dry hardwood MOR 60–110 MPa, weathered: assumed 40) → ≈ 3.9 N·m, ≈ 8 N at the tip | dead wood breaks where live wood bends |

### Physics ids

Ids used here, all in [physics_materials.json](../../assets-src/physics_materials.json) (never invented in a generator): `timber` (sawn and split faces, stumps' cut tops), `foliage` (crown shells), and the three this sheet asked for, added by the physics sign-off (review-materials.md §2, in the archived change `p1-pipeline-hardening`); `cable` is not used.

| Id | For | Signed-off values | Why |
|---|---|---|---|
| `bark` | every live trunk and limb capsule | μs 0.55, μk 0.45, rigid, edge radius = the capsule's own | rougher than `timber`; the fiber bends over the limb radius, not an arris |
| `twig` | crown contact shells of live trees and shrubs (cards region) | k 30 N/m, ζ 0.2, μs 0.5, edge radius 2.5 mm | the derived 5–10 mm twig band; `foliage` (200 N/m) is a 20 mm branch and would stop the drone too hard at the crown edge |
| `deadwood` | dead trees, broken tops, fallen branches, brush, logs | μs 0.45, μk 0.35, rigid; `break_stress_pa` 4.0e7, so M_b = σ_b·π·r³/4 from the capsule's own radius (= 40e6·π·d³/32) | brittle: a capsule thinner than ≈ 15 mm snaps at the drone's push; the snap is a logged event and the piece becomes debris |

`forb_stem` was declined (§2): tall weed stalks within 3 m of the drone are micro-detail cantilevers of the surface's `weeds` cover (AST-10, `MicroDetailNear`), their stiffness and buckling derived from each stem's E, d and L; no asset shape carries an id for them, and a V17 or V18 clump writes no stem collision.

### Shared generator: trees (V01–V06, V10–V12)

One parametric tree generator with a species preset per asset. Variants are seeds plus size class; states are parameters (AST-06).

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `seed` | int | any | per variant | all random draws (fork angles, lean, crown gaps) |
| `height_m` | float | 3–25 | per species | total height; everything else scales from it and `dbh_m` |
| `dbh_m` | float | 0.05–0.80 | per species | trunk diameter at 1.3 m; every branch diameter derives from it (Leonardo's rule) |
| `trunk_lean_deg` | float | 0–15 | 2 | lean of the whole trunk; belts have 5–15° on edge trees |
| `trunk_sinuosity` | float | 0–1 | 0.3 | wander of the trunk axis between forks (0 straight, 1 zigzag) |
| `stems` | int | 1–5 | 1 | stems from one base (locust suckers, elm, box elder, lilac) |
| `first_fork_m` | float | 0.3–6 | per species | height of the first main fork (yard fruit trees 1.3–1.8, belt trees 2–5) |
| `main_limbs` | int | 2–6 | 3 | limbs at the first fork |
| `orders` | int | 3–6 | 5 | branching orders before the twig cards |
| `children_per_order` | float | 1.5–3.5 | 2.4 | mean children per fork; sets the twig count and the crown density |
| `fork_angle_deg` | range | 15–70 | 25–45 | opening angle at forks; locust narrow, oak and apple wide |
| `limb_droop` | float | −0.5–1 | 0 | curvature of limbs (negative: upturned ash tips; positive: drooping cherry and old apple) |
| `crown_shape` | enum | column, oval, dome, spreading, scraggly | per species | the envelope the branches fill |
| `crown_width_m` | float | 2–15 | per species | envelope width at the widest |
| `leaf_cover` | float | 0–1 | 0.7 | share of twig tips carrying leaf cards; summer 0.9, late summer part-bare 0.6, autumn 0.2–0.4, bare 0 |
| `leaf_tint` | colour | — | per species | leaf albedo; autumn presets per species |
| `twig_threshold_mm` | float | 8–12 | 10 | below this a segment is a card (REAL-05 fixes 10; the range is for review only) |
| `broken_top_m` | float | 0 or 2–height | 0 | state V07: the trunk ends at this height in a splintered break |
| `dead` | bool | — | false | state V08: no leaf cards, twig cards reduced to `twig_cards`, bark flaking mask, `deadwood` material |
| `dead_top_frac` | float | 0–1 | 0 | share of the crown, from the top down, that is dead: no leaf cards there and `deadwood` on its capsules (V04 half-dead 0.3) |
| `twig_cards` | float | 0–1 | 1 (0.2 when `dead`) | share of twig-card tufts kept on the skeleton (V08: 0.2; its dead sapling 0.3 with brown leaves) |
| `spines` | bool | — | false | thorn cards on suckers and low branches (V01 when `stems` > 1); visual only |
| `fruit_share` | float | 0–0.5 | 0 | share of twig tips carrying pod, key or fruit cards (V01 pods 0.2 in autumn and bare; V02 keys; V10 fruit) |
| `water_sprouts` | int | 0–20 | 0 | vertical whips 0.5–1.5 m, 5–15 mm, on the upper limbs (V10 abandoned apple 5–15); those ≥ 10 mm get capsules |
| `suckers` | int | 0–10 | 0 | root-sucker whips 0.5–2 m, 5–15 mm, in a ring 0.3–1 m round the base (V11 cherry); those ≥ 10 mm get capsules and `twig` contact |
| `epicormic_tufts` | int | 0–20 | 0 | leafy shoots 0.3–1.5 m, 5–15 mm, on the trunk below a break (V07) |
| `shrapnel_scars` | int | 0–30 | 0 | bark wounds 20–150 mm on the lower 3 m, facing one side (the blast side) |
| `char_height_m` | float | 0–3 | 0 | charred band from the ground (burnt-field edges) |

Outputs per asset: LOD0 mesh (limbs + cards), LOD1–2, an impostor; capsules for every segment ≥ 10 mm; a crown shell (convex pieces per main limb) with `twig` contact; wind volume parts (trunk column, crown pieces) with porosity; snag flags on every capsule under 30 mm; the material slots.

### Shared generator: shrubs, saplings and forbs (V13–V18)

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `seed` | int | any | per variant | all draws |
| `height_m` | float | 0.3–4 | per asset | clump height |
| `width_m` | float | 0.3–4 | per asset | clump footprint |
| `stems` | int | 1–40 | per asset | stems from the base (a sapling 1, a lilac clump 20–40, forbs 5–40 per m²) |
| `stem_d_mm` | range | 3–60 | per asset | base diameter of the stems; ≥ 10 mm get capsules |
| `arch` | float | 0–1 | 0.3 | how far stems bow outward (elder, rose 0.7; caragana, saplings 0.1) |
| `fork_angle_deg` | range | 10–90 | 30–50 | opening angle where stems branch (blackthorn's stiff tangle 60–90) |
| `bare_base_m` | float | 0–1.5 | 0.3 | stems bare of twigs and leaf cards below this height (V13 variant 1: 0, variant 3: 1.0, the leggy shrub under the canopy) |
| `spines` | bool | — | false | blackthorn, rose, locust suckers: spine cards on stems |
| `leaf_cover` | float | 0–1 | 0.8 | as for trees |
| `fruit_tufts` | int | 0–60 | 0 | 1-card clusters of berries, hips, sloes or burrs (V13 honeysuckle berries, V14 elder 0–20, V15 hips 10–40, V18 burrs) |
| `basal_leaf_m` | float | 0–0.6 | 0 | size of the basal-rosette leaf cards (V18 burdock 0.3–0.6); 0 means none |
| `seed_heads` | float | 0–1 | 0 | share of stems with seed-head cards (forbs in autumn 0.8) |
| `dry` | bool | — | false | dead stalks: brown tint, leaf cards off, `deadwood` stiffness for stems |
| `char_height_m` | float | 0–1.5 | 0 | burnt base after a field fire: black bare stems and twigs below this height (V13, V14 burnt clumps) |
| `density_per_m2` | float | 5–60 | per asset | forbs only: stalks per m² of the patch |
| `species_mix` | list of (card set, weight) | weights 0–1, summing to 1 | per asset | forbs only: which leaf and head card sets the stalks draw (V17: mugwort 0.3, goldenrod 0.2, ragweed 0.2, fat-hen 0.15, thistle 0.15) |
| `patch_shape` | polygon | — | — | forbs only: the patch outline from the map (feathered, PR-11) |

### Shared generator: dead wood (V07–V09, V19–V21)

Uses the tree generator's branch skeleton (or a stored one) with `dead = true`, then cuts, splinters, chars or lays it down.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `break_height_m` | float | 0.3–10 | per asset | where the trunk ends (V07, V09) |
| `splinter_len_m` | float | 0.1–1.5 | 0.5 | length of the splinters standing up from the break |
| `splinter_count` | int | 3–12 | 6 | jagged top density |
| `hanging_limb` | bool | — | false | a broken limb still attached by a bark strip, hanging down |
| `split_depth_m` | float | 0–3 | 0 | V09: the trunk split along the grain from the top |
| `char_height_m` | float | 0–3 | 0 | charred band |
| `bark_loss` | float | 0–1 | 0.5 | share of bark gone (grey wood showing) |
| `lying` | bool | — | false | V19, V21: laid on the ground with its contacts resolved |
| `length_m` | float | 0.5–8 | per piece | lying pieces: overall length (V19 0.5–4, V21 4–8) |
| `butt_d_m`, `tip_d_m` | float | 0.02–0.40, 0.01–0.15 | per piece | lying pieces: diameter at the thick and thin end, linear taper between (V19 2–10 cm; V21 20–40 → 8–15 cm) |
| `forks` | int | 0–2 | 0 | lying pieces: side forks of the skeleton (V19 piece 1: 2) |
| `twig_share` | float | 0–1 | 0.3 | lying pieces: share of side twigs still attached (V19 0.2–0.5); ≥ 10 mm are capsules, finer are cards |
| `stubs` | int | 0–5 | 0 | V21: limb stubs 0.3–1.5 m, 3–8 cm, which hold the trunk 0–0.4 m off the ground |
| `char_share` | float | 0–1 | 0 | lying pieces: share of the surface charred (burnt edges); standing trunks use `char_height_m` |
| `root_plate` | bool | — | false | V21: a torn-out root plate 1–2 m across at the butt |

### Materials: CC0 candidates

No set is in `tools/assets/manifest.json` yet for this group (the manifest holds `red_brick` only). Candidates, all CC0, to be fetched and pinned by the asset-artist; the species match is by look, not by name:

| Material | Candidate set (CC0) | Source page | Derived textures |
|---|---|---|---|
| deeply furrowed bark (locust, old elm, oak) | Poly Haven `bark_willow`, `bark_brown_01`, `jolcham_oak_bark_01` | https://polyhaven.com/a/bark_willow · https://polyhaven.com/a/bark_brown_01 · https://polyhaven.com/a/jolcham_oak_bark_01 | per-species tint; a wound mask (R scars, G char, B flaking) |
| smooth to shallow-grooved grey bark (ash, maple, young trees) | Poly Haven `bark_brown_02`, `chinese_hackberry_bark`; ambientCG `Bark004`, `Bark009` | https://polyhaven.com/a/bark_brown_02 · https://polyhaven.com/a/chinese_hackberry_bark · https://ambientcg.com/view?id=Bark004 | tint |
| scaly plated bark (maple, apple, apricot) | Poly Haven `trident_maple_bark`; ambientCG `Bark012` | https://polyhaven.com/a/trident_maple_bark · https://ambientcg.com/view?id=Bark012 | tint |
| dark fissured elm-like bark | Poly Haven `japanese_zelkova_bark`; ambientCG `Bark006` | https://polyhaven.com/a/japanese_zelkova_bark · https://ambientcg.com/view?id=Bark006 | tint |
| dead, bark-stripped and weathered wood | ambientCG `Bark015` (layered, damaged); Poly Haven `bark_willow_02` (rough, dry) | https://ambientcg.com/view?id=Bark015 · https://polyhaven.com/a/bark_willow_02 | grey-silver tint, char mask |
| split and sawn wood faces (stumps, splinters) | a Poly Haven raw-wood set, to be picked from https://polyhaven.com/textures/raw%20wood | — | — |
| leaf and twig atlases | ambientCG `LeafSet015`–`LeafSet030` (alpha atlases) | https://ambientcg.com/list?q=leafset | per-species leaf tint and autumn tint; compound leaves (locust, ash, walnut, elder) are assembled from single-leaflet cards |
| leaf litter and twigs on the ground (used by the surfaces sheet, listed for the yard trees) | Poly Haven `forest_leaves_02`, `forest_leaves_03`, `brown_mud_leaves_01`, `dry_decay_leaves`, `leaves_forest_ground`, `forrest_ground_01` | https://polyhaven.com/a/forest_leaves_02 (and siblings) | litter needs leaf silhouettes under 4 m (K§14.4), so the litter mesh uses the same leaf cards as the tree above it |

### Colour, group-wide (photo albedo is assumed-typical until measured on the chosen set; feed values measured)

| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| belt canopy, overcast, mixed green-yellow | #55602f (assumed) | #5d6457 (H 9, crowns), terrain.md #5f6a60 | measured on H |
| belt canopy, sunlit, part-bare | #6a6a40 (assumed) | #70716d–#796e66 (F 14), terrain.md #877958 | measured on F |
| understory bushes, overcast | #58602c (assumed) | #817651 (H 9) | measured on H |
| sapling and shrub thicket, backlit | — | #6a4d33 (D 4) | measured on D |
| trunk bark, grey-brown, shaded in dust haze | #6e6559 (assumed) | #867a60 (F 14) | measured on F |
| fruit-tree bark, sunlit side | #5a4e42 (assumed) | #6b5c41 (C 2) | measured on C |
| dead trunk, backlit | #8f877a (assumed) | #705c4a (D 4) | measured on D |
| fruit-tree foliage, autumn, sunlit | #8a8a3a (assumed) | #786f53 (C 2) | measured on C |
| dead sapling with dry brown leaves | #a0784a (assumed) | #b78b52 (H 9) | measured on H |
| tall forbs, backlit | #7d7a3a (assumed) | #86723e (D 4), terrain.md #817831 | measured on D |
| dry weeds with seed heads, sunlit | #8a7a52 (assumed) | #695738 (C 2) | measured on C |
| belt as a band at 150–300 m (K§14.4) | — | dark band; backlit haze #7f7d74 (D 4) | K§14.4, measured on D |
| belt band at about 1 km, overcast | — | #9c9b87–#a2a292 against the field #9b997a (A 1–2, horizon rows) | measured on A: at that range the belt is a shape, 5–10 % darker than the field, not a dark band |
| lone tree and bushes at night | — | silhouette #444543 against sky #595a58, ground #171717 (L 1) | measured on L |
| fallow: pale grass heads, overcast (S's feed is cool: neutrals go lilac, greens saturate) | #a69c84 (assumed) | #8d8e8a (S 10), #7a8173 (S 8) | measured on S |
| fallow: rust-brown seed stands (dock-like) | #7a4e33 (assumed) | #8f7363 (S 12); mixed with grass #77695e (S 14) | measured on S |
| short grass, village green and verges, damp | #4e6a2c (assumed) | picks of pure grass #5f8d4c (S 36), #6d864d (S 24), #638648 (S 50); whole-patch means with dry stems and litter #648b55–#708657, down to #617c4e (S 24, 36): the check is the patch mean, not the pick | measured on S |
| yellow-topped verge weeds (goldenrod-like), autumn | #8a8a3a (assumed) | #6b7842 (S 46) | measured on S |
| weed fringe at a fence, rust-brown | #6a5236 (assumed) | #68562d (S 47) | measured on S |
| yard tree crown in leaf, overcast | #4a5a2a (assumed) | #506425 (S 48) | measured on S |
| dead tree, bark-stripped, pale | #a8a499 (assumed) | #9ab0b7 (S 43), #98a6a0 (S 42) | measured on S |

## V01 Belt tree: black locust (робінія / "акація")

**Seen in:** F (stills 1–17, the corridor and the pit), H (1–21, the belt wall behind the bushes), G (1–13, the openwork strip). Also B, D and A, L as the horizon band. S 43–47: tall village trees with a bare trunk to about 4–6 m (by eye) and a compact, tufted crown beside the houses fit the class as single street and yard trees (species not readable); S has no belt inside the village, only belt bands at the horizon.
**Research sources:** https://en.wikipedia.org/wiki/Robinia_pseudoacacia (size, bark, leaves, pods, suckers). The belt structure under Placement rules (rows, spacing, construction classes) is assumed from common shelterbelt practice: no generic source with these figures was found.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height, size S (edge, suckers, young) | 4–7 m | 6 m | measured (G 1–13: thin strip trees 3–6 m; F margins) |
| height, size M | 8–12 m | 10 m | measured (F 1–12 crowns arching at 2–4 m with the canopy well above; terrain.md V1 crowns 6–15 m) |
| height, size L | 12–16 m | 14 m | measured (terrain.md V1, F, H: belt crowns 6–15 m, the upper range) |
| trunk diameter S / M / L | 5–10 / 12–20 / 20–35 cm | 8 / 16 / 26 cm | measured (F 1–17: most trunks 5–15 cm, some 15–25 cm, against the 0.574 m prop line and the 2–3 m pit) |
| stems per base | 1–4 | 1 (S: 2–3) | measured (F 1–12: many two- to four-stem clumps, root suckers) |
| first fork | 2–5 m | 3 m | measured (F 1–12) |
| crown width S / M / L | 2–4 / 3–6 / 5–8 m | 3 / 5 / 6 m | derived (narrow, scraggly crown; width ≈ 20–25 × dbh, an assumed ratio) |
| crown shape | scraggly, open, flat-topped when old | — | measured (F, H) + published ("narrow crown that grows scraggly with age") |
| trunk lean | 0–15° | 3° (edge trees 8°) | measured (F 13–17: leaning trunks at the pit) |
| twigs | 3–10 mm, zigzag | — | published (zigzag twigs) |
| leaf | pinnate 15–36 cm, 9–19 leaflets 2.5–5 × 0.6–2 cm | 25 cm, 13 leaflets | published |
| thorns on suckers and low branches | up to 2 cm, paired | 1.5 cm | published |
| pods | 5–10 × 1.3 cm, hang until spring | 7 cm | published |
| bark furrows | ridges 20–50 mm wide, grooves 10–30 mm deep, crossing in diamonds | — | published pattern; depths assumed |

### Construction and materials
- Trunk: one to four stems, sinuous (`trunk_sinuosity` 0.4), deeply furrowed bark, grey-brown with orange-red in the grooves (published). Suckers at the base and along the belt edge: whips 1–3 m with thorns (V16 covers them as a separate asset).
- Branching: few main limbs at 2–5 m, ascending at 25–40°, then zigzag twigs; the crown is open and lets the sky through (F 1–12: the sky reads through every crown).
- Leaf cards: the leaflet card (2.5–5 cm) assembled into pinnate leaves; dark blue-green above, paler below; autumn clear pale yellow, dropping early (published). Late-summer state in F: `leaf_cover` 0.5–0.6, many bare twigs.
- Pods: card strips on 20 % of twig tips in the autumn and bare states (they hang through winter).
- **Materials:** bark: Poly Haven `bark_willow` or `bark_brown_01` with a grey-brown tint and the wound mask; leaves: an ambientCG LeafSet with small oval leaflets; pods: a hand-painted 2-card strip (derived texture).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark, ridges | #6e6559 (assumed) | #867a60 (F 14, in haze) | measured on F |
| bark, grooves | #7a5a44 (assumed, orange tinge) | — | published |
| leaf, summer | #4f6a2e (assumed) | #5d6457 overcast (H 9) | measured on H |
| leaf, autumn | #d4c25a (assumed, pale yellow) | #786f53 (C 2, a sunlit autumn crown of the same yellow-green class) | measured on C |
| canopy, sunlit part-bare | — | #70716d–#796e66 (F 14) | measured on F |

### Variants and states
- **Variants:** S, M, L (`height_m`, `dbh_m`, `stems`); each with 2 seeds for fork and lean draws. S is the edge and sucker tree, often multi-stem.
- **States:** summer (`leaf_cover` 0.9), late summer part-bare (0.6, F), autumn (0.3 yellow, H), bare (0, with pods); damaged → V07; dead → V08. Damage is parametric on the same skeleton (AST-06).

### Damage pattern
- Fragments scalp bark and snap limbs on the lower 3 m on the blast side (assumed; the wound kinds are published: https://www.trees.org.uk/Help-and-Advice/News/Methods-of-tree-and-landscape-restoration-after-wa). The locust resprouts from the base after the top dies, so a damaged belt mixes dead crowns with a dense young understory of suckers.

### Physics role
- **Collision:** trunk capsules per 1–2 m segment following the lean and sinuosity; every limb and branch ≥ 10 mm as capsules (an M tree: about 150–300 segments, assumed, to be counted by the generator); crown shell per main limb with `twig` contact.
- **Material ids:** `bark` on trunk and limbs, else `timber`; crown shells `twig` or `foliage`.
- **Gaps:** the belt corridor between trunks 1–3 m (terrain.md V1): passable (≥ 0.706 m diagonal). Between limbs inside the crown: openings 0.3–1.5 m; only those ≥ 0.574 m are marked passable, and only nose-first below 0.706 m. The generator writes every inter-limb opening ≥ 0.5 m as a gap polygon.
- **Wind volumes:** trunk column porosity 0.0 (width = dbh); crown pieces porosity 0.55 summer, 0.75 part-bare, 0.9 bare (assumed: an open crown; the belt-construction classes below give the belt-level target).
- **Snag hazards:** every capsule under 30 mm and the whole crown shell; thorns on suckers are visual only (cards).
- **Landable surfaces:** none.

### Placement rules (for the world)
- Belts: rows 2.5–3 m apart, 1–2 m in the row; auxiliary belts 3–4 rows, main belts 5–9 rows (assumed: common shelterbelt practice); width 10–15 m planted, growing to 10–30 m with edge suckers (terrain.md V1). Belts are locust-dominated by default (60–80 % locust, the rest V02/V03/V04: a choice for the map); V01 is the default belt tree for the map.
- Belt construction (assumed: the common agroforestry classes, their thresholds not checked against a standard): **blow-through** (gaps > 60 % in the lower profile, ≤ 10 % above), **openwork** (gaps spread through the profile), **dense** (no gaps). H is dense at low level (PR-10: bushes among the trees, no see-through); G is openwork; F is blow-through inside (bare corridor, closed crowns).
- Dead and broken trees (V07, V08): 5–15 % of stems in over-mature belts (assumed), so one every 10–20 m of belt, more at the field edge.
- Sizes along a belt: L in the middle rows, M at the rows beside them, S and sucker clumps at both edges.

### Generator parameters
Preset over the shared tree generator: `height_m` 6/10/14, `dbh_m` 0.08/0.16/0.26, `stems` 2/1/1, `first_fork_m` 2.5/3/4, `main_limbs` 3, `orders` 5, `children_per_order` 2.2, `fork_angle_deg` 25–40, `trunk_sinuosity` 0.4, `crown_shape` scraggly, `crown_width_m` 3/5/6, `leaf_cover` by state, `spines` true when `stems` > 1, `fruit_share` 0.2 (pods) in the autumn and bare states. Ranges and effects: see the shared table.

### Open points
- Crown width from trunk diameter: the 20–25 × dbh ratio is assumed; a generic crown-allometry source would confirm it. Research, not a pilot question.
- The segment count per tree against the world-query budget (K§10.5): no limit from physics (the physics sign-off, review-materials.md §5: the per-step batch is 46 swept capsules against the near set, so the cost is the world's broadphase, not the count); the world-builder's, signed off at the phase-3 world-query API review; if it bites, segments 10–15 mm fold into the `twig` shell.

## V02 Belt tree: ash

**Seen in:** F (1–12: the smoother, straighter trunks among the locusts), H (1–21). **R** for the species detail.
**Research sources:** https://en.wikipedia.org/wiki/Fraxinus_excelsior (size, bark by age, leaves, keys, late leafing and early drop).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height S / M / L | 5–7 / 9–12 / 13–18 m | 6 / 11 / 15 m | published (12–18 m typical) + measured belt crowns 6–15 m (terrain.md V1, F, H) |
| trunk diameter S / M / L | 6–10 / 14–22 / 25–40 cm | 8 / 18 / 30 cm | derived from height by the locust ratio (assumed the same) |
| first fork | 3–6 m | 4 m | assumed (a tall narrow crown) |
| crown width S / M / L | 2–3 / 3–5 / 5–7 m | 2.5 / 4 / 6 m | published ("tall and narrow") |
| twigs | stout, 5–12 mm, grey-green, tips upturned, black buds | — | published |
| leaf | pinnate 20–35 cm, 7–13 leaflets 3–12 × 0.8–3 cm | 28 cm, 11 leaflets | published |
| keys | 2.5–4.5 cm, in bunches, hang through winter | — | published |

### Construction and materials
- Trunk straight, bark smooth pale grey on S, vertically fissured with a shallow net on M and L (published). Limbs few and stout, upturned at the tips (`limb_droop` −0.3). The crown is leafy only at the outer shell; the inside is open.
- Leaves open late and drop early, often while still green, or bright yellow (published): the autumn state is bare twigs with key bunches while V01 and V03 still carry leaves.
- **Materials:** bark S: Poly Haven `bark_brown_02` or `chinese_hackberry_bark`, pale grey tint; M/L: ambientCG `Bark004` or Poly Haven `bark_brown_01` with a grey tint; leaves: an ambientCG LeafSet with narrow serrated leaflets; keys: a 2-card bunch (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark, young | #8b8a82 (assumed) | — | published (pale grey) |
| bark, old | #6f6c66 (assumed) | #867a60 (F 14, belt trunk class) | measured on F |
| leaf, summer | #4a6a2a (assumed) | #5d6457 (H 9) | measured on H |
| keys, dry | #a8966a (assumed) | — | published |

### Variants and states
- Variants S, M, L; states summer, late summer (`leaf_cover` 0.7), autumn-bare with keys (0.05), dead (V08), broken top (V07).

### Damage pattern
- As V01; ash does not sucker, so a dead ash stays a bare standing tree (V08 default species with elm) rather than regrowing.

### Physics role
- **Collision:** as V01 (fewer, thicker limbs: about 100–200 segments for M, assumed).
- **Material ids:** `bark`, `twig`/`foliage`.
- **Gaps:** inter-limb openings 0.5–2 m, many passable nose-first; the generator writes them.
- **Wind volumes:** crown porosity 0.45 summer, 0.85 bare (assumed).
- **Snag hazards:** the stout twig tips (5–12 mm) and the key bunches; capsules under 30 mm.
- **Landable surfaces:** none.

### Placement rules
- 10–30 % of a belt mixed with V03 and V01; rarely the main species. Keeps to the inner rows.

### Generator parameters
Preset: `height_m` 6/11/15, `dbh_m` 0.08/0.18/0.30, `first_fork_m` 3/4/5, `main_limbs` 2–3, `orders` 4, `children_per_order` 2.0, `fork_angle_deg` 20–35, `limb_droop` −0.3, `crown_shape` oval, `crown_width_m` 2.5/4/6.

### Open points
- None beyond the shared ones.

## V03 Belt tree: maple (incl. box elder)

**Seen in:** F, H (as the denser, rounder crowns in the belt wall, H 1–8). Box elder as saplings: D (1–14), H (7–12). **R** for the species detail.
**Research sources:** https://en.wikipedia.org/wiki/Acer_platanoides (Norway maple: 20–30 m, bark, leaf 7–14 cm, yellow autumn); https://en.wikipedia.org/wiki/Acer_negundo (box elder: 10–25 m, trunk 30–50 cm, multi-stemmed, brittle, green twigs, 3–7 leaflets, invasive in Central Europe). The belt mix (Norway maple in the inner rows, box elder and shrubs at the edges) is assumed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Norway maple height S / M / L | 5–7 / 8–12 / 12–16 m | 6 / 10 / 14 m | published (20–30 m free-grown) capped by measured belt crowns 6–15 m (terrain.md V1, F, H) |
| Norway maple trunk S / M / L | 6–10 / 14–24 / 25–45 cm | 8 / 18 / 32 cm | derived (as V02) |
| Norway maple crown width | 4–6 / 6–9 / 8–11 m | 5 / 7 / 9 m | published ("broad and rounded") + assumed ratios |
| box elder height / trunk | 6–12 m / 15–40 cm | 9 m / 25 cm | published (10–25 m, 30–50 cm) capped for belts |
| box elder stems | 2–5 from the base, leaning 10–30° | 3 | published (multi-stemmed) + measured D (leaning sapling thickets) |
| box elder crown | irregular, 5–9 m wide, 3–7 m from the ground | 7 m | published + assumed |
| leaf, Norway maple | palmate 7–14 cm long, 8–25 cm across, 5 lobes | 12 cm | published |
| leaf, box elder | pinnate 3–7 leaflets 5–10 × 3–7 cm | 5 leaflets | published |
| keys | paired, wings 3–5 cm (Norway) | — | published |

### Construction and materials
- Norway maple: a straight trunk with grey-brown bark, shallow criss-cross grooves (published); a dense rounded crown that blocks the sky (the opposite of V01); bright yellow autumn, orange-red on some trees (published), matching the reddish-orange patches in D 9–13.
- Box elder: several leaning stems from one base, pale grey to light brown bark with deep fissures and broad scaly ridges, green twigs with a waxy bloom (published), brittle limbs that snap and hang (the V07 `hanging_limb` default).
- **Materials:** Norway maple bark: Poly Haven `trident_maple_bark` or ambientCG `Bark012`; box elder bark: Poly Haven `bark_brown_01` with a pale tint; leaves: a LeafSet with lobed leaves and one with 3–5 leaflet cards; autumn tints yellow and orange-red (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark, Norway maple | #6b645c (assumed) | #867a60 (F 14, belt trunk class) | measured on F |
| bark, box elder | #8a8273 (assumed) | — | published |
| leaf, summer | #3f6626 (assumed, dense dark green) | #5d6457 (H 9) | measured on H |
| leaf, autumn yellow / orange-red | #d8b232 / #b8532a (assumed) | reddish-orange patches in D 9–13 (not sampled, backlit) | measured on D |
| twig, box elder | #7a9a4a green (assumed) | — | published |

### Variants and states
- Variants: Norway maple S, M, L; box elder M (multi-stem) and S (sapling, see V16). States as V01; box elder bare state keeps hanging keys.

### Damage pattern
- As V01; box elder breaks easily (brittle) and the broken limbs hang by bark strips.

### Physics role
- **Collision:** as V01; a dense crown shell for Norway maple (the drone cannot see through it: `twig` contact matters more here).
- **Material ids:** `bark`, `twig`/`foliage`.
- **Gaps:** few inside a Norway maple crown (openings < 0.5 m); between box elder stems 0.3–1.0 m at the base (passable only where ≥ 0.574 m).
- **Wind volumes:** Norway maple crown porosity 0.35 summer, 0.8 bare; box elder 0.5 / 0.85 (assumed).
- **Snag hazards:** as V01.
- **Landable surfaces:** none.

### Placement rules
- Norway maple: 10–40 % of belts, inner rows. Box elder: self-seeded at belt edges, in yards, along fences and at ruined walls (the most common self-seeded tree, published invasive status); its saplings are V16's default.

### Generator parameters
Norway maple preset: `height_m` 6/10/14, `dbh_m` 0.08/0.18/0.32, `first_fork_m` 2.5/3.5/4.5, `main_limbs` 3–4, `orders` 5, `children_per_order` 2.8, `fork_angle_deg` 35–55, `crown_shape` dome, `crown_width_m` 5/7/9. Box elder preset: `height_m` 9, `dbh_m` 0.25, `stems` 3, `trunk_lean_deg` 10–25, `first_fork_m` 1–2, `fork_angle_deg` 40–70, `crown_shape` spreading, `crown_width_m` 7.

### Open points
- None beyond the shared ones.

## V04 Belt tree: elm

**Seen in:** **R** (the asset list); the bushy, dark, irregularly fissured trunks in F 1–12 and the dead thin trees in G 1–13 fit the class.
**Research sources:** https://en.wikipedia.org/wiki/Ulmus_pumila (Siberian elm: 15–25 m, dbh to 1 m, dark grey irregularly fissured bark, bushy crown, brittle wood, leaves 2–8 cm, short-lived under 60 years, the classic post-drought shelterbelt tree).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height M / L | 8–12 / 12–16 m | 10 / 14 m | published (15–25 m) capped by measured belt crowns (terrain.md V1, F, H) |
| trunk M / L | 15–25 / 25–40 cm | 20 / 30 cm | published (rapid, 40 mm per year on stumps) + derived as V02 |
| stems | 1–3 | 1 | assumed (often bushy) |
| first fork | 1.5–4 m | 2.5 m | assumed (low, bushy) |
| crown width M / L | 5–8 / 7–10 m | 6 / 8 m | published ("bushy") + assumed |
| leaf | elliptic 2–8 × 1.2–3.5 cm | 5 cm | published |
| bark fissures | irregular, longitudinal, 10–20 mm deep | — | published pattern, depth assumed |

### Construction and materials
- A short trunk with dark grey, irregularly fissured bark; a bushy, poor-shaped crown of many thin brittle twigs; dies young (published: rarely over 60 years), so a belt of this age carries many dead and half-dead elms with bare tops: the V08 default.
- **Materials:** bark: Poly Haven `japanese_zelkova_bark` or ambientCG `Bark006`, dark grey tint; leaves: a LeafSet with small elliptic serrated leaves, yellow autumn tint.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark | #5e5b56 (assumed, dark grey) | #867a60 (F 14, belt trunk class) | measured on F |
| leaf, summer | #4c6a30 (assumed) | #5d6457 (H 9) | measured on H |
| dead crown, grey | #8f877a (assumed) | #705c4a (D 4, dead trunk backlit) | measured on D |

### Variants and states
- Variants M, L; states summer, autumn yellow, half-dead (`leaf_cover` 0.3 with the top 30 % dead), dead (V08).

### Damage pattern
- Brittle: storm and blast both leave hanging limbs and broken tops; bark peels off dead stems in long strips.

### Physics role
- **Collision, materials, gaps, snag:** as V01, with a denser twig shell (`twig` contact) and more dead capsules (`deadwood` on the dead top).
- **Wind volumes:** crown porosity 0.45 summer, 0.7 half-dead, 0.9 dead (assumed).
- **Landable surfaces:** none.

### Placement rules
- 10–30 % of belts, more at dry belt edges; dead and half-dead elms stand at belt edges and strip ends (G).

### Generator parameters
Preset: `height_m` 10/14, `dbh_m` 0.20/0.30, `first_fork_m` 2.5, `main_limbs` 4, `orders` 5, `children_per_order` 3.0, `fork_angle_deg` 35–60, `crown_shape` oval, `crown_width_m` 6/8, `dead_top_frac` 0–0.3 by state (half-dead 0.3).

### Open points
- None beyond the shared ones.

## V05 Poplar (roadside, tall)

**Seen in:** **R**. Not in the letters read.
**Research sources:** https://en.wikipedia.org/wiki/Populus_nigra (20–30 m, dbh to 1.5 m, grey-brown thick furrowed bark with burrs, leaves 5–8 cm triangular, 'Italica' fastigiate with a very narrow crown, sheds branches from 40–50 years and blows over); https://en.wikipedia.org/wiki/Populus_nigra_%27Italica%27.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height, columnar / broad | 15–25 m / 15–22 m | 20 / 18 m | published (20–30 m) capped for the asset list's 15–25 m |
| trunk | 40–80 cm | 55 cm | published (to 1.5 m) + assumed for 40–60-year roadside trees |
| crown width, columnar / broad | 2–4 m / 8–12 m | 3 / 10 m | assumed (fastigiate "very narrow") |
| branch angle, columnar | 10–25° from the trunk | 15° | published ("more or less parallel") |
| leaf | triangular 5–8 × 6–8 cm | 7 cm | published |
| bark | furrowed, grooves 20–40 mm deep, burrs 0.2–0.6 m | — | published pattern, sizes assumed |

### Construction and materials
- Two variants: the columnar Lombardy form (a line along a road) and the broad hybrid form (a lone tree or a short row at a yard). Grey-brown furrowed bark with burrs on the lower trunk; pale greenish-grey on the upper trunk and limbs (assumed). Old trees shed limbs and carry dead tops (published): `broken_top_m` at 60–80 % of height is a normal state, not damage.
- **Materials:** bark: Poly Haven `bark_brown_01` with a grey tint, a burr mask (derived); leaves: a LeafSet with triangular leaves, yellow autumn.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark, lower trunk | #6a6358 (assumed) | — | published |
| bark, upper | #9a9a8a (assumed) | — | assumed |
| leaf, summer | #4e7030 (assumed) | — | published |

### Variants and states
- Variants: columnar, broad. States: summer, autumn yellow, bare; dead top (`broken_top_m`) on 20–40 % of columnar trees in a row (assumed).

### Damage pattern
- Wind and blast both snap the brittle top third; the trunk splits lengthwise when the tree falls (V09, V21 at 20–25 m length: the longest log class).

### Physics role
- **Collision:** as V01; the columnar crown is one tall shell.
- **Material ids:** `bark`, `twig`/`foliage`, `deadwood` on dead tops.
- **Gaps:** a columnar row 3–6 m apart gives gaps 1–3 m between crowns: passable.
- **Wind volumes:** columnar crown porosity 0.4 summer, 0.8 bare; a row is a tall porous wall (a strong wake: K§6.7).
- **Snag hazards:** as V01.
- **Landable surfaces:** none.

### Placement rules
- Rows along asphalt roads and at village entrances, 3–6 m apart (assumed); singles at yards. Night landmarks (L: lone trees read only as silhouettes; the columnar outline is the clearest).

### Generator parameters
Columnar preset: `height_m` 20, `dbh_m` 0.55, `first_fork_m` 1–2, `main_limbs` 6, `fork_angle_deg` 10–25, `limb_droop` −0.5, `crown_shape` column, `crown_width_m` 3. Broad preset: `height_m` 18, `first_fork_m` 3–5, `fork_angle_deg` 30–55, `crown_shape` oval, `crown_width_m` 10.

### Open points
- Crown width of the columnar form is assumed (2–4 m); the artist checks it against any CC0 photo of a Lombardy row before the review sheet.

## V06 Oak

**Seen in:** **R**.
**Research sources:** https://en.wikipedia.org/wiki/Quercus_robur (typically to 20 m, greyish-brown closely grooved bark with vertical plates, spreading unevenly domed crown, massive lower limbs, leaves 10–12 × 7–8 cm with 3–6 rounded lobes).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height M / L | 10–14 / 14–18 m | 12 / 16 m | published (to 20 m typical) within the asset list's 10–18 m |
| trunk M / L | 30–50 / 50–80 cm | 40 / 65 cm | assumed for 40–80-year belt oaks |
| first fork | 2–4 m (belt), 1.5–3 m (open-grown) | 3 m | assumed |
| crown width M / L | 8–12 / 12–16 m | 10 / 14 m | published ("spreading") + assumed ratios |
| lower limbs | 15–30 cm, nearly horizontal | 20 cm | published ("massive lower branches") |
| leaf | 10–12 × 7–8 cm, lobed | 11 cm | published |
| bark plates | vertical, 30–60 mm wide, grooves 15–30 mm | — | published pattern, sizes assumed |

### Construction and materials
- A stout trunk, heavy crooked limbs at wide angles (`fork_angle_deg` 45–70, `trunk_sinuosity` 0.5), a domed crown; young oaks hold brown dead leaves through winter (marcescence, assumed from general knowledge, labelled assumed).
- **Materials:** bark: Poly Haven `jolcham_oak_bark_01` or ambientCG `Bark012`; leaves: a LeafSet with lobed oak leaves; winter brown-leaf tint (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark | #66605a (assumed) | — | published |
| leaf, summer | #3e5d26 (assumed, dark green) | — | published |
| leaf, winter brown (marcescent) | #8a6a3c (assumed) | — | assumed |

### Variants and states
- Variants M, L; states summer, autumn brown-yellow, bare, bare with held brown leaves (M only); damaged → V07; a shell-split oak is V09's strongest case (the hardest wood splinters longest).

### Damage pattern
- Thick bark takes fragment scars without dying; limbs snap and hang; a blast at the trunk splits it into standing halves (V09).

### Physics role
- **Collision:** as V01 with thick low limbs (15–30 cm capsules the drone can hit at 2–4 m height).
- **Material ids:** `bark`, `twig`/`foliage`.
- **Gaps:** under the lowest limbs 2–4 m clear: passable; inside the crown openings 0.5–1.5 m.
- **Wind volumes:** crown porosity 0.35 summer, 0.8 bare (assumed).
- **Snag hazards:** as V01.
- **Landable surfaces:** a horizontal lower limb ≥ 0.3 m wide is a landable surface for two legs (the drone tilts; see V21 for the rule).

### Placement rules
- The main species in some belts (1–3 rows of oak with V02/V03 companions); lone oaks at field corners and yards: one per village (assumed).

### Generator parameters
Preset: `height_m` 12/16, `dbh_m` 0.40/0.65, `first_fork_m` 3, `main_limbs` 4–5, `orders` 6, `children_per_order` 2.5, `fork_angle_deg` 45–70, `trunk_sinuosity` 0.5, `crown_shape` dome, `crown_width_m` 10/14.

### Open points
- None beyond the shared ones.

## V07 Damaged tree, broken top

**Seen in:** B (per the asset list and terrain.md V2: thin trees with broken tops), G (1–13: thin leafless trees with small, ragged crowns in the strip), D (1–12: the dead tree's top is a thin fan, part broken). B not re-read here.
**Research sources:** war damage to trees: https://www.trees.org.uk/Help-and-Advice/News/Methods-of-tree-and-landscape-restoration-after-wa (scalped bark, fractured trunks and limbs, fragment wounds, burnt stems, overturned trees, epicormic regrowth).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| standing height after the break | 4–10 m | 6 m | measured (G 1–13: 3–6 m thin trees; terrain.md V2 4–10 m) |
| trunk | 10–30 cm | 18 cm | measured (terrain.md V2, B, D, G) |
| break height | 2–6 m | 4 m | assumed (the top third to half goes) |
| splinters at the break | 0.3–1.0 m long, 2–6 cm wide | 0.5 m | assumed (dry hardwood splits along the grain) |
| hanging limb | 1–3 m, held by a bark strip 5–15 cm wide | 2 m | published pattern, sizes assumed |
| epicormic shoots below the break | 0.3–1.5 m, 5–15 mm | 0.8 m | published (epicormic growth), sizes assumed |
| fragment scars | 20–150 mm, on the lower 3 m, blast side | 60 mm | published (low on the trunk) |

### Construction and materials
- The V01–V06 skeleton cut at `break_height_m`, with `splinter_count` splinters standing from the break, the top lying nearby as a V19 or V21 piece (optional), and `hanging_limb` on one side. Bark scalped in patches (wound mask R), pale wood showing; live foliage only on the surviving limbs (`leaf_cover` 0.3–0.5) plus epicormic shoots as leafy card tufts on the trunk.
- **Materials:** the parent species' bark with the wound mask; split faces: the raw-wood set; dead parts `Bark015`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| split wood, fresh | #c9b48a (assumed) | — | assumed |
| split wood, weathered | #9a9283 (assumed) | #705c4a (D 4, dead wood backlit) | measured on D |
| scalped wound | #b89a6a (assumed) | — | published |

### Variants and states
- Two variants: (1) a snapped top with a hanging limb, live below (species V01 or V03); (2) a splintered trunk at 2–3 m with no hanging limb and the crown gone, epicormic tufts only (species V04). The break height, splinter count and hanging limb are parameters, so any species can take the state.

### Damage pattern
- First to go: the crown's thin limbs (fragments), then the top at a wound (wind finishes it). What lies around: the fallen top (V19 pieces, 2–6 m), bark slabs 0.1–0.5 m, splinters 0.1–0.5 m, within 1–2 crown widths.

### Physics role
- **Collision:** as the parent tree below the break; splinters as 3–12 thin capsules (2–6 cm) pointing up; the hanging limb as capsules with `deadwood`.
- **Material ids:** `bark` below, `deadwood` for splinters and the hanging limb, `timber` for the split face.
- **Gaps:** the hanging limb forms a loop with the trunk, 0.3–1.0 m: passable only if ≥ 0.574 m; the generator writes it.
- **Wind volumes:** a reduced crown, porosity 0.7 (assumed).
- **Snag hazards:** the splinters (the sharpest snag in the group: a splinter tip 2 cm wide is an `edge` with radius 5 mm), the hanging limb's twigs.
- **Landable surfaces:** none.

### Placement rules
- One per 10–20 m of belt edge facing open ground (assumed 5–15 % of stems); every strip beside a trench (G) has several; one or two per shelled yard.

### Generator parameters
Over the dead-wood generator: `break_height_m` 2–6 (default 4), `splinter_len_m` 0.3–1.0, `splinter_count` 4–10, `hanging_limb` true for variant 1, `bark_loss` 0.2–0.5, `shrapnel_scars` 5–20, `epicormic_tufts` 4–12 (count assumed; the dimension table gives their size), plus the parent preset with `leaf_cover` 0.3–0.5.

### Open points
- The share of broken trees per belt is assumed; the earthworks analyst reading B (task 1.3) can count broken tops per 100 m of belt edge and replace the assumption.

## V08 Dead tree, bare and leaning

**Seen in:** D (1–12: a bare dead trunk in the weeds, leaning, a thin fan of limbs at the top), G (1–13: leafless thin trees in the strip), H (9–10: a dead sapling with dry leaves). Also B, I, J (terrain.md V2). S 40–43: upright dead trees on a village square, bark stripped so trunk and limbs read pale against the grass, 4.7–5.9 m (derived: S 42, foot and top at the 11–14 m camera height), a few stout limbs forking from about 1.5–2.5 m (by eye), no twigs left.
**Research sources:** elm lifespan (https://en.wikipedia.org/wiki/Ulmus_pumila: rarely over 60 years); the locust heart-rot note (https://en.wikipedia.org/wiki/Robinia_pseudoacacia).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 4–10 m | 6 m | measured (D 1–12: 5–7 m against the 1.5–3 m shrubs beside it; terrain.md V2) |
| trunk | 10–30 cm | 20 cm | measured (D 4: 15–25 cm at the 0.574 m prop line, 3–4 m away) |
| bare limb fan | 3–6 m wide | 4 m | measured (D 1–12: 3–5 m; terrain.md V2) |
| lean | 0–15° | 7° | measured (D: 5–10°) |
| surviving limbs | 3–8 limbs 3–10 cm, twigs ≥ 10 mm only | 5 | assumed (twigs under 10 mm are shed first) |
| bark loss | 30–100 % | 60 % | assumed |
| dead sapling (variant 2) | 2–3 m, stem 2–4 cm, dry brown leaves held | 2.5 m | measured (H 9–10) |

### Construction and materials
- Variant 1, the standing dead tree: a V04 or V01 skeleton with `dead` true: no leaf cards, twig cards at 20 %, limbs ending in broken stubs, bark flaking off in strips (the flaking mask B) to show grey weathered wood; a slight lean; a woodpecker-holed, hollow look is a normal map, nothing more.
- Variant 2, the dead sapling: a single whip 2–3 m with a few side twigs, still holding dry brown leaves (H 9–10); brown leaf cards at 30 %.
- **Materials:** ambientCG `Bark015` (layered, damaged) with a silver-grey tint for the wood, the parent bark on the remaining strips; variant 2: the parent sapling bark and a brown-tinted LeafSet.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered dead wood | #8f877a (assumed, silver grey) | #705c4a (D 4, backlit) | measured on D |
| remaining bark strips | #5e5b56 (assumed) | — | assumed |
| dry brown leaves (variant 2) | #a0784a (assumed) | #b78b52 (H 9) | measured on H |
| silhouette at night | — | #444543 against sky #595a58 (L 1) | measured on L |

### Variants and states
- Variants: 1 standing dead tree (species V04 elm default; V01 alternative with suckers at the base), 2 dead sapling. States: `lean` 0–15°, `bark_loss` 0.3–1.0; a fallen one is V21.

### Damage pattern
- Dead limbs snap at the drone's push (the `deadwood` break moment): a 10 mm dead twig snaps at ≈ 8 N at 0.5 m; fallen pieces become V19 debris under the tree.

### Physics role
- **Collision:** trunk and limb capsules (≥ 10 mm) only: 30–80 segments (assumed); no crown shell.
- **Material ids:** `deadwood` throughout.
- **Gaps:** between the bare limbs 0.5–2 m: passable; the generator writes them, since the drone will fly through dead crowns (the thin twigs are nearly invisible on the feed, terrain.md V2).
- **Wind volumes:** porosity 0.9 (assumed).
- **Snag hazards:** every capsule; the dead twigs are the hard-to-see snag (terrain.md V2).
- **Landable surfaces:** none.

### Placement rules
- One or two per overgrown yard (D) in the middle of the weeds; several per belt edge, at strip ends and beside trenches (G); the tallest thing in open ground, so a night landmark (L).

### Generator parameters
Over the tree preset with `dead` true: `trunk_lean_deg` 0–15, `bark_loss` 0.3–1.0, `leaf_cover` 0, `twig_cards` 0.2 (variant 2: 0.3, brown leaf cards).

### Open points
- None.

## V09 Shell-split trunk / charred stump

**Seen in:** **R**. Burnt fields (T3b) are confirmed by the pilot but not in the letters.
**Research sources:** https://www.trees.org.uk/Help-and-Advice/News/Methods-of-tree-and-landscape-restoration-after-wa (burnt stems and main branches from direct flame and nearby building fires; fractured trunks).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| stump height, split trunk | 1–4 m | 2.5 m | assumed |
| stump height, charred stump | 0.3–1.5 m | 0.8 m | assumed |
| trunk | 20–40 cm | 30 cm | assumed (a mature belt or yard tree) |
| split depth | 0.5–3 m from the top | 1.5 m | assumed |
| splinters | 0.3–1.5 m, 2–8 cm | 0.8 m | assumed |
| char band | 0–2 m from the ground, char depth 5–20 mm | 1 m | assumed (grass-fire flame height 1–2 m) |

### Construction and materials
- Variant 1, the shell-split trunk: a trunk opened along the grain into two or three standing halves, pale split faces, splinters at the top, bark hanging in slabs; roots and the lower 0.5 m intact.
- Variant 2, the charred stump: a low stump with a jagged top, charred black on the fire side, grey ash dusting, cracked char (alligator pattern) as geometry only where it changes the silhouette (the cracks are a normal map).
- **Materials:** the raw-wood set for the split faces (tinted pale), `Bark015` for the weathered parts, a char mask (G of the wound mask) driving a black, low-roughness-contrast char material.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| split face, fresh / weathered | #c9b48a / #9a9283 (assumed) | — | assumed |
| char | #1e1c1a (assumed) | — | assumed |
| ash dusting | #b9b6ad (assumed) | — | assumed (terrain.md T3b: grey-white ash) |

### Variants and states
- Variants: split trunk, charred stump; `split_depth_m`, `char_height_m`, `splinter_len_m` as parameters.

### Damage pattern
- The split halves lean outward and shed slabs; what lies around: bark slabs, splinters, a burnt patch 2–6 m (terrain.md R0 and T3b) at the stump.

### Physics role
- **Collision:** each half as capsules; the splinters as thin capsules; the stump top as a convex jagged mesh (geometry, REAL-05).
- **Material ids:** `timber` for split faces, `deadwood` for splinters, `bark` for the bark slabs.
- **Gaps:** between the split halves 0.2–0.8 m: not passable below 0.574 m; the generator writes it anyway (the fiber can enter).
- **Wind volumes:** none (solid, thin).
- **Snag hazards:** splinters (edge radius 5–10 mm), the slot between the halves (a fiber trap).
- **Landable surfaces:** the charred stump top (0.2–0.4 m across, jagged): two legs on, two off; see V21.

### Placement rules
- Split trunks: 1–3 per shelled belt section or yard. Charred stumps: along burnt-field edges and where a belt met a field fire, 1 per 20–50 m.

### Generator parameters
Over the dead-wood generator: `break_height_m` 1–4 / 0.3–1.5, `split_depth_m` 0.5–3, `splinter_len_m` 0.3–1.5, `splinter_count` 4–12, `char_height_m` 0–2, `bark_loss` 0.5–1.0.

### Open points
- Nothing in the letters shows one; the first review sheet (1.5 m view) decides the splinter sizes.

## V10 Yard fruit tree: apple

**Seen in:** C (1–6: an old tree at the left edge, and the row behind it), J (per the asset list: a bare yard tree; not re-read here). S 45–50: rounded yard and front-garden crowns in leaf, one to three per plot, about 5–7 m (derived, rough: S 48, crown top against its visible foot), overlapping the fences and the verge (species not readable; V10–V12 by placement).
**Research sources:** https://en.wikipedia.org/wiki/Malus_domestica (standard trees 2–10 m, to 15 m wild; dark grey or grey-brown trunk, smooth reddish young branches; fruit 2.5–12 cm).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 4–7 m | 5.5 m | measured (C 1–6: 4–6 m against the cellar head 1.8–2.0 m; terrain.md V3) |
| trunk | 20–40 cm | 30 cm | measured (C 2: 20–35 cm against the 0.574 m prop line at 3 m; terrain.md V3) |
| first fork | 1.3–1.8 m | 1.5 m | measured (C 2: the trunk forks at about 1.5 m into 3 limbs) |
| main limbs | 3–5, 8–15 cm | 4 | measured (C 2) |
| crown width | 5–8 m | 6 m | measured (C 1–6; terrain.md V3) |
| lowest branches | 1.5–2.5 m above ground | 1.8 m | measured (C 2) |
| twigs | 3–10 mm, many, bare in autumn | — | measured (C 1–6: thin dark lines against the sky) |
| leaf | ovate 5–10 cm | 7 cm | published (shape), size assumed |
| fruit | 5–8 cm | 6 cm | published (2.5–12 cm) |
| spacing in a yard row | 4–7 m | 5.5 m | measured (C 1–6: 5–7 m) |

### Construction and materials
- A short, dark, gnarled trunk, bark in grey-brown scaly plates, forking low into 3–5 spreading limbs (`fork_angle_deg` 45–70, `limb_droop` 0.4 on old trees), a wide low crown; an abandoned tree carries water sprouts (vertical whips 0.5–1.5 m on the upper limbs) and dead twigs inside the crown (assumed from the abandoned state).
- Leaves ovate, dull green, yellow in autumn, sparse at the time of C (`leaf_cover` 0.3); a few fruit cards in late summer and fallen fruit in the litter.
- **Materials:** bark: Poly Haven `trident_maple_bark` (scaly plates) with a dark grey-brown tint; leaves: a LeafSet with ovate serrated leaves; fruit: a 2-card sphere texture (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| trunk bark, sunlit side | #5a4e42 (assumed) | #6b5c41 (C 2) | measured on C |
| foliage, autumn, sunlit | #8a8a3a (assumed) | #786f53 (C 2, the row behind) | measured on C |
| leaf, summer | #4e6a30 (assumed) | — | published |

### Variants and states
- Variants: old spreading (C), younger upright (6 m, narrower, fork at 1.8 m). States: summer, autumn sparse (C), bare; damaged → V07 (a snapped main limb hanging), dead → V08.

### Damage pattern
- A main limb torn off at the fork (the weak point of an old apple), the split running down the trunk; fallen fruit and leaves under it (T6).

### Physics role
- **Collision:** trunk capsule 1.5 m; 3–5 limb capsules 8–15 cm; branches ≥ 10 mm as capsules (about 100–200 segments, assumed); crown shell with `twig`.
- **Material ids:** `bark`, `twig`/`foliage`.
- **Gaps:** under the crown 1.5–2.5 m clear: passable at yard-approach height, right where the yard is flown (terrain.md V3); openings between the main limbs 0.5–1.2 m at the fork: passable nose-first where ≥ 0.574 m; the generator writes them.
- **Wind volumes:** crown porosity 0.5 summer, 0.85 bare (assumed): a small wind shadow in the yard.
- **Snag hazards:** the low twigs at 1.5–3 m, the water sprouts.
- **Landable surfaces:** the fork at 1.5 m (a saddle 0.3–0.5 m across between the limbs): two to three legs; the drone tilts and slides.

### Placement rules
- 2–6 per yard in a loose row 4–7 m apart along a fence or behind the house, 2–4 m from the nearest wall (C); one or two per yard in the map's villages.

### Generator parameters
Preset: `height_m` 5.5 (4–7), `dbh_m` 0.30, `first_fork_m` 1.5, `main_limbs` 4, `orders` 5, `children_per_order` 2.8, `fork_angle_deg` 45–70, `limb_droop` 0.4, `trunk_sinuosity` 0.6, `crown_shape` spreading, `crown_width_m` 6, `water_sprouts` 5–15 (the younger variant 0–5, assumed), `fruit_share` 0.05 in late summer ("a few fruit cards", assumed).

### Open points
- None.

## V11 Yard fruit tree: cherry / apricot

**Seen in:** C (1–6: the smaller trees in the row behind the apple fit the class). **R** for the species detail.
**Research sources:** https://en.wikipedia.org/wiki/Prunus_cerasus (sour cherry 4–10 m, twiggy); https://en.wikipedia.org/wiki/Prunus_armeniaca (apricot 8–12 m, trunk to 40 cm, dense spreading crown, leaves 5–9 × 4–8 cm).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height, cherry / apricot | 3–5 m / 4–7 m | 4 / 6 m | published (4–10 m; 8–12 m) capped by measured yard trees 4–7 m (C) |
| trunk | 12–25 cm / 20–40 cm | 18 / 30 cm | published (apricot to 40 cm) + assumed |
| first fork | 0.8–1.5 m / 1.2–2.0 m | 1.2 / 1.6 m | assumed |
| crown width | 3–5 m / 5–8 m | 4 / 6 m | assumed (dense, spreading) |
| suckers at the base (cherry) | 0.5–2 m whips, 5–15 mm | 6 | assumed (sour cherry suckers freely) |
| leaf | cherry elliptic 5–8 cm; apricot ovate 5–9 cm | 6 / 7 cm | published (apricot), cherry assumed |

### Construction and materials
- Cherry: a short trunk, red-brown bark with horizontal lenticel bands, peeling in rings (assumed from the genus), many thin drooping twigs (`limb_droop` 0.6), a ring of root suckers; apricot: a stouter trunk, dark grey-brown rough bark, a dense rounded crown, limbs breaking under their own weight when old.
- **Materials:** cherry bark: Poly Haven `sakura_bark` (a Prunus bark with lenticels), apricot bark: `trident_maple_bark` with a dark tint; leaves: a LeafSet with elliptic serrated leaves.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| cherry bark | #6a4a3e (assumed, red-brown) | — | assumed |
| apricot bark | #5a5047 (assumed) | #6b5c41 (C 2, yard-tree bark class) | measured on C |
| leaf, summer | #4a6a2c (assumed) | #786f53 (C 2, autumn row) | measured on C |

### Variants and states
- Variants: cherry (with suckers), apricot. States as V10.

### Damage pattern
- As V10; apricot limbs split at the crotch.

### Physics role
- As V10 at a smaller scale: crown shell porosity 0.4 summer (dense), 0.85 bare; the sucker ring is a 1–2 m thicket of 5–15 mm whips (`twig` contact) around the trunk at leg height: a snag on landing in the yard.
- **Gaps:** under the crown 1–2 m: passable nose-first only where the clear height ≥ 0.6 m above the suckers.
- **Landable surfaces:** none.

### Placement rules
- Mixed with V10 in the yard row; cherry often along the fence; apricot nearer the house (assumed).

### Generator parameters
Cherry preset: `height_m` 4, `dbh_m` 0.18, `first_fork_m` 1.2, `main_limbs` 4, `children_per_order` 3.0, `limb_droop` 0.6, `crown_shape` oval, `crown_width_m` 4, `suckers` 6 (range 0–10). Apricot preset: `height_m` 6, `dbh_m` 0.30, `first_fork_m` 1.6, `main_limbs` 4, `fork_angle_deg` 40–60, `crown_shape` dome, `crown_width_m` 6.

### Open points
- None.

## V12 Walnut (large yard tree)

**Seen in:** **R**. S 47–50: the largest rounded crown beside a house (S 48–50) fits the class; the species is not readable.
**Research sources:** https://en.wikipedia.org/wiki/Juglans_regia (10–25 m, trunk to 2 m, smooth olive-brown bark young, silvery-grey with broad fissures old, broad crown on a short trunk, leaves 20–45 cm with 5–9 leaflets 10–20 cm, slow-growing, over 200 years).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 10–15 m | 12 m | published (10–25 m) within the asset list's 10–15 m |
| trunk | 40–80 cm | 55 cm | published (cultivated trunks over 2 m exist; a 40–80-year yard tree assumed) |
| first fork | 1.5–3 m | 2 m | published ("short trunk") |
| crown width | 10–15 m | 12 m | published (about 15 m cultivated) |
| main limbs | 3–5, 20–35 cm | 4 | assumed |
| leaf | pinnate 20–45 cm, 7 leaflets 10–20 × 6–10 cm | 30 cm | published |
| nuts | 3–4 cm, in the litter | — | assumed |

### Construction and materials
- A short massive trunk, silvery-grey bark with broad shallow fissures, limbs spreading at 40–60° into a wide open dome; large pinnate leaves (the biggest leaf cards in the group), dropping brown-black in autumn; a thick leaf litter under it (T6).
- **Materials:** bark: Poly Haven `bark_brown_02` with a silver-grey tint and a broad-fissure normal map; leaves: a LeafSet with large elliptic leaflets, yellow-brown autumn tint.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| bark | #8a877e (assumed, silver grey) | — | published |
| leaf, summer | #5a7a34 (assumed, yellow-toned green) | — | published |
| litter | #6a5a44 (assumed) | #a67f4d (C, leaf litter, terrain.md T6) | measured on C |

### Variants and states
- One variant; states summer, autumn, bare; damaged → V07 (a limb 20–35 cm torn out leaves a 0.5 m wound).

### Damage pattern
- A heavy limb torn off at the crotch, the wound showing pale wood; the fallen limb is a V21-class piece (4–6 m, 20–30 cm).

### Physics role
- **Collision:** trunk capsule 2 m; limbs 20–35 cm; branches ≥ 10 mm (about 200–400 segments, assumed); crown shell `twig`.
- **Material ids:** `bark`, `twig`/`foliage`.
- **Gaps:** under the crown 2–4 m clear over 10–15 m: the biggest passable canopy in a yard; openings between the limbs 1–2 m: passable.
- **Wind volumes:** crown porosity 0.45 summer, 0.85 bare (assumed): the yard's main wind shadow.
- **Snag hazards:** as V10.
- **Landable surfaces:** the fork saddle at 2 m (0.4–0.6 m across): all four legs can fit on a big walnut, the drone sits tilted 5–15°.

### Placement rules
- One per two or three yards, near the gate or in front of the house, 3–6 m from the wall (assumed, typical village planting).

### Generator parameters
Preset: `height_m` 12, `dbh_m` 0.55, `first_fork_m` 2, `main_limbs` 4, `orders` 5, `children_per_order` 2.4, `fork_angle_deg` 40–60, `crown_shape` dome, `crown_width_m` 12.

### Open points
- None.

## V13 Belt understory shrub

**Seen in:** H (1–21: the continuous bush layer 1.5–3 m in front of and under the trees, thicker at the edge), F (1–12: green shrubs 1–2 m in the mid-ground under the canopy), G (1–15: shrubs 1–2 m beside the trench, the last stills inside one). PR-10. S 25–27, 38–41: the same kind of shrub standing alone on a village green, rounded clumps about 2–3 m across, one turning yellow (S 25–26), a free-standing shrub or young tree every 15–25 m (S 24–40, rough).
**Research sources:** https://en.wikipedia.org/wiki/Lonicera_tatarica (Tatar honeysuckle to about 3 m, leaves 3–6 cm, forms thickets and a dense understory, orange or red berries); https://en.wikipedia.org/wiki/Caragana_arborescens (peashrub 2–6 m, a standard shelterbelt shrub, pods twist open); https://en.wikipedia.org/wiki/Elaeagnus_angustifolia (silverberry to 11 m, thorns 2–7 cm, silvery leaves). The shrub-layer mix (honeysuckle, caragana, silverberry, box elder) and its role (planted to help the main trees and shade out weeds) are assumed from common practice.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height, variant 1 low / 2 mid / 3 tall | 1–1.5 / 1.5–2.5 / 2.5–3.5 m | 1.2 / 2 / 3 m | measured (H 1–21: 1.5–3 m against the pole and the trunks; F; terrain.md V1 understory 1–3 m) |
| width | 1–2 / 1.5–3 / 2–3 m | 1.5 / 2.2 / 2.5 m | measured (H: clumps 1.5–3 m wide) |
| stems from the base | 5–20 | 10 | assumed (honeysuckle, caragana: multi-stem) |
| stem diameter at the base | 10–30 mm | 18 mm | assumed (3 m shrub stems) |
| branching from | 0.3–1 m up (variant 1), 1 m up (variant 3, leggy under the canopy) | 0.6 m | measured (terrain.md V1, F, H: branches from about 1 m up) |
| twigs | 3–8 mm, dense | — | measured (H 13–21: a fine mesh) |
| leaf | 2–6 cm (honeysuckle oval; caragana pinnate with 8–12 leaflets 1–2 cm) | 4 cm | published / assumed |
| spacing between clumps inside the belt | 1–4 m, gaps 0.5–1.5 m | 2.5 m | derived from PR-10 and H (no see-through at 1–2 m height) |

### Construction and materials
- Species mix as a look, not a label: Tatar honeysuckle and caragana (upright, many thin stems, small leaves), silverberry (grey-silver leaves, thorns) and box-elder saplings (V16). One generator, three size variants; a leafy card mass over a stem skeleton; ≥ 10 mm stems as geometry.
- Variant 1 (low, dense, edge): stems to the ground, leaves to the ground. Variant 2 (mid): the H edge clump. Variant 3 (tall, leggy, inside): bare stems 1 m, leaves above, under the canopy.
- **Materials:** stems: Poly Haven `bark_brown_02` with a grey-green tint; leaves: a LeafSet with small oval leaves (honeysuckle) and one with small leaflets (caragana); autumn tints yellow (honeysuckle red berries as 1-card tufts, derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| foliage, summer, overcast | #58602c (assumed) | #817651 (H 9) | measured on H |
| foliage, backlit | — | #6a4d33 (D 4, thicket class) | measured on D |
| stems | #7a7466 (assumed) | — | assumed |
| silverberry foliage | #9aa08a (assumed, silver-green) | — | published (silvery) |

### Variants and states
- Variants 1–3 by size and legginess; states summer, autumn (yellow, `leaf_cover` 0.5), bare (0.1 with the twig mesh still dense: porosity rises only to 0.7), dry-dead (brown, V08 variant 2 look at shrub scale).

### Damage pattern
- Shrubs are not broken by fragments in a readable way; they burn in field fires (char at the base, bare black twigs: `char_height_m` 0.5–1.5).

### Physics role
- **Collision:** stems ≥ 10 mm as capsules (10–20 per clump); the leafy mass as a convex shell with `twig` contact (k 10–50 N/m): the drone pushes 0.2–0.5 m into a clump before the stems stop it.
- **Material ids:** `bark` on stems, `twig` on the shell.
- **Gaps:** between clumps 0.5–1.5 m: passable only where ≥ 0.574 m nose-first; the edge wall in H has none at 1–2 m height (PR-10). The generator does not write gaps inside a clump (none ≥ 0.5 m).
- **Wind volumes:** clump porosity 0.35 summer, 0.5 autumn, 0.7 bare (assumed); at belt level this is what makes the lower profile dense (the belt construction classes, V01).
- **Snag hazards:** the whole twig mesh (3–8 mm twigs: the fiber's worst case) and the stems at leg height.
- **Landable surfaces:** none (the shell gives way, the legs drop through).

### Placement rules
- Inside every belt among the trunks (PR-10): 15–30 % ground cover inside, 40–70 % along the sunlit edges, variant 3 inside, 1 and 2 at the edges; clumps 1–4 m apart; none in the bare corridor of F-type belts (blow-through) beyond the margins. Along trenches (G) and at belt ends.

### Generator parameters
Over the shrub generator: `height_m` 1.2/2/3, `width_m` 1.5/2.2/2.5, `stems` 5–20, `stem_d_mm` 10–30, `arch` 0.1–0.3, `bare_base_m` 0 / 0.6 / 1.0, `leaf_cover` by state, `spines` true for the silverberry look, `fruit_tufts` 0–10 (honeysuckle berries in autumn; count assumed).

### Open points
- Species share per belt (honeysuckle vs caragana vs silverberry) is a look choice for the look-lock (G1); nothing in the footage separates them at PAL resolution.

## V14 Elder / lilac clump

**Seen in:** **R**; the dense green clumps 2–3 m at the yard edges in D (1–12) fit the class. S 46–50: front gardens grown into thickets of shrubs 2–4 m under the yard trees, 60–90 % covered (by eye).
**Research sources:** https://en.wikipedia.org/wiki/Sambucus_nigra (6–10 m, hollow young stems, coarse yellowish-grey deeply furrowed bark with lenticels, pinnate leaves 10–30 cm with 5–7 leaflets, black berries in drooping clusters, waste ground); https://en.wikipedia.org/wiki/Syringa_vulgaris (6–7 m, multi-stemmed, suckering into a clonal thicket, stems to 20 cm, grey furrowed flaking bark, heart-shaped leaves 4–12 cm, near past or present habitation: a lilac thicket marks a vanished house).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height, elder / lilac | 2–4 m / 2–4 m | 3 / 3 m | published (6–10 m; 6–7 m) capped by the asset list's 2–4 m for yard clumps |
| width | 2–4 m / 2–5 m | 3 / 3.5 m | assumed |
| stems, elder / lilac | 5–15 arching, 20–60 mm / 10–40 upright, 20–120 mm (old ones to 200 mm) | 8 / 20 | published (lilac stems to 20 cm) + assumed |
| elder leaf | pinnate 10–30 cm, 5–7 leaflets 5–12 × 3–5 cm | 20 cm | published |
| lilac leaf | heart-shaped 4–12 × 3–8 cm | 8 cm | published |
| berries (elder) | 5–6 mm, in clusters 10–15 cm, late season | — | published |

### Construction and materials
- Elder: arching hollow stems from a short base, corky furrowed grey-brown bark with lenticels, big pinnate leaves, drooping berry clusters in autumn; it grows at fence lines, at manure spots and against ruined walls (nitrogen).
- Lilac: a thicket of upright suckers, the old stems 100–200 mm with grey flaking bark, a dense mass of heart-shaped leaves, bare stiff twigs in winter; marks an old house plot even when the house is gone.
- **Materials:** elder bark: Poly Haven `bark_brown_01` with a pale grey-yellow tint; lilac bark: `chinese_hackberry_bark` grey; leaves: LeafSets with pinnate leaflets (elder) and heart-shaped leaves (lilac); berry clusters: a 1-card tuft (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| elder foliage | #4e6a2c (assumed) | #6a4d33 (D 4, thicket backlit) | measured on D |
| lilac foliage | #5a7a3a (assumed, light green) | — | published (light green to glaucous) |
| elder bark | #8a8470 (assumed) | — | published |
| lilac bark | #7a756d (assumed) | — | published |

### Variants and states
- Variants: elder (arching), lilac (upright thicket). States summer, autumn (elder with berries, lilac yellow-green), bare.

### Damage pattern
- Fragment-cut stems hang and resprout; a burnt clump is bare black stems with green regrowth at the base.

### Physics role
- **Collision:** stems ≥ 10 mm as capsules (elder 5–15, lilac 10–40); shell with `twig`.
- **Material ids:** `bark`, `twig`.
- **Gaps:** none inside; between the clump and a wall 0.3–1 m: passable only where ≥ 0.574 m.
- **Wind volumes:** porosity 0.3 summer (lilac), 0.4 (elder), 0.75 bare (assumed).
- **Snag hazards:** the whole mass; elder's hollow stems snap at the drone's push (treat stems under 20 mm as `deadwood`-like breakable, an open point).
- **Landable surfaces:** none.

### Placement rules
- At fences, gates and the corners of houses and sheds, 1–3 clumps per yard; a lilac thicket on every abandoned house plot (assumed, published habit); elder beside the cellar and the shed (C, E class yards).

### Generator parameters
Elder preset: `height_m` 3, `width_m` 3, `stems` 8, `stem_d_mm` 20–60, `arch` 0.7, `leaf_cover` 0.8, `fruit_tufts` 0–20 (berries, autumn). Lilac preset: `height_m` 3, `width_m` 3.5, `stems` 20, `stem_d_mm` 20–120, `arch` 0.1, `leaf_cover` 0.9.

### Open points
- A hollow elder stem breaks under the drone: settled by the physics sign-off (review-materials.md §2): `deadwood`'s row applies, whose solid-section formula overstates a half-diameter pith by about 7 %, inside the σ_b spread.

## V15 Blackthorn / rose-hip thicket

**Seen in:** **R**.
**Research sources:** https://en.wikipedia.org/wiki/Prunus_spinosa (to 6 m, blackish bark, dense stiff spiny branches that tangle, leaves 2–4.5 cm, sloes 10–12 mm blue-black in late autumn); https://en.wikipedia.org/wiki/Rosa_canina (1–5 m, arching stems, hooked prickles, 5–7 leaflets, hips 1.5–2 cm red-orange, persisting for months).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 1–2.5 m | 1.8 m | published (to 6 m; 1–5 m) within the asset list's 1–2.5 m |
| width | 1.5–4 m | 2.5 m | assumed (suckering thicket) |
| stems | 10–40 (blackthorn) / 5–15 arching canes (rose) | 20 / 8 | assumed |
| stem diameter | 8–30 mm / 8–20 mm | 15 / 12 mm | assumed |
| spines | 2–5 cm stiff (blackthorn) / 5–8 mm hooked (rose) | — | assumed sizes, published kinds |
| hips | 1.5–2 cm | 1.8 cm | published |
| sloes | 10–12 mm | 11 mm | published |

### Construction and materials
- Blackthorn: a dense tangle of stiff blackish twigs at 60–90° angles, spur shoots as thorns, small leaves, blue-black sloes in autumn, bare in winter and still opaque.
- Rose: arching canes 1–3 m from a base, hooked prickles, red hips held through winter (the most visible colour in a bare thicket).
- **Materials:** blackthorn bark: ambientCG `Bark006` dark; rose canes: `Bark009` smooth with a green-brown tint; leaves: small oval LeafSet; hips and sloes: 1-card tufts (derived).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| blackthorn twigs | #3a3a38 (assumed, blackish) | — | published |
| rose canes | #6a6a4a (assumed) | — | assumed |
| hips | #c83a1e (assumed) | — | published (red-orange) |
| foliage | #4a6a2e (assumed) | #817651 (H 9, bush class) | measured on H |

### Variants and states
- Variants: blackthorn, rose. States summer, autumn with fruit, bare with hips/sloes.

### Damage pattern
- None readable; burns to bare black canes.

### Physics role
- **Collision:** stems ≥ 10 mm as capsules; the tangle shell with `twig` contact but stiffer (k 50 N/m, upper band): blackthorn stops the drone where honeysuckle gives way.
- **Material ids:** `bark`, `twig`.
- **Gaps:** none.
- **Wind volumes:** porosity 0.3 summer, 0.6 bare (the twig tangle stays dense; assumed).
- **Snag hazards:** the worst fiber snag in the group after V13: hooked prickles and stiff thorns on every stem (visual cards; the snag is the capsule count and the twig shell's hook rate, K§7.3).
- **Landable surfaces:** none.

### Placement rules
- Belt edges facing the sun, field corners, along old fences and ditch banks; 1 per 20–50 m of belt edge (assumed).

### Generator parameters
Blackthorn preset: `height_m` 1.8, `width_m` 2.5, `stems` 20, `stem_d_mm` 8–30, `arch` 0.1, `spines` true, `fork_angle_deg` 60–90, `fruit_tufts` 10–30 (sloes in autumn; count assumed). Rose preset: `height_m` 1.5, `stems` 8, `stem_d_mm` 8–20, `arch` 0.8, `spines` true, `fruit_tufts` 10–40 (hips).

### Open points
- None.

## V16 Self-seeded saplings

**Seen in:** D (1–14: thickets of leafy saplings 1.5–3 m among the forbs), H (7–12: young whips at the clearing edge, one dead with brown leaves), F (1–3: suckers at the belt margin). S 37–43: thin young trees 2–4 m scattered over a village square and green; S 17–19: saplings at the edge of a flattened plot.
**Research sources:** https://en.wikipedia.org/wiki/Acer_negundo (box elder, the commonest self-seeded tree in disturbed ground); https://en.wikipedia.org/wiki/Robinia_pseudoacacia (root suckers, spiny, in dense colonies).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 1–3 m | 2 m | measured (D 1–14: 1.5–3 m against the dead tree and the 0.5–1.5 m forbs; terrain.md T7 saplings 1–3 m) |
| stem diameter at the base | 10–40 mm | 20 mm | assumed (1–4-year-old whips) |
| stems per sapling | 1–3 | 1 | measured (D: single whips in groups) |
| side twigs | 3–10 mm, 0.3–1 m | — | assumed |
| leaf, sapling-sized | box elder 8–15 cm leaflets; locust 20–30 cm pinnate | — | published (sapling leaves run large) |
| group density | 2–10 per m² in a thicket, groups 2–6 m across | 5 per m² | measured (D: thickets 2–5 m across) + assumed density |

### Construction and materials
- A single whip (or a small fork) with a few side twigs, oversized leaves, no crown shape yet. Two variants: box elder (green stems, pinnate leaves) and locust sucker (zigzag, thorny, pinnate). The dead sapling with brown leaves is V08 variant 2.
- **Materials:** stems: ambientCG `Bark009` smooth with a green-grey tint; leaves: the parent species' LeafSet cards at 1.5× size.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| foliage, backlit | #5a7a34 (assumed) | #6a4d33 (D 4) | measured on D |
| stems, green | #7a9a4a (assumed, box elder) | — | published |

### Variants and states
- Variants: box elder, locust sucker. States summer, autumn yellow, bare, dead-brown (V08 variant 2).

### Damage pattern
- None; they are the regrowth after damage (the "after" state of a shelled belt edge).

### Physics role
- **Collision:** the stem as one or two capsules (10–40 mm); side twigs ≥ 10 mm as capsules; the leaf mass as a thin `twig` shell.
- **Material ids:** `bark`, `twig`.
- **Gaps:** between saplings in a thicket 0.2–0.6 m: not passable; between groups 1–3 m: passable.
- **Wind volumes:** a thicket as one volume, porosity 0.5 summer, 0.85 bare (assumed).
- **Snag hazards:** the whips bend and spring back (k ≈ 30–200 N/m at 1–2 m, derived from the table above): they catch the legs and flip the drone on landing in a plot (K§7.3 hooks).
- **Landable surfaces:** none.

### Placement rules
- Every abandoned plot (D) in groups 2–6 m across among the forbs; belt edges (F margins) as sucker fringes 1–3 m wide; along fences and at ruined walls.

### Generator parameters
Over the shrub generator: `height_m` 1–3, `stems` 1–3, `stem_d_mm` 10–40, `arch` 0–0.2, `leaf_cover` 0.9, `spines` true for the locust variant, groups placed by the map's density field (5 per m², 2–6 m patches).

### Open points
- None.

## V17 Tall weeds: mixed forbs

**Seen in:** D (1–14: the dominant cover, 0.5–1.5 m, green-yellow with seed heads), C (1–6: dry yellow-brown weeds 0.5–1 m along the cellar wall), E (per the asset list; terrain.md T7: patches along the walls). Also the margins in B and the standing stems in H (13–21). S 1–16: an old fallow and abandoned back plots as a closed stand 0.5–1.2 m, grass-dominated with pale feathery heads (read as wood small-reed, assumed: it takes old fallows in Ukraine, https://ecology.dp.ua/index.php/ECO/article/view/1064 ) broken by patches of rust-brown, dock-like seed stands; S 43–48: a weed fringe 0.5–1.5 m wide along the fences and 1–3 m at the road edge, goldenrod-like yellow tops in flower among rust-brown stalks.
**Research sources:** https://en.wikipedia.org/wiki/Artemisia_vulgaris (mugwort to 1.5 m, purplish angular stems, dark green leaves white below, waste places); https://en.wikipedia.org/wiki/Ambrosia_artemisiifolia (ragweed to 0.7 m, much-branched; https://zaxid.net/ambroziya_chim_nebezpechna_yak_borotisya_koli_znishhuvati_poradi_n1640701 : on roadsides, waste ground, yards and abandoned plots); https://en.wikipedia.org/wiki/Chenopodium_album (fat-hen 0.1–1.5 m, mealy leaves, recumbent after seeding, nitrogen-rich wasteland); https://en.wikipedia.org/wiki/Solidago_canadensis (goldenrod 0.6–1.8 m, dense clonal stands, yellow plumes in late summer, invasive in Europe); https://en.wikipedia.org/wiki/Cirsium_arvense (creeping thistle 0.3–1.5 m, clonal colonies, spiny leaves 15–20 cm); https://en.wikipedia.org/wiki/Ruderal_species (ruderals are the first to colonise disturbed land, abandoned fields among it). Tall perennial ruderals as the abandoned-yard cover is assumed.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 0.5–1.5 m | 1.0 m | measured (D 1–12 against the 1.5–3 m saplings and the prop line; terrain.md T7) |
| stalk diameter | 5–15 mm | 8 mm | measured (terrain.md T7, D) |
| stalks per m² | 10–60 | 30 | assumed (a dense late-summer stand) |
| branching | from 0.3–0.6 m up, 3–8 side branches 3–6 mm | 5 | published (mugwort, ragweed much-branched) |
| seed heads / plumes | 0.1–0.4 m at the top, 20–40 % of stalks in late summer, 80 % in autumn | — | measured (D: seed heads on many tops; C: brown heads) |
| leaf | 5–20 cm, pinnate (mugwort, ragweed) or simple (goldenrod, fat-hen) | — | published |
| patch | fills a plot 10–40 m; strips 0.5–2 m along walls | — | measured (D; C and E strips, terrain.md T7) |

### Construction and materials
- A stalk skeleton (5–15 mm, ≥ 10 mm as thin capsules within 3 m of the drone, else instanced visuals, terrain.md §7) with leaf cards along it and a seed-head card at the top; a species mix by cards, not by asset: mugwort (grey-green, white undersides), goldenrod (yellow plumes), ragweed (fine fern-like), fat-hen (mealy, red-streaked stems), thistle (spiny, purple heads then white fluff), plus a grass matrix (couch grass and brome 0.5–1.2 m, terrain.md T4 standing stems).
- Three variants: (1) green late summer with plumes (D), (2) autumn yellow-brown with brown seed heads (C), (3) dry winter stalks (grey-brown, leaves gone, 30 % lodged).
- **Materials:** leaf and head cards: LeafSets with pinnate and simple leaves (tinted), seed-head cards derived (plume, fluff, brown head); stalks: a 1-card strip texture with a purplish-green to grey-brown tint.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| stand, green late summer, backlit | #7d7a3a (assumed) | #86723e (D 4); terrain.md #817831 | measured on D |
| stand, autumn, dry heads, sunlit | #8a7a52 (assumed) | #695738 (C 2) | measured on C |
| stand, dry winter | #a08a5a (assumed) | #797258 (H, straw class, terrain.md T4) | measured on H |
| goldenrod plume | #d8b028 (assumed) | — | published (yellow) |

### Variants and states
- Variants 1–3 above (`leaf_cover`, `seed_heads`, `dry`); a trampled state (a path 0.5–1 m through the stand, stalks lodged) as a map-level cut, not an asset state.

### Damage pattern
- Blast flattens a stand radially 2–6 m around a crater (lodged stalks pointing outward, assumed); fire leaves black stubs 5–15 cm (terrain.md T3b).

### Physics role
- **Collision:** none on the asset; stalks within 3 m of the drone are micro-detail cantilevers of the surface's `weeds` cover (`MicroDetailNear`, AST-10; k 1–3 N/m, buckle at ≈ 1 N, derived above), so the drone wades through a stand at a drag of tens of N when 10–30 stalks touch it; the legs go deep (not a landing surface, terrain.md T7); hooks on the legs per K§7.3.
- **Material ids:** none (`forb_stem` was declined by the physics sign-off, review-materials.md §2: micro-detail has no asset id).
- **Gaps:** none; the stand is passable everywhere at a cost.
- **Wind volumes:** the stand as a ground layer, porosity 0.6 green, 0.8 dry (assumed): the surface's cover height (terrain.md §8 `weeds` 0.5–1.5 m).
- **Snag hazards:** the fiber drags through the stalks (capstans of 2.5–7.5 mm radius, hundreds per metre); seed-head cards are visual only.
- **Landable surfaces:** none (legs go deep; rubble hides underneath, terrain.md T7).

### Placement rules
- Fills every abandoned plot (D), 10–40 m patches with feathered edges (PR-11); strips 0.5–2 m along every wall, fence and cellar (C, E); belt margins 1–3 m (B, H); not inside dense belts (the floor is bare there: F, terrain.md T5) but along the F corridor margins.

### Generator parameters
Over the forb generator: `height_m` 0.5–1.5, `stem_d_mm` 5–15, `density_per_m2` 10–60, `seed_heads` 0.2–0.9, `leaf_cover` 0.9/0.5/0, `dry` by variant, `patch_shape` from the map; `species_mix` mugwort 0.3, goldenrod 0.2, ragweed 0.2, fat-hen 0.15, thistle 0.15 (a look parameter).

### Open points
- Stalk density per m² is assumed; the first 1.5 m review sheet against D decides it.
- The lodged-by-blast pattern is assumed; the earthworks analyst (B, task 1.3) may see it at the craters.

## V18 Burdock, nettle, thistle with seed heads

**Seen in:** **R**; the large-leaved clumps at the foot of the cellar wall in C (5–8) fit the class.
**Research sources:** https://en.wikipedia.org/wiki/Arctium_lappa (burdock to 3 m biennial, large wavy basal leaves, hooked burrs); https://en.wikipedia.org/wiki/Urtica_dioica (nettle 0.9–2 m, spreading rhizomes, leaves 3–20 cm, on nutrient-rich disturbed soil by habitation); https://en.wikipedia.org/wiki/Cirsium_arvense (thistle, spiny leaves, pink-purple heads 10–22 mm, feathery pappus).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| burdock height / basal leaf / stalk | 1–2 m / 0.3–0.6 m / 15–30 mm | 1.5 m / 0.45 m / 20 mm | published (to 3 m), leaf and stalk assumed |
| burr clusters | 20–40 mm burrs, 10–50 per stalk | — | assumed |
| nettle height / stalk / patch | 0.9–1.5 m / 5–8 mm / 1–5 m clonal patches | 1.2 m / 6 mm / 3 m | published (0.9–2 m, rhizomes), stalk assumed |
| thistle height / stalk | 0.5–1.5 m / 5–10 mm | 1 m / 7 mm | published (0.3–1.5 m), stalk assumed |
| density | burdock 2–6 per m², nettle 20–50, thistle 5–20 | — | assumed |

### Construction and materials
- Burdock: a rosette of huge basal leaves (geometry-sized cards 0.3–0.6 m) and a thick branched stalk (≥ 10 mm: a capsule) with burr clusters; after seeding, a dry brown stalk with burrs that stay all winter.
- Nettle: a dense patch of straight stalks with opposite leaves, dark green, seed strings in late summer.
- Thistle: spiny grey-green leaves, purple heads, then white fluff.
- Two variants: leafy (late summer) and dry with seed heads (autumn to winter).
- **Materials:** large-leaf LeafSet cards (burdock), serrated opposite leaves (nettle), spiny lobed leaves (thistle); burr, head and fluff cards derived.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| burdock leaf, top / underside | #4e6a30 / #8a9a7a (assumed) | — | published (large wavy leaves) |
| nettle patch | #3e5c28 (assumed, dark green) | — | published |
| dry burdock stalk and burrs | #7a6648 (assumed) | #695738 (C 2, dry weeds) | measured on C |

### Variants and states
- Leafy, dry (the burr stalks and nettle stems stay standing into winter; `dry` true).

### Damage pattern
- As V17.

### Physics role
- **Collision:** none on the asset, as V17: burdock stalks (15–30 mm) are micro-detail cantilevers too, at the stiff end (k ≈ 10–30 N/m derived for d 20 mm, E 2 GPa, L 1 m); nettle and thistle as V17.
- **Material ids:** none, as V17.
- **Gaps:** none.
- **Wind volumes:** as V17.
- **Snag hazards:** burrs hook the fiber (visual cards; physically the burr stalk is a capstan 10–15 mm with a higher hook rate, K§7.3); nettle stalks drag.
- **Landable surfaces:** none.

### Placement rules
- Nitrogen spots: shed and barn walls, manure heaps, fence lines, the cellar wall (C), around rubble; nettle patches 1–5 m; burdock in groups of 3–10 at shed corners.

### Generator parameters
Over the forb generator: burdock `height_m` 1.5, `stem_d_mm` 15–30, `density_per_m2` 2–6, `basal_leaf_m` 0.45 (0.3–0.6), `fruit_tufts` 10–50 (burrs); nettle `height_m` 1.2, `stem_d_mm` 5–8, `density_per_m2` 20–50; thistle `height_m` 1, `density_per_m2` 5–20, `spines` true; `seed_heads` 0.8 and `dry` for variant 2.

### Open points
- Burdock's 15–30 mm stalks are thicker than the `weeds` cover's 5–15 mm stems (K§7.3): where V18 grows, the cover needs a burdock stem band for its micro-detail; for the world-builder and the physics engineer at V18's asset change.

## V19 Fallen branch set

**Seen in:** F (13–17: dead branches lying across the pit and the ground around it), H (13–21: twigs and small branches in the straw). Also J (brush, V20).
**Research sources:** none needed; sizes from the letters and terrain.md V4.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| piece 1, forked limb | 3–4 m, 6–8 cm, two forks | 3.5 m, 7 cm | measured (F 15–17: the longest pieces span the 2–3 m pit; terrain.md V4 1–4 m, 2–8 cm) |
| piece 2, straight branch | 1.5–2.5 m, 3–5 cm | 2 m, 4 cm | measured (F 13–17) |
| piece 3, twiggy top | 1–2 m, 2 cm, with a twig fan 0.5–1 m | 1.5 m | measured (F 15–16; H 13–21) |
| piece 4, short stub | 0.5–1.2 m, 6–10 cm | 0.8 m, 8 cm | assumed |
| twigs on the pieces | 5–20 mm, 20–50 % still attached | — | measured (F: the lying branches keep side twigs) |
| bark | 30–80 % gone | 50 % | assumed |

### Construction and materials
- Four dead branch pieces from the dead-wood generator with `lying` true: the skeleton of a V08 limb, bark partly off, side twigs ≥ 10 mm as geometry, finer twigs as cards; laid with 2–3 ground contacts and the rest in the air (a 3.5 m fork spans a pit, F).
- **Materials:** `Bark015` grey-weathered wood; the parent bark on the remaining strips.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| dead wood, lying | #8f877a (assumed) | #705c4a (D 4, dead wood class) | measured on D |
| in the straw | — | #797258 (H, straw, terrain.md T4) | measured on H |

### Variants and states
- The four pieces; each with a seed for the twig draw; a charred state (`char_height_m` as a char share) at burnt edges.

### Damage pattern
- The pieces are the damage; a piece under the drone snaps if under 15 mm (`deadwood`).

### Physics role
- **Collision:** capsules along the piece and its twigs ≥ 10 mm (5–30 per piece).
- **Material ids:** `deadwood`.
- **Gaps:** under a piece bridging a pit: a slot 0.2–1 m high: passable nose-first only where ≥ 0.574 m clear (rare); the generator writes it.
- **Wind volumes:** none.
- **Snag hazards:** every twig (the fiber's capstans), the legs on landing (uneven support, terrain.md V4).
- **Landable surfaces:** a 7–8 cm piece rolls under a leg (rigid roller, μ ≈ 0.05 across its axis, K§7.3); not a landing surface, an uneven-support hazard.

### Placement rules
- 2–5 pieces per 100 m² under belts (F), piled at pits and craters, scattered in the straw at belt edges (H); 1–3 per yard under the fruit trees and the walnut.

### Generator parameters
Over the dead-wood generator: `lying` true, `length_m` 0.5–4, `butt_d_m` 0.02–0.10 (`tip_d_m` 0.01–0.04), `forks` 0–2, `twig_share` 0.2–0.5, `bark_loss` 0.3–0.8, `char_share` 0–1; the four pieces of the dimension table are four params files.

### Open points
- None.

## V20 Brush pile

**Seen in:** J (per the asset list and terrain.md V4: piles of cut branches in a yard; not re-read here).
**Research sources:** none needed; sizes from terrain.md V4.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| height | 1–2 m | 1.5 m | measured (terrain.md V4, J) |
| footprint | 2–4 m | 3 m | measured (terrain.md V4, J) |
| pieces | 20–60 cut branches 1–3 m, 2–6 cm, with twigs | 40 | assumed |
| void fraction | 60–80 % | 70 % | assumed (loosely thrown) |

### Construction and materials
- 20–60 V19-class pieces thrown loosely into a mound, twigs on; two variants: fresh-cut with dry brown leaves still on (a pruned yard), and old grey (leafless, settled lower, 1 m).
- **Materials:** as V19; brown-leaf LeafSet cards for the fresh variant.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| grey old pile | #8a8275 (assumed) | — | assumed |
| fresh pile with brown leaves | #8a6a44 (assumed) | #b78b52 (H 9, dry brown leaves) | measured on H |

### Variants and states
- Fresh, old; a burnt pile (a black ring 2–4 m with ash, charred stubs) as a third state.

### Damage pattern
- None; a pile collapses when a prop cuts a supporting piece (visual only in phase 1).

### Physics role
- **Collision:** the outer pieces as capsules (the inner ones culled), 100–300 capsules; a convex hull with `twig` contact at k 20–50 N/m for the drone's push, the capsules for the fiber.
- **Material ids:** `deadwood`, `twig` for the hull.
- **Gaps:** none passable.
- **Wind volumes:** porosity 0.7 (assumed).
- **Snag hazards:** the whole pile: legs and fiber.
- **Landable surfaces:** none (springy, snagging: the drone settles tilted and hooked).

### Placement rules
- Yards (J): 1–2 per yard at the back fence or beside the shed; at belt ends where a belt was cleared.

### Generator parameters
A pile generator that throws V19-class pieces from the dead-wood generator into a mound and settles them:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `seed` | int | any | per variant | every draw (piece sizes, throw, settling) |
| `height_m` | float | 1–2 | 1.5 (old variant 1.0) | mound height after settling |
| `width_m` | float | 2–4 | 3 | mound footprint |
| `pieces` | int | 20–60 | 40 | cut branches thrown in |
| `piece_length_m` | range | 1–3 | 1–3 | length of each piece (drawn) |
| `piece_d_m` | range | 0.02–0.06 | 0.02–0.06 | butt diameter of each piece (drawn) |
| `leaf_share` | float | 0–0.5 | 0.4 fresh, 0 old | share of twig tips with brown leaf cards |
| `void_fraction` | float | 0.6–0.8 | 0.7 | how loosely the pieces sit; sets the pile's mass and the hull's `twig` stiffness band |
| `burnt` | bool | — | false | the third state: a black ring 2–4 m with ash and charred stubs |

Only the outer pieces get capsules (the inner ones are culled, as the physics role says).

### Open points
- The houses-and-yard analyst (J, task 1.2) may add the pile's piece sizes from the photo.

## V21 Fallen log

**Seen in:** **R**; the leaning dead trees of D and G are its "before".
**Research sources:** none needed; a fallen V08.

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| length | 4–8 m | 6 m | derived (a 6–10 m V08 minus its lost top) |
| diameter at the butt / tip | 20–40 / 8–15 cm | 30 / 10 cm | derived (V08 trunk) |
| clearance under the log | 0–0.4 m where it rests on limbs | 0.15 m | assumed |
| root plate | 1–2 m across, 0.3–0.6 m thick, soil on it | 1.5 m | assumed (optional) |
| remaining limbs | 2–5 stubs 0.3–1.5 m, 3–8 cm | 3 | assumed |

### Construction and materials
- A V08 skeleton laid down: the trunk with limb stubs, bark half gone, the butt either snapped (a splintered end, V09-like) or torn out with a root plate; it rests on the stubs, so the trunk floats 0–0.4 m over the ground along part of its length.
- **Materials:** as V08; soil on the root plate from the surfaces sheet's belt_bare set.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| weathered trunk | #8f877a (assumed) | #705c4a (D 4) | measured on D |
| snapped butt, split wood | #9a9283 (assumed) | — | assumed |

### Variants and states
- One variant with `root_plate` on or off; a charred state at burnt edges.

### Damage pattern
- It is the end state of V08; around it, V19 pieces (its limbs) within 1–2 crown widths.

### Physics role
- **Collision:** trunk capsules per 1 m (tapering 30 → 10 cm), stub capsules, the root plate as a convex mesh.
- **Material ids:** `deadwood` (trunk, stubs), `timber` for the split butt, soil id from the surfaces sheet on the root plate.
- **Gaps:** under the floating trunk 0–0.4 m: not passable (< 0.574 m), but the fiber passes; the generator writes it as a non-passable gap.
- **Wind volumes:** none.
- **Snag hazards:** the stubs; the fiber draped over the trunk (a capstan of 0.1–0.15 m radius: low friction loss, the physics engineer's `cable`-like case).
- **Landable surfaces:** the top of the trunk, the one landable surface in this group (asset list: `land`). The log top is 0.3 m wide at the butt against the drone's 0.574 m prop line and the legs about 0.12 m from the centre (K§5.4): all four legs fit only on the butt third with the drone aligned to the log within about 10° (derived: a 0.24 m leg square on a 0.30 m crown); elsewhere two legs land on the log and two drop 0.2–0.4 m to the ground, so the drone tilts 20–40° and may slide off (μs 0.45). A mid-length touchdown on a log is the manifesto's uneven-landing case made hard.

### Placement rules
- 1 per 50–100 m of belt, lying across the corridor or into the field; 1 per shelled yard with a big tree; in G-type strips beside the trench.

### Generator parameters
Over the dead-wood generator: `lying` true, `length_m` 4–8, `butt_d_m` 0.20–0.40, `tip_d_m` 0.08–0.15, `stubs` 2–5, `root_plate` on/off, `bark_loss` 0.3–0.8, `splinter_len_m` 0.3–0.8 at the butt when the root plate is off.

### Open points
- None.

## Open points for other roles (summary)

- **Physics engineer (settled by the physics sign-off, review-materials.md §2, §3, §5):** `bark`, `twig` and `deadwood` added, `forb_stem` declined (micro-detail); hollow elder stems break; no physics limit on the capsules per tree (the budget is signed at the phase-3 world-query API review). The break moment is the new optional field `break_stress_pa`; the buckling load is derived from each stem's E, d and L; a crown's hooking comes from its snag records and the wind volume's porosity, so no hook-rate field.
- **Asset-artist:** fetch and pin the CC0 candidates (Poly Haven bark and ground sets, ambientCG Bark and LeafSet sets); measure the photo albedos on the chosen sets and replace the assumed values; check the Lombardy crown width and the locust crown ratio against CC0 photos.
- **World-builder:** belt-level porosity classes (blow-through, openwork, dense) come from the row layout plus V13 density, not from a per-asset value (K§10.6 noted the one-porosity-per-asset limit); the far-belt check: at 200 m a dark band (K§14.4), at about 1 km a shape 5–10 % darker than the field (A).
- **Other analysts:** B (task 1.3) can count broken tops per 100 m of belt edge and look for blast-lodged weeds at craters; J (task 1.2) can size the brush-pile pieces.
- **Pilot:** nothing. Every open point here is settled by research, the review sheets or a role decision.
