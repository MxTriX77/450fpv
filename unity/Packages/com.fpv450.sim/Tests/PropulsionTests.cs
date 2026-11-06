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

        internal static MassModel Heavy
        {
            get
            {
                MassModel m = L6;
                m.SetPartMass(Drone.Payload.Part, 2.0 + 6.5 - m.Properties.Mass);
                return m;
            }
        }

        internal static FlightCore Core(MassModel mass, double charge, Quat attitude = default, Double3 velocity = default, PropulsionSpec propulsion = null,
            FcSettings? settings = null, AirframeModesSpec modes = null)
        {
            if (attitude.W == 0 && attitude.X == 0 && attitude.Y == 0 && attitude.Z == 0) attitude = Quat.Identity;
            return new FlightCore(new FlightSetup
            {
                Seed = 1, World = FlightWorld.Floor(), Mass = mass, Propulsion = propulsion ?? Drone.BuildPropulsion(),
                StartPosition = new Double3(0, 500, 0), StartAttitude = attitude, StartVelocity = velocity, StartPackCharge = charge,
                Settings = settings ?? FcSettings.Default, Modes = modes,
            });
        }

        // An ESC with current protection (BLHeli_32's, AM32's), A: p6-flight-core 1.5's policy at the motor's 40 A, model
        // 22's drone; the class's published ESC senses no current (drone.json, D-066), so the drone's own has none.
        const double Protection = ShakeScenario.Protection;

        // The drone's propulsion with each ESC's current protection at Protection, or with no rotor in-plane drag (the
        // rate rig of a step-response test, where the tumbling body's own air does not reach the loop).
        internal static PropulsionSpec Protected()
        {
            PropulsionSpec p = Drone.BuildPropulsion();
            for (int i = 0; i < 4; i++) p.Motors[i].EscCurrentLimit = Protection;
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
        internal static void Arm(FlightCore core)
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
                        // Since model 64 the canister's pack rides forward: the CoM 5.1 mm forward of the hubs' centre over their
                        // 0.160 m arm, 3.2 % (0.32 % on the old coil, 0.5 mm forward, the stack's electronics alone).
                        Assert.That(h.FrontOverRear, Is.EqualTo(0.0318).Within(0.0005),
                            "D-050: the front motors 3.2 % over the mean at L4 (the CoM 5.1 mm forward: the coil's pack and canister, the stack)");
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

        // ---- The hover stick by load (D-066) ----

        // The pilot (D-066): drones of the class lift off at 30–45 % throttle, "based on the exact model", a loaded one
        // being about 3.5–4 kg. The throttle the pilot reads is the stick past min_check, the throttle curve's input (rc.c
        // updateRcCommands; the OSD's calculateThrottlePercent). Each load's hover is held by the closed-loop pilot, and its
        // mixer throttle read back through three curves: the pilot's flown set (thr_mid 50, thr_expo 25, fc-profiles §2,
        // which D-066 asks for), the sim's present default (50, 10) and a straight one (50, 0). Liftoff from the ground
        // needs about 1 % less thrust than this hover (the cushion with the hubs 2.3 R up on the legs, T/T∞ = 1/(1 −
        // (R/4z)²) = 1.012), so it lifts off within about half a point of the hover's stick. Computed, never tuned: the
        // pilot's band is a check, and the test pins what the physics gives (to half a point) so a change is seen.
        static readonly (string name, double mid, double expo)[] Curves = { ("the pilot's 50/25", 50, 25), ("the default 50/10", 50, 10), ("straight 50/0", 50, 0) };

        static double ThrottlePercent(double stick) =>
            100 * (Math.Clamp(1000 + 1000 * stick, FcSettings.Default.min_check, 2000) - FcSettings.Default.min_check) / (2000 - FcSettings.Default.min_check);

        static FlightController CurveController(double mid, double expo)
        {
            FcSettings s = FcSettings.Default;
            s.thr_mid = mid;
            s.thr_expo = expo;
            return Core(L4, 1, settings: s).Controller;
        }

        // The throttle the pilot reads, %, holding a hover of this mass on a pack at this charge through the pilot's curve;
        // and the mixer throttle.
        static double HoverPercent(MassModel mass, double charge, FlightController pilotsCurve, out Hover h)
        {
            FlightCore core = Core(mass, charge);
            Arm(core);
            h = HoldHeight(core);
            return ThrottlePercent(StickFor(pilotsCurve, h.Throttle));
        }

        [Test]
        public void TheHoverStickByLoad()
        {
            MassModel empty = Drone.BuildMass(new Loadout { Payload = 0, Legs = true });
            var loads = new (string name, MassModel mass, double pinned)[] { ("no bottle", empty, 36.8), ("a 1 kg bottle", L5, 44.9), ("a 2 kg bottle", L6, 52.8) };
            FlightController pilots = CurveController(50, 25);
            var controllers = new FlightController[Curves.Length];
            for (int c = 0; c < Curves.Length; c++) controllers[c] = CurveController(Curves[c].mid, Curves[c].expo);
            double last = 0;
            var full = new List<(string name, double percent, double pinned)>();
            foreach (var l in loads)
            {
                foreach (double charge in new[] { 1.0, 0.5 })
                {
                    double percent = HoverPercent(l.mass, charge, pilots, out Hover h);
                    var line = new System.Text.StringBuilder();
                    for (int c = 0; c < Curves.Length; c++)
                        line.Append($"{(c > 0 ? ", " : "")}{Curves[c].name} {F(ThrottlePercent(StickFor(controllers[c], h.Throttle)), "0.0")} %");
                    TestContext.Out.WriteLine($"{l.name}, legs on, the coil full: {F(l.mass.Properties.Mass, "0.000")} kg, charge {F(charge, "0.0")}: the hover's throttle "
                        + $"{line}; Betaflight's mixer {F(100 * h.Throttle, "0.0")} %, ESC duty {F(100 * h.Duty, "0.0")} %, {F(h.Rpm, "0")} rpm, {F(h.Amps, "0.0")} A at {F(h.Volts, "0.00")} V"
                        + $"{(charge == 1.0 ? $"; the pilot's 30–45 %: {(percent >= 30 && percent <= 45 ? "inside" : "outside")}" : "")}");
                    if (charge == 1.0) full.Add((l.name, percent, l.pinned));
                }
            }
            foreach (var (name, percent, pinned) in full)
            {
                Assert.That(percent, Is.EqualTo(pinned).Within(0.5), name + ": the hover's throttle through the pilot's curve, as computed");
                Assert.That(percent, Is.GreaterThan(last), name + ": a heavier drone needs more");
                last = percent;
            }
            Assert.That(full[0].percent, Is.InRange(30, 45), "the drone with no bottle lifts off inside the pilot's 30–45 % (D-066)");

            // The heaviest drone that hovers at the pilot's 45 % on a full pack (bisection on the bottle's mass).
            double lo = 0, hi = 2;
            for (int it = 0; it < 10; it++)
            {
                double mid = (lo + hi) / 2;
                if (HoverPercent(Drone.BuildMass(new Loadout { Payload = mid, Legs = true }), 1.0, pilots, out _) <= 45) lo = mid; else hi = mid;
            }
            double heaviest = Drone.BuildMass(new Loadout { Payload = lo, Legs = true }).Properties.Mass;
            TestContext.Out.WriteLine($"the pilot's 45 % holds a hover on a full pack up to {F(heaviest, "0.000")} kg, a {F(lo, "0.00")} kg bottle");
            Assert.That(lo, Is.InRange(0.5, 1.5), "45 % holds between a half and a 1.5 kg bottle, as computed");
        }

        // ---- Battery sag compensation (1.4b) ----

        // vbat_sag_compensation 100 holds the hover throttle from a full pack to 20 % charge; off, the sagging pack
        // needs more (the known-wrong model). The cells forced to the pack's 6, as a Li-ion pilot sets them: Betaflight
        // counts a 6S Li-ion below about 3.58 V a cell as 5 (FilterChainTests).
        [Test]
        public void SagCompensationHoldsTheHoverThrottle()
        {
            double[] charges = { 1.0, 0.6, 0.2 };
            foreach (double compensation in new[] { 100.0, 0.0 })
            {
                FcSettings settings = FcSettings.Default;
                settings.vbat_sag_compensation = compensation;
                settings.force_battery_cell_count = 6;
                double lo = double.MaxValue, hi = 0;
                foreach (double charge in charges)
                {
                    FlightCore core = Core(L6, charge, settings: settings);
                    Arm(core);
                    Hover h = HoldHeight(core);
                    double attenuation = core.Controller.SagAttenuation(core.State.Fc);
                    TestContext.Out.WriteLine($"vbat_sag_compensation {compensation}, L6 at charge {F(charge, "0.0")}: hover stick {F(h.Stick, "0.0000")}, "
                        + $"mixer throttle {F(h.Throttle, "0.0000")}, ESC duty {F(100 * h.Duty, "0.0")} % at {F(h.Volts, "0.00")} V "
                        + $"(the sag filter's {F(core.State.Fc.SagVolts, "0.00")} V, {core.State.Fc.Cells} cells), the motors' top taken {F(100 * attenuation, "0.0")} %");
                    Assert.That(h.Climb, Is.LessThan(0.02), "a steady hover");
                    lo = Math.Min(lo, h.Stick);
                    hi = Math.Max(hi, h.Stick);
                }
                double spread = (hi - lo) / lo;
                TestContext.Out.WriteLine($"vbat_sag_compensation {compensation}: the hover stick spans {F(100 * spread, "0.00")} % from full to 20 % charge");
                if (compensation > 0) Assert.That(spread, Is.LessThan(0.02), "compensated, the hover throttle holds within 2 %");
                else Assert.That(spread, Is.GreaterThan(0.05), "uncompensated, the sagging pack needs more throttle");
            }
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
        internal static FlightState FullDuty(Propulsion prop, double charge, double seconds, out RotorLoads last)
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
            // The drone's ESCs, which sense no current (drone.json, D-066), and with 40 A current protection (model 22's policy).
            foreach (bool limited in new[] { false, true })
            {
                var prop = new Propulsion(limited ? Protected() : Drone.BuildPropulsion());
                FullDuty(prop, 1, 2, out RotorLoads loads);
                double total = (loads.Thrust.RearRight + loads.Thrust.FrontRight + loads.Thrust.RearLeft + loads.Thrust.FrontLeft) / loads.SubSteps;
                FullDutySteadyState(1, out double perMotor, out double volts, out double packAmps, limited ? Protection : double.NaN);
                double weight4 = L4.Properties.Mass * G, weight5 = L5.Properties.Mass * G;
                TestContext.Out.WriteLine($"full duty, full pack, {(limited ? "40 A current protection" : "the drone's ESCs, no current limit")}: {F(total / G, "0.00")} kgf "
                    + $"({F(total / 4 / G, "0.00")} kgf a motor), {F(loads.Volts, "0.00")} V, {F(loads.Amps0 + loads.Amps1, "0.0")} A from the packs, "
                    + $"limited ESCs 0x{loads.Limited:X}; thrust over weight {F(total / weight4, "0.00")} at L4, {F(total / weight5, "0.00")} at L5; "
                    + $"the independent steady state {F(perMotor / G, "0.000")} kgf a motor at {F(volts, "0.00")} V, {F(packAmps, "0.0")} A");
                Assert.That(total / 4, Is.EqualTo(perMotor).Within(0.01 * perMotor), "the dynamic model settles on the DC motor's steady state");
                Assert.That(loads.Volts, Is.EqualTo(volts).Within(0.01 * volts));
                Assert.That(loads.Amps0 + loads.Amps1, Is.EqualTo(packAmps).Within(0.01 * packAmps));
                Assert.That(loads.Limited, Is.EqualTo(limited ? 0xF : 0), "full duty passes 40 A, so the protection holds every motor");
                Assert.That(total / G, limited ? Is.InRange(9.0, 12.5) : Is.InRange(13.0, 17.0),
                    limited ? "K§5.3's 9.0–12.5 kgf at a 40 A maximum"
                        : "a 10-inch 2-blade on a 900 KV 3115 at 6S: the published benches' 3.5–4.2 kgf a motor at full throttle (D-066)");
                if (!limited) Assert.That(packAmps / 4, Is.LessThan(60), "inside the class's published ESC's 60 A a motor, continuous");
            }
        }

        // An ESC's current protection: a spin-up from idle to full duty never lets a motor's current pass it, and the
        // drone's ESC, which has none, draws far more in the spin-up than at its steady full duty.
        [Test]
        public void TheEscHoldsTheMotorAtItsCurrentLimit()
        {
            DroneSpec d = Drone;
            DroneMotor m = d.Motors[0];
            double ke = 60 / (2 * Math.PI * m.Kv);
            var peaks = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                var prop = new Propulsion(pass == 0 ? Protected() : d.BuildPropulsion());
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
                        double volts = loads.Volts, held = (loads.Limited & 1) != 0 ? Protection : double.NaN;
                        double free = (s.EscCommand.RearRight * volts - ke * w) / m.Resistance;
                        peaks[pass] = Math.Max(peaks[pass], double.IsNaN(held) ? free : held);
                    }
                }
            }
            TestContext.Out.WriteLine($"idle to full duty: the motor's peak current {F(peaks[0], "0.0")} A with {F(Protection, "0")} A current protection, "
                + $"{F(peaks[1], "0.0")} A on the drone's ESC, which has none");
            Assert.That(double.IsPositiveInfinity(m.EscCurrentLimit), Is.True, "the class's published ESC senses no current (D-066)");
            Assert.That(peaks[0], Is.LessThanOrEqualTo(Protection * (1 + 1e-12)), "the protection holds the current");
            Assert.That(peaks[1], Is.GreaterThan(1.5 * Protection), "the spin-up's transient current");
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
            // Near idle, at hover and near full: 0.80, below full duty's top speed (the drone's ESC has no current limit,
            // TheEscHoldsTheMotorAtItsCurrentLimit).
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

        static void Step(Propulsion prop, ref FlightState s, in Corners duty, bool driving = true) => Step(prop, ref s, duty, driving, out _);

        static void Step(Propulsion prop, ref FlightState s, in Corners duty, bool driving, out RotorLoads loads)
        {
            prop.BeginStep(s);
            loads = new RotorLoads();
            for (int j = 0; j < FlightController.LoopsPerStep; j++) prop.SubStep(ref s, duty, driving, FlightController.LoopDt, ref loads);
        }

        // ---- The ESC on a disarm (p6-flight-core 1.6c, EscDisarm) ----

        static PropulsionSpec Disarming(EscDisarm rule)
        {
            PropulsionSpec p = Drone.BuildPropulsion();
            for (int i = 0; i < 4; i++) p.Motors[i].EscDisarm = rule;
            return p;
        }

        // Every motor settled at the hover's duty on a body held at rest, then disarmed: the rear right rotor's speed after
        // each 1 ms step (rad/s), and the packs' current after each.
        static (List<double> speed, List<double> amps) Disarm(EscDisarm rule, int steps, Action<Propulsion> damage = null)
        {
            var prop = new Propulsion(Disarming(rule));
            damage?.Invoke(prop);
            var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 1, PackCharge1 = 1 };
            var hover = new Corners { RearRight = 0.47, FrontRight = 0.47, RearLeft = 0.47, FrontLeft = 0.47 };
            for (int k = 0; k < 3000; k++) Step(prop, ref s, hover);
            var speed = new List<double> { s.RotorSpeed.RearRight };
            var amps = new List<double> { 0 };
            for (int k = 0; k < steps; k++)
            {
                Step(prop, ref s, default, false, out RotorLoads loads);
                speed.Add(s.RotorSpeed.RearRight);
                amps.Add(loads.Amps0 + loads.Amps1);
            }
            return (speed, amps);
        }

        static int FirstBelow(List<double> trace, double w) => trace.FindIndex(x => x < w);

        [Test]
        public void ADisarmedBlheliSBrakesItsMotorAsTheShortedDcMotor()
        {
            // BLHeli_S 16.7 stays in its run loop at zero power on Betaflight's DShot 0, its damping FET shorting the active
            // phases: the motor at d = 0, J·ω̇ = −Ke²/R·ω − Kt·I0 − Q(ω), drawing nothing from the packs. The mechanics
            // integrated here by RK4 at 0.1 ms against the sim; the known-wrong is model 38's freewheel (drag alone).
            DroneSpec d = Drone;
            DroneMotor m = d.Motors[0];
            var prop = new Propulsion(d.BuildPropulsion());
            double ke = 60 / (2 * Math.PI * m.Kv), j = m.RotorInertia;
            (List<double> braked, List<double> amps) = Disarm(EscDisarm.BlheliS, 400);
            double w0 = braked[0];
            Func<double, double> rate = w =>
            {
                prop.Aero(w, 0, out _, out double q);
                return -(ke * ke / m.Resistance * w + ke * m.NoLoadCurrent + q) / j;
            };
            double t = 0, x = w0, half = 0, tenth = 0;
            const double dt = 1e-4;
            while (x > 0.1 * w0)
            {
                double k1 = rate(x), k2 = rate(x + 0.5 * dt * k1), k3 = rate(x + 0.5 * dt * k2), k4 = rate(x + dt * k3);
                x += dt / 6 * (k1 + 2 * k2 + 2 * k3 + k4);
                t += dt;
                if (half == 0 && x < 0.5 * w0) half = t;
            }
            tenth = t;
            double simHalf = FirstBelow(braked, 0.5 * w0) * 1e-3, simTenth = FirstBelow(braked, 0.1 * w0) * 1e-3;
            TestContext.Out.WriteLine($"from {F(w0 * 60 / (2 * Math.PI), "0")} rpm: half speed {F(1000 * simHalf, "0")} ms (mechanics {F(1000 * half, "0.0")}), "
                + $"a tenth {F(1000 * simTenth, "0")} ms (mechanics {F(1000 * tenth, "0.0")})");
            Assert.That(simHalf, Is.EqualTo(half).Within(0.0015), "half speed as the shorted motor's");
            Assert.That(simTenth, Is.EqualTo(tenth).Within(0.0015), "a tenth as the shorted motor's");
            for (int k = 5; k < 100; k++) Assert.That(Math.Abs(amps[k]), Is.LessThan(0.05), $"no supply current while it brakes ({F(amps[k])} A at {k} ms)");

            (List<double> coast, _) = Disarm(EscDisarm.Freewheel, 400);
            double coastHalf = FirstBelow(coast, 0.5 * w0) * 1e-3;
            TestContext.Out.WriteLine($"freewheeling (model 38): half speed {F(1000 * coastHalf, "0")} ms");
            Assert.That(coastHalf, Is.GreaterThan(1.5 * simHalf), "the known-wrong freewheel keeps its speed (and thrust) far longer");
        }

        [Test]
        public void ADamagedRotorBrakesByTheSameLaw()
        {
            // The physics integration 2 at model 44: the disarm's short is the motor's, whatever is left of the blades
            // (p7f-damage's DamagedRotor). With both rear right blades trimmed to 0.85 R the rotor brakes as
            // J'·ω̇ = −Ke²/R·ω − Kt·I0 − η_Q·Q(ω): J' its J less the material lost, η_Q its torque ratio at no inflow. The
            // mechanics integrated by RK4 at 0.1 ms against the sim, within 1 ms (the ESC's 0.4 ms command lag is in it); the
            // known-wrong is the sound rotor's law (its J and Q).
            DroneSpec d = Drone;
            DroneMotor m = d.Motors[0];
            Action<Propulsion> trim = p =>
            {
                for (int b = 0; b < d.Props.Blades; b++) p.Rotor(0).Blade(b).Trim(0.85);
                p.Rebuild(0);
            };
            var sound = new Propulsion(d.BuildPropulsion());
            var damaged = new Propulsion(d.BuildPropulsion());
            trim(damaged);
            damaged.Rotor(0).Ratios(0, 0, out _, out double etaQ);
            double ke = 60 / (2 * Math.PI * m.Kv), jSound = m.RotorInertia, jDamaged = damaged.Inertia(0);
            Assert.That(damaged.Damaged & 1, Is.EqualTo(1), "the rear right rotor is off the sheet");
            (List<double> braked, List<double> amps) = Disarm(EscDisarm.BlheliS, 400, trim);
            double w0 = braked[0];
            (double half, double tenth) Shorted(double j, double torqueRatio)
            {
                Func<double, double> rate = w =>
                {
                    sound.Aero(w, 0, out _, out double q);
                    return -(ke * ke / m.Resistance * w + ke * m.NoLoadCurrent + torqueRatio * q) / j;
                };
                double t = 0, x = w0, h = 0;
                const double dt = 1e-4;
                while (x > 0.1 * w0)
                {
                    double k1 = rate(x), k2 = rate(x + 0.5 * dt * k1), k3 = rate(x + 0.5 * dt * k2), k4 = rate(x + dt * k3);
                    x += dt / 6 * (k1 + 2 * k2 + 2 * k3 + k4);
                    t += dt;
                    if (h == 0 && x < 0.5 * w0) h = t;
                }
                return (h, t);
            }
            (double half, double tenth) = Shorted(jDamaged, etaQ);
            (double soundHalf, double soundTenth) = Shorted(jSound, 1);
            // The sim's crossings between its 1 ms records, linear within the step.
            double Crossing(double w)
            {
                int k = FirstBelow(braked, w);
                return (k - 1 + (braked[k - 1] - w) / (braked[k - 1] - braked[k])) * 1e-3;
            }
            double simHalf = Crossing(0.5 * w0), simTenth = Crossing(0.1 * w0);
            TestContext.Out.WriteLine($"trimmed to 0.85 R: J {F(jDamaged, "0.0000E+0")} kg·m² ({F(jDamaged / jSound, "0.000")} of the sound rotor's), η_Q {F(etaQ, "0.0000")}; "
                + $"from {F(w0 * 60 / (2 * Math.PI), "0")} rpm: half speed {F(1000 * simHalf, "0.0")} ms (mechanics {F(1000 * half, "0.0")}, the sound rotor's {F(1000 * soundHalf, "0.0")}), "
                + $"a tenth {F(1000 * simTenth, "0.0")} ms (mechanics {F(1000 * tenth, "0.0")}, the sound rotor's {F(1000 * soundTenth, "0.0")})");
            Assert.That(simHalf, Is.EqualTo(half).Within(0.001), "half speed as the damaged shorted motor's");
            Assert.That(simTenth, Is.EqualTo(tenth).Within(0.001), "a tenth as the damaged shorted motor's");
            for (int k = 5; k < 100; k++) Assert.That(Math.Abs(amps[k]), Is.LessThan(0.05), $"no supply current while it brakes ({F(amps[k])} A at {k} ms)");
            Assert.That(Math.Abs(soundTenth - simTenth), Is.GreaterThan(0.004), "the known-wrong: the sound rotor's J and torque miss it by four tolerances");
        }

        [Test]
        public void BelowItsMinimumSpeedTheEscLetsTheMotorCoast()
        {
            // BLHeli_S leaves its run loop past a 32 ms commutation period (about 1220 eRPM; Bluejay about 1330) and turns
            // every FET off: from there only the bearings, iron and prop slow the rotor. So the speed's fall below the minimum
            // is the friction's Kt·I0 (plus the prop's small drag), not the short's.
            DroneMotor m = Drone.Motors[0];
            double ke = 60 / (2 * Math.PI * m.Kv), friction = ke * m.NoLoadCurrent / m.RotorInertia;
            foreach ((EscDisarm rule, double erpm) in new[] { (EscDisarm.BlheliS, 1220.0), (EscDisarm.Bluejay, 1330.0) })
            {
                double exit = erpm / (m.MagnetPoles / 2) * 2 * Math.PI / 60;
                (List<double> w, _) = Disarm(rule, 600);
                int k = FirstBelow(w, exit);
                Assert.That(k, Is.GreaterThan(0), $"{rule} reaches its minimum speed");
                // The step after it left the loop: the fall of one step against the friction's and the short's.
                double fall = (w[k + 1] - w[k + 2]) / 1e-3, shorted = friction + ke * ke / m.Resistance * w[k + 1] / m.RotorInertia;
                TestContext.Out.WriteLine($"{rule}: left its run loop at {F(w[k] * 60 / (2 * Math.PI), "0")} rpm after {k} ms; then {F(fall, "0")} rad/s² "
                    + $"(friction {F(friction, "0")}, shorted {F(shorted, "0")})");
                Assert.That(fall, Is.EqualTo(friction).Within(0.1 * friction), $"{rule}: it coasts");
            }
        }

        [Test]
        public void BrakeOnStopShortsTheThreePhasesAfterItsCounter()
        {
            // The compared variants (G7's ESC-firmware question): brake on stop leaves the run loop after 3 ticks of the stop
            // counter (assumed mid-phase: BLHeli_S 80 ms of its 32 ms ticks, Bluejay 40 ms of its 16 ms), then BLHeli_S waits
            // 100 ms with every FET off and Bluejay none, and both turn on the three low-side FETs, a three-phase short. Until
            // the counter each brakes as its firmware's default, bit for bit; BLHeli_S then coasts 100 ms; with the short on
            // the rotor stops and stays stopped.
            (List<double> blheli, _) = Disarm(EscDisarm.BlheliS, 400);
            (List<double> blheliBrake, _) = Disarm(EscDisarm.BlheliSBrakeOnStop, 400);
            (List<double> bluejay, _) = Disarm(EscDisarm.Bluejay, 400);
            (List<double> bluejayBrake, _) = Disarm(EscDisarm.BluejayBrakeOnStop, 400);
            for (int k = 0; k <= 79; k++) Assert.That(blheliBrake[k], Is.EqualTo(blheli[k]), $"BLHeli_S's run loop at {k} ms");
            for (int k = 0; k <= 39; k++) Assert.That(bluejayBrake[k], Is.EqualTo(bluejay[k]), $"Bluejay's run loop at {k} ms");
            TestContext.Out.WriteLine($"BLHeli_S at 80, 179, 182, 200 ms: default {F(blheli[80], "0.0")}, {F(blheli[179], "0.0")}, {F(blheli[182], "0.0")}, {F(blheli[200], "0.0")} rad/s; "
                + $"brake on stop {F(blheliBrake[80], "0.0")}, {F(blheliBrake[179], "0.0")}, {F(blheliBrake[182], "0.0")}, {F(blheliBrake[200], "0.0")}");
            TestContext.Out.WriteLine($"Bluejay at 40, 42, 60 ms: default {F(bluejay[40], "0.0")}, {F(bluejay[42], "0.0")}, {F(bluejay[60], "0.0")} rad/s; "
                + $"brake on stop {F(bluejayBrake[40], "0.0")}, {F(bluejayBrake[42], "0.0")}, {F(bluejayBrake[60], "0.0")}");
            Assert.That(blheliBrake[179] / blheliBrake[80], Is.GreaterThan(0.7), "BLHeli_S coasts in its 100 ms wait");
            Assert.That(blheli[179] / blheli[80], Is.LessThan(0.2), "where its default still brakes in the run loop");
            Assert.That(blheliBrake[182] - blheliBrake[183], Is.GreaterThan(3 * (blheliBrake[178] - blheliBrake[179])), "then the short brakes it");
            Assert.That(bluejayBrake[60], Is.LessThan(bluejay[60]), "Bluejay's three-phase short brakes harder than its run loop's two");
            Assert.That(blheliBrake[400], Is.LessThan(1e-6), "BLHeli_S's rotor stopped");
            Assert.That(bluejayBrake[400], Is.LessThan(1e-6), "Bluejay's rotor stopped");
        }

        // Disarmed 400 ms from the hover's duty on a body held at rest (every ESC past its run loop by then), then every rotor
        // spun to kick rad/s as the air would spin a windmilling prop: the rear right rotor's speed after each 1 ms step from the kick.
        static List<double> KickAfterTheDisarm(EscDisarm rule, double kick, int steps)
        {
            var prop = new Propulsion(Disarming(rule));
            var s = new FlightState { Attitude = Quat.Identity, PackCharge0 = 1, PackCharge1 = 1 };
            var hover = new Corners { RearRight = 0.47, FrontRight = 0.47, RearLeft = 0.47, FrontLeft = 0.47 };
            for (int k = 0; k < 3000; k++) Step(prop, ref s, hover);
            for (int k = 0; k < 400; k++) Step(prop, ref s, default, false);
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = kick;
            var speed = new List<double> { s.RotorSpeed.RearRight };
            for (int k = 0; k < steps; k++)
            {
                Step(prop, ref s, default, false);
                speed.Add(s.RotorSpeed.RearRight);
            }
            return speed;
        }

        [Test]
        public void WithBrakeOnStopOffADisarmedMotorFreewheelsPastItsRunLoop()
        {
            // D-086 (the pilot: brake on stop is off). BLHeli_S 16.7 with Pgm_Brake_On_Stop 0, published: past its run loop
            // (run6_brake_done: a commutation period over 32 ms, about 1220 eRPM) run_to_wait_for_power_on calls switch_power_off
            // and turns on its com FETs only under Pgm_Brake_On_Stop; wait_for_power_on turns no FET on. So a disarmed rotor the
            // air spins again freewheels: friction and drag alone, as model 38's Freewheel, bit for bit (the ESC stays off until armed
            // again: it starts a motor only on power on). Brake on stop (the compared
            // variant) shorts the three phases: the same kick dies at the short's rate. The braking before (damped light's
            // complementary PWM at zero duty) is the firmware's run loop with brake on stop off
            // (ADisarmedBlheliSBrakesItsMotorAsTheShortedDcMotor).
            const double kick = 300;
            List<double> off = KickAfterTheDisarm(EscDisarm.BlheliS, kick, 100);
            List<double> free = KickAfterTheDisarm(EscDisarm.Freewheel, kick, 100);
            List<double> brake = KickAfterTheDisarm(EscDisarm.BlheliSBrakeOnStop, kick, 100);
            TestContext.Out.WriteLine($"kicked to {F(kick, "0")} rad/s 400 ms after the disarm, 50 ms on: brake on stop off {F(off[50], "0.0")}, "
                + $"freewheel {F(free[50], "0.0")}, brake on stop {F(brake[50], "0.0")} rad/s");
            for (int k = 0; k <= 100; k++) Assert.That(off[k], Is.EqualTo(free[k]), $"brake on stop off freewheels, bit for bit ({k} ms)");
            Assert.That(off[50], Is.GreaterThan(0.8 * kick), "it keeps most of its speed over 50 ms");
            Assert.That(brake[50], Is.LessThan(0.5 * off[50]), "the known-wrong for D-086, brake on stop, holds it");
        }

        // ---- The builder's PIDs hold a stable loop ----

        struct StepResult
        {
            public double Stick, Throttle, Peak, Overshoot, Settle, LastMean, LastSpread;
            public int Rise;

            public override string ToString() =>
                $"90 % in {Rise} ms, peak {F(Peak, "0.0")} °/s (overshoot {F(100 * Overshoot, "0.0")} %), within 5 % from {F(1000 * Settle, "0")} ms, "
                + $"the last second {F(LastMean, "0.00")} °/s mean, {F(LastSpread, "0.00")} °/s peak to peak";
        }

        // On a rate rig: a 200 °/s step held 2 s tumbles the body through 400°, and in free air the rotors' in-plane drag
        // of that tumble's own motion swings the rate by tens of °/s (FreeTumble prints it); the loop's own stability
        // is measured without it. The rate is the plant's (the body's), not the filtered gyro.
        static StepResult StepResponse(int axis, FcSettings settings, double escTau = 0, bool modes = false)
        {
            PropulsionSpec propulsion = NoInPlaneDrag();
            if (escTau > 0)
                for (int i = 0; i < 4; i++) propulsion.Motors[i].EscTau = escTau;
            FlightCore core = Core(L4, 1, propulsion: propulsion, settings: settings, modes: modes ? Drone.BuildModes() : null);
            if (modes)
                for (int i = 0; i < core.Modes.Count; i++)
                    TestContext.Out.WriteLine($"  mode {i}: {F(core.Modes.Frequency(i), "0.0")} Hz, ζ {F(core.Modes.Damping(i), "0.000")} (derived)");
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
            var r = new StepResult { Stick = stick, Throttle = h.Stick, Rise = -1 };
            double lastMin = double.MaxValue, lastMax = double.MinValue;
            for (int k = 0; k < rate.Length; k++)
            {
                r.Peak = Math.Max(r.Peak, rate[k]);
                if (Math.Abs(rate[k] - 200) > 10) r.Settle = (k + 1) / 1000.0;
                if (r.Rise < 0 && rate[k] >= 180) r.Rise = k + 1;
                if (k >= 1000)
                {
                    lastMin = Math.Min(lastMin, rate[k]);
                    lastMax = Math.Max(lastMax, rate[k]);
                    r.LastMean += rate[k] / 1000;
                }
            }
            r.Overshoot = (r.Peak - 200) / 200;
            r.LastSpread = lastMax - lastMin;
            return r;
        }

        // The builder's chain with every gyro and D-term filter off (1.4b): the PID reads the mean of the gyro's two
        // 8 kHz samples, as Betaflight does without LPF2.
        static FcSettings NoFilters()
        {
            FcSettings s = FcSettings.Default;
            s.gyro_lpf1_dyn_min_hz = s.gyro_lpf1_static_hz = s.gyro_lpf2_static_hz = 0;
            s.gyro_notch1_hz = s.gyro_notch2_hz = 0;
            s.dyn_notch_count = 0;
            s.dshot_bidir = false;
            s.dterm_notch_hz = s.dterm_lpf1_dyn_min_hz = s.dterm_lpf1_static_hz = s.dterm_lpf2_static_hz = 0;
            return s;
        }

        // The step with the filter chain built up group by group, from none to the builder's whole chain, then the
        // chain without feedforward: printed, the whole chain asserted.
        static List<(string label, FcSettings settings)> FilterStages()
        {
            FcSettings d = FcSettings.Default, s = NoFilters();
            var stages = new List<(string, FcSettings)> { ("no filters (the 8 kHz samples averaged)", s) };
            s.gyro_lpf1_dyn_min_hz = d.gyro_lpf1_dyn_min_hz;
            s.gyro_lpf1_static_hz = d.gyro_lpf1_static_hz;
            s.gyro_lpf2_static_hz = d.gyro_lpf2_static_hz;
            stages.Add(("+ gyro LPF1 and LPF2", s));
            s.gyro_notch1_hz = d.gyro_notch1_hz;
            stages.Add(("+ gyro notch", s));
            s.dshot_bidir = d.dshot_bidir;
            stages.Add(("+ RPM filter", s));
            s.dyn_notch_count = d.dyn_notch_count;
            stages.Add(("+ dynamic notch (the gyro's whole chain)", s));
            s.dterm_lpf1_dyn_min_hz = d.dterm_lpf1_dyn_min_hz;
            s.dterm_lpf1_static_hz = d.dterm_lpf1_static_hz;
            s.dterm_lpf2_static_hz = d.dterm_lpf2_static_hz;
            stages.Add(("+ D-term LPF1 and LPF2", s));
            s.dterm_notch_hz = d.dterm_notch_hz;
            stages.Add(("+ D-term notch: the builder's chain", s));
            s.f_roll = s.f_pitch = 0;
            stages.Add(("the builder's chain without feedforward", s));
            return stages;
        }

        [TestCase(0, TestName = "The builder's PIDs hold a stable loop: roll")]
        [TestCase(1, TestName = "The builder's PIDs hold a stable loop: pitch")]
        public void TheBuildersPidsHoldAStableLoop(int axis)
        {
            FreeTumble(axis);
            string name = axis == 0 ? "roll" : "pitch";
            StepResult bare = default, chain = default;
            foreach ((string label, FcSettings settings) in FilterStages())
            {
                StepResult r = StepResponse(axis, settings);
                TestContext.Out.WriteLine($"{name} step to 200 °/s at hover, {label}: {r}");
                if (label.StartsWith("no filters", StringComparison.Ordinal)) bare = r;
                if (label.EndsWith("the builder's chain", StringComparison.Ordinal)) chain = r;
            }
            // The scenario's stability on the builder's whole chain: settled, inside the first second, no sustained
            // oscillation.
            Assert.That(chain.LastMean, Is.EqualTo(200).Within(10), "settles within 5 % of the setpoint");
            Assert.That(chain.Settle, Is.LessThan(1.0), "within 5 % for good inside the first second");
            Assert.That(chain.LastSpread, Is.LessThan(10), "no sustained oscillation");
            // Its overshoot below 25 % holds for the plant and the PIDs alone (1.4, 1.5); the chain's phase lag
            // (10.6 ms at 10 Hz on the gyro, 18.8 ms on the D term's input: FilterChainTests) takes it past that (1.4b,
            // D-045). Bounded here so that a change of it is seen: 20–35 % on the ESC's 0.4 ms command lag (p7b-airframe
            // 1.0), across its 0.2–0.7 ms range; design 5's assumed 5 ms, the known-wrong lag, falls out of it.
            Assert.That(bare.Overshoot, Is.LessThan(0.25), "without the filters: overshoots by less than 25 %");
            Assert.That(chain.Overshoot, Is.InRange(0.20, 0.40), "the builder's chain: measured 34.3 % (roll) on the class's ESC, which no current limit holds in the step (D-066; 32.5 % with 40 A protection on D-050's layout)");
            foreach (double tau in new[] { 0.0002, 0.0007, 0.005 })
            {
                StepResult r = StepResponse(axis, FcSettings.Default, tau);
                TestContext.Out.WriteLine($"{name} step, the builder's chain, τ_esc {F(1000 * tau, "0.0")} ms: {r}");
                if (tau < 0.001) Assert.That(r.Overshoot, Is.InRange(0.20, 0.40), "inside the band across τ_esc's range (33.8–35.8 % roll on the class's ESC, D-066)");
                else Assert.That(r.Overshoot, Is.GreaterThan(0.35), "the known-wrong 5 ms lag overshoots past the band");
            }
            // The same step on the elastic airframe (AirframeModes, 1.4; D-050's strapped loads): the bottle's mount on top is
            // its lowest mode, the coil's beneath next, the arms at the builder's notches, and the gyro reads the frame's
            // turn. The loop stays stable and inside the band.
            StepResult elastic = StepResponse(axis, FcSettings.Default, modes: true);
            TestContext.Out.WriteLine($"{name} step, the builder's chain, the airframe's modes on: {elastic}");
            Assert.That(elastic.LastMean, Is.EqualTo(200).Within(10), "the modes on: settles within 5 % of the setpoint");
            Assert.That(elastic.Settle, Is.LessThan(1.0), "the modes on: within 5 % for good inside the first second");
            Assert.That(elastic.LastSpread, Is.LessThan(10), "the modes on: no sustained oscillation");
            Assert.That(elastic.Overshoot, Is.InRange(0.20, 0.35), "the modes on: inside the band");
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
            double protectedKick = YawKick(Protected(), fc);
            TestContext.Out.WriteLine($"yaw authority at hover: {F(steady, "0")} °/s² steady (drag torque), {F(kick, "0")} °/s² over full yaw's first 50 ms "
                + $"(with the rotors' spin-up; {F(protectedKick, "0")} °/s² with 40 A current protection)");
            Assert.That(steady, Is.InRange(450, 880), "fc-profiles §3: about 500–800 °/s² (FpvPrep's estimate; 831 on the class's ESC, D-066, model 22's 745 with 40 A protection)");
            Assert.That(protectedKick, Is.LessThan(kick), "current protection would take from the spin-up's kick");
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
