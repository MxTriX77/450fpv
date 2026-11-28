using UnityEngine;

namespace Fpv450.Feed
{
    /// <summary>
    /// The wet look's shaders as a player carries them (w21-rain-on-camera 1.3): <c>Settings/Wet/Resources/WetShaders.asset</c>,
    /// written by <c>WetVariants</c> before every build. No material in the build uses the wet copies, so a player would
    /// hold none of them, nor any of their material keywords' variants: this asset references them and the variants the
    /// project's URP/Lit materials and terrains need (<see cref="variants"/>), so a twin draws with its normal, occlusion
    /// and metallic maps as its original does.
    /// </summary>
    public sealed class WetShaders : ScriptableObject
    {
        public const string ResourceName = "WetShaders";

        public Shader lit, terrain, terrainAdd, terrainBase, extinction;
        public ShaderVariantCollection variants;

        public static WetShaders Load() => Resources.Load<WetShaders>(ResourceName);
    }
}
