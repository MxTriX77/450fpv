"""The first attempt as a read-only source for the stand-ins (p6-test-range design 2 and 3; D-027: ported, never
merged), in plain Python.

Source: a file of the Godot commit is read with `git show <commit>:<path>`; a model there is a Git LFS pointer, whose
object is read from the clone's shared LFS store (<git common dir>/lfs/objects/<oid[0:2]>/<oid[2:4]>/<oid>) and
checked against the pinned SHA-256. The Godot ref is fetched read-only (`git fetch origin main:refs/godot/main`,
docs/handover-2026-10-22.md); nothing of it is checked out or merged.

Frames: Godot's is right-handed, +X east, +Y up, +Z south, and its glTF import keeps a model's coordinates as they
are, so the model and the catalog entry share it (the house's door gap at z = +3.0 sits on the wall its mesh opens).
Asset space is Unity's (lib.frame), +Z north: a Godot point (x, y, z) is (x, y, -z), the mirror S = diag(1, 1, -1),
the same for the mesh and for every metadata number. A rotation R becomes S R S, which for a unit quaternion
[x, y, z, w] is [-x, -y, z, w]. Godot's `rotation_deg` is [yaw, pitch, roll], applied as yaw about +Y, then pitch
about the new +X, then roll about the new +Z: R = Ry(yaw) Rx(pitch) Rz(roll), positive yaw turning +X toward -Z
(game/maps/README.md and Axes.FromEuler at the pinned commit). Capsules and cylinders stand along +Y, a capsule's
height_m is its total length, caps included, as in lib.meta. A gap is a rectangle facing asset +Z turned by its
yaw_deg: width along the turned +X, height along +Y about center_m.

The catalog entry's own rules carried over (game/maps/README.md): a shape takes the entry's material unless it names
its own; without a `wind_volume` the collision shapes are the wind volume; a volume of porosity 1 blocks nothing
(the Godot wind grid skips it), which asset space writes as no wind volume (lib.meta: empty means the asset blocks no
wind). The catalog has no landable flag: every shape is written not landable (informational only, asset-pipeline)."""
import hashlib
import json
import math
import os
import subprocess

CATALOG = "game/assets/catalog.json"


class SourceError(RuntimeError):
    """The Godot source cannot be read as pinned; the message names the commit, the path and what to do."""


def git(root, *args):
    """The stdout bytes of `git -C <root> <args>`; SourceError naming the command when it fails."""
    done = subprocess.run(["git", "-C", str(root), *args], capture_output=True)
    if done.returncode != 0:
        raise SourceError(f"git {' '.join(args)}: {done.stderr.decode('utf-8', 'replace').strip()} (the Godot ref "
                          "is fetched read-only with `git fetch origin main:refs/godot/main`)")
    return done.stdout


def show(root, commit, path):
    return git(root, "show", f"{commit}:{path}")


def catalog_entry(root, commit, key):
    """The asset `key` of game/assets/catalog.json at `commit`."""
    assets = json.loads(show(root, commit, CATALOG).decode("utf-8"))["assets"]
    if key not in assets:
        raise SourceError(f"{CATALOG} at {commit[:7]} has no asset {key!r}")
    return assets[key]


def lfs_object(root, commit, path, sha256):
    """The bytes of the LFS object that `path` points to at `commit`, which must be `sha256` both in the pointer and
    in the object's own hash."""
    pointer = show(root, commit, path).decode("utf-8", "replace").splitlines()
    oids = [line.split(":", 1)[1] for line in pointer if line.startswith("oid sha256:")]
    if len(oids) != 1:
        raise SourceError(f"{path} at {commit[:7]} is not a Git LFS pointer")
    if oids[0] != sha256:
        raise SourceError(f"{path} at {commit[:7]} points to {oids[0]}, the params pin {sha256}")
    common = git(root, "rev-parse", "--path-format=absolute", "--git-common-dir").decode("utf-8").strip()
    store = os.path.join(common, "lfs", "objects", sha256[:2], sha256[2:4], sha256)
    if not os.path.isfile(store):
        raise SourceError(f"the LFS object {sha256} of {path} at {commit[:7]} is not in {os.path.dirname(store)}: "
                          "fetch it with `git lfs fetch origin main`")
    with open(store, "rb") as f:
        data = f.read()
    if hashlib.sha256(data).hexdigest() != sha256:
        raise SourceError(f"{store} does not hash to its name; the LFS store is damaged")
    return data


