using System;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

namespace Fpv450.App.Session
{
    /// <summary>
    /// Whether a flight's log ever went into the ground (w3-farmland-ground 3.1, "no step tunnels into the ground beyond the
    /// contact tolerance"), read from the log alone, as headless as its replay: its Tunnel events (the contact model's own
    /// test, a physics bug), and at every step each contact shape of the drone, at the step's pose, measured against the
    /// world's ground under each of its core's ends as a support's sample is: r − n_y·(y − h) past the facet's plane
    /// (design 2.2's Sunk). The shapes are the header's, rigid on the body: a leg's support and skid ride their spring, so
    /// theirs is an upper bound by the leg's compression; the body's parts (the frame, the motors, the leg tubes, the
    /// payload) are where they are.
    /// </summary>
    public static class GroundCheck
    {
        public struct Result
        {
            public long Steps;
            public int Tunnels;

            /// <summary>The deepest a body part and a support or skid went past the ground, m (≤ 0: never touched), by which
            /// shape and at which step.</summary>
            public double BodyDepth, LegDepth;
            public string BodyBy, LegBy;
            public long BodyStep, LegStep;

            /// <summary>The lowest and highest ground under the drone over the flight, and the flight's path length, m.</summary>
            public double LowestGround, HighestGround, Path;

            public override string ToString() =>
                Steps + " steps, " + Path.ToString("0") + " m flown over ground of " + LowestGround.ToString("0.0") + "–" + HighestGround.ToString("0.0") + " m; "
                + Tunnels + " tunnel events; the deepest past the ground: the body " + Mm(BodyDepth) + " (" + BodyBy + ", t = " + (BodyStep * FlightCore.Dt).ToString("0.000")
                + " s), the legs " + Mm(LegDepth) + " (" + LegBy + ", t = " + (LegStep * FlightCore.Dt).ToString("0.000") + " s; rigid, their compression included)";

            static string Mm(double m) => (m * 1e3).ToString("0.00") + " mm";
        }

        /// <summary>Above this height over the ground (m) no shape of the drone can reach it: its steps are not measured.</summary>
        const double Reach = 2;

        public static Result Read(string logPath, IWorldQuery world)
        {
            using (FlightLogReader reader = FlightLogReader.Open(logPath))
            {
                ContactShape[] shapes = reader.Header.Contacts?.Shapes ?? throw new ArgumentException("the log carries no contact shapes");
                var r = new Result
                {
                    BodyDepth = double.NegativeInfinity, LegDepth = double.NegativeInfinity, BodyBy = "none", LegBy = "none",
                    LowestGround = double.PositiveInfinity, HighestGround = double.NegativeInfinity,
                };
                var at = new XZ[1];
                var ground = new GroundSample[1];
                bool any = false;
                Double3 last = default;
                while (reader.Next())
                {
                    if (reader.Kind == LogFormat.EventBlock)
                    {
                        reader.ReadEvent(out LoggedEvent e);
                        if (e.Event.Kind == EventKind.Tunnel) r.Tunnels++;
                        continue;
                    }
                    if (reader.Kind != LogFormat.StepBlock) continue;
                    reader.ReadStep(out LoggedStep step);
                    r.Steps++;
                    Double3 p = step.State.Position;
                    if (any) r.Path += (p - last).Length();
                    last = p;
                    any = true;
                    at[0] = new XZ(p.X, p.Z);
                    world.SampleGround(at, ground);
                    r.LowestGround = Math.Min(r.LowestGround, ground[0].Height);
                    r.HighestGround = Math.Max(r.HighestGround, ground[0].Height);
                    if (p.Y - ground[0].Height > Reach) continue;
                    foreach (ContactShape s in shapes)
                    {
                        if (s.Role == ContactRole.Disc) continue;
                        bool leg = s.Role == ContactRole.Support || s.Role == ContactRole.Skid;
                        for (int end = 0; end < 2; end++)
                        {
                            Double3 x = p + step.State.Attitude.Rotate(end == 0 ? s.A : s.B);
                            at[0] = new XZ(x.X, x.Z);
                            world.SampleGround(at, ground);
                            double past = s.Radius - ground[0].Normal.Y * (x.Y - ground[0].Height);
                            if (leg && past > r.LegDepth)
                            {
                                r.LegDepth = past;
                                r.LegBy = s.Name;
                                r.LegStep = step.State.Step;
                            }
                            else if (!leg && past > r.BodyDepth)
                            {
                                r.BodyDepth = past;
                                r.BodyBy = s.Name;
                                r.BodyStep = step.State.Step;
                            }
                        }
                    }
                }
                return r;
            }
        }
    }
}
