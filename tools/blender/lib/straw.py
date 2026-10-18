"""The S04 instance kit (p1-s04-belt-straw, design decision 4; the object sheet's "Micro-detail" table): the meshes
of lying straws, standing stems with and without a seed head, belt twigs and short green regrowth blades that
ground_patch bakes into its LOD0 micro mesh now and that the shared micro-detail generator places at physics parity
later (AST-10). Plain Python on lib.geometry, no Blender; every draw comes from the generator's seeded
random.Random in a fixed order.

A straw or twig is a tapered three-sided tube of `segments` segments along a bent spine: a bow, a lateral wiggle
and, with probability straw_bend, a kink at one node, each scaled by straw_bend (0 is a straight tube). The spine
runs along +X through the origin, so an instance is placed by its centre; the diameter tapers to TIP of the base. A
stem is the same tube along +Y from its foot at the origin, curving a little under its head, and a headed stem
carries a seed head: a closed four-triangle spindle HEAD_H_M tall on the stem's tip. A blade is a short stem of
BLADE_SEGMENTS segments, BLADE_D_M thick, arching up to BLADE_LEAN of its height to one side.

Every face is UV'd into one column of the straw palette (assets-src/materials/straw.json, kind "palette", whose
colours are laid out left to right in PALETTE order, padded to a power of two of columns, COLUMNS): the n-th
variant of a straw or stem (and its seed head) to SHADES[n % len(SHADES)], so the mat shows pale, mid and dark
straws as H does (H 12-21), twigs to "twig" and blades to "blade" ("litter", the gaps' dark depth, took a step's
wall until R6, which gave the wall the mat's own map). A column is a gradient up the map, dark at v = 0,
the colour at 0.5 and pale at 1 (H 12-21: straws shaded along their length); a tube's v runs from V_ALONG[0] at its
base to V_ALONG[1] at its tip, and place() shifts a whole instance's v (a straw deep in the mat is darker). The
kit's UVs and the recipe's column order are the one coupling between them; test_straw checks it.
"""
import math

from lib.geometry import Mesh

SLOT = "straw"
PALETTE = ("straw_pale", "straw", "straw_dark", "twig", "blade", "litter")  # the straw palette's columns, left to right
COLUMNS = 1 << (len(PALETTE) - 1).bit_length()  # PALETTE padded to a power of two of columns, as derive.py lays it
SHADES = ("straw_pale", "straw", "straw_dark")  # straws and stems cycle through these by variant
V_ALONG = (0.15, 0.85)  # a tube's v at its base and at its tip: a darker base, a paler tip (assumed from H 12-21)
V_LIMITS = (0.04, 0.96)  # v stays this far inside the map, so no filter wraps the dark end onto the pale one
SIDES = 3
TIP = 0.4          # a grass stem's tip diameter as a share of its base (assumed)
TWIG_TIP = 0.6     # a broken twig tapers less
BOW = 0.15         # bow of a lying straw as a share of its length at straw_bend 1 (assumed)
WIGGLE = 0.05      # lateral wiggle, same scale
KINK_DEG = 45.0    # the largest kink at straw_bend 1
LEAN = 0.12        # a standing stem's tip offset as a share of its height at straw_bend 1
HEAD_H_M = 0.05    # a seed head's length (assumed from H: 3-8 cm)
HEAD_R_M = 0.006   # a seed head's half-width at its base
BLADE_SEGMENTS = 2  # a short blade bends once
BLADE_D_M = 0.005  # a regrowth blade's width (assumed: short grass blades 4-6 mm, H 16, H 19-21)
BLADE_LEAN = 0.35  # a blade's tip offset as a share of its height (assumed: the blades arch over, H 19-21)
KINDS = ("straw", "stem", "stem_head", "twig", "blade")


def palette_uv(colour, v=0.5):
    """The UV in a palette column at height v of its gradient (0 dark, 0.5 the colour, 1 pale), v clamped to
    V_LIMITS."""
    return ((PALETTE.index(colour) + 0.5) / COLUMNS, min(V_LIMITS[1], max(V_LIMITS[0], v)))


def shade(variant):
    """The palette column of the variant-th straw or stem."""
    return SHADES[variant % len(SHADES)]


