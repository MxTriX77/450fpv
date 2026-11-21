# A hovering rotor's thrust below its rotation rate: level, corner and slope

For D-064 and D-068 and the physics ledger's gap 10. The sim's own-wake process (`RotorWakeSpec`, design 3e) has a published level (thrust σ 2.9 %) and a borrowed shape: first order, corner f·R/v_h = 0.2 (15 Hz at v_h 9.7 m/s, R 0.127 m). This note checks both against measurements. Labels: **published** (a cited source), **measured** (read here off a published record), **derived** (computed here), **assumed**. No footage is used. Frequencies are in Hz unless marked ω (rad/s); "/rev" is a multiple of the rotation rate.

## 0. The answer

1. **The corner is not Johnson's.** Johnson 2005, Table 2, gives ω·R/v_h with **ω in rad/s**: his own sources check only that way (Xin and Gao, 1.1 m rotor at v_h 4.24 m/s: 1.64 / 3.01 / 0.98 Hz → 1.3 / 2.5 / 0.8; Betzina, 4 ft rotor at v_h 9.4 m/s: 0.5 Hz → 0.2). The sim reads 0.2 as f·R/v_h, 2π too high. Johnson's 0.2 is f·R/v_h = 0.032, about 2.4 Hz here. And it is a vortex-ring-state number, not a hover one.
2. **The hover's own measured record still gives f·R/v_h ≈ 0.2.** Stack, Caradonna and Savaş 2004 (the source of today's level) print their hover thrust history (Fig. 7) as vector data. Read point by point (§2), its autocorrelation is a first-order process's: e^(−t/τ) with τ ≈ 2.0 revolutions = 0.78 R/v_h, at lags of 1–5 revolutions. Its spectrum is flat to about 0.08/rev and falls as f^−1.5 to about 0.2/rev. Above that the paper's undisclosed post-filter shapes it. A first-order corner of 1/(2πτ) is **f·R/v_h = 0.20 (derived)**, which is today's number. Today's shape stands, but its label and source change.
3. **The level is 1.7 %, not 2.9 %.** The same record's σ is **1.72 % of the mean** over 4.6–95 revolutions (measured), and 1.50 % / 1.36 % with the first and last 10 / 20 revolutions trimmed. Its peak to peak is 12.6 %, which matches the paper's "12 %". The 2.9 % came from reading 12 % as 4.2σ. The real ratio is 7.3σ, because the record holds only about 22 independent samples (90 revolutions over 2τ) and its kurtosis is 4.3.
4. **What it does to the bands.** The shape is unchanged, so the wake's whole contribution scales by 0.017/0.029 = **0.59 in amplitude** (derived), the same at 1–4 Hz and above 4 Hz. If the wake carries most of the camera's motion above 4 Hz, D-068's 1.6–1.9× over becomes about 0.9–1.1×. How much 1–4 Hz drops depends on the wake's share there, which only the probe can say.
5. **No measurement supports a second-order roll-off or a second corner below 1/rev.** The one measured slope above the corner, −1.5, is shallower than first order. It is read on filtered data, so the slope above f·R/v_h ≈ 0.5 is **not measured**.
6. **The rotors are independent; the ESC adds no separate low-frequency source.** No published cross-correlation between adjacent rotors' thrust exists. A full-scale wake loses coherence over 0.13 R except below 1/rev. The motor's speed at constant command wanders within ±1 % (published), and much of that is the load's own fluctuation, which the sim already couples through the torque.

## 1. Sources

