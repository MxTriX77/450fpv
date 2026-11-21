using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.World;

// QA probe of the laid line (p7d-tether 1.11h): replays one of the pilot's flights through FlightCore on its logged sticks,
// checks every step's record against the log bit for bit (so every number is the flight's own; the probe's hooks in the
// scratch Runtime copy only read), and writes, by the flight's tag (never its file name):
//   <tag>.trail.txt  every trail freeze (with the node's supports), lift (with Pulled's terms or the air's), thaw, break
//   <tag>.snap.txt   every --every ms: the drone, and every chain node (s, place, height over the ground, contacts, tension, air)
//   <tag>.disc.txt   every episode of a chain segment inside a disc's slab between hub and tip, and the cut
//   <tag>.end.txt    the trail at the end
//   <tag>.shapes.txt every world shape the line touched or rested on: its object, geometry and material
// Args: <repo> <flights dir> <seed hex> <tag> <out dir> [--every MS] [--to S]
static class Program
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;
    static MapWorld Map;
    static LogHeader H;
    static FiberLine Line;
    static FlightCore Core;
    static StreamWriter Tr, Sn, Di;
    static readonly HashSet<(int, int)> Shapes = new HashSet<(int, int)>();
    static readonly string[] Corner = { "RR", "FR", "RL", "FL" };
    static readonly XZ[] At1 = new XZ[1];
    static readonly GroundSample[] G1 = new GroundSample[1];

    static double Ground(Double3 p) { At1[0] = new XZ(p.X, p.Z); Map.SampleGround(At1, G1); return G1[0].Height; }
    static string F(Double3 v) => $"({v.X:0.000},{v.Y:0.000},{v.Z:0.000})";

    static int Main(string[] args)
    {
        try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.Idle; } catch { }
        string root = args[0], dir = args[1], tag = args[3], outDir = args[4];
        Tag = tag;
        OutDir = outDir;
        ulong seed = ulong.Parse(args[2].Replace("0x", ""), NumberStyles.HexNumber, C);
        long every = Opt(args, "--every") is string ev ? long.Parse(ev, C) : 100;
        long toStep = Opt(args, "--to") is string ts ? (long)(double.Parse(ts, C) * 1000) : long.MaxValue;
        if (Opt(args, "--trace") is string trs)
        {
            string[] p = trs.Split('-');
            traceA = (long)Math.Round(double.Parse(p[0], C) * 1000);
            traceB = (long)Math.Round(double.Parse(p[1], C) * 1000);
        }
        string log = null;
        foreach (FileInfo f in new DirectoryInfo(dir).GetFiles("*.flightlog").OrderByDescending(f => f.LastWriteTimeUtc).Take(40))
        {
            try { using FlightLogReader r = FlightLogReader.Open(f.FullName); if (r.Header.Seed == seed) { log = f.FullName; H = r.Header; break; } }
            catch { }
        }
        if (log == null) { Console.WriteLine($"{tag}: no log with seed 0x{seed:X16}"); return 2; }
        Directory.CreateDirectory(outDir);
        Map = MapWorld.Load(root, H.WorldId);
        var world = new FlightWorld(Map);
        var problems = new List<string>();
        FlightSetup setup = H.ToSetup(world, problems);
        if (problems.Count > 0) { Console.WriteLine("setup problems: " + string.Join("; ", problems)); return 3; }
        Core = new FlightCore(setup);
        using Fpv450.Sim.Wind.WindField air = Core.Wind;
        Line = Core.Tether;
        using var tr = Tr = new StreamWriter(Path.Combine(outDir, tag + ".trail.txt"));
        using var sn = Sn = new StreamWriter(Path.Combine(outDir, tag + ".snap.txt"));
        using var di = Di = new StreamWriter(Path.Combine(outDir, tag + ".disc.txt"));
        string head = $"{tag} seed 0x{H.Seed:X16} model {H.ModelVersion} world {H.WorldId} wind {H.Wind.Level} rain {H.Rain} payload {H.Loadout.Payload:0.###} kg";
        Console.WriteLine(head);
        Tr.WriteLine(head);
        Sn.WriteLine(head);
        Di.WriteLine(head);
        if (Line == null) { Console.WriteLine("no line"); return 4; }
        Tr.WriteLine($"line: mass/m {Line.MassPerMetre:G5} kg/m, diameter {Line.Diameter:G4} m, weight {Line.QaWeightPerMetre:G5} N/m, trail hold mu_s*w {Line.QaHoldPerMetre:G5} N/m, hub r {Line.QaHubRadius:G4} rotor r {Line.QaRotorRadius:G4} disc half {Line.QaDiscHalf(0):G4} m, blades {Line.QaBlades}");
        Di.WriteLine($"discs: hub r {Line.QaHubRadius:G4} m, rotor r {Line.QaRotorRadius:G4} m, half thickness {Line.QaDiscHalf(0):G4} m");

        FiberLine.QaFreeze = (fl, a) =>
        {
            Double3 p = a.Position;
            Tr.WriteLine($"{Step + 1} freeze idx {fl.TrailCount} s {a.Material:0.000} at {F(p)} agl {p.Y - Ground(p):0.000} |v| {a.Velocity.Length():0.0000} rest {a.RestTime:0.000} still {a.StillTime:0.000} heldT {fl.Chain.Tension(1):G4} N | {Slot(a.Contact0, p)} | {Slot(a.Contact1, p)}{(fl.TrailCount > 0 ? Through(fl.Trail[fl.TrailCount - 1], p) : "")}");
        };
        FiberLine.QaLift = (fl, why) => Tr.WriteLine($"{Step + 1} lift idx {fl.TrailCount - 1} paid {fl.PaidOut:0.0} exitT {fl.ExitTension:G4} drone {F(Core.State.Position)} | {why}");
        FiberLine.QaThaw = (fl, why) => Tr.WriteLine($"{Step + 1} {why} drone {F(Core.State.Position)}");

        var recorded = new byte[LogFormat.StepSize];
        using FlightLogReader reader = FlightLogReader.Open(log);
        bool broke = false;
        var ep = new Episode[4];
        long steps = 0;
        while (reader.Next())
        {
            if (reader.Kind != LogFormat.StepBlock) continue;
            ReadOnlySpan<byte> payload = reader.Payload;
            LogFormat.DecodeStick(payload, out StickSample stick);
            Corners angleFrom = Core.State.RotorAngle;
            Core.Advance(stick);
            LogFormat.EncodeStep(recorded, Core.State, stick, Core.Trace);
            long k = Core.State.Step;
            steps = k;
            if (!payload.SequenceEqual(recorded))
            {
                Console.WriteLine($"{tag}: DIVERGED from the log at step {k}: stop");
                Tr.WriteLine($"DIVERGED at step {k}");
                return 1;
            }
            if (!broke || Line.BrokeThisStep) Discs(k, ep, angleFrom);
            if (Line.BrokeThisStep && !broke)
            {
                broke = true;
                FiberBreak b = Line.LastBreak;
                Tr.WriteLine($"{k} BREAK {b.Mechanism} at {(FiberMember)b.Kind} member {b.Member} s {b.Material:0.000} T {b.Tension:G4} N point {F(b.Point)}; trail {Line.TrailBeforeBreak} -> {Line.TrailCount}");
                for (int i = Line.TrailBeforeBreak; i < Line.TrailCount; i++)
                    Tr.WriteLine($"{k} breakpush idx {i} s {Line.TrailMaterial(i):0.000} at {F(Line.Trail[i])} agl {Line.Trail[i].Y - Ground(Line.Trail[i]):0.000}");
                Snap(k, stick);
            }
            if (k % every == 0 && (!broke || k % 1000 == 0)) Snap(k, stick);
            if (k >= traceA && k <= traceB) TraceStep(k, stick, angleFrom);
            if (k >= toStep) break;
        }
        for (int c = 0; c < 4; c++) if (ep[c].Active) Close(ep[c], c, steps, false);
        using (var en = new StreamWriter(Path.Combine(outDir, tag + ".end.txt")))
        {
            en.WriteLine(head);
            en.WriteLine($"end step {steps}: line {(Line.Broken ? $"broken at {Line.BrokenAt} ({Line.LastBreak.Mechanism})" : "whole")}, paid {Line.PaidOut:0.0} m, trail {Line.TrailCount}, thaws {Line.Thaws} ({Line.Thawed} points), contact nodes put {Line.Contacts?.Inserted} refused {Line.Contacts?.Refused}, chain splits refused {Line.Chain.SplitsRefused}");
            for (int i = 0; i < Line.TrailCount; i++)
            {
                Double3 p = Line.Trail[i];
                en.WriteLine($"idx {i} s {Line.TrailMaterial(i):0.000} at {F(p)} agl {p.Y - Ground(p):0.000} held {Line.TrailHeld(i):G4} froze {Line.TrailAir(i):G4}");
            }
        }
        using (var sh = new StreamWriter(Path.Combine(outDir, tag + ".shapes.txt")))
            foreach (var (o, s) in Shapes.OrderBy(x => x))
            {
                string geo = "";
                if (Map.Geometry(o, s, out ShapeGeometry g))
                    geo = g.Kind == ShapeKind.Box
                        ? $"box half ({g.HalfExtents.X:0.000},{g.HalfExtents.Y:0.000},{g.HalfExtents.Z:0.000}) at {F(g.Center)} axisY {F(g.AxisY)} ground {Ground(g.Center):0.000} mat {H.MaterialName(g.Material)}"
                        : $"{g.Kind} r {g.Radius:0.000} half {g.HalfLength:0.000} at {F(g.Center)} axisY {F(g.AxisY)} ground {Ground(g.Center):0.000} mat {H.MaterialName(g.Material)}";
                sh.WriteLine($"obj {o} '{Map.ObjectName(o)}' shape {s} {geo}");
            }
        Console.WriteLine($"{tag}: replayed {steps} steps bit for bit; line {(Line.Broken ? $"broken at {Line.BrokenAt * 0.001:0.000} s, {Line.LastBreak.Mechanism}" : "whole")}; trail {Line.TrailCount}; thaws {Line.Thaws}");
        return 0;
    }

    static long Step => Core.State.Step;
    static long traceA = -1, traceB = -1;
    static string Tag, OutDir;
    static StreamWriter Tc;

    // One step in detail (--trace): the drone, each rotor's sweep this step, every segment within 0.3 m of a hub with the
    // prop check's own terms (the slab, the radius, the crossing's azimuth and each blade's lead to it), the nodes within
    // 0.6 m of the drone and their slots.
    static void TraceStep(long k, in StickSample st, in Corners from)
    {
        Tc ??= new StreamWriter(Path.Combine(OutDir, Tag + ".trace.txt"));
        ref readonly FlightState s = ref Core.State;
        Quat q = s.Attitude;
        Double3 up = q.Rotate(new Double3(0, 1, 0));
        CableChain ch = Line.Chain;
        int n = ch.Count;
        const double TwoPi = 2 * Math.PI;
        Tc.WriteLine($"T {k} drone {F(s.Position)} v {F(s.Velocity)} up {up.Y:0.000} rate {F(s.BodyRate)} stk T {st.Throttle:0.00} P {st.Pitch:0.00} R {st.Roll:0.00} Y {st.Yaw:0.00} exitT {Line.ExitTension:G4} paid {Line.PaidOut:0.0000} nodes {n} broken {(Line.Broken ? 1 : 0)}{(Line.BrokeThisStep ? $" BREAK {Line.LastBreak.Mechanism} T {Line.LastBreak.Tension:G4} s {Line.LastBreak.Material:0.000} seg {Line.LastBreak.Segment}" : "")}");
        for (int c = 0; c < 4; c++)
        {
            Double3 hub = Line.QaHub(c);
            double sweep = s.RotorAngle[c] - from[c];
            if (sweep < 0) sweep += TwoPi;
            string rotor = $"  {Corner[c]} omega {s.RotorSpeed[c]:0.0} resting {(Line.QaResting(c) ? 1 : 0)} angle {from[c]:0.000}->{s.RotorAngle[c]:0.000} sweep {sweep:0.000} tips {Line.QaTip(c, 0):0.000}/{Line.QaTip(c, 1):0.000}";
            var segs = new List<string>();
            for (int j = 0; j < n - 1; j++)
            {
                Double3 pa = ch[j].Position, pb = ch[j + 1].Position, d = pb - pa;
                double l2 = Double3.Dot(d, d), f0 = l2 > 0 ? Math.Clamp(Double3.Dot(hub - pa, d) / l2, 0, 1) : 0;
                if ((pa + d * f0 - hub).Length() > 0.3) continue;
                double za = Double3.Dot(pa - hub, up), zb = Double3.Dot(pb - hub, up), h = Line.QaDiscHalf(c);
                string x = $"seg {j} s {ch[j].Material:0.000}-{ch[j + 1].Material:0.000} za {za * 1000:0.0} zb {zb * 1000:0.0} mm T {ch.Tension(j):G4}";
                if (!(za > h && zb > h || za < -h && zb < -h))
                {
                    double f = za * zb < 0 ? za / (za - zb) : Math.Abs(za) <= Math.Abs(zb) ? 0 : 1;
                    Double3 p = pa + d * f, rel = p - hub;
                    rel = rel - up * Double3.Dot(rel, up);
                    double r = rel.Length();
                    Double3 v = q.InverseRotate(rel);
                    double psi = Math.Atan2(Line.QaSpin(c) * v.Z, v.X);
                    while (psi < 0) psi += TwoPi;
                    string ahead = "";
                    for (int bl = 0; bl < Line.QaBlades; bl++)
                    {
                        double a = psi - (from[c] + TwoPi * bl / Line.QaBlades);
                        while (a >= TwoPi) a -= TwoPi;
                        while (a < 0) a += TwoPi;
                        ahead += $" b{bl} ahead {a:0.000}{(a > 0 && a <= sweep ? (r <= Line.QaTip(c, bl) * Line.QaRotorRadius ? " HIT" : " (past its tip)") : "")}";
                    }
                    x += $" IN SLAB r {r * 1000:0.0} mm psi {psi:0.000}{ahead}";
                }
                segs.Add(x);
            }
            Tc.WriteLine(rotor + (segs.Count > 0 ? " | " + string.Join(" | ", segs) : ""));
        }
        for (int i = 0; i < n; i++)
        {
            ref CableNode a = ref ch[i];
            Double3 rel = q.InverseRotate(a.Position - s.Position);
            if (rel.Length() > 0.6) continue;
            Tc.WriteLine($"  n{i} s {a.Material:0.0000} body {F(rel)} |v| {a.Velocity.Length():0.000} {(i < n - 1 ? $"T {ch.Tension(i):G4}" : "exit")} | {Slot(a.Contact0, a.Position)} | {Slot(a.Contact1, a.Position)}");
        }
        Tc.Flush();
    }

    static string Opt(string[] a, string name) { int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

    static string Member(in CableContact c)
    {
        if (c.MemberKind == 0) return "-";
        if (c.MemberKind > (byte)FiberMember.Part) return ((FiberMember)c.MemberKind).ToString();
        FlightEvent e = FiberLine.Place(EventKind.FiberContact, c.MemberKind, c.Member, Line.Contacts, default, 0, 0, 0);
        string k = ((FiberMember)c.MemberKind).ToString();
        if (c.MemberKind == (byte)FiberMember.Part)
            return $"{k}:{(H.Contacts != null && e.Shape >= 0 && e.Shape < H.Contacts.Shapes.Length ? H.Contacts.Shapes[e.Shape].Name : "shape" + e.Shape)}";
        if (c.MemberKind == (byte)FiberMember.Ground || e.Object < 0) return k;
        if (c.MemberKind == (byte)FiberMember.Shape) Shapes.Add((e.Object, e.Shape));
        return $"{k}:{e.Object}/{e.Shape}:{Map.ObjectName(e.Object)}";
    }

    static string Slot(in CableContact c, Double3 p)
    {
        if (!c.Active) return "-";
        double gap = Double3.Dot(p - c.Point, c.Normal);
        return $"{Member(c)} N {c.NormalForce:G4} gap {gap:G3} n ({c.Normal.X:0.00},{c.Normal.Y:0.00},{c.Normal.Z:0.00}) edge {c.Edge:G3} cap {(c.Capstan ? 1 : 0)} sliding {(c.Sliding ? 1 : 0)} slid {(c.Slid ? 1 : 0)} mu {c.StaticFriction:G3}";
    }

    static void Snap(long k, in StickSample st)
    {
        ref readonly FlightState s = ref Core.State;
        Double3 p = s.Position, v = s.Velocity;
        double up = s.Attitude.Rotate(new Double3(0, 1, 0)).Y;
        CableChain ch = Line.Chain;
        int n = ch.Count;
        Sn.WriteLine($"S {k} t {k * 0.001:0.000} drone {F(p)} agl {p.Y - Ground(p):0.00} v {F(v)} up {up:0.000} stk T {st.Throttle:0.00} P {st.Pitch:0.00} R {st.Roll:0.00} Y {st.Yaw:0.00} exitT {Line.ExitTension:G4} paid {Line.PaidOut:0.000} nodes {n} trail {Line.TrailCount} thaws {Line.Thaws} broken {(Line.Broken ? 1 : 0)} wind {F(Core.Trace.Wind)}");
        if (Line.Broken && !Line.BrokeThisStep) return;
        for (int i = 0; i < n; i++)
        {
            ref CableNode a = ref ch[i];
            Double3 x = a.Position;
            string load = i < Line.LoadCount ? $" air {F(Line.LoadAir(i))} drag {F(Line.LoadForce(i) * 1000)}mN" : "";
            string seg = i < n - 1 ? $"T {ch.Tension(i):G4} rest {ch[i + 1].Material - a.Material:0.000} len {(ch[i + 1].Position - x).Length():0.000}" : "exit";
            string thru = i < n - 2 ? Through(x, ch[i + 1].Position) : "";
            Sn.WriteLine($"N {k} {i} s {a.Material:0.000} at {F(x)} agl {x.Y - Ground(x):0.000} |v| {a.Velocity.Length():0.000} {seg} rest {a.RestTime:0.00}{(a.AnchorStiffness > 0 ? " anchor" : "")}{(a.Crest ? " crest" : "")}{(a.Lifted ? " lifted" : "")}{load} | {Slot(a.Contact0, x)} | {Slot(a.Contact1, x)}{thru}");
        }
    }

    static readonly Ray[] Rays = new Ray[2];
    static readonly RayHit[] Hits = new RayHit[2];

    // The segment a–b inside a world object (not the ground): a ray each way along it; both meeting one object with
    // da + db < len - 1 cm, the segment runs through it for len − da − db (the straight chord between two nodes).
    static string Through(Double3 a, Double3 b)
    {
        Double3 d = b - a;
        double len = d.Length();
        if (!(len > 0.03)) return "";
        Double3 u = d * (1 / len);
        Rays[0] = new Ray(a + u * 0.005, u);
        Rays[1] = new Ray(b - u * 0.005, u * -1.0);
        Map.Raycast(Rays, len - 0.01, Hits);
        RayHit h0 = Hits[0], h1 = Hits[1];
        string res = "";
        double in0 = h0.Object >= 0 && h0.Distance < len ? Inside(h0.Object, h0.Shape, a, b) : 0;
        double in1 = h1.Object >= 0 && h1.Distance < len && (h1.Object != h0.Object || h1.Shape != h0.Shape) ? Inside(h1.Object, h1.Shape, a, b) : 0;
        if (in0 > 0.01) { Shapes.Add((h0.Object, h0.Shape)); res += $" | THRU {h0.Object}/{h0.Shape}:{Map.ObjectName(h0.Object)} in {in0:0.000} m from {h0.Distance + 0.005:0.000} of {len:0.000}"; }
        if (in1 > 0.01) { Shapes.Add((h1.Object, h1.Shape)); res += $" | THRU {h1.Object}/{h1.Shape}:{Map.ObjectName(h1.Object)} in {in1:0.000} m from {len - h1.Distance - 0.005:0.000} of {len:0.000}"; }
        return res;
    }

    // How much of the chord a–b lies inside shape (o, s), m: sampled every 5 mm against its own geometry (a box in its
    // axes, a sphere, a capsule or a cylinder about AxisY); 0 for another kind.
    static double Inside(int o, int sh, Double3 a, Double3 b)
    {
        if (!Map.Geometry(o, sh, out ShapeGeometry g)) return 0;
        Double3 d = b - a;
        double len = d.Length();
        int n = Math.Max(2, (int)(len / 0.005));
        int inside = 0;
        for (int i = 0; i <= n; i++)
        {
            Double3 p = a + d * ((double)i / n) - g.Center;
            bool hit;
            switch (g.Kind)
            {
                case ShapeKind.Box:
                    hit = Math.Abs(Double3.Dot(p, g.AxisX)) < g.HalfExtents.X && Math.Abs(Double3.Dot(p, g.AxisY)) < g.HalfExtents.Y && Math.Abs(Double3.Dot(p, g.AxisZ)) < g.HalfExtents.Z;
                    break;
                case ShapeKind.Sphere:
                    hit = p.Length() < g.Radius;
                    break;
                case ShapeKind.Capsule:
                {
                    double y = Math.Clamp(Double3.Dot(p, g.AxisY), -g.HalfLength, g.HalfLength);
                    hit = (p - g.AxisY * y).Length() < g.Radius;
                    break;
                }
                case ShapeKind.Cylinder:
                {
                    double y = Double3.Dot(p, g.AxisY);
                    hit = Math.Abs(y) < g.HalfLength && (p - g.AxisY * y).Length() < g.Radius;
                    break;
                }
                default:
                    return 0;
            }
            if (hit) inside++;
        }
        return len * inside / (n + 1);
    }

    struct Episode { public bool Active; public long Start, Last; public int Steps, Reach, Pass, Both, RestSteps; public double MinR, MaxT, MinTip; public bool Resting, Spinning; public string First; }

    // A chain segment inside a disc's slab between hub and tip (the prop check's geometry, without the blade's pass).
    static void Discs(long k, Episode[] ep, in Corners from)
    {
        const double TwoPi = 2 * Math.PI;
        Quat qa = Core.State.Attitude;
        ref readonly FlightState s = ref Core.State;
        CableChain ch = Line.Chain;
        int n = ch.Count;
        Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
        for (int c = 0; c < 4; c++)
        {
            Double3 hub = Line.QaHub(c);
            double h = Line.QaDiscHalf(c), omega = s.RotorSpeed[c];
            bool met = false, reach = false, pass = false, both = false;
            double bestR = double.MaxValue, sweep = s.RotorAngle[c] - from[c];
            if (sweep < 0) sweep += TwoPi;
            double tipMax = 0;
            for (int bl = 0; bl < Line.QaBlades; bl++) tipMax = Math.Max(tipMax, Line.QaTip(c, bl));
            string what = null;
            for (int j = 0; j < n - 1; j++)
            {
                Double3 pa = ch[j].Position, pb = ch[j + 1].Position;
                double za = Double3.Dot(pa - hub, up), zb = Double3.Dot(pb - hub, up);
                if (za > h && zb > h || za < -h && zb < -h) continue;
                double f = za * zb < 0 ? za / (za - zb) : Math.Abs(za) <= Math.Abs(zb) ? 0 : 1;
                Double3 q = pa + (pb - pa) * f, rel = q - hub;
                rel = rel - up * Double3.Dot(rel, up);
                double r = rel.Length();
                if (!(r >= Line.QaHubRadius) || !(r <= Line.QaRotorRadius)) continue;
                met = true;
                Double3 vb = qa.InverseRotate(rel);
                double psi = Math.Atan2(Line.QaSpin(c) * vb.Z, vb.X);
                while (psi < 0) psi += TwoPi;
                for (int bl = 0; bl < Line.QaBlades; bl++)
                {
                    double ah = psi - (from[c] + TwoPi * bl / Line.QaBlades);
                    while (ah >= TwoPi) ah -= TwoPi;
                    while (ah < 0) ah += TwoPi;
                    bool inReach = r <= Line.QaTip(c, bl) * Line.QaRotorRadius, passes = ah > 0 && ah <= sweep;
                    reach |= inReach;
                    pass |= passes;
                    both |= inReach && passes;
                }
                if (r < bestR)
                {
                    bestR = r;
                    what = $"seg {j}/{n - 1} s {ch[j].Material + (ch[j + 1].Material - ch[j].Material) * f:0.000} r {r:0.0000} T {ch.Tension(j):G4} omega {omega:0} resting {(Line.QaResting(c) ? 1 : 0)}";
                }
            }
            bool cut = Line.BrokeThisStep && Line.LastBreak.Mechanism == TetherMechanism.Prop && (int)Line.LastBreak.Member == c;
            if (met)
            {
                if (!ep[c].Active) ep[c] = new Episode { Active = true, Start = k, MinR = double.MaxValue, MinTip = double.MaxValue, First = what };
                ep[c].Last = k;
                ep[c].Steps++;
                if (bestR < ep[c].MinR) ep[c].MinR = bestR;
                if (Line.QaResting(c)) { ep[c].Resting = true; ep[c].RestSteps++; } else if (omega > 0) ep[c].Spinning = true;
                if (reach) ep[c].Reach++;
                if (pass) ep[c].Pass++;
                if (both) ep[c].Both++;
                if (tipMax < ep[c].MinTip) ep[c].MinTip = tipMax;
            }
            if (cut)
            {
                if (!ep[c].Active) ep[c] = new Episode { Active = true, Start = k, Last = k, MinR = bestR, MinTip = tipMax, First = what ?? "(not met at the step's end)" };
                Close(ep[c], c, k, true);
                ep[c].Active = false;
            }
            else if (ep[c].Active && !met && k - ep[c].Last > 0)
            {
                Close(ep[c], c, k, false);
                ep[c].Active = false;
            }
        }
    }

    static void Close(in Episode e, int c, long k, bool cut)
    {
        Di.WriteLine($"{Corner[c]} meet from {e.Start} ({e.Start * 0.001:0.000} s) to {e.Last} ({e.Steps} steps in the slab), {(cut ? $"CUT at {k} ({k * 0.001:0.000} s), {k - e.Start} ms after the first" : "no cut")}; resting {(e.Resting ? 1 : 0)} spinning {(e.Spinning ? 1 : 0)} least r {e.MinR:0.0000}; steps within a blade's reach {e.Reach}, a blade passing {e.Pass}, both {e.Both}, rotor resting {e.RestSteps}, longest blade {e.MinTip:0.000} R; first: {e.First}");
    }
}
