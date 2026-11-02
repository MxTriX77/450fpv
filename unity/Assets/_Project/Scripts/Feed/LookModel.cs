using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Feed
{
    /// <summary>
    /// The sky model of the look setup (<c>Settings/Sky/sky_model.json</c>, p1-g1-look-lock-gallery design 4): the
    /// solar arc's latitude and declination, the gradient's shape, the ground albedo, and the light at a few sun
    /// elevations, between which the model interpolates linearly. Every colour is linear RGB in the review rig's units
    /// (0.18 grey under its day profile reads about sRGB 118), as the eye or the camera has adapted to the scene: there
    /// is no exposure step before the feed's (phase 4).
    /// </summary>
    [Serializable]
    public sealed class SkyModel
    {
        public float latitude_deg;
        public float declination_deg;
        public float zenith_exponent;
        public float ground_fade_sin;
        public float[] ground_albedo_linear;
        public float far_land_km;
        public float sun_hidden_from_cover;
        public float sun_disc_radius_deg;
        public float sun_disc_gain;
        public float sun_halo_gain;
        public SkyRow[] rows;

        public static SkyModel FromJson(string json, string what)
        {
            var model = JsonUtility.FromJson<SkyModel>(json);
            if (model == null || model.rows == null || model.rows.Length == 0)
                throw new FormatException($"{what}: no rows");
            Check(model.ground_albedo_linear, "ground_albedo_linear", what);
            for (int i = 0; i < model.rows.Length; i++)
            {
                SkyRow row = model.rows[i];
                string at = $"{what} rows[{i}]";
                Check(row.sun_linear, "sun_linear", at);
                Check(row.zenith_linear, "zenith_linear", at);
                Check(row.horizon_linear, "horizon_linear", at);
                Check(row.overcast_linear, "overcast_linear", at);
                if (i > 0 && row.sun_elevation_deg >= model.rows[i - 1].sun_elevation_deg)
                    throw new FormatException($"{at}: rows must run from the highest sun elevation to the lowest");
            }
            return model;
        }

        static void Check(float[] value, string key, string what)
        {
            if (value == null || value.Length != 3)
                throw new FormatException($"{what}: \"{key}\" must hold 3 numbers");
        }
    }

    /// <summary>The light at one sun elevation: the sun, the clear sky's zenith and horizon, and the overcast's tint and gradient.</summary>
    [Serializable]
    public sealed class SkyRow
    {
        public float sun_elevation_deg;
        public float[] sun_linear;
        public float[] zenith_linear;
        public float[] horizon_linear;
        public float[] overcast_linear;
        public float overcast_horizon_ratio;
    }

    /// <summary>A light preset (<c>Settings/Light/&lt;name&gt;.json</c>): the time of day, the cloud cover and the haze.</summary>
    [Serializable]
    public sealed class LookPreset
    {
        public string name;
        public string label;
        public string letters;
        public float hour;
        public float cloud_cover;
        public float fog_extinction_per_km;
        public float fog_scale_height_m;

        public static LookPreset FromJson(string json, string what)
        {
            var preset = JsonUtility.FromJson<LookPreset>(json);
            if (preset == null || string.IsNullOrEmpty(preset.name) || string.IsNullOrEmpty(preset.label))
                throw new FormatException($"{what}: \"name\" and \"label\" are required");
            if (preset.cloud_cover < 0f || preset.cloud_cover > 1f)
                throw new FormatException($"{what}: \"cloud_cover\" must lie in 0-1");
            return preset;
        }
    }

    /// <summary>What the look setup applies: the sun, the sky gradient, the ambient probe and the haze, all linear.</summary>
    public struct LookState
    {
        public float SunElevationDeg, SunAzimuthDeg;
        public Vector3 TowardsSun;
        public Color Sun;              // the directional light's colour times its intensity, 0 when hidden or set
        public float SunVisibility;    // 1 under a clear sky, 0 under full overcast
        public Color Zenith, Horizon, Ground;
        public SphericalHarmonicsL2 Ambient;
        public Color FogColor;
        public float FogExtinctionPerM, FogScaleHeightM;
        public float HorizontalIlluminance; // luminance of sun × sin(elevation) plus the ambient on an up-facing normal
        public float SunHorizontal;         // its sun share
    }

    /// <summary>
    /// The look setup's model (design 4): the sun from the hour on a solar arc, the sky as a zenith-horizon-ground
    /// gradient, the ambient probe projected from that sky, and exponential height fog coloured by the horizon.
    /// Cloud cover mixes the clear sky towards the overcast gradient and hides the sun, and the overcast sky's level
    /// keeps the clear sky's horizontal illuminance at that hour: the review rig's rule (its overcast ambient equals
    /// its day's sun plus ambient on a flat surface), as a camera's exposure holds the frame.
    /// </summary>
    public static class LookModel
    {
        const int Bands = 48, Azimuths = 16; // the ambient projection's grid over the sphere
        static readonly Vector3[] UpOnly = { Vector3.up };
        static float? projectionNorm;

        /// <summary>The sun's elevation and compass azimuth (from +Z north towards +X east) at local solar time.</summary>
        public static (float elevationDeg, float azimuthDeg) SunPosition(float hour, float latitudeDeg, float declinationDeg)
        {
            double phi = latitudeDeg * Math.PI / 180.0, delta = declinationDeg * Math.PI / 180.0;
            double h = (hour - 12.0) * 15.0 * Math.PI / 180.0;
            double sinEl = Math.Sin(phi) * Math.Sin(delta) + Math.Cos(phi) * Math.Cos(delta) * Math.Cos(h);
            double el = Math.Asin(Math.Max(-1.0, Math.Min(1.0, sinEl)));
            double cosAz = (Math.Sin(delta) - sinEl * Math.Sin(phi)) / Math.Max(1e-9, Math.Cos(el) * Math.Cos(phi));
            double az = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosAz)));
            if (Math.Sin(h) > 0.0)
                az = 2.0 * Math.PI - az; // afternoon: west of south
            return ((float)(el * 180.0 / Math.PI), (float)(az * 180.0 / Math.PI));
        }

        public static LookState Evaluate(SkyModel model, LookPreset preset) =>
            Evaluate(model, preset.hour, preset.cloud_cover, preset.fog_extinction_per_km, preset.fog_scale_height_m);

        public static LookState Evaluate(SkyModel model, float hour, float cloudCover, float fogExtinctionPerKm, float fogScaleHeightM)
        {
            float cover = Mathf.Clamp01(cloudCover);
            (float el, float az) = SunPosition(hour, model.latitude_deg, model.declination_deg);
            float elRad = el * Mathf.Deg2Rad, azRad = az * Mathf.Deg2Rad;
            var towardsSun = new Vector3(Mathf.Sin(azRad) * Mathf.Cos(elRad), Mathf.Sin(elRad), Mathf.Cos(azRad) * Mathf.Cos(elRad));

            (SkyRow a, SkyRow b, float t) = Bracket(model.rows, el);
            Color Lerp(Func<SkyRow, float[]> field) => Color.Lerp(Rgb(field(a)), Rgb(field(b)), t);
            float setting = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.5f, 0.5f, el)); // the disc going under the horizon
            float visibility = 1f - Mathf.InverseLerp(model.sun_hidden_from_cover, 1f, cover);
            Color sunClear = Lerp(r => r.sun_linear) * setting;
            Color zenith = Lerp(r => r.zenith_linear), horizon = Lerp(r => r.horizon_linear);
            Color tint = Lerp(r => r.overcast_linear);
            float ratio = Mathf.Lerp(a.overcast_horizon_ratio, b.overcast_horizon_ratio, t);
            float sunUp = Mathf.Max(0f, towardsSun.y);
            float k = model.zenith_exponent;

            // The clear sky's horizontal illuminance is what the cover keeps; the ground bounces it.
            SphericalHarmonicsL2 clearSky = Project(e => e >= 0f ? Gradient(horizon, zenith, e, k) : Color.black);
            Color clearUp = Up(clearSky);
            Color bounce = Rgb(model.ground_albedo_linear) * (sunClear * sunUp + clearUp);
            SphericalHarmonicsL2 groundSh = Project(e => e < 0f ? bounce : Color.black);

            // Overcast: the tint's gradient, scaled so that it gives back the sun the clouds hide and the clear sky
            // they cover: (1 - visibility) sun + cover clear = cover scale shape, on an up-facing normal.
            Color shapeZenith = tint, shapeHorizon = tint * ratio;
            SphericalHarmonicsL2 shapeSh = Project(e => e >= 0f ? Gradient(shapeHorizon, shapeZenith, e, k) : Color.black);
            float sunHorizontal = Luminance(sunClear) * sunUp;
            float scale = cover > 0f
                ? ((1f - visibility) * sunHorizontal + cover * Luminance(clearUp)) / (cover * Mathf.Max(1e-6f, Luminance(Up(shapeSh))))
                : 0f;
            Color skyZenith = Color.Lerp(zenith, shapeZenith * scale, cover);
            Color skyHorizon = Color.Lerp(horizon, shapeHorizon * scale, cover);
            SphericalHarmonicsL2 ambient = clearSky * (1f - cover) + shapeSh * (cover * scale) + groundSh;
            Color sun = sunClear * visibility;

            // Below the horizon the sky shows distant land, its bounce seen through the haze.
            float farTransmission = Mathf.Exp(-fogExtinctionPerKm * model.far_land_km);
            Color farLand = Color.Lerp(skyHorizon, bounce, farTransmission);

            return new LookState
            {
                SunElevationDeg = el,
                SunAzimuthDeg = az,
                TowardsSun = towardsSun,
                Sun = sun,
                SunVisibility = visibility * setting,
                Zenith = Opaque(skyZenith),
                Horizon = Opaque(skyHorizon),
                Ground = Opaque(farLand),
                Ambient = ambient,
                FogColor = Opaque(skyHorizon),
                FogExtinctionPerM = fogExtinctionPerKm / 1000f,
                FogScaleHeightM = fogScaleHeightM,
                SunHorizontal = Luminance(sun) * sunUp,
                HorizontalIlluminance = Luminance(sun) * sunUp + Luminance(Up(ambient)),
            };
        }

        /// <summary>The sky's radiance towards a direction, as the sky shader draws it without the sun's disc and halo.</summary>
        public static Color Sky(LookState state, Vector3 direction, SkyModel model)
        {
            float e = direction.normalized.y;
            return e >= 0f
                ? Gradient(state.Horizon, state.Zenith, e, model.zenith_exponent)
                : Color.Lerp(state.Horizon, state.Ground, Mathf.Clamp01(-e / model.ground_fade_sin));
        }

        /// <summary>The fog's extinction at the camera's height: the height fog integrated along a near-level ray.</summary>
        public static float FogDensity(LookState state, float cameraHeightM) =>
            state.FogExtinctionPerM * Mathf.Exp(-Mathf.Max(0f, cameraHeightM) / Mathf.Max(1f, state.FogScaleHeightM));

        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public static Color Up(SphericalHarmonicsL2 sh)
        {
            var result = new Color[1];
            sh.Evaluate(UpOnly, result);
            return result[0];
        }

        static Color Gradient(Color horizon, Color zenith, float e, float k) => Color.Lerp(horizon, zenith, Mathf.Pow(Mathf.Clamp01(e), k));

        /// <summary>
        /// Projects a radiance that depends only on the sine of the elevation onto the ambient probe, as a sum of directional
        /// lights over a grid weighted by solid angle. The sum's scale is normalised on a uniform white sky, which must
        /// light every normal 1 as Unity's flat ambient does, so the probe holds irradiance over π.
        /// </summary>
        public static SphericalHarmonicsL2 Project(Func<float, Color> radiance)
        {
            projectionNorm ??= 1f / Up(Grid(_ => Color.white)).r;
            return Grid(radiance) * projectionNorm.Value;
        }

        static SphericalHarmonicsL2 Grid(Func<float, Color> radiance)
        {
            var sh = new SphericalHarmonicsL2();
            float dTheta = Mathf.PI / Bands, dPhi = 2f * Mathf.PI / Azimuths;
            for (int i = 0; i < Bands; i++)
            {
                float theta = -0.5f * Mathf.PI + (i + 0.5f) * dTheta; // elevation
                float weight = Mathf.Cos(theta) * dTheta * dPhi;
                Color c = radiance(Mathf.Sin(theta));
                if (c.r == 0f && c.g == 0f && c.b == 0f)
                    continue;
                for (int j = 0; j < Azimuths; j++)
                {
                    float phi = (j + 0.5f) * dPhi;
                    sh.AddDirectionalLight(new Vector3(Mathf.Cos(theta) * Mathf.Sin(phi), Mathf.Sin(theta), Mathf.Cos(theta) * Mathf.Cos(phi)), c, weight);
                }
            }
            return sh;
        }

        static (SkyRow a, SkyRow b, float t) Bracket(SkyRow[] rows, float el)
        {
            if (el >= rows[0].sun_elevation_deg)
                return (rows[0], rows[0], 0f);
            for (int i = 1; i < rows.Length; i++)
                if (el >= rows[i].sun_elevation_deg)
                    return (rows[i - 1], rows[i], Mathf.InverseLerp(rows[i - 1].sun_elevation_deg, rows[i].sun_elevation_deg, el));
            SkyRow last = rows[rows.Length - 1];
            return (last, last, 0f);
        }

        static Color Rgb(float[] c) => new Color(c[0], c[1], c[2], 1f);

        static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
    }
}
