"""Per-flight table and totals for the review (p7d-tether 1.11h). Reads the triage index (by tag and seed only), the
probe's disc and snap files and analyze.py's per-flight reports. Usage: python summary.py <out dir> <lay dir> <rep dir>"""
import math
import os
import re
import sys
from collections import Counter, defaultdict

out, lay, rep = sys.argv[1], sys.argv[2], sys.argv[3]
rows = []
for line in open(os.path.join(out, "triage.log"), encoding="utf-8"):
    m = re.match(r"(f\d+) seed (0x[0-9A-F]+) model (\d+) world (\S+) wind (\S+) rain (\S+) payload (\S+) kg", line)
    if not m:
        continue
    tag = m.group(1)
    time = re.search(r"time (\d+:\d+\.\d)", line).group(1)
    paid = re.search(r"paid ([\d.]+) m", line).group(1)
    brk = re.search(r"\| (fiber broke at .*)$", line)
    brk = brk.group(1).strip() if brk else "whole"
    r = {"tag": tag, "seed": m.group(2), "wind": m.group(5), "rain": m.group(6), "payload": m.group(7), "time": time, "paid": paid, "break": brk}
    # places on buildings
    places, left, high, high_left, maxmove = 0, 0, 0, 0, 0.0
    rp = os.path.join(rep, tag + ".lay.txt")
    if os.path.exists(rp):
        cur = None
        for l in open(rp, encoding="utf-8"):
            m2 = re.match(r"-- L\d+: t ([\d.]+) s, s ([\d.]+) m, (\S+) shape \S+ \((\S+).*agl ([-\d.]+)", l)
            if m2:
                places += 1
                cur = float(m2.group(5))
                if cur > 2:
                    high += 1
            if "LEFT at" in l:
                left += 1
                if cur is not None and cur > 2:
                    high_left += 1
            m3 = re.match(r"   end \([\d.]+ s\): line ([\d.]+) m away", l)
            if m3:
                maxmove = max(maxmove, float(m3.group(1)))
    r.update(places=places, left=left, high=high, high_left=high_left, maxmove=maxmove)
    # through buildings
    thru_n, thru_deep, thru_max = 0, 0, 0.0
    sp = os.path.join(lay, tag + ".snap.txt")
    steps_thru = set()
    for l in open(sp, encoding="utf-8"):
        if "THRU" not in l or not l.startswith("N "):
            continue
        for m4 in re.finditer(r"THRU (\d+)/(\d+):(\S+) in ([\d.]+) m", l):
            if re.match(r"(Ruin_|Debris_|Door|Window)", m4.group(3)):
                v = float(m4.group(4))
                thru_n += 1
                thru_max = max(thru_max, v)
                if v > 0.05:
                    thru_deep += 1
                    steps_thru.add(int(l.split()[1]))
    r.update(thru_n=thru_n, thru_deep=thru_deep, thru_max=thru_max, thru_secs=len(steps_thru) / 10.0)
    # discs
    eps = []
    for l in open(os.path.join(lay, tag + ".disc.txt"), encoding="utf-8"):
        m5 = re.match(r"(\w\w) meet from (\d+) .*?\((\d+) steps in the slab\), (CUT at (\d+) .*?(\d+) ms after the first|no cut); resting (\d) spinning (\d) least r (\S+);(?: steps within a blade's reach (\d+), a blade passing (\d+), both (\d+), rotor resting (\d+), longest blade ([\d.]+) R;)?", l)
        if m5:
            eps.append({"rotor": m5.group(1), "start": int(m5.group(2)), "steps": int(m5.group(3)), "cut": m5.group(5) is not None,
                        "ms": int(m5.group(6)) if m5.group(6) else None, "reach": int(m5.group(10)) if m5.group(10) else None,
                        "pass": int(m5.group(11)) if m5.group(11) else None, "both": int(m5.group(12)) if m5.group(12) else None,
                        "rest": int(m5.group(13)) if m5.group(13) else None, "tip": float(m5.group(14)) if m5.group(14) else None})
    r["eps"] = eps
    rows.append(r)

print("| # | seed | air | payload | time | paid out | line | places on ruins (high) | left (high) | farthest at the end | chord through a ruin, s (deepest) | disc episodes: cut after / no cut |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
tot = Counter()
for r in rows:
    cuts = [f"{e['rotor']} {e['ms']} ms" for e in r["eps"] if e["cut"]]
    nocut = [e for e in r["eps"] if not e["cut"]]
    nc = f"; {len(nocut)} without a cut" if nocut else ""
    air = "Calm, Sunny" if r["wind"] == "Calm" else f"{r['wind']} wind, {r['rain']}"
    print(f"| {r['tag'][1:]} | {r['seed']} | {air} | {r['payload']} kg | {r['time']} | {r['paid']} m | {r['break'].replace('fiber broke at ', 'broke ')} | {r['places']} ({r['high']}) | {r['left']} ({r['high_left']}) | {r['maxmove']:.2f} m | {r['thru_secs']:.1f} ({r['thru_max']:.2f} m) | {', '.join(cuts) if cuts else '-'}{nc} |")
    tot["places"] += r["places"]; tot["left"] += r["left"]; tot["high"] += r["high"]; tot["high_left"] += r["high_left"]
    tot["thru_flights"] += 1 if r["thru_deep"] else 0
    tot["thru_secs"] += r["thru_secs"]
    for e in r["eps"]:
        tot["eps"] += 1
        if e["cut"]:
            tot["cuts"] += 1
            tot[f"cut_{e['ms']}ms"] += 1
        else:
            if e["reach"] == 0:
                tot["nocut_noreach"] += 1
            elif e["both"] == 0 and e["rest"] and e["rest"] == e["steps"]:
                tot["nocut_resting"] += 1
            else:
                tot["nocut_other"] += 1
print()
print(dict(tot))
print()
for r in rows:
    for e in r["eps"]:
        if not e["cut"]:
            print(f"{r['tag']} {r['seed']} {e['rotor']} from {e['start'] / 1000:.3f} s, {e['steps']} steps: reach {e['reach']}, pass {e['pass']}, both {e['both']}, resting {e['rest']}, longest blade {e['tip']} R")
