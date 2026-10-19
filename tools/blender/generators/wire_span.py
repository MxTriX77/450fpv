"""wire_span: one span of a village line, L03 (p1-l01-l03-pole-wires, design decisions 5-7 and 10; the lines sheet
§L03 and its shared conductor, height and sag tables; AST-05, REAL-05). Every conductor is a tube mesh of its real
diameter hanging as a catenary between its two attachments, and the same centre line goes into the metadata as a
`wires` record, which the map compiler copies into objects.json (D-007) and PHY-22 reads for tension and stiffness.

Frame (lib.frame): the origin is the ground under the first pole's axis, +X runs along the span to the second pole's
axis at `span_m`, +Y up. Both poles stand as L01 does, their line along X, so their ports face +Z and -Z. A
conductor's two ends rest in their insulators' neck grooves: its centre is the port plus one radius outward, the
port's direction. The attachments follow the pole head (`arrangement`):
- vertical_zigzag (L01's hooks_0p4): conductor n hangs at `attach_heights_m` less n * `spacing_m`, on +Z for n even
  and -Z for n odd, its port `offset_m` + n * `offset_step_m` from the pole's axis (L01_straight: hook_0's port
  0.250579 m out, each next one 2.63 mm further as the log thickens downward);
- bundle (a SIP drop or main on its anchor): the one bundle at the top conductor's place.
Both ends lie at the same distance from the line's axis, so every conductor hangs in a vertical plane along X.

The catenary: y(u) = y_a + 2 c sinh((u - 2 m) / 2c) sinh(u / 2c) over the horizontal distance u from the first end,
c and the lowest point's u, m, solved so that it passes through both ends and hangs `sag_ratio` * `span_m` below
their chord at mid-span (the sheet's sag f, from which PHY-22 takes T = w L^2 / 8 f). It is sampled at `points_per_m`
points per metre of horizontal distance (2: 81 points for 40 m; the chord between points 0.5 m apart stands 0.13 mm
off the curve at a 2 % sag).

Mesh: LOD0 a 6-sided tube per conductor along the record's points, LOD1 4-sided from `lod.lod1_switch_m`, culled
where the thin-wire material's line weighs under CULL_COVERAGE of a pixel (its column sum, PX_PER_RAD * D / d: 468 m
for 5 mm, 842 m for 9 mm, 1 870 m for 20 mm). Rings stand square to the bisected tangent, framed by parallel transport,
every corner on the conductor's circle, both ends capped. For the thin-wire shader (task 1.4's contract) every vertex
carries its radial unit normal (smooth shading) and its tube's radius as its second UV, (r, 0) in metres (Unity's
uv2). A SIP bundle is CORES cores of D sin(pi / k) / (1 + sin(pi / k)) twisted at `twist_pitch_m` about the bundle's
axis at LOD0 (their centre lines sampled TWIST_RINGS times a turn, each core its own tube and radius), one tube of
the bundle's diameter at LOD1. UVs are metres: U along the conductor from a `seed` offset, V around.

Metadata (the sheet's physics role): no collision primitive (conductors are map objects, D-007), no gap, no wind
volume, nothing landable; per conductor a `wires` record (its slot, diameter, mass per metre, sag, axial stiffness
and rated breaking force from MECHANICS, material cable, damage, the points) and an `edge` snag along the same points
with the wire's radius (the whole wire, cable) and, on a stranded or bundled conductor, the outer strand's or the
core's radius as its `edge_radius_m`.

Slots, each physics cable: steel (wire_steel: steel_wire), aluminium (wire_aluminium: bare_al, acsr), insulated
(wire_sip: sip_drop, sip_main, insulated). Only intact spans on vertical_zigzag and bundle heads are built (G1):
`damage` cut and down and the horizontal and top_triangle arrangements are refused by check.
"""
import math
import random

from lib.geometry import Mesh
from lib.skeleton import add, cross, dot, norm, normalize, scale, sub

PHYSICS = "cable"  # the sheet: every conductor's material id, edge radius its own radius
TYPES = ("steel_wire", "bare_al", "acsr", "sip_drop", "sip_main", "insulated")
SLOTS = {"steel_wire": "steel", "bare_al": "aluminium", "acsr": "aluminium",
         "sip_drop": "insulated", "sip_main": "insulated", "insulated": "insulated"}
