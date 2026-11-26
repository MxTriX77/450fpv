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
        /// (the A/B's before); <c>-plantshots 0</c> takes no shots, so no capture lands in the frame times. Writes <c>plantbench.json</c>
        /// and the PNGs to the folder, and quits.
        /// </summary>
        IEnumerator PlantBench(string folder)
        {
            Directory.CreateDirectory(folder);
            _smokeLogs = Path.Combine(folder, "flights"); // its flight's log beside its shots, never in the pilot's folder
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
            FarmlandPlants plants = _flight.GetComponent<FarmlandPlants>();
            bool shooting = !(Array.IndexOf(args, "-plantshots") is int sa && sa >= 0 && sa + 1 < args.Length && args[sa + 1] == "0");
            int lastGcs = GC.CollectionCount(0);
            long lastDrawn = -1;
            double lastShown = double.NaN;
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
                // This frame's collections and the PAL fields it skipped (its drawn field more than one field past the last one drawn).
                int gcs = GC.CollectionCount(0), collected = gcs - lastGcs;
                long drawn = _flight.DrawnStep;
                int skipped = lastDrawn >= 0 && drawn > lastDrawn ? (int)((drawn - lastDrawn) / FlightCore.FieldSteps) - 1 : 0;
                // The fine wash level's drawn state: its age behind the frame's field (ms), and whether it stood still while the field moved
                // (the field and the plants as the last frame's LateUpdate drew them: a coroutine resumes before this frame's).
                PlantField drawnField = plants != null && plants.DrawFrame == Time.frameCount - 1 ? plants.Field : null;
                double shown = drawnField != null ? drawnField.WashShown(0) : double.NaN;
                double washAge = double.IsNaN(shown) ? double.NaN : (drawn * FlightCore.Dt - shown) * 1000;
                bool washHeld = !double.IsNaN(shown) && shown == lastShown && drawn > lastDrawn;
                if (drawnField != null) lastShown = shown;
                lastGcs = gcs;
                lastDrawn = drawn;
                bool timed = pilot.Holding || leg.Name.StartsWith("low pass", StringComparison.Ordinal);
                if (timed && pilot.HeldFor > 1 || leg.Name.StartsWith("low pass", StringComparison.Ordinal))
                {
                    if (!runs.TryGetValue(leg.Name, out Timing t)) runs[leg.Name] = t = new Timing();
                    FrameTimingManager.CaptureFrameTimings();
                    double gpu = FrameTimingManager.GetLatestTimings(1, timings) > 0 ? timings[0].gpuFrameTime : 0;
                    t.Add(Time.unscaledDeltaTime * 1000, timings[0].cpuMainThreadFrameTime, timings[0].cpuMainThreadPresentWaitTime,
                        timings[0].cpuRenderThreadFrameTime, gpu, drawnField != null ? plants.DrawMs : 0, collected, skipped, drawnField, washAge, washHeld);
                }
                // The shots: the hovers' and the belt's last seconds, the pass's whole, a frame every 0.2 s (0.25 at the belt).
                bool hover = leg.Name.StartsWith("hover", StringComparison.Ordinal) || leg.Name.StartsWith("belt", StringComparison.Ordinal);
                bool shoot = hover ? pilot.Holding && pilot.HeldFor >= leg.Hold - 3 : leg.Name.StartsWith("low pass", StringComparison.Ordinal);
                if (shooting && shoot && now >= nextShot)
                {
                    if (shotLeg != leg.Name) { shotLeg = leg.Name; shots = 0; }
                    yield return new WaitForEndOfFrame();
                    string name = leg.Name.Replace(" ", "_").Replace(".", "") + "_" + (++shots).ToString("00");
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, (FarmlandPlants.On ? "" : "before_") + name + ".png"));
                    nextShot = now + (leg.Name.StartsWith("belt", StringComparison.Ordinal) ? 0.25f : 0.2f);
                }
                if (shooting && hover && pilot.Holding && pilot.HeldFor >= leg.Hold - 0.2 && !_flight.Chase && leg.Name.StartsWith("hover", StringComparison.Ordinal))
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

        // A held leg's frames: per frame the frame's time, the main thread's (and its wait for the present), the render thread's,
        // the GPU's (frame timing's), the plants' Draw on the main thread, the collections and PAL fields skipped, the wash's last
        // tick on its helper and the fine wash level's drawn age (frames that drew the plants); each column's mean, p99 and worst;
        // the frames whose wash stood still while the field moved; each part of the wash's tick, its mean over the frames.
        sealed class Timing
        {
            readonly List<double>[] _c = { new List<double>(4096), new List<double>(4096), new List<double>(4096), new List<double>(4096),
                new List<double>(4096), new List<double>(4096), new List<double>(4096), new List<double>(4096) }; // sized ahead: nothing allocated a frame
            static readonly string[] Names = { "frame", "main", "presentWait", "render", "gpu", "plantsDraw", "washTick", "washAge" };
            static readonly string[] Parts = { "fine tick", "fine export", "steady", "coarse tick", "coarse export" };
            readonly double[] _parts = new double[5];
            int _gcs, _gcFrames, _skipped, _skipFrames, _held, _partFrames;

            public void Add(double ms, double main, double presentWait, double render, double gpu, double plantsDraw, int gcs, int skipped,
                PlantField field, double washAge, bool washHeld)
            {
                if (field != null)
                {
                    _c[6].Add(field.WashTickMs);
                    for (int i = 0; i < _parts.Length; i++) _parts[i] += field.WashPartMs(i);
                    _partFrames++;
                }
                if (!double.IsNaN(washAge)) _c[7].Add(washAge);
                if (washHeld) _held++;
                _c[0].Add(ms);
                _c[1].Add(main);
                _c[2].Add(presentWait);
                _c[3].Add(render);
                if (gpu > 0) _c[4].Add(gpu);
                _c[5].Add(plantsDraw);
                _gcs += gcs;
                if (gcs > 0) _gcFrames++;
                _skipped += Math.Max(0, skipped);
                if (skipped > 0) _skipFrames++;
            }

            static double P(List<double> c, double q)
            {
                var s = new List<double>(c);
                s.Sort();
                return s.Count == 0 ? 0 : s[Math.Min(s.Count - 1, (int)(q * s.Count))];
            }

            static double Mean(List<double> c)
            {
                double sum = 0;
                foreach (double f in c) sum += f;
                return c.Count > 0 ? sum / c.Count : 0;
            }

            static double Max(List<double> c)
            {
                double m = 0;
                foreach (double f in c) m = Math.Max(m, f);
                return m;
            }

            public string Line(string name)
            {
                var b = new StringBuilder(name).Append(": ").Append(_c[0].Count).Append(" frames, ")
                    .Append((1000 / Math.Max(1e-6, Mean(_c[0]))).ToString("0", CultureInfo.InvariantCulture)).Append(" fps");
                for (int i = 0; i < Names.Length; i++)
                    b.Append(", ").Append(Names[i]).Append(' ').Append(_c[i].Count == 0 ? "n/a" : Mean(_c[i]).ToString("0.00", CultureInfo.InvariantCulture) + " / p99 " +
                        P(_c[i], 0.99).ToString("0.00", CultureInfo.InvariantCulture) + " / max " + Max(_c[i]).ToString("0.00", CultureInfo.InvariantCulture));
                b.Append(" ms; ").Append(_gcs).Append(" collections in ").Append(_gcFrames).Append(" frames, ").Append(_skipped)
                    .Append(" PAL fields skipped in ").Append(_skipFrames).Append(" frames, the wash held in ").Append(_held).Append(" frames; the tick's parts");
                for (int i = 0; i < Parts.Length; i++)
                    b.Append(i == 0 ? " " : ", ").Append(Parts[i]).Append(' ').Append((_parts[i] / Math.Max(1, _partFrames)).ToString("0.00", CultureInfo.InvariantCulture));
                return b.Append(" ms").ToString();
            }

            public string Json(string name)
            {
                var b = new StringBuilder("{ \"leg\": \"").Append(name).Append("\", \"frames\": ").Append(_c[0].Count);
                for (int i = 0; i < Names.Length; i++)
                    b.Append(", \"").Append(Names[i]).Append("\": { \"mean\": ").Append(F(Mean(_c[i]))).Append(", \"p99\": ").Append(F(P(_c[i], 0.99)))
                        .Append(", \"max\": ").Append(F(Max(_c[i]))).Append(" }");
                b.Append(", \"collections\": ").Append(_gcs).Append(", \"collectionFrames\": ").Append(_gcFrames).Append(", \"fieldsSkipped\": ")
                    .Append(_skipped).Append(", \"skipFrames\": ").Append(_skipFrames).Append(", \"washHeldFrames\": ").Append(_held).Append(", \"washPartsMs\": [");
                for (int i = 0; i < Parts.Length; i++) b.Append(i == 0 ? "" : ", ").Append(F(_parts[i] / Math.Max(1, _partFrames)));
                return b.Append("] }").ToString();
            }
        }
    }
}
