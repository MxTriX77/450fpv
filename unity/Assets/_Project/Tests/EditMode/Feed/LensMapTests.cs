using System;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The water on the lens as the feed's lens pass reads it (w21-rain-on-camera 1.2, FEED-13): a drop's cap on the map, its
    /// contact line and its slopes against the cap's geometry written again here; a trail's pearls holding the trail's water;
    /// the entrance pupil's depth putting the picture's corner bundle at the clear radius. Run by Unity's EditMode and by
    /// <c>dotnet test tools/feedtests</c>; the drawing itself is the rendered stills' (Fpv450.RainFields).
    /// </summary>
    public sealed class LensMapTests
    {
        // The physics' LensSpec: 4.5 mm clear, an 8 mm dome, water receding at 20°.
        const float Clear = 0.0045f, Dome = 0.008f, Receding = 20f * (float)Math.PI / 180f;

        static LensMap Map() => new LensMap(Clear, Dome, Receding);

        // The texel nearest lens (x, y), m.
        static int At(float x, float y)
        {
            float texel = 2f * Clear / LensMap.Size;
            int i = (int)Math.Floor((x + Clear) / texel), j = (int)Math.Floor((y + Clear) / texel);
            return (j * LensMap.Size + i) * 4;
        }

        static float TexelCentre(float x)
        {
            float texel = 2f * Clear / LensMap.Size;
            return -Clear + ((float)Math.Floor((x + Clear) / texel) + 0.5f) * texel;
        }

        [Test]
        public void ADryMapHoldsNoWater()
        {
            LensMap m = Map();
            Assert.That(m.Wet, Is.False);
            for (int i = 0; i < m.Texels.Length; i += 4)
                Assert.That(m.Texels[i] < -0.5f && m.Texels[i + 1] == 0f && m.Texels[i + 2] == 0f, "texel " + i / 4);
            m.Add(new LensBlob { X = 0f, Y = 0f, Radius = 0f, Height = 0f });
            Assert.That(m.Wet, Is.False, "a drop with no water draws nothing");
        }

        /// A round cap at rest: positive inside its contact line and negative past it, its surface level at the top and as
        /// steep as the receding angle at the line, its slope pointing down and away from its centre, its curvature 1/R.
        [Test]
        public void ARoundDropIsItsCap()
        {
            LensMap m = Map();
            float r = 1.5e-3f, h = r * (float)Math.Tan(Receding / 2), cx = 0.5e-3f, cy = -0.3e-3f;
            m.Add(new LensBlob { X = cx, Y = cy, Radius = r, Height = h, Elongation = 1f, AxisX = 1f, AxisY = 0f });
            Assert.That(m.Wet && m.Drops == 1, Is.True);

            int centre = At(cx, cy);
            Assert.That(m.Texels[centre], Is.GreaterThan(1.3f), "deep in the water at its centre (mm)");
            Assert.That(Math.Abs(m.Texels[centre + 1]) + Math.Abs(m.Texels[centre + 2]), Is.LessThan(0.01f), "level at the top");
            float R = (r * r + h * h) / (2 * h);
            Assert.That(m.Texels[centre + 3], Is.EqualTo(1e-3f / R).Within(1e-4f), "curvature 1/R, 1/mm");

            // Along +X from the centre: the signed distance falls through 0 at the line, the slope is the cap's.
            for (float x = 0.2e-3f; x < r + 0.15e-3f; x += 0.1e-3f)
            {
                float px = TexelCentre(cx + x), py = TexelCentre(cy), dx = px - cx, dy = py - cy, rho = (float)Math.Sqrt(dx * dx + dy * dy);
                int t = At(cx + x, cy);
                Assert.That(m.Texels[t], Is.EqualTo((r - rho) * 1e3f).Within(0.03f), $"the line's distance at {x * 1e3f:0.0} mm");
                float slope = (float)Math.Sqrt(m.Texels[t + 1] * m.Texels[t + 1] + m.Texels[t + 2] * m.Texels[t + 2]);
                float on = Math.Min(rho, r), expect = on / (float)Math.Sqrt(R * R - on * on); // |dz/dρ| of the sphere
                Assert.That(slope, Is.EqualTo(expect).Within(0.01f), $"the slope at {x * 1e3f:0.0} mm");
                Assert.That(m.Texels[t + 1] * dx + m.Texels[t + 2] * dy, Is.LessThan(0f), "down and away from the centre");
            }
            int rim = At(cx + r - 0.02e-3f, cy);
            float rimSlope = (float)Math.Sqrt(m.Texels[rim + 1] * m.Texels[rim + 1] + m.Texels[rim + 2] * m.Texels[rim + 2]);
            Assert.That(rimSlope, Is.EqualTo((float)Math.Tan(Receding)).Within(0.02f), "as steep as the receding angle at the line");
            Assert.That(m.Texels[At(cx + r + 0.6e-3f, cy)], Is.LessThan(-0.5f), "dry past the feather");
        }

        /// A sliding drop is longer along its axis and narrower across it, over the same area, at the same height: steeper
        /// across than along.
        [Test]
        public void AnElongatedDropIsLongAlongItsAxis()
        {
            LensMap m = Map();
            float r = 1.2e-3f, h = r * (float)Math.Tan(Receding / 2), e = 2f;
            float ax = 0.6f, ay = 0.8f; // its axis, unit
            m.Add(new LensBlob { X = 0f, Y = 0f, Radius = r, Height = h, Elongation = e, AxisX = ax, AxisY = ay });
            float a = r * (float)Math.Sqrt(e), b = r / (float)Math.Sqrt(e);
            Assert.That(m.Texels[At(0.9f * a * ax, 0.9f * a * ay)], Is.GreaterThan(0f), "in the water near its long end");
            Assert.That(m.Texels[At(-1.15f * b * ay, 1.15f * b * ax)], Is.LessThan(0f), "dry past its short end");
            int along = At(0.7f * a * ax, 0.7f * a * ay), across = At(-0.7f * b * ay, 0.7f * b * ax);
            float sAlong = (float)Math.Sqrt(m.Texels[along + 1] * m.Texels[along + 1] + m.Texels[along + 2] * m.Texels[along + 2]);
            float sAcross = (float)Math.Sqrt(m.Texels[across + 1] * m.Texels[across + 1] + m.Texels[across + 2] * m.Texels[across + 2]);
            Assert.That(sAcross, Is.GreaterThan(1.5f * sAlong), "steeper across than along");
        }

        /// A trail's stretch is drawn as the pearls its film gathers into: each a cap at the receding angle, evenly along it,
        /// their water the stretch's, spaced at Plateau–Rayleigh's wavelength of the rivulet the water makes.
        [Test]
        public void ATrailsFilmGathersIntoPearlsHoldingItsWater()
        {
            LensMap m = Map();
            var f = new LensFilm { X0 = -2e-3f, Y0 = 1e-3f, X1 = 2e-3f, Y1 = 1e-3f, Width = 2e-3f, Film = 4e-6f };
            m.Add(f);
            Assert.That(m.Wet, Is.True);
            double t = Receding, s = Math.Sin(t), section = (t - s * Math.Cos(t)) / (4 * s * s);
            double water = f.Width * f.Film, width = Math.Sqrt(water / section), length = 4e-3;
            int n = Math.Max(1, (int)Math.Round(length / (LensMap.PearlWavelength * width)));
            Assert.That(m.Pearls, Is.EqualTo(n));
            double tanHalf = Math.Tan(t / 2), shape = Math.PI / 6 * tanHalf * (3 + tanHalf * tanHalf);
            double r = Math.Pow(water * length / n / shape, 1.0 / 3.0);
            TestContext.Progress.WriteLine($"a 4 µm film 2 mm wide over 4 mm: a {width * 1e3:0.000} mm rivulet, {n} pearls of {r * 1e3:0.000} mm radius, " +
                                           $"{length / n * 1e3:0.00} mm apart");
            Assert.That(n * shape * r * r * r, Is.EqualTo(water * length).Within(1e-6).Percent, "the pearls hold the film's water");
            for (int j = 0; j < n; j++)
            {
                float x = f.X0 + (j + 0.5f) / n * (f.X1 - f.X0);
                Assert.That(m.Texels[At(x, f.Y0)], Is.EqualTo((float)r * 1e3f).Within(0.04f), $"pearl {j} at its place");
            }
            Assert.That(m.Texels[At(0f, f.Y0 + 3f * (float)r + 0.3e-3f)], Is.LessThan(-0.5f), "dry beside the row");
        }

        /// The entrance pupil's depth: every ray of the picture's corner bundle, the pupil's width, meets the dome within the
        /// clear radius and the outermost just at it; the picture's centre bundle stays near the axis.
        [Test]
        public void ThePupilsDepthPutsTheCornerBundleAtTheClearRadius()
        {
            float k1 = 0.33f, f = 1.005f;
            float depth = LensMap.PupilDepth(Clear, Dome, k1, f);
            TestContext.Progress.WriteLine($"entrance pupil {LensMap.PupilRadiusM * 2e3f:0.00} mm across, {depth * 1e3f:0.000} mm behind the apex");
            Assert.That(depth, Is.EqualTo(3.81e-3f).Within(0.02e-3f), "the note's hand number");
            double py = 9.0 / 16.0, sc = 1 + k1 * (1 + py * py), dx = sc, dy = py * sc, dz = f, n = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double far = 0;
            for (int k = 0; k < 360; k++)
            {
                double a = 2 * Math.PI * k / 360;
                Assert.That(LensMap.Dome(LensMap.PupilRadiusM * Math.Cos(a), LensMap.PupilRadiusM * Math.Sin(a), -depth, dx / n, dy / n, dz / n, Dome,
                    out double qx, out double qy), Is.True);
                far = Math.Max(far, Math.Sqrt(qx * qx + qy * qy));
            }
            Assert.That(far, Is.EqualTo(Clear).Within(0.01e-3), "the corner bundle reaches the clear radius");
            Assert.That(LensMap.Dome(0, 0, -depth, 0, 0, 1, Dome, out double cx, out double cy) && cx == 0 && cy == 0, Is.True, "the axis at the apex");
        }
    }
}
