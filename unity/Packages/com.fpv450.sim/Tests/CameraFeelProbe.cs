using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // Scratch probe (p7e-camera-feel 1.2): how much micro-motion the sim's camera carries, rigid and elastic, in a hover and
    // a punch, and what a 50 Hz field and a render clock see of it.
    public class CameraFeelProbe
    {
        const double RadToDeg = 57.29577951308232;

        [Test, Explicit]
        public void Probe()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (WindLevel wind in new[] { WindLevel.Still, WindLevel.Severe })
                foreach (double payload in new[] { 0.0, 2.0 })
                {
                    FlightSetup setup = ShakeScenario.Setup(spec, payload, 1);
                    setup.Wind = WindSetup.Of(wind);
                    var core = new FlightCore(setup);
                    var pilot = new SimPilot(core.Controller, setup.StartPosition);
                    int steps = 8000, punchAt = 6000;
                    double cut = 1 - 6.283185307179586 * 5 * FlightCore.Dt;
                    Double3 lowR = default, angR = default, lowE = default, angE = default;
                    double sumR = 0, sumE = 0, sumC = 0, maxSpanC = 0, sumSpanC = 0;
                    int n = 0, fields = 0;
                    double fmin = double.MaxValue, fmax = double.MinValue;
                    double rpm = 0;
                    for (int k = 0; k < steps; k++)
                    {
                        long us = core.State.Step * FlightCore.StepUs;
                        StickSample st = k < 16 ? new StickSample { HostUs = us, Arm = k >= 8 } : pilot.Sticks(core, us);
                        if (k >= punchAt) st.Throttle = 1;
                        core.Advance(st);
                        if (k < 3000) continue;
                        core.GetSignals(out FeedSignals sig);
                        Double3 wr = core.State.BodyRate, wc = sig.CameraAngularVelocity;
                        lowR = lowR * cut + wr * (1 - cut);
                        angR = angR * cut + (wr - lowR) * FlightCore.Dt;
                        lowE = lowE * cut + wc * (1 - cut);
                        angE = angE * cut + (wc - lowE) * FlightCore.Dt;
                        Double3 el = core.Modes.FrameTurn(core.State.ModeShape);
                        sumR += angR.X * angR.X + angR.Y * angR.Y + angR.Z * angR.Z;
                        sumC += angE.X * angE.X + angE.Y * angE.Y + angE.Z * angE.Z;
                        sumE += el.X * el.X + el.Y * el.Y + el.Z * el.Z;
                        double pitch = angE.X * RadToDeg;
                        fmin = System.Math.Min(fmin, pitch);
                        fmax = System.Math.Max(fmax, pitch);
                        rpm = core.State.RotorSpeed[0] / 6.283185307179586;
                        n++;
                        if (k % 20 == 19)
                        {
                            double span = fmax - fmin;
                            maxSpanC = System.Math.Max(maxSpanC, span);
                            sumSpanC += span;
                            fields++;
                            fmin = double.MaxValue;
                            fmax = double.MinValue;
                        }
                        if (k == punchAt - 1 || k == steps - 1)
                        {
                            TestContext.WriteLine($"{wind} {payload} kg {(k < punchAt ? "hover" : "punch")}: rigid HF turn RMS {System.Math.Sqrt(sumR / n) * RadToDeg:F4}°, "
                                + $"camera (rigid+elastic) {System.Math.Sqrt(sumC / n) * RadToDeg:F4}°, elastic turn RMS {System.Math.Sqrt(sumE / n) * RadToDeg:F4}°, "
                                + $"pitch span in a field mean {sumSpanC / fields:F4}° max {maxSpanC:F4}°, rotor {rpm:F0} Hz, vib {sig.CameraVibration.X:F2}/{sig.CameraVibration.Y:F2}/{sig.CameraVibration.Z:F2}");
                            sumR = sumE = sumC = sumSpanC = maxSpanC = 0;
                            n = fields = 0;
                        }
                    }
                }
        }
    }
}
