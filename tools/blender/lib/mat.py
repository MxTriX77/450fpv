"""The mat's shared seeded fields (p1-s04-belt-straw task 1.7): the tussock relief and the green regrowth strips of
a ground patch, one definition for the map and the mesh, as lib.patches is for the bare patches. Plain Python, no
Blender and no Pillow: tools/assets/derive.py darkens the albedo and the occlusion in the relief's hollows and
rasterises the strips from it; generators/ground_patch builds the slab's top from the relief, heaps the straws on
its crowns and grows the green blades over the strips.

Points are asset metres in the ground plane, (x, z), the origin at the patch centre; angles are degrees from +X
towards +Z (the recipes' lodging convention).

The relief is the lodged mat's tussocks (G 1-12: a lumpy mat of humps about half a metre across with dark hollows
between them; H 12-17: heaped into ridges). It is billowed value noise, |n|, on a lattice TUSSOCK_M across and
TUSSOCK_M * (1 + bias) along the lodging azimuth, so each hump is a little longer along the lodging and the humps
meet in narrow creases, plus SWELL_SHARE of plain value noise on a SWELL_M lattice for the broader heaps. It is
shifted and scaled so that over the (n + 1)^2 vertices of the generator's grid (n = round(size_m / cell_m)) its mean
is exactly 0 and its largest magnitude exactly relief_m: the mat boxes' flat top stays the mean top within the
audit's 5 cm.

The green strips are blobs (an ellipse and BLOB_SATELLITES satellites) along the lodging azimuth within
GREEN_SPREAD_DEG, drawn until their hard area outside the bare outlines, measured on a GREEN_SAMPLE_M grid, lands
within SHARE_TOLERANCE of `share`; a strip that would overshoot shrinks by SHRINK. The map blurs them over
GREEN_FEATHER_M (derive.py) and the blades thin out over the same feather (green_weight).
"""
import math
import random

from lib import patches  # the bare outlines the green strips are measured outside of

FIELDS = "mat fields v1"   # bumped with any change to the code below; derive.py hashes it with the constants
TUSSOCK_M = 0.5            # the tussock lattice across the lodging (G 1-12: humps about half a metre across; assumed)
SWELL_M = 1.5              # the broad lattice: the mat heaped into ridges (H 12-17; assumed)
SWELL_SHARE = 0.5          # the swell's amplitude against the tussocks' (assumed)
GREEN_FEATHER_M = 0.5      # regrowth fades into the mat over half a metre (assumed; derive.py's before)
GREEN_SPREAD_DEG = 10.0    # a green strip runs within this of the lodging azimuth (assumed; derive.py's before)
BLOB_SATELLITES = 3        # a strip is an ellipse and this many satellites (assumed; derive.py's before)
BLOB_OFFSET = 0.35         # a satellite's centre within this share of the strip's axes (assumed)
BLOB_SCALE = (0.45, 0.8)   # a satellite's size as a share of the strip's (assumed)
GREEN_SAMPLE_M = 0.1       # the share's sample grid: a tenth of the narrowest strip
SHARE_TOLERANCE = 0.005    # the strips' share lands within this of `share`
SHRINK = 0.75              # a strip that overshoots the share shrinks by this
STRIP_TRIES = 10000        # strips drawn before the layout gives up
CONSTANTS = ("TUSSOCK_M", "SWELL_M", "SWELL_SHARE", "GREEN_FEATHER_M", "GREEN_SPREAD_DEG", "BLOB_SATELLITES",
             "BLOB_OFFSET", "BLOB_SCALE", "GREEN_SAMPLE_M", "SHARE_TOLERANCE", "SHRINK", "STRIP_TRIES")


def constants():
    """The current value of every constant the fields read, for derive.py's stamp."""
    return {"FIELDS": FIELDS, **{name: list(globals()[name]) if isinstance(globals()[name], tuple) else globals()[name]
                                 for name in CONSTANTS}}


# --- the tussock relief -------------------------------------------------------------------------------------------

