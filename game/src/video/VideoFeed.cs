using System;
using Godot;

/// Direction spike (no OpenSpec change yet): three candidate treatments of the analog feed, each one full-screen
/// pass over the 3D render, so they can be compared on the same frame.
///
///   clean     — the pass is off, the raw render
///   chain     — video_chain.gdshader:    encode to PAL composite and decode back; artifacts emerge from the signal
///   authored  — video_authored.gdshader: hand-written effects tuned by eye against the reference notes
///   hybrid    — video_hybrid.gdshader:   a cheaper real encode/decode core, authored event layers on top
///
/// `-- --video chain|authored|hybrid` picks one at start-up. V cycles clean → chain → authored → hybrid at runtime.
/// `-- --video-signals gain,link,current` pins the stand-in drivers of <see cref="FeedSignals"/>.
/// `-- --video-seed n` and `-- --video-field n` pin the feed's randomness, so a still is reproducible and two
/// stills one field apart show how much of the picture is redrawn every field.
public partial class VideoFeed : CanvasLayer
{
    public enum Mode { Clean, Chain, Authored, Hybrid }

    static readonly string[] ShaderPaths =
    {
        null,
        "res://assets/shaders/video_chain.gdshader",
        "res://assets/shaders/video_authored.gdshader",
        "res://assets/shaders/video_hybrid.gdshader",
    };

    readonly ShaderMaterial[] _materials = new ShaderMaterial[ShaderPaths.Length];
    readonly FeedSignals _signals = new();
    FeedEvents _events;
    ColorRect _rect;
    Camera3D _camera;
    Mode _mode;
    int _seed = 1;
    int _pinnedField = -1;
    bool _pinnedSignals;
    bool _reported;
    double _clock;

    /// Builds the layer. `camera` drives the stand-in signals; the rest are the optional pins.
    public static VideoFeed Create(Mode mode, Camera3D camera, string signals, string seed, string field)
    {
        var feed = new VideoFeed { Name = "VideoFeed", Layer = 1, _mode = mode, _camera = camera };
        if (signals != null)
        {
            string[] v = signals.Split(',');
            if (v.Length != 3 || !float.TryParse(v[0], out feed._signals.Gain)
                || !float.TryParse(v[1], out feed._signals.LinkMargin)
                || !float.TryParse(v[2], out feed._signals.MotorCurrent))
                GD.PrintErr($"ERROR: --video-signals needs gain,link,current; got '{signals}'.");
            else
                feed._pinnedSignals = true;
        }
        if (seed != null && !int.TryParse(seed, out feed._seed))
            GD.PrintErr($"ERROR: --video-seed needs an integer; got '{seed}'.");
        if (field != null && !int.TryParse(field, out feed._pinnedField))
            GD.PrintErr($"ERROR: --video-field needs an integer; got '{field}'.");
        return feed;
    }

    public override void _Ready()
    {
        _events = new FeedEvents(_seed);
        _rect = new ColorRect { Name = "Feed", MouseFilter = Control.MouseFilterEnum.Ignore };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        for (int i = 1; i < ShaderPaths.Length; i++)
        {
            var shader = ResourceLoader.Load<Shader>(ShaderPaths[i]);
            if (shader == null)
                GD.PrintErr($"ERROR: VideoFeed cannot load {ShaderPaths[i]}.");
            else
                _materials[i] = new ShaderMaterial { Shader = shader };
        }
        Apply();
    }

    public override void _Process(double delta)
    {
        if (_mode == Mode.Clean)
            return;
        if (!_pinnedSignals && _camera != null)
            _signals.UpdateFromCamera(_camera.GlobalPosition, delta);

        _clock += delta;
        _events.AdvanceTo(_pinnedField >= 0 ? _pinnedField : (int)(_clock * 50.0), _signals.MotorCurrent);
        if (_pinnedField >= 0 && !_reported)
        {
            _reported = true;
            GD.Print($"VideoFeed: field {_events.Field}: N3 {_events.N3Amp:0.000}, N4 {_events.N4Amp:0.000}. "
                + $"Fields with both firing: {BothFiring(_seed, _signals.MotorCurrent)}");
        }

        ShaderMaterial m = _materials[(int)_mode];
        if (m == null)
            return;
        m.SetShaderParameter("gain", _signals.Gain);
        m.SetShaderParameter("link_margin", _signals.LinkMargin);
        m.SetShaderParameter("motor_current", _signals.MotorCurrent);
        m.SetShaderParameter("seed", (float)_seed);
        m.SetShaderParameter("field", (float)_events.Field);
        m.SetShaderParameter("n3_amp", _events.N3Amp);
        m.SetShaderParameter("n3_roll", _events.N3Roll);
        m.SetShaderParameter("n3_spacing", _events.N3Spacing);
        m.SetShaderParameter("n4_amp", _events.N4Amp);
        m.SetShaderParameter("n4_span", _events.N4Span);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.V })
        {
            _mode = (Mode)(((int)_mode + 1) % ShaderPaths.Length);
            Apply();
        }
    }

    void Apply()
    {
        _rect.Material = _mode == Mode.Clean ? null : _materials[(int)_mode];
        _rect.Visible = _mode != Mode.Clean;
        GD.Print($"VideoFeed: {_mode} (V cycles; seed {_seed}"
            + $"{(_pinnedField >= 0 ? $", field pinned to {_pinnedField}" : "")}; gain {_signals.Gain:0.00}, "
            + $"link margin {_signals.LinkMargin:0.00}, motor current {_signals.MotorCurrent:0.00}"
            + $"{(_pinnedSignals ? ", pinned" : "")})");
    }

    /// The first few field indices where N3 and N4 are both firing, so a still can show both. Replaying from the
    /// seed is what makes this answerable at all.
    static string BothFiring(int seed, float motorCurrent)
    {
        var scan = new FeedEvents(seed);
        string found = "";
        for (int f = 1; f <= 2000; f++)
        {
            scan.AdvanceTo(f, motorCurrent);
            if (scan.N3Amp > 0f && scan.N4Amp > 0f && found.Split(' ').Length <= 6)
                found += f + " ";
        }
        return found == "" ? "none in the first 2000" : found;
    }

    /// Parses `--video <name>`. Returns false when the name is not a mode.
    public static bool TryParseMode(string name, out Mode mode) => Enum.TryParse(name, true, out mode);
}
