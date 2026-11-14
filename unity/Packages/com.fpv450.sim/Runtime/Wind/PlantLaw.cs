using Fpv450.Sim.Contact;

namespace Fpv450.Sim.Wind
{
    /// <summary>The plant classes the farmland draws (w16-plants-in-the-air design.md §2; plants-in-the-air.md §2).</summary>
    public enum PlantKind : byte { GreenCulm, DryCulm, BurntCulm, WeedStalk, LeafyTwig, Branch, BroadleafTree, Pine }

    /// <summary>
    /// One plant element's first bending mode (design.md §1): a rigid link of length L from its base, on a spring k at the base,
    /// its inertia I set so the link swings at the element's own first mode. The same lean law as the props' stems
    /// (w9-village-physics 2.12 and 2.13, PropStems): a stem is a rigid link on 4EI/L, its weight's moment μgL²/2·sin φ, the air's
    /// drag normal to it ½ρ·C_d·d·|v|v a length. Each field is labelled where its factory sets it.
    /// </summary>
    public struct PlantMechanics
    {
        public PlantKind Kind;

        /// <summary>The link from the base to the top, m.</summary>
        public double Length;

        /// <summary>The span along the link that carries the drag, m from the base (a stem's whole length, a crown's depth).</summary>
        public double DragFrom, DragTo;

        /// <summary>k, N·m/rad: 4EI/L for a stem, a twig or a branch (a cantilever's tip slope under a uniform load, as
        /// PropStems), 3EI/h_c for a trunk loaded at its crown's centre of pressure h_c.</summary>
        public double Stiffness;

        /// <summary>I, kg·m²: k/ω₁², ω₁ the element's first mode without its weight (a cantilever's 1.875²·√(EI/μL⁴), or a tree's
        /// published f₀).</summary>
        public double Inertia;

        /// <summary>The weight's moment at 90°, N·m (μgL²/2; 0 for a tree, whose f₀ is measured standing).</summary>
        public double Weight;

        /// <summary>The structural damping c_s = 2ζ_s·√((k − W)·I), N·m·s/rad.</summary>
        public double Damping;

        /// <summary>½ρ·C_d·d, kg/m² (the stem's own section) and ½ρ·C_f·a (its foliage: a the one-sided leaf area a metre, or a
        /// crown's width), kg/m²: the drag a metre is (StemDrag + FoliageDrag·R)·|v|·v.</summary>
        public double StemDrag, FoliageDrag;

        /// <summary>The foliage's reconfiguration: R = (|v|/U_c)^α above U_c (m/s), 1 below.</summary>
        public double Reconfigure, Vogel;

        /// <summary>A stand's natural lean from upright in the wash grid, rad (PropStems' hash mean).</summary>
        public double NaturalLean;

        /// <summary>The height on the link where the wind is taken for the closed form, m: the centroid of the drag span weighted
        /// by the arm, ⅔·(s₁³ − s₀³)/(s₁² − s₀²) (a stem's ⅔·L, PropStems' point).</summary>
        public readonly double LoadPoint => 2.0 / 3 * (DragTo * DragTo * DragTo - DragFrom * DragFrom * DragFrom) / (DragTo * DragTo - DragFrom * DragFrom);

        /// <summary>The first mode's frequency standing, with its weight, still air, Hz.</summary>
        public readonly double Frequency => Stiffness > Weight ? DetMath.Sqrt((Stiffness - Weight) / Inertia) / PlantLaw.TwoPi : 0;

