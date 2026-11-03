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
    /// - The motor's own dynamics (D-064): each motor's winding resistance R ("motor.resistance"), its no-load current I0,
    ///   the bearings' and the iron's friction ("motor.friction"), and its rotor's inertia J, the bell and the prop
    ///   ("motor.inertia"), each × (1 + δ), δ nearly normal, clamped. With Kv they set each motor's time constant
    ///   τ_m = J/(Kt·Ke/R + ∂Q/∂ω), its speed at a duty and its idle, so the four answer one command a little apart.
    /// - The blades' pitch (p7f-damage design 3, D-059): each blade's sections sit Δθ off the sheet's, Δθ uniform in
    ///   ±BladePitchBound ("prop.blade_pitch"), flown by the blades' elements (DamagedRotor): their mean a per-rotor thrust and
    ///   torque offset, their difference a 1/rev hub moment and in-plane force, the baseline shake beside the unbalance's.
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

        /// <summary>The ESC command lag's spread, ±s: 0.08 ms (derived): the winding's L/R of 0.3 ms at ±10 % (assumed, maxon's
        /// tolerance), ±30 µs, a motor's own; and where its ESC's 20 kHz loop and 24 kHz PWM period fall against the frame,
        /// up to 50 + 42 µs, ±46 µs about their mean (AM32, published, p7b 1.0). That phase drifts update by update, below
        /// the 250 µs sub-step, so the lag carries it as one offset per flight within its bound (assumed).</summary>
        public double EscLagBound = 0.08e-3;

        /// <summary>The winding resistance's bound, a fraction: ±10 % (published: an outer-rotor BLDC's datasheet tolerance on
        /// the line-to-line resistance at 25 °C, E-JPC's VH series, as on its torque and back-EMF constants).</summary>
        public double ResistanceBound = 0.10;

        /// <summary>Its spread, σ: a third of the bound (assumed: the tolerance as 3σ).</summary>
        public double ResistanceSigma = 0.10 / 3;

        /// <summary>The no-load current's bound, a fraction: ±10 % (published: maxon's test specification, ±10 % on the
        /// no-load current and speed; its catalogue's I0 a typical value).</summary>
        public double FrictionBound = 0.10;

        /// <summary>Its spread, σ: a third of the bound (assumed).</summary>
        public double FrictionSigma = 0.10 / 3;

        /// <summary>The rotor inertia's bound, a fraction: ±3 % (assumed: the moulded prop's and the bell's mass; no lot
        /// spread is published), and its σ, 1 % (assumed).</summary>
        public double InertiaBound = 0.03, InertiaSigma = 0.01;

        /// <summary>ISO 21940-11's balance grade G, m/s: G 6.3 (published: Tyto Robotics, UAV props are typically balanced
        /// to G 6.3).</summary>
        public double BalanceGrade = 6.3e-3;

        /// <summary>The prop's mass, kg (drone.json's prop part, 16 g).</summary>
        public double PropMass = 0.016;

        /// <summary>The service speed the grade is held at, rpm: 9000 (assumed: drone.json's props reference rpm, the loaded
        /// drone's climb).</summary>
        public double ServiceRpm = 9000;

        /// <summary>The blades' factory pitch offset's bound, ±rad: 0.25° (assumed: no blade-to-blade pitch tolerance is
        /// published for moulded props; at the bound the blades' difference makes a 1/rev hub moment of the order of the mass
        /// unbalance's moment about the centre of mass, p7f-damage design 3). 0 draws none (a drone.json without props.blade,
        /// whose blades have no elements to fly them: DroneSpec.BuildAsymmetry).</summary>
        public double BladePitchBound = 0.25 * 3.141592653589793 / 180;

        /// <summary>The props' blades, each drawn its own pitch offset (drone.json's props.blades).</summary>
        public int Blades = 2;

        /// <summary>The permissible residual unbalance U_per = m·G/Ω, kg·m.</summary>
        public double PermissibleUnbalance => PropMass * BalanceGrade / (ServiceRpm * (TwoPi / 60));

        public MotorAsymmetry Clone() => (MotorAsymmetry)MemberwiseClone();

        /// <summary>
        /// One flight's per-motor draws from random (at the flight's start), each recorded for the header: "motor.kv"
        /// (kv_scale), "esc.lag" (lag_s), "prop.unbalance" (kg_m, then angle_rad), then "motor.resistance",
        /// "motor.friction" and "motor.inertia" (each a scale), corner by corner; then, with a pitch bound, "prop.blade_pitch"
        /// (rad), corner by corner and blade by blade.
        /// </summary>
        public Draws DrawFor(FlightRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var d = new Draws
            {
                KvScale = new double[4], EscLag = new double[4], Unbalance = new double[4], UnbalanceAngle = new double[4],
                ResistanceScale = new double[4], FrictionScale = new double[4], InertiaScale = new double[4],
            };
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
            SeedStream r = random.Open("motor.resistance");
            for (int i = 0; i < 4; i++) d.ResistanceScale[i] = 1 + random.DrawNormal(ref r, "motor.resistance." + Corner(i), ResistanceSigma, ResistanceBound);
            SeedStream f = random.Open("motor.friction");
            for (int i = 0; i < 4; i++) d.FrictionScale[i] = 1 + random.DrawNormal(ref f, "motor.friction." + Corner(i), FrictionSigma, FrictionBound);
            SeedStream j = random.Open("motor.inertia");
            for (int i = 0; i < 4; i++) d.InertiaScale[i] = 1 + random.DrawNormal(ref j, "motor.inertia." + Corner(i), InertiaSigma, InertiaBound);
            if (!(BladePitchBound > 0)) return d;
            d.BladePitch = new double[4 * Blades];
            SeedStream pitch = random.Open("prop.blade_pitch");
            for (int i = 0; i < 4; i++)
                for (int b = 0; b < Blades; b++)
                    d.BladePitch[i * Blades + b] = random.Draw(ref pitch, "prop.blade_pitch_rad." + Corner(i) + "." + b, -BladePitchBound, BladePitchBound);
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

            /// <summary>The winding resistance, the no-load current and the rotor inertia over drone.json's (D-064).</summary>
            public double[] ResistanceScale, FrictionScale, InertiaScale;

            /// <summary>Each blade's factory pitch offset, rad, rotor-major (corner × Blades + blade); null without a pitch bound.</summary>
            public double[] BladePitch;
        }
    }
}
