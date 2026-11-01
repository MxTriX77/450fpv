using System.Collections.Generic;
using Fpv450.App;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Tests.App
{
    // fpv-camera (p7e-camera-feel 1.6): the FPV view's field latch. A render clock at 60 or 144 Hz on the flight's clock
    // (the newest published step at host time t is ⌊t / 1 ms⌋ + 1, FlightScheduler.RunDue) draws every whole field once,
    // in order, and holds the picture on every other frame; the pose each field is drawn at is FieldCamera's (sim tests:
    // TheRenderedCameraTracksTheSimsAtFieldRate).
    public class FieldLatchTests
    {
        [TestCase(60)]
        [TestCase(144)]
        public void TheFpvViewDrawsEachWholeFieldOnce(int fps)
        {
            long shown = -1, latest = 0;
            var drawn = new List<long>();
            int frames = 2 * fps, held = 0;
            for (long n = 0; n < frames; n++)
            {
                latest = n * 1000000 / fps / FlightCore.StepUs + 1;
                long field = FlightScene.NewField(latest, shown);
                if (field < 0)
                {
                    held++;
                    continue;
                }
                drawn.Add(field);
                shown = field;
            }
            TestContext.WriteLine($"{fps} Hz: {drawn.Count} fields drawn in {frames} frames, {held} held");
            for (int i = 0; i < drawn.Count; i++) Assert.That(drawn[i], Is.EqualTo(i), "every field in order, none skipped");
            Assert.That(drawn.Count, Is.EqualTo(FieldCamera.LatestField(latest) + 1));
            Assert.That(held, Is.EqualTo(frames - drawn.Count));
            Assert.That(FlightScene.NewField(latest, shown), Is.EqualTo(-1), "the field shown is held");
        }
    }
}
