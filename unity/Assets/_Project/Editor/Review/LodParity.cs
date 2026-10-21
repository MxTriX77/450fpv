using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The LOD parity measure (p1-v01-locust tasks 1.2a and 1.2f, design decision 3 as amended; asset-pipeline "Baked
    /// impostor LOD", scenario "No LOD switch pops"). For every variant of an asset id, or the one <c>-variant</c> names, by
    /// the review's day and overcast light (<c>Settings/Review/</c>): the review sheet's 50 m tile and a tile at each
    /// LOD's switch distance, from the review's far-shot camera, with each LOD forced in turn. The stage, the camera,
    /// the light profiles, the shadow-distance fit and the warm-up render are the review capture's
    /// (<c>ReviewCapture.cs</c>, task 1.2h), with the scale bar hidden (<see cref="Stage"/>). Each LOD's crown comes
    /// from two renders, over black and over white, with no ground and no scale bar: a pixel's coverage is one less the
    /// mean over the linear channels of white minus black, and the render over black is its colour times that coverage.
    /// Each value is the mean over an 8 × 8 grid of camera offsets, an eighth of a pixel apart from 1/16 to 7/16 px
    /// either way of the review's framing, LOD0's too: single renders of a crown of about 100 px scatter by 5–16 % with
    /// the sub-pixel phase, like noise, so the error of the mean falls as 1/√N (tasks 1.2c and 1.2d). A tile where any
    /// LOD's standard error on that grid is 1 % or more is measured again, every LOD, on a 16 × 16 grid a sixteenth of
    /// a pixel apart from 1/32 to 15/32 px (<see cref="Refine"/>, task 1.2k): true coverage at MSAA 1 point-samples a
    /// crown of 85–100 px's sub-pixel leaves and wood as geometry is, 1.10–1.47 % on 64 offsets (task 1.2i). Each row
    /// prints the grid it used, the spread of its luminance and coverage over the grid's offsets (max − min) and their
    /// standard error of the mean (the sample standard deviation over √N), each as a percentage of the mean; the
    /// offsets are made on the projection, see <see cref="Offset"/>.
    /// Per LOD it prints the coverage-weighted mean linear colour and its Rec. 709 luminance and the crown's coverage in
    /// pixels. Each tile judges one row, the scenario's pair (<see cref="Tiles"/>): at a LOD's switch distance the
    /// incoming LOD against the outgoing one, at the 50 m tile the LOD Unity shows there against LOD0. The judged row
    /// prints both values against the other LOD's, the margin to the spec's limits (6 % in luminance, 15 % in
    /// coverage) and the worst of the pair's four standard errors with its margin to 1 %, in percentage points,
    /// negative outside them, and PASS, FAIL or NOT JUDGEABLE (<see cref="Judge"/>). Every other row is printed for
    /// information, against LOD0 as before, marked <c>info</c> with no verdict: LOD0 is the reference only up to its
    /// own switch, and a LOD is never judged nearer than it is shown (task 1.2b). The shadow distance is the
    /// review's, fitted to its stage, so a small asset's 50 m tile has no shadows; the review's 225° camera looks
    /// along one of the impostor's eight baked views, its best case.
    /// <para>Batchmode, with graphics:
    /// <c>-batchmode -quit -executeMethod Fpv450.LodParity.Measure -asset &lt;ID&gt; [-variant &lt;variant&gt;]</c>.
    /// The exit code comes from the judged rows alone (<see cref="ExitCode"/>): 0 when every one passes, 2 when one
    /// fails, 3 when none fails but one is not judgeable, 1 on an error.</para>
    /// </summary>
    public static class LodParity
    {
        // The spec's limits ("No LOD switch pops"): a judged LOD's crown against the other LOD of its pair, each value's
        // standard error under 1 %.
        internal const double LuminanceLimit = 0.06, CoverageLimit = 0.15, ErrorLimit = 0.01;

        // The review sheet's 50 m tile.
        static readonly float ReviewTileM = Review.DistancesM[2];

        // sRGB byte to linear (IEC 61966-2-1), as the readback holds the target's encoded bytes.
        static readonly double[] Linear = Enumerable.Range(0, 256).Select(v =>
        {
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }).ToArray();

        /// <summary>A crown in one tile: its coverage in pixels and the coverage-weighted mean of its linear colour.</summary>
        internal readonly struct Crown
        {
            public readonly double Coverage, R, G, B;

            public Crown(double coverage, double r, double g, double b)
            {
                Coverage = coverage;
                R = r;
                G = g;
                B = b;
            }

            /// <summary>Rec. 709 luminance of the mean linear colour; NaN when nothing is covered.</summary>
            public double Luminance => 0.2126 * R + 0.7152 * G + 0.0722 * B;
        }

        /// <summary>
        /// The crown from the same tile rendered over black and over white. A pixel of coverage a and linear colour c
        /// reads a·c over black and a·c + 1 − a over white, so a = 1 − mean(white − black) over the linear channels, and
        /// the crown's mean colour is the sum over black divided by the summed coverage.
        /// </summary>
        internal static Crown Find(Color32[] overBlack, Color32[] overWhite)
        {
            if (overBlack.Length != overWhite.Length)
                throw new ArgumentException($"the renders differ in size: {overBlack.Length} and {overWhite.Length} px");
            double a = 0, r = 0, g = 0, b = 0;
            for (int i = 0; i < overBlack.Length; i++)
            {
                double r0 = Linear[overBlack[i].r], g0 = Linear[overBlack[i].g], b0 = Linear[overBlack[i].b];
                a += 1.0 - (Linear[overWhite[i].r] - r0 + Linear[overWhite[i].g] - g0 + Linear[overWhite[i].b] - b0) / 3.0;
                r += r0;
                g += g0;
                b += b0;
            }
            return a > 0 ? new Crown(a, r / a, g / a, b / a) : new Crown(0, double.NaN, double.NaN, double.NaN);
        }

        /// <summary>The camera offsets of an n × n grid, in pixels: 1/n px apart, ±1/2n to ±(n − 1)/2n, centred on the review's framing, x the faster.</summary>
        internal static Vector2[] OffsetGrid(int n)
        {
            float[] steps = Enumerable.Range(0, n).Select(k => (2 * k + 1 - n) / (2f * n)).ToArray();
            return steps.SelectMany(y => steps.Select(x => new Vector2(x, y))).ToArray();
        }

        /// <summary>
        /// The camera offsets each value is averaged over: the 8 × 8 grid (±1/16 to ±7/16 px), and the 16 × 16 grid
        /// (±1/32 to ±15/32 px) of a tile that <see cref="Refine"/> measures again.
        /// </summary>
        internal static readonly Vector2[] Offsets = OffsetGrid(8), FineOffsets = OffsetGrid(16);

        /// <summary>
        /// Whether a tile is measured again on the 16 × 16 grid: when any of its LODs' standard errors on the 8 × 8 grid,
        /// in luminance or coverage, as fractions of the mean, is 1 % or more (task 1.2k).
        /// </summary>
        internal static bool Refine((double luminance, double coverage)[] errors) =>
            errors.Any(e => e.luminance >= ErrorLimit || e.coverage >= ErrorLimit);

        /// <summary>
        /// A LOD's crown over the offsets: the mean of the samples' coverages and colours, so its luminance is the mean of
        /// theirs (luminance is linear in the colour); the spread, max − min over the samples, of the luminance and of
        /// the coverage; and the standard error of each mean, the samples' standard deviation (over n − 1) divided by
        /// √n; each as a fraction of its mean.
        /// </summary>
        internal static (Crown mean, double luminanceSpread, double coverageSpread, double luminanceError, double coverageError) Average(Crown[] samples)
        {
            var mean = new Crown(samples.Average(s => s.Coverage), samples.Average(s => s.R), samples.Average(s => s.G), samples.Average(s => s.B));
            double luminanceSpread = samples.Max(s => s.Luminance) - samples.Min(s => s.Luminance);
            double coverageSpread = samples.Max(s => s.Coverage) - samples.Min(s => s.Coverage);
            return (mean, luminanceSpread / mean.Luminance, coverageSpread / mean.Coverage,
                    StandardError(samples.Select(s => s.Luminance), mean.Luminance) / mean.Luminance,
                    StandardError(samples.Select(s => s.Coverage), mean.Coverage) / mean.Coverage);
        }

        static double StandardError(IEnumerable<double> values, double mean)
        {
            double[] v = values.ToArray();
            return Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Length - 1) / v.Length);
        }

        /// <summary>A LOD's value against LOD0's, as a fraction: 0.05 is 5 % over.</summary>
        internal static double Relative(double value, double lod0) => value / lod0 - 1.0;

        /// <summary>Whether a LOD's crown is within the spec's limits of another's (a NaN, a LOD with no crown, fails).</summary>
        internal static bool Passes(double relativeLuminance, double relativeCoverage) =>
            Math.Abs(relativeLuminance) < LuminanceLimit && Math.Abs(relativeCoverage) < CoverageLimit;

        /// <summary>
        /// The tiles in the order they are measured, the review's 50 m tile first and then each LOD's switch distance,
        /// with the pair each judges ("No LOD switch pops"): at LOD i's switch, LOD i coming in against LOD i − 1 going
        /// out (the impostor against the last mesh LOD at its switch); at 50 m, the LOD Unity shows there, the last
        /// whose switch distance is at most 50 m, against LOD0. The 50 m tile judges nothing (−1 and −1) when LOD0 is
        /// shown there or the asset is culled by then. <paramref name="switchDistancesM"/> holds every LOD's, LOD0's 0.
        /// </summary>
        internal static (float distance, int judged, int against)[] Tiles(float[] switchDistancesM, float cullDistanceM)
        {
            int shown = Enumerable.Range(0, switchDistancesM.Length).Last(i => switchDistancesM[i] <= ReviewTileM);
            bool judged = shown > 0 && ReviewTileM < cullDistanceM;
            var tiles = new List<(float distance, int judged, int against)> { (ReviewTileM, judged ? shown : -1, judged ? 0 : -1) };
            for (int i = 1; i < switchDistancesM.Length; i++)
                tiles.Add((switchDistancesM[i], i, i - 1));
            return tiles.ToArray();
        }

        internal enum Verdict { Pass, Fail, NotJudgeable }

        /// <summary>
        /// A judged row: the incoming LOD's crown against the other LOD's of its pair, each with the standard errors of
        /// its luminance and coverage as fractions of the mean. The row is not judgeable when any of the four errors is
        /// 1 % or more; otherwise it passes inside both limits and fails outside them (a NaN, a LOD with no crown,
        /// fails). Returns both differences and the worst of the four errors, for the row's margins.
        /// </summary>
        internal static (double luminance, double coverage, double worstError, Verdict verdict) Judge(
            Crown judged, (double luminance, double coverage) judgedError, Crown against, (double luminance, double coverage) againstError)
        {
            double y = Relative(judged.Luminance, against.Luminance), cover = Relative(judged.Coverage, against.Coverage);
            double worst = new[] { judgedError.luminance, judgedError.coverage, againstError.luminance, againstError.coverage }.Max();
            Verdict verdict = worst >= ErrorLimit ? Verdict.NotJudgeable : Passes(y, cover) ? Verdict.Pass : Verdict.Fail;
            return (y, cover, worst, verdict);
        }

        /// <summary>The exit code from the judged rows alone: 2 when one fails, else 3 when one is not judgeable, else 0.</summary>
        internal static int ExitCode(int failed, int notJudgeable) => failed > 0 ? 2 : notJudgeable > 0 ? 3 : 0;

        static string Outcome(int exitCode) => exitCode == 0 ? "PASS" : exitCode == 2 ? "FAIL" : "NOT JUDGEABLE";

        public static void Measure()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string id = Arg(args, "-asset") ?? throw new ArgumentException("usage: -executeMethod Fpv450.LodParity.Measure -asset <ID> [-variant <variant>]");
                int code = Run(id, Arg(args, "-variant"));
                if (code != 0)
                    EditorApplication.Exit(code);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LodParity] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string[] args, string name)
        {
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static int Run(string id, string onlyVariant)
        {
            AssetDatabase.Refresh();
            (string assetClass, string[] variants) = Review.CatalogEntry(id);
            if (onlyVariant != null)
            {
                if (!variants.Contains(onlyVariant))
                    throw new ArgumentException($"{onlyVariant} is not a variant of {id} ({string.Join(", ", variants)})");
                variants = new[] { onlyVariant };
            }
            foreach (string variant in variants)
            {
                string path = Review.PrefabPath(assetClass, variant);
                Review.RequireRenderable(AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException($"{path} is missing: run the catalog import first"), variant);
            }
            Review.LightProfile[] profiles = Review.Lights.Select(Review.LoadProfile).ToArray();
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset ?? throw new InvalidOperationException("no URP asset is active");

            ShaderUtil.allowAsyncCompilation = false; // or the first renders show the "compiling" placeholder
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[LodParity] {id}: {variants.Length} variant(s), lights {string.Join("/", Review.Lights)}, GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                      $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} ({pipeline.name}), LOD bias {QualitySettings.lodBias:0.##}, MSAA {Mathf.Max(1, pipeline.msaaSampleCount)}, " +
                      $"colour space {QualitySettings.activeColorSpace}; limits luminance {LuminanceLimit * 100:0} %, coverage {CoverageLimit * 100:0} %, standard error under {ErrorLimit * 100:0} %; " +
                      "judged: at each switch the incoming LOD against the outgoing one, at the review tile the LOD shown there against LOD0, every other row info; " +
                      $"each value the mean over {Offsets.Length} projection offsets ({string.Join(", ", Offsets.Select(o => $"{o.x:+0.####;-0.####} {o.y:+0.####;-0.####}"))} px), " +
                      $"at a tile where any LOD's se on them is at or over {ErrorLimit * 100:0} % every LOD's again over the {FineOffsets.Length} of a 16x16 grid " +
                      $"({FineOffsets[0].x:+0.#####;-0.#####} to {FineOffsets[FineOffsets.Length - 1].x:+0.#####;-0.#####} px in x and y, {FineOffsets[1].x - FineOffsets[0].x:0.#####} px apart), " +
                      "each row naming its grid, spread max - min over the grid's offsets, se their sample standard deviation over sqrt(their number), both as a percentage of the mean");

            Review.Rig rig = Review.Rig.Create();
            float projectShadowDistance = pipeline.shadowDistance;
            int judged = 0, failed = 0, notJudgeable = 0, info = 0;
            try
            {
                foreach (string variant in variants)
                {
                    Review.Stage stage = Stage(id, assetClass, variant);
                    try
                    {
                        (int variantJudged, int variantFailed, int variantNotJudgeable, int variantInfo) = MeasureVariant(stage, variant, profiles, rig, pipeline, projectShadowDistance);
                        judged += variantJudged;
                        failed += variantFailed;
                        notJudgeable += variantNotJudgeable;
                        info += variantInfo;
                    }
                    finally
                    {
                        pipeline.shadowDistance = projectShadowDistance;
                        Object.DestroyImmediate(stage.Root);
                    }
                }
            }
            finally
            {
                rig.Dispose();
                foreach (GameObject sun in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "ReviewSun"))
                    Object.DestroyImmediate(sun);
            }
            int code = ExitCode(failed, notJudgeable);
            Debug.Log($"[LodParity] {id}: {Outcome(code)}, {failed} of {judged} judged rows outside the limits, {notJudgeable} not judgeable; {info} info rows");
            return code;
        }

        static (int judged, int failed, int notJudgeable, int info) MeasureVariant(Review.Stage stage, string variant, Review.LightProfile[] profiles, Review.Rig rig, UniversalRenderPipelineAsset pipeline, float projectShadowDistance)
        {
            GameObject instance = stage.Root.transform.GetChild(0).gameObject;
            AssetPhysicsMeta meta = instance.GetComponent<AssetPhysicsMeta>() ?? throw new InvalidOperationException($"{variant}: the prefab has no AssetPhysicsMeta");
            LODGroup group = stage.LodGroup ?? throw new InvalidOperationException($"{variant}: the prefab has no LODGroup");
            int lodCount = group.lodCount;
            if (lodCount != meta.lods.Length)
                throw new InvalidOperationException($"{variant}: the LOD group has {lodCount} LODs, the metadata {meta.lods.Length}");
            string[] names = meta.lods.Select(l => l.mesh.StartsWith(variant + "_", StringComparison.Ordinal) ? l.mesh.Substring(variant.Length + 1) : l.mesh).ToArray();

            // The review's stage and the shadow distance it fits to it.
            Bounds asset = stage.AssetBounds;
            pipeline.shadowDistance = Review.ShadowDistance(stage, projectShadowDistance);

            // Each tile names the LODs Unity shows there, the one in range at 50 m, the two that meet at a switch, and judges them.
            (float distance, int judged, int against)[] tiles = Tiles(meta.lods.Select(l => l.switchDistanceM).ToArray(), meta.cullDistanceM);
            string[] labels = tiles.Select((t, k) => k > 0 ? $"{names[k]} switch {t.distance:0.#} m, {names[t.against]} to {names[t.judged]}"
                : $"review tile {ReviewTileM:0.#} m, " + (ReviewTileM >= meta.cullDistanceM ? "culled" : t.judged < 0 ? $"{names[0]} shown, none judged" : $"{names[t.judged]} shown")).ToArray();
            Debug.Log($"[LodParity] {variant}: LODs {string.Join(", ", names.Select((n, i) => $"{n} {meta.lods[i].tris} tris" + (i == 0 ? "" : $" @ {meta.lods[i].switchDistanceM:0.#} m")))}, " +
                      $"cull {meta.cullDistanceM:0.#} m; bounds centre {asset.center}, size {asset.size}; shadow distance {pipeline.shadowDistance:0.#} m (game {projectShadowDistance:0.#} m)");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            int judgedRows = 0, failed = 0, notJudgeable = 0, info = 0, refined = 0;
            try
            {
                foreach (Review.LightProfile profile in profiles)
                {
                    Review.ApplyLight(profile, rig);
                    for (int t = 0; t < tiles.Length; t++)
                    {
                        string label = labels[t];
                        (float distance, int judged, int against) = tiles[t];
                        Vector3 position = Review.SlantShot(asset, distance, out bool fromBounds);
                        float toCentre = Vector3.Distance(position, asset.center);
                        if (toCentre + asset.extents.magnitude > rig.Camera.farClipPlane)
                            throw new InvalidOperationException($"{variant} {label}: the crown lies past the {rig.Camera.farClipPlane:0} m far plane");
                        Review.WarmUp(rig, stage, position, asset.center);
                        rig.Aim(position, asset.center);
                        (Crown[] crowns, (double luminance, double coverage)[] spreads, (double luminance, double coverage)[] errors) = MeasureLods(group, rig, Offsets);
                        string grid = "8x8", refinedFrom = "";
                        if (Refine(errors))
                        {
                            refinedFrom = $"; worst se on 8x8 {errors.Max(e => Math.Max(e.luminance, e.coverage)) * 100:0.00} %, every LOD again on 16x16";
                            (crowns, spreads, errors) = MeasureLods(group, rig, FineOffsets);
                            grid = "16x16";
                            refined++;
                        }
                        if (crowns[0].Coverage <= 0)
                            throw new InvalidOperationException($"{variant} {profile.name} {label}: LOD0 covers no pixel");
                        Debug.Log($"[LodParity] {variant} {profile.name}, {label}: camera {toCentre:0.00} m from the bounds centre{(fromBounds ? " (pushed out of the bounds)" : "")}{refinedFrom}");
                        for (int i = 0; i < lodCount; i++)
                        {
                            Crown c = crowns[i];
                            string row = $"[LodParity]   {names[i],-9} Y {c.Luminance:0.0000}  coverage {c.Coverage,9:0.0} px  linear rgb {c.R:0.0000} {c.G:0.0000} {c.B:0.0000}" +
                                         $"  spread Y {spreads[i].luminance * 100:0.00} %, coverage {spreads[i].coverage * 100:0.00} %" +
                                         $"  se Y {errors[i].luminance * 100:0.00} %, coverage {errors[i].coverage * 100:0.00} %  grid {grid}";
                            if (i == judged)
                            {
                                (double y, double cover, double worst, Verdict verdict) = Judge(c, errors[i], crowns[against], errors[against]);
                                row += $"  against {names[against]}: Y {y * 100:+0.00;-0.00} % (margin {(LuminanceLimit - Math.Abs(y)) * 100:0.00}), " +
                                       $"coverage {cover * 100:+0.00;-0.00} % (margin {(CoverageLimit - Math.Abs(cover)) * 100:0.00}), " +
                                       $"worst se {worst * 100:0.00} % (margin {(ErrorLimit - worst) * 100:0.00})  " +
                                       (verdict == Verdict.Pass ? "PASS" : verdict == Verdict.Fail ? "FAIL" : "NOT JUDGEABLE");
                                judgedRows++;
                                if (verdict == Verdict.Fail)
                                    failed++;
                                if (verdict == Verdict.NotJudgeable)
                                    notJudgeable++;
                            }
                            else
                            {
                                if (i > 0)
                                {
                                    double y = Relative(c.Luminance, crowns[0].Luminance), cover = Relative(c.Coverage, crowns[0].Coverage);
                                    row += $"  against LOD0: Y {y * 100:+0.00;-0.00} % (margin {(LuminanceLimit - Math.Abs(y)) * 100:0.00}), " +
                                           $"coverage {cover * 100:+0.00;-0.00} % (margin {(CoverageLimit - Math.Abs(cover)) * 100:0.00})";
                                }
                                row += "  info";
                                info++;
                            }
                            Debug.Log(row);
                        }
                    }
                }
            }
            finally
            {
                group.ForceLOD(-1);
            }
            TimeSpan took = watch.Elapsed;
            Debug.Log($"[LodParity] {variant}: {Outcome(ExitCode(failed, notJudgeable))}, {failed} of {judgedRows} judged rows outside the limits, {notJudgeable} not judgeable; {info} info rows; " +
                      $"{refined} of {profiles.Length * tiles.Length} tiles on 16x16; {(int)took.TotalMinutes} min {took.Seconds} s; process peak working set {PeakWorkingSetMb()} MB");
            return (judgedRows, failed, notJudgeable, info);
        }

        /// <summary>
        /// The Unity process's peak working set since it started, in MB, from Windows (task 1.2m: a refined V01 variant
        /// makes about 8 000 renders on a machine other agents share); Mono's Process.PeakWorkingSet64 reads 0 in the
        /// editor (measured). -1 when Windows does not answer.
        /// </summary>
        static long PeakWorkingSetMb()
        {
            var counters = new ProcessMemoryCounters { cb = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
            return GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.cb) ? (long)(counters.PeakWorkingSetSize.ToUInt64() >> 20) : -1;
        }

        // PROCESS_MEMORY_COUNTERS (psapi.h).
        [StructLayout(LayoutKind.Sequential)]
        struct ProcessMemoryCounters
        {
            public uint cb, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }

        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
        static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        /// <summary>Every LOD's crown from the aimed camera, each averaged over <paramref name="offsets"/> (<see cref="Average"/>).</summary>
        static (Crown[] crowns, (double luminance, double coverage)[] spreads, (double luminance, double coverage)[] errors) MeasureLods(LODGroup group, Review.Rig rig, Vector2[] offsets)
        {
            int lodCount = group.lodCount;
            var crowns = new Crown[lodCount];
            var spreads = new (double luminance, double coverage)[lodCount];
            var errors = new (double luminance, double coverage)[lodCount];
            for (int i = 0; i < lodCount; i++)
            {
                group.ForceLOD(i);
                var samples = new Crown[offsets.Length];
                for (int k = 0; k < offsets.Length; k++)
                {
                    Offset(rig.Camera, offsets[k]);
                    samples[k] = Find(Shoot(rig, Color.black), Shoot(rig, Color.white));
                }
                (crowns[i], spreads[i].luminance, spreads[i].coverage, errors[i].luminance, errors[i].coverage) = Average(samples);
            }
            group.ForceLOD(-1);
            return (crowns, spreads, errors);
        }

        // --- the review's stage and camera (ReviewCapture.cs) -----------------------------------------------------

        /// <summary>
        /// The review capture's stage for a variant, the scale bar hidden: the crown is rendered alone, and the bar's
        /// place still enters the stage's shadow distance (<see cref="Review.ShadowDistance"/>).
        /// </summary>
        internal static Review.Stage Stage(string id, string assetClass, string variant)
        {
            ReviewMeta meta = ReviewMeta.Read(Review.ProjectPath(Review.MetaPath(id, assetClass, variant)));
            Review.Stage stage = Review.Stage.Build(Review.PrefabPath(assetClass, variant), meta, assetClass);
            stage.HideBar();
            return stage;
        }

        /// <summary>
        /// Shifts the image by a fraction of a pixel from the review's framing: a translation in clip space on the
        /// projection, as URP's TAA jitters it. The eye point stays, so every depth moves by the same fraction of a
        /// pixel and the light and shadows are the same view's; moving the camera in its own plane instead is exact
        /// at one depth only, and at a 7.5 m switch a crown 2 m deep would see its near and far leaves move apart.
        /// </summary>
        static void Offset(Camera camera, Vector2 pixels)
        {
            camera.ResetProjectionMatrix();
            camera.projectionMatrix = Matrix4x4.Translate(new Vector3(pixels.x, pixels.y, 0f) * (2f / Review.Tile)) * camera.projectionMatrix;
        }

        /// <summary>The review camera's render, as it is aimed, over a background of <paramref name="background"/>.</summary>
        static Color32[] Shoot(Review.Rig rig, Color background)
        {
            rig.Camera.backgroundColor = background;
            return rig.Render().GetPixels32();
        }
    }
}
