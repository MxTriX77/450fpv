using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fiber-tether (p7d-tether 1.1): "Every value labelled and loaded" and "A replay refuses another table". The
    // repository's assets-src/tether.json loads with its six blocks, every value labelled published, derived or assumed;
    // the loader refuses a value without its label (naming the block, the field and the label) and every other fault
    // it guards; the values derived from drone.json agree with it; the header keeps every row bit for bit; and a replay
    // holding a tether.json of another hash is refused before its first step, naming both.
    public sealed class TetherTableTests
    {
        static string Text() => File.ReadAllText(TestFiles.Find(TetherTable.RelativePath));

        // text with old, which must occur exactly once, replaced by new.
        static string Edit(string text, string old, string replacement)
        {
            int at = text.IndexOf(old, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "the table holds " + old);
            Assert.That(text.IndexOf(old, at + 1, StringComparison.Ordinal), Is.EqualTo(-1), "the table holds " + old + " once");
            return text.Substring(0, at) + replacement + text.Substring(at + old.Length);
        }

        [Test]
        public void EveryValueLabelledAndLoaded()
        {
            TetherTable t = TetherTable.Parse(Text());
            foreach (string block in TetherTable.Blocks)
                Assert.That(Array.Exists(t.Rows, r => r.Name.StartsWith(block + ".", StringComparison.Ordinal)), Is.True, block + " is loaded");
            foreach (TetherRow r in t.Rows)
            {
                Assert.That(r.Values.Length, Is.GreaterThan(0), r.Name);
                Assert.That(Array.IndexOf(new[] { "published", "derived", "assumed" }, DroneSpec.Labels[r.Label]), Is.GreaterThanOrEqualTo(0),
                    r.Name + ": design 1 labels every value published, derived or assumed");
            }
            // Design 1's numbers, as read.
            Assert.That(t.Number("fiber.axial_stiffness_n"), Is.EqualTo(895.8));
            Assert.That(t.Number("fiber.proof_force_n"), Is.EqualTo(8.461));
            Assert.That(t.Number("link.launch_margin_db"), Is.EqualTo(13.5));
            Assert.That(t.Row("spool.peel_energy_j_m").Values, Is.EqualTo(new[] { 0.0196, 0.0981 }));
            Assert.That(t.Row("strength.span_pa").Values, Is.EqualTo(new[] { 0, 6.895e8, 1, 1.4e9 }));
            Assert.That(t.Row("strength.span_pa").Kind, Is.EqualTo(TetherValueKind.Quantiles));
            Assert.That(DroneSpec.Labels[t.Row("fiber.youngs_modulus_pa").Label], Is.EqualTo("published"));
            Assert.Throws<ArgumentException>(() => t.Row("fiber.no_such_value"));
            // The macrobend law through G.657.A2's two limits: 2.5 dB a turn at 5 mm (design 1).
            double perTurn = t.Number("link.macrobend_loss_a_db") * DetMath.Exp(-t.Number("link.macrobend_decay_per_m") * (0.005 - t.Number("link.macrobend_radius_a_m")));
            Assert.That(perTurn, Is.EqualTo(2.5).Within(0.01));
        }

        // Each fault the loader guards: the edit to the repository's table and a problem it must name.
        [TestCase("\"youngs_modulus_pa\": {\"value\": 7.3e10, \"label\": \"published\", ", "\"youngs_modulus_pa\": {\"value\": 7.3e10, ",
            "fiber.youngs_modulus_pa.label: missing")]
        [TestCase("\"lip_friction\": {\"value\": 0.3, \"label\": \"assumed\"", "\"lip_friction\": {\"value\": 0.3, \"label\": \"guessed\"",
            "spool.lip_friction.label: must be one of")]
        [TestCase("\"anchor_behind_start_m\": {\"value\": 3, \"label\": \"assumed\", \"rationale\"",
            "\"anchor_behind_start_m\": {\"value\": 3, \"label\": \"assumed\", \"note\"", "launch.anchor_behind_start_m.rationale: missing")]
        [TestCase("\"tie_unseat_slip_m\"", "\"tie_unseat_slips_m\"", "fittings.tie_unseat_slips_m: is not a field of the schema")]
        [TestCase("\"tie_unseat_slip_m\"", "\"tie_unseat_slips_m\"", "fittings.tie_unseat_slip_m: missing")]
        [TestCase("\"launch\": {", "\"launches\": {", "launch: missing or not an object")]
        [TestCase("\"schema\": 1,", "\"schema\": 2,", "tether.json.schema: must be 1")]
        [TestCase("\"irregularity\": {\"value\": [0.75, 1.25]", "\"irregularity\": {\"value\": 0.75", "spool.irregularity.value: must be [lo, hi]")]
        [TestCase("\"irregularity\": {\"value\": [0.75, 1.25]", "\"irregularity\": {\"value\": [1.25, 0.75]", "with 0 < lo ≤ hi")]
        [TestCase("\"intact_pa\": {\"value\": [[0, 3.0e9], [1, 5.0e9]]", "\"intact_pa\": {\"value\": [[0, 3.0e9], [1, 2.0e9]]",
            "strength.intact_pa.value: p must rise from 0 to 1")]
        [TestCase("\"balloon_length_m\": {\"value\": 0.25", "\"balloon_length_m\": {\"value\": 0", "spool.balloon_length_m.value: must be above 0")]
        [TestCase("\"youngs_modulus_pa\": {\"value\": 7.3e10", "\"youngs_modulus_pa\": {\"value\": 7.4e10",
            "fiber.axial_stiffness_n: 895.8 is labelled derived but youngs_modulus_pa·glass_area = ")]
        [TestCase("\"intact_pa\": {\"value\": [[0, 3.0e9]", "\"intact_pa\": {\"value\": [[0, 3.1e9]", "strength.flaw_pa: ends at")]
        [TestCase("\"schema\": 1,", "\"schema\": 1,,", "line ")]
        public void AFaultyTableIsRefusedNamingTheProblem(string old, string replacement, string problem)
        {
            string text = Edit(Text(), old, replacement);
            var e = Assert.Throws<TetherTableException>(() => TetherTable.Parse(text));
            bool named = false;
            foreach (string p in e.Problems) named |= p.Contains(problem);
            Assert.That(named, Is.True, "a problem naming '" + problem + "' in:\n" + e.Message);
        }

        // The two values the table derives from drone.json (design 1): the line's mass per metre from the coil's fiber, and
        // the exit at the rear cap's centre.
        [Test]
        public void TheValuesDerivedFromDroneJsonAgreeWithIt()
        {
            TetherTable t = TetherTable.Parse(Text());
            DroneSpec drone = RepositoryDrone.Spec();
            Assert.That(t.Number("fiber.mass_per_m_kg"), Is.EqualTo(drone.Coil.FiberMass / drone.Coil.FiberLength).Within(0.1).Percent);
            DronePart coil = drone.Parts[drone.Coil.Part];
            double half = coil.Length / 2;
            Assert.That(t.Row("spool.exit_position_m").Values, Is.EqualTo(new[]
            {
                coil.Position.X - coil.Axis.X * half, coil.Position.Y - coil.Axis.Y * half, coil.Position.Z - coil.Axis.Z * half,
            }).Within(1e-12), "the line leaves aft through the rear cap's centre");
        }

        // The header keeps every row and the hash bit for bit; the replay builds from them, and refuses a tether.json of
        // another hash before its first step, naming both.
        [Test]
        public void TheHeaderKeepsTheTableAndAReplayRefusesAnother()
        {
            string text = Text();
            TetherTable table = TetherTable.Parse(text);
            DroneSpec spec = BootstrapDrone.Spec();
            var setup = new FlightSetup
            {
                Seed = 0x7D11,
                World = FlightWorld.Floor(),
                Mass = spec.BuildMass(spec.Nominal),
                StartPosition = new Double3(0, 10, 0),
                Tether = table,
            };
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, spec.Nominal);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int n = 0; n < 200; n++)
            {
                core.Advance(StickSample.Neutral);
                recorder.RecordStep(core.State, StickSample.Neutral, core.Trace, core.Events);
            }
            recorder.Close();
            byte[] bytes = log.ToArray();

            LogHeader back = LogHeader.Read(new MemoryStream(bytes));
            Assert.That(back.Unknown, Is.Empty);
            Assert.That(back.Tether, Is.Not.Null);
            Assert.That(back.Tether.ContentHash, Is.EqualTo(table.ContentHash));
            Assert.That(back.Tether.Rows.Length, Is.EqualTo(table.Rows.Length));
            for (int i = 0; i < table.Rows.Length; i++)
            {
                TetherRow a = table.Rows[i], b = back.Tether.Rows[i];
                Assert.That((b.Name, b.Label, b.Kind), Is.EqualTo((a.Name, a.Label, a.Kind)));
                Assert.That(Array.ConvertAll(b.Values, BitConverter.DoubleToInt64Bits), Is.EqualTo(Array.ConvertAll(a.Values, BitConverter.DoubleToInt64Bits)), a.Name);
            }
            var problems = new List<string>();
            Assert.That(back.ToSetup(FlightWorld.Floor(), problems).Tether.Number("fiber.axial_stiffness_n"), Is.EqualTo(895.8), "the replay's table is the header's");
            Assert.That(problems, Is.Empty);

            ReplayResult same = FlightReplay.Run(new MemoryStream(bytes), id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash, tetherHash: table.ContentHash);
            Assert.That(same.Outcome, Is.EqualTo(ReplayOutcome.Match), same.Message);
            Assert.That(same.Steps, Is.EqualTo(200));

            // One value changed: a valid table of another hash.
            ulong changed = TetherTable.Parse(Edit(text, "\"lip_friction\": {\"value\": 0.3,", "\"lip_friction\": {\"value\": 0.31,")).ContentHash;
            Assert.That(changed, Is.Not.EqualTo(table.ContentHash));
            ReplayResult other = FlightReplay.Run(new MemoryStream(bytes), id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash, tetherHash: changed);
            Assert.That(other.Outcome, Is.EqualTo(ReplayOutcome.Refused), other.Message);
            Assert.That(other.Steps, Is.EqualTo(0), "refused before its first step");
            StringAssert.Contains("tether.json differs", other.Message);
            StringAssert.Contains(table.ContentHash.ToString("X16"), other.Message, "names the log's hash");
            StringAssert.Contains(changed.ToString("X16"), other.Message, "names the replay's hash");

            // A replay without the file is refused too.
            ReplayResult none = FlightReplay.Run(new MemoryStream(bytes), id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            Assert.That(none.Outcome, Is.EqualTo(ReplayOutcome.Refused), none.Message);
        }

        [Test]
        public void TheHashSeesEditsNotLineEndings()
        {
            string text = Text().Replace("\r\n", "\n");
            ulong lf = TetherTable.Parse(text).ContentHash;
            Assert.That(TetherTable.Parse(text.Replace("\n", "\r\n")).ContentHash, Is.EqualTo(lf));
            Assert.That(TetherTable.Parse(Edit(text, "\"lip_friction\": {\"value\": 0.3,", "\"lip_friction\": {\"value\": 0.30,")).ContentHash, Is.Not.EqualTo(lf));
        }
    }
}
