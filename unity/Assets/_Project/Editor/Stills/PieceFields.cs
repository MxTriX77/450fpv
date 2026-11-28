using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The pieces in the air through the feed (w16-plants-in-the-air 1.4): the sim's own flight on the farmland (its world, its
    /// props' cuts, the wash's dust), a throttle loop holding each hover's height over the ground with the sticks centred, from a
    /// start on its legs at each case's place; the coarse wash grid and the pieces (PlantPieces) ticked from its poses as the
    /// player's coarse helper ticks them, the pieces drawn by PlantPiecesView in the FPV camera through the feed, a field at a time.
    /// Cases: the tilled field's bare loam dry (hovers at 2, 1 and 0.5 m: the dust against clip F) and in rain (none: clip H's damp
    /// soil), the meadow by the village at 0.5 and 0.3 m (the props in its grass: the cut pieces against clip H's ending). A PNG per
    /// shot (FPV, and the chase view at each hover's end) and a summary per case in pieces.txt. Needs graphics (never
    /// <c>-nographics</c>). <c>-batchmode -quit -executeMethod Fpv450.PieceFields.Run -out &lt;folder&gt; [-case &lt;name&gt;] [-every n]</c>. The plants stand as the
    /// editor draws them (the player's PlantField is not run here: its bench, AppFlow's -plantbench, is the look's check).
    /// </summary>
    public static class PieceFields
    {
        const string Tag = "[Pieces]";
        const int Width = 1920, Height = 1080;

        /// <summary>A shot every this many fields of a hover's hold (<c>-every n</c>, 5 by default).</summary>
        static int Every = 5;

        /// <summary><c>-without</c>: each shot again without the pieces, at the same field of the feed (its noise the same), as
        /// "&lt;name&gt;_without.png": the pair's difference is the pieces alone.</summary>
        static bool Without;

        sealed class Case
        {
            public string Name;
            public double X, Z, Yaw;
            public Rain Rain;
            public double[] Hovers;
            public double Hold = 4;
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-out");
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("usage: -executeMethod Fpv450.PieceFields.Run -out <folder>");
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("no graphics device: run Unity without -nographics");
                ShaderUtil.allowAsyncCompilation = false;
                string folder = Path.GetFullPath(args[at + 1]);
                int only = Array.IndexOf(args, "-case"), everyAt = Array.IndexOf(args, "-every");
                Every = everyAt >= 0 && everyAt + 1 < args.Length ? int.Parse(args[everyAt + 1], CultureInfo.InvariantCulture) : 5;
                Without = Array.IndexOf(args, "-without") >= 0;
                var cases = new[]
                {
                    new Case { Name = "tilled_dry", X = 430, Z = 528, Yaw = 90 * Math.PI / 180, Rain = Rain.Sunny, Hovers = new[] { 2.0, 1.0, 0.5 } },
                    new Case { Name = "tilled_rain", X = 430, Z = 528, Yaw = 90 * Math.PI / 180, Rain = Rain.Rain, Hovers = new[] { 0.5 }, Hold = 3 },
                    new Case { Name = "meadow", X = FlightWorlds.FarmlandStart.X, Z = FlightWorlds.FarmlandStart.Z, Yaw = FlightWorlds.FarmlandStart.Yaw, Rain = Rain.Sunny, Hovers = new[] { 0.5, 0.3 } },
                };
                foreach (Case c in cases)
                    if (only < 0 || only + 1 >= args.Length || args[only + 1] == c.Name) Shoot(c, Path.Combine(folder, c.Name));
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

        static void Shoot(Case c, string dir)
        {
            Directory.CreateDirectory(dir);
            Scene scene = EditorSceneManager.OpenScene(EditorSetup.ProjectSetup.FarmlandScenePath, OpenSceneMode.Single);
            string root = AppData.Root;
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            Vector3 mount = spec.CameraLens is Double3 lens ? V(lens) : throw new InvalidOperationException("drone.json has no camera.lens_m");

            // The scene's camera off, its look kept, as FlightScene.Attach does on the farmland.
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
            data.antialiasing = AntialiasingMode.None;
            if (own != null && own.gameObject.scene == scene)
            {
                data.renderPostProcessing = own.GetUniversalAdditionalCameraData().renderPostProcessing;
                camera.farClipPlane = own.farClipPlane;
                own.gameObject.SetActive(false);
            }
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, FarmlandFeedDetail.FeedGrassDistance);

            DroneModels models = DroneModels.Load() ?? throw new InvalidOperationException("no Resources/DroneModels");
            GameObject model = Object.Instantiate(models.legsOn, body, false);
            foreach (Collider col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            var props = new PropView();
            props.Bind(model, spec);

            FeedSettings settings = Object.Instantiate(FeedSettings.Load());
            settings.hideFlags = HideFlags.DontSave;
            var stub = new StubFeedSignals();
            FpvCamera rig = eye.AddComponent<FpvCamera>();
            rig.settings = settings;
            rig.Signals = stub;
            var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            PlantPiecesView view = null;
            try
            {
                rig.Driven = true;
                rig.Begin();
                if (!rig.Running) throw new InvalidOperationException("the feed's rig did not begin");
                rig.Output.targetTexture = target;

                // The flight: the sim on the farmland from rest on its legs at the case's place.
                var inputs = new SessionInputs
                {
                    Drone = spec, Materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out ulong hash), MaterialsHash = hash,
                    Surfaces = SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath))),
                    Tether = Sim.Tether.TetherTable.Parse(File.ReadAllText(AppData.File(Sim.Tether.TetherTable.RelativePath))),
                };
                FlightWorlds.Farmland(root).ApplyTo(inputs);
                inputs.StartPoints = new[] { new Fpv450.App.Session.StartPoint { Name = c.Name, X = c.X, Z = c.Z, Yaw = c.Yaw } };
                inputs.Choices = new FlightChoices { StartPoint = c.Name, Legs = true, Payload = 1.0, Wind = WindLevel.Calm, Rain = c.Rain, TimeOfDay = 12 };
                FlightSetup setup = SessionSetup.Build(inputs, 0x1614, out _, out _);
                var core = new FlightCore(setup);
                var poses = new PoseBuffer();
                core.GetPose(out PoseFrame first);
                core.GetSignals(out FeedSignals firstSignals);
                poses.Begin(0, first, firstSignals);
                IWorldQuery world = setup.World.Query;
                var point = new XZ[1];
                var under = new GroundSample[1];
                double Ground(double x, double z)
                {
                    point[0] = new XZ(x, z);
                    world.SampleGround(point, under);
                    return under[0].Height;
                }

                // The coarse wash grid and the pieces, as FarmlandPlants builds them.
                double air = setup.Tether.Number("fiber.air_density_kg_m3");
                PlantMechanics[] bands = PlantBands.Mechanics(k => core.Contacts.Cover(k), air);
                var grass = PlantBands.Washed.Select(b => bands[b]).ToArray();
                var motors = setup.Propulsion.Motors.Select(m => m.Position).ToArray();
                var clockwise = setup.Propulsion.Motors.Select(m => m.Clockwise).ToArray();
                Moisture moisture = SurfaceTable.MoistureOf(setup.Rain);
                var coarse = new PlantWash(setup.Tether, setup.Propulsion.Props.Diameter / 2, motors, world, new PlantAir(core.Wind), grass, inputs.Surfaces, moisture,
                    PlantField.LevelSize[1], PlantField.LevelCell[1]);
                var pieces = new PlantPieces(world, core.Wind, inputs.Surfaces, moisture, k => core.Contacts.Cover(k), motors, clockwise, air);
                view = new PlantPiecesView();

                double exposure = RollingShutter.ExposureS(stub.SceneLuminance);
                long step = 0;
                int field = 0, shots = 0;
                double integral = 0;
                var lines = new List<string>();
                var drawn = new List<string>();
                double hover = c.Hovers[0];
                void Fly(long until, bool shoot, string label)
                {
                    while (step < until)
                    {
                        step++;
                        core.GetPose(out PoseFrame p);
                        core.Advance(Sticks(p, step < 16));
                        core.GetPose(out PoseFrame now);
                        core.GetSignals(out FeedSignals signals);
                        poses.Publish(now, signals);
                        if (step % FlightCore.FieldSteps != 0) continue;
                        field++;
                        pieces.Field(signals, poses);
                        pieces.Advance(coarse, poses);
                        if (!shoot || field % Every != 0) continue;
                        Shot(now, label + "_" + (step / 20).ToString("0000"), false);
                        drawn.Add($"{label} {now.TimeS:0.00} s: {view.ShownPieces} pieces, {view.ShownDust} puffs; {pieces.PiecesShape()}");
                    }
                }

                // The pilot: the hover's height over the ground (the body's origin, as PlantPilot holds it) by a PI loop on the
                // throttle, the place by tilting toward it (at most 1 m/s and 12°), the start's heading by the yaw; acro's rates
                // from the sticks' small deflections against the attitude's error.
                StickSample Sticks(in PoseFrame p, bool idle)
                {
                    Double3 x = p.Position, v = p.Velocity;
                    double err = Ground(x.X, x.Z) + hover - x.Y;
                    integral = Math.Max(-2, Math.Min(2, integral + err * FlightCore.Dt));
                    double throttle = Math.Max(0.1, Math.Min(0.8, 0.40 + 0.15 * err + 0.10 * integral - 0.12 * v.Y));
                    Double3 f = p.Attitude.Rotate(new Double3(0, 0, 1)), r = p.Attitude.Rotate(new Double3(1, 0, 0));
                    double pitch = Math.Asin(Math.Max(-1, Math.Min(1, -f.Y))) * 180 / Math.PI, roll = Math.Asin(Math.Max(-1, Math.Min(1, -r.Y))) * 180 / Math.PI;
                    double psi = Math.Atan2(f.X, f.Z), sin = Math.Sin(psi), cos = Math.Cos(psi);
                    double ahead = v.X * sin + v.Z * cos, right = v.X * cos - v.Z * sin;
                    double dx = c.X - x.X, dz = c.Z - x.Z, d = Math.Sqrt(dx * dx + dz * dz), want = Math.Min(1.0, 0.8 * d);
                    double wx = d > 1e-6 ? dx / d * want : 0, wz = d > 1e-6 ? dz / d * want : 0;
                    double pitchAim = Math.Max(-12, Math.Min(12, 6 * ((wx * sin + wz * cos) - ahead)));
                    double rollAim = Math.Max(-12, Math.Min(12, 6 * ((wx * cos - wz * sin) - right)));
                    double turn = ((c.Yaw - psi) * 180 / Math.PI % 360 + 540) % 360 - 180;
                    return new StickSample
                    {
                        HostUs = step * FlightCore.StepUs, Arm = step >= 8, Throttle = idle ? 0 : throttle,
                        Pitch = Math.Max(-0.3, Math.Min(0.3, 0.02 * (pitchAim - pitch))), Roll = Math.Max(-0.3, Math.Min(0.3, 0.02 * (rollAim - roll))),
                        Yaw = Math.Max(-0.2, Math.Min(0.2, 0.01 * turn)),
                    };
                }

                void Shot(in PoseFrame pose, string name, bool chase)
                {
                    body.SetPositionAndRotation(V(pose.Position), Q(pose.Attitude));
                    FlightScene.PlaceCamera(eye.transform, body, mount, chase);
                    props.Draw(pose, exposure);
                    int at = shots++;
                    foreach (bool with in Without ? new[] { true, false } : new[] { true })
                    {
                        view.Draw(with ? pieces : null, pose.TimeS, camera, exposure);
                        rig.Step(at);
                        camera.Render();
                        rig.Output.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(dir, name + (with ? "" : "_without") + ".png"), image.EncodeToPNG());
                        if (!Without) continue;
                        // The scene's render before the feed (its noise out of the pair's difference).
                        RenderTexture scene = camera.targetTexture;
                        var raw = new Texture2D(scene.width, scene.height, TextureFormat.RGB24, false);
                        RenderTexture.active = scene;
                        raw.ReadPixels(new Rect(0, 0, scene.width, scene.height), 0, 0, false);
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(dir, name + (with ? "" : "_without") + "_scene.png"), raw.EncodeToPNG());
                        Object.DestroyImmediate(raw);
                    }
                    view.Draw(pieces, pose.TimeS, camera, exposure); // the counts the summary reads
                }

                Fly(3000, false, "start");
                foreach (double h in c.Hovers)
                {
                    hover = h;
                    string label = "hover_" + h.ToString("0.0", CultureInfo.InvariantCulture).Replace(".", "");
                    long settle = step + 2500, hold = settle + (long)(c.Hold * 1000);
                    Fly(settle, false, label);
                    Fly(hold, true, label);
                    core.GetPose(out PoseFrame end);
                    Shot(end, label + "_chase", true);
                    double ground = Ground(end.Position.X, end.Position.Z);
                    pieces.DustShape(out double median, out double thickest, out double widest);
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0}: {1:0.00} m over the ground at {2:0.0} s; thrust {3:0.0} N; the dust emitted {4:0.00} g, aloft {5:0.00} g in {6} puffs (centres {11:0.00} m over the ground, median; σ_v to {12:0.00} m, σ_h to {13:0.00} m); {7} cut pieces born, {8} alive; {9} bits born; {10} pieces drawn last; passes recorded: pieces {14}, dust {15}",
                        label, end.Position.Y - ground, end.TimeS, end.RotorThrust.FrontLeft + end.RotorThrust.FrontRight + end.RotorThrust.RearLeft + end.RotorThrust.RearRight,
                        pieces.DustEmitted * 1000, pieces.DustMass * 1000, pieces.Live(PlantPieces.Kind.Dust), pieces.CutsBorn, pieces.Live(PlantPieces.Kind.Cut), pieces.BitsBorn,
                        view.ShownPieces, median, thickest, widest, view.PiecesRecorded, view.DustRecorded);
                    lines.Add(line);
                    Debug.Log($"{Tag} {c.Name} {line}");
                }
                File.WriteAllLines(Path.Combine(dir, "pieces.txt"), lines.Concat(drawn));
            }
            finally
            {
                view?.Dispose();
                rig.End();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(settings);
            }
        }

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(in Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
