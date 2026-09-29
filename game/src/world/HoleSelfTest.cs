using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;

/// The terrain-hole scenarios of `-- --selftest worldquery` (task 2.3, the terrain-holes spec), on the test trench of
/// game/maps/sample_patch: what SampleGround reports over a hole, that rays and the terrain collision pass over one,
/// that the fillers' soil shapes carry their surface, that no micro-detail grows in a hole, that a buried shape is no
/// wind obstacle, and the hole-filler contract F-1 to F-6. The trench's own numbers are read from the world, not from
/// the generator, except the layout constants below, which the generator and this file must agree on.
public static partial class WorldQuerySelfTest
{
    /// The levelled pad the test trench is cut into: TerrainHeight over the whole trench (make_sample_patch.py).
    const double TrenchLip = -0.36;
    /// The closed end of the straight section, its corner with the turned section, and the turn (degrees).
    static readonly XZ TrenchStart = new(-27.0, 88.75);
    static readonly XZ TrenchCorner = new(-21.0, 88.75);
    const double TrenchBend = 30.0;
    /// Cavity length, half width and depth, and the half width of the hole band (make_sample_patch.py, the catalog).
    const double TrenchLength = 6.0, TrenchHalf = 0.4, TrenchDepth = 1.5, HoleHalf = 0.75;
    /// The lip's rise above TerrainHeight: the fillers stand this far proud so nothing z-fights with the ground (F-3, F-4).
    const double LipRise = 0.005;
    /// The filler's surface (F-7).
    const string TrenchSurface = "belt_bare";
    /// Collision layers in the selftest's own space: the terrain keeps layer 1, the fillers get layer 2, so a check of
    /// the terrain alone can mask them out.
    const uint TerrainLayer = 1, FillerLayer = 2;

    static bool HoleScenarios(WorldQuery world, string dir, string surfacesPath, PhysicsDirectSpaceState3D space)
    {
        bool pass = HoleLayerLoaded(world);
        pass &= SampleOverAHole(world);
        pass &= RaysIntoTheTrench(world);
        pass &= TrenchWallIsSoil(world);
        pass &= NoStemsInTheTrench(world, dir, surfacesPath);
        pass &= NoWindFromBuriedShapes(world, surfacesPath);
        pass &= FillerContract(world);
        pass &= JoltOverHoles(world, space);
        return pass;
    }

    /// The two cavity axes: each a closed end and a unit direction along the trench, in world (x, z).
    static (XZ End, double Cos, double Sin)[] TrenchAxes()
    {
        DetMath.SinCosTurns(TrenchBend / 360.0, out double sin, out double cos);
        var far = new XZ(TrenchCorner.X + TrenchLength * cos, TrenchCorner.Z + TrenchLength * sin);
        return new[] { (TrenchStart, 1.0, 0.0), (far, -cos, -sin) };
    }

    /// A point in a cavity's frame: `along` from its closed end, `across` from its axis, `up` from the lip.
    static Double3 InTrench(int section, double along, double across, double up)
    {
        (XZ end, double cos, double sin) = TrenchAxes()[section];
        return new Double3(end.X + along * cos - across * sin, TrenchLip + up, end.Z + along * sin + across * cos);
    }

    /// Every hole cell of the map as (row, column), and the world centre of a cell.
    static List<(int Row, int Column)> HoleCells(WorldQuery world)
    {
        var cells = new List<(int, int)>();
        for (int row = 0; row < world.Cells; row++)
        {
            for (int column = 0; column < world.Cells; column++)
            {
                if (world.HoleCell(row, column))
                    cells.Add((row, column));
            }
        }
        return cells;
    }

    static XZ CellCorner(WorldQuery world, int row, int column) =>
        new(-world.Half + column * world.CellResolution, -world.Half + row * world.CellResolution);

    /// The objects that fill the holes: every placed object whose asset is one of the test trench's.
    static int[] Fillers(WorldQuery world)
    {
        var objects = new List<int>();
        for (int i = 0; i < world.ObjectCount; i++)
        {
            if (world.PlacementOf(i).Asset.Id.StartsWith("test_trench", StringComparison.Ordinal))
                objects.Add(i);
        }
        return objects.ToArray();
    }

    // ---------------------------------------------------------------- the layer and SampleGround

