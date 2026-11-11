# Design: w1-farmland-world

Task 1.1's part: how the friend's farmland (D-088) sits in our Unity project, what is pushed and what stays local (D-089), how a checkout gets the world, and what the friend's generator needs. Task 2.1 (the menu and the start point) adds its own section. Numbers are labelled **measured** or **derived**.

## 1. The friend's paths, unchanged
The world keeps the paths it has in the friend's project, under `unity/Assets/`:
- `Farm_land/`: the friend's own work.
- `Art/`, `TerrainSampleAssets/`, `ALP_Assets/`: the three Asset Store packs.

Why the same paths:
- **The generator finds its sources by path.** `FarmLandBuilder` reads `Assets/Art/Environment/...`, `Assets/TerrainSampleAssets/...`, `Assets/ALP_Assets/country house01` and `Assets/Farm_land/...`, and writes back under `Assets/Farm_land/`.
- **The references hold.** Every file keeps its `.meta`, so the scene, the prefabs, the materials and the TerrainData find each other by the same GUIDs. No GUID is shared between the two projects (measured: 0 of the friend's 568 against our 964).
- **An update from the friend is a copy** over the same paths.

Our own code stays in `_Project`. The friend's code has no assembly definition, so it compiles into `Assembly-CSharp` (`FreeFlyCamera`, `SurroundGrassCulling`) and `Assembly-CSharp-Editor` (`FarmLandBuilder`, `FarmLandCapture`). Nothing of ours references it.

## 2. What is pushed and what stays local
The rule: **push the friend's own text; keep every third-party file and every binary local.**

**Pushed:** `unity/Assets/Farm_land/` (307 files with their `.meta`, 1.6 MB, measured).

| Folder | What |
|---|---|
| `Editor/` | `FarmLandBuilder` (the seeded generator, one partial file per area) and `FarmLandCapture` (the saved review cameras) |
| `Runtime/` | `FreeFlyCamera`, `SurroundGrassCulling` |
| `Shaders/` | the grass, tree bark and tree leaves shaders and their shared wind |
| `Scenes/FarmLand.unity` | the scene: 17 terrains (the map, 8 surround tiles, 8 far-ring tiles), the 5 ruins and their debris, the sun, the camera and the post-processing volume |
| `Prefabs/` | 10 grass, 13 tree, 20 bush and 12 village prefabs |
| `URP_Materials/`, `Lighting/`, `Terrain/Layers/`, `Terrain/FarmLand_TerrainLit.mat` | the rebuilt materials, the sky and the post-processing profile, the 8 terrain layers and the terrain material |
| `Models/*.py` | the Blender scripts that make the reduced island tree and the ruins |

**Local-only, linked from the store:** 56 links, 5 folders and 51 files, each file with its `.meta` (measured).

| Path under `unity/Assets/` | What | Why local |
|---|---|---|
| `Art/` (junction) | Book of the Dead: Environment, the pines and bushes | Asset Store EULA |
| `TerrainSampleAssets/` (junction) | Terrain Sample Asset Pack, the grass and small bushes | Asset Store EULA |
| `ALP_Assets/` (junction) | Country House FREE, the village's house | Asset Store EULA |
| `Farm_land/Materials/` (junction) | the Poly Haven ground textures and the island tree | binary (CC0, 706 MB) |
| `Farm_land/Terrain/Generated/` (junction) | the flattened diffuse maps and mask maps the generator derives | binary; `GrassDry_diff_flat.jpg` copies a pack texture |
| `Farm_land/Terrain/*.asset` (hard links) | the TerrainData of the map, the surround and the far ring | binary (105 MB) |
| `Farm_land/Models/*.fbx`, `*.png` (hard links) | the reduced island tree, its atlases, and `village_ruins.fbx` | binary; `village_ruins.fbx` is the pack's house cut up in Blender, so it copies store geometry |

`tools/assets/manifest.json` pins and credits the four packs (W0). The friend's own binaries aren't pinned there: they are the friend's work, not a third-party pack.

**The prefabs are pushed: they copy no geometry.** W0 listed the pine and bush prefabs as copying Book of the Dead geometry. They don't. Each is 4–13 KB of YAML that names the pack's FBX by GUID and file id, plus the friend's materials and LOD settings. No Mesh object is embedded in any prefab or in the scene (measured). The same holds for the grass and small bushes (Terrain Sample meshes) and the ruins (`village_ruins.fbx`). Pushing them lets W4a and W4b add physics metadata to them in git.

## 3. How a checkout gets the world
```powershell
pwsh tools/vendor/link.ps1     # the vendor\ junction (worktrees), then the 56 world links
git lfs checkout               # our own LFS files
```
Then Unity imports the world with the project. **A fresh worktree's first import takes about 5.5 min and its Library 2.8 GB** (measured 2026-11-11, with other Unity runs on the machine: 275 s of first import, then 46 s of model reimport once our importers compile). Later refreshes take under a second.

What `link.ps1` does:
- **Folders become junctions, files hard links,** each file with its `.meta`. A junction can only link a folder, and `Models/` and `Terrain/` mix the friend's text with binaries. File symlinks need admin rights or Developer Mode; hard links don't, and they stay one file with the store, since every worktree lives on the main checkout's volume.
- **Idempotent.** A link already in place is kept. Anything else at a link's path stops the script, which never overwrites a file it didn't link.
- **The ignore list guards the push.** `.gitignore` lists every linked path, and the script refuses to link a path git wouldn't ignore. So a path added to the script's list but not to `.gitignore` can't reach a commit.
- **In the main checkout** it skips the `vendor\` junction (the store is there) and makes the same world links.

**Removing a worktree:**
```powershell
pwsh tools/vendor/link.ps1 -Unlink   # removes the junctions and hard links, never the store's files
git worktree remove <worktree>
```
A recursive delete that follows a junction deletes the store itself, so the links go first (measured: after `-Unlink`, `git worktree remove` needs no `--force`, and the store's 1,080 files are unchanged).

**Without the store,** the project should still compile and build (derived): the packs carry no scripts, and the friend's code finds its sources by path only. The farmland scene then opens with missing references, and `FarmLandSceneTests` fails, naming `link.ps1`. Since 2.1 the player carries the farmland (§6), so a player built without the store shows a broken farmland in Explore: build players only from a linked checkout.

## 4. Our pipeline, and no new packages
**Our pipeline stays:** `URP-Low`, `-Medium` and `-High` with their renderers and the feed's feature. The friend's `Assets/Settings` (`PC_RPAsset`, `PC_Renderer`, their global settings and volume profiles) don't come. What the scene carries comes with it: the sun, the sky material, fog, ambient light and the post-processing volume (`Lighting/FarmLand_PostProcess.asset`: white balance, colour adjustments, shadows-midtones-highlights, tonemapping, bloom, vignette, motion blur).

**Where our `URP-High` differs from the friend's `PC_RPAsset`** (measured), for W6:

| Setting | Ours | Friend's |
|---|---|---|
| Shadow distance, cascades | 50 m, 1 | 400 m, 4 |
| Soft shadows | off | on, high |
| Colour grading | LDR | HDR |
| Depth and opaque textures | off | on |
| Additional-light shadows | off | on |
| Renderer features | the feed | screen-space ambient occlusion (intensity 0.4, radius 0.3) |

**No package added.** Every package GUID the pushed text references is URP, Core or Shader Graph 17.3.0, which we have (measured). The friend's other packages are used by nothing in the world: AI Navigation, Timeline, Visual Scripting, uGUI, Version Control, the Unity CLI's pipeline package, the IDE packages and Multiplayer Center. `FreeFlyCamera` uses only the Input System's keyboard and mouse (ours 1.20.0, theirs 1.16.0).

**Layers 16 and 17** (the pack's `EnvironmentSmall` and `EnvironmentLarge`) come with the pine and bush prefabs. Our project leaves them unnamed. Nothing culls by them: the cameras, the lights and both renderers take every layer.

## 5. What the generator needs
`FarmLandBuilder` is the "Farm Land" menu, one seeded step per area (Steps 1–13); the same seed gives the same world. In our project it needs:
- **The store linked:** it reads the pines and Quixel bushes (`Art/`), the sample grass, bushes and terrain layers (`TerrainSampleAssets/`), the house pack (`ALP_Assets/`), the Poly Haven textures (`Farm_land/Materials/`) and the reduced models (`Farm_land/Models/`).
- **Blender,** only to remake the reduced island tree and the ruins with `Models/*.py`, outside Unity. Unity itself imports the six Poly Haven `.blend` previews through Blender on a first import (about 40 s, measured). The world doesn't use them, so a machine without Blender only gets their import errors (assumed).

**Running a step rewrites the store.** The steps write the TerrainData, the generated textures and the pack textures' import settings, and those are local-only: hard links and junctions to the store, the same files every checkout links. So a run changes the friend's project in the store for every checkout. Run a step only as a decided rebuild, not from a task's worktree. W5's terrain edits (the trench holes, the flattened footprints) also land in local-only TerrainData, so they need a rebuild step of their own, or the TerrainData pushed (LFS, 105 MB).

**Step 1 must not run in our project.** It sets the render pipeline to `Assets/Settings/PC_RPAsset.asset`. That asset isn't in our project, so the step stops at its first line, before it changes anything. Our pipeline stays (the feed).

## 6. The player on the farmland (task 2.1)
**Explore opens the farmland in noclip.** The menu's Explore (review builds, which the pilot's `build_latest` is) loads `FarmLand` instead of the test range; Train still flies the test range. The menu says so on each item's line: Train "flies the test range", Explore "the farmland, noclip only for now"; the loading line reads "Loading the farmland…  noclip only for now". Esc goes back to the menu.
- `FarmlandExplore.Open` (App) sets the loaded scene up, leaving the friend's scene file untouched: it switches the friend's `FreeFlyCamera` off (found by name, it is in `Assembly-CSharp`), puts our `NoclipCamera` (PRD-07) and `NoclipFeed` (the feed and its OSD, V and O, as on the test range) on the scene's main camera, stands D02 at the start point, and starts the camera 3 m behind the stand and 1.6 m up, looking along its heading.
- The friend's look stays as the scene has it: its sun, sky, fog and post-processing volume. The test range's light presets (F1–F4) and number-key views are not carried over; the look is W6's.
- The farmland is the build's third scene (`ProjectSetup.Apply` and `EditorBuildSettings`), so a player built from a linked checkout carries the world (D-089: the build includes the local-only packs).
- **Load time (PLT-06):** the app logs the seconds from the menu's Explore to the farmland's first noclip frame, and the smoke test fails above 20 s. Measured 2026-11-11 in the review player, 1920 × 1080 window, on AC: 1.78 s on the first run after the build, then 0.55–0.97 s (40 runs in all). The player is 959 MB with the world (measured).
- **Known problem: the player crashes on quit after the feed has drawn the farmland** (measured, D3D12 only). The smoke test's checks all pass, but of 36 runs with the feed on the farmland, 30 ended in an access violation in `D3D12Core.dll` after the app's shutdown, 3 hung (at the start, or in the next flight's first seconds) and 3 exited cleanly. With the feed off on the farmland (`-feed 0`) it exits cleanly (4 of 4), with `-force-d3d11` 2 of 3 did (the third stuck at the start, as 1 of the D3D12 runs did), and so does the test range's noclip with the feed in the same build (2 of 2); the pilot's latest build, without the farmland, exits cleanly (5 of 5). No display-driver reset is logged. Switching off the terrains, the village, every renderer, the sky, the sun, the fog, the volume or its motion blur, or using a new camera, does not stop it. `tools/build_latest.ps1` fails on the smoke test's exit code, so this branch can't feed the pilot's build until it is fixed (the feed and the renderer: tech-artist).

**The start point: `village`, at (566.5, 569.0) in the scene's frame, heading 300°** (`FlightWorlds.FarmlandStart`). The frame is the scene's (`unity-lh-yup`, W2's): x east, z north, m from the map tile's south-west corner; the foot is on the terrain at 37.89 m (measured). With legs off the launch stand stands there; in noclip it always stands there, to show the start (the explorer has no legs setting).

How it was chosen (measured, by a probe over the scene, not committed): every 0.5 m spot inside the village's circle (75 m about (512, 600), the friend's `FarmLandBuilder`, as W2 exports it) was scored on its clearance from the ruins' and debris' renderer bounds and from every tree and bush instance's bounds, on the terrain's slope, and on the grass.
- **The village's core sits on a slope.** The ground falls about 11 m from the hilltop in the south-east (37.9 m) to the westernmost ruin (27.0 m). The ruins stand on levelled plots, but their level ground ends 1.5 m outside the walls, inside the ring where the debris lies (4.5 m). So no spot between the ruins is both 2 m clear and level.
- **The level ground is the hilltop meadow in the village's south-east quarter, by the crossroads.** The point is on that meadow at the gravel track's verge: the terrain's normal 0.1° from vertical, the stand's base within 2.1 mm of the terrain, 62.7 m from the village's centre, and nothing within 21.4 m of its footprint (the nearest, a roof sheet by `Ruin_Stump`).
- **The verge has sparse grass.** The meadow's grass grows about 0.5 m high (derived: W2's `Grass_A`, 0.26 m scaled 1.8–2.6) and hides the 0.25 m stand, and it would stand in the drone's props; the verge has about a third of the meadow's grass (measured, the detail map), so the stand shows.
- **Heading 300°** faces `Ruin_Stump`, 35 m away, so the pilot's first view is the village.
- Rejected: the lane between the ruins (only its west end is 2 m clear, at 1.2–2.6°, in dense dry grass that hides the stand); the lane's east end on its ruts (2.7 m from a dry bush, but 2.7°: not level); the same meadow 15 m to the south-west (554, 560.5: level at 1.3°, but 11.7 m clear of a tree against 21.4 m).

`FarmlandStartTests` checks the spec's scenario in the scene: the stand built as the physics builds it (`LaunchStand.Build`), its footprint inside the village's circle, each post's foot and each base bar's end within 1 cm of the terrain (half the 20 mm tube, derived), and no renderer but the stand's, nor any tree or bush instance, within 2 m of the footprint. It checks every renderer, so W5's houses and cars are held to it too. A spot in the lane (520, 600) fails it (measured: 23.7 mm off the terrain).

**For W3:** the session takes `FlightWorlds.FarmlandStart` (or the map package's start, once it carries one), and the flight draws the physics' stand there as on the test range (`DroneModels.StandPose`); the explorer's stand is the same pose.
