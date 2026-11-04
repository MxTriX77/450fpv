using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The flight's legs-on D01 with each leg's parts its own child (p7g-legs 1.5, drone.py's LEG_KINDS), so the player can
    /// turn, hide or drop each one from PoseFrame: per LOD and corner, in the sim's corner order, the stub, the leg, the
    /// T-joint (none at LOD3) and the skid half, each a mesh of its own name in its LOD, at the model's origin (asset space,
    /// the sim's body frame); the stub and the leg meet at the sim's clamp edge (LegDamageSpec.Clamp) and the skid's halves
    /// midway between the sim's bores, each holding its own bore. Needs a prior pwsh tools/build_asset.ps1 D01.
    /// </summary>
    public sealed class DroneLegPartsTests
    {
        static readonly string[] Corners = { "rr", "fr", "rl", "fl" };
        static readonly string[] Kinds = { "stub", "leg", "tjoint", "skid" };

        [Test]
        public void EachLegsPartsAreTheirOwnChildrenWhereTheSimCutsThem()
        {
            DroneModels models = DroneModels.Load();
            Assert.That(models != null && models.legsOn != null, "ProjectSetup.Apply's D01 prefabs");
            GameObject model = models.legsOn;
            LegDamageSpec legs = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile))).BuildLegDamage();
            LOD[] lods = model.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(4));
            for (int q = 0; q < 4; q++)
                for (int c = 0; c < 4; c++)
                {
                    foreach (string kind in Kinds)
                    {
                        string name = $"{model.name}_LOD{q}_{kind}_{Corners[c]}";
                        Transform part = model.transform.Find(name);
                        if (kind == "tjoint" && q == 3)
                        {
                            Assert.That(part, Is.Null, name + ": no T-joint at LOD3");
                            continue;
                        }
                        Assert.That(part, Is.Not.Null, name);
                        Assert.That(part.localPosition == Vector3.zero && part.localRotation == Quaternion.identity && part.localScale == Vector3.one,
                            name + ": at the model's origin");
                        Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                        Assert.That(mesh.name, Is.EqualTo(name));
                        Assert.That(mesh.isReadable, name + ": the importer's readable copy");
                        Assert.That(lods[q].renderers, Has.Member(part.GetComponent<Renderer>()), name + " drawn in its LOD");
                    }
                    Bounds Part(string kind) => model.transform.Find($"{model.name}_LOD{q}_{kind}_{Corners[c]}").GetComponent<MeshFilter>().sharedMesh.bounds;
                    Vector3 edge = new Vector3((float)legs.Clamp[c].X, (float)legs.Clamp[c].Y, (float)legs.Clamp[c].Z);
                    Bounds stub = Part("stub"), leg = Part("leg"), skid = Part("skid");
                    string at = $"LOD{q} {Corners[c]}";
                    Assert.That(stub.min.y, Is.EqualTo(edge.y).Within(1e-6f), at + ": the stub from the sim's clamp edge up");
                    Assert.That(leg.max.y, Is.EqualTo(edge.y).Within(1e-6f), at + ": the leg up to it");
                    foreach (Bounds tube in new[] { stub, leg })
                    {
                        Assert.That(tube.center.x, Is.EqualTo(edge.x).Within(1e-6f), at + ": on the leg's axis");
                        Assert.That(tube.center.z, Is.EqualTo(edge.z).Within(1e-6f), at + ": on the leg's axis");
                    }
                    Vector3 bore = new Vector3((float)legs.Bore[c].X, (float)legs.Bore[c].Y, (float)legs.Bore[c].Z);
                    float middle = (float)((legs.Bore[c].Z + legs.Bore[legs.Sibling[c]].Z) / 2);
                    Assert.That(bore.z < middle ? skid.max.z : skid.min.z, Is.EqualTo(middle).Within(1e-6f), at + ": the half from midway between the bores");
                    Assert.That(skid.Contains(bore), at + ": the half through its own bore");
                }
            TestContext.WriteLine($"{model.name}: {lods.Sum(l => l.renderers.Length)} renderers over 4 LODs, leg parts " +
                                  string.Join(" / ", Enumerable.Range(0, 4).Select(q => lods[q].renderers.Count(r => Kinds.Any(k => r.name.Contains($"_{k}_"))))));
        }
    }
}
