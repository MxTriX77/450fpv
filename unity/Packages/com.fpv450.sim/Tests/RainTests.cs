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

        [Test]
        public void RainWithoutWindKeepsCalmsWindAndItsDragFormula()
        {
            RainScenario.Result r = RainScenario.Fly(RepositoryDrone.Spec());
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
            double hover = 1000 * rate / 3.6e6 * RainScenario.FormulaFall(rate) * top;
            Assert.That(-atRest.Trace.RainForce.Y, Is.EqualTo(hover).Within(0.01 * hover), "at rest, ρ_w·R·V on the top");
            Assert.That(Math.Abs(atRest.Trace.RainForce.X) + Math.Abs(atRest.Trace.RainForce.Z), Is.LessThan(1e-12 * hover));
        }

        [Test]
        public void TheFilmFillsToItsBoundAndWeighs()
        {
            RainScenario.Result r = RainScenario.Fly(RepositoryDrone.Spec(), 20000);
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
