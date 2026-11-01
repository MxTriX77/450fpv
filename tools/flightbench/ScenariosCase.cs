using System;
using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // The aero and wind scenarios (p6-flight-core 1.5, p7c-wind 1.4), from the same files the suite compiles
    // (CrosswindScenario, WindHarness, WindTargets, GustFrontScenario, SimPilot): the crosswind lean under the closed-loop
    // pilot, clip P's pitch flying the cruise (p7c-wind 1.6), and the wind field's open-air targets, each statistical target also run on a known-wrong model that
    // must fail it. PASS needs every target met and every known-wrong model failed.
    internal static class ScenariosCase
    {
        static bool _ok = true;

        static void Row(string name, bool pass, string detail, bool knownWrong = false)
        {
            bool good = knownWrong ? !pass : pass;
            _ok &= good;
            string verdict = knownWrong ? (pass ? "PASSES: the target cannot tell it from the model (FAIL)" : "fails, as it must") : pass ? "PASS" : "FAIL";
            Console.WriteLine($"  {name}: {detail}: {verdict}");
        }

        static string Hover(GroundEffectScenario.Hover h) =>
            $"skids {h.Clearance:F3} m up, rotors at {h.LeastHeight:F2}-{h.MostHeight:F2} R, body rate RMS per {GroundEffectScenario.Block} s block "
            + $"{h.LeastRate:E2}-{h.MostRate:E2} rad/s (floor {GroundEffectScenario.StillFloor:E0}), wash up to {h.MostWash:E2} v_h";

        static string Lean(CrosswindScenario.Result r) =>
            $"lean {r.LeanDeg:F2}° = {r.LeanPerMs:F3}° per m/s of {r.WindSpeed:F2} m/s, {r.LeanOffWindDeg:F1}° off the wind's bearing, "
            + $"position error {r.MeanError:F3} m mean / {r.MaxError:F3} m max, drift {r.Drift:F4} m/s";

        static (long, long, long) Bits(Double3 v) =>
            (BitConverter.DoubleToInt64Bits(v.X), BitConverter.DoubleToInt64Bits(v.Y), BitConverter.DoubleToInt64Bits(v.Z));

        static bool LeanTarget(CrosswindScenario.Result r) =>
            r.LeanPerMs >= CrosswindScenario.HoverLeanLo && r.LeanPerMs <= CrosswindScenario.HoverLeanHi && r.LeanOffWindDeg < 5;

        public static bool Run()
        {
            DroneSpec spec = PoweredFlightCase.Drone();

            Console.WriteLine($"crosswind lean in a hover (flight-dynamics: the drag's bands over the weight, {CrosswindScenario.HoverLeanLo:F2}–{CrosswindScenario.HoverLeanHi:F2}° "
                + $"per m/s in a steady 5 m/s, to {CrosswindScenario.GustLeanHi:F2} × U/5 above 5 m/s in the Severe field; into the wind), the drone holding its point "
                + $"under the closed-loop pilot, the last {CrosswindScenario.MeasuredSeconds:F0} s of {CrosswindScenario.Seconds:F0}:");
            foreach (double from in new[] { 270.0, 0, 90, 180, 45, 135 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(from, 5)));
                Row($"steady 5 m/s from {from:F0}°", LeanTarget(r) && r.Drift < 0.05, Lean(r));
            }
            foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Severe), seed));
                Row($"Severe field, seed {seed}", r.LeanPerMs >= CrosswindScenario.HoverLeanLo && r.LeanPerMs <= CrosswindScenario.GustLeanHiAt(r.WindSpeed) && r.LeanOffWindDeg < 10, Lean(r));
            }
            CrosswindScenario.Result none = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(270, 5), drag: false, rotorDrag: false));
            Row("known-wrong: no drag", LeanTarget(none), Lean(none), knownWrong: true);
            CrosswindScenario.Result body = CrosswindScenario.Hold(CrosswindScenario.Setup(spec, WindSetup.SteadyFrom(270, 5), rotorDrag: false));
            Row("known-wrong: the body's drag alone, no rotor in-plane drag", LeanTarget(body), Lean(body), knownWrong: true);

            Console.WriteLine("clip P's pitch flies the cruise (wind-turbulence, p7c-wind 1.6, PHY-17a): the harness pilot holds 22.7° of pitch, a heading "
                + "and 35 m in still air at the nominal loadout; it settles inside the published 17–22 m/s, the last 10 s of 39:");
            double slow = WindHarness.HeldPitchAirspeed(spec, 13, out double pitchSlow), fast = WindHarness.HeldPitchAirspeed(spec, 25, out double pitchFast);
            Row("released at 13 m/s and at 25 m/s", slow >= 17 && slow <= 22 && Math.Abs(fast - slow) <= 0.05
                    && Math.Abs(pitchSlow + 22.7) <= 0.3 && Math.Abs(pitchFast + 22.7) <= 0.3,
                $"settles at {slow:F2} m/s (pitch {pitchSlow:F2}°) and {fast:F2} m/s ({pitchFast:F2}°)");
            Row("the harness's release is the cruise", Math.Abs(WindHarness.ReleaseSpeed - slow) <= 0.3, $"{WindHarness.ReleaseSpeed:F1} m/s");
            double old = WindHarness.HeldPitchAirspeed(spec, 13, out _, hForce: double.Parse(GoldenFlights.TunedHForce, System.Globalization.CultureInfo.InvariantCulture));
            Row("known-wrong: unity/main's rotor drag, tuned to 13 m/s (p7b-airframe 1.7)", old >= 17 && old <= 22, $"{old:F2} m/s", knownWrong: true);
            double v = WindHarness.ReleaseSpeed;
            double lean0 = CrosswindScenario.CruiseLean(spec, v, 0), lean2 = CrosswindScenario.CruiseLean(spec, v, 2);
            double perMs = (lean2 - lean0) / 2, relation = 23.4 / v, roll = WindHarness.CruiseRollPerMs(spec, WindPreset.SevereCentre);
            Row($"at the cruise, {v:F1} m/s north, a 2 m/s crosswind from the west, the nose on the track", Math.Abs(perMs / relation - 1) <= 0.15,
                $"{lean2 - lean0:F2}° more lean = {perMs:F3}° per m/s (wind.md §5.3's 23.4°/V: {relation:F3}); as the tracker's roll {roll:F3}, "
                + $"so {roll * WindPreset.SevereCentre:F1}° in Severe's {WindPreset.SevereCentre:F1} m/s abeam (T0, graded by wind_stats.py: 9 ± 3)");
            Console.Write("  cruise trim in still air, the pitch that holds an airspeed:");
            foreach (double speed in new[] { 13.0, 17, v, 22 }) Console.Write($" {speed:F1} m/s {CrosswindScenario.CruisePitch(spec, speed):F1}°;");
            Console.WriteLine();

            Console.WriteLine("the wind field (wind-turbulence, p7c-wind 1.4, design 3 and 5):");
            var calm = new List<WindTargets.History>();
            var still = new List<WindTargets.History>();
            var vector = new List<WindTargets.History>();
            for (ulong seed = 1; seed <= 5; seed++)
            {
                calm.Add(WindTargets.FlightHistory(WindLevel.Calm, seed, 120, 10));
                still.Add(WindTargets.Gusts(WindLevel.Calm, seed, 120, 10, WindTargets.Still));
                vector.Add(WindTargets.Gusts(WindLevel.Calm, seed, 120, 10, WindTargets.Fixed));
            }
            Row("Calm is never perfectly still, 5 flights of 120 s", WindTargets.NeverStill(calm, out string d), d);
            Row("Calm's mean in its band", WindTargets.MeanInBand(calm, WindPreset.Of(WindLevel.Calm), out d), d);
            Row("known-wrong: still air", WindTargets.NeverStill(still, out d), d, knownWrong: true);
            Row("known-wrong: a fixed vector", WindTargets.NeverStill(vector, out d), d, knownWrong: true);

            // Same seed, same wind: two powered flights on one seed and level, flown alike, the wind at every rotor and
            // at the body compared bit for bit at every step; a different seed draws another direction.
            int same = 0, steps = 20000, rotors = 0;
            {
                FlightCore Fly(ulong seed)
                {
                    FlightSetup f = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Severe), seed);
                    f.StartPosition = new Double3(0, 30, 0);
                    var c = new FlightCore(f);
                    CrosswindScenario.Arm(c);
                    return c;
                }
                FlightCore ca = Fly(0x450F), cb = Fly(0x450F);
                var pa = new SimPilot(ca.Controller, ca.State.Position);
                var pb = new SimPilot(cb.Controller, cb.State.Position);
                for (int k = 0; k < steps; k++)
                {
                    ca.Advance(pa.Sticks(ca, ca.State.Step * FlightCore.StepUs));
                    cb.Advance(pb.Sticks(cb, cb.State.Step * FlightCore.StepUs));
                    int alike = 0;
                    for (int i = 0; i < 4; i++)
                        if (Bits(ca.Trace.RotorWind[i]) == Bits(cb.Trace.RotorWind[i])) alike++;
                    rotors += alike;
                    if (alike == 4 && Bits(ca.Trace.Wind) == Bits(cb.Trace.Wind)) same++;
                }
            }
            Row("same seed, same wind (Severe, 20 s held at 30 m)", same == steps && rotors == 4 * steps,
                $"{same} of {steps} steps with the wind at all 4 rotors and the body bit-identical");
            WindTargets.History u1 = WindTargets.Gusts(WindLevel.Windy, 0x450F, 5, 1, WindTargets.Unseeded);
            WindTargets.History u2 = WindTargets.Gusts(WindLevel.Windy, 0x450F, 5, 1, WindTargets.Unseeded);
            int sameWrong = 0;
            for (int i = 0; i < u1.Samples.Length; i++)
                if (u1.Samples[i].X == u2.Samples[i].X && u1.Samples[i].Z == u2.Samples[i].Z) sameWrong++;
            Row("known-wrong: an unseeded turbulence", sameWrong == u1.Samples.Length, $"{sameWrong} of {u1.Samples.Length} samples alike", knownWrong: true);

            var bearings = new double[400];
            for (int i = 0; i < bearings.Length; i++) bearings[i] = new WindField(WindSetup.Of(WindLevel.Windy), new FlightRandom((ulong)i + 1)).FromDeg;
            Row("a different seed, a different direction, uniform over the compass", WindTargets.Uniform(bearings, out d) && new HashSet<double>(bearings).Count == 400, d);
            var left = new double[400];
            for (int i = 0; i < left.Length; i++) left[i] = 225 + 90 * ((i * 0.618034) % 1);
            Row("known-wrong: always from the left", WindTargets.Uniform(left, out d), d, knownWrong: true);

            foreach (WindLevel level in new[] { WindLevel.Windy, WindLevel.Severe })
            {
                WindPreset preset = WindPreset.Of(level);
                var runs = new List<WindTargets.History>();
                var flat = new List<WindTargets.History>();
                var biased = new List<WindTargets.History>();
                var met = new List<WindTargets.History>();
                var lowPass = new List<WindTargets.History>();
                var white = new List<WindTargets.History>();
                WindPreset cut = preset;
                cut.ShortestWavelength = WindHarness.ReleaseSpeed; // the known-wrong field: nothing above 1 Hz at the cruise
                var stubble = new List<WindTargets.History>();
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    runs.Add(WindTargets.Gusts(level, seed, 1200, 50));
                    stubble.Add(WindTargets.Gusts(level, seed, 1200, 50, null, WindTargets.Stubble(level)));
                }
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    flat.Add(WindTargets.Gusts(level, seed, 300, 50, WindTargets.FlatSigma));
                    met.Add(WindTargets.Encounter(level, seed, 66, WindHarness.ReleaseSpeed));
                    lowPass.Add(WindTargets.Encounter(level, seed, 66, WindHarness.ReleaseSpeed, cut));
                    white.Add(WindTargets.Encounter(level, seed, 66, WindHarness.ReleaseSpeed, null, WindTargets.White));
                }
                foreach (WindTargets.History h in runs) biased.Add(WindTargets.Biased(h));
                Row($"{level}: σ/U, 20 seeds of 1200 s", WindTargets.Intensity(runs, preset, out d, out _), d);
                Row($"{level}: the mean in its band and unbiased", WindTargets.MeanInBand(runs, preset, out d), d);
                Row($"{level}: energy at 2–4 Hz at the {WindHarness.ReleaseSpeed:F1} m/s cruise, 5 seeds", WindTargets.Spectrum(met, preset, WindHarness.ReleaseSpeed, 2, 4, out d, out _), d);
                Row($"{level}: energy at 1–2 Hz at the {WindHarness.ReleaseSpeed:F1} m/s cruise, 5 seeds", WindTargets.Spectrum(met, preset, WindHarness.ReleaseSpeed, 1, 2, out d, out _), d);
                Row($"{level} known-wrong: a field that stops below 1 Hz at the {WindHarness.ReleaseSpeed:F1} m/s cruise", WindTargets.Spectrum(lowPass, preset, WindHarness.ReleaseSpeed, 2, 4, out d, out _), d, knownWrong: true);
                Row($"{level} known-wrong: white noise", WindTargets.Spectrum(white, preset, WindHarness.ReleaseSpeed, 2, 4, out d, out _), d, knownWrong: true);
                Row($"{level} known-wrong: a flat 0.2 m/s σ", WindTargets.Intensity(flat, preset, out d, out _), d, knownWrong: true);
                Row($"{level} known-wrong: the stubble's roughness (z0 0.03 m), 20 seeds of 1200 s", WindTargets.Intensity(stubble, preset, out d, out _), d, knownWrong: true);
                Row($"{level} known-wrong: a mean 5 % strong", WindTargets.MeanInBand(biased, preset, out d), d, knownWrong: true);
            }

            // Per-rotor gradients (PR-3): a gust front crossing the drone tail to nose.
            GustFrontScenario.Result field = GustFrontScenario.Fly(spec, uniform: false), uniform = GustFrontScenario.Fly(spec, uniform: true);
            string Rates(GustFrontScenario.Result r) =>
                $"peak pitch {r.Pitch * 57.29578:F2} °/s at +{r.PitchStep} ms, yaw {r.Yaw * 57.29578:F2} °/s at +{r.YawStep} ms, roll {r.Roll * 57.29578:F2} °/s";
            Row("a gust front crossing the drone: coupled pitch and yaw, each 3× the uniform field's or more",
                GustFrontScenario.Coupled(field, uniform), Rates(field) + "; the uniform field " + Rates(uniform));
            Row("known-wrong: the uniform field", GustFrontScenario.Coupled(uniform, uniform), Rates(uniform), knownWrong: true);
            Console.WriteLine("motor asymmetry (airframe-effects, p7b-airframe 1.1): a hands-off hover in still air, "
                + $"{AsymmetryScenario.Seconds:F0} s on seeds 1-5, asymmetry on and off:");
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(spec);
            AsymmetryScenario.Band band = run.Band;
            Console.WriteLine($"  the band from the tolerances ({AsymmetryScenario.BandDraws} seeds' draws, {AsymmetryScenario.BandQuantile * 100:F1}–"
                + $"{100 - AsymmetryScenario.BandQuantile * 100:F1} %): roll {band.RollRateLo:E2}–{band.RollRateHi:E2} °/s, yaw {band.YawRateLo:E2}–"
                + $"{band.YawRateHi:E2} °/s, vibration {band.VibrationLo:E2}–{band.VibrationHi:E2} rad/s per step");
            for (int i = 0; i < AsymmetryScenario.Seeds.Length; i++)
            {
                AsymmetryScenario.Result on = run.On[i], off = run.Off[i];
                AsymmetryScenario.Prediction p = run.Predicted[i];
                bool pass = AsymmetryScenario.InBand(on, band) && AsymmetryScenario.AsPredicted(on, p) && AsymmetryScenario.Steady(on, p)
                    && AsymmetryScenario.NeverStill(on, band) && !AsymmetryScenario.NeverStill(off, band)
                    && off.RollRate <= 0.5 * on.RollRate && off.YawRate <= 0.5 * on.YawRate && off.SideDrift <= 0.5 * on.SideDrift;
                Row($"seed {AsymmetryScenario.Seeds[i]}", pass, AsymmetryScenario.Describe(on, off, p));
            }
            bool identical = true;
            foreach (AsymmetryScenario.Result off in run.Off) identical &= AsymmetryScenario.InBand(off, band);
            Row("known-wrong: identical rotors", identical, $"roll {run.Off[0].RollRate:E1} and yaw {run.Off[0].YawRate:E1} °/s on every seed", knownWrong: true);

            Console.WriteLine("vortex ring state (airframe-effects, p7b-airframe 1.2): the 6.2 kg loadout down at 3 m/s under the pilot, then the "
                + $"throttle held {VortexRingScenario.HoldSeconds:F1} s, low or higher, then recovered:");
            VortexRingSpec ring = spec.BuildVortexRing();
            VortexRingScenario.Result low = VortexRingScenario.Fly(VortexRingScenario.Setup(spec), true);
            VortexRingScenario.Result lowOff = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, false), true);
            VortexRingScenario.Result high = VortexRingScenario.Fly(VortexRingScenario.Setup(spec), false);
            VortexRingScenario.Result wrong = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, true, true), true);
            Row("heavy on low throttle: enters, loses thrust, wobbles; exits below its entry rate", VortexRingScenario.HeavyOnLowThrottle(low, lowOff, ring),
                VortexRingScenario.Describe(low) + $"; ringless: 10 ms change {lowOff.WobbleRms:E2} rad/s RMS");
            Row("the same descent at higher throttle: no ring", VortexRingScenario.HigherThrottle(high), VortexRingScenario.Describe(high));
            Row("known-wrong: the onset from the descent speed alone", VortexRingScenario.HeavyOnLowThrottle(wrong, lowOff, ring), VortexRingScenario.Describe(wrong),
                knownWrong: true);
            // QA's finding 4: on 20 seeds, each against its own ringless flight; the known-wrong fails on each.
            int heavyOk = 0, higher = 0, wrongs = 0;
            double least = double.MaxValue, most = 0;
            foreach (ulong seed in VortexRingScenario.Seeds)
            {
                VortexRingScenario.Result on = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, seed: seed), true);
                VortexRingScenario.Result off = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, false, seed: seed), true);
                if (VortexRingScenario.HeavyOnLowThrottle(on, off, ring)) heavyOk++;
                if (VortexRingScenario.HigherThrottle(VortexRingScenario.Fly(VortexRingScenario.Setup(spec, seed: seed), false))) higher++;
                if (!VortexRingScenario.HeavyOnLowThrottle(VortexRingScenario.Fly(VortexRingScenario.Setup(spec, true, true, seed), true), off, ring)) wrongs++;
                least = Math.Min(least, on.WobbleRms / off.WobbleRms);
                most = Math.Max(most, on.WobbleRms / off.WobbleRms);
            }
            int seeds = VortexRingScenario.Seeds.Length;
            Row($"on seeds 101-120: heavy on low throttle, the higher throttle, the known-wrong failing", heavyOk == seeds && higher == seeds && wrongs == seeds,
                $"{heavyOk}, {higher} and {wrongs} of {seeds}; the wobble {least:F2}-{most:F2} x the ringless flight's (against 1.5)");

            Console.WriteLine("ground and ceiling effect, recirculation (airframe-effects, p7b-airframe 1.3) on the stub, each rotor's thrust at "
                + "the props' reference rpm over its free-air thrust, and a closed-loop hover on the legs in still air without asymmetry:");
            GroundEffectScenario.Surfaces cushion = GroundEffectScenario.Cushion(spec);
            Row("the cushion and the hole", GroundEffectScenario.Within(cushion), $"over the floor at {cushion.DistanceOver:F3} D gain "
                + $"{cushion.LeastOver:F5} against Cheeseman-Bennett's {cushion.Expected:F5} ({(cushion.LeastOver / cushion.Expected - 1) * 100:F2} %); "
                + $"over the hole band no ground ({cushion.DistanceAway} D), |gain - 1| {cushion.Away1:E1}");
            GroundEffectScenario.Surfaces roof = GroundEffectScenario.Ceiling(spec);
            Row("the ceiling under a runtime box roof", GroundEffectScenario.Within(roof), $"under it at {roof.DistanceOver:F3} D gain "
                + $"{roof.LeastOver:F5} against Hsiao-Chirarattananon's {roof.Expected:F5} ({(roof.LeastOver / roof.Expected - 1) * 100:F2} %); "
                + $"in the open |gain - 1| {roof.Away1:E1}");
            GroundEffectScenario.Hover close = GroundEffectScenario.FlyHover(spec, true, GroundEffectScenario.HoverLift);
            GroundEffectScenario.Hover noWash = GroundEffectScenario.FlyHover(spec, false, GroundEffectScenario.HoverLift);
            GroundEffectScenario.Hover up = GroundEffectScenario.FlyHover(spec, true, GroundEffectScenario.HighLift);
            Row("the close hover is never still", GroundEffectScenario.NeverStill(close, noWash, up), Hover(close)
                + $"; {GroundEffectScenario.HighLift:F0} m up: {up.LeastRate:E2}-{up.MostRate:E2} rad/s");
            Row("known-wrong: no recirculation", GroundEffectScenario.NeverStill(noWash, noWash, up), Hover(noWash), knownWrong: true);

            // p7b-airframe 1.3c: the rotors' edgewise-flow moments.
            Console.WriteLine("rotor flapping (p7b-airframe 1.3c):");
            double err = FlappingScenario.WorstError(false), rigid = FlappingScenario.WorstError(true);
            Row("the hub moment is Johnson's at mu 0.05, 0.10, 0.20", err < 1e-9, $"worst relative error {err:E2} (the hingeless 150 Hz blade, roll and pitch)");
            Row("known-wrong: a rigid rotor without flapping (model 10)", rigid < 1e-9, $"worst relative error {rigid:E2}", knownWrong: true);
            foreach (double from in new[] { 0.0, 90.0 })
            {
                FlappingScenario.Response on = FlappingScenario.GustStep(spec, from, true), off = FlappingScenario.GustStep(spec, from, false);
                bool harder = from == 0 ? on.PitchPeak > 1.5 * off.PitchPeak : on.RollPeak > 1.5 * off.RollPeak;
                Row(from == 0 ? "a head gust step pitches harder with flapping" : "a side gust step rolls harder with flapping", harder,
                    FlappingScenario.Describe(from, on, off));
            }
            // p7b-airframe 1.4: the throttle-punch shake, its causes and the airframe's modes (D-049's scenarios).
            Console.WriteLine("the throttle-punch shake (p7b-airframe 1.4, D-049, D-051; its 40-seed distribution: --shake):");
            var modes = new FlightCore(ShakeScenario.Setup(spec, 1, 1)).Modes;
            var hz = new System.Text.StringBuilder();
            for (int i = 0; i < modes.Count; i++) hz.Append($"{(i > 0 ? ", " : "")}{modes.Frequency(i):F1}");
            Console.WriteLine($"  the airframe's modes at 1 kg: {hz} Hz");
            ShakeScenario.Result heavy = default;
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.0, 0.3, 0.6, 1.0 })
                {
                    ShakeScenario.Result sharp = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, 1), drop, false);
                    ShakeScenario.Result gentle = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, 1), drop, true);
                    if (payload > 0 && drop == 0.6) heavy = sharp;
                    // From the hover: the kick, never the shake; arresting a descent: an ESC on its current limit through the
                    // whole arrest and the shake lasting it, the frame's band ringing; a gentle ramp in a 5–9 m/s descent shows
                    // PHY-09's wobble instead.
                    bool ok = drop == 0 ? sharp.Duration < ShakeScenario.KickSeconds && gentle.Fields == 0
                        : ShakeScenario.LastsTheArrest(sharp) && sharp.VibrationPeak.Y > ShakeScenario.Rings
                          && (drop < 0.6 || ShakeScenario.Wobbles(gentle, sharp));
                    Row($"{payload:F0} kg, {(drop == 0 ? "from the hover" : $"after a {drop:F1} s drop")}", ok,
                        $"sharp: {ShakeScenario.Describe(sharp)}; gentle: {ShakeScenario.Describe(gentle)}");
                    if (drop == 0) continue;
                    ShakeScenario.Result noLimit = ShakeScenario.Fly(ShakeScenario.Setup(spec, payload, 1, currentLimit: false), drop, false);
                    Row($"known-wrong: an ESC that senses no current, {payload:F0} kg after a {drop:F1} s drop", ShakeScenario.HoldsTheArrest(noLimit)
                        || noLimit.TiltMax >= sharp.TiltMax, $"tilt {noLimit.TiltMax:F2}° against the punch's {sharp.TiltMax:F2}°; " + ShakeScenario.Describe(noLimit), knownWrong: true);
                }
            ShakeScenario.Result stiff = ShakeScenario.Fly(ShakeScenario.Setup(spec, 2, 1, false), 0.6, false);
            Row("a rigid airframe keeps the visible shake (the rigid body's)", ShakeScenario.LastsTheArrest(stiff) && stiff.Peak / heavy.Peak >= 0.8
                && stiff.Peak / heavy.Peak <= 1.2, $"peak {stiff.Peak / heavy.Peak:F2} of the flexing airframe's; " + ShakeScenario.Describe(stiff));
            Row("known-wrong: a rigid airframe publishes no camera vibration", stiff.Published, ShakeScenario.Describe(stiff), knownWrong: true);
            Console.WriteLine($"scenarios: {(_ok ? "PASS" : "FAIL")}");
            // Rain without wind (wind-turbulence, p7c-wind 1.7, PHY-17): the powered golden's pilot in Calm air and Severe
            // rain, against the same flight Sunny.
            Console.WriteLine("rain without wind (wind-turbulence, p7c-wind 1.7, PHY-17): the golden's scripted pilot, Calm and Severe rain, 10 s:");
            RainScenario.Result rain = RainScenario.Fly(spec);
            RainField rf = rain.Core.Rain;
            bool calmWind = rain.Core.Wind.MeanSpeed == rain.Sunny.Wind.MeanSpeed && rain.Core.Wind.FromDeg == rain.Sunny.Wind.FromDeg;
            Row("the wind stays Calm's", calmWind, $"from {rain.Core.Wind.FromDeg:F2}°, {rain.Core.Wind.MeanSpeed:F3} m/s, the Sunny flight's draws");
            FeedSignals sig = rain.LastSignals;
            bool live = (sig.Live & FeedSignalSet.RainRate) != 0 && (sig.Live & FeedSignalSet.AirVelocityAtCamera) != 0
                && sig.RainRateMmPerHour == rf.RateMmPerHour && rain.LoggedMismatches == 0;
            Row("the rain rate and the camera's air are live and logged", live,
                $"{sig.RainRateMmPerHour:F2} mm/h in [10, 50]; camera air ({sig.AirVelocityAtCamera.X:F2}, {sig.AirVelocityAtCamera.Y:F2}, "
                + $"{sig.AirVelocityAtCamera.Z:F2}) m/s; {rain.LoggedMismatches} records whose rain force, film or camera air read back other");
            Row("the rain's drag matches its labelled formula within 1 %", rain.WorstDragError < 0.01,
                $"worst {rain.WorstDragError:E2} over 10 000 steps; V {rf.FallSpeed:F2} m/s (D0 {rf.DropDiameterMm:F2} mm), W {rf.WaterContent * 1000:F3} g/m³; "
                + $"mean {rain.MeanRainForce * 1000:F2} mN, peak {rain.PeakRainForce * 1000:F2} mN against the air's peak {rain.PeakAirDrag:F2} N");
            Row("the film fills inside its bound and weighs", rain.FilmMass > 0 && rain.FilmMass <= rf.FilmCapacity
                    && Math.Abs(rain.Core.Body.Mass - rain.DryMass - rain.FilmMass) < 1e-12,
                $"{rain.FilmMass * 1000:F2} g of {rf.FilmCapacity * 1000:F2} g ({rf.FilmThickness * 1000:F3} mm over {rf.WettedArea:F3} m²)");
            // The known-wrong: rain as denser air (½(ρ + W)·C_d·A·v²) puts no drops on a drone at rest in still air.
            FlightSetup stillRain = GoldenFlights.PoweredSetup(spec);
            stillRain.Wind = WindSetup.Of(WindLevel.Still);
            stillRain.Rain = RainLevel.SevereRain;
            var atRest = new FlightCore(stillRain);
            atRest.Advance(new StickSample());
            DronePart coil = stillRain.Mass.Part(stillRain.Aero.CoilPart);
            double top = stillRain.Aero.BodyArea.Y + coil.Length * coil.Diameter;
            double hover = RainPreset.WaterDensity * atRest.Rain.RateMmPerHour * RainPreset.MmPerHour * atRest.Rain.FallSpeed * top;
            Row("at rest in still air: ρ_w·R·V on the top silhouettes, down", Math.Abs(-atRest.Trace.RainForce.Y - hover) <= 0.01 * hover,
                $"{-atRest.Trace.RainForce.Y * 1000:F3} mN against {hover * 1000:F3} mN");
            Row("known-wrong: rain as denser air", Math.Abs(0 - hover) <= 0.01 * hover, "0 mN at rest", knownWrong: true);

            return _ok;
        }
    }
}
