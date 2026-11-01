// The wake grid's synthetic layout (p7c-wind 1.3; D-046: the map waits). No NUnit here: tools/worldbench compiles
// this file too, for its --wakes bench and golden.
// - A 600 m square grid centred on the origin (2 m cells).
// - A belt along X at z = 0: one row of stand-in maples (Z09_tree_maple's wind volume, from its .assetmeta.json on
//   p6-test-range: an upright cylinder of radius 3.1 m from 2.2 to 8.4 m, porosity 0.45, Godot's), every 5 m, about
//   300 m long, with a 12 m gap between crown edges (crowns end at 50.6 m and start again at 62.6 m).
// - One solid house: a 10 × 6 m box 5 m tall (Z01_house_adobe's wind volume size) with porosity 0 (solid, where the
//   stand-in has Godot's 0.11), its long side along X, centred at (−100, 200).
// With wind from the north (0°) the belt's wake lies south of it and the house's runs south towards the belt.

using System.Collections.Generic;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    public static class WakeGridFixture
    {
        public const double MapleRadius = 3.1, MapleBase = 2.2, MapleTop = 8.4, MaplePorosity = 0.45, TreeSpacing = 5;

        /// <summary>The gap's crown edges, world X, m, and its middle.</summary>
        public const double GapWest = 50.6, GapEast = 62.6, GapMiddle = 56.6;

        /// <summary>The belt's crowns span z = −3.1 .. 3.1; with wind from the north its leeward edge is at z = −3.1.</summary>
        public const double BeltLeeZ = -MapleRadius;

        public const double HouseX = -100, HouseZ = 200, HouseHalfX = 5, HouseHalfZ = 3, HouseTop = 5;

        /// <summary>The house's leeward face with wind from the north, world Z, m.</summary>
        public const double HouseLeeZ = HouseZ - HouseHalfZ;

        public static WindVolume[] Volumes()
        {
            var v = new List<WindVolume>();
            // West segment: centres −147.5 .. 47.5; east segment: 65.7 .. 145.7 (crown edges 50.6 and 62.6: a 12 m gap).
            for (double x = -147.5; x <= 47.5 + 1e-9; x += TreeSpacing) v.Add(Maple(x));
            for (double x = GapEast + MapleRadius; x <= 145.7 + 1e-9; x += TreeSpacing) v.Add(Maple(x));
            v.Add(new WindVolume
            {
                Kind = WindVolumeKind.Box, X = HouseX, Z = HouseZ, BaseM = 0, TopM = HouseTop,
                HalfX = HouseHalfX, HalfZ = HouseHalfZ, Yaw = 0, Porosity = 0,
            });
            return v.ToArray();
        }

        static WindVolume Maple(double x) => new WindVolume
        {
            Kind = WindVolumeKind.Cylinder, X = x, Z = 0, BaseM = MapleBase, TopM = MapleTop, Radius = MapleRadius,
            Porosity = MaplePorosity,
        };

        public static WakeObstacles Obstacles() => new WakeObstacles(-300, -300, 300, 300, Volumes());
    }
}
