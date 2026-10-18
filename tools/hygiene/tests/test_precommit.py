"""Tests for the hygiene scan (change setup-repo-hygiene, SEC-01, SEC-02, SEC-06).

Integration tests commit for real in a throwaway repo whose core.hooksPath points at this
checkout's .githooks. No planted value is written here in plain text: messenger names come
from the hook's encoded list; footage names, dates, regional coordinates and reference stems
are built at run time; test ids are indices, never values. test_own_sources_hold_no_planted_value
holds this file and the hook to that.
"""
import hashlib
import os
import re
import shutil
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
LFS_HOOKS = ("pre-push", "post-checkout", "post-commit", "post-merge")
OWN = [f".githooks/{h}" for h in ("pre-commit", "commit-msg", *LFS_HOOKS)] + [
    "tools/hygiene/precommit.py", "tools/hygiene/tests/test_precommit.py"]


def j(*parts):
    """Join a planted value at run time, so that this file never holds it."""
    return "".join(parts)


def iso(y, m, d):
    return f"{y:04d}-{m:02d}-{d:02d}"


Y, M, D = 2001, 2, 3
DATE = iso(Y, M, D)
CAMERA = j("DJI", "_", "0001")
PHONE = j("VID", "_", str(Y), f"{M:02d}{D:02d}", "_", "120000")
EXPORT = j("IMG", "-", str(Y), f"{M:02d}{D:02d}", "-WA", "0001")
CLIP = j("flyby", ".", "mov")
# A point inside the regional box (latitude 40-60, longitude 15-50), computed.
NORTH, EAST = 40 + 5.3702, 15 + 10.5678
A, B = f"{NORTH:.4f}", f"{EAST:.4f}"
KEY = j("la", "t")
STEM = j("qz", "-hygiene-", "probe", "-", "07")  # the fake reference/'s distinctive stem
SCISSORS = j("# ", "-" * 24, " >8 ", "-" * 24)


def git(repo, *args):
    return subprocess.run(["git", *args], cwd=repo, env=ENV, capture_output=True, text=True,
                          encoding="utf-8", errors="replace")


def init(path, hooks=HOOKS.as_posix()):
    path.mkdir(exist_ok=True)
    for args in (("init", "-q", "-b", "main"),
                 ("config", "user.name", "Hygiene Test"),
                 ("config", "user.email", "test@hygiene.invalid"),
                 *([("config", "core.hooksPath", hooks)] if hooks else [])):
        assert git(path, *args).returncode == 0
    # Mirror the real ignore rules, so "force-added" means what it says.
    (path / ".gitignore").write_text("reference/\n*.mp4\n*.mov\n", encoding="utf-8")
    return path


@pytest.fixture
def repo(tmp_path):
    return init(tmp_path)


def write(repo, files):
    for rel, data in files.items():
        target = repo / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data.encode("utf-8") if isinstance(data, str) else data)


def commit(repo, files, force=False, message="test: x"):
    """Write and stage files ({path: str or bytes}), then try to commit through the hooks."""
    write(repo, files)
    added = git(repo, "add", *(["-f"] if force else []), "--", *files)
    assert added.returncode == 0, added.stderr
    return git(repo, "commit", "-q", "-m", message)


def plant_reference(repo, names=(STEM + ".mp4",)):
    """A git-ignored local reference/ folder, like the one that holds the pilot's footage."""
    write(repo, {f"reference/{name}": b"\0\0\0\x18ftypmp42" for name in names})


def has_commit(repo):
    return git(repo, "rev-parse", "--verify", "-q", "HEAD").returncode == 0


def assert_blocked(result, repo, rule, hidden=()):
    assert result.returncode != 0
    assert rule in result.stderr
    assert not has_commit(repo)
    leaked = [i for i, text in enumerate(hidden) if text.lower() in result.stderr.lower()]
    assert not leaked, f"matched text #{leaked} leaked into the hook output"


def assert_passes(result, repo):
    assert result.returncode == 0, result.stderr
    assert has_commit(repo)


# --- spec: Footage never commits ------------------------------------------------------------

def test_planted_mp4_is_blocked(repo):
    result = commit(repo, {j("test", ".mp4"): b"\0\0\0\x18ftypmp42"}, force=True)
    assert_blocked(result, repo, "footage-ext", hidden=["test"])


def test_footage_under_unity_assets_is_blocked(repo):
    result = commit(repo, {"unity/Assets/_Project/Art/" + CLIP: b"\0moov"}, force=True)
    assert_blocked(result, repo, "footage-ext", hidden=["flyby"])


@pytest.mark.parametrize("name", [CAMERA + ".txt", PHONE + ".md"], ids=["camera", "phone"])
def test_camera_style_name_is_blocked(repo, name):
    result = commit(repo, {f"docs/{name}": "plain text\n"})
    assert_blocked(result, repo, "footage-name", hidden=[name.split(".")[0]])


def test_anything_under_reference_is_blocked(repo):
    result = commit(repo, {"reference/letter-notes.txt": "a note\n"}, force=True)
    assert_blocked(result, repo, "reference-path", hidden=["letter-notes"])


# --- spec: No footage names inside text -----------------------------------------------------

