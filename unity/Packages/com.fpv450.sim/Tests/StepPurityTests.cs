using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Nothing outside the state enters a step" (PHB-01). The suite scans the step's code paths,
    // the sim package's runtime folders the flight thread runs, with comments and literals removed.
    public sealed class StepPurityTests
    {
        // The step's folders. World/ joins when the flight core consumes p6-world-query-gallery's queries (task 1.6);
        // until then the world lane's own determinism suite covers it.
        static readonly string[] StepFolders = { "Flight", "Control", "Contact", "Wind", "Tether", "Log", "Math" };

        sealed class Rule
        {
            public readonly string Id;
            public readonly Regex Pattern;

            public Rule(string id, string pattern)
            {
                Id = id;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
            }
        }

        static readonly Rule[] Rules =
        {
            new Rule("wall-clock", @"\b(DateTime|DateTimeOffset|Stopwatch|TimeProvider|TickCount64|TickCount)\b|QueryPerformance"),
            new Rule("host-clock", @"\bIHostClock\b|\bNowUs\s*\("),
            new Rule("thread-id", @"\b(ManagedThreadId|CurrentManagedThreadId|CurrentThread|GetCurrentThreadId|ThreadStatic|ThreadLocal|AsyncLocal)\b"),
            new Rule("default-hash-code", @"\b(GetHashCode|HashCode)\b"),
            new Rule("unordered-collection", @"\b(Dictionary|HashSet|Hashtable|ConcurrentDictionary|ConcurrentBag|ConcurrentQueue|ConcurrentStack|ConditionalWeakTable|ToLookup|GroupBy|AsParallel)\b|\bParallel\."),
            // The determinism rules of DetMath.cs, which a step must keep as well.
            new Rule("system-math", @"\bMath\.|\bMathF\b|\bFusedMultiplyAdd\b|System\.Numerics|System\.Runtime\.Intrinsics"),
            // Unity's Mono evaluates float expressions in double precision and CoreCLR does not (p4-feed measured the
            // drift), so the step stays in double.
            new Rule("single-precision", @"\b(float|Single)\b"),
            new Rule("other-randomness", @"\bnew\s+(System\.)?Random\s*\(|\bSystem\.Random\b|\bGuid\.NewGuid\b"),
            new Rule("process-environment", @"\bEnvironment\."),
            new Rule("linq", @"\busing\s+System\.Linq\b"),
        };

        // The only files allowed a rule, and why.
        static readonly string[,] Allowed =
        {
            { "Flight/HostClock.cs", "wall-clock", "it is the host clock: Stopwatch as µs" },
            { "Flight/HostClock.cs", "host-clock", "it defines the clock" },
            { "Flight/FlightScheduler.cs", "host-clock", "only the scheduler reads the clock, never a step" },
            { "Math/DetMath.cs", "system-math", "System.Math is called only inside DetMath" },
            { "Log/LogStore.cs", "process-environment", "the logs' folder under the user's profile (LOG-05), found at a flight's start, never by a step" },
        };

        [Test]
        public void NothingOutsideTheStateEntersAStep()
        {
            string runtime = RuntimeDirectory();
            var violations = new List<string>();
            int files = 0;
            foreach (string folder in StepFolders)
            {
                string dir = Path.Combine(runtime, folder);
                if (!Directory.Exists(dir)) continue;
                string[] paths = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    files++;
                    string relative = path.Substring(runtime.Length + 1).Replace('\\', '/');
                    foreach (string v in Scan(relative, File.ReadAllText(path)))
                        violations.Add(v);
                }
            }
            Assert.That(files, Is.GreaterThanOrEqualTo(10), "the scan must see the flight core's files");
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        // The scan's own check: each rule catches a known-wrong line and ignores the same words in comments and strings.
        [TestCase("wall-clock", "var t = DateTime.UtcNow;")]
        [TestCase("wall-clock", "long t = Stopwatch.GetTimestamp();")]
        [TestCase("wall-clock", "int t = Environment.TickCount;")]
        [TestCase("host-clock", "long now = clock.NowUs();")]
        [TestCase("thread-id", "int id = Thread.CurrentThread.ManagedThreadId;")]
        [TestCase("thread-id", "[ThreadStatic] static double scratch;")]
        [TestCase("default-hash-code", "int h = part.GetHashCode();")]
        [TestCase("unordered-collection", "var hooks = new Dictionary<long, Hook>();")]
        [TestCase("unordered-collection", "foreach (var id in new HashSet<long>()) { }")]
        [TestCase("system-math", "double s = Math.Sin(a);")]
        [TestCase("system-math", "using System.Numerics;")]
        [TestCase("single-precision", "float k = 0.5f;")]
        [TestCase("other-randomness", "var r = new Random(1);")]
        [TestCase("process-environment", "int n = Environment.ProcessorCount;")]
        [TestCase("linq", "using System.Linq;")]
        public void TheScanCatchesAKnownWrongLine(string rule, string line)
        {
            string code = "namespace X {\n// " + line + "\n/* " + line + " */\nclass C { string s = \"" + line.Replace("\"", "") + "\"; }\n}\n";
            Assert.That(Scan("Flight/Clean.cs", code), Is.Empty, "comments and strings are not code");
            List<string> found = Scan("Flight/Wrong.cs", "class C {\n" + line + "\n}\n");
            Assert.That(found.Exists(v => v.Contains("[" + rule + "]")), Is.True, rule + " missed: " + string.Join("; ", found));
        }

        static List<string> Scan(string relative, string source)
        {
            var found = new List<string>();
            string[] lines = StripCommentsAndLiterals(source).Split('\n');
            foreach (Rule rule in Rules)
            {
                if (IsAllowed(relative, rule.Id)) continue;
                for (int i = 0; i < lines.Length; i++)
                    if (rule.Pattern.IsMatch(lines[i]))
                        found.Add(relative + ":" + (i + 1) + " [" + rule.Id + "] " + lines[i].Trim());
            }
            return found;
        }

        static bool IsAllowed(string relative, string rule)
        {
            for (int i = 0; i < Allowed.GetLength(0); i++)
                if (Allowed[i, 0] == relative && Allowed[i, 1] == rule)
                    return true;
            return false;
        }

        // Comments become nothing and string or character literals become empty ones; line breaks stay, so line
        // numbers hold.
        static string StripCommentsAndLiterals(string s)
        {
            var b = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                char next = i + 1 < s.Length ? s[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < s.Length && s[i] != '\n') i++;
                }
                else if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/'))
                    {
                        if (s[i] == '\n') b.Append('\n');
                        i++;
                    }
                    i += 2;
                }
                else if (c == '@' && next == '"')
                {
                    i += 2;
                    while (i < s.Length && !(s[i] == '"' && !(i + 1 < s.Length && s[i + 1] == '"')))
                    {
                        if (s[i] == '"') i++;
                        else if (s[i] == '\n') b.Append('\n');
                        i++;
                    }
                    i++;
                    b.Append("\"\"");
                }
                else if (c == '"' || c == '\'')
                {
                    i++;
                    while (i < s.Length && s[i] != c && s[i] != '\n')
                        i += s[i] == '\\' ? 2 : 1;
                    i++;
                    b.Append(c).Append(c);
                }
                else
                {
                    b.Append(c);
                    i++;
                }
            }
            return b.ToString();
        }

        // The package's Runtime folder, found from this file's own path (the .NET tools compile it in place, and
        // Unity's editor runs from the project folder).
        static string RuntimeDirectory([CallerFilePath] string here = "")
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(here))
                candidates.Add(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(here)) ?? "", "..", "Runtime"));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Packages", "com.fpv450.sim", "Runtime"));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "unity", "Packages", "com.fpv450.sim", "Runtime"));
            foreach (string c in candidates)
                if (Directory.Exists(c))
                    return Path.GetFullPath(c).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Assert.Fail("the sim package's Runtime folder was not found from " + here);
            return null;
        }
    }
}
