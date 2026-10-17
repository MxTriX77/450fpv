# Review: setup-sim-tools

**Reviewer:** qa-engineer · **Task:** 2.1 · **Branch:** `unity/physics/setup-sim-tools` · **Range:** `13f598c..cc33d66` (4 commits, 13 files)
**Requirements cited:** PLT-01, PLT-04 (groundwork), PRC-10 · **Decisions:** D-005, D-006, D-019

Every check below was re-run by QA in a fresh worktree; the author's output was not used. Paths in the output are shortened to `<repo>`.

## Environment

```
$ dotnet --version
10.0.301
$ dotnet --list-sdks
10.0.301 [C:\Program Files\dotnet\sdk]
$ Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"   # is an editor running?
ProcessId   : 28136
CommandLine : "...\UnityTechnologies.UnityHub_3.22.3...\app\resources\unity.exe" serve
```
Only Unity Hub's `serve` helper was running; no Unity editor. Unity 6000.3.26f1 is installed, so its built-in test packages could be read (below), but no Unity compile or test run was done.

## Spec scenarios

### Requirement: Engine-free package

**Scenario: Runtime assembly has no engine references: PASS**
```
$ python -c "import json; d=json.load(open('unity/Packages/com.fpv450.sim/Runtime/Fpv450.Sim.asmdef')); ..."
noEngineReferences = True
references = []
precompiledReferences = []
overrideReferences = False
autoReferenced = True
```

**Scenario: No Unity namespace in runtime sources: PASS**
```
$ grep -rn "using Unity" unity/Packages/com.fpv450.sim/Runtime
grep exit=1 (1 = no matches)
$ grep -rnE "UnityEngine|UnityEditor" unity/Packages/com.fpv450.sim/Runtime --include=*.cs
grep exit=1
```

### Requirement: Same sources outside Unity

**Scenario: Tests pass without Unity: PASS**
From an empty `bin/`/`obj/` (fresh restore), no editor running:
```
$ dotnet test tools/simtests
  Restored <repo>\tools\simcore\simcore.csproj (in 272 ms).
  Restored <repo>\tools\simtests\simtests.csproj (in 785 ms).
  simcore -> <repo>\tools\simcore\bin\Debug\netstandard2.1\Fpv450.Sim.dll
  simtests -> <repo>\tools\simtests\bin\Debug\net10.0\Fpv450.Sim.Tests.dll
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 62 ms - Fpv450.Sim.Tests.dll (net10.0)
EXIT=0
```
Warnings are errors, checked two ways:
```
$ dotnet msbuild tools/simtests/simtests.csproj -getProperty:TreatWarningsAsErrors -getProperty:LangVersion -getProperty:ImplicitUsings
"LangVersion": "9.0", "ImplicitUsings": "", "TreatWarningsAsErrors": "true"

# probe: Runtime/QaProbe/Deep/QaProbe.cs with an unused local
$ dotnet build tools/simcore -c Release
<repo>\unity\Packages\com.fpv450.sim\Runtime\QaProbe\Deep\QaProbe.cs(7,17): error CS0168: The variable 'unused' is declared but never used
    1 Error(s)
```
The test is not vacuous. A mutant `DetMath.Sqrt` returning one ulp above `Math.Sqrt` fails all three cases:
```
  Failed Sqrt_IsCorrectlyRounded(4.0d,4611686018427387904L)   Expected: 4611686018427387904  But was: 4611686018427387905
  Failed Sqrt_IsCorrectlyRounded(2.0d,4609047870845172685L)   Expected: 4609047870845172685  But was: 4609047870845172686
  Failed Sqrt_IsCorrectlyRounded(3.0d,4610479282544200874L)   Expected: 4610479282544200874  But was: 4610479282544200875
Failed!  - Failed:     3, Passed:     0, Skipped:     0, Total:     3
```
The expected bit patterns were checked independently, against a 60-digit decimal square root:
```
4.0 0x4000000000000000 True err/ulp=0.000
2.0 0x3ff6a09e667f3bcd True err/ulp=0.435
3.0 0x3ffbb67ae8584caa True err/ulp=0.452
```
Each is within half an ulp of the exact root, so each is the correctly rounded double.

