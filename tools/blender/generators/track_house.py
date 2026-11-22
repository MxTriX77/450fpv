"""track_house: T02, the race track's three houses (w20-hunter-track design 9; docs/object-sheets/hunter-track.md, "The
three houses"): one building type, one variant per house, because each house's openings stand on its own markers.

Our own design at the sheet's dimensions, drawn in the village's house materials by reference (their recipes are of
kind "vendor"): the walls a real 0.18 m thick with board courses on both faces, frames round every window and door,
a porch on four posts, brick piers, a floor of boards, an open gable roof on rafters and a chimney. No opening holds
glass or a leaf.

Frame: asset x = the sheet's v (across, the door wall's outer face at 0, the porch at negative v), y = h (above the
finished floor), z = u (along the ridge, end A's outer face at 0). So a house stands at the sheet's (ox, floor y, oz)
with yaw b about +y, unmirrored.

Every size is a row of the params, {"name", "value", "label"}, the label the sheet's: F measured from the track file,
C on the clips, D derived, A assumed. What the sheet leaves to the builder is a constant here, marked assumed: the
boards, the trim, the rafters' section (inside the sheet's 0.08 m roof, so nothing hangs below its underside where the
slots run), the porch beam and the birch's limbs.

Physics: the walls' collision is boxes of the wall less its openings, a tilted slot's edges exact on four rotated rim
boxes and a sloped edge stepped within RAKE_TOLERANCE_M; every drawn member that stands proud or apart has its own box.
Every opening is a gap of its clear space, marked ones named by their marker. Snag records follow the openings' edges,
the eaves and the birch's limbs, about 60 segments a house, far under the compiler's 2048 per 2 m cube.
"""
import bisect
import math
import random
from collections import namedtuple

from generators.track_kit import COLOURS
from lib import meta
from lib.geometry import Mesh
from lib.solids import palette_uv, tube

# The pilot's raw build (2026-11-22): lib.audit's full collision search does not finish on these thin-board meshes, so
# they answer to the stand-in audit (bounds and gaps) until task 1.3c makes the full one fast enough. Remove it then.
STAND_IN = True

LABELS = ("F", "C", "D", "A")
PHYSICS = {"boards": "timber", "post": "timber", "frame": "timber", "floor": "timber", "roof": "asbestos_cement",
           "brick": "masonry", "bark": "bark", "batten": "timber"}
BODY_ROWS = ("length_m", "width_m", "wall_t_m", "door_wall_top_m", "ridge_v_m", "pitch_deg", "roof_under_m",
             "roof_sheet_m", "eave_m", "verge_m", "porch_m", "post_m", "soffit_in_m", "soffit_out_m", "pier_u_size_m",
             "pier_v_size_m", "chimney_u0_m", "chimney_v0_m", "chimney_v_top_m", "chimney_step_m", "chimney_top_m",
             "floor_under_m", "ground_a_m", "ground_b_m")
BIRCH_ROWS = ("trunk_u_m", "trunk_v_m", "trunk_d_m", "trunk_top_m", "batten_u0_m", "batten_u1_m", "batten_v0_m",
              "batten_v1_m", "batten_h0_m", "batten_h1_m")
OPENING_KINDS = ("door", "pane", "crawl", "slot")
WALLS = ("door", "A", "B")

# assumed: what the sheet leaves to the builder
BOARD_M = 0.165             # a course's pitch
BOARD_DEPTH_M = 0.022       # the boards' depth on the core, so a groove between courses is real relief
GROOVE_M = 0.010
JOINT_M = 0.005             # a butt joint between two boards of a course
PLANK_M = (1.3, 2.2)        # a board's length; the atlas's board panels hold 2.26 m at the courses' scale
TRIM_PROUD_M = 0.015        # under the 20 mm the collision may lie off the drawn shape, so the trim needs no boxes
WINDOW_TRIM_M = 0.07
DOOR_TRIM_M = 0.10
MEMBER_T_M = (0.05, 0.13)   # the depth a mullion and a transom take in the wall
RAFTER_W_M = 0.07
RAFTER_D_M = 0.055          # inside the sheet's 0.08: the rafters' underside is the sheet's measured underside
RAFTER_PITCH_M = 0.90
SOFFIT_T_M = 0.025
SOFFIT_BOARD_M = 0.18
BEAM_D_M = 0.14
FLOOR_BOARD_M = 0.14
FLOOR_PIECE_M = 4.5
CAP_M = 0.09                # the ridge cap's reach down each slope
CAP_T_M = 0.008
PIER_SINK_M = 0.05          # piers and the trunk reach this far below the sheet's ground, so a blend never floats them
BRICK_M = (1.08, 0.60)      # the atlas's brick patch at true scale
LIMBS = 4
TRUNK_SIDES = (12, 8, 6)
LIMB_SIDES = (6, 4, 3)
BARK_BANDS = 9

# the collision
RIM_M = 0.07                # the rotated boxes along a tilted slot's edges
RAKE_TOLERANCE_M = 0.015    # a sloped edge's stair within this of the edge
RIM_COVER = 0.6             # a stair may reach this share of the rim's width past a tilted slot's grown edge
SNAG_R_M = 0.002            # a sawn arris (lib.joinery's K03 edge radius)
EPS = 1e-7

# The village atlases, as (u0, v0, u1, v1) of the texture (measured on the local maps, whose image y runs down).


def _rect(x0, y0, x1, y1):
    return (x0, 1.0 - y1, x1, 1.0 - y0)


BOARD_ROWS = tuple(_rect(0.0, a, 1.0, b)[1::2] for a, b in (
    (0.397, 0.4255), (0.4255, 0.4522), (0.4522, 0.4763), (0.4763, 0.5058), (0.5058, 0.531), (0.531, 0.556),
    (0.556, 0.5795), (0.5795, 0.609), (0.609, 0.6338), (0.6338, 0.6583), (0.6583, 0.6815), (0.6815, 0.7098),
    (0.7098, 0.7385), (0.7385, 0.765), (0.765, 0.791)))  # House_Wood01's horizontal boards, each row one board
BOARD_PANELS = ((0.03, 0.515), (0.58, 0.965))            # their two panels between the posts
BOARD_U_PER_M = 0.17                                     # a course's scale along the board
BOARD_FAR = _rect(0.03, 0.397, 0.515, 0.791)             # the panel whole, for the far LOD's walls
SEAM_UV = (0.25, 1.0 - 0.397)                            # a dark seam, the core seen in a groove
POST_RECT = _rect(0.526, 0.39, 0.566, 0.81)              # House_Wood01's post
FRAME_RECTS = (_rect(0.538, 0.66, 0.574, 0.985), _rect(0.587, 0.67, 0.614, 0.985),
               _rect(0.777, 0.71, 0.797, 0.985))         # House_Windows' loose boards
FLOOR_RECT = _rect(0.41, 0.35, 0.99, 0.93)               # House_Wood02's floor
FLOOR_UV_PER_M = 0.065
ROOF_RECT = _rect(0.0, 0.237, 0.727, 0.537)              # House_IndoorWall's corrugated sheets
ROOF_UV_PER_M = (0.042, 0.056)                           # along the ridge, down the slope
BRICK_RECT = _rect(0.618, 0.096, 0.767, 0.207)           # House_IndoorWall's brick


def _p(type_, range_, default, effect):
    return {"type": type_, "range": range_, "default": default, "effect": effect}


