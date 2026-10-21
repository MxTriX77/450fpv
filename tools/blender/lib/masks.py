"""Generator-written masks (p1-r01-vaz-sedan, design decision 5): an 8-bit raster in plain Python, a few drawing
primitives, and a deterministic PNG encoder, so a mask is a pure function of the params and the same bytes come out
of Blender's Python and of the tests' (no Pillow, no numpy).

A Raster is `channels` planes of width x height bytes, row 0 at the top of the image. Drawing takes pixel
coordinates (x right, y down) and combines by maximum, so features overlap without order mattering. `png()` writes
greyscale (one channel), RGB or RGBA, filter 0, zlib level 9: identical input gives identical bytes.
"""
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
