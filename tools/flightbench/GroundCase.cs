using System;
using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // w9-village-physics 2.9's harness (p7a-ground design 13, `--ground [seeds]`): liftoffs on Village's ground classes and
    // slopes and on House's grounds (the test range), dry, in Rain and in Severe rain, the flight seeds 1–N (odd a vertical
    // touch, even the glide), each read for design 13's lone held corner and for the held group (LiftoffScenarios); the
    // pilot's account as design 13's targets and its four known-wrong models; then 2.9's other rows: the slope landing and
    // rest on the audit's Village spots against W3's packages, and the soft-soil landing on Village's tilled against a floor.
    internal static class GroundCase
    {
        sealed class Spot
        {
            public string Name, Surface;
            public bool House, Firm;
            public Func<FlightWorld> Open;
            public Double3 Foot;
            public double Yaw, Hover;
        }

        struct Cell
        {
            public int Flights, Lone, Group, Never, Crashes, Tunnels, Thrown;
            public double LoneHold, LoneTurn, Sink;

            public double Rate => Flights > 0 ? (double)Lone / Flights : 0;
            public double GroupRate => Flights > 0 ? (double)Group / Flights : 0;

            public void Add(in Cell o)
            {
                Flights += o.Flights; Thrown += o.Thrown; Lone += o.Lone; Group += o.Group; Never += o.Never; Crashes += o.Crashes; Tunnels += o.Tunnels;
                LoneHold = Math.Max(LoneHold, o.LoneHold); LoneTurn = Math.Max(LoneTurn, o.LoneTurn); Sink += o.Sink;
            }
        }

        static readonly RainLevel[] Weathers = { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain };

        // --nominal-legs: every leg the nominal tube (no leg.stiffness spread), the model before 2.9 for the before-and-after.
        static Action<FlightSetup> _edit;
        static bool _ok = true;

        static void Row(string name, bool pass, string detail)
        {
            _ok &= pass;
            Console.WriteLine($"  {name}: {detail}: {(pass ? "PASS" : "FAIL")}");
        }

        static DroneSpec Spec() => DroneSpec.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static Spot Village(MapWorld map, string name, double x, double z, bool firm)
        {
            GroundSample g = SlopeScenarios.Ground(map, x, z);
            double uphill = Math.Atan2(-g.Normal.X, -g.Normal.Z) * 57.29577951308232;
            return new Spot
            {
                Name = $"Village {name} ({x:F2}, {z:F2}) {SlopeScenarios.FacetDeg(g):F2}°", Surface = map.SurfaceName(g.Surface), Firm = firm,
                Open = () => new FlightWorld(map), Foot = new Double3(x, g.Height, z), Yaw = SlopeScenarios.FacetDeg(g) > 2 ? uphill : 0,
            };
        }

        static List<Spot> Spots()
        {
            MapWorld farm = SlopeScenarios.Farmland(), range = SoilScenarios.Range();
            var s = new List<Spot>
            {
                Village(farm, "tilled", 715.970, 105.470, false),
                Village(farm, "meadow_sod", 322.290, 32.580, false),
                Village(farm, "weeds", 26.660, 20.000, false),
                Village(farm, "dry_crust", 714.120, 106.580, true),
                Village(farm, "dirt_track", 590.170, 108.800, true),
                Village(farm, "weeds 10°", 313.040, 20.000, false),
                Village(farm, "meadow_sod 10°", 311.190, 50.710, false),
            };
            // The S04 patch on one mat piece (SoilScenarios.PatchMat): its centre lies on a piece's edge, a foot over the 0.125 m step
            // to the soil box, so a row there flew the fall into the step (w9-village-physics 2.6's addendum §3).
            Double3 track = SoilScenarios.TrackStart(new FlightWorld(range)), patch = SoilScenarios.PatchMat(new FlightWorld(range));
            s.Add(new Spot { Name = "House track start", Surface = "belt_straw", House = true, Open = () => new FlightWorld(range), Foot = track });
            s.Add(new Spot { Name = "House S04 patch", Surface = "straw over belt_straw", House = true, Open = () => new FlightWorld(range), Foot = patch });
            return s;
        }

        static Cell Fly(DroneSpec spec, Spot p, RainLevel rain, int seeds, SurfaceTable table = null, bool rigid = false, bool print = false)
        {
            var c = new Cell();
            for (int i = 1; i <= seeds; i++)
            {
                LiftoffScenarios.Liftoff r;
                try
                {
                    r = LiftoffScenarios.Fly(spec, p.Open(), p.Foot, p.Hover, rain, (ulong)i, i % 2 == 0, p.Yaw, table, rigid, _edit);
                }
                catch (Exception e)
                {
                    // A step that throws is a physics bug: counted, named, and the cell flies on.
                    c.Thrown++;
                    Console.WriteLine($"      seed {i} {rain}: the step threw {e.GetType().Name} at {e.StackTrace?.Split((char)10)[0].Trim()}");
                    continue;
                }
                c.Flights++;
                if (double.IsNaN(r.Off)) c.Never++;
                if (r.Lone >= 0)
                {
                    c.Lone++;
                    c.LoneHold = Math.Max(c.LoneHold, r.LoneHold);
                    c.LoneTurn = Math.Max(c.LoneTurn, r.LoneTurn);
                }
                if (r.Group > 0) c.Group++;
                c.Crashes += r.Crashes;
                c.Tunnels += r.Tunnels;
                c.Sink += (r.Sink[0] + r.Sink[1] + r.Sink[2] + r.Sink[3]) / 4;
                if (print)
                    Console.WriteLine($"      seed {i} {(i % 2 == 0 ? "glide" : "vert ")}: off {r.Off,6:F3} s at {r.OffThrottle:F3}; lone {(r.Lone >= 0 ? LegDamage.Names[r.Lone] : "--")} " +
                        $"{r.LoneHold * 1000,4:F0} ms {r.LoneTurn,5:F2}° {r.LonePeak,5:F1} N; group {r.Group} {r.GroupHold * 1000,4:F0} ms {r.GroupTurn,5:F2}°; pull {r.Pull,5:F1} N");
            }
            if (c.Flights > 0) c.Sink /= c.Flights;
            return c;
        }

        static string Rates(in Cell c) =>
            $"lone {c.Lone,2}/{c.Flights} ({c.Rate,4:P0}), longest {c.LoneHold * 1000,3:F0} ms, most {c.LoneTurn,4:F2}°; one or many {c.Group,2}/{c.Flights} ({c.GroupRate,4:P0}); " +
            $"never off {c.Never}; threw {c.Thrown}; sink {c.Sink * 1000,5:F2} mm; crashes {c.Crashes}, tunnels {c.Tunnels}";

        // The 95 % binomial margin of a difference of two rates (normal approximation, published).
        static double Margin(in Cell a, in Cell b) =>
            1.96 * Math.Sqrt(a.Rate * (1 - a.Rate) / Math.Max(1, a.Flights) + b.Rate * (1 - b.Rate) / Math.Max(1, b.Flights));

        // The targets, design 13's reading of the pilot's words, on any model's rates; true where each is met.
        struct Verdict
        {
            public bool Sometimes, SoftMore, SoftUnderHalf, RainSoft, RainFirm, Severe;
            public bool All => Sometimes && SoftMore && SoftUnderHalf && RainSoft && RainFirm && Severe;
        }

        static Verdict Judge(in Cell dryFirm, in Cell drySoft, in Cell rainFirm, in Cell rainSoft, in Cell severeSoft) => new Verdict
        {
            Sometimes = dryFirm.Lone >= 1 && dryFirm.Rate <= 1.0 / 3,
            SoftMore = drySoft.Rate - dryFirm.Rate > Margin(drySoft, dryFirm),
            SoftUnderHalf = drySoft.Rate < 0.5,
            RainSoft = rainSoft.Rate > 0.5,
            RainFirm = rainFirm.Rate > dryFirm.Rate,
            Severe = severeSoft.Rate >= rainSoft.Rate,
        };

        static void Print(string who, in Verdict v, in Cell dryFirm, in Cell drySoft, in Cell rainFirm, in Cell rainSoft, in Cell severeSoft)
        {
            Console.WriteLine($"  {who}:");
            Console.WriteLine($"    dry firm {dryFirm.Lone}/{dryFirm.Flights}: sometimes (at least one, at most a third): {(v.Sometimes ? "met" : "not met")}");
            Console.WriteLine($"    dry soft {drySoft.Lone}/{drySoft.Flights}: above dry firm by the 95 % margin ({Margin(drySoft, dryFirm):P1}): {(v.SoftMore ? "met" : "not met")}; under half: {(v.SoftUnderHalf ? "met" : "not met")}");
            Console.WriteLine($"    soft in Rain {rainSoft.Lone}/{rainSoft.Flights}: above half: {(v.RainSoft ? "met" : "not met")}; firm in Rain {rainFirm.Lone}/{rainFirm.Flights} above dry firm: {(v.RainFirm ? "met" : "not met")}");
            Console.WriteLine($"    soft in Severe rain {severeSoft.Lone}/{severeSoft.Flights}: at least Rain's: {(v.Severe ? "met" : "not met")}");
        }

        public static bool Run(int seeds, bool nominalLegs = false)
        {
            if (nominalLegs)
            {
                _edit = s =>
                {
                    if (s.Contacts?.Legs != null) s.Contacts.Legs.StiffnessSigma = s.Contacts.Legs.StiffnessBound = 0;
                };
                Console.WriteLine("  --nominal-legs: no leg.stiffness spread (the legs as before 2.9)");
            }
            DroneSpec spec = Spec();
            List<Spot> spots = Spots();
            Console.WriteLine($"  protocol: session airframe, still air; touch from 2 mm at 0.3 m/s (odd seeds) or the glide 0.5 down, 0.3 on (even), throttle cut; " +
                $"2 s at rest armed; ramp to hover + {LiftoffScenarios.RampAbove} over {LiftoffScenarios.RampSeconds} s, then + {LiftoffScenarios.Climb} a second until off; seeds 1–{seeds}");
            Console.WriteLine($"  held: lone = one corner over {LiftoffScenarios.HeldLoad} N at least {LiftoffScenarios.HeldTime * 1000} ms after the other three, turning {LiftoffScenarios.HeldTurn}° toward it (design 13); one or many = the same for the late group");
            var cells = new Dictionary<(Spot, RainLevel), Cell>();
            foreach (Spot p in spots)
            {
                p.Hover = LiftoffScenarios.Hover(spec, p.Open(), p.Foot);
                Console.WriteLine($"  {p.Name}: {p.Surface}{(p.Firm ? " (firm)" : " (soft)")}, hover {p.Hover:F3}");
                foreach (RainLevel rain in Weathers)
                {
                    Cell c = Fly(spec, p, rain, seeds);
                    cells[(p, rain)] = c;
                    Console.WriteLine($"    {rain,-10} {Rates(c)}");
                }
            }

            // The pilot's account over the groups (firm: dirt_track, dry_crust; soft: the rest, House's too).
            Cell Group(bool firm, RainLevel rain)
            {
                var g = new Cell();
                foreach (Spot p in spots)
                    if (p.Firm == firm) g.Add(cells[(p, rain)]);
                return g;
            }
            Cell df = Group(true, RainLevel.Sunny), ds = Group(false, RainLevel.Sunny), rf = Group(true, RainLevel.Rain), rs = Group(false, RainLevel.Rain),
                ss = Group(false, RainLevel.SevereRain), sf = Group(true, RainLevel.SevereRain);
            Console.WriteLine("  the account (lone corner):");
            Console.WriteLine($"    dry firm {Rates(df)}");
            Console.WriteLine($"    dry soft {Rates(ds)}");
            Console.WriteLine($"    Rain firm {Rates(rf)}");
            Console.WriteLine($"    Rain soft {Rates(rs)}");
            Console.WriteLine($"    Severe firm {Rates(sf)}");
            Console.WriteLine($"    Severe soft {Rates(ss)}");
            Verdict model = Judge(df, ds, rf, rs, ss);
            Print("the model", model, df, ds, rf, rs, ss);
            Row("the pilot's account (design 13's targets on the lone corner)", model.All, model.All ? "every target met" : "a target is not met (above)");
            int tunnels = 0;
            foreach (Cell c in cells.Values) tunnels += c.Tunnels;
            Row("no liftoff tunnels", tunnels == 0, $"{tunnels} tunnels");
            int thrown = 0;
            foreach (Cell c in cells.Values) thrown += c.Thrown;
            Row("no liftoff's step throws", thrown == 0, $"{thrown} flights threw");

            // Known-wrong 1: a dice roll at the dry firm rate everywhere (an outcome model, seeded).
            {
                ulong x = 0x450F;
                Cell Dice(in Cell of)
                {
                    var c = new Cell { Flights = of.Flights };
                    for (int i = 0; i < of.Flights; i++)
                    {
                        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
                        if ((x >> 11) * (1.0 / 9007199254740992.0) < df.Rate) c.Lone++;
                    }
                    return c;
                }
                Cell a = Dice(df), b = Dice(ds), cr = Dice(rf), d = Dice(rs), e = Dice(ss);
                Verdict v = Judge(a, b, cr, d, e);
                Print("known-wrong 1, a dice roll at the dry firm rate", v, a, b, cr, d, e);
                Row("known-wrong 1 misses \"more often on soft\" or \"in rain\"", !v.SoftMore || !v.RainSoft, $"soft {b.Lone}/{b.Flights}, rain soft {d.Lone}/{d.Flights}");
            }
            // Known-wrong 2: the dry rows in rain, flown on the soft spots.
            {
                SurfaceTable dry = LiftoffScenarios.DryEverywhere();
                Cell d = new Cell(), e = new Cell();
                foreach (Spot p in spots)
                {
                    if (p.Firm) continue;
                    d.Add(Fly(spec, p, RainLevel.Rain, seeds, dry));
                    e.Add(Fly(spec, p, RainLevel.SevereRain, seeds, dry));
                }
                Verdict v = Judge(df, ds, rf, d, e);
                Print("known-wrong 2, the dry rows in rain", v, df, ds, rf, d, e);
                Row("known-wrong 2 misses \"in rain\"", !v.RainSoft || !v.Severe || !(d.Rate > ds.Rate), $"rain soft {d.Lone}/{d.Flights} against dry soft {ds.Lone}/{ds.Flights}");
            }
            // Known-wrong 3: K§7.3's touch rule, every anchored element touching a skid hooks: an outcome model (no hook law
            // until W9 2.10), so every liftoff from a vegetated soft spot holds a leg.
            {
                var b = new Cell { Flights = ds.Flights, Lone = ds.Flights };
                Verdict v = Judge(df, b, rf, rs, ss);
                Print("known-wrong 3, every touching element hooks (outcome model)", v, df, b, rf, rs, ss);
                Row("known-wrong 3 misses \"under half\" on dry soft", !v.SoftUnderHalf, $"dry soft {b.Lone}/{b.Flights}");
            }
            // Known-wrong 4: Coulomb friction alone (no anchor, adhesion or plough): the rigid ground, μ 0.5/0.4.
            {
                Cell a = new Cell(), d = new Cell();
                foreach (Spot p in spots)
                {
                    if (p.Firm) a.Add(Fly(spec, p, RainLevel.Sunny, seeds, rigid: true));
                    else d.Add(Fly(spec, p, RainLevel.Rain, seeds, rigid: true));
                }
                Verdict v = Judge(a, ds, rf, d, ss);
                Print("known-wrong 4, Coulomb friction alone", v, a, ds, rf, d, ss);
                Row("known-wrong 4 misses \"at least one\" or \"in rain\"", !v.Sometimes || !v.RainSoft, $"dry firm {a.Lone}/{a.Flights}, rain soft {d.Lone}/{d.Flights}");
            }

            VillageAgainstPackages(spec);
            TilledAgainstFloor(spec);
            return _ok;
        }

        // 2.9's slope rows: the audit's Village spots against W3's package of the same angle and class, the pilot's cut landing
        // (dry) and the disarmed rest, along (nose uphill) and across; where both hold, the end tilt within 1°.
        static void VillageAgainstPackages(DroneSpec spec)
        {
            Console.WriteLine("  Village against the slope packages (the cut landing and the rest; end tilt from the normal, deg):");
            var spots = new (double X, double Z, string Surface)[]
            {
                (313.040, 20.000, "weeds"), (311.190, 50.710, "meadow_sod"), (884.690, 291.580, "meadow_sod"), (586.470, 20.370, "weeds"),
            };
            MapWorld farm = SlopeScenarios.Farmland();
            foreach (var (x, z, surface) in spots)
            {
                double deg = SlopeScenarios.FacetDeg(SlopeScenarios.Ground(farm, x, z));
                foreach (double yaw in new[] { 0.0, 90.0 })
                {
                    SlopeScenarios.Touchdown v = SlopeScenarios.LandOnFarmland(spec, x, z, yaw);
                    SlopeScenarios.Touchdown k = SlopeScenarios.Land(spec, deg, RainLevel.Sunny, yaw, surface: surface);
                    bool rest = v.EndTilt < 15 && k.EndTilt < 15 && v.Crashes == 0 && k.Crashes == 0;
                    string way = yaw == 0 ? "along " : "across";
                    string detail = $"({x:F2}, {z:F2}) {v.Facet:F2}° {surface} {way}: Village {v.MaxTilt:F1}/{v.EndTilt:F1}{(v.Over ? " over" : "")}, " +
                        $"package {k.Facet:F2}° {k.MaxTilt:F1}/{k.EndTilt:F1}{(k.Over ? " over" : "")}";
                    if (rest) Row("  landing " + way, Math.Abs(v.EndTilt - k.EndTilt) <= 1, detail);
                    else Console.WriteLine($"    landing {detail}: not compared (one goes down: W9 2.6's legs on a side slope)");
                    SlopeScenarios.Rest rv = SlopeScenarios.SettleOnFarmland(spec, x, z, yaw), rk = SlopeScenarios.Settle(spec, deg, RainLevel.Sunny, yaw, surface: surface);
                    bool holds = !SlopeScenarios.Slipped(rv) && !SlopeScenarios.Slipped(rk) && rv.Tilt < 15 && rk.Tilt < 15;
                    string rd = $"({x:F2}, {z:F2}) {way}: Village moved {rv.Slide * 1000:F0} mm, tilt {rv.Tilt:F1}°; package moved {rk.Slide * 1000:F0} mm, tilt {rk.Tilt:F1}°";
                    if (holds) Row("  rest " + way, Math.Abs(rv.Tilt - rk.Tilt) <= 1, rd);
                    else Console.WriteLine($"    rest {rd}: not compared (one slides or goes down: W9 2.6)");
                }
            }
        }

        // 2.9's soft-soil row: the pilot's landing at 1 m/s (SoilScenarios.Land) on Village's tilled spot against a floor of tilled,
        // dry, Rain and Severe rain: the peak and resting sinks within 1 mm (the spec's).
        static void TilledAgainstFloor(DroneSpec spec)
        {
            Console.WriteLine("  the soft-soil landing at 1 m/s with the throttle cut, Village's tilled against a floor of tilled (sink peak / after 3 s, mm):");
            MapWorld farm = SlopeScenarios.Farmland();
            GroundSample g = SlopeScenarios.Ground(farm, 715.970, 105.470);
            var at = new Double3(715.970, g.Height, 105.470);
            foreach (RainLevel rain in Weathers)
            {
                var v = LiftoffScenarios.Drop(spec, new FlightWorld(farm), at, 1.0, rain);
                var f = LiftoffScenarios.Drop(spec, SoilScenarios.Floor("tilled"), new Double3(0, 0, 0), 1.0, rain);
                bool pass = Math.Abs(v.Peak - f.Peak) <= 0.001 && Math.Abs(v.After - f.After) <= 0.001 && v.Tunnels == 0 && f.Tunnels == 0;
                Row("  tilled " + rain, pass, $"Village {v.Peak * 1000:F3} / {v.After * 1000:F3}, floor {f.Peak * 1000:F3} / {f.After * 1000:F3}");
            }
        }
    }
}
