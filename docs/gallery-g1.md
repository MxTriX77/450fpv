# G1 gallery: the look-lock build

The first Unity build to fly through. Six stations, each built from the assets as they are today, placed and framed as your reference letters show them, under four lights. You judge one thing: **does each station look like your footage?** Your words go into `docs/style-rules.md` before the remaining assets are built in that style.

## Run it
- Double-click `unity/Builds/Windows-Gallery/450fpv-gallery.exe` (or run it from a terminal). Laptop on **AC power**: on battery it falls back to the integrated GPU.
- It opens in a 1920 × 1080 window with vsync on. **Alt+Enter** toggles full screen.
- **Esc** (or Alt+F4) quits.
- Options for the team: `-vsync 0` (vsync off, for frame-rate measurements), `-smoketest` (loads the scene, logs it, quits after one frame).

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
| 1 | **1 S04**: the lodged straw patch | H | 1.5 m over the patch, 35° down; the belt behind it |
| 2 | **2 V01 V13**: a belt section, locust trees with shrubs at the edge and among the trunks | H | 1.5 m up, 8 m out from the edge, looking along it |
| 3 | **3 H01**: the adobe house, intact and damaged side by side | E | 1.5 m up, 12 m from the facade |
| 4 | **4 R01**: the sedans, struck and burnt | M | 1.2 m up, 6 m from the struck side |
| 5 | **5 E01**: the trench | G | 1.5 m over its lip, along the run |
| 6 | **6 L01 L03**: the wooden poles (straight, leaning, on a stub) and the wires | L | 2 m up, 15 m from the leaning pole, against the sky; try it under F4 |
| 7 | the same line | J | 2 m up, looking up along the wires against the sky |
| 8 | the belt from 200 m | A, H | 2 m up, level: does it read as a dark band against the horizon? Try F1 and F3 |

## What is not judged here
- **The ground between the stations** is the straw cover tiled to the horizon, a stand-in. The S04 patch and the trench's own ground are the judged surfaces.
- **Not in this build:** the goggles feed and its noise, the OSD, wind, rain, sounds and the drone. Everything is seen through a clean camera.
- **Known stand-ins** the team will name at the gate: the locust crowns at 200 m (their crown net comes after this look), the thin wires and battens shimmer (no antialiasing yet, so the far crowns stay dark), and the low sun without the feed's orange.
