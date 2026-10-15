# Salvage Squad — AI handoff

Updated 2026-10-15. Continue the existing project; do not recreate it.

Project: `C:/Users/Noel/Documents/Codex/2026-10-12/we-are-starting-a-brand-new/outputs/SalvageSquad`
Unity **6000.3.26f1**, C#, URP. Windows development; iOS signing/device validation deferred.

Read `SalvageSquad/AGENTS.md`, then only task-relevant files. Caveman/Ponytail; no broad scans, new frameworks or meta systems. OmniRoute is unconfigured.

## Completed

- Latest focused polish: clearer reward text hierarchy, delayed medal stamp (reduced motion respected), heavy boss-death impact layered under victory cue. Three changes only; verified and checkpointed sequentially.

- Playable portrait slice: steering, auto-fire, gates/growth, enemies, boss, victory/defeat, pause and replay. Two authored shifts.
- All numeric powerups grow when shot: recruits, multiplier, damage, fire rate and weapon tier. No authored reward cap. Negative recruit gates start at -20; blue negative becomes warm red at zero/positive.
- Paired gates stay fixed at X ±1.7, X-scale 0.5. Choose left at squad center <= -1.1, right at >= 1.1. Middle charges/collects neither; one reward per row.
- Bullets remember squad center at firing, preventing wide-squad farming of both choices from the middle. Side switching preserves older shots' aim; single-lane gates retain their own window.
- Centered KO-style upgrade text keeps punch/tilt/fade; diagonal lines removed. Damage feedback remains above squad. Reduced motion, pause and reset supported.
- Shot range 22; scene projectile speed 32. Camera lens 42, follow 0.4. Up to 60 visible robots represent larger logical squads; MK2+ share heavy art. Fire rate above 3 adds equivalent damage with fractional carry instead of unlimited visible shots.
- Only a living boss makes large striped lane attacks. Death cancels pending damage/warnings; pre-boss hazard rows removed.

## Verified

- Latest polish: **4 focused PlayMode regressions pass**, Windows build successful. Prior baseline: 27 EditMode + 35 PlayMode = 62 passing; full suite not rerun for this presentation-only change. Evidence: work/polish-final.xml, polish-build.log, polish-after.log/txt (workspace-relative).
- Visible real-time automated touch: novice victory at 61.88s, 480 survivors, 7 losses, 7 gates, 170 kills. Bad route defeat at 3.66s. Pause/replay/reset/pools pass; zero dropped shots/enemies.
- Tests cover gap boundaries, 60-robot middle/left/right shooting, single collection, line-free popup, portrait bounds and existing run/boss behavior.
- Prior stress, **not rerun after latest spacing/UI**: 60 robots, 60 peak enemies, 405 bullets; mean frame 16.73ms, gameplay tick 1.643ms/zero allocations; no drops. Windows result, not phone verification.

## Resume state

Completed: three-detail polish pass, final visible novice/bad replay and refreshed victory image. All targeted checks pass; no drops, pause/replay/reset/pools clean.
In progress: none.
**Next best polish task:** refine boss destruction timing into a short debris/sound sequence. Keep existing loop and art; human gate clarity, enjoyment and boss balance still need observation. No additional work started.
Known issues: strong builds melt the 5500-HP boss after its 1.6s armor phase. Human enjoyment, phone controls/audio/haptics/performance remain unverified. Full QA idle/edge assertions may need updating because middle intentionally earns no gate reward.

## Relevant files and commands

- Runtime: `SalvageSquad/Assets/Game/Runtime` — GateRow, CombatSystem, SquadController, SliceRun, SliceFeedback.
- Tests: `Assets/Game/Tests`; data: `Assets/Game/Slice`; main scene: `Assets/Scenes/Scrapline.unity`. Preserve TrainingRange.
- **Do not rerun PresentationUpgrade or PlayFeelTuning**; they overwrite manual edits.
- Build: Unity menu **Salvage Squad > Build Windows Prototype**. Player: `WindowsSlice/SalvageSquad.exe`.
- Dev checks: `-sliceQA -sliceQAShort` (novice/bad), `-sliceQADirector` (greedy/low), `-sliceReview`, `-sliceBenchmark`. Launch visibly; engine captures work.
- Evidence from workspace root: `work/gate-choice-EditMode.xml`, `work/gate-choice-PlayMode.xml`, `work/gate-choice-build.log`, `work/gate-choice-live.log`, `work/gate-choice-live.txt`. Earlier stress: `outputs/Performance.txt`, `work/long-shots-stress.log`.

## Current in-game pictures

Unedited 540×960 screenshots from the latest verified Windows build, captured 2026-10-15. Stable copies below avoid routine QA overwrites. Actual gameplay, not concept art or phone validation.

### Gate choices — 2 seconds

Blue -20 and red +8; empty center selects neither.

![Separated gate choices](Handoff-Images/01-gate-choice.png)

### Upgrade impact — 4.2 seconds

Centered POWER UP announcement, diagonal lines removed.

![Centered powerup announcement](Handoff-Images/02-powerup-impact.png)

### Squad firepower — 30 seconds

Large formation and extended bullet streams. Logical count exceeds the 60-model display limit.

![Squad firepower](Handoff-Images/03-squad-firepower.png)

### Boss warning — 60.7 seconds

Armored boss and striped lane telegraph.

![Boss warning](Handoff-Images/04-boss-warning.png)

### Victory and next shift

![Victory reward screen](Handoff-Images/05-victory.png)

[Before this polish pass](Handoff-Images/polish-before-victory.png) · [Medal stamp in motion](Handoff-Images/06-medal-stamp.png)

Latest images inspected from actual renders. Audio layering implemented; subjective listening and physical phone checks pending.
