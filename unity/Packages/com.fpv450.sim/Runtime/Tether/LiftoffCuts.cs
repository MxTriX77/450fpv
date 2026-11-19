using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tether
{
    /// <summary>
    /// The line's cut at a liftoff (p7d-tether design 26): an empirical law, the pilot's account (2026-11-18; D-104, D-121), not
    /// derived. At a liftoff the line's own mechanics stay far under any strength (the exit's tension 0.01–0.05 N, the hole's
    /// edge 0.2–0.5 GPa: design 25), yet the pilot loses the line at most very sharp liftoffs. The law gives each liftoff one
    /// chance, from how hard it is and how it leans, and one seeded draw against it.
    ///
    /// A liftoff begins at the first step the drone touches nothing after a step it touched something, armed, having rested
    /// since its last liftoff (launch.liftoff_rest_s in contact; the flight starts rested). It lasts while the drone is still over
    /// the line it lifted off with: until it has paid out launch.liftoff_window_payout_m since the leave (a climb pays out about
    /// its height, flying away low the ground it covers, flying backwards over its own line nothing), or it rests again.
    /// Its chance, held at its running most:
    /// - the pressure: the rotors' thrust over the weight, averaged over the first launch.liftoff_window_s after the leave (so it
    ///   grows with the thrust given and with how fast it came), through launch.liftoff_cut_by_thrust (careful rare, a very sharp
    ///   liftoff 87 %, max throttle 95 %); plus launch.liftoff_lean_extra if in that time the nose rose or the drone rolled past
    ///   launch.liftoff_lean_deg (the pilot's unnecessary roll or pitch backwards); at most launch.liftoff_safe_chance if the nose
    ///   was down by that much on the mean (the pilot's safe launch, throttle with pitch forward);
    /// - backwards: the most speed tail first over the ground in the liftoff, through launch.liftoff_cut_by_back_speed (backwards
    ///   too much with the throttle not yet high, 40 %);
    /// the larger of the two, never above launch.liftoff_cut_cap (below 100 %). The draw u (stream fiber.liftoff, one per
    /// liftoff, counter-based on the liftoff's number) is made as the liftoff begins; the line parts at the hole's edge the first
    /// step the chance passes u, so the chance is the liftoff's whole cut rate. While a liftoff lasts the hole's edge is this
    /// law's alone: FiberCuts' design 25 Weibull yields to it (FiberCuts.EdgeYields), so the two never count one liftoff twice.
    /// Each liftoff logs EventKind.Liftoff as it begins (its draw) and as it ends (its chance and inputs).
    /// </summary>
    public sealed class LiftoffCuts
    {
        /// <summary>The Liftoff event's Index bits: the begin (else the end), and at the end the pilot's safe launch, the lean
        /// past the bound, the line cut, the drone resting again.</summary>
        public const byte Began = 0x80, SafeLaunch = 0x01, Leaned = 0x02, CutHere = 0x04, Rested = 0x08;

        const double Pi = 3.141592653589793;

        readonly double _window, _payout, _rest, _leanSin, _extra, _safe, _cap;
        readonly double[] _byThrust, _bySpeed;
        SeedStream _stream;

        bool _touched, _rested = true, _open;
        double _restTime;
        long _from;
        double _u, _paid0, _sum, _forward, _pressure, _back, _chance;
        int _count;
        byte _flags;
        Double3 _at;

        public LiftoffCuts(TetherTable table, FlightRandom random)
        {
            _window = table.Number("launch.liftoff_window_s");
            _payout = table.Number("launch.liftoff_window_payout_m");
            _rest = table.Number("launch.liftoff_rest_s");
            _leanSin = DetMath.Cos(Pi / 2 - table.Number("launch.liftoff_lean_deg") * (Pi / 180));
            _extra = table.Number("launch.liftoff_lean_extra");
            _safe = table.Number("launch.liftoff_safe_chance");
            _cap = table.Number("launch.liftoff_cut_cap");
            _byThrust = table.Row("launch.liftoff_cut_by_thrust").Values;
            _bySpeed = table.Row("launch.liftoff_cut_by_back_speed").Values;
            _stream = random.Open("fiber.liftoff");
        }

        /// <summary>Whether a liftoff lasts (the hole's edge is this law's).</summary>
        public bool Open => _open;

        /// <summary>The liftoff's chance so far (the last liftoff's after it ended), and its draw.</summary>
        public double Chance => _chance;
        public double Draw => _u;

        /// <summary>The last liftoff's thrust over weight over its window, and its most speed tail first, m/s.</summary>
        public double Pressure => _pressure;
        public double Back => _back;

        /// <summary>Liftoffs begun this flight.</summary>
        public ulong Count => _stream.Count;

        /// <summary>
        /// One step after the line's solve: touching, whether the drone touched anything in the step; thrustOverWeight, the rotors'
        /// thrust over the weight; paidOut, the line paid out, m. True when this liftoff's chance passed its draw in this step (the
        /// caller parts the line at the hole's edge; the liftoff has ended). part: the coil's part for the events.
        /// </summary>
        public bool Step(in FlightState s, bool touching, double thrustOverWeight, double paidOut, double dt, StepEvents events, int part)
        {
            bool restedNow = false;
            if (touching)
            {
                _restTime += dt;
                if (_restTime >= _rest)
                {
                    restedNow = _open;
                    _rested = true;
                }
            }
            else _restTime = 0;
            if (!_open)
            {
                if (!touching && _touched && _rested && s.Armed) Begin(s, thrustOverWeight, paidOut, events, part);
                _touched = touching;
                return false;
            }
            _touched = touching;

            // The window: the pressure and the lean over its first seconds.
            double t = (s.Step - _from) * dt;
            Double3 forward = s.Attitude.Rotate(new Double3(0, 0, 1)), right = s.Attitude.Rotate(new Double3(1, 0, 0));
            if (double.IsNaN(_pressure))
            {
                _sum += thrustOverWeight;
                _forward -= forward.Y;
                _count++;
                if (forward.Y >= _leanSin || right.Y >= _leanSin || -right.Y >= _leanSin) _flags |= Leaned;
                if (t >= _window || restedNow) Reckon();
            }
            // Tail first over the ground: the speed against the nose's heading.
            double h = DetMath.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
            if (h > 0)
            {
                double back = -(s.Velocity.X * forward.X + s.Velocity.Z * forward.Z) / h;
                if (back > _back) _back = back;
            }
            double p = ChanceNow();
            if (p > _chance) _chance = p;
            if (_u < _chance)
            {
                if (double.IsNaN(_pressure)) Reckon();
                End(s, (byte)(_flags | CutHere), events, part);
                return true;
            }
            if (restedNow) End(s, (byte)(_flags | Rested), events, part);
            else if (paidOut - _paid0 >= _payout)
            {
                if (double.IsNaN(_pressure)) Reckon();
                End(s, _flags, events, part);
            }
            return false;
        }

        void Begin(in FlightState s, double thrustOverWeight, double paidOut, StepEvents events, int part)
        {
            ulong index = _stream.Count;
            _u = (_stream.NextBits() >> 11) * (1.0 / 9007199254740992.0);
            _open = true;
            _rested = false;
            _from = s.Step;
            _at = s.Position;
            _paid0 = paidOut;
            _sum = 0;
            _forward = 0;
            _count = 0;
            _pressure = double.NaN;
            _back = 0;
            _chance = 0;
            _flags = 0;
            FlightEvent e = Event(index, part, _at, _u, thrustOverWeight, paidOut);
            e.Index = Began;
            events.Add(e);
        }

        void Reckon()
        {
            _pressure = _count > 0 ? _sum / _count : 0;
            if (_count > 0 && _forward / _count >= _leanSin) _flags |= SafeLaunch;
        }

        double ChanceNow()
        {
            double p = 0;
            if (!double.IsNaN(_pressure))
            {
                p = Curve(_byThrust, _pressure);
                if ((_flags & Leaned) != 0) p += _extra;
                if ((_flags & SafeLaunch) != 0 && p > _safe) p = _safe;
            }
            double b = Curve(_bySpeed, _back);
            if (b > p) p = b;
            return p > _cap ? _cap : p;
        }

        void End(in FlightState s, byte flags, StepEvents events, int part)
        {
            _open = false;
            _flags = flags;
            FlightEvent e = Event(_stream.Count - 1, part, s.Position, _chance, _pressure, _back);
            e.Index = flags;
            events.Add(e);
        }

        static FlightEvent Event(ulong index, int part, Double3 point, double a, double b, double c)
        {
            FlightEvent e = FlightEvent.Of(EventKind.Liftoff);
            e.Object = FiberContacts.DroneObject;
            e.Shape = (int)index;
            e.Part = part >= 0 ? (ushort)part : FlightEvent.NoPart;
            e.Point = point;
            e.A = a;
            e.B = b;
            e.C = c;
            return e;
        }

        /// <summary>A chance table's value at x: [[x, chance], …] piecewise linear, held beyond its ends.</summary>
        public static double Curve(double[] xy, double x)
        {
            int n = xy.Length / 2;
            if (!(x > xy[0])) return xy[1];
            for (int i = 1; i < n; i++)
                if (x < xy[2 * i])
                {
                    double x0 = xy[2 * i - 2], y0 = xy[2 * i - 1];
                    return y0 + (xy[2 * i + 1] - y0) * (x - x0) / (xy[2 * i] - x0);
                }
            return xy[2 * n - 1];
        }
    }
}
