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

## From the pilot (class level)

The pilot's own sorties aren't described here. The pilot flies **10-inch heavy fiber quads of this reference class**. That means:
- Flying **acro** with **0° camera uptilt**.
- **Batteries:** Li-ion class, **20–35 Ah**, 6S4P or 6S5P. A 35 Ah pack needs about 6S7P with 21700 cells; the pilot's "6S4P/6S5P ±" is read as the common range.
- **Fiber coils:** typically **about 1 kg**, which is about 10 km by the module figure above (derived).
- **Cargo:** in the **0.5–2 kg** range. That's the typical span for this class, and published payloads go up to 1.8–2 kg.
- **Legs:** plastic, 25–35 cm, slightly soft; they sometimes cause a stumble on landing.

## Derived take-off mass (reference class, typical loadout)

| Part | Mass |
|---|---|
| Frame (CX10 ECO) | 0.38 kg (published) |
| Motors, 4 × 112 g | 0.45 kg (published) |
| ESC, FC, camera, fiber transceiver, wiring, 4 × 3-blade 10" props, legs | 0.35–0.5 kg (assumed) |
| Battery 6S Li-ion, 20–35 Ah | 1.7–2.2 kg for 6S4P/6S5P with 21700 cells; about 3 kg for 35 Ah (about 6S7P) (derived) |
| Fiber coil | about 1.0 kg (pilot) |
| Cargo | 0.5–2 kg (class range) |
| **Take-off total** | **about 4.4–7.3 kg** across all the ranges (derived). **Typically 4.9–5.5 kg** with a 6S4P/6S5P pack and about 1 kg of cargo, around OTU-10's published 5.35 kg ± 10 % maximum |

The mass drops by up to about 1 kg over a flight as the fiber pays out. The battery's mass doesn't change.

## Implications (for the physics model)

- **Thrust:** 4 × S3115 900 KV on 6S with 10" 3-blade props gives an assumed realistic maximum of about 2.5–3.2 kgf per motor, well below the maker's 4.2 kgf peak. That's about 10–13 kgf in total, a **thrust-to-weight of about 2–2.6 at take-off**. The physics engineer derives the exact curve.
- **Speed:** the published cruise of 60–80 km/h (17–22 m/s) is a sanity check on the wind study's airspeed estimate from the measured pitch.
