using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using StartPoint = Fpv450.App.Session.StartPoint;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The farmland's start (w1-farmland-world 2.1, D-092; farmland-world spec, "The start point is free and level"): the
    /// launch stand the explorer places at the start, built as the physics builds it (LaunchStand), stands inside the
    /// village's outline with its four posts and its base on the terrain, and no ruin, debris piece, tree, bush or other
    /// object lies within 2 m of its footprint. Opens the friend's scene, so it needs <c>pwsh tools/vendor/link.ps1</c>.
    /// </summary>
    public sealed class FarmlandStartTests
    {
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";

        /// <summary>The spec's clearance beyond the stand's footprint, m (assumed).</summary>
        const float Clearance = 2f;

        /// <summary>A post's foot or a base bar's end within this of the terrain, m: half the base's 20 mm tube (derived), so
        /// the stand reads as standing on the ground.</summary>
        const float OnGround = 0.01f;

        [Test]
        public void TheVillageStartIsFreeAndLevel()
        {
            StartPoint start = FlightWorlds.FarmlandStart;
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform stand = FarmlandExplore.PlaceStand(scene, start);
                Assert.That(stand, Is.Not.Null, "the stand at the start");
                var foot = new Double3(stand.position.x, stand.position.y, stand.position.z);
                RuntimeObject physics = LaunchStand.Build(spec, foot, start.Yaw, 0);

                // The footprint: the physics' shapes in plan, as a circle about the foot.
                float footprint = 0f;
                foreach (ShapeGeometry s in physics.Shapes)
                    for (int c = 0; c < 8; c++)
                    {
                        Double3 p = Corner(s, c);
                        footprint = Mathf.Max(footprint, Plan(p.X - foot.X, p.Z - foot.Z));
                    }

                (Vector2 centre, float radius) = Village();
                float fromCentre = Plan(foot.X - centre.x, foot.Z - centre.y);
                Assert.That(fromCentre + footprint, Is.LessThanOrEqualTo(radius), "inside the village's outline");

                // On the terrain: each post's foot (shapes 0-3) and each base bar's two ends (4, 5).
                float worst = 0f;
                for (int s = 0; s < physics.Shapes.Length; s++)
                {
                    ShapeGeometry g = physics.Shapes[s];
                    Double3 bottom = g.Center - new Double3(0, g.HalfExtents.Y, 0);
                    Double3[] points = s < 4 ? new[] { bottom } : new[] { bottom + g.AxisX * g.HalfExtents.X, bottom - g.AxisX * g.HalfExtents.X };
                    foreach (Double3 p in points)
                    {
                        Assert.That(FarmlandExplore.Ground(scene, (float)p.X, (float)p.Z, out float ground), "a terrain under shape " + s);
                        worst = Mathf.Max(worst, Mathf.Abs((float)p.Y - ground));
                    }
                }
                Assert.That(worst, Is.LessThanOrEqualTo(OnGround), "the posts and the base on the terrain");

                // Clear: every renderer but the stand's (the ruins, their debris and any placed object) and every terrain
                // tree instance (the trees and bushes), by their bounds in plan.
                string nearest = null;
                float gap = float.MaxValue;
                void Near(string what, Bounds b)
                {
                    float dx = Mathf.Max(0f, b.min.x - (float)foot.X, (float)foot.X - b.max.x), dz = Mathf.Max(0f, b.min.z - (float)foot.Z, (float)foot.Z - b.max.z);
                    float d = Mathf.Sqrt(dx * dx + dz * dz) - footprint;
                    if (d < gap) { gap = d; nearest = what; }
                }
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                        if (!r.transform.IsChildOf(stand)) Near(r.name, r.bounds);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Terrain t in root.GetComponentsInChildren<Terrain>())
                        Trees(t, foot, Near);

                TestContext.WriteLine($"'{start.Name}' at ({foot.X:0.00}, {foot.Y:0.00}, {foot.Z:0.00}), heading {start.Yaw * 180 / Math.PI:0}°: " +
                                      $"{fromCentre:0.0} m from the village's centre (radius {radius:0}), footprint {footprint:0.000} m, " +
                                      $"the base at most {worst * 1000:0.0} mm off the terrain, the nearest object {nearest} {gap:0.00} m beyond the footprint");
                Assert.That(gap, Is.GreaterThanOrEqualTo(Clearance), "nothing within " + Clearance + " m of the footprint: " + nearest);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // A terrain's tree instances within 100 m of the foot (its trees and bushes), each by its prototype's renderers'
        // bounds, scaled and turned as the instance draws.
        static void Trees(Terrain terrain, Double3 foot, Action<string, Bounds> near)
        {
            TerrainData data = terrain.terrainData;
            var local = new Bounds[data.treePrototypes.Length];
            for (int i = 0; i < local.Length; i++)
            {
                GameObject copy = Object.Instantiate(data.treePrototypes[i].prefab, Vector3.zero, Quaternion.identity);
                try
                {
                    Renderer[] renderers = copy.GetComponentsInChildren<Renderer>(true);
                    local[i] = renderers[0].bounds;
                    foreach (Renderer r in renderers) local[i].Encapsulate(r.bounds);
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 at = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
                if (Plan(at.x - foot.X, at.z - foot.Z) > 100f) continue;
                Bounds b = local[tree.prototypeIndex];
                Quaternion turn = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
                var scale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
                var world = new Bounds(at, Vector3.zero);
                for (int c = 0; c < 8; c++)
                    world.Encapsulate(at + turn * Vector3.Scale(new Vector3(c % 2 == 0 ? b.min.x : b.max.x, c / 2 % 2 == 0 ? b.min.y : b.max.y, c / 4 == 0 ? b.min.z : b.max.z), scale));
                near(data.treePrototypes[tree.prototypeIndex].prefab.name, world);
            }
        }

        /// <summary>The village's circle from the friend's generator (FarmLandBuilder, in the editor's Assembly-CSharp), as
        /// W2's export reads it: its centre (x, z) and radius, m.</summary>
        static (Vector2 centre, float radius) Village()
        {
            Type builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FarmLandBuilder")).FirstOrDefault(t => t != null);
            Assert.That(builder, Is.Not.Null, "the friend's FarmLandBuilder");
            const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var centre = (Vector2)builder.GetField("VillageCentre", Flags).GetValue(null);
            float radius = Convert.ToSingle(builder.GetField("VillageRadius", Flags).GetValue(null));
            return (centre, radius);
        }

        static Double3 Corner(in ShapeGeometry s, int c) =>
            s.Center + s.AxisX * (c % 2 == 0 ? -s.HalfExtents.X : s.HalfExtents.X) + s.AxisY * (c / 2 % 2 == 0 ? -s.HalfExtents.Y : s.HalfExtents.Y)
                     + s.AxisZ * (c / 4 == 0 ? -s.HalfExtents.Z : s.HalfExtents.Z);

        static float Plan(double dx, double dz) => (float)Math.Sqrt(dx * dx + dz * dz);
    }
}
