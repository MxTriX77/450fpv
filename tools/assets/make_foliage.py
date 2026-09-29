"""Draws the foliage atlas the tree-belt assets are built from, game/assets/textures/foliage_belt/.

    python tools/assets/make_foliage.py

No CC0 library has a foliage cutout set, so this one is made by the team (CREDITS.md, Made by the team) and drawn
from the reference notes' V1 and V2: crowns mixed green and yellow, many part-bare, grey-brown bark and many thin
whippy stems. It writes one RGBA albedo, 1024 x 1024, as four 512 x 512 tiles that the builders pick with UVs:

    row 0 (V 0.0-0.5)    green spray | dry spray
    row 1 (V 0.5-1.0)    bare twigs  | far impostor

A card is 1.2 m across in the game, so a 14 px leaflet is about 3 cm: the real leaflet of a robinia or ash, the
trees these shelterbelts are planted with. The cards are cut out with alpha scissor, so the silhouette is the whole
look: every tile is ragged at its edges and open in the middle, and the crown's density comes from the number of
cards, not from a solid card. Coverage per tile is printed and is the number to watch.

The impostor tile is a whole small tree, trunk and crown, for the last LOD level of every tree asset.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "map"))
from mappng import RGBA, write_png  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "textures", "foliage_belt")
TILE = 512
SEED = 4501

# Leaflet colours at photo albedo (notes V1: crowns mixed green and yellow; the feed adds its own shift).
GREEN = [(0x5c, 0x6a, 0x38), (0x6b, 0x78, 0x40), (0x4a, 0x57, 0x2d), (0x76, 0x80, 0x46), (0x3f, 0x4c, 0x28)]
DRY = [(0x8d, 0x7e, 0x45), (0xa1, 0x90, 0x53), (0x74, 0x62, 0x34), (0x9a, 0x84, 0x48), (0x5f, 0x53, 0x30)]
WOOD = [(0x6e, 0x66, 0x5b), (0x5d, 0x55, 0x4b), (0x7d, 0x74, 0x67)]  # notes V2: grey, bark-stripped or leafless
BARK = (0x5a, 0x52, 0x49)


class Canvas:
    """An RGBA tile. Every mark is opaque where it covers a pixel, so alpha scissor keeps a hard cutout edge."""

    def __init__(self, size):
        self.size = size
        self.pixels = bytearray(size * size * 4)

    def coverage(self):
        return sum(1 for i in range(3, len(self.pixels), 4) if self.pixels[i]) / (self.size * self.size)

    def put(self, x, y, colour):
        if 0 <= x < self.size and 0 <= y < self.size:
            at = (y * self.size + x) * 4
            self.pixels[at:at + 4] = bytes((*colour, 255))

    def ellipse(self, cx, cy, half_long, half_short, angle, colour, vein=None):
        """A leaflet: an ellipse turned by `angle` (radians), with an optional darker midrib along its long axis."""
        ca, sa = math.cos(angle), math.sin(angle)
        reach = int(max(half_long, half_short)) + 1
        for y in range(int(cy) - reach, int(cy) + reach + 1):
            for x in range(int(cx) - reach, int(cx) + reach + 1):
                dx, dy = x - cx, y - cy
                u, v = dx * ca + dy * sa, -dx * sa + dy * ca
                if (u / half_long) ** 2 + (v / half_short) ** 2 <= 1.0:
                    self.put(x, y, colour if vein is None or abs(v) > 0.8 else vein)

    def stroke(self, x0, y0, x1, y1, width, colour):
        """A straight stem, `width` px across."""
        length = max(1, int(math.hypot(x1 - x0, y1 - y0)))
        for i in range(length + 1):
            t = i / length
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            half = width / 2
            for y in range(int(cy - half), int(cy + half) + 1):
                for x in range(int(cx - half), int(cx + half) + 1):
                    if (x - cx) ** 2 + (y - cy) ** 2 <= half * half:
                        self.put(x, y, colour)


def darker(colour, by=0.72):
    return tuple(int(c * by) for c in colour)


def leaf(canvas, rng, x, y, angle, length, palette):
    """One pinnate leaf: a thin rachis with leaflets in opposite pairs, the shape of a robinia or ash leaf."""
    tip = (x + math.cos(angle) * length, y + math.sin(angle) * length)
    canvas.stroke(x, y, tip[0], tip[1], 1.6, darker(palette[0], 0.55))
    pairs = rng.randint(5, 8)
    for i in range(pairs):
        t = 0.18 + 0.82 * (i + 1) / pairs
        bx, by = x + (tip[0] - x) * t, y + (tip[1] - y) * t
        scale = 1.0 - 0.35 * t
        half_long = length * 0.115 * scale * rng.uniform(0.85, 1.15)
        half_short = half_long * rng.uniform(0.42, 0.55)
        for side in (-1, 1):
            sweep = angle + side * rng.uniform(1.0, 1.4)
            lx = bx + math.cos(sweep) * half_long * 0.95
            ly = by + math.sin(sweep) * half_long * 0.95
            colour = rng.choice(palette)
            canvas.ellipse(lx, ly, half_long, half_short, sweep, colour, darker(colour))


def lumpy(angle, phase):
    """The outline of a leaf clump as a radius (in tile halves) at `angle`: never a circle, so no card reads as a blob."""
    return 0.78 + 0.16 * math.sin(3 * angle + phase) + 0.09 * math.sin(7 * angle + 2.1 * phase)


def spray_tile(palette, leaves, seed):
    """A clump of leaves inside a lumpy outline, thinning to nothing at the edge. The card is see-through everywhere
    (a quarter to a third covered), so depth comes from stacking cards, not from one solid card."""
    canvas = Canvas(TILE)
    rng = random.Random(seed)
    centre = TILE / 2
    phase = rng.uniform(0, 6.28)
    drawn = attempts = 0
    while drawn < leaves and attempts < leaves * 40:
        attempts += 1
        angle = rng.uniform(0, 2 * math.pi)
        radius = math.sqrt(rng.random())
        edge = lumpy(angle, phase)
        if radius > edge or rng.random() < (radius / edge) ** 3:  # thins toward the ragged outline
            continue
        x, y = centre + math.cos(angle) * radius * centre, centre + math.sin(angle) * radius * centre
        leaf(canvas, rng, x, y, rng.uniform(0, 2 * math.pi), TILE * rng.uniform(0.10, 0.155), palette)
        drawn += 1
    return canvas


def twig_tile(seed):
    """A fan of bare thin twigs (notes V2): the branch tips of a dead tree and the bare parts of a live crown."""
    canvas = Canvas(TILE)
    rng = random.Random(seed)
    root = (TILE * 0.5, TILE * 0.96)
    for _ in range(14):
        angle = rng.uniform(-2.7, -0.45)
        length = TILE * rng.uniform(0.45, 0.8)
        tip = (root[0] + math.cos(angle) * length, root[1] + math.sin(angle) * length)
        canvas.stroke(root[0], root[1], tip[0], tip[1], rng.uniform(2.0, 3.6), rng.choice(WOOD))
        for _ in range(rng.randint(6, 10)):  # side twigs off each limb
            t = rng.uniform(0.25, 0.95)
            bx, by = root[0] + (tip[0] - root[0]) * t, root[1] + (tip[1] - root[1]) * t
            side = angle + rng.choice((-1, 1)) * rng.uniform(0.35, 0.9)
            reach = length * rng.uniform(0.15, 0.4)
            canvas.stroke(bx, by, bx + math.cos(side) * reach, by + math.sin(side) * reach,
                          rng.uniform(1.0, 1.9), rng.choice(WOOD))
    return canvas


def impostor_tile(seed):
    """A whole tree for the last LOD level: a leaning trunk into a crown of leaf clumps, green with dry patches."""
    canvas = Canvas(TILE)
    rng = random.Random(seed)
    base = (TILE * 0.5, TILE * 0.995)
    fork = (base[0] + rng.uniform(-24, 24), TILE * 0.63)
    for step in range(40):  # a smooth tapering trunk, from 5.5 % of the tile to a third of that
        t = step / 39
        x0 = base[0] + (fork[0] - base[0]) * t
        y0 = base[1] + (fork[1] - base[1]) * t
        canvas.stroke(x0, y0, x0 + (fork[0] - base[0]) / 39, y0 + (fork[1] - base[1]) / 39,
                      TILE * 0.055 * (1 - 0.66 * t), BARK)
    for _ in range(5):  # limbs into the crown
        angle = rng.uniform(-2.5, -0.65)
        reach = TILE * rng.uniform(0.16, 0.26)
        canvas.stroke(fork[0], fork[1], fork[0] + math.cos(angle) * reach, fork[1] + math.sin(angle) * reach,
                      TILE * 0.018, BARK)
    crown, phase = (fork[0], TILE * 0.35), rng.uniform(0, 6.28)
    drawn = attempts = 0
    while drawn < 190 and attempts < 6000:
        attempts += 1
        angle = rng.uniform(0, 2 * math.pi)
        radius = math.sqrt(rng.random())
        if radius > lumpy(angle, phase):
            continue  # broken, irregular outline, never a lollipop circle
        x = crown[0] + math.cos(angle) * radius * TILE * 0.37
        y = crown[1] + math.sin(angle) * radius * TILE * 0.30
        palette = DRY if rng.random() < 0.22 else GREEN
        leaf(canvas, rng, x, y, rng.uniform(0, 2 * math.pi), TILE * rng.uniform(0.075, 0.115), palette)
        drawn += 1
    return canvas


def main():
    tiles = {
        (0, 0): ("green spray", spray_tile(GREEN, 190, SEED)),
        (1, 0): ("dry spray", spray_tile(DRY, 140, SEED + 1)),
        (0, 1): ("bare twigs", twig_tile(SEED + 2)),
        (1, 1): ("far impostor", impostor_tile(SEED + 3)),
    }
    size = TILE * 2
    rows = []
    for y in range(size):
        row = bytearray()
        for col in (0, 1):
            _, canvas = tiles[(col, y // TILE)]
            at = (y % TILE) * TILE * 4
            row += canvas.pixels[at:at + TILE * 4]
        rows.append(bytes(row))
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "foliage_belt_albedo.png")
    write_png(path, size, size, RGBA, rows)
    # Godot import settings, as tools/assets/fetch_cc0.py writes them for the CC0 sets, plus the cutout's alpha border.
    with open(path + ".import", "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join([
            "[remap]", "", 'importer="texture"', 'type="CompressedTexture2D"', "",
            "[deps]", "", 'source_file="res://assets/textures/foliage_belt/foliage_belt_albedo.png"', "",
            "[params]", "",
            "compress/mode=2", "compress/high_quality=true", "compress/normal_map=2", "compress/channel_pack=0",
            "mipmaps/generate=true", "mipmaps/limit=-1",
            "process/fix_alpha_border=true",  # keeps the cutout edge clean where mipmaps blend transparent pixels in
            "detect_3d/compress_to=0", "",
        ]))
    for (col, row), (name, canvas) in sorted(tiles.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        print(f"tile ({col}, {row}) {name:<13} {canvas.coverage() * 100:5.1f} % covered")
    print(f"wrote {os.path.relpath(path, ROOT)} ({os.path.getsize(path) / 1e6:.1f} MB)")


main()