    /// The package's hole layer is loaded, and it covers the test trench and nothing else: as many cells as the trench's
    /// footprint needs, all of them on the levelled pad.
    static bool HoleLayerLoaded(WorldQuery world)
    {
        List<(int Row, int Column)> cells = HoleCells(world);
        var points = cells.Select(c => new XZ(CellCorner(world, c.Row, c.Column).X + world.CellResolution / 2,
            CellCorner(world, c.Row, c.Column).Z + world.CellResolution / 2)).ToArray();
        GroundSample[] samples = points.Length > 0 ? SampleAll(world, points) : Array.Empty<GroundSample>();
        int flagged = samples.Count(g => (g.Flags & GroundFlags.Hole) != 0);
        double worstLip = samples.Length > 0 ? samples.Max(g => Math.Abs(g.TerrainHeight - TrenchLip)) : 0;
        int[] fillers = Fillers(world);
        return Check("hole layer loaded", world.HasHoles && cells.Count > 0 && flagged == cells.Count && worstLip <= 1e-6
                && fillers.Length > 0,
            $"holes.png gives {cells.Count} hole cells ({cells.Count * world.CellResolution * world.CellResolution:0.0} m²), "
            + $"all flagged: {flagged == cells.Count}; every cell centre is on the levelled pad within {worstLip * 1000:0.000000} mm "
            + $"of {TrenchLip} m; {fillers.Length} filler objects ({string.Join(", ", fillers)})");
    }

    /// A 1 cm grid across both sections, plus the hole boundary ± 1 mm: over a hole cell SampleGround reports the lip as
    /// TerrainHeight, −∞ ground and support, +Y, no mat, no cover and no feature; 1 mm outside it reports ordinary ground.
    static bool SampleOverAHole(WorldQuery world)
    {
        var points = new List<XZ>();
        var expectHole = new List<bool>();
        for (int section = 0; section < 2; section++)
        {
            for (int step = 0; step <= 400; step++)
            {
                double across = -2.0 + step * 0.01;
                Double3 p = InTrench(section, TrenchLength / 2, across, 0);
                points.Add(new XZ(p.X, p.Z));
                expectHole.Add(world.IsHole(p.X, p.Z));
            }
            // The boundary of the straight section's band lies on cell edges; 1 mm either side of it must differ.
            foreach (double across in new[] { -HoleHalf, HoleHalf })
            {
                foreach (double off in new[] { -1e-3, 1e-3 })
                {
                    Double3 p = InTrench(section, TrenchLength / 2, across + off, 0);
                    points.Add(new XZ(p.X, p.Z));
                    expectHole.Add(world.IsHole(p.X, p.Z));
                }
            }
        }
        GroundSample[] samples = SampleAll(world, points.ToArray());
        int holes = 0, ground = 0, bad = 0;
        string first = "";
        for (int i = 0; i < samples.Length; i++)
        {
            GroundSample g = samples[i];
            bool isHole = (g.Flags & GroundFlags.Hole) != 0;
            bool ok = isHole == expectHole[i];
            if (isHole)
            {
                holes++;
                ok &= Math.Abs(g.TerrainHeight - TrenchLip) <= 1e-6 && double.IsNegativeInfinity(g.GroundHeight)
                    && double.IsNegativeInfinity(g.SupportTop) && g.Normal == new System.Numerics.Vector3(0, 1, 0)
                    && g.MatDepth == System.Numerics.Vector4.Zero && g.CoverDensity == System.Numerics.Vector4.Zero
                    && g.Feature == GroundFeature.None && g.FeatureId == 0 && g.FeatureDepth == 0
                    && (g.Flags & GroundFlags.OutsideMap) == 0;
            }
            else
            {
                ground++;
                ok &= double.IsFinite(g.GroundHeight) && double.IsFinite(g.SupportTop) && g.Normal.Y > 0;
            }
            if (!ok && bad++ == 0)
                first = $"; first wrong at ({points[i].X:0.0000}, {points[i].Z:0.0000}): hole {isHole}, terrain {g.TerrainHeight:0.000}, "
                    + $"ground {g.GroundHeight}, support {g.SupportTop}, normal {g.Normal}, feature {g.Feature}";
        }
        return Check("sample over a hole", bad == 0 && holes > 0 && ground > 0,
            $"{samples.Length} points across both sections at 1 cm and 1 mm either side of the band edges: {holes} in a hole "
            + $"(lip {TrenchLip} m, −∞ ground and support, +Y, no mat, cover or feature), {ground} on ordinary ground, "
            + $"{bad} wrong{first}");
    }