@pytest.mark.parametrize("name", [CAMERA, PHONE, EXPORT, CLIP], ids=["camera", "phone", "export", "clip"])
def test_camera_style_name_in_review_is_blocked(repo, name):
    text = f"# Review\n\nThe frame comes from {name}, at 12 s.\n"
    result = commit(repo, {"openspec/changes/x/review.md": text})
    assert_blocked(result, repo, "footage-text", hidden=[name])
    assert "openspec/changes/x/review.md:3" in result.stderr


@pytest.mark.parametrize("path", ["docs/reference-notes/wind.md", "tools/a/notes.cs"])
def test_reference_file_stem_in_a_note_is_blocked(repo, path):
    plant_reference(repo)
    result = commit(repo, {path: f"// Notes\n\n// Gusts are clearest in {STEM} near the barn.\n"})
    assert_blocked(result, repo, "reference-stem", hidden=[STEM])
    assert f"{path}:3" in result.stderr


def test_reference_stem_is_blocked_in_a_linked_worktree(tmp_path):
    """Agents commit from worktrees: the footage sits in the main checkout, not in theirs."""
    main = init(tmp_path / "main")
    plant_reference(main)
    assert commit(main, {"README.md": "# Repo\n"}).returncode == 0
    assert git(main, "worktree", "add", "-q", "-b", "feature", "../wt").returncode == 0
    wt = tmp_path / "wt"
    assert not (wt / "reference").exists()
    head = git(wt, "rev-parse", "HEAD").stdout
    result = commit(wt, {"docs/notes.md": f"# Notes\n\nSee {STEM}.\n"})
    assert result.returncode != 0 and "reference-stem" in result.stderr
    assert STEM.lower() not in result.stderr.lower()
    assert git(wt, "rev-parse", "HEAD").stdout == head


@pytest.mark.parametrize("kind", ["clip", "stem", "messenger", "coordinates"])
def test_footage_name_in_commit_message_is_blocked(repo, kind):
    plant_reference(repo)
    planted = {"clip": CLIP, "stem": STEM, "messenger": LATIN[0], "coordinates": f"{A}, {B}"}[kind]
    rule = {"clip": "footage-text", "stem": "reference-stem", "messenger": "messenger",
            "coordinates": "coordinates"}[kind]
    result = commit(repo, {"docs/notes.md": "# Notes\n"}, message=f"docs(notes): add {planted}")
    print(f"\n[{kind}] git commit rc={result.returncode}\n{result.stderr.rstrip()}")
    assert_blocked(result, repo, rule, hidden=[planted])
    assert "commit-msg:" in result.stderr and "commit message:1" in result.stderr


@pytest.mark.parametrize("text", [
    "*.mp4\n**/*.MOV\n", "Ignore `*.mp4` and `*.mkv`; a `.webm` file is footage too.\n",
    "The mp4 container, H.264 video and the video_feed.cs script.\n",
], ids=range(3))
def test_extension_patterns_pass(repo, text):
    assert_passes(commit(repo, {"docs/notes.md": text, "notes.txt": text}), repo)


# --- spec: Media only where assets live -----------------------------------------------------

@pytest.mark.parametrize("name", ["diagram.png", "frame.jpg"])
def test_image_in_docs_is_blocked(repo, name):
    result = commit(repo, {f"docs/{name}": b"\x89PNG\r\n\x1a\n\0\0"})
    assert_blocked(result, repo, "media-location", hidden=[name.split(".")[0]])


def test_texture_in_unity_assets_passes(repo):
    result = commit(repo, {"unity/Assets/_Project/Art/brick_albedo.png": b"\x89PNG\r\n\x1a\n\0\0"})
    assert_passes(result, repo)


# --- spec: No OPSEC strings in text ---------------------------------------------------------

@pytest.mark.parametrize("script", ["latin", "cyrillic"])
def test_messenger_name_is_blocked(repo, script):
    term = LATIN[0].upper() if script == "latin" else CYRILLIC[0].title()
    text = f"# Notes\n\nShared through {term} after the flight.\n"
    result = commit(repo, {"docs/notes.md": text})
    assert_blocked(result, repo, "messenger", hidden=[term])
    assert "docs/notes.md:3" in result.stderr


def unambiguous_only_heads():
    """Words stored only in unambiguous forms (a domain, "<word> messenger", "<word> app")."""
    heads = {re.split(r"[ .]", t)[0] for t in precommit.MESSENGER_TERMS}
    return sorted(h for h in heads - set(precommit.MESSENGER_TERMS) if len(h) > 2)


def test_ordinary_word_that_is_also_a_messenger_name_passes(repo):
    heads = unambiguous_only_heads()
    assert len(heads) >= 4
    text = "".join(f"- There was {h} among the crew; {h.title()} again.\n" for h in heads)
    assert_passes(commit(repo, {"docs/notes.md": "# Notes\n\n" + text}), repo)


@pytest.mark.parametrize("path", ["docs/place.md", "openspec/changes/x/design.md", "NOTES.md"])
def test_regional_decimal_coordinates_are_blocked(repo, path):
    result = commit(repo, {path: f"Launch point {A}, {B} by the road.\n"})
    assert_blocked(result, repo, "coordinates", hidden=[A, B])
    assert f"{path}:1" in result.stderr


