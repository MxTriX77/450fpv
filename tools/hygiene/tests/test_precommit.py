"""Tests for the pre-commit hygiene scan (change setup-repo-hygiene, SEC-06).

Integration tests commit for real in a throwaway repo whose core.hooksPath points at this
checkout's .githooks. Messenger names are never written here in plain text: every test
string is built from the hook's own encoded list, and test ids are indices, never names.
"""
import os
import subprocess
import sys
import time
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
HOOKS = ROOT / ".githooks"
PRECOMMIT = ROOT / "tools" / "hygiene" / "precommit.py"
sys.path.insert(0, str(PRECOMMIT.parent))
import precommit  # noqa: E402

ENV = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
LATIN = [t for t in precommit.MESSENGER_TERMS if t.isascii()]
CYRILLIC = [t for t in precommit.MESSENGER_TERMS if not t.isascii()]


def git(repo, *args):
    return subprocess.run(["git", *args], cwd=repo, env=ENV, capture_output=True, text=True,
                          encoding="utf-8", errors="replace")


@pytest.fixture
def repo(tmp_path):
    for args in (("init", "-q", "-b", "main"),
                 ("config", "user.name", "Hygiene Test"),
                 ("config", "user.email", "test@hygiene.invalid"),
                 ("config", "core.hooksPath", HOOKS.as_posix())):
        assert git(tmp_path, *args).returncode == 0
    # Mirror the real ignore rules, so "force-added" means what it says.
    (tmp_path / ".gitignore").write_text("reference/\n*.mp4\n*.mov\n", encoding="utf-8")
    return tmp_path


def commit(repo, files, force=False):
    """Write and stage files ({path: str or bytes}), then try to commit through the hook."""
    for rel, data in files.items():
        target = repo / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(data, str):
            data = data.encode("utf-8")
        target.write_bytes(data)
    added = git(repo, "add", *(["-f"] if force else []), "--", *files)
    assert added.returncode == 0, added.stderr
    return git(repo, "commit", "-q", "-m", "x")


def has_commit(repo):
    return git(repo, "rev-parse", "--verify", "-q", "HEAD").returncode == 0


def assert_blocked(result, repo, rule, hidden=()):
    assert result.returncode != 0
    assert rule in result.stderr
    assert not has_commit(repo)
    leaked = [i for i, text in enumerate(hidden) if text.lower() in result.stderr.lower()]
    assert not leaked, f"matched text #{leaked} leaked into the hook output"


# --- spec: Footage never commits ------------------------------------------------------------

def test_planted_mp4_is_blocked(repo):
    result = commit(repo, {"test.mp4": b"\0\0\0\x18ftypmp42"}, force=True)
    assert_blocked(result, repo, "footage-ext", hidden=["test"])


def test_footage_under_unity_assets_is_blocked(repo):
    result = commit(repo, {"unity/Assets/_Project/Art/flyby.mov": b"\0moov"}, force=True)
    assert_blocked(result, repo, "footage-ext", hidden=["flyby"])


@pytest.mark.parametrize("name", ["DJI_0001.txt", "VID_20240101_120000.md"])
def test_camera_style_name_is_blocked(repo, name):
    result = commit(repo, {f"docs/{name}": "plain text\n"})
    assert_blocked(result, repo, "footage-name", hidden=[name.split(".")[0]])


def test_anything_under_reference_is_blocked(repo):
    result = commit(repo, {"reference/letter-notes.txt": "a note\n"}, force=True)
    assert_blocked(result, repo, "reference-path", hidden=["letter-notes"])


# --- spec: Media only where assets live -----------------------------------------------------

@pytest.mark.parametrize("name", ["diagram.png", "frame.jpg"])
def test_image_in_docs_is_blocked(repo, name):
    result = commit(repo, {f"docs/{name}": b"\x89PNG\r\n\x1a\n\0\0"})
    assert_blocked(result, repo, "media-location", hidden=[name.split(".")[0]])


def test_texture_in_unity_assets_passes(repo):
    result = commit(repo, {"unity/Assets/_Project/Art/brick_albedo.png": b"\x89PNG\r\n\x1a\n\0\0"})
    assert result.returncode == 0, result.stderr
    assert has_commit(repo)


# --- spec: No OPSEC strings in text ---------------------------------------------------------

