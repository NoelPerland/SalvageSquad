# Salvage Squad — Scrapline slice

Unity 6000.3.26f1 LTS / URP. Windows development; iOS verification deferred.

## Play / edit

- Open this folder in Unity Hub. Open `Assets/Scenes/Scrapline.unity`, portrait Game view, Play.
- If batch startup exits 198 for licensing, open Unity Hub before retrying; that restored this machine's licensing client.
- Hold left mouse or touch and drag. Forward shooting is automatic. `R` restarts.
- Steer the squad center into one gate; signs show the exact reward. The middle gap charges/collects neither.
- Avoid striped warning lanes. Squad count is health. Boss defeat opens the reward/replay card.
- Pause, sound and reduced-motion controls are available. `TrainingRange.unity` remains the original regression scene.
- Build menu: **Salvage Squad > Build Windows Prototype**. The slice builds to sibling `WindowsSlice/SalvageSquad.exe`; previous training build is preserved in `Windows/`.

## Current milestone / resume

- Completed (2026-10-15): exactly three small polish improvements: reward text hierarchy (survivors/gold score/secondary stats), delayed medal stamp with a short overshoot, and heavy boss-death impact layered under the victory cue independently of ordinary hit cooldown.
- Verified each improvement before starting the next. Four final focused PlayMode regressions pass (victory/reset, text bounds/medal timing, reduced motion, boss death, pause/defeat); Windows build succeeds. Final visible touch replay: novice wins 61.88s with 480 survivors; bad route loses 3.66s. Pause/retry/reset/pools pass; zero dropped shots/enemies. Final reward capture visually inspected. Prior 62-test suite was not fully rerun for this presentation-only change.
- In progress: none. Next best small polish task: refine boss destruction timing so debris and sound feel like a short sequence. Stop before adding content/systems; human gate clarity/feel and boss balance still need validation.
- Evidence: ../../work/polish-{reward,medal,final}.xml, polish-build.log, polish-after.log/txt; ../Handoff-Images/05-victory.png, 06-medal-stamp.png, polish-before-victory.png. Windows player is current. Human listening and phone performance remain unverified.
- Preserve fixed separated gates/committed-side shooting, uncapped numeric growth, long shots and boss-only attacks/death cancellation. Strong builds still kill boss quickly after armor. Resume ../AI-Handoff.md.
## Current content

- Six paired gate rows contrast recruits against damage/fire rate/weapon upgrades, plus two optional single-lane recruit gates under pressure. Every numeric reward is shootable and has no authored cap; negative blue becomes warm red at zero. Start with five recruits; render at most 60 robots while additional recruits add health and volley power. Tune values and shotsPerIncrement in saved assets; uncapped bypasses legacy maxValue.
- Harmless opening drones; swarm/heavy/charger pressure; guards before optional gates; only the crusher uses large striped lane attacks; crusher with a protected opening dodge, aimed attacks, faster last stand and final ten-second countdown.
- 60–90 second authored run; final crowd wave at 57.2s, boss at 60s with 5500HP; centred victory celebration alternates two shifts with different upgrade tradeoffs and wave layout, causal defeat retries the current shift. Medal and local personal best only.
- Perspective camera adapts to growth; larger spaced ivory/teal wind-up Rangers, coral enemy silhouettes, muted dock panels/fog; baked meshes share a gradient palette atlas. Boss-only articulated arms reuse that material.
- Pooled shots/enemies/bursts, staggered recoil/flashes, joining motion, damage feedback, restrained camera shake.
- Authored wave layouts/approach distances and extended firing reach keep long shot streams visible across the track. Shooting/lean stay consistent across frame rates.
- Original bounded mechanical shot cadence, cannon/metal impacts, upgrade/warning/finale sounds and musical climax; sound and reduced-motion controls. Physical haptics deferred.
- No Overdrive, base building, economy, monetization, backend or accounts.

Art rules: round porcelain bodies, teal enamel helmets, brass wind-up keys; hostile coral hulls/navy treads. Cool dock floor and cream markings keep both teams readable. Navy enamel UI, teal primary buttons and gold reward seals share the world palette. One palette atlas and baked modular meshes keep crowd material draws small; muzzle flashes and impacts use short warm accents.

## Targeted editing map

