using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The stems under the wash with distance (w9-village-physics 2.13, design.md "## 2.13"), built and graded, not flown (D-111:
    /// every flight flies 2.12's column until the near wake's spread and the jets' turbulence are built; FlightSetup.StemWash asks
    /// for it): the air on a stem by p7d-tether design 19's law against the hand calculation at the drone's heights, and 2.12's
    /// column failing it; the stems and the line reading one air in one flight; the stems' refresh against every step; the stem
    /// flights by default model 97's bit for bit, and changed with the wash; and the wash's grades on 2.12's straw landing, its gap
    /// against the pilot's "usually it lies flat" asserted as it stands.
    /// </summary>
    public class StemWashTests
    {
        const double R = 0.127, Hub = 0.1598, Mass = 4.735, Rho = 1.225;

        // tools/flightbench/handcalc/stem_wash.py: the downward air (m/s) and the horizontal (m/s) at a stem's point in the stem
        // scenarios' hover (4.735 kg, still air, flat ground at 0), the hubs h up; under a hub, and between the two right discs
        // (0.21 m right of the centre, outside both discs and their columns).
        static readonly (double H, bool UnderHub, double Y, double Down, double Across)[] Hand =
        {
            (2.5, true, 0.2, 3.239191, 0.194229), (2.5, true, 2.0 / 3, 7.103384, 0), (2.5, false, 0.2, 3.356345, 0.180486), (2.5, false, 2.0 / 3, 7.409726, 0),
            (1.5, true, 0.2, 4.376020, 0.093172), (1.5, true, 2.0 / 3, 12.708453, 0), (1.5, false, 0.2, 4.610882, 0.086579), (1.5, false, 2.0 / 3, 4.527737, 0),
            (1.0, true, 0.2, 12.023001, 0.037675), (1.0, true, 2.0 / 3, 19.340096, 0), (1.0, false, 0.2, 1.217112, 0.035009), (1.0, false, 2.0 / 3, -1.184809, 0),
            (0.5, true, 0.2, 19.340096, 0.001079), (0.5, false, 0.2, -7.604633, 0.001002),
            (0.3, true, 0.2, 19.340096, 0), (0.3, false, 0.2, -8.184283, 0),
        };

        // The hover's field, design 19's law (the line's: the fountain and the ground vortex on, the body left out as the hand's).
        static RotorAir Hover(double h)
        {
            double T = Mass * 9.80665 / 4;
            var air = new RotorAir(SoilScenarios.StemAir(), R, true, null, false);
            var hubs = new[] { new Double3(Hub, h, -Hub), new Double3(Hub, h, Hub), new Double3(-Hub, h, -Hub), new Double3(-Hub, h, Hub) };
            air.Begin(hubs, new Double3(0, 1, 0), new[] { T, T, T, T }, new Double3[4], new double[4]);
            return air;
        }

        // The pressure on a stem at its point, ½ρw² on the air's downward part, falls with the drone's height as the hand
        // calculation has it, and reaches the stems between the discs; 2.12's column (T/A under a disc at any height, nothing
        // between) fails it.
        [Test]
        public void TheWashOnAStemFallsWithHeight()
        {
            double T = Mass * 9.80665 / 4, column = T / (Math.PI * R * R);
            foreach (var p in Hand)
            {
                Double3 at = p.UnderHub ? new Double3(Hub, p.Y, Hub) : new Double3(0.21, p.Y, 0);
                Double3 u = Hover(p.H).At(at, 0, out _);
                double down = -u.Y, across = Math.Sqrt(u.X * u.X + u.Z * u.Z), q = down > 0 ? 0.5 * Rho * down * down : 0;
                double old = p.UnderHub ? column : 0;
                TestContext.Out.WriteLine($"hubs {p.H} m up, {(p.UnderHub ? "under a hub" : "between the discs")}, {p.Y:F3} m: down {down:F4} m/s (hand {p.Down:F4}), "
                    + $"across {across:F4} (hand {p.Across:F4}); {q:F1} Pa against 2.12's {old:F1} Pa");
                Assert.That(down, Is.EqualTo(p.Down).Within(1e-5), "the downward air, by hand");
                Assert.That(across, Is.EqualTo(p.Across).Within(1e-5), "the air across, by hand");
            }
            // The known-wrong: 2.12's column at 2.5 m under a hub is over 30 times the law's, and nothing between the discs.
            double deep = 0.5 * Rho * 3.239191 * 3.239191, between = 0.5 * Rho * 7.409726 * 7.409726;
            Assert.That(column / deep, Is.GreaterThan(30), "2.12's column under a hub 2.5 m up");
            Assert.That(between, Is.GreaterThan(30), "the merged jet between the discs, where 2.12 has nothing");
        }

        // One air: in a flight with the line hovering 2 m over the densest tall cell in the map's calm air, the stems' rotor air
        // and the line's give the same air bit for bit at every point after the step (no stem reaches the discs, so no strike
        // changes the body between their two beginnings).
        [Test]
        public void TheStemsAndTheLineReadOneAir()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MapWorld village = SlopeScenarios.Farmland();
            double y = StemScenarios.Ground(StemScenarios.Dense) + 2.0 - StemScenarios.HubOverOrigin;
            var at = new Double3(StemScenarios.Dense.X, y, StemScenarios.Dense.Z);
            FlightSetup setup = SoilScenarios.Setup(spec, new FlightWorld(village), at);
            setup.Wind = WindSetup.ForMap(village, WindLevel.Calm);
            setup.Tether = GoldenFlights.TetherFor(spec, TetherScenarios.Table());
            setup.TetherCoilPart = spec.Coil.Part;
            setup.TetherSpoolMass = spec.Coil.SpoolMass;
            setup.StemWash = true;
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, at);
            for (int k = 0; k < 16; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 });
            int compared = 0;
            for (int k = 0; k < 1500; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                if (k % 250 != 249) continue;
                RotorAir stems = core.Strikes.Wash, line = core.Tether.Rotors;
                Assert.That(stems, Is.Not.Null, "the stems read the line's table");
                double ground = StemScenarios.Ground(StemScenarios.Dense);
                for (int i = 0; i < 40; i++)
                {
                    double a = i * 0.7, r = 0.05 + 0.03 * i;
                    var p = new Double3(core.State.Position.X + r * Math.Cos(a), ground + 0.05 + 0.045 * i, core.State.Position.Z + r * Math.Sin(a));
                    Double3 u = stems.At(p, ground, out Double3 ju), v = line.At(p, ground, out Double3 jv);
                    Assert.That(u.X == v.X && u.Y == v.Y && u.Z == v.Z && ju.X == jv.X && ju.Z == jv.Z, $"step {core.State.Step}, point {i}: {u} against {v}");
                    compared++;
                }
            }
            TestContext.Out.WriteLine($"{compared} points over 6 steps, the stems' air and the line's bit for bit; the merged jet's U_H {core.Strikes.Wash.FarSpeed:F3} m/s, "
                + $"its landing {core.Strikes.Wash.FarGroundSpeed:F3} m/s");
        }

        // The stems' air is taken every 8 steps by slot (numerical): the vertical landing and the approach cut the same within the
        // stems' own count noise, 2√(n₁ + n₈) fresh, and their slices within 5 %, as at every step.
        [Test]
        public void TheRefreshHoldsTheCuts()
        {
            foreach (string kind in new[] { "vertical", "approach" })
            {
                StemScenarios.Run every = kind == "vertical" ? StemScenarios.Landing(wash: true, refresh: 1) : StemScenarios.Approach(wash: true, refresh: 1);
                StemScenarios.Run eighth = kind == "vertical" ? StemScenarios.Landing(wash: true, refresh: 8) : StemScenarios.Approach(wash: true, refresh: 8);
                double f1 = 0, f8 = 0, s1 = 0, s8 = 0;
                for (int c = 0; c < 4; c++)
                {
                    f1 += every.FreshBefore[c];
                    f8 += eighth.FreshBefore[c];
                    s1 += every.SlicedBefore[c];
                    s8 += eighth.SlicedBefore[c];
                }
                TestContext.Out.WriteLine($"{kind}: every step fresh {f1:F0}, sliced {s1:F0}, the touch at {every.Touch}; every 8 steps fresh {f8:F0}, sliced {s8:F0}, at {eighth.Touch}");
                Assert.That(Math.Abs(f1 - f8), Is.LessThanOrEqualTo(2 * Math.Sqrt(f1 + f8)), kind + ": fresh cuts within the count noise");
                Assert.That(s8, Is.EqualTo(s1).Within(5).Percent, kind + ": slices");
                Assert.That(eighth.Allocated, Is.Zero, AllocProbe.Report());
            }
        }

        // By default (no StemWash) the stems fly 2.12's column: the six stem flights are model 97's bit for bit (each step's pose,
        // rates, rotor speeds, the stems' records and the events, hashed FNV-1a, pinned from model 97's own runs). With the wash
        // they differ; a law no flight flies carries no pins.
        [Test]
        public void ByDefaultTheStemFlightsAreModel97sAndTheWashChangesThem()
        {
            var pins = new (string Name, ulong Hash)[]
            {
                ("landing", 0x4E4A92C087261306UL), ("approach", 0x1F33ED416F8BF433UL), ("creep", 0x6D26625A0D5E47D0UL),
                ("hover", 0x5687D97D870648A3UL), ("range", 0xAE31E8EC100D6742UL), ("thick", 0xCB0A2376DF0B0498UL),
            };
            foreach (bool wash in new[] { false, true })
                foreach (var (name, pin) in pins)
                {
                    ulong hash = 0xCBF29CE484222325UL;
                    Action<FlightCore> stepped = core => hash = StateHash(hash, core);
                    switch (name)
                    {
                        case "landing": StemScenarios.Landing(stepped: stepped, wash: wash); break;
                        case "approach": StemScenarios.Approach(stepped: stepped, wash: wash); break;
                        case "creep": StemScenarios.Creep(speed: 0.5, stepped: stepped, wash: wash); break;
                        case "hover": StemScenarios.Hover(stepped: stepped, wash: wash); break;
                        case "range": StemScenarios.RangeCreep(stepped: stepped, wash: wash); break;
                        default: StemScenarios.Landing(stepped: stepped, wash: wash, scales: new StemScenarios.Scales { Density = 2, Diameter = 1.5 }); break;
                    }
                    TestContext.Out.WriteLine($"{name}{(wash ? ", the wash" : "")}: 0x{hash:X16} (model 97's 0x{pin:X16})");
                    if (wash) Assert.That(hash, Is.Not.EqualTo(pin), name + ": the wash moves the stems");
                    else Assert.That(hash, Is.EqualTo(pin), name + ": model 97's stems by default");
                }
        }

        // The wash's creep (0.5 m/s at 0.6 m): the front discs cut at least the stems their leading edges meet (StemTests' hand
        // count, a floor here: the near column is R/√2 wide and the fountain rises between the discs, so stems passing under the
        // leading edge below its plane stand up into the disc behind it), each at the hand's work; the rear discs in the front's
        // track.
        [Test]
        public void UnderTheWashACreepCutsAtLeastItsLeadingEdgesStems()
        {
            StemScenarios.Run run = StemScenarios.Creep(speed: 0.5, wash: true);
            int from = 4100, to = (int)run.Steps - 1;
            double hand = 0, handWork = 0, sim = 0, simWork = 0;
            for (int c = 1; c < 4; c += 2)
            {
                hand += StemTests.Entries(run, c, from, to, out double w);
                handWork += w;
                for (int i = from; i < to; i++)
                {
                    sim += run.Fresh1[c][i];
                    simWork += run.FreshWork1[c][i];
                }
            }
            double front = run.Fresh[1] + run.Fresh[3], rear = run.Fresh[0] + run.Fresh[2];
            TestContext.Out.WriteLine($"the wash's creep, the front discs: {sim:F0} fresh cuts (the leading edges' {hand:F1} by hand), {simWork / sim * 1000:F0} mJ a cut "
                + $"(hand {handWork / hand * 1000:F0}); front {front:F0}, rear {rear:F0}; {run.Allocated} B");
            Assert.That(sim, Is.GreaterThanOrEqualTo(0.9 * hand), "at least the leading edges' stems");
            Assert.That(simWork / sim, Is.EqualTo(handWork / hand).Within(15).Percent, "a cut's work, by hand");
            Assert.That(rear, Is.LessThan(0.7 * front), "the rear discs run in the front ones' track");
            Assert.That(run.Allocated, Is.Zero, AllocProbe.Report());
        }

        // The wash's approach: the front discs cut in front of the camera at least as much as the rear behind it (2.12's column:
        // 1.3 times; under the wash every disc's outer ring meets its stems standing as it comes down).
        [Test]
        public void UnderTheWashTheApproachCutsInFrontAtLeastAsMuchAsBehind()
        {
            StemScenarios.Run run = StemScenarios.Approach(wash: true);
            double front = run.FreshBefore[1] + run.FreshBefore[3], rear = run.FreshBefore[0] + run.FreshBefore[2];
            TestContext.Out.WriteLine($"the wash's approach: fresh before the touch, front {front:F0}, rear {rear:F0} ({front / rear:F2} times); {run.Allocated} B");
            Assert.That(front, Is.GreaterThan(0), "cut in front of the camera");
            Assert.That(front, Is.GreaterThanOrEqualTo(rear), "at least as much as behind it");
            Assert.That(run.Allocated, Is.Zero, AllocProbe.Report());
        }

        // The wash's hover (0.5 m, 10 s): no uncut stem is met in the last 2 s and every episode ends (the outer rings' stubs, which
        // no column holds down, are sliced for longer as they spring back: 2.12's column clears by 5.6 s, the wash by 8.3 s).
        [Test]
        public void UnderTheWashAHoverCutsItsUncutStemsClear()
        {
            StemScenarios.Run r = StemScenarios.Hover(wash: true);
            long lastFresh = -1;
            for (int i = 0; i < r.Steps && i < r.Pitch.Length; i++)
                for (int c = 0; c < 4; c++)
                    if (r.Fresh1[c][i] > 0) lastFresh = i + 1;
            TestContext.Out.WriteLine($"the wash's hover: the last fresh cut at step {lastFresh}, the last cut at {r.LastCut} of {r.Steps}; episodes {r.Starts}/{r.Ends}; {r.Allocated} B");
            Assert.That(lastFresh, Is.LessThan(r.Steps - 2000), "no uncut stem met in the last 2 s");
            Assert.That(r.Ends, Is.EqualTo(r.Starts), "every episode ended");
            Assert.That(r.Allocated, Is.Zero, AllocProbe.Report());
        }

        // THE GAP (P12b, D-111), asserted as it stands so that nobody reads it as met: the pilot, "Usually it lies flat because of
        // prop wash power, but not always". Under 2.12's column (what flies) most stems under the discs fold in the vertical
        // landing's 1.9 m hover; under the wash alone most stand (the short dry culms under the merged jet's 10–35 Pa, the tall
        // ones in each disc's outer ring where design 19 has no column), and the no-fold known-wrong cuts less than twice as many.
        // Its missing mechanisms: the near wake's contraction and spread, the jets' turbulence on near-upright stems, the stand's
        // contact (design.md 2.13). When they are built this test is to fail, and be turned round.
        [Test]
        public void TheGapUnderTheWashMostStemsStand()
        {
            StemScenarios.Run column = StemScenarios.Landing(), wash = StemScenarios.Landing(wash: true), none = StemScenarios.Landing(wash: true, knownWrong: true);
            const int hover = 4000; // the hover's end, the front right hub 1.85 m up
            double folded = column.Folded[hover] / column.Under[hover], washFolded = wash.Folded[hover] / wash.Under[hover];
            double fresh = 0, noFold = 0;
            for (int c = 0; c < 4; c++)
            {
                fresh += wash.FreshBefore[c];
                noFold += none.FreshBefore[c];
            }
            TestContext.Out.WriteLine($"the hover's end: under the discs folded past 45°, 2.12's column {folded * 100:F0} % ({column.FoldedTall[hover]} of {column.UnderTall[hover]} tall), "
                + $"the wash {washFolded * 100:F0} % ({wash.FoldedTall[hover]} of {wash.UnderTall[hover]} tall); the wash's fresh cuts before the touch {fresh:F0}, "
                + $"the no-fold known-wrong's {noFold:F0}");
            Assert.That(folded, Is.GreaterThan(0.5), "2.12's column folds most (what flies)");
            Assert.That(washFolded, Is.LessThan(0.5), "THE GAP: under the wash alone most stems under the discs stand (P12b not met)");
            Assert.That(noFold, Is.LessThan(2 * fresh), "THE GAP: the no-fold known-wrong cuts less than twice the wash's");
        }

        static ulong Mix(ulong h, double x)
        {
            ulong b = (ulong)BitConverter.DoubleToInt64Bits(x);
            for (int i = 0; i < 8; i++)
            {
                h = unchecked((h ^ (b & 0xFF)) * 0x100000001B3UL);
                b >>= 8;
            }
            return h;
        }

        // The step's pose, velocity, attitude, rates and rotor speeds, each rotor's stems' fresh cuts, slices, work and stems over
        // its plane, and the step's events (kind, index, A, B, C).
        static ulong StateHash(ulong h, FlightCore core)
        {
            ref readonly FlightState s = ref core.State;
            h = Mix(h, s.Step);
            h = Mix(h, s.Position.X);
            h = Mix(h, s.Position.Y);
            h = Mix(h, s.Position.Z);
            h = Mix(h, s.Velocity.X);
            h = Mix(h, s.Velocity.Y);
            h = Mix(h, s.Velocity.Z);
            h = Mix(h, s.Attitude.X);
            h = Mix(h, s.Attitude.Y);
            h = Mix(h, s.Attitude.Z);
            h = Mix(h, s.Attitude.W);
            h = Mix(h, s.BodyRate.X);
            h = Mix(h, s.BodyRate.Y);
            h = Mix(h, s.BodyRate.Z);
            for (int c = 0; c < 4; c++)
            {
                h = Mix(h, s.RotorSpeed[c]);
                PropStrikes.StemRecord r = core.Strikes.StemLast[c];
                h = Mix(h, r.Fresh);
                h = Mix(h, r.Sliced);
                h = Mix(h, r.Work);
                h = Mix(h, r.Over);
            }
            for (int i = 0; i < core.Events.Count; i++)
            {
                FlightEvent e = core.Events[i];
                h = Mix(h, (double)e.Kind);
                h = Mix(h, e.Index);
                h = Mix(h, e.A);
                h = Mix(h, e.B);
                h = Mix(h, e.C);
            }
            return h;
        }
    }
}
