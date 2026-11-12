using Fpv450.App;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The farmland's benchmark path (w6-farmland-look 1.2, PLT-07): long enough for PLT-07's 30 s measured at its speed,
    /// over the map tile, through the village's circle, low and high.
    /// </summary>
    public sealed class FarmlandBenchTests
    {
        [Test]
        public void ThePathMeasuresAtLeastThirtySecondsOverTheMap()
        {
            float length = 0f;
            bool village = false;
            foreach (FarmlandBench.Waypoint w in FarmlandBench.Path)
            {
                Assert.That(w.X, Is.InRange(0f, 1000f));
                Assert.That(w.Z, Is.InRange(0f, 1000f));
                Assert.That(w.Height, Is.InRange(2f, 50f));
                village |= new Vector2(w.X - 512f, w.Z - 600f).magnitude < 75f; // maps/farmland/outlines.json
            }
            for (int i = 0; i + 1 < FarmlandBench.Path.Length; i++)
                length += new Vector2(FarmlandBench.Path[i + 1].X - FarmlandBench.Path[i].X, FarmlandBench.Path[i + 1].Z - FarmlandBench.Path[i].Z).magnitude;
            Assert.That(length / FarmlandBench.Speed, Is.GreaterThanOrEqualTo(30f), "PLT-07: 30 s measured");
            Assert.That(village, Is.True, "a waypoint inside the village's circle");
        }
    }
}
