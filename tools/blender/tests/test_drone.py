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
from lib import geometry, meta
from lib import params as params_lib

REPO = Path(__file__).resolve().parents[3]
VARIANTS = ("D01_legs_on", "D01_legs_off")
BOTTLE = "D01_bottle"  # the bottle alone, which the player scales to the setup's payload
MM = 0.001


def load(variant):
    return json.loads((REPO / "assets-src" / "drone" / f"{variant}.json").read_text(encoding="utf-8"))


def loaded(variant="D01_legs_on", kg=1.0):
    """A legs variant carrying a bottle of `kg` drawn in (the variants' own params carry none)."""
    return dict(load(variant), payload_kg=kg)


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

@pytest.mark.parametrize("variant", VARIANTS + (BOTTLE,))
def test_params_conform(variant):
    assert params_lib.validate(drone, load(variant)) == []


@pytest.mark.parametrize("change, words", [
    (lambda p: p.update(payload_kg=2.5), "outside the spec's setting"),
    (lambda p: p.update(layout="legs_up"), "legs_off or bottle"),
    (lambda p: p["arm"].update(width_m=0.032, mount_width_m=0.028), "narrower"),
    (lambda p: p["bottle"].update(cap_d_m=0.020), "neck, its cap and its body"),
    (lambda p: p["coil"].update(cap_d_m=0.085), "hole, the core, the rear cap"),
    (lambda p: p["lod"].update(lod3_switch_m=8.0), "must increase"),
])
def test_check_refuses_inconsistent_params(change, words):
    p = load("D01_legs_on")
    change(p)
    assert any(words in problem for problem in params_lib.validate(drone, p))


def test_check_holds_the_bottle_layout_to_the_specs_part(spec, tmp_path):
    p = dict(load(BOTTLE), payload_kg=0.5)  # the prefab is the spec's part; the player scales it
    assert any("must be its mass_kg" in problem for problem in params_lib.validate(drone, p))
    s = copy.deepcopy(spec)
    s["legs_off"]["moved"] = {"payload": {"value": [0.0, 0.07, 0.0], "label": "assumed"}}
    path = tmp_path / "moved.json"
    path.write_text(json.dumps(s), encoding="utf-8")
    p = dict(load(BOTTLE), spec=str(path))
    assert any("leaves the payload in place" in problem for problem in params_lib.validate(drone, p))


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
        if layout.has(f"leg_{corner}"):  # its tube drawn in two, the stub and the leg (p7g-legs 1.5)
            leg = part(spec, f"leg_{corner}")
            (x, y, z), r, L = leg["position_m"], leg["diameter_m"] / 2, leg["length_m"]
            lo, hi = bounds(geometry.merged([parts[f"stub_{corner}"], parts[f"leg_{corner}"]]))
            assert lo == pytest.approx((x - r, y - L / 2, z - r), abs=MM)
            assert hi == pytest.approx((x + r, y + L / 2, z + r), abs=MM)
    for side in ("left", "right"):
        pk = part(spec, f"pack_{side}")
        lo, hi = drone.box_extent(pk)
        assert bounds(parts[f"pack_{side}"]) == (pytest.approx(lo, abs=1e-9), pytest.approx(hi, abs=1e-9))
        if layout.has(f"skid_{side}"):  # drawn in its corners' halves
            sk = part(spec, f"skid_{side}")
            (x, y, z), r, L = sk["position_m"], sk["diameter_m"] / 2, sk["length_m"]
            lo, hi = bounds(geometry.merged([parts[f"skid_{c}"] for c in drone.skid_corners(layout, side)]))
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


