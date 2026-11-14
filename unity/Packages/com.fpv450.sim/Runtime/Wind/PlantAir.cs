namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// The plants in the flight's air, as the drawn side samples them each frame (w16-plants-in-the-air design.md §3–§4, task 1.2;
    /// AST-09, VIS-05, PHY-13). Engine-free, deterministic (DetMath only), allocation-free after its constructor; it runs on the
    /// drawn side's frame or a helper, never in the flight's step, and holds nothing the drawn side must keep in step: a sample is
    /// a pure function of the frame's time, the place and the plant.
    /// - **The wind's part, in closed form.** The field is frozen modes drifting with its mean (WindField, Taylor), so at a fixed
    ///   plant each mode is a sinusoid at its encounter frequency Ω = κ·V. The plant's link (PlantLaw) is linearised about its
    ///   mean lean and its response to each mode is the steady response of I·ẍ + c·ẋ + K·x = F·cos θ, exact for the linear link
    ///   at every time (the gusts reach each plant when the field brings them there, from the direction they blow).
    /// - **The mean lean and the gains, statistically linearised** (Caughey 1963; Atalik and Utku 1976; Borgman 1967 for
    ///   Morison's drag): near the ground the turbulence is as strong as the mean (σ/U up to 0.9 at a grass culm's load point), so
    ///   the mean moment is E[c|v|v] and the gains E[∂(c|v|v)/∂v] over the normal air's Gaussian spread along the mean, not the
    ///   mean's own (12-point Gauss–Hermite). The spread is the field's own modes' at the point.
    /// - The bend is from the plant's drawn rest (upright: its weight's sag and its natural lean are the mesh's own).
    /// </summary>
    public sealed class PlantAir
    {
        readonly WindField _wind;
        double _t;
        Double3 _mean, _drift;

        /// <summary>The plants in a flight's wind field (the flight's own instance: the plants read its immutable layers and,
        /// for a map's wakes, its wake grid).</summary>
        public PlantAir(WindField wind)
        {
            _wind = wind ?? throw new System.ArgumentNullException(nameof(wind));
        }

        /// <summary>The frame's time, s from the flight's start.</summary>
        public double Time => _t;

        /// <summary>Fixes the frame: the time (s from the flight's start, any fraction of a step), the field's mean at the
        /// reference height and its drift then.</summary>
        public void Begin(double time)
        {
            _t = time;
            _wind.PlantFrame(time, out _mean, out _drift);
        }

        /// <summary>
        /// A plant's bend in the wind at the frame: its base (world m) over the ground under it (m); the wind taken at its load
        /// point on its base's vertical (PlantMechanics.LoadPoint).
        /// </summary>
        public void Wind(in PlantMechanics m, in Double3 basePoint, double ground, out PlantBend bend) => Respond(m, basePoint, ground, default, out bend);

        /// <summary>The field's air at a point over its ground at the frame, world m/s, as the plants read it (its mean and every
        /// mode, unfiltered): the flight's field (WindField.At) but for a cut's cavity and a step's lip (design.md §3.4).</summary>
        public Double3 Air(in Double3 point, double ground)
        {
            _wind.PlantParts(point, ground, _t, _mean, out PlantParts parts);
            var sum = new Sum();
            _wind.PlantModes(point, parts, _drift, _mean, ref sum);
            return parts.Mean + sum.Air;
        }

        /// <summary>The same with a steady air added at the load point (world m/s: the props' wash, quasi-static).</summary>
        internal void Respond(in PlantMechanics m, in Double3 basePoint, double ground, in Double3 added, out PlantBend bend)
        {
            var load = new Double3(basePoint.X, basePoint.Y + m.LoadPoint, basePoint.Z);
            _wind.PlantParts(load, ground, _t, _mean, out PlantParts parts);
            Double3 mean = parts.Mean + added;
            double horizontal = DetMath.Sqrt(mean.X * mean.X + mean.Z * mean.Z), ex = 1, ez = 0;
            if (horizontal > 0)
            {
                ex = mean.X / horizontal;
                ez = mean.Z / horizontal;
            }

            // The fluctuation's spread along the mean and up, from the modes.
            var spread = new Spread { Ex = ex, Ez = ez };
            _wind.PlantModes(load, parts, _drift, _mean, ref spread);

            // The mean lean in the plane of the mean wind.
            var moment = new Expected(m, horizontal, mean.Y, spread.Along, spread.Up, spread.Cross);
            double phi = PlantLaw.Settle(ref moment, 0);

            // The linear link about it.
            DetMath.SinCos(phi, out double s, out double c);
            double k = m.Stiffness, ratio = PlantLaw.SinOver(phi, s);
            const double h = 1e-4;
            double ka = k - m.Weight * c - (moment.Moment(phi + h) - moment.Moment(phi - h)) / (2 * h), kp = k - m.Weight * ratio;
            if (ka < 0.01 * k) ka = 0.01 * k;
            if (kp < 0.01 * k) kp = 0.01 * k;
            moment.Gains(phi, out double slopeAlong, out double slopeAcross);
            double s0 = m.DragFrom, s1 = m.DragTo, a2 = 0.5 * (s1 * s1 - s0 * s0), a3 = (s1 * s1 * s1 - s0 * s0 * s0) / 3;
            var response = new Response
            {
                Nx = c * ex, Ny = -s, Nz = c * ez, Px = -ez, Pz = ex,
                GainAlong = a2 * slopeAlong, GainAcross = a2 * ratio * slopeAcross, Inertia = m.Inertia,
                StiffAlong = ka, StiffAcross = kp, DampAlong = m.Damping + a3 * slopeAlong, DampAcross = m.Damping + a3 * ratio * ratio * slopeAcross,
            };
            _wind.PlantModes(load, parts, _drift, _mean, ref response);
            double a = phi + response.Along, p = response.Across;
            bend.X = a * ex - p * ez;
            bend.Z = a * ez + p * ex;
            bend.RateX = response.RateAlong * ex - response.RateAcross * ez;
            bend.RateZ = response.RateAlong * ez + response.RateAcross * ex;
        }

        // The modes' variance along the mean's horizontal way, up, and their covariance (each mode a·cos: a²/2).
        struct Spread : IModeSink
        {
            public double Ex, Ez, Along, Up, Cross;

            public void Mode(in Double3 amplitude, double phase, double rate)
            {
                double a = amplitude.X * Ex + amplitude.Z * Ez;
                Along += 0.5 * a * a;
                Up += 0.5 * amplitude.Y * amplitude.Y;
                Cross += 0.5 * a * amplitude.Y;
            }
        }

        // Each mode's steady response on the two axes: F·(P·cos θ − cΩ·sin θ)/(P² + c²Ω²), P = K − IΩ², and its rate
        // F·Ω·(P·sin θ + cΩ·cos θ)/(P² + c²Ω²) (θ falls at Ω).
        struct Response : IModeSink
        {
            public double Nx, Ny, Nz, Px, Pz, GainAlong, GainAcross, Inertia, StiffAlong, StiffAcross, DampAlong, DampAcross;
            public double Along, RateAlong, Across, RateAcross;

            public void Mode(in Double3 amplitude, double phase, double rate)
            {
                double fa = GainAlong * (amplitude.X * Nx + amplitude.Y * Ny + amplitude.Z * Nz);
                double fp = GainAcross * (amplitude.X * Px + amplitude.Z * Pz);
                DetMath.SinCos(phase, out double sin, out double cos);
                double i2 = Inertia * rate * rate;
                double pa = StiffAlong - i2, ca = DampAlong * rate, na = pa * pa + ca * ca;
                double pp = StiffAcross - i2, cp = DampAcross * rate, np = pp * pp + cp * cp;
                if (na > 0)
                {
                    double da = fa / na;
                    Along += da * (pa * cos - ca * sin);
                    RateAlong += da * rate * (pa * sin + ca * cos);
                }
                if (np > 0)
                {
                    double dp = fp / np;
                    Across += dp * (pp * cos - cp * sin);
                    RateAcross += dp * rate * (pp * sin + cp * cos);
                }
            }
        }

        // The modes' plain sum (Air's).
        struct Sum : IModeSink
        {
            public Double3 Air;

            public void Mode(in Double3 amplitude, double phase, double rate) => Air = Air + amplitude * DetMath.Cos(phase);
        }

        // The mean moment along the lean's plane over the normal air's Gaussian spread, and the gains (Gauss–Hermite).
        struct Expected : PlantLaw.IMoment
        {
            readonly PlantMechanics _m;
            readonly double _u, _w, _along, _up, _cross, _a2;

            public Expected(in PlantMechanics m, double horizontal, double vertical, double along, double up, double cross)
            {
                _m = m;
                _u = horizontal;
                _w = vertical;
                _along = along;
                _up = up;
                _cross = cross;
                _a2 = 0.5 * (m.DragTo * m.DragTo - m.DragFrom * m.DragFrom);
            }

            // The normal air's mean and spread at a lean.
            void Normal(double phi, out double mu, out double sigma)
            {
                DetMath.SinCos(phi, out double s, out double c);
                mu = _u * c - _w * s;
                double var = _along * c * c + _up * s * s - 2 * _cross * c * s;
                sigma = var > 0 ? DetMath.Sqrt(var) : 0;
            }

            public double Moment(double phi)
            {
                Normal(phi, out double mu, out double sigma);
                Expect(_m, mu, sigma, out double g, out _, out _);
                return _a2 * g;
            }

            public double Net(double phi)
            {
                DetMath.SinCos(phi, out double s, out _);
                return Moment(phi) + _m.Weight * s - _m.Stiffness * phi;
            }

            public void Gains(double phi, out double along, out double across)
            {
                Normal(phi, out double mu, out double sigma);
                Expect(_m, mu, sigma, out _, out along, out across);
            }
        }

        /// <summary>E[c|v|v], E[(c + c')|v|] and E[c|v|] for v normal of mean mu and spread sigma (12-point Gauss–Hermite;
        /// the point itself when sigma is 0).</summary>
        public static void Expect(in PlantMechanics m, double mu, double sigma, out double drag, out double along, out double across)
        {
            drag = along = across = 0;
            if (!(sigma > 0))
            {
                Term(m, mu, 1, ref drag, ref along, ref across);
                return;
            }
            if (!(m.FoliageDrag > 0))
            {
                // A bare stem's quadratic drag, in closed form (Borgman 1967, published): E[|v|v] = (μ² + σ²)·erf(μ/√2σ) +
                // μσ·√(2/π)·e^(−μ²/2σ²), E|v| = μ·erf(μ/√2σ) + σ·√(2/π)·e^(−μ²/2σ²).
                double z = mu / (PlantLaw.Sqrt2 * sigma), e = DetMath.Exp(-z * z), erf = Erf(z), root = 0.7978845608028654 * sigma * e;
                double absolute = mu * erf + root;
                drag = m.StemDrag * ((mu * mu + sigma * sigma) * erf + mu * root);
                along = 2 * m.StemDrag * absolute;
                across = m.StemDrag * absolute;
                return;
            }
            double spread = PlantLaw.Sqrt2 * sigma;
            for (int i = 0; i < PlantLaw.HermiteX.Length; i++)
            {
                double w = PlantLaw.HermiteW[i] / PlantLaw.SqrtPi, x = spread * PlantLaw.HermiteX[i];
                Term(m, mu + x, w, ref drag, ref along, ref across);
                Term(m, mu - x, w, ref drag, ref along, ref across);
            }
        }

        // erf by Abramowitz and Stegun 7.1.26 (|error| ≤ 1.5e-7, published), odd.
        static double Erf(double x)
        {
            double a = DetMath.Abs(x), t = 1 / (1 + 0.3275911 * a);
            double poly = t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))));
            double r = 1 - poly * DetMath.Exp(-a * a);
            return x < 0 ? -r : r;
        }

        static void Term(in PlantMechanics m, double v, double weight, ref double drag, ref double along, ref double across)
        {
            double a = DetMath.Abs(v);
            PlantLaw.Coefficient(m, a, out double c, out double slope);
            drag += weight * c * a * v;
            along += weight * (c + slope) * a;
            across += weight * c * a;
        }
    }
}
