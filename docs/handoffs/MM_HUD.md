# MM-HUD handoff (meter and Defy update, Phase B1)

Branch `mm/hud` (worktree `D:/Projects/ftt-wt/mm-hud`), cut from `9cba2b9a` (MM-RULES merged).

## 1. Shipped (exact names)

**`FTT.UI.Pulp.MomentumPips`** (`scripts/UI/Pulp/MomentumPips.cs`, new): a code-drawn `Control`, no art, no theme role (reuses `UIPalette.Accent`, `GoldBright`, `Ink3`, `Edge`).
- `Value` (float pips 0..4, NaN reads 0), `Dimmed`, `FullPips`, `Blooms`, `GeometryBuilds`; statics `PipCount` (= `BasicComboRules.MomentumMaxPips`), `RowHeight` 9, `PipGap` 5, `DimAlpha` 0.4, `PipFill(pips, index)`, `RowWidthFor(pipWidth)`.
- Four stadium pips, each filling continuously left to right (clipped with `GlassGeometry.ClipHalfPlane`), a full pip `GoldBright`, a partial one `Accent`. Polygons rebuilt only on a size or value change; a redraw allocates nothing. One `PulpMotion.Bloom` when a pip completes by an increase of under one pip (a seed or a restore jumping the gauge does not bloom; skipped under Reduced Temporal Effects).

**`FTT.UI.DefyNotchRules` and `DefyBindingProbe`** (`scripts/UI/DefyNotchRules.cs`, new): `NotchFraction` (= `DefyInfluenceCost / MaxValue` = 0.5), `DefyAction` ("gameplay_defy"), `IsLit(meter, bound)`, `IsBound(joypad)` (reads `InputMap`; false if the action does not exist, so it works before MM-INPUT lands), `PlayerUsesJoypad(player)`. The probe caches `IsBound` and re-reads only when `InputBindingService.Generation` or the device kind moves.

