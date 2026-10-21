using UnityEngine;

namespace Fpv450.World
{
    /// <summary>
    /// The G1 gallery's stations and viewpoints (p1-g1-look-lock-gallery design 1 and 3), the data
    /// <c>Fpv450.GalleryScene.Build</c> makes <c>Gallery_LookLock.unity</c> from. Placing a station's prefab is one
    /// <see cref="Placement"/> line here and a scene rebuild, never an edit to the scene. The stations run along +X
    /// on a flat floor at y = 0, each row across the view from -Z (the 200 m belt view looks +Z at its band); every
    /// position is metres from the station's origin, every yaw degrees about +Y (0 faces +Z, 90 faces +X).
    /// </summary>
    public static class GalleryStations
    {
        public const string PrefabsFolder = "Assets/_Project/Prefabs/";

        /// <summary>A prefab (its path under <see cref="PrefabsFolder"/>, no extension) standing on the floor.</summary>
        public readonly struct Placement
        {
            public readonly string Prefab;
            public readonly float X, Z, YawDeg;

            public Placement(string prefab, float x, float z, float yawDeg)
            {
                Prefab = prefab;
                X = x;
                Z = z;
                YawDeg = yawDeg;
            }

            public string Name => Prefab.Substring(Prefab.LastIndexOf('/') + 1);
        }

        /// <summary>
        /// A wire span hung by its ports: conductor i from port <see cref="FromPorts"/>[i] of placement
        /// <see cref="From"/> to <see cref="ToPorts"/>[i] of placement <see cref="To"/>, which slides along the line
        /// until its port lies one chord away. <see cref="To"/> -1 is a free end (a service drop whose house is not on
        /// this station): the span leaves along its first port's outward direction.
        /// </summary>
        public readonly struct Hanging
        {
            public readonly string Span;
            public readonly int From, To;
            public readonly string[] FromPorts, ToPorts;

            public Hanging(string span, int from, string[] fromPorts, int to = -1, string[] toPorts = null)
            {
                Span = span;
                From = from;
                FromPorts = fromPorts;
                To = to;
                ToPorts = toPorts ?? new string[0];
            }

            public string Name => Span.Substring(Span.LastIndexOf('/') + 1);
        }

        public sealed class Station
        {
            public readonly int Number;
            public readonly string Name, Letters;
            public readonly Vector3 Origin;
            public readonly Placement[] Placements;
            public readonly Hanging[] Hangings;

            public Station(int number, string name, string letters, Vector3 origin, Placement[] placements, Hanging[] hangings = null)
            {
                Number = number;
                Name = name;
                Letters = letters;
                Origin = origin;
                Placements = placements;
                Hangings = hangings ?? new Hanging[0];
            }

            /// <summary>The numbered marker's name: the station's number and asset ids, no other text.</summary>
            public string MarkerName => $"{Number} {Name}";
        }

        /// <summary>A saved camera pose: number key n is <see cref="Viewpoints"/>[n - 1]. Pitch is down-positive.</summary>
        public readonly struct Viewpoint
        {
            public readonly string Name, Letters;
            public readonly Vector3 Position;
            public readonly float YawDeg, PitchDeg;

            public Viewpoint(string name, string letters, Vector3 position, float yawDeg, float pitchDeg)
            {
                Name = name;
                Letters = letters;
                Position = position;
                YawDeg = yawDeg;
                PitchDeg = pitchDeg;
            }

            public Quaternion Rotation => Quaternion.Euler(PitchDeg, YawDeg, 0f);
        }

        static readonly string[] FourHooks = { "hook_0", "hook_1", "hook_2", "hook_3" };

