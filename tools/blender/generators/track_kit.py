"""track_kit: T01, the race track's kit (w20-hunter-track design 5 and 6; docs/object-sheets/hunter-track.md): one
variant per drawn kind, placed by the map at the layout's poses unchanged, so each variant's origin is where the
sheet says. Portion 1 draws simple true-size shapes (boxes, tubes, real frame members round real openings); portion 2
replaces the shapes behind the same variant ids.

Every size is a row of the variant's params, {"name", "value", "label"}, the label the sheet's: F measured from the
track file, C measured on the clips, D derived, A assumed; a correction is a one-row change. Each part's surface and
colour is a row of "paints", {"part", "slot", "colour"}, the slot a physics material id.

Local axes, read from the layout's poses (measured): a gate's opening faces local z (every checkpoint route through
a gate runs along it), so its width is x and its height y; a block's length is z; a steel bar runs along +y from its
origin, at one end; an LED flag's plate has y for its normal, the normal of the wall it is fixed to; a large board has
x for its normal and its long side along z. Assumed for the kinds the file does not settle, each one row away: the
other bars lie along y, the arrow's plate has y for its normal, a small board stands along y from a short edge.

The two large boards are a variant per layout row: no single origin puts both where the clips show them (the sheet's
corrections), and one shared origin would push board 418 through the house's roof. Each holds its board's centre in
its own local frame, derived from the spans the clips give.

Physics: collision is the drawn parts' own boxes, cylinders, capsules and sphere; a gate's opening and a tyre's hole
are gaps of their real clear space; the tape's band has no hard collision (a micro mesh) but a snag record, and the
light bars and the arc carry snag records too. Solid bodies carry solid wind volumes (porosity 0, the existing rule
of lib.godot and generators.pole); gate openings stay open, and thin bars, flags, stakes and legs are no obstacle
on the 2 m wind grid (assumed).

A ribbon is one variant per layout row: the row stands at the world's origin and its points are world positions, so
the variant's mesh is drawn there. The band lies flat on its points (their stored normals are the ground's), a thin
strip lifted clear of the ground; the file ends each of four ribbons in a point 8 mm past the last, which is dropped.
"""
import json
import math
import os
from collections import namedtuple

from lib import meta
from lib.geometry import Mesh
from lib.solids import AXES, Frame, box, lathe, normalize, palette_uv, prism, tube

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
LAYOUT = os.path.join("maps", "hunter-09", "layout.json")
SLOTS = ("concrete", "fabric", "plastic", "rubber", "steel", "timber")  # each the physics material of the same id
COLOURS = ("concrete", "rubber", "white", "black", "red", "blue", "yellow", "green", "steel", "timber")  # the columns
                                                                                     # of materials/track_kit.json
LABELS = ("F", "C", "D", "A")
SIDES = (16, 12, 8)         # per LOD: a round body's sides, multiples of 4 so its bounds keep the true diameter
THIN_SIDES = (6, 4, 3)      # per LOD: a stake's or a leg's
CONE_SLICES = 6             # stacked cylinders of a cone: each within 3 cm of its wall (AST-05 allows 5)
PYLON_SLICES = 4            # stacked boxes of the tapered sail: each within 2.2 cm of its edge
ARC_SEGMENTS = 12
DUPLICATE_POINT_M = 0.05    # assumed: a ribbon's point this close to the previous one is the file's end marker
MEMBER_TOLERANCE_M = 1e-6
Y_AXIS = Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0))  # (a, b, c) = (x, z, y)
IDENTITY = [0.0, 0.0, 0.0, 1.0]

