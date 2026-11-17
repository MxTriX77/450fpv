using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>A corner's leg tip in soil as the last Forces left it (w17-four-legs, ContactModel.TipOf): the leg's deflection
    /// along the soil's normal (m, below 0 stretched by a hold), the tip's sink below the soil's surface and its deepest (m), the
    /// soil's push on its face, the shaft's grip along it (+ against its going in) and the wet face's suction (N), the net force
    /// along the normal with the face's damping (N, below 0 a hold), the soil's resistance before the shaft across (N), the anchor's
    /// friction this step (world N) and the series solve's residual (N).</summary>
    public struct TipSoil
    {
        public bool Engaged, Sliding;
        public byte Surface;
        public int Object, Shape;
        public double Deflection, Sink, Max, Bearing, Grip, Pull, Force, Lateral, Residual;
        public Double3 Friction;
    }

    /// <summary>
    /// The legs' tips in the soil (w17-four-legs design decisions 1–4 and 6, ContactSpec.LegTips): each corner's one support is its
    /// leg's tip, a small penetrometer, met by the soil it stands in (the ground's surface where SampleGround has it, else the deepest
    /// soil shape the support touches). Every law is closed form (+ − × ÷ √), one node a corner, in series with the leg's springs by
    /// the series solve's safeguarded Newton steps, allocation-free.
    /// - The face (design 1): a disc of the support's radius plugged by the soil (A_t = πr²), met by a resistance growing from nothing
    ///   at the loose surface, q(z) = q_t·z/z_ref (surfaces.json's tip_resistance_pa), so its loading line is K·z, K = k_t·A_t,
    ///   k_t = q_t/z_ref; on the way out it unloads on Wong's line from its deepest sink z_m at the slope max(K, k_e), k_e Sneddon's
    ///   rigid punch 2aE/(1 − ν²) on E = α_E·q(z_m): an elastic return δ_e = (1 − ν²)·A_t/(2a·α_E) whatever the depth. The firm layer
    ///   past max_sink and the soil's damping c_s·A_t are the table's.
    /// - The shaft (design 2): the soil grips the embedded shaft, r_s·q(z) over πD·dz and, wet, the interface's adhesion c_a·πD·z_e,
    ///   both ways: a stick element along the leg, its spring FrictionStiffnessRatio times the face's unloading stiffness, held to
    ///   H(z_e) = r_s·k_t·πD·z_e²/2 + c_a·πD·z_e, so it adds to the face's resistance on the way in and holds on the way out, falling
    ///   to nothing as the tip comes out. Wet, the face's suction by the table's cohesive law over A_t (Soil.Cohesion). Nothing is
    ///   stored but the leg's and the stick's few microjoules: a held leg lets go over its depth, never by a snap.
    /// - Across (design 3): the anchor holds while the tip is in the soil (z_e &gt; 0) or presses, to μ·Fn + c_a·A_t + P(z_e), P the
    ///   soil before the shaft, Matlock 1970's p_u = (3 + J·z/D)·c_u·D up to 9·c_u·D on c_u = q(z)/N_kt, integrated over the shaft;
    ///   its spring FrictionStiffnessRatio times the face's unloading stiffness in series with the leg's side give.
    /// - Prints (design 6): a tip that leaves keeps its hole's deepest sink in a ring, so one landing in the same hole (within the
    ///   tip's radius) starts from it.
    /// - The start (design 4): each tip's set-down load over its static share, leg.setdown, drawn at the flight's start (SetDown).
    /// A rigid surface, or any object (Surface 0) under the tip, leaves the corner to the leg alone on its points, as the skids'.
    /// </summary>
    public sealed partial class ContactModel
    {
        const double TipPi = 3.141592653589793;

        /// <summary>The tips' ring of holes (design 6, assumed: two a corner).</summary>
        public const int TipPrintRing = 8;

        /// <summary>A start's tips stand on the ground when each one's sink at the start pose is within this, m (assumed: a placement
        /// on the surface to a millimetre; a start farther off is in the air or on something else, and is not set down).</summary>
        public const double SetDownReach = 0.001;

        // A corner's leg tip in soil.
        struct Tip
        {
            public bool Known, Engaged;
            public int Support;                  // the corner's tip support shape
            public int Object, Shape;            // the soil's identity: the ground (its surface) or a soil shape
            public byte Surface;
            public double X;                     // the leg's deflection along the soil's normal, m (below 0 stretched by a hold)
            public double Max;                   // the deepest sink, m
            public double Sink, Grip;            // the last step's sink (m) and the shaft's grip there (N): the stick's state
            public double Wet;                   // the face's wetted area since it last let go, m² (0 dry)
            public Double3 Anchor, AnchorBody;   // the stick anchor across (world) and the material point stuck there (body)
            public bool Anchored, Sliding;
            public double SetDown;               // leg.setdown, the start's draw
            public double StartSink, StartMax;   // the start's set: its rest sink and its deepest, m (0 when not set down)
            public double Peak, PeakDepth;       // the hold's peak since it last pressed (N) and the embedment there (m)
            public byte PeakCause;               // the hold's main part at its peak: 1 the grip, 2 the suction, 3 the adhesion
            public bool Freed;                   // a TipFreed to log in this step's LegSums
            public double FreedSpeed;
            public Double3 FreedPoint;
            public double Embedded, Lateral;     // this step's embedment (m) and the soil's resistance before the shaft (N)
            public double Unload;                // this step's unloading stiffness of the face, N/m
            public TipSoil Last;
        }

        struct TipPrint
        {
            public bool Used;
            public int Object, Shape;
            public byte Surface;
            public Double3 Point, N;             // the tip's centre as it left (world) and the soil's normal there
            public double Max;
        }

        TipPrint[] _tipPrints;
        int _tipPrintNext;
        double _tipArea, _tipReturn;             // A_t (m²) and the face's elastic return δ_e (m)
        bool _tipsStarted;

        // The tips (design 1–4) from the spec: each corner's one support, its draw, the face's constants, and the wet sets' checks.
        Tip[] Tips(FlightRandom random)
        {
            double a = _spec.TipRadius, d = _spec.ShaftDiameter;
            if (!(a > 0) || !(d > 0)) throw new ArgumentException("the legs' tips need their radius and their shaft's diameter (ContactSpec.TipRadius, ShaftDiameter)");
            if (!(_spec.LegSideStiffness > 0)) throw new ArgumentException("the legs' tips stand on the legs' side give (LegSideStiffness)");
            _tipArea = TipPi * a * a;
            _tipReturn = (1 - _spec.TipPoisson * _spec.TipPoisson) * _tipArea / (2 * a * _spec.TipModulusRatio);
            _tipPrints = new TipPrint[TipPrintRing];
            var tips = new Tip[4];
            for (int i = 0; i < _shapes.Length; i++)
            {
                ContactShape s = _shapes[i];
                if (s.Role != ContactRole.Support || s.Corner < 0 || s.Corner >= 4) continue;
                if (tips[s.Corner].Known) throw new ArgumentException("a leg's tip is its corner's one support: corner " + s.Corner + " has two");
                tips[s.Corner] = new Tip { Known = true, Support = i, SetDown = _spec.SetDownMin };
            }
            // The start's set-down, a stream of its own so no other draw moves (PHY-24): four draws, logged with their bounds.
            if (random != null)
            {
                SeedStream st = random.Open("leg.setdown");
                for (int c = 0; c < 4; c++)
                    tips[c].SetDown = random.Draw(ref st, "leg.setdown." + LegDamage.Names[c], _spec.SetDownMin, _spec.SetDownMax);
            }
            return tips;
        }

        // ---- The tip's laws (closed forms) -----------------------------------------------------------------------------------

        // The face's loading stiffness K = k_t·A_t (N/m) in a set, k_t = q_t/z_ref.
        double TipStiffness(in SoilSet set) => set.TipResistance / _spec.TipReferenceDepth * _tipArea;

        // The face's unloading stiffness from a deepest sink zMax: max(K, k_e), k_e = K·z_m/δ_e (Sneddon's punch on α_E·q(z_m)).
        double TipUnload(double k, double zMax) => zMax > _tipReturn ? k * (zMax / _tipReturn) : k;

        // The soil's push on the face at sink z (N) and its rate (N/m): the loading line past the deepest, else Wong's unloading line
        // (never below 0), and the firm layer past max_sink.
        double TipBearing(in SoilSet set, double k, double ku, double zMax, double z, out double rate)
        {
            double f;
            rate = 0;
            if (!(z > 0)) return 0;
            if (z >= zMax)
            {
                f = k * z;
                rate = k;
            }
            else
            {
                f = k * zMax - ku * (zMax - z);
                if (f > 0) rate = ku;
                else f = 0;
            }
            if (z > set.MaxSink)
            {
                double firm = _spec.Surfaces.FirmLayer * _tipArea;
                f += firm * (z - set.MaxSink);
                rate += firm;
            }
            return f;
        }

        // The shaft's hold along it at embedment z (N): its grip r_s·k_t·πD·z²/2 and, wet, the interface's adhesion c_a·πD·z; and the
        // rate d/dz (N/m).
        double TipHold(in SoilSet set, double z, out double rate, out double grip)
        {
            rate = 0;
            grip = 0;
            if (!(z > 0)) return 0;
            double g = _spec.ShaftFriction * (set.TipResistance / _spec.TipReferenceDepth) * TipPi * _spec.ShaftDiameter / 2;
            double w = set.InterfaceAdhesion * TipPi * _spec.ShaftDiameter;
            grip = g * z * z;
            rate = 2 * g * z + w;
            return grip + w * z;
        }

        // The soil before the embedded shaft across it (N, design 3): Matlock 1970's p_u = (3 + J·z/D)·c_u·D up to 9·c_u·D, c_u =
        // q(z)/N_kt, over the shaft: (k_t·D/N_kt)(1.5·z² + J·z³/(3D)) to z_c = 6D/J, then 9·k_t·D·(z² − z_c²)/(2·N_kt) more.
        double TipLateral(in SoilSet set, double z)
        {
            if (!(z > 0)) return 0;
            double d = _spec.ShaftDiameter, j = _spec.LateralGrowth, kt = set.TipResistance / _spec.TipReferenceDepth;
            double c = kt * d / _spec.ConeFactor, zc = j > 0 ? 6 * d / j : double.PositiveInfinity;
            if (z <= zc) return c * (1.5 * z * z + j * z * z * z / (3 * d));
            return c * (1.5 * zc * zc + j * zc * zc * zc / (3 * d)) + 4.5 * c * (z * z - zc * zc);
        }

        // The wet face's suction at sink z (N, toward the soil) and its rate d/dz (N/m): the table's cohesive law (Soil.Cohesion) over
        // the wetted area at the face's separation above its hole's set (where the unloading line falls to 0), on the face's unloading
        // modulus per area.
        double TipSuction(in SoilSet set, in Tip t, double k, double ku, double z, out double rate)
        {
            rate = 0;
            if (!(t.Wet > 0) || !(set.Adhesion > 0)) return 0;
            double set0 = t.Max - k * t.Max / ku;
            double pull = Soil.Cohesion(set, ku / _tipArea, t.Wet, set0 - z, out double slope);
            rate = slope; // d(pull)/ds, s = set − z: the suction falls as z rises in its rise, and back
            return pull;
        }

        // The soil's whole force on the tip along the normal at sink z (N, + pushing it out): the face, the shaft's stick element from
        // its last state (held to ±H), less the suction; and its rate d/dz.
        double TipForce(in SoilSet set, in Tip t, double k, double ku, double kg, double z, out double rate, out double bearing, out double grip,
            out double pull)
        {
            bearing = TipBearing(set, k, ku, t.Max, z, out double rb);
            double h = TipHold(set, z > 0 ? z : 0, out double rh, out _);
            double g = t.Grip + kg * (z - t.Sink), rg = kg;
            if (g > h)
            {
                g = h;
                rg = rh;
            }
            else if (g < -h)
            {
                g = -h;
                rg = -rh;
            }
            grip = g;
            pull = TipSuction(set, t, k, ku, z, out double rp);
            rate = rb + rg + rp;
            return bearing + g - pull;
        }

        // ---- The corner -------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Corner c's tip in soil (design 1–3): the series solve with the leg along the soil's normal (its springs this step, as the
        /// half-skid's: LegGive, the side give by the tilt, the lever's rise, the anchor's stretch), the face's force with its damping,
        /// the shaft's grip and the suction, and the corner's one point for the legs' law (TipTrial). False leaves the corner to the
        /// leg alone on its points (no soil under the tip, a rigid surface, or an object under it).
        /// </summary>
        bool TipCorner(int c, in FlightState s, ref Corners compression)
        {
            ref Tip t = ref _tips[c];
            int i = t.Support;
            Double3 centre = World(s, _shapes[i].A);
            if (_off[i])
            {
                if (t.Engaged) LeaveTip(c, centre, false);
                return false;
            }
            // The soil under it: the ground where its sample has a surface, else the deepest soil shape it touches; any object
            // (Surface 0) it presses leaves it to the leg's points.
            bool other = false;
            int soilPair = -1;
            double soilDepth = double.NegativeInfinity;
            for (int j = 0; j < _count[i]; j++)
            {
                ref Pair p = ref _pairs[i * PairsPerShape + j];
                if (!p.Touching || p.Suppressed) continue;
                if (p.Surface == WorldIds.NoSurface)
                {
                    if (p.Depth > 0) other = true;
                    continue;
                }
                if (p.Depth > soilDepth)
                {
                    soilDepth = p.Depth;
                    soilPair = j;
                }
            }
            ref Pair g = ref _ground[i];
            bool shape = soilPair >= 0 && (g.Surface == WorldIds.NoSurface || soilDepth > g.Depth);
            ref Pair soil = ref shape ? ref _pairs[i * PairsPerShape + soilPair] : ref g;
            double delta = soil.Depth;
            Double3 n = soil.N;
            byte surface = soil.Surface;
            int obj = shape ? soil.Object : WorldIds.Ground, shp = shape ? soil.Shape : 0;
            bool soft = surface != WorldIds.NoSurface && _surfaceRow[surface].Known && !_soil[surface].Rigid && _soil[surface].TipResistance > 0;
            if (t.Engaged && (other || !soft || t.Object != obj || t.Shape != shp || t.Surface != surface))
            {
                t.FreedSpeed = Double3.Dot(PointVelocity(s, centre), n);
                LeaveTip(c, centre, true, n);
            }
            if (other || !soft) return false;
            if (!t.Engaged && !(delta > 0)) return false;
            if (!t.Engaged) LandTip(c, obj, shp, surface, centre, n);

            // The leg's springs along the soil's normal (the half-skid's series block, SoilCorner; no frame on four legs).
            SoilSet set = _soil[surface];
            LegGive(c, out double k0, out double c0leg, out double legSide, out double legSideDamp);
            Double3 legUp = s.Attitude.Rotate(new Double3(0, 1, 0));
            bool lever = _spec.LegLeverLength > 0 && legSide > 0;
            if (lever) legUp = LeverAxis(c, s, legUp);
            double along = 0;
            Double3 across = default, dt = default;
            if (_legs != null && _legs.Moved(c))
            {
                along = _legs.AxialOffset(c);
                across = s.Attitude.Rotate(_legs.LateralOffset(c));
            }
            if (t.Anchored)
            {
                dt = World(s, t.AnchorBody) - t.Anchor;
                dt = dt - n * Double3.Dot(n, dt);
            }
            double ratio = _spec.FrictionStiffnessRatio;
            double cu = Double3.Dot(legUp, n), ud = Double3.Dot(legUp, dt);
            double kq0 = legSide > 0 ? Series(legSide, ratio * k0) : 0, rise = 0;
            if (lever)
            {
                Double3 d0 = n * t.X - dt;
                double a0 = Double3.Dot(legUp, d0) - along;
                Double3 lat0 = d0 - legUp * Double3.Dot(legUp, d0) - across;
                Lever(a0, lat0.Length(), k0, 0, legSide, _spec.LegLeverLength, SwayCap(c), out double secant0, out rise);
                kq0 = OnAnchor(secant0, ratio * k0);
            }
            double kN = k0 * cu * cu + kq0 * (1 - cu * cu);
            double b = k0 * cu * (ud + along + rise) - kq0 * cu * ud + kq0 * Double3.Dot(n, across);

            // The series solve: kN·x − b = F(δ − x), F the soil's whole force (rising in z: the leg is far stiffer than the hold's
            // fall), safeguarded Newton from the last step's x. The bracket: no further than the leg reaches a tip lifted out (z ≤ 0,
            // where only the suction holds), no more stretched than the whole hold a millimetre deeper and the suction's peak pull.
            double k = TipStiffness(set), ku = TipUnload(k, t.Max), kg = ratio * ku;
            double holdCap = TipHold(set, (delta > 0 ? delta : 0) + 0.001, out _, out _) + set.Adhesion * t.Wet;
            double hi = DetMath.Max(delta, b / kN), lo = DetMath.Min(0, b / kN) - holdCap / kN;
            double x = DetMath.Min(hi, DetMath.Max(lo, t.X));
            for (int it = 0; it < NewtonSteps; it++)
            {
                double fs = TipForce(set, t, k, ku, kg, delta - x, out double rs, out _, out _, out _);
                double residual = kN * x - b - fs, slope = kN + rs;
                if (residual == 0) break;
                if (residual > 0) hi = x;
                else lo = x;
                double next = slope > 0 ? x - residual / slope : 0.5 * (lo + hi);
                x = next >= lo && next <= hi ? next : 0.5 * (lo + hi);
            }
            double z = delta - x;
            double force = TipForce(set, t, k, ku, kg, z, out double stiffness, out double bearing, out double grip, out double pull);

            // The face's damping c_s·A_t in series with the leg's while it presses, at its closing speed.
            Double3 point = centre - n * _shapes[i].Radius;
            double vn = Double3.Dot(PointVelocity(s, point), n);
            double cs = bearing > 0 && set.Damping > 0 ? Series(c0leg, set.Damping * _tipArea) : 0;
            double face = bearing - cs * vn;
            if (face < 0) face = 0;
            double fn = face + grip - pull;
            double embedded = z > 0 ? z : 0, lateral = TipLateral(set, embedded);

            // The hold's peak and its main part since the face last pressed; a tip out of the soil with nothing holding it lets go.
            if (bearing > 0 && fn > 0) t.Peak = t.PeakDepth = 0;
            else if (-fn > t.Peak)
            {
                double adhesion = set.InterfaceAdhesion * TipPi * _spec.ShaftDiameter * embedded, shaft = -grip;
                t.Peak = -fn;
                t.PeakDepth = embedded;
                t.PeakCause = pull >= shaft ? (byte)2 : shaft > 0 && adhesion > shaft - adhesion ? (byte)3 : (byte)1;
            }
            t.Grip = grip;
            t.Sink = z;
            t.X = x;
            t.Embedded = embedded;
            t.Lateral = lateral;
            t.Unload = ku;
            if (set.Adhesion > 0)
            {
                if (bearing > 0) t.Wet = _tipArea;
                else if (t.Max - k * t.Max / ku - z >= set.AdhesionRelease) t.Wet = 0;
            }
            t.Last = new TipSoil
            {
                Engaged = true, Surface = surface, Object = obj, Shape = shp, Deflection = x, Sink = z, Max = DetMath.Max(t.Max, z), Bearing = bearing,
                Grip = grip, Pull = pull, Force = fn, Lateral = lateral, Residual = kN * x - b - force, Sliding = t.Sliding,
            };
            if (z > t.Max) t.Max = z;
            if (!(z > 0) && !(pull > 0))
            {
                // Out of the soil: nothing holds it any more (the grip and the face are nothing at z ≤ 0).
                t.FreedSpeed = vn;
                LeaveTip(c, centre, true, n);
                return false;
            }
            compression[c] = z;
            TipTrial(c, ref t, s, legUp, n, point, x, fn, stiffness, cs, k0, legSide, legSideDamp, along, across, set, lateral, embedded,
                shape ? soilPair : -1, i);
            // The support's own ground anchor stands aside while the soil carries the corner.
            g.Anchored = false;
            return true;
        }

        // The tip's point for the legs' law (SoilTrial's on four legs): the leg's push along the soil's normal its solve's, across it
        // the side give in series with the anchor (FrictionStiffnessRatio × the face's unloading stiffness), damped as p7g's points;
        // the anchor held while the tip presses or is in the soil.
        void TipTrial(int c, ref Tip t, in FlightState s, Double3 up, Double3 n, Double3 point, double x, double fn, double soilStiffness, double cn,
            double k0, double legSide, double legSideDamp, double along, Double3 across, in SoilSet set, double lateral, double embedded,
            int creditPair, int creditShape)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            double ratio = _spec.FrictionStiffnessRatio, anchor = ratio * t.Unload;
            double kq = legSide > 0 ? Series(legSide, anchor) : 0;
            bool lever = _spec.LegLeverLength > 0 && legSide > 0;
            double cq = legSideDamp > 0 && cn > 0 ? Series(legSideDamp, DetMath.Sqrt(ratio) * cn) : 0;
            _axial[c] = soilStiffness > 0 ? Series(k0, soilStiffness) : 0;
            _side[c] = kq;
            _tubeSide[c] = legSide;
            _legTouching[c] = true;
            sp.On = true;
            sp.Static = set.StaticFriction;
            sp.Kinetic = set.KineticFriction;
            sp.Adhesion = set.InterfaceAdhesion * _tipArea;
            sp.Side = sp.End = lateral;
            sp.Axis = default;
            sp.Along = k0;
            sp.Across = kq;
            sp.AcrossF = 0;
            sp.Pull = default;
            sp.Shape = creditShape;
            sp.Pair = creditPair;
            ref Pair q = ref sp.Foot;
            q.N = n;
            q.Point = point;
            q.Share = 1;
            q.Overlap = 1;
            if (!(fn > 0) && !(embedded > 0))
            {
                t.Anchored = false;
                q.Force = n * fn;
                q.Stretch = default;
                q.Drift = default;
                return;
            }
            if (!t.Anchored)
            {
                t.Anchor = point;
                t.AnchorBody = s.Attitude.InverseRotate(point - s.Position);
                t.Anchored = true;
                t.Sliding = false;
            }
            Double3 held = World(s, t.AnchorBody), dt = held - t.Anchor;
            dt = dt - n * Double3.Dot(n, dt);
            if (Reached(c, ref dt)) t.Anchor = held - dt;
            Double3 v = PointVelocity(s, held);
            Double3 d = n * x - dt;
            double a = Double3.Dot(up, d) - along, va = Double3.Dot(up, v);
            Double3 lat = d - up * Double3.Dot(up, d) - across;
            Double3 trial;
            if (lever)
            {
                double delta = lat.Length(), force = Lever(a, delta, k0, -cn * va, legSide, _spec.LegLeverLength, SwayCap(c), out double secant, out _);
                kq = OnAnchor(secant, anchor);
                _side[c] = kq;
                _tubeSide[c] = secant;
                sp.Across = kq;
                _lean[c] = delta;
                trial = up * force + lat * kq - (v - up * va) * cq;
            }
            else trial = up * (k0 * a - cn * va) + lat * kq - (v - up * va) * cq;
            q.Force = n * fn + (trial - n * Double3.Dot(n, trial));
            q.Stretch = dt;
            q.Drift = lat;
        }

        // The tip's point within its anchor's hold (TipFinal's limit), the anchor left as it is: the load the legs' law judges, a hold's
        // pull along the normal included.
        Double3 TipConed(int c)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            Double3 f = sp.Foot.Force, n = sp.Foot.N;
            double fn = Double3.Dot(n, f), press = fn > 0 ? fn : 0;
            Double3 ft = f - n * fn;
            double size = ft.Length();
            if (!(size > 0)) return f;
            double limit = (_tips[c].Sliding ? sp.Kinetic : sp.Static) * press + sp.Adhesion + sp.Side;
            return size > limit ? n * fn + ft * ((sp.Kinetic * press + sp.Adhesion + sp.Side) / size) : f;
        }

        // The tip's point through its anchor's hold at the foot, μ·Fn + c_a·A_t + P(z_e) (design 3), into the step's force and moment:
        // past it the anchor slides along the force's way by the excess over the leg's stiffness that way (SoilFinal's rule), and an
        // embedded tip's first slide is a TipPloughed. A hold's pull acts along the normal. Its force along the leg.
        double TipFinal(int c, Double3 up, in FlightState s, Double3 com, ref Double3 f, ref Double3 m, ref int touching, ref int strongest,
            ref int strongestPair, ref double strongestForce, StepEvents events)
        {
            ref SoilPoint sp = ref _soilPoint[c];
            ref Tip t = ref _tips[c];
            ref Pair q = ref sp.Foot;
            Double3 n = q.N, force = q.Force;
            double fn = Double3.Dot(n, force), press = fn > 0 ? fn : 0;
            Double3 ft = force - n * fn;
            double size = ft.Length();
            if (t.Anchored && size > 0)
            {
                double limit = (t.Sliding ? sp.Kinetic : sp.Static) * press + sp.Adhesion + sp.Side;
                if (size > limit)
                {
                    double kinetic = sp.Kinetic * press + sp.Adhesion + sp.Side;
                    Double3 dir = ft * (1 / size);
                    double ut = Double3.Dot(up, dir), ktt = (sp.Along * ut * ut + sp.Across * (1 - ut * ut)) * q.Share;
                    if (!t.Sliding && t.Embedded > 0 && sp.Side > 0)
                    {
                        FlightEvent e = FlightEvent.Of(EventKind.TipPloughed);
                        e.Part = (ushort)_shapes[t.Support].Part;
                        e.Index = (byte)c;
                        e.Object = t.Object;
                        e.Shape = t.Shape;
                        e.Surface = t.Surface;
                        e.Point = q.Point;
                        e.A = size;
                        e.B = t.Embedded;
                        e.C = sp.Side;
                        events.Add(e);
                    }
                    ft = dir * kinetic;
                    force = n * fn + ft;
                    if (Holds(ktt, sp.Along * q.Share))
                    {
                        Double3 st = q.Stretch + dir * ((size - kinetic) / ktt);
                        q.Stretch = st - n * Double3.Dot(n, st);
                        Reached(c, ref q.Stretch);
                        t.Anchor = World(s, t.AnchorBody) - q.Stretch;
                    }
                    t.Sliding = true;
                }
                else t.Sliding = false;
            }
            else
            {
                t.Sliding = false;
                if (!t.Anchored)
                {
                    ft = default;
                    force = n * fn;
                }
            }
            // At the foot, where the soil holds it: the tip less the anchor's stretch (Final's rule).
            f = f + force;
            m = m + Double3.Cross(s.Attitude.InverseRotate(q.Point - q.Stretch - s.Position) - com, s.Attitude.InverseRotate(force));
            touching++;
            ref Pair credit = ref sp.Pair < 0 ? ref _ground[sp.Shape] : ref _pairs[sp.Shape * PairsPerShape + sp.Pair];
            credit.Impulse += press * _dt;
            if (press > credit.PeakForce) credit.PeakForce = press;
            if (press > strongestForce)
            {
                strongestForce = press;
                strongest = sp.Shape;
                strongestPair = sp.Pair;
            }
            t.Last.Friction = ft;
            t.Last.Sliding = t.Sliding;
            return Double3.Dot(up, force);
        }

        // This step's TipFreed events (LegSums): a tip that the soil held on its way out let go.
        void TipEvents(StepEvents events)
        {
            for (int c = 0; c < 4; c++)
            {
                ref Tip t = ref _tips[c];
                if (!t.Freed) continue;
                t.Freed = false;
                FlightEvent e = FlightEvent.Of(EventKind.TipFreed);
                e.Part = (ushort)_shapes[t.Support].Part;
                e.Index = (byte)(c | t.PeakCause << 4);
                e.Object = t.Object;
                e.Shape = t.Shape;
                e.Surface = t.Surface;
                e.Point = t.FreedPoint;
                e.A = t.Peak;
                e.B = t.PeakDepth;
                e.C = t.FreedSpeed;
                events.Add(e);
                t.Peak = t.PeakDepth = 0;
            }
        }

        // A tip enters a soil: its hole starts from the newest print on that soil within the tip's radius of it, else virgin.
        void LandTip(int c, int obj, int shp, byte surface, Double3 centre, Double3 n)
        {
            ref Tip t = ref _tips[c];
            t.Engaged = true;
            t.Object = obj;
            t.Shape = shp;
            t.Surface = surface;
            t.X = t.Max = t.Sink = t.Grip = t.Wet = 0;
            t.Anchored = t.Sliding = false;
            t.Peak = t.PeakDepth = 0;
            double r = _shapes[t.Support].Radius;
            for (int back = 1; back <= TipPrintRing; back++)
            {
                ref TipPrint p = ref _tipPrints[(_tipPrintNext - back + TipPrintRing) % TipPrintRing];
                if (!p.Used || p.Object != obj || p.Shape != shp || p.Surface != surface) continue;
                Double3 d = centre - p.Point;
                d = d - p.N * Double3.Dot(p.N, d);
                if (!(Double3.Dot(d, d) <= r * r)) continue;
                t.Max = p.Max;
                break;
            }
        }

        // A tip leaves its soil: its hole into the ring (the oldest out); held (freed), a TipFreed for this step's LegSums.
        void LeaveTip(int c, Double3 centre, bool freed, Double3 n = default)
        {
            ref Tip t = ref _tips[c];
            if (t.Max > 0)
            {
                _tipPrints[_tipPrintNext] = new TipPrint
                {
                    Used = true, Object = t.Object, Shape = t.Shape, Surface = t.Surface, Point = centre, N = n.Y != 0 || n.X != 0 || n.Z != 0 ? n : new Double3(0, 1, 0),
                    Max = t.Max,
                };
                _tipPrintNext = (_tipPrintNext + 1) % TipPrintRing;
            }
            if (freed && t.Peak > 0)
            {
                t.Freed = true;
                t.FreedPoint = centre - (n.Y != 0 || n.X != 0 || n.Z != 0 ? n : new Double3(0, 1, 0)) * _shapes[t.Support].Radius;
            }
            t.Engaged = false;
            t.X = t.Max = t.Sink = t.Grip = t.Wet = 0;
            t.Anchored = t.Sliding = false;
            t.Embedded = t.Lateral = 0;
            t.Last = default;
        }

        /// <summary>Corner c's tip in soil as the last Forces left it (default when it is not in soil, on skids, or without
        /// surfaces.json).</summary>
        public TipSoil TipOf(int corner) => _tips != null && _tips[corner].Engaged ? _tips[corner].Last : default;

        /// <summary>Whether the legs stand on their tips in this flight (ContactSpec.LegTips with surfaces.json).</summary>
        public bool OnTips => _tips != null;

        /// <summary>Corner c's start draw, leg.setdown (its set-down load over its static share), and the start's set: its rest sink
        /// and its deepest, m (0 for a tip that was not set down: a start off the ground, or on a rigid surface).</summary>
        public double SetDownOf(int corner) => _tips != null ? _tips[corner].SetDown : 0;
        public double StartSink(int corner) => _tips != null ? _tips[corner].StartSink : 0;
        public double StartDeepest(int corner) => _tips != null ? _tips[corner].StartMax : 0;

        /// <summary>The tips' face: its area (m²) and its elastic return on unloading, δ_e (m).</summary>
        public double TipArea => _tipArea;
        public double TipReturn => _tipReturn;

        /// <summary>
        /// The start's set-down (design 4), once, before the flight's first Sense (FlightCore): when every tip stands on the ground at
        /// the start pose (its sink within SetDownReach), each tip on soil is pushed to its drawn set-down load λ·F on its loading line
        /// (the face and the shaft's grip against it) to its deepest z_m, and comes back to its static share F on the way out (the
        /// face's unloading line, the grip reversing by its stick and held at −H): its rest sink. F is the weight's share on four equal
        /// springs under the body's centre of mass (derived); a tip on a rigid surface rests at 0. The body is then lowered and tilted
        /// to the plane the four rest sinks fit best (least squares; the warp left is a few tenths of a millimetre), and each tip starts
        /// in its hole. True when it set the drone down.
        /// </summary>
        public bool SetDown(ref FlightState s, in MassProperties body)
        {
            if (_tips == null || _tipsStarted) return false;
            _tipsStarted = true;
            var at = new XZ[4];
            var ground = new GroundSample[4];
            var centre = new Double3[4];
            for (int c = 0; c < 4; c++)
            {
                if (!_tips[c].Known || _off[_tips[c].Support]) return false;
                centre[c] = World(s, _shapes[_tips[c].Support].A);
                at[c] = new XZ(centre[c].X, centre[c].Z);
            }
            _world.SampleGround(at, ground);
            var sink = new double[4];
            for (int c = 0; c < 4; c++)
            {
                if ((ground[c].Flags & GroundFlags.Hole) != 0) return false;
                sink[c] = Sunk(ground[c], centre[c].Y, _shapes[_tips[c].Support].Radius);
                if (!(DetMath.Abs(sink[c]) <= SetDownReach)) return false;
            }
            // The static shares: a rigid body on four equal springs, the weight's moments about the tips' centroid in plan (body).
            double mx = 0, mz = 0;
            for (int c = 0; c < 4; c++)
            {
                mx += _shapes[_tips[c].Support].A.X / 4;
                mz += _shapes[_tips[c].Support].A.Z / 4;
            }
            double sxx = 0, szz = 0;
            for (int c = 0; c < 4; c++)
            {
                double dx = _shapes[_tips[c].Support].A.X - mx, dz = _shapes[_tips[c].Support].A.Z - mz;
                sxx += dx * dx;
                szz += dz * dz;
            }
            double weight = body.Mass * -FlightCore.Gravity.Y, ex = body.CenterOfMass.X - mx, ez = body.CenterOfMass.Z - mz;
            var rest = new double[4];
            var shares = new double[4];
            for (int c = 0; c < 4; c++)
            {
                double dx = _shapes[_tips[c].Support].A.X - mx, dz = _shapes[_tips[c].Support].A.Z - mz;
                shares[c] = weight * (0.25 + (sxx > 0 ? ex * dx / sxx : 0) + (szz > 0 ? ez * dz / szz : 0));
                if (!(shares[c] > 0)) return false;
            }
            for (int c = 0; c < 4; c++)
            {
                double share = shares[c];
                rest[c] = share / _spec.LegStiffness; // the leg's own compression under its share, as on a rigid surface
                byte surface = ground[c].Surface;
                ref Tip t = ref _tips[c];
                if (surface == WorldIds.NoSurface || !_surfaceRow[surface].Known || _soil[surface].Rigid || !(_soil[surface].TipResistance > 0)) continue;
                SoilSet set = _soil[surface];
                SetDownTip(set, t.SetDown * share, share, out double zMax, out double zRest, out double grip);
                rest[c] += zRest;
                t.Engaged = true;
                t.Object = WorldIds.Ground;
                t.Shape = 0;
                t.Surface = surface;
                t.Max = t.StartMax = zMax;
                t.Sink = t.StartSink = zRest;
                t.Grip = grip;
                t.X = share / _spec.LegStiffness;
                t.Wet = set.Adhesion > 0 ? _tipArea : 0;
                t.Anchored = t.Sliding = false;
            }
            // The body comes to rest on the four: the move of each tip down m_i = dy + α·X_i + β·Z_i (X, Z its world plan offset from the
            // tips' centroid) such that the four tips' forces carry the weight and its moments about the centroid (the centre of mass's
            // plan offset), each tip's force by its soil from its set (TipForce, the leg in series): Newton on (dy, α, β) from the rest
            // sinks' plane. The draws' four depths fit no plane: the body settles where the soil's lines balance it, so the flight
            // starts at rest. A tip pushed past its set's deepest by it deepens its hole.
            double cx = 0, cz = 0, cy = 0;
            for (int c = 0; c < 4; c++)
            {
                cx += centre[c].X / 4;
                cy += centre[c].Y / 4;
                cz += centre[c].Z / 4;
            }
            Double3 com = World(s, body.CenterOfMass);
            double gx = com.X - cx, gz = com.Z - cz;
            var px = new double[4];
            var pz = new double[4];
            double wxx = 0, wzz = 0, mean = 0;
            for (int c = 0; c < 4; c++)
            {
                px[c] = centre[c].X - cx;
                pz[c] = centre[c].Z - cz;
                wxx += px[c] * px[c];
                wzz += pz[c] * pz[c];
                mean += (rest[c] - sink[c]) / 4;
            }
            double dy = mean, alpha = 0, beta = 0;
            for (int c = 0; c < 4; c++)
            {
                alpha += wxx > 0 ? px[c] * (rest[c] - sink[c] - mean) / wxx : 0;
                beta += wzz > 0 ? pz[c] * (rest[c] - sink[c] - mean) / wzz : 0;
            }
            for (int it = 0; it < 8; it++)
            {
                // The residual Σ f_i·(1, X_i, Z_i) − W·(1, gx, gz) and its Jacobian Σ f_i'·(1, X, Z)ᵀ(1, X, Z).
                double r0 = -weight, r1 = -weight * gx, r2 = -weight * gz;
                double j00 = 0, j01 = 0, j02 = 0, j11 = 0, j12 = 0, j22 = 0;
                for (int c = 0; c < 4; c++)
                {
                    double f = StartForce(c, ground[c].Surface, sink[c] + dy + alpha * px[c] + beta * pz[c], out double rate);
                    r0 += f;
                    r1 += f * px[c];
                    r2 += f * pz[c];
                    j00 += rate;
                    j01 += rate * px[c];
                    j02 += rate * pz[c];
                    j11 += rate * px[c] * px[c];
                    j12 += rate * px[c] * pz[c];
                    j22 += rate * pz[c] * pz[c];
                }
                // Cramer's rule on the symmetric 3×3.
                double det = j00 * (j11 * j22 - j12 * j12) - j01 * (j01 * j22 - j12 * j02) + j02 * (j01 * j12 - j11 * j02);
                if (!(DetMath.Abs(det) > 0)) break;
                double a0 = (r0 * (j11 * j22 - j12 * j12) - j01 * (r1 * j22 - j12 * r2) + j02 * (r1 * j12 - j11 * r2)) / det;
                double a1 = (j00 * (r1 * j22 - j12 * r2) - r0 * (j01 * j22 - j12 * j02) + j02 * (j01 * r2 - r1 * j02)) / det;
                double a2 = (j00 * (j11 * r2 - r1 * j12) - j01 * (j01 * r2 - r1 * j02) + r0 * (j01 * j12 - j11 * j02)) / det;
                dy -= a0;
                alpha -= a1;
                beta -= a2;
            }
            // Each tip's state where the body rests: its sink in the soil, its hole's deepest, its grip by its stick from the set.
            for (int c = 0; c < 4; c++)
            {
                ref Tip t = ref _tips[c];
                if (!t.Engaged) continue;
                StartForce(c, ground[c].Surface, sink[c] + dy + alpha * px[c] + beta * pz[c], out _, out double z, out double grip);
                t.Grip = grip;
                if (z > t.Max) t.Max = t.StartMax = z;
                t.Sink = t.StartSink = z;
                t.X = sink[c] + dy + alpha * px[c] + beta * pz[c] - z;
            }
            double mean2 = dy;
            Quat turn = Turn(new Double3(beta, 0, -alpha));
            var pivot = new Double3(cx, cy, cz);
            s.Position = pivot + turn.Rotate(s.Position - pivot) - new Double3(0, mean2, 0);
            s.Attitude = (turn * s.Attitude).Normalized();
            return true;
        }

        // A tip's force at the start's overlap d (its support's sink below the ground, m), the leg in series with its soil from its set
        // state (SetDownTip's), by Newton on z + F(z)/k0 = d; its rate dF/dd. A tip on a rigid surface or off the soil: the leg alone.
        double StartForce(int c, byte surface, double d, out double rate) => StartForce(c, surface, d, out rate, out _, out _);

        double StartForce(int c, byte surface, double d, out double rate, out double z, out double grip)
        {
            ref Tip t = ref _tips[c];
            double k0 = _spec.LegStiffness;
            z = 0;
            grip = 0;
            if (!t.Engaged)
            {
                rate = d > 0 ? k0 : 0;
                return d > 0 ? k0 * d : 0;
            }
            SoilSet set = _soil[surface];
            double k = TipStiffness(set), ku = TipUnload(k, t.StartMax), kg = _spec.FrictionStiffnessRatio * ku;
            Tip probe = t;
            probe.Max = t.StartMax;
            probe.Sink = t.StartSink;
            probe.Grip = t.Grip;
            z = DetMath.Min(d, t.StartSink);
            double f = 0, rs = 0;
            for (int it = 0; it < 12; it++)
            {
                f = TipForce(set, probe, k, ku, kg, z, out rs, out _, out grip, out _);
                double g = z + f / k0 - d, slope = 1 + rs / k0;
                if (g == 0 || !(slope > 0)) break;
                z -= g / slope;
            }
            f = TipForce(set, probe, k, ku, kg, z, out rs, out _, out grip, out _);
            rate = rs > 0 ? Series(k0, rs) : 0;
            return f;
        }

        // A tip set down by a load `push` and left at its share (design 4): its deepest sink on the loading line, K·z + H(z) = push (the
        // grip against the push; the firm layer past max_sink), then its way back out to its share: the face on its unloading line and
        // the grip from +H by its stick (FrictionStiffnessRatio × the unloading stiffness) to at most −H. Closed forms; the grip's H
        // changes little over the way back (a fraction of a millimetre) and is held at its value at z_m there.
        void SetDownTip(in SoilSet set, double push, double share, out double zMax, out double zRest, out double grip)
        {
            double k = TipStiffness(set), firm = _spec.Surfaces.FirmLayer * _tipArea;
            double g = _spec.ShaftFriction * (set.TipResistance / _spec.TipReferenceDepth) * TipPi * _spec.ShaftDiameter / 2;
            double w = set.InterfaceAdhesion * TipPi * _spec.ShaftDiameter;
            // g z² + (k + w) z − push = 0 (past max_sink: (k + firm + w) z − firm·max_sink).
            zMax = Root(g, k + w, -push);
            if (zMax > set.MaxSink) zMax = Root(g, k + firm + w, -(push + firm * set.MaxSink));
            double ku = TipUnload(k, zMax), kg = _spec.FrictionStiffnessRatio * ku;
            double h = TipHold(set, zMax, out _, out _), back = push - share;
            double free = (ku + kg) * (2 * h / kg); // the unload the stick takes before the grip reverses fully
            if (back <= free)
            {
                zRest = zMax - back / (ku + kg);
                grip = h - kg * (zMax - zRest);
                return;
            }
            // Past it: the face alone unloads, the grip held at −H(z): ku(z − z_p) − g z² − w z = share, z_p = z_m − K z_m/ku.
            double zp = zMax - k * zMax / ku;
            zRest = Root(g, w - ku, share + ku * zp);
            grip = -TipHold(set, zRest, out _, out _);
        }

        // The positive root of a z² + b z + c = 0 nearest 0 for a ≥ 0 (a vanishing a its linear root), stably: 2c / (−b ∓ √(b² − 4ac)).
        static double Root(double a, double b, double c)
        {
            if (!(a > 0)) return -c / b;
            double disc = b * b - 4 * a * c;
            if (disc < 0) disc = 0;
            double sq = DetMath.Sqrt(disc);
            return b > 0 ? -2 * c / (b + sq) : 2 * c / (-b + sq);
        }
    }
}
