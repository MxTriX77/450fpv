using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// The flow inside and just above a wooded strip (p7c-wind 1.5, belt-geometry-p.md; WindSetup.Canopy), the field's
    /// canopy layer: clip P crossed a wide strip of scrub with scattered tall trees at 0.45–1.0 H, the wind along it,
    /// inside its canopy layer, not in a wake. Built once per flight from the strip, the level's preset and the flight's
    /// draws; WindField samples it at each point (each rotor's hub, the body, the coil) like the rest of the field.
    /// Labels: published (cited), derived (computed from those), assumed (WindPreset's named canopy parameters).
    /// - **The canopy's roughness** (published, Raupach 1994, Boundary-Layer Meteorol. 71, 211–216; the form read in
    ///   CABLE's implementation): with Λ = 2λ, d/h = 1 − (1 − e^(−√(c_d1·Λ)))/√(c_d1·Λ), c_d1 = 7.5;
    ///   β = u*/U(h) = min(√(C_S + C_R·λ), 0.3), C_S = 0.003, C_R = 0.3; the roughness sublayer to z_w, z_w − d =
    ///   c_w·(h − d), c_w = 2, whose influence is ψ_h = ln c_w − 1 + 1/c_w = 0.193; z0/h = (1 − d/h)·e^(−κ/β + ψ_h).
    /// - **The mean profile** along the open field's mean wind, every piece continuous with the next:
    ///   - inside (z &lt; h): U(h)·e^(α(z/h − 1)) (published form: Inoue 1963, Cionco 1965), α = β·h/(κ·c_w·(h − d))
    ///     (derived: the mixing length continuous at h, β·h/α inside against κ·(z_w − d) in the sublayer);
    ///   - the roughness sublayer (h to z_w): linear at the log law's gradient at z_w, u*/(κ(z_w − d)) (derived: the
    ///     profile Raupach's ψ_h integrates);
    ///   - above it, the log law (u*/κ)·ln((z − d)/z0) up to the blending height z_b, where it meets the open field's
    ///     mean, which sets u* (derived); above z_b the open field's.
    /// - **The turbulence:** at and above h the canopy-top σ_i = (σ_i/u*)·u* (CanopySigmaU, V, W, recalled), below h
    ///   each σ falls as (U(z)/U(h))^CanopyDecay (assumed). The open field's modes stay, scaled by the same fall
    ///   inside; the canopy's eddies carry the rest, √(σ_i² − σ_open,i²) per component, u along the flight's drawn
    ///   bearing, v across it, w vertical (the anisotropy's axes fixed per flight, assumed: the meander moves the mean,
    ///   not them). The eddies are CanopyModes random Fourier modes of von Kármán's spectrum (as the open field's,
    ///   WindField) at the isotropic scales CanopyScaleU·h and CanopyScaleW·h, from stream wind.canopy's draws 5·m to
    ///   5·m + 4 (counter-based), each mode across its wave vector (vorticity), frozen and advected at
    ///   CanopyConvection·U(h) along the mean wind (published). Full up to z_w, gone at z_b, linear between (assumed).
    /// - **The plan:** the layer grows from 0 at the strip's edge to full CanopyEdge·h inside it (a smooth step,
    ///   assumed) and is 0 outside. The flow is taken as developed everywhere inside: P's wind ran along the strip's
    ///   long axis. A strip the wind crosses adjusts over about 3·L_c (Belcher, Jerram and Hunt 2003, recalled), which
    ///   is not modelled.
    /// Randomness enters only the air's velocity through the modes' draws, never the pose. Zero allocations after the
    /// constructor.
    /// </summary>
    public sealed class CanopyFlow
    {
        /// <summary>Raupach (1994)'s constants (published).</summary>
        public const double Cd1 = 7.5, Cs = 0.003, Cr = 0.3, BetaMax = 0.3, Cw = 2;

        /// <summary>Von Kármán's constant (published).</summary>
        public const double Karman = 0.4;

        public readonly CanopyStrip Strip;

        /// <summary>h, d, z0, z_w and z_b, m (derived).</summary>
        public readonly double Height, Displacement, Roughness, SublayerTop, BlendingHeight;

        /// <summary>β = u*/U(h) and the in-canopy attenuation α (derived).</summary>
        public readonly double Beta, Attenuation;

        /// <summary>u* over the strip and U(h), m/s (derived from the flight's drawn strength).</summary>
        public readonly double FrictionVelocity, TopSpeed;

        /// <summary>The eddies' σ at the canopy's top per component (u along the drawn bearing, v across it, w), m/s:
        /// the canopy's own less the open field's (derived).</summary>
        public readonly double ExcessU, ExcessV, ExcessW;

        readonly double _ax, _az, _halfL, _halfW, _edge, _gradient, _decay, _convection;
        readonly Double3[] _wave, _amp;
        readonly double[] _phase, _offset;

        /// <summary>
        /// The strip's flow for a flight: the preset's canopy parameters, the drawn bearing (degrees) and strength (m/s at
        /// the reference height), the open field's mean at the blending height over that strength, its σ_u and σ_w (m/s),
        /// and the flight's random (stream wind.canopy).
        /// </summary>
        internal CanopyFlow(CanopyStrip strip, in WindPreset p, double fromDeg, double meanSpeed, Func<double, double> openProfile,
            double sigmaH, double sigmaV, FlightRandom random)
        {
            Strip = strip.Checked();
            double h = strip.Height, lambda = strip.FrontalAreaIndex;
            double x = DetMath.Sqrt(Cd1 * 2 * lambda);
            double dh = 1 - (1 - DetMath.Exp(-x)) / x;
            Beta = DetMath.Min(DetMath.Sqrt(Cs + Cr * lambda), BetaMax);
            double psi = DetMath.Log(Cw) - 1 + 1 / Cw;
            Height = h;
            Displacement = dh * h;
            Roughness = (1 - dh) * DetMath.Exp(-Karman / Beta + psi) * h;
            SublayerTop = Displacement + Cw * (h - Displacement);
            BlendingHeight = p.BlendingHeights * h;
            if (!(BlendingHeight > SublayerTop) || !(BlendingHeight <= p.ProfileTop))
                throw new ArgumentException("the canopy's blending height is above the profile's top or under its roughness sublayer");
            Attenuation = Beta * h / (Karman * Cw * (h - Displacement));
            FrictionVelocity = Karman * meanSpeed * openProfile(BlendingHeight) / DetMath.Log((BlendingHeight - Displacement) / Roughness);
            TopSpeed = FrictionVelocity / Beta;
            _gradient = FrictionVelocity / (Karman * (SublayerTop - Displacement));
            _decay = p.CanopyDecay;
            _convection = p.CanopyConvection * TopSpeed / meanSpeed;

            // The plan: the long axis's unit vector (east X, north Z) and the half sizes.
            DetMath.SinCos(strip.AxisDeg * 0.017453292519943295, out double sa, out double ca);
            _ax = sa;
            _az = ca;
            _halfL = strip.Length / 2;
            _halfW = strip.Width / 2;
            _edge = p.CanopyEdge * h;

            // The eddies' σ beyond the open field's.
            double Excess(double ratio, double open)
            {
                double s = ratio * FrictionVelocity;
                return s > open ? DetMath.Sqrt(s * s - open * open) : 0;
            }
            ExcessU = Excess(p.CanopySigmaU, sigmaH);
            ExcessV = Excess(p.CanopySigmaV, sigmaH);
            ExcessW = Excess(p.CanopySigmaW, sigmaV);

            // The eddies' modes in the drawn bearing's frame: along (the way the wind blows), across, up.
            Double3 along = WindField.Toward(fromDeg, 1), across = new Double3(-along.Z, 0, along.X);
            SeedStream s = random.Open("wind.canopy");
            int modes = p.CanopyModes;
            _wave = new Double3[modes];
            _amp = new Double3[modes];
            _phase = new double[modes];
            _offset = new double[modes];
            double lu = p.CanopyScaleU * h, lw = p.CanopyScaleW * h;
            double kMin = p.LongestFraction / (WindField.Knee * lu), kMax = 6.283185307179586 / p.ShortestWavelength;
            double ratio = DetMath.Log(kMax / kMin);
            for (int m = 0; m < modes; m++)
            {
                double ka = kMin * DetMath.Exp(ratio * m / modes), kb = kMin * DetMath.Exp(ratio * (m + 1) / modes);
                ulong i = (ulong)m * 5;
                double k = ka + (kb - ka) * WindField.Unit(s, i);
                double z = 2 * WindField.Unit(s, i + 1) - 1, r = DetMath.Sqrt(1 - z * z);
                DetMath.SinCos(6.283185307179586 * WindField.Unit(s, i + 2), out double sin, out double cos);
                var dir = new Double3(r * cos, z, r * sin);
                _wave[m] = dir * k;
                Double3 c = WindField.Across(dir, WindField.Unit(s, i + 3));
                double au = 2 * DetMath.Sqrt(WindField.BandEnergy(ka, kb, ExcessU, lu)) * Double3.Dot(c, along);
                double av = 2 * DetMath.Sqrt(WindField.BandEnergy(ka, kb, ExcessV, lu)) * Double3.Dot(c, across);
                double aw = 2 * DetMath.Sqrt(WindField.BandEnergy(ka, kb, ExcessW, lw)) * c.Y;
                _amp[m] = along * au + across * av + new Double3(0, aw, 0);
                _phase[m] = 6.283185307179586 * WindField.Unit(s, i + 4);
            }
        }

        /// <summary>Fixes a step's frame from the open field's drift since the flight's start (world m): the eddies drift
        /// with it at the convection velocity's share.</summary>
        internal void Begin(in Double3 drift)
        {
            double dx = drift.X * _convection, dz = drift.Z * _convection;
            for (int m = 0; m < _wave.Length; m++)
                _offset[m] = _phase[m] - (_wave[m].X * dx + _wave[m].Z * dz);
        }

        /// <summary>The layer's plan weight at a world point: 0 outside the strip, rising smoothly to 1 at CanopyEdge·h
        /// inside its edge.</summary>
        public double Weight(in Double3 point)
        {
            double dx = point.X - Strip.CentreX, dz = point.Z - Strip.CentreZ;
            double along = dx * _ax + dz * _az, across = dz * _ax - dx * _az;
            double inside = DetMath.Min(_halfL - DetMath.Abs(along), _halfW - DetMath.Abs(across));
            return inside <= 0 ? 0 : GustFront.Smooth(inside / _edge);
        }

        /// <summary>The strip's mean speed at a height over the ground below z_b, m/s.</summary>
        public double Speed(double z)
        {
            if (z >= SublayerTop) return FrictionVelocity / Karman * DetMath.Log((z - Displacement) / Roughness);
            if (z >= Height) return TopSpeed + (z - Height) * _gradient;
            return TopSpeed * DetMath.Exp(Attenuation * (z / Height - 1));
        }

        /// <summary>Every σ's fall below h, σ(z)/σ(h).</summary>
        public double Decay(double z) => z >= Height ? 1 : DetMath.Exp(_decay * Attenuation * (z / Height - 1));

        /// <summary>The eddies' weight over the height: 1 to z_w, 0 from z_b.</summary>
        public double Layer(double z) => z <= SublayerTop ? 1 : z >= BlendingHeight ? 0 : (BlendingHeight - z) / (BlendingHeight - SublayerTop);

        /// <summary>The canopy's eddies at a world point in the step's frame, at their canopy-top σ, world m/s.</summary>
        public Double3 Eddies(in Double3 point)
        {
            double x = 0, y = 0, z = 0;
            for (int m = 0; m < _wave.Length; m++)
            {
                Double3 k = _wave[m];
                DetMath.SinCos(k.X * point.X + k.Y * point.Y + k.Z * point.Z + _offset[m], out _, out double cos);
                Double3 a = _amp[m];
                x += a.X * cos;
                y += a.Y * cos;
                z += a.Z * cos;
            }
            return new Double3(x, y, z);
        }
    }
}
