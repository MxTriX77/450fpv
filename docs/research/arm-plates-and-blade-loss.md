# Arm plates, arm failure, blade loss and the gyro: a sourced reading for p7f-damage

Reference Analyst, 2026-11-06, for `p7f-damage` (D-086: "ask science"). Research only: no sim code changed.

**Labels:** **published** (a datasheet, standard, paper or test report, cited), **derived** (calculated here from published or sim values, the calculation shown), **inference** (a reasonable reading of published facts, not measured), **assumed** (a guess with no source), **anecdotal** (forum or vendor blog: a pattern, not a number). Where a source could not be loaded in full, the note says so.

**The drone's arm, as the sheet has it** (`docs/object-sheets/drone.md`): a flat 3K carbon plate arm of the CX10 ECO class, 6 mm thick (published), 25 mm wide (assumed), 0.18 m from the plates' clamp to the motor mount (derived), sandwiched between the body plates, the motor on its top with the prop hub 0.035 m above the arm's mid-plane.

## 1. The sim's numbers today

Read from `unity/Packages/com.fpv450.sim/Runtime/Flight/AirframeModes.cs` (`AirframeModesSpec`), `ArmFlex.cs`, `FlightCore.cs` (`FlightSetup.GyroRange`) and `assets-src/drone/drone.json` at `04240fcc`.

| Sim field | Value | Its label in the code |
|---|---|---|
| `ArmModulus` (flexural E) | 55 GPa | assumed, "inside 45–70 GPa" |
| `ArmShearModulus` (G₁₂) | 4.5 GPa | assumed, "3.5–5.5 GPa" |
| `ArmFlexuralStrength` | 600 MPa | assumed (882 MPa single-ply cited) |
| `ArmShearStrength` (in-plane) | 90 MPa | assumed, "80–125 MPa" |
| `ArmDamping` (ζ, bend and twist) | 0.02 | assumed, "laminate 0.3–1 % plus the clamp's friction" |
| `ArmWidth` × `ArmThickness` × `ArmLength` | 25 × 6 mm × 0.18 m | width assumed, thickness published, length derived |
| the clamp | rigid | assumed |
| arm part's density (`drone.json` `arm_*`) | 1.55 g/cm³, 47 g an arm | published CFRP order |
| twist stiffness GK/L, bend across 3EI/L³ | 38 N·m/rad, 221 kN/m | derived |
| the arm's in-plane modes | 88 Hz (twist-led), 318 Hz (bend across) | derived |
| `FlightSetup.GyroRange` | ±2000 °/s, clipped per axis | published (Betaflight ICM-426xx driver) |
| the accelerometer | ±16 g, not read in acro | published |
| the IMU's anti-alias filter | left out (258 Hz noted) | published, not modelled |
| a lost blade's unbalance | about 0.4 kg·mm, about 260 N at hover | derived |
| the rotor's 1/rev at hover and cruise | 150–230 Hz | derived |

I checked the twist stiffness by hand: Roark's K for a 25 × 6 mm rectangle is 1.53e-9 m⁴, and G·K/L = 4.5e9 × 1.53e-9 / 0.18 = 38.2 N·m/rad (derived). It matches.

## 2. The values: woven carbon/epoxy plate

"0/90" means the woven plies' fibres run along and across the arm, the usual cut. "±45" means the fibres run at 45° to the arm.

