"""Shared pieces for the building builders in blender/structures/ (notes B1, B3, B6, W4).

A wall is a rectangle with rectangular openings cut out of it. `solids` gives the solid rectangles that are left, so
a door or a window is a real hole through the wall and the piers and lintels around it are real boxes. Each is drawn
and collided as that one box, so collision follows the visual to 0 mm.

What a wall's surface is made of is not geometry: a `Facade` lays every face of a building's walls out on one
continuous map, its elevation unrolled around the outside, and paints that map with where the whitewash, the render
and the core show, where soot and water have marked it. The facade material (game/assets/materials/facade.gdshader)
reads the map through the mesh's second UV and draws the layers with textures that run on unbroken across every box
of the wall. So the plaster comes off in patches shaped by the damage, at no scale tied to how the wall was cut up.
"""
import math
import os
import random

TOL = 1e-9


def solids(length, height, openings):
    """The solid rectangles of a wall `length` x `height` m with `openings` cut out, as (u0, u1, v0, v1) m: u along
    the wall from its centre, v up from its foot. Full-height piers between the openings, plus the band under and
    the lintel band over each one. The openings must not overlap in u, which is true of a wall of a house."""
    rects, u = [], -length / 2
    for u0, u1, v0, v1 in sorted(openings, key=lambda o: o[0]):
        if u0 > u + TOL:
            rects.append((u, u0, 0.0, height))
        if v0 > TOL:
            rects.append((u0, u1, 0.0, v0))
        if v1 < height - TOL:
            rects.append((u0, u1, v1, height))
        u = u1
    if u < length / 2 - TOL:
        rects.append((u, length / 2, 0.0, height))
    return rects


def wall(mesh, shapes, axis, face, centre, length, thickness, height, openings, material, base=0.0, facade=None):
    """Draws one axis-aligned wall, one box per solid rectangle, and appends their collision boxes to `shapes`.

    `axis` is "x" for a wall running east-west (its faces look along Z) or "z" for one running north-south. `face`
    is the wall's centre-line coordinate across its thickness, `centre` its centre-line coordinate along `axis`,
    and `base` the height of its foot. The boxes are UV-mapped upright in asset space, so a texture runs on unbroken
    from one box to the next. `facade` is a `Facade.frame` for this wall: it places every face on the building's
    damage map and records the wall's openings there. Returns the solid rectangles, so a caller can draw trim.
    """
    rects = solids(length, height, openings)
    if facade is not None:
        facade.add_wall(centre, base, rects, openings)
    for u0, u1, v0, v1 in rects:
        if axis == "x":
            at, size = (centre + (u0 + u1) / 2, base + (v0 + v1) / 2, face), (u1 - u0, v1 - v0, thickness)
        else:
            at, size = (face, base + (v0 + v1) / 2, centre + (u0 + u1) / 2), (thickness, v1 - v0, u1 - u0)
        shapes.append(mesh.box(at, size, material, upright=True, uv2=facade))
    return rects


# The layer cuts of the facade map's R channel, shared with facade.gdshader's defaults: whitewash below WASH_CUT,
# the render under it between the two, and the wall's core above CORE_CUT.
WASH_CUT, CORE_CUT = 0.40, 0.64


