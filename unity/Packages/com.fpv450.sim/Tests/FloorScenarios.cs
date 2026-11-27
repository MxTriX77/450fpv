using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pilot's "fly underneath a house and go full throttle" (w20-hunter-track design 11): a pilot's flight (the airframe's
    /// effects, the ground and ceiling effect, the rotors' interference) armed in the air 60 m over the test range under a timber
    /// slab the size and thickness of T02's floor (6 × 6 × 0.06 m, its underside 0.15 m over the drone's top), hovered 1 s by
    /// SimPilot, then at full throttle held for the rest (SimPilot holding still; or, from 3 s, pinned, a roll stick held: the pilot
    /// leaning it).
    /// The physics decides what meets the floor: with drone.json's bottle strapped on top (D-050), the bottle; without it, the
    /// props. No step is scripted past the throttle and the sticks. The floor may be tilted (its underside sloping down toward the
    /// drone's nose about the body's x axis), so a drone held level meets it at that angle (design 11's addendum: 0–15°).
    /// Pressed: the same angles on a clamped rig (StrikeScenarios.Rig), the lead disc held a set depth into the slab.
    /// </summary>
    public static class FloorScenarios
    {
        /// <summary>T02's floor box: 6 m square here (wider than the drone's reach), 0.06 m thick (T02_north_east's).</summary>
        public const double FloorHalfWidth = 3, FloorThickness = 0.06;

        /// <summary>The gap from the drone's top to the floor's underside at the start, m.</summary>
        public const double StartGap = 0.15;

        /// <summary>Design 11's addendum's floor angles, degrees (the props flat on it to leaned), and how long each is flown, s.</summary>
        public static readonly double[] Tilts = { 0, 1, 2, 5, 15 };

        public const double TiltSeconds = 6;

        /// <summary>The pilot's payload in flights 30–39 on hunter-09 (their logs' headers: 2.000 kg), drone.json's top.</summary>
        public const double PilotPayload = 2.0;

        /// <summary>What happened under the floor: when the drone was first pressed to it (s from the start, NaN never) and how
        /// hard on average after (N, the contacts' downward force); the props' least clearance to it along their axes after the
        /// press (m: the rotors' ceiling distance); the blades' passes (and those before the first motor failed), damage events and
        /// jams, and the first of each (s, NaN never); each motor's first time hot (its
        /// magnets past their maximum), shorted and failed (s, NaN never) and its windings' peak (°C); the pack current's mean while
        /// pressed (A); the shortest blade at the end (r/R); the rotors' least speed over the last second (rad/s, the slowest of
        /// the four's); tunnels and crashes.</summary>
        public struct Pinned
        {
            public double PressedAt, Push, Clearance, Amps, ShortestTip, FirstPassAt, FirstJamAt, FirstDamageAt, SlowestLate;
            public int Passes, PassesBeforeFailure, Damaged, Jams, Tunnels, Crashes;
            public double[] HotAt, ShortedAt, FailedAt, PeakWinding;

            /// <summary>The blades' damage events by mode (StrikeMode: crush, break, bend, wear).</summary>
            public int[] DamageModes;

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
                    + $"{Passes} blade passes ({PassesBeforeFailure} before a motor failed; the first at {At(FirstPassAt)}), {Damaged} damage events "
                    + $"(the first at {At(FirstDamageAt)}: {DamageModes[1]} crush, {DamageModes[2]} break, {DamageModes[3]} bend, {DamageModes[4]} wear), {Jams} jams (the first at {At(FirstJamAt)}), the shortest blade {ShortestTip:F3} R, "
                    + $"the slowest rotor's least speed in the last second {SlowestLate:F0} rad/s;{motors} {Tunnels} tunnels, {Crashes} crashes";
            }

            static string At(double t) => double.IsNaN(t) ? "never" : t.ToString("F3") + " s";
            static string Corner(int c) => c == 0 ? "rr" : c == 1 ? "fr" : c == 2 ? "rl" : "fl";
        }

        /// <summary>The flight under the floor: payload kg (drone.json's 2 kg bottle, or 0 for none), seconds in all (full throttle
        /// from 1 s), a roll stick held from 3 s (0: SimPilot holds still), the floor's material, its tilt (degrees, about the x
        /// axis through the point over the drone's origin), and the flat faces' law (ContactSpec.FlatFaces; false: model 108's).</summary>
        public static Pinned UnderTheFloor(DroneSpec spec, double payload, double seconds, double roll = 0, string material = "timber",
            ulong seed = 0x450F, double tilt = 0, bool faces = true)
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
            if (tilt != 0) floor = Tilted(floor, under, tilt * StrikeScenarios.DegToRad);
            FlightSetup setup = StrikeScenarios.Setup(spec, start, Quat.Identity, StrikeScenarios.One(floor), seed, true, null, table);
            setup.Mass = spec.BuildMass(load);
            setup.Contacts = contacts;
            setup.Contacts.BladeStrikes = true;
            setup.Contacts.FlatFaces = faces;
            StrikeScenarios.Airframe(spec)(setup);
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.Interference = spec.BuildInterference();
            var core = new FlightCore(setup);
            for (int k = 0; k < 16; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }); // armed in the air
            var pilot = new SimPilot(core.Controller, start);
            var r = new Pinned
            {
                PressedAt = double.NaN, Clearance = double.PositiveInfinity, HotAt = Nan(), ShortedAt = Nan(), FailedAt = Nan(),
                PeakWinding = new double[4], DamageModes = new int[6], FirstPassAt = double.NaN, FirstJamAt = double.NaN, FirstDamageAt = double.NaN,
                SlowestLate = double.PositiveInfinity,
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
                    if (e.Kind == EventKind.BladeStrike)
                    {
                        r.Passes++;
                        if (double.IsNaN(r.FirstPassAt)) r.FirstPassAt = t;
                    }
                    else if (e.Kind == EventKind.BladeDamaged)
                    {
                        r.Damaged++;
                        r.DamageModes[(e.Index >> 4) % 6]++;
                        if (double.IsNaN(r.FirstDamageAt)) r.FirstDamageAt = t;
                    }
                    else if (e.Kind == EventKind.RotorJammed)
                    {
                        r.Jams++;
                        if (double.IsNaN(r.FirstJamAt)) r.FirstJamAt = t;
                    }
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
                if (k >= end - 1000)
                    for (int c = 0; c < 4; c++) r.SlowestLate = Math.Min(r.SlowestLate, core.State.RotorSpeed[c]);
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

        // A box turned by angle (rad) about the world x axis through pivot: its axes and its centre.
        static ShapeGeometry Tilted(ShapeGeometry box, Double3 pivot, double angle)
        {
            DetMath.SinCos(angle, out double sn, out double cs);
            Double3 Turn(Double3 v) => new Double3(v.X, cs * v.Y - sn * v.Z, sn * v.Y + cs * v.Z);
            box.AxisX = Turn(box.AxisX);
            box.AxisY = Turn(box.AxisY);
            box.AxisZ = Turn(box.AxisZ);
            box.Center = pivot + Turn(box.Center - pivot);
            return box;
        }

        /// <summary>The clamped rig's outcome: the first pass's record (Mode Glance and Radius NaN for none), the passes, jams and
        /// damage events in all, the first jam's and the first damage's time (s, NaN never), the lead rotor's mean speed over the
        /// last 0.5 s (rad/s) and its shortest blade at the end (r/R).</summary>
        public struct Press
        {
            public PropStrikes.PassRecord First;
            public int Passes, Jams, Damaged;
            public double FirstJamAt, FirstDamageAt, MeanSpeed, ShortestTip;

            public string Describe() => $"{Passes} passes, {Jams} jams (the first at {At(FirstJamAt)}), {Damaged} damage events (the first at "
                + $"{At(FirstDamageAt)}), the rotor at {MeanSpeed:F0} rad/s over the last 0.5 s, the shortest blade {ShortestTip:F3} R; the first pass: "
                + (double.IsNaN(First.Radius) ? "none" : StrikeScenarios.Line(First));

            static string At(double t) => double.IsNaN(t) ? "never" : t.ToString("F3") + " s";
        }

        /// <summary>
        /// The lead disc (fl) on a clamped rig at full duty under a face at an angle (degrees) to its plane: a slab of a material over
        /// the drone held level, its underside sloping down toward fl, placed so the slab's deepest point (its rim's top edge on the
        /// face's low side, BladeCut's) is depth (m) past it; held there for seconds, the rotor spun up free first (0.3 s, the face
        /// 20 mm off). faces: the flat faces' law (false: model 108's).
        /// </summary>
        public static Press Pressed(DroneSpec spec, double angle, double depth, double seconds, string material = "timber", bool faces = true)
        {
            const int corner = 3;
            double a = angle * StrikeScenarios.DegToRad;
            DetMath.SinCos(a, out double sn, out double cs);
            Quat level = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            Double3 hub = level.Rotate(spec.Motors[corner].Position), outward = new Double3(hub.X, 0, hub.Z);
            outward = outward * (1 / outward.Length());
            // The face's outward normal points down and away from the disc's low side; the slab's top edge reaches farthest along −n
            // at the rim on that side, h above the hub's plane.
            Double3 n = new Double3(0, -cs, 0) - outward * sn;
            double h = 0, radius = spec.Props.Diameter / 2;
            ContactSpec probe = spec.BuildContacts(spec.Nominal, ContactScenarios.RepositoryMaterials(out ulong hash), hash, SoilScenarios.Table());
            foreach (ContactShape s in probe.Shapes)
                if (s.Role == ContactRole.Disc && s.Corner == corner) h = s.HalfThickness;
            Double3 origin = StrikeScenarios.Sky, deepest = origin + hub + outward * radius + new Double3(0, h, 0);
            Double3 onFace = deepest + n * depth; // the face's plane passes depth past the deepest point, along its outward normal
            Double3 ex = outward, ey = n * -1, ez = Double3.Cross(ex, ey);
            ex = Double3.Cross(ey, ez);
            var slab = new ShapeGeometry
            {
                Kind = ShapeKind.Box, AxisX = ex * (1 / ex.Length()), AxisY = ey, AxisZ = ez * (1 / ez.Length()),
                HalfExtents = new Double3(FloorHalfWidth, FloorThickness / 2, FloorHalfWidth), Center = onFace + ey * (FloorThickness / 2),
                Material = RangeScenarios.World.MaterialId(material),
            };
            FlightSetup setup = StrikeScenarios.Setup(spec, origin, level, StrikeScenarios.One(slab), 0x450F, true, null, SoilScenarios.Table());
            setup.Contacts.FlatFaces = faces;
            var rig = new StrikeScenarios.Rig(setup, 1.0);
            rig.Core.Strikes.Record = new System.Collections.Generic.List<PropStrikes.PassRecord>();
            Double3 away = origin + n * 0.02;
            rig.Hold(away, 300);
            int log = rig.Log.Count, records = rig.Core.Strikes.Record.Count, part = spec.PartIndex("prop_fl");
            var r = new Press { FirstJamAt = double.NaN, FirstDamageAt = double.NaN };
            r.First.Radius = double.NaN;
            int steps = (int)(seconds * 1000);
            double sum = 0;
            for (int k = 0; k < steps; k++)
            {
                rig.Step(origin, default);
                double t = (k + 1) * 0.001;
                for (int j = log; j < rig.Log.Count; j++)
                {
                    FlightEvent e = rig.Log[j];
                    if (e.Part != part) continue;
                    if (e.Kind == EventKind.BladeStrike) r.Passes++;
                    else if (e.Kind == EventKind.RotorJammed)
                    {
                        r.Jams++;
                        if (double.IsNaN(r.FirstJamAt)) r.FirstJamAt = t;
                    }
                    else if (e.Kind == EventKind.BladeDamaged)
                    {
                        r.Damaged++;
                        if (double.IsNaN(r.FirstDamageAt)) r.FirstDamageAt = t;
                    }
                }
                log = rig.Log.Count;
                for (; double.IsNaN(r.First.Radius) && records < rig.Core.Strikes.Record.Count; records++)
                    if (rig.Core.Strikes.Record[records].Corner == corner && !rig.Core.Strikes.Record[records].Grind) r.First = rig.Core.Strikes.Record[records];
                if (k >= steps - 500) sum += rig.Now.RotorSpeed[corner];
            }
            r.MeanSpeed = sum / 500;
            r.ShortestTip = 1;
            DamagedRotor rotor = rig.Core.Propulsion.Rotor(corner);
            for (int b = 0; b < rotor.Blades; b++) r.ShortestTip = Math.Min(r.ShortestTip, rotor.Blade(b).TipRadius);
            return r;
        }
    }
}
