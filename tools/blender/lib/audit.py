"""The build-time collision audit (p1-pipeline-hardening, "Collision audit at build time"; physics review P1-2;
AST-05), in plain Python so the tests run without Blender.

For every collision primitive, the largest distance from its surface to the LOD0 mesh (an invisible wall), and for
the LOD0 mesh, the largest distance from its surface to the union of the primitives (a visible surface nothing
collides with), each within 1 mm of the true maximum. Beyond 50 mm the build fails, or beyond 20 mm within 0.3 m of
a gap polygon or a landable face (a landable primitive's faces whose normal is within 30 degrees of up). Each gap's
narrowest_m is checked against the diameter of the largest circle inscribed in its polygon, within 5 mm.

Primitives form a union: the part of a primitive's surface that lies inside another primitive is not a collision
surface (a mound sphere's lower half sunk in the ground slab, the slab's top under the mound), so it is not
measured (a compliant shell's piece, below, hides no other primitive: a stem inside the twig mass is still met);
nor is a seam where primitives only touch (a cell under SEAM_CELL_M whose corners all lie in other
primitives), nor a sliver thinner than the metadata's micrometre rounding (COVERED_M). An asset that stands in the
ground (`planted`, p1-l01-l03-pole-wires) has the terrain as one more such primitive, the half-space below y = 0:
a pole's capsule cap or a brace's foot buried there is no wall, and its mesh, never drawn below the ground, is not
asked to follow it. A landable shape with a soft top is landed on over its whole top, so it widens no 2 cm zone: its top is
held to the 5 cm tolerance everywhere, and the lines name those shapes. Soft means a reference foot (25 N on 15 mm)
sinks at least 25 mm into it (p1-s04-belt-straw, review-physics §2 and PR6): the surfaces of SOFT_SURFACES, or a
plain id of assets-src/physics_materials.json with stiffness_n_per_m of SOFT_STIFFNESS_N_PER_M or less
(soft_materials). On hard ground a 5 cm float would show, so every other landable top keeps the 2 cm zone.
Visual-only micro meshes (meta["micro"]) are not part of the LOD0 mesh the audit sees; the lines name each one
skipped.

Both distances are found by branch and bound over triangles: a cell's three vertices give exact values, a lower
bound; an upper bound comes from convexity; the cell with the largest upper bound is split in four until no cell
can beat the best value by PRECISION_M. The bounds are tight wherever the surfaces coincide, so the search refines
only around chamfers, edges and openings:
- primitive -> mesh: cells are the primitive's tessellation (curved shapes within CHORD_M); the distance to one
  convex mesh face is convex, so its largest value over a cell sits at a vertex. A cell that spans two faces
  sharing an edge is clipped at the fold's bisector plane and each part bounded by its own face.
- mesh -> primitives: cells are the mesh's triangles; each primitive's signed distance is exact. Outside a convex
  primitive the distance is convex (a vertex bound); inside it, the depth is bounded through the shape's own
  planes, centre or axis (exact for a sphere, capsule or cylinder, the nearest face for a box).

Compliant shells (p1-v13-shrub, "Compliant shells in the collision audit"; review-physics §2): the primitives of a
crown contact material (SHELL_MATERIALS) are the pieces of one shell over the LOD0 faces of the material slots
carrying that material. Those faces are not held to the other primitives; the shell is held to them both ways:
- (a) standoff: every point of a piece's surface above the ground and outside the material's other pieces lies within
  ENVELOPE_TOLERANCE_M of the nearest face of the slots, found by branch and bound over cells of the piece's own
  surface (a sphere or capsule: vertices on it, each cell's patch within its sag of its flat triangle, so within
  SHELL_PRECISION_M of the true largest standoff; any other shape flat at SHELL_CHORD_M), the faces' distances from
  a grid (FaceIndex);
- (b) every LOD0 vertex of a geometry slot lies inside a piece or within ENVELOPE_TOLERANCE_M outside the union;
- (c) every LOD0 vertex of a card slot, one whose visual material is an alpha-clipped recipe (`card_visuals`), lies
  inside a piece or within CARD_ALLOWANCE_M outside the union: a sprig hangs at most its own length off the twig mass,
  and its twig carries a snag record.
The report prints the piece count and kinds, each piece's largest standoff and where it is, and per slot the largest
outside distance. The other primitives and faces are audited as above against each other."""
import functools
import heapq
import json
import math
import os

TOLERANCE_M = 0.05          # AST-05: collision follows the visual within +-5 cm
SHELL_MATERIALS = ("twig", "foliage")
ENVELOPE_TOLERANCE_M = 0.1  # derived, p1-v13-shrub review-physics §2: 3 N at the twig row's 30 N/m before a leaf
CARD_ALLOWANCE_M = 0.3      # derived, review-physics §2: card_m 0.2 m plus the envelope tolerance
SHELL_START_M = 0.01        # the chord of a sphere's or capsule's first cells in the standoff search
SHELL_CHORD_M = 0.002       # tessellation error allowed on a shell piece of any other shape
SHELL_PRECISION_M = 0.0005
FACE_CELL_M = 0.05          # FaceIndex grid cell
FACE_RINGS = 3              # rings of cells searched before a far point falls back to every face
NEAR_TOLERANCE_M = 0.02     # +-2 cm around openings and landing surfaces
NEAR_RANGE_M = 0.3
PRECISION_M = 0.0001        # the search stops when no cell can beat the best by this; with CHORD_M, under 1 mm
CHORD_M = 0.0004            # tessellation error allowed on spheres, capsules and cylinders
NARROWEST_TOLERANCE_M = 0.005
LANDABLE_COS = math.cos(math.radians(30))
UP = (0.0, 1.0, 0.0)
FAR_M = 1000.0              # a cylinder's axis as a segment long enough to act as a line
PHYSICS_MATERIALS = os.path.join("assets-src", "physics_materials.json")  # lib.meta.PHYSICS_MATERIALS
# Soft tops (review-physics PR6): the reference foot sinks at least 25 mm (assumed: a round figure under the 3 cm the
# wider tolerance adds). Derived from docs/legacy/map-and-world/surfaces.json at that foot: belt_straw 30.1 mm,
# crater_spoil 50.5 mm, tilled 35.4 mm; every other surface sinks under 21 mm. The phase-3 compiler recomputes the
# list from the map's table. A plain material sinks 25 mm at k = 25 N / 0.025 m = 1000 N/m.
SOFT_SURFACES = ("surface:belt_straw", "surface:crater_spoil", "surface:tilled")
SOFT_STIFFNESS_N_PER_M = 1000.0
COVERED_M = 2e-6            # a point this close to another primitive's inside is on no collision surface: the
                            # metadata rounds every number to a micrometre (lib.meta.DECIMALS), so shapes meant to
                            # meet miss each other by up to that, and a sliver that thin is no surface
