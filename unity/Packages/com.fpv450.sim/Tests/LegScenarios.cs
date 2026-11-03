using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7g-legs 1.2's scripted touches (without NUnit, so tools/flightbench can link it): the drone's free body set down at
    /// an attitude with a velocity onto the floor, a range site or a runtime object, the throttle cut (no propulsion: the
    /// rigid-body landing of 1.1's onsets), on a seed of the legs' strength draws, with the legs' law or without it (the
    /// elastic legs). Each runs until the drone rests or the time is up, and reports each leg's end state, the legs'
    /// events, the tilt, the tunnels and a hash of the flight.
    /// </summary>
    public static class LegScenarios
    {
        public struct Touch
        {
            /// <summary>The roll (+x, the right side, down) and the nose-down pitch, deg.</summary>
            public double RollDeg, PitchDeg;

            /// <summary>The start velocity, world m/s (down is −y).</summary>
            public Double3 Velocity;

            /// <summary>The legs' strength seed (the flight's).</summary>
            public ulong Seed;

            /// <summary>Without the legs' law: the elastic legs (the known-wrong).</summary>
            public bool Elastic;

            public double Seconds;

            /// <summary>Null for the stub's floor; else a range site (its world and foot).</summary>
            public ContactScenarios.Site? Site;

            /// <summary>Runtime objects in the world (a post, a bar), or null.</summary>
            public RuntimeObject[] Objects;

            /// <summary>The body origin's start, world; default: just over the ground at the site's foot.</summary>
            public Double3? Start;
        }

        public sealed class Outcome
        {
            public LegFlags[] Flags = new LegFlags[4];
            public Double3[] Set = new Double3[4];
            public double[] PeakMoment = new double[4], PeakAxial = new double[4];
            public readonly List<KeyValuePair<long, FlightEvent>> Events = new List<KeyValuePair<long, FlightEvent>>();
            public double MaxTilt, TouchSpeed;
            public int Tunnels, Crashes;

            /// <summary>The even split's peak load (the known-wrong): a quarter of the legs' load along them, N.</summary>
            public double EvenAxial;

            /// <summary>The flight's records and events, the legs' judged moment left out (FNV-1a 64).</summary>
            public ulong Hash;
            public FlightCore Core;

            public int Damaged(int from = 0, int to = 4)
            {
                int n = 0;
                for (int c = from; c < to; c++)
                    if ((Flags[c] & (LegFlags.Set | LegFlags.Cracked | LegFlags.Broken | LegFlags.Detached | LegFlags.SkidLoose)) != 0) n++;
                return n;
            }

            public bool Has(EventKind kind)
            {
                foreach (KeyValuePair<long, FlightEvent> e in Events)
                    if (e.Value.Kind == kind) return true;
                return false;
            }
        }

        static Quat Tilt(double rollDeg, double pitchDeg)
        {
            // As LandingScenarios: roll the right side (+x) down, a turn about +z by −roll; pitch the nose (+z) down, about +x.
            double r = -rollDeg * Math.PI / 360, p = pitchDeg * Math.PI / 360;
            return (new Quat(0, 0, Math.Sin(r), Math.Cos(r)) * new Quat(Math.Sin(p), 0, 0, Math.Cos(p))).Normalized();
        }

        static readonly int MomentAt = Offset("leg_moment.rear_right");

        static int Offset(string name)
        {
            foreach (LogField f in LogFormat.StepFields)
                if (f.Name == name) return f.Offset;
            throw new InvalidOperationException(name);
        }

        /// <summary>The flight's setup for a touch: the free body over the ground with its contacts at the nominal loadout.</summary>
        public static FlightSetup Setup(DroneSpec spec, MaterialRow[] materials, ulong hash, in Touch t)
        {
            Loadout loadout = spec.Nominal;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
            if (t.Elastic) contacts.Legs = null;
            ContactScenarios.Site site = t.Site ?? new ContactScenarios.Site(null, default, 0);
            Quat q = (site.Attitude() * Tilt(t.RollDeg, t.PitchDeg)).Normalized();
            double low = double.PositiveInfinity;
            foreach (ContactShape s in contacts.Shapes)
            {
                if (s.Role == ContactRole.Disc) continue;
                low = Math.Min(low, q.Rotate(s.A).Y - s.Radius);
                low = Math.Min(low, q.Rotate(s.B).Y - s.Radius);
            }
            return new FlightSetup
            {
                Seed = t.Seed, World = site.Open(), Mass = spec.BuildMass(loadout), Contacts = contacts,
                StartPosition = t.Start ?? site.Foot + new Double3(0, 0.001 - low, 0), StartAttitude = q, StartVelocity = t.Velocity,
                RuntimeObjects = t.Objects ?? new RuntimeObject[0],
            };
        }

        /// <summary>One touch.</summary>
        public static Outcome Run(DroneSpec spec, MaterialRow[] materials, ulong hash, in Touch t)
        {
            FlightSetup setup = Setup(spec, materials, hash, t);
            var core = new FlightCore(setup);
            var o = new Outcome { Core = core };
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong h = 0xCBF29CE484222325UL;
            int steps = (int)((t.Seconds > 0 ? t.Seconds : 2) * 1000);
            bool touched = false;
            for (int k = 0; k < steps; k++)
            {
                core.Advance(StickSample.Neutral);
                ref readonly FlightState s = ref core.State;
                ref readonly StepTrace tr = ref core.Trace;
                LogFormat.EncodeStep(record, s, StickSample.Neutral, tr);
                h = Fnv(h, record, 0, MomentAt);
                h = Fnv(h, record, MomentAt + 32, record.Length);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent ev = core.Events[i];
                    LogFormat.EncodeEvent(e, s.Step, ev);
                    h = Fnv(h, e, 0, e.Length);
                    if (ev.Kind == EventKind.Tunnel) o.Tunnels++;
                    if (ev.Kind == EventKind.Crash) o.Crashes++;
                    if (ev.Kind == EventKind.ContactMade && !touched)
                    {
                        touched = true;
                        o.TouchSpeed = ev.A;
                    }
                    if (ev.Kind >= EventKind.LegSet && ev.Kind <= EventKind.SkidLoose) o.Events.Add(new KeyValuePair<long, FlightEvent>(s.Step, ev));
                }
                Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
                double tilt = Math.Acos(Math.Max(-1, Math.Min(1, up.Y))) * 180 / Math.PI;
                o.MaxTilt = Math.Max(o.MaxTilt, tilt);
                for (int c = 0; c < 4; c++)
                {
                    o.PeakMoment[c] = Math.Max(o.PeakMoment[c], tr.LegMoment[c]);
                    o.PeakAxial[c] = Math.Max(o.PeakAxial[c], tr.LegLoad[c]);
                }
                // The even split of the legs' own load (1.3: once a broken leg's piece leaves, the frame's own impacts are not the
                // legs' to split).
                o.EvenAxial = Math.Max(o.EvenAxial, (tr.LegLoad[0] + tr.LegLoad[1] + tr.LegLoad[2] + tr.LegLoad[3]) / 4);
            }
            for (int c = 0; c < 4; c++)
            {
                o.Flags[c] = (LegFlags)(byte)(core.Trace.LegState >> (8 * c));
                o.Set[c] = core.Trace.LegSet[c];
            }
            o.Hash = h;
            return o;
        }

        /// <summary>A rigid steel box in the world: its centre, half-extents (world axes).</summary>
        public static RuntimeObject Box(FlightWorld world, string name, Double3 centre, Double3 half)
        {
            ushort steel = world.Query.MaterialId("steel");
            return new RuntimeObject(name, new[]
            {
                new ShapeGeometry
                {
                    Kind = ShapeKind.Box, Center = centre, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
                    HalfExtents = half, Material = steel,
                },
            });
        }

        /// <summary>The set angle of a set vector (its bend's magnitude), deg.</summary>
        public static double SetDegrees(Double3 set) => Math.Sqrt(set.X * set.X + set.Z * set.Z) * 180 / Math.PI;

        static ulong Fnv(ulong h, byte[] bytes, int from, int to)
        {
            for (int i = from; i < to; i++) h = unchecked((h ^ bytes[i]) * 0x100000001B3UL);
            return h;
        }
    }
}
