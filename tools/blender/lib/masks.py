"""Generator-written masks (p1-r01-vaz-sedan, design decision 5): an 8-bit raster in plain Python, a few drawing
primitives, and a deterministic PNG encoder, so a mask is a pure function of the params and the same bytes come out
of Blender's Python and of the tests' (no Pillow, no numpy).

A Raster is `channels` planes of width x height bytes, row 0 at the top of the image. Drawing takes pixel
coordinates (x right, y down) and combines by maximum, so features overlap without order mattering. `png()` writes
greyscale (one channel), RGB or RGBA, filter 0, zlib level 9: identical input gives identical bytes.

The house's damage-mask atlas (p1-h01-adobe-house, design decision 3; `facade_atlas` below) is drawn on the same
raster: R render depth (0 whitewash, 0.5 clay render, 1 bare adobe), G soot, B dirt and streaks, A the base band.
"""
import bisect
import math
import struct
import zlib


class Raster:
    def __init__(self, width, height, channels):
        self.width, self.height, self.channels = width, height, channels
        self.planes = [bytearray(width * height) for _ in range(channels)]

    def put(self, channel, x, y, value):
        """Raises channel at (x, y) to value (0-255), never lowers it; outside the image does nothing."""
        if 0 <= x < self.width and 0 <= y < self.height:
            plane, i = self.planes[channel], y * self.width + x
            v = int(value)
            if v > plane[i]:
                plane[i] = min(255, v)

    def fill(self, channel, value):
        plane = self.planes[channel]
        plane[:] = bytes([max(0, min(255, int(value)))]) * len(plane)

    def rect(self, channel, x0, y0, x1, y1, value):
        """Raises the pixels with x0 <= x < x1 and y0 <= y < y1 (clipped) to value."""
        xa, xb = max(0, int(math.floor(x0))), min(self.width, int(math.ceil(x1)))
        ya, yb = max(0, int(math.floor(y0))), min(self.height, int(math.ceil(y1)))
        if xa >= xb:
            return
        v = max(0, min(255, int(value)))
        plane = self.planes[channel]
        for y in range(ya, yb):
            row = y * self.width
            for i in range(row + xa, row + xb):
                if v > plane[i]:
                    plane[i] = v

    def disc(self, channel, cx, cy, radius, value, feather=0.0):
        """A disc of `radius` px about (cx, cy), full value inside radius - feather, falling linearly to 0 at radius."""
        r = max(radius, 0.5)
        inner = max(0.0, r - feather)
        for y in range(max(0, int(cy - r)), min(self.height, int(cy + r) + 2)):
            for x in range(max(0, int(cx - r)), min(self.width, int(cx + r) + 2)):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if d < r:
                    f = 1.0 if d <= inner or feather <= 0 else (r - d) / (r - inner)
                    self.put(channel, x, y, value * f)

    def line(self, channel, x0, y0, x1, y1, width, value):
        """A segment `width` px wide (at least one pixel) with round ends, at full value."""
        half = max(width, 1.0) / 2
        dx, dy = x1 - x0, y1 - y0
        length2 = dx * dx + dy * dy
        for y in range(max(0, int(min(y0, y1) - half - 1)), min(self.height, int(max(y0, y1) + half) + 2)):
            for x in range(max(0, int(min(x0, x1) - half - 1)), min(self.width, int(max(x0, x1) + half) + 2)):
                px, py = x + 0.5 - x0, y + 0.5 - y0
                t = 0.0 if length2 == 0 else max(0.0, min(1.0, (px * dx + py * dy) / length2))
                if math.hypot(px - t * dx, py - t * dy) <= half:
                    self.put(channel, x, y, value)

    def png(self):
        """The image as PNG bytes: greyscale, RGB or RGBA by the channel count."""
        colour = {1: 0, 3: 2, 4: 6}[self.channels]
        rows = bytearray()
        w = self.width
        for y in range(self.height):
            rows.append(0)
            start = y * w
            if self.channels == 1:
                rows += self.planes[0][start:start + w]
            else:
                pixels = bytearray(w * self.channels)
                for c, plane in enumerate(self.planes):
                    pixels[c::self.channels] = plane[start:start + w]
                rows += pixels
        header = struct.pack(">IIBBBBB", self.width, self.height, 8, colour, 0, 0, 0)
        return (b"\x89PNG\r\n\x1a\n" + _chunk(b"IHDR", header) + _chunk(b"IDAT", zlib.compress(bytes(rows), 9))
                + _chunk(b"IEND", b""))


def _chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)


