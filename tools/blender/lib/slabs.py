"""Collision from a mesh we did not generate (w4b-farmland-ruins, design 2-4): a vendor mesh's faces become boxes that
follow them, in plain Python so the tests run without Blender.

The faces are the input's triangles, each with its outward unit normal and the part it belongs to (a rule index the
caller resolved: part name, physics material, layer, sheet thickness). Every number is asset space (lib.frame),
metres.

- Families: triangles whose normals lie within `angle_tol_deg` of one axis up to sign share a family; a triangle
  within `axis_angle_deg` of X, Y or Z (a sagging floor), or no deeper than `axis_dev_m` along it (a narrow
  bevel), joins that axis's family; a wider tilted face (a debris sheet lying askew) keeps its own exact plane. Any other family's axis is the area-weighted mean of its members. A family's basis is (u, v, n)
  with u x v = n: u horizontal in the plane (X for a horizontal plane), v = n x u.
- Planes: within a family, a face's offset is n . centroid with the family's n; offsets within `plane_tol_m` of a
  plane's first member join it, front faces (normal along +n) and back faces (along -n) apart.
- Raster: each plane is sampled on the family's grid of `raster_m` pixels, aligned to multiples of `raster_m` in
  (u, v), so two meshes sharing a family raster alike; a pixel is the face's when the face overlaps it (a face's
  edge grows by at most a pixel; a sliver thinner than a pixel is not lost). Each row
  is a Python int, one bit per pixel. A pixel belongs to the first part (in rule order) that covers it.
- Slabs: outward normals make solid lie between a back face and the next front face along +n. Per pixel, each back
  plane (ascending offset) pairs with the nearest front plane within `max_thickness_m` covering the same pixel; a
  pixel left unpaired is a sheet: a slab of its part's `sheet_m` behind the face. A slab between two parts with
  different materials is layered: a part with `layer_m` (a lining: plaster on a log wall) takes that much from its
  face, the other part the rest. With the air round the mesh (lib.air), a pair whose space is no closed cavity of its
  own (two boards, the outside reaching between them over the break) is two sheets instead, open between them; never
  a pair with a lining's face (w9-village-physics 2.15): plaster or render is the face of its wall's core, never a
  free skin, so the mesh's two faces of a broken wall bound its core. A lining's unpaired pixel is backed the same
  way: to its wall's other face (the nearest plane across its solid side within `max_thickness_m` holding a face of
  its part's name: the wall the mesh draws there elsewhere), or, with none, to its part's `sheet_m` on its `core`
  (a ceiling's plaster on its boards).
- Cover: families run in order of area. Before an axis-aligned family pairs, the pixels of its planes that lie on or
  inside a box already made by an axis-aligned family (within `cover_tol_m`) are dropped: a sill's top is the wall's
  box, not a second slab.
- Boxes: each slab region becomes rectangles (by rows or by columns, whichever needs fewer): runs per row, rows
  merged while every run end stays within
  `merge_tol_m` either way of the group's (a diagonal edge, a gable's or a hip's, steps; the rectangle takes the
  mid of each end's spread, so an end lies at most `merge_tol_m` off the face). Rectangles under `min_box_m` both ways
  are dropped (raster noise). An axis-aligned family's box has the identity rotation.
"""
import math

AXES = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))


class Part:
    """A rule's physics: its name, physics material, lining layer (None or metres), sheet thickness, landable flag,
    snag edge radius, its own rectangle merge tolerance (None: the settings') and, for a lining, its core (a part
    index, or None): what backs it where the mesh draws no other face of its wall."""

    def __init__(self, name, physics, layer_m=None, sheet_m=0.02, landable=False, edge_radius_m=0.002, merge_tol_m=None,
                 core=None):
        self.name, self.physics, self.layer_m, self.sheet_m = name, physics, layer_m, sheet_m
        self.landable, self.edge_radius_m, self.merge_tol_m = landable, edge_radius_m, merge_tol_m
        self.core = core


