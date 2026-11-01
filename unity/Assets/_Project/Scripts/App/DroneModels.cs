using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// p5-drone's D01 prefabs (D-050's layout), one for each legs setting, so the build carries them: the asset
    /// Resources/DroneModels, which <c>Fpv450.EditorSetup.ProjectSetup.Apply</c> makes. The flight draws the one for the
    /// setup's legs (FlightScene).
    /// </summary>
    public sealed class DroneModels : ScriptableObject
    {
        public const string ResourceName = "DroneModels";

        public GameObject legsOn, legsOff;

        public static DroneModels Load() => Resources.Load<DroneModels>(ResourceName);

        public GameObject For(bool legs) => legs ? legsOn : legsOff;
    }
}
