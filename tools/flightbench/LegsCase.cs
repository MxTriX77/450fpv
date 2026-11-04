using System;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // p7g-legs 1.4's harness (design 7): the legs' outcomes over a grid of touchdowns, the free body set down with the
    // throttle cut on the stub's floor (LegScenarios, μ 0.5/0.4): the descent × the roll (right side down) × the drift (along
    // the roll, +x) on the strength seeds 1–N, each flight's outcome by its worst leg (intact, cracked, set, broken, detached)
    // and whether it went over (past 60° of tilt); the sensitivity to the clamp's preload (the wing nut's torque at its bounds)
    // and the tube's yield (σ_y at its bounds), every leg's draw held there; and the known-wrong models, each against the
    // claim it must miss. The shares are printed for QA to judge against D-079's account ("falls when landing a bit more
    // often"; a leg bends when a landing is too hard or not parallel enough): nothing is tuned to them.
    internal static class LegsCase
    {
        static readonly double[] Descents = { 0.5, 1.0, 1.5 }, Rolls = { 0, 10, 20 }, Drifts = { 0, 0.3, 0.6 };

        enum Worst { Intact, Cracked, Set, Broken, Detached }

        struct Shares
        {
            public int Flights, Tipped, Down, Visible, Even;
            public double MaxSet;
            public int[] Of;

            public static Shares New() => new Shares { Of = new int[5] };

            public int Damaged => Flights - Of[(int)Worst.Intact];

            public override string ToString()
            {
                double n = Flights;
                return $"{Of[0] / n,6:P0} {Of[1] / n,6:P0} {Of[2] / n,6:P0} {Of[3] / n,6:P0} {Of[4] / n,6:P0} | {Tipped / n,6:P0} {Down / n,6:P0} | {Visible / n,6:P0} {MaxSet,6:F2}°";
            }
        }

        static bool _ok = true;

        // The soft legs' quasi-static roll tip, deg (derived, p7g-legs 1.3: LandingStability.SoftRollTipDeg on 1.3's give, 14.40°).
        static double SoftTip;

        static void Row(string name, bool pass, string detail)
        {
            _ok &= pass;
            Console.WriteLine($"  {name}: {detail}: {(pass ? "PASS" : "FAIL")}");
        }

        static Worst Of(LegScenarios.Outcome o)
        {
            Worst w = Worst.Intact;
            for (int c = 0; c < 4; c++)
            {
                LegFlags f = o.Flags[c];
                Worst k = (f & LegFlags.Detached) != 0 ? Worst.Detached : (f & LegFlags.Broken) != 0 ? Worst.Broken
                    : (f & LegFlags.Set) != 0 ? Worst.Set : (f & LegFlags.Cracked) != 0 ? Worst.Cracked : Worst.Intact;
                if (k > w) w = k;
            }
            return w;
        }

        static Shares Cell(DroneSpec spec, MaterialRow[] m, ulong h, double descent, double roll, double drift, int seeds,
            Func<ContactSpec, ContactSpec> edit = null, bool elastic = false)
        {
            Shares s = Shares.New();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var t = new LegScenarios.Touch
                {
                    RollDeg = roll, Velocity = new Double3(drift, -descent, 0), Seed = (ulong)seed, Seconds = 3, Edit = edit, Elastic = elastic,
                };
                LegScenarios.Outcome o = LegScenarios.Run(spec, m, h, t);
                s.Flights++;
                s.Of[(int)Of(o)]++;
                if (o.MaxTilt > 60) s.Tipped++;
                // Down: at the end not standing on its legs, tilted past the soft legs' quasi-static roll tip (14.40°, 1.3: no
                // drone stands on its legs leaning more), on its frame or its side: the pilot's fall.
                if (o.EndTilt > SoftTip) s.Down++;
                // The bend a pilot sees: the largest set left on a leg that stays on, past 1° (assumed: about 4 mm at the foot).
                double bend = 0;
                for (int c = 0; c < 4; c++)
                    if ((o.Flags[c] & (LegFlags.Broken | LegFlags.Detached)) == 0) bend = Math.Max(bend, LegScenarios.SetDegrees(o.Set[c]));
                if (bend >= 1) s.Visible++;
                s.MaxSet = Math.Max(s.MaxSet, bend);
                // The even split (the known-wrong): a quarter of the legs' load along each, which buckles them past their load.
                if (o.Core.Contacts.Legs != null && o.EvenAxial > o.Core.Contacts.Legs.BucklingLoad) s.Even++;
            }
            return s;
        }

        // Every leg's draw of one strength held at a value (its bounds collapsed onto it).
        static Func<ContactSpec, ContactSpec> Hold(Func<LegDamageSpec, LegBound> get, Action<LegDamageSpec, LegBound> set, bool high) => c =>
        {
            c = c.Clone();
            LegBound b = get(c.Legs);
            double v = high ? b.Max : b.Min;
            set(c.Legs, new LegBound { Value = v, Min = v, Max = v });
            return c;
        };

        static Func<ContactSpec, ContactSpec> Linear => c =>
        {
            c = c.Clone();
            c.LegLeverLength = 0;
            return c;
        };

        public static bool Run(int seeds)
        {
            _ok = true;
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            ContactSpec legs = spec.BuildContacts(spec.Nominal, m, h);
            LegDamageSpec l = legs.Legs;
            SoftTip = LandingStability.Legs(spec, legs, spec.BuildMass(spec.Nominal).Properties)
                .SoftRollTipDeg(legs.LegTubeSideStiffness, legs.FrictionStiffnessRatio * legs.LegStiffness, legs.LegSwayLoad, out _);
            Console.WriteLine($"the legs: the lever γL {legs.LegLeverLength * 1e3:F1} mm (π²L/12 of the {l.FreeLength * 1e3:F1} mm free length), "
                + $"the tube's {legs.LegTubeSideStiffness:F0} N/m at the foot, the sway load {legs.LegSwayLoad:F2} N, the bow {ContactModel.BowOverSway * legs.LegSwayLoad:F0} N");
            Console.WriteLine($"  the clamp's wing nut {l.ClampTorque.Min:F2}–{l.ClampTorque.Max:F2} N·m, the tube's yield {l.YieldStress.Min / 1e6:F0}–{l.YieldStress.Max / 1e6:F0} MPa");
            Console.WriteLine($"the grid: the free body onto the stub's floor (μ 0.5/0.4), the throttle cut, 3 s, seeds 1–{seeds}; each flight by its worst leg, over (past 60°), "
                + $"and down at the end (past the soft legs' roll tip, {SoftTip:F2}°: not on its legs)");
            Console.WriteLine("  descent  roll  drift | intact cracked    set broken detached |   over   down | bent ≥ 1° and on, the largest");
            var grid = new Shares[Descents.Length, Rolls.Length, Drifts.Length];
            for (int a = 0; a < Descents.Length; a++)
                for (int b = 0; b < Rolls.Length; b++)
                    for (int d = 0; d < Drifts.Length; d++)
                    {
                        grid[a, b, d] = Cell(spec, m, h, Descents[a], Rolls[b], Drifts[d], seeds);
                        Console.WriteLine($"  {Descents[a],7:F1} {Rolls[b],4:F0}° {Drifts[d],6:F1} | {grid[a, b, d]}");
                    }

            // D-079's account, as the claims the grid can hold: the pilot's normal landing (level, 0.5 m/s, no drift) leaves
            // every leg intact; a harder one and a rolled one damage more, the share rising with each; and the drone falls (goes
            // over) a little, not on every touch.
            Shares normal = grid[0, 0, 0];
            Row("the normal landing (level, 0.5 m/s down, no drift)", normal.Damaged == 0 && normal.Tipped == 0 && normal.Down == 0, $"{normal}");
            bool harder = true, rolled = true;
            for (int b = 0; b < Rolls.Length; b++)
                for (int a = 1; a < Descents.Length; a++)
                    harder &= grid[a, b, 0].Damaged >= grid[a - 1, b, 0].Damaged;
            for (int a = 0; a < Descents.Length; a++)
                for (int b = 1; b < Rolls.Length; b++)
                    rolled &= grid[a, b, 0].Damaged >= grid[a, b - 1, 0].Damaged;
            Shares hard = grid[Descents.Length - 1, Rolls.Length - 1, 0];
            Row("harder and rolled, no drift: the damaged share never falls with the descent or the roll", harder && rolled && hard.Damaged > 0,
                $"{Descents[^1]} m/s rolled {Rolls[^1]}° {hard.Damaged * 100.0 / hard.Flights:F0} % damaged");
            int tipped = 0, flights = 0, damagedAll = 0, down = 0;
            foreach (Shares s in grid)
            {
                tipped += s.Tipped;
                down += s.Down;
                flights += s.Flights;
                damagedAll += s.Damaged;
            }
            Console.WriteLine($"  over the grid: {tipped * 100.0 / flights:F1} % of flights over, {down * 100.0 / flights:F1} % down (not on its legs at the end), "
                + $"{damagedAll * 100.0 / flights:F1} % with a damaged leg");

            // The drift's onset on the pilot's normal landing (level, 0.5 m/s down): the lowest drift that damages a leg on any
            // seed and on half of them, by 0.05 m/s.
            double first = double.NaN, half = double.NaN;
            for (double drift = 0.05; drift <= 1.0001 && double.IsNaN(half); drift += 0.05)
            {
                Shares s = Cell(spec, m, h, 0.5, 0, drift, seeds);
                if (double.IsNaN(first) && s.Damaged > 0) first = drift;
                if (2 * s.Damaged >= s.Flights) half = drift;
            }
            Console.WriteLine($"  the drift that damages a leg on the normal landing: on a first seed from {first:F2} m/s, on half of them from {half:F2} m/s");

            // The sensitivities: every leg's wing nut torque (the clamp's preload P = T/(K·d)) and tube yield held at each bound.
            Console.WriteLine("the sensitivity (every leg's draw held at a bound; the other strengths drawn as ever)");
            Console.WriteLine("  held                     | cell                        | intact cracked    set broken detached |   over   down | bent ≥ 1°, largest");
            var cells = new[] { (1.5, 10.0, 0.0), (1.5, 20.0, 0.0), (0.5, 0.0, 0.3), (1.0, 10.0, 0.6) };
            var holds = new (string, Func<ContactSpec, ContactSpec>)[]
            {
                ("as drawn", null),
                ("wing nut at its least", Hold(x => x.ClampTorque, (x, v) => x.ClampTorque = v, false)),
                ("wing nut at its most", Hold(x => x.ClampTorque, (x, v) => x.ClampTorque = v, true)),
                ("yield at its least", Hold(x => x.YieldStress, (x, v) => x.YieldStress = v, false)),
                ("yield at its most", Hold(x => x.YieldStress, (x, v) => x.YieldStress = v, true)),
            };
            foreach ((double descent, double roll, double drift) in cells)
                foreach ((string name, Func<ContactSpec, ContactSpec> edit) in holds)
                    Console.WriteLine($"  {name,-24} | {descent:F1} m/s, {roll,2:F0}°, {drift:F1} m/s drift | {Cell(spec, m, h, descent, roll, drift, seeds, edit)}");

            // The known-wrong models, each against the claim it must miss.
            Console.WriteLine("the known-wrong models");
            Shares elastic = Cell(spec, m, h, 1.5, 20, 0, seeds, null, true);
            Row("the elastic legs (the law off) never bend: 1.5 m/s rolled 20°", elastic.Damaged == 0 && hard.Damaged > 0, $"{elastic}, against {hard}");
            Shares level = grid[Descents.Length - 1, 0, 0];
            Row("the even split over the four legs (a quarter of the load along each) damages no more rolled than level: 1.5 m/s",
                hard.Even <= level.Even && hard.Damaged > level.Damaged, $"rolled {Rolls[^1]}° {hard.Even} of {hard.Flights}, level {level.Even}; the law {hard.Damaged} and {level.Damaged}");
            Shares linearRolled = Cell(spec, m, h, 1.0, 10, 0, seeds, Linear), leverRolled = grid[1, 1, 0];
            Row("1.3's linear softening (P·δ on an unbounded give past the sway load) damages a rolled 1.0 m/s landing the lever holds",
                linearRolled.Damaged > leverRolled.Damaged, $"{linearRolled}, the lever {leverRolled}");
            Shares rigid = Cell(spec, m, h, 0.5, 10, 0.6, seeds, LandingScenarios.RigidAcross()), lever = grid[0, 1, 2];
            Console.WriteLine($"  rigid across (D-067's mechanics, no law) at 0.5 m/s rolled 10° with 0.6 m/s of drift: {rigid}; the soft legs {lever}");
            return _ok;
        }
    }
}
