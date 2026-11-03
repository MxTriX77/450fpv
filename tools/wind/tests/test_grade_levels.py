"""grade_levels.py: the level columns of wind.md sections 6.3-6.4 and the verdicts (p7c-wind 1.5)."""
import math

import grade_levels as gl


def test_the_columns_scale_from_severe():
    s, w, c = gl.bands("severe"), gl.bands("windy"), gl.bands("calm")
    assert s["T1"] == (0.35, 0.70) and s["T0"] == (6.0, 12.0)
    assert w["T1"] == (0.175, 0.35) and w["T0"] == (3.1, 6.1)        # spreads x c = 0.5; T0 4.6 +- 1.5
    assert w["T9a"] == s["T9a"] and w["T12 cv roll"] == s["T12 cv roll"]  # shapes unchanged
    assert c["T0"] == (None, 2.0) and c["T1"] == (None, 0.175)        # ceilings at Windy's lower bounds
    assert c["T6d pitch"] == (None, 0.044) and "T9a" not in c and "T7 rate" not in c


def test_t6d_is_p_real_motion_with_its_pick():
    # D-060: P's real motion over 4-15 Hz (0.08 / 0.042 deg at the upper end, hover-micro-motion.md section 2.4) with
    # the recorder's pick (0.018 / 0.012 deg, section 2.3) in quadrature; P's raw floor (0.125 / 0.054 deg) and the
    # pose noise (0.15 deg white, about 0.13 deg over 4-15 Hz) are above it.
    s = gl.bands("severe")
    assert s["T6d roll"][1] == round(math.hypot(0.08, 0.018), 3)
    assert s["T6d pitch"][1] == round(math.hypot(0.042, 0.012), 3)
    assert not gl.inside(0.125, s["T6d roll"]) and not gl.inside(0.054, s["T6d pitch"])
    assert gl.bands("windy")["T6d roll"] == s["T6d roll"] == gl.bands("calm")["T6d roll"]


def test_every_condition_has_a_target_and_a_known_wrong_model():
    conds = {k for _, cs, _ in gl.TARGETS for k in cs}
    assert set(gl.SEVERE) <= conds
    assert all(fam for _, _, fam in gl.TARGETS)
    fams = {name: fam for name, _, fam in gl.TARGETS}
    assert "noiseless" in fams["T2"] and "noiseless" in fams["T6c"]   # D-060: the stick noise's known-wrong
    assert all("nowake" in fams[t] for t in ("T2", "T6b", "T6c"))     # p7b-airframe 1.3d: the rotors' wakes' known-wrong


def test_the_verdicts_read_bands_and_ratios():
    v = {k: (lo if lo is not None else hi) for k, (lo, hi) in gl.SEVERE.items()}
    v.update({"T0 into wind": True, "T0b windward runs": 4, "T0b runs": 5, "T3": 1.0, "T1": 0.5, "T3 ratio": 2.0,
              "T4": 0.4, "T2": 0.3, "T4 ratio": 1.33, "T11a belt": 1.5, "T11a field": 0.6, "T11a ratio": 2.5,
              "T10b belt": 0.6, "T10b field": 0.4})
    res = gl.verdicts(v, "severe")
    assert all(ok for _, _, ok in res.values()), [k for k, r in res.items() if not r[2]]
    v["T0 into wind"] = False                                        # a lean away from the wind fails T0
    v["T3 ratio"] = 1.2
    v["T0b windward runs"] = 3
    res = gl.verdicts(v, "severe")
    assert not res["T0"][2] and not res["T3 ratio"][2] and not res["T0b windward"][2]
    assert not gl.inside(math.nan, (0, 1))


def test_t0b_windward_is_a_share_of_the_runs():
    # QA's S6: K section 8.6's "min > 0 in >= 4/5 runs" is 80 % of the runs at any count; the old runs - 1 asked 9 of 10.
    v = {k: (lo if lo is not None else hi) for k, (lo, hi) in gl.SEVERE.items()}
    v.update({"T0 into wind": True, "T3": 1.0, "T1": 0.5, "T3 ratio": 2.0, "T4": 0.4, "T2": 0.3, "T4 ratio": 1.33,
              "T11a belt": 1.5, "T11a field": 0.6, "T11a ratio": 2.5, "T10b belt": 0.6, "T10b field": 0.4})
    for windward, runs, ok in ((4, 5, True), (3, 5, False), (8, 10, True), (7, 10, False), (16, 20, True), (15, 20, False)):
        v.update({"T0b windward runs": windward, "T0b runs": runs})
        assert gl.verdicts(v, "severe")["T0b windward"][2] is ok, (windward, runs)
    v.update({"T0b windward runs": 8, "T0b runs": 10})
    assert not (8 >= 10 - 1)                                       # the old rule failed 8 of 10, the known-wrong
    assert gl.verdicts(v, "severe")["T0b windward"][1] == ">= 8 of 10"