class Family:
    def __init__(self, n):
        self.n = n
        if abs(n[1]) > 0.999:
            u = (1.0, 0.0, 0.0)
        else:
            u = _normalize(_cross((0.0, 1.0, 0.0), n))
        self.u, self.v = u, _cross(n, u)
        self.axis = next((k for k in range(3) if abs(n[k]) > 1 - 1e-9), None)
        self.key = tuple(round(c, 6) for c in n)  # a seeded family keeps its seed's key
        self.triangles = []  # (tri, part, side, offset, area)
        self.area = 0.0
        self.planes = []


class Plane:
    def __init__(self, side, offset):
        self.side, self.offset, self.weight = side, offset, 0.0
        self.parts = {}  # part index -> {row: bits}
        self.rem = {}    # row -> bits, the pixels not yet in a slab


def families(triangles, angle_tol_deg, seed=None, axis_dev_m=0.0, axis_angle_deg=None):
    """The families of `triangles` [(tri, normal, part)], largest first. A triangle within axis_angle_deg of X, Y or
    Z (angle_tol_deg when None), or whose corners all lie within axis_dev_m of the axis plane through its centroid
    (a lap board's narrow bevel),
    joins that axis's family: it is the wall's face, not a family of its own whose sheet would leave a slit in the
    wall. Every other triangle joins its
    nearest other family within angle_tol_deg. Unseeded, those families open greedily (each triangle, largest first,
    farther than angle_tol_deg from every family opens its own on its normal), each family's normal becomes its
    members' area-weighted mean, and then every triangle joins as above. `seed`, a list of family normals, fixes the
    families (a ruin takes its intact house's, so both raster alike and a triangle joins the same family in both); a
    triangle near none opens its own."""
    cos_tol = math.cos(math.radians(angle_tol_deg))
    cos_axis = math.cos(math.radians(axis_angle_deg if axis_angle_deg is not None else angle_tol_deg))
    pending = []
    for tri, normal, part in triangles:
        area = _area(tri)
        if area >= 1e-10:
            pending.append((tri, tuple(normal), part, area))
    pending.sort(key=lambda t: -t[3])
    normals = list(seed) if seed is not None else list(AXES)
    axes = [k for k, n in enumerate(normals) if any(abs(n[c]) > 1 - 1e-9 for c in range(3))]

    def pick(normals, n, tri):
        k = axes[_nearest([normals[i] for i in axes], n)]
        a = normals[k]
        if abs(_dot(a, n)) >= cos_axis:
            return k
        c = _centroid(tri)
        if max(abs(_dot(_sub(p, c), a)) for p in tri) <= axis_dev_m:
            return k
        others = [i for i in range(len(normals)) if i not in axes]
        if others:
            k = others[_nearest([normals[i] for i in others], n)]
            if abs(_dot(normals[k], n)) >= cos_tol:
                return k
        return None

    if seed is None:
        members = [[] for _ in normals]
        for t in pending:
            k = pick(normals, t[1], t[0])
            if k is None:
                normals.append(t[1] if _canonical(t[1]) else tuple(-c for c in t[1]))
                members.append([])
                k = len(normals) - 1
            members[k].append(t)
        for k, n in enumerate(normals):
            if k not in axes and members[k]:
                acc = [0.0, 0.0, 0.0]
                for _, nn, _, area in members[k]:
                    s = 1 if _dot(nn, n) > 0 else -1
                    acc = [acc[c] + s * area * nn[c] for c in range(3)]
                mean = _normalize(acc)
                normals[k] = mean if _dot(mean, n) > 0 else tuple(-c for c in mean)
    fams = [Family(n) for n in normals]
    for tri, normal, part, area in pending:
        k = pick([f.n for f in fams], normal, tri)
        if k is None:
            fams.append(Family(normal if _canonical(normal) else tuple(-c for c in normal)))
            k = len(fams) - 1
        fam = fams[k]
        fam.triangles.append((tri, part, 1 if _dot(fam.n, normal) > 0 else -1, None, area))
        fam.area += area
    out = [f for f in fams if f.triangles]
    out.sort(key=lambda f: (-f.area, f.key))
    return out