        /// <summary>
        /// A stem of the farmland's grass (GreenCulm, DryCulm, BurntCulm) or a weed stalk (WeedStalk), length m, from its cover
        /// row (surfaces.json, the props' stems' own numbers): d = d₁·L^(1/1.53) (Niklas 1995, published; d₁ the row's 1 m
        /// diameter, a weed's WeedDiameter, assumed), the hollow section of the row's wall share, E the row's band's middle
        /// (PropStems' field stems), ρ the row's; a weed's leaves double its mass a metre (assumed, plants-in-the-air.md §3.2).
        /// ζ_s StemZeta (published) or a weed's WeedZeta (assumed); C_d PropStrikes.StemDrag; airDensity the line's (tether.json).
        /// </summary>
        public static PlantMechanics Stem(PlantKind kind, double length, in CoverSet set, double airDensity)
        {
            bool weed = kind == PlantKind.WeedStalk;
            double d1 = weed ? PlantLaw.WeedDiameter : set.Diameter;
            double d = d1 * DetMath.Exp(PropStrikes.StemAllometry * DetMath.Log(length));
            double di = d * (1 - 2 * set.WallShare), d2 = d * d, di2 = di * di;
            double ei = 0.5 * (set.ModulusMin + set.ModulusMax) * PlantLaw.Pi / 64 * (d2 * d2 - di2 * di2);
            double mu = set.Density * PlantLaw.Pi / 4 * (d2 - di2) * (weed ? 2 : 1);
            var m = new PlantMechanics
            {
                Kind = kind, Length = length, DragFrom = 0, DragTo = length,
                StemDrag = 0.5 * airDensity * PropStrikes.StemDrag * d, NaturalLean = 0.5 * PropStrikes.StemNaturalLean,
            };
            Cantilever(ref m, ei, mu, weed ? PlantLaw.WeedZeta : PlantLaw.StemZeta);
            return m;
        }

        /// <summary>
        /// A leafy twig or a branch (LeafyTwig, Branch): solid green wood of the twig row (E its band's middle, ρ its), diameter m,
        /// its foliage kg a metre (assumed: plants-in-the-air.md §3.2's leafy-to-bare frequencies), the foliage's one-sided area
        /// that mass over the leaf row's fresh mass per area (derived), its drag coefficient FoliageDrag (assumed) reconfiguring
        /// as leaves roll into cones (Vogel −1 from FoliageReconfigure: published exponent, assumed onset). ζ_s CrownZeta.
        /// </summary>
        public static PlantMechanics Woody(PlantKind kind, double length, double diameter, double foliage, in CoverSet twig, double leafMassPerArea,
            double airDensity)
        {
            double d2 = diameter * diameter;
            double ei = 0.5 * (twig.ModulusMin + twig.ModulusMax) * PlantLaw.Pi / 64 * d2 * d2;
            double mu = twig.Density * PlantLaw.Pi / 4 * d2 + foliage;
            var m = new PlantMechanics
            {
                Kind = kind, Length = length, DragFrom = 0, DragTo = length,
                StemDrag = 0.5 * airDensity * PropStrikes.StemDrag * diameter,
                FoliageDrag = 0.5 * airDensity * PlantLaw.FoliageDrag * foliage / leafMassPerArea,
                Reconfigure = PlantLaw.FoliageReconfigure, Vogel = PlantLaw.FoliageVogel,
            };
            Cantilever(ref m, ei, mu, PlantLaw.CrownZeta);
            return m;
        }

