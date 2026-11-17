"""QA forensics of the laid line (p7d-tether 1.11h): reads the probe's per-flight files (lay/Program.cs) and reports,
per flight, every stretch of line laid on a building (a roof's piece, a wall's top, a corner) and where it was 5, 15
and 30 s later and at the end, what moved it, the corners' tensions and the discs' episodes.

Usage: python analyze.py <out/lay dir> <report dir> [tags...]
Flights are named by the probe's tag (fNN) and seed only."""
import math
import os
import re
import sys
from collections import defaultdict

from vec3 import np

BUILDING = re.compile(r"^(Ruin_|Debris_|Door|Window)")
VEC = r"\(([-\d.eE+]+),([-\d.eE+]+),([-\d.eE+]+)\)"
TAB, NL = chr(9), chr(10)
MOVED = 0.25  # m: the line no longer within this of where the stretch lay


def vec(s):
    m = re.match(VEC, s)
    return np.array([float(m.group(1)), float(m.group(2)), float(m.group(3))])


def parse_slot(txt):
    txt = txt.strip()
    if txt == "-" or not txt:
        return None
    parts = txt.split()
    member = parts[0]
    d = {"member": member}
    m = re.search(r" N ([-\d.eE+]+) gap ([-\d.eE+]+) n " + VEC + r" edge ([-\d.eE+]+) cap (\d) sliding (\d) slid (\d) mu ([-\d.eE+]+)", txt)
    if m:
        d.update(N=float(m.group(1)), gap=float(m.group(2)), n=np.array([float(m.group(3)), float(m.group(4)), float(m.group(5))]),
                 edge=float(m.group(6)), cap=int(m.group(7)), sliding=int(m.group(8)), slid=int(m.group(9)), mu=float(m.group(10)))
    mm = re.match(r"(Shape|Snag|Element|Wire|Layer|Hook|Stem):(-?\d+)/(-?\d+):(.*)", member)
    if mm:
        d.update(kind=mm.group(1), obj=int(mm.group(2)), shape=int(mm.group(3)), name=mm.group(4))
    else:
        d.update(kind=member.split(":")[0], obj=None, shape=None, name="")
    return d


def building(slot):
    return slot is not None and slot.get("obj") is not None and BUILDING.match(slot["name"] or "") is not None


def read_snaps(path):
    snaps = []  # (step, state dict, nodes list)
    cur = None
    with open(path, encoding="utf-8") as f:
        f.readline()
        for line in f:
            if line.startswith("S "):
                p = line.split()
                st = {"step": int(p[1]), "t": float(p[3])}
                st["drone"] = vec(p[5])
                st["agl"] = float(p[7])
                st["v"] = vec(p[9])
                m = re.search(r"exitT ([-\d.eE+]+) paid ([-\d.eE+]+) nodes (\d+) trail (\d+) thaws (\d+) broken (\d) wind " + VEC, line)
                st.update(exitT=float(m.group(1)), paid=float(m.group(2)), nodes=int(m.group(3)), trail=int(m.group(4)),
                          broken=int(m.group(6)), wind=np.array([float(m.group(7)), float(m.group(8)), float(m.group(9))]))
                m = re.search(r"stk T ([-\d.]+) P ([-\d.]+) R ([-\d.]+) Y ([-\d.]+)", line)
                st["stick"] = tuple(float(m.group(i)) for i in range(1, 5))
                cur = (st["step"], st, [])
                snaps.append(cur)
            elif line.startswith("N "):
                segs = line.rstrip("\n").split(" | ")
                p = segs[0].split()
                nd = {"i": int(p[2]), "s": float(p[4]), "x": vec(p[6]), "agl": float(p[8]), "v": float(p[10])}
                m = re.search(r" T ([-\d.eE+]+) rest ([-\d.eE+]+) len ([-\d.eE+]+) rest ([-\d.eE+]+)", segs[0])
                if m:
                    nd.update(T=float(m.group(1)), seg_rest=float(m.group(2)), seg_len=float(m.group(3)), rest=float(m.group(4)))
                else:
                    nd.update(T=None, rest=float(re.search(r"rest ([-\d.eE+]+)", segs[0]).group(1)))
                nd["anchor"] = " anchor" in segs[0]
                nd["lifted"] = " lifted" in segs[0]
                m = re.search(r"drag " + VEC + "mN", segs[0])
                nd["drag"] = np.array([float(m.group(i)) for i in (1, 2, 3)]) if m else np.zeros(3)
                m = re.search(r" air " + VEC, segs[0])
                nd["air"] = np.array([float(m.group(i)) for i in (1, 2, 3)]) if m else np.zeros(3)
                nd["slots"] = [parse_slot(segs[1]) if len(segs) > 1 else None, parse_slot(segs[2]) if len(segs) > 2 else None]
                nd["thru"] = segs[3] if len(segs) > 3 and segs[3].startswith("THRU") else None
                cur[2].append(nd)
    return snaps


