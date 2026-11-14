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
