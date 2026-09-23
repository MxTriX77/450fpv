"""Writes game/maps/sample_patch: a minimal valid 256 m map package used to test the format tools.

    python tools/map/make_sample_patch.py [OUT_DIR]

Gentle relief with one crater, six surfaces (meadow, a tree-belt strip, a yard with rubble, crater spoil),
cover on every channel, a few catalog objects and one wire between two poles. The full authored sample that
covers all nine surfaces is a later task; this skeleton only has to be small and valid.
"""
import json
import math
import os
import sys
from array import array

from mappng import GREY, RGBA, write_png

SIZE, HEIGHT_RES, CELL_RES, SEED = 256, 1.0, 0.5, 20260923
OFFSET, SCALE = -10.0, 0.01
CRATER = (-60.0, 60.0, 2.0, 0.6, 0.2)  # x, z, radius, depth, rim (m)
MEADOW_SOD, CRATER_SPOIL, BELT_STRAW, BELT_BARE, YARD_LITTER, RUBBLE = 1, 3, 4, 5, 7, 8


def height(x, z):
    h = 0.012 * x - 0.006 * z + 0.8 * math.sin(2 * math.pi * x / 180) * math.cos(2 * math.pi * z / 150)
    cx, cz, radius, depth, rim = CRATER
    q = math.hypot(x - cx, z - cz) / radius
    if q < 1:
        h += -depth + (depth + rim) * q * q
    elif q < 2:
        h += rim * (2 - q) ** 2
    return h


def surface(x, z):
    if math.hypot(x - CRATER[0], z - CRATER[1]) <= 4:
        return CRATER_SPOIL
    if -64 <= z <= -44:
        return BELT_BARE if abs(z + 54) <= 3 else BELT_STRAW
    if 60 <= x <= 68 and 36 <= z <= 44:
        return RUBBLE
    if 30 <= x <= 74 and 8 <= z <= 52:
        return YARD_LITTER
    return MEADOW_SOD


def cover(kind, x, z):
    """RGBA multipliers: R grass, G lodged straw, B twigs, A leaf litter."""
    if kind == MEADOW_SOD:
        return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 7) * math.cos(z / 9))), 0, 0, 0)
    return {BELT_STRAW: (160, 255, 120, 0), BELT_BARE: (0, 0, 255, 180), YARD_LITTER: (0, 0, 90, 255),
            RUBBLE: (0, 0, 255, 0), CRATER_SPOIL: (0, 0, 0, 0)}[kind]


def placed(asset, x, z, yaw=0.0):
    return {"asset": asset, "position_m": [x, round(height(x, z), 3), z], "rotation_deg": [yaw, 0.0, 0.0], "scale": 1.0}


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

    pole_a, pole_b = placed("pole", -30.0, 0.0), placed("pole", 0.0, 0.0)
    objects = [
        placed("house_box", 52.0, 30.0, 15.0),
        placed("shed_box", 38.0, 44.0, -10.0),
        placed("gate_frame", 30.5, 20.0, 90.0),
        placed("household_junk", 58.0, 38.0, 30.0),
        placed("tree_proxy", -40.0, -50.0),
        placed("tree_proxy", -10.0, -56.0),
        placed("tree_proxy", 20.0, -48.0),
        pole_a,
        pole_b,
        {"asset": "cable", "points_m": [[p["position_m"][0], round(p["position_m"][1] + 7.8, 3), p["position_m"][2]] for p in (pole_a, pole_b)],
         "sag_m": 0.6, "diameter_m": 0.012},
    ]
    with open(os.path.join(out, "objects.json"), "w", newline="\n") as f:
        json.dump({"objects": objects}, f, indent=2)
        f.write("\n")

    manifest = {
        "format_version": "1.0",
        "size_m": SIZE,
        "seed": SEED,
        "height": {"resolution_m": HEIGHT_RES, "samples_per_side": samples, "offset_m": OFFSET, "scale_m": SCALE},
        "surface": {"resolution_m": CELL_RES, "cells_per_side": cells},
    }
    with open(os.path.join(out, "map.json"), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"wrote {os.path.normpath(out)}")


if __name__ == "__main__":
    main()
