using System.IO;
using Fpv450.App;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.UI;
using NUnit.Framework;
using SimSignals = Fpv450.Sim.Flight.FeedSignals;

namespace Fpv450.Tests.App
{
    // The app's screens and player (p6-input-app 1.4b): what of them runs without a scene.
    public sealed class AppTests
    {
        [Test]
        public void ThePlayerCarriesEveryFileTheFlightReads()
        {
            foreach (string file in AppData.PlayerFiles)
                Assert.That(File.Exists(AppData.File(file)), file + " under " + AppData.Root);
        }

        [Test]
        public void TheFeedReadsThePhysicsWhereItIsLive()
        {
            var feed = new FlightFeedSignals();
            var s = new SimSignals
            {
                Live = FeedSignalSet.PackVolts | FeedSignalSet.MotorCurrent | FeedSignalSet.CameraAngularVelocity,
                PackVolts = 23.5,
                MotorCurrentTotalA = 41.2,
                MotorCurrentA = new Corners { RearRight = 10.1, FrontRight = 10.2, RearLeft = 10.3, FrontLeft = 10.6 },
                CameraAngularVelocity = new Double3(0.1, -0.2, 0.3),
                LinkMarginDb = 3, // not live: the stub's
            };
            feed.Update(s, 12.4, 250, 75.3);
            Assert.That(feed.SourceOf(FeedSignal.PackVolts), Is.EqualTo(SignalSource.Physics));
            Assert.That(feed.PackVolts, Is.EqualTo(23.5f));
            Assert.That(feed.MotorCurrentTotalA, Is.EqualTo(41.2f));
            Assert.That(feed.MotorCurrentA(3), Is.EqualTo(10.6f));
            Assert.That(feed.CameraAngularVelocity.Y, Is.EqualTo(-0.2f));
            Assert.That(feed.SourceOf(FeedSignal.LinkMargin), Is.EqualTo(SignalSource.Stub));
            Assert.That(feed.LinkMarginDb, Is.EqualTo(float.PositiveInfinity), "the stub's link, until 7d");
            Assert.That(feed.SourceOf(FeedSignal.OsdState), Is.EqualTo(SignalSource.Stub), "the OSD's layout is the stand-in's until p4-feed 1.5");

            feed.BuildOsd(0);
            string grid = "";
            for (int r = 0; r < FeedOsd.Rows; r++)
            {
                for (int c = 0; c < FeedOsd.Cols; c++) grid += feed.OsdCell(r, c);
                grid += "\n";
            }
            StringAssert.Contains("SVINORIZ 10 OPTO", grid);
            StringAssert.Contains("! SAFE !", grid);
            StringAssert.Contains(">12m", grid, "the altitude from the flight");
            StringAssert.Contains("01:15", grid, "the timer from the flight");
            StringAssert.Contains("0.25", grid, "the distance home, km");
            StringAssert.Contains("23.5", grid, "the pack from the physics");
            StringAssert.Contains("41.20", grid, "the current from the physics");
        }

        [Test]
        public void ASlowRadioIsNamedAfterFinding13()
        {
            Assert.That(CalibrateScreen.SlowRadioUs, Is.EqualTo(4375), "EdgeTX's floor gain on the jump rule's fastest accepted sweep");
            Assert.That(CalibrateScreen.SlowRadioNote(2000), Is.Null, "a TX12 in joystick mode, every 2 ms");
            Assert.That(CalibrateScreen.SlowRadioNote(4000), Is.Null);
            Assert.That(CalibrateScreen.SlowRadioNote(6667), Is.EqualTo("The radio updates its USB output only every 6.7 ms (an RF module is on): switch it off for the sim."));
            StringAssert.Contains("every 20 ms", CalibrateScreen.SlowRadioNote(20000));
        }

        [Test]
        public void TheSetupClockReadsHoursAndMinutes()
        {
            Assert.That(SetupScreen.Clock(0), Is.EqualTo("00:00"));
            Assert.That(SetupScreen.Clock(17.5), Is.EqualTo("17:30"));
            Assert.That(SetupScreen.Clock(23 + 59 / 60.0), Is.EqualTo("23:59"));
        }
    }
}