        public static readonly Station[] Stations =
        {
            new Station(1, "S04", "H", new Vector3(0f, 0f, 0f), new[]
            {
                new Placement("surfaces/S04_patch", 0f, 0f, 0f),
            }),
            // The belt section: a 20 m row across the 200 m view, the V13 clumps on its -Z edge and among the trunks (PR-10).
            new Station(2, "V01 V13", "H", new Vector3(40f, 0f, 0f), new[]
            {
                new Placement("vegetation/V01_M", -7f, 0f, 0f),
                new Placement("vegetation/V01_L", 0f, 0f, 120f),
                new Placement("vegetation/V01_S", 7f, 0f, 240f),
                new Placement("vegetation/V13_tall", -3.5f, -1.5f, 200f),
                new Placement("vegetation/V13_mid", 3.5f, -1.5f, 80f),
                new Placement("vegetation/V13_low", 10f, -2f, 310f),
            }),
            // Task 1.3, as p1-h01-adobe-house merges: H01_I at (-15, 0) and H01_D at (0, 0), facades to -Z, 15 m apart (H01_X at (15, 0) after G1).
            new Station(3, "H01", "E", new Vector3(100f, 0f, 0f), new Placement[0]),
            // Task 1.3, as p1-r01-vaz-sedan merges: R01_A at (-4, 0) and R01_F at (4, 0), 8 m apart, noses along +X.
            new Station(4, "R01", "M", new Vector3(160f, 0f, 0f), new Placement[0]),
            new Station(5, "E01", "G", new Vector3(210f, 0f, 0f), new[]
            {
                new Placement("earthworks/E01_curving", 0f, 0f, 0f),
            }),
            // The line along Z: the 4-conductor medium span from the straight pole to the stub 40 m back (the only
            // pole whose hooks match its far ends: the leaning pole's do not), the thin wire from the straight pole's
            // top hook to the leaning pole's, and the service drop off hook_2 to the +X side.
            new Station(6, "L01 L03", "L J", new Vector3(270f, 0f, 0f), new[]
            {
                new Placement("lines/L01_straight", 0f, 0f, 90f),
                new Placement("lines/L01_leaning", 0f, 40f, 90f),
                new Placement("lines/L01_stub", 0f, -40f, 90f),
            }, new[]
            {
                new Hanging("lines/L03_medium", 0, FourHooks, 2, FourHooks),
                new Hanging("lines/L03_thin", 0, new[] { "hook_0" }, 1, new[] { "hook_0" }),
                new Hanging("lines/L03_thick", 0, new[] { "hook_2" }),
            }),
        };

        /// <summary>
        /// One per station at its letter's height and framing (design 3), the 200 m belt view last. H01's and R01's
        /// assume the facade and the struck side face -Z; task 1.3 checks them against the placed prefabs and adds
        /// H01_D's 1 m door view as key 9.
        /// </summary>
        public static readonly Viewpoint[] Viewpoints =
        {
            // 1.5 m over the patch centre, 35° down towards +X (H), as the look setup's capture.
            new Viewpoint("S04", "H", new Vector3(0f, 1.5f, 0f), 90f, 35f),
            // 1.5 m up, 8 m out from the -Z edge clumps, looking west along the edge (H: low along a belt edge).
            new Viewpoint("V01 V13", "H", new Vector3(52f, 1.5f, -9.5f), 300f, 0f),
            // 1.5 m up, 12 m from H01_D's facade (E).
            new Viewpoint("H01", "E", new Vector3(100f, 1.5f, -15.5f), 0f, 0f),
            // 1.2 m up, 6 m from R01_A's struck side (M).
            new Viewpoint("R01", "M", new Vector3(156f, 1.2f, -6.8f), 0f, 0f),
            // 1.5 m above the field-side lip at the run's start, along its first segment 20° down (G), as the review's earthwork pose.
            new Viewpoint("E01", "G", new Vector3(210.133f, 1.5f, -0.377f), 70.62f, 20f),
            // 2 m up, 15 m from the leaning pole, 5° up so the pole stands against the sky (L, under night).
            new Viewpoint("L01", "L", new Vector3(270f, 2f, 55f), 180f, -5f),
            // 2 m up beside the stub, 20 m from the medium span's middle, looking up along the wires to the north (J).
            new Viewpoint("L03", "J", new Vector3(267f, 2f, -40f), 0f, -10f),
            // 200 m from the belt row, 2 m up, level, looking +Z at its band (VIS-04, A and H).
            new Viewpoint("V01 V13 200 M", "A H", new Vector3(40f, 2f, -200f), 0f, 0f),
        };
    }
}
