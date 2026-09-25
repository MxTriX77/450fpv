"""Checks validate_map.py against every map-format scenario, on game/maps/sample_patch and broken copies of it.

    python tools/map/test_validate_map.py [WORK_DIR]

Each case copies the sample (and the shared tables when it breaks them) into WORK_DIR, breaks one thing, runs the
validator and checks its exit code and message. Prints PASS/FAIL per case; exits 1 if any case fails.
"""
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib

from mappng import GREY, read_png, scanlines, write_png

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.join(os.path.dirname(os.path.dirname(HERE)), "game")
SAMPLE = os.path.join(GAME, "maps", "sample_patch")
SURFACES = os.path.join(GAME, "maps", "surfaces.json")
CATALOG = os.path.join(GAME, "assets", "catalog.json")


def edit_json(path, change):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    change(data)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


def set_surface_pixel(pkg, row, col, value):
    png = read_png(os.path.join(pkg, "surface.png"))
    rows = [bytearray(r) for r in scanlines(png)]
    rows[row][col] = value
    write_png(os.path.join(pkg, "surface.png"), png.width, png.height, GREY, [bytes(r) for r in rows])


def add_png_chunk(pkg, name, kind, body):
    path = os.path.join(pkg, name)
    with open(path, "rb") as f:
        data = f.read()
    chunk = struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))
    with open(path, "wb") as f:
        f.write(data[:33] + chunk + data[33:])  # right after the 8-byte signature and the 25-byte IHDR chunk


def remove(name):
    return lambda pkg, tables: os.remove(os.path.join(pkg, name))


def broken_surface(change):
    def mutate(pkg, tables):
        edit_json(tables["surfaces"], change)
    return mutate


def broken_catalog(change):
    def mutate(pkg, tables):
        edit_json(tables["catalog"], change)
    return mutate


def surface(data, sid):
    return next(s for s in data["surfaces"] if s["id"] == sid)


