using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The legs-off launch stand (D-022, D-043, K§5.4) as a runtime object: drone.json's stand, a square post on each
    /// motor diagonal at post_radius_m from its centre, post_height_m tall, and an X base of the same tube on the ground,
    /// all of its material. It is unanchored in truth; the stub's runtime objects are static, so G7's stand cannot tip or
    /// drag (review-physics W8, phase 7).
    /// </summary>
    public static class LaunchStand
    {
        public const string Name = "launch_stand";

        /// <summary>
        /// The stand with its centre at foot (x, the ground's height, z), turned by yaw (rad, positive turns north towards east,
        /// as the drone's) so its posts sit under a drone of that heading. material is the world's id of stand.material.
        /// Shapes 0–3 are the posts under the motors in Corners order; 4 and 5 the base's two bars.
        /// </summary>
        public static RuntimeObject Build(DroneSpec spec, Double3 foot, double yaw, ushort material)
        {
            DroneStand st = spec.Stand;
            DetMath.SinCos(yaw / 2, out double sin, out double cos);
            var q = new Quat(0, sin, 0, cos);
            double a = st.PostDiameter / 2, h = st.PostHeight, r = st.PostRadius * 0.7071067811865476, s = 0.7071067811865476;
            var shapes = new ShapeGeometry[6];
            for (int c = 0; c < 4; c++)
            {
                Double3 m = spec.Motors[c].Position;
                var at = new Double3(m.X > 0 ? r : -r, h / 2, m.Z > 0 ? r : -r);
                shapes[c] = Box(foot + q.Rotate(at), new Double3(a, h / 2, a), material, q.Rotate(new Double3(1, 0, 0)), q.Rotate(new Double3(0, 0, 1)));
            }
            var bar = new Double3(st.PostRadius + a, a, a);
            shapes[4] = Box(foot + new Double3(0, a, 0), bar, material, q.Rotate(new Double3(s, 0, s)), q.Rotate(new Double3(-s, 0, s)));
            shapes[5] = Box(foot + new Double3(0, a, 0), bar, material, q.Rotate(new Double3(s, 0, -s)), q.Rotate(new Double3(s, 0, s)));
            return new RuntimeObject(Name, shapes);
        }

        /// <summary>The body origin of a drone resting on the stand at foot, its arm plates' undersides (the arm
        /// shapes' radius below the arm plane) on the post tops, unloaded.</summary>
        public static Double3 RestingOrigin(DroneSpec spec, ContactSpec contacts, Double3 foot)
        {
            double arm = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Body && spec.Parts[s.Part].Name.StartsWith("arm_", System.StringComparison.Ordinal)) arm = s.Radius;
            return foot + new Double3(0, spec.Stand.PostHeight + arm, 0);
        }

        static ShapeGeometry Box(Double3 centre, Double3 half, ushort material, Double3 ax, Double3 az) => new ShapeGeometry
        {
            Kind = ShapeKind.Box, Center = centre, AxisX = ax, AxisY = new Double3(0, 1, 0), AxisZ = az, HalfExtents = half, Material = material,
        };
    }
}
