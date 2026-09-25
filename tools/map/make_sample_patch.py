"""Writes game/maps/sample_patch: the 256 m test patch for the map format, the loader and the world query.

    python tools/map/make_sample_patch.py [OUT_DIR]

A readable test patch built from the reference notes' vocabulary, not final art. +X is east and +Z south.
- North of the tree belt: a tilled field (west, T3a) with straw streaks along its ridges, and a dry cratered field
  (east, T2) with three craters. A 1.2 m spoil bank runs along the belt between them.
- The tree belt (z -64 to -44, T4/T5): lodged straw with a bare strip under the canopy, and a row of tree proxies along
  its south edge.
- The meadow (T1): gentle roll, a crater, a power line on poles and a dirt road (dry crust) to the yard gate.
- The yard (T6): a house, a shed, the gate, household junk and a rubble heap. South of it is an overgrown garden of weeds
  (T7) with a low cable sagging across it (S1).
- Two start points for the launch rails: on the meadow and in the yard.

The golden file (game/src/world/worldquery_golden.json) probes the features that came first at fixed points: the first
ten objects, the belt, the yard, the meadow crater, height() and the start on the meadow. Keep them where they are and
add new content where no probe relies on it, so a re-record changes only hashes and counts.
"""
import json
import math
import os
import sys
from array import array

from mappng import GREY, RGBA, write_png

SIZE, HEIGHT_RES, CELL_RES, SEED = 256, 1.0, 0.5, 20260923
OFFSET, SCALE = -10.0, 0.01
CRATERS = [  # x, z, radius, depth, rim (m); spoil reaches 2 radii out
    (-60.0, 60.0, 2.0, 0.6, 0.2),     # meadow
    (40.0, -100.0, 1.8, 0.8, 0.25),   # dry field
    (78.0, -86.0, 1.2, 0.5, 0.15),
    (104.0, -112.0, 2.0, 1.0, 0.3),
]
BANK = (50.0, 5.0, -70.0, 3.5, 1.2)  # full-height half length, taper, centre z, half width, height (m)
ROAD_Z = (17.0, 22.0)                # the dirt road from the west edge to the gate
MEADOW_SOD, DRY_CRUST, CRATER_SPOIL, BELT_STRAW, BELT_BARE, TILLED, YARD_LITTER, RUBBLE, WEEDS = range(1, 10)


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def bank(x, z):
    """Height of the spoil bank: a cos² profile across it, tapering off at its ends."""
    length, taper, cz, half, top = BANK
    d = abs(z - cz)
    if d >= half:
        return 0.0
    return top * math.cos(math.pi / 2 * d / half) ** 2 * smoothstep((length + taper - abs(x)) / taper)


def height(x, z):
    h = 0.012 * x - 0.006 * z + 0.8 * math.sin(2 * math.pi * x / 180) * math.cos(2 * math.pi * z / 150) + bank(x, z)
    for cx, cz, radius, depth, rim in CRATERS:
        q = math.hypot(x - cx, z - cz) / radius
        if q < 1:
            h += -depth + (depth + rim) * q * q
        elif q < 2:
            h += rim * (2 - q) ** 2
    return h


def surface(x, z):
    if any(math.hypot(x - cx, z - cz) <= 2 * r for cx, cz, r, _, _ in CRATERS):
        return CRATER_SPOIL
    if -64 <= z <= -44:
        return BELT_BARE if abs(z + 54) <= 3 else BELT_STRAW
    if z < -64:
        if bank(x, z) > 0.02:
            return CRATER_SPOIL
        return TILLED if x < 0 else DRY_CRUST
    if 60 <= x <= 68 and 36 <= z <= 44:
        return RUBBLE
    if 30 <= x <= 74 and 8 <= z <= 52:
        return YARD_LITTER
    if 30 <= x <= 74 and 52 < z <= 78:
        return WEEDS
    if ROAD_Z[0] <= z <= ROAD_Z[1] and x < 30:
        return DRY_CRUST
    return MEADOW_SOD


def cover(kind, x, z):
    """RGBA multipliers: R grass, G lodged straw, B twigs, A leaf litter."""
    if kind == MEADOW_SOD:
        return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 7) * math.cos(z / 9))), 0, 0, 0)
    if kind == DRY_CRUST:  # sparse dead tufts, almost none on the beaten road
        on_road = ROAD_Z[0] <= z <= ROAD_Z[1]
        return (25 if on_road else int(150 + 105 * (0.5 + 0.5 * math.sin(x / 5) * math.cos(z / 6))), 0, 0, 0)
    if kind == TILLED:  # straw residue in streaks along the ridges, 6 m apart
        return (0, int(40 + 215 * (0.5 + 0.5 * math.cos(2 * math.pi * z / 6)) ** 2), 0, 0)
    if kind == WEEDS:
        return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 3) * math.cos(z / 4))), 0, 0, 0)
    return {BELT_STRAW: (160, 255, 120, 0), BELT_BARE: (0, 0, 255, 180), YARD_LITTER: (0, 0, 90, 255),
            RUBBLE: (0, 0, 255, 0), CRATER_SPOIL: (0, 0, 0, 0)}[kind]


