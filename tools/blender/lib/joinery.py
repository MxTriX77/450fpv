"""The house's doors and windows (p1-h01-adobe-house, design decision 5; object sheet
docs/object-sheets/houses-and-yard.md §Shared "Joinery" and §H01): every frame member, board, ledge, casement and
glazing bar as a member with real thickness and its own collision box, glass as shards, and each opening's clear
space as a gap polygon. Plain Python on lib.prism; an opening is lib.walls' Opening, its side lib.walls' Side.

Depths `d` run in from a wall's outer face. The door (AST-04's 0.8 x 1.9 m clear): a frame of 80 mm members 100 mm
deep flush with the inner face, standing on grade, its threshold's top at the floor (yard level, E 7), and a plank
leaf (five boards with 3 mm joints, two ledges and a brace on its inner face, strap hinges on the ledges) hung inward
on the left jamb as seen from outside. `door_leaf_state`: closed (in the frame), ajar (AJAR_DEG in), hanging
(HANGING_DEG in and sagged on its lower hinge until its free corner rests on the floor), gone; `inside` (the leaf
blown down) is task 1.4's.

The window (AST-04's 0.8 x 1.0 m rough opening, H01-H02): a box frame of 70 mm members 0.20 m deep holding two pairs
of casements (the double frame, summer outside and winter inside), each casement 45 mm square stiles and rails with
two 25 mm bars, so 2 x 3 panes. `window_state`: glazed (the panes whole), shards (the glass broken out but for
shards in the panes' corners: the I state, glass never a whole plane, knowledge.md §14.4), frame_empty (casements
gone), sash_hanging (one outer casement left, hung open inward on its stile into the reveal), frame_gone (the rough
opening only; its crumbled edges are task 1.4's).

Gaps (design decision 9): an opening's clear space through the wall, projected along its normal: the frame's clear
opening less everything standing in the wall's depth (a hanging casement, shards); a door's leaf swings into the room
beyond the wall and is collision of its own. narrowest_m is the polygon's largest inscribed circle (lib.audit).
"""
import math

from lib import audit, prism

TIMBER, GLASS, STEEL = "timber", "glass", "steel"
FRAME_SLOT, DOOR_SLOT, GLASS_SLOT, IRON_SLOT = "timber", "door", "glass", "iron"
DOOR_FRAME_M = (0.08, 0.08)      # jamb face width, head height (the sheet: a 0.10 x 0.08 m frame)
DOOR_DEPTH_M = 0.10              # the frame's depth in the wall, flush with the inner face
GRADE_M = 0.0                    # the frame stands on grade: no mesh below it, so the review stage does not lift it
BOARDS = 5                       # the sheet: 3-5 boards
BOARD_M = (0.032, 0.003)         # thickness (the sheet: 25-40 mm) and the joint between boards (assumed)
LEDGE_M = (0.10, 0.025)          # ledge and brace width and thickness (assumed)
LEDGE_FROM_END_M = 0.25          # each ledge's centre from the leaf's top or bottom (assumed)
STRAP_M = (0.03, 0.004, 0.45)    # strap hinge width, thickness, length along its ledge (assumed)
AJAR_DEG, HANGING_DEG = 30.0, 70.0  # design decision 5: hanging 70 degrees from the jamb (assumed); ajar (assumed)
LEAF_CLEARANCE_M = 0.01          # under a hung leaf (assumed)
EDGE_SNAG_R_M = 0.002            # a sawn arris (the sheet's K03 edge radius)
GLASS_EDGE_R_M = 0.001           # a broken glass edge (assumed)
WINDOW_FRAME_M = 0.07            # the sheet: 60-70 mm; the 0.8 x 1.0 m opening's clear 0.66 x 0.86 m
WINDOW_DEPTH_M = (0.08, 0.28)    # the box frame's depth in the wall, from its outer face (assumed)
CASEMENT_M = 0.045               # stile and rail, square (assumed)
BAR_M = 0.025                    # glazing bar face width (assumed)
GLASS_M = 0.003                  # pane thickness (assumed)
SHARD_LEG_M = (0.02, 0.04)       # a corner shard's legs; under 2 x 2 cm from its box everywhere (derived)
SHARD_CHANCE = 0.5               # a pane corner keeps a shard (assumed)
SASH_OPEN_DEG = 70.0             # sash_hanging: the casement's swing into the reveal (assumed)
DOOR_STATES = ("closed", "ajar", "hanging", "inside", "gone")
DOOR_BUILT = ("closed", "ajar", "hanging", "gone")
WINDOW_STATES = ("glazed", "shards", "frame_empty", "sash_hanging", "frame_gone")


