using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The pieces in the air (w16-plants-in-the-air 1.4, PlantPieces), their CPU side on the farmland, flown on published poses as
    /// PlantField's coarse helper flies them (the coarse wash grid ticked from the poses, the pieces after each tick) with the render
    /// thread's cuts given once a field:
    /// - the dust lifts from dry bare loam (the tilled field) as the drone comes lower and its rotors push harder, and none wet;
    /// - the cut pieces leave the blade at its speed there, slow to the air and settle;
    /// - the same flight gives the same pieces, tick for tick (a replay draws them), and the counts stay in their slots.
    /// </summary>
    public sealed class PlantPiecesTests
    {
        const double Mass = 4.735; // the stem scenarios' hover (PlantWashTests)
        const double Omega = 520;  // a hover's rotor speed, rad/s (assumed for the cuts' spin; the thrust is the hover's)

        // The tilled field's bare loam west of the fields (maps/farmland's classes), a meadow beside it.
        static readonly Double3 Tilled = new Double3(430, 0, 528), Meadow = new Double3(503, 0, 540);

        sealed class Rig
        {
            public MapWorld Map;
            public WindField Wind;
            public PlantWash Coarse;
            public PlantPieces Pieces;
            public PoseBuffer Poses;
            public Double3 Body;
            public double Thrust;
            public readonly List<ulong> Digests = new List<ulong>();
            public readonly List<string> Lines = new List<string>();
        }

        static MapWorld _map;
        static SurfaceTable _surfaces;
        static TetherTable _table;
        static PropulsionSpec _propulsion;

        static Rig Make(Double3 where, double hover, Moisture moisture, double thrustShare = 1, WindLevel level = WindLevel.Calm)
        {
            string root = AppData.Root;
            _map ??= MapWorld.Load(root, FlightWorlds.FarmlandMap);
            _surfaces ??= SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath)));
            _table ??= TetherTable.Parse(File.ReadAllText(AppData.File(TetherTable.RelativePath)));
            _propulsion ??= DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile))).BuildPropulsion();
            var world = new FlightWorld(_map);
            var wind = new WindField(WindSetup.ForMap(_map, level), new FlightRandom(0x16B4), world.Hills);
            double air = _table.Number("fiber.air_density_kg_m3");
            PlantMechanics[] bands = PlantBands.Mechanics(k => _surfaces.Cover[(int)k].Set(moisture), air);
            var grass = PlantBands.Washed.Select(b => bands[b]).ToArray();
            var motors = new Double3[4];
            var clockwise = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                motors[i] = _propulsion.Motors[i].Position;
                clockwise[i] = _propulsion.Motors[i].Clockwise;
            }
            double radius = _propulsion.Props.Diameter / 2;
            var coarse = new PlantWash(_table, radius, motors, _map, new PlantAir(wind), grass, _surfaces, moisture, PlantField.LevelSize[1], PlantField.LevelCell[1]);
            var pieces = new PlantPieces(_map, wind, _surfaces, moisture, k => _surfaces.Cover[(int)k].Set(moisture), motors, clockwise, air);
            var under = new GroundSample[1];
            _map.SampleGround(new[] { new XZ(where.X, where.Z) }, under);
            var rig = new Rig
            {
                Map = _map, Wind = wind, Coarse = coarse, Pieces = pieces, Poses = new PoseBuffer(),
                Body = new Double3(where.X, under[0].Height + hover, where.Z), Thrust = thrustShare * Mass * 9.80665 / 4,
            };
            rig.Poses.Begin(0, Pose(rig, 0), default);
            return rig;
        }

        static PoseFrame Pose(Rig rig, long step) => new PoseFrame
        {
            Step = step, Position = rig.Body, Attitude = new Quat(0, 0, 0, 1), Armed = true,
            RotorThrust = new Corners { RearRight = rig.Thrust, FrontRight = rig.Thrust, RearLeft = rig.Thrust, FrontLeft = rig.Thrust },
            RotorSpeed = new Corners { RearRight = Omega, FrontRight = Omega, RearLeft = Omega, FrontLeft = Omega },
        };

        // The flight: a pose a step, the signals each field (cuts from the function), the render thread's frame every 10 steps (its
        // cuts given first, as FarmlandPlants does), the coarse helper's ticks right after it (PlantField without threads).
        static void Fly(Rig rig, double seconds, Func<long, FeedSignals> signals = null)
        {
            long until = (long)Math.Round(seconds / FlightCore.Dt), from = rig.Poses.LatestStep + 1;
            FeedSignals s = default;
            for (long step = from; step <= until; step++)
            {
                if (step % FlightCore.FieldSteps == 0) s = signals != null ? signals(step) : default;
                s.Step = step;
                rig.Poses.Publish(Pose(rig, step), s);
                if (step % 10 != 0) continue;
                rig.Pieces.Field(s, rig.Poses);
                long before = rig.Pieces.LastTick;
                rig.Pieces.Advance(rig.Coarse, rig.Poses);
                if (rig.Pieces.LastTick != before) rig.Digests.Add(rig.Pieces.Digest);
            }
        }

        static string Dust(Rig rig)
        {
            var heights = new List<double>();
            double widest = 0, tallest = 0;
            for (int i = PlantPieces.DustFirst; i < PlantPieces.DustFirst + PlantPieces.DustSlots; i++)
            {
                if (rig.Pieces.KindOf(i) != PlantPieces.Kind.Dust) continue;
                heights.Add(rig.Pieces.Position(i).Y - (rig.Body.Y));
                widest = Math.Max(widest, rig.Pieces.SigmaH(i));
                tallest = Math.Max(tallest, rig.Pieces.SigmaV(i));
            }
            heights.Sort();
            string median = heights.Count > 0 ? (heights[heights.Count / 2]).ToString("0.00") : "-";
            return $"{rig.Pieces.DustEmitted * 1000:0.00} g emitted, {rig.Pieces.Live(PlantPieces.Kind.Dust)} puffs, {rig.Pieces.DustMass * 1000:0.00} g aloft, " +
                   $"centres {median} m from the drone's height (median), σ_h ≤ {widest:0.00} m, σ_v ≤ {tallest:0.00} m";
        }

        [Test, Timeout(600000)]
        public void DustLiftsFromDryLoamAsTheDroneComesLowerAndPushesHarder()
        {
            var emitted = new Dictionary<string, double>();
            foreach ((string name, double hover, double share) in new[] { ("4 m", 4.0, 1.0), ("2 m", 2.0, 1.0), ("1 m", 1.0, 1.0), ("0.5 m", 0.5, 1.0), ("1 m, 1.3 × the hover's thrust", 1.0, 1.3) })
            {
                Rig rig = Make(Tilled, hover, Moisture.Dry, share);
                Fly(rig, 3);
                emitted[name] = rig.Pieces.DustEmitted;
                TestContext.WriteLine($"tilled, dry, {name}, 3 s: {Dust(rig)}");
            }
            TestContext.WriteLine($"the dust's mass extinction {Make(Tilled, 1, Moisture.Dry).Pieces.DustExtinction:0} m²/kg, its settling speed {Make(Tilled, 1, Moisture.Dry).Pieces.DustSettling * 1000:0.00} mm/s");
            Assert.That(emitted["0.5 m"], Is.GreaterThan(0), "a 0.5 m hover over dry tilled loam lifts dust");
            Assert.That(emitted["0.5 m"], Is.GreaterThan(emitted["1 m"]), "lower lifts more");
            Assert.That(emitted["1 m"], Is.GreaterThanOrEqualTo(emitted["2 m"]), "lower lifts more");
            Assert.That(emitted["2 m"], Is.GreaterThanOrEqualTo(emitted["4 m"]), "lower lifts more");
            Assert.That(emitted["1 m, 1.3 × the hover's thrust"], Is.GreaterThan(emitted["1 m"]), "more thrust lifts more");
        }

        [Test, Timeout(600000)]
        public void WetGroundLiftsNoDust()
        {
            foreach (Moisture m in new[] { Moisture.Wet, Moisture.Saturated })
            {
                Rig rig = Make(Tilled, 0.5, m);
                Fly(rig, 2);
                TestContext.WriteLine($"tilled, {m}, 0.5 m, 2 s: {Dust(rig)}");
                Assert.That(rig.Pieces.DustEmitted, Is.EqualTo(0), m + ": no dust");
                Assert.That(rig.Pieces.Live(PlantPieces.Kind.Dust), Is.EqualTo(0));
            }
        }

        [Test, Timeout(600000)]
        public void CutPiecesLeaveTheBladeAtItsSpeedThenSettle()
        {
            Rig rig = Make(Meadow, 0.5, Moisture.Dry);
            const double Radius = 0.09, Length = 0.12;
            // Three stems cut by the front-left rotor in the field that ends at 1.0 s, none after.
            FeedSignals Cuts(long step)
            {
                FeedSignals s = default;
                if (step == 1000)
                {
                    s.StemCuts = new Corners { FrontLeft = 3 };
                    s.StemPieceLength = new Corners { FrontLeft = Length };
                    s.StemCutRadius = new Corners { FrontLeft = Radius };
                    s.StemBladeAngle = new Corners { FrontLeft = 0.4 };
                }
                return s;
            }
            Fly(rig, 1.0, Cuts);
            int[] slots = Enumerable.Range(PlantPieces.CutFirst, PlantPieces.CutSlots).Where(i => rig.Pieces.KindOf(i) == PlantPieces.Kind.Cut).ToArray();
            Assert.That(slots.Length, Is.EqualTo(3), "three pieces off the front-left disc");
            Assert.That(rig.Pieces.CutsBorn, Is.EqualTo(3));
            Double3 hub = rig.Body + _propulsion.Motors[(int)CornerIndex.FrontLeft].Position;
            foreach (int i in slots)
            {
                Double3 v = rig.Pieces.Velocity(i), x = rig.Pieces.Position(i);
                TestContext.WriteLine($"at 1.0 s: {(x - hub).Length():0.000} m from the hub, {v.Length():0.0} m/s");
            }
            var speeds = new List<string>();
            foreach (double t in new[] { 1.04, 1.2, 1.6, 3.0, 6.0 })
            {
                Fly(rig, t, Cuts);
                double fastest = 0, lowest = double.MaxValue;
                int lying = 0, alive = 0;
                foreach (int i in slots)
                {
                    if (rig.Pieces.KindOf(i) != PlantPieces.Kind.Cut) continue;
                    alive++;
                    fastest = Math.Max(fastest, rig.Pieces.Velocity(i).Length());
                    lowest = Math.Min(lowest, rig.Pieces.Position(i).Y);
                    if (rig.Pieces.Lying(i)) lying++;
                }
                speeds.Add($"{t:0.00} s: {alive} alive, {lying} lying, fastest {fastest:0.0} m/s" +
                           (alive > 0 ? $", lowest {lowest - (rig.Body.Y - 0.5):0.00} m over the ground" : ""));
            }
            foreach (string line in speeds) TestContext.WriteLine(line);
            double settling = PlantPieces.StemSettling(_surfaces.Cover[(int)CoverKind.GrassBlade].Set(Moisture.Dry), _table.Number("fiber.air_density_kg_m3"));
            TestContext.WriteLine($"a green culm's settling speed {settling:0.00} m/s (derived), a dry culm's " +
                $"{PlantPieces.StemSettling(_surfaces.Cover[(int)CoverKind.DryCulm].Set(Moisture.Dry), _table.Number("fiber.air_density_kg_m3")):0.00}");
        }

        [Test, Timeout(600000)]
        public void TheSameFlightDrawsTheSamePieces()
        {
            FeedSignals Cuts(long step)
            {
                FeedSignals s = default;
                double n = (step / FlightCore.FieldSteps) % 3;
                s.StemCuts = new Corners { FrontLeft = n, FrontRight = 1.5 };
                s.StemPieceLength = new Corners { FrontLeft = 0.1, FrontRight = 0.3 };
                s.StemCutRadius = new Corners { FrontLeft = 0.08, FrontRight = 0.1 };
                s.StemBladeAngle = new Corners { FrontLeft = step * 0.001, FrontRight = 1 };
                return s;
            }
            Rig a = Make(Tilled, 0.5, Moisture.Dry, 1, WindLevel.Windy), b = Make(Tilled, 0.5, Moisture.Dry, 1, WindLevel.Windy);
            Fly(a, 6, Cuts);
            Fly(b, 6, Cuts);
            TestContext.WriteLine($"{a.Digests.Count} ticks; dust {Dust(a)}; {a.Pieces.CutsBorn} cut pieces born, {a.Pieces.Live(PlantPieces.Kind.Cut)} alive");
            Assert.That(a.Digests.Count, Is.GreaterThan(100));
            Assert.That(b.Digests, Is.EqualTo(a.Digests), "the same pieces, tick for tick");
            Assert.That(a.Pieces.Live(PlantPieces.Kind.Dust), Is.LessThanOrEqualTo(PlantPieces.DustSlots));
            Assert.That(a.Pieces.Live(PlantPieces.Kind.Cut), Is.LessThanOrEqualTo(PlantPieces.CutSlots));
            Assert.That(a.Pieces.MissedFields, Is.EqualTo(0));
            Assert.That(a.Pieces.LateTicks, Is.EqualTo(0));
        }

        enum CornerIndex { RearRight = 0, FrontRight = 1, RearLeft = 2, FrontLeft = 3 }
    }
}
