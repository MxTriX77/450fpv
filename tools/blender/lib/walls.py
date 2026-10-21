"""The house's plan and walls (p1-h01-adobe-house, design decisions 1, 3, 6 and 9; object sheet
docs/object-sheets/houses-and-yard.md §Shared and §H01): the footprint and its spaces, the openings, every wall
surface with its asset-space UVs, the wall collision boxes and the atlas layout the damage mask is drawn on. Plain
Python in asset space (lib.frame), no Blender.

Frame: +X east along the length, +Z north across it, +Y up; the house centred on the origin, its walls standing on
the ground at y = 0. The front wall (the door's) faces -Z. The ends: `roof_type_front` is the -X end (left as seen
from the door side), `roof_type_back` the +X end.

UVs (AST-07, knowledge.md §14.4): metres, V up, U along the wall as seen from where it is looked at. The facade is one
loop: from the north-east corner along the back wall, the left end, the front and the right end, so U continues
across every corner and restarts once, at that back corner, where it has run the whole perimeter (the mask is drawn
periodic in U, so it meets itself there). Every room's inner faces are a loop of their own, from its front-right
corner as seen from inside. A reveal takes the face's UV folded into the opening: from the arris on, U (or V) goes on
by the depth, so the map turns the corner instead of restarting. A wall top takes the outer face's U and the head's V
plus the depth. Loops are laid side by side in one atlas (ATLAS_M), the facade's from (0, 0) and each room's at its
own offset, so one damage-mask atlas serves the facade and the interior slots.

Collision (design decision 9): each wall a box per segment between openings, 0.45 m thick, from just below the ground
(PLANTED: the ground covers the 50 mm below y = 0) to the plate; a gable as two boxes laid along its slopes, their
lower parts inside the wall below; each partition a box per segment round its doorway.
"""
import math

from lib import joinery, prism

FACADE_SLOT, INTERIOR_SLOT = "facade", "interior"
MASONRY = "masonry"
FLOOR_M = 0.05             # the clay floor's top above grade (assumed: "near grade"; the threshold at yard level, E 7)
PARTITION_M = 0.25         # inner wall thickness (design decision 6, assumed)
INNER_DOOR_M = (0.8, 1.9)  # inner doorways, clear (assumed: the entrance's AST-04 size; the sheet gives none)
INNER_DOOR_FROM_FRONT_M = 1.4  # the doorway's centre from the front wall's inner face (assumed: near the door)
PLATE_M = (0.15, 0.15)     # the wall plate (мауерлат), wide x high, centred on the wall (assumed)
BELOW_M = 0.05             # wall boxes reach this far below the ground (PLANTED)
PIER_M = 0.3               # an opening keeps this much wall to a corner, a partition or the next opening (assumed)
ATLAS_M = (32.0, 16.0)     # the mask atlas in UV metres (2048 x 1024 px at 64 px/m)
ROW_GAP_M = 0.25           # between atlas rows and loops
SIDES = ("back", "left", "front", "right")  # the facade loop's order from its origin at the north-east corner


class Side:
    """One outer wall as seen from outside: `origin` its outer face's left end in plan (x, z), `along` the unit
    direction left to right, `normal` outward, its outer `length` and `u0`, where its outer face starts in the facade
    loop."""

    def __init__(self, name, origin, along, normal, length, u0):
        self.name, self.origin, self.along, self.normal, self.length, self.u0 = name, origin, along, normal, length, u0

    def point(self, a, y, d):
        """Asset space at `a` along the outer face, height y, depth d in from it."""
        return (self.origin[0] + a * self.along[0] - d * self.normal[0], y,
                self.origin[1] + a * self.along[1] - d * self.normal[1])

    def outward(self):
        return (self.normal[0], 0.0, self.normal[1])

    def across(self):
        return (self.along[0], 0.0, self.along[1])


class Opening:
    """A rough opening through an outer wall (a door or a window) or a partition (a doorway): `a0`-`a1` along its
    side's outer face, `y0`-`y1` up, and `zones`, the depths (from the outer face) where the outer reveal ends and
    the inner one starts: a frame fills what lies between."""

    def __init__(self, name, kind, side, a0, a1, y0, y1, zones, state=None, params=None):
        self.name, self.kind, self.side = name, kind, side
        self.a0, self.a1, self.y0, self.y1 = a0, a1, y0, y1
        self.zones, self.state, self.params = zones, state, params or {}


