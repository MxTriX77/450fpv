# Light presets and the sky model

The look setup of the G1 gallery (`Scripts/Feed/LookSetup.cs`, `LookModel.cs`, `Shaders/GradientSky.shader`; change `p1-g1-look-lock-gallery`, design 4). One JSON per preset here, and the sky model they share in `../Sky/sky_model.json`. The review capture keeps its own rig (`../Review/`), so its sheets stay comparable.

## Presets (`<name>.json`, keys F1-F4 in the gallery)

| Field | Meaning |
|---|---|
| `name`, `label` | the file's name, and the text shown on screen for one second when it is selected |
| `letters` | the reference letters the preset copies |
| `hour` | local solar time, hours |
| `cloud_cover` | 0 clear to 1 overcast: mixes the clear sky towards the overcast gradient; the sun dims from `sun_hidden_from_cover` and is gone at 1 |
| `fog_extinction_per_km` | the haze's extinction at the ground, per km (Koschmieder: visibility about 3.9 / this) |
| `fog_scale_height_m` | the height over which the haze thins by e; the fog's density follows the camera's height |

| Preset | Hour | Cover | Sun | Haze | Letters |
|---|---|---|---|---|---|
| day | 12.0 | 0.2 | 45.0° up, due south | 0.20 /km | C, M |
| low sun | 17.5 | 0.1 | 5.3° up, west, warm | 0.25 /km | G, B |
| overcast | 12.0 | 1.0 | none | 0.30 /km | A, H |
| night | 23.0 | 1.0 | none (moonless) | 0.30 /km | L |

## The sky model (`../Sky/sky_model.json`)

Every colour is linear RGB in the review rig's units: a 0.18 grey under its day profile reads about sRGB 118. They are the light as the eye or the camera has adapted to it; the feed's own exposure (C1) comes in phase 4.

| Field | Meaning |
|---|---|
| `latitude_deg`, `declination_deg` | the solar arc: 45° and the equinox (assumed: noon at 45°, as the review rig; no real location) |
| `zenith_exponent` | the sky from horizon to zenith follows sin(elevation) to this power |
| `ground_fade_sin` | below the horizon the sky fades to the distant land over this sine of the depression |
| `ground_albedo_linear` | the ground's albedo: its bounce lights the lower hemisphere of the ambient, and the distant land is that bounce seen through `far_land_km` of haze |
| `sun_hidden_from_cover` | the cloud cover from which the sun dims |
| `sun_disc_radius_deg`, `sun_disc_gain`, `sun_halo_gain` | the disc's angular radius, its radiance and the halo's, as multiples of the sun |
| `rows` | the light at a few sun elevations, highest first, interpolated linearly between them: `sun_linear` (the light's colour times its intensity), the clear sky's `zenith_linear` and `horizon_linear`, and the overcast's tint `overcast_linear` with its horizon-over-zenith `overcast_horizon_ratio` |

**Cloud cover keeps the horizontal illuminance** (the review rig's rule): the overcast gradient's level is set so that the sky gives back the sun the clouds hide and the clear sky they cover, on an up-facing surface. Day and overcast therefore light a horizontal grey alike, and night's overcast keeps the clear night's level. **The ambient** is the sky (and the ground's bounce) projected onto the ambient probe; **the reflection** is the same gradient in a 32 px cubemap; **the fog** is URP's exponential fog in the horizon's colour.

**Where the values come from.** The 45° row's sun is the review rig's day sun; its sky is tuned so that sun plus sky light a flat surface as the rig's day does (0.976). The low-sun row copies G and B's look (a deep blue zenith over a near-white horizon, warm raking light), taken nearer neutral than the feed shows it, since the feed's warm, saturated colour is phase 4's. The night row is set so that night's overcast horizon reads about sRGB 93 over near-black ground, as L. The overcast tint is J's pale, slightly cool sky; its gradient is near flat (H and J; P is darker at the horizon). The haze is weak, as the footage measures it (A: about 0.2 /km; P under rain at most 0.29 /km): a belt at 200 m keeps over 94 % of its contrast. Rows between 5° and -18° are interpolated and not judged: no letter shows dusk.
