using System;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics' aero basics (PHB-02, design 6) on the repository's drone: a crosswind leans it into the wind by
    // the derived 0.95–2.6° per m/s (CrosswindScenario), the drag its silhouettes and the rotors' in-plane drag make
    // in a gust, the rotors' gyroscopic moment, and the air at the camera for the feed.
    public sealed class AeroTests
    {
        const double G = 9.80665;

        static DroneSpec Drone => RepositoryDrone.Spec();

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        static string Describe(CrosswindScenario.Result r) =>
            $"lean {F(r.LeanDeg, "0.00")}° ({F(r.LeanPerMs, "0.000")}° per m/s of {F(r.WindSpeed, "0.00")} m/s), {F(r.LeanOffWindDeg, "0.0")}° off the "
            + $"wind's bearing, the air's force {F(r.AirOverWeight, "0.0000")} of the weight (tan of the lean {F(Math.Tan(r.LeanDeg * Math.PI / 180), "0.0000")}), "
            + $"position error {F(r.MeanError, "0.000")} m mean, {F(r.MaxError, "0.000")} m max, drift {F(r.Drift, "0.0000")} m/s";

        [Test]
        public void ACrosswindLeansTheDrone()
        {
            // A steady 5 m/s from each side of the compass: into the wind, inside the band, holding the point.
            foreach (double from in new[] { 270.0, 0, 135 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(Drone, WindSetup.SteadyFrom(from, 5)));
                TestContext.Out.WriteLine($"5 m/s from {F(from, "0")}°: {Describe(r)}");
                Assert.That(r.LeanPerMs, Is.InRange(0.95, 2.6), "K§6.5: 0.95–2.6° per m/s of crosswind");
                Assert.That(r.LeanOffWindDeg, Is.LessThan(5), "the lean follows the wind's direction");
                Assert.That(r.Drift, Is.LessThan(0.05), "the pilot holds the point");
                Assert.That(r.MeanError, Is.LessThan(0.5));
                Assert.That(Math.Tan(r.LeanDeg * Math.PI / 180), Is.EqualTo(r.AirOverWeight).Within(0.05 * r.AirOverWeight),
                    "the lean is the force balance: the thrust's tilt carries the air's push");
            }

            // The known-wrong: no drag at all, and the body's alone (no rotor in-plane drag), lean far too little.
            CrosswindScenario.Result none = CrosswindScenario.Hold(CrosswindScenario.Setup(Drone, WindSetup.SteadyFrom(270, 5), drag: false, rotorDrag: false));
            CrosswindScenario.Result body = CrosswindScenario.Hold(CrosswindScenario.Setup(Drone, WindSetup.SteadyFrom(270, 5), rotorDrag: false));
            TestContext.Out.WriteLine($"known-wrong, no drag: {Describe(none)}");
            TestContext.Out.WriteLine($"known-wrong, the body's drag alone: {Describe(body)}");
            Assert.That(none.LeanPerMs, Is.LessThan(0.95));
            Assert.That(body.LeanPerMs, Is.LessThan(0.95));
        }

        // The stand-in's turbulent Severe wind: the mean lean over the measured window per m/s of the mean wind there.
        [Test]
        public void ASevereWindLeansTheDroneByItsMean()
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                CrosswindScenario.Result r = CrosswindScenario.Hold(CrosswindScenario.Setup(Drone, WindSetup.Of(WindLevel.Severe), seed));
                TestContext.Out.WriteLine($"Severe, seed {seed}: {Describe(r)}");
                Assert.That(r.LeanPerMs, Is.InRange(0.95, 2.6));
                Assert.That(r.LeanOffWindDeg, Is.LessThan(10));
            }
        }

        // The air's push on the drone holding its point in a steady wind, against drone.json's numbers in System.Math:
        // the rotors' in-plane drag at their speeds, the body's silhouettes at its centre of pressure, the coil's
        // cylinder at its centre, each on its own point's velocity through the air in body axes.
        [Test]
        public void TheAirsPushIsTheRotorsAndTheSilhouettes()
        {
            DroneSpec d = Drone;
            FlightSetup setup = CrosswindScenario.Setup(d, WindSetup.SteadyFrom(270, 5));
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            for (int k = 0; k < 3000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            FlightState s = core.State;
            Double3 wind = s.Attitude.InverseRotate(core.Wind.Mean), v = s.Attitude.InverseRotate(s.Velocity);
            Double3 Air(Double3 point) => v + Double3.Cross(s.BodyRate, point) - wind;
            double q = 0.5 * d.Aero.AirDensity * d.Aero.DragCoefficient;
            Double3 b = Air(d.Aero.BodyCenterOfPressure);
            Double3 body = b * (-q * (d.Aero.BodyArea.X * Math.Abs(b.X) + d.Aero.BodyArea.Y * Math.Abs(b.Y) + d.Aero.BodyArea.Z * Math.Abs(b.Z)));
            DronePart coil = d.Parts[d.Coil.Part];
            Double3 c = Air(coil.Position);
            double along = Double3.Dot(c, coil.Axis), across = Math.Sqrt(Math.Max(0, Double3.Dot(c, c) - along * along));
            Double3 coilForce = c * (-q * (coil.Length * coil.Diameter * across + Math.PI / 4 * coil.Diameter * coil.Diameter * Math.Abs(along)));
            Double3 rotors = default;
            for (int i = 0; i < 4; i++)
            {
                Double3 h = Air(d.Motors[i].Position);
                double perMs = d.Props.HForce * s.RotorSpeed[i] / (d.Props.ReferenceRpm * 2 * Math.PI / 60);
                rotors = rotors + new Double3(h.X, 0, h.Z) * -perMs;
            }
            Double3 expected = s.Attitude.Rotate(body + coilForce + rotors), got = core.Trace.AeroForce;
            double rotorShare = Math.Sqrt(rotors.X * rotors.X + rotors.Z * rotors.Z), total = Math.Sqrt(expected.X * expected.X + expected.Z * expected.Z);
            TestContext.Out.WriteLine($"5 m/s from the west on the drone holding its point: the air's push {got} N, expected {expected} N; the rotors' "
                + $"{F(rotorShare, "0.000")} N of {F(total, "0.000")} N, the body's {F(body.Length(), "0.000")} N, the coil's {F(coilForce.Length(), "0.000")} N; "
                + $"the rotors' drag {F(rotorShare / 5 / core.Body.Mass, "0.000")} s⁻¹ per unit mass (wind.md §5.3: 0.25 nominal, 0.1–0.4)");
            Assert.That((got - expected).Length(), Is.LessThan(0.01 * total), "the model's push is its formulas' (the rotors at their mean speed over the step)");
            Assert.That(got.X, Is.GreaterThan(0), "it pushes downwind, east");
        }

        // The rotors' gyroscopic moment: a body carrying spinning rotors, turning with no outside moment, keeps its
        // total angular momentum I·ω + h in the world (to the step's first order); the opposite sign does not.
        [Test]
        public void TheRotorsGyroscopicMomentKeepsTheTotalMomentum()
        {
            MassProperties body = Drone.BuildMass(Drone.Nominal).Properties;
            const double spin = 0.05; // N·m·s: a 0.4 rad/s yaw-pair split of 6e-5 kg·m² rotors at 800 rad/s, about
            double Drift(double sign)
            {
                var s = new FlightState { Attitude = Quat.Identity, BodyRate = new Double3(1.5, 0.3, -1.0) };
                Double3 L0 = s.Attitude.Rotate(body.Inertia * s.BodyRate + new Double3(0, spin, 0));
                double worst = 0;
                for (int k = 0; k < 10000; k++)
                {
                    Double3 m = FlightCore.GyroscopicMoment(s.BodyRate, spin) * sign;
                    RigidBody.Step(ref s, body, default, m, 0.001);
                    Double3 L = s.Attitude.Rotate(body.Inertia * s.BodyRate + new Double3(0, spin, 0));
                    worst = Math.Max(worst, (L - L0).Length() / L0.Length());
                }
                return worst;
            }
            double right = Drift(1), wrong = Drift(-1), none = Drift(0);
            TestContext.Out.WriteLine($"10 s of a free tumble with h = {spin} N·m·s: the world's total angular momentum drifts {F(right, "E2")} with "
                + $"−ω × h, {F(wrong, "E2")} with its sign reversed, {F(none, "E2")} without it");
            Assert.That(right, Is.LessThan(1e-2), "first order in the step, as the body's own ω × I·ω");
            Assert.That(wrong, Is.GreaterThan(50 * right));
            Assert.That(none, Is.GreaterThan(50 * right));
        }

        // The feed's AirVelocityAtCamera: the wind less the camera's motion, in the camera's (the body's) axes.
        [Test]
        public void TheAirAtTheCameraIsTheRelativeWind()
        {
            var core = new FlightCore(new FlightSetup
            {
                Seed = 1, World = FlightWorld.Floor(), Mass = Drone.BuildMass(Drone.Nominal), Wind = WindSetup.SteadyFrom(270, 4),
                StartPosition = new Double3(0, 300, 0), StartVelocity = new Double3(0, 0, 10),
            });
            core.GetSignals(out FeedSignals sig);
            Assert.That((sig.Live & FeedSignalSet.AirVelocityAtCamera) != 0, Is.True, "live");
            // Flying north at 10 m/s with the nose north, a wind from the west: the air comes from ahead and the left.
            Assert.That(sig.AirVelocityAtCamera.X, Is.EqualTo(4).Within(1e-12), "from the left, toward the right");
            Assert.That(sig.AirVelocityAtCamera.Z, Is.EqualTo(-10).Within(1e-12), "from ahead");
            Assert.That(sig.AirVelocityAtCamera.Y, Is.EqualTo(0).Within(1e-12));
        }
    }
}