class Space:
    """A room in plan, x0-x1 by z0-z1 between its walls' inner faces, with its loop's place in the atlas."""

    def __init__(self, name, x0, x1, z0, z1):
        self.name, self.x0, self.x1, self.z0, self.z1 = name, x0, x1, z0, z1
        self.u_off = self.v_off = 0.0

    @property
    def loop(self):
        return 2 * ((self.x1 - self.x0) + (self.z1 - self.z0))


class Plan:
    """Everything the walls, the roof and the joinery share: sizes, the roof's pitch and apex, the sides, the
    openings, the spaces and partitions."""

    def __init__(self, p):
        self.L, self.W, self.T, self.H = p["length"], p["width"], p["wall_thickness"], p["wall_height"]
        self.X, self.Z, self.F = self.L / 2, self.W / 2, FLOOR_M
        self.ends = {-1: p["roof_type_front"], 1: p["roof_type_back"]}
        self.theta = math.radians(p["roof_pitch"])
        self.tan, self.cos, self.sin = math.tan(self.theta), math.cos(self.theta), math.sin(self.theta)
        self.rafter_w, self.rafter_d = p["rafter_m"]["w"], p["rafter_m"]["d"]
        self.eaves, self.verge = p["eaves_overhang"], p["verge_overhang"]
        # The rafters' top faces (the roof's structural plane, the battens' seat): each rafter's underside bears on
        # its plate's outer top arris, so the plane passes D / cos above that arris; the apex is the ridge.
        arris = self.Z - self.T / 2 + PLATE_M[0] / 2
        self.apex = self.H + PLATE_M[1] + arris * self.tan + self.rafter_d / self.cos
        L, W, X, Z = self.L, self.W, self.X, self.Z
        self.sides = {"back": Side("back", (X, Z), (-1.0, 0.0), (0.0, 1.0), L, 0.0),
                      "left": Side("left", (-X, Z), (0.0, -1.0), (-1.0, 0.0), W, L),
                      "front": Side("front", (-X, -Z), (1.0, 0.0), (0.0, -1.0), L, L + W),
                      "right": Side("right", (X, -Z), (0.0, 1.0), (1.0, 0.0), W, 2 * L + W)}
        self.perimeter = 2 * (L + W)
        self.end_of_side = {"left": -1, "right": 1}
        self.openings = []
        side = self.sides[p["door_side"]]
        dw, dh = p["door_m"]["width"], p["door_m"]["height"]
        jamb, head = joinery.DOOR_FRAME_M
        centre = p["door_pos"] * side.length
        self.door = Opening("door", "door", p["door_side"], centre - dw / 2 - jamb, centre + dw / 2 + jamb, 0.0,
                            self.F + dh + head, (self.T - joinery.DOOR_DEPTH_M, self.T), p["door_leaf_state"],
                            {"width": dw, "height": dh})
        self.openings.append(self.door)
        for name in sorted(p["windows"]):
            w = p["windows"][name]
            if w["side"] == "none":
                continue
            side = self.sides[w["side"]]
            c = w["pos"] * side.length
            zones = (joinery.WINDOW_DEPTH_M[0], joinery.WINDOW_DEPTH_M[1]) if w["state"] != "frame_gone" else \
                (self.T / 2, self.T / 2)
            self.openings.append(Opening(f"window_{name}", "window", w["side"], c - w["w"] / 2, c + w["w"] / 2,
                                         w["sill"], w["sill"] + w["h"], zones, w["state"], w))
        # The hall at the door and a room each side (design decision 6): the partitions' centre lines
        x_door = self.sides[p["door_side"]].point(centre, 0, 0)[0]
        half = p["hall_width"] / 2
        self.partitions = [x_door - half, x_door + half]
        t2 = PARTITION_M / 2
        inner = (-X + self.T, X - self.T, -Z + self.T, Z - self.T)
        xs = [inner[0], self.partitions[0] - t2, self.partitions[0] + t2, self.partitions[1] - t2,
              self.partitions[1] + t2, inner[1]]
        self.spaces = [Space("room_left", xs[0], xs[1], inner[2], inner[3]),
                       Space("hall", xs[2], xs[3], inner[2], inner[3]),
                       Space("room_right", xs[4], xs[5], inner[2], inner[3])]
        self.inner = inner
        zc = inner[2] + INNER_DOOR_FROM_FRONT_M
        self.doorways = [Opening(f"doorway_{k}", "doorway", None, zc - INNER_DOOR_M[0] / 2, zc + INNER_DOOR_M[0] / 2,
                                 self.F, self.F + INNER_DOOR_M[1], (PARTITION_M / 2, PARTITION_M / 2),
                                 params={"x": xp}) for k, xp in zip(("left", "right"), self.partitions)]
        self._lay_out_atlas()

    # --- the roof's structural plane, which the gable follows -------------------------------------------------

    def rt(self, x, z):
        """The rafters' top plane over (x, z): the side slopes, cut by a hip end's slope."""
        y = self.apex - abs(z) * self.tan
        for end, kind in self.ends.items():
            if kind == "hip":
                y = min(y, self.apex - (end * x - end * self.hip_ridge_end(end)) * self.tan)
        return y

    def hip_ridge_end(self, end):
        """The x where the ridge meets a hip end's two hips: the eaves run equal (same pitch) round the corner."""
        return end * ((self.X + self.eaves) - (self.Z + self.eaves))

    def ridge_x(self):
        """The ridge's two ends in x: a hip end's ridge end, or a gable's verge."""
        return tuple(self.hip_ridge_end(e) if self.ends[e] == "hip" else e * (self.X + self.verge) for e in (-1, 1))

    def side_top(self, side, a):
        """The outer face's top at `a`: the wall head, or up the gable to the roof's plane at a gable end (the
        gable's corner blocks rise with it)."""
        x, _, z = side.point(a, 0, 0)
        if side.name in self.end_of_side and self.ends[self.end_of_side[side.name]] == "gable":
            return self.rt(x, z)
        for end in (-1, 1):
            if self.ends[end] == "gable" and end * x > self.X - self.T:
                return self.rt(x, z)
        return self.H

    def openings_on(self, side):
        return sorted((o for o in self.openings if o.side == side), key=lambda o: o.a0)

    def is_gable(self, side):
        return side in self.end_of_side and self.ends[self.end_of_side[side]] == "gable"

    # --- the atlas ----------------------------------------------------------------------------------------------

    def _lay_out_atlas(self):
        """The facade loop from (0, 0), up to the gable's apex; the rooms' loops above it, side by side, each as tall
        as its walls (the room beside a gable as tall as the gable's attic face)."""
        v = self.apex + ROW_GAP_M
        row_u, row_h = 0.0, 0.0
        for space in self.spaces:
            height = self.attic_top() - self.F if self._gable_of(space) else self.H - self.F
            if row_u + space.loop > ATLAS_M[0] + 1e-9:
                v += row_h + ROW_GAP_M
                row_u, row_h = 0.0, 0.0
            space.u_off, space.v_off = row_u + (self.T if self._gable_of(space) else 0.0), v
            row_u += space.loop + 2 * self.T + ROW_GAP_M
            row_h = max(row_h, height)
        self.atlas_top = v + row_h

    def attic_top(self):
        return self.apex

    def _gable_of(self, space):
        """The end (-1 or 1) of a gable wall this space stands against, or None."""
        for end in (-1, 1):
            edge = space.x0 if end < 0 else space.x1
            if self.ends[end] == "gable" and abs(abs(edge) - (self.X - self.T)) < 1e-9 and edge * end > 0:
                return end
        return None

    def facade_uv(self, side, a, v):
        return (side.u0 + a, v)


