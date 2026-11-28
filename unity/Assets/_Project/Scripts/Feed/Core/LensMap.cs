using System;

namespace Fpv450.Feed
{
    /// <summary>
    /// One drop on the FPV camera's front glass as the feed draws it (FEED-13, w21-rain-on-camera 1.2): the physics' LensDrop
    /// (LensWater, 1.1) in the lens's own axes, metres from the optical axis, +X right and +Y up as the pilot sees the picture.
    /// A spherical cap of apex Height over an elliptical patch of equal-area radius Radius, Elongation times as long along
    /// (AxisX, AxisY) as it is wide.
    /// </summary>
    public struct LensBlob
    {
        public float X, Y, Radius, Height, Elongation, AxisX, AxisY;
    }

    /// <summary>
    /// One stretch of a sliding drop's trail (LensWater's TrailOf, 1.1): from (X0, Y0) to (X1, Y1) on the glass, metres, the
    /// film the drop's receding edge laid over it, Width wide and Film thick (the newer point's).
    /// </summary>
    public struct LensFilm
    {
        public float X0, Y0, X1, Y1, Width, Film;
    }

    /// <summary>
    /// The water on the FPV camera's front glass as the feed's lens pass reads it (FEED-13, D-126, w21-rain-on-camera 1.2):
    /// a Size × Size map over the glass's clear square, ±ClearRadius, row 0 at −ClearRadius in Y. Each texel holds, in mm,
    /// the signed distance to the nearest drop's contact line (positive in the water), the water surface's slope over the glass
    /// (∂h/∂X, ∂h/∂Y) and its curvature (1/mm, for the blur's mip). Engine-free; the rig uploads it as an RGBAFloat texture.
    /// </summary>
    /// <remarks>
    /// <para><b>How a drop on the front glass images</b> (docs/reference-notes/lens-drops.md §8). The lens is focused far away,
    /// so the glass is deep out of focus: each picture point is the bundle of parallel rays that fills the lens's entrance
    /// pupil, and that bundle crosses the front glass over a footprint the pupil's size. Where the footprint is dry the point
    /// is the scene as before; where it is under water each ray is refracted by the drop's surface (Snell at the water's
    /// surface and at the glass, each with its Fresnel transmission, total internal reflection dark), so the drop shows the
    /// scene shifted, minified and blurred (the cap is a short-focus lens inside the pupil), and its edge is as soft as the
    /// footprint is wide. The pass samples the footprint at <see cref="PupilSamples"/> points.</para>
    /// <para><b>Trails.</b> A film this thin on partially wetting glass does not stay a film: films under the critical
    /// thickness, about a capillary length times sin(θ/2) (0.5–1 mm here), dewet (published, de Gennes, Brochard-Wyart and
    /// Quéré). Its water is drawn as the pearls it gathers into: the rivulet the stretch's water makes at the receding angle,
    /// broken at Plateau–Rayleigh's wavelength (assumed for a rivulet), each pearl a cap at the receding angle holding its
    /// wavelength's water. They image as small drops do, so a trail is a row of soft, smeared spots.</para>
    /// </remarks>
    public sealed class LensMap
    {
        /// The map's texels a side, over the clear square: 47 µm at the 4.5 mm clear radius (numerical: a pearl of 0.1 mm
        /// radius spans four).
        public const int Size = 192;

        /// Water's refractive index (published: 1.333 at 589 nm, 20 °C).
        public const float WaterIndex = 1.333f;

        /// The lens's focal length and f-number: 2.1 mm at f/2.0 (published: the class's M12 2.1 mm lens, RunCam RC21's
        /// listing and Edmund Optics' 2.1 mm f/2 micro video lens). Its entrance pupil is f/N across.
        public const float FocalLengthM = 2.1e-3f, FNumber = 2.0f;

        /// The entrance pupil's radius, m: 0.525 mm (derived).
        public const float PupilRadiusM = FocalLengthM / FNumber / 2f;

        /// The lens pass's samples over the pupil (numerical): a Vogel spiral, each sample standing for 1/16 of the pupil.
        public const int PupilSamples = 16;

        /// A sample's share of the pupil as a disc's radius, m: the feather the pass gives a contact line, and the blur a
        /// sample stands for (numerical).
        public const float SampleRadiusM = PupilRadiusM * 0.25f; // PupilRadiusM / √PupilSamples

        /// Plateau–Rayleigh's fastest wavelength in diameters, 4.51 (published for a free jet), taken for a rivulet with its
        /// width as the diameter (assumed): a trail's pearls are this many rivulet widths apart.
        public const float PearlWavelength = 4.51f;

        /// A pearl under this patch radius is left undrawn (numerical: half a texel, under it the map cannot hold its slope).
        public const float MinPearlRadiusM = 0.0045f / Size;

        /// The texels: Size × Size × 4 floats, row-major from (−ClearRadius, −ClearRadius): the signed distance to the contact
        /// line (mm, + in water), the slope ∂h/∂X and ∂h/∂Y, the curvature (1/mm).
        public readonly float[] Texels = new float[Size * Size * 4];