CORES = {"sip_drop": 2, "sip_main": 4}  # the sheet: SIP is 2-4 cores twisted round each other; the rest one strand
# The sheet's classes (thin 3-6, medium 7-12, thick 13-32 mm) split midway between, so A-25's 6.4 mm is thin as its
# conductor table says.
CLASSES = {"thin": (3.0, 6.5), "medium": (6.5, 12.5), "thick": (12.5, 32.0)}
ARRANGEMENTS = ("vertical_zigzag", "bundle")  # horizontal, top_triangle: not built
DAMAGES = ("intact",)                          # cut, down: not built (G1 shows intact lines)
# The lowest point an intact conductor may reach (published, Ukrainian ПУЕ cl. 2.4.50 via the sheet): bare wires 6.0 m
# over a street but 3.5 m in hard-to-reach terrain, SIP 5.0 and 2.5 m, a service drop 2.75 m at the house entry. The
# village lines the sheet copies hang under the street figures (L01's lowest hook is at 5.855 m), so check holds the
# lowest figure the rules allow anywhere: nothing intact sags lower.
CLEARANCE_M = {"steel_wire": 3.5, "bare_al": 3.5, "acsr": 3.5, "sip_main": 2.5, "insulated": 2.5, "sip_drop": 2.75}
DROP_SPAN_M = 25.0      # published, ПУЕ cl. 2.4.17 via the sheet: a service drop longer than this needs a pole
INTACT_SAG_RATIO = 0.05  # the sheet: a sag ratio above this only on a damaged span
SIDES = (6, 4)          # per LOD (design decision 5: a 9 mm wire at 6 sides stands 0.6 mm off round)
TWIST_RINGS = 8         # rings per turn of a bundle's cores (assumed: a core's centre within 0.4 mm of its helix)
PX_PER_RAD = 935.0      # derived (pipeline design decision 7): the 60 degree vertical view on 1 080 px
CULL_COVERAGE = 0.01    # assumed (task 1.4): culled once the line's column sum falls under 1 % of a pixel
# The record's axial stiffness EA and rated breaking force (task 1.5's reading §2.2 and §8 item 1): per type, Young's
# modulus, the metallic section (from the diameter D, or the type's own) and the rated strength over that section.
# A type without a row is refused by check until its data is read.
E_STEEL_PA, E_ALUMINIUM_PA = 200e9, 63e9               # assumed (textbook 200-210 and 60-70 GPa, stranding lowers Al)
A50_M2, A50_D_M, A50_BREAK_N = 49.5e-6, 0.009, 8198.0  # published, ГОСТ 839-80 Table 2, mark A-50: 49.5 mm² in 9.0 mm
STEEL_STRENGTH_PA = 390e6                             # published, ГОСТ 3282-74 Table 2: 4.5-6.0 mm unannealed 390-830
SIP_DROP_M2 = 2 * 16e-6                               # the sheet's drop: two 16 mm² aluminium cores (published nominal)
MECHANICS = {  # type: (E, the metallic section of a conductor of diameter D m, the rated strength over it)
    "steel_wire": (E_STEEL_PA, lambda d: math.pi * d * d / 4, STEEL_STRENGTH_PA),  # solid: the whole section
    "bare_al": (E_ALUMINIUM_PA, lambda d: A50_M2 * (d / A50_D_M) ** 2, A50_BREAK_N / A50_M2),  # A-50's 0.778 fill
    "sip_drop": (E_ALUMINIUM_PA, lambda d: SIP_DROP_M2, A50_BREAK_N / A50_M2),  # the cores; A-50's strength assumed
}
IDLE = {"single": ("spacing_m", "offset_step_m"), "not sip": ("twist_pitch_m",)}
PARAMS = {
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "the UVs' offset along each conductor and a bundle's twist phase, so neighbouring spans differ"},
    "class": {"type": "str", "range": "thin, medium, thick", "default": "medium",
              "effect": "the diameter class: thin 3-6.5 mm, medium 6.5-12.5 mm, thick 12.5-32 mm (check)"},
    "conductor_type": {"type": "str", "range": ", ".join(TYPES), "default": "bare_al",
                       "effect": "surface and colour (the slot), and a SIP's twisted cores"},
    "diameter_mm": {"type": "float", "range": (3.0, 32.0), "default": 9.0,
                    "effect": "the tube's diameter; the PHY-22 input"},
    "mass_kg_per_m": {"type": "float", "range": (0.04, 0.62), "default": 0.135,
                      "effect": "mass per metre, for PHY-22's sag-tension relation"},
    "conductors": {"type": "int", "range": (1, 5), "default": 4, "effect": "wires in the span (a SIP bundle is one)"},
    "arrangement": {"type": "str", "range": "vertical_zigzag, bundle (horizontal, top_triangle: not built)",
                    "default": "vertical_zigzag", "effect": "the conductors' places at the pole head"},
    "spacing_m": {"type": "float", "range": (0.4, 1.0), "default": 0.5,
                  "effect": "vertical_zigzag: the drop from one conductor's attachment to the next"},
    "span_m": {"type": "float", "range": (10.0, 90.0), "default": 40.0, "effect": "pole to pole, horizontal"},
    "sag_ratio": {"type": "float", "range": (0.005, 0.3), "default": 0.02,
                  "effect": "sag = ratio x span, below the chord at mid-span; above 0.05 only on damaged spans"},
    "attach_heights_m.start": {"type": "float", "range": (2.75, 8.0), "default": 7.355,
                               "effect": "the top conductor's port height at the first pole (L01_straight: hook_0)"},
    "attach_heights_m.end": {"type": "float", "range": (2.75, 8.0), "default": 7.355,
                             "effect": "the top conductor's port height at the second pole or the house"},
    "offset_m": {"type": "float", "range": (0.0, 1.5), "default": 0.250579,
                 "effect": "the top conductor's port distance from the pole's axis (L01_straight: hook_0's)"},
    "offset_step_m": {"type": "float", "range": (0.0, 0.01), "default": 0.002632,
                      "effect": "vertical_zigzag: each next port this much further out (the log's taper over a step)"},
    "twist_pitch_m": {"type": "float", "range": (0.3, 0.6), "default": 0.45,
                      "effect": "SIP: the cores' twist pitch (visual)"},
    "damage": {"type": "str", "range": "intact (cut, down: not built)", "default": "intact",
               "effect": "every conductor's state"},
    "points_per_m": {"type": "float", "range": (1.0, 4.0), "default": 2.0,
                     "effect": "points of the wires record and of the tube's rings, per metre of horizontal span"},
    "lod.lod1_switch_m": {"type": "float", "range": (5.0, 100.0), "default": 20.0,
                          "effect": "LOD1 (4-sided tubes, a bundle as one) from here; the cull follows the diameter"},
}


