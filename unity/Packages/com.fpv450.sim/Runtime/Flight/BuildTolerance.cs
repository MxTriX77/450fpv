using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The build's placement tolerances (PHY-24, D-066): where the pilot's hands set the loads before each flight, drawn
    /// once at the flight's start from the named stream "build.placement", each uniform within its bound and logged
    /// (FlightRandom), never noise on the pose. Each moves its part's centre in the mass model, so the centre of mass and
    /// the inertia follow; the contact shapes stay where drone.json puts them (assumed: every move is under 1/2 of its
    /// shape's radius). A FlightSetup without one (null) flies the exact build, centred side to side, as the goldens do.
    /// - the bottle under its strap on the upper plate: across ±BottleAcross, along ±BottleAlong
    /// - each pack along its side: along ±PackAlong, and out from the frame by 0–PackOut
    /// - the coil in its cradle (in its canister, with it, on the canister's two hangers): across ±CoilAcross, along its axis
    ///   ±CoilAlong
    /// - the main leads' run from the stack to one side: their WiringMass across ±WiringAcross, carried by the
    ///   electronics' part as its share of that part's mass
    /// </summary>
    public sealed class BuildTolerance
    {
        /// <summary>The bottle's across, ±m: 5 mm (assumed: set by eye on the plate's centreline under a strap that holds the
        /// round Ø0.080 m bottle where it lies, about a 16th of its diameter).</summary>
        public double BottleAcross = 0.005;

        /// <summary>The bottle's along, ±m: 15 mm (assumed: the strap's place along the 0.26 m bottle, set by eye, about a
        /// 16th of its length).</summary>
        public double BottleAlong = 0.015;

        /// <summary>A pack's along, ±m: 10 mm (assumed: each 0.22 m pack set by eye between its side's legs under its
        /// hook-and-loop strap, about a 20th of its length).</summary>
        public double PackAlong = 0.010;

        /// <summary>A pack's out from the frame, 0 to m: 2 mm (assumed: its inner face pressed to the frame, the strap's and
        /// the wrap's give).</summary>
        public double PackOut = 0.002;

        /// <summary>The coil's across, ±m: 2 mm (assumed: the spool's play in its cradle on two cable-tie hoops; since model 64
        /// the Ø0.101 m canister's on its two printed hangers, the pack fixed inside it).</summary>
        public double CoilAcross = 0.002;

        /// <summary>The coil's along its axis, ±m: 5 mm (assumed: where the hoops close on the 0.20 m spool; since model 64
        /// where the hangers close on the canister).</summary>
        public double CoilAlong = 0.005;

        /// <summary>The main leads' mass, kg: 0.06 (assumed: the 10 AWG pair and the XT90 of drone.json's 0.10 kg of wiring).</summary>
        public double WiringMass = 0.06;

        /// <summary>The leads' run across, ±m: 20 mm (derived: the stack's half-width, 0.03 m, less the leads' own half-width,
        /// to the side they leave by).</summary>
        public double WiringAcross = 0.020;

        /// <summary>drone.json's parts the tolerances move: the bottle (the payload), the coil, the electronics (the leads'
        /// carrier) and the packs; −1 or none for a part the build lacks.</summary>
        public int BottlePart = -1, CoilPart = -1, WiringPart = -1;

        /// <summary>The coil's housing (drone.json's coil.housing, the canister), −1 for none: it moves with the coil (model 64).</summary>
        public int CoilHousingPart = -1;
        public int[] PackParts = new int[0];

        public BuildTolerance Clone()
        {
            var c = (BuildTolerance)MemberwiseClone();
            c.PackParts = (int[])PackParts.Clone();
            return c;
        }

        /// <summary>One flight's offsets, body m (x right, y up, z forward), by part; null where none.</summary>
        public sealed class Offsets
        {
            public Double3 Bottle, Coil, Wiring;
            public Double3[] Packs;
        }

        /// <summary>One flight's draws from random (at the flight's start), each recorded for the header: "build.placement"
        /// (bottle_across_m, bottle_along_m, pack_along_m.k, pack_out_m.k, coil_across_m, coil_along_m, wiring_across_m).</summary>
        public Offsets DrawFor(FlightRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            SeedStream s = random.Open("build.placement");
            var o = new Offsets { Packs = new Double3[PackParts.Length] };
            o.Bottle = new Double3(random.Draw(ref s, "build.bottle_across_m", -BottleAcross, BottleAcross), 0,
                random.Draw(ref s, "build.bottle_along_m", -BottleAlong, BottleAlong));
            for (int k = 0; k < PackParts.Length; k++)
            {
                double along = random.Draw(ref s, "build.pack_along_m." + k, -PackAlong, PackAlong);
                double outward = random.Draw(ref s, "build.pack_out_m." + k, 0, PackOut);
                o.Packs[k] = new Double3(outward, 0, along); // signed by its side in Apply
            }
            o.Coil = new Double3(random.Draw(ref s, "build.coil_across_m", -CoilAcross, CoilAcross), 0,
                random.Draw(ref s, "build.coil_along_m", -CoilAlong, CoilAlong));
            o.Wiring = new Double3(random.Draw(ref s, "build.wiring_across_m", -WiringAcross, WiringAcross), 0, 0);
            return o;
        }

        /// <summary>Moves the parts of mass by o: each pack outward on its own side, the electronics by the leads' share of
        /// its mass (so the leads' moment is theirs), and rebuilds the model.</summary>
        public void Apply(MassModel mass, Offsets o)
        {
            if (BottlePart >= 0) Move(mass, BottlePart, o.Bottle);
            if (CoilPart >= 0)
            {
                // Across along body x, along on the coil's axis (body z on a level spool).
                Double3 a = mass.Part(CoilPart).Axis, by = new Double3(o.Coil.X + a.X * o.Coil.Z, a.Y * o.Coil.Z, a.Z * o.Coil.Z);
                Move(mass, CoilPart, by);
                if (CoilHousingPart >= 0) Move(mass, CoilHousingPart, by);
            }
            for (int k = 0; k < PackParts.Length; k++)
            {
                Double3 p = o.Packs[k];
                double side = mass.Part(PackParts[k]).Position.X < 0 ? -1 : 1;
                Move(mass, PackParts[k], new Double3(side * p.X, p.Y, p.Z));
            }
            if (WiringPart >= 0)
            {
                double carrier = mass.Part(WiringPart).Mass;
                if (carrier > 0) Move(mass, WiringPart, o.Wiring * (WiringMass / carrier));
            }
        }

        static void Move(MassModel mass, int part, Double3 by) => mass.SetPartPosition(part, mass.Part(part).Position + by);
    }
}
