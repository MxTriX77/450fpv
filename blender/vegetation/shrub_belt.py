"""The belt's shrub understory: notes V1, "shrub understory 1-3 m, with branches from about 1 m up".

2.4 m of many thin whippy stems from one root - blackthorn, elder and acacia suckers - the layer that fills a belt
between the trunks and hides what is on the ground under it. It is a snag, not an obstacle: the drone goes through
it, the fiber does not.

    blender -b --factory-startup --python blender/vegetation/shrub_belt.py   (or tools/blender/export.py --build)

Collision is one cylinder around the stem cluster. Every stem is far under 5 cm, so nothing else is solid
(tools/blender/treekit.py); the bush stops the fiber through `snag_hazard`, not the drone.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import treekit  # noqa: E402

SEED = 8890

kit.reset()
profile = treekit.Profile(
    height=2.4, trunk_radius=0.05, fork=0.45, crown_radius=1.15, primaries=7, bark="bark_dark",
    stagger=0.5, limb=0.72, taper=0.62, lean=6.0, spread=19.0, split=26.0, children=(3, 3),
    dry=0.2, bare=0.1, density=0.55, card_scale=0.55, porosity=0.5, impostor=treekit.GREEN)
lods, materials, entry = treekit.grow(
    profile, SEED, "res://assets/models/vegetation/shrub_belt.glb", [15.0, 40.0, 95.0], ("#5b534a", 0.88))
kit.finish(__file__, "tree", lods, materials, entry)