        /// The glass's clear radius and radius of curvature, m (the physics' LensSpec), and water's receding angle on it, rad.
        public readonly float ClearRadius, CurvatureRadius, RecedingAngle;

        readonly float _texel, _tanHalf, _capShape, _rivulet;

        /// Any water drawn since <see cref="Clear"/>.
        public bool Wet { get; private set; }

        /// The drops and the trails' pearls drawn since <see cref="Clear"/>.
        public int Drops { get; private set; }
        public int Pearls { get; private set; }

        /// <param name="clearRadius">The front glass's clear radius, m (LensSpec.ClearRadius).</param>
        /// <param name="curvatureRadius">Its radius of curvature, m (LensSpec.CurvatureRadius; +∞ for flat glass).</param>
        /// <param name="recedingAngle">Water's receding angle on it, rad (LensSpec.RecedingAngle): the pearls' angle.</param>
        public LensMap(float clearRadius, float curvatureRadius, float recedingAngle)
        {
            if (!(clearRadius > 0 && curvatureRadius > clearRadius && recedingAngle > 0 && recedingAngle < Math.PI / 2))
                throw new ArgumentException("the glass needs a clear radius under its curvature and a receding angle in (0, 90°)");
            ClearRadius = clearRadius;
            CurvatureRadius = curvatureRadius;
            RecedingAngle = recedingAngle;
            _texel = 2f * clearRadius / Size;
            _tanHalf = (float)Math.Tan(recedingAngle / 2);
            _capShape = (float)(Math.PI / 6 * _tanHalf * (3 + _tanHalf * _tanHalf)); // a cap at θ: V = shape·r³
            double t = recedingAngle, s = Math.Sin(t);
            _rivulet = (float)((t - s * Math.Cos(t)) / (4 * s * s)); // a rivulet at θ: its section is this times its width²
            Clear();
        }

        /// Dries the map.
        public void Clear()
        {
            float dry = -1f; // mm: well past the feather, so a dry texel covers nothing
            for (int i = 0; i < Texels.Length; i += 4)
            {
                Texels[i] = dry;
                Texels[i + 1] = Texels[i + 2] = Texels[i + 3] = 0f;
            }
            Wet = false;
            Drops = Pearls = 0;
        }

        /// Draws one drop.
        public void Add(in LensBlob d)
        {
            if (!(d.Radius > 0 && d.Height > 0)) return;
            Cap(d.X, d.Y, d.Radius, d.Height, Math.Max(1f, d.Elongation), d.AxisX, d.AxisY);
            Drops++;
        }

        /// Draws one stretch of a trail as the pearls its film gathers into (see the class's remarks); the pearls sit evenly
        /// along it, so a stretch once laid keeps its pearls where they are.
        public void Add(in LensFilm f)
        {
            float dx = f.X1 - f.X0, dy = f.Y1 - f.Y0, length = (float)Math.Sqrt(dx * dx + dy * dy);
            float water = f.Width * f.Film; // m³ per m of trail
            if (!(length > 0 && water > 0)) return;
            float width = (float)Math.Sqrt(water / _rivulet), spacing = PearlWavelength * width;
            int n = Math.Max(1, (int)Math.Round(length / spacing));
            float volume = water * length / n;
            float r = (float)Math.Pow(volume / _capShape, 1.0 / 3.0);
            if (r < MinPearlRadiusM) return;
            for (int j = 0; j < n; j++)
            {
                float a = (j + 0.5f) / n;
                Cap(f.X0 + a * dx, f.Y0 + a * dy, r, r * _tanHalf, 1f, 1f, 0f);
                Pearls++;
            }
        }

        // A spherical cap of height h over an ellipse of equal-area radius r, e times as long along (ax, ay) as it is wide: its
        // texels within the feather keep it where it is the nearer water (the larger signed distance). The cap's surface is the
        // round cap's, z(ρ) = √(R² − r²ρ²) − (R − h) with R = (r² + h²)/2h, at the ellipse's normalised radius ρ; past the
        // contact line its slope is the rim's, so the pass's bilinear read is right up to the line.
        void Cap(float cx, float cy, float r, float h, float e, float ax, float ay)
        {
            float norm = (float)Math.Sqrt(ax * ax + ay * ay);
            if (norm > 0) { ax /= norm; ay /= norm; } else { ax = 1f; ay = 0f; }
            float se = (float)Math.Sqrt(e), a = r * se, b = r / se;
            float R = (r * r + h * h) / (2f * h);
            float curvature = e / R * 1e-3f; // 1/mm, the larger principal curvature (for the pass's blur)
            float feather = SampleRadiusM + _texel;
            float reach = a + feather;
            int i0 = Math.Max(0, (int)Math.Floor((cx - reach + ClearRadius) / _texel)), i1 = Math.Min(Size - 1, (int)Math.Floor((cx + reach + ClearRadius) / _texel));
            int j0 = Math.Max(0, (int)Math.Floor((cy - reach + ClearRadius) / _texel)), j1 = Math.Min(Size - 1, (int)Math.Floor((cy + reach + ClearRadius) / _texel));
            for (int j = j0; j <= j1; j++)
            {
                float y = -ClearRadius + (j + 0.5f) * _texel - cy;
                for (int i = i0; i <= i1; i++)
                {
                    float x = -ClearRadius + (i + 0.5f) * _texel - cx;
                    float u = x * ax + y * ay, v = -x * ay + y * ax;
                    float fu = u / (a * a), fv = v / (b * b);
                    float rho2 = u * fu + v * fv, rho = (float)Math.Sqrt(rho2);
                    // The ellipse's normalised radius over its gradient, first order to the line (a circle's r − ρ exactly), m.
                    float grad = (float)Math.Sqrt(fu * fu + fv * fv);
                    float sd = rho > 1e-6f && grad > 0 ? Math.Min(b, (1f - rho) * rho / grad) : b;
                    if (sd < -feather) continue;
                    int at = (j * Size + i) * 4;
                    if (sd * 1e3f <= Texels[at]) continue;
                    // The slope at this texel, or at the rim along its radius past the line.
                    float s = rho2 > 1f ? 1f / (float)Math.Sqrt(rho2) : 1f;
                    float under = R * R - r * r * Math.Min(rho2, 1f);
                    float k = -r * r / (float)Math.Sqrt(Math.Max(under, 1e-18f));
                    float zu = k * fu * s, zv = k * fv * s;
                    Texels[at] = sd * 1e3f;
                    Texels[at + 1] = zu * ax - zv * ay;
                    Texels[at + 2] = zu * ay + zv * ax;
                    Texels[at + 3] = curvature;
                }
            }
            Wet = true;
        }