PARAMS = {
    "seed": _p("int", (0, 2**31 - 1), 20, "the boards' lengths and atlas rows and the birch's limbs, by named streams"),
    "rows": _p("list", "BODY_ROWS, each {name, value, label F|C|D|A}", [], "every dimension, each one row to correct"),
    "post_u_m": _p("list", "the porch posts' centres along u", [], "the porch posts"),
    "pier_u_m": _p("list", "the piers' centres along u", [], "the piers, one per u and row"),
    "pier_v_m": _p("list", "the pier rows' centres along v", [], "the piers, one per u and row"),
    "openings": _p("list", "{marker, wall, kind, s, h, tilt_deg, width, height}", [],
                   "the marked openings, as the sheet's tables give them"),
    "windows": _p("list", "{wall, columns: [s, s], widths: [w, w], top: [h0, h1], lower: [h0, h1]}", [],
                  "the 2 x 2 windows, marked panes and unmarked"),
    "birch": _p("list", "BIRCH_ROWS as rows, or empty", [], "the stand-in birch and its batten (the north house)"),
    "lod.lod1_switch_m": _p("float", (5.0, 200.0), 30.0, "LOD1 from here: the boards' relief goes"),
    "lod.lod2_switch_m": _p("float", (20.0, 500.0), 100.0, "LOD2 from here: the walls' outer faces, roof and floor"),
    "lod.cull_m": _p("float", (200.0, 3000.0), 1500.0, "culled beyond"),
}


def rows_of(rows):
    return {row["name"]: row["value"] for row in rows}


def check(params):
    """Every row present, numeric and labelled; each opening on a known wall and of a known kind, each marker once;
    each window two columns and two rows in order; each marked pane one of its window's panes within 2 mm."""
    problems = []
    for key, names in (("rows", BODY_ROWS), ("birch", BIRCH_ROWS)):
        rows = params[key]
        if key == "birch" and not rows:
            continue
        if sorted(str(row.get("name")) for row in rows) != sorted(names):
            problems.append(f"'{key}' must name exactly {', '.join(names)}")
        for row in rows:
            if isinstance(row.get("value"), bool) or not isinstance(row.get("value"), (int, float)):
                problems.append(f"{key}: row {row.get('name')!r} needs a number")
            if row.get("label") not in LABELS:
                problems.append(f"{key}: row {row.get('name')!r} needs a label of {', '.join(LABELS)}")
    markers = [o.get("marker") for o in params["openings"]]
    if len(set(markers)) != len(markers):
        problems.append("a marker is listed twice")
    for o in params["openings"]:
        if o.get("wall") not in WALLS or o.get("kind") not in OPENING_KINDS:
            problems.append(f"opening {o.get('marker')}: wall of {WALLS} and kind of {OPENING_KINDS}")
        elif not (o["width"] > 0 and o["height"] > 0):
            problems.append(f"opening {o['marker']}: needs a positive size")
    for w in params["windows"]:
        if w.get("wall") not in ("A", "B") or len(w["columns"]) != 2 or len(w["widths"]) != 2:
            problems.append(f"window {w.get('wall')}: on end A or B with two columns")
        elif not (w["columns"][0] + w["widths"][0] / 2 < w["columns"][1] - w["widths"][1] / 2
                  and w["lower"][0] < w["lower"][1] < w["top"][0] < w["top"][1]):
            problems.append(f"window {w['wall']}: its columns and rows must not touch")
    if problems:
        return problems
    for o in (o for o in params["openings"] if o["kind"] == "pane"):
        if _pane_of(params["windows"], o) is None:
            problems.append(f"opening {o['marker']}: no pane of its window within 2 mm")
    lod = params["lod"]
    if not lod["lod1_switch_m"] < lod["lod2_switch_m"] < lod["cull_m"]:
        problems.append("the LOD switches and the cull must increase")
    return problems


def _pane_of(windows, o):
    """(window, column) of the top pane the marked opening `o` is, or None."""
    for w in windows:
        if w["wall"] != o["wall"]:
            continue
        for c in (0, 1):
            top = (w["top"][0] + w["top"][1]) / 2
            if abs(w["columns"][c] - o["s"]) <= 0.002 and abs(w["widths"][c] - o["width"]) <= 0.002 \
                    and abs(top - o["h"]) <= 0.002 and abs(w["top"][1] - w["top"][0] - o["height"]) <= 0.002:
                return w, c
    return None


# --- the walls in their own plane --------------------------------------------------------------------------------

Cell = namedtuple("Cell", "h0 h1 a0 a1 b0 b1 left right")  # at h0 it runs a0..b0 along s, at h1 a1..b1
Hole = namedtuple("Hole", "key kind polygon s h w ht tilt")


class Wall:
    """A wall in its plane: `at(s, h, t)` the asset point at depth t from the outer face, `out` the outer normal,
    `along` the direction of +s, `outline` a simple polygon in (s, h), `holes` its openings."""

    def __init__(self, name, at, out, along, outline, thick):
        self.name, self.at, self.out, self.along, self.outline, self.t = name, at, out, along, outline, thick
        self.holes = []

    @property
    def polygons(self):
        return [hole.polygon for hole in self.holes]


def _edges(polygon):
    """The polygon's edges that are not horizontal."""
    n = len(polygon)
    return [(polygon[i], polygon[(i + 1) % n]) for i in range(n) if abs(polygon[i][1] - polygon[(i + 1) % n][1]) > EPS]


def _x(edge, h):
    (x0, h0), (x1, h1) = edge
    return x0 + (x1 - x0) * (h - h0) / (h1 - h0)


def _crossing(edges, h):
    """The edges crossing height h, left to right."""
    hits = [(_x(e, h), i) for i, e in enumerate(edges) if min(e[0][1], e[1][1]) < h < max(e[0][1], e[1][1])]
    return [edges[i] for _, i in sorted(hits)]


def _meet(a, b):
    """The height where two edges cross, or None."""
    (x0, y0), (x1, y1) = a
    (x2, y2), (x3, y3) = b
    d = (x1 - x0) * (y3 - y2) - (y1 - y0) * (x3 - x2)
    if abs(d) < 1e-12:
        return None
    p = ((x2 - x0) * (y3 - y2) - (y2 - y0) * (x3 - x2)) / d
    q = ((x2 - x0) * (y1 - y0) - (y2 - y0) * (x1 - x0)) / d
    return y0 + p * (y1 - y0) if 0.0 <= p <= 1.0 and 0.0 <= q <= 1.0 else None


def cells(outline, holes, breaks=()):
    """The outline less its convex holes (which may reach past it), as trapezoids between the sorted heights of every
    vertex, every crossing of a hole's edge with the outline's, and `breaks`."""
    rim = _edges(outline)
    cuts = [_edges(hole) for hole in holes]
    levels = {p[1] for polygon in [outline, *holes] for p in polygon} | set(breaks)
    for edges in cuts:
        levels |= {h for a in edges for b in rim if (h := _meet(a, b)) is not None}
    lo, hi = min(p[1] for p in outline), max(p[1] for p in outline)
    levels = sorted(h for h in levels if lo <= h <= hi)
    out = []
    for h0, h1 in zip(levels, levels[1:]):
        if h1 - h0 < EPS:
            continue
        mid = (h0 + h1) / 2
        hits = _crossing(rim, mid)
        spans = [(hits[i], hits[i + 1]) for i in range(0, len(hits), 2)]
        for edges in cuts:
            inside = _crossing(edges, mid)
            if inside:
                spans = _minus(spans, inside[0], inside[-1], mid)
        out += [Cell(h0, h1, _x(a, h0), _x(a, h1), _x(b, h0), _x(b, h1), a, b) for a, b in spans]
    return out


def _minus(spans, left, right, h):
    """The spans (left edge, right edge) at height h less the part between two edges of a hole."""
    c, d = _x(left, h), _x(right, h)
    out = []
    for a, b in spans:
        if d <= _x(a, h) or c >= _x(b, h):
            out.append((a, b))
            continue
        if c > _x(a, h):
            out.append((a, left))
        if d < _x(b, h):
            out.append((right, b))
    return out


