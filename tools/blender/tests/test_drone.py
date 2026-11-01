"""generators.drone (p5-drone task 1.3): the prop-tips scenario (DRN-02), every drawn part against the component spec
it reads (DRN-03: drone.json), D-050's layout (the coil beneath on its plate, the bottle on top strapped through
its plate) whichever way the spec places them, both variants, the payload and payout settings, closed solids with
outward normals, LODs, determinism, the palettes against their recipes, and the metadata against schema 1."""
import copy
import json
import math
from pathlib import Path

import pytest

from generators import drone
from lib import meta
from lib import params as params_lib

REPO = Path(__file__).resolve().parents[3]
VARIANTS = ("D01_legs_on", "D01_legs_off")
MM = 0.001


def load(variant):
    return json.loads((REPO / "assets-src" / "drone" / f"{variant}.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def spec():
    return json.loads((REPO / "assets-src" / "drone" / "drone.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def built():
    return {v: drone.build(load(v)) for v in VARIANTS}


def layout_parts(params, q=0):
    layout = drone.Layout(drone.load_spec(params), params)
    return layout, drone.part_meshes(params, layout, q)


def bounds(mesh):
    return mesh.bounds()


def volume(mesh):
    """The enclosed volume over the faces, each taken with its stated outward normal (test_solids.volume)."""
    total = 0.0
    for indices, normal, _, _ in mesh.faces:
        pts = [mesh.vertices[i] for i in indices]
        n = [0.0, 0.0, 0.0]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n[0] += (a[1] - b[1]) * (a[2] + b[2])
            n[1] += (a[2] - b[2]) * (a[0] + b[0])
            n[2] += (a[0] - b[0]) * (a[1] + b[1])
        along = sum(n[a] * normal[a] for a in range(3))
        centroid = [sum(p[a] for p in pts) / len(pts) for a in range(3)]
        total += 0.5 * math.copysign(1.0, along) * sum(centroid[a] * n[a] for a in range(3))
    return total / 3


def d050_spec(spec, tmp_path, legs_off_moves=None):
    """The spec with D-050's layout as the physics engineer will set it (assumed figures for the test): the coil
    beneath the bottom plate, a 1.5 l bottle fore-aft on the upper plate."""
    s = copy.deepcopy(spec)
    for p in s["parts"]:
        if p["name"] == "coil":
            p["position_m"]["value"] = [0.0, -0.053, 0.0]
        if p["name"] == "payload":
            p["position_m"]["value"] = [0.0, 0.0745, -0.01]
            p["axis"]["value"] = [0, 0, 1]
            p["diameter_m"]["value"] = 0.088
            p["length_m"]["value"] = 0.30
    s["legs_off"]["moved"] = legs_off_moves or {}
    path = tmp_path / "drone_d050.json"
    path.write_text(json.dumps(s), encoding="utf-8")
    return str(path)


# --- params -----------------------------------------------------------------------------------------------------

@pytest.mark.parametrize("variant", VARIANTS)
def test_params_conform(variant):
    assert params_lib.validate(drone, load(variant)) == []


@pytest.mark.parametrize("change, words", [
    (lambda p: p.update(payload_kg=2.5), "outside the spec's setting"),
    (lambda p: p.update(layout="legs_up"), "legs_on or legs_off"),
    (lambda p: p["arm"].update(width_m=0.032, mount_width_m=0.028), "narrower"),
    (lambda p: p["bottle"].update(cap_d_m=0.020), "neck, its cap and its body"),
    (lambda p: p["coil"].update(cap_d_m=0.085), "hole, the core, the rear cap"),
    (lambda p: p["lod"].update(lod3_switch_m=8.0), "must increase"),
])
def test_check_refuses_inconsistent_params(change, words):
    p = load("D01_legs_on")
    change(p)
    assert any(words in problem for problem in params_lib.validate(drone, p))


# --- the prop-tips scenario (DRN-02) ------------------------------------------------------------------------------

@pytest.mark.parametrize("variant", VARIANTS)
def test_prop_tips(variant, spec):
    """WHEN the built LOD0 is measured THEN tip to tip is 0.574 +-0.005 m nose-first and 0.706 +-0.005 m on the
    diagonal, each prop's tip at the spec's radius."""
    layout, parts = layout_parts(load(variant))
    spans = drone.prop_spans(parts, layout)
    assert spans["nose_first"] == pytest.approx(0.574, abs=0.005)
    assert spans["diagonal"] == pytest.approx(0.706, abs=0.005)
    R = spec["props"]["diameter_m"]["value"] / 2
    assert spans["radii"] == pytest.approx([R] * 4, abs=1e-9)  # the apex, and nothing beyond it
    print(f"{variant}: nose-first {spans['nose_first']:.4f} m, diagonal {spans['diagonal']:.4f} m")


@pytest.mark.parametrize("spin", [1.0, -1.0])
def test_blades_lead_high_in_their_spin(spin):
    """Each section's leading edge, ahead in the spin's direction, stands above its trailing edge (the pitch)."""
    p = load("D01_legs_on")
    m = drone.Mesh()
    hub, R = (0.0, 0.035, 0.0), 0.127
    drone._blade(m, 0, hub, R, 0.0, spin, p["prop"], 0.1143, 0.0095)
    motion = (0.0, 0.0, -spin)  # at phi = 0 a cw (+y) spin moves the blade towards -z
    rows = {}
    for v in m.vertices:
        if 0.3 * R < v[0] < 0.8 * R:
            rows.setdefault(round(v[0], 4), []).append(v)
    checked = 0
    for row in rows.values():
        if len(row) >= 4:
            le = max(row, key=lambda v: v[2] * motion[2])
            te = min(row, key=lambda v: v[2] * motion[2])
            assert le[1] > te[1]
            checked += 1
    assert checked >= 3


# --- the parts against the spec (DRN-03) ---------------------------------------------------------------------------

def part(spec, name):
    p = next(p for p in spec["parts"] if p["name"] == name)
    return {k: drone.value(v) for k, v in p.items()}


@pytest.mark.parametrize("variant", VARIANTS)
def test_motors_legs_skids_and_packs_are_their_parts(variant, spec):
    layout, parts = layout_parts(load(variant))
    for corner in drone.CORNERS:
        mo = part(spec, f"motor_{corner}")
        (x, y, z), r, L = mo["position_m"], mo["diameter_m"] / 2, mo["length_m"]
        lo, hi = bounds(parts[f"motor_{corner}"])
        assert lo == pytest.approx((x - r, y - L / 2, z - r), abs=MM) and hi == pytest.approx((x + r, y + L / 2, z + r), abs=MM)
        if layout.has(f"leg_{corner}"):
            leg = part(spec, f"leg_{corner}")
            (x, y, z), r, L = leg["position_m"], leg["diameter_m"] / 2, leg["length_m"]
            lo, hi = bounds(parts[f"leg_{corner}"])
            assert lo == pytest.approx((x - r, y - L / 2, z - r), abs=MM)
            assert hi == pytest.approx((x + r, y + L / 2, z + r), abs=MM)
    for side in ("left", "right"):
        pk = part(spec, f"pack_{side}")
        lo, hi = drone.box_extent(pk)
        assert bounds(parts[f"pack_{side}"]) == (pytest.approx(lo, abs=1e-9), pytest.approx(hi, abs=1e-9))
        if layout.has(f"skid_{side}"):
            sk = part(spec, f"skid_{side}")
            (x, y, z), r, L = sk["position_m"], sk["diameter_m"] / 2, sk["length_m"]
            lo, hi = bounds(parts[f"skid_{side}"])
            assert lo == pytest.approx((x - r, y - r, z - L / 2), abs=MM) and hi == pytest.approx((x + r, y + r, z + L / 2), abs=MM)


def test_tjoints_and_clamps_follow_their_parts(spec):
    params = load("D01_legs_on")
    layout, parts = layout_parts(params)
    for corner in drone.CORNERS:
        tj = part(spec, f"tjoint_{corner}")
        lo, hi = drone.box_extent(tj)
        mlo, mhi = bounds(parts[f"tjoint_{corner}"])
        skid = part(spec, "skid_right" if tj["position_m"][0] > 0 else "skid_left")
        ground = skid["position_m"][1] - skid["diameter_m"] / 2
        assert (mlo[0], mlo[2], mhi[0], mhi[2]) == pytest.approx((lo[0], lo[2], hi[0], hi[2]), abs=MM)
        assert mlo[1] == pytest.approx(ground, abs=1e-9)  # flat on the ground, flush with the skid's underside
        clamp = part(spec, f"clamp_{corner}")
        clo, chi = bounds(parts[f"clamp_{corner}"])
        centre = [(clo[a] + chi[a]) / 2 for a in (0, 2)]
        assert centre == pytest.approx([clamp["position_m"][0], clamp["position_m"][2]], abs=MM)
        bottom, top = drone.clamp_span(params, layout, corner)
        assert chi[1] >= top - 1e-9 and top == pytest.approx(drone.plate_heights(params, layout)[0][1])
        assert bottom >= drone.box_extent(clamp)[0][1] - 1e-9


@pytest.mark.parametrize("variant", VARIANTS)
def test_arms_and_plates(variant, spec):
    params = load(variant)
    layout, parts = layout_parts(params)
    t = params["arm"]["thickness_m"]
    for i, corner in enumerate(drone.CORNERS):
        arm = part(spec, f"arm_{corner}")
        d, r_in, y = drone._arm_axis(arm)
        hub = spec["motors"][i]["position_m"]["value"]
        r_m = math.hypot(hub[0], hub[2])
        along = [v[0] * d[0] + v[2] * d[2] for v in parts[f"arm_{corner}"].vertices]
        assert min(along) == pytest.approx(math.hypot(arm["position_m"][0], arm["position_m"][2]) - arm["length_m"] / 2,
                                           abs=1e-9)
        assert max(along) == pytest.approx(r_m + params["arm"]["mount_width_m"] / 2, abs=MM)
        lo, hi = bounds(parts[f"arm_{corner}"])
        assert (lo[1], hi[1]) == pytest.approx((y - t / 2, y + t / 2), abs=1e-9)
    pl = part(spec, "plates")
    lo, hi = bounds(parts["plates"])
    (cx, _, cz), (sx, sy, sz) = pl["position_m"], pl["size_m"]
    assert (lo[0], lo[2], hi[0], hi[2]) == pytest.approx((cx - sx / 2, cz - sz / 2, cx + sx / 2, cz + sz / 2), abs=1e-9)
    assert hi[1] - lo[1] == pytest.approx(sy, abs=1e-9)  # the frame's 34 mm
    assert lo[1] == pytest.approx(-t / 2 - params["plates"]["bottom_m"])  # the bottom plate under the arms


@pytest.mark.parametrize("variant", VARIANTS)
def test_coil_is_its_part_end_to_end(variant, spec):
    params = load(variant)
    layout, parts = layout_parts(params)
    coil = layout.part("coil")
    f, d = drone.mount_frame(params, layout, "coil")
    c = coil["position_m"]
    local = [((v[0] - c[0]) * f.u[0] + (v[1] - c[1]) * f.u[1] + (v[2] - c[2]) * f.u[2],
              (v[0] - c[0]) * f.v[0] + (v[1] - c[1]) * f.v[1] + (v[2] - c[2]) * f.v[2],
              (v[0] - c[0]) * f.w[0] + (v[1] - c[1]) * f.w[1] + (v[2] - c[2]) * f.w[2]) for v in parts["coil"].vertices]
    assert min(p[2] for p in local) == pytest.approx(-coil["length_m"] / 2, abs=1e-9)
    assert max(p[2] for p in local) == pytest.approx(coil["length_m"] / 2, abs=1e-9)
    assert max(math.hypot(p[0], p[1]) for p in local) == pytest.approx(params["coil"]["hoop_d_m"] / 2, abs=1e-9)
    rp, _ = drone.coil_radii(params, layout)
    assert rp == pytest.approx(coil["diameter_m"] / 2)  # the full pack is the part's diameter
    mlo, mhi = bounds(parts["coil_mount"])  # the tray and bracket reach the plate's face
    plate_face = c[1] + d if f.v[1] < 0 else c[1] - d
    assert (mhi[1] if f.v[1] < 0 else mlo[1]) == pytest.approx(plate_face, abs=1e-9)


def test_payout_shrinks_the_pack_to_the_core():
    params = load("D01_legs_on")
    layout = drone.Layout(drone.load_spec(params), params)
    for payout, expected in ((0.0, layout.part("coil")["diameter_m"] / 2), (1.0, params["coil"]["core_d_m"] / 2)):
        p = dict(params, payout=payout)
        assert drone.coil_radii(p, layout)[0] == pytest.approx(expected)
    half = drone.coil_radii(dict(params, payout=0.5), layout)[0]
    R, rc = layout.part("coil")["diameter_m"] / 2, params["coil"]["core_d_m"] / 2
    assert half * half - rc * rc == pytest.approx((R * R - rc * rc) / 2)  # the wound volume halves


@pytest.mark.parametrize("variant", VARIANTS)
def test_bottle_fills_the_payload_part(variant):
    params = load(variant)
    layout, parts = layout_parts(params)
    pay = layout.part("payload")
    f, _ = drone.mount_frame(params, layout, "payload")
    c = pay["position_m"]
    local = [tuple(sum((v[a] - c[a]) * axis[a] for a in range(3)) for axis in (f.u, f.v, f.w))
             for v in parts["bottle"].vertices]
    assert min(p[2] for p in local) == pytest.approx(-pay["length_m"] / 2, abs=1e-9)
    assert max(p[2] for p in local) == pytest.approx(pay["length_m"] / 2, abs=1e-9)
    assert max(math.hypot(p[0], p[1]) for p in local) == pytest.approx(pay["diameter_m"] / 2, abs=1e-9)
    assert volume(parts["bottle"]) > 0.8 * math.pi * (pay["diameter_m"] / 2) ** 2 * pay["length_m"] * 0.6


def test_d050_layout_hangs_the_coil_and_straps_the_bottle_on_top(spec, tmp_path):
    """D-050: the coil beneath, mounted on the bottom plate's underside; the bottle on the upper plate, its straps
    passing under that plate; the upper plate's top left clear for it."""
    for variant in VARIANTS:
        params = dict(load(variant), spec=d050_spec(spec, tmp_path))
        layout, parts = layout_parts(params)
        bottom, top, upper = drone.plate_heights(params, layout)
        f, d = drone.mount_frame(params, layout, "coil")
        assert f.v == (0.0, -1.0, 0.0)
        assert bounds(parts["coil_mount"])[1][1] == pytest.approx(bottom[0], abs=1e-9)
        assert bounds(parts["coil"])[1][1] < bottom[0]
        fb, db = drone.mount_frame(params, layout, "payload")
        assert fb.v == (0.0, 1.0, 0.0) and bounds(parts["bottle"])[0][1] > upper[1]
        assert bounds(parts["bottle_straps"])[0][1] == pytest.approx(upper[0] - drone.STRAP_T_M, abs=1e-6)
        clutter = [v for v in parts["electronics"].vertices if v[1] > upper[1] + 1e-9]
        assert clutter == []  # nothing of the electronics on the upper plate's top
        plate = layout.part("plates")
        for station in drone.bottle_strap_stations(params, layout):
            z = fb.at(0, 0, station)[2]
            assert abs(z - plate["position_m"][2]) < plate["size_m"][2] / 2  # the straps pass through the plate
        built = drone.build(params)
        assert meta.validate(meta.assemble("D01", variant, "drone", params, built,
                                           {s: f"drone_{s}" for s in drone.PALETTES}),
                             meta.load_physics_materials(REPO)) == []


def test_legs_off_drops_the_legs_and_moves_what_the_spec_moves(spec):
    params = load("D01_legs_off")
    layout, parts = layout_parts(params)
    for name in spec["legs_off"]["removed"]:
        assert not layout.has(name) and name not in parts
    for name, centre in spec["legs_off"]["moved"].items():
        expected = centre["value"]
        if name == "payload":
            continue  # moved, then scaled from its moved centre (test_payload_mass_scales_the_bottle)
        assert layout.part(name)["position_m"] == pytest.approx(expected)
    for corner in drone.CORNERS:
        assert f"clamp_{corner}" in parts  # the clamps stay on the arms


def test_payload_mass_scales_the_bottle_and_zero_leaves_it_off():
    params = load("D01_legs_on")
    base = drone.Layout(drone.load_spec(params), params)
    heavy = drone.Layout(drone.load_spec(params), dict(params, payload_kg=2.0))
    s = (2.0 / base.part("payload")["mass_kg"]) ** (1 / 3)
    assert heavy.payload_scale == pytest.approx(s)
    dy = heavy.part("payload")["position_m"][1] - base.part("payload")["position_m"][1]
    assert abs(dy) == pytest.approx((s - 1) * base.part("payload")["diameter_m"] / 2)
    assert math.copysign(1, dy) == math.copysign(1, base.part("payload")["position_m"][1])  # away from the arms
    empty = dict(params, payload_kg=0.0)
    layout, parts = layout_parts(empty)
    assert not layout.has("payload") and "bottle" not in parts and "bottle_straps" not in parts
    assert len(drone.build(empty)["collision"]) == len(drone.build(params)["collision"]) - 3


# --- solids, LODs, determinism ------------------------------------------------------------------------------------

def test_parts_are_closed_solids_with_outward_normals(spec):
    params = load("D01_legs_on")
    layout, parts = layout_parts(params)
    pk = part(spec, "pack_right")
    outline = drone._rounded_rect(0.0, 0.0, pk["size_m"][0], pk["size_m"][1], params["pack"]["corner_r_m"], 4)
    area = 0.5 * abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(outline, outline[1:] + outline[:1])))
    assert volume(parts["pack_right"]) == pytest.approx(area * pk["size_m"][2])
    leg = part(spec, "leg_rr")
    n = 12
    assert volume(parts["leg_rr"]) == pytest.approx(n / 2 * (leg["diameter_m"] / 2) ** 2 * math.sin(2 * math.pi / n)
                                                    * leg["length_m"])
    for name in ("motor_rr", "prop_fl", "coil", "bottle", "skid_left", "plates", "clamp_rr", "tjoint_fr"):
        assert volume(parts[name]) > 0, name


@pytest.mark.parametrize("variant", VARIANTS)
def test_four_lods_lighter_each_and_the_same_size(variant, built):
    b = built[variant]
    tris = [m.triangles() for m in b["lods"]]
    assert len(tris) == 4 and tris == sorted(tris, reverse=True) and len(set(tris)) == 4
    assert tris[0] < 25000 and tris[3] < 600  # AST-08: a hero asset; the far LOD a few hundred
    lo0, hi0 = b["lods"][0].bounds()
    for m in b["lods"][1:]:
        lo, hi = m.bounds()
        assert lo == pytest.approx(lo0, abs=0.015) and hi == pytest.approx(hi0, abs=0.015)
    assert b["switch_distances_m"] == [0.0, 3.0, 10.0, 35.0] and b["cull_distance_m"] == 330.0
    for m in b["lods"]:
        assert m.smooth == set(m.slots())


def test_build_is_deterministic():
    a, b = drone.build(load("D01_legs_on")), drone.build(load("D01_legs_on"))
    assert [m.vertices for m in a["lods"]] == [m.vertices for m in b["lods"]]
    assert [m.faces for m in a["lods"]] == [m.faces for m in b["lods"]]
    assert a["collision"] == b["collision"] and a["snag"] == b["snag"]


def test_no_face_is_degenerate(built):
    for b in built.values():
        for mesh in b["lods"]:
            for indices, _, _, _ in mesh.faces:
                assert len(set(indices)) == len(indices) >= 3
                pts = [mesh.vertices[i] for i in indices]
                n = [0.0, 0.0, 0.0]
                for p, q in zip(pts, pts[1:] + pts[:1]):
                    n[0] += (p[1] - q[1]) * (p[2] + q[2])
                    n[1] += (p[2] - q[2]) * (p[0] + q[0])
                    n[2] += (p[0] - q[0]) * (p[1] + q[1])
                assert math.sqrt(sum(c * c for c in n)) > 1e-12


# --- materials and metadata -----------------------------------------------------------------------------------------

def test_palettes_match_their_recipes_and_the_catalog():
    catalog = json.loads((REPO / "assets-src" / "catalog.json").read_text(encoding="utf-8"))["assets"]["D01"]
    assert catalog["generator"] == "drone" and catalog["variants"] == list(VARIANTS)
    assert set(catalog["materials"]) == set(drone.PALETTES)
    for slot, columns in drone.PALETTES.items():
        recipe = json.loads((REPO / "assets-src" / "materials" / f"{catalog['materials'][slot]}.json")
                            .read_text(encoding="utf-8"))
        assert recipe["kind"] == "palette" and tuple(recipe["colours"]) == columns


@pytest.mark.parametrize("variant", VARIANTS)
def test_metadata_validates(variant, built):
    params = load(variant)
    data = meta.assemble("D01", variant, "drone", params, built[variant], {s: f"drone_{s}" for s in drone.PALETTES})
    assert meta.validate(data, meta.load_physics_materials(REPO)) == []
    kinds = [s["kind"] for s in data["snag"]]
    if variant == "D01_legs_on":
        assert kinds.count("hook") == 4 + 2 and kinds.count("edge") == 2 + 4  # skid ends and ties; skids and legs
    else:
        assert kinds == ["hook", "hook"]  # the cable ties' tails
    assert {m["slot"]: m["physics"] for m in data["materials"]}["metal"] == "steel"
    assert not any(c["landable"] for c in data["collision"])
