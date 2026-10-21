using Fpv450.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.World
{
    /// <summary>
    /// The gallery's number keys (p1-g1-look-lock-gallery design 3): key n moves the noclip camera to
    /// <see cref="GalleryStations.Viewpoints"/>[n - 1] in the frame it is pressed, and noclip control continues from
    /// there. The camera starts at viewpoint 1, the S04 patch.
    /// </summary>
    public sealed class ViewpointKeys : MonoBehaviour
    {
        public NoclipCamera noclip;

        void Start() => Jump(0);

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            for (Key key = Key.Digit1; key <= Key.Digit9; key++)
                if (keyboard[key].wasPressedThisFrame && IndexOf(key) >= 0)
                    Jump(IndexOf(key));
        }

        /// <summary>The viewpoint a key selects: 0 for key 1, up to the table's length; -1 for any other key.</summary>
        public static int IndexOf(Key key)
        {
            int index = key - Key.Digit1;
            return key >= Key.Digit1 && key <= Key.Digit9 && index < GalleryStations.Viewpoints.Length ? index : -1;
        }

        public void Jump(int index)
        {
            GalleryStations.Viewpoint viewpoint = GalleryStations.Viewpoints[index];
            noclip.MoveTo(viewpoint.Position, viewpoint.YawDeg, viewpoint.PitchDeg);
        }
    }
}