def read_png(data):
    """(width, height, channels, planes) of a PNG this module wrote (filter 0 only): for tests."""
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    pos, idat, width = 8, b"", None
    while pos < len(data):
        length = struct.unpack(">I", data[pos:pos + 4])[0]
        kind, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, _, colour = struct.unpack(">IIBB", body[:10])
            channels = {0: 1, 2: 3, 6: 4}[colour]
        elif kind == b"IDAT":
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    planes = [bytearray(width * height) for _ in range(channels)]
    stride = width * channels + 1
    for y in range(height):
        row = raw[y * stride + 1:(y + 1) * stride]
        for c in range(channels):
            planes[c][y * width:(y + 1) * width] = row[c::channels]
    return width, height, channels, planes


# --- the house's damage-mask atlas (p1-h01-adobe-house, design decision 3) ----------------------------------------

ATLAS_PX = (2048, 1024)       # design decision 3: one 2048 x 1024 atlas per variant
FIELD_STEP_PX = 4             # the fields are evaluated every 4 px and interpolated between
PATCH_CELLS_M = {"facade": (2.0, 0.7, 0.25), "interior": (1.0, 0.45, 0.18)}  # the sheet: patches 0.5-2 m outside,
                                                                            # flakes 0.3-1 m inside (assumed octaves)
OCTAVE_WEIGHTS = (0.55, 0.3, 0.15)
BIAS = {"opening": (0.9, 0.45), "corner": (0.6, 0.5), "foot": (0.7, 0.35), "head": (0.5, 0.3)}  # (amplitude, reach
                                                                    # m): loss gathers round openings, corners, the
                                                                    # foot and the head (the sheet's recipe; assumed)
FEATHER = 0.12                # a patch edge's ramp, as a share of the field's spread (assumed: about 0.1 m)
BAND_WOBBLE_M = (0.015, 0.4)  # the base band's top wanders this much over this run (assumed: brushed by hand)
STREAK_M = (0.8, 0.05)        # dirt under a sill: its length down the wall, its streaks' width (assumed)
EAVES_DIRT_M = 0.5            # dirt streaks down from the wall head (assumed)
SOOT_M = (1.2, 0.3)           # a soot plume's height above its opening and its spread past the sides (the sheet:
                              # 0.5-1.5 m)
SOOT_PEAK, DIRT_PEAK = 0.85, 0.4  # a layer covers by its channel's value: 1.1's soot and dirt strengths are the peaks
                                  # (task 1.2's note)
BAND_WEAR = (0.35, 0.6)       # the band's paint worn to this much less cover in patches of about this size (assumed)


class PeriodicNoise:
    """Smooth value noise in [-1, 1], periodic in u over `period` metres (so a wall loop meets itself where its U
    restarts), with cells of about `cell` metres."""

    def __init__(self, rng, period, cell, v_extent):
        self.nu = max(1, round(period / cell))
        self.cu, self.cv = period / self.nu, cell
        self.nv = int(v_extent / cell) + 3
        self.grid = [[rng.uniform(-1.0, 1.0) for _ in range(self.nu)] for _ in range(self.nv)]

    def __call__(self, u, v):
        x, y = u / self.cu, max(0.0, v) / self.cv
        i, j = math.floor(x), min(int(y), self.nv - 2)
        fx, fy = x - i, min(1.0, y - j)
        sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        i0, i1 = i % self.nu, (i + 1) % self.nu
        r0, r1 = self.grid[j], self.grid[j + 1]
        a = r0[i0] + (r0[i1] - r0[i0]) * sx
        b = r1[i0] + (r1[i1] - r1[i0]) * sx
        return a + (b - a) * sy


def _smooth(e0, e1, x):
    if x <= e0:
        return 0.0
    if x >= e1:
        return 1.0
    t = (x - e0) / (e1 - e0)
    return t * t * (3 - 2 * t)


