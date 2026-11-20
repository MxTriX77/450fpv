using System;
using System.Reflection;
using Fpv450.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.App
{
    // w11-world-map 2.1 (QA): the marker follows a moving target. The panel's own OnGUI draws each frame of a known path
    // (a repaint inside an IMGUI container, as an editor panel runs it); the marker must sit where the farmland's transform,
    // worked out here from its extent, puts the target, within one image pixel, its heading within 2° of the target's yaw
    // under acro pitch and roll, and at the image's edge in the target's direction once the target leaves the image.
    public sealed class MapMarkerTests
    {
        const string Farmland = "{\"format\": \"fpv450-topdown\", \"map\": \"farmland\", \"frame\": \"unity-lh-yup\", " +
                                "\"x_m\": [-250, 1250], \"z_m\": [-250, 1250], \"width_px\": 2048, \"height_px\": 2048}";

        [Test]
        public void TheMarkerFollowsAMovingTarget()
        {
            var target = new GameObject("target").transform;
            var host = new GameObject("map");
            MapPanel panel = MapPanel.Add(host, () => "farmland", target);
            typeof(MapPanel).GetProperty(nameof(MapPanel.Map))!.SetValue(panel, MapTransform.Parse(Farmland));
            panel.Toggle();
            try
            {
                int inside = 0, outside = 0;
                var shown = new System.Diagnostics.Stopwatch();
                // 30 s at 60 frames a second: from the village's middle east at 30 m/s and turning, out over the east edge.
                for (int frame = 0; frame < 1800; frame++)
                {
                    float t = frame / 60f, yaw = 90f + 6f * t, pitch = 40f * Mathf.Sin(t), roll = 60f * Mathf.Cos(1.3f * t);
                    var p = new Vector3(500f + 30f * t, 80f, 500f + 200f * Mathf.Sin(t / 5f));
                    target.SetPositionAndRotation(p, Quaternion.Euler(pitch, yaw, roll));
                    shown.Start();
                    Repaint(panel);
                    shown.Stop();
                    Assert.That(panel.Drawn, Is.EqualTo(frame + 1), "drawn every frame");
                    Rect img = panel.ImageRect;
                    float sx = img.width / 2048f, sy = img.height / 2048f;
                    var want = new Vector2(img.x + (p.x + 250f) / 1500f * 2048f * sx, img.y + (1250f - p.z) / 1500f * 2048f * sy);
                    float turn = Mathf.Abs(Mathf.DeltaAngle(panel.HeadingDeg, yaw));
                    Assert.That(turn, Is.LessThanOrEqualTo(2f), "frame " + frame + ": the heading " + panel.HeadingDeg + "° against the yaw " + yaw + "°");
                    if (img.Contains(want) && !panel.OffImage)
                    {
                        inside++;
                        float off = Vector2.Distance(panel.Marker, want) / sx;
                        Assert.That(off, Is.LessThanOrEqualTo(1f), "frame " + frame + " at " + p + ": " + off + " image px from the transform's");
                    }
                    else if (!img.Contains(want))
                    {
                        outside++;
                        Assert.That(panel.OffImage, Is.True, "frame " + frame + " at " + p + ": off the image");
                        Vector2 c = img.center, d = want - c, m = panel.Marker - c;
                        Assert.That(img.Contains(panel.Marker), Is.True, "frame " + frame + ": the edge marker on the image");
                        Assert.That(Mathf.Abs(d.x * m.y - d.y * m.x) / (d.magnitude * m.magnitude), Is.LessThan(1e-3f), "frame " + frame + ": towards the target");
                        Assert.That(Vector2.Dot(d, m), Is.GreaterThan(0f), "frame " + frame + ": on the target's side");
                    }
                }
                panel.Toggle();
                var hidden = System.Diagnostics.Stopwatch.StartNew();
                for (int frame = 0; frame < 1800; frame++) Repaint(panel);
                hidden.Stop();
                TestContext.WriteLine("the image " + panel.ImageRect + " in " + Screen.width + "×" + Screen.height + "; frames on the image " + inside + ", off it " + outside +
                                      "; OnGUI's main-thread time a repaint (indicative, no image): shown " + (shown.Elapsed.TotalMilliseconds / 1800).ToString("0.0000") +
                                      " ms, hidden " + (hidden.Elapsed.TotalMilliseconds / 1800).ToString("0.0000") + " ms");
                Assert.That(inside, Is.GreaterThan(600), "frames on the image");
                Assert.That(outside, Is.GreaterThan(200), "frames off the image");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(target.gameObject);
            }
        }

        // One repaint of the panel's OnGUI inside an IMGUI container (GUIUtility's internal Begin/EndContainer, as
        // UIElements' IMGUIContainer does), so its GUI calls run as in a frame.
        static void Repaint(MapPanel panel)
        {
            const BindingFlags Any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type state = typeof(GUIUtility).Assembly.GetType("UnityEngine.ObjectGUIState", true);
            object s = Activator.CreateInstance(state, true);
            typeof(GUIUtility).GetMethod("BeginContainer", Any, null, new[] { state }, null)!.Invoke(null, new[] { s });
            try
            {
                Event.current = new Event { type = EventType.Repaint };
                typeof(MapPanel).GetMethod("OnGUI", Any)!.Invoke(panel, null);
            }
            finally
            {
                typeof(GUIUtility).GetMethod("EndContainer", Any, null, Type.EmptyTypes, null)!.Invoke(null, null);
                (s as IDisposable)?.Dispose();
            }
        }
    }
}
