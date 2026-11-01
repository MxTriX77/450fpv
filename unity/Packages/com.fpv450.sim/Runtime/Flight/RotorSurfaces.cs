using Fpv450.Sim.World;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Each rotor's surfaces for a step (GroundEffectSpec, p7b-airframe 1.3, PHY-08), in two batched world queries with
    /// caller-owned buffers (no allocation): a ray from each hub down its axis and one up it within the reach (Raycast:
    /// shapes, wires and the ground from above, never over a hole), and the ground under each hub (SampleGround's relief and
    /// surface, which the terrain's triangles the ray sees may not carry). Below: the nearer of the ray's hit and the
    /// sampled ground's plane along the axis, none from the ground over a hole (GroundFlags.Hole). Above: the ray's hit.
    /// </summary>
    public sealed class RotorSurfaces
    {
        readonly Double3[] _hubs = new Double3[4];
        readonly Ray[] _rays = new Ray[8];
        readonly RayHit[] _hits = new RayHit[8];
        readonly XZ[] _hubXZ = new XZ[4];
        readonly GroundSample[] _ground = new GroundSample[4];

        /// <summary>The rotors' hubs (body m, Corners order) and the rays' reach, m.</summary>
        public RotorSurfaces(Propulsion propulsion, double reach)
        {
            for (int i = 0; i < 4; i++) _hubs[i] = propulsion.Hub(i);
            Reach = reach;
        }

        public double Reach { get; }

        /// <summary>Each rotor's distance along its axis to the surface below and above it at the state's pose, m; +∞ for
        /// none within the reach (below, the sampled ground's distance may lie beyond it).</summary>
        public void Find(FlightWorld world, in FlightState s, out Corners below, out Corners above)
        {
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0)), down = -up;
            for (int i = 0; i < 4; i++)
            {
                Double3 hub = s.Position + s.Attitude.Rotate(_hubs[i]);
                _rays[i] = new Ray(hub, down);
                _rays[4 + i] = new Ray(hub, up);
                _hubXZ[i] = new XZ(hub.X, hub.Z);
            }
            world.Raycast(_rays, Reach, _hits);
            world.SampleGround(_hubXZ, _ground);
            below = default;
            above = default;
            for (int i = 0; i < 4; i++)
            {
                double z = _hits[i].Distance;
                ref readonly GroundSample g = ref _ground[i];
                double c = Double3.Dot(g.Normal, up);
                if ((g.Flags & GroundFlags.Hole) == 0 && c > 0)
                {
                    // Along the axis from the hub to the plane through the sampled point (the hub's x and z, the ground's
                    // height) with the ground's normal n: n·(hub − point) / n·up, and n·(hub − point) = n_y·(y − height).
                    double t = g.Normal.Y * (_rays[i].Origin.Y - g.Height) / c;
                    if (t < z) z = t > 0 ? t : 0;
                }
                below[i] = z;
                above[i] = _hits[4 + i].Distance;
            }
        }
    }
}
