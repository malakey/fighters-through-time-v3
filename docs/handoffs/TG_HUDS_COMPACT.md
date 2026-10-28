# TG-HUDS-COMPACT handoff: the compact one-panel Story HUD (2026-10-27)

Branch `tg/hud-compact` from main `50f38282`. Brief: `D:\Projects\ftt-scratch\hud-compact\BRIEF_HUD_COMPACT.md`; the approved
look: `make_compact.py` (`variant="wells"`) and `png\v2_wells_*.png` beside it.

The user's words: "its covering up way too much of the screen now. I think we can combine the 3 sections into one and
reduce the size of everything", then "Ultimate on its own and bigger, then the 2 specials, then movement on its own",
grouping "A" (the Specials share one recessed capsule, Movement has its own). This reverses round 3's "bigger sizes" for
the Story HUD only. **The Fighter HUD is unchanged: the `hudf_versus` capture is pixel-identical to
`tg-captures/final/hudf_versus.png` (PIL difference bounding box `None`).**

## 1. What changed

### Scene tree (`scenes/ui/StoryHUD.tscn`)

Before (TG-HUDS): `SafeArea/TopLeft` (title, objective) above `SafeArea/TopLeft_Panel` (a VBox of three slabs:
`VitalsPanel` (a `PulpTaggedPanel` around `Plate`, `GlassPanelVitals`), `AbilitiesPanel` (tagged, `GlassPanelAbilities`)
and `TimePanel` (`GlassPanelTimeTools`, `Pad/TemporalRow`)).

After (`S` = `SafeArea/TopLeft_Panel/HudPanel/Pad/Stack`):

```
SafeArea/TopLeft                 VBox (32, 214), placed in code 12 px under the slab, indent 8; separation 4
  LevelTitle, Objective          unchanged paths (the level controllers and their tests bind them)
SafeArea/TopLeft_Panel           VBox at (24, 20): its rect is the slab's
  HudPanel                       PanelContainer, GlassPanelHud, min width 484
    Pad                          MarginContainer, right margin 2 (the slab pads 12 / 14+2 / 12 / 14)
      Stack                      VBox, separation 12
        Top                      tier 1, HBox 14
          PortraitHost (68)/PlayerPortrait
          Vitals                 VBox 9, centred
            HpRow                HBox 14: HealthBar_BG (296 x 22; RallyEcho_Band, HPText, HPMax), BlockCharges_Panel (HBox 4)
            StatusEffects_Panel  HBox 16: DamageStatus_Indicator, ControlStatus_Indicator (each 38 tall, with its radial)
            HeroName             kept, hidden (its text is still written; it draws nothing)
        Abilities                tier 2, HBox 12
          MeterRow               Control 83 x 78
            UltimateMeter_BG     (11, 7) 64 x 64 (UltimateMeter_Fill, UltimateMeter_LockOverlay)
            DefySeal             (61, -5) 28 x 28: the Defy badge on the Ultimate ring's upper right
            DefyLabel            kept, hidden (keeps `hud_defy_label` referenced)
          SpecialsCapsule        HudCapsule: CooldownRow (HBox 9): Cooldown1_Icon, Cooldown2_Icon (48 x 48 each)
          MovementCapsule        HudCapsule: CooldownRow: Cooldown3_Icon
          TimeSpacer             expands
          TimeDivider            1.5 x 58 (the hairline is a GradientTexture2D child added in code)
          TimeTools              VBox 8
            RewindRow            HBox 8: RewindCounter (HBox 4: RewindIcon 26, RewindCountText), BeaconAnchors (HBox 4: BeaconIcon 26, AnchorPips HBox 3)
            TimeFreezeIndicator  HBox 8: FreezeIcon 26, FreezeColumn (FreezeSubLabel kept hidden, FreezeRemainingText)
SafeArea/TopRight_Panel          VBox, right 24, top 20, separation 18 (was 44 / 34 / 40): DustPlate (paths kept), IntegrityClock 78 x 90
SafeArea/BossPanel               laid out in code (640 wide at 100 %)
```

