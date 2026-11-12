using System;
using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.App
{
    // w13-props-in-frame 1.1: the props drawn at the sim's rotor angles (PropView): the angles over consecutive fields, a
    // rotor at rest, and a flight that logs the same with the props drawn or the model hidden.
    public sealed class PropViewTests
    {
        const double TwoPi = 2 * Math.PI;

        static DroneSpec _spec;
        static MaterialRow[] _materials;
        static ulong _hash;

        [OneTimeSetUp]
        public void LoadDrone()
        {
            _spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            _materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out _hash);
        }

        static FlightSetup Setup(ulong seed)
        {
            var inputs = new SessionInputs { Drone = _spec, Materials = _materials, MaterialsHash = _hash, Choices = new FlightChoices { Legs = true } };
            return SessionSetup.Build(inputs, seed, out _, out _);
        }

        // Armed at step 8, the throttle from 0 up to 0.45 over the first half second, then held: off the floor and climbing.
        static StickSample Stick(long k) => new StickSample
        {
            HostUs = k * FlightCore.StepUs,
            Arm = k >= 8,
            Throttle = k < 16 ? 0 : Math.Min(0.45, 0.45 * (k - 16) / 500.0),
        };

        static double Wrap(double a)
        {
            a %= TwoPi;
            return a < 0 ? a + TwoPi : a;
        }

        static double Arc(double d)
        {
            d = Wrap(d);
            return d > Math.PI ? d - TwoPi : d;
        }

        static GameObject Model()
        {
            DroneModels models = DroneModels.Load();
            Assert.That(models != null && models.legsOn != null, "ProjectSetup.Apply's D01 prefabs");
            return Object.Instantiate(models.legsOn);
        }

        static Vector4 Rotor(PropView view, int c)
        {
            var block = new MaterialPropertyBlock();
            view.Renderer(c).GetPropertyBlock(block);
            return block.GetVector("_PropRotor");
        }

        [Test]
        public void TheDrawnAngleIsTheSimsAtEachField()
        {
            // 24 consecutive fields of a flight: each rotor drawn at the sim's angle at the field's reference instant, the
            // angles at the history's steps (each row's read) the sim's own, and from field to field the drawn angle moves by
            // the rotation the rotor's speed integrates to between them.
            var core = new FlightCore(Setup(3));
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame first);
            poses.Begin(0, first, default);
            GameObject model = Model();
            try
            {
                var view = new PropView();
                view.Bind(model, _spec);
                Assert.That((view.Hidden, view.Rotors), Is.EqualTo((4, 4)), "every LOD's still props hidden, four turning drawn");
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        Assert.That(m == null || m.name != PropView.ModelMaterial, r.name + " draws no still prop");
                for (int c = 0; c < 4; c++)
                {
                    Vector3 hub = view.Renderer(c).transform.localPosition;
                    Double3 motor = _spec.Motors[c].Position;
                    Assert.That((hub - new Vector3((float)motor.X, (float)motor.Y, (float)motor.Z)).magnitude, Is.LessThan(1e-6f), "at motor " + c + "'s hub");
                }
                FeedSettings feed = FeedSettings.Load();
                var samples = new float[RollingShutter.Samples];
                double[] last = null;
                long firstField = 1500 / FlightCore.FieldSteps, step = 0;
                for (long field = firstField; field < firstField + 24; field++)
                {
                    while (step < FlightCore.FieldSteps * (field + 1) + RollingShutter.SampleLead)
                    {
                        step++;
                        core.Advance(Stick(step));
                        core.GetPose(out PoseFrame p);
                        poses.Publish(p, default);
                    }
                    long start = FieldCamera.FieldStartUs(field);
                    Assert.That(FieldCamera.At(poses, start + (long)RollingShutter.ReferenceUs, out PoseFrame at), "the field's reference instant");
                    view.Field(poses, start, at, 1.2e-4, feed);
                    var now = new double[4];
                    for (int c = 0; c < 4; c++)
                    {
                        Vector4 drawn = Rotor(view, c);
                        Assert.That(drawn.x, Is.EqualTo((float)at.RotorAngle[c]), "rotor " + c + " drawn at the sim's angle");
                        // The disc's frame turns blade 0 to (cos sθ, 0, sin sθ): the sim's (cos θ, 0, s sin θ), PropStrikes' s.
                        Assert.That(drawn.y, Is.EqualTo(_spec.Motors[c].Clockwise ? -1f : 1f));
                        Assert.That(drawn.w, Is.EqualTo(1f), "each row at its read");
                        Assert.That(at.RotorSpeed[c], Is.GreaterThan(500), "turning");

                        PropView.Samples(poses, start, at, c, samples);
                        long s0 = start / FlightCore.StepUs - RollingShutter.SampleLead;
                        for (int i = 0; i < RollingShutter.Samples; i++)
                        {
                            Assert.That(poses.TryRead(s0 + i, out PoseFrame p), "step " + (s0 + i));
                            Assert.That(Math.Abs(Arc(at.RotorAngle[c] + samples[i] - p.RotorAngle[c])), Is.LessThan(2e-5), "rotor " + c + " at step " + (s0 + i));
                        }
                        double r = RollingShutter.ReferenceUs / RollingShutter.SampleUs + RollingShutter.SampleLead;
                        int j = (int)r;
                        Assert.That(samples[j] + (samples[j + 1] - samples[j]) * (r - j), Is.EqualTo(0).Within(1e-5), "the samples against the reference");
                        now[c] = at.RotorAngle[c];
                    }
                    if (last != null)
                    {
                        // The rotation between the two reference instants, from the steps' speeds (trapezoids, 1 ms).
                        long from = (start - FlightCore.FieldSteps * FlightCore.StepUs + (long)RollingShutter.ReferenceUs) / FlightCore.StepUs;
                        for (int c = 0; c < 4; c++)
                        {
                            double turned = 0;
                            for (long k = from; k < from + FlightCore.FieldSteps; k++)
                            {
                                poses.TryRead(k, out PoseFrame a);
                                poses.TryRead(k + 1, out PoseFrame b);
                                turned += 0.5 * (a.RotorSpeed[c] + b.RotorSpeed[c]) * FlightCore.Dt;
                            }
                            Assert.That(Math.Abs(Arc(now[c] - last[c] - turned)), Is.LessThan(0.03), "rotor " + c + ": the field's turn, " + turned.ToString("0.000") + " rad");
                            Assert.That(now[c], Is.Not.EqualTo(last[c]), "a new angle every field");
                        }
                        TestContext.Out.WriteLine($"field {field}: front left at {now[3]:0.000} rad, {Arc(now[3] - last[3]) * 180 / Math.PI:0.0}° on (mod 180° {Wrap(now[3] - last[3]) % Math.PI * 180 / Math.PI:0.0}°), {at.RotorSpeed[3] * 60 / TwoPi:0} rpm");
                    }
                    last = now;
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void ARotorAtRestIsDrawnStill()
        {
            // Disarmed on the floor: no sweep at any row and no turn from field to field; the blade drawn where the sim holds it.
            var core = new FlightCore(Setup(4));
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame first);
            poses.Begin(0, first, default);
            for (long k = 1; k <= 200; k++)
            {
                core.Advance(StickSample.Neutral);
                core.GetPose(out PoseFrame p);
                poses.Publish(p, default);
            }
            GameObject model = Model();
            try
            {
                var view = new PropView();
                view.Bind(model, _spec);
                long start = FieldCamera.FieldStartUs(8);
                Assert.That(FieldCamera.At(poses, start + (long)RollingShutter.ReferenceUs, out PoseFrame at));
                view.Field(poses, start, at, 0.02, FeedSettings.Load()); // the longest exposure
                var samples = new float[RollingShutter.Samples];
                for (int c = 0; c < 4; c++)
                {
                    Assert.That(at.RotorSpeed[c], Is.EqualTo(0), "at rest");
                    Assert.That(Rotor(view, c).x, Is.EqualTo((float)core.State.RotorAngle[c]), "where the sim holds it");
                    var block = new MaterialPropertyBlock();
                    view.Renderer(c).GetPropertyBlock(block);
                    foreach (float a in block.GetFloatArray("_PropAngles")) Assert.That(a, Is.EqualTo(0f), "no sweep, no turn");
                    PropView.Held(at, c, samples);
                    foreach (float a in samples) Assert.That(a, Is.EqualTo(0f), "the chase view's, held");
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void TheLogIsTheSameWithThePropsDrawnOrTheModelHidden()
        {
            // Nothing the drawing does reaches the flight: a fixed-input flight logged with the props drawn at every step and
            // field, and again with no model at all, byte for byte.
            byte[] drawn = Flight(true), hidden = Flight(false);
            Assert.That(drawn.Length, Is.GreaterThan(10000));
            Assert.That(drawn, Is.EqualTo(hidden), "the two logs");
        }

        static byte[] Flight(bool draw)
        {
            FlightSetup setup = Setup(5);
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame first);
            poses.Begin(0, first, default);
            GameObject model = draw ? Model() : null;
            var view = new PropView();
            view.Bind(model, _spec);
            FeedSettings feed = FeedSettings.Load();
            try
            {
                for (long k = 1; k <= 2000; k++)
                {
                    StickSample stick = Stick(k);
                    core.Advance(stick);
                    recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                    core.GetPose(out PoseFrame p);
                    poses.Publish(p, default);
                    if (!draw) continue;
                    view.Draw(p, 1.2e-4);
                    long field = FieldCamera.LatestField(k);
                    if (k % FlightCore.FieldSteps == 0 && field > 0 && FieldCamera.At(poses, FieldCamera.FieldStartUs(field) + (long)RollingShutter.ReferenceUs, out PoseFrame at))
                        view.Field(poses, FieldCamera.FieldStartUs(field), at, 1.2e-4, feed);
                }
                recorder.Close();
                Assert.That(recorder.Fault, Is.Null);
                return stream.ToArray();
            }
            finally
            {
                if (model != null) Object.DestroyImmediate(model);
            }
        }
    }
}
