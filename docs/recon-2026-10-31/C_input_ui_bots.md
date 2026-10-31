# Recon C — input, UI, presentation, localization, bots (Momentum gauge + active Defy)

Scope: INPUT, UI, PRESENTATION, LOCALIZATION, BOTS, UI captures, tests. Source rulings: `docs/recon-2026-10-31/DECISIONS_meter.md`. Read-only recon at main `56031750`. `[REC]` = recommendation, `[Q]` = open question for the planner. Paths relative to the repo root.

---

## 1. Input

### 1.1 What exists
- `project.godot:75-183` `[input]`: `gameplay_grab` (empty, `:162`), `gameplay_time_freeze` (R, pad 7 = L3, `:166-171`), `ui_pause` (Esc, pad 6, `:172`), `ui_dialogue_log` (Tab keycode 4194306 + **pad button 8 = R3**, `:178-183`). No `gameplay_defy`. Pad 4 (Back) and keyboard F carry no action.
- `scripts/Core/InputManager.cs:55-106` `Actions` consts (`TimeFreeze :79`, `Grab :89`, `DialogueLog :105`); `RemappableActions :119-123` (15 actions); `ActionLabelKey :134-152`; `PolledMenuActions :665-667` includes `DialogueLog`; `:813-815` registers `RemappableActions + InputShortcuts.DirectActions + PolledMenuActions` for the device-activity poll, so a new bindable action is polled with no extra work.
- `scripts/Core/InputBindings.cs:546-574` `InputShortcuts`: `UltimateAction`, `GrabAction`, `TimeFreezeAction`, `DirectActions = {Ultimate, Grab, TimeFreeze}`, `RequiredReachableActions = {Ultimate, Grab}` (`:582`), `ActionLabelKey :633-638`. `InputBindingService.RestorableActions` (`:780-786`) = `RemappableActions + DirectActions`; an action outside both is silently dropped on restore (`ApplyAction :927`). `BindableActions() :795-805` feeds the Controls tab.
- Settings Controls tab: `scripts/UI/SettingsMenu.cs:512-586` `BuildControlsTab` loops `InputBindingService.BindableActions()` — one 62 px row per bindable action (`:593`), label via `InputShortcuts.ActionLabelKey` (`:669`). No hand-authored row list: Defy gets a Controls row for free once it is bindable and `controls_action_defy` exists.
- Direct action -> sim: `InputManager.cs:1081-1118` builds `GameplayButtons` (`ReadDirectAction :1168` for Grab); `ToButton :1247-1261`. `GameplayButtons` (`scripts/Core/PlayerInputFrame.cs:7-52`): bits 0-10 verbs, 11 Dash (reserved), 12 Echo Step (retired/reserved), 13 Grab, 14 DirectOrigin, **15 free (`:51`)**. Frame size stays 12 B (ushort fields). Time Freeze has no bit and is read by `TimeFreezeController.cs:178` via global `Input.IsActionJustPressed` (not device-aware).
- Story hero reads the frame: `PlayerController.cs:970, 1029, 2771, 3171, 5422, 5429` (`CurrentInputFrame.IsPressed(GameplayButtons.Ultimate)` pattern). CPU emits buttons through `FighterCpuController.cs:73` (edge mask), `:1616`.

