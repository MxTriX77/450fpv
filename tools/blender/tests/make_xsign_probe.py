"""Builds the X-sign probe fixture (design decision 5, physics review R1): an asymmetric box x in [0, 0.3],
y in [0, 0.1], z in [0, 0.2] in asset space, exported through lib.export exactly as every asset is, to
unity/Assets/_Project/Tests/EditMode/Fixtures/xsign_probe.fbx. XSignProbeTests.cs asserts that Unity imports it
with min (0, 0, 0) and max (0.3, 0.1, 0.2) and wound outward, so a wrong sign in lib.frame fails the EditMode suite.

    blender --background --factory-startup --python tools/blender/tests/make_xsign_probe.py

The fixture is committed (through LFS); rerun this after any change to lib/export.py or lib/frame.py.
"""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS_BLENDER = os.path.dirname(HERE)
REPO_ROOT = os.path.dirname(os.path.dirname(TOOLS_BLENDER))
FIXTURE = os.path.join("unity", "Assets", "_Project", "Tests", "EditMode", "Fixtures", "xsign_probe.fbx")
NAME = "xsign_probe"
SIZE = (0.3, 0.1, 0.2)
sys.path.insert(0, TOOLS_BLENDER)  # lib/ and build.py import from here inside Blender


def probe_mesh(size=SIZE):
    """The box from the origin to `size`: eight vertices, six faces with outward normals, one slot 'probe'."""
    from lib.geometry import Mesh, ring
    mesh = Mesh()
    corners = {(x, y, z): mesh.vertex((size[0] * x, size[1] * y, size[2] * z))
               for x in (0, 1) for y in (0, 1) for z in (0, 1)}
    for axis in range(3):
        for side in (0, 1):
            normal = tuple((1 if side else -1) if a == axis else 0 for a in range(3))
            indices = [i for c, i in corners.items() if c[axis] == side]
            mesh.face(ring(mesh.vertices, indices, normal), normal, "probe")
    return mesh


def main(argv):
    import bpy
    import build
    from lib import export, fbxread
    parser = argparse.ArgumentParser(prog="make_xsign_probe.py", description=__doc__.split("\n\n")[0])
    parser.add_argument("--root", default=REPO_ROOT, help=argparse.SUPPRESS)
    args = parser.parse_args(argv)
    build.check_version(bpy.app.version)
    path = os.path.join(args.root, FIXTURE)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    export.reset_scene()
    export.add_lod(NAME, probe_mesh())
    export.export_fbx(path)
    lo, hi = fbxread.unity_bounds(path)[NAME]
    if any(abs(lo[a]) > 1e-6 or abs(hi[a] - SIZE[a]) > 1e-6 for a in range(3)):
        raise RuntimeError(f"the file reads back as min {lo} max {hi}, not the origin to {SIZE}: lib.frame and "
                           "lib.fbxread disagree")
    lo, hi = (tuple(round(v, 6) + 0.0 for v in p) for p in (lo, hi))
    print(f"make_xsign_probe.py: wrote {os.path.relpath(path, args.root)}; read back as Unity will: min {lo} max {hi}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []))
