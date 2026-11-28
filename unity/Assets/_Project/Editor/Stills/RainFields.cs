using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Num = System.Numerics;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// Rain on the lens through the feed (w21-rain-on-camera 1.2, FEED-13): a recorded rain flight (tools/replay --record ...
    /// --rain rain|severerain, on the test range or the floor) flown again from its log's sticks, its lens's water stepped on
    /// its poses each field as FlightScene does (LensView on LensWater), and chosen fields drawn through the feed over the test
    /// range at the flight's poses as FlightScene's field latch draws them (the camera at each field's reference instant, the
    /// rows through its rolling-shutter track), under the range's overcast preset. Each field is drawn with the lens's water and
    /// again dry (the same field, seed and noise), with the glass's map beside it, and a line in fields.txt: the pose, the air
    /// at the lens, the wind against the nose, the rain arriving and the drops. Without <c>-at</c> it picks its fields from a
    /// first pass: the slowest airborne hover, the fastest nose-first flight, the steepest bank, and the wettest lens. A
    /// Sunny log must stay dry at every field. Needs graphics (never <c>-nographics</c>); exit code 1 on any error.
    /// <c>-batchmode -quit -executeMethod Fpv450.RainFields.Run -log &lt;log&gt; -out &lt;folder&gt; [-at s,s,...] [-dump &lt;file&gt;]</c>;
    /// <c>-dump</c> writes the wettest field's map (LensMap.Save) for the feed's cost run (NoclipFeed's <c>-feedlens</c>).
    /// </summary>
    public static class RainFields
    {
        const string Tag = "[Rain]";
        const int Width = 1920, Height = 1080, MapImage = 384;

        // The drawn side's CPU time a field: LensView.Advance (LensWater's field and the map's redraw when the water moved).
        static double _cpuMs, _cpuMaxMs;
        static int _cpuFields;

        sealed class Metrics
        {
            public long Field;
            public double Time, Speed, Forward, Bank, Tilt, HeadWind, Up;
            public int Drops, Sliding;
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                string log = Arg(args, "-log"), outDir = Arg(args, "-out");
                if (log == null || outDir == null) throw new ArgumentException("usage: -executeMethod Fpv450.RainFields.Run -log <log> -out <folder> [-at s,s,...] [-dump <file>]");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("no graphics device: run Unity without -nographics");
                ShaderUtil.allowAsyncCompilation = false;
                string folder = Path.GetFullPath(outDir);
                Directory.CreateDirectory(folder);
                string at = Arg(args, "-at");
                var picks = new SortedDictionary<long, string>();
                List<Metrics> all = Fly(log, null, null, null);
                if (at != null)
                    foreach (string s in at.Split(','))
                        picks[(long)Math.Round(double.Parse(s, CultureInfo.InvariantCulture) * FpvCamera.FieldsPerSecond)] = "at " + s + " s";
                else Pick(all, picks);
                Capture(log, folder, picks, Arg(args, "-dump"));
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        // The first pass's picks: the hover (the slowest airborne field after the climb, latest of the ties, so the lens has
        // had its time), the fastest nose-first flight, the steepest bank, the wettest lens.
        static void Pick(List<Metrics> all, SortedDictionary<long, string> picks)
        {
            Metrics hover = null, forward = null, bank = null, wet = null;
            foreach (Metrics m in all)
            {
                if (m.Up < 5) continue;
                if (hover == null || m.Speed <= hover.Speed + 0.05) hover = m;
                if (forward == null || m.Forward > forward.Forward) forward = m;
                if (bank == null || Math.Abs(m.Bank) > Math.Abs(bank.Bank)) bank = m;
                if (wet == null || m.Drops > wet.Drops) wet = m;
            }
            if (hover != null) picks[hover.Field] = (picks.TryGetValue(hover.Field, out string h) ? h + ", " : "") + "hover";
            if (forward != null) picks[forward.Field] = (picks.TryGetValue(forward.Field, out string f) ? f + ", " : "") + "forward";
            if (bank != null) picks[bank.Field] = (picks.TryGetValue(bank.Field, out string b) ? b + ", " : "") + "bank";
            if (wet != null) picks[wet.Field] = (picks.TryGetValue(wet.Field, out string w) ? w + ", " : "") + "wettest";
        }

        /// <summary>
        /// Flies the log again from its sticks (the header's setup on its world, as FlightReplay does, never checked here: the
        /// replay tool checks), the lens stepped each field; each field's metrics, and at a field in picks the draw.
        /// </summary>
        static List<Metrics> Fly(string log, SortedDictionary<long, string> picks, Action<long, string, PoseBuffer, LensView, FlightSetup, Metrics> draw,
            Action<string> header)
        {
            using FlightLogReader reader = FlightLogReader.Open(log);
            LogHeader h = reader.Header;
            FlightWorld world = h.WorldId == "floor" ? FlightWorld.Floor()
                : h.WorldId == FlightWorlds.TestRangeMap ? FlightWorlds.TestRange(AppData.Root).Open()
                : throw new InvalidOperationException("the log's world '" + h.WorldId + "' is neither the floor nor the test range");
            var problems = new List<string>();
            FlightSetup setup = h.ToSetup(world, problems);
            if (problems.Count > 0) throw new InvalidOperationException("the log's setup: " + problems[0]);
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            var core = new FlightCore(setup);
            using WindField air = core.Wind;
            var poses = new PoseBuffer();
            core.GetPose(out PoseFrame first);
            core.GetSignals(out FeedSignals signals);
            poses.Begin(0, first, signals);
            var lens = new LensView();
            lens.Begin(0, spec, setup.Mass, signals.RainRateMmPerHour, setup.Seed);
            header?.Invoke(string.Format(CultureInfo.InvariantCulture, "{0}: seed 0x{1:X}, world '{2}', {3} wind, {4} at {5:0.0} mm/h, start {6:0.0}° heading",
                Path.GetFileName(log), setup.Seed, h.WorldId, setup.Wind.Level, setup.Rain, signals.RainRateMmPerHour,
                Math.Atan2(2 * (setup.StartAttitude.W * setup.StartAttitude.Y), 1 - 2 * setup.StartAttitude.Y * setup.StartAttitude.Y) * 180 / Math.PI));
            var all = new List<Metrics>();
            double startY = setup.StartPosition.Y;
            long lastField = -1;
            while (reader.Next())
            {
                if (reader.Kind != LogFormat.StepBlock) continue;
                LogFormat.DecodeStick(reader.Payload, out StickSample stick);
                core.Advance(stick);
                core.GetPose(out PoseFrame pose);
                core.GetSignals(out signals);
                poses.Publish(pose, signals);
                long field = FieldCamera.LatestField(pose.Step);
                if (field < 0 || field == lastField) continue;
                lastField = field;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                lens.Advance(poses);
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                _cpuMs += ms;
                _cpuMaxMs = Math.Max(_cpuMaxMs, ms);
                _cpuFields++;
                if (!poses.TryRead(field * FlightCore.FieldSteps + FlightCore.FieldSteps / 2, out PoseFrame mid)) continue;
                Metrics m = Measure(field, mid, startY, lens.Water);
                all.Add(m);
                if (setup.Rain == RainLevel.Sunny && (lens.Map.Wet || lens.Water.Drops.Length > 0))
                    throw new InvalidOperationException($"a Sunny flight's lens is wet at field {field}");
                // The draw needs the field's reads up to SampleLead steps past its end (FieldCamera's history).
                if (picks != null && picks.TryGetValue(field, out string why))
                {
                    long until = (field + 1) * FlightCore.FieldSteps + Feed.RollingShutter.SampleLead;
                    while (core.State.Step < until && reader.Next())
                    {
                        if (reader.Kind != LogFormat.StepBlock) continue;
                        LogFormat.DecodeStick(reader.Payload, out stick);
                        core.Advance(stick);
                        core.GetPose(out pose);
                        core.GetSignals(out signals);
                        poses.Publish(pose, signals);
                    }
                    draw(field, why, poses, lens, setup, m);
                    lastField = FieldCamera.LatestField(core.State.Step) - 1; // the lens steps the fields it skipped next
                }
            }
            return all;
        }

        // The field's middle pose against the wind and the ground: the speed, its part along the nose, the bank (the right
        // wing's drop, + right wing down) and the whole tilt, the wind along the nose (the air at the lens plus the lens's own
        // velocity; − into the wind), the height over the start.
        static Metrics Measure(long field, in PoseFrame p, double startY, LensWater water)
        {
            Quat q = p.Attitude;
            Double3 fwd = q.Rotate(new Double3(0, 0, 1)), right = q.Rotate(new Double3(1, 0, 0)), up = q.Rotate(new Double3(0, 1, 0));
            Double3 wind = q.Rotate(p.AirVelocityAtCamera) + p.Velocity;
            int sliding = 0;
            foreach (LensDrop d in water.Drops) if (d.Sliding) sliding++;
            return new Metrics
            {
                Field = field, Time = p.TimeS, Speed = p.Velocity.Length(), Forward = Double3.Dot(p.Velocity, fwd),
                Bank = -Math.Asin(Math.Max(-1, Math.Min(1, right.Y))) * 180 / Math.PI, Tilt = Math.Acos(Math.Max(-1, Math.Min(1, up.Y))) * 180 / Math.PI,
                HeadWind = Double3.Dot(wind, fwd), Up = p.Position.Y - startY, Drops = water.Drops.Length, Sliding = sliding,
            };
        }

        static void Capture(string log, string folder, SortedDictionary<long, string> picks, string dump)
        {
            EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Single);
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            Vector3 mount = spec.CameraLens is Double3 lensAt ? V(lensAt) : throw new InvalidOperationException("drone.json has no camera.lens_m");
            Camera own = Camera.main;
            if (own != null) own.gameObject.SetActive(false);
            var body = new GameObject("Drone").transform;
            var eye = new GameObject("FpvCamera");
            eye.transform.SetParent(body, false);
            eye.transform.localPosition = mount;
            Camera camera = eye.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 3000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.enabled = false;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            LookSetup look = Object.FindFirstObjectByType<LookSetup>() ?? throw new InvalidOperationException("the test range has no LookSetup");
            int preset = Array.FindIndex(look.presets, p => p != null && p.name == "overcast");
            if (preset < 0) throw new InvalidOperationException("the range's look has no overcast preset");
            look.Select(preset);
            look.hour = 12f;
            look.Apply();
            look.viewCamera = camera;

            DroneModels models = DroneModels.Load() ?? throw new InvalidOperationException("no Resources/DroneModels");
            GameObject model = Object.Instantiate(models.legsOn, body, false);
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            var props = new PropView();
            props.Bind(model, spec);

            FeedSettings settings = Object.Instantiate(FeedSettings.Load());
            settings.hideFlags = HideFlags.DontSave;
            var signals = new StubFeedSignals();
            FpvCamera rig = eye.AddComponent<FpvCamera>();
            rig.settings = settings;
            rig.Signals = signals;
            var shutter = new Feed.RollingShutter();
            rig.Shutter = shutter;
            var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var wetImage = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var dryImage = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var glass = new Texture2D(MapImage, MapImage, TextureFormat.RGB24, false);
            var samples = new Num.Quaternion[Feed.RollingShutter.Samples];
            var lines = new List<string>();
            string flight = "";
            int wettest = -1, k = 0;
            try
            {
                rig.Driven = true;
                rig.Begin();
                if (!rig.Running) throw new InvalidOperationException("the feed's rig did not begin");
                rig.Output.targetTexture = target;
                double exposure = Feed.RollingShutter.ExposureS(signals.SceneLuminance);
                Fly(log, picks, (field, why, poses, lens, setup, m) =>
                {
                    long start = FieldCamera.FieldStartUs(field);
                    if (!FieldCamera.At(poses, start + (long)Feed.RollingShutter.ReferenceUs, out PoseFrame pose))
                        throw new InvalidOperationException("the history lost field " + field);
                    body.SetPositionAndRotation(V(pose.Position), Q(pose.Attitude));
                    props.Field(poses, start, pose, exposure, settings);
                    Fill(poses, start, pose.Attitude, samples);
                    shutter.Build(samples, exposure, settings.PixelsPerRad);
                    rig.UploadShutter();
                    rig.Step((int)field);
                    look.UpdateFog(camera);
                    camera.Render();
                    LensWater water = lens.Water;
                    LensView.Draw(water, lens.Map); // the map at this field exactly
                    string name = string.Format(CultureInfo.InvariantCulture, "{0:00}_t{1:000.00}", k, pose.TimeS);
                    rig.ShowLens(lens.Map);
                    Shoot(rig, target, wetImage, Path.Combine(folder, name + "_wet.png"));
                    rig.ShowLens(null);
                    Shoot(rig, target, dryImage, Path.Combine(folder, name + "_dry.png"));
                    Glass(lens.Map, settings, glass, Path.Combine(folder, name + "_glass.png"));
                    Diff(wetImage, dryImage, out double changed, out double mean);
                    Rain(water, out double meanR, out double maxR, out double water3);
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0} ({1}): field {2}, t {3:0.00} s, {4:0.0} m up; speed {5:0.0} m/s, {6:0.0} along the nose; bank {7:0.0}°, tilt {8:0.0}°; " +
                        "wind along the nose {9:0.0} m/s ({10}); air at the lens ({11:0.0}, {12:0.0}, {13:0.0}) m/s; rain arriving {14:0.00} drops/s, sheltered {15:0} %; " +
                        "lens: {16} drops ({17} sliding), {18} pearls, patch radius mean {19:0.00} mm max {20:0.00} mm, {21:0.00} mm³ of water; " +
                        "flight so far {22} arrived, {23} merged, {24} shed; the wet still differs from the dry in {25:0.0} % of its pixels (mean {26:0.0} levels there)",
                        name, why, field, pose.TimeS, m.Up, m.Speed, m.Forward, m.Bank, m.Tilt, m.HeadWind, m.HeadWind < 0 ? "into the wind" : "away from it",
                        pose.AirVelocityAtCamera.X, pose.AirVelocityAtCamera.Y, pose.AirVelocityAtCamera.Z, water.ArrivalRate, water.Sheltered * 100,
                        lens.Map.Drops, m.Sliding, lens.Map.Pearls, meanR * 1e3, maxR * 1e3, water3 * 1e9, water.Arrived, water.Merged, water.Shed,
                        changed * 100, mean));
                    Debug.Log($"{Tag} {lines[lines.Count - 1]}");
                    if (dump != null && (wettest < 0 || lens.Map.Drops + lens.Map.Pearls > wettest))
                    {
                        wettest = lens.Map.Drops + lens.Map.Pearls;
                        lens.Map.Save(dump);
                    }
                    k++;
                }, s => flight = s);
                flight += string.Format(CultureInfo.InvariantCulture, "; the lens's CPU time (LensView.Advance) {0:0.000} ms a field on average, {1:0.000} at most, over {2} fields of both passes",
                    _cpuMs / Math.Max(1, _cpuFields), _cpuMaxMs, _cpuFields);
                lines.Insert(0, flight);
                File.WriteAllLines(Path.Combine(folder, "fields.txt"), lines);
                Debug.Log($"{Tag} {flight}: {k} fields in {folder}" + (dump != null ? $"; the wettest map ({wettest} drops and pearls) in {dump}" : ""));
                if (k == 0) throw new InvalidOperationException("no field was drawn");
            }
            finally
            {
                rig.End();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(wetImage);
                Object.DestroyImmediate(dryImage);
                Object.DestroyImmediate(glass);
                Object.DestroyImmediate(settings);
            }
            EditorApplication.Exit(0);
        }

        static void Shoot(FpvCamera rig, RenderTexture target, Texture2D image, string file)
        {
            rig.Output.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            image.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(file, image.EncodeToPNG());
        }

        // The share of the picture's pixels the water changed against the same field dry, and their mean change in code levels.
        static void Diff(Texture2D wet, Texture2D dry, out double changed, out double mean)
        {
            Color32[] a = wet.GetPixels32(), b = dry.GetPixels32();
            long n = 0, sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int d = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                if (d == 0) continue;
                n++;
                sum += d;
            }
            changed = (double)n / a.Length;
            mean = n > 0 ? sum / (3.0 * n) : 0;
        }

        static void Rain(LensWater water, out double meanR, out double maxR, out double volume)
        {
            meanR = maxR = volume = 0;
            foreach (LensDrop d in water.Drops)
            {
                meanR += d.Radius;
                maxR = Math.Max(maxR, d.Radius);
                volume += d.Volume;
            }
            if (water.Drops.Length > 0) meanR /= water.Drops.Length;
        }

        // The glass as the camera looks out through it (+X right, +Y up, as the picture): the water by its slope (white the
        // steepest, the receding angle), the dry glass dark, the clear radius' rim, and the picture's border where its chief
        // rays cross the glass (red).
        static void Glass(LensMap map, FeedSettings settings, Texture2D image, string file)
        {
            var px = new Color32[MapImage * MapImage];
            float tanR = (float)Math.Tan(map.RecedingAngle);
            for (int y = 0; y < MapImage; y++)
                for (int x = 0; x < MapImage; x++)
                {
                    float gx = (x + 0.5f) / MapImage * 2f - 1f, gy = (y + 0.5f) / MapImage * 2f - 1f;
                    int i = Math.Min(LensMap.Size - 1, x * LensMap.Size / MapImage), j = Math.Min(LensMap.Size - 1, y * LensMap.Size / MapImage);
                    int at = (j * LensMap.Size + i) * 4;
                    byte v;
                    if (gx * gx + gy * gy > 1f) v = 0;
                    else if (map.Texels[at] > 0)
                    {
                        float s = (float)Math.Sqrt(map.Texels[at + 1] * map.Texels[at + 1] + map.Texels[at + 2] * map.Texels[at + 2]) / tanR;
                        v = (byte)(90 + 165 * Math.Min(1f, s));
                    }
                    else v = 40;
                    px[y * MapImage + x] = new Color32((byte)(v * 0.85f), v, v, 255);
                }
            float depth = LensMap.PupilDepth(map.ClearRadius, map.CurvatureRadius, settings.K1, settings.focalHalfWidths);
            for (int e = 0; e < 4000; e++)
            {
                // The picture's border, top-down half-widths, through the lens to its pinhole ray.
                float t = e / 1000f, sx, sy;
                if (t < 1) { sx = -1 + 2 * t; sy = -9f / 16f; }
                else if (t < 2) { sx = 1; sy = -9f / 16f + 2 * (t - 1) * 9f / 16f; }
                else if (t < 3) { sx = 1 - 2 * (t - 2); sy = 9f / 16f; }
                else { sx = -1; sy = 9f / 16f - 2 * (t - 3) * 9f / 16f; }
                float s = 1 + settings.K1 * (sx * sx + sy * sy);
                double dx = sx * s, dy = -sy * s, dz = settings.focalHalfWidths, n = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (!LensMap.Dome(0, 0, -depth, dx / n, dy / n, dz / n, map.CurvatureRadius, out double qx, out double qy)) continue;
                int x = (int)((qx / map.ClearRadius * 0.5 + 0.5) * MapImage), y = (int)((qy / map.ClearRadius * 0.5 + 0.5) * MapImage);
                if (x >= 0 && x < MapImage && y >= 0 && y < MapImage) px[y * MapImage + x] = new Color32(220, 40, 40, 255);
            }
            image.SetPixels32(px);
            image.Apply(false);
            File.WriteAllBytes(file, image.EncodeToPNG());
        }

        // FlightScene.FillSamples: the camera on the flexing frame against the body's attitude at the reference instant.
        static void Fill(PoseBuffer poses, long fieldStartUs, Quat reference, Num.Quaternion[] samples)
        {
            Quat inverse = reference.Conjugate();
            long first = fieldStartUs / FlightCore.StepUs - Feed.RollingShutter.SampleLead;
            Num.Quaternion held = Num.Quaternion.Identity;
            for (int i = 0; i < Feed.RollingShutter.Samples; i++)
            {
                if (poses.TryRead(first + i, out PoseFrame p))
                {
                    Quat q = inverse * FieldCamera.Attitude(p);
                    held = new Num.Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
                }
                samples[i] = held;
            }
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(in Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
