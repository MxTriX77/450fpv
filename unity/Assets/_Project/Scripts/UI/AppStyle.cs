using UnityEngine;

namespace Fpv450.UI
{
    /// <summary>
    /// The app's one look (APP-02, PRD-05: "prettier than uglier", clean and legible, nothing extra): a dark slate
    /// ground, white and grey text, one warm accent for what is chosen or under the mouse, flat buttons. IMGUI, sized
    /// for 1080 lines and scaled to the screen's height. Call Begin at the top of every OnGUI.
    /// </summary>
    public static class AppStyle
    {
        public static readonly Color Ground = new Color(0.075f, 0.085f, 0.10f);
        public static readonly Color GroundLow = new Color(0.035f, 0.04f, 0.05f);
        public static readonly Color Panel = new Color(0.11f, 0.125f, 0.145f, 0.94f);
        public static readonly Color Control = new Color(0.17f, 0.19f, 0.215f);
        public static readonly Color ControlHover = new Color(0.22f, 0.245f, 0.275f);
        public static readonly Color Accent = new Color(0.91f, 0.64f, 0.24f);
        public static readonly Color Text = new Color(0.93f, 0.94f, 0.95f);
        public static readonly Color Muted = new Color(0.58f, 0.62f, 0.67f);
        public static readonly Color Warning = new Color(0.95f, 0.45f, 0.35f);

        static int _height;
        static float _scale = 1;
        static GUISkin _skin;
        static Texture2D _gradient;

        /// <summary>Design pixels (at 1080 lines) to screen pixels.</summary>
        public static float S(float px) => Mathf.Round(px * _scale);

        public static GUIStyle Title, Heading, Body, Small, Label, Value, MenuItem, Segment, SegmentOn, Primary, Secondary, Pill;

        /// <summary>Sets the skin for this OnGUI call, rebuilding it when the screen's height changes.</summary>
        public static void Begin()
        {
            if (_skin == null || _height != Screen.height) Build();
            GUI.skin = _skin;
        }

        /// <summary>The full-screen ground: a slow vertical fall from slate to near black.</summary>
        public static void DrawGround()
        {
            if (_gradient == null)
            {
                _gradient = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < 64; y++) _gradient.SetPixel(0, y, Color.Lerp(GroundLow, Ground, y / 63f));
                _gradient.Apply(false, true);
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _gradient);
        }

        /// <summary>
        /// A text colour as IMGUI needs it to draw c. In this Linear-colour-space project IMGUI takes a style's text
        /// colour as linear and draws it darker, each channel to the power 2.2, while the fills keep their values; so text
        /// colours are given as c.gamma (QA's finding 18: Muted on the panel at its designed 6.3:1, not 2.2:1).
        /// </summary>
        public static Color Ink(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.gamma : c;

        /// <summary>A flat rectangle of a colour.</summary>
        public static void Fill(Rect r, Color c)
        {
            Color was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = was;
        }

        /// <summary>A horizontal choice of segments; returns the chosen index.</summary>
        public static int Segments(int selected, string[] names, float height)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
                if (GUILayout.Button(names[i], i == selected ? SegmentOn : Segment, GUILayout.Height(height), GUILayout.MinWidth(S(120))))
                    selected = i;
            GUILayout.EndHorizontal();
            return selected;
        }

        static void Build()
        {
            _height = Screen.height;
            _scale = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);
            if (_skin != null) Object.Destroy(_skin);
            _skin = Object.Instantiate(GUI.skin);
            _skin.hideFlags = HideFlags.DontSave;

            Title = Text0(_skin.label, 64, FontStyle.Bold, Text);
            Heading = Text0(_skin.label, 34, FontStyle.Bold, Text);
            Body = Text0(_skin.label, 22, FontStyle.Normal, Text);
            Body.wordWrap = true;
            Small = Text0(_skin.label, 17, FontStyle.Normal, Muted);
            Small.wordWrap = true;
            Label = Text0(_skin.label, 21, FontStyle.Normal, Muted);
            Label.alignment = TextAnchor.MiddleLeft;
            Value = Text0(_skin.label, 22, FontStyle.Bold, Text);
            Value.alignment = TextAnchor.MiddleCenter;

