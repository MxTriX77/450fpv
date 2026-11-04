# Class legs: tube section, stiffness and buckling (p7g-legs)

A check of the sim's leg tube (`drone.json` `legs.tube`: PVC-U, 10/8 mm, 0.2135 m free, clamped at the arm, a T-joint and skid at the foot) against public sources for heavy 10-inch fiber-optic cargo quads, and against what the pilot and letter T say. Labels: **pilot**, **measured** (T, `object-sheets/drone.md`), **published** (a cited source), **derived** (computed here), **assumed**.

## 1. What the evidence says

| Question | Evidence | Label |
|---|---|---|
| Material family | "plastic, 25–35 cm, a little soft"; sometimes a stumble on landing; falls on landing "a bit more often" than the sim (D-079) | pilot |
| Look of a typical class leg | T: four black tubes Ø 8–10 mm, skids Ø 9–10 mm, black blocks about 25 mm square with a wing-nut bolt under each arm, printed T-joints with 16 mm sockets, open skid ends; the sheet read the tube as carbon from its sheen | measured |
| Leg kits sold for the class (10-inch and larger) | **10 mm OD aluminium tube** legs, 200 / 250 / 300 / 350 mm, with identical moulded plastic ends at both ends that clamp to the frame ("хомут-хомут"), 148 g a set (wall not given) [1][2] | published |
| | printed PETG legs for 10-inch frames, 120–230 mm, zip-tied to flat arms [3][4]; injection-moulded polypropylene struts 125 × 50 × 10 mm, 34 g for four, for smaller quads [5] | published |
| Rigid 10 mm plastic tube exists | rigid PVC tube is sold 10 mm OD × 8 mm ID (the source `drone.json` already cites) | published |
| 16 / 20 / 25 mm tube on the class | **no public source found** (UA/RU/EN shop listings, printable-part sites, build guides searched). PVC conduit appears only in hobby *frames* (16 mm conduit arms) [6] | — |
| Electrical conduit sections | 16 mm rigid PVC: ID 13.7 (wall about 1.15) [7]; 20 mm PVC: ID 16.9 [8]; 25 mm PVC: ID 21.4, wall 1.80 [9] | published |
| PVC-U pressure pipe (EN ISO 1452) | thinnest walls: 16 × 1.5, 20 × 1.5, 25 × 1.5 mm [10]; 20 × 1.5 is PN16 [11] | published |
| Free length | pilot's 25–35 cm leg → about **0.21–0.31 m free** after the sim's 36.5 mm in the clamp and T-joint; T's 0.18 m arm-to-ground → about 0.14 m free | derived |
| Clamp | T: printed block plus wing-nut bolt (quick release); kits: moulded clamp ends [1]; printed legs: zip ties [3][4]. No source shows epoxied legs on this class | measured / published |
| T-joint and skid | T: printed T-joint, 16 mm socket, 30 mm along the skid, skid ends open, one tube stock for legs and skids | measured |
| PVC-U in the cold | modulus and tensile strength **rise** as temperature falls; impact strength **falls**: at 0 °C a PVC pipe keeps 70–90 % of its 23 °C impact strength [12]; failure in falling-weight tests is brittle in cold segments, ductile in warm ones [13]. The sim's own sheets give notched Charpy 2–6 kJ/m² (notch-sensitive) and 20 % elongation at 23 °C | published |
| GFRP (fibreglass) tube, for comparison | pultruded glass-fibre tube ≤ 13.8 mm: tensile modulus 27 GPa, flexural 30 GPa [14]; pultruded flexural strength 276–510 MPa across makers [15][16]; linear to failure, splinters | published |

**Diameter.** Both the class's photo (T, measured 8–10 mm against a 25 mm clamp block and a 16 mm T-joint socket) and the class's sold kits (10 mm OD, published) put the leg at **about 10 mm OD**. A 16, 20 or 25 mm tube would read 1.6–2.5 × T's tube and need a 22–31 mm socket; T rules it out, and no public source shows it on the class.

**Material.** The pilot says plastic; T looks like carbon; the kits are aluminium. No public source names a plastic 10 mm tube on the class, so the material is the open point (section 4).

## 2. Each candidate against the sim's 10/8 PVC-U (derived)