| # | Source | What it gives | Opened |
|---|---|---|---|
| S1 | Stack, Caradonna, Savaş 2004, "Flow visualizations and extended thrust time histories of rotor vortex wakes in descent", AHS 4th Decennial Specialist's Conference; NASA copy https://rotorcraft.arc.nasa.gov/Publications/files/Stack_2004.pdf (ICAS 2004 version https://icas.org/ICAS_ARCHIVE/ICAS2004/PAPERS/334.PDF) | 25.4 cm three-blade rotor, 1.9 cm chord, Ω 4 rev/s, V_tip 3.19 m/s, Re_c 45–106 k, in a 60 m × 2.4 m × 1.5 m tow tank (wake along the tank); thrust from strain-gauge flexures at 200 samples a revolution, "band-pass filtered during post-processing" (band not given), first and last 5 revolutions dropped. Hover (V = 0): C_T 0.0078, 12 % peak to peak (Fig. 7). Fast shallow descent, V_x/v_h 1.62, V_z/v_h −0.93: C_T 0.0181, 12 % (Fig. 8). VRS: 50–121 %, periods of 20–50 revolutions. | yes; Figs. 7 and 8 read from the vector paths (§2) |
| S2 | Johnson 2005, NASA/TP-2005-213477, "Model for vortex ring state influence on rotorcraft flight dynamics", https://rotorcraft.arc.nasa.gov/Publications/files/Johnson%20TP-2005-213477.pdf | Table 2, measured VRS frequencies as ω·R/v_h: 0.1 to 5 (Brinson ~1 Hz → 0.62; Washizu periods of 1–2 s and 5–10 s). "The primary scaling of the frequency will be ω ~ v_h/R." The unit check is §0.1. | yes |
| S3 | Shetty and Selig 2011, AIAA 2011-1254, https://m-selig.web.engr.illinois.edu/pubs/ShettySelig-2011-AIAA-2011-1254-LRN-VSR-Props.pdf | 9–11 inch APC props at 4000 rpm in a 2.8 × 4 ft tunnel; thrust low-passed at 10 Hz (0.15/rev), 120 Hz for 90 s; static thrust "relatively steady"; ±30 % (3σ) at the VRS minimum. No spectra. The static σ is only in Fig. 11 (not read). | yes |
| S4 | Wolf et al. 2022, "Wake unsteadiness and tip vortex system of full-scale helicopters in ground effect", JAHS 67 012010, https://elib.dlr.de/143786/1/JAHS-Paper_WakeUnsteadiness.PDF (CC-BY) | Free-flying Bo105 and EC135 outdoors: the wake's velocity spectra follow −5/3 over most of the band; tip vortices pair at wake ages 270–360°; coherence of two sensors 0.13 R apart is near 0 except below 1/rev (0.035 m apart: 0.8 at low frequency, 0 by 14/rev); in a vertical takeoff the wake is hover-like until the hub passes **h ≈ 1.4 R**, then breaks into high-amplitude low-frequency oscillations, coherent below 3 Hz (about 0.4/rev). | yes |
| S5 | Wolf et al. 2024, "Volumetric wake investigation of a free-flying quadcopter using Shake-The-Box", Exp. Fluids 65:152, https://elib.dlr.de/223206/1/ELIB-Eintrag-2024-WolfCh-223206-PaperPublished.pdf | A free-flying 0.9–1.4 kg quad, v_h 4.9–6.1 m/s: ground effect on C_T below h/R 3; at h/R 3.2 a fountain under the fuselage (upwash 0.9 v_h at 1 R below); the slipstream boundary's σ_w above 0.5 v_h; in forward flight at 0.63 v_h, front and rear wakes merge earlier. No spectra or thrust time history. | yes |
| S6 | Zhou, Ning, Hu, ISPIV 2021, https://journals.library.iit.edu/index.php/ISPIV/article/download/39/52/270 | A 240 mm rotor at 81 rev/s on a JR3, 3000 Hz for 120 s: σ 0.21 N on 3.0 N over the whole band; next to a second rotor, σ × 2.4 at a 0.05 D tip gap, about the single rotor's from 0.5 D, the same at 1.0 D. The mean thrust changes by less than 2 %. No sub-rev spectrum. | yes |
| S7 | Kostek et al. 2024, JAHS 69 022009, https://elib.dlr.de/207785/1/s9.pdf | Quadrotor spacing tests: day-to-day σ of an isolated rotor's thrust about 3 %. Mean values only. | yes |
| S8 | Ma et al. 2024, Acoustics Australia 52:313, https://ira.lib.polyu.edu.hk/bitstream/10397/108873/1/s40857-024-00327-x.pdf | A free-flying 1.4 kg quad (240 mm props) in an 8.1 × 6 × 5.1 m chamber: the room's recirculation forms **about 30 s** after takeoff, seen as rotor-speed fluctuation. It cites Stephenson et al. (a 240 mm rotor on a stand: recirculation forms in about 4 s) and Weitsman et al. (the thrust envelope modulated, the rotation rate more unsteady once it forms). | yes; Stephenson and Weitsman not opened |
| S9 | Zhong et al. 2023, JFM 956 A16, https://doi.org/10.1017/jfm.2022.1097 | APC 9×5 and 11×5 at a constant target speed on a rig: speed by optical encoder at 50 Hz, (RPS − RPS₀)/RPS₀ **within ±0.01**. The speed controller is not stated. | yes |
| S10 | Mula, Stephenson, Tinney, Sirohi 2013, Exp. Fluids 54:1600, https://sites.utexas.edu/sirohi/files/2017/07/022_mula13_exif.pdf | Hover tip-vortex wander: aperiodic, growing linearly with wake age, in phase across blades. Phase-locked PIV at about 4 Hz, so no spectrum. | yes |
| — | Shukla and Komerath 2018, Drones 2(4) 43 (side-by-side rotors, SPIV) | Abstract only: thrust σ within 4 %. | no |
| — | Wolf et al. 2024's publisher page (Springer) | Paywalled; read through the DLR copy above. | no |

