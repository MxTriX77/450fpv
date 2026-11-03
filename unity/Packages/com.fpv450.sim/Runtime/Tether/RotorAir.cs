namespace Fpv450.Sim.Tether
{
    /// <summary>
    /// The rotors' air on the line (p7d-tether design 4): momentum theory's flow about each actuator disc, from its own thrust,
    /// so a full-throttle punch moves the line hardest. Each step Begin fixes every rotor's induced velocity and wake; At gives
    /// the rotors' added air velocity at a point, world m/s, summed over the rotors (the wind is the caller's).
    /// - The induced velocity: Glauert's v_i·√(u_p² + (u_a + v_i)²) = v_h², v_h = √(T/(2ρπR²)) (published, Johnson 1980,
    ///   Leishman 2006), with u_a the free stream down through the disc and u_p across it; in a descent (u_a &lt; 0), where
    ///   the momentum solution fails, the hover's v_h (assumed).
    /// - Above a disc (on its intake side), inside its radius: the vortex cylinder's inflow toward the disc,
    ///   v_i·(1 − z/√(z² + R²)) (published, on its axis); outside it: a hemispherical sink of the disc's flux πR²·v_i,
    ///   Q/(2πd²) toward the hub (derived).
    /// - Below it: the wake column of radius far_wake_radius_ratio·R moving at far_wake_speed_ratio·v_i along −n (published
    ///   far wake), laid along the flow through the disc (the free stream with v_i, so a forward flight sweeps it back).
    /// - Where the column meets the ground within wake_reach_over_radius·R along it, a radial wall jet of the column's flux,
    ///   u = πR²·v_i/(2π·r·h_j) outward from where it lands, inside h_j of the ground (mass conservation; h_j from the table).
    /// Only + − × ÷ √; preallocated; the ground under each hub is the caller's (one SampleGround batch with the nodes).
    /// </summary>
    public sealed class RotorAir
    {
        const int Rotors = 4;
        const double Pi = 3.141592653589793;

        // Glauert's iterations (numerical: Newton from v_h converges to rounding within 6 on the drone's flight envelope).
        const int Iterations = 8;

        readonly double _radius, _density, _wakeSpeed, _wakeRadius, _reach, _jetThickness;
        readonly Double3[] _hub = new Double3[Rotors], _normal = new Double3[Rotors], _wake = new Double3[Rotors];
        readonly Double3[] _landing = new Double3[Rotors];
        readonly double[] _induced = new double[Rotors], _ground = new double[Rotors];
        readonly bool[] _jet = new bool[Rotors];

        /// <param name="radius">R, the props' radius, m.</param>
        public RotorAir(TetherTable table, double radius)
        {
            _radius = radius;
            _density = table.Number("fiber.air_density_kg_m3");
            _wakeSpeed = table.Number("rotor_air.far_wake_speed_ratio");
            _wakeRadius = table.Number("rotor_air.far_wake_radius_ratio") * radius;
            _reach = table.Number("rotor_air.wake_reach_over_radius") * radius;
            _jetThickness = table.Number("rotor_air.wall_jet_thickness_over_radius") * radius;
        }

        public double Radius => _radius;

        /// <summary>The wall jet's thickness h_j, m.</summary>
        public double JetThickness => _jetThickness;

        /// <summary>Each rotor's induced velocity this step, m/s.</summary>
        public double Induced(int rotor) => _induced[rotor];

        /// <summary>Each rotor's wake direction this step, world (unit, downstream).</summary>
        public Double3 Wake(int rotor) => _wake[rotor];

        /// <summary>Where each rotor's wake lands on the ground this step, and whether it does within the reach.</summary>
        public Double3 Landing(int rotor) => _landing[rotor];
        public bool Lands(int rotor) => _jet[rotor];

        /// <summary>
        /// The step's rotors: each hub (world m), its disc's normal (world, unit, the intake side), its thrust (N), the air's
        /// velocity at it relative to the hub (world m/s: the wind less the hub's velocity) and the ground's height under it.
        /// </summary>
        public void Begin(Double3[] hubs, Double3 normal, double[] thrust, Double3[] relativeAir, double[] groundUnderHub)
        {
            double area = Pi * _radius * _radius;
            for (int i = 0; i < Rotors; i++)
            {
                _hub[i] = hubs[i];
                _normal[i] = normal;
                double t = thrust[i] > 0 ? thrust[i] : 0;
                double vh = DetMath.Sqrt(t / (2 * _density * area));
                Double3 a = relativeAir[i];
                double down = -Double3.Dot(normal, a); // the free stream down through the disc, as a climb's
                Double3 across = a + normal * down;
                double cross2 = Double3.Dot(across, across);
                double vi = vh;
                if (vh > 0 && down >= 0)
                {
                    // Newton on f(v) = v·√(u_p² + (u_a + v)²) − v_h², increasing for v ≥ 0.
                    for (int k = 0; k < Iterations; k++)
                    {
                        double s = down + vi, root = DetMath.Sqrt(cross2 + s * s);
                        double f = vi * root - vh * vh, df = root + vi * s / root;
                        double next = vi - f / df;
                        vi = next > 0 ? next : 0.5 * vi;
                    }
                }
                _induced[i] = vi;
                // The wake leaves along the flow through the disc: the free stream with v_i down through it.
                Double3 flow = a - normal * vi;
                double len = flow.Length();
                _wake[i] = len > 0 ? flow * (1 / len) : normal * -1.0;
                _ground[i] = groundUnderHub[i];
                _jet[i] = false;
                if (vi > 0 && _wake[i].Y < 0)
                {
                    double along = (hubs[i].Y - groundUnderHub[i]) / -_wake[i].Y;
                    if (along >= 0 && along <= _reach)
                    {
                        _jet[i] = true;
                        _landing[i] = hubs[i] + _wake[i] * along;
                    }
                }
            }
        }

        /// <summary>
        /// The rotors' added air at a point, world m/s, with the ground's height under it (−∞ for none); jet is the wall
        /// jets' share of it (horizontal, the flow along the ground that lifts a line lying in it).
        /// </summary>
        public Double3 At(Double3 p, double groundUnderPoint, out Double3 jet)
        {
            Double3 air = default;
            jet = default;
            for (int i = 0; i < Rotors; i++)
            {
                double vi = _induced[i];
                if (!(vi > 0)) continue;
                Double3 n = _normal[i];
                Double3 q = p - _hub[i];
                double z = Double3.Dot(n, q);
                if (z > 0)
                {
                    // The intake side: the cylinder's axial inflow inside the radius, the sink outside it.
                    Double3 lateral = q - n * z;
                    if (Double3.Dot(lateral, lateral) < _radius * _radius)
                        air = air - n * (vi * (1 - z / DetMath.Sqrt(z * z + _radius * _radius)));
                    else
                    {
                        double d2 = Double3.Dot(q, q), d = DetMath.Sqrt(d2);
                        air = air - q * (vi * _radius * _radius / (2 * d2 * d));
                    }
                    continue;
                }
                // The wake side: inside the column, down through the disc at the far wake's speed, to the ground.
                Double3 w = _wake[i];
                double h = Double3.Dot(q, w);
                if (h > 0)
                {
                    Double3 off = q - w * h;
                    bool above = !_jet[i] || p.Y > _ground[i];
                    if (above && Double3.Dot(off, off) < _wakeRadius * _wakeRadius)
                    {
                        air = air - n * (_wakeSpeed * vi);
                        continue;
                    }
                }
                // The wall jet: outward from where the column lands, inside h_j of the ground.
                if (!_jet[i] || !(p.Y - groundUnderPoint <= _jetThickness)) continue;
                double dx = p.X - _landing[i].X, dz = p.Z - _landing[i].Z, r2 = dx * dx + dz * dz;
                if (!(r2 > _wakeRadius * _wakeRadius)) continue;
                double scale = vi * _radius * _radius / (2 * _jetThickness * r2); // Q/(2π·r·h_j) along (dx, dz)/r
                var u = new Double3(dx * scale, 0, dz * scale);
                air = air + u;
                jet = jet + u;
            }
            return air;
        }
    }
}