# --- surfaces -------------------------------------------------------------------------------------------------------

def build(plan, mesh):
    """Emits every wall surface into `mesh`: the outer faces, the rooms' inner faces, the reveals, the wall tops and
    the gable's top and attic face. Returns the canvas regions the damage mask is drawn on (lib.masks.facade_atlas)."""
    regions = [_outer_faces(plan, mesh)]
    for space in plan.spaces:
        regions.append(_inner_faces(plan, mesh, space))
    for o in plan.openings:
        _reveals(plan, mesh, o)
    for d in plan.doorways:
        _doorway_reveals(plan, mesh, d)
    _tops(plan, mesh)
    return regions


def _grid_faces(mesh, slot, origin_fn, normal, breaks_a, top_fn, holes, uv_fn, bottom):
    """A wall face as a grid of quads between every opening edge (no T-junctions), from `bottom` to `top_fn(a)`
    (straight between the a breaks), without the cells inside `holes` [(a0, a1, y0, y1)]. Above the highest y break
    each column is one quad to the top line. Returns the face's area."""
    a_breaks = sorted(set(round(b, 9) for b in breaks_a))
    y_breaks = sorted(set([bottom] + [round(y, 9) for h in holes for y in (h[2], h[3])]))
    area = 0.0
    for a0, a1 in zip(a_breaks, a_breaks[1:]):
        t0, t1 = top_fn(a0), top_fn(a1)
        ys = [y for y in y_breaks if y < min(t0, t1) - 1e-9]
        for k, y0 in enumerate(ys):
            last = k == len(ys) - 1
            y1 = ys[k + 1] if not last else None
            mid_a = (a0 + a1) / 2
            probe = (y0 + (y1 if y1 is not None else min(t0, t1))) / 2
            if any(h[0] - 1e-9 <= mid_a <= h[1] + 1e-9 and h[2] - 1e-9 <= probe <= h[3] + 1e-9 for h in holes):
                continue
            if last:
                pts = [(a0, y0), (a1, y0), (a1, t1), (a0, t0)]
            else:
                pts = [(a0, y0), (a1, y0), (a1, y1), (a0, y1)]
            ids = [mesh.vertex(origin_fn(a, y)) for a, y in pts]
            mesh.face(ids, normal, slot, [uv_fn(a, y) for a, y in pts])
            area += (a1 - a0) * ((y1 - y0) if not last else ((t0 + t1) / 2 - y0))
    return area