class Facade:
    """One building's facade map, and the frames that place each wall's faces on it.

    The map is metres wide by metres tall, unrolled from the building: band 0 is the outside elevation, walked round
    the building with the outside on the left, so it is exactly `perimeter` wide and wraps (the texture repeats);
    the other bands hold inside faces. A face's second UV is its place in its band: outer faces at their own
    position; a face across the wall's thickness (a reveal, a sill, a lintel soffit, a wall head) folded out of the
    outer face's edge by its depth, into the opening or over the wall top, so it continues the facade it breaks.

    `paint` writes the map as RGBA: R the damage depth (whitewash, then render, then core, at WASH_CUT and CORE_CUT),
    G soot, B water and dirt, A the painted base band. Each is placed from the walls and openings the frames saw.
    """

    def __init__(self, perimeter, bands, px_per_m=48):
        self.width = perimeter
        self.bands = bands                      # each band's bottom, m; the last is the map's height
        self.px = (int(round(perimeter * px_per_m / 4)) * 4, int(round(bands[-1] * px_per_m / 4)) * 4)
        self.solid, self.holes = set(), set()   # (band, s0, s1, v0, v1)

    def frame(self, axis, face, thickness, outward, s_origin, outer_band=0, inner_band=1):
        """The frame of a wall whose outer face looks along +axis' normal times `outward` (+1 or -1), at
        s = s_origin + the face's position along its right-hand direction."""
        return _Frame(self, axis, face, thickness, outward, s_origin, outer_band, inner_band)

    def paint(self, path, seed, loss=0.3, inner_loss=0.12, corners=(), burnt=(), pocks=(), blasts=(), band_m=0.0,
              inner_soot=()):
        """Paints and saves the map (see the class). `loss` is the share of the outside the whitewash is off,
        `inner_loss` the same inside; `corners` the building's corners, s in band 0; `burnt` (band, s, strength)
        for the openings a fire vented through, found by an s inside them, which blacken above; `pocks` (band, s, v, spread m,
        count) for fragment strikes; `blasts` (band, s, v, rx, ry) for the sheets of render a blast took off;
        `band_m` the height of the painted base band; `inner_soot` (band, s0, s1, strength) for smoke-blackened
        inside walls, darkest at the top."""
        import numpy as np
        rng = np.random.default_rng(seed)
        w_px, h_px = self.px
        s = (np.arange(w_px) + 0.5) * self.width / w_px
        v = (np.arange(h_px) + 0.5) * self.bands[-1] / h_px
        S, V = np.meshgrid(s, v)                     # row 0 is the bottom, as Blender stores an image
        band = np.digitize(V, self.bands[1:-1])      # which band each pixel is in
        local = V - np.take(np.array(self.bands[:-1]), band)
        noise = lambda cell, octaves=1: _fbm(rng, w_px, h_px, self.width, self.bands[-1], cell, octaves)

        walls = np.zeros((h_px, w_px), bool)
        top = np.zeros((h_px, w_px))                 # the wall top over each pixel of its own band
        for b, s0, s1, v0, v1 in self.solid:
            inside = (band == b) & (S >= s0) & (S <= s1) & (local >= v0) & (local <= v1)
            walls |= inside
            column = (band == b) & (S >= s0) & (S <= s1)
            top = np.where(column, np.maximum(top, v1), top)
        holes = sorted(self.holes)
        near_hole = np.zeros((h_px, w_px))
        for b, s0, s1, v0, v1 in holes:
            d = np.hypot(np.maximum(np.maximum(s0 - S, S - s1), 0), np.maximum(np.maximum(v0 - local, local - v1), 0))
            near_hole = np.maximum(near_hole, np.where(band == b, np.exp(-d / 0.3), 0))
        near_corner = np.zeros((h_px, w_px))
        for c in corners:
            d = np.abs(S - c)
            d = np.minimum(d, self.width - d)
            near_corner = np.maximum(near_corner, np.where(band == 0, np.exp(-d / 0.3), 0))
        foot = np.clip(1 - local / 0.6, 0, 1)
        head = np.where(top > 0, np.exp(-np.maximum(top - local, 0) / 0.35), 0)

        # R: the damage depth. Broken most round the openings, at the corners, at the wet foot and under the wall head,
        # then shifted so the whitewash is off over `loss` of the outside and `inner_loss` of the inside.
        depth = noise(1.8, 5) + 0.30 * near_hole + 0.18 * near_corner + 0.20 * foot + 0.12 * head
        for b, target in ((0, loss), (1, inner_loss)):
            area = walls & (band == b) if b == 0 else walls & (band > 0)
            if area.any():
                depth = np.where(band == 0 if b == 0 else band > 0, depth + _shift(depth[area], target), depth)
        ragged = noise(0.25, 3)
        for b, s0, v0, rx, ry in blasts:
            e = ((S - s0) / rx) ** 2 + ((local - v0) / ry) ** 2
            depth = np.where(band == b, depth + 0.45 * np.clip(2.2 * (1 - e) + 1.4 * (ragged - 0.5), 0, 1), depth)
        for b, s0, v0, spread, count in pocks:
            for _ in range(count):
                cs, cv, radius = s0 + rng.normal(0, spread), v0 + rng.normal(0, spread * 0.6), rng.uniform(0.03, 0.11)
                d = np.hypot(S - cs, local - cv) / radius
                depth = np.where(band == b, np.maximum(depth, 0.95 - 0.5 * d), depth)

        # G: soot, in plumes over the openings a fire vented through and on the inside walls it filled with smoke.
        soot = np.zeros((h_px, w_px))
        tongues = noise((0.10, 0.7), 2)
        for at_band, at_s, strength in burnt:
            b, s0, s1, v0, v1 = next(h for h in holes if h[0] == at_band and h[1] <= at_s <= h[2])
            half, centre = (s1 - s0) / 2, (s0 + s1) / 2
            rise = local - v1
            spread = half * (1 + 0.9 * np.maximum(rise, 0)) + 0.08
            plume = strength * np.exp(-np.maximum(rise, 0) / 1.3) \
                * np.clip(1.3 - np.abs(S - centre) / spread + 0.9 * (tongues - 0.5), 0, 1)
            inside = (S >= s0) & (S <= s1) & (local >= v0) & (local <= v1)
            soot = np.maximum(soot, np.where((band == b) & (rise > -0.05), plume, 0))
            soot = np.maximum(soot, np.where((band == b) & inside, 0.9 * strength, 0))
        for b, s0, s1, strength in inner_soot:
            smoke = strength * np.clip((local - 0.7) / 1.5, 0, 1) * (0.75 + 0.5 * (tongues - 0.5))
            soot = np.maximum(soot, np.where((band == b) & (S >= s0) & (S <= s1), smoke, 0))

        # B: dirt. Splashed up the foot, washed down in streaks under each sill and from the bare wall head.
        streaks = noise((0.05, 0.45), 2) ** 2
        dirt = 0.75 * foot ** 1.5 * (0.6 + 0.4 * noise(0.3)) * np.where(band == 0, 1.0, 0.4)
        dirt += 0.55 * head * streaks * 1.6
        for b, s0, s1, v0, v1 in holes:
            if v0 < 0.2:
                continue
            below = v0 - local
            across = np.clip(1 - np.maximum(np.abs(S - (s0 + s1) / 2) - (s1 - s0) / 2, 0) / 0.08, 0, 1)
            dirt += np.where((band == b) & (below > 0), 0.9 * np.exp(-below / 0.8) * streaks * across, 0)

        # A: the base band, painted a hand's width off true.
        painted = (band == 0) & (local < band_m + 0.012 * (noise(0.6) - 0.5)) if band_m > 0 else np.zeros_like(walls)

        _save(path, np.stack([np.clip(depth, 0, 1), np.clip(soot, 0, 1), np.clip(dirt, 0, 1),
                              painted.astype(float)], axis=-1))

    def _add(self, store, band, s0, s1, v0, v1):
        store.add((band, round(min(s0, s1), 4), round(max(s0, s1), 4), round(v0, 4), round(v1, 4)))


