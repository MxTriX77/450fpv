# Review: p7d-tether 1.11i, the pilot's cut table (designs 25 and 26, model 108)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch reviewed:** `unity/physics/physics-integration-2--w9-line-on-faces` at `f6e59dde` (this review on `unity/qa/physics-integration-2--cuts-review`; the branch's later `a200f64f`, the tether golden re-recorded, bears on no finding) · **Range:** `097cc8e8..f6e59dde` (6 commits, 21 files) and the bench's `3a72e3c0`, `1deee5d7`, `9c420140`
**Requirements:** PHY-19, PHY-24, TST-03, LOG-02 · **Decisions:** D-024, D-104, D-121
**Read:** `CLAUDE.md`, `AGENTS.md`, the `karpathy-guidelines` skill; D-104 and D-121 (trunk); `tasks.md` 1.11i; `design.md` 25 and 26; the `fiber-tether` spec delta; `FiberCuts.cs` (the lip's point-contact law, `EdgeStrength`, `Check`, `SectionEvent`), `LiftoffCuts.cs`, the liftoff and edge wiring in `FiberLine.cs` and `FlightCore.Advance`, `FlightRandom.cs`, `LogHeader.cs`'s two new switches, `tether.json`'s 15 new rows, `CutsCase.cs`, `FiberCutScenarios.cs` (Snap, Brake, Turn, Liftoff, Backward, Watch), `LiftoffCutTests.cs`, `tools/flightbench/handcalc/cuts.py`.

**How QA ran it.** Everything ran in the reviewed tree at `f6e59dde`; no code was edited. QA's own probe (`qaprobe`, scratchpad, never committed) compiles the same sources as `flightbench` and adds: the cut table on QA's own seeds 41–80 and with the known-wrongs; the liftoff law's inputs as the scenarios give them; a snap and a flip low over the launch; a record and replay through a liftoff cut and an edge cut; two of the pilot's flights re-flown on their sticks. QA's own Python re-derives the lip's bend, the liftoff draws and the bands' statistics. AC power, Balanced plan; another agent's suite and a game build shared the machine, so **no timing claim is made**. No Unity, no GPU. The pilot's flights are named by their index in write order only. A session restart stopped QA's queue: the survey of all 56 of the pilot's liftoffs, the Windy and Severe hovers and the late backward move were not run (open, below).

## Verdict: REQUEST CHANGES

The bench passes, on the engineer's seeds and on QA's, and the replay, the draws, the logging and the lip's mechanics are right. But the pilot's own sticks say the table does not hold where the pilot flies: **re-flown on model 108, one of their flights parts the line at the hole's edge in quiet flight on all three seeds QA tried, 15–31 s before their first sharp stick input** (W1). The liftoff law's window never closes over a low hover, so it switches the edge's physics off near the launch for as long as the drone stays there (W2). The specs still say every cut is the line's mechanics (W3). The gate itself fails a perfect model 39 % of the time and passes a pitch 12 points short 60 % of the time (W4). Two explanations in the design and the hand calculation are wrong (W5, W6).

## Findings

### Wrong (blocking)

**W1. On the pilot's own sticks the line parts in normal flying.** Flight 35 (model 103, the farmland; as flown its line held to 58.7 s, through sharp rolls from 43.6 s and a roll flip at 46.6 s) re-flown open loop on its sticks with model 108's line (every tether known-wrong the log lacks cleared, the new rows added):

| seed | cut | the sticks and the drone at the break | the edge the step before |
|---|---|---|---|
| the log's | 28.89 s, Bend at Lip | centred (throttle 0.43), nose 2° down, 6 °/s, 1.5 m/s, 61 m out | 52°, 0.037 N, 0.32 GPa |
| 1 | 20.41 s, Bend at Lip | centred (throttle 0.50), nose 10° up, 13 °/s, 9.2 m/s, 53 m out | 91°, 0.017 N, 0.38 GPa |
| 2 | 12.92 s, Bend at Lip | pitch +0.53, nose 31° down, 67 °/s, 2.3 m/s, 17 m out | 83°, 0.091 N, 0.82 GPa |

Flight 19 (as flown: a sharp roll alone at 10.61–11.04 s, the line cut at a disc at 11.31 s): the log's seed and seed 2 part at the lip at 10.97 and 10.91 s, in the roll; seed 1 parts at 9.44 s, before it. So 4 of 6 re-flights cut before any sharp input, against the pilot's "sometimes random, not too often". The stub floor's normal set (2.3 %, below) does not represent the farmland: there a hovering line already reads 0.32–0.38 GPa at the edge, about twice the scenarios' hover (0.14–0.18 GPa, design 25), and with m = 7 that is about 100 times the chance per section. Caveats: the airframe is the log's (model 103), the re-fly diverges from the pilot's path once the line's pull differs, and it is two flights. That is enough to say the law's one calibrated parameter was set on scenarios that do not load the edge as the pilot's flying does. **Needed:** the pilot's flights (all 56 logs, or at least the 30 model-103 ones the snaps came from) re-flown on model 108, the cuts outside sharp inputs counted against "not too often", before σ_0 or the law is trusted.

**W2. The liftoff never ends over a low hover, so the edge's physics is off near the launch indefinitely.** A liftoff lasts until 5 m of climb, 10 m of payout or a landing, with no time bound. QA's probe: the normal liftoff rows end with the liftoff still open (20 of 20, Calm and Windy); the hover row (3 m, 30 s) likewise. While open, `FiberCuts.EdgeYields` turns the edge's Weibull off and the empirical back-speed curve stands in. A sharp pitch of 110° flown at 4 m, 9 s after a careful climb: **12 of 20 cut on model 108 (10 by the liftoff law's back-speed curve, held at 40 %), 20 of 20 with the edge's Weibull on** (the liftoff open at the snap on 19 of 20; the edge's most stress 1.09 GPa, higher than the in-flight snap's 0.80). A pitch flip at 4.5 m cut 20 of 20 either way (8 by the liftoff law); a roll flip at 4.5 m crashed on every seed before the line loaded (inconclusive). The pilot's 40 % is "backwards too much at liftoff", not "any time after a low liftoff". **Needed:** a time bound on the window (the pilot's account is about the first moments), and the edge's law back once it passes.

**W3. The specs say the opposite of what model 108 does.** The `fiber-tether` spec's purpose: "Every outcome comes from the line's mechanics … the only randomness is the strength of the line and the stickiness of its pack"; "The fiber's randomness" lists four streams; "Cut risk" lists five mechanisms; PHY-19 says the very sharp liftoff and the backward momentum "must" come "from physics"; D-121 says "The cuts stay physics … Nothing is scaled to hit a rate". Model 108 adds `TetherMechanism.Liftoff` (empirical), three streams (`spool.hole_edge`, `fiber.edge_strength`, `fiber.liftoff`) and a parameter set against the pilot's table (σ_0). The design labels all of it honestly, but no spec delta or superseding decision records it, and "The draws cover their bounds" (10 000 seeds, `FiberCutScenarios.DrawBands`) does not cover the new streams. **Needed:** the orchestrator's decision superseding D-121's "physics only" for the liftoff, the spec delta (purpose, randomness, cut risk, accounts), and the three streams in the draw-coverage test.

**W4. The gate's statistics.** `CutsCase` judges each row at ±2 binomial σ on 40 fixed seeds (D-121's wording, TST-03's sizing missing). QA's exact binomial sums (`stats.py`):
- a model exactly on every pilot target passes the whole table with probability **61 %** (each P row about 95–98 %; the normal set's "at least 1" fails 37 % at a true 1 in 240, 5 % at 3 in 240): **a false failure of 39 %**;
- yet a sharp pitch at a true 63 % passes 60 % of the time, 60 % passes 44 %; a 65 km/h brake at a true 15 % passes 61 %;
- the rows are not independent: the liftoff draw is the seed's, so seed 80's draw (u = 0.0118, QA's Python) cut the careful climb in 19 of 21 rows at 0.70 s on seeds 41–80, and 5 of the normal set's 8 there are that one draw.

**Needed:** bands sized by a stated family false-failure rate (Bonferroni or exact intervals), the seed count per row sized for the rows the pilot named (75 % against 63 % needs about 200 seeds for 80 % power), and the rows' liftoffs drawn so one seed does not cut every row.

**W5. The normal set's "random" cuts are a punch-out, not cruise.** Seeds 1–40's three normal cuts (all in "cruise at 100 km/h, Windy") are at 5.24, 5.27 and 5.39 s, the moment the scenario asks 27.8 m/s from a climbing hover. QA's trace of seed 18: the nose goes from 2° up to 37° down in 0.24 s, the lip from 79° to 111°, the edge from 0.33 to 0.51 GPa, the cut at 5.236 s. The bench's edge column for the row reads from 11 s, so it never saw them, and `cuts.py`'s "the edge's at a gust's peak far above its mean" with n_eff 1.9 km is wrong. On seeds 41–80 the normal set's 8 are 5 careful-liftoff draws (seed 80), the punch (5.39 s), a Windy climb (4.00 s) and one orbit (16.77 s). So inside steady cruise, orbits, turns and hovers the stub floor cuts 1 of 480. **Needed:** the design and hand calculation corrected; the punch counted as the sharp pitch it is.

**W6. `cuts.py`'s stress grid is not the engine's law.** The grid is design 6's frictionless elastica over the whole lip turn; the engine applies design 18's point contact past the bore's free angle (9.73°) with μ = 0.45. At the same turn and tension the grid reads 30–34 % higher (QA: 60° 0.569 against 0.434 GPa, 120° 1.095 against 0.830 GPa at 0.05 N), which is 6–8 times the chance at m = 7. The rows use the engine's measured stresses, so n_eff is consistent; the grid is labelled "Design 25: the edge's stress" and is not. **Needed:** the grid on the engine's law.

### Doubtful

- **D1. m = 7 is labelled "published" without a source that can be opened** ("SPIE, the Charles Kurkjian profile's papers"). QA found no paper giving about 2 in tension and about 7 in two-point bending on marked fibers. OFS's 2.5–20 is for "an out-of-control process", not damage; Corning gives 2–5 for strengths near the proof level; a Matthewson–Kurkjian weak (acetone-degraded) population is about 5 in two-point bending. m = 7 sits inside the published spread, so label it "assumed" and cite the range. It matters: the rows' ratio goes as σ^m (brake 0.51 against pitch 0.80 GPa: 23 times at m = 7, 9.5 at m = 5).
- **D2. σ_0 (1.0–1.4 GPa a flight) is calibrated to the pilot's table** and labelled "assumed". The rationale says so ("the one parameter set against their account"), but D-121 forbids scaling to a rate, so the decision should record it as a calibration.
- **D3. The Weibull's scale is per 0.1 m section.** `fiber.section_length_m` (assumed, set for the props' scale) silently sets every edge rate: halving it takes the 25 % brake to about 44 %. Use a reference length.
- **D4. The liftoff rows test the draw, not physics.** The nodes sit at the scenarios' own thrust over weight (QA's probe: 80 % in 0.1 s gives 1.920–1.992 against the 87 % node at 1.95; max throttle gives 2.418–2.523 against 95 % at 2.45; backward flies the 3 m/s node). QA's Python reproduces the rows from the draws alone: 33 of 40 and 17 of 40 on seeds 1–40, 12 of 40 backward on 41–80. Between the nodes the curve is assumed linear and steep: 2 % at 1.3, 28 % at 1.5, 87 % at 1.95. The pilot's own liftoffs: flight 19 1.07–1.12 (0.5–0.8 %); flight 35 1.40–1.49 with the nose down (held at 5 %; level, 16–20 %). The survey of all their liftoffs is open.
- **D5. The lean's 10 points also land on a careful liftoff** (2 % to 12 %). A trim or a wind correction past 10° is not the pilot's "unnecessary" roll or pitch back.
- **D6. Rows at their bands' edges.** Pooled over 80 seeds (Wilson 95 %): sharp pitch 54/80, 67.5 % (56.6–76.8) against 75; the 65 km/h brake 10/80, 12.5 % (6.9–21.5) against about 7; a roll alone 0 of 79 from the roll itself (the band 0–61 % accepts "never" as "less than pitch").
- **D7. The back-speed path counts ground speed tail first**, so wind drift counts; the pilot's mechanism is the spool's axis toward the ground (attitude). It did not bite in a Windy liftoff (the drift at most 0.10 m/s); the Windy and Severe hovers were not run.
- **D8. `SectionBand.Edge = 4` sits outside FiberSection's documented "low 2 bits"** (StepTrace's doc not updated), and `FiberCutScenarios.Watch` reads `Index & 0x03`, so an edge break's `Outcome.Band` reads Intact. No log reader misreads it today.
- **D9. Labels.** The five "pilot" rows carry assumed nodes (3 m/s, 0.5 m/s, the 99 % cap, 2 % at 1.3), said in their rationales but not in their labels.