### 1.2 What adding `gameplay_defy` requires
1. `project.godot`: `gameplay_defy` = key F (physical 70) + pad button 8 (R3). Re-author `ui_dialogue_log`'s pad event 8 -> 4 (Back/View); keep Tab.
2. `InputManager.Actions.Defy = "gameplay_defy"`; arm in `ToButton`; held read in the frame builder `:1081-1092`. **Defy is a sim verb in both modes, so unlike Time Freeze it needs a wire bit.** Only bit 15 is free. `[REC]` take bit 15 as `GameplayButtons.Defy`, fix the `:51` comment, bump `RollbackInputPacket.ProtocolVersion` 4 -> 5 (new verb on the wire; LAN is de-scoped, so cheap). `[Q]` bump vs. stay v4 like the 2026-10-16 update; and spend the last bit vs. widen Held/Pressed/Released now (12 -> 18 B frame, 47 -> 53 B packet) since this is the last free bit. Pin: `InputBindingSchemaTests.ADirectGrabBindRoundTripsAndAStaleEchoStepRowIsIgnored` (~line 590) asserts bit values and the 12-byte size.
3. Register in `RemappableActions` (after `Interact`/`TimeFreeze`, before `Pause`) rather than `DirectActions`: single button, no chord recipe/`ShortcutEnabled`/`RequiredReachableActions`. Add `ActionLabelKey` arm -> `controls_action_defy`.
4. Gate list (DECISIONS 5.4): ignores lockout and Suppression, arms no lockout, refused under Time Freeze like the Ultimate; Story reads it where the Ultimate press is read (`PlayerController.cs:3171`). Must not copy Time Freeze's global-Input read.

### 1.3 Moving the Dialogue Log R3 -> Back
`ui_dialogue_log` is not remappable, never saved, never conflict-checked and ignored by `Apply`, so only the project default changes. Read via `InputEventAction` (`DialogueManager.cs ~905-935`) and the keyboard-activity poll. Pins: `tests/Unit/GamepadDefaultBindingTests.cs:35-37` (`AuthoredUiActions`), `NoTwoActionsShareADefaultGamepadButton :184`, `NoAuthoredActionDefaultsToTheGuideButton :166` (all still pass with Defy on 8 and the log on 4); add `DefyDefaultsToR3AndTheDialogueLogToBack`. `DialogueLogTests.cs:89` drives the action directly - unaffected.

### 1.4 Saved-override repairs `[REC]`
Pattern: `InputBindingSet.RepairGodot3ShoulderRows` (`InputBindings.cs:368-380`), called from `SaveManager.ApplySavedInputBindings` (`SaveManager.cs:1128-1146`) before `InputBindingService.Apply`; idempotent, judged against the payload as loaded, touches only rows the Controls tab could not have made on purpose. Pins: `GamepadOverrideRepairTests` (7), `TimeFreezeBindingMigrationTests` (2).
- **A - another saved row holds pad 8 (R3).** Defy has no override so inherits pad 8; the conflict checker only runs at capture time, so a player who rebound e.g. Interact to R3 now shares it. Rule: new `RepairDefyPadDefaultCollision(projectDefaults)`: if `gameplay_defy` has no override and another action's saved joypad half contains JoyButton 8, mark `gameplay_defy|Joypad` Unbound (`UnboundActions`), keep the player's rebind, raise a notice `save_notice_defy_pad_unbound`. Idempotent. Wire it after `RepairGodot3ShoulderRows` in `ApplySavedInputBindings`.
- **B - Back (4).** A saved Time Freeze row may still hold Back (frozen before 2026-10-11, "keeps what it carried", `GamepadOverrideRepairTests.cs:130-131`), now shared with the log. Benign (the log only opens while a sequence shows; Time Freeze is refused in dialogue). `[REC]` no repair, document in the ledger + one test. `[Q]` or repair that row to L3 via `RepairStaleJoypadHalf :382-407` (treat the log as a holder).
- A stale `gameplay_echo_step` row on RightStick (8) is ignored by restore (not restorable) - harmless.
- Reset-to-default (`ResetInputBindingsToDefault`, `SaveManager.cs:1163`) reloads project settings, so new defaults arrive.

---

## 2. HUD

