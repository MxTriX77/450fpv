"""camera_place.py: the feed's projection and the blade's share of a turn (w14-camera-place 1.1)."""
import math

import camera_place as cp


def test_distort_inverts_undistort():
    for u, v in [(0.0, 0.0), (0.5, -0.3), (-1.0, 0.5625), (0.9, 0.1)]:
        uu, vu = cp.undistort(u, v)
        ud, vd = cp.distort(uu, vu)
        assert math.isclose(ud, u, abs_tol=1e-9) and math.isclose(vd, v, abs_tol=1e-9)


def test_edge_angle_matches_the_feed():
    # the picture's side edge at mid-height: tan = (1 + k1) / f, about 53 deg (FeedCapture's hfov)
    uv = cp.project((math.tan(math.radians(52.9)), 0.0, 1.0), (0.0, 0.0, 0.0))
    assert math.isclose(uv[0], 1.0, abs_tol=0.005)


def test_old_place_reproduces_w13():
    share, band = cp.blade_share((0.0, 0.015, 0.115), steps=360)
    assert 0.33 <= share <= 0.36  # w13-props-in-frame design 1: 34 %
    assert band[1] < 0.5          # above the centre line: the disc is above the lens


def test_recommended_place_shows_no_blade():
    assert cp.blade_share((0.0, -0.020, 0.205), steps=360) == (0.0, None)


def test_low_lens_grazes_at_the_top_corner():
    share, band = cp.blade_share((0.0, -0.020, 0.200), steps=360)
    assert 0 < share < 0.03 and band[1] < 0.05


def test_rod_slope():
    assert math.isclose(cp.rod_slope((0, -0.020, 0.205), 0.06, -0.0314), 0.19, abs_tol=1e-3)