def _nearest(normals, n):
    return max(range(len(normals)), key=lambda k: (abs(_dot(normals[k], n)), -k))


def decompose(triangles, parts, settings, seed_families=None, air=None, walls=None):
    """Boxes for `triangles` [(tri, outward normal, part index)]: a dict with "boxes" [{"center", "size",
    "rotation", "part", "family", "lo", "hi"}], "families" (the Family objects, rastered) and "regions" (per family,
    the slab regions {(lo, hi, part): {row: bits}} before rectangles, which the wind volumes and gaps read: every
    pair solid, the open ones too, so a wall's band spans both its skins). With `air` (lib.air.Air), a pair whose
    space is no closed cavity of its own is two skins, each a sheet of its part behind its face, open between them.
    `walls` ({family key: [(side, offset, part indices)]}, another decomposition's planes: the intact house a ruin was
    cut from) gives a lining's lone face its wall's other face where this mesh has lost it whole."""
    r = settings["raster_m"]
    fams = families(triangles, settings["angle_tol_deg"], seed_families, settings.get("axis_dev_m", 0.0),
                    settings.get("axis_angle_deg"))
    boxes, regions = [], []
    axis_boxes = []
    for fi, fam in enumerate(fams):
        _planes(fam, settings["plane_tol_m"])
        for plane in fam.planes:
            for (tri, part, side, offset, area) in plane.members:
                _raster(plane, fam, tri, part, r)
        _resolve_overlaps(fam, parts)
        if fam.axis is not None:
            _drop_covered(fam, axis_boxes, r, settings["cover_tol_m"])
        slabs, shape = _pair(fam, parts, settings["max_thickness_m"], air, r, (walls or {}).get(fam.key, ()))
        regions.append(shape)
        for (lo, hi, pi), rows in slabs.items():
            tol = parts[pi].merge_tol_m if parts[pi].merge_tol_m is not None else settings["merge_tol_m"]
            for rect in rectangles_either_way(rows, tol / r, settings["min_box_m"] / r):
                box = _box(fam, fi, rect, lo, hi, pi, r)
                boxes.append(box)
                if fam.axis is not None:
                    axis_boxes.append(box)
    return {"boxes": boxes, "families": fams, "regions": regions}


def _planes(fam, tol):
    members = []
    for tri, part, side, _, area in fam.triangles:
        c = _centroid(tri)
        members.append((tri, part, side, _dot(fam.n, c), area))
    fam.planes = []
    for side in (-1, 1):
        group = sorted((m for m in members if m[2] == side), key=lambda m: m[3])
        current = None
        for m in group:
            if current is None or m[3] - current.first > tol:
                current = Plane(side, m[3])
                current.first, current.members, current.sum = m[3], [], 0.0
                fam.planes.append(current)
            current.members.append(m)
            current.sum += m[3] * m[4]
            current.weight += m[4]
        for p in fam.planes:
            if p.side == side:
                p.offset = p.sum / p.weight
    fam.planes.sort(key=lambda p: p.offset)


def _raster(plane, fam, tri, part, r):
    """Sets, in the plane's rows of `part`, every pixel the triangle projected on (u, v) overlaps (conservative: a
    sliver or a spike's tip narrower than a pixel still marks the pixels it crosses, so no thin face goes without
    collision; an edge lying on a pixel boundary marks no pixel past it)."""
    eps = 1e-6
    pts = [(_dot(p, fam.u), _dot(p, fam.v)) for p in tri]
    vmin, vmax = min(p[1] for p in pts), max(p[1] for p in pts)
    j0, j1 = math.floor(vmin / r + eps), math.ceil(vmax / r - eps) - 1
    rows = plane.parts.setdefault(part, {})
    for j in range(j0, max(j0, j1) + 1):
        b0, b1 = j * r, (j + 1) * r
        xs = [a for a, b in pts if b0 <= b <= b1]
        for k in range(3):
            (a0, c0), (a1, c1) = pts[k], pts[(k + 1) % 3]
            for edge in (b0, b1):
                if (c0 - edge) * (c1 - edge) < 0:
                    xs.append(a0 + (edge - c0) * (a1 - a0) / (c1 - c0))
        if not xs:
            continue
        i0, i1 = math.floor(min(xs) / r + eps), math.ceil(max(xs) / r - eps) - 1
        if i1 < i0:
            i1 = i0
        rows[j] = rows.get(j, 0) | _span(i0, i1)


