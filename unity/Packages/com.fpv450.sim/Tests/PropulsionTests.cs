using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // propulsion (PHB-03) on the repository's drone (p5-drone 1.2): the builder's PIDs on the plant, yaw saturating
    // first, hover and liftoff through the Betaflight throttle path, the motor's lag, the fast-flight inflow and the
    // sag. Flights start high in still air with nothing to touch (contacts are 1.6); a simulated pilot holds height
    // where a scenario needs hover. The numbers each scenario finds are written to the test output.
    public sealed class PropulsionTests
    {
        const double G = 9.80665;
        const double Rho = 1.225; // ISA sea level (published): APC's tables and k_T are at it

        static DroneSpec Drone => RepositoryDrone.Spec();

        // p5-drone's loadouts (the sheet): L4 nominal in flight, L5 take-off, L6 the loaded drone, and K§5.2's 6.5 kg
        // heavy corner (2.34 kg of payload, past the setting's 2.0 kg, set on the mass model directly).
        static MassModel L4 => Drone.BuildMass(Drone.Nominal);
        static MassModel L5 => Drone.BuildMass(new Loadout { Payload = 1.0, Legs = true });
        static MassModel L6 => Drone.BuildMass(new Loadout { Payload = 2.0, Legs = true });

        static MassModel Heavy
        {
            get
            {
                MassModel m = L6;
                m.SetPartMass(Drone.Payload.Part, 2.0 + 6.5 - m.Properties.Mass);
                return m;
            }
        }

        static FlightCore Core(MassModel mass, double charge, Quat attitude = default, Double3 velocity = default, PropulsionSpec propulsion = null)
        {
            if (attitude.W == 0 && attitude.X == 0 && attitude.Y == 0 && attitude.Z == 0) attitude = Quat.Identity;
            return new FlightCore(new FlightSetup
            {
                Seed = 1, World = FlightWorld.Floor(), Mass = mass, Propulsion = propulsion ?? Drone.BuildPropulsion(),
                StartPosition = new Double3(0, 500, 0), StartAttitude = attitude, StartVelocity = velocity, StartPackCharge = charge,
            });
        }

        // The drone's propulsion with its ESCs' current limit lifted (an ESC with none), or with no rotor in-plane drag
        // (the rate rig of a step-response test, where the tumbling body's own air does not reach the loop).
        static PropulsionSpec Unlimited()
        {
            PropulsionSpec p = Drone.BuildPropulsion();
            for (int i = 0; i < 4; i++) p.Motors[i].EscCurrentLimit = 1e9;
            return p;
        }

        static PropulsionSpec NoInPlaneDrag()
        {
            PropulsionSpec p = Drone.BuildPropulsion();
            p.Props.HForce = 0;
            return p;
        }

        static StickSample Stick(double throttle, double roll = 0, double pitch = 0, double yaw = 0, bool arm = true) =>
            new StickSample { Throttle = throttle, Roll = roll, Pitch = pitch, Yaw = yaw, Arm = arm };

        // Betaflight's arming: the switch seen off, then on with the throttle low.
        static void Arm(FlightCore core)
        {
            for (int k = 0; k < 8; k++) core.Advance(Stick(0, arm: false));
            for (int k = 0; k < 8; k++) core.Advance(Stick(0));
            Assert.That(core.State.Armed, Is.True, "armed");
        }

        struct Hover
        {
            public double Stick, Throttle, Duty, Rpm, Amps, Volts, FrontOverRear, Climb;
        }

        // The closed-loop pilot (SimPilot) holding the start's position, height and heading, so the hover is level and
        // still (the rotors' in-plane drag would otherwise trim a drift); the steady state over the last two seconds
        // of eight.
        static Hover HoldHeight(FlightCore core)
        {
            var pilot = new SimPilot(core.Controller, core.State.Position);
            double stick;
            var h = new Hover();
            int n = 0;
            for (int k = 0; k < 8000; k++)
            {
                StickSample sticks = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                stick = sticks.Throttle;
                core.Advance(sticks);
                double vz = core.State.Velocity.Y;
                if (k < 6000) continue;
                ref readonly FlightState s = ref core.State;
                h.Stick += stick;
                h.Throttle += (s.Fc.Throttle - 1000) / 1000;
                double duty = 0, rpm = 0;
                for (int i = 0; i < 4; i++)
                {
                    duty += s.EscCommand[i] / 4;
                    rpm += s.RotorSpeed[i] * 60 / (2 * Math.PI) / 4;
                }
                h.Duty += duty;
                h.Rpm += rpm;
                h.Amps += core.Trace.PackAmps0 + core.Trace.PackAmps1;
                h.Volts += core.Trace.PackVolts0;
                h.Climb += Math.Abs(vz);
                n++;
            }
            h.Stick /= n;
            h.Throttle /= n;
            h.Duty /= n;
            h.Rpm /= n;
            h.Amps /= n;
            h.Volts /= n;
            h.Climb /= n;
            // The CoM's forward offset: the front pair carries more (p5-drone: 3.7 % at L4).
            Corners w = core.State.RotorSpeed;
            core.Propulsion.Aero(w.FrontRight, 0, out double fr, out _);
            core.Propulsion.Aero(w.FrontLeft, 0, out double fl, out _);
            core.Propulsion.Aero(w.RearRight, 0, out double rr, out _);
            core.Propulsion.Aero(w.RearLeft, 0, out double rl, out _);
            h.FrontOverRear = (fr + fl - rr - rl) / (fr + fl + rr + rl);
            return h;
        }

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        // ---- Hover and liftoff as the real drone ----

        [Test]
        public void HoverAndLiftoffThroughTheBetaflightThrottlePath()
        {
            var cases = new (string name, MassModel mass)[] { ("L4 nominal", L4), ("L5 take-off", L5), ("L6 loaded", L6), ("6.5 kg corner", Heavy) };
            double[] charges = { 1.0, 0.5, 0.1 };
            var duty = new Dictionary<string, double>();
            foreach (var c in cases)
            {
                foreach (double charge in charges)
                {
                    FlightCore core = Core(c.mass, charge);
                    Arm(core);
                    Hover h = HoldHeight(core);
                    FlightController fc = core.Controller;
                    TestContext.Out.WriteLine($"{c.name} {F(c.mass.Properties.Mass, "0.00")} kg, charge {F(charge, "0.0")}: stick {F(h.Stick)}, "
                        + $"Betaflight motor % {F(100 * h.Throttle, "0.0")}, ESC duty {F(100 * h.Duty, "0.0")} %, {F(h.Rpm, "0")} rpm, "
                        + $"{F(h.Amps, "0.0")} A at {F(h.Volts, "0.00")} V, front over rear {F(100 * h.FrontOverRear, "0.0")} %, |climb| {F(h.Climb, "0.0000")} m/s");
                    Assert.That(h.Climb, Is.LessThan(0.02), "a steady hover");
                    // The throttle path: the stick through min_check and the curve to the mixer, and the idle to the ESC.
                    Assert.That((fc.ThrottleCommandOf(h.Stick) - 1000) / 1000, Is.EqualTo(h.Throttle).Within(0.002));
                    Assert.That(fc.DutyOf(h.Throttle), Is.EqualTo(h.Duty).Within(0.003));
                    duty[c.name + " " + charge] = h.Duty;
                    if (c.name == "L4 nominal" && charge == 1.0)
                    {
                        Assert.That(h.FrontOverRear, Is.EqualTo(0.037).Within(0.003),
                            "p5-drone: the front motors 3.7 % over the mean at L4 (the CoM 6 mm forward: the front pair 7.7 % over the rear pair)");
                        core.GetSignals(out FeedSignals sig);
                        Assert.That(sig.PackVolts, Is.EqualTo(h.Volts).Within(0.05));
                        Assert.That(sig.MotorElectricalHz.RearRight, Is.EqualTo(core.State.RotorSpeed.RearRight / (2 * Math.PI) * 7).Within(2));
                        Assert.That(sig.MotorCurrentTotalA, Is.EqualTo(h.Amps).Within(0.5));
                    }
                }
            }
            // PHB-03 and fc-profiles §3: a loaded drone lifts off near 50–65 % motor power (the pilot: about 62 %).
            Assert.That(duty["L6 loaded 1"], Is.InRange(0.50, 0.65));
            Assert.That(duty["6.5 kg corner 1"], Is.InRange(0.50, 0.65));
            Assert.That(duty["L6 loaded 0.5"], Is.InRange(0.50, 0.65));
            Assert.That(duty["L4 nominal 0.5"], Is.LessThan(duty["L4 nominal 0.1"]), "the sagging pack needs more");
        }

        // ---- The maximum thrust and the sag ----

        // The independent check: the DC motor's steady state at full duty, every motor alike, two packs in parallel,
        // solved by bisection on the rotor speed with drone.json's numbers in System.Math (not Propulsion's code). Where
        // the motor's current would pass the ESC's limit, the limit holds it: the prop's torque then balances the
        // limit's, Kt·(I_lim − I0) = Q(ω), and the ESC draws its duty's share of it, d'·I_lim with d' = (I_lim·R + Ke·ω)/V.
        static void FullDutySteadyState(double charge, out double thrustPerMotor, out double volts, out double packAmps,
            double limit = double.NaN)
        {
            DroneSpec d = Drone;
            DroneMotor m = d.Motors[0];
            DroneProps p = d.Props;
            DronePack pack = d.Packs[0];
            if (double.IsNaN(limit)) limit = m.EscCurrentLimit;
            double ke = 60 / (2 * Math.PI * m.Kv);
            double voc = pack.Series * Ocv(pack.Ocv, charge), rPack = pack.Series * pack.CellResistance / pack.Parallel / 2; // two packs
            double Q(double w) => p.TorqueRatio * p.ThrustCoefficient * Lerp(p.StaticRpm, p.StaticTorqueRatio, w * 60 / (2 * Math.PI)) * w * w;
            double lo = 0, hi = m.Kv * voc * 2 * Math.PI / 60;
            for (int it = 0; it < 200; it++)
            {
                double w = (lo + hi) / 2;
                double i = m.NoLoadCurrent + Q(w) / ke;
                double v = voc - rPack * 4 * i;
                if (v - ke * w - m.Resistance * i > 0) lo = w; else hi = w;
            }
            double ws = lo;
            double current = m.NoLoadCurrent + Q(ws) / ke;
            volts = voc - rPack * 4 * current;
            packAmps = 4 * current; // both packs together: four ESCs at full duty draw their motors' current
            if (current > limit)
            {
                lo = 0;
                for (int it = 0; it < 200; it++)
                {
                    double w = (lo + hi) / 2;
                    if (Q(w) < ke * (limit - m.NoLoadCurrent)) lo = w; else hi = w;
                }
                ws = lo;
                double power = 4 * (limit * m.Resistance + ke * ws) * limit; // the four ESCs' constant power
                volts = (voc + Math.Sqrt(voc * voc - 4 * rPack * power)) / 2;
                packAmps = power / volts;
            }
            thrustPerMotor = p.ThrustCoefficient * Lerp(p.StaticRpm, p.StaticThrustRatio, ws * 60 / (2 * Math.PI)) * ws * ws;
        }

        static double Ocv(double[] table, double charge)
        {
            double x = Math.Clamp(charge, 0, 1) * (table.Length - 1);
            int k = Math.Min((int)x, table.Length - 2);
            return table[k] + (x - k) * (table[k + 1] - table[k]);
        }

        static double Lerp(double[] x, double[] y, double at)
        {
            if (at <= x[0]) return y[0];
            for (int k = 0; k < x.Length - 1; k++)
                if (at < x[k + 1]) return y[k] + (at - x[k]) * (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
            return y[y.Length - 1];
        }

        // Propulsion alone on a body held at rest, every ESC at full duty for `seconds`.
        static FlightState FullDuty(Propulsion prop, double charge, double seconds, out RotorLoads last)
        {
            var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = charge, PackCharge1 = charge };
            var full = new Corners { RearRight = 1, FrontRight = 1, RearLeft = 1, FrontLeft = 1 };
            last = default;
            for (int k = 0; k < seconds * 1000; k++)
            {
                prop.BeginStep(s);
                last = new RotorLoads();
                for (int j = 0; j < FlightController.LoopsPerStep; j++) prop.SubStep(ref s, full, true, FlightController.LoopDt, ref last);
            }
            return s;
        }

        [Test]
        public void TheMaximumThrustIsTheMotorsAtItsTopSpeed()
        {
            // With the ESCs' 40 A limit (drone.json, the 1.5 policy) and without one.
            foreach (bool limited in new[] { true, false })
            {
                var prop = new Propulsion(limited ? Drone.BuildPropulsion() : Unlimited());
                FullDuty(prop, 1, 2, out RotorLoads loads);
                double total = (loads.Thrust.RearRight + loads.Thrust.FrontRight + loads.Thrust.RearLeft + loads.Thrust.FrontLeft) / loads.SubSteps;
                FullDutySteadyState(1, out double perMotor, out double volts, out double packAmps, limited ? double.NaN : 1e9);
                double weight4 = L4.Properties.Mass * G, weight5 = L5.Properties.Mass * G;
                TestContext.Out.WriteLine($"full duty, full pack, {(limited ? "the ESCs' 40 A limit" : "no current limit")}: {F(total / G, "0.00")} kgf "
                    + $"({F(total / 4 / G, "0.00")} kgf a motor), {F(loads.Volts, "0.00")} V, {F(loads.Amps0 + loads.Amps1, "0.0")} A from the packs, "
                    + $"limited ESCs 0x{loads.Limited:X}; thrust over weight {F(total / weight4, "0.00")} at L4, {F(total / weight5, "0.00")} at L5; "
                    + $"the independent steady state {F(perMotor / G, "0.000")} kgf a motor at {F(volts, "0.00")} V, {F(packAmps, "0.0")} A");
                Assert.That(total / 4, Is.EqualTo(perMotor).Within(0.01 * perMotor), "the dynamic model settles on the DC motor's steady state");
                Assert.That(loads.Volts, Is.EqualTo(volts).Within(0.01 * volts));
                Assert.That(loads.Amps0 + loads.Amps1, Is.EqualTo(packAmps).Within(0.01 * packAmps));
                Assert.That(loads.Limited, Is.EqualTo(limited ? 0xF : 0), "full duty passes 40 A, so the limit holds every motor");
                Assert.That(total / G, limited ? Is.InRange(9.0, 12.5) : Is.InRange(10.0, 17.0),
                    limited ? "K§5.3's 9.0–12.5 kgf at a 40 A maximum" : "a 10-inch 2-blade on a 900 KV 3115 at 6S");
            }
        }

        // The ESC's limit: a spin-up from idle to full duty never lets a motor's current pass it, and an ESC without one
        // draws far more in the spin-up than at its steady full duty.
        [Test]
        public void TheEscHoldsTheMotorAtItsCurrentLimit()
        {
            DroneSpec d = Drone;
            DroneMotor m = d.Motors[0];
            double ke = 60 / (2 * Math.PI * m.Kv);
            var peaks = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                var prop = new Propulsion(pass == 0 ? d.BuildPropulsion() : Unlimited());
                var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 1, PackCharge1 = 1 };
                var idle = new Corners { RearRight = 0.06, FrontRight = 0.06, RearLeft = 0.06, FrontLeft = 0.06 };
                var full = new Corners { RearRight = 1, FrontRight = 1, RearLeft = 1, FrontLeft = 1 };
                for (int k = 0; k < 2000; k++) Step(prop, ref s, idle);
                for (int k = 0; k < 500; k++)
                {
                    prop.BeginStep(s);
                    for (int j = 0; j < FlightController.LoopsPerStep; j++)
                    {
                        var loads = new RotorLoads();
                        double w = s.RotorSpeed.RearRight;
                        prop.SubStep(ref s, full, true, FlightController.LoopDt, ref loads);
                        // The motor's current over the sub-step from its supply current and duty: i = I_supply / d'.
                        double volts = loads.Volts, held = (loads.Limited & 1) != 0 ? m.EscCurrentLimit : double.NaN;
                        double free = (s.EscCommand.RearRight * volts - ke * w) / m.Resistance;
                        peaks[pass] = Math.Max(peaks[pass], double.IsNaN(held) ? free : held);
                    }
                }
            }
            TestContext.Out.WriteLine($"idle to full duty: the motor's peak current {F(peaks[0], "0.0")} A with the ESC's {F(m.EscCurrentLimit, "0")} A limit, "
                + $"{F(peaks[1], "0.0")} A without one");
            Assert.That(peaks[0], Is.LessThanOrEqualTo(m.EscCurrentLimit * (1 + 1e-12)), "the limit holds the current");
            Assert.That(peaks[1], Is.GreaterThan(1.5 * m.EscCurrentLimit), "the spin-up's transient current");
        }

        [Test]
        public void AFullCollectivePullOnASaggedPack()
        {
            DroneSpec d = Drone;
            var prop = new Propulsion(d.BuildPropulsion());
            FlightState s = FullDuty(prop, 0.2, 3, out RotorLoads loads);
            DronePack pack = d.Packs[0];
            double cellAmps = loads.Amps0 / pack.Parallel;
            double expected = pack.Series * (Ocv(pack.Ocv, s.PackCharge0) - cellAmps * pack.CellResistance);
            Assert.That(loads.Volts, Is.EqualTo(expected).Within(0.01 * expected), "V = n_s·(OCV(SoC) − I_cell·R_cell)");
            Assert.That(loads.Amps0, Is.EqualTo(loads.Amps1).Within(1e-9), "two like packs share alike");

            double sagged = (loads.Thrust.RearRight + loads.Thrust.FrontRight + loads.Thrust.RearLeft + loads.Thrust.FrontLeft) / loads.SubSteps / 4;
            FullDutySteadyState(s.PackCharge0, out double perMotor, out double volts, out _);
            Assert.That(sagged, Is.EqualTo(perMotor).Within(0.01 * perMotor), "the maximum thrust is the motor's steady state at the sagged voltage");

            FullDuty(prop, 1, 2, out RotorLoads fresh);
            double full = (fresh.Thrust.RearRight + fresh.Thrust.FrontRight + fresh.Thrust.RearLeft + fresh.Thrust.FrontLeft) / fresh.SubSteps / 4;
            double ratio = sagged / full, voltRatio = loads.Volts / fresh.Volts;
            TestContext.Out.WriteLine($"3 s of full collective from 20 % charge: {F(loads.Volts, "0.00")} V at {F(loads.Amps0 + loads.Amps1, "0.0")} A "
                + $"(n_s·(OCV − I·R) = {F(expected, "0.00")} V), {F(sagged / G, "0.000")} kgf a motor against {F(full / G, "0.000")} on a full pack "
                + $"({F(fresh.Volts, "0.00")} V): thrust ratio {F(ratio)}, (V/V_full)² {F(voltRatio * voltRatio)}, charge now {F(s.PackCharge0, "0.0000")}");
            Assert.That(ratio, Is.LessThan(1).And.GreaterThan(voltRatio * voltRatio),
                "the top speed follows V less than in proportion: the motor's own I·R drop is smaller at the lower current");
        }

        // ---- ESC, motor and rotor ----

        [Test]
        public void TheMotorsLagIsItsPhysics()
        {
            DroneSpec d = Drone;
            var prop = new Propulsion(d.BuildPropulsion());
            DroneMotor m = d.Motors[0];
            double ke = 60 / (2 * Math.PI * m.Kv);
            // Near idle, at hover and near full: 0.80, where the 0.02 step keeps the current under the ESC's 40 A limit (a
            // limited motor is a current source, TheEscHoldsTheMotorAtItsCurrentLimit).
            foreach (double duty in new[] { 0.15, 0.47, 0.80 })
            {
                // Settle every motor at the duty, then step one motor by 0.02 (a small signal; the bus barely moves).
                var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 1, PackCharge1 = 1 };
                var u = new Corners { RearRight = duty, FrontRight = duty, RearLeft = duty, FrontLeft = duty };
                for (int k = 0; k < 3000; k++) Step(prop, ref s, u);
                double w0 = s.RotorSpeed.RearRight;
                prop.Aero(w0, 0, out _, out double q0);
                prop.Aero(w0 * 1.001, 0, out _, out double q1);
                double dq = (q1 - q0) / (w0 * 0.001);
                double tauModel = m.RotorInertia / (ke * ke / m.Resistance + dq);
                u.RearRight = duty + 0.02;
                var trace = new List<double>();
                for (int k = 0; k < 2000; k++)
                {
                    Step(prop, ref s, u);
                    trace.Add(s.RotorSpeed.RearRight);
                }
                double w1 = trace[trace.Count - 1], at63 = w0 + 0.632 * (w1 - w0);
                int t63 = trace.FindIndex(w => w >= at63) + 1;
                TestContext.Out.WriteLine($"duty {F(duty, "0.00")}: {F(w0 * 60 / (2 * Math.PI), "0")} rpm, τ_m = J/(Kt·Ke/R + ∂Q/∂ω) = {F(1000 * tauModel, "0.0")} ms; "
                    + $"a 0.02 duty step reaches 63 % in {t63} ms (τ_m + τ_esc = {F(1000 * (tauModel + m.EscTau), "0.0")} ms)");
                Assert.That(t63 / 1000.0, Is.EqualTo(tauModel + m.EscTau).Within(0.1 * (tauModel + m.EscTau)),
                    "the two first-order lags in series reach 63 % within a combined time constant ± 10 %");
                Assert.That(trace[0], Is.LessThan(at63), "never within a step");
            }
        }

        static void Step(Propulsion prop, ref FlightState s, in Corners duty)
        {
            prop.BeginStep(s);
            var loads = new RotorLoads();
            for (int j = 0; j < FlightController.LoopsPerStep; j++) prop.SubStep(ref s, duty, true, FlightController.LoopDt, ref loads);
        }

        // ---- The builder's PIDs hold a stable loop ----

        [TestCase(0, TestName = "The builder's PIDs hold a stable loop: roll")]
        [TestCase(1, TestName = "The builder's PIDs hold a stable loop: pitch")]
        public void TheBuildersPidsHoldAStableLoop(int axis)
        {
            // On a rate rig: a 200 °/s step held 2 s tumbles the body through 400°, and in free air the rotors'
            // in-plane drag of that tumble's own motion swings the rate by tens of °/s (printed below); the loop's own
            // stability is measured without it.
            FreeTumble(axis);
            FlightCore core = Core(L4, 1, propulsion: NoInPlaneDrag());
            Arm(core);
            Hover h = HoldHeight(core);
            FlightController fc = core.Controller;
            double lo = 0, hi = 1;
            for (int it = 0; it < 60; it++)
            {
                double mid = (lo + hi) / 2;
                if (fc.SetpointOf(axis, mid) < 200) lo = mid; else hi = mid;
            }
            double stick = (lo + hi) / 2;
            var rate = new double[2000];
            for (int k = 0; k < 2000; k++)
            {
                core.Advance(axis == 0 ? Stick(h.Stick, roll: stick) : Stick(h.Stick, pitch: stick));
                Axes3 g = FlightCore.Gyro(core.State.BodyRate);
                rate[k] = axis == 0 ? g.Roll : g.Pitch;
            }
            double peak = 0, settle = 0, lastMin = double.MaxValue, lastMax = double.MinValue, lastMean = 0;
            int rise = -1;
            for (int k = 0; k < rate.Length; k++)
            {
                peak = Math.Max(peak, rate[k]);
                if (Math.Abs(rate[k] - 200) > 10) settle = (k + 1) / 1000.0;
                if (rise < 0 && rate[k] >= 180) rise = k + 1;
                if (k >= 1000)
                {
                    lastMin = Math.Min(lastMin, rate[k]);
                    lastMax = Math.Max(lastMax, rate[k]);
                    lastMean += rate[k] / 1000;
                }
            }
            double overshoot = (peak - 200) / 200;
            TestContext.Out.WriteLine($"{(axis == 0 ? "roll" : "pitch")} step to 200 °/s at hover (stick {F(stick, "0.0000")}, throttle stick {F(h.Stick)}): "
                + $"90 % in {rise} ms, peak {F(peak, "0.0")} °/s (overshoot {F(100 * overshoot, "0.0")} %), within 5 % from {F(1000 * settle, "0")} ms, "
                + $"the last second {F(lastMean, "0.00")} °/s mean, {F(lastMax - lastMin, "0.00")} °/s peak to peak");
            Assert.That(lastMean, Is.EqualTo(200).Within(10), "settles within 5 % of the setpoint");
            Assert.That(overshoot, Is.LessThan(0.25), "overshoots by less than 25 %");
            Assert.That(settle, Is.LessThan(1.0), "within 5 % for good inside the first second");
            Assert.That(lastMax - lastMin, Is.LessThan(10), "no sustained oscillation");
        }

        // The same step in free air, the rotors' in-plane drag on: printed, not asserted.
        static void FreeTumble(int axis)
        {
            FlightCore core = Core(L4, 1);
            Arm(core);
            Hover h = HoldHeight(core);
            double stick = 0.9575;
            double lo = double.MaxValue, hi = double.MinValue;
            for (int k = 0; k < 2000; k++)
            {
                core.Advance(axis == 0 ? Stick(h.Stick, roll: stick) : Stick(h.Stick, pitch: stick));
                Axes3 g = FlightCore.Gyro(core.State.BodyRate);
                double r = axis == 0 ? g.Roll : g.Pitch;
                if (k >= 1000)
                {
                    lo = Math.Min(lo, r);
                    hi = Math.Max(hi, r);
                }
            }
            TestContext.Out.WriteLine($"in free air, the rotors' in-plane drag on: the last second's {(axis == 0 ? "roll" : "pitch")} rate {F(lo, "0.0")}–{F(hi, "0.0")} °/s, "
                + $"the body at {F(core.State.Velocity.Length(), "0.0")} m/s");
        }

        // ---- Yaw saturates first ----

        [Test]
        public void YawSaturatesFirstAndTheCollectiveYields()
        {
            // The demand K§5.3 derived from: the wind clip's maximum yaw acceleration, 288 °/s² (measured, wind.md
            // §5.5), on L4's yaw inertia, as a fixed moment the two rotor pairs' drag torque must make.
            MassModel mass = L4;
            double demand = mass.Properties.Inertia.YY * 288 * Math.PI / 180;
            var prop = new Propulsion(Drone.BuildPropulsion());
            FullDuty(prop, 1, 2, out RotorLoads full);
            double tMax = full.Thrust.RearRight / full.SubSteps;
            // The model's yaw authority holding collective c of full thrust: one pair at full, the other at (2c − 1).
            double Available(double c) => 2 * (Torque(prop, tMax) - Torque(prop, (2 * c - 1) * tMax));
            double lo = 0.5, hi = 1;
            for (int it = 0; it < 60; it++)
            {
                double mid = (lo + hi) / 2;
                if (Available(mid) > demand) lo = mid; else hi = mid;
            }
            double cStar = lo;
            TestContext.Out.WriteLine($"the wind clip's maximum yaw demand, {F(demand, "0.000")} N·m (288 °/s² on {F(mass.Properties.Inertia.YY, "0.0000")} kg·m²), "
                + $"runs out of mix past a collective of {F(cStar)} of full thrust ({F(tMax / G, "0.00")} kgf a motor; K§5.3 had 0.72 with κ 0.015–0.02 "
                + "and the shift as a share of the collective)");
            Assert.That(cStar, Is.InRange(0.75, 0.92));

            // Roll, for comparison: the clip's maximum roll acceleration, 412 °/s² (measured, wind.md §5.5), needs a
            // thrust difference across the 0.160 m arms (four rotors), so it runs out only near full collective.
            double rollDemand = mass.Properties.Inertia.ZZ * 412 * Math.PI / 180;
            double rollStar = 1 - rollDemand / (4 * Math.Abs(Drone.Motors[0].Position.X - mass.Properties.CenterOfMass.X)) / tMax;
            TestContext.Out.WriteLine($"the clip's maximum roll demand, {F(rollDemand, "0.000")} N·m, runs out past a collective of {F(rollStar)}");
            Assert.That(cStar, Is.LessThan(rollStar - 0.1), "yaw runs out of authority first (K§5.3)");

            // Full yaw stick through Betaflight: the yaw P term alone is Kp × 260 °/s = 375 of the 1000 mix scale, so
            // above a mixer throttle of about 1 − 0.375 airmode lowers the collective to keep the yaw, and roll and
            // pitch keep theirs. The first 50 ms add the rotors' own spin-up reaction to their drag torque.
            FlightController fc = Core(L4, 1).Controller;
            double hoverThrottle = 0, kick = 0;
            foreach (double throttle in new[] { 0.45, 0.55, 0.65, 0.75, 0.85 })
            {
                FlightCore core = Core(L4, 1);
                Arm(core);
                Hover h = HoldHeight(core);
                hoverThrottle = h.Throttle;
                double stick = StickFor(fc, throttle);
                for (int k = 0; k < 500; k++) core.Advance(Stick(stick)); // the throttle boost's transient gone
                var entered = new bool[4];
                var largest = new double[4];
                double yaw0 = FlightCore.Gyro(core.State.BodyRate).Yaw, accel = 0;
                for (int k = 0; k < 150; k++)
                {
                    core.Advance(Stick(stick, yaw: 1));
                    for (int e = 0; e < core.Events.Count; e++)
                    {
                        FlightEvent ev = core.Events[e];
                        if (ev.Kind != EventKind.MixerSaturation || ev.A <= 0) continue;
                        entered[ev.Index] = true;
                        largest[ev.Index] = Math.Max(largest[ev.Index], ev.A);
                    }
                    if (k == 49) accel = (FlightCore.Gyro(core.State.BodyRate).Yaw - yaw0) / 0.05;
                }
                if (throttle == 0.45) kick = accel;
                TestContext.Out.WriteLine($"full yaw at mixer throttle {F(throttle, "0.00")} (hover {F(h.Throttle)}): saturated roll {entered[0]}, pitch {entered[1]}, "
                    + $"yaw {entered[2]}, collective {entered[3]} (lost at entry {F(largest[3])}); yaw acceleration over the first 50 ms {F(accel, "0")} °/s²");
                Assert.That(entered[0] || entered[1], Is.False, "roll and pitch keep their authority");
                Assert.That(entered[3], Is.EqualTo(throttle > 0.625), "the collective yields past 1 − 0.375");
            }

            // The steady yaw authority at hover, drag torque alone: one pair at the hover throttle + 0.4 of the mix
            // (pidsum_limit_yaw), the other − 0.4; fc-profiles §3 (FpvPrep's notes): about 500–800 °/s² on these sizes.
            double up = fc.DutyOf(hoverThrottle + 0.4), down = fc.DutyOf(hoverThrottle - 0.4);
            var u = new Corners { RearRight = down, FrontRight = up, RearLeft = up, FrontLeft = down }; // the counter-clockwise pair up
            var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 1, PackCharge1 = 1 };
            var loads = new RotorLoads();
            for (int k = 0; k < 2000; k++)
            {
                prop.BeginStep(s);
                loads = new RotorLoads();
                for (int j = 0; j < 4; j++) prop.SubStep(ref s, u, true, FlightController.LoopDt, ref loads);
            }
            double steady = loads.YawMoment / loads.SubSteps / mass.Properties.Inertia.YY * 180 / Math.PI;
            double unlimitedKick = YawKick(Unlimited(), fc);
            TestContext.Out.WriteLine($"yaw authority at hover: {F(steady, "0")} °/s² steady (drag torque), {F(kick, "0")} °/s² over full yaw's first 50 ms "
                + $"(with the rotors' spin-up; {F(unlimitedKick, "0")} °/s² with no ESC current limit)");
            Assert.That(steady, Is.InRange(500, 800), "fc-profiles §3: about 500–800 °/s²");
            Assert.That(kick, Is.LessThan(unlimitedKick), "the 40 A limit takes from the spin-up's kick");
        }

        // Full yaw from a hover at mixer throttle 0.45: the yaw acceleration over the first 50 ms, °/s².
        static double YawKick(PropulsionSpec propulsion, FlightController fc)
        {
            FlightCore core = Core(L4, 1, propulsion: propulsion);
            Arm(core);
            HoldHeight(core);
            double stick = StickFor(fc, 0.45);
            for (int k = 0; k < 500; k++) core.Advance(Stick(stick));
            double yaw0 = FlightCore.Gyro(core.State.BodyRate).Yaw;
            for (int k = 0; k < 50; k++) core.Advance(Stick(stick, yaw: 1));
            return (FlightCore.Gyro(core.State.BodyRate).Yaw - yaw0) / 0.05;
        }

        // A rotor's drag torque at the speed where it makes this static thrust.
        static double Torque(Propulsion prop, double thrust)
        {
            if (thrust <= 0) return 0;
            double lo = 0, hi = 4000;
            for (int it = 0; it < 80; it++)
            {
                double mid = (lo + hi) / 2;
                prop.Aero(mid, 0, out double t, out _);
                if (t < thrust) lo = mid; else hi = mid;
            }
            prop.Aero(lo, 0, out _, out double q);
            return q;
        }

        // The stick whose curve gives this mixer throttle.
        static double StickFor(FlightController fc, double throttle)
        {
            double lo = 0, hi = 1;
            for (int it = 0; it < 60; it++)
            {
                double mid = (lo + hi) / 2;
                if ((fc.ThrottleCommandOf(mid) - 1000) / 1000 < throttle) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        // ---- Fast flight loses thrust to inflow ----

        [Test]
        public void FastFlightLosesThrustToInflow()
        {
            DroneSpec d = Drone;
            var prop = new Propulsion(d.BuildPropulsion());
            double tilt = 22.7 * Math.PI / 180;
            // Nose down by the tilt: a positive rotation about body +x (DetMath.cs), flying north at 25 m/s.
            var q = new Quat(Math.Sin(tilt / 2), 0, 0, Math.Cos(tilt / 2));
            var s = new FlightState { Attitude = q, Velocity = new Double3(0, 0, 25), PackCharge0 = 1, PackCharge1 = 1 };
            prop.BeginStep(s);
            double va = prop.Inflow.RearRight;
            Assert.That(va, Is.EqualTo(9.6).Within(0.96), "25 m/s through a disc tilted 22.7°");
            for (int i = 0; i < 4; i++) Assert.That(prop.Inflow[i], Is.EqualTo(25 * Math.Sin(tilt)).Within(1e-9));

            // Full duty held there, and again static: the available thrust and what it costs.
            var full = new Corners { RearRight = 1, FrontRight = 1, RearLeft = 1, FrontLeft = 1 };
            RotorLoads loads = default;
            for (int k = 0; k < 2000; k++)
            {
                prop.BeginStep(s);
                loads = new RotorLoads();
                for (int j = 0; j < 4; j++) prop.SubStep(ref s, full, true, FlightController.LoopDt, ref loads);
            }
            double t25 = loads.Thrust.RearRight / loads.SubSteps, w25 = s.RotorSpeed.RearRight;
            prop.Aero(w25, va, out _, out double q25);
            FullDuty(prop, 1, 2, out RotorLoads still);
            double t0 = still.Thrust.RearRight / still.SubSteps;

            // Momentum theory at that inflow: T = 2ρA·v_i·(v_i + V_a), the ideal power T·(V_a + v_i) against the shaft's.
            double area = Math.PI * d.Props.Diameter * d.Props.Diameter / 4;
            double vi = -va / 2 + Math.Sqrt(va * va / 4 + t25 / (2 * Rho * area));
            double efficiency = t25 * (va + vi) / (q25 * w25);
            double advance = va / (w25 / (2 * Math.PI) * d.Props.Diameter);
            TestContext.Out.WriteLine($"25 m/s at 22.7°: axial inflow {F(va, "0.00")} m/s, J {F(advance, "0.000")}, full-duty thrust {F(t25 / G, "0.000")} kgf a motor "
                + $"at {F(w25 * 60 / (2 * Math.PI), "0")} rpm against {F(t0 / G, "0.000")} static: {F(t25 / t0)}; momentum theory's v_i {F(vi, "0.00")} m/s "
                + $"(hover's {F(Math.Sqrt(t0 / 4 / (2 * Rho * area)), "0.00")} at a quarter of full), ideal over shaft power {F(efficiency)}");
            Assert.That(t25 / t0, Is.InRange(0.6, 0.95), "the inflow costs thrust: never a flat factor, never none");
            Assert.That(efficiency, Is.InRange(0.5, 1.0), "the prop never makes more momentum power than its shaft gives");

            // Momentum theory's thrust at that inflow and speed: one blade station at 0.75 R whose angle of attack the
            // inflow through the disc, V_a + v_i, takes from, calibrated on the static thrust (its v_i from momentum
            // theory) and on the zero-thrust advance ratio; with T = 2ρA·v_i·(v_i + V_a) it is a quadratic in v_i.
            // The model's table agrees; design 5's assumed form, T = k_T·n²·(1 − V_a/(n·p)), which takes all of V_a
            // and none of the induced velocity's fall, does not.
            prop.Aero(w25, 0, out double tStatic, out _);
            prop.Aero(w25, va, out double tModel, out _);
            double[] jTable = d.Props.InflowAdvanceRatio;
            double theta = jTable[jTable.Length - 1] / (0.75 * Math.PI); // zero thrust at J0: V_a = θ·Ω·0.75R
            double re = 0.75 * d.Props.Diameter / 2, omegaR = w25 * re;
            double vi0 = Math.Sqrt(tStatic / (2 * Rho * area));
            double lift = tStatic / (w25 * w25 * (theta - vi0 / omegaR));
            double a2 = 2 * Rho * area, b = 2 * Rho * area * va + lift * w25 / re, c = lift * w25 * va / re - lift * w25 * w25 * theta;
            double viBem = (-b + Math.Sqrt(b * b - 4 * a2 * c)) / (2 * a2);
            double tMomentum = a2 * viBem * (viBem + va);
            double tLinear = tStatic * (1 - va / (w25 / (2 * Math.PI) * d.Props.Pitch));
            TestContext.Out.WriteLine($"at {F(w25 * 60 / (2 * Math.PI), "0")} rpm: the model's derate {F(tModel / tStatic)}, momentum theory's (blade element "
                + $"with v_i {F(viBem, "0.00")} m/s) {F(tMomentum / tStatic)}, design 5's linear form {F(tLinear / tStatic)}");
            Assert.That(tModel, Is.EqualTo(tMomentum).Within(0.10 * tMomentum), "the available thrust is momentum theory's at that inflow");
            Assert.That(Math.Abs(tLinear - tMomentum), Is.GreaterThan(0.10 * tMomentum), "a known-wrong derate fails the same check");

            // The heavy fast corner (PHB-03's L11 case): 6.5 kg held level at 25 m/s on the 22.7° disc, drag aside.
            double need = 6.5 * G / Math.Cos(tilt);
            TestContext.Out.WriteLine($"the 6.5 kg corner at 25 m/s needs {F(need / G, "0.00")} kgf of thrust, {F(4 * t25 / G, "0.00")} kgf available at full duty "
                + $"({(4 * t25 > need ? "feasible" : "infeasible")} before drag, 1.5)");
        }
    }
}
