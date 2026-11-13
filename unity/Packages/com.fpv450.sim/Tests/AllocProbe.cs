using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
#if NET7_0_OR_GREATER
using System.Diagnostics.Tracing;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
#endif

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The suite's one zero-allocation measure: the bytes allocated on the calling thread between Before and After, read from
    /// GC.GetAllocatedBytesForCurrentThread. That counter is exact only while no background collection runs: during one it books the
    /// unused tail of the thread's allocation context (under the 8 KiB quantum) as allocated though nothing was, the 0 B checks'
    /// one-in-six failures (night-alloc-hunt). So the host runs with concurrent GC off (simtests.csproj), and the first Before refuses
    /// to measure otherwise. A window's body must be a call: written inline, the JIT may move an allocation across the reads.
    /// When a window shows bytes, Report says how many did and how many bytes; in a host started with the runtime's allocation
    /// events (tools/simtests/alloc-names.runsettings) it also names what the runtime saw this thread allocate in the first.
    /// </summary>
    public static class AllocProbe
    {
        [ThreadStatic] static long t_opened;
        [ThreadStatic] static int t_windows;
        [ThreadStatic] static long t_bytes, t_firstOpened, t_firstClosed, t_firstBytes;

        static AllocProbe()
        {
#if NET7_0_OR_GREATER
            if (GCSettings.LatencyMode != GCLatencyMode.Batch)
                throw new InvalidOperationException($"GC latency mode {GCSettings.LatencyMode}: a background collection can run, and during one "
                    + "GC.GetAllocatedBytesForCurrentThread counts bytes nobody allocated. Set ConcurrentGarbageCollection false in the project.");
            if (Listener.OnAtStart) Listener.Start();
#endif
            // Compiled now, so no counted window holds their compilation.
            After(Before());
            t_windows = 0;
            t_bytes = 0;
        }

#if NET7_0_OR_GREATER
        /// <summary>Report names the types: the host started with the runtime's allocation events on.</summary>
        public static bool NamesTypes => Listener.OnAtStart;
#endif

        /// <summary>Opens a window on this thread: the counter now (read last, after anything this call does).</summary>
        public static long Before()
        {
            t_opened = Stopwatch.GetTimestamp();
            return GC.GetAllocatedBytesForCurrentThread();
        }

        /// <summary>Closes the window Before opened: the bytes allocated on this thread since (read first). A window with bytes is
        /// kept for Report.</summary>
        public static long After(long before)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (bytes != 0) Keep(bytes);
            return bytes;
        }

        static void Keep(long bytes)
        {
            long closed = Stopwatch.GetTimestamp();
            if (t_windows++ == 0)
            {
                t_firstOpened = t_opened;
                t_firstClosed = closed;
                t_firstBytes = bytes;
#if NET7_0_OR_GREATER
                Listener.MarkClose();
#endif
            }
            t_bytes += bytes;
        }

        /// <summary>"" when no window on this thread had bytes since the last Report; else how many did and their bytes, and the
        /// types, counts and bytes the runtime saw this thread allocate in the first. Starts this thread's count afresh.</summary>
        public static string Report()
        {
            if (t_windows == 0) return "";
            var text = new StringBuilder();
            text.Append($"; {t_windows} window(s) with bytes, {t_bytes} B; the first, {t_firstBytes} B in {(t_firstClosed - t_firstOpened) * 1e6 / Stopwatch.Frequency:F0} µs: ");
#if NET7_0_OR_GREATER
            if (Listener.OnAtStart) Listener.Describe(t_firstOpened, text);
            else text.Append("to name the types, run it again with: dotnet test tools/simtests -c Release --settings tools/simtests/alloc-names.runsettings --filter <the test>");
#else
            text.Append("no allocation events on this runtime");
#endif
            t_windows = 0;
            t_bytes = 0;
            return text.ToString();
        }