def _resolve_overlaps(fam, parts):
    """A pixel covered by two parts of one plane goes to the first part in rule order; `rem` is their union."""
    for plane in fam.planes:
        taken = {}
        for pi in sorted(plane.parts):
            rows = plane.parts[pi]
            for j in list(rows):
                bits = rows[j] & ~taken.get(j, 0)
                if bits:
                    rows[j] = bits
                    taken[j] = taken.get(j, 0) | bits
                else:
                    del rows[j]
        plane.rem = dict(taken)


def _drop_covered(fam, boxes, r, tol):
    """Drops the pixels of the family's planes that lie on or inside an axis-aligned box already made."""
    a, b = _axis_index(fam.u), _axis_index(fam.v)
    for plane in fam.planes:
        s = plane.offset
        for box in boxes:
            lo, hi = box["aabb"]
            if not (lo[fam.axis] - tol <= s * fam.n[fam.axis] <= hi[fam.axis] + tol):
                continue
            ua, ub = _range_on(lo, hi, fam.u, a)
            va, vb = _range_on(lo, hi, fam.v, b)
            i0, i1 = math.ceil((ua - tol) / r - 0.5), math.floor((ub + tol) / r - 0.5)
            j0, j1 = math.ceil((va - tol) / r - 0.5), math.floor((vb + tol) / r - 0.5)
            if i1 < i0 or j1 < j0:
                continue
            mask = ~_span(i0, i1)
            for j in range(j0, j1 + 1):
                if j in plane.rem:
                    plane.rem[j] &= mask
                    for rows in plane.parts.values():
                        if j in rows:
                            rows[j] &= mask


def _pair(fam, parts, t_max, air=None, r=None, walls=()):
    """The slab regions {(lo, hi, part): rows} of a family, for the boxes and for the shape: back-front pairs per
    pixel, layered by material, then the unpaired pixels as sheets behind their face. With `air`, a pair that leaves
    at least an air cell between the two faces' sheets is, at the pixels where the space between them is no closed
    cavity of its own (lib.air.Air.open_bits), a sheet behind each face for the boxes, and still the pair for the
    shape; a narrower one stays whole (a door leaf's open end: what is left between its sheets is no space the air
    resolves). A pixel with a lining on either face never opens (a lining is no free skin), nor counts in the sheets
    the air must leave room for. A lining's unpaired pixel is backed (_backing) for the boxes; the shape keeps its
    sheet."""
    slabs, shape = {}, {}
    backs = [p for p in fam.planes if p.side < 0]
    fronts = [p for p in fam.planes if p.side > 0]

    def skin(plane):
        return max((parts[pi].sheet_m for pi in plane.parts if not parts[pi].layer_m), default=0.0)

    for B in backs:
        for F in fronts:
            if F.offset <= B.offset:
                continue
            if F.offset - B.offset > t_max:
                break
            hollow = air is not None and F.offset - B.offset >= skin(B) + skin(F) + air.cell
            for j in list(B.rem):
                m = B.rem[j] & F.rem.get(j, 0)
                if not m:
                    continue
                B.rem[j] &= ~m
                F.rem[j] &= ~m
                opened = air.open_bits(fam, j, m, B.offset, F.offset, r) if hollow else 0
                if opened:
                    for P in (B, F):
                        for pi, rows_p in P.parts.items():
                            if parts[pi].layer_m:
                                opened &= ~rows_p.get(j, 0)
                for pb, rows_b in B.parts.items():
                    mb = m & rows_b.get(j, 0)
                    if not mb:
                        continue
                    if mb & opened:
                        _add(slabs, (B.offset, B.offset + parts[pb].sheet_m, pb), j, mb & opened)
                    for pf, rows_f in F.parts.items():
                        mbf = mb & rows_f.get(j, 0)
                        if mbf:
                            for lo, hi, pi in _layers(B.offset, F.offset, pb, pf, parts):
                                _add(shape, (lo, hi, pi), j, mbf)
                                if mbf & ~opened:
                                    _add(slabs, (lo, hi, pi), j, mbf & ~opened)
                for pf, rows_f in F.parts.items():
                    mf = m & opened & rows_f.get(j, 0)
                    if mf:
                        _add(slabs, (F.offset - parts[pf].sheet_m, F.offset, pf), j, mf)
    for P in fam.planes:
        for pi, rows in P.parts.items():
            t = parts[pi].sheet_m
            lo, hi = (P.offset, P.offset + t) if P.side < 0 else (P.offset - t, P.offset)
            boxes = (parts[pi].layer_m and _backing(fam, P, pi, parts, t_max, walls)) or [(lo, hi, pi)]
            for j, bits in rows.items():
                m = bits & P.rem.get(j, 0)
                if m:
                    for key in boxes:
                        _add(slabs, key, j, m)
                    _add(shape, (lo, hi, pi), j, m)
    return slabs, shape