def _outer_faces(plan, mesh):
    """Every side's outer face in the facade loop, and the facade's canvas region."""
    region = {"name": "facade", "kind": "facade", "u_off": 0.0, "v_off": 0.0, "period": plan.perimeter,
              "spans": [], "openings": [], "corners": [plan.sides[s].u0 for s in SIDES], "foot": 0.0,
              "head": plan.H, "base_band": True}
    for name in SIDES:
        side = plan.sides[name]
        holes = [(o.a0, o.a1, o.y0, o.y1) for o in plan.openings_on(name)]
        breaks = [0.0, side.length] + [b for h in holes for b in h[:2]]
        gable = plan.is_gable(name)
        if gable:
            breaks.append(side.length / 2)
        blocks = []  # a long side's corner block, which rises with a gable at its end
        for end in (-1, 1):
            if plan.ends[end] == "gable" and name in ("front", "back"):
                a = _a_at_x(side, end * (plan.X - plan.T))
                breaks.append(a)
                blocks.append((0.0, a) if a < side.length / 2 else (a, side.length))
        top = (lambda a, s=side: plan.side_top(s, a)) if gable else (lambda a: plan.H)
        _grid_faces(mesh, FACADE_SLOT, lambda a, y, s=side: s.point(a, y, 0.0), side.outward(), breaks, top, holes,
                    lambda a, y, s=side: (s.u0 + a, y), 0.0)
        for a0, a1 in blocks:
            pts = [(a0, plan.H), (a1, plan.H), (a1, plan.side_top(side, a1 - 1e-9)), (a0, plan.side_top(side, a0 + 1e-9))]
            ids = [mesh.vertex(side.point(a, y, 0.0)) for a, y in pts]
            mesh.face(ids, side.outward(), FACADE_SLOT, [(side.u0 + a, y) for a, y in pts])
        cuts = sorted(set(round(b, 9) for b in breaks))
        region["spans"] += [(side.u0 + a0, side.u0 + a1, plan.side_top(side, a0 + 1e-6), plan.side_top(side, a1 - 1e-6))
                            for a0, a1 in zip(cuts, cuts[1:])]
        region["openings"] += [(side.u0 + h[0], side.u0 + h[1], h[2], h[3]) for h in holes]
    return region


def _a_at_x(side, x):
    return (x - side.origin[0]) / side.along[0]


def inner_walls(plan, space):
    """A room's four inner faces in its loop order, seen from inside: (name, start point (x, z), unit direction,
    inward normal, length, a offset in the loop, the outer side or partition it belongs to)."""
    x0, x1, z0, z1 = space.x0, space.x1, space.z0, space.z1
    walls = [("front", (x1, z0), (-1.0, 0.0), (0.0, 1.0), x1 - x0),
             ("left", (x0, z0), (0.0, 1.0), (1.0, 0.0), z1 - z0),
             ("back", (x0, z1), (1.0, 0.0), (0.0, -1.0), x1 - x0),
             ("right", (x1, z1), (0.0, -1.0), (-1.0, 0.0), z1 - z0)]
    out, a = [], 0.0
    for name, start, along, inward, length in walls:
        out.append((name, start, along, inward, length, a))
        a += length
    return out