SEAM_CELL_M = 0.001         # a cell this small with every corner inside some other primitive is covered (a seam)
BOX_CELL_M = 0.25           # a box face is tessellated in cells of at most this, so the search never quarters a long
                            # thin wall (12 x 0.2 m) into slivers; a brick's faces stay one cell each


def soft_materials(root):
    """The material ids whose landable tops are soft (SOFT_SURFACES and every plain id of the physics table at
    SOFT_STIFFNESS_N_PER_M or less), from <root>/assets-src/physics_materials.json."""
    with open(os.path.join(root, PHYSICS_MATERIALS), encoding="utf-8") as f:
        rows = json.load(f)["materials"]
    return frozenset(SOFT_SURFACES) | {material for material, row in rows.items()
                                       if row.get("stiffness_n_per_m") is not None
                                       and row["stiffness_n_per_m"] <= SOFT_STIFFNESS_N_PER_M}


def run(meta, lod0, *, soft=frozenset(), card_visuals=(), planted=False):
    """(problems, report lines) of one variant: `meta` as lib.meta.assemble builds it, `lod0` its first
    lib.geometry.Mesh, `soft` the material ids with soft tops (soft_materials; none by default, so every landable
    top keeps the 2 cm zone), `card_visuals` the visual materials drawn as alpha-clipped cards
    (lib.catalog.card_visuals). Both are keywords: each is a set of names, easily swapped. `planted` says the asset
    stands in the ground (its generator's PLANTED): the terrain then covers the half-space below y = 0 as another
    primitive would, so a primitive's surface there (a log's capsule cap, a brace's buried foot) is no invisible
    wall (p1-l01-l03-pole-wires). Problems name the primitive or slot, the deviation and the tolerance; the lines
    carry the measured numbers for every primitive, the mesh, every shell piece and slot and every gap, success or
    not."""
    shapes = [_Shape(s) for s in meta["collision"]]
    shell_slots = {m["slot"] for m in meta["materials"] if m["physics"] in SHELL_MATERIALS}
    shells = [i for i, s in enumerate(meta["collision"]) if s["material"] in SHELL_MATERIALS]
    body = [i for i in range(len(shapes)) if i not in shells]
    faces = [face for face in _mesh_faces(lod0) if face.slot not in shell_slots]
    index = _FaceIndex(faces)
    zone = _zone(meta, shapes, soft)
    problems, lines = [], []
    if planted:
        lines.append("audit: planted, the ground covers every primitive below y = 0")
    for m in meta.get("micro", []):
        lines.append(f"audit: skipped micro mesh {m['name']} ({m['count']} {m['kind']})")
    soft_tops = [(i, s["material"]) for i, s in enumerate(meta["collision"]) if s["landable"] and s["material"] in soft]
    if soft_tops:
        lines.append(f"audit: soft tops {_runs(soft_tops)} are landed on over their whole top, "
                     f"held to {TOLERANCE_M * 1000:g} mm there")
    for i in body:
        shape = shapes[i]
        if not faces:
            problems.append(f"collision[{i}] {shape.kind}: no LOD0 face outside the shells' slots stands under it")
            continue
        others = [shapes[j] for j in body if j != i]
        cells = [tri[:3] for tri in shape.triangles]
        if planted:
            cells = _above_ground(cells)
            if not cells:
                lines.append(f"audit collision[{i}] {shape.kind}: wholly below the ground, covered")
                continue

        def value(v, others=others):
            d, which = index.nearest(v)
            return (0.0 if _covered(v, others) else d), which

        def bound(cell, which, others=others):
            return 0.0 if _cell_covered(cell, others) else _faces_bound(cell, which, faces)

        problems += _report(lines, f"collision[{i}] {shape.kind}", f"{shape.kind}->mesh", cells, zone, value, bound)
    if faces and not body:
        problems.append("mesh: no collision primitive stands for the LOD0 mesh")
    elif faces:
        cells = [tuple(face.points[i] for i in tri) for face in faces for tri in face.fan()]
        problems += _report(lines, "mesh", "mesh->collision", cells, zone,
                            lambda v: _nearest_shape(v, shapes, body), lambda cell, which: _shapes_bound(cell, which, shapes),
                            lambda which: f" nearest collision[{which}]")
    problems += _audit_shells(lines, meta, shapes, shells, lod0, set(card_visuals))
    for i, gap in enumerate(meta["gaps"]):
        diameter = 2 * _inscribed_radius(_flatten_polygon(gap["polygon"], gap["normal"]))
        text = f"gaps[{i}] '{gap['id']}': narrowest_m {gap['narrowest_m']:.4f}, largest inscribed circle {diameter:.4f}"
        if abs(gap["narrowest_m"] - diameter) > NARROWEST_TOLERANCE_M:
            problems.append(f"{text}: they differ by more than {NARROWEST_TOLERANCE_M * 1000:g} mm")
        lines.append(f"audit {text}, tolerance {NARROWEST_TOLERANCE_M * 1000:g} mm")
    return problems, lines


def _runs(indexed):
    """'collision[0] a, collision[1..9] b, ...': consecutive (index, material) pairs of one material as one run."""
    runs = []
    for i, material in indexed:
        if runs and runs[-1][1] == i - 1 and runs[-1][2] == material:
            runs[-1][1] = i
        else:
            runs.append([i, i, material])
    return ", ".join(f"collision[{a}{'' if a == b else f'..{b}'}] {material}" for a, b, material in runs)


def _report(lines, label, direction, cells, zone, value, bound, note=lambda which: ""):
    """Measures one direction, appends its report line and returns its problems."""
    distance, at, which = _directed_max(cells, value, bound)
    text = f"{direction} {distance * 1000:.3f} mm at {_fmt(at)}{note(which)}"
    line = f"{label}: {text}, tolerance {TOLERANCE_M * 1000:g} mm"
    problems = [] if distance <= TOLERANCE_M else [f"{label}: {text} exceeds the tolerance {TOLERANCE_M * 1000:g} mm"]
    if zone:
        distance, at, which = _directed_max(cells, value, bound, zone)
        if at is not None:
            text = f"near gaps/landable {distance * 1000:.3f} mm at {_fmt(at)}{note(which)}"
            line += f"; {text}, tolerance {NEAR_TOLERANCE_M * 1000:g} mm"
            if distance > NEAR_TOLERANCE_M:
                problems.append(f"{label}: {text} exceeds the tolerance {NEAR_TOLERANCE_M * 1000:g} mm")
    lines.append("audit " + line)
    return problems