def _backing(fam, P, pi, parts, t_max, walls=()):
    """The layers behind an unpaired pixel of lining `pi` on plane P: from P to its wall's other face, the nearest
    plane across P's solid side, farther than the part's sheet_m and within t_max, holding a face of the same part name
    (the thickness the mesh draws for that wall where both faces stand), first among the family's own planes, then
    among `walls` [(side, offset, part indices)] (the intact house's, where the ruin lost that face whole), layered
    with that plane's part (its core where it has one, else the lining's own: a partition plastered both sides is one
    slab, as its pairs are); with no such plane, the part's sheet_m on its core; None when it has neither (a plain
    sheet)."""
    name, back, t = parts[pi].name, P.side < 0, parts[pi].sheet_m
    sign = 1 if back else -1
    for planes in ([(Q.side, Q.offset, Q.parts) for Q in fam.planes], walls):
        across = [(offset, ks) for side, offset, ks in planes if side == -P.side
                  and t < (offset - P.offset) * sign <= t_max and any(parts[k].name == name for k in ks)]
        if across:
            offset, ks = min(across, key=lambda a: abs(a[0] - P.offset))
            same = sorted(k for k in ks if parts[k].name == name)
            q = next((k for k in same if parts[k].physics != parts[pi].physics), same[0])
            return _layers(P.offset, offset, pi, q, parts) if back else _layers(offset, P.offset, q, pi, parts)
    core = parts[pi].core
    if core is None or t <= parts[pi].layer_m:
        return None
    return _layers(P.offset, P.offset + t, pi, core, parts) if back else _layers(P.offset - t, P.offset, core, pi, parts)


def _layers(lo, hi, pb, pf, parts):
    if pb == pf or parts[pb].physics == parts[pf].physics and parts[pb].name == parts[pf].name:
        return [(lo, hi, pb)]
    lb, lf = parts[pb].layer_m, parts[pf].layer_m
    t = hi - lo
    if lb and not lf:
        cut = lo + min(lb, t)
    elif lf and not lb:
        cut = hi - min(lf, t)
    else:
        cut = lo + t / 2
    out = []
    if cut > lo:
        out.append((lo, cut, pb))
    if hi > cut:
        out.append((cut, hi, pf))
    return out


def _add(slabs, key, j, bits):
    rows = slabs.setdefault(key, {})
    rows[j] = rows.get(j, 0) | bits


