using System;
using System.Runtime.CompilerServices;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("Fpv450.Tests.Fiber")]

namespace Fpv450.Fiber
{
    /// <summary>
    /// The fiber's look (p7d-tether 1.14, DRN-05, design 13): the trailing line drawn through the pose's active nodes (the
    /// span, from the touchdown to the exit) and the laid trail (FiberLine.Trail up to the pose's published count), at the
    /// line's diameter (its wet film in rain) with the thin-wire coverage rule (<c>Shaders/ThinWire</c> with
    /// <c>_THIN_WIRE_FEED</c>, the material <c>Resources/FiberLine</c>): it weighs D/d of a pixel per radian of the render, fades
    /// with distance and is never widened to show; and the coil's wound pack drawn at the line left on it (CoilPack). The
    /// line is world geometry, so it draws in the FPV view and the chase view alike (D-055) and the feed reads it as it reads
    /// the scene; through the feed the rule takes the feed's sample spacing (SetFeedRead), so no field line drops it.
    /// The scene's hooks: Begin with each flight's line, Draw with the pose the drone is drawn at, SetFeedRead as the feed runs
    /// or stops.
    /// - **The span** is one mesh, rebuilt every Draw: a 4-sided tube (FiberTube) with a ring on each node, the straight
    ///   segments between them the chain's own; the rings past the count collapse onto the last.
    /// - **The trail** is drawn in chunks of <see cref="ChunkPoints"/> points, each its own mesh (each chunk's last ring the
    ///   next chunk's first point), rebuilt only where the trail changed. The physics appends a point as it freezes and takes
    ///   the last back as it lifts (an overflow shifts them all), so a Draw compares the trail from its end with the points
    ///   it drew and rebuilds from the first chunk that holds a changed one: one comparison a frame while the trail rests.
    /// Each mesh's vertices are relative to its first point, so float positions keep the line within 1 mm of the nodes. The
    /// line casts no shadow (a 0.25 mm line's is under a shadow texel). 0 B a Draw: every mesh and buffer is made at Begin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FiberView : MonoBehaviour
    {
        /// <summary>Trail points per chunk mesh.</summary>
        public const int ChunkPoints = 512;

        /// <summary>The line's material under a Resources folder (Settings/Fiber/Resources), so a player build carries it and its
        /// shader variant.</summary>
        public const string MaterialResource = "FiberLine";

        const int SpanRings = FiberNodes.Capacity, ChunkRings = ChunkPoints + 1;
        const float BoundsMargin = 1f; // m: the shader's widening of the tube, (r + R px) / cos(π/4), under it to ~100 m
        const MeshUpdateFlags Quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

        static readonly int FeedReadId = Shader.PropertyToID("_ThinWireFeedRead");

        /// <summary>The feed's read for the thin-wire rule: FiberCoverage.FeedRead(k1, the render's coverage) while the feed
        /// reads the FPV camera's render, Vector4.zero when it does not (the feed off).</summary>
        public static void SetFeedRead(Vector4 read) => Shader.SetGlobalVector(FeedReadId, read);

        Material _material;
        Tube _span;
        Tube[] _chunks = new Tube[0];
        int[] _spanIndices, _chunkIndices;
        Vector3[] _positions, _normals;
        Vector2[] _zero, _wire;
        readonly Double3[] _nodes = new Double3[FiberNodes.Capacity];
        Double3[] _drawn = new Double3[0];
        int _drawnCount;
        float _radius;
        FiberLine _line;
        CoilPack _coil;
        double _coilLine, _coilMark, _coilShown;

        /// <summary>The coil's pack on this flight's model, or null (no coil part, no model, or none of its meshes readable).</summary>
        public CoilPack Coil => _coil;

        /// <summary>Trail chunks rebuilt since Begin.</summary>
        public int ChunkBuilds { get; private set; }

        /// <summary>
        /// A new flight: its line (FlightCore.Tether; null draws nothing), its table (for the coil's rebuild mark,
        /// span.coil_rebuild_m: the pack is drawn at the line the physics' coil mass carries), and the drawn drone and its
        /// coil (CoilShape.D01; null or a default shape leaves the pack as the model draws it).
        /// </summary>
        public void Begin(FiberLine line, TetherTable table = null, GameObject model = null, CoilShape coil = default)
        {
            End();
            if (line == null) return;
            _line = line;
            Prepare((float)(line.Diameter / 2), line.Trail.Length);
            if (model == null || !(coil.FiberLength > 0) || double.IsInfinity(line.CoilLineAtStart)) return;
            _coil = new CoilPack(model, coil);
            if (_coil.Unreadable > 0)
                Debug.LogWarning($"[Fiber] {model.name}: {_coil.Unreadable} mesh(es) are not readable, their coil stays full " +
                                 "(its coil_pack meshes are the importer's readable copies: pwsh tools/build_asset.ps1 D01)");
            if (_coil.Vertices == 0)
            {
                _coil.Dispose();
                _coil = null;
                return;
            }
            _coilLine = line.CoilLineAtStart;
            _coilMark = table != null ? table.Number("span.coil_rebuild_m") : 0;
            _coilShown = double.NaN;
        }

        /// <summary>This frame's line, from the pose the drone is drawn at.</summary>
        public void Draw(in PoseFrame pose)
        {
            if (_line == null) return;
            int n = Math.Min(pose.FiberNodeCount, FiberNodes.Capacity);
            for (int i = 0; i < n; i++) _nodes[i] = pose.FiberNodes[i];
            DrawSpan(_nodes, n);
            DrawTrail(_line.Trail, pose.FiberTrailCount);
            if (_coil == null) return;
            double paid = _coilMark > 0 ? Math.Floor(pose.FiberPaidOut / _coilMark) * _coilMark : pose.FiberPaidOut;
            double left = _coilLine - paid;
            if (left == _coilShown) return;
            _coil.Show(left);
            _coilShown = left;
        }

        /// <summary>Draws nothing until the next Begin, and gives the model its own meshes back.</summary>
        public void End()
        {
            _line = null;
            _drawnCount = 0;
            if (_span != null) _span.Renderer.enabled = false;
            foreach (Tube t in _chunks) t.Renderer.enabled = false;
            _coil?.Dispose();
            _coil = null;
        }

        void OnDestroy()
        {
            End();
            if (_span != null) Release(_span.Mesh);
            foreach (Tube t in _chunks) Release(t.Mesh);
        }

        // ---- the parts, for Begin and the tests ----

        /// <summary>The meshes and buffers for a line of this radius and a trail of this capacity; the trail drawn empty.</summary>
        internal void Prepare(float radius, int trailCapacity)
        {
            if (_material == null)
            {
                _material = Resources.Load<Material>(MaterialResource);
                if (_material == null) Debug.LogError("[Fiber] no Resources/" + MaterialResource + " material: the line draws with none");
            }
            if (_span == null)
            {
                _spanIndices = FiberTube.Indices(SpanRings);
                _chunkIndices = FiberTube.Indices(ChunkRings);
                int vertices = ChunkRings * FiberTube.Sides;
                _positions = new Vector3[vertices];
                _normals = new Vector3[vertices];
                _zero = new Vector2[vertices];
                _wire = new Vector2[vertices];
                _span = new Tube("FiberSpan", transform, _material, SpanRings, _spanIndices, _positions, _zero);
            }
            int chunks = Chunks(trailCapacity);
            if (_chunks.Length < chunks)
            {
                int had = _chunks.Length;
                Array.Resize(ref _chunks, chunks);
                for (int k = had; k < chunks; k++)
                    _chunks[k] = new Tube("FiberTrail" + k, transform, _material, ChunkRings, _chunkIndices, _positions, _zero);
            }
            if (_drawn.Length < trailCapacity) _drawn = new Double3[trailCapacity];
            _radius = radius;
            for (int i = 0; i < _wire.Length; i++) _wire[i] = new Vector2(radius, 0f);
            _drawnCount = 0;
            ChunkBuilds = 0;
            _span.Renderer.enabled = false;
            foreach (Tube t in _chunks) t.Renderer.enabled = false;
        }

        /// <summary>The span through count nodes (none under two).</summary>
        internal void DrawSpan(Double3[] nodes, int count)
        {
            if (count < 2)
            {
                _span.Renderer.enabled = false;
                return;
            }
            Vector3 at = V(nodes[0]);
            Bounds bounds = FiberTube.Build(nodes, 0, count, SpanRings, D(at), _radius, _positions, _normals);
            _span.Set(at, _positions, _normals, _wire, _radius, bounds);
        }

        /// <summary>The trail's first count points, rebuilt from the first chunk whose points changed since the last call.</summary>
        internal void DrawTrail(Double3[] trail, int count)
        {
            int n = Math.Max(0, Math.Min(count, Math.Min(trail.Length, _drawn.Length)));
            int same = Math.Min(n, _drawnCount);
            while (same > 0 && !Same(trail[same - 1], _drawn[same - 1])) same--;
            if (same == n && n == _drawnCount) return;
            for (int i = same; i < n; i++) _drawn[i] = trail[i];
            _drawnCount = n;
            int chunks = Chunks(n);
            for (int k = same >= 1 ? (same - 1) / ChunkPoints : 0; k < chunks; k++)
            {
                int first = k * ChunkPoints;
                Vector3 at = V(_drawn[first]);
                Bounds bounds = FiberTube.Build(_drawn, first, Math.Min(ChunkRings, n - first), ChunkRings, D(at), _radius, _positions, _normals);
                _chunks[k].Set(at, _positions, _normals, _wire, _radius, bounds);
                ChunkBuilds++;
            }
            for (int k = chunks; k < _chunks.Length; k++) _chunks[k].Renderer.enabled = false;
        }

        internal MeshRenderer SpanRenderer => _span.Renderer;
        internal MeshRenderer ChunkRenderer(int k) => _chunks[k].Renderer;
        internal int ChunkCount => _chunks.Length;

        // The chunks a trail of n points draws in: chunk k holds points kC to kC + C, so one with a segment, kC ≤ n − 2.
        static int Chunks(int n) => n >= 2 ? (n - 2) / ChunkPoints + 1 : 0;

        static bool Same(in Double3 a, in Double3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        static Double3 D(Vector3 v) => new Double3(v.x, v.y, v.z);

        static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // One tube's mesh and renderer, its vertex count and topology fixed at creation (unused rings collapse).
        sealed class Tube
        {
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            readonly Transform _transform;
            readonly int _vertices;
            float _radius = float.NaN;

            public Tube(string name, Transform parent, Material material, int rings, int[] indices, Vector3[] zero3, Vector2[] zero2)
            {
                var go = new GameObject(name);
                _transform = go.transform;
                _transform.SetParent(parent, false);
                _vertices = rings * FiberTube.Sides;
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
                Mesh.SetVertices(zero3, 0, _vertices);
                Mesh.SetNormals(zero3, 0, _vertices);
                Mesh.SetUVs(0, zero2, 0, _vertices);
                Mesh.SetUVs(1, zero2, 0, _vertices);
                Mesh.SetIndices(indices, 0, indices.Length, MeshTopology.Triangles, 0, false);
                go.AddComponent<MeshFilter>().sharedMesh = Mesh;
                Renderer = go.AddComponent<MeshRenderer>();
                Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.enabled = false;
            }

            public void Set(Vector3 at, Vector3[] positions, Vector3[] normals, Vector2[] wire, float radius, Bounds bounds)
            {
                _transform.SetPositionAndRotation(at, Quaternion.identity); // world m (the view's object is unscaled)
                Mesh.SetVertices(positions, 0, _vertices, Quiet);
                Mesh.SetNormals(normals, 0, _vertices, Quiet);
                if (radius != _radius)
                {
                    Mesh.SetUVs(1, wire, 0, _vertices, Quiet);
                    _radius = radius;
                }
                bounds.Expand(2 * BoundsMargin);
                Mesh.bounds = bounds;
                Renderer.enabled = true;
            }
        }
    }
}