Leaf names are kept. `FreezeGap`, `DefyStack` and the two `PulpTaggedPanel`s are gone. The hidden `HeroName`, `DefyLabel` and
`FreezeSubLabel` nodes stay so their keys are not orphaned (`UnusedTranslationKeyTests`) and scripts that write them keep
working; they draw nothing. **No translation key was added**, so `localization/en.en.translation` is untouched.

`StoryHUD.cs` now exposes its paths as public constants (`TopLeftPanelPath`, `HudPanelPath`, `HudStackPath`, `VitalsPath`,
`HpRowPath`, `AbilitiesPath`, `SpecialsCapsulePath`, `MovementCapsulePath`, `TimeToolsPath`, `RewindRowPath`,
`CooldownIconPaths[3]`, `TopRightPanelPath`, `CurrencyPath`); the tests bind them.

### New constants (`StoryHUD.Pulp.cs`, all at UI scale 1.0)

| Constant | Value (was) |
|---|---|
| `HudPanelWidth`, `PortraitSize` | 484, 68 (112) |
| `HpTubeWidth`, `HpTubeHeight`, `HpTubeRun` | 296, 22, 9 (380, 30, 12) |
| `HpValueRightPad`, `ChipInset` | 11 (14), 0 (4) |
| `ChipBadgeSize`, `ChipRingWidth`, `ChipRingOutset`, `ChipTextGap`, `ChipColumnGap`, `ChipGlowSize`, `ChipRowHeight` | 30 (44), 2.5 (3), 4 (5), 9 (12), 2 (4), 10 (14), 38 |
| `AbilityLensSize`, `AbilityRingWidth`, `AbilityRingOutset`, `AbilityCenterSize` | 48 (74), 2.5 (3), 5 (6), 18 |
| `UltimateLensSize`, `UltimateRingWidth`, `UltimateRingOutset`, `UltimateCenterSize` | 64 (92), 4.5 (5), 7 (9), 21 |
| `DefyBadgeSize`, `DefyGlyphSize` | 28, 20 (a 34 px icon with a label) |
| `TimeBadgeSize`, `TimeBadgeRingWidth`, `TimeBadgeRingOutset` | 26 (46), 2 (3), 3 (5) |
| `DividerWidth`, `DividerHeight`, `TitleRowGap`, `TitleRowIndent` | 1.5, 58, 12, 8 |
| `ObjectiveFlagSize`, `ObjectiveFlagGap` | 18, 8 (the toast keeps 22 / 10) |
| `DustGlyphSize`, `IntegrityDialDiameter`, `WedgeOuterRadius`, `WedgeInnerRadius` | 26 (38), 64 (112), 24.96 (43.68), 13.23 (23.15) |
| `CaptionGap`, `SealedNoteTop` | 12 (14), -6 (-16) |
| `BossBarMaxWidth`, `LeftClusterRight`, `DustPlateReach` | 640 (840), 508 (652), 220 (308) |
| `BossTop`, `BossHeaderHeight`, `BossTubeGap`, `BossTubeHeight` | 20 (22), 26 (38), 7 (10), 20 (34) |
| `BossDiamondInset`, `BossDiamondSize`, `BossNameGap`, `BossDiamondLift`, `BossTubeRun`, `BossNotchScale` | 4, 10, 12, 4 (6, 12, 14, 6), 10 (16), 0.75 |
| `BossNameSize`, `BossNameFloorPixels`, `BossValueRightPad`, `BossDroppedTubeGap` | 26 (38), 22.8 (kept), 12 (16), 6 (8) |
| `StoryHUD.BlockPipSize` (now public) | 18 (30) |
| `AnchorPip.PipSize`, `AnchorPip.IconSize`, `AnchorPip.SplitPixels` | 10 (18), 26 (46), 2.5 (4) |

`BossPreferredX` is gone: the bar is centred on the canvas (`(canvas - width) / 2`) and clamped into the free band. The free
band is now `(508 + 24, canvas - 220 - 24)`, so `FreeBand`, `BossBarWidthFor`, the checkpoint toast and the first-use tooltip
(which calls `FreeBand`) all follow the new left edge; the bar is 640 at 100 % and 595 canvas units (834 px on screen) at 140 %.

### Theme (`tools/ui_theme/build_ftt_theme.py`, regenerated `ftt_theme.tres` and `UIPalette.Roles.cs`)

