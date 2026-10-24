using Fpv450.Feed;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.World
{
    /// <summary>
    /// The gallery's function keys (p1-g1-look-lock-gallery design 4): F1 day, F2 low sun, F3 overcast, F4 night,
    /// each through <see cref="LookSetup.Select"/>, which also shows the preset's name for a second.
    /// </summary>
    public sealed class LightPresets : MonoBehaviour
    {
        static readonly Key[] Keys = { Key.F1, Key.F2, Key.F3, Key.F4 };

        public LookSetup look;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            for (int i = 0; i < Keys.Length && i < look.PresetCount; i++)
                if (keyboard[Keys[i]].wasPressedThisFrame)
                    look.Select(i);
        }
    }
}