def read_trail(path):
    ops = []
    head = None
    with open(path, encoding="utf-8") as f:
        head = f.readline().strip()
        for line in f:
            p = line.split()
            if not p or not p[0].isdigit():
                continue
            step, op = int(p[0]), p[1]
            if op == "freeze":
                segs = line.rstrip("\n").split(" | ")
                m = re.search(r"idx (\d+) s ([-\d.]+) at " + VEC + r" agl ([-\d.eE+]+)", segs[0])
                ops.append({"step": step, "op": "freeze", "idx": int(m.group(1)), "s": float(m.group(2)),
                            "x": np.array([float(m.group(i)) for i in (3, 4, 5)]), "agl": float(m.group(6)),
                            "slots": [parse_slot(segs[1]), parse_slot(segs[2]) if len(segs) > 2 else None],
                            "thru": segs[3] if len(segs) > 3 else None, "line": line.strip()})
            elif op == "lift":
                ops.append({"step": step, "op": "lift", "idx": int(p[3]), "why": line.split(" | ", 1)[1].strip(), "line": line.strip()})
            elif op == "thaw":
                ops.append({"step": step, "op": "thaw", "depth": int(p[3]), "line": line.strip()})
            elif op == "breakpush":
                m = re.search(r"idx (\d+) s ([-\d.]+) at " + VEC, line)
                ops.append({"step": step, "op": "push", "idx": int(m.group(1)), "s": float(m.group(2)),
                            "x": np.array([float(m.group(i)) for i in (3, 4, 5)])})
            elif op == "BREAK":
                ops.append({"step": step, "op": "break", "line": line.strip()})
    return head, ops


class Trail:
    """The trail stack replayed from the probe's operations."""

    def __init__(self, ops):
        self.ops = ops
        self.k = 0
        self.pts = []  # (s, x)
        self.bad = 0

    def advance(self, step):
        while self.k < len(self.ops) and self.ops[self.k]["step"] <= step:
            o = self.ops[self.k]
            if o["op"] in ("freeze", "push"):
                if o["idx"] != len(self.pts):
                    self.bad += 1
                    del self.pts[o["idx"]:]
                self.pts.append((o["s"], o["x"]))
            elif o["op"] == "lift":
                if o["idx"] != len(self.pts) - 1:
                    self.bad += 1
                self.pts.pop()
            elif o["op"] == "thaw":
                for _ in range(o["depth"] + 1):
                    self.pts.pop()
            self.k += 1


def polyline(trail_pts, nodes, broken):
    """The line's points by material: the trail, then the chain from its node 1 (node 0 is the trail's end)."""
    s = [p[0] for p in trail_pts]
    x = [p[1] for p in trail_pts]
    if not broken:
        for nd in nodes[1:] if trail_pts else nodes:
            s.append(nd["s"])
            x.append(nd["x"])
    return s, x


