using System;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.App
{
    /// <summary>
    /// A scripted pilot on the development keyboard for the plants' look and their frame rate (w16-plants-in-the-air 1.3): it flies
    /// a list of legs, each to a place at a height over the ground and then held there for a while, facing a heading. HillsPilot's
    /// hands (KeyboardSticks' acro, the throttle steered to the curve's command for the height's and the climb's errors about the
    /// hover), with a position held: the speed it wants toward the place, slowing within it, the lean for the speed's error.
    /// Engine-free, frame by frame from the published pose.
    /// </summary>
    public sealed class PlantPilot
    {
        /// <summary>A leg: its name, the place (scene x, z, m), the height over the ground there (m), the speed to it (m/s), the
        /// heading to face while held (degrees, the scene's: 0 north, 90 east), and the seconds to hold it (0: on to the next).</summary>
        public readonly struct Leg
        {
            public readonly string Name;
            public readonly double X, Z, Height, Speed, Face, Hold;

            public Leg(string name, double x, double z, double height, double speed, double face, double hold)
            {
                Name = name; X = x; Z = z; Height = height; Speed = speed; Face = face; Hold = hold;
            }
        }

        /// <summary>The hover's throttle command at 1 kg of payload (HillsPilot's), the attitude's band (°) and the most it tilts (°),
        /// how near a place counts as reached (m) and the speed under which it is held (m/s).</summary>
        public const double Hover = 0.46, Band = 1.5, MaxTiltDeg = 20, Reach = 1.0, Still = 0.4;

        readonly RatePreview _curve;
        readonly Func<double, double, double> _ground;
        readonly Leg[] _legs;
        double _throttle;
        KeyboardKeys _held;

        /// <summary>The leg flown now (Legs.Length once done), whether it is being held, and for how long, s.</summary>
        public int Index { get; private set; }
        public bool Holding { get; private set; }
        public double HeldFor { get; private set; }
        public bool Done => Index >= _legs.Length;
        public Leg Now => _legs[Math.Min(Index, _legs.Length - 1)];

        public PlantPilot(in FcSettings settings, Func<double, double, double> ground, Leg[] legs)
        {
            _curve = new RatePreview();
            _curve.From(settings);
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _legs = legs;
        }

        /// <summary>The keys to hold until the next frame (HillsPilot.Next's contract).</summary>
        public KeyboardKeys Next(in PoseFrame pose, double dt)
        {
            _throttle = Clamp(_throttle + KeyboardSticks.ThrottlePerSecond * dt
                * (((_held & KeyboardKeys.ThrottleUp) != 0 ? 1 : 0) - ((_held & KeyboardKeys.ThrottleDown) != 0 ? 1 : 0)), 0, 1);
            if (Done) return _held = KeyboardKeys.None;
            Leg leg = _legs[Index];
            Double3 x = pose.Position, v = pose.Velocity;
            Double3 f = pose.Attitude.Rotate(new Double3(0, 0, 1)), r = pose.Attitude.Rotate(new Double3(1, 0, 0));
            double up = pose.Attitude.Rotate(new Double3(0, 1, 0)).Y;
            double pitch = Deg(Math.Asin(Clamp(-f.Y, -1, 1))), roll = Deg(Math.Asin(Clamp(-r.Y, -1, 1)));
            double psi = Math.Atan2(f.X, f.Z), sin = Math.Sin(psi), cos = Math.Cos(psi);
            double ahead = v.X * sin + v.Z * cos, right = v.X * cos - v.Z * sin;
            double ground = _ground(x.X, x.Z);

            // The place: the speed toward it, slowing within 2 s of it; the heading to it on the way, the leg's once there.
            double dx = leg.X - x.X, dz = leg.Z - x.Z, d = Math.Sqrt(dx * dx + dz * dz);
            double want = Math.Min(leg.Speed, 0.5 * d), wx = d > 1e-6 ? dx / d * want : 0, wz = d > 1e-6 ? dz / d * want : 0;
            double wantAhead = wx * sin + wz * cos, wantRight = wx * cos - wz * sin;
            double heading = d > 4 ? Deg(Math.Atan2(dx, dz)) : leg.Face;
            double pitchAim = Clamp(4 * (wantAhead - ahead) + 1.5 * wantAhead, -MaxTiltDeg, MaxTiltDeg);
            double rollAim = Clamp(4 * (wantRight - right), -MaxTiltDeg, MaxTiltDeg);
            // The ground ahead at 1 s of the speed: a rise is climbed before it comes.
            double lift = Math.Max(ground, _ground(x.X + v.X, x.Z + v.Z));
            double command = Hover / Math.Max(0.6, up) + 0.05 * (lift + leg.Height - x.Y) - 0.06 * v.Y;

            bool there = d < Reach && Math.Sqrt(v.X * v.X + v.Z * v.Z) < Still && Math.Abs(x.Y - ground - leg.Height) < Math.Max(0.15, 0.1 * leg.Height)
                         && Math.Abs(Wrap(leg.Face - Deg(psi))) < 10;
            if (Holding || there)
            {
                Holding = true;
                HeldFor += dt;
                if (HeldFor >= leg.Hold)
                {
                    Index++;
                    Holding = false;
                    HeldFor = 0;
                }
            }

            KeyboardKeys keys = KeyboardKeys.None;
            if (pitch < pitchAim - Band) keys |= KeyboardKeys.PitchForward;
            else if (pitch > pitchAim + Band) keys |= KeyboardKeys.PitchBack;
            if (roll < rollAim - Band) keys |= KeyboardKeys.RollRight;
            else if (roll > rollAim + Band) keys |= KeyboardKeys.RollLeft;
            double turn = Wrap(heading - Deg(psi));
            if (turn > 3) keys |= KeyboardKeys.YawRight;
            else if (turn < -3) keys |= KeyboardKeys.YawLeft;
            double stick = HillsPilot.StickFor(_curve, Clamp(command, 0.2, 0.8));
            if (_throttle < stick - 0.01) keys |= KeyboardKeys.ThrottleUp;
            else if (_throttle > stick + 0.01) keys |= KeyboardKeys.ThrottleDown;
            return _held = keys;
        }

        static double Deg(double rad) => rad * 180 / Math.PI;
        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
        static double Wrap(double deg) => ((deg % 360) + 540) % 360 - 180;
    }
}
