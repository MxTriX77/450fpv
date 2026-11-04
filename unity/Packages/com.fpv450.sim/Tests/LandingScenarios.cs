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
    /// LandingStability's rigid-body answer. A free body starts a millimetre over the ground; a powered one flies a steady
    /// approach to it (N4). Each touch is judged on its own, from the first contact to the first one after the drone has
    /// been off the ground longer than a hop; that later landing is reported apart. The tests assert them (LandingTests);
    /// tools/flightbench --landing prints the grid.
    /// </summary>
    public static class LandingScenarios
    {
        /// <summary>How the drone comes down: a free body (no propulsion: the rigid-body answer's own case), or powered and
        /// armed with airmode active and the sticks centred at the hover throttle, which at the first touch is cut to zero
        /// (still armed), held, or disarmed.</summary>
        public enum Power { Free, Cut, Held, Disarmed }

        /// <summary>A powered touch comes this long after the throttle's step, s (assumed; N4 asks at least 0.4 s: the rotors
        /// reach 90 % of their thrust 0.14 s after the step and 99 % after 0.25 s, and anti-gravity's 5 Hz PT2 settles). The
        /// spin-up sinks the drone about 0.75 m/s, so its start carries that up and it rises a centimetre or two before it
        /// settles; when the start would have to lie under the ground the touch comes later.</summary>
        public const double ApproachSeconds = 0.5;

        /// <summary>The gentlest descent a powered touch is flown at, m/s (assumed): a held throttle reaches the ground with
        /// no descent only as an asymptote, so "no descent" is a gentle settle onto the skids. Slower than this the ground's
        /// cushion (D-064's wash and gain) brakes the last centimetres by about as much as the descent, so the touch would be
        /// the cushion's and not the asked one (at 0.1 m/s the approach does not settle; N4's probe touched at 0.22–0.34).</summary>
        public const double PoweredMinDescent = 0.2;

        /// <summary>A touch ends once nothing has touched the ground for this long, s (derived: the flight of a 5 cm hop,
        /// 2·√(2·0.05 m / g)); its next contact is another landing. A pinned skid's hops last 21–38 ms; a held throttle's
        /// drift flies on for 0.67 s (N4).</summary>
        public const double HopSeconds = 0.2;

        const double G = 9.80665;
        const int ArmStep = 8, ThrottleStep = 16, ApproachRounds = 8;
        const double LowestStart = 0.005, ApproachWindow = 3;

        // The approach's steady part starts this long after the throttle's step, s (the rotors at 99 % of their thrust
        // after 0.25 s, N4).
        const double SpinUpSeconds = 0.3;

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

            /// <summary>How long the run goes on after the first touch, s (0 for 3 s).</summary>
            public double Seconds;

            /// <summary>A tilt (deg) whose time from the first touch Outcome.Reached reports; 0 for none.</summary>
            public double ReachDeg;

            /// <summary>Null for the stub's floor; else the site (its world, foot and heading).</summary>
            public ContactScenarios.Site? Site;

            /// <summary>A change to the contacts (a known-wrong model), or null.</summary>
            public Func<ContactSpec, ContactSpec> Edit;

            /// <summary>Every ESC's disarm rule in place of drone.json's (a compared variant), or null.</summary>
            public EscDisarm? Esc;

            /// <summary>The lateral speed's way: negative against body +x (or +z), else along it (p7g-legs 1.3: each side in turn).</summary>
            public double Sign;

            /// <summary>A change to the flight before its first step (a damaged leg, p7g-legs 1.3), or null.</summary>
            public Action<FlightCore> Prepare;
        }

        public struct Outcome
        {
            /// <summary>The largest tilt of body up from the vertical during the touch, deg, and the last step's; over when
            /// it passed 60° before the touch ended (the motor cans meet the ground at 75° on the legs).</summary>
            public double MaxTilt, EndTilt;
            public bool Over;
            public int Tunnels;

            /// <summary>The time from the first touch to Touch.ReachDeg of tilt, s (+∞ if never).</summary>
            public double Reached;

            /// <summary>The first touch's own state: whether it came, its time after the throttle's step (s; powered), its
            /// lateral speed along the touch's direction and its descent (m/s), the rotors' thrust (N), anti-gravity's filter
            /// output and the tilt (deg) on that step.</summary>
            public bool Touched;
            public double TouchAt, TouchLateral, TouchDescent, TouchThrust, TouchAntiGravity, TouchTilt;

            /// <summary>When the touch ended: the time from the first touch to the last contact before HopSeconds off the
            /// ground, s (+∞ if it stayed down); the touch's verdict (Over) runs to the next contact. That later landing's
            /// horizontal speed at its first contact (m/s, NaN if none) and whether it went over.</summary>
            public double TouchEnded, LaterSpeed;
            public bool LaterOver;

            /// <summary>The throttle the approach flew, 0–1 of the stick (powered).</summary>
            public double Throttle;

            /// <summary>An approach (Run's iteration) at SpinUpSeconds after the throttle's step: the lowest point's height over
            /// the foot (m; NaN if it touched sooner), the vertical and lateral speeds (m/s), and the accelerations from then
            /// to the touch or the window's end (m/s²).</summary>
            internal double Height, Sink, Drift, Accel, AccelLateral;

            /// <summary>The attitude at an approach's stop (Run's iteration).</summary>
            internal Quat Attitude;
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

        /// <summary>
        /// One touchdown. A free body starts 1 mm over the ground at the touch's speeds. A powered one flies a steady approach
        /// (N4): armed at step 8, the approach's throttle from step 16, the attitude held by acro's rate loop, and the touch
        /// ApproachSeconds later at the asked lateral speed and a steady descent (at least PoweredMinDescent), its rotors
        /// at the approach's thrust and anti-gravity settled. The approach's throttle holds the sink steady at the tilt (the
        /// tilted thrust balancing the weight, so it drifts at g·tan φ); the start's height, sink, drift (backed off by that
        /// drift over the approach: a braking flare) and attitude and the throttle are found by flying the approach
        /// ApproachRounds times. At the first touch the throttle is cut, held at the approach's or the drone disarmed; the run lasts
        /// Touch.Seconds (3 s) after it.
        /// </summary>
        public static Outcome Run(DroneSpec spec, MaterialRow[] materials, ulong hash, in Touch t)
        {
            Loadout loadout = spec.Nominal;
            loadout.Legs = t.Legs;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
            if (t.Edit != null) contacts = t.Edit(contacts);
            ContactScenarios.Site site = t.Site ?? new ContactScenarios.Site(null, default, 0);
            Quat heading = site.Attitude(), q = (heading * Tilt(t.RollDeg, t.PitchDeg)).Normalized();
            double way = t.Sign < 0 ? -1 : 1, signed = t.Sign < 0 ? -t.Lateral : t.Lateral; // p7g-legs 1.3: each side in turn
            Double3 along = heading.Rotate(t.Forward ? new Double3(0, 0, way) : new Double3(way, 0, 0));
            var f = new Flight(spec, t, loadout, contacts, site, along);
            if (t.Power == Power.Free)
                return f.Fly(q, 0.001, heading.Rotate(t.Forward ? new Double3(0, 0, signed) : new Double3(signed, 0, 0)) + new Double3(0, -t.Descent, 0), false, 0);

            // The approach: each round flies it and moves the start by what it missed at its steady part's start (SpinUpSeconds
            // after the step): the throttle by the sink's acceleration from there (thrust ∝ the throttle² near the hover), the
            // sink by its error there, the height so that the steady sink touches at ApproachSeconds, and the drift so that the
            // touch comes at the asked lateral speed when it does; and the attitude by the turn the approach made (acro holds
            // the rate, not the attitude, and the spin-up and the flare's drag turn it a little), so that the touch comes at
            // the asked one.
            double descent = Math.Max(t.Descent, PoweredMinDescent), T = ApproachSeconds, ts = SpinUpSeconds;
            double lean = (t.Forward ? t.PitchDeg : t.RollDeg) * Math.PI / 180;
            double lateral = t.Lateral - way * G * Math.Tan(lean) * T, sink = -descent, clear = descent * T + 0.3;
            double throttle = HoverThrottle(spec, materials, hash) / Math.Sqrt(Math.Cos(lean));
            Quat start = q;
            for (int r = 0; r < ApproachRounds; r++)
            {
                Outcome a = f.Fly(start, clear, along * lateral + new Double3(0, sink, 0), true, throttle);
                start = (q * a.Attitude.Conjugate() * start).Normalized();
                if (double.IsNaN(a.Height))
                {
                    clear += 0.2;
                    continue;
                }
                double ds = -descent - a.Sink;
                double next = Math.Max(LowestStart, clear + descent * (T - ts) - a.Height - ds * ts);
                double at = ts + (a.Height + next - clear + ds * ts) / descent;
                lateral += t.Lateral - (a.Touched ? a.TouchLateral : a.Drift + a.AccelLateral * (at - ts));
                clear = next;
                sink += ds;
                throttle -= a.Accel * throttle / (2 * G);
            }
            return f.Fly(start, clear, along * lateral + new Double3(0, sink, 0), false, throttle);
        }

        // One touchdown's flight from its start, the lowest point clear over the ground at velocity v: to the first touch
        // only (approach: the touch's state, or the state when the window closes), or judged.
        readonly struct Flight
        {
            readonly DroneSpec _spec;
            readonly Touch _t;
            readonly Loadout _loadout;
            readonly ContactSpec _contacts;
            readonly ContactScenarios.Site _site;
            readonly Double3 _along;

            public Flight(DroneSpec spec, in Touch t, Loadout loadout, ContactSpec contacts, ContactScenarios.Site site, Double3 along)
            {
                _spec = spec;
                _t = t;
                _loadout = loadout;
                _contacts = contacts;
                _site = site;
                _along = along;
            }

            public Outcome Fly(Quat q, double clear, Double3 v, bool approach, double hold)
            {
                bool powered = _t.Power != Power.Free;
                double low = double.PositiveInfinity;
                foreach (ContactShape sh in _contacts.Shapes)
                {
                    if (sh.Role == ContactRole.Disc) continue;
                    low = Math.Min(low, q.Rotate(sh.A).Y - sh.Radius);
                    low = Math.Min(low, q.Rotate(sh.B).Y - sh.Radius);
                }
                var setup = new FlightSetup
                {
                    Seed = 0x450F, World = _site.Open(), Mass = _spec.BuildMass(_loadout), Contacts = _contacts,
                    StartPosition = _site.Foot + new Double3(0, clear - low, 0), StartAttitude = q, StartVelocity = v,
                };
                if (powered)
                {
                    setup.Propulsion = _spec.BuildPropulsion();
                    if (_t.Esc != null)
                        for (int i = 0; i < 4; i++) setup.Propulsion.Motors[i].EscDisarm = _t.Esc.Value;
                    setup.Aero = _spec.BuildAero();
                    // D-064's micro-motion, as a pilot's flight has it: the motors' spreads, the re-ingested wash (with the
                    // ground's gain) and each rotor's own wake.
                    setup.Asymmetry = _spec.BuildAsymmetry();
                    setup.GroundEffect = _spec.BuildGroundEffect();
                    setup.RotorWake = _spec.BuildRotorWake();
                    setup.StartPackCharge = 0.9;
                }
                var core = new FlightCore(setup);
                _t.Prepare?.Invoke(core);
                var o = new Outcome
                {
                    Reached = double.PositiveInfinity, TouchEnded = double.PositiveInfinity, LaterSpeed = double.NaN, Throttle = hold,
                };
                double weight = core.Body.Mass * G, laterTilt = 0;
                long settled = ThrottleStep + (long)(SpinUpSeconds * 1000);
                if (approach) o.Height = double.NaN;
                bool touched = false, flying = false, ended = false;
                long touch = 0, window = (powered ? ThrottleStep : 0) + (long)(ApproachWindow * 1000);
                long run = (long)((_t.Seconds > 0 ? _t.Seconds : 3) * 1000), hop = (long)(HopSeconds * 1000), airborne = 0;
                while (touched ? core.State.Step - touch < run : core.State.Step < window)
                {
                    long k = core.State.Step;
                    var stick = new StickSample { HostUs = k * FlightCore.StepUs };
                    if (powered)
                    {
                        stick.Arm = k >= ArmStep && !(touched && _t.Power == Power.Disarmed);
                        stick.Throttle = k < ThrottleStep || touched && _t.Power != Power.Held ? 0 : hold;
                    }
                    core.Advance(stick);
                    ref readonly FlightState s = ref core.State;
                    Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
                    double tilt = Math.Acos(Math.Max(-1, Math.Min(1, up.Y))) * 180 / Math.PI;
                    for (int i = 0; i < core.Events.Count; i++)
                    {
                        FlightEvent e = core.Events[i];
                        if (e.Kind == EventKind.Tunnel) o.Tunnels++;
                        if (e.Kind != EventKind.ContactMade) continue;
                        if (!touched)
                        {
                            touched = true;
                            touch = s.Step;
                            ref readonly StepTrace tr = ref core.Trace;
                            o.Touched = true;
                            o.TouchAt = (s.Step - (powered ? ThrottleStep : 0)) * FlightCore.Dt;
                            o.TouchLateral = Double3.Dot(s.Velocity, _along);
                            o.TouchDescent = -s.Velocity.Y;
                            o.TouchThrust = (tr.NetForce - tr.AeroForce - tr.ContactForce + new Double3(0, weight, 0)).Length();
                            o.TouchAntiGravity = s.Fc.AntiGravity;
                            o.TouchTilt = tilt;
                            o.Attitude = s.Attitude;
                        }
                        else if (flying && !ended)
                        {
                            ended = true;
                            o.LaterSpeed = Math.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z);
                        }
                    }
                    if (approach && touched)
                    {
                        Steady(ref o, s, settled);
                        return o;
                    }
                    if (approach && s.Step == settled && !touched)
                    {
                        o.Height = Lowest(s);
                        o.Sink = s.Velocity.Y;
                        o.Drift = Double3.Dot(s.Velocity, _along);
                    }
                    o.EndTilt = tilt;
                    if (!touched) continue;
                    // Once the drone has been off the ground for a hop's flight its next contact is another landing; a turn in the
                    // air before it (a vault off a caught skid) is still the touch's.
                    airborne = core.Trace.ContactsTouching == 0 ? airborne + 1 : 0;
                    if (!flying && airborne >= hop)
                    {
                        flying = true;
                        o.TouchEnded = (s.Step - airborne - touch) * FlightCore.Dt;
                    }
                    if (!ended) o.MaxTilt = Math.Max(o.MaxTilt, tilt);
                    else laterTilt = Math.Max(laterTilt, tilt);
                    if (_t.ReachDeg > 0 && tilt >= _t.ReachDeg && double.IsPositiveInfinity(o.Reached)) o.Reached = (s.Step - touch) * FlightCore.Dt;
                }
                if (approach && !touched) o.Attitude = core.State.Attitude;
                if (approach) Steady(ref o, core.State, settled);
                o.Over = o.MaxTilt > 60;
                o.LaterOver = laterTilt > 60;
                return o;
            }

            // The approach's accelerations over its steady part, from SpinUpSeconds after the throttle's step to the stop.
            void Steady(ref Outcome o, in FlightState s, long settled)
            {
                if (s.Step <= settled || double.IsNaN(o.Height)) return;
                double span = (s.Step - settled) * FlightCore.Dt;
                o.Accel = (s.Velocity.Y - o.Sink) / span;
                o.AccelLateral = (Double3.Dot(s.Velocity, _along) - o.Drift) / span;
            }

            // The lowest point of the body's shapes over the foot, m.
            double Lowest(in FlightState s)
            {
                double lowest = double.PositiveInfinity;
                foreach (ContactShape sh in _contacts.Shapes)
                {
                    if (sh.Role == ContactRole.Disc) continue;
                    lowest = Math.Min(lowest, s.Position.Y + s.Attitude.Rotate(sh.A).Y - sh.Radius);
                    lowest = Math.Min(lowest, s.Position.Y + s.Attitude.Rotate(sh.B).Y - sh.Radius);
                }
                return lowest - _site.Foot.Y;
            }
        }

        /// <summary>The lowest lateral speed (m/s) at which the touchdown tips over, by bisection over [0, 4] to 1 mm/s;
        /// 0 if it tips with none, +∞ if not even at 4 m/s. The lateral speed is the touch's own (a powered approach sets its
        /// start for it). Powered and armed, the outcome need not be monotonic in it above about 2 m/s (N4: from the steady
        /// approach at 10°, 2.6 m/s tips, 3.0 does not, 3.5 does), so the crossing found is one of several.</summary>
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

        /// <summary>A caught skid on dead legs: the ground's pair at μ 10 and each leg at 10 kN/m with its damper at ζ √2 on a
        /// quarter of the mass (overdamped, so a leg returns none of the touch), the rigid-body mechanics' own plastic catch.
        /// The catch does not depend on the leg's spring, and the device keeps model 26's 10 kN/m: on p7g-legs 1.1's derived
        /// 52.3 kN/m the overdamped damper (729 N·s/m) passes the explicit step's stability on a skid end's small effective
        /// mass, and the catch tips the drone at no speed at all past 30–40 kN/m (a sweep of 10–52.3 kN/m at ζ 0.7, 1 and √2:
        /// within 0.6 % of the mechanics to 20 kN/m at every ζ, and at ζ 0.7 to 52.3 kN/m).</summary>
        public static Func<ContactSpec, ContactSpec> DeadLegsCaught(double mass) => c =>
        {
            c = GroundFriction(10)(c);
            c.LegStiffness = 10000;
            c.LegDamping = 2 * Math.Sqrt(2) * Math.Sqrt(c.LegStiffness * mass / 4);
            return c;
        };

        /// <summary>The legs rigid across their tubes, as LandingStability's mechanics has them: model 37's one spring along the
        /// contact's normal and the stick anchor alone across it (no side give, no failure law), then edit if any.</summary>
        public static Func<ContactSpec, ContactSpec> RigidAcross(Func<ContactSpec, ContactSpec> edit = null) => c =>
        {
            c = (edit != null ? edit(c) : c).Clone();
            c.LegSideStiffness = 0;
            c.LegSideDampingRatio = 0;
            c.Legs = null;
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