def _inner_holes(plan, space, wall):
    """The openings in one inner face, in its own a coordinate: an outer side's windows and door, or a partition's
    doorway."""
    name, start, along, inward, length, _ = wall
    holes = []
    if name in ("front", "back"):
        side = plan.sides[name]
        for o in plan.openings_on(name):
            xs = [side.point(a, 0, 0)[0] for a in (o.a0, o.a1)]
            a_in = sorted(((x - start[0]) / along[0]) for x in xs)
            if a_in[1] > 1e-9 and a_in[0] < length - 1e-9:
                holes.append((a_in[0], a_in[1], o.y0, o.y1, o))
    else:
        x = start[0]
        outer = "left" if name == "left" else "right"
        if abs(abs(x) - (plan.X - plan.T)) < 1e-9 and (x < 0) == (outer == "left"):
            side = plan.sides[outer]
            for o in plan.openings_on(outer):
                zs = [side.point(a, 0, 0)[2] for a in (o.a0, o.a1)]
                a_in = sorted(((z - start[1]) / along[1]) for z in zs)
                holes.append((a_in[0], a_in[1], o.y0, o.y1, o))
        else:
            for d in plan.doorways:
                if abs(abs(d.params["x"] - x) - PARTITION_M / 2) < 1e-9:
                    a_in = sorted(((z - start[1]) / along[1]) for z in (d.a0, d.a1))
                    holes.append((a_in[0], a_in[1], d.y0, d.y1, d))
    return holes


def _inner_faces(plan, mesh, space):
    """A room's inner faces, from the floor to the ceiling (to the gable's slope where the room stands against a
    gable: its attic face, wall to wall over the corner blocks), and its canvas region."""
    region = {"name": space.name, "kind": "interior", "u_off": space.u_off, "v_off": space.v_off,
              "period": space.loop, "spans": [], "openings": [], "corners": [], "foot": 0.0,
              "head": plan.H - plan.F, "base_band": False}
    gable = plan._gable_of(space)
    for wall in inner_walls(plan, space):
        name, start, along, inward, length, a_loop = wall
        holes = _inner_holes(plan, space, wall)
        region["corners"].append(space.u_off + a_loop)

        def point(a, y, start=start, along=along):
            return (start[0] + a * along[0], y, start[1] + a * along[1])

        def uv(a, y, a_loop=a_loop):
            return (space.u_off + a_loop + a, space.v_off + y - plan.F)

        normal = (inward[0], 0.0, inward[1])
        at_gable = gable is not None and name == ("left" if gable < 0 else "right")
        lo, hi = (-plan.T, length + plan.T) if at_gable else (0.0, length)
        breaks = [0.0, length] + [b for h in holes for b in h[:2]] + ([length / 2] if at_gable else [])

        def top(a, at_gable=at_gable):
            if not at_gable:
                return plan.H
            x, _, z = point(a, 0)
            return plan.rt(x, z)

        _grid_faces(mesh, INTERIOR_SLOT, point, normal, breaks, top if at_gable else (lambda a: plan.H),
                    [h[:4] for h in holes], uv, plan.F)
        if at_gable:  # the attic face over the corner blocks, wall face to wall face, above the wall head
            for a0, a1 in ((lo, 0.0), (0.0, length / 2), (length / 2, length), (length, hi)):
                pts = [(a0, plan.H), (a1, plan.H), (a1, top(a1)), (a0, top(a0))]
                if a0 >= 0.0 and a1 <= length:
                    continue  # inside the room's face, drawn above
                ids = [mesh.vertex(point(a, y)) for a, y in pts]
                mesh.face(ids, normal, INTERIOR_SLOT, [uv(a, y) for a, y in pts])
        cuts = sorted(set(round(b, 9) for b in breaks))
        region["spans"] += [(space.u_off + a_loop + a0, space.u_off + a_loop + a1, top(a0) - plan.F,
                             top(a1) - plan.F) for a0, a1 in zip(cuts, cuts[1:])]
        region["openings"] += [(space.u_off + a_loop + h[0], space.u_off + a_loop + h[1], h[2] - plan.F,
                                h[3] - plan.F) for h in holes]
    return region


