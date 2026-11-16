using System;
using System.Collections.Generic;
using System.Reflection;
using Fpv450.Sim.Contact;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // TEMPORARY (not committed): where the parts' push spends its time in the dense weeds, the cache's cuts A/B interleaved.
    public sealed class ZzStemCost
    {
        static void Reset()
        {
            PropStrikes.PrSurv = PropStrikes.PrLinked = PropStrikes.PrSqMiss = PropStrikes.PrG0 = PropStrikes.PrApply = PropStrikes.PrCache = PropStrikes.PrScan = PropStrikes.PrLaw = PropStrikes.PrCacheN = PropStrikes.PrSteps = 0;
            PropStrikes.PrCached = PropStrikes.PrActive = PropStrikes.PrBegin = PropStrikes.PrDiscs = PropStrikes.PrEnd = PropStrikes.PrParts = 0;
        }

        [TestCase(2.0, 0.3, 5)]
        [TestCase(5.0, 0.75, 5)]
        [TestCase(5.0, 0.3, 3)]
        public void Profile(double speed, double height, int rounds)
        {
            MethodInfo skim = typeof(PartStemTests).GetMethod("SkimWeeds", BindingFlags.NonPublic | BindingFlags.Static);
            double us = 1e6 / System.Diagnostics.Stopwatch.Frequency;
            var p50 = new Dictionary<string, List<double>>();
            var p99 = new Dictionary<string, List<double>>();
            var cache = new Dictionary<string, List<double>>();
            for (int round = 0; round < rounds; round++)
                foreach ((string name, bool on, bool old) in new[] { ("old", true, true), ("new", true, false), ("off", false, false) })
                {
                    PropStrikes.PrOld = old;
                    var ticks = new List<long>(8192);
                    Reset();
                    skim.Invoke(null, new object[] { speed, height, on, ticks });
                    ticks.Sort();
                    long n = Math.Max(1, PropStrikes.PrApply);
                    double a = ticks[ticks.Count / 2] * us, b = ticks[ticks.Count * 99 / 100] * us, c = PropStrikes.PrCache * us / Math.Max(1, PropStrikes.PrCacheN);
                    TestContext.Out.WriteLine($"round {round} {name}: p50 {a:F1} p99 {b:F1} µs ({ticks.Count} steps); "
                        + $"per step: begin {PropStrikes.PrBegin * us / n:F1}, parts {PropStrikes.PrParts * us / n:F1} (cache {PropStrikes.PrCache * us / n:F1} = {PropStrikes.PrCacheN} × {c:F0}, scan {PropStrikes.PrScan * us / n:F1}, law {PropStrikes.PrLaw * us / n:F1}), "
                        + $"discs {PropStrikes.PrDiscs * us / n:F1}, end {PropStrikes.PrEnd * us / n:F1}; cached {PropStrikes.PrCached / (double)n:F0}, active {PropStrikes.PrActive / (double)n:F0} over {PropStrikes.PrSteps} of {PropStrikes.PrApply} steps; square misses {PropStrikes.PrSqMiss}, ground-up {PropStrikes.PrG0}; scan survivors {PropStrikes.PrSurv / (double)n:F0}, linked {PropStrikes.PrLinked / (double)n:F0} a step");
                    if (round == 0) continue; // the JIT's warm-up
                    if (!p50.ContainsKey(name))
                    {
                        p50[name] = new List<double>();
                        p99[name] = new List<double>();
                        cache[name] = new List<double>();
                    }
                    p50[name].Add(a);
                    p99[name].Add(b);
                    cache[name].Add(c);
                }
            PropStrikes.PrOld = false;
            foreach (string name in p50.Keys)
            {
                p50[name].Sort();
                p99[name].Sort();
                cache[name].Sort();
                TestContext.Out.WriteLine($"{speed} m/s, {height} m, {name}: median over rounds p50 {p50[name][p50[name].Count / 2]:F1} µs, p99 {p99[name][p99[name].Count / 2]:F1} µs, a rebuild {cache[name][cache[name].Count / 2]:F0} µs");
            }
        }
    }
}
