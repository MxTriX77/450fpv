using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>The field at a plant's load point, read by the plants (PlantAir): the mean air and each fluctuation family's
    /// share there (the open modes at the profile's log λ, a strip's eddies, the wake eddies at 1/H).</summary>
    internal struct PlantParts
    {
        public Double3 Mean;
        public double Lambda, Open, StripShare, Wake, WakeInverse;
        public CanopyFlow Strip;
    }

    /// <summary>A fluctuation mode at a point, given to a plant's filter: its amplitude (world m/s, the family's share in), its
    /// phase θ (rad) and its encounter rate Ω (rad/s; θ falls at Ω as the frozen field drifts past the point).</summary>
    internal interface IModeSink
    {
        void Mode(in Double3 amplitude, double phase, double rate);
    }

    /// <summary>
    /// The plants' reading of the field (w16-plants-in-the-air design.md §3): the same layers as At, read at any time t without
    /// the flight's step frame, so a frame of the drawn side never touches what the flight thread writes (Begin's offsets, mean
    /// and drift). The mean wind and its drift at t are the direction history's, as Begin's; the means at a point are Open's and
    /// InLee's; the fluctuation is handed mode by mode to a filter (IModeSink) with each mode's phase and encounter rate, so a
    /// plant's linear response to the frozen field is exact in closed form. Left out for the plants (design.md §3.4): a cut's
    /// cavity (the drone's frame); above a step's lip the floor's fluctuation is kept (InLee blends two fields' means). The wake
    /// grid is the flight's (WakeGridWorker.Sample, read while the flight thread may switch its fade: a visual glitch at worst).
    /// Zero allocations; nothing here is called by a flight.
    /// </summary>
    public sealed partial class WindField
    {
        /// <summary>The mean wind at the reference height (world m/s) and the field's drift since the start (world m) at time t
        /// (s from the flight's start): Begin's arithmetic on the time instead of the step.</summary>
        internal void PlantFrame(double t, out Double3 mean, out Double3 drift)
        {
            drift = default;
            if (!_level)
            {
                mean = Setup.Level == WindLevel.Steady && t + Dt >= _onsetStep * Dt ? _steady : default; // Begin's onset
                return;
            }
            long last = _vx.Length - 1, i = t > 0 ? (long)t : 0;
            double f = t > 0 ? t - i : 0;
            if (i >= last)
            {
                f += i - last;
                i = last;
            }
            int j = (int)i, k = j < last ? j + 1 : j;
            double ax = _vx[k] - _vx[j], az = _vz[k] - _vz[j];
            mean = new Double3(_vx[j] + ax * f, 0, _vz[j] + az * f);
            drift = new Double3(_cx[j] + (_vx[j] + 0.5 * ax * f) * f, 0, _cz[j] + (_vz[j] + 0.5 * az * f) * f);
        }

        /// <summary>The field's layers at a point over its ground (m) at the frame whose reference mean is `mean` and time t: the
        /// mean air and the families' shares (PlantParts). A gust front's gust is part of the mean at the point.</summary>
        internal void PlantParts(in Double3 point, double ground, double t, in Double3 mean, out PlantParts parts)
        {
            parts = default;
            if (_level)
            {
                if (_hills != null && _hills.Steps > 0 && _hills.Lee(point.X, point.Z, mean, out LeeFrame lee)) PlantLee(point, ground, mean, lee, ref parts);
                else PlantOpen(point, ground, mean, ref parts);
            }
            else parts.Mean = mean;
            if (_front != null) parts.Mean = parts.Mean + _front.At(point, t);
        }

        // Open's layers, its mean and shares (the same arithmetic, read at the given reference mean).
        void PlantOpen(in Double3 point, double ground, in Double3 reference, ref PlantParts parts)
        {
            double h = point.Y - ground;
            if (!(h - _d > _z0) || !(h < double.PositiveInfinity)) return;
            double lambda = Log(h), profile = lambda / _logRef;
            double taper = DetMath.Min(1, (h - _d) / Preset.TaperHeight);
            parts.Lambda = lambda;
            if (_canopies != null)
            {
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
                    parts.Open = taper * (1 - w * (1 - r));
                    parts.Strip = c;
                    parts.StripShare = taper * r * DetMath.Sqrt(w * c.Layer(h));
                    if (_wakes == null)
                    {
                        parts.Mean = Hill(reference * (profile + w * (c.Speed(h) / MeanSpeed - profile)), point.X, point.Z, lambda);
                        return;
                    }
                    _wakes.Sample(point.X, point.Z, h, out WakeSample b);
                    parts.Mean = Hill(reference * ((1 - w) * profile * b.Speed + w * (c.Speed(h) / MeanSpeed)), point.X, point.Z, lambda);
                    double b2 = b.Turbulence * b.Turbulence - 1;
                    if (b2 > 0 && b.ObstacleHeight > 0)
                    {
                        parts.Wake = DetMath.Sqrt(b2) * SigmaH * (taper * DetMath.Sqrt(1 - w));
                        parts.WakeInverse = 1 / b.ObstacleHeight;
                    }
                    return;
                }
            }
            parts.Open = taper;
            if (_wakes == null)
            {
                parts.Mean = Hill(reference * profile, point.X, point.Z, lambda);
                return;
            }
            _wakes.Sample(point.X, point.Z, h, out WakeSample s);
            parts.Mean = Hill(reference * (profile * s.Speed), point.X, point.Z, lambda);
            double t2 = s.Turbulence * s.Turbulence - 1;
            if (t2 > 0 && s.ObstacleHeight > 0)
            {
                parts.Wake = DetMath.Sqrt(t2) * SigmaH * taper;
                parts.WakeInverse = 1 / s.ObstacleHeight;
            }
        }

        // InLee's mean over the floor's layers (its fluctuation the floor's).
        void PlantLee(in Double3 point, double ground, in Double3 reference, in LeeFrame lee, ref PlantParts parts)
        {
            PlantOpen(point, ground, reference, ref parts);
            Double3 floorMean = parts.Mean;
            double depth = lee.Lip - ground;
            if (!(depth - _d > _z0) || !(point.Y - ground - _d > _z0)) return;
            double xr = Cavity.StepReattachment * depth, share = lee.Xi <= xr ? 1 : 1 - (lee.Xi - xr) / (Cavity.RecoveryLengths * xr);
            if (!(share > 0)) return;
            if (!(point.Y < lee.Lip))
            {
                var above = new PlantParts();
                PlantOpen(point, lee.Lip, reference, ref above);
                parts.Mean = above.Mean * share + floorMean * (1 - share);
                return;
            }
            var drive = new PlantParts();
            PlantOpen(new Double3(point.X, lee.Lip + Cavity.DriveHeightOverDepth * depth, point.Z), lee.Lip, reference, ref drive);
            double u0 = drive.Mean.X * lee.NX + drive.Mean.Z * lee.NZ;
            if (!(u0 > 0)) return;
            double eta = lee.Lip - point.Y, a = Cavity.CellStrength * u0;
            DetMath.SinCos(Pi * eta / depth, out double se, out double ce);
            DetMath.SinCos(Pi * DetMath.Min(lee.Xi, xr) / xr, out double sx, out double cx);
            double u = a * ce * sx, w = a * depth / xr * cx * se;
            double along = floorMean.X * lee.NX + floorMean.Z * lee.NZ;
            var bubble = new Double3(floorMean.X + (u - along) * lee.NX, w, floorMean.Z + (u - along) * lee.NZ);
            parts.Mean = floorMean + (bubble - floorMean) * share;
        }

        /// <summary>
        /// Every fluctuation mode at a point, each family at its share (PlantParts), handed to the sink with its phase at the
        /// frame's drift and its encounter rate at the frame's reference mean: the open modes κ·p + φ − κ·X, rate κ·V (at the
        /// point's own scales, Scaled's table); a strip's eddies (CanopyFlow.PlantModes); the wake eddies at the obstacle's scale.
        /// </summary>
        internal void PlantModes<T>(in Double3 point, in PlantParts parts, in Double3 drift, in Double3 reference, ref T sink) where T : struct, IModeSink
        {
            if (!_level) return;
            if (parts.Open > 0)
            {
                double share = parts.Open;
                if (_scaled)
                {
                    double sh = DetMath.Min(DetMath.Max(parts.Lambda * _perStep + _hA, _hB), _sTop);
                    double sw = DetMath.Min(DetMath.Max(parts.Lambda * _perStep + _wA, _wB), _sTop);
                    int jh = (int)sh, jw = (int)sw;
                    double th = sh - jh, tw = sw - jw;
                    for (int m = 0; m < _wave.Length; m++)
                    {
                        Double3 k = _wave[m], c = _pol[m];
                        int ih = m + jh, iw = m + jw;
                        double ah = (_unit[ih] + th * _rise[ih]) * share, aw = (_unit[iw] + tw * _rise[iw]) * share;
                        double phase = k.X * point.X + k.Y * point.Y + k.Z * point.Z + _phase[m] - (k.X * drift.X + k.Z * drift.Z);
                        sink.Mode(new Double3(c.X * ah, c.Y * aw, c.Z * ah), phase, k.X * reference.X + k.Z * reference.Z);
                    }
                }
                else
                    for (int m = 0; m < _wave.Length; m++)
                    {
                        Double3 k = _wave[m];
                        double phase = k.X * point.X + k.Y * point.Y + k.Z * point.Z + _phase[m] - (k.X * drift.X + k.Z * drift.Z);
                        sink.Mode(_amp[m] * share, phase, k.X * reference.X + k.Z * reference.Z);
                    }
            }
            if (parts.Strip != null && parts.StripShare > 0) parts.Strip.PlantModes(point, parts.StripShare, drift, reference, ref sink);
            if (parts.Wake > 0 && _eddyDir != null)
            {
                double inv = parts.WakeInverse;
                for (int m = 0; m < _eddyDir.Length; m++)
                {
                    Double3 k = _eddyDir[m];
                    double scale = _eddyScale[m] * inv;
                    double along = k.X * point.X + k.Y * point.Y + k.Z * point.Z - (k.X * drift.X + k.Z * drift.Z);
                    sink.Mode(_eddyAmp[m] * parts.Wake, along * scale + _eddyPhase[m], (k.X * reference.X + k.Z * reference.Z) * scale);
                }
            }
        }
    }

    public sealed partial class CanopyFlow
    {
        /// <summary>The strip's eddies at a point for the plants (WindField.PlantModes): frozen and drifting at the convection
        /// velocity's share of the field's drift.</summary>
        internal void PlantModes<T>(in Double3 point, double share, in Double3 drift, in Double3 reference, ref T sink) where T : struct, IModeSink
        {
            double dx = drift.X * _convection, dz = drift.Z * _convection, vx = reference.X * _convection, vz = reference.Z * _convection;
            for (int m = 0; m < _wave.Length; m++)
            {
                Double3 k = _wave[m];
                double phase = k.X * point.X + k.Y * point.Y + k.Z * point.Z + _phase[m] - (k.X * dx + k.Z * dz);
                sink.Mode(_amp[m] * share, phase, k.X * vx + k.Z * vz);
            }
        }
    }
}
