# TG-WIDGETS handoff: the glass widgets

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.5). Look authority: `UI_SPEC_GLASS.md` (section
numbers below are its own). Branch `tg/widgets`, worktree `D:\Projects\ftt-wt\tg-widgets`, cut from `main` at
`993f4233` (TG-ART and TG-THEME merged, 3698 green).

## Step 1 (2026-10-27)

Step 1 is everything but the gauges (step 2), plus the two findings of the owed UR-POLISH2 review
(`REVIEW-UI-PULP-CODEX-3`) and the orchestrator's hover ruling. Code commit `aff5f2d7`; this handoff in the next commit.

### What shipped

| File | Change |
|---|---|
| `scripts/UI/Pulp/GlassGeometry.cs` (new) | Pure geometry for 0.2-0.3 and 3.1-3.4: `Chamfer` / `ChamferFill` (inset 1.5, cut `n - 1.5 / 1.5`, a 1.41 px hairline on the 45 degree cut), `Chevron` / `ChevronFill` (a fixed 30 px run to a mid-height tip at any height), `Parallelogram` (run R, "/" or mirrored "\"), `ClipHalfPlane`, `ClipVerticalSlab`, `ClippedFill` (a tube fill whose moving end is vertical; mirrored grows from the right), `Circle`, `BoxUvs`, the CSS angle gradient (`GradientDirection`, `GradientLength` = W abs(sin) + H abs(cos), `GradientT`, `GradientOffset`), `Stop2` / `Stop3` / `GradientColors`, `BlurAlpha`. Every builder has an allocation-free `into` overload. |
| `scripts/UI/Pulp/GlassSheen.cs` (new) | PNL-A layers 2 and 3 (3.1): the 112 degree sheen (white .05 up to t .34) and the two hard streaks (t .570-.575 at .075, .630-.634 at .04), clipped to the fill's chamfer, scaled with the panel's own rect; the MOT-A row 4 light band (`IPanelLightBand.PlayLightBand`, 40 px at .10, never under Reduced). Polygons rebuilt only on a size change. |
| `scripts/UI/Pulp/ScreenStreak.cs` (new) | The full-screen 118 degree streak (1.4: t .69-.6925 at .04, .735-.737 at .022), z 10, mouse-transparent. |
| `scripts/UI/Pulp/GlassFlourish.cs` (new) | The rule-diamond-rule flourish (3.6) in four shapes (`RuleDiamondRule`, `RuleDiamond`, `DiamondRule`, `Rule`), the `glass_diamond_11/12` art, gaps 16 (menu) / 24 (heading), heading rules 240. |
| `scripts/UI/Pulp/TitleGradient.cs` (new), `assets/shaders/ui_title_gradient.gdshader` (new) | 6.5: one `ShaderMaterial` per title label (no instance uniforms), the label drawn white and multiplied (`COLOR.rgb *= g.rgb`, the modulate kept), `line_top` / `line_height` (0.94 of the size) from the font metrics, the label's vertical alignment and the UI scale, re-applied once, deferred, after a theme change or a resize. |
| `scripts/UI/Pulp/GlassTileDecor.cs` (new) | The select tile and chip (3.4.3): chamfer 16 / 12, the 160 degree fill, edges Dim / Standard / Lit / Focus (Lit adds the .20 wash), owner edges (135 degrees, P1 / P2 / both through `UIPalette.OwnerOnDark`), glows as bevelled `StyleBoxFlat` shadows (14), the art at 150 % offset (-25 %, -4 %) inside `PortraitInsets` (8, 8, 8, 42), owner badges and the lock mark in a top overlay; focus follows the host (hover takes it when the host is focusable). |
| `scripts/UI/Pulp/GlassMarker.cs` (new, a `PulpIcon`) | The BTN-A diamond (3.2): a 14 px square at 45 degrees (19.8 diagonal); Normal an `Edge` outline, Focus filled `Focus` with a white outline, a 4 px `Accent` .30 ring and a 16 px glow, Selected filled and outlined `Accent`, Disabled dashed at half opacity. |
| `scripts/UI/Pulp/GlassPromptRing.cs` (new) | The 2 px prompt ring (3.4.4). |
| `scripts/UI/Pulp/PulpButtonDecor.cs` | BTN-A: the `GlassMarker` at x 31 (compact 29); the badge (a 36 px ring in the label colour with the confirm letter, or the key-cap) at `W - right padding - 36` on the focused row only; the theme's `selected` chevron (`glass_btn_selected`) on a toggled row; per-state label colours, drawn upper-case from cached `TextLine` / `TextParagraph`; one line shrinks toward its floor (26 px, compact 20, at the UI scale) then ellipsizes; `GrowToFit` kept; heights, paddings, marker and badge scaled by the UI scale (6.7 item 8); hover takes focus; the per-frame `_Process` poll replaced by the button's `Draw` signal; the D6 helpers `RequiredWidth` and `ApplyStackWidth` (below). |
| `scripts/UI/Pulp/PulpIcon.cs` | ICN-A: the glass texture first (`PulpKit.GlassIconTexture`), the bold weight below 30 px, aliases (`anchor` -> `beacon_anchor`, `anchor_hollow` -> `beacon_anchor_hollow`), the frozen pulp layers as the fallback for the four names glass lacks (`anchor_pip`, `anchor_pip_cracked`, `marker`, `prompt_circle`). |
| `scripts/UI/Pulp/PulpPromptGlyphs.cs`, `PulpPromptBar.cs` | The pad glyph is a `GlassPromptRing` plus its letter in ink with the halo S; the key-cap is a `StyleBoxFlat` (2 px ink border, radius 6, padding 9, at least 34 wide, no fill). |
| `scripts/UI/Pulp/PulpTagPlate.cs`, `PulpTaggedPanel.cs`, `PanelTagAnchor.cs` | The glass tag at the panel corner + (26, -11) (3.1: chamfer 18 + 8), no tilt, the 24 px clamp kept; a theme change places the tag once, deferred (Codex finding 2). `PulpTagPlate.NaturalWidth` is public. |
| `scripts/UI/Pulp/PulpToggleDecor.cs` | G13 (3.3): the parallelogram track (run 14), the knob (34, run 10, white, a 14 px `Accent` .80 glow) in its own layer above the text, "ON" white at left 16, "OFF" ink2 at right 16 (T54). |
| `scripts/UI/Pulp/PulpPortraitRoundel.cs` | The G5 frame (3.4.4): chamfer 22 %, a 2 px 135 degree edge (EdgeHi, `Accent` .25 at .45, Edge), the `glass_portrait_radial` fill, the art at 142 % offset (-21 %, -6.8 %) of the inner box, a 12 px shadow; no child nodes (`ui_circle_mask` deleted). Class name kept. |
| `scripts/UI/Pulp/PulpScreenKit.cs`, `PulpScreenKit.Pause.cs` | `DressPanel` adds the sheen; the 14 px stack inset on the panel's own padding (`InsetRight`); the prompt bar 70 from the right, 44 from the bottom; `MakeScrim` .70; `StylePauseButton` 560 x 64 (label at 58); the derived `FocusRowGap` and `SeparateFocusRow` (Codex finding 1); new `AddScreenStreak`, `AddMenuScrim`, `AddFlourishHeading`, `ButtonsOf`, `ReserveBadgeSpace`. |
| `scripts/UI/Motion/FocusPose.cs` | Additive: `FocusPose.Transition` / `Ease` (Quad, Out) name the curve its tweens use, so the row gap reads the real one. |
| Deleted | `scripts/UI/Pulp/HalftoneCorner.cs`, `assets/shaders/halftone_corner.gdshader`, `assets/shaders/ui_circle_mask.gdshader` (each with its `.uid`), `tests/Pulp/HalftoneCornerTests.cs`. Nothing outside `scripts/UI/Pulp/` used them in code; four Phase B screens still say "halftone" in a comment (`CalibrationDrillRunner`, `FighterPlayOptionsScreen`, `GameOverScreen`, `TimelineRestartPanel`). |
| Cross-edit | `scripts/UI/PausePanel.cs` with `tests/Pause/PausePanelTests.cs`: the sheen replaces the halftone, and the tag hangs from a `PanelTagAnchor` (see deviation 1). Padding constants untouched (TG-PAUSE's). |
| Captures | `scripts/Diagnostics/UiCapture/UiCaptureTargets.Widgets.cs` rewritten (12 targets), `widgets_roundel` in `UiCaptureTargets.WidgetsGauges.cs` restyled. The gauge targets (`widgets_gauges`, `widgets_slots`, `widgets_chips`) are step 2's. |

### API for Phase B

```csharp
// Chevron rows (BTN-A). Apply once; the decor draws the marker, label and badge.
PulpButtonDecor decor = PulpButtonDecor.Apply(button);                 // PulpButton / TemporalGlassButton rows
PulpButtonDecor footer = PulpButtonDecor.Apply(close, compact: true);  // PulpButtonCompact footers
footer.ShowPrompt = false;                                             // D6: compact footers show no prompt (default true)
decor.Selected = true;                                                 // or ToggleMode + ButtonPressed

// D6 width: every button of one stack shares the widest required width, min / cap; a label still too long
// shrinks, then ellipsizes. Call it once, before or after the screen is in the tree: the rule stays bound and
// runs again after a theme change (entering the tree, a UI-scale change) or a locale change. It sets ClipText.
PulpButtonDecor.ApplyStackWidth(menuRows, 560f, 700f);                 // main menu (MENU-A)
PulpButtonDecor.ApplyStackWidth(pauseRows, 560f, 680f);                // pause slab, TG-PAUSE's numbers
PulpButtonDecor.ApplyStackWidth(footerRow, 290f, reservePrompt: false); // compact footer

// Pause rows, panels, screens.
PulpScreenKit.StylePauseButton(row);                     // 560 x 64 glass chevron, label at 58, decorated
PulpScreenKit.DressPanel(panel, "level_title_key", sheen: true, buttonInset: true); // sheen, tag, 14 px stack inset
PulpScreenKit.AddPromptBar(root, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back")); // 70 / 44
PulpScreenKit.AddScreenStreak(root);                     // last child, z 10
PulpScreenKit.AddMenuScrim(root, mirrored: false);       // the glass_menu_scrim texture, full rect
PulpScreenKit.AddFlourishHeading(header, "select_title"); // rule-diamond-title-diamond-rule, BannerHeading role by default
PulpScreenKit.SeparateFocusRow(footerHBox);              // derived gap, never under 34 (Apply does this for a row of 2+)

// Title lock-up (6.5): one material per title label.
TitleGradient.Attach(fightersLabel);
GlassFlourish rule = GlassFlourish.Create(GlassFlourish.FlourishShape.Rule, 240f);

// Select tiles and chips (3.4.3).
GlassTileDecor tile = GlassTileDecor.Attach(tileControl);               // chamfer 16; chips: GlassTileDecor.ChipChamfer
tile.Portrait = portraitTexture;
tile.Owners = GlassTileDecor.OwnerP1 | GlassTileDecor.OwnerP2;        // the owner edges and badges
tile.LockedBy = 0;                                                    // -1 none, 0 P1, 1 P2
tile.Edge = GlassTileDecor.TileEdge.Lit;                              // Dim / Standard / Lit; Focus follows focus

// Portrait frame (G5), icons (ICN-A), toggles (G13).
var frame = new PulpPortraitRoundel { Diameter = 86f, Portrait = texture };
PulpIcon icon = PulpIcon.Create("shield", UIPalette.Accent, 24f);     // bold below 30 px; legacy names still draw
PulpToggleDecor.Apply(checkButton);
```

Notes:

- **A decorated button outside a D6 stack takes its width from its own (hidden, mixed-case) text** plus padding, while the decor draws the wider upper-case label and keeps the badge room, so its label shrinks a little. Give every decorated row an explicit width: the D6 helper, or a `CustomMinimumSize`.
- `PulpScreenKit.FocusPlateExpandRight` / `Left` are derived properties now, not constants (they were the typed 10 / 6 the review found): no current caller used them in a constant expression.
- `PulpScreenKit.ButtonStackRightInset` is now the 14 px added to a panel's own padding (`InsetRight`, pause 20 + 14); it was "at least 40".
- `DressPanel`'s third parameter is `sheen` (was `halftone`); no caller passed it by name.
- `PulpScreenKit.TagFromPanelCorner` is (26, -11); `PulpTaggedPanel.TagOffset` = `PulpTagPlate.PanelCornerOffset`, `PaddingBoxInset` 0, `TagDegrees` 0.
- `PulpScreenKit.ReserveBadgeSpace`'s default is the glass badge room, 52 (was 84). For a decorated row it is now redundant: the decor keeps the badge room itself, and the badge sits at `W - right padding - 36`, so the extra right margin moves it left. `CalibrationDrillList` still calls it (TG-SCREENS).

### Codex finding 1 (major): the derived focus-row gap

`FocusRowGap` = max(34, the shared theme's derived gap). The derivation reads, for each chevron variation
(`PulpButton`, `PulpButtonCompact`, `TemporalGlassButton`) at the widest row button (480 x 64), the `focus` overlay's and
the five state boxes' expand margins, and runs the real `FocusPose` path: progress 0 to the tween's sampled peak
(`FocusPose.Transition` / `Ease`, Quad Out, so 1.0: no overshoot), pressed or not (+6 px, 0.985 about the centre). The gap
is the larger of "left focused against the right one's own plate" and the reverse, plus 2 px of air.

| Case | Focused reach toward the neighbour | Neighbour's own plate | Gap |
|---|---|---|---|
| Glass kit (all three variations) | 14 slide + 14 overlay expand = 28 | 0 | 28 + 2 = **30**, floored at the spec's **34** (9.1 item 4) |
| Reverse direction | 14 overlay expand (the slide moves it away) | 0 | 16 |
| The test's wide-overlay probe (focus expand 12 / 23) | 14 + 23 = 37 | 0 | **39** (`RequiredRowGap`, the row widens itself) |

`RequiredRowGap(left, right)` reads each button's real boxes (local overrides included) and its own `FocusPose`;
`SeparateFocusRow` takes the largest over a row's neighbours and never goes under `FocusRowGap`. The rewritten
`UrPolish2Tests.AFocusedButtonInAHorizontalRowClearsItsNeighboursDrawnPlatesAtBothUiScales` reads the boxes itself
(not through the kit), focuses each button, samples its drawn rect over 16 frames of the slide and a press, and
checks it against the neighbour's own drawn plate, also mid-slide (rows: the confirm modal, the Move List footer, and
a probe row of two wide-overlay buttons).

### Codex finding 2 (minor): tags after a theme change

A theme change reaches the host before the tag's label, whose font size stays cached until the label is notified, so a
placement made inside the change measured the old size. `PanelTagAnchor`, `PulpTaggedPanel` and (through its anchor)
`PausePanel` now queue one coalesced, deferred placement on a theme change; a resize, a reparent or a visibility change
still places at once. `UrPolish2Tests.AnOverlongTagClampsInsideItsPanelAndFollowsALiveUiScaleChangeBothWays` builds an
anchor, a tagged panel and a pause panel just wide enough that a middling tag fits at 1.0 and needs the clamp at 1.4,
and rescales the same hosts 1.0 -> 1.4 -> 1.0 and 1.4 -> 1.0 -> 1.4 (`UIPalette.ApplyUiScale`), then swaps a 1.4 theme
in and out in one step (a single theme notification, which `ApplyUiScale` masks with its many); also no tilt.

### Hover ruling

`PulpButtonDecor` grabs focus on `MouseEntered` when the button is visible, enabled, focusable and not already focused,
so hover reads as focus (AC8): the marker, badge and pose follow the mouse. `GlassTileDecor` does the same for a
focusable tile.

### Test delta

`[TestCase]` attribute count 3698 -> **3709** (+11): the plan's step-1 **+9**, and +2 of mine.

- `GlassGeometryTests` new, 4: the chamfer and its 1.41 px hairline, the chevron's fixed run, a clipped fill's vertical moving end and its mirror, the CSS angle gradient (lengths 655.0 and 2202.3 px, a 3.3 px streak).
- `GlassDecorTests` new, 4: the sheen and streak bands scale with the panel (3.3 / 2.6 px on 620 x 214, 4.2 / 3.4 on 600 x 768.6, rebuilt once on a resize); the screen streak's bands where 1.4 puts them; every title label its own gradient material following the UI scale; a redraw without a change rebuilds no geometry in any step-1 draw widget (sheen, streak, flourish, tile, frame, ring, toggle, the button label, the marker).
- `HalftoneCornerTests` deleted, -2.
- `PulpButtonDecorTests` 12 -> 14 (+2): new `AStackSharesItsWidestRequiredWidthUpToItsCap` (D6, plan) and `AStackBuiltBeforeItsScreenIsInTheTreeTakesItsWidthThereAndFollowsTheUiScale` (mine, see deviation 2); rewritten `TheMarkerTintAndShadowFollowTheSpecTable` -> `TheDiamondMarkerFollowsTheSpecTablePerState`, `SelectedSwapsToThePaperPlateAndBackAndFollowsToggleMode` -> `SelectedSwapsToTheGlassSelectedChevronAndBackAndFollowsToggleMode`, `CompactButtonsGetTheSmallMarker` -> `CompactButtonsCentreTheirMarkerAtTwentyNine`, `TheBadgeShowsOnlyOnTheFocusedRowAndSwitchesBetweenPadAndKeyCap` -> `...AndHoverTakesFocus`, `ADisabledButtonDimsItsMarkerAndShowsNoBadge` -> `ADisabledButtonDashesItsMarkerAndShowsNoBadge`, `TheButtonsOwnGlyphsAreTransparentAndTheDecorDrawsThePerStateColours` -> `...TheGlassColourPerState`, `AFocusedLongLabelReservesTheBadgeAndIsTrimmedNotDrawnUnderIt` -> `ALongLabelShrinksToItsFloorThenEllipsizesAndKeepsTheBadgeRoom`; updated in place `ApplyAddsTheMarkerAndFocusPoseOnceAndIsHarmlessTwice`; unchanged the other four.
- `PulpIconTests` 3 -> 5 (+2): deleted `EveryKitIconNameLoadsAllThreeLayers` and `TheKitListMatchesTheFillFilesOnDisk` (TG-ART's `GlassKit` / `PulpKit` suites own the files); added `EveryGlassIconDrawsItsRegularTextureFromThirtyPixelsUp`, `EveryLegacyPulpNameStillDrawsThroughGlassAnAliasOrThePulpFallback`, `ALegacyNameResolvesThroughItsAlias`, `BelowThirtyPixelsTheBoldWeightIsDrawn`; `AnUnknownNameIsSafeAndDrawsNothing` unchanged.
- `PulpScreenKitTests` 4 -> 5 (+1): new `APauseRowStyledBeforeItsScreenIsInTheTreeDrawsTheThemesGlassBoxes` (mine, see deviation 3); rewritten `DressingAPanelPutsTheHalftoneBehindItsContentOnce` -> `...TheSheenBehindItsContentOnce` and `AButtonStackPanelKeepsTheFocusPoseInsideItsPlate` -> `AButtonStackPanelAddsTheFourteenPixelInsetToItsOwnPaddingOnce`.
- In place, no count change: `UrPolish2Tests` (the two findings' methods, renamed as above), `PausePanelTests` (the sheen, no tilt, an overlong tag does not widen the panel), `PulpPortraitRoundelTests` (`ThePortraitIsDrawnAt142PercentOffsetAndMaskedToTheDisc` -> `TheFrameIsChamferedAtTwentyTwoPercentAndTheArtIsTheInnerBoxAtOneFortyTwoPercent`), `PulpPromptBarTests` (pad and keyboard cases: the ring and the key-cap), `PulpTaggedPanelTests` (`TheTagKeepsItsRawKeyIsTiltedAndAnEmptyKeyHidesIt` -> `TheGlassTagKeepsItsRawKeyHangsAtTwentySixMinusElevenUntiltedAndAnEmptyKeyHidesIt`), `PulpToggleDecorTests` (both cases: G13 geometry, T54 colours), `UiShaderModulationScanTests` (`EveryPulpUiShaderKeepsTheIncomingModulation` -> `EveryUiShaderKeepsTheIncomingModulation`: it discovers every `*.gdshader` but `outline_glow.gdshader`, with a harness check that it found the title gradient).
- 3698 + 4 + 4 - 2 + 2 + 2 + 1 = **3709**. The findings changed no count (both rewritten in place).

### Runs (this worktree, the lock held for every launch)

- UI filter (plan 3.2): **721 / 721** at `aff5f2d7` (719 / 719 before the two additions; 710 + 11).
- The suites outside the UI filter that build a screen using these widgets (`MenuSceneContentTests`, `SceneSmokeTests`, `EpilogueFlowTests`, `CampaignCompletionTests`, `HubDifficultyLoweringTests`, `PauseFocusLossTests`, `FighterFlowRoutesTests`, `FighterDebugOverlayTests`, `StockDisplayModeTests`, `StoryLevelControllerBaseTests`, `StoryHealingLoopTests` and twelve more, plus `BootSequence`, `FighterPlayOptions`, `NetworkSelect`, `GameOver`, `HudBadge`): **205 / 205**.
- `dotnet build`: 0 errors, the two known warnings (`CS8632`, `KLSG_ECS004`). Headless `--import`: exit 0, no error. Capture runs: no engine error. `.import` and `default_bus_layout.tres` line-ending churn reverted, not committed.
- The first UI-filter run had two failures, both in my own tests: the decor's label colour compared to `UIPalette.Ink3` exactly (the theme stores seven digits; now 1e-4), and the long-label case measured a button its own hidden mixed-case text had widened (to 1818 px), so the label fitted at 29 px instead of shrinking to its floor (the case now clips the button, as a capped stack does; this found deviation 2's `ClipText`).

### Fail evidence (every new or rewritten case seen failing)

- **The two findings, against `993f4233`'s code** (logs `D:\Projects\ftt-scratch\tg-widgets\before-findings.txt`, `before-tag2.txt`): the row case reported "CancelButton and ConfirmButton are 32 px apart, under the spec's 34" (modal, Move List footer, probe row, at 1.0 and 1.4) and "focused WideA draws to (68, 946), (529, 93), over WideB's own plate"; the tag case reported the tilt (-1.2 degrees, anchor and pause), "pause A rescaled 1.0 -> 1.4: the label is not clipped although the tag needs 388 px ... the tag ends at 377, past the clamp 336", and on the single theme swap "anchor theme swap 1.0 -> 1.4: the label is not clipped" and "1.4 -> 1.0: the label is still clipped although the tag fits ... 347 wide, not its natural 305".
- **Mutation run 1** (17 mutations: the marker state order, the marker centres 31 / 29 -> 22 / 20, no focus on hover, the disabled tint, the disabled label slot, the selected plate from `normal`, no shrink, `BoldBelow` 30 -> 20, the regular texture always bold, no aliases, `Exists` glass-only, the fill cut `n - edge`, the chevron run H / 2, the fill slab past its edge, the gradient length with sin twice, `PausePanel`'s tag back on the panel, the sheen rebuilding every draw): **21 failed, 28 passed**, every targeted case at its own assertion (e.g. `ComputeMarkerState(false, false, true, true)`, `CentreX - 31`, `other.HasFocus()`, `Marker.Tint == MarkerDisabledTint`, `glass_btn_selected.png`, `DrawnFontSize == 26`, `Exists(name)`, `ResolveGlass("anchor")`, `UseBold(29.9)`, the chamfer hairline, the chevron at 530, the vertical edge at 190, t = 0 at 90 degrees, `Tag ... Position.Y < panel`, "sheen rebuilt N times"). Log `D:\Projects\ftt-scratch\tg-widgets\mut\run1.log`.
- **Mutation run 2** (13 mutations: band 1 from .565, the streak at 112 degrees, the title gradient without its hooks, the frame chamfer 25 %, the key-cap border 1, the pad ring in ink2, the tag tilted -1.2, the knob inset 4, "OFF" in ink3, `DressPanel` without the sheen, `InsetRight` not idempotent, the title shader assigning `COLOR` whole): **12 failed, 37 passed**, each at its own assertion ("vitals streak 1 is 6.51 px, not 3.3", the streak's x-ranges, `line_height` at 1.4, the 22 % corner, `ring.RingColor`, `BorderWidthLeft`, `Tag.Rotation`, `KnobX(0) == 5`, the OFF colour, `GetChild(0) is GlassSheen`, `own + 14`, "shader assignments dropping the modulate"). Log `...\mut\run2.log`.
- **Probes**: without `ApplyStackWidth`'s `ClipText` an overlong stacked button's combined minimum width is **1818** against the 700 cap (`...\mut\failprobe-stackclip2.log`); without the bound rule a stack built before the tree stays at **560** where it needs **616** (`...\mut\stackbind-probe.log`); with the earlier step-1 `PadLabel` the pause row "froze a copy of its normal box" (`...\mut\padlabel-probe.log`).
- Every mutation and probe was reverted by restoring backed-up copies (the mutation runs' files checksum-verified against the pre-mutation tree, the probes checked by grep); the runs above are on the restored tree.

### Captures

`D:\Projects\ftt-scratch\tg-captures\widgets\`, the windowed console build under the lock, 15 targets: `widgets_buttons`,
`_kb`, `_pressed`, `_140`, `widgets_icons`, `widgets_prompts_pad`, `_kb`, `widgets_slabs`, `widgets_continue`,
`widgets_tiles`, `widgets_title`, `widgets_streak`, `widgets_roundel`, `widgets_tagged_panel`, `widgets_toggles`.
Compared with `py tools/ui_theme/compare_capture.py` (the `__vs__` side-by-sides and overlays sit beside them):

| Capture | Crop | Result |
|---|---|---|
| `widgets_buttons` | `menu_button_column__from_01_86_505` | Marker at +40 and label at +68 from the crop edge, as the mock; focus fill, white outline, glow, the A badge and the 14 px slide match. "CALIBRATION DRILLS" draws 454 px against the mock's 460 (Godot's `spacing_glyph` is an integer: 4 px for 0.13 em of 34 = 4.42). The column is **616** wide by D6 where the mock draws 560 (the mock's D6 defect); row pitch 80 against 79. |
| `widgets_slabs` | `pause_panel__from_03_640_66` | The glass chevrons, the focused row (gold fill, white outline, glow, badge), the tag, the sheen streaks and the title match. Differences not mine: the rows start 23 px inside the slab against the mock's 18 and the title sits about 15 px lower (UR-PAUSE's padding constants, TG-PAUSE's to re-value); the replica keeps the mock's 560 rows, so the longest labels shrink to keep the badge room (TG-PAUSE's 680 D6 slab draws them full size). |
| `widgets_prompts_pad` | `menu_prompt_bar__from_01_1580_995` | The same bright-pixel extent (252 against 253 px wide) and rows, 3 px to the right. |
| `widgets_continue` | `menu_continue_card__from_01_1238_818` | The G5 frame and the card's tag, slab and text match; the replica's line spacing is looser than the mock's (the card's layout is TG-MENU's). |
| `widgets_tiles` | `select_tile_grid__from_02_688_186` | Chamfers, fills, the P1-lock tile (cyan edge, glow, check badge, triangle), the P2-hover tile (red edge, dot), the RANDOM tile: a close match. The build's tile names are a little larger than the mock's (the role is TG-SELECT's) and the owner glows a little stronger. |
| `widgets_title` | `menu_title_lockup__from_01_100_50` | Flourish, the gradient (white to the blue-grey), THROUGH with its rule and TIME match; the mock's "TEMPORAL RESONANCE" line is TG-MENU's. |
| `widgets_streak` | `select_title__from_02_270_25` | The flourish heading matches. |

No crop exists for the icons, the key-cap bar, the 1.4 sheet, the pressed row, the frames, the tag hosts or the toggles;
each was looked at: 34 icons in both weights with the alias and fallback row; ENTER / ESCAPE key-caps; at 1.4 the menu
stack caps at 700 and its compact footer takes the D6 compact width; the pressed row draws the pressed fill with the
focus outline and the press transform, the 680-capped stack shrinks "QUICK PLAY (EINSTEIN VS CPU)" and ellipsizes the
over-long row; the toggles draw the 3.3 knob glow (the mock clips it, D4).

### Deviations and why

1. **`PausePanel` hosts its tag in a `PanelTagAnchor`** (a cross-edit beyond "halftone -> sheen"). Its tag was a direct child of the `PanelContainer`, which counts every child in its minimum size, so an overlong tag widened the panel instead of clamping, and the old tag test passed vacuously (the panel grew with it). Node `PanelTagAnchor`, public `TagText` / `Tag` unchanged.
2. **`ApplyStackWidth` binds the rule and sets `ClipText`.** Called before its screen is in the tree, a button resolves no theme, so the first width was only the minimum (my own capture showed 560 rows with shrunk labels); a UI-scale or locale change also changes the widest label. The rule now runs again, once, deferred, after a theme or locale change reaches the stack. `ClipText` keeps the button's own hidden glyphs from widening it past the cap (an overlong label made 1818 px). Neither is in the plan text; both are what AC6 needs at 1.4 and for code-built stacks. +1 test.
3. **`StylePauseButton`'s `PadLabel` reads the shared theme.** My first step-1 version read the button's boxes, which outside the tree resolve the engine's default box; it then froze that copy (the Fighter pause builds its rows so: `LocalFighterPause` line 335). It now reads the row's own override or the shared theme's box for its variation, as `main` did, and writes only when the left padding is not 58 (the glass theme already pads 58, so normally nothing is written and the rows stay theme-driven). +1 test.
4. **The tag plate is about 29 px tall, not 24**: the theme's `PanelTagPlate` (content margins 3 / 3) around an Outfit line. The plate is centred on the spec tag's centre (y -11 + 12). TG-THEME could tighten it.
5. **The decor scales heights, paddings, the marker and the badge by `UIPalette.AppliedUiScale`** (clamped to the save's 0.9-1.4), not widths. A size the screen writes later (per axis) becomes the new design size.
6. **`ShowPrompt` defaults to true for every decor, compact included**, so existing compact rows (the confirm modal's) keep their badge; D6 footers set it false.
7. **`GlassFlourish` has a fourth shape, `Rule`** (a rule fading out, no diamond), for the THROUGH rule of the title lock-up.
8. **`GlassMarker` derives from `PulpIcon`** so `PulpButtonDecor.Marker` keeps its type; the legacy `MarkerTint` / `MarkerHasShadow` stay (they now return the glass values) and `SelectedPlateHeight` stays as a legacy table the glass decor no longer reads.

### Not seen

Real screens (Phase B), motion in flight (the 120 ms slide and press only as end states, the light band only in tests),
Reduced Temporal Effects, gamepad glyphs other than A / B, any locale but English, the tile decor and the frame inside the
real select and menu screens.

### For the orchestrator

1. **D6 reserves 52 px for the pad ring; a keyboard key-cap is wider** ("ENTER" about 80), and the decor reserves the badge it really has, so on keyboard the widest row's label shrinks a few percent (`widgets_buttons_kb`). Keep, or make D6 reserve the current device's widest badge and re-run on a device change?
2. **Tracking and D6 numbers**: Godot measures "CALIBRATION DRILLS" at 616, not the spec's 636, because `spacing_glyph` is an integer (4 px for 4.42); and the theme's `spacing_glyph` does not follow the UI scale (4 px at 1.4 as well). Both are TG-THEME's generator; flag only.
3. **Compact prompts**: should `Apply(button, compact: true)` default `ShowPrompt` to false (D6 says compact footers show none)? I kept true to leave the confirm modal's badge.
4. **The tag plate height** (deviation 4): tighten `PanelTagPlate` to 24 in the generator?
5. **Non-stacked decorated rows** take their width from their own text (the API note above). Should Phase B be told to give every decorated button an explicit width, or should the decor itself widen a row to fit its upper-case label?

### Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the UI section's pulp-redesign bullet, the widgets sentence, to replace "PulpIcon (three-layer ICN-B icons ...)", "HalftoneCorner" and "PulpPortraitRoundel": "Since the 2026-10-27 Temporal Glass rebuild (TG-WIDGETS) the widgets draw glass: `PulpButtonDecor` the BTN-A chevron row (the diamond `GlassMarker`, the focused row's prompt badge, the `selected` chevron, shrink then ellipsis, hover takes focus) with the D6 stack width (`ApplyStackWidth`, bound to the tree, the UI scale and the locale), `PulpIcon` the ICN-A glass icons (bold below 30 px; legacy names through aliases or the frozen pulp layers), `GlassSheen` / `ScreenStreak` / `GlassFlourish` / `TitleGradient` / `GlassTileDecor` the panel sheen, the screen streak, the flourish, the title gradient and the select tile, `PulpPortraitRoundel` the G5 chamfered frame, `PulpToggleDecor` the G13 toggle; `HalftoneCorner`, `halftone_corner.gdshader` and `ui_circle_mask.gdshader` are deleted. `PulpScreenKit.FocusRowGap` is derived from the theme's focus and state boxes and the real focus pose (30, floored at 34)."
- AGENTS.md repository map, `assets/shaders/`: drop `halftone_corner` and `ui_circle_mask`, add `ui_title_gradient`.
- Ledger, under the glass section: `PROVISIONAL-UI-GLASS-WIDGETS` (deviations 4-8 and questions 1, 3, 5 above), and `REVIEW-UI-PULP-CODEX-3` findings 1 and 2 closed by `aff5f2d7`.

## Step 2 (2026-10-27)

Step 2 is, in the orchestrator's order: ruling 5 (its own commit, sent ahead), Codex review 1's four findings, then the
gauges (plan section 2.5). `main` (`8612b571`) was merged into `tg/widgets` first (a fast-forward). Commits:
`aec29b3b` ruling 5 (3711), `aa7ee210` review 1 (3715), `ad12f9b3` the gauges (3720), `cb5ffa80` a legacy constant
kept (deviation 9 below), and this handoff.

### Ruling 5: the decor widens a row no screen sized (`aec29b3b`)

A decorated button that is not in a D6 stack, has no width of its own (`CustomMinimumSize.X` 0 when the decor first
sees it), draws its label upper-case on one line and is not clipped (`ClipText` off, no overrun) now gets its minimum
width from the decor: the left and right paddings and the badge reserve (52, when the row can show a prompt) times the
UI scale, plus the icon and its separation, plus the drawn label's width, rounded up (`AutoWidth`, cached on the drawn
text, font, size and fixed part). It follows a theme change (the UI scale), a locale change, a text change (the
button's redraw) and the button's own `minimum_size_changed`, deferred; it writes only a changed size, so the resize it
causes refreshes to the same value and nothing loops. A width the screen sets later wins (it becomes the design width).
**This supersedes step 1's first API note and question 5**: Phase B does not have to size a decorated row.

Limitation: a screen that sets a row's width back to 0 does not always reach the decor, because Godot raises
`minimum_size_changed` only when the combined minimum changes; call `decor.Refresh()` after doing that.

Tests +2 (`PulpButtonDecorTests` 14 -> 16): `AnUnsizedRowWidensToItsDrawnLabelAndFollowsItsTextAndTheUiScaleWithoutALoop`
and `ARowWithTheScreensWidthAStacksWidthAWrapOrAClipKeepsThatWidth`. Both fail on `8612b571`'s decor ("'616' but is
'0'", log `D:\Projects\ftt-scratch\tg-widgets\mut\r5-before.log`).

### Codex review 1 (`aa7ee210`), +4

| Finding | Fix | Test (seen failing against `8612b571`'s sources) |
|---|---|---|
| 1, major: `PulpMotion`'s offset driver and `PopIn` let a re-open or a close mid-motion stack a second tween on the same panel, which then ended off its slot | `PopIn` / `PopOut` first take the panel over (`TakeOver`: kill its running tween, settle its driver on the slot) and register the new one (`Own`, removed when it finishes; the registry is purged past 16) | `PulpMotionTests.APanelReopenedOrClosedMidMotionEndsExactlyOnItsSlot` (+1): "'(100, 200)' but is '(100, 416.11)'" |
| 2, major: hover-is-focus took the keyboard from an active text field | `PulpButtonDecor.KeyboardIsHeld(control)` (the focus owner is an editable `LineEdit` / `TextEdit`, or an `OptionButton` / `MenuButton` with its list open) stops hover taking focus, in the decor and in `GlassTileDecor`; the row and the tile then show their hover look without the focus | `PulpButtonDecorTests.HoverLeavesTheKeyboardWithATextFieldOnARowAndATile` (+1): "hovering the row took the keyboard from Address" |
| 3, minor: the paragraph cache key left out the available height | the key has it (`_layoutHeight`, multi-line rows only) | `PulpButtonDecorTests.AWrappingLabelShowsMoreOrFewerLinesWhenOnlyItsButtonsHeightChanges` (+1): "the shorter button keeps showing every line" |
| 4, minor: `GlassTileDecor` kept its owner colours after a slot-palette change | its geometry cache keys on the P1 / P2 colours it built with | `GlassDecorTests.AnOwnedTileTakesTheNewOwnerColoursWhenThePlayerSlotPaletteChanges` (+1): "the edge kept the old palette's colours" (every edge vertex listed) |

Log `D:\Projects\ftt-scratch\tg-widgets\mut\review1-before.log` (the four sources of `8612b571` with the new tests: 6 of
35 failed, the four above and the two ruling-5 cases). A harness note: Godot 4.7 does not raise `mouse_entered` for a
scripted `Notification(NotificationMouseEnter)`, so the hover cases emit the signal and the notification themselves.

### What shipped (the gauges, `ad12f9b3`)

| File | Change |
|---|---|
| `scripts/UI/Pulp/PulpBarDecor.cs` | GAUGE-A (3.4.1, 3.6): the whole tube drawn in code as polygons clipped to the inner parallelogram. The outer parallelogram (run 12, boss 16, leaning "/") in `edge` (boss: the 110 degree gradient, `BossEdge` .95 to .25 at .40 to white .5), the inner one inset 1.5 on run 11 (15) with the 180 degree track gradient, the tube's glow (10, boss 12) as a skewed flat box. The tracker to `LagValue`; the Rally echo band from the fill's end to `EchoValue`, inset 22 % of the inner height, white to `echo` with a 3 px halo; the fill a vertex gradient from the anchored end (on the slant) to a vertical moving end (Hp: `hp2` to `hp`, or a darker tint to the bar's fill colour for `TintFill`; Low: `boss2` to `danger`; Meter: `meter` to `meter2`, full: `res` to `res2`; Stat: `accent` to `accent2`; Boss: `boss2` to `boss`); the top-46 % highlight; the 3 px white cap with its two-sided glow (none on Stat or Boss); the ten 1 px decile ticks (none on Boss); the 7 px low pulse (0.5 Hz, steady under Reduced) and the 14 px full-meter glow; the boss notches (a 2 px white line from 8 above to 14 below, its glow, a 12 px accent diamond 16 below; at most four). `FillMode` `EndToBegin` mirrors all of it (R6). Geometry is rebuilt only when a value, the size, the style, the fill colour or the direction changes (`GeometryBuilds`); a draw allocates nothing. The bar's own drawing is hidden by a material and the tube follows the bar's `SelfModulate` (deviation 1). |
| `assets/shaders/ui_hide_self.gdshader` (new, with its `.uid`) | `COLOR.a = 0.0`: the one shared material that hides a decorated bar's own drawing; children do not use a parent's material, so the tube, the value labels and the not-yet frame draw as usual. `UiShaderModulationScanTests` discovers it and passes (it adjusts `COLOR` in place). |
| `assets/shaders/pulp_bar_layers.gdshader` | Deleted, with its `.uid` (nothing else loaded it). |
| `scripts/UI/Pulp/PulpAbilitySlot.cs` | The lens (3.4.2): the art inset 9 % and clipped to the lens circle (a polygon with UVs, through the `ui_desaturate` material for saturation and brightness), the `glass_lens_radial` fill, the 1.5 px edge, the outer and inner glows (ready: the resonance glow; suppressed: the Severed cold), the cooldown sweep (`SweepShade`, clockwise from 12), the readiness ring outside the lens (small: 3 px, outer edge 6 px out, so 74 -> 86, 54 -> 66, 56 -> 68; the Ultimate: 5 px, 9 px out, the 92 lens in a 110 box), on an `accent` .16 track; the Ultimate's charging arc a `meter` to `meter2` gradient and its ready ring a looping `res2` / `res` / `res2` gradient with READY; the lock overlays (`lock_dormant`, `lock_suppressed`) inset 16 %; the centre number at 0.36 x the lens following the theme's `SlotNumber` size (19 / 27 / 33); the key label 9 px under the lens; one bloom on entering ready (MOT-A row 10, none under Reduced). |
| `scripts/UI/Pulp/PulpStatusChip.cs` | G7 (3.4.3): the `glass_badge_radial` disc with a 14 px `accent` .12 glow and a 1.5 px `edge` ring, the glyph at 62 % (bold below 30 px) in the status colour, a 3 px duration ring with its outer edge 5 px outside the badge (44 -> 54, 40 -> 50); G8 (empty): a 1.5 px `edge-dim` ring and the text at .35. |
| `scripts/UI/RadialProgress.cs` | Additive: `GradientMode` (`None`, the default, with every existing mode unchanged; `Arc`, the filled arc from `FillColor` to `FillColorEnd`; `Loop`, by position round the whole ring), `FillColorEnd`, the pure `GradientColorAt`; the default track written out as `DefaultTrackColor` #3A3144, the pulp `TrackBackground`, so the world-space Restoration Font and Resonance Hold rings never follow a palette change (ruling). |
| `scripts/UI/NotYetFlash.cs` | Additive: `NotYetShape { Frame, Circle }` and `Play(host, tint, shape)`; the circle is a 2 px `danger` ring 2.5 px outside the host's own circle, contracting from 1.35x and fading over the same 0.25 s (`CircleOf`, `CircleName`, `CircleStroke`, `CircleOutset`). `Play(host, tint)` is unchanged (the frame). |
| Captures | `UiCaptureTargets.WidgetsGauges.cs`: `widgets_gauges`, `widgets_slots`, `widgets_chips` rewritten; `widgets_notyet` new; `widgets_roundel`, `widgets_tagged_panel`, `widgets_toggles` kept. |

### API for Phase B (TG-HUDS, TG-HUDF, TG-SELECT)

```csharp
// Tubes (GAUGE-A). The ProgressBar stays the value carrier: Value / MinValue / MaxValue, FillMode (EndToBegin mirrors
// the tube for P2, R6), the fill StyleBoxFlat's bg_color (the HP tint; FighterHUD.TintFill keeps working) and its
// SelfModulate (the flash channel: PulpMotion.HitFlash, NotYetFlash), which the tube follows. Attach once.
PulpBarDecor hp = PulpBarDecor.Attach(hpBar);                            // PulpBarStyle.Hp
PulpBarDecor meter = PulpBarDecor.Attach(meterBar, PulpBarStyle.Meter);  // full at the max: res -> res2 and the glow
PulpBarDecor stat = PulpBarDecor.Attach(statBar, PulpBarStyle.Stat);     // no glow
PulpBarDecor boss = PulpBarDecor.Attach(bossBar, PulpBarStyle.Boss);     // run 16, no cap, no ticks
hp.LagValue = before;          // the damage tracker, in bar units; hp.DropLagTo(after) holds, then drains (MOT-A row 7)
hp.EchoValue = hpNow + echo;   // the Rally echo band's end, in bar units
hp.Low = lowHp;                // boss2 -> danger and the 7 px 0.5 Hz pulse (steady under Reduced)
boss.PhaseNotches = new[] { 0.5f };   // at most four

// Lenses (3.4.2).
var lens = new PulpAbilitySlot { SlotSize = 74f };    // 74 Story, 54 Fighter, 56 select; Ultimate = true: 92 in a 110 box
lens.Art = AbilityIconLibrary.Load(heroId, AbilitySlot.Special1);
lens.KeyText = "hud_slot_special1";                    // a raw key, 9 px under the lens; KeyOverScene for the scene role
lens.State = PulpSlotState.Cooling; lens.Progress = elapsedShare; lens.SecondsRemaining = seconds;
lens.State = PulpSlotState.Charging; lens.Progress = meterShare;   // the Ultimate: the percent and the meter arc
lens.State = PulpSlotState.Ready;                      // one bloom on entering (none under Reduced)
lens.State = PulpSlotState.Dormant;                    // or Suppressed: lock_dormant / lock_suppressed
NotYetFlash.Play(lens.Ring, lens, NotYetShape.Circle); // the glass not-yet: a danger circle round the ring

// Status chips (G7, G8; 3.4.3).
var chip = new PulpStatusChip { BadgeSize = 44f, Kind = PulpChipKind.Damage, GlyphName = "venom",
    Remaining = share, NameText = "status_venom", TimeText = "4s" };   // Empty = true for G8; ShowText = false
```

Notes:

- Read-only seams for tests: `PulpBarDecor.OuterPolygon`, `InnerPolygon`, `EdgeColors`, `TrackerPolygon`, `EchoPolygon`, `FillPolygon`, `FillColors`, `FillGradient`, `CapPolygon`, `TickPolygons`, `Run`, `Mirrored`, `GeometryBuilds`, `HideMaterial`; `PulpAbilitySlot.LensSize`, `LensOrigin`, `Ring`, `KeyTag`, `ReadyTag`, `CenterLabel`, `GlyphIcon`, `ArtNode`, `GeometryBuilds`, `Blooms`, `BloomGlow`, `Look`; `NotYetFlash.CircleOf`.
- `PulpBarDecor.InnerRect()` and `Skew` are the pulp overlay's legacy reads, kept; the tube measures its own inset (`TubeInnerBox`).
- `BossPhaseNotchOverlay` already stands down when a decor draws the notches (UR-HUDS); the glass notches are white with an accent diamond.

### Deviations and why

1. **The tube hides the bar with a material, not the bar's `SelfModulate`.** My first build hid the bar through its `SelfModulate` alpha and redirected the not-yet tint to the tube. The UI filter then failed `FighterHudSceneTests.ANotYetRefusalFlashesThatPlayersIconOrMeter` ("'(0.95, 0.25, 0.25, 1)' but is '(1, 1, 1, 0)'") and `FighterHudTopBandTests.TheHitFlashPlaysOnceASecondPerBar` ("'(1, 1, 1, 1)' but is '(1, 1, 1, 0)'"), log `D:\Projects\ftt-scratch\tg-widgets\s2-ui.log`: both HUDs use the bar's `SelfModulate` as their flash channel, and the hit flash would have gone silently invisible (it keeps the alpha it finds). Now the bar wears `ui_hide_self.gdshader`, its `SelfModulate` stays free, and the decor copies the bar's `SelfModulate` onto its own every frame (the decor runs `ProcessMode.Always`, so a flash the bar's tween finishes in a paused tree still reaches the tube; the low pulse still stops with the bar). `NotYetFlash` needs no redirect; the HUD tests pass unchanged; removing the decor restores the bar's material. TG-CLOSE can drop the material once the five bar variations are empty.
2. **`PulpSlotLook.GlyphName` stays logical** ("dormant", "cross"), because `StoryHudPulpTests` reads it; the lens draws `LockIconFor(name)`: `lock_dormant`, `lock_suppressed`.
3. **The Ultimate's box is 110** (the ring's outer diameter, 92 + 2 x 9), down from the pulp 116.
4. **A dormant slot's key label is not dimmed** (`Look.KeyDim` false; agent choice, the spec names none).
5. **`Blooms` counts started blooms**: none under Reduced Temporal Effects, where `PulpMotion.Bloom` returns no tween and the steady look shows at once.
6. **The not-yet circle's host is the lens's `Ring`**, so the circle sits outside the ring; the tint target stays the caller's (the slot tints its own lens fill and edge, not its children). The HUDs choose their hosts in Phase B; today's HUDs still play the frame.
7. **The suppressed glow and ring use `UIPalette.UnboundCold`** (0.2, 0.95, 1), 0.02 from the spec's #38F2FF: `UiChromeCyanSourceScanTests` rejects a cyan literal (my first build had one) and Suppression is the Severed's cold.
8. **`PulpBarDecor.ResGlow`** (rgb 242, 204, 77) is the one statement of the spec's resonance glow, read by the full meter and the ready lens.
9. **`PulpBarDecor.ShaderPath` is kept** (`cb5ffa80`) as a documented legacy constant naming the deleted shader, under the Phase A rule that no public widget member goes before TG-CLOSE; nothing reads it.

### Test delta

`[TestCase]` attribute count 3709 -> 3711 (ruling 5, +2) -> 3715 (review 1, +4) -> **3720** (the gauges, the plan's +5):

- `PulpBarDecorTests` 3 -> 6 (+3): new `TheTubeLeansAndAMirroredBarMirrorsItsLeanAnchorGradientAndCap` (with the hiding material, the flash mirror and the restore on removal), `TheEchoBandRunsFromTheFillToHpPlusEchoInsetTwentyTwoPercent`, `TheBossTubeDrawsTheBossEdgeAndNoCapOrTicks`; updated `TheLowGlowPulsesAtHalfAHertzAndIsSteadyUnderReducedEffects` (was `TheLowGlowIsOffStaticWhenReducedAndASlowSineOtherwise`); unchanged `TheFractionsAreClampedToTheBarsRange`, `TheDecorOverlaysTheBarWithoutTouchingItsFill`.
- `PulpAbilitySlotTests` 4 -> 6 (+2): new `TheReadinessRingSitsOutsideTheLensAtItsSpecWidth` (with the not-yet circle round the ring and a fresh `RadialProgress`'s #3A3144 track and `None` mode), `AReadyStateBloomsOnceAndARedrawWithoutAChangeRebuildsNothing`; updated `TheStateTableMatchesTheSpec`, `TheBuiltSlotShowsTheRightPartsPerState`; rewritten `TheCentreNumberAndTheArtInsetFollowTheSpec`; unchanged `TheCountdownTextDoesNotDependOnWhichValueFilledTheCacheFirst`.
- `PulpStatusChipTests` 2, updated in place (the 3 px ring 5 px out, the G8 `edge-dim` ring; the glass glyph, its bold weight and its colour).
- 3709 + 2 + 4 + 5 = **3720**. No other suite changed.

### Runs (this worktree, the lock held for every launch)

- UI filter: **732 / 732** at `ad12f9b3` (727 + 5; 727 / 727 at `aa7ee210`). The first step-2 run had three failures: the two Fighter HUD cases of deviation 1 and `UiChromeCyanSourceScanTests` on the lens's cold-glow literal (deviation 7).
- The suites outside the UI filter that build a screen with these widgets (step 1's list): **205 / 205** at `ad12f9b3`.
- `dotnet build --no-incremental`: 0 errors, the two known warnings (`CS8632`, `KLSG_ECS004`). Headless `--import`: exit 0, no error (it wrote `ui_hide_self.gdshader.uid`). Capture runs: no engine error. `.import` and `default_bus_layout.tres` churn reverted, not committed.
- `cb5ffa80` adds only a constant; it was built, not re-run.

### Fail evidence (every new, updated or rewritten case seen failing)

- **Ruling 5 and review 1**: against `8612b571`'s sources, above.
- **Deviation 1's two Fighter HUD cases** failed against the `SelfModulate` hiding (`s2-ui.log`) and pass against the material.
- **Mutation runs on the gauge code** (17 mutations in four batches; the gauge suites, 14 cases; logs `D:\Projects\ftt-scratch\tg-widgets\mut\s2a.log` to `s2d.log`, the harness `mut\mutate2.py`):
  - `s2a` (7 failed, 7 passed): the reduced glow pulsing ("'1' but is '0.206'"), the mirrored tube leaning "/" ("mirrored outer lacks (0, 0) | ..."), the echo inset .30 ("the band lacks (227.7, 7.44) | ..."), ticks on the boss tube ("'0' but is '10'"), the chip ring 4 px out ("'27' but is '26'"), the chip glyph in ink2 (the tint), the not-yet circle branch removed (`CircleOf` null).
  - `s2b` (6 failed, 8 passed): Cooling's brightness .80 ("'0.72' but is '0.8'"), the art inset 10 % (60.68), the key gap 6 (the key label's y), the small ring 4 px out ("'(86, 86)' but is '(82, 82)'"), the lens rebuilt on every draw ("'2' but is '43'" builds), the tube not following the bar's `SelfModulate` ("'(0, 0, 0, 1)' but is '(1, 1, 1, 1)'").
  - `s2c` (3 failed, 11 passed): a fresh ring's track `accent` (the #3A3144 check), the bar not hidden (`bar.Material == HideMaterial`), two blooms on one entry ("'1' but is '2'").
  - `s2d` (1 failed, 13 passed): no restore when the decor leaves the bar (`bar.Material == null`).
  - Every targeted case failed at its own assertion. The sources were restored from backups and checksum-verified (`mut\md5-s2-before.txt`) before the runs above.

### Captures

`D:\Projects\ftt-scratch\tg-captures\widgets\`, the windowed console build under the lock: `widgets_gauges` (every tube
style both ways, low, echo, the meter charging / full / mirrored, the stat bar, the boss tube with a notch),
`widgets_slots` (every state at 74, 54 and 56; the Ultimate charging, ready, suppressed and dormant),
`widgets_notyet` (6 frames in: the circle round a 74 lens's ring and round the Ultimate, the frame on a 54 lens),
`widgets_chips` (each kind at 44 and 40, G8, a chip without text), and `widgets_roundel` again. Side-by-sides and
overlays from `py tools/ui_theme/compare_capture.py` sit beside them:

| Capture | Crop | Result |
|---|---|---|
| `widgets_gauges` | `boss_bar__from_05_700_8` | The run-16 lean, the `boss2` to `boss` fill, the grey tracker, the dark track, the white notch line with the gold diamond 16 px below: a close match. |
| `widgets_gauges` | `hud_fighter_p1__from_06_24_14` | The HP tube: the lean, the green gradient, the white cap at the fill's end, the red tracker, the gold edge and glow match. The mock has no echo band to compare. |
| `widgets_slots` | `hud_story_left_column__from_04_20_14` | The Ultimate at 64 % (the gradient arc, the dimmed art, the number), S1 ready (the gold ring), S2 cooling ("2.4", the sweep, the arc), MOV: a close match; my ready glow is a little stronger than the mock's. |
| `widgets_chips` | the same crop | The VENOM chip (skull in the damage colour, the partial ring, the disc and edge) and the empty G8 ring match. |

The tubes show no pulp art under them (zoomed, `D:\Projects\ftt-scratch\tg-widgets\zoom_tube_story.png` and
`zoom_tube_meter_p2.png`): the material hides the bar's own drawing in `gl_compatibility`.

### Not seen

The real HUDs and the select screen (Phase B); motion in flight (the bloom, the low pulse, the tracker drain, the
not-yet contraction past one frame, the hit flash on a tube); Reduced Temporal Effects on screen; UI scale 1.4; a P2
meter beside its HUD; the lens over a busy level background.

### For the orchestrator

1. **The hiding mechanism** (deviation 1): a shared material, with the tube following the bar's `SelfModulate`. Keep it, or have TG-CLOSE flip the five bar variations sooner and drop the material?
2. **The hit flash on a tube** is the pulp one-frame black flash, now multiplying the whole tube (a black tube for a frame). MOT-A's glass hit flash may want something else; that is TG-HUDF's and TG-HUDS's call through `PulpMotion.HitFlash`.
3. **The not-yet hosts**: should the HUDs play the circle round `lens.Ring` with the slot as the tint target (it reddens the lens fill, not the art)? Today's HUDs still play the frame; Phase B decides.
4. **The Ultimate box is 110, not 116**: the Story and Fighter HUDs pass as they are, but their layouts are Phase B's to re-check.

### Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the UI section's pulp-redesign bullet, after step 1's widgets sentence: "TG-WIDGETS step 2 (2026-10-27) made the gauges glass: `PulpBarDecor` the GAUGE-A tube drawn in code over a `ProgressBar` that stays the value carrier (the tracker, the Rally echo band, the vertex-gradient fill with a vertical moving end, the cap, the ticks, the low pulse, the full-meter glow, the boss edge and notches; `FillMode` `EndToBegin` mirrors it for P2), which hides the bar's own drawing with `ui_hide_self.gdshader` and follows the bar's `SelfModulate` (the HUDs' flash channel); `PulpAbilitySlot` the lens (the 9 % inset art clipped to the circle, the readiness ring outside it, the Ultimate's 92 lens in a 110 box with the meter arc and the looping ready ring, the lock overlays, one bloom on ready); `PulpStatusChip` the G7 badge with its 3 px duration ring and the G8 empty ring; `RadialProgress` gains `GradientMode` (its old modes and the #3A3144 default track unchanged); `NotYetFlash` gains the circle shape. `pulp_bar_layers.gdshader` is deleted. A decorated button no screen sizes takes its width from its drawn label (ruling 5)."
- AGENTS.md repository map, `assets/shaders/`: drop `pulp_bar_layers`, add `ui_hide_self`.
- Ledger, under the glass section: `PROVISIONAL-UI-GLASS-WIDGETS` gains deviations 1-8 of step 2 and questions 1-4 above; Codex review 1 of Phase A: findings 1-4 closed by `aa7ee210`.

## Fix step (2026-10-27): the lens's centre number is scaled once

Plan standing decision 9: a widget bug found in Phase B comes back to TG-WIDGETS. `main` (`4761fc1e`: TG-MENU,
TG-SCREENS and TG-PAUSE merged, 3724) was merged into `tg/widgets` first (a fast-forward). Commit `92f29657`, then this
section.

### The bug (found by TG-HUDF, `hudf_140.png`)

`PulpAbilitySlot.Relayout` read the `SlotNumber` font size from the shared theme (`UIPalette.LoadTheme()`), which
`UIPalette.ApplyUiScale` has already grown. Both HUDs give their roots an unscaled copy of the theme
(`UIPalette.NewUnscaledTheme()`) and magnify the whole layout through `UiScaleBinder` instead, so a lens in a HUD was
scaled twice: at UI scale 1.4 its number was 38 px (`CenterFontSize(74, 27)`) under a 1.4x root, 53 px on screen,
against 27 px (37.8 on screen).

### The fix

`PulpAbilitySlot.SlotNumberSizeInContext` (new, public, read-only) resolves the `SlotNumber` size the way a `Label`
under the lens would: through the nearest ancestor's theme (in a HUD, the unscaled copy; on a screen that wears the
shared theme, the scaled size), else the authored 19 (`SlotNumberBaseSize`) when nothing resolves it (outside the
tree). `Relayout` passes it to `CenterFontSize`, so the UI scale reaches the number exactly once in either context. The
lookup uses a static `StringName`; `Relayout` runs on a layout or theme change, never in a draw. Every public member is
kept. The number re-lays itself out on the theme change a HUD's root theme or the shared theme's rescale already sends.

### The other widgets, checked

- **The decor's label** (`PulpButtonDecor`): drawn at the button's own resolved font size (`_button.GetThemeFontSize`), so it is scaled once in any context. Its geometry (paddings, heights, the marker, the badge, the shrink floor) follows `UIPalette.AppliedUiScale`, which is right on every screen that uses it; no decorated button sits under a `UiScaleBinder` root today (the HUDs have none). Under one, that geometry would be scaled twice. Not changed: there is no instance, and a context-aware scale would need the authored sizes, which only `UIPalette` (outside `scripts/UI/Pulp/`) holds.
- **The tube's values** (`PulpBarDecor`): the tube draws no text; the HP and meter values are the HUDs' own labels, which resolve their roles through the HUD's unscaled theme.
- **The chip's text** (`PulpStatusChip`), the lens's key label and READY, the panel tags (`PulpTagPlate` measures with the label's own size), `TitleGradient`: all `Label`s resolving their roles in their own context. Scaled once.

### Test delta

`PulpAbilitySlotTests` 6 -> 7 (+1): `TheCentreNumberIsScaledOnceInAScaledHudAndOnAThemeScaledScreen`. At UI scale 1.4
(the saved setting and the shared theme both) it builds a HUD root the way StoryHUD and FighterHUD do (an unscaled theme
copy, then `UiScaleBinder.Apply`) holding a 74 lens and a 44 chip, and a screen on the shared theme holding a 56 lens,
and checks what is drawn (the font size times the canvas scale): the HUD lens's number about 0.36 x 74 x 1.4 = 37.3,
the HUD lens's key label and the chip's name at their authored sizes x 1.4, and the screen lens's number about
0.36 x 56 x 1.4 = 28.2. Against `4761fc1e`'s lens it fails with "the HUD lens's number draws at 53.2 px, not about
37.3" (log `D:\Projects\ftt-scratch\tg-widgets\mut\fix-before.log`); the other three checks pass on both.

`[TestCase]` count on this branch 3724 -> **3725**; with TG-HUDF's 3726 merged first, **3727**.

### Runs (the lock held for every launch)

- UI filter: **737 / 737** with the fix. The extra screen suites: **206 / 206**. `dotnet build`: 0 errors.
- Captures, before and after, `D:\Projects\ftt-scratch\tg-captures\widgets-fix\`: main's `huds_140` and `hudf_140` set only the saved setting (the binder) and leave the shared theme at 1.0, so they show no difference. A scratch probe (`fixprobe_huds_140_theme`: `huds_140` with the shared theme at 1.4 too, as the game runs; not committed) shows the Story HUD's "40%" and "9.8" filling their lenses before and at the lens's 0.36 after, with DEFY, ULT, S1 and the other labels unchanged (`fixprobe_abilities__before_above_after.png`).

### For the orchestrator

1. The HUD capture targets on `main` (`huds_140`, `huds_boss_140`, `hudf_140`) scale only the saved setting, not the shared theme, so they cannot show a double-scaling bug like this one. TG-HUDF's branch already scales both in its `HudfSetScale` (which is how its `hudf_140` showed the bug); `huds_140` and `huds_boss_140` (TG-HUDS's) may want `UIPalette.ApplyUiScale(1.4f)` too.
2. The decor's latent case above (a decorated button under a `UiScaleBinder` root) is recorded, not fixed.
