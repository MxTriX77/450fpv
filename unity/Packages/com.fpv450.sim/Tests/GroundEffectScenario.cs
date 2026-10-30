using System;
using System.Text;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The stub's floor (FloorWorld at y = 0) with a hole band across it, x in [XMin, XMax]: there SampleGround reports a
    /// hole, and neither Raycast nor StaticContacts sees the ground (IWorldQuery's rule for a hole). Runtime objects are the
    /// floor's. With sampleRise, SampleGround's ground stands that much above the floor the ray sees (a mat's top or a relief
    /// the terrain's triangles lack, PHY-08). A test world for airframe-effects' "The cushion and the hole" until the test
    /// range's holes land.
    /// </summary>
    public sealed class HoleBandWorld : IWorldQuery
    {
        readonly FloorWorld _floor = new FloorWorld(0, "belt_bare", "steel", "timber");
        readonly double _xMin, _xMax;

        readonly double _rise;

        public HoleBandWorld(double xMin, double xMax, double sampleRise = 0)
        {
            _xMin = xMin;
            _xMax = xMax;
            _rise = sampleRise;
            string text = "HoleBand\n" + BitConverter.DoubleToInt64Bits(xMin).ToString("X16") + "\n" + BitConverter.DoubleToInt64Bits(xMax).ToString("X16")
                + "\n" + BitConverter.DoubleToInt64Bits(sampleRise).ToString("X16");
            ContentHash = WorldHash.HashFile(_floor.ContentHash, Encoding.UTF8.GetBytes(text), true);
        }

        bool InHole(double x) => x >= _xMin && x <= _xMax;

        public ulong ContentHash { get; }
        public int QueryVersion => _floor.QueryVersion;

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)
        {
            _floor.SampleGround(points, results);
            for (int i = 0; i < points.Length; i++)
            {
                results[i].Height += _rise;
                if (InHole(points[i].X))
                    results[i] = new GroundSample { Height = double.NegativeInfinity, Normal = new Double3(0, 1, 0), Flags = GroundFlags.Hole };
            }
        }

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)
        {
            int n = _floor.StaticContacts(previous, current, margin, results), kept = 0;
            for (int i = 0; i < Math.Min(n, results.Length); i++)
                if (!(results[i].Object == WorldIds.Ground && InHole(results[i].Point.X)))
                    results[kept++] = results[i];
            return n - (Math.Min(n, results.Length) - kept);
        }

        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits)
        {
            _floor.Raycast(rays, maxDistance, hits);
            for (int i = 0; i < rays.Length; i++)
                if (hits[i].Object == WorldIds.Ground && hits[i].Surface != WorldIds.NoSurface && InHole(hits[i].Point.X))
                    hits[i] = new RayHit { Distance = double.PositiveInfinity, Object = WorldIds.Ground };
        }

        public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _floor.Geometry(obj, shape, out geometry);
        public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _floor.AddObject(name, shapes);
        public void ResetRuntimeObjects() => _floor.ResetRuntimeObjects();
        public string ObjectName(int obj) => _floor.ObjectName(obj);
        public string MaterialName(ushort material) => _floor.MaterialName(material);
        public ushort MaterialId(string name) => _floor.MaterialId(name);
        public string SurfaceName(byte surface) => _floor.SurfaceName(surface);
    }

    /// <summary>
    /// airframe-effects' "Ground effect, ceiling effect and recirculation per rotor" (p7b-airframe 1.3) on the stub:
    /// - The cushion and the hole: the repository's drone level, its hubs Height diameters above the floor, the hole band
    ///   under its left rotors; each rotor's thrust at a fixed speed (the props' reference rpm) with the ground effect over
    ///   the same rotor's in free air. The floor's rotors gain Cheeseman and Bennett's 1/(1 − (R/4z)²); the hole's equal free air.
    /// - The ceiling: the same drone 100 m up under a runtime box roof Height diameters above its right rotors' hubs: they
    ///   gain Hsiao and Chirarattananon's γ^(2/3); the left ones, in the open, equal free air.
    /// - The close hover: the drone on its legs, still air, no asymmetry (so nothing else stirs it), held by the closed-loop
    ///   pilot with its skids HoverLift above its standing height; the body rate over each half second of the hold, with the
    ///   recirculation and with the known-wrong model without it (RecirculationSigma 0), and the same hover HighLift up.
    /// The suite asserts it (GroundEffectTests); tools/flightbench --scenarios prints it.
    /// </summary>
    public static class GroundEffectScenario
    {
        public const double Height = 0.3, HoverLift = 0.1, HighLift = 3, SettleSeconds = 10, HoldSeconds = 3, Block = 0.5;
        public const ulong Seed = 0x450F;

        /// <summary>The pilot's throttle at the target height: the nominal drone's hover stick out of ground effect, 0.482
        /// (measured: SimPilot's 0.5 held it 0.175 m above its target), so the close hover sits at its target.</summary>
        public const double HoverThrottle = 0.482;
        const double TwoPi = 6.283185307179586;

        public struct Surfaces
        {
            /// <summary>The rotors over the surface and away from it (bit 1 &lt;&lt; corner).</summary>
            public int Over, Away;
            /// <summary>The published ratio at Height, the least and largest gain of the rotors over the surface, and the
            /// largest |gain − 1| of the rotors away from it; their found distances over D.</summary>
            public double Expected, LeastOver, MostOver, Away1, DistanceOver, DistanceAway;
        }

        static double Speed(DroneSpec spec) => spec.Props.ReferenceRpm * (TwoPi / 60);

        // Each rotor's thrust at the fixed speed with the ground effect over its free-air thrust, at the state's pose in world.
        static void Gains(DroneSpec spec, FlightWorld world, in FlightState s, out Corners gain, out Corners below, out Corners above)
        {
            GroundEffectSpec ground = spec.BuildGroundEffect();
            var with = new Propulsion(spec.BuildPropulsion(), null, null, default, ground, new SeedStream("ground.recirc", 1));
            var free = new Propulsion(spec.BuildPropulsion());
            var surfaces = new RotorSurfaces(with, ground.Reach * spec.Props.Diameter / 2);
            surfaces.Find(world, s, out below, out above);
            with.SetSurfaces(below, above);
            with.BeginStep(s);
            with.BeginStep(s); // the second sees v_h of the first's thrust (in still air the fade is 1 either way)
            free.BeginStep(s);
            gain = default;
            for (int i = 0; i < 4; i++) gain[i] = with.Thrust(i) / free.Thrust(i);
        }

        static FlightState Level(DroneSpec spec, double hubHeight)
        {
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, hubHeight - spec.BuildPropulsion().Motors[0].Position.Y, 0) };
            double w = Speed(spec);
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = w;
            return s;
        }

        static Surfaces Sort(DroneSpec spec, in Corners gain, in Corners distance, Func<int, bool> over, double expected)
        {
            double d = spec.Props.Diameter;
            var r = new Surfaces { Expected = expected, LeastOver = double.PositiveInfinity, DistanceAway = double.PositiveInfinity };
            for (int i = 0; i < 4; i++)
            {
                if (over(i))
                {
                    r.Over |= 1 << i;
                    r.LeastOver = Math.Min(r.LeastOver, gain[i]);
                    r.MostOver = Math.Max(r.MostOver, gain[i]);
                    r.DistanceOver = Math.Max(r.DistanceOver, distance[i] / d);
                }
                else
                {
                    r.Away |= 1 << i;
                    r.Away1 = Math.Max(r.Away1, Math.Abs(gain[i] - 1));
                    r.DistanceAway = Math.Min(r.DistanceAway, distance[i] / d);
                }
            }
            return r;
        }

        /// <summary>The cushion and the hole: the hole band under x &lt; 0 (the left rotors), the hubs Height·D above the floor.</summary>
        public static Surfaces Cushion(DroneSpec spec)
        {
            double d = spec.Props.Diameter, zr = 2 * Height;
            FlightState s = Level(spec, Height * d);
            var world = new FlightWorld(new HoleBandWorld(-5, 0));
            Gains(spec, world, s, out Corners gain, out Corners below, out _);
            PropulsionSpec p = spec.BuildPropulsion();
            return Sort(spec, gain, below, i => p.Motors[i].Position.X > 0, 1 / (1 - 1 / (16 * zr * zr)));
        }

        /// <summary>The ceiling: 100 m up, a runtime timber box roof over x in [0, 2] (the right rotors), its underside Height·D
        /// above the hubs.</summary>
        public static Surfaces Ceiling(DroneSpec spec)
        {
            double d = spec.Props.Diameter, zr = 2 * Height, hubs = 100;
            FlightState s = Level(spec, hubs);
            FlightWorld world = FlightWorld.Floor();
            world.Query.ResetRuntimeObjects();
            world.Query.AddObject("roof", new[]
            {
                new ShapeGeometry
                {
                    Kind = ShapeKind.Box, Center = new Double3(1, hubs + Height * d + 0.05, 0), AxisX = new Double3(1, 0, 0),
                    AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(1, 0.05, 2),
                    Material = world.Query.MaterialId("timber"),
                },
            });
            Gains(spec, world, s, out Corners gain, out _, out Corners above);
            PropulsionSpec p = spec.BuildPropulsion();
            GroundEffectSpec g = spec.BuildGroundEffect();
            double gamma = g.Gamma(1 / zr);
            return Sort(spec, gain, above, i => p.Motors[i].Position.X > 0, Math.Pow(gamma, 2.0 / 3));
        }

        public struct Hover
        {
            /// <summary>The rotors' height over R during the hold, least and most; the least and largest body rate RMS of the
            /// hold's half-second blocks, rad/s; the largest re-ingested inflow over v_h; the skids' clearance, m.</summary>
            public double LeastHeight, MostHeight, LeastRate, MostRate, MostWash, Clearance;
            public int Blocks;
            public bool Crashed;
        }

        public static FlightSetup HoverSetup(DroneSpec spec, bool recirculation, out double stand)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, new Double3(0, stand, 0), Seed);
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            if (!recirculation) setup.GroundEffect.RecirculationSigma = 0;
            return setup;
        }

        /// <summary>Flies the close hover (or HighLift up); onStep sees each step after it ran.</summary>
        public static Hover FlyHover(DroneSpec spec, bool recirculation, double lift, Action<FlightCore, StickSample> onStep = null)
        {
            FlightSetup setup = HoverSetup(spec, recirculation, out double stand);
            var core = new FlightCore(setup);
            return FlyHover(core, spec, stand, lift, onStep);
        }

        public static Hover FlyHover(FlightCore core, DroneSpec spec, double stand, double lift, Action<FlightCore, StickSample> onStep = null)
        {
            var pilot = new SimPilot(core.Controller, new Double3(0, stand + lift, 0)) { ThrottleBase = HoverThrottle };
            double r = spec.Props.Diameter / 2;
            int settle = (int)(SettleSeconds / FlightCore.Dt), hold = (int)(HoldSeconds / FlightCore.Dt), block = (int)(Block / FlightCore.Dt);
            var h = new Hover { LeastHeight = double.PositiveInfinity, LeastRate = double.PositiveInfinity };
            double sum = 0, clear = 0;
            for (int k = 0; k < settle + hold; k++)
            {
                StickSample stick = k < 16
                    ? new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 }
                    : pilot.Sticks(core, k * FlightCore.StepUs);
                core.Advance(stick);
                onStep?.Invoke(core, stick);
                if (k < settle) continue;
                ref readonly FlightState s = ref core.State;
                for (int i = 0; i < 4; i++)
                {
                    h.LeastHeight = Math.Min(h.LeastHeight, core.Trace.GroundDistance[i] / r);
                    h.MostHeight = Math.Max(h.MostHeight, core.Trace.GroundDistance[i] / r);
                    h.MostWash = Math.Max(h.MostWash, Math.Abs(s.WashInflow[i]));
                }
                Double3 w = s.BodyRate;
                sum += Double3.Dot(w, w);
                clear += s.Position.Y - stand;
                if ((k - settle + 1) % block != 0) continue;
                double rms = Math.Sqrt(sum / block);
                h.LeastRate = Math.Min(h.LeastRate, rms);
                h.MostRate = Math.Max(h.MostRate, rms);
                h.Blocks++;
                sum = 0;
            }
            h.Clearance = clear / hold;
            h.Crashed = core.State.Crashed;
            return h;
        }

        /// <summary>The cushion within 2 % of Cheeseman and Bennett's ratio (or the ceiling within 2 % of its published one) on
        /// every rotor over the surface, and the rotors away from it at their free-air thrust, bit for bit.</summary>
        public static bool Within(in Surfaces s) =>
            s.Over != 0 && s.Away != 0 && Math.Abs(s.LeastOver / s.Expected - 1) <= 0.02 && Math.Abs(s.MostOver / s.Expected - 1) <= 0.02 && s.Away1 == 0;

        /// <summary>The close hover's rate floor, rad/s: every block of the close hover above it (never still), the known-wrong
        /// model's and the high hover's below a tenth of it (the pilot's own settling, about 1e-5).</summary>
        public const double StillFloor = 1e-3;

        public static bool NeverStill(in Hover close, in Hover wrong, in Hover high) =>
            !close.Crashed && close.Blocks > 0 && close.LeastRate >= StillFloor && wrong.MostRate < StillFloor / 10 && high.MostRate < StillFloor / 10;
    }
}
