using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using UnityEngine;

namespace Fpv450.App
{
    public sealed partial class AppFlow
    {
        /// <summary>
        /// <c>-plantbench &lt;folder&gt;</c> in a review build (w16-plants-in-the-air 1.3): a flight on Village from the village's start,
        /// flown by PlantPilot on the development keyboard, for the plants' look and their frame rate through the feed. It hovers
        /// 2 m up at the start, flies to the open fields south of the village and hovers over their grass at 4, 2, 1 and 0.5 m,
        /// makes a low pass 1 m over them, then hovers 4 m up in field F8 looking west over it at its tree belt. Each held leg's
        /// frames are timed (the frame, the CPU's main and render threads, the GPU where frame timing gives it); the hovers, the pass
        /// and the belt are shot through the feed (FPV, and the hovers again in the chase view) as sequences of frames. The wind
        /// is <c>-plantwind &lt;Calm|Windy|Severe&gt;</c> (Calm by default); <c>-plants 0</c> flies it with the friend's own plants
        /// (the A/B's before). Writes <c>plantbench.json</c> and the PNGs to the folder, and quits.
        /// </summary>
        IEnumerator PlantBench(string folder)
        {
            Directory.CreateDirectory(folder);
            AppRadio.Unplug();
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-plantwind");
            WindLevel wind = WindLevel.Calm;
            if (at >= 0 && at + 1 < args.Length) Enum.TryParse(args[at + 1], true, out wind);
            yield return null;
            Choose(PlayMaps.Village);
            FlightChoices c = _setup.Choices;
            c.StartPoint = FlightWorlds.FarmlandStart.Name;
            c.Legs = true;
            c.Payload = 1.0;
            c.Wind = wind;
            c.Rain = Rain.Sunny;
            c.TimeOfDay = 12.0;
            Fly();
            float until = Time.realtimeSinceStartup + 120;
            while (!(_view == View.Flight && _flight != null && _flight.Host.Inputs != null) && Time.realtimeSinceStartup < until) yield return null;
            if (_flight != null && _flight.Host.Inputs != null) _flight.Host.Inputs.KeyboardAllowed = true; // the bench's own keys, the radio unplugged (the smoke test's)
            while (!(_view == View.Flight && _flight != null && _flight.Host.Session != null && _flight.Host.Session.State == SessionState.Flying &&
                     _flight.ShownFlight == _flight.Host.Session.Flights) && Time.realtimeSinceStartup < until)
                yield return null;
            FlightSessionHost host = _flight != null ? _flight.Host : null;
            FlightSession session = host?.Session;
            if (session == null || !host.Inputs.KeyboardAllowed)
            {
                Debug.LogError("[PlantBench] the flight never flew on the keyboard (a review build is needed)");
                Application.Quit(1);
                yield break;
            }
            Debug.Log($"[PlantBench] Village, {wind}, plants {(FarmlandPlants.On ? "in the flight's air" : "the friend's (-plants 0)")}; {SystemInfo.graphicsDeviceName}, " +
                      $"{Screen.width}x{Screen.height}, vsync {QualitySettings.vSyncCount}, frame timing stats {FrameTimingManager.IsFeatureEnabled()}");
            yield return new WaitForSecondsRealtime(2);

            KeyboardKeys keys = KeyboardKeys.ArmToggle;
            host.Keys = () => keys;
            yield return new WaitForSecondsRealtime(0.15f);
            keys = KeyboardKeys.None;
            yield return new WaitForSecondsRealtime(0.5f);

            IWorldQuery world = session.Setup.World.Query;
            var point = new XZ[1];
            var under = new GroundSample[1];
            double Ground(double x, double z)
            {
                point[0] = new XZ(x, z);
                world.SampleGround(point, under);
                return under[0].Height;
            }
            Session.StartPoint start = FlightWorlds.FarmlandStart;
            const double FieldX = 503, FieldZ = 524, BeltX = 543, BeltZ = 820;
            var legs = new[]
            {
                new PlantPilot.Leg("start 2 m", start.X, start.Z, 2, 2, 300, 10),
                new PlantPilot.Leg("to the field", FieldX, FieldZ, 8, 6, 200, 0),
                new PlantPilot.Leg("hover 4 m", FieldX, FieldZ, 4, 2, 200, 8),
                new PlantPilot.Leg("hover 2 m", FieldX, FieldZ, 2, 1, 200, 8),
                new PlantPilot.Leg("hover 1 m", FieldX, FieldZ, 1, 1, 200, 8),
                new PlantPilot.Leg("hover 0.5 m", FieldX, FieldZ, 0.5, 0.5, 200, 8),
                new PlantPilot.Leg("up", FieldX, FieldZ, 1.5, 1, 200, 0),
                new PlantPilot.Leg("low pass 1 m", FieldX - 60 * Math.Sin(200 * Math.PI / 180), FieldZ - 60 * Math.Cos(200 * Math.PI / 180), 1, 6, 20, 0),
                new PlantPilot.Leg("to the belt", BeltX, BeltZ, 8, 8, 270, 0),
                new PlantPilot.Leg("belt 4 m", BeltX, BeltZ, 4, 2, 270, 10),
            };
            var pilot = new PlantPilot(host.Inputs.Settings, Ground, legs);
            var runs = new Dictionary<string, Timing>();
            var timings = new FrameTiming[1];
            float last = Time.realtimeSinceStartup, end = last + 420;
            int shots = 0;
            string shotLeg = null;
            float nextShot = 0;
            while (!pilot.Done && session.State == SessionState.Flying && !Crashed(session) && Time.realtimeSinceStartup < end)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                keys = pilot.Next(_flight.Pose, now - last);
                last = now;
                PlantPilot.Leg leg = pilot.Now;
                bool timed = pilot.Holding || leg.Name.StartsWith("low pass", StringComparison.Ordinal);
                if (timed && pilot.HeldFor > 1 || leg.Name.StartsWith("low pass", StringComparison.Ordinal))
                {
                    if (!runs.TryGetValue(leg.Name, out Timing t)) runs[leg.Name] = t = new Timing();
                    FrameTimingManager.CaptureFrameTimings();
                    double gpu = FrameTimingManager.GetLatestTimings(1, timings) > 0 ? timings[0].gpuFrameTime : 0;
                    double main = timings[0].cpuMainThreadFrameTime, render = timings[0].cpuRenderThreadFrameTime;
                    t.Add(Time.unscaledDeltaTime * 1000, main, render, gpu, _flight.Pose);
                }
                // The shots: the hovers' and the belt's last seconds, the pass's whole, a frame every 0.2 s (0.25 at the belt).
                bool hover = leg.Name.StartsWith("hover", StringComparison.Ordinal) || leg.Name.StartsWith("belt", StringComparison.Ordinal);
                bool shoot = hover ? pilot.Holding && pilot.HeldFor >= leg.Hold - 3 : leg.Name.StartsWith("low pass", StringComparison.Ordinal);
                if (shoot && now >= nextShot)
                {
                    if (shotLeg != leg.Name) { shotLeg = leg.Name; shots = 0; }
                    yield return new WaitForEndOfFrame();
                    string name = leg.Name.Replace(" ", "_").Replace(".", "") + "_" + (++shots).ToString("00");
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, (FarmlandPlants.On ? "" : "before_") + name + ".png"));
                    nextShot = now + (leg.Name.StartsWith("belt", StringComparison.Ordinal) ? 0.25f : 0.2f);
                }
                if (hover && pilot.Holding && pilot.HeldFor >= leg.Hold - 0.2 && !_flight.Chase && leg.Name.StartsWith("hover", StringComparison.Ordinal))
                {
                    // The hover once more from behind and above (V), held while the pilot holds the place.
                    _flight.Chase = true;
                    for (int i = 0; i < 6; i++)
                    {
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, (FarmlandPlants.On ? "" : "before_") + leg.Name.Replace(" ", "_").Replace(".", "") + "_chase_" + (i + 1).ToString("00") + ".png"));
                        float wait = Time.realtimeSinceStartup + 0.2f;
                        while (Time.realtimeSinceStartup < wait)
                        {
                            yield return null;
                            float n2 = Time.realtimeSinceStartup;
                            keys = pilot.Next(_flight.Pose, n2 - last);
                            last = n2;
                        }
                    }
                    _flight.Chase = false;
                }
            }
            keys = KeyboardKeys.None;
            PoseFrame p = _flight.Pose;
            string result = pilot.Done ? "flown" : Crashed(session) ? "CRASHED" : "stopped at '" + pilot.Now.Name + "'";
            Debug.Log($"[PlantBench] {result} at t = {p.TimeS:0.0} s, ({p.Position.X:0.0}, {p.Position.Y:0.0}, {p.Position.Z:0.0})");
            PlantField field = _flight.GetComponent<FarmlandPlants>()?.Field;
            var json = new StringBuilder();
            json.Append("{\n  \"wind\": \"").Append(wind).Append("\",\n  \"plants\": ").Append(FarmlandPlants.On ? "true" : "false")
                .Append(",\n  \"result\": \"").Append(result).Append("\",\n  \"gpu\": \"").Append(SystemInfo.graphicsDeviceName)
                .Append("\",\n  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height)
                .Append("\",\n  \"meanMsPerSecond\": ").Append(F(field?.MeanMsPerSecond ?? 0)).Append(",\n  \"washTickMs\": ").Append(F(field?.WashTickMs ?? 0))
                .Append(",\n  \"legs\": [");
            bool first = true;
            foreach (KeyValuePair<string, Timing> kv in runs)
            {
                json.Append(first ? "\n" : ",\n").Append("    ").Append(kv.Value.Json(kv.Key));
                Debug.Log("[PlantBench] " + kv.Value.Line(kv.Key));
                first = false;
            }
            json.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(folder, (FarmlandPlants.On ? "" : "before_") + "plantbench.json"), json.ToString());
            host.Keys = () => KeyboardKeys.None;
            Application.Quit(pilot.Done ? 0 : 1);
        }

        static string F(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        // A held leg's frames.
        sealed class Timing
        {
            readonly List<double> _frames = new List<double>();
            double _main, _render, _gpu;
            int _gpuFrames;

            public void Add(double ms, double main, double render, double gpu, in PoseFrame pose)
            {
                _frames.Add(ms);
                _main += main;
                _render += render;
                if (gpu > 0) { _gpu += gpu; _gpuFrames++; }
            }

            double P(double q)
            {
                var s = new List<double>(_frames);
                s.Sort();
                return s.Count == 0 ? 0 : s[Math.Min(s.Count - 1, (int)(q * s.Count))];
            }

            double Mean()
            {
                double sum = 0;
                foreach (double f in _frames) sum += f;
                return _frames.Count > 0 ? sum / _frames.Count : 0;
            }

            public string Line(string name) =>
                $"{name}: {_frames.Count} frames, {Mean():0.00} ms mean ({1000 / Math.Max(1e-6, Mean()):0} fps), p99 {P(0.99):0.00} ms, " +
                $"CPU main {_main / Math.Max(1, _frames.Count):0.00} ms, render {_render / Math.Max(1, _frames.Count):0.00} ms, " +
                (_gpuFrames > 0 ? $"GPU {_gpu / _gpuFrames:0.00} ms" : "GPU n/a");

            public string Json(string name) =>
                "{ \"leg\": \"" + name + "\", \"frames\": " + _frames.Count + ", \"meanMs\": " + F(Mean()) + ", \"p99Ms\": " + F(P(0.99)) +
                ", \"cpuMainMs\": " + F(_main / Math.Max(1, _frames.Count)) + ", \"cpuRenderMs\": " + F(_render / Math.Max(1, _frames.Count)) +
                ", \"gpuMs\": " + F(_gpuFrames > 0 ? _gpu / _gpuFrames : 0) + " }";
        }
    }
}