### 2.1 Story compact HUD (current)
- `scenes/ui/StoryHUD.tscn`; paths `StoryHUD.cs:1189-1200` (`SpecialsCapsulePath`, `MovementCapsulePath`, `Cooldown1_Icon...` carriers with hidden `RadialProgress`/lock overlay); `MeterRow` holds `UltimateMeter_BG`, `DefySeal` (`.tscn:250`), hidden `DefyLabel` (`:261`).
- `StoryHUD.Pulp.cs`: `BuildAbilities :371-410` builds 48 px `PulpAbilitySlot` lenses, the 64 px Ultimate slot (ring 4.5, outset 7, `ReadyPill`, `:95`, `:390-398`) and the 28 px Defy `HudBadge` (`:97`, `:403-408`). `RefreshIndicators StoryHUD.cs:1019-1041` per frame: `_indicators.ReadinessFraction/RemainingSeconds/LockState` -> `ApplySlot` (`Pulp.cs:677-688`: Ready/Cooling/Charging/Dormant/Suppressed, `Progress`, `SecondsRemaining`). `SyncCooldownAuthority :361-366` reads `player.SpecialOneCooldownTimer/SpecialTwoCooldownTimer/MovementAbilityCooldownTimer`. `HudAbilityIndicatorModel` (`StartCooldown :102`, `SyncCooldownRemaining :121`). Ultimate ring: `UpdateMeter :966-973`.
- `HudCapsule.cs` (pill, padding 7). `PulpAbilitySlot` (`scripts/UI/Pulp/`): `PulpSlotState :8`, `Progress :255`, `SecondsRemaining :256`, `Ring :301`.

### 2.2 Fighter HUD (current)
`scripts/UI/FighterHUD.cs`: `Slots = {SpecialOne, SpecialTwo, Movement}` `:51-54`; lens row `:330-380` (`LensGap 20`, `LensToChipGap 50`); `UpdateCooldowns :1219-1260` reads `runtime.SpecialOneCooldownFrames/SpecialTwoCooldownFrames/MovementCooldownFrames`, drives `PulpAbilitySlot` + kept `RadialProgress`; refusal flash `:1277-1278`. Defy seal: `DefyIcon` 32 px (`:329-335`), `ApplyDefySeal :803-820`, seam `DefySealFor :1089`; `FighterHudModel.cs:301-312`. Meter tube = `PulpBarDecor` (`Pulp/PulpBarDecor.cs`): boss `PhaseNotches :116` (`MaxNotches 4 :47`, white `NotchLine :287`), `TickCount 10 :45`.

### 2.3 Where the 4 pips go `[REC]`
- New code-drawn `FTT.UI.Pulp.MomentumPips` (`scripts/UI/Pulp/MomentumPips.cs`): `Value` 0-4 float (continuous fill within the pip, G1.1), `_Draw` primitives (same family as `HudCapsule`/`BlockSparkEffect`, no art), cached polygons, zero per-draw allocation, a completion bloom gated by `ComfortSettings`, tooltip/accessible name from `hud_momentum_pips`.
- Theme: pips need no Label role. Reuse existing tokens (`Res2`, `Accent`, `Ink3`, `GoldBright`) and the generator is untouched. If a role/colour is added it goes in `tools/ui_theme/build_ftt_theme.py` (`pal(...)` ~`:109`, label table ~`:403`), regenerated with `py tools/ui_theme/build_ftt_theme.py` (writes `UIPalette.Roles.cs` + `ftt_theme.tres`; never hand-edit); `UIThemeTests` (~`:809`) pins palette<->constant parity, the 70-role count and the 18 px floor.
- Story: a row under `SpecialsCapsule` inside `Abilities` (G5.1). `StoryHudCompactTests` pins slab 484 x 182, lens sizes and places -> the pip row either grows the slab/capsule (rewrite the size pins) or fits in the capsule's 7 px bottom padding + a ~6 px row. `[Q]` accept slab growth or hold 182? Title/objective sit under the slab (`StoryHudBoundsTests...StaysClearOfTheTooltipAndToastBand`) and move with it.
- Fighter: a row under `SlotRow` per panel; `FighterHudTopBandTests`/`FighterHudLayoutRulesTests` pin the top band.