        /// <summary>
        /// A whole tree's sway (BroadleafTree, Pine): height H, its trunk's diameter at breast height (m, as drawn), its crown's
        /// width and depth (m, as drawn). The link is the tree to its top; its stiffness the trunk's cantilever loaded at the
        /// crown's centre of pressure, 3EI/h_c (E the twig row's green wood, derived); its inertia from the published f₀: a pine's
        /// by Moore and Maguire's regression as restated (0.0948 + (3.4317 − 0.7765)·D/H², D in cm, published), a broadleaf's
        /// BroadleafFrequency (assumed); ζ_s CrownZeta or PineZeta. The crown's drag its silhouette, ½ρ·C_f·width a metre over the
        /// crown's depth, reconfiguring as a dense crown (Vogel −1, published).
        /// </summary>
        public static PlantMechanics Tree(PlantKind kind, double height, double dbh, double crownWidth, double crownDepth, in CoverSet wood,
            double airDensity)
        {
            bool pine = kind == PlantKind.Pine;
            double from = height - crownDepth, centre = 2.0 / 3 * (height * height * height - from * from * from) / (height * height - from * from);
            double d2 = dbh * dbh, ei = 0.5 * (wood.ModulusMin + wood.ModulusMax) * PlantLaw.Pi / 64 * d2 * d2;
            double k = 3 * ei / centre;
            double f0 = pine ? 0.0948 + (3.4317 - 0.7765) * (100 * dbh) / (height * height) : PlantLaw.BroadleafFrequency;
            double w0 = PlantLaw.TwoPi * f0, inertia = k / (w0 * w0), zeta = pine ? PlantLaw.PineZeta : PlantLaw.CrownZeta;
            return new PlantMechanics
            {
                Kind = kind, Length = height, DragFrom = from, DragTo = height, Stiffness = k, Inertia = inertia, Weight = 0,
                Damping = 2 * zeta * DetMath.Sqrt(k * inertia), FoliageDrag = 0.5 * airDensity * PlantLaw.FoliageDrag * crownWidth,
                Reconfigure = PlantLaw.FoliageReconfigure, Vogel = PlantLaw.FoliageVogel,
            };
        }

        // k = 4EI/L, ω₁ = 1.875²·√(EI/μL⁴) (the cantilever's first mode, PropStems' 1.875), I = k/ω₁², W = μgL²/2,
        // c_s = 2ζ·√((k − W)·I).
        static void Cantilever(ref PlantMechanics m, double ei, double mu, double zeta)
        {
            double l = m.Length, k = 4 * ei / l;
            double w1 = PlantLaw.Beta1 * PlantLaw.Beta1 * DetMath.Sqrt(ei / (mu * l * l * l * l));
            m.Stiffness = k;
            m.Inertia = k / (w1 * w1);
            m.Weight = mu * PlantLaw.Gravity * l * l / 2;
            m.Damping = k > m.Weight ? 2 * zeta * DetMath.Sqrt((k - m.Weight) * m.Inertia) : 0;
        }
    }

    /// <summary>A link's state: its lean from upright as a horizontal vector (rad, world X and Z: the top's way times the angle)
    /// and that vector's rate, rad/s.</summary>
    public struct PlantState
    {
        public double X, Z, RateX, RateZ;
    }

    /// <summary>What the drawn side reads per plant: its bend from its rest (rad, world X and Z: the top's way times the angle)
    /// and the bend's rate (rad/s).</summary>
    public struct PlantBend
    {
        public double X, Z, RateX, RateZ;
    }

    /// <summary>
    /// The plants' law in the air (w16-plants-in-the-air design.md §1, task 1.2): the air's generalised force on a leaning link and
    /// its damping, and one step of the link's motion. Engine-free, allocation-free, DetMath only.
    /// - The link leans φ toward ê (horizontal unit), its axis t = (sin φ·ê, cos φ). The air u at an element meets it as its part
    ///   normal to t, in the lean's plane v_n = (u·ê)·cos φ − u_y·sin φ and across it v_⊥ = u·ê_⊥; the drag a metre is
    ///   c(|v|)·|v|·v, c = StemDrag + FoliageDrag·R(|v|). Its generalised forces are ∫ f·s·∂t/∂b ds over the drag span:
    ///   along Q_∥ = ∫ c|v|v_n·s ds, across Q_⊥ = (sin φ/φ)·∫ c|v|v_⊥·s ds.
    /// - The drag is on the air relative to the moving element (quasi-steady): along v_n − s·φ̇_∥, across v_⊥ − s·(sin φ/φ)·φ̇_⊥.
    ///   Its slope in the link's own rate is the air's damping (plants-in-the-air.md §3.1's ζ_a at rest):
    ///   along ∫ s²·|v|·(c + c'·n_∥²) ds, across (sin φ/φ)²·∫ s²·|v|·(c + c'·n_⊥²) ds, c' = c + FoliageDrag·α·R.
    /// - The motion: I·b̈ + c_s·ḃ + k·(b − b₀) − W·sin φ·ê = Q(b, ḃ). One step of dt holds the air (zero-order hold) and
    ///   integrates each axis exactly, linearised at the step's start: along with the tangent stiffness k − W·cos φ − ∂Q_∥/∂φ
    ///   (by a central difference), across with k − W·sin φ/φ (the drag's is nil to first order), the drag's rate slope with
    ///   c_s, each toward its Newton target; the link cannot pass lying flat (|b| ≤ π/2, the ground).
    /// </summary>
    public static class PlantLaw
    {
        internal const double Pi = 3.141592653589793, TwoPi = 6.283185307179586, HalfPi = 1.5707963267948966;
        internal const double Gravity = 9.80665;

