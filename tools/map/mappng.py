"""Minimal PNG reader and writer for map layers (stdlib only): 8-bit greyscale or RGBA, non-interlaced."""
import struct
import zlib
from collections import namedtuple

SIGNATURE = b"\x89PNG\r\n\x1a\n"
GREY, RGBA = 0, 6
CHANNELS = {GREY: 1, RGBA: 4}

Png = namedtuple("Png", "width height bit_depth color_type interlace raw ancillary")


class PngError(Exception):
    pass


def _chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def write_png(path, width, height, color_type, rows):
    """Writes `rows` (an iterable of `height` byte strings, each width × channels long) without filtering."""
    compressor = zlib.compressobj(9)
    parts = [compressor.compress(b"\x00" + row) for row in rows]
    parts.append(compressor.flush())
    with open(path, "wb") as f:
        f.write(SIGNATURE)
        f.write(_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)))
        f.write(_chunk(b"IDAT", b"".join(parts)))
        f.write(_chunk(b"IEND", b""))


def read_png(path):
    """Parses the chunks and inflates the image data. Raises PngError on a malformed file."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != SIGNATURE:
        raise PngError("not a PNG file")
    pos, header, idat, ancillary = 8, None, [], []
    while True:
        if pos + 12 > len(data):
            raise PngError("truncated before IEND")
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        crc = data[pos + 8 + length:pos + 12 + length]
        if len(body) != length or len(crc) != 4:
            raise PngError("truncated chunk")
        if struct.unpack(">I", crc)[0] != zlib.crc32(kind + body):
            raise PngError(f"bad CRC in {kind.decode('latin-1')} chunk")
        pos += 12 + length
        if kind == b"IHDR":
            header = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat.append(body)
        elif kind == b"IEND":
            break
        else:
            ancillary.append((kind.decode("latin-1"), body))
    if header is None:
        raise PngError("no IHDR chunk")
    width, height, bit_depth, color_type, _, _, interlace = header
    try:
        raw = zlib.decompress(b"".join(idat))
    except zlib.error as e:
        raise PngError(f"corrupt image data: {e}") from None
    return Png(width, height, bit_depth, color_type, interlace, raw, ancillary)


def scanlines(png):
    """Yields each row as unfiltered bytes. Only for 8-bit, non-interlaced greyscale or RGBA."""
    bpp = CHANNELS[png.color_type]
    stride = png.width * bpp
    if len(png.raw) != png.height * (stride + 1):
        raise PngError(f"image data is {len(png.raw)} bytes, expected {png.height * (stride + 1)}")
    prev = bytearray(stride)
    for y in range(png.height):
        start = y * (stride + 1)
        kind = png.raw[start]
        line = bytearray(png.raw[start + 1:start + 1 + stride])
        if kind == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif kind == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif kind == 3:
            for i in range(stride):
                left = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif kind == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        elif kind != 0:
            raise PngError(f"row {y}: unknown filter type {kind}")
        yield bytes(line)
        prev = line
