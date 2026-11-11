"""The world-query core's height golden (tools/map/probe_golden.py, w3-farmland-ground 1.1): the committed file is
validate.py's ground height at maps/farmland's probes, to the bit, so the sim's SampleGround is checked against the
reference itself."""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import probe_golden as g  # noqa: E402
import validate as v  # noqa: E402

FARMLAND = v.REPO / "maps" / "farmland"
GOLDEN = v.REPO / "tools" / "map" / "golden" / "farmland-probes.txt"


def test_the_committed_golden_is_the_reference_at_every_probe():
    want = g.lines(FARMLAND)
    have = GOLDEN.read_text(encoding="utf-8").splitlines()
    assert len(have) == 1002, "a header, the manifest's hash and 1,000 probes"
    assert have == want, "stale: python tools/map/probe_golden.py maps/farmland tools/map/golden/farmland-probes.txt"


def test_the_bits_round_trip():
    for value in (0.0, 1.0 / 1024, -4000.0, 26.727631, 1e-300):
        assert g.bits(value) == struct.pack(">d", value).hex()