def _audit_shells(lines, meta, shapes, shells, lod0, card_visuals):
    """The shell rule (module docstring), per crown contact material: (a) each piece's largest standoff, (b) and (c)
    each slot's largest outside distance. A piece no slot carries, and a slot with LOD0 faces that no piece stands
    for, are each a problem."""
    problems = []
    by_material = {m["physics"]: [] for m in meta["materials"] if m["physics"] in SHELL_MATERIALS}
    for i in shells:
        by_material.setdefault(meta["collision"][i]["material"], []).append(i)
    for material, members in sorted(by_material.items()):
        entries = sorted((m["slot"], m["visual"]) for m in meta["materials"] if m["physics"] == material)
        slots = [slot for slot, _ in entries]
        faces = [face for face in _mesh_faces(lod0) if face.slot in slots]
        if not members:
            if faces:
                problems.append(f"slots {', '.join(slots)} carry {material} but no {material} shell stands for them")
            continue
        kinds = [shapes[i].kind for i in members]
        lines.append(f"audit {material} shell: {len(members)} piece{'s' * (len(members) > 1)} ("
                     + ", ".join(f"{kinds.count(k)} {k}{'s' * (kinds.count(k) > 1)}" for k in sorted(set(kinds))) + ")")
        index = FaceIndex(faces) if faces else None
        for i in members:
            label = f"collision[{i}] {shapes[i].kind} ({material} shell)"
            if index is None:
                problems.append(f"{label}: no material slot carries {material}, so it encloses nothing" if not slots
                                else f"{label}: slots {', '.join(slots)} have no LOD0 face to stand off")
                lines.append(f"audit {label}: no face of slots {', '.join(slots) or 'none'} to stand off")
                continue
            distance, at = _standoff(shapes, i, [j for j in members if j != i], index)
            if at is None:
                lines.append(f"audit {label}: no surface above the ground outside the other pieces, "
                             f"tolerance {ENVELOPE_TOLERANCE_M * 1000:g} mm")
                continue
            text = f"largest standoff {distance * 1000:.3f} mm at {_fmt(at)}"
            if distance > ENVELOPE_TOLERANCE_M:
                problems.append(f"{label}: {text} exceeds the tolerance {ENVELOPE_TOLERANCE_M * 1000:g} mm")
            lines.append(f"audit {label}: {text}, tolerance {ENVELOPE_TOLERANCE_M * 1000:g} mm")
        for slot, visual in entries:
            card = visual in card_visuals
            limit, word = (CARD_ALLOWANCE_M, "allowance") if card else (ENVELOPE_TOLERANCE_M, "tolerance")
            vertices = sorted({lod0.vertices[k] for indices, _, _, s in lod0.faces if s == slot for k in indices})
            distance, at = 0.0, None
            for v in vertices:
                outside = math.inf
                for i in members:
                    outside = min(outside, shapes[i].sdf(v))
                    if outside <= 0:
                        break
                if outside > distance:
                    distance, at = outside, v
            label = f"slot {slot} ({'cards' if card else 'geometry'}, {material} shell)"
            text = f"largest outside distance {distance * 1000:.3f} mm" + (f" at {_fmt(at)}" if at else "")
            if distance > limit:
                problems.append(f"{label}: {text} exceeds the {word} {limit * 1000:g} mm")
            lines.append(f"audit {label}: {text} over {len(vertices)} vertices, {word} {limit * 1000:g} mm")
    return problems


def _standoff(shapes, i, others, index):
    """(largest standoff, where) of piece i: the largest distance from its surface to the nearest face of `index`,
    over the points above the ground and outside the `others`, within SHELL_PRECISION_M by branch and bound over the
    cells of _shell_cells; (0, None) when no such point exists. A cell's vertices lie on the surface and its patch
    within its sag of its flat triangle, so its bound is the lesser of two, plus the sag: the distance to one
    candidate face, convex, at its worst vertex; and the distance at its nearest vertex plus its longest edge (the
    distance is 1-Lipschitz). A cell whose vertices all lie deeper than its sag under the ground, or inside one other
    piece (convex), is dropped; a vertex hidden either way counts as no value but its cell's bound still holds every
    point, so the search closes in on the edges of the exposed surface."""
    shape = shapes[i]
    reach = shape.reach()
    others = [shapes[j] for j in others if _dist(shapes[j].centre, shape.centre) < reach + shapes[j].reach()]

    def hidden(v):
        return v[1] < 0 or any(other.sdf(v) < 0 for other in others)

    def dropped(cell, sag):
        return all(v[1] < -sag for v in cell) or any(all(other.sdf(v) < -sag for v in cell) for other in others)

    cache = {}

    def value(v):
        """(distance, nearest face, exposed) at a vertex."""
        if v not in cache:
            d, which = index.nearest(v)
            cache[v] = (d, which, not hidden(v))
        return cache[v]

    best, at, heap = -1.0, None, []

    def push(cell):
        nonlocal best, at
        points, patch = cell
        sag = _sag(shape, cell)
        if dropped(points, sag):
            return
        values = [value(v) for v in points]
        for v, (d, _, exposed) in zip(points, values):
            if exposed and d > best:
                best, at = d, v
        edge = _longest_edge(points)
        upper = min(v[0] for v in values) + edge + sag
        if upper > best + SHELL_PRECISION_M:
            faces = {v[1] for v in values}
            upper = min(upper, sag + min(max(_face_distance(v, index.faces[f]) for v in points) for f in faces))
        if upper > best + SHELL_PRECISION_M and edge > SHELL_PRECISION_M:
            heapq.heappush(heap, (-upper, len(heap), cell))

    for cell in _shell_cells(shape):
        push(cell)
    while heap and -heap[0][0] > best + SHELL_PRECISION_M:
        _, _, cell = heapq.heappop(heap)
        for child in _split(shape, cell):
            push(child)
    return (best, at) if at is not None else (0.0, None)


def _shell_cells(shape):
    """Coarse cells (vertices, patch) over a piece's surface, from its tessellation at SHELL_START_M. A sphere's
    cells, and a capsule's cap cells, are patches of radial projection from the sphere's or cap's centre
    (("radial", centre)); a capsule's wall is cut in angle and height (("wall", params)); both tile the surface
    exactly as they split. Any other shape is cut flat at SHELL_CHORD_M (patch None)."""
    if shape.kind not in ("sphere", "capsule"):
        return [((a, b, c), None) for a, b, c, _ in _tessellate(shape, SHELL_CHORD_M)]
    cells = []
    for a, b, c, _ in _tessellate(shape, SHELL_START_M):
        ys = [shape.local(p)[1] for p in (a, b, c)]
        if shape.kind == "sphere":
            cells.append(((a, b, c), ("radial", shape.centre)))
        elif all(y <= -shape.h + 1e-9 for y in ys) or all(y >= shape.h - 1e-9 for y in ys):
            cells.append(((a, b, c), ("radial", shape.place((0.0, math.copysign(shape.h, ys[0]), 0.0)))))
    if shape.kind == "capsule" and shape.h > 0:
        around = max(8, math.ceil(2 * math.pi / math.acos(1 - SHELL_START_M / shape.r))) if shape.r > SHELL_START_M else 8
        for j in range(around):
            t0, t1 = 2 * math.pi * j / around, 2 * math.pi * (j + 1) / around
            for params in (((t0, -shape.h), (t1, -shape.h), (t1, shape.h)), ((t0, -shape.h), (t1, shape.h), (t0, shape.h))):
                cells.append((tuple(_on_wall(shape, q) for q in params), ("wall", params)))
    return cells


