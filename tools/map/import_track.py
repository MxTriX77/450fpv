"""Reads the pilot's saved track and race files into the neutral layout maps/hunter-09/layout.json (w20-hunter-track
1.2, D-123).

    python tools/map/import_track.py --track FILE --race FILE [--out maps/hunter-09]

The two paths come only from the command line and are the only inputs it opens. It stops, writing nothing, on an item
id with no kind or with two, and on a race that is not one linear path from its start to its finish.

The layout's frame is the file's, unchanged (Unity's: left-handed, +Y up, metres):
- objects: one row per row of the file, in the file's order. instance_id is the file's and is not unique, because the
  file can hold the same row twice. position_m and scale are the file's numbers. rotation_xyzw is Unity's
  Quaternion.Euler of the file's angles (Z, then X, then Y), rounded to 9 decimals. scale is there only where the file
  has one; points only on a ribbon, whose own position is 0.
- race: the spawn point's instance id, the laps, and the passages in race order, which follows the file's links and
  is not the order of its rows.
The output holds no item id from the file, and the same input gives the same bytes.
"""
import argparse
import collections
import json
import math
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "maps" / "hunter-09"
MAP_ID = "hunter-09"

# id fragment: neutral kind. No fragment is a whole item id, so led_flag's stops short of the id's last digit.
DRAWN = {
    "ConcreteBarrier": "concrete_block", "Pylon300cm": "pylon_3m", "LightConeGreen": "cone_light",
    "TireRed": "tyre_red", "TireBlank": "tyre_plain", "TireBlue": "tyre_blue", "TireYellow": "tyre_yellow",
    "TapeYellowBlack": "ribbon_tape", "8x10Frame": "frame_8x10", "10x10Frame01": "frame_square_a",
    "10x10Frame02": "frame_square_b", "LightGate300x220": "light_gate", "SteelProfile": "steel_profile",
    "LedFlagGreen": "led_flag_green", "LedFlag0": "led_flag", "LightRigBase": "light_rig",
    "DirectionArrowRed": "arrow_red", "OSB220": "board_large", "OSB100": "board_small", "TrackLight": "track_light",
    "LEDBall": "led_ball", "LightCurve": "light_curve", "PartyTent": "tent", "NoticeBoard": "notice_board",
}
DATA_ONLY = {
    "CheckpointBoxFlexible": "volume_box_flex", "CheckpointBox2mX4m": "volume_box_2x4",
    "CheckpointSphereFlexible": "volume_sphere_flex", "SpawnPoint": "spawn_point",
    "PlayerPosition": "player_position", "TriggerBox": "trigger_repair",
}
PASSAGE_TYPES = {"Start": "start", "Pass": "pass", "Finish": "finish"}
DIRECTIONS = {"LeftToRight": "left_to_right", "RightToLeft": "right_to_left"}


class Refused(Exception):
    """The input cannot be imported faithfully; the message says why."""


def unity_quaternion(x_deg, y_deg, z_deg):
    """Unity's Quaternion.Euler as [x, y, z, w]: Z first, then X, then Y (q = qy * qx * qz)."""
    sx, cx = math.sin(math.radians(x_deg) / 2), math.cos(math.radians(x_deg) / 2)
    sy, cy = math.sin(math.radians(y_deg) / 2), math.cos(math.radians(y_deg) / 2)
    sz, cz = math.sin(math.radians(z_deg) / 2), math.cos(math.radians(z_deg) / 2)
    q = (cy * sx * cz + sy * cx * sz,
         sy * cx * cz - cy * sx * sz,
         cy * cx * sz - sy * sx * cz,
         cy * cx * cz + sy * sx * sz)
    return [round(v, 9) + 0.0 for v in q]  # + 0.0 turns -0.0 into 0.0


def vector(element):
    return [float(element.findtext(axis)) + 0.0 for axis in "xyz"]


def pose(element):
    return {"position_m": vector(element.find("position")),
            "rotation_xyzw": unity_quaternion(*vector(element.find("rotation")))}


def kinds_of(items):
    """{item id: (kind, drawn)}, or Refused naming every id with no kind or with two."""
    table = {f: (k, True) for f, k in DRAWN.items()} | {f: (k, False) for f, k in DATA_ONLY.items()}
    found, bad = {}, []
    for item in sorted(items):
        hits = [table[f] for f in table if f in item]
        if len(hits) == 1:
            found[item] = hits[0]
        else:
            bad.append(f"{item} (matches {' and '.join(k for k, _ in hits)})" if hits else f"{item} (no kind)")
    if bad:
        raise Refused("item ids without exactly one kind: " + ", ".join(bad))
    return found


