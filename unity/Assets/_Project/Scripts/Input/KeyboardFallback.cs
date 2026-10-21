using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.Input
{
    /// <summary>
    /// The keyboard fallback (INP-06), for development only: in the editor and development builds, never in the pilot's
    /// build, and the flight session marks its flights in the log header (InputSource.Keyboard) so they are never used
    /// for tuning. W / S move the throttle, A / D yaw, the arrows pitch and roll, Enter toggles arm; the keys are
    /// physical positions, so they work on Ukrainian and Russian layouts. The session calls Poll once a frame, so the
    /// keyboard's samples come at the frame rate: unlike the radio's, which never wait for a frame (RadioReader).
    /// </summary>
    public sealed class KeyboardFallback
    {
        readonly KeyboardSticks _sticks = new KeyboardSticks();

        public static bool Allowed => Debug.isDebugBuild;

        /// <summary>Reads the keyboard and writes one sample to the flight's inbox (main thread, its only producer).</summary>
        public void Poll(StickInbox inbox)
        {
            Keyboard k = Keyboard.current;
            if (!Allowed || k == null || inbox == null) return;
            KeyboardKeys keys = KeyboardKeys.None;
            if (k.wKey.isPressed) keys |= KeyboardKeys.ThrottleUp;
            if (k.sKey.isPressed) keys |= KeyboardKeys.ThrottleDown;
            if (k.aKey.isPressed) keys |= KeyboardKeys.YawLeft;
            if (k.dKey.isPressed) keys |= KeyboardKeys.YawRight;
            if (k.upArrowKey.isPressed) keys |= KeyboardKeys.PitchForward;
            if (k.downArrowKey.isPressed) keys |= KeyboardKeys.PitchBack;
            if (k.leftArrowKey.isPressed) keys |= KeyboardKeys.RollLeft;
            if (k.rightArrowKey.isPressed) keys |= KeyboardKeys.RollRight;
            if (k.enterKey.isPressed || k.numpadEnterKey.isPressed) keys |= KeyboardKeys.ArmToggle;
            _sticks.Update(keys, HostClock.Instance.NowUs(), out StickSample s);
            inbox.TryWrite(s);
        }
    }
}