def build_kit(params, rng):
    """The kit's meshes per kind, params["variants"] of each: lying straws and twigs along +X centred on the origin,
    headless and headed stems and blades along +Y from the origin. `params` carries the sheet's table (straw_len_m,
    straw_d_mm, straw_bend, stem_h_m, twig_len_m, twig_d_mm, variants, segments) and blade_h_m, each range as
    {"min", "max"}."""
    n, segments, bend = params["variants"], params["segments"], params["straw_bend"]
    kit = {kind: [] for kind in KINDS}
    for i in range(n):
        kit["straw"].append(lying(rng, segments, _draw(rng, params["straw_len_m"]),
                                  _draw(rng, params["straw_d_mm"]) / 1000, bend, TIP, shade(i)))
    for i in range(n):
        kit["stem"].append(stem(rng, segments, _draw(rng, params["stem_h_m"]),
                                _draw(rng, params["straw_d_mm"]) / 1000, bend, False, shade(i)))
    for i in range(n):
        kit["stem_head"].append(stem(rng, segments, _draw(rng, params["stem_h_m"]),
                                     _draw(rng, params["straw_d_mm"]) / 1000, bend, True, shade(i)))
    for _ in range(n):
        kit["twig"].append(lying(rng, segments, _draw(rng, params["twig_len_m"]),
                                 _draw(rng, params["twig_d_mm"]) / 1000, bend, TWIG_TIP, "twig"))
    for _ in range(n):
        kit["blade"].append(stem(rng, BLADE_SEGMENTS, _draw(rng, params["blade_h_m"]), BLADE_D_M, 1.0, False, "blade",
                                 BLADE_LEAN))
    return kit


def lying(rng, segments, length, diameter, bend, tip, colour):
    """A lying straw or twig: a tapered tube along a bowed, wiggled and maybe kinked spine along +X."""
    bow = rng.uniform(-1, 1) * bend * BOW * length
    wiggle, phase = rng.uniform(-1, 1) * bend * WIGGLE * length, rng.uniform(0, 2 * math.pi)
    spine = [[(t - 0.5) * length, bow * math.sin(math.pi * t), wiggle * math.sin(2 * math.pi * t + phase)]
             for t in (i / segments for i in range(segments + 1))]
    if segments >= 2 and rng.random() < bend:
        k = rng.randrange(1, segments)  # the spine beyond node k turns in the lying plane
        a = math.radians(rng.uniform(-1, 1) * bend * KINK_DEG)
        c, s = math.cos(a), math.sin(a)
        px, pz = spine[k][0], spine[k][2]
        for p in spine[k + 1:]:
            dx, dz = p[0] - px, p[2] - pz
            p[0], p[2] = px + dx * c - dz * s, pz + dx * s + dz * c
    return tube(spine, diameter / 2, tip, (0.0, 0.0, 1.0), colour)


def stem(rng, segments, height, diameter, bend, headed, colour, lean=LEAN):
    """A standing stem from its foot at the origin: a tube along +Y curving towards a random side by up to `lean` of
    its height at bend 1, with a seed head when `headed`, both in the palette column `colour`."""
    lean = rng.uniform(0.2, 1.0) * bend * lean * height
    direction = rng.uniform(0, 2 * math.pi)
    spine = [[lean * t * t * math.cos(direction), t * height, lean * t * t * math.sin(direction)]
             for t in (i / segments for i in range(segments + 1))]
    mesh = tube(spine, diameter / 2, TIP, (1.0, 0.0, 0.0), colour)
    if headed:
        head(mesh, spine[-1], _normalize(_sub(spine[-1], spine[-2])), rng.uniform(0, 2 * math.pi), colour)
    return mesh


def tube(spine, radius, tip, ref, colour):
    """A SIDES-sided tube along `spine` (a list of points), its radius tapering linearly to `tip` of `radius` at the
    end, each ring framed by the local tangent and `ref`; two triangles per side and segment, outward normals; the
    rings' v runs from V_ALONG[0] at the first to V_ALONG[1] at the last."""
    mesh = Mesh()
    n = len(spine) - 1
    rings = []
    for i, p in enumerate(spine):
        tangent = _normalize(_sub(spine[min(i + 1, n)], spine[max(i - 1, 0)]))
        normal = _normalize(_reject(ref, tangent))
        binormal = _cross(tangent, normal)
        r = radius * (1 - (1 - tip) * i / n)
        rings.append([mesh.vertex(_add(p, _add(_scale(normal, r * math.cos(a)), _scale(binormal, r * math.sin(a)))))
                      for a in (2 * math.pi * j / SIDES for j in range(SIDES))])
    v = [palette_uv(colour, V_ALONG[0] + (V_ALONG[1] - V_ALONG[0]) * i / n) for i in range(n + 1)]
    for i in range(n):
        centre = _mid(spine[i], spine[i + 1])
        for j in range(SIDES):
            a, b = rings[i][j], rings[i][(j + 1) % SIDES]
            c, d = rings[i + 1][(j + 1) % SIDES], rings[i + 1][j]
            for tri, uvs in (((a, b, c), [v[i], v[i], v[i + 1]]), ((a, c, d), [v[i], v[i + 1], v[i + 1]])):
                mesh.face(tri, _outward(mesh, tri, centre), SLOT, uvs)
    return mesh