class _Frame:
    """Places one wall's faces on its facade (see Facade), and records the wall's solids and openings there."""

    def __init__(self, facade, axis, face, thickness, outward, s_origin, outer_band, inner_band):
        self.facade, self.outer_band, self.inner_band = facade, outer_band, inner_band
        self.normal = (0.0, 0.0, float(outward)) if axis == "x" else (float(outward), 0.0, 0.0)
        self.right = (float(outward), 0.0, 0.0) if axis == "x" else (0.0, 0.0, -float(outward))
        self.along = outward if axis == "x" else -outward   # ds per metre of the wall's own u
        self.outer = outward * face + thickness / 2          # the outer face's dot(p, normal)
        self.s_origin = s_origin

    def __call__(self, point, normal):
        s = self.s_origin + _dot(point, self.right)
        v = point[1]
        depth = self.outer - _dot(point, self.normal)
        across, side = _dot(normal, self.normal), _dot(normal, self.right)
        band = self.outer_band
        if across < -0.9:
            band = self.inner_band
        elif side > 0.9:
            s += depth
        elif side < -0.9:
            s -= depth
        elif normal[1] > 0.9:
            v += depth
        elif normal[1] < -0.9:
            v -= depth
        f = self.facade
        return (s / f.width, (f.bands[band] + v) / f.bands[-1])

    def add_wall(self, centre, base, rects, openings):
        """Records a wall's solid rectangles and openings, in its own u and v, on both of its bands."""
        s = lambda u: self.s_origin + self.along * (centre + u)
        for band in {self.outer_band, self.inner_band}:
            for u0, u1, v0, v1 in rects:
                self.facade._add(self.facade.solid, band, s(u0), s(u1), base + v0, base + v1)
            for u0, u1, v0, v1 in openings:
                self.facade._add(self.facade.holes, band, s(u0), s(u1), base + v0, base + v1)

    def add_solid(self, u0, u1, v0, v1):
        """Records a solid drawn outside `wall`, such as a gable course, in this wall's u (asset position along its
        axis) and v."""
        for band in {self.outer_band, self.inner_band}:
            self.facade._add(self.facade.solid, band, self.s_origin + self.along * u0, self.s_origin + self.along * u1,
                             v0, v1)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _shift(values, target):
    """The offset that puts `target` of `values` over WASH_CUT."""
    import numpy as np
    return WASH_CUT - float(np.quantile(values, 1 - target))


