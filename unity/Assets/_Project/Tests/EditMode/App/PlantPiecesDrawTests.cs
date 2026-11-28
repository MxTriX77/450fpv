using System;
using Fpv450.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The pieces' drawing (w16-plants-in-the-air 1.4, Shaders/PlantPieces.shader) against its own rules, in an empty scene through
    /// URP with a camera of known focal length (1080 rows, 60° up and down: 935.3 px a radian):
    /// - a straw 0.1 m long and 4 mm thick, 2 m out, covers its own projected area, L·W·f²/z² = 87.5 px², summed over its quad's
    ///   alpha, still and swept 0.1 m over the exposure (the sweep spreads the coverage, it never adds to it);
    /// - a dust puff's centre pixel takes 1 − e^(−τ), τ = k·m/(2π·σ_h·σ_v) through its centre (the closed form's); behind a wall
    ///   it adds nothing, and before it the wall takes its far part as the closed form truncates it at the scene's depth.
    /// Needs a GPU: run without -nographics.
    /// </summary>
    public sealed class PlantPiecesDrawTests
    {
        const int Width = 1920, Height = 1080;

        [Test]
        public void APieceCoversItsAreaAndAPuffItsOpticalDepth()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("no GPU (-nographics)");
            var go = new GameObject("Pieces test camera");
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var image = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true);
            var view = new PlantPiecesView();
            GameObject wall = null;
            Light sun = null;
            try
            {
                Assert.That(view.Ready, "Resources/" + PlantPiecesView.MaterialResource);
                Camera camera = go.AddComponent<Camera>();
                camera.fieldOfView = 60;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0, 0, 0, 0);
                camera.allowHDR = false;
                camera.targetTexture = target;
                camera.transform.SetPositionAndRotation(new Vector3(0, 1, 0), Quaternion.identity);
                // URP's camera data at its defaults: no post-processing, no anti-aliasing.
                double focal = Height / 2.0 / Math.Tan(30 * Math.PI / 180);

                // A straw, still: 0.1 m along x, 4 mm, 2 m ahead.
                float Alpha(PlantPieces.Snapshot s, double exposure)
                {
                    view.Draw(s, true, s.T0 + 2 * s.TickSeconds, camera, exposure);
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    RenderTexture.active = null;
                    double sum = 0;
                    foreach (Color c in image.GetPixels()) sum += c.a;
                    return (float)sum;
                }
                double own = 0.1 * 0.004 * focal * focal / 4;
                float still = Alpha(Straw(new Vector3(0, 1, 2), new Vector3(0, 1, 2)), 0);
                float swept = Alpha(Straw(new Vector3(0, 0.8f, 2), new Vector3(0, 1.2f, 2)), 0.01);
                TestContext.WriteLine($"a straw's own projected area {own:0.0} px²; its quad's alpha summed: still {still:0.0}, swept 0.1 m {swept:0.0}");
                Assert.That(still, Is.EqualTo(own).Within(15).Percent, "still: its own area");
                Assert.That(swept, Is.EqualTo(own).Within(15).Percent, "swept: its own area, spread");

                // A puff 3 m ahead: σ_h 0.3, σ_v 0.2 m, k·m 0.05 m²; through its centre τ = k·m/(2π·σ_h·σ_v).
                PlantPieces.Snapshot puff = Puff(new Vector3(0, 1, 3), 0.3f, 0.2f, 0.05f);
                double tau = 0.05 / (2 * Math.PI * 0.3 * 0.2), expected = 1 - Math.Exp(-tau);
                view.Draw(puff, true, puff.T0 + 2 * puff.TickSeconds, camera, 0);
                camera.Render();
                float centre = Centre(target, image);
                TestContext.WriteLine($"a puff's centre pixel alpha {centre:0.000}, its closed form's {expected:0.000} (τ {tau:0.000}); dust passes recorded {view.DustRecorded}");
                Assert.That(view.DustRecorded, Is.GreaterThan(0), "the dust's pass ran");
                Assert.That(centre, Is.EqualTo(expected).Within(10).Percent);

                // A wall 2 m ahead, lit by a sun: the puff 3 m ahead behind it adds nothing; 1.5 m ahead, in front of it, the wall takes
                // its far part, Φ((2 − 1.5)/σ_h) of its optical depth along the axis kept (the closed form's truncation at the depth).
                sun = new GameObject("Pieces test sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1;
                sun.transform.rotation = Quaternion.Euler(50, 30, 0);
                Color Rgb(PlantPieces.Snapshot s)
                {
                    view.Draw(s, true, 0.08, camera, 0); // a tick past the snapshots' tick: drawn at its end
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    RenderTexture.active = null;
                    return image.GetPixel(Width / 2, Height / 2);
                }
                PlantPieces.Snapshot near = Puff(new Vector3(0, 1, 1.5f), 0.3f, 0.2f, 0.05f);
                Color free = Rgb(near);
                wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
                wall.transform.SetPositionAndRotation(new Vector3(0, 1, 2), Quaternion.identity);
                wall.transform.localScale = new Vector3(10, 10, 1);
                wall.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.black };
                Color bare = Rgb(null), behind = Rgb(puff), front = Rgb(near);
                double kept = 0.5 * (1 + Erf(0.5 / 0.3 / Math.Sqrt(2)));
                double ratio = front.r / Math.Max(1e-9, free.r), expectedRatio = (1 - Math.Exp(-tau * kept)) / (1 - Math.Exp(-tau));
                TestContext.WriteLine($"lit: the puff 1.5 m ahead alone {free.r:0.0000}, before the wall {front.r:0.0000} (ratio {ratio:0.000}, the closed form's {expectedRatio:0.000}); " +
                                      $"behind the wall {behind.r:0.00000} against the bare wall's {bare.r:0.00000}");
                Assert.That(free.r, Is.GreaterThan(0.001), "the lit puff shows");
                Assert.That(Math.Abs(behind.r - bare.r), Is.LessThan(1e-4), "a surface in front hides it");
                Assert.That(ratio, Is.EqualTo(expectedRatio).Within(3).Percent, "the wall takes the puff's far part");
            }
            finally
            {
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                if (sun != null) UnityEngine.Object.DestroyImmediate(sun.gameObject);
                view.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        // erf (Abramowitz and Stegun 7.1.26, |error| ≤ 1.5e-7).
        static double Erf(double x)
        {
            double t = 1 / (1 + 0.3275911 * Math.Abs(x));
            double y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return x < 0 ? -y : y;
        }

        static float Centre(RenderTexture target, Texture2D image)
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = null;
            // The composite's alpha is the camera's colour over black: its luminance over the puff's colour is unknown, so read the
            // alpha channel the composite wrote (One OneMinusSrcAlpha over a clear target).
            return image.GetPixel(Width / 2, Height / 2).a;
        }

        // A snapshot of one straw (kind 4) whose path runs from a to b over the tick, born before it, its axis along x, no tumble.
        static PlantPieces.Snapshot Straw(Vector3 a, Vector3 b)
        {
            var s = new PlantPieces.Snapshot { Tick = 40, T0 = 0, TickSeconds = 0.04, Pieces = 1 };
            int o = PlantPieces.CutFirst * PlantPieces.Stride;
            for (int k = 0; k <= PlantPieces.SubSteps; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / (float)PlantPieces.SubSteps);
                s.Data[o + 4 * k] = p.x;
                s.Data[o + 4 * k + 1] = p.y;
                s.Data[o + 4 * k + 2] = p.z;
            }
            s.Data[o + 3] = (float)PlantPieces.Kind.Straw;
            s.Data[o + 7] = -1;   // born
            s.Data[o + 11] = 1e9f; // never lies
            s.Data[o + 19] = 0;   // no tumble
            s.Data[o + 20] = 0.1f;
            s.Data[o + 21] = 0.004f;
            s.Data[o + 26] = 1;
            s.Data[o + 27] = 8;   // straw
            s.Data[o + 28] = 1;   // its axis along x
            s.Data[o + 33] = 1;   // the tumble's axis up
            return s;
        }

        static PlantPieces.Snapshot Puff(Vector3 at, float sh, float sv, float km)
        {
            var s = new PlantPieces.Snapshot { Tick = 40, T0 = 0, TickSeconds = 0.04, Dust = 1 };
            int o = PlantPieces.DustFirst * PlantPieces.Stride;
            for (int k = 0; k <= PlantPieces.SubSteps; k++)
            {
                s.Data[o + 4 * k] = at.x;
                s.Data[o + 4 * k + 1] = at.y;
                s.Data[o + 4 * k + 2] = at.z;
            }
            s.Data[o + 3] = (float)PlantPieces.Kind.Dust;
            s.Data[o + 7] = -1;
            s.Data[o + 11] = 1e9f;
            s.Data[o + 22] = sh;
            s.Data[o + 23] = sh;
            s.Data[o + 24] = sv;
            s.Data[o + 25] = sv;
            s.Data[o + 26] = km;
            s.Data[o + 27] = 0;
            return s;
        }
    }
}
