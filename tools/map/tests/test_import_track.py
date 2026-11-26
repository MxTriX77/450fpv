"""The track import (tools/map/import_track.py, w20-hunter-track 1.2): the rotation convention, ribbons, the kind table's
refusals, byte-identical reruns and the race walk, on a small invented fixture. One check runs on the pilot's real
files, only when HUNTER_TRACK_FILE and HUNTER_RACE_FILE give their paths; nothing else knows where they are."""
import collections
import json
import math
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import import_track as imp  # noqa: E402

COMMITTED = imp.REPO / "maps" / "hunter-09" / "layout.json"
REAL_TRACK, REAL_RACE = os.environ.get("HUNTER_TRACK_FILE"), os.environ.get("HUNTER_RACE_FILE")

CONCRETE, FRAME, VOLUME, SPAWN, RIBBON = ("ZzConcreteBarrierQ", "ZzFloat10x10Frame01Q", "ZzCheckpointBoxFlexibleQ",
                                          "ZzSpawnPointQ", "ZzTapeYellowBlackQ")


def xyz(tag, v):
    return f"<{tag}><x>{v[0]}</x><y>{v[1]}</y><z>{v[2]}</z></{tag}>"


def blueprint(item, instance, position=(0, 0, 0), rotation=(0, 0, 0), scale=None, points=()):
    extra = xyz("scale", scale) if scale else ""
    if points:
        extra += "<attachPoints>" + "".join(
            f"<RibbonAttachPoint>{xyz('position', p)}{xyz('rotation', r)}<attachItemName>Zz{i}</attachItemName>"
            "</RibbonAttachPoint>" for i, (p, r) in enumerate(points)) + "</attachPoints>"
    return (f"<TrackBlueprint><itemID>{item}</itemID><instanceID>{instance}</instanceID>"
            f"{xyz('position', position)}{xyz('rotation', rotation)}{extra}</TrackBlueprint>")


RIBBON_POINTS = (((1.5, 2, 3), (0, 90, 0)), ((4, 5.25, 6), (0, 0, 0)))
ROWS = [
    blueprint(CONCRETE, 1, (1.5, 2.25, 3.125), (0, 90, 0)),
    blueprint(FRAME, 2, (10, 20, 30), (-0, 45, 0)),
    blueprint(VOLUME, 3, (7, 8, 9), (0, 0, 0), scale=(2, 3.5, 4)),
    blueprint(SPAWN, 4, (-1.90734863E-06, 0, 1)),
    blueprint(RIBBON, 5, points=RIBBON_POINTS),
    blueprint(CONCRETE, 1, (1.5, 2.25, 3.125), (0, 90, 0)),  # the real file holds some rows twice: both are kept
]


def passage(uid, checkpoint, kind, direction, *nexts):
    links = "".join(f"<string>{n}</string>" for n in nexts)
    return (f"<RaceCheckpointPassage><uniqueId>{uid}</uniqueId><checkPointID>{checkpoint}</checkPointID>"
            f"<checkPointSubID /><passageType>{kind}</passageType><directionality>{direction}</directionality>"
            f"<nextPassageIDs>{links}</nextPassageIDs></RaceCheckpointPassage>")


# The last row is linked in before the finish, as in the real file: row order is not race order.
PASSAGES = [
    passage("a", 2, "Start", "LeftToRight", "b"),
    passage("b", 3, "Pass", "RightToLeft", "d"),
    passage("c", 3, "Finish", "LeftToRight"),
    passage("d", 2, "Pass", "LeftToRight", "c"),
]


def write(tmp_path, rows=ROWS, passages=PASSAGES, spawn=4):
    track, race = tmp_path / "t.xml", tmp_path / "r.xml"
    track.write_text(f'<?xml version="1.0"?><Track><blueprints>{"".join(rows)}</blueprints></Track>', encoding="utf-8")
    race.write_text(f'<?xml version="1.0"?><Race><requiredLaps>1</requiredLaps><spawnPointID>{spawn}</spawnPointID>'
                    f'<checkPointPassages>{"".join(passages)}</checkPointPassages></Race>', encoding="utf-8")
    return track, race


def run(track, race, out):
    return imp.main(["--track", str(track), "--race", str(race), "--out", str(out)])


def load(out):
    return json.loads((out / "layout.json").read_text(encoding="utf-8"))