def _on_wall(shape, param):
    angle, y = param
    return shape.place((shape.r * math.cos(angle), y, shape.r * math.sin(angle)))


def _split(shape, cell):
    """The four children of a cell, their new vertices on the surface (see _shell_cells)."""
    (a, b, c), patch = cell
    if patch is None:
        ab, bc, ca = _mid(a, b), _mid(b, c), _mid(c, a)
    elif patch[0] == "radial":
        centre = patch[1]
        ab, bc, ca = (_add(centre, tuple(shape.r * k for k in _normalize(_sub(_mid(p, q), centre))))
                      for p, q in ((a, b), (b, c), (c, a)))
    else:
        pa, pb, pc = patch[1]
        qab, qbc, qca = (((p[0] + q[0]) / 2, (p[1] + q[1]) / 2) for p, q in ((pa, pb), (pb, pc), (pc, pa)))
        ab, bc, ca = _on_wall(shape, qab), _on_wall(shape, qbc), _on_wall(shape, qca)
        return [((a, ab, ca), ("wall", (pa, qab, qca))), ((ab, b, bc), ("wall", (qab, pb, qbc))),
                ((ca, bc, c), ("wall", (qca, qbc, pc))), ((ab, bc, ca), ("wall", (qab, qbc, qca)))]
    return [((a, ab, ca), patch), ((ab, b, bc), patch), ((ca, bc, c), patch), ((ab, bc, ca), patch)]


def _sag(shape, cell):
    """How far a cell's patch may stand off its flat triangle: r less the distance from the projection centre to
    the triangle for a radial patch; r * da^2 / 2 for a wall patch spanning the angle da (Taylor's bound on the
    circle's chord); 0 when flat."""
    points, patch = cell
    if patch is None:
        return 0.0
    if patch[0] == "radial":
        return max(0.0, shape.r - _point_triangle_distance(patch[1], points))
    angles = [q[0] for q in patch[1]]
    return shape.r * (max(angles) - min(angles)) ** 2 / 2


def standoff(shape, index):
    """(largest standoff, where) of one shape (a metadata dict) on its own against the faces of `index`: rule (a)
    with no other piece hiding any of it, so at least what the audit measures. lib.shell checks its fit with it."""
    return _standoff([_Shape(shape)], 0, [], index)


class FaceIndex:
    """The faces of some slots on a grid of FACE_CELL_M cells, each face listed in every cell it may meet (its
    bounding box's cells whose centre lies within half a cell diagonal of it), for the nearest face to a point: rings
    of cells outward until the face found is nearer than any unseen one can be, and every face (bounding boxes
    first) for a point more than FACE_RINGS rings from the faces. `faces` are _Face polygons, or a
    lib.geometry.Mesh whose faces of `slots` are taken."""

    def __init__(self, faces, slots=None):
        if slots is not None:
            faces = [face for face in _mesh_faces(faces) if face.slot in slots]
        self.faces, self.cells = faces, {}
        half = FACE_CELL_M * math.sqrt(3) / 2
        for i, face in enumerate(faces):
            lo = [math.floor(c / FACE_CELL_M) for c in face.lo]
            hi = [math.floor(c / FACE_CELL_M) for c in face.hi]
            for x in range(lo[0], hi[0] + 1):
                for y in range(lo[1], hi[1] + 1):
                    for z in range(lo[2], hi[2] + 1):
                        centre = ((x + 0.5) * FACE_CELL_M, (y + 0.5) * FACE_CELL_M, (z + 0.5) * FACE_CELL_M)
                        if _face_distance(centre, face) <= half:
                            self.cells.setdefault((x, y, z), []).append(i)

    def nearest(self, p, cap=math.inf):
        """(distance, face index) of the face nearest to p, or (cap, None) when none lies nearer than `cap`. A face
        whose bounding box lies no nearer than the best so far is skipped."""
        px, py, pz = p
        cx, cy, cz = math.floor(px / FACE_CELL_M), math.floor(py / FACE_CELL_M), math.floor(pz / FACE_CELL_M)
        best, which, seen, faces, cells = cap, None, set(), self.faces, self.cells
        for k in range(FACE_RINGS + 2):
            if k <= FACE_RINGS:
                found = (i for dx, dy, dz in _RINGS[k] for i in cells.get((cx + dx, cy + dy, cz + dz), ()))
            else:  # beyond the rings: every face not seen yet
                found = range(len(faces))
            for i in found:
                if i in seen:
                    continue
                seen.add(i)
                face = faces[i]
                (lx, ly, lz), (hx, hy, hz) = face.lo, face.hi
                ex = lx - px if px < lx else px - hx if px > hx else 0.0
                ey = ly - py if py < ly else py - hy if py > hy else 0.0
                ez = lz - pz if pz < lz else pz - hz if pz > hz else 0.0
                if ex * ex + ey * ey + ez * ez < best * best:
                    d = _face_distance(p, face)
                    if d < best:
                        best, which = d, i
            if best <= k * FACE_CELL_M:  # every unseen face lies beyond the rings searched
                break
        return best, which


# the cell offsets of each ring of FaceIndex.nearest, ring k at Chebyshev distance k
_RINGS = [[(x, y, z) for x in range(-k, k + 1) for y in range(-k, k + 1) for z in range(-k, k + 1)
           if max(abs(x), abs(y), abs(z)) == k] for k in range(FACE_RINGS + 1)]