def rectangle(s, h, width, height, tilt_deg=0.0):
    """A rectangle centred at (s, h), its width turned tilt_deg from +s towards +h, counter-clockwise."""
    t = math.radians(tilt_deg)
    e, n = (math.cos(t), math.sin(t)), (-math.sin(t), math.cos(t))
    return [(s + a * width / 2 * e[0] + b * height / 2 * n[0], h + a * width / 2 * e[1] + b * height / 2 * n[1])
            for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]


House = namedtuple("House", "r walls windows openings birch posts piers L W T under ridge pitch porch seed")


def plan(params):
    """The house's walls with their openings, from the params."""
    r = rows_of(params["rows"])
    L, W, T, ridge = r["length_m"], r["width_m"], r["wall_t_m"], r["ridge_v_m"]
    pitch = math.radians(r["pitch_deg"])
    under = lambda v: r["roof_under_m"] - abs(v - ridge) * math.tan(pitch)
    porch = -r["porch_m"]
    gable = [(0.0, 0.0), (W, 0.0), (W, under(W)), (ridge, r["roof_under_m"]), (porch, under(porch)),
             (porch, r["soffit_out_m"]), (0.0, r["soffit_in_m"])]
    top = r["door_wall_top_m"]
    walls = {
        "door": Wall("door", lambda s, h, t: (t, h, s), (-1.0, 0.0, 0.0), (0.0, 0.0, 1.0),
                     [(T, 0.0), (L - T, 0.0), (L - T, top), (T, top)], T),
        "back": Wall("back", lambda s, h, t: (W - t, h, s), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0),
                     [(T, 0.0), (L - T, 0.0), (L - T, under(W)), (T, under(W))], T),
        "A": Wall("A", lambda s, h, t: (s, h, t), (0.0, 0.0, -1.0), (1.0, 0.0, 0.0), gable, T),
        "B": Wall("B", lambda s, h, t: (s, h, L - t), (0.0, 0.0, 1.0), (1.0, 0.0, 0.0), gable, T),
    }
    for o in params["openings"]:
        if o["kind"] in ("door", "slot"):
            polygon = rectangle(o["s"], o["h"], o["width"], o["height"], o["tilt_deg"])
            walls[o["wall"]].holes.append(Hole(f"{o['kind']}_{o['marker']}", o["kind"], polygon, o["s"], o["h"],
                                               o["width"], o["height"], o["tilt_deg"]))
    for w in params["windows"]:
        s0 = w["columns"][0] - w["widths"][0] / 2
        s1 = w["columns"][1] + w["widths"][1] / 2
        h0, h1 = w["lower"][0], w["top"][1]
        hole = Hole(f"window_{w['wall']}", "window", [(s0, h0), (s1, h0), (s1, h1), (s0, h1)], (s0 + s1) / 2,
                    (h0 + h1) / 2, s1 - s0, h1 - h0, 0.0)
        walls[w["wall"]].holes.append(hole)
    piers = [(u, v) for u in params["pier_u_m"] for v in params["pier_v_m"]]
    return House(r, walls, params["windows"], params["openings"], rows_of(params["birch"]), params["post_u_m"], piers,
                 L, W, T, under, ridge, pitch, porch, params["seed"])


# --- drawing ------------------------------------------------------------------------------------------------------


def _neg(v):
    return (-v[0], -v[1], -v[2])


def _add(p, d, k):
    return (p[0] + d[0] * k, p[1] + d[1] * k, p[2] + d[2] * k)


def _flat(mesh, points, normal, slot, uvs):
    """One face, a corner a cell's tip repeats dropped."""
    keep = []
    for p, uv in zip(points, uvs):
        if not keep or math.dist(p, keep[-1][0]) > 1e-6:
            keep.append((p, uv))
    if len(keep) > 1 and math.dist(keep[0][0], keep[-1][0]) <= 1e-6:
        keep.pop()
    if len(keep) >= 3:
        mesh.face([mesh.vertex(p) for p, _ in keep], normal, slot, [uv for _, uv in keep])


def _cuboid(mesh, origin, axes, lengths, slot, uv):
    """A closed box from `origin` along three unit axes. `uv(x, y)` maps its main faces (the two across the third
    axis); the narrow faces take the same map over a strip as wide as the box is deep."""
    ex, ey, ez = axes
    lx, ly, lz = lengths

    def at(x, y, z):
        return _add(_add(_add(origin, ex, x), ey, y), ez, z)

    for z, n in ((0.0, _neg(ez)), (lz, ez)):
        _flat(mesh, [at(0, 0, z), at(lx, 0, z), at(lx, ly, z), at(0, ly, z)], n, slot,
              [uv(0, 0), uv(lx, 0), uv(lx, ly), uv(0, ly)])
    strip_y, strip_x = min(lz, ly), min(lz, lx)
    for y, n in ((0.0, _neg(ey)), (ly, ey)):
        _flat(mesh, [at(0, y, 0), at(lx, y, 0), at(lx, y, lz), at(0, y, lz)], n, slot,
              [uv(0, 0), uv(lx, 0), uv(lx, strip_y), uv(0, strip_y)])
    for x, n in ((0.0, _neg(ex)), (lx, ex)):
        _flat(mesh, [at(x, 0, 0), at(x, ly, 0), at(x, ly, lz), at(x, 0, lz)], n, slot,
              [uv(0, 0), uv(0, ly), uv(strip_x, ly), uv(strip_x, 0)])


def _along(rect, lx, ly):
    """A map laying a box's length lx along the rect's V and its width ly across its U: a strip of the atlas."""
    u0, v0, u1, v1 = rect
    return lambda x, y: (u0 + (u1 - u0) * y / ly, v0 + (v1 - v0) * x / lx)


def _boards(mesh, wall, rng, relief, inner):
    """One face's courses of boards, each course cut into boards of its own lengths, each board its own row and stretch
    of the atlas, so nothing tiles. With `relief`, each stands BOARD_DEPTH_M on the core with its edges drawn."""
    face_t, back_t = (wall.t, wall.t - BOARD_DEPTH_M) if inner else (0.0, BOARD_DEPTH_M)
    normal = _neg(wall.out) if inner else wall.out
    ss = [p[0] for p in wall.outline]
    hs = [p[1] for p in wall.outline]
    lo, hi = min(hs), max(hs)
    count = math.ceil((hi - lo) / BOARD_M)
    courses = []
    for _ in range(count):
        v0, v1 = rng.choice(BOARD_ROWS)
        x0, x1 = rng.choice(BOARD_PANELS)
        joints = [min(ss) - rng.uniform(0.0, PLANK_M[0])]
        while joints[-1] < max(ss):
            joints.append(joints[-1] + rng.uniform(*PLANK_M))
        room = [x1 - x0 - (b - a) * BOARD_U_PER_M for a, b in zip(joints, joints[1:])]
        courses.append(((v0, v1), joints, [x0 + rng.uniform(0.0, max(0.0, k)) for k in room]))
    breaks = {lo + k * BOARD_M for k in range(count)} | {lo + (k + 1) * BOARD_M - GROOVE_M for k in range(count)}
    for c in cells(wall.outline, wall.polygons, breaks):
        mid = (c.h0 + c.h1) / 2
        k = min(int((mid - lo) / BOARD_M), count - 1)
        c0, c1 = lo + k * BOARD_M, lo + (k + 1) * BOARD_M - GROOVE_M
        if mid > c1:
            continue
        (v0, v1), joints, starts = courses[k]
        for plank, a0, a1, b0, b1 in _planks(c, joints):
            def uv(s, h, plank=plank):
                return (starts[plank] + (s - joints[plank]) * BOARD_U_PER_M, v0 + (v1 - v0) * (h - c0) / (c1 - c0))

            front = [(a0, c.h0), (b0, c.h0), (b1, c.h1), (a1, c.h1)]
            _flat(mesh, [wall.at(s, h, face_t) for s, h in front], normal, "boards", [uv(s, h) for s, h in front])
            if not relief:
                continue
            strip = (c1 - c0) * min(1.0, BOARD_DEPTH_M / (c1 - c0))
            for (s0, s1, h), n in (((a0, b0, c.h0), (0.0, -1.0, 0.0)), ((a1, b1, c.h1), (0.0, 1.0, 0.0))):
                if abs(h - c0) < EPS or abs(h - c1) < EPS:  # a course's own edge, not a cut through it
                    _flat(mesh, [wall.at(s0, h, face_t), wall.at(s1, h, face_t), wall.at(s1, h, back_t),
                                 wall.at(s0, h, back_t)], n, "boards",
                          [uv(s0, c0), uv(s1, c0), uv(s1, c0 + strip), uv(s0, c0 + strip)])
            for s, side in ((a0, -1.0), (b0, 1.0)):
                if abs(s - (a0 if side < 0 else b0)) < EPS and a0 == a1 and b0 == b1 and _is_joint(s, joints):
                    n = tuple(side * x for x in wall.along)
                    _flat(mesh, [wall.at(s, c.h0, face_t), wall.at(s, c.h1, face_t), wall.at(s, c.h1, back_t),
                                 wall.at(s, c.h0, back_t)], n, "boards",
                          [uv(s, c.h0), uv(s, c.h1), uv(s + side * BOARD_DEPTH_M, c.h1),
                           uv(s + side * BOARD_DEPTH_M, c.h0)])