# (name, mutation, expect success, substrings the output must contain)
CASES = [
    ("complete package", lambda pkg, tables: None, True, ["OK:"]),
    *[(f"missing layer {name}", remove(name), False, ["missing layer", name])
      for name in ("map.json", "height.r16", "surface.png", "cover.png", "objects.json")],
    ("size mismatch", lambda pkg, tables: open(os.path.join(pkg, "height.r16"), "ab").write(b"\0\0"),
     False, ["height.r16 is 132100 bytes, expected 132098"]),
    ("surface grid mismatch", lambda pkg, tables: write_png(os.path.join(pkg, "surface.png"), 256, 256, GREY, [b"\x01" * 256] * 256),
     False, ["surface.png is 256×256 pixels, expected 512×512"]),
    ("cover wrong pixel format", lambda pkg, tables: shutil.copy(os.path.join(pkg, "surface.png"), os.path.join(pkg, "cover.png")),
     False, ["cover.png must be 8-bit RGBA"]),
    ("side not a 256 m multiple", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(size_m=300)),
     False, ["size_m 300 breaks the side rule: a multiple of 256 m", "at most 8192 m"]),
    ("side above 8192 m", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(size_m=8448)),
     False, ["size_m 8448 breaks the side rule"]),
    ("manifest grid mismatch", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                             lambda d: d["height"].update(samples_per_side=256)),
     False, ["height.samples_per_side is 256, expected 257"]),
    ("unknown surface", lambda pkg, tables: set_surface_pixel(pkg, 10, 20, 42),
     False, ["unknown surface index 42", "first at row 10, column 20"]),
    ("surface index 0", lambda pkg, tables: set_surface_pixel(pkg, 511, 0, 0),
     False, ["unknown surface index 0 (0 is invalid)", "row 511, column 0"]),
    ("surface field missing", broken_surface(lambda d: surface(d, "meadow_sod")["soil"].pop("unload_stiffness_ratio")),
     False, ["surface 'meadow_sod': soil.unload_stiffness_ratio is missing"]),
    ("negative stiffness", broken_surface(lambda d: surface(d, "dry_crust")["soil"].update(bearing_n_per_m3=-5e6)),
     False, ["surface 'dry_crust': soil.bearing_n_per_m3 = -5000000.0 is outside"]),
    ("unload ratio below 1", broken_surface(lambda d: surface(d, "tilled")["soil"].update(unload_stiffness_ratio=0.5)),
     False, ["surface 'tilled': soil.unload_stiffness_ratio = 0.5 is outside 1 to 100"]),
    ("soil reference diameter missing", broken_surface(lambda d: d.pop("soil_reference_diameter_m")),
     False, ["surfaces.json: soil_reference_diameter_m is missing"]),
    ("ridge azimuth out of range", broken_surface(lambda d: surface(d, "tilled")["micro_relief"]["ridges"].update(azimuth_deg=200)),
     False, ["surface 'tilled': micro_relief.ridges.azimuth_deg = 200 is outside 0 to 180"]),
    ("mat depth min above max", broken_surface(lambda d: surface(d, "belt_straw")["cover"][0]["mat"].update(depth_m=[0.2, 0.05])),
     False, ["surface 'belt_straw': cover.straw.mat.depth_m = [0.2, 0.05] must satisfy"]),
    ("mat kinetic above static", broken_surface(lambda d: surface(d, "meadow_sod")["cover"][0]["mat"].update(friction_kinetic=0.6)),
     False, ["surface 'meadow_sod': cover.grass.mat.friction_kinetic is greater than cover.grass.mat.friction_static"]),
    ("zero element length", broken_surface(lambda d: surface(d, "weeds")["cover"][0].update(height_m=[0, 1.5])),
     False, ["surface 'weeds': cover.grass.height_m min must be above 0"]),
    ("zero element diameter", broken_surface(lambda d: surface(d, "dry_crust")["cover"][0].update(diameter_m=[0, 0.003])),
     False, ["surface 'dry_crust': cover.grass.diameter_m min must be above 0"]),
    ("hook release missing", broken_surface(lambda d: surface(d, "rubble")["cover"][0].pop("hook_release_n")),
     False, ["surface 'rubble': cover.twigs.hook_release_n is missing"]),
    ("cover field missing", broken_surface(lambda d: surface(d, "belt_straw")["cover"][0].pop("hook_probability")),
     False, ["surface 'belt_straw': cover.straw.hook_probability is missing"]),
    ("inverted range", broken_surface(lambda d: surface(d, "weeds")["pitfalls"].update(depth_m=[0.3, 0.05])),
     False, ["surface 'weeds': pitfalls.depth_m = [0.3, 0.05] must satisfy"]),
    ("unknown material", broken_catalog(lambda d: d["assets"]["house_box"].update(material="concrete")),
     False, ["asset 'house_box': material 'concrete' is not in the material table"]),
    ("unknown shape material", broken_catalog(lambda d: d["assets"]["gate_frame"]["collision"][2].update(material="brass")),
     False, ["asset 'gate_frame': box shape material 'brass' is not in the material table"]),
    ("collider without material", broken_catalog(lambda d: d["assets"]["pole"].pop("material")),
     False, ["asset 'pole': material is required because the asset has collision"]),
    ("material kinetic above static", broken_catalog(lambda d: d["materials"]["steel"].update(friction_kinetic=0.5)),
     False, ["material 'steel': friction_kinetic is greater than friction_static"]),
    ("material stiffness out of range", broken_catalog(lambda d: d["materials"]["sheet_metal"].update(stiffness_n_per_m=10)),
     False, ["material 'sheet_metal': stiffness_n_per_m = 10 is outside"]),
    ("wind volume not a primitive", broken_catalog(lambda d: d["assets"]["tree_proxy"]["wind_volume"][0].update(shape="capsule_chain")),
     False, ["asset 'tree_proxy': wind_volume shape 'capsule_chain' is not one of box, sphere, cylinder, capsule"]),
    ("start point inside", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(
        starts=[{"position_m": [0.0, 0.5, 10.0], "yaw_deg": 90.0}])),
     True, ["OK:"]),
    ("start outside the map", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(
        starts=[{"position_m": [0.0, 0.5, 10.0], "yaw_deg": 90.0}, {"position_m": [20.0, 0.0, 129.0], "yaw_deg": 0.0}])),
     False, ["map.json: start point 1 at x=20, z=129 is outside the map"]),
    ("unknown asset", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                    lambda d: d["objects"][3].update(asset="tank_hull")),
     False, ["object 3: unknown asset 'tank_hull'"]),
    ("object out of bounds", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                           lambda d: d["objects"][0]["position_m"].__setitem__(0, 131.0)),
     False, ["object 0 (house_box): x=131", "outside the map"]),
    ("wire point out of bounds", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                               lambda d: d["objects"][9]["points_m"][1].__setitem__(2, -140.0)),
     False, ["object 9 (cable)", "z=-140 is outside the map"]),
    ("future major version", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                           lambda d: d.update(format_version="2.0")),
     False, ["format_version 2.0 is not supported; this validator reads 1.x"]),
    ("newer minor version", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                          lambda d: d.update(format_version="1.3")),
     True, ["OK:"]),
    ("latitude field", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(latitude=48.6)),
     False, ["field 'latitude' is a georeference field"]),
    ("nested lat field", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                       lambda d: d["objects"][0].update(origin_lat=48.6)),
     False, ["field 'objects.origin_lat' is a georeference field"]),
    ("EPSG field", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(EPSG=4326)),
     False, ["field 'EPSG' is a georeference field"]),
    ("geo field", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"), lambda d: d.update(geoRef={"x": 1})),
     False, ["field 'geoRef' is a georeference field"]),
    ("real place name", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                      lambda d: d["objects"][0].update(note="house near Bakhmut")),
     False, ["text 'Bakhmut' looks like a real-world place"]),
    ("real place in Cyrillic", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                             lambda d: d.update(title="Околиці Покровська")),
     False, ["looks like a real-world place"]),
    ("coordinate text", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                      lambda d: d.update(note="start 48.5912, 38.0012")),
     False, ["text '48.5912, 38.0012' looks like a real-world place or coordinate"]),
    ("MGRS text", lambda pkg, tables: edit_json(os.path.join(pkg, "objects.json"),
                                                lambda d: d["objects"][1].update(note="37U DQ 12345 67890")),
     False, ["looks like a real-world place or coordinate"]),
    ("place in catalog", lambda pkg, tables: edit_json(tables["catalog"],
                                                       lambda d: d["assets"]["pole"].update(note="Kupiansk road pole")),
     False, ["catalog.json: text 'Kupiansk'"]),
    ("PNG text chunk", lambda pkg, tables: add_png_chunk(pkg, "cover.png", b"tEXt", b"Comment\0traced over Kherson map"),
     False, ["cover.png tEXt metadata", "Kherson"]),
    ("PNG EXIF chunk", lambda pkg, tables: add_png_chunk(pkg, "surface.png", b"eXIf", b"MM\0*\0\0\0\x08\0\0"),
     False, ["surface.png: has EXIF metadata"]),
    ("GIS sidecar", lambda pkg, tables: open(os.path.join(pkg, "surface.pgw"), "w").write("0.5\n0\n0\n-0.5\n0\n0\n"),
     False, ["surface.pgw: GIS georeference sidecar"]),
    ("innocent geometry field", lambda pkg, tables: edit_json(os.path.join(pkg, "map.json"),
                                                              lambda d: d.update(geometry_note="flat")),
     True, ["OK:"]),
]


