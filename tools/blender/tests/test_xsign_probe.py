"""The X-sign probe fixture (design decision 5, physics review R1): the probe mesh is the asymmetric box, the
committed FBX read as Unity reads it spans (0, 0, 0) to (0.3, 0.1, 0.2), and make_xsign_probe.py writes that same
file byte for byte when the pinned Blender is installed, so the fixture never goes stale against lib.export."""
import subprocess

import pytest

import make_xsign_probe
from lib import fbxread


@pytest.fixture
def fixture_fbx(repo_root):
    fbx = repo_root / make_xsign_probe.FIXTURE
    if not fbx.is_file():
        pytest.skip("the probe fixture has not been built: run make_xsign_probe.py in Blender")
    if fbxread.is_lfs_pointer(fbx):
        pytest.skip(f"{fbx.name} is a git-lfs pointer here; run `git lfs pull` for the bytes")
    return fbx


def test_probe_mesh_is_the_asymmetric_box():
    mesh = make_xsign_probe.probe_mesh()
    assert mesh.bounds() == ((0.0, 0.0, 0.0), (0.3, 0.1, 0.2))
    assert (len(mesh.vertices), len(mesh.faces), mesh.triangles()) == (8, 6, 12)


def test_committed_fixture_reads_as_the_positive_box(fixture_fbx):
    lo, hi = fbxread.unity_bounds(str(fixture_fbx))[make_xsign_probe.NAME]
    assert lo == pytest.approx((0.0, 0.0, 0.0), abs=1e-6)
    assert hi == pytest.approx((0.3, 0.1, 0.2), abs=1e-6)


def test_committed_fixture_is_what_the_script_writes(blender, repo_root, fixture_fbx, tmp_path):
    script = repo_root / "tools" / "blender" / "tests" / "make_xsign_probe.py"
    run = subprocess.run([blender, "--background", "--factory-startup", "--python", str(script), "--", "--root",
                          str(tmp_path)], capture_output=True, text=True, timeout=300)
    assert run.returncode == 0 and "make_xsign_probe.py: wrote" in run.stdout, run.stdout + run.stderr
    assert (tmp_path / make_xsign_probe.FIXTURE).read_bytes() == fixture_fbx.read_bytes(), \
        "the committed fixture is stale: rerun make_xsign_probe.py"
