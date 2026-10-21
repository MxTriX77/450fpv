"""lib.shell (p1-v13-shrub, review-physics PR1): the distance transform against brute force, no faces no pieces, and
two leaf masses with a twig, one of them on the ground, fitted with pieces that pass the audit's shell rule both
ways. The orphan path (p1-v01-locust review-physics §A.1): a bare twig no lattice candidate reaches gets orphan
pieces that hold it within the tolerance, and a mass with no orphan fits from exactly the lattice's candidates. The
cap by steps (p1-v01-locust review-physics PR1): a mass that 24 pieces cannot hold escalates to the first
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


def fit_seeing_the_candidates(monkeypatch, mesh):
    """(pieces, the fit's candidates, the lattice's of step 2) of `mesh`'s twig shell over its leaf cards."""
    seen, real = [], shell._fit

    def record(cover, *args, **kwargs):
        seen.append(list(cover.candidates))
        return real(cover, *args, **kwargs)

    monkeypatch.setattr(shell, "_fit", record)
    pieces, cap, passes = shell.fit(mesh, ("twig",), ("leaf",), SPRIG_ALLOWANCE)
    assert passes
    index = audit.FaceIndex(mesh, {"twig", "leaf"})
    lattice = shell._Mass(index, shell._vertices(mesh, ("twig",)) + shell._vertices(mesh, ("leaf",))).candidates()
    return pieces, seen[0], lattice


def test_a_bare_twig_no_lattice_candidate_reaches_gets_its_orphan_pieces_and_passes_rule_b(monkeypatch):
    """p1-v01-locust review-physics §A.1 (QA 2.1's R4): a bare twig 0.45 m over a leaf mass, its axis between the
    lattice's cells, so no candidate of step 2 brings it within reach; the fit adds its orphan spheres and capsules
    after the lattice's, takes one, and every twig vertex ends within the 0.1 m tolerance."""
    mesh = Mesh()
    leaf_slab(mesh, -0.6, 0.6, 1.0, 1.6, 0.3)
    skeleton._tube(mesh, [((-0.2, 2.05, 0.05), 0.003), ((0.2, 2.07, 0.05), 0.002)], 3, "twig", cap=False)
    pieces, candidates, lattice = fit_seeing_the_candidates(monkeypatch, mesh)
    twig = shell._vertices(mesh, ("twig",))
    assert all(shell._outside(v, lattice) > audit.ENVELOPE_TOLERANCE_M for v in twig)  # no lattice candidate reaches it
    added = candidates[len(lattice):]
    assert candidates[:len(lattice)] == lattice and added
    assert all(r >= shell.MIN_RADIUS_M and (a == b or shell._dist(a, b) <= shell.ORPHAN_SPAN_M) for a, b, r in added)
    orphans = {(a, b) for a, b, _ in added}
    taken = [(a, b, r) for a, b, r in pieces if (a, b) in orphans]  # step 5 may shrink a piece, never move it
    assert taken and all(shell._outside(v, taken) <= audit.ENVELOPE_TOLERANCE_M for v in twig)
    assert max(shell._outside(v, pieces) for v in twig) <= audit.ENVELOPE_TOLERANCE_M
    data = {"materials": [{"slot": "twig", "visual": "bark_shrub", "physics": "twig"},
                          {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}],
            "collision": [meta.capsule(a, b, r, "twig") for a, b, r in pieces], "gaps": []}
    assert audit.run(data, mesh, card_visuals={"leaf_shrub"}, card_reach_m=0.2)[0] == []


def test_a_mass_with_no_orphan_fits_from_exactly_the_lattices_candidates(monkeypatch):
    """p1-v01-locust review-physics §A.1 (QA 2.1's R4): the same twig inside the leaf mass, which the lattice
    reaches: the fit's candidates are the lattice's, no more and in the same order."""
    mesh = Mesh()
    leaf_slab(mesh, -0.6, 0.6, 1.0, 1.6, 0.3)
    skeleton._tube(mesh, [((-0.2, 1.3, 0.05), 0.003), ((0.2, 1.32, 0.05), 0.002)], 3, "twig", cap=False)
    pieces, candidates, lattice = fit_seeing_the_candidates(monkeypatch, mesh)
    assert candidates == lattice
    assert max(shell._outside(v, pieces) for v in shell._vertices(mesh, ("twig",))) <= audit.ENVELOPE_TOLERANCE_M


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


def test_only_a_fit_that_fails_at_every_cap_takes_the_caps_again_at_more_rounds(monkeypatch):
    """p1-v01-locust task 1.5: step 5 can shrink the last round's pieces off a target that no round is left to cover
    (a twig fork of V13_tall's thicket), so when every cap fails the caps run again at RETRY_ROUNDS; a fit that passes
    at some cap never does, so every shell that passed before is the same."""
    calls, real = [], shell._fit

    def record(cover, index, cards, card_reach, max_pieces, standoffs, rounds=shell.ROUNDS):
        calls.append((max_pieces, rounds))
        return real(cover, index, cards, card_reach, max_pieces, standoffs, rounds)

    monkeypatch.setattr(shell, "_fit", record)
    assert shell.fit(scattered_twigs(30), ("twig",), (), SPRIG_ALLOWANCE)[1:] == (32, True)
    assert calls == [(24, shell.ROUNDS), (32, shell.ROUNDS)]
    calls.clear()
    assert shell.fit(scattered_twigs(66), ("twig",), (), SPRIG_ALLOWANCE)[1:] == (64, False)
    assert calls == [(cap, shell.ROUNDS) for cap in shell.CAPS] + [(cap, shell.RETRY_ROUNDS) for cap in shell.CAPS]
