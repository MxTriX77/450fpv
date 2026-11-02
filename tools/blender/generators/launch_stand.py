"""launch_stand: the legs-off launch stand, D02 (p5-drone design 4; DRN-07, D-022, D-043): "like a chair upside down,
so each chair leg is beneath each motor", an X of steel tube on the ground with a post under each motor, the
legs-off drone resting on its arms on the posts' tops and the coil hanging clear between them.

Every dimension comes from drone.json's `stand` and `motors`, as the physics' runtime stand reads them
(Fpv450.Sim.Contact.LaunchStand.Build, at foot (0, 0, 0) and yaw 0), so the drawn stand is the flown one:

- **Posts:** one per motor, on its diagonal at stand.post_radius_m from the centre (the motor's quadrant), a square
  tube of stand.post_diameter_m across, its sides along body x and z, from the ground to stand.post_height_m; its top
  closed (a welded end plate), where the arm rests.
- **Base:** two bars of the same tube on the ground, one along each diagonal, crossing at the centre, each
  2 (post_radius_m + post_diameter_m / 2) long, so its ends run into the posts' feet.
- **Material:** stand.material (steel) for all of it; the mass is stand.mass_kg, written to the metadata
  (mass_kg), since the stand is loose and physics may tip or drag it (D-022).

The params hold only the look: the cold-formed tube's corner radius, the worn paint (the sheet's satin black worn to
bare steel at the edges, in pieces along each tube chosen by the seed, and on the posts' tops where the arms seat),
and the LODs. Collision is the physics' six boxes exactly; the drawn tube's rounded corners lie inside them by
r (1 - 1/sqrt(2)) at most. LODs drop detail by the pixel at the feed's 935 px/rad: LOD1 (3 m) loses the 3 mm
corners' rounding and the worn pieces (a chamfer, one piece per tube), LOD2 (10 m) the chamfer (boxes); the stand is
culled with the drone that sits on it (330 m), so the drone never hangs over a vanished stand.
"""
import json
import math
import os
import random

from lib.geometry import Mesh
from lib.solids import Frame, box, normalize, palette_uv

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
LODS = 3
PALETTES = {"steel": ("paint", "worn")}  # assets-src/materials/stand_steel.json's columns, left to right
PAINT, WORN = (("steel", palette_uv(i, 2)) for i in range(2))
CORNER_SEGMENTS = (3, 1, 0)  # per LOD: the rounded corner's segments (1: a chamfer; 0: a plain box)
ROOT_HALF = math.sqrt(0.5)

PARAMS = {
    "spec": {"type": "str", "range": "a component spec under assets-src, relative to the repo root",
             "default": "assets-src/drone/drone.json", "effect": "every dimension: its stand and motors"},
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1, "effect": "which edge pieces are worn"},
    "corner_r_m": {"type": "float", "range": (0.0, 0.005), "default": 0.003,
                   "effect": "the square tube's outer corner radius (cold-formed: about twice its 1.5 mm wall)"},
    "wear": {"type": "float", "range": (0.0, 1.0), "default": 0.3,
             "effect": "the share of the tubes' edge pieces worn to bare steel (the sheet's dial)"},
    "wear_piece_m": {"type": "float", "range": (0.01, 0.2), "default": 0.03,
                     "effect": "the length of an edge piece that wears as one"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.5, 20.0), "default": 3.0, "effect": "LOD1 from here"},
    "lod.lod2_switch_m": {"type": "float", "range": (2.0, 60.0), "default": 10.0, "effect": "LOD2 from here"},
    "lod.cull_m": {"type": "float", "range": (50.0, 2000.0), "default": 330.0, "effect": "culled beyond"},
}


def value(x):
    return x["value"] if isinstance(x, dict) and "value" in x else x


def load_spec(params):
    with open(os.path.join(REPO_ROOT, params["spec"]), encoding="utf-8") as f:
        return json.load(f)


