"""debris_car: one of a car's own small parts lying on the ground (object sheet docs/object-sheets/vehicles.md §K07;
p1-r01-vaz-sedan, design decision 9). The first four pieces R01 needs: a headlamp's bowl, a chrome bezel ring and a
flat trim plate (the A scatter, measured M) and a burnt tyre's bead wire (the F wheels). Plain Python on
lib.geometry and lib.loft.

Params (assets-src/debris/K07_<piece>.json) are PARAMS below; a key the piece does not use must be 0 (check), so a
params file never carries a number that does nothing. Frame (asset space, lib.frame): the piece rests on y = 0,
centred on the origin; it is PLANTED, so the audit lets the ground cover a primitive below y = 0 (the bowl's
sphere).

- lamp_bowl: a hemispherical steel reflector of `diameter_m`, its wall `section_m` thick, resting dome up on its rim.
  Collision: the sphere of its outer radius about the rim's centre (its lower half in the ground); an `edge` snag
  round its rim at half the wall's thickness.
- bezel_ring: a chrome ring of outer diameter `diameter_m` and round section `section_m`, lying flat. Collision:
  RING_CAPSULES capsules along its centre circle's polygon (the sheet's ring of capsule segments); an `edge` snag
  along it.
- trim_plate: a `length_m` x `width_m` steel plate `section_m` thick, bowed `bend_m` along its length, its two short
  ends torn (a seeded jitter of TORN_M), the long sides cut straight. Collision: one thin box holding the bow;
  an `edge` snag round its outline at half its thickness.
- bead_wire: a tyre's bead bundle left by a fire, a steel ring of outer diameter `diameter_m` and section
  `section_m`, out of round by up to `bend_m` (seeded). Collision: RING_CAPSULES capsules; a `hook` snag along it
  (free to lift and catch, the sheet).

Slots: chrome (the bowl, the ring, the bare plate: K07's #9e9d9b and #c9c8c5) and rust (the burnt wire), physics
`physics_material` (steel for all four, design 9). UVs are metres: the bowl and rings round and along, the plate on
its top. LOD1 drops the thickness and halves the segments; the cull distances follow the pixel rule (935 px/rad,
the piece's longest size under two pixels).
"""
import math
import random

from lib import loft, meta
from lib.geometry import Mesh

PLANTED = True
CHROME, RUST = "chrome", "rust"
PIECES = ("lamp_bowl", "bezel_ring", "trim_plate", "bead_wire")
SLOT = {"lamp_bowl": CHROME, "bezel_ring": CHROME, "trim_plate": CHROME, "bead_wire": RUST}
USES = {"lamp_bowl": ("diameter_m", "section_m"), "bezel_ring": ("diameter_m", "section_m"),
        "trim_plate": ("length_m", "width_m", "section_m", "bend_m"),
        "bead_wire": ("diameter_m", "section_m", "bend_m")}
SIZES = ("diameter_m", "section_m", "length_m", "width_m", "bend_m")
RING_CAPSULES = 8         # the sheet: a ring as 6-8 capsule segments
TORN_M = 0.008            # a torn end's jitter along the plate (assumed: 5-10 mm teeth)
SEGMENTS = ((32, 8), (16, 4))   # per LOD: around a ring or bowl, across its section or up the dome

PARAMS = {
    "piece": {"type": "str", "range": ", ".join(PIECES), "default": "lamp_bowl", "effect": "which part"},
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "the torn ends' teeth and the wire's out-of-round"},
    "diameter_m": {"type": "float", "range": (0.0, 0.6), "default": 0.17,
                   "effect": "outer diameter of the bowl, the bezel ring or the bead wire; 0 for the plate"},
    "section_m": {"type": "float", "range": (0.0, 0.03), "default": 0.0008,
                  "effect": "the bowl's and plate's sheet thickness, the ring's and wire's round section"},
    "length_m": {"type": "float", "range": (0.0, 0.6), "default": 0.0, "effect": "the plate's length; 0 otherwise"},
    "width_m": {"type": "float", "range": (0.0, 0.6), "default": 0.0, "effect": "the plate's width; 0 otherwise"},
    "bend_m": {"type": "float", "range": (0.0, 0.05), "default": 0.0,
               "effect": "the plate's bow at mid-length, the wire's largest radial kink; 0 otherwise"},
    "physics_material": {"type": "str", "range": "an id of assets-src/physics_materials.json", "default": "steel",
                         "effect": "collision material and the slot's physics (design 9: steel)"},
    "lod.lod1_switch_m": {"type": "float", "range": (1.0, 50.0), "default": 8.0, "effect": "LOD1 from here"},
    "lod.cull_m": {"type": "float", "range": (10.0, 400.0), "default": 80.0, "effect": "culled beyond"},
}


