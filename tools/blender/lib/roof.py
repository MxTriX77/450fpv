"""The house's roof, chimney and ceiling (p1-h01-adobe-house, design decisions 4, 6, 8 and 9; object sheet
docs/object-sheets/houses-and-yard.md §Shared "Roofs" and §H01): the carpentry with every rafter, batten and collar,
every clay tile hung on its batten with its head lap, half-round ridge and hip tiles, the brick chimney built of K01's
bricks, and the ceiling's beam, joists, boards and clay. Plain Python on lib.prism; `plan` is lib.walls.Plan.

The roof's structural plane is the rafters' top faces (Plan.rt): each slope is a plane at `roof_pitch` from its eaves
line, `eaves_overhang` out from the walls, up to the ridge; a hip end's slope has the same pitch, so its eaves run
the same and its two hips meet the ridge at Plan.hip_ridge_end. A slope's coordinates: `a` along its eaves, `s` up the
slope from the eaves line, `n` out of the roof from the structural plane.

Carpentry (assumed sections, labelled where the sheet gives them): rafters `rafter_m` at `rafter_pitch` (the sheet:
0.10-0.15 m at 0.9-1.2 m), laid out from the hip's ridge-end pair (or the gable) so a bay is the pitch less a rafter;
the first pair against a gable's inner face; jacks on the side slopes and the hip end at the same pitch, cut on the hip
rafters' side faces; hip rafters HIP_RAFTER_M, dropped so their top arrises lie in the two planes; a ridge board; a
collar on every pair between (COLLAR_M at COLLAR_DROP of the rise below the ridge); battens BATTEN_M at
`batten_pitch`, mitred on the hips' centre planes, square at a gable's verge, cut round the chimney; a tilting fillet
under the eaves course.

Tiles: the grooved strip tile of ГОСТ 1808-71 (table 1, пазова стрічкова: overall 400 x 220 mm, covering 333 x 200 mm,
so a 67 mm head lap on battens at the 333 mm covering length; published,
http://standartgost.ru/g/%D0%93%D0%9E%D0%A1%D0%A2_1808-71): length = batten pitch + head lap. Each tile hangs by its
nib (lib.debris_tile.NIB_M) on its batten, its head on the batten's top and its tail on the course below, so it tilts
by TILT_RISE; it is K02's tile shape at LOD1 (a camber of two planes and the nib) and maps its clay into one of the
clay set's clean cells (lib.debris_tile.CELLS_M), seeded per tile, so the roof never repeats. Tiles are laid from a
gable's verge (or a hip slope's centre) at the covering width less a 2 mm joint, cut along the hips HIP_MARGIN_M short
of the hip line and round the chimney; a cut face is `tile_fracture`, the fresh clay. The eaves course overhangs the
rafter tails by `e`, at least TAIL_MIN_M, set so every batten keeps the gauge and the top course's head stops
RIDGE_GAP_M short of the ridge. `roof_strip` takes that share of the tiles away (_strip): up to SLIP of them slipped,
seeded and scattered, the rest stripped so the tiles left cluster at the eaves, the lee end and in seeded patches.

The D state (task 1.4), each item from its own named stream: `rafters_broken` commons off the plate, fallen into the
attic with their collars; `batten_break` of the spans between two commons broken where the tiles over them are gone,
each leaving a stub on either rafter that ends in K03's splinters, the middle fallen; each run of broken rows in a bay
a gap between the rafters' faces and the battens left standing, less the stubs (a bay is a gap only where no batten
stands); `interior_ceiling_down` of the ceiling down in strips between its joists, the boards broken over their
joists, each hole a gap. What fell is listed in the result's "fallen" and "holes" for lib.rubble.

Collision: plates, rafters, collars, the ridge board, battens and joists one box each; a tile course one box per run of
whole tiles in the tile's own tilt, a box per tile cut round the chimney, and the tiles cut along a hip as strips along
the course, each STRIP_M of the hip's run wide, so the box's corner stays within half a strip of the cut line; the
ridge and each hip one box over their arches; the chimney a box per wall of its flue and per wall of each cap course.
"""
import math

from generators import debris_brick
from lib import audit, debris_tile, debris_timber, kits_vehicle, masks, prism, rects, rubble, walls
from lib.geometry import Mesh

TIMBER, MASONRY = "timber", "masonry"
TIMBER_SLOT, TILE_SLOT, CUT_SLOT, BRICK_SLOT, CLAY_SLOT = "timber", "tile", "tile_fracture", "brick", "clay"
CUT_TIMBER_SLOT = debris_timber.FRACTURE_SLOT  # a broken member's fresh wood (K03's)
BATTEN_M = (0.05, 0.05)        # the sheet: battens 50 x 50 mm
HIP_RAFTER_M = (0.10, 0.20)    # wide, deep (assumed: a hip deeper than the commons)
RIDGE_BOARD_M = (0.04, 0.25)   # thick, deep (assumed)
COLLAR_M = (0.05, 0.15)        # thick, deep (assumed)
COLLAR_DROP = 0.35             # the collar's top, as a share of the rise below the ridge (assumed)
TILE_JOINT_M = 0.002           # between tiles in a course: the interlock's seam (assumed)
HIP_MARGIN_M = 0.01            # tiles stop short of a hip line, under its hip tiles (assumed)
CHIMNEY_GAP_M = 0.01           # tiles and battens stop short of the chimney, under its fillet (assumed)
TAIL_MIN_M = 0.04              # the eaves course's least overhang of the rafter tails (assumed)
RIDGE_GAP_M = 0.02             # the top course's head below the ridge, under the ridge tiles (assumed)
STRIP_M = 0.08                 # a hip strip's run along the course (derived: its corner within 3.2 cm of the cut)
MIN_PIECE_M2, MIN_PIECE_M = 0.003, 0.03  # a cut piece smaller than this is not laid (assumed)
ARCH_R_M = 0.10                # ridge and hip tiles: ГОСТ 1808-71 table 2, коньковая 365 x 200 mm overall
ARCH_PITCH_M = 0.333           # its covering length (same table); laid abutting, each 3 mm short
ARCH_T_M = 0.014               # its clay, as the tiles'
ARCH_SEGMENTS = (8, 4)         # round the arch, LOD0 and LOD1
BRICK_M = (0.25, 0.065, 0.12)  # AST-04, K01
JOINT_M = 0.010                # the sheet: 10-12 mm joints
CAP_COURSES, CAP_OUT_M = 2, 0.03  # the sheet: a 2-course cap (E 7); stepping out (assumed, the cornice's 30-60 mm)
MORTAR_INSET_M = 0.005         # joints struck back this far (assumed)
BEVEL_MM = (2.0, 3.0)          # K01's worn arrises, on the bricks above the roof
FILLET_M = (0.05, 0.05)        # the chimney's mortar fillet on the tiles, wide x high (assumed)
JOIST_M = (0.15, 0.15)         # the sheet: joists 0.15-0.20 m
JOIST_PITCH_M = 0.8            # design decision 6 (assumed)
BEAM_M = (0.20, 0.25)          # the sheet: the main beam 0.20 x 0.25 m
BOARD_M = (0.15, 0.025, 0.005)  # ceiling boards: width, thickness, joint (assumed)
CLAY_M = 0.15                  # the clay-straw layer on the boards (the sheet: 0.15-0.25 m)
MIN_JOIST_M = 0.3              # a joist cut by the chimney shorter than this is left out (assumed)
EDGE_SNAG_R_M = 0.002          # sawn and brick arrises (K03's, K01's chamfer)
ROOF_FIELD_POROSITY = 0.775    # the sheet: a stripped field passes 0.7-0.85 (its battens and rafters block 15-30 %)
# The D state (task 1.4)
SLIP = 0.05                    # the sheet: I's 2-5 % of tiles slipped, scattered; a stripped roof's own slips
CLUSTER_CELL_M = 1.2           # the tiles left lie in patches of a few tiles (assumed: E shows clusters 1-2 m across)
STUB_BODY_M = (0.03, 0.25)     # a broken batten's stub from its rafter's face to its splinters' root (assumed)
STUB_SPLINTER_M = (0.04, 0.12) # their reach past it (assumed; K03's 0.05-0.4 m, short on a 50 mm batten)
SPLINTERS = 3                  # per broken end of a batten or a ceiling board (K03: 2-10)
MIN_FALLEN_M = 0.15            # a broken span's fallen middle is at least this (assumed)
CEILING_CELL_BOARDS = 4        # the ceiling falls in strips of this many boards between two joists (assumed)
BOARD_SPLINTER_M = (0.02, 0.04)  # a ceiling board's splinters over its joist (derived: within the joist's top)
BOARD_SPLINTERS = 2
CLAY_SETBACK_M = (0.0, 0.08)   # the clay crumbles back from a broken board's end (assumed)


class Slope:
    """One roof plane (`front`, `back`, or a hip end `end-1` / `end1`): its eaves origin, its axes (ea along the
    eaves, es up the slope, en out), its length S from the eaves line to the ridge, and its a range at s."""

    def __init__(self, plan, name):
        c, s = plan.cos, plan.sin
        self.plan, self.name = plan, name
        ze = plan.Z + plan.eaves
        ye = plan.apex - ze * plan.tan
        self.S = ze / c
        if name in ("front", "back"):
            sz = -1 if name == "front" else 1
            self.side, self.end = sz, None
            self.origin, self.ea = (0.0, ye, sz * ze), (1.0, 0.0, 0.0)
            self.es, self.en = (0.0, s, -sz * c), (0.0, c, sz * s)
        else:
            e = -1 if name == "end-1" else 1
            self.side, self.end = None, e
            self.origin, self.ea = (e * (plan.X + plan.eaves), ye, 0.0), (0.0, 0.0, 1.0)
            self.es, self.en = (-e * c, s, 0.0), (e * s, c, 0.0)

    def point(self, a, s, n=0.0):
        return tuple(self.origin[k] + a * self.ea[k] + s * self.es[k] + n * self.en[k] for k in range(3))

    def bounds(self):
        """[(lo or hi, kind, hip)] of this slope: kind "verge" (a constant) or "hip" (a + s cos fixed), with the hip's
        (end, side)."""
        p = self.plan
        if self.end is None:
            return [("lo", "verge" if p.ends[-1] == "gable" else "hip", (-1, self.side)),
                    ("hi", "verge" if p.ends[1] == "gable" else "hip", (1, self.side))]
        # a hip end's a is z: its lo bound is the hip on the -Z side
        return [("lo", "hip", (self.end, -1)), ("hi", "hip", (self.end, 1))]

    def a_range(self, s):
        p = self.plan
        if self.end is not None:
            half = (p.Z + p.eaves) - s * p.cos
            return -half, half
        lo = -(p.X + p.verge) if p.ends[-1] == "gable" else -(p.X + p.eaves) + s * p.cos
        hi = (p.X + p.verge) if p.ends[1] == "gable" else (p.X + p.eaves) - s * p.cos
        return lo, hi


def hip_geometry(plan, end, side):
    """A hip's ridge end R, eaves corner C (on the structural plane) and p, the horizontal unit square to it in
    plan pointing into the hip end's slope."""
    r = (plan.hip_ridge_end(end), plan.apex, 0.0)
    ze = plan.Z + plan.eaves
    c = (end * (plan.X + plan.eaves), plan.apex - ze * plan.tan, side * ze)
    p = (end / math.sqrt(2), 0.0, -side / math.sqrt(2))
    return r, c, p


