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

**Variables.** `UNITY` and `BLENDER` are user environment variables. Shells and apps started before the install don't see them, nor the new PATH entries: open a new terminal. Scripts fall back to the paths above when a variable is unset.

**Unity batchmode** can't run while an editor has the project open (`unity/Temp/UnityLockfile`).

## Repository setup (once per clone)
```powershell
git lfs install --local
git config remote.origin.fetch "+refs/heads/unity/*:refs/remotes/origin/unity/*"
git config core.hooksPath .githooks
python -m pip install --user -r tools/requirements.txt
```
- **Fetch refspec:** this folder fetches only `unity/*` (docs/repository.md). Godot is read from the tag: `git fetch origin tag godot-final`, then `git show godot-final:<path>`.
- **Pre-commit hook** (SEC-06): `.githooks/pre-commit`, active through `core.hooksPath`, which every worktree shares.
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

## Godot-era tools no longer needed
- Godot 4.7.2 (.NET build), installed through winget as `GodotEngine.GodotEngine.Mono`. Keep it only to run the frozen attempt from its own checkout.
- The Godot add-ons and modules: Terrain3D, Jolt Physics.
- Godot's C# project generator (the old csproj workaround, knowledge.md §14.6).
- The Godot-side Blender reference scripts (`tools/reference/track_attitude.py`, `wind_stats.py`, `rain_stats.py` on `godot-final`). They stay readable from the tag; the feed and wind studies they fed are already measured.

## Unity MCP
- Claude Code's Unity plugin can drive a **running** editor through the `com.unity.pipeline` package and a beta `unity` CLI. Neither is installed.
- **Batchmode stays the verified path** for every check in `CLAUDE.md`. Revisit when an agent needs to edit scenes interactively.
