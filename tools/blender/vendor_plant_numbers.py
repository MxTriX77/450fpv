"""The plants' numbers for the props (w9-village-physics task 2.8; D-089): stems per drawn grass instance and the leaf
area density of every vendor tree and bush, measured from the local store's meshes and textures.

    python tools/blender/vendor_plant_numbers.py            # write assets-src/vendor_plant_numbers.json
    python tools/blender/vendor_plant_numbers.py --check    # exit 1 when the committed file differs from a re-run

Only derived numbers are written; no vertex, triangle, UV or texel of the store leaves it.

**Grass.** For each prefab the farmland's grass layers name (maps/farmland/map.json grass.layers), its mesh and its
material's alpha-clipped base map are read as Unity draws them. A stem is one blade or culm as drawn: a connected run
of opaque texels (alpha >= the material's cutoff) where the card meets a horizontal plane, runs joined across the
triangles of one card where they meet on a shared edge. The planes stand at 5 %, 10 % ... 95 % of the mesh's top; a
stem at height h is what a disc sweeping at h would meet. The instance's stems are the most any plane crosses (blades
merge in the clump near the base, short ones end higher up, so the most is the best single count, and a floor).
Widths are each run's length along the card where it crosses the plane, so a slanted blade reads wider.

**Crowns.** For each variant of assets-src/vendor_plants.json, the drawn card area of each foliage slot of its LOD0
(the params' slots): per triangle its 3D area times the share of its UV area whose texels are opaque (texel centres
inside the UV triangle; a triangle under 4 texels takes its centroid). The crown's volume is the union of the twig
shell's spheres in the variant's committed metadata (W4a), integrated on 1 cm rows; leaf area density is the leaf and
needle slots' area over that volume. Twig cards (bark-textured twig tubes, bare dead twigs) are reported apart."""
import argparse
import hashlib
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from lib import plant, vendor  # noqa: E402

OUT = os.path.join("assets-src", "vendor_plant_numbers.json")
MAP = os.path.join("maps", "farmland", "map.json")
PLANTS = os.path.join("assets-src", "vendor_plants.json")
STORE = os.path.join("vendor", "world_final", "Farming_land")
MODELS = os.path.join("unity", "Assets", "_Project", "Art", "Models")
GRASS_PREFABS = "Assets/Farm_land/Prefabs/Grass"
FRACTIONS = [round(0.05 * k, 2) for k in range(1, 20)]  # the planes, as fractions of the mesh's top
SAMPLE_M = 0.0005           # along a plane's cut: at most 0.5 mm, and at most half a texel, between samples
ROW_M = 0.01                # the crown volume's integration rows
# What each foliage base map draws (looked at, 2026-11-12): leaf, needle, or twig (no leaf).
CARD_KIND = {
    "island_tree_01_branches_diff_4k.png": "twig",   # bark-textured twig tubes, drawn whole
    "island_tree_leaves.png": "leaf",
    "island_tree_dry_leaves.png": "leaf",
    "Branches_albedo.srgb.dds.asset": "needle",      # Pine002: needle sprays on twigs
    "branches_albedo.srgb.dds.asset": "needle",      # Pine004, Pine006
    "Bush_Dead_Br_01_Albedo_Opacity.tif": "twig",    # bare dead twigs
    "GreenBush_Albedo_Opacity.tif": "leaf",
    "Plant_Perennials_qilgP2_Albedo_Opacity.tif": "leaf",
    "RedBush_Albedo_Opacity.tif": "leaf",
    "BushDry_BaseColor.tif": "twig",                 # bare dry twigs with a few dry scraps
}


def store(root, relative):
    return os.path.join(root, STORE, *relative.split("/"))


def rel(root, path):
    return os.path.relpath(path, os.path.join(root, STORE)).replace(os.sep, "/")


def guid_index(root):
    """guid -> store path, over every .meta under the store's Assets."""
    index = {}
    for folder, _, files in os.walk(os.path.join(root, STORE, "Assets")):
        for name in sorted(files):
            if name.endswith(".meta"):
                index[vendor.guid_of(os.path.join(folder, name[:-5]))] = os.path.join(folder, name[:-5])
    return index


def shader_text(index):
    def source(guid):
        path = index.get(guid)
        if path and path.endswith(".shader"):
            with open(path, encoding="utf-8", errors="ignore") as f:
                return f.read()
        return None
    return source


def pin(root, path):
    return {"file": rel(root, path), "sha256": vendor.sha256(path)}


