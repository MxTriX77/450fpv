using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Num = System.Numerics;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The props in the frame (w13-props-in-frame 1.1): consecutive PAL fields of a hover through the feed, as the flight's
    /// FPV view draws them (FlightScene's field latch: the body and camera at each field's reference instant, the rows through
    /// the field's rolling-shutter track, the props by PropView at each row's read), on House and on Village, at noon and under
    /// overcast. The flight is the sim's own, stepped here at 1 kHz on the floor world with the repository's drone (a
    /// throttle loop holds it HoverM up, the sticks centred), and drawn over each map's start. One PNG per field, and a
    /// line per field in fields.txt: each front rotor's angle and turn since the last field, the smear (the rotor's speed
    /// times the feed's exposure) and where a blade crosses the picture (each field row's read, the blade's centre line
    /// through the feed's lens). Needs graphics (never <c>-nographics</c>); exit code 1 on any error.
    /// <c>-batchmode -quit -executeMethod Fpv450.PropFields.Run -out &lt;folder&gt; [-fields 24]</c>.
    /// </summary>
    public static class PropFields
    {
        const string Tag = "[Props]";
        const double HoverM = 2.5, SettleS = 4.0;
        const int Width = 1920, Height = 1080;

        sealed class Case
        {
            public string Map, Light, Scene;
            public StartPoint Start;
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-out");
            int fieldsAt = Array.IndexOf(args, "-fields");
            int fields = fieldsAt >= 0 && fieldsAt + 1 < args.Length ? int.Parse(args[fieldsAt + 1], CultureInfo.InvariantCulture) : 24;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("usage: -executeMethod Fpv450.PropFields.Run -out <folder> [-fields n]");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("no graphics device: run Unity without -nographics");
                ShaderUtil.allowAsyncCompilation = false;
                string folder = Path.GetFullPath(args[at + 1]);
                StartPoint house = FlightWorlds.TestRange(AppData.Root).StartPoints[0];
                var cases = new[]
                {
                    new Case { Map = "house", Light = "noon", Scene = TestRangeScene.ScenePath, Start = house },
                    new Case { Map = "house", Light = "overcast", Scene = TestRangeScene.ScenePath, Start = house },
                    new Case { Map = "village", Light = "noon", Scene = EditorSetup.ProjectSetup.FarmlandScenePath, Start = FlightWorlds.FarmlandStart },
                    new Case { Map = "village", Light = "overcast", Scene = EditorSetup.ProjectSetup.FarmlandScenePath, Start = FlightWorlds.FarmlandStart },
                };
                foreach (Case c in cases) Shoot(c, Path.Combine(folder, c.Map + "_" + c.Light), fields);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void Shoot(Case c, string dir, int fields)
        {
            Directory.CreateDirectory(dir);
            Scene scene = EditorSceneManager.OpenScene(c.Scene, OpenSceneMode.Single);
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            Vector3 mount = spec.CameraLens is Double3 lens ? V(lens) : throw new InvalidOperationException("drone.json has no camera.lens_m");

            // The scene's own camera off, its look kept (FlightScene.Attach); the range's look at noon or overcast as
            // FlightScene.Look sets it, the farmland's the friend's whatever the setup (D-088).
            Camera own = Camera.main;
            var body = new GameObject("Drone").transform;
            SceneManager.MoveGameObjectToScene(body.gameObject, scene);
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
            LookSetup look = Object.FindFirstObjectByType<LookSetup>();
            string light;
            if (look != null)
            {
                int preset = Array.FindIndex(look.presets, p => p != null && p.name == (c.Light == "noon" ? "day" : "overcast"));
                if (preset < 0) throw new InvalidOperationException("the range's look has no " + c.Light + " preset");
                look.Select(preset);
                look.hour = 12f;
                look.Apply();
                look.viewCamera = camera;
                light = $"the range's {look.Active.name} preset at 12:00, sun {look.State.SunElevationDeg:0.0}° up";
            }
            else
            {
                light = "the farmland's own light (D-088: the setup's hour and rain leave it as it is)";
            }
            if (own != null && own.gameObject.scene == scene)
            {
                if (look == null)
                {
                    data.renderPostProcessing = own.GetUniversalAdditionalCameraData().renderPostProcessing;
                    camera.farClipPlane = own.farClipPlane;
                    foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                        t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, FarmlandFeedDetail.FeedGrassDistance);
                }
                own.gameObject.SetActive(false);
            }

            DroneModels models = DroneModels.Load() ?? throw new InvalidOperationException("no Resources/DroneModels");
            GameObject model = Object.Instantiate(models.legsOn, body, false);
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            var props = new PropView();
            props.Bind(model, spec);
            if (props.Rotors != 4 || props.Hidden == 0) throw new InvalidOperationException($"the props bound {props.Rotors} rotors, hid {props.Hidden} slots");

            FeedSettings settings = Object.Instantiate(FeedSettings.Load());
            settings.hideFlags = HideFlags.DontSave;
            var signals = new StubFeedSignals();
            FpvCamera rig = eye.AddComponent<FpvCamera>();
            rig.settings = settings;
            rig.Signals = signals;
            var shutter = new RollingShutter();
            rig.Shutter = shutter;
            var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                rig.Driven = true;
                rig.Begin();
                if (!rig.Running) throw new InvalidOperationException("the feed's rig did not begin");
                rig.Output.targetTexture = target;

                // The hover: the sim on the floor world, from rest on its legs; drawn over the map's start.
                var inputs = new SessionInputs { Drone = spec, Materials = Materials(out ulong hash), MaterialsHash = hash, Choices = new FlightChoices { Legs = true } };
                FlightSetup setup = SessionSetup.Build(inputs, 7, out _, out _);
                var core = new FlightCore(setup);
                var poses = new PoseBuffer();
                core.GetPose(out PoseFrame first);
                poses.Begin(0, first, default);
                float ground = FarmlandExplore.Ground(scene, (float)c.Start.X, (float)c.Start.Z, out float h) ? h : 0f;
                Vector3 anchor = new Vector3((float)c.Start.X, ground, (float)c.Start.Z);
                Quaternion yaw = Quaternion.Euler(0f, (float)(c.Start.Yaw * 180 / Math.PI), 0f);
                long step = 0;
                void Advance(long until)
                {
                    while (step < until)
                    {
                        step++;
                        PoseFrame p = core.Pose();
                        double up = p.Position.Y - HoverM, climb = p.Velocity.Y;
                        double throttle = Math.Max(0.2, Math.Min(0.7, 0.42 - 0.08 * up - 0.12 * climb));
                        core.Advance(new StickSample { HostUs = step * FlightCore.StepUs, Arm = step >= 8, Throttle = step < 16 ? 0 : throttle });
                        poses.Publish(core.Pose(), default);
                    }
                }

                long firstField = (long)(SettleS * 1e6) / (FlightCore.FieldSteps * FlightCore.StepUs);
                double exposure = RollingShutter.ExposureS(signals.SceneLuminance);
                var samples = new Num.Quaternion[RollingShutter.Samples];
                var angles = new float[RollingShutter.Samples];
                var lines = new List<string>();
                var last = new double[4];
                int withBlade = 0, topCorner = 0, sides = 0;
                for (int k = 0; k < fields; k++)
                {
                    long field = firstField + k;
                    Advance(FlightCore.FieldSteps * (field + 1) + RollingShutter.SampleLead);
                    long start = FieldCamera.FieldStartUs(field);
                    if (!FieldCamera.At(poses, start + (long)RollingShutter.ReferenceUs, out PoseFrame pose))
                        throw new InvalidOperationException("the history lost field " + field);
                    body.SetPositionAndRotation(anchor + yaw * V(pose.Position - new Double3(setup.StartPosition.X, 0, setup.StartPosition.Z)), yaw * Q(pose.Attitude));
                    props.Field(poses, start, pose, exposure, settings);
                    Fill(poses, start, pose.Attitude, samples);
                    shutter.Build(samples, exposure, settings.PixelsPerRad);
                    rig.UploadShutter();
                    rig.Step(k);
                    if (look != null) look.UpdateFog(camera);
                    camera.Render();
                    rig.Output.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, k.ToString("00") + ".png"), image.EncodeToPNG());

                    var line = new StringBuilder(string.Format(CultureInfo.InvariantCulture, "{0:00} field {1} t {2:0.000} s, {3:0.00} m up",
                        k, field, pose.TimeS, pose.Position.Y));
                    string where = "";
                    for (int r = 1; r <= 3; r += 2) // front right, front left
                    {
                        PropView.Samples(poses, start, pose, r, angles);
                        double turn = k == 0 ? double.NaN : Wrap(pose.RotorAngle[r] - last[r]) * 180 / Math.PI;
                        last[r] = pose.RotorAngle[r];
                        string seen = Crossing(spec, mount, settings, pose, r, angles);
                        where += seen;
                        line.Append(string.Format(CultureInfo.InvariantCulture, "; {0}: {1:0.0}°, {2:0} rpm, turned {3:0.0}° (mod 180° {4:0.0}°), smear {5:0.0}°, blade {6}",
                            r == 1 ? "front right" : "front left", pose.RotorAngle[r] * 180 / Math.PI, pose.RotorSpeed[r] * 60 / (2 * Math.PI),
                            turn, turn % 180, PropView.SmearDeg(pose.RotorSpeed[r], exposure), seen.Length > 0 ? seen : "none"));
                    }
                    if (where.Length > 0) withBlade++;
                    if (where.Contains("top")) topCorner++;
                    if (where.Contains("side")) sides++;
                    lines.Add(line.ToString());
                }
                // The chase view (V) at the last field's pose: every row read at that instant, the speeds held (PropView.Draw).
                core.GetPose(out PoseFrame now);
                body.SetPositionAndRotation(anchor + yaw * V(now.Position - new Double3(setup.StartPosition.X, 0, setup.StartPosition.Z)), yaw * Q(now.Attitude));
                props.Draw(now, exposure);
                shutter.Clear();
                rig.UploadShutter();
                FlightScene.PlaceCamera(eye.transform, body, mount, true);
                rig.Step(fields);
                camera.Render();
                rig.Output.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, "chase.png"), image.EncodeToPNG());
                string summary = string.Format(CultureInfo.InvariantCulture,
                    "{0} {1}: {2} fields, {3}; exposure {4:0} µs. A front blade in {5} of {2} fields, in a top corner in {6}, at a side edge below the top third in {7}. " +
                    "The reference note's C-H group: none at this scale (D 2 frames, H none); B/G: right about 20 of 24, left about 16 of 24; A: top left 11, top right 12 of 24.",
                    c.Map, c.Light, fields, light, exposure * 1e6, withBlade, topCorner, sides);
                lines.Insert(0, summary);
                File.WriteAllLines(Path.Combine(dir, "fields.txt"), lines);
                Debug.Log($"{Tag} {summary} In {dir}");
                foreach (string l in lines.GetRange(1, Math.Min(4, lines.Count - 1))) Debug.Log($"{Tag}   {l}");
            }
            finally
            {
                rig.End();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(settings);
            }
        }

        static PoseFrame Pose(this FlightCore core)
        {
            core.GetPose(out PoseFrame p);
            return p;
        }

        static Sim.Contact.MaterialRow[] Materials(out ulong hash) =>
            Sim.Contact.PhysicsMaterials.Parse(File.ReadAllText(AppData.File(Sim.Contact.PhysicsMaterials.RelativePath)), out hash);

        // FlightScene.FillSamples: the camera on the flexing frame against the body's attitude at the reference instant.
        static void Fill(PoseBuffer poses, long fieldStartUs, Quat reference, Num.Quaternion[] samples)
        {
            Quat inverse = reference.Conjugate();
            long first = fieldStartUs / FlightCore.StepUs - RollingShutter.SampleLead;
            Num.Quaternion held = Num.Quaternion.Identity;
            for (int i = 0; i < RollingShutter.Samples; i++)
            {
                if (poses.TryRead(first + i, out PoseFrame p))
                {
                    Quat q = inverse * FieldCamera.Attitude(p);
                    held = new Num.Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
                }
                samples[i] = held;
            }
        }

        /// <summary>
        /// Where rotor c's blades cross the picture in this field: for each field row, its blades' centre lines (hub to tip,
        /// the sim's straight blade) at the rotor's angle at the row's read, through the feed's pinhole and lens, crossing the
        /// row's line inside the picture. "top-left" / "top-right" for crossings in the top third within a quarter of the
        /// width from a side, "side-left" / "side-right" for crossings below the top third within a quarter of the width from a
        /// side, "middle" otherwise; empty when no row meets a blade.
        /// </summary>
        static string Crossing(DroneSpec spec, Vector3 mount, FeedSettings settings, in PoseFrame pose, int c, float[] angles)
        {
            Double3 hub = spec.Motors[c].Position;
            double R = spec.Props.Diameter / 2, r0 = spec.Blade.HubDiameter / 2, s = spec.Motors[c].Clockwise ? -1 : 1;
            int blades = spec.Props.Blades;
            const int Points = 40;
            var found = new SortedSet<string>();
            var ys = new double[Points];
            var xs = new double[Points];
            var ok = new bool[Points];
            for (int row = 0; row < RollingShutter.Rows; row++)
            {
                double t = RollingShutter.RowReadUs(row);
                double si = t / RollingShutter.SampleUs + RollingShutter.SampleLead;
                int i = Math.Min((int)si, RollingShutter.Samples - 2);
                double theta = pose.RotorAngle[c] + angles[i] + (angles[i + 1] - angles[i]) * (si - i);
                double line = row + 0.5; // the row's centre, in rows from the top
                for (int b = 0; b < blades; b++)
                {
                    double a = theta + 2 * Math.PI * b / blades;
                    for (int j = 0; j < Points; j++)
                    {
                        double r = r0 + (R - r0) * j / (Points - 1);
                        double x = hub.X + r * Math.Cos(a) - mount.x, y = hub.Y - mount.y, z = hub.Z + r * s * Math.Sin(a) - mount.z;
                        ok[j] = z > 1e-4 && Picture(settings, x / z, y / z, out xs[j], out ys[j]);
                    }
                    for (int j = 1; j < Points; j++)
                    {
                        if (!ok[j] || !ok[j - 1] || (ys[j] - line) * (ys[j - 1] - line) > 0) continue;
                        double f = ys[j] == ys[j - 1] ? 0 : (line - ys[j - 1]) / (ys[j] - ys[j - 1]);
                        double px = xs[j - 1] + (xs[j] - xs[j - 1]) * f;
                        if (px < -1 || px > 1) continue;
                        string side = px < -0.5 ? "left" : px > 0.5 ? "right" : null;
                        found.Add(side == null ? "middle" : (row < RollingShutter.Rows / 3 ? "top-" : "side-") + side);
                    }
                }
            }
            return string.Join(" ", found);
        }

        /// <summary>A pinhole ray (x/z, y/z, y up) through the feed's lens into the picture: x in half-widths (−1 left to 1
        /// right) and the row from the top; false outside the picture's height.</summary>
        static bool Picture(FeedSettings settings, double tx, double ty, out double x, out double row)
        {
            double k1 = settings.K1, f = settings.focalHalfWidths;
            double ux = f * tx, uy = f * ty, ru = Math.Sqrt(ux * ux + uy * uy), rd = ru;
            for (int n = 0; n < 8; n++) rd -= (rd + k1 * rd * rd * rd - ru) / (1 + 3 * k1 * rd * rd);
            double scale = ru > 1e-9 ? rd / ru : 1;
            x = ux * scale;
            double top = 0.5 - 0.5 * uy * scale * FeedSettings.Aspect;
            row = top * RollingShutter.Rows;
            return top >= 0 && top <= 1;
        }

        static double Wrap(double a)
        {
            a %= 2 * Math.PI;
            return a < 0 ? a + 2 * Math.PI : a;
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(in Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
