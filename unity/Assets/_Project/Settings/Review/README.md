# Review light profiles

The fixed neutral rig of the asset review sheet (`Fpv450.Review.Capture`, change `setup-review-capture`, design decision 3). One JSON per light; the capture renders every asset in each of them. All values are **assumed** (a neutral rig, not the phase-1 look).

| Field | Meaning |
|---|---|
| `sun` | whether a directional light exists |
| `sun_elevation_deg`, `sun_azimuth_deg` | where the sun is seen: elevation above the horizon, compass bearing from +Z towards +X |
| `sun_intensity`, `sun_color_linear` | the light's intensity and colour, linear RGB |
| `shadows` | the sun casts shadows |
| `ambient_linear` | a flat (uniform) ambient, linear RGB; it is also the environment reflection |
| `background_srgb` | the camera's clear colour, sRGB |

**Equal illuminance.** `overcast` has no sun, and its ambient equals `day`'s sun on a horizontal surface plus `day`'s ambient (`intensity × sin(elevation) × colour + ambient`), so a flat surface reads the same brightness in both: 0.18 grey ground → about sRGB 118. The capture log prints each profile's horizontal illuminance.