**Scenario: A new runtime file is picked up automatically: PASS**
QA added a new file two folders deep, `Runtime/QaProbe/Deep/QaProbe.cs`, which declares `Fpv450.Sim.QaProbe.QaProbeMarker7f3c`. No project file was edited:
```
$ git status --porcelain
?? unity/Packages/com.fpv450.sim/Runtime/QaProbe/
$ dotnet build tools/simcore -c Release
Build succeeded.    0 Warning(s)    0 Error(s)
$ grep -a -c "QaProbeMarker7f3c" tools/simcore/bin/Release/netstandard2.1/Fpv450.Sim.dll
1
# probe removed, rebuilt
$ grep -a -c "QaProbeMarker7f3c" tools/simcore/bin/Release/netstandard2.1/Fpv450.Sim.dll
0
```
The compile set comes straight from the package, never from a copy:
```
$ dotnet msbuild tools/simcore/simcore.csproj -getItem:Compile
"Identity": "..\\..\\unity\\Packages\\com.fpv450.sim\\Runtime\\Math\\DetMath.cs"   (the only item)
```

### Requirement: API surface Unity accepts

**Scenario: Core library targets netstandard2.1: PASS**
```
$ dotnet build tools/simcore -c Release
  simcore -> <repo>\tools\simcore\bin\Release\netstandard2.1\Fpv450.Sim.dll
Build succeeded.    0 Warning(s)    0 Error(s)
EXIT=0
$ dotnet msbuild tools/simcore/simcore.csproj -p:Configuration=Release -getProperty:TargetFramework -getProperty:LangVersion ...
"TargetFramework": "netstandard2.1", "LangVersion": "9.0", "TreatWarningsAsErrors": "true", "Deterministic": "true", "Nullable": "disable"
$ grep -a -o ".NETStandard,Version=v2.1" tools/simcore/bin/Release/netstandard2.1/Fpv450.Sim.dll
.NETStandard,Version=v2.1
```
The guard works. Two probes in `Runtime/` fail the build as intended:
```
# C# 10 file-scoped namespace
QaProbe.cs(1,1): error CS8773: Feature 'file-scoped namespace' is not available in C# 9.0. Please use language version 10.0 or greater.
# APIs present on net10.0 but absent from netstandard2.1
QaProbe.cs(5,40): error CS0234: The type or namespace name 'BitOperations' does not exist in the namespace 'System.Numerics'
QaProbe.cs(6,77): error CS0117: 'Math' does not contain a definition for 'FusedMultiplyAdd'
```
So the explicit FMA that DetMath bans can't reach the core by accident either.

### Requirement: Bench entry points exist

**Scenario: Worldbench golden with no cases: PASS**
```
$ dotnet run -c Release --project tools/worldbench -- --golden
worldbench: 0 golden cases registered
EXIT=0
```
The rest of the requirement:
```
$ dotnet run -c Release --project tools/worldbench -- --bench
worldbench: 0 bench cases registered
EXIT=0
$ dotnet run -c Release --project tools/flightbench -- --golden
flightbench: 0 golden cases registered
EXIT=0
$ dotnet run -c Release --project tools/flightbench -- --bench
flightbench: 0 bench cases registered
EXIT=0
```

**Scenario: Unknown argument: PASS**
```
$ dotnet run -c Release --project tools/worldbench -- --frobnicate
usage: worldbench --golden | --bench
EXIT=2
$ dotnet run -c Release --project tools/worldbench
usage: worldbench --golden | --bench
EXIT=2
$ dotnet run -c Release --project tools/flightbench -- --frobnicate
usage: flightbench --golden | --bench
EXIT=2
$ dotnet run -c Release --project tools/flightbench
usage: flightbench --golden | --bench
EXIT=2
```

**All 7 scenarios: PASS.**

## Design conformance

