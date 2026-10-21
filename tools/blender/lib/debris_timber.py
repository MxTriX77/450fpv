"""The K03 sawn-timber pieces as library builders (p1-h01-adobe-house, design decision 10; object sheet
docs/object-sheets/lines-roads-debris.md §K03): a board, a batten or a sawn rafter, broken at none, one or both ends
into splinters, with nails standing proud, for the `debris_timber` generator and for a host's rubble (the `house`
generator's fallen battens, rafters and ceiling boards). Plain Python on lib.geometry, no Blender.

A piece is a dict {"lods": [LOD0, LOD1], "collision", "snag", "materials": [{"slot", "physics"}]} in its own frame:
x along the piece, lying on its wide face (section w across in z, h up in y), centred on the origin in plan, its
underside on y = 0; a broken end is the +x end first, then -x. A host places it with lib.kits_vehicle's Rigid
(append_mesh, transform_shape, transform_snag), keeping the slot names.

A broken end (the sheet's "snap at mid-span or at the plate with long splinters"): the section is split into
`splinter_count` cells, each split of the largest cell across its longer side at SPLIT, and every cell runs on past
the solid body as a splinter tapering to a tip TAPER of its root, splayed outward by up to SPLAY of its length. The
splinters are longest on the end's tension face (seeded, top or underside) and shortest on the other, a bending
break's stepped end; the longest reaches the piece's `length` exactly. Each splinter is geometry with its own capsule
(the RMS of its half-section at mid-length, along its axis) and a `hook` snag of half its thickness there; the end's
section outline at the root is an `edge` snag, EDGE_SNAG_R_M. Faces a splinter keeps of the piece's outer surface are
`timber`, the faces the break opened `timber_fracture`, the paler fresh wood.

Nails: each stands from the top face at a stratified seeded place along the body, a 3.5 mm shank (NAIL_R_M) bent
once at a seeded height and angle, two capsules and one `hook` snag through its root, bend and tip, slot `nail`
(steel).

UVs (AST-07): metres in the piece's frame, V along the piece so the set's grain runs along it, U round the section
continuous across its four long faces (from the underside's -z arris), and a seeded offset per piece so pieces in a
heap do not repeat one another. LOD1 is the body with one tapered block per broken end and no nails.
"""
import math

from lib import meta
from lib.geometry import Mesh

TIMBER_SLOT, FRACTURE_SLOT, NAIL_SLOT = "timber", "timber_fracture", "nail"
PHYSICS, NAIL_PHYSICS = "timber", "steel"  # the sheet: sawn timber is `timber`; a nail is steel
EDGE_SNAG_R_M = 0.002       # the sheet: broken ends, `edge`, 2 mm
SPLIT = (0.35, 0.65)        # where a cell splits across its longer side (assumed)
SHORTEST = 0.3              # a splinter on the compression face against one on the tension face (assumed)
LENGTH_JITTER = (0.6, 1.0)  # each splinter's own share of its face's length (assumed)
TAPER = (0.15, 0.4)         # a splinter's tip section as a share of its root's (assumed)
SPLAY = 0.08                # a tip's outward spread as a share of its length, about 5 degrees (assumed)
ENVELOPE_TIP = 0.5          # LOD1's block: its tip section as a share of the piece's
NAIL_R_M = 0.00175          # the sheet: nails 3-4 mm, 3.5 mm chosen
NAIL_SIDES = 6
NAIL_BEND = (0.4, 0.8)      # where along its proud length a nail bends (assumed)
NAIL_BEND_DEG = (0.0, 60.0) # and how far from upright (assumed: "often bent")
NAIL_EMBED_M = 0.004        # its first capsule starts this deep in the wood, so no cap stands proud at the face
NAIL_EDGE_M = 0.012         # a nail keeps this far from the top face's arrises (assumed)
NAIL_END_M = 0.05           # and from the body's ends (assumed)
GRAIN_TILE_M = (0.5, 1.0)   # the seeded UV offset's range: one tile of recipe roof_timber


