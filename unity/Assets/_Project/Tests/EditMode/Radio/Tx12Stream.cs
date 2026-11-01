using System;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Tests.Radio
{
    /// <summary>
    /// A synthetic TX12 report stream with EdgeTX 2.8's USB joystick layout and timing, from its source: the report holds
    /// 24 buttons (channels 9–32) and 8 axes 0–2047 (channels 1–8, each the channel's −1024…1024 output + 1024, clipped);
    /// in joystick mode the mixer runs every 2 ms and queues a report each run; the host polls the 1 ms interrupt
    /// endpoint. Assumed: the radio's crystal 30 ppm slow, and 20–200 µs from the poll to the input thread's wake.
    /// Not a recording of a real TX12: tools/radioprobe --record makes one when it is plugged in.
    /// </summary>
    static class Tx12Stream
    {
        public static readonly RadioAxis[] Axes =
        {
            new RadioAxis(1, 0x30, 0, 2047), new RadioAxis(1, 0x31, 0, 2047), new RadioAxis(1, 0x32, 0, 2047), new RadioAxis(1, 0x33, 0, 2047),
            new RadioAxis(1, 0x34, 0, 2047), new RadioAxis(1, 0x35, 0, 2047), new RadioAxis(1, 0x36, 0, 2047), new RadioAxis(1, 0x36, 0, 2047),
        };

        /// <summary>
        /// Reports arriving from startUs for durationUs, the sticks sweeping, the throttle ramping, the arm switch (ch5)
        /// on from startUs + 100 ms; the device lost at lostAtUs (−1 for never).
        /// </summary>
        public static RecordedRadio Make(long startUs, long durationUs, long lostAtUs = -1)
        {
            var radio = new RecordedRadio("RadioMaster TX12 (synthetic)", InputSource.Radio, Axes, 24) { LostAtUs = lostAtUs };
            var channels = new int[8];
            var axes = new int[8];
            const long frameUs = 1000, frameAt = 417; // the host's 1 ms frames fall at startUs + 417 + n ms
            for (long k = 0; ; k++)
            {
                long ready = startUs + k * 2000 + k * 2000 * 30 / 1000000; // the mixer's k-th run on the radio's clock
                long poll = startUs + frameAt + ((ready - startUs - frameAt + frameUs) / frameUs) * frameUs; // the next frame
                long arrived = poll + 20 + (long)(Hash((ulong)k) % 181);
                if (arrived >= startUs + durationUs) break;
                long t = ready - startUs;
                channels[0] = Triangle(t, 700000);
                channels[1] = Triangle(t + 150000, 1100000);
                channels[2] = (int)(t % 2000000 * 2048 / 2000000) - 1024;
                channels[3] = Triangle(t + 400000, 1300000);
                channels[4] = t >= 100000 ? 1024 : -1024;
                channels[5] = -1024;
                channels[6] = 0;
                channels[7] = 1024;
                for (int i = 0; i < 8; i++) axes[i] = Math.Max(0, Math.Min(2047, channels[i] + 1024));
                radio.Add(arrived, axes, 0);
            }
            return radio;
        }

        // −1024 → +1024 → −1024 over periodUs.
        static int Triangle(long t, long periodUs)
        {
            long p = t % periodUs;
            long half = periodUs / 2;
            return (int)((p < half ? p : periodUs - p) * 2048 / half) - 1024;
        }

        static ulong Hash(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>A clock the test sets.</summary>
    sealed class VirtualClock : IHostClock
    {
        public long Now;
        public long NowUs() => Now;
    }

    /// <summary>The host clock shifted so a recording's µs run in real time from now + leadUs.</summary>
    sealed class ShiftedClock : IHostClock
    {
        readonly long _zero;

        public ShiftedClock(long leadUs) => _zero = HostClock.Instance.NowUs() + leadUs;

        public long NowUs() => HostClock.Instance.NowUs() - _zero;
    }

    /// <summary>The stick in force at every step, as the flight thread hands it to the log.</summary>
    sealed class SticksInForce : IFlightRecorder
    {
        public readonly StickSample[] Sticks;
        public int Count;
        public bool Armed; // the state after the last step

        public SticksInForce(int capacity) => Sticks = new StickSample[capacity];

        public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
        {
            if (Count < Sticks.Length) Sticks[Count++] = stick;
            Armed = state.Armed;
        }

        public void RecordHitch(in Hitch hitch)
        {
        }
    }

    static class Flights
    {
        public static FlightSetup FreeBody() => new FlightSetup
        {
            Seed = 0x7812,
            World = FlightWorld.Floor(),
            Mass = new MassModel(new[] { new DronePart { Name = "cube", Kind = PartKind.Box, Mass = 4.8, Size = new Double3(0.25, 0.25, 0.25) } }),
            StartPosition = new Double3(0, 0.5, 0),
        };

        /// <summary>A device factory that opens device once, then finds nothing (the cable stays out).</summary>
        public static Func<IRadioDevice> Once(IRadioDevice device)
        {
            bool opened = false;
            return () =>
            {
                if (opened) return null;
                opened = true;
                return device;
            };
        }

        /// <summary>The samples the input layer makes of a recording: each report through the map, then the loss.</summary>
        public static StickSample[] Expected(RecordedRadio radio)
        {
            var map = new RawStickMap();
            var report = new RadioReport(radio.Axes.Length);
            var list = new System.Collections.Generic.List<StickSample>();
            for (int i = 0; i < radio.Count; i++)
            {
                if (radio.LostAtUs >= 0 && radio.TimeOf(i) >= radio.LostAtUs) break;
                report.HostUs = radio.TimeOf(i);
                Array.Copy(radio.AxesOf(i), report.Axes, report.Axes.Length);
                report.Buttons = radio.ButtonsOf(i);
                map.Map(report, radio.Axes, out StickSample s);
                list.Add(s);
            }
            if (radio.LostAtUs >= 0) list.Add(new StickSample { HostUs = radio.LostAtUs, Failsafe = true });
            return list.ToArray();
        }

        /// <summary>The stick in force at each of steps steps from startUs, by the timing contract's rule alone.</summary>
        public static StickSample[] InForce(StickSample[] samples, long startUs, int steps)
        {
            var result = new StickSample[steps];
            StickSample inForce = StickSample.Neutral;
            int next = 0;
            for (int k = 0; k < steps; k++)
            {
                long stepUs = startUs + k * FlightCore.StepUs;
                while (next < samples.Length && samples[next].HostUs <= stepUs) inForce = samples[next++];
                result[k] = inForce;
            }
            return result;
        }
    }
}
