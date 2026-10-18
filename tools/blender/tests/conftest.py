"""Fixtures: tools/blender on sys.path (lib and generators import from there, as they do inside Blender), the
repo paths, K01's real params, and the pinned Blender when it is installed. Only test_build_blender.py runs
Blender; everything else is plain Python. Tests take these as fixtures rather than importing conftest, because
tools/assets/tests has a conftest of its own and `python -m pytest tools` collects both."""
import json
import os
import sys
from pathlib import Path

import pytest

TOOLS_BLENDER = Path(__file__).resolve().parents[1]
REPO_ROOT = TOOLS_BLENDER.parents[1]
BLENDER_DEFAULT = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"

sys.path.insert(0, str(TOOLS_BLENDER))


@pytest.fixture
def repo_root():
    return REPO_ROOT


@pytest.fixture
def k01_params():
    return json.loads((REPO_ROOT / "assets-src" / "debris" / "K01_whole.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def s04_params():
    return json.loads((REPO_ROOT / "assets-src" / "surfaces" / "S04_patch.json").read_text(encoding="utf-8"))


@pytest.fixture
def material_ids():
    """The closed physics-material list of the repo, as build.py loads it."""
    from lib import meta
    return meta.load_physics_materials(REPO_ROOT)


@pytest.fixture
def k01_out():
    """The committed K01 output folder; skips when K01 has not been built."""
    folder = REPO_ROOT / "unity" / "Assets" / "_Project" / "Art" / "Models" / "debris" / "K01"
    if not (folder / "K01_whole.assetmeta.json").is_file():
        pytest.skip("K01 has not been built")
    return folder


@pytest.fixture
def blender():
    """The pinned Blender executable; skips when it is not installed here."""
    path = os.environ.get("BLENDER") or BLENDER_DEFAULT
    if not os.path.isfile(path):
        pytest.skip("the pinned Blender is not installed ($BLENDER or the default path)")
    return path
