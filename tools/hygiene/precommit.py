"""OPSEC and hygiene scan of the staged files and the commit message (SEC-01, SEC-02, SEC-06).

Run by .githooks/pre-commit (the index, never the working tree) and by .githooks/commit-msg
(`--commit-msg <file>`, which also enforces the commit format of AGENTS.md, PRC-03).
`--tree <rev>` scans every file of a commit or tree, for the check that the existing tree is
clean. Exit 1 blocks the commit. Findings print the rule id and where it hit, never the
matched text, so the hook's own output can't leak what it caught. A false positive is fixed
in the rule, never bypassed.
"""
import argparse
import base64
import os
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
# Text files whose prose may name files: footage names inside them are blocked.
NAME_TEXT_EXT = {".md", ".txt", ".yaml", ".yml", ".json"}

# Exempting prefixes match case-sensitively and scoping prefixes case-insensitively,
# so a case slip always makes the scan stricter, never looser.
MEDIA_ROOTS = ("assets-src/", "unity/Assets/")
COORDINATE_ROOTS = ("docs/", "openspec/")
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
_VIDEO_EXT = "|".join(sorted((re.escape(e[1:]) for e in FOOTAGE_EXT), key=len, reverse=True))
# The same names inside text, at a token start, and any <name>.<video ext> token. A bare
# pattern such as *.mp4 has no name character before the dot, so it passes.
FOOTAGE_TEXT = re.compile(
    rf"(?<![^\W_])(?:{FOOTAGE_NAME.pattern})|[\w-]\.(?:{_VIDEO_EXT})(?!\w)", re.IGNORECASE
)