### 2.4 Special lenses: reuse delay + lockout only, dim at 0 pips `[REC]`
- Cooling sweep becomes the **2 s per-slot reuse delay** (`Progress` = remaining/2 s, `SecondsRemaining` <= 2.0) plus the lockout. `PulpSlotState` has no starved look: append `Starved` (greyed icon, Ink3 ring, no sweep); `Dormant` (Levels 0-2, G4.1) outranks it. Movement keeps its 5 s sweep (G1.5).
- Story data path: replace the Special halves of `SyncCooldownAuthority` with new hero getters (`MomentumPips`, per-slot reuse remaining; names owned by recon A/B); `HudAbilityIndicatorModel` gets `SyncReuseDelay(slot, s)` + `SyncMomentum(pips)`. `OnCooldownStarted/OnCooldownComplete` (`StoryHUD.cs:235-236, 854-860`) either keep firing for the reuse delay or are replaced by a Momentum payload; the HUD already prefers per-frame polling of the hero's own values (since 2026-10-11), so poll. `[Q]` event vs poll.
- Fighter data path: `UpdateCooldowns` reads component 305 cooldown frames; those become reuse-delay frames and Momentum comes from the sim component (recon A); `FighterHudModel.CooldownRadialFraction` reused with a 120-frame start.
- `[Q]` a press with 0 pips should flash the lens (reuse `NotYetFlash`; today D10 flashes only lockout refusals, `FighterHUD.cs:1277`).

### 2.5 Notch at 50 on the Influence meter `[REC]`
- Fighter: `PulpBarDecor` already draws up to four notch lines from `PhaseNotches` (`:445-453`). Add a separate `GoldNotchFraction` (0.5) + `GoldNotchLit` drawn in `Accent`/`GoldBright` so it cannot collide with the white boss notches; lit when meter >= 50 and `gameplay_defy` is bound for the device kind (4.2). The tube's tick at 50 already exists. Spend feedback: `DropLagTo` already holds the tracker 0.5 s then drains 0.6 s.
- Story: the "bar" is the **ring** (`PulpAbilitySlot.Ring`, `RadialProgress`), not a tube. A 50 notch = a short radial tick at the half-way angle: add `NotchFraction`/`NotchColor` to `scripts/UI/RadialProgress.cs` (verify its start angle/direction) or draw it in `PulpAbilitySlot`. The 28 px Defy badge goes; its upper-right ring spot frees. `RadialProgress` has no tracker band: accept an immediate fall plus the burst ring.
- "Bound" check: `InputMap.ActionGetEvents("gameplay_defy")` per device kind; refresh on `InputBindingService.Generation` change (`InputBindings.cs ~949`).

### 2.6 Retiring the four-state seal - delete list `[REC]`
`scripts/UI/DefySealModel.cs`; uses at `StoryHUD.cs:96-97,179,248,256,282,670-688,1238`, `StoryHUD.Pulp.cs:96-97,136,157,174-175,399-409`, `FighterHUD.cs:62-69,100-110,146-147,258,329-335,681,801-820,1088-1090`, `FighterHudModel.cs:301-312`; nodes `StoryHUD.tscn:250,261`, `FighterHUD.tscn:166,379`; role `DefyLabel` (`UIPalette.Roles.cs:63,188`, generator `build_ftt_theme.py:403`) and palette `DefyReadyGlow` (`UIPalette.cs:291`, `Roles.cs:360`, generator `:109`, `UIThemeTests:809`); `EventBus.cs:427-447,794-799` (`DefySealState/Payload/OnDefySealChanged/RaiseDefySealChanged`); publisher `PlayerController.cs ~600-636`; `Level00Controller.cs:921-925`; listeners `CoreMechanicCuePresenter.cs:45,156,169,207-212`, `FirstUseTooltipPresenter.cs:121,133,327-331`; capture seeds `UiCaptureTargets.Huds.cs:88,113,245`. Kit icons `defy_ready/notready/spent/barred` (+`_bold`) in `assets/sprites/ui/glass/icons/`, `glass_kit.json`, `tools/asset_src/glass/glass_svg.py:409-436`, `build_contact_sheet.py:392-393`, `UiCaptureTargets.Widgets.cs:194-196`, `GlassKitContentTests`. `[REC]` keep one icon (`defy_ready`) for the first-use tooltip; delete the other three in the kit rebuild (`build_all.ps1`, takes the GdUnit lock) or defer art deletion to a cleanup step to limit blast radius.

