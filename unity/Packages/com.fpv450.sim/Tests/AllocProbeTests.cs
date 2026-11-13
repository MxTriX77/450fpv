using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>The 0 B checks' measure (AllocProbe): it sees one deliberate 24-byte allocation in a window and names it, it reads 0 for
    /// a window that allocates nothing, and the host it measures in cannot run a background collection.</summary>
    public class AllocProbeTests
    {
        static object sink;

        /// <summary>24 B on a 64-bit runtime: header, method table pointer and the minimum payload.</summary>
        sealed class Deliberate { }

        // A call, as every window's body is: written inline, the JIT may move an allocation out across the counter's reads.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void AllocateOne() => sink = new Deliberate();

        [Test]
        public void TheKnownWrong_OneObjectInTheWindow_IsSeenAndNamed()
        {
            // A type's first allocation fires no event of its own but rides on its next: two before the window, apart and well
            // before it, leave the window's one alone in its event.
            AllocateOne();
            System.Threading.Thread.Sleep(20);
            AllocateOne();
            System.Threading.Thread.Sleep(20);
            AllocProbe.Report();
            long before = AllocProbe.Before();
            AllocateOne();
            long bytes = AllocProbe.After(before);
            string report = AllocProbe.Report();
            TestContext.Out.WriteLine($"{bytes} B{report}");
            // A bool, not Is.EqualTo(24): the JIT may move the constraint's boxed 24 into the window.
            Assert.That(bytes == (IntPtr.Size == 8 ? 24 : 12), $"one object, its header and method table pointer: {bytes} B");
#if NET7_0_OR_GREATER
            Assert.That(report, AllocProbe.NamesTypes ? Does.Contain("AllocProbeTests+Deliberate ×1 24 B") : Does.Contain("alloc-names.runsettings"));
#endif
        }

        [Test]
        public void AWindowThatAllocatesNothing_ReadsZero()
        {
            var a = new double[64];
            AllocProbe.Report();
            long before = AllocProbe.Before();
            for (int i = 0; i < a.Length; i++) a[i] = a[i] * 0.5 + i;
            long bytes = AllocProbe.After(before);
            Assert.That(bytes == 0 && AllocProbe.Report() == "", "0 B and nothing to report");
        }

#if NET7_0_OR_GREATER
        [Test]
        public void TheHostRunsNoBackgroundCollection()
        {
            Assert.That(System.Runtime.GCSettings.LatencyMode, Is.EqualTo(System.Runtime.GCLatencyMode.Batch), "concurrent GC off (simtests.csproj)");
            Assert.That(AppContext.GetData("System.GC.Concurrent")?.ToString(), Is.EqualTo("false"));
        }
#endif
    }
}