def check(params):
    """The piece is one of PIECES, its sizes are set and every size it does not use is 0; a bowl or ring's
    section under its radius; the LODs ordered."""
    piece = params["piece"]
    if piece not in PIECES:
        return [f"'piece' = {piece!r} must be one of {', '.join(PIECES)}"]
    problems = []
    for key in SIZES:
        used = key in USES[piece]
        if used and params[key] <= 0 and key != "bend_m":
            problems.append(f"'{key}' must be positive for a {piece}")
        if not used and params[key] != 0:
            problems.append(f"'{key}' = {params[key]} is not used by a {piece}; set it to 0")
    if piece in ("lamp_bowl", "bezel_ring", "bead_wire") and params["section_m"] * 2 >= params["diameter_m"] / 2:
        problems.append(f"'section_m' = {params['section_m']} is too thick for 'diameter_m' = {params['diameter_m']}")
    if params["lod"]["lod1_switch_m"] >= params["lod"]["cull_m"]:
        problems.append("'lod.lod1_switch_m' must lie under 'lod.cull_m'")
    return problems


def build(params):
    piece = params["piece"]
    rng = random.Random(f"debris_car {piece} {params['seed']}")
    material = params["physics_material"]
    lods, collision, snag = {
        "lamp_bowl": _bowl, "bezel_ring": _ring, "trim_plate": _plate, "bead_wire": _ring,
    }[piece](params, rng, material)
    lod = params["lod"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"]],
        "thin_wire": [False, False],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": SLOT[piece], "physics": material}],
        "collision": collision,
        "gaps": [], "wind_volumes": [], "holes": [],
        "snag": snag,
    }


def _bowl(params, rng, material):
    r, t = params["diameter_m"] / 2, params["section_m"]
    lods = []
    for k, (around, up) in enumerate(SEGMENTS):
        mesh = Mesh()
        grid, uvs = [], []
        for i in range(up + 1):
            a = math.pi / 2 * i / up  # from the rim (0) to the apex
            row = [(r * math.cos(a) * math.cos(2 * math.pi * j / around), r * math.sin(a),
                    r * math.cos(a) * math.sin(2 * math.pi * j / around)) for j in range(around + 1)]
            grid.append(row)
            uvs.append([(2 * math.pi * r * j / around, r * a) for j in range(around + 1)])
        loft.sheet(mesh, grid, uvs, CHROME, lambda p: p, t if k == 0 else 0.0)
        lods.append(mesh)
    collision = [{"shape": "sphere", "center": [0.0, 0.0, 0.0], "radius": r, "material": material, "landable": False}]
    # its pressed rim on the ground, the sheet's edge at mid-thickness (the spec: an `edge` snag)
    around = SEGMENTS[0][0]
    rim = [[(r - t / 2) * math.cos(2 * math.pi * j / around), 0.0, (r - t / 2) * math.sin(2 * math.pi * j / around)]
           for j in range(around)]
    return lods, collision, [{"kind": "edge", "points": rim + rim[:1], "radius_m": t / 2}]