def _directed_max(cells, value, bound, zone=None):
    """The largest value over the surface of the `cells` triangles, within PRECISION_M, as (value, point, which):
    `value(vertex)` gives the exact (distance, which) at a point and `bound(cell, [which per vertex])` an upper
    bound over a cell. Only the points within NEAR_RANGE_M of `zone` count when it is given (point None when no
    such point exists)."""
    cache, near = {}, {}
    best, at, which = -1.0, None, None
    heap = []

    def push(cell):
        nonlocal best, at, which
        for v in cell:
            if v not in cache:
                cache[v] = value(v)
            if zone and v not in near:
                near[v] = _zone_distance(v, zone)
        if zone and min(near[v] for v in cell) - _longest_edge(cell) > NEAR_RANGE_M:
            return
        for v in cell:
            d, w = cache[v]
            if d > best and (not zone or near[v] <= NEAR_RANGE_M):
                best, at, which = d, v, w
        upper = bound(cell, [cache[v][1] for v in cell])
        if upper > best + PRECISION_M:
            heapq.heappush(heap, (-upper, len(heap), cell))

    for cell in cells:
        push(cell)
    while heap and -heap[0][0] > best + PRECISION_M:
        _, _, (a, b, c) = heapq.heappop(heap)
        ab, bc, ca = _mid(a, b), _mid(b, c), _mid(c, a)
        for child in ((a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)):
            push(child)
    return best, at, which


# --- the mesh as convex faces ---------------------------------------------------------------------------------

class _Face:
    """A convex planar polygon of the mesh: points counter-clockwise about its outward normal, the vertex indices
    it was built from (for the shared edge with a neighbour), its material slot, and its bounding box."""

    def __init__(self, points, indices, outward, slot=None):
        self.normal = _normalize(outward)  # generators fix only its direction
        if _dot(_newell(points), outward) < 0:
            points, indices = points[::-1], indices[::-1]
        self.points, self.indices, self.slot = points, indices, slot
        self.lo = tuple(min(p[k] for p in points) for k in range(3))
        self.hi = tuple(max(p[k] for p in points) for k in range(3))
        # per edge, for _face_distance: its start, its vector, its squared length and the in-plane normal pointing
        # inside
        self.edges = [(a, _sub(b, a), _dot(_sub(b, a), _sub(b, a)), _cross(self.normal, _sub(b, a)))
                      for a, b in zip(points, points[1:] + points[:1])]

    def fan(self):
        return [(0, k, k + 1) for k in range(1, len(self.points) - 1)]

    def centroid(self):
        return tuple(sum(p[k] for p in self.points) / len(self.points) for k in range(3))


def _mesh_faces(mesh):
    """The mesh's faces as _Face polygons: convex ones whole, concave ones ear-clipped into triangles."""
    faces = []
    for indices, outward, _, slot in mesh.faces:
        points = [mesh.vertices[i] for i in indices]
        if _is_convex(points, outward):
            faces.append(_Face(points, list(indices), outward, slot))
        else:
            for tri in _triangulate(points, outward):
                faces.append(_Face([points[i] for i in tri], [indices[i] for i in tri], outward, slot))
    return faces


def _face_distance(p, face):
    """Distance from p to a convex planar face: to its plane when p projects inside (on the inner side of every
    edge's in-plane normal), else to its nearest edge. Written out: the shell audit calls it millions of times."""
    px, py, pz = p
    for (ax, ay, az), _, _, (mx, my, mz) in face.edges:
        if (px - ax) * mx + (py - ay) * my + (pz - az) * mz < 0:
            break
    else:
        (ox, oy, oz), (nx, ny, nz) = face.points[0], face.normal
        return abs((px - ox) * nx + (py - oy) * ny + (pz - oz) * nz)
    best = math.inf
    for (ax, ay, az), (bx, by, bz), length2, _ in face.edges:
        apx, apy, apz = px - ax, py - ay, pz - az
        t = 0.0 if length2 == 0 else (apx * bx + apy * by + apz * bz) / length2
        t = 0.0 if t < 0 else 1.0 if t > 1 else t
        dx, dy, dz = apx - t * bx, apy - t * by, apz - t * bz
        d2 = dx * dx + dy * dy + dz * dz
        if d2 < best:
            best = d2
    return math.sqrt(best)


class _FaceIndex:
    """The mesh's faces in a uniform grid of buckets (each face in every bucket its bounding box touches), so the
    nearest face to a point is found among a few buckets instead of the whole mesh: a 12 m ground patch has twenty
    thousand faces, and the search visits most of its vertices. The bucket size is the median face extent. The
    primitive -> mesh direction's index; the shell audit and lib.shell use FaceIndex (fixed FACE_CELL_M cells)."""

    def __init__(self, faces):
        self.faces = faces
        extents = sorted(max(f.hi[k] - f.lo[k] for k in range(3)) for f in faces)
        self.h = max(0.01, extents[len(extents) // 2]) if extents else 1.0
        self.buckets = {}
        for i, face in enumerate(faces):
            lo, hi = self._cell(face.lo), self._cell(face.hi)
            for x in range(lo[0], hi[0] + 1):
                for y in range(lo[1], hi[1] + 1):
                    for z in range(lo[2], hi[2] + 1):
                        self.buckets.setdefault((x, y, z), []).append(i)
        self.reach = max((abs(c[k]) for c in self.buckets for k in range(3)), default=0) + 1

    def _cell(self, p):
        return (math.floor(p[0] / self.h), math.floor(p[1] / self.h), math.floor(p[2] / self.h))

    def nearest(self, p):
        """(distance, index) of the face nearest to p: the rings of buckets around p's, outwards, until no further
        ring can hold a closer face (a face in ring k lies at least (k - 1) bucket sizes away)."""
        px, py, pz = p
        cx, cy, cz = self._cell(p)
        best, which = math.inf, None
        seen = set()
        k = 0
        while True:
            for x in range(cx - k, cx + k + 1):
                for y in range(cy - k, cy + k + 1):
                    edge = max(abs(x - cx), abs(y - cy)) == k
                    for z in (range(cz - k, cz + k + 1) if edge else (cz - k, cz + k) if k else (cz,)):
                        for i in self.buckets.get((x, y, z), ()):
                            if i in seen:
                                continue
                            seen.add(i)
                            face = self.faces[i]
                            lo, hi = face.lo, face.hi
                            dx = lo[0] - px if px < lo[0] else px - hi[0] if px > hi[0] else 0.0
                            dy = lo[1] - py if py < lo[1] else py - hi[1] if py > hi[1] else 0.0
                            dz = lo[2] - pz if pz < lo[2] else pz - hi[2] if pz > hi[2] else 0.0
                            if dx * dx + dy * dy + dz * dz >= best * best:
                                continue
                            d = _face_distance(p, face)
                            if d < best:
                                best, which = d, i
            if best <= k * self.h or k > self.reach + max(abs(cx), abs(cy), abs(cz)):
                return best, which
            k += 1


def _covered(p, others):
    """Whether p lies inside one of the other primitives, so no collision surface passes there. A primitive whose
    bounding box (grown by COVERED_M) misses p is skipped without its signed distance."""
    return any(shape.lo[0] <= p[0] <= shape.hi[0] and shape.lo[1] <= p[1] <= shape.hi[1]
               and shape.lo[2] <= p[2] <= shape.hi[2] and shape.sdf(p) <= COVERED_M for shape in others)


def _cell_covered(cell, others):
    """Whether a whole triangle lies inside the other primitives: its three vertices inside the same one (every
    primitive is convex), or, for a cell no longer than SEAM_CELL_M, each vertex inside some other one. The second
    rule ends the search at a seam where two primitives only touch (the mat boxes standing on the soil box), which
    no single primitive covers: a strip of face under a millimetre wide there is no surface a foot could find."""
    if any(all(shape.lo[k] <= v[k] <= shape.hi[k] for v in cell for k in range(3))
           and all(shape.sdf(v) <= COVERED_M for v in cell) for shape in others):
        return True
    return _longest_edge(cell) <= SEAM_CELL_M and all(_covered(v, others) for v in cell)


def _faces_bound(cell, which, faces):
    """An upper bound of the distance to the mesh over a cell, from the faces nearest to its vertices: the largest
    vertex distance to one face (convex, so its maximum sits at a vertex); when two candidate faces share an edge,
    also the cell clipped at the fold's bisector plane, each part held to its own face."""
    candidates = sorted(set(which))
    upper = min(max(_face_distance(v, faces[i]) for v in cell) for i in candidates)
    if len(candidates) == 2:
        a, b = faces[candidates[0]], faces[candidates[1]]
        shared = [a.points[a.indices.index(i)] for i in a.indices if i in b.indices]
        if len(shared) >= 2:
            axis = _sub(shared[1], shared[0])
            mean = _add(a.normal, b.normal)
            w = _normalize(_cross(axis, mean if _dot(mean, mean) > 1e-12 else a.normal))
            if _dot(_sub(a.centroid(), shared[0]), w) < 0:
                w = (-w[0], -w[1], -w[2])
            side_a, side_b = _clip(cell, shared[0], w)
            upper = min(upper, max([_face_distance(v, a) for v in side_a] + [_face_distance(v, b) for v in side_b]))
    return upper


def _above_ground(cells):
    """The parts of the cells at y >= 0, fanned into triangles (slivers of no area dropped): for a planted asset the
    terrain covers the rest, and clipping first keeps the search from refining along the ground line."""
    out = []
    for cell in cells:
        part, _ = _clip(cell, (0.0, 0.0, 0.0), UP)
        for k in range(1, len(part) - 1):
            tri = (part[0], part[k], part[k + 1])
            if _dot(*(2 * [_cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))])) > 1e-24:
                out.append(tri)
    return out


