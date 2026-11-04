# GR-INTEG-B1: Phase B1 integration

Worktree `D:/Projects/ftt-wt/gr-integ`, branch `gr/integ`, from `946db4ab`. Each merge `--no-ff`, in plan order. Every build: 0 errors, `CS8632` the only warning, no `KLOTHO_DET002`. Every full run exit 0, no orphan processes, lock held and released.

| # | Branch | Merge SHA | Translation commit | Result line |
|---|---|---|---|---|
| 1 | gr/cpu | a395dd08 | none (no en.csv change) | Failed: 0, Passed: 4171, Total: 4171 |
| 2 | gr/stage | b88cdfbb | 96406553 | Failed: 0, Passed: 4196, Total: 4196 |
| 3 | gr/audio | 3177cd9c | fef24b2c | Failed: 0, Passed: 4212, Total: 4212 |
| 4 | gr/train | c557a2b3 | 3768236a | Failed: 0, Passed: 4226, Total: 4226 |
| 5 | gr/cap | cb80a142 | 6d699556 | Failed: 0, Passed: 4240, Total: 4240 |
| 6 | gr/pilot | fbf65142 | 9d411044 | Failed: 0, Passed: 4269, Total: 4269 |

Audio landed +16 (declared +10) and pilot +29 (declared +27), as the brief expected; every total equals the table.

## Conflicts
- Train, Cap, Pilot: `localization/en.csv`, the same tail hunk each time. Resolved as a union (markers removed, every block kept in merge order: GR-RUN, GR-AUDIO, GR-TRAIN, GR-CAP, GR-PILOT). Nothing dropped.
- Cpu, Stage, Audio merged without conflict (Stage changed two layout rows in place, Audio appended `settings_mute_placeholder_cues`).
- No conflict in `BossPhaseRuleMatrixTests`, `PlayerController.cs`, `SaveManager.cs` or `EnemyController.Hazard.cs`.

## Translation
`en.csv` changed in Stage, Audio, Train, Cap and Pilot. After each, `touch localization/en.csv`, `--import` under the lock, committing only `localization/en.en.translation`. Owed keys present: `stage_berlin_layout`, `stage_globe_layout`, `settings_mute_placeholder_cues`, the `# GR-TRAIN` block, the `# GR-CAP` block, `orleans_room_rampart`. `.import` and `default_bus_layout.tres` churn discarded each time.

## GR-INTEG fixes
None needed. No fixture or assertion changed; no count change beyond the merged branches' own.

## Notes
The matrix tool's case in `tests/Tools/` (`FighterMatrixMeasureTests`) ran as a no-op with `FTT_MATRIX` unset. Run time grew 2 m 41 s to 3 m 29 s across the six merges, in line with +98 cases.

## Codex review fixes (GR-INTEG)
Five findings, +5 cases (4269 to 4274, one full run `Failed: 0, Passed: 4274, Total: 4274`).
1. `CpuBandTuning.For(Hard)` returns `Hard` (the Defy reserve); `HardBoss` stays in `BossOverride(Hard)` for the Mirror. `CpuDefyPolicyTests.TheNormalHardConstructorKeepsTheReserveAndTheMirrorBandDoesNot`.
2. One pause owner in the training room: `PauseMenuBase.ExternalPauseOwnerActive` (set by `TrainingRoomPanel.Open`, cleared in `Close`/`_ExitTree`) makes a pause menu refuse its button and a focus loss; the panel answers Pause and cancel in `_Input`, before the driver's menu. `TrainingRoomSceneTests.WhileTheSettingsPanelIsUpAPauseOrEscapePressAndAFocusLossOpenNoSecondMenu` pushes a real `InputEventAction` through the viewport and calls `HandleFocusLost`.
3. Leonardo's plain Story synced bolt stops on any resolved contact (see `GR_CAP.md`, `PROVISIONAL-LEONARDO-PLAIN-BOLT-STOPS`).
4. Fighter cues keep a per-cue, per-player watermark of the highest sim tick that played (`FighterSimulationDriver.ClaimCue`, hit, block, perfect block, swing whiff); a correction never lowers it and `Initialize` resets it. Limit: a genuine event at or below the watermark in a corrected timeline is suppressed once. The throttle-window sequence is `FighterCombatCueTests.TheCueWatermarkNeverPlaysADecreasingTickTwicePastTheThrottleWindow`. Presentation only.
5. Mozart's stack lane reads current ownership; a live respec clears the history.

## Second Codex round (GR-INTEG)
- Disconnect under the training panel: `PauseMenuBase.SetPaused` keeps the tree paused while `ExternalPauseOwnerActive`, so `ResolveDisconnect` cannot unpause under the open panel; the panel's close hands it back. The reverse order is already refused (`IsAnyOpen`). `TrainingRoomSceneTests.AControllerDisconnectResolvedUnderTheSettingsPanelLeavesTheTreePausedAndTheFlagDoesNotOutliveTheRoom` (+1) also asserts the static flag is false after a scene exit with the panel open.
- Cue watermark claim corrected: it guards decreasing ticks only and does not dedupe events a real rollback removes and reintroduces (replay reaches the current tick before `SyncPresentation()`, so ticks increase). Code kept, comment and `GR_AUDIO.md` state the limit.
- **GR-DOCS must widen the ledger row `VERIFY-PRESENTATION-DEDUPE-WEAKNESS` with the new Fighter cues (hit, block, perfect block, swing whiff), to be fixed with stable sim event identities when Package 7 enables rollback in real play.**

## Pause fixes (review round, 2 majors)

- Forfeit under the open training panel: `LocalFighterPause.ExitToLobby` now calls `ClosePauseForSceneExit()`, which also clears `ExternalPauseOwnerActive` before releasing the tree. Audit: the other scene exits (`PauseMenu` restart/quit) already used it; `MatchResults` never pauses the tree; `TrainingRoom.ExitToShip` now calls `Panel.Close(true)`, an unconditional release.
- `TrainingRoomPanel.Close` keeps the tree paused while a pause menu is open (`PauseMenuBase.IsAnyOpen`); `Close(forSceneExit: true)` overrides.
- Tests (both failed before the fix): `ClosingThePanelUnderAnUnresolvedDisconnect...`, `ForfeitUnderTheOpenPanelReleasesThePause...` in `TrainingRoomSceneTests`. Full suite 4277 (4275 + 2).