def relief_inputs(params):
    """The relief's inputs from a ground_patch params dict, as relief() takes them (and derive.py records them)."""
    return {"seed": params["seed"], "size_m": params["size_m"], "cell_m": params["cell_m"],
            "relief_m": params["relief_m"], "azimuth_deg": params["lodging"]["azimuth_deg"],
            "bias": params["lodging"]["bias"]}


def relief(seed, size_m, cell_m, relief_m, azimuth_deg, bias):
    """The relief as a function (x, z) -> height in metres: mean 0 and largest magnitude relief_m over the
    generator's grid vertices (0 everywhere when relief_m is 0)."""
    rng = random.Random(f"mat relief {seed}")
    c, s = math.cos(math.radians(azimuth_deg)), math.sin(math.radians(azimuth_deg))
    reach = size_m / 2 * math.sqrt(2)  # the rotated frame covers the square's corners
    tussock = value_noise(rng, reach, TUSSOCK_M * (1 + bias), TUSSOCK_M)
    swell = value_noise(rng, reach, SWELL_M * (1 + bias), SWELL_M)

    def raw(x, z):
        u, v = x * c + z * s, -x * s + z * c  # along and across the lodging
        return abs(tussock(u, v)) + SWELL_SHARE * swell(u, v)

    n = max(1, round(size_m / cell_m))
    samples = [raw(-size_m / 2 + i * size_m / n, -size_m / 2 + j * size_m / n) for i in range(n + 1) for j in range(n + 1)]
    mean = sum(samples) / len(samples)
    peak = max(abs(v - mean) for v in samples)
    scale = relief_m / peak if peak > 0 else 0.0
    return lambda x, z: (raw(x, z) - mean) * scale


def value_noise(rng, reach, along, across):
    """Value noise in [-1, 1] over u, v in [-reach, reach], on a lattice `along` x `across`, smoothstep-interpolated;
    its lattice values drawn from `rng`."""
    nu, nv = int(math.ceil(2 * reach / along)) + 2, int(math.ceil(2 * reach / across)) + 2
    values = [[rng.uniform(-1, 1) for _ in range(nv)] for _ in range(nu)]

    def noise(u, v):
        fu = min(max((u + reach) / along, 0.0), nu - 1.000001)
        fv = min(max((v + reach) / across, 0.0), nv - 1.000001)
        iu, iv = int(fu), int(fv)
        tu, tv = _smooth(fu - iu), _smooth(fv - iv)
        r0, r1 = values[iu], values[iu + 1]
        return (r0[iv] * (1 - tu) + r1[iv] * tu) * (1 - tv) + (r0[iv + 1] * (1 - tu) + r1[iv + 1] * tu) * tv

    return noise


def _smooth(t):
    return t * t * (3 - 2 * t)


# --- the green strips ---------------------------------------------------------------------------------------------

def green_inputs(params):
    """The strips' inputs from a ground_patch params dict, as green() takes them besides the bare patches."""
    green = params["green"]
    return {"seed": params["seed"], "size_m": params["size_m"], "share": green["share"],
            "width_m": [green["width_m"]["min"], green["width_m"]["max"]],
            "length_m": [green["length_m"]["min"], green["length_m"]["max"]],
            "azimuth_deg": params["lodging"]["azimuth_deg"]}


