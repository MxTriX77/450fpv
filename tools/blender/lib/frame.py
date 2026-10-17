"""Frames: asset space is Unity's (D-019); Blender's frame is an export detail.

Asset space, used by every generator and every metadata number: Unity's world frame, metres, +X east, +Y up,
+Z north, left-handed. Nothing downstream converts (design decision 5).

Blender is right-handed Z-up. lib.export writes the FBX with the add-on's default axes (forward -Z, up Y), the
transform baked into the vertices and units applied: a Maya-style right-handed Y-up file in centimetres with an
identity node, in which a Blender point (x, y, z) lands as (x, z, -y) * 100 (build.py parses the export to check
it). Unity's importer takes such a right-handed file into its left-handed frame by negating X, the convention of
Unity's own FBX exporter/importer pair (an assumption until task 1.3 sees it in Unity), and "Convert Units" scales by
UnitScaleFactor / 100. Hence Blender (x, y, z) -> Unity (-x, z, -y), and a point authored in asset space is placed in
Blender at (-x, -z, y). Both maps mirror, so lib.export settles each face's winding from its outward normal instead of
carrying the generator's order over.
"""


def unity_to_blender(p):
    """A point or direction of asset space in Blender's frame."""
    return (-p[0], -p[2], p[1])


def blender_to_unity(p):
    """The inverse of unity_to_blender."""
    return (-p[0], p[2], -p[1])


def fbx_to_unity(p, unit_scale_factor):
    """A point of the exported file's scene space as Unity imports it: X negated, file units to metres."""
    k = unit_scale_factor / 100.0
    return (-p[0] * k, p[1] * k, p[2] * k)