        /// <summary>The cantilever's first root, as PropStems takes it (published 1.8751).</summary>
        internal const double Beta1 = 1.875;

        /// <summary>A grass or straw shoot's structural damping ratio: 0.08 (published: a wheat shoot, Sterling et al. 2003;
        /// plants-in-the-air.md §3.2, §7).</summary>
        public const double StemZeta = 0.08;

        /// <summary>A weed stalk's: 0.1 (assumed: the cover rows' damping_ratio, within the note's 0.08–0.1).</summary>
        public const double WeedZeta = 0.1;

        /// <summary>A broadleaf crown in leaf, and its twigs and branches: 0.086 (published: 0.086 ± 0.022, Jackson et al. 2019).</summary>
        public const double CrownZeta = 0.086;

        /// <summary>A pine's: 0.05 (published as an upper bound: internal damping under 0.05, Moore and Maguire 2004).</summary>
        public const double PineZeta = 0.05;

        /// <summary>A weed stalk's diameter at 1 m, m: 8 mm (assumed: stem-properties.md's 5–15 mm weeds).</summary>
        public const double WeedDiameter = 0.008;

        /// <summary>Foliage's drag coefficient on its one-sided leaf area (twigs, branches) or a crown's silhouette: 0.3 (assumed:
        /// within the canopy models' 0.1–0.5, Katul et al. 2004, and the conifers' 0.22–0.47 at 20 m/s, Rudnicki et al. 2004).</summary>
        public const double FoliageDrag = 0.3;

        /// <summary>The speed foliage starts to reconfigure, m/s: 4 (assumed: the hardwood tunnels' lowest speed, Vollsinger et al.
        /// 2005) and Vogel's exponent past it, −1 (published: leaves rolled into cones and dense crowns, Gosselin 2019).</summary>
        public const double FoliageReconfigure = 4, FoliageVogel = -1;

        /// <summary>A broadleaf tree's sway in leaf, Hz: 0.45 (assumed, 0.3–0.6: Jackson et al. 2021's pendulum fit and open-grown
        /// offset; plants-in-the-air.md §7).</summary>
        public const double BroadleafFrequency = 0.45;

        /// <summary>A leaf's own flutter, Hz: 6 (published: a cherry leaf's torsional mode, 6 ± 1, Tadrist et al. 2018); the belts'
        /// species 4–8 (assumed), each leaf at its own phase.</summary>
        public const double LeafFlutter = 6;

        /// <summary>The tangent's central difference, rad (numerical).</summary>
        const double Tangent = 1e-4;

        /// <summary>A tangent stiffness is floored at this share of k (numerical: a link whose weight passes its stiffness runs
        /// over in a few steps as it would, never at an unbounded rate).</summary>
        const double Floor = 0.01;