def read_objects(path):
    rows = list(ET.fromstring(Path(path).read_bytes()).find("blueprints"))
    kinds = kinds_of({row.findtext("itemID") for row in rows})
    objects = []
    for row in rows:
        kind, drawn = kinds[row.findtext("itemID")]
        out = {"instance_id": int(row.findtext("instanceID")), "kind": kind, "drawn": drawn, **pose(row)}
        if row.find("scale") is not None:
            out["scale"] = vector(row.find("scale"))
        if row.find("attachPoints") is not None:
            out["points"] = [pose(point) for point in row.find("attachPoints")]
        objects.append(out)
    return objects


def read_race(path, objects):
    root = ET.fromstring(Path(path).read_bytes())
    passages = {p.findtext("uniqueId"): p for p in root.find("checkPointPassages")}
    starts = [u for u, p in passages.items() if p.findtext("passageType") == "Start"]
    if len(starts) != 1:
        raise Refused(f"the race has {len(starts)} start passages, not 1")

    walk, at = [], starts[0]
    while at is not None:
        if at not in passages or at in walk:
            raise Refused("the race's links leave the file or loop")
        walk.append(at)
        nexts = [n.text for n in passages[at].find("nextPassageIDs")]
        if len(nexts) > 1:
            raise Refused("the race branches")
        at = nexts[0] if nexts else None
    if len(walk) != len(passages) or passages[walk[-1]].findtext("passageType") != "Finish":
        raise Refused(f"the walk from the start covers {len(walk)} of {len(passages)} passages or misses the finish")

    spawn = int(root.findtext("spawnPointID"))
    rows_with = collections.Counter(o["instance_id"] for o in objects)
    named = {spawn} | {int(p.findtext("checkPointID")) for p in passages.values()}
    unclear = sorted(i for i in named if rows_with[i] != 1)
    if unclear:
        raise Refused(f"the race names instance ids that are not exactly one object: {unclear}")

    try:
        order = [{"checkpoint_instance_id": int(passages[u].findtext("checkPointID")),
                  "type": PASSAGE_TYPES[passages[u].findtext("passageType")],
                  "direction": DIRECTIONS[passages[u].findtext("directionality")]} for u in walk]
    except KeyError as e:
        raise Refused(f"unknown passage type or direction: {e}") from e
    return {"spawn_point_instance_id": spawn, "laps": int(root.findtext("requiredLaps")), "passages": order}


def render(objects, race):
    def rows(items, indent):
        return ",\n".join(indent + json.dumps(r, ensure_ascii=False, separators=(", ", ": ")) for r in items)
    return (
        "{\n"
        '  "format": "fpv450-track-layout",\n'
        '  "format_version": 1,\n'
        f'  "map": "{MAP_ID}",\n'
        '  "frame": "unity-lh-yup",\n'
        f'  "objects": [\n{rows(objects, "    ")}\n  ],\n'
        '  "race": {\n'
        f'    "spawn_point_instance_id": {race["spawn_point_instance_id"]},\n'
        f'    "laps": {race["laps"]},\n'
        f'    "passages": [\n{rows(race["passages"], "      ")}\n    ]\n'
        "  }\n"
        "}\n")


def main(argv=None):
    ap = argparse.ArgumentParser(description="Import the pilot's track and race files into the neutral layout.")
    ap.add_argument("--track", required=True, help="the saved track file")
    ap.add_argument("--race", required=True, help="the saved race file")
    ap.add_argument("--out", type=Path, default=OUT, help="the map's folder (default: maps/hunter-09)")
    args = ap.parse_args(argv)
    try:
        objects = read_objects(args.track)
        race = read_race(args.race, objects)
    except Refused as e:
        print(f"import_track: refused: {e}", file=sys.stderr)
        return 1
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "layout.json").write_bytes(render(objects, race).encode("utf-8"))
    drawn = sum(o["drawn"] for o in objects)
    checkpoints = len({p["checkpoint_instance_id"] for p in race["passages"]})
    print(f"import_track: {len(objects)} objects, {drawn} drawn, {len(objects) - drawn} data only; "
          f"{len(race['passages'])} passages through {checkpoints} checkpoints")
    return 0


if __name__ == "__main__":
    sys.exit(main())
