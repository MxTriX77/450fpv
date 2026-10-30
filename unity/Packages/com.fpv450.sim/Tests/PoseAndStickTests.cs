using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The interfaces' own rules (design 1): the stick consumption rule, and the pose the renderer interpolates.
    public sealed class PoseAndStickTests
    {
        static StickSample At(long hostUs, double roll) => new StickSample { HostUs = hostUs, Roll = roll, Arm = true };

        [Test]
        public void TheSampleInForceIsTheLatestAtOrBeforeTheStep()
        {
            var inbox = new StickInbox(8);
            inbox.Consume(1000, out StickSample none);
            Assert.That(none.Arm, Is.False, "before any report: neutral and disarmed");
            Assert.That(none.Throttle, Is.EqualTo(0));

            inbox.TryWrite(At(1000, 0.1));
            inbox.TryWrite(At(1500, 0.2));
            inbox.TryWrite(At(2999, 0.3));
            inbox.TryWrite(At(3001, 0.4));
            inbox.Consume(1000, out StickSample a);
            inbox.Consume(2000, out StickSample b);
            inbox.Consume(3000, out StickSample c);
            inbox.Consume(3000, out StickSample again);
            inbox.Consume(4000, out StickSample d);
            Assert.That(a.Roll, Is.EqualTo(0.1), "a report stamped at the step's time is in force at it");
            Assert.That(b.Roll, Is.EqualTo(0.2));
            Assert.That(c.Roll, Is.EqualTo(0.3), "a report from the step's future stays queued");
            Assert.That(again.Roll, Is.EqualTo(0.3));
            Assert.That(d.Roll, Is.EqualTo(0.4));
        }

        [Test]
        public void AFullInboxRefusesAndCounts()
        {
            var inbox = new StickInbox(4);
            for (int i = 0; i < 4; i++) Assert.That(inbox.TryWrite(At(i, 0)), Is.True);
            Assert.That(inbox.TryWrite(At(4, 0)), Is.False);
            Assert.That(inbox.Dropped, Is.EqualTo(1));
            inbox.Consume(10, out _);
            Assert.That(inbox.TryWrite(At(11, 0)), Is.True);
        }

        // A paused flight's drain (p6-input-app, QA's interim finding 1): a failsafe superseded within one drain is still
        // seen, and Hold keeps it in force for the steps before its end while the later reports are taken.
        [Test]
        public void ADrainSeesASupersededFailsafeAndHoldKeepsIt()
        {
            var inbox = new StickInbox(8);
            inbox.TryWrite(At(1000, 0.1));
            Assert.That(inbox.Drain(1000, out _), Is.False, "no failsafe");
            inbox.TryWrite(new StickSample { HostUs = 1500, Failsafe = true });
            inbox.TryWrite(At(1800, 0.2));
            inbox.TryWrite(At(2500, 0.3));
            Assert.That(inbox.Drain(2000, out StickSample lost), Is.True, "superseded by the report at 1800, still seen");
            Assert.That((lost.HostUs, lost.Failsafe, lost.Arm), Is.EqualTo((1500L, true, false)));

            inbox.Hold(lost, 4000);
            inbox.Consume(3000, out StickSample held);
            inbox.TryWrite(At(3500, 0.4));
            inbox.Consume(3999, out StickSample stillHeld);
            inbox.Consume(4000, out StickSample after);
            Assert.That((held.HostUs, held.Failsafe), Is.EqualTo((1500L, true)), "held before its end");
            Assert.That(stillHeld.Failsafe, Is.True);
            Assert.That((after.HostUs, after.Roll, after.Failsafe), Is.EqualTo((3500L, 0.4, false)), "then the latest report taken meanwhile");
        }

        static PosePair Pair(long startUs)
        {
            var a = new PoseFrame { Step = 10, Position = new Double3(1, 2, 3), Attitude = Quat.Identity };
            // 90° of yaw (about +Y), written in the opposite hemisphere: the same rotation as its negation.
            double h = System.Math.Sqrt(0.5);
            var b = new PoseFrame { Step = 11, Position = new Double3(3, 2, 1), Attitude = new Quat(0, -h, 0, -h) };
            return new PosePair { Previous = a, Current = b, StartUs = startUs };
        }

        [Test]
        public void TheRendererInterpolatesByItsOwnClock()
        {
            const long start = 1000000;
            PosePair pair = Pair(start);
            pair.Interpolate(start + 10500, out PoseFrame mid);
            Assert.That(mid.Position.X, Is.EqualTo(2).Within(1e-12));
            Assert.That(mid.Position.Z, Is.EqualTo(2).Within(1e-12));
            // Halfway along the shorter arc: 45° of yaw, not a flip through the long way round.
            Assert.That(mid.Attitude.Y / mid.Attitude.W, Is.EqualTo(System.Math.Tan(System.Math.PI / 8)).Within(1e-12));
            Assert.That(mid.Attitude.Norm(), Is.EqualTo(1).Within(1e-15));

            pair.Interpolate(start + 9000, out PoseFrame early);
            Assert.That(early.Position.X, Is.EqualTo(1), "before Previous: held at Previous");
            pair.Interpolate(start + 50000, out PoseFrame late);
            Assert.That(late.Position.X, Is.EqualTo(3), "past Current: held, never extrapolated");
        }

        [Test]
        public void TheBufferHandsTheReaderTheNewestPair()
        {
            var buffer = new PoseBuffer();
            buffer.Begin(500, new PoseFrame { Step = 0 }, default);
            Assert.That(buffer.Read().Current.Step, Is.EqualTo(0));
            Assert.That(buffer.Read().StartUs, Is.EqualTo(500));
            for (long k = 1; k <= 5; k++) buffer.Publish(new PoseFrame { Step = k }, new FeedSignals { Step = k });
            ref readonly PosePair pair = ref buffer.Read();
            Assert.That(pair.Previous.Step, Is.EqualTo(4));
            Assert.That(pair.Current.Step, Is.EqualTo(5));
            Assert.That(pair.Signals.Step, Is.EqualTo(5));
            Assert.That(buffer.Read().Current.Step, Is.EqualTo(5), "no new pair: the same one again");
        }
    }
}