PARAMS = {
    "kind": {"type": "str", "range": "a neutral kind of KINDS", "default": "concrete_block",
             "effect": "which shape is drawn, and which rows and parts it takes"},
    "rows": {"type": "list", "range": "the kind's rows, each {name, value > 0 (an offset may be negative), label F|C|D|A}", "default": [],
             "effect": "every size, each one row to correct"},
    "paints": {"type": "list", "range": "the kind's parts, each {part, slot, colour}", "default": [],
               "effect": "each part's physics surface and palette colour"},
    "lod.lod1_switch_m": {"type": "float", "range": (1.0, 200.0), "default": 20.0,
                          "effect": "a round kind's LOD1 from here (a kind of boxes has one LOD)"},
    "lod.lod2_switch_m": {"type": "float", "range": (2.0, 500.0), "default": 60.0, "effect": "its LOD2 from here"},
    "lod.cull_m": {"type": "float", "range": (50.0, 2000.0), "default": 800.0, "effect": "culled beyond"},
}

# draw(mesh, q) adds the part at LOD q; shapes are its collision; wind its wind volumes; lods how many LODs it can use
Part = namedtuple("Part", "draw shapes wind lods")


def _solid(shape):
    """A collision shape as a solid wind volume."""
    return {k: v for k, v in shape.items() if k not in ("material", "landable", "part")} | {"porosity": 0.0}


def _box(lo, hi, paint, landable=False, wind=False):
    shape = {"shape": "box", "center": [(a + b) / 2 for a, b in zip(lo, hi)], "size": [b - a for a, b in zip(lo, hi)],
             "rotation": list(IDENTITY), "material": paint[0], "landable": landable}
    return Part(lambda mesh, q: box(mesh, AXES, lo, hi, paint), [shape], [_solid(shape)] if wind else [], 1)


def _rod(p0, p1, diameter, paint):
    """A thin round member, a stake or a leg: no wind obstacle."""
    r = diameter / 2
    d = normalize([p1[a] - p0[a] for a in range(3)])
    shape = {"shape": "cylinder", "center": [(p0[a] + p1[a]) / 2 for a in range(3)], "radius": r,
             "height": math.dist(p0, p1), "rotation": meta.rotation_from_y(d), "material": paint[0], "landable": False}

    def draw(mesh, q):
        tube(mesh, [tuple(p0), tuple(p1)], r, THIN_SIDES[q], paint)
        mesh.smooth.add(paint[0])

    return Part(draw, [shape], [], len(THIN_SIDES))


def _gate_gap(width, sill, height):
    """The opening's clear space in the plane z = 0, facing z."""
    x, top = width / 2, sill + height
    return {"id": "opening", "polygon": [[-x, sill, 0.0], [x, sill, 0.0], [x, top, 0.0], [-x, top, 0.0]],
            "normal": [0.0, 0.0, 1.0], "narrowest_m": min(width, height)}


def _edge(points, radius, material, edge_radius=None):
    record = {"kind": "edge", "points": [list(p) for p in points], "radius_m": radius, "material": material}
    return record | ({"edge_radius_m": edge_radius} if edge_radius is not None else {})


def _kit(parts, gaps=(), snag=(), micro=None):
    return {"parts": parts, "gaps": list(gaps), "snag": list(snag), "micro": micro}


# --- the kinds ------------------------------------------------------------------------------------------------------


def _bar(anchor, landable=False, wind=False, snag=False):
    """A kind that is one box of size_x_m, size_y_m, size_z_m, its origin at its centre or, for "foot", at the centre
    of its lower y face. `snag`: an edge round its rim in the xz plane, a plate's, of its half-thickness."""

    def build(r, c):
        x, y, z = r["size_x_m"] / 2, r["size_y_m"], r["size_z_m"] / 2
        y0, y1 = (0.0, y) if anchor == "foot" else (-y / 2, y / 2)
        rim = [(-x, 0.0, -z), (x, 0.0, -z), (x, 0.0, z), (-x, 0.0, z), (-x, 0.0, -z)]
        records = [_edge(rim, y / 2, c["main"][0])] if snag else []
        return _kit([_box((-x, y0, -z), (x, y1, z), c["main"], landable, wind)], snag=records)

    return build


def _board(r, c):
    """A large board: a panel of size_x_m (its normal) by size_y_m by size_z_m, centred at (0, centre_y_m, centre_z_m)
    of its row's own origin."""
    x, y, z = r["size_x_m"] / 2, r["size_y_m"] / 2, r["size_z_m"] / 2
    cy, cz = r["centre_y_m"], r["centre_z_m"]
    return _kit([_box((-x, cy - y, cz - z), (x, cy + y, cz + z), c["main"], wind=True)])


