using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The throttle-punch shake's distribution (p7b-airframe, QA's findings 1–3, the re-review's B1 and the integration
    // review's B1): ShakeScenario on Seeds seeds a cell from a first seed (1 by default), the two payloads, the four drops
    // (the hover, 2.5, 5.3 and 9 m/s down) and the three inputs (the full punch, the 0.8 step and the gentle ramp), each
    // flight on its own seed and independent, flown in parallel; then the full punch with 40 A current protection (D-051's alternative; the drone's own
    // ESC, the class's, senses no current, D-066), with the known-wrong rings off and on the known-wrong rigid airframe. It prints each cell's spread, then checks each of PHY-10's claims on
    // these seeds and fails if one misses.
    internal static class ShakeCase
    {
        public const int Seeds = 40;

        sealed class Cell
        {
            public string Name;
            public double Payload, Drop, Stick;
            public bool Gentle, Protect, Modes = true, Rings = true;
            public ShakeScenario.Result[] Runs = new ShakeScenario.Result[Seeds];
        }

        static bool _ok;

        public static bool Run(int first)
        {
            _ok = true;
            DroneSpec spec = PoweredFlightCase.Drone();
            var cells = new List<Cell>();
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.0, 0.3, 0.6, 1.0 })
                {
                    cells.Add(new Cell { Name = "full punch", Payload = payload, Drop = drop, Stick = 1 });
                    cells.Add(new Cell { Name = "0.8 step", Payload = payload, Drop = drop, Stick = 0.8 });
                    cells.Add(new Cell { Name = "gentle ramp", Payload = payload, Drop = drop, Stick = 1, Gentle = true });
                }
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.3, 0.6, 1.0 })
                {
                    cells.Add(new Cell { Name = "full punch, 40 A protection", Payload = payload, Drop = drop, Stick = 1, Protect = true });
                    cells.Add(new Cell { Name = "full punch, rings off", Payload = payload, Drop = drop, Stick = 1, Rings = false });
                }
            cells.Add(new Cell { Name = "full punch, rigid airframe", Payload = 2, Drop = 0.6, Stick = 1, Modes = false });
            var jobs = new List<(Cell cell, int seed)>();
            foreach (Cell c in cells)
                for (int s = 0; s < Seeds; s++) jobs.Add((c, s));
            Parallel.For(0, jobs.Count, j =>
            {
                (Cell c, int s) = jobs[j];
                FlightSetup setup = ShakeScenario.Setup(spec, c.Payload, (ulong)(first + s), c.Modes, c.Protect);
                if (!c.Rings) setup.VortexRing = null;
                c.Runs[s] = ShakeScenario.Fly(setup, c.Drop, c.Gentle, c.Stick);
            });
            Console.WriteLine($"the throttle-punch shake on seeds {first}-{first + Seeds - 1} a cell, the window {ShakeScenario.WindowSeconds:F1} s "
                + $"(min-max; the shake's length read to a tenth in brackets; 'end' is the shake's end less the arrest):");
            foreach (Cell c in cells)
                Console.WriteLine($"  {c.Payload:F0} kg, {(c.Drop == 0 ? "from the hover" : $"after a {c.Drop:F1} s drop")}, {c.Name}: {Line(c)}");
            Cell flexHeavy = Find(cells, 2, 0.6, "full punch"), rigid = cells[cells.Count - 1];
            double lo = double.MaxValue, hi = 0, longer = 0;
            int rigidAsLong = 0, rigidLasts = 0;
            bool rigidSilent = true;
            for (int s = 0; s < Seeds; s++)
            {
                double ratio = rigid.Runs[s].Peak / flexHeavy.Runs[s].Peak, apart = Math.Abs(rigid.Runs[s].Duration - flexHeavy.Runs[s].Duration);
                lo = Math.Min(lo, ratio);
                hi = Math.Max(hi, ratio);
                longer = Math.Max(longer, apart);
                if (apart <= ThreeFields) rigidAsLong++;
                if (ShakeScenario.EndsWithTheArrest(rigid.Runs[s])) rigidLasts++;
                rigidSilent &= !rigid.Runs[s].Published;
            }
            Console.WriteLine($"  the rigid airframe's peak over the flexing one's, seed by seed: {lo:F2}-{hi:F2}; its shake's length within {longer:F2} s of it");
            Console.WriteLine("  seed by seed, each arrested descent's full punch against its 0.8 step, its gentle ramp and the known-wrongs:");

            // The tallies over the arrested descents (the three drops, both payloads) and the gentle ramps from 5-9 m/s.
            int arrests = 0, fullShown = 0, ringlessShown = 0, ringlessWeaker = 0, protectHolds = 0, protectTilts = 0, protectLonger = 0;
            int fullLasts = 0, ringlessLasts = 0;
            int ramps = 0, wobbles = 0, halfShorterPairs = 0;
            var ratios = new List<double>();
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.3, 0.6, 1.0 })
                {
                    Cell full = Find(cells, payload, drop, "full punch"), half = Find(cells, payload, drop, "0.8 step"), gentle = Find(cells, payload, drop, "gentle ramp");
                    Cell prot = Find(cells, payload, drop, "full punch, 40 A protection"), ringsOff = Find(cells, payload, drop, "full punch, rings off");
                    int halfShorter = 0, shownHere = 0, ringlessHere = 0, lastsHere = 0, ringlessLastsHere = 0, weakerHere = 0;
                    double halfSum = 0, fullSum = 0, ringsTilt = double.MaxValue, ringsTiltHi = 0;
                    var outside = new List<string>();
                    for (int s = 0; s < Seeds; s++)
                    {
                        ShakeScenario.Result f = full.Runs[s], h = half.Runs[s], g = gentle.Runs[s], w = prot.Runs[s], o = ringsOff.Runs[s];
                        arrests++;
                        if (f.Fields > 0) { fullShown++; shownHere++; }
                        if (o.Fields > 0) { ringlessShown++; ringlessHere++; }
                        if (ShakeScenario.PastTheKick(f)) { fullLasts++; lastsHere++; }
                        if (ShakeScenario.PastTheKick(o)) { ringlessLasts++; ringlessLastsHere++; }
                        if (o.Peak < f.Peak) { ringlessWeaker++; weakerHere++; }
                        if (h.Duration < f.Duration) halfShorter++;
                        halfSum += h.Duration;
                        fullSum += f.Duration;
                        if (drop >= 0.6)
                        {
                            ramps++;
                            if (ShakeScenario.Wobbles(g)) wobbles++;
                        }
                        if (ShakeScenario.HoldsTheLimit(w)) protectHolds++;
                        if (w.TiltMax > f.TiltMax) protectTilts++;
                        if (w.ArrestedAfter > f.ArrestedAfter) protectLonger++;
                        ratios.Add(w.TiltMax / f.TiltMax);
                        ringsTilt = Math.Min(ringsTilt, o.TiltMax / f.TiltMax);
                        ringsTiltHi = Math.Max(ringsTiltHi, o.TiltMax / f.TiltMax);
                        if (!ShakeScenario.EndsWithTheArrest(f))
                            outside.Add($"seed {first + s} lasts {f.Duration:F2} s to {f.First + f.Duration - f.ArrestedAfter:+0.00;-0.00} s of its arrest");
                        if (payload == 0 && drop == 1.0 && !ShakeScenario.LastsInTenths(f.Duration, 0.3, 0.7)) outside.Add($"seed {first + s} {f.Duration:F2} s, peak {f.Peak:F2}°");
                    }
                    halfShorterPairs += halfShorter;
                    Console.WriteLine($"    {payload:F0} kg after a {drop:F1} s drop: shown {shownHere}/{Seeds}, with the rings off {ringlessHere}/{Seeds}; past the kick "
                        + $"{lastsHere}/{Seeds}, with the rings off {ringlessLastsHere}/{Seeds}; the rings off weaker {weakerHere}/{Seeds} (their tilt "
                        + $"{ringsTilt:F2}-{ringsTiltHi:F2} of it); the 0.8 step's shake shorter {halfShorter}/{Seeds} (summed {halfSum / fullSum:F2} of the punch's)"
                        + $"{(outside.Count > 0 ? "; outside: " + string.Join(", ", outside) : "")}");
                }

            // PHY-10's claims on the class's ESC, which senses no current (the spec's three scenarios; D-053, D-066): "every"
            // on every flight here, "nearly every" on at least NearlyEvery of them, "most" on at least Most of a cell and its
            // median.
            Console.WriteLine($"PHY-10's claims on seeds {first}-{first + Seeds - 1} ('nearly every': at least {Nearly(arrests)} of {arrests}, {Nearly(Seeds)} of {Seeds}):");
            // "Shake only when its causes meet".
            int hoverGentle = 0, hoverSteps = 0;
            double kick = 0, gentlePeak = 0;
            foreach (double payload in new[] { 0.0, 2.0 })
            {
                foreach (ShakeScenario.Result r in Find(cells, payload, 0, "gentle ramp").Runs)
                {
                    if (r.Fields == 0) hoverGentle++;
                    gentlePeak = Math.Max(gentlePeak, r.Peak);
                }
                foreach (string name in new[] { "full punch", "0.8 step" })
                    foreach (ShakeScenario.Result r in Find(cells, payload, 0, name).Runs)
                    {
                        hoverSteps++;
                        kick = Math.Max(kick, r.Duration);
                    }
            }
            Check("from the hover a gentle increase never shows the shake", hoverGentle == 2 * Seeds, $"{hoverGentle}/{2 * Seeds}, its peak at most {gentlePeak:F3}° against the visible {ShakeScenario.Visible:F3}°");
            Check($"from the hover a sharp step shows only the kick, under {ShakeScenario.KickSeconds:F1} s", kick < ShakeScenario.KickSeconds, $"{hoverSteps} steps; the kick at most {kick:F2} s");
            int rings = 0, ends = 0, published = 0, flown = 0, fastShown = 0;
            double yLeast = double.MaxValue, endLate = double.MinValue, ringLeast = double.MaxValue;
            foreach (Cell c in cells)
            {
                foreach (ShakeScenario.Result r in c.Runs)
                {
                    flown++;
                    if (r.Published == c.Modes) published++;
                }
                if (c.Drop == 0 || c.Protect || !c.Modes || !c.Rings) continue;
                foreach (ShakeScenario.Result r in c.Runs)
                {
                    if (c.Gentle)
                    {
                        if (c.Drop >= 0.6) ringLeast = Math.Min(ringLeast, r.InRing);
                        continue;
                    }
                    if (r.VibrationPeak.Y > ShakeScenario.Rings) rings++;
                    yLeast = Math.Min(yLeast, r.VibrationPeak.Y);
                    if (c.Stick < 1) continue;
                    if (ShakeScenario.EndsWithTheArrest(r)) ends++;
                    if (c.Drop == 1.0 && r.Fields > 0) fastShown++;
                    if (!double.IsNaN(r.ArrestedAfter) && r.Fields > 0) endLate = Math.Max(endLate, r.First + r.Duration - r.ArrestedAfter);
                }
            }
            Check($"every sharp arrest rings the frame's band over {ShakeScenario.Rings:F1} m/s²", rings == 2 * arrests, $"{rings}/{2 * arrests}; the Y band {yLeast:F2} m/s² at least");
            // A rate too (D-053's rule): the 2 kg punch from 9 m/s peaks from 0.097° on the 520 seeds the claims were set on,
            // just over the visible line (0.088°), where all 1040 from 9 m/s showed it; one of the fresh sets flown after
            // the gentle ramp's restatement had one under it (seed 3205, 2 kg, at 0.0880°).
            Check("nearly every full punch from 9 m/s shows it", fastShown >= Nearly(2 * Seeds), $"{fastShown}/{2 * Seeds}; from 2.5-9 m/s {fullShown}/{arrests}");
            // The wobble's showing is a rate (D-053's rule): the 2 kg ramp from 5.4 m/s peaks at 0.10-0.28°, a fraction over
            // the visible line (0.088°), and seed 2421 of the first fresh sets flown after the integration review (2001-2040,
            // 2401-2440, 2801-2840) stayed at 0.085°. Its ring is every flight's: over 1.33 s on all 2080 ramps of the 520 seeds
            // the claims were set on, where all 2080 showed it.
            Check("every gentle ramp from 5-9 m/s keeps its rotors in their ring over 1 s, and nearly every one wobbles (shown)", ringLeast > 1.0 && wobbles >= Nearly(ramps),
                $"{wobbles}/{ramps}; the ring {ringLeast:F2} s at least");
            Check("the shake varies between occurrences: neighbouring seeds' peaks differ in every cell", Varies(cells), "");
            // "The shake lasts as long as the arrest".
            Check("every full punch is arrested and its shake ends within the loop's settling after the arrest", ends == arrests,
                $"{ends}/{arrests}; the end at most {endLate:+0.00;-0.00} s after the arrest (Settle {ShakeScenario.Settle:F2} s)");
            // Its length past the kick (D-053's rule; QA's integration review, B1): a full punch whose shake is shorter than
            // KickSeconds shows only the motors' spin-up kick, so it reads none. Each payload's median of that never falls as the
            // descent grows, and is longer from 9 m/s than from 2.5 m/s. At 2 kg the two slower descents show the kick or stay
            // under the visible line (past it on 10 of 1040 such flights on the 520 seeds this was set on), so they tie at none.
            // Bootstrapped from those seeds, a fresh 40 fails this about 0.05 % of the time; the whole lengths' medians rising
            // strictly (as this read before) 9.5 %, never falling 1.1 %. The whole lengths' medians are printed beside it.
            var medians = new double[2, 3];
            var past = new double[2, 3];
            bool rises = true;
            for (int p = 0; p < 2; p++)
            {
                for (int d = 0; d < 3; d++)
                {
                    Cell c = Find(cells, 2 * p, new[] { 0.3, 0.6, 1.0 }[d], "full punch");
                    medians[p, d] = Median(c);
                    past[p, d] = MedianPastTheKick(c);
                    if (d > 0 && past[p, d] < past[p, d - 1]) rises = false;
                }
                if (!(past[p, 2] > past[p, 0])) rises = false;
            }
            Check("its length follows the descent: past the kick, each payload's median never falls with it and is longer from 9 m/s than from 2.5 m/s", rises,
                $"past the kick empty {past[0, 0]:F2} / {past[0, 1]:F2} / {past[0, 2]:F2} s, 2 kg {past[1, 0]:F2} / {past[1, 1]:F2} / {past[1, 2]:F2} s "
                + $"(whole: empty {medians[0, 0]:F2} / {medians[0, 1]:F2} / {medians[0, 2]:F2} s, 2 kg {medians[1, 0]:F2} / {medians[1, 1]:F2} / {medians[1, 2]:F2} s)");
            {
                Cell c = Find(cells, 0, 1.0, "full punch");
                int inside = 0;
                foreach (ShakeScenario.Result r in c.Runs) if (ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7)) inside++;
                double median = Median(c);
                Check("most of the empty drone's full punches from 9 m/s shake 0.3-0.7 s, and the median",
                    ShakeScenario.PilotBand(inside, Seeds, median), $"{inside}/{Seeds}, the median {median:F2} s; {Groups(c, first)}");
            }
            Console.WriteLine($"  (the 0.8 step's shake shorter than the full punch's, pair by pair: {halfShorterPairs}/{arrests})");
            // The main cause: the rotors re-ingesting their wash through the arrest (PHY-09's ring), not a current limit. D-064's
            // motor spreads, which no current limit trims since D-066, are a second (QA's integration review, B2): they show as
            // the spin-up kick on ringed and ringless flights alike, and alone carry a ringless shake past the kick on some
            // (ShakeScenario.PastTheKick). So the ring's share is a rate: past the kick on at most half as many.
            Check("known-wrong, no ring (the rotors never re-ingest their wash): most full punches' shake weaker, lasting past the kick on at most half as many",
                ringlessWeaker >= ShakeScenario.Most * arrests && 2 * ringlessLasts <= fullLasts,
                $"weaker {ringlessWeaker}/{arrests}; past the kick ({ShakeScenario.KickSeconds:F1} s) on {ringlessLasts}/{arrests} against {fullLasts}; shown at all on "
                + $"{ringlessShown}/{arrests} against {fullShown}");
            ratios.Sort();
            // The hold is a rate (D-053's rule; QA's integration review, B1): D-064's resistance spread gives each ESC its own
            // limit voltage, so one can stay on it a field or two past the arrest (seed 720, empty from 5.3 m/s: limited to
            // 0.63 s, arrested at 0.58). Held on 3119 of 3120 arrests on the 520 seeds this was set on, longer on every one (by
            // 0.17 s at least). Tilting further is a rate too: on those seeds every one did, its tilt over the drone's 1.035 at
            // least; the third fresh trio flown after the restatements here (4801-4840) had one tilt less (seed 4834, empty from
            // 9 m/s: 1.71° against 1.93°), the drone's own tilt there set by D-064's spreads, not by a limit.
            Check("40 A current protection (D-051's alternative): nearly every arrest held at the limit, every one longer, nearly every one tilting further",
                protectHolds >= Nearly(arrests) && protectLonger == arrests && protectTilts >= Nearly(arrests),
                $"held {protectHolds}/{arrests}, longer {protectLonger}/{arrests}, further {protectTilts}/{arrests}; its tilt over the drone's at least {ratios[0]:F2} "
                + $"(median {Quantile(ratios, 0.5):F2})");
            // "The visible shake is the rigid body's".
            double rigidMedian = Median(rigid), flexMedian = Median(flexHeavy);
            Check("known-wrong, a rigid airframe: its visible shake within 20 % of the flexing one's on every seed, ending with the arrest, publishing no camera vibration",
                lo >= 0.8 && hi <= 1.2 && rigidLasts == Seeds && rigidSilent, $"{lo:F2}-{hi:F2}, {rigidLasts}/{Seeds}, published by none");
            Check("known-wrong, a rigid airframe: as long, the same median within a field and within three fields on nearly every seed",
                Math.Abs(rigidMedian - flexMedian) <= OneField && rigidAsLong >= Nearly(Seeds), $"medians {rigidMedian:F2} and {flexMedian:F2} s; {rigidAsLong}/{Seeds} within 0.06 s, {longer:F2} s apart at most");
            Check("the modes publish CameraVibration on every flight, the rigid airframe on none", published == flown, $"{published}/{flown}");
            Console.WriteLine($"shake: {(_ok ? "PASS" : "FAIL")}");
            return _ok;
        }

        const double OneField = FlightCore.FieldSteps * FlightCore.Dt + 1e-9, ThreeFields = 3 * FlightCore.FieldSteps * FlightCore.Dt + 1e-9;

        // "Nearly every" of a count: at least NearlyEvery of it, rounded up.
        static int Nearly(int of) => (int)Math.Ceiling(ShakeScenario.NearlyEvery * of - 1e-9);

        static void Check(string claim, bool ok, string measured)
        {
            Console.WriteLine($"  {claim}: {measured}{(measured.Length > 0 ? ": " : "")}{(ok ? "PASS" : "FAIL")}");
            _ok &= ok;
        }

        static bool Varies(List<Cell> cells)
        {
            foreach (Cell c in cells)
            {
                if (c.Drop == 0 || c.Gentle) continue;
                for (int s = 1; s < Seeds; s++)
                    if (c.Runs[s].Peak == c.Runs[s - 1].Peak) return false;
            }
            return true;
        }

        // TheShakeLastsAboutTheArrest's statistic on each group of ten seeds: inside 0.3-0.7 s on at least Most of them and
        // the median inside.
        static string Groups(Cell c, int first)
        {
            var failed = new List<string>();
            for (int g = 0; g < Seeds / 10; g++)
            {
                var lengths = new double[10];
                int inside = 0;
                for (int s = 0; s < 10; s++)
                {
                    lengths[s] = c.Runs[10 * g + s].Duration;
                    if (ShakeScenario.LastsInTenths(lengths[s], 0.3, 0.7)) inside++;
                }
                Array.Sort(lengths);
                if (!ShakeScenario.PilotBand(inside, 10, (lengths[4] + lengths[5]) / 2)) failed.Add($"{first + 10 * g}-{first + 10 * g + 9} ({inside}/10)");
            }
            return $"its 10-seed groups failing the suite's statistic: {(failed.Count == 0 ? "none" : string.Join(", ", failed))}";
        }

        // The cell's median as the table reads it, the 21st of 40 lengths.
        static double Median(Cell c)
        {
            var d = new List<double>();
            foreach (ShakeScenario.Result r in c.Runs) d.Add(r.Duration);
            d.Sort();
            return d[Seeds / 2];
        }

        // The same of the lengths past the kick: a shake shorter than KickSeconds reads none.
        static double MedianPastTheKick(Cell c)
        {
            var d = new List<double>();
            foreach (ShakeScenario.Result r in c.Runs) d.Add(ShakeScenario.PastTheKick(r) ? r.Duration : 0);
            d.Sort();
            return d[Seeds / 2];
        }

        static double Quantile(List<double> sorted, double q) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(q * sorted.Count) - 1)];

        static Cell Find(List<Cell> cells, double payload, double drop, string name) =>
            cells.Find(c => c.Payload == payload && c.Drop == drop && c.Name == name);

        static string Line(Cell c)
        {
            var dur = new List<double>();
            int shown = 0, arrested = 0, lastsArrest = 0, endsAfter = 0;
            double descent = 0;
            Range arrest = new Range(), tilt = new Range(), peak = new Range(), limited = new Range(), all = new Range(), end = new Range(),
                ring = new Range(), y = new Range(), tenths = new Range();
            foreach (ShakeScenario.Result r in c.Runs)
            {
                descent += r.Descent / Seeds;
                if (r.Fields > 0) shown++;
                dur.Add(r.Duration);
                tenths.Add(Math.Round(r.Duration * 10, MidpointRounding.AwayFromZero) / 10);
                tilt.Add(r.TiltMax);
                peak.Add(r.Peak);
                limited.Add(r.Limited);
                all.Add(r.AllLimited);
                ring.Add(r.InRing);
                y.Add(r.VibrationPeak.Y);
                if (!double.IsNaN(r.ArrestedAfter))
                {
                    arrested++;
                    arrest.Add(r.ArrestedAfter);
                    if (r.Fields > 0) end.Add(r.First + r.Duration - r.ArrestedAfter);
                }
                if (ShakeScenario.LastsTheArrest(r)) lastsArrest++;
                if (!double.IsNaN(r.ArrestedAfter) && r.Fields > 0 && r.First + r.Duration >= r.ArrestedAfter) endsAfter++;
            }
            dur.Sort();
            int pilots = 0;
            foreach (double d in dur) if (ShakeScenario.LastsInTenths(d, 0.3, 0.7)) pilots++;
            return $"{descent:F2} m/s down; arrested {arrested}/{Seeds} at {arrest}; shown {shown}/{Seeds}, {Fmt(dur[0])}-{Fmt(dur[dur.Count - 1])} s "
                + $"(10 % {Fmt(dur[Seeds / 10])}, median {Fmt(dur[Seeds / 2])}, 90 % {Fmt(dur[Seeds - 1 - Seeds / 10])}; [{tenths}]; 0.3-0.7 s {pilots}/{Seeds}), "
                + $"end {end}, after the arrest {endsAfter}/{Seeds}; lasts the arrest {lastsArrest}/{Seeds}; tilt {tilt}°, peak {peak}°, "
                + $"Y band {y} m/s²; an ESC limited {limited} s, all four {all} s; ring {ring} s";
        }

        static string Fmt(double v) => v.ToString("F2");

        sealed class Range
        {
            double _lo = double.MaxValue, _hi = double.MinValue;
            public void Add(double v)
            {
                _lo = Math.Min(_lo, v);
                _hi = Math.Max(_hi, v);
            }
            public override string ToString() => _lo > _hi ? "-" : $"{_lo:F2}-{_hi:F2}";
        }
    }
}
