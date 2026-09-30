using System;
using Godot;

/// The analog feed: one full-screen pass over the 3D render, on by default in the sandbox.
///
///   mixed     — video_feed.gdshader:     the feed, and what the pilot chose. The simulated composite chain held to
///                                        the authored version's restraint, sliding into the heavy degraded look and
///                                        back out at random, with occasional grain-only cuts
///   clean     — the pass is off, the raw render
///   chain     — video_chain.gdshader:    spike candidate 1, every artifact emerges from the signal
///   authored  — video_authored.gdshader: spike candidate 2, hand-written effects
///   hybrid    — video_hybrid.gdshader:   spike candidate 3, a real core with authored event layers
///
/// The three candidates are kept so the choice can be revisited against the same frame.
/// `-- --video clean|chain|authored|hybrid` picks another at start-up. V cycles them at runtime.
/// `-- --video-signals gain,link,current` pins the stand-in drivers of <see cref="FeedSignals"/>.
/// `-- --video-seed n` and `-- --video-field n` pin the feed's randomness, so a still is reproducible and two
/// stills one field apart show how much of the picture is redrawn every field.
/// `-- --video-field a-b` walks the range instead, saving a PNG of every field and quitting: how a loss sequence
/// (FeedLoss) is read at all, since it is over in half a second.
public partial class VideoFeed : CanvasLayer
{
    public enum Mode { Mixed, Clean, Chain, Authored, Hybrid }

    static readonly string[] ShaderPaths =
    {
        "res://assets/shaders/video_feed.gdshader",
        null,
        "res://assets/shaders/video_chain.gdshader",
        "res://assets/shaders/video_authored.gdshader",
        "res://assets/shaders/video_hybrid.gdshader",
    };

