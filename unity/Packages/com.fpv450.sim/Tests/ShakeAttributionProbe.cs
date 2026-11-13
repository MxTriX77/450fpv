using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// What carries the ringless shake past the kick (PHY-10, the physics ledger's gap 10; p7b-airframe design 3f): the six
    /// full punches of --shake's known-wrong on seeds 1–40, the drone and its ringless twin, each with one source changed: the
    /// motors' spreads off (D-064's, D-070's second cause), the coil's canister and pack moved fore-aft until the centre of
    /// mass sits over the hubs' centre (p7d-tether 1.6's probe: the front motors' full duty in a hard arrest), and the
    /// rotors' own wake on (design 3f's law, and model 90's; ShakeScenario.Setup flies none). Per variant: the shake shown,
    /// past the kick, its median length, and the time in the window a front and a rear motor sit at full command; then,
    /// with the own wake on, a gentle step from the hover (no punch, no descent). A printed table, never a verdict.
    /// </summary>
    public class ShakeAttributionProbe
    {
        const int Seeds = 40;
        static readonly double[] Payloads = { 0, 2 }, Drops = { 0.3, 0.6, 1.0 };

        enum Variant { Model, NoSpreads, Centred, OwnWake, OwnWake90 }

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
            FlightSetup s = ShakeScenario.Setup(spec, payload, seed);
            if (!ring) s.VortexRing = null;
            if (v == Variant.NoSpreads) s.Asymmetry = null;
            if (v == Variant.OwnWake || v == Variant.OwnWake90) s.RotorWake = spec.BuildRotorWake();
            return s;
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
                flight.Propulsion.HoverWakeLaw = variants[v] == Variant.OwnWake90;
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
                    for (int cell = 0; cell < cells; cell++)
                    {
                        int past = 0;
                        for (int seed = 0; seed < Seeds; seed++)
                        {
                            int j = ((v * 2 + ring) * cells + cell) * Seeds + seed;
                            if (runs[j].Fields > 0) c.Shown++;
                            lengths.Add(runs[j].Duration);
                            if (ShakeScenario.PastTheKick(runs[j])) { c.Past++; past++; }
                            c.FrontFull += front[j] / (cells * Seeds);
                            c.RearFull += rear[j] / (cells * Seeds);
                        }
                        perCell.Append(cell > 0 ? " / " : "").Append(past);
                    }
                    lengths.Sort();
                    TestContext.WriteLine($"{variants[v],-9} {(ring == 1 ? "ringed  " : "ringless")}: shown {c.Shown}/{cells * Seeds}, past the kick {c.Past} "
                        + $"(empty 2.5 / 5.3 / 9 m/s, 2 kg the same: {perCell}), the median length {lengths[lengths.Count / 2]:F2} s; "
                        + $"a front motor at full {c.FrontFull:F3} s, a rear {c.RearFull:F3} s a flight");
                }
            }
            // The own wake's floor without a punch: a gentle step from the hover (ShakeScenario's "from the hover", gentle).
            foreach (Variant v in new[] { Variant.Model, Variant.OwnWake, Variant.OwnWake90 })
                foreach (double payload in Payloads)
                {
                    int shown = 0;
                    double peak = 0;
                    for (int seed = 1; seed <= 10; seed++)
                    {
                        FlightSetup setup = Setup(v, payload, (ulong)seed, true);
                        var flight = new FlightCore(setup);
                        flight.Propulsion.HoverWakeLaw = v == Variant.OwnWake90;
                        ShakeScenario.Result g = ShakeScenario.Fly(flight, setup, 0, true);
                        if (g.Fields > 0) shown++;
                        peak = Math.Max(peak, g.Peak);
                    }
                    TestContext.WriteLine($"{v,-9} {payload:F0} kg, a gentle step from the hover: shown on {shown}/10, the largest field {peak:F3}° (visible {ShakeScenario.Visible:F3}°)");
                }
        }
    }
}
