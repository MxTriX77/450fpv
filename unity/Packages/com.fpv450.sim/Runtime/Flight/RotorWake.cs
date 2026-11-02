using System;
using System.Collections.Generic;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The rotors in each other's wake (p7b-airframe 1.3d, D-060): in forward flight each rotor's wake is swept back under
    /// the rotors behind it, whose discs then take its downwash, and the rotors beside and ahead its upwash. Per rotor inside
    /// the inflow seam (Propulsion.RotorInflow), as an axial inflow at the disc, as the wind at the hub is.
    /// - The wake: Coleman's uniform skewed vortex cylinder (published: Coleman, R. P., Feingold, A. M., Stempin, C. W., NACA
    ///   ARR L5E10, 1945; the normal component of its induced velocity off the disc and a second rotor's interference from it,
    ///   Castles, W., De Leeuw, J. H., NACA Report 1184, 1954): rings of the disc's radius parallel to it, uniform in depth,
    ///   their centres swept downstream along the wake's skew χ from the disc's axis, tan χ = μ/λ, μ the hub's in-plane airspeed
    ///   and λ = V_a + v_i its axial inflow with its own induced velocity (Glauert's, as RotorFlappingSpec's). The normal
    ///   velocity a ring induces in the disc's plane is the closed form of a circular vortex (complete elliptic integrals, by
    ///   the arithmetic-geometric mean), integrated over the wake's depth.
    ///   - Its checks (published or exact): over the disc it gives Coleman's v_i·(1 + tan(χ/2)·x/R) at the centre, and in the
    ///     plane outside an unskewed wake (a hover) exactly nothing (the semi-infinite cylinder's end plane).
    ///   - Behind the disc in its plane the downwash rises with the skew: 0.04 v_i one hub spacing behind at χ 20°, 0.35 at
    ///     60°, 1.0 at 80° (this frame's 0.32 m, the target disc's average); beside and ahead it is an upwash of 0.01–0.10 v_i.
    ///     The interference factor's growth with the skew, to 2 in a flat wake, is the form the flight-dynamics models use
    ///     (published: Chen, Yuan and Thomson, Progress in Aerospace Sciences 120, 2021, §4: k·v_i, k from the skew and the
    ///     point's place).
    /// - The rotors' interference: rotor j's added inflow is Σ_i k_ij·v_i over the other rotors, k_ij the target disc's average
    ///   of the source's wake at its skew and the target's place in the source's flow (its distance and its angle off the
    ///   source's downstream direction), weighted along the blade as the thrust takes an inflow (the chord, c·r per unit
    ///   radius, blade-element theory). A table per distance between hubs over tan(χ/2) and sin(φ/2), built once for a layout
    ///   (Table) and read bilinearly; held at its largest skew.
    ///   - The skews come from each source's own inflow before the interference (one pass; assumed). The wake keeps the disc's
    ///     skew for its whole depth: the real wake bends down as its inflow doubles (Heyson and Katzoff's curved wake), which
    ///     moves it away from the rotors behind; left out (assumed).
    ///   - Only the axial (normal) component enters, as the wind's axial part does: the in-plane part of the wake's velocity is
    ///     left out (assumed: the H-force and flapping take the hub's own in-plane air).
    ///   - Where the flow through the disc reverses (λ &lt; 0, a fast descent), the wake rises at the mirrored skew and the
    ///     disc's plane sees the same normal velocity (a ring's normal field is even about its plane).
    ///   - The wake is a cylinder only while the flow carries it away: below a transport velocity √(μ² + λ²) of 0.74 v_h it
    ///     piles up round the disc instead (published: Newman et al.'s onset of the vortex ring state, as the ring's design
    ///     takes it), so each source's wake fades out from v_h (assumed: a hover's own transport velocity, so the normal working
    ///     state, hover, climb and forward flight, always has the whole wake) to 0.74 v_h, where the vortex ring owns it.
    /// - What it gives (published orders, other frames): the rear rotors lose thrust in forward flight at 5–15 m/s, 11 % (Ye,
    ///   Wang and Lv, Aerospace 8(11) 349, 2021, CFD of an X quadcopter, negligible in a hover and at 25 m/s), 19 % (Misiorowski,
    ///   Gandhi and Oberai, AIAA Journal 57(12), 2019, DES at 10 m/s and 5° nose down, cross configuration, the loss at the
    ///   aft rotors' front from the forward rotors' downwash; the side rotors of the plus configuration gain 5.5–7.6 % from
    ///   upwash), at most 11 % in a tandem wind-tunnel test, which a Biot–Savart formula reproduced qualitatively (Nguyen, Liu
    ///   and Mori, Trans. JSASS 61(1), 2018).
    /// - The wake's own unsteadiness: its tip vortices and shear layer wander past the target disc, so each rotor's
    ///   interference carries a fluctuation, a share x of it, a first-order process with the corner of eddies of
    ///   FluctuationScale·R carried past at the rotor's in-plane airspeed, σ FluctuationSigma, bounded, its unit from the named
    ///   stream "wake.rotor" (counter-based on the step and the corner). Never on the pose. The tip vortices' passage at the
    ///   blade-pass frequency (2/rev, 290–350 Hz) is left out: it is vibration (hover-micro-motion.md §4, the blade pass).
    /// Basic operations and square roots only; the table is built once per layout and shared, a step allocates nothing.
    /// </summary>
    public sealed class RotorWakeSpec
    {
        const double Pi = 3.141592653589793;

        /// <summary>The prop's radius, m (drone.json's props).</summary>
        public double Radius = 0.127;

        /// <summary>The air's density, kg/m³ (drone.json's aero air_density_kg_m3, 1.225, published ISA).</summary>
        public double AirDensity = 1.225;

        /// <summary>The blade's stations x = r/R and its chord there, m (linear between): the target disc's weighting along the
        /// blade, c·x per unit radius (DroneSpec.BuildRotorWake: the sheet's chord, as the flapping's).</summary>
        public double[] Stations, Chords;

        /// <summary>The table's step in tan(χ/2) and its nodes from 0: 0.025 and 37, to tan(χ/2) 0.9, χ 84° (assumed: the
        /// numerics, 2.2–2.9° of skew a step).</summary>
        public double SkewStep = 0.025;
        public int SkewNodes = 37;

        /// <summary>The table's step in sin(φ/2), φ the target's angle off the source's downstream direction, from 0 to 1:
        /// 0.025 (assumed: the numerics, 2.9–4° of angle a step to 150°).</summary>
        public double OffsetStep = 0.025;

        /// <summary>The wake's transport velocity over v_h below which it no longer forms (Newman et al.'s onset, published: 0.74)
        /// and at and above which it is whole (assumed: 1, a hover's).</summary>
        public double TransportOnset = 0.74, TransportWhole = 1;

        /// <summary>The fluctuation's σ, a share of the interference: 0.05 (assumed: the tip vortices pass 0.04–0.12 m under the
        /// target disc's leading half at the cruise and wander by 1–2 % of R in a model rotor's young wake (Leishman, Principles
        /// of Helicopter Aerodynamics, recalled), 2–6 % of that distance; no measured fluctuation of a rotor in another's wake
        /// below the blade pass was found).</summary>
        public double FluctuationSigma = 0.05;

        /// <summary>Its bound: 0.15 (assumed, 3σ).</summary>
        public double FluctuationBound = 0.15;

        /// <summary>The fluctuation's eddies over R: 1 (assumed: the wake's tip vortices and shear layer at about the disc's
        /// radius from the target, so its corner is V_∥/(2πR), 24 Hz at the cruise).</summary>
        public double FluctuationScale = 1;

        public RotorWakeSpec Clone()
        {
            var c = (RotorWakeSpec)MemberwiseClone();
            c.Stations = (double[])Stations?.Clone();
            c.Chords = (double[])Chords?.Clone();
            return c;
        }

        /// <summary>The table's nodes in sin(φ/2), from 0 to 1.</summary>
        public int OffsetNodes => (int)(1 / OffsetStep + 0.5) + 1;

        /// <summary>Glauert's induced velocity at a thrust (N) with the hub's in-plane airspeed and axial inflow, m/s: six relaxed
        /// passes from v_h, the root held at ½v_h or more (as RotorFlappingSpec.InducedVelocity).</summary>
        public double InducedVelocity(double inPlane, double axial, double thrust)
        {
            if (!(thrust > 0)) return 0;
            double vh2 = thrust / (2 * AirDensity * Pi * Radius * Radius), vh = DetMath.Sqrt(vh2), v = vh;
            for (int n = 0; n < 6; n++)
            {
                double u = axial + v;
                double d = DetMath.Max(DetMath.Sqrt(inPlane * inPlane + u * u), 0.5 * vh);
                v = 0.5 * (v + vh2 / d);
            }
            return v;
        }

        /// <summary>The share of a source's wake that forms at a thrust (N) with its transport velocity √(μ² + λ²) (m/s): 0 below
        /// TransportOnset·v_h, 1 at and above TransportWhole·v_h, linear between; 0 with no thrust.</summary>
        public double Formed(double thrust, double transport)
        {
            if (!(thrust > 0)) return 0;
            double vh = DetMath.Sqrt(thrust / (2 * AirDensity * Pi * Radius * Radius));
            double x = (transport / vh - TransportOnset) / (TransportWhole - TransportOnset);
            return x > 1 ? 1 : x > 0 ? x : 0;
        }

        /// <summary>The wake's tan(χ/2) for an in-plane airspeed μ and an axial inflow λ through the disc (V_a + v_i), m/s:
        /// μ/(√(μ² + λ²) + |λ|); 0 with no flow.</summary>
        public static double HalfSkew(double inPlane, double through)
        {
            double l = DetMath.Abs(through), root = DetMath.Sqrt(inPlane * inPlane + l * l) + l;
            return root > 0 ? inPlane / root : 0;
        }

        // The wake's depth nodes over R, z_n = DepthStart·DepthRatio^n to DepthEnd, the integral a trapezoid in ln z with the
        // first piece z₀·f(z₀) (the integrand is finite there off the source's rim). 1.08 a node: 0.1 % of the fine sum.
        const double DepthStart = 1e-4, DepthRatio = 1.08, DepthLogStep = 0.07696104113612832, DepthEnd = 400;
        static readonly double[] Depth = Depths();

        static double[] Depths()
        {
            var z = new List<double>();
            for (double h = DepthStart; h < DepthEnd; h *= DepthRatio) z.Add(h);
            return z.ToArray();
        }

        /// <summary>
        /// The normal velocity the wake induces at a point of the disc's plane, (s, d) over R (s downstream of the hub, d to
        /// its side), over its value at the disc's centre (v_i), for a skew of tan(χ/2) = halfSkew below 1: a positive value
        /// is a downwash.
        /// </summary>
        public static double PointFactor(double halfSkew, double s, double d)
        {
            double t = Tangent(halfSkew);
            return Integral(t, s, d) / Integral(t, 0, 0);
        }

        // tan χ of tan(χ/2).
        static double Tangent(double u) => 2 * u / (1 - u * u);

        // ∫ the rings' normal velocity at (s, d) over the depth, in a ring's units (radius 1, circulation 2π per unit depth).
        static double Integral(double tanChi, double s, double d)
        {
            double sum = 0, prev = 0, d2 = d * d;
            for (int n = 0; n < Depth.Length; n++)
            {
                double z = Depth[n], x = s - z * tanChi;
                double g = Ring(DetMath.Sqrt(x * x + d2), z) * z;
                if (n == 0) sum = g;
                else sum += 0.5 * (g + prev) * DepthLogStep;
                prev = g;
            }
            return sum;
        }

        // A unit ring's (radius 1, circulation 2π) velocity along its axis at ρ from it and z off its plane: (K(m) + (1 − ρ² −
        // z²)/((1 − ρ)² + z²)·E(m))/√((1 + ρ)² + z²), m = 4ρ/((1 + ρ)² + z²) (the closed form of a circular vortex, published).
        static double Ring(double rho, double z)
        {
            double z2 = z * z, q = (1 + rho) * (1 + rho) + z2, m = 4 * rho / q;
            if (m > 1 - 1e-15) m = 1 - 1e-15;
            Elliptic(m, out double k, out double e);
            return (k + (1 - rho * rho - z2) / ((1 - rho) * (1 - rho) + z2) * e) / DetMath.Sqrt(q);
        }

        // The complete elliptic integrals K(m) and E(m) by the arithmetic-geometric mean: K = π/(2·a_N), E = K·(1 − Σ 2^(n−1)·c_n²)
        // with c₀² = m (published, Abramowitz and Stegun 17.6).
        static void Elliptic(double m, out double k, out double e)
        {
            double a = 1, b = DetMath.Sqrt(1 - m), sum = 0.5 * m, p = 0.5;
            for (int n = 0; n < 12; n++)
            {
                double c = 0.5 * (a - b);
                double next = 0.5 * (a + b);
                b = DetMath.Sqrt(a * b);
                a = next;
                p *= 2;
                sum += p * c * c;
                if (!(c > 1e-16 * a)) break;
            }
            k = Pi / (2 * a);
            e = k * (1 - sum);
        }

        // The target disc's points: Gauss–Legendre in x over the blade, uniform in azimuth, weighted c(x)·x (published nodes).
        static readonly double[] GaussNodes = { -0.8611363115940526, -0.3399810435848563, 0.3399810435848563, 0.8611363115940526 };
        static readonly double[] GaussWeights = { 0.3478548451374538, 0.6521451548625461, 0.6521451548625461, 0.3478548451374538 };
        const int Azimuths = 12;

        /// <summary>
        /// The target disc's average of the source's wake at a skew of tan(χ/2) = halfSkew (below 1), the target's hub at
        /// distance over R from the source's and at φ off its downstream direction (cos φ, sin φ ≥ 0), weighted along the blade
        /// by c·x (Stations, Chords). 0 for an unskewed wake (exactly: the semi-infinite cylinder's end plane).
        /// </summary>
        public double DiscFactor(double halfSkew, double distance, double cos, double sin)
        {
            if (!(halfSkew > 0)) return 0;
            double t = Tangent(halfSkew), centre = Integral(t, 0, 0);
            double x0 = Stations[0], x1 = Stations[Stations.Length - 1], half = 0.5 * (x1 - x0), mid = 0.5 * (x1 + x0);
            double sc = distance * cos, dc = distance * sin, sum = 0, weights = 0;
            for (int g = 0; g < GaussNodes.Length; g++)
            {
                double x = mid + half * GaussNodes[g], w = GaussWeights[g] * half * Chord(x) * x;
                for (int a = 0; a < Azimuths; a++)
                {
                    DetMath.SinCos((a + 0.5) * (2 * Pi / Azimuths), out double sa, out double ca);
                    sum += w * Integral(t, sc + x * ca, dc + x * sa);
                    weights += w;
                }
            }
            return sum / (weights * centre);
        }

        double Chord(double x)
        {
            int n = Stations.Length;
            if (!(x > Stations[0])) return Chords[0];
            if (x >= Stations[n - 1]) return Chords[n - 1];
            int k = 0;
            while (x >= Stations[k + 1]) k++;
            return Chords[k] + (x - Stations[k]) * (Chords[k + 1] - Chords[k]) / (Stations[k + 1] - Stations[k]);
        }

        static readonly List<RotorWakeTable> Built = new List<RotorWakeTable>();
        static readonly object BuildLock = new object();

        /// <summary>The interference table of this spec for four hubs (body m; their positions in the discs' plane, x and z):
        /// built once for a layout and spec and shared (it is a function of them alone).</summary>
        public RotorWakeTable Table(Double3[] hubs)
        {
            if (hubs == null || hubs.Length != 4) throw new ArgumentException("four hubs", nameof(hubs));
            lock (BuildLock)
            {
                foreach (RotorWakeTable t in Built)
                    if (t.IsFor(this, hubs))
                        return t;
                var built = new RotorWakeTable(this, hubs);
                Built.Add(built);
                return built;
            }
        }
    }

    /// <summary>
    /// The interference factors of a layout (RotorWakeSpec.Table): for each ordered pair of rotors, the target's added inflow
    /// over the source's induced velocity at the source's skew and the target's place in its flow, from a table per distance
    /// between hubs over tan(χ/2) and sin(φ/2), read bilinearly.
    /// </summary>
    public sealed class RotorWakeTable
    {
        readonly RotorWakeSpec _spec;
        readonly Double3[] _hubs = new Double3[4];
        readonly int[] _pairTable = new int[16];
        readonly double[] _pairX = new double[16], _pairZ = new double[16], _pairLength = new double[16];
        readonly double[][] _factor;
        readonly int _skewNodes, _offsetNodes;

        internal RotorWakeTable(RotorWakeSpec spec, Double3[] hubs)
        {
            _spec = spec.Clone();
            _skewNodes = spec.SkewNodes;
            _offsetNodes = spec.OffsetNodes;
            var distances = new List<double>();
            for (int i = 0; i < 4; i++) _hubs[i] = hubs[i];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    int p = i * 4 + j;
                    _pairTable[p] = -1;
                    if (i == j) continue;
                    double x = hubs[j].X - hubs[i].X, z = hubs[j].Z - hubs[i].Z, length = DetMath.Sqrt(x * x + z * z);
                    _pairX[p] = x;
                    _pairZ[p] = z;
                    _pairLength[p] = length;
                    double over = length / spec.Radius;
                    int at = -1;
                    for (int k = 0; k < distances.Count; k++)
                        if (DetMath.Abs(distances[k] - over) < 1e-9) at = k;
                    if (at < 0)
                    {
                        at = distances.Count;
                        distances.Add(over);
                    }
                    _pairTable[p] = at;
                }
            _factor = new double[distances.Count][];
            for (int k = 0; k < distances.Count; k++)
            {
                var f = new double[_skewNodes * _offsetNodes];
                for (int u = 1; u < _skewNodes; u++)
                    for (int o = 0; o < _offsetNodes; o++)
                    {
                        // φ = 2·asin(t): cos φ = 1 − 2t², sin φ = 2t·√(1 − t²).
                        double t = o * spec.OffsetStep;
                        if (t > 1) t = 1;
                        f[u * _offsetNodes + o] = spec.DiscFactor(u * spec.SkewStep, distances[k], 1 - 2 * t * t, 2 * t * DetMath.Sqrt(1 - t * t));
                    }
                _factor[k] = f;
            }
            Distances = distances.ToArray();
        }

        /// <summary>The distinct distances between hubs over R, in the order the tables hold them.</summary>
        public double[] Distances { get; }

        internal bool IsFor(RotorWakeSpec spec, Double3[] hubs)
        {
            for (int i = 0; i < 4; i++)
                if (hubs[i].X != _hubs[i].X || hubs[i].Z != _hubs[i].Z) return false;
            return spec.Radius == _spec.Radius && spec.SkewStep == _spec.SkewStep && spec.SkewNodes == _spec.SkewNodes
                && spec.OffsetStep == _spec.OffsetStep && Same(spec.Stations, _spec.Stations) && Same(spec.Chords, _spec.Chords);
        }

        static bool Same(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Target's added inflow over source's induced velocity, with the source's wake at tan(χ/2) = halfSkew (held at the
        /// table's last node) going downstream along (dx, dz), a unit body direction in the discs' plane. 0 for the same rotor.
        /// </summary>
        public double Factor(int source, int target, double halfSkew, double dx, double dz)
        {
            int p = source * 4 + target, table = _pairTable[p];
            if (table < 0) return 0;
            double cos = (_pairX[p] * dx + _pairZ[p] * dz) / _pairLength[p];
            double t = 0.5 * (1 - cos);
            t = t > 0 ? DetMath.Sqrt(t) : 0;
            double us = halfSkew / _spec.SkewStep, os = t / _spec.OffsetStep;
            if (!(us > 0)) return 0;
            int u = (int)us, o = (int)os;
            if (u >= _skewNodes - 1) { u = _skewNodes - 2; us = _skewNodes - 1; }
            if (o >= _offsetNodes - 1) { o = _offsetNodes - 2; os = _offsetNodes - 1; }
            double fu = us - u, fo = os - o;
            double[] f = _factor[table];
            int at = u * _offsetNodes + o;
            double low = f[at] + fo * (f[at + 1] - f[at]), high = f[at + _offsetNodes] + fo * (f[at + _offsetNodes + 1] - f[at + _offsetNodes]);
            return low + fu * (high - low);
        }
    }
}
