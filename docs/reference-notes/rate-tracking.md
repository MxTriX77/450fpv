# Rate tracking: how the drone follows its flight controller's setpoint

For the pilot's model-26 feedback (D-076): "yaw and roll are currently super slow… ultra smooth and slow", while "the way the drone flies is very nice". D-076 showed the setpoint is not the cause: the pilot's real profile (rate profile 0, 50 / 220 / 0.45 roll and pitch, 80 / 240 / 0.20 yaw) is slightly gentler than the 55 / 225 / 0.34 and 85 / 260 / 0.15 model 26 flew. This note measures the response itself, on the sim core, headless. Labels: *pilot*, *measured* (on the sim, or in a log), *published*, *derived*, *assumed*. Nothing here is fixed yet.

## 0. The answer

1. **Yaw is slow, and part of it is a controller bug.** Betaflight 4.5 multiplies the yaw I gain by 2.5 whenever integrated yaw is off (published: `src/main/flight/pid_init.c`, `pidInitConfig`, right after the P/I/D/F scaling: `pidRuntime.pidCoefficient[FD_YAW].Ki *= 2.5f;` unless `use_integrated_yaw`). Integrated yaw is off by default, and none of the pilot's saved configs turns it on. The sim's `FlightController` scales yaw I like roll's and leaves out the 2.5.
   - **As built,** a full-stick yaw step reaches 90 % of its rate in 255–273 ms (0–2 kg, measured). It then hangs 14–16 % above the setpoint for most of a second, and takes 254–268 ms to stop after the stick is centred.
   - **With Betaflight's yaw I:** 147–180 ms to 90 %, 3–4 % over, 142–163 ms to stop.
   - **In the pilot's own model-26 flights** the yaw rate trails the stick's setpoint by 108–148 ms (measured, §4).
2. **Roll and pitch track the setpoint.** A full-stick step reaches 90 % in 45–62 ms, and a 360° roll completes 4–13 ms after the stick's own rate would complete it (measured, §2). In the pilot's flights the roll rate follows the setpoint 14–24 ms behind, at a gain of 1.04–1.12. **The plant and the controller are not what makes roll feel slow.** At 220–225 °/s full stick, a roll takes 1.6 s on the real drone too.
3. **What limits each axis** (§3):
   - **Roll and pitch:**
     - Over a whole manoeuvre, the setpoint itself.
     - In the first 50 ms, the motors' lag: τ_m ≈ 42–44 ms, from the rotor inertia and the winding resistance (derived).
     - The builder's filter chain adds overshoot (24–37 %) rather than delay.
   - **Yaw: the loop gain.**
     - A yaw P of 45 on a plant that gives 2.2–2.3 °/s² per PID unit through the props' drag torque is a 3.2–3.4 rad/s loop (derived).
     - So the I term has to carry the rise, and the sim's I is 2.5× too weak.
     - The yaw authority itself is the class's: 840–890 °/s² at the PID-sum limit (derived), against FpvPrep's 500–800 °/s² for these sizes (fc-profiles §3).
     - Nothing saturates as built.
   - **D-064's spreads, the rotor inertia from p7f 1.2's blades, airmode and the airframe's modes** each move these numbers by 6 ms or less.
4. **The verdict:** roll and pitch track as the class does on the builder's PIDs and filters. Yaw does not track as Betaflight would on the same settings. The wrong element is the controller's yaw I, by Betaflight's own source. The fix is in §5. It is described here, not applied.
5. **Even after the fix, the OTU builder's yaw tune (P 45, I 30, F 0) is the gentlest in the pilot's fleet.** The Beshketnyk builds the pilot also configures fly yaw P 50–80, I 55–150 and F 45–100 (§4b). That is a choice of defaults (D-023), not physics, and it waits for the pilot's word on the fixed yaw.

## 1. The harness

`tools/flightbench`, `--rate-tracking` (`RateTrackingCase.cs`).

**The setup** is the player branch's `SessionSetup.Build`:
- drone.json at the loadout: legs on, the coil full, a full pack, Calm
- every airframe effect: the motors' asymmetry and the build's placement (D-064, D-066), the vortex rings, the ground effect, each rotor's wake, flapping, the interference and the modes

