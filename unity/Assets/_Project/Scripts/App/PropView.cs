using System;
using System.Collections.Generic;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The props as the camera exposes them (w13-props-in-frame, its design.md): the model's still props (its "prop" slot,
    /// the drone_prop material, merged into each LOD's mesh) are drawn by Shaders/PropHidden, which draws nothing, and each
    /// rotor is drawn by Shaders/PropDisc over a bound about
    /// its hub: the sim's blade (DroneSpec.Blade) at the sim's rotor angle (PoseFrame.RotorAngle), each pixel covered for
    /// the share of the camera's exposure (RollingShutter.ExposureS) the blade spends on its ray. In the FPV view each pixel
    /// takes the angle at its own field row's read (Field, from the history's 1 ms steps); the chase view and a camera not
    /// on the field latch read every row at the pose's instant, the rotor's speed held (Draw). A rotor at rest has no sweep:
    /// its blade is drawn still, where the sim holds it. Drawing only: nothing here reaches the flight.
    /// </summary>
    public sealed class PropView
    {
        public const string MaterialResource = "PropDisc", HiddenResource = "PropHidden";

        /// <summary>D01's prop material (its assetmeta's "prop" slot): the blades and hubs, and nothing else.</summary>
        public const string ModelMaterial = "drone_prop";

        /// <summary>The bound's height past the blade above and below, m: so its far wall, where the shader runs, clears the
        /// FPV camera's 0.05 m near plane for rays to the disc's nearest arc (bounding only: it changes nothing drawn).</summary>
        public const float BoundMarginM = 0.05f;

        /// <summary>The most blade stations the shader takes.</summary>
        public const int MaxStations = 40;

        const int BoundSides = 48;

        struct Rotor
        {
            public MeshRenderer Renderer;
            public double Sign; // the blade's angle in the disc's frame per sim angle: −1 clockwise (PropStrikes' s)
        }

        readonly List<Renderer> _hidden = new List<Renderer>();
        readonly Rotor[] _rotors = new Rotor[4];
        readonly float[] _angles = new float[RollingShutter.Samples];
        readonly float[] _chord = new float[MaxStations], _thick = new float[MaxStations], _pitch = new float[MaxStations];
        MaterialPropertyBlock _block;
        Material _material, _nothing;
        Mesh _bound;
        int _bound4;
        Vector4 _hub;
        float _radius, _stations, _blades;

        /// <summary>The model's prop material slots hidden (every LOD's).</summary>
        public int Hidden => _hidden.Count;

        /// <summary>The rotors drawn.</summary>
        public int Rotors => _bound4;

        /// <summary>Rotor c's drawing (tests and captures).</summary>
        public MeshRenderer Renderer(int c) => _rotors[c].Renderer;

        /// <summary>A new flight's model: its still props hidden and a turning prop drawn at each motor's hub (drone.json's
        /// motors[].position_m, the model's frame); none for a null model or a spec without a blade.</summary>
        public void Bind(GameObject model, DroneSpec spec)
        {
            _hidden.Clear();
            for (int c = 0; c < 4; c++) _rotors[c] = default;
            _bound4 = 0;
            if (model == null || spec?.Blade == null || spec.Motors == null || spec.Motors.Length != 4) return;
            if (_material == null) _material = Resources.Load<Material>(MaterialResource);
            if (_nothing == null) _nothing = Resources.Load<Material>(HiddenResource);
            if (_material == null || _nothing == null)
            {
                Debug.LogError("[Flight] no Resources/" + MaterialResource + " or " + HiddenResource + " material: the props are drawn as built, still");
                return;
            }
            Material own = null;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].name == ModelMaterial)
                    {
                        own = slots[i];
                        slots[i] = _nothing;
                        any = true;
                    }
                if (!any) continue;
                _hidden.Add(r);
                r.sharedMaterials = slots;
            }
            if (own == null) Debug.LogWarning("[Flight] the drone model has no " + ModelMaterial + " slot: its still props stay drawn under the turning ones");

            PropBlade blade = spec.Blade;
            int n = Math.Min(blade.Stations.Length, MaxStations);
            if (blade.Stations.Length > MaxStations) Debug.LogError("[Flight] the blade's " + blade.Stations.Length + " stations: the props draw its first " + MaxStations);
            double half = blade.HubThickness / 2;
            for (int k = 0; k < n; k++)
            {
                double a = blade.SectionAngle[k];
                _chord[k] = (float)blade.Chord[k];
                _thick[k] = (float)blade.Thickness[k];
                _pitch[k] = (float)a;
                half = Math.Max(half, Math.Abs(blade.Chord[k] / 2 * Math.Sin(a)) + Math.Abs(blade.Thickness[k] / 2 * Math.Cos(a)));
            }
            _radius = (float)(spec.Props.Diameter / 2);
            _stations = n;
            _blades = spec.Props.Blades;
            _hub = new Vector4((float)(blade.HubDiameter / 2), (float)(blade.HubThickness / 2), (float)(blade.Stations[0] - blade.Span / 2), (float)blade.Span);
            if (_bound == null) _bound = Bound(_radius, (float)half + BoundMarginM);
            if (_block == null) _block = new MaterialPropertyBlock();

            for (int c = 0; c < 4; c++)
            {
                var go = new GameObject("prop_turning_" + Sim.Contact.LegDamage.Names[c]);
                go.transform.SetParent(model.transform, false);
                Double3 hub = spec.Motors[c].Position;
                go.transform.localPosition = new Vector3((float)hub.X, (float)hub.Y, (float)hub.Z);
                go.AddComponent<MeshFilter>().sharedMesh = _bound;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _rotors[c] = new Rotor { Renderer = renderer, Sign = spec.Motors[c].Clockwise ? -1 : 1 };
                if (own != null)
                {
                    _block.Clear();
                    renderer.GetPropertyBlock(_block);
                    if (own.HasProperty("_BaseMap") && own.GetTexture("_BaseMap") != null) _block.SetTexture("_BaseMap", own.GetTexture("_BaseMap"));
                    if (own.HasProperty("_BaseColor")) _block.SetColor("_BaseColor", own.GetColor("_BaseColor"));
                    renderer.SetPropertyBlock(_block);
                }
            }
            _bound4 = 4;
        }

        /// <summary>The render clock's pose (the chase view, or a camera off the field latch): every row read at the pose's
        /// instant, the rotors' speeds held over the exposure.</summary>
        public void Draw(in PoseFrame pose, double exposureS)
        {
            for (int c = 0; c < _bound4; c++)
            {
                Held(pose, c, _angles);
                Upload(c, pose.RotorAngle[c], exposureS, false);
            }
        }

        /// <summary>The FPV view's field (FlightScene's latch): each pixel at its row's read, the rotors' angles from the
        /// history's 1 ms steps; at is the pose at the field's reference instant (FieldCamera.At).</summary>
        public void Field(PoseBuffer poses, long fieldStartUs, in PoseFrame at, double exposureS, FeedSettings feed)
        {
            if (feed != null) Shader.SetGlobalVector("_PropLens", new Vector4(feed.K1, feed.focalHalfWidths, FeedSettings.Aspect, FeedSettings.FieldRows));
            for (int c = 0; c < _bound4; c++)
            {
                Samples(poses, fieldStartUs, at, c, _angles);
                Upload(c, at.RotorAngle[c], exposureS, feed != null);
            }
        }

        void Upload(int c, double reference, double exposureS, bool rowTimed)
        {
            MeshRenderer r = _rotors[c].Renderer;
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetVector("_PropRotor", new Vector4((float)reference, (float)_rotors[c].Sign, _radius, rowTimed ? 1f : 0f));
            _block.SetVector("_PropHub", _hub);
            _block.SetVector("_PropRead", new Vector4((float)(exposureS * 1e6), (float)RollingShutter.ReferenceUs, _stations, _blades));
            _block.SetFloatArray("_PropAngles", _angles);
            _block.SetFloatArray("_PropChord", _chord);
            _block.SetFloatArray("_PropThick", _thick);
            _block.SetFloatArray("_PropPitch", _pitch);
            r.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Rotor c's angle at RollingShutter's samples (sample i at (i − SampleLead) ms of the field), less its angle at the
        /// reference instant, unwrapped along the turn: the history's steps from the reference outward, each step's turn the
        /// short way (under π rad a step, PoseFrame.Angles); a step the history does not hold takes its neighbour's.
        /// </summary>
        public static void Samples(PoseBuffer poses, long fieldStartUs, in PoseFrame at, int c, float[] into)
        {
            long first = fieldStartUs / FlightCore.StepUs - RollingShutter.SampleLead;
            int before = (int)Math.Floor(RollingShutter.ReferenceUs / RollingShutter.SampleUs) + RollingShutter.SampleLead;
            double reference = at.RotorAngle[c];
            double last = reference, sum = 0;
            for (int i = before; i < RollingShutter.Samples; i++) // forward from the reference
            {
                if (poses.TryRead(first + i, out PoseFrame p))
                {
                    sum += Arc(p.RotorAngle[c] - last);
                    last = p.RotorAngle[c];
                }
                into[i] = (float)sum;
            }
            // the sample at or before the reference, then back
            last = reference;
            sum = 0;
            for (int i = before; i >= 0; i--)
            {
                if (poses.TryRead(first + i, out PoseFrame p))
                {
                    sum -= Arc(last - p.RotorAngle[c]);
                    last = p.RotorAngle[c];
                }
                into[i] = (float)sum;
            }
        }

        /// <summary>Rotor c's angle at the samples against the pose's instant, its speed held: the chase view's.</summary>
        public static void Held(in PoseFrame pose, int c, float[] into)
        {
            for (int i = 0; i < RollingShutter.Samples; i++)
                into[i] = (float)(pose.RotorSpeed[c] * ((i - RollingShutter.SampleLead) * RollingShutter.SampleUs - RollingShutter.ReferenceUs) * 1e-6);
        }

        /// <summary>The blade's smear over an exposure, degrees: the rotor's speed (rad/s) times the exposure (s).</summary>
        public static double SmearDeg(double speed, double exposureS) => speed * exposureS * 180 / Math.PI;

        // A turn the short way, into (−π, π].
        static double Arc(double d)
        {
            const double TwoPi = 2 * Math.PI;
            if (d > Math.PI) d -= TwoPi;
            else if (d <= -Math.PI) d += TwoPi;
            return d;
        }

        // A closed cylinder about y through the origin, circumscribing the disc of this radius, halfHeight above and below.
        static Mesh Bound(float radius, float halfHeight)
        {
            float outer = radius / Mathf.Cos(Mathf.PI / BoundSides);
            var v = new List<Vector3> { new Vector3(0, halfHeight, 0), new Vector3(0, -halfHeight, 0) };
            var tris = new List<int>();
            for (int i = 0; i < BoundSides; i++)
            {
                float a = 2 * Mathf.PI * i / BoundSides;
                v.Add(new Vector3(outer * Mathf.Cos(a), halfHeight, outer * Mathf.Sin(a)));
                v.Add(new Vector3(outer * Mathf.Cos(a), -halfHeight, outer * Mathf.Sin(a)));
            }
            for (int i = 0; i < BoundSides; i++)
            {
                int top = 2 + 2 * i, bottom = top + 1, nextTop = 2 + 2 * ((i + 1) % BoundSides), nextBottom = nextTop + 1;
                tris.AddRange(new[] { 0, nextTop, top });           // the top, facing up
                tris.AddRange(new[] { 1, bottom, nextBottom });     // the bottom, facing down
                tris.AddRange(new[] { top, nextTop, nextBottom });  // the side, facing out
                tris.AddRange(new[] { top, nextBottom, bottom });
            }
            var mesh = new Mesh { name = "prop_bound" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