Closed forms: I = π(D⁴ − d⁴)/64; side stiffness at the foot k = 3EI/L³ (clamped at the arm, free at the foot); buckling: the free-sway cantilever P = π²EI/(4L²), the clamped–guided sway (both ends held square, the skid's portal) P = π²EI/L², and the clamped–pinned P = 2.046π²EI/L² that design 3 uses; first yield at the clamp M_y = σ_y·I/c and the foot's side load that reaches it, F_y = M_y/L. L = 0.2135 m unless noted. Static load at L5: 12.7 N a leg (`drone.md`).

| Candidate | OD/ID mm | E GPa | I mm⁴ | EI N·m² | k N/m | k at 0.31 m | P cantilever N | P at 0.31 m | P guided N | P clamped–pinned N | F_y N | g per 0.25 m |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **A PVC-U tube (the sim)** | 10/8 | 3.0 | 290 | 0.87 | **268** | 88 | **47** | 22 | 188 | 385 | 14.9 | 10 |
| B PVC-U solid rod (any 10 mm wall's upper bound) | 10/0 | 3.0 | 491 | 1.47 | 454 | 148 | 80 | 38 | 319 | 652 | 25 | 28 |
| C PVC conduit | 16/13.7 | 3.0 | 1488 | 4.5 | 1376 | 449 | 242 | 115 | 966 | 1977 | 44 | 19 |
| D PVC-U pressure pipe | 16/13 | 3.0 | 1815 | 5.4 | 1679 | 548 | 295 | 140 | 1179 | 2412 | 53 | 24 |
| E PVC conduit | 20/16.9 | 3.0 | 3850 | 11.6 | 3560 | 1163 | 625 | 297 | 2501 | 5116 | 90 | 32 |
| F PVC conduit | 25/21.4 | 3.0 | 8880 | 26.6 | 8212 | 2683 | 1442 | 684 | 5768 | 11801 | 166 | 47 |
| G GFRP tube | 10/8 | 27 | 290 | 7.8 | 2412 | 788 | 424 | 201 | 1694 | 3466 | about 80 (fracture) | 13 |
| H aluminium tube (the kits' OD) | 10/8 | 69 | 290 | 20.0 | 6164 | 2014 | 1082 | 513 | 4330 | 8859 | 46 | 19 |
| I CFRP tube (T's look) | 10/8 | 70 | 290 | 20.3 | 6254 | 2043 | 1098 | 521 | 4393 | 8987 | — (brittle, well above) | 11 |

Labels: OD/ID published for A, C, E, F, D; B, G, H, I take the sim's 10/8 section (assumed wall, OD measured/published). E: PVC-U 3.0 GPa published (the sim's sheet), applied to conduit compound as assumed; GFRP 27 GPa published [14]; aluminium 6063 69 GPa and σ_y 170 MPa assumed (handbook values, T5–T6 145–215 MPa); CFRP 70 GPa assumed (the sheet's). σ_y: PVC-U 55 MPa (A, B) and 50 MPa (C–F, the low published bound) published. G's F_y is a fracture load at an assumed 300 MPa flexural strength, inside [15][16]. Masses at 1440 / 1420 / 1900 / 2700 / 1550 kg/m³ (PVC published, others assumed). Everything else derived.

**What the numbers show.**
- **The sim's leg is the softest plausible 10 mm leg.** Any wall of PVC-U at 10 mm OD stays within 268–454 N/m (A–B). Only a stiffer material (G, H, I: 9–23 ×) or a fatter tube (C–F: 5–31 ×) changes that.
- **10/8 PVC-U barely stands.** Its free-sway buckling load is 47 N at 0.2135 m, 3.7 × the 12.7 N static share at L5 (1.1's 3.78 ×), but **22 N at the pilot's 35 cm leg (0.31 m free), 1.7 × the static share**, and 1.4 × at the heaviest take-off (6.46 kg, 15.8 N a leg). The sideways sway of the drone on four such legs, with the P·Δ loss at L5, is about 2 Hz at 0.2135 m and under 1 Hz at 0.31 m (derived: k(1 − P/P_cr), four legs, 5.1 kg). A real leg like that sags and wobbles visibly on the ground; the pilot's "a little soft" does not read as that.
- **First yield at 15 N of side load a leg** (A) is about the static weight share: a drifting touchdown at a friction of about 0.3 already yields the two leading legs (derived: 0.3 × 50 N on two legs is 7.5 N each, doubled by a 2 g touchdown). That is the engineer's "bends before it tips".
- **The skid is a portal in pitch.** Two legs on one skid, with rigid T-joints, cannot sway fore-aft as free cantilevers: the skid would have to pitch, which the legs' axial stiffness blocks. Fore-aft they sway clamped–guided, 12EI/L³ = **1073 N/m** and 188 N for A. Sideways nothing links the two sides (the skid only rolls on its 5 mm radius), so the 268 N/m and 47 N cantilever values hold there (derived; design 3 uses one value for both planes).

## 3. Which the evidence favours

1. **Keep the 10 mm OD.** T and the class kits agree; 16–25 mm tube is not the class (no source, and T excludes it).
2. **The material, not the section, is in doubt.** PVC-U 10/8 is the softest leg that 10 mm allows, and at the pilot's 25–35 cm it would sway-buckle under 1.4–3.7 × the drone's weight share: firm enough at 25 cm, floppy rather than "a little soft" at 35 cm. The leg most consistent with all three sources (T's 10 mm black tube read as carbon, the pilot's "plastic", a leg that stands firm and still fails on a bad landing) is a **glass-fibre (GFRP) tube, 10 mm OD**: k about 2.4 kN/m at 0.2135 m (0.8 kN/m at 0.31 m), sway buckling 424 N (201 N), linear to a splintering fracture at about 80 N of side load at the foot. That is an inference (labelled assumed): no public source names the class's leg material.
3. **If the pilot confirms plain plastic pipe**, the evidence-consistent stiffer option is still 10 mm (a thicker wall gains at most 1.7 ×, B); a 16 mm PVC tube (C–D, 5–6 ×) contradicts T but matches the pilot's words if their own legs differ from T's.
4. **Either way, model the skid's portal in pitch** (12EI/L³ fore-aft, 3EI/L³ sideways) before tuning any value: it quadruples the fore-aft stiffness and buckling load of whatever tube is chosen.
5. **"A little soft" may be the joints.** The printed clamp rotating or slipping under a hand-tight wing nut, and the skid in its T-joint, give way at loads the tube alone would not; design 3 already models both.

## 4. Open point for the pilot (qualitative, at the next gate)

- Were your legs fibreglass rod or tube (склопластик), or plain plastic pipe; and did they ever sag or wobble on the ground before take-off? (Material identity of their own kit, which no public source shows.)

## Sources

1. 10 mm aluminium tube legs, 300 mm, moulded clamp ends, 148 g: https://prom.ua/m-4387215512269270347-nozhki-alyuminievye-homut.html
2. The same family at 200 / 250 / 350 mm: https://prom.ua/Nozhki-dlya-fpv-drona.html and https://bigl.ua/sc-10459672-nozhki-dlya-fpv-drona
3. PETG legs 230 mm, zip-tie mount, flat arm: https://prom.ua/Nozhki-dlya-fpv-drona.html (listing "Ножки 230 мм … PETG")
4. PETG landing legs for 10-inch frames: https://prom.ua/p2785049590-posadochnye-nozhki-dlya.html
5. Injection-moulded polypropylene struts 125 × 50 × 10 mm, 34 g a set: https://flasharmy.com.ua/en/stojki-nozhki-dlya-prop-ua
6. 16 mm conduit as multirotor arms (hobby frame): https://www.flitetest.com/articles/cheap-pvc-pipe-multi-rotor-frame
7. 16 mm rigid smooth PVC tube, OD 16 / ID 13.7: https://xcom-shop.ru/dkc_63916_793478.html
8. 20 mm rigid PVC conduit, nominal (inner) 16.9 mm: https://kr.rs-online.com/web/p/conduit/2758794
9. 25 mm rigid PVC conduit, ID 21.4, wall 1.80, EN/IEC 61386-21: https://legrand.com/ecatalogue/en/legrand-ecat/generatepdf/21821
10. EN 1452 PVC-U minimum walls: https://www.engineeringtoolbox.com/EN-1452-PVC-U-pipes-d_2142.html
11. PVC-U pressure pipe 20 × 1.5 PN16: https://bevo.com/en-lv/pressure-pipe-pvc-u-plain-iso-pn-16-din-pn-16-1m-7021850m
12. Cold weather and PVC pipe (modulus and tensile strength rise; impact 70–90 % at 0 °C): https://www.uni-bell.org/portals/0/ResourceFile/cold-weather-no-practical-effect-on-pvc-pipe-installation-and-use.pdf
13. PVC pipe falling-weight brittle–ductile transition: https://www.pe100plus.com/PPCA/The-effect-of-residual-stresses-and-external-effects-on-the-fracture-behaviour-of-PVC-pipes-p1581.html
14. Glass-fibre tube, tensile modulus 27 GPa, flexural 30 GPa (≤ 13.8 mm): https://www.plastock.co.uk/products/glass-fibre-grey-tube
15. Pultruded round tube, flexural strength 40 000 psi (276 MPa): https://datasheets.globalspec.com/ds/u-s-plastic/48467/9ebb6d4b-91c0-4497-946c-910ce2d3f12a
16. Pultruded profile, flexural strength 510 MPa: https://njlf-frp.en.made-in-china.com/share/nopmDutEBvUr/China-FRP-Pultruded-Profile.html