@pytest.mark.parametrize("variant", VARIANTS)
def test_the_coil_pack_is_its_own_readable_mesh_holding_what_the_payout_moves(variant, built):
    """p7d-tether 1.14: the player's CoilPack moves every vertex on the pack's outer surface (within the ribs' depth
    under the full pack's radius, between its rear face and the hoop: CoilShape.D01's band, its 0.1 mm tolerance) as
    the line pays out, and only the metadata's readable parts are imported readable. So each LOD's pack is its own
    mesh, <variant>_LOD<n>_coil_pack, holding every such vertex, all but its two rims, and the LOD itself none."""
    params = load(variant)
    layout = drone.Layout(drone.load_spec(params), params)
    f, _ = drone.mount_frame(params, layout, "coil")
    c = layout.part("coil")["position_m"]
    rp, (_, z1, z2, _) = drone.coil_radii(params, layout)
    tol, rib = 1e-4, params["coil"]["rib_depth_m"]

    def on_pack(v):
        d = [v[a] - c[a] for a in range(3)]
        z = sum(d[a] * f.w[a] for a in range(3))
        r = math.hypot(sum(d[a] * f.u[a] for a in range(3)), sum(d[a] * f.v[a] for a in range(3)))
        return z1 - tol <= z <= z2 + tol and rp - rib - tol <= r <= rp + tol

    b = built[variant]
    packs = [r for r in b["readable"] if r["kind"] == "coil_pack"]  # beside the legs' parts on legs-on
    assert [r["lod"] for r in packs] == list(range(4))
    for q, (lod, readable) in enumerate(zip(b["lods"], packs)):
        pack = readable["mesh"]
        others = [r["mesh"] for r in b["readable"] if r["lod"] == q and r["kind"] != "coil_pack"]
        assert not any(on_pack(v) for m in [lod] + others for v in m.vertices), \
            f"LOD{q}: a vertex the payout moves outside the pack"
        assert sum(on_pack(v) for v in pack.vertices) == len(pack.vertices) - 2 * drone.COIL_SEGMENTS[q], f"LOD{q}"
        assert set(pack.slots()) == {"plastic"}
    data = meta.assemble("D01", variant, "drone", params, b, {s: f"drone_{s}" for s in drone.PALETTES})
    assert [r["name"] for r in data["readable"] if r["kind"] == "coil_pack"] == \
        [f"{variant}_LOD{q}_coil_pack" for q in range(4)]


