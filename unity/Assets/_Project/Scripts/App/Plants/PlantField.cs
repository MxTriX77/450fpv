using System;
using System.Diagnostics;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
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
    ///   fluctuation's covariance, the mean lean) on two coarse grids on a helper thread, for flight times a second apart and ahead
    ///   of the frame; the GPU interpolates them in space and time and sums the modes per texel (PlantField.compute), at the
    ///   frame's time, statelessly: the gusts reach each plant when the field brings them there.
    /// - **The wash** (PlantWash, 1.2's grids): a fine (32 × 5 cm) and a coarse (32 × 15 cm) grid round the drone for the grass
    ///   bands, ticked from the published poses on a second helper thread; each tick's stands go to the GPU, whose texels are the
    ///   nodes, and the gusts are linearised about the wash's bend in the wash's air (PlantAir.Sample's arithmetic).
    /// - **The levels**, finest first: the two wash grids, near (96 × 0.5 m), mid (96 × 2 m) and far (64 × 8 m) round the camera.
    /// Nothing here touches the flight: the helpers read the flight's wind field (its immutable layers and its wake grid, as
    /// 1.2 does) and the published poses; the flight's step never waits on them (PLT-04).
    /// </summary>
    public sealed class PlantField : IDisposable
    {
        public const int Levels = 5;

        /// <summary>The levels: texels a side and their cell, m (the wash grids': PlantWash's defaults, design.md §4.4).</summary>
        public static readonly int[] LevelSize = { 32, 32, 96, 96, 64 };
        public static readonly double[] LevelCell = { 0.05, 0.15, 0.5, 2, 8 };

        /// <summary>The bands each level holds, in its slices' order (PlantBend.hlsl's PlantBandLevels and slices).</summary>
        public static readonly int[][] LevelBands =
        {
            Range(0, PlantBands.Grass), Range(0, PlantBands.Grass), Range(0, PlantBands.Branch), Range(0, PlantBands.Branch + 1),
            Range(PlantBands.Branch, PlantBands.Count),
        };

        /// <summary>The wind's mean grids: A for the wash, near and mid levels (32 × 8 m), B for the far level (24 × 32 m); the
        /// snapshots a second of flight time apart, computed up to 1.5 s ahead of the frame (numerical: the mean air at a plant
        /// moves with the direction's meander, whose time constant is 120 s, and the mean is smooth over metres, design.md §7.1).</summary>
        const int GridASize = 32, GridBSize = 24;
        const double GridASpacing = 8, GridBSpacing = 32, SnapshotStep = 1, Lead = 1.5;

        public const int ThreadGroup = 8;

        readonly PlantMechanics[] _m;
        readonly PlantAir _frame, _meanAir;
        readonly PlantWash _fine, _coarse;
        readonly PoseBuffer _poses;
        readonly IWorldQuery _world;
        readonly Grid _gridA, _gridB;
        readonly Grid[] _grids;
        readonly Wash _washFine, _washCoarse;

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
        readonly ComputeBuffer[] _levelBands = new ComputeBuffer[Levels];
        readonly RenderTexture[] _out = new RenderTexture[Levels];
        readonly Vector4[] _boxes = new Vector4[Levels];

        // The helpers.
        readonly Thread _meanThread, _washThread;
        volatile bool _stop;
        long _nowBits, _cameraXBits, _cameraZBits;

        /// <param name="threads">False: no helper threads; each Draw computes the snapshots its time needs and ticks the wash itself
        /// first (the EditMode suite's port check, frame by frame).</param>
        public PlantField(PlantMechanics[] mechanics, WindField wind, IWorldQuery world, PoseBuffer poses, PlantWash fine, PlantWash coarse,
            ComputeShader shader, bool threads = true)
        {
            _threads = threads;
            _m = mechanics;
            _frame = new PlantAir(wind);
            _meanAir = new PlantAir(wind);
            _world = world;
            _poses = poses;
            _fine = fine;
            _coarse = coarse;
            _cs = shader;
            _kernel = shader.FindKernel("Bend");

            _gridA = new Grid(GridASize, GridASpacing, Range(0, PlantBands.Branch + 1));
            _gridB = new Grid(GridBSize, GridBSpacing, Range(PlantBands.Branch, PlantBands.Count));
            _grids = new[] { _gridA, _gridB };
            if (fine != null) _washFine = new Wash(fine, LevelSize[0], LevelCell[0]);
            if (coarse != null) _washCoarse = new Wash(coarse, LevelSize[1], LevelCell[1]);

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
            _bandBuf = new ComputeBuffer(PlantBands.Count, 12 * sizeof(float));
            var bands = new float[PlantBands.Count * 12];
            for (int b = 0; b < PlantBands.Count; b++)
            {
                PlantMechanics m = mechanics[b];
                double[] row = { m.Stiffness, m.Inertia, m.Weight, m.Damping, m.StemDrag, m.FoliageDrag, m.Reconfigure, m.Vogel,
                    m.DragFrom, m.DragTo, m.LoadPoint, PlantBands.Elevation(b) };
                for (int k = 0; k < 12; k++) bands[b * 12 + k] = (float)row[k];
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
            for (int l = 0; l < Levels; l++)
            {
                _levelBands[l] = new ComputeBuffer(LevelBands[l].Length, 4);
                _levelBands[l].SetData(LevelBands[l]);
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

        readonly bool _threads;
        long _washLast = -1;

        /// <summary>The level's bend texture (a texture array, a slice per band in LevelBands' order) and its box (texel 0's
        /// corner x, z, the cell, the size; size 0 while it holds nothing).</summary>
        public RenderTexture Level(int level) => _out[level];
        public Vector4 Box(int level) => _boxes[level];

        /// <summary>The last frame's wind means: a grid's (0 the near ones', 1 the far level's) two snapshots' boxes (node 0's centre x,
        /// z, the spacing, the size), their flight times and the blend between them; a wash level's fraction between its ticks.</summary>
        public Vector4 MeanBox(int grid, int snapshot) => _grids[grid].Boxes[snapshot];
        public double MeanTime(int grid, int snapshot) => _grids[grid].Times[snapshot];
        public float MeanBlend(int grid) => _blends[grid];
        public double WashFraction(int level) => _fractions[level];

        /// <summary>A wash level's window: its first node's world cell (x, z); its node (a, b) is at ((i + a + 0.5)·cell, (j + b + 0.5)·cell).</summary>
        public (long I, long J) WashStart(int level) => _starts[level];

        readonly (long, long)[] _starts = new (long, long)[2];

        readonly float[] _blends = new float[2];
        readonly double[] _fractions = new double[2];

        /// <summary>The CPU time the helpers spent on their last snapshot or tick, ms.</summary>
        public double MeanSnapshotMs => _gridA.LastMs + _gridB.LastMs;
        public double WashTickMs => (_washFine?.LastMs ?? 0) + (_washCoarse?.LastMs ?? 0);

        /// <summary>
        /// The frame (render thread): the bends at flight time t (s), the near levels round the camera (world m), written to the
        /// levels and bound for the shaders (PlantBend.hlsl's globals). Returns false while the wind's first snapshot is not ready
        /// (the plants stand as drawn).
        /// </summary>
        public bool Draw(double t, Vector3 camera)
        {
            Interlocked.Exchange(ref _nowBits, BitConverter.DoubleToInt64Bits(t));
            Interlocked.Exchange(ref _cameraXBits, BitConverter.DoubleToInt64Bits(camera.x));
            Interlocked.Exchange(ref _cameraZBits, BitConverter.DoubleToInt64Bits(camera.z));
            if (!_threads)
            {
                while (MeanStep()) { }
                WashStep(ref _washLast);
            }

            if (!_gridA.Pick(t, out Snapshot a0, out Snapshot a1, out float blendA) || !_gridB.Pick(t, out Snapshot b0, out Snapshot b1, out float blendB))
            {
                Unbind();
                return false;
            }
            _gridA.Upload(a0, a1);
            _gridB.Upload(b0, b1);
            _blends[0] = blendA;
            _blends[1] = blendB;

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
            _cs.SetInt("_WashBands", PlantBands.Grass);
            Bind("_Bands", _bandBuf);
            Bind("_OpenWave", _openWave);
            Bind("_OpenRate", _openRate);
            Bind("_OpenAmp", _openAmp);
            Bind("_StripWave", _stripWave);
            Bind("_StripAmp", _stripAmp);
            Bind("_WakeWave", _wakeWave);
            Bind("_WakeAmp", _wakeAmp);
            Bind("_WakeFrame", _wakeFrame);

            // The wash levels: the newest tick's nodes, between its last two ticks (another level binds a stand-in: the kernel reads
            // the wash only on a wash level).
            Bind("_WashNodes", _noWash);
            for (int l = 0; l < 2; l++)
            {
                Wash w = l == 0 ? _washFine : _washCoarse;
                WashSnapshot ws = w?.Take();
                if (ws == null)
                {
                    _boxes[l] = Vector4.zero;
                    continue;
                }
                double fraction = (t - (ws.Tick * FlightCore.Dt - ws.TickSeconds)) / ws.TickSeconds;
                fraction = fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
                _fractions[l] = fraction;
                _starts[l] = (ws.StartI, ws.StartJ);
                _boxes[l] = new Vector4((float)(ws.StartI * LevelCell[l]), (float)(ws.StartJ * LevelCell[l]), (float)LevelCell[l], LevelSize[l]);
                Bind("_WashNodes", w.Buffer);
                _cs.SetFloat("_WashFraction", (float)fraction);
                Dispatch(l, _gridA, blendA, true);
            }
            if (_washFine == null) Bind("_WashNodes", _bandBuf); // a buffer must be bound; the kernel reads it only on a wash level
            for (int l = 2; l < Levels; l++)
            {
                // Texel centres on whole cells, so the mean grids' nodes (their spacing's half-cells) are texels.
                double cell = LevelCell[l];
                double half = LevelSize[l] / 2 + 0.5;
                _boxes[l] = new Vector4((float)((Math.Floor(camera.x / cell) - half) * cell), (float)((Math.Floor(camera.z / cell) - half) * cell),
                    (float)cell, LevelSize[l]);
                Dispatch(l, l < 4 ? _gridA : _gridB, l < 4 ? blendA : blendB, false);
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

        void Dispatch(int level, Grid grid, float blend, bool wash)
        {
            _cs.SetInt("_Wash", wash ? 1 : 0);
            _cs.SetVector("_LevelOrigin", new Vector4(_boxes[level].x, _boxes[level].y, 0, 0));
            _cs.SetFloat("_LevelCell", (float)LevelCell[level]);
            _cs.SetInt("_LevelSize", LevelSize[level]);
            Bind("_LevelBands", _levelBands[level]);
            Bind("_Means", grid.Buffer);
            Bind("_MeanSlot", grid.SlotBuffer);
            _cs.SetInt("_MeanSlots", grid.Bands.Length);
            _cs.SetVectorArray("_MeanGrid", grid.Boxes);
            _cs.SetFloat("_MeanBlend", blend);
            _cs.SetTexture(_kernel, "_Out", _out[level]);
            int groups = (LevelSize[level] + ThreadGroup - 1) / ThreadGroup;
            _cs.Dispatch(_kernel, groups, groups, LevelBands[level].Length);
        }

        void Bind(string name, ComputeBuffer buffer) => _cs.SetBuffer(_kernel, name, buffer);

        // ---- the wind's mean, on the helper ----

        // One grid's snapshot: its flight time, node 0's centre, and per slot (band) and node PlantField.compute's five float4s:
        // (air, load y), (lean·way, way), (xx, xz, zz, yy), (xy, zy, open, strip share), (wake, 1/H, strip, valid).
        sealed class Snapshot
        {
            public long K = -1;
            public double X0, Z0;
            public float[] Data;
            public volatile bool Ready;
        }

        sealed class Grid
        {
            public readonly int Size;
            public readonly double Spacing;
            public readonly int[] Bands;
            public readonly Snapshot[] Ring = new Snapshot[4];
            public readonly ComputeBuffer Buffer, SlotBuffer;
            public readonly Vector4[] Boxes = new Vector4[2];
            public readonly double[] Times = new double[2];
            public long NextK;
            public double LastMs;
            readonly long[] _uploaded = { -1, -1 };

            public Grid(int size, double spacing, int[] bands)
            {
                Size = size;
                Spacing = spacing;
                Bands = bands;
                for (int i = 0; i < Ring.Length; i++) Ring[i] = new Snapshot { Data = new float[bands.Length * size * size * 20] };
                Buffer = new ComputeBuffer(2 * bands.Length * size * size * 5, 16);
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
                    if (s.Ready && s.K * SnapshotStep <= t + 1e-9 && (a == null || s.K > a.K)) a = s;
                if (a == null)
                    foreach (Snapshot s in Ring)
                        if (s.Ready && (a == null || s.K < a.K)) a = s; // a frame before the first snapshot's time holds it
                if (a == null) return false;
                foreach (Snapshot s in Ring)
                    if (s.Ready && s.K == a.K + 1) b = s;
                if (b == null) b = a;
                else blend = (float)Math.Min(1, Math.Max(0, (t - a.K * SnapshotStep) / SnapshotStep));
                return true;
            }

            public void Upload(Snapshot a, Snapshot b)
            {
                int n = Bands.Length * Size * Size * 5;
                if (_uploaded[0] != a.K) { Buffer.SetData(a.Data, 0, 0, n * 4); _uploaded[0] = a.K; }
                if (_uploaded[1] != b.K) { Buffer.SetData(b.Data, 0, n * 4, n * 4); _uploaded[1] = b.K; }
                Boxes[0] = new Vector4((float)a.X0, (float)a.Z0, (float)Spacing, Size);
                Boxes[1] = new Vector4((float)b.X0, (float)b.Z0, (float)Spacing, Size);
                Times[0] = a.K * SnapshotStep;
                Times[1] = b.K * SnapshotStep;
            }
        }

        readonly XZ[] _meanXz = new XZ[GridASize];
        readonly GroundSample[] _meanGround = new GroundSample[GridASize];

        // One snapshot that is due (the next of a grid from the frame's step to Lead ahead); false when none is.
        bool MeanStep()
        {
            double now = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _nowBits));
            foreach (Grid g in _grids)
            {
                long target = (long)Math.Floor((now + Lead) / SnapshotStep);
                long k = Math.Max(g.NextK, (long)Math.Floor(now / SnapshotStep));
                if (k > target) continue;
                Compute(g, k, _meanXz, _meanGround);
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

        // A snapshot of a grid at step k's time, centred on the camera now.
        void Compute(Grid g, long k, XZ[] xz, GroundSample[] ground)
        {
            var clock = Stopwatch.StartNew();
            Snapshot s = g.Ring[k % g.Ring.Length];
            s.Ready = false;
            double cx = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _cameraXBits));
            double cz = BitConverter.Int64BitsToDouble(Interlocked.Read(ref _cameraZBits));
            long i0 = (long)Math.Floor(cx / g.Spacing) - g.Size / 2, j0 = (long)Math.Floor(cz / g.Spacing) - g.Size / 2;
            s.X0 = (i0 + 0.5) * g.Spacing;
            s.Z0 = (j0 + 0.5) * g.Spacing;
            _meanAir.Begin(k * SnapshotStep);
            int nodes = g.Size * g.Size;
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
            s.K = k;
            s.Ready = true;
            g.LastMs = clock.Elapsed.TotalMilliseconds;
        }

        /// <summary>A mean as PlantField.compute reads it (five float4s).</summary>
        public static void Pack(float[] data, int at, in PlantMean m, int strip)
        {
            float[] v =
            {
                (float)m.Air.X, (float)m.Air.Y, (float)m.Air.Z, (float)m.LoadY,
                (float)(m.Lean * m.WayX), (float)(m.Lean * m.WayZ), (float)m.WayX, (float)m.WayZ,
                (float)m.XX, (float)m.XZ, (float)m.ZZ, (float)m.YY,
                (float)m.XY, (float)m.ZY, (float)m.Open, (float)m.StripShare,
                (float)m.Wake, (float)m.WakeInverse, strip, 1,
            };
            Array.Copy(v, 0, data, at, 20);
        }

        // ---- the wash, on its helper ----

        // One wash grid's tick as the GPU reads it: per node (window order) and band, (was x, z, now x, z) and (the wash's air at the
        // band's load point, the node's ground), the ground −1e30 where PlantWash.Bend holds no bend at the node.
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
            readonly object _lock = new object();
            readonly XZ[] _xz;
            readonly GroundSample[] _ground;

            public Wash(PlantWash grid, int size, double cell)
            {
                Grid = grid;
                Size = size;
                Cell = cell;
                Buffer = new ComputeBuffer(size * size * PlantBands.Grass * 2, 16);
                int length = size * size * PlantBands.Grass * 8;
                _back = new WashSnapshot { Data = new float[length] };
                _ready = new WashSnapshot { Data = new float[length] };
                _front = new WashSnapshot { Data = new float[length] };
                _xz = new XZ[size];
                _ground = new GroundSample[size];
            }

            // The helper: the tick's stands to the back snapshot, then swapped in.
            public void Export(PoseBuffer poses, IWorldQuery world)
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
                        for (int band = 0; band < PlantBands.Grass; band++)
                        {
                            int at = ((a + Size * b) * PlantBands.Grass + band) * 8;
                            // The window's last row and column hold no bend (PlantWash.Bend needs the node beyond; at a node's centre
                            // its answer there would turn on the coordinates' last bit).
                            if (a == Size - 1 || b == Size - 1 || !Grid.Bend(band, _xz[a].X, _xz[a].Z, 0, out PlantBend was, out Double3 air)
                                || !Grid.Bend(band, _xz[a].X, _xz[a].Z, 1, out PlantBend now, out _) || !(_ground[a].Height > double.NegativeInfinity))
                            {
                                Array.Clear(s.Data, at, 8);
                                s.Data[at + 7] = -1e30f;
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

        // The wash grids ticked to the newest published step, and a new tick's stands exported.
        void WashStep(ref long last)
        {
            if (_fine == null && _coarse == null) return;
            _fine?.Advance(_poses);
            _coarse?.Advance(_poses);
            long tick = (_fine ?? _coarse).LastTick;
            if (tick == last || tick < 0) return;
            _washFine?.Export(_poses, _world);
            _washCoarse?.Export(_poses, _world);
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
                _gridA.Buffer, _gridA.SlotBuffer, _gridB.Buffer, _gridB.SlotBuffer, _washFine?.Buffer, _washCoarse?.Buffer })
                b?.Release();
            foreach (ComputeBuffer b in _levelBands) b?.Release();
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