def check(params):
    """Cross-parameter rules once every key is in range: the built types, arrangements and damage; the class holding
    the diameter; a type with its stiffness and strength in MECHANICS; a SIP as one bundle and nothing else bundled;
    idle keys at their defaults; a service drop within its 25 m; an intact sag ratio; every conductor's lowest point
    above its type's clearance."""
    p = params
    problems = []
    for key, allowed, why in (("conductor_type", TYPES, "the sheet's conductor types"),
                              ("arrangement", ARRANGEMENTS, "horizontal and top_triangle are not built yet"),
                              ("damage", DAMAGES, "cut and down are L04's and L05's states, not built yet"),
                              ("class", tuple(CLASSES), "the sheet's classes")):
        if p[key] not in allowed:
            problems.append(f"'{key}' = {p[key]!r} is not built ({why}); use one of {', '.join(allowed)}")
    if problems:
        return problems
    lo, hi = CLASSES[p["class"]]
    if not lo <= p["diameter_mm"] <= hi:
        problems.append(f"'diameter_mm' = {p['diameter_mm']} is outside [{lo}, {hi}] for class {p['class']}")
    if p["conductor_type"] not in MECHANICS:
        problems.append(f"'conductor_type' = {p['conductor_type']!r} has no axial stiffness or breaking force read "
                        f"yet; use one of {', '.join(MECHANICS)}")
    sip = p["conductor_type"] in CORES
    if sip != (p["arrangement"] == "bundle"):
        problems.append(f"'arrangement' = {p['arrangement']!r} with {p['conductor_type']}: a SIP hangs as a bundle, "
                        "every other conductor on vertical_zigzag")
    if p["arrangement"] == "bundle" and p["conductors"] != 1:
        problems.append(f"'conductors' = {p['conductors']}: a bundle span carries one bundle")
    idle = list(IDLE["single"]) if p["conductors"] == 1 else []
    idle += IDLE["not sip"] if not sip else []
    for key in idle:
        if p[key] != PARAMS[key]["default"]:
            problems.append(f"'{key}' = {p[key]} is not read here ({p['conductors']} conductor(s) of "
                            f"{p['conductor_type']}); keep {PARAMS[key]['default']}")
    if p["conductor_type"] == "sip_drop" and p["span_m"] > DROP_SPAN_M:
        problems.append(f"'span_m' = {p['span_m']}: a service drop spans {DROP_SPAN_M:g} m at most, else an extra pole")
    if p["sag_ratio"] > INTACT_SAG_RATIO:
        problems.append(f"'sag_ratio' = {p['sag_ratio']} is over the intact {INTACT_SAG_RATIO}; only a damaged span "
                        "sags more")
    if problems:
        return problems
    floor = CLEARANCE_M[p["conductor_type"]]
    for n, (a, b) in enumerate(attachments(p)):
        low = Catenary(a, b, sag(p)).lowest()
        if low < floor:
            problems.append(f"conductor {n}'s lowest point {low:.3f} m is under the {floor} m clearance of "
                            f"{p['conductor_type']} (the sheet's ПУЕ figures): raise 'attach_heights_m' or lower "
                            "'sag_ratio'")
    return problems