def test_each_legs_parts_are_their_own_readable_meshes_cut_where_the_sim_cuts(spec, built):
    """p7g-legs 1.5: the player draws each leg from PoseFrame (turned by its set about its clamp's edge, a stub when
    broken, gone when detached, a hanging skid half folded, the pieces it dropped), so on legs-on each corner's parts
    are their own readable meshes per LOD, <variant>_LOD<n>_<kind>_<corner> in the sim's corner order, and the body
    draws none of them: the stub in the clamp above the clamp part's underside (DroneSpec.BuildLegDamage's Clamp),
    the leg below it, the T-joint (none at LOD3, as before) and the skid's half from midway between its bores (the
    skid capsules' inner end in drone.json) out, the band with the port rear half; all in asset space."""
    params = load("D01_legs_on")
    layout = drone.Layout(drone.load_spec(params), params)
    b = built["D01_legs_on"]
    assert spec["legs"]["parts"] == [f"leg_{c}" for c in drone.CORNERS]  # the sim's corner order, rear right first
    kinds = [(q, f"{k}_{c}") for q in range(4) for c in drone.CORNERS for k in ("stub", "leg", "tjoint", "skid")
             if q < 3 or k != "tjoint"]
    assert [(r["lod"], r["kind"]) for r in b["readable"] if r["kind"] != "coil_pack"] == kinds
    data = meta.assemble("D01", "D01_legs_on", "drone", params, b, {s: f"drone_{s}" for s in drone.PALETTES})
    assert [r["name"] for r in data["readable"] if r["kind"] != "coil_pack"] == \
        [f"D01_legs_on_LOD{q}_{k}" for q, k in kinds]
    assert not [r for r in built["D01_legs_off"]["readable"] if r["kind"] != "coil_pack"]
    capsules = {s["name"]: s for s in spec["contacts"]["shapes"] if s.get("role") == "skid"}
    for q in range(4):
        mesh = {r["kind"]: r["mesh"] for r in b["readable"] if r["lod"] == q}
        body = b["lods"][q].vertices
        for c in drone.CORNERS:
            leg, clamp = part(spec, f"leg_{c}"), part(spec, f"clamp_{c}")
            (x, y, z), r, L = leg["position_m"], leg["diameter_m"] / 2, leg["length_m"]
            edge = clamp["position_m"][1] - clamp["size_m"][1] / 2
            assert drone.clamp_edge(layout, c) == pytest.approx((x, edge, z), abs=1e-12)
            for kind, (bottom, top) in (("stub", (edge, y + L / 2)), ("leg", (y - L / 2, edge))):
                lo, hi = bounds(mesh[f"{kind}_{c}"])  # the tube's polygon across, exactly its span along
                assert (lo[1], hi[1]) == pytest.approx((bottom, top), abs=1e-12), f"LOD{q} {kind}_{c}"
                assert (lo[0], lo[2], hi[0], hi[2]) == pytest.approx((x - r, z - r, x + r, z + r),
                                                                     abs=r * (1 - math.cos(math.pi / 4)) + 1e-9)
            assert not any(abs(v[1] - edge) < 1e-9 and math.hypot(v[0] - x, v[2] - z) < r - 1e-6
                           for v in mesh[f"stub_{c}"].vertices), "the stub is open at the edge"
            assert not any(math.hypot(v[0] - x, v[2] - z) < r - 1e-6 and v[1] < edge - 1e-9 for v in body), \
                f"LOD{q}: the body draws {c}'s leg"
            assert (f"tjoint_{c}" in mesh) == (q < 3)
        for side in ("right", "left"):
            sk = part(spec, f"skid_{side}")
            (x, y, z), r, L = sk["position_m"], sk["diameter_m"] / 2, sk["length_m"]
            cut = drone.skid_cut(layout, side)
            corners = drone.skid_corners(layout, side)
            assert corners == [c for c in drone.CORNERS if (part(spec, f"leg_{c}")["position_m"][0] >= 0) == (side == "right")]
            assert all(capsules[f"skid_{c}"]["b_m"]["value"][2] == pytest.approx(cut) for c in corners)
            for c in corners:
                rear = part(spec, f"leg_{c}")["position_m"][2] < cut
                lo, hi = bounds(mesh[f"skid_{c}"])
                assert (lo[2], hi[2]) == pytest.approx((z - L / 2, cut) if rear else (cut, z + L / 2), abs=1e-9)
                band = any(uvs[0] == drone.LIME[1] for _, _, uvs, _ in mesh[f"skid_{c}"].faces)
                assert band == (side == "left" and rear and q < 3), f"LOD{q} skid_{c}: the band with its half"
            assert not any(math.hypot(v[0] - x, v[1] - y) < r - 1e-6 and abs(v[2] - z) < L / 2 for v in body), \
                f"LOD{q}: the body draws the {side} skid"


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
@pytest.mark.parametrize("kg", [0.1, 0.5, 1.0, 1.5, 2.0])
def test_bottle_holds_its_water(variant, kg):
    """D-050: a plain PET bottle sized by its water. The mass build's part is the water as a solid cylinder (about
    1000 kg/m3), scaled by the cube root of the mass; the drawn bottle is that part's diameter, centred on it, and
    holds its volume below the cap, so its water follows the setup's mass."""
    params = loaded(variant, kg)
    layout, parts = layout_parts(params)
    pay = layout.scaled_payload()
    f, _ = drone.mount_frame(params, layout, "payload")
    c = pay["position_m"]
    local = [tuple(sum((v[a] - c[a]) * axis[a] for a in range(3)) for axis in (f.u, f.v, f.w))
             for v in parts["bottle"].vertices]
    s, length = layout.payload_scale, drone.bottle_length(params, layout) * layout.payload_scale
    assert min(p[2] for p in local) == pytest.approx(-length / 2, abs=1e-9)
    assert max(p[2] for p in local) == pytest.approx(length / 2, abs=1e-9)
    assert max(math.hypot(p[0], p[1]) for p in local) == pytest.approx(pay["diameter_m"] / 2, abs=1e-9)
    water = math.pi * (pay["diameter_m"] / 2) ** 2 * pay["length_m"]
    file_part = layout.part("payload")
    profile = drone._profile_points(params, file_part["diameter_m"] / 2, length / s, 0, ring=False)
    assert s ** 3 * drone.water_volume(profile[:-4]) == pytest.approx(water, rel=1e-9)  # base to the cap's foot
    assert water == pytest.approx(kg / 1000.0, rel=0.01)  # about 1 l a kilogram (the spec's part)
    assert volume(parts["bottle"]) == pytest.approx(water, rel=0.02)  # its 32-gon, with the cap and ring
    capsule = next(sh for sh in drone.load_spec(params)["contacts"]["shapes"] if sh["name"] == "payload")
    reach = math.dist(capsule["a_m"]["value"], capsule["b_m"]["value"]) + 2 * capsule["radius_m"]["value"]
    assert length == pytest.approx(reach * s, abs=0.005 * s)  # the flight's capsule end to end, 0.26 m at 1 kg


