"""lib.audit: K01 measures as the physics review did (3.288 mm box->mesh, 1.898 mm mesh->box), a mesh grown 6 cm
beyond its box fails naming the primitive, the deviation and the tolerance, a box grown beyond its mesh fails the
other way, the 2 cm rule applies within 0.3 m of a gap or a landable face, curved primitives tessellate to their
dimensions and measure against a mesh, and narrowest_m is held to the largest inscribed circle, concave included
(p1-pipeline-hardening, "Collision audit at build time"). Compliant shells (p1-v13-shrub, review-physics PR2): two
overlapping spheres hugging a leaf mass pass and print each piece's standoff and each slot's outside distance; a
piece 0.3 m too large fails (a); a twig vertex 0.15 m outside fails (b); a card corner 0.35 m outside fails (c) and
one 0.25 m outside passes; surface under the ground is ignored; a capsule off its tube by 7 cm still fails; and a
shell no slot carries or a slot no shell stands for is refused. The real V13_mid with its first, single sphere
failing (a) is in test_shrub.py."""
import copy
import math
import re

import pytest

from generators import debris_brick
from lib import audit, meta, skeleton
from lib.geometry import Mesh

MM = re.compile(r"(-?[\d.]+) mm at \(")


def k01(params, grow_x_m=0.0):
    """K01's metadata and LOD0, the +X half of the mesh pushed out by `grow_x_m`."""
    built = debris_brick.build(params)
    data = meta.assemble("K01", "K01_whole", "debris_brick", params, built, {"brick": "brick_red"})
    lod0 = built["lods"][0]
    lod0.vertices = [(x + grow_x_m if x > 0.1 else x, y, z) for x, y, z in lod0.vertices]
    return data, lod0


def measured_mm(line):
    return float(MM.search(line).group(1))


def test_k01_measures_as_the_physics_review_did(k01_params):
    problems, lines = audit.run(*k01(k01_params))
    assert problems == []
    assert lines[0].startswith("audit collision[0] box: box->mesh 3.288 mm at (-0.125, 0, 0.06), tolerance 50 mm")
    assert lines[1].startswith("audit mesh: mesh->collision ") and "nearest collision[0], tolerance 50 mm" in lines[1]
    assert measured_mm(lines[1]) == pytest.approx(1.898, abs=0.1)  # 2w/3 at a corner triangle's centroid
    assert len(lines) == 2


def test_mesh_grown_6cm_beyond_its_box_fails_naming_primitive_deviation_and_tolerance(k01_params):
    problems, lines = audit.run(*k01(k01_params, grow_x_m=0.06))
    assert len(problems) == 1 and problems[0].startswith("mesh: mesh->collision 60.000 mm at (0.185, ")
    assert problems[0].endswith(") nearest collision[0] exceeds the tolerance 50 mm")
    assert lines[0].startswith("audit collision[0] box: box->mesh 32.500 mm at (0.125, 0.0325, 0)")


def test_box_grown_beyond_its_mesh_is_an_invisible_wall(k01_params):
    data, lod0 = k01(k01_params)
    data["collision"][0]["size"][0] += 0.12
    problems, _ = audit.run(data, lod0)
    assert len(problems) == 1 and problems[0].startswith("collision[0] box: box->mesh 60.1")
    assert problems[0].endswith(" exceeds the tolerance 50 mm")
    # 60 mm plus the chamfer: the box corner's foot on the mesh face is a corner the chamfer took away.
    assert measured_mm(problems[0]) == pytest.approx(math.hypot(60.0, 2.847 * math.sqrt(2)), abs=0.1)
    assert problems[0].split(" at ")[1].startswith("(-0.185, ") or problems[0].split(" at ")[1].startswith("(0.185, ")


def test_rotated_box_follows_the_rotated_mesh(k01_params):
    q = [0.0, math.sin(math.pi / 4), 0.0, math.cos(math.pi / 4)]  # 90 degrees about Y
    data, lod0 = k01(k01_params)
    lod0.vertices = [audit._rotate(q, v) for v in lod0.vertices]
    lod0.faces = [(indices, audit._rotate(q, n), uvs, slot) for indices, n, uvs, slot in lod0.faces]
    data["collision"][0]["rotation"] = q
    problems, lines = audit.run(data, lod0)
    assert problems == []
    assert measured_mm(lines[0]) == pytest.approx(3.288, abs=0.1)
    assert measured_mm(lines[1]) == pytest.approx(1.898, abs=0.1)