#if NET7_0_OR_GREATER
        /// <summary>The runtime's allocation events (GCSampledObjectAllocationHigh), when the host started with them on
        /// (alloc-names.runsettings; started later, the JIT's fast allocation helpers fire none), off by default. A type that allocates rarely fires one per allocation except
        /// its first, whose count rides on its next; a frequent one fires a sample carrying the count and bytes since its last. One
        /// thread's events arrive in the order it allocated, so a Mark allocated as a window closes ends that window's events.</summary>
        sealed class Listener : EventListener
        {
            const int Size = 1 << 16;
            static Listener instance;
            static long markType;
            [ThreadStatic] static long t_markTid, t_markAt;
            readonly object gate = new object();
            readonly long[] tid = new long[Size], ticks = new long[Size], type = new long[Size], objects = new long[Size], size = new long[Size];
            long written;

            public static readonly bool OnAtStart = Environment.GetEnvironmentVariable("DOTNET_EnableEventPipe") == "1"
                && (Environment.GetEnvironmentVariable("DOTNET_EventPipeConfig") ?? "").Contains("Microsoft-Windows-DotNETRuntime:0x200000");

            sealed class Mark { }
            static Mark placed;

            [DllImport("kernel32.dll")]
            static extern uint GetCurrentThreadId();

            // Kept in a field, behind a call: a Mark the JIT could prove unused, or move, would never reach the heap's events.
            [MethodImpl(MethodImplOptions.NoInlining)]
            static void Place() => placed = new Mark();

            public static void Start()
            {
                markType = typeof(Mark).TypeHandle.Value.ToInt64();
                instance = new Listener();
                // A type's first allocation fires no event of its own; and Marks close together would be sampled, not each sent.
                Place();
                Thread.Sleep(20);
            }

            public static void MarkClose()
            {
                Place();
                t_markAt = Stopwatch.GetTimestamp();
                t_markTid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? GetCurrentThreadId() : -1;
            }

            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name == "Microsoft-Windows-DotNETRuntime")
                    EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x200000);
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                if ((e.EventId != 20 && e.EventId != 32) || e.Payload == null || e.Payload.Count < 4) return;
                lock (gate)
                {
                    int i = (int)(written++ & (Size - 1));
                    tid[i] = e.OSThreadId;
                    ticks[i] = e.TimeStamp.Ticks;
                    type[i] = e.Payload[1] is IntPtr id ? id.ToInt64() : Convert.ToInt64(e.Payload[1]);
                    objects[i] = Convert.ToInt64(e.Payload[2]);
                    size[i] = Convert.ToInt64(e.Payload[3]);
                }
            }

            /// <summary>Waits for this thread's last Mark, then sums by type this thread's events before it, back to the window's
            /// opening: the Mark's stamp less the time from the opening to the Mark, with 50 µs for the stamps' own delay.</summary>
            public static void Describe(long opened, StringBuilder text)
            {
                Listener l = instance;
                long me = t_markTid;
                if (l == null || me < 0) { text.Append("no allocation events on this platform"); return; }
                long mark = -1;
                var wait = Stopwatch.StartNew();
                while (mark < 0 && wait.ElapsedMilliseconds < 3000)
                {
                    lock (l.gate)
                        for (long n = l.written - 1; n >= Math.Max(0, l.written - Size) && mark < 0; n--)
                        {
                            int i = (int)(n & (Size - 1));
                            if (l.type[i] == markType && l.tid[i] == me) mark = n;
                        }
                    if (mark < 0) Thread.Sleep(20);
                }
                if (mark < 0) { text.Append("its allocation events did not arrive in 3 s (a young thread's can wait in its buffer)"); return; }
                var seen = new SortedDictionary<string, (long n, long b)>();
                lock (l.gate)
                {
                    long markTicks = l.ticks[(int)(mark & (Size - 1))];
                    long from = markTicks - (t_markAt - opened) * TimeSpan.TicksPerSecond / Stopwatch.Frequency - 500;
                    for (long n = mark - 1; n >= Math.Max(0, l.written - Size); n--)
                    {
                        int i = (int)(n & (Size - 1));
                        if (l.tid[i] != me) continue;
                        if (l.ticks[i] < from) break;
                        string name = Name(l.type[i]);
                        seen.TryGetValue(name, out var s);
                        seen[name] = (s.n + l.objects[i], s.b + l.size[i]);
                    }
                }
                if (seen.Count == 0)
                {
                    text.Append("no allocation event (a type's first allocation fires only with its next); this thread's last before it: ");
                    lock (l.gate)
                    {
                        long markTicks = l.ticks[(int)(mark & (Size - 1))];
                        int shown = 0;
                        for (long n = mark - 1; n >= Math.Max(0, l.written - Size) && shown < 4; n--)
                        {
                            int i = (int)(n & (Size - 1));
                            if (l.tid[i] != me) continue;
                            text.Append($"{Name(l.type[i])} ×{l.objects[i]} {l.size[i]} B {(l.ticks[i] - markTicks) / 1e4:F3} ms, ");
                            shown++;
                        }
                    }
                }
                foreach (var kv in seen) text.Append($"{kv.Key} ×{kv.Value.n} {kv.Value.b} B, ");
            }

            static string Name(long typeId)
            {
                try { return Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr(new IntPtr(typeId)))?.ToString() ?? $"type 0x{typeId:x}"; }
                catch (Exception) { return $"type 0x{typeId:x}"; }
            }
        }
#endif
    }
}
