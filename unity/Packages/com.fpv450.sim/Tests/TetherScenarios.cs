using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// fiber-tether's 1.6 scenarios (p7d-tether: the spool, the span and the trail), flown on the stub's floor by the repository's
    /// drone legs-on with its line laid, under SimPilot. Without NUnit, so the tools can link it; TetherTests asserts them.
    /// </summary>
    public static class TetherScenarios
    {
        static TetherTable _table;

        /// <summary>The repository's tether.json, read once.</summary>
        public static TetherTable Table() =>
            _table ?? (_table = TetherTable.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(TetherTable.RelativePath))));

        /// <summary>
        /// The drone legs-on on the stub's floor at the origin facing +z, its line laid by the table's launch (or layout): its
        /// controller and propulsion, its drag and contacts, the wind level and the rain; the coil at the nominal loadout's
        /// payout, or coilLeft metres of line left on it.
        /// </summary>
        public static FlightSetup Setup(DroneSpec spec, WindLevel wind = WindLevel.Calm, RainLevel rain = RainLevel.Sunny, bool twoWay = false,
            Double3[] layout = null, double coilLeft = -1, ulong seed = 0x7D16)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            Loadout loadout = spec.Nominal;
            if (coilLeft >= 0) loadout.CoilPaidOut = spec.Coil.FiberLength - coilLeft;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            return new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(loadout), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Contacts = contacts, StartPosition = new Double3(0, stand, 0), StartPackCharge = 0.9, CameraPosition = spec.CameraPosition,
                Wind = WindSetup.Of(wind), Rain = rain, Tether = Table(), TetherCoilPart = spec.Coil.Part, TetherSpoolMass = spec.Coil.SpoolMass,
                TetherTwoWay = twoWay, TetherLayout = layout,
            };
        }

        /// <summary>The height of the body origin standing legs-on on the floor.</summary>
        public static double Stand(FlightSetup s) => s.StartPosition.Y;

        /// <summary>A plan: the pilot's target at t (position, and a velocity to hold when hold is set).</summary>
        public delegate void Plan(double t, FlightCore core, SimPilot pilot);

        /// <summary>Flies a setup for seconds under plan, after the arming (armed at 10 ms, the throttle low to 20 ms), calling
        /// each after every step.</summary>
        public static FlightCore Fly(FlightSetup setup, double seconds, Plan plan, Action<FlightCore> each)
        {
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    plan(k * FlightCore.Dt, core, pilot);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                each?.Invoke(core);
            }
            return core;
        }

        /// <summary>Climbs to height over the start by 2 m/s, holds there to t0, then flies along +z at speed to t1 and at
        /// back (along −z) after it.</summary>
        public static Plan OutAndBack(double stand, double height, double t0, double speed, double t1, double back)
        {
            return (t, core, pilot) =>
            {
                double y = stand + height;
                if (t < t0)
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, stand + DetMath.Min(height, 2 * t), 0);
                    return;
                }
                pilot.HoldVelocity = true;
                pilot.TargetVelocity = new Double3(0, 0, t < t1 ? speed : -back);
                pilot.Target = new Double3(0, y, core.State.Position.Z);
            };
        }

        // ---- "Line pays out as the drone flies away" ----

        public struct PayOut
        {
            /// <summary>The line paid out at the end, m; the steps it fell (must be none); the least margin of the line from the
            /// anchor over the straight distance to the exit, m (never below 0).</summary>
            public double Paid;
            public int Fell;
            public double Margin;
            /// <summary>Over the last 5 s: the mean speed of payout and of the drone over the ground, m/s, the mean logged exit
            /// tension and the resistance's law over the lip's capstan at the drone's speed (the balloon steady; the lip and the
            /// irregularity at their means over the steps), N.</summary>
            public double Speed, Ground, Exit, Law;
            public long Wedges, Clips, SafeSteps;
            public double PeelEnergy;
        }

        /// <summary>Up to 10 m, then straight away along +z at speed for 20 s in Calm.</summary>
        public static PayOut FlyAway(DroneSpec spec, double speed)
        {
            FlightSetup setup = Setup(spec);
            const double climb = 6, cruise = 20;
            var r = new PayOut { Margin = double.PositiveInfinity };
            double last = 0, exit = 0, rate = 0, ground = 0, lipIrr = 0;
            int n = 0;
            FlightCore c = Fly(setup, climb + cruise, OutAndBack(Stand(setup), 10, climb, speed, double.PositiveInfinity, 0), core =>
            {
                FiberLine f = core.Tether;
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                if (tr.PaidOut < last) r.Fell++;
                last = tr.PaidOut;
                CableChain line = f.Chain;
                Double3 exitPoint = line[line.Count - 1].Position;
                double margin = line[line.Count - 1].Material - (exitPoint - f.Trail[0]).Length();
                if (margin < r.Margin) r.Margin = margin;
                if (core.State.Step * FlightCore.Dt > climb + cruise - 5)
                {
                    exit += tr.ExitTension;
                    lipIrr += DetMath.Exp(Table().Number("spool.lip_friction") * tr.LipAngle) * tr.Irregularity;
                    rate += tr.PayoutSpeed;
                    ground += DetMath.Sqrt(core.State.Velocity.X * core.State.Velocity.X + core.State.Velocity.Z * core.State.Velocity.Z);
                    n++;
                }
            });
            r.Paid = last;
            r.Exit = exit / n;
            r.Speed = rate / n;
            r.Ground = ground / n;
            // T = μ·V² + e^(μθ)·G₀·irregularity·max(1, (V/v_ref)^n): the pack's law at the flight's speed (CableSpool).
            TetherTable t = Table();
            double v = r.Ground, vref = t.Number("spool.peel_reference_rate_m_s");
            double rateLaw = v > vref ? Math.Pow(v / vref, t.Number("spool.peel_rate_exponent")) : 1;
            r.Law = t.Number("fiber.mass_per_m_kg") * v * v + lipIrr / n * c.Tether.PeelEnergy * rateLaw;
            r.Wedges = c.Tether.Chain.Wedges;
            r.Clips = c.Tether.Chain.Clips;
            r.SafeSteps = c.Tether.Chain.SafeSteps;
            r.PeelEnergy = c.Tether.PeelEnergy;
            return r;
        }

        // ---- "Line never comes back" and "A known-wrong two-way spool" ----

        public struct OutBack
        {
            /// <summary>The most paid out, at the turn and at the end, m; the steps the paid-out length fell.</summary>
            public double Most, AtTurn, AtEnd;
            public int Fell;
            /// <summary>The least paid out after the turn, m; the static hold e^(μθ)·P(0) at the turn, N, and the least and the
            /// most exit tension from 0.5 s to 2.5 s after it, N.</summary>
            public double LeastAfterTurn, Hold, SlackLeast, SlackTension;
            /// <summary>The span's line over its chord (from the touchdown to the exit) 3 s after the turn, and the trail's points
            /// then and at the end.</summary>
            public double Slack;
            public int TrailAtTurn, TrailAtEnd;
            public long Wedges;
        }

        /// <summary>At 10 m and 10 m/s along +z for 10 s (100 m out), then straight back along −z at 10 m/s for 6 s, then a
        /// hover for 4 s, where the slack comes to rest.</summary>
        public static OutBack OutThenBack(DroneSpec spec, bool twoWay)
        {
            FlightSetup setup = Setup(spec, twoWay: twoWay);
            const double climb = 6, outFor = 10, backFor = 6, hoverFor = 4, turn = climb + outFor;
            var r = new OutBack { LeastAfterTurn = double.PositiveInfinity, SlackLeast = double.PositiveInfinity };
            double last = 0, stand = Stand(setup);
            Plan flight = OutAndBack(stand, 10, climb, 10, turn, 10);
            Double3 hover = default;
            Plan plan = (t, core, pilot) =>
            {
                if (t < turn + backFor)
                {
                    flight(t, core, pilot);
                    hover = core.State.Position;
                    return;
                }
                pilot.HoldVelocity = false;
                pilot.Target = hover;
            };
            FlightCore c = Fly(setup, turn + backFor + hoverFor, plan, core =>
            {
                FiberLine f = core.Tether;
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                double t = core.State.Step * FlightCore.Dt;
                if (tr.PaidOut < last) r.Fell++;
                last = tr.PaidOut;
                if (tr.PaidOut > r.Most) r.Most = tr.PaidOut;
                if (core.State.Step == (long)(turn * 1000))
                {
                    r.AtTurn = tr.PaidOut;
                    r.Hold = DetMath.Exp(Table().Number("spool.lip_friction") * tr.LipAngle) * f.PeelEnergy * tr.Irregularity;
                    r.TrailAtTurn = tr.Trail;
                }
                if (t > turn && tr.PaidOut < r.LeastAfterTurn) r.LeastAfterTurn = tr.PaidOut;
                if (t > turn + 0.5 && t <= turn + 2.5)
                {
                    if (tr.ExitTension > r.SlackTension) r.SlackTension = tr.ExitTension;
                    if (tr.ExitTension < r.SlackLeast) r.SlackLeast = tr.ExitTension;
                }
                if (core.State.Step == (long)((turn + 3) * 1000))
                {
                    CableChain line = f.Chain;
                    r.Slack = (line[line.Count - 1].Material - line[0].Material) / (line[line.Count - 1].Position - line[0].Position).Length();
                }
            });
            r.AtEnd = last;
            r.TrailAtEnd = c.Tether.TrailCount;
            r.Wedges = c.Tether.Chain.Wedges;
            return r;
        }

        // ---- "A falling line settles at its terminal speed" ----

        public struct Fall
        {
            /// <summary>The line's mean fall speed once it has accelerated (5–30 s), m/s; the time its mean height reached the
            /// ground 50 m below, s; the law's terminal speed (weight against White's drag at it), m/s.</summary>
            public double Speed, Landed, Terminal;
        }

        /// <summary>A horizontal 10 m length of the fiber, 21 nodes, released at rest 50 m up in still air, its drag the
        /// flight's (FiberDrag on the chain's implicit linear drag).</summary>
        public static Fall Falling()
        {
            TetherTable table = Table();
            double mu = table.Number("fiber.mass_per_m_kg"), d = table.Number("fiber.coating_diameter_m");
            var drag = new FiberDrag(table, d);
            const int n = 21;
            var x = new Double3[n];
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                s[i] = 0.5 * i;
                x[i] = new Double3(0.5 * i, 50, 0);
            }
            var chain = new CableChain(n, mu, table.Number("fiber.axial_stiffness_n")) { Gravity = FlightCore.Gravity };
            chain.Reset(x, s, n);
            var r = new Fall { Landed = double.NaN };
            double h5 = 0, h30 = 0;
            for (int k = 1; k <= 60000; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    ref CableNode a = ref chain[i];
                    double length = 0.5 * ((i > 0 ? 0.5 : 0) + (i + 1 < n ? 0.5 : 0));
                    drag.Apply(ref a, default, new Double3(1, 0, 0), length);
                }
                chain.Step(FlightCore.Dt);
                double h = 0;
                for (int i = 0; i < n; i++) h += chain[i].Position.Y / n;
                if (k == 5000) h5 = h;
                if (k == 30000) h30 = h;
                if (double.IsNaN(r.Landed) && h <= 0) r.Landed = k * FlightCore.Dt;
            }
            r.Speed = (h5 - h30) / 25;
            // The law's terminal speed: μg = ½ρ·v²·D·C_d(v), by bisection.
            double lo = 0.1, hi = 10;
            for (int it = 0; it < 100; it++)
            {
                double v = 0.5 * (lo + hi);
                if (drag.PerMetre(v) > mu * -FlightCore.Gravity.Y) hi = v;
                else lo = v;
            }
            r.Terminal = 0.5 * (lo + hi);
            return r;
        }

        // ---- "A pulled trail lifts" ----

        public struct PulledTrail
        {
            /// <summary>The trail's points once frozen, the fewest it came down to, and whether it ever rose again while lifting.</summary>
            public int Frozen, Fewest;
            public bool RoseWhileLifting;
            /// <summary>The most nodes the chain held.</summary>
            public int MostNodes;
            /// <summary>At the first step that paid out: the trail's points, the exit tension over the static hold, and the line
            /// from the snag to the exit over its chord.</summary>
            public int TrailAtPayout;
            public double TensionOverHold, LineOverChord;
            public double Paid;
            public long Wedges;
        }

        /// <summary>The line laid from a snag 50 m behind in a zigzag (60 m of line, 3 m aside, so it has slack), held there; the
        /// drone sits on it 2 s for the trail to freeze, then climbs to 5 m and flies on along +z at 3 m/s.</summary>
        public static PulledTrail PullTrail(DroneSpec spec)
        {
            var layout = new Double3[6];
            for (int i = 0; i < 6; i++) layout[i] = new Double3(i % 2 == 0 ? 0 : 6, 0, -50 + 10 * i);
            layout[5] = new Double3(0, 0, -0.1);
            FlightSetup setup = Setup(spec, layout: layout);
            double stand = Stand(setup);
            var r = new PulledTrail { Fewest = int.MaxValue, TrailAtPayout = -1 };
            bool lifting = false;
            int lastTrail = 0;
            FlightCore c = Fly(setup, 14, (t, core, pilot) =>
            {
                pilot.HoldVelocity = false;
                if (t < 2)
                {
                    pilot.Target = new Double3(0, stand - 0.05, 0); // sits on the floor, the throttle low
                    return;
                }
                if (t < 5)
                {
                    pilot.Target = new Double3(0, stand + DetMath.Min(5, 2 * (t - 2)), 0);
                    return;
                }
                pilot.HoldVelocity = true;
                pilot.TargetVelocity = new Double3(0, 0, 3);
                pilot.Target = new Double3(0, stand + 5, core.State.Position.Z);
            }, core =>
            {
                FiberLine f = core.Tether;
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                double t = core.State.Step * FlightCore.Dt;
                if (tr.Nodes > r.MostNodes) r.MostNodes = tr.Nodes;
                if (t <= 2) r.Frozen = tr.Trail;
                else
                {
                    if (tr.Trail < lastTrail) lifting = true;
                    if (lifting && tr.Trail > lastTrail && tr.Trail > r.Fewest) r.RoseWhileLifting = true;
                    if (tr.Trail < r.Fewest) r.Fewest = tr.Trail;
                }
                lastTrail = tr.Trail;
                if (r.TrailAtPayout < 0 && tr.PaidOut > 1e-6 && t > 2)
                {
                    CableChain line = f.Chain;
                    r.TrailAtPayout = tr.Trail;
                    double hold = DetMath.Exp(Table().Number("spool.lip_friction") * tr.LipAngle) * f.PeelEnergy * tr.Irregularity;
                    r.TensionOverHold = tr.ExitTension / hold;
                    r.LineOverChord = (line[line.Count - 1].Material - f.TrailMaterial(0)) / (line[line.Count - 1].Position - f.Trail[0]).Length();
                }
            });
            r.Paid = c.Tether.PaidOut;
            r.Wedges = c.Tether.Chain.Wedges;
            return r;
        }

        // ---- "Rain makes it heavier and draggier" ----

        public struct Wet
        {
            public double MassPerMetre, Diameter;
            /// <summary>The airborne line's mean height over the floor at the end, from the touchdown to 2 m behind the exit, m;
            /// its nodes' mean sink over the last 5 s (more than 5 m of line behind the exit and 0.3 m up), m/s; and the law's
            /// terminal speed for this line (its weight against White's drag at its diameter), m/s.</summary>
            public double MeanHeight, Sink, Terminal;
            public long Wedges;
        }

        /// <summary>The same 30 s: up to 10 m, then 10 m/s along +z, in Calm, at a rain level.</summary>
        public static Wet Cruise(DroneSpec spec, RainLevel rain)
        {
            FlightSetup setup = Setup(spec, rain: rain);
            double sink = 0;
            int sinks = 0;
            FlightCore c = Fly(setup, 30, OutAndBack(Stand(setup), 10, 6, 10, double.PositiveInfinity, 0), core =>
            {
                if (core.State.Step <= 25000) return;
                CableChain l = core.Tether.Chain;
                double ex = l[l.Count - 1].Material;
                for (int i = 0; i < l.Count; i++)
                    if (ex - l[i].Material > 5 && l[i].Position.Y > 0.3)
                    {
                        sink -= l[i].Velocity.Y;
                        sinks++;
                    }
            });
            FiberLine f = c.Tether;
            CableChain line = f.Chain;
            double sum = 0, length = 0, exit = line[line.Count - 1].Material;
            for (int j = 0; j + 1 < line.Count; j++)
            {
                if (exit - line[j + 1].Material < 2) break;
                double l = line[j + 1].Material - line[j].Material;
                sum += 0.5 * (line[j].Position.Y + line[j + 1].Position.Y) * l;
                length += l;
            }
            double lo = 0.1, hi = 10, weight = f.MassPerMetre * -FlightCore.Gravity.Y;
            for (int it = 0; it < 100; it++)
            {
                double v = 0.5 * (lo + hi);
                if (f.Drag.PerMetre(v) > weight) hi = v;
                else lo = v;
            }
            return new Wet
            {
                MassPerMetre = f.MassPerMetre, Diameter = f.Diameter, MeanHeight = sum / length, Sink = sink / sinks, Terminal = 0.5 * (lo + hi),
                Wedges = line.Wedges,
            };
        }

        // ---- "A line under a punch is lifted and drawn" ----

        public struct Punch
        {
            /// <summary>Over the first 0.3 s of thrust, the line lying beside the skids: the most height it rose off the floor, m
            /// (the drone's rise pulls its near end up too), the wall jet's most lift on it over its weight, its most speed, m/s,
            /// and the least distance it came to a disc's rim, m.</summary>
            public double Lift, LiftOverWeight, Speed, Rim;
            /// <summary>The steps a node crossed a turning disc (1.8 cuts the line there).</summary>
            public int Crossed;
            /// <summary>The worst relative error of a node's air against the field's law at its position, and of its drag force
            /// against ½ρ·C_d·u⊥²·D·L across and the skin friction along, over every node of every step.</summary>
            public double AirError, DragError;
            public int Checked;
        }

        /// <summary>
        /// Legs-on on the floor in still air with the line laid under the rear: armed, then from 0.1 s the throttle full, or held at
        /// hover thrust (the pilot lifting 0.1 m), for 0.3 s. Watched: the line lying on the floor beside the skids when the thrust
        /// began (within 0.5 m of the body's axis, more than 0.3 m of line from the exit, so not the piece rising to it), and every
        /// node's loads against the field's law at the point it took them.
        /// </summary>
        public static Punch Liftoff(DroneSpec spec, bool full)
        {
            FlightSetup setup = Setup(spec, WindLevel.Still);
            var r = new Punch { Rim = double.PositiveInfinity };
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition + new Double3(0, 0.1, 0));
            double radius = setup.Propulsion.Props.Diameter / 2;
            double lo = double.NaN, hi = double.NaN; // the watched line's material span
            for (int k = 0; k < 400; k++)
            {
                StickSample stick;
                if (k < 100) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else if (full) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = true, Throttle = 1 };
                else stick = pilot.Sticks(core, k * FlightCore.StepUs);
                core.Advance(stick);
                FiberLine f = core.Tether;
                CableChain line = f.Chain;
                ref readonly FlightState s = ref core.State;
                double exit = line[line.Count - 1].Material;
                if (k == 99)
                {
                    lo = double.PositiveInfinity;
                    hi = double.NegativeInfinity;
                    for (int i = 0; i < line.Count - 1; i++)
                    {
                        Double3 rel = line[i].Position - s.Position;
                        if (rel.X * rel.X + rel.Z * rel.Z < 0.25 && exit - line[i].Material > 0.3 && line[i].Position.Y < 0.01)
                        {
                            lo = DetMath.Min(lo, line[i].Material);
                            hi = DetMath.Max(hi, line[i].Material);
                        }
                    }
                }
                if (k < 100) continue;
                Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
                for (int i = 0; i < line.Count; i++)
                {
                    ref CableNode a = ref line[i];
                    if (a.Material < lo || a.Material > hi) continue;
                    if (a.Position.Y > r.Lift) r.Lift = a.Position.Y;
                    double v = a.Velocity.Length();
                    if (v > r.Speed) r.Speed = v;
                    for (int m = 0; m < 4; m++)
                    {
                        Double3 q = a.Position - (s.Position + s.Attitude.Rotate(setup.Propulsion.Motors[m].Position));
                        double z = Double3.Dot(q, up);
                        double lateral = (q - up * z).Length();
                        double rim = DetMath.Sqrt((lateral - radius) * (lateral - radius) + z * z);
                        if (rim < r.Rim) r.Rim = rim;
                    }
                }
                for (int i = 0; i + 1 < line.Count; i++)
                    for (int m = 0; m < 4; m++)
                    {
                        Double3 hub = s.Position + s.Attitude.Rotate(setup.Propulsion.Motors[m].Position);
                        double za = Double3.Dot(line[i].Position - hub, up), zb = Double3.Dot(line[i + 1].Position - hub, up);
                        if (za * zb > 0) continue;
                        double w = za == zb ? 0 : za / (za - zb);
                        Double3 cross = line[i].Position + (line[i + 1].Position - line[i].Position) * w - hub;
                        if ((cross - up * Double3.Dot(cross, up)).Length() < radius) r.Crossed++;
                    }
                // The loads each node took this step against the field's law at that point (still air: the rotors' alone), and
                // the drag on that air by White's fit across and skin friction along, written out here apart from FiberDrag.
                TetherTable t = Table();
                double rho = t.Number("fiber.air_density_kg_m3"), nu = t.Number("fiber.air_kinematic_viscosity_m2_s");
                for (int i = 0; i < f.LoadCount; i++)
                {
                    if (f.LoadMaterial(i) >= lo && f.LoadMaterial(i) <= hi)
                    {
                        double lift = f.LoadLift(i).Length() / (f.MassPerMetre * -FlightCore.Gravity.Y * f.LoadLength(i));
                        if (lift > r.LiftOverWeight) r.LiftOverWeight = lift;
                    }
                    Double3 expected = ExpectedAir(core, f.LoadPoint(i));
                    double airError = (f.LoadAir(i) - expected).Length() / DetMath.Max(expected.Length(), 1e-6);
                    if (airError > r.AirError) r.AirError = airError;
                    Double3 tan = f.LoadTangent(i), u = f.LoadAir(i) - f.LoadVelocity(i);
                    double ua = Double3.Dot(u, tan);
                    Double3 uc = u - tan * ua;
                    double d = f.Diameter, re = Math.Max(1, Math.Min(2e5, uc.Length() * d / nu));
                    double cd = 1 + 10 * Math.Pow(re, -2.0 / 3.0);
                    Double3 law = uc * (0.5 * rho * d * cd * uc.Length() * f.LoadLength(i))
                        + tan * (0.5 * rho * Math.PI * d * t.Number("fiber.drag_axial") * Math.Abs(ua) * ua * f.LoadLength(i));
                    if (law.Length() > 1e-12)
                    {
                        double dragError = (f.LoadForce(i) - law).Length() / law.Length();
                        if (dragError > r.DragError) r.DragError = dragError;
                    }
                    r.Checked++;
                }
            }
            r.Lift -= Table().Number("fiber.coating_diameter_m") / 2;
            return r;
        }

        // The rotors' air at p as RotorAir's design gives it (design 4), from the flight's rotors at the start of the line's last
        // step: the regions written out again here, apart from RotorAir's code.
        static Double3 ExpectedAir(FlightCore core, Double3 p)
        {
            RotorAir air = core.Tether.Rotors;
            TetherTable table = Table();
            double radius = air.Radius, wakeRadius = table.Number("rotor_air.far_wake_radius_ratio") * radius;
            double jet = table.Number("rotor_air.wall_jet_thickness_over_radius") * radius;
            ref readonly FlightState s = ref core.State;
            Double3 n = s.Attitude.Rotate(new Double3(0, 1, 0));
            Double3 sum = default;
            for (int m = 0; m < 4; m++)
            {
                double vi = air.Induced(m);
                if (!(vi > 0)) continue;
                Double3 hub = s.Position + s.Attitude.Rotate(core.Propulsion.Hub(m));
                Double3 q = p - hub;
                double z = Double3.Dot(q, n);
                if (z > 0)
                {
                    Double3 lateral = q - n * z;
                    if (lateral.Length() < radius) sum = sum - n * (vi * (1 - z / Math.Sqrt(z * z + radius * radius)));
                    else sum = sum - q * (Math.PI * radius * radius * vi / (2 * Math.PI * q.Length() * q.Length() * q.Length()));
                    continue;
                }
                Double3 w = air.Wake(m);
                double h = Double3.Dot(q, w);
                if (h > 0 && (q - w * h).Length() < wakeRadius && (!air.Lands(m) || p.Y > 0))
                {
                    sum = sum - n * (2 * vi);
                    continue;
                }
                if (!air.Lands(m) || !(p.Y <= jet)) continue;
                Double3 land = air.Landing(m);
                double dx = p.X - land.X, dz = p.Z - land.Z, r = Math.Sqrt(dx * dx + dz * dz);
                if (!(r > wakeRadius)) continue;
                double u = Math.PI * radius * radius * vi / (2 * Math.PI * r * jet);
                sum = sum + new Double3(dx / r * u, 0, dz / r * u);
            }
            return sum;
        }

        // ---- "The exit's pull and its moment" ----

        public struct Pull
        {
            /// <summary>The worst relative difference, over every step, of the force on the drone from (T_exit − μ·ṡ²) along the
            /// first segment, and of the moment from that force at the exit about the CoM.</summary>
            public double ForceError, MomentError;
            public int Steps;
            /// <summary>Hovering with the line taut: the most exit tension, N. Held (the coil empty): the most exit tension, N, the
            /// time it emptied, s, how far the drone flew on past where it emptied, m, its most pitch toward the hold (rad, + nose
            /// toward it), and the moment's pitch about body x at the most tension, N·m.</summary>
            public double HoverTension, HeldTension, EmptyAt, OnPast, Pitch, PitchMoment;
            public double FreeOnPast;
        }

        /// <summary>
        /// The line's pull: hovering 5 m up with the line taut behind it (the pilot drifting back from the anchor slowly) its force
        /// and moment checked every step against the logged exit tension along the first segment at the exit; then the coil with
        /// 15 m of line left, flown away at 3 m/s until it is empty and the line holds the drone; and the same flight with a full
        /// coil, for how far it flies on.
        /// </summary>
        public static Pull ExitPull(DroneSpec spec)
        {
            var r = new Pull { EmptyAt = double.NaN };
            Double3 expectedForce = default, exitBody = default;
            bool have = false;
            double mu = Table().Number("fiber.mass_per_m_kg");
            Action<FlightCore> check = core =>
            {
                FiberLine f = core.Tether;
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                if (have)
                {
                    double fe = (tr.Force - expectedForce).Length() / DetMath.Max(expectedForce.Length(), 1e-300);
                    if (expectedForce.Length() > 0 && fe > r.ForceError) r.ForceError = fe;
                    r.Steps++;
                }
                CableChain line = f.Chain;
                Double3 outward = line[line.Count - 2].Position - line[line.Count - 1].Position;
                double sdot = tr.PayoutSpeed;
                expectedForce = outward * ((tr.ExitTension - (sdot > 0 ? mu * sdot * sdot : 0)) / outward.Length());
                exitBody = f.ExitBody;
                have = true;
            };
            // The moment: from the record's force at the exit about the CoM the step began with.
            MassProperties bodyBefore = default;
            Quat attitudeBefore = default;
            Action<FlightCore> moment = core =>
            {
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                Double3 m = Double3.Cross(exitBody - bodyBefore.CenterOfMass, attitudeBefore.InverseRotate(tr.Force));
                double me = (tr.Moment - m).Length() / DetMath.Max(m.Length(), 1e-300);
                if (m.Length() > 0 && me > r.MomentError) r.MomentError = me;
            };

            FlightSetup hover = Setup(spec);
            double stand = Stand(hover);
            var core0 = new FlightCore(hover);
            var pilot = new SimPilot(core0.Controller, hover.StartPosition);
            exitBody = core0.Tether.ExitBody;
            for (int k = 0; k < 12000; k++)
            {
                double t = k * FlightCore.Dt;
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    pilot.Target = new Double3(0, stand + DetMath.Min(5, 2 * t), t < 3 ? 0 : 0.3 * (t - 3));
                    stick = pilot.Sticks(core0, k * FlightCore.StepUs);
                }
                bodyBefore = core0.Body;
                attitudeBefore = core0.State.Attitude;
                core0.Advance(stick);
                moment(core0);
                check(core0);
                if (t > 6 && core0.Trace.Tether.ExitTension > r.HoverTension) r.HoverTension = core0.Trace.Tether.ExitTension;
            }

            r.FreeOnPast = HeldFlight(spec, -1, ref r, false);
            r.OnPast = HeldFlight(spec, 15, ref r, true);
            return r;
        }

        // Up to 5 m, then away along +z at 3 m/s for 25 s; with coilLeft ≥ 0 the coil empties on the way. Returns how far the drone
        // flew past where it emptied (or past the same point with a full coil).
        static double HeldFlight(DroneSpec spec, double coilLeft, ref Pull r, bool held)
        {
            FlightSetup setup = Setup(spec, coilLeft: coilLeft);
            double stand = Stand(setup), emptyZ = double.NaN, emptyAt = double.NaN, mostZ = double.NegativeInfinity, most = 0, pitch = 0, pitchMoment = 0;
            double hold = 0;
            FlightCore c = Fly(setup, 30, OutAndBack(stand, 5, 5, 3, double.PositiveInfinity, 0), core =>
            {
                ref readonly TetherTrace tr = ref core.Trace.Tether;
                double t = core.State.Step * FlightCore.Dt;
                if (double.IsNaN(emptyAt) && (held ? (tr.Flags & (byte)TetherFlags.CoilEmpty) != 0 : tr.PaidOut >= 15 - 3.2))
                {
                    emptyAt = t;
                    emptyZ = core.State.Position.Z;
                    hold = tr.ExitTension;
                }
                if (!double.IsNaN(emptyAt))
                {
                    if (core.State.Position.Z > mostZ) mostZ = core.State.Position.Z;
                    if (tr.ExitTension > most)
                    {
                        most = tr.ExitTension;
                        pitchMoment = tr.Moment.X;
                    }
                    // The nose's tilt toward the hold (−z): the body's +z axis below the horizon, nose down, is away from it.
                    Double3 nose = core.State.Attitude.Rotate(new Double3(0, 0, 1));
                    double toward = nose.Y; // + nose up: toward a hold behind and below
                    if (toward > pitch) pitch = toward;
                }
            });
            if (held)
            {
                r.HeldTension = most;
                r.EmptyAt = emptyAt;
                r.Pitch = pitch;
                r.PitchMoment = pitchMoment;
            }
            return mostZ - emptyZ;
        }

        // ---- "The drone gets lighter" ----

        public struct Lighter
        {
            /// <summary>The line paid out when the run stopped (just past the 2000 m mark), m; the body's mass then less at the
            /// start, kg; the centre of mass against the mass build at that coil, m; the rebuilds; the worst relative change of the
            /// angular momentum of the drone and the line that left at a rebuild.</summary>
            public double Paid, MassFall, ComError, MomentumError;
            public int Rebuilds;
            public long Wedges;
        }

        /// <summary>Up to 20 m, then along +z at 20 m/s until 2000 m of line have paid out (each 10 m a rebuild), in Calm, on
        /// the exact build (no placement), dry.</summary>
        public static Lighter TwoKilometres(DroneSpec spec)
        {
            FlightSetup setup = Setup(spec);
            DronePart coil = spec.Parts[spec.Coil.Part];
            double start = setup.Mass.Properties.Mass;
            var r = new Lighter();
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            Plan plan = OutAndBack(Stand(setup), 20, 10, 20, double.PositiveInfinity, 0);
            double coilMass = core.Tether.CoilMassAtStart;
            for (int k = 0; k < 400000 && core.Tether.PaidOut < 2000; k++)
            {
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    plan(k * FlightCore.Dt, core, pilot);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                MassProperties before = core.Body;
                core.Advance(stick);
                if (!core.Tether.MassChanged) continue;
                r.Rebuilds++;
                // The angular momentum about the world origin of the drone before the rebuild, against the drone after it and
                // the line that left (the coil's shape at its centre, the mass it lost, moving with the body).
                ref readonly FlightState s = ref core.State;
                double lost = coilMass - core.Tether.CoilMass;
                coilMass = core.Tether.CoilMass;
                Double3 change = Momentum(s, core.Body) + PartMomentum(s, coil, lost) - Momentum(s, before);
                double e = change.Length() / Momentum(s, before).Length();
                if (e > r.MomentumError) r.MomentumError = e;
            }
            r.Paid = core.Tether.PaidOut;
            r.MassFall = start - core.Body.Mass;
            Loadout loadout = spec.Nominal;
            loadout.CoilPaidOut += 10 * (int)(core.Tether.PaidOut / 10);
            Double3 expected = spec.BuildMass(loadout).Properties.CenterOfMass;
            r.ComError = (core.Body.CenterOfMass - expected).Length();
            r.Wedges = core.Tether.Chain.Wedges;
            return r;
        }

        // L about the world origin of a body: M·(c × v_c) + R·I·ω (c the CoM in the world, v_c its velocity).
        static Double3 Momentum(in FlightState s, in MassProperties body)
        {
            Double3 c = s.Position + s.Attitude.Rotate(body.CenterOfMass);
            Double3 vc = s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, body.CenterOfMass));
            return Double3.Cross(c, vc) * body.Mass + s.Attitude.Rotate(body.Inertia * s.BodyRate);
        }

        // The angular momentum of mass m of a part's shape at its place in the body, moving with the body.
        static Double3 PartMomentum(in FlightState s, in DronePart p, double m)
        {
            Double3 c = s.Position + s.Attitude.Rotate(p.Position);
            Double3 v = s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, p.Position));
            return Double3.Cross(c, v) * m + s.Attitude.Rotate(p.InertiaPerKg() * s.BodyRate) * m;
        }
    }
}