### Fine

- **The lip's bend.** QA re-derived five logged cuts from their θ and T by the point-contact law: R within 0.6 %, σ within 0.5 % (the printed θ and T are rounded). Units hold: E 73 GPa, glass 125 µm, coating 250 µm, EI 8.748e-7 N·m², A_g 1.2272e-8 m². Proof 0.69 GPa (G.652); Prysmian's intrinsic 4.8 GPa (700 kpsi) and STL's "approximately 99 %" of breaks above 2.75 GPa confirmed in the documents.
- **The Weibull's draw.** σ_e = σ_0(−ln(1−u))^(1/m) inverts 1 − exp(−(σ/σ_0)^m), with DetMath's Log and Exp. It is counter-based on the section, so the order of use shifts nothing. σ_0 is a per-flight header draw (DrawQuantile); the edge's read is logged as FiberSection at the near miss; a liftoff's u is logged at its begin. Streams are keyed by name, so a new stream shifts none.
- **No double count.** `EdgeYields` is set from the liftoff's state before the same step's `Check`, so no step sees both laws. A liftoff break writes no FiberSection. The nose-up row's later edge cuts (11 and 13) come at about 1.9 s, after its liftoff ended at 5 m of climb, with the drone backwards up at 15 m/s.
- **Nothing on the pose.** The law only parts the line; every draw is a named, seeded, logged stream.
- **The inputs are read correctly.** Thrust over weight is the step's mean rotor thrusts over m·g with the coil. The legs are the four leg spring forces of this step; touching is ContactModel's count of this step. The body axes are x right, y up, z forward: the nose up is forward.Y ≥ sin 10°, a roll |right.Y| ≥ sin 10°, the safe launch the window's mean nose down.
- **Replay bit for bit.** 10 flights recorded and replayed through `FlightReplay.Run`: 5 liftoff-law cuts, 2 edge cuts (brake seeds 3 and 8), 3 whole; every one Match.
- **The table reproduces.** Seeds 1–40 match the engineer's table row for row; seeds 41–80 all PASS.
- **The known-wrongs fail.** Model 107's line (both switches) on seeds 1–10: pitch 1 of 10, every liftoff row 0, backward 0, the normal set 0 of 60, so the table FAILs. The brakes read 0, which the 10-seed band cannot fail; at 40 seeds 0 is outside 11–39 %.
- **Hygiene.** 9 commits, subject only, at most 72 characters, under the role identities. Nothing from `reference/`, `Library/`, `Temp/` or `Logs/`. `precommit.py --tree HEAD`: 3638 files scanned, 0 findings.