def matrix_of_euler(x_deg, y_deg, z_deg):
    """Ry * Rx * Rz from the elementary rotations, independent of the import's closed form."""
    def mul(a, b):
        return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    x, y, z = map(math.radians, (x_deg, y_deg, z_deg))
    rx = [[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]]
    ry = [[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]
    rz = [[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]]
    return mul(ry, mul(rx, rz))


def matrix_of_quaternion(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def angle_between_deg(a, b):
    """The angle of a^T * b. atan2 of its skew and trace parts stays accurate near 0, where acos of the trace does not."""
    r = [[sum(a[k][i] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    skew = math.hypot(r[2][1] - r[1][2], r[0][2] - r[2][0], r[1][0] - r[0][1])
    return math.degrees(math.atan2(skew / 2, (r[0][0] + r[1][1] + r[2][2] - 1) / 2))


def test_euler_angles_turn_z_then_x_then_y():
    s = math.sqrt(0.5)
    assert imp.unity_quaternion(90, 0, 0) == pytest.approx([s, 0, 0, s])
    assert imp.unity_quaternion(0, 90, 0) == pytest.approx([0, s, 0, s])
    assert imp.unity_quaternion(0, 0, 90) == pytest.approx([0, 0, s, s])
    for euler in ((30, 45, 60), (359.3, 126.79, 359.51), (-12.5, 270, 178.29), (0.05, 270.93, 178.29)):
        q = imp.unity_quaternion(*euler)
        assert angle_between_deg(matrix_of_euler(*euler), matrix_of_quaternion(q)) < 1e-4, euler


def test_every_row_is_kept_with_its_pose_and_scale(tmp_path):
    track, race = write(tmp_path)
    assert run(track, race, tmp_path / "out") == 0
    objects = load(tmp_path / "out")["objects"]
    assert [o["instance_id"] for o in objects] == [1, 2, 3, 4, 5, 1]
    assert [o["kind"] for o in objects] == ["concrete_block", "frame_square_a", "volume_box_flex", "spawn_point",
                                            "ribbon_tape", "concrete_block"]
    assert [o["drawn"] for o in objects] == [True, True, False, False, True, True]
    assert objects[0]["position_m"] == [1.5, 2.25, 3.125]
    assert objects[3]["position_m"] == [-1.90734863e-06, 0.0, 1.0]
    assert objects[2]["scale"] == [2.0, 3.5, 4.0]
    assert sum("scale" in o for o in objects) == 1, "a scale only where the file has one"


def test_a_ribbon_keeps_its_points(tmp_path):
    track, race = write(tmp_path)
    run(track, race, tmp_path / "out")
    ribbon = load(tmp_path / "out")["objects"][4]
    assert ribbon["position_m"] == [0.0, 0.0, 0.0]
    assert [p["position_m"] for p in ribbon["points"]] == [[1.5, 2.0, 3.0], [4.0, 5.25, 6.0]]
    assert ribbon["points"][0]["rotation_xyzw"] == imp.unity_quaternion(0, 90, 0)
    assert ribbon["points"][1]["rotation_xyzw"] == [0.0, 0.0, 0.0, 1.0]


@pytest.mark.parametrize("item, why", [("ZzMysteryThing", "no kind"), ("ZzPylon300cmTireRedQ", "pylon_3m and tyre_red")])
def test_an_item_without_exactly_one_kind_is_refused(tmp_path, capsys, item, why):
    track, race = write(tmp_path, rows=ROWS + [blueprint(item, 9)])
    assert run(track, race, tmp_path / "out") == 1
    err = capsys.readouterr().err
    assert item in err and why in err
    assert not (tmp_path / "out" / "layout.json").exists(), "a refusal writes nothing"


def test_a_rerun_is_byte_identical(tmp_path):
    track, race = write(tmp_path)
    run(track, race, tmp_path / "a")
    run(track, race, tmp_path / "b")
    a = (tmp_path / "a" / "layout.json").read_bytes()
    assert a == (tmp_path / "b" / "layout.json").read_bytes()
    assert b"\r" not in a


def test_the_layout_carries_no_item_id(tmp_path):
    track, race = write(tmp_path)
    run(track, race, tmp_path / "out")
    text = (tmp_path / "out" / "layout.json").read_text(encoding="utf-8")
    assert not [i for i in (CONCRETE, FRAME, VOLUME, SPAWN, RIBBON) if i in text]
    assert "Zz" not in text, "nor a ribbon point's attached name"


def test_the_race_follows_its_links_not_its_rows(tmp_path):
    track, race = write(tmp_path)
    run(track, race, tmp_path / "out")
    got = load(tmp_path / "out")["race"]
    assert (got["spawn_point_instance_id"], got["laps"]) == (4, 1)
    assert got["passages"] == [
        {"checkpoint_instance_id": 2, "type": "start", "direction": "left_to_right"},
        {"checkpoint_instance_id": 3, "type": "pass", "direction": "right_to_left"},
        {"checkpoint_instance_id": 2, "type": "pass", "direction": "left_to_right"},
        {"checkpoint_instance_id": 3, "type": "finish", "direction": "left_to_right"},
    ]


@pytest.mark.parametrize("passages", [
    [passage("a", 2, "Start", "LeftToRight", "b", "c"), passage("b", 3, "Finish", "LeftToRight"),
     passage("c", 3, "Finish", "LeftToRight")],
    [passage("a", 2, "Start", "LeftToRight", "b"), passage("b", 3, "Pass", "LeftToRight", "a")],
    [passage("a", 2, "Start", "LeftToRight", "b"), passage("b", 3, "Pass", "LeftToRight")],
    [passage("a", 1, "Start", "LeftToRight", "b"), passage("b", 3, "Finish", "LeftToRight")],
], ids=["branch", "loop", "no-finish", "names-a-doubled-row"])
def test_a_race_that_is_not_one_clear_path_is_refused(tmp_path, passages):
    track, race = write(tmp_path, passages=passages)
    assert run(track, race, tmp_path / "out") == 1
    assert not (tmp_path / "out" / "layout.json").exists()


def test_both_paths_are_required():
    for argv in ([], ["--track", "x"], ["--race", "x"]):
        with pytest.raises(SystemExit) as e:
            imp.main(argv)
        assert e.value.code == 2


def test_it_reads_only_the_two_paths(tmp_path):
    track, race = write(tmp_path)
    run(track, race, tmp_path / "warm")  # lazy stdlib imports happen here, outside the audit
    state = {"on": True, "read": set()}

    def hook(event, args):
        if state["on"] and event == "open" and (args[1] is None or "r" in args[1]):
            state["read"].add(str(args[0]))
    sys.addaudithook(hook)  # an audit hook cannot be removed, so it switches itself off below
    try:
        run(track, race, tmp_path / "out")
    finally:
        state["on"] = False
    assert state["read"] == {str(track), str(race)}


KIND_COUNTS = {
    "concrete_block": 204, "pylon_3m": 15, "cone_light": 12, "tyre_red": 7, "tyre_plain": 1, "tyre_blue": 1,
    "tyre_yellow": 1, "ribbon_tape": 5, "frame_8x10": 5, "frame_square_a": 2, "frame_square_b": 2, "light_gate": 1,
    "steel_profile": 4, "led_flag_green": 3, "led_flag": 1, "light_rig": 3, "arrow_red": 2, "board_large": 2,
    "board_small": 1, "track_light": 2, "led_ball": 1, "light_curve": 1, "tent": 1, "notice_board": 1,
    "volume_box_flex": 36, "volume_box_2x4": 13, "volume_sphere_flex": 2, "spawn_point": 3, "player_position": 1,
    "trigger_repair": 1,
}


def numbers(element):
    return [float(element.findtext(axis)) for axis in "xyz"]


def assert_pose(row, element):
    assert all(abs(a - b) <= 0.001 for a, b in zip(row["position_m"], numbers(element.find("position"))))
    want = matrix_of_euler(*numbers(element.find("rotation")))
    assert angle_between_deg(want, matrix_of_quaternion(row["rotation_xyzw"])) <= 0.01


@pytest.mark.skipif(not (REAL_TRACK and REAL_RACE), reason="HUNTER_TRACK_FILE and HUNTER_RACE_FILE are not set")
def test_the_real_files(tmp_path):
    assert run(REAL_TRACK, REAL_RACE, tmp_path) == 0
    text = (tmp_path / "layout.json").read_text(encoding="utf-8")
    assert (tmp_path / "layout.json").read_bytes() == COMMITTED.read_bytes(), "stale: rerun tools/map/import_track.py"
    layout = json.loads(text)
    objects = layout["objects"]

    assert len(objects) == 334
    assert sum(o["drawn"] for o in objects) == 278
    assert sum(not o["drawn"] for o in objects) == 56
    assert dict(collections.Counter(o["kind"] for o in objects)) == KIND_COUNTS

    rows = list(ET.parse(REAL_TRACK).getroot().find("blueprints"))
    assert len(rows) == len(objects)
    for row, element in zip(objects, rows):
        assert row["instance_id"] == int(element.findtext("instanceID"))
        assert_pose(row, element)
        scale = element.find("scale")
        assert ("scale" in row) == (scale is not None)
        if scale is not None:
            assert all(abs(a - b) <= 0.001 * abs(b) for a, b in zip(row["scale"], numbers(scale)))
        points = element.find("attachPoints")
        assert ("points" in row) == (points is not None)
        if points is not None:
            assert len(row["points"]) == len(points)
            for stored, point in zip(row["points"], points):
                assert_pose(stored, point)

    items = {e.findtext("itemID") for e in rows}
    tool = Path(imp.__file__).read_text(encoding="utf-8")
    assert not [i for i in items if i in text or i in tool], "a whole item id in the layout or the tool"

    root = ET.parse(REAL_RACE).getroot()
    by_id = {p.findtext("uniqueId"): p for p in root.find("checkPointPassages")}
    linked = {n.text for p in by_id.values() for n in p.find("nextPassageIDs")}
    at = next(u for u in by_id if u not in linked)
    walked = []
    while at:
        walked.append(by_id[at])
        nexts = by_id[at].find("nextPassageIDs")
        at = nexts[0].text if len(nexts) else None
    stored = layout["race"]["passages"]
    assert len(walked) == len(stored) == 101
    assert len({p["checkpoint_instance_id"] for p in stored}) == 56
    assert [p["checkpoint_instance_id"] for p in stored] == [int(p.findtext("checkPointID")) for p in walked]
    assert [p["type"] for p in stored] == [imp.PASSAGE_TYPES[p.findtext("passageType")] for p in walked]
    assert [p["direction"] for p in stored] == [imp.DIRECTIONS[p.findtext("directionality")] for p in walked]
    assert stored[0]["type"] == "start" and stored[-1]["type"] == "finish"
    assert layout["race"]["laps"] == int(root.findtext("requiredLaps"))
    assert layout["race"]["spawn_point_instance_id"] == int(root.findtext("spawnPointID"))