class Layout:
    """The courses: the battens' s, each course's tail and head, the tile's tilt rise and the eaves overhang."""

    def __init__(self, plan, params, S):
        t = params["tile_m"]
        self.L, self.w, self.cover = t["length"], t["width"], t["cover_width"]
        self.t, self.camber = t["thickness"], t["camber"]
        self.gauge = params["batten_pitch"]
        self.lap = self.L - self.gauge
        nib = debris_tile.NIB_M[0]
        head_past = BATTEN_M[1] / 2 + nib  # the head stands this far up past its batten's centre
        # the tilt: the tail rests on the course below's top a lap in from its head, camber included (derived)
        self.rise = (self.t + 2 * self.camber * self.lap / self.L) / (1 - self.lap / self.L)
        self.alpha = math.asin(self.rise / self.L)
        self.Ls = self.L * math.cos(self.alpha)  # a tile's run up the slope
        n = math.ceil((S - RIDGE_GAP_M - self.Ls + TAIL_MIN_M) / self.gauge)
        self.e = self.Ls + self.gauge * n - S + RIDGE_GAP_M  # the eaves course's tail past the rafter tails
        self.battens = [-self.e + self.Ls - head_past + k * self.gauge for k in range(n + 1)]
        self.heads = [s + head_past for s in self.battens]
        self.tails = [h - self.Ls for h in self.heads]
        self.n_head = BATTEN_M[1]
        self.n_tail = BATTEN_M[1] + self.rise
        # the tilting fillet under the eaves course's tail, on the rafter tails (BATTEN_M wide)
        s_f = BATTEN_M[0] / 2
        self.fillet = self.n_head + (self.heads[0] - s_f) / self.Ls * self.rise


def chimney_place(plan, params, commons):
    """The chimney's footprint (x0, x1, z0, z1) and top, its centre in the bay of `commons` nearest `chimney.pos`,
    its near face `chimney.offset_m` behind the ridge line (+ the back slope), or None."""
    ch = params["chimney"]
    if not ch["present"]:
        return None
    size = 2 * BRICK_M[0] + JOINT_M
    want = -plan.X + ch["pos"] * plan.L
    mids = [(a + b) / 2 for a, b in zip(commons, commons[1:])]
    xc = min(mids, key=lambda m: abs(m - want))
    off = ch["offset_m"]
    z0 = off if off >= 0 else off - size
    courses = round((plan.apex + ch["height_above_ridge_m"] - plan.F - BRICK_M[1]) / (BRICK_M[1] + JOINT_M)) + 1
    top = plan.F + (courses - 1) * (BRICK_M[1] + JOINT_M) + BRICK_M[1]
    return {"x0": xc - size / 2, "x1": xc + size / 2, "z0": z0, "z1": z0 + size, "courses": courses, "top": top,
            "size": size}


def rafter_layout(plan, pitch):
    """(commons, forced): the common rafters' centres in x, and those set by a hip's ridge end or a gable's inner
    face, which carry no collar. The lattice runs at `pitch` from the +X hip's ridge-end pair, else the -X one, else
    the -X gable's pair."""
    w = plan.rafter_w
    forced = []
    for end in (1, -1):
        if plan.ends[end] == "hip":
            forced.append(plan.hip_ridge_end(end) - end * w / 2)
        else:
            forced.append(end * (plan.X - plan.T - w / 2))
    lo = min(forced)
    hi = max(forced)
    ref = forced[0] if plan.ends[1] == "hip" else forced[1]
    k_lo, k_hi = math.ceil((lo - ref) / pitch - 1e-9), math.floor((hi - ref) / pitch + 1e-9)
    lattice = [ref + k * pitch for k in range(k_lo, k_hi + 1)]
    lattice = [x for x in lattice if all(abs(x - f) > w + 0.15 for f in forced)]
    return sorted(lattice + forced), forced, ref


# --- the build ------------------------------------------------------------------------------------------------------

def build(plan, params, lods, rngs):
    """Every roof member, tile, ridge tile, the chimney and the ceiling into `lods` (four meshes); returns
    {"collision", "wind_volumes", "snag", "gaps", "lines", "chimney", "tiles", "layout", "commons"} and the damage's
    "gone_tiles", "fallen" (battens, rafters, collars, boards, clay) and "holes" (the ceiling's, per room)."""
    out = {"collision": [], "wind_volumes": [], "snag": [], "gaps": [], "lines": []}
    pitch = params["rafter_pitch"]
    commons, forced, ref = rafter_layout(plan, pitch)
    chimney = chimney_place(plan, params, commons)
    slopes = [Slope(plan, "front"), Slope(plan, "back")] + [Slope(plan, f"end{e}") for e in (-1, 1)
                                                               if plan.ends[e] == "hip"]
    layout = Layout(plan, params, slopes[0].S)
    out["fallen"], out["holes"] = [], []
    pieces = _tile_pieces(plan, slopes, layout, chimney, rngs)
    gone = _strip(plan, params, pieces, rngs)
    out["gone_tiles"] = [pieces[i] for i in sorted(gone)]
    broken = _pick_rafters(plan, params, slopes, commons, forced, chimney, pieces, gone, rngs)
    bays = _bays(plan, slopes, layout, commons, broken, chimney)
    breaks = _pick_breaks(params, layout, bays, pieces, gone, rngs)
    _plates(plan, lods, out)
    _rafters(plan, slopes, commons, forced, ref, pitch, chimney, lods, out, broken)
    _battens(plan, slopes, layout, chimney, lods, out, breaks, rngs)
    tiles = _tiles(plan, slopes, layout, chimney, lods, out, pieces, gone)
    out["gaps"] += _bay_gaps(plan, layout, bays, breaks, pieces, gone)
    _ridge_tiles(plan, slopes, layout, lods, out, rngs)
    if chimney:
        _chimney(plan, slopes, layout, chimney, lods, out, rngs)
    if params["interior_ceiling_down"] > 0:
        _ceiling_down(plan, params, chimney, lods, out, rngs)
    else:
        _ceiling(plan, chimney, lods, out)
    _far_cover(plan, slopes, layout, lods, None if params["roof_strip"] <= SLIP else out["regions"])
    tiles["breaks"], tiles["spans"], tiles["rafters_off"] = len(breaks), sum(len(b["rows"]) for b in bays), len(broken)
    porosity = params["roof_strip"] * ROOF_FIELD_POROSITY
    for slope in slopes:
        lo = min(slope.a_range(0.0)[0], slope.a_range(slope.S)[0])
        hi = max(slope.a_range(0.0)[1], slope.a_range(slope.S)[1])
        frame = prism.Frame(slope.origin, slope.ea, slope.en, prism.cross(slope.ea, slope.en))
        sign = prism.dot(frame.ez, slope.es)
        z0, z1 = sorted((-sign * layout.e, sign * slope.S))
        box = prism.Member(frame, (lo, -plan.rafter_d, z0), (hi, layout.n_tail + layout.t + layout.camber, z1)).box(TIMBER)
        box.pop("material")
        box.pop("landable")
        box["porosity"] = porosity
        out["wind_volumes"].append(box)
    out["chimney"], out["tiles"], out["layout"], out["commons"] = chimney, tiles, layout, commons
    return out


def _pick_rafters(plan, params, slopes, commons, forced, chimney, pieces, gone, rngs):
    """The `rafters_broken` commons knocked off the plate, as (slope, x): on the side slopes, not a hip's or a gable's
    pair nor the chimney's trimmers, those with the most of their tiles gone (within a pitch either side), ties broken
    by the seeded stream "rafters broken"."""
    if not params["rafters_broken"]:
        return frozenset()
    rng = rngs("rafters broken")
    pitch = params["rafter_pitch"]
    ch_slope = None if not chimney else ("front" if chimney["z1"] <= 0 else "back")
    candidates = []
    for slope in slopes[:2]:
        for x in commons:
            key = rng.random()
            if any(abs(x - f) < 1e-9 for f in forced):
                continue
            if slope.name == ch_slope and chimney["x0"] - pitch < x < chimney["x1"] + pitch:
                continue
            near = [i for i, pc in enumerate(pieces) if pc["slope"] is slope
                    and abs(sum(q[0] for q in pc["poly"]) / len(pc["poly"]) - x) < pitch]
            share = sum(i in gone for i in near) / max(1, len(near))
            candidates.append((-share, key, slope.name, x))
    return frozenset((name, x) for _, _, name, x in sorted(candidates)[:params["rafters_broken"]])


def _bays(plan, slopes, layout, commons, broken, chimney):
    """The regular bays of the side slopes, between two neighbouring commons still on the plate (the bays a batten
    breaks in and a gap opens through; the hip's jacks, a gable's verge and the chimney's bay are not), each with its
    rafters' centres `x`, its clear `a` between their faces and its batten rows."""
    w = plan.rafter_w
    ch_slope = None if not chimney else ("front" if chimney["z1"] <= 0 else "back")
    out = []
    for slope in slopes[:2]:
        xs = sorted(x for x in commons if (slope.name, x) not in broken)
        for x0, x1 in zip(xs, xs[1:]):
            if slope.name == ch_slope and x0 < chimney["x1"] and chimney["x0"] < x1:
                continue
            out.append({"slope": slope, "x": (x0, x1), "a": (x0 + w / 2, x1 - w / 2),
                        "rows": list(range(len(layout.battens)))})
    return out


def _over(pieces, gone, name, k, a0, a1):
    """Whether every tile of course k on slope `name` over a0-a1 is gone."""
    return all(i in gone for i, pc in enumerate(pieces) if pc["slope"].name == name and pc["k"] == k
               and max(q[0] for q in pc["poly"]) > a0 and min(q[0] for q in pc["poly"]) < a1)


def _pick_breaks(params, layout, bays, pieces, gone, rngs):
    """`batten_break` of the regular bays' batten spans broken (task 1.4: of the spans that can break), drawn by the
    stream "batten break" among the spans whose tiles are gone from their own course and the course above (a tile
    hangs on its batten and the next course laps over it), never the eaves or the top row. Each break leaves a stub
    of STUB_BODY_M on each rafter, splintered by STUB_SPLINTER_M; the middle fell. Returns the breaks, by bay and
    row."""
    total = sum(len(b["rows"]) for b in bays)
    target = round(params["batten_break"] * total)
    if not target:
        return []
    n = len(layout.battens)
    candidates = [(bi, k) for bi, b in enumerate(bays) for k in range(1, n - 1)
                  if _over(pieces, gone, b["slope"].name, k, *b["a"]) and _over(pieces, gone, b["slope"].name, k + 1, *b["a"])]
    chosen = sorted(rngs("batten break").sample(candidates, min(target, len(candidates))))
    breaks = []
    for bi, k in chosen:
        b = bays[bi]
        a0, a1 = b["a"]
        name = f"batten break {b['slope'].name} {bi} {k}"
        r = rngs(name)
        body = [r.uniform(*STUB_BODY_M), r.uniform(*STUB_BODY_M)]
        reach = [r.uniform(*STUB_SPLINTER_M), r.uniform(*STUB_SPLINTER_M)]
        room = (a1 - a0) - MIN_FALLEN_M - sum(reach)
        if sum(body) > room:
            body = [v * max(0.0, room) / sum(body) for v in body]
        breaks.append({"bay": bi, "slope": b["slope"], "k": k, "roots": (a0 + body[0], a1 - body[1]),
                       "reach": reach, "name": name, "notches": []})
    return breaks