# --- the frame change -------------------------------------------------------------------------------------------

def point(p):
    """A Godot point or direction in asset space."""
    return [float(p[0]), float(p[1]), -float(p[2])]


def quaternion(rotation_deg):
    """The asset-space unit quaternion [x, y, z, w] of a Godot [yaw, pitch, roll] in degrees, w >= 0."""
    yaw, pitch, roll = rotation_deg
    x, y, z, w = _product(_product(_turn((0.0, 1.0, 0.0), yaw), _turn((1.0, 0.0, 0.0), pitch)),
                          _turn((0.0, 0.0, 1.0), roll))
    q = [-x, -y, z, w]
    return [-c for c in q] if q[3] < 0 else q


def shape(godot_shape):
    """A Godot collision or wind-volume shape's fields in asset space, in lib.meta's key order: shape, center, size or
    radius, height, rotation (none for a sphere)."""
    kind = godot_shape["shape"]
    if kind not in ("box", "sphere", "capsule", "cylinder"):
        raise ValueError(f"a Godot shape {kind!r} has no asset-space form")
    out = {"shape": kind, "center": point(godot_shape.get("position_m", (0.0, 0.0, 0.0)))}
    if kind == "box":
        out["size"] = [float(s) for s in godot_shape["size_m"]]
    else:
        out["radius"] = float(godot_shape["radius_m"])
    if kind in ("capsule", "cylinder"):
        out["height"] = float(godot_shape["height_m"])
    if kind != "sphere":
        out["rotation"] = quaternion(godot_shape.get("rotation_deg", (0.0, 0.0, 0.0)))
    return out


def collision(entry):
    """The entry's collision shapes, each with its material (its own or the entry's) and not landable."""
    return [dict(shape(s), material=s.get("material", entry["material"]), landable=False)
            for s in entry.get("collision", [])]


def wind_volumes(entry):
    """The entry's wind volumes with its porosity: its `wind_volume`, or its collision shapes without one; none when
    the porosity is 1."""
    porosity = float(entry["wind_porosity"])
    if porosity >= 1.0:
        return []
    return [dict(shape(s), porosity=porosity) for s in entry.get("wind_volume", entry.get("collision", []))]


def gap(godot_gap):
    """A Godot gap rectangle as an asset-space gap polygon: its four corners in order round it, the normal its
    face's, narrowest_m its smaller side (a rectangle's largest inscribed circle)."""
    c, half_w, half_h = godot_gap["center_m"], godot_gap["width_m"] / 2, godot_gap["height_m"] / 2
    yaw = math.radians(godot_gap["yaw_deg"])
    across, normal = (math.cos(yaw), 0.0, -math.sin(yaw)), (math.sin(yaw), 0.0, math.cos(yaw))
    corners = [[c[a] + sx * half_w * across[a] + (sy * half_h if a == 1 else 0.0) for a in range(3)]
               for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    return {"id": godot_gap["name"], "polygon": [point(p) for p in corners], "normal": point(normal),
            "narrowest_m": float(min(godot_gap["width_m"], godot_gap["height_m"]))}


def hole(record):
    """A hole record (lib.meta HOLE_KEYS) given in Godot's frame, in asset space."""
    return {"id": record["id"], "centre_line": [point(p) for p in record["centre_line"]],
            "top_width_m": float(record["top_width_m"]), "depth_m": float(record["depth_m"]),
            "band_width_m": float(record["band_width_m"])}


def _turn(axis, degrees):
    half = math.radians(degrees) / 2
    s = math.sin(half)
    return (axis[0] * s, axis[1] * s, axis[2] * s, math.cos(half))


def _product(a, b):
    """The Hamilton product a b of quaternions [x, y, z, w]: the rotation b, then a."""
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)