Nothing published gives a thrust or inflow **spectrum** for a hovering small rotor in free air, and nothing gives a cross-spectrum between neighbouring rotors. S1's printed record is the only time history of a hovering rotor's sub-rev thrust found.

## 2. Stack et al.'s hover record, read off the vector plot

The figure's curve is a PDF vector path: 17 491 points over 4.6–95.1 revolutions, 193 a revolution, matching the paper's 200. The axes come from the tick labels: 0.0015 in C_T per 21.6 pt, 2.12 pt a revolution. Resampled at 10 a revolution, with Hann-windowed Welch periodograms (three halves, 50 % overlap). The mean reads C_T 0.00765 against the paper's 0.0078, a 2 % offset from the assumed label baseline, which does not move σ/mean.

| Quantity | Hover, Fig. 7 (V = 0) | Fast shallow descent, Fig. 8 (V_x/v_h 1.62, V_z/v_h −0.93) | Label |
|---|---|---|---|
| Record | 90 rev = 22.6 s = 35 R/v_h (v_h 0.199 m/s) | 239 rev = 60 s | measured; v_h derived from C_T and V_tip |
| σ / mean, whole | **0.0172** | 0.0212 (long drift) | measured |
| σ / mean, trimmed 10–90 / 20–80 rev | 0.0150 / 0.0136 | 0.0141 / 0.0132 | measured |
| Peak to peak / mean | 0.126 (paper: 0.12) | 0.117 (paper: 0.12) | measured |
| Kurtosis | 4.3 | 2.5 | measured |
| Autocorrelation at 0.5 / 1 / 2 / 3 / 5 rev | 0.71 / 0.66 / 0.41 / 0.17 / 0.10 | 0.91 / 0.81 / 0.69 / 0.59 / 0.42 | measured |
| First-order fit, e^(−t/τ) | τ ≈ 2.0 rev = 0.78 R/v_h (e^(−t/τ) at 1 / 2 / 3 / 5 rev: 0.61 / 0.37 / 0.22 / 0.08) | none: a drift over tens of revolutions | derived |
| Corner 1/(2πτ) | **0.08/rev; f·R/v_h 0.20; ω·R/v_h 1.3** | — | derived |

Hover PSD of thrust/mean, per (1/rev), with octave-wide bands centred at:

| f, /rev | f·R/v_h | PSD | Reading |
|---|---|---|---|
| 0.024 | 0.06 | 3.9e-4 | the record's lowest bins (few averages) |
| 0.048 | 0.12 | 8.5e-4 | plateau |
| 0.067 | 0.17 | 2.2e-3 | plateau |
| 0.094 | 0.24 | 7.0e-4 | the turn |
| 0.13–0.19 | 0.34–0.48 | 4.5e-4 | f^−1.5 from 0.17 |
| 0.27 | 0.67 | 7.6e-5 | steep: the post-filter |
| 0.37 | 0.95 | 6.4e-6 | a null |
| 0.5–0.75 | 1.3–1.9 | 4.4e-5 | a side lobe: a moving average about 2.7 revolutions wide would put its first null at 0.37/rev and a −13 dB lobe here (assumed reading) |
| 1–3 | 2.7–7.5 | 2e-5 → 2e-6 | the filter's tail and the 1/rev |

So **the plateau and the turn are measured; the slope above f·R/v_h ≈ 0.5 is the paper's filter, not the rotor.** A moving average also lengthens τ, so the true corner is at 0.20 or a little above (derived). If the true spectrum stays first order past the filter, the unfiltered σ is 1.72 %/√(1 − 0.17) = **1.9 %** (derived: 17 % of a Lorentzian with its corner at 0.08/rev lies above 0.3/rev).

