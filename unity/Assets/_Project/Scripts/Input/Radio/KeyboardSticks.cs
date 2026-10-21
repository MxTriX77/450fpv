using System;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    /// <summary>The development keyboard's keys, by what they do (the Unity side binds physical key positions).</summary>
    [Flags]
    public enum KeyboardKeys
    {
        None = 0,
        ThrottleUp = 1 << 0,
        ThrottleDown = 1 << 1,
        YawLeft = 1 << 2,
        YawRight = 1 << 3,
        PitchForward = 1 << 4,
        PitchBack = 1 << 5,
        RollLeft = 1 << 6,
        RollRight = 1 << 7,
        ArmToggle = 1 << 8,
    }

    /// <summary>
    /// The keyboard fallback's sticks (INP-06): development only, never for tuning, and the log header marks its
    /// flights (InputSource.Keyboard). A held key deflects its axis by Deflection; the throttle moves at
    /// ThrottlePerSecond while its key is held and stays where it is left; the arm key toggles on its press.
    /// </summary>
    public sealed class KeyboardSticks
    {
        /// <summary>Roll, pitch and yaw while a key is held (assumed: a key has no proportion).</summary>
        public const double Deflection = 0.5;

        /// <summary>The throttle's travel per second while W or S is held (assumed).</summary>
        public const double ThrottlePerSecond = 0.5;

        double _throttle;
        bool _armed, _armHeld;
        long _lastUs = long.MinValue;

        public void Update(KeyboardKeys keys, long hostUs, out StickSample s)
        {
            double dt = _lastUs == long.MinValue ? 0 : (hostUs - _lastUs) * 1e-6;
            _lastUs = hostUs;
            _throttle += ThrottlePerSecond * dt * (Held(keys, KeyboardKeys.ThrottleUp) - Held(keys, KeyboardKeys.ThrottleDown));
            _throttle = _throttle < 0 ? 0 : _throttle > 1 ? 1 : _throttle;
            bool arm = (keys & KeyboardKeys.ArmToggle) != 0;
            if (arm && !_armHeld) _armed = !_armed;
            _armHeld = arm;
            s = new StickSample
            {
                HostUs = hostUs,
                Roll = Deflection * (Held(keys, KeyboardKeys.RollRight) - Held(keys, KeyboardKeys.RollLeft)),
                Pitch = Deflection * (Held(keys, KeyboardKeys.PitchForward) - Held(keys, KeyboardKeys.PitchBack)),
                Yaw = Deflection * (Held(keys, KeyboardKeys.YawRight) - Held(keys, KeyboardKeys.YawLeft)),
                Throttle = _throttle,
                Arm = _armed,
            };
        }

        static int Held(KeyboardKeys keys, KeyboardKeys key) => (keys & key) != 0 ? 1 : 0;
    }
}
