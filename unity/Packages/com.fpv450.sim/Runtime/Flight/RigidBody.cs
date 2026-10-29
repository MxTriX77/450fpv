namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The rigid body (PHB-02, K§6.2): one semi-implicit Euler step of dt, in the frames of DetMath.cs. The state
    /// carries the body origin (the arm-plane centre), not the centre of mass, so a mass change moves the CoM inside
    /// the body and leaves the body's motion as it was: mass that leaves (the line paying out) leaves at its own
    /// velocity. Newton's and Euler's laws act at the CoM:
    /// - translation: the CoM's velocity, then its position with the new velocity
    /// - rotation: the angular momentum about the CoM h = I·ω (body) is kicked by the moment, then drifts: the attitude
    ///   turns by the Cayley rotation of θ = dt·I⁻¹·m (Quat.Integrate's q·(θ/2, 1), normalised), and h is carried into
    ///   the new body axes, h' = q'⁻¹·(q·h), so the momentum in the world is unchanged to rounding and |h| exactly.
    ///   m is the step's midpoint momentum (h + h')/2: for the Cayley rotation that is m = h − ½dt·(I⁻¹·m) × m, solved
    ///   by MidpointPasses fixed-point passes from m = h. Then h' = h − dt·(I⁻¹·m) × m, the implicit midpoint rule on
    ///   Euler's equation I·ω̇ = M − ω × I·ω (the gyroscopic coupling), which keeps both quadratic invariants, |h| and
    ///   the energy ½·h·I⁻¹·h (p6-flight-core 1.7, QA's review-base finding 2). Turning at the start's rate (m = h, no
    ///   pass) gained energy at first order: +10 % in 60 s at 4 rad/s on L1 legs off.
    /// The quaternion is renormalised every step (Quat.Integrate).
    /// </summary>
    public static class RigidBody
    {
        /// <summary>
        /// The fixed-point passes for the midpoint momentum: each gains about dt·|ω| (derived), so three leave the energy
        /// at rounding on the drone's bodies: 1e-11 of it over 60 s at 4 rad/s, 6e-8 at 35 rad/s (2000 °/s). A fixed
        /// count, so the step's cost and bits never depend on a tolerance.
        /// </summary>
        public const int MidpointPasses = 3;

        /// <summary>
        /// One step. accel is the CoM's acceleration in the world (gravity plus the net force over the mass), m/s²;
        /// moment is the net moment about the CoM in body axes, N·m.
        /// </summary>
        public static void Step(ref FlightState s, in MassProperties body, Double3 accel, Double3 moment, double dt)
        {
            Quat q0 = s.Attitude;
            Double3 c = body.CenterOfMass;

            // The CoM's position and velocity in the world: p + q·c and v + q·(ω × c).
            Double3 com = s.Position + q0.Rotate(c);
            Double3 comVelocity = s.Velocity + q0.Rotate(Double3.Cross(s.BodyRate, c));

            // The kick: linear and angular momentum.
            comVelocity = comVelocity + accel * dt;
            Double3 h = body.Inertia * s.BodyRate + moment * dt;

            // The step's midpoint momentum m = h − ½dt·(I⁻¹·m) × m.
            Double3 m = h;
            double half = 0.5 * dt;
            for (int i = 0; i < MidpointPasses; i++)
                m = h - Double3.Cross(body.InverseInertia * m, m) * half;

            // The drift: the attitude at the midpoint rate, the CoM at the new velocity.
            Quat q1 = Quat.Integrate(q0, body.InverseInertia * m, dt);
            com = com + comVelocity * dt;

            // The momentum, unchanged in the world, read in the new body axes.
            Double3 rate = body.InverseInertia * q1.InverseRotate(q0.Rotate(h));

            s.Attitude = q1;
            s.BodyRate = rate;
            s.Position = com - q1.Rotate(c);
            s.Velocity = comVelocity - q1.Rotate(Double3.Cross(rate, c));
        }
    }
}
