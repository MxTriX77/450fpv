using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The motors' asymmetry (p7b-airframe design 1, PHY-07): the component tolerances each flight draws its four motors,
    /// ESCs and props from, once at the flight's start, named, bounded and logged (FlightRandom), never noise on the pose.
    /// A FlightSetup without one (null) flies four identical rotors, as Calm's wind grading does (PHY-14).
    /// - Kv: each motor's Kv is drone.json's × (1 + δ), δ nearly normal with σ KvSigma, clamped to ±KvBound
    ///   ("motor.kv"). Kt = Ke = 1/Kv in SI moves with it, so a hot motor spins faster and makes less torque per amp.
    /// - ESC timing: each ESC's command lag is drone.json's τ_esc + Δ, Δ uniform in ±EscLagBound ("esc.lag").
    /// - Prop balance: each prop keeps a residual unbalance U uniform in [0, U_per] at a uniform angle ("prop.unbalance"),
    ///   U_per = m·G/Ω by ISO 21940-11's grade G at the service speed Ω, so each rotor pulls its hub with U·ω² turning
    ///   with it (Propulsion).
    /// </summary>
    public sealed class MotorAsymmetry
    {
        const double TwoPi = 6.283185307179586;

        /// <summary>The Kv's bound, a fraction: ±10 % (published: maxon's motor data hold their constants to about ±10 %;
        /// hobbyists' testers find 3–7 % off the label, a model's own offset included).</summary>
        public double KvBound = 0.10;

        /// <summary>The Kv's spread within one lot, σ, a fraction: 2 % (assumed, tunable; no lot spread is published, and a
        /// lot's spread is a share of the label deviations).</summary>
        public double KvSigma = 0.02;

        /// <summary>The ESC command lag's spread, ±s: 0.08 ms (derived: where each ESC's 20 kHz loop and 24 kHz PWM period
        /// fall against the frame, up to 50 + 42 µs, about ±46 µs about their mean (AM32, published, p7b 1.0), plus the
        /// winding's L/R of 0.3 ms at ±10 % (assumed, maxon's tolerance), ±30 µs).</summary>
        public double EscLagBound = 0.08e-3;

        /// <summary>ISO 21940-11's balance grade G, m/s: G 6.3 (published: Tyto Robotics, UAV props are typically balanced
        /// to G 6.3).</summary>
        public double BalanceGrade = 6.3e-3;

        /// <summary>The prop's mass, kg (drone.json's prop part, 16 g).</summary>
        public double PropMass = 0.016;

        /// <summary>The service speed the grade is held at, rpm: 9000 (assumed: drone.json's props reference rpm, the loaded
        /// drone's climb).</summary>
        public double ServiceRpm = 9000;

        /// <summary>The permissible residual unbalance U_per = m·G/Ω, kg·m.</summary>
        public double PermissibleUnbalance => PropMass * BalanceGrade / (ServiceRpm * (TwoPi / 60));

        public MotorAsymmetry Clone() => (MotorAsymmetry)MemberwiseClone();

        /// <summary>
        /// One flight's per-motor draws from random (at the flight's start), each recorded for the header: "motor.kv"
        /// (kv_scale), "esc.lag" (lag_s), "prop.unbalance" (kg_m, then angle_rad), corner by corner.
        /// </summary>
        public Draws DrawFor(FlightRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var d = new Draws { KvScale = new double[4], EscLag = new double[4], Unbalance = new double[4], UnbalanceAngle = new double[4] };
            SeedStream kv = random.Open("motor.kv");
            for (int i = 0; i < 4; i++) d.KvScale[i] = 1 + random.DrawNormal(ref kv, "motor.kv." + Corner(i), KvSigma, KvBound);
            SeedStream lag = random.Open("esc.lag");
            for (int i = 0; i < 4; i++) d.EscLag[i] = random.Draw(ref lag, "esc.lag_s." + Corner(i), -EscLagBound, EscLagBound);
            SeedStream prop = random.Open("prop.unbalance");
            double limit = PermissibleUnbalance;
            for (int i = 0; i < 4; i++)
            {
                d.Unbalance[i] = random.Draw(ref prop, "prop.unbalance_kg_m." + Corner(i), 0, limit);
                d.UnbalanceAngle[i] = random.Draw(ref prop, "prop.unbalance_angle_rad." + Corner(i), 0, TwoPi);
            }
            return d;
        }

        static string Corner(int i) => i == 0 ? "rear_right" : i == 1 ? "front_right" : i == 2 ? "rear_left" : "front_left";

        /// <summary>One flight's draws, per corner in Corners order.</summary>
        public sealed class Draws
        {
            /// <summary>Kv over drone.json's.</summary>
            public double[] KvScale;

            /// <summary>The ESC's command lag over drone.json's τ_esc, s.</summary>
            public double[] EscLag;

            /// <summary>The prop's residual unbalance, kg·m, and its angle from the rotor's zero, rad.</summary>
            public double[] Unbalance, UnbalanceAngle;
        }
    }
}
