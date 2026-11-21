# The fiber at the canister's exit: friction on the edge, and what real exits are like (p7d-tether)

Written for the physics engineer's p7d-tether design 17. One number there decides whether a sudden backward move after liftoff tears the line at the exit's edge: the coating's friction on the edge, `tether.json` `spool.lip_friction` μ_l = 0.3 (assumed). This note gathers what public sources give on that friction, on the law it enters, on real exits, on the payout tension, and on where the exit sits against the rear props. It extends [spool-exit-and-rear.md](spool-exit-and-rear.md) and does not repeat it.

Labels: **published** (a source I read, cited by number), **published (summary)** (seen only through a search engine's summary of the source, not checked against its text), **derived** (computed here; the script is described in §2.3), **assumed**. Body frame and symbols as in `tether.json` and design 17: E 73 GPa, glass radius r 62.5 µm, EI 8.748e-7 N·m², A_g 1.2272e-8 m² (all from `tether.json`).

## 1. Answer in short

1. **The friction (published).** No source gives an acrylate-coated fiber on aluminium, anodised aluminium or ceramic. The nearest published pairs are a cured acrylate coating film on stainless steel, **0.36–1.0** over 11 formulations (median 0.45) [1], and coated fiber on coated fiber, **0.15–0.89** kinetic over 16 fibers [2] and **0.17–0.33** static [3]. A silicone slip additive in the outer coating halves the friction, 0.33 to 0.18 [3]. The sim's 0.3 lies below every published coating-on-steel value.
2. **The law (derived, with a published check). The capstan law does not hold at the sim's edge.** A 0.3 mm edge is far smaller than the line's bend length √(EI/T), 1.7–3 mm at 0.1–0.3 N. So the line touches the edge at one point and turns through the two free elastica arms on either side, not round the edge. The friction then raises the tension far less than e^(μθ), and the bend at the edge is smaller than design 17's symmetric formula gives (§2.3). Singh (2022) reaches the same conclusion for a rod with bending stiffness on a capstan: its force ratio is always below the classical one and only returns to it as the tension grows without bound [4].
3. **Load and speed (published).** Over 0.49–1.08 N of crossed-fiber contact, the kinetic coefficient stays at 0.35–0.37 and does not rise as the load falls. Below 0.59 N it only scatters more [2]. Textile fibres follow F = kWⁿ with n between 0.67 and 1 [5, summary]. So a rise at the lowest loads is possible but unmeasured for this coating. Use Amontons' law (constant μ) at the edge.
4. **Real exits (published).** Drone canisters let the line out through an **aluminium-alloy outlet nozzle**: a 5 cm one on one listing [6], and on GetFPV's canisters [7, summary]. One vendor recommends **ceramic** or hardened metal, and calls a soft plastic outlet that the line grooves "the most common failure location" [8]. The older guided canisters use a **small eyelet in the end wall** [9], a **360° flared opening** so the vehicle can turn without bending the fiber [10], a payout tube whose ends are "slightly flared" to cut breakage [11], or a **conical necked-down fairlead** into a payout tube (wire-guided torpedo, 120° included angle) [12]. None gives an edge radius or a liner.
5. **Payout tension (published).** The only figures are a missile canister patent's **peel force**: 5–10 g with conventional bonding, 0.01–2 g with light bonding, and "satisfactory payout" with no adhesive at all [9]. The patent calls these peel forces, not tensions measured during payout. One drone-spool vendor states a "release tension" of 3–5 N, with spikes above 10 N on outer-wound spools [13]. No method or speed is given, and 3 N would break most intact glass over a 60° turn at any edge under about 1 mm (derived, §4), which contradicts safe smooth orbits. So it is not used. A fiber vendor's engineering note says payout tension is "not published by anyone" [14].
6. **The exit against the rear props (published, plus the earlier note's measurements).** "The spool is typically attached to the bottom of the drone, underneath and away from the propellers" [15]. One spool maker's flight checklist says to keep the outlet away from the propellers [16]. The two measured 10-inch builds put the nozzle tip 0.03–0.08 m aft of the rear motor axis and 0.12–0.20 m below the arm plane, outside the rear discs' plan ([spool-exit-and-rear.md](spool-exit-and-rear.md) §2.2). The sim's exit, at 0.040 m aft and 0.184 m below, is inside that band.

**Recommendation in one line:** the sources support raising μ_l to **0.45, band 0.36–1.0**. Do it only together with the **point-contact law** at a sharp edge. With that law, no μ in the band makes the edge tear an intact section at the published peel: at 150° even μ 1.0 needs a pack resistance G·irr of 0.136 N against the band's top of 0.123 N (§2.3). So the friction number can no longer carry the pilot's "tears upon the canister hole's edge". What can is a flaw, wear, or a groove (§5).

## 2. The friction and its law

### 2.1 Published friction of acrylate-coated fiber and coating films

| Pair | Value | Conditions | Label | Source |
|---|---|---|---|---|
| Cured acrylate coating film on **stainless steel** (sled balls on film) | 0.7, 0.4, 0.8, 1.0, 0.7, 0.4 (Ex. 1–6); 0.74, 0.43, 0.36, 0.42, 0.45 (Ex. 7–12; the extract's column order is uncertain) | Instron, 100 g sled, 10 in/min (4.2 mm/s), urethane-free acrylic coatings | published | [1] |
| Same set: range, median, geometric mean | 0.36–1.0; 0.45; 0.55 | from the 11 values above | derived | [1] |
| Coating film on coating film | 0.4–1.04 | same | published | [1] |
| The same patent's finding | film-on-film friction lower than film-on-steel, "surprising" because the reverse is usual | | published | [1] |
| Coated fiber on coated fiber, crossed at 90°, **kinetic** | 0.15–0.89 over 16 fibers (median about 0.43); outer-coating moduli 410–936 MPa | 85 gf sled (0.83 N), 300 mm/min (5 mm/s), 100 mm of travel; 125 µm glass, about 245 µm coated | published | [2] |
| Same, one fiber, against load | 0.35 (50 gf), 0.36 (60), 0.37 (70–110 gf) | 300 mm/min; "variations … are large" below 60 gf; above 100 gf the sled "begins to deform the optical fiber" | published | [2] |
| Same, against speed | 0.36–0.38 over 100–700 mm/min; below 100 mm/min the sled stick-slips | 85 gf | published | [2] |
| Coated fiber on coated fiber, crossed, **static** (maximum) | 0.17–0.25 with a silicone slip additive; 0.33 without; 0.25 for an unmodified comparison coating | 0.54 N, 60 mm/min, 23 °C | published | [3] |
| Fiber on fiber, an upper bound claimed for a commercial coating | ≤ 0.74 | 0.423 cm/s, 100 g | published (summary) | [17] |
| Winding needs | μ ≥ 0.15 so that a wound package does not collapse | | published | [2] |

**Not found:** any coating-on-aluminium, coating-on-anodised-aluminium, coating-on-ceramic or coating-on-ABS value; any humidity, temperature or dust dependence for the coating; the fiber–capstan Stribeck study (nylon monofilament on aluminium and nylon capstans) [18], which is behind a paywall and was read only from its catalogue record.

**Reading (derived):** the coating's own shear strength sets the friction on a hard, smooth counterface. The counterface's material matters less than its finish. So stainless steel stands in for polished aluminium and ceramic (assumed), and the film-on-steel set [1] is the nearest published pair. Telecom fibers' outer coatings often carry slip additives, which would bring them toward the 0.17–0.25 of [3]. The drone fiber's coating chemistry is not published (G.657.A2 fixes only the glass and the coated diameter). That spread is what the band carries.

### 2.2 Does the friction rise as the load falls?

- **The edge's normal load (derived):** N ≈ (T₁ + T₂)·sin(θ/2), 0.05–0.4 N at the exit's tensions of 0.04–0.3 N and 90–180° of wrap. The upper part of this range overlaps [2]'s 0.49–1.08 N, where μ is flat or rises slightly with load.
- **Contact pressure (derived):** a 0.3 mm edge crossed with the 0.125 mm coating radius gives an effective radius of 0.19 mm. Hertz with E* 1.19 GPa (`tether.json`'s coating, assumed) at 0.1 N gives a contact radius of 23 µm and a peak pressure of 90 MPa. That is above the coating's assumed 70 MPa hardness, so the contact is close to fully plastic. In a plastic contact the area grows with the load, so μ is load-independent (Bowden and Tabor's adhesion picture). The Hertzian F ∝ N^(2/3), the case in which μ would rise as the load falls, applies only to elastic contacts [5, summary].
- **Use:** Amontons' law, constant μ (published [2], derived). If the n = 2/3 bound were taken at the lightest loads, μ would rise at most 1.8× from 0.3 N to 0.05 N (derived, (6)^(1/3)). That is labelled assumed and not recommended.
- **Ploughing (derived, an order of magnitude):** a smooth 0.3 mm round adds about 0.05–0.1 to μ by deforming the coating (0.6·√(d/R) with d 3–5 µm). A burr of tens of microns adds about 0.2. This sits inside the band of §2.1.

### 2.3 The capstan law fails at a sharp edge: the point-contact law

The capstan law T₂ = T₁·e^(μθ) assumes a line that conforms to the surface over the whole wrap. A line with bending stiffness conforms only where the surface's radius exceeds the line's own radius of curvature. At the exit's tensions that radius is 1/κ = √(EI/T)/(2·sin(φ/2)), 1.7 mm or more (derived; 1.7 mm at 0.27 N and 150°). So a 0.3 mm edge, or any edge under about 1.7 mm, gives **one-point contact** (Singh's one-point equilibria: the curvature at contact is below the capstan's [4]). The line turns through two free elastica arms: φ₁ on the pack side (tension T₁) and φ₂ on the outside (T₂), with φ₁ + φ₂ = θ. Equilibrium of the contact point, sliding outward (derived):

- curvature continuous across the contact (no point couple; the friction's moment arm, the coating radius, is about 2 % of EI·κ): √T₁·sin(φ₁/2) = √T₂·sin(φ₂/2)
- along the tangent: T₂·cos φ₂ − T₁·cos φ₁ = μ·(T₁·sin φ₁ + T₂·sin φ₂)
- the glass's stress at the edge: σ = E·r·2·sin(φ₂/2)·√(T₂/EI) + T₂/A_g, which equals the same expression with T₁ and φ₁

The higher-tension arm turns less (φ₂ < θ/2). The tangent at the contact therefore leans toward the pack side, which lowers both the friction's gain and the bend. The law reduces to the capstan law for small θ, and to T₂ = T₁ at μ = 0. A rigid sharp corner with no bending stiffness would self-lock at μ·tan(θ/2) ≥ 1. The elastica never does.

**Tension ratio T₂/T₁ (derived;** the script `pointlaw.py` and `stress.py` in the task's scratchpad, bisection on φ₁; the capstan column is e^(μθ)**):**

| θ | μ 0.3: capstan / point | μ 0.45: capstan / point | μ 0.7: capstan / point | μ 1.0: capstan / point |
|---|---|---|---|---|
| 30° | 1.17 / 1.17 | 1.27 / 1.26 | | |
| 90° | 1.60 / 1.52 | 2.03 / 1.87 | 3.00 / 2.60 | 4.81 / 3.73 |
| 120° | 1.87 / 1.67 | 2.57 / 2.14 | 4.33 / 3.15 | 8.12 / 4.79 |
| 135° | 2.03 / 1.73 | 2.89 / 2.25 | 5.20 / 3.38 | 10.55 / 5.22 |
| 150° | 2.19 / 1.77 | 3.25 / 2.33 | 6.25 / 3.55 | 13.71 / 5.55 |
| 180° | 2.57 / 1.81 | 4.11 / 2.39 | 9.02 / 3.69 | 23.14 / 5.83 |

**The pack resistance G·irr that brings the edge to the intact band's 3.0 GPa floor** (derived; quasi-static, the s̈ and ṡ² terms left out as in design 17's table). The "design 17" column reproduces design 17's figures: 0.115 N at 150° and μ 0.3.

| θ | μ | design 17 (capstan, symmetric bend) | point-contact law | σ at the band's top, G·irr 0.123 N: design 17 / point |
|---|---|---|---|---|
| 135° | 0.30 | 0.149 N | 0.234 N | 2.73 / 2.17 GPa |
| 135° | 0.45 | 0.104 | 0.211 | 3.26 / 2.28 |
| 135° | 1.00 | 0.029 | 0.161 | 6.28 / 2.61 |
| 150° | 0.30 | 0.115 | 0.194 | 3.11 / 2.38 |
| 150° | 0.45 | 0.077 | 0.175 | 3.79 / 2.51 |
| 150° | 0.70 | 0.040 | 0.153 | 5.27 / 2.68 |
| 150° | 1.00 | 0.018 | 0.136 | 7.85 / 2.85 |
| 165° | 1.00 | 0.012 | 0.119 | 9.70 / 3.05 |

**What this does to design 17's sensitivity (derived).** With the capstan law, μ 0.45 tore the backward move at the edge 8 times in 20, at 143–150° under 0.29–0.33 N. With the point law and μ 0.45, the same wrap and peel give at most 2.33 × 0.123 = 0.29 N outside and 2.5 GPa. An intact section stays whole there for every μ up to 1.0, and up to about 160° of wrap. Only a flawed section (0.69–3.0 GPa, `strength.flaw_pa`) or a worn one breaks there. A flared lip whose radius is above about 2–3 mm wraps the line instead: there the capstan law applies over the wrapped arc, with an elastica at each end, and [4] gives the corrections.

**Published check:** Singh (2022) solves the rod with bending stiffness on a frictional capstan. Its force ratios "are always lower" than the classical law's, the ratio depends on the input force itself, and the solutions "asymptotically merge with the classic solution" as the input force tends to infinity [4]. The same paper names Jung, Pan and Kang (2008) as the first capstan equation with bending rigidity and a power-law friction [19, catalogue record only].

## 3. Real exits

| Exit | What it is | Label | Source |
|---|---|---|---|
| Drone canister (one listing) | outlet nozzle "Aluminum Alloy, 5cm"; spool 130 × 404 mm | published | [6] |
| Drone canisters (10, 15, 40, 50 km) | outlet nozzle "aluminum alloy for smooth payout"; ABS shells on the smaller ones, carbon fiber on the larger | published (summary) | [7] |
| Drone canisters (a vendor's guide) | the outlet should be harder than the fiber: ceramic (Mohs 8–9) or hardened aluminium alloy. In soft plastic, flight tension "cuts grooves", whose edges act as blades; "the most common failure location"; the failure is progressive and invisible. No radius, chamfer or liner given | published (vendor's claim, untested) | [8] |
| Drone canisters, the class's builds | a straight tube nozzle 0.06–0.08 m long, about 15–20 mm across, with a flared or knob-like mouth | measured | [spool-exit-and-rear.md](spool-exit-and-rear.md) §2 |
| Missile, inside payout | a "small opening or eyelet" in the end wall and a duct; adhesive scraped off into the duct raises the drag "sharply"; some adhesive "provides lubricity" | published | [9] |
| Missile, launched from an aircraft | the canister sits in a housing whose opening is "flared throughout 360 degrees", so the vehicle can change heading "without inducing additional stress or bending on the fiber"; no angle, radius or material | published | [10] |
| Inside-payout package | a payout tube with a bore "only slightly larger" than the coated fiber, ends "slightly flared" to reduce breakage on entry and exit; bending the tube adds drag | published | [11] |
| Wire-guided torpedo | a conical necked-down fairlead (120° included angle) into a payout tube | published | [12] |
| Payout test rig (missile fiber, over 100 m/s) | a conical guide collected adhesive at its small end, and the drag grew until the fiber broke "after about 5 kilometers"; replaced by spaced rings | published | [20] |

**Not found:** an edge radius or chamfer, a liner (PTFE or other) in any drone or missile exit, a surface finish, or a measured friction for any exit material.

## 4. Payout tension

| Figure | Value | Label | Source |
|---|---|---|---|
| Peel force, conventionally bonded pack | 5–10 g (0.049–0.098 N) | published | [9] |
| Peel force, lightly bonded | 0.01–2 g (0.0001–0.0196 N); no adhesive also pays out satisfactorily | published | [9] |
| The speeds of those packs | over 100 m/s, tested up to about 300 m/s | published | [9] |
| A drone inner-wound spool's "release tension" | 3–5 N "typically"; outer-wound over 10 N in spikes; no method or speed | published (vendor's claim) | [13] |
| A fiber vendor's note | the payout tension is "not published by anyone — you have to test it" | published | [14] |
| Rewind tension of a fiber pack | about 100 g (a winding figure, not payout) | published | [21] |

**Why 3–5 N is not used (derived):** at 3 N, and with no friction at all, a line turned 60° over any edge under about 1 mm (its radius of curvature there, √(EI/T)/(2·sin 15°)) reaches σ = 2·sin 15°·E·r·√(3/EI) + 3/A_g = 4.6 GPa. That is above the strength of 80 % of intact sections (the 3.0–5.0 GPa band). Every turn of an orbit that bends the line at the exit would cut, which the pilot's account forbids. `tether.json`'s 2–10 g band covers [9]'s two upper bands. [9] supports widening the band down to about 0.01 g, and says the figures are peel forces, not payout tensions.

## 5. What the sources say the sim should change, in order

1. **The law at the lip (derived; [4] published as the check).** Replace T·e^(μθ) and the symmetric bend 2·sin(θ/4)·√(T/EI) with the point-contact law of §2.3 whenever R_edge + c is under the contact's radius of curvature 1/κ. Keep the capstan law, with elastica ends, for a lip that wraps the line. Without this change the friction number carries a load the mechanics do not give.
2. **μ_l = 0.45, band 0.36–1.0.** The 0.45 is derived (the median of [1]'s 11 published coating-on-stainless-steel values) and the band is published [1]. A slip-additive coating would sit lower, about 0.2 [3], so a draw over 0.2–1.0 is defensible if the band is drawn per flight. 0.3 lies below every published coating-on-steel value. Amontons' law holds (§2.2).
3. **The pack's band (published [9]):** 0.01–10 g, with the light-bond band 0.01–2 g as likely as the conventional 5–10 g. Drone sellers write "near zero" (design 17). No drone figure supports a pack above 10 g.
4. **The edge itself (published [8], derived).** Once items 1 and 2 are in place, the edge can tear the line only where the glass is already weak: a flaw, or coating worn at the exit. A plastic exit the line grooves is the published route [8][14]. A groove that fits the coating also raises the effective friction by the wedge effect: π/2 for a fitted semicircular groove under uniform pressure, 4/π under cosine pressure, 1/sin(β/2) for a V of angle β (derived; the V-belt law). That gives μ_eff of 0.57–0.71 from μ 0.45. The class's nozzles are aluminium [6][7] or ceramic [8], so the sim's mild Archard k on the lip stands. The severe k (a grooved plastic exit) remains the known-wrong, or a separate exit variant, not the default.
5. **Geometry (published, measured):** the exit's place against the rear discs matches the class (§1 item 6). Nothing in the sources moves it. A "flared" mouth is what every guided canister uses where the line must leave off-axis [10][11]. Its radius is unpublished, so `lip_radius_m` 0.3 mm stays assumed. With item 1, the result no longer depends on it for any radius under about 1.7 mm.

## 6. Still open

- Coating-on-aluminium, coating-on-anodised-aluminium and coating-on-ceramic friction for a G.657 fiber: no published number found. Only a bench test would give one (the ASTM D3108 yarn-to-solid capstan method at the payout speed, with a 0.3 mm and a 3 mm round).
- The coating chemistry of the fiber that drone canisters carry, and whether it has a slip additive.
- Humidity, temperature and dust effects on the coating's friction: not found.
- Any drone canister's payout tension measured with a stated method.
- Inside the nozzle, a line leaving at 135–150° may also bear on the bore wall or the collar (a second contact, Singh's two-point case [4]). Neither the sources nor this note resolve it, so the physics engineer should check it in the sim's own geometry.

## References

1. US 6,362,249 B2, radiation-curable coating compositions, coated optical fiber… (DSM); Tables 2–3, film-to-film and film-to-stainless-steel COF. https://patents.google.com/patent/US6362249B2/en
2. US 7,085,465 B2, optical fiber and method of measuring coefficient of kinetic friction of optical fiber; Tables 2–3. https://patents.google.com/patent/US7085465B2/en
3. US 6,597,846, optical fiber (outer-coating sliding friction, crossed fibers); Tables 1–3. https://patents.google.com/patent/US6597846
4. H. Singh, Planar equilibria of an elastic rod wrapped around a circular capstan, J. Elasticity (2022), arXiv:2205.04348. https://ar5iv.labs.arxiv.org/html/2205.04348 (read; the journal version: https://infoscience.epfl.ch/record/297737/files/s10659-022-09939-8.pdf, not opened)
5. Nature 172, 169 (1953), a letter on the friction of textile fibres, F = kWⁿ with n between 0.67 and 1. https://www.nature.com/articles/172169b0 (paywalled; read only through a search summary)
6. A drone fiber kit listing: outlet nozzle "Aluminum Alloy, 5cm". https://soreinpower-store.en.made-in-china.com/product/NOpfqCTZyrbs/China-Fpv-Optical-Fiber-Kit-0-3mm-0-4mm-Fiber-5km-10km-20km-30km-Uav-Fibre-Optic-Image-Data-Module-for-Optical-Communication.html
7. GetFPV fiber optic canisters, 10–50 km. https://www.getfpv.com/fiber-optic-canister-v2-15km.html (the site blocks automated reads; read through a search summary)
8. Glory Optical, FPV optic drum and cable buyer's guide (the outlet). https://www.gloryoptics.com/info/fpv-optic-drum-fpv-optic-cable-the-definiti-103503709.html
9. US 5,167,382, inside payout optical fiber canister having reduced adhesive in the optical fiber pack (Hughes). https://patents.google.com/patent/US5167382A/en
10. EP 0 443 623 B1, multi-directional payout fiber optic canister. https://patents.google.com/patent/EP0443623B1/en
11. US 5,100,078, controlling optical fiber payout from the inside of a wound package. https://patents.google.com/patent/US5100078A/en
12. US 3,831,879, wire dispenser (wire-guided torpedo). https://patents.google.com/patent/US3831879A/en
13. Zion Communication, FPV inner-winding optical fiber spools (a vendor's article). https://www.zion-communication.com/FPV-Inner-Winding-Optical-Fiber-Spools-Eliminate-Fiber-Damage-Reduce-Attenuation-Improve-Flight-Stability-id49536575.html
14. Fibconet, FPV drone fiber tensile strength and payout. https://fibconet.com/fpv-drone-fiber-tensile-strength-payout/
15. Calibre Defence, the fibre optic FPV. https://www.calibredefence.co.uk/the-fibre-optic-fpv/
16. Fiberwdm, FPV fiber optic spool (the flight checklist: keep the outlet away from the propellers; accelerate slowly at takeoff; avoid rapid deceleration and tight turns). https://www.fiberwdm.com/fpv-fiber-optic-spool_p415.html
17. US 6,584,263 B2, optical fiber coating compositions and coated optical fibers (Corning), fiber-to-fiber COF ≤ 0.74. https://patents.google.com/patent/US6584263B2/en (read through a search summary)
18. C.-F. Tu, T. Fort, et al., A study of fiber–capstan friction. 1. Stribeck curves, Tribology International 37(9), 701–710 (2004). https://www.sciencedirect.com/science/article/abs/pii/S0301679X04000441 (403; catalogue record only)
19. J. H. Jung, N. Pan, T. J. Kang, Capstan equation including bending rigidity and non-linear frictional behavior, Mechanism and Machine Theory 43(6), 661–675 (2008). https://portal.mardi4nfdi.de/wiki/Publication:925794 (catalogue record only)
20. US 5,029,959, multiple ring guide for payout testing of optical fibers. https://patents.google.com/patent/US5029959
21. US 5,257,339, package of optical fiber suitable for high speed payout. https://patents.google.com/patent/US5257339A/en