def test_d050_layout_hangs_the_coil_and_straps_the_bottle_on_top(spec, tmp_path):
    """D-050: the coil beneath, mounted on the bottom plate's underside; the bottle on the upper plate, its straps
    passing under that plate; the upper plate's top left clear for it."""
    for variant in VARIANTS:
        params = dict(loaded(variant), spec=d050_spec(spec, tmp_path))
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


@pytest.mark.parametrize("kg", [0.1, 0.5, 2.0])
def test_payload_mass_scales_the_bottle_as_the_mass_build_and_zero_leaves_it_off(kg):
    """DroneSpec.ScaleAwayFromArmPlane: the part's size times s = (m / m0)^(1/3), its face toward the arm plane kept,
    so the centre moves away from the arms when heavier and towards them when lighter; the drawn bottle, its straps
    and its shapes are the spec-mass bottle scaled so about that face, which is what the player does to D01_bottle."""
    params = loaded()
    base = drone.Layout(drone.load_spec(params), params)
    layout = drone.Layout(drone.load_spec(params), loaded(kg=kg))
    pay0 = base.part("payload")
    s = (kg / pay0["mass_kg"]) ** (1 / 3)
    assert layout.payload_scale == pytest.approx(s)
    pay = layout.scaled_payload()
    assert pay["diameter_m"] == pytest.approx(pay0["diameter_m"] * s) and pay["length_m"] == pytest.approx(
        pay0["length_m"] * s)
    dy = pay["position_m"][1] - pay0["position_m"][1]
    assert dy == pytest.approx(math.copysign(1, pay0["position_m"][1]) * (s - 1) * pay0["diameter_m"] / 2)
    assert pay["position_m"][1] - pay["diameter_m"] / 2 == pytest.approx(pay0["position_m"][1] - pay0["diameter_m"] / 2)
    pivot = drone.bottle_pivot(params, base)
    prefab = drone.build(load(BOTTLE))
    parts = drone.part_meshes(loaded(kg=kg), layout, 0)
    drawn = parts["bottle"].vertices + parts["bottle_straps"].vertices
    assert len(drawn) == len(prefab["lods"][0].vertices)
    for v, w in zip(drawn, prefab["lods"][0].vertices):  # the prefab at localPosition pivot, localScale s
        assert v == pytest.approx(tuple(pivot[a] + s * w[a] for a in range(3)), abs=1e-12)
    shapes = drone.build(loaded(kg=kg))["collision"][-3:]
    for got, ref in zip(shapes, prefab["collision"]):
        assert got["center"] == pytest.approx([pivot[a] + s * ref["center"][a] for a in range(3)], abs=1e-12)
        assert got["radius"] == pytest.approx(ref["radius"] * s) and got["height"] == pytest.approx(ref["height"] * s)
    empty = loaded(kg=0.0)
    layout, parts = layout_parts(empty)
    assert not layout.has("payload") and "bottle" not in parts and "bottle_straps" not in parts
    assert len(drone.build(empty)["collision"]) == len(drone.build(params)["collision"]) - 3
    assert empty == dict(load("D01_legs_on"))  # the legs variants carry no bottle: it is D01_bottle's


def test_bottle_variant_is_the_bottle_alone_from_where_it_sits():
    """D01_bottle: the bottle and its straps at the spec's 1 kg, their origin where the bottle meets the upper plate
    (the face the mass build keeps), so one prefab serves every payload of the setup."""
    params = load(BOTTLE)
    built = drone.build(params)
    layout = drone.Layout(drone.load_spec(params), params)
    pivot = drone.bottle_pivot(params, layout)
    _, _, upper = drone.plate_heights(params, layout)
    assert pivot == pytest.approx((0.0, upper[1], 0.0), abs=1e-12)  # on the upper plate's top, over the centre
    pay = layout.part("payload")
    assert pivot[1] == pytest.approx(pay["position_m"][1] - pay["diameter_m"] / 2, abs=1e-12)
    lod0 = built["lods"][0]
    assert set(lod0.slots()) == {"bottle", "strap"}
    bottle = drone.part_meshes(params, layout, 0)["bottle"]
    assert bottle.bounds()[0][1] == pytest.approx(pivot[1], abs=1e-12)  # drawn in place, moved by build only
    assert lod0.bounds()[0][1] == pytest.approx(-params["plates"]["upper_m"] - drone.STRAP_T_M, abs=1e-9)
    assert [round(c["center"][1] - (pay["position_m"][1] - pivot[1]), 9) for c in built["collision"]] == [0, 0, 0]
    tris = [m.triangles() for m in built["lods"]]
    assert tris == sorted(tris, reverse=True) and len(set(tris)) == 4 and tris[0] < 2000
    assert built["snag"] == [] and len(built["collision"]) == 3
    assert built["cull_distance_m"] == 120.0  # its 0.256 m under two pixels at the feed's 935 px/rad
    data = meta.assemble("D01", BOTTLE, "drone", params, built, {s: f"drone_{s}" for s in drone.PALETTES})
    assert meta.validate(data, meta.load_physics_materials(REPO)) == []


