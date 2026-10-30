using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // K§8.5's acceptance harness (p7c-wind 1.2), headless: the simulated pilot and its variants fly the test flight on
    // N seeds, in parallel, and each run's camera log is written in track_attitude.py's columns, the input wind_stats.py
    // reads, with a manifest of the runs and their belt segments; optionally the seeds' T0c wind histories. Its own
    // check: flown at the cruise clip P's pitch settles at (p7c-wind 1.6), the nominal pilot's lean per m/s of crosswind
    // reproduces p6-flight-core 1.5's scenario's there (within 15 %, 20 % with the belt) and its airspeed is the cruise's;
    // without drag (a known-wrong model) it must fail. T0, the lean that grades it, is wind_stats.py's. p7c-wind 1.5: the
    // belt's wakes come from the wake grid, and `--wrong steady|nodrag` flies the known-wrong families that
    // tools/wind/grade_levels.py grades every target on.
    internal static class WindHarnessCase
    {
        const string Usage = "usage: flightbench --wind-harness calm|windy|severe [--seeds N] [--out DIR] [--pilots nominal|all] [--belt on|off] [--wrong steady|nodrag] [--history N]";

        public static int Run(string[] args)
        {
            if (args.Length < 2 || !Enum.TryParse(args[1], true, out WindLevel level) || level == WindLevel.Still || level == WindLevel.Steady)
                return Fail(Usage);
            int seeds = 5, history = 0;
            string output = null;
            bool all = true, belt = true, steady = false, drag = true;
            for (int i = 2; i < args.Length; i++)
            {
                string value = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "--seeds" when int.TryParse(value, out seeds) && seeds >= 1: i++; break;
                    case "--history" when int.TryParse(value, out history) && history >= 0: i++; break;
                    case "--out" when value != null: output = value; i++; break;
                    case "--pilots" when value == "nominal" || value == "all": all = value == "all"; i++; break;
                    case "--belt" when value == "on" || value == "off": belt = value == "on"; i++; break;
                    case "--wrong" when value == "steady" || value == "nodrag": steady = value == "steady"; drag = value != "nodrag"; i++; break;
                    default: return Fail(Usage);
                }
            }

            DroneSpec spec = PoweredFlightCase.Drone();
            WindHarness.Pilot[] pilots = all ? WindHarness.Pilots : new[] { WindHarness.Pilots[0] };
            Console.WriteLine($"flightbench: the wind harness (p7c-wind 1.2, K§8.5): {level}, the {WindPreset.Model} wind"
                + (drag ? "" : ", the known-wrong airframe without drag") + (steady ? ", the known-wrong fixed vector (no turbulence, direction changes or wakes)" : belt ? $" with its belt's wakes (H {WindHarness.BeltHeight:F1} m at {WindHarness.BeltAngleDeg:F0}°, porosity {WindHarness.BeltPorosity:F2})" : " without a belt")
                + $", {seeds} seeds × {pilots.Length} pilots, {WindHarness.Seconds:F0} s each, the first {WindHarness.DiscardSeconds:F0} s discarded");

            var runs = new WindHarness.Run[pilots.Length * seeds];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Parallel.For(0, runs.Length, j =>
                runs[j] = WindHarness.Fly(spec, level, (ulong)(j % seeds) + 1, j % seeds, seeds, pilots[j / seeds], belt, drag, steady));
            Console.WriteLine($"  {runs.Length} flights in {clock.Elapsed.TotalSeconds:F1} s");

            foreach (WindHarness.Run r in runs)
            {
                double beltSeconds = r.BeltLast > 0 ? (r.BeltLast - r.BeltFirst + 1) / HarnessPilot.CameraHz : 0;
                Console.WriteLine($"  {r.Pilot,-7} seed {r.Seed}: {r.HeightRef:F0} m, wind from {r.FromDeg:F1}° at {r.MeanSpeed:F2} m/s, course {r.CourseDeg:F1}°, "
                    + $"{r.Mass:F3} kg ({r.RollInertia:F4} / {r.PitchInertia:F4} / {r.YawInertia:F4} kg·m²); height off by {r.MeanHeightError:F2} m; airspeed {r.MeanAirspeed:F2} m/s, pitch {r.MeanPitchDeg:F2}°, roll {r.MeanRollDeg:F2}° against {r.MeanCrosswind:F2} m/s "
                    + $"from the left = {r.LeanPerMs:F3}° per m/s; {r.Samples.Length} frames, belt {r.Segment ?? "none"} ({beltSeconds:F2} s)");
            }

            bool ok = true;
            if (level != WindLevel.Calm && !steady && drag)
            {
                // 1.5's scenario's own lean at the cruise (CrosswindScenario), in the Severe centre's crosswind.
                double c = WindPreset.SevereCentre;
                double reference = WindHarness.CruiseRollPerMs(spec, c);
                Console.WriteLine($"  1.5's cruise lean at {WindHarness.ReleaseSpeed:F1} m/s in a steady {c:F1} m/s crosswind, as the tracker's roll: {reference:F3}° per m/s, "
                    + $"{reference * c:F1}° at {c:F1} m/s abeam (T0: 9 ± 3)");
                foreach (WindHarness.Run r in runs)
                {
                    if (r.Pilot != "nominal") continue;
                    double own = WindHarness.CruiseRollPerMs(spec, r.MeanCrosswind); // 1.5's at the run's own crosswind
                    bool lean = Reproduces(r.LeanPerMs, own, belt);
                    bool speed = r.MeanAirspeed / WindHarness.ReleaseSpeed >= 0.95 && r.MeanAirspeed / WindHarness.ReleaseSpeed <= 1.08;
                    ok &= lean && speed;
                    Console.WriteLine($"  nominal seed {r.Seed}: {r.LeanPerMs:F3}° per m/s in {r.MeanCrosswind:F2} m/s, {100 * (r.LeanPerMs / own - 1):+0.0;-0.0} % off 1.5's there ({own:F3}): {(lean ? "PASS" : "FAIL")}; "
                        + $"airspeed {r.MeanAirspeed:F2} m/s, {100 * (r.MeanAirspeed / WindHarness.ReleaseSpeed - 1):+0.0;-0.0} % off the cruise (−5 to +8 %): {(speed ? "PASS" : "FAIL")}");
                }
                WindHarness.Run wrong = WindHarness.Fly(spec, level, 1, 0, seeds, WindHarness.Pilots[0], belt, drag: false);
                bool caught = !Reproduces(wrong.LeanPerMs, reference, belt);
                ok &= caught;
                Console.WriteLine($"  known-wrong: no drag, seed 1: {wrong.LeanPerMs:F3}° per m/s: {(caught ? "fails, as it must" : "PASSES: the check cannot tell it (FAIL)")}");
            }

            if (output != null)
            {
                string dir = Path.Combine(output, level.ToString().ToLowerInvariant() + (steady ? "-steady" : !drag ? "-nodrag" : belt ? "" : "-nobelt"));
                var manifest = new List<object>();
                foreach (WindHarness.Run r in runs)
                {
                    string csv = Path.Combine(r.Pilot, $"seed-{r.Seed}.csv");
                    Directory.CreateDirectory(Path.Combine(dir, r.Pilot));
                    File.WriteAllText(Path.Combine(dir, csv), WindHarness.Csv(r));
                    manifest.Add(new
                    {
                        csv = csv.Replace('\\', '/'), segment = r.Segment, pilot = r.Pilot, seed = r.Seed, height_m = r.HeightRef,
                        wind_from_deg = r.FromDeg, wind_mean_m_s = r.MeanSpeed, course_deg = r.CourseDeg, start_s = r.StartSecond, mass_kg = r.Mass,
                        inertia_roll_pitch_yaw_kg_m2 = new[] { r.RollInertia, r.PitchInertia, r.YawInertia }, mean_height_error_m = r.MeanHeightError,
                        frames = r.Samples.Length, fps = HarnessPilot.CameraHz, mean_airspeed_m_s = r.MeanAirspeed, mean_roll_deg = r.MeanRollDeg,
                        mean_pitch_deg = r.MeanPitchDeg, mean_crosswind_m_s = r.MeanCrosswind, lean_deg_per_m_s = r.LeanPerMs,
                    });
                }
                var doc = new
                {
                    harness = "p7c-wind 1.5 (K§8.5)", level = level.ToString(), wind = steady ? "steady (known-wrong)" : WindPreset.Model, belt = belt && !steady,
                    belt_height_m = belt ? WindHarness.BeltHeight : 0, seconds = WindHarness.Seconds, discarded_s = WindHarness.DiscardSeconds,
                    how = "per run: wind_stats.py --csv <dir>/<csv> --segment <segment> --json <out>; T0c: --wind-log <dir>/history/*.csv",
                    runs = manifest,
                };
                File.WriteAllText(Path.Combine(dir, "harness.json"), JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                if (history > 0)
                {
                    Directory.CreateDirectory(Path.Combine(dir, "history"));
                    Parallel.For(1, history + 1, s =>
                    {
                        double[] h = WindHarness.History(level, (ulong)s, 120, out double drawn);
                        File.WriteAllText(Path.Combine(dir, "history", $"seed-{s}.csv"), WindHarness.HistoryCsv(h, drawn));
                    });
                }
                Console.WriteLine($"  wrote {runs.Length} runs{(history > 0 ? $" and {history} wind histories of 120 min" : "")} to {dir}");
            }
            Console.WriteLine($"wind harness: {(ok ? "PASS" : "FAIL")}");
            return ok ? 0 : 1;
        }

        // Within 15 % of 1.5's scenario's cruise lean (20 % with the belt, whose wakes 1.5 never flew). p7c-wind 1.5: 10 %
        // until Severe's centre moved to 7.2 m/s; a run's 34 s then hold about 1.5 integral times of the field's longest
        // eddies (L_u/U ≈ 23 s), whose own mean crosswind and its lag through the pilot move the lean by up to 11 %.
        static bool Reproduces(double perMs, double reference, bool belt) => WindHarness.Reproduces(perMs, reference, belt ? 0.2 : 0.15);

        static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 2;
        }
    }
}
