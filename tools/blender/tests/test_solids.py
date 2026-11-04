"""lib.solids: closed solids with outward normals (the divergence theorem gives each one's volume back), flat faces
on their own vertices and smooth surfaces sharing theirs, frames of any handedness, and palette columns."""
import math

import pytest

from lib import solids
from lib.geometry import Mesh

P = ("s", (0.5, 0.5))


def volume(mesh):
    """The enclosed volume by the divergence theorem over the faces, each taken with its stated outward normal (the
    winding is lib.export's business): a closed solid with every normal outward gives its volume back, a flipped
    face or a hole does not. Each stated normal must lie along its face's own (Newell's), either way."""
    total = 0.0
    for indices, normal, _, _ in mesh.faces:
        pts = [mesh.vertices[i] for i in indices]
        n = [0.0, 0.0, 0.0]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n[0] += (a[1] - b[1]) * (a[2] + b[2])
            n[1] += (a[2] - b[2]) * (a[0] + b[0])
            n[2] += (a[0] - b[0]) * (a[1] + b[1])
        length = math.sqrt(sum(c * c for c in n))
        unit = [c / math.sqrt(sum(x * x for x in normal)) for c in normal]
        along = sum(n[a] * unit[a] for a in range(3))
        assert abs(along) > 0.99 * length, "a stated normal leaves its face's"
        centroid = [sum(p[a] for p in pts) / len(pts) for a in range(3)]
        total += 0.5 * math.copysign(1.0, along) * sum(centroid[a] * n[a] for a in range(3))
    return total / 3


def ngon_area(r, n):
    return n / 2 * r * r * math.sin(2 * math.pi / n)


def test_palette_uv_is_the_column_centre_of_the_padded_map():
    assert solids.palette_uv(0, 1) == (0.5, 0.5)
    assert solids.palette_uv(2, 3) == (2.5 / 4, 0.5)  # three columns pad to four
    assert solids.palette_uv(8, 9) == (8.5 / 16, 0.5)


def test_box_is_closed_flat_and_in_its_frame():
    m = Mesh()
    f = solids.Frame((1.0, 2.0, 3.0), (0, 0, 1), (0, 1, 0), (1, 0, 0))  # left-handed relative to the body's axes
    solids.box(m, f, (-0.1, 0.0, -0.2), (0.3, 0.05, 0.2), P)
    assert len(m.faces) == 6 and len(m.vertices) == 24  # every face has vertices of its own
    assert m.bounds() == (pytest.approx((0.8, 2.0, 2.9)), pytest.approx((1.2, 2.05, 3.3)))
    assert volume(m) == pytest.approx(0.4 * 0.05 * 0.4)


@pytest.mark.parametrize("ccw", [True, False])
def test_prism_is_closed_whichever_way_its_outline_runs(ccw):
    outline = [(0.0, 0.0), (0.2, 0.0), (0.2, 0.1), (0.1, 0.05), (0.0, 0.1)]  # concave
    m = Mesh()
    solids.prism(m, solids.AXES, outline if ccw else outline[::-1], -0.01, 0.02, P)
    assert volume(m) == pytest.approx((0.02 - 0.005) * 0.03)
    assert m.bounds() == (pytest.approx((0.0, -0.01, 0.0)), pytest.approx((0.2, 0.02, 0.1)))


def test_prism_smooth_sides_share_vertices_only_where_the_outline_turns_gently():
    rounded = solids.circle(0.0, 0.0, 0.05, 24)
    m = Mesh()
    solids.prism(m, solids.AXES, rounded, 0.0, 0.01, P, smooth_sides=True)
    assert len(m.vertices) == 24 * 2 + 24 * 2  # the caps' own, and one shared pair per outline corner
    m = Mesh()
    solids.prism(m, solids.AXES, [(0, 0), (1, 0), (1, 1), (0, 1)], 0.0, 0.01, P, smooth_sides=True)
    assert len(m.vertices) == 8 + 16  # square corners crease: four vertices per side face