        /// <summary>The 12-point Gauss–Hermite rule's positive nodes and weights (published, Abramowitz and Stegun 25.4.46; the
        /// suite checks its moments): E[g(μ + σξ)] = Σ wᵢ·g(μ + √2·σ·xᵢ)/√π over ±xᵢ.</summary>
        internal static readonly double[] HermiteX =
        {
            0.31424037625435913, 0.9477883912401638, 1.5976826351526048, 2.2795070805010598, 3.0206370251208896, 3.889724897869782,
        };

        internal static readonly double[] HermiteW =
        {
            0.5701352362624795, 0.26049231026416114, 0.05160798561588393, 0.0039053905846290616, 8.573687043587859e-05, 2.658551684356302e-07,
        };

        internal const double SqrtPi = 1.7724538509055159, Sqrt2 = 1.4142135623730951;

        /// <summary>The air's generalised forces on a link and its damping, along and across its lean.</summary>
        public struct Forces
        {
            public double Along, Across, DampAlong, DampAcross;
        }

        /// <summary>The drag's coefficient a metre at |v| (c), and c' = c + FoliageDrag·α·R, the slope's along-flow part.</summary>
        internal static void Coefficient(in PlantMechanics m, double v, out double c, out double slope)
        {
            double r = 1;
            if (m.FoliageDrag > 0 && v > m.Reconfigure) r = DetMath.Exp(m.Vogel * DetMath.Log(v / m.Reconfigure));
            double foliage = m.FoliageDrag * r;
            c = m.StemDrag + foliage;
            slope = c + (v > m.Reconfigure ? foliage * m.Vogel : 0);
        }

        /// <summary>The 8-point Gauss–Legendre rule on [−1, 1] (published), the positive half: a span's integrals with the
        /// element's own motion in the drag.</summary>
        static readonly double[] LegendreX = { 0.1834346424956498, 0.525532409916329, 0.7966664774136267, 0.9602898564975363 };
        static readonly double[] LegendreW = { 0.362683783378362, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763 };

        /// <summary>The forces on a link leaning φ toward (ex, ez), at rest, in air uniform over its drag span (world m/s).</summary>
        public static void Uniform(in PlantMechanics m, double phi, double ex, double ez, in Double3 air, out Forces f) =>
            Uniform(m, phi, ex, ez, air, 0, 0, out f);

        /// <summary>
        /// The forces on a link leaning φ toward (ex, ez), its lean turning at (along, across) rad/s, in air uniform over its drag
        /// span (world m/s): each element's drag on the air relative to it, over the span by Gauss–Legendre.
        /// </summary>
        public static void Uniform(in PlantMechanics m, double phi, double ex, double ez, in Double3 air, double rateAlong, double rateAcross, out Forces f)
        {
            f = default;
            DetMath.SinCos(phi, out double s, out double c);
            double ratio = SinOver(phi, s);
            double vn = (air.X * ex + air.Z * ez) * c - air.Y * s, vp = air.Z * ex - air.X * ez;
            double s0 = m.DragFrom, s1 = m.DragTo, mid = 0.5 * (s0 + s1), half = 0.5 * (s1 - s0);
            for (int i = 0; i < LegendreX.Length; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    double at = mid + side * half * LegendreX[i], w = half * LegendreW[i];
                    Point(m, vn - at * rateAlong, vp - at * ratio * rateAcross, out double gn, out double gp, out double hn, out double hp);
                    f.Along += w * gn * at;
                    f.Across += w * gp * at;
                    f.DampAlong += w * hn * at * at;
                    f.DampAcross += w * hp * at * at;
                }
            f.Across *= ratio;
            f.DampAcross *= ratio * ratio;
        }

        /// <summary>
        /// The forces on a stem (drag span from its base) leaning φ toward (ex, ez) in air sampled on its base's vertical: count
        /// heights over the base (m, rising, the first above 0) and the air at each (world m/s). Its element at s takes the air at
        /// its own height y = s·cos φ (linear between the heights, the lowest's below them, the highest's above), so
        /// Q = (1/cos²φ)·∫ g(y)·y dy and the damping (1/cos³φ)·∫ h(y)·y² dy to L·cos φ, each by the trapezoid on the heights;
        /// a stem lying flat takes the lowest air along its length.
        /// </summary>
        public static void Profile(in PlantMechanics m, double phi, double ex, double ez, double[] heights, Double3[] air, int count, out Forces f) =>
            Profile(m, phi, ex, ez, heights, air, count, 0, 0, out f);

