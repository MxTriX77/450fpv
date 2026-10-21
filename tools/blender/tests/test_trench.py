"""generators/trench and lib/holes (p1-e01-trench, tasks 1.1-1.7): the committed E01 params, check's refusals, the
hole contract on the built E01 (sealed from below on a 5 cm grid over the bands and 0.71 m past them, the cavity's
faces, the crumbled lip and the niches 0.65 m inside the bands, level lips, nothing proud, thickness and the 0.2 m
overlaps at every joint, held by lib.holes), the cross-section gaps' inscribed circle, the slots and the niches'
mouths, the width jitter, the holes records, the shapes' surfaces, the berm's and the top outside the cut, the lodged
mat and the budget."""
import copy
import json
import math
import random
from pathlib import Path

import pytest

from generators import trench
from lib import audit, holes, meta, params as params_lib

VISUALS = {"ground": "grassed_spoil", "wall": "trench_wall", "clod": "trench_wall", "section": "trench_wall",
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
    (lambda p: p.update(leg_jitter_m=1.5), "do not fit legs of 5.0 +- 1.5 m"),
    (lambda p: p.update(bend_radius_m=3.5), "past a bend's radius 'bend_radius_m' = 3.5 m less 0.5 m"),
    (lambda p: p.update(margin_m=8.0), "the patch spans up to 40.50 m"),
    (lambda p: p.update(width_jitter_m=0.16), "'width_jitter_m' = 0.16 is outside [0.0, 0.15]"),
    (lambda p: p.update(top_w_m=1.0, width_jitter_m=0.15), "than the trench_wall strip's end slot (1.2 m)"),
    (lambda p: p.update(bottom_w_m=0.8, width_jitter_m=0.05), "than the trench_wall strip's floor row (0.8 m)"),
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
    problems, lines = holes.audit(data, built["lods"][0], built["joints"])
    assert problems == [], problems
    text = "\n".join(lines)
    assert ", 0 miss every filler" in text and "0.71 m past their edges" in text
    margin = float(text.split("the least ")[1].split(" m inside")[0])
    assert 0.65 - 1e-6 <= margin < 0.66, "the band is 0.65 m wider than the widest crumble on each side"
    assert "0 landable;" in text and "F-8 no wind obstacle: 0 wind volumes" in text
    assert f"the fillers' top within {trench.RING_DROP_M * 1000:.1f} mm" in text, "past the ends F-3 reads the ring"


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
    problems, _ = holes.audit(broken, built["lods"][0], built["joints"])
    assert any(p.startswith("F-1 sealed below") and "miss every filler" in p for p in problems), problems


def test_a_proud_or_thin_filler_is_refused(e01):
    _, built, data, _ = e01
    broken = copy.deepcopy(data)
    broken["collision"].append({"shape": "box", "center": [0.0, 0.1, 5.0], "size": [1.0, 0.25, 1.0],
                                "rotation": [0.0, 0.0, 0.0, 1.0], "material": trench.CUT, "landable": True})
    problems, _ = holes.audit(broken, built["lods"][0], built["joints"])
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
    assert [j["joint"] for j in built["joints"]] == list(range(1, run.n))
    assert all(j["overlap_m"] >= holes.JOINT_OVERLAP_M - 1e-9 for j in built["joints"])


def test_a_short_joint_fails_the_build(e01, monkeypatch):
    """S3: lib.holes holds the joints to its own 0.2 m, whatever the generator's constant: with OVERLAP_M at 0.05
    the boxes overlap 0.1 m and the hole contract fails on F-5's overlap (QA's mutation M3)."""
    params, built, data, run = e01
    problems, lines = holes.audit(data, built["lods"][0], built["joints"])
    assert problems == [] and any(line.startswith(f"holes F-5 overlap: {run.n - 1} joints") for line in lines), lines
    monkeypatch.setattr(trench, "OVERLAP_M", 0.05)
    _, joints = trench._collision(run, trench._clods(params, run))
    problems, _ = holes.audit(data, built["lods"][0], joints)
    assert [p.split(":")[0] for p in problems] == ["F-5 overlap"] and "overlapping 0.100 m" in problems[0], problems


def test_joints_are_checked_whether_or_not_reported(e01):
    """S13: the overlap check runs for every asset with holes: a generator that reports no joints, or misses one of
    its holes' centre lines' interior points, fails the hole contract on F-5's overlap."""
    _, built, data, run = e01
    problems, _ = holes.audit(data, built["lods"][0])
    assert [p.split(":")[0] for p in problems] == ["F-5 overlap"], problems
    for joints in (None, [], built["joints"][1:]):
        problems = holes._joints(data["holes"], joints, [])
        assert problems == [f"F-5 overlap: the generator reports {len(joints or [])} joints, the holes' centre lines "
                            f"have {run.n - 1}"], problems


def test_cross_sections_span_the_cut(e01):
    """Every cross-section gap runs from the floor to the lips with the walls at the batter, and its narrowest_m is
    the audit's inscribed circle within 5 mm."""
    _, built, data, run = e01
    sections = [g for g in data["gaps"] if g["id"].startswith("xs_")]
    assert len(sections) == run.n - 1
    for g in sections:
        ys = sorted(p[1] for p in g["polygon"])
        assert ys[0] == pytest.approx(-1.5) and ys[-1] == pytest.approx(0.0)
        assert g["narrowest_m"] == pytest.approx(audit.inscribed_diameter(g["polygon"], g["normal"]), abs=0.005)
        assert 0.75 - 0.005 <= g["narrowest_m"] <= 0.75 + 0.05 + 0.2
    assert meta.validate(data, meta.load_physics_materials(REPO_ROOT)) == []


def test_slots_are_the_openings_throat(e01):
    """R2: the open top is one slot per stretch of about a metre, the stretches tiling the run end to end, each at
    least as long along the centre line as the cut is wide there, so no slot's narrowest_m (the audit's inscribed
    circle) falls under its own lip-to-lip width less 5 mm, nor under the drone's 0.574 m; every slot lies at y = 0."""
    _, built, data, run = e01
    slots = [g for g in data["gaps"] if g["id"].startswith("slot_")]
    stretches = trench._slot_stretches(run)
    assert [g["id"] for g in slots] == [f"slot_{a:.2f}" for a, _ in stretches]
    assert stretches[0][0] == 0 and stretches[-1][1] == pytest.approx(run.length)
    assert all(b == c for (_, b), (c, _) in zip(stretches, stretches[1:]))
    assert len(slots) == 21 and all(1.0 < b - a < 1.2 for a, b in stretches), stretches  # 24 m / 1.143 m
    for g, (a, b) in zip(slots, stretches):
        assert all(p[1] == 0.0 for p in g["polygon"])
        ends = [(trench._lip_at(run, s, -1), trench._lip_at(run, s, 1)) for s in (a, b)]
        widths = [math.dist(p, q) for p, q in ends] + [math.dist(run.lip(j, -1), run.lip(j, 1))
                                                      for j in range(run.n + 1) if a < run.chainage[j] < b]
        assert b - a >= max(widths), g["id"]
        assert g["narrowest_m"] == pytest.approx(audit.inscribed_diameter(g["polygon"], g["normal"]), abs=0.005)
        assert g["narrowest_m"] >= min(widths) - 0.005 and g["narrowest_m"] > 0.574, (g["id"], g["narrowest_m"], widths)
    area = sum(trench._area2([(p[0], p[2]) for p in g["polygon"]]) for g in slots)
    assert area == pytest.approx(sum(trench._area2(q) for q, _ in trench._slot_quads(run)), rel=1e-6), \
        "the slots tile the opening the top is cut by"


def test_niche_mouths_are_gaps(e01):
    """S5: each niche's mouth is a gap polygon in the belt wall's plane, the niche's width by its height on the
    battered wall, its narrowest_m the inscribed circle (the height, 0.6 m / sin 86 degrees)."""
    _, built, data, run = e01
    mouths = [g for g in data["gaps"] if g["id"].startswith("mouth_")]
    assert len(mouths) == len(run.niche_segments) == 2
    for g, i in zip(mouths, run.niche_segments):
        foot, ex, ey, ez, sill, ceiling, _ = run.niche_frame(i)
        assert g["id"] == f"mouth_{run.chainage[i] + run.niche_axis(i):.2f}"
        for p in g["polygon"]:
            assert abs(sum((p[k] - foot[k]) * ez[k] for k in range(3))) < 1e-5, "on the wall's plane"
        assert g["narrowest_m"] == pytest.approx(min(run.niche["w"], ceiling - sill), abs=0.002)
        assert g["narrowest_m"] == pytest.approx(audit.inscribed_diameter(g["polygon"], g["normal"]), abs=0.005)


def test_width_jitter_only_widens(e01):
    """S8: each wall stands out by 0 to width_jitter_m / 2, drawn per metre, floor and lip together (the batter
    kept), held over a niche's segment on the belt side; so the floor and the lip are never narrower than
    bottom_w_m and top_w_m (the sheet's corridor verdicts hold everywhere)."""
    params, built, data, run = e01
    half = params["width_jitter_m"] / 2
    for side in trench.SIDES:
        assert all(0.0 <= v <= half + 1e-12 for v in run.jitter[side])
        assert max(run.jitter[side]) - min(run.jitter[side]) > half / 2, "it varies along the run"
        for j in range(run.n + 1):
            assert run.lip_offset(j, side) - run.crumble[side][j] - run.jitter[side][j] == pytest.approx(run.top)
    for i in run.niche_segments:
        assert run.jitter[1][i] == run.jitter[1][i + 1]
    for j in range(run.n + 1):
        assert math.dist(run.foot(j, -1), run.foot(j, 1)) >= params["bottom_w_m"] - 1e-9
        assert math.dist(run.lip(j, -1), run.lip(j, 1)) >= params["top_w_m"] - 1e-9


def test_default_section_inscribes_0_75(e01_params):
    """The default cut (0.8 m lip, 0.6 m floor, 1.5 m deep) without crumble or width jitter: every cross-section's
    inscribed circle is 0.75 m within 5 mm (the circle touches both walls where the width equals its diameter, and
    the lip line)."""
    p = copy.deepcopy(e01_params)
    p["lip_crumble_m"] = 0.0
    p["width_jitter_m"] = 0.0
    p["shape"] = "straight"
    sections = [g for g in trench._gaps(trench.Run(p)) if g["id"].startswith("xs_")]
    assert sections and all(abs(g["narrowest_m"] - 0.75) <= 0.005 for g in sections), \
        [round(g["narrowest_m"], 4) for g in sections]


def test_holes_records(e01):
    """The run's record: a point per metre at least, [x, 0, z], the cut's top width and depth, a 2.35 m band (the lip,
    its widest jitter and crumble, 0.65 m a side); then one record per niche from the run's centre line square to its
    back, 0.8 m wide, 1.35 m deep, a 2.1 m band."""
    _, built, data, run = e01
    records = data["holes"]
    assert [r["id"] for r in records] == ["run", "niche_0", "niche_1"]
    line = records[0]["centre_line"]
    assert all(p[1] == 0.0 for p in line) and line[0] == [0.0, 0.0, 0.0]
    spacing = [math.dist(a, b) for a, b in zip(line, line[1:])]
    assert max(spacing) <= 1.0 + 1e-5 and sum(spacing) == pytest.approx(24.0, abs=0.02)
    assert (records[0]["top_width_m"], records[0]["depth_m"], records[0]["band_width_m"]) == (0.8, 1.5, 2.35)
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


def test_the_berm_is_grassed_ground(e01):
    """S4: on every segment and side, a vertical line through the berm (between the lip and the parapet's inner
    toe) meets first a landable belt_straw box with its top at y = 0, at least 0.3 m thick, and under it the cut's
    fillers; the parapets' outer reach keeps a filler's top at y = 0 for F-3."""
    _, built, data, run = e01
    boxes = [(holes._Box(s), s) for s in data["collision"] if s["shape"] == "box"]
    for i in range(run.n):
        for side in trench.SIDES:
            for f in (0.25, 0.5, 0.75):
                d = (run.lip_offset(i, side) + run.toe) / 2
                o = (run.points[i][0] + (run.points[i + 1][0] - run.points[i][0]) * f,
                     run.points[i][1] + (run.points[i + 1][1] - run.points[i][1]) * f)
                n = run.normal[i]
                x, z = o[0] + side * n[0] * d, o[1] + side * n[1] * d
                spans = [(b.span(x, z), s) for b, s in boxes]
                spans = sorted(((sp[1], sp[0], s) for sp, s in spans if sp is not None), key=lambda t: -t[0])
                top, bottom, shape = spans[0]
                assert top == pytest.approx(0.0, abs=1e-5) and shape["material"] == trench.GROUND and shape["landable"]
                assert top - bottom >= 0.3 - 1e-6
                assert any(s["material"] == trench.CUT for _, _, s in spans[1:]), (i, side, f)


def test_the_top_outside_the_cut_is_grassed_ground(e01):
    """Task 1.7 (R3, QA's top-surface map): on a 5 cm grid over the whole patch, wherever a vertical line misses the
    cut's opening, the highest collision top it meets (the one a contact meets first) is a landable belt_straw
    shape's, never belt_bare, and no shape of another surface tops within 1 mm of it: at the lips, along the berm, at
    the parapets' toes and past the run's ends, where the berm turns round them as level grassed ground over the
    lowered end block and F-3's ring."""
    _, built, data, run = e01
    index = {}
    for s in data["collision"]:
        box = holes._Box(s) if s["shape"] == "box" else None
        c, r = s["center"], s.get("radius", 0.0)
        lo, hi = (box.lo, box.hi) if box else ((c[0] - r, c[2] - r), (c[0] + r, c[2] + r))
        for i in range(math.floor(lo[0] / 0.5), math.floor(hi[0] / 0.5) + 1):
            for j in range(math.floor(lo[1] / 0.5), math.floor(hi[1] / 0.5) + 1):
                index.setdefault((i, j), []).append((box, s))

    def top(box, s, x, z):
        if box:
            span = box.span(x, z)
            return None if span is None else span[1]
        d2 = (x - s["center"][0]) ** 2 + (z - s["center"][2]) ** 2
        if d2 > s["radius"] ** 2:
            return None
        if s["shape"] == "cylinder":
            assert s["rotation"] == [0.0, 0.0, 0.0, 1.0]
            return s["center"][1] + s["height"] / 2
        return s["center"][1] + math.sqrt(s["radius"] ** 2 - d2)

    opening = trench._Cut(trench._slot_quads(run), 0.0)
    x0, z0, nx, nz, cx, cz = trench._patch(run)
    ends = [(run.points[0], run.t[0], -1.0), (run.points[-1], run.t[-1], 1.0)]
    step, outside, past = 0.05, 0, [0, 0]
    for a in range(round(nx * cx / step)):
        x = x0 + (a + 0.5) * step
        for b in range(round(nz * cz / step)):
            z = z0 + (b + 0.5) * step
            if opening.near(x, z):
                continue
            tops = sorted(((t, s) for box, s in index.get((math.floor(x / 0.5), math.floor(z / 0.5)), ())
                           for t in (top(box, s, x, z),) if t is not None), key=lambda ts: -ts[0])
            highest, shape = tops[0]
            assert shape["material"] == trench.GROUND and shape["landable"], (x, z, highest, shape)
            tied = [(t, s["material"]) for t, s in tops[1:] if highest - t <= 0.001 and s["material"] != trench.GROUND]
            assert not tied, (x, z, highest, tied)
            outside += 1
            for k, (p, t, sign) in enumerate(ends):
                along = sign * ((x - p[0]) * t[0] + (z - p[1]) * t[1])
                past[k] += 0.0 < along < run.band + trench.BACKING_M and run.distance(x, z) < run.backing[1]
    assert outside * step * step > 400 and min(past) * step * step > 8.0, (outside, past)


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


def test_the_ground_map_is_laid_once_over_the_patch(e01):
    """Task 1.4: on every LOD the top's UVs are metres from the patch's centre and stay inside the grassed_spoil map,
    size_m across and centred on the UVs' origin (the importer's half-tile offset): the map is laid once over the whole
    patch, never wrapped, so nothing of it repeats or seams across the parapets."""
    recipe = json.loads((REPO_ROOT / "assets-src" / "materials" / "grassed_spoil.json").read_text(encoding="utf-8"))
    assert recipe["size_m"] == trench.GROUND_MAP_M and "bare" not in recipe["patches"] and "green" not in recipe["patches"]
    _, built, _, run = e01
    x0, z0, nx, nz, cx, cz = trench._patch(run)
    centre = (x0 + nx * cx / 2, z0 + nz * cz / 2)
    for mesh in built["lods"][0::2]:
        ground = [(mesh.vertices[i], uv) for indices, _, uvs, slot in mesh.faces if slot == trench.GROUND_SLOT
                  for i, uv in zip(indices, uvs)]
        assert all(uv == pytest.approx((p[0] - centre[0], p[2] - centre[1]), abs=1e-9) for p, uv in ground)
        assert max(abs(c) for _, uv in ground for c in uv) <= trench.GROUND_MAP_M / 2


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


def test_the_mat_is_visual_only_and_clear_of_the_corridor(e01):
    """Task 1.6 (R1): the lodged mat (the berm's, the parapets' and the lips' straws) and the floor's straws are
    LOD0's micro meshes, all of it lib.straw's kit in the straw slot, with no shape of its own (the audit skips every
    micro mesh listed in the metadata, lib.audit). It covers the berm and the parapets from the lip to the outer toe.
    Away from the run's ends, nothing of it stands inside the half-span of a drone flown nose-first down the middle
    (0.287 m) below the lip; a lip straw hangs at most LIP_HANG_M down, in front of its wall. LOD0 lasts until every
    straw is at least S04's 8 m from the camera (S1: the review's views, 10-12 m from the reference point, see it)."""
    params, built, data, run = e01
    micro = {(m["lod"], m["kind"]): m for m in built["micro"]}
    assert sorted(micro) == [(0, "mat"), (0, "straw")]
    mesh = micro[(0, "mat")]["mesh"]
    centre = [(data["bounds"]["min"][k] + data["bounds"]["max"][k]) / 2 for k in range(3)]  # the LOD reference point
    farthest = max(math.dist(p, centre) for m in built["micro"] for p in m["mesh"].vertices)
    assert farthest + 8.0 <= params["lod"]["lod1_switch_m"] < farthest + 9.0, farthest
    assert {slot for *_, slot in mesh.faces} == {trench.straw.SLOT}
    assert len(data["collision"]) == len(trench._collision(run, trench._clods(params, run))[0])
    ends = [run.points[0], run.points[-1]]
    x0, z0, nx, nz, cx, cz = trench._patch(run)
    near = trench._Near(run.points, (x0, z0, x0 + nx * cx, z0 + nz * cz))
    opening = trench._Cut(trench._slot_quads(run), 0.0)
    hanging, cover = 0, {}
    for x, y, z in mesh.vertices:
        assert y >= -trench.LIP_HANG_M[1] - 0.01, (x, y, z)
        d = near.distance(x, z)
        if y > 0:
            cover[min(int((d - run.top) / 0.25), 13)] = cover.get(min(int((d - run.top) / 0.25), 13), 0) + 1
        if min(math.hypot(x - e[0], z - e[1]) for e in ends) <= 0.8:
            continue
        assert y > -0.01 or d > 0.287, (x, y, z)
        if y < -0.005 and opening.near(x, z):  # a lip straw hanging in the cut, in front of the widest wall there
            hanging += 1
            lip = run.top + run.jitter_m + run.crumble_m
            assert d < lip - (lip - run.bottom - run.jitter_m) * (-y) / run.depth, (x, y, z, d)
    assert hanging > 1000, "the lip's straws hang down the walls"
    reach = int((run.toe + run.base - run.top) / 0.25)
    assert all(cover.get(k, 0) > 1000 for k in range(reach)), cover  # the berm and the parapets, every 25 cm band


def test_near_gives_the_runs_own_distance(e01):
    """_Near (the height's and the mat's faster lookup) gives exactly Run.distance's number, inside its bounds and
    out of them, so the ground's top is the same surface either way."""
    _, _, _, run = e01
    x0, z0, nx, nz, cx, cz = trench._patch(run)
    near = trench._Near(run.parapet_line[-1], (x0, z0, x0 + nx * cx, z0 + nz * cz))
    rng = random.Random(3)
    for _ in range(3000):
        x, z = rng.uniform(x0 - 2, x0 + nx * cx + 2), rng.uniform(z0 - 2, z0 + nz * cz + 2)
        assert near.distance(x, z) == run.distance(x, z, run.parapet_line[-1])


def test_budget_and_determinism(e01, e01_params):
    """Per LOD: the ground (the grid, the lip's refinement, the clods) under design 8's 60 000 triangles and LOD0's
    bake (the mat and the floor's straws) under its 450 000, LOD1 LOD0's ground without the bake, LOD2 the 1 m patch;
    and a rebuild draws the same."""
    _, built, data, _ = e01
    bake = [sum(m["tris"] for m in data["micro"] if m["lod"] == n) for n in range(3)]
    assert all(lod["tris"] < 60000 for lod in data["lods"]) and 300000 < bake[0] < 450000 and bake[1:] == [0, 0]
    assert data["lods"][1]["tris"] == data["lods"][0]["tris"] > data["lods"][2]["tris"]
    assert [(m["lod"], m["kind"]) for m in data["micro"]] == [(0, "straw"), (0, "mat")]
    assert len(data["lods"]) == 3 and [lod["switch_distance_m"] for lod in data["lods"]] == [0.0, 24.0, 60.0]
    again = meta.assemble("E01", "E01_curving", "trench", e01_params, trench.build(e01_params), VISUALS)
    assert meta.dumps(again) == meta.dumps(data)
