using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.App.Session;
using Fpv450.Input;
using Fpv450.Input.Radio;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The flight session in a scene (design 6): it builds a FlightSession from the user's profile folder (the last
    /// setup, the settings, the active calibration) on the app's radio over the test range (FlightWorlds.TestRange),
    /// starts a flight once the radio is connected (or after StartWaitSeconds on the development keyboard), ticks it
    /// once a frame and ends it when disabled. FlightScene draws it: Session's Poses, State and summary. Without a radio
    /// where the keyboard may not fly, it waits for one; it never gives up on the pilot's radio.
    /// </summary>
    public sealed class FlightSessionHost : MonoBehaviour
    {
        /// <summary>How long a scene waits for the radio before the keyboard flies (development builds only).</summary>
        public const float StartWaitSeconds = 1;

        float _enabledAt;

        public FlightSession Session { get; private set; }

        /// <summary>What the session flies; set Inputs.Choices before a flight started from the summary's setup.</summary>
        public SessionInputs Inputs { get; private set; }

        /// <summary>The keyboard fallback's keys each frame: the physical keys, or a smoke test's script.</summary>
        public Func<KeyboardKeys> Keys = KeyboardFallback.ReadKeys;

        /// <summary>Why no flight could start, or a profile file that could not be read; empty when all is well.</summary>
        public readonly List<string> Problems = new List<string>();

        /// <summary>No radio is connected and the keyboard may not fly: the scene asks for the radio.</summary>
        public bool WaitingForRadio => Session != null && Session.State == SessionState.Ready && !AppRadio.Reader.Connected && !Inputs.KeyboardAllowed;

        void OnEnable()
        {
            _enabledAt = Time.unscaledTime;
            Problems.Clear();
            try
            {
                DroneSpec drone = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
                MaterialRow[] materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong hash);
                SessionInputs inputs = SessionInputs.FromProfile(Application.persistentDataPath, drone, materials, hash, Problems);
                FlightWorlds.TestRange(AppData.Root).ApplyTo(inputs);
                inputs.Reader = AppRadio.Reader;
                inputs.KeyboardAllowed = KeyboardFallback.Allowed;
                inputs.Build = Application.version;
                Inputs = inputs;
                Session = new FlightSession(inputs);
            }
            catch (Exception e)
            {
                Problems.Add(e.Message);
                Debug.LogError("[FlightSessionHost] " + e.Message);
            }
        }

        void Update()
        {
            if (Session == null) return;
            if (Session.State == SessionState.Ready
                && (AppRadio.Reader.Connected || (Inputs.KeyboardAllowed && Time.unscaledTime - _enabledAt >= StartWaitSeconds)))
                Fly();
            Session?.Tick(Keys());
        }

        void OnDisable()
        {
            Session?.Dispose();
            Session = null;
        }

        /// <summary>A new flight, after the last has ended (the summary's Fly again) or before the first.</summary>
        public void Fly()
        {
            if (Session == null || Session.State == SessionState.Flying || Session.State == SessionState.Paused) return;
            try
            {
                Session.Start();
                Logged();
            }
            catch (Exception e)
            {
                Problems.Add(e.Message);
                Debug.LogError("[FlightSessionHost] " + e.Message);
                Session.Dispose();
                Session = null;
            }
        }

        public void Pause() => Session?.Pause();
        public void Resume() => Session?.Resume();

        /// <summary>Ends the flight by the pilot and starts a new one from the start point (APP-04).</summary>
        public void Restart()
        {
            if (Session == null) return;
            Session.Restart();
            Logged();
        }

        void Logged() =>
            Debug.Log("[Flight] flight " + Session.Flights + " at '" + Session.StartPoint.Name + "' on " + Session.Input + ", legs "
                      + (Session.Choices.Legs ? "on" : "off") + ", payload " + Session.Choices.Payload.ToString("0.0") + " kg, wind " + Session.Choices.Wind
                      + ", seed 0x" + Session.Seed.ToString("X16") + ", log " + Session.LogPath);
    }
}
