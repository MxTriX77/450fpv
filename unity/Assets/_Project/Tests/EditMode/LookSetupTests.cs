using System.IO;
using Fpv450.Feed;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The look setup (p1-g1-look-lock-gallery task 1.1, look-setup "Sun and sky by time of day"): day and overcast
    /// light a horizontal 0.18 grey as the review rig's day does (its sun plus ambient on a flat surface, 0.18 grey
    /// about sRGB 118) within 5 %, and within 5 % of each other; the sun's arc puts the presets where design 4 has
    /// them; night's sky is about sRGB 93 at the horizon over near-black ground, as L; and the ambient probe is
    /// projected from the sky with Unity's flat-ambient scale and the right way up.
    /// </summary>
    public sealed class LookSetupTests
    {
        const float Grey = 0.18f;

        static SkyModel Model() => SkyModel.FromJson(Read(LookSetup.SkyModelPath), LookSetup.SkyModelPath);

        static LookPreset Preset(int index) => LookPreset.FromJson(Read(LookSetup.PresetPaths[index]), LookSetup.PresetPaths[index]);

        static string Read(string assetPath) => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath));

        static float Srgb(float linear) => 255f * Mathf.LinearToGammaSpace(linear);

        [Test]
        public void DayAndOvercast_LightAHorizontalGreyWithinFivePercentOfTheReviewRigsDay()
        {
            Review.LightProfile rig = Review.LoadProfile("day");
            float target = Grey * rig.HorizontalIlluminance;
            SkyModel model = Model();
            float day = 0f, overcast = 0f;
            foreach (int index in new[] { 0, 2 })
            {
                LookPreset preset = Preset(index);
                LookState state = LookModel.Evaluate(model, preset);
                float grey = Grey * state.HorizontalIlluminance;
                TestContext.Out.WriteLine($"{preset.name}: sun {state.SunHorizontal:0.0000} + ambient {state.HorizontalIlluminance - state.SunHorizontal:0.0000} = {state.HorizontalIlluminance:0.0000}; " +
                                          $"0.18 grey {grey:0.0000} (sRGB {Srgb(grey):0.0}) against the review day's {target:0.0000} (sRGB {Srgb(target):0.0}), {100f * (grey / target - 1f):+0.00;-0.00} %");
                Assert.That(grey / target, Is.InRange(0.95f, 1.05f), preset.name);
                if (index == 0) day = grey; else overcast = grey;
            }
            TestContext.Out.WriteLine($"overcast against day: {100f * (overcast / day - 1f):+0.00;-0.00} %");
            Assert.That(overcast / day, Is.InRange(0.95f, 1.05f));
        }

        [Test]
        public void SunArc_PutsThePresetsWhereDesign4HasThem()
        {
            SkyModel model = Model();
            (float noon, float noonAz) = LookModel.SunPosition(Preset(0).hour, model.latitude_deg, model.declination_deg);
            (float low, float lowAz) = LookModel.SunPosition(Preset(1).hour, model.latitude_deg, model.declination_deg);
            (float night, _) = LookModel.SunPosition(Preset(3).hour, model.latitude_deg, model.declination_deg);
            TestContext.Out.WriteLine($"day {noon:0.00}° at {noonAz:0.0}°, low sun {low:0.00}° at {lowAz:0.0}°, night {night:0.00}°");
            Assert.That(noon, Is.EqualTo(45f).Within(1f), "day: about 45° up, as the review rig");
            Assert.That(noonAz, Is.EqualTo(180f).Within(0.5f), "day: due south at solar noon");
            Assert.That(low, Is.InRange(1f, 12f), "low sun: under 12° up, as G and B");
            Assert.That(lowAz, Is.InRange(225f, 315f), "low sun: in the west in the afternoon");
            Assert.That(night, Is.LessThan(-18f), "night: below astronomical twilight");
            LookState lowState = LookModel.Evaluate(model, Preset(1));
            Assert.That(lowState.Sun.r / lowState.Sun.b, Is.GreaterThan(1.8f), "low sun: warm");
            Assert.That(LookModel.Evaluate(model, Preset(2)).Sun.maxColorComponent, Is.EqualTo(0f), "overcast: no sun");
        }

        [Test]
        public void Night_SkyAboutSrgb93AtTheHorizonOverNearBlackGround()
        {
            SkyModel model = Model();
            LookState state = LookModel.Evaluate(model, Preset(3));
            float horizon = Srgb(LookModel.Luminance(state.Horizon));
            Color albedo = new Color(model.ground_albedo_linear[0], model.ground_albedo_linear[1], model.ground_albedo_linear[2]);
            float ground = Srgb(LookModel.Luminance(albedo) * state.HorizontalIlluminance);
            TestContext.Out.WriteLine($"night: horizon sRGB {horizon:0.0}, zenith sRGB {Srgb(LookModel.Luminance(state.Zenith)):0.0}, ground sRGB {ground:0.0}, sun {state.Sun.maxColorComponent}");
            Assert.That(horizon, Is.InRange(83f, 103f), "the horizon about sRGB 93, as L");
            Assert.That(ground, Is.LessThan(40f), "the ground near black, as L (sRGB 13-35)");
            Assert.That(state.Sun.maxColorComponent, Is.EqualTo(0f), "moonless");
        }

        [Test]
        public void AmbientProbe_HasFlatAmbientsScaleAndLightsFromTheBrighterHemisphere()
        {
            var directions = new[] { Vector3.up, Vector3.down, Vector3.forward };
            var result = new Color[3];

            // Unity's flat ambient c evaluates to c; a uniform sky of radiance 1 must give the same.
            var flat = new SphericalHarmonicsL2();
            flat.AddAmbientLight(Color.white);
            flat.Evaluate(directions, result);
            Assert.That(result[0].r, Is.EqualTo(1f).Within(0.01f), "Unity's flat ambient");
            LookModel.Project(_ => Color.white).Evaluate(directions, result);
            TestContext.Out.WriteLine($"uniform sky 1: up {result[0].r:0.0000}, down {result[1].r:0.0000}, side {result[2].r:0.0000}");
            foreach (Color c in result)
                Assert.That(c.r, Is.EqualTo(1f).Within(0.01f), "a uniform sky of 1 lights every normal 1");

            // The upper hemisphere alone: irradiance over pi is 1 up, 0 down and 0.5 on a vertical face.
            LookModel.Project(e => e >= 0f ? Color.white : Color.black).Evaluate(directions, result);
            TestContext.Out.WriteLine($"upper hemisphere 1: up {result[0].r:0.0000}, down {result[1].r:0.0000}, side {result[2].r:0.0000}");
            Assert.That(result[0].r, Is.EqualTo(1f).Within(0.02f), "up");
            Assert.That(result[1].r, Is.EqualTo(0f).Within(0.02f), "down");
            Assert.That(result[2].r, Is.EqualTo(0.5f).Within(0.02f), "side");
        }
    }
}
