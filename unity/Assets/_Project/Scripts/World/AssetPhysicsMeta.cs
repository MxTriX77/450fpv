using System;
using UnityEngine;

namespace Fpv450.World
{
    /// <summary>
    /// The physics metadata of one asset variant (AST-05): a copy of its <c>.assetmeta.json</c>, schema 1
    /// (setup-asset-pipeline, design decision 6), in Unity's frame (D-019) and metres, relative to the prefab
    /// origin. The catalog importer fills it; the sim reads it through an adapter later. Never edited by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetPhysicsMeta : MonoBehaviour
    {
        public enum ShapeKind { Box, Sphere, Capsule, Cylinder }

        [Serializable]
        public sealed class LodEntry
        {
            public string mesh;
            public int tris;
            public float switchDistanceM;
            /// <summary>Every slot of this LOD's mesh draws with the thin-wire material, its radius in uv2 (a wire's tube, a pole's far LODs).</summary>
            public bool thinWire;
        }

        [Serializable]
        public sealed class MaterialSlot
        {
            public string slot;
            public string visual;
            public string physics;
        }

        /// <summary>
        /// A visual-only micro-detail mesh rendered with its LOD and never collided with (p1-s04-belt-straw,
        /// "Visual-only micro-detail meshes"): the baked straws of a ground patch until the shared micro-detail
        /// generator places them at physics parity (AST-10).
        /// </summary>
        [Serializable]
        public sealed class MicroMesh
        {
            public int lod;
            public string name;
            public string kind;
            public int count;
            public int tris;
        }

        /// <summary>A primitive: box (center, size), sphere (center, radius), capsule and cylinder (center, radius, height along the rotated Y axis).</summary>
        [Serializable]
        public class Shape
        {
            public ShapeKind kind;
            public Vector3 center;
            public Vector3 size;
            public float radius;
            public float height;
            public Quaternion rotation = Quaternion.identity;
        }

        [Serializable]
        public sealed class CollisionShape : Shape
        {
            public string material;
            public bool landable;
        }

        [Serializable]
        public sealed class WindVolume : Shape
        {
            [Range(0f, 1f)] public float porosity;
        }

        /// <summary>A planar polygon of the real clear space, with the narrowest width across it.</summary>
        [Serializable]
        public sealed class Gap
        {
            public string id;
            public Vector3[] polygon;
            public Vector3 normal;
            public float narrowestM;
        }

        /// <summary>
        /// A named attachment point another generator or the map attaches to (p1-l01-l03-pole-wires): a pole's
        /// insulator for its wire, at the neck groove on the outward side, with the outward unit direction and the
        /// largest conductor diameter its seat holds.
        /// </summary>
        [Serializable]
        public sealed class Port
        {
            public string id;
            public Vector3 position;
            public Vector3 direction;
            public float maxDiameterM;
        }

        /// <summary>
        /// A conductor that is a map object, not a collision shape (p1-l01-l03-pole-wires, D-007): the material slot
        /// its tube is drawn in, its diameter, mass per metre and sag below the chord at mid-span, its axial stiffness
        /// EA and rated breaking force (PHY-22's inputs), material <c>cable</c>, its damage state and its sampled centre
        /// line. No collider stands for it.
        /// </summary>
        [Serializable]
        public sealed class Wire
        {
            public string slot;
            public float diameterM;
            public float massKgPerM;
            public float sagM;
            public float axialStiffnessN;
            public float breakingForceN;
            public string material;
            public string damage;
            public Vector3[] points;
        }

        [Serializable]
        public sealed class SnagFeature
        {
            public string kind;
            public Vector3[] points;
            public float radiusM;
        }

        public int schema;
        public string id;
        public string variant;
        public string generator;
        public string paramsSha256;
        public string units;
        public string frame;
        public Bounds bounds;
        public LodEntry[] lods;
        public MicroMesh[] micro;
        public float cullDistanceM;
        public MaterialSlot[] materials;
        public CollisionShape[] collision;
        public Gap[] gaps;
        public WindVolume[] windVolumes;
        public Port[] ports;
        public Wire[] wires;
        public SnagFeature[] snag;

        /// <summary>The <c>.assetmeta.json</c> this was filled from, for provenance and for readers that want the file.</summary>
        public TextAsset sourceJson;
    }
}