def _fbm(rng, w_px, h_px, width, height, cell, octaves):
    """Value noise in [0, 1] over a width x height m map, periodic across its width; `cell` is the largest feature
    in m, (across, up) or one figure for both, and each octave halves it at half the weight."""
    import numpy as np
    cu, cv = cell if isinstance(cell, tuple) else (cell, cell)
    total, weight = np.zeros((h_px, w_px)), 0.0
    for k in range(octaves):
        grid_w = max(1, int(round(width / (cu / 2 ** k))))
        grid_h = int(math.ceil(height / (cv / 2 ** k))) + 2
        grid = rng.random((grid_h, grid_w))
        x = (np.arange(w_px) + 0.5) / w_px * grid_w
        y = (np.arange(h_px) + 0.5) / h_px * height / (cv / 2 ** k)
        x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
        fx, fy = x - x0, y - y0
        fx, fy = fx * fx * (3 - 2 * fx), (fy * fy * (3 - 2 * fy))[:, None]
        x1, x0 = (x0 + 1) % grid_w, x0 % grid_w
        a, b = grid[np.ix_(y0, x0)], grid[np.ix_(y0, x1)]
        c, d = grid[np.ix_(y0 + 1, x0)], grid[np.ix_(y0 + 1, x1)]
        total += 0.5 ** k * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
        weight += 0.5 ** k
    return total / weight


def _save(path, rgba):
    """Writes an (h, w, 4) array in [0, 1], row 0 at the bottom, as an 8-bit RGBA PNG of raw data (no colour
    transform), and rounds it to 8 bits first so the file depends only on the numbers."""
    import bpy
    import numpy as np
    h_px, w_px = rgba.shape[:2]
    image = bpy.data.images.new(os.path.splitext(os.path.basename(path))[0], w_px, h_px, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set((np.round(rgba * 255) / 255).astype(np.float32).ravel())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    print(f"painted {path} ({w_px} x {h_px})")


def heap(mesh, shapes, rng, at, spread, count, size, material):
    """A low heap of small rigid pieces: tile shards or brick bits at a wall foot (notes T6, S3). Each piece is a
    box with its own collision, because a shard under a leg is what tilts a drone at lift-off."""
    for _ in range(count):
        dx = rng.gauss(0, spread[0])
        dz = rng.gauss(0, spread[1])
        piece = [s * rng.uniform(0.7, 1.35) for s in size]
        shapes.append(mesh.box((at[0] + dx, at[1] + piece[1] / 2, at[2] + dz), piece, material,
                               rotation_deg=(rng.uniform(0, 360), rng.uniform(-22, 22), rng.uniform(-14, 14)),
                               uv_offset=(rng.uniform(0, 2), rng.uniform(0, 2))))


def silhouette_porosity(boxes, u_range, v_range, step=0.05):
    """The open fraction of `boxes` seen along +Z, over the rectangle `u_range` x `v_range` in (x, y) metres.

    A box turned about X only (a rafter, a batten, a tile patch) projects along Z to exactly its axis-aligned
    extent, so this is exact for a roof; a box turned about Y or Z is counted by that extent, which over-states its
    cover a little. Rasterised, so a whole roof costs one pass per box.
    """
    u0, u1 = u_range
    v0, v1 = v_range
    columns = max(1, round((u1 - u0) / step))
    rows = max(1, round((v1 - v0) / step))
    filled = bytearray(columns * rows)
    for box in boxes:
        half = _extent(box)
        c = box["position_m"]
        for j in range(max(0, math.floor((c[1] - half[1] - v0) / step)),
                       min(rows, math.ceil((c[1] + half[1] - v0) / step))):
            for i in range(max(0, math.floor((c[0] - half[0] - u0) / step)),
                           min(columns, math.ceil((c[0] + half[0] - u0) / step))):
                filled[j * columns + i] = 1
    return round(1 - sum(filled) / (columns * rows), 2)


def _extent(box):
    """Half the box's axis-aligned extent, its rotation included."""
    size = box["size_m"]
    yaw, pitch, roll = box.get("rotation_deg", (0.0, 0.0, 0.0))
    rows = _matrix(yaw, pitch, roll)
    return [sum(abs(rows[r][k]) * size[k] / 2 for k in range(3)) for r in range(3)]


def _matrix(yaw, pitch, roll):
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cx, sx = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    cz, sz = math.cos(math.radians(roll)), math.sin(math.radians(roll))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    mul = lambda a, b: tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
    return mul(mul(ry, rx), rz)