def _clip(cell, origin, normal):
    """The cell's parts on the positive and the negative side of the plane through `origin` with `normal`."""
    positive, negative = [], []
    for p, q in zip(cell, cell[1:] + cell[:1]):
        hp, hq = _dot(_sub(p, origin), normal), _dot(_sub(q, origin), normal)
        (positive if hp >= 0 else negative).append(p)
        if (hp < 0) != (hq < 0) and hp != hq:
            t = hp / (hp - hq)
            x = (p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1]), p[2] + t * (q[2] - p[2]))
            positive.append(x)
            negative.append(x)
    return positive, negative


# --- the primitives, exact ------------------------------------------------------------------------------------

class _Shape:
    """A collision primitive: its signed distance in asset space (negative inside), the depth bound over a cell,
    and its tessellation for the primitive -> mesh direction and the landable faces."""

    def __init__(self, shape):
        self.kind, self.centre = shape["shape"], tuple(shape["center"])
        self.q = tuple(shape.get("rotation", (0.0, 0.0, 0.0, 1.0)))
        self.r = shape.get("radius")
        self.half = tuple(s / 2 for s in shape["size"]) if self.kind == "box" else None
        self.h = shape["height"] / 2 - (self.r if self.kind == "capsule" else 0.0) if "height" in shape else 0.0
        self._triangles = None

    @property
    def triangles(self):
        """The tessellation within CHORD_M, built on first use (a shell piece never needs it)."""
        if self._triangles is None:
            self._triangles = _tessellate(self)
        return self._triangles

    @functools.cached_property
    def lo(self):
        """The bounding box (lo, hi) for _covered, grown past the tessellation's chords and by the coverage
        tolerance; built on first use like the tessellation, then a plain attribute."""
        return tuple(min(p[k] for t in self.triangles for p in t[:3]) - self._grow() for k in range(3))

    @functools.cached_property
    def hi(self):
        return tuple(max(p[k] for t in self.triangles for p in t[:3]) + self._grow() for k in range(3))

    def _grow(self):
        return (0.0 if self.kind == "box" else CHORD_M) + 2 * COVERED_M

    def reach(self):
        """The radius of a sphere about the centre that holds the whole shape."""
        if self.kind == "box":
            return math.sqrt(sum(h * h for h in self.half))
        if self.kind == "cylinder":
            return math.hypot(self.h, self.r)
        return self.h + self.r

    def local(self, p):
        x, y, z, w = self.q
        return _rotate((-x, -y, -z, w), _sub(p, self.centre))

    def place(self, p):
        return _add(self.centre, _rotate(self.q, p))

    def sdf(self, p):
        x, y, z = self.local(p)
        if self.kind == "box":
            d = (abs(x) - self.half[0], abs(y) - self.half[1], abs(z) - self.half[2])
            outside = math.sqrt(sum(max(c, 0.0) ** 2 for c in d))
            return outside + min(max(d), 0.0)
        if self.kind == "sphere":
            return math.sqrt(x * x + y * y + z * z) - self.r
        if self.kind == "capsule":
            yy = min(self.h, max(-self.h, y))
            return math.sqrt(x * x + (y - yy) ** 2 + z * z) - self.r
        d = (math.sqrt(x * x + z * z) - self.r, abs(y) - self.h)
        return min(max(d), 0.0) + math.sqrt(max(d[0], 0.0) ** 2 + max(d[1], 0.0) ** 2)

    def bound(self, cell):
        """An upper bound of |sdf| over a cell: outside, the distance to the convex shape is convex (largest at a
        vertex); inside, the depth is at most the depth below the shape's nearest plane (box), or the radius less
        the cell's distance to the centre or axis (sphere, capsule, cylinder), exactly."""
        local = [self.local(v) for v in cell]
        outside = max(max(self.sdf(v), 0.0) for v in cell)
        if self.kind == "box":
            depth = min(max(self.half[k] - s * p[k] for p in local) for k in range(3) for s in (-1, 1))
        elif self.kind == "sphere":
            depth = self.r - _point_triangle_distance((0.0, 0.0, 0.0), local)
        else:
            reach = self.h if self.kind == "capsule" else FAR_M
            depth = self.r - _segment_triangle_distance((0.0, -reach, 0.0), (0.0, reach, 0.0), local)
            if self.kind == "cylinder":
                ys = [p[1] for p in local]
                depth = min(depth, self.h - (0.0 if min(ys) < 0 < max(ys) else min(abs(y) for y in ys)))
        return max(outside, depth, 0.0)