def _is_joint(s, joints):
    return any(abs(abs(s - j) - JOINT_M / 2) < 1e-6 for j in joints)


def _planks(c, joints):
    """The cell cut at a course's joints: (board, a0, a1, b0, b1). A joint that would cross a sloped end is left out,
    so that board runs on."""
    pieces = []
    a0, a1 = c.a0, c.a1
    for x in joints:
        if x + JOINT_M >= min(c.b0, c.b1):
            break
        if x > max(a0, a1) + JOINT_M:
            board = bisect.bisect_right(joints, max(a0, a1)) - 1
            pieces.append((board, a0, a1, x - JOINT_M / 2, x - JOINT_M / 2))
            a0 = a1 = x + JOINT_M / 2
    pieces.append((bisect.bisect_right(joints, max(a0, a1)) - 1, a0, a1, c.b0, c.b1))
    return pieces


def _core(mesh, wall, q):
    """The wall's mass: its reveals and edges, and at LOD0 the faces its boards stand on."""
    u0, v0, u1, v1 = FRAME_RECTS[0]
    for c in cells(wall.outline, wall.polygons):
        ring = [(c.a0, c.h0), (c.b0, c.h0), (c.b1, c.h1), (c.a1, c.h1)]
        if q == 0:
            for t, n in ((BOARD_DEPTH_M, wall.out), (wall.t - BOARD_DEPTH_M, _neg(wall.out))):
                _flat(mesh, [wall.at(s, h, t) for s, h in ring], n, "boards", [SEAM_UV] * 4)
        for i in range(4):
            (s0, h0), (s1, h1) = ring[i], ring[(i + 1) % 4]
            length = math.hypot(s1 - s0, h1 - h0)
            if length < 1e-6:
                continue
            ds, dh = (s1 - s0) / length, (h1 - h0) / length
            n = _add(_add((0.0, 0.0, 0.0), wall.along, dh), (0.0, 1.0, 0.0), -ds)  # outward of a counter-clockwise ring
            v = v0 + (v1 - v0) * min(1.0, length / 2.0)
            _flat(mesh, [wall.at(s0, h0, 0.0), wall.at(s1, h1, 0.0), wall.at(s1, h1, wall.t), wall.at(s0, h0, wall.t)],
                  n, "frame", [(u0, v0), (u0, v), (u1, v), (u1, v0)])


def _far_wall(mesh, wall):
    """LOD2's wall: its outer face with the openings, the board panel stretched over it."""
    ss, hs = [p[0] for p in wall.outline], [p[1] for p in wall.outline]
    u0, v0, u1, v1 = BOARD_FAR

    def uv(s, h):
        return (u0 + (u1 - u0) * (s - min(ss)) / (max(ss) - min(ss)), v0 + (v1 - v0) * (h - min(hs)) / (max(hs) - min(hs)))

    for c in cells(wall.outline, wall.polygons):
        ring = [(c.a0, c.h0), (c.b0, c.h0), (c.b1, c.h1), (c.a1, c.h1)]
        _flat(mesh, [wall.at(s, h, 0.0) for s, h in ring], wall.out, "boards", [uv(s, h) for s, h in ring])


def _member(mesh, wall, s0, s1, h0, h1, t0, t1, rect):
    """A frame board in the wall's axes, its grain along the longer of its two sides."""
    up = (0.0, 1.0, 0.0)
    depth = _neg(wall.out)
    origin = wall.at(s0, h0, t0)
    if s1 - s0 >= h1 - h0:
        axes, lengths = (wall.along, up, depth), (s1 - s0, h1 - h0, t1 - t0)
    else:
        axes, lengths = (up, wall.along, depth), (h1 - h0, s1 - s0, t1 - t0)
    _cuboid(mesh, origin, axes, lengths, "frame", _along(rect, lengths[0], lengths[1]))


def _trim(mesh, h, wall, hole):
    """The frame round a door (its two jambs and head) or a window (with a sill), proud of the outer face."""
    s0, s1 = hole.s - hole.w / 2, hole.s + hole.w / 2
    b0, b1 = hole.h - hole.ht / 2, hole.h + hole.ht / 2
    w = DOOR_TRIM_M if hole.kind == "door" else WINDOW_TRIM_M
    t0, t1 = -TRIM_PROUD_M, 0.0
    rng = random.Random(f"{h.seed}:trim:{hole.key}")
    pick = lambda: FRAME_RECTS[rng.randrange(len(FRAME_RECTS))]
    _member(mesh, wall, s0 - w, s0, b0, b1, t0, t1, pick())
    _member(mesh, wall, s1, s1 + w, b0, b1, t0, t1, pick())
    _member(mesh, wall, s0 - w, s1 + w, b1, b1 + w, t0, t1, pick())
    if hole.kind == "window":
        _member(mesh, wall, s0 - w, s1 + w, b0 - w, b0, t0, t1, pick())


def _window_bars(mesh, wall, w):
    """The mullion and the transom, between the panes."""
    s0, s1 = w["columns"][0] + w["widths"][0] / 2, w["columns"][1] - w["widths"][1] / 2
    a, b = w["columns"][0] - w["widths"][0] / 2, w["columns"][1] + w["widths"][1] / 2
    _member(mesh, wall, s0, s1, w["lower"][0], w["top"][1], *MEMBER_T_M, FRAME_RECTS[1])
    _member(mesh, wall, a, s0, w["lower"][1], w["top"][0], *MEMBER_T_M, FRAME_RECTS[2])
    _member(mesh, wall, s1, b, w["lower"][1], w["top"][0], *MEMBER_T_M, FRAME_RECTS[2])


def _bars_of(w):
    """(s0, s1, h0, h1) of a window's mullion and the transom's two halves."""
    s0, s1 = w["columns"][0] + w["widths"][0] / 2, w["columns"][1] - w["widths"][1] / 2
    a, b = w["columns"][0] - w["widths"][0] / 2, w["columns"][1] + w["widths"][1] / 2
    return [(s0, s1, w["lower"][0], w["top"][1]), (a, s0, w["lower"][1], w["top"][0]),
            (s1, b, w["lower"][1], w["top"][0])]


