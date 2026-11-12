using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.UI
{
    /// <summary>
    /// The world map (w11-world-map 1.2, D-101): M shows and hides a panel over the output, in flight and in Explore, and
    /// nothing pauses; the panel only reads where its target is. It is centred and smaller than the window on every side
    /// (the pilot: "a little bit smaller than whole game window"), the flight showing round it: the map's top-down image
    /// (w11 1.1's <c>topdown/&lt;map-id&gt;.png</c> and <c>.json</c>) fitted into the window less <see cref="Margin"/> on
    /// every side, with the target's position and heading drawn on it every frame, and at the image's edge in its direction
    /// when it is off the image. The image is decoded once, at the scene's start, with mipmaps; without it the panel is plain,
    /// with the marker; without the transform it says so.
    /// </summary>
    public sealed class MapPanel : MonoBehaviour
    {
        /// <summary>The map's key: a keyboard key like V, R, C and X, none of the flight's (KeyboardFallback's).</summary>
        public const Key ToggleKey = Key.M;

        /// <summary>The margin round the panel, each side: this share of the window's shorter side, at least MinMarginPx.</summary>
        public const float Margin = 0.06f;
        const float MinMarginPx = 24f;
        const float BorderPx = 3f;

        /// <summary>The panel shows.</summary>
        public bool Shown { get; private set; }

        /// <summary>The map's world-to-image transform; null without its file.</summary>
        public MapTransform Map { get; private set; }

        /// <summary>The map's image; null without its file (a plain panel).</summary>
        public Texture2D Image { get; private set; }

        /// <summary>Last drawn, in GUI pixels: the panel, the image's rect in it, the marker's centre (at the edge when the
        /// target is off the image) and its heading, degrees clockwise from up.</summary>
        public Rect PanelRect { get; private set; }
        public Rect ImageRect { get; private set; }
        public Vector2 Marker { get; private set; }
        public float HeadingDeg { get; private set; }
        public bool OffImage { get; private set; }

        /// <summary>The frames the panel has drawn (Repaint), for the smoke test.</summary>
        public int Drawn { get; private set; }

        Func<string> _mapId;
        Transform _target;
        Func<bool> _hidden;
        string _id;
        float _yaw;
        static Texture2D _arrow;

        /// <summary>
        /// Puts a map panel on a scene's object. mapId is read at its Start (the scene's start, after the app has chosen
        /// the flight's world), and the image decoded there; target is what the marker follows (the drone's drawn body, or
        /// the noclip camera); hidden, when given and true, keeps the panel and its key off (the setup over the flight).
        /// </summary>
        public static MapPanel Add(GameObject on, Func<string> mapId, Transform target, Func<bool> hidden = null)
        {
            MapPanel panel = on.AddComponent<MapPanel>();
            panel._mapId = mapId;
            panel._target = target;
            panel._hidden = hidden;
            return panel;
        }

        /// <summary>The folder of the maps' images: unity/Generated/topdown in the editor, the player's
        /// StreamingAssets/generated/topdown (Fpv450.Build carries unity/Generated there).</summary>
        public static string Folder => Application.isEditor
            ? Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Generated", "topdown")
            : Path.Combine(Application.streamingAssetsPath, "generated", "topdown");

        public void Toggle() => Shown = !Shown;

        void Awake() => useGUILayout = false;

        void Start()
        {
            _id = _mapId?.Invoke();
            if (string.IsNullOrEmpty(_id))
            {
                Debug.LogError("[Map] no map id: the panel is plain");
                return;
            }
            string json = Path.Combine(Folder, _id + ".json"), png = Path.Combine(Folder, _id + ".png");
            try
            {
                Map = MapTransform.Parse(File.ReadAllText(json));
            }
            catch (Exception e)
            {
                Debug.LogError("[Map] '" + _id + "': no transform (" + e.Message + "): the panel names the map only");
                return;
            }
            if (!File.Exists(png))
            {
                Debug.LogError("[Map] '" + _id + "': no image at " + png + ": a plain panel with the marker");
                return;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "Map " + _id, hideFlags = HideFlags.DontSave };
            if (!image.LoadImage(File.ReadAllBytes(png), true))
            {
                Destroy(image);
                Debug.LogError("[Map] '" + _id + "': the image does not decode: a plain panel with the marker");
                return;
            }
            image.wrapMode = TextureWrapMode.Clamp;
            image.filterMode = FilterMode.Trilinear;
            image.anisoLevel = 4;
            Image = image;
            Debug.Log("[Map] '" + _id + "': " + image.width + "×" + image.height + " with " + image.mipmapCount + " mips, decoded in " +
                      watch.ElapsedMilliseconds + " ms; M shows it");
        }

        void OnDestroy()
        {
            if (Image != null) Destroy(Image);
        }

        void Update()
        {
            if (_hidden != null && _hidden()) return;
            Keyboard k = Keyboard.current;
            if (k != null && k[ToggleKey].wasPressedThisFrame) Toggle();
        }

        void OnGUI()
        {
            if (!Shown || Event.current.type != EventType.Repaint || (_hidden != null && _hidden())) return;
            GUI.depth = 1; // under the flight's hints, pause and summary
            float aspect = Image != null ? (float)Image.width / Image.height : Map != null ? Map.Aspect : 1f;
            PanelRect = Layout(Screen.width, Screen.height, aspect);
            ImageRect = new Rect(PanelRect.x + BorderPx, PanelRect.y + BorderPx, PanelRect.width - 2 * BorderPx, PanelRect.height - 2 * BorderPx);
            AppStyle.Fill(PanelRect, AppStyle.Panel);
            if (Image != null) GUI.DrawTexture(ImageRect, Image, ScaleMode.StretchToFill, false);
            if (Map == null)
            {
                AppStyle.Begin();
                GUI.Label(ImageRect, "No map for '" + _id + "'", AppStyle.Body);
                return;
            }
            if (_target != null)
            {
                Vector3 p = _target.position, f = _target.forward;
                if (f.x * f.x + f.z * f.z > 1e-6f) _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; // straight up or down: the last heading
                Vector2 px = Map.Pixel(p.x, p.z);
                Vector2 at = new Vector2(ImageRect.x + px.x / Map.WidthPx * ImageRect.width, ImageRect.y + px.y / Map.HeightPx * ImageRect.height);
                float size = Mathf.Max(16f, ImageRect.height * 0.03f);
                Vector2 kept = Edge(ImageRect, at, size * 0.6f);
                OffImage = kept != at;
                Marker = kept;
                HeadingDeg = _yaw;
                DrawArrow(kept, _yaw, size, OffImage ? AppStyle.Warning : AppStyle.Accent);
            }
            Drawn++;
        }

        /// <summary>The panel in a window of w × h pixels: the largest rect of the image's aspect that leaves the margin
        /// (Margin of the shorter side, at least 24 px) on every side, centred.</summary>
        public static Rect Layout(float w, float h, float aspect)
        {
            float margin = Mathf.Max(MinMarginPx, Margin * Mathf.Min(w, h));
            float pw = w - 2 * margin, ph = h - 2 * margin;
            if (pw / ph > aspect) pw = ph * aspect;
            else ph = pw / aspect;
            return new Rect(Mathf.Round((w - pw) / 2), Mathf.Round((h - ph) / 2), Mathf.Round(pw), Mathf.Round(ph));
        }

        /// <summary>p kept inside r less inset: p itself when inside, else the point on the inset rect's edge on the line
        /// from its centre to p (the target's direction from the map's middle).</summary>
        public static Vector2 Edge(Rect r, Vector2 p, float inset)
        {
            Rect inner = new Rect(r.x + inset, r.y + inset, r.width - 2 * inset, r.height - 2 * inset);
            if (inner.Contains(p)) return p;
            Vector2 c = inner.center, d = p - c;
            float s = Mathf.Min(d.x != 0 ? inner.width / 2 / Mathf.Abs(d.x) : float.MaxValue, d.y != 0 ? inner.height / 2 / Mathf.Abs(d.y) : float.MaxValue);
            return c + d * s;
        }

        static void DrawArrow(Vector2 at, float headingDeg, float size, Color fill)
        {
            if (_arrow == null) _arrow = Arrow(64);
            Matrix4x4 before = GUI.matrix;
            Color tint = GUI.color;
            GUIUtility.RotateAroundPivot(headingDeg, at); // GUI's y is down: clockwise from up, as the heading from north
            float outline = size * 1.35f;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(at.x - outline / 2, at.y - outline / 2, outline, outline), _arrow);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(at.x - size / 2, at.y - size / 2, size, size), _arrow);
            GUI.color = tint;
            GUI.matrix = before;
        }

        // A white arrowhead pointing up, its centre the texture's: the tip at the top, a notch at the base.
        static Texture2D Arrow(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[n * n];
            Vector2 tip = new Vector2(0.5f, 0.97f), left = new Vector2(0.1f, 0.05f), right = new Vector2(0.9f, 0.05f), notch = new Vector2(0.5f, 0.3f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int inside = 0;
                    for (int s = 0; s < 4; s++) // 2 × 2 samples a pixel
                    {
                        Vector2 p = new Vector2((x + 0.25f + 0.5f * (s & 1)) / n, (y + 0.25f + 0.5f * (s >> 1)) / n);
                        bool head = Side(left, tip, p) <= 0 && Side(tip, right, p) <= 0 && Side(right, left, p) <= 0;
                        bool cut = Side(left, notch, p) <= 0 && Side(notch, right, p) <= 0 && Side(right, left, p) <= 0;
                        if (head && !cut) inside++;
                    }
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
                }
            t.SetPixels32(pixels);
            t.Apply(false, true);
            return t;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    /// <summary>
    /// A map's world-to-image transform, w11 1.1's <c>&lt;map-id&gt;.json</c> (TopDownMaps): the image covers the sim's x in
    /// [x0, x1] and z in [z0, z1] metres, north (+z) up and east (+x) right. A point (x, z) is at u = (x − x0) / (x1 − x0) · W
    /// pixels from the image's left edge and v = (z1 − z) / (z1 − z0) · H from its top; a heading ψ (positive from north
    /// towards east) points along (sin ψ, −cos ψ) in (u, v).
    /// </summary>
    public sealed class MapTransform
    {
        public const string Format = "fpv450-topdown";

        public string Map;
        public float X0, X1, Z0, Z1;
        public int WidthPx, HeightPx;

        public float Aspect => (float)WidthPx / HeightPx;

        [Serializable]
        sealed class Json
        {
            public string format, map, frame;
            public float[] x_m, z_m;
            public int width_px, height_px;
        }

        public static MapTransform Parse(string json)
        {
            Json f = JsonUtility.FromJson<Json>(json);
            if (f == null || f.format != Format || f.x_m == null || f.x_m.Length != 2 || f.z_m == null || f.z_m.Length != 2 ||
                f.width_px <= 0 || f.height_px <= 0 || !(f.x_m[1] > f.x_m[0]) || !(f.z_m[1] > f.z_m[0]))
                throw new FormatException("not an " + Format + " transform");
            return new MapTransform { Map = f.map, X0 = f.x_m[0], X1 = f.x_m[1], Z0 = f.z_m[0], Z1 = f.z_m[1], WidthPx = f.width_px, HeightPx = f.height_px };
        }

        /// <summary>(x, z) in the sim's frame, m, to (u, v) image pixels from the top-left corner.</summary>
        public Vector2 Pixel(float x, float z) => new Vector2((x - X0) / (X1 - X0) * WidthPx, (Z1 - z) / (Z1 - Z0) * HeightPx);

        /// <summary>A heading ψ, degrees from north towards east, as a unit direction in (u, v).</summary>
        public static Vector2 Direction(float headingDeg)
        {
            float r = headingDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
        }
    }
}