def _reveals(plan, mesh, o):
    """An outer opening's reveals: the jambs, the sill (none when it reaches the ground) and the head, from the outer
    face to the first zone depth in the facade slot and from the second to the inner face in the interior slot, each
    UV'd folded from its face."""
    side = plan.sides[o.side]
    d_out, d_in = o.zones
    inner = _inner_side_coords(plan, o)
    for part, (d0, d1) in (("outer", (0.0, d_out)), ("inner", (d_in, plan.T))):
        if d1 - d0 <= 1e-9:
            continue
        slot = FACADE_SLOT if part == "outer" else INTERIOR_SLOT

        def uv(a, y, d, part=part):
            if part == "outer":
                return (side.u0 + a, y), d
            ua, va = inner(a, y)
            return (ua, va), plan.T - d

        # (fixed coordinate, axis, the face's normal in (along, y) terms, fold sign)
        for kind in ("left", "right", "sill", "head"):
            if kind == "sill" and o.y0 <= 1e-9:
                continue
            if kind in ("left", "right"):
                a = o.a0 if kind == "left" else o.a1
                quad = [(a, o.y0, d0), (a, o.y0, d1), (a, o.y1, d1), (a, o.y1, d0)]
                n = side.across() if kind == "left" else tuple(-c for c in side.across())
            else:
                y = o.y0 if kind == "sill" else o.y1
                quad = [(o.a0, y, d0), (o.a1, y, d0), (o.a1, y, d1), (o.a0, y, d1)]
                n = (0.0, 1.0, 0.0) if kind == "sill" else (0.0, -1.0, 0.0)
            ids, uvs = [], []
            for a, y, d in quad:
                ids.append(mesh.vertex(side.point(a, y, d)))
                (u, v), depth = uv(a, y, d)
                uvs.append(_fold(kind, u, v, depth, part))
            mesh.face(ids, n, slot, uvs)


def _fold(kind, u, v, depth, part):
    """A reveal point's UV: its face's UV at the arris, folded on by its depth from that face, into the opening."""
    if kind == "left":
        return (u + depth, v) if part == "outer" else (u - depth, v)
    if kind == "right":
        return (u - depth, v) if part == "outer" else (u + depth, v)
    if kind == "sill":
        return (u, v + depth)
    return (u, v - depth)


def _inner_side_coords(plan, o):
    """The interior UV at an outer opening's edge point (a along its side, y): the room's loop UV there."""
    side = plan.sides[o.side]

    def uv(a, y):
        p = side.point(a, y, plan.T)
        for space in plan.spaces:
            for wall in inner_walls(plan, space):
                name, start, along, inward, length, a_loop = wall
                ai = (p[0] - start[0]) * along[0] + (p[2] - start[1]) * along[1]
                off = abs((p[0] - start[0]) * inward[0] + (p[2] - start[1]) * inward[1])
                if off < 1e-6 and -1e-6 <= ai <= length + 1e-6:
                    return (space.u_off + a_loop + ai, space.v_off + y - plan.F)
        raise ValueError(f"{o.name}: no room's inner face meets its edge at a = {a:.3f}")
    return uv


def _doorway_reveals(plan, mesh, d):
    """A doorway's jambs and head through its partition, each half UV'd folded from the room on its side."""
    xp, t2 = d.params["x"], PARTITION_M / 2
    for sx in (-1, 1):  # each half of the partition, from its face at xp + sx * t2 to the centre line
        x_face = xp + sx * t2
        space = next(s for s in plan.spaces if abs(s.x0 - x_face) < 1e-9 or abs(s.x1 - x_face) < 1e-9)
        wall = next(w for w in inner_walls(plan, space)
                    if w[0] in ("left", "right") and abs(w[1][0] - x_face) < 1e-9)
        _, start, along, inward, length, a_loop = wall

        def uv(z, y, depth, kind):
            ai = (z - start[1]) * along[1]
            u, v = space.u_off + a_loop + ai, space.v_off + y - plan.F
            lo_edge = min(d.a0, d.a1) if along[1] > 0 else max(d.a0, d.a1)
            if kind == "head":
                return (u, v - depth)
            into = 1.0 if abs(z - lo_edge) < 1e-9 else -1.0
            return (u + into * depth, v)

        for kind in ("z0", "z1", "head"):
            if kind == "head":
                quad = [(x_face, d.y1, d.a0), (x_face, d.y1, d.a1), (xp, d.y1, d.a1), (xp, d.y1, d.a0)]
                n = (0.0, -1.0, 0.0)
            else:
                z = d.a0 if kind == "z0" else d.a1
                quad = [(x_face, d.y0, z), (xp, d.y0, z), (xp, d.y1, z), (x_face, d.y1, z)]
                n = (0.0, 0.0, 1.0) if kind == "z0" else (0.0, 0.0, -1.0)
            ids = [mesh.vertex(p) for p in quad]
            mesh.face(ids, n, INTERIOR_SLOT, [uv(p[2], p[1], abs(p[0] - x_face), kind) for p in quad])


