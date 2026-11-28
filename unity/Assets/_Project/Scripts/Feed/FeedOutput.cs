using UnityEngine;

namespace Fpv450.Feed
{
    /// <summary>
    /// What <see cref="FeedFeature"/> draws on the camera this sits on: the FPV camera's render (or a fixed frame
    /// for the A/B), this field's uniforms and the settings. <see cref="FpvCamera"/> makes the camera and fills this
    /// every frame; a camera without one is left alone by the feature.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedOutput : MonoBehaviour
    {
        /// The picture the feed reads: the FPV camera's render target, or a still.
        public Texture Scene;
        /// True when <see cref="Scene"/> already holds sRGB code values (a PNG read as linear data), as the Godot
        /// feed's screen texture did; false for a render, which the feature encodes first.
        public bool SceneEncoded;
        public FeedSettings Settings;
        public FeedUniforms Uniforms;
        /// The OSD's codes and glyph atlas (task 1.4); none leaves the grid blank.
        public Texture OsdCodes, OsdGlyphs;
        public float OsdCells = 1f;
        public Vector2 OsdCell = new Vector2(0.0350521f, 0.0657407f), OsdOrigin; // gd:42-43, Godot's defaults
        /// The OSD's stick overlay at this field (D-084), off by default: the pass draws nothing more.
        public FeedSticks Sticks;
        /// The camera's read (p7e-camera-feel): the field's track (<see cref="RollingShutter.Track"/>, 9 × 287 floats),
        /// and x = 1 to read through it (0: the old read), y = the pinhole's f in half-widths, z = the render's coverage.
        public Texture ShutterTrack;
        public Vector4 Shutter;
        /// The water on the lens (w21-rain-on-camera 1.2, FEED-13): <see cref="LensMap"/>'s texture, and whether it holds any
        /// water. Dry, the feature adds no pass and the field pass is the one before, keyword for keyword.
        public Texture LensMap;
        public bool LensWet;
        /// The glass and its pupil for the lens pass, mm: x the clear radius, y the dome's radius of curvature, z the entrance
        /// pupil's radius, w its depth behind the dome's apex (<see cref="Feed.LensMap.PupilDepth"/>).
        public Vector4 LensGlass;
    }
}
