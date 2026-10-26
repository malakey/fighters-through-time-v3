# FU-ECHO handoff: Echo Step removed from both modes

Workstream FU-ECHO of the feel and UI update (plan `docs/FEEL_UI_UPDATE_2026-10-25_PLAN.md` §2.5; removal map `docs/recon-2026-10-25/A_echo_step_removal_map.md`). Branch `fu/echo`, cut from `c30d7cb8`.

## 1. Summary

Echo Step (Block + Roll in a swing's recovery, 30 meter, an 8-frame wind-up, then a snap to where you stood 30 frames ago) no longer exists in Story or in the Fighter sim, and nothing replaces it. Block + Roll now does whatever its two inputs already did. The Fighter HUD shows three cooldown radials, the Calibration Drills are five, the Systems Card's last-but-one section is "Momentum", and the Controls tab lists two chords (Ultimate, Grab).

## 2. What was removed

**Sim (`scripts/FighterSim/`)**
- Deleted `FighterEchoStepRules.cs` (+ `.uid`): `FighterEchoStepRules`, `FighterEchoStepHistory`.
- `FighterSimulationComponents.cs`: components **313-317** (`FighterEchoStepRing0..4Component`) and the static `FighterEchoStepRing`; the four 310 `FighterVerbComponent` fields `EchoStepCooldownFrames`, `EchoStepWindupFrames`, `EchoStepDestX`, `EchoStepDestY`. IDs 313-317 are retired for good (311 stays retired); **the highest component ID stays 323**.
- `FighterSimulationSystems.cs`: spawn adds and `Reset`, the per-tick `Sample`, `AdvanceEchoStep`, `CancelArmedEchoStep`, `TryStartEchoStep`, `EchoStepStateAllows`, the cooldown tick, the A02 capture clear, the Sudden Death and stock-loss resets, the `EchoStepButton` / `DirectOriginButton` constants and the `allowRoll` Echo term.
- `FighterActionKind.EchoStep` deleted; **ordinal 10 left as a gap** (comment in `FighterActionRules.cs`; the enum is never reordered).
- `FighterSpecialCancel.cs`: `echoStepRequested` gone, so a Roll press always selects `VerbRoll`; `FighterMovementSystem.SpecialCancel.cs`: the wind-up term in `CancelVerbCanStart`; `FighterLedgeRules.cs`: the Echo mask (**`!blockHeld` kept**, so a hang still starts its ledge roll only on Roll without Block).
- `FighterSimulation.cs`: `TryGetEchoStepDestination`, `TryGetEchoStepRing`. `FighterSimulationDriver.cs`: the Echo cue block, `_cueEchoWindup`, `_cuePosition`, `EchoStepArrivalDistance` (the Rally `_cueEchoPool` stays).
- CPU: `TryDecideEchoStep`, `CanStartEchoStep`, `IsSafeEchoStepDestination`, `IsCrediblePunishThreat`, `CpuVerbAction.EchoStep`, `_echoOpportunity*`, `_attackExecutionID` / `_lastObservedAttackPhase` / `AttackExecutionID` (and `TrackEpisodeAndExecution` is now `TrackEpisode`); `CpuBandTuning.EchoStepAdmissionPercent`, `UltimateBeatsEchoStep`, `ReservesMeterForDefy`; observation fields `SelfEchoStepCooldownFrames`, `SelfEchoStepWindupFrames`, `HasEchoStepDestination`, `EchoStepDestinationX/YRaw`, `SelfAttackMadeContact`, `SelfDefyAvailable`, the world-observer `TryGetEchoStepDestination`, and `CpuVerbState` `EchoStep*`, `DefyHistoryUsed`, `DefyBarred` (all dead afterwards). Kept: `SelfAttackPhase` (the grab gate reads it), `UltimateSetupViable`, `FighterDefenseRules.IsDefyUnspentAndUnbarred` (a sim rule, untouched).

**Shared rules**: `BasicComboRules` five Echo constants and the whole `RecoveryVerb` / `SelectRecoveryVerb` block; `StoryCombatRules.StoryEchoStepMeterCost` / `EchoStepMeterCost(bool)`.

**Story**: `PlayerController.cs` ring, wind-up, destination check, `TryStartEchoStep`, the ring seams, `ResetEchoStepHistory` calls (rewind landing, checkpoint restore), the roll / Block-cancel guards and the persisted-cooldown capture and restore; `PlayerController.StoryFeel.cs` `EchoStepMeterCost`. `StoryPlayerResourceTimers.EchoStepCooldownFrames` is **kept as documented dead data** (still deserialized, never read or written).

**Input**: `gameplay_echo_step` block in `project.godot`; `Actions.EchoStep`, its label case and `ToButton` mapping; `InputShortcuts.EchoStepAction`, its `DirectActions` / `RequiredReachableActions` entries, recipe and label (two recipes remain); `GameplayButtons.EchoStep` deleted, **bit 12 reserved** (comment like Dash's bit 11); `DirectOrigin` is now `!grabShortcut`. **Protocol stays v4**, `SerializedSize` 12 B / 47 B packet unchanged. **Save schema stays v8.**

**UI and content**: Fighter HUD `FighterCooldownSlot.EchoStep`, `EchoStepAffordable`, the fourth radial in `FighterHUD.cs` and both `EchoStep_Cooldown` node pairs in `FighterHUD.tscn` (P1 and mirrored P2); `ApplyPlayerState` lost its `echoStepCooldownFrames` parameter. Calibration drills: `drill_echo_step` (six become five, Rally reclaim is index 4, the last), plus `Stage`, `StudentMeter`, `StudentEchoStepWindupFrames`, `ObjectiveStageTwoKey`, `ObserveEchoStep`, the runner's `_lastStage` handling. Systems Card: nodes `EchoTitle`/`EchoBody` to `MomentumTitle`/`MomentumBody`, keys `systems_card_echo_*` to `systems_card_momentum_*` ("Momentum"; body is the per-hit Momentum sentence only). Three Echo cue IDs, their `.tres` sub-resources and entries (`load_steps` 43 to 40). Eight localization keys deleted (`controls_action_echo_step`, `controls_shortcut_echo_step`, `drill_echo_step_name/_objective/_objective_two/_pass/_fail`, `fighter_hud_cooldown_echo_step`). `--import` was run locally; the compiled translation is not committed.

Not touched (Rally "echo"): `EchoPool`, `EchoDrainPerFrame`, `BasicComboRules.Echo*` fractions, `collectsEcho`, the HUD echo bands, `FirstUseTooltips.RallyEcho`, `drill_rally_reclaim_*`, `systems_card_rally_*`, `InputEventKey.Echo`.

## 3. Tests

`[TestCase]` count: 3529 to **3503**, verified by grep (tests dir, before and after) and by the full run.
- Deleted files: `EchoStepRingTests` 8, `EchoStepDestinationTests` 5, `StoryEchoStepCostTests` 3, `RecoveryChordPriorityTests` 4 = **-20** (their `.uid` sidecars too).
- Deleted cases: `CpuVerbPolicyTests` 4 (`HardWeighsTheThirtyMeterSpend…`, `HardHoldsNoMeterForADefyThatSuddenDeathBars`, `TheEchoStepGateHonours…`, `MediumsImmediateUltimatePolicy…`), `FighterTimeSystemsTests` 2 = **-6**.
- `UltimateActivationTests.EchoStepCannotUndoTheWhiffRecovery` **rewritten in place** as `ARollCannotUndoTheWhiffRecovery` (a bare Roll and Block+Roll in the whiff recovery start no roll and leave the phase) instead of deleted, so the map's -27 becomes **-26**.
- Edited, no count change: `CpuVerbPolicyTests` (`EasyEmitsNoGrab`, helpers trimmed), `FighterCpuBehaviorTests` (tuning pins; the cast test is now `NoGrabIsScheduledDuringTheCpusOwnCastAndNoAdmissionIsSpent`), `CpuMeleeBandTests`, `CpuSelfActionStateTests`, `FighterHitConfirmTests`, `MirrorParadoxTests` (the Story-adapter case now asserts no grab chord), `DefyProtectedRecoveryTests`, `FighterCombatContractW3Tests`, `FighterMatchFlowTests` (2), `FighterSpecialTimingTests`, `FighterLedgeGetUpTests` (renamed `…LogicalGrabBit…`), `FighterSpecialRecoveryCancelTests` (rows removed; renamed `NoSpecialOrUltimateComesOutOfALandedRecovery`, because a Roll in a landed recovery is now a D3 verb), `StorySpecialRecoveryCancelTests` (renamed), `StoryResourceTimerPersistenceTests`, `StoryMeterGainScaleTests` (literal 30 spend), `InfluenceMeterSourceScanTests`, `FighterHudSceneTests` (renamed `ThreeRadialCooldownsTrackAuthoritativeState`), `FighterHudContentTests`, `FighterHudModelTests` (glyph keys 4 to 3), `InputBindingSchemaTests` (two cases renamed; the round-trip case now also proves a stale `gameplay_echo_step` row loads and is ignored), `SettingsMenuSceneTests`, `CalibrationDrillScriptTests` (Count 5, list, index 4; the Echo Step beat dropped), `SystemsCardContentTests` (stem `momentum`), and comment-only edits elsewhere.
- Expectation changes beyond the removal itself: `GamepadDefaultBindingTests.NoAuthoredActionDefaultsToTheGuideButton` action-count floor 18 to 17 (one fewer authored action).
- **Seeded CPU tests: none flipped.** Removing `TryDecideEchoStep` shifts the Medium/Hard RNG stream, but no seeded Medium/Hard expectation changed and none failed. Fighter hashes all move (components removed); no suite pins an absolute hash.
- Results: full run `Failed: 0, Passed: 3503, Total: 3503`, 1.81 minutes (first run before the two fixes above: 3 failures, all in the two files just named). Rollback readiness output: **largest snapshot 3277 B** (was 4473 B), worst case median 0.090 ms, p95 0.137 ms, no breaches.
- Build: 0 errors; incremental build shows only `CS8632` in the vendored GdUnit file (the Klotho `KLSG_ECS004` warning appears on a full rebuild, which I did not run).

## 4. Behaviour notes

- **Block + Roll, grounded in the stance or at idle**: a Roll (Block + Roll pressed together is the Block raise plus the roll as before). **In a swing's recovery**: the phase machine's block cancel or roll cancel, as before for Echo Step's fallback. **After a landed Special**: the grounded Block cancel wins its tick; a Roll alone is the D3 roll cancel in both modes (the Echo exclusion is gone from the sim and Story together). **Airborne**: the air dodge. **Ledge hang**: nothing (Roll with Block held does not start the ledge roll).
- `DirectOrigin` (bit 14) now means "the grab chord is off on the originating device" (`!grabShortcut`); it used to need both chords off.
- Old saves: stale `gameplay_echo_step` rows in `Actions`, `UnboundActions` or `ShortcutEnabled` load and are ignored (restore is filtered by the InputMap); the saved `EchoStepCooldownFrames` is read and dropped. No migration, no schema bump. A device on a pre-removal LAN build would desync (LAN is de-scoped).
- Story hero loses the 10.5-meter Echo cost; nothing else in Story changed. The Mirror clone loses the verb with the Story code, and the CPU no longer spends meter on it.

## 5. Shared files touched and merge notes

- `FighterSimulationSystems.cs`: removals scattered across spawn, the movement loop, `TickCounters`, A02 capture, Sudden Death and `ApplyStockLoss` (FU-KNOCK edits other regions; textual overlap is likely near `ApplyStockLoss`'s comment and the movement loop's roll call).
- `FighterEntitySystems.cs`: **not touched** by this branch. `BasicComboRules.cs`: one contiguous block deleted (the Echo constants + chord table, just before "Per-hit Momentum") plus one comment line; FU-KNOCK should merge cleanly unless it edits that block.
- `FighterSimulationDriver.cs`: only the cue region (FU-BLOCK adds hooks nearby). `PlayerController.cs`: removals only, no additions (FU-BLOCK adds hooks elsewhere in the file).
- `localization/en.csv`: eight deleted rows, two renamed rows and one removed `# CM-STORY` comment line around rows ~960-966, 1238-1242, 1299-1303, 1370. FU-LOCK edits rows 1661-1662, which are untouched, but line numbers shift by about -8.
- `project.godot`: one block removed. `FighterHUD.tscn`: two node pairs removed.

## 6. Not delivered / risks

- No hands-on look at the HUD (two radials plus a gap versus three tidy ones), the Systems Card or the Calibration Drill list; headless only.
- `CalibrationDrillDummyRole.Idle` is now unused by the catalog (left in the enum and the dummy script; tests may still cover it).
- `CpuBandTuning` and the observation shrink changes the public test seams used by other branches' tests; any new test built against `EchoStepAdmissionPercent`, `GameplayButtons.EchoStep` or `FighterCooldownSlot.EchoStep` fails to compile after merge.
- The 3503 total assumes no other branch changes the count.
- GDD: about 60 hits in `design-godot.md` and the docs-3 master; the Controls table "Unbound" lines (`DOC-MASTER-UNBOUND-BINDING`) differ between the mirror and the master, so edit both by hand. `TEMPORAL_STATE_CONTRACT.md` is mostly the Echo Step section (retire it with a banner); `COMFORT_SETTINGS.md` lines 101-103, 128, 130, 136 are Unbound-binding lines.

## 7. Drafted doc changes for the closeout

**New ledger row `USER-REMOVE-ECHO-STEP`** (user row, 2026-10-25). Intended behaviour: Echo Step does not exist in either mode and nothing replaces it; Block + Roll is the ordinary stance-plus-roll (block cancel in recovery, air dodge in the air). The user's words: "Remove echo step — it doesnt seem to do much and I think its just adding extra complexity to the game." Observed at `fu/echo` head (this branch): verb, history ring, components 313-317, 310 Echo fields, CPU decision, Story code, input action, HUD radial, drill, cues and keys removed; 3503 tests green; largest snapshot 3277 B. Owner: Unassigned. Acceptance: (1) grep for `EchoStep|Echo Step|echo_step|EchoRing` across `scripts/`, `scenes/`, `resources/`, `tests/`, `project.godot` and `localization/en.csv` returns only the retired-ordinal and reserved-bit comments and the dead save field; (2) a hands-on pass confirms Block + Roll behaves as in section 4 and the three-radial HUD reads; (3) the design master and mirror drop the verb (about 60 lines each).

**Close or supersede**: `DEFER-M04-SUBPHASE-FLAGS` (its Echo wind-up flag is gone; the tech flag remains, so amend rather than close), `DEFER-TOGGLE-BLOCK-ECHO-CHORD` (moot, close), `USER-STORY-ECHO-STEP-COST` (superseded by the removal), `PROVISIONAL-STORY-METER-GAIN-SCOPE`'s Echo cost clause. Edit-in-place mentions: ledger lines 86, 120, 175-178, 189, 261, 282, 302-303, 311-312, 369, 403, 418, 434, 441, 444, 454, 486, 490, 497, index lines 533 and 541 (per recon A section 12).

**AGENTS.md replacement sentences**
- Component inventory: delete the 313-317 row ("`FighterEchoStepRing0..4Component`"); add "Retired sim identifiers, never reused: ... component IDs **313-317** (the Echo Step position ring, removed 2026-10-25)"; 310's row: drop nothing visible (the Echo fields were not itemised) but note `Tumble`. "The highest component ID is now 323" stays. Snapshot figure: largest snapshot 4473 B becomes 3277 B in the performance paragraph.
- `GameplayButtons`: "bits **12 = EchoStep (retired 2026-10-25, reserved like Dash bit 11), 13 = Grab, 14 = DirectOrigin**, bit 15 is the only free one"; "`DirectOrigin` ... *my grab chord is off*".
- "six scripted drills" becomes "five scripted drills (block string, landing tech, launch DI, grab/throw, Rally reclaim)".
- Fighter HUD: "four 20 px cooldown radials (S1 / S2 / Movement / Echo Step, the last dimmed below 30 meter; M25)" becomes "three 20 px cooldown radials (S1 / S2 / Movement)".
- C01c: "three fixed recipes" becomes "two fixed recipes (Ultimate = Movement Ability + Special 2, Grab = Block + Basic Attack)"; `RequiredReachableActions` is Ultimate and Grab; `InputBindingSchemaTests` bullets accordingly.
- A06 ("Story Special recovery no longer arms Echo Step; Echo Step keys to the basic swing in both modes") and the Echo Step exactness bullet ("Echo Step is exact (V7.6, Package 11 A1c)") are deleted; the same-frame chord priority bullet (`SelectRecoveryVerb`) is deleted.
- D3: delete "the Echo Step chord's Roll never rolls" (Story `BufferSpecialCancelPress` and the sim's `echoStepRequested` are gone); the Roll cancels in both modes.
- Story: delete "The campaign hero's Echo Step costs 30 x 0.35 = 10.5" (`StoryEchoStepMeterCost`); `InfluenceMeterSourceScanTests` no longer scans for it.
- Controls: Systems Card is still eight sections, the seventh named "Momentum"; the move-list / Systems Card descriptions that say "Echo Step & Resonance Momentum" now say "Resonance Momentum".
- Validation baseline: 3529 becomes 3503 (-26: four suites -20, six cases -6, one case rewritten in place, plus the changed-in-place list in section 3); `[TestCase]` count 3503.
- Failure-signature table, test-authoring notes: no Echo-specific entries.

**CLAUDE.md**: line ~86 (the 3503 baseline arithmetic and "Verified baseline"), ~189 (the bots note: nothing functional) and ~326 (the Systems Card / drills reference) mention Echo Step; update the baseline to 3503 and drop the verb.

**GDD edits needing care**: the Controls table (Ultimate / Grab / "Echo Step" rows, `DOC-MASTER-UNBOUND-BINDING`); `COMFORT_SETTINGS.md` C01c recipe list and its remapping bullet; `TEMPORAL_STATE_CONTRACT.md` (retire the Echo Step section, keep Time Freeze); `HUD_CONTRACT.md` M25 (four radials become three); `CPU_COMBAT_POLICY.md` F19 (Echo Step column and the reserve-meter-for-Defy tradeoff); `ROLLBACK_STATE_CONTRACT.md` (ring components 313-317 and the snapshot size); Section 7's drill table and the Overtime / Momentum text in the Systems Card description.