def gap_at_x(x):
    return {"id": "hole", "polygon": [[x, 0.0, -0.1], [x, 0.0, 0.1], [x, 0.2, 0.1], [x, 0.2, -0.1]],
            "normal": [1.0, 0.0, 0.0], "narrowest_m": 0.2}


def test_3cm_passes_far_from_openings_and_fails_within_30cm_of_a_gap(k01_params):
    data, lod0 = k01(k01_params, grow_x_m=0.03)
    assert audit.run(data, lod0)[0] == []
    data["gaps"].append(gap_at_x(5.0))
    problems, lines = audit.run(data, lod0)
    assert problems == [] and all("near gaps/landable" not in line for line in lines)
    data["gaps"][0] = gap_at_x(0.3)
    problems, lines = audit.run(data, lod0)
    assert [p.split(" at ")[0] for p in problems] == ["collision[0] box: near gaps/landable 30.000 mm",
                                                       "mesh: near gaps/landable 30.000 mm"]
    assert problems[0].endswith("at (0.125, 0.0325, 0) exceeds the tolerance 20 mm")
    assert problems[1].endswith(") nearest collision[0] exceeds the tolerance 20 mm") and " at (0.155, " in problems[1]
    assert "box->mesh 30.000 mm at (0.125, 0.0325, 0), tolerance 50 mm; near gaps/landable 30.000 mm" in lines[0]
    assert lines[2] == "audit gaps[0] 'hole': narrowest_m 0.2000, largest inscribed circle 0.2000, tolerance 5 mm"


def test_3cm_fails_within_30cm_of_a_landable_face(k01_params):
    data, lod0 = k01(k01_params, grow_x_m=0.03)
    data["collision"][0]["landable"] = True
    problems, _ = audit.run(data, lod0)
    assert [p.split(" at ")[0] for p in problems] == ["collision[0] box: near gaps/landable 30.000 mm",
                                                       "mesh: near gaps/landable 30.000 mm"]


def cylinder_mesh(radius, height, segments=64):
    mesh = Mesh()
    rings = [[mesh.vertex((radius * math.cos(a), y, radius * math.sin(a)))
              for a in (2 * math.pi * j / segments for j in range(segments))] for y in (-height / 2, height / 2)]
    for j in range(segments):
        k = (j + 1) % segments
        a = 2 * math.pi * (j + 0.5) / segments
        mesh.face([rings[0][j], rings[0][k], rings[1][k], rings[1][j]], (math.cos(a), 0.0, math.sin(a)), "s")
    mesh.face(rings[0], (0.0, -1.0, 0.0), "s")
    mesh.face(rings[1], (0.0, 1.0, 0.0), "s")
    return mesh


def round_shape(kind, radius, height):
    return {"shape": kind, "center": [0.0, 0.0, 0.0], "radius": radius, "height": height,
            "rotation": [0.0, 0.0, 0.0, 1.0], "material": "steel", "landable": False}


def test_cylinder_primitive_follows_a_cylinder_mesh_and_a_longer_capsule_does_not(k01_params):
    data, _ = k01(k01_params)
    lod0 = cylinder_mesh(0.06, 1.0)
    data["collision"] = [round_shape("cylinder", 0.06, 1.0)]
    problems, lines = audit.run(data, lod0)
    assert problems == []
    assert measured_mm(lines[0]) < 0.5 and measured_mm(lines[1]) < 0.5, lines
    data["collision"] = [round_shape("capsule", 0.06, 1.12)]  # its poles stand 60 mm beyond the flat caps
    problems, _ = audit.run(data, lod0)
    assert [p.split(" ")[0:2] for p in problems] == [["collision[0]", "capsule:"], ["mesh:", "mesh->collision"]]
    assert all(measured_mm(p) == pytest.approx(60.0, abs=0.5) and p.endswith("exceeds the tolerance 50 mm")
               for p in problems), problems


@pytest.mark.parametrize("shape, lo, hi", [
    ({"shape": "sphere", "center": [0.0, 0.0, 0.0], "radius": 0.05}, (-0.05, -0.05, -0.05), (0.05, 0.05, 0.05)),
    (round_shape("capsule", 0.05, 1.0), (-0.05, -0.5, -0.05), (0.05, 0.5, 0.05)),
    (round_shape("cylinder", 0.05, 1.0), (-0.05, -0.5, -0.05), (0.05, 0.5, 0.05)),
    ({"shape": "box", "center": [1.0, 0.0, 0.0], "size": [1.0, 2.0, 3.0],
      "rotation": [0.0, math.sin(math.pi / 4), 0.0, math.cos(math.pi / 4)]}, (-0.5, -1.0, -0.5), (2.5, 1.0, 0.5)),
])
def test_tessellations_span_their_dimensions_with_outward_normals(shape, lo, hi):
    tris = audit._Shape(shape).triangles
    points = [p for a, b, c, _ in tris for p in (a, b, c)]
    assert tuple(min(p[k] for p in points) for k in range(3)) == pytest.approx(lo, abs=audit.CHORD_M)
    assert tuple(max(p[k] for p in points) for k in range(3)) == pytest.approx(hi, abs=audit.CHORD_M)
    for a, b, c, n in tris:
        centroid = audit._centroid(a, b, c)
        assert audit._dot(n, audit._sub(centroid, shape["center"])) > 0