def split_section(rng, w, h, count):
    """`count` cells (z0, z1, y0, y1) tiling the section [-w/2, w/2] x [0, h]: the largest cell is split across its
    longer side until there are enough."""
    cells = [(-w / 2, w / 2, 0.0, h)]
    while len(cells) < count:
        k = max(range(len(cells)), key=lambda i: (cells[i][1] - cells[i][0]) * (cells[i][3] - cells[i][2]))
        z0, z1, y0, y1 = cells.pop(k)
        f = rng.uniform(*SPLIT)
        if z1 - z0 >= y1 - y0:
            zm = z0 + (z1 - z0) * f
            cells[k:k] = [(z0, zm, y0, y1), (zm, z1, y0, y1)]
        else:
            ym = y0 + (y1 - y0) * f
            cells[k:k] = [(z0, z1, y0, ym), (z0, z1, ym, y1)]
    return cells


def splinters(rng, w, h, length, count):
    """The splinters of one broken end: per cell its root cell, its length along the piece, its tip's centre (z, y)
    and half-section (z, y); the longest is `length`."""
    cells = split_section(rng, w, h, count)
    tension_top = rng.random() < 0.5
    raw = []
    for z0, z1, y0, y1 in cells:
        share = (y0 + y1) / 2 / h
        share = share if tension_top else 1 - share
        raw.append((SHORTEST + (1 - SHORTEST) * share) * rng.uniform(*LENGTH_JITTER))
    scale = length / max(raw)
    out = []
    for (z0, z1, y0, y1), r in zip(cells, raw):
        ell = r * scale
        cz, cy = (z0 + z1) / 2, (y0 + y1) / 2
        dz, dy = cz, cy - h / 2
        norm = math.hypot(dz, dy)
        if norm < 1e-9:
            a = rng.uniform(0, 2 * math.pi)
            dz, dy, norm = math.cos(a), math.sin(a), 1.0
        spread = ell * rng.uniform(0, SPLAY)
        half = ((z1 - z0) / 2 * rng.uniform(*TAPER), (y1 - y0) / 2 * rng.uniform(*TAPER))
        tip = (cz + dz / norm * spread, max(cy + dy / norm * spread, half[1]))  # never below the underside
        out.append({"cell": (z0, z1, y0, y1), "length": ell, "tip": tip, "tip_half": half})
    return out


def piece(rng, section, length, break_ends, splinter_len, splinter_count, nails, nail_proud):
    """A sawn piece of `section` (w, h) and `length`, broken at `break_ends` (0-2) ends into `splinter_count`
    splinters of up to `splinter_len`, with `nails` standing `nail_proud` from its top (the module's piece)."""
    w, h = section
    ends = [1, -1][:break_ends]
    x_lo = -length / 2 + (splinter_len if -1 in ends else 0.0)
    x_hi = length / 2 - (splinter_len if 1 in ends else 0.0)
    offset = (rng.uniform(0, GRAIN_TILE_M[0]), rng.uniform(0, GRAIN_TILE_M[1]))
    uv = _UV(w, h, offset)
    lods = [Mesh(), Mesh()]
    for mesh in lods:
        _body(mesh, uv, w, h, x_lo, x_hi, ends)
    collision = [{"shape": "box", "center": [(x_lo + x_hi) / 2, h / 2, 0.0], "size": [x_hi - x_lo, h, w],
                  "rotation": [0.0, 0.0, 0.0, 1.0], "material": PHYSICS, "landable": False}]
    snag = []
    volume = (x_hi - x_lo) * w * h
    for end in ends:
        x_root = x_hi if end > 0 else x_lo
        end_collision, end_snag, broken = broken_end(lods, uv, rng, w, h, x_root, end, splinter_len, splinter_count)
        collision += end_collision
        snag += end_snag
        volume += sum(s["volume"] for s in broken)
    span = (x_hi - x_lo - 2 * NAIL_END_M) / max(nails, 1)
    for k in range(nails):
        x = x_lo + NAIL_END_M + span * (k + rng.random())
        z = rng.uniform(-w / 2 + NAIL_EDGE_M, w / 2 - NAIL_EDGE_M) if w > 2 * NAIL_EDGE_M else 0.0
        bend = h + nail_proud * rng.uniform(*NAIL_BEND)
        beta, phi = math.radians(rng.uniform(*NAIL_BEND_DEG)), rng.uniform(0, 2 * math.pi)
        rest = h + nail_proud - bend
        p0, p1 = (x, h, z), (x, bend, z)
        p2 = (x + rest * math.sin(beta) * math.cos(phi), bend + rest * math.cos(beta),
              z + rest * math.sin(beta) * math.sin(phi))
        _rod(lods[0], p0, p1, (False, True))
        _rod(lods[0], p1, p2, (True, True))
        collision += [meta.capsule((x, h - NAIL_EMBED_M, z), p1, NAIL_R_M, NAIL_PHYSICS),
                      meta.capsule(p1, p2, NAIL_R_M, NAIL_PHYSICS)]
        snag.append({"kind": "hook", "points": [list(p0), list(p1), list(p2)], "radius_m": NAIL_R_M})
    slots = lods[0].slots()
    physics = {TIMBER_SLOT: PHYSICS, FRACTURE_SLOT: PHYSICS, NAIL_SLOT: NAIL_PHYSICS}
    return {"lods": lods, "collision": collision, "snag": snag,
            "materials": [{"slot": s, "physics": physics[s]} for s in (TIMBER_SLOT, FRACTURE_SLOT, NAIL_SLOT)
                          if s in slots], "volume_m3": volume}


