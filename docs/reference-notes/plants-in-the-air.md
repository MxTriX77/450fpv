# Plants in the air: clip H and the published numbers (w16-plants-in-the-air 1.1)

The reference for the plants that move in the sim's own air (1.2's law, 1.3's drawn plants, 1.4's cut pieces and dust, 1.5's drag on the drone). Clip H's ending comes first, as the pilot asked; then the classes the farmland draws, and per class the published bending, damping, drag and flutter numbers; then the rotors' wash on crops, the dust it lifts and the forces of vegetation on a body pushed through it.

- **The pilot's words** (the change's proposal): "they are flexible, so they should flexibly get back to their default position if a gust is short etc., again - calculate and make it realistic"; "about grass cutting, swaying because of propwash, dusts whiffs as you land closer to the ground - refer to [clip H] - towards the end you will see short examples".
- **Labels:** *measured* (from the frames, or printed by a tool named in §1.6), *published* (a cited source), *derived* (computed here, the rule shown), *assumed*. Each published row says what was read: **full**, **abstract**, **record** (a catalogue page) or **restated** (a later work citing the original, named).
- **OPSEC:** clips by letter and by time inside the clip only. No value was read from the on-screen display; no reference file name is written.
- **Companions:** [stem-properties.md](stem-properties.md) (the stems' published section, modulus, cutting and drag numbers: not repeated here), [vegetation-on-the-props.md](vegetation-on-the-props.md) (every stretch of H, D, A and F near the lens, and the props in view), [ground-micro-motion.md](ground-micro-motion.md) (the camera's motion near the ground), [terrain.md](terrain.md) (the reference set and the surface table), and the rotors' air in `openspec/changes/p7d-tether/design.md` decisions 19 and 20.

## 1. Clip H's ending

### 1.1 The view (REAL-02)

- **Scene:** a creep along the edge of a tree belt over dead vegetation: lodged dry grass and straw lying in mats, standing clumps of pale dead grass stalks up to about 1 m, a few green weed stalks, and patches of dark bare soil between the mats. The belt's trees stand behind with their leaves on.
- **Viewpoint:** the camera 0.3–0.6 m above the mats (assumed, ±50 %, as ground-micro-motion.md §1), level, 0° uptilt; the drone creeping forward and yawing.
- **Light and weather:** overcast, flat light, no shadows; dry vegetation, while the bare soil is dark and looks damp (assumed from its colour).
- **Its end:** the last live frame is at 20.91 s. The picture is lost at 20.94 s, and a blue screen fills the clip's last 3.6 s (to 24.5 s).
- **The drone's motion** (derived from `motion.csv`'s forward term, forward speed over depth per frame, times 30 frames a second and an assumed 2 m mean depth of the picture): 0.1–0.5 m/s over 16–20.5 s. In the last 0.4 s (f617–628) the term is 0.029 a frame, and the camera pitches 30° down and yaws 22° (measured, the frames' turns summed): the drone noses into the straw mat.

### 1.2 What the frames show

Read at 30 frames a second over 8.0–20.94 s: contact sheets of every frame and every third frame, and native-resolution crops of the moments below (§1.6). Sizes are at the recorder's 1920 px width; the feed itself resolves about 450 px across, so one feed pixel is about 4.3 px here. Distances to things in view are assumed, from their blur and size, and the speeds that rest on them are derived with the camera's 965 px focal length at 1920 (wind.md §1).

| Time (s) | What moves | Read |
|---|---|---|
| 8.55–8.85 | **A pale grass tuft at the lens.** A soft-edged (out of focus, so a few cm from the lens) bunch of pale stalks rises into the bottom centre, about 13 % of the width, drifts right and leaves the bottom-right edge in 9 frames. Whole. | about 700 px in 0.3 s; at 5 cm (assumed) about 0.1 m/s: a stalk brushed under the camera as the drone creeps, not a flung piece (derived) |
| 10.36–10.72 | **A long loose straw.** One straw, about 5 px thick (0.7 m away, vegetation-on-the-props.md §1), sweeps in from the right edge across the lower third to the bottom left and back out, in 10 frames. It bends along its length as it sweeps, like a whip, and stays whole. | lifted by the outwash or dragged by the airframe; the frames cannot tell which |
| 17.23–17.70 | **A standing clump of dead stalks sways.** The clump ahead-left (about 0.5–1 m, assumed) has pale stalks whose bow changes between consecutive frames: a stalk straight in one frame is bowed in the next and straight again two frames later, around a lean that holds. Nothing breaks. | a flutter at the frame rate's limit (33–67 ms per change, so 7–15 Hz or above, aliased: measured by eye), on a slow lean |
| 17.30–17.40 | **A dry leaf lifts.** A curled brown leaf rises beside the left airframe rod and leaves the left edge within 3 frames, moving left and slightly up. | about 60 px a frame; at 0.2–0.4 m (assumed) 0.4–0.7 m/s across the view (derived); the camera's own yaw then is about 4 px a frame (measured), so the motion is the leaf's |
| 17.6–19.6 | **A green weed stalk at the right** (0.3–0.6 m, assumed) bends and unbends over a few frames, its S-shape changing; a small yellow leaf lying on the mat to its right flickers in place. | irregular, no clean period readable at 30 frames a second (measured by eye) |
| 18.47–18.84 | **A dead leaf thrown forward.** A brown leaf comes up from under the lens at the bottom edge, tumbling (its outline changes every frame: face-on, then edge-on), and flies forward and up away from the camera: its image shrinks from about 150 to 40 px in 4 frames, then it is lost among the stalk's leaves. | with a 3–6 cm leaf (assumed) it goes from about 0.2–0.4 m to 0.7–1.5 m in 0.13 s: **about 3–8 m/s** (derived), the speed of the front rotors' outwash near the ground (§4) |
| 9.0–13.8 | **No dust over the bare soil.** The drone creeps over dark soil patches at its creep height; nothing rises from them. Lighter grey bands on the soil move with the ground, not with the air: a drier crust or tyre marks. | no dust (measured by eye, every second frame) |
| 20.0–20.94 | **Into the straw.** The drone pitches down and noses into the mat (vegetation-on-the-props.md §1). Standing dry stalks at the left edge, a few cm from the lens, whip across the view; from 20.54 s single straws pass between the lens and the right rod. **The picture turns pale and soft:** over the centre (400–1500 × 300–640 px) the darkest 2 % of the luma rise from 23–25 to 31–53 of 255, the fine detail (the mean of the luma less its 5 × 5 blur) falls from 9.3 to 2.1–4.8, and the saturation from 84 to 53–64. | measured (§1.6); a puff of dust and chaff from the mat, motion blur and stalks at the lens all lower contrast this way, and the view also turns from the belt to the mat, so the stills cannot separate them |

### 1.3 The three effects the pilot named

- **Grass swaying in the wash.** Seen within about 1 m of the drone: standing dead stalks flutter frame to frame around a lean that holds while the drone is near, and a green weed bends and unbends. The lodged mats stay down; loose pieces on them move (straws, leaves). Beyond about 1.5–2 m nothing is seen to move between frames apart from the camera's own motion (by eye; the feed's resolution limits this).
- **Pieces in the air.** No piece is seen leaving a disc: the discs are out of view in this airframe group (vegetation-on-the-props.md §3). What the feed shows are **whole dry leaves and loose straws thrown up and forward from under and around the drone, tumbling, at up to several metres a second, each in view for 4–10 frames**, and straws at the lens in the last 0.4 s. A piece cut and flung at a blade's speed (tens of m/s) would cross the view within one field and show as one speck or streak (vegetation-on-the-props.md §1). So what reads as "cutting" in this feed is most likely these thrown pieces: whether a blade cut them first cannot be told.
- **Dust whiffs as the drone comes lower.** H's dark soil gives none at the creep height. The only candidate in H is the pale, soft last 0.4 s as the drone noses into the mat. **Clip F is the dust reference** (§1.4).

