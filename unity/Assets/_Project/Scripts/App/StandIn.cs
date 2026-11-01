using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.App
{
    /// <summary>
    /// The flight's stand-ins (design 6), drawn from the physics' own geometry so what is seen is what the sim flies:
    /// the drone as drone.json's parts at the flight's loadout (legs off: the legs-off build's parts; the payload at its
    /// mass), labelled a stand-in until p5-drone's prefab replaces it, and the launch stand as its runtime object's
    /// boxes. Plain URP/Lit primitives in flat colours, without colliders (PhysX stays out of the flight).
    /// </summary>
    public static class StandIn
    {
        public const string DroneName = "Svinoriz 10 Opto (stand-in)";

        static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "carbon", new Color(0.09f, 0.09f, 0.10f) },
            { "aluminium", new Color(0.62f, 0.64f, 0.66f) },
            { "steel", new Color(0.42f, 0.43f, 0.44f) },
            { "polymer", new Color(0.16f, 0.16f, 0.17f) },
            { "li_ion", new Color(0.22f, 0.30f, 0.40f) },
            { "electronics", new Color(0.18f, 0.28f, 0.20f) },
        };

        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        /// <summary>The drone at the loadout, its root at the body origin (the arm-plane centre), body axes as Unity's.</summary>
        public static GameObject Drone(DroneSpec spec, in Loadout loadout)
        {
            var root = new GameObject(DroneName);
            MassModel mass = spec.BuildMass(loadout);
            for (int i = 0; i < mass.PartCount; i++)
            {
                ref readonly DronePart p = ref mass.Part(i);
                if (!(p.Mass > 0)) continue; // a part the loadout leaves off
                GameObject go;
                if (p.Kind == PartKind.Box)
                {
                    go = Primitive(PrimitiveType.Cube, p.Name, root.transform, p.Material);
                    go.transform.localPosition = V(p.Position);
                    go.transform.localScale = V(p.Size);
                }
                else
                {
                    // Unity's cylinder is 1 across and 2 long, along its y.
                    go = Primitive(PrimitiveType.Cylinder, p.Name, root.transform, p.Material);
                    go.transform.localPosition = V(p.Position);
                    go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, V(p.Axis));
                    go.transform.localScale = new Vector3((float)p.Diameter, (float)p.Length / 2f, (float)p.Diameter);
                }
            }
            return root;
        }

        /// <summary>A runtime object's boxes (the launch stand), in world space.</summary>
        public static GameObject Boxes(RuntimeObject o, string material)
        {
            var root = new GameObject(o.Name);
            foreach (ShapeGeometry s in o.Shapes)
            {
                if (s.Kind != ShapeKind.Box) continue;
                GameObject go = Primitive(PrimitiveType.Cube, o.Name, root.transform, material);
                go.transform.SetPositionAndRotation(V(s.Center), Quaternion.LookRotation(V(s.AxisZ), V(s.AxisY)));
                go.transform.localScale = 2f * V(s.HalfExtents);
            }
            return root;
        }

        /// <summary>
        /// How the drone's own renderers draw: in the FPV view only into the shadows (the camera sits inside the frame,
        /// and what of the drone it sees from its mount is p5-drone's to decide), in the chase view as well as into them.
        /// </summary>
        public static void Visible(GameObject drone, bool seen)
        {
            foreach (MeshRenderer r in drone.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = seen ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, string material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialOf(material);
            return go;
        }

        static Material MaterialOf(string name)
        {
            Color colour = Colours.TryGetValue(name ?? "", out Color c) ? c : new Color(0.3f, 0.3f, 0.3f);
            if (Materials.TryGetValue(colour, out Material m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "StandIn " + name, hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", name == "aluminium" || name == "steel" ? 0.55f : 0.3f);
            m.SetFloat("_Metallic", name == "aluminium" || name == "steel" ? 0.8f : 0f);
            Materials[colour] = m;
            return m;
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