**How well it transfers** (derived): the Reynolds number (45–106 k against about 160 k at this prop's tip at 9000 rpm) is close. The blade count (3 against 2) changes how the tip vortices pair, which the record cannot separate. λ_h = v_h/V_tip is 0.062 against 0.083. The tank's cross-section is 9.4 D × 5.9 D, with the wake running down a 60 m length for 22 s; the paper kept hover runs short to limit recirculation. A small residual from the tank cannot be excluded (assumed small).

## 3. What sets the corner

| Mechanism | Time scale | Free-flying quad outdoors? | Label |
|---|---|---|---|
| The wake's own instability: tip vortices leapfrog and merge within a few revolutions and break down about 1 D below (S1's visualisation; S4: pairing at 270–360°; S10: wander growing with age). The disc feels a disturbance while it is within about R, carried away at about v_h. | τ ≈ 0.8 R/v_h → f·R/v_h ≈ 0.2 | **yes**: the whole of today's process | published mechanism, derived scale |
| Room recirculation: the downwash returns along the walls and is re-ingested | onset 4 s on a stand, 30 s for a free quad in an 8 × 6 × 5 m room (S8); then modulation over seconds | **no** outdoors; the sim's re-ingested wash near the ground (`GroundEffectSpec`) is the outdoor analogue | published |
| The vortex ring's shedding in descent | 20–50 rev (S1), ω·R/v_h 0.1–5 (S2) | only in descent (the sim's ring) | published |
| Ambient turbulence through the disc | the wind's spectrum | yes; already the sim's per-rotor turbulence gradient | — |

The record's τ of 0.78 R/v_h is about the time the contracted wake (about 1.4–2 v_h) needs to move 1–1.5 R, about where S1 and S4 see the vortices pair and break down. That consistency is **derived**, not a measured cause. The scaling with v_h/R rather than the rotation rate follows Johnson's argument (published). At this prop the two scalings give 15 Hz and 12 Hz (0.08/rev × 147 Hz).

## 4. Forward flight, descent and the ground

| Condition | What is measured | For the sim | Label |
|---|---|---|---|
| Forward flight, level | S1: at low advance ratio (μ < 0.01) the fluctuations are minimal whatever the descent rate. At moderate advance they are large only with descent; high advance means little wake interaction. No sub-rev level in level flight is published. | keep the Glauert fade v_i/v_h | published trend; fade assumed |
| Fast shallow descent (V_x/v_h 1.62, V_z/v_h −0.93) | S1 Fig. 8: trimmed σ 1.3–1.4 %, the same as the hover's. The plateau to about 0.08/rev lasts as long as in hover, but drifts dominate at 20+ revolutions. | at μ 1.62 the sim's fade (in-plane only) is 0.58, against a measured level about 1.0× hover's. The descent's own wake interaction offsets the fade; one point, no fit. | measured; comparison derived |
| Steep descent near the ring | S1, S2, S3: 50–121 % peak to peak, periods 20–50 rev, ω·R/v_h 0.1–5 | the sim's ring wobble (`vrs.thrust`) owns this | published |
| Near the ground | S4: hover-like structure below h ≈ 1.4 R; high-amplitude low-frequency oscillation above it in a takeoff. S5: ground effect on C_T below h/R 3, and a fountain under the fuselage at 3.2 R. No thrust spectrum vs height is published. | the sim's re-ingested wash (onset 4 R, loop T_conv) owns it; the own-wake process needs no height law | published |

## 5. Four rotors together

- **No measured cross-correlation of neighbouring rotors' thrust exists** (searched: S6, S7, Shukla and Komerath, S5).
- **Evidence for independence** (published, indirect): S4's coherence across 0.13 R of one wake is near 0 except below 1/rev. Each rotor's τ (2 revolutions) is set inside its own wake, within about R below its disc. S6: the neighbour's effect on σ is gone from a 0.5 D tip gap and absent at 1.0 D.
- **This drone's gap is 0.26 D**, between S6's 0.05 D (× 2.4, the whole band, blade-passage loading) and 0.5 D (× 1). The share of that rise below 1/rev is not published.
- **Recommendation:** keep the four independent (assumed, now with the evidence above). Do not apply S6's gain.

## 6. The motor and ESC's own share

- S9: speed within ±1 % of target at constant command (published; 50 Hz sampling, rig controller unknown). Read as 3σ, σ_n ≈ 0.3 % and σ_T ≈ 0.7 % (derived, T ∝ n²). That is an upper bound on any ESC-alone share, since it includes the speed's answer to the aerodynamic load.
- S8 and Weitsman et al. (as cited): the rotation rate's unsteadiness grows when the inflow becomes unsteady. The speed ripple follows the aerodynamics.
- The sim already passes the wake's inflow into the torque, so the motor's speed answers it through τ_m. **Add no separate ESC low-frequency source** until a published constant-command speed record from a sensorless ESC exists. (Commutation ripple is at 6–7× the electrical rate: vibration, not attitude.)