@pytest.mark.parametrize("shape", [
    {"shape": "sphere", "center": [0.1, 0.2, 0.3], "radius": 0.05},
    round_shape("capsule", 0.05, 1.0), round_shape("cylinder", 0.05, 1.0),
    {"shape": "box", "center": [1.0, 0.0, 0.0], "size": [1.0, 2.0, 3.0],
     "rotation": [0.0, math.sin(math.pi / 4), 0.0, math.cos(math.pi / 4)]},
])
def test_signed_distance_is_zero_on_the_tessellation_and_bounds_a_sunk_cell(shape):
    prim = audit._Shape(shape)
    for a, b, c, n in prim.triangles[::7]:
        assert abs(prim.sdf(audit._centroid(a, b, c))) <= audit.CHORD_M
        assert prim.sdf(audit._add(a, tuple(0.01 * k for k in n))) == pytest.approx(0.01, abs=audit.CHORD_M)
        sunk = tuple(audit._sub(p, tuple(0.003 * k for k in n)) for p in (a, b, c))
        assert prim.bound(sunk) >= 0.003 - 1e-9 and prim.bound(sunk) <= 0.003 + 0.01
    assert prim.bound(prim.triangles[0][:3]) <= audit.CHORD_M + 1e-9


def door(narrowest):
    return {"id": "door", "polygon": [[0.0, 0.0, 0.0], [0.9, 0.0, 0.0], [0.9, 2.0, 0.0], [0.0, 2.0, 0.0]],
            "normal": [0.0, 0.0, 1.0], "narrowest_m": narrowest}


def test_narrowest_m_is_held_to_the_largest_inscribed_circle(k01_params):
    data, lod0 = k01(k01_params)
    data["gaps"] = [door(0.9)]
    problems, lines = audit.run(data, lod0)
    assert problems == []
    assert lines[2].startswith("audit gaps[0] 'door': narrowest_m 0.9000, largest inscribed circle 0.")
    data["gaps"] = [door(0.95)]
    problems, _ = audit.run(data, lod0)
    assert len(problems) == 1 and problems[0].startswith("gaps[0] 'door': narrowest_m 0.9500, largest inscribed circle 0.")
    assert problems[0].endswith(": they differ by more than 5 mm")


def test_concave_gap_takes_the_circle_in_its_corner(k01_params):
    # An L: a 2 x 2 square without its top-right quarter. The widest circle sits in the bottom-left corner,
    # touching both outer walls and the reflex corner: r = sqrt(2) / (1 + sqrt(2)), not the arms' 0.5.
    data, lod0 = k01(k01_params)
    diameter = 2 * math.sqrt(2) / (1 + math.sqrt(2))
    polygon = [[0.0, 0.0, 0.0], [2.0, 0.0, 0.0], [2.0, 1.0, 0.0], [1.0, 1.0, 0.0], [1.0, 2.0, 0.0], [0.0, 2.0, 0.0]]
    data["gaps"] = [{"id": "L", "polygon": polygon, "normal": [0.0, 0.0, 1.0], "narrowest_m": diameter}]
    assert audit.run(data, lod0)[0] == []
    data["gaps"][0]["narrowest_m"] = 1.0
    assert len(audit.run(data, lod0)[0]) == 1


def test_a_mesh_with_no_primitive_is_refused(k01_params):
    data, lod0 = k01(k01_params)
    data["collision"] = []
    assert audit.run(data, lod0) == (["mesh: no collision primitive stands for the LOD0 mesh"], [])


CARD_VISUALS = {"leaf_shrub"}
A, B = (-0.3, 1.3, 0.0), (0.3, 1.3, 0.0)  # the centres of the two leaf boxes and of the spheres hugging them
HUG_R = 0.38
CORNER_STANDOFF_MM = (0.3 - HUG_R / math.sqrt(3)) * 1000  # a hugging sphere's surface along a box diagonal
SHELL_MM = audit.SHELL_PRECISION_M * 1000 + 0.01  # the shell standoff's precision on spheres, in mm


