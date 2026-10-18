"""lib.shell (p1-v13-shrub, review-physics PR1): the distance transform against brute force, no faces no pieces, and
two leaf masses with a twig, one of them on the ground, fitted with pieces that pass the audit's shell rule both
ways. The cap by steps (p1-v01-locust review-physics PR1): a mass that 24 pieces cannot hold escalates to the first
cap that passes, and no fit returns more than 64. The V13 variants' shells, reproduced at 6 / 15 / 24 pieces, are
tested through the generator in test_shrub.py."""
import random

from lib import audit, meta, shell, skeleton
from lib.geometry import Mesh

SPRIG_ALLOWANCE = audit.card_allowance(0.2)


def test_the_distance_transform_matches_brute_force():
    rng = random.Random(3)
    n = (7, 5, 6)
    inside = [[[rng.random() < 0.8 for _ in range(n[2])] for _ in range(n[1])] for _ in range(n[0])]
    inside[0][0][0] = False
    outside = [(i, j, k) for i in range(n[0]) for j in range(n[1]) for k in range(n[2]) if not inside[i][j][k]]
    squared = shell._transform(inside, n)
    for i in range(n[0]):
        for j in range(n[1]):
            for k in range(n[2]):
                assert squared[i][j][k] == min((i - a) ** 2 + (j - b) ** 2 + (k - c) ** 2 for a, b, c in outside)


def leaf_slab(mesh, x0, x1, y0, y1, half_z):
    """A leaf mass: square cards across X every 0.1 m from x0 to x1, each spanning y0..y1 and -half_z..half_z."""
    count = round((x1 - x0) / 0.1)
    for n in range(count + 1):
        x = x0 + n * (x1 - x0) / count
        corners = [(x, y0, -half_z), (x, y0, half_z), (x, y1, half_z), (x, y1, -half_z)]
        mesh.face([mesh.vertex(c) for c in corners], (1.0, 0.0, 0.0), "leaf")


def test_no_faces_of_the_slots_no_pieces():
    mesh = Mesh()
    leaf_slab(mesh, 0.0, 0.4, 1.0, 1.4, 0.2)
    assert shell.fit(mesh, ("twig",), ("bloom",), SPRIG_ALLOWANCE) == ([], 24, True)


def test_leaf_masses_round_a_twig_are_fitted_with_pieces_that_pass_the_shell_rule():
    mesh = Mesh()
    leaf_slab(mesh, -0.6, 0.6, 1.0, 1.6, 0.3)
    leaf_slab(mesh, -0.3, 0.3, 0.0, 0.4, 0.3)  # on the ground: what stands under it is not measured
    skeleton._tube(mesh, [((-0.5, 1.3, 0.0), 0.003), ((0.5, 1.3, 0.1), 0.002)], 3, "twig", cap=False)
    pieces, cap, passes = shell.fit(mesh, ("twig",), ("leaf",), SPRIG_ALLOWANCE)
    assert (cap, passes) == (24, True)
    assert 2 <= len(pieces) <= 24 and all(r >= shell.MIN_RADIUS_M for _, _, r in pieces)
    assert min(min(a[1], b[1]) - r for a, b, r in pieces) < 0  # the piece on the ground runs under it
    data = {"materials": [{"slot": "twig", "visual": "bark_shrub", "physics": "twig"},
                          {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}],
            "collision": [meta.capsule(a, b, r, "twig") for a, b, r in pieces], "gaps": []}
    problems, lines = audit.run(data, mesh, card_visuals={"leaf_shrub"}, card_reach_m=0.2)
    assert problems == []
    assert lines[0].startswith(f"audit twig shell: {len(pieces)} pieces (")


def scattered_twigs(count, spacing=0.8):
    """`count` short twigs in a row `spacing` apart: no piece within the mass reaches two of them, so each takes
    one of its own."""
    mesh = Mesh()
    for n in range(count):
        x = n * spacing
        skeleton._tube(mesh, [((x, 1.0, 0.0), 0.003), ((x + 0.2, 1.05, 0.0), 0.002)], 3, "twig", cap=False)
    return mesh


def test_a_mass_that_24_pieces_do_not_hold_escalates_to_the_first_cap_that_passes():
    mesh = scattered_twigs(30)
    pieces, cap, passes = shell.fit(mesh, ("twig",), (), SPRIG_ALLOWANCE)
    assert (cap, passes) == (32, True) and 24 < len(pieces) <= 32
    data = {"materials": [{"slot": "twig", "visual": "bark_shrub", "physics": "twig"}],
            "collision": [meta.capsule(a, b, r, "twig") for a, b, r in pieces], "gaps": []}
    assert audit.run(data, mesh)[0] == []


def test_no_fit_returns_more_than_64_pieces():
    pieces, cap, passes = shell.fit(scattered_twigs(66), ("twig",), (), SPRIG_ALLOWANCE)
    assert (cap, passes) == (64, False) and len(pieces) <= 64 == shell.MAX_PIECES