---

## 3. Presentation
- Cues: `scripts/Core/CoreMechanicCueCatalog.cs:30-31` (`DefyToll`, `DefySealShatter`), lists `:61,:77`; data `resources/Audio/core_mechanic_cues.tres:62-76` (silent streams); played only on seal Ready->Spent (`CoreMechanicCuePresenter.cs:207-212`). `[REC]` add `CoreMechanicCueIDs.DefyBurst` ("defy_burst", Critical, silent), retire `DefySealShatter`, keep or drop `DefyToll`. `CoreMechanicCueCatalogTests` (6, 4 Defy refs) -> rewrite.
- Snapshot: `AudioSnapshotMixer.cs:34,135,154` `DefyOrScripted` (priority 6, `Silent(0.20f)`), currently raised by nothing. `[REC]` request it for the ~8-frame freeze window.
- Glow: `GlowStateModel.cs:43` already documents a "post-Defy" invulnerability aura at highest priority. `[REC]` reuse it for the 6 invulnerable startup frames; no new channel.
- Shockwave ring: pattern `scripts/Combat/BlockSparkEffect.cs` (pooled Node2D, `_Draw()` primitives, `Show(...)` registers its own pool `:71`, bright fills gated by `ComfortSettings.FullScreenFlashAllowed`). `[REC]` new `DefyBurstEffect`: gold ring expanding to 2.5 u x 62.5 = ~156 px, 0.4 s. Fighter: spawn from `FighterSimulationDriver` presentation using a wrapping burst counter in 323 `ActionFlags` bits (precedent: wall-bounce bits 8-11, `IsForwardCounterStep`, so a rollback never replays it). Story: spawn from the Defy execution.
- Screen pulse: `ComfortSettings.cs:88 DistortionAllowed`, `:104 FullScreenFlashAllowed`, `:111 ScreenTintPulseAllowed`. `[REC]` short `ColorRect` + `assets/shaders/ui_desaturate.gdshader` on a CanvasLayer gated by `ScreenTintPulseAllowed`; under Reduced Temporal Effects only the ring plays. Shake via `CameraShake` (accessibility scale applies).
- "DEFIED" label: damage numbers are int-only (`scripts/UI/FloatingDamageNumber.cs:50-78`, `damage.ToString()`; warded boss "0" uses a tint, `BossController.cs:663`). `[REC]` add `ShowText(string, position, parent, tint, scale)` (GoldBright, scale ~1.25, Punish precedent) fed by `Tr("defy_world_label")`; Story from the `PlayerController.cs:1816-1821` spawn site, Fighter from `FighterSimulationDriver.cs:831`. `[Q]` `Show` early-returns when `DamageNumbersVisible == false` (`:65`); should DEFIED ignore that toggle? (I suggest yes - it announces a state.)

---

## 4. Text (`localization/en.csv`)
| Key | Line | Fate |
|---|---|---|
| `systems_card_defy_title/_body` | 958-959 | rewrite body (active burst, 50 meter, button, heal) |
| `systems_card_momentum_title/_body` | 960-961 | rewrite for the gauge ("Land hits to build Momentum, spend it on Specials.") |
| `tutorial_step_defy`, `_spent` | 996-997 | rewrite (press-Defy objective / done line) |
| `hud_defy_not_ready/ready/spent/unavailable` | 1219-1222 | delete with `DefySealModel.LabelKey` (only reader) |
| `hud_defy_label` | 1671 | delete with the hidden node |
| `dlg_l00_station_defy` | 1535 | rewrite (resource `resources/Dialogue/level_00_dialogue.tres:78-82` keeps the id) |
| `tooltip_first_use_defy` | 1545 | rewrite, keep key (`FirstUseTooltips.cs:28,53`) |
| `movelist_ability_stats` | 935 | "Damage {0} - cooldown {1} s": Specials show cost + reuse delay instead |
| `status_suppression_tooltip` | 967 | drop "Cooldowns keep running." |
| `resonance_joan_piercing_cooldown_*` 1031-32, `resonance_mozart_wave_cooldown_*` 1103-04, `resonance_lincoln_splitting_cooldown_*` 1139-40 | | descriptions -> "+10% Momentum gain" (G4.3 placeholder) |
| `resonance_cleopatra_vortex_step_description` 1120 | | rule disabled until kit pass (G4.2) - flag |
| `fighter_hud_cooldown_special1/2/movement` 873-875, `hud_time_freeze_cooldown` 974, `test_arena_cooldowns` 257 | | keep |
| `controls_action_time_freeze` 971 | | precedent for `controls_action_defy` |

