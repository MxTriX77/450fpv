# Proposal: w6-farmland-look

**Owner:** tech-artist · **Reviewer:** qa-engineer; **the pilot when they fly the farmland (D-093)**
**Requirements:** REAL-01, REAL-03, PLT-02, PLT-05, PLT-06, PLT-07 · **Decisions:** D-026, D-088, D-089, D-093 · **VISION:** §2 (what the goggles show), §3 (no lag) · **Plan:** `docs/final-world-plan.md` §4, unit W6 · **Tier:** Standard

## Why
The farmland's look is adopted as the friend made it (D-088), and REAL-01 says realism is a copy of the references. Our pipeline renders it differently. W1's review measured ours at about 4 luma darker than the friend's at their 10 saved cameras, with hard tree shadows where theirs are soft, no distant haze and no ambient occlusion. W1's design §4 lists why: our `URP-High` has 50 m and 1 cascade of shadows against their 400 m and 4, no soft shadows, LDR grading against HDR, no depth or opaque texture, and no SSAO.

The farmland is also far heavier than the test range: 1,646 trees and bushes, 2.84 million grass instances, the village and 4.5 km of terrain rings. Our feed renders at 2889×1625 before its PAL pass. No one has measured the frame rate, VRAM or load time there. The pilot flies it next (D-093), and their first complaint was lag.

## What Changes
- **The look:** our pipeline renders the farmland like the friend's at their 10 saved cameras, in noclip with the feed off: the same exposure, soft shadows to a useful distance, ambient occlusion and the distance haze. The settings change only in our own pipeline assets and renderer. The friend's scene and the store stay untouched (D-089). The feed's own passes stay a faithful port (D-026).
- **The frame rate and memory:** with the feed on, the busiest farmland views meet PLT-02. VRAM stays ≤ 4.5 GB and RAM ≤ 8 GB (PLT-05), and Explore loads in ≤ 20 s (PLT-06). The levers are grass distance and density, shadow distance and cascades, LODs and culling. Detail the PAL feed can't show is the first to go.
- **Measured by PLT-07:** on AC, vsync off, along a fixed scripted camera path over the village, a tree belt and open fields, with a report.

## Non-goals
- No change to the friend's scene, terrain, vegetation or village.
- No change to our feed's look (D-026).
- No new assets (W5) and no flight on the farmland (W3).
- Medium and Low presets only as far as they must still load the farmland.

## Impact
`unity/Assets/_Project/Settings` (the URP assets, renderers, volumes); the feed's camera setup where it renders the farmland; a scripted benchmark path and its runner; `docs/reports/` for the PLT-07 report.