@pytest.mark.parametrize("script", ["latin", "cyrillic"])
def test_messenger_name_is_blocked(repo, script):
    term = LATIN[0].upper() if script == "latin" else CYRILLIC[0].title()
    text = f"# Notes\n\nShared through {term} after the flight.\n"
    result = commit(repo, {"docs/notes.md": text})
    assert_blocked(result, repo, "messenger", hidden=[term])
    assert "docs/notes.md:3" in result.stderr


def test_decimal_coordinates_in_docs_are_blocked(repo):
    # The spec's example pair, with neutral (open-ocean) values.
    result = commit(repo, {"docs/place.md": "Launch point -40.1234, -30.5678 by the road.\n"})
    assert_blocked(result, repo, "coordinates", hidden=["40.1234"])
    assert "docs/place.md:1" in result.stderr


def test_hook_own_sources_pass(repo):
    own = [".githooks/pre-commit", "tools/hygiene/precommit.py", "tools/hygiene/tests/test_precommit.py"]
    files = {rel: (ROOT / rel).read_bytes() for rel in own}
    result = commit(repo, files)
    assert result.returncode == 0, result.stderr
    assert has_commit(repo)


# --- spec: No dates in notes ----------------------------------------------------------------

def test_sortie_like_date_in_object_sheet_is_blocked(repo):
    # The spec's example line, with a neutral date.
    result = commit(repo, {"docs/object-sheets/fence.md": "# Fence\n\nseen on 2001-02-03\n"})
    assert_blocked(result, repo, "notes-date", hidden=["2001-02-03"])
    assert "docs/object-sheets/fence.md:3" in result.stderr


def test_decision_date_is_allowed(repo):
    result = commit(repo, {"docs/reference-notes/legs.md": "Legs stay on (the pilot, 2026-10-17).\n"})
    assert result.returncode == 0, result.stderr
    assert has_commit(repo)


# --- spec: Clean commits pass quickly -------------------------------------------------------

def test_ordinary_code_commit_passes(repo):
    files = {
        "unity/Packages/com.fpv450.sim/Runtime/Flight/Rotor.cs":
            "namespace Fpv450.Sim.Flight\n{\n    public struct Rotor { public float Radius; }\n}\n",
        "tools/map/validate.py": "def validate(path):\n    return path.exists()\n",
        "docs/notes.md": "# Notes\n\nThe video signal drops behind the barn; the wire sags 0.4 m.\n",
    }
    result = commit(repo, files)
    assert result.returncode == 0, result.stderr
    assert has_commit(repo)


def test_later_commits_are_scanned_too(repo):
    """Modified and renamed files on top of an existing HEAD go through the same rules."""
    assert commit(repo, {"docs/notes.md": "# Notes\n", "docs/plan.md": "# Plan\n"}).returncode == 0
    head = git(repo, "rev-parse", "HEAD").stdout

    modified = commit(repo, {"docs/notes.md": f"# Notes\n\nvia {LATIN[0].title()}\n"})
    assert modified.returncode != 0 and "messenger" in modified.stderr
    assert "docs/notes.md:3" in modified.stderr
    assert git(repo, "reset", "-q", "--", "docs/notes.md").returncode == 0

    assert git(repo, "mv", "docs/plan.md", "docs/DJI_0002.md").returncode == 0
    renamed = git(repo, "commit", "-q", "-m", "x")
    assert renamed.returncode != 0 and "footage-name" in renamed.stderr
    assert git(repo, "rev-parse", "HEAD").stdout == head


def clean_tree(count):
    """count clean files of a few kilobytes each: C#, Python and object-sheet Markdown."""
    files = {}
    for i in range(count):
        kind = i % 3
        if kind == 0:
            body = "".join(f"        public float Value{j} = {j}.5f; // rotor {j} cm\n" for j in range(100))
            files[f"unity/Assets/_Project/Scripts/Gen{i}.cs"] = f"public class Gen{i}\n{{\n{body}}}\n"
        elif kind == 1:
            body = "".join(f"    total += {j} * 0.25  # sample {j}\n" for j in range(100))
            files[f"tools/gen/gen_{i}.py"] = f"def gen_{i}():\n    total = 0\n{body}    return total\n"
        else:
            body = "".join(f"- Wall {j}: {j * 2} cm thick, seen from 50 m (D-028).\n" for j in range(100))
            files[f"docs/object-sheets/sheet_{i}.md"] = f"# Sheet {i}\n\n{body}"
    return files


