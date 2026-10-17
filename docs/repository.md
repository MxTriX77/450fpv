# One repo, two codebases: Godot and Unity

`https://github.com/MxTriX77/450fpv` holds **both** attempts, completely separate inside the same GitHub repo (D-002, D-025, D-027). They share no files and no history. You switch between them by switching branch.

| | **Godot** (first attempt, frozen) | **Unity** (the restart) |
|---|---|---|
| Trunk branch | `main` | **`unity/main`** (an orphan branch: no shared history, no Godot files) |
| Feature branches | `physics/…`, `world/…`, `techart/…`, `game/…`, `qa/…`, `chore/…` (old, never touched again) | **`unity/<role>/<change-id>`**, e.g. `unity/physics/build-flight-model` |
| Final state | tag **`godot-final`** → `b684cce` | grows; gate builds tagged `unity/g2-gallery`, `unity/g3-world`, … |
| Browse on GitHub | `https://github.com/MxTriX77/450fpv/tree/main` | `https://github.com/MxTriX77/450fpv/tree/unity/main` |
| Its PRs | `https://github.com/MxTriX77/450fpv/pulls?q=base:main` | `https://github.com/MxTriX77/450fpv/pulls?q=base:unity/main` |
| Local folder on the dev machine | `C:\Users\maksy\.claude\projects\450fpv` | `C:\.dev\450fpv` |

## Switching on GitHub
- **Branch dropdown** (top left of the code view): pick `main` for Godot, or `unity/main` for Unity. Typing `unity/` in the dropdown filters to the Unity branches only.
- **Direct links:** the two `tree/` links in the table above. Bookmark both.
- **The repo's front page** shows the **default branch**. Phase 0 asks the pilot to make `unity/main` the default, so the front page shows Unity. Godot stays one click away in the dropdown, or via the tag `godot-final`.
- **Cross-links on both front pages:**
  - The **Unity README** (`unity/main`) opens with a line linking to the Godot codebase (`tree/main`) and the `godot-final` tag.
  - The **Godot README** (`main`) gets **one** banner line linking to `tree/unity/main`. It is the only commit ever made on `main` after the freeze, and it's made in phase 0 **with the pilot's OK**. Without it, Godot visitors wouldn't know the Unity line exists.
- **Comparing:** don't use GitHub's compare view between `main` and `unity/main`. They share no history, so it shows nothing useful.

## Switching locally
- **Use two folders, one per codebase** (the table above). Don't `git switch` between `main` and `unity/main` in one folder: the trees share nothing, and Unity's ignored `Library/` and other generated files would sit around in the Godot tree.
- **The same remote, two checkouts:** `C:\.dev\450fpv` tracks `unity/*`; the old folder tracks `main`. Both push to the same GitHub repo.
- **Optional:** in the Unity folder, fetch only the Unity branches, so the Godot ones never clutter it:
  ```bash
  git config remote.origin.fetch "+refs/heads/unity/*:refs/remotes/origin/unity/*"
  ```
  To look at Godot code from the Unity folder without switching, read a file straight from the tag:
  ```bash
  git fetch origin tag godot-final
  ```
  ```bash
  git show godot-final:game/src/video/VideoFeed.cs
  ```
- **Parallel agents** use `git worktree add ../450fpv-<branch> unity/<role>/<change-id>` (Claude's worktree isolation does this), so they never collide in the main folder.

## Rules that keep the two apart
- **Unity work is only ever committed on branches under `unity/`.** PRs target `unity/main`.
- **Never create a branch named plain `unity`.** Git stores branch names as paths, so `unity` and `unity/...` can't coexist.
- **Never rename, delete, rebase or force-push** `main`, the old Godot branches or the `godot-final` tag. The only exception is the one README banner commit above, made with the pilot's OK.
- **Godot code is reused by reading or copying it** into the Unity tree as a port (for example the feed, [legacy/video-feed-godot/](legacy/video-feed-godot/)), never by merging branches.
- **Optional (repo settings, pilot's OK):**
  - a GitHub ruleset on `unity/main`: require a PR, block force-push and deletion
  - a ruleset on `main` that blocks pushes, to freeze it formally, applied after the banner commit

## Gate tags (Unity)
Each pilot gate build is tagged on `unity/main` when it ships: `unity/g1-look-lock`, `unity/g2-gallery`, `unity/g3-world`, `unity/g4-feed`, `unity/g5-drone`, `unity/g7-mvp1`, `unity/mvp-2`, … and `unity/release-1.0`.

From the GitHub tags page you can open any build the pilot reviewed, just as `godot-final` opens the last Godot state.
