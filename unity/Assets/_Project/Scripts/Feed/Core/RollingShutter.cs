using System;
using System.Numerics;

namespace Fpv450.Feed
{
    /// <summary>
    /// The camera's sensor read line by line, and each line's exposure (p7e-camera-feel design 4 and 5; fpv-camera spec).
    /// The build's Caddx Ratel 2 is a CMOS sensor (its 1/1.8" Starlight, published), so it reads a field top to bottom:
    /// field row r is read at (25 + r + ½) × 64 µs after the field starts (PAL's 64 µs line and 25 blanking lines a field,
    /// published; read in step with the output, assumed). Each row is the scene over its exposure, the window ending at
    /// its read, sampled along the camera's own rotation in that window: <see cref="Build"/> turns the camera's rotations
    /// against the field's reference instant into each row's taps, <see cref="Track"/>, which the feed's field pass reads.
    /// Engine-free: the flight hands it the camera at the history's 1 ms steps, the renderer only reads the result.
    /// </summary>
    public sealed class RollingShutter
    {
        /// PAL's line, µs, and its field blanking, lines (published, 625/50).
        public const double LineUs = 64, BlankingLines = 25;
        /// The field texture's rows (FeedSettings.FieldRows): the 286 lines and an odd field's half line.
        public const int Rows = 287;
        /// The most taps a row's exposure is sampled with, and the track's texels a row: the taps, then the count.
        public const int Taps = 8, RowTexels = Taps + 1;

        /// The camera's samples <see cref="Build"/> reads: the history's steps from one field before the field's start
        /// (the longest exposure, 1/50 s) to its end, 1 ms apart: sample i at (i − 20) ms of the field's time.
        public const int Samples = 41, SampleLead = 20;
        public const double SampleUs = 1000;

        /// The Ratel 2's PAL shutter, s: 1/100 000 to 1/50 (published).
        public const double MinExposureS = 1e-5, MaxExposureS = 0.02;
        /// The reflected-light meter's K, cd·s/m² (published, ISO 2720); the lens's f-number (assumed: the 2.1 mm M8
        /// lens class's f/2.0) and the sensor's base sensitivity (assumed: ISO 100 at unity gain).
        public const double MeterK = 12.5, FNumber = 2.0, Sensitivity = 100;
        /// Sunlight's scene luminance, cd/m²: sunny-16 at EV 15 and ISO 100, 2^15 × 12.5 / 100 (derived). The exposure's
        /// stand-in while the scene's light is unmeasured (the AE loop, p4-feed 1.7).
        public const double DaylightLuminance = 4096;

        /// Row r's read, µs after its field starts.
        public static double RowReadUs(int row) => (BlankingLines + row + 0.5) * LineUs;

        /// The field's reference instant, µs after its start: the middle row's read, 10 784 µs. The scene is rendered with
        /// the camera there and each row turned from it.
        public static readonly double ReferenceUs = RowReadUs(Rows / 2);

        /// The exposure for the scene's mean luminance (cd/m²), s: K·N²/(L·S), clamped to the shutter's range; 0 (or less)
        /// is unmeasured and takes <see cref="DaylightLuminance"/>.
        public static double ExposureS(float luminance)
        {
            double l = luminance > 0 ? luminance : DaylightLuminance;
            double t = MeterK * FNumber * FNumber / (l * Sensitivity);
            return t < MinExposureS ? MinExposureS : t > MaxExposureS ? MaxExposureS : t;
        }

        /// The track: per row (top first), <see cref="Taps"/> rotations (x, y, z, w; camera at the tap against the camera
        /// at the reference) then (tap count, 0, 0, 0): RGBA floats, <see cref="RowTexels"/> × <see cref="Rows"/>.
        public readonly float[] Track = new float[Rows * RowTexels * 4];

        /// True once a built track turns any row's read from the reference by more than 1e-9 rad.
        public bool Turning { get; private set; }

        public RollingShutter() => Clear();