def green(seed, size_m, share, width_m, length_m, azimuth_deg, bare_patches):
    """{"strips": [...], "share": the strips' hard area outside the bare outlines over size_m^2}. Each strip:
    "centre", "angle_deg" and "parts", the ellipses (cx, cz, half_length, half_width) at the strip's angle. Raises
    ValueError when the share cannot be reached."""
    rng = random.Random(f"green strips {seed}")
    half = size_m / 2
    count = max(1, round(size_m / GREEN_SAMPLE_M))
    step = size_m / count
    centres = [-half + (k + 0.5) * step for k in range(count)]
    free = [[not patches.inside(bare_patches, x, z) for z in centres] for x in centres]
    covered = [[False] * count for _ in range(count)]
    total, strips = 0, []
    target, tolerance = share * count * count, SHARE_TOLERANCE * count * count
    for _ in range(STRIP_TRIES):
        if total >= target - tolerance:
            return {"strips": strips, "share": total / (count * count)}
        length, width = rng.uniform(*length_m), rng.uniform(*width_m)
        angle = azimuth_deg + rng.uniform(-GREEN_SPREAD_DEG, GREEN_SPREAD_DEG)
        satellites = [(rng.uniform(-BLOB_OFFSET, BLOB_OFFSET), rng.uniform(-BLOB_OFFSET, BLOB_OFFSET),
                       rng.uniform(*BLOB_SCALE)) for _ in range(BLOB_SATELLITES)]
        centre = (rng.uniform(-half, half), rng.uniform(-half, half))
        for _ in range(12):
            strip = _strip(centre, angle, length, width, satellites)
            cells = _cells(strip, centres, step, half)
            added = sum(1 for i, j in cells if free[i][j] and not covered[i][j])
            if total + added <= target + tolerance or min(length, width) <= 0.3:
                break
            length, width = length * SHRINK, width * SHRINK
        for i, j in cells:
            if free[i][j] and not covered[i][j]:
                covered[i][j] = True
        total += added
        strips.append(strip)
    raise ValueError(f"green strips: could not reach a share of {share} in {STRIP_TRIES} strips")


def green_weight(strips, x, z):
    """How green the mat is at (x, z), 0-1: 1 inside a strip, fading to 0 over GREEN_FEATHER_M about its edge, as
    the map's blur does."""
    best = 0.0
    for strip in strips:
        c, s = math.cos(math.radians(strip["angle_deg"])), math.sin(math.radians(strip["angle_deg"]))
        for cx, cz, a, b in strip["parts"]:
            dx, dz = x - cx, z - cz
            u, v = dx * c + dz * s, -dx * s + dz * c
            r = math.hypot(u, v)
            rho = math.hypot(u / a, v / b)
            d = r - r / rho if rho > 0 else -min(a, b)  # signed distance along the ray from the centre
            best = max(best, 1 - _smooth(min(1.0, max(0.0, (d + GREEN_FEATHER_M / 2) / GREEN_FEATHER_M))))
            if best >= 1.0:
                return 1.0
    return best


def ellipse(part, angle_deg, points=48):
    """The outline of one strip part as `points` (x, z) points, for the map's rasteriser."""
    cx, cz, a, b = part
    c, s = math.cos(math.radians(angle_deg)), math.sin(math.radians(angle_deg))
    out = []
    for k in range(points):
        t = 2 * math.pi * k / points
        u, v = a * math.cos(t), b * math.sin(t)
        out.append((cx + u * c - v * s, cz + u * s + v * c))
    return out


def _strip(centre, angle, length, width, satellites):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    parts = []
    for du, dv, scale in [(0.0, 0.0, 1.0)] + satellites:
        u, v = du * length, dv * width
        parts.append((centre[0] + u * c - v * s, centre[1] + u * s + v * c, length * scale / 2, width * scale / 2))
    return {"centre": centre, "angle_deg": angle, "parts": parts}


def _cells(strip, centres, step, half):
    """The sample cells whose centres lie inside one of the strip's ellipses."""
    c, s = math.cos(math.radians(strip["angle_deg"])), math.sin(math.radians(strip["angle_deg"]))
    count = len(centres)
    cells = set()
    for cx, cz, a, b in strip["parts"]:
        r = max(a, b)
        i0, i1 = max(0, int((cx - r + half) / step)), min(count - 1, int((cx + r + half) / step))
        j0, j1 = max(0, int((cz - r + half) / step)), min(count - 1, int((cz + r + half) / step))
        for i in range(i0, i1 + 1):
            dx = centres[i] - cx
            for j in range(j0, j1 + 1):
                dz = centres[j] - cz
                u, v = dx * c + dz * s, -dx * s + dz * c
                if (u / a) ** 2 + (v / b) ** 2 <= 1:
                    cells.add((i, j))
    return cells