def rectangles(rows, tol_px, min_px):
    """Rectangles (i0, i1, j0, j1), pixel indices inclusive, covering `rows` {row: bits}: each row's runs, merged
    down the rows while both ends stay within tol_px of the group's extremes; each rectangle takes the mid of its
    ends. A rectangle under min_px both ways is raster noise and dropped; one short only one way is kept (a step of
    a diagonal edge's staircase is a row or two tall), and lib.slabs.prune drops it later if its neighbours cover it."""
    out, active = [], []  # active: [lo_min, lo_max, hi_min, hi_max, j0, j1]
    previous = None
    for j in sorted(rows):
        row_runs = runs(rows[j])
        if previous is None or j != previous + 1:
            out += active
            active = []
        nxt, used = [], set()
        for lo, hi in row_runs:
            hit = None
            for k, g in enumerate(active):
                if k in used:
                    continue
                if (max(g[1], lo) - min(g[0], lo) <= 2 * tol_px and max(g[3], hi) - min(g[2], hi) <= 2 * tol_px):
                    hit = k
                    break
            if hit is None:
                nxt.append([lo, lo, hi, hi, j, j])
            else:
                g = active[hit]
                used.add(hit)
                nxt.append([min(g[0], lo), max(g[1], lo), min(g[2], hi), max(g[3], hi), g[4], j])
        out += [g for k, g in enumerate(active) if k not in used]
        active = nxt
        previous = j
    out += active
    rects = []
    for lo_min, lo_max, hi_min, hi_max, j0, j1 in out:
        i0, i1 = (lo_min + lo_max) / 2.0, (hi_min + hi_max) / 2.0
        if i1 - i0 + 1 >= min_px or j1 - j0 + 1 >= min_px:
            rects.append((i0, i1, j0, j1))
    rects.sort(key=lambda q: (q[2], q[0]))
    return rects


def rectangles_either_way(rows, tol_px, min_px):
    """rectangles() of the region by rows and by columns, whichever needs fewer (by rows on a tie): a long run whose
    one end steps along a diagonal (a wall's broken side) is one rectangle and a staircase of narrow ones by columns,
    where by rows it is a stack of full-width strips."""
    by_rows = rectangles(rows, tol_px, min_px)
    columns = {}
    for j, bits in rows.items():
        bit = span(j, j)
        for i0, i1 in runs(bits):
            for i in range(i0, i1 + 1):
                columns[i] = columns.get(i, 0) | bit
    by_columns = rectangles(columns, tol_px, min_px)
    if len(by_columns) < len(by_rows):
        return sorted(((i0, i1, j0, j1) for j0, j1, i0, i1 in by_columns), key=lambda q: (q[2], q[0]))
    return by_rows


def _runs(bits):
    runs, i = [], 0
    while bits:
        if bits & 1:
            length = (~bits & (bits + 1)).bit_length() - 1
            runs.append((i, i + length - 1))
            bits >>= length
            i += length
        else:
            skip = (bits & -bits).bit_length() - 1
            bits >>= skip
            i += skip
    return runs


def _box(fam, fi, rect, lo, hi, pi, r):
    i0, i1, j0, j1 = rect
    a0, a1 = i0 * r, (i1 + 1) * r
    b0, b1 = j0 * r, (j1 + 1) * r
    ac, bc, sc = (a0 + a1) / 2, (b0 + b1) / 2, (lo + hi) / 2
    center = tuple(ac * fam.u[k] + bc * fam.v[k] + sc * fam.n[k] for k in range(3))
    size_uvn = (a1 - a0, b1 - b0, hi - lo)
    if fam.axis is not None:
        size = [0.0, 0.0, 0.0]
        for axis_vec, length in zip((fam.u, fam.v, fam.n), size_uvn):
            size[_axis_index(axis_vec)] = length
        rotation = (0.0, 0.0, 0.0, 1.0)
        half = [s / 2 for s in size]
        aabb = (tuple(center[k] - half[k] for k in range(3)), tuple(center[k] + half[k] for k in range(3)))
    else:
        size = list(size_uvn)
        rotation = quaternion_from_basis(fam.u, fam.v, fam.n)
        aabb = None
    return {"center": center, "size": tuple(size), "rotation": rotation, "part": pi, "family": fi,
            "lo": lo, "hi": hi, "aabb": aabb, "rect": rect, "normal": fam.n}


