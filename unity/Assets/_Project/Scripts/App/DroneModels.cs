using System;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// p5-drone's prefabs (D-050's layout), so the build carries them: D01 for each legs setting, D01_bottle and D02 (the
    /// launch stand), in the asset Resources/DroneModels, which <c>Fpv450.EditorSetup.ProjectSetup.Apply</c> makes. The
    /// flight draws the D01 for the setup's legs, the bottle on it at the payload's size, and D02 where the physics put
    /// its stand (FlightScene).
    /// </summary>
    public sealed class DroneModels : ScriptableObject
    {
        public const string ResourceName = "DroneModels";

        public GameObject legsOn, legsOff, bottle, stand;

        /// <summary>D01_bottle's origin in the drone, body m: where it sits on the upper plate, the point the mass build
        /// scales the payload about (p5-drone 1.4; drone.json's payload part, its underside).</summary>
        public static readonly Vector3 BottleSeat = new Vector3(0f, 0.028f, 0f);

        /// <summary>The payload D01_bottle is drawn at, kg (drone.json's payload part).</summary>
        public const double BottleDrawnKg = 1.0;

        public static DroneModels Load() => Resources.Load<DroneModels>(ResourceName);

        public GameObject For(bool legs) => legs ? legsOn : legsOff;

        /// <summary>D01_bottle's scale for a payload, kg: its water's, the cube root of the mass ratio (D-050), as the mass
        /// build scales the part; 0 for no payload (no bottle).</summary>
        public static float BottleScale(double payloadKg) => payloadKg > 0 ? (float)Math.Cbrt(payloadKg / BottleDrawnKg) : 0f;

        /// <summary>Where D02 (drawn as LaunchStand.Build at foot 0, yaw 0) stands for the physics' stand: the foot, the
        /// bars' centre on the ground (shape 4 less its half height), and the turn, the posts' axes (shape 0's).</summary>
        public static void StandPose(RuntimeObject stand, out Vector3 foot, out Quaternion turn)
        {
            ShapeGeometry post = stand.Shapes[0], bar = stand.Shapes[4];
            foot = V(bar.Center) - new Vector3(0f, (float)bar.HalfExtents.Y, 0f);
            turn = Quaternion.LookRotation(V(post.AxisZ), V(post.AxisY));
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