# Messenger names, Latin and Cyrillic, one per line, base64 so this file never holds them in
# plain text. Names that are also ordinary words appear only in unambiguous forms (a domain,
# or the name followed by "messenger" or "app").
_MESSENGER_B64 = (
    "dGVsZWdyYW0KdGVsZWdyYS5waAp0Lm1lLwp3aGF0c2FwcAp3aGF0cyBhcHAKd2EubWUvCnZp"
    "YmVyCmRpc2NvcmQuY29tCmRpc2NvcmQuZ2cKZGlzY29yZCBhcHAKZGlzY29yZCBtZXNzZW5n"
    "ZXIKc2t5cGUKd2VjaGF0CnRocmVlbWEKaW1lc3NhZ2UKc25hcGNoYXQKa2FrYW90YWxrCmZh"
    "Y2Vib29rIG1lc3NlbmdlcgpmYiBtZXNzZW5nZXIKbGluZSBtZXNzZW5nZXIKc2lnbmFsIG1l"
    "c3NlbmdlcgpzaWduYWwub3JnCnNpZ25hbC5tZQrRgtC10LvQtdCz0YDQsNC8CtCy0LDRgtGB"
    "0LDQvwrQstCw0YbQsNC/CtCy0L7RgtGB0LDQvwrQstC+0YbQsNC/CtCy0LDQudCx0LXRgArQ"
    "tNC40YHQutC+0YDQtArRgdC60LDQudC/CtCy0ZbRh9Cw0YIK0LLQuNGH0LDRggrRgdC40LPQ"
    "vdCw0Lsg0LzQtdGB0LXQvdC00LbQtdGACtGB0LjQs9C90LDQuyDQvNC10YHRgdC10L3QtNC2"
    "0LXRgA=="
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

# Coordinates count only inside the regional box (Ukraine and its neighbours, with margin),
# which keeps physics coefficients, colours and shader constants out.
LAT_RANGE, LON_RANGE = (40, 60), (15, 50)
ANY_RANGE = (15, 60)  # a value with no hemisphere letter may be either axis

_NUM = r"[-+]?\d{1,3}[.,]\d{4,}"  # a point or a decimal comma, at least 4 places
_HEMI = r"[NSEWnsew]"
# Either axis order, hemisphere letters before or after. The second number sits in a
# lookahead, so it can also start the next pair (lat, lon, alt).
DECIMAL_PAIR = re.compile(
    rf"(?<![\d.,+-])({_NUM})(?=\s*°?\s*(?:{_HEMI}(?!{_LETTER}))?\s*(?:[,;/]|\s)\s*"
    rf"(?:{_HEMI}\s*)?({_NUM})(?!\.?\d))"
)
PREFIXED_DECIMAL = re.compile(rf"(?<!{_LETTER})([NSEW])\s?(\d{{1,3}}[.,]\d{{4,}})(?!\.?\d)")
_DM = r"(\d{1,3})\s*°\s*\d{1,2}(?:[.,]\d+)?\s*['′’]"
_SEC = r"\d{1,2}(?:[.,]\d+)?\s*(?:[\"″”]|''|′′)"
DMS = re.compile(
    rf"(?<!\d){_DM}\s*(?:{_SEC}\s*)?({_HEMI})(?!{_LETTER})"  # D°M′(S″) and a letter
    rf"|(?<!{_LETTER})([NSEW])\s*{_DM}"                      # a letter, then D°M′
    rf"|(?<!\d){_DM}\s*{_SEC}"                               # D°M′S″ with no letter
)
# Grid zones 33-39 and latitude bands T-V are the ones that overlap the box.
MGRS = re.compile(
    r"(?<!\w)3[3-9][TUV]\s?[A-HJ-NP-Z][A-HJ-NP-V]\s?(?:(\d{1,5})\s(\d{1,5})|(\d{2,10}))(?!\w)",
    re.IGNORECASE,
)
# A key, a ":" or "=", and a number: JSON, YAML, INI, code and URL query forms.
LABELLED_COORDINATE = re.compile(
    rf"(?<!{_LETTER})(?:lat(?:itude)?|lon(?:gitude)?|lng)(?!{_LETTER})[\"']?\s*[:=]\s*[\"']?[-+]?\d",
    re.IGNORECASE,
)

_DAY = r"(?:0?[1-9]|[12]\d|3[01])"
_MONTH = r"(?:0?[1-9]|1[0-2])"
_YEAR = r"(?:19|20)\d\d"
# Month names, English (and abbreviations), Ukrainian and Russian, in the cases dates use.
MONTH_WORDS = {
    "january", "february", "march", "april", "may", "june", "july", "august", "september",
    "october", "november", "december",
    "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec",
    *(stem + end for stem in ("січ", "берез", "квіт", "трав", "черв", "лип", "серп", "верес",
                              "жовт", "груд") for end in ("ень", "ня", "ні")),
    "лютий", "лютого", "лютому", "листопад", "листопада", "листопаді",
    *(stem + end for stem in ("январ", "феврал", "апрел", "июн", "июл", "сентябр", "октябр",
                              "ноябр", "декабр") for end in "ьяе"),
    "март", "марта", "марте", "август", "августа", "августе", "май", "мая", "мае",
}
_MONTH_NAME = "(?:" + "|".join(sorted(MONTH_WORDS, key=len, reverse=True)) + rf")(?!{_LETTER})\.?"
DATE = re.compile(
    rf"(?<!\d){_YEAR}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])(?!\d)"                   # ISO
    rf"|(?<![\d.]){_YEAR}\.{_MONTH}\.{_DAY}(?![^\W_]|[./]\d)"                             # Y.M.D
    rf"|(?<![\d/]){_YEAR}/{_MONTH}/{_DAY}(?![^\W_]|[./]\d)"                               # Y/M/D
    rf"|(?<![\d.]){_DAY}\.{_MONTH}\.{_YEAR}(?!\.?\d)"                                    # D.M.Y
    r"|(?<![\d.])(?:0[1-9]|[12]\d|3[01])\.(?:0[1-9]|1[0-2])\.\d\d(?!\.?\d)"               # D.M.YY
    rf"|(?<![\d/]){_DAY}/{_DAY}/{_YEAR}(?![\d/])"                                        # D/M/Y, M/D/Y
    rf"|(?<![^\W_]){_YEAR}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])(?![^\W_])"            # YYYYMMDD
    rf"|(?<![\d.]){_DAY}(?:st|nd|rd|th)?[\s-]+(?:of\s+)?{_MONTH_NAME}"                   # D Month
    rf"|(?<!{_LETTER}){_MONTH_NAME}\s+{_DAY}(?:st|nd|rd|th)?(?![^\W_])"                  # Month D
    rf"|(?<!{_LETTER}){_MONTH_NAME},?\s+{_YEAR}(?!\d)",                                  # Month YYYY
    re.IGNORECASE,
)
DECISION = re.compile(r"D-\d{3}")
# A date that directly follows an attribution to the pilot: "(the pilot, <date>)",
# "**What the pilot said** (<date>", "**Correction from the pilot** (<date>".
PILOT_ATTRIBUTION = re.compile(rf"(?<!{_LETTER})pilot(?:\s+said)?\**(?:,\s*|\s*\(\s*)$", re.IGNORECASE)

# Whole-file prefilters, each matching a superset of its line rule, so most files skip the
# per-line pass (the lookbehinds above are slow when run at every position of every line).
MESSENGER_HEADS = sorted({t.split(" ")[0].lower() for t in MESSENGER_TERMS})
COORDINATES_HINT = re.compile(r"\d[.,]\d{4}|°|3[3-9][TUVtuv]\s?[A-Za-z]{2}")
YEAR_HINT = re.compile(r"(?:19|20)\d\d|\d\.\d\d\.\d\d")
WORD = re.compile(r"[^\W\d_]+")

SCISSORS = re.compile(r"^\S -{24} >8 -{24}\r?$", re.MULTILINE)

# The commit format of AGENTS.md: `type(scope): summary` or `type: summary`, subject only.
COMMIT_TYPES = ("feat", "fix", "perf", "refactor", "test", "docs", "chore", "art")
SUBJECT_LIMIT = 72
SUBJECT = re.compile(r"^(?:" + "|".join(COMMIT_TYPES) + r")(?:\([^\s()]+\))?: (\S.*)$")
# What git's fmt-merge-msg writes: `Merge branch 'x'` (with ` into y` or ` of <url>`),
# `Merge branches 'a' and 'b'`, `Merge remote-tracking branch 'origin/x'`, `Merge tag 'v1'`,
# `Merge commit '<sha>'`, their plurals and mixes; and GitHub's `Merge pull request #N from`.
MERGE_SUBJECT = re.compile(r"^Merge (?:(?:remote-tracking )?branch(?:es)?|tags?|commits?) '"
                           r"|^Merge pull request #\d+ from ")


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


def _within(value, bounds):
    return bounds[0] <= value <= bounds[1]


def _axis(letter):
    """The range a value may take: latitude for N/S, longitude for E/W, either without one."""
    if not letter:
        return ANY_RANGE
    return LAT_RANGE if letter.upper() in "NS" else LON_RANGE


def _number(text):
    return float(text.replace(",", "."))


def in_box(a, b):
    """Whether (a, b) is a point in the regional box, in either axis order."""
    return ((_within(a, LAT_RANGE) and _within(b, LON_RANGE))
            or (_within(a, LON_RANGE) and _within(b, LAT_RANGE)))


def coordinates(line):
    for m in DECIMAL_PAIR.finditer(line):
        if in_box(_number(m.group(1)), _number(m.group(2))):
            return True
    for m in PREFIXED_DECIMAL.finditer(line):
        if _within(_number(m.group(2)), _axis(m.group(1))):
            return True
    for m in DMS.finditer(line):
        degrees = m.group(1) or m.group(4) or m.group(5)
        if _within(int(degrees), _axis(m.group(2) or m.group(3))):
            return True
    for m in MGRS.finditer(line):
        east, north, run = m.groups()
        if (run and len(run) % 2 == 0) or (east and len(east) == len(north)):
            return True
    return False


def labelled_coordinate(line):
    return LABELLED_COORDINATE.search(line) is not None


def notes_date(line):
    if DECISION.search(line):
        return False
    return any(PILOT_ATTRIBUTION.search(line, 0, m.start()) is None for m in DATE.finditer(line))


def footage_text(line):
    return FOOTAGE_TEXT.search(line) is not None


# --- the commit format (the whole message, not a line) ----------------------------------------

def commit_format(text):
    """(line number, reason) for every way the message breaks the commit format of AGENTS.md.

    The subject is the first non-blank line, whatever it starts with (`git commit -m` keeps a
    `#` line, so it is not a comment there): at most 72 characters, `type(scope): summary`
    with a known type, the summary not starting with an upper-case letter, and no non-empty
    line after it (a body, a trailer, a co-author line). The `#` lines after the subject are
    git's editor template and are ignored. Git's own merge commits (MERGE_SUBJECT) are exempt.
    """
    lines = [(number, line.rstrip()) for number, line in enumerate(text.split("\n"), 1)]
    lines = [(number, line) for number, line in lines if line]
    if not lines:
        return [(1, "the message has no subject")]
    (number, subject), rest = lines[0], [(n, line) for n, line in lines[1:] if not line.startswith("#")]
    if MERGE_SUBJECT.match(subject):
        return []
    findings = []
    if len(subject) > SUBJECT_LIMIT:
        findings.append((number, f"the subject is {len(subject)} characters, the limit is {SUBJECT_LIMIT}"))
    match = SUBJECT.match(subject)
    if not match:
        findings.append((number, "the subject must read type(scope): summary or type: summary, "
                                 "type one of " + ", ".join(COMMIT_TYPES)))
    elif match.group(1)[0].isupper():
        findings.append((number, "the summary must not start with an upper-case letter"))
    if rest:
        findings.append((rest[0][0], "nothing may follow the subject: no body, no trailer, no co-author line"))
    return findings


def messenger_hint(text):
    lowered = text.lower()
    return any(head in lowered for head in MESSENGER_HEADS)


def date_hint(text):
    """Every date holds a year, a two-digit-year dotted date or a month word."""
    return YEAR_HINT.search(text) is not None or not MONTH_WORDS.isdisjoint(WORD.findall(text.lower()))


def _hint(pattern):
    return lambda text: pattern.search(text) is not None


class ReferenceStems:
    """Stems of the local reference files, matched as whole tokens in any case.

    Each stem is indexed by its longest letter-and-digit run, which must then appear as a
    whole token of the text, so the per-file cost does not grow with the number of stems.
    The stems live in memory only and are never printed.
    """

    RUN = re.compile(r"[^\W_]+")

    def __init__(self, stems):
        self.by_run = {}
        for stem in stems:
            runs = self.RUN.findall(stem.lower())
            if runs:
                self.by_run.setdefault(max(runs, key=len), []).append(stem)
        self.pattern = None

    def hint(self, text):
        tokens = set(self.RUN.findall(text.lower()))
        found = [s for key in tokens & self.by_run.keys() for s in self.by_run[key]]
        if not found:
            return False
        alternatives = "|".join(re.escape(s) for s in sorted(found, key=len, reverse=True))
        self.pattern = re.compile(rf"(?<![^\W_])(?:{alternatives})(?![^\W_])", re.IGNORECASE)
        return self.pattern.search(text) is not None

    def check(self, line):
        return self.pattern.search(line) is not None


def distinctive_stem(name):
    """The file's stem if it is distinctive enough to be a name rather than a word, else None.

    Footage, photos and audio count from 8 characters with a digit, "_", "-" or a space.
    Other files (the scripts and data the analysis keeps next to the footage) count only
    with a digit: their identifier-like names are tool vocabulary the docs cite.
    """
    stem, ext = os.path.splitext(name)
    shape = r"[\d_\- ]" if ext.lower() in MEDIA_EXT else r"\d"
    return stem if len(stem) >= 8 and re.search(shape, stem) else None


def reference_stems():
    """Distinctive stems of the files under reference/, in this worktree and the main checkout.

    Agents commit from linked worktrees, whose own reference/ is absent: the footage sits in
    the main checkout, next to the shared .git folder. The folder is walked at scan time.
    Folders directly under reference/ whose name starts with "_" (_frames, _compare, ...) hold
    the reference tools' own letter-named outputs, not the pilot's files, and are skipped.
    """
    out = git("rev-parse", "--path-format=absolute", "--show-toplevel", "--git-common-dir")
    top, common = out.decode("utf-8").splitlines()
    stems = set()
    for folder in {os.path.join(top, "reference"), os.path.join(os.path.dirname(common), "reference")}:
        for root, dirs, names in os.walk(folder):
            if root == folder:
                dirs[:] = [d for d in dirs if not d.startswith("_")]
            stems.update(filter(None, map(distinctive_stem, names)))
    return stems


# --- scopes: (path, or None for the commit message) -> bool -----------------------------------

def everywhere(path):
    return True


def name_text_scope(path):
    return path is None or extension(path) in NAME_TEXT_EXT


def coordinates_scope(path):
    if path is None:
        return True
    return path.lower().startswith(COORDINATE_ROOTS) or ("/" not in path and extension(path) == ".md")


def notes_scope(path):
    return path is not None and path.lower().startswith(NOTES_ROOTS)


PATH_RULES = (
    ("footage-ext", footage_ext),
    ("reference-path", reference_path),
    ("footage-name", footage_name),
    ("media-location", media_location),
)
# (rule id, scope, whole-file hint, line check)
LINE_RULES = (
    ("messenger", everywhere, messenger_hint, messenger),
    ("coordinates", coordinates_scope, COORDINATES_HINT.search, coordinates),
    ("labelled-coordinate", everywhere, _hint(LABELLED_COORDINATE), labelled_coordinate),
    ("notes-date", notes_scope, date_hint, notes_date),
    ("footage-text", name_text_scope, _hint(FOOTAGE_TEXT), footage_text),
)


def line_rules(stems):
    """LINE_RULES plus reference-stem when the local reference/ folder holds any stem."""
    if not stems.by_run:
        return LINE_RULES
    return LINE_RULES + (("reference-stem", everywhere, stems.hint, stems.check),)


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


def scan_text(path, text, rules=LINE_RULES):
    """(rule id, line number) for every line that breaks a line rule in scope for path."""
    hits = []
    for rule, scope, hint, check in rules:
        if scope(path) and hint(text):
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


def tree_entries(rev):
    """(path, mode, blob id) for every file of a commit or tree."""
    entries = []
    for record in git("ls-tree", "-r", "-z", "--full-tree", rev).split(b"\0"):
        if record:
            meta, path = record.split(b"\t", 1)
            mode, _, oid = meta.decode("ascii").split(" ")
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


def scan_entries(entries, rules):
    """Findings for (path, mode, blob id) entries: the path rules, then the line rules."""
    findings = []
    to_read = []
    for path, mode, oid in entries:
        hit = scan_path(path)
        findings.extend(f"  {rule:<19} {masked(path)}" for rule in hit)
        is_gitlink = mode == "160000"
        if not hit and not is_gitlink and extension(path) not in MEDIA_EXT:
            to_read.append((path, oid))
    for (path, _), blob in zip(to_read, blobs([oid for _, oid in to_read])):
        text = decode_text(blob)
        if text is not None:
            findings.extend(f"  {rule:<19} {path}:{line}" for rule, line in scan_text(path, text, rules))
    return findings


def scan_message(path, rules):
    """Findings for a commit message file, up to git's scissors line (commit -v puts the diff below)."""
    with open(path, "rb") as file:
        text = file.read().decode("utf-8", "replace")
    cut = SCISSORS.search(text)
    if cut:
        text = text[:cut.start()]
    findings = [f"  {rule:<19} commit message:{line}" for rule, line in scan_text(None, text, rules)]
    findings += [f"  {'commit-format':<19} commit message:{line} ({reason})"
                 for line, reason in commit_format(text)]
    return findings


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--commit-msg", metavar="FILE", help="scan a commit message (commit-msg hook)")
    mode.add_argument("--tree", metavar="REV", help="scan every file of a commit or tree")
    args = parser.parse_args(argv)
    rules = line_rules(ReferenceStems(reference_stems()))
    if args.commit_msg:
        hook, findings = "commit-msg", scan_message(args.commit_msg, rules)
    elif args.tree:
        entries = tree_entries(args.tree)
        hook, findings = "tree scan", scan_entries(entries, rules)
        print(f"{len(entries)} files scanned, {len(findings)} findings")
    else:
        hook, findings = "pre-commit", scan_entries(staged(), rules)
    if not findings:
        return 0
    print(f"{hook}: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.",
          file=sys.stderr)
    print("\n".join(findings), file=sys.stderr)
    print("Unstage or fix these. A false positive is fixed in tools/hygiene/precommit.py, "
          "never bypassed.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (subprocess.CalledProcessError, OSError, ValueError) as error:
        print(f"hygiene scan failed, so the commit is blocked: {error}", file=sys.stderr)
        sys.exit(2)
