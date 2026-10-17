# Review: setup-unity-skeleton

**Reviewer:** qa-engineer · **Task:** 2.1 · **Branch:** `unity/game/setup-unity-skeleton` · **Range:** `3d67b8e..00cebbc` (9 commits, 102 files)
**Requirements cited:** PLT-01, PLT-02 (quality presets groundwork), PRD-07 (noclip stub) · **Decisions:** D-001, D-005, D-006, D-017, D-019

Every check below was re-run by QA in a fresh worktree with no `Library/` (so the first EditMode run was also the project's first import). The author's output was not used. `$U` is `C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe`; every Unity call was piped (`| Out-File`) so PowerShell waited and `$LASTEXITCODE` is real. Paths are shortened to `<repo>`; logs stay in the local scratchpad.

## Environment

```
$ Get-Process Unity          # is an editor running?
28136 ...\UnityTechnologies.UnityHub_3.22.3...\app\resources\unity.exe     (Unity Hub only, no editor)
$ Test-Path unity/Library
False                         # fresh checkout
```
Unity 6000.3.26f1 (changeset `de8167e7440c`), Personal licence. The licence log shows the known harmless `Code 10 while verifying Licensing Client signature`, then `Successfully resolved entitlement details`.

## Spec scenarios

### Requirement: Pinned editor and pipeline

**Scenario: Version pinned: PASS**
```
$ cat unity/ProjectSettings/ProjectVersion.txt
m_EditorVersion: 6000.3.26f1
m_EditorVersionWithRevision: 6000.3.26f1 (de8167e7440c)
```

**Scenario: Forward+ at every level: PASS**

Each quality level traced through its pipeline asset to its renderer (GUIDs resolved from the `.meta` files):
```
$ python -I -c "<trace QualitySettings -> URP asset -> renderer>"
levels: ['Low', 'Medium', 'High'] | m_CurrentQuality: 2 | Standalone default: 2 | graphics default pipeline: URP-High.asset
Low     -> URP-Low.asset    -> URP-Low-Renderer.asset    m_RenderingMode: 2
Medium  -> URP-Medium.asset -> URP-Medium-Renderer.asset m_RenderingMode: 2
High    -> URP-High.asset   -> URP-High-Renderer.asset   m_RenderingMode: 2
```
`2` is Forward+ in the URP that ships with this editor (17.3.0):
```
$ grep -A8 "public enum RenderingMode" "<editor>/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs"
        Forward = 0,
        [InspectorName("Forward+")]
        ForwardPlus = 2,
        Deferred = 1,
```

### Requirement: Three quality levels

**Scenario: Quality levels present: PASS**

Same trace: exactly `Low, Medium, High` in that order, each with its own URP asset; `m_PerPlatformDefaultQuality` → `Standalone: 2` (High; every other platform is 2 as well) and `m_CurrentQuality: 2`. `GraphicsSettings.m_CustomRenderPipeline` is `URP-High`.

### Requirement: Folder and assembly layout

**Scenario: Layout exists in git: PASS**

From `git ls-files` of this fresh worktree:
```
Art            meta=True  files=['README.md']
Prefabs        meta=True  files=['README.md']
Scenes         meta=True  files=['Boot.unity']
Scripts        meta=True  files=[]            (subfolders only)
Scripts/App    meta=True  files=['Boot.cs', 'Fpv450.App.asmdef']
Scripts/Input  meta=True  files=['Fpv450.Input.asmdef', 'NoclipCamera.cs']
Scripts/UI     meta=True  files=['Fpv450.UI.asmdef', 'README.md']
Scripts/Feed   meta=True  files=['Fpv450.Feed.asmdef', 'README.md']
Scripts/World  meta=True  files=['Fpv450.World.asmdef', 'README.md']
Shaders        meta=True  files=['README.md']
Settings       meta=True  files=[3 URP assets, 3 renderers, DefaultVolumeProfile, UniversalRenderPipelineGlobalSettings]
Editor         meta=True  files=['Build.cs', 'Fpv450.Editor.asmdef', 'ProjectSetup.cs']
Tests          meta=True  files=[]            (Tests/EditMode holds the asmdef and NoclipCameraTests.cs)
```
Every folder without other content has its one-line `README.md`. The meta check over all 227 tracked files: 50 `.meta` files, **no missing meta** for any tracked file or folder under `Assets/` or the sim package, no orphan meta, no duplicate GUID.

**Scenario: Dependency direction: PASS**
```
$ cat unity/Packages/com.fpv450.sim/Runtime/Fpv450.Sim.asmdef        # references: [], noEngineReferences: true
$ python -I rspcheck.py unity/Library/Bee/artifacts/<dag>/Fpv450.Sim.rsp      # Unity's compiler command line
== Fpv450.Sim.rsp
  langversion: ['9.0']
  defines: ENABLE_INPUT_SYSTEM ENABLE_MONO NET_STANDARD NET_STANDARD_2_0 NET_STANDARD_2_1 UNITY_6000_3_26 UNITY_EDITOR UNITY_INCLUDE_TESTS
  references (125): Microsoft.Win32.Primitives.dll Mono.Cecil.dll Unity.Collections.LowLevel.ILSupport.dll nunit.framework.dll   (plus System.*, mscorlib, netstandard)
$ PEReader over Library/ScriptAssemblies/*.dll (assembly references as compiled)
Fpv450.Sim       -> netstandard
Fpv450.Sim.Tests -> Fpv450.Sim, mscorlib, nunit.framework, System
Fpv450.Input     -> netstandard, Unity.InputSystem, UnityEngine.CoreModule
Fpv450.App       -> netstandard, UnityEngine.CoreModule
```
The compiled `Fpv450.Sim.dll` references `netstandard` and nothing else.

### Requirement: Boot scene and Windows build

**Scenario: Batchmode build succeeds: PASS**
```
$ & $U -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -logFile - | Out-File build-ok.log
exit=0 seconds=162
Build Finished, Result: Success.
[Build] Windows: Succeeded, 0 errors, 3 warnings, 99884044 bytes in 00:02:20.9704175
Exiting batchmode successfully now!
$ ls unity/Builds/Windows
450fpv.exe  450fpv_BurstDebugInformation_DoNotShip  450fpv_Data  D3D12  MonoBleedingEdge  UnityCrashHandler64.exe  UnityPlayer.dll
$ ls unity/Builds/Windows/450fpv_Data | grep -E '^level'
level0                         # one scene only: Boot
```
The 3 warnings are Unity's "will not be compiled, because it has no scripts" for the empty `Fpv450.World`, `Fpv450.Feed` and `Fpv450.UI` asmdefs. `git status` after the build: clean.

**Scenario: Build failure fails the command: PASS**

Probe: `Assets/_Project/Scripts/World/QaBuildBreak.cs` with a type error inside `#if !UNITY_EDITOR`, so the editor compiles, `Fpv450.Build.Windows` runs, and only the player compile fails. That exercises the entry point's own failure path rather than Unity's "scripts have compiler errors" abort.
```
$ & $U -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -logFile - | Out-File build-fail.log
exit=1 seconds=31
Assets\_Project\Scripts\World\QaBuildBreak.cs(7,32): error CS0029: Cannot implicitly convert type 'string' to 'int'
Build Finished, Result: Failure.
[Build] Windows: Failed, 2 errors, 2 warnings, 0 bytes in 00:00:06.5550262
```
After the failed build, `git status --porcelain --untracked-files=all` showed the probe and these leftovers:
```
?? unity/Assets/Resources.meta
?? unity/Assets/Resources/PerformanceTestRunInfo.json
?? unity/Assets/Resources/PerformanceTestRunInfo.json.meta
?? unity/Assets/Resources/PerformanceTestRunSettings.json
?? unity/Assets/Resources/PerformanceTestRunSettings.json.meta
?? unity/Assets/_Project/Scripts/World/QaBuildBreak.cs
?? unity/Assets/_Project/Scripts/World/QaBuildBreak.cs.meta
```
All seven were deleted by explicit path, and the tree was clean again. See suggestion 1.

**Scenario: Player opens the Boot scene: PASS**
```
$ pwsh smoke.ps1   # Start-Process 450fpv.exe -batchmode -nographics -smoketest -logFile smoke.log; WaitForExit(30 s)
args=[-batchmode -nographics -smoketest] exited=True exit=0 seconds=14.82     (cold: first start after the build)
  log: [Boot] Boot scene loaded
args=[-batchmode -nographics] exited=False (still running after 10 s, killed)  (negative control)
  log: [Boot] Boot scene loaded
$ pwsh smoke.ps1   # again, warm
args=[-batchmode -nographics -smoketest] exited=True exit=0 seconds=3.21
  log: [Boot] Boot scene loaded
args=[-batchmode -nographics] exited=False (still running after 10 s, killed)
```
The control shows that the exit comes from the `-smoketest` switch and not from a crash or an empty scene list. The cold start spent 10.35 s in "Loaded All Assemblies" (0.26 s warm); see risk 2.

### Requirement: EditMode tests run in batchmode

**Scenario: EditMode run passes: PASS**
```
$ & $U -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-File editmode1.log
exit=0 seconds=171                      (first import of the fresh checkout)
Test run completed. Exiting with code 0 (Ok). Run completed.
$ python -I -c "<parse unity/Logs/editmode.xml>"
{'testcasecount': '4', 'result': 'Passed', 'total': '4', 'passed': '4', 'failed': '0', 'skipped': '0', 'engine-version': '3.5.0.0', 'clr-version': '4.0.30319.42000'}
assembly Fpv450.Sim.Tests.dll Passed 3
assembly Fpv450.Tests.EditMode.dll Passed 1
Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded(4.0d,4611686018427387904L) Passed
Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded(2.0d,4609047870845172685L) Passed
Fpv450.Sim.Tests.DetMathTests.Sqrt_IsCorrectlyRounded(3.0d,4610479282544200874L) Passed
Fpv450.Tests.EditMode.NoclipCameraTests.Step_DistanceIsFrameRateIndependent Passed
```
Re-run at the end, after every probe and mutant had been reverted:
```
$ & $U -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-File editmode-final.log
final editmode exit=0 seconds=26
{'result': 'Passed', 'total': '4', 'passed': '4', 'failed': '0'}     (the same 4 cases, all Passed)
```

### Requirement: Noclip camera stub

**Scenario: Speed is frame-rate independent: PASS**

`NoclipCameraTests.Step_DistanceIsFrameRateIndependent` moves for 1 s at 30 and at 144 fps with the same input. It asserts the 30 fps distance equals speed × 1 s within 1 %, and the 144 fps distance equals the 30 fps one within 1 % (passed above).

The test must fail a known-wrong model. Mutant: a frame-locked step, `speed * dt` replaced by `speed * (1f / 60f)` in `NoclipCamera.Step`:
```
$ & $U ... -runTests -testPlatform EditMode -testResults Logs/editmode-mutant.xml ...
exit=2 seconds=32
{'result': 'Failed(Child)', 'total': '4', 'passed': '3', 'failed': '1'}
Fpv450.Tests.EditMode.NoclipCameraTests.Step_DistanceIsFrameRateIndependent Failed Expected: 20.0f +/- 1 Percent |   But was:  9.99999809f
```
The mutant was reverted with the same edit in reverse, and `git status` was clean afterwards. The test covers the pure `Step` function, as design decision 7 says. That `Update()` feeds it `Time.unscaledDeltaTime` is checked by reading the code (`NoclipCamera.cs`, `Update`), not by a test.

The controls match PRD-07 and the spec: mouse look (pitch clamped to ±89°), WASD, Shift ×4, E/Space up, Q/Ctrl down, and the wheel ×1.25 per notch, clamped to 0.5–200 m/s.

## Design decisions (not scenarios, checked by reading the settings)

| Decision | Result |
|---|---|
| 1. Settings re-derived by `ProjectSetup.Apply`, idempotent | met: `& $U -batchmode -nographics -quit -projectPath unity -executeMethod Fpv450.EditorSetup.ProjectSetup.Apply -logFile -` gave `apply exit=0 seconds=17` and `[ProjectSetup] Applied`, and `git status --porcelain --untracked-files=all` was empty afterwards, so the committed settings are exactly what the script derives |
| 2. Packages: URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0; no Collaborate, Ads, Analytics, Timeline, Visual Scripting or multiplayer packages | met. `packages-lock.json`: 42 builtin, 7 registry (all `https://packages.unity.com`), 1 embedded (`com.fpv450.sim`) |
| 3. Player settings | met: `companyName`/`productName` `450fpv`, `m_ActiveColorSpace: 1` (linear), `scriptingBackend Standalone: 0` (Mono), `activeInputHandler: 1` (Input System only), `m_SerializationMode: 2` (Force Text), `m_Mode: Visible Meta Files`. .NET Standard 2.1 is proven by the runtime assemblies' compile defines (`NET_STANDARD_2_1`, above) |
| 4. Mesh LOD off "where a project setting exists" | met: `grep -i lod unity/ProjectSettings/*.asset` finds only `lodBias`, `maximumLODLevel`, `enableLODCrossFade` and `meshLodThreshold: 1` (a selection threshold per quality level, left at its default). No Mesh LOD generation switch is serialised in any project settings file, so the generation choice lives in each model importer, which the asset pipeline owns |
| 5. Assemblies | met: the seven asmdefs named in the design, `Fpv450.Editor` and `Fpv450.Tests.EditMode` Editor-only; `Fpv450.Build.Windows` resolves as `-executeMethod` (build scenario) |
| 6. Boot scene | met: `Main Camera` (Camera, URP camera data, AudioListener, `NoclipCamera`, `Boot`) and `Sun` (directional light, URP light data), nothing else; the only build scene; output under `unity/Builds/Windows/` (git-ignored) |
| 7. Noclip stub | met (above) |
| Risk: second open diff-free | met: `git status --porcelain --untracked-files=all` was empty after each of the five Unity opens (first import with EditMode run, build, `ProjectSetup.Apply`, final EditMode run, and the mutant run once reverted). The only exception was the failed build (suggestion 1) |

## Risks left open by `setup-sim-tools` (what only Unity could prove)

1. **Unity compiles `Fpv450.Sim` under `noEngineReferences`, and `Fpv450.Sim.Tests` with TestRunner and NUnit, at C# 9 and .NET Standard 2.1: closed.** The `.rsp` shows `-langversion:9.0` and `NET_STANDARD_2_1` for `Fpv450.Sim`, and the compiled DLL references only `netstandard`.
2. **EditMode lists and passes `DetMathTests` on Mono: closed.** The three `Sqrt_IsCorrectlyRounded` cases pass in the Unity Child Domain (Mono, `clr-version 4.0.30319.42000`, Unity NUnit 3.5.0.0). The bit patterns match the CoreCLR run, so the first cross-runtime check of `Sqrt` agrees.
3. **Unity accepts `"unity": "6000.3"`: closed.** The package registers as `com.fpv450.sim@file:...` with no Package Manager warning or error.
4. **Embedded-package tests appear without `testables`: closed.** `Fpv450.Sim.Tests.dll` is in the results, and `manifest.json` has no `testables` entry.
5. **Package `.meta` files committed once: closed.** All eight package metas are tracked: the folders `Runtime`, `Runtime/Math` and `Tests`, both asmdefs, `DetMath.cs`, `DetMathTests.cs` and `package.json`. A fresh import created none.

## Hygiene

| Check | Result |
|---|---|
| Base | `git merge-base origin/unity/main HEAD` = `3d67b8e` = `origin/unity/main`: PASS |
| Tracked files | 102 changed files: `unity/Assets/_Project/**`, the sim package's metas, `Packages/manifest.json` and `packages-lock.json`, 22 `ProjectSettings` files, and `tasks.md`. Nothing under `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/`, `obj/`, `bin/` or `reference/`: PASS |
| `.meta` files | present for every tracked asset and folder; no orphans; no duplicate GUIDs: PASS |
| Churn | `git status --porcelain --untracked-files=all` empty after the first import, the build, the probe and mutant reverts, `ProjectSetup.Apply` and the final EditMode run (and before this `review.md` was written): PASS |
| Whitespace | `git diff --check` flags trailing spaces only in Unity-serialised `.meta`, `.asset` and `.unity` files (for example `userData: `). Unity writes them that way, so normalising them would churn. The hand-written `.cs`, `.md`, `.json` and `.asmdef` files are clean: PASS |
| Commit subjects | below; all `type(scope): summary`, lower case, at most 67 characters, empty body, no trailer, no co-author: PASS |
| Author and committer | all nine are `Game Developer <gamedev@450fpv.local>` (team.md): PASS |
| One logical step per commit | create project / layout / noclip + test / boot component / setup script / boot scene / build entry / URP first-build settings / task tick: PASS |
| OPSEC | no places, coordinates, sortie dates, OSD values or reference file names in the diff; `cloudProjectId`, `projectName` and `organizationId` empty, Unity Connect services disabled: PASS |
| Licences | no third-party asset files; the packages are Unity's own from `packages.unity.com`, pinned by `manifest.json` and `packages-lock.json`; `tools/assets/manifest.json` (D-003) doesn't apply: PASS |
| Spec | `openspec validate setup-unity-skeleton --strict`: "Change 'setup-unity-skeleton' is valid": PASS |

```
$ git log --format='%h|%an <%ae>|%cn <%ce>|%s|body=[%b]' 3d67b8e..HEAD
00cebbc|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|docs(openspec): tick setup-unity-skeleton task 1.1|body=[]
745174f|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|chore(unity): record the settings urp writes on its first build|body=[]
3d380f7|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|chore(build): add the fpv450.build.windows batchmode build|body=[]
db2cfb6|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|feat(app): add the boot scene as the only scene in the build|body=[]
ebe9feb|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|chore(unity): add the project setup script with urp forward+ levels|body=[]
073924c|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|feat(app): add the boot component with a smoke-test switch|body=[]
e3d4c30|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|feat(input): add the noclip camera stub with a frame-rate test|body=[]
99af71b|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|chore(unity): add the project folder and assembly layout|body=[]
821d23b|Game Developer <gamedev@450fpv.local>|Game Developer <gamedev@450fpv.local>|chore(unity): create the project pinned to 6000.3.26f1 with urp|body=[]
$ git log --format='%s' 3d67b8e..HEAD | awk '{ printf "%3d  %s\n", length($0), $0 }'
 50  docs(openspec): tick setup-unity-skeleton task 1.1
 63  chore(unity): record the settings urp writes on its first build
 58  chore(build): add the fpv450.build.windows batchmode build
 60  feat(app): add the boot scene as the only scene in the build
 67  chore(unity): add the project setup script with urp forward+ levels
 58  feat(app): add the boot component with a smoke-test switch
 62  feat(input): add the noclip camera stub with a frame-rate test
 56  chore(unity): add the project folder and assembly layout
 63  chore(unity): create the project pinned to 6000.3.26f1 with urp
```

This is not an asset or view change, so the realism checks (REAL-03 side-by-side, REAL-05 geometry audit, review sheets) don't apply. It is not a performance change either, so no AC-power numbers are needed.

## Requirements

- **PLT-01: met.** Unity 6 LTS pinned exactly (6000.3.26f1), URP Forward+ at every level, C#, Windows x64 (Mono) player.
- **PLT-02: partly met** (groundwork, as cited). Low, Medium and High exist with their own URP assets, High is the default. The three assets are identical apart from their names (suggestion 3), and the frame-rate targets belong to later phases and PLT-07.
- **PRD-07: partly met** (stub, as cited). Every control is present, and frame-rate independence is tested. Acceptance happens at G2 and G3.
- **D-001: met** (with D-031). **D-006: met.** The sim core compiles in Unity with only `netstandard`, and no assembly under the sim package references `Assets/`. **D-019: consistent.** The noclip stub uses Unity's frame (+Y up). **D-005, D-017:** nothing to check yet; no flight loop and no FPV camera target in this change.

## Suggestions (non-blocking, for the owner or orchestrator)

1. **A failed build leaves untracked files in `Assets/`.** `com.unity.test-framework.performance` (pulled in by URP core → `com.unity.collections`) writes `Assets/Resources/PerformanceTestRun{Info,Settings}.json` and their metas, plus `Assets/Resources.meta`, before a build, and doesn't remove them when the build fails. An agent that stages a folder could commit them. Add `unity/Assets/Resources/PerformanceTestRun*` to `.gitignore`, and keep `unity/Assets/Resources.meta` ignored until the project needs a real `Resources` folder. Owner: game-developer (a small chore change).
2. **Cold start uses half the smoke budget.** The first start of a fresh build took 14.8 s, 10.35 s of it spent loading assemblies (most likely the on-access scan of newly written DLLs). A warm start takes 3.2 s. Fine for 30 s today; watch it as the player grows, or have the smoke check start the player once before timing it.
3. **Vsync and identical tiers.** `vSyncCount: 1` on Medium and High (0 on Low), and the three URP assets are byte-identical apart from their names. PLT-07 benches need vsync off: set it in the quality levels or force `QualitySettings.vSyncCount = 0` in the bench. Tiering the URP assets is tech-artist work (PLT-02). The proposal names tech-artist as the URP-settings reviewer; QA checked only the scenario facts (Forward+, levels, default), not the look settings.
4. **Fpv450.Sim gets Unity's auto-referenced DLLs at compile time.** `nunit.framework.dll`, `Mono.Cecil.dll` and `Unity.Collections.LowLevel.ILSupport.dll` are on its compile line (`noEngineReferences` removes only the engine). The .NET `simcore` build would reject any use of them, so the risk is small. `"overrideReferences": true` with an empty `precompiledReferences` in `Fpv450.Sim.asmdef` would close it in Unity too. Owner: physics-engineer.
5. **Empty asmdefs warn on every compile** (`Fpv450.World`, `Fpv450.Feed`, `Fpv450.UI`: 3 warnings per build). Harmless; they go away when those folders get their first script. Don't add placeholder code to silence them.
6. **SEC-06 hook missing on the trunk.** `docs/environment.md` describes `.githooks/pre-commit` (through `core.hooksPath`), but no `.githooks/` is tracked on `unity/main`, so the pre-commit scan isn't active. For the orchestrator.

All 10 spec scenarios pass, and every check was re-run by QA. The six suggestions are follow-ups, not blockers.

Verdict: APPROVED
