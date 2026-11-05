using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.6's scenarios (prop-motor-damage's "The shaft and the bearings" and "Breakage"): the repository's drone in
    /// DamageScenario's still-air hover 300 m up with its contacts (so its bells, shafts and bearings), held by SimPilot. A hit on
    /// the hub is put through the shaft's load law (MotorShaft.Strike) before the flight arms; the flown strikes are
    /// StrikeScenarios'. ShaftTests asserts them; tools/flightbench --damage prints them.
    /// </summary>
    public static class ShaftScenarios
    {
        /// <summary>The motor the scenarios load: rear right, clockwise.</summary>
        public const int Corner = 0;

        /// <summary>The hub hit's contact time, s (a pass's order: the arc of a 5 mm graze at hover speed).</summary>
        public const double HitTime = 1e-4;

        /// <summary>The hover's setup on a seed with the drone's contacts (its bells, shafts and bearings); shafts false flies them
        /// rigid (ContactSpec.MotorShaft off, model 63's); draws false without the factory unbalance and pitch draws.</summary>
        public static FlightSetup Setup(DroneSpec spec, ulong seed, bool shafts = true, bool draws = true)
        {
            FlightSetup s = DamageScenario.Setup(spec, seed, draws, draws);
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            s.Contacts = spec.BuildContacts(spec.Nominal, m, h);
            s.Contacts.MotorShaft = shafts;
            return s;
        }

        /// <summary>
        /// A side hit on rotor c's hub through its load law: a force along body +x at the hub whose moment at the upper bearing is
        /// times the shaft's yield moment, over HitTime, the rotor at its angle now; the events it made into events.
        /// </summary>
        public static void HubHit(FlightCore core, int c, double times, StepEvents events)
        {
            MotorShaft shafts = core.Contacts.Shafts;
            double force = times * shafts.YieldMoment(c) / shafts.Lever(c);
            Double3 hub = core.Propulsion.Hub(c);
            var cause = new FlightEvent { Object = -1, Shape = -1, Point = core.State.Position + hub };
            shafts.Strike(c, hub, new Double3(force * HitTime, 0, 0), HitTime, 0, 0, core.State.RotorAngle[c], core.State.RotorSpeed[c], events, cause);
        }

        /// <summary>A bent shaft's hover (prop-motor-damage's "A bent shaft shakes").</summary>
        public struct Bent
        {
            public double Times, Bend, BendP, BendQ, RunOut, Tilt, Band, Rub, Amps, Speed, Moment;
            public int Bends;
            public bool Bound;

            public string Describe() =>
                $"{Times:F1} M_y at the upper bearing ({Moment:F3} N·m): bend {Bend:F6} rad (p {BendP:F6}, q {BendQ:F6}; {Bends} MotorDamaged), run-out at the stator "
                + $"{RunOut * 1e3:F4} mm, disc tilt {Tilt:F4}°; the hover's camera 50–200 Hz band {Band:F4} m/s², rr's rub {Rub:F4} N·m, rr {Speed:F1} rad/s, "
                + $"{Amps:F2} A in all{(Bound ? ", rr bound (MotorBound)" : "")}";
        }

        /// <summary>
        /// The bent-shaft hover on a seed: rr's hub struck through the law at times M_y before arming (0 for none), then 10 s of
        /// DamageScenario's hover, its last 5 s measured; flat, the known-wrong: a straight shaft and rr's motor a flat efficiency
        /// loss (Kt × 0.95) from the start.
        /// </summary>
        public static Bent Hover(DroneSpec spec, ulong seed, double times, bool flat = false)
        {
            FlightSetup setup = Setup(spec, seed);
            var hit = new StepEvents(16);
            var r = new Bent { Times = times };
            FlightCore core = null;
            DamageScenario.Result res = DamageScenario.Hover(setup, out core, before: c =>
            {
                if (times > 0) HubHit(c, Corner, times, hit);
            }, each: c =>
            {
                if (flat) c.Propulsion.SetMotor(Corner, 0.95, 0);
            });
            r.Bound = (core.Propulsion.Bound & 1 << Corner) != 0;
            MotorShaft s = core.Contacts.Shafts;
            for (int i = 0; i < hit.Count; i++)
                if (hit[i].Kind == EventKind.MotorDamaged && hit[i].Index >> 4 == 2)
                {
                    r.Bends++;
                    r.Moment = hit[i].B;
                }
            if (r.Moment == 0) r.Moment = times * s.YieldMoment(Corner);
            r.Bend = s.Bend(Corner);
            r.BendP = s.BendP(Corner);
            r.BendQ = s.BendQ(Corner);
            r.RunOut = core.Contacts.Bells.RunOut(Corner);
            r.Tilt = r.Bend * 57.29577951308232;
            r.Band = res.Band;
            r.Rub = core.Trace.BellRub[Corner];
            r.Amps = res.Amps;
            r.Speed = res.Speed[Corner];
            return r;
        }

        /// <summary>A lost blade's flight (prop-motor-damage's "A lost blade shakes the drone and pulls at its hub").</summary>
        public struct Lost
        {
            public double TiltAt, TiltAfterOne, Pull, ThrustShare, TorqueShare, Band, Command, Speed;
            public bool Wrong, Centred;

            public string Describe() =>
                $"{(Wrong ? "known-wrong (the same thrust and torque loss, no 1/rev)" : "blade 0 of rr lost at its root")}, {(Centred ? "the sticks centred (acro)" : "SimPilot holding the point")}: "
                + $"thrust × {ThrustShare:F4}, torque × {TorqueShare:F4}, the lost blade's pull {Pull:F1} N at the loss; the tilt passes 45° "
                + $"{(double.IsNaN(TiltAt) ? "never in 1 s" : $"{TiltAt * 1e3:F0} ms after")}, {TiltAfterOne:F2}° at 1 s; over that second rr's command {Command:F3}, "
                + $"its speed {Speed:F1} rad/s, the camera's 50–200 Hz band {Band:F4} m/s²";
        }

        /// <summary>
        /// The hovering drone on a seed loses rr's blade 0 at its root after 3 s (the strike's root break's state, through the fixture
        /// path: its tip at the first station's middle, as a break there leaves it), then flies 1 s with the sticks centred: roll,
        /// pitch and yaw at the middle and the throttle where SimPilot last held it (the builder's acro rates, the scenario's
        /// words), or, centred false, SimPilot still holding the point. wrong: instead, the same mean thrust and torque loss (the
        /// rotor's η with that blade gone) as a scripted multiplier, no 1/rev.
        /// </summary>
        public static Lost LoseBlade(DroneSpec spec, ulong seed, bool wrong = false, bool centred = true)
        {
            FlightSetup setup = Setup(spec, seed);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            StickSample last = default;
            for (int k = 0; k < 3000; k++)
            {
                last = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                core.Advance(last);
            }
            Propulsion p = core.Propulsion;
            DamagedRotor rotor = p.Rotor(Corner);
            BladeState blade = rotor.Blade(0);
            double pitch = blade.FactoryPitch;
            blade.Trim(spec.Blade.Stations[0]);
            p.Rebuild(Corner);
            var r = new Lost { Wrong = wrong, Centred = centred, TiltAt = double.NaN };
            r.ThrustShare = rotor.ThrustRatio(0);
            r.TorqueShare = rotor.TorqueRatio(0);
            double w = core.State.RotorSpeed[Corner], band = 0;
            r.Pull = Math.Sqrt(rotor.UnbalanceP * rotor.UnbalanceP + rotor.UnbalanceQ * rotor.UnbalanceQ) * w * w;
            if (wrong)
            {
                blade.Reset(pitch);
                p.Rebuild(Corner);
            }
            const int steps = 1000;
            for (int k = 1; k <= steps; k++)
            {
                if (wrong) p.SetImmersed(Corner, 1 - r.ThrustShare, 1 - r.TorqueShare);
                long us = core.State.Step * FlightCore.StepUs;
                core.Advance(centred ? new StickSample { HostUs = us, Arm = true, Throttle = last.Throttle } : pilot.Sticks(core, us));
                double up = core.State.Attitude.Rotate(new Double3(0, 1, 0)).Y;
                double tilt = Math.Acos(Math.Max(-1, Math.Min(1, up))) * 57.29577951308232;
                if (double.IsNaN(r.TiltAt) && tilt > 45) r.TiltAt = k * FlightCore.Dt;
                r.TiltAfterOne = tilt;
                double y = core.Vibration.Last.Y;
                band += y * y / steps;
                r.Command += core.State.EscCommand[Corner] / steps;
                r.Speed += core.State.RotorSpeed[Corner] / steps;
            }
            r.Band = Math.Sqrt(band);
            return r;
        }

        /// <summary>
        /// The head-on rod of StrikeScenarios.HeadOn at inside m inside the front-right rim (its near side; 12 mm steel): the passes'
        /// records and the rig's events, for a root break (BladeLost) and the hub's loads it puts through the shaft.
        /// </summary>
        public static StrikeScenarios.Rig Rod(DroneSpec spec, double inside, double radius = 0.006, ulong seed = 0x450F, bool shafts = true)
        {
            const double R = 0.127;
            Double3 hub = spec.Motors[1].Position, origin = StrikeScenarios.Sky;
            Double3 rodAt = origin + hub + new Double3(R - inside + radius, 0, 0);
            FlightSetup setup = StrikeScenarios.Setup(spec, origin + new Double3(-0.05, 0, 0), Quat.Identity,
                StrikeScenarios.One(StrikeScenarios.Rod("steel", rodAt, radius, 0.3)), seed);
            setup.Contacts.MotorShaft = shafts;
            var rig = new StrikeScenarios.Rig(setup, StrikeScenarios.HoverDuty(spec));
            rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
            rig.Step(origin, default);
            rig.Hold(origin, 8);
            return rig;
        }
    }
}
