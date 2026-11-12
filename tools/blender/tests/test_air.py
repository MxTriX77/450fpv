"""lib.air (w4b-farmland-ruins task 1.2): the air the outside reaches round a mesh, faces as walls."""
from lib import air


def quad(a, b, c, d):
    return [((a, b, c), None, None), ((a, c, d), None, None)]


def box_faces(lo, hi, top=True):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    out = quad((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1))
    out += quad((x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1))
    out += quad((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1))
    out += quad((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0))
    out += quad((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))
    if top:
        out += quad((x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1))
    return out


def test_a_closed_box_keeps_its_inside_from_the_outside():
    a = air.Air(box_faces((0, 0.5, 0), (1, 1.5, 1)), 0.02)
    assert not a.reaches((0.5, 1.0, 0.5))
    assert a.reaches((-0.1, 1.0, 0.5)) and a.reaches((0.5, 1.6, 0.5)) and a.reaches((0.5, 0.1, 0.5))


def test_an_open_top_lets_the_outside_in():
    a = air.Air(box_faces((0, 0.5, 0), (1, 1.5, 1), top=False), 0.02)
    assert a.reaches((0.5, 1.0, 0.5)) and a.reaches((0.5, 0.6, 0.5))


def test_the_ground_closes_a_box_standing_on_it():
    a = air.Air(box_faces((0, -0.1, 0), (1, 1, 1))[2:], 0.02)  # no bottom face: it stands in the ground
    assert not a.reaches((0.5, 0.5, 0.5))


def test_a_crack_narrower_than_a_cell_is_sealed():
    faces = box_faces((0, 0.5, 0), (1, 1.5, 1), top=False)
    faces += quad((0, 1.5, 0), (0.495, 1.5, 0), (0.495, 1.5, 1), (0, 1.5, 1))
    faces += quad((0.505, 1.5, 0), (1, 1.5, 0), (1, 1.5, 1), (0.505, 1.5, 1))  # a 1 cm slit along the lid
    assert not air.Air(faces, 0.02).reaches((0.5, 1.0, 0.5))


class _Family:
    """A lib.slabs family facing +z: pixel (i, j) at x = (i + 0.5) r, y = (j + 0.5) r."""
    n, u, v, key = (0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)


def test_a_thin_closed_cavity_is_closed_and_a_big_closed_void_is_open():
    from lib import slabs
    fam, r = _Family(), 0.01
    row = slabs.span(40, 60)  # x 0.40..0.61
    thin = air.Air(box_faces((0, 0.5, 0), (1, 1.5, 0.3)), 0.02)
    assert thin.open_bits(fam, 100, row, 0.0, 0.3, r) == 0, "a cavity its two faces close: solid"
    big = air.Air(box_faces((0, 0.5, 0), (1, 1.5, 1)), 0.02)
    assert big.open_bits(fam, 100, row, 0.0, 0.3, r) == row, "a pair inside a bigger closed hollow: two sheets"
    assert big.open_bits(fam, -10, row, 0.0, 0.3, r) == row, "below the ground, which covers it: two sheets"
