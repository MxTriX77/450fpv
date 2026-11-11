"""The world-query core's height golden (w3-farmland-ground 1.1): validate.py's ground height at each of a package's
height probes, to the bit, for the sim's SampleGround to match exactly.

    python tools/map/probe_golden.py maps/farmland tools/map/golden/farmland-probes.txt

Each line holds a probe's x, z and ground height as the 16 hex digits of their IEEE doubles' bits, so the C# side
reads them without parsing decimals. CPython's floats are IEEE doubles with each operation rounded on its own, so the
reference (validate.py's Tile.height and ground_height) is a second runtime for free. Rewrite the file only when the
package changes; tests/test_probe_golden.py fails while it is stale.
"""
import hashlib
import json
import struct
import sys
from array import array
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate as v  # noqa: E402

HEADER = "# fpv450 height golden: validate.py's ground_height at each height probe; x z y as IEEE double bits (w3-farmland-ground 1.1)"


def bits(value):
    return f"{struct.unpack('<Q', struct.pack('<d', value))[0]:016x}"


def lines(root):
    """The golden's lines for the package at `root`: the header, the manifest's hash, then one line per probe."""
    root = Path(root)
    manifest_bytes = (root / "map.json").read_bytes()
    manifest = json.loads(manifest_bytes)
    tiles = []
    for row in manifest["tiles"]:
        samples = array("H")
        samples.frombytes((root / row["heights"]["file"]).read_bytes())
        if sys.byteorder == "big":
            samples.byteswap()
        tiles.append(v.Tile(row, samples))
    probes = json.loads((root / "height_probes.json").read_text(encoding="utf-8"))["probes"]
    digest = hashlib.sha256(manifest_bytes.replace(b"\r\n", b"\n")).hexdigest()
    out = [HEADER, f"# map.json sha256 {digest}"]
    for probe in probes:
        y = v.ground_height(tiles, probe["x"], probe["z"])
        out.append(f"{bits(float(probe['x']))} {bits(float(probe['z']))} {bits(y)}")
    return out


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if len(argv) != 2:
        print(__doc__.split("\n\n")[1])
        return 2
    text = "\n".join(lines(argv[0])) + "\n"
    out = Path(argv[1])
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {len(text.splitlines()) - 2} probes to {out.as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
