"""Writes a synthetic terrain-only test map: gentle steppe relief, crater fields, a gully (balka),
two road embankments with a ditch and an earth bank. Used to measure the terrain renderer.

    python tools/map/make_synthetic.py OUT_DIR [--size-m 4096] [--seed 7]

The output is a complete map-format 1.0 package with a 1 m heightfield, one surface (meadow_sod),
no cover and no objects. The same seed always gives the same bytes. OUT_DIR is best kept out of git
(for example build/maps/synthetic_4km), because the heightfield alone is 33 MB.
"""
import argparse
import json
import math
import os
import random
import sys
import time
from array import array

from mappng import GREY, RGBA, write_png

COARSE = 8  # metres between fBm samples before upsampling to the 1 m grid
OCTAVES = [(2048, 9.0), (1024, 4.5), (512, 2.2), (256, 1.1), (128, 0.5), (64, 0.25), (32, 0.12)]  # (wavelength m, amplitude m)
HEIGHT_SCALE = 0.01
SURFACE_RES = 0.5
MEADOW_SOD = 1


def value_noise_grid(rng, size, n):
    """Sum of value-noise octaves sampled every COARSE metres: n × n floats, row-major."""
    grid = [0.0] * (n * n)
    for wavelength, amplitude in OCTAVES:
        cells = size // wavelength + 2
        lattice = [rng.uniform(-1.0, 1.0) for _ in range(cells * cells)]
        for r in range(n):
            fz = r * COARSE / wavelength
            iz = int(fz)
            tz = fz - iz
            tz = tz * tz * (3 - 2 * tz)
            row0, row1 = iz * cells, (iz + 1) * cells
            for c in range(n):
                fx = c * COARSE / wavelength
                ix = int(fx)
                tx = fx - ix
                tx = tx * tx * (3 - 2 * tx)
                top = lattice[row0 + ix] + (lattice[row0 + ix + 1] - lattice[row0 + ix]) * tx
                bottom = lattice[row1 + ix] + (lattice[row1 + ix + 1] - lattice[row1 + ix]) * tx
                grid[r * n + c] += amplitude * (top + (bottom - top) * tz)
    return grid