    /// Rays down, at 45° and grazing over the trench: each hits a filler shape and carries its surface, never the terrain.
    static bool RaysIntoTheTrench(WorldQuery world)
    {
        byte expected = world.Surfaces.First(s => s.Id == TrenchSurface).Index;
        var rays = new List<(string What, Ray Ray)>();
        for (int section = 0; section < 2; section++)
        {
            for (int k = 0; k <= 8; k++)
            {
                double along = TrenchLength * (0.1 + 0.1 * k);
                rays.Add(($"down s{section} at {along:0.0} m", new Ray(InTrench(section, along, 0, 1.0), new Double3(0, -1, 0))));
            }
            foreach (double across in new[] { -0.3, 0.0, 0.3 })
            {
                Double3 from = InTrench(section, TrenchLength / 2 - 1.0, across, 0.6);
                Double3 to = InTrench(section, TrenchLength / 2, across, -1.0);
                rays.Add(($"45° s{section} across {across:0.0} m", new Ray(from, to - from)));
            }
            // Grazing: from 2 cm over the lip to 2 cm under it, so it crosses the lip level in the middle of the band.
            Double3 side = InTrench(section, TrenchLength / 2, -1.2, 0.02);
            Double3 other = InTrench(section, TrenchLength / 2, 1.2, -0.02);
            rays.Add(($"grazing s{section}", new Ray(side, other - side)));
        }
        var hits = new RayHit[rays.Count];
        world.Raycast(rays.Select(r => r.Ray).ToArray(), 10, hits);
        int[] fillers = Fillers(world);
        int bad = 0, terrainHits = 0;
        string first = "";
        for (int i = 0; i < hits.Length; i++)
        {
            RayHit h = hits[i];
            bool filler = Array.IndexOf(fillers, h.Object) >= 0;
            bool ok = filler && h.Surface == expected && h.Material == Catalog.NoMaterial && double.IsFinite(h.Distance);
            terrainHits += h.Object == -1 && double.IsFinite(h.Distance) ? 1 : 0;
            if (!ok && bad++ < 3)
                first += $"; {rays[i].What} hit object {h.Object}, surface {h.Surface}, material {h.Material}, "
                    + $"distance {h.Distance:0.000}";
        }
        // The cavity is 0.8 m wide the whole way: just inside its faces a ray reaches the floor, just outside it stops on
        // the lip.
        var widthRays = new List<Ray>();
        var wide = new List<bool>();
        for (int section = 0; section < 2; section++)
        {
            for (int k = 1; k <= 11; k++)
            {
                foreach (double across in new[] { -0.35, 0.35, -0.45, 0.45 })
                {
                    widthRays.Add(new Ray(InTrench(section, TrenchLength * k / 12.0, across, 1.0), new Double3(0, -1, 0)));
                    wide.Add(Math.Abs(across) < TrenchHalf);
                }
            }
        }
        var widthHits = new RayHit[widthRays.Count];
        world.Raycast(widthRays.ToArray(), 10, widthHits);
        int wrongWidth = 0;
        for (int i = 0; i < widthHits.Length; i++)
        {
            double y = widthHits[i].Point.Y;
            bool ok = wide[i] ? Math.Abs(y - (TrenchLip - TrenchDepth)) <= 1e-3 : Math.Abs(y - (TrenchLip + LipRise)) <= 1e-3;
            wrongWidth += ok && double.IsFinite(widthHits[i].Distance) ? 0 : 1;
        }
        // Horizontal rays across the cavity at three heights: each crosses the open 0.8 m and hits the far wall's face.
        var across3 = new List<Ray>();
        for (int section = 0; section < 2; section++)
        {
            foreach (double depth in new[] { -0.2, -0.75, -1.3 })
            {
                Double3 from = InTrench(section, TrenchLength / 2, -1.0, depth);
                Double3 to = InTrench(section, TrenchLength / 2, 1.0, depth);
                across3.Add(new Ray(from, to - from));
            }
        }
        var wallHits = new RayHit[across3.Count];
        world.Raycast(across3.ToArray(), 5, wallHits);
        int wrongFace = 0;
        double worstFace = 0;
        for (int i = 0; i < wallHits.Length; i++)
        {
            // The ray starts inside the near wall, which it therefore ignores, and must reach the far face at 0.4 m.
            double travel = wallHits[i].Distance;
            worstFace = Math.Max(worstFace, Math.Abs(travel - (1.0 + TrenchHalf)));
            wrongFace += Array.IndexOf(fillers, wallHits[i].Object) >= 0 && Math.Abs(travel - (1.0 + TrenchHalf)) <= 0.05 ? 0 : 1;
        }
        double floor = hits[0].Point.Y;
        return Check("rays into a trench", bad == 0 && terrainHits == 0 && wrongWidth == 0 && wrongFace == 0
                && Math.Abs(floor - (TrenchLip - TrenchDepth)) <= 1e-3,
            $"{hits.Length} rays (18 down, 6 at 45°, 2 grazing): {bad} that did not hit a filler with surface "
            + $"{expected} ({TrenchSurface}) and material NoMaterial, {terrainHits} that hit the terrain; the first ray "
            + $"reaches the floor at y {floor:0.000} (lip {TrenchLip} − depth {TrenchDepth}); of {widthHits.Length} rays "
            + $"at ±0.35 and ±0.45 m across, 11 stations along each section, {wrongWidth} did not find the floor inside the "
            + $"0.8 m cavity or the lip outside it; {across3.Count} horizontal rays across the cavity at 0.2, 0.75 and 1.3 m "
            + $"down reach the far wall's face within {worstFace * 1000:0.0} mm of the drawn one (limit 50 mm), {wrongFace} "
            + $"missing it");
    }

