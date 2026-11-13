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
        public const int Seeds = 120;

        sealed class Cell
        {
            public string Name;
            public double Payload, Drop, Stick;
            public bool Gentle, Protect, Modes = true, Rings = true, Spreads = true;
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
                    cells.Add(new Cell { Name = "full punch, spreads off", Payload = payload, Drop = drop, Stick = 1, Spreads = false });
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
                if (!c.Spreads) setup.Asymmetry = null;
                c.Runs[s] = ShakeScenario.Fly(setup, c.Drop, c.Gentle, c.Stick);
            });
            Console.WriteLine($"the throttle-punch shake on seeds {first}-{first + Seeds - 1} a cell, the window {ShakeScenario.WindowSeconds:F1} s "
                + $"(min-max; the shake's length read to a tenth in brackets; 'end' is the shake's end less the arrest):");
            foreach (Cell c in cells)
                Console.WriteLine($"  {c.Payload:F0} kg, {(c.Drop == 0 ? "from the hover" : $"after a {c.Drop:F1} s drop")}, {c.Name}: {Line(c)}");
            Cell flexHeavy = Find(cells, 2, 0.6, "full punch"), rigid = Find(cells, 2, 0.6, "full punch, rigid airframe");
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
            int fullLasts = 0, ringlessLasts = 0, spreadlessLasts = 0;
            int ramps = 0, wobbles = 0, halfShorterPairs = 0;
            var ratios = new List<double>();
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.3, 0.6, 1.0 })
                {
                    Cell full = Find(cells, payload, drop, "full punch"), half = Find(cells, payload, drop, "0.8 step"), gentle = Find(cells, payload, drop, "gentle ramp");
                    Cell prot = Find(cells, payload, drop, "full punch, 40 A protection"), ringsOff = Find(cells, payload, drop, "full punch, rings off");
                    Cell even = Find(cells, payload, drop, "full punch, spreads off");
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
                        if (ShakeScenario.PastTheKick(even.Runs[s])) spreadlessLasts++;
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
                            outside.Add($"seed {first + s} lasts {f.Duration:F2} s to {f.Last + ShakeScenario.FieldSeconds - f.ArrestedAfter:+0.00;-0.00} s of its arrest");
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
            int rings = 0, ends = 0, published = 0, flown = 0, fastShown = 0, heavyShown = 0;
            double yLeast = double.MaxValue, endLate = double.MinValue, ringLeast = double.MaxValue;
            foreach (Cell c in cells)
            {
                foreach (ShakeScenario.Result r in c.Runs)
                {
                    flown++;
                    if (r.Published == c.Modes) published++;
                }
                if (c.Drop == 0 || c.Protect || !c.Modes || !c.Rings || !c.Spreads) continue;
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
                    if (c.Drop == 1.0 && r.Fields > 0)
                    {
                        if (c.Payload == 0) fastShown++;
                        else heavyShown++;
                    }
                    if (!double.IsNaN(r.ArrestedAfter) && r.Fields > 0) endLate = Math.Max(endLate, r.Last + ShakeScenario.FieldSeconds - r.ArrestedAfter);
                }
            }
            Check($"every sharp arrest rings the frame's band over {ShakeScenario.Rings:F1} m/s²", rings == 2 * arrests, $"{rings}/{2 * arrests}; the Y band {yLeast:F2} m/s² at least");
            // Rates (D-053's rule). On the excess over the hover (design 3i, model 96, seeds 1-200) the empty punch from 9 m/s
            // shows on 200 of 200, the 2 kg one on 190 (its threshold over its hover's floor, the arrest's shake near it).
            Check("nearly every empty full punch from 9 m/s shows it, and most at 2 kg", fastShown >= Nearly(Seeds) && heavyShown >= ShakeScenario.Most * Seeds,
                $"empty {fastShown}/{Seeds}, 2 kg {heavyShown}/{Seeds}; from 2.5-9 m/s {fullShown}/{arrests}");
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
            // Its length follows the descent (design 3i): each payload's median is longest from 9 m/s, and the shake lasts past
            // the kick most often from it. On seeds 1-200 the medians are 0.20 / 0.14 / 0.42 s empty and 0.08 / 0.06 / 0.14 s at
            // 2 kg from 2.5 / 5.3 / 9 m/s, past the kick on 26 / 45 / 147 and 2 / 4 / 34 of 200: from 2.5 m/s the empty drone's
            // shake is the spin-up kick's, a little longer than from 5.3 m/s.
            var medians = new double[2, 3];
            var pastCount = new int[2, 3];
            bool rises = true;
            for (int p = 0; p < 2; p++)
            {
                for (int d = 0; d < 3; d++)
                {
                    Cell c = Find(cells, 2 * p, new[] { 0.3, 0.6, 1.0 }[d], "full punch");
                    medians[p, d] = Median(c);
                    foreach (ShakeScenario.Result r in c.Runs) if (ShakeScenario.PastTheKick(r)) pastCount[p, d]++;
                }
                for (int d = 0; d < 2; d++)
                    if (!(medians[p, 2] > medians[p, d] && pastCount[p, 2] > pastCount[p, d])) rises = false;
            }
            Check("its length follows the descent: each payload's median is longest from 9 m/s, and past the kick most often", rises,
                $"medians empty {medians[0, 0]:F2} / {medians[0, 1]:F2} / {medians[0, 2]:F2} s, 2 kg {medians[1, 0]:F2} / {medians[1, 1]:F2} / {medians[1, 2]:F2} s; "
                + $"past the kick empty {pastCount[0, 0]} / {pastCount[0, 1]} / {pastCount[0, 2]}, 2 kg {pastCount[1, 0]} / {pastCount[1, 1]} / {pastCount[1, 2]} of {Seeds}");
            {
                Cell c = Find(cells, 0, 1.0, "full punch");
                int inside = 0;
                foreach (ShakeScenario.Result r in c.Runs) if (ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7)) inside++;
                double median = Median(c);
                Check("most of the empty drone's full punches from 9 m/s shake 0.3-0.7 s, and the median",
                    ShakeScenario.PilotBand(inside, Seeds, median), $"{inside}/{Seeds}, the median {median:F2} s");
            }
            Console.WriteLine($"  (the 0.8 step's shake shorter than the full punch's, pair by pair: {halfShorterPairs}/{arrests})");
            // The causes past the kick (design 3i, seeds 1-200, 1200 full punches): the rotors re-ingesting their wash through
            // the arrest (PHY-09's ring) the main one (258 against 107 without it), D-064's motor spreads a second (217 with them
            // off). Each known-wrong's ratio to the model, bootstrapped from those seeds for 120 a cell: the ringless 0.41 (at
            // most 0.53 at its 99th percentile on 80), the spreadless 0.84 (under 1 at its 99th percentile on 80).
            Check("known-wrong, no ring (the rotors never re-ingest their wash): the shake lasts past the kick on at most two thirds as many",
                ShakeScenario.RingCarriesIt(fullLasts, ringlessLasts),
                $"past the kick ({ShakeScenario.KickSeconds:F1} s) on {ringlessLasts}/{arrests} against {fullLasts}; weaker {ringlessWeaker}/{arrests}; shown at all on "
                + $"{ringlessShown}/{arrests} against {fullShown}", knownWrong: true);
            Check("known-wrong, no motor spreads: the shake lasts past the kick on fewer", spreadlessLasts < fullLasts,
                $"{spreadlessLasts}/{arrests} against {fullLasts}", knownWrong: true);
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

        static void Check(string claim, bool ok, string measured, bool knownWrong = false)
        {
            Console.WriteLine($"  {claim}: {measured}{(measured.Length > 0 ? ": " : "")}{(ok ? (knownWrong ? "PASS (the known-wrong fails it)" : "PASS") : "FAIL")}");
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

        // The cell's median as the table reads it, the (Seeds/2 + 1)th length.
        static double Median(Cell c)
        {
            var d = new List<double>();
            foreach (ShakeScenario.Result r in c.Runs) d.Add(r.Duration);
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
                ring = new Range(), y = new Range(), tenths = new Range(), hover = new Range(), threshold = new Range(), episodes = new Range();
            foreach (ShakeScenario.Result r in c.Runs)
            {
                descent += r.Descent / Seeds;
                if (r.Fields > 0) shown++;
                dur.Add(r.Duration);
                tenths.Add(Math.Round(r.Duration * 10, MidpointRounding.AwayFromZero) / 10);
                tilt.Add(r.TiltMax);
                peak.Add(r.Peak);
                hover.Add(r.Hover);
                threshold.Add(r.Threshold);
                episodes.Add(r.Episodes);
                limited.Add(r.Limited);
                all.Add(r.AllLimited);
                ring.Add(r.InRing);
                y.Add(r.VibrationPeak.Y);
                if (!double.IsNaN(r.ArrestedAfter))
                {
                    arrested++;
                    arrest.Add(r.ArrestedAfter);
                    if (r.Fields > 0) end.Add(r.Last + ShakeScenario.FieldSeconds - r.ArrestedAfter);
                }
                if (ShakeScenario.LastsTheArrest(r)) lastsArrest++;
                if (!double.IsNaN(r.ArrestedAfter) && r.Fields > 0 && r.Last + ShakeScenario.FieldSeconds >= r.ArrestedAfter) endsAfter++;
            }
            dur.Sort();
            int pilots = 0;
            foreach (double d in dur) if (ShakeScenario.LastsInTenths(d, 0.3, 0.7)) pilots++;
            return $"{descent:F2} m/s down; arrested {arrested}/{Seeds} at {arrest}; shown {shown}/{Seeds}, {Fmt(dur[0])}-{Fmt(dur[dur.Count - 1])} s "
                + $"(10 % {Fmt(dur[Seeds / 10])}, median {Fmt(dur[Seeds / 2])}, 90 % {Fmt(dur[Seeds - 1 - Seeds / 10])}; [{tenths}]; 0.3-0.7 s {pilots}/{Seeds}), "
                + $"end {end}, after the arrest {endsAfter}/{Seeds}; lasts the arrest {lastsArrest}/{Seeds}; episodes {episodes}; tilt {tilt}°, peak {peak}° "
                + $"over the hover's {hover}° (threshold {threshold}°), "
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
