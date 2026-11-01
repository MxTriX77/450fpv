using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Log
{
    /// <summary>How a replay ended; the value is tools/replay's exit code.</summary>
    public enum ReplayOutcome
    {
        /// <summary>Every record and event of a complete log matched bit for bit.</summary>
        Match = 0,

        /// <summary>A record or an event differs: Step and Field name the first.</summary>
        Diverged = 1,

        /// <summary>The log is of another world, drone, model or settings: nothing was replayed.</summary>
        Refused = 3,

        /// <summary>The log's header cannot be read.</summary>
        Unreadable = 4,

        /// <summary>Every whole record of a cut log matched; Notes say where it was cut.</summary>
        MatchCut = 5,
    }

    public sealed class ReplayResult
    {
        public ReplayOutcome Outcome;

        /// <summary>Step records replayed and matched, and events compared and matched (hitches are not compared).</summary>
        public long Steps, Events;

        /// <summary>The first diverging step (0 for the flight's start: the body or a draw), or −1.</summary>
        public long Step = -1;

        /// <summary>The first diverging field, by its name in the record layout.</summary>
        public string Field;

        /// <summary>The verdict in one line.</summary>
        public string Message;

        /// <summary>What a reader of the verdict should know: the host differences, where a log was cut.</summary>
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// The headless bit-exact replay (design 10, LOG-02): rebuilds the flight from the log's header, steps it with the
    /// logged stick samples, and compares each step's record and events, byte for byte, with the log's. No clock, no
    /// thread, no Unity: the same FlightCore.Advance as the flight. Hitches are timing, not physics, so they are not
    /// compared. A log of another world (content hash or QueryVersion), another drone.json, another model version or
    /// another FcSettings layout is refused before its first step, naming what differs. A different build, runtime or
    /// ISA is only noted: the core's arithmetic is meant to give the same bits on every x64 host (LOG-04), and a
    /// divergence then names its step and field.
    /// </summary>
    public static class FlightReplay
    {
        /// <summary>
        /// Replays the log on stream. openWorld opens the world the header names (null when it cannot); droneHash is
        /// DroneSpec.ContentHash of the drone.json the replay holds, 0 for none; isa is this host's ISA flags.
        /// </summary>
        public static ReplayResult Run(Stream log, Func<string, FlightWorld> openWorld, ulong droneHash, string isa = null)
        {
            FlightLogReader reader;
            try
            {
                reader = new FlightLogReader(log);
            }
            catch (FlightLogException e)
            {
                return new ReplayResult { Outcome = ReplayOutcome.Unreadable, Message = "unreadable: " + e.Message };
            }
            FlightWorld world = openWorld(reader.Header.WorldId);
            if (world == null)
                return Refuse("this replay cannot open the world '" + reader.Header.WorldId + "' the log was flown in");
            return Run(reader, world, droneHash, isa);
        }

        /// <summary>Replays the log reader has opened, on world (opened for its header's WorldId).</summary>
        public static ReplayResult Run(FlightLogReader log, FlightWorld world, ulong droneHash, string isa = null)
        {
            LogHeader h = log.Header;
            CultureInfo c = CultureInfo.InvariantCulture;

            // Refusals: the log's flight is not this replay's.
            if (h.ModelVersion != LogFormat.ModelVersion)
                return Refuse("the flight model differs: the log is of model version " + h.ModelVersion + ", this build is version " + LogFormat.ModelVersion);
            if (h.WorldHash != world.ContentHash)
                return Refuse("the world differs: the log was flown on world '" + h.WorldId + "' of content 0x" + h.WorldHash.ToString("X16", c)
                    + ", this replay's is 0x" + world.ContentHash.ToString("X16", c));
            if (h.QueryVersion != world.QueryVersion)
                return Refuse("the world's query version differs: the log's is " + h.QueryVersion + ", this replay's " + world.QueryVersion);
            if (h.DroneHash != droneHash)
                return Refuse("drone.json differs: the log was flown with drone.json 0x" + h.DroneHash.ToString("X16", c)
                    + ", this replay has 0x" + droneHash.ToString("X16", c));
            var problems = new List<string>();
            FlightSetup setup = h.ToSetup(world, problems);
            if (problems.Count > 0)
                return Refuse("the flight controller's settings differ: " + problems[0] + (problems.Count > 1 ? " (and " + (problems.Count - 1) + " more)" : ""));

            var result = new ReplayResult();
            string runtime = LogHeader.CurrentRuntime();
            if (h.Runtime != runtime) result.Notes.Add("recorded on runtime " + h.Runtime + ", replayed on " + runtime);
            if (isa != null && h.Isa != isa) result.Notes.Add("recorded with ISA " + h.Isa + ", replayed with " + isa);

            var core = new FlightCore(setup);
            if (StartDiffers(h, core, out string field, out string detail))
                return Diverge(result, 0, field, "the flight's start differs: " + detail);

            var replayed = new byte[LogFormat.StepSize];
            var replayedEvent = new byte[LogFormat.EventSize];
            long step = core.State.Step;
            int matched = 0, expected = 0; // the current step's events: matched so far, made by the replay
            while (log.Next())
            {
                ReadOnlySpan<byte> payload = log.Payload;
                if (log.Kind == LogFormat.StepBlock)
                {
                    if (matched < expected)
                        return Diverge(result, step, "event " + matched, "the replay made " + (expected - matched) + " events the log lacks, the first "
                            + KindOf(core, matched));
                    long at = LogFormat.StepOf(payload);
                    if (at != step + 1)
                        return Diverge(result, step + 1, "step", "the log skips from step " + step + " to step " + at);
                    LogFormat.DecodeStick(payload, out StickSample stick);
                    core.Advance(stick);
                    step = core.State.Step;
                    LogFormat.EncodeStep(replayed, core.State, stick, core.Trace);
                    int i = FirstDifference(payload, replayed);
                    if (i >= 0)
                        return DivergeField(result, step, LogFormat.StepFields, i, payload, replayed);
                    result.Steps++;
                    matched = 0;
                    expected = core.Events.Count + (core.Events.Lost > 0 ? 1 : 0);
                }
                else
                {
                    LogFormat.DecodeEvent(payload, out long at, out FlightEvent e);
                    if (e.Kind == EventKind.Hitch) continue;
                    if (at != step || matched >= expected)
                        return Diverge(result, at, "event", "the log has a " + e.Kind + " event at step " + at + " that the replay did not make");
                    FlightEvent mine = matched < core.Events.Count ? core.Events[matched] : Lost(core.Events.Lost);
                    LogFormat.EncodeEvent(replayedEvent, step, mine);
                    int i = FirstDifference(payload, replayedEvent);
                    if (i >= 0)
                    {
                        DivergeField(result, step, LogFormat.EventFields, i, payload, replayedEvent);
                        result.Field = "event " + matched + " " + result.Field;
                        return result;
                    }
                    matched++;
                    result.Events++;
                }
            }
            // A cut that falls between the last step's record and its events loses those events, not the match (QA
            // review-base finding 4): only a complete log owes them.
            if (matched < expected && log.Complete)
                return Diverge(result, step, "event " + matched, "the replay made " + (expected - matched) + " events the log lacks, the first "
                    + KindOf(core, matched));

            if (log.Complete)
            {
                if (log.End.Steps != result.Steps)
                    result.Notes.Add("the end block counts " + log.End.Steps + " steps, the log holds " + result.Steps);
                result.Outcome = ReplayOutcome.Match;
                result.Message = "match: " + result.Steps + " steps and " + result.Events + " events, bit for bit";
            }
            else
            {
                result.Outcome = ReplayOutcome.MatchCut;
                result.Message = "match to the cut: " + result.Steps + " steps and " + result.Events + " events, bit for bit, to step " + step;
                result.Notes.Add("the log is cut at byte " + log.CutAt + ": " + log.CutReason);
                if (matched < expected) result.Notes.Add("the cut took the last step's " + (expected - matched) + " events after the " + matched + " it holds");
            }
            return result;
        }

        // The body the replay built from the header's parts, and its per-flight draws, against the header's.
        static bool StartDiffers(LogHeader h, FlightCore core, out string field, out string detail)
        {
            MassProperties a = h.Body, b = core.Body;
            double[] logged = { a.Mass, a.InverseMass, a.CenterOfMass.X, a.CenterOfMass.Y, a.CenterOfMass.Z, a.Inertia.XX, a.Inertia.YY, a.Inertia.ZZ,
                a.Inertia.XY, a.Inertia.XZ, a.Inertia.YZ };
            double[] mine = { b.Mass, b.InverseMass, b.CenterOfMass.X, b.CenterOfMass.Y, b.CenterOfMass.Z, b.Inertia.XX, b.Inertia.YY, b.Inertia.ZZ,
                b.Inertia.XY, b.Inertia.XZ, b.Inertia.YZ };
            string[] names = { "body.mass", "body.inverse_mass", "body.com.x", "body.com.y", "body.com.z", "body.inertia.xx", "body.inertia.yy",
                "body.inertia.zz", "body.inertia.xy", "body.inertia.xz", "body.inertia.yz" };
            for (int i = 0; i < logged.Length; i++)
            {
                if (BitConverter.DoubleToInt64Bits(logged[i]) != BitConverter.DoubleToInt64Bits(mine[i]))
                {
                    field = names[i];
                    detail = "the mass build gives " + Bits(mine[i]) + ", the log " + Bits(logged[i]);
                    return true;
                }
            }
            IReadOnlyList<string> streams = core.Random.Opened;
            IReadOnlyList<ParameterDraw> draws = core.Random.Draws;
            if (streams.Count != h.Streams.Length || draws.Count != h.Draws.Length)
            {
                field = "randomness";
                detail = "the replay opened " + streams.Count + " streams and drew " + draws.Count + " values, the log " + h.Streams.Length + " and " + h.Draws.Length;
                return true;
            }
            for (int i = 0; i < streams.Count; i++)
            {
                if (streams[i] != h.Streams[i])
                {
                    field = "stream " + i;
                    detail = "the replay's is " + streams[i] + ", the log's " + h.Streams[i];
                    return true;
                }
            }
            for (int i = 0; i < draws.Count; i++)
            {
                ParameterDraw d = draws[i], l = h.Draws[i];
                if (d.Stream != l.Stream || d.Parameter != l.Parameter || d.Index != l.Index || !Same(d.Lo, l.Lo) || !Same(d.Hi, l.Hi) || !Same(d.Value, l.Value))
                {
                    field = "draw " + l.Stream + "/" + l.Parameter;
                    detail = "the replay drew " + Bits(d.Value) + " in [" + Bits(d.Lo) + ", " + Bits(d.Hi) + "], the log " + Bits(l.Value);
                    return true;
                }
            }
            field = null;
            detail = null;
            return false;
        }

        static FlightEvent Lost(int count)
        {
            FlightEvent e = FlightEvent.Of(EventKind.EventsLost);
            e.A = count;
            return e;
        }

        static string KindOf(FlightCore core, int i) => i < core.Events.Count ? core.Events[i].Kind.ToString() : EventKind.EventsLost.ToString();

        static int FirstDifference(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return i;
            return -1;
        }

        static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        static string Bits(double v) =>
            v.ToString("R", CultureInfo.InvariantCulture) + " (0x" + BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture) + ")";

        static ReplayResult Refuse(string why) => new ReplayResult { Outcome = ReplayOutcome.Refused, Message = "refused: " + why };

        static ReplayResult Diverge(ReplayResult r, long step, string field, string why)
        {
            r.Outcome = ReplayOutcome.Diverged;
            r.Step = step;
            r.Field = field;
            r.Message = "diverged at step " + step + " (t = " + (step * FlightCore.Dt).ToString("0.000", CultureInfo.InvariantCulture) + " s), " + field + ": " + why;
            return r;
        }

        static ReplayResult DivergeField(ReplayResult r, long step, LogField[] fields, int offset, ReadOnlySpan<byte> logged, ReadOnlySpan<byte> replayed)
        {
            int f = LogFormat.FieldAt(fields, offset);
            string name = f >= 0 ? fields[f].Name : "byte " + offset;
            string why = f >= 0
                ? "logged " + LogFormat.FormatField(fields[f], logged) + ", replayed " + LogFormat.FormatField(fields[f], replayed)
                : "the bytes differ";
            return Diverge(r, step, name, why);
        }
    }
}