def sag(p):
    return p["sag_ratio"] * p["span_m"]


def mechanics(p):
    """(axial stiffness EA, rated breaking force), both in N, of one conductor or bundle of the span (MECHANICS):
    5 mm steel 3.93 MN and 7 658 N, 9 mm A-50 3.12 MN and 8 198 N, the 2 x 16 mm² drop 2.02 MN and 5 300 N."""
    modulus, section, strength = MECHANICS[p["conductor_type"]]
    area = section(p["diameter_mm"] / 1000)
    return modulus * area, strength * area


def edge_radius(p):
    """The arris the fiber bends over on this conductor where it is sharper than the conductor (task 1.5's reading
    §6), or None: a 7-strand bare conductor's outer strand, D / 6 (A-50: 1.5 mm); a SIP bundle's core; solid steel
    none (its own radius)."""
    d = p["diameter_mm"] / 1000
    if p["conductor_type"] == "bare_al":
        return d / 6
    if p["conductor_type"] in CORES:
        return core_diameter(d, CORES[p["conductor_type"]]) / 2
    return None


def core_diameter(diameter, cores):
    """Each of `cores` equal cores packed in a bundle of `diameter`: D sin(pi / k) / (1 + sin(pi / k))."""
    s = math.sin(math.pi / cores)
    return diameter * s / (1 + s)


def cull_distance(p):
    """Where the line's column sum, PX_PER_RAD * D / d pixels, falls to CULL_COVERAGE: beyond LOD1's 100 m at most for
    every diameter (281 m at 3 mm)."""
    return PX_PER_RAD * p["diameter_mm"] / 1000 / CULL_COVERAGE


def attachments(p):
    """Each conductor's two ends, from the top: its centre resting in the insulator's groove, one radius out from
    its port."""
    r = p["diameter_mm"] / 2000
    ends = []
    for n in range(p["conductors"]):
        side = 1.0 if n % 2 == 0 else -1.0
        z = side * (p["offset_m"] + n * p["offset_step_m"] + r)
        drop = n * p["spacing_m"]
        ends.append(((0.0, p["attach_heights_m"]["start"] - drop, z), (p["span_m"], p["attach_heights_m"]["end"] - drop, z)))
    return ends