# --- solids, LODs, determinism ------------------------------------------------------------------------------------

def test_parts_are_closed_solids_with_outward_normals(spec):
    params = loaded()
    layout, parts = layout_parts(params)
    pk = part(spec, "pack_right")
    outline = drone._rounded_rect(0.0, 0.0, pk["size_m"][0], pk["size_m"][1], params["pack"]["corner_r_m"], 4)
    area = 0.5 * abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(outline, outline[1:] + outline[:1])))
    assert volume(parts["pack_right"]) == pytest.approx(area * pk["size_m"][2])
    leg = part(spec, "leg_rr")
    n, r, foot = 12, leg["diameter_m"] / 2, leg["position_m"][1] - leg["length_m"] / 2

    def prism(radius, length):
        return n / 2 * radius ** 2 * math.sin(2 * math.pi / n) * length

    # the free length below the clamp's edge, its end there the tube's 10 mm bore; the stub above is open at the edge
    assert volume(parts["leg_rr"]) == pytest.approx(prism(r, drone.clamp_edge(layout, "rr")[1] - foot)
                                                    - prism(r - 0.001, 0.010))
    for name in ("motor_rr", "prop_fl", "bottle", "skid_rl", "skid_fl", "plates", "clamp_rr", "tjoint_fr"):
        assert volume(parts[name]) > 0, name
    assert volume(geometry.merged([parts["coil"], parts["coil_pack"]])) > 0  # the spool, its pack its own mesh


@pytest.mark.parametrize("variant", VARIANTS)
def test_four_lods_lighter_each_and_the_same_size(variant, built):
    b = built[variant]
    tris = [m.triangles() + sum(r["mesh"].triangles() for r in b["readable"] if r["lod"] == q)
            for q, m in enumerate(b["lods"])]  # what each LOD draws, its coil pack with it
    assert len(tris) == 4 and tris == sorted(tris, reverse=True) and len(set(tris)) == 4
    assert tris[0] < 25000 and tris[3] < 600  # AST-08: a hero asset; the far LOD a few hundred
    lo0, hi0 = b["lods"][0].bounds()
    for m in b["lods"][1:]:
        lo, hi = m.bounds()
        assert lo == pytest.approx(lo0, abs=0.015) and hi == pytest.approx(hi0, abs=0.015)
    assert b["switch_distances_m"] == [0.0, 3.0, 10.0, 35.0] and b["cull_distance_m"] == 330.0
    for m in b["lods"] + [r["mesh"] for r in b["readable"]]:
        assert m.smooth == set(m.slots())


def test_build_is_deterministic():
    a, b = drone.build(load("D01_legs_on")), drone.build(load("D01_legs_on"))
    assert [m.vertices for m in a["lods"]] == [m.vertices for m in b["lods"]]
    assert [m.faces for m in a["lods"]] == [m.faces for m in b["lods"]]
    assert [(r["lod"], r["mesh"].vertices, r["mesh"].faces) for r in a["readable"]] ==         [(r["lod"], r["mesh"].vertices, r["mesh"].faces) for r in b["readable"]]
    assert a["collision"] == b["collision"] and a["snag"] == b["snag"]


def test_no_face_is_degenerate(built):
    for b in built.values():
        for mesh in b["lods"] + [r["mesh"] for r in b["readable"]]:
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
    assert catalog["generator"] == "drone" and catalog["variants"] == list(VARIANTS) + [BOTTLE]
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
    physics = {m["slot"]: m["physics"] for m in data["materials"]}
    assert physics["metal"] == "steel" and physics["carbon"] == "carbon_composite"
    assert "carbon_composite" in {c["material"] for c in data["collision"]}
    assert all(s["material"] == "carbon_composite" for s in data["snag"] if s["kind"] == "edge")  # legs and skids
    assert not any(c["landable"] for c in data["collision"])
