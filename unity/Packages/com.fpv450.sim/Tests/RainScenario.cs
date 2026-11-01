using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// "Rain without wind" (wind-turbulence, p7c-wind 1.7, PHY-17): the powered golden's scripted pilot in Calm air and
    /// Severe rain, beside the same flight Sunny; each step's rain force against its labelled formula, written again
    /// with System.Math, and each record's rain and camera fields read back. RainTests and flightbench --scenarios fly it.
    /// </summary>
    public static class RainScenario
    {
        // The rain's drag by its labelled formula, written again here with System.Math (not the core's DetMath or its
        // code): W = ρ_w·R/V, V = 9.65 − 10.3·e^(−0.6·D0), D0 = 0.895·R^0.21 mm; F = −W·(silhouette·|u|)·u.
        public static double FormulaWater(double rate) => 1000 * rate / 3.6e6 / FormulaFall(rate);

        public static double FormulaFall(double rate) => 9.65 - 10.3 * Math.Exp(-0.6 * 0.895 * Math.Pow(rate, 0.21));

        public static Result Fly(DroneSpec spec, int steps = 10000, ulong seed = 0x450F)
        {
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
            var r = new Result { Core = core, Sunny = sunny, DryMass = core.Body.Mass };
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

        public sealed class Result
        {
            public FlightCore Core, Sunny;
            public FeedSignals FirstSignals, LastSignals;
            public double DryMass, FilmMass, WorstDragError, MeanRainForce, PeakRainForce, PeakAirDrag;
            public Double3 FirstHoverForce;
            public int LoggedMismatches;
        }
    }
}
