using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// fiber-tether's 1.7 scenarios (p7d-tether design 6 and 12: the fiber's contacts). The flights fly the repository's drone
    /// legs-on under SimPilot with its line (TetherScenarios' setup); the wedge, the bend and the synthetic elements step a line
    /// of their own through FiberContacts as FiberLine does, its ends anchors (one moved as the drone's pull). Without NUnit, so
    /// the tools can link it; FiberContactTests asserts them.
    /// </summary>
    public static class FiberContactScenarios
    {
        const double Dt = FlightCore.Dt, Pi = 3.141592653589793;

        static TetherTable Table => TetherScenarios.Table();
        static double LineRadius => Table.Number("fiber.coating_diameter_m") / 2;

        // ---- "The bend is the elastica's" ----

        public struct Bend
        {
            /// <summary>The line's tension, turn (rad) and the arris (m) the case sets.</summary>
            public double Tension, Turn, Edge;
            /// <summary>FiberContact's logged turn (A), tension (B) and bend radius (C), and the bend's stress after the step.</summary>
            public double LoggedTurn, LoggedTension, LoggedRadius, Stress;
            public bool Logged;
        }

        /// <summary>A line of 1 m either side of one node pressed on a snag member of arris edge, turning by degrees under tension
        /// (its rest lengths so stretched, its ends anchored), stepped once and read through FiberContacts.Finish.</summary>
        public static Bend BendOver(double tension, double degrees, double edge)
        {
            TetherTable t = Table;
            var contacts = new FiberContacts(t, new FlightSetup { Tether = t }, FlightWorld.Floor(), null, LineRadius,
                t.Number("fiber.mass_per_m_kg"), 100, 1, Dt);
            double ea = t.Number("fiber.axial_stiffness_n"), theta = degrees >= 180 ? Pi - 1e-6 : degrees * Pi / 180;
            DetMath.SinCos(theta, out double sin, out double cos);
            Double3 dIn = new Double3(1, 0, 0), dOut = new Double3(cos, -sin, 0), at = new Double3(0, 10, 0);
            double rest = 1 / (1 + tension / ea);
            var chain = new CableChain(3, t.Number("fiber.mass_per_m_kg"), ea) { Gravity = default };
            chain.Reset(new[] { at - dIn, at, at + dOut }, new[] { 0, rest, 2 * rest }, 3);
            foreach (int end in new[] { 0, 2 })
            {
                chain[end].AnchorStiffness = 1e6;
                chain[end].AnchorTarget = chain[end].Position;
            }
            Double3 n = dIn - dOut;
            n = n * (1 / n.Length());
            CableContact c = CableContact.On(at, n, default, 1e8, 0, 1e8, 0.35, 0.28, true); // a stiff test member: the case's tension holds
            c.MemberKind = (byte)FiberMember.Snag;
            c.Member = 1;
            c.Edge = edge;
            c.Fresh = true;
            c.Kinematic = true;
            chain[1].Contact0 = c;
            chain.Step(Dt);
            var events = new StepEvents();
            contacts.Finish(chain, new FlightState { Attitude = Quat.Identity }, events);
            var r = new Bend { Tension = tension, Turn = theta, Edge = edge, Stress = contacts.MinBendStress };
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != EventKind.FiberContact) continue;
                r.Logged = true;
                r.LoggedTurn = events[i].A;
                r.LoggedTension = events[i].B;
                r.LoggedRadius = events[i].C;
            }
            return r;
        }

        // ---- "A segment far from everything is not queried" ----

        public struct Certificate
        {
            /// <summary>The least steps between two queries of one segment (named by its first node's material coordinate) while
            /// trailing at 20 m/s, the segments queried and the queries over the window.</summary>
            public int LeastGap, Segments;
            public long Queries, Steps;
            /// <summary>Segments whose certificate had run out (an end past the margin less the line's radius from its core, or
            /// none) and that the next step did not query: must be 0.</summary>
            public int MissedExpiries;
            /// <summary>The line's height at the drone's end and its lowest node in the window, m.</summary>
            public double Height, Lowest;
        }

        /// <summary>Climbs to 50 m at 5 m/s from the stub's floor, then flies along +z at 20 m/s; over the last 4 s of 8 every
        /// segment's queries are counted, and each step checks every segment whose certificate ran out was queried in the next.</summary>
        public static Certificate HighCruise(DroneSpec spec)
        {
            FlightSetup setup = TetherScenarios.Setup(spec);
            double stand = TetherScenarios.Stand(setup), allow = Table.Number("contact.clearance_margin_m") - LineRadius;
            var r = new Certificate { LeastGap = int.MaxValue, Lowest = double.PositiveInfinity };
            var last = new Dictionary<(double, double), long>();
            var expired = new List<(double, double)>();
            const double climb = 10, cruise = 8, window = 4;
            TetherScenarios.Fly(setup, climb + cruise, (t, core, pilot) =>
            {
                if (t < climb)
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, stand + DetMath.Min(50, 5 * t), 0);
                    return;
                }
                pilot.HoldVelocity = true;
                pilot.TargetVelocity = new Double3(0, 0, 20);
                pilot.Target = new Double3(0, stand + 50, core.State.Position.Z);
            }, core =>
            {
                FiberContacts f = core.Tether.Contacts;
                CableChain line = core.Tether.Chain;
                long step = core.State.Step;
                if (step <= (climb + cruise - window) * 1000) return;
                r.Steps++;
                r.Queries += f.QueriedCount;
                // Every segment the last step found expired is among this step's queries.
                foreach ((double, double) key in expired)
                {
                    bool asked = false;
                    for (int k = 0; k < f.QueriedCount; k++) asked |= (f.QueriedMaterial(k), f.QueriedNext(k)) == key;
                    if (!asked) r.MissedExpiries++;
                }
                for (int k = 0; k < f.QueriedCount; k++)
                {
                    (double, double) key = (f.QueriedMaterial(k), f.QueriedNext(k));
                    if (last.TryGetValue(key, out long before) && (int)(step - before) < r.LeastGap) r.LeastGap = (int)(step - before);
                    last[key] = step;
                }
                expired.Clear();
                for (int j = 0; j + 1 < line.Count; j++)
                {
                    ref CableNode a = ref line[j];
                    bool clear = a.Cleared && FiberContacts.PointSegment(a.Position, a.ClearA, a.ClearB) < allow * allow
                        && FiberContacts.PointSegment(line[j + 1].Position, a.ClearA, a.ClearB) < allow * allow;
                    if (!clear) expired.Add((a.Material, j + 2 == line.Count ? double.PositiveInfinity : line[j + 1].Material));
                    if (a.Position.Y < r.Lowest) r.Lowest = a.Position.Y;
                }
                r.Height = core.State.Position.Y;
            });
            r.Segments = last.Count;
            return r;
        }

        public struct Pole
        {
            /// <summary>The first step a segment's core came within the pole's and the line's radii (and the chain's 2 mm band) of
            /// the pole's core, and the step its first FiberContact on the pole was logged (−1 for none).</summary>
            public long Reached, Met;
            /// <summary>Whether a slot on the pole pressed at the end of the step it was reached; the deepest any segment went into
            /// the pole, m; the steps a segment lay within the margin of it and those that queried the world (each, as it should);
            /// and the pole's surface distance from the segment it was placed beside, m.</summary>
            public bool PressingWhenReached;
            public double Deepest, PlacedAt;
            public long NearSteps, NearQueried;
        }

        /// <summary>Hovering 3 m up on the stub's floor with the line hanging back to the ground: at 4 s a steel pole (r 50 mm,
        /// 2.5 m high, below the skids) is placed with its surface 0.4 m beside the segment crossing z = −1 m (the hover settled);
        /// the drone then drifts toward it at 0.5 m/s.</summary>
        public static Pole PoleBeside(DroneSpec spec)
        {
            FlightSetup setup = TetherScenarios.Setup(spec);
            double stand = TetherScenarios.Stand(setup), radius = 0.05, r = LineRadius;
            var p = new Pole { Reached = -1, Met = -1 };
            int pole = -1;
            Double3 a0 = default, b0 = default;
            const int place = 4000;
            TetherScenarios.Fly(setup, 9, (t, core, pilot) =>
            {
                pilot.HoldVelocity = false;
                double x = t < 4.2 ? 0 : 0.5 * (t - 4.2);
                pilot.Target = new Double3(x, stand + DetMath.Min(3, t), 0);
            }, core =>
            {
                CableChain line = core.Tether.Chain;
                if (core.State.Step == place)
                {
                    int best = 0;
                    for (int j = 0; j + 1 < line.Count; j++)
                        if (Math.Abs((line[j].Position.Z + line[j + 1].Position.Z) / 2 + 1) < Math.Abs((line[best].Position.Z + line[best + 1].Position.Z) / 2 + 1)) best = j;
                    Double3 at = (line[best].Position + line[best + 1].Position) * 0.5;
                    a0 = new Double3(at.X + 0.4 + radius + r, radius, at.Z);
                    b0 = new Double3(a0.X, 2.5 - radius, a0.Z);
                    pole = core.World.Query.AddObject("test pole", new[]
                    {
                        new ShapeGeometry
                        {
                            Kind = ShapeKind.Capsule, Center = (a0 + b0) * 0.5, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                            AxisZ = new Double3(0, 0, 1), Radius = radius, HalfLength = (b0 - a0).Length() / 2,
                            Material = core.World.Query.MaterialId("steel"),
                        },
                    });
                    p.PlacedAt = Math.Sqrt(FiberContacts.SegmentSegment(line[best].Position, line[best + 1].Position, a0, b0, out _, out _)) - radius - r;
                    // The world changed under the flight (a test's edit; a map is static): no certificate issued before stands.
                    for (int j = 0; j < line.Count; j++) line[j].Cleared = false;
                }
                if (pole < 0) return;
                for (int i = 0; i < core.Events.Count; i++)
                    if (core.Events[i].Kind == EventKind.FiberContact && core.Events[i].Object == pole && p.Met < 0) p.Met = core.State.Step;
                bool near = false;
                for (int j = 0; j + 1 < line.Count; j++)
                {
                    double d = Math.Sqrt(FiberContacts.SegmentSegment(line[j].Position, line[j + 1].Position, a0, b0, out _, out _)), reach = radius + r;
                    // Reached: within the chain's speculative band of the surface (a member that close holds the node off by at
                    // most that, CableChain.KinematicBand).
                    if (d < reach + CableChain.KinematicBand && p.Reached < 0)
                    {
                        p.Reached = core.State.Step;
                        for (int i = 0; i < line.Count; i++)
                            foreach (CableContact c in new[] { line[i].Contact0, line[i].Contact1 })
                                p.PressingWhenReached |= c.Active && c.NormalForce > 0 && (int)(c.Member >> 32) == pole;
                    }
                    if (reach - d > p.Deepest) p.Deepest = reach - d;
                    near |= d - reach < 0.45;
                }
                if (near && core.State.Step > place)
                {
                    p.NearSteps++;
                    if (core.Tether.Contacts.QueriedCount > 0) p.NearQueried++;
                }
            });
            return p;
        }

        // ---- "The wedge of a fork" ----

        public struct Wedge
        {
            /// <summary>The first step a node pressed on both limbs at once, and the steps that did.</summary>
            public long FirstBoth, BothSteps;
            /// <summary>The tension at the line's held end when the pull stopped, the most it reached while held still after, and the
            /// most over the pull, N.</summary>
            public double AtStop, MostAfterStop, Most;
            /// <summary>The bend at the crotch: the least R_eff, m, and the most glass stress there, Pa.</summary>
            public double LeastRadius, MostStress;
            public long Wedges, SafeSteps;
            public bool Finite;
        }

        /// <summary>
        /// Two bark limbs (r 25 mm) from a crotch 1.5 m up, ±30° from the vertical in the plane z = 0, on the stub's floor; a line
        /// of 4 m along z across the fork's opening 0.7 m above the crotch, both ends anchored and drawn down at 0.5 m/s for 2.4 s
        /// (the drone's pull), then held still for 1 s.
        /// </summary>
        public static Wedge ForkWedge()
        {
            TetherTable t = Table;
            var world = new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber", "bark"));
            ushort bark = world.Query.MaterialId("bark");
            Double3 crotch = new Double3(0, 1.5, 0);
            DetMath.SinCos(Pi / 6, out double s30, out double c30);
            var limbs = new ShapeGeometry[2];
            for (int k = 0; k < 2; k++)
            {
                Double3 dir = new Double3(k == 0 ? -s30 : s30, c30, 0);
                limbs[k] = new ShapeGeometry
                {
                    Kind = ShapeKind.Capsule, Center = crotch + dir * 0.6, AxisY = dir, AxisX = new Double3(c30, k == 0 ? s30 : -s30, 0),
                    AxisZ = new Double3(0, 0, 1), Radius = 0.025, HalfLength = 0.6, Material = bark,
                };
                limbs[k].AxisX = Double3.Cross(limbs[k].AxisY, limbs[k].AxisZ);
            }
            world.Query.ResetRuntimeObjects();
            int fork = world.Query.AddObject("fork", limbs);
            var r = new Wedge { FirstBoth = -1, LeastRadius = double.PositiveInfinity, Finite = true };
            const int nodes = 21;
            var x = new Double3[nodes];
            var s = new double[nodes];
            double mu = t.Number("fiber.mass_per_m_kg");
            for (int i = 0; i < nodes; i++)
            {
                x[i] = new Double3(0, 2.2, -2 + 4.0 * i / (nodes - 1));
                s[i] = 4.0 * i / (nodes - 1);
            }
            var chain = new CableChain(32, mu, t.Number("fiber.axial_stiffness_n")) { Gravity = FlightCore.Gravity };
            chain.Reset(x, s, nodes);
            var contacts = new FiberContacts(t, new FlightSetup { Tether = t, World = world }, world, null, LineRadius, mu, 100, 1, Dt);
            var events = new StepEvents(256);
            var ground = new GroundSample[32];
            var at = new XZ[32];
            var heights = new double[32];
            var normals = new Double3[32];
            Double3 left = x[0], right = x[nodes - 1], down = new Double3(0, -0.5, 0);
            const int pull = 2400, hold = 1000;
            for (int k = 0; k < pull + hold; k++)
            {
                events.Clear();
                Double3 drop = k < pull ? down * ((k + 1) * Dt) : down * (pull * Dt);
                Double3 v = k < pull ? down : default;
                int n = chain.Count;
                foreach (int end in new[] { 0, n - 1 })
                {
                    ref CableNode e = ref chain[end];
                    e.AnchorStiffness = 1e4;
                    e.AnchorDamping = 2 * Math.Sqrt(1e4 * e.Mass);
                    e.AnchorFollows = true;
                    e.AnchorTarget = (end == 0 ? left : right) + drop;
                    e.AnchorVelocity = v;
                }
                var state = new FlightState { Step = k, Position = crotch + new Double3(0, 0.5, 0), Attitude = Quat.Identity };
                contacts.Prepare(chain, state, events);
                n = chain.Count;
                for (int i = 0; i < n; i++) at[i] = new XZ(chain[i].Position.X, chain[i].Position.Z);
                world.SampleGround(new ReadOnlySpan<XZ>(at, 0, n), new Span<GroundSample>(ground, 0, n));
                for (int i = 0; i < n; i++)
                {
                    heights[i] = ground[i].Height;
                    normals[i] = ground[i].Normal;
                    chain[i].Drag = new Sym3(1e-5, 1e-5, 1e-5, 0, 0, 0); // still air's drag, a stand-in (labelled): it only settles the line
                    chain[i].Air = default;
                    chain[i].Force = default;
                }
                contacts.Assign(chain, ground, heights, normals, state);
                chain.Step(Dt);
                contacts.Finish(chain, state, events);
                n = chain.Count;
                double tension = chain.Tension(n - 2);
                if (k < pull) r.Most = Math.Max(r.Most, tension);
                if (k == pull - 1) r.AtStop = tension;
                if (k >= pull) r.MostAfterStop = Math.Max(r.MostAfterStop, tension);
                for (int i = 0; i < n; i++)
                {
                    ref CableNode a = ref chain[i];
                    r.Finite &= !double.IsNaN(a.Position.X + a.Position.Y + a.Position.Z + a.Velocity.X + a.Velocity.Y + a.Velocity.Z);
                    bool both = a.Contact0.Active && a.Contact1.Active && a.Contact0.NormalForce > 0 && a.Contact1.NormalForce > 0
                        && (int)(a.Contact0.Member >> 32) == fork && (int)(a.Contact1.Member >> 32) == fork && a.Contact0.Member != a.Contact1.Member;
                    if (!both) continue;
                    r.BothSteps++;
                    if (r.FirstBoth < 0) r.FirstBoth = k;
                }
                if (contacts.MinBendRadius < r.LeastRadius) r.LeastRadius = contacts.MinBendRadius;
                if (contacts.MinBendStress > r.MostStress) r.MostStress = contacts.MinBendStress;
            }
            r.Wedges = chain.Wedges;
            r.SafeSteps = chain.SafeSteps;
            return r;
        }

        // ---- "A line round a skid tethers the drone" ----

        /// <summary>A probe called after each step of SkidWrap past the re-lay (forensics; null in the suite).</summary>
        public static Action<FlightCore, int> SkidEach;

        public struct Skid
        {
            /// <summary>The skid's contact: the most reaction it took, N, its mean direction over the load (world), the steps it
            /// carried a load, and whether line slid round it (a FiberSlip on the skid) and when, s.</summary>
            public double MostReaction, SlipAt;
            public Double3 MeanReaction;
            public long Loaded;
            public bool Slipped;
            /// <summary>The line's most tension beside the skid, N, and its most bend stress there, Pa; the drone's most pitch and roll
            /// toward the skid (rad, + toward the rear right), and the line paid out, m.</summary>
            public double MostTension, MostStress, PitchToward, RollToward, PaidOut;
            public long Wedges;
            public bool Crashed;
            /// <summary>The mean reaction's part toward the anchor from the skid (world), N.</summary>
            public double TowardAnchor;
        }

        /// <summary>
        /// The drone armed on the stub's floor, climbing to 0.5 m and hovering there; at 3 s its line is laid again (the scenario's
        /// premise) from the anchor 3 m behind on the ground, along the ground to beneath its rear right skid, once round the skid
        /// (8 nodes, from below, outboard, over, inboard and below again) and up to the exit, at its rest length; the far end is the
        /// trail's (held fast). The drone then climbs at 1 m/s for 3 s.
        /// </summary>
        public static Skid SkidWrap(DroneSpec spec)
        {
            FlightSetup setup = TetherScenarios.Setup(spec);
            double stand = TetherScenarios.Stand(setup), r = LineRadius;
            const int relay = 3000;
            var res = new Skid { SlipAt = double.NaN };
            Double3 sum = default;
            int skid = -1;
            double paid0 = 0;
            Double3 toAnchor = default;
            const int climbing = relay + 100; // the re-lay's own settling (its first 0.1 s) is not the climb's
            TetherScenarios.Fly(setup, 6, (t, core, pilot) =>
            {
                pilot.HoldVelocity = false;
                pilot.Target = new Double3(0, stand + (t < 3 ? DetMath.Min(0.5, 0.5 * t) : 0.5 + (t - 3)), 0);
            }, core =>
            {
                long step = core.State.Step;
                if (step == relay)
                {
                    ContactModel model = core.Contacts;
                    for (int i = 0; i < model.ShapeCount; i++)
                        if (model.Shape(i).Name == "skid_rr") skid = i;
                    ContactShape k = model.Shape(skid);
                    Quat q = core.State.Attitude;
                    Double3 o = core.State.Position, axis = q.Rotate(new Double3(0, 0, 1)), side = q.Rotate(new Double3(1, 0, 0)), up = q.Rotate(new Double3(0, 1, 0));
                    Double3 centre = o + q.Rotate(new Double3(k.A.X, k.A.Y, -0.11));
                    double wrap = k.Radius + r;
                    var path = new List<Double3> { core.Tether.Trail[0], new Double3(centre.X, r, centre.Z - 1.0), centre - up * (wrap + 0.08) - axis * 0.2 };
                    for (int j = 0; j <= 8; j++)
                    {
                        DetMath.SinCos(-Pi / 2 + 2 * Pi * j / 8, out double sn, out double cs);
                        path.Add(centre + side * (wrap * cs) + up * (wrap * sn) + axis * (0.001 * j));
                    }
                    path.Add(o + q.Rotate(core.Tether.ExitBody));
                    var x = path.ToArray();
                    var s = new double[x.Length];
                    for (int i = 1; i < x.Length; i++) s[i] = s[i - 1] + (x[i] - x[i - 1]).Length();
                    CableChain line = core.Tether.Chain;
                    line.Reset(x, s, x.Length);
                    ref CableNode a0 = ref line[0];
                    a0.AnchorStiffness = 1e4;
                    a0.AnchorDamping = 2 * Math.Sqrt(1e4 * a0.Mass);
                    a0.AnchorTarget = a0.Position;
                    ref CableNode e = ref line[x.Length - 1];
                    e.AnchorStiffness = 1e4;
                    e.AnchorFollows = true;
                    e.AnchorTarget = e.Position;
                    paid0 = core.Tether.PaidOut;
                    toAnchor = core.Tether.Trail[0] - centre;
                    toAnchor = toAnchor * (1 / toAnchor.Length());
                }
                if (step <= climbing) return;
                SkidEach?.Invoke(core, (int)step);
                FiberContacts f = core.Tether.Contacts;
                for (int i = 0; i < f.ReactionCount; i++)
                {
                    Double3 rf = f.ReactionForce(i);
                    res.MostReaction = Math.Max(res.MostReaction, rf.Length());
                    sum = sum + rf;
                }
                if (f.ReactionCount > 0) res.Loaded++;
                for (int i = 0; i < core.Events.Count; i++)
                    if (core.Events[i].Kind == EventKind.FiberSlip && core.Events[i].Index == (byte)FiberMember.Part && core.Events[i].Shape == skid && !res.Slipped)
                    {
                        res.Slipped = true;
                        res.SlipAt = step * Dt;
                    }
                CableChain c = core.Tether.Chain;
                for (int j = 0; j + 1 < c.Count; j++) res.MostTension = Math.Max(res.MostTension, c.Tension(j));
                if (f.MinBendMember == FiberMember.Part) res.MostStress = Math.Max(res.MostStress, f.MinBendStress);
                // Pitch toward the rear (nose up) and roll toward the right (right side down): the body's up axis leaning toward
                // the rear right skid.
                Double3 bodyUp = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                res.PitchToward = Math.Max(res.PitchToward, -bodyUp.Z);
                res.RollToward = Math.Max(res.RollToward, bodyUp.X);
                res.PaidOut = core.Tether.PaidOut - paid0;
                res.Wedges = c.Wedges;
                res.Crashed |= core.State.Crashed;
            });
            res.MeanReaction = res.Loaded > 0 ? sum * (1.0 / res.Loaded) : default;
            res.TowardAnchor = Double3.Dot(res.MeanReaction, toAnchor);
            return res;
        }

        // ---- "The line lies on what is under it" ----

        public struct Lying
        {
            public string Spot;
            /// <summary>The trail's points and the chain's nodes from the trail's end to the touchdown, each over or beside a surface:
            /// how many were checked, the worst distance above a surface (m), the deepest inside any shape (m) and whether any point
            /// supported by nothing (floating).</summary>
            public int Points, Floating;
            public double WorstAbove, DeepestInside;
            /// <summary>The supports met: the plane, a straw layer, a soil shape (a floor or a wall), another shape.</summary>
            public int OnPlane, OnStraw, OnSoil, OnOther;
            public double PaidOut;
            public long Wedges;
        }

        /// <summary>The flight of "The line lies on what is under it" on the test range at spot ("plane", "straw" or "run"): the
        /// line laid from an anchor 30 m off, the drone 1 m over the spot for 60 s.</summary>
        /// <summary>A probe called after each step of LieOn (forensics; null in the suite).</summary>
        public static Action<FlightCore> LieOnEach;

        public static Lying LieOn(DroneSpec spec, string spot, double seconds = 60, Action<FlightSetup> edit = null)
        {
            MapWorld map = RangeScenarios.World;
            var world = new FlightWorld(map);
            FlightSetup setup = TetherScenarios.Setup(spec);
            setup.World = world;
            edit?.Invoke(setup);
            double stand = TetherScenarios.Stand(setup), r = LineRadius;
            Double3 foot, anchor;
            var layout = new List<Double3>();
            if (spot == "plane")
            {
                foot = new Double3(-178, 0, -40);
                anchor = new Double3(-178, 0, -70);
            }
            else if (spot == "straw")
            {
                foot = RangeScenarios.Straw;
                anchor = foot + new Double3(-30, 0, 0);
            }
            else
            {
                // E01's run: the anchor 30 m south across the run's heading (107°), the line over the trench's south wall top.
                foot = RangeScenarios.Run;
                DetMath.SinCos(RangeScenarios.RunHeading * Pi / 180, out double sh, out double ch);
                Double3 across = new Double3(ch, 0, -sh); // across the run, toward the south (its heading turned −90°)
                double edge = 0;
                var ray = new Ray[1];
                var hit = new RayHit[1];
                for (double d = 0; d < 1.5; d += 0.005)
                {
                    Double3 p = foot + across * d;
                    ray[0] = new Ray(new Double3(p.X, 3, p.Z), new Double3(0, -1, 0));
                    map.Raycast(ray, 10, hit);
                    if (hit[0].Point.Y > -0.3)
                    {
                        edge = d;
                        break;
                    }
                }
                anchor = foot + across * 30;
                for (double d = 30; d > edge + 0.05; d -= 1) layout.Add(foot + across * d);
                layout.Add(foot + across * (edge + 0.03));
            }
            if (layout.Count == 0)
            {
                Double3 dir = foot - anchor;
                double len = dir.Length();
                for (double d = 0; d < len - 0.5; d += 1) layout.Add(anchor + dir * (d / len));
            }
            setup.StartPosition = new Double3(foot.X, foot.Y + stand, foot.Z);
            setup.TetherLayout = layout.ToArray();
            Double3 hover = new Double3(foot.X, foot.Y + stand + 1, foot.Z);
            FlightCore core = TetherScenarios.Fly(setup, seconds, (t, c, pilot) =>
            {
                pilot.HoldVelocity = false;
                pilot.Target = new Double3(hover.X, setup.StartPosition.Y + DetMath.Min(1, t), hover.Z);
            }, LieOnEach);

            var res = new Lying { Spot = spot, PaidOut = core.Tether.PaidOut, Wedges = core.Tether.Chain.Wedges };
            FiberLine f = core.Tether;
            for (int i = 0; i < f.TrailCount; i++) Check(map, f.Trail[i], r, ref res);
            CableChain line = f.Chain;
            for (int i = 1; i < line.Count; i++)
            {
                ref CableNode a = ref line[i];
                bool held = a.Contact0.Active && a.Contact0.NormalForce > 0 || a.Contact1.Active && a.Contact1.NormalForce > 0;
                if (!held) break; // the touchdown: the line beyond rises to the drone
                Check(map, a.Position, r, ref res);
            }
            return res;
        }

        // A point of the line lying: within 2 mm above the plane (not over a hole) or of a shape it rests on, never inside one.
        static void Check(MapWorld map, Double3 p, double r, ref Lying res)
        {
            res.Points++;
            var at = new[] { new XZ(p.X, p.Z) };
            var g = new GroundSample[1];
            map.SampleGround(at, g);
            double best = double.PositiveInfinity;
            int kind = -1;
            if ((g[0].Flags & GroundFlags.Hole) == 0)
            {
                best = p.Y - g[0].Height - r;
                kind = 0;
            }
            var hits = new StaticContact[64];
            var cap = new Capsule(p, p, r);
            int n = Math.Min(map.StaticContacts(cap, cap, 0.01, hits, QueryFilter.NoWires), hits.Length);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].Object == WorldIds.Ground) continue;
                string material = map.MaterialName(hits[i].Material);
                if (material == "twig" || material == "foliage" || material == "razor_wire") continue;
                if (-hits[i].Distance > res.DeepestInside) res.DeepestInside = -hits[i].Distance;
                if (Math.Abs(hits[i].Distance) < Math.Abs(best))
                {
                    best = hits[i].Distance;
                    kind = hits[i].Surface != WorldIds.NoSurface ? 2 : material == "straw" ? 1 : 3;
                }
            }
            if (!(Math.Abs(best) <= 0.002))
            {
                res.Floating++;
                return;
            }
            if (best > res.WorstAbove) res.WorstAbove = best;
            if (kind == 0) res.OnPlane++;
            else if (kind == 1) res.OnStraw++;
            else if (kind == 2) res.OnSoil++;
            else res.OnOther++;
        }

        // ---- "Compliant shells are skipped" ----

        public struct Shells
        {
            /// <summary>FiberContact events on V01_L: on its limbs (bark shapes), on its snag records (the crown's twigs and small
            /// limbs on this line), and on its twig shells (must be 0); and slot-steps on a twig shell (must be 0).</summary>
            public int Limbs, Records, ShellEvents;
            public long ShellSlots, Steps;
            public long Wedges;
        }

        /// <summary>V01_L on the test range: the line laid on the ground from an anchor 10 m west of the trunk (1 m south of it) to
        /// the drone 10 m east; the drone climbs to 12 m (14 m, over the crown's top, before model 73) and flies west over the tree to 8 m past the trunk
        /// at 3 m/s, back east and west again, so the line from its anchor is drawn up through and over the crown.</summary>
        public static Shells Crown(DroneSpec spec, double seconds = 30)
        {
            MapWorld map = RangeScenarios.World;
            int tree = RangeScenarios.Placed("V01_L");
            map.Geometry(tree, 0, out ShapeGeometry trunk);
            Double3 centre = new Double3(trunk.Center.X, 0, trunk.Center.Z);
            FlightSetup setup = TetherScenarios.Setup(spec);
            setup.World = new FlightWorld(map);
            double stand = TetherScenarios.Stand(setup);
            setup.StartPosition = new Double3(centre.X + 10, stand, centre.Z - 1);
            var layout = new List<Double3>();
            for (double d = -10; d < 9.5; d += 1) layout.Add(new Double3(centre.X + d, 0, centre.Z - 1));
            setup.TetherLayout = layout.ToArray();
            var res = new Shells();
            TetherScenarios.Fly(setup, seconds, (t, core, pilot) =>
            {
                pilot.HoldVelocity = false;
                // Since model 73 at 12 m (14 m until then): at 13 and 14 m the hanging line only grazed the crown (0–8 limb contacts
                // in 30 s and no record over airmode on and off, the motors' heat on and off: 0 without airmode with the heat), at
                // 12 m it meets it in every one (10–39 limbs, 7–58 records).
                double y = stand + DetMath.Min(12, 2 * t), u = t < 7 ? 0 : 3 * (t - 7), x;
                // From 10 m east to 8 m west (18 m), then east to 8 m and west to 8 m again (16 m legs).
                double w = (u - 18) % 32;
                x = u < 18 ? 10 - u : w < 16 ? -8 + w : 8 - (w - 16);
                pilot.Target = new Double3(centre.X + x, y, centre.Z);
            }, core =>
            {
                res.Steps++;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind != EventKind.FiberContact || e.Object != tree) continue;
                    if (e.Index == (byte)FiberMember.Snag) res.Records++;
                    else if (map.Geometry(tree, e.Shape, out ShapeGeometry g) && map.MaterialName(g.Material) == "twig") res.ShellEvents++;
                    else res.Limbs++;
                }
                CableChain line = core.Tether.Chain;
                for (int i = 0; i < line.Count; i++)
                    foreach (CableContact c in new[] { line[i].Contact0, line[i].Contact1 })
                    {
                        if (!c.Active || c.MemberKind != (byte)FiberMember.Shape || (int)(c.Member >> 32) != tree) continue;
                        if (map.Geometry(tree, (int)(uint)c.Member, out ShapeGeometry g) && map.MaterialName(g.Material) == "twig") res.ShellSlots++;
                    }
            });
            return res;
        }

        public struct Elements
        {
            /// <summary>FiberContact events naming a synthetic twig element (its instance and index), the distinct elements met, and
            /// the steps a slot held one.</summary>
            public int Events, Distinct;
            public long HeldSteps, Wedges;
            public bool NamedRight;
        }

        /// <summary>A row of 12 standing synthetic twigs (5 mm, 1 m tall, 0.25 m apart along x, at z 0.5) on the stub's floor, the
        /// elements of instance 7 (until p7a's crowns carry theirs, 1.7's synthetic elements); a 4 m line along x 0.6 m up, its
        /// ends anchored and drawn along +z at 1 m/s for 2 s through the row.</summary>
        public static Elements TwigRow()
        {
            TetherTable t = Table;
            var twigs = new MicroElement[12];
            for (int i = 0; i < twigs.Length; i++)
            {
                double px = -1.375 + 0.25 * i;
                twigs[i] = new MicroElement
                {
                    Id = (7UL << 32) | (uint)(100 + i), Kind = MicroKind.Twig, Anchored = true, PointCount = 2,
                    P0 = new Double3(px, 0, 0.5), P1 = new Double3(px, 1, 0.5), P2 = new Double3(px, 1, 0.5), Diameter = 0.005, Parent = -1, ParentShape = -1,
                };
            }
            var world = new FlightWorld(new ElementWorld(twigs));
            double mu = t.Number("fiber.mass_per_m_kg");
            const int nodes = 21;
            var x = new Double3[nodes];
            var s = new double[nodes];
            for (int i = 0; i < nodes; i++)
            {
                x[i] = new Double3(-2 + 4.0 * i / (nodes - 1), 0.6, 0);
                s[i] = 4.0 * i / (nodes - 1);
            }
            var chain = new CableChain(32, mu, t.Number("fiber.axial_stiffness_n")) { Gravity = FlightCore.Gravity };
            chain.Reset(x, s, nodes);
            var contacts = new FiberContacts(t, new FlightSetup { Tether = t, World = world }, world, null, LineRadius, mu, 100, 1, Dt);
            var events = new StepEvents(256);
            var ground = new GroundSample[32];
            var at = new XZ[32];
            var heights = new double[32];
            var normals = new Double3[32];
            Double3 left = x[0], right = x[nodes - 1], along = new Double3(0, 0, 1);
            var res = new Elements { NamedRight = true };
            var met = new HashSet<ulong>();
            for (int k = 0; k < 2000; k++)
            {
                events.Clear();
                int n = chain.Count;
                foreach (int end in new[] { 0, n - 1 })
                {
                    ref CableNode e = ref chain[end];
                    e.AnchorStiffness = 1e4;
                    e.AnchorDamping = 2 * Math.Sqrt(1e4 * e.Mass);
                    e.AnchorFollows = true;
                    e.AnchorTarget = (end == 0 ? left : right) + along * ((k + 1) * Dt);
                    e.AnchorVelocity = along;
                }
                var state = new FlightState { Step = k, Position = new Double3(0, 0.6, (k + 1) * Dt), Attitude = Quat.Identity };
                contacts.Prepare(chain, state, events);
                n = chain.Count;
                for (int i = 0; i < n; i++) at[i] = new XZ(chain[i].Position.X, chain[i].Position.Z);
                world.SampleGround(new ReadOnlySpan<XZ>(at, 0, n), new Span<GroundSample>(ground, 0, n));
                for (int i = 0; i < n; i++)
                {
                    heights[i] = ground[i].Height;
                    normals[i] = ground[i].Normal;
                    chain[i].Drag = new Sym3(1e-5, 1e-5, 1e-5, 0, 0, 0);
                    chain[i].Air = default;
                    chain[i].Force = default;
                }
                contacts.Assign(chain, ground, heights, normals, state);
                chain.Step(Dt);
                contacts.Finish(chain, state, events);
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].Kind != EventKind.FiberContact || events[i].Index != (byte)FiberMember.Twig) continue;
                    res.Events++;
                    ulong id = ((ulong)(uint)events[i].Object << 32) | (uint)events[i].Shape;
                    met.Add(id);
                    res.NamedRight &= Array.Exists(twigs, w => w.Id == id);
                }
                n = chain.Count;
                for (int i = 0; i < n; i++)
                    if (chain[i].Contact0.Active && chain[i].Contact0.MemberKind == (byte)FiberMember.Twig
                        || chain[i].Contact1.Active && chain[i].Contact1.MemberKind == (byte)FiberMember.Twig)
                    {
                        res.HeldSteps++;
                        break;
                    }
            }
            res.Distinct = met.Count;
            res.Wedges = chain.Wedges;
            return res;
        }

        // ---- "The near lists change no bit" (w3-farmland-ground 2.1) ----

        public struct NearDiff
        {
            /// <summary>The steps flown and the first whose record or events differ between the two flights (−1 for none).</summary>
            public long Steps, FirstDiffer;
            /// <summary>The near lists' flight's whole queries and one-shape checks, the reference's whole queries.</summary>
            public long Queries, Checks, ReferenceQueries;
            /// <summary>Node-steps held by a shape (a Layer or a Shape member) in the near lists' flight.</summary>
            public long Held;
        }

        /// <summary>
        /// Two flights of one plan in lockstep on the test range, the reference's near lists off (every segment the certificate
        /// does not clear takes the whole query, as before 2.1), each step's record and events compared as the log writes them.
        /// "straw": the line laid 30 m across S04's straw from the west, the drone dragging it low over the patch east and back;
        /// "yard": the line laid by the launch at the houses' start (track), the drone flying into the yard and holding at the
        /// house as flight 35 did (L1). Severe wind, the pilot's sessions'.
        /// </summary>
        public static NearDiff NearListsAgainstQueries(DroneSpec spec, string spot, double seconds)
        {
            FlightSetup Make(out Double3 foot)
            {
                FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Severe);
                setup.World = new FlightWorld(RangeScenarios.World);
                double stand = TetherScenarios.Stand(setup);
                if (spot == "straw")
                {
                    foot = RangeScenarios.Straw;
                    var layout = new List<Double3>();
                    for (double d = 0; d < 29.5; d += 1) layout.Add(foot + new Double3(d - 30, 0, 0));
                    setup.TetherLayout = layout.ToArray();
                }
                else
                {
                    StartPoint start = RangeScenarios.Start("track");
                    foot = start.Position;
                    DetMath.SinCos(start.HeadingDeg * Pi / 360, out double sin, out double cos);
                    setup.StartAttitude = new Quat(0, sin, 0, cos);
                }
                setup.StartPosition = new Double3(foot.X, foot.Y + stand, foot.Z);
                return setup;
            }
            FlightSetup a = Make(out Double3 at), b = Make(out _);
            var near = new FlightCore(a);
            var reference = new FlightCore(b);
            reference.Tether.Contacts.NearLists = false;
            var pilot = new SimPilot(near.Controller, a.StartPosition);
            var house = new Double3(-169.5, 0, -53);
            var ra = new byte[LogFormat.StepSize];
            var rb = new byte[LogFormat.StepSize];
            var ea = new byte[LogFormat.EventSize];
            var eb = new byte[LogFormat.EventSize];
            var res = new NearDiff { FirstDiffer = -1 };
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                double t = k * FlightCore.Dt;
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    double y = a.StartPosition.Y;
                    if (spot == "straw")
                    {
                        pilot.HoldVelocity = t >= 2;
                        pilot.TargetVelocity = new Double3(t < 6 ? 2.5 : -2.5, 0, 0);
                        pilot.Target = new Double3(at.X, y + (t < 2 ? DetMath.Min(0.6, t) : 0.5), at.Z);
                    }
                    else
                    {
                        // Up 1.2 m, into the yard to the house's corner over 6 s, then held there at 0.8 m.
                        double f = DetMath.Min(1, DetMath.Max(0, (t - 2) / 6));
                        pilot.HoldVelocity = false;
                        pilot.Target = new Double3(at.X + (house.X - at.X) * f, y + (t < 8 ? DetMath.Min(1.2, t) : 0.8), at.Z + (house.Z - at.Z) * f);
                    }
                    stick = pilot.Sticks(near, k * FlightCore.StepUs);
                }
                near.Advance(stick);
                reference.Advance(stick);
                res.Steps++;
                LogFormat.EncodeStep(ra, near.State, stick, near.Trace);
                LogFormat.EncodeStep(rb, reference.State, stick, reference.Trace);
                bool same = ra.AsSpan().SequenceEqual(rb) && near.Events.Count == reference.Events.Count;
                for (int e = 0; same && e < near.Events.Count; e++)
                {
                    LogFormat.EncodeEvent(ea, near.State.Step, near.Events[e]);
                    LogFormat.EncodeEvent(eb, reference.State.Step, reference.Events[e]);
                    same = ea.AsSpan().SequenceEqual(eb);
                }
                if (!same)
                {
                    res.FirstDiffer = near.State.Step;
                    break;
                }
                CableChain line = near.Tether.Chain;
                for (int i = 0; i < line.Count; i++)
                {
                    byte k0 = line[i].Contact0.MemberKind, k1 = line[i].Contact1.MemberKind;
                    if (line[i].Contact0.Active && (k0 == (byte)FiberMember.Layer || k0 == (byte)FiberMember.Shape)
                        || line[i].Contact1.Active && (k1 == (byte)FiberMember.Layer || k1 == (byte)FiberMember.Shape))
                        res.Held++;
                }
            }
            res.Queries = near.Tether.Contacts.QueriesTotal;
            res.Checks = near.Tether.Contacts.ChecksTotal;
            res.ReferenceQueries = reference.Tether.Contacts.QueriesTotal;
            near.Wind.Dispose();
            reference.Wind.Dispose();
            return res;
        }
    }

    /// <summary>The stub's floor (FloorWorld, steel and timber) with a set of synthetic elements MicroDetailNear returns: 1.7's
    /// stand-in for p7a's drawn crowns and mats (the test of the line's element contacts).</summary>
    public sealed class ElementWorld : IWorldQuery
    {
        readonly FloorWorld _floor = new FloorWorld(0, "belt_bare", "steel", "timber");
        readonly MicroElement[] _elements;

        public ElementWorld(MicroElement[] elements) => _elements = elements;

        public ulong ContentHash => _floor.ContentHash ^ 0x7D17;
        public int QueryVersion => _floor.QueryVersion;
        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _floor.SampleGround(points, results);
        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results, QueryFilter filter = QueryFilter.None) =>
            _floor.StaticContacts(previous, current, margin, results, filter);
        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits, QueryFilter filter = QueryFilter.None) =>
            _floor.Raycast(rays, maxDistance, hits, filter);
        public int WiresNear(Double3 centre, double radius, Span<Conductor> results) => _floor.WiresNear(centre, radius, results);
        public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results) => _floor.SnagsNear(centre, radius, results);
        public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _floor.Geometry(obj, shape, out geometry);
        public bool ShapeContact(int obj, int shape, in Capsule previous, in Capsule current, double margin, out StaticContact contact) =>
            _floor.ShapeContact(obj, shape, previous, current, margin, out contact);
        public bool Feature(int obj, int shape, out GroundFeature feature) => _floor.Feature(obj, shape, out feature);
        public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _floor.AddObject(name, shapes);
        public void ResetRuntimeObjects() => _floor.ResetRuntimeObjects();
        public string ObjectName(int obj) => _floor.ObjectName(obj);
        public string MaterialName(ushort material) => _floor.MaterialName(material);
        public ushort MaterialId(string name) => _floor.MaterialId(name);
        public string SurfaceName(byte surface) => _floor.SurfaceName(surface);

        public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results)
        {
            int count = 0;
            foreach (MicroElement e in _elements)
            {
                if ((e.Kind & kinds) == 0) continue;
                double reach = radius + e.Diameter / 2;
                if (!(FiberContacts.PointSegment(centre, e.P0, e.P1) <= reach * reach)) continue;
                if (count < results.Length) results[count] = e;
                count++;
            }
            return count;
        }
    }
}