- Story-only roles resized in place (each was grepped; none is used by the Fighter HUD or another screen; the capture
  specimens in `UiCaptureTargets.Theme.cs` show them): `LevelTitle` 26 → 20, `ObjectiveLine` 22 → 18, `HudNumeralSmall`
  22 → 20, `RewindCount` 32 → 20, `DustTotal` 38 → 28, `DustFloat` 26 → 21, `IntegrityPercent` 23 → 18, `BossName` 38 → 26,
  `BossHpValue` 24 → 20.
- New Story-only roles (the shared ones are used by the Fighter chips, the results panel and the select): `HudStatusName`
  (Outfit 500 18, 0.01 em, Ink, halo S), `HudStatusTime` (Outfit 600 18, Ink2, halo S), `HudStateLabel` (Outfit 600 18,
  0.10 em, Ink). `LINE_TRIM` trims `HudStatusName` / `HudStatusTime` (-2, -2) and `LevelTitle` (-2, -2) / `ObjectiveLine`
  (-1, -2) toward the mock's line boxes.
- Slab presets: new `GlassPanelHud` (12 / 14 / 12; the Pad adds 2 on the right); `GlassPanelVitals`, `GlassPanelAbilities` and
  `GlassPanelTimeTools` deleted (nothing used them after this; the capture specimens that did now use `GlassPanelHud` or
  `InkPanel`). `GlassPanelDust` 14 / 24 / 14 → 13 / 18 / 13 (the mock's 9 / 18 sits around a 42 px row; Outfit's 35 px line
  needs 13 to stand as tall, 61 px). The retired names were **not** added to `UiLegacyKitReferenceScanTests`: its lists are
  the glass closeout's pulp purge (plan 2.2's closeout column), not a general register.

### Widget API additions (every default renders exactly as before)

| Widget | Addition | Default |
|---|---|---|
| `PulpAbilitySlot` | `UltimateLensSize`, `RingWidthOverride`, `RingOutsetOverride`, `CenterTextSize`, `EffectScale`, `ReadyPill`; read-outs `RingThickness`, `RingOutset`, `UltimateBoxSize`; consts `ReadyPillPadX` 8, `ReadyPillHeight` 22, `ReadyPillRise` 19, `ReadyPillSlant` 6 | 92, 0 (3 / 5), 0 (6 / 9), 0 (the 0.36 x lens rule), 1, false |
| `PulpStatusChip` | `RingThickness`, `RingOutsetPixels`, `TextGapPixels`, `ColumnGapPixels`, `GlowSize`, `UppercaseName`, `NameRole`, `TimeRole`; seams `NameLabel`, `TimeLabel`, `DrawnRingRadius` | 3, 5, 12, 4, 14, true, `StatusName`, `StatusTime` |
| `PulpBarDecor` | `RunOverride`, `NotchScale` | 0 (12 / 16), 1 |
| `HudBadge` (Story only) | `RingThickness`, `RingOutsetPixels`, `IconShare`; `DrawnRingRadius`, `EffectScale` (the glows scale below 46 px) | 3, 5, 0.62 |
| `IntegrityDialLayer` | its outer glow scales with its size (22 at 112, 13 at 64) | unchanged at 112 |
| `SiphonStreamsLayer` | its glow line width scales with its box | 6 at 87.36 |
| new `HudCapsule` (`scripts/UI/HudCapsule.cs`) | a `MarginContainer` (7 px padding) drawing the recessed pill: rgba(2,5,14,.70) → rgba(10,16,36,.42), an inner shadow, a 1.5 px gold hairline at .5 | — |

The READY pill is drawn by a private `ReadyPillLayer` in `PulpAbilitySlot` (a 6 px-slanted plate, rgba(6,10,26,.94), a
1.5 px `res` edge and a soft glow); the label keeps the `UltimateReadyLabel` role (T60, 18 px bold, upper case). The Defy seal
is a `HudBadge` (28 px, glyph 20) whose `Glyph` is the HUD's `_defyIcon`, so `RefreshDefySeal` is unchanged.

