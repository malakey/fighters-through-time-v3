# A. Time Freeze: switch off, keep the code (decision 10b++)
Recon at 43d54db6. Citations are file:line from reading the code. 354 mentions in 83 files under scripts/, 369 in 46 test files.

## 1. Dependency map (what the switch must touch)
**Core class.** scripts/Environment/TimeFreezeController.cs polls Input.IsActionJustPressed(Actions.TimeFreeze) at :178; TryBeginTimeFreeze :186; DrillFreeFreeze :145. Its statics (FreezeSeconds, CooldownSeconds, FrozenTimeGroups, ProcessFallbackGroups, ThawWarningLabelKey, ControllerGroup) are ALSO used by ChronalRewindManager (:580, :633, :673, :746, :795, :867) for the Post-Landing Hold and the boss T01b suspension. Those statics and IStoryTimeFreezable (31 files) are shared infrastructure and MUST stay live. Only the controller node and its input go dormant.
**Creation.** StorySceneBootstrapper.cs:81-88 builds the controller under includeRewind (levels only, never hub) and attaches FirstUseTooltipPresenter.Attach(..., sceneHasTimeFreeze: true).
**Player flag.** PlayerController.TimeFrozen (:4449, about 20 reads, e.g. :632, :956, :2769, :3109) is set only by the controller (:201, :220); with no controller it stays false. Also read at PlayerController.Defy.cs:49 (refusal label), InputReadiness.cs:23, PlaytestCombatStats.cs:128 (label). Construct nodes have SetTimeFrozen (TeslaCoilNode:463, LeonardoTurretNode:467, SerpentNestNode:282, SandDecoyNode:151, SonataPlatformNode:95), also driven by rewind/hold sweeps.
**Input.** project.godot:166-171 gameplay_time_freeze = keyboard physical 82 (R) + pad button 7 (L3). InputManager.cs:79 constant, :128 in RemappableActions (drives AddPolledAction :820, the Controls tab list, conflict set InputBindings.cs:804/819), :142 label key; InputBindings.cs:587 TimeFreezeAction, :594 DirectActions (Controls direct-slot rows), :600 comment (not in RequiredReachableActions), :657 label. Legacy repair InputBindings.cs:293-321 (gameplay_rewind -> time_freeze). Saved rows live in InputBindingSet (global payload).
**Save.** SaveManager.cs:84 StorySaveData.TimeFreezeCooldownSeconds; StoryAttemptState.cs:141,151,359; StoryManager.cs:941-1046 and :1324 (TimeFreezeCooldownRemaining, SetTimeFreezeCooldown, EndActiveTimeFreezeForExplicitSave :957, fresh-attempt reset :1002).
**HUD.** scenes/ui/StoryHUD.tscn:440-467 TimeTools/TimeFreezeIndicator (FreezeIcon, FreezeColumn, FreezeSubLabel hud_time_freeze_name, FreezeRemainingText); StoryHUD.cs:110,180,242,253,274,299,511-578,1239-1242 (subscribes OnTimeFreezeStateChanged, _freezeBadge, StatusClockSuspended :515). Paths are bound with GetNodeOrNull, so hiding is safe. HudAbilityIndicatorModel, HudBadge, PulpAbilitySlot, RadialProgress mention it in shared code/comments.
**Level 0.** TutorialCalibration.cs:40 step UseTimeFreeze; :204-208 RegisterRewindComplete advances to it; :211 RegisterTimeFreezeComplete; :229 SkipTimeFreezeLesson. Level00Controller.cs:586 (level_00.station_rewind completion -> BeginTimeFreezeDrill), :1028 (fallback), :1048-1100 drill (already calls SkipTimeFreezeLesson when _services.TimeFreeze is null, :1049-1051), :1209 ProcessTimeFreezeDrill. String tutorial_step_time_freeze.
**First-use tooltip.** FirstUseTooltips.cs:32,41,57; FirstUseTooltipPresenter.cs:43,49,124 offers it on the Skip path when SceneHasTimeFreeze; FirstUseTooltipsTests.cs:60.
**Audio.** CoreMechanicCueCatalog.cs:24-28,60,76 four cues + resources/Audio/core_mechanic_cues.tres; CoreMechanicCuePresenter.cs plays them from OnTimeFreezeStateChanged; AudioSnapshotMixer.cs:36,136,155. The thaw-warning cue is REUSED by the Post-Landing Hold (CoreMechanicCueCatalog.cs:28, ChronalRewindManager.cs:673): do not delete catalog rows.
**Events.** EventBus OnTimeFreezeStateChanged / TimeFreezePayload; nothing raises it once the controller is absent.
**Pause.** PauseMenu.cs:431,573 call EndActiveTimeFreezeForExplicitSave (null-safe).
**Bots.** Bots never press it (no Actions.TimeFreeze in scripts/Diagnostics); only a refusal label (PlaytestCombatStats.cs:112,128). No change.
**UI capture.** UiCaptureTargets.Huds.cs:109,301 raise Active/Cooldown payloads for the huds_* targets.
**Move List, Systems Card, Drills, Reduced Temporal Effects.** No Time Freeze text or hook found (the only en.csv hits are the 8 keys below; the controller reads no ComfortSettings).
**en.csv.** controls_action_time_freeze :975, hud_time_freeze_ready/active/cooldown/thaw :976-979, tutorial_step_time_freeze :980, tooltip_first_use_time_freeze :1549, hud_time_freeze_name :1673. UnusedTranslationKeyTests fails only on a NEW orphan.