def _plates(plan, lods, out):
    """The wall plates on the long walls (from a gable's inner face, or to the hip end's plate) and on a hip end."""
    w, h = walls.PLATE_M
    zc = plan.Z - plan.T / 2
    for sz in (-1, 1):
        x0 = -(plan.X - plan.T) if plan.ends[-1] == "gable" else -(plan.X - plan.T / 2 + w / 2)
        x1 = (plan.X - plan.T) if plan.ends[1] == "gable" else (plan.X - plan.T / 2 + w / 2)
        frame = prism.Frame((0.0, plan.H, sz * zc), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
        for mesh in lods[:2]:
            _, box = prism.member(mesh, frame, (x0, 0.0, -w / 2), (x1, h, w / 2), TIMBER_SLOT, TIMBER,
                                  offset=(0.1 * sz, 0.3))
        out["collision"].append(box)
    for end in (-1, 1):
        if plan.ends[end] != "hip":
            continue
        xc = end * (plan.X - plan.T / 2)
        frame = prism.Frame((xc, plan.H, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (-1.0, 0.0, 0.0))
        zz = zc - w / 2
        for mesh in lods[:2]:
            _, box = prism.member(mesh, frame, (-zz, 0.0, -w / 2), (zz, h, w / 2), TIMBER_SLOT, TIMBER,
                                  offset=(0.2, 0.1))
        out["collision"].append(box)


def _rafter_frame(slope, a):
    return prism.Frame(slope.point(a, 0.0), slope.es, slope.en, prism.cross(slope.es, slope.en))


def _tail_snag(member, out):
    """An `edge` round a rafter's tail: its end face's outline."""
    face = min(member.faces, key=lambda f: prism.dot(f[1], member.frame.ex))
    out["snag"].append({"kind": "edge", "radius_m": EDGE_SNAG_R_M, "points": [list(p) for p in face[0] + face[0][:1]]})


def _rafters(plan, slopes, commons, forced, ref, pitch, chimney, lods, out, broken=frozenset()):
    """Commons (with collars and the ridge board), jacks, hip rafters and a hip end's king common and jacks; the
    commons in `broken` ((slope, x)) are off the plate, fallen inside with their collar (out["fallen"])."""
    w, d = plan.rafter_w, plan.rafter_d
    front, back = slopes[0], slopes[1]
    ridge_planes = {s.name: [prism.plane((0.0, 0.0, s.side * RIDGE_BOARD_M[0] / 2), (0.0, 0.0, -s.side))]
                    for s in (front, back)}
    hips = {(e, sz): hip_geometry(plan, e, sz) for e in (-1, 1) if plan.ends[e] == "hip" for sz in (-1, 1)}
    wh = HIP_RAFTER_M[0]

    def side_face(e, sz, toward_end):
        r, _, p = hips[e, sz]
        k = 1 if toward_end else -1
        point = tuple(r[i] + k * wh / 2 * p[i] for i in range(3))
        return prism.plane(point, tuple(-k * c for c in p))

    def emit(slope, a, planes, tail=True):
        frame = _rafter_frame(slope, a)
        box = None
        for n, mesh in enumerate(lods[:2]):
            m, b = prism.member(mesh, frame, (0.0, -d, -w / 2), (slope.S + 0.5, 0.0, w / 2), TIMBER_SLOT, TIMBER,
                                planes, offset=(0.07 * a, 0.13 * a))
            if m is None:
                return
            if n == 0:
                box, member = b, m
        out["collision"].append(box)
        if tail:
            _tail_snag(member, out)

    # a jack must still cross its plate's outer arris: its plan run from the eaves longer than this
    reach = plan.eaves + plan.T / 2 - walls.PLATE_M[0] / 2 + 0.1
    for slope in (front, back):
        for x in commons:
            if (slope.name, x) in broken:
                m = prism.Member(_rafter_frame(slope, x), (0.0, -d, -w / 2), (slope.S + 0.5, 0.0, w / 2),
                                 ridge_planes[slope.name])
                lo, hi = m.local_bounds()
                out["fallen"].append({"kind": "rafter", "slope": slope.name, "x": x, "length": hi[0] - lo[0],
                                      "section": (w, d), "volume_m3": (hi[0] - lo[0]) * w * d})
                continue
            emit(slope, x, ridge_planes[slope.name])
        for (e, sz), (r, c, p) in hips.items():
            if sz != slope.side:
                continue
            m_lo, m_hi = math.floor((-plan.X - plan.eaves - ref) / pitch), math.ceil((plan.X + plan.eaves - ref) / pitch)
            for m in range(m_lo, m_hi + 1):  # the lattice beyond the hip's ridge end
                x = ref + m * pitch
                if e * x <= e * plan.hip_ridge_end(e) + wh or e * x >= plan.X + plan.eaves - reach:
                    continue
                emit(slope, x, [side_face(e, sz, False)])
    # collars between the commons that are neither a gable's nor a hip's ridge-end pair
    yc = plan.apex - COLLAR_DROP * (plan.apex - plan.H)
    for x in commons:
        if any(abs(x - f) < 1e-9 for f in forced):
            continue
        if (front.name, x) in broken or (back.name, x) in broken:  # the collar came down with its rafter
            out["fallen"].append({"kind": "collar", "x": x, "length": 2 * plan.Z, "section": COLLAR_M,
                                  "volume_m3": 2 * plan.Z * COLLAR_M[0] * COLLAR_M[1]})
            continue
        frame = prism.Frame((x + w / 2 + COLLAR_M[0] / 2, yc, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (-1.0, 0.0, 0.0))
        planes = [prism.plane(s.origin, s.en) for s in (front, back)]
        for n, mesh in enumerate(lods[:2]):
            _, box = prism.member(mesh, frame, (-plan.Z, -COLLAR_M[1], -COLLAR_M[0] / 2),
                                  (plan.Z, 0.0, COLLAR_M[0] / 2), TIMBER_SLOT, TIMBER, planes, offset=(0.3, 0.1 * x))
        out["collision"].append(box)
    # the ridge board, from a gable's inner face or a hip's ridge end to the other
    x0 = -(plan.X - plan.T) if plan.ends[-1] == "gable" else plan.hip_ridge_end(-1)
    x1 = (plan.X - plan.T) if plan.ends[1] == "gable" else plan.hip_ridge_end(1)
    frame = prism.Frame((0.0, plan.apex, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
    for mesh in lods[:2]:
        _, box = prism.member(mesh, frame, (x0, -RIDGE_BOARD_M[1], -RIDGE_BOARD_M[0] / 2),
                              (x1, 0.0, RIDGE_BOARD_M[0] / 2), TIMBER_SLOT, TIMBER, offset=(0.2, 0.0))
    out["collision"].append(box)
    # hip rafters, and the hip end's king common and jacks
    drop = wh / 2 / math.sqrt(2) * plan.tan
    for (e, sz), (r, c, p) in hips.items():
        length = math.dist(r, c)
        frame = prism.frame_along(c, r, (0.0, 1.0, 0.0))
        planes = [prism.plane((plan.hip_ridge_end(e), 0.0, 0.0), (-e, 0.0, 0.0)),
                  prism.plane((0.0, 0.0, 0.0), (0.0, 0.0, -sz))]
        for n, mesh in enumerate(lods[:2]):
            m, b = prism.member(mesh, frame, (0.0, -drop - HIP_RAFTER_M[1], -wh / 2), (length + 0.5, -drop, wh / 2),
                                TIMBER_SLOT, TIMBER, planes, offset=(0.11, 0.0))
            if n == 0:
                box, member = b, m
        out["collision"].append(box)
        _tail_snag(member, out)
    for slope in slopes[2:]:
        e = slope.end
        k_max = math.floor((plan.Z + plan.eaves - reach) / pitch)
        for k in range(-k_max, k_max + 1):
            z = k * pitch
            planes = [side_face(e, sz, True) for sz in ((-1, 1) if k == 0 else ((1,) if z > 0 else (-1,)))]
            emit(slope, z, planes)


def _battens(plan, slopes, layout, chimney, lods, out, breaks=(), rngs=None):
    """Every batten on every slope, the tilting fillet under the eaves course; a broken span's two stubs end at
    their splinters' roots, a fracture face and K03's splinters (lib.debris_timber.broken_end), the middle fallen
    (out["fallen"]), and each break keeps its stubs' reach in the slope's (a, s) for its bay's gap."""
    bw, bh = BATTEN_M
    by_row = {}
    for brk in breaks:
        by_row.setdefault((brk["slope"].name, brk["k"]), []).append(brk)
    for slope in slopes:
        rows = [(s, bh) for s in layout.battens] + [(bw / 2, layout.fillet)]
        for k, (s, height) in enumerate(rows):
            lo, hi = slope.a_range(s)
            planes, ext = [], [0.0, 0.0]
            for which, kind, hip in slope.bounds():
                if kind == "hip":
                    r, _, p = hip_geometry(plan, *hip)
                    normal = p if slope.end is None else tuple(-c for c in p)
                    planes.append(prism.plane(r, normal))
                    ext[0 if which == "lo" else 1] = 0.2
            frame = prism.Frame(slope.point(0.0, s), slope.ea, slope.en, prism.cross(slope.ea, slope.en))
            spans = [(lo - ext[0], hi + ext[1])]
            if chimney and slope.end is None:
                spans = _round_chimney(spans, chimney, slope, s, height)
            segments = [(a0, a1, None, None) for a0, a1 in spans]
            for brk in sorted(by_row.get((slope.name, k), []), key=lambda b: b["roots"][0]):
                r0, r1 = brk["roots"]
                segments = [seg for a0, a1, e0, e1 in segments
                            for seg in (((a0, r0, e0, brk), (r1, a1, brk, e1)) if a0 < r0 < r1 < a1 else
                                        ((a0, a1, e0, e1),))]
                out["fallen"].append({"kind": "batten", "slope": slope.name, "k": k, "a": (r0, r1), "s": s,
                                      "length": r1 - r0, "section": BATTEN_M, "volume_m3": (r1 - r0) * bw * bh})
            for a0, a1, e0, e1 in segments:
                box = member = None
                kinds = {key: CUT_TIMBER_SLOT for key, end in (("x-", e0), ("x+", e1)) if end}
                for n, mesh in enumerate(lods[:2]):
                    m, b = prism.member(mesh, frame, (a0, 0.0, -bw / 2), (a1, height, bw / 2), TIMBER_SLOT, TIMBER,
                                        planes, offset=(0.17 * s, 0.29 * s), slot_by_kind=kinds)
                    if n == 0:
                        box, member = b, m
                if box is None:
                    continue
                out["collision"].append(box)
                for root, end, brk in ((a0, -1, e0), (a1, 1, e1)):
                    if brk:
                        _stub_end(slope, frame, s, height, root, end, brk, lods, out, rngs)
                for which, kind, _ in slope.bounds():  # a batten's end at a gable's verge
                    if kind == "verge" and abs((a0 if which == "lo" else a1) - (lo if which == "lo" else hi)) < 1e-9:
                        end = min(member.faces, key=lambda f, sg=(-1 if which == "lo" else 1):
                                  -sg * prism.dot(f[1], slope.ea))
                        out["snag"].append({"kind": "edge", "radius_m": EDGE_SNAG_R_M,
                                            "points": [list(q) for q in end[0] + end[0][:1]]})


def _stub_end(slope, frame, s, height, root, end, brk, lods, out, rngs):
    """A broken batten's stub end: K03's splinters from its root at a = `root` towards `end`, placed on the batten
    (whose frame is K03's piece frame: x along, y up from its underside, z across), and its notch, the stub with its
    splinters' reach, in the slope's (a, s) for the bay's clear polygon."""
    bw = BATTEN_M[0]
    side = 0 if end > 0 else 1
    rows = tuple(tuple((frame.ex, frame.ey, frame.ez)[j][i] for j in range(3)) for i in range(3))
    rigid = kits_vehicle.Rigid(rows, frame.origin)
    tmp = [Mesh(), Mesh()]
    uv = debris_timber.UV(bw, height, (0.17 * s, 0.29 * s))  # the batten's own grain offset, V along a
    collision, snag, splinters = debris_timber.broken_end(tmp, uv, rngs(f"{brk['name']} {side}"), bw, height, root,
                                                         end, brk["reach"][side], SPLINTERS)
    for mesh, piece in zip(lods[:2], tmp):
        kits_vehicle.append_mesh(mesh, piece, rigid)
    out["collision"] += [kits_vehicle.transform_shape(c, rigid) for c in collision]
    out["snag"] += [kits_vehicle.transform_snag(n, rigid) for n in snag]
    sign = prism.dot(frame.ez, slope.es)
    zs = [z for sp in splinters for z in (sp["tip"][0] - sp["tip_half"][0], sp["tip"][0] + sp["tip_half"][0])]
    s_lo, s_hi = sorted((s + sign * min(-bw / 2, *zs), s + sign * max(bw / 2, *zs)))
    tip = root + end * max(sp["length"] for sp in splinters)
    # the stub from its rafter (the bay's rect clips the notch at the rafter's face) to its longest splinter's tip
    brk["notches"].append((root - 10.0, tip, s_lo, s_hi) if end > 0 else (tip, root + 10.0, s_lo, s_hi))


def _bay_gaps(plan, layout, bays, breaks, pieces, gone):
    """A roof bay is a gap only where no batten stands (design decision 9): per bay, each run of neighbouring broken
    rows opens between the two rafters' faces and the battens left below and above it (or the tiles lying on them),
    less its stubs and their splinters; its polygon in the slope's plane at the battens' mid-height, narrowest_m its
    inscribed circle. A bay whose battens all stand (0.33 m apart) is no gap."""
    bw = BATTEN_M[0]
    out = []
    by_bay = {}
    for brk in breaks:
        by_bay.setdefault(brk["bay"], []).append(brk)
    for bi, found in sorted(by_bay.items()):
        bay, slope = bays[bi], bays[bi]["slope"]
        a0, a1 = bay["a"]
        rows = sorted(found, key=lambda b: b["k"])
        runs = []
        for brk in rows:
            if runs and brk["k"] == runs[-1][-1]["k"] + 1:
                runs[-1].append(brk)
            else:
                runs.append([brk])
        for run in runs:
            k1, k2 = run[0]["k"], run[-1]["k"]
            bottom = layout.battens[k1 - 1] + bw / 2
            if not _over(pieces, gone, slope.name, k1 - 1, a0, a1):
                bottom = max(bottom, layout.heads[k1 - 1])
            top = layout.battens[k2 + 1] - bw / 2
            if k2 + 2 < len(layout.tails) and not _over(pieces, gone, slope.name, k2 + 2, a0, a1):
                top = min(top, layout.tails[k2 + 2])
            cells = rects.free((a0, a1, bottom, top), [n for brk in run for n in brk["notches"]])
            points = [list(slope.point(a, s_, bw / 2)) for a, s_ in rects.outline(cells)]
            normal = list(slope.en)
            out.append({"id": f"roof_{slope.name}_bay{bi}_row{k1}", "polygon": points, "normal": normal,
                        "narrowest_m": round(audit.inscribed_diameter(points, normal), 6)})
    return out


def _round_chimney(spans, chimney, slope, s, height):
    """A batten's spans along a side slope less the chimney's footprint and its gap, where they cross."""
    zs = [slope.point(0.0, s, n)[2] for n in (0.0, height)]
    z_lo, z_hi = min(zs) - BATTEN_M[0] / 2 / slope.plan.cos, max(zs) + BATTEN_M[0] / 2 / slope.plan.cos
    if z_hi < chimney["z0"] - CHIMNEY_GAP_M or z_lo > chimney["z1"] + CHIMNEY_GAP_M:
        return spans
    out = []
    for a0, a1 in spans:
        if a0 < chimney["x0"] - CHIMNEY_GAP_M:
            out.append((a0, min(a1, chimney["x0"] - CHIMNEY_GAP_M)))
        if a1 > chimney["x1"] + CHIMNEY_GAP_M:
            out.append((max(a0, chimney["x1"] + CHIMNEY_GAP_M), a1))
    return out


# --- tiles ----------------------------------------------------------------------------------------------------------

class _CellUV:
    """A tile's clay in one clean cell of the set (lib.debris_tile.CELLS_M), at the scale that fits the whole tile,
    V towards its head; a rim by its distance round the outline (K02's mapping)."""

    def __init__(self, layout, cell):
        (u0, v0), (u1, v1) = debris_tile.CELLS_M[cell]
        self.centre = ((u0 + u1) / 2, (v0 + v1) / 2)
        self.height = v1 - v0
        self.scale = min((u1 - u0) / layout.cover, (v1 - v0) / layout.L)

    def top(self, x, z):
        return (self.centre[0] + z * self.scale, self.centre[1] + x * self.scale)

    def rim(self, along, perimeter, y):
        k = self.height / perimeter
        return (self.centre[0] + y * k, self.centre[1] + (along - perimeter / 2) * k)


def _course_frame(slope, layout, k, a=0.0):
    """Course k's tile frame at a: x up the tile from tail to head (tilted by the rise), y out of it, z across; the
    origin on the tile's underside line at the course's middle."""
    L, rise = layout.L, layout.rise
    s_mid = (layout.tails[k] + layout.heads[k]) / 2
    n_mid = layout.n_head + rise / 2
    x_t = prism.unit(tuple(slope.es[i] * L - slope.en[i] * rise for i in range(3)))
    y_t = prism.unit(tuple(slope.es[i] * rise + slope.en[i] * L for i in range(3)))
    return prism.Frame(slope.point(a, s_mid, n_mid), x_t, y_t, prism.cross(x_t, y_t)), s_mid


def _clip(poly, n, c):
    """A convex polygon in (a, s) kept where n . (a, s) <= c."""
    out = []
    for p, q in zip(poly, poly[1:] + poly[:1]):
        dp, dq = n[0] * p[0] + n[1] * p[1] - c, n[0] * q[0] + n[1] * q[1] - c
        if dp <= 1e-12:
            out.append(p)
        if (dp < -1e-12 < 1e-12 < dq) or (dq < -1e-12 < 1e-12 < dp):
            t = dp / (dp - dq)
            out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return out


def _area2(poly):
    return abs(sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(poly, poly[1:] + poly[:1]))) / 2


def _bound_planes(slope, layout):
    """The slope's a bounds as half-planes in (a, s): [(which, kind, n, c)], tiles HIP_MARGIN_M inside a hip."""
    p = slope.plan
    out = []
    for which, kind, _ in slope.bounds():
        sign = 1 if which == "hi" else -1
        if kind == "verge":
            limit = (p.X + p.verge) if slope.end is None else None
            out.append((which, kind, (sign, 0.0), limit))
        else:
            run = (p.Z + p.eaves) if slope.end is not None else (p.X + p.eaves)
            out.append((which, kind, (sign, p.cos), run - HIP_MARGIN_M))
    return out


def _chimney_rect(slope, layout, chimney):
    """The chimney's footprint and gap in a side slope's (a, s) at the tiles' mid-height, or None."""
    if not chimney or slope.end is not None:
        return None
    p = slope.plan
    n_mid = layout.n_head + layout.rise / 2 + layout.t / 2
    ze = p.Z + p.eaves
    s_of = lambda z: (ze - slope.side * z + n_mid * p.sin) / p.cos
    s_vals = [s_of(chimney["z0"] - CHIMNEY_GAP_M), s_of(chimney["z1"] + CHIMNEY_GAP_M)]
    rect = (chimney["x0"] - CHIMNEY_GAP_M, min(s_vals), chimney["x1"] + CHIMNEY_GAP_M, max(s_vals))
    lo, hi = layout.tails[0], layout.heads[-1]
    return rect if rect[3] > lo and rect[1] < hi else None


def _tile_pieces(plan, slopes, layout, chimney, rngs):
    """Every tile's place on the roof (pieces where cut): its polygon in its slope's (a, s), its full width, its cuts,
    its clay cell, its slope and course."""
    cell_rng = rngs("tile cells")
    width = layout.cover - TILE_JOINT_M
    pieces_all = []
    for slope in slopes:
        bounds = _bound_planes(slope, layout)
        crect = _chimney_rect(slope, layout, chimney)
        verges = [b for b in bounds if b[1] == "verge"]
        lo_a, hi_a = slope.a_range(0.0)
        if any(b[0] == "lo" for b in verges):
            start, step = lo_a, 1
        elif verges:
            start, step = hi_a, -1
        else:
            start, step = -layout.cover / 2, 1
        for k in range(len(layout.heads)):
            s0, s1 = layout.tails[k], layout.heads[k]
            course = []
            a_lo = min(slope.a_range(s0)[0], slope.a_range(s1)[0])
            a_hi = max(slope.a_range(s0)[1], slope.a_range(s1)[1])
            if step > 0 and verges and verges[0][0] == "lo":
                i0, i1 = 0, math.ceil((a_hi - start) / layout.cover)
            elif step < 0:
                i0, i1 = 0, math.ceil((start - a_lo) / layout.cover)
            else:
                i0, i1 = math.floor((a_lo - start) / layout.cover) - 1, math.ceil((a_hi - start) / layout.cover) + 1
            for i in range(i0, i1):
                ta = start + step * i * layout.cover
                ta0, ta1 = (ta, ta + width) if step > 0 else (ta - width, ta)
                rects = [(ta0, s0, ta1, s1)]
                chimney_cut = False
                if crect and ta1 > crect[0] and ta0 < crect[2] and s1 > crect[1] and s0 < crect[3]:
                    chimney_cut = True
                    rects = [r for r in ((ta0, s0, min(ta1, crect[0]), s1), (max(ta0, crect[2]), s0, ta1, s1),
                                         (max(ta0, crect[0]), s0, min(ta1, crect[2]), min(s1, crect[1])),
                                         (max(ta0, crect[0]), max(s0, crect[3]), min(ta1, crect[2]), s1))
                             if r[2] - r[0] > 1e-9 and r[3] - r[1] > 1e-9]
                for r in rects:
                    poly = [(r[0], r[1]), (r[2], r[1]), (r[2], r[3]), (r[0], r[3])]
                    hip_cut = None
                    for which, kind, n, c in bounds:
                        if c is None:
                            continue
                        before = _area2(poly)
                        poly = _clip(poly, n, c)
                        if len(poly) < 3:
                            break
                        if _area2(poly) < before - 1e-12:
                            hip_cut = which if kind == "hip" else hip_cut
                    if len(poly) < 3 or _area2(poly) < MIN_PIECE_M2:
                        continue
                    xs, ys = [q[0] for q in poly], [q[1] for q in poly]
                    if min(max(xs) - min(xs), max(ys) - min(ys)) < MIN_PIECE_M:
                        continue
                    cut = chimney_cut or hip_cut is not None or _area2(poly) < width * layout.Ls - 1e-9
                    course.append({"poly": poly, "full": (ta0, ta1), "hip": hip_cut, "chimney": chimney_cut,
                                   "cut": cut, "cell": cell_rng.randrange(len(debris_tile.CELLS_M))})
            for piece in course:
                piece.update(slope=slope, k=k)
            pieces_all += course
    return pieces_all


def _strip(plan, params, pieces, rngs):
    """The tiles `roof_strip` takes away (indices into `pieces`): up to SLIP of them slipped, seeded and scattered (the
    I state's 2-5 %), and the rest stripped where the tiles left cluster: each tile scored by how near the eaves and
    the lee end it lies (lib.rubble.travel) and by a smooth seeded noise of CLUSTER_CELL_M cells, weighted by
    `tile_cluster_bias`, the lowest scores gone first."""
    n = len(pieces)
    n_gone = round(params["roof_strip"] * n)
    n_slip = min(n_gone, round(SLIP * n))
    gone = set(rngs("roof strip").sample(range(n), n_slip))
    if n_gone == n_slip:
        return gone
    tx, tz = rubble.travel(params)
    centres = []
    for pc in pieces:
        a = sum(q[0] for q in pc["poly"]) / len(pc["poly"])
        s_ = sum(q[1] for q in pc["poly"]) / len(pc["poly"])
        x, _, z = pc["slope"].point(a, s_)
        centres.append((x, z, 1 - s_ / pc["slope"].S))
    reach = max(abs(x * tx + z * tz) for x, z, _ in centres) or 1.0
    span = 2 * (plan.X + plan.eaves) + 2 * CLUSTER_CELL_M
    noise = masks.PeriodicNoise(rngs("roof clusters"), 2 * span, CLUSTER_CELL_M, span)
    bias = params["tile_cluster_bias"]

    def score(i):
        x, z, eaves = centres[i]
        lee = 0.5 + 0.5 * (x * tx + z * tz) / reach
        patch = 0.5 + 0.5 * noise(x + span / 2, z + span / 2)
        return bias * (eaves + lee) / 2 + (1 - bias) * patch

    order = sorted((score(i), i) for i in range(n) if i not in gone)
    return gone | {i for _, i in order[:n_gone - n_slip]}


def _tiles(plan, slopes, layout, chimney, lods, out, pieces_all, gone):
    """Every tile left (pieces where cut), the courses' collision and LOD1 strips; returns the tile counts and keeps
    the course regions (for the far LODs) in out["regions"]."""
    counts = {"whole": 0, "cut": 0, "removed": len(gone), "boxes": 0}
    out["regions"] = []
    kept = [pc for i, pc in enumerate(pieces_all) if i not in gone]
    by_course = {}
    for pc in kept:
        by_course.setdefault((pc["slope"].name, pc["k"]), []).append(pc)
    for (name, k), course in by_course.items():
        slope = course[0]["slope"]
        frame, s_mid = _course_frame(slope, layout, k)
        sign = prism.dot(frame.ez, slope.ea)
        for pc in course:
            _tile_piece(lods[0], frame, sign, s_mid, layout, pc, nib=True, top=True, body=True)
            _tile_piece(lods[1], frame, sign, s_mid, layout, pc, nib=False, top=True, body=False)
            counts["cut" if pc["cut"] else "whole"] += 1
        for region in _regions(slope, layout, course, chimney):
            boxes = _region_boxes(frame, sign, s_mid, layout, region)
            out["regions"].append((slope, region["poly"]))
            out["collision"] += boxes
            counts["boxes"] += len(boxes)
            _region_body(lods[1], frame, sign, s_mid, layout, region["poly"])
    return counts


def _to_local(poly, sign, s_mid, layout):
    c = math.cos(layout.alpha)
    return [((s - s_mid) / c, sign * a) for a, s in poly]


def _tile_piece(mesh, frame, sign, s_mid, layout, pc, nib, top, body):
    """One tile (or cut piece) of a course: its top halves in its cell (always), its underside, rims and nib (body)."""
    a_mid = (pc["full"][0] + pc["full"][1]) / 2
    local = _to_local(pc["poly"], sign, s_mid, layout)
    zc = sign * a_mid
    full = (layout.L / 2, (layout.cover - TILE_JOINT_M) / 2)
    uv = _CellUV(layout, pc["cell"])
    shifted = [(x, z - zc) for x, z in local]
    tile_frame = prism.Frame(frame.point((0.0, 0.0, zc)), frame.ex, frame.ey, frame.ez)
    cuts = []
    for p, q in zip(shifted, shifted[1:] + shifted[:1]):
        on_edge = ((abs(abs(p[0]) - full[0]) < 1e-6 and abs(p[0] - q[0]) < 1e-6)
                   or (abs(abs(p[1]) - full[1]) < 1e-6 and abs(p[1] - q[1]) < 1e-6))
        cuts.append(not on_edge)
    _slab(mesh, tile_frame, shifted, layout, uv, cuts, top=top, body=body,
          nib=nib and _contains(shifted, layout))


def _contains(poly, layout):
    """Whether the nib's footprint lies inside a piece."""
    a, c, _ = debris_tile.NIB_M
    corners = [(layout.L / 2 - a, -c / 2), (layout.L / 2, -c / 2), (layout.L / 2, c / 2), (layout.L / 2 - a, c / 2)]
    return all(_inside(p, poly) for p in corners)


def _inside(p, poly):
    sgn = None
    for a, b in zip(poly, poly[1:] + poly[:1]):
        cr = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
        if abs(cr) < 1e-12:
            continue
        if sgn is None:
            sgn = cr > 0
        elif (cr > 0) != sgn:
            return False
    return True


def _camber(layout, x):
    """A tile's underside above its tail-to-head line at x: two planes, the middle `camber` up (K02's LOD1)."""
    return layout.camber * max(0.0, 1 - abs(2 * x / layout.L))


def _slab(mesh, frame, poly, layout, uv, cuts, top=True, body=True, nib=False):
    """A tile slab from a convex polygon in its frame's (x, z): split at x = 0 into the camber's two planes, the top
    (when `top`) and the underside and rims (when `body`), a rim in CUT_SLOT where `cuts` marks its edge; the nib."""
    t = layout.t
    pts, flags = [], []
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        pts.append(p)
        flags.append(cuts[i])
        if (p[0] < -1e-12 < 1e-12 < q[0]) or (q[0] < -1e-12 < 1e-12 < p[0]):
            f = p[0] / (p[0] - q[0])
            pts.append((0.0, p[1] + f * (q[1] - p[1])))
            flags.append(cuts[i])
    for sign in (-1, 1):
        half = [p for p in pts if sign * p[0] >= -1e-12]
        if len(half) < 3 or _area2(half) < 1e-9:
            continue
        for is_top in ((True, False) if body else (True,)) if top else ((False,) if body else ()):
            lift = t if is_top else 0.0
            verts = [frame.point((p[0], _camber(layout, p[0]) + lift, p[1])) for p in half]
            normal = _newell(verts)
            if prism.dot(normal, frame.ey) * (1 if is_top else -1) < 0:
                normal = tuple(-c for c in normal)
            mesh.face([mesh.vertex(v) for v in verts], normal, TILE_SLOT, [uv.top(p[0], p[1]) for p in half])
    if not body:
        return
    perimeter = sum(math.dist(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts)))
    centre = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
    along = 0.0
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        seg = math.dist(p, q)
        if seg < 1e-9:
            continue
        quad = [(p[0], _camber(layout, p[0]), p[1]), (q[0], _camber(layout, q[0]), q[1]),
                (q[0], _camber(layout, q[0]) + t, q[1]), (p[0], _camber(layout, p[0]) + t, p[1])]
        verts = [frame.point(v) for v in quad]
        normal = _newell(verts)
        mid = frame.point(((p[0] + q[0]) / 2, 0.0, (p[1] + q[1]) / 2))
        inner = frame.point((centre[0], 0.0, centre[1]))
        if prism.dot(normal, tuple(mid[k] - inner[k] for k in range(3))) < 0:
            normal = tuple(-c for c in normal)
        end = along + seg
        uvs = [uv.rim(along, perimeter, 0.0), uv.rim(end, perimeter, 0.0), uv.rim(end, perimeter, t),
               uv.rim(along, perimeter, t)]
        mesh.face([mesh.vertex(v) for v in verts], normal, CUT_SLOT if flags[i] else TILE_SLOT, uvs)
        along = end
    if nib:
        a, c, dn = debris_tile.NIB_M
        x0, x1 = layout.L / 2 - a, layout.L / 2
        box = {(sx, sy, sz): (x0 if sx < 0 else x1, -dn if sy < 0 else _camber(layout, x0 if sx < 0 else x1),
                              sz * c / 2) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)}
        ids = {k: mesh.vertex(frame.point(p)) for k, p in box.items()}
        for axis, sgn in ((0, -1), (0, 1), (1, -1), (2, -1), (2, 1)):
            keys = [k for k in ids if k[axis] == sgn]
            o = [(axis + 1) % 3, (axis + 2) % 3]
            keys.sort(key=lambda k: math.atan2(k[o[1]], k[o[0]]))
            local_n = tuple(float(sgn) if i == axis else 0.0 for i in range(3))
            mesh.face([ids[k] for k in keys], frame.vector(local_n), TILE_SLOT,
                      [uv.rim(perimeter / 2 + box[k][2], perimeter, box[k][1] + dn) for k in keys])


def _regions(slope, layout, course, chimney):
    """A course's collision regions in (a, s): runs of whole tiles, the hip-cut tiles at each hip end as one
    polygon (the course between the first cut tile and the hip line), and each chimney-cut piece."""
    s0, s1 = layout.tails[course[0]["k"]], layout.heads[course[0]["k"]]
    regions = []
    bounds = {b[0]: b for b in _bound_planes(slope, layout)}
    cx = (chimney["x0"], chimney["x1"]) if chimney and slope.end is None and any(pc["chimney"] for pc in course) \
        else None
    hip_groups = {"lo": [], "hi": []}
    run = []
    for pc in sorted(course, key=lambda pc: min(q[0] for q in pc["poly"])):
        lo_a = min(q[0] for q in pc["poly"])
        hi_a = max(q[0] for q in pc["poly"])
        if pc["hip"] and not (cx and ((pc["hip"] == "hi" and lo_a < cx[1]) or (pc["hip"] == "lo" and hi_a > cx[0]))):
            hip_groups[pc["hip"]].append(pc)
            continue
        if pc["cut"]:
            regions.append({"poly": pc["poly"], "kind": "piece"})
            continue
        if run and lo_a - run[-1][1] > TILE_JOINT_M + 1e-6:
            regions.append(_run_region(run, s0, s1))
            run = []
        run.append((lo_a, hi_a))
    if run:
        regions.append(_run_region(run, s0, s1))
    for which, group in hip_groups.items():
        _, _, n, c = bounds[which]
        spans = []  # contiguous runs of the hip-cut pieces (a tile taken away splits them)
        for pc in sorted(group, key=lambda pc: min(q[0] for q in pc["poly"])):
            a0, a1 = min(q[0] for q in pc["poly"]), max(q[0] for q in pc["poly"])
            if spans and a0 - spans[-1][1] <= TILE_JOINT_M + 1e-6:
                spans[-1][1] = max(spans[-1][1], a1)
            else:
                spans.append([a0, a1])
        for a0, a1 in spans:
            poly = _clip([(a0, s0), (a1, s0), (a1, s1), (a0, s1)], n, c)
            if len(poly) >= 3:
                regions.append({"poly": poly, "kind": "hip", "which": which, "n": n, "c": c, "span": (a0, a1)})
    return regions


def _run_region(run, s0, s1):
    a0, a1 = run[0][0], run[-1][1]
    return {"poly": [(a0, s0), (a1, s0), (a1, s1), (a0, s1)], "kind": "run"}


def _region_boxes(frame, sign, s_mid, layout, region):
    """A region's boxes in the course's tilted frame, from the tiles' underside to the camber's top: one for a run
    or a piece; strips STRIP_M of the hip's run wide for a hip region."""
    polys = [region["poly"]]
    if region["kind"] == "hip":
        ss = [q[1] for q in region["poly"]]
        s0, s1 = min(ss), max(ss)
        n, c = region["n"], region["c"]
        run = abs(n[1]) * (s1 - s0)  # how far the hip line moves along a over the course
        count = max(1, math.ceil(run / STRIP_M))
        polys = []
        a_lo, a_hi = region["span"]
        for j in range(count):
            a, b = s0 + (s1 - s0) * j / count, s0 + (s1 - s0) * (j + 1) / count
            sm = (a + b) / 2
            edge = (c - n[1] * sm) / n[0]  # the hip line's a at the strip's middle
            lo, hi = (a_lo, min(a_hi, edge)) if region["which"] == "hi" else (max(a_lo, edge), a_hi)
            if hi - lo > 1e-6:
                polys.append([(lo, a), (hi, a), (hi, b), (lo, b)])
    boxes = []
    top = layout.t + layout.camber
    for poly in polys:
        local = _to_local(poly, sign, s_mid, layout)
        xs, zs = [p[0] for p in local], [p[1] for p in local]
        member = prism.Member(frame, (min(xs), 0.0, min(zs)), (max(xs), top, max(zs)))
        boxes.append(member.box(MASONRY))
    return boxes


def _region_body(mesh, frame, sign, s_mid, layout, poly):
    """LOD1: a region's underside and rims as one slab (its tiles' tops are drawn per tile)."""
    local = _to_local(poly, sign, s_mid, layout)
    if _signed(local) < 0:
        local = local[::-1]
    uv = _CellUV(layout, 0)
    _slab(mesh, frame, local, layout, uv, [False] * len(local), top=False, body=True)


def _signed(poly):
    return sum(p[0] * q[1] - q[0] * p[1] for p, q in zip(poly, poly[1:] + poly[:1])) / 2


# --- ridge and hip tiles --------------------------------------------------------------------------------------------

def _ridge_tiles(plan, slopes, layout, lods, out, rngs):
    """Half-round tiles along the ridge and every hip, each on the two covers it closes, one box per run."""
    rng = rngs("ridge tiles")
    n_c = layout.n_head + layout.t + layout.camber / 2  # the covers' top under the arch (assumed contact)
    front, back = slopes[0], slopes[1]
    runs = []
    x0, x1 = plan.ridge_x()
    if plan.ends[-1] == "gable":
        x0 = -(plan.X + plan.verge)
    if plan.ends[1] == "gable":
        x1 = plan.X + plan.verge
    ridge_h = plan.apex + n_c / plan.cos - ARCH_R_M * plan.tan
    # each run: its arch centre at both ends, w across (towards the first slope), u up, and `lift`, how far the covers'
    # meeting line stands above the arch's chord (its box starts there: below it lie the covers' own boxes)
    runs.append(((x0, ridge_h, 0.0), (x1, ridge_h, 0.0), (0.0, 0.0, -1.0), (0.0, 1.0, 0.0), ARCH_R_M * plan.tan))
    for e in (-1, 1):
        if plan.ends[e] != "hip":
            continue
        for sz in (-1, 1):
            r, c, p = hip_geometry(plan, e, sz)
            side = front if sz < 0 else back
            axis = prism.unit(tuple(c[k] - r[k] for k in range(3)))
            w = tuple(-x for x in p)  # towards the side slope
            u = prism.cross(w, axis)
            if u[1] < 0:
                u = tuple(-x for x in u)

            def along_u(q, side=side, u=u):
                """How far along u from q the side slope's cover plane lies."""
                return (n_c - prism.dot(tuple(q[k] - side.origin[k] for k in range(3)), side.en)) / prism.dot(u, side.en)

            ends = []
            for h in (r, c):
                lam = along_u(tuple(h[k] + ARCH_R_M * w[k] for k in range(3)))
                ends.append(tuple(h[k] + lam * u[k] for k in range(3)))
            lift = along_u(r) - along_u(tuple(r[k] + ARCH_R_M * w[k] for k in range(3)))
            runs.append((ends[0], ends[1], w, u, lift))
    for p0, p1, w, u, lift in runs:
        length = math.dist(p0, p1)
        axis = prism.unit(tuple(p1[k] - p0[k] for k in range(3)))
        count = max(1, math.ceil(length / ARCH_PITCH_M - 1e-9))
        pitch = length / count
        for j in range(count):
            a0 = j * pitch
            cell = rng.randrange(len(debris_tile.CELLS_M))
            for lod, segments in zip(lods[:2], ARCH_SEGMENTS):
                _arch(lod, p0, axis, w, u, a0, a0 + pitch - 0.003, segments, cell)
        # one box over the run: across 2R, from the covers' meeting line up to the arch's crown
        frame = prism.Frame(p0, axis, u, prism.cross(axis, u))
        out["collision"].append(prism.Member(frame, (0.0, lift, -ARCH_R_M), (length, ARCH_R_M, ARCH_R_M)).box(MASONRY))


def _arch(mesh, p0, axis, w, u, a0, a1, segments, cell):
    """One half-round tile from a0 to a1 along the run: outer and inner skins, the two base strips, both ends."""
    r_out, r_in = ARCH_R_M, ARCH_R_M - ARCH_T_M
    (cu0, cv0), (cu1, cv1) = debris_tile.CELLS_M[cell]
    scale = min((cu1 - cu0) / (math.pi * r_out), (cv1 - cv0) / (a1 - a0))

    def pt(a, r, phi):
        return tuple(p0[k] + a * axis[k] + r * (math.cos(phi) * w[k] + math.sin(phi) * u[k]) for k in range(3))

    def uv(a, r, phi):
        return (cu0 + phi * r_out * scale, cv0 + (a - a0) * scale)

    phis = [math.pi * i / segments for i in range(segments + 1)]
    for r, sign in ((r_out, 1), (r_in, -1)):
        for i in range(segments):
            quad = [(a0, phis[i]), (a1, phis[i]), (a1, phis[i + 1]), (a0, phis[i + 1])]
            verts = [pt(a, r, f) for a, f in quad]
            mid = (phis[i] + phis[i + 1]) / 2
            normal = tuple(sign * (math.cos(mid) * w[k] + math.sin(mid) * u[k]) for k in range(3))
            mesh.face([mesh.vertex(v) for v in verts], normal, TILE_SLOT, [uv(a, r, f) for a, f in quad])
    for f in (0.0, math.pi):
        quad = [pt(a0, r_in, f), pt(a1, r_in, f), pt(a1, r_out, f), pt(a0, r_out, f)]
        normal = tuple(-u[k] for k in range(3))
        mesh.face([mesh.vertex(v) for v in quad], normal, TILE_SLOT,
                  [uv(a0, r_in, f), uv(a1, r_in, f), uv(a1, r_out, f), uv(a0, r_out, f)])
    for a, sign in ((a0, -1), (a1, 1)):
        for i in range(segments):
            quad = [pt(a, r_in, phis[i]), pt(a, r_out, phis[i]), pt(a, r_out, phis[i + 1]), pt(a, r_in, phis[i + 1])]
            mesh.face([mesh.vertex(v) for v in quad], tuple(sign * c for c in axis), TILE_SLOT,
                      [uv(a, r_in, phis[i]), uv(a, r_out, phis[i]), uv(a, r_out, phis[i + 1]),
                       uv(a, r_in, phis[i + 1])])


# --- the chimney ----------------------------------------------------------------------------------------------------

def _chimney(plan, slopes, layout, ch, lods, out, rngs):
    """The stack of K01 bricks from the floor to its cap, a flue of one brick by one in it, the bricks above the
    roof with K01's worn arrises, its mortar core, its fillet on the tiles, its boxes, the flue's gap, the cap's
    snags."""
    rng = rngs("chimney")
    bl, bh, bw = BRICK_M
    x0, x1, z0, z1 = ch["x0"], ch["x1"], ch["z0"], ch["z1"]
    pitch = bh + JOINT_M
    roof_y = max(plan.rt(x, z) for x in (x0, x1) for z in (z0, z1)) + 0.15
    cap_from = ch["courses"] - CAP_COURSES
    inset = MORTAR_INSET_M
    xf = prism.Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
    yf = prism.Frame((0.0, 0.0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0))
    for k in range(ch["courses"]):
        y = plan.F + k * pitch
        out_by = CAP_OUT_M if k >= cap_from else 0.0
        for (cx, cz, along_x) in _course_bricks(x0, z0, k):
            centre = [cx, cz]
            if along_x:
                centre[1] += (-out_by if cz < (z0 + z1) / 2 else out_by)
            else:
                centre[0] += (-out_by if cx < (x0 + x1) / 2 else out_by)
            rot = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)) if along_x else \
                ((0.0, 0.0, -1.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
            rigid = kits_vehicle.Rigid(rot, (centre[0], y, centre[1]))
            exposed = y > roof_y
            chamfer = {c: rng.uniform(*BEVEL_MM) / 1000 for c in debris_brick.CORNERS} if exposed else None
            kits_vehicle.append_mesh(lods[0], debris_brick.brick_mesh(BRICK_M, chamfer), rigid,
                                     {debris_brick.SLOT: BRICK_SLOT})
            hx, hz = (bl / 2, bw / 2) if along_x else (bw / 2, bl / 2)
            out["collision"].append(prism.axis_box((centre[0] - hx, y, centre[1] - hz),
                                                   (centre[0] + hx, y + bh, centre[1] + hz), MASONRY))
        # the course's mortar, its bed under it and its joints, struck back from both faces and split as its bricks
        # are, so no face of it lies deep in a brick
        y0 = y - JOINT_M if k else y
        for n, (lo, hi) in enumerate(_cap_walls(x0, z0, k, out_by)):
            # across a wall: struck back from both faces; along it: a full wall's struck back from its ends, a wall
            # between the full ones reaching through its end joints to their bricks
            along_x = hi[0] - lo[0] > hi[1] - lo[1]
            reach = -inset if n < 2 else JOINT_M
            a0, a1 = (lo[0] - reach, hi[0] + reach) if along_x else (lo[0] + inset, hi[0] - inset)
            b0, b1 = (lo[1] + inset, hi[1] - inset) if along_x else (lo[1] - reach, hi[1] + reach)
            prism.member(lods[0], xf, (a0, y0, b0), (a1, y + bh, b1), CLAY_SLOT, MASONRY)
            if k >= cap_from:  # LOD1: the cap as its courses' walls
                prism.member(lods[1], yf, (y, lo[0], -hi[1]), (y + bh, hi[0], -lo[1]), BRICK_SLOT, MASONRY)
    top = ch["top"]
    cap_y = plan.F + cap_from * pitch
    # LOD1: the stack below the cap as the flue's four walls, one brick's width thick
    walls = [((x0, z0), (x1, z0 + bw)), ((x0, z1 - bw), (x1, z1)), ((x0, z0 + bw), (x0 + bw, z1 - bw)),
             ((x1 - bw, z0 + bw), (x1, z1 - bw))]
    for (a0, b0), (a1, b1) in walls:
        prism.member(lods[1], yf, (plan.F, a0, -b1), (cap_y - JOINT_M, a1, -b0), BRICK_SLOT, MASONRY)
    for mesh in lods[2:]:  # far: one box
        frame = prism.Frame((0.0, 0.0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, -1.0))
        prism.member(mesh, frame, (roof_y - 0.3, x0, -z1), (top, x1, -z0), BRICK_SLOT, MASONRY)
    # the flue's mouth under the cap: its clear space
    f0, f1 = (x0 + bw, z0 + bw), (x1 - bw, z1 - bw)
    y_mouth = cap_y - JOINT_M
    poly = [[f0[0], y_mouth, f0[1]], [f1[0], y_mouth, f0[1]], [f1[0], y_mouth, f1[1]], [f0[0], y_mouth, f1[1]]]
    out["gaps"].append({"id": "flue", "polygon": poly, "normal": [0.0, 1.0, 0.0],
                        "narrowest_m": round(audit.inscribed_diameter(poly, [0.0, 1.0, 0.0]), 6)})
    o = CAP_OUT_M
    rim = [[x0 - o, top, z0 - o], [x1 + o, top, z0 - o], [x1 + o, top, z1 + o], [x0 - o, top, z1 + o]]
    out["snag"].append({"kind": "edge", "radius_m": EDGE_SNAG_R_M, "points": rim + rim[:1]})
    flue = [[f0[0] - o, top, f0[1] - o], [f1[0] + o, top, f0[1] - o], [f1[0] + o, top, f1[1] + o],
            [f0[0] - o, top, f1[1] + o]]
    out["snag"].append({"kind": "edge", "radius_m": EDGE_SNAG_R_M, "points": flue + flue[:1]})
    _fillets(plan, slopes, layout, ch, lods, out)
    out["lines"].append(f"chimney: {ch['courses']} courses of {bl * 1000:g} x {bw * 1000:g} x {bh * 1000:g} mm bricks "
                        f"with {JOINT_M * 1000:g} mm joints, {x1 - x0:.3f} x {z1 - z0:.3f} m, top {top:.3f} m "
                        f"({top - plan.apex:.3f} m above the ridge), flue {f1[0] - f0[0]:.3f} x {f1[1] - f0[1]:.3f} m")


def _course_bricks(x0, z0, k):
    """Course k's six bricks (centre x, centre z, along x?) round a one-brick flue: two stretchers on the front and
    back with headers between on even courses, turned a quarter on odd ones (bonded at the corners)."""
    bl, _, bw = BRICK_M
    j = JOINT_M
    full = 2 * bl + j
    if k % 2 == 0:
        out = [(x0 + bl / 2, z0 + bw / 2, True), (x0 + bl + j + bl / 2, z0 + bw / 2, True),
               (x0 + bl / 2, z0 + full - bw / 2, True), (x0 + bl + j + bl / 2, z0 + full - bw / 2, True),
               (x0 + bw / 2, z0 + full / 2, False), (x0 + full - bw / 2, z0 + full / 2, False)]
    else:
        out = [(x0 + bw / 2, z0 + bl / 2, False), (x0 + bw / 2, z0 + bl + j + bl / 2, False),
               (x0 + full - bw / 2, z0 + bl / 2, False), (x0 + full - bw / 2, z0 + bl + j + bl / 2, False),
               (x0 + full / 2, z0 + bw / 2, True), (x0 + full / 2, z0 + full - bw / 2, True)]
    return out


def _cap_walls(x0, z0, k, o):
    """A cap course's four walls (lo, hi) in plan (x, z), each stepped out by o."""
    bl, _, bw = BRICK_M
    j, full = JOINT_M, 2 * BRICK_M[0] + JOINT_M
    x1, z1 = x0 + full, z0 + full
    if k % 2 == 0:  # stretchers front and back over the whole width, headers between
        return [((x0, z0 - o), (x1, z0 + bw - o)), ((x0, z1 - bw + o), (x1, z1 + o)),
                ((x0 - o, z0 + bw + j), (x0 + bw - o, z1 - bw - j)), ((x1 - bw + o, z0 + bw + j), (x1 + o, z1 - bw - j))]
    return [((x0 - o, z0), (x0 + bw - o, z1)), ((x1 - bw + o, z0), (x1 + o, z1)),
            ((x0 + bw + j, z0 - o), (x1 - bw - j, z0 + bw - o)), ((x0 + bw + j, z1 - bw + o), (x1 - bw - j, z1 + o))]


def _fillets(plan, slopes, layout, ch, lods, out):
    """The mortar fillet where the chimney stands through the tiles: along its two faces square to the slope, level
    across the slope on the other two, on the tiles' top."""
    slope = slopes[0] if ch["z1"] <= 0 else slopes[1]
    n_top = layout.n_tail + layout.t
    fw, fh = FILLET_M
    x0, x1, z0, z1 = ch["x0"], ch["x1"], ch["z0"], ch["z1"]
    ze = plan.Z + plan.eaves

    def s_at(z):
        return (ze - slope.side * z) / plan.cos

    s_lo, s_hi = sorted((s_at(z0), s_at(z1)))
    frame = prism.Frame(slope.origin, slope.ea, slope.en, prism.cross(slope.ea, slope.en))
    sign = prism.dot(frame.ez, slope.es)  # the frame's z runs along the slope one way or the other
    za, zb = sorted((sign * (s_lo - 0.05), sign * (s_hi + 0.05)))
    pieces = [((x0 - fw, n_top, za), (x0, n_top + fh, zb)), ((x1, n_top, za), (x1 + fw, n_top + fh, zb))]
    for lo, hi in pieces:
        for mesh in lods[:2]:
            _, box = prism.member(mesh, frame, lo, hi, CLAY_SLOT, MASONRY)
        out["collision"].append(box)
    for z, sgn in ((z0, -1), (z1, 1)):
        s = s_at(z)
        y = plan.rt((x0 + x1) / 2, z) + n_top / plan.cos
        lev = prism.Frame((0.0, y, z), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
        lo, hi = (x0 - fw, -0.03, min(0.0, sgn * fw)), (x1 + fw, fh, max(0.0, sgn * fw))
        for mesh in lods[:2]:
            _, box = prism.member(mesh, lev, lo, hi, CLAY_SLOT, MASONRY)
        out["collision"].append(box)


# --- the ceiling ----------------------------------------------------------------------------------------------------

def _ceiling(plan, ch, lods, out):
    """Each room's beam across it (a room wider than BEAM_SPAN), the joists along the house at JOIST_PITCH_M, the
    boards across over the whole interior and the clay on them, all cut round the chimney."""
    x_in0, x_in1, z_in0, z_in1 = plan.inner
    H = plan.H
    jw, jh = JOIST_M
    clear = 0.03
    hole = (ch["x0"] - clear, ch["x1"] + clear, ch["z0"] - clear, ch["z1"] + clear) if ch else None
    xf = prism.Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
    zf = prism.Frame((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (-1.0, 0.0, 0.0))
    for space in plan.spaces:
        n = int((space.z1 - space.z0) / JOIST_PITCH_M)
        zs = [((k + 0.5) - n / 2) * JOIST_PITCH_M + (space.z0 + space.z1) / 2 for k in range(n)]
        for z in zs:
            spans = [(space.x0, space.x1)]
            if hole and hole[2] < z + jw / 2 and hole[3] > z - jw / 2:
                spans = [(a, b) for a, b in ((space.x0, min(space.x1, hole[0])), (max(space.x0, hole[1]), space.x1))
                         if b - a >= MIN_JOIST_M]
            for a, b in spans:
                for mesh in lods[:2]:
                    _, box = prism.member(mesh, xf, (a, H - jh, z - jw / 2), (b, H, z + jw / 2), TIMBER_SLOT,
                                          TIMBER, offset=(0.3 * z, 0.1 * a))
                out["collision"].append(box)
        if space.x1 - space.x0 > 2.5:
            xc = (space.x0 + space.x1) / 2
            bw, bh = BEAM_M
            for mesh in lods[:2]:
                _, box = prism.member(mesh, zf, (space.z0, H - jh - bh, -xc - bw / 2), (space.z1, H - jh, -xc + bw / 2),
                                      TIMBER_SLOT, TIMBER, offset=(0.4, 0.2))
            out["collision"].append(box)
    bw, bt, gap = BOARD_M
    count = int((x_in1 - x_in0 + gap) / (bw + gap))
    pitch = (x_in1 - x_in0 + gap) / count
    for k in range(count):
        a, b = x_in0 + k * pitch, x_in0 + k * pitch + pitch - gap
        spans = [(z_in0, z_in1)]
        if hole and hole[0] < b and hole[1] > a:
            spans = [(c, d) for c, d in ((z_in0, hole[2]), (hole[3], z_in1)) if d - c > 0.05]
        for c, d in spans:
            for mesh in lods[:2]:
                prism.member(mesh, zf, (c, H, -b), (d, H + bt, -a), TIMBER_SLOT, TIMBER, offset=(0.21 * k, 0.37 * k))
    # the clay on the boards, and the ceiling's boxes, both round the chimney
    y0, y1 = H, H + bt + CLAY_M
    rects = [(x_in0, x_in1, z_in0, z_in1)]
    if hole:
        rects = [r for r in ((x_in0, hole[0], z_in0, z_in1), (hole[1], x_in1, z_in0, z_in1),
                             (hole[0], hole[1], z_in0, hole[2]), (hole[0], hole[1], hole[3], z_in1))
                 if r[1] - r[0] > 1e-6 and r[3] - r[2] > 1e-6]
    for xa, xb, za, zb in rects:
        for mesh in lods[:2]:
            prism.member(mesh, xf, (xa, H + bt, za), (xb, y1, zb), CLAY_SLOT, MASONRY, offset=(0.0, 0.0))
        out["collision"].append(prism.axis_box((xa, y0, za), (xb, y1, zb), MASONRY))
        wind = prism.axis_box((xa, y0, za), (xb, y1, zb), MASONRY)
        wind.pop("material")
        wind.pop("landable")
        wind["porosity"] = 0.0
        out["wind_volumes"].append(wind)
    for mesh in lods[2:3]:  # far: the ceiling's underside as one slab
        prism.member(mesh, xf, (x_in0, H, z_in0), (x_in1, y1, z_in1), TIMBER_SLOT, TIMBER)


def _ceiling_down(plan, params, ch, lods, out, rngs):
    """The ceiling with `interior_ceiling_down` of it fallen (design decision 6; the sheet: the ceiling still in place
    over the door room, boards and clay down in the others): in each room but the hall, strips of CEILING_CELL_BOARDS
    boards between two joists (or a joist and a wall) fall, the lowest of a smooth seeded noise (stream "ceiling
    down") first, until that share of the whole ceiling's area is down. Joists and beams stay. A board left breaks over
    its joist, K03's splinters reaching no farther than the joist's top (BOARD_SPLINTER_M), so the clear space
    between the joists' faces is the hole's; its clay crumbles back by CLAY_SETBACK_M, the stub bare. Each hole between
    two joists, split by a beam under it, is a gap; what fell goes to the rubble (out["fallen"], out["holes"]) and the
    rooms' joists to out["joists"]."""
    x_in0, x_in1, z_in0, z_in1 = plan.inner
    H = plan.H
    jw, jh = JOIST_M
    clear = 0.03
    hole = (ch["x0"] - clear, ch["x1"] + clear, ch["z0"] - clear, ch["z1"] + clear) if ch else None
    xf = prism.Frame((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
    zf = prism.Frame((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (-1.0, 0.0, 0.0))
    joists, beams = {}, {}
    for si, space in enumerate(plan.spaces):  # the joists and beams, as the intact ceiling's
        n = int((space.z1 - space.z0) / JOIST_PITCH_M)
        zs = [((k + 0.5) - n / 2) * JOIST_PITCH_M + (space.z0 + space.z1) / 2 for k in range(n)]
        joists[si] = zs
        for z in zs:
            spans = [(space.x0, space.x1)]
            if hole and hole[2] < z + jw / 2 and hole[3] > z - jw / 2:
                spans = [(a, b) for a, b in ((space.x0, min(space.x1, hole[0])), (max(space.x0, hole[1]), space.x1))
                         if b - a >= MIN_JOIST_M]
            for a, b in spans:
                for mesh in lods[:2]:
                    _, box = prism.member(mesh, xf, (a, H - jh, z - jw / 2), (b, H, z + jw / 2), TIMBER_SLOT,
                                          TIMBER, offset=(0.3 * z, 0.1 * a))
                out["collision"].append(box)
        if space.x1 - space.x0 > 2.5:
            xc = (space.x0 + space.x1) / 2
            bw, bh = BEAM_M
            beams[si] = (xc - bw / 2, xc + bw / 2)
            for mesh in lods[:2]:
                _, box = prism.member(mesh, zf, (space.z0, H - jh - bh, -xc - bw / 2), (space.z1, H - jh, -xc + bw / 2),
                                      TIMBER_SLOT, TIMBER, offset=(0.4, 0.2))
            out["collision"].append(box)
    out["joists"] = {plan.spaces[si].name: zs for si, zs in joists.items()}
    bw, bt, gap = BOARD_M
    count = int((x_in1 - x_in0 + gap) / (bw + gap))
    pitch = (x_in1 - x_in0 + gap) / count
    columns = [(x_in0 + k * pitch, x_in0 + k * pitch + pitch - gap) for k in range(count)]
    cells = []
    for si, space in enumerate(plan.spaces):
        if space.name == "hall":
            continue
        cols = [k for k, (a, b) in enumerate(columns) if a >= space.x0 - 1e-9 and b <= space.x1 + 1e-9]
        edges = [space.z0] + joists[si] + [space.z1]
        for g in range(0, len(cols), CEILING_CELL_BOARDS):
            group = cols[g:g + CEILING_CELL_BOARDS]
            xa, xb = columns[group[0]][0], min(columns[group[-1]][0] + pitch, x_in1)
            for za, zb in zip(edges, edges[1:]):
                if hole and hole[0] < xb and xa < hole[1] and hole[2] < zb and za < hole[3]:
                    continue
                cells.append({"space": si, "cols": group, "x": (xa, xb), "z": (za, zb)})
    want = params["interior_ceiling_down"] * (x_in1 - x_in0) * (z_in1 - z_in0)
    span = 2 * (plan.X + plan.Z) + 4.0
    noise = masks.PeriodicNoise(rngs("ceiling down"), 2 * span, 1.5, span)
    order = sorted((noise(sum(c["x"]) / 2 + span / 2, sum(c["z"]) / 2 + span / 2), i) for i, c in enumerate(cells))
    down, area = [], 0.0
    for _, i in order:
        if area >= want - 1e-9:
            break
        down.append(cells[i])
        area += (cells[i]["x"][1] - cells[i]["x"][0]) * (cells[i]["z"][1] - cells[i]["z"][0])
    out["ceiling_down_m2"], out["ceiling_m2"] = area, (x_in1 - x_in0) * (z_in1 - z_in0)
    lines = {z for zs in joists.values() for z in zs}
    clay_rects = []
    rows = ((0.0, 0.0, 1.0), (0.0, 1.0, 0.0), (-1.0, 0.0, 0.0))  # a board's K03 frame: along z, up, across -x
    for k, (a, b) in enumerate(columns):
        merged = []  # this column's holes, neighbouring bays down as one hole over the joist between them
        for za, zb in sorted(c["z"] for c in down if k in c["cols"]):
            if merged and abs(merged[-1][1] - za) < 1e-9:
                merged[-1] = (merged[-1][0], zb)
            else:
                merged.append((za, zb))
        cuts = list(merged) + ([(hole[2], hole[3])] if hole and hole[0] < b and hole[1] > a else [])
        kept, z = [], z_in0
        for za, zb in sorted(cuts):
            if za - z > 0.05:
                kept.append((z, za))
            z = max(z, zb)
        if z_in1 - z > 0.05:
            kept.append((z, z_in1))
        rigid = kits_vehicle.Rigid(tuple(tuple(rows[j][i] for j in range(3)) for i in range(3)), ((a + b) / 2, H, 0.0))
        for c0, c1 in kept:
            ends = []  # per end: (the board's end, its clay's end, its break or None)
            for zz, dirn in ((c0, -1), (c1, 1)):
                broken = any(abs(zz - j) < 1e-9 for j in lines) and any(
                    abs((za if dirn > 0 else zb) - zz) < 1e-9 for za, zb in merged)
                if not broken:
                    ends.append((zz, zz, None))
                    continue
                r = rngs(f"ceiling board {k} {zz:.4f}")
                reach = r.uniform(*BOARD_SPLINTER_M)
                end = zz + dirn * r.uniform(0.0, max(0.0, jw / 2 - reach - 0.005))
                ends.append((end, end - dirn * r.uniform(*CLAY_SETBACK_M), (r, reach, dirn)))
            (z0, cz0, brk0), (z1, cz1, brk1) = ends
            kinds = {key: CUT_TIMBER_SLOT for key, brk in (("x-", brk0), ("x+", brk1)) if brk}
            for mesh in lods[:2]:
                prism.member(mesh, zf, (z0, H, -b), (z1, H + bt, -a), TIMBER_SLOT, TIMBER, offset=(0.21 * k, 0.37 * k),
                             slot_by_kind=kinds)
            clay_rects.append((a, min(a + pitch, x_in1), cz0, cz1))
            for zz, cz, brk in ((z0, cz0, brk0), (z1, cz1, brk1)):
                if not brk:
                    continue
                r, reach, dirn = brk
                tmp = [Mesh(), Mesh()]
                collision, snag, _ = debris_timber.broken_end(tmp, debris_timber.UV(bw, bt, (0.21 * k, 0.37 * k)), r,
                                                              bw, bt, zz, dirn, reach, BOARD_SPLINTERS)
                for mesh, piece in zip(lods[:2], tmp):
                    kits_vehicle.append_mesh(mesh, piece, rigid)
                out["collision"] += [kits_vehicle.transform_shape(c, rigid) for c in collision]
                out["snag"] += [kits_vehicle.transform_snag(n, rigid) for n in snag]
                if abs(zz - cz) > 1e-4:  # the bare stub past its clay
                    out["collision"].append(prism.axis_box((a, H, min(zz, cz)), (b, H + bt, max(zz, cz)), TIMBER))
        for za, zb in merged:  # what fell of this column: its board between the joists, and its clay
            si = next(c["space"] for c in down if k in c["cols"])
            out["fallen"].append({"kind": "board", "space": si, "x": (a, b), "z": (za, zb), "length": zb - za,
                                  "section": (bw, bt), "volume_m3": (zb - za) * bw * bt})
            out["fallen"].append({"kind": "clay", "space": si, "rect": (a, min(a + pitch, x_in1), za, zb),
                                  "volume_m3": (min(a + pitch, x_in1) - a) * (zb - za) * CLAY_M})
    y0, y1 = H, H + bt + CLAY_M
    for xa, xb, za, zb in rects.merge(clay_rects):
        for mesh in lods[:2]:
            prism.member(mesh, xf, (xa, H + bt, za), (xb, y1, zb), CLAY_SLOT, MASONRY, offset=(0.0, 0.0))
        out["collision"].append(prism.axis_box((xa, y0, za), (xb, y1, zb), MASONRY))
        wind = prism.axis_box((xa, y0, za), (xb, y1, zb), MASONRY)
        wind.pop("material")
        wind.pop("landable")
        wind["porosity"] = 0.0
        out["wind_volumes"].append(wind)
        prism.member(lods[2], xf, (xa, H, za), (xb, y1, zb), TIMBER_SLOT, TIMBER)  # far: the ceiling left, as slabs
    for si, space in enumerate(plan.spaces):  # the holes: per bay between two joists, runs of strips, a beam between
        edges = [space.z0] + joists[si] + [space.z1]
        for za, zb in zip(edges, edges[1:]):
            runs = []
            for xa, xb in sorted(c["x"] for c in down if c["space"] == si and c["z"] == (za, zb)):
                if runs and xa - runs[-1][1] < 1e-6:
                    runs[-1][1] = xb
                else:
                    runs.append([xa, xb])
            z_lo = za + (jw / 2 if za in joists[si] else 0.0)
            z_hi = zb - (jw / 2 if zb in joists[si] else 0.0)
            for xa, xb in runs:
                out["holes"].append({"space": si, "rect": (xa, xb, za, zb)})
                parts = [(xa, xb)]
                if si in beams and xa < beams[si][1] and beams[si][0] < xb:
                    parts = [(p0, p1) for p0, p1 in ((xa, beams[si][0]), (beams[si][1], xb)) if p1 - p0 > 0.05]
                for p0, p1 in parts:
                    poly = [[p0, H, z_lo], [p1, H, z_lo], [p1, H, z_hi], [p0, H, z_hi]]
                    out["gaps"].append({"id": f"ceiling_{space.name}_x{p0 + plan.X:.2f}_z{za + plan.Z:.2f}",
                                        "polygon": poly, "normal": [0.0, 1.0, 0.0],
                                        "narrowest_m": round(audit.inscribed_diameter(poly, [0.0, 1.0, 0.0]), 6)})


def _far_cover(plan, slopes, layout, lods, regions=None):
    """LOD2 and LOD3: each slope's cover as one slab over its polygon, UV'd in the slope's metres; a stripped roof's
    (`regions`, its courses' regions of tiles left, (slope, polygon in (a, s))) as those regions only, top and
    underside at LOD2 and the top at LOD3, so the far roof shows the same holes."""
    if regions is not None:
        n0, n1 = layout.n_head, layout.n_tail + layout.t
        for slope, poly in regions:
            for mesh, faces in ((lods[2], ((n1, 1), (n0, -1))), (lods[3], ((n1, 1),))):
                for n, sgn in faces:
                    verts = [slope.point(a, s_, n) for a, s_ in poly]
                    mesh.face([mesh.vertex(v) for v in verts], tuple(sgn * c for c in slope.en), TILE_SLOT,
                              [(a, s_) for a, s_ in poly])
        return
    for slope in slopes:
        s0, s1 = -layout.e, slope.S
        lo0, hi0 = slope.a_range(0.0)
        lo1, hi1 = slope.a_range(s1)
        dlo = (lo1 - lo0) / s1
        dhi = (hi1 - hi0) / s1
        poly = [(lo0 + dlo * s0, s0), (hi0 + dhi * s0, s0), (hi1, s1), (lo1, s1)]
        if abs(hi1 - lo1) < 1e-9:
            poly = poly[:3]
        n0, n1 = layout.n_head, layout.n_tail + layout.t
        for mesh, both in ((lods[2], True), (lods[3], False)):
            for n, sgn in ((n1, 1), (n0, -1)) if both else ((n1, 1),):
                verts = [slope.point(a, s, n) for a, s in poly]
                normal = tuple(sgn * c for c in slope.en)
                mesh.face([mesh.vertex(v) for v in verts], normal, TILE_SLOT, [(a, s) for a, s in poly])
            if both:
                for (a0, s_0), (a1, s_1) in zip(poly, poly[1:] + poly[:1]):
                    quad = [slope.point(a0, s_0, n0), slope.point(a1, s_1, n0), slope.point(a1, s_1, n1),
                            slope.point(a0, s_0, n1)]
                    normal = _newell(quad)
                    mid = slope.point((a0 + a1) / 2, (s_0 + s_1) / 2)
                    inner = slope.point(sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))
                    if prism.dot(normal, tuple(mid[k] - inner[k] for k in range(3))) < 0:
                        normal = tuple(-c for c in normal)
                    mesh.face([mesh.vertex(v) for v in quad], normal, TILE_SLOT,
                              [(a0, 0.0), (a1, 0.0), (a1, 0.05), (a0, 0.05)])


def _newell(pts):
    n = [0.0, 0.0, 0.0]
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    length = math.sqrt(sum(c * c for c in n))
    return tuple(c / length for c in n) if length > 0 else (0.0, 1.0, 0.0)
