using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The wind study's tracker angles of an attitude (wind.md §1.2 and §6.2 step 4, `track_attitude.py`'s definitions),
    /// for a camera with 0° uptilt on the body axes: with the world's up u in camera coordinates (x right, y down,
    /// z forward), roll = atan2(−u_x, −u_y), positive rolled right; pitch = asin(u_z), positive looking up; the heading
    /// is the optical axis's azimuth, from north toward east, so its rate is positive turning right. They are exactly
    /// the ZYX yaw, pitch and roll (aerospace signs). Basic operations only (DetMath's rule), so the pilot that reads
    /// them flies the same bits on every runtime.
    /// </summary>
    public static class TrackerAngles
    {
        public const double Pi = 3.141592653589793;

        /// <summary>Roll, pitch and heading of an attitude, radians.</summary>
        public static void Of(in Quat attitude, out double roll, out double pitch, out double heading)
        {
            // Body axes (x right, y up, z forward) are the camera's with y flipped: u_camera = (u.x, −u.y, u.z).
            Double3 u = attitude.InverseRotate(new Double3(0, 1, 0));
            roll = Atan2(-u.X, u.Y);
            pitch = Asin(u.Z);
            Double3 f = attitude.Rotate(new Double3(0, 0, 1));
            heading = Atan2(f.X, f.Z);
        }

        /// <summary>An angle wrapped into (−π, π].</summary>
        public static double Wrap(double a)
        {
            while (a > Pi) a -= 2 * Pi;
            while (a <= -Pi) a += 2 * Pi;
            return a;
        }

        public static double Asin(double x)
        {
            x = x > 1 ? 1 : x < -1 ? -1 : x;
            return Atan2(x, DetMath.Sqrt((1 - x) * (1 + x)));
        }

        public static double Atan2(double y, double x)
        {
            if (x > 0) return Atan(y / x);
            if (x < 0) return y < 0 ? Atan(y / x) - Pi : Atan(y / x) + Pi;
            return y > 0 ? Pi / 2 : y < 0 ? -Pi / 2 : 0;
        }

        // atan x: |x| > 1 by π/2 − atan(1/x), then the angle halved twice by atan x = 2·atan(x / (1 + √(1 + x²))) to
        // |x| ≤ tan(π/16) < 0.2, then the series to x^25, whose next term is below 1e-19.
        static double Atan(double x)
        {
            bool negative = x < 0;
            if (negative) x = -x;
            bool inverse = x > 1;
            if (inverse) x = 1 / x;
            x /= 1 + DetMath.Sqrt(1 + x * x);
            x /= 1 + DetMath.Sqrt(1 + x * x);
            double x2 = x * x, t = 0;
            for (int n = 12; n >= 0; n--) t = 1.0 / (2 * n + 1) - x2 * t;
            double a = 4 * x * t;
            if (inverse) a = Pi / 2 - a;
            return negative ? -a : a;
        }
    }

    /// <summary>
    /// K§8.5's simulated pilot (wind.md §6.1; p7c-wind 1.2), the acceptance harness's pilot. It flies the sticks in rate
    /// mode through the core's own controller and sees only the camera:
    /// - **Perception:** frames at 29.917 Hz (the harness captures them, `See`), each seen `Delay` after it was taken. A
    ///   frame holds the horizon roll φ, the camera pitch θ, the heading ψ, the ground's slide across the course v
    ///   (positive to the right), the height and the climb rate.
    /// - **Tilt:** it wants the sideways thrust tilt e* = ē − v/(g·τ_v), and ē follows ē̇ = −v/(g·τ_v·`TrimTau`), so it
    ///   settles on the mean lean into the wind.
    /// - **Split:** φ_ref = ē + s·(e* − ē) and ψ_ref = ψ_course + (1 − s)·(e* − ē)/k, with k = −sin θ_ref (the run's
    ///   mean pitch is θ_ref within the hold's ±2°).
    /// - **Pitch:** θ_ref = `PitchDeg`, clip P's speed regime.
    /// - **Loops:** the Euler rates (ref − angle)/τ per axis, turned into body rates by the exact ZYX relations
    ///   p = φ̇ − ψ̇·sin θ, q = θ̇·cos φ + ψ̇·cos θ·sin φ, r = ψ̇·cos θ·cos φ − θ̇·sin φ, and into sticks through the
    ///   controller's own rate curve (ACTUAL rates), inverted by bisection.
    /// - **Throttle:** the height through a 2 s loop: a climb rate (h_ref − h)/`HeightTau` wanted, its error on the
    ///   throttle with a proportional and an integral share.
    /// - **Stick noise** (D-060), when the harness gives it its stream (`UseRemnant`): the optimal-control model's motor
    ///   noise (Kleinman, Baron and Levison 1970), at its published ratio, not tuned. On each command (the three body
    ///   rates and the throttle), white noise of covariance V_u = π·ρ_u·E{u_c²}, ρ_u at `MotorNoiseDb` and E{u_c²} the
    ///   command's variance about its running mean over `RemnantWindow`, learnt only while the flight is stationary
    ///   (`Learning`: the model's E{u_c²} is a steady flight's, so the harness's release and its climb between the
    ///   stretches are not in it, and before any steady flight the noise is 0), through the neuromotor lag `NeuromotorTau` (so
    ///   its variance is V_u/(2·τ_n)), added to the command; the commands' own path keeps K§8.5's measured τ, which already
    ///   holds the pilot's lag. The model's observation noise (−20 dB) is not carried: it enters that model through its
    ///   optimal estimator, which this pilot has not, and fed raw into K§8.5's laws it is another model (p7c-wind 1.5's
    ///   tasks note its reading). Each draw is a 12-uniform sum (bounded at ±6σ) from the named counter-based stream
    ///   `pilot.motor`, per stick report and command. Without it the pilot is noiseless (the known-wrong).
    /// - **Hover** (D-064's near-ground grade, `Hover`): the same pilot holding a spot instead of a course. It wants the
    ///   tilt that stops the slide on both axes, the roll as above with the whole of it on the horizon (no heading trade
    ///   without forward speed, k = −sin θ ≈ 0) and the pitch θ_ref = ē_p + u/(g·τ_v) from the forward slide u, ē_p trimmed
    ///   as ē is; the heading holds the course. Its loops, delay, τ and stick noise are K§8.5's (assumed to carry over from
    ///   P's cruise to a hover).
    /// It reports the sticks at the TX12's 250 Hz (every `Period` steps). Basic operations only, so its flights give
    /// the same bits on every runtime.
    /// </summary>
    public sealed class HarnessPilot
    {
        /// <summary>The camera's frame rate, Hz (measured: clip P's recording, wind.md §1).</summary>
        public const double CameraHz = 29.917;

        const double G = 9.80665;
        const double RadToDeg = 57.29577951308232, DegToRad = 0.017453292519943295;

        /// <summary>The reaction delay, s (assumed, wind.md §6.1; variants 0 and 0.2).</summary>
        public double Delay = 0.10;

        /// <summary>τ_v, s: the slide's time constant in the tilt wanted (assumed; variants 0.3 and 1.0).</summary>
        public double TauV = 0.5;

        /// <summary>ē's time scale, s: ē̇ = −v/(g·τ_v·TrimTau) (assumed, wind.md §6.1).</summary>
        public double TrimTau = 3;

        /// <summary>s, the tilt's share on the horizon (derived: the belt's 64 %, wind.md §5.5; variants 1 and 0).</summary>
        public double Split = 0.64;

        /// <summary>Clip P's mean camera pitch, degrees (measured, K§8.2).</summary>
        public const double ClipPPitchDeg = -22.7;

        /// <summary>θ_ref, degrees: clip P's speed regime.</summary>
        public double PitchDeg = ClipPPitchDeg;

        /// <summary>Each axis's correction time to 1/e, s (measured, K§8.2).</summary>
        public double TauRoll = 0.30, TauPitch = 0.17, TauYaw = 0.27;

        /// <summary>The height loop's time constant, s (assumed, wind.md §6.1).</summary>
        public double HeightTau = 2;

        /// <summary>
        /// The climb loop under it (assumed): the throttle per m/s of climb-rate error, 0.05, for a 0.5 s climb loop on
        /// about 40 m/s² per unit of throttle at the trim (thrust ∝ throttle² near 0.55); its integral over 2 s; the
        /// throttle it starts from.
        /// </summary>
        public double ClimbGain = 0.05, ClimbIntegralTau = 2, ThrottleStart = 0.55;

        /// <summary>Steps between stick reports: 4, the radio's 250 Hz (fc-profiles §1).</summary>
        public int Period = 4;

        /// <summary>The motor noise ratio, dB: ρ_u = 10^(dB/10) (published: −25 dB, the optimal-control model's, Kleinman,
        /// Baron and Levison 1970; recalled, not re-read).</summary>
        public double MotorNoiseDb = -25;

        /// <summary>The neuromotor lag the motor noise passes through, s (published: about 0.1 s, the same model's τ_n;
        /// recalled, not re-read).</summary>
        public double NeuromotorTau = 0.1;

        /// <summary>The running variance's time constant, s (assumed: long against the pilot's 0.17–0.30 s loops and the
        /// 0.23 Hz band's period, short against a run; set before any grade, never tuned).</summary>
        public double RemnantWindow = 10;

        /// <summary>Whether the flight is stationary now, so the stick noise's E{u_c²} learns from it (the harness clears it
        /// before its discarded start and through its climb between the stretches).</summary>
        public bool Learning = true;

        /// <summary>The course's bearing, radians from north toward east, and the height to hold, m.</summary>
        public double Course, Height;

        /// <summary>Hold a spot (a hover) instead of flying the course at PitchDeg.</summary>
        public bool Hover;

        /// <summary>One camera frame: the step it was taken at (relative to the release) and what it shows.</summary>
        public struct Frame
        {
            public long Step;
            public double Roll, Pitch, Heading, Slide, Height, Climb;

            /// <summary>The ground's slide along the course, m/s (forward positive): the hover's pitch reads it.</summary>
            public double Forward;
        }

        readonly FlightController _fc;
        readonly Frame[] _frames = new Frame[16];
        long _taken, _used;
        double _meanTilt, _meanPitch, _throttleIntegral;
        int _count;
        StickSample _last;

        // The stick noise: its stream, each command's running mean and variance and its noise through the neuromotor lag,
        // and the stick reports so far (the draws' index).
        bool _remnant;
        SeedStream _motor;
        readonly double[] _mean = new double[4], _variance = new double[4], _lagged = new double[4];
        readonly long[] _learnt = new long[4];
        long _reports;

        public HarnessPilot(FlightController fc)
        {
            _fc = fc;
        }

        /// <summary>Gives the pilot the optimal-control model's stick noise, drawn from the flight seed's named stream
        /// `pilot.motor`.</summary>
        public void UseRemnant(ulong seed)
        {
            _motor = new FlightRandom(seed).Open("pilot.motor");
            _remnant = true;
        }

        /// <summary>A frame the camera took (in time order).</summary>
        public void See(in Frame frame) => _frames[_taken++ % _frames.Length] = frame;

        /// <summary>The camera frame of a state: its tracker angles, the slide across a course and the height.</summary>
        public static Frame Take(in FlightState s, long step, double course)
        {
            TrackerAngles.Of(s.Attitude, out double roll, out double pitch, out double heading);
            DetMath.SinCos(course, out double sin, out double cos);
            return new Frame
            {
                Step = step, Roll = roll, Pitch = pitch, Heading = heading,
                Slide = s.Velocity.X * cos - s.Velocity.Z * sin, // the course's right is (cos, 0, −sin)
                Forward = s.Velocity.X * sin + s.Velocity.Z * cos, // the course is (sin, 0, cos)
                Height = s.Position.Y, Climb = s.Velocity.Y,
            };
        }

        /// <summary>The sticks at a step (relative to the release), armed, at host time hostUs.</summary>
        public StickSample Sticks(long step, long hostUs)
        {
            if (_count++ % Period != 0)
            {
                _last.HostUs = hostUs;
                return _last;
            }

            // The frames seen by now: each one taken Delay ago or earlier, the integrators stepped once per new frame.
            long delaySteps = (long)(Delay * 1000 + 0.5);
            long seen = _used;
            while (seen < _taken && _frames[seen % _frames.Length].Step + delaySteps <= step)
            {
                ref readonly Frame f = ref _frames[seen % _frames.Length];
                double dt = 1 / CameraHz;
                _meanTilt -= f.Slide / (G * TauV * TrimTau) * dt;
                if (Hover) _meanPitch += f.Forward / (G * TauV * TrimTau) * dt;
                _throttleIntegral += ClimbGain / ClimbIntegralTau * ClimbError(f) * dt;
                seen++;
            }
            _used = seen;
            if (seen == 0)
                return _last = new StickSample { HostUs = hostUs, Throttle = ThrottleStart, Arm = true };
            ref readonly Frame v = ref _frames[(seen - 1) % _frames.Length];

            // The tilt it wants, split between the horizon and the heading.
            DetMath.SinCos(PitchDeg * DegToRad, out double sinRef, out _);
            double k = -sinRef;
            double change = -v.Slide / (G * TauV); // e* − ē
            double rollRef = _meanTilt + (Hover ? change : Split * change);
            double headingRef = Hover ? Course : Course + (1 - Split) * change / k;
            double pitchRef = Hover ? _meanPitch + v.Forward / (G * TauV) : PitchDeg * DegToRad;

            // The Euler rates, then the body rates by the exact ZYX relations.
            double rollRate = (rollRef - v.Roll) / TauRoll;
            double pitchRate = (pitchRef - v.Pitch) / TauPitch;
            double headingRate = TrackerAngles.Wrap(headingRef - v.Heading) / TauYaw;
            DetMath.SinCos(v.Roll, out double sinRoll, out double cosRoll);
            DetMath.SinCos(v.Pitch, out double sinPitch, out double cosPitch);
            double p = rollRate - headingRate * sinPitch;
            double q = pitchRate * cosRoll + headingRate * cosPitch * sinRoll;
            double r = headingRate * cosPitch * cosRoll - pitchRate * sinRoll;

            double throttle = ThrottleStart + _throttleIntegral + ClimbGain * ClimbError(v);
            if (_remnant)
            {
                p += Motor(0, p, _reports, Learning);
                q += Motor(1, q, _reports, Learning);
                r += Motor(2, r, _reports, Learning);
                throttle += Motor(3, throttle, _reports, Learning);
                _reports++;
            }
            throttle = throttle < 0 ? 0 : throttle > 1 ? 1 : throttle;

            // The pilot's axes (DetMath.cs): roll right +p; stick forward is nose down, −q; yaw right +r; °/s.
            return _last = new StickSample
            {
                HostUs = hostUs,
                Roll = Stick(0, p * RadToDeg),
                Pitch = Stick(1, -q * RadToDeg),
                Yaw = Stick(2, r * RadToDeg),
                Throttle = throttle,
                Arm = true,
            };
        }

        double ClimbError(in Frame f) => (Height - f.Height) / HeightTau - f.Climb;

        // Command i's motor noise at stick report `report` (from 0, once per report and command): when `learn`, its running
        // mean and variance (exponentially weighted over RemnantWindow, the plain mean until it has learnt that many
        // reports) updated with the command; then one draw of white noise of covariance π·ρ_u·E{u_c²} held over the report,
        // through the neuromotor lag.
        internal double Motor(int i, double command, long report, bool learn)
        {
            double dt = Period * 0.001, window = RemnantWindow / dt;
            if (learn)
            {
                long n = ++_learnt[i];
                double alpha = n < window ? 1.0 / n : 1 / window;
                double d = command - _mean[i];
                _mean[i] += alpha * d;
                _variance[i] = (1 - alpha) * (_variance[i] + alpha * d * d);
            }
            double ratio = DetMath.Exp(MotorNoiseDb * 0.23025850929940458); // 10^(dB/10)
            double white = DetMath.Sqrt(TrackerAngles.Pi * ratio * _variance[i] / dt) * Normal(_motor, (ulong)report * 4 + (ulong)i);
            double a = DetMath.Exp(-dt / NeuromotorTau);
            _lagged[i] = a * _lagged[i] + (1 - a) * white;
            return _lagged[i];
        }

        // Draw i of a stream as a nearly normal value: 12 uniforms less 6 (Irwin–Hall), bounded by ±6.
        static double Normal(in SeedStream stream, ulong i)
        {
            double sum = -6;
            for (ulong j = 0; j < 12; j++) sum += (stream.BitsAt(12 * i + j) >> 11) * (1.0 / 9007199254740992.0);
            return sum;
        }

        // The stick whose ACTUAL setpoint is this rate (°/s), by bisection on the controller's curve.
        double Stick(int axis, double rate)
        {
            if (!(rate < _fc.SetpointOf(axis, 1))) return 1;
            if (!(rate > _fc.SetpointOf(axis, -1))) return -1;
            double lo = -1, hi = 1;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                if (_fc.SetpointOf(axis, mid) < rate) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }
    }
}
