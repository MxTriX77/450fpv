"""letter_index.py: existing letters never change, new files take the next free letters (FEED-12, D-037)."""
import pytest

import letter_index
from conftest import SECRET, run, snapshot
from ref_paths import RefError

MAP = """# Letter map: LOCAL ONLY (git-ignored). Never copy this file or its names anywhere.

A hand-written note that must survive.

| Letter | Kind | File (under `reference/`) |
|---|---|---|
| A | clip | `{secret}.mp4` |
| B | photo | `sub/{secret}-b.jpg` (a free note) |
| D | photo | `sub/gone.png` |

A trailing note.
"""


@pytest.fixture
def mapped(root):
    """reference/ with a map citing A, B and D (D's file is gone and C was skipped)."""
    (root / "reference" / "sub").mkdir()
    (root / "reference" / "sub" / f"{SECRET}-b.jpg").write_bytes(b"x")
    (root / "reference" / "_frames" / "letters.md").write_text(MAP.format(secret=SECRET), encoding="utf-8")
    return root


def letters_md(root):
    return (root / "reference" / "_frames" / "letters.md").read_text(encoding="utf-8")


def add(root, rel):
    p = root / "reference" / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_bytes(b"x")


def test_rows_with_notes_are_read(mapped):
    assert [r[:2] for r in letter_index.read(mapped)] == [("A", "clip"), ("B", "photo"), ("D", "photo")]


def test_dry_run_writes_nothing(mapped, capsys):
    add(mapped, f"new/{SECRET}-x.png")
    before = (snapshot(mapped), letters_md(mapped))
    assert run(letter_index, "--root", mapped) == 0
    assert (snapshot(mapped), letters_md(mapped)) == before
    out = capsys.readouterr().out
    assert "E photo (new, not written" in out and "D photo (file missing" in out
    assert SECRET.lower() not in out.lower()


def test_new_files_take_the_next_letters_and_nothing_else_changes(mapped, capsys):
    add(mapped, f"z/{SECRET}-photo.webp")
    add(mapped, f"y/{SECRET}-clip.mov")
    add(mapped, "_compare/ignored.png")
    add(mapped, "notes.md")
    old = letters_md(mapped)
    assert run(letter_index, "--write", "--root", mapped) == 0
    new = letters_md(mapped)
    rows = letter_index.read(mapped)
    # the old rows keep their letters; the clip goes before the photo; C stays skipped
    assert rows[:3] == letter_index.ROW.findall(old)
    assert [(r[0], r[1], r[2]) for r in rows[3:]] == [
        ("E", "clip", f"y/{SECRET}-clip.mov"), ("F", "photo", f"z/{SECRET}-photo.webp")]
    # inserted after the last row: everything before and after it is untouched
    cut = old.index("\nA trailing note.")
    assert new.startswith(old[:cut]) and new.endswith(old[cut:])
    assert SECRET.lower() not in capsys.readouterr().out.lower()


def test_letters_survive_later_additions(mapped):
    add(mapped, "one.png")
    assert run(letter_index, "--write", "--root", mapped) == 0
    first = letter_index.read(mapped)
    add(mapped, "another.mkv")
    assert run(letter_index, "--write", "--root", mapped) == 0
    second = letter_index.read(mapped)
    assert second[:len(first)] == first and second[-1][:2] == ("F", "clip")


def test_first_run_starts_the_map(root):
    assert run(letter_index, "--write", "--root", root) == 0
    assert [r[:2] for r in letter_index.read(root)] == [("A", "clip")]


def test_running_past_z_is_refused(mapped):
    (mapped / "reference" / "_frames" / "letters.md").write_text(
        MAP.format(secret=SECRET).replace("| D | photo |", "| Y | photo |"), encoding="utf-8")
    add(mapped, "p1.png")
    add(mapped, "p2.png")
    before = letters_md(mapped)
    assert run(letter_index, "--write", "--root", mapped) != 0
    assert letters_md(mapped) == before


def test_clip_resolves_only_existing_clip_letters(mapped):
    assert letter_index.clip(mapped, "A").name == f"{SECRET}.mp4"
    for letter, message in [("B", "photo"), ("D", "photo"), ("Q", "not in")]:
        with pytest.raises(RefError, match=message):
            letter_index.clip(mapped, letter)
    (mapped / "reference" / f"{SECRET}.mp4").unlink()
    with pytest.raises(RefError, match="missing"):
        letter_index.clip(mapped, "A")