class _Region:
    """One wall loop of the atlas in its own (u, v): its face spans, openings and corners, local to its offset."""

    def __init__(self, r):
        self.r, self.kind = r, r["kind"]
        self.u_off, self.v_off, self.period = r["u_off"], r["v_off"], r["period"]
        self.spans = sorted((u0 - self.u_off, u1 - self.u_off, t0, t1) for u0, u1, t0, t1 in r["spans"])
        self.starts = [s[0] for s in self.spans]
        self.openings = [(u0 - self.u_off, u1 - self.u_off, v0, v1) for u0, u1, v0, v1 in r["openings"]]
        self.corners = [c - self.u_off for c in r["corners"]]
        self.height = max(max(t0, t1) for _, _, t0, t1 in self.spans)

    def top(self, u):
        """The face's top at u, or None outside every span."""
        k = bisect.bisect_right(self.starts, u) - 1
        if k < 0:
            return None
        u0, u1, t0, t1 = self.spans[k]
        if u > u1 + 1e-9:
            return None
        return t0 + (t1 - t0) * ((u - u0) / (u1 - u0) if u1 > u0 else 0.0)

    def opening_at(self, u, v):
        return any(a0 <= u <= a1 and b0 <= v <= b1 for a0, a1, b0, b1 in self.openings)

    def on_face(self, u, v):
        if v < 0 or v > self.height:
            return False
        t = self.top(u)
        return t is not None and v <= t and not self.opening_at(u, v)

    def d_opening(self, u, v):
        best = 1e9
        for a0, a1, b0, b1 in self.openings:
            best = min(best, math.hypot(max(a0 - u, 0.0, u - a1), max(b0 - v, 0.0, v - b1)))
        return best

    def d_corner(self, u):
        p = self.period
        return min(min(abs(u - c) % p, p - abs(u - c) % p) for c in self.corners) if self.corners else 1e9


