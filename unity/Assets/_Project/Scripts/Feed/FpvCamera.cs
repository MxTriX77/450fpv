using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Fpv450.Feed
{
    /// <summary>
    /// The FPV camera rig (p4-feed design 3, FEED-02, D-017): this camera renders the scene into its own target at
    /// <see cref="FeedSettings.SceneSize"/> with the pinned intrinsics and the lens's extra field of view, and a child
    /// output camera draws the analog feed from it to the screen through <see cref="FeedFeature"/>. URP has no
    /// per-camera render scale, so the render's own size needs its own target, and the output camera carries the feed
    /// to the screen at the screen's size. The feed advances at 50 fields/s, as the Godot feed's
    /// <c>VideoFeed._Process</c> did.
    /// </summary>
    /// <remarks>
    /// The intrinsics: a pinhole of <see cref="FeedSettings.focalHalfWidths"/> half-widths (f ≈ 1.005) at 0° uptilt,
    /// the camera looking straight along this transform's forward, native 16:9 with square pixels. With the lens on,
    /// the render covers <see cref="FeedSettings.Coverage"/> half-widths so the barrel-distorted picture fills to its
    /// corners and spans about 106° across. The rig owns this camera's field of view and target while it runs, and
    /// gives them back when it stops.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class FpvCamera : MonoBehaviour
    {
        /// The feed's field rate: PAL, 50 fields/s (FEED-02, VideoFeed.cs:125).
        public const double FieldsPerSecond = 50.0;

        public FeedSettings settings;
        [Tooltip("Pins the feed to this field for a reproducible still (the Godot feed's --video-field); -1 runs at 50 fields/s.")]
        public int pinnedField = -1;

        /// The feed's causes. Labelled stubs until the physics publishes them (FEED-03).
        public IFeedSignals Signals { get; set; } = new StubFeedSignals();
        /// The scheduler: the place a terminal loss is fired from (<see cref="FeedEvents.TriggerCut"/>).
        public FeedEvents Events { get; private set; }
        /// The camera that draws the feed to the screen; a capture points its target elsewhere.
        public Camera Output { get; private set; }
        public FeedOutput Feed { get; private set; }
        public bool Running => Output != null;

        Camera fpv;
        RenderTexture scene;
        float savedFov;
        RenderTexture savedTarget;
        double clock;

        // A rig added from code (NoclipFeed) gets its settings and signals after AddComponent, so it begins at Start.
        void OnEnable()
        {
            if (settings != null)
                Begin();
        }

        void Start() => Begin();

        void OnDisable() => End();

        void LateUpdate()
        {
            if (!Running)
                return;
            clock += Time.deltaTime;
            Step(pinnedField >= 0 ? pinnedField : (int)(clock * FieldsPerSecond));
        }

        /// Starts the rig: the camera's intrinsics and reduced target, the output camera, the scheduler. OnEnable
        /// calls it; an edit-mode capture calls it directly.
        public void Begin()
        {
            if (Running)
                return;
            if (settings == null)
            {
                Debug.LogError($"[Feed] {name}: FpvCamera has no FeedSettings; the feed is off.", this);
                return;
            }
            fpv = GetComponent<Camera>();
            savedFov = fpv.fieldOfView;
            savedTarget = fpv.targetTexture;

            Vector2Int size = settings.SceneSize;
            // With a depth buffer: URP's render graph wants one on a camera target, and warns every frame without it.
            // The feed reads the colour alone (FeedFeature imports it by its identifier).
            scene = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "FpvScene",
                antiAliasing = 1, // MSAA 1× (p1-g1-look-lock-gallery task 1.1)
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            scene.Create();
            fpv.targetTexture = scene;
            fpv.fieldOfView = settings.RenderVerticalFovDeg;

            var go = new GameObject("FeedOutput") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            Output = go.AddComponent<Camera>();
            Output.cullingMask = 0;
            Output.clearFlags = CameraClearFlags.SolidColor;
            Output.backgroundColor = Color.black;
            Output.allowHDR = false; // an 8-bit sRGB colour keeps the feed's code values exact
            Output.allowMSAA = false;
            Output.depth = fpv.depth + 1f;
            UniversalAdditionalCameraData data = Output.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = false;
            Feed = go.AddComponent<FeedOutput>();
            Feed.Settings = settings;
            Feed.Scene = scene;
            Feed.SceneEncoded = false;

            // The clock runs on from where the rig last stopped, so switching the feed off and on resumes the schedule.
            Events = new FeedEvents(settings.seed);
            Step(pinnedField >= 0 ? pinnedField : (int)(clock * FieldsPerSecond));
            Debug.Log($"[Feed] FPV camera: {size.x}x{size.y} render, {settings.RenderVerticalFovDeg:0.00}° vertical, " +
                      $"picture {settings.PictureHorizontalFovDeg:0.0}° across, lens k1 {settings.K1:0.00}, " +
                      $"field {settings.fieldWidth}x{FeedSettings.FieldRows}, seed {settings.seed}. {FeedSignalLog.Describe(Signals)}");
        }

        /// Stops the rig and gives the camera back.
        public void End()
        {
            if (!Running)
                return;
            fpv.targetTexture = savedTarget;
            fpv.fieldOfView = savedFov;
            DestroyNow(Output.gameObject);
            Output = null;
            Feed = null;
            scene.Release();
            DestroyNow(scene);
            scene = null;
        }

        /// Puts the feed at <paramref name="field"/>, replaying from the seed when it has to go back, and fills this
        /// field's uniforms.
        public void Step(int field)
        {
            if (field < Events.Field)
                Events = new FeedEvents(settings.seed);
            Events.AdvanceTo(field, FeedSignalScale.MotorCurrent(Signals.MotorCurrentTotalA));
            Feed.Uniforms = FeedUniforms.From(Events, settings.seed, settings.gain, Signals);
        }

        /// Feeds a fixed frame instead of the render (the still A/B's identical input frame, FEED-00): `encoded` when
        /// it holds sRGB code values, as a PNG read as linear data does.
        public void ShowStill(Texture still, bool encoded)
        {
            Feed.Scene = still;
            Feed.SceneEncoded = encoded;
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