| Property | Low | Typical | High | Source (label) |
|---|---|---|---|---|
| Tensile or flexural modulus, 0/90 woven | 55 GPa | 60 GPa | 70 GPa | [PC]: E₁ 70 GPa at Vf 50 %; [CC]: flexural 60.1 GPa and tensile 55.2 GPa, 3K 2×2 twill T300, ISO 14125; [CMH]: T700 plain weave, about 56 GPa tension (published) |
| Modulus along the arm, ±45 woven | 17 GPa | 19 GPa | 19 GPa | [PC]: 19.1 GPa fabric, 17 GPa UD at ±45 (published) |
| Modulus, quasi-isotropic (0/±45/90) | 40 GPa | 45 GPa | 50 GPa | derived: about the mean of the 0/90 and ±45 values above |
| G₁₂, 0/90 woven | 3.3 GPa | 4.0 GPa | 5.0 GPa | [E650]: 3.34 GPa, 3K 2×2 twill, ISO 14129; [CMH]: about 4 GPa, T700 plain weave, ASTM D5379 (read from the report's modulus unit); [PC]: 5 GPa (published) |
| G₁₂, ±45 woven along the arm | 30 GPa | 30 GPa | 33 GPa | [PC]: 30 GPa fabric, 33 GPa UD (published) |
| G₁₂, quasi-isotropic | 15 GPa | 17 GPa | 19 GPa | derived: E/(2(1+ν)) with E 45 GPa and ν 0.3 |
| In-plane shear strength S, 0/90 | 89 MPa | 95 MPa | 133 MPa | [E650]: 89 MPa (ISO 14129); [PC]: 90 MPa; [T300]: 95 MPa (ASTM D3518, Vf 60 %); [CMH]: 132.6 MPa mean, B-basis 124.7 (ASTM D5379, V-notch) (published) |
| Shear strain at that strength | 2 % if linear | 3–5 % | > 5 % | inference: ISO 14129 caps the reading at 5 % shear strain [ISO], so woven coupons commonly reach 5 % unbroken. The linear 90 MPa / 4.5 GPa gives 2 % (derived) |
| Interlaminar shear strength (ILSS, short beam) | 57 MPa | 60 MPa | 70 MPa | [E650]: 57 MPa (ISO 14130); [CMH]: 59.9 MPa mean, B-basis 55.3; [CC]: 69.8 MPa; [T300]: 94 MPa at Vf 60 %, an outlier for a cut plate (published) |
| Flexural strength, 0/90 | 570 MPa | 700 MPa | 882 MPa | [PC]: 600 MPa tension and 570 MPa compression; [CMH]: 918 MPa tension and 709 MPa compression; [CC]: 882 MPa flexural, single ply, ISO 14125 (published). A thick plate's bending fails on its compression face first (inference), so the lower values fit a 6 mm plate |
| The root's notch factor at the clamp's bolt holes | 0.35 | 0.7 | 0.9 | [OH]: woven or hybrid CFRP loses 10–65 % with a hole, depending on the hole's size and the width-to-diameter ratio (published spread). Typical 0.7 is assumed |
| Density | 1.48 g/cm³ | 1.55 g/cm³ | 1.60 g/cm³ | [CMH]: 1.48–1.52; [PC]: 1.60 (published) |
| Strain to failure, tension | 1.5 % | | | [CC] (published) |
| Material loss factor η, fibre direction | 0.002 | | | derived: η = ψ/2π from ψ_L 1.4 % [ADB] |
| Material loss factor η, in-plane shear | 0.009 | 0.0135 | rises with strain | derived: ψ_LT 8.5 % [ADB]; the source finds shear damping strongly nonlinear in stress (published) |
| Modal ζ, CFRP plates and a bolted CFRP frame | 0.004 | 0.007 | 0.013 | [RAFT]: plates 0.39–1.32 %, the bolted frame's modes 0.45–0.91 % (published) |
| ζ for a cantilever's twist mode, material only | 0.0045 | 0.007 | | derived: ζ = η/2 from the shear row |

**Which fits a 3K twill frame plate** (inference): the 0/90 woven rows. The vendor sources say FPV plates have 3K twill faces, and the cores are 0/90 woven or UD plies [UAVM], [FMS]. One sheet maker sells a ±45-ply layup as stiffer in torsion for X-frame arms [FMS]. So a quasi-isotropic arm exists in the class, but it is not the default. The CX10 ECO's layup is not published. Read the arm as 0/90 woven, Vf about 50 %: E 55–60 GPa, G₁₂ 3.3–5 GPa, S 90–130 MPa, ILSS 57–70 MPa, flexural 570–880 MPa, density 1.5–1.6 g/cm³.

**The twist mode moves with the layup** (derived, √G scaling of the sim's 88 Hz, ignoring the bend coupling):

| G₁₂ | Twist mode | The rotor speed whose 1/rev meets it |
|---|---|---|
| 3.34 GPa (twill, [E650]) | 76 Hz | 4550 rpm |
| 4.0 GPa (plain weave, [CMH]) | 83 Hz | 4980 rpm |
| 4.5 GPa (the sim) | 88 Hz | 5280 rpm |
| 5.0 GPa ([PC]) | 93 Hz | 5570 rpm |
| 17 GPa (quasi-isotropic) | 171 Hz | 10 260 rpm, in the hover band |
| 30 GPa (±45 arm) | 227 Hz | 13 630 rpm, in the cruise band |

With any 0/90 woven plate the twist mode lies below hover, so only a chop or a descent crosses it. A quasi-isotropic or ±45 arm puts it inside the 150–230 Hz hover and cruise 1/rev. There a lost blade would excite it at steady throttle. The layup decides the throttle band in which a damaged rotor resonates its arm.

## 3. How 10-inch arms fail, and the loads

**Published crash-test data for 10-inch FPV arms: none found.** What exists:
- **Where (anecdotal):** pilots report arms breaking at the bolt holes where the arm meets the body plates, and at the motor-mount holes [IFPV]. Arms delaminate round the clamp's screw holes, with cracks running out from the holes [IFPV]. A frame-maker blog names three places: the motor mount, mid-arm and the plate-arm junction. It ties twisting loads to ply delamination [UAVM]. (That blog's "4–6 plies" for a 4–6 mm arm is implausible at 0.2 mm a ply; read it as a pattern only.)
- **Accident report (published):** a carbon multirotor arm broke in flight and hung on its motor cable. Investigators linked it to poor fibre-resin bonding, and noted that sub-surface cracks propagate unpredictably [ATSB]. That arm was UD tube, not plate.
- **Clean break or delamination (inference):** a thin woven plate overloaded in bending breaks across its width, mostly clean, with frayed faces where the compression face buckles. Torsion and impact load the interlaminar shear, whose strength (57–70 MPa) is a half to two-thirds of the in-plane strength. So twist and repeated knocks open delaminations from the bolt holes before the plate breaks.

**The loads that break the sim's 25 × 6 mm arm at its root** (derived: Euler–Bernoulli and Roark, with the table's strengths and no notch factor; multiply by 0.35–0.9 for the bolt holes):