def test_labelled_coordinate_in_json_is_blocked(repo):
    text = f'{{\n  "name": "start",\n  "{KEY}": {NORTH:.2f}\n}}\n'
    result = commit(repo, {"tools/map/points.json": text})
    assert_blocked(result, repo, "labelled-coordinate", hidden=[f"{NORTH:.2f}"])
    assert "tools/map/points.json:3" in result.stderr


def test_physics_coefficients_pass(repo):
    text = ("Drag coefficients 1.2345, 0.8765 and 12.3456, 78.9012 (D-028).\n"
            f"Inertia -{A}, -{B} kg m2 and {A} m, {B} m.\n")
    assert_passes(commit(repo, {"openspec/changes/x/design.md": text}), repo)


def test_hook_own_sources_pass(repo):
    assert_passes(commit(repo, {rel: (ROOT / rel).read_bytes() for rel in OWN}), repo)


# --- spec: No dates in notes ----------------------------------------------------------------

def test_sortie_like_date_in_object_sheet_is_blocked(repo):
    result = commit(repo, {"docs/object-sheets/fence.md": f"# Fence\n\nseen on {DATE}\n"})
    assert_blocked(result, repo, "notes-date", hidden=[DATE])
    assert "docs/object-sheets/fence.md:3" in result.stderr


def test_date_on_a_line_that_merely_mentions_the_pilot_is_blocked(repo):
    line = f"the pilot flew there on {D + 11} March {Y}\n"
    result = commit(repo, {"docs/reference-notes/a.md": line})
    assert_blocked(result, repo, "notes-date", hidden=[str(Y)])


def test_attributed_decision_date_is_allowed(repo):
    line = f"Legs stay on (the pilot, {iso(2026, 10, 17)}).\n"
    assert_passes(commit(repo, {"docs/reference-notes/legs.md": line}), repo)


# --- spec: Git LFS keeps working ------------------------------------------------------------

def lfs_hook_body(hook):
    return (
        "#!/bin/sh\ncommand -v git-lfs >/dev/null 2>&1 || { printf >&2 \"\\n%s\\n\\n\" \"This "
        "repository is configured for Git LFS but 'git-lfs' was not found on your path. If you "
        "no longer wish to use Git LFS, remove this hook by deleting the '" + hook + "' file in "
        "the hooks directory (set by 'core.hookspath'; usually '.git/hooks').\"; exit 2; }\n"
        f"git lfs {hook} \"$@\"\n"
    ).encode("ascii")


@pytest.mark.parametrize("hook", LFS_HOOKS)
def test_lfs_shims_are_the_standard_git_lfs_hooks(hook):
    assert (HOOKS / hook).read_bytes() == lfs_hook_body(hook)


@pytest.mark.skipif(shutil.which("git-lfs") is None, reason="git-lfs is not installed")
def test_lfs_shims_match_what_the_installed_git_lfs_writes():
    printed = git(ROOT, "lfs", "update", "--manual").stdout
    blocks = re.findall(r"Add the following to '[^']*/([\w-]+)':\n\n((?:\t.*\n)+)", printed)
    bodies = {hook: "".join(line[1:] + "\n" for line in block.splitlines()) for hook, block in blocks}
    assert sorted(bodies) == sorted(LFS_HOOKS)
    for hook, body in bodies.items():
        assert (HOOKS / hook).read_bytes() == body.encode("ascii"), hook


@pytest.mark.skipif(shutil.which("git-lfs") is None, reason="git-lfs is not installed")
@pytest.mark.parametrize("shims", [True, False], ids=["with-shims", "control-without-shims"])
def test_lfs_push_uploads_objects_and_leaves_no_hook_files(tmp_path, shims):
    """The docs/environment.md set-up in a clone that carries .githooks, then a push.

    The control run drops the shims and shows the failure they prevent: git-lfs then writes
    its own copies into the tracked hooks folder as untracked files.
    """
    work, remote = tmp_path / "work", tmp_path / "remote.git"
    assert git(tmp_path, "init", "-q", "--bare", str(remote)).returncode == 0
    init(work, hooks=None)
    own = [rel for rel in OWN if "tests" not in rel and (shims or Path(rel).name not in LFS_HOOKS)]
    write(work, {rel: (ROOT / rel).read_bytes() for rel in own})
    write(work, {".gitattributes": "*.bin filter=lfs diff=lfs merge=lfs -text\n"})
    assert git(work, "lfs", "install", "--local").returncode == 0
    assert git(work, "config", "core.hooksPath", ".githooks").returncode == 0
    assert git(work, "add", "--", ".gitignore", ".gitattributes", *own).returncode == 0
    assert git(work, "commit", "-q", "-m", "chore: set up hooks").returncode == 0

    data = bytes(range(256)) * 64
    result = commit(work, {"data.bin": data}, message="feat: add data")
    assert result.returncode == 0, result.stderr
    stray = git(work, "status", "--porcelain", "--untracked-files=all", "--", ".githooks").stdout
    if not shims:
        assert stray.strip(), "the control should show git-lfs writing untracked hook files"
        return
    assert stray == ""

    assert git(work, "remote", "add", "origin", remote.as_uri()).returncode == 0
    pushed = git(work, "push", "-q", "origin", "main")
    assert pushed.returncode == 0, pushed.stderr
    oid = hashlib.sha256(data).hexdigest()
    stored = remote / "lfs" / "objects" / oid[:2] / oid[2:4] / oid
    print(f"\nLFS object on the bare remote: {stored.is_file()}; "
          f"git status after push: {git(work, 'status', '--porcelain', '--untracked-files=all').stdout!r}")
    assert stored.is_file() and stored.read_bytes() == data
    assert git(work, "status", "--porcelain", "--untracked-files=all").stdout == ""