def _tops(plan, mesh):
    """The wall heads outside the plate (each long side over its whole length but a gable's corner block, each end
    between the long sides' strips) and a gable's top, both slopes, UV'd folded from the outer face."""
    strip = plan.T / 2 - PLATE_M[0] / 2
    for name in SIDES:
        side = plan.sides[name]
        if plan.is_gable(name):
            half = side.length / 2
            for a0, a1 in ((0.0, half), (half, side.length)):
                quad = [(a0, 0.0), (a1, 0.0), (a1, plan.T), (a0, plan.T)]
                pts = [side.point(a, plan.side_top(side, a), d) for a, d in quad]
                n = _newell(pts)
                if n[1] < 0:
                    n = tuple(-c for c in n)
                ids = [mesh.vertex(p) for p in pts]
                mesh.face(ids, n, FACADE_SLOT, [(side.u0 + a, plan.side_top(side, a) + d) for a, d in quad])
            continue
        # the outer strip, face to plate: a long side over its length (to a gable's inner face), an end between them;
        # the inner strip, plate to inner face: a long side to a gable's inner face or a hip end's plate, an end
        # between the long sides' inner faces
        inner_d = plan.T / 2 + PLATE_M[0] / 2
        if name in ("front", "back"):
            spans = {}
            for which, d0 in (("outer", 0.0), ("inner", inner_d)):
                a_lo, a_hi = 0.0, side.length
                for end in (-1, 1):
                    stop = plan.T if plan.ends[end] == "gable" else (0.0 if which == "outer" else inner_d)
                    a = _a_at_x(side, end * (plan.X - stop))
                    a_lo, a_hi = (max(a_lo, a), a_hi) if a < side.length / 2 else (a_lo, min(a_hi, a))
                spans[which] = (a_lo, a_hi)
        else:
            spans = {"outer": (strip, side.length - strip), "inner": (plan.T, side.length - plan.T)}
        for which, (d0, d1) in (("outer", (0.0, strip)), ("inner", (inner_d, plan.T))):
            a_lo, a_hi = spans[which]
            quad = [(a_lo, d0), (a_hi, d0), (a_hi, d1), (a_lo, d1)]
            ids = [mesh.vertex(side.point(a, plan.H, d)) for a, d in quad]
            mesh.face(ids, (0.0, 1.0, 0.0), FACADE_SLOT, [(side.u0 + a, plan.H + d) for a, d in quad])


def floor(plan, mesh):
    """Each room's clay floor, FLOOR_M above grade, and the floor through each inner doorway."""
    quads = [[(s.x0, s.z0), (s.x1, s.z0), (s.x1, s.z1), (s.x0, s.z1)] for s in plan.spaces]
    for d in plan.doorways:  # and through each doorway, across its partition
        x0, x1 = d.params["x"] - PARTITION_M / 2, d.params["x"] + PARTITION_M / 2
        quads.append([(x0, d.a0), (x1, d.a0), (x1, d.a1), (x0, d.a1)])
    for quad in quads:
        ids = [mesh.vertex((x, plan.F, z)) for x, z in quad]
        mesh.face(ids, (0.0, 1.0, 0.0), "clay", [(x, z) for x, z in quad])


def floor_box(plan):
    """The floor's box over the whole interior, its top the floor, its foot under the ground (PLANTED)."""
    x0, x1, z0, z1 = plan.inner
    return _axis_box((x0, -2 * BELOW_M, z0), (x1, plan.F, z1))


def outer_and_liner(plan, mesh):
    """LOD3: the outer faces with their openings, and behind them a dark liner per room (its floor, walls and
    ceiling facing in), so an opening shows the inside, not the sky."""
    _outer_faces(plan, mesh)
    for s in plan.spaces:
        x0, x1, z0, z1, y0, y1 = s.x0, s.x1, s.z0, s.z1, plan.F, plan.H
        faces = [([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0.0, 1.0, 0.0)),
                 ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0.0, -1.0, 0.0)),
                 ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0.0, 0.0, 1.0)),
                 ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0.0, 0.0, -1.0)),
                 ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (1.0, 0.0, 0.0)),
                 ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (-1.0, 0.0, 0.0))]
        for pts, n in faces:
            ids = [mesh.vertex(p) for p in pts]
            mesh.face(ids, n, INTERIOR_SLOT, [(s.u_off + p[0] - x0 + p[2] - z0, s.v_off + p[1] - y0) for p in pts])


