using System;
using System.Collections.Generic;
using Godot;

/// F3 performance overlay: fps (last 1 s), average frame time and 1 % low (last 5 s), draw calls.
/// Frame times are wall-clock intervals between frames, so they include hitches that delta smoothing hides.
/// 1 % low is the average fps of the slowest 1 % of frames in the window. Refreshes 5 times per second.
public partial class PerfOverlay : CanvasLayer
{
    const ulong WindowUsec = 5_000_000;
    const ulong FpsWindowUsec = 1_000_000;
    const ulong RefreshUsec = 200_000;

    public double Fps { get; private set; }
    public double AvgFrameMs { get; private set; }
    public double OnePercentLowFps { get; private set; }
    public int DrawCalls { get; private set; }
    /// Number of refreshes so far, for the selftest.
    public int Refreshes { get; private set; }

    readonly Queue<(ulong At, double Ms)> _frames = new();
    ulong _lastFrame;
    ulong _lastRefresh;
    Label _label;

    public override void _Ready()
    {
        _label = GetNode<Label>("Panel/Label");
        Visible = false;
        _lastFrame = Time.GetTicksUsec();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("toggle_overlay"))
            Visible = !Visible;
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        _frames.Enqueue((now, (now - _lastFrame) / 1000.0));
        _lastFrame = now;
        while (now - _frames.Peek().At > WindowUsec)
            _frames.Dequeue();

        if (!Visible || now - _lastRefresh < RefreshUsec)
            return;
        _lastRefresh = now;
        Refreshes++;

        var ms = new double[_frames.Count];
        int i = 0;
        int lastSecond = 0;
        double sum = 0;
        foreach (var frame in _frames)
        {
            ms[i++] = frame.Ms;
            sum += frame.Ms;
            if (now - frame.At < FpsWindowUsec)
                lastSecond++;
        }
        Array.Sort(ms);
        int slowest = Math.Max(1, ms.Length / 100);
        double slowestSum = 0;
        for (int j = ms.Length - slowest; j < ms.Length; j++)
            slowestSum += ms[j];

        Fps = lastSecond;
        AvgFrameMs = sum / ms.Length;
        OnePercentLowFps = 1000.0 * slowest / slowestSum;
        DrawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        _label.Text = $"FPS      {Fps:0}\n"
            + $"Frame    {AvgFrameMs:0.00} ms  avg 5 s\n"
            + $"1% low   {OnePercentLowFps:0} fps  5 s\n"
            + $"Draws    {DrawCalls}";
    }
}
