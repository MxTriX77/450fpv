using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.App.Session;
using Fpv450.Input;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The flight session in a scene (design 6): it builds a FlightSession from the user's profile folder (the last
    /// setup, the settings, the active calibration) on the app's radio, starts a flight once the radio is connected
    /// (or after StartWaitSeconds on the development keyboard), ticks it once a frame and ends it when disabled. The
    /// scene's drone, camera and screens (p6-input-app 1.4b) read Session: its Poses, its State and its summary.
    /// </summary>
    public sealed class FlightSessionHost : MonoBehaviour
    {
        /// <summary>How long a scene waits for the radio before the keyboard flies (development builds only).</summary>
        public const float StartWaitSeconds = 1;

        float _enabledAt;

        public FlightSession Session { get; private set; }

        /// <summary>Why no flight could start, or a profile file that could not be read; empty when all is well.</summary>
        public readonly List<string> Problems = new List<string>();

        void OnEnable()
        {
            _enabledAt = Time.unscaledTime;
            Problems.Clear();
            try
            {
                DroneSpec drone = DroneSpec.Parse(File.ReadAllText(RepositoryFile("assets-src/drone/drone.json")));
                MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(RepositoryFile(PhysicsMaterials.RelativePath)), out ulong hash);
                SessionInputs inputs = SessionInputs.FromProfile(Application.persistentDataPath, drone, materials, hash, Problems);
                inputs.Reader = AppRadio.Reader;
                inputs.KeyboardAllowed = KeyboardFallback.Allowed;
                inputs.Build = Application.version;
                Session = new FlightSession(inputs);
            }
            catch (Exception e)
            {
                Problems.Add(e.Message);
                Debug.LogWarning("[FlightSessionHost] " + e.Message);
            }
        }

        void Update()
        {
            if (Session == null) return;
            if (Session.State == SessionState.Ready && (AppRadio.Reader.Connected || Time.unscaledTime - _enabledAt >= StartWaitSeconds))
            {
                try
                {
                    Session.Start();
                }
                catch (InvalidOperationException e)
                {
                    Problems.Add(e.Message);
                    Session.Dispose();
                    Session = null;
                    return;
                }
            }
            Session.Tick(KeyboardFallback.ReadKeys());
        }

        void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }

        public void Pause() => Session?.Pause();
        public void Resume() => Session?.Resume();
        public void Restart() => Session?.Restart();

        // drone.json and physics_materials.json from the repository, beside unity/ (the editor's case; the built player's
        // copy comes with 1.4b).
        static string RepositoryFile(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", relative));
    }
}