- Layout matches design §1 file for file. The Runtime asmdef has `rootNamespace Fpv450.Sim`, `noEngineReferences true` and `autoReferenced true`. `package.json` has name `com.fpv450.sim`, displayName "450fpv Sim Core" and `unity "6000.3"`. `Directory.Build.props` sets LangVersion 9.0, Nullable disable, Deterministic true and TreatWarningsAsErrors true. `simcore` targets netstandard2.1, and the other three tools target net10.0 and reference simcore.
- §2: one test tree, compiled by `simtests` from the package's `Tests/`; no NUnit tests live under `tools/`.
- §4: the DetMath header documents everything required: IEEE basic operations only, no transcendental `System.Math`, no MathF, FMA, `System.Numerics` or intrinsics, the D-019 frame and the cross-product rule. QA checked the stated conventions against Unity's maths:
  - `X × Y = Z` holds with the component formula given.
  - A positive rotation about +Y takes north (+Z) to east (+X); in Unity, `Euler(0,90,0)·fwd = right`.
  - About body +x it takes the nose down; `Euler(90,0,0)·fwd = (0,−1,0)`.
  - About body +z it takes the right side up; `Euler(0,0,90)·right = up`.
  - Viewed from the +Y tip, east is right and north is up, so the "clockwise" wording is consistent.
  - The cited `knowledge.md §6.10` exists.
- §5: the stubs print "0 … cases registered" and exit 0; anything else prints usage to stderr and exits 2.
- Not applicable here: realism or geometry (no asset or view change), statistics, performance (PLT-07: the bench stubs measure nothing) and goldens (none exist, none re-recorded).

## Unity 6 Tests asmdef

The Tests asmdef was compared with the Unity Test Framework that ships in the installed 6000.3.26f1 (`com.unity.test-framework` 1.6.0, `com.unity.ext.nunit` 2.0.5). Its EditMode sample `Samples~/15_TestCases/Tests_15/Tests_15.asmdef` has the same shape:

| Field | `Fpv450.Sim.Tests.asmdef` | Unity 6.3 sample |
|---|---|---|
| references | `Fpv450.Sim`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` | `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, the code under test |
| includePlatforms | `Editor` | `Editor` |
| overrideReferences / precompiledReferences | `true` / `nunit.framework.dll` | `true` / `nunit.framework.dll` |
| autoReferenced | `false` | `false` |
| defineConstraints | `UNITY_INCLUDE_TESTS` | `UNITY_INCLUDE_TESTS` |

`com.unity.ext.nunit` ships `net40/unity-custom/nunit.framework.dll`, so the precompiled reference name resolves. **Plausible: PASS.**

**Only the Unity run can prove these** (the job of `setup-unity-skeleton`, per design Risks):
1. Unity compiles `Fpv450.Sim` under `noEngineReferences` and `Fpv450.Sim.Tests` with the TestRunner and NUnit references, at Unity's own C# 9 and .NET Standard 2.1 profile.
2. EditMode lists and passes `DetMathTests` on Mono. That is also the first cross-runtime bit check of `Sqrt` (CoreCLR here, Mono there).
3. Unity accepts `"unity": "6000.3"` in `package.json` for 6000.3.26f1.
4. Embedded-package tests appear in the Test Runner without a `testables` entry in `Packages/manifest.json`. If they don't, add one.
5. Unity will generate `.meta` files for the package on first import. They must be committed once, or every clone mints new GUIDs.

## Hygiene

| Check | Result |
|---|---|
| Base | `git merge-base origin/unity/main HEAD` = `13f598c` = `origin/unity/main`: PASS |
| Tracked files | the 13 intended files only; `git ls-files` has no `bin/`, `obj/`, `Library/`, `Temp/`, `Logs/`, `UserSettings/` or `reference/`: PASS |
| Build outputs ignored | `git status --porcelain --ignored` shows `tools/*/bin/` and `tools/*/obj/` as `!!` only: PASS |
| Whitespace | `git diff --check 13f598c..HEAD` exit 0: PASS |
| Commit subjects | below; all `type(scope): summary`, lower case, at most 72 characters, empty body, no trailer, no co-author: PASS |
| Author and committer | all four are `Physics Engineer <physics@450fpv.local>`: PASS |
| One logical step per commit | package / core and test projects / benches / task ticks: PASS |
| OPSEC | no places, coordinates, dates, OSD values or reference file names in the diff (grep exit 1): PASS |
| Licences | NuGet build-time dependencies are pinned to exact versions and all MIT (NUnit 3.14.0 from LICENSE.txt, NUnit3TestAdapter 5.0.0 and Microsoft.NET.Test.Sdk 17.14.1 from their nuspec `MIT` expressions). They are not shipped asset files, so `tools/assets/manifest.json` doesn't apply (D-003 covers assets): PASS |
| Spec | `openspec validate setup-sim-tools --strict`: "Change 'setup-sim-tools' is valid": PASS |

