"""lib.fbxread and the bounds check: the committed K01 FBX read the way Unity reads it, against its metadata."""
import json

import pytest

from lib import fbxread, meta


@pytest.fixture
def k01_fbx(k01_out):
    fbx = k01_out / "K01_whole.fbx"
    if fbxread.is_lfs_pointer(fbx):
        pytest.skip(f"{fbx.name} is a git-lfs pointer here; run `git lfs pull` for the bytes")
    return fbx


@pytest.fixture
def k01_meta(k01_out):
    return json.loads((k01_out / "K01_whole.assetmeta.json").read_text(encoding="utf-8"))


def test_committed_fbx_matches_its_metadata_within_a_millimetre(k01_fbx, k01_meta):
    assert meta.check_against_fbx(k01_meta, str(k01_fbx)) == []
    bounds = fbxread.unity_bounds(str(k01_fbx))
    assert set(bounds) == {"K01_whole_LOD0", "K01_whole_LOD1"}
    lo, hi = bounds["K01_whole_LOD0"]
    assert [hi[a] - lo[a] for a in range(3)] == pytest.approx([0.250, 0.065, 0.120], abs=meta.BOUNDS_TOLERANCE_M)
    assert lo[1] == pytest.approx(0.0, abs=1e-6)  # the brick lies on y = 0 in Unity's frame


def test_bounds_check_reports_a_disagreement(k01_fbx, k01_meta):
    k01_meta["bounds"]["max"][1] = 0.088
    problems = meta.check_against_fbx(k01_meta, str(k01_fbx))
    assert len(problems) == 1 and "bounds max" in problems[0]
    k01_meta["lods"][0]["mesh"] = "K01_whole_LOD9"
    assert any("LOD9 is not a mesh" in p for p in meta.check_against_fbx(k01_meta, str(k01_fbx)))


def test_reader_refuses_a_non_fbx_file(tmp_path):
    bogus = tmp_path / "x.fbx"
    bogus.write_bytes(b"version https://git-lfs.github.com/spec/v1\noid sha256:00\nsize 1\n")
    with pytest.raises(ValueError, match="git-lfs pointer"):
        fbxread.parse(bogus)
