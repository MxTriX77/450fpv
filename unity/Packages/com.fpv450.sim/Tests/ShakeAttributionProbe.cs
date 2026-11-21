using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// What carries the shake past the kick (PHY-10, the physics ledger's gap 10; p7b-airframe designs 3f and 3g): the six
    /// full punches of --shake on seeds 1–40, the drone and its ringless twin, each with one source changed: the rotors' own
    /// wake off (ShakeScenario.Setup flew none before 3g) or at model 92's level (0.029, its bound 3), the motors' spreads
    /// off (D-064's, D-070's second cause), the coil's canister and pack moved fore-aft until the centre of mass sits over
    /// the hubs' centre (p7d-tether 1.6's probe: the front motors' full duty in a hard arrest), the rigid airframe and the
    /// 40 A variant (design 3i). Per variant: the shake shown, past the kick, its median length, and the time in the window
    /// a front and a rear motor sit at full command; then the hold, a gentle step from the hover (no punch, no descent) and
    /// a sharp one. A printed table, never a verdict. SHAKE_PROBE_SEEDS sets the seeds (40 by default), SHAKE_PROBE_CSV a
    /// file for each flight's row.
    /// </summary>
    public class ShakeAttributionProbe
    {
        static readonly int Seeds = int.TryParse(Environment.GetEnvironmentVariable("SHAKE_PROBE_SEEDS"), out int n) ? n : 40;
        static readonly double[] Payloads = { 0, 2 }, Drops = { 0.3, 0.6, 1.0 };

        enum Variant { Model, NoWake, Level92, NoSpreads, Centred, Rigid, Protected }

        struct Count
        {
            public int Shown, Past;
            public double FrontFull, RearFull;
        }

        static FlightSetup Setup(Variant v, double payload, ulong seed, bool ring)
        {
            DroneSpec spec = DroneSpec.Parse(RepositoryDrone.Text()); // fresh: Centred moves its parts
            if (v == Variant.Centred)
            {
                // Move the canister and its pack together along z until the CoM's z is the hubs' mean (0).
                MassProperties m = spec.BuildMass(new Loadout { Payload = payload, Legs = true }).Properties;
                int can = spec.Coil.Housing, pack = spec.Coil.Part;
                MassModel built = spec.BuildMass(new Loadout { Payload = payload, Legs = true });
                double moved = built.Part(can).Mass + built.Part(pack).Mass, dz = -m.CenterOfMass.Z * m.Mass / moved;
                spec.Parts[can].Position.Z += dz;
                spec.Parts[pack].Position.Z += dz;
            }
            FlightSetup s = ShakeScenario.Setup(spec, payload, seed, v != Variant.Rigid, v == Variant.Protected);
            if (!ring) s.VortexRing = null;
            if (v == Variant.NoSpreads) s.Asymmetry = null;
            if (v == Variant.NoWake) s.RotorWake = null;
            if (v == Variant.Level92) GoldenFlights.Model92WakeLevel(s.RotorWake);
            return s;
        }

        /// <summary>The null flights Margin is read off, per payload (design 3i).</summary>
        public static int MarginFlights => int.TryParse(Environment.GetEnvironmentVariable("SHAKE_MARGIN_FLIGHTS"), out int n) ? n : 10000;

        // Design 3i's margin: the hover, then the hover's stick held for the window (ShakeScenario.Hold), on MarginFlights
        // seeds a payload; each window's largest field over its own hover's RMS. The largest of them is Margin: a fresh null
        // flight exceeds the largest of n exchangeable ones with probability 1/(n + 1).
        [Test, Explicit("a printed probe (2 × MarginFlights null flights)")]
        public void TheHoldsMargin()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int n = MarginFlights;
            double overall = 0;
            foreach (double payload in new[] { 0.0, 1.0, 2.0 })
            {
                var ratio = new double[n];
                var hover = new double[n];
                var peak = new double[n];
                System.Threading.Tasks.Parallel.For(0, n, j =>
                {
                    ShakeScenario.Result r = ShakeScenario.Hold(ShakeScenario.Setup(spec, payload, (ulong)(j + 1)));
                    ratio[j] = r.Peak / r.Hover;
                    hover[j] = r.Hover;
                    peak[j] = r.Peak;
                });
                Array.Sort(ratio);
                Array.Sort(hover);
                Array.Sort(peak);
                double Q(double[] a, double q) => a[Math.Min(a.Length - 1, (int)(q * a.Length))];
                overall = Math.Max(overall, ratio[n - 1]);
                TestContext.WriteLine($"{payload:F0} kg, {n} holds: the window's peak over its hover's RMS 50 % {Q(ratio, 0.5):F3}, 90 % {Q(ratio, 0.9):F3}, "
                    + $"99 % {Q(ratio, 0.99):F3}, 99.9 % {Q(ratio, 0.999):F3}, max {ratio[n - 1]:F3}; the hover's RMS {hover[0]:F4}–{Q(hover, 0.5):F4}–{hover[n - 1]:F4}°; "
                    + $"the window's peak {peak[0]:F4}–{Q(peak, 0.5):F4}–{peak[n - 1]:F4}° (visible {ShakeScenario.Visible:F3}°)");
            }
            TestContext.WriteLine($"the margin: {overall:F3} (ShakeScenario.Margin {ShakeScenario.Margin:F3})");
        }

        [Test, Explicit("a printed probe (about 2000 flights)")]
        public void WhatCarriesTheRinglessShake()
        {
            foreach (double payload in Payloads)
            {
                DroneSpec spec = DroneSpec.Parse(RepositoryDrone.Text()); // fresh: Centred moves its parts
                MassProperties m = spec.BuildMass(new Loadout { Payload = payload, Legs = true }).Properties;
                TestContext.WriteLine($"{payload:F0} kg: the centre of mass {m.CenterOfMass.Z * 1000:F2} mm forward of the hubs' centre, {m.Mass:F3} kg");
            }
            var variants = (Variant[])Enum.GetValues(typeof(Variant));
            int cells = Payloads.Length * Drops.Length, n = variants.Length * 2 * cells * Seeds;
            var runs = new ShakeScenario.Result[n];
            var front = new double[n];
            var rear = new double[n];
            System.Threading.Tasks.Parallel.For(0, n, j =>
            {
                int seed = j % Seeds, cell = j / Seeds % cells, ring = j / (Seeds * cells) % 2, v = j / (2 * Seeds * cells);
                double payload = Payloads[cell / Drops.Length], drop = Drops[cell % Drops.Length];
                FlightSetup setup = Setup(variants[v], payload, (ulong)(seed + 1), ring == 1);
                double f = 0, r = 0;
                var flight = new FlightCore(setup);
                runs[j] = ShakeScenario.Fly(flight, setup, drop, false, after: (core, sticks) =>
                {
                    ref readonly FlightState st = ref core.State;
                    if (st.EscCommand[1] >= 0.999 || st.EscCommand[3] >= 0.999) f += FlightCore.Dt;
                    if (st.EscCommand[0] >= 0.999 || st.EscCommand[2] >= 0.999) r += FlightCore.Dt;
                });
                front[j] = f;
                rear[j] = r;
            });
            for (int v = 0; v < variants.Length; v++)
            {
                for (int ring = 1; ring >= 0; ring--)
                {
                    var c = new Count();
                    var perCell = new System.Text.StringBuilder();
                    var lengths = new System.Collections.Generic.List<double>();
                    var medians = new System.Text.StringBuilder();
                    for (int cell = 0; cell < cells; cell++)
                    {
                        int past = 0;
                        var here = new double[Seeds];
                        for (int seed = 0; seed < Seeds; seed++)
                        {
                            int j = ((v * 2 + ring) * cells + cell) * Seeds + seed;
                            if (runs[j].Fields > 0) c.Shown++;
                            lengths.Add(runs[j].Duration);
                            here[seed] = runs[j].Duration;
                            if (ShakeScenario.PastTheKick(runs[j])) { c.Past++; past++; }
                            c.FrontFull += front[j] / (cells * Seeds);
                            c.RearFull += rear[j] / (cells * Seeds);
                        }
                        perCell.Append(cell > 0 ? " / " : "").Append(past);
                        Array.Sort(here);
                        medians.Append(cell > 0 ? " / " : "").Append(here[Seeds / 2].ToString("F2"));
                    }
                    lengths.Sort();
                    TestContext.WriteLine($"{variants[v],-9} {(ring == 1 ? "ringed  " : "ringless")}: shown {c.Shown}/{cells * Seeds}, past the kick {c.Past} "
                        + $"(empty 2.5 / 5.3 / 9 m/s, 2 kg the same: {perCell}), the median length {lengths[lengths.Count / 2]:F2} s (per cell {medians}); "
                        + $"a front motor at full {c.FrontFull:F3} s, a rear {c.RearFull:F3} s a flight");
                }
            }
            // The own wake's floor without a punch: the hold, a gentle step and a sharp one from the hover (ShakeScenario's "from
            // the hover"), the drone and the two wake variants, ringed.
            Variant[] floors = { Variant.Model, Variant.NoWake, Variant.Level92 };
            int m3 = floors.Length * 3 * Payloads.Length * Seeds;
            var hovers = new ShakeScenario.Result[m3];
            System.Threading.Tasks.Parallel.For(0, m3, j =>
            {
                int seed = j % Seeds, p = j / Seeds % Payloads.Length, input = j / (Seeds * Payloads.Length) % 3, v = j / (3 * Seeds * Payloads.Length);
                FlightSetup setup = Setup(floors[v], Payloads[p], (ulong)(seed + 1), true);
                hovers[j] = input == 0 ? ShakeScenario.Hold(setup) : ShakeScenario.Fly(setup, 0, input == 1);
            });
            for (int j0 = 0; j0 < m3; j0 += Seeds)
            {
                int p = j0 / Seeds % Payloads.Length, input = j0 / (Seeds * Payloads.Length) % 3, v = j0 / (3 * Seeds * Payloads.Length);
                int shown = 0, past = 0;
                double peak = 0, low = double.MaxValue, longest = 0;
                for (int j = j0; j < j0 + Seeds; j++)
                {
                    if (hovers[j].Fields > 0) shown++;
                    if (ShakeScenario.PastTheKick(hovers[j])) past++;
                    peak = Math.Max(peak, hovers[j].Peak / hovers[j].Threshold);
                    low = Math.Min(low, hovers[j].Peak / hovers[j].Threshold);
                    longest = Math.Max(longest, hovers[j].Duration);
                }
                TestContext.WriteLine($"{floors[v],-9} {Payloads[p]:F0} kg, {new[] { "the hold", "a gentle step", "a sharp step" }[input]} from the hover: shown on {shown}/{Seeds}, "
                    + $"past the kick on {past}, the longest {longest:F2} s; the window's peak over its threshold {low:F2}–{peak:F2}");
            }
            string csv = Environment.GetEnvironmentVariable("SHAKE_PROBE_CSV");
            if (string.IsNullOrEmpty(csv)) return;
            var w = new System.Text.StringBuilder("variant,ring,payload,drop,seed,fields,episodes,duration,past,peak,hover,threshold,arrested,last\n");
            void Row(string name, int ring, double payload, double drop, int seed, in ShakeScenario.Result r) =>
                w.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7:F3},{8},{9:F5},{10:F5},{11:F5},{12:F3},{13:F3}\n",
                    name, ring, payload, drop, seed, r.Fields, r.Episodes, r.Duration, ShakeScenario.PastTheKick(r) ? 1 : 0, r.Peak, r.Hover, r.Threshold, r.ArrestedAfter, r.Last));
            for (int j = 0; j < n; j++)
            {
                int seed = j % Seeds, cell = j / Seeds % cells, ring = j / (Seeds * cells) % 2, v = j / (2 * Seeds * cells);
                Row(variants[v].ToString(), ring, Payloads[cell / Drops.Length], Drops[cell % Drops.Length], seed + 1, runs[j]);
            }
            for (int j = 0; j < m3; j++)
            {
                int seed = j % Seeds, p = j / Seeds % Payloads.Length, input = j / (Seeds * Payloads.Length) % 3, v = j / (3 * Seeds * Payloads.Length);
                Row(floors[v] + "-" + new[] { "hold", "gentle", "sharp" }[input], 1, Payloads[p], 0, seed + 1, hovers[j]);
            }
            System.IO.File.WriteAllText(csv, w.ToString());
        }
    }
}
