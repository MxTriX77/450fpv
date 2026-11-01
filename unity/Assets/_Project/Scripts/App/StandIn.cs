using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The flight's stand-in for the launch stand until p5-drone's (task 1.4), drawn from the physics' own geometry so
    /// what is seen is what the sim flies: its runtime object's boxes, plain URP/Lit primitives in a flat colour, without
    /// colliders (PhysX stays out of the flight).
    /// </summary>
    public static class StandIn
    {
        static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "steel", new Color(0.42f, 0.43f, 0.44f) },
        };

        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

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
