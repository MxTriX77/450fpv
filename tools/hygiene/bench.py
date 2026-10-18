"""Benchmark of the hygiene scan: 200 staged text files and a 1,000-file reference/ (SEC-06).

The repo-hygiene spec bounds the scan itself, Python start-up excluded, at 2 s for 200 staged
text files. Wall time is no unit test: other agents load the machine and battery clocks are
not evidence (TST-03, PLT-07). So the number lives here, with its limit, its margin and the
power state, and counts only on AC power. The suite checks that this workload scans clean.

    python tools/hygiene/bench.py [--runs N]
"""
import argparse
import os
import statistics
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import precommit  # noqa: E402

LIMIT_S = 2.0  # the spec's literal budget
FILES, REFERENCE_FILES = 200, 1000
STEM = "qz-bench-probe-07"  # the fake reference/'s distinctive stem
# Windows 11 power modes (the overlay on the power plan).
MODES = {
    "961cc777-2547-4f9d-8174-7d86181b8a7a": "best power efficiency",
    "00000000-0000-0000-0000-000000000000": "balanced",
    "3af9b8d9-7c97-431d-ad78-34a8bfea439f": "better performance",
    "ded574b5-45a0-4f42-8737-46345c09c238": "best performance",
}


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


def git(repo, *args):
    env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
    return subprocess.run(["git", *args], cwd=repo, env=env, capture_output=True, text=True,
                          check=True).stdout


def stage_workload(repo):
    """Plant the reference/ folder, write and stage the clean files; return the staged paths."""
    (repo / "reference").mkdir(parents=True, exist_ok=True)
    for i in range(REFERENCE_FILES):
        (repo / "reference" / f"{STEM}-{i:04d}.jpg").write_bytes(b"\xff\xd8\xff")
    for rel, text in clean_tree(FILES).items():
        (repo / rel).parent.mkdir(parents=True, exist_ok=True)
        (repo / rel).write_text(text, encoding="utf-8", newline="\n")
    git(repo, "add", "--", "unity", "tools", "docs")
    return git(repo, "diff", "--cached", "--name-only").split()


def time_scans(repo, runs):
    """Wall time of each scan of the staged files, after one warm-up scan.

    The scan runs in this process, so the Python start-up and the module import (about 0.1 s)
    are excluded, as the spec counts it.
    """
    home = os.getcwd()
    os.chdir(repo)
    try:
        times = []
        for _ in range(runs + 1):
            start = time.perf_counter()
            blocked = precommit.main([])
            times.append(time.perf_counter() - start)
            if blocked:
                raise SystemExit("the benchmark's workload must scan clean")
    finally:
        os.chdir(home)
    return times[1:]


def power():
    """The power source, plan, mode and CPU clock, printed next to every number (PLT-07)."""
    if sys.platform != "win32":
        return "power state unknown"
    import ctypes
    status = (ctypes.c_ubyte * 12)()  # SYSTEM_POWER_STATUS; byte 0 is ACLineStatus
    ctypes.windll.kernel32.GetSystemPowerStatus(status)
    source = {0: "battery, not evidence (PLT-07)", 1: "AC"}.get(status[0], "unknown source")
    plan = subprocess.run(["powercfg", "/getactivescheme"], capture_output=True, text=True).stdout
    plan = plan[plan.find("(") + 1:plan.rfind(")")] or "unknown plan"
    overlay = (ctypes.c_ubyte * 16)()
    mode = "unknown mode"
    if ctypes.windll.powrprof.PowerGetEffectiveOverlayScheme(overlay) == 0:
        mode = MODES.get(str(uuid.UUID(bytes_le=bytes(overlay))), "unknown mode")
    cpus = os.cpu_count()
    info = (ctypes.c_ulong * (6 * cpus))()  # PROCESSOR_POWER_INFORMATION per logical CPU
    ctypes.windll.powrprof.CallNtPowerInformation(11, None, 0, info, ctypes.sizeof(info))
    clock = f"CPU {max(info[2::6])}/{max(info[1::6])} MHz"
    return f"{source}, {plan} plan, {mode} mode, {clock}"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--runs", type=int, default=30, help="measured scans after one warm-up (30)")
    args = parser.parse_args(argv)
    for key in [k for k in os.environ if k.startswith("GIT_")]:
        del os.environ[key]  # the in-process scan must see the throwaway repo, not a hook's index
    with tempfile.TemporaryDirectory(prefix="hygiene-bench-") as tmp:
        repo = Path(tmp)
        git(repo, "init", "-q")
        staged = stage_workload(repo)
        times = time_scans(repo, args.runs)
    worst = max(times)
    print(f"hygiene scan, {len(staged)} staged files, {REFERENCE_FILES}-file reference/: "
          f"median {statistics.median(times):.3f} s, worst {worst:.3f} s of {args.runs}; "
          f"limit {LIMIT_S:.1f} s, margin {LIMIT_S - worst:.3f} s; {power()}")
    return 0 if worst < LIMIT_S else 1


if __name__ == "__main__":
    sys.exit(main())