**The flight:**
- 100 m over the floor world, held 6 s by the closed-loop pilot (`SimPilot`).
- Then 0.2 s with the sticks centred at the last second's mean throttle.
- Then a stick step on one axis, timed to an RC frame, held 2.0 s (roll and pitch, full stick), 1.2 s (yaw, full stick) or 1.0 s (half stick), then centred for 0.6 s.
- Each cell is the mean of three seeds (0x450F–0x4511).
- The rate is the rigid body's, in the controller's axes.

**The settings:**
- **Player default:** D-076's `FcSettingsStore.PlayerDefault` on `unity/game/p6-input-app--player`: 50 / 220 / 0.45 and 80 / 240 / 0.20, deadband 4 / 5, smoothing auto 80, feedforward 80 / 80 / 0 with transition 40, jitter 12 and boost 0, on the builder's PIDs and filters (fc-profiles §1).
- **Model 26 as flown:** the pilot's own `fc-settings.json` on the build machine, the sim default column.
- **The throttle curve is flown straight (50 / 0).** `unity/main`'s `FlightController` cannot build `thr_mid` 100: its 12th point divides by zero. The player branch guards it.
  - The curve maps only the throttle stick to the mixer's throttle.
  - TPA, anti-gravity, the dynamic filters and the mixer all read the mixer's throttle, which the hover sets.
  - So the response is the player default's.
  - **Cross-check:** the hover's mixer throttle reads 40.3 / 46.6 / 52.7 % at 0 / 1 / 2 kg, against `LiftoffTests`' 40.4 / 46.7 / 52.7 % (fc-profiles §2b).

**The columns:**
- **RC smoothing t90:** when the controller's smoothed setpoint reaches 90 % of the stick's.
- **Rate t90:** when the drone's rate reaches 90 % of it.
- **Steady err:** the mean rate from 0.5 s to the hold's end, against the setpoint.
- **Turn:** the time to turn 360° (roll and pitch) or 180° (yaw) at full stick, and 45° at half stick. *Stick's own* is that angle at the stick's rate with no lag.
- **Stop:** from the stick centred until the rate stays within 10 % of the setpoint of zero.
- **Swing back:** the rate's largest swing past zero after that, as a share of the setpoint.
- **Axis sat:** the share of the hold during which the mixer could not deliver that axis's demand (`MixerSaturation`).

**The masses and inertias** (derived, the mass build):

| Bottle | Mass | I roll | I pitch | I yaw |
|---|---|---|---|---|
| 0 kg | 4.087 kg | 0.0358 kg·m² | 0.0369 | 0.0567 |
| 1 kg | 5.087 kg | 0.0448 | 0.0488 | 0.0604 |
| 2 kg | 6.087 kg | 0.0551 | 0.0655 | 0.0686 |

## 2. The steps (measured)

### 2.1 The player default, in a hover

Rates in °/s, times in ms.

