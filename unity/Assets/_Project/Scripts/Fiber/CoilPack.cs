using System;
using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Fiber
{
    /// <summary>
    /// Where the drone model draws the coil's wound pack (model m, the body frame of DetMath.cs) and how it shrinks: the
    /// generator's own law (tools/blender/generators/drone.py, coil_radii), the pack's volume falling linearly with the line's
    /// length, r_p = √(r_c² + (R² − r_c²)·L / L_full), R the full pack's radius (the spec's coil diameter), r_c the empty
    /// spool's. The pack lies between its rear face and the front hoop along the coil's axis.
    /// </summary>
    public struct CoilShape
    {
        /// <summary>The coil part's centre and axis (the line leaves by its rear end, −Axis), model m.</summary>
        public Vector3 Centre, Axis;

        /// <summary>The wound pack's rear and front faces along the axis from the centre, m.</summary>
        public float PackRear, PackFront;

        /// <summary>The full pack's radius (the model is drawn full), the empty spool's core and the ribs' depth under the
        /// pack's radius, m.</summary>
        public float Radius, CoreRadius, RibDepth;

        /// <summary>The line on a full coil, m (drone.json's coil.fiber_length_m).</summary>
        public double FiberLength;

        // D01's look (p5-drone): assets-src/drone/D01_legs_on.json and D01_legs_off.json, "coil" (assumed there; the core
        // is the volume a 0.25 mm line of 10 km fills at a square lay, π(R² − r_c²)·0.15 m = 6.3e-4 m³ against 6.25e-4 m³),
        // drawn at "payout" 0. Keep them in step with those files.
        public const float D01CoreDiameterM = 0.039f, D01WoundM = 0.15f, D01HoopLengthM = 0.015f, D01RibDepthM = 0.0006f;

        /// <summary>The D01 model's pack, from drone.json's coil part (its centre, axis, diameter and length) and coil block (the
        /// line on a full spool) and the look's coil stations above.</summary>
        public static CoilShape D01(DroneSpec spec)
        {
            DronePart part = spec.Parts[spec.Coil.Part];
            float half = (float)part.Length / 2;
            return new CoilShape
            {
                Centre = V(part.Position),
                Axis = V(part.Axis).normalized,
                PackFront = half - D01HoopLengthM,
                PackRear = half - D01HoopLengthM - D01WoundM,
                Radius = (float)part.Diameter / 2,
                CoreRadius = D01CoreDiameterM / 2,
                RibDepth = D01RibDepthM,
                FiberLength = spec.Coil.FiberLength,
            };
        }

        /// <summary>The pack's radius with lineOnCoil metres of line on it, m.</summary>
        public readonly double PackRadius(double lineOnCoil)
        {
            double left = FiberLength > 0 ? Math.Max(0, Math.Min(1, lineOnCoil / FiberLength)) : 1;
            double r = Radius, c = CoreRadius;
            return Math.Sqrt(c * c + (r * r - c * c) * left);
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }

    /// <summary>
    /// The coil's wound pack on the drone model, drawn at the line left on it (p7d-tether 1.14, DRN-05): every vertex of the
    /// model's meshes on the pack's outer surface (within its ribs' depth under its radius, between its rear face and the
    /// front hoop: the ribbed pack, its rear face's rim and the hoop's inner rim) moves radially by the pack's shrink, so the
    /// faces stay closed and the ribs keep their depth. The meshes that hold the pack are cloned for this model (each must
    /// be readable: Read/Write on its import); Dispose gives the model its own meshes back. 0 B a Show.
    /// </summary>
    public sealed class CoilPack : IDisposable
    {
        const float Tolerance = 1e-4f; // m, on the model's vertex positions

        readonly CoilShape _shape;
        readonly MeshFilter[] _filters;
        readonly Mesh[] _originals, _clones;
        readonly Vector3[][] _vertices, _base, _shift;
        readonly int[][] _index;

        public CoilPack(GameObject model, in CoilShape shape)
        {
            _shape = shape;
            var filters = new List<MeshFilter>();
            var index = new List<int[]>();
            var bases = new List<Vector3[]>();
            var shifts = new List<Vector3[]>();
            var vertices = new List<Vector3[]>();
            Matrix4x4 modelFromWorld = model.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    Unreadable++;
                    continue;
                }
                Matrix4x4 toModel = modelFromWorld * filter.transform.localToWorldMatrix, toMesh = toModel.inverse;
                Vector3[] v = mesh.vertices;
                var picked = new List<int>();
                var shift = new List<Vector3>();
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 d = toModel.MultiplyPoint3x4(v[i]) - shape.Centre;
                    float z = Vector3.Dot(d, shape.Axis);
                    Vector3 radial = d - shape.Axis * z;
                    float r = radial.magnitude;
                    if (z < shape.PackRear - Tolerance || z > shape.PackFront + Tolerance) continue;
                    if (r < shape.Radius - shape.RibDepth - Tolerance || r > shape.Radius + Tolerance) continue;
                    picked.Add(i);
                    shift.Add(toMesh.MultiplyVector(radial / r));
                }
                if (picked.Count == 0) continue;
                filters.Add(filter);
                index.Add(picked.ToArray());
                shifts.Add(shift.ToArray());
                var at = new Vector3[picked.Count];
                for (int k = 0; k < at.Length; k++) at[k] = v[picked[k]];
                bases.Add(at);
                vertices.Add(v);
                Vertices += picked.Count;
            }
            _filters = filters.ToArray();
            _index = index.ToArray();
            _base = bases.ToArray();
            _shift = shifts.ToArray();
            _vertices = vertices.ToArray();
            _originals = new Mesh[_filters.Length];
            _clones = new Mesh[_filters.Length];
            for (int m = 0; m < _filters.Length; m++)
            {
                _originals[m] = _filters[m].sharedMesh;
                _clones[m] = Object.Instantiate(_originals[m]);
                _clones[m].name = _originals[m].name + " (coil)";
                _filters[m].sharedMesh = _clones[m];
            }
            ShownRadius = shape.Radius;
        }

        /// <summary>The pack's vertices found on readable meshes, and the meshes that could not be read.</summary>
        public int Vertices { get; }
        public int Unreadable { get; }

        /// <summary>The pack's radius as drawn, m.</summary>
        public double ShownRadius { get; private set; }

        /// <summary>Draws the pack with lineOnCoil metres of line left on it.</summary>
        public void Show(double lineOnCoil)
        {
            double radius = _shape.PackRadius(lineOnCoil);
            float by = (float)(radius - _shape.Radius);
            for (int m = 0; m < _clones.Length; m++)
            {
                int[] index = _index[m];
                Vector3[] v = _vertices[m], at = _base[m], shift = _shift[m];
                for (int k = 0; k < index.Length; k++) v[index[k]] = at[k] + shift[k] * by;
                _clones[m].SetVertices(v); // the pack only shrinks inside the model's bounds
            }
            ShownRadius = radius;
        }

        public void Dispose()
        {
            for (int m = 0; m < _clones.Length; m++)
            {
                if (_clones[m] == null) continue;
                if (_filters[m] != null && _filters[m].sharedMesh == _clones[m]) _filters[m].sharedMesh = _originals[m];
                if (Application.isPlaying) Object.Destroy(_clones[m]);
                else Object.DestroyImmediate(_clones[m]);
                _clones[m] = null;
            }
        }
    }
}