def test_200_files_scan_in_under_2_s(repo):
    files = clean_tree(200)
    for rel, text in files.items():
        (repo / rel).parent.mkdir(parents=True, exist_ok=True)
        (repo / rel).write_text(text, encoding="utf-8", newline="\n")
    assert git(repo, "add", "--", "unity", "tools", "docs").returncode == 0
    staged = git(repo, "diff", "--cached", "--name-only").stdout.split()
    assert len(staged) == 200

    times = []
    for _ in range(3):
        start = time.perf_counter()
        scan = subprocess.run([sys.executable, str(PRECOMMIT)], cwd=repo, env=ENV, capture_output=True)
        times.append(time.perf_counter() - start)
        assert scan.returncode == 0, scan.stderr
    start = time.perf_counter()
    result = git(repo, "commit", "-q", "-m", "x")
    commit_time = time.perf_counter() - start
    assert result.returncode == 0, result.stderr
    print(f"\n200 staged files: scan {', '.join(f'{t:.3f}' for t in times)} s; "
          f"full commit through the hook {commit_time:.3f} s")
    assert max(times) < 2.0


# --- rules, case by case (no git) -----------------------------------------------------------

@pytest.mark.parametrize("ext", sorted(precommit.FOOTAGE_EXT))
def test_every_footage_extension_is_caught_in_any_case(ext):
    assert precommit.footage_ext(f"a/b{ext}")
    assert precommit.footage_ext(f"a/b{ext.upper()}")


@pytest.mark.parametrize("path", ["a.cs", "a.ts", "a.md", "a.mp4.md", "mp4"])
def test_other_extensions_are_not_footage(path):
    assert not precommit.footage_ext(path)


@pytest.mark.parametrize("name", [
    "DJI_0001.txt", "DJIG0007.md", "VID_20240101_120000.md", "IMG_1234.cs", "PXL_20240101_1.txt",
    "MVI_0042.txt", "DSC_0001.md", "DSCN0001.md", "DSCF0001.md", "DSC00001.md", "GOPR0001.md",
    "GX010001.md", "GH010001.md", "Screenshot_20240101-120000.md", "Screenshot 2024-01-01.md",
    "photo_2024-01-01_12-00-00.md", "video_2024-01-01_12-00-00.md", "IMG-20240101-WA0001.md",
    "VID-20240101-WA0001.md", "20240101_120000.md", "dji_0001.md",
])
def test_camera_and_export_names_are_caught(name):
    assert precommit.footage_name(f"some/dir/{name}")


@pytest.mark.parametrize("name", [
    "video_feed.cs", "image_util.py", "dji_notes.md", "screenshot_tool.py", "gx12.md",
    "tx12_profile.md", "README.md", "photo_scan.md", "2024_plan.md",
])
def test_ordinary_names_are_not_footage_names(name):
    assert not precommit.footage_name(f"some/dir/{name}")


@pytest.mark.parametrize("path, caught", [
    ("reference/a.txt", True), ("Reference/x/b.md", True), ("reference/_compare/a.md", True),
    ("docs/reference-notes/a.md", False), ("references.md", False), ("reference.md", False),
])
def test_reference_path(path, caught):
    assert precommit.reference_path(path) == caught


@pytest.mark.parametrize("path, caught", [
    ("docs/a.png", True), ("a.jpg", True), ("tools/x/b.WAV", True), ("docs/c.svg", True),
    ("maps/m/d.mp3", True), ("Unity/Assets/e.png", True),
    ("unity/Assets/_Project/Art/a.png", False), ("assets-src/sfx/b.wav", False),
    ("docs/a.md", False), ("unity/Assets/a.cs", False),
])
def test_media_location(path, caught):
    assert precommit.media_location(path) == caught


@pytest.mark.parametrize("index", range(len(precommit.MESSENGER_TERMS)))
def test_every_messenger_term_is_caught_in_any_case(index):
    term = precommit.MESSENGER_TERMS[index]
    variants = [term, term.upper(), term.title(), term.replace(" ", "  "), f"({term})", f"{term}, ok"]
    if not term.isascii():
        variants.append(term + "і")  # an inflected ending
    missed = [i for i, v in enumerate(variants) if not precommit.messenger(f"via {v} today")]
    assert not missed, f"term #{index}: variants {missed} not caught"