def _pylon(r, c):
    h, t = r["height_m"], r["thick_m"] / 2
    base, tip = r["base_w_m"] / 2, r["tip_w_m"] / 2
    paint = c["main"]

    def draw(mesh, q):
        prism(mesh, Y_AXIS, [(-base, 0.0), (base, 0.0), (tip, h), (-tip, h)], -t, t, paint)

    slices = []
    for k in range(PYLON_SLICES):
        y0, y1 = h * k / PYLON_SLICES, h * (k + 1) / PYLON_SLICES
        half = base + (tip - base) * (y0 + y1) / (2 * h)
        slices.append(_box((-half, y0, -t), (half, y1, t), paint, wind=True))
    return _kit([Part(draw, [s for p in slices for s in p.shapes], [w for p in slices for w in p.wind], 1)])


def _cone(r, c):
    h, band = r["height_m"], r["band_h_m"]
    r0, r1 = r["base_d_m"] / 2, r["tip_d_m"] / 2
    radius = lambda y: r0 + (r1 - r0) * y / h

    def draw(mesh, q):
        lathe(mesh, Y_AXIS, [(0.0, 0.0), (0.0, r0), (band, radius(band)), (h, r1), (h, 0.0)], SIDES[q],
              [c["band"], c["band"], c["main"], c["main"]])
        mesh.smooth.update((c["band"][0], c["main"][0]))

    shapes = []
    for k in range(CONE_SLICES):
        y0, y1 = h * k / CONE_SLICES, h * (k + 1) / CONE_SLICES
        shapes.append({"shape": "cylinder", "center": [0.0, (y0 + y1) / 2, 0.0], "radius": radius((y0 + y1) / 2),
                       "height": y1 - y0, "rotation": list(IDENTITY), "material": c["main"][0], "landable": False})
    return _kit([Part(draw, shapes, [_solid(s) for s in shapes], len(SIDES))])


def _tyre(r, c):
    """A ring of a rectangular section about y: its hole's flats stand at the hole's true radius, so the clear space
    is the hole's diameter, and one box per side of LOD0 fills the ring exactly (each box's outer corners are the
    outer polygon's vertices; its inner ones lie in the rubber)."""
    outer, hole, h = r["outer_d_m"] / 2, r["hole_d_m"] / 2, r["height_m"]
    paint = c["main"]

    def draw(mesh, q):
        inner = hole / math.cos(math.pi / SIDES[q])
        lathe(mesh, Y_AXIS, [(0.0, inner), (0.0, outer), (h, outer), (h, inner)], SIDES[q], [paint] * 4, closed=True)
        mesh.smooth.add(paint[0])

    n = SIDES[0]
    flat = outer * math.cos(math.pi / n)
    half = outer * math.sin(math.pi / n)
    shapes = []
    for k in range(n):
        t = (k + 0.5) * 2 * math.pi / n
        mid = (hole + flat) / 2
        shapes.append({"shape": "box", "center": [mid * math.cos(t), h / 2, mid * math.sin(t)],
                       "size": [flat - hole, h, 2 * half], "rotation": [0.0, math.sin(-t / 2), 0.0, math.cos(-t / 2)],
                       "material": paint[0], "landable": False})
    wind = [{"shape": "cylinder", "center": [0.0, h / 2, 0.0], "radius": outer, "height": h,
             "rotation": list(IDENTITY), "porosity": 0.0}]  # a ring is solid to a horizontal path through it
    corner = hole / math.cos(math.pi / n)
    polygon = [[corner * math.cos(2 * math.pi * k / n), h / 2, corner * math.sin(2 * math.pi * k / n)]
               for k in range(n)]
    gap = {"id": "hole", "polygon": polygon, "normal": [0.0, 1.0, 0.0], "narrowest_m": 2 * hole}
    return _kit([Part(draw, shapes, wind, len(SIDES))], gaps=[gap])


