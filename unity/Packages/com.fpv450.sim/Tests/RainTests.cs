using System;
using System.Collections.Generic;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.7 (design 7, PHY-17, D-021): rain is a level of its own. "Rain without wind" (wind-turbulence spec), the
    // rain's draws in their classes, the film's bound, and the Sunny audit (every golden is model 8's over its bytes).
    public class RainTests
    {
        const int Model8Bytes = 1392;

        // The rain's drag by its labelled formula, written again here with System.Math (not the core's DetMath or its
        // code): W = ρ_w·R/V, V = 9.65 − 10.3·e^(−0.6·D0), D0 = 0.895·R^0.21 mm; F = −W·(silhouette·|u|)·u.
        static double FormulaWater(double rate) => 1000 * rate / 3.6e6 / FormulaFall(rate);

        static double FormulaFall(double rate) => 9.65 - 10.3 * Math.Exp(-0.6 * 0.895 * Math.Pow(rate, 0.21));

        public static RainScenario RainWithoutWind(int steps = 10000, ulong seed = 0x450F)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            setup.Seed = seed;
            setup.Wind = WindSetup.Of(WindLevel.Calm);
            setup.Rain = RainLevel.SevereRain;
            setup.CameraPosition = spec.CameraPosition;
            FlightSetup dry = GoldenFlights.PoweredSetup(spec);
            dry.Seed = seed;
            dry.Wind = WindSetup.Of(WindLevel.Calm);
            var core = new FlightCore(setup);
            var sunny = new FlightCore(dry);
            var r = new RainScenario { Core = core, Sunny = sunny, DryMass = core.Body.Mass };
            core.GetSignals(out FeedSignals first);
            r.FirstSignals = first;

            var record = new byte[LogFormat.StepSize];
            for (int k = 0; k < steps; k++)
            {
                StickSample stick = GoldenFlights.PoweredStick(k);
                FlightState was = core.State;
                MassProperties body = core.Body;
                core.Advance(stick);
                sunny.Advance(stick);

                // The formula on the step's starting state, with the field's wind at each silhouette's centre (the
                // field's frame is still this step's).
                AeroSpec aero = setup.Aero;
                double w = FormulaWater(core.Rain.RateMmPerHour), fall = FormulaFall(core.Rain.RateMmPerHour);
                Double3 v = was.Attitude.InverseRotate(was.Velocity);
                Double3 expected = default;
                foreach (int part in new[] { -1, aero.CoilPart })
                {
                    Double3 point = part < 0 ? aero.BodyCenterOfPressure : setup.Mass.Part(part).Position;
                    Double3 wind = core.Wind.At(was.Position + was.Attitude.Rotate(point));
                    Double3 drop = new Double3(wind.X, wind.Y - fall, wind.Z);
                    Double3 u = v + Double3.Cross(was.BodyRate, point) - was.Attitude.InverseRotate(drop);
                    double flux;
                    if (part < 0)
                        flux = aero.BodyArea.X * Math.Abs(u.X) + aero.BodyArea.Y * Math.Abs(u.Y) + aero.BodyArea.Z * Math.Abs(u.Z);
                    else
                    {
                        DronePart c = setup.Mass.Part(part);
                        double along = Double3.Dot(u, c.Axis), across = Math.Sqrt(Math.Max(Double3.Dot(u, u) - along * along, 0));
                        flux = c.Length * c.Diameter * across + Math.PI / 4 * c.Diameter * c.Diameter * Math.Abs(along);
                    }
                    expected = expected + u * (-w * flux);
                }
                expected = was.Attitude.Rotate(expected);
                double err = (core.Trace.RainForce - expected).Length() / expected.Length();
                r.WorstDragError = Math.Max(r.WorstDragError, err);
                r.MeanRainForce += core.Trace.RainForce.Length() / steps;
                r.PeakRainForce = Math.Max(r.PeakRainForce, core.Trace.RainForce.Length());
                r.PeakAirDrag = Math.Max(r.PeakAirDrag, (core.Trace.AeroForce - core.Trace.RainForce).Length());

                // Logged: the record carries the rain's force, the film and the air at the camera.
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                LogFormat.DecodeStep(record, out _, out _, out StepTrace back);
                if (!(back.RainForce.Equals(core.Trace.RainForce) && back.FilmMass == core.Trace.FilmMass
                      && back.CameraAir.Equals(core.Trace.CameraAir)))
                    r.LoggedMismatches++;
                if (k == 0) r.FirstHoverForce = core.Trace.RainForce;
            }
            core.GetSignals(out FeedSignals last);
            r.LastSignals = last;
            r.FilmMass = core.Rain.FilmMass;
            return r;
        }

        public sealed class RainScenario
        {
            public FlightCore Core, Sunny;
            public FeedSignals FirstSignals, LastSignals;
            public double DryMass, FilmMass, WorstDragError, MeanRainForce, PeakRainForce, PeakAirDrag;
            public Double3 FirstHoverForce;
            public int LoggedMismatches;
        }

        [Test]
        public void RainWithoutWindKeepsCalmsWindAndItsDragFormula()
        {
            RainScenario r = RainWithoutWind();
            FlightCore core = r.Core;

            // The rain's draws, named, in Severe rain's class, and the wind's draws Calm's, as in the Sunny flight.
            var rain = new List<ParameterDraw>();
            var wind = new List<string>();
            foreach (ParameterDraw d in core.Random.Draws)
                if (d.Stream.StartsWith("rain.", StringComparison.Ordinal)) rain.Add(d);
                else wind.Add(d.Stream + " " + d.Parameter + " " + d.Value.ToString("R"));
            var dryWind = new List<string>();
            foreach (ParameterDraw d in r.Sunny.Random.Draws) dryWind.Add(d.Stream + " " + d.Parameter + " " + d.Value.ToString("R"));
            Assert.That(wind, Is.EqualTo(dryWind), "the wind's draws are Calm's, as without rain");
            Assert.That(rain.Count, Is.EqualTo(2));
            Assert.That(rain[0].Parameter, Is.EqualTo("rain.rate_mm_h"));
            Assert.That(rain[0].Lo, Is.EqualTo(10));
            Assert.That(rain[0].Hi, Is.EqualTo(50));
            Assert.That(rain[1].Parameter, Is.EqualTo("rain.film_thickness_m"));
            Assert.That(core.Rain.RateMmPerHour, Is.InRange(10.0, 50.0));
            Assert.That(core.Wind.MeanSpeed, Is.EqualTo(r.Sunny.Wind.MeanSpeed));

            // The signals: live, the rate the draw, the air at the camera the trace's.
            Assert.That((r.LastSignals.Live & FeedSignalSet.RainRate) != 0 && (r.LastSignals.Live & FeedSignalSet.AirVelocityAtCamera) != 0);
            Assert.That(r.LastSignals.RainRateMmPerHour, Is.EqualTo(core.Rain.RateMmPerHour));
            Assert.That(r.LastSignals.AirVelocityAtCamera.Length(), Is.GreaterThan(0));
            Assert.That(r.LoggedMismatches, Is.EqualTo(0), "the record carries the rain's force, film and the camera's air");

            // The drag by its labelled formula, within 1 % (it is the same arithmetic, so far closer).
            Assert.That(r.WorstDragError, Is.LessThan(0.01));
            // Its size: at rest in still air, the drops' momentum flux ρ_w·R·V on the top silhouettes (body and coil),
            // straight down, within 1 %.
            FlightSetup still = GoldenFlights.PoweredSetup(RepositoryDrone.Spec());
            still.Wind = WindSetup.Of(WindLevel.Still);
            still.Rain = RainLevel.SevereRain;
            var atRest = new FlightCore(still);
            atRest.Advance(new StickSample());
            DronePart coil = still.Mass.Part(still.Aero.CoilPart);
            double rate = atRest.Rain.RateMmPerHour, top = still.Aero.BodyArea.Y + coil.Length * coil.Diameter;
            double hover = 1000 * rate / 3.6e6 * FormulaFall(rate) * top;
            Assert.That(-atRest.Trace.RainForce.Y, Is.EqualTo(hover).Within(0.01 * hover), "at rest, ρ_w·R·V on the top");
            Assert.That(Math.Abs(atRest.Trace.RainForce.X) + Math.Abs(atRest.Trace.RainForce.Z), Is.LessThan(1e-12 * hover));
        }

        [Test]
        public void TheFilmFillsToItsBoundAndWeighs()
        {
            RainScenario r = RainWithoutWind(20000);
            RainField rain = r.Core.Rain;
            Assert.That(rain.FilmCapacity, Is.EqualTo(1000 * rain.FilmThickness * rain.WettedArea).Within(1e-12));
            Assert.That(r.FilmMass, Is.GreaterThan(0));
            Assert.That(r.FilmMass, Is.LessThanOrEqualTo(rain.FilmCapacity));
            Assert.That(r.Core.Body.Mass - r.DryMass, Is.EqualTo(r.FilmMass).Within(1e-12), "the film is the body's");
            Assert.That(r.Core.Trace.FilmMass, Is.EqualTo(r.FilmMass));
        }

        [Test]
        public void ASunnyFlightIsModel8sBitForBit()
        {
            // The goldens over model 8's 1392 bytes (the record less 1.7's rain force, film and camera air) are model 8's pins.
            DroneSpec spec = RepositoryDrone.Spec();
            int skip = LogFormat.StepSize - Model8Bytes;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, Model8Bytes, skip), Is.EqualTo(GoldenFlights.Model8PoweredHash));
            Assert.That(GoldenFlights.FlyContact(spec, out _, Model8Bytes, skip), Is.EqualTo(GoldenFlights.Model8ContactHash));
            // A Sunny flight opens no rain stream and logs no rain.
            var core = new FlightCore(GoldenFlights.PoweredSetup(spec));
            Assert.That(core.Rain, Is.Null);
            foreach (string s in core.Random.Opened) Assert.That(s, Does.Not.StartWith("rain."));
            core.GetSignals(out FeedSignals sig);
            Assert.That(sig.RainRateMmPerHour, Is.EqualTo(0));
        }

        [Test]
        public void TheRainGoldenHolds()
        {
            ulong hash = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore core, wind: WindSetup.Of(WindLevel.Calm),
                rain: RainLevel.SevereRain);
            Assert.That(hash, Is.EqualTo(GoldenFlights.RainPoweredHash), "the pinned golden (a re-record is audited and raises the model version)");
            Assert.That(core.Rain.FilmMass, Is.GreaterThan(0));
        }

        [Test]
        public void RainLevelsAreTheirClasses()
        {
            Assert.That(RainPreset.Of(RainLevel.Rain).RateLo, Is.EqualTo(2.5));
            Assert.That(RainPreset.Of(RainLevel.Rain).RateHi, Is.EqualTo(10));
            Assert.That(RainPreset.Of(RainLevel.SevereRain).RateLo, Is.EqualTo(10));
            Assert.That(RainPreset.Of(RainLevel.SevereRain).RateHi, Is.EqualTo(50));
            Assert.That(RainPreset.Named(RainLevel.Sunny), Is.Empty);
            Assert.That(() => RainPreset.Of(RainLevel.Sunny), Throws.ArgumentException);
            // Rain needs the airframe's silhouettes.
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            setup.Aero = null;
            setup.Rain = RainLevel.Rain;
            Assert.That(() => new FlightCore(setup), Throws.ArgumentException);
        }
    }
}