@pytest.mark.parametrize("index", range(len(precommit.MESSENGER_TERMS)))
def test_messenger_term_inside_another_word_passes(index):
    hit = precommit.messenger("x" + precommit.MESSENGER_TERMS[index])
    assert not hit, f"term #{index} caught inside a word"


@pytest.mark.parametrize("line", [
    "The video signal drops behind the barn.", "Signal strength falls with range.",
    "signal.mean() over the frame", "A wire strike at 3 m.", "Fiber slack on the spool.",
    "Line of sight is lost.", "rotation matrix", "a messenger bag", "сигнал слабне",
])
def test_project_vocabulary_is_not_a_messenger(line):
    assert not precommit.messenger(line)


@pytest.mark.parametrize("line", [
    "-40.1234, -30.5678", "40.1234 S 30.5678 W", "40.1234°S, 30.5678°W", "-40.12345;-30.56789",
    "120.1234, -40.1234", "(200.1234, -40.1234, -30.5678)", "40°07′24″S 30°34′04″W", "40° 07' 24\" S", "S 40°07′", "40°07.5′S",
    "25G AB 1234 5678", "25GAB12345678", "grid 25G AB 12345 67890 here",
])
def test_coordinate_like_strings_are_caught(line):
    assert precommit.coordinates(line)


@pytest.mark.parametrize("line", [
    "vec3(0.1031, 0.1030, 0.0973)", "colour (0.3247, 0.2142, 0.1035)", "a 45° bank", "0° uptilt",
    "30° 20′ with no hemisphere", "TX12", "G1 to G8", "D-028", "4K PAL 50 Hz", "1.2345 m, 2.3456 m",
    "vec2(12.9898, 78.233)", "200.1234, 300.1234", "25G AB 123", "1080p", "M3 bolt, 10 mm",
])
def test_ordinary_numbers_are_not_coordinates(line):
    assert not precommit.coordinates(line)


@pytest.mark.parametrize("line", [
    "seen on 2001-02-03", "2001-02-03T10:00", "on 14.03.2025.", "on 1.3.2025", "on 14.03.25",
])
def test_dates_are_caught(line):
    assert precommit.notes_date(line)


@pytest.mark.parametrize("line", [
    "(the pilot, 2026-10-17)", "(pilot, 2026-10-17)", "The pilot said so on 2026-10-17.",
    "D-028 (2026-10-17)", "see §4.1.2", "Betaflight 4.5.10", "v1.2.3", "2026-13-01", "1.25.30",
])
def test_non_dates_and_excused_dates_pass(line):
    assert not precommit.notes_date(line)


def test_rules_apply_only_in_their_scope():
    coords, date = "-40.1234, -30.5678", "seen on 2001-02-03"
    name = "via " + LATIN[0]
    assert precommit.scan_text("tools/a.py", coords) == []
    assert precommit.scan_text("docs/plan.md", date) == []
    assert precommit.scan_text("Docs/a.md", coords) == [("coordinates", 1)]
    assert precommit.scan_text("docs/reference-notes/a.md", "ok\n" + date) == [("notes-date", 2)]
    assert precommit.scan_text("tools/a.py", name) == [("messenger", 1)]


def test_utf16_and_bom_text_is_scanned_and_binary_is_skipped():
    text = precommit.decode_text(("via " + LATIN[0]).encode("utf-16"))
    assert text is not None and precommit.messenger(text)
    assert precommit.decode_text(b"\xef\xbb\xbfplain") == "plain"
    assert precommit.decode_text(b"\x89PNG\0\0\0") is None


@pytest.mark.parametrize("path, shown", [
    ("test.mp4", "*.mp4"), ("docs/x/DJI_0001.txt", "docs/x/*.txt"),
    ("reference/a/b.mp4", "reference/..."),
])
def test_paths_are_masked_in_messages(path, shown):
    assert precommit.masked(path) == shown


def test_hook_is_a_posix_shell_script_with_lf_endings():
    data = (HOOKS / "pre-commit").read_bytes()
    assert data.startswith(b"#!/bin/sh\n")
    assert b"\r" not in data
