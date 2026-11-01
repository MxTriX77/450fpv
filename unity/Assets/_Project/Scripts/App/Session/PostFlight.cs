using System;
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
    /// distance and the incidents. The screen shows Lines; it never reads the log itself.
    /// The incidents (D-063), each logged and none an end, in step order:
    /// - a crash: the log's first Crash event, the core's latch, with the part, the object and its material (LOG-03)
    /// - upside down on the ground: the body's up axis below the horizon while any contact touches, held UpsideDownSteps;
    ///   once a rest (assumed: a tumble that rights itself is not one)
    /// - the failsafe: each loss of the radio (FlightSummary.Failsafes), which disarms an armed drone (INP-05)
    /// - the battery cut-off: the first step either pack's charge is at or under BatteryCutoffCharge. Charge 0 is the end
    ///   of drone.json's OCV table, 3.0 V a cell open-circuit (the flight controller's warning); past it the pack's
    ///   voltage falls on towards 0 (Propulsion.OpenCircuitVolts), and the flight goes on
    /// </summary>
    public sealed class PostFlight
    {
        public const long UpsideDownSteps = 1000;
        public const double BatteryCutoffCharge = 0;

        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public string LogPath;

        /// <summary>The core's summary of the log; its Header is null when the log is unreadable.</summary>
        public FlightSummary Log;

        /// <summary>Pilot at the last step of a complete log (only the pilot ends a flight, D-063), None for a cut one.</summary>
        public FlightEnd End;

        /// <summary>What the flight met, in step order (D-063).</summary>
        public readonly List<FlightIncident> Incidents = new List<FlightIncident>();

        /// <summary>The flight's time to its last record, s, and the body origin's path over it, m.</summary>
        public double Seconds, Distance;

        /// <summary>The packs' charge used, 0–1 (their mean, first record to last).</summary>
        public double BatteryUsed;

        /// <summary>The first crash's event and its names: the part that hit, the object and its material (or surface).</summary>
        public bool HasCrash;
        public LoggedEvent Crash;
        public string CrashPart = "", CrashObject = "", CrashMaterial = "";

        public ulong Seed => Log.Header != null ? Log.Header.Seed : 0;

        /// <summary>
        /// The summary of the log at path. A crash's object is named by the header when the flight placed it (the stand),
        /// else by the world the log was flown in: openWorld opens it by the header's world id, and its name counts only
        /// when the world's content hash is the header's (QA's finding 3). Without it, or for another world, "object N".
        /// </summary>
        public static PostFlight Read(string path, Func<string, IWorldQuery> openWorld = null)
        {
            var p = new PostFlight { LogPath = path, Log = FlightSummary.Read(path) };
            if (p.Log.Header == null) return p;
            Double3 last = p.Log.Header.StartPosition;
            long upsideFrom = -1;
            bool counted = false, cutoff = false;
            using (FlightLogReader reader = FlightLogReader.Open(path))
                while (reader.Next())
                {
                    if (reader.Kind != LogFormat.StepBlock) continue;
                    reader.ReadStep(out LoggedStep s);
                    p.Distance += (s.State.Position - last).Length();
                    last = s.State.Position;
                    long step = s.State.Step;
                    if (!(s.State.Attitude.Rotate(new Double3(0, 1, 0)).Y < 0 && s.Trace.ContactsTouching > 0)) upsideFrom = -1;
                    else if (upsideFrom < 0)
                    {
                        upsideFrom = step;
                        counted = false;
                    }
                    if (upsideFrom >= 0 && !counted && step - upsideFrom >= UpsideDownSteps)
                    {
                        counted = true;
                        p.Incidents.Add(new FlightIncident { Kind = FlightIncidentKind.UpsideDown, Step = step });
                    }
                    if (!cutoff && (s.State.PackCharge0 <= BatteryCutoffCharge || s.State.PackCharge1 <= BatteryCutoffCharge))
                    {
                        cutoff = true;
                        p.Incidents.Add(new FlightIncident { Kind = FlightIncidentKind.BatteryCutoff, Step = step });
                    }
                }
            FlightSummary log = p.Log;
            p.Seconds = log.Seconds;
            p.End = new FlightEnd { Kind = log.Complete ? FlightEndKind.Pilot : FlightEndKind.None, Step = log.HasSteps ? log.Last.State.Step : 0 };
            if (log.HasSteps)
                p.BatteryUsed = (log.First.State.PackCharge0 + log.First.State.PackCharge1 - log.Last.State.PackCharge0 - log.Last.State.PackCharge1) / 2;
            foreach (LoggedEvent e in log.Events)
            {
                if (e.Event.Kind != EventKind.Crash) continue;
                LogHeader h = log.Header;
                p.HasCrash = true;
                p.Crash = e;
                p.CrashPart = e.Event.Part < h.Parts.Length ? h.Parts[e.Event.Part].Name : "part #" + e.Event.Part;
                p.CrashObject = ObjectName(h, e.Event.Object, openWorld);
                p.CrashMaterial = e.Event.Surface != 0 ? h.SurfaceName(e.Event.Surface) : e.Event.Material != 0 ? h.MaterialName(e.Event.Material) : "";
                p.Incidents.Add(new FlightIncident { Kind = FlightIncidentKind.Crash, Step = e.Step });
                break;
            }
            foreach (LoggedStep f in log.Failsafes)
                p.Incidents.Add(new FlightIncident { Kind = FlightIncidentKind.Failsafe, Step = f.State.Step });
            p.Incidents.Sort((a, b) => a.Step != b.Step ? a.Step.CompareTo(b.Step) : a.Kind.CompareTo(b.Kind));
            return p;
        }

        static string ObjectName(LogHeader h, int id, Func<string, IWorldQuery> openWorld)
        {
            if (id == WorldIds.Ground) return "the ground";
            string name = h.RuntimeObjectName(id);
            if (name == null)
            {
                IWorldQuery world = openWorld?.Invoke(h.WorldId);
                if (world != null && world.ContentHash == h.WorldHash) name = world.ObjectName(id);
            }
            return name ?? "object " + id;
        }

        /// <summary>The end in words.</summary>
        public string Cause()
        {
            switch (End.Kind)
            {
                case FlightEndKind.Pilot: return "ended by the pilot";
                case FlightEndKind.Fault: return "the flight thread failed";
                default: return Log.Header == null ? "the log cannot be read: " + Log.HeaderProblem : "the log was cut: " + Log.CutReason;
            }
        }

        /// <summary>An incident in words, with its time: "crash at 0:04.3: prop_fl hit R01_A (steel), a prop strike at 9.4 m/s".</summary>
        public string Describe(in FlightIncident incident)
        {
            string at = " at " + Clock(incident.Seconds);
            switch (incident.Kind)
            {
                case FlightIncidentKind.Crash:
                    string how = Crash.Event.Index == (byte)CrashCause.PropStrike ? "a prop strike" : "an impact";
                    return "crash" + at + ": " + CrashPart + " hit " + CrashObject + (CrashMaterial.Length > 0 ? " (" + CrashMaterial + ")" : "") + ", " + how + " at "
                        + Crash.Event.A.ToString("0.0", C) + " m/s";
                case FlightIncidentKind.UpsideDown: return "upside down on the ground" + at;
                case FlightIncidentKind.BatteryCutoff: return "battery cut-off" + at;
                default: return "failsafe" + at + ": the radio was lost";
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
            foreach (FlightIncident i in Incidents)
                lines.Add(Describe(i));
            if (Log.Header.Input == InputSource.Keyboard) lines.Add("keyboard: never tune from this flight");
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
