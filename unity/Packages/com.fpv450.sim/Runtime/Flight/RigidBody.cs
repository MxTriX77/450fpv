namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The rigid body (PHB-02, K§6.2): one semi-implicit Euler step of dt, in the frames of DetMath.cs. The state
    /// carries the body origin (the arm-plane centre), not the centre of mass, so a mass change moves the CoM inside
    /// the body and leaves the body's motion as it was: mass that leaves (the line paying out) leaves at its own
    /// velocity. Newton's and Euler's laws act at the CoM:
    /// - translation: the CoM's velocity, then its position with the new velocity
    /// - rotation: the angular momentum about the CoM h = I·ω (body), kicked by the moment, turns the attitude at the
    ///   new rate ω* = I⁻¹·h, and then is carried into the new body axes: h' = q'⁻¹·(q·h). With no moment the
    ///   momentum in the world is unchanged to rounding. To first order h' = h − dt·ω*×h, which is Euler's equation
    ///   I·ω̇ = M − ω × I·ω: the gyroscopic coupling, with |h| kept exactly.
    /// The quaternion is renormalised every step (Quat.Integrate).
    /// </summary>
    public static class RigidBody
    {
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

            // The drift: the attitude at the new rate, the CoM at the new velocity.
            Quat q1 = Quat.Integrate(q0, body.InverseInertia * h, dt);
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