class Catenary:
    """The catenary through the points a and b in the vertical plane through them, hanging `sag` below their chord
    at mid-span: y(u) = y_a + 2 c sinh((u - 2 m) / 2c) sinh(u / 2c) at the horizontal distance u from a (the
    difference form of c cosh((u - m) / c), free of cancellation), c found by bisection on its logarithm to float
    precision (the mid-span sag falls as c grows), m from the two ends' heights."""

    def __init__(self, a, b, sag_m):
        self.a, self.b = tuple(a), tuple(b)
        self.length = math.hypot(b[0] - a[0], b[2] - a[2])
        lo, hi = math.log(self.length / 40), math.log(self.length * 1e6)
        for _ in range(100):
            self._solve(math.exp((lo + hi) / 2))
            if (a[1] + b[1]) / 2 - self.y(self.length / 2) > sag_m:
                lo = math.log(self.c)  # too slack: a larger parameter
            else:
                hi = math.log(self.c)
        self._solve(math.exp((lo + hi) / 2))

    def _solve(self, c):
        self.c = c
        rise = self.b[1] - self.a[1]
        self.m = self.length / 2 - c * math.asinh(rise / (2 * c * math.sinh(self.length / (2 * c))))

    def y(self, u):
        return self.a[1] + 2 * self.c * math.sinh((u - 2 * self.m) / (2 * self.c)) * math.sinh(u / (2 * self.c))

    def lowest(self):
        """The curve's lowest height between its ends: at u = m when that lies inside the span, else the lower end."""
        return self.y(self.m) if 0 < self.m < self.length else min(self.a[1], self.b[1])

    def points(self, count):
        """count + 1 points from a to b, evenly spaced in horizontal distance; the ends exactly a and b."""
        out = [self.a]
        for i in range(1, count):
            t = i / count
            out.append((self.a[0] + t * (self.b[0] - self.a[0]), self.y(t * self.length),
                        self.a[2] + t * (self.b[2] - self.a[2])))
        return out + [self.b]


def build(params):
    p = params
    rng = random.Random(p["seed"])
    radius = p["diameter_mm"] / 2000
    slot = SLOTS[p["conductor_type"]]
    cores = CORES.get(p["conductor_type"], 1)
    lods = [Mesh(), Mesh()]
    wires, snag = [], []
    stiffness, strength = mechanics(p)
    edge = edge_radius(p)
    for a, b in attachments(p):
        u0, phase = rng.uniform(0.0, 1.0), rng.uniform(0.0, 2 * math.pi)
        curve = Catenary(a, b, sag(p))
        points = curve.points(max(1, math.ceil(curve.length * p["points_per_m"] - 1e-9)))
        if cores == 1:
            _tube(lods[0], points, radius, SIDES[0], slot, u0)
        else:
            arc = sum(norm(sub(q, r)) for q, r in zip(points[1:], points))
            axis = curve.points(math.ceil(arc / (p["twist_pitch_m"] / TWIST_RINGS)))
            lines, core_r = twisted_cores(axis, p["diameter_mm"] / 1000, cores, p["twist_pitch_m"], phase)
            for line in lines:
                _tube(lods[0], line, core_r, SIDES[0], slot, u0)
        _tube(lods[1], points, radius, SIDES[1], slot, u0)
        record = [list(q) for q in points]
        wires.append({"slot": slot, "diameter_m": 2 * radius, "mass_kg_per_m": p["mass_kg_per_m"], "sag_m": sag(p),
                      "axial_stiffness_n": stiffness, "breaking_force_n": strength, "material": PHYSICS,
                      "damage": p["damage"], "points": record})
        feature = {"kind": "edge", "points": [list(q) for q in record], "radius_m": radius}
        if edge is not None:
            feature["edge_radius_m"] = edge
        feature["material"] = PHYSICS
        snag.append(feature)
    return {
        "lods": lods,
        "switch_distances_m": [0.0, p["lod"]["lod1_switch_m"]],
        "cull_distance_m": cull_distance(p),
        "materials": [{"slot": slot, "physics": PHYSICS}],
        "collision": [], "gaps": [], "wind_volumes": [], "ports": [],
        "wires": wires,
        "snag": snag,
    }