def broken_end(lods, uv, rng, w, h, x_root, end, splinter_len, splinter_count):
    """One broken end of a member of section (w, h) in the piece's frame, its root section at x = x_root and its
    splinters running towards `end` (+1 or -1): the splinters into lods[0], LOD1's tapered block into lods[1] (when
    given), and (collision, snags, splinters): a capsule and a `hook` per splinter, an `edge` round the root section,
    and each splinter's {"length", "tip", "tip_half", "cell", "volume"} (a host bounds the clear space past the end by
    them). A host's own member (the house's battens and ceiling boards) breaks with the same splinters."""
    collision, snag = [], []
    broken = splinters(rng, w, h, splinter_len, splinter_count)
    for s in broken:
        root, tip, radius, thickness = _splinter(lods[0], uv, w, h, x_root, end, s)
        collision.append(meta.capsule(root, tip, radius, PHYSICS))
        snag.append({"kind": "hook", "points": [list(root), list(tip)], "radius_m": thickness / 2})
        z0, z1, y0, y1 = s["cell"]
        a0, a1 = (z1 - z0) * (y1 - y0), 4 * s["tip_half"][0] * s["tip_half"][1]
        s["volume"] = s["length"] * (a0 + a1 + math.sqrt(a0 * a1)) / 3  # a frustum between root and tip
    if len(lods) > 1 and lods[1] is not None:
        mean = sum(s["length"] for s in broken) / len(broken)
        _envelope(lods[1], uv, w, h, x_root, end, mean)
    snag.append({"kind": "edge", "radius_m": EDGE_SNAG_R_M,
                 "points": [[x_root, y, z] for y, z in ((0.0, -w / 2), (0.0, w / 2), (h, w / 2), (h, -w / 2),
                                                        (0.0, -w / 2))]})
    return collision, snag, broken


class _UV:
    """The piece's UVs: V along it, U round the section from the underside's -z arris (underside, +z side, top, -z
    side), or across an inner face or an end; plus the piece's offset."""

    def __init__(self, w, h, offset):
        self.w, self.h, self.offset = w, h, offset

    def round(self, p, face):
        w, h = self.w, self.h
        x, y, z = p
        u = {"under": z + w / 2, "pz": w + y, "top": w + h + (w / 2 - z), "nz": 2 * w + h + (h - y)}[face]
        return (u + self.offset[0], x + self.offset[1])

    def across(self, p, normal):
        x, y, z = p
        u = z if abs(normal[1]) >= abs(normal[2]) else y
        if abs(normal[0]) > max(abs(normal[1]), abs(normal[2])):  # an end: across and up
            return (z + self.offset[0], y + self.offset[1])
        return (u + self.offset[0], x + self.offset[1])


