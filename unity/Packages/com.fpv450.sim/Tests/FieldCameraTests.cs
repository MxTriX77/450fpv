using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fpv-camera (p7e-camera-feel 1.2): the pose history, the camera's elastic turn and the camera at each field's own time.
    public class FieldCameraTests
    {
        // The field's middle line's read, µs after its start: Feed/Core's RollingShutter.ReferenceUs, (25 + 143 + ½) × 64 µs.
        const long ReferenceUs = 10784;

        static PoseFrame Marked(long step) => new PoseFrame
        {
            Step = step,
            Position = new Double3(step, step, step),
            BodyRate = new Double3(step, -step, step),
            FiberPaidOut = step,
        };

        static bool Whole(in PoseFrame p, long step) =>
            p.Step == step && p.Position.X == step && p.Position.Z == step && p.BodyRate.Y == -step && p.FiberPaidOut == step;

        [Test]
        public void TheHistoryHoldsTheLastStepsByStep()
        {
            var poses = new PoseBuffer();
            Assert.That(poses.LatestStep, Is.EqualTo(-1));
            Assert.That(poses.TryRead(0, out _), Is.False, "nothing before Begin");
            poses.Begin(0, Marked(0), default);
            for (long s = 1; s < 300; s++) poses.Publish(Marked(s), default);
            Assert.That(poses.LatestStep, Is.EqualTo(299));
            for (long s = 300 - PoseBuffer.HistorySteps; s < 300; s++)
            {
                Assert.That(poses.TryRead(s, out PoseFrame p), Is.True, "step " + s);
                Assert.That(Whole(p, s), Is.True, "step " + s);
            }
            Assert.That(poses.TryRead(300 - PoseBuffer.HistorySteps - 1, out _), Is.False, "overwritten");
            Assert.That(poses.TryRead(300, out _), Is.False, "not yet published");
            Assert.That(poses.TryRead(-1, out _), Is.False);

            // A new flight starts at step 0 again and its steps replace the last flight's.
            PoseFrame first = Marked(0);
            first.FiberPaidOut = 0;
            first.Position = new Double3(-7, -7, -7);
            poses.Begin(5000000, first, default);
            Assert.That(poses.LatestStep, Is.EqualTo(0));
            Assert.That(poses.TryRead(0, out PoseFrame again), Is.True);
            Assert.That(again.Position.X, Is.EqualTo(-7));
        }

        // A writer at full speed against a reader of the newest steps: a read the writer laps is refused, never torn.
        [Test]
        public void AReadTheWriterLapsIsRefusedNeverTorn()
        {
            var poses = new PoseBuffer();
            poses.Begin(0, Marked(0), default);
            long written = 0;
            var stop = false;
            var writer = new Thread(() =>
            {
                for (long s = 1; !Volatile.Read(ref stop); s++)
                {
                    poses.Publish(Marked(s), default);
                    Volatile.Write(ref written, s);
                }
            });
            writer.Start();
            long kept = 0, refused = 0;
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (deadline.ElapsedMilliseconds < 300)
            {
                long latest = poses.LatestStep;
                for (long s = latest - PoseBuffer.HistorySteps; s <= latest; s++)
                {
                    if (poses.TryRead(s, out PoseFrame p))
                    {
                        kept++;
                        Assert.That(Whole(p, s), Is.True, "a kept read is the whole pose of the step asked for");
                    }
                    else refused++;
                }
            }
            Volatile.Write(ref stop, true);
            writer.Join();
            TestContext.WriteLine($"{Volatile.Read(ref written)} steps written; {kept} reads kept, {refused} refused");
            Assert.That(kept, Is.GreaterThan(0));
        }

        // The pose carries the frame's turn at the camera; a rigid airframe has none.
        [Test]
        public void ThePoseCarriesTheFramesTurn()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var core = new FlightCore(ShakeScenario.Setup(spec, 1, 1));
            var rigid = new FlightCore(ShakeScenario.Setup(spec, 1, 1, modes: false));
            for (int k = 0; k < 600; k++)
            {
                long us = k * FlightCore.StepUs;
                var stick = new StickSample { HostUs = us, Arm = k >= 8, Throttle = k < 16 ? 0 : 1 };
                core.Advance(stick);
                rigid.Advance(stick);
            }
            core.GetPose(out PoseFrame pose);
            Double3 turn = core.Modes.FrameTurn(core.State.ModeShape);
            Assert.That(pose.CameraTurn.X, Is.EqualTo(turn.X));
            Assert.That(pose.CameraTurn.Y, Is.EqualTo(turn.Y));
            Assert.That(pose.CameraTurn.Z, Is.EqualTo(turn.Z));
            Assert.That(Double3.Dot(turn, turn), Is.GreaterThan(0), "a punch bends the frame");
            rigid.GetPose(out PoseFrame still);
            Assert.That(Double3.Dot(still.CameraTurn, still.CameraTurn), Is.EqualTo(0));
        }

        [Test]
        public void TheFieldIsWholeOnceItsLastStepIsPublished()
        {
            Assert.That(FieldCamera.LatestField(0), Is.EqualTo(-1));
            Assert.That(FieldCamera.LatestField(19), Is.EqualTo(-1));
            Assert.That(FieldCamera.LatestField(20), Is.EqualTo(0));
            Assert.That(FieldCamera.LatestField(39), Is.EqualTo(0));
            Assert.That(FieldCamera.LatestField(40), Is.EqualTo(1));
            Assert.That(FieldCamera.FieldStartUs(3), Is.EqualTo(60000));
        }

        // fpv-camera's "The rendered camera tracks the sim's at field rate" and "The known-wrong camera fails".
        [TestCase(60)]
        [TestCase(144)]
        public void TheRenderedCameraTracksTheSimsAtFieldRate(int fps)
        {
            Track track = Fly(fps);
            TestContext.WriteLine($"{fps} Hz: {track.Fields} fields drawn (skips {track.Skips}); rate spread {track.Spread:F4} rad/s; "
                + $"error: field camera {track.FieldError / track.Spread:P2}, render clock's rigid pose {track.ClockError / track.Spread:P2}, "
                + $"30 ms lag {track.LagError / track.Spread:P2}");
            Assert.That(track.Skips, Is.EqualTo(0), "no field skipped");
            Assert.That(track.Spread, Is.GreaterThan(0.05), "the punch turns the camera");
            Assert.That(track.FieldError, Is.LessThanOrEqualTo(0.02 * track.Spread), "the field camera tracks the sim");
            Assert.That(track.ClockError, Is.GreaterThan(0.2 * track.Spread), "the render clock's rigid pose is caught");
            Assert.That(track.LagError, Is.GreaterThan(0.2 * track.Spread), "a smoothed camera is caught");
        }

        struct Track
        {
            public int Fields, Skips;
            public double Spread, FieldError, ClockError, LagError;
        }

        // The repository's drone (1 kg, its asymmetry and modes, seed 1) hovers 3 s, then punches full throttle for 1 s; a
        // renderer at fps on the flight's clock draws each new whole field. Over the punch, three cameras' field-to-field
        // rates against the sim's: its CameraAngularVelocity integrated over the same interval (Quat.Integrate, step by
        // step, the window's ends by their fractions).
        static Track Fly(int fps)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = ShakeScenario.Setup(spec, 1, 1);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame p0);
            core.GetSignals(out FeedSignals s0);
            poses.Begin(0, p0, s0);

            const int Punch = 3000, End = 4000;
            int steps = End + 40;
            var omega = new Double3[steps + 1]; // the camera's rate over step j - 1 → j, by j
            var rigidAt = new Quat[steps + 1];
            var lag = new Quat[steps + 1];
            Quat smooth = FieldCamera.Attitude(p0);
            lag[0] = smooth;
            rigidAt[0] = p0.Attitude;
            int k = 0;
            // The flight, run on until its newest published step is `until`.
            void FlyTo(long until)
            {
                for (; k < until; k++)
                {
                    long us = core.State.Step * FlightCore.StepUs;
                    StickSample st = k < 16 ? new StickSample { HostUs = us, Arm = k >= 8 } : pilot.Sticks(core, us);
                    if (k >= Punch) st.Throttle = 1;
                    core.Advance(st);
                    core.GetPose(out PoseFrame pose);
                    core.GetSignals(out FeedSignals sig);
                    poses.Publish(pose, sig);
                    int j = (int)pose.Step;
                    omega[j] = sig.CameraAngularVelocity;
                    rigidAt[j] = pose.Attitude;
                    // The known-wrong smoothed camera: a 30 ms first-order lag on the camera's attitude, step by step.
                    Quat q = FieldCamera.Attitude(pose);
                    if (Quat.Dot(smooth, q) < 0) q = new Quat(-q.X, -q.Y, -q.Z, -q.W);
                    double a = FlightCore.Dt / 0.030;
                    smooth = new Quat(smooth.X + (q.X - smooth.X) * a, smooth.Y + (q.Y - smooth.Y) * a, smooth.Z + (q.Z - smooth.Z) * a,
                        smooth.W + (q.W - smooth.W) * a).Normalized();
                    lag[j] = smooth;
                }
            }

            // The renderer: frame n at n / fps s of host time sees the steps due by then (FlightScheduler.RunDue: step
            // ⌊t / 1 ms⌋ + 1 is the newest), and draws the newest whole field when it is new.
            var r = new Track();
            long shown = -1;
            bool have = false;
            Quat lastField = default, lastClock = default, lastLag = default;
            long lastAt = 0;
            double sumF = 0, sumC = 0, sumL = 0;
            var rates = new System.Collections.Generic.List<Double3>();
            var errs = new System.Collections.Generic.List<Double3[]>();
            for (long n = 0; ; n++)
            {
                long hostUs = n * 1000000 / fps;
                long latest = hostUs / FlightCore.StepUs + 1;
                if (latest > steps) break;
                FlyTo(latest);
                long field = FieldCamera.LatestField(latest);
                if (field < 0 || field == shown) continue;
                if (shown >= 0 && field > shown + 1 && FieldCamera.FieldStartUs(field) > Punch * FlightCore.StepUs) r.Skips++;
                shown = field;
                long at = FieldCamera.FieldStartUs(field) + ReferenceUs;
                Assert.That(At(poses, latest, at, out PoseFrame camera), Is.True, "the history holds the field");
                Quat qField = FieldCamera.Attitude(camera);
                Quat qClock = Lerp(rigidAt, hostUs);    // the render clock's interpolated rigid pose
                Quat qLag = Lerp(lag, at);
                if (have && at > Punch * FlightCore.StepUs && at <= End * FlightCore.StepUs)
                {
                    double dt = (at - lastAt) * 1e-6;
                    Double3 truth = Integrated(omega, lastAt, at) * (1 / dt);
                    rates.Add(truth);
                    errs.Add(new[]
                    {
                        Rotation(lastField, qField) * (1 / dt) - truth,
                        Rotation(lastClock, qClock) * (1 / dt) - truth,
                        Rotation(lastLag, qLag) * (1 / dt) - truth,
                    });
                    r.Fields++;
                }
                have = true;
                lastField = qField;
                lastClock = qClock;
                lastLag = qLag;
                lastAt = at;
            }
            Double3 mean = default;
            foreach (Double3 w in rates) mean = mean + w * (1.0 / rates.Count);
            double spread = 0;
            foreach (Double3 w in rates) spread += Double3.Dot(w - mean, w - mean);
            foreach (Double3[] e in errs)
            {
                sumF += Double3.Dot(e[0], e[0]);
                sumC += Double3.Dot(e[1], e[1]);
                sumL += Double3.Dot(e[2], e[2]);
            }
            int m = rates.Count;
            r.Spread = System.Math.Sqrt(spread / m);
            r.FieldError = System.Math.Sqrt(sumF / m);
            r.ClockError = System.Math.Sqrt(sumC / m);
            r.LagError = System.Math.Sqrt(sumL / m);
            return r;
        }

        // The app's read: the history as it stands at the latest step (every step up to it published).
        static bool At(PoseBuffer poses, long latest, long timeUs, out PoseFrame pose)
        {
            Assert.That(poses.LatestStep, Is.GreaterThanOrEqualTo(latest));
            return FieldCamera.At(poses, timeUs, out pose);
        }

        // An attitude series by step, normalised-linear between steps at µs t.
        static Quat Lerp(Quat[] series, long t)
        {
            long j = t / FlightCore.StepUs;
            double f = (t - j * FlightCore.StepUs) / (double)FlightCore.StepUs;
            Quat a = series[j], b = series[System.Math.Min(j + 1, series.Length - 1)];
            if (Quat.Dot(a, b) < 0) b = new Quat(-b.X, -b.Y, -b.Z, -b.W);
            return new Quat(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f, a.W + (b.W - a.W) * f).Normalized();
        }

        // The sim's camera rate integrated from µs a to b: per step j the rate omega[j] over (j - 1, j] ms, the ends by fraction.
        static Double3 Integrated(Double3[] omega, long a, long b)
        {
            Quat q = Quat.Identity;
            for (long j = a / FlightCore.StepUs + 1; (j - 1) * FlightCore.StepUs < b; j++)
            {
                long from = System.Math.Max(a, (j - 1) * FlightCore.StepUs), to = System.Math.Min(b, j * FlightCore.StepUs);
                if (to > from) q = Quat.Integrate(q, omega[j], (to - from) * 1e-6);
            }
            return Vector(q);
        }

        // The rotation from a to b in a's axes, as a rotation vector, rad.
        static Double3 Rotation(Quat a, Quat b) => Vector(a.Conjugate() * b);

        static Double3 Vector(Quat q)
        {
            if (q.W < 0) q = new Quat(-q.X, -q.Y, -q.Z, -q.W);
            double s = System.Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z);
            if (s < 1e-15) return new Double3(2 * q.X, 2 * q.Y, 2 * q.Z);
            double angle = 2 * System.Math.Atan2(s, q.W);
            return new Double3(q.X, q.Y, q.Z) * (angle / s);
        }
    }
}
