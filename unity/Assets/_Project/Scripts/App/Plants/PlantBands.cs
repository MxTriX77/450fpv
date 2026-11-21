using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Wind;

namespace Fpv450.App
{
    /// <summary>
    /// The plant elements the farmland draws, as 1.2's law takes them (w16-plants-in-the-air 1.3; plants-in-the-air.md §2, design.md
    /// §1): the drawn side buckets each class's sizes into bands (design.md §7.1: f₁ ∝ L^−1.35 for culms) and the shaders
    /// interpolate between a class's bands by a plant's drawn length. The ids are PlantBend.hlsl's; each band's numbers are 1.2's
    /// factories' (PlantMechanics), on the flight's own cover sets (the props' stems' numbers, at the flight's moisture).
    /// </summary>
    public static class PlantBands
    {
        public const int Count = 18;

        /// <summary>The grass culms: green 0.5, 0.8 and 1.3 m, dry and burnt 0.6, 1.0 and 1.7 m (measured: the drawn culms are
        /// green 0.46–1.34 m and dry and burnt 0.53–1.77 m, each clump's mesh top at its instance's scale, assets-src/
        /// vendor_plant_numbers.json and FarmLandBuilder.Grass's scales). Bands 0–8; the wash grids integrate these.</summary>
        public const int Grass = 9;

        public static readonly double[] GrassLength = { 0.5, 0.8, 1.3, 0.6, 1.0, 1.7, 0.6, 1.0, 1.7 };

        /// <summary>The culms the wash grids integrate (PlantWash's bands, in this order): each class's middle one. The others take
        /// the wash's steady air at their load point (PlantAir.Sample's path for a band no grid holds, design.md §4.5): a grid band
        /// costs 1.2 ms a 32² tick (design.md §7.1), and nine of them at 25 Hz took the helpers past a core.</summary>
        public static readonly int[] Washed = { 1, 4, 7 };

        /// <summary>A band's index in the wash grids, −1 for a band they do not integrate.</summary>
        public static int WashIndex(int band) => Array.IndexOf(Washed, band);

        static readonly PlantKind[] GrassKind = { PlantKind.GreenCulm, PlantKind.DryCulm, PlantKind.BurntCulm };
        static readonly CoverKind[] GrassCover = { CoverKind.GrassBlade, CoverKind.DryCulm, CoverKind.BurntCulm };

        /// <summary>A crown's leafy twig and a bush's (1.2's 5 mm × 0.5 m twig, 20 g/m of leaves, assumed there), a crown's
        /// branch (1.2's 40 mm × 3.5 m, 1.2 kg/m of leaves), and the whole plants' sway.</summary>
        public const int TreeTwig = 9, BushTwig = 10, Branch = 11;

        public const int Island = 12, PineSmall = 13, PineMid = 14, PineLarge = 15, BushLeafy = 16, BushBare = 17;

        /// <summary>Each band's base over the ground, m: a crown's twigs at 6 m and its branches at 4 m (assumed: the drawn
        /// crowns span about 2–13 m, the island tree's from about 4 m; the wind's profile changes by under 10 % between 4 and
        /// 8 m), a bush's twigs at 1 m (assumed: the drawn bushes are 1.6–5 m tall); the ground's stems and the whole plants at
        /// the ground.</summary>
        public static double Elevation(int band) => band == TreeTwig ? 6 : band == Branch ? 4 : band == BushTwig ? 1 : 0;