| Bottle | Axis | Stick | Setpoint | RC smoothing t90 | Rate t90 | Peak (overshoot) | Steady err | Turn: sim / stick's own | Stop | Swing back | Axis sat |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 kg | roll | 1.0 | 220.0 | 11 | 45 | 300.7 (36.7 %) | −0.7 % | 360°: 1642 / 1636 | 205 | 45.5 % | 0 % |
| 0 kg | roll | 0.5 | 48.9 | 11 | 38 | 70.6 (44.2 %) | −3.2 % | 45°: 956 / 920 | 296 | 55.2 % | 0 % |
| 0 kg | pitch | 1.0 | 220.0 | 11 | 48 | 291.8 (32.6 %) | −0.7 % | 360°: 1640 / 1636 | 208 | 39.6 % | 0 % |
| 0 kg | pitch | 0.5 | 48.9 | 11 | 43 | 71.7 (46.6 %) | −4.0 % | 45°: 962 / 920 | 216 | 39.7 % | 0 % |
| 0 kg | yaw | 1.0 | 240.0 | 11 | **255** | 283.4 (18.1 %) | **+13.5 %** | 180°: 792 / 750 | **256** | 19.4 % | 0 % |
| 0 kg | yaw | 0.5 | 71.4 | 11 | **250** | 84.1 (17.8 %) | **+15.4 %** | 45°: 688 / 630 | **269** | 18.6 % | 0 % |
| 1 kg | roll | 1.0 | 220.0 | 11 | 49 | 292.6 (33.0 %) | −0.6 % | 360°: 1645 / 1636 | 197 | 40.3 % | 0 % |
| 1 kg | roll | 0.5 | 48.9 | 11 | 41 | 68.7 (40.3 %) | −3.1 % | 45°: 957 / 920 | 250 | 53.3 % | 0 % |
| 1 kg | pitch | 1.0 | 220.0 | 11 | 54 | 280.4 (27.4 %) | −0.6 % | 360°: 1643 / 1636 | 119 | 35.0 % | 0 % |
| 1 kg | pitch | 0.5 | 48.9 | 11 | 49 | 66.3 (35.5 %) | −3.8 % | 45°: 961 / 920 | 129 | 37.9 % | 0 % |
| 1 kg | yaw | 1.0 | 240.0 | 11 | **257** | 286.5 (19.4 %) | **+14.1 %** | 180°: 791 / 750 | **254** | 21.0 % | 0 % |
| 1 kg | yaw | 0.5 | 71.4 | 11 | **253** | 85.0 (19.0 %) | **+16.3 %** | 45°: 688 / 630 | **268** | 20.3 % | 0 % |
| 2 kg | roll | 1.0 | 220.0 | 11 | 53 | 286.2 (30.1 %) | −0.5 % | 360°: 1649 / 1636 | 170 | 36.5 % | 0 % |
| 2 kg | roll | 0.5 | 48.9 | 11 | 45 | 66.6 (36.2 %) | −3.0 % | 45°: 958 / 920 | 269 | 53.0 % | 0 % |
| 2 kg | pitch | 1.0 | 220.0 | 11 | 62 | 271.9 (23.6 %) | −0.5 % | 360°: 1646 / 1636 | 141 | 30.5 % | 0 % |
| 2 kg | pitch | 0.5 | 48.9 | 11 | 55 | 63.6 (29.9 %) | −3.7 % | 45°: 960 / 920 | 160 | 34.4 % | 0 % |
| 2 kg | yaw | 1.0 | 240.0 | 11 | **273** | 291.9 (21.6 %) | **+16.1 %** | 180°: 796 / 750 | **268** | 23.6 % | 0 % |
| 2 kg | yaw | 0.5 | 71.4 | 11 | **268** | 86.5 (21.1 %) | **+18.3 %** | 45°: 695 / 630 | **282** | 22.9 % | 0 % |

**Reading it:**
- **Roll and pitch** rise to 90 % in 38–62 ms. They overshoot 24–47 %, and a full-stick 360° is done 4–13 ms after the stick's own rate would finish it: the overshoot gives back what the rise lost.
- **At half stick** the rise is 4–8 ms quicker (38–55 ms).
  - Feedforward gives 4–5 ms of that (§3: off, 45 against 41 ms on roll at 1 kg).
  - At full stick Betaflight's max-rate limiter zeroes feedforward, since `limit = (maxRate − |setpoint|) × feedforward_max_rate_limit` is 0 there (published, rc.c `calculateFeedforward`, which the sim mirrors). So "feedforward off" changes nothing at full stick.
  - The rest is not isolated here.
- **Yaw** takes a quarter second to reach 90 %. It then overshoots for most of a second, because the I term has wound up during the slow rise and unwinds slowly. It needs 254–282 ms to stop.
  - A 180° yaw ends 41–46 ms after the stick's own 750 ms, and a 45° yaw 58–65 ms after.
- **The RC smoothing** (auto 80: 41 Hz, three PT1 stages) delivers 90 % of the setpoint in 11 ms on every axis, so it is not the delay.

### 2.2 Model 26 as the pilot flew it

The sim default column: smoothing auto 50, feedforward 50 / 50 / 0, transition 0. Full stick.