def _floor(mesh, h, q):
    """The floor of boards along u, the house's and the porch's in one deck."""
    r = h.r
    v0, v1 = h.porch - r["post_m"] / 2, h.W
    fu = r["floor_under_m"]
    u0, a0, _, _ = FLOOR_RECT
    a1 = FLOOR_RECT[3]
    span = FLOOR_RECT[2] - FLOOR_RECT[0]
    axes = ((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0))  # along u, across v, up
    rng = random.Random(f"{h.seed}:floor")
    if q > 0:
        def uv(x, y):
            return (u0 + span * x / h.L, a0 + (a1 - a0) * y / (v1 - v0))

        _cuboid(mesh, (v0, fu, 0.0), axes, (h.L, v1 - v0, -fu), "floor", uv)
        return
    count = round((v1 - v0) / FLOOR_BOARD_M)
    width = (v1 - v0) / count
    for k in range(count):
        v = v0 + k * width
        z = -rng.uniform(0.0, FLOOR_PIECE_M)
        while z < h.L:
            z1 = min(h.L, z + FLOOR_PIECE_M)
            z0 = max(0.0, z)
            start = u0 + rng.uniform(0.0, span - FLOOR_PIECE_M * FLOOR_UV_PER_M)

            def uv(x, y, start=start, v=v):
                return (start + x * FLOOR_UV_PER_M, a0 + (v - v0 + y) * FLOOR_UV_PER_M)

            _cuboid(mesh, (v + 0.002, fu, z0), axes, (z1 - z0, width - 0.004, -fu), "floor", uv)
            z = z1


def _bricks(mesh, lo, hi):
    """A brick column: its four sides courses of the atlas's patch at true scale, mirrored each repeat so no seam shows,
    and its top."""
    x0, v0, x1, v1 = BRICK_RECT
    top = lo[1]
    k = 0
    while top < hi[1] - EPS:
        y0, y1 = top, min(top + BRICK_M[1], hi[1])
        share = (y1 - y0) / BRICK_M[1]
        va, vb = (v0, v0 + (v1 - v0) * share) if k % 2 == 0 else (v1, v1 - (v1 - v0) * share)
        for p, q_, n in (((lo[0], lo[2]), (hi[0], lo[2]), (0.0, 0.0, -1.0)), ((hi[0], lo[2]), (hi[0], hi[2]), (1.0, 0.0, 0.0)),
                         ((hi[0], hi[2]), (lo[0], hi[2]), (0.0, 0.0, 1.0)), ((lo[0], hi[2]), (lo[0], lo[2]), (-1.0, 0.0, 0.0))):
            run = math.dist(p, q_)
            ub = x0 + (x1 - x0) * min(1.0, run / BRICK_M[0])
            _flat(mesh, [(p[0], y0, p[1]), (q_[0], y0, q_[1]), (q_[0], y1, q_[1]), (p[0], y1, p[1])], n, "brick",
                  [(x0, va), (ub, va), (ub, vb), (x0, vb)])
        top = y1
        k += 1
    _flat(mesh, [(lo[0], hi[1], lo[2]), (hi[0], hi[1], lo[2]), (hi[0], hi[1], hi[2]), (lo[0], hi[1], hi[2])],
          (0.0, 1.0, 0.0), "brick", [(x0, v0), (x1, v0), (x1, v1), (x0, v1)])


def _slope(h, side):
    """A roof slope in the (v, h) plane: (the ridge's point on the underside, downhill unit, outward unit, its length
    from the ridge to the eave). `side` +1 is the back's, -1 the porch's."""
    p = h.pitch
    run = (h.W - h.ridge) + h.r["eave_m"]
    return (h.ridge, h.r["roof_under_m"]), (side * math.cos(p), -math.sin(p)), (side * math.sin(p), math.cos(p)), \
        run / math.cos(p)


def _sheet_box(h, side):
    """(origin, axes, lengths) of a slope's sheet: its upper part, over the rafters, carried past the ridge so the two
    sheets' outer faces meet at the apex."""
    (rv, rh), d, n, length = _slope(h, side)
    sheet = h.r["roof_sheet_m"]
    back = sheet * math.tan(h.pitch)
    o = (rv - d[0] * back + n[0] * RAFTER_D_M, rh - d[1] * back + n[1] * RAFTER_D_M, -h.r["verge_m"])
    axes = ((d[0], d[1], 0.0), (0.0, 0.0, 1.0), (n[0], n[1], 0.0))
    return o, axes, (length + back, h.L + 2 * h.r["verge_m"], sheet - RAFTER_D_M)


def _rafter_us(h):
    """The rafters' centres along u, the first and last over the end walls."""
    first, last = h.T / 2, h.L - h.T / 2
    n = round((last - first) / RAFTER_PITCH_M)
    return [first + (last - first) * k / n for k in range(n + 1)]


def _rafter_box(h, side, u):
    (rv, rh), d, n, length = _slope(h, side)
    back = RAFTER_D_M * math.tan(h.pitch)
    o = (rv - d[0] * back, rh - d[1] * back, u - RAFTER_W_M / 2)
    return o, ((d[0], d[1], 0.0), (0.0, 0.0, 1.0), (n[0], n[1], 0.0)), (length + back, RAFTER_W_M, RAFTER_D_M)


def _roof(mesh, h, q):
    u0, v0, u1, v1 = ROOF_RECT
    for side in (1.0, -1.0):
        o, axes, lengths = _sheet_box(h, side)

        def uv(x, y):
            return (u0 + y * ROOF_UV_PER_M[0], v1 - x * ROOF_UV_PER_M[1])

        _cuboid(mesh, o, axes, lengths, "roof", uv)
        if q < 2:
            for u in _rafter_us(h):
                o_r, axes_r, lengths_r = _rafter_box(h, side, u)
                _cuboid(mesh, o_r, axes_r, lengths_r, "post", _along(POST_RECT, lengths_r[0], lengths_r[1]))
            (rv, rh), d, n, _ = _slope(h, side)
            apex = h.r["roof_sheet_m"] / math.cos(h.pitch)
            o_c = (rv, rh + apex, -h.r["verge_m"])
            _cuboid(mesh, o_c, ((d[0], d[1], 0.0), (0.0, 0.0, 1.0), (n[0], n[1], 0.0)),
                    (CAP_M, h.L + 2 * h.r["verge_m"], CAP_T_M), "frame",
                    _along(FRAME_RECTS[0], CAP_M, h.L + 2 * h.r["verge_m"]))


def _soffit_h(h, v):
    """The soffit's upper face over the porch."""
    r = h.r
    return r["soffit_in_m"] + (r["soffit_in_m"] - r["soffit_out_m"]) * v / r["porch_m"]


def _soffit_box(h):
    """(origin, axes, lengths) of the porch's soffit, under the line the sheet gives."""
    r = h.r
    run = math.hypot(r["porch_m"], r["soffit_in_m"] - r["soffit_out_m"])
    d = (-r["porch_m"] / run, (r["soffit_out_m"] - r["soffit_in_m"]) / run)
    n = (d[1], -d[0])  # up and towards the room
    n = n if n[1] > 0 else (-n[0], -n[1])
    o = (-n[0] * SOFFIT_T_M, r["soffit_in_m"] - n[1] * SOFFIT_T_M, 0.0)
    return o, ((d[0], d[1], 0.0), (0.0, 0.0, 1.0), (n[0], n[1], 0.0)), (run, h.L, SOFFIT_T_M)


def _beam_box(h):
    """(lo, hi) of the porch's beam along the posts' heads, under the soffit."""
    p, r = h.porch, h.r
    top = r["soffit_out_m"] - SOFFIT_T_M
    return (p - r["post_m"] / 2, top - BEAM_D_M, 0.0), (p + r["post_m"] / 2, top, h.L)


