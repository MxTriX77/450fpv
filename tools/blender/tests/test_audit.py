"""lib.audit: K01 measures as the physics review did (3.288 mm box->mesh, 1.898 mm mesh->box), a mesh grown 6 cm
beyond its box fails naming the primitive, the deviation and the tolerance, a box grown beyond its mesh fails the
other way, the 2 cm rule applies within 0.3 m of a gap or a landable face, curved primitives tessellate to their
dimensions and measure against a mesh, and narrowest_m is held to the largest inscribed circle, concave included
(p1-pipeline-hardening, "Collision audit at build time"). Compliant shells (p1-v13-shrub): cards inside a twig
sphere pass with 0 printed, a card corner 8 cm outside fails naming the shell, the distance and the tolerance, a
capsule off its tube by 7 cm still fails, and a shell no slot carries or a slot no shell stands for is refused."""
import copy
import math
import re

import pytest

from generators import debris_brick
from lib import audit, cards, meta, skeleton
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


SHELL_CENTRE = (0.0, 1.5, 0.0)


def shrub_like():
    """A 1 m vertical stem tube with its bark capsule, and three 0.6 m crossed leaf cards centred 1.5 m up inside a
    0.5 m twig sphere, whose surface stands up to 0.2 m clear of the cards."""
    lod0 = Mesh()
    skeleton._tube(lod0, [((0.0, 0.0, 0.0), 0.009), ((0.0, 1.0, 0.0), 0.009)], 6, "stem", cap=True)
    cards.add_to(lod0, cards.crossed(0.6, 1.2, 1.8), "leaf")
    data = {"materials": [{"slot": "stem", "visual": "bark_shrub", "physics": "bark"},
                          {"slot": "leaf", "visual": "leaf_shrub", "physics": "twig"}],
            "collision": [{"shape": "capsule", "center": [0.0, 0.5, 0.0], "radius": 0.009, "height": 1.018,
                           "rotation": [0.0, 0.0, 0.0, 1.0], "material": "bark", "landable": False},
                          {"shape": "sphere", "center": list(SHELL_CENTRE), "radius": 0.5, "material": "twig", "landable": False}],
            "gaps": []}
    return data, lod0


def test_cards_inside_the_shell_pass_with_zero_printed():
    problems, lines = audit.run(*shrub_like())
    assert problems == []
    # the capsule's bottom pole stands its radius under the tube's open base, r * sqrt(2) = 12.7 mm from the base
    # ring; the tip cap's centre sits the radius, 9 mm, inside the capsule's top cap
    assert lines[0].startswith("audit collision[0] capsule: capsule->mesh ") and measured_mm(lines[0]) < 13.0
    assert lines[1].startswith("audit mesh: mesh->collision 9.000 mm at (0, 1, 0) nearest collision[0]")
    assert lines[2] == ("audit collision[1] sphere (twig shell): largest outside distance 0.000 mm over 12 vertices of "
                        "slots leaf, tolerance 50 mm")
    assert len(lines) == 3


def test_a_card_corner_8cm_outside_fails_naming_the_shell_the_distance_and_the_tolerance():
    data, lod0 = shrub_like()
    corner = next(i for i, v in enumerate(lod0.vertices) if v == (0.3, 1.2, 0.0))
    offset = audit._sub(lod0.vertices[corner], SHELL_CENTRE)
    lod0.vertices[corner] = audit._add(SHELL_CENTRE, tuple(c * 0.58 / audit._dist(offset, (0.0, 0.0, 0.0)) for c in offset))
    problems, lines = audit.run(data, lod0)
    assert len(problems) == 1
    assert problems[0].startswith("collision[1] sphere (twig shell): largest outside distance 80.000 mm at (0.41")
    assert problems[0].endswith(" exceeds the tolerance 50 mm")
    assert "largest outside distance 80.000 mm at (0.41" in lines[2]


def test_a_capsule_off_its_tube_by_7cm_still_fails():
    data, lod0 = shrub_like()
    data["collision"][0]["center"][0] += 0.07
    problems, _ = audit.run(data, lod0)
    assert [p.split(" ")[:2] for p in problems] == [["collision[0]", "capsule:"], ["mesh:", "mesh->collision"]]
    # 70 mm, give or take the 6-sided tube's 1.2 mm between its inradius and its circumradius
    assert all(measured_mm(p) == pytest.approx(70.0, abs=1.5) and p.endswith("exceeds the tolerance 50 mm") for p in problems)


def test_a_shell_without_a_slot_and_a_slot_without_a_shell_are_refused():
    data, lod0 = shrub_like()
    data["collision"][1]["material"] = "foliage"  # the leaf slot still carries twig
    problems, lines = audit.run(data, lod0)
    assert problems == ["collision[1] sphere (foliage shell): no material slot carries foliage, so it encloses nothing",
                        "slots leaf carry twig but no twig shell stands for them"]
    assert lines[-1].endswith("over 0 vertices of slots none, tolerance 50 mm")