```
$ git log --format='%h|%an <%ae>|%cn <%ce>|%s|body=[%b]' 13f598c..HEAD
cc33d66|Physics Engineer <physics@450fpv.local>|Physics Engineer <physics@450fpv.local>|docs(openspec): tick setup-sim-tools tasks 1.1 and 1.2|body=[]
71c0baf|Physics Engineer <physics@450fpv.local>|Physics Engineer <physics@450fpv.local>|chore(tools): add worldbench and flightbench entry points|body=[]
39520af|Physics Engineer <physics@450fpv.local>|Physics Engineer <physics@450fpv.local>|chore(tools): add simcore and simtests projects over the sim package|body=[]
56bab06|Physics Engineer <physics@450fpv.local>|Physics Engineer <physics@450fpv.local>|feat(sim): add engine-free sim core package with detmath|body=[]
$ git log --format='%s' 13f598c..HEAD | awk '{ printf "%3d  %s\n", length($0), $0 }'
 54  docs(openspec): tick setup-sim-tools tasks 1.1 and 1.2
 57  chore(tools): add worldbench and flightbench entry points
 68  chore(tools): add simcore and simtests projects over the sim package
 56  feat(sim): add engine-free sim core package with detmath
```

All probes and the mutant were reverted before this review was committed: `git status --porcelain --untracked-files=all` was empty, and `Runtime/` holds only `Fpv450.Sim.asmdef` and `Math/`.

## Requirements

- **D-006: met.** One embedded package with `noEngineReferences`, and the .NET tools compile the very same files. Proven by the new-file probe and the compile item list.
- **D-019: met** for this phase. The frame, handedness and cross-product rule are documented once, in DetMath, and checked above.
- **PLT-01: partly met** (expected for this change). The C# sim core and .NET tooling are in place. Unity 6 LTS, URP and the Unity-side compile come with `setup-unity-skeleton`.
- **PLT-04: partly met** (groundwork only, as cited). The determinism rules are documented, and a correctly rounded `Sqrt` is tested bit-exact. The guards against FMA and newer APIs are proven to work. The 1 kHz, p99 and zero-allocation budgets belong to later phases.
- **PRC-10: met** by this review (commands and output above). The author's handoff wasn't part of this brief, so QA did not judge it.

## Suggestions (non-blocking, for the owner or orchestrator)

1. **No `global.json`.** The "pinned .NET SDK" exists only as a measurement. A `global.json` pinning 10.0.301 (`rollForward: latestPatch`) would make the pin enforceable. It belongs with the phase-0 `docs/environment.md` work, which doesn't exist on this branch yet.
2. **NuGet audit can break the build with no code change.** `NuGetAudit=true` and `NuGetAuditMode=all` meet `TreatWarningsAsErrors=true`. A future advisory against any pinned or transitive test package (NU1901–NU1904) would then fail `dotnet test`. Today: "no vulnerable packages". Consider `WarningsNotAsErrors NU1901;NU1902;NU1903;NU1904`, or record the choice as a decision.
3. **Default compile globbing is still on** (`EnableDefaultCompileItems=true`) in `simcore` and `simtests`. A stray `.cs` under `tools/simcore/` would build into the .NET core but never into Unity, quietly breaking "never from a copy". `EnableDefaultCompileItems=false` in both projects would close that.
4. **NUnit drift.** `simtests` uses NUnit 3.14.0, while Unity runs its custom NUnit 3.5. A test using a newer NUnit API would pass under `dotnet` and fail to compile in Unity. The EditMode run in `setup-unity-skeleton` is the gate. Keep tests to the NUnit 3.5 surface until then.

Verdict: APPROVED
