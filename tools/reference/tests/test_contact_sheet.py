"""contact_sheet.py against the spec scenarios, plus the both-tools OPSEC scenario."""
import json
import re

import pytest
from PIL import Image, ImageChops, ImageStat

import contact_sheet
import extract_frames
from conftest import SECRET, run, snapshot

TILE_W = 160  # half the clip's width, so the tiles are really resampled


@pytest.fixture
def frames(root, clip):
    """30 stills of letter A (3 fps over the 10 s clip)."""
    assert run(extract_frames, clip, "--letter", "A", "--fps", 3, "--root", root) == 0
    return root / "reference" / "_frames" / "A"


def test_24_still_sheet(root, frames):
    assert run(contact_sheet, "--letter", "A", "--count", 24, "--cols", 6, "--tile-width", TILE_W, "--root", root) == 0
    sheets = list((frames / "sheets").iterdir())
    assert [p.name for p in sheets] == ["A_0001-0024.jpg"]

    gap, label_h = contact_sheet.GAP, contact_sheet.LABEL_H
    tile_h = TILE_W * 240 // 320
    with Image.open(sheets[0]) as im:
        sheet = im.convert("RGB")
    assert sheet.size == (6 * (TILE_W + gap) + gap, 4 * (tile_h + label_h + gap) + gap)  # a 6 x 4 grid

    stills = json.loads((frames / "index.json").read_text(encoding="utf-8"))["stills"]
    refs = []
    for s in stills:
        with Image.open(frames / s["file"]) as im:
            refs.append(im.convert("RGB").resize((TILE_W, tile_h), Image.Resampling.BOX))
    for i in range(24):
        row, col = divmod(i, 6)
        x, y = gap + col * (TILE_W + gap), gap + row * (tile_h + label_h + gap)
        tile = sheet.crop((x, y, x + TILE_W, y + tile_h))
        diffs = [sum(ImageStat.Stat(ImageChops.difference(tile, r)).mean) for r in refs]
        assert diffs.index(min(diffs)) == i, f"tile {i} is not still {i + 1}: tiles must run in time order"
        label = sheet.crop((x, y + tile_h, x + TILE_W, y + tile_h + label_h)).convert("L")
        assert label.getextrema()[1] > 200, f"tile {i} has no label"


@pytest.mark.parametrize("count", [30, 11])
def test_out_of_range_count_is_refused(root, frames, count, capsys):
    before = snapshot(root)
    assert run(contact_sheet, "--letter", "A", "--count", count, "--root", root) != 0
    assert snapshot(root) == before
    err = capsys.readouterr().err
    assert "12" in err and "24" in err


def test_not_enough_stills_is_refused(root, frames, capsys):
    assert run(contact_sheet, "--letter", "A", "--first", 20, "--count", 12, "--root", root) != 0
    assert "only 11 stills" in capsys.readouterr().err


@pytest.mark.parametrize("opt,path", [("--out", "outside"), ("--out", "reference/_frames/../../escape"),
                                      ("--out", "reference/_frames"), ("--frames", "reference")])
def test_paths_outside_frames_are_refused(root, frames, opt, path, capsys):
    before = snapshot(root)
    assert run(contact_sheet, "--letter", "A", opt, root / path, "--root", root) != 0
    assert snapshot(root) == before
    assert "reference/_frames" in capsys.readouterr().err


def test_source_name_absent_from_outputs(root, clip, capsys):
    """Extract as letter Z and make a sheet: SECRET is in no output name, index field or image metadata."""
    assert run(extract_frames, clip, "--letter", "Z", "--fps", 2, "--root", root) == 0
    assert run(contact_sheet, "--letter", "Z", "--count", 12, "--cols", 4, "--root", root) == 0
    outputs = [p for p in snapshot(root / "reference" / "_frames") if p.is_file()]
    assert len(outputs) == 20 + 1 + 1  # stills, index, sheet
    secret = re.compile(re.escape(SECRET), re.IGNORECASE)
    for p in outputs:
        assert not secret.search(p.relative_to(root).as_posix())
        assert not secret.search(p.read_bytes().decode("latin-1"))
        if p.suffix in (".png", ".jpg"):
            with Image.open(p) as im:
                assert not secret.search(repr(im.info) + repr(dict(im.getexif())))
    assert not secret.search(json.dumps(json.loads((root / "reference" / "_frames" / "Z" / "index.json").read_text())))
    out = capsys.readouterr()
    assert not secret.search(out.out + out.err)
