using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>The dust the wash lifts at one place (DustLaw): the wall jet's friction velocity u* there and the surface's threshold
    /// u*_t (m/s; +∞ where none lifts: a covered surface, saturated ground, no ground), whether it lifts, the saltation flux that
    /// knocks the dust loose (kg/m/s, White's) and the surface (IWorldQuery's id). Film: the surface's loose layer is a thin film
    /// (a track, a slab, rubble): short whiffs, then clean (its supply is not modelled).</summary>
    public struct DustSample
    {
        public double Friction, Threshold, Flux;
        public bool Lifts, Film;
        public byte Surface;
    }

    /// <summary>
    /// The dust the props' wash lifts (w16-plants-in-the-air design.md §5; plants-in-the-air.md §5): a threshold on the wall jet's
    /// friction velocity per surface. Dust is knocked loose by saltating sand-sized grains, so its threshold is the saltation
    /// threshold (Kok et al. 2012, published); the surfaces' dry thresholds and their labels are the note's §5.3.
    /// - **u\*** from the wall jet's peak speed u_m along the ground and its height y_m: the larger of the smooth wall's
    ///   u_m·√(c_f/2), c_f = 0.06·(u_m·δ/ν)^−0.3·(r/H)^−0.16, δ = y_m/0.165 (Poreh et al. 1967 and Verhoff's profile, published)
    ///   and the fully rough wall's u_m/(5.75·log₁₀(y_m/k_s) + 8.5) (Nikuradse, published; k_s per surface, assumed): the
    ///   transitional wall lies between and tends to the larger.
    /// - **Wet ground lifts none** (derived): the sim's wet and saturated sets hold a loam's field capacity (Wetting.WaterContentWet,
    ///   25 % by volume, 15–24 % by mass here), and at it the topsoil's grains are bound in the set's own cohesion, 0.3–18 kPa
    ///   (the sets' cohesion_pa), against the wash's shear ρu*² of at most 8 Pa (u* ≤ 2.5 m/s at a 0.3 m hover). Fécan et al.'s
    ///   moisture factor √(1 + 1.21·(w − w′)^0.68), w′ = 0.0014c² + 0.17c (published; w the gravimetric water %, c the clay %,
    ///   assumed per surface) holds only up to FecanMaximum, the moisture it was fitted to; past it, none. "Wet" is what the sim
    ///   knows: the flight's rain (SurfaceTable.MoistureOf). Covered surfaces (sod, weeds, straw, litter) shelter the soil and
    ///   lift none (published in kind, Kok et al. 2012): the wash lifts their loose chaff, straws and leaves instead (ChaffLaw).
    /// - **The flux** past the threshold, Q = 2.61·(ρ/g)·u*³·(1 − r²)·(1 + r), r = u*_t/u* (White 1979, recalled).
    /// </summary>
    public static class DustLaw
    {
        /// <summary>Air's kinematic viscosity, m²/s (published: 15–20 °C).</summary>
        public const double Viscosity = 1.5e-5;

        /// <summary>The bulk density of the loose film on a paved surface, kg/m³: 1300 (assumed, a loose silt's), for its wetting.</summary>
        public const double FilmBulkDensity = 1300;

        /// <summary>The highest gravimetric water %, Fécan's factor is taken to: 10 (assumed: the band the reference note reads the
        /// law over, ×1.5–2.5 at 5–10 %; plants-in-the-air.md §5.1). Wetter ground lifts no dust (DustLaw's summary).</summary>
        public const double FecanMaximum = 10;

        /// <summary>White's constant (published, recalled).</summary>
        public const double White = 2.61;

        /// <summary>One surface's dust: its dry threshold u*_t (m/s), its wall roughness k_s (m), its clay share (%), whether its
        /// loose layer is a film.</summary>
        public struct Row
        {
            public string Name;
            public double Threshold, Roughness, Clay;
            public bool Film;
        }

        /// <summary>The surface table's bare surfaces (plants-in-the-air.md §5.3; the thresholds the bands' middles): crater spoil
        /// 0.14–0.25 and the track's loose film 0.19–0.25 (published, Sharratt and Vaddella 2012's loose loams), the belt's firm
        /// bare loam 0.20–0.35 (assumed between them and a fallow field), ash on burnt ground 0.13–0.38 (published for volcanic
        /// ash), tilled loam 0.35–0.40 (published, a fallow silt loam in place), a crusted field 0.5–1.0 (assumed between
        /// disturbed crusts and loose loam, Belnap and Gillette 1997), rubble's fines 0.25–0.5 and the films on asphalt and slabs
        /// 0.2–0.3 (assumed). k_s 2 mm on soils, 0.5 mm on paving (assumed: rolled soil, the note's §5.2); clay 20 % on loams, 10 %
        /// on spoil-free fines (assumed, Fécan's 10–30 %).</summary>
        public static bool Find(string surface, out Row row)
        {
            switch (surface)
            {
                case "crater_spoil": row = new Row { Name = surface, Threshold = 0.19, Roughness = 0.002, Clay = 20 }; return true;
                case "dirt_track": row = new Row { Name = surface, Threshold = 0.22, Roughness = 0.002, Clay = 20, Film = true }; return true;
                case "belt_bare": row = new Row { Name = surface, Threshold = 0.27, Roughness = 0.002, Clay = 20 }; return true;
                case "burnt_field": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.002, Clay = 20 }; return true;
                case "tilled": row = new Row { Name = surface, Threshold = 0.375, Roughness = 0.002, Clay = 20 }; return true;
                case "dry_crust": row = new Row { Name = surface, Threshold = 0.75, Roughness = 0.002, Clay = 20 }; return true;
                case "rubble": row = new Row { Name = surface, Threshold = 0.37, Roughness = 0.002, Clay = 10, Film = true }; return true;
                case "asphalt": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.0005, Clay = 10, Film = true }; return true;
                case "concrete_slab": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.0005, Clay = 10, Film = true }; return true;
                default: row = default; return false; // covered, or not a surface of the table
            }
        }

        /// <summary>A surface's threshold u*_t (m/s) at the flight's moisture, from its row and the surface table (the wet set's
        /// water content over the surface's dry bulk density, Fécan's factor up to FecanMaximum); +∞ for a surface with no row
        /// (covered), saturated, or wetter than Fécan's range (the wet topsoil's cohesion holds its grains).</summary>
        public static double Threshold(string surface, Moisture moisture, SurfaceTable table, out Row row)
        {
            if (!Find(surface, out row) || moisture == Moisture.Saturated) return double.PositiveInfinity;
            if (moisture == Moisture.Dry) return row.Threshold;
            double bulk = 0;
            foreach (SurfaceRow s in table.Surfaces)
                if (s.Name == surface) bulk = s.Dry.BulkDensity;
            if (!(bulk > 0)) bulk = FilmBulkDensity; // a paved surface: its loose film's
            double water = 100 * table.Wetting.WaterContentWet * table.WaterDensity / bulk;
            if (water > FecanMaximum) return double.PositiveInfinity;
            double wp = 0.0014 * row.Clay * row.Clay + 0.17 * row.Clay;
            if (!(water > wp)) return row.Threshold;
            return row.Threshold * DetMath.Sqrt(1 + 1.21 * DetMath.Exp(0.68 * DetMath.Log(water - wp)));
        }

        /// <summary>u* (m/s) of a wall jet whose peak speed along the ground is peak (m/s) at height (m), radius r from where it lands
        /// and H its source's distance (m), over a wall of roughness k_s (m).</summary>
        public static double Friction(double peak, double height, double radius, double source, double roughness)
        {
            if (!(peak > 0) || !(height > 0)) return 0;
            double delta = height / 0.165;
            double ratio = radius > 1e-3 && source > 0 ? radius / source : 1;
            double cf = 0.06 * DetMath.Exp(-0.3 * DetMath.Log(peak * delta / Viscosity)) * DetMath.Exp(-0.16 * DetMath.Log(ratio));
            double smooth = peak * DetMath.Sqrt(0.5 * cf);
            if (!(height > roughness)) return smooth;
            double rough = peak / (5.75 * DetMath.Log(height / roughness) / 2.302585092994046 + 8.5);
            return rough > smooth ? rough : smooth;
        }

        /// <summary>White's saltation flux, kg/m/s, at u* over a threshold (0 at or under it).</summary>
        public static double Flux(double friction, double threshold, double airDensity)
        {
            if (!(friction > threshold)) return 0;
            double r = threshold / friction;
            return White * airDensity / PlantLaw.Gravity * friction * friction * friction * (1 - r * r) * (1 + r);
        }
    }

    /// <summary>The loose plant bits' size classes (ChaffLaw; w16-plants-in-the-air 1.4a): the index of a class's rate in
    /// WashGround.BitsRate and of its piece in ChaffLaw.Piece.</summary>
    public enum BitClass : byte
    {
        /// <summary>Fine chaff: glumes, husks and broken blade and leaf fragments, about a centimetre.</summary>
        Chaff = 0,
        /// <summary>Straw bits: broken dead culms and straws, a few centimetres long.</summary>
        Straw = 1,
        /// <summary>Dead leaves: whole, dry and curled, a few centimetres across.</summary>
        Leaf = 2,
    }

    /// <summary>One size class's piece (ChaffLaw.Piece), for the drawing and the law: its size (m: Length along it, Width across
    /// it, Thickness through it; a straw bit's Width and Thickness are its diameter), its mass (kg, with the water it holds at
    /// the flight's moisture), its settling speed in still air (m/s, tumbling, as it falls), the wall jet's friction velocity
    /// u* that starts a piece lying in the cover moving (Start, the median over the pieces, m/s; +∞ where none moves) and that
    /// keeps a moving one moving (Keep, m/s), and the pieces' spread about Start (the geometric standard deviation of their
    /// thresholds, ≥ 1).</summary>
    public struct BitPiece
    {
        public double Length, Width, Thickness, Mass, Settling, Start, Keep, Spread;
    }

    /// <summary>
    /// The loose plant bits the props' wash lifts from covered ground (w16-plants-in-the-air 1.4a, design.md §5b;
    /// plants-in-the-air.md §5.4): on sod, weeds, straw and litter the cover shelters the soil (no dust, DustLaw), and the wash
    /// lifts the loose dead pieces lying on it instead (clip H's leaves and straws). Each number's label is the note's §5.4.
    /// - **The pieces** (BitPiece): fine chaff and dead leaves are flat pieces of their mass per area σ; a straw bit is the surface
    ///   table's straw (3.5 mm, its wall share and density, 2.03 g a metre), 4 cm long.
    /// - **What starts one:** the wall jet's friction velocity u* over the cover's roughness (DustLaw.Friction) against the
    ///   piece's hold. A flat piece slides when c·ρu*²·A passes μ·m·g, so u*_t ∝ √σ: the leaves' 0.46 m/s is the Beaufort
    ///   scale's force 4 (dust, leaves and loose paper lifted, 5.5–7.9 m/s at 10 m) over open grass (z₀ 0.03 m), and chaff
    ///   takes it by √(σ_chaff/σ_leaf) (derived). A straw bit rolls out of its groove on the mat when the drag on it at its
    ///   centre's speed (the rough wall's (8.5 + 5.75·log₁₀(y/k_s))·u*, y = d/2 over k_s = d) passes tan 30° of its weight
    ///   (derived: two touching neighbours), at the median of its ways across the flow (|sin θ| = sin 45°). Moving, a piece
    ///   keeps moving down to KeepRatio of that.
    /// - **Wet** (Rain, Severe rain): the piece holds its water (dead leaves and chaff LitterWater of their dry mass, straw the
    ///   surface table's straw_density_wet) and a meniscus of water along its contact with the wet mat holds it by 2γ per metre
    ///   of contact (its rim; a straw's length): u*_t grows by √((m_wet·g + 2γ·contact)/(m·g)). Chaff then needs over 6 m/s, past
    ///   any wash; wet leaves about 2.3 m/s, only at the discs' outwash; wet straw bits about 1.1 m/s, under a low hover.
    /// - **Shelter:** among standing stems (the world's stem field under the node: its stems per m², their drawn heights and
    ///   the cover rows' allometry) the loose pieces feel only the ground's share of u* (Shelter, Raupach et al. 1993); a mat or
    ///   litter with no standing stems shelters nothing.
    /// - **The supply:** each covered surface holds a loose top layer per class, its plan cover φ over the piece's area
    ///   (pieces per m², assumed; the litter's and the straw's published masses bound it). A piece's threshold is spread over
    ///   the pieces log-normally (Spread) about the class's Start, so a node lifts the share F(u*) = Φ(ln(u*/Start)/ln Spread)
    ///   of its supply as its wash first passes u* (the pieces whose thresholds lie under it): what lifted is gone, so a hover
    ///   lifts its whiff and then clears, and more throttle or a lower drone (a higher u*, the wall jet's own scaling) lifts
    ///   more.
    /// </summary>
    public static class ChaffLaw
    {
        /// <summary>The size classes (BitClass).</summary>
        public const int Classes = 3;

        /// <summary>The geometric standard deviation of the pieces' thresholds about their class's Start: 1.5 (assumed: the dry
        /// leaf mass per area's published 30–120 g/m² spread of ×0.7–1.4 on √σ, with each piece's snag and exposure on the mat; the
        /// 5–95 % pieces start at 0.51–1.95 of the median).</summary>
        public const double Spread = 1.5;

        /// <summary>The u* that keeps a moving piece moving over the one that starts it: 0.8 (published for sand grains, Bagnold's
        /// impact threshold against the fluid threshold, restated by Kok et al. 2012; assumed for litter).</summary>
        public const double KeepRatio = 0.8;

        /// <summary>Water's surface tension, N/m (published, 20 °C).</summary>
        public const double SurfaceTension = 0.072;

        /// <summary>A flat piece falling broadside: 1.17 (published, a flat plate normal to the flow, Hoerner 1965, recalled); a
        /// straw across its axis, also its drag lying on the mat: 1.1 (published, a circular cylinder at Re 10²–10³, recalled).</summary>
        public const double PlateDrag = 1.17, CylinderDrag = 1.1;

        /// <summary>The water an air-dry dead leaf or chaff holds, kg per kg dry: 0.1 (assumed, dry weather's litter); wet: 2.0
        /// (published, litter's specific water-holding capacity 2.0–2.1 l per kg oven-dry, whatever the species).</summary>
        public const double LitterAirDry = 0.1, LitterWater = 2.0;

        /// <summary>The dry mass per area of fine chaff, kg/m²: 0.05 (assumed: glumes and dead blades toward the low side of the
        /// published leaf mass per area, 30–120 g/m², Wright et al. 2004); of a dead leaf the surface table's leaf row (0.06).</summary>
        public const double ChaffMassPerArea = 0.05, LeafMassPerArea = 0.06;

        /// <summary>A dry leaf's start, u* m/s: 0.46 (derived: the Beaufort scale's force 4 lifts dust, leaves and loose paper at
        /// 5.5–7.9 m/s 10 m up, published, NWS; over open flat grass, z₀ 0.03 m (published, WMO), u* = 0.4·U/ln(10/z₀) is
        /// 0.38–0.54, and 0.46 its middle).</summary>
        public const double LeafStart = 0.46;

        /// <summary>The straw table's: its diameter (m, the surface table's straw row's note: 3.5 mm), wall share and dry wall
        /// density (kg/m³) as the row has them, and its wet mass factor (straw_density_wet).</summary>
        public const double StrawDiameter = 0.0035, StrawWallShare = 0.12, StrawDensity = 500, StrawWet = 1.3;

        // The pieces' sizes, m (assumed: a glume or a dead blade's bit; a straw bit; a locust or ash leaflet to a small maple
        // leaf, the belts' species).
        const double ChaffLength = 0.008, ChaffWidth = 0.003, ChaffThickness = 0.0002, StrawLength = 0.04,
            LeafLength = 0.05, LeafWidth = 0.03, LeafThickness = 0.0003;

        // Air's density for the settling, kg/m³ (the line's table's, tether.json).
        const double Air = 1.225;

        /// <summary>A class's piece at the flight's moisture (Wet and Saturated alike: the cover table has one wet set).</summary>
        public static BitPiece Piece(BitClass c, Moisture moisture)
        {
            bool wet = moisture != Moisture.Dry;
            double g = PlantLaw.Gravity;
            var p = new BitPiece { Spread = Spread };
            if (c == BitClass.Straw)
            {
                double d = StrawDiameter, inner = d * (1 - 2 * StrawWallShare);
                double line = StrawDensity * PlantLaw.Pi / 4 * (d * d - inner * inner); // kg/m, at the table's ~10 % moisture
                double centre = 8.5 + 5.75 * DetMath.Log(0.5) / 2.302585092994046; // the rough wall's u/u* at d/2 over k_s = d
                double start = DetMath.Sqrt(2 * Tan30 * line * g / (Air * CylinderDrag * d * Sin45)) / centre;
                double m = line * StrawLength, factor = wet ? StrawWet : 1;
                p.Length = StrawLength;
                p.Width = d;
                p.Thickness = d;
                p.Mass = m * factor;
                p.Settling = DetMath.Sqrt(2 * line * factor * g / (Air * CylinderDrag * d));
                p.Start = wet ? start * DetMath.Sqrt((m * factor * g + 2 * SurfaceTension * StrawLength) / (m * g)) : start;
            }
            else
            {
                bool leaf = c == BitClass.Leaf;
                double a = leaf ? LeafLength : ChaffLength, b = leaf ? LeafWidth : ChaffWidth;
                double dry = leaf ? LeafMassPerArea : ChaffMassPerArea, sigma = dry * (1 + (wet ? LitterWater : LitterAirDry));
                double area = PlantLaw.Pi / 4 * a * b, airDry = dry * (1 + LitterAirDry) * area;
                double start = LeafStart * DetMath.Sqrt(dry / LeafMassPerArea);
                p.Length = a;
                p.Width = b;
                p.Thickness = leaf ? LeafThickness : ChaffThickness;
                p.Mass = sigma * area;
                p.Settling = DetMath.Sqrt(2 * sigma * g / (Air * PlateDrag));
                p.Start = wet ? start * DetMath.Sqrt((p.Mass * g + 2 * SurfaceTension * Rim(a, b)) / (airDry * g)) : start;
            }
            p.Keep = KeepRatio * p.Start;
            return p;
        }

        const double Tan30 = 0.57735026918962576, Sin45 = 0.70710678118654752;

        // An ellipse's rim of axes a and b, m (Ramanujan's first approximation).
        static double Rim(double a, double b)
        {
            double x = a / 2, y = b / 2;
            return PlantLaw.Pi * (3 * (x + y) - DetMath.Sqrt((3 * x + y) * (x + 3 * y)));
        }

        /// <summary>A surface's loose pieces of a class lying on its cover, per m² (0 on a surface with none): its loose top
        /// layer's plan cover φ over the piece's area (assumed per surface, design.md §5b): meadow_sod (the root-bound sod's
        /// thatch; the track map's grass) chaff 0.1, straw 0.05; weeds chaff, straw and leaves 0.05 each; belt_straw (the belt's
        /// straw mat) chaff 0.05, straw 0.3, leaves 0.1; yard_litter (leaf litter) chaff 0.1, leaves 0.6.</summary>
        public static double Supply(string surface, BitClass c)
        {
            double chaff = 0, straw = 0, leaf = 0;
            switch (surface)
            {
                case "meadow_sod": chaff = 0.1; straw = 0.05; break;
                case "weeds": chaff = 0.05; straw = 0.05; leaf = 0.05; break;
                case "belt_straw": chaff = 0.05; straw = 0.3; leaf = 0.1; break;
                case "yard_litter": chaff = 0.1; leaf = 0.6; break;
                default: return 0;
            }
            double area = c == BitClass.Straw ? StrawLength * StrawDiameter
                : c == BitClass.Leaf ? PlantLaw.Pi / 4 * LeafLength * LeafWidth : PlantLaw.Pi / 4 * ChaffLength * ChaffWidth;
            return (c == BitClass.Chaff ? chaff : c == BitClass.Straw ? straw : leaf) / area;
        }

        /// <summary>A surface's cover roughness k_s for the wall jet's u* over it, m (0 on a surface with no loose cover): the
        /// lying elements' diameter (assumed, Nikuradse's k_s of the order of a bed's elements): the thatch's blades and the
        /// soil among the weeds 2 mm, the straw mat's straws 3.5 mm, the litter's curled leaves 5 mm.</summary>
        public static double Roughness(string surface)
        {
            switch (surface)
            {
                case "meadow_sod": case "weeds": return 0.002;
                case "belt_straw": return StrawDiameter;
                case "yard_litter": return 0.005;
                default: return 0;
            }
        }

        /// <summary>Raupach et al. 1993's partition of a surface's stress among standing roughness elements: the share of the u*
        /// that reaches the ground between them, [(1 − mσλ)(1 + mβλ)]^(−1/2), m 0.5 (published, the recommended value), β 90
        /// (published, the ratio of an element's drag coefficient to the bare surface's, Raupach et al. 1993's value, recalled), λ
        /// the stems' frontal area per ground area; a stem's σ (its basal over its frontal area, πd/4h) is under 0.01 and its term
        /// left out (derived).</summary>
        public const double ShelterM = 0.5, ShelterBeta = 90;

        /// <summary>The share of the wall's u* that reaches the loose pieces among standing stems of frontal area index λ.</summary>
        public static double Shelter(double frontalIndex) =>
            frontalIndex > 0 ? 1 / DetMath.Sqrt(1 + ShelterM * ShelterBeta * frontalIndex) : 1;

        /// <summary>The share of a class's pieces whose start lies under u* (m/s): Φ(ln(u*/start)/ln Spread); 0 at u* 0 or an
        /// infinite start.</summary>
        public static double Share(double friction, double start)
        {
            if (!(friction > 0) || !(start < double.PositiveInfinity)) return 0;
            return WakeGridMath.Phi(DetMath.Log(friction / start) / LnSpread);
        }

        const double LnSpread = 0.4054651081081644; // ln Spread
    }

    /// <summary>
    /// One wash tick's ground as the drawn side reads it (w16-plants-in-the-air 1.4a; design.md §5b): per node of the grid's
    /// window, the dust (DustLaw) and the loose plant bits (ChaffLaw) the wash lifts there. The thread that ticks the grid fills
    /// one and hands it whole to the render thread through PlantWash.TakeGround (a triple buffer: the ticking thread never
    /// waits, and a snapshot taken stays unchanged until the taking thread's next TakeGround). Three are made with the grid;
    /// nothing is allocated after.
    /// - **Layout:** Size × Size nodes. Node (a, b) is the world cell (StartI + a, StartJ + b) of Cell m, its centre at world
    ///   X = (StartI + a + 0.5)·Cell, Z = (StartJ + b + 0.5)·Cell, its index a + Size·b (X fastest, PlantField's order). The
    ///   window is the grid's at the tick, about the drone by whole cells.
    /// - **Per node:** Surface (IWorldQuery's id, 0 where there is no ground); the dust: DustFriction u* (m/s, the wall jet's
    ///   over the bare soil's roughness), DustThreshold u*_t (m/s, +∞ where none lifts) and DustFlux (kg/m/s, White's
    ///   saltation flux, &gt; 0 where dust lifts); the bits: BitsFriction u* (m/s, the u* the loose pieces feel: the wall jet's
    ///   over the cover's roughness, times the standing stems' shelter; 0 on a surface with no loose cover). Compare a landed
    ///   drawn piece's ChaffLaw.Piece(…).Keep with it. Each u* is 0 where the wash is under PlantWash.MinWash.
    /// - **Per node and class:** BitsRate[index·ChaffLaw.Classes + (int)class], the pieces of that class the wash lifts there,
    ///   per m² of ground per second, over the tick that ended at Tick. The pieces a cell lifts over a drawn frame of dt s are
    ///   BitsRate·Cell²·dt; each piece's size, mass, settling speed and the u* that keeps it moving are ChaffLaw.Piece(class,
    ///   the flight's moisture).
    /// </summary>
    public sealed class WashGround
    {
        /// <summary>The tick's flight step (−1 before the first), its length (s).</summary>
        public long Tick = -1;
        public double TickSeconds;

        /// <summary>The window: the world cell of node (0, 0), the nodes a side, the cell (m).</summary>
        public long StartI, StartJ;
        public readonly int Size;
        public readonly double Cell;

        /// <summary>Per node (index a + Size·b).</summary>
        public readonly byte[] Surface;
        public readonly double[] DustFriction, DustThreshold, DustFlux, BitsFriction;

        /// <summary>Per node and class (index (a + Size·b)·ChaffLaw.Classes + class): pieces per m² per s.</summary>
        public readonly double[] BitsRate;

        readonly bool[] _film;

        internal WashGround(int size, double cell, bool[] film)
        {
            Size = size;
            Cell = cell;
            _film = film;
            int n = size * size;
            Surface = new byte[n];
            DustFriction = new double[n];
            DustThreshold = new double[n];
            DustFlux = new double[n];
            BitsFriction = new double[n];
            BitsRate = new double[n * ChaffLaw.Classes];
        }

        /// <summary>The node under world (x, z) (the cell it lies in), false outside the window or before the first tick.</summary>
        public bool Node(double x, double z, out int node)
        {
            node = -1;
            if (Tick < 0) return false;
            long a = FloorL(x / Cell) - StartI, b = FloorL(z / Cell) - StartJ;
            if (a < 0 || a >= Size || b < 0 || b >= Size) return false;
            node = (int)(a + Size * b);
            return true;
        }

        /// <summary>The dust at world (x, z), as PlantWash.DustAt gives it on the ticking thread. False outside the window.</summary>
        public bool DustAt(double x, double z, out DustSample dust)
        {
            dust = default;
            if (!Node(x, z, out int s)) return false;
            dust.Friction = DustFriction[s];
            dust.Threshold = DustThreshold[s];
            dust.Flux = DustFlux[s];
            dust.Lifts = DustFlux[s] > 0;
            dust.Film = _film[Surface[s]];
            dust.Surface = Surface[s];
            return true;
        }

        /// <summary>A class's lift rate at node (pieces per m² per s).</summary>
        public double Rate(int node, BitClass c) => BitsRate[node * ChaffLaw.Classes + (int)c];

        static long FloorL(double v)
        {
            long t = (long)v;
            return t > v ? t - 1 : t;
        }
    }

    /// <summary>
    /// The props' wash on the ground's plants and dust (w16-plants-in-the-air design.md §4–§5): a world-anchored grid of nodes
    /// around the drone, each a stand of stems of every band the drawn side asks for, integrated in the rotors' air at a fixed
    /// tick of the flight's time. It runs on the drawn side's frame or a helper, never in the flight's step; it reads the poses
    /// the flight publishes (PoseBuffer), so a replay drives it the same way.
    /// - **The air:** the line's law (RotorAir, p7d-tether design 19: the near columns, the merged jet, the wall jets, the fountain
    ///   and the ground vortex) in an instance of its own, begun each tick with the line's inputs from the pose: the hubs, the
    ///   rotors' mean thrust, the field's air at each hub less its velocity, the ground under each. The drone's body over the
    ///   fountain is left out (it shapes only the air within a body's depth under it, where stems meet the legs: 1.5's).
    /// - **Each node** samples the air on its vertical at Heights over its ground (the near jets' top-hat edge straddled), and each
    ///   band's stand steps by PlantLaw (Profile: each element at its own height, the drag on the air relative to it), from its
    ///   natural lean (PropStems' mean, its way by the node's world hash). The wash's bend is the stand's lean less its rest.
    /// - **The window** follows the drone by whole cells; a node entering it starts at rest, one leaving it is dropped (what it
    ///   held is lost: the window's size bounds the trail a fast low pass leaves; design.md §4.4).
    /// - **Dust** per node from its lowest samples (DustLaw), for the surface under it at the flight's moisture; and the loose
    ///   plant bits from covered ground (ChaffLaw), from the same samples over the cover's roughness.
    /// - **The drawn side** takes each tick's dust and bits whole, on another thread, through TakeGround (WashGround).
    /// Deterministic (DetMath only, a fixed tick, the world's own hash), allocation-free after the constructor.
    /// </summary>
    public sealed class PlantWash
    {

        /// <summary>A wash weaker than this where it lands (m/s: the merged jet's landing speed or a near column's) moves no plant
        /// visibly (published: no visible change in a crop under 3 m/s at the canopy, the note's [21]): the tick skips the air (the
        /// stands relax in still air), from about 12 m up for this drone's hover.</summary>
        public const double MinWash = 3;

        /// <summary>The stands' steps a tick (numerical: 20 ms each at the 40 ms tick, the air held over the tick; a culm's fast
        /// swing back after a gust stays within 0.02 rad of the full law at 20 ms, 0.08 at 40, PlantAirTests).</summary>
        public const int SubSteps = 2;

        /// <summary>The highest sample read for dust, m (numerical: above the wall jet's peak at any hover of interest).</summary>
        const double DustTop = 0.1;

        readonly RotorAir _air;
        readonly PlantAir _wind;
        readonly IWorldQuery _world;
        readonly Double3[] _motors;
        readonly PlantMechanics[] _bands;
        // The heights the air is sampled at over a node's ground, m: fine at the wall (the wall jet's peak lies at 0.165·δ,
        // 1–6 cm here), the near jets' top-hat edge h_j straddled by ±2 %, then to the tallest drawn stems (numerical); and each
        // band's rest.
        readonly double[] _heights, _rest;
        readonly int[] _bandHeights;
        readonly int _size, _tickSteps, _heightCount;
        readonly double _cell, _dt, _density, _jetOrigin, _wallRatio, _wallExponent;

        // Per slot (size² of them): the world cell held, its ground and surface, its stand's natural way, its air (Heights) this
        // tick, whether its air is nil, its dust; per slot and band the stand's state now and at the tick before.
        readonly int[] _ci, _cj;
        readonly bool[] _held, _still;
        readonly double[] _ground, _wayX, _wayZ, _friction, _flux, _threshold;
        readonly byte[] _surface;
        readonly Double3[] _samples;
        readonly PlantState[] _now, _was;

        // Per slot the bits' u* this tick and the highest since the node entered the window (what it has lifted), the standing
        // stems' shelter (the share of the wall's u* the pieces feel), and per slot and class their lift rate this tick
        // (pieces/m²/s).
        readonly double[] _bitsFriction, _felt, _shelter, _bitsRate;

        // The stems' 1 m diameter by StemKind (the cover rows' diameter_m), and the stem query's buffer.
        readonly double[] _stemDiameter = new double[4];
        readonly StemPatch[] _patches = new StemPatch[64];

        // The surfaces' dust (by IWorldQuery's id).
        readonly double[] _surfaceThreshold = new double[256], _surfaceRoughness = new double[256];
        readonly bool[] _surfaceFilm = new bool[256];

        // The surfaces' bits (by id): the cover's roughness, per class the supply (pieces/m²); per class the start at the
        // flight's moisture.
        readonly double[] _coverRoughness = new double[256], _supply = new double[256 * ChaffLaw.Classes], _start = new double[ChaffLaw.Classes];

        // The ground's snapshots for the drawn side (a triple buffer: the ticking thread fills the back one and swaps it with
        // the ready one; TakeGround swaps the ready one with the front one).
        WashGround _groundBack, _groundReady, _groundFront;
        bool _groundFresh, _groundShown;
        readonly object _groundLock = new object();

        // The tick's inputs.
        readonly Double3[] _hub = new Double3[4], _relative = new Double3[4];
        readonly double[] _thrust = new double[4], _hubGround = new double[4];
        readonly XZ[] _xz = new XZ[4], _one = new XZ[1];
        readonly GroundSample[] _hubSample = new GroundSample[4], _oneSample = new GroundSample[1];
        long _tick = -1;
        int _startI, _startJ;
        bool _on;
        double _sourceDistance;
        Double3 _centre;

        /// <param name="table">The line's table (its rotor_air rows and air density).</param>
        /// <param name="radius">The props' radius, m.</param>
        /// <param name="motors">The hubs in the body frame (drone.json's motors), Corners' order.</param>
        /// <param name="bands">The stems the drawn side draws (PlantMechanics.Stem), integrated at every node.</param>
        /// <param name="moisture">The flight's ground moisture (SurfaceTable.MoistureOf its rain).</param>
        /// <param name="size">Nodes a side (even).</param>
        /// <param name="cell">The nodes' spacing, m.</param>
        /// <param name="tickSteps">The tick, flight steps (40: 25 Hz; the drawn side reads between the last two ticks).</param>
        public PlantWash(TetherTable table, double radius, Double3[] motors, IWorldQuery world, PlantAir wind, PlantMechanics[] bands,
            SurfaceTable surfaces, Moisture moisture, int size = 32, double cell = 0.15, int tickSteps = 40)
        {
            if (motors == null || motors.Length != 4) throw new ArgumentException("four motors", nameof(motors));
            if (size < 4 || size % 2 != 0 || !(cell > 0) || tickSteps < 1) throw new ArgumentException("the grid is out of its bounds");
            _air = new RotorAir(table, radius);
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _motors = (Double3[])motors.Clone();
            _bands = (PlantMechanics[])bands.Clone();
            _size = size;
            _cell = cell;
            _tickSteps = tickSteps;
            _dt = tickSteps * FlightCore.Dt;
            _density = table.Number("fiber.air_density_kg_m3");
            _jetOrigin = table.Number("rotor_air.far_jet_virtual_origin_over_motor_distance");
            _wallRatio = table.Number("rotor_air.wall_jet_half_height_ratio");
            _wallExponent = table.Number("rotor_air.wall_jet_half_height_exponent");
            double hj = _air.JetThickness, top = DustTop;
            foreach (PlantMechanics b in _bands) top = DetMath.Max(top, b.DragTo);
            _heights = new[] { 0.008, 0.02, 0.98 * hj, 1.02 * hj, 0.08, 0.15, 0.27, 0.45, 0.7, 1.0, 1.4, 1.9 };
            Array.Sort(_heights);
            int count = 0;
            while (count < _heights.Length && (count == 0 || _heights[count - 1] < top)) count++;
            _heightCount = count;
            _bandHeights = new int[_bands.Length];
            _rest = new double[_bands.Length];
            for (int b = 0; b < _bands.Length; b++)
            {
                int k = 0;
                while (k < _heightCount && (k == 0 || _heights[k - 1] < _bands[b].DragTo)) k++;
                _bandHeights[b] = k;
                _rest[b] = PlantLaw.Rest(_bands[b], _bands[b].NaturalLean);
            }
            int n = size * size;
            _ci = new int[n];
            _cj = new int[n];
            _held = new bool[n];
            _still = new bool[n];
            _ground = new double[n];
            _wayX = new double[n];
            _wayZ = new double[n];
            _friction = new double[n];
            _flux = new double[n];
            _threshold = new double[n];
            _surface = new byte[n];
            _samples = new Double3[n * _heightCount];
            _now = new PlantState[n * _bands.Length];
            _was = new PlantState[n * _bands.Length];
            _bitsFriction = new double[n];
            _felt = new double[n];
            _shelter = new double[n];
            _bitsRate = new double[n * ChaffLaw.Classes];
            foreach (CoverRow row in surfaces.Cover) // StemKind k is cover kind GrassBlade + k − 1 (PropStems')
            {
                int k = (int)row.Kind - (int)CoverKind.GrassBlade + 1;
                if (k >= 1 && k < _stemDiameter.Length) _stemDiameter[k] = row.Dry.Diameter;
            }
            _groundBack = new WashGround(size, cell, _surfaceFilm);
            _groundReady = new WashGround(size, cell, _surfaceFilm);
            _groundFront = new WashGround(size, cell, _surfaceFilm);
            for (int s = 0; s < 256; s++) _surfaceThreshold[s] = double.PositiveInfinity;
            for (int s = 1; s < 256; s++)
            {
                string name = world.SurfaceName((byte)s);
                if (name == null) continue;
                _surfaceThreshold[s] = DustLaw.Threshold(name, moisture, surfaces, out DustLaw.Row row);
                _surfaceRoughness[s] = row.Roughness;
                _surfaceFilm[s] = row.Film;
                _coverRoughness[s] = ChaffLaw.Roughness(name);
                for (int c = 0; c < ChaffLaw.Classes; c++) _supply[s * ChaffLaw.Classes + c] = ChaffLaw.Supply(name, (BitClass)c);
            }
            for (int c = 0; c < ChaffLaw.Classes; c++) _start[c] = ChaffLaw.Piece((BitClass)c, moisture).Start;
        }

        /// <summary>The bands' count, and the flight step of the last tick (−1 before the first).</summary>
        public int Bands => _bands.Length;
        public long LastTick => _tick;

        /// <summary>The tick's length, s.</summary>
        public double Tick => _dt;

        /// <summary>The heights the air is sampled at, m over a node's ground (the first count of them in use).</summary>
        public ReadOnlySpan<double> Heights => new ReadOnlySpan<double>(_heights, 0, _heightCount);

        /// <summary>The wash's own instance of the line's law, as the last tick began it.</summary>
        public RotorAir Rotors => _air;

        /// <summary>
        /// Ticks to the newest step the flight has published (every tickSteps steps of its time), each from the pose of its own
        /// step in the history. A render that fell behind the history (128 steps) resumes at the newest tick: the steps between
        /// are skipped (the stands keep their state).
        /// </summary>
        public void Advance(PoseBuffer poses)
        {
            long newest = poses.LatestStep;
            if (newest < 0) return;
            long next = _tick < 0 ? newest - newest % _tickSteps : _tick + _tickSteps;
            if (newest - next >= PoseBuffer.HistorySteps) next = newest - newest % _tickSteps;
            for (; next <= newest; next += _tickSteps)
            {
                if (!poses.TryRead(next, out PoseFrame pose)) continue;
                Advance(pose);
            }
        }

        /// <summary>One tick at the pose's step (the caller keeps the ticks tickSteps apart).</summary>
        public void Advance(in PoseFrame pose)
        {
            double t = pose.Step * FlightCore.Dt;
            Quat q = pose.Attitude;
            Double3 up = q.Rotate(new Double3(0, 1, 0));
            for (int i = 0; i < 4; i++)
            {
                _hub[i] = pose.Position + q.Rotate(_motors[i]);
                _xz[i] = new XZ(_hub[i].X, _hub[i].Z);
            }
            _world.SampleGround(_xz, _hubSample);
            for (int i = 0; i < 4; i++)
            {
                double g = _hubSample[i].Height;
                _hubGround[i] = g;
                Double3 v = pose.Velocity + q.Rotate(Double3.Cross(pose.BodyRate, _motors[i]));
                _relative[i] = (g > double.NegativeInfinity ? _wind.AirAt(_hub[i], g, t) : default) - v;
                _thrust[i] = pose.RotorThrust[i];
            }
            _air.Begin(_hub, up, _thrust, _relative, _hubGround);
            double strength = _air.FarLands ? _air.FarGroundSpeed * _air.FarLandingShare : 0, columns = 0;
            for (int i = 0; i < 4; i++) columns = DetMath.Max(columns, 2 * _air.Induced(i) * _air.NearShare);
            _on = strength > MinWash || columns > MinWash;
            double mean = 0;
            for (int i = 0; i < 4; i++) mean += 0.25 * (_hub[i].Y - _hubGround[i]);
            _sourceDistance = _air.FarLands ? mean - _jetOrigin * _air.MotorDistance : mean;
            _centre = _air.FarLands ? _air.FarLanding : pose.Position;
            Recentre(pose.Position);
            for (int s = 0; s < _size * _size; s++) Node(s);
            _tick = pose.Step;
            PublishGround();
        }

        // The tick's dust and bits to the back snapshot in window order, then swapped in as the ready one.
        void PublishGround()
        {
            WashGround g = _groundBack;
            g.Tick = _tick;
            g.TickSeconds = _dt;
            g.StartI = _startI;
            g.StartJ = _startJ;
            for (int b = 0; b < _size; b++)
                for (int a = 0; a < _size; a++)
                {
                    int s = Slot(_startI + a, _startJ + b), i = a + _size * b;
                    bool ground = _ground[s] > double.NegativeInfinity;
                    g.Surface[i] = ground ? _surface[s] : (byte)0;
                    g.DustFriction[i] = _friction[s];
                    g.DustThreshold[i] = _threshold[s];
                    g.DustFlux[i] = _flux[s];
                    g.BitsFriction[i] = _bitsFriction[s];
                    for (int c = 0; c < ChaffLaw.Classes; c++) g.BitsRate[i * ChaffLaw.Classes + c] = _bitsRate[s * ChaffLaw.Classes + c];
                }
            lock (_groundLock)
            {
                (_groundBack, _groundReady) = (_groundReady, _groundBack);
                _groundFresh = true;
            }
        }

        /// <summary>
        /// The render thread, once a drawn frame: the newest finished tick's dust and bits at or before the frame's flight time
        /// (s; the time PlantAir.Begin takes), null before the first. The snapshot stays unchanged until this thread's next call;
        /// the thread that ticks the grid (Advance) never waits on it. One thread takes, one ticks.
        /// </summary>
        public WashGround TakeGround(double time)
        {
            lock (_groundLock)
            {
                if (_groundFresh && _groundReady.Tick * FlightCore.Dt <= time + 1e-9)
                {
                    (_groundReady, _groundFront) = (_groundFront, _groundReady);
                    _groundFresh = false;
                    _groundShown = true;
                }
            }
            return _groundShown ? _groundFront : null;
        }

        // The window by whole cells about the drone: a cell newly in it takes a free slot, its ground and its stands at rest.
        void Recentre(in Double3 position)
        {
            int ci = Floor(position.X / _cell), cj = Floor(position.Z / _cell), half = _size / 2;
            _startI = ci - half;
            _startJ = cj - half;
            for (int a = 0; a < _size; a++)
                for (int b = 0; b < _size; b++)
                {
                    int wi = _startI + a, wj = _startJ + b, s = Slot(wi, wj);
                    if (_held[s] && _ci[s] == wi && _cj[s] == wj) continue;
                    Enter(s, wi, wj);
                }
        }

        void Enter(int s, int wi, int wj)
        {
            _held[s] = true;
            _ci[s] = wi;
            _cj[s] = wj;
            _one[0] = new XZ((wi + 0.5) * _cell, (wj + 0.5) * _cell);
            _world.SampleGround(_one, _oneSample);
            _ground[s] = _oneSample[0].Height;
            _surface[s] = _oneSample[0].Surface;
            _threshold[s] = _ground[s] > double.NegativeInfinity ? _surfaceThreshold[_surface[s]] : double.PositiveInfinity;
            // The stand's natural way by the cell's world hash (world-derived, as PropStems' stems).
            ulong h = FlightRandom.Mix64(unchecked(((ulong)(uint)wi << 32) ^ (uint)wj ^ 0x9E3779B97F4A7C15UL));
            DetMath.SinCos(PlantLaw.TwoPi * ((h >> 11) * (1.0 / 9007199254740992.0)), out double sin, out double cos);
            _wayX[s] = cos;
            _wayZ[s] = sin;
            _still[s] = false;
            for (int k = 0; k < _heightCount; k++) _samples[s * _heightCount + k] = default;
            for (int b = 0; b < _bands.Length; b++)
            {
                var rest = new PlantState { X = _rest[b] * cos, Z = _rest[b] * sin };
                _now[s * _bands.Length + b] = rest;
                _was[s * _bands.Length + b] = rest;
            }
            _friction[s] = 0;
            _flux[s] = 0;
            _bitsFriction[s] = 0;
            _felt[s] = 0;
            for (int c = 0; c < ChaffLaw.Classes; c++) _bitsRate[s * ChaffLaw.Classes + c] = 0;
            _shelter[s] = ChaffLaw.Shelter(FrontalIndex(_one[0]));
        }

        // The standing stems' frontal area per ground area over a cell (the world's stem field in the circle of its area): each
        // layer's stems per m² by its drawn tops' middle height h and the kind's allometry, d = d₁·h^(1/1.53).
        double FrontalIndex(XZ centre)
        {
            double r = _cell * 0.56418958354775628, circle = PlantLaw.Pi * r * r, lambda = 0; // r = cell/√π
            int count = _world.StemsNear(centre, r, 0, _patches);
            if (count > _patches.Length) count = _patches.Length;
            for (int p = 0; p < count; p++)
            {
                StemPatch patch = _patches[p];
                double d1 = (int)patch.Kind < _stemDiameter.Length ? _stemDiameter[(int)patch.Kind] : 0, h = 0.5 * (patch.HeightMin + patch.HeightMax);
                if (!(d1 > 0) || !(h > 0)) continue;
                lambda += patch.Density * patch.Area / circle * d1 * DetMath.Exp(PropStrikes.StemAllometry * DetMath.Log(h)) * h;
            }
            return lambda;
        }

        // One node's tick: its air, each band's stand, its dust.
        void Node(int s)
        {
            double ground = _ground[s];
            bool hasGround = ground > double.NegativeInfinity;
            int k0 = s * _heightCount;
            if (_on && hasGround)
            {
                double x = (_ci[s] + 0.5) * _cell, z = (_cj[s] + 0.5) * _cell;
                for (int k = 0; k < _heightCount; k++) _samples[k0 + k] = _air.At(new Double3(x, ground + _heights[k], z), ground, out _);
                _still[s] = false;
            }
            else if (!_still[s])
            {
                for (int k = 0; k < _heightCount; k++) _samples[k0 + k] = default;
                _still[s] = true;
            }
            if (!_still[s]) Array.Copy(_samples, k0, _scratch, 0, _heightCount); // the step reads the node's block from 0
            for (int b = 0; b < _bands.Length; b++)
            {
                int i = s * _bands.Length + b;
                PlantState st = _now[i];
                _was[i] = st;
                if (!hasGround) continue;
                double nx = _bands[b].NaturalLean * _wayX[s], nz = _bands[b].NaturalLean * _wayZ[s];
                if (_still[s])
                {
                    double rx = _rest[b] * _wayX[s], rz = _rest[b] * _wayZ[s];
                    double dx = st.X - rx, dz = st.Z - rz;
                    if (dx * dx + dz * dz < 1e-14 && st.RateX * st.RateX + st.RateZ * st.RateZ < 1e-12) continue; // at rest
                }
                for (int sub = 0; sub < SubSteps; sub++)
                {
                    if (_still[s]) PlantLaw.Step(_bands[b], ref st, nx, nz, default(Double3), _dt / SubSteps);
                    else PlantLaw.Step(_bands[b], ref st, nx, nz, _heights, _scratch, _bandHeights[b], _dt / SubSteps);
                }
                _now[i] = st;
            }
            Dust(s);
        }

        readonly Double3[] _scratch = new Double3[16];

        // The node's u* from its lowest samples' peak speed along the ground, at the wall jet's own peak height 0.165·δ: the merged
        // jet's δ = c·R·(R/H)^e, R = max(r, r_i) (RotorAir's law, the table's rows) where it carries the landing (its share ≥ ½),
        // else the near jets' top-hat, its thickness taken as its δ (assumed).
        void Dust(int s)
        {
            _friction[s] = 0;
            _flux[s] = 0;
            _bitsFriction[s] = 0;
            for (int c = 0; c < ChaffLaw.Classes; c++) _bitsRate[s * ChaffLaw.Classes + c] = 0;
            if (_still[s]) return; // u* everywhere the wash blows (1.4 may use it), the flux only past a finite threshold
            double peak = 0;
            int k0 = s * _heightCount;
            for (int k = 0; k < _heightCount && _heights[k] <= DustTop; k++)
            {
                Double3 u = _samples[k0 + k];
                double h = DetMath.Sqrt(u.X * u.X + u.Z * u.Z);
                if (h > peak) peak = h;
            }
            double x = (_ci[s] + 0.5) * _cell - _centre.X, z = (_cj[s] + 0.5) * _cell - _centre.Z, r = DetMath.Sqrt(x * x + z * z);
            double height = 0.165 * _air.JetThickness;
            if (_air.FarLands && _air.FarLandingShare >= 0.5)
            {
                double big = r > _air.FarStart ? r : _air.FarStart;
                height = 0.165 * _wallRatio * big * DetMath.Exp(_wallExponent * DetMath.Log(big / _sourceDistance));
            }
            double f = DustLaw.Friction(peak, height, r, _sourceDistance, _surfaceRoughness[_surface[s]]);
            _friction[s] = f;
            _flux[s] = DustLaw.Flux(f, _threshold[s], _density);
            Bits(s, peak, height, r);
        }

        // The node's loose bits (ChaffLaw): the same wall jet's u* over the cover's roughness; per class the share of the supply
        // whose starts its u* passes for the first time since the node entered the window, over the tick.
        void Bits(int s, double peak, double height, double r)
        {
            int surface = _surface[s];
            double k = _coverRoughness[surface];
            if (!(k > 0) || !(_ground[s] > double.NegativeInfinity)) return; // no loose cover
            double f = _shelter[s] * DustLaw.Friction(peak, height, r, _sourceDistance, k), felt = _felt[s];
            _bitsFriction[s] = f;
            if (!(f > felt)) return;
            for (int c = 0; c < ChaffLaw.Classes; c++)
            {
                double supply = _supply[surface * ChaffLaw.Classes + c];
                if (!(supply > 0)) continue;
                double lifted = ChaffLaw.Share(f, _start[c]) - ChaffLaw.Share(felt, _start[c]);
                _bitsRate[s * ChaffLaw.Classes + c] = lifted > 0 ? supply * lifted / _dt : 0;
            }
            _felt[s] = f;
        }

        static int Floor(double v)
        {
            int t = (int)v;
            return t > v ? t - 1 : t;
        }

        int Slot(int wi, int wj)
        {
            int a = wi % _size, b = wj % _size;
            if (a < 0) a += _size;
            if (b < 0) b += _size;
            return a + _size * b;
        }

        // The node at world cell (wi, wj) when the window holds it, else −1.
        int Held(int wi, int wj)
        {
            if (wi < _startI || wi >= _startI + _size || wj < _startJ || wj >= _startJ + _size) return -1;
            int s = Slot(wi, wj);
            return _held[s] && _ci[s] == wi && _cj[s] == wj ? s : -1;
        }

        /// <summary>
        /// A band's wash bend at world (x, z): its stand's lean less its rest, between the last two ticks at fraction (0 the tick
        /// before, 1 the last), bilinear between the four nodes about it, and the wash's air at the band's load point (world m/s,
        /// for the wind's linearisation, PlantAir.Sample). False outside the window (no wash part).
        /// </summary>
        public bool Bend(int band, double x, double z, double fraction, out PlantBend bend, out Double3 washAtLoad)
        {
            bend = default;
            washAtLoad = default;
            if (_tick < 0 || band < 0 || band >= _bands.Length) return false;
            double u = x / _cell - 0.5, v = z / _cell - 0.5;
            int i0 = Floor(u), j0 = Floor(v);
            double fu = u - i0, fv = v - j0;
            double load = _bands[band].LoadPoint;
            for (int c = 0; c < 4; c++)
            {
                int s = Held(i0 + (c & 1), j0 + (c >> 1));
                if (s < 0) { bend = default; washAtLoad = default; return false; }
                double w = ((c & 1) == 0 ? 1 - fu : fu) * ((c >> 1) == 0 ? 1 - fv : fv);
                PlantState a = _was[s * _bands.Length + band], b = _now[s * _bands.Length + band];
                double rx = _rest[band] * _wayX[s], rz = _rest[band] * _wayZ[s];
                bend.X += w * (a.X + (b.X - a.X) * fraction - rx);
                bend.Z += w * (a.Z + (b.Z - a.Z) * fraction - rz);
                bend.RateX += w * (a.RateX + (b.RateX - a.RateX) * fraction);
                bend.RateZ += w * (a.RateZ + (b.RateZ - a.RateZ) * fraction);
                washAtLoad = washAtLoad + AirAt(s, load) * w;
            }
            return true;
        }

        // A node's air at a height over its ground (linear between the samples, the nearest outside them).
        Double3 AirAt(int s, double height)
        {
            int k0 = s * _heightCount;
            if (!(height > _heights[0])) return _samples[k0];
            for (int k = 1; k < _heightCount; k++)
                if (height <= _heights[k])
                {
                    double t = (height - _heights[k - 1]) / (_heights[k] - _heights[k - 1]);
                    return _samples[k0 + k - 1] + (_samples[k0 + k] - _samples[k0 + k - 1]) * t;
                }
            return _samples[k0 + _heightCount - 1];
        }

        /// <summary>The dust at world (x, z): the node's under it (nearest, the last tick's). False outside the window.</summary>
        public bool DustAt(double x, double z, out DustSample dust)
        {
            dust = default;
            int s = _tick < 0 ? -1 : Held(Floor(x / _cell), Floor(z / _cell));
            if (s < 0) return false;
            dust.Friction = _friction[s];
            dust.Threshold = _threshold[s];
            dust.Flux = _flux[s];
            dust.Lifts = _flux[s] > 0;
            dust.Film = _surfaceFilm[_surface[s]];
            dust.Surface = _surface[s];
            return true;
        }
    }
}
