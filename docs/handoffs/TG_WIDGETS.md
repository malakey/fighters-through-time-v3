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