def head(mesh, tip, direction, phase, colour):
    """A seed head on a stem's tip: a closed spindle of four triangles, its base triangle of radius HEAD_R_M around
    the tip and its apex HEAD_H_M along `direction`, in the palette column `colour` at a tip's v."""
    normal = _normalize(_reject((1.0, 0.0, 0.0) if abs(direction[0]) < 0.9 else (0.0, 0.0, 1.0), direction))
    binormal = _cross(direction, normal)
    base = [mesh.vertex(_add(tip, _add(_scale(normal, HEAD_R_M * math.cos(a)), _scale(binormal, HEAD_R_M * math.sin(a)))))
            for a in (phase + 2 * math.pi * j / 3 for j in range(3))]
    apex = mesh.vertex(_add(tip, _scale(direction, HEAD_H_M)))
    centre = _add(tip, _scale(direction, HEAD_H_M / 3))
    uv = [palette_uv(colour, V_ALONG[1])] * 3
    for j in range(3):
        tri = (base[j], base[(j + 1) % 3], apex)
        mesh.face(tri, _outward(mesh, tri, centre), SLOT, uv)
    mesh.face(base, _outward(mesh, base, centre), SLOT, uv)


def place(target, variant, origin, axes, dv=0.0):
    """Appends an instance of `variant` to `target`: each vertex at origin + x ex + y ey + z ez for the orthonormal
    `axes` (ex, ey, ez), normals rotated the same way, slots kept, and every UV's v shifted by `dv` within V_LIMITS
    (negative: a darker instance)."""
    ex, ey, ez = axes
    base = len(target.vertices)
    for x, y, z in variant.vertices:
        target.vertices.append((origin[0] + x * ex[0] + y * ey[0] + z * ez[0],
                                origin[1] + x * ex[1] + y * ey[1] + z * ez[1],
                                origin[2] + x * ex[2] + y * ey[2] + z * ez[2]))
    for indices, (x, y, z), uvs, slot in variant.faces:
        normal = (x * ex[0] + y * ey[0] + z * ez[0], x * ex[1] + y * ey[1] + z * ez[1], x * ex[2] + y * ey[2] + z * ez[2])
        if dv:
            uvs = [(u, min(V_LIMITS[1], max(V_LIMITS[0], v + dv))) for u, v in uvs]
        target.faces.append(([i + base for i in indices], normal, uvs, slot))


def lying_axes(yaw, pitch, roll):
    """The axes of a lying instance: its spine (local +X) at `yaw` from +X towards +Z, pitched up by `pitch` and
    rolled about itself by `roll`, all in radians."""
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    ex = (cy * cp, sp, sy * cp)
    ez0 = (-sy, 0.0, cy)
    ey0 = _cross(ez0, ex)
    cr, sr = math.cos(roll), math.sin(roll)
    return ex, _add(_scale(ey0, cr), _scale(ez0, sr)), _add(_scale(ey0, -sr), _scale(ez0, cr))


def standing_axes(yaw, tilt):
    """The axes of a standing instance: its stem (local +Y) tilted by `tilt` towards the bearing `yaw` (radians)."""
    cy, sy, ct, st = math.cos(yaw), math.sin(yaw), math.cos(tilt), math.sin(tilt)
    return (ct * cy, -st, ct * sy), (st * cy, ct, st * sy), (-sy, 0.0, cy)


def _draw(rng, span):
    return rng.uniform(span["min"], span["max"])


def _outward(mesh, indices, centre):
    """The unit normal of a triangle or planar polygon, pointing away from `centre`."""
    a, b, c = (mesh.vertices[i] for i in indices[:3])
    n = _normalize(_cross(_sub(b, a), _sub(c, a)))
    centroid = tuple(sum(mesh.vertices[i][k] for i in indices) / len(indices) for k in range(3))
    return n if _dot(n, _sub(centroid, centre)) >= 0 else (-n[0], -n[1], -n[2])


def _reject(v, unit):
    d = _dot(v, unit)
    return (v[0] - d * unit[0], v[1] - d * unit[1], v[2] - d * unit[2])


def _mid(a, b):
    return ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2)


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _scale(v, s):
    return (v[0] * s, v[1] * s, v[2] * s)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _normalize(v):
    length = math.sqrt(_dot(v, v))
    if length < 1e-12:
        raise ValueError("a zero vector has no direction")
    return (v[0] / length, v[1] / length, v[2] / length)
