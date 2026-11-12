#ifndef FARM_LAND_TREE_WIND_INCLUDED
#define FARM_LAND_TREE_WIND_INCLUDED

// Wind shared by every part of a tree, so trunk, twigs, leaves and impostor cards stay joined while they
// move. The including shader declares _WindDirection, _WindSpeed, _WindGustSize, _TreeSway and _TreeFlutter
// in its UnityPerMaterial buffer. Gusts use the same bands as Farm Land/Grass, so with the same settings a
// gust bends the grass and the trees it passes over together.

// The tree's position, read straight from its matrix. Terrain trees are instanced, so this works per tree.
float3 TreeOrigin()
{
    return GetObjectToWorldMatrix()._m03_m13_m23;
}

// The whole tree bends downwind by the square of its height above the ground, so the base stays put and
// taller trees sway further. It rocks slowly at its own pace, so it keeps moving between gusts, and a
// little across the wind. Leaves and twigs also flutter in a field that varies smoothly through space, so
// parts that touch move together, and the flutter grows away from the trunk, so the trunk doesn't shake.
float3 ApplyTreeWind(float3 positionWS)
{
    float3 origin = TreeOrigin();
    float height = max(0, positionWS.y - origin.y);
    float2 direction = normalize(_WindDirection.xz);
    float2 across = float2(-direction.y, direction.x);

    float phase = (dot(origin.xz, direction) - _Time.y * _WindSpeed) / _WindGustSize;
    float gust = 0.55 + 0.45 * sin(phase * 6.2832) * sin(phase * 2.31 + 1.7);

    float random = frac(sin(dot(origin.xz, float2(39.3468, 11.1352))) * 24634.6345);
    float rock = sin(_Time.y * (1.6 + 0.8 * random) + random * 6.2832);
    float side = sin(_Time.y * (1.1 + 0.6 * random) + random * 12.566);
    float relative = height / 10;
    float bend = _TreeSway * relative * relative;
    float2 sway = (direction * (0.6 * gust + 0.4 * rock * (0.6 + 0.4 * gust)) + across * 0.2 * side) * bend;

    float reach = length(positionWS.xz - origin.xz);
    float weight = saturate(reach / 2.5) * saturate(height / 2);
    float wave = 0.6 * sin(dot(positionWS, float3(1.7, 1.1, 1.3)) + _Time.y * 5.3)
               + 0.4 * sin(dot(positionWS, float3(-1.2, 2.3, 0.7)) + _Time.y * 8.1);
    float flutter = _TreeFlutter * weight * (0.4 + 0.6 * gust) * wave;

    positionWS.xz += sway + direction * flutter * 0.5;
    positionWS.y += flutter - 0.5 * dot(sway, sway) / max(height, 0.5);
    return positionWS;
}

#endif
