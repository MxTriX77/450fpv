using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pilot's "fly underneath a house and go full throttle" (w20-hunter-track design 11): a pilot's flight (the airframe's
    /// effects, the ground and ceiling effect, the rotors' interference) armed in the air 60 m over the test range under a timber
    /// slab the size and thickness of T02's floor (6 × 6 × 0.06 m, its underside 0.15 m over the drone's top), hovered 1 s by
    /// SimPilot, then at full throttle held for the rest (SimPilot holding still; or, from 3 s, pinned, a roll stick held: the pilot
    /// leaning it).
    /// The physics decides what meets the floor: with drone.json's bottle strapped on top (D-050), the bottle; without it, the
    /// props. No step is scripted past the throttle and the sticks.
    /// </summary>
    public static class FloorScenarios
    {
        /// <summary>T02's floor box: 6 m square here (wider than the drone's reach), 0.06 m thick (T02_north_east's).</summary>
        public const double FloorHalfWidth = 3, FloorThickness = 0.06;

        /// <summary>The gap from the drone's top to the floor's underside at the start, m.</summary>
        public const double StartGap = 0.15;

        /// <summary>The pilot's payload in flights 30–39 on hunter-09 (their logs' headers: 2.000 kg), drone.json's top.</summary>
        public const double PilotPayload = 2.0;

        /// <summary>What happened under the floor: when the drone was first pressed to it (s from the start, NaN never) and how
        /// hard on average after (N, the contacts' downward force); the props' least clearance to it along their axes after the
        /// press (m: the rotors' ceiling distance); the blades' passes (and those before the first motor failed), damage events and
        /// jams; each motor's first time hot (its
        /// magnets past their maximum), shorted and failed (s, NaN never) and its windings' peak (°C); the pack current's mean while
        /// pressed (A); the shortest blade at the end (r/R); tunnels and crashes.</summary>
        public struct Pinned
        {
            public double PressedAt, Push, Clearance, Amps, ShortestTip;
            public int Passes, PassesBeforeFailure, Damaged, Jams, Tunnels, Crashes;
            public double[] HotAt, ShortedAt, FailedAt, PeakWinding;

            public double FirstFailure()
            {
                double t = double.PositiveInfinity;
                foreach (double x in FailedAt)
                    if (!double.IsNaN(x) && x < t) t = x;
                return t;
            }

            public double FirstShort()
            {
                double t = double.PositiveInfinity;
                foreach (double x in ShortedAt)
                    if (!double.IsNaN(x) && x < t) t = x;
                return t;
            }

            public string Describe()
            {
                string motors = "";
                for (int c = 0; c < 4; c++)
                    motors += $" {Corner(c)} {PeakWinding[c]:F0} °C (hot {At(HotAt[c])}, shorted {At(ShortedAt[c])}, failed {At(FailedAt[c])});";
                return $"pressed at {At(PressedAt)} with {Push:F0} N, the props' least clearance {Clearance * 1000:F1} mm, {Amps:F0} A; "
                    + $"{Passes} blade passes ({PassesBeforeFailure} before a motor failed), {Damaged} damage events, {Jams} jams, the shortest blade {ShortestTip:F3} R;{motors} "
                    + $"{Tunnels} tunnels, {Crashes} crashes";
            }

            static string At(double t) => double.IsNaN(t) ? "never" : t.ToString("F3") + " s";
            static string Corner(int c) => c == 0 ? "rr" : c == 1 ? "fr" : c == 2 ? "rl" : "fl";
        }

        /// <summary>The flight under the floor: payload kg (drone.json's 2 kg bottle, or 0 for none), seconds in all (full throttle
        /// from 1 s), a roll stick held from 3 s (0: SimPilot holds still), the floor's material.</summary>
        public static Pinned UnderTheFloor(DroneSpec spec, double payload, double seconds, double roll = 0, string material = "timber",
            ulong seed = 0x450F)
        {
            Double3 start = StrikeScenarios.Sky;
            Loadout load = spec.Nominal;
            load.Payload = payload;
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            SurfaceTable table = SoilScenarios.Table();
            ContactSpec contacts = spec.BuildContacts(load, materials, hash, table);
            double top = double.NegativeInfinity; // the drone's top over its origin: the highest point of its shapes
            foreach (ContactShape s in contacts.Shapes)
            {
                if (s.Role == ContactRole.Support) continue;
                top = Math.Max(top, Math.Max(s.A.Y, s.B.Y) + (s.Role == ContactRole.Disc ? s.HalfThickness : s.Radius));
            }
            Double3 under = start + new Double3(0, top + StartGap, 0);
            var floor = StrikeScenarios.Box(material, under + new Double3(0, FloorThickness / 2, 0), new Double3(FloorHalfWidth, FloorThickness / 2, FloorHalfWidth));
            FlightSetup setup = StrikeScenarios.Setup(spec, start, Quat.Identity, StrikeScenarios.One(floor), seed, true, null, table);
            setup.Mass = spec.BuildMass(load);
            setup.Contacts = contacts;
            setup.Contacts.BladeStrikes = true;
            StrikeScenarios.Airframe(spec)(setup);
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.Interference = spec.BuildInterference();
            var core = new FlightCore(setup);
            for (int k = 0; k < 16; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }); // armed in the air
            var pilot = new SimPilot(core.Controller, start);
            var r = new Pinned
            {
                PressedAt = double.NaN, Clearance = double.PositiveInfinity, HotAt = Nan(), ShortedAt = Nan(), FailedAt = Nan(),
                PeakWinding = new double[4],
            };
            long full = 1000, lean = 3000, end = (long)(seconds * 1000), first = core.State.Step;
            double push = 0, amps = 0;
            int pressed = 0;
            for (long k = 0; k < end; k++)
            {
                if (k == full) pilot.HeldThrottle = 1.0;
                StickSample stick = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                if (k >= lean && roll != 0) stick.Roll = roll;
                core.Advance(stick);
                double t = (core.State.Step - first) * FlightCore.Dt;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.BladeStrike) r.Passes++;
                    else if (e.Kind == EventKind.BladeDamaged) r.Damaged++;
                    else if (e.Kind == EventKind.RotorJammed) r.Jams++;
                    else if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    else if (e.Kind == EventKind.Crash) r.Crashes++;
                }
                ref readonly StepTrace tr = ref core.Trace;
                for (int c = 0; c < 4; c++)
                {
                    double state = tr.MotorState[c];
                    if (state >= MotorHealth.Hot && double.IsNaN(r.HotAt[c])) r.HotAt[c] = t;
                    if (state >= MotorHealth.Shorted && double.IsNaN(r.ShortedAt[c])) r.ShortedAt[c] = t;
                    if (state >= MotorHealth.Failed && double.IsNaN(r.FailedAt[c])) r.FailedAt[c] = t;
                    r.PeakWinding[c] = Math.Max(r.PeakWinding[c], tr.WindingTemperature[c]);
                }
                if (r.FirstFailure() == double.PositiveInfinity) r.PassesBeforeFailure = r.Passes;
                if (k < full) continue;
                if (tr.ContactsTouching > 0 && tr.ContactForce.Y < 0)
                {
                    if (double.IsNaN(r.PressedAt)) r.PressedAt = t;
                    push -= tr.ContactForce.Y;
                    amps += tr.PackAmps0 + tr.PackAmps1;
                    pressed++;
                }
                if (!double.IsNaN(r.PressedAt))
                    for (int c = 0; c < 4; c++) r.Clearance = Math.Min(r.Clearance, tr.CeilingDistance[c]);
            }
            r.Push = pressed > 0 ? push / pressed : 0;
            r.Amps = pressed > 0 ? amps / pressed : 0;
            r.ShortestTip = 1;
            for (int c = 0; c < 4; c++)
            {
                DamagedRotor rotor = core.Propulsion.Rotor(c);
                for (int b = 0; b < rotor.Blades; b++) r.ShortestTip = Math.Min(r.ShortestTip, rotor.Blade(b).TipRadius);
            }
            return r;
        }

        static double[] Nan() => new[] { double.NaN, double.NaN, double.NaN, double.NaN };
    }
}