        /// <summary>The same with the lean turning at (along, across) rad/s: each element's drag on the air relative to it.</summary>
        public static void Profile(in PlantMechanics m, double phi, double ex, double ez, double[] heights, Double3[] air, int count,
            double rateAlong, double rateAcross, out Forces f)
        {
            f = default;
            DetMath.SinCos(phi, out double s, out double c);
            double ratio = SinOver(phi, s), l = m.DragTo;
            if (c < 1e-6)
            {
                // Lying flat: the lowest air along its length, by Uniform's rule.
                Uniform(m, phi, ex, ez, air[0], rateAlong, rateAcross, out f);
                return;
            }
            double ra = rateAlong / c, rp = ratio * rateAcross / c; // the element at height y moves at (y/cos φ)·rate
            double top = l * c;
            double qn = 0, qp = 0, dn = 0, dp = 0, y0 = 0, gn = 0, gp = 0, hn = 0, hp = 0; // the integrands are nil at y = 0
            for (int k = 0; k <= count; k++)
            {
                double y;
                Double3 u;
                if (k < count && heights[k] < top)
                {
                    y = heights[k];
                    u = air[k];
                }
                else
                {
                    y = top;
                    if (k == 0) u = air[0];
                    else if (k >= count) u = air[count - 1];
                    else
                    {
                        double t = (top - heights[k - 1]) / (heights[k] - heights[k - 1]);
                        u = air[k - 1] + (air[k] - air[k - 1]) * t;
                    }
                }
                double un = (u.X * ex + u.Z * ez) * c - u.Y * s, up = u.Z * ex - u.X * ez;
                Point(m, un - y * ra, up - y * rp, out double an, out double ap, out double bn, out double bp);
                double width = y - y0;
                qn += 0.5 * (gn + an * y) * width;
                qp += 0.5 * (gp + ap * y) * width;
                dn += 0.5 * (hn + bn * y * y) * width;
                dp += 0.5 * (hp + bp * y * y) * width;
                gn = an * y;
                gp = ap * y;
                hn = bn * y * y;
                hp = bp * y * y;
                y0 = y;
                if (!(y < top)) break;
            }
            double c2 = c * c, c3 = c2 * c;
            f.Along = qn / c2;
            f.Across = ratio * qp / c2;
            f.DampAlong = dn / c3;
            f.DampAcross = ratio * ratio * dp / c3;
        }

        // The drag a metre on the air (vn, vp) normal to an element (along, across) and its slopes in that air (along, across).
        static void Point(in PlantMechanics m, double vn, double vp, out double gn, out double gp, out double hn, out double hp)
        {
            double v = DetMath.Sqrt(vn * vn + vp * vp);
            gn = gp = hn = hp = 0;
            if (!(v > 0)) return;
            Coefficient(m, v, out double k, out double slope);
            double nn = vn / v, np = vp / v;
            gn = k * v * vn;
            gp = k * v * vp;
            hn = v * (k + slope * nn * nn);
            hp = v * (k + slope * np * np);
        }

        // sin φ/φ, 1 − φ²/6 near 0.
        internal static double SinOver(double phi, double sin) => DetMath.Abs(phi) > 1e-6 ? sin / phi : 1 - phi * phi / 6;

        /// <summary>
        /// One step of dt of a link in air uniform over its span (world m/s), its natural lean b₀ (rad, world X and Z).
        /// </summary>
        public static void Step(in PlantMechanics m, ref PlantState state, double naturalX, double naturalZ, in Double3 air, double dt) =>
            Advance(m, ref state, naturalX, naturalZ, air, null, null, 0, dt);

