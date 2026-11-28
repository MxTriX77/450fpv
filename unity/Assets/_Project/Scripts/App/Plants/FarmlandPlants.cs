using System;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The farmland's plants in the flight's air (w16-plants-in-the-air 1.3): for each flight, its plants' field (PlantField) on the
    /// flight's own wind field, its published poses and its world, and each drawn frame the bends at the frame's flight time (the
    /// PAL field's reference instant in the FPV view, FlightScene.DrawnStep), round the camera. The plants' shaders read the field
    /// (FarmlandPlantDrawing gave them the project's). With them the pieces in the air (1.4: PlantPieces on the coarse wash grid's
    /// helper, PlantPiecesView drawing them), fed the props' cuts from the flight's signals once a frame. Render side only: nothing
    /// here reaches the flight. <c>-plants 0</c> leaves the friend's own plant shaders and wind (the frame rate's A/B: its before);
    /// <c>-pieces 0</c> draws the plants without the pieces (the pieces' A/B).
    /// </summary>
    [DefaultExecutionOrder(110)] // after FlightScene has placed the drone and the camera for the frame
    public sealed class FarmlandPlants : MonoBehaviour
    {
        /// <summary>False with <c>-plants 0</c>; the pieces false with <c>-pieces 0</c> (or with the plants off).</summary>
        public static readonly bool On = Arg("-plants"), PiecesOn = Arg("-pieces");

        /// <summary>The compute shader in Settings/Plants/Resources.</summary>
        public const string Kernel = "PlantField";

        FlightScene _flight;
        PlantField _field;
        PlantPieces _pieces;
        PlantPiecesView _view;
        ComputeShader _cs;
        int _built = -1;
        bool _drawing;

        /// <summary>The flight's field, its pieces and their drawing, or null.</summary>
        public PlantField Field => _field;
        public PlantPieces Pieces => _pieces;
        public PlantPiecesView PiecesView => _view;

        /// <summary>The main thread's time in the field's Draw, ms, and the frame it was drawn in (the plant bench's).</summary>
        public double DrawMs { get; private set; }

        /// <summary>The main thread's time in the pieces' Draw, ms (the plant bench's).</summary>
        public double PiecesDrawMs { get; private set; }
        public int DrawFrame { get; private set; } = -1;

        static bool Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return !(i >= 0 && i + 1 < args.Length && args[i + 1] == "0");
        }

        void Start()
        {
            _flight = GetComponent<FlightScene>();
            _cs = Resources.Load<ComputeShader>(Kernel);
            if (!On) Debug.Log("[Plants] -plants 0: the farmland's plants keep the friend's wind");
            else if (_cs == null) Debug.LogError("[Plants] no Resources/" + Kernel + ": the farmland's plants stand as drawn");
            else if (!SystemInfo.supportsComputeShaders) Debug.LogWarning("[Plants] no compute shaders on this device: the farmland's plants stand as drawn");
        }

        void LateUpdate()
        {
            FlightSession session = _flight != null && _flight.Host != null ? _flight.Host.Session : null;
            if (!On || _cs == null || !SystemInfo.supportsComputeShaders || session == null || session.Flights == 0)
            {
                Stop();
                return;
            }
            if (session.Flights != _built) Build(session);
            if (_flight.DrawnStep < 0) return; // the last flight's pose still: this one's plants wait for its first
            ref readonly PosePair pair = ref session.Poses.Read();
            if (_pieces != null && pair.StartUs >= session.StartUs) _pieces.Field(pair.Signals, session.Poses); // every frame, drawn or held
            Transform eye = _flight.Eye;
            Camera camera = eye != null ? eye.GetComponent<Camera>() : null;
            if (camera == null || !camera.enabled) return; // a held field: nothing is drawn this frame
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool drawing = _field.Draw(_flight.DrawnStep * FlightCore.Dt, eye.position);
            long drawn = System.Diagnostics.Stopwatch.GetTimestamp();
            DrawMs = (drawn - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (drawing && _view != null) _view.Draw(_pieces, _flight.DrawnStep * FlightCore.Dt, camera, RollingShutter.ExposureS(_flight.Signals.SceneLuminance));
            PiecesDrawMs = (System.Diagnostics.Stopwatch.GetTimestamp() - drawn) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            DrawFrame = Time.frameCount;
            if (drawing && !_drawing)
                Debug.Log($"[Plants] the plants move in the flight's air: the wind's mean {_field.MeanMsPerSecond:0} ms a second of flight, the wash in {_field.WashTickMs:0.0} ms a tick");
            _drawing = drawing;
        }

        // The flight's field: its bands on the flight's own cover sets, the wash grids on the line's table and the drone's rotors.
        void Build(FlightSession session)
        {
            Stop();
            _built = session.Flights;
            FlightCore core = session.Core;
            FlightSetup setup = session.Setup;
            TetherTable table = setup.Tether ?? setup.StemAir;
            double air = table != null ? table.Number("fiber.air_density_kg_m3") : 1.225; // tether.json's (1.2's plants' air)
            PlantMechanics[] bands = PlantBands.Mechanics(k => core.Contacts.Cover(k), air);
            PlantWash fine = null, coarse = null;
            SurfaceTable surfaces = _flight.Host.Inputs?.Surfaces;
            if (table != null && surfaces != null && setup.Propulsion != null)
            {
                var motors = new Double3[4];
                for (int i = 0; i < 4; i++) motors[i] = setup.Propulsion.Motors[i].Position;
                double radius = setup.Propulsion.Props.Diameter / 2;
                var grass = Array.ConvertAll(PlantBands.Washed, b => bands[b]);
                Moisture moisture = SurfaceTable.MoistureOf(setup.Rain);
                var washAir = new PlantAir(core.Wind);
                fine = new PlantWash(table, radius, motors, core.World.Query, washAir, grass, surfaces, moisture,
                    PlantField.LevelSize[0], PlantField.LevelCell[0]);
                coarse = new PlantWash(table, radius, motors, core.World.Query, washAir, grass, surfaces, moisture,
                    PlantField.LevelSize[1], PlantField.LevelCell[1]);
            }
            else Debug.LogWarning("[Plants] the flight has no line's table, surfaces or rotors: its plants take the wind without the wash");
            if (coarse != null && PiecesOn)
            {
                var clockwise = new bool[4];
                var motors = new Double3[4];
                for (int i = 0; i < 4; i++)
                {
                    motors[i] = setup.Propulsion.Motors[i].Position;
                    clockwise[i] = setup.Propulsion.Motors[i].Clockwise;
                }
                _pieces = new PlantPieces(core.World.Query, core.Wind, surfaces, SurfaceTable.MoistureOf(setup.Rain), k => core.Contacts.Cover(k),
                    motors, clockwise, air);
                _view = new PlantPiecesView();
            }
            else if (!PiecesOn) Debug.Log("[Plants] -pieces 0: no pieces in the air");
            _field = new PlantField(bands, core.Wind, core.World.Query, session.Poses, fine, coarse, _cs, pieces: _pieces);
            Debug.Log($"[Plants] flight {session.Flights}: {PlantBands.Count} bands on {PlantField.Levels} levels, the wash grids " +
                      (fine != null ? $"{PlantField.LevelSize[0]} × {PlantField.LevelCell[0]} m and {PlantField.LevelSize[1]} × {PlantField.LevelCell[1]} m" : "off"));
        }

        void Stop()
        {
            if (_field == null) return;
            _field.Dispose();
            _field = null;
            _view?.Dispose();
            _view = null;
            _pieces = null;
            _built = -1;
            _drawing = false;
        }

        void OnDisable() => Stop();
    }
}
