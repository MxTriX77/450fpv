using System;
using UnityEngine;

namespace Fpv450.Fiber
{
    /// <summary>
    /// The thin-wire coverage rule of <c>Shaders/ThinWire.shader</c> on the CPU (p7d-tether 1.14): the fiber's tests check
    /// the rule with it, and the scene hands the shader the feed's read through it. A line of true width 2h render pixels
    /// gives a pixel x pixels from its centre line the coverage T((x + h) / R) − T((x − h) / R), T the integral of a tent of
    /// unit area and radius one, R the filter's radius: one pixel on a plain render, and through the analog feed the feed's
    /// sample spacing across the line (<see cref="FeedFilterPx"/>, the shader's <c>_THIN_WIRE_FEED</c>). Summed across the
    /// line those coverages are its true width wherever it falls between the samples, so a 0.25 mm line weighs D/d of a pixel
    /// per radian of the render and fades with distance, never widened to show (the object sheet's 270·D/d on the feed).
    /// </summary>
    public static class FiberCoverage
    {
        /// <summary>The tent's radius on a plain render, pixels (the shader's THIN_WIRE_FILTER_PX).</summary>
        public const double FilterPx = 1.0;

        /// <summary>The feed's scene taps across the picture and its field lines (Shaders/Feed.shader's SC_SAMPLES and LINES,
        /// the Godot feed's gd:71-72).</summary>
        public const float FeedTaps = 920f, FeedLines = 286f;

        /// <summary>The shader's global <c>_ThinWireFeedRead</c> while the feed reads the render: the lens's k1 (FeedSettings.K1)
        /// and the render's coverage in half-widths (FpvCamera.RenderCoverage), the feed's taps and lines. Vector4.zero when
        /// nothing reads the render (the feed off): the plain rule.</summary>
        public static Vector4 FeedRead(float k1, float renderCoverage) => new Vector4(k1, renderCoverage, FeedTaps, FeedLines);

        /// <summary>The integral of a tent of unit area and radius one, from −∞ to t (the shader's TentIntegral).</summary>
        public static double TentIntegral(double t)
        {
            t = Math.Max(-1.0, Math.Min(1.0, t));
            return t < 0.0 ? 0.5 * (1.0 + t) * (1.0 + t) : 1.0 - 0.5 * (1.0 - t) * (1.0 - t);
        }

        /// <summary>A pixel's coverage at x pixels from the centre line of a line of half-width h pixels, through a tent of
        /// radius filter pixels.</summary>
        public static double Coverage(double x, double h, double filter = FilterPx)
            => TentIntegral((Math.Abs(x) + h) / filter) - TentIntegral((Math.Abs(x) - h) / filter);

        /// <summary>
        /// The shader's FeedFilterPx: the tent's radius in render pixels at a point of a render width × height pixels (ndc, the
        /// render's −1…1 across and up), across a line whose screen direction across is (ax, ay) (any length; zero for the
        /// widest spacing in any direction). The feed's taps along a field line and its field lines down the picture, mapped
        /// into the render by the lens's Jacobian at the point the feed reads there (p_u = p_d (1 + k1 r_d²) in half-widths),
        /// never under one pixel. With read.w = 0 it is one pixel.
        /// </summary>
        public static double FeedFilterPx(Vector4 read, double width, double height, double ndcX, double ndcY, double ax, double ay)
        {
            if (read.w <= 0) return FilterPx;
            double aspect = width / height, k1 = read.x, coverage = read.y;
            double ux = ndcX * coverage, uy = ndcY * coverage / aspect;
            double ru = Math.Sqrt(ux * ux + uy * uy), rd = ru;
            for (int i = 0; i < 4; i++) rd -= (rd + k1 * rd * rd * rd - ru) / (1.0 + 3.0 * k1 * rd * rd);
            double s = ru > 1e-6 ? rd / ru : 1.0, dx = ux * s, dy = uy * s;
            double m = 1.0 + k1 * rd * rd, perHalfWidth = width / (2.0 * coverage);
            double tapScale = 2.0 / read.z * perHalfWidth, lineScale = 2.0 / (aspect * read.w) * perHalfWidth;
            double tapX = (m + 2.0 * k1 * dx * dx) * tapScale, tapY = 2.0 * k1 * dx * dy * tapScale;
            double lineX = 2.0 * k1 * dx * dy * lineScale, lineY = (m + 2.0 * k1 * dy * dy) * lineScale;
            double a = Math.Sqrt(ax * ax + ay * ay), spacing;
            if (a > 0)
            {
                ax /= a;
                ay /= a;
                spacing = Math.Max(Math.Abs(tapX * ax + tapY * ay), Math.Abs(lineX * ax + lineY * ay));
            }
            else spacing = Math.Max(Math.Sqrt(tapX * tapX + tapY * tapY), Math.Sqrt(lineX * lineX + lineY * lineY));
            return Math.Max(spacing, FilterPx);
        }
    }
}