def upsample(grid, n, samples):
    """Bilinear upsampling of the coarse grid to one float32 row per metre."""
    cols = range(samples)
    j = [min(c // COARSE, n - 2) for c in cols]
    t = [c / COARSE - jc for c, jc in zip(cols, j)]
    rows = []
    for r in range(samples):
        i = min(r // COARSE, n - 2)
        u = r / COARSE - i
        a, b = grid[i * n:(i + 1) * n], grid[(i + 1) * n:(i + 2) * n]
        line = [a[k] + (b[k] - a[k]) * u for k in range(n)]
        rows.append(array("f", [line[jc] + (line[jc + 1] - line[jc]) * tc for jc, tc in zip(j, t)]))
    return rows


def add_craters(rows, rng, size, fields):
    """Crater fields: bowls 1–4 m across and 0.3–1 m deep with a raised rim (reference notes W1)."""
    half, samples = size / 2, len(rows)
    for _ in range(fields):
        cx, cz = rng.uniform(-half + 400, half - 400), rng.uniform(-half + 400, half - 400)
        field_radius = rng.uniform(150, 350)
        for _ in range(rng.randint(400, 900)):
            a, d = rng.uniform(0, 2 * math.pi), field_radius * math.sqrt(rng.random())
            x, z = cx + d * math.cos(a), cz + d * math.sin(a)
            radius = rng.uniform(0.5, 2.0)
            depth, rim = rng.uniform(0.3, min(1.0, 0.3 + 0.5 * radius)), rng.uniform(0.1, 0.3)
            for r in range(max(0, int(z + half - 2 * radius)), min(samples, int(z + half + 2 * radius) + 2)):
                for c in range(max(0, int(x + half - 2 * radius)), min(samples, int(x + half + 2 * radius) + 2)):
                    q = math.hypot(c - half - x, r - half - z) / radius
                    if q < 1:
                        rows[r][c] += -depth + (depth + rim) * q * q
                    elif q < 2:
                        rows[r][c] += rim * (2 - q) ** 2


def add_line(rows, size, origin, angle_deg, length, reach, profile):
    """Adds profile(s) along a straight segment, where s is the signed distance (m) from its centre line."""
    half, samples = size / 2, len(rows)
    dx, dz = math.cos(math.radians(angle_deg)), math.sin(math.radians(angle_deg))
    ox, oz = origin
    along_x = abs(dx) >= abs(dz)
    for k in range(samples):
        # Walk the major axis; find where the centre line crosses this column (or row).
        p = k - half
        t = (p - ox) / dx if along_x else (p - oz) / dz
        if not 0 <= t <= length:
            continue
        centre = (oz + t * dz if along_x else ox + t * dx) + half
        span = reach / (abs(dx) if along_x else abs(dz))
        for m in range(max(0, int(centre - span)), min(samples, int(centre + span) + 2)):
            x, z = (p, m - half) if along_x else (m - half, p)
            s = -(x - ox) * dz + (z - oz) * dx
            if abs(s) <= reach:
                r, c = (m, k) if along_x else (k, m)
                rows[r][c] += profile(s)


def road(s):
    """A 0.5 m dirt-road embankment 6 m wide, with a 1.2 m deep ditch 7.5 m to one side."""
    a = abs(s)
    h = 0.5 if a <= 3 else 0.5 * (1 - (a - 3) / 2) if a < 5 else 0.0
    if abs(s - 7.5) < 1.5:
        h -= 1.2 * (0.5 + 0.5 * math.cos(math.pi * (s - 7.5) / 1.5))
    return h


def add_gully(rows, rng, size):
    """A balka: a meandering valley 70 m wide and up to 7 m deep crossing the map west to east."""
    half, samples = size / 2, len(rows)
    z0, phase1, phase2 = rng.uniform(-half / 3, half / 3), rng.uniform(0, 6.3), rng.uniform(0, 6.3)
    for c in range(samples):
        x = c - half
        centre = z0 + 180 * math.sin(2 * math.pi * x / 2600 + phase1) + 60 * math.sin(2 * math.pi * x / 700 + phase2)
        depth = 7.0 * min(1.0, (x + half) / 600)  # starts shallow at the west edge
        for r in range(max(0, int(centre + half - 35)), min(samples, int(centre + half + 36))):
            u = (r - half - centre) / 35
            if abs(u) < 1:
                rows[r][c] -= depth * (0.5 + 0.5 * math.cos(math.pi * u))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("out_dir")
    parser.add_argument("--size-m", type=int, default=4096)
    parser.add_argument("--seed", type=int, default=7)
    args = parser.parse_args()
    size, samples, n = args.size_m, args.size_m + 1, args.size_m // COARSE + 1
    if size % COARSE or not 0 < size <= 8192:
        sys.exit("--size-m must be a multiple of 8 and at most 8192")
    start = time.time()
    rng = random.Random(args.seed)

    rows = upsample(value_noise_grid(rng, size, n), n, samples)
    add_gully(rows, rng, size)
    add_line(rows, size, (-size / 2, -size * 0.3), 20, size * 1.2, 9, road)
    add_line(rows, size, (size * 0.15, -size / 2), 100, size * 1.2, 5, lambda s: road(s) if abs(s) < 5 else 0.0)
    add_line(rows, size, (-size * 0.35, size * 0.1), -35, 1200, 5, lambda s: 1.0 * (0.5 + 0.5 * math.cos(math.pi * s / 5)))
    add_craters(rows, rng, size, fields=3)

    low, high = min(min(r) for r in rows), max(max(r) for r in rows)
    offset = math.floor(low) - 1.0
    if (high - offset) / HEIGHT_SCALE > 65535:
        sys.exit(f"relief {low:.1f}..{high:.1f} m does not fit 16 bits at {HEIGHT_SCALE} m steps")

    os.makedirs(args.out_dir, exist_ok=True)
    with open(os.path.join(args.out_dir, "height.r16"), "wb") as f:
        for row in rows:
            quantised = array("H", [round((h - offset) / HEIGHT_SCALE) for h in row])
            if sys.byteorder == "big":
                quantised.byteswap()
            f.write(quantised.tobytes())
    cells = int(size / SURFACE_RES)
    write_png(os.path.join(args.out_dir, "surface.png"), cells, cells, GREY, [bytes([MEADOW_SOD]) * cells] * cells)
    write_png(os.path.join(args.out_dir, "cover.png"), cells, cells, RGBA, [bytes(4 * cells)] * cells)
    with open(os.path.join(args.out_dir, "objects.json"), "w", newline="\n") as f:
        json.dump({"objects": []}, f, indent=2)
        f.write("\n")
    manifest = {
        "format_version": "1.0",
        "size_m": size,
        "seed": args.seed,
        "height": {"resolution_m": 1.0, "samples_per_side": samples, "offset_m": offset, "scale_m": HEIGHT_SCALE},
        "surface": {"resolution_m": SURFACE_RES, "cells_per_side": cells},
    }
    with open(os.path.join(args.out_dir, "map.json"), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"{args.out_dir}: {size} m, heights {low:.2f}..{high:.2f} m, {time.time() - start:.1f} s")


if __name__ == "__main__":
    main()
