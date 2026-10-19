"""assets-src/catalog.json: asset id -> class, generator, variants and the visual material per slot; and the params
file per variant, assets-src/<class>/<variant>.json (design decision 1)."""
import hashlib
import json
import os

CATALOG = os.path.join("assets-src", "catalog.json")
MODELS = os.path.join("unity", "Assets", "_Project", "Art", "Models")
RECIPES = os.path.join("assets-src", "materials")


class CatalogError(Exception):
    """A missing or malformed catalog entry; the message names it."""


def load(root):
    with open(os.path.join(root, CATALOG), encoding="utf-8") as f:
        catalog = json.load(f)
    if catalog.get("schema") != 1 or not isinstance(catalog.get("assets"), dict):
        raise CatalogError(f"{CATALOG}: expected schema 1 with an 'assets' object")
    for asset_id, entry in catalog["assets"].items():
        for key in ("class", "generator", "variants", "materials"):
            if key not in entry:
                raise CatalogError(f"{CATALOG}: {asset_id} lacks '{key}'")
    return catalog


def asset_ids(catalog, wanted):
    """The ids to build from the command line: every id for ['all'], else each named id, which must exist."""
    if list(wanted) == ["all"]:
        return sorted(catalog["assets"])
    unknown = [i for i in wanted if i not in catalog["assets"]]
    if unknown:
        raise CatalogError(f"not in {CATALOG}: {', '.join(unknown)}")
    return list(wanted)


def params_path(root, entry, variant):
    return os.path.join(root, "assets-src", entry["class"], variant + ".json")


def output_dir(root, entry, asset_id):
    return os.path.join(root, MODELS, entry["class"], asset_id)


def load_params(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def card_visuals(root):
    """The visual materials drawn as cards: the recipes in assets-src/materials with an `alpha_clip` (p1-v13-shrub,
    "Compliant shells in the collision audit": the audit holds card slots to the card allowance), and the far-card
    recipes of such a recipe (`far_card_of`, p1-v01-locust task 1.2j)."""
    folder = os.path.join(root, RECIPES)

    def recipe(name):
        with open(os.path.join(folder, name + ".json"), encoding="utf-8") as f:
            return json.load(f)

    names = set()
    for name in sorted(n[:-len(".json")] for n in os.listdir(folder) if n.endswith(".json")):
        r = recipe(name)
        if "alpha_clip" in (recipe(r["far_card_of"]) if "far_card_of" in r else r):
            names.add(name)
    return names


def params_sha256(params):
    """SHA-256 of the params in canonical JSON (sorted keys, no whitespace), so formatting and line endings don't
    change the hash recorded in the metadata."""
    canonical = json.dumps(params, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()
