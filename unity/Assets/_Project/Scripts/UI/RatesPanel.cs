using System;
using System.Globalization;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using UnityEngine;

namespace Fpv450.UI
{
    /// <summary>
    /// The Calibrate screen's rates and throttle curve (p6-input-app 1.3, INP-03), plain IMGUI as the wizard is: a
    /// column a group of FcSettingRow.Rows (roll, pitch, yaw, throttle), each under its curve from RatePreview, the
    /// flight controller's own functions on the settings being edited. The rate curves share one scale so the axes
    /// compare at a glance; the throttle's shows the straight line it would be with no curve. A slider and two steps
    /// edit each CLI value on its row's step. A change is saved to fc-settings.json when the mouse button is let go,
    /// and when the screen closes; a file that could not be read is left alone until then, and its reason shown.
    /// </summary>
    sealed class RatesPanel
    {
        const float PlotHeight = 120, Gap = 16;

        static readonly Color PlotColour = new Color(0.13f, 0.13f, 0.15f), GridColour = new Color(0.28f, 0.28f, 0.31f);
        static readonly Color CurveColour = new Color(0.95f, 0.95f, 0.95f);
        static readonly double[] Straight = new double[RatePreview.Points];

        readonly string _directory;
        readonly RatePreview _preview = new RatePreview();
        FcSettings _settings;
        bool _dirty;
        string _message;
        GUIStyle _head, _value, _small;
        Texture2D _white;

        static RatesPanel()
        {
            for (int i = 0; i < Straight.Length; i++) Straight[i] = RatePreview.Stick(i);
        }

        public RatesPanel(string directory)
        {
            _directory = directory;
            _settings = FcSettingsStore.Load(directory, out _message);
            _preview.From(_settings);
        }

        /// <summary>Writes the settings if they changed since the last save.</summary>
        public void Save()
        {
            if (!_dirty) return;
            try
            {
                FcSettingsStore.Save(_directory, _settings);
                _dirty = false;
                _message = null;
            }
            catch (Exception e)
            {
                _message = FcSettingsStore.FileName + " could not be saved: " + e.Message;
            }
        }

        void Styles()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            _head = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _value = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _head.normal.textColor = _value.normal.textColor = Color.white;
            _small.normal.textColor = AppStyle.Ink(new Color(0.75f, 0.75f, 0.75f));
        }

        /// <param name="width">The screen's content width.</param>
        public void Draw(float width)
        {
            Styles();
            FcSettingRow[] rows = FcSettingRow.Rows;
            int columns = 1;
            for (int i = 1; i < rows.Length; i++)
                if (rows[i].Group != rows[i - 1].Group) columns++;
            float column = (width - Gap * (columns - 1)) / columns;

            // One scale for the rate curves: the highest full-stick rate, rounded up to 100 °/s.
            double top = 100;
            for (int a = 0; a < 3; a++) top = Math.Max(top, Math.Ceiling(_preview.Rate[a][RatePreview.Points - 1] / 100) * 100);

            GUILayout.BeginHorizontal();
            for (int from = 0; from < rows.Length;)
            {
                int to = from + 1;
                while (to < rows.Length && rows[to].Group == rows[from].Group) to++;
                if (from > 0) GUILayout.Space(Gap);
                Column(rows, from, to, column, top);
                from = to;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Defaults", GUILayout.Width(160), GUILayout.Height(36)))
            {
                foreach (FcSettingRow row in rows) row.Set(ref _settings, row.Get(FcSettingsStore.PlayerDefault));
                Changed();
            }
            GUILayout.Space(12);
            GUILayout.Label("Betaflight 4.5, ACTUAL rates. Changes are saved as you make them.", _small, GUILayout.Height(36));
            GUILayout.EndHorizontal();
            if (_message != null) GUILayout.Label(_message, _small);

            if (_dirty && Event.current.rawType == EventType.MouseUp) Save();
        }

        void Column(FcSettingRow[] rows, int from, int to, float width, double top)
        {
            FcSettingRow head = rows[from];
            bool throttle = head.Curve == FcSettingRow.ThrottleCurve;
            double[] curve = throttle ? _preview.Throttle : _preview.Rate[head.Curve];
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(head.Group, _head);
            Rect plot = GUILayoutUtility.GetRect(width, PlotHeight, GUILayout.Width(width), GUILayout.Height(PlotHeight));
            Plot(plot, curve, throttle ? 1 : top, throttle);
            string caption = throttle
                ? "Half stick " + curve[RatePreview.Points / 2].ToString("0.00", CultureInfo.InvariantCulture)
                : "Full stick " + curve[RatePreview.Points - 1].ToString("0", CultureInfo.InvariantCulture) + " °/s";
            GUILayout.Label(caption, _small);
            for (int i = from; i < to; i++) Row(rows[i]);
            GUILayout.EndVertical();
        }

        void Plot(Rect r, double[] curve, double top, bool straight)
        {
            if (Event.current.type != EventType.Repaint) return;
            Fill(r, PlotColour);
            Fill(new Rect(r.x + r.width / 2, r.y, 1, r.height), GridColour);
            if (straight) Line(r, Straight, 1, GridColour);
            Line(r, curve, top, CurveColour);
            GUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 20), straight ? "1.00" : top.ToString("0", CultureInfo.InvariantCulture) + " °/s", _small);
        }

        // The curve over the stick's 0…1, a block from each point to the next, so a steep stretch stays joined.
        void Line(Rect r, double[] y, double top, Color colour)
        {
            GUI.color = colour;
            int n = y.Length - 1;
            for (int i = 1; i <= n; i++)
            {
                float x0 = r.x + r.width * (i - 1) / n, x1 = r.x + r.width * i / n;
                float y0 = Y(r, y[i - 1], top), y1 = Y(r, y[i], top);
                GUI.DrawTexture(new Rect(x0, Mathf.Min(y0, y1) - 1, x1 - x0 + 1, Mathf.Abs(y1 - y0) + 2), _white);
            }
            GUI.color = Color.white;
        }

        static float Y(Rect r, double v, double top) => r.yMax - r.height * Mathf.Clamp01((float)(v / top));

        void Fill(Rect r, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(r, _white);
            GUI.color = Color.white;
        }

        void Row(FcSettingRow row)
        {
            double cli = row.Get(_settings);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(row.Label, _small);
            GUILayout.FlexibleSpace();
            GUILayout.Label(row.Shown(_settings).ToString(row.Format, CultureInfo.InvariantCulture) + row.Unit, _value);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(26), GUILayout.Height(22))) Change(row, cli - row.Step);
            // GUI.changed is set by the pilot's drag only, so a value outside the slider's range is never clamped by
            // just being drawn.
            bool was = GUI.changed;
            GUI.changed = false;
            float v = GUILayout.HorizontalSlider((float)cli, (float)row.SliderLow, (float)row.SliderHigh, GUILayout.Height(22));
            if (GUI.changed) Change(row, v);
            GUI.changed |= was;
            if (GUILayout.Button("+", GUILayout.Width(26), GUILayout.Height(22))) Change(row, cli + row.Step);
            GUILayout.EndHorizontal();
        }

        void Change(FcSettingRow row, double cli)
        {
            double v = row.Snap(cli);
            if (v == row.Get(_settings)) return;
            row.Set(ref _settings, v);
            Changed();
        }

        void Changed()
        {
            _preview.From(_settings);
            _dirty = true;
        }
    }
}