    /// A foot sphere on the floor, on each wall and in the inner corner: the contact carries the trench's surface, no
    /// catalog material, and Geometry gives the same surface. A contact with any other object carries surface 0.
    static bool TrenchWallIsSoil(WorldQuery world)
    {
        byte expected = world.Surfaces.First(s => s.Id == TrenchSurface).Index;
        const double Foot = 0.0075; // a 15 mm foot
        var probes = new List<(string What, Double3 At)>
        {
            ("floor", InTrench(0, TrenchLength / 2, 0, -TrenchDepth + Foot)),
            ("north wall", InTrench(0, TrenchLength / 2, TrenchHalf - Foot, -0.5)),
            ("south wall", InTrench(0, TrenchLength / 2, -TrenchHalf + Foot, -0.5)),
            ("end wall", InTrench(0, Foot, 0, -0.5)),
            ("inner corner", new Double3(-21.107 - Foot, TrenchLip - 0.5, 89.15 - Foot)),
        };
        var contacts = new StaticContact[32];
        int bad = 0, touched = 0;
        string detail = "";
        SurfaceParams soil = null;
        foreach ((string what, Double3 at) in probes)
        {
            var sphere = new Capsule(at, at, Foot);
            int n = world.StaticContacts(sphere, 0.02, contacts);
            int soilContacts = 0;
            for (int i = 0; i < Math.Min(n, contacts.Length); i++)
            {
                StaticContact c = contacts[i];
                if (c.Surface == 0)
                    continue;
                soilContacts++;
                soil = world.Surface(c.Surface);
                bool ok = c.Surface == expected && c.Material == Catalog.NoMaterial && soil != null && soil.Id == TrenchSurface
                    && world.Geometry(c.Object, c.Shape, 0, out ShapeGeometry g) && g.Surface == c.Surface
                    && g.Material == Catalog.NoMaterial;
                bad += ok ? 0 : 1;
            }
            touched += soilContacts > 0 ? 1 : 0;
            bad += soilContacts > 0 ? 0 : 1;
            detail += $"{what} {soilContacts} soil contact(s) of {n}; ";
        }
        // A contact with an ordinary object: surface 0 and a catalog material.
        Placement house = world.PlacementOf(0);
        var onHouse = new Double3(house.Position.X, house.Position.Y + 5.0 + Foot - 0.005, house.Position.Z);
        int roof = world.StaticContacts(new Capsule(onHouse, onHouse, Foot), 0.02, contacts);
        bool houseOk = roof > 0 && contacts[0].Surface == 0 && contacts[0].Material != Catalog.NoMaterial;
        return Check("trench wall is soil", bad == 0 && touched == probes.Count && houseOk,
            $"{detail}every soil contact has surface {expected} ({TrenchSurface}), material NoMaterial and the same surface "
            + $"from Geometry: {bad} wrong; soil bearing {soil?.Bearing:0} N/m³, friction {soil?.FrictionStatic:0.00}; the "
            + $"house roof gives surface {(roof > 0 ? contacts[0].Surface : -1)} and material {(roof > 0 ? contacts[0].Material : 0)}");
    }

