# Proposal: study-wind-reference

**Owner:** physics-engineer · **Contributor:** tech-artist (rain on the feed) · **Reviewer:** qa-engineer
**Motivation:** CLAUDE.md §2.3 says no sim has real turbulence, yet combat logistics means flying through narrow gaps and dense woods in severe wind. §2.4 describes tiny asymmetries that make the drone yank, and inertia on heavy airframes. §4.2 asks for physics that is "realistic as fuck", with wind logic.

## Why

The pilot added a clip of a real flight in **severe wind and pouring rain**, and marked it as the target for how windy flight must feel in the sim. The drone wobbled on both axes the whole time, the pilot was correcting trajectory and horizon non-stop, and the motors were working at their limit. The pilot's words: it must be "constant and dynamic physics … not just 'wind goes south so my quad goes south smoothly'".
To build a turbulence model that meets that, we first need **numbers** from the clip: how far and how fast the airframe was thrown, at which frequencies, how often, and on which axes. The pilot asked for this to be done frame by frame.

## What Changes

- A **frame-by-frame attitude tracker** (`tools/reference/track_attitude.py`, headless Blender with numpy). For every frame of a clip it estimates camera roll and pitch from the horizon, after the measured lens distortion is removed, and yaw rate from image motion. Each value carries a confidence. The output is a local, git-ignored CSV.
- **Wind reference notes** (`docs/reference-notes/wind.md`):
  - attitude disturbance statistics: angle and rate distributions, spectra, gust-event rate and duration, and cross-axis coupling
  - what they imply for turbulence and for motor headroom and saturation
  - the limits of what a closed-loop recording can show: the pilot was correcting, so the real disturbance is larger than what's visible
  - **quantitative targets** the future wind model must hit in a "severe wind" preset
- A **rain on the feed** section (tech-artist): drops, streaks, contrast and colour loss, lens-clearing by prop wash. Each is mapped to effects and simulation drivers such as rain rate, airspeed, attitude and throttle.

## Capabilities

### New Capabilities
- `reference-wind`: the attitude tracker's behaviour and accuracy, and the required content, measurements, targets and OPSEC rules of the wind notes

### Modified Capabilities
_None._

## Non-goals

- Implementing the wind, turbulence or rain simulation. The physics and video changes do that later, against these targets.
- Separating the pilot's stick inputs from the disturbance exactly. There's no stick log, so that limit is stated, not solved.
- Analysing any clip other than the wind clip, beyond spot checks for calibration.

## Impact

- New: `tools/reference/track_attitude.py`, `tools/reference/wind_stats.py` (computes every number in the notes, and later runs the same statistics on a simulated flight log for the §6 target test) and `docs/reference-notes/wind.md`
- Local only (git-ignored): `reference/_frames/<letter>/attitude.csv`
- Depends on the stable-letter extractor from `study-flight-references`, which assigns the wind clip its letter
- OPSEC: the repo is public. The notes follow the same rules as the other reference notes: letters only, and no places, dates, OSD values, file names or people.
