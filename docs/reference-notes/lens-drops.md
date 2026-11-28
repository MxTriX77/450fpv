# Lens drops: the rain on the FPV camera's lens

New for w21-rain-on-camera 1.1 (D-126, FEED-13). The numbers behind `LensWater` (`Runtime/Wind/LensWater.cs`): what reaches the lens, what an impact leaves, what holds a drop, what moves it, and how it merges, trails and sheds. Every number is labelled **published** (a source's value), **measured** (a source's measurement), **derived** (computed here from labelled inputs), **assumed** (a named, tunable parameter in `LensSpec`) or **numerical** (a discretisation). The hand numbers are `tools/flightbench/handcalc/lens_water.py`; its output is quoted below as "hand".

## 1. The lens and the airframe around it

| Number | Value | Label | Source / reasoning |
|---|---|---|---|
| Lens apex, body m | (0, 0.015, 0.115), facing +z at 0° uptilt | assumed (drone.json) | drone.json `camera.lens_m`: a 19 mm micro camera between the plates, its lens 15 mm proud of their front edge |
| Lens type | M12 × 0.5, 2.1 mm, 165° | published | the class's micro cameras' 2.1 mm lens (RunCam RC21 listing, getfpv.com; Caddx Ratel 2 listings: 2.1 mm, 165°) |
| Front glass clear radius | 4.5 mm | assumed | an M12 wide-angle front element 8–10 mm across; no drawing found |
| Barrel front face radius | 7 mm | assumed | the M12 thread (12 mm) plus its front ring |
| Front glass radius of curvature | 8 mm | assumed | a convex meniscus front element: its rim tilts asin(4.5/8) = 34° (derived) |
| Airframe shelter | drone.json's parts as built, the props' swept disks | published / derived | the boxes and solid cylinders of `parts` (a prop part is a mass shape; its disk is `props.diameter_m`) |

The lens sits ahead of every plate, the payload and the electronics, so for drops arriving from ahead nothing shades it. What can shade it: the payload's front end (above and behind the lens) for drops arriving steeply from above and behind onto the dome's upper rim, the front prop disks for drops arriving low from ahead and the side, and the coil and canister's front ends (below) for drops from below.

**A prop disk's shelter (derived).** A drop crossing a disk at speed w along the rotor's axis spends (c·sin β + t)/w within a blade's axial depth; it is hit if a blade's planform passes over it then: P = min(1, N_b/(2π)·(c/r + Ω·(c·sin β + t)/w)), at r = 0.75 R from drone.json's blade table (c, t, β there). Hand, at the hover's 8 755 rpm: 33 % of the drops crossing at 5 m/s, 19 % at 10, 12 % at 20.

## 2. The rain's drops

