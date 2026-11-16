using System;
using System.Diagnostics;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Fpv450.App
{
    /// <summary>
    /// The plants' bend field (w16-plants-in-the-air 1.3, design.md §7.1): each drawn frame, at the frame's flight time, every band's
    /// bend in the flight's air on five levels round the camera and the drone, which the plants' shaders read (PlantBend.hlsl).
    /// - **The wind** (PlantAir, 1.2's closed form): its mean at a plant (PlantAir.Mean: the mean air at the load point, the
    ///   fluctuation's covariance, the mean lean) on three coarse grids on a helper thread, for flight times ahead of the frame;
    ///   the GPU interpolates them in space and time and sums the modes per texel (PlantField.compute), at the frame's time,
    ///   statelessly: the gusts reach each plant when the field brings them there. The wake eddies' scale is the wake grid's own
    ///   obstacle height at each texel near the camera (their phase is anchored at the world's origin, so an interpolated height
    ///   would scramble it).
    /// - **The wash** (PlantWash, 1.2's grids): a fine (24 × 5 cm) and a coarse (32 × 15 cm) grid round the drone for the grass
    ///   classes' middle culms, ticked from the published poses on a second helper thread; each tick's stands go to the GPU, whose texels are the
    ///   nodes, and the gusts are linearised about the wash's bend in the wash's air (PlantAir.Sample's arithmetic).
    /// - **The levels**, finest first: the two wash grids, near (96 × 0.5 m), mid (96 × 2 m) and far (64 × 8 m) round the camera.
    /// Nothing here touches the flight: the helpers read the flight's wind field (its immutable layers and its wake grid, as
    /// 1.2 does) and the published poses; the flight's step never waits on them (PLT-04).
    /// </summary>
    public sealed class PlantField : IDisposable
    {
        public const int Levels = 5;

        /// <summary>The levels: texels a side and their cell, m. The wash grids': PlantWash's coarse default, and a fine one of 24 ×
        /// 5 cm (design.md §4.4's 32–48 cut to 24 for the helper's CPU: the columns' footprints lie within 0.25 m of the centre, the
        /// level's full weight within 0.35 m).</summary>
        public static readonly int[] LevelSize = { 24, 32, 96, 96, 64 };
        public static readonly double[] LevelCell = { 0.05, 0.15, 0.5, 2, 8 };

        /// <summary>The bands each level holds, in its slices' order (PlantBend.hlsl's PlantBandLevels and slices).</summary>
        public static readonly int[][] LevelBands =
        {
            Range(0, PlantBands.Grass), Range(0, PlantBands.Grass), Range(0, PlantBands.Branch), Range(0, PlantBands.Branch + 1),
            Range(PlantBands.Branch, PlantBands.Count),
        };

        /// <summary>
        /// The wind's mean grids (numerical): the grass's near the camera (26 × 2 m, a snapshot every 4 s of flight time; the
        /// tallest culms barely stand, W/k up to 0.93, so their lean follows a wake's mean within metres) and out to the mid
        /// level (24 × 8 m, every 8 s), the crowns' twigs and branches (24 × 8 m, every 4 s) and the far level's whole plants and
        /// branches (12 × 48 m, every 8 s): the helper's CPU in the player, each over its levels and computed up to
        /// Lead ahead of the frame and centred where the camera will be. The mean air at a plant moves with the direction's
        /// meander (τ 120 s) and is smooth over metres (design.md §7.1); the wake eddies' scale, which is not, is read from the wake
        /// grid's cells on the first two (WakeCells).
        /// </summary>
        public static readonly (int Size, double Spacing, double Step, int[] Bands, bool WakeCells)[] Grids =
        {
            (26, 2, 4, Range(0, PlantBands.Grass), true),
            (24, 8, 8, Range(0, PlantBands.Grass), true),
            (24, 8, 4, Range(PlantBands.TreeTwig, PlantBands.Branch + 1), true),
            (12, 48, 8, Range(PlantBands.Branch, PlantBands.Count), false),
        };

        const double Lead = 1.5;

        /// <summary>
        /// The wash's steady air where no wash grid holds a plant (PlantAir.Sample: the line's law at its load point, quasi-static,
        /// design.md §4.5): RotorAir.At on a polar grid round the drone each tick, at each of the first SteadyBands bands' load
        /// height over the ground: SteadyRings rings from SteadyR0 m, each SteadyGrowth times the last (to 33 m), SteadyAngles
        /// angles (numerical). A plant takes it where it passes SteadyMin (m/s: a grass culm's, under which its lean moves under
        /// 0.01 rad; a crown's twigs and branches, 0.3 m/s, under which a twig's moves under 1e-4 rad, derived from 1.2's twig).
        /// </summary>
        public const int SteadyBands = PlantBands.Branch + 1, SteadyRings = 33, SteadyAngles = 16;
        public const double SteadyR0 = 0.1, SteadyGrowth = 1.2;
        public static double SteadyMin(int band) => band < PlantBands.Grass ? 0.005 : 0.3;

        /// <summary>The dispatches each frame: a level, the grid its bands' means come from, and those bands.</summary>
        public static readonly (int Level, int Grid, int[] Bands)[] Dispatches =
        {
            (0, 0, Range(0, PlantBands.Grass)), (1, 0, Range(0, PlantBands.Grass)),
            (2, 0, Range(0, PlantBands.Grass)), (2, 2, Range(PlantBands.TreeTwig, PlantBands.Branch)),
            (3, 1, Range(0, PlantBands.Grass)), (3, 2, Range(PlantBands.TreeTwig, PlantBands.Branch + 1)),
            (4, 3, Range(PlantBands.Branch, PlantBands.Count)),
        };

        /// <summary>A band's mean grid on a level.</summary>
        public static int GridOf(int level, int band)
        {
            foreach ((int l, int g, int[] bands) in Dispatches)
                if (l == level && Array.IndexOf(bands, band) >= 0) return g;
            return -1;
        }

        public const int ThreadGroup = 8;

        readonly PlantMechanics[] _m;
        readonly PlantAir _frame, _meanAir;
        readonly WindField _wind;
        readonly PlantWash _fine, _coarse;
        readonly PoseBuffer _poses;
        readonly IWorldQuery _world;
        readonly Grid[] _grids;
        readonly Wash _washFine, _washCoarse;
        readonly Steady _steady;

        // The modes' tables (static for the flight) and the frame's data.
        readonly PlantMode[] _open, _wakes;
        readonly PlantMode[][] _strips;
        readonly double[] _stripConvection;
        readonly int _stripModes;
        readonly Vector4[] _openWaveData, _stripWaveData, _stripAmpData, _wakeWaveData, _wakeAmpData;
        readonly float[] _openRateData;
        readonly Vector2[] _wakeFrameData;

        // The GPU.
        readonly ComputeShader _cs;
        readonly int _kernel;
        readonly ComputeBuffer _noWash, _bandBuf, _openWave, _openRate, _openAmp, _stripWave, _stripAmp, _wakeWave, _wakeAmp, _wakeFrame;
        readonly ComputeBuffer[] _dispatchBands = new ComputeBuffer[Dispatches.Length];
        readonly RenderTexture[] _out = new RenderTexture[Levels];
        readonly Vector4[] _boxes = new Vector4[Levels];

        // The helpers, and the camera they centre on.
        readonly Thread _meanThread, _washThread;
        readonly bool _threads;
        volatile bool _stop;
        long _nowBits, _cameraXBits, _cameraZBits, _velocityXBits, _velocityZBits;
        long _washLast = -1;
        double _lastT = double.NaN;
        Vector3 _lastCamera, _velocity;

        /// <param name="threads">False: no helper threads; each Draw computes the snapshots its time needs and ticks the wash itself
        /// first (the EditMode suite's port check, frame by frame).</param>
        public PlantField(PlantMechanics[] mechanics, WindField wind, IWorldQuery world, PoseBuffer poses, PlantWash fine, PlantWash coarse,
            ComputeShader shader, bool threads = true)
        {
            _threads = threads;
            _m = mechanics;
            _wind = wind;
            _frame = new PlantAir(wind);
            _meanAir = new PlantAir(wind);
            _world = world;
            _poses = poses;
            _fine = fine;
            _coarse = coarse;
            _cs = shader;
            _kernel = shader.FindKernel("Bend");

            _grids = new Grid[Grids.Length];
            for (int g = 0; g < Grids.Length; g++)
                _grids[g] = new Grid(Grids[g].Size, Grids[g].Spacing, Grids[g].Step, Grids[g].Bands, Grids[g].WakeCells && wind.Wakes != null);
            if (fine != null) _washFine = new Wash(fine, LevelSize[0], LevelCell[0], mechanics);
            if (coarse != null) _washCoarse = new Wash(coarse, LevelSize[1], LevelCell[1], mechanics);
            if (fine != null || coarse != null) _steady = new Steady(fine ?? coarse, mechanics);

            // The modes' tables: each band's open modes at its load point's height, the strips' eddies, the wake eddies.
            _open = new PlantMode[_frame.OpenModeCount];
            _wakes = new PlantMode[_frame.WakeModeCount];
            _frame.WakeModes(_wakes);
            _strips = new PlantMode[_frame.StripCount][];
            _stripConvection = new double[_strips.Length];
            for (int s = 0; s < _strips.Length; s++)
            {
                _strips[s] = new PlantMode[_frame.StripModeCount(s)];
                _stripConvection[s] = _frame.StripModes(s, _strips[s]);
                _stripModes = Math.Max(_stripModes, _strips[s].Length);
            }
            var amp = new Vector4[Math.Max(1, PlantBands.Count * _open.Length)];
            for (int b = 0; b < PlantBands.Count; b++)
            {
                double lambda = Lambda(wind, mechanics[b], PlantBands.Elevation(b));
                _frame.OpenModes(lambda, _open);
                for (int i = 0; i < _open.Length; i++) amp[b * _open.Length + i] = V4(_open[i].Amplitude, 0);
            }
            _openWaveData = new Vector4[Math.Max(1, _open.Length)];
            _openRateData = new float[Math.Max(1, _open.Length)];
            _stripWaveData = new Vector4[Math.Max(1, _strips.Length * _stripModes)];
            _stripAmpData = new Vector4[_stripWaveData.Length];
            _wakeWaveData = new Vector4[Math.Max(1, _wakes.Length)];
            _wakeAmpData = new Vector4[_wakeWaveData.Length];
            _wakeFrameData = new Vector2[_wakeWaveData.Length];
            for (int i = 0; i < _wakes.Length; i++)
            {
                _wakeWaveData[i] = V4(_wakes[i].Wave, _wakes[i].Scale);
                _wakeAmpData[i] = V4(_wakes[i].Amplitude, _wakes[i].Phase);
            }

            _noWash = new ComputeBuffer(1, 16);
            _bandBuf = new ComputeBuffer(PlantBands.Count, 13 * sizeof(float));
            var bands = new float[PlantBands.Count * 13];
            for (int b = 0; b < PlantBands.Count; b++)
            {
                PlantMechanics m = mechanics[b];
                // The wake eddies' share over √(T² − 1) at the band's load height, outside a strip: σ_u times the profile's taper
                // there (WindField.PlantOpen's).
                double h = PlantBands.Elevation(b) + m.LoadPoint, wake = wind.HasProfile && h - wind.Preset.Displacement > wind.Preset.Roughness
                    ? wind.SigmaH * Math.Min(1, (h - wind.Preset.Displacement) / wind.Preset.TaperHeight) : 0;
                double[] row = { m.Stiffness, m.Inertia, m.Weight, m.Damping, m.StemDrag, m.FoliageDrag, m.Reconfigure, m.Vogel,
                    m.DragFrom, m.DragTo, m.LoadPoint, PlantBands.Elevation(b), Math.Max(0, wake) };
                for (int k = 0; k < 13; k++) bands[b * 13 + k] = (float)row[k];
            }
            _bandBuf.SetData(bands);
            _openWave = new ComputeBuffer(_openWaveData.Length, 16);
            _openRate = new ComputeBuffer(_openRateData.Length, 4);
            _openAmp = new ComputeBuffer(amp.Length, 16);
            _openAmp.SetData(amp);
            _stripWave = new ComputeBuffer(_stripWaveData.Length, 16);
            _stripAmp = new ComputeBuffer(_stripAmpData.Length, 16);
            _wakeWave = new ComputeBuffer(_wakeWaveData.Length, 16);
            _wakeWave.SetData(_wakeWaveData);
            _wakeAmp = new ComputeBuffer(_wakeAmpData.Length, 16);
            _wakeAmp.SetData(_wakeAmpData);
            _wakeFrame = new ComputeBuffer(_wakeFrameData.Length, 8);
            for (int d = 0; d < Dispatches.Length; d++)
            {
                // Each dispatched band and its slice on its level.
                (int level, _, int[] list) = Dispatches[d];
                var pairs = new int[list.Length * 2];
                for (int i = 0; i < list.Length; i++)
                {
                    pairs[2 * i] = list[i];
                    pairs[2 * i + 1] = Array.IndexOf(LevelBands[level], list[i]);
                }
                _dispatchBands[d] = new ComputeBuffer(list.Length, 8);
                _dispatchBands[d].SetData(pairs);
            }
            for (int l = 0; l < Levels; l++)
            {
                _out[l] = new RenderTexture(LevelSize[l], LevelSize[l], 0, RenderTextureFormat.ARGBFloat)
                {
                    dimension = TextureDimension.Tex2DArray, volumeDepth = LevelBands[l].Length, enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Plant level " + l,
                };
                _out[l].Create();
            }

            _meanThread = new Thread(MeanLoop) { IsBackground = true, Name = "plants: the wind's mean", Priority = System.Threading.ThreadPriority.BelowNormal };
            _washThread = new Thread(WashLoop) { IsBackground = true, Name = "plants: the wash", Priority = System.Threading.ThreadPriority.BelowNormal };
            if (!threads) return;
            _meanThread.Start();
            if (_washFine != null || _washCoarse != null) _washThread.Start();
        }

        /// <summary>The level's bend texture (a texture array, a slice per band in LevelBands' order) and its box (texel 0's
        /// corner x, z, the cell, the size; size 0 while it holds nothing).</summary>
        public RenderTexture Level(int level) => _out[level];
        public Vector4 Box(int level) => _boxes[level];

        /// <summary>The last frame's wind means: a grid's two snapshots' boxes (node 0's centre x, z, the spacing, the size), their
        /// flight times and the blend between them; a wash level's fraction between its ticks and its window's first node.</summary>
        public Vector4 MeanBox(int grid, int snapshot) => _grids[grid].Boxes[snapshot];
        public double MeanTime(int grid, int snapshot) => _grids[grid].Times[snapshot];
        public float MeanBlend(int grid) => _blends[grid];
        public double WashFraction(int level) => _fractions[level];

        /// <summary>The line's air a wash level's node holds for a grass band, as the last frame uploaded it (world m/s).</summary>
        public Vector3 WashNodeAir(int level, int a, int b, int band)
        {
            Wash w = level == 0 ? _washFine : _washCoarse;
            int at = ((a + w.Size * b) * WashStride + 6 + band) * 4;
            float[] d = w.Shown.Data;
            return new Vector3(d[at], d[at + 1], d[at + 2]);
        }

        /// <summary>A wash level's window: its first node's world cell (x, z); its node (a, b) is at ((i + a + 0.5)·cell, (j + b + 0.5)·cell).</summary>
        public (long I, long J) WashStart(int level) => _starts[level];

        /// <summary>Whether a grid reads the wake eddies' scale from the wake grid's cells, and that wake grid.</summary>
        public bool WakeCells(int grid) => _grids[grid].Cells;
        public WakeGrid WakeGridOf(int grid, int snapshot) => _grids[grid].WakeGrids[snapshot];

        readonly (long, long)[] _starts = new (long, long)[2];
        SteadySnapshot _steadyShown;

        /// <summary>The wash's steady air the last frame drew with, at a band's load point over (x, z), as the kernel interpolates
        /// it (bilinear in the log of the radius and the angle); false where it holds none.</summary>
        public bool SteadyAt(int band, double x, double z, out Double3 air) => Steady.At(_steadyShown, band, x, z, out air);
        readonly float[] _blends = new float[4];
        readonly double[] _fractions = new double[2];

        /// <summary>The helpers' CPU: the wind's mean, ms a second of flight (each grid's last snapshot over its step); a wash tick, ms.</summary>
        public double MeanMsPerSecond
        {
            get
            {
                double ms = 0;
                foreach (Grid g in _grids) ms += g.LastMs / g.Step;
                return ms;
            }
        }

        public double WashTickMs => (_washFine?.LastMs ?? 0) + (_washCoarse?.LastMs ?? 0);

        /// <summary>
        /// The frame (render thread): the bends at flight time t (s), the near levels round the camera (world m), written to the
        /// levels and bound for the shaders (PlantBend.hlsl's globals). Returns false while the wind's first snapshot is not ready
        /// (the plants stand as drawn).
        /// </summary>
        public bool Draw(double t, Vector3 camera)
        {
            // The camera's velocity over flight time, smoothed, so the helper centres a snapshot where the camera will be.
            if (!double.IsNaN(_lastT) && t > _lastT)
                _velocity = Vector3.Lerp(_velocity, (camera - _lastCamera) / (float)(t - _lastT), 0.1f);
            _lastT = t;
            _lastCamera = camera;
            Interlocked.Exchange(ref _nowBits, BitConverter.DoubleToInt64Bits(t));
            Interlocked.Exchange(ref _cameraXBits, BitConverter.DoubleToInt64Bits(camera.x));
            Interlocked.Exchange(ref _cameraZBits, BitConverter.DoubleToInt64Bits(camera.z));
            Interlocked.Exchange(ref _velocityXBits, BitConverter.DoubleToInt64Bits(_velocity.x));
            Interlocked.Exchange(ref _velocityZBits, BitConverter.DoubleToInt64Bits(_velocity.z));
            if (!_threads)
            {
                while (MeanStep()) { }
                WashStep(ref _washLast);
            }

            for (int g = 0; g < _grids.Length; g++)
            {
                if (!_grids[g].Pick(t, out Snapshot s0, out Snapshot s1, out _blends[g]))
                {
                    Unbind();
                    return false;
                }
                _grids[g].Upload(s0, s1);
            }

            // The frame's modes: each phase relative to an origin by the camera (κ·(p − O) + Φ, Φ reduced in double).
            _frame.Begin(t);
            Double3 v = _frame.FrameMean, d = _frame.FrameDrift;
            var o = new Double3(Math.Round(camera.x), 0, Math.Round(camera.z));
            for (int i = 0; i < _open.Length; i++)
            {
                Double3 k = _open[i].Wave;
                _openWaveData[i] = V4(k, Reduce(_open[i].Phase - (k.X * d.X + k.Z * d.Z) + Double3.Dot(k, o)));
                _openRateData[i] = (float)(k.X * v.X + k.Z * v.Z);
            }
            for (int s = 0; s < _strips.Length; s++)
            {
                double c = _stripConvection[s];
                for (int i = 0; i < _strips[s].Length; i++)
                {
                    PlantMode mode = _strips[s][i];
                    Double3 k = mode.Wave;
                    _stripWaveData[s * _stripModes + i] = V4(k, Reduce(mode.Phase - c * (k.X * d.X + k.Z * d.Z) + Double3.Dot(k, o)));
                    _stripAmpData[s * _stripModes + i] = V4(mode.Amplitude, c * (k.X * v.X + k.Z * v.Z));
                }
            }
            for (int i = 0; i < _wakes.Length; i++)
            {
                Double3 k = _wakes[i].Wave;
                _wakeFrameData[i] = new Vector2((float)(Double3.Dot(k, o) - (k.X * d.X + k.Z * d.Z)), (float)(k.X * v.X + k.Z * v.Z));
            }
            _openWave.SetData(_openWaveData);
            _openRate.SetData(_openRateData);
            _stripWave.SetData(_stripWaveData);
            _stripAmp.SetData(_stripAmpData);
            _wakeFrame.SetData(_wakeFrameData);

            _cs.SetVector("_Origin", new Vector4((float)o.X, 0, (float)o.Z, 0));
            _cs.SetInt("_OpenCount", _open.Length);
            _cs.SetInt("_StripModes", _stripModes);
            _cs.SetInt("_WakeCount", _wakes.Length);
            _cs.SetInt("_WashStride", WashStride);
            Bind("_Bands", _bandBuf);
            Bind("_OpenWave", _openWave);
            Bind("_OpenRate", _openRate);
            Bind("_OpenAmp", _openAmp);
            Bind("_StripWave", _stripWave);
            Bind("_StripAmp", _stripAmp);
            Bind("_WakeWave", _wakeWave);
            Bind("_WakeAmp", _wakeAmp);
            Bind("_WakeFrame", _wakeFrame);

            // The levels' boxes: the wash's from its newest tick (none before the first), the others round the camera with their
            // texel centres on whole cells, so the mean grids' nodes (their spacing's half-cells) are texels.
            var washes = new WashSnapshot[2];
            for (int l = 0; l < 2; l++)
            {
                Wash w = l == 0 ? _washFine : _washCoarse;
                washes[l] = w?.Take();
                if (washes[l] == null)
                {
                    _boxes[l] = Vector4.zero;
                    continue;
                }
                double fraction = (t - (washes[l].Tick * FlightCore.Dt - washes[l].TickSeconds)) / washes[l].TickSeconds;
                _fractions[l] = fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
                _starts[l] = (washes[l].StartI, washes[l].StartJ);
                _boxes[l] = new Vector4((float)(washes[l].StartI * LevelCell[l]), (float)(washes[l].StartJ * LevelCell[l]), (float)LevelCell[l], LevelSize[l]);
            }
            for (int l = 2; l < Levels; l++)
            {
                double cell = LevelCell[l], half = LevelSize[l] / 2 + 0.5;
                _boxes[l] = new Vector4((float)((Math.Floor(camera.x / cell) - half) * cell), (float)((Math.Floor(camera.z / cell) - half) * cell),
                    (float)cell, LevelSize[l]);
            }
            // The wash's steady air beyond its grids, the newest tick's.
            SteadySnapshot steady = _steady?.Take();
            _steadyShown = steady;
            _cs.SetInt("_SteadyOn", steady != null ? 1 : 0);
            _cs.SetInt("_SteadyBands", SteadyBands);
            _cs.SetVector("_SteadyGrid", new Vector4((float)SteadyR0, (float)SteadyGrowth, SteadyRings, SteadyAngles));
            _cs.SetVector("_SteadyCentre", steady != null ? new Vector4((float)steady.X, (float)steady.Z, 0, 0) : Vector4.zero);
            Bind("_Steady", steady != null ? _steady.Buffer : _noWash);
            for (int i = 0; i < Dispatches.Length; i++)
            {
                (int level, int grid, int[] list) = Dispatches[i];
                if (_boxes[level].w <= 0) continue;
                bool wash = level < 2;
                Bind("_WashNodes", wash ? (level == 0 ? _washFine : _washCoarse).Buffer : _noWash);
                _cs.SetFloat("_WashFraction", wash ? (float)_fractions[level] : 0);
                Dispatch(level, _grids[grid], _blends[grid], wash, _dispatchBands[i], list.Length);
            }
            for (int l = 0; l < Levels; l++) Shader.SetGlobalTexture(LevelIds[l], _out[l]);
            Shader.SetGlobalVectorArray(BoxId, _boxes);
            return true;
        }

        static readonly int BoxId = Shader.PropertyToID("_PlantLevelBox");
        static readonly int[] LevelIds = { Shader.PropertyToID("_PlantLevel0"), Shader.PropertyToID("_PlantLevel1"),
            Shader.PropertyToID("_PlantLevel2"), Shader.PropertyToID("_PlantLevel3"), Shader.PropertyToID("_PlantLevel4") };

        static Texture2DArray _none;

        /// <summary>No level for the shaders (each bound to an empty array, its box 0): the plants stand as drawn.</summary>
        public static void Unbind()
        {
            if (_none == null)
            {
                _none = new Texture2DArray(1, 1, 1, TextureFormat.RGBAFloat, false) { name = "No plant level", hideFlags = HideFlags.DontSave };
                _none.SetPixels(new[] { Color.clear }, 0);
                _none.Apply(false, true);
            }
            for (int l = 0; l < Levels; l++) Shader.SetGlobalTexture(LevelIds[l], _none);
            Shader.SetGlobalVectorArray(BoxId, new Vector4[Levels]);
        }

        void Dispatch(int level, Grid grid, float blend, bool wash, ComputeBuffer bands, int count)
        {
            _cs.SetInt("_Wash", wash ? 1 : 0);
            _cs.SetVector("_LevelOrigin", new Vector4(_boxes[level].x, _boxes[level].y, 0, 0));
            _cs.SetFloat("_LevelCell", (float)LevelCell[level]);
            _cs.SetInt("_LevelSize", LevelSize[level]);
            Bind("_DispatchBands", bands);
            Bind("_Means", grid.Buffer);
            Bind("_MeanSlot", grid.SlotBuffer);
            _cs.SetInt("_MeanSlots", grid.Bands.Length);
            _cs.SetVectorArray("_MeanGrid", grid.Boxes);
            _cs.SetFloat("_MeanBlend", blend);
            Bind("_WakeCells", grid.CellBuffer);
            _cs.SetInt("_WakeCellsOn", grid.Cells ? 1 : 0);
            _cs.SetInt("_WakeN", grid.CellCount);
            _cs.SetVectorArray("_WakeGrid", grid.CellBoxes);
            _cs.SetTexture(_kernel, "_Out", _out[level]);
            int groups = (LevelSize[level] + ThreadGroup - 1) / ThreadGroup;
            _cs.Dispatch(_kernel, groups, groups, count);
        }

        void Bind(string name, ComputeBuffer buffer) => _cs.SetBuffer(_kernel, name, buffer);

        // ---- the wind's mean, on the helper ----

        // One grid's snapshot: its flight time, node 0's centre, and per slot (band) and node PlantField.compute's five float4s:
        // (air, load y), (lean·way, way), (xx, xz, zz, yy), (xy, zy, open, strip share), (wake, 1/H, strip, valid); and, on a grid
        // that reads them, the wake grid's cells' obstacle heights at each slot's load height over a window of cells.
        sealed class Snapshot
        {
            public long K = -1;
            public double X0, Z0;
            public float[] Data, Cells;
            public long CellI0, CellJ0;
            public WakeGrid Wake;
            public volatile bool Ready;
        }

        sealed class Grid
        {
            public readonly int Size, CellCount;
            public readonly double Spacing, Step;
            public readonly int[] Bands;
            public readonly bool Cells;
            public readonly Snapshot[] Ring = new Snapshot[4];
            public readonly ComputeBuffer Buffer, SlotBuffer, CellBuffer;
            public readonly Vector4[] Boxes = new Vector4[2], CellBoxes = new Vector4[2];
            public readonly double[] Times = new double[2];
            public readonly WakeGrid[] WakeGrids = new WakeGrid[2];
            public long NextK;
            public double LastMs;
            readonly long[] _uploaded = { -1, -1 };

            public Grid(int size, double spacing, double step, int[] bands, bool cells)
            {
                Size = size;
                Spacing = spacing;
                Step = step;
                Bands = bands;
                Cells = cells;
                // The cells under the grid's nodes and a node beyond each side (WakeObstacles.Cell: 2 m).
                CellCount = cells ? (int)Math.Ceiling((size + 2) * spacing / WakeObstacles.Cell) + 2 : 1;
                for (int i = 0; i < Ring.Length; i++)
                    Ring[i] = new Snapshot { Data = new float[bands.Length * size * size * 20], Cells = new float[cells ? 2 * bands.Length * CellCount * CellCount : 2] };
                Buffer = new ComputeBuffer(2 * bands.Length * size * size * 5, 16);
                CellBuffer = new ComputeBuffer(Ring[0].Cells.Length, 8);
                SlotBuffer = new ComputeBuffer(PlantBands.Count, 4);
                var slots = new int[PlantBands.Count];
                for (int b = 0; b < slots.Length; b++) slots[b] = Array.IndexOf(bands, b);
                SlotBuffer.SetData(slots);
            }

            // The snapshots about t: the newest ready at or before t and the one a step after it (blend 0 to 1 between); the
            // newest alone, held, once t passes it. False before the first.
            public bool Pick(double t, out Snapshot a, out Snapshot b, out float blend)
            {
                a = b = null;
                blend = 0;
                foreach (Snapshot s in Ring)
                    if (s.Ready && s.K * Step <= t + 1e-9 && (a == null || s.K > a.K)) a = s;
                if (a == null)
                    foreach (Snapshot s in Ring)
                        if (s.Ready && (a == null || s.K < a.K)) a = s; // a frame before the first snapshot's time holds it
                if (a == null) return false;
                foreach (Snapshot s in Ring)
                    if (s.Ready && s.K == a.K + 1) b = s;
                if (b == null) b = a;
                else blend = (float)Math.Min(1, Math.Max(0, (t - a.K * Step) / Step));
                return true;
            }

            public void Upload(Snapshot a, Snapshot b)
            {
                int n = Bands.Length * Size * Size * 5, c = a.Cells.Length;
                if (_uploaded[0] != a.K)
                {
                    Buffer.SetData(a.Data, 0, 0, n * 4);
                    CellBuffer.SetData(a.Cells, 0, 0, c);
                    _uploaded[0] = a.K;
                }
                if (_uploaded[1] != b.K)
                {
                    Buffer.SetData(b.Data, 0, n * 4, n * 4);
                    CellBuffer.SetData(b.Cells, 0, c, c);
                    _uploaded[1] = b.K;
                }
                int k = 0;
                foreach (Snapshot s in new[] { a, b })
                {
                    Boxes[k] = new Vector4((float)s.X0, (float)s.Z0, (float)Spacing, Size);
                    Times[k] = s.K * Step;
                    WakeGrids[k] = s.Wake;
                    CellBoxes[k] = s.Wake != null
                        ? new Vector4((float)s.Wake.Obstacles.OriginX, (float)s.Wake.Obstacles.OriginZ, s.CellI0, s.CellJ0)
                        : new Vector4(0, 0, 0, -1);
                    k++;
                }
            }
        }

        readonly XZ[] _meanXz = new XZ[64];
        readonly GroundSample[] _meanGround = new GroundSample[64];

        // One snapshot that is due (the next of a grid from the frame's step to Lead ahead); false when none is.
        bool MeanStep()
        {
            double now = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _nowBits));
            foreach (Grid g in _grids)
            {
                long target = (long)Math.Floor((now + g.Step + Lead) / g.Step); // the next step's ready before the frame reaches it
                long k = Math.Max(g.NextK, (long)Math.Floor(now / g.Step));
                if (k > target) continue;
                Compute(g, k, now);
                g.NextK = k + 1;
                return true;
            }
            return false;
        }

        void MeanLoop()
        {
            try
            {
                while (!_stop)
                    if (!MeanStep()) Thread.Sleep(20);
            }
            catch (Exception e)
            {
                Debug.LogError("[Plants] the wind's mean stopped: " + e);
            }
        }

        // A snapshot of a grid at step k's time, centred where the camera will be then (its velocity held, at most 3 s ahead).
        void Compute(Grid g, long k, double now)
        {
            var clock = Stopwatch.StartNew();
            Snapshot s = g.Ring[k % g.Ring.Length];
            s.Ready = false;
            double ahead = Math.Min(3, Math.Max(0, k * g.Step - now));
            double cx = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _cameraXBits)) + ahead * BitConverter.Int64BitsToDouble(Interlocked.Read(ref _velocityXBits));
            double cz = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _cameraZBits)) + ahead * BitConverter.Int64BitsToDouble(Interlocked.Read(ref _velocityZBits));
            long i0 = (long)Math.Floor(cx / g.Spacing) - g.Size / 2, j0 = (long)Math.Floor(cz / g.Spacing) - g.Size / 2;
            s.X0 = (i0 + 0.5) * g.Spacing;
            s.Z0 = (j0 + 0.5) * g.Spacing;
            _meanAir.Begin(k * g.Step);
            int nodes = g.Size * g.Size;
            XZ[] xz = _meanXz;
            GroundSample[] ground = _meanGround;
            for (int j = 0; j < g.Size; j++)
            {
                for (int i = 0; i < g.Size; i++) xz[i] = new XZ(s.X0 + i * g.Spacing, s.Z0 + j * g.Spacing);
                _world.SampleGround(new ReadOnlySpan<XZ>(xz, 0, g.Size), new Span<GroundSample>(ground, 0, g.Size));
                for (int i = 0; i < g.Size; i++)
                {
                    int node = i + g.Size * j;
                    double h = ground[i].Height;
                    for (int slot = 0; slot < g.Bands.Length; slot++)
                    {
                        int at = (slot * nodes + node) * 20;
                        if (!(h > double.NegativeInfinity))
                        {
                            Array.Clear(s.Data, at, 20);
                            continue;
                        }
                        int band = g.Bands[slot];
                        _meanAir.Mean(_m[band], new Double3(xz[i].X, h + PlantBands.Elevation(band), xz[i].Z), h, out PlantMean mean);
                        Pack(s.Data, at, mean, _meanAir.StripOf(mean));
                    }
                }
            }
            // The wake grid's cells under the grid at each slot's load height (WakeGrid.SampleCell: the obstacle height the wake
            // eddies take their scale from, the cell's or its overlying wake's, and the turbulence), as WakeGrid.Sample reads them.
            s.Wake = null;
            if (g.Cells)
            {
                WakeGrid wake = _wind.Wakes.Active;
                WakeObstacles o = wake.Obstacles;
                s.CellI0 = (long)Math.Floor((s.X0 - g.Spacing - o.OriginX) / WakeObstacles.Cell) - 1;
                s.CellJ0 = (long)Math.Floor((s.Z0 - g.Spacing - o.OriginZ) / WakeObstacles.Cell) - 1;
                int n = g.CellCount;
                for (int slot = 0; slot < g.Bands.Length; slot++)
                {
                    int band = g.Bands[slot];
                    double height = PlantBands.Elevation(band) + _m[band].LoadPoint;
                    for (int cj = 0; cj < n; cj++)
                        for (int ci = 0; ci < n; ci++)
                        {
                            wake.SampleCell((int)(s.CellI0 + ci), (int)(s.CellJ0 + cj), height, out WakeSample cell);
                            int at = ((slot * n + cj) * n + ci) * 2;
                            s.Cells[at] = (float)cell.ObstacleHeight;
                            s.Cells[at + 1] = (float)cell.Turbulence;
                        }
                }
                s.Wake = wake;
            }
            s.K = k;
            s.Ready = true;
            g.LastMs = clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>A mean as PlantField.compute reads it (five float4s).</summary>
        public static void Pack(float[] data, int at, in PlantMean m, int strip)
        {
            data[at] = (float)m.Air.X;
            data[at + 1] = (float)m.Air.Y;
            data[at + 2] = (float)m.Air.Z;
            data[at + 3] = (float)m.LoadY;
            data[at + 4] = (float)(m.Lean * m.WayX);
            data[at + 5] = (float)(m.Lean * m.WayZ);
            data[at + 6] = (float)m.WayX;
            data[at + 7] = (float)m.WayZ;
            data[at + 8] = (float)m.XX;
            data[at + 9] = (float)m.XZ;
            data[at + 10] = (float)m.ZZ;
            data[at + 11] = (float)m.YY;
            data[at + 12] = (float)m.XY;
            data[at + 13] = (float)m.ZY;
            data[at + 14] = (float)m.Open;
            data[at + 15] = (float)m.StripShare;
            data[at + 16] = (float)m.Wake;
            data[at + 17] = (float)m.WakeInverse;
            data[at + 18] = strip;
            data[at + 19] = 1;
        }

        // ---- the wash, on its helper ----

        // One wash grid's tick as the GPU reads it, WashStride float4s a node (window order): per grid band (PlantBands.Washed),
        // (was x, z, now x, z) and (the wash's air at the band's load point, the node's ground), the ground −1e30 where PlantWash.Bend
        // holds no bend at the node; then per grass band the line's air at its load point (RotorAir.At, the steady air PlantAir.Sample
        // gives a band the grids do not integrate).
        public const int WashStride = 2 * 3 + PlantBands.Grass;

        sealed class WashSnapshot
        {
            public long Tick;
            public double TickSeconds;
            public long StartI, StartJ;
            public float[] Data;
        }

        sealed class Wash
        {
            public readonly PlantWash Grid;
            public readonly int Size;
            public readonly double Cell;
            public readonly ComputeBuffer Buffer;
            public double LastMs;
            WashSnapshot _back, _ready, _front;
            bool _fresh, _shown;
            public WashSnapshot Shown => _front;
            readonly object _lock = new object();
            readonly XZ[] _xz;
            readonly GroundSample[] _ground;
            readonly double[] _heights;

            public Wash(PlantWash grid, int size, double cell, PlantMechanics[] bands)
            {
                Grid = grid;
                Size = size;
                Cell = cell;
                _heights = new double[PlantBands.Grass];
                for (int b = 0; b < _heights.Length; b++) _heights[b] = PlantBands.Elevation(b) + bands[b].LoadPoint;
                Buffer = new ComputeBuffer(size * size * WashStride, 16);
                int length = size * size * WashStride * 4;
                _back = new WashSnapshot { Data = new float[length] };
                _ready = new WashSnapshot { Data = new float[length] };
                _front = new WashSnapshot { Data = new float[length] };
                _xz = new XZ[size];
                _ground = new GroundSample[size];
            }

            // The helper: the tick's stands and the line's air at the nodes to the back snapshot, then swapped in. The air is the
            // instance PlantAir.Sample reads (the fine grid's).
            public void Export(PoseBuffer poses, IWorldQuery world, RotorAir line)
            {
                if (!poses.TryRead(Grid.LastTick, out PoseFrame pose)) return;
                var clock = Stopwatch.StartNew();
                WashSnapshot s = _back;
                s.Tick = Grid.LastTick;
                s.TickSeconds = Grid.Tick;
                s.StartI = Floor(pose.Position.X / Cell) - Size / 2;
                s.StartJ = Floor(pose.Position.Z / Cell) - Size / 2;
                for (int b = 0; b < Size; b++)
                {
                    for (int a = 0; a < Size; a++) _xz[a] = new XZ((s.StartI + a + 0.5) * Cell, (s.StartJ + b + 0.5) * Cell);
                    world.SampleGround(_xz, _ground);
                    for (int a = 0; a < Size; a++)
                    {
                        int node = (a + Size * b) * WashStride * 4;
                        bool held = true;
                        for (int band = 0; band < PlantBands.Washed.Length; band++) // the grid's own bands (PlantBands.Washed)
                        {
                            int at = node + band * 8;
                            // The window's last row and column hold no bend (PlantWash.Bend needs the node beyond; at a node's centre
                            // its answer there would turn on the coordinates' last bit).
                            if (a == Size - 1 || b == Size - 1 || !Grid.Bend(band, _xz[a].X, _xz[a].Z, 0, out PlantBend was, out Double3 air)
                                || !Grid.Bend(band, _xz[a].X, _xz[a].Z, 1, out PlantBend now, out _) || !(_ground[a].Height > double.NegativeInfinity))
                            {
                                Array.Clear(s.Data, at, 8);
                                s.Data[at + 7] = -1e30f;
                                held = false;
                                continue;
                            }
                            s.Data[at] = (float)was.X;
                            s.Data[at + 1] = (float)was.Z;
                            s.Data[at + 2] = (float)now.X;
                            s.Data[at + 3] = (float)now.Z;
                            s.Data[at + 4] = (float)air.X;
                            s.Data[at + 5] = (float)air.Y;
                            s.Data[at + 6] = (float)air.Z;
                            s.Data[at + 7] = (float)_ground[a].Height;
                        }
                        for (int band = 0; band < PlantBands.Grass; band++)
                        {
                            int at = node + (6 + band) * 4;
                            double g = _ground[a].Height;
                            Double3 u = held && PlantBands.WashIndex(band) < 0 ? line.At(new Double3(_xz[a].X, g + _heights[band], _xz[a].Z), g, out _) : default;
                            s.Data[at] = (float)u.X;
                            s.Data[at + 1] = (float)u.Y;
                            s.Data[at + 2] = (float)u.Z;
                            s.Data[at + 3] = 0;
                        }
                    }
                }
                lock (_lock)
                {
                    (_back, _ready) = (_ready, _back);
                    _fresh = true;
                }
                LastMs = clock.Elapsed.TotalMilliseconds;
            }

            // The render thread: the newest tick (a triple buffer: the helper never waits), uploaded once; null before the first.
            public WashSnapshot Take()
            {
                bool changed = false;
                lock (_lock)
                {
                    if (_fresh)
                    {
                        (_ready, _front) = (_front, _ready);
                        _fresh = false;
                        changed = true;
                    }
                }
                if (changed)
                {
                    Buffer.SetData(_front.Data);
                    _shown = true;
                }
                return _shown ? _front : null;
            }

            static long Floor(double v)
            {
                long t = (long)v;
                return t > v ? t - 1 : t;
            }
        }

        // The wash's steady air on its polar grid at a tick: the drone's place then, per band, ring and angle the air (world m/s).
        sealed class SteadySnapshot
        {
            public double X, Z;
            public float[] Data;
        }

        sealed class Steady
        {
            readonly PlantWash _grid;
            readonly double[] _heights = new double[SteadyBands];
            readonly XZ[] _xz = new XZ[SteadyRings * SteadyAngles];
            readonly GroundSample[] _ground = new GroundSample[SteadyRings * SteadyAngles];
            public readonly ComputeBuffer Buffer;
            SteadySnapshot _back, _ready, _front;
            bool _fresh, _shown;
            readonly object _lock = new object();

            public Steady(PlantWash grid, PlantMechanics[] bands)
            {
                _grid = grid;
                for (int b = 0; b < SteadyBands; b++) _heights[b] = PlantBands.Elevation(b) + bands[b].LoadPoint;
                int n = SteadyBands * SteadyRings * SteadyAngles;
                Buffer = new ComputeBuffer(n, 16);
                _back = new SteadySnapshot { Data = new float[n * 4] };
                _ready = new SteadySnapshot { Data = new float[n * 4] };
                _front = new SteadySnapshot { Data = new float[n * 4] };
            }

            public static double Radius(int ring) => SteadyR0 * Math.Pow(SteadyGrowth, ring);

            // The helper, after a tick: RotorAir.At (the grid's own instance, begun at the tick) at each point's load heights.
            public void Export(PoseBuffer poses, IWorldQuery world)
            {
                if (!poses.TryRead(_grid.LastTick, out PoseFrame pose)) return;
                SteadySnapshot s = _back;
                s.X = pose.Position.X;
                s.Z = pose.Position.Z;
                for (int r = 0; r < SteadyRings; r++)
                    for (int a = 0; a < SteadyAngles; a++)
                    {
                        double angle = 2 * Math.PI * a / SteadyAngles, radius = Radius(r);
                        _xz[r * SteadyAngles + a] = new XZ(s.X + radius * Math.Cos(angle), s.Z + radius * Math.Sin(angle));
                    }
                world.SampleGround(_xz, _ground);
                RotorAir air = _grid.Rotors;
                for (int b = 0; b < SteadyBands; b++)
                    for (int k = 0; k < _xz.Length; k++)
                    {
                        int at = (b * _xz.Length + k) * 4;
                        double g = _ground[k].Height;
                        Double3 u = g > double.NegativeInfinity ? air.At(new Double3(_xz[k].X, g + _heights[b], _xz[k].Z), g, out _) : default;
                        s.Data[at] = (float)u.X;
                        s.Data[at + 1] = (float)u.Y;
                        s.Data[at + 2] = (float)u.Z;
                        s.Data[at + 3] = 0;
                    }
                lock (_lock)
                {
                    (_back, _ready) = (_ready, _back);
                    _fresh = true;
                }
            }

            public SteadySnapshot Take()
            {
                bool changed = false;
                lock (_lock)
                {
                    if (_fresh)
                    {
                        (_ready, _front) = (_front, _ready);
                        _fresh = false;
                        changed = true;
                    }
                }
                if (changed)
                {
                    Buffer.SetData(_front.Data);
                    _shown = true;
                }
                return _shown ? _front : null;
            }

            // The kernel's SteadyAt in double.
            public static bool At(SteadySnapshot s, int band, double x, double z, out Double3 air)
            {
                air = default;
                if (s == null || band >= SteadyBands) return false;
                double dx = x - s.X, dz = z - s.Z, r = Math.Sqrt(dx * dx + dz * dz);
                if (r > Radius(SteadyRings - 1)) return false;
                double ring = Math.Log(Math.Max(r, SteadyR0) / SteadyR0) / Math.Log(SteadyGrowth), angle = Math.Atan2(dz, dx) / (2 * Math.PI) * SteadyAngles;
                if (angle < 0) angle += SteadyAngles;
                int r0 = Math.Min((int)Math.Floor(ring), SteadyRings - 2), a0 = Math.Min((int)Math.Floor(angle), SteadyAngles - 1), a1 = a0 + 1 < SteadyAngles ? a0 + 1 : 0;
                double fr = ring - r0, fa = angle - Math.Floor(angle);
                for (int c = 0; c < 4; c++)
                {
                    int ri = r0 + (c >> 1), ai = (c & 1) == 0 ? a0 : a1;
                    double w = ((c >> 1) == 0 ? 1 - fr : fr) * ((c & 1) == 0 ? 1 - fa : fa);
                    int at = ((band * SteadyRings + ri) * SteadyAngles + ai) * 4;
                    air = air + new Double3(s.Data[at], s.Data[at + 1], s.Data[at + 2]) * w;
                }
                return true;
            }
        }

        // The wash grids ticked to the newest published step, and a new tick's stands exported.
        void WashStep(ref long last)
        {
            if (_fine == null && _coarse == null) return;
            _fine?.Advance(_poses);
            _coarse?.Advance(_poses);
            long tick = (_fine ?? _coarse).LastTick;
            if (tick == last || tick < 0) return;
            RotorAir line = (_fine ?? _coarse).Rotors;
            _washFine?.Export(_poses, _world, line);
            _washCoarse?.Export(_poses, _world, line);
            _steady?.Export(_poses, _world);
            last = tick;
        }

        void WashLoop()
        {
            long last = -1;
            try
            {
                while (!_stop)
                {
                    WashStep(ref last);
                    Thread.Sleep(4);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Plants] the wash stopped: " + e);
            }
        }

        public void Dispose()
        {
            _stop = true;
            if (_meanThread.IsAlive) _meanThread.Join();
            if (_washThread.IsAlive) _washThread.Join();
            Unbind();
            foreach (ComputeBuffer b in new[] { _noWash, _bandBuf, _openWave, _openRate, _openAmp, _stripWave, _stripAmp, _wakeWave, _wakeAmp, _wakeFrame,
                _washFine?.Buffer, _washCoarse?.Buffer, _steady?.Buffer })
                b?.Release();
            foreach (Grid g in _grids)
            {
                g.Buffer.Release();
                g.SlotBuffer.Release();
                g.CellBuffer.Release();
            }
            foreach (ComputeBuffer b in _dispatchBands) b?.Release();
            foreach (RenderTexture rt in _out)
                if (rt != null) rt.Release();
        }

        // ---- helpers ----

        // The profile's log at a band's load point (the open modes' scale there): PlantMean's, at a point over flat open ground.
        static double Lambda(WindField wind, in PlantMechanics m, double elevation)
        {
            var air = new PlantAir(wind);
            air.Begin(0);
            air.Mean(m, new Double3(0, elevation, 0), 0, out PlantMean mean);
            return mean.Lambda;
        }

        static double Reduce(double phase)
        {
            const double TwoPi = 6.283185307179586;
            phase %= TwoPi;
            return phase < 0 ? phase + TwoPi : phase;
        }

        static Vector4 V4(in Double3 v, double w) => new Vector4((float)v.X, (float)v.Y, (float)v.Z, (float)w);

        static int[] Range(int from, int to)
        {
            var r = new int[to - from];
            for (int i = 0; i < r.Length; i++) r[i] = from + i;
            return r;
        }
    }
}
