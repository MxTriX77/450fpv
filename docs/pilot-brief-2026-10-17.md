# Pilot brief, 2026-10-17 (verbatim)

This is the pilot's restart brief, kept word for word. [VISION.md](../VISION.md) is still the root. This brief changes the tech stack, the order of work and the drone's name. The refined form is in [requirements.md](requirements.md) and [plan.md](plan.md).

## The brief

> ive pasted this only as an example of how it was done in another project.
> i need you to prepare all info, basing off of my original plain text VISION.md, and plan the work based off of the following mega high level plan but make it right:
>
> 1. to win time and tokens BEFORE session gets compacted, we FIRST do the most demanding tasks resource-wise  -  analyze all videos and images, and create textures and models out of them. we save assets. !!we make sure that we can easily do changes down the line on the models and textures if any from pilot's side
> 2. based on the collections of ready to use models+textures, you do the first release - gallery of all objects (like tree 1, 2, ..., 5, house 1, 2, ..., 6), (trench 1, 2, 3), etc. as you can see, we don't need to many versions of same objects, but not very few either because map should be quite big (like 3 adjacent typical ukrainian villages) and rich. and the graphics should represent an ULTRA realistic version of ukrainian terrain and urban objects
> 3. upon gallery approval goes map world work.
> 4. when world done - we work on ultra realistic analog noisiness and OSD
> 5. after this, we design the one and only drone model - it shouldn;t be rocket science, just google at typical opto fiber drones in ukraine and how they look (the coil underneath, a couple of batteries, etc, an explosive cargo on top of it, etc.)
> 6. add support of tx controllers and basic flight physics
> 7. add ultra realistic physics
>
> high level reqs:
>
> 1. unity, backend - whatever the best
> 2. should be smooth 60fps on my pc settings. quite realistic graphics and effects
> 3. top notch physics (all of what weve discussed and what will have been analyzed from reference media)
> 4. only one drone model called "Svinoriz 10 Opto" (10 inch)
> 5. orchestrator, subagents and their roles
>
> now create the doc and req collection and .mds for best practice
>
> i believe all questions ive answered before during the existing development, but if you have any important(!only) questions - feel free to ask. i need the requirements to be refined as fuck so i start the development smoothly

"This" in the first line is a handoff brief from another project, which the pilot shared as a style example. That is why the phase briefs in [briefs/](briefs/) follow its shape: task, settled points, read-first list, constraints, flow, checks, done-means, write scope and commit rule.

## Answers to the open questions, the same day (verbatim)

The pilot called some questions "dumb". The rule that follows: **never ask the pilot for anything physics can calculate.** Those questions are dropped, and the team computes the answers.

> 1. hiehght maybe 25cm. like a chair upside down, so each chair leg is beneath each motor. based this off of the expected size of our future svinoriz. also look at this (even nevermind that the drone on this image is a regular civilian radio analog: https://prodrone.com.ua/content/images/27/536x363l50nn0/5300004155-76038560858957.jpg
> 2. oh yes they do. matter of fact, they sit even if i fly fast, although they travel sometimes based on aerodynamics. how fast - look these question how fast what size how behaves etc. - why tf are you asking me these questions if it's you whos going to write ultra realistic physics? youll need to fucking calculate it
> 3. dumb question. i answered that it's a mix. you remember? also, you will have many references
> 4. look at some profiles at c:/FPV_TEST (refer to its srouce code https://github.com/MxTriX77/FpvPrep. so there are original profiles of drones and those modified by me. let's say you use those used by me while leaning a little bit towards those sharpy original settings, especially throttle - you dont always get the chance to set comfortable throttle when in combat env. HOWEVER, it's important that you make room for flexibility on this one coz in the future I MAY ask you to introduce betaflight rates, throttle curves, and other fly control related settings you will have seen in this FpvPrep
> 5. dumb question. physics
> 6. sometimes a little bit. but based on ground type and how soft it is - more often. if it's raining it sticks most of the times (yeah, by the way, we will have rain on/off setting also in the sim. so rain/sunny/severe rain/night)
> 7. dumb question. physics
> 8. not a question of speed of descent but mostly how much thorttle power hence pressure you give as opposed to drones weight. wobble - yes, of course
> 9. doesnt happen much. but again dumb question - calculate
> 10. i cant say speed is different, mostly i focus more how to compensate shifts in course because of wind. you will see it in wind reference
> 11. overly sharp reverse, snagging on tress, very sharp lift off, or sudden momentum backwards after lift off

What each question was: 1 the launch stand · 2 rain drops on the lens · 3 frame stutter before a loss · 4 rates and expo · 5 landing-bounce height · 6 a leg sticking at liftoff · 7 motor-mismatch yank · 8 falling through the drone's own prop wash · 9 the throttle-punch shake · 10 speed in strong wind · 11 what cuts the fiber.

Where each answer went: [decisions.md](decisions.md) D-021 to D-024, [requirements.md](requirements.md), [pilot-questions.md](pilot-questions.md), [knowledge.md](knowledge.md) §4.6 and [reference-notes/fc-profiles.md](reference-notes/fc-profiles.md).

## Answers given earlier the same day

| Question | Answer |
|---|---|
| Can the team use paid 3D assets as a base? | **Free only (CC0 + team-built).** |
| Where does the new Unity project live? | "existing repo, but a new branch or something separate. can we do that?" Yes: an orphan branch in `MxTriX77/450fpv`, with no shared history and no Godot files. The Godot work stays on `main`, tagged `godot-final`. A follow-up from the pilot ("what about feature branches?… we could use master prefix unity") gave every Unity branch the `unity/` prefix: trunk `unity/main`, features `unity/<prefix>/<change-id>` (D-025). |

## What "realism" means (verbatim, the same day)

> and also specify that when i say realism i mean more than just abstract realism.
>
> i also mean that the game should be a copy of how it looks on reference (both my flight replay references and those images), it's important. it's also important that we do proper geometry if you remember - not only for looks but also for physics

Where it went: D-028, REAL-01 to REAL-05 in [requirements.md](requirements.md), `CLAUDE.md`, the plan's done-means and the agent files.

## Feed and repository (verbatim, the same day)

> make additions to the docs: specifcy that the current noisiness algorithms should be very much similar to those lie under the godot codebase on git.

> also, add there in the docs information about the branches - that we're creating something separate INSIDE the current gh repo. it should be easy to switch between GODOT codebase and UNITY codebase on github

Where they went: D-026 and [legacy/video-feed-godot/](legacy/video-feed-godot/); D-025, D-027 and [repository.md](repository.md).

## Subagents (verbatim, the same day)

> also, specify that it's preferred to have each subagent for each logical task

Where it went: D-029, `CLAUDE.md`, [workflow.md](workflow.md), [team.md](team.md), PRC-01 and every agent file.

## Tooling (verbatim, the same day)

> ONE LAST thing, specify that we're using openspec and other things we added here (remember i mean those prompt optimization thingies)

Where it went: D-030, [tooling.md](tooling.md), [../openspec/config.yaml](../openspec/config.yaml), `.claude/skills/` (karpathy-guidelines plus the six OpenSpec skills), `.claude/commands/opsx/`, `CLAUDE.md`, the workflow and the phase-0 brief.
