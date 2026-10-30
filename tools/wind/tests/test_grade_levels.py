"""grade_levels.py: the level columns of wind.md sections 6.3-6.4 and the verdicts (p7c-wind 1.5)."""
import math

import grade_levels as gl


def test_the_columns_scale_from_severe():
    s, w, c = gl.bands("severe"), gl.bands("windy"), gl.bands("calm")
    assert s["T1"] == (0.35, 0.70) and s["T0"] == (6.0, 12.0)
    assert w["T1"] == (0.175, 0.35) and w["T0"] == (3.1, 6.1)        # spreads x c = 0.5; T0 4.6 +- 1.5
    assert w["T9a"] == s["T9a"] and w["T12 cv roll"] == s["T12 cv roll"]  # shapes unchanged
    assert c["T0"] == (None, 2.0) and c["T1"] == (None, 0.175)        # ceilings at Windy's lower bounds
    assert c["T6d pitch"] == (None, 0.06) and "T9a" not in c and "T7 rate" not in c


def test_every_condition_has_a_target_and_a_known_wrong_model():
    conds = {k for _, cs, _ in gl.TARGETS for k in cs}
    assert set(gl.SEVERE) <= conds
    assert all(fam for _, _, fam in gl.TARGETS)


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
