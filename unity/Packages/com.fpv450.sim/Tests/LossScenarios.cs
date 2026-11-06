using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.6's losses' flights (prop-motor-damage's "Breakage"): what losing a whole prop, a stopped motor or one blade of
    /// the two does to the repository's drone, from a still-air hover 300 m up or from clip P's cruise, at a payload of the
    /// setup's range (APP-03: 0–2 kg), with the sticks centred (acro: the rates held at zero, the throttle where the pilot left
    /// it) or the simulated pilot still flying (SimPilot: holding its point, or its cruise's velocity). ShaftScenarios.Setup's
    /// flight (the factory draws, the vortex rings, the wakes, the flapping, the modes, the contacts with their bells, shafts
    /// and bearings, since model 75 the motors' heat and dirt) with model 72's arms in their plane and the gyro's range (since model
    /// 80 the arm breaking on its strains, damped at 0.007, and the gyro's anti-alias filter), on the
    /// pilot's controller (no airmode, D-086); clean flies model 68's clean sensors (the
    /// arms stiff in their plane, no gyro range: GoldenFlights.CleanSensors), the known-wrong. LossTests asserts them;
    /// tools/flightbench --damage prints them over the loads and the seeds.
    /// </summary>
    public static class LossScenarios
    {
        /// <summary>The motor that loses: rear right, clockwise (ShaftScenarios.Corner).</summary>
        public const int Corner = 0;

        /// <summary>The loss's flight after the event, s; the hover before it, s; the cruise's run-up, s.</summary>
        public const double After = 1, HoverBefore = 3, CruiseBefore = 12;

        /// <summary>Clip P's cruise, m/s along the course (WindHarness.ReleaseSpeed, PHY-17a's published cruise).</summary>
        public const double Cruise = WindHarness.ReleaseSpeed;

        public enum Loss { Prop, Stopped, Blade }

        const double RadToDeg = 57.29577951308232, G = 9.80665;

        public struct Result
        {
            public Loss Loss;
            public bool Cruising, Centred, Clean, Chop, Airmode, Heat;
            public double Payload;
            public ulong Seed;

            /// <summary>The flight after the loss, s (After; a chop's ChopSeconds).</summary>
            public double Seconds;

            /// <summary>The tilt (body up from the vertical) at half the flight and at its end, degrees; when it first passed 45°, s after the loss
            /// (NaN never); how far the dead corner fell against the body's centre, m: its hub's height over the body's origin at the
            /// loss less its lowest over the second (positive: the drone rolled toward it).</summary>
            public double TiltHalf, TiltOne, TiltAt, CornerDrop;

            /// <summary>The yaw rate at the end and its largest over the flight, °/s (abs); the height lost over it, m.</summary>
            public double YawOne, YawPeak, Drop;

            /// <summary>The losing corner's ESC command, its mean over the second and the share of the second it spent at full
            /// (past 0.98); and its speed's mean, rad/s.</summary>
            public double Command, Saturated, Speed;

            /// <summary>The gyro's largest axis before its range, °/s, and the share of steps it passed 2000 °/s; the
            /// accelerometer's largest specific force at the body's origin, g (its range is ±16 g; acro reads no accelerometer).</summary>
            public double GyroPeak, GyroClipped, AccelPeak;

            /// <summary>The camera's 50–200 Hz band over the second, m/s² RMS (VibrationBands' Y).</summary>
            public double Band;

            /// <summary>The losing arm's largest load (its root's larger share of its strengths, ArmFlex), its largest twist, degrees, and its
            /// tip's largest bend across it, mm; whether it broke, and when, s after the loss (NaN never).</summary>
            public double ArmLoad, Twist, Bend, BrokeAt;

            /// <summary>The losing arm's root at its largest twist and bend (model 80; ArmFlex): its in-plane shear strain, its short
            /// edge's interlaminar shear stress and its linear in-plane shear stress (model 79's law), Pa, and its bend's root stress,
            /// Pa; each law's share of the plate's strengths (AirframeModesSpec's defaults, whatever the flight's law and strengths, so
            /// an unbreakable arm prints how far past them it went).</summary>
            public double Strain, EdgeStress, LinearStress, BendStress, StrainShare, EdgeShare, LinearShare, BendShare;

            public string Arm() =>
                $"rr's arm: twist {Twist:F2}°, γ {Strain * 100:F2} % ({StrainShare:F3} of its failure strain), edge τ {EdgeStress / 1e6:F1} MPa "
                + $"({EdgeShare:F3} of ILSS), linear τ {LinearStress / 1e6:F1} MPa ({LinearShare:F3} of model 79's 90 MPa), bend {Bend:F2} mm "
                + $"({BendShare:F3} of its flexural strength); load {ArmLoad:F3}{(double.IsNaN(BrokeAt) ? ", whole" : $", BROKEN at {BrokeAt * 1e3:F0} ms")}";

            /// <summary>The ESC's commutation: the losing rotor's largest relative speed change over one commutation (60° electrical)
            /// in the second, against the share of a commutation period its zero crossing may move before the ESC misses it.</summary>
            public double CommutationChange;

            /// <summary>The losing rotor's thrust and torque shares left (a blade: its DamagedRotor's at the loss), and the lost
            /// blade's pull at the hub at the loss, N.</summary>
            public double ThrustShare, TorqueShare, Pull;

            public string Describe() =>
                $"{Name(Loss)} {(Cruising ? "in the cruise" : "in the hover")}, {Payload:F1} kg, {(Centred ? "sticks centred" : "SimPilot flying")}"
                + $"{(Chop ? $", the throttle at {ChopThrottle} from {ChopFrom} s for {ChopFor} s" : "")}{(Airmode ? ", airmode on" : "")}{(Heat ? "" : ", no heat")}{(Clean ?", clean sensors (known-wrong)" : "")}, seed {Seed}: tilt {TiltHalf:F1}° at {Seconds / 2} s, {TiltOne:F1}° at {Seconds} s, "
                + $"{(double.IsNaN(TiltAt) ? "never 45°" : $"45° at {TiltAt * 1e3:F0} ms")}, rr's corner down {CornerDrop * 1e3:F0} mm; yaw {YawOne:F0}°/s at the end "
                + $"(peak {YawPeak:F0}); {Drop:F2} m lost; rr's command {Command:F3} ({Saturated * 100:F0} % at full), {Speed:F0} rad/s; gyro peak {GyroPeak:F0}°/s "
                + $"({GyroClipped * 100:F1} % clipped), accel peak {AccelPeak:F1} g; camera band {Band:F2} m/s²; rr's arm: load {ArmLoad:F3}, twist {Twist:F2}°, "
                + $"bend {Bend:F2} mm{(double.IsNaN(BrokeAt) ? "" : $", BROKEN at {BrokeAt * 1e3:F0} ms")}; commutation change {CommutationChange * 100:F3} %";

            static string Name(Loss l) => l == Loss.Prop ? "prop lost" : l == Loss.Stopped ? "motor stopped" : "blade lost";
        }

        /// <summary>The chop (p7f-damage 1.6's losses): with the sticks centred, the throttle stick taken to ChopThrottle ChopFrom s after
        /// the loss for ChopFor s, then back to the hover's, and the flight measured over ChopSeconds; a pilot easing off over a
        /// shaking drone, or a descent. It takes the losing rotor down through its arm's twist mode (88 Hz, about 550 rad/s).</summary>
        public const double ChopThrottle = 0.15, ChopFrom = 0.3, ChopFor = 0.5, ChopSeconds = 2;

        /// <summary>The share of a commutation period a zero crossing may move before the ESC misses it, 0.25 (published, the BLHeli_S
        /// manual: the default medium timing advances the commutation 15° of its 60°; a crossing later or earlier than that window
        /// is met at the wrong phase).</summary>
        public const double CommutationWindow = 0.25;

        /// <summary>A lost prop's or a stopped motor's account (physics and practice: lift and yaw torque lost together at one corner,
        /// which three rotors cannot balance under Betaflight's mixer; the pilot's words, the corner drops completely): the
        /// dead corner's motor runs at full for most of the second and its corner still falls (at least 30 mm against the body's
        /// centre), and the drone falls at least 1.5 m in the second (a hover holds within a few decimetres; a free fall is 4.9 m).</summary>
        public static bool DropsItsCorner(in Result r) => r.Saturated >= 0.5 && r.CornerDrop >= 0.03 && r.Drop >= 1.5;

        /// <summary>A lost blade's account (p7f-damage 1.6's losses, what model 72 computes): the remaining blade lifts the corner,
        /// its motor's command rises and stays short of full, the gyro stays inside its range, the arm holds, and the drone keeps
        /// its attitude through the second (never 45°).</summary>
        public static bool BladeHolds(in Result r) => double.IsNaN(r.TiltAt) && double.IsNaN(r.BrokeAt) && r.GyroClipped == 0 && r.Saturated < 0.5 && r.Drop < 1;

        /// <summary>A lost blade's chop (ChopThrottle): the losing rotor passes its arm's twist mode, the arm breaks at its clamp
        /// (ArmBroken), its prop is lost, and when the throttle comes back the drone rolls toward that corner past 45°.</summary>
        public static bool ChopBreaksTheArm(in Result r) => !double.IsNaN(r.BrokeAt) && !double.IsNaN(r.TiltAt) && r.TiltAt > r.BrokeAt;

        /// <summary>The arm's plate that never breaks (the chop's table, model 80): every strength infinite, so the flight shows how far
        /// past each law's strength its twist and bend went.</summary>
        public static void Unbreakable(FlightSetup s)
        {
            s.Modes.ArmFlexuralStrength = s.Modes.ArmShearStrength = s.Modes.ArmInterlaminarStrength = s.Modes.ArmShearFailureStrain = double.PositiveInfinity;
        }

        /// <summary>The quasi-isotropic arm (0/±45/90 woven), a variant only, never the default (the reading's §2, derived: G₁₂ about
        /// 17 GPa from E/(2(1 + ν)), E about 45 GPa, the mean of the 0/90 and ±45 values): its twist mode moves to about 171 Hz, into the
        /// hover's 1/rev. Its interlaminar strength and failure strain are kept (assumed: the same matrix).</summary>
        public static void QuasiIsotropic(FlightSetup s)
        {
            s.Modes.ArmShearModulus = 17e9;
            s.Modes.ArmModulus = 45e9;
        }

        /// <summary>
        /// The chop's arm on sourced data (model 80; docs/research/arm-plates-and-blade-loss.md §6–7), printed: a lost blade's chop
        /// (and a lost blade at steady throttle) in the hover at a payload on a seed, the sticks centred, each row's plate and law:
        /// the arm unbreakable at ζ 0.005, 0.007 (the flown), 0.01 and model 79's 0.02, so each law's share of its strength shows how
        /// far past it the twist went; then the flown outcomes: model 80's law at ζ 0.005, 0.007 and 0.01, the strain alone (the
        /// interlaminar check off), model 79's arm and gyro whole (GoldenFlights.Model79Arm); then the quasi-isotropic plate
        /// (QuasiIsotropic), a variant only, unbreakable and flown, in the chop and at steady throttle.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<(string Row, Result Result)> ChopTable(DroneSpec spec, double payload, ulong seed)
        {
            Action<FlightSetup> Damping(double z) => s => s.Modes.ArmDamping = z;
            Action<FlightSetup> Plus(Action<FlightSetup> a, Action<FlightSetup> b) => s => { a(s); b(s); };
            Result Chop(Action<FlightSetup> e) => Fly(spec, Loss.Blade, false, payload, true, seed, chop: true, edit: e);
            foreach (double z in new[] { 0.005, 0.007, 0.01, GoldenFlights.Model79ArmDamping })
                yield return ($"the chop, unbreakable, ζ {z}", Chop(Plus(Damping(z), Unbreakable)));
            foreach (double z in new[] { 0.005, 0.007, 0.01 })
                yield return ($"the chop, model 80's law (strain 4 %, ILSS 60 MPa), ζ {z}", Chop(Damping(z)));
            yield return ("the chop, the strain alone (no interlaminar check), ζ 0.007", Chop(s => s.Modes.ArmInterlaminarStrength = double.PositiveInfinity));
            yield return ("the chop, model 79's arm and gyro (linear 90 MPa, ζ 0.02, no AAF)", Chop(GoldenFlights.Model79Arm));
            yield return ("the chop, model 80's law without the AAF", Chop(s => s.GyroAntiAlias = 0));
            yield return ("quasi-isotropic (G₁₂ 17 GPa, E 45 GPa; a variant), the chop, unbreakable", Chop(Plus(QuasiIsotropic, Unbreakable)));
            yield return ("quasi-isotropic, the chop, model 80's law", Chop(QuasiIsotropic));
            yield return ("quasi-isotropic, steady throttle (no chop), model 80's law",
                Fly(spec, Loss.Blade, false, payload, true, seed, edit: QuasiIsotropic));
        }

        /// <summary>The flight's setup on a seed at a payload; clean: model 68's clean sensors; heat false: the motors without their
        /// heat and dirt (GoldenFlights.WithoutTheHeat, model 72's motors).</summary>
        public static FlightSetup Setup(DroneSpec spec, ulong seed, double payload, bool clean, bool heat = true)
        {
            FlightSetup s = ShaftScenarios.Setup(spec, seed);
            Loadout l = spec.Nominal;
            l.Payload = payload;
            s.Mass = spec.BuildMass(l);
            s.Contacts = spec.BuildContacts(l, ContactScenarios.RepositoryMaterials(out ulong h), h);
            if (clean) GoldenFlights.CleanSensors(s);
            if (!heat) GoldenFlights.WithoutTheHeat(s);
            return s;
        }

        /// <summary>The loss flown on a seed: the hover (or the cruise's run-up), the loss at its end, then After s measured (a chop's
        /// ChopSeconds). By default on the pilot's controller (FcSettings.Default: no airmode, D-086) with the motors' heat and dirt
        /// (model 75); airmode true and heat false fly model 72's (Betaflight's airmode on, the motors without their heat).</summary>
        public static Result Fly(DroneSpec spec, Loss loss, bool cruising, double payload, bool centred, ulong seed, bool clean = false, bool chop = false,
            bool airmode = false, bool heat = true, Action<FlightSetup> edit = null)
        {
            FlightSetup setup = Setup(spec, seed, payload, clean, heat);
            setup.Settings.airmode = airmode;
            edit?.Invoke(setup);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            if (cruising)
            {
                pilot.HoldVelocity = true;
                pilot.TargetVelocity = new Double3(0, 0, Cruise);
            }
            int before = (int)((cruising ? CruiseBefore : HoverBefore) * 1000);
            StickSample last = default;
            for (int k = 0; k < before; k++)
            {
                last = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                core.Advance(last);
            }

            var r = new Result { Loss = loss, Cruising = cruising, Centred = centred || chop, Clean = clean, Chop = chop, Airmode = airmode, Heat = heat, Seconds = chop ? ChopSeconds : After, Payload = payload, Seed = seed, TiltAt = double.NaN, BrokeAt = double.NaN, ThrustShare = 1, TorqueShare = 1 };
            Propulsion p = core.Propulsion;
            if (loss == Loss.Prop) p.LoseProp(Corner);
            else if (loss == Loss.Stopped) p.StopEsc(Corner);
            else
            {
                DamagedRotor rotor = p.Rotor(Corner);
                rotor.Blade(0).Trim(spec.Blade.Stations[0]);
                p.Rebuild(Corner);
                r.ThrustShare = rotor.ThrustRatio(0);
                r.TorqueShare = rotor.TorqueRatio(0);
                double w = core.State.RotorSpeed[Corner];
                r.Pull = Math.Sqrt(rotor.UnbalanceP * rotor.UnbalanceP + rotor.UnbalanceQ * rotor.UnbalanceQ) * w * w;
            }

            int steps = (int)(r.Seconds * 1000);
            double y0 = core.State.Position.Y, band = 0, pairs = spec.Motors[Corner].MagnetPoles / 2.0;
            Double3 v = core.State.Velocity;
            double speed = core.State.RotorSpeed[Corner], hub0 = core.State.Attitude.Rotate(p.Hub(Corner)).Y;
            for (int k = 1; k <= steps; k++)
            {
                long us = core.State.Step * FlightCore.StepUs;
                double t = k * FlightCore.Dt, throttle = chop && t > ChopFrom && t <= ChopFrom + ChopFor ? ChopThrottle : last.Throttle;
                core.Advance(centred || chop ? new StickSample { HostUs = us, Arm = true, Throttle = throttle } : pilot.Sticks(core, us));
                ref readonly FlightState s = ref core.State;
                double up = s.Attitude.Rotate(new Double3(0, 1, 0)).Y;
                double tilt = Math.Acos(Math.Max(-1, Math.Min(1, up))) * RadToDeg;
                r.CornerDrop = Math.Max(r.CornerDrop, hub0 - s.Attitude.Rotate(p.Hub(Corner)).Y);
                if (double.IsNaN(r.TiltAt) && tilt > 45) r.TiltAt = k * FlightCore.Dt;
                if (k == steps / 2) r.TiltHalf = tilt;
                if (k == steps)
                {
                    r.TiltOne = tilt;
                    r.YawOne = Math.Abs(s.BodyRate.Y) * RadToDeg;
                    r.Drop = y0 - s.Position.Y;
                }
                r.YawPeak = Math.Max(r.YawPeak, Math.Abs(s.BodyRate.Y) * RadToDeg);
                band += core.Vibration.Last.Y * core.Vibration.Last.Y / steps;
                r.Command += s.EscCommand[Corner] / steps;
                if (s.EscCommand[Corner] > 0.98) r.Saturated += 1.0 / steps;
                r.Speed += s.RotorSpeed[Corner] / steps;
                Axes3 rigid = FlightCore.Gyro(s.BodyRate);
                r.GyroPeak = Math.Max(r.GyroPeak, Math.Max(core.Trace.GyroPeak, Math.Max(Math.Abs(rigid.Roll), Math.Max(Math.Abs(rigid.Pitch), Math.Abs(rigid.Yaw)))));
                if (core.Trace.GyroPeak > 2000) r.GyroClipped += 1.0 / steps; // the range's own trace: 0 on the clean sensors
                Double3 a = (s.Velocity - v) * (1 / FlightCore.Dt) - FlightCore.Gravity;
                r.AccelPeak = Math.Max(r.AccelPeak, a.Length() / G);
                v = s.Velocity;
                r.ArmLoad = Math.Max(r.ArmLoad, core.Trace.ArmLoad[Corner]);
                r.Twist = Math.Max(r.Twist, Math.Abs(s.ArmTwist[Corner]) * RadToDeg);
                r.Bend = Math.Max(r.Bend, Math.Abs(s.ArmBend[Corner]) * 1e3);
                if (double.IsNaN(r.BrokeAt) && (s.ArmBroken & 1 << Corner) != 0) r.BrokeAt = k * FlightCore.Dt;
                double w = s.RotorSpeed[Corner];
                if (w > 50)
                {
                    // A step's change over one commutation, 2π/(6·p·ω) s of the step's 1 ms, as a share of the speed.
                    double commutation = 2 * Math.PI / (6 * pairs * w);
                    r.CommutationChange = Math.Max(r.CommutationChange, Math.Abs(w - speed) / FlightCore.Dt * commutation / w);
                }
                speed = w;
            }
            r.Band = Math.Sqrt(band);
            if (setup.Modes != null)
            {
                // The arm's root at the flight's largest twist and bend, on the flight's own plate, against the plate's strengths.
                AirframeModesSpec m = setup.Modes, plate = new AirframeModesSpec();
                var hubs = new Double3[4];
                for (int i = 0; i < 4; i++) hubs[i] = setup.Propulsion.Motors[i].Position;
                var arm = new ArmFlex(m, hubs);
                double th = r.Twist / RadToDeg;
                r.Strain = th * arm.TwistStrain;
                r.EdgeStress = th * arm.EdgeStress;
                r.LinearStress = th * arm.TwistStress;
                r.BendStress = r.Bend * 1e-3 * arm.BendStress;
                r.StrainShare = r.Strain / plate.ArmShearFailureStrain;
                r.EdgeShare = r.EdgeStress / plate.ArmInterlaminarStrength;
                r.LinearShare = r.LinearStress / plate.ArmShearStrength;
                r.BendShare = r.BendStress / plate.ArmFlexuralStrength;
            }
            return r;
        }
    }
}