## 2. Does anything require or lean on it?
No. Verified: no level controller references it except the Level 0 drill (Level00Controller.cs:38 calls the drill an exempt scripted case) and a comment at Level10Controller.cs:865. resources/Dialogue has zero hits for "freeze" or "stop time". The controller doc forbids a progression gate (TimeFreezeController.cs:25) and TimeFreezeTests.NoAuthoredLevelGatesProgressionOnTimeFreeze (:635) pins it. Bots finish levels without it. Locks, bosses and hazards only consume the shared suspension statics that the Post-Landing Hold and T01b still drive. The only player-facing text telling the player to use it: tutorial_step_time_freeze, tooltip_first_use_time_freeze, and the HUD strings. Real cost is the loss of a zero-cost escape (5 s, 45 s cooldown, legal at zero rewind charges), as the decision file notes.

## 3. Cleanest single off switch
One named rule, e.g. public static bool TimeFreezeController.FeatureEnabled (default false; or a pure TimeFreezeRules.Enabled in scripts/Core) plus an internal nullable override seam for tests (the MainMenu.ShowDeveloperLevelSelect pattern). Readers (about 7):
1. StorySceneBootstrapper.cs:81 skip creating the controller (services.TimeFreeze stays null). Downstream already tolerates null (Level00Controller.cs:1049, ChronalRewindManager.ResolveTimeFreeze :580, StoryManager :959).
2. FirstUseTooltipPresenter.Attach at :88: pass sceneHasTimeFreeze from the flag.
3. TutorialCalibration.RegisterRewindComplete :204 goes to Done when off (keep the enum member and the Register/Skip methods for the on state). Level00Controller :586/:1028 then need no drill: nine lessons become eight. The station_rewind line stays as a flavour line.
4. InputManager.RemappableActions (:125) and InputShortcuts.DirectActions (:594) omit TimeFreeze when off (static readonly initialised from the flag). This removes it from polling, the Controls tab and the conflict set.
5. project.godot:166 keep the action but set "events": [] (like gameplay_grab :162), so lookups by name never throw and R / L3 are physically unbound.
6. StoryHUD.cs ~:1239 hide TimeFreezeIndicator and skip refresh when off; the .tscn keeps the node.
7. CoreMechanicCuePresenter: nothing, no events fire.
Saved data: schema stays v8, no derivation. TimeFreezeCooldownSeconds stays dead data, written back unchanged (StoryManager.cs:1024,1046,1324); recommend no clamp so a later re-enable restores a valid 0-45 s. Saved gameplay_time_freeze binding rows are silently dropped on apply because the name leaves RemappableActions (the behaviour InputBindings.cs:300-303 describes); the row stays in the file. Keys: code keeps referencing them (StoryHUD.cs:575-578, ActionLabelKey, Level00Controller:1056/1073), so no new orphan is expected; verify tutorial_step_time_freeze.