def _porch(mesh, h, q):
    r = h.r
    lo, hi = _beam_box(h)
    _cuboid(mesh, lo, ((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0)), (h.L, hi[0] - lo[0], hi[1] - lo[1]), "post",
            _along(POST_RECT, h.L, hi[0] - lo[0]))
    for u in h.posts:
        size = r["post_m"]
        _cuboid(mesh, (h.porch - size / 2, 0.0, u - size / 2), ((0.0, 1.0, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0)),
                (lo[1], size, size), "post", _along(POST_RECT, lo[1], size))
    o, axes, (run, length, t) = _soffit_box(h)
    rng = random.Random(f"{h.seed}:soffit")
    if q > 0:
        _cuboid(mesh, o, axes, (run, length, t), "boards", _along(_rect(0.03, 0.397, 0.515, 0.791), run, length))
        return
    count = round(length / SOFFIT_BOARD_M)
    width = length / count
    for k in range(count):
        v0, v1 = rng.choice(BOARD_ROWS)
        start = rng.uniform(BOARD_PANELS[0][0], BOARD_PANELS[0][1] - run * BOARD_U_PER_M)
        origin = _add(o, axes[1], k * width)

        def uv(x, y, v0=v0, v1=v1, start=start, w=width):
            return (start + x * BOARD_U_PER_M, v0 + (v1 - v0) * y / w)

        _cuboid(mesh, origin, axes, (run, width - JOINT_M, t), "boards", uv)


def _chimney_boxes(h):
    r = h.r
    u1, v1 = h.L - h.T, h.W - h.T
    return [((r["chimney_v0_m"], 0.0, r["chimney_u0_m"]), (v1, r["chimney_step_m"], u1)),
            ((r["chimney_v_top_m"], r["chimney_step_m"], r["chimney_u0_m"]), (v1, r["chimney_top_m"], u1))]


def _pier_box(h, u, v):
    r = h.r
    ground = r["ground_a_m"] + (r["ground_b_m"] - r["ground_a_m"]) * u / h.L
    return (v - r["pier_v_size_m"] / 2, ground - PIER_SINK_M, u - r["pier_u_size_m"] / 2), \
        (v + r["pier_v_size_m"] / 2, r["floor_under_m"], u + r["pier_u_size_m"] / 2)


def _back_wedge(h):
    """The back wall's top in the (v, h) section: from its outer top up to the roof's underside at its inner face."""
    w, t = h.W, h.T
    return [(w - t, h.under(w)), (w, h.under(w)), (w - t, h.under(w - t))]


def _draw_wedge(mesh, h):
    (v0, h0), (v1, _), (_, h2) = _back_wedge(h)
    z0, z1 = h.T, h.L - h.T
    u0, v_, u1, _ = FRAME_RECTS[0]
    run = math.hypot(v1 - v0, h2 - h0)
    n = ((h2 - h0) / run, (v1 - v0) / run, 0.0)
    _flat(mesh, [(v1, h0, z0), (v1, h0, z1), (v0, h2, z1), (v0, h2, z0)], n, "frame", [(u0, v_), (u1, v_), (u1, v_ + 0.1),
                                                                                      (u0, v_ + 0.1)])
    _flat(mesh, [(v0, h0, z0), (v0, h0, z1), (v0, h2, z1), (v0, h2, z0)], (-1.0, 0.0, 0.0), "frame",
          [(u0, v_), (u1, v_), (u1, v_ + 0.05), (u0, v_ + 0.05)])


def _limbs(h):
    """(base, tip, radius) of the birch's upper limbs, all on the side away from the house (assumed)."""
    b = h.birch
    rng = random.Random(f"{h.seed}:birch")
    radius = b["trunk_d_m"] / 2
    out = []
    for k in range(LIMBS):
        y = b["trunk_top_m"] - 0.15 - k * (b["trunk_top_m"] - b["batten_h1_m"] - 0.7) / LIMBS
        az = rng.uniform(-1.2, 1.2) + (k % 2) * 0.4
        d = (math.sin(az), 0.0, math.cos(az))  # towards +u, away from the house
        length = rng.uniform(0.8, 1.4)
        base = (b["trunk_v_m"] + d[0] * radius * 0.8, y, b["trunk_u_m"] + d[2] * radius * 0.8)
        tip = (base[0] + d[0] * length * 0.8, y + length * 0.6, base[2] + d[2] * length * 0.8)
        out.append((base, tip, rng.uniform(0.03, 0.045)))
    return out


def _birch(mesh, h, q):
    b = h.birch
    white = ("bark", palette_uv(COLOURS.index("white"), len(COLOURS)))
    black = ("bark", palette_uv(COLOURS.index("black"), len(COLOURS)))
    foot = h.r["ground_b_m"] - PIER_SINK_M
    radius = b["trunk_d_m"] / 2
    v, u = b["trunk_v_m"], b["trunk_u_m"]
    tube(mesh, [(v, foot, u), (v, b["trunk_top_m"], u)], radius, TRUNK_SIDES[q], white)
    if q == 0:  # birch's dark bands, a sliver proud of the white
        rng = random.Random(f"{h.seed}:bark")
        for _ in range(BARK_BANDS):
            y = rng.uniform(foot + 0.3, b["trunk_top_m"] - 0.2)
            tube(mesh, [(v, y, u), (v, y + rng.uniform(0.03, 0.09), u)], radius + 0.003, TRUNK_SIDES[q], black)
    for base, tip, r in _limbs(h):
        tube(mesh, [base, tip], r, LIMB_SIDES[q], black if q == 2 else white)
    mesh.smooth.add("bark")
    batten = ("batten", palette_uv(COLOURS.index("timber"), len(COLOURS)))
    lo = (b["batten_v0_m"], b["batten_h0_m"], b["batten_u0_m"])
    hi = (b["batten_v1_m"], b["batten_h1_m"], b["batten_u1_m"])
    _cuboid(mesh, lo, ((1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0)), (hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1]),
            "batten", lambda x, y: batten[1])


def _draw(h, q):
    mesh = Mesh()
    for name, wall in h.walls.items():
        if q == 2:
            _far_wall(mesh, wall)
            continue
        _core(mesh, wall, q)
        rng = random.Random(f"{h.seed}:boards:{name}")
        _boards(mesh, wall, rng, q == 0, False)
        _boards(mesh, wall, rng, q == 0, True)
    if q < 2:
        for wall in h.walls.values():
            for hole in wall.holes:
                if hole.kind in ("door", "window"):
                    _trim(mesh, h, wall, hole)
        for w in h.windows:
            _window_bars(mesh, h.walls[w["wall"]], w)
        _draw_wedge(mesh, h)
    _floor(mesh, h, q)
    _roof(mesh, h, q)
    _porch(mesh, h, q)
    for lo, hi in _chimney_boxes(h):
        _bricks(mesh, lo, hi)
    for u, v in h.piers:
        _bricks(mesh, *_pier_box(h, u, v))
    if h.birch:
        _birch(mesh, h, q)
    return mesh


# --- physics ------------------------------------------------------------------------------------------------------


def _box(lo, hi, material, landable=False):
    return {"shape": "box", "center": [(a + b) / 2 for a, b in zip(lo, hi)], "size": [b - a for a, b in zip(lo, hi)],
            "rotation": [0.0, 0.0, 0.0, 1.0], "material": material, "landable": landable}


def _oriented(origin, axes, lengths, material):
    """A collision box of a _cuboid whose first axis lies in the asset's xy plane and whose second is +z."""
    ex, _, _ = axes
    theta = math.atan2(ex[1], ex[0])
    c = _add(_add(_add(origin, axes[0], lengths[0] / 2), axes[1], lengths[1] / 2), axes[2], lengths[2] / 2)
    return {"shape": "box", "center": list(c), "size": [lengths[0], lengths[2], lengths[1]],
            "rotation": [0.0, 0.0, math.sin(theta / 2), math.cos(theta / 2)], "material": material, "landable": False}


def _wall_box(wall, s0, s1, h0, h1, t0, t1, material="timber"):
    a, b = wall.at(s0, h0, t0), wall.at(s1, h1, t1)
    return _box([min(p, q) for p, q in zip(a, b)], [max(p, q) for p, q in zip(a, b)], material)