def wall_frame(side, grain):
    """The frame of a member in a wall: grain "y" runs up (local (y, a, -d)), grain "a" along the wall (local
    (a, y, d))."""
    up = (0.0, 1.0, 0.0)
    origin = side.point(0.0, 0.0, 0.0)
    if grain == "y":
        return prism.Frame(origin, up, side.across(), side.outward())
    inward = tuple(-c for c in side.outward())
    return prism.Frame(origin, side.across(), up, inward)


def wall_member(mesh, side, a, y, d, grain, slot, material, offset=(0.0, 0.0)):
    """A member of the wall-space box a x y x d (each a (lo, hi) pair) with its grain; (Member, box)."""
    f = wall_frame(side, grain)
    if grain == "y":
        lo, hi = (y[0], a[0], -d[1]), (y[1], a[1], -d[0])
    else:
        lo, hi = (a[0], y[0], d[0]), (a[1], y[1], d[1])
    return prism.member(mesh, f, lo, hi, slot, material, offset=offset)


class Result:
    """What the joinery adds to the house: collision boxes, gaps, snags, per LOD meshes (filled by the callers)."""

    def __init__(self):
        self.collision, self.gaps, self.snag = [], [], []


# --- the door -------------------------------------------------------------------------------------------------------

def door(plan, o, lods, rng):
    """The door frame and leaf into lods[0] and lods[1] (lods[2] gets the leaf as one box), and its Result."""
    side = plan.sides[o.side]
    res = Result()
    jamb, head = DOOR_FRAME_M
    w, h = o.params["width"], o.params["height"]
    d = (plan.T - DOOR_DEPTH_M, plan.T)
    c0, c1 = o.a0 + jamb, o.a1 - jamb
    y_top = plan.F + h
    for mesh in lods[:2]:
        boxes = [wall_member(mesh, side, (o.a0, c0), (GRADE_M, y_top + head), d, "y", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c1, o.a1), (GRADE_M, y_top + head), d, "y", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c0, c1), (y_top, y_top + head), d, "a", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c0, c1), (GRADE_M, plan.F), d, "a", FRAME_SLOT, TIMBER)]
    res.collision += [b for _, b in boxes]
    state = o.state
    if state == "gone":
        res.gaps.append(_gap(side, "door", [(c0, plan.F), (c1, plan.F), (c1, y_top), (c0, y_top)], plan.T - DOOR_DEPTH_M / 2))
        return res
    phi = {"closed": 0.0, "ajar": AJAR_DEG, "hanging": HANGING_DEG}[state]
    hinge = side.point(c0, plan.F + LEAF_CLEARANCE_M, plan.T)
    across, up = side.across(), (0.0, 1.0, 0.0)
    inward = tuple(-c for c in side.outward())
    cp, sp = math.cos(math.radians(phi)), math.sin(math.radians(phi))
    ex = tuple(cp * across[k] + sp * inward[k] for k in range(3))
    ez = tuple(-sp * across[k] + cp * inward[k] for k in range(3))
    leaf_h = h - LEAF_CLEARANCE_M
    frame = prism.Frame(hinge, ex, up, ez)
    if state == "hanging":  # sagged on its lower hinge until the free corner rests on the floor
        y_hinge = LEDGE_FROM_END_M
        sag = math.asin(min(1.0, LEAF_CLEARANCE_M / w))
        frame = _turned(frame, (0.0, y_hinge, 0.0), -sag)
    res.collision += _leaf(lods[:2], frame, w, leaf_h)
    _leaf_lod2(lods[2], frame, w, leaf_h)
    if state != "closed":
        res.gaps.append(_gap(side, "door", [(c0, plan.F), (c1, plan.F), (c1, y_top), (c0, y_top)], plan.T - DOOR_DEPTH_M / 2))
        z = LEDGE_M[1]
        res.snag.append({"kind": "edge", "radius_m": EDGE_SNAG_R_M,
                         "points": [list(frame.point(p)) for p in ((w, 0.0, z), (w, leaf_h, z), (0.0, leaf_h, z))],
                         "material": TIMBER})
    return res