def _nearest_shape(p, shapes, indices=None):
    return min((abs(shapes[i].sdf(p)), i) for i in (range(len(shapes)) if indices is None else indices))


def _shapes_bound(cell, which, shapes):
    return min(shapes[i].bound(cell) for i in set(which))


def _tessellate(shape, chord=CHORD_M):
    """(a, b, c, outward normal) triangles of a _Shape's surface in asset space: a box exactly, each face in cells
    of at most BOX_CELL_M, the curved shapes within `chord`, built along local +Y, rotated by the shape's
    quaternion and moved to its centre."""
    if shape.kind == "box":
        h = shape.half
        triangles = []
        for axis in range(3):
            u, v = (axis + 1) % 3, (axis + 2) % 3
            nu, nv = (max(1, math.ceil(2 * h[k] / BOX_CELL_M)) for k in (u, v))
            for side in (-1, 1):
                def corner(i, j):
                    p = [0.0, 0.0, 0.0]
                    p[axis], p[u], p[v] = side * h[axis], -h[u] + 2 * h[u] * i / nu, -h[v] + 2 * h[v] * j / nv
                    return shape.place(p)
                for i in range(nu):
                    for j in range(nv):
                        a, b, c, d = corner(i, j), corner(i + 1, j), corner(i + 1, j + 1), corner(i, j + 1)
                        triangles += [(a, b, c), (a, c, d)]
    else:
        r, h = shape.r, shape.h
        step = math.pi / 4 if r <= chord else math.acos(1 - chord / r)
        around, half_rings = max(8, math.ceil(2 * math.pi / step)), max(2, math.ceil(math.pi / step / 2))
        ring = lambda y, rho: [(rho * math.cos(2 * math.pi * j / around), y, rho * math.sin(2 * math.pi * j / around))
                               for j in range(around)]
        if shape.kind == "cylinder":
            rings = [[(0.0, -h, 0.0)], ring(-h, r), ring(h, r), [(0.0, h, 0.0)]]
        else:
            lats = [math.pi / 2 * k / half_rings for k in range(half_rings)]  # 0 at the equator, pole excluded
            lower = [[(0.0, -h - r, 0.0)]] + [ring(-h - r * math.sin(t), r * math.cos(t)) for t in reversed(lats)]
            upper = [ring(h + r * math.sin(t), r * math.cos(t)) for t in lats] + [[(0.0, h + r, 0.0)]]
            rings = lower + upper[1:] if shape.kind == "sphere" else lower + upper  # one equator on a sphere
        triangles = []
        for lo, hi in zip(rings, rings[1:]):
            if len(lo) == 1:
                triangles += [(lo[0], hi[j], hi[(j + 1) % around]) for j in range(around)]
            elif len(hi) == 1:
                triangles += [(lo[j], lo[(j + 1) % around], hi[0]) for j in range(around)]
            else:
                for j in range(around):
                    k = (j + 1) % around
                    triangles += [(lo[j], lo[k], hi[k]), (lo[j], hi[k], hi[j])]
        triangles = [tuple(shape.place(p) for p in t) for t in triangles]
    result = []
    for a, b, c in triangles:
        n = _normalize(_cross(_sub(b, a), _sub(c, a)))
        if n == (0.0, 0.0, 0.0):
            continue  # a capsule of height 2r has a zero-length wall
        if _dot(n, _sub(_centroid(a, b, c), shape.centre)) < 0:
            n = (-n[0], -n[1], -n[2])
        result.append((a, b, c, n))
    return result


# --- the 2 cm zone ----------------------------------------------------------------------------------------------

def _zone(meta, shapes, soft):
    """The geometry that tightens the tolerance: (landable faces, gap polygons), or None when there is none. A soft
    top is a landing face everywhere and is held to the 5 cm tolerance, so it adds no zone."""
    faces = [_Face([a, b, c], [0, 1, 2], n) for shape, entry in zip(shapes, meta["collision"])
             if entry["landable"] and entry["material"] not in soft
             for a, b, c, n in shape.triangles if _dot(n, UP) >= LANDABLE_COS]
    polygons = [(tuple(tuple(p) for p in g["polygon"]), tuple(g["normal"])) for g in meta["gaps"]]
    return (faces, polygons) if faces or polygons else None


def _zone_distance(p, zone):
    faces, polygons = zone
    return min([_face_distance(p, face) for face in faces] + [_gap_distance(p, *g) for g in polygons])


def _gap_distance(p, polygon, normal):
    """Distance from p to a planar, possibly concave polygon: to its plane when p projects inside it, else to its
    nearest edge."""
    u, v = _plane_basis(normal)
    flat = [(_dot(q, u), _dot(q, v)) for q in polygon]
    if _inside_polygon2((_dot(p, u), _dot(p, v)), flat):
        return abs(_dot(_sub(p, polygon[0]), normal))
    return min(_segment_distance(p, a, b) for a, b in zip(polygon, polygon[1:] + polygon[:1]))


# --- gaps: the largest inscribed circle -------------------------------------------------------------------------

def _flatten_polygon(polygon, normal):
    u, v = _plane_basis(normal)
    return [(_dot(p, u), _dot(p, v)) for p in polygon]


def _inscribed_radius(polygon, precision=0.001):
    """The radius of the largest circle inscribed in a simple 2D polygon, within `precision` (Mapbox's polylabel:
    a quadtree of cells ranked by the best radius they could still hold)."""
    xs, ys = [p[0] for p in polygon], [p[1] for p in polygon]
    size = min(max(xs) - min(xs), max(ys) - min(ys))
    if size <= 0:
        return 0.0
    centroid = (sum(xs) / len(xs), sum(ys) / len(ys))
    best = max(_signed_distance2(centroid, polygon),
               _signed_distance2(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2), polygon))
    heap = []

    def push(x, y, h):
        nonlocal best
        d = _signed_distance2((x, y), polygon)
        best = max(best, d)
        heapq.heappush(heap, (-(d + h * math.sqrt(2)), len(heap), x, y, h))

    x = min(xs)
    while x < max(xs):
        y = min(ys)
        while y < max(ys):
            push(x + size / 2, y + size / 2, size / 2)
            y += size
        x += size
    while heap and -heap[0][0] - best > precision:
        _, _, x, y, h = heapq.heappop(heap)
        for dx in (-h / 2, h / 2):
            for dy in (-h / 2, h / 2):
                push(x + dx, y + dy, h / 2)
    return best


