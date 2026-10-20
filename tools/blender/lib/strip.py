"""The wall-space strip of a cut's material (p1-e01-trench, design decision 7): one map laid along the run, u the
chainage in metres and v metres up the map, which the generator UVs every cut face into and tools/assets/derive.py
composes its wall mask on, so the map's bands and the faces never disagree. Plain Python, no Blender.

The strip spans U_M x V_M metres from its bottom-left corner (the importer tiles a wall-space ground recipe 1/U_M by
1/V_M with no offset). Three rows, bottom to top: the floor (FLOOR_ROW, v = its bottom + the lateral offset from the
floor's right edge), the field wall (FIELD_ROW) and the belt wall (BELT_ROW), each wall row's v its top less the depth
below the lip, so the lip is at the row's top and the map reads upright on the wall (AST-07). u runs along the run's
chainage, the run at most RUN_M; the two end walls take a slot of END_SLOT_M each past it (from END_U_M); the niches'
inner faces take the wall's map at their own chainage."""

U_M = 27.0          # the strip's length along u: the run and the end walls' slots (assumed: 2048 px, 76 px/m)
RUN_M = 24.6        # the longest run the strip holds
END_U_M = RUN_M     # the end walls' slots start here
END_SLOT_M = 1.2    # an end wall's slot: the widest lip, 1.0 m, and its crumble
V_M = 4.0           # the strip's height: three rows (1024 px at 256 px/m)
FLOOR_ROW = (0.0, 0.8)    # (bottom, top) of the floor row: the floor's 0.6 m with 0.1 m spare each side
FIELD_ROW = (0.8, 2.4)    # the field wall: the lip at its top, 1.6 m down to its bottom
BELT_ROW = (2.4, 4.0)     # the belt wall: same layout
SPILL_M = 0.2       # the clods' band: the bottom of each wall row, where the clod spill lies
TOPSOIL_ROOTS_M = 0.2  # the root fringe: the top of each wall row


def wall_uv(side, u, depth):
    """The UV of a point on a wall: `side` +1 the belt wall (the run's left), -1 the field wall (its right), `u` the
    chainage (or a spare u past the run), `depth` metres below the lip."""
    top = BELT_ROW[1] if side > 0 else FIELD_ROW[1]
    return (u, top - depth)


def floor_uv(u, lateral):
    """The UV of a point on the floor: `lateral` metres from the floor's centre line, positive to the belt side."""
    return (u, (FLOOR_ROW[0] + FLOOR_ROW[1]) / 2 + lateral)