def _levels(outline, holes):
    """The heights that keep a stair within tolerance: each band between vertices cut finely enough for the steepest
    sloped edge crossing it, the outline's within RAKE_TOLERANCE_M and a grown hole's within its rim."""
    tagged = [(e, 2 * RAKE_TOLERANCE_M) for e in _edges(outline)] + \
        [(e, RIM_COVER * RIM_M) for hole in holes for e in _edges(hole)]
    base = sorted({p[1] for polygon in [outline, *holes] for p in polygon})
    out = set(base)
    for h0, h1 in zip(base, base[1:]):
        mid = (h0 + h1) / 2
        steps = 1
        for e, allowed in tagged:
            if min(e[0][1], e[1][1]) < mid < max(e[0][1], e[1][1]):
                k = abs((e[1][0] - e[0][0]) / (e[1][1] - e[0][1]))
                if k > 1e-9:
                    steps = max(steps, math.ceil((h1 - h0) * (k / math.sqrt(1 + k * k)) / allowed))
        out |= {h0 + (h1 - h0) * i / steps for i in range(steps)}
    return out


def wall_boxes(wall):
    """A wall's collision: boxes of its outline less its openings, a tilted slot grown by RIM_M and its edges held by
    four rotated boxes, so nothing of the stair stands in the slot."""
    grown = [rectangle(o.s, o.h, o.w + 2 * RIM_M, o.ht + 2 * RIM_M, o.tilt) if o.tilt else o.polygon
             for o in wall.holes]
    hole_edges = {e for polygon in grown for e in _edges(polygon)}
    boxes, runs = [], {}
    for c in sorted(cells(wall.outline, grown, _levels(wall.outline, grown)), key=lambda c: c.h0):
        l = min(c.a0, c.a1) if c.left in hole_edges else (c.a0 + c.a1) / 2
        r = max(c.b0, c.b1) if c.right in hole_edges else (c.b0 + c.b1) / 2
        if r - l < 0.002:
            continue
        key = (round(l, 6), round(r, 6))
        run = runs.get(key)
        if run and abs(run[3] - c.h0) < 1e-6:
            run[3] = c.h1
            continue
        if run:
            boxes.append(run)
        runs[key] = [l, r, c.h0, c.h1]
    boxes += runs.values()
    shapes = [_wall_box(wall, l, r, h0, h1, 0.0, wall.t) for l, r, h0, h1 in sorted(boxes, key=lambda b: (b[2], b[0]))]
    for o in wall.holes:
        if o.tilt:
            shapes += _rim(wall, o)
    return shapes


def _rim(wall, o):
    """The four boxes along a tilted slot's edges, in the wall's depth."""
    t = math.radians(o.tilt)
    e, n = (math.cos(t), math.sin(t)), (-math.sin(t), math.cos(t))
    q = [0.0, 0.0, math.sin(t / 2), math.cos(t / 2)]  # the box's x along the slot's width (end walls: asset x = s)
    out = []
    for (a, b), (sx, sy) in ((((o.ht + RIM_M) / 2, 0.0), (o.w + 2 * RIM_M, RIM_M)),
                             ((-(o.ht + RIM_M) / 2, 0.0), (o.w + 2 * RIM_M, RIM_M)),
                             ((0.0, (o.w + RIM_M) / 2), (RIM_M, o.ht)), ((0.0, -(o.w + RIM_M) / 2), (RIM_M, o.ht))):
        cs = o.s + a * n[0] + b * e[0]
        ch = o.h + a * n[1] + b * e[1]
        out.append({"shape": "box", "center": list(wall.at(cs, ch, wall.t / 2)), "size": [sx, sy, wall.t],
                    "rotation": list(q), "material": "timber", "landable": False})
    return out


def _wedge_boxes(h):
    """The back wall's wedge as a stair, each step within RAKE_TOLERANCE_M of the roof's underside."""
    (v0, h0), (v1, _), (_, h2) = _back_wedge(h)
    steps = math.ceil((h2 - h0) / (2 * RAKE_TOLERANCE_M))
    return [_box((v0 + (v1 - v0) * k / steps, h0, h.T),
                 (v0 + (v1 - v0) * (k + 1) / steps, h2 - (h2 - h0) * (k + 0.5) / steps, h.L - h.T), "timber")
            for k in range(steps)]


def collision(h):
    r = h.r
    shapes = []
    for wall in h.walls.values():
        shapes += wall_boxes(wall)
    shapes += _wedge_boxes(h)
    for w in h.windows:
        wall = h.walls[w["wall"]]
        shapes += [_wall_box(wall, s0, s1, h0, h1, *MEMBER_T_M) for s0, s1, h0, h1 in _bars_of(w)]
    shapes.append(_box((h.porch - r["post_m"] / 2, r["floor_under_m"], 0.0), (h.W, 0.0, h.L), "timber", landable=True))
    lo, hi = _beam_box(h)
    shapes.append(_box(lo, hi, "timber"))
    shapes += [_box((h.porch - r["post_m"] / 2, 0.0, u - r["post_m"] / 2),
                    (h.porch + r["post_m"] / 2, lo[1], u + r["post_m"] / 2), "timber") for u in h.posts]
    o, axes, lengths = _soffit_box(h)
    shapes.append(_oriented(o, axes, lengths, "timber"))
    for side in (1.0, -1.0):
        o, axes, lengths = _sheet_box(h, side)
        shapes.append(_oriented(o, axes, lengths, "asbestos_cement"))
        for u in _rafter_us(h):
            o, axes, lengths = _rafter_box(h, side, u)
            shapes.append(_oriented(o, axes, lengths, "timber"))
    shapes += [_box(a, b, "masonry") for a, b in _chimney_boxes(h)]
    shapes += [_box(*_pier_box(h, u, v), "masonry") for u, v in h.piers]
    if h.birch:
        shapes += _birch_shapes(h)
    return shapes


def _birch_shapes(h):
    b = h.birch
    foot = h.r["ground_b_m"] - PIER_SINK_M
    shapes = [{"shape": "cylinder", "center": [b["trunk_v_m"], (foot + b["trunk_top_m"]) / 2, b["trunk_u_m"]],
               "radius": b["trunk_d_m"] / 2, "height": b["trunk_top_m"] - foot, "rotation": [0.0, 0.0, 0.0, 1.0],
               "material": "bark", "landable": False}]
    shapes += [meta.capsule(base, tip, r, "bark") for base, tip, r in _limbs(h)]
    shapes.append(_box((b["batten_v0_m"], b["batten_h0_m"], b["batten_u0_m"]),
                       (b["batten_v1_m"], b["batten_h1_m"], b["batten_u1_m"]), "timber"))
    return shapes


def _gap(gap_id, wall, polygon, narrowest):
    return {"id": gap_id, "polygon": [list(wall.at(s, a, wall.t / 2)) for s, a in polygon],
            "normal": list(wall.out), "narrowest_m": narrowest}


def pane_rect(w, column, row):
    """(s0, s1, h0, h1) of a window's pane; row 1 is the top."""
    s, width = w["columns"][column], w["widths"][column]
    h0, h1 = w["top"] if row else w["lower"]
    return s - width / 2, s + width / 2, h0, h1