def _ring_centre(params, rng):
    """The ring's centre line: RING_CAPSULES * 4 points round the circle of its mean radius, kinked by bend_m."""
    rm = params["diameter_m"] / 2 - params["section_m"] / 2
    n = RING_CAPSULES * 4
    kinks = [rng.uniform(-1, 1) * params["bend_m"] for _ in range(RING_CAPSULES)]
    pts = []
    for j in range(n):
        a = 2 * math.pi * j / n
        f = j / 4
        k0, k1 = kinks[int(f) % RING_CAPSULES], kinks[(int(f) + 1) % RING_CAPSULES]
        d = k0 + (k1 - k0) * (f - int(f))
        pts.append(((rm + d) * math.cos(a), params["section_m"] / 2, (rm + d) * math.sin(a)))
    return pts


def _ring(params, rng, material):
    rs = params["section_m"] / 2
    centre = _ring_centre(params, rng)
    slot = SLOT[params["piece"]]
    lods = []
    for k, (_, sides) in enumerate(SEGMENTS):
        mesh = Mesh()
        path = centre if k == 0 else centre[::2]
        grid, uvs = [], []
        for i, p in enumerate(path + path[:1]):
            radial = (p[0], 0.0, p[2])
            length = math.hypot(p[0], p[2])
            radial = (radial[0] / length, 0.0, radial[2] / length)
            row = [(p[0] + rs * math.cos(2 * math.pi * j / sides) * radial[0], p[1] + rs * math.sin(2 * math.pi * j / sides),
                    p[2] + rs * math.cos(2 * math.pi * j / sides) * radial[2]) for j in range(sides + 1)]
            grid.append(row)
            uvs.append([(0.1 * i, 2 * math.pi * rs * j / sides) for j in range(sides + 1)])

        def outward(q, path=path):
            best = min(path, key=lambda c: (c[0] - q[0]) ** 2 + (c[1] - q[1]) ** 2 + (c[2] - q[2]) ** 2)
            return (q[0] - best[0], q[1] - best[1], q[2] - best[2])
        loft.surface(mesh, grid, uvs, slot, outward)
        lods.append(mesh)
    corners = centre[::4]
    collision = [meta.capsule(a, b, rs, material) for a, b in zip(corners, corners[1:] + corners[:1])]
    kind = "hook" if params["piece"] == "bead_wire" else "edge"
    snag = [{"kind": kind, "points": [list(p) for p in centre + centre[:1]], "radius_m": rs}]
    return lods, collision, snag


def _plate(params, rng, material):
    length, width, t, bend = params["length_m"], params["width_m"], params["section_m"], params["bend_m"]
    teeth = 9
    torn = {end: [rng.uniform(0, TORN_M) for _ in range(teeth + 1)] for end in (-1, 1)}
    lods = []
    for k, (n_len, n_wid) in enumerate(((12, teeth), (4, 3))):
        mesh = Mesh()
        grid, uvs = [], []
        for i in range(n_len + 1):
            f = i / n_len
            row, row_uv = [], []
            for j in range(n_wid + 1):
                g = j / n_wid
                inset = 0.0
                if k == 0 and i in (0, n_len):
                    inset = torn[-1 if i == 0 else 1][round(g * teeth)]
                x = -length / 2 + f * length + (inset if i == 0 else -inset if i == n_len else 0.0)
                y = t + bend * math.sin(math.pi * (x / length + 0.5))
                z = -width / 2 + g * width
                row.append((x, y, z))
                row_uv.append((x, z))
            grid.append(row)
            uvs.append(row_uv)
        loft.sheet(mesh, grid, uvs, CHROME, lambda p: (0.0, 1.0, 0.0), t if k == 0 else 0.0)
        lods.append(mesh)
    collision = [{"shape": "box", "center": [0.0, (t + bend) / 2, 0.0], "size": [length, t + bend, width],
                  "rotation": [0.0, 0.0, 0.0, 1.0], "material": material, "landable": False}]
    pts = [(x, t / 2 + bend * math.sin(math.pi * (x / length + 0.5)), z) for x, z in
           [(-length / 2, -width / 2), (length / 2, -width / 2), (length / 2, width / 2), (-length / 2, width / 2),
            (-length / 2, -width / 2)]]
    snag = [{"kind": "edge", "points": [list(p) for p in pts], "radius_m": t / 2}]
    return lods, collision, snag