    /// Micro-detail over the trench: nothing is rooted in a hole cell and no lying element's tip lies in one, while every
    /// element the same layers give without a hole layer is returned unchanged.
    static bool NoStemsInTheTrench(WorldQuery world, string dir, string surfacesPath)
    {
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        var noHoles = new WorldQuery(table, world.SizeM, world.Seed, world.Samples, HeightsOf(world), world.Cells,
            SurfaceLayer(world), CoverLayer(dir));
        int rooted = 0, tips = 0, missing = 0, differing = 0, kept = 0;
        string first = "";
        for (int section = 0; section < 2; section++)
        {
            for (int k = 0; k < 5; k++)
            {
                Double3 centre = InTrench(section, TrenchLength * (0.1 + 0.2 * k), 0, 0.3);
                MicroElement[] holed = Query(world, centre, 2.0, KindMask.All);
                MicroElement[] whole = Query(noHoles, centre, 2.0, KindMask.All);
                var byId = holed.ToDictionary(e => e.Id);
                foreach (MicroElement e in holed)
                {
                    if (world.IsHole(e.Base.X, e.Base.Z))
                        rooted++;
                    else if (e.Kind != CoverKind.Grass && world.IsHole(e.Base.X + e.Direction.X * e.Length, e.Base.Z + e.Direction.Z * e.Length))
                        tips++;
                }
                foreach (MicroElement e in whole)
                {
                    bool drop = world.IsHole(e.Base.X, e.Base.Z) || e.Kind != CoverKind.Grass
                        && world.IsHole(e.Base.X + e.Direction.X * e.Length, e.Base.Z + e.Direction.Z * e.Length);
                    if (drop)
                        continue;
                    kept++;
                    if (!byId.TryGetValue(e.Id, out MicroElement same))
                    {
                        if (missing++ == 0)
                            first = $"; first missing 0x{e.Id:x16} at ({e.Base.X:0.000}, {e.Base.Z:0.000})";
                        continue;
                    }
                    differing += Bytes(e).SequenceEqual(Bytes(same)) ? 0 : 1;
                }
            }
        }
        return Check("no stems in the trench", rooted == 0 && tips == 0 && missing == 0 && differing == 0 && kept > 0,
            $"10 queries of r 2 m along both sections: {rooted} elements rooted in a hole cell and {tips} lying tips in one "
            + $"(limit 0); of the {kept} elements the same layers give without a hole layer, {missing} missing and "
            + $"{differing} differing byte for byte{first}");
    }

    /// The trench's fillers leave the wind grid open, and a buried shape with porosity 0 changes no cell at all.
    static bool NoWindFromBuriedShapes(WorldQuery world, string surfacesPath)
    {
        int obstacles = 0;
        foreach ((int row, int column) in HoleCells(world))
        {
            XZ corner = CellCorner(world, row, column);
            int wr = (int)Math.Floor((corner.Z + world.Half) / WorldQuery.WindCellSize);
            int wc = (int)Math.Floor((corner.X + world.Half) / WorldQuery.WindCellSize);
            WindCell cell = world.WindGrid[wr * world.WindCells + wc];
            obstacles += cell.Porosity < 1 || cell.TopM != 0 || cell.BaseM != 0 ? 1 : 0;
        }
        // The guard itself: a solid box buried 5 m under a flat world must leave every cell as it was.
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        Catalog catalog = Catalog.Parse(File.ReadAllText(TestCatalog()), table);
        WorldQuery flat = Uniform(table, table[0].Index, world.Seed, catalog);
        WindCell[] before = flat.WindGrid.ToArray();
        flat.AddObject("t_box", new Double3(4, -5, -7), 30);
        int changed = 0;
        for (int i = 0; i < before.Length; i++)
        {
            WindCell a = before[i], b = flat.WindGrid[i];
            changed += a.TopM == b.TopM && a.BaseM == b.BaseM && a.Porosity == b.Porosity ? 0 : 1;
        }
        return Check("buried filler adds no wind obstacle", obstacles == 0 && changed == 0,
            $"of the wind cells over the trench's hole cells, {obstacles} carry an obstacle (limit 0); a solid 0.8 × 0.3 × 1.2 m "
            + $"box buried 5 m under a flat world changes {changed} of {before.Length} wind cells (limit 0)");
    }

    // ---------------------------------------------------------------- the hole-filler contract