def dist_to_polyline(p, X):
    if len(X) < 2:
        return (math.dist(X[0], p) if len(X) else math.inf), 0
    best, bj = math.inf, 0
    px, py, pz = p
    for j in range(len(X) - 1):
        ax, ay, az = X[j]
        bx, by, bz = X[j + 1]
        if (min(ax, bx) - px > best or px - max(ax, bx) > best or min(az, bz) - pz > best or pz - max(az, bz) > best
                or min(ay, by) - py > best or py - max(ay, by) > best):
            continue
        dx, dy, dz = bx - ax, by - ay, bz - az
        l2 = dx * dx + dy * dy + dz * dz
        t = ((px - ax) * dx + (py - ay) * dy + (pz - az) * dz) / l2 if l2 > 0 else 0
        t = 0 if t < 0 else 1 if t > 1 else t
        d = math.sqrt((ax + dx * t - px) ** 2 + (ay + dy * t - py) ** 2 + (az + dz * t - pz) ** 2)
        if d < best:
            best, bj = d, j
    return best, bj


def at_material(s0, S, X):
    if len(S) == 0:
        return None
    if s0 <= S[0]:
        return X[0]
    if s0 >= S[-1]:
        return None  # not yet paid out / beyond
    import bisect
    j = bisect.bisect_left(S, s0) - 1
    j = max(0, min(j, len(S) - 2))
    f = (s0 - S[j]) / (S[j + 1] - S[j]) if S[j + 1] > S[j] else 0
    return X[j] + (X[j + 1] - X[j]) * f


def turn_at(nodes, i):
    if i <= 0 or i >= len(nodes) - 1:
        return 0.0
    a = nodes[i]["x"] - nodes[i - 1]["x"]
    b = nodes[i + 1]["x"] - nodes[i]["x"]
    la, lb = np.linalg.norm(a), np.linalg.norm(b)
    if la == 0 or lb == 0:
        return 0.0
    c = float(np.clip(np.dot(a, b) / la / lb, -1, 1))
    return math.acos(c)