Standing rules checked: no per-frame allocation added (the capsule, pill and badge geometry build once per size), no new
`GetChildren()` in a per-frame path, no `Dispose()` of a resource, no chrome cyan, nothing writes `SceneTree.Paused`, HUD
opacity and Reduced Temporal Effects paths unchanged, the D10 circle follows the lens's (smaller) ring.

## 2. Tests

Full run: section 4 (3770 / 3770). `[TestCase]` attributes under `tests/`: **3770** (3761 + 9).

### Added: `tests/Unit/StoryHudCompactTests.cs` (9 cases, one `[TestSuite]`, `[RequireGodotRuntime]`)

The suite finds nodes by name and uses only the HUD's long-standing seams, so its first eight cases build against the
three-slab HUD. Fail-first evidence: the suite (without the Fighter guard, which uses the new widget properties) was copied
into a scratch worktree at `50f38282`, built and run there (`test_old.log` in the scratch folder): **7 failed, 1 passed**.

| Case | On `50f38282` |
|---|---|
| `OneGlassSlabHoldsTheVitalsTheAbilitiesAndTheTimeTools` | fails: "3 glass slabs on the left, not one" |
| `TheSlabIsAbout484By182AtTheTopLeftAtUiScaleOne` (also: Act III's anchors do not widen it) | fails: no single slab |
| `TheUltimateLensStandsAloneAndIsLargerThanTheThreeAbilityLenses` | fails: '64' but is '92' |
| `TheSpecialsShareOneCapsuleAndMovementSitsInAnotherAndTheyDoNotOverlap` | fails: "Special 1 sits in a capsule" |
| `TheTimeToolsSitRightOfTheHairlineAtTheSlabsRightEnd` | fails: no hairline |
| `TheTitleAndObjectiveSitUnderTheSlabAndTheLeftClusterEndsBy260` | fails (no slab to measure against: a null reference) |
| `StatusNamesAreShownInSentenceCase` | fails: "the status name is drawn in capitals" |
| `NoStoryHudTextIsDrawnUnder18PixelsAtUiScaleOne` | **passes on the old HUD** (its floor was already 18). Shown failing on the new geometry with `AbilityCenterSize` set to 0 (the 0.36 x lens rule): "Cooldown1_Icon/PulpAbilitySlot/Center '5.9' 17 px" (`test_mut.log`) |
| `TheFighterHudKeepsItsLensesAndBadges` | a guard (unchanged by definition on the old HUD). Shown failing with `FighterHUD.AbilitySlotSize` 54 → 48: "'54' but is '48'" (`test_mut.log`); both mutations were reverted before the commit |

### Changed (each to the approved new value; no assertion deleted)

- `StoryHudPulpTests`: paths from the HUD's constants; the slab-preset loop now checks `GlassPanelHud` (and that the slab has no
  panel tag) and `GlassPanelDust`; lens 74 → 48, the Ultimate's host 92 → 64 and its box 110 → 78, chip badge 44 → 30; key
  labels `hud_slot_special1` → `""` (no S1 / S2 / MOV / ULT labels); the control chip's gap 30 → 16 after the widest damage name,
  plus sentence case; the Defy icon is now the badge's glyph (`DefySeal` moved out of `DefyStack`); "/max" alpha .78 → .72 (the
  mock's .72); the time tools: the freeze badge 46 → 26 with a 2 px ring 3 px out (was `HudBadge.RingWidth` 3 / 5 out), and the
  layout pins became the column (Time Freeze 8 px under the rewind row, left-aligned, both right of the hairline; the Beacon 8 px
  after the counter, was 24 / 48 px along one row); the pip text 18 → 10; the siphon stroke 1.92 → 2.2 % of its box (1.10 px at the
  64 px dial; the box is checked too); the dial wedge radii 43.68 / 23.15 → 24.96 / 13.23 (the 112 px constants still pinned) and
  the dial layers sized 64; the boss bar 840 → 640 (and the 140 % width from the constants, and centred on the screen), its tube
  height 20; the objective flag 22 → 18 and its margin 32 → 26.
- `StoryHudBoundsTests`: the band formula reads the constants (`min(640 s, 1920 − 776 s)`; it hard-coded 1008); the "closer than
  24 s to the stack" message now prints both rects.
