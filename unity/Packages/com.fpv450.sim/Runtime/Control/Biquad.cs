namespace Fpv450.Sim.Control
{
    /// <summary>
    /// Betaflight 4.5.0's biquad (src/main/common/filter.c 133–245, read for the formulas, never copied): a second-order
    /// section's coefficients, normalised by a0, and its delay line together, as biquadFilter_t holds them.
    /// - Update (biquadFilterUpdate 157–202): ω = 2π·f·T, α = sin ω / 2Q; the low-pass is the Butterworth section
    ///   (Q = 1/√2, biquadFilterInitLPF 143–146), b = ((1 − cos ω)/2, 1 − cos ω, (1 − cos ω)/2), a = (1 + α, −2 cos ω,
    ///   1 − α); the notch b = (1, −2 cos ω, 1) over the same a.
    /// - ApplyDF1 (210–224): direct form 1, which takes coefficient changes (the dynamic filters); ApplyDF1Weighted
    ///   (227–234) cross-fades it with its input by the weight (the RPM notches); ApplyDF2 (237–245): the transposed
    ///   direct form 2, for the static ones.
    /// - NotchQ (filterGetNotchQ 137–140): a notch's Q from its centre and its lower cutoff, f0·f1 / (f0² − f1²).
    /// Departures: double precision, and DetMath's sine and cosine where Betaflight uses float and its sin_approx and
    /// cos_approx polynomials (common/maths.c).
    /// </summary>
    public struct Biquad
    {
        const double TwoPi = 6.283185307179586;

        public double B0, B1, B2, A1, A2, Weight;

        /// <summary>The delay line: the last two inputs and outputs (DF1); DF2 keeps its two states in X1 and X2.</summary>
        public double X1, X2, Y1, Y2;

        /// <summary>Betaflight's BIQUAD_Q, 1/√2: the Butterworth section (filter.c 31).</summary>
        public static readonly double ButterworthQ = 1 / DetMath.Sqrt(2);

        /// <summary>A low-pass at hz, refreshed every refreshUs µs, with an empty delay line (biquadFilterInitLPF).</summary>
        public static Biquad Lowpass(double hz, double refreshUs)
        {
            var f = new Biquad();
            f.Update(hz, refreshUs, ButterworthQ, false, 1);
            return f;
        }

        /// <summary>A notch at hz with its Q and weight, refreshed every refreshUs µs, with an empty delay line (biquadFilterInit).</summary>
        public static Biquad Notch(double hz, double refreshUs, double q, double weight = 1)
        {
            var f = new Biquad();
            f.Update(hz, refreshUs, q, true, weight);
            return f;
        }

        /// <summary>filterGetNotchQ: the Q of a notch at centre with its lower cutoff.</summary>
        public static double NotchQ(double centre, double cutoff) => centre * cutoff / (centre * centre - cutoff * cutoff);

        /// <summary>biquadFilterUpdate: new coefficients and weight; the delay line stays.</summary>
        public void Update(double hz, double refreshUs, double q, bool notch, double weight)
        {
            double omega = TwoPi * hz * refreshUs * 0.000001;
            DetMath.SinCos(omega, out double sn, out double cs);
            double alpha = sn / (2 * q);
            double b0, b1, b2, a1;
            if (notch)
            {
                b0 = 1;
                b1 = -2 * cs;
                b2 = 1;
                a1 = b1;
            }
            else
            {
                b1 = 1 - cs;
                b0 = b1 * 0.5;
                b2 = b0;
                a1 = -2 * cs;
            }
            double a2 = 1 - alpha, a0 = 1 + alpha;
            B0 = b0 / a0;
            B1 = b1 / a0;
            B2 = b2 / a0;
            A1 = a1 / a0;
            A2 = a2 / a0;
            Weight = weight;
        }

        /// <summary>Copies another section's coefficients and weight, keeping this delay line.</summary>
        public void CopyCoefficients(in Biquad from)
        {
            B0 = from.B0;
            B1 = from.B1;
            B2 = from.B2;
            A1 = from.A1;
            A2 = from.A2;
            Weight = from.Weight;
        }

        /// <summary>biquadFilterApplyDF1.</summary>
        public double ApplyDF1(double input)
        {
            double result = B0 * input + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2;
            X2 = X1;
            X1 = input;
            Y2 = Y1;
            Y1 = result;
            return result;
        }

        /// <summary>biquadFilterApplyDF1Weighted.</summary>
        public double ApplyDF1Weighted(double input)
        {
            double result = ApplyDF1(input);
            return Weight * result + (1 - Weight) * input;
        }

        /// <summary>biquadFilterApply: the transposed direct form 2.</summary>
        public double ApplyDF2(double input)
        {
            double result = B0 * input + X1;
            X1 = B1 * input - A1 * result + X2;
            X2 = B2 * input - A2 * result;
            return result;
        }
    }
}