def analyze(lay, tag, rep):
    snaps = read_snaps(os.path.join(lay, tag + ".snap.txt"))
    head, ops = read_trail(os.path.join(lay, tag + ".trail.txt"))
    out = open(os.path.join(rep, tag + ".lay.txt"), "w", encoding="utf-8")
    w = lambda *a: print(*a, file=out)
    w(head)
    if not snaps:
        w("no snapshots")
        return {}
    end_step = snaps[-1][0]
    brk = next((o for o in ops if o["op"] == "break"), None)
    w(f"snapshots {len(snaps)}, last step {end_step}; break: {brk['line'] if brk else 'none'}")
    lifts = [o for o in ops if o["op"] == "lift"]
    why = defaultdict(int)
    for o in lifts:
        why[o["why"].split()[0]] += 1
    w(f"trail ops: freezes {sum(1 for o in ops if o['op'] == 'freeze')}, lifts {len(lifts)} {dict(why)}, thaws {sum(1 for o in ops if o['op'] == 'thaw')}")

    # 1. Every place the line came to rest on a building member: a chain node pressing it, still (|v| < 5 cm/s), or a
    # trail point frozen on it.
    stretches = []

    def add(t, step, s0, x0, slot, agl, src, speed=0.0):
        for st in stretches:
            if st["obj"] == slot["obj"] and np.linalg.norm(st["x0"] - x0) < 0.5:
                return
        n = slot.get("n", np.zeros(3))
        kind = "top" if n[1] > 0.7 else ("underside" if n[1] < -0.7 else "side/edge")
        stretches.append({"t0": t, "step0": step, "s0": s0, "x0": x0, "obj": slot["obj"], "shape": slot["shape"], "name": slot["name"],
                          "kind": kind, "agl": agl, "src": src, "member": slot["member"], "N": slot.get("N", 0), "cap": slot.get("cap", 0), "speed": speed})

    for step, st, nodes in snaps:
        if st["broken"]:
            break
        for nd in nodes:
            for sl in nd["slots"]:
                if building(sl) and sl.get("N", 0) > 0:
                    add(st["t"], step, nd["s"], nd["x"], sl, nd["agl"], "chain", nd["v"])
    for o in ops:
        if o["op"] == "freeze":
            for sl in o["slots"]:
                if building(sl):
                    add(o["step"] / 1000, o["step"], o["s"], o["x"], sl, o["agl"], "trail")
    stretches.sort(key=lambda x: x["step0"])

    # 2. Follow each: every snapshot the line's distance from where it lay, the laid material's place, and whether a node
    # still presses that object there.
    trail = Trail(ops)
    with open(os.path.join(lay, tag + ".end.txt"), encoding="utf-8") as f:
        for line in f:
            if line.startswith("idx 0 "):
                m = re.search(r"s ([-\d.]+) at " + VEC, line)
                trail.pts.append((float(m.group(1)), np.array([float(m.group(i)) for i in (2, 3, 4)])))
    track = [[] for _ in stretches]
    thru_count = 0
    thru_bld = defaultdict(int)
    corners = []
    for step, st, nodes in snaps:
        trail.advance(step)
        S, X = polyline(trail.pts, nodes, st["broken"])
        for nd in nodes:
            if nd["thru"]:
                thru_count += 1
                m = re.search(r"THRU (\d+)/(\d+):(\S+) in ([\d.]+)", nd["thru"])
                if m and BUILDING.match(m.group(3)):
                    thru_bld[m.group(3)] += 1
        # corners: a node on a building member with a turn over 20 degrees
        if not st["broken"]:
            for i, nd in enumerate(nodes):
                for sl in nd["slots"]:
                    if building(sl) and sl.get("N", 0) > 0 and 0 < i < len(nodes) - 1:
                        th = turn_at(nodes, i)
                        if th > math.radians(20):
                            corners.append((st["t"], i, nd["s"], th, nodes[i - 1]["T"], nd["T"], sl, nd, len(nodes)))
        for k, sr in enumerate(stretches):
            if step < sr["step0"]:
                continue
            d, j = dist_to_polyline(sr["x0"], X)
            pm = at_material(sr["s0"], S, X)
            dm = float(np.linalg.norm(pm - sr["x0"])) if pm is not None else math.nan
            held = False
            for nd in nodes if not st["broken"] else []:
                for sl in nd["slots"]:
                    if sl and sl.get("obj") == sr["obj"] and sl.get("N", 0) > 0 and np.linalg.norm(nd["x"] - sr["x0"]) < 0.5:
                        held = True
            intrail = len(trail.pts) > 0 and sr["s0"] <= trail.pts[-1][0]
            track[k].append((step, d, dm, held, intrail, j, st))
    if trail.bad:
        w(f"WARNING: trail replay mismatches {trail.bad}")

    w(f"segments through a world object at the snapshots: {thru_count} (buildings: {dict(thru_bld)})")
    w("")
    w(f"== {len(stretches)} places the line lay on a building")
    results = []
    for k, sr in enumerate(stretches):
        tr = track[k]
        w(f"-- L{k + 1}: t {sr['t0']:.1f} s, s {sr['s0']:.2f} m, {sr['name']} shape {sr['shape']} ({sr['kind']}, {sr['member'].split(':')[0]}, cap {sr['cap']}), at ({sr['x0'][0]:.2f}, {sr['x0'][1]:.2f}, {sr['x0'][2]:.2f}) agl {sr['agl']:.2f}, from the {sr['src']}, N {sr['N']:.4g}, node speed {sr['speed']:.3f} m/s")
        marks = []
        cps = []
        for dt in (5, 15, 30):
            target = sr["step0"] + dt * 1000
            row = next((r for r in tr if r[0] >= target), None)
            cps.append(row)
            if row is None:
                marks.append(f"+{dt}s: (flight over)")
            else:
                marks.append(f"+{dt}s: line {row[1]:.2f} m away, material moved {row[2]:.2f} m, {'pressed' if row[3] else 'free'}, {'trail' if row[4] else 'chain'}")
        r = tr[-1]
        marks.append(f"end ({r[0] / 1000:.1f} s): line {r[1]:.2f} m away, material moved {r[2]:.2f} m, {'pressed' if r[3] else 'free'}, {'trail' if r[4] else 'chain'}")
        for mk in marks:
            w("   " + mk)
        moved = next((row for row in tr if row[1] > MOVED), None)
        res = {"k": k + 1, "sr": sr, "moved": moved, "end": r, "first_free": next((row for row in tr if not row[3]), None), "cps": cps}
        if moved:
            step, d, dm, held, intrail, j, st = moved
            w(f"   LEFT at {step / 1000:.3f} s ({(step - sr['step0']) / 1000:.1f} s after): line {d:.2f} m from the place, the drone at ({st['drone'][0]:.1f}, {st['drone'][1]:.1f}, {st['drone'][2]:.1f}) agl {st['agl']:.1f} |v| {np.linalg.norm(st['v']):.1f}, exit T {st['exitT']:.4g} N, nodes {st['nodes']}, trail {st['trail']}, wind |{np.linalg.norm(st['wind']):.1f}| m/s")
            lo, hi = step - 1500, step + 300
            # The forces in the 1.5 s before: the drone's distance and height over the place, the air and drag at the nodes
            # within 1 m of the place, the exit's tension, the nearest node's slots.
            dmin, hmin, amax, avert, dragmax, tmax, wmax = 1e9, 1e9, 0, 0, 0, 0, 0
            for s2, st2, nodes2 in snaps:
                if not (lo <= s2 <= step) or st2["broken"]:
                    continue
                dd = np.linalg.norm(st2["drone"] - sr["x0"])
                if dd < dmin:
                    dmin, hmin = dd, st2["drone"][1] - sr["x0"][1]
                tmax = max(tmax, st2["exitT"])
                wmax = max(wmax, np.linalg.norm(st2["wind"]))
                for nd in nodes2:
                    if np.linalg.norm(nd["x"] - sr["x0"]) < 1.0:
                        a = np.linalg.norm(nd["air"])
                        if a > amax:
                            amax, avert = a, nd["air"][1]
                        dragmax = max(dragmax, np.linalg.norm(nd["drag"]))
            seg = sr.get("seglen", 0)
            w(f"   CAUSE NUMBERS: drone nearest {dmin:.2f} m (height over the place {hmin:.2f} m), air at the nodes within 1 m up to {amax:.1f} m/s (vertical {avert:.1f}), drag on a node up to {dragmax:.2f} mN, exit T up to {tmax:.4g} N, wind at the drone up to {wmax:.1f} m/s")
            res["cause"] = (dmin, hmin, amax, avert, dragmax, tmax, wmax)
            for o in ops:
                if lo <= o["step"] <= hi and o["op"] in ("lift", "thaw"):
                    w(f"      op {o['line'][:420]}")
            for s2, st2, nodes2 in snaps:
                if not (lo <= s2 <= hi) or st2["broken"]:
                    continue
                best = min(nodes2, key=lambda nd: abs(nd["s"] - sr["s0"]))
                i = best["i"]
                for nd in nodes2[max(0, i - 1):i + 2]:
                    sl = " / ".join(f"{x['member'].split(':')[0]}:{x.get('shape')} N {x.get('N', 0):.3g} n ({x['n'][0]:.2f},{x['n'][1]:.2f},{x['n'][2]:.2f}) sl {x.get('sliding')}{x.get('slid')}" for x in nd["slots"] if x)
                    w(f"      {s2 / 1000:.1f} n{nd['i']} s {nd['s']:.3f} at ({nd['x'][0]:.2f},{nd['x'][1]:.2f},{nd['x'][2]:.2f}) agl {nd['agl']:.2f} |v| {nd['v']:.3f} T {nd['T'] if nd['T'] is not None else -1:.4g} air ({nd['air'][0]:.1f},{nd['air'][1]:.1f},{nd['air'][2]:.1f}) drag ({nd['drag'][0]:.3f},{nd['drag'][1]:.3f},{nd['drag'][2]:.3f}) mN{' lifted' if nd['lifted'] else ''}{' anchor' if nd['anchor'] else ''} | {sl}")
                w(f"      {s2 / 1000:.1f} drone ({st2['drone'][0]:.2f},{st2['drone'][1]:.2f},{st2['drone'][2]:.2f}) agl {st2['agl']:.2f} exitT {st2['exitT']:.4g} trail {st2['trail']} wind ({st2['wind'][0]:.1f},{st2['wind'][1]:.1f},{st2['wind'][2]:.1f}) stick {st2['stick']}")
        else:
            w("   stayed (the line within %.2f m of the place to the end)" % MOVED)
        results.append(res)

    w("")
    w(f"== corners: a chain node on a building member turning the line over 20 degrees ({len(corners)} node-snapshots)")
    seen = set()
    for t, i, s, th, Tin, Tout, sl, nd, n in corners:
        key = (sl["obj"], sl["shape"], round(t))
        if key in seen:
            continue
        seen.add(key)
        if Tin is None or Tout is None:
            continue
        lo, hi = min(Tin, Tout), max(Tin, Tout)
        mu = sl.get("mu", 0.5)
        bound = lo * math.exp(mu * th)
        w(f"   t {t:.1f} node {i}/{n - 1} s {s:.2f} {sl['name']} shape {sl['shape']} turn {math.degrees(th):.0f} deg, T before {Tin:.4g} after {Tout:.4g} N, ratio {hi / lo if lo > 0 else math.inf:.3g} vs e^(mu theta) {math.exp(mu * th):.3g} (mu {mu}), cap {sl.get('cap')} sliding {sl.get('sliding')} N {sl.get('N'):.4g}")
    out.close()
    seed = re.search(r"seed (0x[0-9A-F]+)", head).group(1)
    air = re.search(r"wind (\S+) rain (\S+)", head)
    with open(os.path.join(rep, tag + ".stretches.tsv"), "w", encoding="utf-8") as tsv:
        for res in results:
            sr = res["sr"]
            def cell(row):
                return "-" if row is None else f"{row[1]:.2f}/{row[2]:.2f}"
            mv = res["moved"]
            cause = res.get("cause")
            cz = "-" if cause is None else f"drone {cause[0]:.1f} m ({cause[1]:+.1f} m), air {cause[2]:.1f} m/s, drag {cause[4]:.1f} mN, exit {cause[5]:.3f} N, wind {cause[6]:.1f} m/s"
            tsv.write(TAB.join([tag[1:], seed, air.group(1), f"L{res['k']}", f"{sr['t0']:.1f}", f"{sr['s0']:.2f}", sr["name"], str(sr["shape"]), sr["kind"],
                                   f"{sr['agl']:.2f}", f"({sr['x0'][0]:.2f}, {sr['x0'][1]:.2f}, {sr['x0'][2]:.2f})",
                                   cell(res["cps"][0]), cell(res["cps"][1]), cell(res["cps"][2]), f"{res['end'][0] / 1000:.1f}", cell(res["end"]),
                                   "-" if mv is None else f"{mv[0] / 1000:.1f}", cz]) + NL)
    return {"stretches": results, "lifts": dict(why)}


def main():
    lay, rep = sys.argv[1], sys.argv[2]
    os.makedirs(rep, exist_ok=True)
    tags = sys.argv[3:] or sorted({f.split(".")[0] for f in os.listdir(lay) if f.endswith(".snap.txt")})
    for tag in tags:
        r = analyze(lay, tag, rep)
        n = len(r.get("stretches", []))
        m = sum(1 for x in r.get("stretches", []) if x["moved"])
        print(f"{tag}: {n} places on buildings, {m} left; lifts {r.get('lifts')}")


if __name__ == "__main__":
    main()
