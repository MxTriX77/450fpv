using System;
using System.Globalization;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // drone.json's schema and validator (DRN-03, design 3): a small valid drone of the test's own passes, and the same
    // drone, mutated one fault at a time, is refused with the fault named.
    public sealed class DroneSpecTests
    {
        // A minimal valid drone, written with ' for " so it reads as JSON. Every number is labelled.
        static string L(string value) => "{'value': " + value + ", 'label': 'assumed'}";

        static string Box(string name, string material, string mass, string position, string size) =>
            "{'name': '" + name + "', 'kind': 'box', 'material': '" + material + "', 'mass_kg': " + L(mass)
            + ", 'position_m': " + L(position) + ", 'size_m': " + L(size) + "}";

        static string Cylinder(string name, string mass, string position, string diameter, string length) =>
            "{'name': '" + name + "', 'kind': 'cylinder', 'material': 'polymer', 'mass_kg': " + L(mass)
            + ", 'position_m': " + L(position) + ", 'diameter_m': " + L(diameter) + ", 'length_m': " + L(length)
            + ", 'axis': " + L("[0, 1, 0]") + "}";

        static string Motor(string position, string spin) =>
            "{'position_m': " + L(position) + ", 'spin': '" + spin + "', 'kv_rpm_per_v': " + L("900")
            + ", 'tau_s': " + L("0.05") + ", 'esc_tau_s': " + L("0.005") + "}";

        static string Pack(string part) =>
            "{'part': '" + part + "', 'series': " + L("6") + ", 'parallel': " + L("2") + ", 'cell_capacity_ah': " + L("5")
            + ", 'r_cell_ohm': " + L("0.015") + ", 'ocv_v': " + L("[3.0, 3.7, 4.2]") + "}";

        static readonly string Valid = (
            "{'schema': 1, 'name': 'test drone', 'bootstrap': false, 'about': 'a test', 'fields': {'parts': 'words'},"
            + " 'parts': ["
            + Box("body", "carbon", "1.0", "[0, 0, 0]", "[0.1, 0.05, 0.2]") + ", "
            + Box("pack_a", "li_ion", "0.5", "[-0.03, 0.05, 0]", "[0.05, 0.05, 0.1]") + ", "
            + Box("pack_b", "li_ion", "0.5", "[0.03, 0.05, 0]", "[0.05, 0.05, 0.1]") + ", "
            + Cylinder("spool", "0.6", "[0, -0.1, 0]", "0.14", "0.08") + ", "
            + Box("cargo", "polymer", "1.0", "[0, -0.08, 0.1]", "[0.1, 0.12, 0.1]") + ", "
            + Cylinder("leg_rr", "0.03", "[0.08, -0.15, -0.08]", "0.012", "0.3") + ", "
            + Cylinder("leg_fr", "0.03", "[0.08, -0.15, 0.08]", "0.012", "0.3") + ", "
            + Cylinder("leg_rl", "0.03", "[-0.08, -0.15, -0.08]", "0.012", "0.3") + ", "
            + Cylinder("leg_fl", "0.03", "[-0.08, -0.15, 0.08]", "0.012", "0.3")
            + "], 'motors': ["
            + Motor("[0.16, 0.04, -0.16]", "cw") + ", " + Motor("[0.16, 0.04, 0.16]", "ccw") + ", "
            + Motor("[-0.16, 0.04, -0.16]", "ccw") + ", " + Motor("[-0.16, 0.04, 0.16]", "cw")
            + "], 'props': {'diameter_m': " + L("0.254") + ", 'blades': " + L("3") + ", 'pitch_m': " + L("0.127")
            + ", 'k_t_n_per_rad2_s2': " + L("1.5e-5") + ", 'kappa_m': " + L("0.0175") + "},"
            + " 'packs': [" + Pack("pack_a") + ", " + Pack("pack_b") + "],"
            + " 'legs': {'parts': ['leg_rr', 'leg_fr', 'leg_rl', 'leg_fl'], 'stiffness_n_per_m': " + L("20000")
            + ", 'damping_n_s_per_m': " + L("40") + ", 'foot_radius_m': " + L("0.0075") + "},"
            + " 'coil': {'part': 'spool', 'spool_mass_kg': " + L("0.2") + ", 'fiber_mass_kg': " + L("0.8")
            + ", 'fiber_length_m': " + L("10000") + "},"
            + " 'payload': {'part': 'cargo', 'mass_min_kg': " + L("0") + ", 'mass_max_kg': " + L("2") + "},"
            + " 'stand': {'material': 'steel', 'post_radius_m': " + L("0.226") + ", 'post_height_m': " + L("0.25")
            + ", 'post_diameter_m': " + L("0.025") + ", 'mass_kg': " + L("2.9") + "}}"
        ).Replace('\'', '"');

        [Test]
        public void TheTestDroneIsValid()
        {
            DroneSpec spec = DroneSpec.Parse(Valid);
            Assert.That(spec.Parts.Length, Is.EqualTo(9));
            Assert.That(spec.Parts[spec.Coil.Part].Name, Is.EqualTo("spool"));
            Assert.That(spec.Parts[spec.Payload.Part].Name, Is.EqualTo("cargo"));
            Assert.That(spec.Motors[0].Clockwise, Is.True);
            Assert.That(spec.Motors[1].Clockwise, Is.False);
            Assert.That(spec.Props.Blades, Is.EqualTo(3));
            Assert.That(spec.Packs[1].Ocv, Is.EqualTo(new[] { 3.0, 3.7, 4.2 }));
        }

        // Each case: a fault put into the valid drone (find → replace, both with ' for "), and what the refusal says.
        [TestCase("'mass_kg': {'value': 1.0, 'label': 'assumed'}, ", "", "parts[0] (body).mass_kg: missing", TestName = "A part with no mass is refused")]
        [TestCase("'mass_kg': {'value': 1.0, 'label': 'assumed'}", "'mass_kg': {'value': 1.0}", "parts[0] (body).mass_kg.label: missing", TestName = "A number with no label is refused")]
        [TestCase("'mass_kg': {'value': 1.0, 'label': 'assumed'}", "'mass_kg': {'value': 1.0, 'label': 'guessed'}", "parts[0] (body).mass_kg.label: must be one of", TestName = "An unknown label is refused")]
        [TestCase("'mass_kg': {'value': 1.0, 'label': 'assumed'}", "'mass_kg': 1.0", "parts[0] (body).mass_kg: a number carries its label", TestName = "A bare number is refused")]
        [TestCase("'mass_kg': {'value': 1.0, 'label': 'assumed'}", "'mass_kg': {'value': 0, 'label': 'assumed'}", "parts[0] (body).mass_kg: must be above 0", TestName = "A massless part is refused")]
        [TestCase("'name': 'body', ", "'name': 'body', 'colour': 'red', ", "parts[0] (body).colour: is not a field of the schema", TestName = "An unknown field is refused")]
        [TestCase("'material': 'carbon'", "'material': 'unobtainium'", "parts[0] (body).material: 'unobtainium' is not one of", TestName = "An unknown material is refused")]
        [TestCase("'name': 'pack_b'", "'name': 'pack_a'", "parts[2] (pack_a).name: is used twice", TestName = "A part name used twice is refused")]
        [TestCase("[0.16, 0.04, -0.16]", "[0.16, 0.04, 0.17]", "motors[0] (rear right).position_m: is not at the rear right corner", TestName = "A motor out of Betaflight's order is refused")]
        [TestCase("'spin': 'ccw'", "'spin': 'cw'", "motors: the diagonal pairs must spin alike", TestName = "Unbalanced rotor torques are refused")]
        [TestCase("'part': 'spool'", "'part': 'reel'", "coil.part: no part is named 'reel'", TestName = "A reference to no part is refused")]
        [TestCase("'mass_max_kg': {'value': 2, 'label': 'assumed'}", "'mass_max_kg': {'value': 0.5, 'label': 'assumed'}", "payload: the payload part's mass, 1 kg, is outside", TestName = "A payload outside its range is refused")]
        [TestCase("'ocv_v': {'value': [3.0, 3.7, 4.2]", "'ocv_v': {'value': [3.0, 4.2, 3.7]", "packs[0].ocv_v: the voltage must not fall", TestName = "A falling OCV curve is refused")]
        [TestCase("'pitch_m': {'value': 0.127", "'pitch_m': {'value': 0.12345678901234567", "a number with more significant digits than an exact read allows", TestName = "A number past an exact read is refused")]
        [TestCase("'schema': 1,", "'schema': 1", "line 1: ", TestName = "Broken JSON is refused with its line")]
        public void TheValidatorRefuses(string find, string replace, string expected)
        {
            find = find.Replace('\'', '"');
            Assert.That(Valid.Contains(find), Is.True, "the test's fault must apply: " + find);
            string json = ReplaceFirst(Valid, find, replace.Replace('\'', '"'));
            var e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(json));
            Assert.That(string.Join("\n", e.Problems), Does.Contain(expected));
        }

        [Test]
        public void EveryProblemIsListedInOneRun()
        {
            string json = Valid
                .Replace("\"label\": \"assumed\"}, \"position_m\"", "\"label\": \"guessed\"}, \"position_m\"")
                .Replace("\"spin\": \"cw\"", "\"spin\": \"up\"");
            var e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(json));
            Assert.That(e.Problems.Count, Is.GreaterThanOrEqualTo(2 + 9), string.Join("\n", e.Problems));
        }

        // The deterministic number read gives the correctly rounded value, which is what CoreCLR's double.Parse gives.
        [TestCase("0.1")]
        [TestCase("0.143")]
        [TestCase("-0.15981")]
        [TestCase("0.08485")]
        [TestCase("1.5499e-5")]
        [TestCase("1.5499E-05")]
        [TestCase("4.2")]
        [TestCase("25.497")]
        [TestCase("10000")]
        [TestCase("2.5e3")]
        [TestCase("123456789012345")]
        [TestCase("0.000123456789012345")]
        [TestCase("9007199254740992")]
        [TestCase("1e22")]
        [TestCase("3.0000000000000000000000")]
        public void ANumberIsReadExactly(string text)
        {
            double expected = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            Assert.That(BitConverter.DoubleToInt64Bits(DetJson.ParseNumber(text)), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)), text);
        }

        // Where the runtimes part: Unity's Mono parses "-0" as +0 and CoreCLR as −0 (the EditMode run showed it). The
        // core never calls double.Parse, and DetJson reads IEEE's −0 on both.
        [Test]
        public void MinusZeroIsMinusZeroOnEveryRuntime()
        {
            Assert.That(BitConverter.DoubleToInt64Bits(DetJson.ParseNumber("-0")), Is.EqualTo(BitConverter.DoubleToInt64Bits(-0.0)));
            Assert.That(BitConverter.DoubleToInt64Bits(DetJson.ParseNumber("-0.0e5")), Is.EqualTo(long.MinValue));
        }

        [TestCase("0.12345678901234567")]
        [TestCase("1e23")]
        [TestCase("1e-30")]
        [TestCase("01")]
        [TestCase(".5")]
        [TestCase("1.")]
        [TestCase("+1")]
        public void ANumberPastAnExactReadOrOutsideJsonIsRefused(string text)
        {
            Assert.Throws<FormatException>(() => DetJson.ParseNumber(text));
        }

        static string ReplaceFirst(string s, string find, string replace)
        {
            int i = s.IndexOf(find, StringComparison.Ordinal);
            return s.Substring(0, i) + replace + s.Substring(i + find.Length);
        }
    }
}
