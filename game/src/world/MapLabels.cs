using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// The review labels of a map package's labels.json (uat1-gallery spec, Labels; game/maps/README.md): the name and state
/// of every terrain patch and asset station whose area the camera is within Range of, nearest first, in a panel at the
/// bottom of the screen. A station whose assets are not all built yet says so. L (toggle_labels) hides and shows them.
///
/// The layer is above the analog feed's (VideoFeed, layer 1). The feed samples the screen as it stands when its own
/// layer draws, so nothing on a higher layer goes through it: the labels stay sharp in every feed mode, and V still
/// switches the feed under them.
public partial class MapLabels : CanvasLayer
{
    public const string FileName = "labels.json";
    /// How near the camera must be to a label's area for the label to show, m.
    public const float Range = 25f;
    /// Above VideoFeed's layer 1.
    public const int LabelLayer = 2;
    public const string NotBuilt = "not built yet";

    /// One label: its text, whether it is a patch or a station, a station's assets and whether they are built, and its
    /// area: a rectangle on the ground (centre, half size, turned by yaw as objects are) from its bottom to its top.
    public sealed record Entry(string Text, string Kind, bool Built, string[] Assets, Vector2 Centre, Vector2 Half,
        float Yaw, float Bottom, float Top);

    public IReadOnlyList<Entry> Entries => _entries;
    /// The texts on screen in the last frame, nearest first; empty while the labels are hidden.
    public IReadOnlyList<string> Shown => _shown;

    readonly List<Entry> _entries = new();
    readonly List<string> _shown = new();
    PanelContainer _panel;
    Label _label;

    /// Reads a labels.json. Throws with a one-line message on a malformed file (the validator checks every rule).
    public static MapLabels Load(string path)
    {
        var labels = new MapLabels { Name = "Labels", Layer = LabelLayer };
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement l in doc.RootElement.GetProperty("labels").EnumerateArray())
        {
            string kind = l.GetProperty("kind").GetString();
            bool station = kind == "station";
            bool built = !station || l.GetProperty("built").GetBoolean();
            string text = $"{l.GetProperty("name").GetString()}, {l.GetProperty("state").GetString()}";
            JsonElement centre = l.GetProperty("centre_m"), half = l.GetProperty("half_m"), y = l.GetProperty("y_m");
            labels._entries.Add(new Entry(built ? text : $"{text} — {NotBuilt}", kind, built,
                station ? l.GetProperty("assets").EnumerateArray().Select(a => a.GetString()).ToArray() : Array.Empty<string>(),
                new Vector2(centre[0].GetSingle(), centre[1].GetSingle()), new Vector2(half[0].GetSingle(), half[1].GetSingle()),
                l.GetProperty("yaw_deg").GetSingle(), y[0].GetSingle(), y[1].GetSingle()));
        }
        return labels;
    }

    /// The distance from `p` to a label's area, 0 inside it.
    public static float Distance(Entry e, Vector3 p)
    {
        // The area's own axes: +X is (cos yaw, −sin yaw) and +Z (sin yaw, cos yaw) in world x, z (object yaw).
        float yaw = Mathf.DegToRad(e.Yaw), c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
        float dx = p.X - e.Centre.X, dz = p.Z - e.Centre.Y;
        float ex = Mathf.Max(Mathf.Abs(dx * c - dz * s) - e.Half.X, 0f);
        float ez = Mathf.Max(Mathf.Abs(dx * s + dz * c) - e.Half.Y, 0f);
        float ey = Mathf.Max(Mathf.Max(e.Bottom - p.Y, p.Y - e.Top), 0f);
        return Mathf.Sqrt(ex * ex + ey * ey + ez * ez);
    }

    public override void _Ready()
    {
        _panel = new PanelContainer { Name = "Panel", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.06f, 0.72f),
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        _label = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _label.AddThemeFontOverride("font", new SystemFont { FontNames = new[] { "Segoe UI", "Arial", "DejaVu Sans" } });
        _label.AddThemeFontSizeOverride("font_size", 20);
        _label.AddThemeColorOverride("font_color", new Color(0.93f, 0.95f, 0.88f));
        _panel.AddChild(_label);
        AddChild(_panel);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("toggle_labels"))
            Visible = !Visible;
    }

    public override void _Process(double delta)
    {
        _shown.Clear();
        Camera3D camera = GetViewport()?.GetCamera3D();
        if (Visible && camera != null)
        {
            Vector3 p = camera.GlobalPosition;
            foreach (Entry e in _entries.Where(e => Distance(e, p) <= Range).OrderBy(e => Distance(e, p)))
                _shown.Add(e.Text);
        }
        string text = string.Join("\n", _shown);
        if (text != _label.Text)
        {
            // Refit the panel to the new text, centred at the bottom.
            _label.Text = text;
            _panel.ResetSize();
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.Minsize, 28);
        }
        _panel.Visible = _shown.Count > 0;
    }
}