| Bottle | Axis | Setpoint | RC smoothing t90 | Rate t90 | Peak (overshoot) | Steady err | Turn: sim / stick's own | Stop |
|---|---|---|---|---|---|---|---|---|
| 0 kg | roll | 225.0 | 8 | 43 | 308.3 (37.0 %) | −0.8 % | 360°: 1605 / 1600 | 202 |
| 0 kg | yaw | 260.0 | 8 | 254 | 307.2 (18.2 %) | +13.5 % | 180°: 742 / 692 | 256 |
| 1 kg | roll | 225.0 | 8 | 47 | 299.5 (33.1 %) | −0.7 % | 360°: 1609 / 1600 | 179 |
| 1 kg | yaw | 260.0 | 8 | 256 | 310.6 (19.5 %) | +14.2 % | 180°: 741 / 692 | 254 |
| 2 kg | roll | 225.0 | 8 | 51 | 293.0 (30.2 %) | −0.6 % | 360°: 1611 / 1600 | 171 |
| 2 kg | pitch | 225.0 | 8 | 60 | 278.1 (23.6 %) | −0.6 % | 360°: 1610 / 1600 | 135 |
| 2 kg | yaw | 260.0 | 8 | 272 | 316.4 (21.7 %) | +16.2 % | 180°: 747 / 692 | 268 |

Within 1–3 ms of the player default on every axis. At half stick it is the same (`unity/Logs/rate-tracking.txt` in the worktree, not committed).

### 2.3 Yaw with Betaflight's yaw I (the player default, `i_yaw` × 2.5)

| Bottle | Stick | Rate t90 | Peak (overshoot) | Steady err | Turn: sim / stick's own | Stop | Axis sat |
|---|---|---|---|---|---|---|---|
| 0 kg | 1.0 | **147** | 286.9 (19.5 %) | +3.3 % | 180°: 748 / 750 | **142** | 2.8 % |
| 0 kg | 0.5 | **140** | 85.4 (19.6 %) | +4.6 % | 45°: 631 / 630 | **136** | 0 % |
| 1 kg | 1.0 | **154** | 292.4 (21.8 %) | +3.2 % | 180°: 745 / 750 | **147** | 7.7 % |
| 1 kg | 0.5 | **146** | 86.9 (21.6 %) | +4.5 % | 45°: 629 / 630 | **140** | 0 % |
| 2 kg | 1.0 | **180** | 303.2 (26.3 %) | +4.0 % | 180°: 743 / 750 | **163** | 16.7 % |
| 2 kg | 0.5 | **159** | 88.9 (24.4 %) | +5.5 % | 45°: 628 / 630 | **151** | 0 % |

The yaw now finishes its turn with the stick, settles within 3–6 % and stops in about 60 % of the time. At 1–2 kg, full stick, the mixer can no longer deliver all of the yaw demand for 8–17 % of the hold. That is the class's honest saturation (K§5.3: yaw saturates first), which the as-built yaw never reached.

### 2.4 At cruise (18 m/s, the player default)

`SimPilot` holds the speed 10 s first.

| Bottle | Axis | Stick | Rate t90 | Peak (overshoot) | Turn: sim / stick's own | Stop |
|---|---|---|---|---|---|---|
| 1 kg | roll | 1.0 | 50 | 295.1 (34.1 %) | 360°: 1659 / 1636 | 176 |
| 1 kg | yaw | 1.0 | 249 | 286.3 (19.3 %) | 180°: 788 / 750 | 248 |
| 2 kg | roll | 1.0 | 54 | 288.6 (31.2 %) | 360°: 1663 / 1636 | 173 |
| 2 kg | pitch | 1.0 | 61 | 273.4 (24.3 %) | 360°: 1664 / 1636 | 157 |
| 2 kg | yaw | 1.0 | 268 | 292.5 (21.9 %) | 180°: 795 / 750 | 263 |

The forward speed changes the rise by 1 ms on roll and pitch, and speeds yaw's by 5–8 ms. A full roll at speed finishes 23–28 ms after the stick, against 9–13 ms in a hover, from the rotors' in-plane drag and flapping through the roll. At half stick, the pitch's steady rate reads 6–7 % high at speed: the flapping's pitch moment adds to it.

## 3. What limits each axis (measured, full stick, the player default)

One element changed at a time. t90 in ms, and overshoot. The full table, with pitch, is in `RateTrackingCase`'s output.

