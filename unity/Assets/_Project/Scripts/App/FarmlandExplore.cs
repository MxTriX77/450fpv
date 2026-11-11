using System;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450.App
{
    /// <summary>
    /// The farmland in noclip (w1-farmland-world 2.1, design 6): the friend's scene as it is, its camera driven by our
    /// noclip (PRD-07) in place of the friend's FreeFlyCamera, with the feed and its OSD as on the test range (NoclipFeed:
    /// V, O), and the launch stand (D02) at the village's start point on the terrain. The camera starts behind the stand,
    /// looking along its heading. No flight yet: the sim gets the farmland's hills with W3.
    /// </summary>
    public static class FarmlandExplore
    {
        public const string Scene = "FarmLand";

        /// <summary>The noclip camera's start: this far behind the stand, along its heading, and this high over its foot, m.</summary>
        const float CameraBack = 3f, CameraUp = 1.6f;

        /// <summary>Sets the loaded farmland up for noclip and returns the stand; null, with an error logged, when the scene
        /// lacks its camera or the stand can't be placed.</summary>
        public static Transform Open(Scene scene)
        {
            Camera camera = Camera.main;
            if (camera == null || camera.gameObject.scene != scene)
            {
                Debug.LogError("[App] the farmland has no main camera");
                return null;
            }
            // The friend's FreeFlyCamera (Assembly-CSharp, so by name): ours moves the camera.
            foreach (MonoBehaviour b in camera.GetComponents<MonoBehaviour>())
                if (b.GetType().Name == "FreeFlyCamera") b.enabled = false;
            NoclipCamera noclip = camera.gameObject.AddComponent<NoclipCamera>();
            camera.gameObject.AddComponent<NoclipFeed>();
            Transform stand = PlaceStand(scene, FlightWorlds.FarmlandStart);
            if (stand == null) return null;
            float yaw = (float)(FlightWorlds.FarmlandStart.Yaw * 180 / Math.PI);
            Vector3 eye = stand.position - stand.forward * CameraBack + Vector3.up * CameraUp;
            noclip.MoveTo(eye, yaw, Mathf.Atan2(CameraUp, CameraBack) * Mathf.Rad2Deg);
            return stand;
        }

        /// <summary>D02 at the start, as the flight draws the physics' stand: its foot on the terrain under the start's
        /// centre, turned to its heading. Null, with an error logged, without DroneModels' stand or a terrain there.</summary>
        public static Transform PlaceStand(Scene scene, StartPoint start)
        {
            DroneModels models = DroneModels.Load();
            if (models == null || models.stand == null)
            {
                Debug.LogError("[App] no Resources/" + DroneModels.ResourceName + " with D02: run Fpv450.EditorSetup.ProjectSetup.Apply");
                return null;
            }
            if (!Ground(scene, (float)start.X, (float)start.Z, out float ground))
            {
                Debug.LogError("[App] the farmland has no terrain under its start '" + start.Name + "'");
                return null;
            }
            GameObject stand = UnityEngine.Object.Instantiate(models.stand);
            SceneManager.MoveGameObjectToScene(stand, scene);
            stand.name = models.stand.name;
            stand.transform.SetPositionAndRotation(new Vector3((float)start.X, ground, (float)start.Z),
                Quaternion.Euler(0f, (float)(start.Yaw * 180 / Math.PI), 0f));
            return stand.transform;
        }

        /// <summary>The terrain's height at (x, z), m, from the scene's terrain tile that covers the point.</summary>
        public static bool Ground(Scene scene, float x, float z, out float height)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>())
                {
                    Vector3 o = t.transform.position, size = t.terrainData != null ? t.terrainData.size : Vector3.zero;
                    if (x < o.x || z < o.z || x > o.x + size.x || z > o.z + size.z) continue;
                    height = t.SampleHeight(new Vector3(x, 0f, z)) + o.y;
                    return true;
                }
            height = 0f;
            return false;
        }
    }
}