UV = _UV  # a host's broken end (broken_end) takes the piece's UVs


def _body(mesh, uv, w, h, x_lo, x_hi, ends):
    """The solid body between the broken ends' roots (or the sawn ends), its broken end faces in FRACTURE_SLOT."""
    faces = {"under": ((0.0, -1.0, 0.0), [(x_lo, 0, -w / 2), (x_hi, 0, -w / 2), (x_hi, 0, w / 2), (x_lo, 0, w / 2)]),
             "pz": ((0.0, 0.0, 1.0), [(x_lo, 0, w / 2), (x_hi, 0, w / 2), (x_hi, h, w / 2), (x_lo, h, w / 2)]),
             "top": ((0.0, 1.0, 0.0), [(x_lo, h, w / 2), (x_hi, h, w / 2), (x_hi, h, -w / 2), (x_lo, h, -w / 2)]),
             "nz": ((0.0, 0.0, -1.0), [(x_lo, h, -w / 2), (x_hi, h, -w / 2), (x_hi, 0, -w / 2), (x_lo, 0, -w / 2)])}
    for name, (normal, quad) in faces.items():
        mesh.face([mesh.vertex(p) for p in quad], normal, TIMBER_SLOT, [uv.round(p, name) for p in quad])
    for end, x in ((1, x_hi), (-1, x_lo)):
        quad = [(x, 0, -w / 2), (x, 0, w / 2), (x, h, w / 2), (x, h, -w / 2)]
        normal = (float(end), 0.0, 0.0)
        mesh.face([mesh.vertex(p) for p in quad], normal, FRACTURE_SLOT if end in ends else TIMBER_SLOT,
                  [uv.across(p, normal) for p in quad])


def _splinter(mesh, uv, w, h, x_root, end, s):
    """One splinter's tapered prism from its root cell to its tip; returns its axis' two ends, its capsule radius
    and its thickness at mid-length."""
    z0, z1, y0, y1 = s["cell"]
    x_tip = x_root + end * s["length"]
    (tz, ty), (hz, hy) = s["tip"], s["tip_half"]
    root = {(a, b): (x_root, y1 if b > 0 else y0, z1 if a > 0 else z0) for a in (-1, 1) for b in (-1, 1)}
    tip = {(a, b): (x_tip, ty + b * hy, tz + a * hz) for a in (-1, 1) for b in (-1, 1)}
    inside = tuple(sum(p[k] for p in list(root.values()) + list(tip.values())) / 8 for k in range(3))
    outer = {(0, -1): ("under", y0 <= 1e-12), (0, 1): ("top", y1 >= h - 1e-12),
             (-1, 0): ("nz", z0 <= -w / 2 + 1e-12), (1, 0): ("pz", z1 >= w / 2 - 1e-12)}
    for (a, b), (name, on_surface) in outer.items():
        keys = [(-1, b), (1, b)] if a == 0 else [(a, -1), (a, 1)]
        quad = [root[keys[0]], root[keys[1]], tip[keys[1]], tip[keys[0]]]
        normal = _outward(quad, inside)
        uvs = [uv.round(p, name) for p in quad] if on_surface else [uv.across(p, normal) for p in quad]
        mesh.face([mesh.vertex(p) for p in quad], normal, TIMBER_SLOT if on_surface else FRACTURE_SLOT, uvs)
    cap = [tip[(-1, -1)], tip[(1, -1)], tip[(1, 1)], tip[(-1, 1)]]
    normal = (float(end), 0.0, 0.0)
    mesh.face([mesh.vertex(p) for p in cap], normal, FRACTURE_SLOT, [uv.across(p, normal) for p in cap])
    mid = (((z1 - z0) / 2 + hz) / 2, ((y1 - y0) / 2 + hy) / 2)
    radius = math.sqrt((mid[0] ** 2 + mid[1] ** 2) / 2)
    return (x_root, (y0 + y1) / 2, (z0 + z1) / 2), (x_tip, ty, tz), radius, 2 * min(mid)


