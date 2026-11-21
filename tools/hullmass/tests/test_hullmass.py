"""The hull's added-mass tool (tools/hullmass, p7c-wind design 12) against the closed forms: a sphere's ½V and Lamb's prolate
spheroids (L/D 2 and 1.5, and 2 tilted 15° for the off-diagonal term), each within 1.5 % of the mean diagonal after the
tool's extrapolation over two cells, and A_across − A_along within 3 % of Lamb's k2 − k1. HULLMASS_HULL=1 also rebuilds
the hull and checks drone.json holds the tool's numbers (about 4 minutes)."""
import json
import os
import re
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
DOTNET = shutil.which("dotnet")


def run(*args):
    out = subprocess.run([DOTNET, "run", "-c", "Release", "--project", str(ROOT / "tools" / "hullmass"), "--", *args],
                         cwd=ROOT, capture_output=True, text=True, encoding="utf-8", timeout=1800)
    return out.returncode, out.stdout + out.stderr


@pytest.mark.skipif(DOTNET is None, reason="needs the .NET SDK")
def test_the_panel_method_meets_the_sphere_and_lambs_spheroids():
    code, text = run("--check")
    print(text)
    assert code == 0 and "check: PASS" in text, text


@pytest.mark.skipif(DOTNET is None or os.environ.get("HULLMASS_HULL") != "1", reason="HULLMASS_HULL=1 rebuilds the hull")
def test_drone_json_holds_the_tools_hull():
    code, text = run()
    assert code == 0, text
    size = json.loads(re.search(r"JSON size (\[.*\])", text).group(1))
    added = json.loads(re.search(r"JSON added (\[.*\])", text).group(1))
    hull = json.loads((ROOT / "assets-src" / "drone" / "drone.json").read_text(encoding="utf-8"))["aero"]["hull"]
    assert hull["frontal_area_m2"]["value"] == pytest.approx(size[0], rel=1e-4)
    assert hull["size_m"]["value"] == pytest.approx(size[1:], rel=1e-4)
    assert hull["added_volume_m3"]["value"] == pytest.approx(added, rel=1e-4, abs=1e-9)