        /// <summary>
        /// One step of dt of a stem in air sampled on its base's vertical (Profile's heights and air), its natural lean b₀.
        /// </summary>
        public static void Step(in PlantMechanics m, ref PlantState state, double naturalX, double naturalZ, double[] heights, Double3[] air,
            int count, double dt) => Advance(m, ref state, naturalX, naturalZ, default, heights, air, count, dt);

        static void Load(in PlantMechanics m, double phi, double ex, double ez, in Double3 uniform, double[] heights, Double3[] air, int count,
            double rateAlong, double rateAcross, out Forces f)
        {
            if (heights == null) Uniform(m, phi, ex, ez, uniform, rateAlong, rateAcross, out f);
            else Profile(m, phi, ex, ez, heights, air, count, rateAlong, rateAcross, out f);
        }

        static void Advance(in PlantMechanics m, ref PlantState st, double nx, double nz, in Double3 uniform, double[] heights, Double3[] air,
            int count, double dt)
        {
            double phi = DetMath.Sqrt(st.X * st.X + st.Z * st.Z), ex, ez;
            if (phi > 1e-12)
            {
                ex = st.X / phi;
                ez = st.Z / phi;
            }
            else
            {
                // Upright: the frame of the natural lean, else of the air near the load point, else east.
                Double3 u = heights == null ? uniform : air[count / 2];
                double n = DetMath.Sqrt(nx * nx + nz * nz), h = DetMath.Sqrt(u.X * u.X + u.Z * u.Z);
                if (n > 0) { ex = nx / n; ez = nz / n; }
                else if (h > 0) { ex = u.X / h; ez = u.Z / h; }
                else { ex = 1; ez = 0; }
                phi = 0;
            }
            double va = st.RateX * ex + st.RateZ * ez, vp = st.RateZ * ex - st.RateX * ez;
            Load(m, phi, ex, ez, uniform, heights, air, count, va, vp, out Forces f);
            Load(m, phi + Tangent, ex, ez, uniform, heights, air, count, va, vp, out Forces up);
            Load(m, phi - Tangent, ex, ez, uniform, heights, air, count, va, vp, out Forces down);
            DetMath.SinCos(phi, out double s, out double c);
            double k = m.Stiffness, w = m.Weight, ratio = SinOver(phi, s);
            double b0a = nx * ex + nz * ez, b0p = nz * ex - nx * ez;
            // Q(b, ḃ) ≈ Q₀ − D·(ḃ − ḃ₀): the slope D joins c_s, D·ḃ₀ the held force.
            double ra = f.Along + f.DampAlong * va + w * s - k * (phi - b0a), rp = f.Across + f.DampAcross * vp + k * b0p;
            double ka = k - w * c - (up.Along - down.Along) / (2 * Tangent), kp = k - w * ratio;
            if (ka < Floor * k) ka = Floor * k;
            if (kp < Floor * k) kp = Floor * k;
            double ta = phi + ra / ka, tp = rp / kp;
            double xa = phi - ta, xp = -tp;
            Exact(m.Inertia, m.Damping + f.DampAlong, ka, dt, ref xa, ref va);
            Exact(m.Inertia, m.Damping + f.DampAcross, kp, dt, ref xp, ref vp);
            double a = ta + xa, p = tp + xp;
            // (a, p) in the frame (ê, ê_⊥), ê_⊥ = (−ez, ex).
            st.X = a * ex - p * ez;
            st.Z = a * ez + p * ex;
            st.RateX = va * ex - vp * ez;
            st.RateZ = va * ez + vp * ex;
            double lean = DetMath.Sqrt(st.X * st.X + st.Z * st.Z);
            if (lean > HalfPi)
            {
                // Lying on the ground: no further, and no rate into it.
                double ux = st.X / lean, uz = st.Z / lean, out_ = st.RateX * ux + st.RateZ * uz;
                st.X = ux * HalfPi;
                st.Z = uz * HalfPi;
                if (out_ > 0)
                {
                    st.RateX -= out_ * ux;
                    st.RateZ -= out_ * uz;
                }
            }
        }