def gaps(h):
    """Every opening's clear space: the doors, slots, crawl bays and panes, the marked ones named by their marker; the
    open space over the door wall, the porch's bays between its posts and its two open ends."""
    out = []
    marked = {}
    for o in h.openings:
        if o["kind"] == "pane":
            w, c = _pane_of(h.windows, o)
            marked[(w["wall"], c)] = o["marker"]
        elif o["kind"] == "crawl":
            polygon = rectangle(o["s"], o["h"], o["width"], o["height"])
            out.append(_gap(f"crawl_{o['marker']}", h.walls[o["wall"]], polygon, min(o["width"], o["height"])))
    for wall in h.walls.values():
        for hole in wall.holes:
            if hole.kind != "window":
                out.append(_gap(hole.key, wall, hole.polygon, min(hole.w, hole.ht)))
    for w in h.windows:
        wall = h.walls[w["wall"]]
        for c in (0, 1):
            for row in (0, 1):
                s0, s1, a, b = pane_rect(w, c, row)
                name = f"pane_{marked[(w['wall'], c)]}" if row and (w["wall"], c) in marked else \
                    f"pane_{w['wall']}_{'lt'[row]}{c}"
                out.append(_gap(name, wall, [(s0, a), (s1, a), (s1, b), (s0, b)], min(s1 - s0, b - a)))
    r = h.r
    door = h.walls["door"]
    top = r["door_wall_top_m"]
    roof = h.under(h.T / 2)
    out.append(_gap("over_door_wall", door, [(h.T, top), (h.L - h.T, top), (h.L - h.T, roof), (h.T, roof)], roof - top))
    head = _beam_box(h)[0][1]
    x = h.porch
    half = r["post_m"] / 2
    for k, (u0, u1) in enumerate(zip(h.posts, h.posts[1:])):
        a, b = u0 + half, u1 - half
        out.append({"id": f"porch_bay_{k}", "polygon": [[x, 0.0, a], [x, 0.0, b], [x, head, b], [x, head, a]],
                    "normal": [-1.0, 0.0, 0.0], "narrowest_m": min(b - a, head)})
    for name in ("A", "B"):
        wall = h.walls[name]
        s0 = x + half
        roofline = _soffit_h(h, s0) - SOFFIT_T_M / math.cos(math.atan2(r["soffit_in_m"] - r["soffit_out_m"], r["porch_m"]))
        inner = r["soffit_in_m"] - SOFFIT_T_M / math.cos(math.atan2(r["soffit_in_m"] - r["soffit_out_m"], r["porch_m"]))
        polygon = [(s0, 0.0), (0.0, 0.0), (0.0, inner), (s0, roofline)]
        out.append({"id": f"porch_end_{name}", "polygon": [list(wall.at(s, a, wall.t / 2)) for s, a in polygon],
                    "normal": list(wall.out), "narrowest_m": _inscribed(polygon)})
    return out


def _inscribed(polygon):
    """The diameter of the largest circle in a convex polygon, by bisection on the inset distance."""
    n = len(polygon)
    area = sum(polygon[i][0] * polygon[(i + 1) % n][1] - polygon[(i + 1) % n][0] * polygon[i][1] for i in range(n))
    sign = 1.0 if area > 0 else -1.0
    planes = []
    for i in range(n):
        (x0, y0), (x1, y1) = polygon[i], polygon[(i + 1) % n]
        length = math.hypot(x1 - x0, y1 - y0)
        planes.append((-(y1 - y0) * sign / length, (x1 - x0) * sign / length, x0, y0))  # inward unit normal

    def feasible(d):
        """Whether the polygon inset by d still has a point: clip a big square by each inset plane."""
        poly = [(-1e3, -1e3), (1e3, -1e3), (1e3, 1e3), (-1e3, 1e3)]
        for nx, ny, px, py in planes:
            side = [nx * (x - px) + ny * (y - py) - d for x, y in poly]
            out = []
            for i, p in enumerate(poly):
                j = (i + 1) % len(poly)
                if side[i] >= 0:
                    out.append(p)
                if (side[i] >= 0) != (side[j] >= 0):
                    k = side[i] / (side[i] - side[j])
                    out.append((p[0] + (poly[j][0] - p[0]) * k, p[1] + (poly[j][1] - p[1]) * k))
            poly = out
            if len(poly) < 3:
                return False
        return True

    lo, hi = 0.0, 10.0
    for _ in range(40):
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if feasible(mid) else (lo, mid)
    return 2 * lo


def _edge(points, material="timber"):
    return {"kind": "edge", "points": [list(p) for p in points], "radius_m": SNAG_R_M, "material": material}


def snags(h):
    """The arrises a fiber bends over: round each opening's outer face, along the window bars and the eaves and rakes
    of the roof; each limb of the birch is a free-ended hook."""
    out = []
    for wall in h.walls.values():
        for hole in wall.holes:
            ring = [wall.at(s, a, 0.0) for s, a in hole.polygon]
            if hole.kind == "door":  # no sill: the jambs and the head
                ring = ring[1:] + ring[:1]
                out.append(_edge(ring))
            else:
                out.append(_edge(ring + ring[:1]))
    for w in h.windows:
        wall = h.walls[w["wall"]]
        s = (w["columns"][0] + w["widths"][0] / 2 + w["columns"][1] - w["widths"][1] / 2) / 2
        a = (w["lower"][1] + w["top"][0]) / 2
        out.append(_edge([wall.at(s, w["lower"][0], 0.09), wall.at(s, w["top"][1], 0.09)]))
        out.append(_edge([wall.at(w["columns"][0] - w["widths"][0] / 2, a, 0.09),
                          wall.at(w["columns"][1] + w["widths"][1] / 2, a, 0.09)]))
    z0, z1 = -h.r["verge_m"], h.L + h.r["verge_m"]
    for side in (1.0, -1.0):
        (rv, rh), d, n, length = _slope(h, side)
        sheet = h.r["roof_sheet_m"]
        eave = (rv + d[0] * length + n[0] * sheet, rh + d[1] * length + n[1] * sheet)
        apex = (rv, rh + sheet / math.cos(h.pitch))
        out.append(_edge([(eave[0], eave[1], z0), (eave[0], eave[1], z1)], "asbestos_cement"))
        for z in (z0, z1):
            out.append(_edge([(apex[0], apex[1], z), (eave[0], eave[1], z)], "asbestos_cement"))
    if h.birch:
        out += [{"kind": "hook", "points": [list(base), list(tip)], "radius_m": r, "material": "bark"}
                for base, tip, r in _limbs(h)]
    return out


def wind(h, shapes, gap_records):
    """Solid volumes (porosity 0) of the walls' coarse parts, the roof, the deck, chimney and piers, and the room with
    the loft as one sheltered cavity, its draught through every opening of the house's own walls."""
    r = h.r
    out = []
    for wall in h.walls.values():
        for c in cells(wall.outline, wall.polygons):
            box = _wall_box(wall, (c.a0 + c.a1) / 2, (c.b0 + c.b1) / 2, c.h0, c.h1, 0.0, wall.t)
            out.append(_solid(box))
    keep = ("masonry", "asbestos_cement")
    out += [_solid(s) for s in shapes if s["shape"] == "box" and (s["material"] in keep or s["landable"])]
    if h.birch:
        out.append(_solid(_birch_shapes(h)[0]))
    room = _box((h.T, 0.0, h.T), (h.W - h.T, r["roof_under_m"], h.L - h.T), "timber")
    cavity = _solid(room) | {"porosity": 1.0, "cavity": True,
                             "openings": [g["id"] for g in gap_records
                                          if g["id"].split("_")[0] in ("door", "pane", "slot", "over")]}
    return out + [cavity]


def _solid(shape):
    return {k: v for k, v in shape.items() if k not in ("material", "landable")} | {"porosity": 0.0}


def build(params):
    h = plan(params)
    lods = [_draw(h, q) for q in range(3)]
    shapes = collision(h)
    gap_records = gaps(h)
    snag = snags(h)
    lod = params["lod"]
    used = sorted({slot for mesh in lods for slot in mesh.slots()})
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"]],
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": slot, "physics": PHYSICS[slot]} for slot in used],
        "collision": shapes,
        "gaps": gap_records,
        "wind_volumes": wind(h, shapes, gap_records),
        "snag": snag,
        "report": [f"{len(shapes)} collision shapes, {len(gap_records)} gaps, "
                   f"{sum(len(s['points']) - 1 for s in snag)} snag segments, "
                   f"{' / '.join(str(m.triangles()) for m in lods)} tris"],
    }