def _turned(frame, pivot, angle):
    """The frame turned by `angle` (radians) in its own xy plane about the local point `pivot`."""
    c, s = math.cos(angle), math.sin(angle)
    ex = tuple(c * frame.ex[k] + s * frame.ey[k] for k in range(3))
    ey = tuple(-s * frame.ex[k] + c * frame.ey[k] for k in range(3))
    p = frame.point(pivot)
    moved = tuple(p[k] - (pivot[0] * ex[k] + pivot[1] * ey[k] + pivot[2] * frame.ez[k]) for k in range(3))
    return prism.Frame(moved, ex, ey, frame.ez)


def _leaf(meshes, frame, w, h):
    """The plank leaf in its frame (x across from the hinge, y up, z into the room; the boards' outer face on
    z = -BOARD_M[0], the ledges and brace on z = 0): five boards, two ledges, a brace from the lower ledge at the
    hinge to the upper at the latch, a strap hinge on each ledge. Returns the leaf's four boxes."""
    t, joint = BOARD_M
    bw = (w - (BOARDS - 1) * joint) / BOARDS
    lw, lt = LEDGE_M
    ledges = [(LEDGE_FROM_END_M - lw / 2, LEDGE_FROM_END_M + lw / 2), (h - LEDGE_FROM_END_M - lw / 2, h - LEDGE_FROM_END_M + lw / 2)]
    boxes = []
    for n, mesh in enumerate(meshes):
        for k in range(BOARDS):
            x0 = k * (bw + joint)
            board = prism.Frame(frame.point((x0, 0.0, -t)), frame.ey, frame.ex, prism.cross(frame.ey, frame.ex))
            prism.member(mesh, board, (0.0, 0.0, -t), (h, bw, 0.0), DOOR_SLOT, TIMBER, offset=(0.13 * k, 0.37 * k))
        for y0, y1 in ledges:
            m, b = prism.member(mesh, frame, (0.0, y0, 0.0), (w, y1, lt), DOOR_SLOT, TIMBER)
            if n == 0:
                boxes.append(b)
            strap = prism.Frame(frame.point((0.0, (y0 + y1) / 2 - STRAP_M[0] / 2, lt)), frame.ex, frame.ey, frame.ez)
            prism.member(mesh, strap, (-0.02, 0.0, 0.0), (STRAP_M[2], STRAP_M[0], STRAP_M[1]), IRON_SLOT, STEEL)
        p0 = (0.06, ledges[0][1], 0.0)
        p1 = (w - 0.06, ledges[1][0], 0.0)
        brace = prism.frame_along(frame.point(p0), frame.point(p1), frame.ez)
        length = math.dist(p0, p1)
        # the brace in the leaf's plane: local x along it, y square to the leaf (frame.ez), z across in the plane
        brace = prism.Frame(brace.origin, brace.ex, frame.ez, prism.cross(brace.ex, frame.ez))
        planes = [prism.plane(frame.point((0.0, ledges[0][1], 0.0)), tuple(-c for c in frame.ey)),
                  prism.plane(frame.point((0.0, ledges[1][0], 0.0)), frame.ey)]
        m, b = prism.member(mesh, brace, (-0.2, 0.0, -lw / 2), (length + 0.2, lt, lw / 2), DOOR_SLOT, TIMBER, planes)
        if n == 0:
            boxes.append(b)
    board_box = prism.Member(frame, (0.0, 0.0, -t), (w, h, 0.0)).box(TIMBER)
    return [board_box] + boxes