def _signed_distance2(p, polygon):
    """Distance from a 2D point to the polygon's boundary, negative outside."""
    d = min(_segment_distance(p, a, b) for a, b in zip(polygon, polygon[1:] + polygon[:1]))
    return d if _inside_polygon2(p, polygon) else -d


def _inside_polygon2(p, polygon):
    """Crossing-number test in 2D."""
    inside = False
    for (ax, ay), (bx, by) in zip(polygon, polygon[1:] + polygon[:1]):
        if (ay > p[1]) != (by > p[1]) and p[0] < ax + (p[1] - ay) * (bx - ax) / (by - ay):
            inside = not inside
    return inside


# --- polygons and triangulation ---------------------------------------------------------------------------------

def _is_convex(points, normal):
    """Every turn of the cyclic polygon has the same sense about `normal` (collinear points allowed)."""
    sense = 0
    for a, b, c in zip(points, points[1:] + points[:1], points[2:] + points[:2]):
        turn = _dot(_cross(_sub(b, a), _sub(c, b)), normal)
        if abs(turn) > 1e-15:
            if sense and (turn > 0) != (sense > 0):
                return False
            sense = turn
    return True


def _triangulate(points, normal):
    """Ear clipping of a simple polygon in cyclic order, seen along `normal`, as index triples; a polygon with no
    ear left (degenerate) is fanned, and lib.export's mesh validation reports it."""
    u, v = _plane_basis(normal)
    flat = [(_dot(p, u), _dot(p, v)) for p in points]
    order = list(range(len(points)))
    if sum(flat[i][0] * flat[j][1] - flat[j][0] * flat[i][1] for i, j in zip(order, order[1:] + order[:1])) < 0:
        order.reverse()
    triangles = []
    while len(order) > 3:
        for k in range(len(order)):
            i, j, l = order[k - 1], order[k], order[(k + 1) % len(order)]
            if _cross2(flat[i], flat[j], flat[l]) <= 0:
                continue
            if any(_inside2(flat[m], flat[i], flat[j], flat[l]) for m in order if m not in (i, j, l)):
                continue
            triangles.append((i, j, l))
            del order[k]
            break
        else:
            return triangles + [(order[0], order[k], order[k + 1]) for k in range(1, len(order) - 1)]
    return triangles + [tuple(order)]


# --- distances --------------------------------------------------------------------------------------------------

def _point_triangle_distance(p, tri):
    return _face_distance(p, _Face(list(tri), [0, 1, 2], _normalize(_cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0])))))


def _segment_triangle_distance(s0, s1, tri):
    """Distance between the segment s0-s1 and a triangle: zero when the segment pierces it, else the least of its
    end points to the triangle and the segment to the triangle's edges."""
    face = _Face(list(tri), [0, 1, 2], _normalize(_cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))))
    h0, h1 = _dot(_sub(s0, tri[0]), face.normal), _dot(_sub(s1, tri[0]), face.normal)
    if (h0 < 0) != (h1 < 0) and h0 != h1:
        t = h0 / (h0 - h1)
        x = (s0[0] + t * (s1[0] - s0[0]), s0[1] + t * (s1[1] - s0[1]), s0[2] + t * (s1[2] - s0[2]))
        if _face_distance(x, face) < 1e-12:
            return 0.0
    return min([_face_distance(s0, face), _face_distance(s1, face)]
               + [_segment_segment_distance(s0, s1, a, b) for a, b in zip(tri, tri[1:] + tri[:1])])


def _segment_segment_distance(p1, q1, p2, q2):
    """Distance between two segments (Ericson, Real-Time Collision Detection 5.1.9)."""
    d1, d2, r = _sub(q1, p1), _sub(q2, p2), _sub(p1, p2)
    a, e, f = _dot(d1, d1), _dot(d2, d2), _dot(d2, r)
    if a <= 1e-18 and e <= 1e-18:
        return _dist(p1, p2)
    if a <= 1e-18:
        s, t = 0.0, min(1.0, max(0.0, f / e))
    else:
        c = _dot(d1, r)
        if e <= 1e-18:
            s, t = min(1.0, max(0.0, -c / a)), 0.0
        else:
            b = _dot(d1, d2)
            denom = a * e - b * b
            s = min(1.0, max(0.0, (b * f - c * e) / denom)) if denom != 0 else 0.0
            t = (b * s + f) / e
            if t < 0:
                s, t = min(1.0, max(0.0, -c / a)), 0.0
            elif t > 1:
                s, t = min(1.0, max(0.0, (b - c) / a)), 1.0
    c1 = (p1[0] + s * d1[0], p1[1] + s * d1[1], p1[2] + s * d1[2])
    c2 = (p2[0] + t * d2[0], p2[1] + t * d2[1], p2[2] + t * d2[2])
    return _dist(c1, c2)


def _segment_distance(p, a, b):
    """Distance from a point to the segment ab, in 2D or 3D."""
    ab, ap = [b[k] - a[k] for k in range(len(a))], [p[k] - a[k] for k in range(len(a))]
    length2 = sum(c * c for c in ab)
    t = 0.0 if length2 == 0 else min(1.0, max(0.0, sum(x * y for x, y in zip(ab, ap)) / length2))
    return math.sqrt(sum((ap[k] - t * ab[k]) ** 2 for k in range(len(a))))


def _longest_edge(cell):
    return max(_dist(cell[0], cell[1]), _dist(cell[1], cell[2]), _dist(cell[2], cell[0]))


def _newell(points):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(points, points[1:] + points[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return n


def _plane_basis(n):
    helper = (1.0, 0.0, 0.0) if abs(n[0]) < 0.9 else (0.0, 1.0, 0.0)
    u = _normalize(_cross(n, helper))
    return u, _cross(n, u)


def _rotate(q, v):
    """v rotated by the unit quaternion [x, y, z, w] (Hamilton, as Unity applies it)."""
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def _cross2(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _inside2(p, a, b, c):
    return _cross2(a, b, p) >= 0 and _cross2(b, c, p) >= 0 and _cross2(c, a, p) >= 0


def _fmt(p):
    return "(" + ", ".join(f"{round(c, 4) + 0.0:g}" for c in p) + ")"


def _mid(a, b):
    return ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)


def _centroid(a, b, c):
    return ((a[0] + b[0] + c[0]) / 3, (a[1] + b[1] + c[1]) / 3, (a[2] + b[2] + c[2]) / 3)


def _dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(_dot(v, v))
    return (v[0] / length, v[1] / length, v[2] / length) if length > 0 else (0.0, 0.0, 0.0)
