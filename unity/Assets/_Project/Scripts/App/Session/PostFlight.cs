using System.Collections.Generic;
using System.Globalization;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

namespace Fpv450.App.Session
{
    /// <summary>
    /// The post-flight summary (APP-04, design 7), from the flight's log alone: the core's summary reader
    /// (FlightSummary) for the header, the events and the failsafes, and one pass over the step records for the
    /// distance and FlightEndRule's end. The screen shows Lines; it never reads the log itself.
    /// </summary>
    public sealed class PostFlight
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public string LogPath;

        /// <summary>The core's summary of the log; its Header is null when the log is unreadable.</summary>
        public FlightSummary Log;

        /// <summary>The end FlightEndRule finds in the records; Pilot when none holds in a complete log, None in a cut one.</summary>
        public FlightEnd End;

        /// <summary>The flight's time to its last record, s, and the body origin's path over it, m.</summary>
        public double Seconds, Distance;

        /// <summary>The packs' charge used, 0–1 (their mean, first record to last).</summary>
        public double BatteryUsed;

        /// <summary>The first crash's event and its names: the part that hit, the object and its material (or surface).</summary>
        public bool HasCrash;
        public LoggedEvent Crash;
        public string CrashPart = "", CrashObject = "", CrashMaterial = "";

        public ulong Seed => Log.Header != null ? Log.Header.Seed : 0;

        public static PostFlight Read(string path)
        {
            var p = new PostFlight { LogPath = path, Log = FlightSummary.Read(path) };
            if (p.Log.Header == null) return p;
            var rule = new FlightEndRule();
            Double3 last = p.Log.Header.StartPosition;
            using (FlightLogReader reader = FlightLogReader.Open(path))
                while (reader.Next())
                {
                    if (reader.Kind != LogFormat.StepBlock) continue;
                    reader.ReadStep(out LoggedStep s);
                    p.Distance += (s.State.Position - last).Length();
                    last = s.State.Position;
                    rule.Step(s.State, s.Stick, s.Trace);
                }
            FlightSummary log = p.Log;
            p.Seconds = log.Seconds;
            p.End = rule.End.Ended ? rule.End : new FlightEnd { Kind = log.Complete ? FlightEndKind.Pilot : FlightEndKind.None, Step = log.HasSteps ? log.Last.State.Step : 0 };
            if (log.HasSteps)
                p.BatteryUsed = (log.First.State.PackCharge0 + log.First.State.PackCharge1 - log.Last.State.PackCharge0 - log.Last.State.PackCharge1) / 2;
            foreach (LoggedEvent e in log.Events)
            {
                if (e.Event.Kind != EventKind.Crash) continue;
                LogHeader h = log.Header;
                p.HasCrash = true;
                p.Crash = e;
                p.CrashPart = e.Event.Part < h.Parts.Length ? h.Parts[e.Event.Part].Name : "part #" + e.Event.Part;
                p.CrashObject = e.Event.Object == WorldIds.Ground ? "the ground" : h.RuntimeObjectName(e.Event.Object) ?? "object " + e.Event.Object;
                p.CrashMaterial = e.Event.Surface != 0 ? h.SurfaceName(e.Event.Surface) : e.Event.Material != 0 ? h.MaterialName(e.Event.Material) : "";
                break;
            }
            return p;
        }

        /// <summary>The end and its cause in words.</summary>
        public string Cause()
        {
            switch (End.Kind)
            {
                case FlightEndKind.Crash:
                    if (!HasCrash) return "crash";
                    string how = Crash.Event.Index == (byte)CrashCause.PropStrike ? "a prop strike" : "an impact";
                    return "crash: " + CrashPart + " hit " + CrashObject + (CrashMaterial.Length > 0 ? " (" + CrashMaterial + ")" : "") + ", " + how + " at "
                        + Crash.Event.A.ToString("0.0", C) + " m/s";
                case FlightEndKind.UpsideDown: return "crash: upside down on the ground";
                case FlightEndKind.BatteryCutoff: return "battery cut-off";
                case FlightEndKind.Failsafe: return "failsafe: the radio was lost, and the drone disarmed";
                case FlightEndKind.Disarm: return "disarmed";
                case FlightEndKind.Pilot: return "ended by the pilot";
                case FlightEndKind.Fault: return "the flight thread failed";
                default: return Log.Header == null ? "the log cannot be read: " + Log.HeaderProblem : "the log was cut: " + Log.CutReason;
            }
        }

        /// <summary>The summary's lines, for the screen.</summary>
        public List<string> Lines()
        {
            var lines = new List<string>();
            if (Log.Header == null)
            {
                lines.Add(Cause());
                lines.Add("log " + LogPath);
                return lines;
            }
            lines.Add(Cause());
            lines.Add("time " + Clock(Seconds) + ", distance " + Distance.ToString("0", C) + " m");
            lines.Add("battery used " + (BatteryUsed * 100).ToString("0.0", C) + " %");
            if (Log.Header.Input == InputSource.Keyboard) lines.Add("keyboard: never tune from this flight");
            foreach (LoggedStep f in Log.Failsafes)
                lines.Add("failsafe at " + Clock(f.State.Step * FlightCore.Dt));
            lines.Add("seed 0x" + Seed.ToString("X16", C));
            lines.Add("log " + LogPath);
            return lines;
        }

        static string Clock(double seconds)
        {
            int whole = (int)seconds;
            return (whole / 60).ToString(C) + ":" + (whole % 60).ToString("00", C) + "." + ((int)((seconds - whole) * 10)).ToString(C);
        }
    }
}
