"""lib.frame: the asset-space (Unity) to Blender map, its inverse, and its agreement with what the FBX carries."""
import pytest

from lib import frame


def test_round_trip():
    p = (0.1, -0.2, 0.3)
    assert frame.blender_to_unity(frame.unity_to_blender(p)) == p
    assert frame.unity_to_blender(frame.blender_to_unity(p)) == p


def test_documented_maps():
    assert frame.blender_to_unity((1, 2, 3)) == (-1, 3, -2)
    assert frame.unity_to_blender((1, 2, 3)) == (-1, -3, 2)
    assert frame.unity_to_blender((0, 1, 0)) == (0, 0, 1)  # up stays up


def test_fbx_read_agrees_with_the_direct_map():
    # The exporter writes a Blender point as (x, z, -y) * 100 into a centimetre file (lib.frame); reading that
    # back the way Unity does must land on the direct Blender -> Unity map.
    b = (0.5, -1.25, 2.0)
    in_file = (b[0] * 100, b[2] * 100, -b[1] * 100)
    assert frame.fbx_to_unity(in_file, 1.0) == pytest.approx(frame.blender_to_unity(b))
    assert frame.fbx_to_unity((b[0], b[2], -b[1]), 100.0) == pytest.approx(frame.blender_to_unity(b))
