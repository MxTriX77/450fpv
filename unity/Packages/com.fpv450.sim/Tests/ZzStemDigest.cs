using System;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using static Fpv450.Sim.Tests.SweepScenarios;

namespace Fpv450.Sim.Tests
{
    // TEMPORARY (not committed): a per-step digest of the parts' push runs, to show a cost cut changes no result bit for bit.
    public sealed class ZzStemDigest
    {
        const string Out = @"C:\Users\maksy\AppData\Local\Temp\claude\d952a8549f1b6dab\5c065864-a9b3-4aa1-a251-910986d788cc\scratchpad\w16-plants\drone2\";

        static ulong _h;
        static long _n;

        static void Mix(double x)
        {
            _h ^= (ulong)BitConverter.DoubleToInt64Bits(x);
            _h *= 0x100000001B3UL;
            _h ^= _h >> 29;
        }

        static void Mix(Double3 v)
        {
            Mix(v.X);
            Mix(v.Y);
            Mix(v.Z);
        }

        static void Digest(FlightCore core)
        {
            ref readonly FlightState s = ref core.State;
            Mix(s.Position);
            Mix(s.Velocity);
            Mix(s.BodyRate);
            Mix(s.Attitude.X);
            Mix(s.Attitude.Y);
            Mix(s.Attitude.Z);
            Mix(s.Attitude.W);
            for (int g = 0; g < 5; g++)
            {
                Mix(core.Strikes.PartLast[g].Force);
                Mix(core.Strikes.PartLast[g].Stems);
            }
            for (int c = 0; c < 4; c++)
            {
                Mix(core.Strikes.StemLast[c].Cuts);
                Mix(core.Strikes.StemLast[c].Work);
                Mix(core.Strikes.StemLast[c].InPlane);
            }
            Mix(core.Strikes.PartActive);
            _n++;
        }

        [Test]
        public void Write()
        {
            PropStrikes.PrOld = true;
            string a = All();
            PropStrikes.PrOld = false;
            string b = All();
            string c = All();
            File.WriteAllText(Out + "digest-old.txt", a);
            File.WriteAllText(Out + "digest-new.txt", b);
            TestContext.Out.WriteLine("old:\n" + a + "new:\n" + b + "new again:\n" + c);
            Assert.That(b, Is.EqualTo(a));
            Assert.That(c, Is.EqualTo(a));
        }

        static string All()
        {
            var text = new System.Text.StringBuilder();
            Type t = typeof(PartStemTests);
            MethodInfo skim = t.GetMethod("SkimWeeds", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo crown = t.GetMethod("ThroughCrown", BindingFlags.NonPublic | BindingFlags.Static);
            // The skims and the crown: digest via a ticks list is not available, so hook the core through a wrapper: run each with
            // Advance observed by the strikes' state after the run's end (the run's own sums) and, step by step, through the landing.
            foreach ((string name, object[] args) in new (string, object[])[]
            {
                ("plough 2 m/s 0.3 m", new object[] { 2.0, 0.3, true, null }),
                ("skim 5 m/s 0.75 m", new object[] { 5.0, 0.75, true, null }),
                ("over 2 m/s 3 m", new object[] { 2.0, 3.0, true, null }),
                ("plough 5 m/s 0.3 m", new object[] { 5.0, 0.3, true, null }),
            })
            {
                object r = skim.Invoke(null, args);
                text.AppendLine($"{name}: {Fields(r)}");
            }
            object cr = crown.Invoke(null, new object[] { 2.0, true, null });
            text.AppendLine($"crown 2 m/s: {Fields(cr)}");

            // The landing in the densest tall cell, step by step.
            _h = 0xCBF29CE484222325UL;
            _n = 0;
            StemScenarios.Run land = StemScenarios.Landing(parts: true, from: 1.2, stepped: Digest);
            text.AppendLine($"landing: {_n} steps, digest {_h:X16}, steps {land.Steps}");

            // Range #61's slide on S04's drawn stems, step by step.
            DroneSpec spec = RepositoryDrone.Spec();
            var range = new Context(MapWorld.Load(RangeScenarios.Root, "test-range"), spec, WindLevel.Calm);
            Point p = Array.Find(Draw(range.Map, 400, 1, out _), q => q.Index == 61);
            _h = 0xCBF29CE484222325UL;
            _n = 0;
            Row row = Fly(range, p, Manoeuvre.Slide, 1, replay: false, observe: Digest)[0];
            text.AppendLine($"range #61 slide: {_n} steps, digest {_h:X16}; {row.Line()}");
            return text.ToString();
        }

        static string Fields(object o)
        {
            var sb = new System.Text.StringBuilder();
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(o);
                sb.Append(f.Name).Append('=').Append(v is double d ? BitConverter.DoubleToInt64Bits(d).ToString("X16") : v?.ToString()).Append(' ');
            }
            return sb.ToString();
        }
    }
}