        /// <summary>A map as a file (a bench's lens: RainFields' -dump, NoclipFeed's -feedlens): the glass, the counts and the texels.</summary>
        public void Save(string path)
        {
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            w.Write(0x50414D4C); // "LMAP"
            w.Write(Size);
            w.Write(ClearRadius);
            w.Write(CurvatureRadius);
            w.Write(RecedingAngle);
            w.Write(Drops);
            w.Write(Pearls);
            foreach (float t in Texels) w.Write(t);
        }

        /// <summary>A map <see cref="Save"/> wrote.</summary>
        public static LensMap Load(string path)
        {
            using var r = new System.IO.BinaryReader(System.IO.File.OpenRead(path));
            if (r.ReadInt32() != 0x50414D4C || r.ReadInt32() != Size) throw new System.IO.InvalidDataException(path + " is not a lens map of this size");
            var map = new LensMap(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            map.Drops = r.ReadInt32();
            map.Pearls = r.ReadInt32();
            for (int i = 0; i < map.Texels.Length; i++) map.Texels[i] = r.ReadSingle();
            map.Wet = map.Drops + map.Pearls > 0;
            return map;
        }

        /// <summary>
        /// The entrance pupil's depth behind the glass's apex, m (derived, docs/reference-notes/lens-drops.md §8): every chief
        /// ray points at the pupil's centre (an object-space property of a lens, its pupil aberration left out), and the front
        /// element is just large enough to pass the picture's corner bundle unvignetted (assumed: no drawing of the lens was
        /// found), so the bundle of the picture's corner, the pupil's width wide, meets the dome at most at the clear radius.
        /// The corner is the feed's: k1 and the pinhole's f in half-widths (FeedSettings), 16:9. 3.81 mm at the defaults.
        /// </summary>
        public static float PupilDepth(float clearRadius, float curvatureRadius, float k1, float focalHalfWidths)
        {
            double py = 9.0 / 16.0, r2 = 1 + py * py, s = 1 + k1 * r2;
            double dx = s, dy = py * s, dz = focalHalfWidths, n = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx /= n; dy /= n; dz /= n;
            double lo = 1e-5, hi = 0.05;
            for (int it = 0; it < 60; it++)
            {
                double depth = 0.5 * (lo + hi), far = 0;
                for (int k = 0; k < 72; k++)
                {
                    double a = 2 * Math.PI * k / 72;
                    if (!Dome(PupilRadiusM * Math.Cos(a), PupilRadiusM * Math.Sin(a), -depth, dx, dy, dz, curvatureRadius, out double qx, out double qy))
                    {
                        far = double.PositiveInfinity;
                        break;
                    }
                    far = Math.Max(far, Math.Sqrt(qx * qx + qy * qy));
                }
                if (far > clearRadius) hi = depth;
                else lo = depth;
            }
            return (float)(0.5 * (lo + hi));
        }

        /// <summary>Where the ray from (ex, ey, ez) along the unit (dx, dy, dz) leaves the dome of radius R whose apex is the
        /// origin (its centre at (0, 0, −R)), across the axis: the same intersection the lens pass takes.</summary>
        public static bool Dome(double ex, double ey, double ez, double dx, double dy, double dz, double R, out double qx, out double qy)
        {
            double oz = ez + R, b = ex * dx + ey * dy + oz * dz, c = ex * ex + ey * ey + oz * oz - R * R, disc = b * b - c;
            if (disc < 0)
            {
                qx = qy = 0;
                return false;
            }
            double t = -b + Math.Sqrt(disc);
            qx = ex + t * dx;
            qy = ey + t * dy;
            return true;
        }
    }
}
