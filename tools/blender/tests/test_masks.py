"""lib.masks: the plain-Python raster and its deterministic PNG (p1-r01-vaz-sedan, design decision 5): drawing
raises values and never lowers them, the encoder round-trips greyscale and RGBA, and Pillow reads what it writes."""
import io

from PIL import Image

from lib import masks


def test_drawing_raises_by_maximum_and_clips():
    r = masks.Raster(16, 8, 1)
    r.rect(0, 2, 2, 6, 4, 100)
    r.rect(0, 4, 0, 30, 3, 50)  # overlapping, lower: no change where 100 stands; clipped at the image's edge
    assert r.planes[0][2 * 16 + 3] == 100 and r.planes[0][2 * 16 + 10] == 50 and r.planes[0][0] == 0
    r.disc(0, 8.0, 4.0, 2.0, 255)
    assert r.planes[0][4 * 16 + 8] == 255
    r.line(0, 0.5, 7.5, 15.5, 7.5, 1.0, 200)
    assert all(r.planes[0][7 * 16 + x] == 200 for x in range(16))


def test_png_round_trips_and_reads_in_pillow():
    for channels, mode in ((1, "L"), (4, "RGBA")):
        r = masks.Raster(9, 5, channels)
        for c in range(channels):
            r.rect(c, c, 0, c + 2, 5, 40 * (c + 1))
        data = r.png()
        assert data == r.png()  # deterministic bytes
        width, height, got_channels, planes = masks.read_png(data)
        assert (width, height, got_channels) == (9, 5, channels) and planes == r.planes
        image = Image.open(io.BytesIO(data))
        assert image.mode == mode and image.size == (9, 5)
        assert image.getpixel((0, 0)) == (40 if channels == 1 else (40, 0, 0, 0))
