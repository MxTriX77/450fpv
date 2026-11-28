// The wet look's law (w21-rain-on-camera 1.3, VIS-03, D-126): a wetted surface darkens by Lekner and Dorf's
// internal reflection (Appl. Opt. 27, 1278, 1988), from its own albedo and water's index; the numbers and their
// labels are in the change's design (1.3) and WetOptics.cs, which computes them and sets the globals below.
//
// A wetted rough surface lies under water whose top is the air: light reflected diffusely by the surface's grains
// meets that top from inside, where a share p of it (water's internal reflectance for diffuse light, total internal
// reflection beyond the critical angle and Fresnel inside it) goes back down to the grains, which absorb a share
// again. Per colour channel, with A the surface's drawn diffuse albedo (BRDFData.diffuse, or a Lambert shader's
// albedo) and a_d = 1 - A its absorption per bounce:
//   - the grains under water absorb more, their own reflection lowered by the water against them (LD eq. 10-11):
//       a_w = a_d * (c * (1 - a_d) + a_d),   c = (1 - Rbar(n_r / n_l)) / (1 - Rbar(n_r))
//   - the light leaving the water after every bounce (LD eq. 1, its diffuse part; the top's own reflection is the
//     specular term's, water's F0):
//       D = T * (1 - a_w) * (1 - p) / (1 - p * (1 - a_w)),   T = 1 - Rbar(n_l),   p = 1 - (1 - Rbar(n_l)) / n_l^2
// A metal (A = 0) keeps no body to darken. Darker channels darken more, so the colour holds (K§8.7 R4).
//
// Everything sits under FPV450_WET: a shader built without the keyword is token for token what it was, so Sunny
// draws as before (WetLook never sets the keyword in Sunny). Include it anywhere after Core.hlsl.
#ifndef FPV450_WET_LAW_INCLUDED
#define FPV450_WET_LAW_INCLUDED

#if defined(FPV450_WET)

// Set by WetLook.Apply (Scripts/Feed/WetLook.cs); globals, so every material reads the same rain.
// _Fpv450Wet:      x 1 while the surfaces are wet (Rain, Severe rain), 0 dry (the identity, for the captures' dry
//                  reference); y 1 in Severe rain (standing water); z the captures' probe (0 none, 1 no specular,
//                  2 a white body and no specular: the diffuse light alone); w unused.
// _Fpv450WetWater: x T, y p, z c, w water's F0 = ((n_l - 1) / (n_l + 1))^2.
// _Fpv450WetPond:  x the standing water's smoothness, y the cosine of the steepest slope that holds it, zw unused.
float4 _Fpv450Wet;
float4 _Fpv450WetWater;
float4 _Fpv450WetPond;

// Hard surfaces (the sim's rigid materials) carry this rendering-layer bit while it rains (WetLook.HardLayer).
#define FPV450_WET_HARD_LAYER (1u << 7)

// The wetted body of a drawn diffuse albedo (the law above), per channel.
float3 Fpv450WetBodyLaw(float3 albedo)
{
    float3 ad = 1.0 - saturate(albedo);
    float3 rw = 1.0 - ad * (_Fpv450WetWater.z * (1.0 - ad) + ad); // the grains' reflectance under water, 1 - a_w
    float p = _Fpv450WetWater.y;
    return _Fpv450WetWater.x * rw * (1.0 - p) / (1.0 - p * rw);
}

// A Lambert shader's albedo as it is drawn: wetted while it rains, a white body under the captures' second probe.
half3 Fpv450WetAlbedo(half3 albedo)
{
    half3 a = _Fpv450Wet.x > 0.5 ? (half3)Fpv450WetBodyLaw(albedo) : albedo;
    return _Fpv450Wet.z > 1.5 ? half3(1.0, 1.0, 1.0) : a;
}

#endif // FPV450_WET
#endif // FPV450_WET_LAW_INCLUDED