def facade_atlas(regions, render_loss, whitewash_loss, base_band_height, soot_openings, rng_for, atlas_m):
    """The damage-mask atlas (ATLAS_PX, RGBA) over the wall loops `regions` (lib.walls.build's), the atlas spanning
    `atlas_m` metres of UV from (0, 0). Everywhere off a wall face reads bare adobe (R 1); on a face R is the render's
    depth: seeded patches of bare adobe over `render_loss` of each loop's face area, gathered round its openings,
    corners, foot and head, and patches of render without whitewash over `whitewash_loss` of what render is left,
    each feathered (R 0 whitewash, 0.5 render, 1 adobe, for the layered shader's thresholds at 0.25 and 0.75); A the
    base band's cover to `base_band_height` with a wandering top and its paint worn in patches (facade only); B dirt
    streaks under the sills and down from the wall head, peaking at DIRT_PEAK (facade only); G soot plumes over the
    first `soot_openings` openings in a seeded order, peaking at SOOT_PEAK. Each of A, G and B is the cover of its
    layer. Periodic in each loop's U, so a loop meets itself where its U restarts. Returns (Raster, lines)."""
    w_px, h_px = ATLAS_PX
    ppm = w_px / atlas_m[0]
    raster = Raster(w_px, h_px, 4)
    raster.fill(0, 255)
    lines = [f"mask: {w_px} x {h_px} px over {atlas_m[0]:g} x {atlas_m[1]:g} m of UV, {ppm:g} px/m"]
    soot_rng = rng_for("mask soot")
    for raw in regions:
        reg = _Region(raw)
        rng = rng_for(f"mask {raw['name']}")
        cells = PATCH_CELLS_M[reg.kind]
        f_noise = [PeriodicNoise(rng, reg.period, c, reg.height + 1) for c in cells]
        g_noise = [PeriodicNoise(rng, reg.period, c, reg.height + 1) for c in cells]
        band = PeriodicNoise(rng, reg.period, BAND_WOBBLE_M[1], 1.0)
        streak = PeriodicNoise(rng, reg.period, STREAK_M[1], 1.0)
        wear = PeriodicNoise(rng, reg.period, BAND_WEAR[1], 1.0)
        foot, head = raw["foot"], raw["head"]

        def field(noise, u, v, reg=reg, foot=foot, head=head):
            f = sum(wt * n(u, v) for wt, n in zip(OCTAVE_WEIGHTS, noise))
            for key, d in (("opening", reg.d_opening(u, v)), ("corner", reg.d_corner(u)),
                           ("foot", max(0.0, v - foot)), ("head", head - v if v <= head else 1e9)):
                amp, reach = BIAS[key]
                f += amp * math.exp(-d / reach)
            return f

        step = FIELD_STEP_PX / ppm
        nu = int(math.ceil(reg.period / step)) + 2
        nv = int(math.ceil(reg.height / step)) + 2
        fg = [[field(f_noise, i * step, j * step) for i in range(nu)] for j in range(nv)]
        gg = [[field(g_noise, i * step, j * step) for i in range(nu)] for j in range(nv)]
        face = [(j, i) for j in range(nv) for i in range(nu) if reg.on_face(i * step, j * step)]
        fs = sorted(fg[j][i] for j, i in face)
        w_r = FEATHER * (fs[int(0.9 * len(fs))] - fs[int(0.1 * len(fs))])
        # the ramp reaches R 0.75 at 0.347 of its half-width past its middle: put that at the quantile, so the bare
        # adobe (R from 0.75, the shader's adobe layer) covers `render_loss` of the face
        t_r = fs[min(len(fs) - 1, int((1 - render_loss) * len(fs)))] - 0.347 * w_r if render_loss > 0 else math.inf
        rest = sorted(gg[j][i] for j, i in face if fg[j][i] < t_r)
        t_w = rest[min(len(rest) - 1, int((1 - whitewash_loss) * len(rest)))] \
            if whitewash_loss > 0 and rest else math.inf
        w_w = FEATHER * (rest[int(0.9 * len(rest))] - rest[int(0.1 * len(rest))]) if rest else 1.0
        plumes = []
        if reg.kind == "facade" and soot_openings:
            order = list(reg.openings)
            soot_rng.shuffle(order)
            plumes = order[:soot_openings]
        i_lo, i_hi = max(0, int(reg.u_off * ppm)), min(w_px, int(math.ceil((reg.u_off + reg.period) * ppm)))
        j_top = max(0, h_px - int(math.ceil((reg.v_off + reg.height) * ppm)))
        j_bot = min(h_px, h_px - int(reg.v_off * ppm))
        adobe = loss = area = 0
        r_plane, g_plane, b_plane, a_plane = raster.planes
        for py in range(j_top, j_bot):
            v = (h_px - py - 0.5) / ppm - reg.v_off
            if v < 0 or v > reg.height:
                continue
            gy = v / step
            j0 = min(int(gy), nv - 2)
            fy = gy - j0
            row = py * w_px
            f_row0, f_row1, g_row0, g_row1 = fg[j0], fg[j0 + 1], gg[j0], gg[j0 + 1]
            for px in range(i_lo, i_hi):
                u = (px + 0.5) / ppm - reg.u_off
                t = reg.top(u)
                inside = t is not None and v <= t
                hole = reg.opening_at(u, v)
                if not (inside or hole):
                    continue
                gx = u / step
                i0 = min(int(gx), nu - 2)
                fx = gx - i0
                f = (f_row0[i0] * (1 - fx) + f_row0[i0 + 1] * fx) * (1 - fy) + \
                    (f_row1[i0] * (1 - fx) + f_row1[i0 + 1] * fx) * fy
                g = (g_row0[i0] * (1 - fx) + g_row0[i0 + 1] * fx) * (1 - fy) + \
                    (g_row1[i0] * (1 - fx) + g_row1[i0 + 1] * fx) * fy
                r = max(_smooth(t_r - w_r, t_r + w_r, f), 0.5 * _smooth(t_w - w_w, t_w + w_w, g))
                r_plane[row + px] = int(round(255 * r))
                if inside and not hole:
                    area += 1
                    adobe += r >= 0.75
                    loss += 0.25 <= r < 0.75
                if reg.kind != "facade":
                    continue
                if base_band_height > 0:
                    top_band = base_band_height + BAND_WOBBLE_M[0] * band(u, 0.0)
                    cover = 1 - BAND_WEAR[0] * max(0.0, wear(u, v))
                    a_plane[row + px] = int(round(255 * cover * (1 - _smooth(top_band - 1 / ppm, top_band + 1 / ppm, v))))
                b = 0.0
                s = 0.5 + 0.5 * streak(u, 0.0)
                for a0, a1, b0, b1 in reg.openings:
                    if b0 > 0.05 and a0 - 0.05 <= u <= a1 + 0.05 and b0 - STREAK_M[0] <= v < b0:
                        b = max(b, s * (1 - (b0 - v) / STREAK_M[0]) ** 1.5)
                if head - EAVES_DIRT_M <= v <= head:
                    b = max(b, 0.6 * s * (v - (head - EAVES_DIRT_M)) / EAVES_DIRT_M)
                b_plane[row + px] = int(round(255 * DIRT_PEAK * b))
                for a0, a1, b0, b1 in plumes:
                    if b1 <= v <= b1 + SOOT_M[0]:
                        rise = (v - b1) / SOOT_M[0]
                        half = (a1 - a0) / 2 + SOOT_M[1] * rise
                        c = (a0 + a1) / 2
                        if abs(u - c) <= half:
                            k = SOOT_PEAK * (1 - rise) * (1 - abs(u - c) / half) ** 0.5 * (0.75 + 0.25 * s)
                            g_plane[row + px] = max(g_plane[row + px], int(round(255 * k)))
        area = max(1, area)
        lines.append(f"mask {raw['name']}: bare adobe on {adobe / area:.3f} of the face (render_loss {render_loss:g}), "
                     f"render without whitewash on {loss / max(1, area - adobe):.3f} of the render left "
                     f"(whitewash_loss {whitewash_loss:g}, patch rims included)")
    return raster, lines
