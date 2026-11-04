"""The map compiler's snag cap (p7d-tether 1.3; the world-query spec's "Records near a point"; the fortifications reading
§7): at most 2048 snag record segments meet any 2 m cube, found exactly; a synthetic map over it is refused naming the
cube. The test range's own count is printed by the compiler and checked by test_test_range.py's byte test."""
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import make_test_range as gen  # noqa: E402


class Assets:
    """One synthetic variant: its snag records only."""

    def __init__(self, records):
        self.records = records

    def klass(self, asset):
        return "fortifications"

    def meta(self, asset, variant):
        return {"snag": self.records}


def lattice(n, origin, step=0.1, length=0.05):
    """n one-segment edge records of 8 mm reach at a 13-per-side lattice from `origin`, each `length` long along x."""
    records = []
    for k in range(n):
        x, y, z = (origin[0] + step * (k % 13), origin[1] + step * (k // 13 % 13), origin[2] + step * (k // 169))
        records.append({"kind": "edge", "points": [[x, y, z], [x + length, y, z]], "radius_m": 0.008,
                        "material": "razor_wire"})
    return records


def placed(records, x=0.0, z=0.0, yaw=0.0):
    m = gen.Map(Assets(records))
    m.place("test", "F01", "F01_stack", x, z, yaw)
    return m


def test_2049_segments_in_one_2m_cube_are_refused_naming_the_cube():
    with pytest.raises(SystemExit) as refused:
        gen.snag_cap(placed(lattice(2049, (0.3, 0.3, 0.3))))
    text = str(refused.value)
    print(text)
    assert text.startswith("2049 snag record segments meet the 2 m cube from (")
    assert "over the limit 2048 (F01_stack #0 2049)" in text


def test_2048_pass_and_the_report_names_the_bound():
    line = gen.snag_cap(placed(lattice(2048, (0.3, 0.3, 0.3))))
    print(line)
    assert line == ("snag records: 2048 segments; any 2 m cube meets at most 2048 (the 4 m block from (0, 0, 0)), "
                    "limit 2048")


def test_a_cluster_across_the_blocks_seams_is_found():
    """2049 segments round a point where eight 2 m cells meet: no cell's cube holds them, the shifted cube does."""
    with pytest.raises(SystemExit) as refused:
        gen.snag_cap(placed(lattice(2049, (1.4, 1.4, 1.4))))
    assert str(refused.value).startswith("2049 snag record segments meet the 2 m cube from (")


def test_2049_spread_along_3m_pass_when_searched_exactly():
    """A block holds all 2049, so it is searched; no 2 m cube meets more than about two thirds of them."""
    records = [{"kind": "edge", "points": [[0.3 + 3.0 * k / 2049, 1.0, 1.0], [0.3 + 3.0 * k / 2049, 1.1, 1.0]],
                "radius_m": 0.008, "material": "razor_wire"} for k in range(2049)]
    line = gen.snag_cap(placed(records))
    print(line)
    assert line.startswith("snag records: 2049 segments; any 2 m cube meets at most 2048 (every 4 m block over it "
                           "searched exactly: at most 1377 there), limit 2048")


def test_the_pose_places_the_records():
    """The same cluster turned and moved is still refused, its cube moved with it."""
    with pytest.raises(SystemExit) as refused:
        gen.snag_cap(placed(lattice(2049, (0.3, 0.3, 0.3)), x=40.0, z=-20.0, yaw=37.0))
    corner = str(refused.value).split("from (")[1].split(")")[0].split(", ")
    assert 37.0 < float(corner[0]) < 41.0 and -22.0 < float(corner[2]) < -18.0
