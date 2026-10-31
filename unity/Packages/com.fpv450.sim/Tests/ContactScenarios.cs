using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's scenarios on the world-query stub (p6-flight-core 1.6): the repository's drone with its contacts
    /// on FloorWorld's rigid floor, the launch stand and a copy of L01_straight as runtime objects, flown by the
    /// closed-loop pilot (SimPilot) the way the pilot flies them: the landing with the sticks held and with the throttle
    /// cut, the liftoff from the stand, the pole at 10 m/s. The suite asserts them (ContactTests); tools/flightbench
    /// --contacts prints them. Still air, so the contacts alone set what happens.
    /// </summary>
    public static class ContactScenarios
    {
        const double G = 9.80665;

        static MaterialRow[] _materials;
        static ulong _materialsHash;

        /// <summary>The repository's assets-src/physics_materials.json, read once (PhysicsMaterials.Parse).</summary>
        public static MaterialRow[] RepositoryMaterials(out ulong hash)
        {
            if (_materials == null) _materials = PhysicsMaterials.Parse(System.IO.File.ReadAllText(RepositoryFile(PhysicsMaterials.RelativePath)), out _materialsHash);
            hash = _materialsHash;
            return _materials;
        }

        /// <summary>A repository file, found from this file's own path (the .NET tools compile it in place) or from the
        /// working directory upwards (Unity's editor runs in unity/); without NUnit, so flightbench links it too.</summary>
        public static string RepositoryFile(string relative, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        {
            var starts = new List<string>();
            if (!string.IsNullOrEmpty(here)) starts.Add(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(here)));
            starts.Add(System.IO.Directory.GetCurrentDirectory());
            foreach (string start in starts)
                for (string dir = start; dir != null; dir = System.IO.Path.GetDirectoryName(dir))
                    if (System.IO.File.Exists(System.IO.Path.Combine(dir, relative)))
                        return System.IO.Path.Combine(dir, relative);
            throw new System.IO.FileNotFoundException(relative + " was not found above " + here);
        }

        /// <summary>L01_straight's collision (unity/Assets/_Project/Art/Models/lines/L01/L01_straight.assetmeta.json, the
        /// asset's own: two capsules and a cylinder of timber up the pole), at a foot on the ground.</summary>
        public static RuntimeObject Pole(FlightWorld world, Double3 foot)
        {
            ushort timber = world.Query.MaterialId("timber");
            var x = new Double3(1, 0, 0);
            var y = new Double3(0, 1, 0);
            var z = new Double3(0, 0, 1);
            return new RuntimeObject("L01_straight", new[]
            {
                new ShapeGeometry { Kind = ShapeKind.Capsule, Center = foot + new Double3(0, 1.25, 0), AxisX = x, AxisY = y, AxisZ = z, Radius = 0.112895,
                    HalfLength = 2.725789 / 2 - 0.112895, Material = timber },
                new ShapeGeometry { Kind = ShapeKind.Capsule, Center = foot + new Double3(0, 3.75, 0), AxisX = x, AxisY = y, AxisZ = z, Radius = 0.099737,
                    HalfLength = 2.699474 / 2 - 0.099737, Material = timber },
                new ShapeGeometry { Kind = ShapeKind.Cylinder, Center = foot + new Double3(0, 6.25, 0), AxisX = x, AxisY = y, AxisZ = z, Radius = 0.086579,
                    HalfLength = 1.25, Material = timber },
            });
        }

        /// <summary>The drone at a loadout with its contacts and drag, in still air on the stub's floor, resting at start.</summary>
        public static FlightSetup Setup(DroneSpec spec, MaterialRow[] materials, ulong materialsHash, bool legs, Double3 start, ulong seed = 0x450F)
        {
            Loadout loadout = spec.Nominal;
            loadout.Legs = legs;
            return new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(loadout), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Contacts = spec.BuildContacts(loadout, materials, materialsHash), StartPosition = start, StartPackCharge = 0.9,
            };
        }

        /// <summary>The body origin's height of the legs-on drone standing on the floor at y = 0, its skids unloaded.</summary>
        public static double Standing(ContactSpec contacts)
        {
            double low = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support) low = DetMath.Min(low, s.A.Y - s.Radius);
            return -low;
        }

        // ---- The landing ------------------------------------------------------------------------------------------------

        public struct Landing
        {
            /// <summary>The descent at the first touch, m/s (a support's ContactMade); the throttle the pilot held, 0–1.</summary>
            public double TouchSpeed, HeldThrottle;

            /// <summary>After the first touch: the most any leg compressed, m; the most a leg carried, N, and over the weight.</summary>
            public double PeakCompression, PeakLegLoad, PeakLegOverWeight;

            /// <summary>Whether every support left the ground after the first touch, the highest the skids rose (the lowest
            /// support's bottom over the floor, m), and the upward speed they left at, m/s, over TouchSpeed.</summary>
            public bool Separated;
            public double SeparationHeight, LeaveSpeed, Restitution;

            /// <summary>The last time after the first touch a leg carried nothing (s), and the time it took to come to rest
            /// with every leg loaded and the body slower than 1 cm/s, from the first touch, s (NaN if never).</summary>
            public double LastUnloaded, Settled;

            public int Tunnels, Crashes, Lost, ContactEvents;
        }

        /// <summary>
        /// The manifesto's §2.6: the pilot takes off from the floor, hovers, and comes down on the slow glide path at
        /// descentSpeed; with the skids a hand's width (0.1 m) above the floor the sticks are held where they are (roll,
        /// pitch and yaw centred, the throttle at its sweet spot) until the touch, and then either held (cut = false) or the
        /// throttle dropped to zero at the first touch (cut = true). Flown for 4 s past the first touch.
        /// </summary>
        public static Landing Land(DroneSpec spec, MaterialRow[] materials, ulong materialsHash, bool cut, double descentSpeed = 0.5, double forward = 0)
        {
            ContactSpec probe = spec.BuildContacts(spec.Nominal, materials, materialsHash);
            double stand = Standing(probe);
            FlightSetup setup = Setup(spec, materials, materialsHash, true, new Double3(0, stand, 0));
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, new Double3(0, stand + 1, 0));
            var r = new Landing { Settled = double.NaN };
            double weight = core.Body.Mass * G;
            // Up to a metre, a hover, then the descent: the target falls at descentSpeed (and moves forward if asked).
            int k = 0;
            for (; k < 6000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            double y0 = stand + 1;
            StickSample held = default;
            bool holding = false;
            long touch = -1;
            double lowest = double.PositiveInfinity, top = 0;
            bool left = false;
            for (; k < 30000; k++)
            {
                ref readonly FlightState s = ref core.State;
                double clearance = s.Position.Y - stand;
                StickSample stick = held;
                if (!holding)
                {
                    double t = (k - 6000) * FlightCore.Dt;
                    pilot.Target = new Double3(0, y0 - descentSpeed * t, 0);
                    if (forward > 0)
                    {
                        pilot.HoldVelocity = true;
                        pilot.TargetVelocity = new Double3(0, 0, forward);
                    }
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (clearance < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        r.HeldThrottle = held.Throttle;
                        stick = held;
                    }
                }
                if (touch >= 0 && cut) stick.Throttle = 0;
                stick.HostUs = s.Step * FlightCore.StepUs;
                core.Advance(stick);

                ref readonly FlightState n = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Kind == EventKind.ContactMade || e.Kind == EventKind.ContactBroken) r.ContactEvents++;
                    if (touch < 0 && e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground)
                    {
                        touch = n.Step;
                        r.TouchSpeed = e.A;
                    }
                }
                if (touch < 0) continue;
                double t1 = (n.Step - touch) * FlightCore.Dt;
                ref readonly StepTrace tr = ref core.Trace;
                int loaded = 0;
                for (int c = 0; c < 4; c++)
                {
                    r.PeakCompression = DetMath.Max(r.PeakCompression, tr.LegCompression[c]);
                    r.PeakLegLoad = DetMath.Max(r.PeakLegLoad, tr.LegLoad[c]);
                    if (tr.LegLoad[c] > 0) loaded++;
                }
                if (loaded < 4) r.LastUnloaded = t1;
                if (loaded == 4 && double.IsNaN(r.Settled) && DetMath.Sqrt(Double3.Dot(n.Velocity, n.Velocity)) < 0.01) r.Settled = t1;
                if (loaded < 4 || DetMath.Sqrt(Double3.Dot(n.Velocity, n.Velocity)) >= 0.01) r.Settled = double.NaN;
                // The skids' lowest point over the floor: positive when every support is off the ground.
                lowest = double.PositiveInfinity;
                foreach (ContactShape sh in core.Contacts.Spec.Shapes)
                    if (sh.Role == ContactRole.Support) lowest = DetMath.Min(lowest, n.Position.Y + n.Attitude.Rotate(sh.A).Y - sh.Radius);
                if (lowest > 0 && t1 < 2)
                {
                    if (!left) r.LeaveSpeed = n.Velocity.Y;
                    left = true;
                    r.Separated = true;
                    top = DetMath.Max(top, lowest);
                }
                if (t1 >= 4) break;
            }
            r.SeparationHeight = top;
            r.Restitution = r.TouchSpeed > 0 ? r.LeaveSpeed / r.TouchSpeed : 0;
            r.PeakLegOverWeight = r.PeakLegLoad / weight;
            return r;
        }

        // ---- The stand ----------------------------------------------------------------------------------------------------

        public struct Liftoff
        {
            /// <summary>The post contacts made at rest and broken at the liftoff, each with steel (the stand's material).</summary>
            public int Made, Broken, BrokenSteel, MadeSteel;

            /// <summary>When the last post contact broke, s from the throttle's start, the throttle stick then, and the ESCs'
            /// mean duty then (Betaflight's motor output, 0–1).</summary>
            public double Time, Throttle, Duty;

            /// <summary>How far the drone moved across the posts before it left them, m; its height a second later over the
            /// post tops, m.</summary>
            public double Slide, ClimbAfter;

            /// <summary>The settled depth on the posts before the throttle, m (the arms' out-of-plane give: m·g/(n·k)).</summary>
            public double RestDepth;

            public int Tunnels, Crashes, Lost;
            public string Material;
        }

        /// <summary>
        /// ground-contact's "Liftoff from the stand": the legs-off drone set on the stand at rest (1 s to settle), armed, then
        /// a collective ramp from zero to 0.75 over 3 s with roll, pitch and yaw centred, and 1 s more.
        /// </summary>
        public static Liftoff FromStand(DroneSpec spec, MaterialRow[] materials, ulong materialsHash, double rampSeconds = 3)
        {
            Loadout off = spec.Nominal;
            off.Legs = false;
            ContactSpec contacts = spec.BuildContacts(off, materials, materialsHash);
            FlightWorld world = FlightWorld.Floor();
            Double3 foot = default;
            FlightSetup setup = Setup(spec, materials, materialsHash, false, LaunchStand.RestingOrigin(spec, contacts, foot));
            setup.World = world;
            setup.RuntimeObjects = new[] { LaunchStand.Build(spec, foot, 0, world.Query.MaterialId(spec.Stand.Material)) };
            var core = new FlightCore(setup);
            int stand = core.RuntimeIds[0];
            var r = new Liftoff { Time = double.NaN };
            int open = 0;
            Double3 start = default;
            long ramp = 1016;
            double postTop = spec.Stand.PostHeight;
            for (int k = 0; k < 1016 + (int)(rampSeconds * 1000) + 2000; k++)
            {
                ref readonly FlightState s = ref core.State;
                var stick = new StickSample { HostUs = s.Step * FlightCore.StepUs, Arm = k >= 1008 };
                if (k >= ramp) stick.Throttle = DetMath.Min(0.75, 0.75 * (k - ramp) / (rampSeconds * 1000));
                core.Advance(stick);
                ref readonly FlightState n = ref core.State;
                if (k == 1000)
                {
                    r.RestDepth = postTop + 0.003 - n.Position.Y;
                    start = n.Position;
                }
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Object != stand) continue;
                    string material = world.Query.MaterialName(e.Material);
                    if (e.Kind == EventKind.ContactMade)
                    {
                        r.Made++;
                        open++;
                        if (material == spec.Stand.Material) r.MadeSteel++;
                    }
                    if (e.Kind == EventKind.ContactBroken)
                    {
                        open--;
                        r.Material = material;
                        if (k >= ramp)
                        {
                            r.Broken++;
                            if (material == spec.Stand.Material) r.BrokenSteel++;
                        }
                        if (open == 0 && k >= ramp && double.IsNaN(r.Time))
                        {
                            r.Time = (k - ramp) * FlightCore.Dt;
                            r.Throttle = stick.Throttle;
                            Corners d = core.Trace.RotorCommand;
                            r.Duty = (d[0] + d[1] + d[2] + d[3]) / 4;
                            Double3 moved = n.Position - start;
                            r.Slide = DetMath.Sqrt(moved.X * moved.X + moved.Z * moved.Z);
                        }
                    }
                }
                if (!double.IsNaN(r.Time) && (k - ramp) * FlightCore.Dt <= r.Time + 1) r.ClimbAfter = n.Position.Y - 0.003 - postTop;
            }
            return r;
        }

        // ---- A tunnel is caught ------------------------------------------------------------------------------------------

        /// <summary>
        /// K§7.5's tunnel event: the drone as a free body (no propulsion) with its contacts, sideways at speed (+x) into a
        /// 9 mm steel rod across its path at its leg tubes' height, 0.1 m ahead of them, for 80 ms. At a walking pace the
        /// tubes' sideways bending stops the body; at 40 m/s its 6.6 kN/m cannot, the tubes pass through, and the log must
        /// say so. Returns the tunnel events.
        /// </summary>
        public static int Overdriven(DroneSpec spec, MaterialRow[] materials, ulong materialsHash, double speed, out string detail)
        {
            FlightWorld world = FlightWorld.Floor();
            const double height = 2, below = 0.215;
            double lead = 0;
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, materialsHash);
            foreach (ContactShape s in contacts.Shapes)
                if (s.Name.StartsWith("leg_", System.StringComparison.Ordinal)) lead = DetMath.Max(lead, s.A.X + s.Radius);
            var rod = new ShapeGeometry
            {
                Kind = ShapeKind.Capsule, Center = new Double3(lead + 0.1 + 0.0045, height - below, 0), AxisX = new Double3(1, 0, 0),
                AxisY = new Double3(0, 0, 1), AxisZ = new Double3(0, -1, 0), Radius = 0.0045, HalfLength = 1, Material = world.Query.MaterialId("steel"),
            };
            var setup = new FlightSetup
            {
                Seed = 1, World = world, Mass = spec.BuildMass(spec.Nominal), Contacts = contacts, StartPosition = new Double3(0, height, 0),
                StartVelocity = new Double3(speed, 0, 0), RuntimeObjects = new[] { new RuntimeObject("rod", new[] { rod }) },
            };
            var core = new FlightCore(setup);
            int tunnels = 0, made = 0;
            double depth = 0, minX = double.PositiveInfinity;
            for (int k = 0; k < 80; k++)
            {
                core.Advance(new StickSample { HostUs = k * FlightCore.StepUs });
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.ContactMade && e.Object == core.RuntimeIds[0]) made++;
                    if (e.Kind != EventKind.Tunnel) continue;
                    tunnels++;
                    depth = DetMath.Max(depth, e.A);
                }
                minX = DetMath.Min(minX, core.State.Velocity.X);
            }
            ref readonly FlightState end = ref core.State;
            detail = $"{made} rod contacts made, {tunnels} tunnel events (deepest {depth * 1e3:F1} mm past the latched plane); the body ends at x "
                + $"{end.Position.X:F3} m moving {end.Velocity.X:F2} m/s (the rod at {rod.Center.X:F3} m)";
            return tunnels;
        }

        // ---- The pole ---------------------------------------------------------------------------------------------------

        public struct PoleStrike
        {
            /// <summary>The first crash: its part, object and material names, its cause, the speed it names, m/s.</summary>
            public string Part, Object, Material;
            public CrashCause Cause;
            public double Speed;

            /// <summary>The drone's speed the step before the crash, m/s, and its height, m.</summary>
            public double FlightSpeed, Height;

            /// <summary>The impact that held it: its peak acceleration (m/s²) and impulse (N·s), from its Impact event.</summary>
            public double PeakAccel, Impulse;
            public string ImpactPart;

            /// <summary>Every touch of the pole: the drone's parts, by name, in order.</summary>
            public List<string> Touched;

            public int Crashes, Tunnels, Lost, Impacts;
        }

        /// <summary>
        /// ground-contact's "Into a pole": the legs-on drone takes off from the floor, the pilot climbs to 1.5 m, then holds
        /// 10 m/s north at that height straight into L01_straight standing 30 m ahead, and keeps the sticks there for 1.5 s
        /// past the hit.
        /// </summary>
        public static PoleStrike IntoPole(DroneSpec spec, MaterialRow[] materials, ulong materialsHash, double speed = 10)
        {
            ContactSpec probe = spec.BuildContacts(spec.Nominal, materials, materialsHash);
            double stand = Standing(probe);
            FlightWorld world = FlightWorld.Floor();
            FlightSetup setup = Setup(spec, materials, materialsHash, true, new Double3(0, stand, 0));
            setup.World = world;
            setup.RuntimeObjects = new[] { Pole(world, new Double3(0, 0, 30)) };
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, new Double3(0, 1.5, 0));
            var r = new PoleStrike { Touched = new List<string>() };
            long crashed = -1;
            double lastSpeed = 0, lastHeight = 0;
            for (int k = 0; k < 20000; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (k == 3000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                }
                lastSpeed = DetMath.Sqrt(Double3.Dot(s.Velocity, s.Velocity));
                lastHeight = s.Position.Y;
                core.Advance(pilot.Sticks(core, s.Step * FlightCore.StepUs));
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Kind == EventKind.Impact && crashed >= 0 && r.ImpactPart == null)
                    {
                        r.Impacts++;
                        r.PeakAccel = e.A;
                        r.Impulse = e.B;
                        r.ImpactPart = spec.Parts[e.Part].Name;
                    }
                    if ((e.Kind == EventKind.ContactMade || e.Kind == EventKind.PropStrike) && world.Query.ObjectName(e.Object) == "L01_straight")
                        r.Touched.Add(spec.Parts[e.Part].Name);
                    if (e.Kind != EventKind.Crash) continue;
                    r.Crashes++;
                    if (crashed >= 0) continue;
                    crashed = core.State.Step;
                    r.Part = spec.Parts[e.Part].Name;
                    r.Object = world.Query.ObjectName(e.Object);
                    r.Material = world.Query.MaterialName(e.Material);
                    r.Cause = (CrashCause)e.Index;
                    r.Speed = e.A;
                    r.FlightSpeed = lastSpeed;
                    r.Height = lastHeight;
                }
                if (crashed >= 0 && core.State.Step - crashed > 1500) break;
            }
            return r;
        }
    }
}
