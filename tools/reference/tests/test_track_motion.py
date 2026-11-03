"""track_motion.py on synthetic clips with a known camera turn: it recovers roll, pitch and yaw with wind.md's signs,
masks a camera-fixed overlay, keeps the clip's name out of every output, and its fit rejects outliers.
No test opens the real reference/ folder."""
import csv
import math
import random
import shutil
import subprocess

import pytest
from PIL import Image, ImageDraw, ImageFilter

import track_motion as tm
from conftest import SECRET, run, snapshot

W, H, F_HW, FPS, N = 640, 360, 1.0, 30, 48
F_PX = F_HW * W / 2


def rot(axis, a):
    c, s = math.cos(a), math.sin(a)
    if axis == "x":
        return [[1, 0, 0], [0, c, -s], [0, s, c]]
    if axis == "y":
        return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def turns():
    """Per-frame camera turns (roll, pitch, yaw) in degrees, in the camera's own axes: roll 0.4 deg at 5 Hz, pitch
    0.3 deg at 2 Hz on a 2 deg/s nose-up drift, yaw a steady 6 deg/s to the right."""
    out = []
    for k in range(1, N):
        t0, t1 = (k - 1) / FPS, k / FPS
        roll = 0.4 * (math.sin(2 * math.pi * 5 * t1) - math.sin(2 * math.pi * 5 * t0))
        pitch = 0.3 * (math.sin(2 * math.pi * 2 * t1) - math.sin(2 * math.pi * 2 * t0)) + 2.0 / FPS
        out.append((roll, pitch, 6.0 / FPS))
    return out


def texture():
    rng = random.Random(7)
    small = Image.new("L", (220, 130))
    small.putdata([rng.randrange(256) for _ in range(220 * 130)])
    big = small.resize((2200, 1300), Image.BICUBIC).filter(ImageFilter.GaussianBlur(2))
    fine = Image.new("L", (1100, 650))
    fine.putdata([rng.randrange(256) for _ in range(1100 * 650)])
    return Image.blend(big, fine.resize((2200, 1300), Image.BICUBIC), 0.35)


def render(path):
    """Frames of a pinhole camera (f = 1 half-width) turning by turns() in front of a scene at infinity, with a
    camera-fixed white block (an OSD), encoded losslessly."""
    tex = texture()
    tw, th = tex.size
    c = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    frames = path.parent / "png"
    frames.mkdir()
    for k in range(N):
        if k:
            r, p, y = (math.radians(v) for v in turns()[k - 1])
            c = mul(c, mul(rot("x", p), mul(rot("y", y), rot("z", r))))
        # output pixel -> ray (camera) -> world = c @ ray -> texture pixel (texture = the view at the start)
        kin = [[1 / F_PX, 0, -(W - 1) / 2 / F_PX], [0, 1 / F_PX, -(H - 1) / 2 / F_PX], [0, 0, 1]]
        ktex = [[F_PX, 0, (tw - 1) / 2], [0, F_PX, (th - 1) / 2], [0, 0, 1]]
        h = mul(ktex, mul(c, kin))
        coeffs = [v / h[2][2] for row in h for v in row][:8]
        img = tex.transform((W, H), Image.PERSPECTIVE, coeffs, Image.BICUBIC)
        ImageDraw.Draw(img).rectangle((20, 300, 200, 340), fill=255)
        ImageDraw.Draw(img).text((30, 310), "AIR 00:00", fill=0)
        img.save(frames / f"f{k:03d}.png")
    subprocess.run([shutil.which("ffmpeg"), "-hide_banner", "-loglevel", "error", "-framerate", str(FPS),
                    "-i", str(frames / "f%03d.png"), "-metadata", f"title={SECRET}", "-c:v", "libx264", "-qp", "0",
                    "-pix_fmt", "yuv420p", str(path)], check=True)
    shutil.rmtree(frames)


