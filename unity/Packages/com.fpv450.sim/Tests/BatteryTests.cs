using System;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects: "The battery's margin and the inertia character" (PHY-11; p7b-airframe 1.5): the heavy corner's
    // margin lost to the sag, the OSD's low-voltage signal, and the pack running to its cut-off. drone.json's packs, two
    // 6S2P of Molicel P50B, on K§5.2's 6.5 kg heavy corner (PropulsionTests.Heavy). Every figure printed is derived.
    public sealed class BatteryTests
    {
        const double G = 9.80665;

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        static double Thrust(in RotorLoads l) => (l.Thrust.RearRight + l.Thrust.FrontRight + l.Thrust.RearLeft + l.Thrust.FrontLeft) / l.SubSteps;

        // Full throttle on a body held at rest: the thrust over the heavy corner's weight and the loaded voltage a cell.
        static void FullThrottle(DroneSpec spec, double charge, double weight, out double tw, out double cellVolts)
        {
            var prop = new Propulsion(spec.BuildPropulsion());
            PropulsionTests.FullDuty(prop, charge, 2, out RotorLoads loads);
            tw = Thrust(loads) / weight;
            cellVolts = loads.Volts / spec.Packs[0].Series;
        }

        [Test]
        public void TheHeavyCornerAtLowCharge()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double weight = PropulsionTests.Heavy.Properties.Mass * G;
            FullThrottle(spec, 1.0, weight, out double twFull, out double vFull);
            FullThrottle(spec, 0.2, weight, out double twLow, out double vLow);

            // Known-wrong: a stiff pack, its voltage not falling with the charge (the table flat at the full cell's 4.2 V).
            DroneSpec stiff = DroneSpec.Parse(RepositoryDrone.Text());
            for (int k = 0; k < stiff.Packs.Length; k++)
                for (int j = 0; j < stiff.Packs[k].Ocv.Length; j++) stiff.Packs[k].Ocv[j] = 4.2;
            FullThrottle(stiff, 0.2, weight, out double twStiff, out _);

            // The OSD's signal in flight: the 6.5 kg corner at 20 % charge, full throttle for 1 s from a hover's stick.
            FlightCore core = PropulsionTests.Core(PropulsionTests.Heavy, 0.2);
            PropulsionTests.Arm(core);
            for (int k = 0; k < 1000; k++) core.Advance(new StickSample { Throttle = 1, Arm = true });
            core.GetSignals(out FeedSignals signals);
            double vFlight = signals.PackVolts / spec.Packs[0].Series;

            TestContext.Out.WriteLine($"6.5 kg corner, full throttle: full pack {F(vFull)} V a cell loaded, T/W {F(twFull)} (margin {F(twFull - 1)}); "
                + $"20 % charge {F(vLow)} V, T/W {F(twLow)} (margin {F(twLow - 1)}), {F(100 * (1 - (twLow - 1) / (twFull - 1)), "0.0")} % of the margin lost; "
                + $"known-wrong stiff pack at 20 %: T/W {F(twStiff)}. In flight after 1 s: {F(vFlight)} V a cell, low-voltage signal {signals.PackLow} "
                + $"(threshold {F(FeedSignals.LowCellVolts, "0.00")} V a cell)");
            Assert.That(twLow, Is.LessThan(twFull), "the sag takes the margin");
            Assert.That(twStiff, Is.GreaterThan(twLow), "the known-wrong stiff pack keeps it");
            Assert.That(signals.PackLow, Is.EqualTo(vFlight < FeedSignals.LowCellVolts), "the signal is the loaded voltage against its threshold");
            // As computed (1.5's finding, handed to the orchestrator): at 20 % the loaded cell holds about 3.1 V, above the
            // pilot's 3.00 V warning, so the spec's "the low-voltage signal is set" does not hold at 20 %; it does below 13 %.
            Assert.That(signals.PackLow, Is.False, "at 20 % the loaded cell is above the 3.00 V warning");
        }

        // Full throttle on the heavy corner from 30 % to empty, in 1 % steps: where the low-voltage signal first sets, and
        // the margin at the cut-off.
        [Test]
        public void TheLowVoltageSignalAndTheCutOffUnderFullThrottle()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double weight = PropulsionTests.Heavy.Properties.Mass * G;
            string line = "6.5 kg corner, full throttle, loaded V a cell (T/W):";
            double lastV = double.MaxValue, lastTw = double.MaxValue, setBelow = double.NaN, twEmpty = double.NaN;
            for (int pct = 30; pct >= 0; pct -= 1)
            {
                FullThrottle(spec, pct / 100.0, weight, out double tw, out double v);
                if (pct % 5 == 0) line += $" {pct} % {F(v)} ({F(tw, "0.00")})";
                Assert.That(v, Is.LessThan(lastV), "the loaded voltage falls with the charge");
                Assert.That(tw, Is.LessThan(lastTw), "and the thrust with it");
                if (double.IsNaN(setBelow) && v < FeedSignals.LowCellVolts) setBelow = pct;
                lastV = v;
                lastTw = tw;
                twEmpty = tw;
            }
            TestContext.Out.WriteLine(line + $"; the low-voltage signal sets at {setBelow} % and below");
            Assert.That(setBelow, Is.InRange(10, 14), "the warning under full throttle, near the knee");
            Assert.That(twEmpty, Is.LessThan(1), "at the cut-off the heavy corner has no margin left: it cannot hold its weight");
        }

        // A long flight: the packs from 10 % charge at a steady 60 % duty, a body held at rest, until the loaded voltage
        // passes the P50B's 2.5 V cut-off (published), so a session's battery end can fire. Before model 12 the table stopped
        // at 3.0 V and the model held it below charge 0, so the pack never emptied.
        [Test]
        public void ThePackReachesItsCutOffInALongFlight()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var prop = new Propulsion(spec.BuildPropulsion());
            int series = spec.Packs[0].Series;
            var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 0.1, PackCharge1 = 0.1 };
            var duty = new Corners { RearRight = 0.6, FrontRight = 0.6, RearLeft = 0.6, FrontLeft = 0.6 };
            double tLow = double.NaN, cLow = double.NaN, tCut = double.NaN, cCut = double.NaN, amps = 0;
            for (int k = 0; k < 600000 && double.IsNaN(tCut); k++)
            {
                prop.BeginStep(s);
                var loads = new RotorLoads();
                for (int j = 0; j < FlightController.LoopsPerStep; j++) prop.SubStep(ref s, duty, true, FlightController.LoopDt, ref loads);
                double cell = loads.Volts / series;
                amps = loads.Amps0 + loads.Amps1;
                if (double.IsNaN(tLow) && cell < FeedSignals.LowCellVolts) { tLow = k / 1000.0; cLow = s.PackCharge0; }
                if (cell < 2.5) { tCut = k / 1000.0; cCut = s.PackCharge0; }
            }
            TestContext.Out.WriteLine($"From 10 % at 60 % duty: the low-voltage signal at {F(tLow, "0.0")} s (charge {F(cLow, "0.0000")}), "
                + $"the 2.5 V cut-off at {F(tCut, "0.0")} s (charge {F(cCut, "0.0000")}), {F(amps, "0.0")} A from the packs then");
            Assert.That(tCut, Is.Not.NaN, "the loaded pack reaches its cut-off");
            Assert.That(tLow, Is.LessThan(tCut), "the warning comes first");
            Assert.That(cCut, Is.GreaterThan(-0.02).And.LessThan(0.05), "at the knee, near empty");
        }
    }
}
