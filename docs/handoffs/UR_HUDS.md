# UR-HUDS handoff (pulp UI redesign, Phase B)

Branch `ur/huds`, from `main` at `22219329`. The Story HUD is rebuilt as the HUD-S1 corner cluster and the boss bar as BOSS-B. No edit to the theme, `UIPalette*`, `Motion/*`, `Pulp/*`, `AGENTS.md`, `CLAUDE.md`, the ledger or the contracts. **`localization/en.en.translation` is not committed: regenerate it with `--import` after merging.**

## 1. What shipped

**Scene** `scenes/ui/StoryHUD.tscn` (regenerated; nesting changed, node names kept):

- `SafeArea/TopLeft` (title row at 44, 20): `LevelTitle` (role `LevelTitle`), `Objective` (role `ObjectiveLine`; a `StyleBoxEmpty` left margin of 32 holds a `flag` `PulpIcon` child, shown only while an objective is set). Hidden while the boss bar shows (UI_SPEC 4.4 stress frame).
- `SafeArea/TopLeft_Panel` is now a VBox at (32, 108), 26 px between three panels:
  - `VitalsPanel` (`PulpTaggedPanel`, tag `hud_vitals`) / `Plate` (InkPanel, 620 min) / `Pad` / `Stack`: `Top` (`PortraitHost` 112 holding `PlayerPortrait` and a `PulpPortraitRoundel`; `Vitals`: `HeroName` (role `HeroName`, the character's `DisplayNameKey`), `HealthBar_BG` 380 x 30 (variation `PulpBar`, `PulpBarDecor`, `HPText` in `HudNumeralSmall` over it, `RallyEcho_Band` kept hidden), `BlockCharges_Panel` with 30 px `shield` `PulpIcon` pips, gap 7) and `StatusEffects_Panel` (two 200 x 44 hosts, gap 30).
  - `AbilitiesPanel` (tag `hud_abilities`) / `Plate` / `Pad` / `Abilities`: `MeterRow` (`UltimateMeter_BG` 116 holding the Ultimate `PulpAbilitySlot`; `DefyStack`: `DefySeal` 34 and `DefyLabel` in role `DefyLabel`) and `CooldownRow` (`Cooldown{1,2,3}_Icon`, 74 each, gap 30).
  - `TimePanel` (plain InkPanel) / `Pad` / `TemporalRow`: `RewindCounter` (`RewindIcon` 46 + `RewindCountText`, role `RewindCount`), `BeaconAnchors` (`BeaconIcon` 46, `AnchorPips`), `TimeFreezeIndicator` (`FreezeIcon` 46 + `FreezeColumn`: `FreezeSubLabel`, `FreezeRemainingText`).
- `SafeArea/TopRight_Panel` (right edge 44, top 34, 35 px between): `DustPlate` / `Pad` / `CurrencyContainer` (`ChronalDustIcon` 38, `ChronalDustText` role `HudNumeral`, `DustPickup_Float` role `DustFloat`) and `IntegrityClock` (138 x 150): `ClockFace` (dial back), `ClockWedge`, ticks layer, `SiphonStreams` (Label kept, invisible), a `SiphonDraw` layer, `IntegrityCaption`, `SealedNote`, `IntegrityText` (role `HudNumeral`).
- `SafeArea/BossPanel` is a plain Control laid out in code: `BossName` (label with the `boss_nameplate` plate as its `normal` box, -1 degrees), `BossPhaseTab` (`boss_phase_tab` plate, +2 degrees), `BossBar` (`PulpBossBar`, `PulpBarDecor` Boss style, phase notches from `BossPhaseNotchOverlay.Positions`) with `BossHpValue` (role `BossHpValue`) and `PhaseNotches`. `BossIntroCard` and `CheckpointToast` wear the kit plates (`boss_nameplate`, `tag_panel`); the tremor overlay is unchanged.

**Code** (`scripts/UI/`): `StoryHUD.cs` (events, refreshers, API unchanged), new `StoryHUD.Pulp.cs` (widget build, `ApplySlot`, `ApplyChip`, boss layout, per-corner fit; test seams `VitalsPanel`, `AbilitiesPanel`, `SlotWidgets`, `UltimateSlotWidget`, `StatusChips`, `HpDecor`, `BossDecor`, `SiphonDraw`, `DialShown`, `BossPhase`), `HudBadge` (46 px sticker badge with an optional ring: rewind and Time Freeze), `SiphonStreamsLayer` (one fanned zig-zag per living Extractor between UI_SPEC polylines A and B, capped at 5 drawn), `IntegrityDialLayer` (the `dial_back` / `dial_ticks` 2x bakes at half size). Edited: `RadialProgress` (a `Pie` mode with an inner ring; default fill `Res2`), `BossPhaseNotchOverlay` (`DrawNotches`, false when the decor draws; then the phase flash is a gold wash), `BeaconAnchorIcon` (a `PulpIcon` `anchor` / `anchor_hollow`), `AnchorPip` (a `PulpIcon` `anchor_pip` / `anchor_pip_cracked`; `PipSize` 8 to 18, new `IconSize` 46; **AnchorPip.cs is not in the brief's owned list; only StoryHUD uses it**), `HudAbilityIndicatorModel` (`StatusIconName`, `StatusChipKind`), `DefySealModel` (`IconName`, `PulpTint`): additions only, source compatible for UR-HUDF.

**Behaviour.** HP: `DropLagTo` on every update (the first seeds the tracker), `Low` at 25 % or less (provisional) with the fill override `Danger` (a duplicated flat fill; the same `TintFill` route Fighter uses), the Rally echo as `EchoValue = HP + echo`. Slots: model state to `PulpSlotState` (locks win; ready, else cooling, or charging for the Ultimate), written only when changed. Chips: damage left, control right, the empty one a dashed ghost. Defy: four distinct icons with the spec tints. Rewind: `x{0}`, glyph and number `Danger` at 1 or 0 left, the badge dims at 0. Time Freeze: ready `Res2` with no ring, active ring on `seconds / 5`, cooldown `seconds / 45`, the long keyed strings kept as tooltip and accessible name. Dust: the bare number (`N0`), the long form as its tooltip. Dial: the wedge is `Danger` (slate when frozen), siphon streams only while living Extractors drain it and it is not frozen, the dial gives way to `SealedNote` while `StoryManager.IsPreBossLocked`.

**Glyph Labels** (`DamageStatus_Indicator`, `ControlStatus_Indicator`, `DefySeal`, `Cooldown{N}_Icon`, the lock overlays, `RewindIcon`, `FreezeIcon`, `ChronalDustIcon`) keep `Text` and `TooltipText`; `VisibleCharacters = 0`; the widget is their child. The not-yet flash plays on the label (frame, tint) and on the slot widget (tint); the frame is a later sibling, so it draws over the slot. The legacy radials and `UltimateMeter_Fill` keep their values and stay hidden.

**Localization.** New block `# UR HUDS`: `hud_vitals`, `hud_abilities`, `hud_defy_label`, `hud_integrity_caption`, `hud_integrity_sealed`, `hud_time_freeze_name`, `hud_seconds_short`, `hud_percent`, `hud_rewind_count`, `hud_boss_phase`. No existing English value changed (the old long strings now feed tooltips and accessible names, so no key went unused).

## 2. Tests

`[TestCase]` delta **+11**: `StoryHudPulpTests` 10 (build and glyph Labels, slots by cooldown, lock and meter, the not-yet flash over the slot, chips and distinct status icons, Defy shapes, HP decor, time tools and Beacon, siphon fan and frozen, boss nameplate and phase tab, objective flag) and `StoryHudBoundsTests` 1 (UI scale 0.9 / 1.0 / 1.4: every cluster inside the frame, no overlaps among the left column, the dust plate, the dial and the boss bar, the boss width between its limits, the phase tab inside the bar). 3602 + 11 = **3613**; full run `Failed: 0, Passed: 3613, Total: 3613`, 2 m 20 s, lock held and `testhost` drained. Build 0 errors, no new warnings.

**Changed in place (no count change):**

- `StoryHudContractTests.ContractNodes`: every `TopLeft_Panel/Vitals/...` path moved into the panel nesting (`VitalsPanel/Plate/Pad/Stack/...`, `AbilitiesPanel/Plate/Pad/Abilities/...`, `TimePanel/Pad/TemporalRow/...`), `MeterRow/DefySeal` became `MeterRow/DefyStack/DefySeal`, `TopRight_Panel/CurrencyContainer` became `TopRight_Panel/DustPlate/Pad/CurrencyContainer`. Reason: the three-panel grouping the spec draws needs the plate containers.
- `StoryHudPresentationTests`: the same paths (portrait, bar, echo band, pips, rewind text, dust text, cooldown row and meter host in the not-yet case).
- `StoryHudBeaconAnchorTests`: `TemporalRow` path; the icon pin 24 x 24 became `AnchorPip.IconSize` (46) and `AnchorPip.PipSize` 8 became 18 (the bigger sizes the brief asked for).
- `DustCounterPersistenceTests`, `IntegrityClockTests`: paths only.

`EncounterLockCameraRatchetTests` (no `EncounterLockPanel`), `InfluenceMeterSourceScanTests`, `UnusedTranslationKeyTests`, `SceneVisibleTextTests` and the level suites that read `SafeArea/TopLeft/Objective` pass unchanged.

## 3. Provisional values and kit gaps

- Low HP threshold 0.25 (UI_SPEC leaves it to data); the boss bar bounds (left edge = left cluster + 24, right edge = viewport / scale - 356, width 320 to 840, centred in the band); the 5-stream cap; the dial's sealed note text; the 200 px chip hosts; the Vitals / Abilities / time padding (14 / 10 / 8, 14 / 2 / 2, 14 / 4 / 4 on top of the plate's own margins) were tuned by eye against the mix, not measured.
- The Rally **reclaim flash** has no pulp equivalent (the old tint of the echo `ColorRect`); `OnRallyEchoChanged` is a no-op. The band itself shows.
- The status ring's remaining share is still `seconds / 10` (statuses publish no original duration).
- No kit plate for the toast and intro card beyond reusing `tag_panel` and `boss_nameplate`; the checkpoint toast text role is `StateLabel` (no dedicated role).
- A boss name too long for the available width is shrunk with the label `Scale` (0.5 floor), not re-fonted (no per-node font sizes).
- At 1.4 the boss bar is about 340 units wide (floor 320); the phase tab overlaps the nameplate there.
- `HudBadge` draws its own disc because `PulpStatusChip` forces damage / control / suppression glyph colours.
- The Time Freeze indicator's `FreezeRemainingText` and the siphon Label sit at changed paths / are invisible (see section 1).

## 4. Captures

`D:\Projects\ftt-scratch\ur-captures\huds\`: `huds_calm`, `huds_stress`, `huds_boss`, `huds_140` (targets in `UiCaptureTargets.Huds.cs`, windowed, lock held), compared with `mix_story.png`. **Matches:** the three tagged panels with the rotated gold tags, the portrait roundel with the hero name, the green hatched HP bar with the numeral and ticks (the low state red, the tracker as the `HpLag` strip), the shield pips, the status chip and its dashed empty ghost, the Ultimate ring and READY / cooling / dormant / suppressed slots with key tags, the Defy stack, the rewind and Time Freeze badges, the dust plate, the stopwatch dial with the crown and the cyan streams, the nameplate / PHASE tab / skewed boss bar with its notch, the title row with the flag. **Differs:** the mix's wedge is a plain cream face (it draws no wedge in the calm frame; the spec's wedge is `Danger` and I follow the spec); the numerals are not outlined as heavily as the mix's; the dust plate has no `+N` in the captures (only shown on pickup); the Ultimate art is the dark VFX crop, so the mix's bright core is absent; the mix's panel gaps and heights are a few pixels different. **Not seen:** the low-HP glow pulse mid-cycle, a real Rally echo band, the tracker mid-drop, the sealed-note state, the tremor wash, the boss intro card, the checkpoint toast, the HUD over real gameplay, a pad session, Reduced Temporal Effects.

## 5. Drafted amendments

**`HUD_CONTRACT.md` (Story HUD).**

- Status slots (`:5-16`): the two slots become 44 x 44 `PulpStatusChip`s (badge 44, a 5 px duration ring, name and time beside it, 30 apart); an empty slot keeps its place as a dashed ring with the text at 0.35.
- Cooldowns (`:18-20`): the three 20 x 20 radials are replaced by 74 x 74 `PulpAbilitySlot`s (a sweep from 12 o'clock over the elapsed share and the remaining seconds as the centre number; Dormant and Suppressed shown, never hidden) with S1 / S2 / MOV key tags; the Ultimate is a 92 px frame in a 116 px ring (meter percent while charging, a full gold ring and a READY tag when ready, a cold ring when suppressed).
- Beacon (`:22-24`): the icon is 46 x 46 (`anchor`, `anchor_hollow` at zero), the pips 18 x 18 (`anchor_pip`, cracked on a Snap), up to three, right of the rewind badge and before Time Freeze; the accessible label is unchanged.
- Time tools (new row): rewind badge 46 (`hourglass`, `x{n}`, danger at one or none) and Time Freeze badge 46 (`freeze`; Ready `Res2`, Active a ring on the remaining 5 s with the seconds, Cooldown a ring on the remaining 45 s) in their own panel under the Abilities panel.
- Dust (`:26-30`): a plate at the top right, glyph 38, the bare number; `+N` fades after one second.
- Boss bar: top centre, at most 840 wide and at least 320 (between the left cluster and the dust plate at any UI scale), a rotated nameplate, a PHASE tab (`Phase {n}`), a skewed bar with the accent tracker, 6 px phase notches and the HP value; the level title and objective hide while it shows.
- Integrity: a 112 px stopwatch dial with the `Danger` wedge, the percent beneath, the caption to its left, one siphon stream per living Extractor, and the sealed note in its place at the pre-boss checkpoint.
- Acceptance at 90 to 140 % UI scale: the clusters stay inside the frame and overlap neither each other nor the boss bar (`StoryHudBoundsTests`).

**`design-godot.md` Story HUD lines** (recon B section 3: `:3371`, `:3375`, `:3516-3528`, the node sketches `:3426-3507`): top-left becomes three tagged panels (Vitals, Abilities, time tools) under the level title; "Ultimate Meter (circular or smaller bar)" becomes the ringed circular slot; Story sizing becomes HP 380 x 30, slots 74 / 92 (ring 116), badges 44 / 46, pips 30, Defy glyph 34, dust glyph 38, dial 112, boss bar 840; the sketches gain the panel nesting above.