- `Assets/Game/Runtime/SquadController.cs`: drag, formation, upgrades and unit loss.
- `CombatSystem.cs`: swept hits, fixed pools, enemy movement; training mode retained.
- `SliceRun.cs`: encounter clock, hazard/boss telegraphs, pause/end/reset and UI.
- `GateRow.cs`: center-based single gate resolution. `SliceFeedback.cs`: sound/music/camera.
- `Assets/Game/Slice/FirstShift.asset`: authored encounter list; `ReverseShift.asset` demonstrates a data-only variant. Assign either to SliceRun.level.
- Enemy assets in that folder tune health, speed, contact damage and scale. Polished silhouettes/colors live in `Art/Polished`; the legacy target still supports settings tint. Base firing/movement values remain on SquadController.
- `SliceSetup.cs`: initial scene/asset creation, refuses to overwrite the scene. Preserve hand edits; do not regenerate assets wholesale.
- `PlayFeelTuning.cs`: explicit migration for this pass, overwrites encounter/enemy tuning; do not rerun automatically over later manual edits. Edit the saved level assets for ordinary tuning.
- `PresentationUpgrade.cs`: explicit character and world/interface art migrations. `Apply` rebuilds this generated art kit and rebinds only Scrapline presentation references; do not run over hand-edited art. `EnemyVisual.cs` swaps four shell meshes; two additional arms activate only for the boss.
- `SliceBenchmark.cs`: development-only stress (`-sliceBenchmark`) and two-shift real-time replay/capture/reset checks (`-sliceReview`; add `-sliceRouteRight` for firepower). `-sliceQA` runs novice/idle/zigzag/centre-edge/bad touch profiles; `-sliceQAShort` limits that to novice/bad; `-sliceQADirector` checks greedy/low-growth. Absent from release builds. Use visible player windows.

Runtime state never modifies level/enemy assets. All 64 primary gate paths are checked with uncharged and large rewards; uncharged negative choices can defeat the squad. Single-lane gates can be avoided. Legacy capped data remains supported. No custom level editor is needed.

## Verification

Tests cover real UI touch clicks, padded hit regions, pause actions, settled/transient camera bounds, fatal-hazard overlay cleanup, mouse/touch release and drag-height consistency, gate math/charge/boundaries, formation bounds, all authored gate paths, upgrades, frame consistency, warning priority, damage/defeat, pause, complete reset, six winning builds and pool stability.
Batch commands live in AGENTS.md. Logs/XML stay outside the project in `work/`.
`Review-*.png` (540×960 army) and `ReviewRight-*.png` (480×1040 firepower), including their `Next-` shift captures, are real-time Windows frames from the first uncapped-gate pass, before final boss/near-sign tuning. QA frames are refreshed by final touch verification. `ReviewTall-*.png` are the older presentation baseline. `Slice-*.png` are automated scene renders, including 540×1170. `Art-Kit.png` is a neutral-light mesh lineup.
Current verification: `../../work/gate-choice-EditMode.xml`, `gate-choice-PlayMode.xml`, `gate-choice-live.txt`; prior stress: `long-shots-stress.log`; earlier numeric pass: `powerups-*`; older replays: `Review-Checks.txt` / `ReviewRight-Checks.txt`; pacing samples: their `Timeline.csv` files; stress: `Performance.txt`. Logs/XML remain in `work/`. Hidden Windows players skip rendering; launch visibly for rendering measurements.

Manual checks remain: touch feel, safe areas on hardware, audio preference, unfamiliar-player replay intent and iPhone profiling/signing.
Design targets: 8/10 understand within ten seconds; 6/10 voluntarily replay. No retention or revenue claim follows from these small samples.

## Decisions / evidence

| Date | Decision | Evidence / limit |
|---|---|---|
| 2026-10-12 | Preserve Windows workflow and pinned Editor/packages | Licensed Editor, tests and original Windows build worked. |
| 2026-10-12 | Make multiplication visible, maintain shooter focus | [Mob Control listing](https://apps.apple.com/us/app/mob-control/id1562817072) and reference gameplay; growth remains the key payoff hypothesis. |
| 2026-10-12 | Defer city/economy systems | Sampled [Last Z reviews](https://apps.apple.com/us/app/last-z-survival-shooter/id6503272652?see-all=reviews) describe grind/spending pressure; qualitative evidence only. |
| 2026-10-12 | No claims that all reference games are growing | [August market report](https://sensortower.com/blog/top-10-worldwide-mobile-games-by-revenue-and-downloads-in-august-2026) does not establish mechanic-level retention. |
| 2026-10-12 | Improve gate text before further content | First scene capture showed undersized labels; enlarged text and corrected UI bar sprites. |
| 2026-10-13 | Muted tactile dock, visible backs, shootable signed choice, mechanical mix | Six reference galleries + four gameplay videos sampled; ../Presentation-Research.md records source distinctions and limitations. Render iteration and 30 checks informed changes. |
| 2026-10-14 | Earlier upgrades, one-shot firepower choice, visible incoming crowds and a different next shift | First-minute footage/reviews, six simulated builds, real-time replays and captures; evidence and limits in ../Presentation-Research.md. Human replay appeal still needs observation. |
| 2026-10-14 | Explain shot progress, retain distinct growth/weapon routes, protect boss arrival and announce deadline | Baseline/full/greedy/low visible replays and 54 tests; [Last War player discussion](https://www.reddit.com/r/LastWarMobileGame/comments/1gdnnny/) flags confusing gate shot counts (anecdotal). Human comprehension/replay appeal unverified. |

Art/meshes and synthesized audio were created for this project; no competitor assets or sounds imported. Display font is Lilita One under SIL OFL; unchanged font/license are in Art/Polished. Small body text uses Unity's default font.
At meaningful milestones compare first-minute pacing, growth clarity and replay friction against Last War, Last Z, Top Lords and Mob Control. Record one experiment; do not build their live-ops systems.
