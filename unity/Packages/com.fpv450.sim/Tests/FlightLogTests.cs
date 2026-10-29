using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // physics-log (LOG-01, LOG-02, LOG-05): "Replay matches the flight" and "A truncated log still reads" on the
    // bootstrap drone with its flight controller and propulsion (1.4), its drag in the Windy wind (1.5; the field, p7c-wind 1.4),
    // launched into the air and flown by a scripted pilot, through the scheduler, the ring and the flush worker; the refusals, a divergence named by step and field,
    // a full ring that waits and logs a hitch, the header's round trip, and the guards that keep the record and the
    // header covering the state and the setup as later tasks extend them.
    public sealed class FlightLogTests
    {
        const long StartUs = 1000000;
        const long TickUs = 250;
        const long StallAtUs = StartUs + 700000, StallUs = 30000;

        sealed class VirtualClock : IHostClock
        {
            public long Now;
            public long NowUs() => Now;
        }

        static FlightWorld OpenWorld(string id) => id == "floor" ? FlightWorld.Floor() : null;

        static FlightSetup Setup(DroneSpec spec) => new FlightSetup
        {
            Seed = 0x450F,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            Propulsion = spec.BuildPropulsion(),
            Aero = spec.BuildAero(),
            Wind = WindSetup.Of(WindLevel.Windy),
            StartPosition = new Double3(0, 100, 0),
            StartVelocity = new Double3(3, 20, -1),
            StartBodyRate = new Double3(2, -1, 0.5),
            StartPackCharge = 0.8,
        };

        static LogHeader Header(FlightSetup setup, FlightCore core, DroneSpec spec)
        {
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, spec.Nominal);
            return h;
        }

        // A ~250 Hz report stream with jitter, armed from the 50th report with the throttle low (Betaflight's rule), every
        // axis moving from the 60th.
        static StickSample Report(int i, long hostUs) => new StickSample
        {
            HostUs = hostUs,
            Roll = (i * 37 % 1001 - 500) / 500.0,
            Pitch = (i * 53 % 1001 - 500) / 500.0,
            Yaw = (i * 71 % 1001 - 500) / 500.0,
            Throttle = i < 60 ? 0 : i * 29 % 1001 / 1000.0,
            Arm = i >= 50,
        };

        // Flies the scripted flight for `steps` steps through the scheduler on a virtual clock, with one 30 ms process
        // stall at 0.7 s (a hitch), into a recorder on stream; returns its header.
        static LogHeader Record(Stream stream, long steps, Action<LogHeader> edit = null, int capacity = FlightRecorder.DefaultCapacity)
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            LogHeader header = Header(setup, core, spec);
            edit?.Invoke(header);
            var recorder = new FlightRecorder(stream, header, capacity, ownsStream: false);
            var clock = new VirtualClock { Now = StartUs };
            var inbox = new StickInbox();
            var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), clock, recorder);
            scheduler.Begin(StartUs);
            long report = StartUs - 2000;
            for (int i = 0, t = 0; core.State.Step < steps; t++)
            {
                long now = StartUs + t * TickUs;
                clock.Now = now;
                for (; report <= now; i++)
                {
                    inbox.TryWrite(Report(i, report));
                    report += 3700 + i * 131 % 600;
                }
                if (!(now >= StallAtUs && now < StallAtUs + StallUs)) scheduler.RunDue(now);
            }
            recorder.Close(inbox.Dropped);
            Assert.That(recorder.Fault, Is.Null);
            return header;
        }

        // The byte offset of step's record block in a log.
        static long BlockOf(byte[] log, long step)
        {
            using (var reader = new FlightLogReader(new MemoryStream(log)))
            {
                while (reader.Next())
                    if (reader.Kind == LogFormat.StepBlock && LogFormat.StepOf(reader.Payload) == step)
                        return reader.Position;
            }
            Assert.Fail("no record of step " + step);
            return -1;
        }

        static byte[] Head(byte[] log, long length)
        {
            var cut = new byte[length];
            Array.Copy(log, cut, length);
            return cut;
        }

        [Test]
        public void ReplayMatchesTheFlight()
        {
            // 120 s of 1300 B steps: 156 MB, kept in memory.
            var log = new MemoryStream();
            Record(log, 120000);
            DroneSpec spec = BootstrapDrone.Spec();
            Assert.That(log.Length, Is.GreaterThan(120000L * (LogFormat.BlockHeaderSize + LogFormat.StepSize)));

            log.Position = 0;
            ReplayResult r = FlightReplay.Run(log, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(120000), "every step record replayed");

            log.Position = 0;
            FlightSummary summary = FlightSummary.Read(log);
            Assert.That(summary.Complete, Is.True);
            Assert.That(summary.Gaps, Is.EqualTo(0));
            // The stall's Late hitch; the virtual clock runs unpaced, so a slow flush worker (Unity's Mono) may also
            // fill the ring, and each wait for it is a LogFull hitch with nothing dropped.
            Assert.That(summary.Events.FindAll(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Late).Count,
                Is.EqualTo(1), "the stall's hitch");
            Assert.That(summary.Count(EventKind.Hitch), Is.EqualTo(1 + summary.End.LogWaits), "every other hitch is a wait for the ring");
            Assert.That(summary.End.Steps, Is.EqualTo(120000));
            Assert.That(summary.Last.State.Armed, Is.True, "the flight armed and flew");
            Assert.That(summary.Last.State.RotorSpeed.RearRight, Is.GreaterThan(100), "the motors turn");
            Assert.That(summary.Last.State.PackCharge0, Is.LessThan(0.8), "the packs drained");
            Assert.That(summary.Count(EventKind.MixerSaturation), Is.GreaterThan(0), "the random sticks saturate the mixer");

            // One motor 1 g heavier: a valid drone.json of another hash, refused before the first step.
            string text = BootstrapDrone.Text();
            const string motor = "\"mass_kg\": {\"value\": 0.130";
            int at = text.IndexOf(motor, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            string changed = text.Substring(0, at) + motor + "1" + text.Substring(at + motor.Length);
            ulong changedHash = DroneSpec.Parse(changed).ContentHash;
            Assert.That(changedHash, Is.Not.EqualTo(spec.ContentHash));

            log.Position = 0;
            ReplayResult refused = FlightReplay.Run(log, OpenWorld, changedHash);
            Assert.That(refused.Outcome, Is.EqualTo(ReplayOutcome.Refused), refused.Message);
            Assert.That(refused.Steps, Is.EqualTo(0));
            StringAssert.Contains("drone.json differs", refused.Message);
            StringAssert.Contains(spec.ContentHash.ToString("X16"), refused.Message, "names the log's hash");
            StringAssert.Contains(changedHash.ToString("X16"), refused.Message, "names the replay's hash");
        }

        [Test]
        public void ATruncatedLogStillReads()
        {
            var full = new MemoryStream();
            Record(full, 3000);
            byte[] bytes = full.ToArray();
            FlightSummary whole = FlightSummary.Read(new MemoryStream(bytes));
            Assert.That(whole.Complete, Is.True);
            Assert.That(whole.Steps, Is.EqualTo(3000));
            Assert.That(whole.CutAt, Is.EqualTo(-1));

            // The record the cut logs end on, from the whole log.
            var reader = new FlightLogReader(new MemoryStream(bytes));
            var lastWhole = new byte[LogFormat.StepSize];
            while (reader.Next())
                if (reader.Kind == LogFormat.StepBlock && LogFormat.StepOf(reader.Payload) == 1233)
                    reader.Payload.CopyTo(lastWhole);

            long block = BlockOf(bytes, 1234);
            var cuts = new[]
            {
                (at: block + LogFormat.BlockHeaderSize + 100, reason: "cut inside a step block, 100 of its " + LogFormat.StepSize + " bytes"),
                (at: block + 2, reason: "cut inside a block's header"),
                (at: block, reason: "ends after a whole block, without its end block"),
            };
            foreach (var cut in cuts)
            {
                FlightSummary s = FlightSummary.Read(new MemoryStream(Head(bytes, cut.at)));
                Assert.That(s.Header, Is.Not.Null);
                Assert.That(s.Complete, Is.False, cut.reason);
                Assert.That(s.CutAt, Is.EqualTo(block), "where it was cut: the first byte of the incomplete record");
                StringAssert.Contains(cut.reason, s.CutReason);
                Assert.That(s.Steps, Is.EqualTo(1233), "up to the last whole record");
                Assert.That(s.Last.State.Step, Is.EqualTo(1233));
                var mine = new byte[LogFormat.StepSize];
                LogFormat.EncodeStep(mine, s.Last.State, s.Last.Stick, s.Last.Trace);
                Assert.That(mine, Is.EqualTo(lastWhole), "the last whole record reads bit for bit");
                Assert.That(s.Count(EventKind.Hitch), Is.EqualTo(1), "the stall's hitch at 0.7 s is before the cut");

                var text = new StringWriter();
                s.Print(text);
                StringAssert.Contains("CUT at byte " + block, text.ToString());
                StringAssert.Contains("step 1233", text.ToString());

                ReplayResult r = FlightReplay.Run(new MemoryStream(Head(bytes, cut.at)), OpenWorld, BootstrapDrone.Spec().ContentHash);
                Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.MatchCut), r.Message);
                Assert.That(r.Steps, Is.EqualTo(1233));
            }

            // Cut inside the header: no flight to read, and the summary says where.
            FlightSummary none = FlightSummary.Read(new MemoryStream(Head(bytes, 100)));
            Assert.That(none.Header, Is.Null);
            StringAssert.Contains("cut inside its header, at byte 100", none.HeaderProblem);
        }

        [Test]
        public void AnotherWorldModelOrSettingsIsRefused()
        {
            ulong drone = BootstrapDrone.Spec().ContentHash;
            var log = new MemoryStream();
            Record(log, 200);
            byte[] bytes = log.ToArray();

            var raised = new FlightWorld(new FloorWorld(0.5, "belt_bare", "steel"));
            ReplayResult world = FlightReplay.Run(new MemoryStream(bytes), id => raised, drone);
            Assert.That(world.Outcome, Is.EqualTo(ReplayOutcome.Refused), world.Message);
            StringAssert.Contains("world differs", world.Message);
            StringAssert.Contains(FlightWorld.Floor().ContentHash.ToString("X16"), world.Message);
            StringAssert.Contains(raised.ContentHash.ToString("X16"), world.Message);

            ReplayResult unknown = FlightReplay.Run(new MemoryStream(bytes), id => null, drone);
            Assert.That(unknown.Outcome, Is.EqualTo(ReplayOutcome.Refused));
            StringAssert.Contains("cannot open the world 'floor'", unknown.Message);

            // The first header entry is the flight's: a u16 tag, a u32 length, then the model version.
            byte[] model = (byte[])bytes.Clone();
            Assert.That(BitConverter.ToUInt16(model, LogHeader.PrologueSize), Is.EqualTo(1));
            model[LogHeader.PrologueSize + 6] = 99;
            ReplayResult other = FlightReplay.Run(new MemoryStream(model), OpenWorld, drone);
            Assert.That(other.Outcome, Is.EqualTo(ReplayOutcome.Refused), other.Message);
            StringAssert.Contains("model version 99", other.Message);

            var settings = new MemoryStream();
            Record(settings, 10, h =>
            {
                var list = new List<SettingValue>(h.Settings) { new SettingValue("no_such_setting", SettingValue.Double, 0) };
                h.Settings = list.ToArray();
            });
            settings.Position = 0;
            ReplayResult fc = FlightReplay.Run(settings, OpenWorld, drone);
            Assert.That(fc.Outcome, Is.EqualTo(ReplayOutcome.Refused), fc.Message);
            StringAssert.Contains("no_such_setting", fc.Message);

            ReplayResult garbage = FlightReplay.Run(new MemoryStream(Encoding.ASCII.GetBytes("not a flight log at all")), OpenWorld, drone);
            Assert.That(garbage.Outcome, Is.EqualTo(ReplayOutcome.Unreadable));
        }

        [Test]
        public void ADivergenceNamesTheStepAndField()
        {
            var log = new MemoryStream();
            Record(log, 1000);
            byte[] bytes = log.ToArray();
            int field = Array.FindIndex(LogFormat.StepFields, f => f.Name == "attitude.y");
            long at = BlockOf(bytes, 777) + LogFormat.BlockHeaderSize + LogFormat.StepFields[field].Offset;
            bytes[at] ^= 1; // the last bit of the mantissa

            ReplayResult r = FlightReplay.Run(new MemoryStream(bytes), OpenWorld, BootstrapDrone.Spec().ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Diverged), r.Message);
            Assert.That(r.Step, Is.EqualTo(777));
            Assert.That(r.Field, Is.EqualTo("attitude.y"));
            Assert.That(r.Steps, Is.EqualTo(776), "matched up to the step before");
            StringAssert.Contains("diverged at step 777", r.Message);
        }

        // A stream whose writes block while its gate is shut: a stalled disk.
        sealed class GateStream : MemoryStream
        {
            readonly ManualResetEventSlim _open = new ManualResetEventSlim(true);

            public void Shut() => _open.Reset();

            public void Open() => _open.Set();

            public override void Write(byte[] buffer, int offset, int count)
            {
                _open.Wait();
                base.Write(buffer, offset, count);
            }
        }

        [Test]
        public void AFullRingWaitsAndLogsAHitch()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var disk = new GateStream();
            var recorder = new FlightRecorder(disk, Header(setup, core, spec), FlightRecorder.MinCapacity, ownsStream: false);
            disk.Shut(); // a 16 KiB ring holds 17 steps

            var opener = new Thread(() =>
            {
                Thread.Sleep(100);
                disk.Open();
            });
            opener.Start();
            for (int k = 0; k < 2000; k++)
            {
                StickSample stick = Report(k, StartUs + k * FlightCore.StepUs);
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            opener.Join();
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(recorder.Counts.LogWaits, Is.GreaterThanOrEqualTo(1), "the flight thread waited for the stalled disk");

            disk.Position = 0;
            FlightSummary s = FlightSummary.Read(disk);
            Assert.That(s.Steps, Is.EqualTo(2000), "nothing dropped");
            Assert.That(s.Gaps, Is.EqualTo(0));
            Assert.That(s.Count(EventKind.Hitch), Is.EqualTo(recorder.Counts.LogWaits));
            LoggedEvent hitch = s.Events.Find(e => e.Event.Kind == EventKind.Hitch);
            Assert.That(hitch.Event.Index, Is.EqualTo((byte)HitchCause.LogFull));
            Assert.That(hitch.Event.A, Is.GreaterThanOrEqualTo(1), "the 1 ms waits");

            disk.Position = 0;
            ReplayResult r = FlightReplay.Run(disk, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
        }

        [Test]
        public void TheHeaderKeepsTheSetupAndSimDataOnly()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            FcSettings fc = FcSettings.Default;
            fc.p_roll = 66.5;
            fc.airmode = false;
            fc.iterm_relax = ItermRelax.RPY_INC;
            setup.Settings = fc;
            var core = new FlightCore(setup);
            SeedStream stream = core.Random.Open("test.parameter");
            double drawn = core.Random.Draw(ref stream, "test.parameter", 0.9, 1.1);
            LogHeader h = Header(setup, core, spec);

            byte[] bytes = h.Encode();
            Assert.That(h.Length, Is.EqualTo(bytes.Length));
            LogHeader back = LogHeader.Read(new MemoryStream(bytes));
            Assert.That(back.Unknown, Is.Empty);
            Assert.That(back.LayoutsDiffer(out _), Is.False);
            Assert.That(back.Seed, Is.EqualTo(setup.Seed));
            Assert.That(back.ModelVersion, Is.EqualTo(LogFormat.ModelVersion));
            Assert.That((back.Build, back.Runtime, back.Isa), Is.EqualTo(("simtests", LogHeader.CurrentRuntime(), "test")));
            Assert.That((back.WorldId, back.WorldHash, back.QueryVersion), Is.EqualTo(("floor", setup.World.ContentHash, setup.World.QueryVersion)));
            Assert.That(back.SurfaceName(1), Is.EqualTo("belt_bare"));
            Assert.That(back.MaterialName(1), Is.EqualTo("steel"));
            Assert.That((back.DroneHash, back.DroneName), Is.EqualTo((spec.ContentHash, spec.Name)));
            Assert.That(back.Loadout, Is.EqualTo(spec.Nominal));
            Assert.That((back.StartPosition, back.StartAttitude, back.StartVelocity, back.StartBodyRate),
                Is.EqualTo((setup.StartPosition, setup.StartAttitude, setup.StartVelocity, setup.StartBodyRate)));
            Assert.That(back.Streams, Is.EqualTo(new[] { "wind.direction", "wind.strength", "wind.meander", "wind.shift", "wind.turbulence", "test.parameter" }));
            Assert.That(back.Draws.Length, Is.EqualTo(3), "the wind's two and the test's");
            for (int i = 0; i < 2; i++) Assert.That(back.Draws[i], Is.EqualTo(core.Random.Draws[i]), "wind draw " + i);
            ParameterDraw last = back.Draws[2];
            Assert.That((last.Parameter, last.Lo, last.Hi, last.Value, last.Index), Is.EqualTo(("test.parameter", 0.9, 1.1, drawn, 0UL)));

            // The air: the drag spec field for field, and the wind's model, level and preset.
            foreach (string f in LogHeader.AeroFieldsLogged)
            {
                FieldInfo info = typeof(AeroSpec).GetField(f);
                Assert.That(info.GetValue(back.Aero), Is.EqualTo(info.GetValue(setup.Aero)), "aero." + f);
            }
            Assert.That((back.WindModel, back.Wind), Is.EqualTo(("field", setup.Wind)));
            Assert.That(back.WindParameters, Is.EqualTo(LogHeader.PresetOf(WindLevel.Windy)));
            Assert.That(back.WindParameters.Length, Is.EqualTo(24), "every field of the preset but its level");

            var problems = new List<string>();
            back.ReadSettings(out FcSettings read, problems);
            Assert.That(problems, Is.Empty);
            Assert.That(LogHeader.SettingsOf(read), Is.EqualTo(LogHeader.SettingsOf(fc)), "every setting, bit for bit");

            Assert.That(back.Parts.Length, Is.EqualTo(setup.Mass.PartCount));
            for (int i = 0; i < back.Parts.Length; i++)
                Assert.That(back.Parts[i], Is.EqualTo(setup.Mass.Part(i)), "part " + i);
            Assert.That(back.Body, Is.EqualTo(core.Body));
            Assert.That(new MassModel(back.Parts).Properties, Is.EqualTo(core.Body), "the logged parts rebuild the body");

            // The propulsion, every field and curve point, and the packs' start charge.
            Assert.That(back.StartPackCharge, Is.EqualTo(setup.StartPackCharge));
            PropulsionSpec a = setup.Propulsion, b = back.Propulsion;
            Assert.That(b, Is.Not.Null);
            Assert.That(b.Motors, Is.EqualTo(a.Motors));
            foreach (string f in LogHeader.PropsFieldsLogged)
            {
                FieldInfo info = typeof(DroneProps).GetField(f);
                Assert.That(info.GetValue(b.Props), Is.EqualTo(info.GetValue(a.Props)), "props." + f);
            }
            for (int k = 0; k < 2; k++)
                foreach (string f in LogHeader.PackFieldsLogged)
                {
                    FieldInfo info = typeof(DronePack).GetField(f);
                    Assert.That(info.GetValue(b.Packs[k]), Is.EqualTo(info.GetValue(a.Packs[k])), "packs[" + k + "]." + f);
                }

            // LOG-05: no user, machine or path in the log.
            string text = Encoding.UTF8.GetString(bytes);
            foreach (string personal in new[]
                     {
                         Environment.UserName, Environment.MachineName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         Path.GetTempPath(), Directory.GetCurrentDirectory(),
                     })
                if (!string.IsNullOrEmpty(personal) && personal.Length > 2)
                    StringAssert.DoesNotContain(personal, text);
        }

        [Test]
        public void TheRecordsCoverTheStateAndTheSetup()
        {
            Assert.That(InstanceFields(typeof(FlightState)), Is.EquivalentTo(LogFormat.StateFieldsLogged),
                "a new FlightState field is logged: extend the step record (a new LogFormat.Version)");
            Assert.That(InstanceFields(typeof(StickSample)), Is.EquivalentTo(LogFormat.StickFieldsLogged));
            Assert.That(InstanceFields(typeof(StepTrace)), Is.EquivalentTo(LogFormat.TraceFieldsLogged));
            Assert.That(InstanceFields(typeof(FlightSetup)), Is.EquivalentTo(LogHeader.SetupFieldsLogged),
                "a new FlightSetup field goes into the header, so a replay rebuilds it");
            Assert.That(InstanceFields(typeof(DronePart)), Is.EquivalentTo(LogHeader.PartFieldsLogged));
            Assert.That(InstanceFields(typeof(FcState)), Is.EquivalentTo(LogFormat.FcFieldsLogged),
                "a new FcState field is logged: extend the step record (a new LogFormat.Version)");
            Assert.That(InstanceFields(typeof(Axes3)), Is.EquivalentTo(new[] { "Roll", "Pitch", "Yaw" }));
            Assert.That(InstanceFields(typeof(PropulsionSpec)), Is.EquivalentTo(LogHeader.PropulsionFieldsLogged));
            Assert.That(InstanceFields(typeof(DroneMotor)), Is.EquivalentTo(LogHeader.MotorFieldsLogged));
            Assert.That(InstanceFields(typeof(DroneProps)), Is.EquivalentTo(LogHeader.PropsFieldsLogged));
            Assert.That(InstanceFields(typeof(DronePack)), Is.EquivalentTo(LogHeader.PackFieldsLogged));
            Assert.That(InstanceFields(typeof(AeroSpec)), Is.EquivalentTo(LogHeader.AeroFieldsLogged));
            Assert.That(InstanceFields(typeof(WindSetup)), Is.EquivalentTo(LogHeader.WindFieldsLogged));

            foreach ((LogField[] fields, int size) in new[] { (LogFormat.StepFields, LogFormat.StepSize), (LogFormat.EventFields, LogFormat.EventSize),
                         (LogFormat.EndFields, LogFormat.EndSize) })
            {
                int at = 0;
                foreach (LogField f in fields)
                {
                    Assert.That(f.Offset, Is.EqualTo(at), f.Name + " follows the field before it");
                    at += f.Size;
                }
                Assert.That(at, Is.EqualTo(size));
            }

            // Every field of a step and an event, each a distinct value, through the encoder and back.
            var s = new FlightState
            {
                Step = 123456789, Position = new Double3(1.5, -2.25, 3.125), Velocity = new Double3(4.5, 5.5, -6.5),
                Attitude = new Quat(0.1, -0.2, 0.3, 0.9), BodyRate = new Double3(7, 8, -9), Armed = true,
                RotorSpeed = new Corners { RearRight = 10, FrontRight = 11, RearLeft = 12, FrontLeft = 13 },
                EscCommand = new Corners { RearRight = 0.14, FrontRight = 0.15, RearLeft = 0.16, FrontLeft = 0.17 },
                PackCharge0 = 0.18, PackCharge1 = 0.19, EscLimited = 0x9,
            };
            // Every FcState double a distinct value through reflection, its flags every bit.
            object fc = default(FcState);
            double v = 100;
            foreach (FieldInfo f in typeof(FcState).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(double)) f.SetValue(fc, v++);
                else if (f.FieldType == typeof(Axes3)) f.SetValue(fc, new Axes3 { Roll = v++, Pitch = v++, Yaw = v++ });
                else if (f.FieldType == typeof(FcFlags)) f.SetValue(fc, (FcFlags)0x3FF);
                else if (f.FieldType == typeof(byte)) f.SetValue(fc, (byte)v++);
                else Assert.Fail("FcState." + f.Name + " has a type the log does not encode");
            }
            s.Fc = (FcState)fc;
            Assert.That(v, Is.EqualTo(100 + 91 + 3), "the record's 91 controller doubles and 3 bytes");
            var k = new StickSample { HostUs = -987654321, Roll = -0.2, Pitch = 0.21, Yaw = -0.22, Throttle = 0.23, Arm = false };
            var t = new StepTrace
            {
                RotorCommand = new Corners { RearRight = 0.24, FrontRight = 0.25, RearLeft = 0.26, FrontLeft = 0.27 },
                PackVolts0 = 24.8, PackVolts1 = 24.9, PackAmps0 = 30.1, PackAmps1 = 30.2,
                NetForce = new Double3(31, -32, 33), NetMoment = new Double3(-0.34, 0.35, -0.36),
                AeroForce = new Double3(-1.5, 2.5, 3.5), AeroMoment = new Double3(0.04, -0.05, 0.06),
                Wind = new Double3(0.5, -0.25, 0.75),
                RotorWind = new Corners3
                {
                    RearRight = new Double3(1.25, -0.5, 2), FrontRight = new Double3(-1.75, 0.125, 2.5), RearLeft = new Double3(3, 0.0625, -3.5),
                    FrontLeft = new Double3(-4, 0.375, 4.5),
                },
            };
            var bytes = new byte[LogFormat.StepSize];
            LogFormat.EncodeStep(bytes, s, k, t);
            LogFormat.DecodeStep(bytes, out FlightState s2, out StickSample k2, out StepTrace t2);
            Assert.That(s2, Is.EqualTo(s));
            Assert.That(k2, Is.EqualTo(k));
            Assert.That(t2, Is.EqualTo(t));
            LogFormat.DecodeStick(bytes, out StickSample k3);
            Assert.That(k3, Is.EqualTo(k));

            var e = new FlightEvent
            {
                Kind = EventKind.Crash, Part = 7, Index = 2, Object = 1234, Shape = 5, Material = 6, Surface = 3,
                Point = new Double3(1, 2, 3), A = 4.5, B = 6.5, C = 490.3,
            };
            var eb = new byte[LogFormat.EventSize];
            LogFormat.EncodeEvent(eb, 42, e);
            LogFormat.DecodeEvent(eb, out long step, out FlightEvent e2);
            Assert.That((step, e2), Is.EqualTo((42L, e)));
        }

        // A recorder handed a step's events directly, until the producers exist (1.4, 1.6).
        [Test]
        public void EventsAreLoggedAndReplayed()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            FlightSetup setup = Setup(spec);
            var core = new FlightCore(setup);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, Header(setup, core, spec), ownsStream: false);
            var events = new StepEvents();
            FlightEvent contact = FlightEvent.Of(EventKind.ContactMade);
            contact.Part = 3;
            contact.Object = WorldIds.Ground;
            contact.Surface = 1;
            contact.Point = new Double3(0.2, 0, 0.3);
            contact.A = 1.25;
            for (int n = 1; n <= 10; n++)
            {
                core.Advance(StickSample.Neutral);
                events.Clear();
                if (n == 5) events.Add(contact);
                if (n == 7)
                    for (int i = 0; i < 65; i++)
                        events.Add(FlightEvent.Of(EventKind.MixerSaturation));
                recorder.RecordStep(core.State, StickSample.Neutral, core.Trace, events);
            }
            recorder.Close();

            log.Position = 0;
            FlightSummary s = FlightSummary.Read(log);
            Assert.That(s.Events.Count, Is.EqualTo(1 + 64 + 1));
            Assert.That((s.Events[0].Step, s.Events[0].Event), Is.EqualTo((5L, contact)));
            Assert.That(s.Count(EventKind.MixerSaturation), Is.EqualTo(64));
            LoggedEvent lost = s.Events[s.Events.Count - 1];
            Assert.That((lost.Step, lost.Event.Kind, lost.Event.A), Is.EqualTo((7L, EventKind.EventsLost, 1.0)));
            Assert.That(s.End.EventsLost, Is.EqualTo(1));
            StringAssert.Contains("touches the ground (surface belt_bare)", s.Describe(contact));

            // The free body makes no events, so the replay finds the logged contact it did not make.
            log.Position = 0;
            ReplayResult r = FlightReplay.Run(log, OpenWorld, spec.ContentHash);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Diverged), r.Message);
            Assert.That(r.Step, Is.EqualTo(5));
            StringAssert.Contains("ContactMade event at step 5 that the replay did not make", r.Message);
        }

        [Test]
        public void TheDroneHashSeesEditsNotLineEndings()
        {
            string text = BootstrapDrone.Text().Replace("\r\n", "\n");
            Assert.That(DroneSpec.HashText(text.Replace("\n", "\r\n")), Is.EqualTo(DroneSpec.HashText(text)));
            Assert.That(DroneSpec.HashText(text + " "), Is.Not.EqualTo(DroneSpec.HashText(text)));
            Assert.That(BootstrapDrone.Spec().ContentHash, Is.EqualTo(DroneSpec.HashText(text)));
        }

        [Test]
        public void LogsLiveUnderTheUsersProfile()
        {
            string profileData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            StringAssert.StartsWith(profileData, LogStore.Folder);
            StringAssert.EndsWith(Path.Combine("450fpv", "flights"), LogStore.Folder);

            string folder = Path.Combine(Path.GetTempPath(), "fpv450-logstore-" + Guid.NewGuid().ToString("N"));
            try
            {
                string first = LogStore.NewPath(0x450F, folder);
                Assert.That(Path.GetFileName(first), Is.EqualTo("flight-000000000000450f-1.flightlog"));
                File.WriteAllBytes(first, new byte[0]);
                Assert.That(Path.GetFileName(LogStore.NewPath(0x450F, folder)), Is.EqualTo("flight-000000000000450f-2.flightlog"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static string[] InstanceFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            return names;
        }
    }
}
