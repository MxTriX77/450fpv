"""generators/trench and lib/holes (p1-e01-trench, task 1.1): the committed E01 params, check's refusals, the hole
contract on the built E01 (sealed from below on a 5 cm grid over the bands and 0.71 m past them, the cavity's faces,
the crumbled lip and the niches 0.65 m inside the bands, level lips, nothing proud, thickness and the 0.2 m overlaps),
the cross-section gaps' inscribed circle, the holes records and the shapes' surfaces."""
import copy
import json
import math
from pathlib import Path

import pytest

from generators import trench
from lib import audit, holes, meta, params as params_lib

VISUALS = {"ground": "belt_straw", "wall": "trench_wall", "clod": "trench_wall", "section": "trench_wall",
           "straw": "straw"}
REPO_ROOT = Path(__file__).resolve().parents[3]  # module-scoped fixtures cannot take conftest's repo_root


@pytest.fixture(scope="module")
def e01_params():
    return json.loads((REPO_ROOT / "assets-src" / "earthworks" / "E01_curving.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def e01(e01_params):
    """E01 built once: (params, built, metadata, run)."""
    built = trench.build(e01_params)
    data = meta.assemble("E01", "E01_curving", "trench", e01_params, built, VISUALS)
    return e01_params, built, data, trench.Run(e01_params)


def test_committed_params_validate(e01_params):
    assert params_lib.validate(trench, e01_params) == []


@pytest.mark.parametrize("edit, expected", [
    (lambda p: p.update(bottom_w_m=0.8, top_w_m=0.7), "'bottom_w_m' = 0.8 is wider than the lip 'top_w_m' = 0.7"),
    (lambda p: p.update(batter_deg=74.0), "'batter_deg' = 74.0 is outside [75.0, 90.0]"),
    (lambda p: p.update(batter_deg=80.0), "'batter_deg' = 80.0 disagrees with the widths and depth, which give 86.19"),
    (lambda p: p["parapet_h_m"].update(field=0.2, belt=0.1), "the parapets hold 0.626 m3/m"),
    (lambda p: p.update(shape="zigzag"), "'shape' = 'zigzag' is not built yet"),
    (lambda p: p.update(shape="spiral"), "'shape' = 'spiral' must be one of straight, zigzag, curving"),
    (lambda p: p.update(revetment="boards"), "'revetment' = 'boards' is E02's"),
    (lambda p: p.update(collapse=0.5), "'collapse' = 0.5 is E03's"),
    (lambda p: p.update(age=0.2), "'age' = 0.2 is the fresh state"),
    (lambda p: p["lod"].update(lod2_switch_m=5.0), "'lod.lod1_switch_m' < 'lod.lod2_switch_m' < 'lod.cull_m' must hold"),
    (lambda p: p.update(bend_deg=40.0, bend_every_m=3.0), "bends of 40.0 +- 5.0 degrees"),
])
def test_check_refuses(e01_params, edit, expected):
    p = copy.deepcopy(e01_params)
    edit(p)
    problems = params_lib.validate(trench, p)
    assert any(expected in problem for problem in problems), problems


def test_hole_contract_holds(e01):
    """F-1...F-8 as lib.holes checks them on E01: no ray of the 5 cm grid over the bands and 0.71 m past them misses a
    filler, every face of the cut (the crumbled lip and the niches included) at least 0.65 m inside the bands, the
    fillers' top level with the ground along the bands' edges, none proud, every one 0.3 m thick, the surfaces."""
    _, built, data, _ = e01
    problems, lines = holes.audit(data, built["lods"][0])
    assert problems == [], problems
    text = "\n".join(lines)
    assert ", 0 miss every filler" in text and "0.71 m past their edges" in text
    margin = float(text.split("the least ")[1].split(" m inside")[0])
    assert 0.65 - 1e-6 <= margin < 0.66, "the band is 0.65 m wider than the widest crumble on each side"
    assert "0 landable;" in text and "F-8 no wind obstacle: 0 wind volumes" in text


def test_the_crumbled_lip_and_the_niches_are_tested(e01):
    """F-2's samples reach the crumble's widest lip and the niches' backs (so the 0.65 m holds at the edge cases)."""
    params, built, data, run = e01
    assert all(0.0 <= c <= params["lip_crumble_m"] for values in run.crumble.values() for c in values)
    assert max(max(values) for values in run.crumble.values()) > 0.08, "the crumble widens the lip towards its top"
    walls = {i for i, (_, _, _, slot) in enumerate(built["lods"][0].faces) if slot == trench.WALL_SLOT}
    reach = max(run.distance(*built["lods"][0].vertices[k][0::2]) for i in walls
                for k in built["lods"][0].faces[i][0])
    assert reach == pytest.approx(run.niche_reach, abs=1e-6), "the farthest cut face is a niche's back"


def test_a_ray_off_the_fillers_is_found(e01):
    """lib.holes F-1 fails when a lip box is taken out."""
    _, built, data, _ = e01
    broken = copy.deepcopy(data)
    lip = next(k for k, s in enumerate(broken["collision"]) if s["material"] == trench.CUT and s["size"][1] > 1.7)
    del broken["collision"][lip]
    problems, _ = holes.audit(broken, built["lods"][0])
    assert any(p.startswith("F-1 sealed below") and "miss every filler" in p for p in problems), problems


def test_a_proud_or_thin_filler_is_refused(e01):
    _, built, data, _ = e01
    broken = copy.deepcopy(data)
    broken["collision"].append({"shape": "box", "center": [0.0, 0.1, 5.0], "size": [1.0, 0.25, 1.0],
                                "rotation": [0.0, 0.0, 0.0, 1.0], "material": trench.CUT, "landable": True})
    problems, _ = holes.audit(broken, built["lods"][0])
    assert [p.split(":")[0] for p in problems] == ["F-4 nothing proud", "F-5 thick", "F-7 surfaces"], problems


def test_neighbours_overlap_at_every_joint(e01):
    """F-5: at every interior joint, on the floor and on each wall, two cut boxes each straddle the joint by at least
    0.1 m: a point 0.15 m into the soil behind the face, moved 0.099 m along the run either way, lies in both."""
    _, built, data, run = e01
    boxes = [holes._Box(s) for s in data["collision"] if s["material"] == trench.CUT]

    def inside(box, p):
        d = (p[0] - box.c[0], p[1] - box.c[1], p[2] - box.c[2])
        return all(abs(sum(d[k] * axis[k] for k in range(3))) <= half + 1e-9 for axis, half in zip(box.axes, box.half))

    for j in range(1, run.n):
        t = run.tangent(j)
        floor = run.floor_centre(j)
        probes = [(floor[0], floor[1] - 0.15, floor[2])]
        for side in trench.SIDES:
            lip, foot = run.lip(j, side), run.foot(j, side)
            m, k = run.mitre(j)
            out = (side * m[0] * 0.15, 0.0, side * m[1] * 0.15)
            probes.append(tuple((lip[a] + foot[a]) / 2 + out[a] for a in range(3)))
        for p in probes:
            ahead = (p[0] + 0.099 * t[0], p[1], p[2] + 0.099 * t[1])
            behind = (p[0] - 0.099 * t[0], p[1], p[2] - 0.099 * t[1])
            both = [b for b in boxes if inside(b, ahead) and inside(b, behind)]
            assert len(both) >= 2, f"joint {j} at {run.chainage[j]:.2f} m, probe {p}: {len(both)} boxes straddle it"
    assert len(built["lines"]) == run.n - 1
    assert all(float(line.split("overlap ")[1].split(" m")[0]) >= 0.2 for line in built["lines"])


def test_cross_sections_span_the_cut(e01):
    """Every cross-section gap runs from the floor to the lips with the walls at the batter, and its narrowest_m is
    the audit's inscribed circle within 5 mm; every slot lies at y = 0."""
    _, built, data, run = e01
    sections = [g for g in data["gaps"] if g["id"].startswith("xs_")]
    slots = [g for g in data["gaps"] if g["id"].startswith("slot_")]
    assert len(sections) == run.n - 1 and len(slots) == run.n
    for g in sections:
        ys = sorted(p[1] for p in g["polygon"])
        assert ys[0] == pytest.approx(-1.5) and ys[-1] == pytest.approx(0.0)
        assert g["narrowest_m"] == pytest.approx(audit.inscribed_diameter(g["polygon"], g["normal"]), abs=0.005)
        assert 0.75 - 0.005 <= g["narrowest_m"] <= 0.9
    assert all(p[1] == 0.0 for g in slots for p in g["polygon"])
    assert meta.validate(data, meta.load_physics_materials(REPO_ROOT)) == []


def test_default_section_inscribes_0_75(e01_params):
    """The default cut (0.8 m lip, 0.6 m floor, 1.5 m deep) without crumble: every cross-section's inscribed circle
    is 0.75 m within 5 mm (the circle touches both walls where the width equals its diameter, and the lip line)."""
    p = copy.deepcopy(e01_params)
    p["lip_crumble_m"] = 0.0
    p["shape"] = "straight"
    sections = [g for g in trench._gaps(trench.Run(p)) if g["id"].startswith("xs_")]
    assert sections and all(abs(g["narrowest_m"] - 0.75) <= 0.005 for g in sections), \
        [round(g["narrowest_m"], 4) for g in sections]


def test_holes_records(e01):
    """The run's record: a point per metre at least, [x, 0, z], the cut's top width and depth, a 2.3 m band; then one
    record per niche from the run's centre line square to its back, 0.8 m wide, 1.35 m deep, a 2.1 m band."""
    _, built, data, run = e01
    records = data["holes"]
    assert [r["id"] for r in records] == ["run", "niche_0", "niche_1"]
    line = records[0]["centre_line"]
    assert all(p[1] == 0.0 for p in line) and line[0] == [0.0, 0.0, 0.0]
    spacing = [math.dist(a, b) for a, b in zip(line, line[1:])]
    assert max(spacing) <= 1.0 + 1e-5 and sum(spacing) == pytest.approx(24.0, abs=0.02)
    assert (records[0]["top_width_m"], records[0]["depth_m"], records[0]["band_width_m"]) == (0.8, 1.5, 2.3)
    for k, record in enumerate(records[1:]):
        i = run.niche_segments[k]
        (x0, _, z0), (x1, _, z1) = record["centre_line"]
        t = run.t[i]
        assert abs((x1 - x0) * t[0] + (z1 - z0) * t[1]) < 1e-5, "square to the run (the metadata keeps micrometres)"
        assert run.distance(x0, z0) < 1e-5 and math.hypot(x1 - x0, z1 - z0) == pytest.approx(run.niche_reach_of(i), abs=1e-5)
        assert (record["top_width_m"], record["depth_m"], record["band_width_m"]) == (0.8, 1.35, 2.1)


def test_surfaces_and_flags(e01):
    """Parapets and margins landable belt_straw, clods crater_spoil, every cut shape belt_bare and not landable; no
    wind volume, no snag; the run's centre line follows the cut (the floor's middle lies on it)."""
    _, built, data, run = e01
    by = {}
    for s in data["collision"]:
        by.setdefault(s["material"], []).append(s)
    assert set(by) == {trench.CUT, trench.GROUND, trench.SPOIL}
    assert all(s["landable"] for s in by[trench.GROUND]) and not any(s["landable"] for s in by[trench.CUT])
    assert all(s["shape"] == "sphere" for s in by[trench.SPOIL]) and len(by[trench.SPOIL]) == 24
    assert data["wind_volumes"] == [] and data["snag"] == []
    for j in range(run.n + 1):
        left, right = run.foot(j, 1), run.foot(j, -1)
        middle = ((left[0] + right[0]) / 2, (left[2] + right[2]) / 2)
        assert run.distance(*middle) < 0.05


def test_sides_follow_the_capture(e01):
    """Looking along the run from its first point, the field parapet (0.45 m) is on the right and the belt parapet
    (0.3 m) and the niches on the left (the review capture's convention)."""
    _, built, data, run = e01
    t = run.t[0]
    right = (t[1], -t[0])  # Unity is left-handed: looking along +X from above, the right is -Z
    field = run.parapet_line[-1][0]
    assert field[0] * right[0] + field[1] * right[1] > 0
    assert run.h[-1] == 0.45 and run.h[1] == 0.3
    for i in run.niche_segments:
        foot, ex, ey, ez, *_ = run.niche_frame(i)
        assert ez[0] * right[0] + ez[2] * right[1] < 0


def test_every_face_carries_its_own_planes_normal(e01):
    """lib.export and the audit take a face's plane from its given normal: on every LOD it is perpendicular to every
    edge of the face (a battered end wall given a level normal was measured 94 mm off)."""
    _, built, _, _ = e01
    for mesh in built["lods"][0::2]:
        for indices, normal, _, slot in mesh.faces:
            points = [mesh.vertices[i] for i in indices]
            for a, b in zip(points, points[1:] + points[:1]):
                edge = [b[k] - a[k] for k in range(3)]
                length = math.sqrt(sum(c * c for c in edge))
                if length > 1e-9:
                    assert abs(sum(e * n for e, n in zip(edge, normal))) / length < 1e-6, (slot, points, normal)


def test_budget_and_determinism(e01, e01_params):
    """LOD0 with its micro mesh under the design's 60 000 triangles, and a rebuild draws the same."""
    _, built, data, _ = e01
    assert data["lods"][0]["tris"] + data["micro"][0]["tris"] < 60000
    assert len(data["lods"]) == 3 and [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 8.0, 60.0]
    again = meta.assemble("E01", "E01_curving", "trench", e01_params, trench.build(e01_params), VISUALS)
    assert meta.dumps(again) == meta.dumps(data)
