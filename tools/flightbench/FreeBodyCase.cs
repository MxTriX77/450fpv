using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.FlightBench
{
    // A free body (task 1.1): launched with a velocity and a spin under gravity, no forces or moments otherwise, the
    // sticks neutral. The golden check pins every step's state bits and compares the flight with its exact solution;
    // the bench times the step.
    internal static class FreeBodyCase
    {
        public const string Name = "free-body";
        const int GoldenSteps = 10000; // 10 s

        // FNV-1a 64 over every step's state bits. A re-record is audited case by case (CLAUDE.md).
        const ulong GoldenHash = 0xAECE29900A37638BUL;

        static FlightSetup Setup() => new FlightSetup
        {
            Seed = 1,
            World = FlightWorld.Floor(),
            StartPosition = new Double3(0, 100, 0),
            StartVelocity = new Double3(3, 2, -1),
            StartBodyRate = new Double3(2, -1, 0.5),
        };

        public static bool Golden()
        {
            FlightSetup setup = Setup();
            var core = new FlightCore(setup);
            ulong hash = 0xCBF29CE484222325UL;
            double worstNorm = 0;
            for (int k = 0; k < GoldenSteps; k++)
            {
                core.Advance(StickSample.Neutral);
                hash = Hash(hash, core.State);
                worstNorm = Math.Max(worstNorm, Math.Abs(core.State.Attitude.Norm() - 1));
            }

            // Semi-implicit Euler's exact discrete solution: x_n = x0 + n·dt·v0 + g·dt²·n(n+1)/2.
            double n = GoldenSteps, dt = FlightCore.Dt;
            Double3 expected = setup.StartPosition + setup.StartVelocity * (n * dt) + FlightCore.Gravity * (dt * dt * n * (n + 1) / 2);
            double positionError = (core.State.Position - expected).Length();

            // The exact attitude under a constant body rate: q0·(cos(|ω|t/2), sin(|ω|t/2)·ω/|ω|).
            Double3 w = setup.StartBodyRate;
            double rate = w.Length(), half = rate * n * dt / 2, s = Math.Sin(half) / rate;
            var exact = new Quat(w.X * s, w.Y * s, w.Z * s, Math.Cos(half));
            double angleError = 2 * Math.Acos(Math.Min(1, Math.Abs(Quat.Dot(exact, core.State.Attitude))));

            bool hashOk = hash == GoldenHash;
            bool ok = hashOk && positionError < 1e-9 && worstNorm < 1e-12 && angleError < 1e-4;
            Console.WriteLine($"{Name}: {GoldenSteps} steps, hash 0x{hash:X16} ({(hashOk ? "matches" : $"golden 0x{GoldenHash:X16}")}), "
                + $"position error {positionError:E2} m (< 1e-9), quaternion norm error {worstNorm:E2} (< 1e-12), "
                + $"attitude error {angleError:E2} rad (< 1e-4): {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        public static bool Bench()
        {
            const int warmup = 2000, steps = 60000; // 60 s of flight
            var core = new FlightCore(Setup());
            var times = new long[steps];
            for (int k = 0; k < warmup; k++) core.Advance(StickSample.Neutral);

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < steps; k++)
            {
                long t0 = Stopwatch.GetTimestamp();
                core.Advance(StickSample.Neutral);
                times[k] = Stopwatch.GetTimestamp() - t0;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

            Array.Sort(times);
            double us = 1e6 / Stopwatch.Frequency;
            double p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us, max = times[steps - 1] * us;
            bool ok = p99 <= 300 && allocated == 0;
            Console.WriteLine($"{Name}: {steps} steps, p50 {p50:F2} µs, p99 {p99:F2} µs, max {max:F1} µs, "
                + $"timer {us * 1000:F0} ns, steady-state allocation {allocated} B: {(ok ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return ok;
        }

        static ulong Hash(ulong h, in FlightState s)
        {
            h = Mix(h, (ulong)s.Step);
            h = Mix(h, s.Position);
            h = Mix(h, s.Velocity);
            h = Mix(h, s.Attitude.X);
            h = Mix(h, s.Attitude.Y);
            h = Mix(h, s.Attitude.Z);
            h = Mix(h, s.Attitude.W);
            h = Mix(h, s.BodyRate);
            return h;
        }

        static ulong Mix(ulong h, Double3 v) => Mix(Mix(Mix(h, v.X), v.Y), v.Z);

        static ulong Mix(ulong h, double v) => Mix(h, (ulong)BitConverter.DoubleToInt64Bits(v));

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
                h = unchecked((h ^ ((v >> (8 * i)) & 0xFF)) * 0x100000001B3UL);
            return h;
        }
    }
}
