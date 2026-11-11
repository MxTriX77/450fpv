"""The lag probe's timed sim sources (L1, the model-79 lag): copies of three sim-core files with timing marks
inserted at fixed anchors, written under the build's obj folder. The copies compute exactly what the runtime does
(lagprobe's phases mode checks every step record bit for bit); they only read the clock. Each anchor must occur
exactly once, or this fails: a changed core needs the probe re-aimed, never silently mis-timed.

usage: python timed_sources.py <runtime folder> <output folder>
"""
import pathlib
import sys

MARK = "global::Fpv450.LagProbe.Probe.Mark(global::Fpv450.LagProbe.Phase.{});"

# (file, anchor, phase): the mark goes on its own line before the anchor, at the anchor's indentation.
MARKS = [
    ("Flight/FlightCore.cs", "            _windBatch?.Join(); // nothing evaluates the wind while its frame changes", "Wind"),
    ("Flight/FlightCore.cs", "            if (_fc == null) s.Armed = flown.Arm;", "Flight"),
    ("Flight/FlightCore.cs", "            if (_contacts != null) _contacts.Forces(ref s, body, ref force, ref moment, ref _trace, _events);", "ContactForces"),
    ("Flight/FlightCore.cs", "            if (_tether != null) TetherForce(s, body, ref force, ref moment);", "Body"),
    ("Flight/FlightCore.cs", "            if (_loose != null)\n            {\n                _loose.Step(_wind);", "Loose"),
    ("Flight/FlightCore.cs", "            if (_contacts != null) _contacts.Sense(was, ref s, _events);", "ContactSense"),
    ("Flight/FlightCore.cs", "            if (_strikes != null) _strikes.Apply(angles, ref s, body, _events);", "Strikes"),
    ("Flight/FlightCore.cs", "            if (_tether != null)\n            {\n                _tether.Step(s, _tetherHubs", "Tether"),
    ("Flight/FlightCore.cs", "            if (_windBatch != null) TraceWind();", "WindTrace"),
    ("Flight/FlightCore.cs", "            // The film takes the water the drops brought this step; the next step flies its mass.", "Rain"),
    ("Tether/FiberLine.cs", "            if (_contacts != null)\n            {\n                _contacts.Prepare(_chain, s, events);", "TetherPrepare"),
    ("Tether/FiberLine.cs", "            for (int i = 0; i < n; i++) _at[i] = new XZ(_chain[i].Position.X, _chain[i].Position.Z);", "TetherGround"),
    ("Tether/FiberLine.cs", "            if (_rotors != null)\n            {\n                Double3 up = q.Rotate(new Double3(0, 1, 0));", "TetherRotors"),
    ("Tether/FiberLine.cs", "            _windCursor = _windBatch != null ? _windFirst : -1;", "TetherLoads"),
    ("Tether/FiberLine.cs", "            _contacts?.Assign(_chain, _ground, _groundHeight, _groundNormal, s);", "TetherAssign"),
    ("Tether/FiberLine.cs", "            _chain.SetSpacing(_spacing, _nearLength, _growth, end.Material);", "TetherSolve"),
    ("Tether/FiberLine.cs", "            _contacts?.Finish(_chain, s, events);", "TetherFinish"),
    ("Tether/FiberLine.cs", "            if (_cuts != null && !Broken)\n            {\n                Cut(s, from, wedgeNode, events);", "TetherCut"),
    ("Tether/FiberLine.cs", "            if (Broken) flags |= (byte)TetherFlags.Broken;", "TetherRest"),
]

Q0 = "long probe = global::Fpv450.LagProbe.Probe.Q0(); "
Q1 = " global::Fpv450.LagProbe.Probe.Q1(probe, global::Fpv450.LagProbe.Query.{});"

# (file, the call as written, the query kind, returns a value): the world's door, each call timed.
QUERIES = [
    ("public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _query.SampleGround(points, results);", "Ground", False),
    ("public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results) =>\n            _query.StaticContacts(previous, current, margin, results);", "Static", True),
    ("public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits) => _query.Raycast(rays, maxDistance, hits);", "Raycast", False),
    ("public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results, QueryFilter filter) =>\n            _query.StaticContacts(previous, current, margin, results, filter);", "Static", True),
    ("public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results) =>\n            _query.MicroDetailNear(centre, radius, kinds, results);", "Micro", True),
    ("public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results) => _query.SnagsNear(centre, radius, results);", "Snags", True),
    ("public int WiresNear(Double3 centre, double radius, Span<Conductor> results) => _query.WiresNear(centre, radius, results);", "Wires", True),
    ("public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _query.Geometry(obj, shape, out geometry);", "Geometry", True),
    ("public bool ShapeContact(int obj, int shape, in Capsule previous, in Capsule current, double margin, out StaticContact contact) =>\n            _query.ShapeContact(obj, shape, previous, current, margin, out contact);", "Shape", True),
    ("_query.Raycast(ray.Slice(0, 1), SoilReach, hit.Slice(0, 1));", "Soil", None),
]


def once(text, anchor, name):
    n = text.count(anchor)
    if n != 1:
        sys.exit(f"timed_sources: {name}: the anchor occurs {n} times, not once: {anchor.splitlines()[0].strip()}")


def timed_query(anchor, kind, returns):
    if returns is None:  # a statement inside SoilUnder
        return "{ " + Q0 + anchor + Q1.format(kind) + " }"
    head, call = anchor.split("=>")
    call = call.strip().rstrip(";")
    if returns:
        ret = head.split()[1]
        body = Q0 + f"{ret} result = {call};" + Q1.format(kind) + " return result;"
    else:
        body = Q0 + call + ";" + Q1.format(kind)
    return head.rstrip() + " { " + body + " }"


def main():
    runtime, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    texts = {f: (runtime / f).read_text(encoding="utf-8-sig") for f in ("Flight/FlightCore.cs", "Tether/FiberLine.cs", "Flight/FlightWorld.cs")}
    for f, anchor, phase in MARKS:
        once(texts[f], anchor, f)
        indent = anchor[: len(anchor) - len(anchor.lstrip(" "))]
        texts[f] = texts[f].replace(anchor, indent + MARK.format(phase) + "\n" + anchor)
    for anchor, kind, returns in QUERIES:
        once(texts["Flight/FlightWorld.cs"], anchor, "Flight/FlightWorld.cs")
        texts["Flight/FlightWorld.cs"] = texts["Flight/FlightWorld.cs"].replace(anchor, timed_query(anchor, kind, returns))
    for f, text in texts.items():
        (out / (pathlib.Path(f).stem + ".Timed.cs")).write_text("// Generated by tools/lagprobe/timed_sources.py from " + f + "; do not edit.\n" + text, encoding="utf-8")


if __name__ == "__main__":
    main()
