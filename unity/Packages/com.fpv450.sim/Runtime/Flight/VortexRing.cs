using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Vortex ring state, the settling into the own wash (p7b-airframe design 4, PHY-09), per rotor, inside the inflow seam
    /// (Propulsion.RotorInflow). Every velocity is scaled by the rotor's ideal induced velocity at its present thrust,
    /// v_h = √(T/(2ρA)) (momentum theory, published): η = −V_a/v_h the descent along the rotor's axis, μ = |V_∥|/v_h the
    /// in-plane airspeed. So a heavy drone on low throttle, whose v_h is small, enters it at a descent its hover would not
    /// (the pilot: "how much throttle power ... as opposed to drones weight").
    /// - The ring forms when η reaches EntryRate with μ below EscapeInPlane, and stays until η falls below ExitRate, passes
    ///   EndRate (the windmill brake state) or μ reaches EscapeInPlane: the hysteresis, entry and exit logged as events.
    /// - Inside, the ring's recirculation adds to the inflow through the disc (Johnson's measured mean inflow in VRS rising
    ///   above the held descent branch the props' table has, Propulsion.DescentAdvanceRatioLimit), lagged by Johnson's
    ///   inflow time constant: the thrust at a rotor speed falls as the descent deepens, so the descent runs away.
    /// - Inside, each rotor's thrust wanders by a share x, a first-order random process at the ring's low-frequency band
    ///   with σ rising with the depth, drawn from the named stream "vrs.thrust" (counter-based on the step and the corner),
    ///   bounded to ±FluctuationBound: four rings apart, so the moment is asymmetric and wanders. Never on the pose: it is
    ///   each rotor's thrust.
    /// Sources: Johnson, W., "Model for Vortex Ring State Influence on Rotorcraft Flight Dynamics", NASA/TP-2005-213477
    /// (2005): its Table 4's points, its inflow time constant and its review of the measured frequencies (Table 2). Shetty,
    /// O. R., Selig, M. S., "Small-Scale Propellers Operating in the Vortex Ring State", AIAA 2011-1254: 9–11 in props at
    /// fixed rpm, the mean thrust falling in descent to a minimum and the thrust fluctuating by up to about ±30 % there.
    /// </summary>
    public sealed class VortexRingSpec
    {
        const double TwoPi = 6.283185307179586;

        /// <summary>η where the ring forms: 0.45 (published: Johnson's VzN, the stability boundary the helicopter and
        /// tiltrotor flight tests define, where the mean inflow's slope turns and the descent runs away).</summary>
        public double EntryRate = 0.45;

        /// <summary>η below which the ring is gone: 0.2 (published: Johnson's VzD, where his VRS inflow rejoins momentum
        /// theory). Its use as the exit, so the ring once formed holds to a lower rate than it forms at, is assumed.</summary>
        public double ExitRate = 0.2;

        /// <summary>η of the deepest ring, its largest mean inflow and fluctuation: 1.5 (published: Johnson's VzX).</summary>
        public double PeakRate = 1.5;

        /// <summary>η beyond which the rotor is in the windmill brake state and the ring is gone: 2.0 (published: Johnson's VzE).</summary>
        public double EndRate = 2.0;

        /// <summary>The mean inflow through the disc, (V_z + v)/v_h, at EntryRate and at PeakRate: 0.85 and 1.25 (published:
        /// Johnson's (Vz+v)N and (Vz+v)X, from the measured local minimum and maximum of the mean inflow in vertical descent).</summary>
        public double EntryInflow = 0.85, PeakInflow = 1.25;

        /// <summary>μ at which the forward speed sweeps the ring away: 0.95 (published: Johnson's VxM); the ring's inflow and
        /// fluctuation fade by √(1 − (μ/μ_M)⁶) toward it (published: Johnson's Table 3).</summary>
        public double EscapeInPlane = 0.95;

        /// <summary>The ring's inflow lag over R/v_h: 0.7·2π = 4.40 (published: Johnson's τ = 0.7/λ_h rotor revolutions,
        /// fitted to the D6075's flight test and scaled as R/v_h): about 50 ms at hover, 0.1 s on low throttle.</summary>
        public double InflowLag = 0.7 * TwoPi;

        /// <summary>The thrust fluctuation's σ at PeakRate, a share of the thrust: 0.10 (published: Shetty and Selig's 10-inch
        /// APC props, about ±30 % as the measured limits and ±3σ at the minimum thrust, low-passed at 10 Hz). It rises
        /// linearly from ExitRate and falls to EndRate (assumed between the points).</summary>
        public double FluctuationSigma = 0.10;

        /// <summary>The fluctuation's bound, a share of the thrust: ±0.30 (published: Shetty and Selig's measured limits).</summary>
        public double FluctuationBound = 0.30;

        /// <summary>The fluctuation's corner frequency, f·R/v_h: 0.2 (published: the low band of Johnson's Table 2, Brinson's
        /// 0.14–0.3 and Betzina's 0.2, the measured content Shetty and Selig's 10 Hz filter keeps on a small prop; the band's
        /// choice is assumed). About 8 Hz on low throttle, 17 Hz at hover.</summary>
        public double FluctuationFrequency = 0.2;

        /// <summary>The air's density, kg/m³ (drone.json's aero air_density_kg_m3, 1.225, published ISA).</summary>
        public double AirDensity = 1.225;

        /// <summary>The known-wrong model's switch, m/s: above 0, the rates are scaled by this fixed velocity (the drone's
        /// weight's v_h, so the onset is the descent speed alone) rather than by the rotor's present thrust. 0 in a flight.</summary>
        public double FixedInducedVelocity;

        public VortexRingSpec Clone() => (VortexRingSpec)MemberwiseClone();

        /// <summary>The mean inflow through the disc in the ring, over v_h, at η: momentum theory at ExitRate, then linear
        /// through EntryRate's and PeakRate's measured values (Johnson's points; linear between them, assumed).</summary>
        public double RingInflow(double eta)
        {
            double atExit = -0.5 * ExitRate + DetMath.Sqrt(0.25 * ExitRate * ExitRate + 1);
            if (eta <= EntryRate) return atExit + (EntryInflow - atExit) * (eta - ExitRate) / (EntryRate - ExitRate);
            return EntryInflow + (PeakInflow - EntryInflow) * (eta - EntryRate) / (PeakRate - EntryRate);
        }

        /// <summary>
        /// What the ring adds to the inflow through the disc, over v_h, at η and μ, over the held descent branch's own inflow
        /// held (over v_h): RingInflow above it to PeakRate, then down to 0 at EndRate (as Johnson's VRS increment returns to
        /// his baseline there), faded by the forward speed. 0 outside the ring's range.
        /// </summary>
        public double AddedInflow(double eta, double mu, double held)
        {
            if (!(eta > ExitRate && eta < EndRate)) return 0;
            double added = eta <= PeakRate
                ? RingInflow(eta) - held
                : (RingInflow(PeakRate) - held) * (EndRate - eta) / (EndRate - PeakRate);
            return added > 0 ? added * Fade(mu) : 0;
        }

        /// <summary>The fluctuation's σ at η and μ, a share of the thrust.</summary>
        public double Sigma(double eta, double mu)
        {
            if (!(eta > ExitRate && eta < EndRate)) return 0;
            double shape = eta <= PeakRate ? (eta - ExitRate) / (PeakRate - ExitRate) : (EndRate - eta) / (EndRate - PeakRate);
            return FluctuationSigma * shape * Fade(mu);
        }

        /// <summary>Johnson's forward-speed fade, √(1 − (μ/μ_M)⁶), 0 at and beyond EscapeInPlane.</summary>
        public double Fade(double mu)
        {
            double m = mu / EscapeInPlane;
            if (!(m < 1)) return 0;
            double m2 = m * m;
            return DetMath.Sqrt(1 - m2 * m2 * m2);
        }

        /// <summary>Whether a rotor at η and μ is in the ring, given whether it was (the hysteresis).</summary>
        public bool InRing(bool was, double eta, double mu) =>
            was ? !(eta < ExitRate || eta > EndRate || mu >= EscapeInPlane) : eta >= EntryRate && eta <= EndRate && mu < EscapeInPlane;

        /// <summary>A nearly normal draw of unit variance for a corner at a step, from the stream's counter (pure in the step,
        /// the corner and the stream's key): the sum of four uniform draws less 2, times √3 (Irwin–Hall), bounded to ±2√3.</summary>
        public static double Normal(in SeedStream stream, long step, int corner)
        {
            ulong at = (ulong)step * 16 + (ulong)corner * 4;
            double sum = -2;
            for (ulong j = 0; j < 4; j++) sum += (stream.BitsAt(at + j) >> 11) * (1.0 / 9007199254740992.0);
            return sum * 1.7320508075688772;
        }
    }
}