def quaternion_from_basis(x, y, z):
    """The unit quaternion [x, y, z, w] whose rotation takes the local axes to the columns x, y, z (right-handed)."""
    m00, m01, m02 = x[0], y[0], z[0]
    m10, m11, m12 = x[1], y[1], z[1]
    m20, m21, m22 = x[2], y[2], z[2]
    trace = m00 + m11 + m22
    if trace > 0:
        s = math.sqrt(trace + 1.0) * 2
        q = ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s)
    elif m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        q = (0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    elif m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        q = ((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s)
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2
        q = ((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s)
    length = math.sqrt(sum(c * c for c in q))
    return tuple(c / length for c in q)


def prune(boxes, material_of, tol, step=0.1, cell=0.5):
    """The boxes left once every box whose surface lies within `tol` of the union of the others of its own
    material is dropped, smallest first (the raster's slivers beside a wall, a bevel's sliver on a sill): its surface
    is sampled on a lattice of at most `step` spacing, and each sample must lie inside another kept box of the same
    material grown by `tol`. A lining (another material) is never dropped for its core. A box that covers a dropped
    one is kept from then on, so a chain of slivers (a spike's staircase) cannot erode away step by step. A box that
    knows its slab's normal ("normal", as decompose makes them) is dropped only when, besides, the line along that
    normal through each point of its footprint (its middle plane, sampled the same way) meets another kept box of its
    material within `tol` past its faces: a strip of a sheet between two others that merely touch it would leave a
    slit through the sheet, which a fiber passes. Returns the kept boxes in their original order."""
    order = sorted(range(len(boxes)), key=lambda i: _volume(boxes[i]))
    frames = [_frame(b) for b in boxes]
    grid = {}
    for i, f in enumerate(frames):
        for key in _cells(f["aabb"], cell):
            grid.setdefault(key, []).append(i)
    removed, kept_for = set(), set()
    for i in order:
        if i in kept_for:
            continue
        material = material_of(boxes[i])
        f = frames[i]
        candidates = set()
        for key in _cells(f["aabb"], cell):
            candidates.update(grid.get(key, ()))
        candidates = sorted(k for k in candidates if k != i and k not in removed and material_of(boxes[k]) == material)
        if not candidates:
            continue
        cover = set()
        for p in _surface_samples(f, step):
            k = next((k for k in candidates if _inside(p, frames[k], tol)), None)
            if k is None:
                break
            cover.add(k)
        else:
            n = boxes[i].get("normal")
            if n is not None:
                reach = tol + _half_along(f, n)
                for p in _footprint_samples(f, n, step):
                    k = next((k for k in candidates if _segment_meets(p, n, reach, frames[k])), None)
                    if k is None:
                        break
                    cover.add(k)
                else:
                    n = None
                if n is not None:
                    continue
            removed.add(i)
            kept_for |= cover
    return [b for i, b in enumerate(boxes) if i not in removed]


def _half_along(f, n):
    return sum(abs(_dot(f["axes"][a], n)) * f["half"][a] for a in range(3))


def _footprint_samples(f, n, step):
    """The box's middle plane across `n` (one of its axes), sampled on a lattice of at most `step`."""
    a = max(range(3), key=lambda k: abs(_dot(f["axes"][k], n)))
    others = [k for k in range(3) if k != a]
    grids = []
    for k in others:
        h = f["half"][k]
        count = max(3, math.ceil(2 * h / step) + 1)
        grids.append([-h + 2 * h * t / (count - 1) for t in range(count)])
    for x in grids[0]:
        for y in grids[1]:
            yield tuple(f["c"][c] + x * f["axes"][others[0]][c] + y * f["axes"][others[1]][c] for c in range(3))


def _segment_meets(p, n, reach, f):
    """Whether the segment p + t n, |t| <= reach, meets box f (grown by a micrometre)."""
    d = _sub(p, f["c"])
    t0, t1 = -reach, reach
    for a in range(3):
        da, na, h = _dot(d, f["axes"][a]), _dot(n, f["axes"][a]), f["half"][a] + 1e-6
        if abs(na) < 1e-12:
            if abs(da) > h:
                return False
            continue
        lo, hi = (-h - da) / na, (h - da) / na
        if lo > hi:
            lo, hi = hi, lo
        t0, t1 = max(t0, lo), min(t1, hi)
        if t0 > t1:
            return False
    return True


def _volume(box):
    return box["size"][0] * box["size"][1] * box["size"][2]


def _frame(box):
    q = box["rotation"]
    axes = [_qrot(q, a) for a in AXES]
    half = [s / 2 for s in box["size"]]
    c = box["center"]
    extent = [sum(abs(axes[a][k]) * half[a] for a in range(3)) for k in range(3)]
    return {"c": c, "axes": axes, "half": half,
            "aabb": (tuple(c[k] - extent[k] for k in range(3)), tuple(c[k] + extent[k] for k in range(3)))}


def _cells(aabb, cell):
    lo, hi = aabb
    rng = [range(math.floor(lo[k] / cell), math.floor(hi[k] / cell) + 1) for k in range(3)]
    return [(x, y, z) for x in rng[0] for y in rng[1] for z in rng[2]]


def _inside(p, f, tol):
    d = _sub(p, f["c"])
    return all(abs(_dot(d, f["axes"][a])) <= f["half"][a] + tol for a in range(3))


def _surface_samples(f, step):
    counts = [max(3, math.ceil(2 * h / step) + 1) for h in f["half"]]
    grids = [[-h + 2 * h * t / (n - 1) for t in range(n)] for h, n in zip(f["half"], counts)]
    for x in grids[0]:
        for y in grids[1]:
            for z in grids[2]:
                on_face = (x in (grids[0][0], grids[0][-1]) or y in (grids[1][0], grids[1][-1])
                           or z in (grids[2][0], grids[2][-1]))
                if on_face:
                    yield tuple(f["c"][k] + x * f["axes"][0][k] + y * f["axes"][1][k] + z * f["axes"][2][k]
                                for k in range(3))


def _qrot(q, v):
    x, y, z, w = q
    t = (2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0]))
    return (v[0] + w * t[0] + (y * t[2] - z * t[1]), v[1] + w * t[1] + (z * t[0] - x * t[2]),
            v[2] + w * t[2] + (x * t[1] - y * t[0]))


# ---------------------------------------------------------------------------------------------- raster helpers

def span(i0, i1):
    return _span(i0, i1)


def _span(i0, i1):
    """Bits i0..i1 inclusive; a negative index is shifted by the caller's origin, so rows here may hold negative
    pixel indices: Python ints shift fine to the left only, so pixel i is bit (i - BASE)."""
    return ((1 << (i1 - i0 + 1)) - 1) << (i0 - BASE)


BASE = -4096  # pixel index of bit 0: 40.96 m to the negative side at 1 cm, more than any asset's reach


def runs(bits):
    """The (i0, i1) pixel runs of a row, inclusive, in pixel indices."""
    return [(a + BASE, b + BASE) for a, b in _runs(bits)]


def _range_on(lo, hi, axis_vec, k):
    sign = axis_vec[k]
    return (lo[k], hi[k]) if sign > 0 else (-hi[k], -lo[k])


def _axis_index(vec):
    return max(range(3), key=lambda k: abs(vec[k]))


def _canonical(n):
    for c in n:
        if abs(c) > 1e-9:
            return c > 0
    return True


def _area(tri):
    c = _cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))
    return 0.5 * math.sqrt(_dot(c, c))


def _centroid(tri):
    return tuple(sum(p[k] for p in tri) / 3.0 for k in range(3))


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(_dot(v, v))
    return tuple(c / length for c in v) if length > 0 else (0.0, 0.0, 0.0)
