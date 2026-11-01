using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The throttle-punch shake's distribution (p7b-airframe, QA's findings 1–3 and the re-review's B1): ShakeScenario on
    // Seeds seeds a cell from a first seed (1 by default), the two payloads, the four drops (the hover, 2.5, 5.3 and 9 m/s
    // down) and the three inputs (the full punch, the 0.8 step and the gentle ramp), each flight on its own seed and
    // independent, flown in parallel; then the full punch with the known-wrong ESC that senses no current (D-051), with the
    // rings off and on the known-wrong rigid airframe. It prints each cell's spread, then checks each of PHY-10's claims on
    // these seeds and fails if one misses.
    internal static class ShakeCase
    {
        public const int Seeds = 40;

        sealed class Cell
        {
            public string Name;
            public double Payload, Drop, Stick;
            public bool Gentle, Limit = true, Modes = true, Rings = true;
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
                    cells.Add(new Cell { Name = "full punch, no current limit", Payload = payload, Drop = drop, Stick = 1, Limit = false });
                    cells.Add(new Cell { Name = "full punch, rings off", Payload = payload, Drop = drop, Stick = 1, Rings = false });
                }
            cells.Add(new Cell { Name = "full punch, rigid airframe", Payload = 2, Drop = 0.6, Stick = 1, Modes = false });
            var jobs = new List<(Cell cell, int seed)>();
            foreach (Cell c in cells)
                for (int s = 0; s < Seeds; s++) jobs.Add((c, s));
            Parallel.For(0, jobs.Count, j =>
            {
                (Cell c, int s) = jobs[j];
                FlightSetup setup = ShakeScenario.Setup(spec, c.Payload, (ulong)(first + s), c.Modes, c.Limit);
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
                if (ShakeScenario.HoldsTheArrest(rigid.Runs[s])) rigidLasts++;
                rigidSilent &= !rigid.Runs[s].Published;
            }
            Console.WriteLine($"  the rigid airframe's peak over the flexing one's, seed by seed: {lo:F2}-{hi:F2}; its shake's length within {longer:F2} s of it");
            Console.WriteLine("  seed by seed, each arrested descent's full punch against its 0.8 step, its gentle ramp and the known-wrongs:");

            // The tallies over the arrested descents (the three drops, both payloads) and the gentle ramps from 5-9 m/s.
            int arrests = 0, fullShown = 0, halfShown = 0, halfSooner = 0, halfShorterPairs = 0, wrongHolds = 0, wrongLess = 0, wrongHalf = 0;
            int ramps = 0, wobbles = 0, gentleHalf = 0;
            var sooner = new List<double>();
            var shares = new List<double>();
            var ratios = new List<double>();
            bool shorterSummed = true;
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.3, 0.6, 1.0 })
                {
                    Cell full = Find(cells, payload, drop, "full punch"), half = Find(cells, payload, drop, "0.8 step"), gentle = Find(cells, payload, drop, "gentle ramp");
                    Cell wrong = Find(cells, payload, drop, "full punch, no current limit"), ringsOff = Find(cells, payload, drop, "full punch, rings off");
                    int halfLimited = 0, halfShorter = 0, gentleBelow = 0, tiltOver = 0, peakOver = 0;
                    double gentleShare = 0, tiltRatio = 0, ringsTilt = double.MaxValue, ringsTiltHi = 0, halfSum = 0, fullSum = 0;
                    var outside = new List<string>();
                    for (int s = 0; s < Seeds; s++)
                    {
                        ShakeScenario.Result f = full.Runs[s], h = half.Runs[s], g = gentle.Runs[s], w = wrong.Runs[s], o = ringsOff.Runs[s];
                        arrests++;
                        if (f.Fields > 0) fullShown++;
                        if (h.Fields > 0) halfShown++;
                        if (h.Limited < f.Limited) halfLimited++;
                        sooner.Add(f.Limited - h.Limited);
                        if (h.Duration < f.Duration) halfShorter++;
                        halfSum += h.Duration;
                        fullSum += f.Duration;
                        if (g.Peak < f.Peak) gentleBelow++;
                        gentleShare = Math.Max(gentleShare, g.Limited / f.Limited);
                        if (drop >= 0.6)
                        {
                            ramps++;
                            shares.Add(g.Limited / f.Limited);
                            if (ShakeScenario.Wobbles(g, f)) wobbles++;
                            if (g.Limited <= 0.5 * f.Limited) gentleHalf++;
                        }
                        if (f.TiltMax > w.TiltMax) tiltOver++;
                        if (f.Peak > w.Peak) peakOver++;
                        tiltRatio = Math.Max(tiltRatio, w.TiltMax / f.TiltMax);
                        ratios.Add(w.TiltMax / f.TiltMax);
                        if (ShakeScenario.HoldsTheArrest(w)) wrongHolds++;
                        if (w.TiltMax < f.TiltMax) wrongLess++;
                        if (w.TiltMax < 0.5 * f.TiltMax) wrongHalf++;
                        ringsTilt = Math.Min(ringsTilt, o.TiltMax / f.TiltMax);
                        ringsTiltHi = Math.Max(ringsTiltHi, o.TiltMax / f.TiltMax);
                        if (!ShakeScenario.LastsTheArrest(f) || f.First + f.Duration < f.ArrestedAfter)
                            outside.Add($"seed {first + s} lasts {f.Duration:F2} s to {f.First + f.Duration - f.ArrestedAfter:+0.00;-0.00} s of its arrest");
                        if (payload == 0 && drop <= 0.6 && !ShakeScenario.LastsInTenths(f.Duration, 0.3, 0.7)) outside.Add($"seed {first + s} {f.Duration:F2} s, peak {f.Peak:F2}°");
                        if (h.Fields == 0) outside.Add($"seed {first + s}'s 0.8 step not shown (peak {h.Peak:F3}°)");
                        if (drop >= 0.6 && g.Limited > 0.5 * f.Limited) outside.Add($"seed {first + s}'s gentle ramp limited {g.Limited / f.Limited:F2} of the punch's time");
                        if (w.TiltMax >= 0.5 * f.TiltMax) outside.Add($"seed {first + s}'s no-limit tilt {w.TiltMax / f.TiltMax:F2} of the punch's");
                    }
                    halfSooner += halfLimited;
                    halfShorterPairs += halfShorter;
                    shorterSummed &= halfSum < fullSum;
                    Console.WriteLine($"    {payload:F0} kg after a {drop:F1} s drop: the 0.8 step's ESC off its limit sooner {halfLimited}/{Seeds}, its shake shorter "
                        + $"{halfShorter}/{Seeds} (summed {halfSum / fullSum:F2} of the punch's); the gentle ramp's jitter below the punch's {gentleBelow}/{Seeds}, "
                        + $"its ESC limit at most {gentleShare:F2} of the punch's; the tilt over the no-limit ESC's {tiltOver}/{Seeds} (the no-limit's at most "
                        + $"{tiltRatio:F2} of it), the peak {peakOver}/{Seeds}; with the rings off the tilt {ringsTilt:F2}-{ringsTiltHi:F2} of it"
                        + $"{(outside.Count > 0 ? "; outside: " + string.Join(", ", outside) : "")}");
                }

            // PHY-10's claims (the spec's three scenarios; D-053): "every" on every flight here, "nearly every" on at least
            // NearlyEvery of them, "most" on at least Most of a cell and its median.
            Console.WriteLine($"PHY-10's claims on seeds {first}-{first + Seeds - 1} ('nearly every': at least {Nearly(arrests)} of {arrests}, {Nearly(ramps)} of {ramps}, {Nearly(Seeds)} of {Seeds}):");
            // "Shake only when its causes meet".
            int hoverGentle = 0, hoverSteps = 0;
            double kick = 0, kickLimited = 0, gentlePeak = 0;
            foreach (double payload in new[] { 0.0, 2.0 })
            {
                foreach (ShakeScenario.Result r in Find(cells, payload, 0, "gentle ramp").Runs)
                {
                    if (r.Fields == 0) hoverGentle++;
                    gentlePeak = Math.Max(gentlePeak, r.Peak);
                    kickLimited = Math.Max(kickLimited, r.Limited);
                }
                foreach (string name in new[] { "full punch", "0.8 step" })
                    foreach (ShakeScenario.Result r in Find(cells, payload, 0, name).Runs)
                    {
                        hoverSteps++;
                        kick = Math.Max(kick, r.Duration);
                        kickLimited = Math.Max(kickLimited, r.Limited);
                    }
            }
            Check("from the hover a gentle increase never shows the shake", hoverGentle == 2 * Seeds, $"{hoverGentle}/{2 * Seeds}, its peak at most {gentlePeak:F3}° against the visible {ShakeScenario.Visible:F3}°");
            Check($"from the hover a sharp step shows only the kick, under {ShakeScenario.KickSeconds:F1} s, every ESC off its limit within {ShakeScenario.KickLimited:F1} s",
                kick < ShakeScenario.KickSeconds && kickLimited < ShakeScenario.KickLimited, $"{hoverSteps} steps; the kick at most {kick:F2} s, an ESC limited at most {kickLimited:F2} s");
            int meets = 0, holds = 0, published = 0, flown = 0;
            double yLeast = double.MaxValue, limitLeast = double.MaxValue, holdMiss = 0, endLate = double.MinValue, allLo = double.MaxValue, allHi = 0, ringLeast = double.MaxValue;
            foreach (Cell c in cells)
            {
                foreach (ShakeScenario.Result r in c.Runs)
                {
                    flown++;
                    if (r.Published == c.Modes) published++;
                }
                if (c.Drop == 0 || !c.Limit || !c.Modes || !c.Rings) continue;
                foreach (ShakeScenario.Result r in c.Runs)
                {
                    if (c.Gentle)
                    {
                        if (c.Drop >= 0.6) ringLeast = Math.Min(ringLeast, r.InRing);
                        continue;
                    }
                    if (r.Limited > 0 && r.VibrationPeak.Y > ShakeScenario.Rings) meets++;
                    yLeast = Math.Min(yLeast, r.VibrationPeak.Y);
                    limitLeast = Math.Min(limitLeast, r.Limited);
                    if (c.Stick < 1) continue;
                    if (ShakeScenario.HoldsTheArrest(r)) holds++;
                    if (double.IsNaN(r.ArrestedAfter)) continue;
                    holdMiss = Math.Max(holdMiss, Math.Abs(r.Limited - r.ArrestedAfter));
                    if (r.Fields > 0) endLate = Math.Max(endLate, r.First + r.Duration - r.ArrestedAfter);
                    allLo = Math.Min(allLo, r.AllLimited);
                    allHi = Math.Max(allHi, r.AllLimited);
                }
            }
            Check($"every sharp arrest meets an ESC's current limit and rings the frame's band over {ShakeScenario.Rings:F1} m/s²", meets == 2 * arrests,
                $"{meets}/{2 * arrests}; an ESC limited {limitLeast:F2} s, the Y band {yLeast:F2} m/s² at least");
            Check("nearly every full punch shows it", fullShown >= Nearly(arrests), $"{fullShown}/{arrests}");
            Check("nearly every 0.8 step shows it", halfShown >= Nearly(arrests), $"{halfShown}/{arrests}");
            shares.Sort();
            Check("every gentle ramp from 5-9 m/s wobbles: shown, its rotors in their ring over 1 s, an ESC limited for less than the full punch's time",
                wobbles == ramps, $"{wobbles}/{ramps}; the ring {ringLeast:F2} s at least, the share at most {shares[shares.Count - 1]:F2} (median {Quantile(shares, 0.5):F2}, 95 % {Quantile(shares, 0.95):F2})");
            Check("nearly every gentle ramp's ESC is limited at most half the full punch's time", gentleHalf >= Nearly(ramps), $"{gentleHalf}/{ramps}");
            Check("the shake varies between occurrences: neighbouring seeds' peaks differ in every cell", Varies(cells), "");
            // "The shake lasts as long as the arrest".
            Check("every full punch holds an ESC through the arrest, all four for its first part, its shake ending within the loop's settling",
                holds == arrests, $"{holds}/{arrests}; the hold within {holdMiss:F3} s of the arrest, all four {allLo:F2}-{allHi:F2} s, the end at most {endLate:+0.00;-0.00} s after the arrest (Settle {ShakeScenario.Settle:F2} s)");
            var medians = new double[2, 3];
            bool rises = true;
            for (int p = 0; p < 2; p++)
                for (int d = 0; d < 3; d++)
                {
                    medians[p, d] = Median(Find(cells, 2 * p, new[] { 0.3, 0.6, 1.0 }[d], "full punch"));
                    if (d > 0 && medians[p, d] <= medians[p, d - 1]) rises = false;
                    if (p > 0 && medians[p, d] <= medians[0, d]) rises = false;
                }
            Check("its length follows the arrest: the medians rise with the descent and the payload", rises,
                $"empty {medians[0, 0]:F2} / {medians[0, 1]:F2} / {medians[0, 2]:F2} s, 2 kg {medians[1, 0]:F2} / {medians[1, 1]:F2} / {medians[1, 2]:F2} s");
            foreach (double drop in new[] { 0.3, 0.6 })
            {
                Cell c = Find(cells, 0, drop, "full punch");
                int inside = 0;
                foreach (ShakeScenario.Result r in c.Runs) if (ShakeScenario.LastsInTenths(r.Duration, 0.3, 0.7)) inside++;
                double median = Median(c);
                Check($"most of the empty drone's full punches after a {drop:F1} s drop shake 0.3-0.7 s, and the median",
                    ShakeScenario.PilotBand(inside, Seeds, median), $"{inside}/{Seeds}, the median {median:F2} s; {Groups(c, first)}");
            }
            sooner.Sort();
            Check("nearly every 0.8 step lets its ESCs go sooner", halfSooner >= Nearly(arrests), $"{halfSooner}/{arrests}, {sooner[0]:F3} s sooner at least (1 % {Quantile(sooner, 0.01):F3}, 5 % {Quantile(sooner, 0.05):F3}, median {Quantile(sooner, 0.5):F3} s)");
            Check("the 0.8 steps' shakes are shorter in sum, in every cell", shorterSummed, $"pair by pair {halfShorterPairs}/{arrests}");
            // "The visible shake is the rigid body's".
            ratios.Sort();
            Check("known-wrong, an ESC that senses no current: none holds the arrest", wrongHolds == 0, $"{arrests - wrongHolds}/{arrests} fail HoldsTheArrest");
            Check("known-wrong, no current limit: every one tilts less far", wrongLess == arrests,
                $"{wrongLess}/{arrests}; its tilt over the punch's at most {ratios[ratios.Count - 1]:F2} (median {Quantile(ratios, 0.5):F2}, 95 % {Quantile(ratios, 0.95):F2})");
            Check("known-wrong, no current limit: nearly every one tilts less than half as far", wrongHalf >= Nearly(arrests), $"{wrongHalf}/{arrests}");
            double rigidMedian = Median(rigid), flexMedian = Median(flexHeavy);
            Check("known-wrong, a rigid airframe: its visible shake within 20 % of the flexing one's on every seed, holding the arrest, publishing no camera vibration",
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