def _area(a, b, c):
    u = [b[i] - a[i] for i in range(3)]
    w = [c[i] - a[i] for i in range(3)]
    x = (u[1] * w[2] - u[2] * w[1], u[2] * w[0] - u[0] * w[2], u[0] * w[1] - u[1] * w[0])
    return 0.5 * math.sqrt(x[0] * x[0] + x[1] * x[1] + x[2] * x[2])


def _quantiles(values, qs=(0.1, 0.5, 0.9)):
    if not values:
        return [0.0] * len(qs)
    s = sorted(values)
    return [s[min(len(s) - 1, int(q * (len(s) - 1) + 0.5))] for q in qs]


# --- grass ------------------------------------------------------------------------------------------------------

class _Union:
    def __init__(self):
        self.parent = {}

    def find(self, x):
        self.parent.setdefault(x, x)
        while self.parent[x] != x:
            self.parent[x] = self.parent[self.parent[x]]
            x = self.parent[x]
        return x

    def join(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.parent[max(ra, rb)] = min(ra, rb)


def cut(points, tris, uvs, alpha, texels, h):
    """The stems crossing the plane y = h: a list of their widths (m) as drawn on the cards."""
    union, width = _Union(), {}
    for t, (tri, uv) in enumerate(zip(tris, uvs)):
        ends = []
        for i, j in ((0, 1), (1, 2), (2, 0)):
            ya, yb = points[tri[i]][1], points[tri[j]][1]
            if (ya - h) * (yb - h) < 0:
                s = (h - ya) / (yb - ya)
                p = tuple(points[tri[i]][k] + s * (points[tri[j]][k] - points[tri[i]][k]) for k in range(3))
                q = tuple(uv[i][k] + s * (uv[j][k] - uv[i][k]) for k in range(2))
                ends.append((("e", min(tri[i], tri[j]), max(tri[i], tri[j])), p, q))
        if len(ends) != 2:
            continue
        (ka, pa, qa), (kb, pb, qb) = ends
        length = math.dist(pa, pb)
        uv_texels = max(abs(qb[0] - qa[0]) * texels[0], abs(qb[1] - qa[1]) * texels[1])
        n = max(1, math.ceil(length / SAMPLE_M), math.ceil(2 * uv_texels))
        opaque = [alpha.opaque(qa[0] + (k + 0.5) / n * (qb[0] - qa[0]), qa[1] + (k + 0.5) / n * (qb[1] - qa[1]))
                  for k in range(n)]
        k = 0
        while k < n:
            if not opaque[k]:
                k += 1
                continue
            start = k
            while k < n and opaque[k]:
                k += 1
            run = ("r", t, start)
            union.find(run)
            width[run] = (k - start) * length / n
            if start == 0:
                union.join(run, ka)
            if k == n:
                union.join(run, kb)
    stems = {}
    for run, w in width.items():
        root = union.find(run)
        stems[root] = stems.get(root, 0.0) + w
    return sorted(stems.values())


def grass(root, index):
    with open(os.path.join(root, MAP), encoding="utf-8") as f:
        layers = json.load(f)["grass"]["layers"]
    prefabs = {}
    for name in sorted({layer["prefab"] for layer in layers}):
        prefab = store(root, f"{GRASS_PREFABS}/{name}.prefab")
        with open(prefab, encoding="utf-8") as f:
            text = f.read()
        mesh_ref = re.search(r"m_Mesh: \{fileID: (-?\d+), guid: (\w+)", text)
        material = index[re.search(r"m_Materials:\s+- \{fileID: -?\d+, guid: (\w+)", text).group(1)]
        mesh_path = index[mesh_ref.group(2)]
        raw, tris, uvs = vendor.read_mesh(mesh_path, int(mesh_ref.group(1)))
        maps = vendor.material_maps(material, shader_text(index))
        base_map = index[maps["base_map"]]
        alpha = vendor.Alpha(base_map, maps["cutoff"], maps["scale"], maps["offset"])
        texels = (alpha.width * abs(maps["scale"][0]), alpha.height * abs(maps["scale"][1]))
        points, welded = plant.weld(raw, [t[:3] for t in tris])
        cell = {}  # plant.weld's merge, kept per raw vertex, so a shared edge is one key on both its triangles
        for p in points:
            cell[tuple(round(c / plant.WELD_M) for c in p)] = len(cell)
        remap = [cell[tuple(round(c / plant.WELD_M) for c in p)] for p in raw]
        wtris, wuvs = [], []
        for t, uv in zip(tris, uvs):
            w = tuple(remap[v] for v in t[:3])
            if len(set(w)) == 3:
                wtris.append(w)
                wuvs.append(uv)
        top = max(p[1] for p in raw)
        heights = sorted({p[1] for p in points})
        profile = []
        for fraction in FRACTIONS:
            h = fraction * top
            while any(abs(h - y) < 1e-7 for y in heights):  # a plane through a vertex is nudged off it
                h += 1e-6
            widths = cut(points, wtris, wuvs, alpha, texels, h)
            profile.append({"at": fraction, "height_m": round(h, 4), "stems": len(widths),
                            "width_mm": [round(1000 * w, 2) for w in _quantiles(widths)]})
        best = max(profile, key=lambda r: (r["stems"], -r["at"]))
        prefabs[name] = {
            "source": {"prefab": pin(root, prefab), "mesh": pin(root, mesh_path), "material": pin(root, material),
                       "base_map": pin(root, base_map)},
            "cutoff": maps["cutoff"],
            "cards": len(plant.components(welded)),
            "triangles": len(tris),
            "mesh_y_m": [round(min(p[1] for p in raw), 4), round(top, 4)],
            "stems": best["stems"], "stems_at": best["at"], "width_mm": best["width_mm"],
            "profile": profile,
        }
    out_layers = []
    for layer in layers:
        p = prefabs[layer["prefab"]]
        hs, ws = layer["height_scale"], layer["width_scale"]
        out_layers.append({
            "layer": layer["layer"], "prefab": layer["prefab"], "group": layer["group"],
            "stems_per_instance": p["stems"],
            "height_m": [round(p["mesh_y_m"][1] * hs[0], 3), round(p["mesh_y_m"][1] * hs[1], 3)],
            "width_mm": [round(p["width_mm"][0] * ws[0], 2), round(p["width_mm"][2] * ws[1], 2)],
        })
    return prefabs, out_layers


# --- crowns -----------------------------------------------------------------------------------------------------

def opaque_share(uv, alpha, texels, cache):
    """The share of a UV triangle whose texels are opaque, by texel centres inside it."""
    k = tuple(round(c, 6) for q in uv for c in q)
    if k in cache:
        return cache[k]
    if alpha.data is None:
        cache[k] = 1.0
        return 1.0
    (u0, v0), (u1, v1), (u2, v2) = ((q[0] * texels[0], q[1] * texels[1]) for q in uv)
    det = (u1 - u0) * (v2 - v0) - (u2 - u0) * (v1 - v0)
    inside = hits = 0
    if abs(det) >= 8.0:  # 4 texels or more
        for y in range(math.floor(min(v0, v1, v2)), math.ceil(max(v0, v1, v2))):
            cy = y + 0.5
            for x in range(math.floor(min(u0, u1, u2)), math.ceil(max(u0, u1, u2))):
                cx = x + 0.5
                s = ((cx - u0) * (v2 - v0) - (u2 - u0) * (cy - v0)) / det
                t = ((u1 - u0) * (cy - v0) - (cx - u0) * (v1 - v0)) / det
                if s >= 0 and t >= 0 and s + t <= 1:
                    inside += 1
                    hits += alpha.opaque(cx / texels[0], cy / texels[1])
    if not inside:
        inside, hits = 1, alpha.opaque((u0 + u1 + u2) / 3 / texels[0], (v0 + v1 + v2) / 3 / texels[1])
    cache[k] = hits / inside
    return cache[k]


def shell_volume(spheres):
    """The union volume of spheres [(centre, r)], integrated on ROW_M rows along x."""
    if not spheres:
        return 0.0
    lo = [min(c[i] - r for c, r in spheres) for i in range(3)]
    hi = [max(c[i] + r for c, r in spheres) for i in range(3)]
    volume = 0.0
    ny, nz = math.ceil((hi[1] - lo[1]) / ROW_M), math.ceil((hi[2] - lo[2]) / ROW_M)
    for j in range(ny):
        y = lo[1] + (j + 0.5) * ROW_M
        near = [(c, r) for c, r in spheres if abs(c[1] - y) < r]
        for k in range(nz):
            z = lo[2] + (k + 0.5) * ROW_M
            spans = []
            for c, r in near:
                d = r * r - (c[1] - y) ** 2 - (c[2] - z) ** 2
                if d > 0:
                    s = math.sqrt(d)
                    spans.append((c[0] - s, c[0] + s))
            if not spans:
                continue
            spans.sort()
            a, b = spans[0]
            for s0, s1 in spans[1:]:
                if s0 > b:
                    volume += b - a
                    a, b = s0, s1
                else:
                    b = max(b, s1)
            volume += b - a
    return volume * ROW_M * ROW_M


def crowns(root, index):
    import vendor_plants as vp
    catalog = vp.load_catalog(root)
    out = {}
    for asset_id, entry, variant in vp.selected(catalog, "all"):
        with open(vp.params_path(root, entry, variant), encoding="utf-8") as f:
            params = json.load(f)
        source = params["source"]
        prefab = vendor.pinned(store(root, source["prefab"]), source["prefab_sha256"])
        mesh_file = vendor.pinned(store(root, source["mesh_file"]), source["mesh_sha256"])
        renderer = vendor.prefab_lods(prefab)["lods"][0]["renderers"][0]
        raw, tris, uvs = vendor.read_mesh(mesh_file, renderer["mesh_file_id"], source["mesh_name"] or None)
        points = [vendor.apply(renderer["matrix"], p) for p in raw]
        materials = [vp._material_path(root, prefab, g) for g in renderer["materials"]]
        names = [os.path.splitext(os.path.basename(m))[0] for m in materials]
        with open(os.path.join(root, MODELS, entry["class"], asset_id, variant + ".assetmeta.json"),
                  encoding="utf-8") as f:
            meta = json.load(f)
        spheres = [(tuple(c["center"]), c["radius"]) for c in meta["collision"]
                   if c["shape"] == "sphere" and c["material"] == "twig"]
        volume = shell_volume(spheres)
        slots, area = [], {"leaf": 0.0, "needle": 0.0, "twig": 0.0}
        inside_area = 0.0
        for slot in params["slots"]:
            if slot["role"] != "foliage":
                continue
            material = materials[names.index(slot["material"])]
            vendor.pinned(material, slot["material_sha256"])
            maps = vendor.material_maps(material, vp._shader_source(root, slot["shader_sha256"]))
            base_map = vendor.pinned(store(root, slot["base_map"]), slot["base_map_sha256"])
            alpha = vendor.Alpha(base_map, maps["cutoff"], maps["scale"], maps["offset"])
            texels = ((alpha.width * abs(maps["scale"][0]), alpha.height * abs(maps["scale"][1]))
                      if alpha.data is not None else (1.0, 1.0))
            kind = CARD_KIND[slot["base_map"].split("/")[-1]]
            cache, card, drawn = {}, 0.0, 0.0
            for t, uv in zip(tris, uvs):
                if names[t[3]] != slot["material"]:
                    continue
                a = _area(points[t[0]], points[t[1]], points[t[2]])
                share = a * opaque_share(uv, alpha, texels, cache)
                card += a
                drawn += share
                centre = tuple(sum(points[v][i] for v in t[:3]) / 3 for i in range(3))
                if kind != "twig" and any(math.dist(centre, c) <= r for c, r in spheres):
                    inside_area += share
            area[kind] += drawn
            slots.append({"material": slot["material"], "kind": kind, "cutoff": maps["cutoff"],
                          "card_area_m2": round(card, 3), "drawn_area_m2": round(drawn, 3)})
        leaf = area["leaf"] + area["needle"]
        out[variant] = {
            "id": asset_id, "kind": entry["kind"], "slots": slots,
            "leaf_area_m2": round(leaf, 3), "twig_card_area_m2": round(area["twig"], 3),
            "shell_spheres": len(spheres), "crown_volume_m3": round(volume, 3),
            "leaf_area_density_m2_per_m3": round(leaf / volume, 3) if volume else 0.0,
            "leaf_inside_shell": round(inside_area / leaf, 3) if leaf else None,
        }
    return out


def measure(root):
    index = guid_index(root)
    prefabs, layers = grass(root, index)
    return {
        "schema": 1,
        "about": "The plants' numbers for the props (w9-village-physics 2.8; D-089): stems per drawn grass instance "
                 "per prefab and per layer of maps/farmland/map.json, and each vendor tree and bush variant's drawn "
                 "leaf area per m3 of its twig shell. Measured from the local store by python "
                 "tools/blender/vendor_plant_numbers.py; method and limits in openspec/changes/w9-village-physics/"
                 "design.md 2.8. Widths in mm as drawn where a blade crosses a horizontal plane (p10, p50, p90).",
        "grass": {"prefabs": prefabs, "layers": layers},
        "crowns": crowns(root, index),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--check", action="store_true", help="compare a re-run with the committed file")
    args = parser.parse_args()
    text = json.dumps(measure(ROOT), indent=2, sort_keys=False) + "\n"
    path = os.path.join(ROOT, OUT)
    if args.check:
        with open(path, encoding="utf-8") as f:
            same = f.read() == text
        print(f"{OUT}: {'same' if same else 'DIFFERS'} (sha256 {hashlib.sha256(text.encode()).hexdigest()})")
        return 0 if same else 1
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote {OUT} (sha256 {hashlib.sha256(text.encode()).hexdigest()})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
