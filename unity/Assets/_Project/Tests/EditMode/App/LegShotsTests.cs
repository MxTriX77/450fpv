using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Tests.App
{
    // p7g-legs 1.5: the smoke test's leg flights (LegShots) as the player flies them, on the test range at the houses' start.
    public sealed class LegShotsTests
    {
        static SessionInputs _inputs;

        [OneTimeSetUp]
        public void Load()
        {
            DroneSpec drone = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong hash);
            _inputs = new SessionInputs { Drone = drone, Materials = materials, MaterialsHash = hash, Settings = FcSettingsStore.PlayerDefault };
            _inputs.Surfaces = SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath)));
            _inputs.Tether = TetherTable.Parse(File.ReadAllText(AppData.File(TetherTable.RelativePath)));
            FlightWorlds.TestRange(AppData.Root).ApplyTo(_inputs);
            FlightWorld world = _inputs.World();
            _inputs.World = () => world;
            _inputs.Choices = new FlightChoices { StartPoint = FlightWorlds.HousesStart, Legs = true, Payload = 1.0, Wind = WindLevel.Calm, Rain = Rain.Sunny, TimeOfDay = 12 };
            SessionSetup.Prepare(drone);
        }

        static FlightCore Fly(LegShots.Case c, int steps)
        {
            _inputs.Adjust = s => LegShots.Apply(c, s);
            var core = new FlightCore(SessionSetup.Build(_inputs, c.Seed, out _, out _));
            for (int k = 0; k < steps; k++) core.Advance(StickSample.Neutral);
            return core;
        }

        [Test]
        public void EachFlightShowsItsLeg()
        {
            foreach (LegShots.Case c in LegShots.Cases)
            {
                FlightCore core = Fly(c, (int)(c.ShotS * 1000));
                core.GetPose(out PoseFrame pose);
                TestContext.Out.WriteLine(c.Name + ": " + LegShots.Describe(pose));
                Assert.That(LegShots.Shows(c, pose), c.Name + ": " + LegShots.Describe(pose));
            }
        }
    }
}
