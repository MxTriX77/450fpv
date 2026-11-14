using System.Text;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.App
{
    /// <summary>
    /// The smoke test's leg flights (p7g-legs 1.5, AppFlow's -smokelegs): on the legs at the houses' start, disarmed, each
    /// on a seed of its own, started so a leg is left in a state the player draws, then shot in the FPV and chase views.
    /// The bent leg lands at Down m/s rolled RollDeg right side down and stays upright: a level landing on the range's soil
    /// leaves no visible set at model 50 (at most 0.17° on seeds 1–6, and 0.02 m/s faster parts every leg), rolled 5–10°
    /// sets the legs by degrees. The others rise from rest at Up m/s with rear right's skid overhang 2 mm under a steel bar
    /// (as LegDamageTests.ALegComesOff hooks it, a runtime object the scene does not draw) and fall back onto the ground on
    /// their back: by the seed and the speed the hooked leg breaks or slides out of its clamp, its skid half hanging from
    /// front right, and the fall breaks more. LegShotsTests flies each case as the player does.
    /// </summary>
    public static class LegShots
    {
        public sealed class Case
        {
            public string Name;
            public ulong Seed;
            public double Up, Down, RollDeg; // m/s at the start, and a landing's roll
            public double ShotS; // the flight's time at the shot, s
            public int Leg; // the corner shown (Corners order)
            public LegFlags Flags; // its leg's state holds these at the shot
            public CornerState Corner; // and its corner is drawn so
            public double SetDeg; // its set at least, °
        }

        /// <summary>The flights, found by sweeps of seeds 1–4 at model 50: landings of 1–3 m/s rolled 0–20°, rises of 0.5–5 m/s.
        /// Model 57 moved the missing leg's: seed 1 at 5 m/s now breaks front right on the fall back, so the half has nothing to
        /// hang from. A sweep of seeds 1–24 at 4–5.75 m/s keeps the pulled-out leg and its folded half to rest on seed 11 at
        /// 4.25 m/s (and seeds 15, 17, 21 and 24 at 4.75); the other rises that pull it out hold it only in the air.
        /// Model 64 (the skid's frame, the canister) moved three, each held from 2.5 s to 3.5 s: the 2 m/s rolled landing now
        /// breaks legs, and seed 5 at 1.75 m/s rolled 10° sets rear right 3.5° (front right 2.5°, the other two intact in
        /// shape); seed 11 at 4.25 breaks every leg, and seed 13 at 4.5 m/s pulls rear right out with its half folded from a
        /// bent front right; seed 2 at 4 m/s no longer breaks front left, at 3.5 m/s it does (rear and front right break too).
        /// The line's models moved two: from model 70 seed 13 at 4.5 m/s breaks every leg and rear right's half goes with its
        /// tube, and from model 79 seed 2 at 3.5 m/s leaves front left's skid gone. A sweep of seeds 1–24 at 2–6 m/s, then
        /// 0.05 m/s around the held ones, keeps seed 14's rear right pulled out with its half folded from a bent front right
        /// from 5.70 to 5.80 m/s, and seed 2's front left broken and hanging (rear and front right broken too) from 1.90 to
        /// 2.05 m/s, each held from 2.5 s to 3.5 s.
        /// Model 99 with the legs drawn (w15-player-physics 1.1) moved all four; a sweep of seeds 1–24 (landings of 0.75–2.5 m/s
        /// rolled 5–20°, rises within 1 m/s of each case's and 3–8 m/s for the missing leg), each held from 2.5 s to 3.5 s: seed 5
        /// at 1.25 m/s rolled 15° sets rear right 5.6° (front right 3.8°, the other two intact); seed 3 at 2.75 m/s breaks rear
        /// right and leaves its half hanging, the rest intact; no rise leaves a pulled-out leg's half folded any more (the hanging
        /// half bears on the ground, 2.6b), so the missing leg is seed 1 at 4.5 m/s, rear right out and its half hanging unfolded
        /// from front right (at 4.75 too), the rest intact; and seed 6 at 2 m/s leaves front left broken and hanging (rear and
        /// front right broken too).</summary>
        public static readonly Case[] Cases =
        {
            new Case { Name = "bent", Seed = 5, Down = 1.25, RollDeg = 15, ShotS = 2.5, Leg = 0, Flags = LegFlags.Set, Corner = CornerState.None, SetDeg = 3 },
            new Case { Name = "broken", Seed = 3, Up = 2.75, ShotS = 2.5, Leg = 0, Flags = LegFlags.Broken, Corner = CornerState.Stub | CornerState.Hanging },
            new Case { Name = "missing", Seed = 1, Up = 4.5, ShotS = 2.5, Leg = 0, Flags = LegFlags.Detached, Corner = CornerState.TubeGone | CornerState.Hanging },
            new Case { Name = "hanging", Seed = 6, Up = 2.0, ShotS = 2.5, Leg = 3, Flags = LegFlags.Broken, Corner = CornerState.Stub | CornerState.Hanging },
        };

        /// <summary>Whether the pose shows the case's leg in its state.</summary>
        public static bool Shows(Case c, in PoseFrame pose)
        {
            var flags = (LegFlags)(byte)(pose.LegState >> (8 * c.Leg));
            return (flags & c.Flags) == c.Flags && LegPlacement.Corner(pose, c.Leg) == c.Corner && Degrees(pose.LegSet[c.Leg]) >= c.SetDeg;
        }

        /// <summary>Each leg's state, corner and set in a pose.</summary>
        public static string Describe(in PoseFrame pose)
        {
            var b = new StringBuilder();
            for (int c = 0; c < LegDamage.Legs; c++)
                b.Append(c > 0 ? "; " : "").Append(LegDamage.Names[c]).Append(' ').Append((LegFlags)(byte)(pose.LegState >> (8 * c)))
                    .Append(" / ").Append(LegPlacement.Corner(pose, c)).Append(' ').Append(Degrees(pose.LegSet[c]).ToString("0.00")).Append('°');
            return b.ToString();
        }

        static double Degrees(Double3 set) => set.Length() * 180 / System.Math.PI;

        /// <summary>The case's start: the rolled landing, or the rise and the bar over rear right's skid.</summary>
        public static void Apply(Case c, FlightSetup s)
        {
            if (c.Down > 0)
            {
                // Rolled right side down by RollDeg about the body's forward axis, its lowest foot 2 mm over the ground.
                DetMath.SinCos(c.RollDeg * System.Math.PI / 360, out double sin, out double cos);
                s.StartAttitude = s.StartAttitude * new Quat(0, 0, -sin, cos);
                s.StartPosition = s.StartPosition + new Double3(0, 0.002 + System.Math.Abs(s.Contacts.Legs.Foot[0].X) * 2 * sin * cos, 0);
                s.StartVelocity = new Double3(0, -c.Down, 0);
                return;
            }
            Double3 foot = s.Contacts.Legs.Foot[0];
            // On the overhang between the T-joint and the skid's end, its underside 2 mm over the skid's top.
            var centre = new Double3(foot.X, foot.Y + 0.015 + 0.002 + 0.01, foot.Z - 0.06);
            Quat turn = s.StartAttitude;
            var bar = new ShapeGeometry
            {
                Kind = ShapeKind.Box, Center = s.StartPosition + turn.Rotate(centre),
                AxisX = turn.Rotate(new Double3(1, 0, 0)), AxisY = turn.Rotate(new Double3(0, 1, 0)), AxisZ = turn.Rotate(new Double3(0, 0, 1)),
                HalfExtents = new Double3(0.03, 0.01, 0.01), Material = s.World.Query.MaterialId("steel"),
            };
            s.RuntimeObjects = new[] { new RuntimeObject("bar", new[] { bar }) };
            s.StartVelocity = new Double3(0, c.Up, 0);
        }
    }
}