        /// <summary>The bands' mechanics from the flight's cover sets (ContactModel.Cover) and its air density (tether.json's).</summary>
        public static PlantMechanics[] Mechanics(Func<CoverKind, CoverSet> cover, double airDensity)
        {
            var m = new PlantMechanics[Count];
            for (int i = 0; i < Grass; i++)
                m[i] = PlantMechanics.Stem(GrassKind[i / 3], GrassLength[i], cover(GrassCover[i / 3]), airDensity);
            CoverSet twig = cover(CoverKind.Twig);
            double leaf = cover(CoverKind.Leaf).MassPerArea;
            m[TreeTwig] = PlantMechanics.Woody(PlantKind.LeafyTwig, 0.5, 0.005, 0.02, twig, leaf, airDensity);
            m[BushTwig] = m[TreeTwig];
            m[Branch] = PlantMechanics.Woody(PlantKind.Branch, 3.5, 0.04, 1.2, twig, leaf, airDensity);
            // The island tree (measured from the drawn tree: 12.9 m tall, its crown 12.8 m wide from about 4 m up, its trunk
            // 1.1 m at breast height, plants-in-the-air.md §2); a broadleaf's f₀ is 1.2's assumed 0.45 Hz.
            m[Island] = PlantMechanics.Tree(PlantKind.BroadleafTree, 12.9, 1.1, 12.8, 8.9, twig, airDensity);
            // The pines by size (the heights and crown widths measured from the drawn bounds; each diameter derived from the
            // note's f₀ by the restated Moore and Maguire regression; the crown 0.6 of the height deep, assumed): Pine006 and
            // Pine002_S2 (5.1–7.9 m), Pine002_M and PineDead_02 (11.5–12 m), Pine002_L and Pine004 (15.6–20 m).
            m[PineSmall] = PlantMechanics.Tree(PlantKind.Pine, 6.5, 0.11, 3.3, 3.9, twig, airDensity);
            m[PineMid] = PlantMechanics.Tree(PlantKind.Pine, 11.7, 0.27, 5.5, 7.0, twig, airDensity);
            m[PineLarge] = PlantMechanics.Tree(PlantKind.Pine, 17.5, 0.55, 7.5, 10.5, twig, airDensity);
            // A bush sways as its stems do: a leafy bush's 2.5 m stems of 15 mm (measured: GreenBush 2.0 m, LeafyBush_P2 3.2 m
            // with twigs 9–18 mm at the base), their leaves 1.2 kg/m × (d / 40 mm)² (derived: 1.2's two assumed loads, 20 g/m on
            // 5 mm and 1.2 kg/m on 40 mm, both lie on d², the pipe model's); a bare bush's 3 m stems of 20 mm without leaves
            // (measured: DeadBush 3.0–5.0 m with dead twigs 10–44 mm, SmallBushDry 1.6–1.8 m).
            m[BushLeafy] = PlantMechanics.Woody(PlantKind.Branch, 2.5, 0.015, 1.2 * (0.015 / 0.04) * (0.015 / 0.04), twig, leaf, airDensity);
            m[BushBare] = PlantMechanics.Woody(PlantKind.Branch, 3.0, 0.02, 0, twig, leaf, airDensity);
            return m;
        }

        /// <summary>A tree or bush prototype's sway band by its prefab's name (FarmLandBuilder's), −1 for none.</summary>
        public static int Sway(string prefab)
        {
            if (prefab.StartsWith("IslandTree")) return Island;
            if (prefab.StartsWith("Pine006") || prefab.StartsWith("Pine002_S")) return PineSmall;
            if (prefab.StartsWith("Pine002_M") || prefab.StartsWith("PineDead")) return PineMid;
            if (prefab.StartsWith("Pine002_L") || prefab.StartsWith("Pine004")) return PineLarge;
            if (prefab.StartsWith("DeadBush") || prefab.StartsWith("SmallBushDry") || prefab.StartsWith("TwigBush") || prefab == "RedBush_Dry")
                return BushBare;
            if (prefab.Contains("Bush") || prefab.StartsWith("Juniper")) return BushLeafy;
            return -1;
        }

        /// <summary>Whether a sway band is a tree's (its foliage rides branches) or a bush's (its stems).</summary>
        public static bool IsTree(int sway) => sway >= Island && sway <= PineLarge;
    }
}
