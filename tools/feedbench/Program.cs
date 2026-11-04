using System;
using System.Globalization;
using Fpv450.Feed;

namespace Fpv450.FeedBench
{
    // The feed's headless tools. `schedule` writes the schedule a seed gives, one row per field (FeedSchedule), to
    // stdout, and its summary, digest and the stubbed signals to stderr: the port's side of the A/B against the
    // Godot feed (p4-feed task 1.1). The dropouts run on Godot's timer by default (D-085), so the dump is the Godot
    // feed's own; `--no-dropouts` stops the timer. `--godot` puts back the tear's black wedge, the "black sticks"
    // D-065 took out, for the A/B: it moves no draw, so the dump is the same, and only the wedge line says 1.
    internal static class Program
    {
        const string Usage = "usage: feedbench schedule --seed <n> [--fields <n>] [--current <0-1>] [--cut <field>[:abrupt]] [--no-dropouts] [--godot]";

        private static int Main(string[] args)
        {
            if (args.Length < 1 || args[0] != "schedule" || !Parse(args, out int seed, out int fields,
                    out float current, out int cutField, out bool abrupt, out bool dropouts, out bool godot))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            var stdout = Console.Out;
            stdout.NewLine = "\n";
            stdout.WriteLine(FeedSchedule.Header);
            FeedSchedule.Run(seed, fields, current, cutField, abrupt, e => stdout.WriteLine(FeedSchedule.Row(e)), dropouts);
            stdout.Flush();

            Console.Error.WriteLine(FeedSignalLog.Describe(new StubFeedSignals()));
            Console.Error.WriteLine(FeedSchedule.Summary(seed, fields, current, dropouts));
            var wedge = new FeedEvents(seed) { TearWedge = godot };
            Console.Error.WriteLine("tear wedge " + FeedUniforms.From(wedge, seed, 1f, 1f, current).TearWedge
                .ToString(CultureInfo.InvariantCulture) + (godot ? " (Godot's, the FEED-00 A/B)" : " (off, D-065)"));
            Console.Error.WriteLine("digest " + FeedSchedule.Digest(seed, fields, current, cutField, abrupt, dropouts)
                .ToString("X16", CultureInfo.InvariantCulture));
            return 0;
        }

        static bool Parse(string[] args, out int seed, out int fields, out float current, out int cutField,
            out bool abrupt, out bool dropouts, out bool godot)
        {
            seed = int.MinValue;
            fields = 15000; // 5 minutes, the Godot feed's own scan (VideoFeed.cs:239)
            current = 0f;
            cutField = 0;
            abrupt = false;
            dropouts = true;
            godot = false;
            var inv = CultureInfo.InvariantCulture;
            for (int i = 1; i < args.Length; i += 2)
            {
                if (args[i] == "--godot")
                {
                    godot = true; // the tear's black wedge (D-065), for the A/B
                    i--;
                    continue;
                }
                if (args[i] == "--no-dropouts")
                {
                    dropouts = false; // Godot's dropout timer off (D-085 keeps it on)
                    i--;
                    continue;
                }
                if (i + 1 >= args.Length)
                    return false;
                string v = args[i + 1];
                switch (args[i])
                {
                    case "--seed":
                        if (!int.TryParse(v, NumberStyles.Integer, inv, out seed)) return false;
                        break;
                    case "--fields":
                        if (!int.TryParse(v, NumberStyles.Integer, inv, out fields) || fields < 1) return false;
                        break;
                    case "--current":
                        if (!float.TryParse(v, NumberStyles.Float, inv, out current) || current < 0f || current > 1f)
                            return false;
                        break;
                    case "--cut":
                        string[] p = v.Split(':');
                        if (!int.TryParse(p[0], NumberStyles.Integer, inv, out cutField) || cutField < 1
                            || p.Length > 2 || (p.Length == 2 && p[1] != "abrupt"))
                            return false;
                        abrupt = p.Length == 2;
                        break;
                    default:
                        return false;
                }
            }
            return seed != int.MinValue;
        }
    }
}