- `StoryHudContractTests`: the contract paths moved to the one slab (same leaf names); the Time Freeze indicator's path.
- `StoryHudPresentationTests`: paths (the portrait, the HP tube, the Rally band, the block pips, the rewind count, the lens and
  the meter host for the D10 case).
- `StoryHudBeaconAnchorTests`: the Beacon still immediately follows the rewind counter; Time Freeze is still its own node, never
  a child of the counter, now the time tools column's next row after the counter's row (it was the counter's next sibling but
  one); `PipSize` 18 → 10.
- `UIThemeTests`: the presets table (`GlassPanelHud` 12 / 14 / 12 for the three deleted ones; `GlassPanelDust` 13 / 18 / 13); the
  halo S list gains `HudStatusName`, `HudStatusTime`.

## 3. Captures

`D:/Projects/ftt-scratch/hud-compact/captures/`: `huds_calm`, `huds_calm_140`, `huds_stress`, `huds_140`, `huds_boss`,
`huds_boss_140`, `huds_boss_names`, `huds_boss_names_140`, `huds_anchors`, `huds_anchors_zero`, `huds_frozen_dial`,
`huds_low_first`, `huds_toasts`, `huds_toasts_140`, the new `huds_states` (the mock's third frame: Act III 2 / 3, the Ultimate
ready with its pill and Defy armed, S1 cooling, Time Freeze cooling, the damage slot empty under Time Dilation), and
`hudf_versus` (pixel-identical to the final glass gallery's). `huds_calm__vs__v2_wells_calm__side.png` / `__overlay.png` are
`compare_capture.py`'s output; `cmp_left.png`, `cmp_states2.png` and `cmp_right.png` in the scratch folder set crops of the
capture over the mock's at 2×. I looked at every capture.

Capture-tool changes: `huds_boss_names` / `huds_boss_names_140` crop the compact bar (both now 2 x 8 grids; the 140 % page was
3 x 5); a new `huds_states` target; the theme and widget specimens that showed the deleted presets show `GlassPanelHud` /
`InkPanel` (`theme_*` / `widgets_*` captures will differ slightly from the final gallery; they were not re-captured).

### What still differs from the mockup, and why

- **The slab's chamfer is 18 px, the mock's 12.** The slab is the glass kit's baked nine-slice (`glass_panel.png`, chamfer 18);
  a 12 px chamfer needs new art (a second nine-slice), which the brief said to raise rather than make. Not made.
- **Anchor pips read smaller than the mock's.** The mock draws 10 px squares turned 45° (about 14 px across); the build's pip is
  a 10 px diamond (the brief's number). 12 px pips widen the Act III slab to 489 and break the free-band rule, so the pips stay
  10 and the time tools' gaps were tightened (rewind row 8, Beacon 4, pips 3; the mock's 14 / 7 / 5) to keep the slab at 484 in
  Act III. The mock's own Act III slab grows to about 489.
- **Ultimate lens's row is 2 px narrower** (`MeterRow` 83: the mock's 4 + 7 + 64 + 10 = 85, its right margin trimmed for the
  same Act III width).
- **The dial caption sits 12 px from the dial** (the brief); the mock's caption box ends about 50 px from the disc (its clock
  element is wider than the disc). The dust plate is about 19 px narrower than the mock's (Godot's tighter glyph advance).
- **The sealed note wraps "TIMELINE SEALED AT THE / CHECKPOINT"** at its 340 px box; the mock breaks after SEALED with a `<br>`.
- **The game's glass is darker and bluer** than the mock's renders (no backdrop blur), as expected; the capsules read fainter
  for the same reason.
- The READY pill's text is tracked 0.16 em (the T60 role) where the mock tracks 0.08, so the pill is a few pixels wider.

## 4. Test total

One full run at commit `0c27d14d` (the implementation; the later docs commit changes one code comment), lock held:
**`Passed! - Failed: 0, Passed: 3770, Skipped: 0, Total: 3770, Duration: 2 m 18 s`**, exit 0. Delta **+9** from the 3761
baseline, all in the new `StoryHudCompactTests`; the `[TestCase]` count under `tests/` is 3770. The filtered Story HUD, theme,
widget and Fighter HUD suites (147 cases) were green before it.

## 5. Provisional values and not delivered

