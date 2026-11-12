using System;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.App.Session
{
    /// <summary>
    /// A scripted pilot on the development keyboard over the farmland's hills (w3-farmland-ground 3.1, the scenario "a
    /// flight on the hills replays", which the pilot is not there to fly). From rest at the village's start, armed by its
    /// caller, it climbs to Height over the ground, flies Route at Speed with the ground looked up ahead of it, then lowers
    /// its height's aim at SinkRate through the ground, lands, and pulls the throttle down once the legs have settled; its
    /// caller disarms. Every phase steers to a height, so a throttle stick off its count costs a height, never the landing.
    /// Each frame it reads the
    /// published pose and returns the keys held until the next frame, as a pilot's hands would (KeyboardSticks: acro, so a
    /// key held turns the drone at the half stick's rate and none holds its attitude): pitch and roll within Band of their
    /// aims, the heading by yaw, and the throttle stick steered to the curve's command for the height's and the climb's
    /// errors about the hover. Engine-free: the smoke test flies it in the player (-smokehills), FarmlandFlightTests headless.
    /// </summary>
    public sealed class HillsPilot
    {
        /// <summary>The cruise's height over the ground (m), its speed (m/s), the landing's sink (m/s), and the hover's throttle
        /// command at 1 kg of payload (assumed: the smoke test's house flight's 0.46).</summary>
        public const double Height = 5, Speed = 6.5, SinkRate = 0.6, Hover = 0.46;

        /// <summary>The attitude's band about its aim (°), the most it tilts (°), and how near a waypoint counts as reached (m).</summary>
        public const double Band = 1.5, MaxTiltDeg = 25, Reach = 25;

        /// <summary>
        /// The waypoints from the village's start (566.5, 569; 38 m): south over the fields to the hill's top at (374, 281),
        /// 39.3 m, between its two tree clumps, then north and west down its side to the valley at (110, 576), 15.1 m. Since
        /// 1.2's objects are solid it threads the open fields: within 12 m of its line (each waypoint left at Reach) nothing
        /// stands higher than 3.5 m over the ground, and nothing at all within 12 m of the climb or of the landing run
        /// (FarmlandFlightTests). So every crown, trunk and ruin is at least 12 m off, and the bushes it crosses at least
        /// 1.5 m under the cruise. About 770 m over ground of 15–39 m, a little over two minutes at Speed.
        /// </summary>
        public static readonly (double X, double Z)[] Route =
        {
            (503, 524), (456, 282), (374, 281), (352, 378), (248, 400), (226, 514), (110, 576),
        };

        public enum Phase { Climb, Cruise, Descend, Landed, Done }

        readonly RatePreview _curve;
        readonly Func<double, double, double> _ground;
        double _throttle; // the keyboard's throttle stick as the keys move it (KeyboardSticks.ThrottlePerSecond)
        KeyboardKeys _held; // the keys returned last, which the flight held over the dt that ended this frame
        double _settled; // s the legs have stood still on the ground
        double _sinking; // s since the descent began
        int _next;

        public Phase Now { get; private set; }

        /// <summary>The lowest and highest ground under the drone while it cruised, m: the hills it flew over.</summary>
        public double LowestGround { get; private set; } = double.PositiveInfinity;
        public double HighestGround { get; private set; } = double.NegativeInfinity;

        /// <summary>The pilot on the flight's own throttle curve (its settings) and a ground height at (x, z), the sim's
        /// SampleGround (the drawn terrain's is the same within 0.01 mm, w2-farmland-export design).</summary>
        public HillsPilot(in FcSettings settings, Func<double, double, double> ground)
        {
            _curve = new RatePreview();
            _curve.From(settings);
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
        }

        /// <summary>The keys to hold from now to the next frame, from this frame's pose; dt is the time since the last frame,
        /// over which the flight held the keys returned then (the player's scene reads them a frame late: a stall between
        /// two frames moves the throttle by the keys held across it, so the stick's own count follows the keys held, not
        /// the keys chosen).</summary>
        public KeyboardKeys Next(in PoseFrame pose, double dt)
        {
            _throttle = Clamp(_throttle + KeyboardSticks.ThrottlePerSecond * dt
                * (((_held & KeyboardKeys.ThrottleUp) != 0 ? 1 : 0) - ((_held & KeyboardKeys.ThrottleDown) != 0 ? 1 : 0)), 0, 1);
            if (Now == Phase.Done) return _held = KeyboardKeys.None;
            Double3 x = pose.Position, v = pose.Velocity;
            Double3 f = pose.Attitude.Rotate(new Double3(0, 0, 1)), r = pose.Attitude.Rotate(new Double3(1, 0, 0));
            double up = pose.Attitude.Rotate(new Double3(0, 1, 0)).Y;
            double pitch = Deg(Math.Asin(Clamp(-f.Y, -1, 1))), roll = Deg(Math.Asin(Clamp(-r.Y, -1, 1)));
            double psi = Math.Atan2(f.X, f.Z), sin = Math.Sin(psi), cos = Math.Cos(psi);
            double ahead = v.X * sin + v.Z * cos, right = v.X * cos - v.Z * sin; // m/s along the heading and to its right
            double ground = _ground(x.X, x.Z), agl = x.Y - ground;

            // Drift held: lean against the horizontal speed.
            double pitchAim = Clamp(-3 * ahead, -15, 15), rollAim = Clamp(-3 * right, -15, 15), headingAim = Deg(psi);
            double hover = Hover / Math.Max(0.6, up), command;
            switch (Now)
            {
                case Phase.Climb:
                    command = hover + 0.05 * (Height - agl) - 0.06 * v.Y;
                    if (agl >= Height - 0.5) Now = Phase.Cruise;
                    break;
                case Phase.Cruise:
                {
                    (double wx, double wz) = Route[_next];
                    double dx = wx - x.X, dz = wz - x.Z;
                    if (dx * dx + dz * dz < Reach * Reach && ++_next == Route.Length)
                    {
                        Now = Phase.Descend;
                        goto case Phase.Descend;
                    }
                    headingAim = Deg(Math.Atan2(dx, dz));
                    if (Math.Abs(Wrap(headingAim - Deg(psi))) < 30) pitchAim = Clamp(8 + 4 * (Speed - ahead), -10, MaxTiltDeg);
                    // The ground ahead, at 1 s and 2.5 s of the speed: the hill's rise is climbed before it comes.
                    double lift = Math.Max(ground, Math.Max(_ground(x.X + v.X, x.Z + v.Z), _ground(x.X + 2.5 * v.X, x.Z + 2.5 * v.Z)));
                    command = hover + 0.05 * (lift + Height - x.Y) - 0.06 * v.Y;
                    LowestGround = Math.Min(LowestGround, ground);
                    HighestGround = Math.Max(HighestGround, ground);
                    break;
                }
                case Phase.Descend:
                {
                    // The height's aim sinks from Height at SinkRate, on through the ground to 2 m under it, so the legs press
                    // down whatever the stick's count is off by.
                    _sinking += dt;
                    double aim = ground + Math.Max(Height - SinkRate * _sinking, -2);
                    command = hover + 0.05 * (aim - x.Y) - 0.06 * v.Y;
                    _settled = agl < 0.5 && Math.Abs(v.Y) < 0.1 ? _settled + dt : 0;
                    if (_settled > 0.5) Now = Phase.Landed;
                    break;
                }
                default: // Landed: the throttle down
                    command = 0;
                    break;
            }

            KeyboardKeys keys = KeyboardKeys.None;
            if (Now == Phase.Landed)
            {
                keys = KeyboardKeys.ThrottleDown;
                if (_throttle <= 0) Now = Phase.Done;
            }
            else
            {
                if (pitch < pitchAim - Band) keys |= KeyboardKeys.PitchForward;
                else if (pitch > pitchAim + Band) keys |= KeyboardKeys.PitchBack;
                if (roll < rollAim - Band) keys |= KeyboardKeys.RollRight;
                else if (roll > rollAim + Band) keys |= KeyboardKeys.RollLeft;
                double turn = Wrap(headingAim - Deg(psi));
                if (turn > 3) keys |= KeyboardKeys.YawRight;
                else if (turn < -3) keys |= KeyboardKeys.YawLeft;
                double want = StickFor(_curve, Clamp(command, 0.25, 0.75));
                if (_throttle < want - 0.01) keys |= KeyboardKeys.ThrottleUp;
                else if (_throttle > want + 0.01) keys |= KeyboardKeys.ThrottleDown;
            }
            return _held = keys;
        }

        /// <summary>The throttle stick (0–1) whose command (0–1 of rcCommand) is command on a preview's curve: its inverse,
        /// linear between the samples. command is above the curve's 0 at the stick's foot.</summary>
        public static double StickFor(RatePreview curve, double command)
        {
            double[] t = curve.Throttle;
            for (int i = 1; i < t.Length; i++)
                if (t[i] >= command)
                    return RatePreview.Stick(i - 1) + (RatePreview.Stick(i) - RatePreview.Stick(i - 1)) * (command - t[i - 1]) / (t[i] - t[i - 1]);
            return 1;
        }

        static double Deg(double rad) => rad * 180 / Math.PI;
        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
        static double Wrap(double deg) => ((deg % 360) + 540) % 360 - 180;
    }
}