        /// <summary>
        /// The exact motion over dt of I·ẍ + c·ẋ + K·x = 0 from (x, v), K &gt; 0: e^(−σt)·(x·C + (v + σx)·S) and its rate
        /// e^(−σt)·(v·C − (σv + (K/I)·x)·S), σ = c/2I, with C = cos ωt, S = sin ωt/ω under critical damping (ω² = K/I − σ²) and
        /// C = cosh γt, S = sinh γt/γ over it (γ² = −ω²; S by its series below γt 10⁻³).
        /// </summary>
        internal static void Exact(double inertia, double damping, double stiffness, double dt, ref double x, ref double v)
        {
            double sigma = damping / (2 * inertia), kOverI = stiffness / inertia, w2 = kOverI - sigma * sigma;
            double co, sn;
            if (w2 > 0)
            {
                double w = DetMath.Sqrt(w2);
                DetMath.SinCos(w * dt, out double sin, out double cos);
                co = cos;
                sn = sin / w;
            }
            else
            {
                double g = DetMath.Sqrt(-w2), gt = g * dt;
                if (gt < 1e-3)
                {
                    co = 1 + 0.5 * gt * gt;
                    sn = dt * (1 + gt * gt / 6);
                }
                else
                {
                    double ep = DetMath.Exp(gt), em = 1 / ep;
                    co = 0.5 * (ep + em);
                    sn = 0.5 * (ep - em) / g;
                }
            }
            double e = DetMath.Exp(-sigma * dt);
            double x1 = e * (x * co + (v + sigma * x) * sn);
            double v1 = e * (v * co - (sigma * v + kOverI * x) * sn);
            x = x1;
            v = v1;
        }

        /// <summary>
        /// The rest a link reaches along one plane from its natural lean φ₀ under a net moment R(φ) (Q + W·sin φ − k·(φ − φ₀)),
        /// the first root in the way R pushes: a scan of 0.05 rad, then 50 halvings; ±π/2 (lying on the ground) when none.
        /// </summary>
        internal static double Settle<T>(ref T moment, double phi0) where T : struct, IMoment
        {
            double f0 = moment.Net(phi0);
            if (f0 == 0) return phi0;
            double d = f0 > 0 ? 1 : -1, a = phi0, b;
            while (true)
            {
                b = a + d * 0.05;
                if (d * b >= HalfPi)
                {
                    b = d * HalfPi;
                    if (moment.Net(b) * d > 0) return b;
                    break;
                }
                if (moment.Net(b) * d <= 0) break;
                a = b;
            }
            for (int i = 0; i < 50; i++)
            {
                double mid = 0.5 * (a + b);
                if (moment.Net(mid) * d > 0) a = mid;
                else b = mid;
            }
            return 0.5 * (a + b);
        }

        /// <summary>A net moment along one plane as a function of the lean, rad → N·m (Settle's; a struct, so no allocation).</summary>
        internal interface IMoment
        {
            double Net(double phi);
        }

        // The weight alone from the natural lean.
        struct WeightOnly : IMoment
        {
            public double K, W, Natural;
            public double Net(double phi) => W * Sin(phi) - K * (phi - Natural);
        }

        static double Sin(double x)
        {
            DetMath.SinCos(x, out double s, out _);
            return s;
        }

        /// <summary>A link's rest under its weight alone from its natural lean φ₀ (rad, the same plane): the drawn plant's own
        /// pose, from which its bend is read.</summary>
        public static double Rest(in PlantMechanics m, double natural)
        {
            var w = new WeightOnly { K = m.Stiffness, W = m.Weight, Natural = natural };
            return Settle(ref w, natural);
        }
    }
}