            MenuItem = Flat(30, FontStyle.Normal, Text, Accent, Color.clear, new Color(1, 1, 1, 0.04f));
            MenuItem.alignment = TextAnchor.MiddleLeft;
            MenuItem.padding = new RectOffset((int)S(28), (int)S(12), 0, 0);
            Segment = Flat(20, FontStyle.Normal, Text, Text, Control, ControlHover);
            SegmentOn = Flat(20, FontStyle.Bold, GroundLow, GroundLow, Accent, Accent);
            Primary = Flat(26, FontStyle.Bold, GroundLow, GroundLow, Accent, Color.Lerp(Accent, Color.white, 0.18f));
            Secondary = Flat(22, FontStyle.Normal, Text, Accent, Control, ControlHover);
            Pill = Text0(_skin.label, 19, FontStyle.Normal, Text);
            Pill.alignment = TextAnchor.MiddleCenter;
            Pill.normal.background = Solid(new Color(0, 0, 0, 0.55f));
            Pill.padding = new RectOffset((int)S(18), (int)S(18), (int)S(8), (int)S(8));

            _skin.button = Secondary;
            _skin.label = Body;
            _skin.toggle.fontSize = (int)S(19);
            _skin.toggle.normal.textColor = _skin.toggle.onNormal.textColor = _skin.toggle.hover.textColor = _skin.toggle.onHover.textColor = Ink(Text);
            _skin.textField.fontSize = (int)S(20);
            _skin.textField.normal.background = _skin.textField.focused.background = _skin.textField.hover.background = Solid(Control);
            _skin.textField.normal.textColor = _skin.textField.focused.textColor = _skin.textField.hover.textColor = Ink(Text);
            _skin.textField.padding = new RectOffset((int)S(10), (int)S(10), (int)S(6), (int)S(6));
            _skin.horizontalSlider.normal.background = Solid(Control);
            _skin.horizontalSlider.fixedHeight = S(6);
            _skin.horizontalSlider.margin = new RectOffset(0, 0, (int)S(14), (int)S(14));
            _skin.horizontalSliderThumb.normal.background = _skin.horizontalSliderThumb.hover.background = _skin.horizontalSliderThumb.active.background = Solid(Accent);
            _skin.horizontalSliderThumb.fixedWidth = S(18);
            _skin.horizontalSliderThumb.fixedHeight = S(22);
            _skin.horizontalSliderThumb.margin = new RectOffset(0, 0, (int)-S(8), 0);
        }

        static GUIStyle Text0(GUIStyle from, float size, FontStyle style, Color colour)
        {
            var s = new GUIStyle(from) { fontSize = (int)S(size), fontStyle = style, wordWrap = false, richText = false };
            s.normal.textColor = s.hover.textColor = s.active.textColor = Ink(colour);
            s.normal.background = null;
            return s;
        }

        static GUIStyle Flat(float size, FontStyle style, Color text, Color hoverText, Color fill, Color hoverFill)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = (int)S(size), fontStyle = style, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset((int)S(18), (int)S(18), (int)S(8), (int)S(8)), margin = new RectOffset((int)S(4), (int)S(4), (int)S(4), (int)S(4)),
            };
            s.normal.background = Solid(fill);
            s.hover.background = Solid(hoverFill);
            s.active.background = Solid(Color.Lerp(hoverFill, Accent, 0.35f));
            s.focused.background = s.normal.background;
            s.onNormal.background = s.onHover.background = s.onActive.background = s.normal.background;
            s.normal.textColor = s.focused.textColor = s.onNormal.textColor = Ink(text);
            s.hover.textColor = s.active.textColor = s.onHover.textColor = s.onActive.textColor = Ink(hoverText);
            return s;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply(false, true);
            return t;
        }
    }
}
