using Fpv450.App.Session;
using Fpv450.UI;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.App
{
    // w11-world-map 1.2: the map panel's transform (1.1's JSON), its size in the window and its edge marker.
    public sealed class MapPanelTests
    {
        // The farmland's transform as TopDownMaps writes it (x and z [−250, 1250] m at 2048²).
        const string Farmland = "{\"format\": \"fpv450-topdown\", \"map\": \"farmland\", \"frame\": \"unity-lh-yup\", " +
                                "\"x_m\": [-250, 1250], \"z_m\": [-250, 1250], \"width_px\": 2048, \"height_px\": 2048}";

        // The corners land on the image's corners, north up and east right; the village's start where 1.1's check found its
        // marker (u = 816.5 / 1500 · 2048, v = 681 / 1500 · 2048); headings along (sin ψ, −cos ψ).
        [Test]
        public void TheTransformIsTopDownMaps()
        {
            MapTransform t = MapTransform.Parse(Farmland);
            Assert.That((t.Map, t.WidthPx, t.HeightPx), Is.EqualTo(("farmland", 2048, 2048)));
            Assert.That(Vector2.Distance(t.Pixel(-250f, 1250f), Vector2.zero), Is.LessThan(1e-3f), "north-west: the top left");
            Assert.That(Vector2.Distance(t.Pixel(1250f, -250f), new Vector2(2048f, 2048f)), Is.LessThan(1e-3f), "south-east: the bottom right");
            StartPoint s = FlightWorlds.FarmlandStart;
            Vector2 start = t.Pixel((float)s.X, (float)s.Z);
            Assert.That(Vector2.Distance(start, new Vector2(816.5f / 1500f * 2048f, 681f / 1500f * 2048f)), Is.LessThan(1e-2f), "the village's start: " + start);
            Assert.That(Vector2.Distance(MapTransform.Direction(0f), new Vector2(0f, -1f)), Is.LessThan(1e-6f), "north: up");
            Assert.That(Vector2.Distance(MapTransform.Direction(90f), new Vector2(1f, 0f)), Is.LessThan(1e-6f), "east: right");
            Assert.That(Vector2.Distance(MapTransform.Direction(180f), new Vector2(0f, 1f)), Is.LessThan(1e-6f), "south: down");
            Assert.Throws<System.FormatException>(() => MapTransform.Parse("{\"format\": \"other\"}"));
        }

        // The pilot: "a little bit smaller than whole game window". The square map fits the window less 6 % of its shorter
        // side on every side, centred, at 1920×1080, 2560×1440 and a small window.
        [Test]
        public void ThePanelIsCentredInsideTheWindow()
        {
            foreach (Vector2 w in new[] { new Vector2(1920, 1080), new Vector2(2560, 1440), new Vector2(1280, 720), new Vector2(800, 600) })
            {
                Rect r = MapPanel.Layout(w.x, w.y, 1f);
                float margin = Mathf.Max(24f, 0.06f * Mathf.Min(w.x, w.y));
                Assert.That(r.yMin, Is.EqualTo(margin).Within(1f), w + ": the top margin");
                Assert.That(w.y - r.yMax, Is.EqualTo(margin).Within(1f), w + ": the bottom margin");
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(margin - 1f), w + ": the left margin");
                Assert.That(Mathf.Abs(r.xMin - (w.x - r.xMax)), Is.LessThanOrEqualTo(1f), w + ": centred");
                Assert.That(r.width, Is.EqualTo(r.height).Within(1f), w + ": the map's aspect");
            }
            Assert.That(MapPanel.Layout(1920, 1080, 1f), Is.EqualTo(new Rect(485, 65, 950, 950)));
        }

        // Off the image the marker sits at the image's edge, on the line from its middle towards the drone.
        [Test]
        public void OffTheImageTheMarkerSitsAtTheEdge()
        {
            var r = new Rect(100, 50, 400, 400);
            Assert.That(MapPanel.Edge(r, new Vector2(300, 250), 10), Is.EqualTo(new Vector2(300, 250)), "inside: where it is");
            Assert.That(Vector2.Distance(MapPanel.Edge(r, new Vector2(900, 250), 10), new Vector2(490, 250)), Is.LessThan(1e-3f), "east: the right edge");
            Assert.That(Vector2.Distance(MapPanel.Edge(r, new Vector2(300, -950), 10), new Vector2(300, 60)), Is.LessThan(1e-3f), "north: the top edge");
            Vector2 ne = MapPanel.Edge(r, new Vector2(1300, -750), 10);
            Assert.That(Vector2.Distance(ne, new Vector2(490, 60)), Is.LessThan(1e-3f), "north-east, along the diagonal: the corner");
        }
    }
}