**Proposed new keys:** `controls_action_defy`; `defy_world_label` ("DEFIED"); `hud_defy_notch_ready`, `hud_defy_notch_building`; `hud_momentum_pips`; `hud_momentum_empty`; `tutorial_step_defy_press`, `tutorial_step_defy_done`; `save_notice_defy_pad_unbound`; `movelist_ability_momentum` (format with a comma - **must be quoted**); optional `fighter_hud_momentum_label`.

Rules: `TranslationCsvShapeTests` fails on an unquoted comma (Godot splits it silently); run `--headless --import` and commit `en.en.translation`. `UnusedTranslationKeyTests` (`:96-144`, ceiling 12) fails on a new key nothing references, and removing a still-referenced key fails `ScriptTranslationKeyTests`/`SceneVisibleTextTests`: retire keys with their readers and add keys with their code in the same step. `StoryHudLocalizationTests`, `CampaignLocalizationTests` and `NarrativeKnowledgeBoundaryTests` also sweep copy (Level 0 is safe).
- Systems Card: `scenes/ui/SystemsCard.tscn:117-137` nodes `DefyTitle/DefyBody/MomentumTitle/MomentumBody`; `SystemsCardContentTests` (2), `MoveListScreenTests` (3). Move List: `MoveListScreen.cs:422` formats `ability.BaseDamage, ability.CooldownDuration` for every row; Specials no longer have a cooldown rule. `[Q]` does `AbilityData.CooldownDuration` stay authored-but-unused for Specials (recon A)?
- First-use: `FirstUseTooltipPresenter.cs:327-331` fires on seal Ready/Spent -> rewire to "meter first reaches 50" / first Defy. Tooltips are one-shot per slot (`ShownFirstUseTooltips`), so a save that already saw `defy` never sees the new text. `[Q]` accept, or mint `defy_button`. The text should name the bound button (`PulpPromptGlyphs`).

---

## 5. UI capture targets to recapture (`scripts/Diagnostics/UiCapture/`)
- `UiCaptureTargets.Huds.cs`: seeds `:85-88` (`StartCooldown(Special2, 8f)`, `RaiseDefySealChanged(Building)`), `:113` (Ready); `huds_calm :121`, `_140 :129`, `huds_stress :138`, `huds_boss :146`, `huds_140 :159`, `huds_boss_140 :168`, `huds_low_first :183`, `huds_frozen_dial :196`, `huds_anchors :210`, **`huds_states :227` (Ult ready + READY pill + Defy armed + S1 cooling; `RaiseDefySealChanged(Ready)` `:245`)**, `huds_anchors_zero :254`, `huds_toasts :266/:279`, `huds_boss_names :292/:295`. Seeds break at compile when the seal API is removed.
- `UiCaptureTargets.Hudf.cs`: `hudf_versus :84`, `hudf_time_mode :93`, `hudf_sudden_death :102/:111`, `hudf_ko :120`, `hudf_140 :159`, `hudf_low_hp :168`, `hudf_statuses :185`, palettes `:212,:221`, long names `:230,:239` (all show the lens row and seal).
- `UiCaptureTargets.Widgets.cs:194-196` (`defy_*` icon tints) and the lens specimens (add a Starved specimen).
- New targets: `huds_momentum`, `huds_defy_notch` (bound/unbound), `huds_defy_burst` (ring + DEFIED), `hudf_momentum`. Needs a window and the GdUnit lock; compare with `compare_capture.py`; baseline set `D:/Projects/ftt-scratch/hud-compact/captures/`.