### 1.4 The other clips, briefly

- **F (0–8.4 s), the dust:** dry season, full sun, the drone at about 0.5–1 m (ground-micro-motion.md) among bushes over bare dry loam. The first wisps rise at mid-distance to the right at 0.7–1.0 s; by 1.4 s an ochre sheet the soil's colour covers the lower third of the view, dense at the ground and thinning upward; by 2–3 s it reaches above the horizon line (the camera's height) and veils the bushes behind (2–5 m, assumed). It is a continuous veil with drifting streaks, not separate puffs, and it lasts as long as the drone stays low (measured by eye, 3 stills a second).
- **A (11.2–14.1 s):** a fast shallow descent over a green-and-dry meadow. The grass streaks with the forward motion; no flattened patch shows ahead of the drone: a moving drone's wash lands behind the camera's view.
- **D (5.0–6.9 s):** leafy weed tops pushed aside by the body at about 1 m, whole (vegetation-on-the-props.md §1).
- **P (4–11 s):** high over stubble fields and scrub in severe wind. The crowns are a few feed pixels; their sway is not resolved.

### 1.5 Targets (REAL-03)

| Target | From | For |
|---|---|---|
| Standing stalks within about 1 m flutter frame to frame around a held lean; mats stay down | H 17.2–19.6 s | 1.3: the wash's ring at a creep height of 0.3–0.6 m |
| Loose dry leaves and straws thrown up and forward at about 1–8 m/s, tumbling, 4–10 frames in view, whole | H 17.30, 18.47–18.84, 10.36–10.72 s | 1.4: pieces from the wash, beside the cut pieces |
| No dust from damp dark soil at the creep height | H 9.0–13.8 s | 1.4: the wet case (spec: "a landing on wet or covered ground") |
| A continuous ochre veil from dry bare loam at 0.5–1 m, first wisps within 1 s, over the camera's height in 2–3 s | F 0–3 s | 1.4: the dry case |
| The view softening and paling as the drone noses into a straw mat | H 20.0–20.94 s | 1.4's side by side; low confidence |

### 1.6 Reproduce

The clip's path is passed on the command line and never written; every output lands in the git-ignored `reference/_frames/<letter>/`. Run from the main checkout, which holds `reference/` (`--root`).

```
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 2 --out reference/_frames/H/overview
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 30 --start 8 --duration 8.05 --out reference/_frames/H/mid
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 30 --start 16 --out reference/_frames/H/end
python tools/reference/contact_sheet.py --letter H --frames reference/_frames/H/end --first 1 --count 24   # and --first 25, 49, 73, 97, 121
python tools/reference/extract_frames.py <clip F under reference/> --letter F --fps 3 --duration 8.4 --out reference/_frames/F/w16
```

ffmpeg decoded on the CPU (no hardware acceleration). The crops, the tone statistics of 20.0–20.94 s (luma percentiles, saturation, fine detail) and the forward term's segment medians from H's `motion.csv` (ground-micro-motion.md §6) used throwaway scripts kept outside the repo; their outputs are local only.

## 2. The plant classes the farmland draws

What the friend's world places (`maps/farmland/placements.json`, its grass layers in `maps/farmland/map.json`), with the sizes as drawn: the meshes' bounds and collision capsules in `unity/Assets/_Project/Art/Models/vegetation/VF*/*.assetmeta.json`, and the stems and leaf areas measured from the local store in `assets-src/vendor_plant_numbers.json` (all **measured** from the assets, w9-village-physics 2.8). The sim's own stem numbers are in `assets-src/surfaces.json`'s `cover` table (w9-village-physics 2.11 and 2.12, from stem-properties.md). Placement scales run 0.5–1.5 of the drawn size.