| Change | Roll 1 kg | Roll 2 kg | Yaw 1 kg: t90 / stop | Yaw 2 kg: t90 / stop |
|---|---|---|---|---|
| as built | 49 (33 %) | 53 (30 %) | 257 / 254 | 273 / 268 |
| Betaflight's yaw I, × 2.5 | — | — | **154 / 147** | **180 / 163** |
| no gyro or D-term filters | 63 (10 %) | 66 (11 %) | 273 / 273 | 287 / 285 |
| feedforward off | 49 (33 %); half stick 45 against 41 | 53 (30 %); half stick 50 against 45 | 257 / 254 | 273 / 268 |
| P, I, D, D min × 1.5 | 44 (40 %) | 47 (37 %) | 207 / 193 | 216 / 205 |
| OTU10's set (§4b) and Betaflight's yaw I | 45 (38 %) | 49 (34 %) | 157 / 147 | 180 / 164 |
| Beshketnyk 10x8's yaw (§4b), Betaflight's yaw I | — | — | 147 / 139 | 176 / 166 |
| rotor inertia × 0.907 (p7f 1.2's blades) | 47 (32 %) | 51 (28 %) | 261 / 256 | 276 / 270 |
| rotor inertia × 0.5 | 38 (27 %) | 41 (20 %) | 276 / 260 | 289 / 271 |
| D-064's spreads off (the motors' and the wakes') | 49 (33 %) | 53 (31 %) | 255 / 253 | 271 / 267 |
| rigid airframe (no modes) | 49 (32 %) | 53 (30 %) | 257 / 254 | 273 / 268 |
| airmode off | 50 (32 %) | 53 (30 %) | 263 / 261 | 273 / 268 |
| `pidsum_limit_yaw` 1000 | — | — | 257 / 254 | 273 / 268 |
| drag torque κ × 1.25 | — | — | 238 / 228 | 253 / 241 |

### 3.1 By element

- **The PID gains (the OTU builder's, fc-profiles §1):**
  - Roll moves little with them. × 1.5 takes 5–6 ms off the rise and adds 6–7 points of overshoot.
  - Yaw moves most with I. Betaflight's × 2.5 on yaw I alone takes 93–103 ms off. × 1.5 on everything takes 50–57 ms off.
- **The motors and the ESCs:**
  - **The motor's lag is τ_m = J / (Kt·Ke/R + 2Q/ω): 42.7 ms at 1 kg and 41.7 ms at 2 kg** (derived, from drone.json's published Kv, its R from the published Flash Hobby 3115 bench, its derived J 6.0e-5 kg·m², and APC's published static table at hover).
  - **It sets roll's rise.** The rigid loop's own crossover would be 63–84 rad/s (derived: 30–40 °/s² of roll per PID unit times Kp 2.08).
  - **The ESC's 0.4 ms command lag** moves nothing (PropulsionTests: 0.2–0.7 ms keeps the overshoot inside 34–36 %).
- **The rotor inertia:**
  - **p7f 1.2's blade** is 2.12e-5 kg·m² against the rod's 4.30e-5 (p7f-damage tasks 1.2). With the bell, the rotor is 5.44e-5 against 6.0e-5, × 0.907. It changes roll and pitch by 2 ms and yaw by 3–4 ms, so **it does not matter here**.
    - The "half the rod" applied to the flapping's blade inertia.
    - drone.json's `rotor_inertia_kg_m2` already uses UMN's scaled measurement (4.7e-5 for the prop), not the rod's 8.6e-5.
  - **Halving it** speeds roll by 11–12 ms but *slows* yaw by 16–19 ms. Part of yaw's kick is the rotors' own spin-up reaction, J·ω̇, which halves with J.
- **Mixer saturation and airmode:**
  - **As built, no axis saturates** in the player default's steps, and no motor sits at idle or at full for more than 1 % of a hold.
  - **Airmode off** changes 0–6 ms (its mixer transition acts below 50 % throttle only).
  - **With Betaflight's yaw I,** yaw saturates 3–17 % of a full-stick hold at 0–2 kg.
- **Yaw authority from the props' drag torque:**
  - **At the yaw PID-sum limit** (400, ± 0.378 of duty), the steady drag torques give **840–890 °/s²** at 0–2 kg (derived from the DC motor model on drone.json: κ 0.0144 m, published APC 10x4.5MR). That is FpvPrep's 500–800 °/s² class figure (fc-profiles §3) or a little above it.
    - The transient peak with the spin-up reaction reaches 2100–2900 °/s² (measured).
  - **Per PID unit, yaw gets 2.2–2.3 °/s² against roll's 30–40** (derived), a ratio of 13–18.
  - **The limit lies elsewhere.**
    - κ × 1.25 takes only 19–20 ms off.
    - Raising the PID-sum limit changes nothing, because the yaw never reaches it.
    - What limits yaw is P 45's 3.2–3.4 rad/s loop (time constant 297–317 ms, derived), which the I term must lift.
- **D-064's spreads** move every number by 2 ms or less.
- **The filters' delay:**
  - **On roll and pitch,** the builder's chain shortens the rise by 13–15 ms and adds 13–24 points of overshoot (roll at 1 kg: 33 % against 10 %). The delayed D lets P act harder early.
    - PropulsionTests pins this band (20–40 %).
  - **On yaw** (P and I only) the chain is worth 14–16 ms, the other way.
  - It is not what makes either slow.

## 4. The pilot's own flights (measured)

`--rate-log` over the pilot's ten model-26 flights on the build machine: all with a 2 kg bottle, Severe wind and the sim default rates. The logs stay on that machine and are named nowhere.

**Free flight** means armed, not crashed, and no contact within 1 s either side. One flight never flew free.

**A move:**
- the stick's setpoint rising through 30 % of the axis's full rate (67 °/s roll and pitch, 78 °/s yaw) after 100 ms below half of that
- its delay runs until the rate reaches the same level

| Axis | Stick past 0.5 | Full stick | Best lag (rate behind the stick's setpoint) | Gain at that lag | Moves | Move delay: median / 90 % | Rate peak / setpoint peak | Axis sat |
|---|---|---|---|---|---|---|---|---|
| roll | 0–2.8 % of free flight | 1 flight, 0.8 % | **14–24 ms** | 1.04–1.12 (1.38 in a flight whose stick never passed 0.1) | 5 | **16 / 19 ms** | 1.12 | 0 % |
| pitch | 0–2.5 % | 1 flight, 1.1 % | 18–30 ms | 1.05–1.10 | 3 | 15 / 15 ms | 1.17 | 0 % |
| yaw | 0–2.8 % | never past 0.87 | **108–148 ms** | 0.92–1.05 | 8 | **81 / 100 ms**, 1 of 8 not reached in 0.5 s | 0.96 | 0 % |

- **The roll follows the stick within about 20 ms** in the pilot's own hands, in Severe wind with 2 kg. Nothing saturated.
- **The yaw trails by 110–150 ms.** In a banked turn the heading follows the yaw, so a turn feels slow even when the roll is crisp. That fits "yaw and roll are super slow" better than any roll measurement here does.
- **The pilot seldom used much roll stick:** past half stick for under 3 % of free flight. So the full-stick rate seldom set what they felt.

### 4b. The yaw tunes in the pilot's fleet

Read from the pilot's saved quad configs (`C:\.dev\FpvPrepConfigurator\quads`, profile 0 of each "after" dump; hardware ids and dates not copied).

| Build | Roll P / I / D / F | Yaw P / I / F |
|---|---|---|
| OTU, OTU20 (the sim's, fc-profiles §1) | 65 / 20 / 70 / 40–80 | 45 (default) / 30 / 0 |
| OTU10 | 78 / 24 / 84 / 80, the §1 set × 1.2 | 45 / 30 / 0 |
| Beshketnyk | 28–31 / 35–39 / 20–22 / 20 | 80 / 150 / 45 |
| Beshketnyk 10x8 | 34–72 / 42–60 / 24–66 / 100 | 50 / 85 / 100 |
| Beshketnyk 13x4 | 84 / 72 / 62 / 65 | 69 / 55 / 65 |

The OTU yaw has the lowest I and no feedforward. None of these configs sets `use_integrated_yaw`, so on every one of them Betaflight runs yaw I at 2.5× its value: 75 on the OTU, 137–375 on the others.

## 5. The real class

**No published blackbox logs or measured step responses of a 4–6 kg 10-inch quad were found.** The searches covered PIDtoolbox and blackbox analyses, tuning guides and multirotor system-identification papers. The comparison therefore rests on the following.

- **Betaflight 4.5.0's own code** (published, `github.com/betaflight/betaflight`, tag 4.5.0):
  - `pid_init.c` `pidInitConfig`: Kp, Ki, Kd and Kf scaled as the sim scales them, then yaw Ki × 2.5 without integrated yaw. **The sim misses this one line.**
  - `pid.c` `pidController`: the P, I (no anti-gravity on yaw), D with D min and TPA, and F terms as the sim has them.
  - `rc.c` `calculateFeedforward`: the max-rate limiter, as the sim has it.
- **The plant against published data:**
  - the motors' lag from drone.json's published motor benches (§3)
  - the yaw authority against FpvPrep's class figure (§3)
- **A published tuner's thresholds:** FPVPIDlab, an open-source Betaflight autotuner (`github.com/eddycek/fpvpidlab`, its README's flight-style thresholds).
  - *Sluggish rise time:* 120 / 80 / 50 ms for its Smooth / Balanced / Aggressive styles, yaw 1.5×, so 180 / 120 / 75 ms.
  - *Overshoot max:* 12 / 25 / 35 %.
  - Its rise-time definition is not stated there, so read these as a band, not a pass line.
  - **Against them:**
    - The sim's roll and pitch (45–62 ms) are on the brisk side, and their overshoot (24–37 %) on the lively side.
    - The as-built yaw (250–273 ms) is sluggish even for the Smooth style's 180 ms.
    - Betaflight's yaw (140–180 ms) is just inside the Smooth style's 180 ms.
- **The pilot's other builds:** §4b.

## 6. The verdict and the proposed fix

**Roll and pitch track like the class.** On the builder's PIDs and filters the sim's roll rises in 45–62 ms, ends a full roll within 1 % of the stick's own time, and follows the pilot's real stick within about 20 ms. Nothing physical or in the controller is wrong there that this measurement can see.

**Yaw does not track like Betaflight.** The element that is wrong is the controller's yaw I gain, 2.5× too weak (Betaflight's `pid_init.c`). The evidence:
- the source line
- the variant (§2.3, §3): Betaflight's yaw I alone takes the rise from 255–273 to 147–180 ms and the stop from 254–268 to 142–163 ms, and removes the floaty 14–16 % hang
- the pilot's flights (§4): a 110–150 ms yaw lag

**The fix (not applied):**
1. **In `FlightController`'s constructor, after the P/I/D/F scaling, multiply yaw's Ki by 2.5,** as `pidInitConfig` does when `use_integrated_yaw` is off. The sim does not model integrated yaw; Betaflight's default and every config of the pilot's has it off. Name it in the class's comment as Betaflight's.
2. **The flight model moves,** so:
   - a model number from the orchestrator (D-072)
   - the goldens re-recorded and audited case by case: the audit flies the old I (`i_yaw` 12 = 30 / 2.5 gives today's Ki) and must match the parent bit for bit
   - re-run the wind harness's yaw targets (K§8.6 T5) and the shake, landing and contact suites, which all fly `SimPilot` or `HarnessPilot` through the yaw loop
   - two tests pin the missing factor and move with it: `FlightControllerTests.TheGainsCarryBetaflightsScaling` (yaw Ki = ITERM_SCALE × 30) and `AsymmetryScenario`'s analytic loop model (`YawKi = 0.244381 * fc.i_yaw`)
3. **A test that pins the fact:** the yaw step's 90 % rise on the session's drone at 1 kg, 154 ms ± a band from three seeds, and the 180° yaw ending within 10 ms of the stick's own time. It fails the old I (257 ms, 41 ms late).
4. **Then ask the pilot at the next gate (qualitatively, D-024)** whether yaw now feels like their quads. If not, the next step is the yaw tune, which is a decision, not a fix. A new D-entry could take the OTU10 set or a Beshketnyk yaw (§4b) as the defaults, superseding D-023's choice of the OTU builder's.

**Not proposed:** any change to roll or pitch. If roll still feels slow after the yaw fix, the next things to measure are outside the sim core:
- the end-to-end latency from the stick to the picture (the game developer's `LatencyTest` covers the stick to the DShot command, not the feed and the display)
- what the pilot means by "roll" in a turn (the heading's lag)

## 7. How to reproduce

```
dotnet run -c Release --project tools/flightbench -- --rate-tracking
dotnet run -c Release --project tools/flightbench -- --rate-log <flight logs...>
```

`--rate-tracking` runs in about 12 s (all cells in parallel). Every number in §2 and §3 is its output. `--rate-log` reads logs of format 16 (model 26).