## Spec scenarios and requirements

- *The accounts over many seeds* (`flightbench --cuts 40`): **passed** on seeds 1–40 and 41–80. But W1 shows the accounts do not hold on the pilot's own flight, and W4 that the pass is weak evidence.
- *Known-wrong models fail*: **passed** for model 107's line (10 seeds). The table's own rows are tautological for the liftoff law (D4).
- *The same manoeuvre can go either way*, *each seed the same on every replay*: **passed** (the replay).
- *The bend is the elastica's*: **passed** for the lip (QA's re-derivation; the scenario's formula is design 6's, the lip uses design 18's, as designed).
- *The draws cover their bounds*: **not met** for the three new streams (W3).
- PHY-19 **partly met** (the liftoff causes are empirical, not physics; normal flying on the pilot's sticks cuts too often, W1). PHY-24 **met** (named, bounded, logged), with the catalogue and coverage owed (W3). TST-03 **missed** (W4). LOG-02 **met**.

## Commands and output (excerpts)

```
dotnet tools/flightbench/bin/Release/net10.0/flightbench.dll --cuts 40     # 14:16:46-14:21:09, exit 0
  PASS [snap] sharp pitch, 110° in flight: 25 of 40 (63 %) against the pilot's 75 %, 61-89 %: Bend at Lip 18, Prop at Disc 7
  PASS [brake] a proper brake from a higher speed, 65 km/h: 6 of 40 (15 %) against the pilot's 7 %, 0-15 %: Bend at Lip 6
  PASS [normal] cruise at 100 km/h, Windy: 3 of 40 (8 %) against 0-10 %: Bend at Lip 3   (cuts at 5.236, 5.265, 5.389 s)
  PASS [normal] the normal set: 3 of 240 flights cut ... --cuts: PASS
qaprobe table 41 40                       # QA's seeds, exit 0
  PASS sharp pitch 29/40 (73 %); full throttle 35/40; roll alone 1/40 (Liftoff@Lip, 0.70 s); roll+yaw 30/40; flips 40/40, 40/40
  PASS brake 8/40, 4/40, 2/40; liftoffs 32/40, 40/40, 40/40, safe 4/40, backward 12/40; normal set 8/240; table: PASS
qaprobe table 1 10 both                   # model 107's line: sharp pitch 1/10 FAIL, liftoffs 0/10 FAIL x3, backward 0/10 FAIL, set 0/60 FAIL
qaprobe replay                            # 10 of 10 "Match: ... bit for bit" (5 Liftoff, 2 Bend, 3 whole)
qaprobe trace 18                          # t 5.000 nose -1.9°, lip 80°, 0.328 GPa ... t 5.236 nose 36.9° down, lip 111°, 0.511 GPa, BROKEN Bend
qaprobe low 20                            # sharp pitch at 4 m: 12/20 (Liftoff@Lip 10) on model 108, 20/20 with the edge's Weibull on
qaprobe lift 20                           # 80 %: tw 1.920-1.992, chance 0.868; max: tw 2.418-2.523, chance 0.949; normal liftoffs open at the end 20/20
qaprobe pilot <flights> 35 0 1 2          # Bend@Lip at 28.89 / 20.41 / 12.92 s, the first sharp stick at 43.57 s (table W1)
qaprobe pilot <flights> 19 0 1 2          # Bend@Lip at 10.97 / 9.44 / 10.91 s, the roll at 10.61-11.04 s
python tools/flightbench/handcalc/cuts.py # the numbers above; grid against the engine in W6
python stats.py ...                       # family pass 61.2 %; pitch at 63 % passes 59.6 %
python draws.py                           # seeds 1-40: 33 under 0.872, 17 under 0.40; 41-80: 12 under 0.40; seed 80 u 0.0118
python tools/hygiene/precommit.py --tree HEAD   # 3638 files scanned, 0 findings
```

## Open (not run, the session restart)

The first liftoff of each of the pilot's 56 flights on model 108 (the law's inputs where they fly); the Windy and Severe 30 s hovers (D7); a 20 s and a 60 s low hold before a backward move (W2's second half, the back-speed curve long after the liftoff); the known-wrongs at 40 seeds.