| Class | What the farmland draws | The sim's numbers today | Proxy species for the published numbers |
|---|---|---|---|
| **Grass blades and green culms** | Grass_A (225 stems an instance), Grass_B, _C, _D clumps (566–2,050): 0.45–0.97 m tall (p10–p90), a tall layer of Grass_A to 1.34 m; drawn widths 1.1–25 mm (blade cards) | `grass_blade`: d 1.81 mm for a 1 m culm, d ∝ L^(1/1.53) (Niklas 1995); wall 0.125 d; E 6.4 GPa (derived: the least that lets the tallest drawn culm stand); 1000 kg/m³; ζ 0.1 (assumed) | meadow grasses (*Dactylis*, *Arrhenatherum*, *Elymus*: stem-properties.md [1]) |
| **Dry culms and straw** | GrassDry_A, _B, _C (110–210 stems): 0.59–0.95 m; tall layers 1.17–1.77 m | `dry_culm`: d 4.49 mm for a 1 m culm; wall 0.125 d; E 2 GPa (assumed); 400 kg/m³; `straw`: E 4.8–6.6 GPa (published, O'Dogherty et al. 1995) | wheat and barley straw (stem-properties.md §1.4) |
| **Burnt culms** | GrassBurnt_A, _B, _C: 0.44–1.15 m; tall 1.22–1.77 m | `burnt_culm`: the dry culm's with half the wall, 300 kg/m³ (assumed, no source) | none found |
| **Weed stalks** | the `weeds` ground class (34 % of the map) under GrassDry; no separate weed mesh | `stem`: wall 0.2 d, E 1–4 GPa, 800 kg/m³ (assumed); stem-properties.md: 5–15 mm, 0.5–1.5 m | ragweed, mugwort, goldenrod (stem-properties.md §1.3) |
| **Hedge shrubs** | GreenBush (579 + 190 dry): 2.0 m tall, 3.3 m wide, leaf area density 0.98 m²/m³; LeafyBush_P2 (300 + 73): 3.2 × 5.3 m, twigs 9–18 mm at the base, 0.95 m²/m³; RedBush_Dry (40): 1.8 × 3.5 m, 1.13 m²/m³; SmallBushDry_A, _B (102): 1.6–1.8 m, twigs only; DeadBush_4, _6 (68): 3.4–5.0 m, dead twigs 10–44 mm at the base | `twig`: E 7–12.8 GPa (published, Wood Handbook; black locust 12.8), 950 kg/m³; `leaf`: 0.06 kg/m² dry, 0.2 fresh | the belts' shrubs: elder, blackthorn, privet, lilac (object-sheets/vegetation.md) |
| **Broadleaf trees** | IslandTree (154 + 48 dry): 13.0 m tall, crown 12.8 m wide, trunk about 1.1 m across at breast height as drawn, leaf area density 0.88 m²/m³ (0.60 dry) | the crown shell's twigs and leaves as above | the belts' black locust, ash, maple, elm (object-sheets/vegetation.md V01–V04) |
| **Pines** | Pine006_01, _02 (56): 6.5–7.9 m, dbh about 11 cm; Pine002_S2, _M, _L (20): 5.1–15.6 m, dbh 11–60 cm; Pine004_1, _3 (11): 20–21 m; PineDead_02 (5): 12 m, no needles. Needle area density 0.21–2.09 m²/m³ | as above | Scots pine (*Pinus sylvestris*) |

## 3. Bending, damping, drag and flutter

### 3.1 The laws (derived from beam theory; the inputs are the sim's and §2's)

- **First bending mode of a stem, twig or branch** (a uniform cantilever): f₁ = (1.875² / 2π)·√(EI / μL⁴), μ the mass per length. A tip mass M (an ear, a seed head) lowers it to f = (1/2π)·√((3EI/L³) / (M + 0.243 μL)). Both are the textbook beam (Rayleigh's tip-mass form). For a solid section they reduce to f₁ ∝ (d/L²)·√(E/ρ): **thicker raises it, longer lowers it by the square**, the scaling the tree literature fits (§3.2).
- **Damping:** the structural ratio ζ_s (published per class below) plus the **air's own damping** of a stem in a mean flow U: the drag on a stem moving at v in that flow is ½ρC_d d(U − v)², whose slope gives c = ρC_d dU per length, so ζ_a = ρC_d dU / (2μ·2πf) (quasi-steady drag, derived). It grows with the wind and falls with the stem's mass: **a thin grass culm is overdamped in any breeze, a thick stalk or a twig rings** (table below).
- **What a short gust leaves behind** (an impulse on a damped oscillator): the stem swings past its rest and each later swing is e^(−2πζ/√(1−ζ²)) of the one before; it is within 5 % of rest after ln 20 / (2πζ) cycles:

  | ζ | each swing of the last | cycles to 5 % |
  |---|---|---|
  | 0.04 (a leafless tree, §3.2) | 0.78 | 11.9 |
  | 0.08 (a wheat shoot; a tree in leaf 0.086) | 0.60 | 6.0 |
  | 0.2 | 0.28 | 2.4 |
  | 0.36 | 0.09 | 1.3 |
  | ≥ 1 | no swing past rest: it creeps back | — |

- **A steady wind** leans a stem by its drag over its stiffness: the tip moves q·C_d·d·L⁴ / (8EI) for a uniform load (linear while under about 0.3 L). Past that the stem reconfigures (§3.2's Vogel exponents): its drag grows slower than U².
- **The Cauchy number** ρU²dL³/EI tells which: under about 1 the stem is a rigid bluff body, over it reconfiguration sets in, and far over it the original length drops out of the drag (Gosselin 2019 [3]).

### 3.2 Per class

**Read the table this way:** the frequency is derived from the class's own section (§2) unless it names a source; the ζ_a columns are at a wind of 2 / 5 / 10 m/s (C_d 1.15, stem-properties.md §2.4).

| Class | First mode f₁ | Structural damping ζ_s | Air damping ζ_a at 2 / 5 / 10 m/s | Drag and reconfiguration | Flutter | Scaling |
|---|---|---|---|---|---|---|
| **Green grass culm** 0.45 / 0.67 / 0.97 / 1.34 m (d 1.1–2.2 mm, EI 2.9e-4 – 5.0e-3 N·m²) | 2.4 / 1.4 / 0.8 / 0.5 Hz bare; 0.8 / 0.7 / 0.6 / 0.4 Hz with a 0.3 g seed head (assumed) | 0.08 (published for a wheat shoot [5]; no grass value found) | 0.27–0.59 / 0.67–1.5 / 1.3–3.0: **overdamped from about 3 m/s** | C_d ≈ 1 upright (wheat plants about unity [5]); Cauchy number past 1 from about 1.5 m/s for a 0.67 m culm (derived), then the Vogel exponent of a filament or plate, **−2/3** (drag ∝ U^(4/3), [3]) | the blades (leaves on the culm) flutter in any wind: no frequency found | f₁ ∝ d/L², d ∝ L^0.65, so f₁ ∝ L^−1.35 (derived) |
| **Dry culm and straw** 0.2 / 0.6 / 0.9 / 1.6 m (d 1.6–6.1 mm) | 15 / 3.5 / 2.0 / 0.9 Hz at E 2 GPa; with a 1.5 g ear (assumed) 1.2 Hz (0.6 m) and 1.0 Hz (0.9 m), 1.6–1.9 Hz at straw's 4.8 GPa | 0.08 [5] | 0.07–0.28 / 0.17–0.70 / 0.33–1.4: a standing straw rings 1–3 swings | wheat plant C_d about unity [5]; **stubble (0.2 m) is stiff: 15 Hz, barely moves** | ears and loose leaves flutter | as above, d ∝ L^0.65 |
| **Weed stalk** 5 / 8 / 13 mm, 0.5–1.0 m (EI 0.03–4.9 N·m²) | 1.0 m: 0.6–1.3 / 1.0–2.1 / 1.7–3.4 Hz; 0.5 m: 2.6–5.2 / 4.1–8.3 / 6.7–13 Hz (E 1–4 GPa, the leaves doubling μ, assumed) | 0.08–0.1 (assumed: the wheat shoot's [5] and the sim's 0.1) | 0.003–0.09 / 0.008–0.21 / 0.016–0.42: **rings**, light stalks damp more | rigid cylinder C_d 1.0–1.3 until its leaves fold (stem-properties.md §2.4) | its leaves flutter | f₁ ∝ d/L² |
| **Twig with leaves** 3–10 mm, 0.3–1 m (solid wood, E 10 GPa) | 3 mm × 0.3 m: 15 Hz bare, 9.6 leafy; 5 mm × 0.5 m: 9.1 / 6.3; 10 mm × 1 m: 4.5 / 3.5 (leaf load 10–50 g/m, assumed) | part of the crown's (next rows) | small next to its leaves' drag | a broad leaf **rolls into a cone**, its drag rising about linearly with U (Vogel exponent about **−1**, the tulip tree, Vogel 1989 as [3] restates it); white oak leaves flutter and tear instead ([3]) | **leaf flutter: a cherry leaf's torsional mode 6 ± 1 Hz** [11]; flutter dominates the foliage's motion in light wind and depends only weakly on the wind's speed [11]; its onset depends on the leaf's angle to the wind and the wind's speed [12] | f₁ ∝ d/L² |
| **Branch** 30–50 mm, 3–4 m | 1.5 / 1.4 Hz bare, 1.1 / 1.0 leafy (derived) | branches are tuned near the tree's own sway and take its energy (multiple resonance damping, [13]) | included in the tree's | as the crown | — | f₁ ∝ d/L² |
| **Broadleaf tree** (IslandTree, 13 m) | **not the cantilever's:** broadleaves follow a pendulum, f₀ ∝ 1/√H, best fit R² 0.61–0.67; open-grown ones sway about 0.5 Hz faster than forest ones of their height [2]; a 3D-modelled sycamore 0.26 Hz [1]. Assumed for IslandTree: **0.3–0.6 Hz in leaf** | **0.086 ± 0.022 in leaf, 0.039 ± 0.013 leafless**, the leaves adding 0.047 (pull-and-release, sycamore, ash, birch [1]) | in the ζ_s (the leaves are most of it) | crowns streamline: at 20 m/s the frontal area falls to 20–37 % of still air (cottonwood, alder, birch, [9]); a dense poroelastic crown tends to a Vogel exponent of **−1** [3] | leaves flutter (as the twig row) | **leafless f₀ is 1.18 ± 0.12 × in leaf (+19 %)** [1]; leaves lower both f₀ and ζ [1] |
| **Pine** (Pine002, 004, 006, PineDead) | f₀ grows linearly with dbh/H² (602 trees, 8 species, [6]); restated as f₀ = 0.0948 + 3.4317·D/H² − 0.7765·I_P·D/H² Hz (D cm, H m, I_P 1 for pines) [7]: **Pine006 0.6–0.8 Hz, Pine002_S2 1.3, Pine002_M 0.6, PineDead 0.65, Pine002_L 0.75, Pine004 0.42 Hz** (derived from the drawn sizes); the uniform cantilever with √(E/ρ) 2,900 m/s gives 0.5–1.8 Hz | **internal under 0.05**, not tied to diameter; the external damping is the foliage's drag and the crowns hitting their neighbours [6] | in the external damping [6] | conifers at 20 m/s: C_d 0.22–0.47, frontal area down 36–54 % (redcedar, hemlock, lodgepole pine, [8]) | needles: none found | pines sway slower than spruce and fir of the same dbh/H² [6] |

**Checks:** the derived wheat-like straw with an ear (1.0–1.9 Hz) sits where the lodging literature puts a shoot's natural frequency, which varies over the season and with wetting [5] (its value was not read). The derived pines (0.4–1.3 Hz) and the restated regression agree within 30 % (the cantilever reads high, as a uniform cylinder ignores the crown's mass). The tree spectra stay of one slope from 3–4 m/s up to high winds, so **no extra damping or amplification switches on in strong wind** [2].

### 3.3 What 1.2 should take from it

- **Two kinds of motion.** Grass culms and blades are overdamped in any wind above 2–3 m/s (ζ_a ≥ 0.4–1): they **follow the gust with a lag** τ ≈ ζ/(πf₁) (for ζ > 1: 0.2–0.5 s for a 0.67 m culm at 5–10 m/s, 0.9–1.8 s for a 1.34 m one, derived) and creep back without overshoot; in calm air (ζ ≈ 0.08) a flicked culm swings 5–6 times. Straw, weed stalks, twigs, branches and trees ring: **a short gust leaves 2–6 decaying swings** at their own f₁ (ζ 0.04–0.2).
- **Reconfiguration caps the load:** grass and leaves past a Cauchy number of about 1 take drag ∝ U^(4/3) (plates, filaments) or ∝ U (cones, dense crowns), not U².
- **Leaves flutter by themselves** at a frequency nearly independent of the wind (a cherry leaf 6 ± 1 Hz), riding on the branch's slower sway; in stronger wind the branch's buffeting takes over [11].
- **Season:** a leafless broadleaf sways 18 % faster with half the damping [1]; the farmland's `_Dry` variants are the late-season look, not leafless.

## 4. The rotors' wash on plants

### 4.1 Published

| Finding | Value | Conditions | Source | Read |
|---|---|---|---|---|
| A crop's deformation follows the wash's **peak speed** at the canopy; **under 3 m/s no visible change** in the plants | — | heading-stage rice under a multirotor at 1.5–5 m, 1–5 m/s, payload 0–20 kg | [21] | abstract (search excerpt; its journal record could not be read) |
| The deformed area in forward flight is **horseshoe-shaped**; payload (thrust) matters most, then flight speed, then height | — | the same | [21] | the same |
| Hovering higher (2 → 8 m), the speed at the canopy falls and its turbulence intensity rises | — | the same | [21] | the same |
| Over about **3.5 m/s** of flight speed or **3.5 m** of height the wash hardly forms a vortex in the canopy | — | rice, a heavier multirotor | [22] | abstract (search excerpt) |
| The **outwash peaks at 1.8–1.95 v_h** at r/R 1.6–1.8, whatever the height, in a layer about 0.15 R deep, 0.02–0.08 R above the ground | — | a rotor at z/R 0.87–2.09 | [23] | full, as design 19 reads it |
| The radial wall jet's peak speed falls as **r^−1.1**, its half-height δ = 0.098·r·(r/H)^−0.1; its profile (Verhoff) has its peak at 0.165 δ, half the peak at δ, 9 % at 2 δ, 0.7 % at 3 δ | — | a round jet landing normally | [24], design 19 | restated (design 19, [16]) |

### 4.2 This drone, from the sim's own law (derived)

The hover's v_h is 10.1 m/s and the contracted near wake 2v_h ≈ 20 m/s (p7d design 19). Its landing numbers at 2 m and 4 m are interpolated (log-linear) from design 19's table at 1.5 / 3 / 6 m. A culm's lean is §3's: a green 0.6 m culm lies over (tip past L under a full-height load) from about **5 m/s**, leans a third of its height at 3 m/s and a tenth at 1.5 m/s; a dry culm needs about **10 m/s** to lie over.

| Hub height | What lands | Flattened (≥ 5 m/s over the plant's height) | Leaning and rippling ring (3 → 1.5 m/s) |
|---|---|---|---|
| **0.5 m** (below l = 0.45 m … 2.5 l = 1.13 m, mostly the near wake) | four columns at about 20 m/s, a wall jet from each at up to 18–20 m/s at 0.20–0.23 m from its hub [23] | **under the four discs and between them**, dry culms too (≥ 10 m/s): a patch about the drone's span (0.6–0.8 m) | the outwash falls as r^−1.1 to about 3.8 m/s at 1 m and 1.8 m/s at 2 m from a hub: **visible lean out to about 1–1.2 m, ripples to about 2 m** |
| **1 m** (between the near wake and the merged jet: decision 19's blend) | the columns merging | as at 0.5 m with lower speeds; dry culms only under the discs | as above |
| **2 m** | the merged jet at about **9.8 m/s**, its landing zone r_i ≈ 0.77 m | the landing zone, r ≲ 0.8 m: green and most dry culms down | the wall jet is 3 m/s at about 2.3 m, but **only 0.1–0.3 m deep** there (δ ≈ 0.1 r): a tall culm takes it on its lowest quarter, so it leans and flickers rather than lies |
| **4 m** | about **6.9 m/s**, r_i ≈ 1.1 m | green culms within about 1.1 m; dry culms lean (0.4 L), not down | 3 m/s at about 2.3 m, as at 2 m: **the moving ring's reach barely changes with the height** (the outwash beyond r_i carries the jet's momentum flux, design 19) |
| **≥ 8 m** | under 5 m/s at the ground | none | a breeze-like ring, under the 3 m/s of visible change past 1–2 m |

So, for 1.2's check of the spec's hover at 0.5, 1, 2 and 4 m: **the flattened patch is the drone's span low down, the landing zone (0.8 → 1.1 m) higher up, and its force falls with height** (dry culms stop lying flat from about 2–3 m, green ones from about 6–8 m); the leaning ring reaches about 2 m from almost any height; tall grass only feels the outwash near its base. In forward flight the patch trails as the horseshoe of [21], behind the camera (A).

**Against clip H** (rotors 0.3–0.6 m up, the near wake): mats pressed flat under the drone, standing stalks within about 1 m fluttering, nothing visibly moving past 1.5–2 m, and dead leaves thrown out at 3–8 m/s (§1) all fit the outwash's 18–20 m/s at the discs falling to about 4 m/s at 1 m and 2 m/s at 2 m.

## 5. The dust the wash lifts

### 5.1 Published

| Finding | Value | Conditions | Source | Read |
|---|---|---|---|---|
| **Threshold friction velocity** to start grains moving (Shao and Lu 2000): u*_t = A_N·√((ρ_p/ρ_a)·g·D + γ/(ρ_a·D)) | A_N 0.111; γ 1.65–5.0 × 10⁻⁴ N/m for dry loose soil (2.9 × 10⁻⁴ fitted by Kok and Renno 2006) | loose, dry grains on a flat bed | [14] | full |
| Its minimum | at grains of about **75–100 µm**; derived from the law: **0.21–0.27 m/s** at 80–140 µm | — | [14]; derived | full |
| **Dust is not lifted directly** (a 10 µm grain needs 0.4–0.7 m/s, a 2 µm one 0.9–1.6 m/s, derived from the law): it is knocked out by **saltating sand-sized grains**, so the dust threshold is the saltation threshold | — | — | [14] | full |
| **Moisture** raises it (Fécan et al.): u*_wet / u*_dry = 1 for w ≤ w′, √(1 + 1.21·(w − w′)^0.68) above, w′ = 0.0014·c² + 0.17·c (w gravimetric water %, c clay %) | **× 1.5–2.5 at 5–10 % water, × 2.7–3.3 at 15–25 %** (derived, clay 10–30 %) | all soil types | [14], [15] | full ([14]); abstract ([15]) |
| **Crusts and cover** raise it: roughness elements (stones, plants) take the wind's stress off the bare soil between them | — | — | [14] | full |
| Loose agricultural soils, wind tunnel | sandy loam **0.139 m/s**; four silt loams **0.189–0.252 m/s**; a silt loam fallow field in place **0.35–0.40 m/s** | soils from cropped fields | [17] | abstract (search excerpt) |
| Desert crusts | bare sand **0.16 m/s**; crusts disturbed 1, 5–10 years before **0.46, 0.87 m/s**; crusts undisturbed for 20 years **3.76 m/s** | sandy soil, portable wind tunnel | [18] | abstract (search excerpt) |
| Volcanic ash on the ground (no burnt-field ash number found) | **0.13–0.38 m/s**; fine ash's threshold doubles with humidity | field and laboratory | [19] | abstract (search excerpt; the units read as m/s) |
| Radial wall jet's skin friction (Poreh et al. 1967): c_f = τ_w / (½ρu_m²) = 0.06·(u_m·δ/ν)^−0.3·(r/H)^−0.16 | c_f 0.0021–0.0029 here (derived) | smooth wall | [16] | full (restating Poreh) |
| Under a rotor, the wake's vortices hitting the ground give **large, brief excursions of wall shear and an upwash** that lifts and holds sediment; models built on a steady threshold miss them | — | rotorcraft brownout, model rotor over a mobile bed | [25] | record and search excerpt |

### 5.2 The wash's friction velocity on the ground (derived)

u* = u_m·√(c_f/2) with [16]'s smooth-wall c_f, and on a rough wall u* = u_m / (5.75·log₁₀(y_m/k_s) + 8.5) at the jet's peak height y_m = 0.165 δ (Nikuradse's fully rough law, p7d design 20), for k_s 2 mm (rolled soil, a track). The jet's numbers are design 19's for this drone.

| Where | u_m (m/s) | u* smooth | u* rough (k_s 2 mm) |
|---|---|---|---|
| 10 m hover, at the landing zone's edge (r_i 2.07 m) | 3.7 | 0.14 | 0.23 |
| 6 m, r_i 1.41 m | 5.4 | 0.20 | 0.36 |
| 3 m, r_i 0.92 m | 8.2 | 0.30 | 0.59 |
| 1.5 m, r_i 0.68 m | 11.2 | 0.40 | 0.86 |
| 1.5 m, at 2 m out | 3.4 | 0.11 | 0.22 |
| 1.5 m, at 4 m out | 1.6 | 0.05 | 0.09 |
| rotors 0.3–0.5 m up, at the outwash's peak | 19 | 0.72 | about 2 |

**The friction velocity under the drone doubles from 10 m to 3 m and doubles again by 0.5 m**, and it falls fast outward (r^−1.1 and a thickening layer). The smooth and rough laws differ by up to 2×: that is the band.

### 5.3 Per surface (the surface table's `dry` sets)

"Dust from" is the hover height at which the landing zone's u* passes the surface's threshold, from §5.2's smooth to rough columns (derived; the threshold labelled as given).

| Surface | Threshold u*_t, dry | Label | Dust from | Look |
|---|---|---|---|---|
| `crater_spoil` (loose churned spoil) | 0.14–0.25 m/s | published range of loose loams [17] | **about 6–10 m** | the soil's colour; plenty of supply |
| `dirt_track` (compacted silt loam, a loose film) | 0.19–0.25 | published, loose silt loams [17] | about 5–10 m, but a thin film: **short whiffs**, then the compacted body | the track's dust |
| `belt_bare` (firm bare loam, loose top; F's dust) | 0.20–0.35 | assumed, between loose loam and a fallow field [17] | about 3–6 m; dense at 0.5–1 m (F) | ochre (F) |
| `burnt_field` (ash on crusted loam) | 0.13–0.38 (the ash) | published for volcanic ash [19]; no burnt-ground value | about 3–10 m; a light ash puffs first | dark grey to black |
| `tilled` (loose tilled loam, clods) | 0.35–0.40 | published, fallow silt loam in place [17] | about 1.5–4 m | the soil's colour |
| `dry_crust` (crusted silty field) | 0.5–1.0 | assumed, between disturbed crusts 0.46–0.87 and loose loam [18]; intact old crusts reach 3.76 | **about 1–3 m, growing lower**: the pilot's "dust whiffs as you land closer to the ground" | sparse, as the crust abrades |
| `rubble` (brick and mortar) | 0.25–0.5 | assumed (a fine fraction among coarse debris) | about 1.5–5 m, supply-limited | red-grey brick dust |
| `asphalt`, `concrete_slab` | 0.2–0.3 for the film on them | assumed | from 5–10 m, one short whiff, then clean | grey |
| `meadow_sod`, `weeds`, `belt_straw`, `yard_litter` | the cover shelters the soil [14] | published in kind | **no soil dust**; chaff and litter pieces lift instead (H's straw mat) | straw and leaf bits |
| any surface **wet** | × 1.5–3.3 by water content | published [14], [15] | wet loam (15–25 % water) needs 0.5–1 m/s: **only under about 1 m, or none** (H's dark soil) | — |
| **saturated** | — | assumed: no loose grains | **none** | — |

**What a whiff looks like** (derived): the fines (under 20 µm) settle at under 3 cm/s (Stokes), so the dust hangs and drifts with the wind and the wake's recirculation for many seconds; the sand-sized grains that knock it loose (about 100 µm) hop and fall back within a second. The unsteady wake (its vortices landing, [25]) makes the lift come in puffs at first; low and long over loose dry ground it becomes F's continuous veil.

## 6. The drone in what it flies through

### 6.1 Published

| Finding | Value | Conditions | Source | Read |
|---|---|---|---|---|
| A canopy's drag on the air: ½ρC_d·a·U² per volume, a the one-sided leaf area per volume | C_d **0.1–0.5** (Katul et al. 2004, Endalew et al. 2009) | canopy flow models | [20] | search excerpt |
| The drag length ℓ_d = 1/(a·C_d) | field medians **21 m for trees, 0.7 m for low vegetation such as crops** | review of field experiments; 168 large-eddy simulations | [10] | abstract |
| A wheat plant's drag coefficient | about **1** | portable wind tunnel over the field | [5] | abstract |
| Whole crowns at 20 m/s | C_d 0.22–0.47 (conifers) | wind tunnel, crowns 4–20 m/s | [8] | abstract (search excerpt) |
| The drawn crowns' leaf area density | **0.88 m²/m³** (IslandTree; 0.60 dry), 0.95–1.13 (the leafy bushes), 0.21–2.09 (the pines' needles) | measured from the store's meshes | `assets-src/vendor_plant_numbers.json` | measured |
| A force on a small aircraft pushed through foliage | **none found** | — | — | — |

### 6.2 What it gives the drone (derived)

- **Each element pushed aside** is a cantilever: F = 3EI·δ/L³ at a deflection δ at its tip. At δ = 0.1 m:

  | Element | Stiffness at the tip | Force at 0.1 m |
  |---|---|---|
  | green grass culm, 0.6 m | 0.008 N/m | **0.8 mN** |
  | dry culm, 0.9 m | 0.085 N/m | 8.5 mN |
  | weed stalk 5 mm × 0.5 m / 8 mm × 1 m / 13 mm × 0.85 m (E 2 GPa) | 1.3 / 1.0 / 12 N/m | **0.13 / 0.10 / 1.2 N** |
  | twig 3 mm × 0.3 m / 5 mm × 0.5 m / 10 mm × 1 m (E 10 GPa) | 4.4 / 7.4 / 15 N/m | **0.44 / 0.74 / 1.5 N** |

  **A twig pushes back about 900 times as hard as a green culm, 90 times a dry one and 4–7 times a weed stalk** (EI ∝ d⁴, over L³). The pilot's "more heavily" for twigs and leaves is this ratio, computed.
- **Grass in numbers:** at about 400 standing culms per m² (stem-properties.md §3), a body 0.25 m wide skimming 0.1 m deep for 0.3 m bends about 30 culms: about 0.03 N if green, 0.26 N if dry. One weed stalk in the way is as much again; a few twigs several times more.
- **Leaves as drag:** the foliage swept by the body resists as ½ρU²·C_d·a·V. With C_d 0.1–0.5 [20], a 0.9–1.5 m²/m³ (the drawn crowns) and V the volume in contact (0.06 m² frontal × 0.3 m deep, assumed): **0.04–0.2 N at 5 m/s, 0.16–0.8 N at 10 m/s**. The leaves' own mass set moving (0.2 kg/m² fresh × a, so 0.2–0.3 kg/m³) bounds a further m″U²A of about 0.3–0.45 N at 5 m/s, if every leaf in the path were brought to the body's speed (an upper bound: attached leaves bend instead).
- **How many twigs at once:** w9-village-physics design 2.3 already counts them by stereology (a part pushed a distance z meets L_V·p·z/4 twigs, p its silhouette's perimeter, L_V 2.0 m/m³ assumed) and breaks each at 0.115 J; the bending forces above are what each twig holds before it breaks or slips off.
- **Damping in contact:** the stems' ζ_s 0.08–0.1 (§3.2) for the bending, plus the air's ζ_a; a stem released by the body springs back at its own f₁ (a culm creeps back, a twig rings).

## 7. Gaps, and the assumption each would take

| Gap | Assumption proposed | Why |
|---|---|---|
| A grass culm's own natural frequency and damping | f₁ from the section (§3.2); ζ_s 0.08, the wheat shoot's [5] | the only measured herbaceous shoot damping read; grass is overdamped by the air anyway (ζ_a ≫ ζ_s in any wind) |
| A wheat shoot's natural frequency (the value) | the derived 1.0–1.9 Hz with an ear | [5] says it varies with season and wetting, but its number was not read |
| A broadleaf tree's f₀ at the IslandTree's size | 0.3–0.6 Hz in leaf | [2]'s pendulum fit and open-grown offset, [1]'s modelled sycamore 0.26 Hz; no measured value for a 13 m open-grown broadleaf was read |
| The restated pine regression's constants [7] | used as given, the pine term negative | read in a search excerpt, the sign inferred from [6]'s finding that pines sway slower |
| Leaf flutter's frequency for the belts' species (locust, ash, maple) | 4–8 Hz, around the cherry's 6 ± 1 Hz [11], each leaf with its own phase | no other species' value found |
| The Vogel exponents of whole broadleaf crowns | −1 for a dense crown [3] and −2/3 for single blades | the conifer table in [3] could not be read; the hardwoods' streamlining [9] fits a strong reduction |
| The crop downwash studies' full texts [21], [22] | used only for the 3 m/s visible-change threshold and the horseshoe | their journals' pages could not be read |
| A dust threshold for ash on burnt farmland | 0.13–0.38 m/s (volcanic ash, [19]) | no post-fire ash value was found |
| The crusted field's threshold | 0.5–1.0 m/s | between disturbed crusts and loose loam [18], [17]; a physical (rain) crust, not a biological one |
| The wall jet's friction on a real field | the smooth-to-rough band of §5.2 (up to 2×) | the soil's roughness k_s is not measured |
| Burnt stems' stiffness | the dry culm's, wall halved (stem-properties.md) | no source |
| Forces on a drone in foliage | §6.2's element sums | nothing measured was found |
| The crowns' twig count per volume | the twig length density L_V 2.0 m/m³ that w9-village-physics design 2.3 carries (assumed, checked there by the pipe model), until it is measured from the store's meshes as 2.8 measured the leaves | the drawn twig cards and the collision's 1.5 m twig spheres do not give it |

## 8. What 1.2 needs first

1. **Two regimes:** grass culms overdamped by the air (lag τ ≈ ζ/(πf₁)), everything thicker a damped oscillator with ζ_s 0.04–0.1 plus ζ_a (§3.1, §3.3). One law covers both: modal mass, f₁ from the section, ζ = ζ_s + ζ_a(U).
2. **The drag reconfigures** past a Cauchy number of about 1: U^(4/3) for blades and culms, U for leaves rolled into cones and dense crowns (§3.2).
3. **The wash's load is the air at the plant's height:** the near columns flatten under the discs, the merged jet's landing zone presses down, and beyond it the wall jet is thin (δ ≈ 0.1 r), so tall grass takes it near its base only (§4.2).
4. **Dust** is a threshold on u* from the wall jet's speed, with §5.3's thresholds and the wet factor; the onset heights in §5.3 are the check.
5. **Clip H's targets** (§1.5): flutter within about 1 m at a 0.3–0.6 m creep, pieces thrown at 1–8 m/s, no dust from damp soil; and clip F's veil.

## References

1. Jackson T., Shenkin A., Moore J., Bunce A., van Emmerik T., Kane B., Burcham D., James K., Selker J., Calders K., Origo N., Disney M., Burt A., Wilkes P., Raumonen P., Gonzalez de Tanago Menaca J., Lau A., Herold M., Goodman R.C., Fourcaud T., Malhi Y. (2019). An architectural understanding of natural sway frequencies in trees. *J. R. Soc. Interface* 16(155), doi:10.1098/rsif.2019.0116. Accepted manuscript: https://discovery.ucl.ac.uk/10078184/ (full).
2. Jackson T.D. et al. (2021). The motion of trees in the wind: a data synthesis. *Biogeosciences* 18: 4059–4072. https://bg.copernicus.org/articles/18/4059/2021/ (full).
3. Gosselin F.P. (2019). Mechanics of a plant in fluid flow. *J. Exp. Bot.*, preprint arXiv:1905.08055 (full; its exponents' symbols confirmed by a search excerpt), restating Vogel S. (1989), Drag and reconfiguration of broad leaves in high winds, *J. Exp. Bot.* 40: 941–948, Gosselin F.P. & de Langre E. (2011), Drag reduction by reconfiguration of a poroelastic system, *J. Fluids Struct.* 27: 1111–1123. https://arxiv.org/abs/1905.08055
4. Berry P.M., Sterling M., Baker C.J., Spink J., Sparkes D.L. (2003). A calibrated model of wheat lodging compared with field measurements. *Agric. For. Meteorol.* 119: 167–180. doi:10.1016/S0168-1923(03)00139-4. https://research.birmingham.ac.uk/en/publications/a-calibrated-model-of-wheat-lodging-compared-with-field-measureme/ (abstract: accurate drag and damping values lowered the predicted failure wind speeds by 44 %).
5. Sterling M., Baker C.J., Berry P.M., Wade A. (2003). An experimental investigation of the lodging of wheat. *Agric. For. Meteorol.* 119: 149–165. doi:10.1016/S0168-1923(03)00140-0. https://research.birmingham.ac.uk/en/publications/an-experimental-investigation-of-the-lodging-of-wheat (abstract).
6. Moore J.R., Maguire D.A. (2004). Natural sway frequencies and damping ratios of trees: concepts, review and synthesis of previous studies. *Trees* 18: 195–203. doi:10.1007/s00468-003-0295-6. https://napier-repository.worktribe.com/output/259422/ (abstract, search excerpt).
7. The regression of [6] as restated in *Forests* 11(9): 915 (2020), doi:10.3390/f11090915 (search excerpt; the page refused automated reads).
8. Rudnicki M., Mitchell S.J., Novak M.D. (2004). Wind tunnel measurements of crown streamlining and drag relationships for three conifer species. *Can. J. For. Res.* 34: 666–676. doi:10.1139/x03-233 (abstract, search excerpt).
9. Vollsinger S., Mitchell S.J., Byrne K.E., Novak M.D., Rudnicki M. (2005). Wind tunnel measurements of crown streamlining and drag relationships for several hardwood species. *Can. J. For. Res.* 35: 1238–1249. doi:10.1139/x05-051 (abstract, search excerpt).
10. Majumdar D., Vita G., Ramponi R., Glover N., van Reeuwijk M. (2024). The drag length is key to quantifying tree canopy drag. arXiv:2411.01570. https://arxiv.org/abs/2411.01570 (abstract).
11. Tadrist L., Saudreau M., Hémon P., Amandolese X., Marquier A., Leclercq T., de Langre E. (2018). Foliage motion under wind, from leaf flutter to branch buffeting. *J. R. Soc. Interface* 15: 20180010. PMC6000177 (abstract and a search excerpt of the text for the 6 ± 1 Hz).
12. Tadrist L., Saudreau M., de Langre E. (2015). Leaf flutter by torsional galloping: experiments and model. *J. Fluids Struct.* 56. https://hal-polytechnique.archives-ouvertes.fr/hal-01187713 (abstract, search excerpt).
13. Spatz H.-C., Brüchert F., Pfisterer J. (2007). Multiple resonance damping or how do trees escape dangerously large oscillations? *Am. J. Bot.* 94: 1603–1611. doi:10.3732/ajb.94.10.1603 (abstract, search excerpt).
14. Kok J.F., Parteli E.J.R., Michaels T.I., Bou Karam D. (2012). The physics of wind-blown sand and dust. *Rep. Prog. Phys.* 75: 106901; arXiv:1201.4353 (full: §2.1.1 restating Shao Y. & Lu H. (2000), *J. Geophys. Res.* 105, and §4.1.1 restating Fécan et al.). https://arxiv.org/abs/1201.4353
15. Fécan F., Marticorena B., Bergametti G. (1999). Parametrization of the increase of the aeolian erosion threshold wind friction velocity due to soil moisture for arid and semi-arid areas. *Ann. Geophys.* 17. doi:10.1007/s00585-999-0149-7 (abstract, OpenAlex).
16. Mahoney et al., A Colebrook equation for impinging radial wall jets, *J. Fluid Mech.* 1008, doi:10.1017/jfm.2022.1095; accepted manuscript https://www.osti.gov/biblio/2587227 (full; restating Poreh, Tsuei & Cermak 1967's c_f).
17. Sharratt B., Vaddella V. (2012). Threshold friction velocity of soils within the Columbia Plateau. *Aeolian Research*. doi:10.1016/j.aeolia.2012.06.002 (abstract, search excerpt).
18. Belnap J., Gillette D.A. (1997). Disturbance of biological soil crusts: impacts on potential wind erodibility of sandy desert soils. Record: https://archive.jornada.nmsu.edu/biblio/disturbance-biological-soil-crusts-impacts-potential-wind-erodibility-sandy-desert-soils (abstract, search excerpt; the site did not resolve for a direct read).
19. Field-based measurements of volcanic ash resuspension by wind. https://www.earth-prints.org/handle/2122/14271 (abstract, search excerpt); and Experimental simulations of volcanic ash resuspension by wind under the effects of atmospheric humidity, PMC6162286 (abstract, search excerpt).
20. RANS solver for microscale pollution dispersion problems in areas with vegetation: development and validation, arXiv:1609.03427 (search excerpt), restating Katul G.G. et al. (2004), *Boundary-Layer Meteorol.* 113, and Endalew A.M. et al. (2009).
21. A field study of a multirotor's wake on heading-stage rice, indexed at https://www.x-mol.com/paper/1352747997662638080 (abstract, search excerpt; its journal record could not be read).
22. *Agriculture* 13(4): 892 (2023), on the wake's vortex in a rice canopy by flight speed and height. https://www.mdpi.com/2077-0472/13/4/892 (search excerpt).
23. Tanner P.E., Overmeyer A.D., Jenkins L.N., Yao C.-S., Bartram S.M. (2015). Experimental investigation of rotorcraft outwash in ground effect. AHS 71st Forum, NASA NTRS 20160006428 (full, as `openspec/changes/p7d-tether/design.md` decision 19 reads it).
24. Poreh M., Tsuei Y.G., Cermak J.E. (1967). Investigation of a turbulent radial wall jet. *J. Appl. Mech.* 34; Verhoff A. (1963), the wall jet's profile (both as decision 19 reads them).
25. Rotorcraft brownout research on a model rotor over a mobile bed (University of Maryland theses: https://drum.lib.umd.edu/handle/1903/12357 and https://drum.lib.umd.edu/bitstream/1903/15914/1/Mulinti_umd_0117E_15592.pdf; record and search excerpt).

Companion references used through the companions: stem-properties.md (the stems' sections, moduli, cutting and stand densities), p7d-tether design 19 and 20 (the wash's law and the wall's roughness law), Niklas K.J. (1995), *Ann. Bot.* 75: 133–142 (the stems' height–diameter allometry, through `assets-src/surfaces.json`).
