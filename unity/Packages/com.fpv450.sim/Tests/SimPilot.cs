using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// A simple closed-loop pilot in acro (p6-flight-core 1.5): it flies the sticks the flight controller reads, never
    /// the state. It holds a world position (or a velocity), a height and a heading the way a rate-mode pilot does:
    /// the horizontal error asks for an acceleration, the acceleration for a tilt of the thrust, the tilt's error for
    /// a body rate, and the rate for the stick that the controller's own ACTUAL curve maps to it; the height's error
    /// works the throttle. The integrators hold a steady lean against a steady wind, so the position does not drift.
    /// It moves the sticks at the TX12's 250 Hz report rate (every Period steps) and holds them between. Basic operations
    /// only (no System.Math), so a flight it flies gives the same bits on every runtime.
    ///
    /// Gains (assumed, for a stable hold well inside the controller's own bandwidth): the attitude loop 5 rad/s; the
    /// horizontal loop ω_n 0.77 rad/s with ζ 0.78 and a slow integrator; the throttle 0.1 of stick per m and per m/s, as
    /// PropulsionTests' height hold had it (a height offset holds the trim). Tilt is capped at 35°.
    /// </summary>
    public sealed class SimPilot
    {
        const double G = 9.80665;
        const double RadToDeg = 57.29577951308232;

        public Double3 Target;
        public Double3 TargetVelocity;

        /// <summary>Hold TargetVelocity in the horizontal instead of Target's position (the height is Target's).</summary>
        public bool HoldVelocity;

        /// <summary>The nose's bearing to hold, as a horizontal unit vector in the world (+Z north by default).</summary>
        public Double3 Heading = new Double3(0, 0, 1);

        public double AttitudeGain = 5, PositionGain = 0.6, VelocityGain = 1.2, PositionIntegral = 0.05, VelocityIntegral = 0.3;
        public double YawGain = 2, ThrottleBase = 0.5, ThrottleGain = 0.1, ThrottleDamping = 0.1;
        public double MaxTiltSin = 0.5736; // sin 35°

        /// <summary>Steps between stick reports: 4, the radio's 250 Hz (fc-profiles §1).</summary>
        public int Period = 4;

        double _ix, _iz;
        int _count;
        StickSample _last;
        readonly FlightController _fc;

        public SimPilot(FlightController fc, Double3 target)
        {
            _fc = fc;
            Target = target;
        }

        /// <summary>The sticks for the core's present state, armed, at host time hostUs.</summary>
        public StickSample Sticks(FlightCore core, long hostUs)
        {
            if (_count++ % Period != 0)
            {
                _last.HostUs = hostUs;
                return _last;
            }
            ref readonly FlightState s = ref core.State;
            double dt = FlightCore.Dt * Period;

            // The horizontal acceleration it wants.
            double ax, az;
            if (HoldVelocity)
            {
                double ex = TargetVelocity.X - s.Velocity.X, ez = TargetVelocity.Z - s.Velocity.Z;
                _ix += ex * dt;
                _iz += ez * dt;
                ax = VelocityGain * ex + VelocityIntegral * _ix;
                az = VelocityGain * ez + VelocityIntegral * _iz;
            }
            else
            {
                double ex = Target.X - s.Position.X, ez = Target.Z - s.Position.Z;
                _ix += ex * dt;
                _iz += ez * dt;
                ax = PositionGain * ex + PositionIntegral * _ix - VelocityGain * s.Velocity.X;
                az = PositionGain * ez + PositionIntegral * _iz - VelocityGain * s.Velocity.Z;
            }

            // The thrust's direction for it, the tilt capped.
            var want = new Double3(ax, G, az);
            want = want * (1 / want.Length());
            double horizontal = DetMath.Sqrt(want.X * want.X + want.Z * want.Z);
            if (horizontal > MaxTiltSin)
            {
                double k = MaxTiltSin / horizontal;
                want = new Double3(want.X * k, DetMath.Sqrt(1 - MaxTiltSin * MaxTiltSin), want.Z * k);
            }

            // The body rate that turns the body's up toward it, and the yaw that holds the heading (sin of its error).
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
            Double3 turn = s.Attitude.InverseRotate(Double3.Cross(up, want)) * AttitudeGain;
            Double3 nose = s.Attitude.Rotate(new Double3(0, 0, 1));
            double n = DetMath.Sqrt(nose.X * nose.X + nose.Z * nose.Z);
            double headingError = n > 1e-6 ? (nose.Z * Heading.X - nose.X * Heading.Z) / n : 0;
            Double3 rate = turn + s.Attitude.InverseRotate(new Double3(0, YawGain * headingError, 0));

            // Height on the throttle.
            double throttle = ThrottleBase + ThrottleGain * (Target.Y - s.Position.Y) - ThrottleDamping * s.Velocity.Y;
            throttle = throttle < 0 ? 0 : throttle > 1 ? 1 : throttle;

            // The pilot's axes (DetMath.cs): roll right −ω_z, nose down +ω_x, yaw right +ω_y, in °/s.
            return _last = new StickSample
            {
                HostUs = hostUs,
                Roll = Stick(0, -rate.Z * RadToDeg),
                Pitch = Stick(1, rate.X * RadToDeg),
                Yaw = Stick(2, rate.Y * RadToDeg),
                Throttle = throttle,
                Arm = true,
            };
        }

        // The stick whose ACTUAL setpoint is this rate (°/s), by bisection on the controller's curve.
        double Stick(int axis, double rate)
        {
            double lo = -1, hi = 1;
            if (!(rate < _fc.SetpointOf(axis, 1))) return 1;
            if (!(rate > _fc.SetpointOf(axis, -1))) return -1;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                if (_fc.SetpointOf(axis, mid) < rate) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }
    }
}
