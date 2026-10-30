"""p7c-wind 1.2: the wind harness's files parse as wind_stats.py reads its input.

`flightbench --wind-harness` (when the .NET SDK is installed) writes one Severe run and one T0c wind history into a
scratch folder, and both are read here by the reader logic of the first attempt's `tools/reference/wind_stats.py`
(godot-final, read only, D-027): `load` (the attitude columns, "" as NaN, dup, the no-picture guard), the first lines
of `analyse` (the frame rate from time_s, the usable frames by confidence, the --segment frame range) and `load_wind`
(time_s, wind_from_deg, and one prevailing_from_deg per seed). It is that logic in plain Python, without numpy.
When task 1.1's port lands (`tools/wind/wind_stats.py`), the round trip through the ported tool itself replaces it.
"""
import csv
import json
import math
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
CONF = 0.5            # wind_stats.py's usable-frame confidence
LOSS_GUARD = 2        # frames next to a picture loss are not used
CAMERA_HZ = 29.917
NUMERIC = ("frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw", "edge_px")


def load(path):
    """wind_stats.py's load(), without numpy: the columns it reads, the guard around picture losses."""
    rows = list(csv.DictReader(path.open(encoding="utf-8")))
    num = lambda r, k: float(r[k]) if r.get(k, "") != "" else math.nan
    cols = {k: [num(r, k) for r in rows] for k in NUMERIC}
    cols["dup"] = [r.get("dup", "0") == "1" for r in rows]
    lost = [r.get("flag", "ok") == "no_picture" for r in rows]
    near = [any(lost[max(0, i - LOSS_GUARD):i + LOSS_GUARD + 1]) for i in range(len(lost))]
    for k in ("conf_roll", "conf_pitch", "conf_yaw"):
        cols[k] = [0.0 if n else v for n, v in zip(near, cols[k])]
    cols["lost"] = lost
    return cols


def load_wind(path):
    """wind_stats.py's load_wind(): one seed's T0c history."""
    rows = list(csv.DictReader(Path(path).open(encoding="utf-8")))
    t, d, p = ([float(r[k]) for r in rows] for k in ("time_s", "wind_from_deg", "prevailing_from_deg"))
    if len(set(p)) != 1:
        raise ValueError(f"{path}: prevailing_from_deg must be the seed's one drawn direction on every row")
    return t, d, p


@pytest.fixture(scope="module")
def harness(tmp_path_factory):
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        pytest.skip("the .NET SDK is not installed")
    out = tmp_path_factory.mktemp("wind-harness")
    run = subprocess.run([dotnet, "run", "-c", "Release", "--project", str(ROOT / "tools" / "flightbench"), "--",
                          "--wind-harness", "severe", "--seeds", "1", "--pilots", "nominal", "--out", str(out),
                          "--history", "1"], cwd=ROOT, capture_output=True, text=True, timeout=600)
    assert run.returncode == 0, run.stdout + run.stderr
    folder = out / "severe"
    return folder, json.loads((folder / "harness.json").read_text(encoding="utf-8"))


def test_the_run_reads_as_an_attitude_log(harness):
    folder, manifest = harness
    assert manifest["wind"] == "field" and manifest["belt"] is True
    (run,) = manifest["runs"]
    c = load(folder / run["csv"])
    n = len(c["frame"])
    assert n == run["frames"] > 2 * 60, "describe() needs 2 x BLOCK frames"
    assert c["frame"] == [float(i) for i in range(1, n + 1)]
    fps = (n - 1) / (c["time_s"][-1] - c["time_s"][0])               # analyse()'s frame rate
    assert abs(fps - CAMERA_HZ) < 1e-4
    ok = [cr >= CONF and cp >= CONF and math.isfinite(r) and math.isfinite(p)
          for cr, cp, r, p in zip(c["conf_roll"], c["conf_pitch"], c["roll_deg"], c["pitch_deg"])]
    oky = [cy >= CONF and math.isfinite(y) for cy, y in zip(c["conf_yaw"], c["yaw_rate_dps"])]
    assert all(ok) and all(oky), "every frame usable on every axis"
    assert not any(c["dup"]) and not any(c["lost"])
    assert all(math.isnan(e) for e in c["edge_px"]), "a simulation has no horizon edge"
    assert -12 < sum(c["roll_deg"]) / n < -3, "leaning into the Severe field's wind from the left"
    assert abs(sum(c["pitch_deg"]) / n + 22.7) < 2, "clip P's pitch held"

    first, last = (int(v) for v in run["segment"].split("-"))    # main()'s --segment parse
    inside = [first <= f <= last for f in c["frame"]]
    assert 4 < sum(inside) / fps < 12, "10 H in 7 s at the cruise through the air, give or take the wind along the course"
    assert 0 < first and last < n


def test_the_history_reads_as_a_wind_log(harness):
    folder, manifest = harness
    (path,) = sorted((folder / "history").glob("*.csv"))
    t, d, p = load_wind(path)
    assert len(t) == 120 * 60 + 1 and t[1] - t[0] == 1, "120 min at 1 Hz"
    assert all(0 <= v < 360 for v in d + p)