# --- spec: Clean commits pass quickly -------------------------------------------------------

def test_ordinary_code_commit_passes(repo):
    files = {
        "unity/Packages/com.fpv450.sim/Runtime/Flight/Rotor.cs":
            "namespace Fpv450.Sim.Flight\n{\n    public struct Rotor { public float Radius; }\n}\n",
        "tools/map/validate.py": "def validate(path):\n    return path.exists()\n",
        "docs/notes.md": "# Notes\n\nThe video signal drops behind the barn; the wire sags 0.4 m.\n",
    }
    assert_passes(commit(repo, files, message="feat(sim): add rotor struct"), repo)


def test_later_commits_are_scanned_too(repo):
    """Modified and renamed files on top of an existing HEAD go through the same rules."""
    assert commit(repo, {"docs/notes.md": "# Notes\n", "docs/plan.md": "# Plan\n"}).returncode == 0
    head = git(repo, "rev-parse", "HEAD").stdout

    modified = commit(repo, {"docs/notes.md": f"# Notes\n\nvia {LATIN[0].title()}\n"})
    assert modified.returncode != 0 and "messenger" in modified.stderr
    assert "docs/notes.md:3" in modified.stderr
    assert git(repo, "reset", "-q", "--", "docs/notes.md").returncode == 0

    assert git(repo, "mv", "docs/plan.md", f"docs/{CAMERA}.md").returncode == 0
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
    # A reference/ the size of the real one, so the stem rule runs at full cost.
    plant_reference(repo, [f"{STEM}-{i:04d}.jpg" for i in range(1000)])
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
    result = git(repo, "commit", "-q", "-m", "feat: add generated files")
    commit_time = time.perf_counter() - start
    assert result.returncode == 0, result.stderr
    print(f"\n200 staged files: scan {', '.join(f'{t:.3f}' for t in times)} s; "
          f"full commit through both hooks {commit_time:.3f} s")
    assert max(times) < 2.0


