# TG-CLOSE C1 handoff: fold the kit gaps, purge the pulp layer, add the guards

Temporal Glass closeout, code half (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.13, C1). Look authority:
`UI_SPEC_GLASS.md` (section numbers below are its own). Branch `tg/close`, worktree `D:\Projects\ftt-wt\tg-close`, cut
from `main` at `141f56b2` (3732). Nothing is merged or pushed. C2 (the docs, the three full runs, the final gallery, Codex
review 3) is not part of this half.

Commits, in order:

| Commit | What |
|---|---|
| `b45b4d0c` | Codex review 2's finding: a heal stops the damage tracker's hold and drain |
| `4c455e1d` | The purge: retired variations, roles, palette, icons, the pulp art and tools |
| `45627a1b` | The three art gaps (18 px option arrow, 44 px slider grabber, quieter tab hover) and the theme's focus folds |
| `000aa56c` | The seven missing Label roles; the screens drop their stand-ins; the winner line is T40 |
| `c7bf14cd` | The 24 px tag plate, the empty slab, the compact footer variation and the 18 px compact floor |
| `e103c319` | `GrowToFit`'s badge-width cache; the badge reserve follows the device |
| `53d5ab44` | The toggle follows the UI scale and draws its own T52 row label |
| `546806e5` | `PulpMotion.Settle`; centred status badges; the tube not-yet frame; the suppressed Ultimate's glow at .2 |
| `ff5345d1` | The title gradient repeats on every line, so the credits title wraps |
| `7f7b4cc5` | One overlay prompt clearance, measured from the prompt bar's real top |
| `9e5d6ded` | The first-use tooltip keeps to the HUD's free band |
| `b60d7504` | `GlassScreenDressing`, the slab frame and both title flourishes folded into the kit |
| `f3a11632` | The chrome-cyan allow-list cannot go stale |
| `f5fbb36d` | The drill list hint counts five drills; the unused Random-card and dates rows deleted |
| `ea87481f`, `4c874880` | The tracking scale, folded, then reverted (see "What did not fold") |
| `0c2baf94` | A toggle skinned while hidden lays its row label on its own row (found in the captures) |
| `fa48a615`, `a8827654` | Capture targets for the closeout |

## What folded

Every gap the orchestrator listed for TG-CLOSE (plan section 9, the A1-A2, A3-A4 and Phase B entries), with where it
went. "Generator" is `tools/ui_theme/build_ftt_theme.py` (its outputs regenerated, never hand-edited); "kit" is
`scripts/UI/Pulp/` and `tools/asset_src/glass/`.