def stand_geometry(spec):
    """The runtime stand's boxes (LaunchStand.Build at foot 0, yaw 0): [(name, centre, half extents, x axis, z axis)],
    the four posts in the motors' order, then the base's two bars. Every axis is horizontal or up."""
    st = spec["stand"]
    a, h = value(st["post_diameter_m"]) / 2, value(st["post_height_m"])
    r = value(st["post_radius_m"]) * ROOT_HALF
    boxes = []
    for i, motor in enumerate(spec["motors"]):
        m = value(motor["position_m"])
        boxes.append((f"post_{i}", (r if m[0] > 0 else -r, h / 2, r if m[2] > 0 else -r), (a, h / 2, a),
                      (1.0, 0.0, 0.0), (0.0, 0.0, 1.0)))
    bar = (value(st["post_radius_m"]) + a, a, a)
    s = ROOT_HALF
    boxes.append(("bar_0", (0.0, a, 0.0), bar, (s, 0.0, s), (-s, 0.0, s)))
    boxes.append(("bar_1", (0.0, a, 0.0), bar, (s, 0.0, -s), (s, 0.0, s)))
    return boxes


def check(params):
    """The spec's stand present and sane: a post inside its arm's span under each motor, the tube no wider than the
    posts stand apart; the tube's corner inside its half-width; the LODs in order."""
    try:
        spec = load_spec(params)
    except (OSError, ValueError) as e:
        return [f"'spec' {params['spec']!r} does not load: {e}"]
    problems = []
    st = spec["stand"]
    r, d, h = (value(st[k]) for k in ("post_radius_m", "post_diameter_m", "post_height_m"))
    if not (r > d > 0 and h > d and value(st["mass_kg"]) > 0):
        problems.append(f"the stand needs post_radius_m > post_diameter_m > 0, post_height_m above the tube and a "
                        f"mass, got r {r}, d {d}, h {h}")
    arms = [p for p in spec["parts"] if p["name"].startswith("arm_")]
    for p in arms:
        c, length = value(p["position_m"]), value(p["length_m"])
        rc = math.hypot(c[0], c[2])
        if not rc - length / 2 <= r <= rc + length / 2:
            problems.append(f"{p['name']}: the post at r {r} m lies off its arm (r {rc - length / 2:.4f} to "
                            f"{rc + length / 2:.4f} m)")
    if params["corner_r_m"] >= d / 2:
        problems.append(f"'corner_r_m' = {params['corner_r_m']} is not inside the tube's half-width {d / 2}")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append("the LOD switches and the cull must increase")
    return problems


def build(params):
    spec = load_spec(params)
    boxes = stand_geometry(spec)
    lods = []
    for q in range(LODS):
        rng = random.Random(params["seed"])  # the same pieces worn at every LOD that draws them
        mesh = Mesh()
        for name, centre, half, ax, az in boxes:
            _tube(mesh, q, params, rng, name.startswith("post"), centre, half, ax, az)
        lods.append(mesh)
    st, lod = spec["stand"], params["lod"]
    material = st["material"]
    collision = [{"shape": "box", "center": list(centre), "size": [2 * h for h in half], "rotation": _yaw(ax),
                  "material": material, "landable": name.startswith("post")}
                 for name, centre, half, ax, _ in boxes]
    snag = [{"kind": "edge", "points": [[centre[i] - ax[i] * half[0] + (0.0, half[1], 0.0)[i] for i in range(3)],
                                        [centre[i] + ax[i] * half[0] + (0.0, half[1], 0.0)[i] for i in range(3)]],
             "radius_m": half[1], "material": material}
            for name, centre, half, ax, _ in boxes if name.startswith("bar")]  # a dragged line or skid trips here
    a, h = value(st["post_diameter_m"]) / 2, value(st["post_height_m"])
    report = [f"posts {2 * a * 1000:g} mm square, {h:g} m tall, on the motor diagonals at r "
              f"{value(st['post_radius_m']):g} m; base bars {2 * boxes[-1][2][0]:g} m on the diagonals; "
              f"{material}, {value(st['mass_kg']):g} kg (drone.json's stand, LaunchStand.Build's boxes)"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": "steel", "physics": material}],
        "collision": collision,
        "gaps": [],
        "wind_volumes": [],
        "snag": snag,
        "mass_kg": value(st["mass_kg"]),
        "report": report,
    }