    /// F-1 to F-4 on the test trench, measured against the world query, and F-5's thicknesses from the shapes.
    static bool FillerContract(WorldQuery world)
    {
        List<(int Row, int Column)> cells = HoleCells(world);
        int[] fillers = Fillers(world);
        double cell = world.CellResolution;
        bool Filler(int obj) => Array.IndexOf(fillers, obj) >= 0;

        // F-1: a downward ray from TerrainHeight + 0.5 m on a 5 cm grid over every hole cell hits a filler shape.
        var rays = new List<Ray>();
        var at = new List<XZ>();
        foreach ((int row, int column) in cells)
        {
            XZ corner = CellCorner(world, row, column);
            for (int j = 0; j < 10; j++)
            {
                for (int i = 0; i < 10; i++)
                {
                    var p = new XZ(corner.X + (i + 0.5) * cell / 10, corner.Z + (j + 0.5) * cell / 10);
                    at.Add(p);
                    rays.Add(new Ray(new Double3(p.X, TrenchLip + 0.5, p.Z), new Double3(0, -1, 0)));
                }
            }
        }
        var hits = new RayHit[rays.Count];
        world.Raycast(rays.ToArray(), 5, hits);
        int unsealed = 0;
        string firstUnsealed = "";
        for (int i = 0; i < hits.Length; i++)
        {
            if (Filler(hits[i].Object) && double.IsFinite(hits[i].Distance))
                continue;
            if (unsealed++ == 0)
                firstUnsealed = $" (first at ({at[i].X:0.000}, {at[i].Z:0.000}), object {hits[i].Object})";
        }
        bool pass = Check("hole fillers: F-1 sealed below", unsealed == 0,
            $"{hits.Length} downward rays from the lip + 0.5 m on a 5 cm grid over {cells.Count} hole cells: {unsealed} that "
            + $"found no filler under them{firstUnsealed}");

        // F-2: from TerrainHeight down to the cavity floor, every point within 0.25 m of the hole boundary is inside a filler.
        var probes = new List<Double3>();
        foreach ((int row, int column) in cells)
        {
            XZ corner = CellCorner(world, row, column);
            for (int j = 0; j < 10; j++)
            {
                for (int i = 0; i < 10; i++)
                {
                    double x = corner.X + (i + 0.5) * cell / 10, z = corner.Z + (j + 0.5) * cell / 10;
                    bool near = false;
                    for (int k = 0; k < 8 && !near; k++)
                    {
                        DetMath.SinCosTurns(k / 8.0, out double sin, out double cos);
                        near = !world.IsHole(x + 0.25 * cos, z + 0.25 * sin);
                    }
                    if (!near)
                        continue;
                    for (double y = TrenchLip; y >= TrenchLip - TrenchDepth - 1e-9; y -= 0.05)
                        probes.Add(new Double3(x, y, z));
                }
            }
        }
        var contacts = new StaticContact[16];
        int outside = 0;
        string firstOutside = "";
        foreach (Double3 p in probes)
        {
            int n = world.StaticContacts(new Capsule(p, p, 1e-6), 0, contacts);
            bool inside = false;
            for (int i = 0; i < Math.Min(n, contacts.Length); i++)
                inside |= Filler(contacts[i].Object) && contacts[i].Distance <= 0;
            if (!inside && outside++ == 0)
                firstOutside = $" (first at ({p.X:0.000}, {p.Y:0.000}, {p.Z:0.000}))";
        }
        pass &= Check("hole fillers: F-2 cavity inside the hole", outside == 0,
            $"{probes.Count} points on a 5 cm grid within 0.25 m of the hole boundary, from the lip down to the floor: "
            + $"{outside} not inside a filler shape{firstOutside}");

        // F-3: along every boundary edge, the filler's top on the hole side is within 0.03 m of TerrainHeight.
        var edges = new List<XZ>();
        var holeSet = new HashSet<(int, int)>(cells);
        foreach ((int row, int column) in cells)
        {
            foreach ((int dr, int dc) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                if (holeSet.Contains((row + dr, column + dc)))
                    continue;
                XZ corner = CellCorner(world, row, column);
                for (int k = 0; k <= 10; k++)
                {
                    double t = k / 10.0;
                    edges.Add(dc != 0
                        ? new XZ(corner.X + (dc > 0 ? cell - 0.01 : 0.01), corner.Z + t * cell)
                        : new XZ(corner.X + t * cell, corner.Z + (dr > 0 ? cell - 0.01 : 0.01)));
                }
            }
        }
        var edgeRays = edges.Select(p => new Ray(new Double3(p.X, TrenchLip + 0.5, p.Z), new Double3(0, -1, 0))).ToArray();
        var edgeHits = new RayHit[edgeRays.Length];
        world.Raycast(edgeRays, 5, edgeHits);
        double worstLip = 0;
        int noTop = 0;
        for (int i = 0; i < edgeHits.Length; i++)
        {
            if (!Filler(edgeHits[i].Object))
            {
                noTop++;
                continue;
            }
            worstLip = Math.Max(worstLip, Math.Abs(edgeHits[i].Point.Y - TrenchLip));
        }
        pass &= Check("hole fillers: F-3 level lip", noTop == 0 && worstLip <= 0.03,
            $"{edges.Count} samples along {edges.Count / 11} boundary edges: worst |filler top − TerrainHeight| "
            + $"{worstLip * 1000:0.0} mm (limit 30 mm), {noTop} with no filler top");

        // F-4: outside the hole cells, no filler rises above TerrainHeight + 0.01 m.
        var band = new List<XZ>();
        var samples = new List<XZ>();
        int reach = (int)Math.Ceiling(1.0 / cell);
        var around = new HashSet<(int, int)>();
        foreach ((int row, int column) in cells)
        {
            for (int dr = -reach; dr <= reach; dr++)
            {
                for (int dc = -reach; dc <= reach; dc++)
                    around.Add((row + dr, column + dc));
            }
        }
        foreach ((int row, int column) in around)
        {
            if (holeSet.Contains((row, column)))
                continue;
            XZ corner = CellCorner(world, row, column);
            for (int j = 0; j < 10; j++)
            {
                for (int i = 0; i < 10; i++)
                    band.Add(new XZ(corner.X + (i + 0.5) * cell / 10, corner.Z + (j + 0.5) * cell / 10));
            }
        }
        var bandRays = band.Select(p => new Ray(new Double3(p.X, TrenchLip + 0.5, p.Z), new Double3(0, -1, 0))).ToArray();
        var bandHits = new RayHit[bandRays.Length];
        world.Raycast(bandRays, 5, bandHits);
        var terrain = new GroundSample[band.Count];
        world.SampleGround(band.ToArray(), terrain);
        double worstRise = 0;
        int above = 0;
        string firstAbove = "";
        for (int i = 0; i < bandHits.Length; i++)
        {
            if (!Filler(bandHits[i].Object))
                continue;
            double rise = bandHits[i].Point.Y - terrain[i].TerrainHeight;
            worstRise = Math.Max(worstRise, rise);
            if (rise > 0.01 && above++ == 0)
                firstAbove = $" (first at ({band[i].X:0.000}, {band[i].Z:0.000}), {rise * 1000:0.0} mm)";
        }
        pass &= Check("hole fillers: F-4 buried outside", above == 0,
            $"{band.Count} points on a 5 cm grid over a 1 m band around the holes: worst filler top above TerrainHeight "
            + $"{worstRise * 1000:0.0} mm (limit 10 mm), {above} over it{firstAbove}");

        // F-5: the thickness behind each open face, and the overlaps, from the shapes themselves (information for 5.1).
        var thickness = new List<string>();
        foreach (int obj in fillers)
        {
            for (int shape = 0; world.Geometry(obj, shape, 0, out ShapeGeometry g); shape++)
                thickness.Add($"{world.PlacementOf(obj).Asset.Id}[{shape}] {2 * g.HalfExtents.X:0.00} × {2 * g.HalfExtents.Y:0.00} × {2 * g.HalfExtents.Z:0.00} m");
        }
        GD.Print($"selftest worldquery: hole fillers: F-5 shape sizes (asset review 5.1): {string.Join("; ", thickness)}");
        return pass;
    }

