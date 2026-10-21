"""lib.export.add_lod (p1-v01-locust QA 2.1, S7): a mesh whose vertices carry normals and that also has per-corner
normals or smooth slots is refused before Blender is touched, since the vertices' block would replace the corners'
normals and reset every face's smoothing; per-corner normals with smooth slots (V13's far cards beside its smooth
stems) and vertex normals alone (the poles and wires) go on. Plain Python: a stand-in bpy, empty, so a mesh
that passes the check stops at Blender's first call."""
import importlib
import sys
import types

import pytest

from lib.geometry import Mesh


@pytest.fixture
def export(monkeypatch):
    monkeypatch.setitem(sys.modules, "bpy", types.ModuleType("bpy"))
    monkeypatch.delitem(sys.modules, "lib.export", raising=False)
    yield importlib.import_module("lib.export")
    sys.modules.pop("lib.export", None)


def quad(corner_normals=False, vertex_normals=False):
    mesh = Mesh()
    up = (0.0, 1.0, 0.0) if vertex_normals else None
    indices = [mesh.vertex(p, normal=up) for p in ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1))]
    mesh.face(indices, (0.0, 1.0, 0.0), "bark", normals=[(0.0, 1.0, 0.0)] * 4 if corner_normals else None)
    return mesh


def test_vertex_normals_with_corner_normals_are_refused(export):
    with pytest.raises(ValueError, match="per-vertex normals or per-corner normals, not both"):
        export.add_lod("mixed", quad(corner_normals=True, vertex_normals=True))


def test_vertex_normals_with_smooth_slots_are_refused(export):
    mesh = quad(vertex_normals=True)
    mesh.smooth = {"bark"}
    with pytest.raises(ValueError, match=r"per-vertex normals or smooth slots \['bark'\], not both"):
        export.add_lod("mixed", mesh)


@pytest.mark.parametrize("corner_normals, vertex_normals, smooth", [(True, False, True), (False, True, False)])
def test_either_kind_alone_goes_on_to_blender(export, corner_normals, vertex_normals, smooth):
    mesh = quad(corner_normals, vertex_normals)
    mesh.smooth = {"bark"} if smooth else set()
    with pytest.raises(AttributeError, match="'bpy' has no attribute 'data'"):  # past the check, at Blender's first call
        export.add_lod("alone", mesh)
