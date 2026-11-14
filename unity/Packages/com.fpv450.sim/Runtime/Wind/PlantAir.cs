using Fpv450.Sim.Flight;

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
        public void Wind(in PlantMechanics m, in Double3 basePoint, double ground, out PlantBend bend) =>
            Respond(m, basePoint, ground, default, default, default, out bend);

        /// <summary>
        /// A plant's bend in the wind and the props' wash at the frame. A stand the wash grid integrates (band ≥ 0, its base on the
        /// grid's ground) takes the grid's bend, its motion through the wash in full, over the wind's mean lean, and the wind's
        /// gusts linearised about that whole lean in the wind and the wash at its load point (so the wash's own damping holds
        /// them). Any other plant (a twig in a crown, a stand outside the window, band −1) takes the wash at its load point as a
        /// steady air on its mean (quasi-static: its mode is fast beside the wash's changes at a creep, design.md §4.5). Without a
        /// wash (null), Wind's.
        /// </summary>
        public void Sample(in PlantMechanics m, int band, PlantWash wash, in Double3 basePoint, double ground, out PlantBend bend)
        {
            if (wash == null || wash.LastTick < 0)
            {
                Respond(m, basePoint, ground, default, default, default, out bend);
                return;
            }
            double last = wash.LastTick * FlightCore.Dt, fraction = (_t - (last - wash.Tick)) / wash.Tick;
            fraction = fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
            if (band >= 0 && wash.Bend(band, basePoint.X, basePoint.Z, fraction, out PlantBend grid, out Double3 washAir))
            {
                Respond(m, basePoint, ground, default, washAir, grid, out bend);
                return;
            }
            var load = new Double3(basePoint.X, basePoint.Y + m.LoadPoint, basePoint.Z);
            Double3 steady = wash.Rotors.At(load, ground, out _);
            Respond(m, basePoint, ground, steady, default, default, out bend);
        }

        /// <summary>The field's air at a point over its ground at the frame, world m/s, as the plants read it (its mean and every
        /// mode, unfiltered): the flight's field (WindField.At) but for a cut's cavity and a step's lip (design.md §3.4).</summary>
        public Double3 Air(in Double3 point, double ground) => AirAt(point, ground, _mean, _drift, _t);

        /// <summary>The same at any time t, s (the frame left as it is).</summary>
        internal Double3 AirAt(in Double3 point, double ground, double t)
        {
            _wind.PlantFrame(t, out Double3 mean, out Double3 drift);
            return AirAt(point, ground, mean, drift, t);
        }

        Double3 AirAt(in Double3 point, double ground, in Double3 mean, in Double3 drift, double t)
        {
            _wind.PlantParts(point, ground, t, mean, out PlantParts parts);
            var sum = new Sum();
            _wind.PlantModes(point, parts, drift, mean, ref sum);
            return parts.Mean + sum.Air;
        }

        /// <summary>
        /// The bend from the wind's mean lean in the wind and a steady air (meanAdded, world m/s at the load point), plus a lean
        /// already made (made: rad and rad/s, world X and Z: the wash grid's), with the gusts' response linearised about the whole
        /// lean in the wind, the steady air and a further air (linearAdded: the wash at the load point).
        /// </summary>
        internal void Respond(in PlantMechanics m, in Double3 basePoint, double ground, in Double3 meanAdded, in Double3 linearAdded,
            in PlantBend made, out PlantBend bend)
        {
            var load = new Double3(basePoint.X, basePoint.Y + m.LoadPoint, basePoint.Z);
            _wind.PlantParts(load, ground, _t, _mean, out PlantParts parts);
            Double3 mean = parts.Mean + meanAdded;
            double horizontal = DetMath.Sqrt(mean.X * mean.X + mean.Z * mean.Z), mx = 1, mz = 0;
            if (horizontal > 0)
            {
                mx = mean.X / horizontal;
                mz = mean.Z / horizontal;
            }

            // The fluctuation's covariance at the load point, from the modes.
            var spread = new Spread();
            _wind.PlantModes(load, parts, _drift, _mean, ref spread);

            // The mean lean in the plane of the mean air.
            var meanMoment = new Expected(m, horizontal, mean.Y, spread.Along(mx, mz), spread.YY, spread.Covariance(mx, mz));
            double lean = PlantLaw.Settle(ref meanMoment, 0);

            // The whole lean, and the air the gusts are linearised in.
            double bx = lean * mx + made.X, bz = lean * mz + made.Z, phi = DetMath.Sqrt(bx * bx + bz * bz), ex = mx, ez = mz;
            if (phi > 1e-9)
            {
                ex = bx / phi;
                ez = bz / phi;
            }
            if (phi > PlantLaw.HalfPi) phi = PlantLaw.HalfPi;
            Double3 air = mean + linearAdded;
            var moment = new Expected(m, air.X * ex + air.Z * ez, air.Y, spread.Along(ex, ez), spread.YY, spread.Covariance(ex, ez));
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
            bend.RateX = made.RateX + response.RateAlong * ex - response.RateAcross * ez;
            bend.RateZ = made.RateZ + response.RateAlong * ez + response.RateAcross * ex;
            double total = DetMath.Sqrt(bend.X * bend.X + bend.Z * bend.Z);
            if (total > PlantLaw.HalfPi)
            {
                // No plant bends through the ground.
                bend.X *= PlantLaw.HalfPi / total;
                bend.Z *= PlantLaw.HalfPi / total;
            }
        }

        // The modes' covariance (each mode a·cos θ: aᵢaⱼ/2), horizontal and vertical.
        struct Spread : IModeSink
        {
            public double XX, XZ, ZZ, YY, XY, ZY;

            public void Mode(in Double3 amplitude, double phase, double rate)
            {
                double x = amplitude.X, y = amplitude.Y, z = amplitude.Z;
                XX += 0.5 * x * x;
                XZ += 0.5 * x * z;
                ZZ += 0.5 * z * z;
                YY += 0.5 * y * y;
                XY += 0.5 * x * y;
                ZY += 0.5 * z * y;
            }

            // The variance along a horizontal way, and its covariance with the vertical.
            public readonly double Along(double ex, double ez) => ex * ex * XX + 2 * ex * ez * XZ + ez * ez * ZZ;
            public readonly double Covariance(double ex, double ez) => ex * XY + ez * ZY;
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
