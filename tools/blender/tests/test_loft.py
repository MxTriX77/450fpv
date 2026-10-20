"""lib.loft: piecewise-linear tables and samples, and sheets: a single surface, a sheet of real thickness with its
edges at the borders and at a masked opening, and corners that merge into triangles."""
import pytest

from lib import loft
from lib.geometry import Mesh


def flat_grid(n=3, m=3, step=1.0):
    return [[(i * step, 0.0, j * step) for j in range(m)] for i in range(n)]


def uvs_of(grid):
    return [[(p[0], p[2]) for p in row] for row in grid]


def up(p):
    return (0.0, 1.0, 0.0)


def test_interp_and_samples():
    table = [(0.0, 1.0), (2.0, 3.0), (4.0, 3.0)]
    assert loft.interp(table, -1.0) == 1.0 and loft.interp(table, 1.0) == 2.0 and loft.interp(table, 9.0) == 3.0
    s = loft.samples(0.0, 1.0, 0.3, extra=(0.5, 2.0))
    assert s[0] == 0.0 and s[-1] == 1.0 and 0.5 in s and 2.0 not in s
    assert all(b - a <= 0.3 + 1e-12 for a, b in zip(s, s[1:]))


def test_single_surface_faces_agree_with_outward():
    mesh = Mesh()
    grid = flat_grid()
    assert loft.surface(mesh, grid, uvs_of(grid), "a", up) == 4
    assert mesh.triangles() == 8 and all(n == (0.0, 1.0, 0.0) for _, n, _, _ in mesh.faces)


def test_thick_sheet_has_inner_face_and_border_edges():
    mesh = Mesh()
    grid = flat_grid()
    loft.sheet(mesh, grid, uvs_of(grid), "a", up, thickness=0.001)
    normals = [n for _, n, _, _ in mesh.faces]
    assert normals.count((0.0, 1.0, 0.0)) == 4 and normals.count((0.0, -1.0, 0.0)) == 4
    assert len(mesh.faces) == 4 + 4 + 8  # outer, inner, eight border segments
    lo, hi = mesh.bounds()
    assert lo[1] == pytest.approx(-0.001) and hi[1] == 0.0


def test_mask_cuts_an_opening_with_its_edges():
    mesh = Mesh()
    grid = flat_grid(4, 4)
    mask = [[not (i == 1 and j == 1) for j in range(3)] for i in range(3)]
    loft.sheet(mesh, grid, uvs_of(grid), "a", up, thickness=0.001, mask=mask)
    assert len(mesh.faces) == 8 + 8 + 12 + 4  # 8 quads each side, 12 border segments, the hole's 4


def test_merged_corners_make_a_triangle_and_drop_a_line():
    mesh = Mesh()
    grid = [[(0.0, 0.0, 0.0), (0.0, 0.0, 1.0)], [(1.0, 0.0, 0.0), (1.0, 0.0, 0.0)], [(2.0, 0.0, 0.0), (2.0, 0.0, 0.0)]]
    assert loft.surface(mesh, grid, uvs_of(grid), "a", up) == 1
    assert mesh.triangles() == 1 and len(mesh.vertices) == 3
