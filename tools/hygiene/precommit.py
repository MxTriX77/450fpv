"""Pre-commit OPSEC and hygiene scan of the staged files (SEC-01, SEC-02, SEC-06).

Run by .githooks/pre-commit. It scans the index, never the working tree, and exits 1 to block
the commit. Findings print the rule id and where it hit, never the matched text, so the hook's
own output can't leak what it caught. A false positive is fixed in the rule, never bypassed.
"""
import base64
import posixpath
import re
import subprocess
import sys

FOOTAGE_EXT = {
    ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".mts", ".m2ts", ".3gp", ".webm", ".wmv",
    ".flv", ".mpg", ".mpeg", ".h264", ".h265", ".hevc", ".insv", ".lrv", ".dav",
}
IMAGE_EXT = {
    ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tif", ".tiff", ".webp", ".heic",
    ".heif", ".avif", ".exr", ".hdr", ".psd", ".svg", ".dng", ".cr2", ".cr3", ".nef",
    ".arw", ".orf", ".rw2",
}
AUDIO_EXT = {
    ".wav", ".mp3", ".ogg", ".flac", ".aac", ".m4a", ".opus", ".wma", ".aif", ".aiff", ".amr",
}
MEDIA_EXT = FOOTAGE_EXT | IMAGE_EXT | AUDIO_EXT

# Exempting prefixes match case-sensitively and scoping prefixes case-insensitively,
# so a case slip always makes the scan stricter, never looser.
MEDIA_ROOTS = ("assets-src/", "unity/Assets/")
DOCS_ROOT = "docs/"
NOTES_ROOTS = ("docs/reference-notes/", "docs/object-sheets/")

# Camera, phone, screenshot and messenger-export names, matched at the start of the base name.
FOOTAGE_NAME = re.compile(
    r"(?:DJI_|DJIG|VID_|IMG_|PXL_|MVI_|DSC[A-Z_]?|GOPR)\d|G[HX]\d{6}"
    r"|screen ?shot[ _-]?20\d\d"
    r"|(?:photo|video)_20\d\d-"
    r"|(?:IMG|VID|AUD|PTT)-20\d{6}-WA\d"
    r"|20\d{6}_\d{6}",
    re.IGNORECASE,
)

# Messenger names, Latin and Cyrillic, one per line, base64 so this file never holds them in
# plain text. Names that are also project words appear only in unambiguous forms (a domain,
# or the name followed by "messenger").
_MESSENGER_B64 = (
    "dGVsZWdyYW0KdGVsZWdyYS5waAp0Lm1lLwp3aGF0c2FwcAp3aGF0cyBhcHAKd2EubWUvCnZp"
    "YmVyCmRpc2NvcmQKc2t5cGUKd2VjaGF0CnRocmVlbWEKaW1lc3NhZ2UKc25hcGNoYXQKa2Fr"
    "YW90YWxrCmZhY2Vib29rIG1lc3NlbmdlcgpmYiBtZXNzZW5nZXIKbGluZSBtZXNzZW5nZXIK"
    "c2lnbmFsIG1lc3NlbmdlcgpzaWduYWwub3JnCnNpZ25hbC5tZQrRgtC10LvQtdCz0YDQsNC8"
    "CtCy0LDRgtGB0LDQvwrQstCw0YbQsNC/CtCy0L7RgtGB0LDQvwrQstC+0YbQsNC/CtCy0LDQ"
    "udCx0LXRgArQtNC40YHQutC+0YDQtArRgdC60LDQudC/CtCy0ZbRh9Cw0YIK0LLQuNGH0LDR"
    "ggrRgdC40LPQvdCw0Lsg0LzQtdGB0LXQvdC00LbQtdGACtGB0LjQs9C90LDQuyDQvNC10YHR"
    "gdC10L3QtNC20LXRgA=="
)
MESSENGER_TERMS = base64.b64decode(_MESSENGER_B64).decode("utf-8").split("\n")

_LETTER = r"[^\W\d_]"