def _envelope(mesh, uv, w, h, x_root, end, length):
    """LOD1's stand-in for a broken end's splinters: one block from the section to a tip ENVELOPE_TIP of it."""
    x_tip = x_root + end * length
    hz, hy = w / 2 * ENVELOPE_TIP, h / 2 * ENVELOPE_TIP
    root = {(a, b): (x_root, h if b > 0 else 0.0, a * w / 2) for a in (-1, 1) for b in (-1, 1)}
    tip = {(a, b): (x_tip, h / 2 + b * hy, a * hz) for a in (-1, 1) for b in (-1, 1)}
    inside = ((x_root + x_tip) / 2, h / 2, 0.0)
    for (a, b), name in (((0, -1), "under"), ((0, 1), "top"), ((-1, 0), "nz"), ((1, 0), "pz")):
        keys = [(-1, b), (1, b)] if a == 0 else [(a, -1), (a, 1)]
        quad = [root[keys[0]], root[keys[1]], tip[keys[1]], tip[keys[0]]]
        mesh.face([mesh.vertex(p) for p in quad], _outward(quad, inside), TIMBER_SLOT, [uv.round(p, name) for p in quad])
    cap = [tip[(-1, -1)], tip[(1, -1)], tip[(1, 1)], tip[(-1, 1)]]
    normal = (float(end), 0.0, 0.0)
    mesh.face([mesh.vertex(p) for p in cap], normal, FRACTURE_SLOT, [uv.across(p, normal) for p in cap])


def _rod(mesh, a, b, caps):
    """A nail's shank from a to b, NAIL_SIDES round at NAIL_R_M, capped at the ends `caps` says (start, end)."""
    d = [b[k] - a[k] for k in range(3)]
    length = math.sqrt(sum(c * c for c in d))
    d = [c / length for c in d]
    helper = (1.0, 0.0, 0.0) if abs(d[0]) < 0.9 else (0.0, 0.0, 1.0)
    e1 = _unit(_cross(d, helper))
    e2 = _cross(d, e1)
    rings = []
    for centre in (a, b):
        rings.append([tuple(centre[k] + NAIL_R_M * (math.cos(t) * e1[k] + math.sin(t) * e2[k]) for k in range(3))
                      for t in (2 * math.pi * j / NAIL_SIDES for j in range(NAIL_SIDES))])
    arc = 2 * math.pi * NAIL_R_M / NAIL_SIDES
    for j in range(NAIL_SIDES):
        k = (j + 1) % NAIL_SIDES
        quad = [rings[0][j], rings[0][k], rings[1][k], rings[1][j]]
        mid = [sum(p[c] for p in quad) / 4 for c in range(3)]
        axis = [(a[c] + b[c]) / 2 for c in range(3)]
        normal = tuple(mid[c] - axis[c] for c in range(3))
        mesh.face([mesh.vertex(p) for p in quad], normal, NAIL_SLOT,
                  [(j * arc, 0.0), ((j + 1) * arc, 0.0), ((j + 1) * arc, length), (j * arc, length)])
    for ring, flag, sign in ((rings[0], caps[0], -1), (rings[1], caps[1], 1)):
        if flag:
            mesh.face([mesh.vertex(p) for p in ring], tuple(sign * c for c in d), NAIL_SLOT,
                      [(NAIL_R_M * math.cos(2 * math.pi * j / NAIL_SIDES), NAIL_R_M * math.sin(2 * math.pi * j / NAIL_SIDES))
                       for j in range(NAIL_SIDES)])


def _outward(quad, inside):
    """The unit normal of a planar quad, turned away from a point inside the solid."""
    n = [0.0, 0.0, 0.0]
    for p, q in zip(quad, quad[1:] + quad[:1]):
        n[0] += (p[1] - q[1]) * (p[2] + q[2])
        n[1] += (p[2] - q[2]) * (p[0] + q[0])
        n[2] += (p[0] - q[0]) * (p[1] + q[1])
    centre = [sum(p[k] for p in quad) / 4 for k in range(3)]
    if sum(n[k] * (centre[k] - inside[k]) for k in range(3)) < 0:
        n = [-c for c in n]
    return tuple(_unit(n))


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _unit(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v)
