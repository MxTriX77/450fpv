# Reference airframes (public specs)

These are the heavy fiber-optic quads named in CLAUDE.md: **Vyriy (Вирій) 10 Opto, BOMBUS Opto, OTU and Beshketnyk (Бешкетник)**. The figures come from public product pages, and they're starting values for the drone model and the physics until the pilot says otherwise.
Figures are **published**, **pilot** (from the pilot) or **derived** (computed here).

## Published

| Airframe | Motors | Props | Frame | Mass / payload | Performance | Link | Source |
|---|---|---|---|---|---|---|---|
| Vyriy 10 (Opto build, same as "Амага 10 оптика") | Surpass Hobby Bat **S3115 900 KV**, 112 g each, max current 40 A; the maker quotes a 4.2 kg peak thrust (optimistic) | 10" | iFlight **Chimera CX10 ECO**, carbon, **452 mm wheelbase**, **383 g** | Payload up to **1.8 kg** for 10 km (other listings quote 2.5–4 kg) | Cruise **60–80 km/h** with 1.5 kg | Fiber, 10 km; camera CADDX Ratel 2 | [dron-shop VYRIY 10](https://dron-shop.com.ua/fpv/vyriy-10), [magura "Амага 10 оптика"](https://magura.in.ua/product/fpv-10-optika-10km/), [iFlight CX10 ECO](https://shop.iflight.com/Chimera-CX10-ECO-6S-Pro2067), [Surpass S3115](https://www.oyostepper.com/goods-1369-Surpass-Hobby-Bat-S3115-900KV1050kv-FPV-Drone-Motor-RC-Airplanes-Motor.html) |
| OTU-10 | 4 electric motors (model not published) | **3-blade**, 10" | 10" frame | **Maximum take-off mass 5.35 kg ± 10 %**; payload up to **2 kg** | **12 min** with payload; up to **80 km/h**; up to **50 m** altitude | Fiber, 10 km | [OTU-10](https://otu-tech.com.ua/otu10) |
| BOMBUS 10-O | not published | 10" | 10" | not published | not published | Fiber (WireDO), **10 km spool, 0.25 mm** | [BOMBUS 10-O](https://drones.com.ua/product/bombus-10-o) |
| Beshketnyk | no public specs found | | | | | | |
| Fiber module (generic) | | | | **20 km ≈ 1.75 kg** (0.27 mm fiber, air unit) | | | [youdrone module](https://youdrone.com.ua/ua/p2547999518-modul-optovoloknom-027.html) |

## From the pilot

- The pilot's airframe was a **Vyriy 10 Opto**, flown in **acro** with **0° camera uptilt**.
- **Battery:** Li-ion class, **20–35 Ah**, **6S4P or 6S5P**.
- **Fiber coil:** about **1 kg**, which is about 10 km by the module figure above (derived).
- **Cargo:** about **0.8 kg**. The pilot's "800 g cargo+battery (maybe 100–200 g)" can't include a 20–35 Ah pack, so 0.8 kg is read as cargo (derived; to confirm only if it matters).
- **Legs:** plastic, 25–35 cm, slightly soft; they sometimes cause a stumble on landing.
- **Flight:** about 10 min; the worst 47 s is clip P.

## Derived take-off mass (Vyriy 10 Opto as flown)

| Part | Mass |
|---|---|
| Frame (CX10 ECO) | 0.38 kg (published) |
| Motors, 4 × 112 g | 0.45 kg (published) |
| ESC, FC, camera, fiber transceiver, wiring, 4 × 3-blade 10" props, legs | 0.35–0.5 kg (assumed) |
| Battery 6S4P / 6S5P, 21700 cells at about 68–70 g plus packaging | 1.7–2.2 kg (derived from the pilot's config) |
| Fiber coil | about 1.0 kg (pilot) |
| Cargo | about 0.8 kg (pilot, as read above) |
| **Take-off total** | **about 4.7–5.3 kg** (derived), consistent with OTU-10's published 5.35 kg ± 10 % maximum |

The mass drops by up to about 1 kg over a flight as the fiber pays out. The battery's mass doesn't change.

## Implications (for the physics model)

- **Thrust:** 4 × S3115 900 KV on 6S with 10" 3-blade props gives an assumed realistic maximum of about 2.5–3.2 kgf per motor, well below the maker's 4.2 kgf peak. That's about 10–13 kgf in total, a **thrust-to-weight of about 2–2.6 at take-off**. The physics engineer derives the exact curve.
- **Speed:** the published cruise of 60–80 km/h (17–22 m/s) is a sanity check on the wind study's airspeed estimate from the measured pitch.