| Number | Value | Label | Source |
|---|---|---|---|
| Spectrum | N(D) = N₀·e^(−ΛD), Λ = 4.1·R^(−0.21) mm⁻¹ | published | Marshall and Palmer 1948 ([J. Meteor. 5, 165](https://www.radartutorial.eu/15.weather/pubs/marshall-palmer-1948.pdf)); N₀ = 8000 m⁻³ mm⁻¹ there |
| N₀ used | set so the drops carry the flight's rate exactly: 6 800 (5 mm/h) to 7 400 (50 mm/h) | derived | hand; within 15 % of the published 8 000, which was fitted with Gunn and Kinzer's speeds rather than Atlas's |
| Fall speed | V = 9.65 − 10.3·e^(−0.6 D) m/s | published | Atlas, Srivastava and Sekhon 1973 (`RainPreset`) |
| Diameter range | 0.1–6 mm in 12 bins | assumed / numerical | MP misrepresents drops under ~0.5 mm (published caveat); drops over ~6 mm break up |
| Drops move with the air | across at the air's velocity, down at V through it | assumed | `RainPreset`'s: a 1–2 mm drop's relaxation time V/g ≈ 0.5 s |
| Collection efficiency | 1 | derived | a drop's Stokes number on the 7 mm barrel, ρ_w·D²·u/(18·μ_air·a), is 25 or more for D ≥ 0.1 mm at u ≥ 2 m/s: raindrops do not follow the air around the lens |

**The water arriving** on a patch of the lens of outward normal n and area A, from drops of diameter D in a bin of number density n_D, is n_D·max(0, −u_D·n)·A drops a second, with u_D the drops' velocity relative to the camera: the air relative to the camera (`AirVelocityAtCamera`) plus the fall V(D) downward, in the camera's axes. Hand, for 5 mm/h on the dome, no shelter:

| Case | drops/s | water mm³/s |
|---|---|---|
| Hover, calm, level | 0.023 | 0.012 (only the dome's upper rim faces the fall) |
| Hover, 5 m/s into the lens | 0.55 | 0.093 |
| Hover, 5 m/s from behind | 0 | 0 (the rim's 28° tilt is under the large drops' 29° from the vertical) |
| Forward 20 m/s, pitched 25° | 1.93 | 0.30 |

On a flat lens the forward flight's rate over a headwind hover's is the ratio of the normal components exactly (hand: 20 over 5, 4.000).

## 3. An impact

| Number | Value | Label | Source |
|---|---|---|---|
| Splashed share (water) | We/(We + 10⁶), We = ρ_w·D·u_n²/γ on the normal speed | published | Brown, Jepsen and Yoon 2008, ILASS Americas ([Sandia SAND2008-2023C](https://www.osti.gov/servlets/purl/1146266)): "only minor amounts" separate below We 10⁴ |
| Spread | D_max/D = Re^(1/5)·√P/(A + √P), P = We·Re^(−2/5), A = 1.24 ± 0.01 | published | Laan et al. 2014, Phys. Rev. Applied 2, 044018 ([pdf](https://yakari.polytechnique.fr/people/josserand/papier/Radius.pdf)); fitted at 0.5–6 m/s, extrapolated to 20 m/s here |
| Resting angle of a deposit | the receding angle | derived | the spread lamella's angle is far under θ_R, so its line recedes until the angle reaches θ_R |

Hand: a 1.5 mm drop at 18 m/s splashes 0.7 % of itself and spreads to 5 mm, over the whole clear radius; it rests as a 1.8 mm cap. A 1 mm drop at 5 m/s spreads to 2 mm. The spread lamella runs over every drop within it: they merge into the new one.

## 4. What holds a drop

| Number | Value | Label | Source |
|---|---|---|---|
| Retention | F = k·γ·w·(cos θ_R − cos θ_A), w the width across the push | published | Furmidge 1962; the form every source uses |
| k | 1.0 | published (measured) | 0.98 ± 0.08 for sessile water drops ([arXiv 1902.06721](https://ar5iv.labs.arxiv.org/html/1902.06721)); the models give 24/π³ = 0.77 (ElSherbini and Jacobi 2006) and 2/π (Extrand and Gent) |
| θ_A, θ_R on the lens | 50°, 20° | assumed | clean glass wets at under 5–15° (published: glass-treatment patents' controls); glass and silica in open air pick up hydrocarbons and rise to 25–40° within days to weeks (published: patents and silica papers, search record); conventional MgF₂ antireflection coatings are hydrophilic (published, sol–gel AR film literature). A field camera's lens is taken as weathered glass, static near 35° with 30° of hysteresis |
| Water | γ 0.0735 N/m, μ 1.138 mPa·s, ρ 1000 kg/m³ | published | 15 °C, the sim's ISA air (recalled standard tables) |
| Normal pull-off | 2π·r·γ | assumed | the most the contact line's tension can hold against a pull straight off the glass |

A drop at rest is a spherical cap at θ_R. Hand: 0.1 mm³ is a 0.71 mm patch radius held by 31 µN against its 1 µN weight; 1 mm³, 1.53 mm, 67 µN against 10; gravity alone slides a drop over 18 mm³ (4 mm radius) down a vertical lens, a drop nearly the whole glass.

## 5. What moves a drop

**The force in the lens's frame (derived).** A drop at offset ρ from the lens's centre, moving at v over the glass, feels per kg: f = g − a_L − ω̇×ρ − ω×(ω×ρ) − 2ω×v, with g gravity in the camera's axes (the drone's tilt), a_L the lens's own acceleration (the body's, plus ω̇×r and ω×(ω×r) at the lens's arm r), ω the camera's rate. Its part along the glass pushes; its part off the glass pulls (shed past the pull-off).

**The air at the lens.** The barrel's end is a bluff disc of radius a facing the camera's +z.

| Number | Value | Label | Source |
|---|---|---|---|
| Surface speed, stream onto the face | (2/π)·u_n·s/√(a² − s²), outward from the centre | published | potential flow onto a disc broadside (Lamb, Hydrodynamics; recalled) |
| Edge-on part | u_t, as it is | assumed | a thin disc leaves the edge-on stream undisturbed in potential flow; the barrel's rim separation is left out |
| Lee side | the onto-face part 0 | assumed | a face in its own wake sees little of the stream; the edge-on part kept |
| Boundary layer | linear to δ = min(√(ν/k)/1.312, √(ν·a/u_t)/0.332), k = (2/π)·u_n/a | published / derived | Homann's axisymmetric stagnation flow f″(0) = 1.312; Blasius's 0.332 from the windward rim |
| Drag | ½·ρ_air·C_D·A_f·⟨u²⟩ over the drop's height, A_f the cap's side silhouette | derived | ⟨u²⟩ = U²(h/δ)²/3 under δ, U²(1 − 2δ/3h) over it |
| C_D | 1.0 | assumed | see the check below; sessile drops drag more than like-shaped solids (Milne and Amirfazli, published in kind) |

**The check on C_D (derived).** With A_f ≈ ⅔·w·h, the balance ½ρC_D A_f U² = kγw·Δcos gives a depinning Weber number We_h = ρU²h/γ = 3kΔcos/C_D for a drop in a uniform stream and 9kΔcos/C_D inside a linear boundary layer. The published thresholds for one surface are 7.5 ± 0.5 in a flat plate's boundary layer and 2–4 in impinging jets (anodised aluminium, [Phys. Rev. Fluids 9, 034004](https://journals.aps.org/prfluids/abstract/10.1103/PhysRevFluids.9.034004)). C_D = 1 and k = 1 give both for Δcos ≈ 0.83 (7.5 and 2.5), a plausible hysteresis for water on anodised aluminium (assumed: not given in the abstract). The 3 : 1 ratio between the two flows is the law's, not a fit.

Hand, forward flight at 20 m/s pitched 25° (u_t 8.5 m/s down the glass, u_n 18 m/s onto it, δ ≈ 0.07 mm at the centre): the air pushes a 1 mm³ drop at the centre with 20 µN and gravity with 9, against 67: it stays. A 10 mm³ drop takes 103 + 89 against 144: it slides down and off. The edge-on air alone moves 0.1 mm³ at 21 m/s, 1 mm³ at 14, 10 mm³ at 9.6. So small drops stay in fast flight and the large ones, mostly merged, travel with the air, as FEED-13 asks.

## 6. Sliding, elongation and trails

| Number | Value | Label | Source |
|---|---|---|---|
| Sliding speed | Ca = s·(F − F_ret)/(γ·V^(1/3)), Ca = μU/γ | published | Le Grand, Daerr and Limat 2005, J. Fluid Mech. 541, 293 ([pdf](https://labo.msc.u-paris.fr/~daerr/reprints/Le-Grand-et-al_Shape-of-drops_JFM2005.pdf)), eq. 4.1 |
| s | 0.006 | derived | their fitted slopes 0.0067–0.0113 (three silicone oils on a fluoropolymer, θ 43–58°, ln(b/a) ≈ 13), scaled by the contact line's dissipation θ/ln(b/a) to water at θ̄ 35° and ln(b/a) 15 |
| ln(b/a) | 15 | derived | 1 mm over a water molecule's 0.3 nm |
| Trail onset | Ca* = θ_R³/(9·ln(b/a)) = 3.2·10⁻⁴, 2.0 cm/s | published / derived | Cox–Voinov θ³ = θ_s³ − 9 ln(b/a)·Ca at the rear (Le Grand et al. eq. 5.2, A = 1/[9 ln(b/a)]); their corner forms at θ_r = 0.46–0.56·θ_s,r, pearls follow |
| Trail film | 0.94·h·Ca^(2/3) | published / assumed | Landau–Levich–Derjaguin's film with the drop's height h as the meniscus's scale (assumed) |
| Elongation | L/W = 1 + Ca/Ca* while sliding, at most 3; 1 pinned | assumed | Le Grand et al.: sliding drops grow longer and thinner at constant height, to about 2 at the corner (their figure 8) |
| Merging | patches that touch coalesce at once | derived | coalescence takes milliseconds, under a field |
| Shedding | the drop's centre passes the clear radius, or its pull exceeds the pull-off | assumed | past the glass it is on the barrel, out of the picture |

Hand: a 3 mm³ drop with 20 µN of excess force slides at 7 cm/s (Ca 1.1·10⁻³) and lays a 4 µm film, losing 2.6 % of itself across the clear radius; with 60 µN, 22 cm/s and 5.5 %. A drop the glass's width crosses it in 20–60 ms in fast flight.

## 7. Left out, with reasons

- **Evaporation:** rain's air is near saturation; at 95 % humidity (assumed) a 10 µm trail film lasts about 100 s in fast flight's flow onto the lens and 180 s at 5 m/s (hand: Sibulkin's stagnation mass transfer, h_m = 0.763·Sc^0.4·D_v·√(k/ν), D_v 2.5·10⁻⁵ m²/s and saturated vapour 12.8 g/m³ at 15 °C, published), and a drop far longer. Drops and trails keep their water; a trail standing for minutes is the model's known excess.
- **The rotors' own air at the lens:** `AirVelocityAtCamera` is the field's air relative to the camera; the props' induced flow at the nose (between the front slipstreams) is not in it. Drops passing through the disks are taken by the shelter (§1), not deflected.
- **The camera's vibration:** a shaking mount lowers a drop's effective hysteresis; the model uses the field's mean force, not `CameraVibration`'s bands.
- **Oblique impacts:** the spread is taken round about the impact point; an oblique lamella runs further downstream.
- **Water on the barrel's rim, drops sliding back onto the glass, a drop crossing an old trail:** none tracked.
- **Rain hitting the props and flung off them:** flung in the disk's plane, above the lens.