def card_box(mesh, lo, hi, skip=()):
    """Leaf cards on the faces of the box lo..hi with outward normals, one quad a face; `skip` names faces left
    out ('x-', 'y+', ...)."""
    for axis in range(3):
        for side in (0, 1):
            if "xyz"[axis] + "-+"[side] in skip:
                continue
            u, v = (axis + 1) % 3, (axis + 2) % 3
            corners = []
            for a, b in ((0, 0), (1, 0), (1, 1), (0, 1)):
                p = [0.0, 0.0, 0.0]
                p[axis], p[u], p[v] = (lo, hi)[side][axis], (lo, hi)[a][u], (lo, hi)[b][v]
                corners.append(mesh.vertex(p))
            mesh.face(corners, tuple(float(side * 2 - 1) if k == axis else 0.0 for k in range(3)), "leaf")


def sphere(centre, radius):
    return {"shape": "sphere", "center": list(centre), "radius": radius, "material": "twig", "landable": False}


def hugged():
    """A 1 m stem tube with its bark capsule under a leaf mass of two 0.6 m card boxes side by side (sharing the
    card between them) round a twig tube, each box hugged by a twig sphere of 0.38 m about its centre: the spheres
    overlap, every point of their surface stands within 81 mm of a card, the twig lies inside them and the box
    corners stand 140 mm outside."""
    lod0 = Mesh()
    skeleton._tube(lod0, [((0.0, 0.0, 0.0), 0.009), ((0.0, 1.0, 0.0), 0.009)], 6, "stem", cap=True)
    card_box(lod0, (-0.6, 1.0, -0.3), (0.0, 1.6, 0.3))
    card_box(lod0, (0.0, 1.0, -0.3), (0.6, 1.6, 0.3), skip=("x-",))
    skeleton._tube(lod0, [(A, 0.003), (B, 0.003)], 3, "twig", cap=False)
    data = {"materials": [{"slot": "stem", "visual": "bark_shrub", "physics": "bark"},
                          {"slot": "twig", "visual": "bark_shrub", "physics": "twig"},
                          {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}],
            "collision": [{"shape": "capsule", "center": [0.0, 0.5, 0.0], "radius": 0.009, "height": 1.018,
                           "rotation": [0.0, 0.0, 0.0, 1.0], "material": "bark", "landable": False},
                          sphere(A, HUG_R), sphere(B, HUG_R)],
            "gaps": []}
    return data, lod0


def shell_lines(lines):
    return [line for line in lines if "shell" in line]


def test_two_overlapping_spheres_hugging_the_mass_pass_and_print_both_ways():
    problems, lines = audit.run(*hugged(), CARD_VISUALS)
    assert problems == []
    # the capsule's bottom pole stands its radius under the tube's open base, r * sqrt(2) = 12.7 mm from the base
    # ring; the tip cap's centre sits the radius, 9 mm, inside the capsule's top cap
    assert lines[0].startswith("audit collision[0] capsule: capsule->mesh ") and measured_mm(lines[0]) < 13.0
    assert lines[1].startswith("audit mesh: mesh->collision 9.000 mm at (0, 1, 0) nearest collision[0]")
    shell = shell_lines(lines)
    assert shell[0] == "audit twig shell: 2 pieces (2 spheres)"
    for line, piece in zip(shell[1:3], (1, 2)):
        assert line.startswith(f"audit collision[{piece}] sphere (twig shell): largest standoff ")
        assert line.endswith(", tolerance 100 mm")
        assert measured_mm(line) == pytest.approx(CORNER_STANDOFF_MM, abs=SHELL_MM)
    corner = (math.sqrt(3) * 0.3 - HUG_R) * 1000
    assert shell[3].startswith(f"audit slot leaf (cards, twig shell): largest outside distance {corner:.3f} mm at (")
    assert shell[3].endswith(") over 12 vertices, allowance 300 mm")
    assert shell[4] == ("audit slot twig (geometry, twig shell): largest outside distance 0.000 mm over 6 vertices, "
                        "tolerance 100 mm")
    assert len(lines) == 2 + len(shell)


def test_a_piece_30cm_too_large_fails_its_standoff():
    data, lod0 = hugged()
    data["collision"][1]["radius"] += 0.3
    problems, _ = audit.run(data, lod0, CARD_VISUALS)
    assert len(problems) == 1
    assert problems[0].startswith("collision[1] sphere (twig shell): largest standoff ")
    assert problems[0].endswith(" exceeds the tolerance 100 mm")
    # 0.3 m beyond the 80 mm the hugging sphere stood off the box's faces along its axes
    assert measured_mm(problems[0]) == pytest.approx(380.0, abs=SHELL_MM)


