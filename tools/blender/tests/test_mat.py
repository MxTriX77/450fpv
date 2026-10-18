"""lib.mat, the mat's shared fields (p1-s04-belt-straw task 1.7): the tussock relief is seeded, has its mean exactly 0
and its largest magnitude exactly relief_m over the generator's grid, is billowed (broad humps, narrow deep creases)
and longer along the lodging than across it; the green strips reach their share outside the bare outlines, lie
along the lodging, and their weight is 1 inside a strip and 0 beyond its feather; the constants are recorded."""
import math
import statistics

import pytest

from lib import mat, patches

PARAMS = {"seed": 1, "size_m": 12.0, "cell_m": 0.125, "relief_m": 0.04,
          "bare": {"share": 0.08, "size_m": {"min": 0.5, "max": 2.0}, "outline_vertices": 12},
          "green": {"share": 0.15, "width_m": {"min": 1.0, "max": 3.0}, "length_m": {"min": 3.0, "max": 12.0}},
          "lodging": {"azimuth_deg": 25.0, "bias": 0.3}}


def grid(p):
    n = round(p["size_m"] / p["cell_m"])
    return [(-p["size_m"] / 2 + i * p["size_m"] / n, -p["size_m"] / 2 + j * p["size_m"] / n)
            for i in range(n + 1) for j in range(n + 1)]


def test_relief_is_normalised_over_the_grid_and_seeded():
    relief = mat.relief(**mat.relief_inputs(PARAMS))
    values = [relief(x, z) for x, z in grid(PARAMS)]
    assert sum(values) / len(values) == pytest.approx(0.0, abs=1e-12)
    assert max(abs(v) for v in values) == pytest.approx(PARAMS["relief_m"])
    again, other = mat.relief(**mat.relief_inputs(PARAMS)), mat.relief(**mat.relief_inputs(dict(PARAMS, seed=2)))
    assert [again(x, z) for x, z in grid(PARAMS)] == values
    assert [other(x, z) for x, z in grid(PARAMS)] != values
    flat = mat.relief(**mat.relief_inputs(dict(PARAMS, relief_m=0.0)))
    assert all(flat(x, z) == 0.0 for x, z in grid(PARAMS)[:100])


def test_relief_is_lumpy_and_longer_along_the_lodging():
    """The relief uses its range (a lumpy mat, not a few spikes on a flat one), and its autocorrelation at the
    tussock's scale is higher along the lodging than across it (the humps stretched 1 + bias along it)."""
    relief = mat.relief(**mat.relief_inputs(PARAMS))
    values = [relief(x, z) for x, z in grid(PARAMS)]
    sd = statistics.pstdev(values)
    print(f"relief sd {1000 * sd:.1f} mm of +-{1000 * PARAMS['relief_m']:.0f} mm")
    assert sd > 0.25 * PARAMS["relief_m"]

    a = math.radians(PARAMS["lodging"]["azimuth_deg"])

    def correlation(dx, dz):
        pairs = [(relief(x, z), relief(x + dx, z + dz)) for x, z in grid(PARAMS)[::7]
                 if abs(x + dx) < 6 and abs(z + dz) < 6]
        ma, mb = statistics.fmean(p[0] for p in pairs), statistics.fmean(p[1] for p in pairs)
        cov = sum((p - ma) * (q - mb) for p, q in pairs)
        return cov / math.sqrt(sum((p - ma) ** 2 for p, _ in pairs) * sum((q - mb) ** 2 for _, q in pairs))

    step = mat.TUSSOCK_M * 0.6
    along = correlation(step * math.cos(a), step * math.sin(a))
    across = correlation(-step * math.sin(a), step * math.cos(a))
    print(f"correlation at {step:.2f} m: along the lodging {along:.2f}, across {across:.2f}")
    assert along > across


def test_green_strips_reach_their_share_outside_the_bare_patches_along_the_lodging():
    bare = patches.layout(**patches.inputs(PARAMS))["patches"]
    green = mat.green(**mat.green_inputs(PARAMS), bare_patches=bare)
    assert abs(green["share"] - PARAMS["green"]["share"]) <= mat.SHARE_TOLERANCE + 1e-9
    for strip in green["strips"]:
        assert abs(strip["angle_deg"] - PARAMS["lodging"]["azimuth_deg"]) <= mat.GREEN_SPREAD_DEG
        assert len(strip["parts"]) == 1 + mat.BLOB_SATELLITES
    again = mat.green(**mat.green_inputs(PARAMS), bare_patches=bare)
    assert again == green, "seeded"


def test_green_weight_is_one_inside_a_strip_and_nothing_beyond_its_feather():
    strip = {"centre": (0.0, 0.0), "angle_deg": 0.0, "parts": [(0.0, 0.0, 2.0, 0.5)]}
    assert mat.green_weight([strip], 0.0, 0.0) == 1.0
    assert mat.green_weight([strip], 1.5, 0.0) == 1.0
    assert 0.3 < mat.green_weight([strip], 0.0, 0.5) < 0.7, "half at the edge, as the map's blur"
    assert mat.green_weight([strip], 0.0, 0.5 + mat.GREEN_FEATHER_M / 2 + 0.01) == 0.0
    assert mat.green_weight([], 0.0, 0.0) == 0.0
    ring = mat.ellipse(strip["parts"][0], 30.0, points=8)
    assert len(ring) == 8 and ring[0] == pytest.approx((2.0 * math.cos(math.radians(30)), 2.0 * math.sin(math.radians(30))))


def test_constants_are_recorded_with_the_version():
    recorded = mat.constants()
    assert recorded["FIELDS"] == mat.FIELDS
    assert set(recorded) == {"FIELDS", *mat.CONSTANTS}
    assert recorded["BLOB_SCALE"] == list(mat.BLOB_SCALE)