## 7. Recommendation for `RotorWakeSpec`

| Parameter | Today | Recommended | Band | Label |
|---|---|---|---|---|
| Order | first | **first** | — | derived (S1's autocorrelation, 1–5 rev) |
| Corner | f·R/v_h 0.2, cited to Johnson Table 2 (a unit error) | **f·R/v_h 0.20**, from S1 Fig. 7's τ = 0.78 R/v_h | 0.17–0.25 (τ 0.65–0.95 R/v_h; the filter may lengthen it) | derived |
| Slope above the corner | −2 | **−2** | measured −1.5 to f·R/v_h 0.5; nothing above | derived; above 0.5 assumed |
| Second corner below 1/rev | none | **none** | — | no evidence |
| Thrust σ in hover | 0.029 | **0.017** | 0.014–0.019 (trimmed, whole record, filter-corrected) | derived from published (measured off S1) |
| Bound | 3σ | 3σ; S1's kurtosis 4.3 means rare excursions past 3σ | — | assumed |
| Neighbours | independent | independent | — | assumed (§5) |
| Forward fade | Glauert v_i/v_h | keep | one descent point reads higher (§4) | published law, assumed use |

**Effect on the graded bands** (derived for the wake's own contribution; the camera's total also carries the turbulence, the wash and the motors):

| Variant | 1–4 Hz (amplitude) | Above 4 Hz (4–15 Hz) | Supported? |
|---|---|---|---|
| Recommended: first order, f·R/v_h 0.20 (15 Hz), σ 1.7 % | × 0.59 | × 0.59 | yes |
| Corner at the band's ends, 0.17 / 0.25 (13 / 19 Hz) at 1.7 % | × 0.63 / 0.53 | × 0.61 / 0.55 | yes |
| σ 1.9 % (filter-corrected) | × 0.66 | × 0.66 | yes |
| Johnson's 0.2 read correctly (ω), f·R/v_h 0.032 (2.4 Hz), σ 2.9 % | × 1.83 | × 0.86 | no: VRS data, not hover |

Band shares for a first-order process at fixed σ: (2/π)·(atan(f₂/f_c) − atan(f₁/f_c)). With f_c 15.3 Hz: 0.121 of the variance at 1–4 Hz and 0.331 at 4–15 Hz. With 2.4 Hz: 0.404 and 0.245.

**For the physics engineer, in order:**
1. Set `ThrustSigma` to 0.017 and cite S1's Fig. 7, read off the vector plot (σ 1.72 %; this note §2).
2. Keep `CornerFrequency` at 0.2 but change its citation from Johnson's Table 2 to S1's autocorrelation (derived). Johnson's ω·R/v_h is rad/s.
3. Re-run `GroundMicroMotionProbe` and the micro-motion levels against D-068's bands and `p7c-wind`'s T6d. Report the 1–4 Hz and the above-4 Hz before and after. The 0.59 is the wake's share only.
4. Re-grade the ledger's gap 10 (the throttle-punch shake's visibility line) on the new level.
5. Leave the order, the neighbours and the ESC unchanged.

## 8. What stays open

- **The slope above f·R/v_h ≈ 0.5 and the band 0.5–1/rev.** S1's post-filter hides them. Today this covers 37–147 Hz here, above the graded bands.
- **Below f·R/v_h 0.03** (about 2 Hz here), S1's 22 s record has only a few averages. The plateau is extrapolated to 1 Hz (assumed flat, as a first-order process is).
- **Two blades against three**, and a free-air record of a 10-inch prop's sub-rev thrust on a stand outdoors or in a large hall. None is published. A thrust-stand recording would settle the level and the slope at once.
- **The neighbour's sub-rev share at a 0.26 D gap.**
- **A sensorless ESC's speed at constant command, below 1/rev.**

## 9. Reproduce

Digitising used only the published PDF (S1, NASA copy), with throwaway scripts that are not in the tree:
- Inflate each content stream.
- Walk its operators with the graphics state's `cm` to collect `m`/`l` paths and the `Tm`/`Tj` tick labels.
- Keep the paths inside the plot frame: Fig. 7 on its page, the paths spanning y 170–183 pt; x 339.1 pt = revolution 0 at 2.12 pt a revolution; C_T 0.006 at the label baseline + 2.5 pt (assumed), at 0.0015 per 21.6 pt.
- Resample at 10 a revolution, and take the statistics and the Hann Welch periodograms above.

Fig. 8 is the same, with 0.849 pt a revolution.
