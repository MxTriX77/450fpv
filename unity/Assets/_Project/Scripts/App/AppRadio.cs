using Fpv450.Input.Radio;
using Fpv450.Sim.Flight;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fpv450.App
{
    /// <summary>
    /// The app's one radio reader (design 1), shared by the Calibrate screen (CalibrateScreen.Reader) and the flight
    /// session: its input thread starts on first use and stops when the app quits, and in the editor before a domain
    /// reload too, so no input thread outlives the domain that started it.
    /// </summary>
    public static class AppRadio
    {
        static RadioReader _reader;

        public static RadioReader Reader
        {
            get
            {
                if (_reader != null) return _reader;
                _reader = new RadioReader(() => WinHid.OpenFirst(HostClock.Instance));
                _reader.Start();
                Application.quitting += Stop;
#if UNITY_EDITOR
                AssemblyReloadEvents.beforeAssemblyReload += Stop;
#endif
                return _reader;
            }
        }

        /// <summary>The smoke test's: from now on a reader that finds no device, so a radio left plugged in never flies
        /// the smoke test's scripted flights (they fly on the development keyboard, or wait for the radio).</summary>
        public static void Unplug()
        {
            Stop();
            _reader = new RadioReader(() => null);
            _reader.Start();
            Application.quitting += Stop;
        }

        /// <summary>Stops the input thread and closes the radio; the next use starts them again.</summary>
        public static void Stop()
        {
            Application.quitting -= Stop;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
#endif
            _reader?.Stop();
            _reader = null;
        }
    }
}