    // ---------------------------------------------------------------- Jolt

    /// The fillers' collision in the Jolt space, from the world query's shapes as the loader builds them, so the sweep
    /// below meets the same boxes physics does. Call it while the scene is being set up, before the space is queried.
    static void AddFillerBodies(Node3D sandbox, WorldQuery world)
    {
        foreach (int obj in Fillers(world))
        {
            // Layer 2: the terrain-only ray checks (matches the rendered surface, the cavity rays) mask it out.
            var body = new StaticBody3D { Name = $"{obj} {world.PlacementOf(obj).Asset.Id} collision", CollisionLayer = FillerLayer };
            body.SetMeta("object", obj);
            sandbox.AddChild(body);
            for (int shape = 0; world.Geometry(obj, shape, 0, out ShapeGeometry g); shape++)
            {
                body.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = new Vector3((float)g.HalfExtents.X, (float)g.HalfExtents.Y, (float)g.HalfExtents.Z) * 2 },
                    Position = new Vector3((float)g.Center.X, (float)g.Center.Y, (float)g.Center.Z),
                });
            }
        }
    }

    /// Jolt's terrain over a hole (design TH-9): a NaN sample takes every quad that uses it out of the height field, so
    /// the hole is vertex-granular on the 1 m grid and the removed triangles come back clipped to the non-hole cells.
    /// Measured here on 4.7.2: rays down into the cavity find no terrain, rays just outside the hole land on the rendered
    /// triangle, and a drone-sized capsule swept into the trench passes the terrain level and stops on a filler.
    static bool JoltOverHoles(WorldQuery world, PhysicsDirectSpaceState3D space)
    {
        int inside = 0, throughTerrain = 0;
        for (int section = 0; section < 2; section++)
        {
            for (int k = 1; k <= 9; k++)
            {
                Double3 p = InTrench(section, TrenchLength * k / 10.0, 0, 0);
                inside++;
                var query = PhysicsRayQueryParameters3D.Create(new Vector3((float)p.X, (float)(TrenchLip + 0.5f), (float)p.Z),
                    new Vector3((float)p.X, (float)(TrenchLip - 0.5f), (float)p.Z), TerrainLayer);
                throughTerrain += space.IntersectRay(query).Count == 0 ? 1 : 0;
            }
        }
        // Just outside the hole cells, inside the quads whose corners went NaN: the clipped patch must still be there.
        var outsidePoints = new List<XZ>();
        foreach ((int row, int column) in HoleCells(world))
        {
            foreach ((int dr, int dc) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                if (world.HoleCell(row + dr, column + dc))
                    continue;
                XZ corner = CellCorner(world, row + dr, column + dc);
                outsidePoints.Add(new XZ(corner.X + world.CellResolution / 2, corner.Z + world.CellResolution / 2));
            }
        }
        var ground = new GroundSample[outsidePoints.Count];
        world.SampleGround(outsidePoints.ToArray(), ground);
        int misses = 0;
        double worst = 0;
        for (int i = 0; i < outsidePoints.Count; i++)
        {
            var query = PhysicsRayQueryParameters3D.Create(new Vector3((float)outsidePoints[i].X, 50f, (float)outsidePoints[i].Z),
                new Vector3((float)outsidePoints[i].X, -50f, (float)outsidePoints[i].Z), TerrainLayer);
            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            if (hit.Count == 0)
                misses++;
            else
                worst = Math.Max(worst, Math.Abs(((Vector3)hit["position"]).Y - ground[i].TerrainHeight));
        }

        // A drone-sized capsule swept down the middle of the straight section.
        var shape = new CapsuleShape3D { Radius = 0.25f, Height = 0.6f };
        Double3 top = InTrench(0, TrenchLength / 2, 0, 1.0);
        var parameters = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, new Vector3((float)top.X, (float)top.Y, (float)top.Z)),
            Motion = new Vector3(0, -2.6f, 0),
        };
        float[] motion = space.CastMotion(parameters);
        double travel = motion[0] * 2.6;
        parameters.Transform = new Transform3D(Basis.Identity,
            new Vector3((float)top.X, (float)(top.Y - motion[1] * 2.6), (float)top.Z));
        parameters.Motion = Vector3.Zero;
        Godot.Collections.Array<Godot.Collections.Dictionary> touching = space.IntersectShape(parameters, 8);
        var names = new List<string>();
        bool terrainTouched = false;
        foreach (Godot.Collections.Dictionary touch in touching)
        {
            var collider = (CollisionObject3D)touch["collider"];
            int obj = collider.HasMeta("object") ? (int)collider.GetMeta("object") : -2;
            terrainTouched |= obj == -1;
            names.Add($"{collider.Name} (object {obj})");
        }
        // The capsule's bottom starts 0.7 m over the lip and must reach the floor, 1.5 m under it.
        bool reached = travel >= 0.7 + TrenchDepth - 0.6 / 2 - 0.05 && touching.Count > 0 && !terrainTouched;
        return Check("Jolt terrain over holes", throughTerrain == inside && misses == 0 && worst <= 1e-3 && reached,
            $"{throughTerrain} of {inside} downward Jolt rays inside the cavity find no terrain; of {outsidePoints.Count} rays "
            + $"on the cells beside the holes (inside the quads whose corners went NaN) {misses} miss and the worst differs "
            + $"from TerrainHeight by {worst * 1000:0.0000} mm (limit 1 mm); a 0.5 m capsule swept 2.6 m down the trench "
            + $"travels {travel:0.000} m to the floor and rests on {(touching.Count == 0 ? "nothing" : string.Join(", ", names))}, "
            + $"terrain touched: {terrainTouched}");
    }
}
