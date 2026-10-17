"""Path guard shared by the reference tools (OPSEC, SEC-01).

Clips are read only from under <root>/reference/. Everything is written only strictly inside
<root>/reference/_frames/, which git ignores. Paths are resolved first (`..`, symlinks), so
neither `..`, an absolute path nor a link can step outside. Error messages never repeat a clip path.
"""
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LETTER = re.compile(r"[A-Z]")  # the notes cite clips by one capital letter


class RefError(Exception):
    """A refused argument or a failed step. The message never names a reference file."""


def letter(value):
    """argparse type: one capital letter A-Z."""
    if not LETTER.fullmatch(value):
        raise ValueError(value)
    return value


def reference_dir(root):
    return (Path(root).resolve() / "reference").resolve()


def frames_dir(root):
    return (reference_dir(root) / "_frames").resolve()


def _inside(path, base):
    return base in Path(path).resolve().parents


def clip_path(root, clip):
    """The resolved clip, which must be an existing file under reference/."""
    p = Path(clip).resolve()
    if not _inside(p, reference_dir(root)):
        raise RefError("the clip must be a file under reference/")
    if not p.is_file():
        raise RefError("the clip was not found under reference/")
    return p


def frames_path(root, path, what):
    """The resolved path, which must lie strictly inside reference/_frames/."""
    p = Path(path).resolve()
    if not _inside(p, frames_dir(root)):
        raise RefError(f"{what} must be inside reference/_frames/")
    return p