def twisted_cores(axis, diameter, cores, pitch, phase):
    """The centre lines of `cores` equal cores packed in a bundle of `diameter` and twisted about the `axis`
    polyline at `pitch` (each core D sin(pi / k) / (1 + sin(pi / k)) across, its centre (D - d) / 2 off the axis,
    turning by arc length from `phase`), and the cores' radius."""
    core_d = core_diameter(diameter, cores)
    rho = (diameter - core_d) / 2
    along = normalize((axis[-1][0] - axis[0][0], 0.0, axis[-1][2] - axis[0][2]))
    side = (-along[2], 0.0, along[0])  # horizontal, square to the span
    last = len(axis) - 1
    lines, travelled = [[] for _ in range(cores)], 0.0
    for i, q in enumerate(axis):
        if i:
            travelled += norm(sub(q, axis[i - 1]))
        up = normalize(cross(side, normalize(sub(axis[min(i + 1, last)], axis[max(i - 1, 0)]))))
        for k, line in enumerate(lines):
            angle = phase + 2 * math.pi * (travelled / pitch + k / cores)
            line.append(add(q, add(scale(side, rho * math.cos(angle)), scale(up, rho * math.sin(angle)))))
    return lines, core_d / 2


def _tube(mesh, centre, radius, sides, slot, u0):
    """A tube of `sides` along the polyline `centre`, both ends capped: rings square to the bisected tangent, framed
    by parallel transport, every corner on the circle of `radius` with its radial unit normal and (radius, 0) as its
    second UV (the thin-wire shader's inputs); one quad per side and segment with its outward normal. UVs in metres:
    U along from u0, V around."""
    last = len(centre) - 1
    tangents = [normalize(sub(centre[min(i + 1, last)], centre[max(i - 1, 0)])) for i in range(len(centre))]
    t0 = tangents[0]
    e = normalize(cross(t0, (0.0, 0.0, 1.0) if abs(t0[2]) < 0.9 else (1.0, 0.0, 0.0)))
    rings, along = [], [u0]
    for i, (q, t) in enumerate(zip(centre, tangents)):
        if i:
            e = normalize(sub(e, scale(t, dot(e, t))))
            along.append(along[-1] + norm(sub(q, centre[i - 1])))
        f = cross(t, e)
        ring = []
        for a in (2 * math.pi * j / sides for j in range(sides)):
            out = add(scale(e, math.cos(a)), scale(f, math.sin(a)))
            ring.append(mesh.vertex(add(q, scale(out, radius)), normal=out, uv2=(radius, 0.0)))
        rings.append(ring)
    step = 2 * math.pi * radius / sides
    for i in range(last):
        mid = scale(add(centre[i], centre[i + 1]), 0.5)
        for j in range(sides):
            k = (j + 1) % sides
            quad = [rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]]
            uvs = [(along[i], j * step), (along[i], (j + 1) * step), (along[i + 1], (j + 1) * step),
                   (along[i + 1], j * step)]
            outward = _outward(mesh, quad, mid)
            for a, b, c in ((0, 1, 2), (0, 2, 3)):  # triangles here: see below
                mesh.face([quad[a], quad[b], quad[c]], outward, slot, [uvs[a], uvs[b], uvs[c]])
    # The caps fanned too. Blender keeps a custom normal relative to its corner fan, and the FBX export's own
    # triangulation would re-cut the fans: at an end ring, whose radial normal stands far from the fan's average with
    # the cap, the re-cut turns it by up to 45 degrees. A mesh that is all triangles is exported as it is.
    for ring, outward in ((rings[0], scale(tangents[0], -1.0)), (rings[last], tangents[last])):
        for j in range(1, sides - 1):
            mesh.face([ring[0], ring[j], ring[j + 1]], outward, slot)


def _outward(mesh, indices, inside):
    """The polygon's unit normal (Newell), turned away from the point `inside`."""
    pts = [mesh.vertices[i] for i in indices]
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    n = normalize(tuple(n))
    centre = scale(tuple(sum(q[a] for q in pts) for a in range(3)), 1.0 / len(pts))
    return n if dot(n, sub(centre, inside)) >= 0 else scale(n, -1.0)
