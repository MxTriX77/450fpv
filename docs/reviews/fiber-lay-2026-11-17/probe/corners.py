"""The corners (p7d-tether 1.11h, task 2): every run of chain nodes on one building object (a line over an edge, a wall's
top, a roof's ridge or a corner), per snapshot: the wrap (the sum of the node turns over the run, and the net turn of the
line from the segment entering it to the one leaving it), the tension on the anchor's side (T_far) and the drone's side
(T_near), and the capstan's bound e^(mu_s * wrap). Episodes are a run on one object kept across snapshots (a gap under
0.3 s). For each episode: how far the line beyond the corner (the material 1 m behind the run's anchor-side end) moved
while it lasted, and the most the drone's side pulled. Usage: python corners.py <lay dir> <report file> [tags]"""
import math
import os
import sys
from analyze import read_snaps, building, turn_at, at_material, polyline, Trail, read_trail, VEC
from vec3 import np
import re


def runs(nodes):
    out = []
    i = 1
    n = len(nodes)
    while i < n - 1:
        objs = {sl["obj"] for sl in nodes[i]["slots"] if building(sl)}
        if not objs:
            i += 1
            continue
        obj = next(iter(objs))
        j = i
        while j + 1 < n - 1 and any(building(sl) and sl["obj"] == obj for sl in nodes[j + 1]["slots"]):
            j += 1
        pressing = any(building(sl) and sl["obj"] == obj and sl.get("N", 0) > 0 for k in range(i, j + 1) for sl in nodes[k]["slots"])
        if pressing:
            wrap = sum(turn_at(nodes, k) for k in range(i, j + 1))
            a = nodes[i]["x"] - nodes[i - 1]["x"]
            b = nodes[j + 1]["x"] - nodes[j]["x"]
            la, lb = np.linalg.norm(a), np.linalg.norm(b)
            net = math.acos(max(-1, min(1, np.dot(a, b) / la / lb))) if la > 0 and lb > 0 else 0
            mus = [sl.get("mu", 0.5) for k in range(i, j + 1) for sl in nodes[k]["slots"] if building(sl) and sl["obj"] == obj]
            sliding = any(sl.get("sliding") for k in range(i, j + 1) for sl in nodes[k]["slots"] if building(sl))
            name = next(sl["name"] for sl in nodes[i]["slots"] if building(sl))
            out.append({"obj": obj, "name": name, "i": i, "j": j, "wrap": wrap, "net": net, "mu": min(mus) if mus else 0.5,
                        "Tfar": nodes[i - 1]["T"], "Tnear": nodes[j]["T"], "s0": nodes[i]["s"], "s1": nodes[j]["s"],
                        "x": nodes[i]["x"], "sliding": sliding, "agl": nodes[i]["agl"]})
        i = j + 1
    return out


def main():
    lay, outp = sys.argv[1], sys.argv[2]
    tags = sys.argv[3:] or sorted({f.split(".")[0] for f in os.listdir(lay) if f.endswith(".snap.txt")})
    out = open(outp, "w", encoding="utf-8")
    w = lambda *a: print(*a, file=out)
    total = {"episodes": 0, "big": 0, "held": 0, "slid": 0, "moved": 0}
    for tag in tags:
        snaps = read_snaps(os.path.join(lay, tag + ".snap.txt"))
        head, ops = read_trail(os.path.join(lay, tag + ".trail.txt"))
        trail = Trail(ops)
        with open(os.path.join(lay, tag + ".end.txt"), encoding="utf-8") as f:
            for line in f:
                if line.startswith("idx 0 "):
                    m = re.search(r"s ([-\d.]+) at " + VEC, line)
                    trail.pts.append((float(m.group(1)), np.array([float(m.group(i)) for i in (2, 3, 4)])))
        eps = []
        open_eps = {}
        for step, st, nodes in snaps:
            trail.advance(step)
            if st["broken"]:
                break
            S, X = polyline(trail.pts, nodes, False)
            for r in runs(nodes):
                key = (r["obj"], round(r["x"][0] / 2), round(r["x"][2] / 2))
                e = open_eps.get(key)
                if e is None or step - e["last"] > 300:
                    e = {"key": key, "name": r["name"], "start": step, "rows": [], "behind_s": r["s0"] - 1.0}
                    e["behind_x0"] = at_material(e["behind_s"], S, X)
                    open_eps[key] = e
                    eps.append(e)
                e["last"] = step
                bx = at_material(e["behind_s"], S, X)
                r["behind_moved"] = np.linalg.norm(bx - e["behind_x0"]) if bx is not None and e["behind_x0"] is not None else math.nan
                r["step"] = step
                r["exitT"] = st["exitT"]
                e["rows"].append(r)
        w(f"== {head}")
        w(f"   {len(eps)} corner episodes (a run of nodes on one building object, pressing)")
        for e in eps:
            rows = e["rows"]
            wrap = max(r["wrap"] for r in rows)
            pulls = [r for r in rows if r["Tnear"] is not None and r["Tfar"] is not None]
            if not pulls:
                continue
            top = max(pulls, key=lambda r: r["Tnear"])
            bound = math.exp(top["mu"] * top["wrap"])
            ratio = top["Tnear"] / top["Tfar"] if top["Tfar"] > 0 else math.inf
            moved = max((r["behind_moved"] for r in rows if not math.isnan(r["behind_moved"])), default=math.nan)
            slid = sum(1 for r in rows if r["sliding"])
            total["episodes"] += 1
            if wrap >= math.radians(60):
                total["big"] += 1
                if ratio < 0.95 * bound:
                    total["held"] += 1
                else:
                    total["slid"] += 1
                if moved > 0.25:
                    total["moved"] += 1
            w(f"   {e['name']} near ({rows[0]['x'][0]:.1f}, {rows[0]['x'][2]:.1f}) agl {rows[0]['agl']:.1f}: {rows[0]['step'] / 1000:.1f}-{rows[-1]['step'] / 1000:.1f} s, {len(rows)} snapshots, nodes in the run up to {max(r['j'] - r['i'] + 1 for r in rows)}, wrap up to {math.degrees(wrap):.0f} deg (net {math.degrees(max(r['net'] for r in rows)):.0f}); at the hardest pull ({top['step'] / 1000:.1f} s): T near {top['Tnear']:.4g} N, T far {top['Tfar']:.4g} N, ratio {ratio:.3g} vs e^(mu wrap) {bound:.3g} (mu {top['mu']}, wrap {math.degrees(top['wrap']):.0f}), exit T {top['exitT']:.4g}; sliding in {slid} of {len(rows)}; the line 1 m beyond moved up to {moved:.2f} m")
    w("")
    w(f"TOTAL {total}")
    out.close()
    print(total)


if __name__ == "__main__":
    main()