@pytest.mark.parametrize("target", ["HEAD", "origin/unity/main merged with HEAD"])
def test_existing_tree_is_clean(target):
    """Every tracked file of this branch, and of unity/main as this branch would leave it."""
    rev = "HEAD"
    if target != "HEAD":
        if git(ROOT, "rev-parse", "-q", "--verify", "origin/unity/main").returncode != 0:
            pytest.skip("origin/unity/main is not fetched")
        merged = git(ROOT, "merge-tree", "--write-tree", "origin/unity/main", "HEAD")
        assert merged.returncode == 0, "this branch does not merge cleanly into unity/main"
        rev = merged.stdout.split()[0]
    scan = subprocess.run([sys.executable, str(PRECOMMIT), "--tree", rev], cwd=ROOT, env=ENV,
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(f"\n[{target}] {scan.stdout.strip()}")
    assert scan.returncode == 0, scan.stdout + scan.stderr
    assert scan.stdout.strip().endswith(" 0 findings")


# --- spec: Commit message format ------------------------------------------------------------

def test_long_subject_is_rejected(repo):
    subject = "fix(review): " + "a" * 60
    assert len(subject) == 73
    result = commit(repo, {"docs/notes.md": "# Notes\n"}, message=subject)
    print(f"\ngit commit rc={result.returncode}\n{result.stderr.rstrip()}")
    assert_blocked(result, repo, "commit-format")
    assert "commit message:1" in result.stderr and "73 characters" in result.stderr


@pytest.mark.parametrize("subject", ["Added the thing", "feature(x): thing", "fix(x): Thing"],
                         ids=["no-type", "unknown-type", "upper-case-summary"])
def test_wrong_shape_is_rejected(repo, subject):
    result = commit(repo, {"docs/notes.md": "# Notes\n"}, message=subject)
    assert_blocked(result, repo, "commit-format")
    assert "commit message:1" in result.stderr


@pytest.mark.parametrize("second", ["Explains why.", "Co-Authored-By: Someone <someone@example.invalid>"],
                         ids=["body", "co-author-trailer"])
def test_body_or_trailer_is_rejected(repo, second):
    result = commit(repo, {"docs/notes.md": "# Notes\n"}, message=f"fix(x): thing\n\n{second}\n")
    assert_blocked(result, repo, "commit-format")
    assert "commit message:3" in result.stderr


@pytest.mark.parametrize("subject", ["fix(review): keep the bar in frame", "fix(review): " + "a" * 59],
                         ids=["spec", "72-characters"])
def test_conforming_subject_passes(repo, subject):
    assert len(subject) <= 72
    assert_passes(commit(repo, {"docs/notes.md": "# Notes\n"}, message=subject), repo)


def side_branch(repo, name, file):
    """One commit on a new branch off main, then back on main."""
    assert git(repo, "switch", "-q", "-c", name).returncode == 0
    assert commit(repo, {file: f"# {file}\n"}).returncode == 0
    assert git(repo, "switch", "-q", "main").returncode == 0


@pytest.mark.parametrize("kind", ["branch", "branches", "remote-tracking", "tag", "commit", "pull-request"])
def test_git_merge_commit_passes(repo, kind):
    """A real `git merge` of each kind fmt-merge-msg names (git runs commit-msg for merges
    too), and GitHub's merge shape with the PR title as its body."""
    assert commit(repo, {"a.md": "# A\n"}).returncode == 0
    if kind == "pull-request":
        message = "Merge pull request #1 from someone/unity/x/y\n\nfeat(x): add y\n"
        result = commit(repo, {"b.md": "# B\n"}, message=message)
        assert result.returncode == 0, result.stderr
        assert git(repo, "log", "-1", "--format=%s").stdout.startswith("Merge pull request ")
        return
    side_branch(repo, "topic", "b.md")
    targets, expected = ["topic"], "Merge branch 'topic'"
    if kind == "branches":
        side_branch(repo, "other", "c.md")
        targets, expected = ["topic", "other"], "Merge branches 'topic' and 'other'"
    elif kind == "remote-tracking":
        remote = repo.parent / (repo.name + "-remote.git")
        remote.mkdir()
        assert git(remote, "init", "-q", "--bare").returncode == 0
        assert git(repo, "remote", "add", "origin", remote.as_posix()).returncode == 0
        assert git(repo, "push", "-q", "origin", "topic").returncode == 0
        assert git(repo, "branch", "-q", "-D", "topic").returncode == 0
        targets, expected = ["origin/topic"], "Merge remote-tracking branch 'origin/topic'"
    elif kind == "tag":
        assert git(repo, "tag", "v1", "topic").returncode == 0
        assert git(repo, "branch", "-q", "-D", "topic").returncode == 0
        targets, expected = ["v1"], "Merge tag 'v1'"
    elif kind == "commit":
        sha = git(repo, "rev-parse", "topic").stdout.strip()
        assert git(repo, "branch", "-q", "-D", "topic").returncode == 0
        targets, expected = [sha], f"Merge commit '{sha}'"
    merged = git(repo, "merge", "--no-ff", "--no-edit", *targets)
    assert merged.returncode == 0, merged.stderr
    assert git(repo, "log", "-1", "--format=%s").stdout.rstrip("\n") == expected
    assert len(git(repo, "rev-list", "--parents", "-1", "HEAD").stdout.split()) == 2 + len(targets)


@pytest.mark.parametrize("message", ["# just a comment", "#123 fix the thing", ""],
                         ids=["comment", "issue-number", "empty"])
def test_comment_like_or_empty_subject_is_rejected(repo, message):
    """`git commit -m` keeps a `#` line (cleanup=whitespace), so it is the subject, not a comment."""
    write(repo, {"docs/notes.md": "# Notes\n"})
    assert git(repo, "add", "--", "docs/notes.md").returncode == 0
    result = git(repo, "commit", "-q", "--allow-empty-message", "-m", message)
    assert_blocked(result, repo, "commit-format")
    assert "commit message:1" in result.stderr
    if not message:
        assert "no subject" in result.stderr


# --- rules, case by case (no git) -----------------------------------------------------------

@pytest.mark.parametrize("ext", sorted(precommit.FOOTAGE_EXT))
def test_every_footage_extension_is_caught_in_any_case(ext):
    assert precommit.footage_ext(f"a/b{ext}")
    assert precommit.footage_ext(f"a/b{ext.upper()}")
    assert precommit.footage_text(f"see b{ext} and B{ext.upper()}")


@pytest.mark.parametrize("path", ["a.cs", "a.ts", "a.md", j("a", ".mp4", ".md"), "mp4"])
def test_other_extensions_are_not_footage(path):
    assert not precommit.footage_ext(path)


CAMERA_NAMES = [
    ("DJI_", "0001.txt"), ("DJIG", "0007.md"), ("VID_", "2024", "0101_", "120000.md"),
    ("IMG_", "1234.cs"), ("PXL_", "2024", "0101_1.txt"), ("MVI_", "0042.txt"), ("DSC_", "0001.md"),
    ("DSCN", "0001.md"), ("DSCF", "0001.md"), ("DSC", "00001.md"), ("GOPR", "0001.md"),
    ("GX", "010001.md"), ("GH", "010001.md"), ("Screenshot_", "2024", "0101-120000.md"),
    ("Screenshot ", "2024", "-01-01.md"), ("photo_", "2024", "-01-01_12-00-00.md"),
    ("video_", "2024", "-01-01_12-00-00.md"), ("IMG-", "2024", "0101-WA0001.md"),
    ("VID-", "2024", "0101-WA0001.md"), ("2024", "0101_", "120000.md"), ("dji_", "0001.md"),
]


@pytest.mark.parametrize("index", range(len(CAMERA_NAMES)))
def test_camera_and_export_names_are_caught(index):
    name = j(*CAMERA_NAMES[index])
    assert precommit.footage_name(f"some/dir/{name}")
    assert precommit.footage_text(f"The frame from {name} at 12 s.")
    assert precommit.footage_text(f"(`{name.split('.')[0]}`)")


@pytest.mark.parametrize("name", [
    "video_feed.cs", "image_util.py", "dji_notes.md", "screenshot_tool.py", "gx12.md",
    "tx12_profile.md", "README.md", "photo_scan.md", "2024_plan.md",
])
def test_ordinary_names_are_not_footage_names(name):
    assert not precommit.footage_name(f"some/dir/{name}")
    assert not precommit.footage_text(f"see {name} for details")


@pytest.mark.parametrize("line", [
    "*.mp4", "**/*.MOV", "`*.mkv`", ".webm files", "the mp4 container", "H.264 and H.265 video",
    "Betaflight 4.5.10", "flyby clip", "x_mov.md", "a.movie", "e.g. the dav format",
])
def test_bare_extensions_and_words_are_not_footage_text(line):
    assert not precommit.footage_text(line)


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


@pytest.mark.parametrize("index", range(len(unambiguous_only_heads())))
def test_unambiguous_only_names_pass_as_bare_words(index):
    head = unambiguous_only_heads()[index]
    for variant in (head, head.title(), head.upper(), f"{head} applies", f"{head}s"):
        assert not precommit.messenger(f"There was {variant} among the crew."), index


@pytest.mark.parametrize("line", [
    "The video signal drops behind the barn.", "Signal strength falls with range.",
    "signal.m" + "ean() over the frame", "A wire strike at 3 m.", "Fiber slack on the spool.",
    "Line of sight is lost.", "rotation matrix", "a messenger bag", "сигнал слабне",
    "a command-line app",
])
def test_project_vocabulary_is_not_a_messenger(line):
    assert not precommit.messenger(line)


def dms(value):
    degrees = int(value)
    minutes = int((value - degrees) * 60)
    seconds = round(((value - degrees) * 60 - minutes) * 60)
    return f"{degrees}°{minutes:02d}′{seconds:02d}″"


COORDINATE_LINES = [
    f"{A}, {B}", f"{B}, {A}", f"{A};{B}", f"{A} {B}", f"({A}, {B}, 120.5000)", f"@{A},{B},15z",
    f"{A.replace('.', ',')}; {B.replace('.', ',')}", f"{A.replace('.', ',')} {B.replace('.', ',')}",
    f"N{A} E{B}", f"N {A}, E {B}", f"{A}°N, {B}°E", f"{A} N {B} E", f"N {A}", f"E {B}",
    f"{dms(NORTH)}N {dms(EAST)}E", f"{dms(NORTH)} {dms(EAST)}", f"N {int(NORTH)}°{22}′", f"{int(NORTH)}°{22}.5′N",
    f"{int(EAST)}° {34}' {4}\" E", f"{35}T MK {1234} {5678}", f"{35}TMK{12345678}",
    f"grid {35}t mk {12345} {67890} here",
]


@pytest.mark.parametrize("index", range(len(COORDINATE_LINES)))
def test_regional_coordinate_forms_are_caught(index):
    assert precommit.coordinates(COORDINATE_LINES[index])


ORDINARY_NUMBERS = [
    "vec3(0.1031, 0.1030, 0.0973)", "colour (0.3247, 0.2142, 0.1035)", "a 45° bank", "0° uptilt",
    "30° 20′ with no hemisphere", "TX12", "G1 to G8", "D-028", "4K PAL 50 Hz", "1.2345 m, 2.3456 m",
    "vec2(12.9898, 78.233)", "200.1234, 300.1234", "25G AB 123", "1080p", "M3 bolt, 10 mm",
    "1.2345, 2.3456", "12.3456, 78.9012", "25G AB 1234 5678", "10°20′30″", "70°20′30″N",
    "E 70.1234", "N 12.3456", f"-{A}, -{B}", f"{A} m, {B} m", f"{int(NORTH)}°{22}′ bank",
    "a 35V ESC rated 1200 W",
]


@pytest.mark.parametrize("index", range(len(ORDINARY_NUMBERS)))
def test_ordinary_numbers_are_not_coordinates(index):
    assert not precommit.coordinates(ORDINARY_NUMBERS[index])


LABELLED = [
    f'"{KEY}": {NORTH:.4f}', f"{KEY}={NORTH:.4f}", f"{KEY}itude: {NORTH:.1f}", f"'{KEY}' : '{NORTH:.2f}'",
    f"l{'ng'}: {EAST:.4f}", f"l{'on'} = -12", f"gps_{KEY} = {int(NORTH)}", f"?{KEY}={NORTH:.3f}&z=15",
    f"{KEY.upper()}ITUDE= {NORTH:.2f}",
]


@pytest.mark.parametrize("index", range(len(LABELLED)))
def test_labelled_coordinates_are_caught(index):
    assert precommit.labelled_coordinate(LABELLED[index])


@pytest.mark.parametrize("line", [
    "latency: 5 ms", "flat: 3", "lateral: 2 m", "plate = 4", "the lon/lat order",
    "the regional box (latitude 40-60°, longitude 15-50°)", "translate(3, 4)", "Lat. 50 N",
    "float latAccel = 2f;",
])
def test_unlabelled_numbers_are_not_labelled_coordinates(line):
    assert not precommit.labelled_coordinate(line)


DATE_LINES = [
    f"seen on {DATE}", f"{DATE}T10:00", f"on {D + 11:02d}.{M + 1:02d}.{Y}.", f"on {D}.{M}.{Y}",
    f"on {D + 11:02d}.{M + 1:02d}.{Y % 100:02d}", f"on {D + 11:02d}/{M + 1:02d}/{Y}",
    f"on {M + 1}/{D + 11}/{Y}", f"{Y}/{M:02d}/{D:02d}", f"{Y}.{M:02d}.{D:02d}", f"stamp {Y}{M:02d}{D:02d}",
    f"on {D} March {Y}", f"on {D}th of March", f"March {D}, {Y}", f"in Mar {Y}", f"{D}-Mar-{Y}",
    f"{D} Sept. {Y}", f"{D} березня {Y} р.", f"у березні {Y}", f"{D} марта", f"в марте {Y}",
    f"the pilot flew there on {D} March {Y}", f"The pilot said so on {DATE}.",
    f"(the pilot, {DATE}) and the sortie on {iso(Y, M, D + 1)}",
]


@pytest.mark.parametrize("index", range(len(DATE_LINES)))
def test_dates_are_caught(index):
    line = DATE_LINES[index]
    assert precommit.notes_date(line)
    assert precommit.date_hint(line), "the whole-file hint must cover every date the rule catches"


NOT_DATES = [
    f"(the pilot, {DATE})", f"(pilot, {DATE})", f"Source: the pilot, {DATE}.",
    f"**What the pilot said** ({DATE}, after flying the feed).", f"**Correction from the pilot** ({DATE}):",
    f"D-028 ({DATE})", "see §4.1.2", "Betaflight 4.5.10", "v1.2.3", "2026-13-01", "1.25.30",
    "Unity 2022.3.14f1", "in March", "12/24/48 V", "it may 2x the drag", "a 12 m march",
    "May the wind drop", "10 червоних цеглин", "трава 20 см",
]


@pytest.mark.parametrize("index", range(len(NOT_DATES)))
def test_non_dates_and_attributed_dates_pass(index):
    assert not precommit.notes_date(NOT_DATES[index])


@pytest.mark.parametrize("name, kept", [
    (j("barn", "-roof-a", ".jpg"), True), (j("clip", " ", "seven", ".mp4"), True),
    ("track_0001.json", True), ("wind_field.py", False), (j("overview", ".mp4"), False),
    (j("a_", "0001", ".mp4"), False), ("notes.txt", False),
])
def test_distinctive_stems(name, kept):
    assert (precommit.distinctive_stem(name) is not None) == kept


def test_reference_stems_match_whole_tokens_in_any_case():
    stems = precommit.ReferenceStems({STEM})
    for i, text in enumerate([STEM, STEM.upper(), f"`{STEM}.mp4`", f"see {STEM}, then", f"a_{STEM}"]):
        assert stems.hint(text) and stems.check(text), i
    for i, text in enumerate(["x" + STEM, STEM + "x", STEM + "1", STEM.replace("-", " "), "nothing"]):
        assert not stems.hint(text), i


def test_reference_stems_scale_with_the_text_not_the_folder():
    stems = precommit.ReferenceStems({f"{STEM}-{i:05d}" for i in range(20000)})
    text = "".join(f"- Wall {i}: {i * 2} cm thick, seen from 50 m.\n" for i in range(2000))
    start = time.perf_counter()
    assert not stems.hint(text)
    assert time.perf_counter() - start < 0.2


def test_rules_apply_only_in_their_scope():
    coords, date, name = f"{A}, {B}", f"seen on {DATE}", "via " + LATIN[0]
    assert precommit.scan_text("tools/a.py", coords) == []
    assert precommit.scan_text("docs/plan.md", date) == []
    assert precommit.scan_text("tools/a.py", "see " + CLIP) == []
    assert precommit.scan_text("Docs/a.md", coords) == [("coordinates", 1)]
    assert precommit.scan_text("openspec/x/design.md", coords) == [("coordinates", 1)]
    assert precommit.scan_text("NOTES.md", coords) == [("coordinates", 1)]
    assert precommit.scan_text(None, coords) == [("coordinates", 1)]
    assert precommit.scan_text("docs/reference-notes/a.md", "ok\n" + date) == [("notes-date", 2)]
    assert precommit.scan_text(None, date) == []
    assert precommit.scan_text("tools/a.py", name) == [("messenger", 1)]
    assert precommit.scan_text("tools/a.py", f"{KEY}={A}") == [("labelled-coordinate", 1)]
    for ext in (".md", ".txt", ".yaml", ".yml", ".json"):
        assert precommit.scan_text("tools/a" + ext, "see " + CLIP) == [("footage-text", 1)]
    assert precommit.scan_text(None, "see " + CLIP) == [("footage-text", 1)]


def test_commit_message_below_the_scissors_line_is_not_scanned(tmp_path):
    message = tmp_path / "COMMIT_EDITMSG"
    message.write_text(f"docs: fix\n{SCISSORS}\n+ {A}, {B}\n", encoding="utf-8")
    assert precommit.scan_message(message, precommit.LINE_RULES) == []
    message.write_text(f"docs: fix\n\n{A}, {B}\n", encoding="utf-8")
    assert precommit.scan_message(message, precommit.LINE_RULES) == [
        "  coordinates         commit message:3",
        "  commit-format       commit message:3 "
        "(nothing may follow the subject: no body, no trailer, no co-author line)"]


@pytest.mark.parametrize("subject", [
    "feat: add x", "chore(tools): x", "art(k01): add y", "fix(com.fpv450.sim): x", "perf: 1 khz step",
    "refactor(a/b): x", "docs(openspec): `review.md` for x", "test: x\n",
])
def test_commit_format_accepts_every_type_and_scope_shape(subject):
    assert precommit.commit_format(subject) == []


@pytest.mark.parametrize("subject", [
    "fix:x", "fix(): x", "fix (x): x", "Fix: x", "fix: ", "fix(x) x", "feature: x", "fix(a b): x",
    "Merge the thing", "merge: x", "Merge branch main", "Merged branch 'x'", "Merge pull request from x",
    "# just a comment", "#123 fix the thing",
])
def test_commit_format_rejects_near_misses(subject):
    assert [line for line, _ in precommit.commit_format(subject)] == [1]


@pytest.mark.parametrize("subject", [
    "Merge branch 'x'", "Merge branch 'x' into y", "Merge branch 'x' of https://example.invalid/r",
    "Merge branches 'a' and 'b'", "Merge branches 'a', 'b' and 'c'", "Merge branch 'a', tag 'v1'",
    "Merge remote-tracking branch 'origin/x'", "Merge remote-tracking branches 'origin/a' and 'origin/b'",
    "Merge tag 'v1'", "Merge tags 'v1' and 'v2'", "Merge commit '0123abc'", "Merge commits '01' and '23'",
    "Merge pull request #12 from someone/unity/x/y\n\nfeat(x): add y\n",
])
def test_commit_format_exempts_every_git_merge_subject(subject):
    assert precommit.commit_format(subject) == []


def test_commit_format_takes_the_first_non_blank_line_as_the_subject():
    assert precommit.commit_format("") == [(1, "the message has no subject")]
    assert precommit.commit_format("\n\n") == [(1, "the message has no subject")]
    assert [line for line, _ in precommit.commit_format("\n# not a comment\n\nfix(x): y\n")] == [2, 4]
    assert precommit.commit_format("fix(x): y\n\n# a comment after the subject\n") == []


def test_commit_format_ignores_comment_lines_and_the_scissors_diff(tmp_path):
    message = tmp_path / "COMMIT_EDITMSG"
    template = "# Please enter the commit message for your changes.\n#\n# On branch main\n"
    message.write_text(f"fix(x): thing\n\n{template}{SCISSORS}\n+Co-Authored-By: nobody\n", encoding="utf-8")
    assert precommit.scan_message(message, precommit.LINE_RULES) == []


def test_utf16_and_bom_text_is_scanned_and_binary_is_skipped():
    text = precommit.decode_text(("via " + LATIN[0]).encode("utf-16"))
    assert text is not None and precommit.messenger(text)
    assert precommit.decode_text(b"\xef\xbb\xbfplain") == "plain"
    assert precommit.decode_text(b"\x89PNG\0\0\0") is None


@pytest.mark.parametrize("path, shown", [
    (j("test", ".mp4"), "*.mp4"), (f"docs/x/{CAMERA}.txt", "docs/x/*.txt"),
    (j("reference/a/b", ".mp4"), "reference/..."),
])
def test_paths_are_masked_in_messages(path, shown):
    assert precommit.masked(path) == shown


@pytest.mark.parametrize("hook", ["pre-commit", "commit-msg", *LFS_HOOKS])
def test_hooks_are_lf_shell_scripts_tracked_as_executable(hook):
    data = (HOOKS / hook).read_bytes()
    assert data.startswith(b"#!/bin/sh\n") and b"\r" not in data
    staged = git(ROOT, "ls-files", "-s", "--", f".githooks/{hook}").stdout
    assert staged.startswith("100755 "), f"{hook} is not tracked as 100755"


@pytest.mark.parametrize("rel", OWN)
def test_own_sources_hold_no_planted_value(rel):
    """Every line rule, out of scope too, finds nothing in the hook and these tests."""
    lines = (ROOT / rel).read_text(encoding="utf-8").split("\n")
    hits = [(rule, n) for rule, _, _, check in precommit.LINE_RULES
            for n, line in enumerate(lines, 1) if check(line)]
    assert not hits, f"{rel}: {hits}"