def main():
    work = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="validate_map_")
    sys.stdout.reconfigure(encoding="utf-8")  # the validator quotes Cyrillic; Windows pipes default to cp1252
    failures = 0
    for n, (name, mutate, succeed, expected) in enumerate(CASES):
        case = os.path.join(work, f"case{n:02d}")
        shutil.rmtree(case, ignore_errors=True)
        pkg = os.path.join(case, "sample_patch")
        shutil.copytree(SAMPLE, pkg)
        tables = {"surfaces": os.path.join(case, "surfaces.json"), "catalog": os.path.join(case, "catalog.json")}
        shutil.copy(SURFACES, tables["surfaces"])
        shutil.copy(CATALOG, tables["catalog"])
        mutate(pkg, tables)
        run = subprocess.run([sys.executable, os.path.join(HERE, "validate_map.py"), pkg,
                              "--surfaces", tables["surfaces"], "--catalog", tables["catalog"]],
                             capture_output=True, text=True, encoding="utf-8")
        output = run.stdout + run.stderr
        ok = (run.returncode == 0) == succeed and all(text in output for text in expected)
        failures += not ok
        first = next((line for line in output.splitlines() if line.startswith(("ERROR", "OK"))), output.strip())
        print(f"{'PASS' if ok else 'FAIL'}  {name:32s} exit {run.returncode}  {first}")
        if not ok:
            print(output)
    print(f"{len(CASES) - failures}/{len(CASES)} cases passed (work folder {work})")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