# --- collision --------------------------------------------------------------------------------------------------

def boxes(plan):
    """The wall boxes (design decision 9): per side the piers between openings from BELOW_M under the ground to the
    head, and the wall under and over each opening; the long sides over their whole length, the ends between them;
    a gable's triangle as two boxes along its slopes; the partitions round their doorways."""
    out = []
    for name in SIDES:
        side = plan.sides[name]
        long_side = name in ("front", "back")
        a_lo, a_hi = (0.0, side.length) if long_side else (plan.T, side.length - plan.T)
        cuts = [a_lo]
        for o in plan.openings_on(name):
            cuts += [o.a0, o.a1]
        cuts.append(a_hi)
        segments = [(cuts[k], cuts[k + 1], -BELOW_M, plan.H) for k in range(0, len(cuts), 2)]
        for o in plan.openings_on(name):
            if o.y0 > 1e-9:
                segments.append((o.a0, o.a1, -BELOW_M, o.y0))
            segments.append((o.a0, o.a1, o.y1, plan.H))
        for a0, a1, y0, y1 in segments:
            if a1 - a0 > 1e-9 and y1 - y0 > 1e-9:
                out.append(_side_box(side, a0, a1, y0, y1, 0.0, plan.T))
        if plan.is_gable(name):
            out += _gable_boxes(plan, side)
        for end in (-1, 1):  # a long side's corner block over the head, up to the roof's plane at its outer face
            if long_side and plan.ends[end] == "gable":
                a = _a_at_x(side, end * (plan.X - plan.T))
                a0, a1 = (0.0, a) if a < side.length / 2 else (a, side.length)
                out.append(_side_box(side, a0, a1, plan.H, plan.side_top(side, (a0 + a1) / 2), 0.0, plan.T))
    for d in plan.doorways:
        xp, t2 = d.params["x"], PARTITION_M / 2
        z0, z1 = plan.inner[2], plan.inner[3]
        for zz0, zz1, y0, y1 in ((z0, d.a0, 0.0, plan.H), (d.a1, z1, 0.0, plan.H), (d.a0, d.a1, d.y1, plan.H)):
            out.append(_axis_box((xp - t2, y0, zz0), (xp + t2, y1, zz1)))
    return out


def _side_box(side, a0, a1, y0, y1, d0, d1):
    p0, p1 = side.point(a0, y0, d0), side.point(a1, y1, d1)
    return _axis_box(tuple(min(p0[k], p1[k]) for k in range(3)), tuple(max(p0[k], p1[k]) for k in range(3)))


def _axis_box(lo, hi):
    return {"shape": "box", "center": [(lo[k] + hi[k]) / 2 for k in range(3)], "size": [hi[k] - lo[k] for k in range(3)],
            "rotation": [0.0, 0.0, 0.0, 1.0], "material": MASONRY, "landable": False}


def _gable_boxes(plan, side):
    """A gable above the wall head as two boxes, one per slope: its top on the roof's plane from the corner to the
    apex, as deep (square to the slope) as the apex stands above the head's centre, so the triangle is held and what
    the box holds past it lies inside the wall below or the other slope's box."""
    x_out = side.origin[0]
    x_in = x_out - side.normal[0] * plan.T
    xc, xt = (x_out + x_in) / 2, abs(x_out - x_in)
    depth = (plan.apex - plan.H) * plan.cos
    out = []
    for sz in (-1, 1):
        corner = (sz * plan.Z, plan.rt(xc, sz * plan.Z))  # (z, y)
        apex = (0.0, plan.apex)
        length = math.dist(corner, apex)
        dz, dy = (apex[0] - corner[0]) / length, (apex[1] - corner[1]) / length  # up the slope
        nz, ny = (dy, -dz) if dz > 0 else (-dy, dz)  # square to it, down into the triangle
        cz = (corner[0] + apex[0]) / 2 + nz * depth / 2
        cy = (corner[1] + apex[1]) / 2 + ny * depth / 2
        ex, ey = (1.0, 0.0, 0.0), (0.0, -ny, -nz)  # across the wall, square to the slope (up), along it
        frame = prism.Frame((xc, cy, cz), ex, ey, prism.cross(ex, ey))
        out.append({"shape": "box", "center": [xc, cy, cz], "size": [xt, depth, length], "rotation": frame.quat(),
                    "material": MASONRY, "landable": False})
    return out


def _newell(pts):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    length = math.sqrt(sum(c * c for c in n))
    return tuple(c / length for c in n)
