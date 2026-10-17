# FPV Simulator: Project Manifesto \& Architecture

## 1\. Vision \& Core Concept

We are creating a lightweight performant FPV simulator with the main focus on ultra realistic physics.

## 2\. General vision

This is going to be a huge project which I'd like to be of good quality fueled by my utmost enthusiasm and real experience I've recently had.

I'm a combat FPV pilot in the Russo-Ukraine war fighting on the UAF side, and I've seen a lot - kamikaze drones, recon drones, cargo drones, etc.

Logistics is the core reason why Ukraine uses heavy FPV drones. I would like to create simulator of heavy cargo drones.

I'm quite a varsity pilot, and before being one I've tinkered with tons of simulators. What surprised me is that there is literally no FPV sim out there that ACTUALLY has the real-world physics. Some claim to have that, and they do to some extent, yet it's never about the very basics:

1. the lift off process - the cargo drones are heavy, and the effective weight can vary, so the moment you touch out of the ground you may shake a little bit. Sometimes, such drones have legs, and one or many of them may get randomly stuck for a moment which affects the horizon while you liftoff resulting in the need to correct the horizon immediately.
2. following #1, when you land the ground it may be unexpected (pitfalls, loose or porous soil, subtle grass which causes jitter).
3. there's no sim that has turbulence simulation - in combat scenarios, you sometimes have to fly through narrow holes or among dense woods in a severy wind to deliver package for allies
4. motors may work fine, but still in real life they may suddenly start working A TAD BIT asynchronously, and even a tiny offset in aerodynamics causes the drone to mildly yank during flight so you have to play around with roll and yaw to correct you course towards the target. inertia - large sized drones differ a lot from tiny freestyle FPV aircraft because they are heavy. basically, when you fly a real drone it always feels random. of course, physics are not random, but the amount of physics needed to make a sim fully realistic would require a hell of a mainframe. so one of the goals is to mimic real-life realism by adding a tiny, cunning, and elegant randomness.
5. such heavy cargo drones are geared up with optic fiber which changes your approach to piloting a lot - you don't want to do crazy turns, but smooth orbits instead, and you can' go backwards sharply because the optic cable may (or may not - again, random and unexpected dynamics) cut off.
6. Another subtle scenario is when you’re coming in for a landing on a slow glide path. You’re already about a meter above the ground, and you find that sweet-spot stick position where the drone is moving slightly down and slightly forward at the same time. Then you don’t touch the sticks until it actually touches the ground. But the moment you make contact, you need to drop the throttle to zero. Otherwise, if you leave the sticks in that fixed position, the plastic landing legs will bounce the drone back up into the air. So you have to catch the right moment to do things right, not just learn how to turn, orbit, and do flip-s or matty flips.

This is only what I've noticed during my career, there obviously are other aerodynamics related aspects which i can't explain and require your expertise to transcend realism to an even more next-level kind.

All of the simulators claim to have good physics, but in fact, at the end of the day, they all still give you smooth and rigid physics. I would like to create a simulation of undefined and, sometimes, unexpected dynamics, just like in real life.

Look at the typical similar Ukrainian drones: Viriy 10 Opto, BOMBUS Opto, OTU, Beshketnyk

## 3\. High-level requirements

-- it shouldn't be a AAA game, it should be quite light-weighted with main focus on the physics. However, the graphics should represent the analog feel to it, and still look very much realistic. The analog feel to it- not just have a plain TV noise, but be dynamic, like a noise that changes sometimes, and ACTUALLY represent the low quality sense like we had in 2000s on our TVs. Review reference\terrain folder carefully, these are examples of my real-life flights, it will give the idea of what the it should look like ulimately. the terrain i flew in on those materials is Ukraine, typical "посадки, домики, разрушенные из-за войны, машины, свойственные Украине растительность и поля".
-- the app shouldn't be complicated and flexible in terms how you configure drones. you only configure the amount of additional weight, legs TRUE/FALSE, and also configure the time of day. investigate the obtic fiber quads ukraine uses - you will see that there's a optic fiber coil that's supposed to be geared up on the bottom of quad. this means that if you have "legs" TRUE you lift off of just terrain, but if "legs" is FALSE - you lift off of 2 parallel iron bars so the coil beneath the quad doesn't touch the ground .
-- following the first requirement, even though the sim should be lightweighted, the graphics should look as realistic as possible
-- the game should run smoothly 60fps+ on most computer, specifically, on this current machine it should run ultra smoothly
-- at first, there should be just one map, pretty much a big one, not humongous though. it should be a compilation of different terrains. you'll have video and photographics references of terrain, how it looks, the objects that are there, you will review all those media materials and create a map that looks like this.
-- there will be a single drone, we don't make it flexible for the first MVP. just one drone that looks somewhere like Viriy, Bombus, OTU, Beshketnyk with 3-blade props
-- the game should support connecting radiomaster, obviously there should be an easy-to-use calibration setting
-- the UI should be simple - open the game, see "Train", "Calibrate" and "Exit" options. the design you can do yourself, just make it prettier than uglier

## 4\. Suggested approach

The project must be developed in 2 separate modules (placed in order of work):

1. Create map format and the map itself. Review all videos and images of real-life flights of mine to get an idea of what kind of terrain and objects should be there in the game world. Also, the objects should detailed - not just grass, but straws, and each straw or stick cana affect aerodynamics. Each object should also be micro detailed - each tiny nook-and-cranny must react with the quad and its physics
2. The physics engine
Should be realistic as fuck. Also have the wind logic in it. This simulator is aimed for people to learn harsh reality of combat logistics.
3. The Game app itself
Here you only provide with wireframes and I decide whether approved or not

Claude will be the main orchestrator and will need to create subagents and delegate tasks effectively. Platform - Windows. Tech stack - Godot, Blender, Git.
I as USER must be able to explicitly see the work of subagents and how the orchestrator works with them.

Git should be used, with best practices in terms of how you push, review PRs, merge branches, etc. Each agent must commit under separate names: like "Physics developer, 3d designer, QA, game developer" or smth like this. whenever i ask the orchestrator it should provide reports encompassing whole team's work and affect the ongoing work accordingly if i happen to make changes. the main agent IS ALLOWED to ask questions, but only if they are important to the project and are NOT dumb. the final flyable version must have physics logging, so if for example i test and i fuck up my drone - i must know what actually happened in terms of physics to be sure it's not a bug

## 5. Suggested high-level milestones approach

1. Intermediary feedbacks on the map - I, the user, want to see parts of terrains, separate objects, textures, but like not "house1, house2, house10500", but rather main stuff, so i can give feedback and you incorporate my vision.
2. After this series of intermediary feedback, the first build should be the final world map which i navigate using mouse+WASD in noclip mode, with SHIFT for a way faster movement. I explore every nook and cranny of the map and provide final feedback
3. After building the physics core and game interface, there should be the first MVP - the version which i launch, connect my radiomaster tx12 controller, calibrate and start flying. As I fly, i give feedback and you create newer and newer MVPs until we RELEASE.