    readonly ShaderMaterial[] _materials = new ShaderMaterial[ShaderPaths.Length];
    readonly FeedSignals _signals = new();
    readonly FeedOsd _osd = new();
    FeedEvents _events;
    Texture2D _glyphs;
    ColorRect _rect;
    Camera3D _camera;
    Mode _mode;
    int _seed = 1;
    int _pinnedField = -1;
    int _lastField = -1;
    int _captureTo = -1;
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
        if (field != null && !ParseField(field, out feed._pinnedField, out feed._captureTo))
            GD.PrintErr($"ERROR: --video-field needs a field or a range like 610-660; got '{field}'.");
        return feed;
    }

    /// `--video-field n` pins one field. `--video-field a-b` walks a to b, saving a PNG of each: the only way to read
    /// a loss sequence field by field, since it is over in half a second at 50 Hz.
    static bool ParseField(string arg, out int from, out int to)
    {
        to = -1;
        int dash = arg.IndexOf('-', 1);
        if (dash < 0)
            return int.TryParse(arg, out from);
        return int.TryParse(arg[..dash], out from) && int.TryParse(arg[(dash + 1)..], out to) && to >= from;
    }

    public override void _Ready()
    {
        _events = new FeedEvents(_seed);
        _glyphs = FeedFont.BuildAtlas();
        _rect = new ColorRect { Name = "Feed", MouseFilter = Control.MouseFilterEnum.Ignore };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        for (int i = 0; i < ShaderPaths.Length; i++)
        {
            if (ShaderPaths[i] == null)
                continue;
            var shader = ResourceLoader.Load<Shader>(ShaderPaths[i]);
            if (shader == null)
                GD.PrintErr($"ERROR: VideoFeed cannot load {ShaderPaths[i]}.");
            else
                _materials[i] = new ShaderMaterial { Shader = shader };
        }
        Apply();
        if (_captureTo >= 0)
            Capture();
    }

    public override void _Process(double delta)
    {
        if (_mode == Mode.Clean)
            return;
        if (!_pinnedSignals && _camera != null)
            _signals.UpdateFromCamera(_camera.GlobalPosition, _camera.GlobalRotation, delta);

        _clock += delta;
        _events.AdvanceTo(_pinnedField >= 0 ? _pinnedField : (int)(_clock * 50.0), _signals.MotorCurrent);
        // The OSD is built once per video field, as §7 specifies, not once per rendered frame.
        if (_events.Field != _lastField)
        {
            _lastField = _events.Field;
            _osd.Build(_signals, _events.Field);
        }
        if (_pinnedField >= 0 && !_reported)
        {
            _reported = true;
            GD.Print($"VideoFeed: {Describe()}. {Schedule(_seed, _signals.MotorCurrent)}");
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
        m.SetShaderParameter("degrade", _events.Degrade);
        m.SetShaderParameter("osd_codes", _osd.Codes);
        m.SetShaderParameter("osd_glyphs", _glyphs);
        m.SetShaderParameter("osd_cells", (float)FeedFont.CellCount);

        FeedLoss loss = _events.Loss;
        m.SetShaderParameter("loss_state", (float)(int)loss.Current);
        m.SetShaderParameter("loss_split", loss.Split);
        m.SetShaderParameter("loss_top_strip", loss.TopStrip);
        m.SetShaderParameter("black_a", loss.BlackA);
        m.SetShaderParameter("black_b", loss.BlackB);
        m.SetShaderParameter("snow_level", loss.SnowLevel);
        m.SetShaderParameter("snow_chroma", loss.SnowChroma);
        m.SetShaderParameter("tell", loss.Tell);
        m.SetShaderParameter("tear_amp", loss.TearAmp);
        m.SetShaderParameter("tear_top", loss.TearTop);
        m.SetShaderParameter("grain_boost", loss.GrainBoost);
        m.SetShaderParameter("rx_text", loss.RxText ? 1f : 0f);
    }

    /// Walks the pinned field range, saving one PNG per field, then quits. Reading a loss sequence any other way is
    /// not possible: it is over in half a second.
    async void Capture()
    {
        SceneTree tree = GetTree();
        string dir = ProjectSettings.GlobalizePath("user://screenshots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GD.Print($"VideoFeed: capturing fields {_pinnedField}-{_captureTo}. {Schedule(_seed, _signals.MotorCurrent)}");
        for (int f = _pinnedField; f <= _captureTo; f++)
        {
            _pinnedField = f;
            _reported = true;
            for (int i = 0; i < 3; i++)
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            string path = $"{dir}/feed-{_seed}-{f:0000}.png";
            Error error = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print(error == Error.Ok ? $"VideoFeed: {path}: {Describe()}"
                : $"ERROR: VideoFeed cannot save {path}: {error}");
        }
        tree.Quit();
    }

    string Describe()
    {
        FeedLoss loss = _events.Loss;
        return $"field {_events.Field}: {loss.Current}"
            + (loss.Current == FeedLoss.State.PartialSnow ? $" split {loss.Split:0.00} top {loss.TopStrip:0.00}" : "")
            + (loss.Current == FeedLoss.State.Black ? $" rows {loss.BlackA:0.00}-{loss.BlackB:0.00}" : "")
            + (loss.SnowLevel > 0f ? $" snow {loss.SnowLevel * 255f:0} chroma {loss.SnowChroma:0.0}" : "")
            + (loss.Tell > 0f ? $", tell +{loss.Tell * 100f:0} %" : "")
            + (loss.TearAmp > 0f ? $", tear {loss.TearAmp:0.00} top {loss.TearTop:0}" : "")
            + (loss.GrainBoost > 0f ? $", grain +{loss.GrainBoost * 100f:0} %" : "")
            + (loss.RxText ? ", rx text" : "")
            + $", N3 {_events.N3Amp:0.000}, N4 {_events.N4Amp:0.000}, degrade {_events.Degrade:0.00}";
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
        _rect.Material = _materials[(int)_mode];
        _rect.Visible = _rect.Material != null;
        GD.Print($"VideoFeed: {_mode} (V cycles; seed {_seed}"
            + $"{(_pinnedField >= 0 ? $", field pinned to {_pinnedField}" : "")}; gain {_signals.Gain:0.00}, "
            + $"link margin {_signals.LinkMargin:0.00}, motor current {_signals.MotorCurrent:0.00}"
            + $"{(_pinnedSignals ? ", pinned" : "")})");
    }

    /// The measured rates, and the first field of each of the first few dropouts and deep quality drops, so a still or
    /// a `--video-field a-b` sweep can be aimed at one. Replaying from the seed is what makes this answerable at all.
    static string Schedule(int seed, float motorCurrent)
    {
        var scan = new FeedEvents(seed);
        string drops = "", dropouts = "";
        int dropStarts = 0, dropoutStarts = 0;
        bool wasDrop = false, wasLoss = false;
        for (int f = 1; f <= 15000; f++) // 5 minutes
        {
            scan.AdvanceTo(f, motorCurrent);
            bool drop = scan.Degrade > 0f, lost = scan.Loss.Busy;
            if (drop && !wasDrop) dropStarts++;
            if (lost && !wasLoss)
            {
                dropoutStarts++;
                if (dropouts.Split(' ').Length <= 6) dropouts += f + " ";
            }
            wasDrop = drop;
            wasLoss = lost;
            if (scan.Degrade > 0.75f && drops.Split(' ').Length <= 5) drops += f + " ";
        }
        return $"over 5 min: {dropStarts / 5.0:0.0} quality drops/min, {dropoutStarts / 5.0:0.0} dropouts/min "
            + $"(the pilot asked for 3-6); dropouts start at fields [{dropouts.Trim()}], "
            + $"deep drops at [{drops.Trim()}]";
    }

    /// Parses `--video <name>`. Returns false when the name is not a mode.
    public static bool TryParseMode(string name, out Mode mode) => Enum.TryParse(name, true, out mode);
}
