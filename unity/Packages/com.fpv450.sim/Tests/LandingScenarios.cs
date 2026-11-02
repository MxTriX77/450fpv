using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// D-067's scripted touchdowns (p6-flight-core 1.6c): the drone set down at an attitude error with a lateral speed and
    /// a descent, on the stub's floor or at a range site, so the sim's toppling and the coil's rock can be held against
    /// LandingStability's rigid-body answer. Each touchdown starts in the air a little above the ground and runs until the
    /// drone rests or is over. The tests assert them (LandingTests); tools/flightbench --landing prints the grid.
    /// </summary>
    public static class LandingScenarios
    {
        /// <summary>How the drone comes down: a free body (no propulsion: the rigid-body answer's own case), or powered and
        /// armed with airmode active and the sticks centred at the hover throttle, which at the first touch is cut to zero
        /// (still armed), held, or disarmed.</summary>
        public enum Power { Free, Cut, Held, Disarmed }

        public struct Touch
        {
            public bool Legs;

            /// <summary>The roll toward the motion (+x, right side down) and the nose-down pitch, deg.</summary>
            public double RollDeg, PitchDeg;

            /// <summary>The lateral speed along the roll (body +x) or, with Forward, along the nose (body +z), and the
            /// descent, m/s.</summary>
            public double Lateral, Descent;
            public bool Forward;

            public Power Power;
            public double Seconds;

            /// <summary>A tilt (deg) whose time from the first touch Outcome.Reached reports; 0 for none.</summary>
            public double ReachDeg;

            /// <summary>Null for the stub's floor; else the site (its world, foot and heading).</summary>
            public ContactScenarios.Site? Site;

            /// <summary>A change to the contacts (a known-wrong model), or null.</summary>
            public Func<ContactSpec, ContactSpec> Edit;
        }

        public struct Outcome
        {
            /// <summary>The largest tilt of body up from the vertical, deg; over when it passed 60° (the motor cans meet the
            /// ground at 75° on the legs).</summary>
            public double MaxTilt, EndTilt;
            public bool Over;
            public int Tunnels;

            /// <summary>The time from the first touch to Touch.ReachDeg of tilt, s (+∞ if never).</summary>
            public double Reached;
        }

        static double _hover = -1;

        /// <summary>The throttle that holds the repository drone over the floor (ContactScenarios.Land's, the sticks held).</summary>
        public static double HoverThrottle(DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            if (_hover < 0) _hover = ContactScenarios.Land(spec, materials, hash, false).HeldThrottle;
            return _hover;
        }

        static Quat Tilt(double rollDeg, double pitchDeg)
        {
            // Roll: the right side (+x) down, a turn about +z by −roll; pitch: the nose (+z) down, a turn about +x by +pitch.
            double r = -rollDeg * Math.PI / 360, p = pitchDeg * Math.PI / 360;
            return (new Quat(0, 0, Math.Sin(r), Math.Cos(r)) * new Quat(Math.Sin(p), 0, 0, Math.Cos(p))).Normalized();
        }

        /// <summary>One touchdown.</summary>
        public static Outcome Run(DroneSpec spec, MaterialRow[] materials, ulong hash, in Touch t)
        {
            Loadout loadout = spec.Nominal;
            loadout.Legs = t.Legs;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
            if (t.Edit != null) contacts = t.Edit(contacts);
            ContactScenarios.Site site = t.Site ?? new ContactScenarios.Site(null, default, 0);
            Quat heading = site.Attitude(), q = (heading * Tilt(t.RollDeg, t.PitchDeg)).Normalized();
            double low = double.PositiveInfinity;
            foreach (ContactShape s in contacts.Shapes)
            {
                if (s.Role == ContactRole.Disc) continue;
                low = Math.Min(low, q.Rotate(s.A).Y - s.Radius);
                low = Math.Min(low, q.Rotate(s.B).Y - s.Radius);
            }
            bool powered = t.Power != Power.Free;
            // Powered, the arming and airmode's start take 16 steps before the touch.
            double clear = powered ? 0.002 + 0.03 * t.Descent : 0.001;
            Double3 v = heading.Rotate(t.Forward ? new Double3(0, 0, t.Lateral) : new Double3(t.Lateral, 0, 0)) + new Double3(0, -t.Descent, 0);
            var setup = new FlightSetup
            {
                Seed = 0x450F, World = site.Open(), Mass = spec.BuildMass(loadout), Contacts = contacts,
                StartPosition = site.Foot + new Double3(0, clear - low, 0), StartAttitude = q, StartVelocity = v,
            };
            double hold = 0;
            if (powered)
            {
                setup.Propulsion = spec.BuildPropulsion();
                setup.Aero = spec.BuildAero();
                setup.StartPackCharge = 0.9;
                hold = HoverThrottle(spec, materials, hash);
            }
            var core = new FlightCore(setup);
            var o = new Outcome { Reached = double.PositiveInfinity };
            bool touched = false;
            long touch = 0;
            int steps = (int)((t.Seconds > 0 ? t.Seconds : 3) * 1000);
            for (int k = 0; k < steps; k++)
            {
                var stick = new StickSample { HostUs = core.State.Step * FlightCore.StepUs };
                if (powered)
                {
                    stick.Arm = k >= 8 && !(touched && t.Power == Power.Disarmed);
                    stick.Throttle = k < 16 || touched && t.Power != Power.Held ? 0 : hold;
                }
                core.Advance(stick);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.ContactMade && !touched)
                    {
                        touched = true;
                        touch = core.State.Step;
                    }
                    if (e.Kind == EventKind.Tunnel) o.Tunnels++;
                }
                Double3 up = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                double tilt = Math.Acos(Math.Max(-1, Math.Min(1, up.Y))) * 180 / Math.PI;
                o.MaxTilt = Math.Max(o.MaxTilt, tilt);
                o.EndTilt = tilt;
                if (touched && t.ReachDeg > 0 && tilt >= t.ReachDeg && double.IsPositiveInfinity(o.Reached)) o.Reached = (core.State.Step - touch) * FlightCore.Dt;
            }
            o.Over = o.MaxTilt > 60;
            return o;
        }

        /// <summary>The lowest lateral speed (m/s) at which the touchdown tips over, by bisection over [0, 4] to 1 mm/s;
        /// 0 if it tips with none, +∞ if not even at 4 m/s.</summary>
        public static double CriticalSpeed(DroneSpec spec, MaterialRow[] materials, ulong hash, Touch t)
        {
            Touch at = t;
            at.Lateral = 0;
            if (Run(spec, materials, hash, at).Over) return 0;
            at.Lateral = 4;
            if (!Run(spec, materials, hash, at).Over) return double.PositiveInfinity;
            double lo = 0, hi = 4;
            while (hi - lo > 0.001)
            {
                at.Lateral = (lo + hi) / 2;
                if (Run(spec, materials, hash, at).Over) hi = at.Lateral;
                else lo = at.Lateral;
            }
            return (lo + hi) / 2;
        }

        /// <summary>The time (s) a free body set down at rest at fromDeg of roll (its lowest point on the floor) takes to
        /// roll to toDeg; +∞ if it does not within 10 s.</summary>
        public static double RollTime(DroneSpec spec, MaterialRow[] materials, ulong hash, bool legs, double fromDeg, double toDeg,
            Func<ContactSpec, ContactSpec> edit = null)
        {
            Loadout loadout = spec.Nominal;
            loadout.Legs = legs;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
            if (edit != null) contacts = edit(contacts);
            Quat q = Tilt(fromDeg, 0);
            double low = double.PositiveInfinity;
            foreach (ContactShape s in contacts.Shapes)
            {
                if (s.Role == ContactRole.Disc) continue;
                low = Math.Min(low, q.Rotate(s.A).Y - s.Radius);
                low = Math.Min(low, q.Rotate(s.B).Y - s.Radius);
            }
            // Set 10 µm into the floor, so the first step already carries it.
            var setup = new FlightSetup
            {
                Seed = 0x450F, World = FlightWorld.Floor(), Mass = spec.BuildMass(loadout), Contacts = contacts,
                StartPosition = new Double3(0, -low - 1e-5, 0), StartAttitude = q,
            };
            var core = new FlightCore(setup);
            for (int k = 0; k < 10000; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                Double3 up = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                if (Math.Asin(Math.Min(1, up.X)) * 180 / Math.PI >= toDeg || up.Y < 0.3) return (k + 1) * FlightCore.Dt;
            }
            return double.PositiveInfinity;
        }

        /// <summary>The ground's friction pair set to μ (static and kinetic): μ 10 is a skid that catches, μ 0.001 a
        /// frictionless one (the known-wrong models).</summary>
        public static Func<ContactSpec, ContactSpec> GroundFriction(double mu) => c =>
        {
            c = c.Clone();
            c.GroundStaticFriction = mu;
            c.GroundKineticFriction = mu;
            return c;
        };

        /// <summary>Every body shape's damping ratio set to zeta (the overdamped known-wrong at 2).</summary>
        public static Func<ContactSpec, ContactSpec> BodyDamping(double zeta) => c =>
        {
            c = c.Clone();
            var shapes = (ContactShape[])c.Shapes.Clone();
            for (int i = 0; i < shapes.Length; i++)
                if (shapes[i].Role == ContactRole.Body) shapes[i].DampingRatio = zeta;
            c.Shapes = shapes;
            return c;
        };
    }
}