def test_lathe_cylinder_volume_caps_and_sharing():
    m = Mesh()
    f = solids.Frame((0.0, 0.0, 0.0), (1, 0, 0), (0, 0, 1), (0, 1, 0))  # axis up
    solids.lathe(m, f, [(0.0, 0.0), (0.0, 0.1), (0.3, 0.1), (0.3, 0.0)], 16, [P] * 3)
    assert volume(m) == pytest.approx(ngon_area(0.1, 16) * 0.3)
    assert m.bounds() == (pytest.approx((-0.1, 0.0, -0.1)), pytest.approx((0.1, 0.3, 0.1)))
    assert sum(len(i) == 16 for i, _, _, _ in m.faces) == 2  # two disc caps, one face each
    assert len(m.vertices) == 16 * 4  # caps' own rings, and the wall's two rings


def test_lathe_cone_and_ring_and_hollow_end():
    m = Mesh()  # a cone to its apex
    solids.lathe(m, solids.AXES, [(0.0, 0.0), (0.0, 0.1), (0.2, 0.0)], 12, [P] * 2)
    assert volume(m) == pytest.approx(ngon_area(0.1, 12) * 0.2 / 3)
    m = Mesh()  # a closed ring: a washer
    solids.lathe(m, solids.AXES, [(0.0, 0.05), (0.0, 0.1), (0.01, 0.1), (0.01, 0.05)], 12, [P] * 4, closed=True)
    assert volume(m) == pytest.approx((ngon_area(0.1, 12) - ngon_area(0.05, 12)) * 0.01)
    m = Mesh()  # a tube with a bore 0.05 deep at its far end
    solids.lathe(m, solids.AXES, [(0.0, 0.0), (0.0, 0.1), (0.3, 0.1), (0.3, 0.08), (0.25, 0.08), (0.25, 0.0)], 12,
                 [P] * 5)
    assert volume(m) == pytest.approx(ngon_area(0.1, 12) * 0.3 - ngon_area(0.08, 12) * 0.05)


def test_lathe_refuses_a_bad_profile():
    with pytest.raises(ValueError):
        solids.lathe(Mesh(), solids.AXES, [(0.0, 0.0), (0.0, 0.1), (0.1, 0.1)], 8, [P])  # one paint for two segments
    with pytest.raises(ValueError):
        solids.lathe(Mesh(), solids.AXES, [(0.0, 0.0), (0.1, 0.0)], 8, [P])  # along the axis


def test_tube_volume_and_bounds():
    m = Mesh()
    solids.tube(m, [(0.0, 0.0, 0.0), (0.0, 0.0, 0.5), (0.0, 0.0, 1.0)], 0.01, 8, P)
    assert volume(m) == pytest.approx(ngon_area(0.01, 8) * 1.0)
    lo, hi = m.bounds()
    assert lo[2] == pytest.approx(0.0) and hi[2] == pytest.approx(1.0)


def test_closed_ribbon_wraps_its_loop_outward():
    loop = [(0.0, 0.0), (0.1, 0.0), (0.1, 0.05), (0.0, 0.05)]
    m = Mesh()
    solids.ribbon(m, solids.AXES, loop, 0.02, 0.002, ([(0.2, P), (0.8, P)], P, P), closed=True)
    expected = ((0.1 + 0.004) * (0.05 + 0.004) - 0.1 * 0.05) * 0.02
    assert volume(m) == pytest.approx(expected, rel=1e-9)
    assert m.bounds() == (pytest.approx((-0.002, -0.002, -0.01)), pytest.approx((0.102, 0.052, 0.01)))


def test_open_ribbon_is_closed_at_its_ends():
    m = Mesh()
    solids.ribbon(m, solids.AXES, [(0.0, 0.1), (0.0, 0.0), (0.1, 0.0), (0.1, 0.1)], 0.02, 0.002, (P, P, P))
    assert volume(m) > 0
    lo, hi = m.bounds()
    assert lo[0] == pytest.approx(-0.002) and hi[0] == pytest.approx(0.102) and lo[1] == pytest.approx(-0.002)


def test_frame_refuses_skew_axes():
    with pytest.raises(ValueError):
        solids.Frame((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 0, 1))