def placed(asset, x, z, yaw=0.0):
    return {"asset": asset, "position_m": [x, round(height(x, z), 3), z], "rotation_deg": [yaw, 0.0, 0.0], "scale": 1.0}


def at(p, up, dx=0.0, dz=0.0):
    """A point `up` m above an object's origin, moved dx and dz in the world."""
    x, y, z = p["position_m"]
    return [round(x + dx, 3), round(y + up, 3), round(z + dz, 3)]


def objects():
    # The first ten are the golden file's: keep them and their order (indices).
    pole_a, pole_b = placed("pole", -30.0, 0.0), placed("pole", 0.0, 0.0)
    shed = placed("shed_box", 38.0, 44.0, -10.0)
    first = [
        placed("house_box", 52.0, 30.0, 15.0),
        shed,
        placed("gate_frame", 30.5, 20.0, 90.0),
        placed("household_junk", 58.0, 38.0, 30.0),
        placed("tree_proxy", -40.0, -50.0),
        placed("tree_proxy", -10.0, -56.0),
        placed("tree_proxy", 20.0, -48.0),
        pole_a,
        pole_b,
        {"asset": "cable", "points_m": [at(p, 7.8) for p in (pole_a, pole_b)], "sag_m": 0.6, "diameter_m": 0.012},
    ]
    # A row along the belt's south edge, every 8 m, clear of the first three trees' crowns.
    trees = [placed("tree_proxy", float(x), round(-46.0 + 0.6 * math.sin(1.7 * x), 2)) for x in range(-116, 117, 8)
             if all(math.hypot(x - t["position_m"][0], -46.0 - t["position_m"][2]) >= 8 for t in first[4:7])]
    # The power line goes on west from pole_a.
    line = [placed("pole", x, 0.0) for x in (-60.0, -90.0, -120.0)]
    # Shed east wall, 2.2 m up: the shed is 4 m wide, turned -10 degrees.
    wall = at(shed, 2.2, 2.0 * math.cos(math.radians(10)), 2.0 * math.sin(math.radians(10)))
    garden_pole = placed("pole", 66.0, 72.0)
    return first + trees + line + [
        {"asset": "cable", "points_m": [at(p, 7.8) for p in [pole_a] + line], "sag_m": 0.6, "diameter_m": 0.012},
        placed("household_junk", 44.0, 14.0, 70.0),
        placed("household_junk", 35.0, 30.0, 110.0),
        placed("household_junk", 68.0, 48.0, -20.0),
        garden_pole,
        {"asset": "cable", "points_m": [wall, at(garden_pole, 2.0)], "sag_m": 1.0, "diameter_m": 0.01},
    ]


def start(x, z, yaw):
    return {"position_m": [x, round(height(x, z), 2), z], "yaw_deg": yaw}


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "game", "maps", "sample_patch")
    os.makedirs(out, exist_ok=True)
    samples, cells, half = int(SIZE / HEIGHT_RES) + 1, int(SIZE / CELL_RES), SIZE / 2

    with open(os.path.join(out, "height.r16"), "wb") as f:
        for r in range(samples):
            row = array("H", [round((height(-half + c * HEIGHT_RES, -half + r * HEIGHT_RES) - OFFSET) / SCALE) for c in range(samples)])
            if sys.byteorder == "big":
                row.byteswap()
            f.write(row.tobytes())

    kinds, covers = [], []
    for r in range(cells):
        z = -half + (r + 0.5) * CELL_RES
        row_kinds = [surface(-half + (c + 0.5) * CELL_RES, z) for c in range(cells)]
        kinds.append(bytes(row_kinds))
        covers.append(bytes(v for c, k in enumerate(row_kinds) for v in cover(k, -half + (c + 0.5) * CELL_RES, z)))
    write_png(os.path.join(out, "surface.png"), cells, cells, GREY, kinds)
    write_png(os.path.join(out, "cover.png"), cells, cells, RGBA, covers)

    with open(os.path.join(out, "objects.json"), "w", newline="\n") as f:
        json.dump({"objects": objects()}, f, indent=2)
        f.write("\n")

    manifest = {
        "format_version": "1.0",
        "size_m": SIZE,
        "seed": SEED,
        "height": {"resolution_m": HEIGHT_RES, "samples_per_side": samples, "offset_m": OFFSET, "scale_m": SCALE},
        "surface": {"resolution_m": CELL_RES, "cells_per_side": cells},
        # The golden file's launch rails stand on the first start.
        "starts": [start(12.0, -30.0, 90.0), start(66.0, 20.0, 0.0)],
    }
    with open(os.path.join(out, "map.json"), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"wrote {os.path.normpath(out)}")


if __name__ == "__main__":
    main()
