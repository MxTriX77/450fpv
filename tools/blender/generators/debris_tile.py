"""debris_tile: one clay-tile piece lying on the ground (object sheet docs/object-sheets/lines-roads-debris.md §K02;
p1-h01-adobe-house, design decision 10): a shard broken out of a tile, or a whole tile. The pieces are lib.debris_tile's
library builders, which the `house` generator composes as its tile heap; this generator builds one of them as a
catalog asset. Plain Python.

Params (assets-src/debris/K02_<piece>.json) are PARAMS below; a key the piece does not use must be 0 (check). The
sheet's ridge pieces and slate pieces are range-checked and refused, "not built yet" (built with H02-H04's slate roofs
and the ridge, later). Frame (asset space, lib.frame): x along the tile, the piece centred on the origin in plan and
resting on y = 0, PLANTED (it lies on the ground, which covers any primitive below it).

- tile_shard: `size_m` (its long side) broken from a tile of `tile_m` by `fracture_count` breaks (lib.debris_tile:
  two leave a corner or a strip, three an edge piece, four an inner piece, more chip its corners). One box; an `edge`
  snag along each run of fracture rim.
- tile_whole: the tile of `tile_m` with its camber and nib, resting on its tail and nib. Two boxes, one per half.

Slots: tile (the clay) and tile_fracture (the broken rims), both `physics_material`, masonry (the sheet). The seed
draws the outline and which of lib.debris_tile.CELLS_PX the clay maps into. LOD1 drops the jags (a shard) or the
camber's strips and the nib (a whole tile); the cull distances follow the sheet's rule, 935 * s / 2 for the piece's
longest size s.
"""
import random

from lib import debris_tile

PLANTED = True
PIECES = ("tile_shard", "tile_whole", "ridge_piece", "slate_sheet", "slate_half", "slate_fragment", "slate_stack")
BUILT = ("tile_shard", "tile_whole")
SHARD_SIZE_M = (0.05, 0.35)  # the sheet: shards 0.05-0.35 m
BREAKS = (2, 6)              # the sheet: 2-6 pieces per tile

PARAMS = {
    "piece": {"type": "str", "range": ", ".join(PIECES), "default": "tile_shard",
              "effect": f"which piece; {', '.join(p for p in PIECES if p not in BUILT)} are not built yet"},
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "the shard's outline and the cell of the clay set its tile maps into"},
    "size_m": {"type": "float", "range": (0.0, 1.75), "default": 0.15,
               "effect": "a shard's long side (0.05-0.35); 0 for a whole tile"},
    "tile_m.length": {"type": "float", "range": (0.33, 0.40), "default": 0.36, "effect": "the tile along its slope"},
    "tile_m.width": {"type": "float", "range": (0.20, 0.24), "default": 0.22, "effect": "the tile across"},
    "tile_m.thickness": {"type": "float", "range": (0.012, 0.016), "default": 0.014, "effect": "the tile's clay"},
    "tile_m.camber": {"type": "float", "range": (0.0, 0.02), "default": 0.008,
                      "effect": "how far the tile's middle stands above its ends (assumed: a slight camber)"},
    "fracture_count": {"type": "int", "range": (0, 6), "default": 3,
                       "effect": "a shard's breaks (2-6); 0 for a whole tile"},
    "physics_material": {"type": "str", "range": "masonry", "default": "masonry",
                         "effect": "collision material and the slots' physics (the sheet: masonry)"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.5, 50.0), "default": 3.0, "effect": "LOD1 from here"},
    "lod.cull_m": {"type": "float", "range": (5.0, 400.0), "default": 70.0, "effect": "culled beyond"},
}


def check(params):
    """The piece is built, its sizes are set and the unused ones 0, an inner shard fits inside its tile, the
    material is masonry and the LODs are ordered."""
    piece = params["piece"]
    if piece not in PIECES:
        return [f"'piece' = {piece!r} must be one of {', '.join(PIECES)}"]
    if piece not in BUILT:
        return [f"'piece' = {piece!r} is not built yet (built: {', '.join(BUILT)})"]
    problems = []
    size, breaks = params["size_m"], params["fracture_count"]
    if piece == "tile_shard":
        if not SHARD_SIZE_M[0] <= size <= SHARD_SIZE_M[1]:
            problems.append(f"'size_m' = {size} must lie in {list(SHARD_SIZE_M)} for a tile_shard")
        if not BREAKS[0] <= breaks <= BREAKS[1]:
            problems.append(f"'fracture_count' = {breaks} must lie in {list(BREAKS)} for a tile_shard")
        inner = params["tile_m"]["length"] - 2 * debris_tile.MARGIN_M
        if breaks >= 4 and size > inner:
            problems.append(f"'size_m' = {size} does not fit inside its tile with {breaks} breaks (at most {inner:g})")
    else:
        for key, value in (("size_m", size), ("fracture_count", breaks)):
            if value != 0:
                problems.append(f"'{key}' = {value} is not used by a tile_whole; set it to 0")
    if params["physics_material"] != debris_tile.PHYSICS:
        problems.append(f"'physics_material' = {params['physics_material']!r} must be {debris_tile.PHYSICS!r}")
    if params["lod"]["lod1_switch_m"] >= params["lod"]["cull_m"]:
        problems.append("'lod.lod1_switch_m' must lie under 'lod.cull_m'")
    return problems


def build(params):
    rng = random.Random(f"debris_tile {params['piece']} {params['seed']}")
    t = params["tile_m"]
    tile = debris_tile.Tile(t["length"], t["width"], t["thickness"], t["camber"])
    cell = rng.randrange(len(debris_tile.CELLS_PX))
    if params["piece"] == "tile_shard":
        piece = debris_tile.shard(rng, tile, params["size_m"], params["fracture_count"], cell)
    else:
        piece = debris_tile.whole(tile, cell)
    lod = params["lod"]
    return {
        "lods": piece["lods"],
        "switch_distances_m": [0.0, lod["lod1_switch_m"]],
        "thin_wire": [False, False],
        "cull_distance_m": lod["cull_m"],
        "materials": piece["materials"],
        "collision": piece["collision"],
        "gaps": [], "wind_volumes": [], "holes": [],
        "snag": piece["snag"],
    }