| Gap | Fold | Commit |
|---|---|---|
| The `OptionButton` arrow was Godot's default | `glass_option_arrow` (`icons/chevron_down_18.png`): `chevron_down`'s bold weight rasterized at its 18 px display size with its glow, baked ink2 / ink; the generator wires it with `modulate_arrow` 1 (ink2 at rest, white lit, ink3 disabled) | `45627a1b` |
| The slider grabber's 64 px canvas made a Settings slider row 64 tall | `glass_slider_grabber` on a 44 x 44 canvas, its glow clipped and fading over the last 3 px | `45627a1b` |
| A hovered tab read as selected | `glass_tab_hover`: the quiet lit tab (fill rgba(glow,.16), glow .30); hover text white (`GlassTab`, `TabContainer`) | `45627a1b` |
| Sliders and labels drew Godot's focus frame | The generator gives `HSlider`, `VSlider` and `Label` the glass field's focus overlay; `SettingsMenu.GiveSliderTheFieldFocus` is gone | `45627a1b` |
| The five bar variations kept their pulp tracks under the tube | Value carriers: an empty background and a fill that holds its colour and draws nothing; the tube's hide-self material and `ui_hide_self.gdshader` deleted | `4c455e1d` |
| The tag plate measured 29, not 6.4's 24 | `LINE_TRIM` in the generator: the `PanelTag` role's `FontVariation` trims its line box (`spacing_top` -2, `spacing_bottom` -3) inside the plate's 3 + 3 padding | `c7bf14cd` |
| Seven roles stood in with the nearest role plus an override | `DescriptionLabel`, `BodyCompactLabel`, `BodySmallDimLabel`, `SlotCaption`, `LogSpeaker`, `CreditsHeading`, `HudNumeralMaxScene` (the Fighter "/max", Outfit 400 21, halo S); every stand-in and colour override removed from the screens | `000aa56c` |
| The match winner line was `HeadingLabel` with a copied scene halo | T40, the existing `SideCardName` role | `000aa56c` |
| An empty save slot built its own `StyleBoxFlat` | `GlassPanelEmpty` (the disabled chevron fill, the 18 px TL / BR chamfer, the Continue card's padding); `PaintSlotFill` switches the variation | `c7bf14cd` |
| A badge-less compact footer padded 40 on the right | `PulpButtonCompactFooter` (52 / 13 / 30 / 13); the select footer wears it | `c7bf14cd` |
| The compact chevron's shrink floor was 20 x the UI scale (28 at 1.4) | 4.2's 18 px at every scale (`PulpButtonDecor.ShrinkFloorFor(compact: true)`); the T10 floor still scales | `c7bf14cd` |
| `GrowToFit` cached its word fit without the badge it reserves; `ConfirmModal` worked round it | The badge width is in the cache key; the workaround is removed | `e103c319` |
| The badge reserve was a fixed 52 (the superseding ruling) | `PulpButtonDecor.BadgeReserveFor`: the pad ring's 36 + 16, or the keyboard confirm key-cap's own width (at least 34) + 16; a bound D6 stack runs again on a device change | `e103c319` |
| The toggle drew a fixed 132 x 44 at every scale; its text was the button's own mixed case | The track, knob, insets, run and glows follow the UI scale; the decor draws the button's text as the upper-case T52 row label (`RowLabel`) and hides the button's glyphs; `SettingsRowLayout.ToggleRowWidth` scales | `53d5ab44`, `0c2baf94` |
| No public stop-and-settle for panel motion | `PulpMotion.Settle(panel)`; `PausePanel.StopAppear` settles the panel on its slot | `546806e5` |
| A status chip drew its badge at its top, so the Fighter row shrink-centred it | `PulpStatusChip.BadgeTop` centres the badge, glyph and text column in a taller rect, relayout on resize; the Fighter chips drop `ShrinkCenter` | `546806e5` |
| The Fighter Ultimate tube's refusal drew the rectangle round it | `NotYetShape.Tube`: a 2 px danger frame leaning with the tube (its run, mirrored for P2), cached per size, run and lean | `546806e5` |
| The suppressed Ultimate's cold glow read twice as heavy as rec_05 | `PulpAbilitySlot.OuterGlowOf`: .2 (a CSS blur starts at half its colour, a Godot shadow at its full colour) | `546806e5` |
| The title gradient spanned one line, so the credits title could not wrap | `ui_title_gradient.gdshader` takes `line_pitch` and `line_count` (a pixel takes the nearest line box's gradient); the credits title wraps in its 1100 px column | `ff5345d1` |
| Two prompt reserves: TG-SCREENS' scaled 56 (78.4 at 1.4), TG-PAUSE's unscaled 56 | `MenuSafeAreaFit.AttachClearOfPromptBar`: the panel keeps 10 px clear of the prompt bar's real top, measured whenever the bar is laid out; every overlay, the drill result card and the Resonance grid (whose own 56 fell 8 px short of its corner bar) | `7f7b4cc5` |
| Check the first-use tooltip at y 140 against the HUD clusters at 1.4 | It covered the Vitals panel: 8 of the 9 lines at 1.0 and all 9 at 1.4. The plate keeps to the HUD's free band like the checkpoint toast (`StoryHUD.FreeBand`, now the one statement the boss bar, the toast and the tooltip share), wrapping at the band's width where the band is narrower (about 508 px at 1.4) | `9e5d6ded` |
| Fold `GlassScreenDressing`, `MainMenu.SlabFrame` / `HeadingFlourishes` and `FlankTitle` | `scripts/UI/Pulp/PulpScreenKit.Screens.cs` (the screen-type builders as a part of `PulpScreenKit`); `TitleFlourishes` (one widget, `TitleFlourishes.Attach`, merging the two copies; they hid their rules under 24 and 32 px of room, the kit keeps 24); `ResultRow` with its pure `SplitRow`; `SlabFrame`. `GlassScreenDressing.cs` is deleted | `b60d7504` |
| The stale `drill_list_hint` | "Five short lessons ..." | `f5fbb36d` |
| `fighter_random_mystery`, nine `character_<id>_dates` | Deleted from `en.csv` (no user anywhere under scripts, scenes, resources, tests or tools) | `f5fbb36d` |

## What did not fold, and why

- **Unscaled tracking** (TG-WIDGETS' gap). Folded in `ea87481f`: `UIPalette.ApplyUiScale` scaled each theme font's
  authored spacing with its size (keyed like the sizes; the HUDs' unscaled copy kept the authored values), with a test
  seen failing first ("PauseTitle tracks 11 px at 1.4, not 15"). The 1.4 captures then showed the cover's
  `QUICK PLAY (EINSTEIN VS CPU)` ellipsized (`menu_cover_scale14`, whole before): every upper-case label widens by about a
  pixel a glyph, and that column is capped at 870 px at 1.4 by the Continue card (`MainMenu.ColumnCapAt`). Reverted in
  `4c874880` (the test with it). It needs a ruling on that geometry: accept the trimmed row (which
  `VERIFY-UI-QUICK-PLAY-ROW` may remove anyway), or let the 1.4 column reach past the card's clearance.
- **Integer `spacing_glyph`.** An engine limit (Godot 4.7's `FontVariation.spacing_glyph` is an int); the generator rounds
  each role's CSS tracking to whole pixels (2.5), as before.
- **Display-role line heights** beyond `PanelTag`. The Cinzel display roles' line boxes are about 1.37 em where the CSS
  says 0.94 to 1.0; trimming them in the generator moves every display label and the hand layouts that already correct
  for it (`PausePanel.TrimTitleBox`, `MainMenu`'s title lock-up, the boss header's CSS line box), so it is a pass of its
  own. Only the tag plate, which a fixed 24 px needed, is trimmed.
- **A glass Rally reclaim flash.** `EventBus.OnRallyEchoChanged` has no publisher (AGENTS.md); a flash needs one in
  `PlayerController` (gameplay, outside C1), or a poll in the HUD, and MOT-A row 8 gives the echo band no motion, so the
  look itself is a ruling. The HUD's handler keeps its comment.
- **"SYSTEMS CARD" in the select footer at 1.4.** The 18 px floor fixed MOVE LIST and BACK, but SYSTEMS CARD needs about
  178 px at 18 px and the 280 px button keeps 165 after its scaled paddings, so it still ends in an ellipsis (plan 0.4 P1
  fixed the 280).

## The purge (`4c455e1d`)

The reference scan before deleting found the retired names only in the capture targets, the generator, `UIThemeTests` and
one `MainMenuCoverTests` guard list. Deleted:

- Theme (generator and outputs): 13 variations (`HudPlatePanel`, `PaperPanel`, `BannerPanel`, `CoverBannerPanel`,
  `BossNameplate`, `BossPhaseTab`, `SlotPlate`, `SlotPlateReady`, `KeyTagPlate`, `CardPlate`, `CardPlatePick`,
  `BubblePlate`, `StampPlate`), 3 roles (`CardDates`, `BubbleText`, `StarBadgeText`), the `LabelDark` palette entry.
- `UIPalette`: 12 constants (`PlateFill`, `PlateRaised`, `Paper`, `LabelDark`, `PaperInk`, `PaperInk2`, `PaperInk3`,
  `Track`, `SlotFrame`, `PromptCream`, `DisabledFill`, `DisabledLabel`); `TrackBackground` is the glass tube track (no
  production reader: `RadialProgress` writes its own #3A3144 default).
- `PulpKit` reads the glass catalog only; `PulpIcon` draws glass icons only (no aliases, no pulp layers); `GlassMarker` is a
  `Control` with read-only `Tint` / `GlowEnabled`, and `PulpButtonDecor.Marker` is typed `GlassMarker`.
- Motion: `PulpMotion.HitFlash`, `HitFlashLimiter`, `FrameSeconds`, `HitFlashMinIntervalSeconds`, `CardLift`,
  `CardTiltDegrees`.
- Other dead members: `PulpBarDecor.ShaderPath` / `InnerRect` / `Skew`, `PulpButtonDecor.SelectedPlateHeight`,
  `PulpPromptGlyphs.KeyCapPath`, `PulpTaggedPanel.PaddingBoxInset`, `PulpScreenKit.ReserveBadgeSpace`,
  `AnchorPip.SpentAlpha`.
- Files: `assets/sprites/ui/pulp/` (the ability icons moved to `assets/sprites/ui/ability_icons/` with their 36 `.import`
  sidecars, each keeping its uid; `AbilityIconLibrary.Folder` and the builder, now `tools/asset_src/glass/build_ability_icons.py`,
  follow), `tools/asset_src/pulp/`, the `pulp` and `pulp_icons_raw` jobs, `assets/shaders/ui_hide_self.gdshader`.

Evidence: `UiLegacyKitReferenceScanTests` is green over scripts, scenes, resources, tests and tools (cs, tscn, tres, py,
gd, json, ps1, md, gdshader); `--headless --import` then `--headless --quit` at the head report no error and no missing
dependency (`D:/Projects/ftt-scratch/tg-close/import-final.log`). The main checkout still carries `.import` line-ending
churn on the pulp sidecars: run `git checkout -- assets` there before `main` advances past the purge (plan 2.13 item 2).

## Guards

- `tests/ContentValidation/UiLegacyKitReferenceScanTests.cs` (+2, pure C#): nothing names `ui/pulp/`, `pulp_kit`,
  `asset_src/pulp`, a retired variation, role, palette constant or icon name (icon names only on icon-API lines); the
  detector sees a planted reference of each kind and leaves the glass names alone. It is the one place the retired lists
  live; `UIThemeTests`, `PulpIconTests` and `MainMenuCoverTests` read them from it.
- `UiChromeCyanSourceScanTests.EveryAllowListedFileStillNeedsItsEntry` (+1): every allow-listed file exists and still
  names cyan; seen failing with `ResonanceEdgeLayer.cs` planted back ("names no cyan").
- `UIThemeTests` in place: the retired names absent from the theme's type lists, the folded roles and variations present.

## Codex review 2 (`REVIEW-UI-GLASS-CODEX-2`)

The finding: a heal assigned the tube's `LagValue` while the last hit's hold-then-drain tween kept running and later wrote
its start back, so a damage band reappeared above the healed fill. `PulpBarDecor.SnapLagTo` kills the tween and sets the
tracker; both HUDs use it on every heal, first presentation and new maximum (the Fighter HP tubes, the Story hero tube,
the boss tube on its reveal and a historical-recovery heal). Two tests, each seen failing first: a heal during the hold
and during the drain, then past the old tween's end, leaves no band above the fill.

## Tests

`[TestCase]` count: 3732 at `141f56b2`, **3740** at the head. The plan's declared C1 delta is -2 (-3, -2, +2, +1); the
gap and review fixes add +10:

| Suite | Count | Why |
|---|---|---|
| `PulpKitContentTests` | 3 -> 0 (deleted) | the pulp kit is gone |
| `PulpMotionRulesTests`, `PulpMotionTests` | -1 each | the hit flash is gone (P7) |
| `UiLegacyKitReferenceScanTests` | +2 (new) | the purge guard |
| `UiChromeCyanSourceScanTests` | 2 -> 3 | the allow-list guard |
| `FighterHudTopBandTests`, `StoryHudPulpTests` | +1 each | Codex review 2 |
| `PulpButtonDecorTests` | 18 -> 20 | the badge-width cache; the device-aware reserve |
| `PulpToggleDecorTests` | 2 -> 4 | the toggle's scale and row label; a toggle skinned while hidden |
| `PausePanelTests` | 1 -> 2 | the stopped appear settles on the slot |
| `PulpStatusChipTests` | 2 -> 3 | the badge centred in a taller rect |
| `GlassDecorTests` | 5 -> 6 | the wrapped title's gradient; the credits title wraps |
| `StoryHudBoundsTests` | 1 -> 2 | the tooltip keeps to the free band |

Arithmetic: 3732 - 3 - 2 + 2 + 1 + 2 + 2 + 2 + 1 + 1 + 1 + 1 = 3740.

Changed in place (no count change), each seen failing first where it pins a new behaviour:
`UIThemeTests` (`EveryKitVariationExistsOnItsBaseTypeAndNoRetiredNameRemains` reads the theme's type lists; the folded
roles in `EveryLabelRolePinsItsFaceAndWeight`; the arrow, `modulate_arrow`, the 44 px grabber and the hover in
`ThemeCarriesTheStyleEntriesScreensDependOn`; slider and label focus in `EveryKeyboardNavigableTypeDrawsAFocusStyle`; the
24 px tag, `GlassPanelEmpty` and `PulpButtonCompactFooter` in `TheGlassPanelPresetsCarryTheirSpecPaddings`;
`HudNumeralMaxScene` in the halo S list), `GlassKitContentTests` (case 1 renamed
`EveryCatalogFileLoadsAtItsStatedSizeWithMipmapsAndTheRetiredPulpKitIsGone`), `PulpIconTests` (two cases rewritten for the
glass-only names), `PulpBarDecorTests` (value carriers, no material), `PulpButtonDecorTests` (`GlowEnabled`, the 18 px
compact floor; `AStackSharesItsWidestRequiredWidthUpToItsCap` pins the pad, D6 as drawn), `MainMenuCoverTests` (the
guard lists; each slot's slab by its save: it reads the machine's real save slots, display only, nothing pressed or
written; `SlabFrame` / `TitleFlourishes`), `CharacterSelectCardTests` (the footer variation, nothing under 18 px, trimmed
only at the floor, MOVE LIST and BACK whole at 1.4), `FighterMatchPresentationTests` (`SideCardName`, no copied halo),
`OverlaySkinTests` (`LogSpeaker`, `BodyCompactLabel`), `FighterHudSceneTests` (the tube frame, mirrored for P2),
`PulpAbilitySlotTests` (the .2 cold glow), `FighterHudTopBandTests` (it measures the drawn badge: the chip now fills the
row), `MenuLayoutBoundsTests` (measured 20 frames after a surface opens, past the 0.22 s appear; no panel within 10 px of
a prompt bar; the twelve overlays clearing a bar reserve exactly the measured clearance: seen failing first with 24
reserves of 56 or 78.4 where 54, 55, 64 or 65 were measured), `UnusedTranslationKeyTests` (the roster and ceiling 13 ->
12; seen failing with the row deleted and the roster unchanged), `CalibrationDrillsRouteTests`
(`TheDrillListBuildsTheCatalogsSixDrillsAndIsFullyFocusAuthored` renamed `...CatalogsDrills...`; it reads the catalog's
count).

Fail evidence for the new cases is in each commit message (for example "the glyph is centred at 20, not 27", "line_count ,
the label has 3 lines", "23 overlaps", "the row label is 400 tall on a 52 px row"); the logs are under
`D:/Projects/ftt-scratch/tg-close/` (`*-before.log`).

Filtered runs only (the lock held for each): the widened UI filter (`D:/Projects/ftt-scratch/tg-close/uifilter2.txt`,
the plan's UI filter plus the guard, credits, boot, play options and flow routes) **779 / 779**, and with
`CampaignLocalization` added **784 / 784** at the head `a8827654` (`head-ui.log`). Build: 0 errors, the two known warnings
(`KLSG_ECS004`, `CS8632`).

## Captures

All 145 targets at the head, windowed under the lock, into `D:\Projects\ftt-scratch\tg-captures\close\` (1.0 and every
1.4 twin). `compare.txt` scores each against the latest Phase A / B capture of the same target, `compare\` holds
before / after pairs for the 106 that changed, `sheets\` holds 3 x 3 contact sheets; every PNG was looked at.

Regressions found and fixed:

- `select_stage`, `select_stage_140`: the rules' toggles drew their row labels some 170 px under the track, below the
  BACK / FIGHT! row (the stage phase skins them while hidden; `0c2baf94`).
- `menu_cover_scale14`: the Quick Play row ellipsized under the tracking fold (reverted, above).
- `theme_controls`, `theme_glass_components`: the five bar variations drew nothing (the purge emptied them); the targets
  attach the tube decor now.
- `first_use_tooltip_ui14`: the 1.4 pause-family captures set the theme scale but not the saved setting the HUD reads, so
  the tooltip used the 1.0 band; `PauseAtUi14` sets both, as the game does.
- `widgets_notyet` shows the new tube frame on a P1 and a mirrored P2 tube.

Changed as intended: the T40 winner line, the 24 px tags, the 18 px compact floor (the boot and crash cards' compact rows
read larger and grow to fit), the toggles' upper-case row labels, the centred Fighter chips, the quieter suppressed glow,
the credits title on two lines at 1.0 and three at 1.4, the tooltip in the free band, the panels at 1.4 clearing the
measured 55 instead of 78.4 (Game Over, results, restart, the Holodeck console).

Seen, not C1's (for the orchestrator): the drill and Fighter countdown numeral draws in `UIPalette.P1` with a font-size
override (`FighterOverlayModel`, Package 8 B2), so it reads as player one's cyan; the boss intro card (`huds_toasts`,
centred on the scene per the TG-HUDS ruling) runs over the left stack's lower panels at 1.0 and 1.4; the gold block in
`boot_first_run` is the comfort toggle's preview swatch.

## Outside `scripts/UI/`

`scripts/Combat/CalibrationDrillRunner.cs` (its result card's fit only, presentation), `scripts/Diagnostics/UiCapture/`
(capture targets), `tools/ui_theme/build_ftt_theme.py`, `tools/asset_src/glass/`, `tools/asset_src/jobs.json`,
`assets/shaders/ui_title_gradient.gdshader`, `assets/sprites/ui/`, `localization/en.csv`. Nothing in `scripts/Core/`,
`scripts/FighterSim/`, saves or gameplay. `localization/en.en.translation` was regenerated locally with `--import` and is
not committed.

## For the orchestrator

1. The tracking scale (above): accept the trimmed Quick Play row at 1.4, or widen the 1.4 cover column; then re-apply
   `ea87481f`.
2. "SYSTEMS CARD" in the 280 px select footer at 1.4 (above).
3. Display-role line heights and the Rally reclaim flash stay open (above).
4. Agent choices to record: `TitleFlourishes`' 24 px minimum (the menu's copy used 32); the tag trim's asymmetric -2 /
   -3; the measured prompt gap of 10 px; the tooltip wrapping at the band's 508 px at 1.4; `TrackBackground` re-pointed
   to the glass track (no production reader); `PulpButtonDecor.Marker` typed `GlassMarker`; `MainMenuCoverTests` reading
   the real save slots (read-only).

## Fix step (after the C1 merge)

The orchestrator's rulings on C1 (`D:\Projects\ftt-scratch\tg-routed-findings.md`, the TG-CLOSE C1 section): rulings 1, 3
and 4 needed no code (the tracking stays unscaled, `PROVISIONAL-UI-GLASS-TRACKING-UNSCALED`; line heights and the reclaim
flash stay open; the agent choices are recorded as `PROVISIONAL-UI-GLASS-CLOSE`). Rulings 2 and 5 are this step, on
`tg/close` fast-forwarded to `main` at `feb3f619` (3740). Presentation only.

| Commit | What |
|---|---|
| `9fcfc0e1` | The Fighter countdown digit draws in the chrome accent |
| `94811c47` | Every select footer label reads whole at 1.0 and 1.4 |
| `54c79e9f` | Capture targets `hudf_countdown`, `hudf_countdown_140` |

**The select footer (ruling 2).** `CharacterSelectScreen.FooterWidths` (pure) sizes the row in the layout pass
(`FitFooter`). The buttons keep the shared 280 px while every label reads whole in one at the 18 px compact floor or above,
which is UI scale 1.0, as the mock. Otherwise they share the same 3 x 280 by need: every label at the largest single size,
from the role's down to the floor, at which all three fit together, each button its paddings, its label at that size and
2 px of slack (`FooterSlack`). The row stays inside the 908 px between the side panels, the focus-row separation is
unchanged, and nothing draws under the floor. The footer no longer binds the D6 stack rule (min = cap = 280), which would
write 280 back on a theme change. `PulpButtonDecor.LabelPaddingWidth` and `DrawnLabelWidth` are the two measures the rule
reads (the same ones the decor's own fit and `RequiredWidth` use). Measured:

| Scale | MOVE LIST | SYSTEMS CARD | BACK | Row |
|---|---|---|---|---|
| 1.0 | 280 px, label 24 px | 280 px, 20 px | 280 px, 24 px | x 506 to 1414 (unchanged) |
| 1.4 | 282 px, 25 px | 350 px, 25 px (was 280, trimmed at 18 px) | 198 px, 25 px | x 511 to 1409 |

The side panels end at x 500 and start at 1420 at both scales.

**The countdown digit (ruling 5).** `FighterOverlayModel.TryResolveBanner` drew the pre-match 3 / 2 / 1 in
`UIPalette.P1`, which followed the player-slot palette (cyan, blue or white). UI_SPEC_GLASS gives this numeral no colour
of its own (T59, the select's countdown, is accent), so it draws in the chrome accent, `UIPalette.Accent`. The 190 px size
stays (the overlay is a HUD on `UiScaleBinder`); GO, the stamps and the winner banner are unchanged.

**Tests:** 3740 to **3742**.

- `CharacterSelectCardTests.EveryFooterLabelReadsWholeBetweenTheSidePanelsAtBothUiScales` (+1). At 1.0 and 1.4: no label
  trimmed, none under 18 px, every button between `P1Panel` and `P2Panel`, and neighbours at least the focus-row gap
  apart. Seen failing first: "1.4: SystemsCardButton's label is trimmed at 18 px".
- `FighterOverlayModelTests.TheCountdownDigitIsTheChromeAccentInEveryPlayerSlotPalette` (+1). Seen failing first: the
  digit drew (0, 0.941, 1), (0.227, 0.553, 1) and white in the three palettes.
- In place: `CharacterSelectCardTests.TileTextShrinksToItsBoxAtLargeUiScaleButNeverBelowItsSizeAtScaleOne` drops its
  SYSTEMS CARD exemption, and `FighterMatchPresentationTests.TheCountdownRendersNumeralsThenGo` pins the accent where it
  pinned `UIPalette.P1`.

**Runs:** the widened UI filter plus `FighterOverlayModel`, `FighterLocalization` and `FighterMatchFlowContent`
(`fixstep-ui.log`) is **798 / 798**. It holds every suite this step touches (CharacterSelect, FighterOverlayModel,
FighterMatchPresentation, FighterHud, PadMenuFighterSelect); a run of those alone before the in-place pin read 96 / 97,
the one failure the old `UIPalette.P1` pin. Build: 0 errors, the two known warnings.

**Captures** (windowed, under the lock) are in `D:\Projects\ftt-scratch\tg-captures\close-fix\`:

- The footer states: `select_cards`, `select_p1_ready`, `select_ready`, `select_mirror`, `select_join`,
  `select_versus_cpu`, the three `select_palette_*`, `select_140` and `select_ready_140`.
- `select_stage` and `select_stage_140`.
- The countdown beat: `hudf_countdown`, `hudf_countdown_140` and `drill_hud`, which opens on the countdown. The `hudf_ko`
  stamp is unchanged.

Every one was looked at. At 1.4 all three footer labels read whole at one size, BACK the narrowest. Against the C1 head,
every 1.0 select state is pixel-identical (`select_mirror` differs only in P2's animated hero sprite, outside the footer),
and the 1.4 states differ only in the footer band (x 506 to 1413). The digit is gold at both scales and keeps its size.

## Codex review 3 fixes

- **First-use tooltip refit** (`scripts/UI/FirstUseTooltipPresenter.cs`): `FitPlate` caches the UI scale factor, HUD scale setting and viewport width it read; `_Process` refits (settling the appear tween first through `PulpMotion.Settle`) when one changes or a translation change arrives, only while the plate is visible. No allocation per frame.
- **Select footer bound** (`CharacterSelectScreen.Cards.cs`, `FooterWidths`): when the labels cannot fit even at the floor, the widths are scaled down (floored) so their sum stays within `shared * count`. Cases that fit return exactly what they did.
- **Tests**: `StoryHudBoundsTests.AShowingFirstUseTooltipRefitsWhenTheUiScaleChanges`, `CharacterSelectCardTests.TheFooterWidthsStayInsideTheBudgetWhenTheLabelsCannotFit`. Both failed against the unfixed scripts (stashed) and pass with the fix. Filtered UI suites: Failed 0, Passed 80, Total 80.

### Follow-up: the footer fallback keeps each button's minimum

The first bound scaled padding with the label space, so an asymmetric oversized row (`[600, 600, 40]` px labels) could drop a button under its intrinsic minimum (paddings + slack) and the container then grew it past the budget. `FooterWidths` now gives each button its minimum first and shares the rest of `shared * count` across the label spaces in proportion to need; if the minimums alone exceed the budget they stand. Cases that fit are unchanged.

Tests (both failed on the first bound, pass now): `TheFooterWidthsKeepEveryButtonItsMinimumWhenOneLabelIsOversized` ("button 2 is 69, under its minimum") and the real-scene `OversizedAsymmetricFooterLabelsStayBetweenTheSidePanelsAtBothUiScales` (at 1.0: "MoveListButton (495, 880), (394, 59) leaves the row (500 to 1420) | BackButton (1343, 880), (82, 59) leaves the row"). The scene case sets long asymmetric labels and emits `PlayersRow.resized` to run the layout (a text change alone queues none). Filtered CharacterSelectCard + CharacterSelectScene + MenuLayoutBounds: Failed 0, Passed 42, Total 42.
