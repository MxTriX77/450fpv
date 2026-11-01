# Test range

A small flat map, 512 m square, to look around in noclip now and to fly the first flights on later (D-044): a village yard with its house, a posadka, fields, two cars, a power line and a trench. It is made of what exists today, so much of it is a stand-in (below).

## Run it
- Double-click `unity/Builds/Windows-TestRange/450fpv-test-range.exe` (or run it from a terminal). Laptop on **AC power**: on battery it falls back to the integrated GPU.
- It opens full screen at the display's resolution, with vsync on. **Alt+Enter** toggles a window. **Esc** (or Alt+F4) quits.
- You look through the **goggles feed** from the start: the analog PAL picture with its noise, quality drops and short dropouts, as the Godot feed had them. There is no drone yet, so nothing in it reacts to motors or the fiber. The OSD is in the picture, laid out as the Godot feed's: "Svinoriz 10 Opto", "! SAFE !", the altitude, the distance from the map's origin and the flight timer from the noclip camera, and invented battery and current readouts (a full 6S pack, 0 A) until the OSD content model reads the sim. Not in it yet: the camera's own exposure and white balance, night's low-light look and drops on the lens.
- Options for the team: `-windowed` (a 1920 × 1080 window), `-vsync 0` (vsync off, for frame-rate measurements), `-smoketest` (loads, logs every placed object, quits after one frame), `-capture <folder>` (saves every number-key view under every light, then quits), `-bench` (flies the three frame-rate legs, over the yard, along the posadka at 2 m and over the fields at 50 m, logs each leg's frame rate, then quits). Captures and the bench go through the feed unless it is off:
  - `-feed 0` starts with the feed off (the clean noclip view, its frame rate and clean captures)
  - `-feedwidth <n>` renders the scene at `n` samples across the picture instead of 1920 (frame-rate runs)
  - `-feedcost` measures the feed's own GPU time for 30 s at the start view and quits (exit code 1 over 1.0 ms); it needs a development build (`-executeMethod Fpv450.Build.TestRange -developmentBuild`); `-feedshutter <s>` adds the camera's rolling-shutter read (p7e-camera-feel), a 300 °/s yaw at that exposure: 0.000122 is daylight's one tap a row, 0.02 dusk's eight

## Keys
| Key | Does |
|---|---|
| Mouse | look |
| W A S D | fly forward, left, back, right |
| E or Space / Q or Ctrl | up / down |
| Shift (held) | 4 × faster |
| Mouse wheel | faster / slower (0.5 to 200 m/s; starts at 5) |
| F1 / F2 / F3 / F4 | day / low sun / overcast / night, as in the gallery |
| 1 | above the yard, looking along the posadka: where you start |
| 2 | the take-off point on the track by the yard's gate (legs on, from the ground) |
| 3 | the take-off point in the field north of the posadka, facing it (legs off, from the launch stand) |
| 4 | along the posadka's south edge at 2 m |
| 5 | the trench, 1.5 m over its lip |
| 6 | the whole map from 50 m up, from its north-west |
| V | the goggles feed off / on (off is the clean view, with a narrower lens) |
| O | the feed's review overlay: every signal it reads, its value and whether it is a stub |

## What is where
- **The track** runs east-west. **The yard** lies south of it at the map's west end: the house by the road-side fence and gate, the shed, the cellar, two fruit trees, laid out as the Godot yard was. **Its neighbours** stand west and east of it along the track: H01's intact house and, by the burnt car, its damaged one, each with its front to the track.
- **The power line** runs along the track's north side: six wooden poles (a strutted end pole, one on a concrete stub) 40 m apart, four wires.
- **The struck car** stands by the yard's gate; **the burnt car** stands on the track.
- **The posadka** runs 40 m north of the track: 300 m long, about 13 m wide, four rows of trees 1.5 to 3 m apart with bushes among them and along both edges, and one 12 m gap where a field track would cross.
- **The trench** runs 23 m along the posadka's south edge.
- **Straw patches** (S04) lie along both edges of the posadka as its verges; elsewhere the ground is the gallery's straw floor, flat.

## What is a stand-in
- **From the Godot attempt**, as they were, with no look work (D-040), each swapped for its Unity asset when that lands: the house, the shed, the cellar, the gate, the fence and the fruit trees in the yard, and in the posadka the acacia, ash, maple, young and dead trees and the round bushes (their leaves are flat green cards).
- **Unity's own**, as judged at G1: the straw patches (S04), the locusts (V01) and the leafy bushes (V13) in the posadka, the two cars (R01), the poles and wires (L01, L03) and the trench (E01); the neighbours' two houses (H01) are Unity's own too.
- **Not there yet:** the launch stand itself (the take-off point is set; the stand comes with the flight session); relief, roads and the drone.