## 4. Test impact (approx; [TestCase] counts)
- TimeFreezeTests (21 cases, 98 mentions): most build TimeFreezeController directly and are unaffected; any case via the bootstrapper or input fails. Force on via the seam in a before/finally. Estimate 2-4 touched.
- TimeFreezeBindingMigrationTests (3): pure; leave.
- GamepadDefaultBindingTests (7): TimeFreezeDefaultsToTheLeftStickClickAlone :147 fails; rewrite to pin empty events and that R and L3 are held by no action.
- GamepadOverrideRepairTests (9): pure InputBindingSet; leave.
- TutorialCalibrationTests (8): the chain UseRewind -> UseTimeFreeze -> Done at :42, :82, :134, :194, :204 fails; force on for the legacy chain, add 2 off cases.
- Level00FlowSceneTests: no Time Freeze refs; verify the flow does not wait on the drill.
- PostLandingHoldTests (11) and ChronalRewindTests: build the controller directly; probably leave, verify.
- StoryHudPulpTests (15), StoryHudCompactTests, StoryHudContractTests (3), HudAbilityIndicatorModelTests (13): cases asserting the badge or slab width with the badge fail; force on for model cases, add 1-2 off cases (indicator hidden, slab geometry).
- FirstUseTooltipsTests (5): ShouldShow is pure; leave, add one presenter-off case. CoreMechanicCueCatalogTests, AudioSnapshotMixerTests: leave.
- The remaining ~35 files only set TimeFrozen directly or use the world-hold: leave.
Estimate: 10-20 cases force-on, 3-6 rewrites, +8-12 new off-state cases (no controller from the bootstrapper, tooltip not attached, RemappableActions/DirectActions omit it, project action unbound, Level 0 chain skips, indicator hidden, cooldown field round-trips, schema v8). Net delta about +6 to +10; fix exactly in the plan.

## 5. Moot rules and docs to amend
Moot (code stays): Defy refusal under Time Freeze (PlayerController.Defy.cs:49), "Time Freeze discards, never buffers", Momentum regen / reuse delay / block regen suspension under it, the Suppression carve-out, VERIFY-TIME-FREEZE-ON-BACK-SHARED. Still live via Post-Landing Hold: Hitbox/Hurtbox world-hold gates, the thaw cue, the freeze sweep.
Amend: design-godot.md (59 mentions: F03, controls, Level 0 lessons, HUD, audio), TEMPORAL_STATE_CONTRACT.md (10), HUD_CONTRACT.md (5), COMFORT_SETTINGS.md (12), STORY_PERSISTENCE.md (6), CHECKPOINT_RECOVERY.md (2), DEFENSIVE_EFFECTS.md (12), CAMPAIGN_VALIDATION.md (8), MIRROR_PARADOX.md (3), the ledger (39; add a DEFER-TIME-FREEZE-OFF row), AGENTS.md and CLAUDE.md (input, HUD, Level 0, baseline).

## 6. Risks and questions (recommended answer first)
1. Level 0 drops to eight lessons: set RegisterRewindComplete -> Done; keep the station_rewind flavour line. Recommend yes.
2. Skip-path tooltip: do not offer it. Recommend yes.
3. R and L3 become free: recommend leave unassigned this package, so a later re-enable cannot collide; ask the user whether any new feature wants L3.
4. Flag location: code constant plus test seam (recommended), not a Settings toggle, which would be save state.
5. HUD: hide the node, keep the slab at 484 px, re-capture huds_* targets.
6. Keep every shared piece (thaw cue, AudioSnapshot.TimeFreeze ladder, suspension statics); the Post-Landing Hold depends on them.
7. Hub: never had the controller; no change.
8. No stranding path found; the DrillFreeFreeze exemption disappears with the lesson.
9. Saved cooldown on a later re-enable: leave unclamped.
10. Consequence: with slots rising to 2/3/4 and no escape tool, the death-rewind pool is the only recovery besides Fonts; worth a bot re-measure.
