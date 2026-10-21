using System;
using Fpv450.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.World
{
    /// <summary>
    /// The gallery's number keys (p1-g1-look-lock-gallery design 3): key n moves the noclip camera to
    /// <see cref="GalleryStations.Viewpoints"/>[n - 1] in the frame it is pressed, and noclip control continues from
    /// there. The camera starts at viewpoint 1, the S04 patch. A map whose scene builder writes <see cref="views"/>
    /// (the test range, p6-test-range design 8) uses those instead, and starts at its first.
    /// </summary>
    public sealed class ViewpointKeys : MonoBehaviour
    {
        /// <summary>A saved camera pose, pitch down-positive, as a scene builder writes it.</summary>
        [Serializable]
        public struct View
        {
            public string name;
            public Vector3 position;
            public float yawDeg;
            public float pitchDeg;
        }

        public NoclipCamera noclip;
        [Tooltip("Empty: the gallery's table (GalleryStations.Viewpoints).")]
        public View[] views = new View[0];

        public int Count => views.Length > 0 ? views.Length : GalleryStations.Viewpoints.Length;

        void Start() => Jump(0);

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            for (Key key = Key.Digit1; key <= Key.Digit9; key++)
                if (keyboard[key].wasPressedThisFrame && IndexOf(key, Count) >= 0)
                    Jump(IndexOf(key, Count));
        }

        /// <summary>The gallery's viewpoint a key selects: 0 for key 1, up to the table's length; -1 for any other key.</summary>
        public static int IndexOf(Key key) => IndexOf(key, GalleryStations.Viewpoints.Length);

        /// <summary>The view a key selects among <paramref name="count"/>: 0 for key 1; -1 for any other key.</summary>
        public static int IndexOf(Key key, int count)
        {
            int index = key - Key.Digit1;
            return key >= Key.Digit1 && key <= Key.Digit9 && index < count ? index : -1;
        }

        public void Jump(int index)
        {
            if (views.Length > 0)
            {
                View view = views[index];
                noclip.MoveTo(view.position, view.yawDeg, view.pitchDeg);
                return;
            }
            GalleryStations.Viewpoint viewpoint = GalleryStations.Viewpoints[index];
            noclip.MoveTo(viewpoint.Position, viewpoint.YawDeg, viewpoint.PitchDeg);
        }
    }
}
