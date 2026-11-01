# Design: p7e-camera-feel

Numbers are labelled **pilot / measured / published / derived / assumed**.

## 1. Where the smoothness comes from (task 1.1, measured)
`CameraFeelProbe` (sim tests) flies the repository's drone (its asymmetry, rings, flapping and modes; seed 1) 3 s into a hover, then a full-throttle punch, and reports the camera's turn above 5 Hz (the shake scenario's high-pass, `VibrationBands.LowCorner`) as RMS over the window:

| Air, payload | Hover: rigid · rigid + elastic · elastic turn | Punch: rigid · rigid + elastic | Rotor 1× |
|---|---|---|---|
| Still, 0 kg | 0.0001° · 0.0001° · 0.0003° | 0.151° · 0.151° | 136 → 239 Hz |
| Still, 2 kg | 0.0002° · 0.0002° · 0.0006° | 0.078° · 0.078° | 165 → 230 Hz |
| Severe, 0 kg | 0.0033° · 0.0033° · 0.0074° | 0.207° · 0.208° (a field's span to 0.36°) | 136 → 237 Hz |
| Severe, 2 kg | 0.0023° · 0.0023° · 0.0110° | 0.129° · 0.129° | 165 → 227 Hz |

The mount's vibration bands in the hover are 0.02–0.06 m/s² RMS (measured, same runs). Against the footage (clip P, severe wind): the angles' RMS above 4 Hz is 0.13° roll, 0.05° pitch and 0.05° heading (measured, `reference-notes/wind.md` §3.3), and the shake within an exposure is ≤ 0.15° (derived there).

What made the picture smooth, in order:
1. **The render clock.** The camera took the interpolated pose at each render frame (60–144 Hz on the dev machine), so the picture moved at the monitor's rate: smoother than 50 fields a second, and its samples fell at the render's phase, not the field's.
2. **One instant per picture.** Every line was read at the same instant; a CMOS camera reads its field over 18.3 ms, so its rotation shows as skew and its vibration as wobble across the lines.
3. **The rigid body only.** The pose is the rigid body's (`AirframeModes`: "never on the pose"); the frame's elastic turn reached the gyro and `CameraAngularVelocity` but not the rendered camera. Its share is small (0.0003–0.011° RMS).
4. **The physics' own floor (not this change).** In a hover the sim's camera carries 0.0001–0.003° above 5 Hz, 40–1000× under clip P's floor. Nothing in the render can show motion the physics does not make; design 6 names the signals.
There was no filter on the camera: `PlaceCamera` set the mount with the identity rotation, and `PosePair.Interpolate` blends two 1 ms steps only.

## 2. The camera at each field's own time
- **The history** (sim core, `PoseBuffer`): every published pose also goes into a ring of 128 slots (derived: a field and the longest exposure need 41 steps, a 30 fps render frame adds 34, so 128 leaves margin), each slot under its own sequence number (a per-slot seqlock: the writer marks the slot, copies, then publishes its step; the reader checks the slot's step before and after its copy). One writer, wait-free; no allocation. `TryRead(step)` fails only for a step already overwritten or not yet published.
- **The pose carries the frame's turn:** `PoseFrame.CameraTurn` = `AirframeModes.FrameTurn(ModeShape)`, the frame's elastic turn at the camera in body axes (small angles, rad); zero for a rigid airframe. Output only: nothing in the flight reads it, and the log does not carry the pose.
- **The camera's attitude** = `Attitude × (½·CameraTurn, 1)` normalised (the same first-order quaternion as `Quat.Integrate`). Its position is the body's at the mount; the frame's elastic translation (below 0.1 mm, derived from the modes' coordinates) is left out.
- **The field latch** (`FlightScene`, FPV view): each render frame finds the newest field k whose last line is read, k = ⌊latest step / 20⌋ − 1 (the field is steps 20k…20k + 19, `FlightCore.FieldSteps`, aligned to the flight). On a new k the FPV camera renders once, at the camera's pose at the field's reference instant; otherwise it does not render and the feed reads the held picture. The feed's field is the flight's field plus an offset fixed when the flight starts, so the noise and the picture change on the same frame; while the flight is not flying or paused the feed's own clock runs on from there.
- **Latency** (derived): line r is shown from the frame after the field's end, (286 − r) × 64 µs + 0.06 ms after its read plus up to one render frame: 0.1–18.4 ms, 9.2 ms on average, plus up to 6.9 ms at 144 Hz. The goggles' scaler holds whole fields as well (bob deinterlace, `video-feed.md` §0), so the shape is the real chain's; the render clock's camera had none of it.

## 3. The camera's mount (derived)
The Ratel 2 is 19 × 19 × 20 mm and 5.9 g (published); its inertia about any axis through its centre is about m·(a² + b²)/12 = 4.2·10⁻⁷ kg·m² (derived, a uniform box). The object sheet puts it between the top plate and the upper plate at the front (assumed there).
- **Screwed into the side panels:** rotation needs the panels to bend or the clamp to slip; kN·m/rad stiffness, a resonance in the tens of kHz (derived).
- **On a thin TPU pad in the clamp** (the frame kit's TPU set, published; its use here assumed): two 19 × 8 mm pads, 2 mm thick, in torsion: k = 2·G·J/h with G = E/3 = 8.7 MPa (TPU 95A, E ≈ 26 MPa, published range 12–50 MPa) and J = A·(a² + b²)/12 = 5.4·10⁻⁹ m⁴ gives 47 N·m/rad and f = √(k/I)/2π ≈ 1.7 kHz (derived).
Either way the mount's resonance is far above the step's 500 Hz Nyquist and the rotors' 136–240 Hz: the camera rides the frame. Its static deflection under the punch's largest angular acceleration (~100 rad/s², measured order) is I·α/k ≈ 10⁻⁶ rad. So no separate mount state is added: **the soft part is the frame's**, the arms (45–65 Hz) and the strapped loads (27–37 Hz, `ShakeTests`), which the pose now carries. A cantilevered printed holder would be soft (~70 Hz, derived for 2 × 8 mm TPU arms 10 mm long); if the pilot's builds use one, its mode joins `AirframeModes` as a drone.json part, in physics.

## 4. The rolling-shutter read (`Feed/Core/RollingShutter.cs`)
- **The sensor** is CMOS (the Ratel 2's 1/1.8" Starlight sensor, published), so it reads line by line (published: the rolling shutter of CMOS FPV cameras, Oscar Liang's CMOS/CCD note).
- **Line timing:** a PAL line is 64 µs and a field has 25 blanking lines (published, 625/50). Field row r (0–286, `FeedSettings.FieldRows`) is read at t_r = (25 + r + ½) × 64 µs after the field starts (assumed: a sensor read in step with the PAL output, no frame buffer). Row 0 at 1.63 ms, the middle row 143 at 10.78 ms (the reference instant), row 286 at 19.94 ms (derived).
- **Per row:** the taps' rotations q_ref⁻¹ · q(t) for times t inside the row's exposure, interpolated between the history's 1 ms poses (normalised-linear, as `PoseFrame.Lerp`), into a 9 × 287 float texture: 8 quaternions and the tap count. Uploaded once per field (41 KB).
- **In the shader:** a picture point's pinhole ray (p_u.x, −p_u.y, f) in half-widths (f = 1.005, FEED-02) is rotated by each tap and projected back, p'_u = f·(r.x/r.z, −r.y/r.z); the taps are averaged. Rotation-only: over 20 ms at 20 m/s the camera moves 0.4 m, a parallax below a pixel beyond 40 m and left out (assumed).
- **The render margin:** with the shutter on, the render covers 4° more on every side (derived: the pilot's 300 °/s yaw at full stick, `fc-profiles.md`, over half the 18.3 ms read is 2.7°, with headroom for overshoot and shake): coverage 1.434 → 1.504 half-widths, 4.9 % more pixels.
- **Off:** the shader branch is the old read, so the FEED-00 A/B is untouched (D-026).

## 5. Motion blur within the exposure (C4)
- **The exposure:** t = K·N²/(L·S), the reflected-light meter equation (published, ISO 2720, K = 12.5 cd·s/m²), N = 2.0 (assumed: the 2.1 mm M8 lens class), S = 100 (assumed: the sensor's base sensitivity), clamped to 1/100 000–1/50 s (published, the Ratel 2's PAL shutter). Sunlight, 4096 cd/m² (derived: sunny-16 at EV 15): 1/8192 s. Heavy overcast and rain, ~256 cd/m²: 2 ms. Dusk, 8 cd/m²: 1/50 s, and the gain takes the rest (AE, `p4-feed` 1.7).
- **Unmeasured light:** `SceneLuminance` is a stub until the AE loop measures the render; its 0 means unmeasured and the exposure is the sunlight's 1/8192 s (the stand-in, labelled in the log).
- **Taps:** the row's exposure is sampled at n equally spaced times ending at its read; n = ⌈sweep⌉ clamped to 1–8, the sweep being the largest angle between the row's taps in render pixels at the centre (965 px/rad: 960 px per half-width × f, derived). Daylight's 122 µs at 300 °/s sweeps 0.6 px: one tap.

## 6. What the physics would add (named, not built here)
The camera shows the sim's motion faithfully; the hover's floor is the physics'. The signals that would raise it, each a physical cause:
- **Turbulence above 4 Hz:** `p7c-wind`'s field at the rotors (`FlightCore`'s `_hubWind`); the stand-in wind's spectrum carries little there.
- **Each prop's factory imbalance:** `MotorAsymmetry.Draws.Unbalance` (`prop.unbalance_kg_m.<corner>`, kg·m) at `BalanceGrade`; a chipped or bent blade's added unbalance from `p7f-damage` (PHY-26) on the same draw, through `Propulsion`'s U·ω² pull. The renderer reads nothing of it directly: it reaches the camera through `BodyRate` and `ModeShape`.
- **The blade pass:** each blade's thrust ripple as it crosses its arm (3 blades: 3× the rotor rate, 400–720 Hz), not modelled; it would drive the arm modes.
- **The mount's vibration bands** stay the feed's other read (`CameraVibration`, N2/N3 hooks); the picture's motion comes from the poses, never from the bands' RMS.

## 7. Cost
One quaternion rotation per scene read (36 per field pixel, ALU only) with one tap; up to 8 reads where the exposure's sweep exceeds a pixel (dim light with motion). The track's upload is 41 KB per field. Measured per PLT-07 once the GPU is free (task 1.7).
