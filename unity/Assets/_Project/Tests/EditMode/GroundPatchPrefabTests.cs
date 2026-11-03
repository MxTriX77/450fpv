using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Fpv450.Assets;
using Fpv450.Sim;
using Fpv450.Sim.World;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ray = Fpv450.Sim.World.Ray;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The S04 prefab as the catalog importer builds it (p1-s04-belt-straw, "Micro meshes carry no collision" and
    /// "Micro meshes skipped by the audit"): LOD0 renders the slab and the baked straws, no collider stands for the
    /// straws, the metadata component lists them under <c>micro</c>. The far layer (D-081): each switch cross-fades
    /// over the last quarter of its distance, LOD0's slab draws the near map and LOD1's and LOD2's the far map, and
    /// physics reads none of it. Needs a prior <c>pwsh tools/build_asset.ps1 S04</c>.
    /// </summary>
    public sealed class GroundPatchPrefabTests
    {
        const string PrefabPath = "Assets/_Project/Prefabs/surfaces/S04_patch.prefab";
        const string MicroMesh = "S04_patch_LOD0_straw";
        const string MetaPath = "Assets/_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json";

        static GameObject Prefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{PrefabPath} is missing: run pwsh tools/build_asset.ps1 S04");
            return prefab;
        }

        [Test]
        public void Lod0_RendersTheSlabAndTheMicroMesh_AndTheOtherLodsOnlyTheSlab()
        {
            LOD[] lods = Prefab().GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            string[] lod0 = lods[0].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name).ToArray();
            Assert.That(lod0, Is.EquivalentTo(new[] { "S04_patch_LOD0", MicroMesh }));
            Assert.That(lods[1].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name), Is.EqualTo(new[] { "S04_patch_LOD1" }));
            Assert.That(lods[2].renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.name), Is.EqualTo(new[] { "S04_patch_LOD2" }));
            Renderer straws = lods[0].renderers.Single(r => r.GetComponent<MeshFilter>().sharedMesh.name == MicroMesh);
            Assert.That(straws.sharedMaterial.name, Is.EqualTo("straw"));
            Assert.That(straws.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.GreaterThan(100000), "the baked straws are geometry");
        }

        /// <summary>A 12 m patch is taller than the screen at its 8 m switch; the group's size keeps every switch distance.</summary>
        [Test]
        public void LodGroup_SwitchesAtTheMetadataDistances()
        {
            GameObject prefab = Prefab();
            LODGroup group = prefab.GetComponent<LODGroup>();
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            LOD[] lods = group.GetLODs();
            float[] far = { meta.lods[1].switchDistanceM, meta.lods[2].switchDistanceM, meta.cullDistanceM };
            Assert.That(far, Is.EqualTo(new[] { 8f, 60f, 400f }));
            for (int i = 0; i < lods.Length; i++)
            {
                float distance = group.size / (2f * lods[i].screenRelativeTransitionHeight * Mathf.Tan(30f * Mathf.Deg2Rad));
                Assert.That(distance, Is.EqualTo(far[i]).Within(0.01f), $"LOD{i} ends at {distance} m");
            }
        }

        [Test]
        public void MicroMesh_HasNoCollider_AndTheMetaListsIt()
        {
            GameObject prefab = Prefab();
            Transform straws = prefab.transform.Find(MicroMesh);
            Assert.That(straws, Is.Not.Null);
            Assert.That(straws.GetComponentsInChildren<Collider>(true), Is.Empty, "a micro mesh carries no collision");
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            Assert.That(meta.micro.Length, Is.EqualTo(1));
            Assert.That(meta.micro[0].name, Is.EqualTo(MicroMesh));
            Assert.That(meta.micro[0].kind, Is.EqualTo("straw"));
            Assert.That(meta.micro[0].lod, Is.EqualTo(0));
            Assert.That(meta.micro[0].count, Is.GreaterThan(10000));
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.EqualTo(meta.collision.Length), "one collider per primitive, none for the straws");
            int boxes = meta.collision.Count(c => c.kind == AssetPhysicsMeta.ShapeKind.Box);
            Assert.That(colliders.OfType<BoxCollider>().Count(), Is.EqualTo(boxes));
            Assert.That(colliders.OfType<SphereCollider>().Count(), Is.EqualTo(meta.collision.Length - boxes));
            Assert.That(meta.collision.All(c => c.landable), Is.True);
        }

        /// <summary>
        /// review-physics PR1, PR2 and PR8: collision[0] is the soil box one mat depth down, then the mat as straw boxes
        /// (fill, then each bare patch's edge boxes) from -D to 0, then the mound spheres; every rotated box keeps its
        /// rotation on its collider.
        /// </summary>
        [Test]
        public void Collision_IsTheSoilThenTheStrawMatThenTheMounds()
        {
            GameObject prefab = Prefab();
            AssetPhysicsMeta meta = prefab.GetComponent<AssetPhysicsMeta>();
            AssetPhysicsMeta.CollisionShape soil = meta.collision[0];
            Assert.That(soil.kind, Is.EqualTo(AssetPhysicsMeta.ShapeKind.Box));
            Assert.That(soil.material, Is.EqualTo("surface:belt_straw"));
            Assert.That(soil.center.y + soil.size.y / 2f, Is.EqualTo(-0.125f).Within(1e-5f), "the soil's top one mat depth down");
            int mounds = meta.collision.Count(c => c.kind == AssetPhysicsMeta.ShapeKind.Sphere);
            var mat = meta.collision.Skip(1).Take(meta.collision.Length - 1 - mounds).ToArray();
            Assert.That(mat.Length, Is.GreaterThan(50));
            Assert.That(mat.All(c => c.kind == AssetPhysicsMeta.ShapeKind.Box && c.material == "straw"), Is.True);
            Assert.That(mat.All(c => Mathf.Abs(c.center.y + 0.0625f) < 1e-5f && Mathf.Abs(c.size.y - 0.125f) < 1e-5f), Is.True,
                "the mat boxes stand from -D to 0");
            Assert.That(meta.collision.Skip(1 + mat.Length).All(c => c.material == "surface:crater_spoil"), Is.True);
            for (int i = 0; i < meta.collision.Length; i++)
            {
                Transform host = prefab.transform.Find($"Collision{i}");
                Assert.That(Quaternion.Angle(host.localRotation, meta.collision[i].rotation), Is.LessThan(0.01f), $"Collision{i}");
            }
        }

        /// <summary>
        /// D-081: the group cross-fades by distance, never by time. LOD0 is whole up to 6 m and dissolves into LOD1 by its
        /// 8 m switch, LOD1 into LOD2 from 45 m to 60 m (the metadata's lod_fade_share 0.25), and the cull at 400 m is not
        /// faded; each band is read back from the heights and widths Unity draws with.
        /// </summary>
        [Test]
        public void LodGroup_CrossFadesEachSwitchOverItsLastQuarter()
        {
            LODGroup group = Prefab().GetComponent<LODGroup>();
            Assert.That(group.fadeMode, Is.EqualTo(LODFadeMode.CrossFade));
            Assert.That(group.animateCrossFading, Is.False);
            LOD[] lods = group.GetLODs();
            float Distance(float height) => group.size / (2f * height * Mathf.Tan(30f * Mathf.Deg2Rad));
            float[] starts = { 6f, 45f };
            for (int i = 0; i < starts.Length; i++)
            {
                float near = i == 0 ? 1f : lods[i - 1].screenRelativeTransitionHeight, end = lods[i].screenRelativeTransitionHeight;
                float start = end + lods[i].fadeTransitionWidth * (near - end);
                Assert.That(Distance(start), Is.EqualTo(starts[i]).Within(0.01f), $"LOD{i} starts to dissolve at {Distance(start)} m");
            }
            Assert.That(lods[2].fadeTransitionWidth, Is.EqualTo(0f), "the cull is not faded");
        }

        /// <summary>
        /// D-081: LOD0's slab draws its ground with S04_patch_ground_base over the near map and mask, LOD1's and LOD2's in
        /// the far slot with S04_patch_far_base over the far map and mask; each is belt_straw's material, its shader, normal
        /// map, detail and colour, with only the base map and the packed mask swapped, laid over the patch's 12 m once
        /// (tiling 1/12, offset 0.5); each map imports as sRGB colour and each mask as linear data, clamped at the edge.
        /// </summary>
        [Test]
        public void Slab_DrawsTheNearMapAtLod0_AndTheFarMapBeyond()
        {
            LOD[] lods = Prefab().GetComponent<LODGroup>().GetLODs();
            var recipe = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/belt_straw.mat");
            Renderer slab = lods[0].renderers.Single(r => r.GetComponent<MeshFilter>().sharedMesh.name == "S04_patch_LOD0");
            Assert.That(slab.sharedMaterials.Select(m => m.name), Does.Contain("S04_patch_ground_base").And.Not.Contain("S04_patch_far_base"));
            for (int i = 1; i < lods.Length; i++)
                Assert.That(lods[i].renderers.Single().sharedMaterials.Select(m => m.name),
                    Does.Contain("S04_patch_far_base").And.Not.Contain("S04_patch_ground_base"), $"LOD{i}");
            Material near = slab.sharedMaterials.Single(m => m.name == "S04_patch_ground_base");
            Material far = lods[1].renderers.Single().sharedMaterials.Single(m => m.name == "S04_patch_far_base");
            foreach ((Material m, string map, string mask) in new[] { (near, "S04_patch_near_albedo", "S04_patch_near_mask"),
                         (far, "S04_patch_far_albedo", "S04_patch_far_mask") })
            {
                Assert.That(m.shader, Is.EqualTo(recipe.shader), m.name);
                Assert.That(m.GetTexture("_BaseMap").name, Is.EqualTo(map), m.name);
                Assert.That(m.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(1f / 12f, 1f / 12f)), m.name);
                Assert.That(m.GetTextureOffset("_BaseMap"), Is.EqualTo(new Vector2(0.5f, 0.5f)), m.name);
                foreach (string packed in new[] { "_MetallicGlossMap", "_OcclusionMap" })
                    Assert.That(m.GetTexture(packed).name, Is.EqualTo(mask), $"{m.name} {packed}: the straws' occlusion and smoothness over the ground's");
                var maskImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m.GetTexture("_OcclusionMap")));
                Assert.That(maskImporter.sRGBTexture, Is.False, $"{mask} is linear data");
                Assert.That(maskImporter.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), mask);
                foreach (string kept in new[] { "_BumpMap", "_DetailAlbedoMap", "_DetailNormalMap" })
                    Assert.That(m.GetTexture(kept), Is.EqualTo(recipe.GetTexture(kept)), $"{m.name} {kept}");
                Assert.That(m.GetTextureScale("_DetailAlbedoMap"), Is.EqualTo(recipe.GetTextureScale("_DetailAlbedoMap")), m.name);
                Assert.That(m.GetColor("_BaseColor"), Is.EqualTo(recipe.GetColor("_BaseColor")), m.name);
                Assert.That(m.shaderKeywords, Is.EquivalentTo(recipe.shaderKeywords), m.name);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")));
                Assert.That(importer.sRGBTexture, Is.True, map);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), map);
            }
        }

        /// <summary>
        /// D-081 and D-028: physics reads the elements, never the stand-ins. No collider stands under a far LOD and the
        /// colliders are the metadata's primitives; and the test range's world built again with S04's far layer taken
        /// out of its metadata (the base maps, the far slot, the cross-fade) answers every ground sample, every ray down
        /// and every foot's contacts over each S04 patch exactly as the world built from the committed metadata. Only
        /// the content hash, which covers the metadata's bytes, tells the two apart.
        /// </summary>
        [Test]
        public void Physics_ReadsNoStandIn()
        {
            GameObject prefab = Prefab();
            foreach (LOD lod in prefab.GetComponent<LODGroup>().GetLODs().Skip(1))
                foreach (Renderer r in lod.renderers)
                    Assert.That(r.GetComponentsInChildren<Collider>(true), Is.Empty, $"{r.name} only draws");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(prefab.GetComponent<AssetPhysicsMeta>().collision.Length));

            string committed = File.ReadAllText(MetaPath);
            string stripped = Regex.Replace(committed, "\"textures\": \\[.*?\\n  \\]", "\"textures\": []", RegexOptions.Singleline);
            stripped = Regex.Replace(stripped, ",\\s*\"lod_fade_share\": [-+0-9.eE]+", "");
            stripped = Regex.Replace(stripped, ",\\s*\\{\\s*\"slot\": \"far\",[^}]*\\}", "");
            StringAssert.Contains("\"base_map\"", committed, "the committed metadata carries the far layer");
            foreach (string gone in new[] { "base_map", "lod_fade_share", "\"slot\": \"far\"" })
                StringAssert.DoesNotContain(gone, stripped);

            string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string Read(string relative) => File.ReadAllText(Path.Combine(root, relative));
            string[] materials = MapWorld.MaterialNames(Read(MapWorld.MaterialsFile));
            string placements = Read("maps/test-range/placements.json");
            var with = new MapWorld(placements, model => Read(MapWorld.ModelsRoot + "/" + model), materials);
            var without = new MapWorld(placements, model => model.EndsWith("/S04_patch.assetmeta.json", StringComparison.Ordinal)
                ? stripped : Read(MapWorld.ModelsRoot + "/" + model), materials);
            Assert.That(without.ContentHash, Is.Not.EqualTo(with.ContentHash), "the hash covers the metadata's bytes");

            var points = new List<XZ>();
            foreach (object item in Json.Arr(Json.Obj(Json.Parse(placements), "placements"), "instances", "placements"))
            {
                Dictionary<string, object> row = Json.Obj(item, "instance");
                if (Json.Str(row, "asset", "instance") != "S04")
                    continue;
                Vector3 at = Json.Vec3(row, "position_m", "instance");
                for (double x = -5.9; x < 6.0; x += 0.37)
                    for (double z = -5.9; z < 6.0; z += 0.37)
                        points.Add(new XZ(at.x + x, at.z + z));
            }
            Assert.That(points.Count, Is.GreaterThan(40000), "every S04 patch sampled");
            XZ[] grid = points.ToArray();
            var groundA = new GroundSample[grid.Length];
            var groundB = new GroundSample[grid.Length];
            with.SampleGround(grid, groundA);
            without.SampleGround(grid, groundB);
            Ray[] down = grid.Select(p => new Ray(new Double3(p.X, 2.0, p.Z), new Double3(0.0, -1.0, 0.0))).ToArray();
            var hitsA = new RayHit[down.Length];
            var hitsB = new RayHit[down.Length];
            with.Raycast(down, 5.0, hitsA);
            without.Raycast(down, 5.0, hitsB);
            var contactsA = new StaticContact[32];
            var contactsB = new StaticContact[32];
            int contacts = 0;
            for (int i = 0; i < grid.Length; i++)
            {
                string where = $"at ({grid[i].X:0.00}, {grid[i].Z:0.00})";
                Assert.That((groundB[i].Height, groundB[i].Surface, groundB[i].Flags, groundB[i].Normal.X, groundB[i].Normal.Y, groundB[i].Normal.Z),
                    Is.EqualTo((groundA[i].Height, groundA[i].Surface, groundA[i].Flags, groundA[i].Normal.X, groundA[i].Normal.Y, groundA[i].Normal.Z)), $"ground {where}");
                Assert.That((hitsB[i].Distance, hitsB[i].Object, hitsB[i].Shape, hitsB[i].Material, hitsB[i].Surface),
                    Is.EqualTo((hitsA[i].Distance, hitsA[i].Object, hitsA[i].Shape, hitsA[i].Material, hitsA[i].Surface)), $"ray {where}");
                if (i % 7 != 0)
                    continue;
                var foot = new Capsule(new Double3(grid[i].X, 0.03, grid[i].Z), new Double3(grid[i].X + 0.1, 0.03, grid[i].Z), 0.05);
                int na = with.StaticContacts(foot, foot, 0.01, contactsA), nb = without.StaticContacts(foot, foot, 0.01, contactsB);
                Assert.That(nb, Is.EqualTo(na), $"contacts {where}");
                for (int c = 0; c < na; c++)
                    Assert.That((contactsB[c].Distance, contactsB[c].Object, contactsB[c].Shape, contactsB[c].Material, contactsB[c].Point.X, contactsB[c].Point.Y, contactsB[c].Point.Z),
                        Is.EqualTo((contactsA[c].Distance, contactsA[c].Object, contactsA[c].Shape, contactsA[c].Material, contactsA[c].Point.X, contactsA[c].Point.Y, contactsA[c].Point.Z)),
                        $"contact {c} {where}");
                contacts += na;
            }
            Assert.That(contacts, Is.GreaterThan(1000), "the feet touch the mat");
        }
    }
}