def _frame_arch(r, c):
    """Two posts standing on the ground and a top banner over them, the opening open to the ground."""
    w, h, z = r["width_m"] / 2, r["height_m"], r["depth_m"] / 2
    x, top = r["opening_w_m"] / 2, r["opening_h_m"]
    parts = [_box((sx0, 0.0, -z), (sx1, top, z), c["post"], wind=True) for sx0, sx1 in ((-w, -x), (x, w))]
    parts.append(_box((-w, top, -z), (w, h, z), c["banner"], wind=True))
    return _kit(parts, gaps=[_gate_gap(r["opening_w_m"], 0.0, r["opening_h_m"])])


def _frame_square(r, c):
    """A square head of two posts between a bottom and a top banner, on two thin legs."""
    w, z, leg = r["head_w_m"] / 2, r["depth_m"] / 2, r["leg_len_m"]
    x = r["opening_w_m"] / 2
    sill = leg + r["banner_h_m"]
    top, head_top = sill + r["opening_h_m"], leg + r["head_h_m"]
    parts = [_box((-w, leg, -z), (w, sill, z), c["banner"], wind=True),
             _box((-w, top, -z), (w, head_top, z), c["banner"], wind=True)]
    parts += [_box((a, sill, -z), (b, top, z), c["post"], wind=True) for a, b in ((-w, -x), (x, w))]
    parts += [_rod((sx * r["leg_x_m"], 0.0, 0.0), (sx * r["leg_x_m"], leg, 0.0), r["leg_d_m"], c["leg"])
              for sx in (-1.0, 1.0)]
    return _kit(parts, gaps=[_gate_gap(r["opening_w_m"], sill, r["opening_h_m"])])


def _light_gate(r, c):
    """A ring of four members; portion 2 rounds its corners."""
    w, h, z, rim = r["width_m"] / 2, r["height_m"], r["depth_m"] / 2, r["rim_m"]
    x, top = r["opening_w_m"] / 2, rim + r["opening_h_m"]
    paint = c["main"]
    parts = [_box((-w, 0.0, -z), (w, rim, z), paint, wind=True), _box((-w, top, -z), (w, h, z), paint, wind=True),
             _box((-w, rim, -z), (-x, top, z), paint, wind=True), _box((x, rim, -z), (w, top, z), paint, wind=True)]
    return _kit(parts, gaps=[_gate_gap(r["opening_w_m"], rim, r["opening_h_m"])])


def _ball(r, c):
    radius = r["diameter_m"] / 2
    paint = c["main"]

    def draw(mesh, q):
        n = SIDES[q] // 2
        profile = [(-radius * math.cos(math.pi * k / n), radius * math.sin(math.pi * k / n)) for k in range(n + 1)]
        profile[0], profile[-1] = (-radius, 0.0), (radius, 0.0)
        lathe(mesh, Y_AXIS, profile, SIDES[q], [paint] * n)
        mesh.smooth.add(paint[0])

    shape = {"shape": "sphere", "center": [0.0, 0.0, 0.0], "radius": radius, "material": paint[0], "landable": False}
    return _kit([Part(draw, [shape], [], len(SIDES))])


def arc_points(r):
    """The arc's centre line in the local xy plane: its middle at the origin, its chord along y, bowing towards -x."""
    theta = math.radians(r["arc_deg"])
    radius = r["length_m"] / theta
    angles = [theta * (k / ARC_SEGMENTS - 0.5) for k in range(ARC_SEGMENTS + 1)]
    return [(radius * (math.cos(t) - 1.0), radius * math.sin(t), 0.0) for t in angles]


def _curve(r, c):
    points, radius, paint = arc_points(r), r["tube_d_m"] / 2, c["main"]

    def draw(mesh, q):
        tube(mesh, points, radius, THIN_SIDES[q], paint)
        mesh.smooth.add(paint[0])

    shapes = [meta.capsule(a, b, radius, paint[0]) for a, b in zip(points, points[1:])]
    return _kit([Part(draw, shapes, [], len(THIN_SIDES))], snag=[_edge(points, radius, paint[0])])