def _leaf_lod2(mesh, frame, w, h):
    prism.member(mesh, frame, (0.0, 0.0, -BOARD_M[0]), (w, h, LEDGE_M[1]), DOOR_SLOT, TIMBER)


# --- windows --------------------------------------------------------------------------------------------------------

def window(plan, o, lods, rng):
    """One window by its state into lods[0] and lods[1], and its Result."""
    side = plan.sides[o.side]
    res = Result()
    state = o.state
    name = o.name
    if state == "frame_gone":
        mid = plan.T / 2
        res.gaps.append(_gap(side, name, [(o.a0, o.y0), (o.a1, o.y0), (o.a1, o.y1), (o.a0, o.y1)], mid))
        return res
    f, (d0, d1) = WINDOW_FRAME_M, WINDOW_DEPTH_M
    c0, c1, r0, r1 = o.a0 + f, o.a1 - f, o.y0 + f, o.y1 - f
    for mesh in lods[:2]:
        boxes = [wall_member(mesh, side, (o.a0, c0), (o.y0, o.y1), (d0, d1), "y", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c1, o.a1), (o.y0, o.y1), (d0, d1), "y", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c0, c1), (r1, o.y1), (d0, d1), "a", FRAME_SLOT, TIMBER),
                 wall_member(mesh, side, (c0, c1), (o.y0, r0), (d0, d1), "a", FRAME_SLOT, TIMBER)]
    res.collision += [b for _, b in boxes]
    clear = [(c0, r0), (c1, r0), (c1, r1), (c0, r1)]
    if state == "frame_empty":
        res.gaps.append(_gap(side, name, clear, (d0 + d1) / 2))
        res.snag.append({"kind": "edge", "radius_m": EDGE_SNAG_R_M,
                         "points": [list(side.point(a, y, d0)) for a, y in clear + clear[:1]], "material": TIMBER})
        return res
    cm = (c0 + c1) / 2
    layers = [(d0, d0 + CASEMENT_M), (d1 - CASEMENT_M, d1)]
    if state == "sash_hanging":
        w_c = cm - c0
        phi = math.radians(SASH_OPEN_DEG)
        hinge = side.point(c0, r0, d0 + CASEMENT_M)
        across = side.across()
        inward = tuple(-c for c in side.outward())
        ex = tuple(math.cos(phi) * across[k] + math.sin(phi) * inward[k] for k in range(3))
        ez = tuple(-math.sin(phi) * across[k] + math.cos(phi) * inward[k] for k in range(3))
        frame = prism.Frame(hinge, ex, (0.0, 1.0, 0.0), ez)
        res.collision += _casement(lods[:2], frame, w_c, r1 - r0, -CASEMENT_M, 0.0)
        shadow = c0 + w_c * math.cos(phi) + CASEMENT_M * math.sin(phi)  # its outer face's far arris
        res.gaps.append(_gap(side, name, [(shadow, r0), (c1, r0), (c1, r1), (shadow, r1)], (d0 + d1) / 2))
        res.snag.append({"kind": "edge", "radius_m": EDGE_SNAG_R_M,
                         "points": [list(frame.point(p)) for p in ((w_c, 0.0, 0.0), (w_c, r1 - r0, 0.0))],
                         "material": TIMBER})
        return res
    panes = []  # per layer, per pane: its clear rectangle (a0, y0, a1, y1)
    for d_lo, d_hi in layers:
        frame = wall_frame(side, "a")
        layer_panes = []
        for s0, s1 in ((c0, cm), (cm, c1)):
            local = prism.Frame(side.point(s0, r0, d_lo), side.across(), (0.0, 1.0, 0.0),
                                tuple(-c for c in side.outward()))
            res.collision += _casement(lods[:2], local, s1 - s0, r1 - r0, 0.0, d_hi - d_lo)
            layer_panes += [(s0 + a0, r0 + y0, s0 + a1, r0 + y1) for a0, y0, a1, y1 in _pane_rects(s1 - s0, r1 - r0)]
        panes.append(layer_panes)
    for k, (outer, inner) in enumerate(zip(*panes)):
        if state == "glazed":
            for (d_lo, d_hi), rect in zip(layers, (outer, inner)):
                dm = (d_lo + d_hi) / 2
                for mesh in lods[:2]:
                    _, b = wall_member(mesh, side, (rect[0], rect[2]), (rect[1], rect[3]),
                                       (dm - GLASS_M / 2, dm + GLASS_M / 2), "a", GLASS_SLOT, GLASS)
                res.collision.append(b)
            continue
        cuts = []  # half-planes past each shard's hypotenuse, in (a, y)
        for (d_lo, d_hi), rect in zip(layers, (outer, inner)):
            dm = (d_lo + d_hi) / 2
            for corner in ((0, 0), (1, 0), (1, 1), (0, 1)):
                if rng.random() >= SHARD_CHANCE:
                    continue
                legs = (rng.uniform(*SHARD_LEG_M), rng.uniform(*SHARD_LEG_M))
                tri, box_lo_hi = _shard_triangle(rect, corner, legs)
                for mesh in lods[:2]:
                    _shard(mesh, side, tri, dm)
                (a_lo, y_lo), (a_hi, y_hi) = box_lo_hi
                res.collision.append(wall_member(None, side, (a_lo, a_hi), (y_lo, y_hi),
                                                 (dm - GLASS_M / 2, dm + GLASS_M / 2), "a", GLASS_SLOT, GLASS)[1])
                res.snag.append({"kind": "edge", "radius_m": GLASS_EDGE_R_M,
                                 "points": [list(side.point(a, y, dm)) for a, y in tri[1:]], "material": GLASS})
                cuts.append(tri)
        polygon = [(rect_a, rect_y) for rect_a, rect_y in ((outer[0], outer[1]), (outer[2], outer[1]),
                                                           (outer[2], outer[3]), (outer[0], outer[3]))]
        for tri in cuts:
            polygon = _clip_past(polygon, tri)
        res.gaps.append(_gap(side, f"{name}_pane{k + 1}", polygon, (layers[0][0] + layers[0][1]) / 2))
    return res