def test_a_twig_vertex_15cm_outside_fails_naming_the_slot_the_distance_and_the_tolerance():
    data, lod0 = hugged()
    tip = math.sqrt((HUG_R + 0.15) ** 2 - 0.0015 ** 2)  # the tip ring's 1.5 mm, about an axis through the centre
    skeleton._tube(lod0, [(A, 0.0025), ((A[0], A[1], A[2] + tip), 0.0015)], 3, "twig", cap=False)
    problems, _ = audit.run(data, lod0, CARD_VISUALS)
    assert len(problems) == 1
    assert problems[0].startswith("slot twig (geometry, twig shell): largest outside distance 150.000 mm at (")
    assert problems[0].endswith(") exceeds the tolerance 100 mm")


@pytest.mark.parametrize("outside_m, fails", [(0.35, True), (0.25, False)])
def test_a_card_corner_35cm_outside_fails_and_25cm_passes(outside_m, fails):
    data, lod0 = hugged()
    length = math.sqrt((HUG_R + outside_m) ** 2 - 0.05 ** 2) - 0.3  # a 0.1 m wide sprig off the box's end card
    corners = [(-0.6, 1.25, 0.0), (-0.6, 1.35, 0.0), (-0.6 - length, 1.35, 0.0), (-0.6 - length, 1.25, 0.0)]
    lod0.face([lod0.vertex(c) for c in corners], (0.0, 0.0, 1.0), "leaf")
    problems, lines = audit.run(data, lod0, CARD_VISUALS)
    text = f"slot leaf (cards, twig shell): largest outside distance {outside_m * 1000:.3f} mm at ("
    if fails:
        assert len(problems) == 1 and problems[0].startswith(text)
        assert problems[0].endswith(") exceeds the allowance 300 mm")
    else:
        assert problems == [] and any(line.startswith("audit " + text) for line in lines)


@pytest.mark.parametrize("lift", [0.0, 0.5])
def test_surface_under_the_ground_is_ignored(lift):
    """A sphere centred on the ground in a leaf box open at the bottom: its upper half hugs the cards, its lower
    half stands up to 0.48 m off them under the ground. Lifted 0.5 m, the same lower half fails."""
    lod0 = Mesh()
    card_box(lod0, (-0.3, lift, -0.3), (0.3, lift + 0.3, 0.3), skip=("y-",))
    data = {"materials": [{"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}],
            "collision": [sphere((0.0, lift, 0.0), HUG_R)], "gaps": []}
    problems, lines = audit.run(data, lod0, CARD_VISUALS)
    if lift == 0.0:
        assert problems == []
        assert measured_mm(shell_lines(lines)[1]) == pytest.approx(CORNER_STANDOFF_MM, abs=SHELL_MM)
    else:
        # the bottom pole, 0.38 m under the box's open base, whose edges lie 0.3 m to its sides
        assert len(problems) == 1 and problems[0].startswith("collision[0] sphere (twig shell): largest standoff ")
        assert measured_mm(problems[0]) == pytest.approx(math.hypot(0.3, HUG_R) * 1000, abs=SHELL_MM)


def test_a_capsule_off_its_tube_by_7cm_still_fails():
    data, lod0 = hugged()
    data["collision"][0]["center"][0] += 0.07
    problems, _ = audit.run(data, lod0, CARD_VISUALS)
    assert [p.split(" ")[:2] for p in problems] == [["collision[0]", "capsule:"], ["mesh:", "mesh->collision"]]
    # 70 mm, give or take the 6-sided tube's 1.2 mm between its inradius and its circumradius
    assert all(measured_mm(p) == pytest.approx(70.0, abs=1.5) and p.endswith("exceeds the tolerance 50 mm") for p in problems)


def test_a_shell_without_a_slot_and_a_slot_without_a_shell_are_refused():
    data, lod0 = hugged()
    for piece in data["collision"][1:]:
        piece["material"] = "foliage"  # the twig and leaf slots still carry twig
    problems, lines = audit.run(data, lod0, CARD_VISUALS)
    assert problems == ["collision[1] sphere (foliage shell): no material slot carries foliage, so it encloses nothing",
                        "collision[2] sphere (foliage shell): no material slot carries foliage, so it encloses nothing",
                        "slots leaf, twig carry twig but no twig shell stands for them"]
    assert lines[-1] == "audit collision[2] sphere (foliage shell): no face of slots none to stand off"