def _tent(r, c):
    """A thin flat canopy at the full height on four corner legs, open underneath; portion 2 draws the peak."""
    f, h, t, d = r["footprint_m"] / 2, r["height_m"], r["roof_t_m"], r["leg_d_m"]
    eave = h - t
    parts = [_box((-f, eave, -f), (f, h, f), c["roof"], wind=True)]
    at = f - d / 2
    parts += [_rod((sx * at, 0.0, sz * at), (sx * at, eave, sz * at), d, c["leg"])
              for sx in (-1.0, 1.0) for sz in (-1.0, 1.0)]
    return _kit(parts)


def ribbon_points(instance_id, root=REPO_ROOT):
    """The ribbon row's points in world metres, without the file's end marker (DUPLICATE_POINT_M)."""
    with open(os.path.join(root, LAYOUT), encoding="utf-8") as f:
        rows = json.load(f)["objects"]
    row = next((o for o in rows if o["instance_id"] == instance_id and o["kind"] == "ribbon_tape"), None)
    if row is None:
        raise ValueError(f"the layout has no ribbon_tape row {instance_id}")
    points = []
    for point in row["points"]:
        if not points or math.dist(point["position_m"], points[-1]) >= DUPLICATE_POINT_M:
            points.append(tuple(point["position_m"]))
    return points