def _pane_rects(w, h):
    """A casement's three panes, (a0, y0, a1, y1) in its own frame from its lower outer corner."""
    m, b = CASEMENT_M, BAR_M
    ph = (h - 2 * m - 2 * b) / 3
    return [(m, m + k * (ph + b), w - m, m + k * (ph + b) + ph) for k in range(3)]


def _casement(meshes, frame, w, h, z0, z1):
    """A casement in `frame` (x across from its lower outer corner, y up, z through the wall from z0 to z1): two
    stiles, two rails and two bars. Returns its six boxes."""
    m, b = CASEMENT_M, BAR_M
    ph = (h - 2 * m - 2 * b) / 3
    parts = [((0.0, 0.0), (m, h), "y"), ((w - m, 0.0), (w, h), "y"), ((m, 0.0), (w - m, m), "x"),
             ((m, h - m), (w - m, h), "x")]
    parts += [((m, m + ph + k * (ph + b)), (w - m, m + ph + k * (ph + b) + b), "x") for k in range(2)]
    boxes = []
    for n, mesh in enumerate(meshes):
        for (x0, y0), (x1, y1), grain in parts:
            if grain == "y":
                f = prism.Frame(frame.point((0.0, 0.0, 0.0)), frame.ey, frame.ex, prism.cross(frame.ey, frame.ex))
                _, box = prism.member(mesh, f, (y0, x0, -z1), (y1, x1, -z0), FRAME_SLOT, TIMBER)
            else:
                _, box = prism.member(mesh, frame, (x0, y0, z0), (x1, y1, z1), FRAME_SLOT, TIMBER)
            if n == 0:
                boxes.append(box)
    return boxes


