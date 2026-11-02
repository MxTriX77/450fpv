"""rain_stats.py: the per-frame measurement on synthetic frames with planted traits, the statistics on synthetic
per-frame series, and the decoder on a generated clip. No test opens the real reference/ folder."""
import math
import random
import shutil
import subprocess
from array import array

import pytest

import rain_stats as rs

W, H = 1920, 1080


@pytest.fixture(scope="module")
def rays():
    return rs.pixel_rays(H // 2, W // 2), rs.pixel_rays(H, W)


def scene(rays_full, roll, pitch, grain, sparkles=(), seed=1):
    """A native frame: grey sky (180) above the horizon of a camera at roll/pitch, dark ground (60) below, 8-bit
    grey grain of std `grain`, and pure red single-pixel sparkles at the given pixel indices."""
    rng = random.Random(seed)
    v = array("d", [max(0, min(255, round((180.0 if d < 0 else 60.0) + rng.gauss(0, grain))))
                    for d in rs.depression(roll, pitch, rays_full)])
    r, g, b = array("d", v), array("d", v), array("d", v)
    for i in sparkles:
        r[i], g[i], b[i] = 255.0, 0.0, 0.0
    return rs.Img(W, H, r, g, b)


# ---------------------------------------------------------------- image helpers

def test_box_mean_is_the_edge_padded_box_mean():
    rng = random.Random(2)
    w, h, r = 13, 9, 2
    a = array("d", [rng.uniform(0, 255) for _ in range(w * h)])
    got = rs.box_mean(a, w, h, r)
    for y in range(h):
        for x in range(w):
            ref = sum(a[min(h - 1, max(0, y + j)) * w + min(w - 1, max(0, x + i))]
                      for j in range(-r, r + 1) for i in range(-r, r + 1)) / (2 * r + 1) ** 2
            assert got[y * w + x] == pytest.approx(ref)


def test_half_is_the_2x2_area_average():
    img = rs.Img(4, 2, array("d", [0, 4, 8, 8, 4, 0, 0, 4]), array("d", [1] * 8), array("d", [2] * 8))
    h = rs.half(img)
    assert (h.w, h.h) == (2, 1) and list(h.ch[0]) == [2.0, 5.0] and list(h.ch[1]) == [1.0, 1.0]


def test_depression_is_positive_below_the_horizon():
    rays = rs.pixel_rays(3, 3)                           # 3 x 3 pixels: the middle one is the optical axis
    dep = rs.depression(0.0, -10.0, rays)                # nose 10 deg down: the axis looks 10 deg below
    assert dep[4] == pytest.approx(10.0, abs=1e-9)
    assert dep[1] < dep[4] < dep[7]                      # the top row sees higher, the bottom row lower


# ---------------------------------------------------------------- the horizon fit (track_attitude.py)

@pytest.mark.parametrize("roll,pitch", [(0.0, -10.0), (15.0, -22.7), (-25.0, 5.0)])
def test_horizon_recovers_roll_and_pitch(rays, roll, pitch):
    img = rs.half(scene(rays[1], roll, pitch, 2.0))
    hz = rs.horizon(img, bytearray(img.w * img.h), rs.K1, rs.FOCAL, random.Random(0))
    assert hz["roll"] == pytest.approx(roll, abs=0.15)
    assert hz["pitch"] == pytest.approx(pitch, abs=0.15)
    assert min(hz["conf_roll"], hz["conf_pitch"]) >= rs.CONF


def test_no_horizon_in_a_flat_frame():
    img = rs.Img(960, 540, *(array("d", [128.0]) * (960 * 540) for _ in range(3)))
    hz = rs.horizon(img, bytearray(960 * 540), rs.K1, rs.FOCAL, random.Random(0))
    assert hz["flag"] == "no_line" and hz["conf_roll"] == 0.0


def test_static_mask_finds_a_camera_fixed_block():
    """An OSD-like block fixed to the camera over a scene whose level changes every frame."""
    w, h, rng, frames = 120, 80, random.Random(3), []
    for _ in range(30):
        level = rng.uniform(40, 200)
        v = array("d", [level]) * (w * h)
        for y in range(20, 30):
            for x in range(30, 70):
                v[y * w + x] = 250.0
        frames.append(rs.Img(w, h, v, array("d", v), array("d", v)))
    m = rs.static_mask(frames, w, h)
    assert all(m[y * w + x] for y in range(20, 30) for x in range(30, 70))
    assert not any(m[y * w + x] for y in range(h) for x in range(w) if y < 8 or y > 42 or x < 18 or x > 82)


# ---------------------------------------------------------------- the per-frame measurement

def test_measure_recovers_the_planted_frame(rays):
    sparkles = [(150 + 20 * (k // 10)) * W + 400 + 60 * (k % 10) for k in range(50)]   # 50 in the sky, far apart
    full = scene(rays[1], 0.0, -10.0, 3.0, sparkles)
    m, bands = rs.measure(full, bytearray(W // 2 * H // 2), rays[0], rays[1], True, 7)
    assert m["pic"] == 1.0
    assert m["roll"] == pytest.approx(0.0, abs=0.1) and m["pitch"] == pytest.approx(-10.0, abs=0.1)
    assert m["sparkle"] == pytest.approx(1000.0 * 50 / (W * H))          # every sparkle, nothing else
    # grain: the MAD sigma of luma minus its 5x5 mean, sqrt(1 - 1/25) of the grain's std (plus 8-bit rounding)
    assert m["grain"] == pytest.approx(3.0 * math.sqrt(24 / 25), abs=0.12)
    assert m["blobs"] == 0                                               # a clean lens shows no drops
    assert m["grain_sky"] == pytest.approx(180.0, abs=0.5)
    sky = bands[list(rs.DEP).index("sky")]
    assert sky[0] > 0 and sky[1] / sky[0] == pytest.approx(180.0, abs=0.5)
    assert math.isnan(m["rod"])                                          # no rod in this frame


def test_a_blue_no_signal_frame_is_not_a_picture(rays):
    blue = rs.Img(W, H, array("d", [10.0]) * (W * H), array("d", [10.0]) * (W * H), array("d", [230.0]) * (W * H))
    m, bands = rs.measure(blue, bytearray(W // 2 * H // 2), rays[0], rays[1], False, 1)
    assert m["pic"] == 0.0 and bands is None


def sky(w=480, h=270, seed=4):
    rng = random.Random(seed)
    return array("d", [150.0 + rng.gauss(0, 2) for _ in range(w * h)])


def test_drops_on_the_lens_are_counted_and_a_clean_lens_reads_none():
    """R1 (pilot override: drops sit on the lens): the blob search must find planted drops and nothing on a clean sky;
    a long streak is not a drop (bounding box over BLOB_ASPECT)."""
    w, h = 480, 270
    all_sky = bytearray([1]) * (w * h)
    y = sky()
    assert rs.sky_blobs(y, all_sky, w, h) == 0
    for cx, cy in ((80, 80), (240, 150), (400, 200)):
        for yy in range(cy - 12, cy + 13):
            for xx in range(cx - 12, cx + 13):
                if (xx - cx) ** 2 + (yy - cy) ** 2 <= 144:
                    y[yy * w + xx] += 25.0
    assert rs.sky_blobs(y, all_sky, w, h) == 3
    for yy in range(30, 36):                                             # a 6 x 120 px streak
        for xx in range(200, 320):
            y[yy * w + xx] += 25.0
    assert rs.sky_blobs(y, all_sky, w, h) == 3
    assert math.isnan(rs.sky_blobs(y, bytearray(w * h), w, h))           # too little sky: no answer, not "none"


def rod_frame(sigma, tilt=0.03):
    """Native luma: dark background (40) over a bright rod (200) whose upper edge runs at row 700 (slightly tilted)
    across the rod box, blurred vertically by a Gaussian of std sigma px."""
    y = array("d", [40.0]) * (W * H)
    for x in range(1540, 1920):
        e = 700.0 + tilt * (x - 1730)
        for row in range(640, 900):
            y[row * W + x] = 40.0 + 160.0 * 0.5 * (1 + math.erf((row + 0.5 - e) / (sigma * math.sqrt(2))))
        for row in range(900, H):
            y[row * W + x] = 200.0
    return y


@pytest.mark.parametrize("sigma", [1.0, 2.0])
def test_rod_edge_width_is_the_10_90_rise(sigma):
    """A Gaussian edge's 10-90 % rise is 2.563 sigma (wind.md 7.3, R6); a wet-lens blur reads wider."""
    width, under = rs.rod_edge(rod_frame(sigma), W, H)
    assert width == pytest.approx(2.563 * sigma, abs=0.2)
    assert under == pytest.approx(0.0, abs=0.02)


def test_no_rod_no_edge():
    assert rs.rod_edge(array("d", [40.0]) * (W * H), W, H) is None


# ---------------------------------------------------------------- the statistics on synthetic per-frame series

def clip(letter, n, fps=29.917, seed=0):
    """A synthetic per-frame record as scan() returns it."""
    rng = random.Random(seed)
    last = rs.FIRST_LOSS[letter] - rs.LOSS_GUARD - 1 if letter in rs.FIRST_LOSS else n
    frames = list(range(1, last + 1))
    pic = [0.0 if letter == rs.RAIN and any(a <= f <= b for a, b in rs.OUTAGES) else 1.0 for f in frames]
    s = {k: [rng.uniform(1, 2) for _ in frames] for k in rs.FIELDS}
    lum = dict(sky=150.0, skyline=140.0, far=90.0, mid=80.0, near=70.0)
    px = dict(sky=5000.0, skyline=3000.0, far=4000.0, mid=8000.0, near=20000.0)
    s.update(pic=pic, conf=[1.0] * len(frames), luma=[100 + rng.gauss(0, 1) for _ in frames],
             black=[10.0] * len(frames), luma_p99=[200.0] * len(frames), edge=[3.5] * len(frames),
             blobs=[0.0] * len(frames), block_median=[100.0] * len(frames), band_left=[100.0] * len(frames),
             band_right=[100.0] * len(frames))
    if letter == rs.RAIN:                                # the left band brightens by 10 levels every second frame
        s["band_left"] = [110.0 if f % 2 == 0 and f < 400 else 100.0 for f in frames]
    s.update(frames=frames, bands=[[[px[k], px[k] * lum[k], px[k] * 5.0, px[k] * 0.4] for k in rs.DEP] for _ in frames],
             mask_share=0.142, fps=fps, n=n, version=rs.VERSION, seconds=0.0, rod_mean=[3.2, 0.05])
    return s


@pytest.fixture(scope="module")
def analysed():
    sc = {L: clip(L, 1417 if L == rs.RAIN else 700, seed=i) for i, L in enumerate(rs.CLIPS)}
    return rs.analyse(sc)


def test_stills_bound_the_drop_share(analysed):
    """wind.md 7.3, R1: 48 evenly spaced stills with no drop bound the share at < 6.1 % (95 %)."""
    st = analysed["stills"]
    assert st["count"] == 48
    assert st["share_bound_95"] == pytest.approx(0.061, abs=0.0006)


def test_frame_sets_leave_out_the_outages_and_the_last_second(analysed):
    c = analysed["clips"]
    outage_frames = sum(b - a + 1 + 2 * rs.MARGIN for a, b in rs.OUTAGES)
    assert c["P"]["window"] == 1417 - outage_frames
    assert c["A"]["scanned"] == 424 - 30 - 1 and c["L"]["scanned"] == 700


def test_edge_band_counts_the_every_second_frame_steps(analysed):
    e = analysed["edge_bands"]["P"]["left"]
    assert e["frames"] == len([f for f in range(2, 400, 2)])            # every even frame below 400
    assert e["gaps_2"] == e["gaps"] == e["frames"] - 1
    assert analysed["edge_bands"]["P"]["right"]["frames"] == 0


def test_profile_and_koschmieder(analysed):
    b = analysed["profile"]["P"]["bands"]
    assert b["far"]["luma"] == pytest.approx(90.0) and b["near"]["texture_rel"] == pytest.approx(0.4 / 70.0)
    k = analysed["derived"]["koschmieder"]
    assert k["t_min"] == pytest.approx((140 - 90) / (140 - 0.5 * 70))  # far terrain at least half the near's brightness
    assert analysed["profile"]["P"]["far_near_q"][1] == pytest.approx(90.0 / 70.0)


def test_derived_drop_and_prop_wash_are_the_notes_numbers():
    """wind.md 7.3: a 2 mm drop 0.5 m away spans about 0.23 deg, 4 px at the centre; at 4.8 kg each 10" disc of
    0.051 m^2 carries 11.8 N and induces 9.7 m/s in hover."""
    d = rs.drop()
    assert d["deg"] == pytest.approx(0.23, abs=0.005) and round(d["px"]) == 4
    pw = rs.prop_wash()
    assert pw["thrust_n"] == pytest.approx(11.8, abs=0.05) and pw["disc_m2"] == pytest.approx(0.051, abs=0.0005)
    assert pw["induced_mps"] == pytest.approx(9.7, abs=0.05)


def test_outage_check_runs_on_random_windows(analysed):
    ou = analysed["outages"]
    assert len(ou["each"]) == 4 and 0.0 <= ou["random_share"] <= 1.0


# ---------------------------------------------------------------- decoding and the OPSEC guard

def test_decoder_yields_every_frame_in_order(tmp_path):
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg is None:
        pytest.fail("ffmpeg not found on PATH; the reference tools need it (docs/environment.md)")
    path = tmp_path / "clip.mp4"
    subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=64x48:rate=25:duration=1",
                    "-pix_fmt", "yuv420p", "-c:v", "libx264", str(path)], check=True)
    dec = rs.Decoder(path)
    assert dec.size == (64, 48) and dec.n == 25 and dec.fps == pytest.approx(25.0)
    got = [(k, img.w, img.h, len(img.ch[0])) for k, img in dec.frames()]
    assert got == [(k, 64, 48, 64 * 48) for k in range(1, 26)]


def test_a_clip_outside_reference_is_refused(tmp_path):
    ref = tmp_path / "reference"
    (ref / "_frames").mkdir(parents=True)
    (tmp_path / "outside.mp4").write_bytes(b"")
    (ref / "_frames" / "index.md").write_text("| Z | clip | `../outside.mp4` |\n", encoding="utf-8")
    with pytest.raises(rs.ref_paths.RefError):
        rs.clip_path(ref, "Z")


def test_the_restarts_letter_map_wins_over_the_old_index(tmp_path):
    ref = tmp_path / "reference"
    (ref / "_frames").mkdir(parents=True)
    (ref / "new.mp4").write_bytes(b"")
    (ref / "_frames" / "index.md").write_text("| Z | clip | `old.mp4` |\n", encoding="utf-8")
    (ref / "_frames" / "letters.md").write_text("| Z | clip | `new.mp4` |\n", encoding="utf-8")
    assert rs.clip_path(ref, "Z") == (ref / "new.mp4").resolve()
