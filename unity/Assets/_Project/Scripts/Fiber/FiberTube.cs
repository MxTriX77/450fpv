using System;
using Fpv450.Sim;
using UnityEngine;

namespace Fpv450.Fiber
{
    /// <summary>
    /// The fiber's tube for <c>Shaders/ThinWire</c> (its header: every vertex on its ring the line's radius from the centre
    /// line, its normal radial, its radius in uv2): a ring of <see cref="Sides"/> vertices on each point, square to the
    /// line's direction there (the bisector of its two segments), turned along the line by parallel transport so the tube
    /// never twists between two rings; the straight segments between the points are the physics' own line. Four sides is
    /// the shader's narrowest tube (THIN_WIRE_NARROWEST). Allocation-free: the caller's arrays.
    /// </summary>
    public static class FiberTube
    {
        public const int Sides = 4;

        /// <summary>The triangles of a tube of rings rings, wound outwards (Unity's front faces).</summary>
        public static int[] Indices(int rings)
        {
            var indices = new int[Math.Max(0, rings - 1) * Sides * 6];
            int n = 0;
            for (int s = 0; s < rings - 1; s++)
                for (int k = 0; k < Sides; k++)
                {
                    int a = s * Sides + k, a1 = s * Sides + (k + 1) % Sides, b = a + Sides, b1 = a1 + Sides;
                    indices[n++] = a;
                    indices[n++] = b1;
                    indices[n++] = b;
                    indices[n++] = a;
                    indices[n++] = a1;
                    indices[n++] = b1;
                }
            return indices;
        }

        /// <summary>
        /// Rings on count points (2 or more) from points[first], relative to origin, into the first rings × Sides vertices;
        /// the rings past count repeat the last point's (their segments have no area). Returns the centres' bounds relative
        /// to origin.
        /// </summary>
        public static Bounds Build(Double3[] points, int first, int count, int rings, Double3 origin, float radius,
            Vector3[] positions, Vector3[] normals)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            Vector3 tangent = Vector3.forward, u = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Double3 p = points[first + i];
                Vector3 c = V(p - origin);
                tangent = Tangent(points, first, count, i, tangent);
                if (i == 0) u = Perpendicular(tangent);
                else
                {
                    u -= tangent * Vector3.Dot(u, tangent); // parallel transport: the last ring's turned onto this one
                    if (u.sqrMagnitude < 1e-12f) u = Perpendicular(tangent);
                }
                u.Normalize();
                Vector3 v = Vector3.Cross(tangent, u);
                int at = i * Sides;
                normals[at] = u;
                normals[at + 1] = v;
                normals[at + 2] = -u;
                normals[at + 3] = -v;
                for (int k = 0; k < Sides; k++) positions[at + k] = c + normals[at + k] * radius;
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            int last = (count - 1) * Sides;
            for (int i = count; i < rings; i++)
                for (int k = 0; k < Sides; k++)
                {
                    positions[i * Sides + k] = positions[last + k];
                    normals[i * Sides + k] = normals[last + k];
                }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        // The line's direction at point i: its two segments' bisector (one at the ends), the last one where both vanish.
        static Vector3 Tangent(Double3[] points, int first, int count, int i, Vector3 previous)
        {
            Double3 p = points[first + i];
            Double3 back = i > 0 ? p - points[first + i - 1] : default;
            Double3 ahead = i < count - 1 ? points[first + i + 1] - p : default;
            double lb = back.Length(), la = ahead.Length();
            Double3 t;
            if (lb > 1e-9 && la > 1e-9)
            {
                t = back * (1 / lb) + ahead * (1 / la);
                if (t.Length() < 1e-6) t = ahead * (1 / la); // a turn back on itself
            }
            else if (la > 1e-9) t = ahead;
            else if (lb > 1e-9) t = back;
            else return previous;
            return V(t).normalized;
        }

        static Vector3 Perpendicular(Vector3 t)
        {
            Vector3 u = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right);
            return u.normalized;
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
