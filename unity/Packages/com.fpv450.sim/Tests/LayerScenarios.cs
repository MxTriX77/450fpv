using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's compliant layers (p7a-ground 1.12, design 6): the legs-on drone pressed level into a layer by the
    /// contact model alone, so the static law is what is measured, on S04's straw on the test range (its mat over its own
    /// soil box) and on a mattress of the fabric row over the stub's rigid floor; the known-wrong models are a row without
    /// its area law and a model without the stroke limit. LayerTests asserts them; tools/flightbench --contacts prints them.
    /// </summary>
    public static class LayerScenarios
    {
        const double G = 9.80665;

        /// <summary>The mattress's depth, m: the fabric row's area_reference_depth_m (the mattress it was read for).</summary>
        public const double FabricDepth = 0.15;

        /// <summary>S04's instance 707: the middle of its straw tile 4 (1.82 × 4.59 m, top at y 0, over the patch's soil box),
        /// no other straw tile within 5 cm of a 0.6 × 0.8 m square round it, so a press meets one tile's top alone (where tiles
        /// overlap, a skid sunk in one lies inside another's side face, a face buried in the mat that carries nothing since
        /// model 38).</summary>
        public static readonly Double3 Tile = new Double3(-5.7, 0, -9.7);

        /// <summary>The drone's contacts at its nominal loadout from the repository's files, with surfaces.json when table is
        /// given; without names a layer row whose area law a known-wrong model drops.</summary>
        public static FlightSetup Setup(DroneSpec spec, FlightWorld world, SurfaceTable table = null, string without = null)
        {
            MaterialRow[] materials = (MaterialRow[])ContactScenarios.RepositoryMaterials(out ulong hash).Clone();
            for (int i = 0; without != null && i < materials.Length; i++)
                if (materials[i].Name == without) materials[i].AreaLaw = AreaLaw.None;
            return new FlightSetup
            {
                World = world, Mass = spec.BuildMass(spec.Nominal), Contacts = spec.BuildContacts(spec.Nominal, materials, hash, table),
            };
        }

        /// <summary>The setup's drone standing on the reference part (K§7.1's leg: one 15 mm foot at each T-joint, the skids
        /// and the body away): each corner meets a layer row's own stiffness_n_per_m.</summary>
        public static FlightSetup ReferenceFeet(FlightSetup setup)
        {
            ContactShape[] all = setup.Contacts.Shapes;
            var feet = new System.Collections.Generic.List<ContactShape>();
            foreach (ContactShape s in all)
            {
                if (s.Role != ContactRole.Support || !s.Name.EndsWith("_joint", StringComparison.Ordinal)) continue;
                ContactShape f = s;
                f.Radius = 0.5 * Layer.ReferenceFoot;
                feet.Add(f);
            }
            setup.Contacts = setup.Contacts.Clone();
            setup.Contacts.Shapes = feet.ToArray();
            return setup;
        }

        public struct Press
        {
            /// <summary>Per corner at the press's end: its lowest support's overlap of the layer's top (m, the geometry's: the
            /// drone level, the top at the press point's height), the leg's load (N), the leg's deflection (m: its load over
            /// its stiffness on a layer, the series solve's in soil), what is under the part compressed (the overlap less the
            /// leg's deflection: the layer, and in soil the layer and the soil's sink), and its half-skid in soil (SoilOf).</summary>
            public double[] Overlap, Load, Deflection, Compression;
            public ContactModel.SkidSoil[] Soil;

            /// <summary>The contacts' upward force at the end, and the drone's weight (N); the leg's stiffness (N/m); the bytes
            /// allocated over the press's steps.</summary>
            public double Total, Weight, LegStiffness;
            public long Allocated;
        }

        /// <summary>
        /// The drone held level with its lowest support 1 mm over top (the layer's top at the press point), pressed down at
        /// 10 mm/s until its contacts carry g times its weight, each step's pose moved with its velocity zero (quasi-static, as
        /// SoilScenarios.Unsticking: the dampers read nothing). stroke is the layers' stroke share (+∞: the known-wrong
        /// model without the limit); place adds the scenario's runtime objects after the world's are reset.
        /// </summary>
        public static Press Pressing(FlightSetup setup, Double3 top, double g, double stroke = Layer.StrokeShare, Action<FlightWorld> place = null,
            Moisture moisture = Moisture.Dry)
        {
            FlightWorld world = setup.World;
            world.Query.ResetRuntimeObjects();
            place?.Invoke(world);
            double mass = setup.Mass.Properties.Mass, foot = -ContactScenarios.Standing(setup.Contacts);
            var model = new ContactModel(setup.Contacts, world, mass, moisture, stroke, new FlightRandom(setup.Seed));
            var events = new StepEvents();
            var s = new FlightState { Attitude = Quat.Identity, Position = top + new Double3(0, -foot + 0.001, 0) };
            model.Sense(s, ref s, events);
            const double Step = 0.01 * FlightCore.Dt; // 10 mm/s
            var r = new Press
            {
                Overlap = new double[4], Load = new double[4], Deflection = new double[4], Compression = new double[4], Soil = new ContactModel.SkidSoil[4],
                Weight = mass * G, LegStiffness = setup.Contacts.LegStiffness,
            };
            var trace = new StepTrace();
            long before = AllocProbe.Before();
            for (int k = 0; k < 40000 && r.Total < g * r.Weight; k++)
            {
                FlightState was = s;
                s.Position = s.Position - new Double3(0, Step, 0);
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                Double3 f = default, m = default;
                trace = new StepTrace();
                model.Forces(ref s, setup.Mass.Properties, ref f, ref m, ref trace, events);
                r.Total = f.Y;
            }
            r.Allocated = AllocProbe.After(before);
            for (int c = 0; c < 4; c++)
            {
                r.Soil[c] = model.SoilOf(c);
                r.Overlap[c] = top.Y - (s.Position.Y + foot);
                r.Load[c] = trace.LegLoad[c];
                r.Deflection[c] = r.Soil[c].Engaged ? r.Soil[c].Deflection : r.Load[c] / r.LegStiffness;
                r.Compression[c] = r.Overlap[c] - r.Deflection[c];
            }
            return r;
        }

        /// <summary>The stub's rigid floor (surfaces.json absent: the ground is rigid) with a mattress on it: one fabric box,
        /// 2 m square and FabricDepth deep, its top at FabricDepth over the origin. Place it with Pressing's place.</summary>
        public static FlightWorld MattressFloor() => new FlightWorld(new FloorWorld(0, "belt_bare", "fabric"));

        public static void PlaceMattress(FlightWorld world)
        {
            var box = new ShapeGeometry
            {
                Kind = ShapeKind.Box, Center = new Double3(0, 0.5 * FabricDepth, 0), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(1, 0.5 * FabricDepth, 1), Material = 1,
            };
            ((FloorWorld)world.Query).AddObject("mattress", new[] { box });
        }

        /// <summary>A layer row's stiffness under a level half-skid of drone.json's skid (its 0.22 m half and 10 mm tube) on a
        /// layer of depth D, by the row's law (Layer.Scale): what the corner meets.</summary>
        public static double HalfSkid(in MaterialRow row, double depth) =>
            row.Stiffness * Layer.Scale(row.AreaLaw, row.AreaReferenceDepth, depth, false, 0.22, 0.01);
    }
}