def _strip(mesh, points, width, thickness, lift, paint):
    """The flat band along `points`, its width horizontal and mitred at each corner, `lift` above them."""
    slot, uv = paint
    across = []
    for i in range(len(points)):
        ds = []
        for a, b in ((i - 1, i), (i, i + 1)):
            if 0 <= a and b < len(points):
                d = normalize((points[b][0] - points[a][0], 0.0, points[b][2] - points[a][2]))
                ds.append((-d[2], 0.0, d[0]))
        m = normalize(tuple(sum(d[a] for d in ds) for a in range(3)))
        k = 1.0 / max(0.3, m[0] * ds[0][0] + m[2] * ds[0][2])
        across.append((m[0] * k * width / 2, 0.0, m[2] * k * width / 2))
    rings = []
    for p, a in zip(points, across):
        y0, y1 = p[1] + lift, p[1] + lift + thickness
        corners = ((p[0] + a[0], y1, p[2] + a[2]), (p[0] - a[0], y1, p[2] - a[2]),
                   (p[0] - a[0], y0, p[2] - a[2]), (p[0] + a[0], y0, p[2] + a[2]))
        rings.append([mesh.vertex(c) for c in corners])

    def tri(ids, up):
        a, b, c = (mesh.vertices[i] for i in ids)
        n = normalize(_cross([b[k] - a[k] for k in range(3)], [c[k] - a[k] for k in range(3)]))
        n = n if (n[1] > 0) == up else tuple(-v for v in n)
        mesh.face(ids, n, slot, [uv] * 3)

    for i in range(len(points) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for ids, up in (([r0[0], r0[1], r1[1]], True), ([r0[0], r1[1], r1[0]], True),  # triangles: a sloped bend's
                        ([r0[3], r0[2], r1[2]], False), ([r0[3], r1[2], r1[3]], False)):  # quad is not planar
            tri(ids, up)
        d = normalize((points[i + 1][0] - points[i][0], 0.0, points[i + 1][2] - points[i][2]))
        side = (-d[2], 0.0, d[0])
        mesh.face([r0[0], r1[0], r1[3], r0[3]], side, slot, [uv] * 4)
        mesh.face([r0[1], r1[1], r1[2], r0[2]], tuple(-v for v in side), slot, [uv] * 4)
    for ring, a, b in ((rings[0], 0, 1), (rings[-1], -2, -1)):
        d = normalize((points[b][0] - points[a][0], 0.0, points[b][2] - points[a][2]))
        out = tuple(-v for v in d) if ring is rings[0] else d
        mesh.face(ring, out, slot, [uv] * 4)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _ribbon(r, c):
    points = ribbon_points(int(r["instance_id"]))
    w, t, lift = r["band_w_m"], r["band_t_m"], r["band_lift_m"]
    parts = [_rod(p, (p[0], p[1] + r["stake_h_m"], p[2]), r["stake_d_m"], c["stake"]) for p in points]

    def micro(q):
        mesh = Mesh()
        _strip(mesh, points, w, t, lift, c["band"])
        return {"lod": q, "kind": "band", "count": len(points) - 1, "mesh": mesh}

    centre = [(p[0], p[1] + lift + t / 2, p[2]) for p in points]
    return _kit(parts, snag=[_edge(centre, w / 2, c["band"][0], t / 2)], micro=micro)


TYRE_ROWS = ("outer_d_m", "hole_d_m", "height_m")
BOX_ROWS = ("size_x_m", "size_y_m", "size_z_m")
SIGNED_ROWS = ("centre_y_m", "centre_z_m")  # offsets, the only rows that may be negative
FRAME_SQUARE_ROWS = ("head_w_m", "head_h_m", "depth_m", "opening_w_m", "opening_h_m", "post_w_m", "banner_h_m",
                     "leg_len_m", "leg_d_m", "leg_x_m")

# kind -> (its builder, the rows it takes, the parts it paints)
KINDS = {
    "concrete_block": (_bar("foot", landable=True, wind=True), BOX_ROWS, ("main",)),
    "pylon_3m": (_pylon, ("height_m", "base_w_m", "tip_w_m", "thick_m"), ("main",)),
    "cone_light": (_cone, ("height_m", "base_d_m", "tip_d_m", "band_h_m"), ("main", "band")),
    "tyre_red": (_tyre, TYRE_ROWS, ("main",)),
    "tyre_plain": (_tyre, TYRE_ROWS, ("main",)),
    "tyre_blue": (_tyre, TYRE_ROWS, ("main",)),
    "tyre_yellow": (_tyre, TYRE_ROWS, ("main",)),
    "ribbon_tape": (_ribbon, ("instance_id", "band_w_m", "band_t_m", "band_lift_m", "stake_d_m", "stake_h_m"),
                    ("band", "stake")),
    "frame_8x10": (_frame_arch, ("width_m", "height_m", "depth_m", "opening_w_m", "opening_h_m", "post_w_m",
                                 "banner_h_m"), ("post", "banner")),
    "frame_square_a": (_frame_square, FRAME_SQUARE_ROWS, ("post", "banner", "leg")),
    "frame_square_b": (_frame_square, FRAME_SQUARE_ROWS, ("post", "banner", "leg")),
    "light_gate": (_light_gate, ("width_m", "height_m", "depth_m", "rim_m", "opening_w_m", "opening_h_m"), ("main",)),
    "steel_profile": (_bar("foot"), BOX_ROWS, ("main",)),
    "led_flag_green": (_bar("centre", snag=True), BOX_ROWS, ("main",)),
    "led_flag": (_bar("centre", snag=True), BOX_ROWS, ("main",)),
    "light_rig": (_bar("centre"), BOX_ROWS, ("main",)),
    "arrow_red": (_bar("centre"), BOX_ROWS, ("main",)),
    "board_large": (_board, BOX_ROWS + SIGNED_ROWS, ("main",)),
    "board_small": (_bar("foot", wind=True), BOX_ROWS, ("main",)),
    "track_light": (_bar("centre"), BOX_ROWS, ("main",)),
    "led_ball": (_ball, ("diameter_m",), ("main",)),
    "light_curve": (_curve, ("length_m", "tube_d_m", "arc_deg"), ("main",)),
    "tent": (_tent, ("footprint_m", "height_m", "roof_t_m", "leg_d_m"), ("roof", "leg")),
    "notice_board": (_bar("foot", wind=True), BOX_ROWS, ("main",)),
}


def rows_of(params):
    """The params' rows as name -> value."""
    return {row["name"]: row["value"] for row in params["rows"]}


def paints_of(params):
    """The params' parts as part -> (slot, palette uv)."""
    return {p["part"]: (p["slot"], palette_uv(COLOURS.index(p["colour"]), len(COLOURS))) for p in params["paints"]}


def check(params):
    """The kind known; its rows and parts exactly the kind's, each row positive and labelled, each part on a slot and
    a palette colour; a gate's members adding up to its outer size; the LODs in order."""
    kind = params["kind"]
    if kind not in KINDS:
        return [f"'kind' {kind!r} is not one of {', '.join(KINDS)}"]
    _, row_names, part_names = KINDS[kind]
    problems = []
    if sorted(str(row.get("name")) for row in params["rows"]) != sorted(row_names):
        problems.append(f"{kind}: 'rows' must name exactly {', '.join(row_names)}")
    for row in params["rows"]:
        value = row.get("value")
        if isinstance(value, bool) or not isinstance(value, (int, float)) \
                or (value <= 0 and row.get("name") not in SIGNED_ROWS):
            problems.append(f"{kind}: row {row.get('name')!r} needs a positive value, got {value!r}")
        if row.get("label") not in LABELS:
            problems.append(f"{kind}: row {row.get('name')!r} needs a label of {', '.join(LABELS)}")
    if sorted(str(p.get("part")) for p in params["paints"]) != sorted(part_names):
        problems.append(f"{kind}: 'paints' must name exactly {', '.join(part_names)}")
    for p in params["paints"]:
        if p.get("slot") not in SLOTS or p.get("colour") not in COLOURS:
            problems.append(f"{kind}: part {p.get('part')!r} needs a slot of {SLOTS} and a colour of {COLOURS}")
    if problems:
        return problems
    problems += _member_problems(kind, rows_of(params))
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append("the LOD switches and the cull must increase")
    return problems


def _member_problems(kind, r):
    """A gate's outer size against its opening and members, so a corrected row cannot leave a frame that disagrees."""
    pairs = []
    if kind == "frame_8x10":
        pairs = [("width_m", r["opening_w_m"] + 2 * r["post_w_m"], r["width_m"]),
                 ("height_m", r["opening_h_m"] + r["banner_h_m"], r["height_m"])]
    elif kind in ("frame_square_a", "frame_square_b"):
        pairs = [("head_w_m", r["opening_w_m"] + 2 * r["post_w_m"], r["head_w_m"])]
        if r["head_h_m"] <= r["opening_h_m"] + r["banner_h_m"]:
            return [f"{kind}: head_h_m must exceed the opening and the bottom banner"]
        if r["leg_x_m"] + r["leg_d_m"] / 2 > r["head_w_m"] / 2:
            return [f"{kind}: the legs must stand under the head"]
    elif kind == "light_gate":
        pairs = [("width_m", r["opening_w_m"] + 2 * r["rim_m"], r["width_m"]),
                 ("height_m", r["opening_h_m"] + 2 * r["rim_m"], r["height_m"])]
    return [f"{kind}: {name} is {have}, but its opening and members add up to {want:.6f}"
            for name, want, have in pairs if abs(want - have) > MEMBER_TOLERANCE_M]


def build(params):
    kind = params["kind"]
    kit = KINDS[kind][0](rows_of(params), paints_of(params))
    parts = kit["parts"]
    count = max(p.lods for p in parts)
    lods = []
    for q in range(count):
        mesh = Mesh()
        for part in parts:
            part.draw(mesh, min(q, part.lods - 1))
        lods.append(mesh)
    micro = [kit["micro"](q) for q in range(count)] if kit["micro"] else []
    lod = params["lod"]
    switches = [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]][:count]
    slots = sorted({slot for mesh in lods + [m["mesh"] for m in micro] for slot in mesh.slots()})
    collision = [shape for part in parts for shape in part.shapes]
    built = {
        "lods": lods,
        "switch_distances_m": switches,
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": slot} for slot in slots],
        "collision": collision,
        "gaps": kit["gaps"],
        "wind_volumes": [w for part in parts for w in part.wind],
        "snag": kit["snag"],
        "report": [f"{kind}: {len(collision)} collision shapes, {len(kit['gaps'])} gaps, {len(kit['snag'])} snags"],
    }
    if micro:
        built["micro"] = micro
    return built
