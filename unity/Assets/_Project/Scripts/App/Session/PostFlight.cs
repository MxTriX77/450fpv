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
    /// - a crash: the log's first Crash event, the core's latch, with the body part, the object and its material (LOG-03);
    ///   since model 35 a crash is by acceleration only (a blade's strike is damage, never a crash: p7f-damage 1.3)
    /// - upside down on the ground: the body's up axis below the horizon while any contact touches, held UpsideDownSteps;
    ///   once each time it turns over: tipping from one upside-down rest to another (off the bottle onto a side, every
    ///   contact lifting for a few ms) is the same one, and only righting starts a new one (assumed: a tumble that
    ///   rights itself is not one)
    /// - the failsafe: each loss of the radio (FlightSummary.Failsafes), which disarms an armed drone (INP-05)
    /// - the battery cut-off: the first step either pack's charge is at or under BatteryCutoffCharge. Charge 0 is the end
    ///   of drone.json's OCV table, 3.0 V a cell open-circuit (the flight controller's warning); past it the pack's
    ///   voltage falls on towards 0 (Propulsion.OpenCircuitVolts), and the flight goes on
    /// After them, the props' damage (D-079, until p7f-damage's DamageReport and its screen), per rotor in the header's part
    /// order: each damaged blade's last BladeDamaged (its tip left as a share of the radius, that pass's mass lost), with the
    /// mass the blade lost in all, and the rotor's last RotorJammed, with how often it jammed; each worded by
    /// FlightSummary.Describe.
    /// Then the legs' damage (p7g-legs 1.5), in Corners order: each leg not intact at the last step record (its state there,
    /// the elastic bow left out, and its set), with the event of its worst state, which names the landing or the object it
    /// met and the speed it touched at.
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

        /// <summary>The props' damage, per rotor (the event's part) and, for a rotor, its blades in order, then its jam.</summary>
        public readonly List<PropDamage> Damage = new List<PropDamage>();

        /// <summary>The legs' damage, each damaged leg in Corners order (p7g-legs 1.5).</summary>
        public readonly List<DamagedLeg> Legs = new List<DamagedLeg>();

        static readonly string[] CornerNames = { "rear right", "front right", "rear left", "front left" };

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
                    bool upside = s.State.Attitude.Rotate(new Double3(0, 1, 0)).Y < 0;
                    if (!upside) counted = false; // righted: the next time it turns over is another
                    if (!(upside && s.Trace.ContactsTouching > 0)) upsideFrom = -1;
                    else if (upsideFrom < 0) upsideFrom = step;
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
            foreach (LoggedEvent e in log.Events)
            {
                if (e.Event.Kind != EventKind.BladeDamaged && e.Event.Kind != EventKind.RotorJammed) continue;
                int slot = e.Event.Kind == EventKind.RotorJammed ? PropDamage.JamSlot : e.Event.Index & 15; // the blade, or the jam
                int i = p.Damage.FindIndex(d => d.Last.Event.Part == e.Event.Part && d.Slot == slot);
                PropDamage now = i >= 0 ? p.Damage[i] : new PropDamage { Slot = slot };
                now.Last = e;
                now.Count++;
                if (e.Event.Kind == EventKind.BladeDamaged) now.MassLost += e.Event.B;
                if (i >= 0) p.Damage[i] = now;
                else p.Damage.Add(now);
            }
            p.Damage.Sort((a, b) => a.Last.Event.Part != b.Last.Event.Part ? a.Last.Event.Part.CompareTo(b.Last.Event.Part) : a.Slot.CompareTo(b.Slot));
            LegDamageSpec legs = log.Header.Contacts?.Legs;
            if (legs != null && log.HasSteps)
            {
                StepTrace end = log.Last.Trace;
                for (int c = 0; c < LegDamage.Legs; c++)
                {
                    LegFlags state = (LegFlags)(byte)(end.LegState >> (8 * c)) & ~LegFlags.Bowed;
                    if (state == LegFlags.None) continue;
                    var leg = new DamagedLeg { Corner = c, State = state, Set = end.LegSet[c] };
                    int worst = 0;
                    foreach (LoggedEvent e in log.Events)
                    {
                        int rank = Rank(e.Event, legs, c);
                        if (rank <= worst) continue;
                        worst = rank;
                        leg.Cause = e;
                    }
                    FlightEvent cause = leg.Cause.Event;
                    leg.Object = ObjectName(log.Header, cause.Object, openWorld);
                    leg.Material = cause.Surface != 0 ? log.Header.SurfaceName(cause.Surface) : cause.Material != 0 ? log.Header.MaterialName(cause.Material) : "";
                    p.Legs.Add(leg);
                }
            }
            return p;
        }

        // How bad an event is for the leg at corner c, by the state it brings: 0 for another part's or another kind.
        static int Rank(in FlightEvent e, LegDamageSpec legs, int c)
        {
            bool leg = e.Part == legs.Parts[c];
            switch (e.Kind)
            {
                case EventKind.LegDetached: return leg ? 5 : 0;
                case EventKind.LegBroken: return leg ? 4 : 0;
                case EventKind.SkidLoose: return e.Part == legs.TJointParts[c] ? 3 : 0;
                case EventKind.LegCracked: return leg ? 2 : 0;
                case EventKind.LegSet: return leg ? 1 : 0; // a skid's fold names the skid, never a leg
                default: return 0;
            }
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

        /// <summary>An incident in words, with its time: "crash at 0:04.3: motor_fl hit R01_A (steel), an impact at 9.4 m/s".</summary>
        public string Describe(in FlightIncident incident)
        {
            string at = " at " + Clock(incident.Seconds);
            switch (incident.Kind)
            {
                case FlightIncidentKind.Crash:
                    return "crash" + at + ": " + CrashPart + " hit " + CrashObject + (CrashMaterial.Length > 0 ? " (" + CrashMaterial + ")" : "") + ", an impact at "
                        + Crash.Event.A.ToString("0.0", C) + " m/s";
                case FlightIncidentKind.UpsideDown: return "upside down on the ground" + at;
                case FlightIncidentKind.BatteryCutoff: return "battery cut-off" + at;
                default: return "failsafe" + at + ": the radio was lost";
            }
        }

        /// <summary>A prop's damage in words, its last event's time and FlightSummary.Describe's line: "damage at 0:00.2: part
        /// 'prop_fl' blade 0 damaged (break) by 'wall' shape 0 (material steel): tip at 0.96625 R, from 0.96625 R, 0.0239741 g
        /// lost; 0.03508 g lost over its 2 events", or "jam at 0:02.1: rotor front left JAMMED on …; 3 jams".</summary>
        public string Describe(in PropDamage damage)
        {
            bool jam = damage.Slot == PropDamage.JamSlot;
            string line = (jam ? "jam" : "damage") + " at " + Clock(damage.Last.Step * FlightCore.Dt) + ": " + Log.Describe(damage.Last.Event);
            if (damage.Count > 1)
                line += jam ? "; " + damage.Count.ToString(C) + " jams" : "; " + (damage.MassLost * 1000).ToString("G4", C) + " g lost over its " + damage.Count.ToString(C) + " events";
            return line;
        }

        /// <summary>A leg's damage in words, its state and the event of its worst state: "leg front right bent 2.9°, cracked
        /// at 0:00.1: landed on the ground at 1.52 m/s", or "leg rear left broken at 0:00.0: hit kerb (steel) at 1.50 m/s".</summary>
        public string Describe(in DamagedLeg leg)
        {
            FlightEvent e = leg.Cause.Event;
            string met = (e.Object == WorldIds.Ground ? "landed on " : "hit ") + leg.Object + (leg.Material.Length > 0 ? " (" + leg.Material + ")" : "");
            return "leg " + CornerNames[leg.Corner] + " " + StateWords(leg) + " at " + Clock(leg.Cause.Step * FlightCore.Dt) + ": " + met + " at "
                + e.C.ToString("0.00", C) + " m/s";
        }

        // A leg gone says so alone; one still on lists its bend and twist (degrees), its crack and its skid.
        static string StateWords(in DamagedLeg leg)
        {
            if ((leg.State & LegFlags.Detached) != 0) return "off";
            if ((leg.State & LegFlags.Broken) != 0) return "broken";
            var words = new List<string>();
            double bend = Math.Sqrt(leg.Set.X * leg.Set.X + leg.Set.Z * leg.Set.Z) * 180 / Math.PI, twist = Math.Abs(leg.Set.Y) * 180 / Math.PI;
            if (bend > 0) words.Add("bent " + bend.ToString("0.0", C) + "°");
            if (twist > 0) words.Add("twisted " + twist.ToString("0.0", C) + "°");
            if (words.Count == 0 && (leg.State & LegFlags.Set) != 0) words.Add("bent");
            if ((leg.State & LegFlags.Cracked) != 0) words.Add("cracked");
            if ((leg.State & LegFlags.SkidLoose) != 0) words.Add("its skid loose");
            return string.Join(", ", words);
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
            foreach (PropDamage d in Damage)
                lines.Add(Describe(d));
            foreach (DamagedLeg l in Legs)
                lines.Add(Describe(l));
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
