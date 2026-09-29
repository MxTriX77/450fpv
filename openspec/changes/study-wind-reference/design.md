# Design

## Context

- The wind clip is local and git-ignored. The stable-letter extractor (`study-flight-references`, task 4.4) assigns it the next free letter, expected to be P. It then gets stills, bursts, contact sheets, `metrics.csv` and glitch events like every other clip.
- Lens distortion is already measured: k1 ≈ 0.33 (0.30–0.34), with p_u = p_d·(1 + k1·r_d²) and r normalised to the half-width (`video-feed.md` O1). The picture is 16:9 and the effective vertical resolution is about 286 lines per field.
- There's no stick or telemetry log for the flight. The recording is the closed-loop result of wind plus the pilot's corrections.
- Agents can view stills, but "frame by frame" means a programmatic pass over every frame, with visual spot checks.

## Goals / Non-Goals

**Goals:**
- Numbers with uncertainties that a physics test can later be checked against.
- A tracker that says when it doesn't know.

**Non-Goals:**
- Full visual odometry or structure-from-motion. Horizon plus global image motion is enough for attitude and yaw rate.

## Decisions

- **Decoding via Blender, as in `extract_frames.py`,** so nothing new is installed. Frames are processed at the working resolution, with no JPEG round-trip.
- **Horizon estimation.**
  - Undistort first with the inverse radial map, so the horizon becomes a straight line.
  - Take the sky/ground boundary per column from a vertical luma and chroma gradient, with a robust line fit (RANSAC or Theil–Sen) over the columns.
  - Roll = the line angle. Pitch = the line's vertical offset converted through the vertical field of view.
  - The vertical FOV is estimated once from the horizontal FOV implied by k1 and the 16:9 aspect. It's stated as an assumption with its effect on pitch scale.
  - Confidence = inlier fraction × edge contrast, falling to near zero when the column coverage is too small.
- **Yaw rate:** phase correlation of the sky band, or the full frame when there's no sky, between consecutive undistorted frames. The horizontal shift goes through the horizontal FOV to give deg/frame, times fps.
- **Rain and occlusion:** drops and streaks on the lens break the gradient locally. The robust fit plus temporal median gating (reject jumps > 15° between frames unless neighbours agree) handles them. Rejected frames are reported, not interpolated.
- **Spectra:** Welch power spectra on segments of continuous high-confidence frames, with Hann windows and segment lengths stated. Frequencies above the frame-rate Nyquist (15 Hz) can't be seen. That limit is stated: fast motor-level shake shows up as blur, not as angle.
- **Gust events:** excursions of |roll| or |pitch| beyond 2σ, or rates beyond the 95th percentile. Report the count per minute and the durations. The threshold is stated with the results.
- **Closed-loop interpretation:** also estimate the pilot's correction bandwidth from how fast excursions decay. The targets are defined against a simulated pilot of that bandwidth, so the sim is judged like-for-like.
- **What varies per flight (orchestrator, 2026-09-24, following D-011).** The pilot picks the level: Calm, Windy or Severe. Within that level, **each flight draws its own prevailing wind direction and mean strength**, within the level's band, from the flight seed. The draw is logged in the physics log, so any flight can be replayed. Gusts, shifts and turbulence then evolve from the same seed. This keeps D-011's "always chosen by the pilot" (the level) and the pilot's "unexpected and undefined" (the wind itself). The level is never drawn at random.
- **Per-flight draw details (orchestrator confirms the physics engineer's assumptions, 2026-09-25):**
  - The prevailing direction is drawn uniformly over the compass.
  - The mean strength is drawn uniformly within the level's centre ± 25 %. That keeps the levels apart and every flight's lean inside its level's T0 band.
  - **Severe and Windy tests fly with the airframe's seeded asymmetry on,** as real drones have it. Only Calm's ceilings are judged with it off (per the spec).
- **Ownership:** the physics engineer writes the tracker and `wind.md` §1–§5. The tech-artist then adds the rain section, running after the physics engineer on the same file, so there are no conflicts.

## Risks / Trade-offs

- [Rain, low contrast or no visible horizon for long stretches] → The confidence gating and coverage are reported. If coverage is below 30 % of frames, the results are flagged as indicative, and the pilot's description is weighted accordingly.
- [The pitch scale depends on the assumed vertical FOV] → State the assumption. Roll and yaw-rate targets don't depend on it.
- [The 30 fps recording hides vibration above 15 Hz] → Estimate high-frequency shake from motion-blur width. Mark it derived.
- [One clip is one flight] → The targets carry tolerances, and the pilot will judge the result in the MVP flights anyway.