| Load at the motor end | Section | Strength | Force at the motor |
|---|---|---|---|
| Up or down (an arm-first landing, a motor on the ground) | flatwise, w·t²/6 = 1.5e-7 m³ | 570–882 MPa | **475–735 N** at 0.18 m |
| Sideways in the arm plane (a wall, a trunk) | edgewise, t·w²/6 = 6.25e-7 m³ | 570–882 MPa | 2.0–3.1 kN |
| Twist from a side load on the prop hub (0.035 m above the mid-plane) | Roark, T = τ·0.282·w·t² | 89–133 MPa | 650–960 N (22.6–33.8 N·m) |
| Twist from a side load on the bell (0.02 m) | as above | 89–133 MPa | 1.1–1.7 kN |

So the weakest way is flatwise, about 500 N at the motor (derived). A 4.8 kg drone stopped on one motor in 10 ms from 3 m/s takes 1.4 kN, about three times that (derived; impulse m·Δv/Δt). Arms snapping in a hard arm-first landing is what this predicts. A sideways hit has to be about four times harder to break the arm in-plane, but a hit on the hub breaks it by twist at about the flatwise load.

## 4. One blade off versus a whole prop off

**What is published:**
- **Partial blade loss on a 10-inch rotor** (Pose et al. 2024, [POSE]): an F450 quad of 1.5 kg on 1045 props and 920 KV motors, a Pixhawk (published). Cutting one tip only, 0 and 15 mm, made the IMU data extremely noisy and "resulted in several crashes during maneuvers". The peak sat at the rotor's own rate (about 83 Hz healthy) and grew with the tips' difference. Equal cuts on both tips added little vibration, about 3 % thrust loss at 5 + 5 mm. No saturation was reported, only noise.
- **Unbalance estimation** (Ghalamchi, Jia, Mueller 2020, [GHAL]): the 1/rev from an unbalanced prop on two quads and a hexacopter is measurable and estimable in flight. It grows with the unbalance and ω² (published).
- **Carbon prop failures on a hexacopter** (Gururajan et al. 2019, [SLU]): flights with failed props on up to two rotors, arm accelerometers logged. A dataset only, with no outcome table (published).
- **Whole rotor lost** (Mueller and D'Andrea 2014, [MDA]): a quad holds position on three rotors only by giving up yaw and spinning. A standard controller cannot do it (published). Sun et al. 2021 [SUN] fly it at a yaw rate above 20 rad/s with a fault-tolerant controller; without one the drone crashes (published).
- **Loss of control on Betaflight** (van Beers et al. 2024, [VB]; TU Delft dataset 2026, [TUD]): a 3-inch Betaflight quad driven into loss of control by ±2000 °/s yaw commands diverges in roll and pitch, then turns suddenly and crashes (published). TU Delft also logged flights with blade damage increased to the loss of stability, on Betaflight and a custom controller. The dataset page gives no damage thresholds.
- **A full blade off one prop in flight on a 7–13-inch X:** no controlled test found. Only anecdotes: one pilot with half a blade missing reports violent shaking, and the drone unflyable with the damage on a front arm [MAV].

**Checks of the sim's claims:**
- **Unbalance** (derived): one blade of the 16 g prop is about 7 g with its centroid near 0.38 R (0.048 m), so U ≈ 0.34 kg·mm. At 8000–9000 rpm that is 236–298 N. The sim's 0.4 kg·mm and 260 N agree. That is about 3200× the G 6.3 factory unbalance.
- **Whole prop lost** (derived): with the other three rotors at hover thrust, 11.8 N × 0.226 m about the diagonal axis on about 0.055 kg·m² gives 48 rad/s², so 45° in about 0.18 s. Betaflight's mixer can only slow this by cutting the opposite motor, which drops thrust and spins yaw. The sim's 0.28–0.34 s and 135–317 °/s of yaw fit between that open-loop bound and [MDA]'s analysis.
- **Pose's crashes and the sim's "holds at 5–8°"** (inference): per kilogram, Pose's 15 mm tip cut (about 0.04–0.06 kg·mm on 1.5 kg, the cut tip's 0.3–0.5 g assumed) is a third to a half of the sim's full blade (0.34 kg·mm on 4.8 kg), so the excitation is of the same order. Pose's crashes were in a Pixhawk's position mode, whose attitude estimator fuses the accelerometer. Acro on Betaflight reads only the gyro (the sim's note), so the same shake need not crash it. This supports "holds at steady throttle" for acro. It does not prove it: [POSE]'s crashes came in manoeuvres, not in hover.