Every size above is the mock's (provisional, like the glass HUD's, `PROVISIONAL-UI-GLASS-HUDS`), except the agent choices:
the badge ring 2 px / 3 px out and the chip glow 10; the capsule's inner shadow; the READY pill's 22 px height and glow; the
dust plate's 13 px padding; the line trims; the time tools' tightened gaps; `DustPlateReach` 220 (the capture's "1,250 +25" plate measures 188 px; 220 less the 24 px margin leaves room for about one
more digit, the same kind of allowance the old 264 / 308 was). The `HUD_CONTRACT.md` amendment is in place (dated). AGENTS.md, CLAUDE.md,
the ledger, the glass plan and the design master are the orchestrator's. Nobody has looked at it in play, on a pad or at 140 %
in a live level.

## 6. Fix step (Codex review 1)

**Finding (major, agreed by the orchestrator):** the title and objective moved under the slab but did not wrap, so a long
production title ran past the slab into the free band. At UI scale 1.4, `florence_level_title` with the wrapped
`tooltip_first_use_ledge` plate (y 140, growing down) put the tooltip over the title row near y 214. The bounds tests used
the shorter `campaign_level_orleans` string and missed it.

**Fix.** `SafeArea/TopLeft` is now exactly as wide as the slab less its 8 px indent: 476 at 100 %. `PlaceTitleRow` sets the
width from the slab's live rect whenever the slab resizes. `LevelTitle` and `Objective` word-wrap (`autowrap_mode = 3`), so a
long title or objective takes a second line instead of entering the band. The band holds the checkpoint toast, the first-use
tooltip and the boss bar. The objective's 18 px flag now sits on the first line, centred on that line's box, instead of
centring on the whole label. Sizes are unchanged (title 20 px, objective 18 px), so the 18 px floor holds. Files:
`scenes/ui/StoryHUD.tscn` (the two autowrap flags and the header comment) and `scripts/UI/StoryHUD.Pulp.cs` (`PlaceTitleRow`,
`BuildTitleRow`'s flag anchors).

**Longest real values.** Titles: `gettysburg_level_title` ("LEVEL 11: THE GETTYSBURG BATTLEFIELD — PENNSYLVANIA, JULY
1863") and `florence_level_title`, both two lines at 476 px. Level objectives: `gettysburg_objective_breach` (117 characters)
and a dozen others above 100 characters, all two lines. The left cluster then ends lower (about y 300 at 100 % for two
two-line values). That is below the slab and left of the band, so nothing overlaps. The mock's own Pompeii title is about
503 px and now wraps too: see `captures-fix/huds_calm.png`.

**Test (+1).** New case `StoryHudBoundsTests.EveryProductionTitleAndObjectiveStaysClearOfTheTooltipAndToastBand`. It reads
every `*_level_title` key (16) and every non-drill `*_objective*` key from `en.csv`, at 1.0 and then after a live change to
1.4 on the same HUD. The longest first-use tooltip and the checkpoint toast show the whole time. For each title and objective
label it checks: no right edge past the slab's right edge, nothing inside the free band, no overlap with the tooltip plate or
the toast, and the label stays inside the frame.
- Fail-first: with the fix stashed (the `2eed38d3` HUD), it fails with 544 problems. The first is "1 florence_level_title (32,
  214), (679, 24) runs past the slab's right edge 508", and the list includes overlaps "under the tooltip (532, 140), (900,
  79)" (`test_fix_old.log`).
- With the fix it passes.
- `StoryHudCompactTests.TheTitleAndObjectiveSitUnderTheSlabAndTheLeftClusterEndsBy260` still passes on its string. No test
  was changed.

**Filtered run** (StoryHud*, EncounterLock*, Level06/07/10 content suites, which bind the objective, and UIThemeTests):
`Passed! - Failed: 0, Passed: 203, Skipped: 0, Total: 203`. The `[TestCase]` count is 3771 (3770 + 1); the full suite was not
run. Captures: `D:/Projects/ftt-scratch/hud-compact/captures-fix/` (`huds_calm`, `huds_calm_140`, `huds_toasts`,
`huds_toasts_140`). I looked at all four: the title wraps under the slab at both scales, and at 1.4 the toast and the boss
intro card are clear of it.