@pytest.fixture(scope="module")
def turning_root(tmp_path_factory):
    root = tmp_path_factory.mktemp("root")
    (root / "reference" / "_frames").mkdir(parents=True)
    render(root / "reference" / f"{SECRET}.mp4")
    return root


@pytest.fixture(scope="module")
def tracked(turning_root):
    out = turning_root / "reference" / "_frames" / "Z" / "motion.csv"
    code = run(tm, turning_root / "reference" / f"{SECRET}.mp4", "--letter", "Z", "--k1", "0", "--focal", F_HW,
               "--root", turning_root)
    assert code == 0
    return list(csv.DictReader(out.open(encoding="utf-8")))


def test_the_turn_is_recovered_with_wind_md_signs(tracked):
    truth = turns()
    err = {"roll": [], "pitch": [], "yaw": []}
    for row, (r, p, y) in zip(tracked[1:], truth):
        assert float(row["conf_roll"]) >= 0.5
        err["roll"].append(float(row["d_roll_deg"]) - r)
        err["pitch"].append(float(row["d_pitch_deg"]) - p)
        err["yaw"].append(float(row["d_yaw_deg"]) - y)
    for axis, e in err.items():
        rms = math.sqrt(sum(v * v for v in e) / len(e))
        assert rms < 0.008, (axis, rms)         # 1 px is 0.18 deg here: the fit reads 0.01-0.02 px (measured)
    assert abs(float(tracked[-1]["yaw_rate_dps"]) - 6.0) < 0.6


def test_the_split_halves_agree_on_a_rigid_turn(tracked):
    for row in tracked[1:]:
        for axis in ("roll", "pitch", "yaw"):
            assert abs(float(row[f"d_{axis}_a"]) - float(row[f"d_{axis}_b"])) < 0.05
            assert abs(float(row[f"d_{axis}_top"]) - float(row[f"d_{axis}_bot"])) < 0.05


def test_no_output_names_the_clip(turning_root, tracked, capsys):
    for f in (turning_root / "reference" / "_frames").rglob("*"):
        if f.is_file():
            assert SECRET not in f.name and SECRET not in f.read_text(encoding="utf-8", errors="replace")


def test_output_outside_frames_is_refused(turning_root, capsys):
    before = snapshot(turning_root)
    code = run(tm, turning_root / "reference" / f"{SECRET}.mp4", "--letter", "Z", "--out", turning_root / "m.csv",
               "--root", turning_root)
    assert code == 2 and snapshot(turning_root) == before
    assert SECRET not in capsys.readouterr().err


def test_the_fit_rejects_a_third_of_wild_matches():
    lens = tm.Lens(1920, 1080, 0.33, 1 / math.sqrt(3 * 0.33))
    w = (math.radians(0.3), math.radians(-0.2), math.radians(0.5))
    rng = random.Random(3)
    pairs = []
    for i in range(90):
        a = lens.ray(rng.uniform(100, 1820), rng.uniform(80, 1000))
        b = [a[k] + sum(r[j] * v for j, v in enumerate((*w, 0.0))) for k, r in enumerate(tm.rows(a))]
        if i % 3 == 0:
            b = lens.ray(rng.uniform(100, 1820), rng.uniform(80, 1000))
        pairs.append((a, tuple(b)))
    x, weights = tm.fit(pairs, lens.f_px, seed=1)
    roll, pitch, yaw = tm.angles(x)
    assert abs(roll - 0.5) < 1e-3 and abs(pitch - 0.3) < 1e-3 and abs(yaw + 0.2) < 1e-3
    assert sum(1 for v in weights if v == 0) >= 28


def test_trf_parsing():
    text = "VID.STAB 1\nFrame 1 (List 0 [])\nFrame 2 (List 2 [(LM -2 1 1208 216 112 0.63 0.84),(LM 0 -1 588 216 112 0.24 0.93)])\n"
    lms = tm.parse_trf(text)
    assert lms[1] == [] and lms[2] == [(-2, 1, 1208, 216, 112, 0.63), (0, -1, 588, 216, 112, 0.24)]
