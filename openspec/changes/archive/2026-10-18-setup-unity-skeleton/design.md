# Design: setup-unity-skeleton

## Context
Unity 6000.3.26f1 is installed at the path in `docs/environment.md` (`$env:UNITY`), licensed with the pilot's Personal licence. The embedded package `Packages/com.fpv450.sim` already exists on `unity/main` (from `setup-sim-tools`). Unity can't run in batchmode while an editor holds the project open.

## Decisions
1. **Create the project in batchmode** (`-createProject unity`), then shape it by an idempotent editor script, `Fpv450.EditorSetup.ProjectSetup.Apply` (executeMethod), which creates the three URP assets and their renderers (Forward+), assigns them to the quality levels and the graphics settings, and sets the player settings below. The script stays in the repo so the settings can be re-derived; the generated `.asset` files are committed. *Alternative:* cloning the URP template. Rejected: it drags in sample content.
2. **Packages** (`Packages/manifest.json`): URP (`com.unity.render-pipelines.universal`, the version bundled with 6000.3.26f1), Input System (`com.unity.inputsystem`), Test Framework (`com.unity.test-framework`). Remove default packages the project doesn't use (Collaborate, Ads, Analytics, Timeline, Visual Scripting, multiplayer) to keep imports fast.
3. **Player settings:** company `450fpv`, product `450fpv`; Windows x64, Mono, API compatibility .NET Standard 2.1; linear colour space; active input handling = Input System package; Force Text serialisation and visible meta files (the Unity 6 defaults, made explicit); no splash screen changes beyond what the Personal licence allows.
4. **Unity's automatic Mesh LOD stays off** project-wide where a project setting exists, because the project uses its own LOD groups (knowledge.md §14.4).
5. **Assemblies:** `Fpv450.App`, `Fpv450.Input`, `Fpv450.UI`, `Fpv450.Feed`, `Fpv450.World` (one per `Scripts/` folder, referencing `Fpv450.Sim` where needed), `Fpv450.Editor` (Editor-only), `Fpv450.Tests.EditMode` (under `Tests/EditMode`, Editor-only). `-executeMethod` names are `Namespace.Class.Method`, so the build entry point is `namespace Fpv450 { public static class Build { public static void Windows() } }` in the Editor assembly.
6. **Boot:** `Assets/_Project/Scenes/Boot.unity` holds a camera with the noclip stub and a directional light. A `Boot` MonoBehaviour (in `Fpv450.App`) logs `[Boot] Boot scene loaded` and, if the command line contains `-smoketest`, quits on the next frame. The build output goes to `unity/Builds/Windows/450fpv.exe` (ignored).
7. **Noclip stub** (`Fpv450.Input.NoclipCamera`): reads the Input System's mouse and keyboard directly (the TX12 layer comes in phase 6). Movement uses `Time.unscaledDeltaTime`; speed starts at an assumed 5 m/s, Shift ×4, wheel ×1.25 per notch, clamped to 0.5–200 m/s (assumed; the gallery spans up to a few hundred metres). The frame-rate scenario is an EditMode test on the pure movement function.

## Risks / Trade-offs
- [The Personal licence isn't active] → batchmode fails at start-up; the orchestrator confirms the licence before spawning this task.
- [The editor generates big diffs on first open (package lock, settings)] → commit them in one `chore(unity)` commit, then make sure a second open is diff-free.