def _term_regex(term):
    body = r"\s+".join(re.escape(word) for word in term.split(" "))
    if not term.isascii():
        body += _LETTER + "*"  # Cyrillic names inflect: the stem plus any ending
    if term[-1].isalpha():
        body += rf"(?!{_LETTER})"
    return body


# Whole words: no letter may touch a name on either side (every name starts with a letter).
MESSENGER = re.compile(
    rf"(?<!{_LETTER})(?:" + "|".join(_term_regex(t) for t in MESSENGER_TERMS) + ")", re.IGNORECASE
)

_DEC = r"[-+]?\d{1,3}\.\d{4,}"
# The second number sits in a lookahead, so it can also start the next pair (lat, lon, alt).
DECIMAL_PAIR = re.compile(
    rf"(?<![\d.])({_DEC})(?=\s*°?\s*[NSns]?\s*(?:[,;/]|\s)\s*({_DEC})(?!\.?\d))"
)
DMS = re.compile(
    r"(?<!\d)\d{1,3}\s*°\s*\d{1,2}(?:[.,]\d+)?\s*['′’]\s*"
    r"(?:\d{1,2}(?:[.,]\d+)?\s*(?:[\"″”]|''|′′)\s*)?[NSEWnsew](?![^\W\d_])"
    r"|(?<![^\W\d_])[NSEW]\s*\d{1,3}\s*°\s*\d{1,2}(?:[.,]\d+)?\s*['′’]"
)
MGRS = re.compile(
    r"(?<!\w)(?:[1-9]|[1-5]\d|60)[C-HJ-NP-X]\s?[A-HJ-NP-Z][A-HJ-NP-V]\s?"
    r"(?:(\d{1,5})\s(\d{1,5})|(\d{2,10}))(?!\w)"
)

DATE = re.compile(
    r"(?<!\d)(?:19|20)\d\d-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])(?!\d)"
    r"|(?<![\d.])(?:0?[1-9]|[12]\d|3[01])\.(?:0?[1-9]|1[0-2])\.(?:19|20)\d\d(?!\.?\d)"
    r"|(?<![\d.])(?:0[1-9]|[12]\d|3[01])\.(?:0[1-9]|1[0-2])\.\d\d(?!\.?\d)"
)
# A decision id, or an attribution to the pilot: "(the pilot, <date>)" and "(pilot, <date>)".
DATE_EXCUSE = re.compile(r"D-\d{3}|(?i:\bpilot\b)")

# Whole-file prefilters, each matching a superset of its line rule, so most files skip the
# per-line pass (the lookbehinds above are slow when run at every position of every line).
MESSENGER_HEADS = sorted({t.split(" ")[0].lower() for t in MESSENGER_TERMS})
COORDINATES_HINT = re.compile(r"\d\.\d{4}|°\s*\d|\d[C-HJ-NP-X]\s?[A-HJ-NP-Z][A-HJ-NP-V]")
DATE_HINT = re.compile(r"\d[-.]\d\d?[-.]\d")


# --- path rules: (path) -> bool ---------------------------------------------------------------

def extension(path):
    return posixpath.splitext(path)[1].lower()


def footage_ext(path):
    return extension(path) in FOOTAGE_EXT


def reference_path(path):
    return path.lower().startswith("reference/")


def footage_name(path):
    return FOOTAGE_NAME.match(posixpath.basename(path)) is not None


def media_location(path):
    return extension(path) in IMAGE_EXT | AUDIO_EXT and not path.startswith(MEDIA_ROOTS)


# --- line rules: (line) -> bool ---------------------------------------------------------------

def messenger(line):
    return MESSENGER.search(line) is not None


def coordinates(line):
    for m in DECIMAL_PAIR.finditer(line):
        a, b = abs(float(m.group(1))), abs(float(m.group(2)))
        # Either axis order. Both below 1 is open ocean, and it is what colours, vectors
        # and shader constants look like, so it is not a coordinate.
        in_range = (a <= 90 and b <= 180) or (a <= 180 and b <= 90)
        if in_range and max(a, b) >= 1:
            return True
    if DMS.search(line):
        return True
    for m in MGRS.finditer(line):
        east, north, run = m.groups()
        if (run and len(run) % 2 == 0) or (east and len(east) == len(north)):
            return True
    return False