**What the outcome depends on** (published mechanisms, inferred ranking):
1. **Throttle, through ω²:** the unbalance force grows with ω² ([GHAL]; Newton). The 1/rev frequency is rpm/60, so the throttle decides which frame mode it meets (section 2). A chop sweeps the 1/rev down through the twist mode; a punch sweeps it up.
2. **The remaining blade's mass and centroid:** U = Δm·r̄ (derived). A whole blade off is the worst; a chip scales with the mass lost times its radius.
3. **Motor kV and the pack:** these set the hover rpm, so where the 1/rev sits against the arm's modes. A 900 KV motor on 6S hovers this drone at 150–230 Hz 1/rev (the sim's derivation).
4. **The filters:** Betaflight's RPM filter notches each motor's 1/rev and harmonics, so the controller does not fight the shake. Its D-term passes what the notch leaves, and that drives motor saturation and heat. A filter set without RPM filtering ([POSE]'s Pixhawk) lets the shake into the loop (inference).
5. **The layup and the clamp:** these place the arm's twist mode, either below hover (0/90) or inside the hover band (quasi-isotropic) (section 2).

## 5. The gyro

| Chip | Full scale (Betaflight setting) | Anti-alias filter (Betaflight default) | At full scale | Mechanical | Source |
|---|---|---|---|---|---|
| ICM-42688-P | ±15.625 to ±2000 °/s; BF ±2000, 16.4 LSB/(°/s), 16-bit two's complement | 2nd-order AAF programmable 42–3979 Hz; BF NORMAL 258 Hz, OPTION_1 536 Hz, OPTION_2 997 Hz; ODR up to 32 kHz, BF 8 kHz | the output clips at the code range. Betaflight's docs say ICM gyros can suffer "overflow-inversion" at very high rates, so BF disables the PIDs past 1950 °/s | sense resonance 25–29 kHz (typ 27); 20 000 g shock | [ICM], [BF426], [BFYAW] (published) |
| BMI270 | ±125 to ±2000 °/s; BF ±2000, 16.4 LSB/(°/s) | ODR 3.2 kHz; normal mode −3 dB at 751 Hz; BF NORMAL uses OSR4: about 188 Hz by Bosch's forum (751/4, called NDA data), about 300 Hz by a user's impulse test | saturates at 0x7FFF / 0x8001 and sets a per-axis SATURATION flag (no wrap) | shocks "up to several thousand g" | [BMI], [BOSCH], [BF270] (published; the OSR4 figure soft) |
| MPU6000 | ±250 to ±2000 °/s; BF ±2000 | BF DLPF_CFG 0: 256 Hz gyro bandwidth at 8 kHz | clips; Betaflight's docs say MPU gyros need no overflow protection | — | [BF6000], [BFMPU], [BFYAW]; the 256 Hz row from InvenSense's register map, not re-read this session (published) |

**Under heavy vibration** (published facts; inference where marked):
- **Aliasing:** none of the three aliases a 150–230 Hz 1/rev at 3.2–8 kHz output with a 188–751 Hz filter in front.
- **Rectification:** the MEMS sense resonance at 25–29 kHz (ICM) is far above any rotor harmonic, so rectification from the props is unlikely (inference).
- **Attenuation:** the AAF weakens the 1/rev the loop sees. A 2nd-order 258 Hz filter (ICM, Butterworth assumed) passes 95 % at 150 Hz and 78 % at 230 Hz, with about 9° of phase at 30 Hz (derived). BMI270's OSR4 cuts harder, if its cutoff is 188 Hz.
- **Clipping:** a ±2000 °/s clip needs an angular-rate amplitude of 35 rad/s. At 150 Hz that is a 2.1° angular amplitude of the board itself, or 3.3e4 rad/s² (derived). A 4.8 kg drone's rigid body under 260 N does not get there. Only a soft-mounted FC on a resonant stack or plate could (inference). The sim's 100–214 °/s at the FC is far from the clip, and the published physics agrees.
- **The accelerometer:** ±16 g on all three under BF. In acro it is unread, so its clip changes nothing; in angle mode it would (published, inference).

## 6. The sim's numbers against the sourced ranges

| Sim value | Verdict | Suggestion |
|---|---|---|
| `ArmModulus` 55 GPa | **within** (0/90 woven 55–70) | keep, or 60 GPa as typical. A quasi-isotropic arm would be 40–50, a ±45 arm 17–19 |
| `ArmShearModulus` 4.5 GPa | **within** (0/90 woven 3.3–5.0) | 4.0 GPa as typical (the [CMH] plain weave and [E650]'s twill straddle it). Print a quasi-isotropic variant, 17 GPa: it moves the twist mode to about 171 Hz, into the hover 1/rev |
| `ArmShearStrength` 90 MPa | **within, at the low end** (89–133) | about 110 MPa typical. Also check the short-edge interlaminar shear against ILSS 57–70 MPa, which may govern before the in-plane value (inference: the short-edge stress is a large share of the peak in a 4:1 rectangle, and ILSS is about 0.6 × S) |
| Linear shear to failure (2 % strain at 90 MPa) | **outside**: woven shear softens past about 1 % and coupons reach 5 % [ISO] | break on shear strain (3–5 %), or a secant law, not on linear stress. The sim's 35° twist at the chop is already 2 % strain, past the small-angle form |
| `ArmFlexuralStrength` 600 MPa | **within** (570–882; with the notch factor 0.35–0.9, an effective 200–800 at the bolt holes) | keep 600 as the notched root's value, or 700 unnotched × 0.85 for the holes; state the notch factor |
| `ArmDamping` ζ 0.02 | **outside, high**: material ζ about 0.002–0.007 (shear-led twist about 0.007), a bolted CFRP frame's modes 0.0045–0.009 [RAFT] | ζ 0.007–0.01 for the twist, 0.005 for the bend across. Shear damping grows with strain [ADB], so at the chop's large twist an amplitude-dependent ζ is the physical model. This cuts both ways: a lower small-amplitude ζ makes the chop's resonance worse, and the nonlinear damping limits it |
| Rigid clamp | **unsourced** (no published stiffness for a sandwiched FPV arm) | keep, but print the twist mode with a compliant clamp: the plates' bending lowers the mode and adds friction damping (inference) |
| Density 1.55 g/cm³ | **within** (1.48–1.60) | keep |
| `GyroRange` ±2000 °/s | **within** (all three chips under Betaflight) | keep; a clean clip matches BMI270 and MPU6000. ICM's overflow-inversion and BF's 1950 °/s PID cut-off are worth modelling only if a flight ever nears the clip |
| The IMU's AAF left out | **outside** the as-flown chain | add a 2nd-order low-pass at 258 Hz (the ICM-42688-P default under BF); a BMI270 variant at about 188–300 Hz. A fixed filter, no randomness |
| Accelerometer ±16 g | **within** | keep |
| Lost blade's U ≈ 0.4 kg·mm, 260 N | **within** (derived 0.34 kg·mm, 236–298 N) | keep |
| A lost prop past 45° in 0.28–0.34 s | **consistent** (open-loop bound 0.18 s; [MDA]) | keep |
| A lost blade holds in acro at steady throttle | **consistent but not confirmed**: no 7–13-inch full-blade test found; [POSE]'s crashes came in a position mode, in manoeuvres | keep; ask at the gate, as 72's note does, whether the drops came with the throttle eased |

## 7. Top three suggested changes (for the physics engineer; not done here)

1. **Break the arm on shear strain or a nonlinear shear law, and add the interlaminar check.** Today the arm breaks on linear in-plane stress at 2 % strain. Woven coupons reach 5 % strain and soften past about 1 % [ISO], [CMH], [E650]; the interlaminar strength is 57–70 MPa. This decides the chop's `ArmBroken` more than any other number.
2. **Lower `ArmDamping` to about 0.007–0.01, and make the shear part grow with amplitude.** The sourced small-amplitude ζ is under 0.01 [RAFT], [ADB]; 0.02 is above the range. Pair it with item 1, and print the chop's arm load at ζ 0.005, 0.01 and with the amplitude-dependent law.
3. **Print the quasi-isotropic arm as a variant** (G₁₂ about 17 GPa, E about 45 GPa). Such an arm puts the twist mode inside the hover 1/rev, so a lost blade would resonate the arm at steady throttle instead of at the chop. Separately, add the IMU's 258 Hz 2nd-order anti-alias filter, a published fixed part of the flown chain.

## Sources
- [PC] Performance Composites, "Mechanical properties of carbon fibre composite materials" (fibre/epoxy, Vf 50–60 %, cured 120 °C, room temperature dry): http://www.performance-composites.com/carbonfibre/mechanicalproperties_2.asp
- [CC] Customised Composites, carbon fibre veneer technical data sheet, 3K 2×2 twill T300 200 gsm, 42 % resin: https://static1.squarespace.com/static/5660323be4b0860912c27bb3/t/6383811182e98a54b0dd12db/1669562642816/Technical+data+sheet+Veneer.pdf
- [CMH] Tomblin, Sherraden, Seneviratne, Raju (2002), AGATE-WP3.3-033051-134, A- and B-basis design allowables, Toray T700SC-12K-50C/#2510 plain weave (NIAR): https://thyme.ornl.gov/CFCrush/materials/cmh17/wp3.3-033051-134.pdf (its moduli are printed in a thousandfold unit, e.g. 0.056 for the 56 GPa tension modulus; G₁₂ 0.004 is read as 4 GPa)
- [E650] Toray Advanced Composites, E650 epoxy prepreg PDS, 3K 205 gsm 2×2 twill: https://toraytac-staging.azurewebsites.net/media/2afcb86b-713c-4e92-abaa-0fc9a9570c2e/nm1L-A/TAC/Documents/Data_sheets/Thermoset/UD%20tapes%20and%20prepregs/E650_Epoxy_PDS.pdf (values from the indexed text; the PDF returned 404 on fetch)
- [T300] Toray CMA, T300 data sheet (ILSS 94 MPa, in-plane shear 95 MPa at Vf 60 %): https://www.toraycma.com/wp-content/uploads/T300-Data-Sheet.pdf (values from the indexed text)
- [ISO] ISO 14129:1997, in-plane shear by the ±45° tension test, preview (shear strength at failure or at 5 % shear strain): https://webstore.ansi.org/preview-pages/ISO/preview_ISO+14129-1997.pdf
- [ADB] "Specific damping of a carbon/epoxy laminate under cyclic loading" (Adams–Bacon three-term damping: ψ_L 1.4 %, ψ_T 5.6 %, ψ_LT 8.5 %, shear nonlinear): https://link.springer.com/chapter/10.1007/978-94-011-3658-7_46 (abstract only); method from Ni, R.G., Adams, R.D. (1984) J. Compos. Mater. 18(2): 104–121
- [RAFT] "Investigation of the damping capacity of CFRP raft frames" (2022), measured plate and bolted-frame modal damping: https://pmc.ncbi.nlm.nih.gov/articles/PMC8779907/
- [OH] Khan et al. (2023), open-hole strength of carbon, Kevlar and hybrid woven laminates: https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10223104/
- [UAVM] FPV frame materials, carbon grades and failure locations (vendor blog, anecdotal): https://blog.uavmodel.com/fpv-drone-frame-materials-carbon-fiber-grades-manufacturing-and-what-actually-matters/
- [FMS] FMS Carbon, carbon sheet layouts, 0/90 versus added ±45 for X-frame arms (vendor; indexed text, the page returned 404 on fetch): https://www.fmscarbon.com/news/what-is-the-difference-of-carbon-fiber-sheet-layout
- [IFPV] IntoFPV forum, arm breakage at bolt holes and delamination round the clamp's screws (anecdotal): https://intofpv.com/t-often-breaking-parts-in-fpv-quads?pid=95726
- [ATSB] ATSB AO-2015-112, in-flight break-up of a carbon-armed multirotor: https://www.atsb.gov.au/node/10235
- [POSE] Pose, Giribet, Torre (2024), propeller damage detection, classification and estimation in multirotor vehicles, arXiv:2410.05447: https://arxiv.org/html/2410.05447
- [GHAL] Ghalamchi, Jia, Mueller (2020), real-time vibration-based propeller fault diagnosis for multicopters, IEEE/ASME T-Mech, doi:10.1109/TMECH.2019.2947250: https://urn.fi/URN:NBN:fi-fe2020070346866
- [SLU] Gururajan, Mitchell, Ebel (2019), flights of a multirotor UAS with structural faults: failures on composite propellers, Data 4(3): 128: https://www.mdpi.com/2306-5729/4/3/128
- [MDA] Mueller, M.W., D'Andrea, R. (2014), stability and control of a quadrocopter despite the complete loss of one, two, or three propellers, ICRA 2014
- [SUN] Sun, Cioffi, de Visser, Scaramuzza (2021), autonomous quadrotor flight despite rotor failure with onboard vision sensors, arXiv:2102.13406: https://arxiv.org/pdf/2102.13406.pdf
- [VB] van Beers, Solanki, de Visser (2024), a novel metric for detecting quadrotor loss-of-control, arXiv:2408.06025: https://arxiv.org/html/2408.06025
- [TUD] TU Delft (2026), raw data, propeller damage induced quadrotor loss of control: https://research.tudelft.nl/en/datasets/raw-data-propeller-damage-induced-quadrotor-loss-of-control/
- [MAV] a forum report of a quad flown with half a blade missing (anecdotal): https://mavicpilots.com/threads/yet-another-bird-attack.129354/post-1462548
- [ICM] TDK InvenSense, ICM-42688-P datasheet DS-000347 rev 1.6 (§3.1, §5.3; read from a mirror): https://pcbsync.com/wp-content/uploads/2026/07/ICM-42688-P-Datasheet-TDK.pdf
- [BMI] Bosch Sensortec, BMI270 datasheet BST-BMI270-DS000-07 (table 10, register 0x4A): https://cdn.sparkfun.com/assets/5/c/0/0/d/bst-bmi270-ds000.pdf
- [BOSCH] Bosch Sensortec community, BMI270 OSR mode behaviour: https://community.bosch-sensortec.com/t5/MEMS-sensors-forum/BMI270-OSR-mode-behaviour/m-p/52043
- [BF426] Betaflight 4.5, accgyro_spi_icm426xx.c: https://raw.githubusercontent.com/betaflight/betaflight/4.5-maintenance/src/main/drivers/accgyro/accgyro_spi_icm426xx.c
- [BF270] Betaflight 4.5, accgyro_spi_bmi270.c: https://raw.githubusercontent.com/betaflight/betaflight/4.5-maintenance/src/main/drivers/accgyro/accgyro_spi_bmi270.c
- [BF6000] Betaflight 4.5, accgyro_spi_mpu6000.c: https://raw.githubusercontent.com/betaflight/betaflight/4.5-maintenance/src/main/drivers/accgyro/accgyro_spi_mpu6000.c
- [BFMPU] Betaflight 4.5, accgyro_mpu.c (mpuGyroDLPF: NORMAL → DLPF_CFG 0): https://raw.githubusercontent.com/betaflight/betaflight/4.5-maintenance/src/main/drivers/accgyro/accgyro_mpu.c
- [BFYAW] Betaflight wiki, yaw spin recovery and gyro overflow detect: https://betaflight.com/docs/wiki/guides/current/Yaw-Spin-Recovery-and-Gyro-Overflow-Detect
