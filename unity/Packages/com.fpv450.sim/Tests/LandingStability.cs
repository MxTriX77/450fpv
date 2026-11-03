using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// D-067's landing stability from rigid-body mechanics (p6-flight-core 1.6c), the answer the sim's touchdowns are
    /// checked against: every number derived from the mass build (DroneSpec.BuildMass) and the contact shapes
    /// (DroneSpec.BuildContacts) at a loadout, nothing tuned.
    ///
    /// Legs on, sideways (roll, about body z): the drone pivots on its low skid, a tube of the supports' radius whose axis
    /// is the support spheres' line at x = ±Track. A tube rolls, so the pivot is its axis: the drone tips once its centre of
    /// mass passes over that axis, β = atan(Track / h) with h the centre of mass's height over the axis; about the axis its
    /// inertia is I_P = I_roll + m·ρ², ρ = √(Track² + h²), and past the tip it falls as an inverted pendulum,
    /// I_P·ψ̈ = m·g·ρ·sin ψ. Fore-aft (pitch) the same about the skids' end spheres.
    ///
    /// A touchdown with lateral speed v toward the low side, a roll φ toward it and a descent v_z: if the skid catches (no
    /// slip, plastic, the impact short against the fall), the angular momentum about the skid is kept through the catch,
    /// L = m·ρ·(v·cos α + v_z·sin α), α = φ − β the centre of mass's angle past the vertical over the skid (negative inboard:
    /// the descent rights it); the drone goes over if ½·L²/I_P passes m·g·ρ·(1 − cos α). Level (φ = 0) both skids meet
    /// the ground at once and the descent's moments cancel, so L = m·h·v. The catch needs a friction impulse ratio
    /// J_t/J_n (CatchFriction); a Coulomb contact with μ below it slips, so a smooth skid tips only at a higher speed.
    ///
    /// With thrust T held (through the centre of mass, along body y) on a pinned skid the thrust lifts the high side with the
    /// lever Track while gravity's lever shrinks with the roll: the drone rolls over from φ = β − asin(T·Track/(W·ρ))
    /// (dynamic rollover). Pinning it takes μ ≥ T·sin φ / (W − T·cos φ) of Coulomb friction (quasi-static), which a lightly
    /// loaded skid never has; only a catch that does not scale with the load (a groove, a stem, a clod) holds it.
    ///
    /// Legs off, on the coil: the spool, a cylinder of radius R lying fore-aft, with the centre of mass d above its axis.
    /// It rolls on the ground (the contact point is the instant pivot): M(θ)·θ̈ = m·(g + R·θ̇²)·(d·sin θ + e·cos θ) with
    /// M(θ) = I_roll + m·(R² + 2·R·(d·cos θ − e·sin θ) + d² + e²) and e the centre of mass's offset across the axis, until a pack meets
    /// the ground (PackRest). Its linear rate is λ = √(m·g·d / (I_roll + m·(R + d)²)). A pivot about the axis (an axle,
    /// the contact point held where it first touched) has λ = √(m·g·d / (I_roll + m·d²)), the known-wrong this checks.
    /// </summary>
    public sealed class LandingStability
    {
        public const double G = 9.80665;

        /// <summary>kg; the centre of mass, body m; roll (body z) and pitch (body x) inertia about it, kg·m².</summary>
        public double Mass, RollInertia, PitchInertia;
        public Double3 CenterOfMass;

        // ---- Legs on ----
        /// <summary>The support spheres' half track (|x|), their centres' height (body y), radius and the end spheres' z, m.</summary>
        public double Track, SupportY, SupportRadius, SkidEndZ;

        // ---- Legs off ----
        /// <summary>The coil's radius and axis height (body y), its axis's x, and the pack capsule nearest the ground on
        /// the side the drone rolls to: its centre (body x, y) and radius, m.</summary>
        public double CoilRadius, CoilAxisY, PackX, PackY, PackRadius;

        /// <summary>The legs-on answer at a loadout (its legs on).</summary>
        public static LandingStability Legs(DroneSpec spec, ContactSpec contacts, in MassProperties body)
        {
            var s = Of(body);
            foreach (ContactShape c in contacts.Shapes)
            {
                if (c.Role != ContactRole.Support) continue;
                s.Track = Math.Max(s.Track, Math.Abs(c.A.X));
                s.SupportY = c.A.Y;
                s.SupportRadius = c.Radius;
                s.SkidEndZ = Math.Max(s.SkidEndZ, c.A.Z);
            }
            if (s.Track == 0) throw new ArgumentException("the contacts have no supports: the legs are off", nameof(contacts));
            return s;
        }

        /// <summary>The legs-off answer at a loadout (its legs off): the coil's shape, and the pack on the +x side.</summary>
        public static LandingStability Coil(DroneSpec spec, ContactSpec contacts, in MassProperties body)
        {
            var s = Of(body);
            int coil = spec.Coil.Part, pack = -1;
            foreach (DronePack p in spec.Packs)
                if (spec.Parts[p.Part].Position.X > 0) pack = p.Part;
            foreach (ContactShape c in contacts.Shapes)
            {
                if (c.Role != ContactRole.Body) continue;
                if (c.Part == coil && !(c.A.X == c.B.X && c.A.Y == c.B.Y && c.A.Z == c.B.Z))
                {
                    s.CoilRadius = c.Radius;
                    s.CoilAxisY = c.A.Y;
                }
                if (c.Part == pack && !(c.A.X == c.B.X && c.A.Y == c.B.Y && c.A.Z == c.B.Z))
                {
                    s.PackX = c.A.X;
                    s.PackY = c.A.Y;
                    s.PackRadius = c.Radius;
                }
            }
            if (s.CoilRadius == 0 || s.PackRadius == 0) throw new ArgumentException("the contacts lack the coil's or a pack's capsule", nameof(contacts));
            return s;
        }

        static LandingStability Of(in MassProperties body) => new LandingStability
        {
            Mass = body.Mass, RollInertia = body.Inertia.ZZ, PitchInertia = body.Inertia.XX, CenterOfMass = body.CenterOfMass,
        };

        static double Deg(double rad) => rad * 180 / Math.PI;
        static double Rad(double deg) => deg * Math.PI / 180;

        // ---- Legs on: sideways over a skid ----

        /// <summary>The centre of mass's height over the skid's axis, m; its distance from the axis, m.</summary>
        public double SkidHeight => CenterOfMass.Y - SupportY;
        public double Rho => Math.Sqrt(Track * Track + SkidHeight * SkidHeight);

        /// <summary>The roll at which the centre of mass stands over the low skid's axis, rad.</summary>
        public double Beta => Math.Atan2(Track, SkidHeight);
        public double StaticRollTipDeg => Deg(Beta);

        /// <summary>The nose-down pitch at which the centre of mass stands over the skids' front end spheres, deg.</summary>
        public double StaticPitchTipDeg => Deg(Math.Atan2(SkidEndZ - CenterOfMass.Z, SkidHeight));

        /// <summary>Fore-aft: the centre of mass's distance ahead of the front end spheres' axis, and the pitch inertia
        /// about it, kg·m²; the nose-down pitch's lowest forward speed (m/s) at which the skids' front ends, caught, tip the
        /// drone over their ends; and the kinetic friction above which skids sliding along their length trip it (the
        /// friction at the ground, the centre of mass's height over it, against the front ends' lever), with none caught.</summary>
        double EndLever => SkidEndZ - CenterOfMass.Z;
        double EndRho => Math.Sqrt(EndLever * EndLever + SkidHeight * SkidHeight);
        public double EndPivotInertia => PitchInertia + Mass * EndRho * EndRho;

        public double CaughtEndSpeed(double pitchDeg, double descent)
        {
            double beta = Math.Atan2(EndLever, SkidHeight), alpha = Rad(pitchDeg) - beta;
            double need = Math.Sqrt(2 * EndPivotInertia * Mass * G * EndRho * (1 - Math.Cos(alpha)));
            if (pitchDeg == 0) return need / (Mass * SkidHeight);
            return (need / (Mass * EndRho) - descent * Math.Sin(alpha)) / Math.Cos(alpha);
        }

        public double SlidingTripFriction => EndLever / (SkidHeight + SupportRadius);

        /// <summary>The roll inertia about the skid's axis, kg·m², and the fall's linear rate there, 1/s.</summary>
        public double PivotInertia => RollInertia + Mass * Rho * Rho;
        public double FallRate => Math.Sqrt(Mass * G * Rho / PivotInertia);

        /// <summary>The lowest lateral speed (m/s, toward the low side) at which a caught skid tips the drone over, landing
        /// at roll (deg, toward the motion) with a descent (m/s).</summary>
        public double CaughtSkidSpeed(double rollDeg, double descent)
        {
            double alpha = Rad(rollDeg) - Beta;
            double need = Math.Sqrt(2 * PivotInertia * Mass * G * Rho * (1 - Math.Cos(alpha)));
            if (rollDeg == 0) return need / (Mass * SkidHeight);
            return (need / (Mass * Rho) - descent * Math.Sin(alpha)) / Math.Cos(alpha);
        }

        /// <summary>The friction impulse over the normal impulse a plastic catch of one skid takes at that touchdown.</summary>
        public double CatchFriction(double rollDeg, double descent, double lateral)
        {
            double alpha = Rad(rollDeg) - Beta;
            double l = rollDeg == 0 ? Mass * SkidHeight * lateral : Mass * Rho * (lateral * Math.Cos(alpha) + descent * Math.Sin(alpha));
            double w = l / PivotInertia;
            double jt = Mass * (lateral - w * Rho * Math.Cos(alpha));
            double jn = Mass * (-w * Rho * Math.Sin(alpha) + descent);
            return jn > 0 ? jt / jn : double.PositiveInfinity;
        }

        /// <summary>The time (s) to fall from rest pastTip degrees beyond the tip to a roll of toRoll degrees (the pendulum
        /// about the skid's axis, integrated at 10 µs).</summary>
        public double FallTime(double pastTipDeg, double toRollDeg)
        {
            double psi = Rad(pastTipDeg), end = Rad(toRollDeg) - Beta, w = 0, t = 0, dt = 1e-5, k = Mass * G * Rho / PivotInertia;
            while (psi < end)
            {
                w += k * Math.Sin(psi) * dt;
                psi += w * dt;
                t += dt;
            }
            return t;
        }

        /// <summary>The roll (deg) from which a pinned skid rolls the drone over with thrust T = thrustOverWeight·W held.</summary>
        public double RolloverDeg(double thrustOverWeight)
        {
            double s = thrustOverWeight * Track / Rho;
            return s >= 1 ? 0 : Math.Max(0, Deg(Beta - Math.Asin(s)));
        }

        /// <summary>The Coulomb friction a skid needs to stay pinned at a roll (deg) with thrust T = thrustOverWeight·W
        /// held (quasi-static: the thrust's sideways part over what it leaves on the skid).</summary>
        public static double PinFriction(double rollDeg, double thrustOverWeight)
        {
            double left = 1 - thrustOverWeight * Math.Cos(Rad(rollDeg));
            return left > 0 ? thrustOverWeight * Math.Sin(Rad(rollDeg)) / left : double.PositiveInfinity;
        }

        // ---- Legs off: the coil ----

        /// <summary>The centre of mass's height over the coil's axis, m.</summary>
        public double CoilAbove => CenterOfMass.Y - CoilAxisY;

        /// <summary>The roll (deg) at which the pack meets the ground while the coil rolls (bisection).</summary>
        public double PackRestDeg
        {
            get
            {
                double dx = PackX, dy = PackY - CoilAxisY, lo = 0, hi = Math.PI / 2;
                for (int i = 0; i < 60; i++)
                {
                    double th = (lo + hi) / 2;
                    double height = CoilRadius - dx * Math.Sin(th) + dy * Math.Cos(th) - PackRadius;
                    if (height > 0) lo = th;
                    else hi = th;
                }
                return Deg((lo + hi) / 2);
            }
        }

        /// <summary>The rock's linear rate rolling on the coil (about its contact), and pivoting on its axis, 1/s.</summary>
        public double CoilRollingRate => Math.Sqrt(Mass * G * CoilAbove / (RollInertia + Mass * (CoilRadius + CoilAbove) * (CoilRadius + CoilAbove)));
        public double CoilAxleRate => Math.Sqrt(Mass * G * CoilAbove / (RollInertia + Mass * CoilAbove * CoilAbove));

        /// <summary>The time (s) the coil takes to rock from rest at fromDeg to toDeg: rolling on the ground, or pivoting on
        /// its axis (the known-wrong); offset is the centre of mass's distance across the axis toward the fall, m.</summary>
        public double CoilRockTime(double fromDeg, double toDeg, bool rolling = true, double offset = 0)
        {
            double th = Rad(fromDeg), end = Rad(toDeg), w = 0, t = 0, dt = 1e-5, m = Mass, d = CoilAbove, r = CoilRadius, e = offset;
            while (th < end)
            {
                double arm = d * Math.Sin(th) + e * Math.Cos(th);
                double acc = rolling
                    ? (m * G * arm + m * r * arm * w * w) / (RollInertia + m * (r * r + 2 * r * (d * Math.Cos(th) - e * Math.Sin(th)) + d * d + e * e))
                    : m * G * arm / (RollInertia + m * (d * d + e * e));
                w += acc * dt;
                th += w * dt;
                t += dt;
                if (t > 60) return double.PositiveInfinity;
            }
            return t;
        }
    }
}
