using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // The throttle-punch shake's distribution (p7b-airframe, QA's findings 1–3): ShakeScenario on Seeds seeds a cell, the
    // two payloads, the four drops (the hover, 2.5, 5.3 and 9 m/s down) and the three inputs (the full punch, the 0.8 step
    // and the gentle ramp), each flight on its own seed and independent, flown in parallel; then the full punch with the
    // known-wrong ESC that senses no current (D-051) and on the known-wrong rigid airframe. It prints each cell's spread, the
    // table PHY-10's bands are stated from.
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

        public static bool Run()
        {
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
                FlightSetup setup = ShakeScenario.Setup(spec, c.Payload, (ulong)(s + 1), c.Modes, c.Limit);
                if (!c.Rings) setup.VortexRing = null;
                c.Runs[s] = ShakeScenario.Fly(setup, c.Drop, c.Gentle, c.Stick);
            });
            Console.WriteLine($"the throttle-punch shake on seeds 1-{Seeds} a cell, the window {ShakeScenario.WindowSeconds:F1} s "
                + $"(min-max; the shake's length read to a tenth in brackets; 'end' is the shake's end less the arrest):");
            Cell flexHeavy = null;
            foreach (Cell c in cells)
            {
                if (c.Payload == 2 && c.Drop == 0.6 && c.Name == "full punch") flexHeavy = c;
                Console.WriteLine($"  {c.Payload:F0} kg, {(c.Drop == 0 ? "from the hover" : $"after a {c.Drop:F1} s drop")}, {c.Name}: {Line(c)}");
            }
            Cell rigid = cells[cells.Count - 1];
            double lo = double.MaxValue, hi = 0;
            for (int s = 0; s < Seeds; s++)
            {
                double ratio = rigid.Runs[s].Peak / flexHeavy.Runs[s].Peak;
                lo = Math.Min(lo, ratio);
                hi = Math.Max(hi, ratio);
            }
            double longer = 0;
            for (int s = 0; s < Seeds; s++) longer = Math.Max(longer, Math.Abs(rigid.Runs[s].Duration - flexHeavy.Runs[s].Duration));
            Console.WriteLine($"  the rigid airframe's peak over the flexing one's, seed by seed: {lo:F2}-{hi:F2}; its shake's length within {longer:F2} s of it");
            Console.WriteLine("  seed by seed, each arrested descent's full punch against its 0.8 step, its gentle ramp and the known-wrongs:");
            foreach (double payload in new[] { 0.0, 2.0 })
                foreach (double drop in new[] { 0.3, 0.6, 1.0 })
                {
                    Cell full = Find(cells, payload, drop, "full punch"), half = Find(cells, payload, drop, "0.8 step"), gentle = Find(cells, payload, drop, "gentle ramp");
                    Cell wrong = Find(cells, payload, drop, "full punch, no current limit"), ringsOff = Find(cells, payload, drop, "full punch, rings off");
                    int halfLimited = 0, halfShorter = 0, gentleBelow = 0, tiltOver = 0, peakOver = 0;
                    double gentleShare = 0, tiltRatio = double.MaxValue, ringsTilt = double.MaxValue, ringsTiltHi = 0;
                    var outside = new List<string>();
                    for (int s = 0; s < Seeds; s++)
                    {
                        ShakeScenario.Result f = full.Runs[s], h = half.Runs[s], g = gentle.Runs[s], w = wrong.Runs[s], o = ringsOff.Runs[s];
                        if (h.Limited < f.Limited) halfLimited++;
                        if (h.Duration < f.Duration) halfShorter++;
                        if (g.Peak < f.Peak) gentleBelow++;
                        gentleShare = Math.Max(gentleShare, g.Limited / f.Limited);
                        if (f.TiltMax > w.TiltMax) tiltOver++;
                        if (f.Peak > w.Peak) peakOver++;
                        tiltRatio = Math.Min(tiltRatio, f.TiltMax / w.TiltMax);
                        ringsTilt = Math.Min(ringsTilt, o.TiltMax / f.TiltMax);
                        ringsTiltHi = Math.Max(ringsTiltHi, o.TiltMax / f.TiltMax);
                        if (!ShakeScenario.LastsTheArrest(f) || f.First + f.Duration < f.ArrestedAfter)
                            outside.Add($"seed {s + 1} lasts {f.Duration:F2} s to {f.First + f.Duration - f.ArrestedAfter:+0.00;-0.00} s of its arrest");
                        if (payload == 0 && drop <= 0.6 && !ShakeScenario.LastsInTenths(f.Duration, 0.3, 0.7)) outside.Add($"seed {s + 1} {f.Duration:F2} s, peak {f.Peak:F2}°");
                    }
                    Console.WriteLine($"    {payload:F0} kg after a {drop:F1} s drop: the 0.8 step's ESC off its limit sooner {halfLimited}/{Seeds}, its shake shorter "
                        + $"{halfShorter}/{Seeds}; the gentle ramp's jitter below the punch's {gentleBelow}/{Seeds}, its ESC limit at most {gentleShare:F2} of the punch's; "
                        + $"the tilt over the no-limit ESC's {tiltOver}/{Seeds} (at least {tiltRatio:F1} times), the peak {peakOver}/{Seeds}; with the rings off the tilt "
                        + $"{ringsTilt:F2}-{ringsTiltHi:F2} of it{(outside.Count > 0 ? "; outside: " + string.Join(", ", outside) : "")}");
                }
            return true;
        }

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