**`PulpAbilitySlot`**: `PulpSlotState.Starved` and `PulpRingMode.Dim` appended; `Derive(Starved)` is saturation 0.2, brightness 0.45, an `Ink3` full ring, and the sweep and seconds only while a reuse delay runs (an Ultimate Starved reads as Charging); `NotchFraction` / `NotchLit` push to the ring.
**`RadialProgress`**: `NotchFraction` (negative = none), `NotchColor`, `NotchWidth` 2, `NotchOverhang` 1.5; a short radial tick across the ring at its share (clockwise from 12 o'clock, so 0.5 is 6 o'clock). Ring mode only.
**`PulpBarDecor`**: `DefyNotchFraction` (negative = none), `DefyNotchLit`: a 3 px line standing 5 px proud of the tube (gold `GoldBright` with a glow when lit, `Ink3` unlit), apart from the boss bar's white phase notches (which are boss-style only and have a diamond).
**`HudAbilityIndicatorModel`**: `SpecialReuseDelaySeconds` (2), `MomentumPips` (default 2.0, `Reset` restores it), `SyncReuseDelay(slot, seconds)` (Specials only; the denominator is the fixed 2 s), `SyncMomentum(pips)`, `IsStarved(slot)` (Special, unlocked, under one pip); `IsReady` is false for a starved Special. `SyncCooldownRemaining` stays for the movement ability.
**`FighterHudModel`**: `MomentumPipFill(int units)` (units / 7200, clamped 0..4), `PipRowTop` 278, `PipRowWidth` 128, `TopBandHeight` 273 -> 287.
**Story HUD**: `StoryHUD.SpecialsColumnPath`, `MomentumPipsPath`; `Pips`, `UltimateRing`, `InvalidateDefyBinding()` seams; the pip row follows `Indicators.MomentumPips` and dims while both Specials are Dormant; the Ultimate lens ring carries the Defy notch (lit at meter >= 50 and bound for player 0's device kind); `SyncCooldownAuthority` now reads `PlayerController.SpecialReuseRemainingSeconds(slot)` and `MomentumPips`; `ApplySlot` gains `starved` with precedence Dormant > Suppressed > Starved > Ready > Cooling/Charging. The 0-pip flash needs no HUD change: `OnAbilityLockoutRefused` already flashes any Special / Ultimate lens, so MM-SMOM raising the event for a 0-pip refusal (plan 2.2) is all it takes.
**Fighter HUD**: `ApplyPlayerState(..., float echoPool = 0f, int momentumUnits = MomentumStartUnits)`; the driver call passes `TryGetMomentum`. Pips are a top-level `P1_Pips` / `P2_Pips` child of `SafeArea` (made in `BuildWidgets`, positioned in `ApplyLayout` under the two Special lenses, the Specials on the outer edge for P2); the Specials' sweep uses the fixed 120-frame denominator; a Special under one pip is Starved; the meter tube carries the Defy notch (lit at influence >= 50 and bound). Seams: `MomentumPipsFor(i)`, `MeterDecorFor(i)`, `SlotWidgetFor(i, slot)`.
**Removed**: both Defy seal widgets and nodes (`StoryHUD.cs`/`.Pulp.cs` seal fields, handler, badge and `DefySeal` seam; `StoryHUD.tscn` `DefySeal` and `DefyLabel`; `FighterHUD.cs` seal panel members, icon, `ApplyDefySeal`, `DefySealFor`; `FighterHUD.tscn` two `DefySeal` labels); `hud_defy_label` deleted from `en.csv`.
**Scene**: `StoryHUD.tscn` gains `Abilities/SpecialsColumn` (VBox: an 8 px `AlignSpacer`, the `SpecialsCapsule`, `MomentumPips` 9 px). The capsule's children moved with it.
**Captures**: seal seeds removed; `huds_calm` / `huds_states` use `SyncReuseDelay` and `SyncMomentum`; new `huds_momentum`, `huds_momentum_starved`, `huds_defy_notch` (bound), `huds_defy_notch_unbound`, `hudf_momentum`; `hudf_*` reuse seeds are now under 120 frames. Not captured (orchestrator, window and lock).
**en.csv**: `# MM-HUD block` with `hud_momentum_pips,Momentum`.

## 2. Files changed outside the matrix
- `tests/Unit/FighterHudTopBandTests.cs`, `FighterHudSceneTests.cs`, `StoryHudContractTests.cs`, `StoryHudCompactTests.cs`, `StoryHudPulpTests.cs`, `tests/ContentValidation/FighterHudContentTests.cs`, `tests/Pulp/PulpAbilitySlotTests.cs`, `tests/Unit/HudAbilityIndicatorModelTests.cs`: all in the MM-HUD row of plan 4.1.
- Kept for MM-SWEEP (plan 1.9): `DefySealModel`, the `EventBus` seal members, the `DefyLabel` role, `DefyReadyGlow`, the `defy_*` kit icons, and `FighterHudModel.DefyIconName` / `DefyIconTint` (still used by `GlassKitContentTests` and `FighterHudLayoutRulesTests`).

## 3. Decisions
- Pips sit in a `SpecialsColumn` VBox with a spacer so the three groups stay centred on one line; the slab grows 182 -> about 189-190 (provisional, `PROVISIONAL-MOMENTUM-HUD-LAYOUT`). Pips 9 px tall, 5 px gaps, 3 px under the capsule.
- Starved look: saturation 0.2 / brightness 0.45 / `Ink3` ring (P15). The Defy notch: radial tick 2 px; tube line 3 px, 5 px proud.
- The Fighter pips sit under the key labels (y 278, a 5 px gap), not above the lenses, per plan 2.10; the top band grows to 287.
- `ApplyPlayerState` gets an optional last parameter so the tests and captures feed units directly; the HUD never reaches into the sim.
- `PlaceTitleRow()` now also runs every `_Process` (guarded setters): the slab outgrew its authored rect after the last `Resized`, leaving the title 8 px short. A `MinimumSizeChanged` hook looped (title height exploded) and was dropped. The scene's `TopLeft_Panel` / `TopLeft` offsets moved 7 px to match.

## 4. Deviations
- Plan 4.1 counts "StoryHudPulpTests seal cases -2"; the file had exactly one seal case (`TheDefySealShowsADifferentShapeInEachState`). The second seal case the plan has in mind lives in the other workstreams' files (`DefySealStateTests`, `DefySealTests`). Net MM-HUD delta is therefore +10, not +9.
- The 0-pip flash has no new code in the HUD (it is the existing event path); the unit coverage is the model/lens cases, and the event-to-flash path was already pinned by `TheNotYetFlashPlaysTheCircleRoundTheLensRing`.

## 5. Tests
New: `MomentumPipsTests` 3 (fill rule and clamps; bloom once, none on a seed jump; polygons rebuilt only on a size or value change), `DefyNotchTests` 3 (Story ring lit at 50+ and bound only; Fighter tube notch distinct from the boss bar's; unbound stays dim on both HUDs), `StoryHudCompactTests.TheMomentumPipsSitInARowUnderTheSpecialsCapsule...` +1, `StoryHudPulpTests.TheSpecialLensesShowTheReuseDelayAndDimAtZeroPips` +1 (and `TheDefySealShowsADifferentShapeInEachState` -1), `HudAbilityIndicatorModelTests.ASpecialsReuseDelayIsTwo...` +1, `FighterHudSceneTests.TheMomentumPipsAndTheSpecialLenses...` +1, `PulpAbilitySlotTests.AStarvedSpecialIsDimmer...` +1. Delta +10 (plan +9).
Rewritten in place (0 delta): `StoryHudCompactTests` (slab 484 x 182 -> 189, test renamed `...By189...`; the cluster bound 260 -> 270, renamed `...By270`; the "one slab" list swaps `DefySeal` for `MomentumPips`), `StoryHudContractTests` (the `SpecialsColumn` paths, no `DefySeal`), `FighterHudTopBandTests` (no Defy icon, pips present, tube carries the notch), `FighterHudContentTests` (no `DefySeal` node).
Seen failing (mutations, then reverted, one batch each): bloom guard removed, geometry cache removed, `PipFill` offset by 0.5, `IsLit` ignoring `bound`, `IsStarved` never true, the reuse denominator = remaining, the Starved look equal to Cooling -> `MomentumPipsTests` 3/3, `DefyNotchTests` unbound, `PulpAbilitySlotTests` Starved, `StoryHudPulpTests` reuse case, `HudAbilityIndicatorModelTests` reuse case (7 failed); Fighter starved threshold 0, `MomentumPipFill` / 7000, the pip-row dim `&&` -> `||` -> `FighterHudSceneTests` and `StoryHudPulpTests` (2 failed). The compact pips case, the Fighter top-band additions and `DefyNotchTests` Story/Fighter lit cases fail on the pre-change tree because the nodes and members do not exist; no separate mutation was run for them.
Runs (lock held, `dotnet test --filter`): MM-HUD set (MomentumPips, DefyNotch, StoryHud*, FighterHud*, HudAbilityIndicatorModel, PulpAbilitySlot, PulpBarDecor, MenuLayoutBounds, UIThemeTests, CampaignLocalization, ScriptTranslationKey, UnusedTranslationKey, TranslationCsvShape, SceneVisibleText): `Failed: 0, Passed: 158, Total: 158`. Broad Pulp set (`FTT.Tests.Pulp`, UiCapture, RadialProgress, DefySeal, UiChromeCyan, UiLegacyKit, UiShaderModulation, GlassKitContent, UrPolish, TestArena, FighterStageConformance): `Passed: 118, Total: 118`. No full suite run.

## 6. Measurements
Story slab at 100 %: 484 x about 190 (was 182); the title row follows it (top 222). Fighter pips: y 278..287 at 100 %, 128 px wide.

## 7. Not delivered / open
- Captures were not taken (orchestrator, after merge). The Fighter HUD cases run against the driver stub (`TryGetMomentum` returns the start fill) until MM-SIM merges; re-run `FighterHud*` after that merge.
- Provisional: the 189-190 px slab, the pip look and size, the starved brightness, the notch sizes.
- Nobody played the build.

## 8. For the docs
- `HUD_CONTRACT.md`: replace the Defy seal with the Momentum pips and the notch at 50 on the Ultimate ring / tube; the Special lenses show only the 2 s reuse delay and the lockout and dim at under one pip; Story slab about 484 x 190; Fighter top band to 287.
- Ledger: `USER-MOMENTUM-HUD`, `USER-DEFY-HUD-NOTCH`, `PROVISIONAL-MOMENTUM-HUD-LAYOUT` (amends `USER-STORY-HUD-COMPACT` / `PROVISIONAL-STORY-HUD-COMPACT`); AGENTS.md: the compact Story HUD bullet's size and seal text.
