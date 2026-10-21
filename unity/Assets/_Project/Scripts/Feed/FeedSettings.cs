using System;
using UnityEngine;

namespace Fpv450.Feed
{
    /// <summary>
    /// The feed's settings asset (p4-feed design 1, FEED-09): the FPV camera's intrinsics and render size, the
    /// analog pass's field size, the seed, and the stand-in gain. The per-field uniforms come from
    /// <see cref="FeedEvents"/> and <see cref="IFeedSignals"/> through <see cref="FeedUniforms"/>; the shader's own
    /// constants stay literal in <c>Shaders/Feed.shader</c>, each with its Godot line.
    /// </summary>
    [CreateAssetMenu(menuName = "450fpv/Feed Settings", fileName = "FeedSettings")]
    public sealed class FeedSettings : ScriptableObject
    {
        [Tooltip("The feed's seed: the whole feed is a function of (seed, field), as in the Godot feed.")]
        public int seed = 1;

        [Header("The FPV camera (FEED-02)")]
        [Tooltip("O1 barrel distortion on the scene read; off reads the scene as the Godot feed did.")]
        public bool lens = true;
        [Tooltip("O1's k1, p_u = p_d (1 + k1 r_d²) with r normalised to the half-width (K§11.4: 0.33, measured 0.30-0.34).")]
        public float k1 = 0.33f;
        [Tooltip("The pinhole's focal length in half-widths, before k1 (FEED-02: f ≈ 1.005, about 106° across the picture).")]
        public float focalHalfWidths = 1.005f;
        [Tooltip("The scene render's samples across the picture's width at its centre; the render is this times the lens's coverage wide. 1920 is the Godot feed's full-resolution read: D-017's reduced render failed the still A/B (p4-feed task 1.2).")]
        public int sceneWidth = 1920;

        [Header("The analog pass")]
        [Tooltip("Columns of the field texture. 1920 evaluates the Godot shader at every 1080p column, as Godot did.")]
        public int fieldWidth = 1920;
        [Tooltip("The camera's AE gain, 1 by day (the Godot stand-in's). Task 1.7's AE loop replaces it.")]
        public float gain = 1f;

        /// The picture's aspect: native 16:9 with square pixels (FEED-02).
        public const float Aspect = 16f / 9f;
        /// Rows of the field texture: the 286 field lines plus the half line an odd field starts with (gd:169).
        public const int FieldRows = 287;

        /// The lens's k1, or 0 when it is off.
        public float K1 => lens ? k1 : 0f;

        /// How many half-widths of the pinhole image the render must cover so the barrel-distorted picture's
        /// corners are filled: 1 + k1 r² at the corner, r² = 1 + (9/16)². 1.434 at k1 = 0.33 (K§11.4's "about
        /// 1.33× at the edge" is the same mapping read at the edge's middle).
        public float Coverage => (float)(1.0 + K1 * (1.0 + 1.0 / (Aspect * (double)Aspect)));

        /// The render's vertical field of view in degrees: the pinhole covering <see cref="Coverage"/> half-widths.
        public float RenderVerticalFovDeg
            => (float)(2.0 * Math.Atan(Coverage / Aspect / focalHalfWidths) * 180.0 / Math.PI);

        /// The picture's horizontal field of view in degrees: where its edge (r_d = 1) lands, p_u = 1 + k1.
        public float PictureHorizontalFovDeg => (float)(2.0 * Math.Atan((1.0 + K1) / focalHalfWidths) * 180.0 / Math.PI);

        /// The scene render's size in pixels, 16:9.
        public Vector2Int SceneSize
        {
            get
            {
                int w = Mathf.Max(16, Mathf.RoundToInt(sceneWidth * Coverage));
                return new Vector2Int(w, Mathf.Max(9, Mathf.RoundToInt(w / Aspect)));
            }
        }

        /// The shader's <c>_Lens</c>: k1, the coverage and the aspect.
        public Vector4 LensVector => new Vector4(K1, Coverage, Aspect, 0f);
    }
}
