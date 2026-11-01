using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// A level's preset (design 3 and 5, K§6.7, K§8.3, K§8.6): what the level fixes. Every number is labelled; the log
    /// header records each by name (LogHeader.PresetOf).
    /// </summary>
    public struct WindPreset
    {
        /// <summary>The model's name in the log header and the summary.</summary>
        public const string Model = "field";

        /// <summary>Severe's mean wind at the reference height, m/s (assumed, tuned until T0 passes, p7c-wind 1.5, on 10 seeds
        /// with the whole airframe): at 9.5 the harness's pooled |mean roll| reads 7.17° (6–12, P's 9) and Windy's 3.37°
        /// (3.1–6.1), its runs' mean crosswind 5.8 m/s. The derived 0.384 × 20.6 m/s = 7.9 (wind.md §5.3's ratio of P's
        /// crosswind to its airspeed, at the cruise on D-050's silhouette) is P's crosswind, not the strength: under K§8.5's
        /// start rule a run's mean crosswind is about 0.63 of the strength, and at 7.9 T0 reads 6.0, its floor, and Windy's
        /// 2.9. At 11.5 T0 reads P's 9.25 and the crosswind 7.3 m/s, inside PHY-17a's 6.5–8.5, but T5 field leaves its band
        /// (0.97 against 0.22–0.66) and the known-wrong models of T5 belt, T6a and T11b pass: 9.5 keeps them.</summary>
        public const double SevereCentre = 9.5;

        /// <summary>Windy's and Calm's centres as fractions of Severe's (derived, K§8.6: c = 0.5 and 0.15).</summary>
        public const double WindyFraction = 0.5, CalmFraction = 0.15;

        /// <summary>Windy's and Calm's centres, m/s (derived: 4.75 and 1.425).</summary>
        public const double WindyCentre = WindyFraction * SevereCentre, CalmCentre = CalmFraction * SevereCentre;

        /// <summary>Each band is its centre ± 25 % (derived, K§8.6), so no two overlap.</summary>
        public const double Band = 0.25;

        public WindLevel Level;

        /// <summary>The band the per-flight strength (the mean speed at the reference height) is drawn from, m/s.</summary>
        public double MeanLo, MeanHi;

        // ---- the mean profile (K§6.7 layer 1) ----

        /// <summary>The height the drawn strength is the mean at, m (assumed: the middle of the wind study's 20–50 m test
        /// band, wind.md §6.2, where clip P's crosswind was derived).</summary>
        public double ReferenceHeight;

        /// <summary>The cover's height, m (assumed: stubble and low crop, clip P's open fields; 1.3's wake grid brings
        /// the world's own).</summary>
        public double CoverHeight;

        /// <summary>z0 and d over the cover's height (published rules of thumb, K§6.7: z0 ≈ 0.1·h, d ≈ 0.67·h).</summary>
        public double RoughnessRatio, DisplacementRatio;

        /// <summary>The log law's top, m: above it the mean holds (assumed: the neutral surface layer's depth, about a
        /// tenth of a 1 km boundary layer).</summary>
        public double ProfileTop;

        /// <summary>The turbulence's taper over the ground, m: its amplitude grows from 0 at d to full at d + this
        /// (assumed: the surface blocks the gusts within about a metre).</summary>
        public double TaperHeight;

        // ---- the turbulence (K§6.7 layer 2): von Kármán, as random Fourier modes ----

        /// <summary>σ_u/U = σ_v/U at every height (assumed: the middle of K§6.7's 0.12–0.15; the neutral surface layer's
        /// σ_u ≈ 2.4·u*, constant with height (published, Panofsky and Dutton), gives 0.136 at 35 m over z0 = 0.03 m).</summary>
        public double Intensity;

        /// <summary>σ_w/σ_u (derived: MIL-HDBK-1797's low-altitude (0.177 + 0.000823·h)^0.4 at h = 35 m = 114.8 ft).</summary>
        public double VerticalRatio;

        /// <summary>L_u, m: the horizontal components' longitudinal integral scale (derived: MIL-HDBK-1797's
        /// h / (0.177 + 0.000823·h)^1.2 at 35 m, inside K§6.7's 115–200 m). Isotropy gives L_v = L_u / 2 (the
        /// handbook's von Kármán form).</summary>
        public double HorizontalScale;

        /// <summary>L_w, m: the vertical component's (derived: the handbook's 2·L_w = h in its von Kármán form, so the
        /// isotropic scale of w is h, at 35 m; K§6.7's "L_w ≈ the height").</summary>
        public double VerticalScale;

        /// <summary>The modes (assumed: about 34 per decade of wavenumber, so the drone's 0.1–20 Hz sees a near-normal
        /// sum).</summary>
        public int Modes;

        /// <summary>The shortest wavelength, m (assumed: a rotor's 0.254 m disc averages shorter eddies' inflow, and they
        /// carry 0.7 % of the variance).</summary>
        public double ShortestWavelength;

        /// <summary>The longest scale as a fraction of the spectrum's knee: k_min = this / (1.339·L_u) (assumed: the
        /// energy below a quarter of the knee is 0.02 % of σ²).</summary>
        public double LongestFraction;

        // ---- the wakes' eddies (K§6.7 layer 3, with 1.3's wake grid) ----

        /// <summary>The wakes' eddies' wavelengths over the obstacle's height (assumed: clip P's belt eddies 5–20 m apart,
        /// K§8.2, "the size of the trees", over the stand-in maple's 8.4 m; P's belt height is not measured).</summary>
        public double WakeEddyMin, WakeEddyMax;

        /// <summary>The wakes' eddies' plane waves (assumed, as 1.2's belt).</summary>
        public int WakeModes;

        // ---- a canopy strip's flow (p7c-wind 1.5, with WindSetup.Canopy) ----

        /// <summary>σ_u/u*, σ_v/u* and σ_w/u* at the canopy's top, u along the wind (recalled, not re-read in this task:
        /// the canopy-top ensemble of Raupach, Finnigan and Brunet (1996) and Finnigan (2000), σ_u/u* about 1.8–2.0,
        /// σ_v/u* about 1.5–1.7, σ_w/u* about 1.1; below the surface layer's 2.4, 1.9 and 1.25).</summary>
        public double CanopySigmaU, CanopySigmaV, CanopySigmaW;

        /// <summary>The canopy eddies' isotropic integral scales over h: the horizontal components' and w's (the first
        /// published, Finnigan, Shaw and Patton (2009): integral length scales of order h at the canopy's top; the second
        /// derived: w's streamwise scale about 0.3 h (Kruijt et al. 2000: 0.28 h from the shear length; Kaimal and
        /// Finnigan (1994)'s h/3, recalled) is the isotropic form's lateral scale, half its L).</summary>
        public double CanopyScaleU, CanopyScaleW;

        /// <summary>The canopy eddies' random Fourier modes (assumed: about 23 per decade over their 2.7 decades).</summary>
        public int CanopyModes;

        /// <summary>The canopy eddies' convection velocity over U(h) (published: U_c ≈ 1.8·U(h), Finnigan, Shaw and Patton
        /// 2009, after Shaw et al. 1995).</summary>
        public double CanopyConvection;

        /// <summary>Below h every σ falls as (U(z)/U(h))^this (assumed: 0.5 halves σ_w at half height in a dense canopy,
        /// α ≈ 2.5–3, as the canopy ensembles show, recalled; local equilibrium would be 1).</summary>
        public double CanopyDecay;

        /// <summary>The blending height over h: the strip's surface layer meets the open field's mean there, and its
        /// eddies are gone (assumed: above the roughness sublayer's 1.4–2 h, a few canopy heights).</summary>
        public double BlendingHeights;

        /// <summary>The strip's edge blend over h: its flow grows from 0 at its edge to full this far inside (assumed:
        /// the canopy-scale eddies mix across an edge over about one canopy height).</summary>
        public double CanopyEdge;

        // ---- the direction (K§8.3, design 5) ----

        /// <summary>The meander's σ, degrees, its Ornstein–Uhlenbeck time constant, s, and its smoothing lag, s
        /// (assumed: K§8.3's starting values, which pass T0c in 1000 seed sets, wind_history_fixture.py).</summary>
        public double MeanderSigmaDeg, MeanderTau, MeanderLag;

        /// <summary>The shifts: their Poisson rate, per s, their size, degrees, either way, and their ramp, s; each
        /// returns towards the prevailing direction with the meander's time constant (assumed: K§8.3).</summary>
        public double ShiftRate, ShiftLoDeg, ShiftHiDeg, RampLo, RampHi;

        /// <summary>The history's horizon after the flight's start, s: past it the direction holds (assumed: T0c's 120 min,
        /// well past a pack's endurance).</summary>
        public int Horizon;

        WindPreset(WindLevel level, double centre)
        {
            Level = level;
            MeanLo = centre * (1 - Band);
            MeanHi = centre * (1 + Band);
            ReferenceHeight = 35;
            CoverHeight = 0.3;
            RoughnessRatio = 0.1;
            DisplacementRatio = 0.67;
            ProfileTop = 100;
            TaperHeight = 1;
            Intensity = 0.135;
            VerticalRatio = 0.594;
            HorizontalScale = 167;
            VerticalScale = 35;
            Modes = 128;
            ShortestWavelength = 1;
            LongestFraction = 0.25;
            WakeEddyMin = 0.6;
            WakeEddyMax = 2.4;
            WakeModes = 24;
            CanopySigmaU = 2.0;
            CanopySigmaV = 1.6;
            CanopySigmaW = 1.1;
            CanopyScaleU = 1.0;
            CanopyScaleW = 0.6;
            CanopyModes = 64;
            CanopyConvection = 1.8;
            CanopyDecay = 0.5;
            BlendingHeights = 3;
            CanopyEdge = 1;
            MeanderSigmaDeg = 30;
            MeanderTau = 120;
            MeanderLag = 30;
            ShiftRate = 1 / 300.0;
            ShiftLoDeg = 45;
            ShiftHiDeg = 120;
            RampLo = 10;
            RampHi = 30;
            Horizon = 7200;
        }

        /// <summary>A pilot's level's preset. Still and Steady have none.</summary>
        public static WindPreset Of(WindLevel level)
        {
            switch (level)
            {
                case WindLevel.Calm: return new WindPreset(level, CalmCentre);
                case WindLevel.Windy: return new WindPreset(level, WindyCentre);
                case WindLevel.Severe: return new WindPreset(level, SevereCentre);
                default: throw new ArgumentException("only Calm, Windy and Severe have a preset", nameof(level));
            }
        }

        /// <summary>z0, m.</summary>
        public double Roughness => RoughnessRatio * CoverHeight;

        /// <summary>d, m.</summary>
        public double Displacement => DisplacementRatio * CoverHeight;

        internal void Check()
        {
            if (!(MeanLo > 0 && MeanHi >= MeanLo) || !(CoverHeight > 0) || !(RoughnessRatio > 0) || !(DisplacementRatio >= 0)
                || !(ReferenceHeight > Displacement + Roughness) || !(ProfileTop >= ReferenceHeight) || !(TaperHeight > 0)
                || !(Intensity >= 0) || !(VerticalRatio >= 0) || !(HorizontalScale > 0) || !(VerticalScale > 0) || Modes < 1
                || !(ShortestWavelength > 0) || !(LongestFraction > 0) || !(WakeEddyMin > 0 && WakeEddyMax >= WakeEddyMin) || WakeModes < 1
                || !(CanopySigmaU >= 0) || !(CanopySigmaV >= 0) || !(CanopySigmaW >= 0) || !(CanopyScaleU > 0) || !(CanopyScaleW > 0)
                || CanopyModes < 1 || !(CanopyConvection >= 0) || !(CanopyDecay >= 0) || !(BlendingHeights > 2) || !(CanopyEdge > 0)
                || !(MeanderSigmaDeg >= 0) || !(MeanderTau > 0)
                || !(MeanderLag > 0) || !(ShiftRate > 0) || !(ShiftLoDeg >= 0 && ShiftHiDeg >= ShiftLoDeg)
                || !(RampLo > 0 && RampHi >= RampLo) || Horizon < 1)
                throw new ArgumentException("the wind preset is out of its bounds");
        }
    }

    /// <summary>
    /// The wind field (p7c-wind 1.4, design 3 and 5, K§6.7, K§8.3, PHY-13): a pure function of the flight's time and a
    /// world point, given the ground under the body, so it holds no state of its own between steps and a flight
    /// replays it from its seed. Its layers, sampled at each rotor's hub, the body's centre of pressure and the coil:
    /// - **The direction** (K§8.3, wind_history_fixture.py's model step for step at 1 Hz): the prevailing bearing drawn
    ///   per flight (uniform, wind.direction), an Ornstein–Uhlenbeck meander (wind.meander: one near-normal draw per
    ///   second) through a first-order lag, scaled to its σ, and Poisson shifts (wind.shift: the gap, size, side and
    ///   ramp of each), each a linear ramp then an exponential return. Built at the flight's start for Horizon seconds
    ///   from StartSecond, from 10 meander time constants before the history's start; between seconds the mean wind's
    ///   velocity is linear and its drift (the integral) exact.
    /// - **The mean profile:** the drawn strength U (wind.strength, uniform in the level's band) at the reference
    ///   height, scaled by the log law ln((z − d)/z0) / ln((z_ref − d)/z0), z the height over the ground under the
    ///   body, held above ProfileTop, 0 at and below d + z0 (inside the cover). Horizontal.
    /// - **The turbulence:** von Kármán's isotropic spectrum E(k) = C·σ²·(k/k_e)^4 / (1 + (k/k_e)²)^(17/6),
    ///   k_e = 1/(1.339·L), C·k_e·½B(5/2, 1/3) = 1.5 (so ∫E = 1.5·σ² and its 1-D spectra are MIL-HDBK-1797's), as
    ///   Modes random Fourier modes in log-spaced bands from k_min to 2π/ShortestWavelength: per mode a wavenumber
    ///   uniform in its band, a direction uniform on the sphere, a polarisation across it (so each mode carries
    ///   vorticity) at a uniform angle and a uniform phase, from wind.turbulence's draws 5·n to 5·n + 4 (counter-based).
    ///   Its horizontal part has the amplitude 2·√(band energy) of σ_u and L_u, its vertical part that of σ_w and L_w.
    ///   Frozen and advected with the mean wind (Taylor): the phase is κ·(x − X(t)) + φ, X(t) the drift. Each
    ///   component is bounded by the sum of its modes' amplitudes (Bound). Tapered to 0 at d over TaperHeight.
    /// - **The wakes** (p7c-wind 1.5), when the setup has obstacles (WakeLayout): 1.3's wake grid of them on its worker
    ///   (WakeGridWorker), stepped with the direction's bearing at each step and sampled at each point at its height over
    ///   the ground under the body. Its speed scales the mean along the wind (below 0 the flow reverses); its turbulence
    ///   T is the local σ over the open field's: the open modes stay, and wake eddies at the obstacle's scale carry the
    ///   rest, √(T² − 1)·σ_u, tapered as the open modes. The eddies are WakeModes plane waves u = a·ĉ·cos(κ̂·(x − X(t))·2π
    ///   / (λ·H) + φ), κ̂ uniform on the sphere, ĉ across it (vorticity, no divergence) at a uniform angle, λ uniform in
    ///   WakeEddyMin–WakeEddyMax, φ uniform (wind.wake's draws 5·m to 5·m + 4, counter-based), a = √(6/WakeModes), so each
    ///   component has unit σ; H is the sample's ObstacleHeight. Frozen and advected with the drift, as the open modes.
    /// - **Wooded strips' canopy flow** (p7c-wind 1.5, CanopyFlow), when the setup has them: inside a strip and below its
    ///   blending height, the canopy's mean profile blended in by the plan weight w, the open modes scaled by
    ///   1 − w·(1 − r), r the canopy's fall of σ below its top, and its own eddies by r·√(w·layer). With wakes too (a
    ///   map's, MapWind: the belt's plants are the strips and, for its lee, one porous obstacle per strip, never their
    ///   own volumes), the point is shared once: the canopy takes w of the mean, U·(w·U_c(z)/U + (1 − w)·P(z)·s), P the
    ///   open profile and s the wake grid's speed, and the wake eddies carry √(1 − w) of their excess. Outside every
    ///   strip, or above its blending height, the wakes alone.
    /// - **A gust front** (GustFront), only when the setup has one.
    /// Randomness enters only the wind's velocity, a physical parameter, and reaches the body only through the drag and
    /// rotor forces (flight-randomness): never the pose. Begin fixes a step's frame (its time, the drift, each mode's
    /// phase offset, the ground); At answers for that frame. Zero allocations after the constructor.
    /// </summary>
    public sealed class WindField : IDisposable
    {
        const double Dt = 0.001; // FlightCore.Dt
        const double DegToRad = 0.017453292519943295;
        const double TwoPi = 6.283185307179586;

        /// <summary>½·B(5/2, 1/3) = ∫₀^∞ x⁴/(1 + x²)^(17/6) dx (derived: ½·Γ(5/2)·Γ(1/3)/Γ(17/6)).</summary>
        public const double SpectrumIntegral = 1.0325159139214712;

        /// <summary>The von Kármán knee's constant (published, MIL-HDBK-1797): k_e = 1/(1.339·L).</summary>
        public const double Knee = 1.339;

        public readonly WindSetup Setup;
        public readonly WindPreset Preset;

        /// <summary>The prevailing bearing the wind blows from, degrees, and its strength at the reference height, m/s
        /// (the per-flight draws); for Steady its fixed bearing and speed.</summary>
        public readonly double FromDeg, MeanSpeed;

        /// <summary>σ_u = σ_v and σ_w, m/s.</summary>
        public readonly double SigmaH, SigmaV;

        /// <summary>Each turbulence component's bound, m/s: the sum of its modes' amplitudes.</summary>
        public readonly Double3 Bound;

        readonly bool _level;
        readonly Double3 _steady;
        readonly long _onsetStep; // Steady only: the onset, in steps (0: blowing from the start)
        readonly double _z0, _d, _logRef, _top;

        // The direction history from the flight's start, per second: the bearing, the mean wind's velocity at the
        // reference height (X, Z) and its drift since the start (X, Z).
        readonly double[] _fromDeg, _vx, _vz, _cx, _cz;

        // The turbulence's modes: wave vector (rad/m), amplitude (m/s, world), phase (rad); each step's phase offset.
        readonly Double3[] _wave, _amp;
        readonly double[] _phase, _offset;

        // The wakes: the worker and the eddies' unit wave directions, 2π/λ over H, unit amplitudes, phases and each step's
        // offset −κ̂·X(t).
        readonly WakeGridWorker _wakes;
        readonly Double3[] _eddyDir, _eddyAmp;
        readonly double[] _eddyScale, _eddyPhase, _eddyOffset;
        readonly GustFront _front;

        // The wooded strips' canopy layers (p7c-wind 1.5), or null.
        readonly CanopyFlow[] _canopies;

        // The step's frame (Begin).
        double _t, _ground;
        bool _still; // a steady wind before its onset
        Double3 _mean, _drift, _origin;

        /// <summary>The wind of a flight with this setup, its per-flight draws made from random (at the flight's start).</summary>
        public WindField(in WindSetup setup, FlightRandom random) : this(setup, random, PresetFor(setup.Level)) { }

        /// <summary>The same with a preset of the caller's (a test's known-wrong field); the log records the level's own.</summary>
        public WindField(in WindSetup setup, FlightRandom random, in WindPreset preset)
        {
            Setup = setup;
            Preset = preset;
            _front = setup.Front?.Checked();
            if ((setup.Wakes != null || setup.Canopies != null) && (setup.Level == WindLevel.Still || setup.Level == WindLevel.Steady))
                throw new ArgumentException("wakes and a canopy need a pilot's level", nameof(setup));
            if (setup.StartSecond != 0 && (setup.Level == WindLevel.Still || setup.Level == WindLevel.Steady))
                throw new ArgumentException("a start second needs a pilot's level", nameof(setup));
            switch (setup.Level)
            {
                case WindLevel.Still:
                    return;
                case WindLevel.Steady:
                    if (!(setup.SteadySpeed >= 0)) throw new ArgumentException("a steady wind's speed is 0 or more", nameof(setup));
                    FromDeg = setup.SteadyFromDeg;
                    MeanSpeed = setup.SteadySpeed;
                    _steady = Toward(FromDeg, MeanSpeed);
                    if (!(setup.SteadyOnsetSeconds >= 0)) throw new ArgumentException("a steady wind's onset is 0 s or later", nameof(setup));
                    _onsetStep = (long)(setup.SteadyOnsetSeconds / Dt + 0.5);
                    return;
            }
            if (random == null) throw new ArgumentNullException(nameof(random));
            preset.Check();
            if (setup.StartSecond < 0 || setup.StartSecond > preset.Horizon)
                throw new ArgumentException("the start second is 0 to the preset's horizon", nameof(setup));
            _level = true;
            SeedStream direction = random.Open("wind.direction");
            FromDeg = random.Draw(ref direction, "wind.from_deg", 0, 360);
            SeedStream strength = random.Open("wind.strength");
            MeanSpeed = random.Draw(ref strength, "wind.mean_speed_m_s", preset.MeanLo, preset.MeanHi);

            // The profile.
            _z0 = preset.Roughness;
            _d = preset.Displacement;
            _top = preset.ProfileTop;
            _logRef = DetMath.Log((preset.ReferenceHeight - _d) / _z0);

            // The direction history.
            int n = preset.Horizon + 1;
            _fromDeg = new double[n];
            _vx = new double[n];
            _vz = new double[n];
            _cx = new double[n];
            _cz = new double[n];
            double[] deviation = Deviation(random, preset, setup.StartSecond);
            for (int i = 0; i < n; i++)
            {
                _fromDeg[i] = FromDeg + deviation[i];
                Double3 v = Toward(_fromDeg[i], MeanSpeed);
                _vx[i] = v.X;
                _vz[i] = v.Z;
                if (i == 0) continue;
                _cx[i] = _cx[i - 1] + 0.5 * (_vx[i - 1] + _vx[i]);
                _cz[i] = _cz[i - 1] + 0.5 * (_vz[i - 1] + _vz[i]);
            }

            // The turbulence's modes.
            SigmaH = preset.Intensity * MeanSpeed;
            SigmaV = SigmaH * preset.VerticalRatio;
            SeedStream turbulence = random.Open("wind.turbulence");
            int modes = preset.Modes;
            _wave = new Double3[modes];
            _amp = new Double3[modes];
            _phase = new double[modes];
            _offset = new double[modes];
            double kMin = preset.LongestFraction / (Knee * preset.HorizontalScale), kMax = TwoPi / preset.ShortestWavelength;
            double ratio = kMax / kMin, bound0 = 0, bound1 = 0, bound2 = 0;
            for (int m = 0; m < modes; m++)
            {
                double ka = kMin * DetMath.Exp(DetMath.Log(ratio) * m / modes), kb = kMin * DetMath.Exp(DetMath.Log(ratio) * (m + 1) / modes);
                ulong i = (ulong)m * 5;
                double k = ka + (kb - ka) * Unit(turbulence, i);
                double z = 2 * Unit(turbulence, i + 1) - 1, r = DetMath.Sqrt(1 - z * z);
                DetMath.SinCos(TwoPi * Unit(turbulence, i + 2), out double sin, out double cos);
                var dir = new Double3(r * cos, z, r * sin);
                _wave[m] = dir * k;
                Double3 c = Across(dir, Unit(turbulence, i + 3));
                double ah = 2 * DetMath.Sqrt(BandEnergy(ka, kb, SigmaH, preset.HorizontalScale));
                double aw = 2 * DetMath.Sqrt(BandEnergy(ka, kb, SigmaV, preset.VerticalScale));
                _amp[m] = new Double3(ah * c.X, aw * c.Y, ah * c.Z);
                _phase[m] = TwoPi * Unit(turbulence, i + 4);
                bound0 += DetMath.Abs(_amp[m].X);
                bound1 += DetMath.Abs(_amp[m].Y);
                bound2 += DetMath.Abs(_amp[m].Z);
            }
            Bound = new Double3(bound0, bound1, bound2);
            if (setup.Canopies != null)
            {
                if (setup.Canopies.Length == 0) throw new ArgumentException("a setup's strips are null or at least one", nameof(setup));
                SeedStream canopy = random.Open("wind.canopy");
                _canopies = new CanopyFlow[setup.Canopies.Length];
                for (int i = 0; i < _canopies.Length; i++)
                    _canopies[i] = new CanopyFlow(setup.Canopies[i], preset, FromDeg, MeanSpeed, Profile, SigmaH, SigmaV, canopy);
            }
            if (setup.Wakes == null) return;

            // The wakes (p7c-wind 1.5): the grid for the history's first bearing, and the eddies from the counter-based
            // stream wind.wake.
            WakeObstacles obstacles = setup.Wakes.Checked().Obstacles();
            SeedStream wake = random.Open("wind.wake");
            int ne = preset.WakeModes;
            _eddyDir = new Double3[ne];
            _eddyAmp = new Double3[ne];
            _eddyScale = new double[ne];
            _eddyPhase = new double[ne];
            _eddyOffset = new double[ne];
            double unit = DetMath.Sqrt(6.0 / ne);
            for (int m = 0; m < ne; m++)
            {
                ulong i = (ulong)m * 5;
                double z = 2 * Unit(wake, i) - 1, r = DetMath.Sqrt(1 - z * z);
                DetMath.SinCos(TwoPi * Unit(wake, i + 1), out double sin, out double cos);
                var k = new Double3(r * cos, z, r * sin);
                _eddyDir[m] = k;
                _eddyScale[m] = TwoPi / (preset.WakeEddyMin + (preset.WakeEddyMax - preset.WakeEddyMin) * Unit(wake, i + 2));
                _eddyAmp[m] = Across(k, Unit(wake, i + 3)) * unit;
                _eddyPhase[m] = TwoPi * Unit(wake, i + 4);
            }
            _wakes = new WakeGridWorker(obstacles, _fromDeg[0]);
        }

        /// <summary>Stops the wake grid's worker, if the flight has one.</summary>
        public void Dispose() => _wakes?.Dispose();

        /// <summary>The wake grid's worker, or null without wakes.</summary>
        public WakeGridWorker Wakes => _wakes;

        static WindPreset PresetFor(WindLevel level) =>
            level == WindLevel.Still || level == WindLevel.Steady ? default : WindPreset.Of(level);

        /// <summary>A pilot's level: the field has a profile, so the core gives Begin the ground under the body.</summary>
        public bool HasProfile => _level;

        /// <summary>The bearing the wind blows from at second i of the flight's history (its prevailing direction with
        /// the meander and the shifts, K§8.3), degrees, not wrapped; past the horizon its last.</summary>
        public double FromDegAt(int second)
        {
            if (!_level) return FromDeg;
            return _fromDeg[second < 0 ? 0 : second >= _fromDeg.Length ? _fromDeg.Length - 1 : second];
        }

        /// <summary>
        /// Fixes the frame of the step that starts at `step`: its time, the mean wind at the reference height and its
        /// drift, each mode's phase offset, the body's origin (world) and the ground's height under it (m; not finite
        /// in a hole, which the field takes as sheltered until 1.3's cavity).
        /// </summary>
        public void Begin(long step, in Double3 origin, double ground)
        {
            _t = step * Dt;
            _origin = origin;
            _ground = ground;
            if (!_level)
            {
                // A steady wind with an onset blows over the step that ends at it and every one after (p7b-airframe 1.3c's
                // gust step: the stand-in's gust carried −Mean until then).
                _still = step + 1 < _onsetStep;
                return;
            }
            long last = _vx.Length - 1, i = step / 1000;
            double f = (step - i * 1000) * Dt;
            if (i >= last)
            {
                f += i - last;
                i = last;
            }
            int j = (int)i, k = j < last ? j + 1 : j;
            double ax = _vx[k] - _vx[j], az = _vz[k] - _vz[j];
            _mean = new Double3(_vx[j] + ax * f, 0, _vz[j] + az * f);
            _drift = new Double3(_cx[j] + (_vx[j] + 0.5 * ax * f) * f, 0, _cz[j] + (_vz[j] + 0.5 * az * f) * f);
            for (int m = 0; m < _wave.Length; m++)
                _offset[m] = _phase[m] - (_wave[m].X * _drift.X + _wave[m].Z * _drift.Z);
            if (_canopies != null) foreach (CanopyFlow c in _canopies) c.Begin(_drift);
            if (_wakes == null) return;
            _wakes.Step(step, _fromDeg[j] + (_fromDeg[k] - _fromDeg[j]) * f);
            for (int m = 0; m < _eddyDir.Length; m++)
                _eddyOffset[m] = -(_eddyDir[m].X * _drift.X + _eddyDir[m].Z * _drift.Z);
        }

        /// <summary>The mean wind at the reference height in the step's frame, world m/s.</summary>
        public Double3 Mean => _level ? _mean : _still ? default : _steady;

        /// <summary>The field's drift since the flight's start in the step's frame, world m.</summary>
        public Double3 Drift => _drift;

        /// <summary>The wind's velocity at a world point in the step's frame, world m/s.</summary>
        public Double3 At(in Double3 point)
        {
            Double3 wind = _level ? Air(point) : _still ? default : _steady;
            if (_front != null) wind = wind + _front.At(_front.Uniform ? _origin : point, _t);
            return wind;
        }

        /// <summary>The turbulence alone at a world point in the step's frame, untapered, world m/s.</summary>
        public Double3 Turbulence(in Double3 point)
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

        /// <summary>The first strip's canopy layer, or null without a canopy strip.</summary>
        public CanopyFlow Canopy => _canopies?[0];

        /// <summary>Every strip's canopy layer in the setup's order, or null without one.</summary>
        public CanopyFlow[] Canopies => _canopies;

        // The open field's mean at a height over the ground, over the reference height's.
        double Profile(double h) => DetMath.Log((DetMath.Min(h, _top) - _d) / _z0) / _logRef;

        Double3 Air(in Double3 point)
        {
            double h = point.Y - _ground;
            if (!(h - _d > _z0) || !(h < double.PositiveInfinity)) return default; // in the cover, or sheltered in a hole
            double profile = Profile(h);
            double taper = DetMath.Min(1, (h - _d) / Preset.TaperHeight);
            if (_canopies != null)
            {
                // The strip whose layer holds the point (CanopyFlow; a map's strips never overlap): its mean blended in by
                // the plan weight, the open modes falling with its σ inside, its eddies carrying the rest.
                CanopyFlow c = null;
                double w = 0;
                foreach (CanopyFlow strip in _canopies)
                {
                    if (!(h < strip.BlendingHeight)) continue;
                    double ws = strip.Weight(point);
                    if (ws > w) { w = ws; c = strip; }
                }
                if (w > 0)
                {
                    double r = c.Decay(h);
                    Double3 open = Turbulence(point) * (taper * (1 - w * (1 - r)));
                    Double3 canopy = c.Eddies(point) * (taper * r * DetMath.Sqrt(w * c.Layer(h)));
                    if (_wakes == null)
                    {
                        double speed = profile + w * (c.Speed(h) / MeanSpeed - profile);
                        return _mean * speed + open + canopy;
                    }
                    // With wakes (MapWind's rule): the canopy takes w of the mean and the wake grid the rest, and the wake
                    // eddies √(1 − w) of their excess, so the belt is counted once.
                    _wakes.Sample(point.X, point.Z, h, out WakeSample b);
                    Double3 mixed = _mean * ((1 - w) * profile * b.Speed + w * (c.Speed(h) / MeanSpeed)) + open + canopy;
                    double b2 = b.Turbulence * b.Turbulence - 1;
                    if (!(b2 > 0) || !(b.ObstacleHeight > 0)) return mixed;
                    return mixed + WakeEddies(point, 1 / b.ObstacleHeight) * (DetMath.Sqrt(b2) * SigmaH * (taper * DetMath.Sqrt(1 - w)));
                }
            }
            Double3 turbulence = Turbulence(point) * taper;
            if (_wakes == null) return _mean * profile + turbulence;
            _wakes.Sample(point.X, point.Z, h, out WakeSample s);
            Double3 wind = _mean * (profile * s.Speed) + turbulence;
            double t2 = s.Turbulence * s.Turbulence - 1;
            if (!(t2 > 0) || !(s.ObstacleHeight > 0)) return wind;
            return wind + WakeEddies(point, 1 / s.ObstacleHeight) * (DetMath.Sqrt(t2) * SigmaH * taper);
        }

        // The wake eddies' sum at a point for an obstacle of height 1/inv, each component of unit σ.
        Double3 WakeEddies(in Double3 point, double inv)
        {
            Double3 eddies = default;
            for (int m = 0; m < _eddyDir.Length; m++)
            {
                Double3 k = _eddyDir[m];
                double along = k.X * point.X + k.Y * point.Y + k.Z * point.Z + _eddyOffset[m];
                DetMath.SinCos(along * _eddyScale[m] * inv + _eddyPhase[m], out _, out double cos);
                eddies = eddies + _eddyAmp[m] * cos;
            }
            return eddies;
        }

        /// <summary>The wind's velocity of a speed blowing from a bearing, world (+X east, +Z north), m/s.</summary>
        public static Double3 Toward(double fromDeg, double speed)
        {
            DetMath.SinCos(fromDeg * DegToRad, out double sin, out double cos);
            return new Double3(-sin * speed, 0, -cos * speed);
        }

        /// <summary>
        /// The von Kármán spectrum's energy between ka and kb for σ and L, m²/s²: Simpson's rule on 8 intervals of
        /// E(k) = 1.5·σ²·(k/k_e)⁴ / (k_e·½B(5/2, 1/3)·(1 + (k/k_e)²)^(17/6)), the power as q²·√q·∛q.
        /// </summary>
        public static double BandEnergy(double ka, double kb, double sigma, double scale)
        {
            double ke = 1 / (Knee * scale), c = 1.5 * sigma * sigma / (ke * SpectrumIntegral), h = (kb - ka) / 8, sum = 0;
            for (int i = 0; i <= 8; i++)
            {
                double x = (ka + h * i) / ke, q = 1 + x * x;
                double e = c * x * x * x * x / (q * q * DetMath.Sqrt(q) * DetMath.Cbrt(q));
                sum += e * (i == 0 || i == 8 ? 1 : i % 2 == 1 ? 4 : 2);
            }
            return sum * h / 3;
        }

        // A unit vector across `along` (a unit vector) at the angle 2π·u about it.
        internal static Double3 Across(in Double3 along, double u)
        {
            Double3 e1 = Double3.Cross(along, DetMath.Abs(along.Y) < 0.9 ? new Double3(0, 1, 0) : new Double3(1, 0, 0));
            e1 = e1 * (1 / e1.Length());
            Double3 e2 = Double3.Cross(along, e1);
            DetMath.SinCos(TwoPi * u, out double sin, out double cos);
            return e1 * cos + e2 * sin;
        }

        // Draw i of a counter-based stream as a uniform value in [0, 1): the top 53 bits, exactly.
        internal static double Unit(in SeedStream stream, ulong i) => (stream.BitsAt(i) >> 11) * (1.0 / 9007199254740992.0);

        // Draw i of a stream as a nearly normal value (Irwin–Hall: 12 uniforms less 6, bounded by ±6).
        static double Normal(in SeedStream stream, ulong i)
        {
            double sum = -6;
            for (ulong j = 0; j < 12; j++) sum += Unit(stream, 12 * i + j);
            return sum;
        }

        // ceil(x) for |x| < 2^53, from the basic operations.
        static double Ceiling(double x)
        {
            double t = (long)x;
            return t < x ? t + 1 : t;
        }

        /// <summary>
        /// The direction's deviation from the prevailing bearing, degrees, at each second from `start` to start + Horizon:
        /// wind_history_fixture.py's meander and shifts on this flight's streams. The meander: x ← a1·x + b1·w and
        /// y ← a2·y + (1 − a2)·x (y from the old x) from rest 10 time constants before second 0, a1 = e^(−1/τ),
        /// a2 = e^(−1/lag), b1 = √(1 − a1²), w wind.meander's draw per second, then σ·y over the pair's stationary
        /// spread. The shifts: onsets from 10 time constants before second 0 by exponential gaps, each a ramp then a
        /// return summed by one recurrence (the fixture's own).
        /// </summary>
        static double[] Deviation(FlightRandom random, in WindPreset p, int start)
        {
            int n = start + p.Horizon + 1;
            var dev = new double[n];
            int burn = (int)(10 * p.MeanderTau);
            double a1 = DetMath.Exp(-1 / p.MeanderTau), a2 = DetMath.Exp(-1 / p.MeanderLag);
            double gain = DetMath.Sqrt((1 - a2) * (1 - a2) * (1 + a1 * a2) / ((1 - a2 * a2) * (1 - a1 * a2)));
            double b1 = DetMath.Sqrt(1 - a1 * a1), x = 0, y = 0;
            SeedStream meander = random.Open("wind.meander");
            for (int i = 0; i < burn + n; i++)
            {
                y = a2 * y + (1 - a2) * x;
                x = a1 * x + b1 * Normal(meander, (ulong)i);
                if (i >= burn) dev[i - burn] = p.MeanderSigmaDeg * y / gain;
            }

            SeedStream shift = random.Open("wind.shift");
            var enter = new double[n];
            double end = n - 1, onset = -10 * p.MeanderTau;
            for (ulong s = 0; ; s++)
            {
                onset += -DetMath.Log(1 - Unit(shift, 4 * s)) / p.ShiftRate;
                if (onset >= end) break;
                double amp = p.ShiftLoDeg + (p.ShiftHiDeg - p.ShiftLoDeg) * Unit(shift, 4 * s + 1);
                if (Unit(shift, 4 * s + 2) < 0.5) amp = -amp;
                double ramp = p.RampLo + (p.RampHi - p.RampLo) * Unit(shift, 4 * s + 3);
                int i = (int)DetMath.Max(0, Ceiling(onset));
                while (i < n && i - onset < ramp)
                {
                    dev[i] += amp * (i - onset) / ramp;
                    i++;
                }
                if (i < n) enter[i] += amp * DetMath.Exp(-(i - onset - ramp) / p.MeanderTau);
            }
            double q = DetMath.Exp(-1 / p.MeanderTau), d = 0;
            for (int i = 0; i < n; i++)
            {
                d = d * q + enter[i];
                dev[i] += d;
            }
            var fromStart = new double[p.Horizon + 1];
            Array.Copy(dev, start, fromStart, 0, fromStart.Length);
            return fromStart;
        }
    }
}