def _shard_triangle(rect, corner, legs):
    """A shard in a pane's corner: the triangle (corner, the point along a, the point along y) in (a, y), and its
    box, the corner's quarter of the legs' rectangle, so every point of the shard lies within half a leg of it."""
    a0, y0, a1, y1 = rect
    ca = a0 if corner[0] == 0 else a1
    cy = y0 if corner[1] == 0 else y1
    sa = 1 if corner[0] == 0 else -1
    sy = 1 if corner[1] == 0 else -1
    tri = [(ca, cy), (ca + sa * legs[0], cy), (ca, cy + sy * legs[1])]
    box = ((min(ca, ca + sa * legs[0] / 2), min(cy, cy + sy * legs[1] / 2)),
           (max(ca, ca + sa * legs[0] / 2), max(cy, cy + sy * legs[1] / 2)))
    return tri, box


def _shard(mesh, side, tri, dm):
    """A glass shard: the triangle as a slab GLASS_M thick about depth dm."""
    pts = {}
    for k, (a, y) in enumerate(tri):
        for s in (-1, 1):
            pts[k, s] = mesh.vertex(side.point(a, y, dm + s * GLASS_M / 2))
    out = side.outward()
    mesh.face([pts[k, -1] for k in range(3)], out, GLASS_SLOT, [(0.5, 0.5)] * 3)
    mesh.face([pts[k, 1] for k in range(3)], tuple(-c for c in out), GLASS_SLOT, [(0.5, 0.5)] * 3)
    centre = [sum(t[i] for t in tri) / 3 for i in range(2)]
    for k in range(3):
        j = (k + 1) % 3
        (a0, y0), (a1, y1) = tri[k], tri[j]
        n2 = (y1 - y0, -(a1 - a0))
        if n2[0] * ((a0 + a1) / 2 - centre[0]) + n2[1] * ((y0 + y1) / 2 - centre[1]) < 0:
            n2 = (-n2[0], -n2[1])
        across = side.across()
        n = (n2[0] * across[0], n2[1], n2[0] * across[2])
        mesh.face([pts[k, -1], pts[j, -1], pts[j, 1], pts[k, 1]], n, GLASS_SLOT, [(0.5, 0.5)] * 4)


def _clip_past(polygon, tri):
    """A convex polygon in (a, y) clipped to the side of the shard's hypotenuse away from its corner."""
    corner, p, q = tri
    n = (q[1] - p[1], -(q[0] - p[0]))
    if n[0] * (corner[0] - p[0]) + n[1] * (corner[1] - p[1]) > 0:
        n = (-n[0], -n[1])
    out = []
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        da = n[0] * (a[0] - p[0]) + n[1] * (a[1] - p[1])
        db = n[0] * (b[0] - p[0]) + n[1] * (b[1] - p[1])
        if da >= 0:
            out.append(a)
        if (da > 0 > db) or (da < 0 < db):
            t = da / (da - db)
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])))
    return out


# --- gaps -----------------------------------------------------------------------------------------------------------

def _gap(side, gap_id, polygon, depth):
    """A gap record: the polygon (a, y) on its side at `depth`, the side's outward normal, its inscribed circle."""
    points = [list(side.point(a, y, depth)) for a, y in polygon]
    normal = list(side.outward())
    return {"id": gap_id, "polygon": points, "normal": normal,
            "narrowest_m": round(audit.inscribed_diameter(points, normal), 6)}


def doorway_gap(plan, d):
    """An inner doorway's clear opening in its partition's centre plane."""
    x = d.params["x"]
    points = [[x, y, z] for z, y in ((d.a0, d.y0), (d.a1, d.y0), (d.a1, d.y1), (d.a0, d.y1))]
    normal = [1.0, 0.0, 0.0]
    return {"id": d.name, "polygon": points, "normal": normal,
            "narrowest_m": round(audit.inscribed_diameter(points, normal), 6)}


def extent(gap):
    """(width, height) of a gap's polygon in its plane, horizontal first."""
    n = gap["normal"]
    h = prism.unit((n[2], 0.0, -n[0])) if abs(n[1]) < 0.9 else (1.0, 0.0, 0.0)  # a roof slope's too
    v = prism.cross(n, h)
    us = [prism.dot(p, h) for p in gap["polygon"]]
    vs = [prism.dot(p, v) for p in gap["polygon"]]
    return max(us) - min(us), max(vs) - min(vs)
