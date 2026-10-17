# Design: setup-sim-tools

## Context
D-006 fixes the shape: one embedded UPM package, `noEngineReferences: true`, and .NET tools compiling the same files. The .NET SDK on the dev machine is 10.0.301 (measured, `dotnet --list-sdks`). Unity 6 compiles C# 9 against .NET Standard 2.1 (published, Unity 6 manual).

## Goals / Non-Goals
**Goals:** the folder layout every later sim change follows; a build that catches Unity-incompatible APIs outside Unity.
**Non-Goals:** any physics; `tools/replay`.

## Decisions
1. **Layout.**
   ```
   unity/Packages/com.fpv450.sim/
     package.json                       name com.fpv450.sim, displayName "450fpv Sim Core", unity "6000.3"
     Runtime/Fpv450.Sim.asmdef          rootNamespace Fpv450.Sim, noEngineReferences true, autoReferenced true
     Runtime/Math/DetMath.cs
     Tests/Fpv450.Sim.Tests.asmdef      Editor-only, references Fpv450.Sim + Unity test runner, nunit precompiled ref
     Tests/DetMathTests.cs
   tools/Directory.Build.props          LangVersion 9.0, Nullable disable, Deterministic true, TreatWarningsAsErrors true
   tools/simcore/simcore.csproj         netstandard2.1, <Compile Include="../../unity/Packages/com.fpv450.sim/Runtime/**/*.cs" />
   tools/simtests/simtests.csproj       net10.0, NUnit, refs simcore, <Compile Include=".../Tests/**/*.cs" />
   tools/worldbench/worldbench.csproj   net10.0 console, refs simcore
   tools/flightbench/flightbench.csproj net10.0 console, refs simcore
   ```
   Other runtime folders (`World/`, `Flight/`, `Contact/`, `Wind/`, `Tether/`, `Log/`) are created by the changes that fill them, not now.
2. **One test tree, two runners.** Tests are written in NUnit, which both Unity's test framework and `dotnet test` run. The package `Tests/` folder is the single source; `simtests` compiles it. *Alternative:* xunit in `tools/` only. Rejected: two test trees drift, and Unity EditMode couldn't run them.
3. **netstandard2.1 core library.** `simcore` targets `netstandard2.1`, so an API that Unity lacks fails the .NET build too. The tools target `net10.0` and reference it.
4. **DetMath now holds only rules and one trivial operation** (for example `DetMath.Sqrt` wrapping `Math.Sqrt`, which is IEEE-exact) with its test. The file header documents: IEEE basic operations only (+ − × ÷ √), no `Math.Sin/Cos/Exp/Pow`, no `MathF`/FMA intrinsics, the D-019 frame (+X east, +Y up, +Z north, left-handed) and the cross-product convention. The real port of the Godot `DetMath` comes with phase 3.
5. **Bench stubs.** `--golden` and `--bench` print "0 cases registered" and exit 0; anything else prints usage and exits 2. They make the `CLAUDE.md` commands real without inventing cases.

## Risks / Trade-offs
- [Unity's test asmdef needs engine test-runner references] → only the Tests asmdef has them; Runtime stays engine-free. The skeleton change confirms the package tests run in EditMode.
- [.NET 10 SDK vs Unity's compiler differ in edge cases] → `LangVersion 9.0` plus `netstandard2.1` cover the usual breakages; the Unity compile in `setup-unity-skeleton` is the final check.