        /// Every row read at the reference with one tap: the scene as rendered (the chase view, a still drone).
        public void Clear()
        {
            Array.Clear(Track, 0, Track.Length);
            for (int r = 0; r < Rows; r++)
            {
                int o = r * RowTexels * 4;
                for (int j = 0; j < Taps; j++) Track[o + j * 4 + 3] = 1f;
                Track[o + Taps * 4] = 1f;
            }
            Turning = false;
        }

        /// <summary>
        /// The field's track from the camera's rotation against the reference at the history's steps (<see cref="Samples"/>
        /// of them, sample i at (i − <see cref="SampleLead"/>) ms of the field's time, unit quaternions), an exposure of
        /// exposureS seconds, and the render's pixels per radian at the centre (the taps' spacing): each row's exposure
        /// window ends at its read and is sampled at n equally spaced times, n the window's sweep in render pixels rounded
        /// up, 1 to <see cref="Taps"/>.
        /// </summary>
        public void Build(Quaternion[] camera, double exposureS, double pixelsPerRad)
        {
            if (camera == null || camera.Length != Samples) throw new ArgumentException("the camera's " + Samples + " samples", nameof(camera));
            double exposureUs = Math.Max(0, exposureS) * 1e6;
            bool turning = false;
            for (int r = 0; r < Rows; r++)
            {
                double read = RowReadUs(r);
                // The sweep: the largest turn from the read's rotation to any of Taps times in the window.
                Quaternion end = At(camera, read);
                double sweep = 0;
                for (int j = 1; j < Taps; j++)
                    sweep = Math.Max(sweep, Angle(end, At(camera, read - exposureUs * j / (Taps - 1))));
                int n = (int)Math.Ceiling(sweep * pixelsPerRad);
                n = n < 1 ? 1 : n > Taps ? Taps : n;
                int o = r * RowTexels * 4;
                for (int j = 0; j < Taps; j++)
                {
                    Quaternion q = j < n ? At(camera, read - exposureUs * (j + 0.5) / n) : Quaternion.Identity;
                    Track[o + j * 4] = q.X;
                    Track[o + j * 4 + 1] = q.Y;
                    Track[o + j * 4 + 2] = q.Z;
                    Track[o + j * 4 + 3] = q.W;
                    turning |= j < n && Angle(q, Quaternion.Identity) > 1e-9;
                }
                Track[o + Taps * 4] = n;
                Track[o + Taps * 4 + 1] = Track[o + Taps * 4 + 2] = Track[o + Taps * 4 + 3] = 0f;
            }
            Turning = turning;
        }

        /// Row r's tap count and tap j's rotation, as the shader reads them.
        public int TapsOf(int row) => (int)Track[row * RowTexels * 4 + Taps * 4];

        public Quaternion TapOf(int row, int tap)
        {
            int o = row * RowTexels * 4 + tap * 4;
            return new Quaternion(Track[o], Track[o + 1], Track[o + 2], Track[o + 3]);
        }

        // The camera at µs t of the field's time, normalised-linear between the samples around it (PoseFrame.Lerp's blend),
        // held at the ends.
        static Quaternion At(Quaternion[] camera, double t)
        {
            double s = t / SampleUs + SampleLead;
            if (s <= 0) return camera[0];
            if (s >= Samples - 1) return camera[Samples - 1];
            int i = (int)s;
            float f = (float)(s - i);
            Quaternion a = camera[i], b = camera[i + 1];
            if (Quaternion.Dot(a, b) < 0) b = Quaternion.Negate(b);
            return Quaternion.Normalize(new Quaternion(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f, a.W + (b.W - a.W) * f));
        }

        /// The angle between two unit rotations, rad: from the vector part of a⁻¹·b, which keeps a small angle's precision
        /// where the dot product's arccosine would round it away.
        public static double Angle(Quaternion a, Quaternion b)
        {
            Quaternion d = Quaternion.Conjugate(a) * b;
            double v = Math.Sqrt((double)d.X * d.X + (double)d.Y * d.Y + (double)d.Z * d.Z);
            return 2 * Math.Atan2(v, Math.Abs((double)d.W));
        }
    }
}
