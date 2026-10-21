"""debris_timber: one sawn-timber piece lying on the ground (object sheet docs/object-sheets/lines-roads-debris.md
§K03; p1-h01-adobe-house, design decision 10): a board, a batten or a sawn rafter, splintered at none, one or both
ends, with nails standing proud. The pieces are lib.debris_timber's library builders, which the `house` generator
composes as its fallen timber; this generator builds one of them as a catalog asset. Plain Python.

Params (assets-src/debris/K03_<piece>.json) are PARAMS below; a key the piece does not use must be 0 (check). The
sheet's round rafters, beams, panels and log sections are range-checked and refused, "not built yet". Frame (asset
space, lib.frame): x along the piece, lying on its wide face, centred on the origin in plan and resting on y = 0,
PLANTED (it lies on the ground, which covers a splinter's capsule where it dips under it).

Collision (the sheet): one box for the solid body, a capsule per splinter and two per nail. Snags: `hook` on every
splinter and nail, `edge` round each broken end. Slots: timber (the weathered faces), timber_fracture (the faces the
break opened, paler) and nail (steel). LOD1 drops the nails and replaces each end's splinters with one tapered block;
the cull distance is the sheet's 2 px rule applied to the piece's area-equivalent size, 935 * sqrt(length * w) / 2
(derived: a long thin piece reads by its area, not its longest side).
"""
import random

from lib import debris_timber

PLANTED = True
PIECES = ("board", "batten", "rafter_sawn", "rafter_round", "beam", "panel", "log_section")
BUILT = ("board", "batten", "rafter_sawn")
# The sheet's dimensions per piece: the section's wide face w and its depth h (lying on w, so w >= h), and length.
SECTIONS = {"board": ((0.10, 0.20), (0.025, 0.040), (0.3, 6.0)),
            "batten": ((0.05, 0.10), (0.025, 0.05), (0.3, 4.0)),
            "rafter_sawn": ((0.10, 0.20), (0.05, 0.15), (1.0, 6.0))}
SPLINTER_LEN_M = (0.05, 0.4)  # the sheet
SPLINTERS = (2, 10)           # the sheet
NAIL_PROUD_MM = (20.0, 80.0)  # the sheet
MIN_BODY_M = 0.1              # a broken piece keeps this much solid body (assumed)

PARAMS = {
    "piece": {"type": "str", "range": ", ".join(PIECES), "default": "board",
              "effect": f"which piece; {', '.join(p for p in PIECES if p not in BUILT)} are not built yet"},
    "seed": {"type": "int", "range": (0, 2**31 - 1), "default": 1,
             "effect": "the splinters, the nails and the grain's offset"},
    "section_m.w": {"type": "float", "range": (0.025, 0.25), "default": 0.15,
                    "effect": "the wide face, across the piece as it lies"},
    "section_m.h": {"type": "float", "range": (0.016, 0.25), "default": 0.032, "effect": "the depth, up"},
    "length_m": {"type": "float", "range": (0.3, 6.0), "default": 1.5, "effect": "tip to tip"},
    "break_ends": {"type": "int", "range": (0, 2), "default": 1, "effect": "splintered ends, +x first"},
    "splinter_len_m": {"type": "float", "range": (0.0, 0.4), "default": 0.2,
                       "effect": "the longest splinter past the solid body (0.05-0.4); 0 when no end is broken"},
    "splinter_count": {"type": "int", "range": (0, 10), "default": 5,
                       "effect": "splinters per broken end (2-10); 0 when no end is broken"},
    "nails": {"type": "int", "range": (0, 10), "default": 2, "effect": "nails standing from the top face"},
    "nail_proud_mm": {"type": "float", "range": (0.0, 80.0), "default": 50.0,
                      "effect": "how far each nail stands (20-80); 0 without nails"},
    "physics_material": {"type": "str", "range": "timber", "default": "timber",
                         "effect": "the wood's collision material (the sheet: timber); nails are steel"},
    "lod.lod1_switch_m": {"type": "float", "range": (0.5, 50.0), "default": 6.0, "effect": "LOD1 from here"},
    "lod.cull_m": {"type": "float", "range": (5.0, 600.0), "default": 220.0, "effect": "culled beyond"},
}


def check(params):
    """The piece is built, its section and length lie in its class, a broken end has its splinters and an unbroken
    piece none, the body is left whole between the breaks with room for its nails, the material is timber and the
    LODs are ordered."""
    piece = params["piece"]
    if piece not in PIECES:
        return [f"'piece' = {piece!r} must be one of {', '.join(PIECES)}"]
    if piece not in BUILT:
        return [f"'piece' = {piece!r} is not built yet (built: {', '.join(BUILT)})"]
    problems = []
    (w_range, h_range, l_range) = SECTIONS[piece]
    w, h, length = params["section_m"]["w"], params["section_m"]["h"], params["length_m"]
    for key, value, (lo, hi) in (("section_m.w", w, w_range), ("section_m.h", h, h_range),
                                 ("length_m", length, l_range)):
        if not lo <= value <= hi:
            problems.append(f"'{key}' = {value} must lie in [{lo}, {hi}] for a {piece}")
    if h > w:
        problems.append(f"'section_m.h' = {h} exceeds 'section_m.w' = {w}: the piece lies on its wide face")
    ends, ell, count = params["break_ends"], params["splinter_len_m"], params["splinter_count"]
    if ends == 0:
        for key, value in (("splinter_len_m", ell), ("splinter_count", count)):
            if value != 0:
                problems.append(f"'{key}' = {value} is not used without a broken end; set it to 0")
    else:
        if not SPLINTER_LEN_M[0] <= ell <= SPLINTER_LEN_M[1]:
            problems.append(f"'splinter_len_m' = {ell} must lie in {list(SPLINTER_LEN_M)} for a broken end")
        if not SPLINTERS[0] <= count <= SPLINTERS[1]:
            problems.append(f"'splinter_count' = {count} must lie in {list(SPLINTERS)} for a broken end")
    body = length - ends * ell
    need = max(MIN_BODY_M, 2 * debris_timber.NAIL_END_M if params["nails"] else 0.0)
    if body < need:
        problems.append(f"'length_m' = {length} leaves {body:.3f} m of solid body between the breaks, under {need} m")
    nails, proud = params["nails"], params["nail_proud_mm"]
    if nails == 0 and proud != 0:
        problems.append(f"'nail_proud_mm' = {proud} is not used without nails; set it to 0")
    if nails and not NAIL_PROUD_MM[0] <= proud <= NAIL_PROUD_MM[1]:
        problems.append(f"'nail_proud_mm' = {proud} must lie in {list(NAIL_PROUD_MM)}")
    if params["physics_material"] != debris_timber.PHYSICS:
        problems.append(f"'physics_material' = {params['physics_material']!r} must be {debris_timber.PHYSICS!r}")
    if params["lod"]["lod1_switch_m"] >= params["lod"]["cull_m"]:
        problems.append("'lod.lod1_switch_m' must lie under 'lod.cull_m'")
    return problems


def build(params):
    rng = random.Random(f"debris_timber {params['piece']} {params['seed']}")
    section = (params["section_m"]["w"], params["section_m"]["h"])
    piece = debris_timber.piece(rng, section, params["length_m"], params["break_ends"], params["splinter_len_m"],
                                params["splinter_count"], params["nails"], params["nail_proud_mm"] / 1000)
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