---

## 6. Bots / telemetry
- Cooldown coupling: `PlaytestBots.cs:804-822` (`TryConfirmSpecial` reads `SpecialOne/TwoCooldownTimer`), `:1061-1095` (Specials phase of the `feel` probe; `PlaytestFeel.cs:125,128` also read the timers - the frame-exact probe needs >= 1 pip per Special; start fill is 2 pips, so it works but audit the 702-715 lockout segment). `PlaytestWorld.SpecialGateFrames :178` (D1 gate) is unchanged (lockouts stay, G2.2).
- Press snapshot: `PlaytestTelemetry.cs:800-822` (`CooldownSeconds` from the timers); `PlaytestCombatStats.cs:106` `RefusalLabels` with `"cooldown"` (`:126`, `CooldownTickSeconds :96`). Add `"no_momentum"` and `"reuse_delay"`; drop/repurpose `"cooldown"`. `PlaytestCombatStatsTests` (9; 3 Defy, 4 cooldown) rewrite; `tools/playtest/compare_runs.py` (`:268-323`, `:609`) prints `defy/run` and `top_refusals` - update labels.
- Defy telemetry today: `PlaytestTelemetry.cs:599-606` feeds `_meter.Sample(.., StoryDefyHistoryUsed)`; `MeterTracker.DefyProcs` (`PlaytestCombatStats.cs:808-880`) counts the latch's rising edge; report `defy_procs` (`PlaytestTelemetry.cs:1567`). With the latch gone, count a new Defy-executed event/counter on `PlayerController` (frame, meter before, enemies pushed, HP healed).
- `[REC]` add read-only getters in PlaytestWorld's "Hero gates" region (`MomentumPips`, per-slot reuse remaining) and replace the three timer reads; new report block `momentum` (pips at level start - should be 2.0, the analogue of `meter0` - gained by source, spent per slot, starved presses, time at 0 and at 4 pips).
- **Should `play` press Defy? `[REC]` yes**, conservatively: meter >= 50, in hitstun or under a string, >= 2 enemies within 2.5 u (or HP < 35 %), at most once per 5 s, and not when a full meter has an Ultimate available and unthreatened (mirrors the Hard CPU rule, 4.4). `spam` never; `basics` only below 25 % HP; `bypass`/`feel` never. Needs only the new `GameplayButtons.Defy` bit (bots feed `PlayerInputFrame` through `SetInputSource`). Judge the rework with new metrics: hits taken in the 60 frames before/after, kills within 2 s, `died_within_3s` (the user's original complaint). Bot batches before/after are not comparable; plan a fresh reference pair in a clean worktree with the lock held. Bots start via `StartLevelDirect` (Levels 1-15), so the Level 0 lesson is covered only by `Level00FlowSceneTests`/`TutorialCalibration*Tests`.

---

## 7. Tests (case counts = `[TestCase]`)
**Delete / gut:** `Unit/DefySealTests` (5), `Unit/DefySealStateTests` (4), `Determinism/DefyProtectedRecoveryTests` (44 refs; sim owner decides rewrite vs delete), the seal cases in `Unit/StoryHudPulpTests` (15 cases, 10 Defy refs), `Unit/FighterHudTopBandTests` (6, 6 refs), `Unit/FighterHudLayoutRulesTests` (2, 7 refs).

**Rewrite in place:**
- input: `GamepadDefaultBindingTests` (6), `InputBindingSchemaTests` (20; bit pin, action counts), `GamepadOverrideRepairTests` (7), `TimeFreezeBindingMigrationTests` (2), `PadMenu*` pad-button checks, Settings Controls suites (`UrPolish2Tests` Controls columns at 1.0/1.4), rollback packet/protocol tests if v5.
- HUD: `StoryHudCompactTests` (9), `StoryHudContractTests` (3), `StoryHudPresentationTests` (8), `StoryHudBoundsTests` (4), `StoryHudLocalizationTests`, `HudAbilityIndicatorModelTests` (12; 27 cooldown refs), `FighterHudSceneTests` (12; 18 cooldown refs), `FighterHudModelTests` (11), `FighterHudContentTests`, `PulpAbilitySlotTests`, `PulpBarDecorTests`, `UIThemeTests`, `GlassKitContentTests` (if icons go), `MenuLayoutBoundsTests`.
- text: `SystemsCardContentTests` (2), `MoveListScreenTests` (3), `FirstUseTooltipsTests`, `CampaignLocalizationTests`, `UnusedTranslationKeyTests` (3), `CoreMechanicCueCatalogTests` (6).
- Level 0: `TutorialCalibrationTests` (8), `TutorialCalibrationV76Tests` (8), `TutorialCalibrationContentTests`, `Level00FlowSceneTests`, `LegacyUnlockScheduleTests` (add "Defy never gated").
- adjacent (sim/Story-owned, listed for overlap): `StoryDefenceOrderTests` (17 refs), `StoryMeterGainFactorTests`/`StoryMeterGainScaleTests` (0.35 -> 0.5), `StoryEnvironmentalDamageTests` (23 refs; extractor -20 -> -10), `SuppressionTests`, `StoryTumbleTests`, `FeelPassStoryTests`, `StoryRallyTests`, `StoryAttemptStateTests`, `SaveEnvelopeTests`, `LevelAttemptPersistenceTests`, `FighterHitPipelineTests`, `FighterMatchFlowTests`, `FighterVerbLayerTests`, `FighterLedgeGetUpTests`, `FighterTimeSystemsTests`.
- bots: `PlaytestCombatStatsTests` (9), `PlaytestEncounterTelemetryTests`.

**New suggested:** `MomentumPipsTests`, `DefyNotchTests` (Story ring + Fighter tube, lit only >= 50 and bound), `DefyBindingTests` (defaults F/R3, log on Back, collision repair, Controls row), `DefyPresentationTests` (ring spawn, DEFIED, comfort gating, rollback edge), `LensStarvedStateTests`, Story/Fighter pip layout, bot press-policy tests. Order of magnitude: about -50 cases / 12 files gutted, ~35 files rewritten, +45-60 new cases; reconcile exact arithmetic against the AGENTS.md validation section (baseline 3771).

---

## 8. Cross-cutting risks
1. Bit 15 is the last free `GameplayButtons` bit; decide wire-widening vs spending it, and the protocol bump.
2. Orphan hygiene: retire `DefySealModel`, its keys, kit icons and capture seeds in one step (`UnusedTranslationKeyTests`, compile errors in `UiCaptureTargets.Huds.cs`, `GlassKitContentTests`).
3. Compact Story HUD is pixel-pinned (484 x 182 slab, 48/64 px lenses, 28 px badge); the pip row changes capsule/slab height and the title/objective placement.
4. Level 0 lesson: `Level00Controller.cs:864-925` (`BeginMeterAndDefyLesson` fills to 100, publishes seal Ready, arms a scripted lethal hit that passive Defy refuses) and `TutorialCalibration.cs:193-245` (`RegisterDefyProc`, `SkipDefyLesson`) are rewritten: set meter 50, drone swarm lock, objective `tutorial_step_defy_press` naming the bound button (keyboard F), complete on the Defy-executed event, show blast + heal. The `_player.StoryDefyHistoryUsed` mid-level-resume fallback disappears with the latch.
5. Time Freeze's read is global `Input.IsActionJustPressed`; Defy must use the per-device frame path in both modes.
6. Recapture needs a window plus the GdUnit lock; keep unchanged target names so `compare_capture.py` pairs 1:1.