def _yaw(direction):
    """The quaternion turning local +x onto a horizontal direction about +y (Unity's convention)."""
    angle = math.atan2(-direction[2], direction[0])
    return [0.0, math.sin(angle / 2), 0.0, math.cos(angle / 2)]


def _tube(mesh, q, params, rng, post, centre, half, ax, az):
    """One box of the stand drawn as the square tube it is: along its long axis (a post's y, a bar's x), its
    section the other two. A post's top is worn where the arm seats."""
    up = (0.0, 1.0, 0.0)
    if post:  # the frame's v is the tube's axis; (u, w) its section
        frame, hv, hu, hw = Frame(centre, ax, up, az), half[1], half[0], half[2]
    else:
        frame, hv, hu, hw = Frame(centre, az, ax, up), half[0], half[2], half[1]
    n, r = CORNER_SEGMENTS[q], params["corner_r_m"]
    top = WORN if post else PAINT
    if n == 0 or r <= 0:
        faces = {"+v": top} if post else {}
        box(mesh, frame, (-hu, -hv, -hw), (hu, hv, hw), PAINT, faces)
        return
    pieces = max(1, round(2 * hv / params["wear_piece_m"])) if q == 0 else 1
    worn = [[q == 0 and rng.random() < params["wear"] for _ in range(4)] for _ in range(pieces)]
    _rounded_tube(mesh, frame, hu, hw, -hv, hv, r, n, worn, top)


def _rounded_tube(mesh, frame, hu, hw, b0, b1, r, n, worn, top):
    """A rounded-square tube along the frame's v from b0 to b1, its section 2hu x 2hw in (u, w) with corners of
    radius r in n segments; closed by flat end plates (`top` the +v plate's paint). Each of its len(worn) equal
    pieces along v paints corner c's strip worn when worn[piece][c]. Its side's vertices carry their true normals,
    so the rounding shades smooth and the flats flat."""
    outline, normals, corner_of = [], [], []
    for c, (sx, sz, a0) in enumerate(((1, 1, 0.0), (-1, 1, 90.0), (-1, -1, 180.0), (1, -1, 270.0))):
        for i in range(n + 1):
            t = math.radians(a0 + 90.0 * i / n)
            outline.append((sx * (hu - r) + r * math.cos(t), sz * (hw - r) + r * math.sin(t)))
            normals.append((math.cos(t), math.sin(t)))
            corner_of.append(c if i < n else None)  # segment from this point: its corner's strip, else a flat
    k = len(outline)
    stations = [b0 + (b1 - b0) * j / len(worn) for j in range(len(worn) + 1)]
    rings = [[mesh.vertex(frame.at(p[0], b, p[1]), normal=frame.dir(nu, 0.0, nw))
              for p, (nu, nw) in zip(outline, normals)] for b in stations]
    for j in range(len(worn)):
        for i in range(k):
            i1 = (i + 1) % k
            c = corner_of[i]
            nu, nw = normalize((normals[i][0] + normals[i1][0], 0.0, normals[i][1] + normals[i1][1]))[::2]
            paint = WORN if c is not None and worn[j][c] else PAINT
            mesh.face([rings[j][i], rings[j][i1], rings[j + 1][i1], rings[j + 1][i]], frame.dir(nu, 0.0, nw),
                      paint[0], [paint[1]] * 4)
    for b, sign, paint in ((b0, -1.0, PAINT), (b1, 1.0, top)):
        ids = [mesh.vertex(frame.at(p[0], b, p[1])) for p in outline]
        mesh.face(ids, frame.dir(0.0, sign, 0.0), paint[0], [paint[1]] * k)
