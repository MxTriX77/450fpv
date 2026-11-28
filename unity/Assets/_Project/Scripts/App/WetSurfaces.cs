using System.Collections.Generic;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Wind;
using Fpv450.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450.App
{
    /// <summary>
    /// The wet look's inputs from the sim (w21-rain-on-camera 1.3, VIS-03): what <see cref="WetLook"/> draws, taken from the
    /// flight's own physics, never tuned per asset.
    /// <list type="bullet">
    /// <item>The surfaces' moisture: the sim's for the rain (SurfaceTable.MoistureOf, D-021).</item>
    /// <item>The rain's extinction: its drop spectrum at the flight's rate (RainField's draw) with RainPreset's numbers
    /// (WetOptics.ExtinctionPerM).</item>
    /// <item>The hard surfaces, where Severe rain's water stands: the renderers of every asset slot whose physics the sim
    /// calls rigid (physics_materials.json's rows without a stiffness, surfaces.json's rigid sets: asphalt and the
    /// concrete slab), by the slot's visual material, from each asset's AssetPhysicsMeta. The farmland's own village
    /// carries no metadata, so none of its faces is hard.</item>
    /// </list>
    /// </summary>
    public static class WetSurfaces
    {
        const string SurfacePrefix = "surface:";

        public static WetState StateOf(RainLevel rain) => (WetState)(int)SurfaceTable.MoistureOf(rain);

        public static RainLevel LevelOf(Rain rain) =>
            rain == Rain.SevereRain ? RainLevel.SevereRain : rain == Rain.Rain ? RainLevel.Rain : RainLevel.Sunny;

        /// <summary>The middle of the rain's class, mm/h: the rate the setup's preview draws before a flight has drawn its own.</summary>
        public static double ClassMiddle(RainLevel rain)
        {
            if (rain == RainLevel.Sunny) return 0;
            RainPreset p = RainPreset.Of(rain);
            return 0.5 * (p.RateLo + p.RateHi);
        }

        /// <summary>The drop spectrum's extinction of the picture at <paramref name="rateMmPerHour"/>, 1/m; 0 when Sunny.</summary>
        public static float ExtinctionPerM(RainLevel rain, double rateMmPerHour)
        {
            if (rain == RainLevel.Sunny) return 0f;
            RainPreset p = RainPreset.Of(rain);
            return (float)WetOptics.ExtinctionPerM(rateMmPerHour, p.DropScale, p.DropExponent, p.FallA, p.FallB, p.FallC);
        }

        /// <summary>Applies the rain to the loaded scenes (Sunny restores them): its moisture, its extinction at the rate, and
        /// the hard renderers by the tables. With no tables (null), no face is hard.</summary>
        public static void Apply(RainLevel rain, double rateMmPerHour, MaterialRow[] materials, SurfaceTable surfaces)
        {
            WetLook.Restore(); // the slots are matched by the originals' names, never a twin's
            if (rain == RainLevel.Sunny)
                return;
            WetLook.Apply(StateOf(rain), ExtinctionPerM(rain, rateMmPerHour), Hard(materials, surfaces));
        }

        /// <summary>The renderers of the loaded scenes' rigid slots.</summary>
        public static List<Renderer> Hard(MaterialRow[] materials, SurfaceTable surfaces)
        {
            var rigid = new HashSet<string>();
            if (materials != null)
                foreach (MaterialRow row in materials)
                    if (row.Rigid) rigid.Add(row.Name);
            if (surfaces != null)
                for (int i = 0; i < surfaces.Surfaces.Length; i++)
                    if (surfaces.Surfaces[i].Dry.Rigid) rigid.Add(SurfacePrefix + surfaces.Surfaces[i].Name);

            var hard = new List<Renderer>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (AssetPhysicsMeta meta in root.GetComponentsInChildren<AssetPhysicsMeta>(true))
                    {
                        if (meta.materials == null) continue;
                        var visuals = new HashSet<string>();
                        foreach (AssetPhysicsMeta.MaterialSlot slot in meta.materials)
                            if (slot != null && slot.physics != null && rigid.Contains(slot.physics)) visuals.Add(slot.visual);
                        if (visuals.Count == 0) continue;
                        foreach (Renderer r in meta.GetComponentsInChildren<Renderer>(true))
                            foreach (Material m in r.sharedMaterials)
                                if (m != null && visuals.Contains(m.name))
                                {
                                    hard.Add(r);
                                    break;
                                }
                    }
            }
            return hard;
        }
    }
}