def notes_date(line):
    return DATE.search(line) is not None and DATE_EXCUSE.search(line) is None


def messenger_hint(text):
    lowered = text.lower()
    return any(head in lowered for head in MESSENGER_HEADS)


PATH_RULES = (
    ("footage-ext", footage_ext),
    ("reference-path", reference_path),
    ("footage-name", footage_name),
    ("media-location", media_location),
)
# (rule id, path prefixes it applies to or None for every text file, whole-file hint, line check)
LINE_RULES = (
    ("messenger", None, messenger_hint, messenger),
    ("coordinates", (DOCS_ROOT,), COORDINATES_HINT.search, coordinates),
    ("notes-date", NOTES_ROOTS, DATE_HINT.search, notes_date),
)


def masked(path):
    """The path with its file name hidden: a footage-like name is itself the leak."""
    if reference_path(path):
        return path[:len("reference/")] + "..."
    parent = path.rsplit("/", 1)[0] + "/" if "/" in path else ""
    return parent + "*" + posixpath.splitext(path)[1]


def decode_text(blob):
    """The blob as text, or None if it is binary."""
    if blob[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return blob.decode("utf-16", "replace")
    if b"\0" in blob[:8000]:
        return None
    return blob.decode("utf-8-sig", "replace")


def scan_path(path):
    return [rule for rule, check in PATH_RULES if check(path)]


def scan_text(path, text):
    """(rule id, line number) for every line that breaks a line rule in scope for path."""
    lowered = path.lower()
    hits = []
    for rule, scope, hint, check in LINE_RULES:
        if (scope is None or lowered.startswith(scope)) and hint(text):
            lines = enumerate(text.split("\n"), 1)
            hits.extend((rule, number) for number, line in lines if check(line))
    return sorted(hits, key=lambda hit: hit[1])


def git(*args, stdin=None):
    return subprocess.run(["git", *args], input=stdin, capture_output=True, check=True).stdout


def staged():
    """(path, mode, blob id) for every added, copied, modified, renamed or retyped staged file."""
    out = git("diff", "--cached", "--raw", "-z", "--no-abbrev", "--no-renames", "--no-color",
              "--no-ext-diff", "--diff-filter=ACMRT")
    fields = out.split(b"\0")
    entries = []
    for meta, path in zip(fields[0::2], fields[1::2]):
        _, mode, _, oid, _ = meta.decode("ascii").split(" ")
        entries.append((path.decode("utf-8", "replace"), mode, oid))
    return entries


def blobs(oids):
    if not oids:
        return []
    out = git("cat-file", "--batch", stdin=("\n".join(oids) + "\n").encode("ascii"))
    result, pos = [], 0
    for _ in oids:
        end = out.index(b"\n", pos)
        size = int(out[pos:end].split()[2])
        result.append(out[end + 1:end + 1 + size])
        pos = end + 2 + size
    return result


def main():
    findings = []
    to_read = []
    for path, mode, oid in staged():
        rules = scan_path(path)
        findings.extend(f"  {rule:<15} {masked(path)}" for rule in rules)
        is_gitlink = mode == "160000"
        if not rules and not is_gitlink and extension(path) not in MEDIA_EXT:
            to_read.append((path, oid))
    for (path, _), blob in zip(to_read, blobs([oid for _, oid in to_read])):
        text = decode_text(blob)
        if text is not None:
            findings.extend(f"  {rule:<15} {path}:{line}" for rule, line in scan_text(path, text))
    if not findings:
        return 0
    print("pre-commit: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.",
          file=sys.stderr)
    print("\n".join(findings), file=sys.stderr)
    print("Unstage or fix these. A false positive is fixed in tools/hygiene/precommit.py, "
          "never bypassed.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (subprocess.CalledProcessError, OSError, ValueError) as error:
        print(f"pre-commit: the hygiene scan failed, so the commit is blocked: {error}",
              file=sys.stderr)
        sys.exit(2)
