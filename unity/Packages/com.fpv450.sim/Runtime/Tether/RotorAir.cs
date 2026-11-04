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
    /// The fountain and the ground vortex (p7d-tether 1.6b, D-075), the paths by which a line under or ahead of the drone
    /// reaches a disc (off for the known-wrong design 4 field, FlightSetup.TetherDesign4Air):
    /// - The fountain: where the wall jets of two rotors meet (at a point, the two strongest; their meeting line the locus of
    ///   equal speed, the measured stagnation line of equal rotors at their midpoint, Lee et al. 2026) the jets' flux and
    ///   momentum normal to it turn up into a plane jet, its momentum fountain_momentum_ratio of theirs (derived from Dekker
    ///   et al. 2023's fountain reaching 3R at H/R 4), self-similar and Gaussian across with the plane jet's spreading rate
    ///   (published), its virtual origin where it carries the jets' flux, rising linearly from the ground's stagnation line
    ///   through the jets' thickness h_j (derived: stagnation flow); from the ground up to the colliding discs' plane, where
    ///   their intake takes it (re-ingestion: at H/R 2 the measured fountain rises above the discs and is re-ingested); never
    ///   inside a wake column. Whole in the hover-like regime, fading to nothing as μ* = U/v_h rises from
    ///   hover_like_advance_ratio to ground_vortex_advance_ratio's start (published bounds, Wolf et al. 2022 after Sheridan
    ///   and Wiesner 1977 and Curtiss et al. 1984; the linear fade assumed).
    /// - The ground vortex: each rotor whose wake lands in a stream along the ground (U, the air at the hub across the
    ///   vertical less the hub's motion) rolls its upstream wall jet into a vortex ahead of the landing point at
    ///   offset·R + penetration·r_p, r_p = R²·v_i/(2·h_j·U) where the jet meets the stream (derived from Wolf et al. 2022's
    ///   full-scale centres, 1.07 R and 0.49 R ahead of the hub at μ* 0.55 and 0.80), at the jet's height, spanning the
    ///   wake's width, a straight vortex with a Scully core and its image under the ground, its circulation
    ///   ground_vortex_circulation·v_h·R (derived from Wolf et al.'s speeds under it) growing from hover to
    ///   hover_like_advance_ratio, fading out over ground_vortex_advance_ratio (published bounds; the ramps assumed); a rotor
    ///   whose upstream jet meets another rotor's landing wake (within its span, ahead of it) forms none (assumed).
    /// Only + − × ÷ √ and DetMath.Exp; preallocated; the ground under each hub is the caller's (one SampleGround batch with
    /// the nodes).
    /// </summary>
    public sealed class RotorAir
    {
        const int Rotors = 4;
        const double Pi = 3.141592653589793;

        // ln 2: the Gaussian's half-width at half its height (the plane jet's half-width b, Pope 2000's spreading rate).
        const double Ln2 = 0.6931471805599453;

        // Glauert's iterations (numerical: Newton from v_h converges to rounding within 6 on the drone's flight envelope).
        const int Iterations = 8;

        readonly double _radius, _density, _wakeSpeed, _wakeRadius, _reach, _jetThickness;
        readonly Double3[] _hub = new Double3[Rotors], _normal = new Double3[Rotors], _wake = new Double3[Rotors];
        readonly Double3[] _landing = new Double3[Rotors];
        readonly double[] _induced = new double[Rotors], _ground = new double[Rotors];
        readonly bool[] _jet = new bool[Rotors];

        // 1.6b: the fountain's and the ground vortex's constants (the table's) and the step's state.
        readonly bool _recirculation;
        readonly double _fountainMomentum, _spread, _hoverLike, _vortexEnd, _vortexGone, _vortexOffset, _vortexPenetration;
        readonly double _vortexHeight, _vortexCore, _vortexCirculation, _vortexSpan;
        readonly double _profileMass, _profileMomentum; // ∫e^(−ln2·η²)dη = √(π/ln2), ∫e^(−2·ln2·η²)dη = √(π/(2·ln2))
        readonly double _jetSpeed, _originScale, _gaussScale, _centreScale, _inverseJet; // the fountain's constants, once

        // The fountain's Gaussian is not evaluated beyond e^−40 (4e-18) of its peak, 7.6 half-widths out (numerical).
        const double CutOff = -40;
        readonly double[] _hover = new double[Rotors], _fade = new double[Rotors], _advance = new double[Rotors];
        readonly double[] _circulation = new double[Rotors];
        readonly Double3[] _stream = new Double3[Rotors], _vortexA = new Double3[Rotors], _vortexB = new Double3[Rotors];
        readonly Double3[] _vortexCentre = new Double3[Rotors];

        /// <param name="radius">R, the props' radius, m.</param>
        /// <param name="recirculation">The fountain and the ground vortex (1.6b); false for design 4's field alone, the
        /// known-wrong (FlightSetup.TetherDesign4Air).</param>
        public RotorAir(TetherTable table, double radius, bool recirculation = true)
        {
            _radius = radius;
            _density = table.Number("fiber.air_density_kg_m3");
            _wakeSpeed = table.Number("rotor_air.far_wake_speed_ratio");
            _wakeRadius = table.Number("rotor_air.far_wake_radius_ratio") * radius;
            _reach = table.Number("rotor_air.wake_reach_over_radius") * radius;
            _jetThickness = table.Number("rotor_air.wall_jet_thickness_over_radius") * radius;
            _recirculation = recirculation;
            _fountainMomentum = table.Number("rotor_air.fountain_momentum_ratio");
            _spread = table.Number("rotor_air.plane_jet_spreading_rate");
            _hoverLike = table.Number("rotor_air.hover_like_advance_ratio");
            double[] end = table.Row("rotor_air.ground_vortex_advance_ratio").Values;
            _vortexEnd = end[0];
            _vortexGone = end[1];
            _vortexOffset = table.Number("rotor_air.ground_vortex_offset_over_radius") * radius;
            _vortexPenetration = table.Number("rotor_air.ground_vortex_penetration_ratio");
            _vortexHeight = table.Number("rotor_air.ground_vortex_height_over_jet") * _jetThickness;
            _vortexCore = table.Number("rotor_air.ground_vortex_core_over_height") * _vortexHeight;
            _vortexCirculation = table.Number("rotor_air.ground_vortex_circulation");
            _vortexSpan = table.Number("rotor_air.ground_vortex_half_span_over_radius") * radius;
            _profileMass = DetMath.Sqrt(Pi / Ln2);
            _profileMomentum = DetMath.Sqrt(Pi / (2 * Ln2));
            _jetSpeed = radius * radius / (2 * _jetThickness);
            _originScale = _profileMomentum / (_profileMass * _profileMass * _spread);
            _gaussScale = Ln2 / (_spread * _spread);
            _centreScale = 1 / (_profileMomentum * _spread);
            _inverseJet = 1 / _jetThickness;
        }

        public double Radius => _radius;

        /// <summary>The wall jet's thickness h_j, m.</summary>
        public double JetThickness => _jetThickness;

        /// <summary>Whether the fountain and the ground vortex act (false: design 4's field, the known-wrong).</summary>
        public bool Recirculation => _recirculation;

        /// <summary>Each rotor's induced velocity this step, m/s.</summary>
        public double Induced(int rotor) => _induced[rotor];

        /// <summary>Each rotor's hover induced velocity v_h from its thrust this step, m/s.</summary>
        public double Hover(int rotor) => _hover[rotor];

        /// <summary>Each rotor's normalised advance ratio μ* = U/v_h this step (U the stream along the ground at its hub).</summary>
        public double Advance(int rotor) => _advance[rotor];

        /// <summary>Each rotor's wake direction this step, world (unit, downstream).</summary>
        public Double3 Wake(int rotor) => _wake[rotor];

        /// <summary>Where each rotor's wake lands on the ground this step, and whether it does within the reach.</summary>
        public Double3 Landing(int rotor) => _landing[rotor];
        public bool Lands(int rotor) => _jet[rotor];

        /// <summary>Each rotor's fountain share this step (1 in the hover-like regime, 0 from the ground vortex's end).</summary>
        public double FountainShare(int rotor) => _fade[rotor];

        /// <summary>Each rotor's ground vortex this step: its circulation, m²/s (0: none), its centre and its ends, world m
        /// (its axis from A to B, the right-hand sense of the circulation: the flow upstream along the ground under it).</summary>
        public double VortexCirculation(int rotor) => _circulation[rotor];
        public Double3 VortexCentre(int rotor) => _vortexCentre[rotor];
        public Double3 VortexA(int rotor) => _vortexA[rotor];
        public Double3 VortexB(int rotor) => _vortexB[rotor];

        /// <summary>The ground vortex's core radius, m.</summary>
        public double VortexCore => _vortexCore;

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
                _hover[i] = vh;
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
                // The stream along the ground at the hub (the air across the vertical) and μ* on it.
                double ux = a.X, uz = a.Z, u = DetMath.Sqrt(ux * ux + uz * uz);
                _advance[i] = vh > 0 ? u / vh : 0;
                _stream[i] = u > 0 ? new Double3(ux / u, 0, uz / u) : default;
                double mu = _advance[i];
                _fade[i] = mu <= _hoverLike ? 1 : mu >= _vortexEnd ? 0 : (_vortexEnd - mu) / (_vortexEnd - _hoverLike);
                _circulation[i] = 0;
                if (!_recirculation || !_jet[i] || !(u > 0) || !(vh > 0)) continue;
                double grow = mu < _hoverLike ? mu / _hoverLike : 1;
                if (mu > _vortexEnd) grow = mu < _vortexGone ? (_vortexGone - mu) / (_vortexGone - _vortexEnd) : 0;
                if (!(grow > 0)) continue;
                double penetration = _radius * _radius * vi / (2 * _jetThickness * u);
                double ahead = _vortexOffset + _vortexPenetration * penetration;
                Double3 up = new Double3(0, 1, 0), st = _stream[i];
                Double3 centre = _landing[i] - st * ahead + up * _vortexHeight;
                var axis = new Double3(st.Z, 0, -st.X); // up × the stream: the flow upstream under it, up at its upstream side
                _vortexCentre[i] = centre;
                _vortexA[i] = centre - axis * _vortexSpan;
                _vortexB[i] = centre + axis * _vortexSpan;
                _circulation[i] = _vortexCirculation * vh * _radius * grow;
            }
            // A rotor whose upstream wall jet meets another's landing wake forms no vortex of its own: shadowed by a landing
            // ahead of it, wholly within one span across, not at all beyond two, ramped over a span along (assumed).
            if (!_recirculation) return;
            for (int j = 0; j < Rotors; j++)
            {
                if (_circulation[j] == 0) continue;
                Double3 st = _stream[j];
                double keep = 1;
                for (int i = 0; i < Rotors; i++)
                {
                    if (i == j || !_jet[i]) continue;
                    double dx = _landing[i].X - _landing[j].X, dz = _landing[i].Z - _landing[j].Z;
                    double along = -(dx * st.X + dz * st.Z), lateral = DetMath.Abs(dx * st.Z - dz * st.X);
                    double a = along / _vortexSpan, b = 2 - lateral / _vortexSpan;
                    a = a < 0 ? 0 : a > 1 ? 1 : a;
                    b = b < 0 ? 0 : b > 1 ? 1 : b;
                    keep = keep * (1 - a * b);
                }
                _circulation[j] = _circulation[j] * keep;
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
            bool column = false;
            int first = -1, second = -1; // the two strongest wall jets here, by v_i²/r² (their speeds' order)
            double firstKey = 0, secondKey = 0, firstZ = 0, secondZ = 0;
            for (int i = 0; i < Rotors; i++)
            {
                double vi = _induced[i];
                if (!(vi > 0)) continue;
                Double3 n = _normal[i];
                Double3 q = p - _hub[i];
                double z = Double3.Dot(n, q);
                if (_recirculation && _jet[i])
                {
                    double fx = p.X - _landing[i].X, fz = p.Z - _landing[i].Z, f2 = fx * fx + fz * fz;
                    if (f2 > _wakeRadius * _wakeRadius)
                    {
                        double key = vi * vi / f2;
                        if (key > firstKey)
                        {
                            second = first; secondKey = firstKey; secondZ = firstZ;
                            first = i; firstKey = key; firstZ = z;
                        }
                        else if (key > secondKey)
                        {
                            second = i; secondKey = key; secondZ = z;
                        }
                    }
                }
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
                        column = true;
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
            if (!_recirculation) return air;
            if (!column && second >= 0 && firstZ <= 0 && secondZ <= 0)
                air = air + new Double3(0, Fountain(p, groundUnderPoint, first, second), 0);
            for (int i = 0; i < Rotors; i++)
            {
                double g = _circulation[i];
                if (g == 0) continue;
                // The vortex and its image under the ground (the ground's height at the landing): no flow through the ground.
                double ground = _landing[i].Y;
                Double3 a = _vortexA[i], b = _vortexB[i];
                air = air + Segment(p, a, b, g);
                air = air + Segment(p, new Double3(a.X, 2 * ground - a.Y, a.Z), new Double3(b.X, 2 * ground - b.Y, b.Z), -g);
            }
            return air;
        }

        // The fountain's upward speed at p from the wall jets of rotors k and m (k's the stronger here): their meeting line
        // the locus u_k = u_m, p's distance from it (u_k² − u_m²)/(2·|u_m·∇u_k − u_k·∇u_m|), exact for equal jets (the
        // midline) and well conditioned for unequal ones; the jets' parts normal to it turn up.
        double Fountain(Double3 p, double groundUnderPoint, int k, int m)
        {
            double height = p.Y - groundUnderPoint;
            if (!(height >= 0)) return 0;
            double kx = p.X - _landing[k].X, kz = p.Z - _landing[k].Z, ik = 1 / (kx * kx + kz * kz), rk = DetMath.Sqrt(kx * kx + kz * kz);
            double mx = p.X - _landing[m].X, mz = p.Z - _landing[m].Z, im = 1 / (mx * mx + mz * mz), rm = DetMath.Sqrt(mx * mx + mz * mz);
            double uk = _induced[k] * _jetSpeed * rk * ik, um = _induced[m] * _jetSpeed * rm * im; // Q/(2π·r·h_j)
            // ∇u = −u·(p − L)/r²; u_m·∇u_k − u_k·∇u_m points across the line toward k's landing.
            double gx = uk * um * (mx * im - kx * ik), gz = uk * um * (mz * im - kz * ik);
            double gn = DetMath.Sqrt(gx * gx + gz * gz);
            if (!(gn > 0)) return 0;
            double ig = 1 / gn, nx = gx * ig, nz = gz * ig;
            double delta = (uk * uk - um * um) * 0.5 * ig;
            double kn = -uk * (kx * nx + kz * nz) * rk * ik, mn = um * (mx * nx + mz * nz) * rm * im;
            kn = kn > 0 ? kn : 0;
            mn = mn > 0 ? mn : 0;
            double flux = _jetThickness * (kn + mn), momentum = _fountainMomentum * _jetThickness * (kn * kn + mn * mn);
            if (!(momentum > 0)) return 0;
            // The plane jet: w_c²·b·∫f² = M, b = S·(y + y₀), y₀ where its flux w_c·b·∫f is the jets'.
            double y = height + _originScale * flux * flux / momentum, iy = 1 / y;
            double exponent = -_gaussScale * delta * delta * iy * iy; // −ln2·δ²/b²
            if (!(exponent > CutOff)) return 0;
            double centre = DetMath.Sqrt(momentum * iy * _centreScale);
            double rise = height < _jetThickness ? height * _inverseJet : 1;
            double share = _fade[k] < _fade[m] ? _fade[k] : _fade[m];
            return centre * DetMath.Exp(exponent) * rise * share;
        }

        // A straight vortex from a to b of circulation g (right-handed about a → b) at p, with a Scully core: Biot–Savart's
        // segment law, |r₁ × r₂|² + r_c²·|r₀|² below (published, Leishman 2006 §10).
        Double3 Segment(Double3 p, Double3 a, Double3 b, double g)
        {
            Double3 r0 = b - a, r1 = p - a, r2 = p - b;
            double n1 = r1.Length(), n2 = r2.Length();
            if (!(n1 > 0) || !(n2 > 0)) return default;
            Double3 cross = Double3.Cross(r1, r2);
            double t = Double3.Dot(r1, r0) / n1 - Double3.Dot(r2, r0) / n2;
            double den = Double3.Dot(cross, cross) + _vortexCore * _vortexCore * Double3.Dot(r0, r0);
            return cross * (g * t / (4 * Pi * den));
        }
    }
}
