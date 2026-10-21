# G1 gallery: the look-lock build

The first Unity build to fly through. Six stations, each built from the assets as they are today, placed and framed as your reference letters show them, under four lights. You judge one thing: **does each station look like your footage?** Your words go into `docs/style-rules.md` before the remaining assets are built in that style.

## Run it
- Double-click `unity/Builds/Windows-Gallery/450fpv-gallery.exe` (or run it from a terminal). Laptop on **AC power**: on battery it falls back to the integrated GPU.
- It opens full screen at the display's resolution, with vsync on, every time. **Alt+Enter** toggles a window.
- **Esc** (or Alt+F4) quits.
- Options for the team: `-windowed` (a 1920 × 1080 window), `-vsync 0` (vsync off, for frame-rate measurements), `-smoketest` (loads the scene, logs it, quits after one frame), `-capture <folder>` (saves every viewpoint under every light, then quits).

## Keys
| Key | Does |
|---|---|
| Mouse | look |
| W A S D | fly forward, left, back, right |
| E or Space / Q or Ctrl | up / down |
| Shift (held) | 4 × faster |
| Mouse wheel | faster / slower (0.5 to 200 m/s; starts at 5) |
| 1 to 8 | jump to a viewpoint (below); flying continues from there |
| F1 | day: sun high in the south, light cloud |
| F2 | low sun: late afternoon, sun low in the west, warm |
| F3 | overcast: flat white sky, no sun |
| F4 | night: moonless, grey sky over near-black ground |

The light's name shows for one second when you switch.

## Stations and viewpoints
The stations stand in a row along one axis, about 40 to 60 m apart. Each viewpoint copies its letter's height and framing.

| Key | Station | Letter | The view |
|---|---|---|---|
| 1 | **1 S04**: the lodged straw patch | H | 1.5 m over the patch, 35° down at the mat |
| 2 | **2 V01 V13**: a belt section, locust trees with shrubs at the edge and among the trunks | H | 1.5 m up, 8 m out from the edge, looking along it |
| 3 | **3 H01**: the adobe house, intact and damaged side by side | E | 1.5 m up, 12 m from the facade |
| 4 | **4 R01**: the sedans, struck and burnt | M | 1.35 m up, the struck car's front three-quarter from its struck side, nose left, 17° down; the burnt car stands 8 m beyond its nose. Try F1: the sun stands beyond the car |
| 5 | **5 E01**: the trench | G | 1.5 m over its lip, along the run |
| 6 | **6 L01 L03**: the wooden poles (straight, leaning, on a stub) and the wires | L | 2 m up, 15 m from the leaning pole, against the sky; try it under F4 |
| 7 | the same line | J | 2 m up, looking up along the wires against the sky |
| 8 | the belt from 200 m | A, H | 2 m up, level: does it read as a dark band against the horizon? Try F1 and F3 |

## What is not judged here
- **The ground between the stations** is the straw cover tiled to the horizon, a stand-in. The S04 patch and the trench's own ground are the judged surfaces.
- **Not in this build:** the goggles feed and its noise, the OSD, wind, rain, sounds and the drone. Everything is seen through a clean camera.
- **Known stand-ins** the team will name at the gate: the locust crowns at 200 m (their crown net comes after this look), the thin wires and battens shimmer (no antialiasing yet, so the far crowns stay dark), and the low sun without the feed's orange.

## What each station does not copy yet (the stand-ins, read out at G1)
Each line is a known gap: it is listed so you judge the rest, and each is fixed where it says, after your look unless noted. The full lists are in each change's review.

**Everywhere**
- No goggles feed yet: no PAL noise, blur, 450-line resolution, lens bow or OSD (phase 4). The footage's gold and orange come partly through the feed.
- No antialiasing: thin wires and battens shimmer, and far crowns sparkle in motion.
- No wind sway, no rain look beyond overcast.
- The ground between stations is a stand-in: S04's straw mat tiled. Only the patches and each asset's own ground are judged.

**1 S04, the straw patch (H, G)**
- Seen 35° down from 1.5 m, where H was flown 0.3–0.5 m up looking forward.
- Flat: H's mat lies over heaps and a bank 0.2–0.8 m high (the world's relief, phase 3).
- Fewer and thinner straws than H; past 4 m from the centre the mat is texture only, and the straws drop out at 8 m (the micro-detail generator).
- Bare patches read as pits with a comb of tufts; H's are trodden strips. The three earth mounds read like stones.
- Little green regrowth beyond 4 m; the mat looks slightly glossy under overcast.

**2 V01 and V13, the belt (H, F, G)**
- The locust crowns are a few long limbs with sparse leaf tufts; F's are a dense, scraggly net of fine zigzag twigs. At 200 m the belt reads as dotted crowns, not a dark band. Fixed first after G1, with the physics ruling already made.
- The H edge reads as an olive leaf band on bare stems; H is a picket of thin dark stems through sparse, mostly green leaves. Fixed with the crowns.
- Only 20 m of belt stands, two rows deep.

**3 H01, the adobe house (E, J)**
- Intact and damaged only; the ruined state and letter S's items (ceiling debris, TV mast, painted frames) follow after G1.
- The adobe texture reads more like fired brick under plaster; the roof's tiles mottle in two tones; a slight tone step where the far model takes over at 100 m.
- Further gaps from its look pass against E and J are added here when it lands.

**4 R01, the sedans (M, O)**
- Struck car: the halos read as round orange rings with drips (M's are ragged pale primer flakes); M's hole behind the headlamp and the tear at the door edge are not built (the 2 cm landing zone needs closed collision there); two extra large holes.
- The bonnet flap is a low flat triangle; M's stands tall and curls. The engine bay is a dark block.
- By day the windscreen reads clear with faint cracks; M's is frosted pale with dark hit marks.
- The wheels read as flat pale discs; the chrome bumpers as flat grey; the grille as a coarse lattice; no door mirror; the rack is plain tubes.
- Burnt car: the shell is dark grey rather than O's soot-black; the soot patch is hard-edged; the roof sag doesn't show; the doors and rack stay as they were.
- Blank plates, always (no characters).

**5 E01, the trench (G)**
- G's grass is a deep tufted golden mat; E01's is thin straws over a mottled map (the micro-detail generator).
- The lip's soil edge reads as a clean line between grass clusters; G's is hidden under grass.
- The bends are more regular than G's; the walls read smooth at 1.5 m.
- No weeds or shrubs on the parapets; no belt or field beside it (the world, phase 3).
- The parapets' pale gold is G's, so they read brighter than the floor around them.

**6 and 7 L01 and L03, the poles and wires (L, J, D)**
- Every pole is intact and weathered: no gouges, broken insulators, cut or hanging wires, or charred poles.
- One wood look; the wires are flat colours with no stranding.
- The service drop has no clamp, and its house end hangs in the air at 3.5 m.
- Not built: the lamp bracket, glass insulators and the concrete pole.
