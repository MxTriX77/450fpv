## ADDED Requirements

### Requirement: A faithful port of the Godot feed
The feed SHALL port the Godot feed the pilot chose (`docs/legacy/video-feed-godot/`), keeping its algorithms, constants, rates and structure (FEED-00, D-026), with Godot's plumbing replaced by a URP full-screen pass in HLSL and C# ports of `FeedEvents`, `FeedLoss`, `FeedOsd` and `FeedFont`. The two link-type artefacts D-065 removed (the free-running recovering dropouts and the tear's black wedge) SHALL come back with one switch, Godot's link artefacts, which the A/B against Godot runs with.

#### Scenario: The same seed, the same schedule
- **WHEN** the Unity and Godot event schedulers run with the same seed for 15 000 fields, Unity's with Godot's link artefacts
- **THEN** their schedules (quality drops, recovering dropouts, N3, N4 and every loss state, field by field) are identical

#### Scenario: The same seed and field, matching stills
- **WHEN** stills are captured in Unity, with Godot's link artefacts, and in the Godot original at the same seed and field, for the baseline, a deep quality drop, N3, N4, every loss state, the tell, the tear and the OSD
- **THEN** each pair matches stage by stage on a side-by-side sheet, and their per-channel mean differs by at most 2 % of full scale

### Requirement: The camera and the pipeline
The FPV camera SHALL render at field resolution with extra FOV (D-017) and the pinned intrinsics (FEED-02: about 106° horizontal, 16:9, f ≈ 1.005 before k1, 0° uptilt), at 50 fields per second in the K§11.7 pipeline order, with every trait of FEED-04 at its K§11 parameters.

#### Scenario: The lens as measured
- **WHEN** a straight-line grid is rendered through the feed
- **THEN** the radial distortion's k1 lies within ±0.03 of K§11's measured value

### Requirement: Losses, recovery and snow
The receiver's states SHALL be the Godot feed's: staged loss, hard cut, and recovering dropouts, with a clean recovery: the picture and the OSD come back clean, and receiver text appears only on blue screens (FEED-05, FEED-06). Snow SHALL be near-binary white dashes on black, one field line tall, colour-killed after the first 1–2 fields, under the receiver text, and never grey Gaussian noise (FEED-07). The noise SHALL be the analog camera's, never a radio link's (D-065): nothing SHALL lose the picture on its own, a recovering dropout SHALL come only from the fiber's margin dip and a terminal loss only from its break (`p7d-tether`), and a tear SHALL shear the lines without the black wedge. There SHALL always be a tell before the picture goes.

#### Scenario: The feed never loses the picture on its own
- **WHEN** the feed runs for 15 000 fields with an intact fiber and no event
- **THEN** every field shows the picture, with no tell, tear, dropout or terminal loss, while N1–N4 and the quality drops run as before

#### Scenario: No black sticks
- **WHEN** a fiber loss's tell tears the picture
- **THEN** the lines shear sideways and no black wedge cuts in from the edge

#### Scenario: A recovering dropout comes back clean
- **WHEN** a recovering dropout fires on the fiber's margin dip and ends
- **THEN** the next clean field shows the picture and the OSD with no receiver text and no residual snow, and the dropout's length is inside the Godot feed's range

### Requirement: Signals drive the feed
The feed SHALL read its causes through `IFeedSignals`, from `p6-flight-core`'s `FeedSignals` (the K§11.8 list): motor speeds, pack voltage, the camera's angular rate and the link. Each signal the physics does not yet publish SHALL be a stub labelled as such in the review overlay (FEED-01, FEED-03).

#### Scenario: A stub is visible as a stub
- **WHEN** the review overlay is on
- **THEN** every signal shows its value and its source: `physics`, `stub`, or `noclip` for the noclip camera's measured angular rate (design 4)

### Requirement: Light, night and lens drops
The AE and AWB loops SHALL follow K§11's time constants, and night SHALL be the same pipeline at its low-light end: monochrome, the strongest grain, N13 on, never a separate filter (FEED-08). Rain SHALL put drops on the lens as FEED-13 gives them.

#### Scenario: Into the night preset
- **WHEN** the gallery switches from day to the night preset
- **THEN** the feed goes monochrome with grain at its strongest within the AE time constant, using the same pass as by day

### Requirement: Performance
The feed's GPU cost SHALL be at most 1.0 ms at 1080p output on the dev machine, measured on AC power (FEED-10), and the FPV view SHALL hold PLT-02.

#### Scenario: The cost on AC
- **WHEN** the feed's GPU time is measured in the test range (D-044) at 1080p on AC
- **THEN** it is at most 1.0 ms, printed with the power mode

### Requirement: Review and acceptance
The review tools of FEED-11 SHALL exist. One frame-metrics tool SHALL run alike on the footage and on captured feed output (per frame: mean luma, high-pass noise, row-to-row stripe energy, frame change, mean R, G and B, with the local-jump outlier rule). The comparison sheets SHALL be local only, in `reference/_compare/`, never committed (FEED-12).

#### Scenario: Like for like against the footage
- **WHEN** the metrics tool runs on a reference letter's frames and on the feed's capture of the matching gallery view
- **THEN** grain σ, the stripe energy and the colour shift with distance land within K§11's measured tolerances, written per trait on the local sheet
