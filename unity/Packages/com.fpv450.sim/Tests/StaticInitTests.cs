using System;
using System.Collections.Generic;
using System.Reflection;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // PLT-04's zero steady-state allocations, guarded at its root: a static reference field (an array, a table, a string
    // built at runtime, a cached delegate) is allocated when its type is first touched. A type first touched on a rare
    // step path (a strike, a break, a cut, a tether event) then allocates mid-flight on the flight thread, as DetMath's
    // atan tables did on the first prop strike (fixed in ae332906). The 0 B benches catch that only if their flight hits
    // the path, so this test fails on every such field in the sim core unless it is listed below as load-time only.
    public sealed class StaticInitTests
    {
        // Type full name, field, why the field is touched only before the first step (map loading, JSON parsing, setup).
        // The <>c entries are the compiler's cache of a method's non-capturing lambdas, named by the method's ordinal: an edit
        // to the type can rename one, and the stale check below then asks for the entry to be re-checked.
        static readonly string[,] LoadTimeOnly =
        {
            { "Fpv450.Sim.Contact.ContactSpec", "BladeSegmentEnds", "DroneSpec.BuildContacts' blade edges (setup) runs ContactSpec's type init; ContactModel.AttachRotors (setup) and the blades' placement read it after" },
            { "Fpv450.Sim.Contact.LabelledReader", "Labels", "LabelledReader.Value, the JSON readers' label check (load)" },
            { "Fpv450.Sim.Contact.LegDamage", "Names", "the LegDamage constructor's per-flight strength streams (setup)" },
            { "Fpv450.Sim.Contact.PhysicsMaterials", "AreaLaws", "ReadLayerFields, physics_materials.json's reader (load)" },
            { "Fpv450.Sim.Contact.PhysicsMaterials", "AreaRows", "ReadLayerFields, physics_materials.json's reader (load)" },
            { "Fpv450.Sim.Contact.PhysicsMaterials", "BendingRows", "ReadLayerFields, physics_materials.json's reader (load)" },
            { "Fpv450.Sim.Contact.PropStrikes", "CornerIds", "the PropStrikes constructor's blade-strength draws (setup), which also initialise the type before Apply reads Up" },
            { "Fpv450.Sim.Contact.PropStrikes", "CrownRows", "the PropStrikes constructor's crown rows by material id (setup)" },
            { "Fpv450.Sim.Contact.SurfaceTable", "CoverFields", "ReadCoverSet, under SurfaceTable.Parse (load)" },
            { "Fpv450.Sim.Contact.SurfaceTable", "CoverKinds", "SurfaceTable.Parse and ReadCover (load)" },
            { "Fpv450.Sim.Contact.SurfaceTable", "Levels", "ReadSoil, under SurfaceTable.Parse (load)" },
            { "Fpv450.Sim.Contact.SurfaceTable", "RigidFields", "ReadSoil, under SurfaceTable.Parse (load)" },
            { "Fpv450.Sim.Contact.SurfaceTable", "SoilFields", "ReadSoil, under SurfaceTable.Parse (load)" },
            { "Fpv450.Sim.Flight.DetJson", "Pow10", "DetJson.Number; every DetJson.Parse caller is a file's Parse or the map's load" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "Corner", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "CornerIds", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "End", "Reader.AtCorner, drone.json's corner check (load)" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "PrintMaterials", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "Side", "Reader.AtCorner, drone.json's corner check (load)" },
            { "Fpv450.Sim.Flight.DroneSpec+Reader", "TubeMaterials", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec", "ContactMaterials", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec", "DisarmWords", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.DroneSpec", "Labels", "the drone, materials and tether readers' label check (load)" },
            { "Fpv450.Sim.Flight.DroneSpec", "Materials", "drone.json's reader (load)" },
            { "Fpv450.Sim.Flight.HostClock", "Instance", "the host clock, read by the session's build and the radio's device open, never by a step (StepPurityTests' host-clock rule)" },
            { "Fpv450.Sim.Flight.RotorInterferenceSpec", "BuildLock", "RotorInterferenceSpec.Table, from the Propulsion constructor (setup)" },
            { "Fpv450.Sim.Flight.RotorInterferenceSpec", "Built", "RotorInterferenceSpec.Table, from the Propulsion constructor (setup)" },
            { "Fpv450.Sim.Flight.RotorInterferenceSpec", "Depth", "Integral, only under the RotorInterferenceTable constructor (setup)" },
            { "Fpv450.Sim.Flight.RotorInterferenceSpec", "GaussNodes", "DiscFactor, only under the RotorInterferenceTable constructor (setup)" },
            { "Fpv450.Sim.Flight.RotorInterferenceSpec", "GaussWeights", "DiscFactor, only under the RotorInterferenceTable constructor (setup)" },
            { "Fpv450.Sim.Log.LogFormat", "CornerNames", "LogFormat's field-list builders (its type init, via LogHeader at setup); the step's encoders read only consts" },
            { "Fpv450.Sim.Log.LogFormat", "EndFields", "LogHeader's construction (setup) and the log's readers (replay)" },
            { "Fpv450.Sim.Log.LogFormat", "EventFields", "LogHeader's construction (setup) and the log's readers (replay)" },
            { "Fpv450.Sim.Log.LogFormat", "FcFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogFormat", "Format69EndFields", "the log's readers, for a format-69 log's end layout (replay)" },
            { "Fpv450.Sim.Log.LogFormat", "LooseFields", "FlightReplay's divergence report (replay)" },
            { "Fpv450.Sim.Log.LogFormat", "Magic", "the log's prologue, written by the FlightRecorder constructor (setup) and checked by the readers" },
            { "Fpv450.Sim.Log.LogFormat", "StateFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogFormat", "StepFields", "LogHeader's construction (setup) and the log's readers (replay)" },
            { "Fpv450.Sim.Log.LogFormat", "StickFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogFormat", "TetherTraceFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogFormat", "TraceFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader+<>c", "<>9", "the lambda cache of LogHeader.SettingsFields, the header's write (setup)" },
            { "Fpv450.Sim.Log.LogHeader+<>c", "<>9__147_0", "LogHeader.SettingsFields' sort comparison, the header's write (setup)" },
            { "Fpv450.Sim.Log.LogHeader", "AeroFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "AsymmetryFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "BladeFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "CanopyFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "ContactFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "ContactShapeFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "CoverRowFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "CoverSetFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "CutFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "FlappingFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "FrontFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "GroundEffectFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "InterferenceFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "InternalsFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "LegFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "MaterialRowFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "ModesFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "MotorFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "PackFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "PartFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "PlacementFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "PropsFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "PropulsionFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "RotorFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "RotorWakeFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "RuntimeObjectFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "SetupFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "ShapeGeometryFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "SoilSetFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "SurfaceRowFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "SurfaceTableFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "TetherFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "TetherRowFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "VolumeFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "VortexRingFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "WakeFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "WettingFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Log.LogHeader", "WindFieldsLogged", "read only by the suite's log-coverage tests" },
            { "Fpv450.Sim.Tether.TetherTable", "Blocks", "TetherTable.Parse (load)" },
            { "Fpv450.Sim.Tether.FiberContacts", "RoughMaterials", "the FiberContacts constructor's material classes for the microbend (setup)" },
            { "Fpv450.Sim.Tether.TetherTable", "Fields", "TetherTable.Parse and its derived-value checks (load)" },
            { "Fpv450.Sim.Wind.WakeGridCache", "Lock", "WakeGridCache.Worker, from the WindField constructor (setup): the last flight's wake grid, kept for the next" },
            { "Fpv450.Sim.Wind.WakeGridCache", "_last", "WakeGridCache.Worker, from the WindField constructor (setup): the last flight's wake grid, kept for the next" },
            { "Fpv450.Sim.Wind.HillFlow", "CacheLock", "HillFlow.Of, from FlightWorld.Hills in the FlightCore constructor (setup): the last world's hills, kept for the next flight" },
            { "Fpv450.Sim.Wind.HillFlow", "_last", "HillFlow.Of, from FlightWorld.Hills in the FlightCore constructor (setup): the last world's hills, kept for the next flight" },
            { "Fpv450.Sim.Wind.MapWind+<>c", "<>9", "the lambda cache of MapWind.Strips, read by WindSetup (setup)" },
            { "Fpv450.Sim.Wind.MapWind+<>c", "<>9__7_0", "MapWind.Strips' conversion, read by WindSetup (setup)" },
            { "Fpv450.Sim.World.MapWorld", "MicroKindNames", "ParseElements, the micro-detail sidecars' reader (map load)" },
            { "Fpv450.Sim.World.MapWorld", "SurfaceIds", "the map's load, SurfaceTable.Parse and AddObject's shape check (the FlightCore constructor)" },
            { "Fpv450.Sim.World.MapWorld", "_region", "[ThreadStatic], no type init: the loading thread's is made at the map's load, another thread's (the flight thread's) at its first small StaticContacts or Raycast, the first step; never after" },
        };

        [Test]
        public void NoStaticReferenceFieldOnTheStepPath()
        {
            var allowed = new HashSet<string>();
            for (int i = 0; i < LoadTimeOnly.GetLength(0); i++) allowed.Add(LoadTimeOnly[i, 0] + "." + LoadTimeOnly[i, 1]);

            var found = new HashSet<string>();
            var offenders = new List<string>();
            foreach (var type in typeof(FlightCore).Assembly.GetTypes())
            {
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral || field.FieldType.IsValueType) continue; // const, or no heap object of its own
                    string key = type.FullName + "." + field.Name;
                    found.Add(key);
                    if (!allowed.Contains(key)) offenders.Add(key + " : " + field.FieldType.Name);
                }
            }
            offenders.Sort(StringComparer.Ordinal);

            var stale = new List<string>();
            foreach (var key in allowed) if (!found.Contains(key)) stale.Add(key);
            stale.Sort(StringComparer.Ordinal);

            Assert.That(offenders, Is.Empty, "static reference fields a step may initialise lazily (make them const, " +
                "instance state built at setup, or list them as load-time only with the reason):\n  " + string.Join("\n  ", offenders));
            Assert.That(stale, Is.Empty, "load-time entries that no longer exist:\n  " + string.Join("\n  ", stale));
        }
    }
}
