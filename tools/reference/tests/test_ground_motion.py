"""ground_motion.py on the exact flow of a flat ground: it recovers speed over height, the camera's tilt and roll, the
climb and the crab, with houses and mismatches among the matches. No test opens the real reference/ folder."""
import math
import random

import ground_motion as gm
import track_motion as tm

FPS = 30.0


def rot(axis, a):
    c, s = math.cos(a), math.sin(a)
    if axis == "x":
        return [[1, 0, 0], [0, c, -s], [0, s, c]]
    if axis == "y":
        return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def apply(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))


def transpose(m):
    return [list(r) for r in zip(*m)]


def unit(v):
    n = math.sqrt(sum(x * x for x in v))
    return tuple(x / n for x in v)


def scene(tilt, roll, speed_over_h, climb, crab, turn_deg, seed=1):
    """Matches of one frame pair at 10 m over flat ground (world: x ahead, y right, z down)."""
    h = 10.0
    c0 = [[0, 0, 1], [1, 0, 0], [0, 1, 0]]                       # camera x right = world y, y down = z, z ahead = x
    c = mul(c0, mul(rot("x", -math.radians(tilt)), rot("z", math.radians(roll))))
    turn = mul(rot("x", math.radians(turn_deg[1])), mul(rot("y", math.radians(turn_deg[2])), rot("z", math.radians(turn_deg[0]))))
    c2 = mul(c, turn)
    s = speed_over_h * h / FPS
    g, k = math.radians(climb), math.radians(crab)
    t = (s * math.cos(g) * math.cos(k), s * math.cos(g) * math.sin(k), -s * math.sin(g))
    lens = tm.Lens(1920, 1080, 0.33, 1 / math.sqrt(3 * 0.33))
    rng = random.Random(seed)
    pairs = []
    for fy in range(60, 1080, 90):
        for fx in range(60, 1920, 110):
            a = lens.ray(fx, fy)
            aw = apply(c, a)
            if aw[2] > 0.02:
                p = tuple(v * h / aw[2] for v in aw)
                if rng.random() < 0.15:                            # a house: the match lies nearer than the ground
                    p = tuple(v * 0.5 for v in p)
                b = unit(apply(transpose(c2), tuple(p[i] - t[i] for i in range(3))))
            else:
                b = apply(transpose(c2), aw)                       # sky and far scenery: the turn only
            if rng.random() < 0.05:
                b = unit(tuple(v + rng.uniform(-0.01, 0.01) for v in b))
            pairs.append((a, b))
    return pairs, lens


def test_flat_ground_motion_is_recovered():
    pairs, lens = scene(tilt=28.0, roll=4.0, speed_over_h=1.2, climb=3.0, crab=6.0, turn_deg=(0.2, -0.1, 0.4))
    tilt0 = math.radians(30)
    p0 = [0.0, 0.0, 0.0, 0.0, 0.03 * math.cos(tilt0), 0.03 * math.sin(tilt0), 0.0, -math.tan(tilt0)]
    p, w, resid = gm.fit(pairs, p0, lens.f_px)
    d = gm.describe(p, FPS)
    assert abs(d["v_over_h"] - 1.2) < 0.03
    assert abs(d["tilt_deg"] - 28.0) < 0.5 and abs(d["roll_ground_deg"] - 4.0) < 0.5
    assert abs(d["climb_deg"] - 3.0) < 1.0 and abs(d["crab_deg"] - 6.0) < 1.0
    assert sum(1 for v in w if v == 0) >= 0.1 * len(w)              # the houses and mismatches are let go


def test_a_ground_point_maps_back_to_its_place():
    """A point 3 heights ahead and 1 to the right, seen by the scene() camera, lands there again."""
    lens = tm.Lens(1920, 1080, 0.33, 1 / math.sqrt(3 * 0.33))
    tilt, roll = 28.0, 4.0
    c0 = [[0, 0, 1], [1, 0, 0], [0, 1, 0]]
    c = mul(c0, mul(rot("x", -math.radians(tilt)), rot("z", math.radians(roll))))
    ray = apply(transpose(c), unit((3.0, 1.0, 1.0)))                 # world: ahead, right, down
    x, y = ray[0] / ray[2] * lens.f, ray[1] / ray[2] * lens.f         # pinhole, half-widths
    for _ in range(30):                                               # distort: invert x_u = x_d (1 + k1 r_d^2)
        r2 = (x * x + y * y)
        xd, yd = ray[0] / ray[2] * lens.f / (1 + 0.33 * r2), ray[1] / ray[2] * lens.f / (1 + 0.33 * r2)
        if abs(xd - x) < 1e-12 and abs(yd - y) < 1e-12:
            break
        x, y = xd, yd
    ahead, right = gm.to_ground(lens, lens.cx + x * lens.hw, lens.cy + y * lens.hw, tilt, roll)
    assert abs(ahead - 3.0) < 1e-6 and abs(right - 1.0) < 1e-6
