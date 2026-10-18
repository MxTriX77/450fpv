# Environment

The pinned toolchain for the Unity restart, as installed and checked in phase 0 (2026-10-17). Change a version only with a new entry in [decisions.md](decisions.md), then update this file and rebuild what depends on it.

## Dev machine
| | |
|---|---|
| CPU | AMD Ryzen 7 7735HS (8 cores) |
| GPU | NVIDIA GeForce RTX 4050 Laptop, 6 GB (driver 32.0.16.1714); iGPU AMD Radeon 680M |
| RAM | 16 GB |
| OS | Windows 11 Home, build 26300 |
| Power | AC adapter, "Balanced" scheme at setup. **Benchmarks only on AC, vsync off, power mode printed** (PLT-07). On battery the laptop falls back to the iGPU. |

## Pinned tools
| Tool | Version | Path or command | Installed by |
|---|---|---|---|
| Unity editor | **6000.3.26f1** (changeset `de8167e7440c`, the newest Unity 6 LTS on 2026-10-17) | `$env:UNITY` = `C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe` | official installer from Unity's release API, MD5-checked |
| Unity modules | Windows x64 Mono player (built in) | — | IL2CPP is not installed; add it only if profiling asks for it |
| Unity Hub | 3.22.3 | MSIX package `UnityTechnologies.UnityHub` | winget `Unity.UnityHub` |
| Unity licence | Personal | activated by the pilot in Unity Hub | — |
| Blender | **5.2.2 LTS** | `$env:BLENDER` = `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (not on PATH) | winget `BlenderFoundation.Blender` |
| .NET SDK | 10.0.301 | `dotnet` | preinstalled |
| Python | 3.14.6 | `python` (`C:\Python314`) | preinstalled |
| Python packages | Pillow 12.3.0, pytest 9.1.1 | [tools/requirements.txt](../tools/requirements.txt); run tests as `python -m pytest` | `python -m pip install --user -r tools/requirements.txt` |
| Node.js | 24.17.0 | `node`, `npm` | preinstalled |
| OpenSpec CLI | 1.13.1 | `openspec` | `npm i -g @fission-ai/openspec` |
| PowerShell | 7.6.7 | `pwsh` (MSIX alias in `%LOCALAPPDATA%\Microsoft\WindowsApps`) | winget `Microsoft.PowerShell` |
| ffmpeg | 9.0.2 (gyan.dev full build) | `ffmpeg`, `ffprobe` (on the user PATH from `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_…\ffmpeg-9.0.2-full_build\bin`) | winget `Gyan.FFmpeg` |
| Git | 2.55.0.windows.3 | `git` | preinstalled |
| Git LFS | 3.7.1 | `git lfs` | preinstalled |
| GitHub CLI | 2.101.0 | `gh`, signed in as the repo owner | preinstalled |

**Variables.** `UNITY` and `BLENDER` are user environment variables. Shells and apps started before the install don't see them, nor the new PATH entries: open a new terminal. Scripts fall back to the paths above when a variable is unset. A subagent's shell may not carry them either: set `$env:UNITY` and `$env:BLENDER` explicitly in the brief, or rely on the pinned paths the scripts fall back to.

**Unity batchmode** can't run while an editor has the project open (`unity/Temp/UnityLockfile`).

**Keep Unity Hub running for batchmode.** The Hub is an MSIX package, so the Personal licence it activated lives in its virtualised AppData, which the editor's own licensing client can't read. The editor gets the licence only through the Hub's licensing-client pipe while the Hub is running; with the Hub closed, batchmode fails with `No valid Unity Editor license found` (exit 198) and leaves a stale `unity/Temp/UnityLockfile` behind (delete it once no Unity process runs). Found in phase 0 (2026-10-18).

**Licence in batchmode:** checked on 2026-10-17 by creating a throwaway project with `-batchmode -nographics -quit -createProject` (exit 0, entitlements resolved). The log warns `Code 10 while verifying Licensing Client signature` for the Hub's MSIX licensing client; it's harmless, and licensing still succeeds.

## Repository setup (once per clone)
```powershell
git lfs install --local
git config remote.origin.fetch "+refs/heads/unity/*:refs/remotes/origin/unity/*"
git config core.hooksPath .githooks
python -m pip install --user -r tools/requirements.txt
```
- **Fetch refspec:** this folder fetches only `unity/*` (docs/repository.md). Godot is read from the tag: `git fetch origin tag godot-final`, then `git show godot-final:<path>`.
- **Hooks** (SEC-06): `.githooks/` holds `pre-commit` and `commit-msg` (the OPSEC scan, `tools/hygiene/precommit.py`) and git-lfs's `pre-push`, `post-checkout`, `post-commit` and `post-merge` shims, which must stay there because `core.hooksPath` turns off `.git/hooks`. The setting is shared by every worktree. A worktree on a branch that predates `.githooks/` gets neither the scan nor LFS uploads until it merges `unity/main`.
- **LFS in a new worktree:** a worktree the app creates can hold LFS *pointer files* (130 bytes) instead of the textures and FBX files, even though the objects sit in the shared `.git/lfs/objects`. Run `git lfs checkout` (local, no network) before any Blender or Unity work; `git lfs ls-files` shows `*` for real content and `-` for pointers. Found in phase 0: Unity imported nothing and the review capture still exited 0 with an empty stage. Since `p1-pipeline-hardening` the capture refuses an empty stage with exit 1 and names the fix (`run git lfs checkout`), and it prints the LOD bias it ran with.
- **Claude Code worktrees** start from the current `HEAD` (`.claude/settings.json`, `worktree.baseRef: head`), never from `origin/main`, which is the frozen Godot trunk.

## GitHub
| | |
|---|---|
| Repo | `MxTriX77/450fpv`, public |
| Default branch | `unity/main` (set in phase 0, the pilot's OK) |
| Ruleset | "protect unity/main": PR required, merge commits only, no force-push, no deletion; repository admins may bypass (bookkeeping) |
| Godot | `main` frozen, tag `godot-final` → `b684cce`; one banner commit on `main` links to `unity/main` (D-027) |

## Git LFS quota
- **Allowance:** GitHub Free includes **10 GiB of LFS storage and 10 GiB of bandwidth per month** (GitHub docs, checked 2026-10-17). Over the limit, pushes of new LFS files are refused (storage) or LFS is disabled until the next month (bandwidth).
- **Used by this repo:** about **0.28 GiB**, all of it the Godot attempt's 164 LFS objects (measured at the `godot-final` push). The Unity line adds its CC0 textures, FBX exports and textures through LFS.
- **Exact account usage** needs the `user` token scope, which `gh` doesn't have. Check it at github.com → Settings → Billing → Git LFS before a large asset push. Every local worktree shares one LFS object store, so agents don't spend bandwidth.

## Godot-era tools
- **Kept until G4:** Godot 4.7.2 (.NET build), installed through winget as `GodotEngine.GodotEngine.Mono`, and a `godot-final` worktree (`git worktree add <scratchpad>/godot godot-final`, never a branch), for FEED-00's A/B: one identical input frame through both feeds, stage by stage (D-037).
- **Not needed:** the Godot add-ons and modules (Terrain3D, Jolt Physics), Godot's C# project generator (the old csproj workaround, knowledge.md §14.6), and `tools/reference/track_attitude.py` on `godot-final` (the attitude study is measured).
- **To port, not to run:** `wind_stats.py`, `wind_history_fixture.py` and `rain_stats.py` on `godot-final` are Blender Python; PHY-14 grades the wind targets with them, so phase 7c ports them to plain Python under `tools/` with their self-checks (D-037).

## Unity MCP
- Claude Code's Unity plugin can drive a **running** editor through the `com.unity.pipeline` package and a beta `unity` CLI. Neither is installed.
- **Batchmode stays the verified path** for every check in `CLAUDE.md`. Revisit when an agent needs to edit scenes interactively.
