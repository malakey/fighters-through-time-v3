# TG-THEME handoff: palette, roles, styles, motion, the transition, the capture tool

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.4). Look authority: `UI_SPEC_GLASS.md`
(section numbers below are its own). Branch `tg/theme`, worktree `D:\Projects\ftt-wt\tg-theme`, cut from `main` at
`196d821c`.

## Step 1 (2026-10-27)

Step 1 runs in parallel with TG-ART and cannot use its textures: every glass style is built in the 6.2 `StyleBoxFlat`
fallback form, so screens stay usable before the art lands. Step 2 wires TG-ART's nine-slices.

### What shipped

| File | Change |
|---|---|
| `tools/ui_theme/build_ftt_theme.py` | Rewritten around one palette table (`PALETTE`, plan ruling R11), the glass Label roles (`ROLES`: 2.3 values, 2.4 halo tiers, tracking, tnum, line spacing) and the glass styles in flat-fallback form. Writes the palette into `UIPalette.Roles.cs` as `GeneratorPalette`. |
| `resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.Roles.cs` | Regenerated (24 ext, 118 sub-resources, 733 items, 66 roles, 82 palette entries). Never hand-edited. |
| `scripts/UI/UIPalette.cs` | The glass constants of spec 1.6 (and the other literals the theme paints) with their exact values; every existing constant kept, the pulp-only ones marked "Legacy (pulp, retired by the glass closeout)"; the theme mirrors re-pointed at glass values; `TitleFontSize` 40 -> 36; new `TextFocus`, `ButtonLabel` and the halo constants. |
| `scripts/UI/Motion/PulpMotionRules.cs` | MOT-A numbers and pure curves (`TrackerValueAt`, `TrackerFadeAlphaAt`, `BloomAt`, `PanelRiseAt`, `LowPulseAt`, `SineInOut`, `CubicOut`). Deleted `ThunkValues`, `Stepped`, `ThunkSteps`, the wipe constants and `WipeOffsetCover` / `WipeOffsetReveal`, `TrackerSteps`, `TrackerDropSeconds`, `PanelPopSeconds`, `PanelPopFromScale` (no caller outside `Motion/` and the rewritten tests). Kept `CardLift` / `CardTiltDegrees`, `FrameSeconds`, `HitFlashMinIntervalSeconds` and `HitFlashLimiter` for the closeout. |
| `scripts/UI/Motion/PulpMotion.cs` | `PopIn` = the fade-rise (with the light-band hook `IPanelLightBand`), `PopOut` = the fade-sink, `TrackerDrop` = hold then a smooth drain (Reduced: a fade through an optional new `fadeAlpha` callback), new `HealRise`, new `Bloom`; `Thunk` deleted; `HitFlash` kept until the closeout. |
| `scripts/UI/Motion/FocusPose.cs` | A 14 px slide over 120 ms (Quad Out), no tilt, no scale; the pressed transform (+6 px from rest, 0.985 about the centre) on the press frame and a 120 ms release; the mechanism unchanged (an inner visual allowed, re-based after a container sort, exact identity on release, a snap under Reduced). |
| `scripts/UI/Motion/ScreenWipe.cs` | The 0.2 s bg0 dissolve (MOT-A row 6), the same under Reduced. Class, node name `ScreenWipe`, child `Slab` (now a `ColorRect`), API and events unchanged; additive `Opacity`. `GameManager.cs` untouched. |
| `scripts/Diagnostics/UiCapture/UiCaptureTargets.Theme.cs` | The five targets restyled for glass (names kept) and the new `theme_glass_components`. |
| `tools/ui_theme/compare_capture.py` (new), `tools/ui_theme/README.md` | The capture comparison helper and its documentation. |
| Tests | `UIThemeTests` 12 -> 14, `PulpMotionRulesTests` 4 -> 3, `PulpMotionTests` 10 -> 11; in place `UrPolishTests` (one TG-THEME method), `UiCaptureRunnerTests`, `ScreenSceneStyleTests`, `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation`. |
| Cross-edits | `scripts/UI/PausePanel.cs` with `tests/Pause/PausePanelTests.cs`; `tests/Unit/UrPolish2Tests.cs` (one TG-WIDGETS method); `scripts/UI/DialogueTheme.cs`. See below. |

Commit `981b8a09` (code); this handoff in the next commit.

### Test delta

`[TestCase]` attribute count 3691 -> **3693** (+2), the plan's declared delta (section 2.14):

- `UIThemeTests` 12 -> 14 (+2): added `EverySceneTextRoleCarriesItsHaloTier` and `TheGlassPaletteConstantsMatchTheSpecAndTheGenerator`; rewrote `TheFoldedKitGapsCarryTheirSpecSizesAndPlates` as `TheGlassPanelPresetsCarryTheirSpecPaddings`; updated `ThemeCarriesTheStyleEntriesScreensDependOn` (the glass entries: focus, `selected`, presets, badges, tabs, scroll bars, popup hover, tooltip, separators), `AuthoredColoursMatchTheUIPaletteConstants` (the glass colours, what each box paints, the focus box an overlay with the lit edge and the 14 px glow), `LabelTypeVariationsMatchTheUIPaletteTypeScale` (logic unchanged; `TitleFontSize` 36), `EveryLabelRolePinsItsFaceAndWeight` (now also the tracking as `spacing_glyph`), `EveryPulpVariationExistsOnItsBaseType` (the new variations) and `TheThemesTextPairsClearTheContrastFloor` (the 1.5 pairs composited over white behind the build fills); the other five unchanged.
- `PulpMotionRulesTests` 4 -> 3 (-1): deleted `ANumberThunkIsThreeEqualStepsThatLandExactlyOnTheTarget` (`Thunk` is deleted); rewrote `SteppedProgressKeepsItsEndpointsAndQuantisesTheMiddle` as `TheTrackerHoldsThenDrainsSmoothlyAndFadesUnderReducedEffects`; rewrote `TheTimingsAreTheSpecsAndNothingFlashesFasterThanOnceASecond` with the MOT-A values and curves (the once-a-second intent kept); kept `TheHitFlashLimiterAllowsOneFlashPerSecondPerBar`.
- `PulpMotionTests` 10 -> 11 (+1): updated both focus-pose cases (14 px, 120 ms, no tilt or scale, the pressed transform and its release); kept `AContainerSortDoesNotLoseTheFocusPose`, the paused and released wipe cases and the hit-flash case; rewrote the panel pop case as `APanelFadesInRisingTwentyFourPixelsAndOnlyFadesUnderReducedEffects` (Reduced: alpha only, no band); rewrote the two wipe cases as `TheDissolveCoversAndRevealsAndRaisesCoveredWhileTheScreenIsFullyCovered` and `UnderReducedEffectsTheDissolveIsTheSameStableFade`; deleted `ANumberThunkStepsOverThreeFramesAndIsInstantUnderReducedEffects`; added `APanelFadesOutSinkingTwelvePixels` and `TheReadyBloomRisesOnceAndSettlesWithNoLoop`. The file also holds the non-suite probe `PanelBandProbe`.
- In place, no count change: `UrPolishTests.TheDialoguePanelGivesTheSpeakerItsPaddingAndTheSliderTrackEndsUnderTheGrabber` (the dialogue slab pads 22 / 26 / 22, spec 3.1, where the pulp plate needed 30 on top; the slider track is now a leaning `StyleBoxFlat` and must still end its own lean inside the track, under the grabber); `UiCaptureRunnerTests` (the registry list gains `theme_glass_components`); `ScreenSceneStyleTests` (drops the `temporal_glass_` ban); `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation` (a flat slab or the glass nine-slice, translucent, in `DialogueTheme.GlassBackground`). `UrPolishTests.AScaleAppliedWhileTheCaptureRunnerHoldsTheTheme...` needed no change.
- 3691 + 2 - 1 + 1 = 3693.

### Runs

- UI filter (plan 3.2), in this worktree under the lock: **705 / 705** at `981b8a09` (703 at `196d821c`; +2 exactly).
- Extra suites that reach the changed palette and motion defaults (`ActThreeChaining`, `CollapseTremor`, `DefySeal`, `HudAbilityIndicator`, `ResonanceHold`, `StoryHealingLoop`, `Level00Flow`, `ThoroughRunCollection`, `DamageNumber`, `TestArena`, `ScriptTypedEmptyArray`, `SceneAssignedResourcePin`): **74 / 74**.
- `dotnet build`: 0 errors, the two known warnings (`CS8632`, `KLSG_ECS004`). Headless project-load check: exit 0, no error. No `.import` churn.
- The first UI-filter run had three failures, all fixed: `PulpBarDecorTests.TheDecorOverlaysTheBarWithoutTouchingItsFill` (my first cut re-skinned the five HUD bars; reverted, deviation 1), `PulpMotionTests.APanelFadesOutSinkingTwelvePixels` (my threshold assumed nine frames of tween progress in nine awaits; it is eight, 8.3 px) and `UrPolish2Tests.AFocusedButtonInAHorizontalRowClearsItsNeighboursAtBothUiScales` (the cross-edit below).

### Fail evidence (every new or rewritten case seen failing)

Two temporary mutation sets on top of `981b8a09`, each a filtered GdUnit run under the lock, reverted with
`git checkout -- .` (logs `D:\Projects\ftt-scratch\tg-theme\mutA.log`, `mutB.log`):

- **Set A** (generator, palette, dialogue): halo S shadow outline 4 -> 3; `GlassPanelStats` bottom padding 18 -> 16; the generator's `Edge` alpha .55 -> .50; `UIPalette.GlassFillTop` alpha .92 -> .80 (the mock's fill without its blur); the chevron focus box drawing its centre; the chevrons' `selected` box removed; `GlassTab` without a focus box; tracking floored instead of rounded; the slider track's right expand -6 -> -2; `UIPalette.TitleFontSize` 36 -> 40; `DialogueTheme.GlassBackground` back to `PlateFill`. Result: 11 failed, 9 passed. All nine targeted `UIThemeTests` cases failed with their own messages ("LevelTitle needs halo S but has ... shadow 3", "GlassPanelStats pads 20 / 26-26 / 16 but 3.1 says 20 / 26 / 18", "Edge: the generator says ... 0.5, UIPalette ... 0.55", "Ink2 on the panel (sheen) 4.16:1 under 4.5", "PulpButton/styles/focus paints an opaque fill: it must be an overlay", "PulpButton/styles/selected" missing, "GlassTab/styles/focus missing", "TitleLabel tracks 1 px but 0.05 em at 36 px is 2", "TitleLabel is 36 px but UIPalette says 40"), plus the `UrPolishTests` slider case ("the track's slanted right cap must not stick out past the grabber") and the `DialoguePresentationTests` glass case; the five kept `UIThemeTests` cases and the other `UrPolishTests` cases stayed green.
- **Set B** (motion, dissolve, capture registry, scene scan, cross-edit): a linear tracker drain; `FocusTranslateX` 14 -> 16; a panel rising from above; no sink; a looping bloom; a 1 ms dissolve; a press offset equal to the focus slide; the `theme_glass_components` target renamed; a planted `theme_override_font_sizes/font_size` in `Credits.tscn`; `PausePanel` back to `StyleBoxTexture` only. Result: 12 failed, 7 passed: both rewritten `PulpMotionRulesTests` cases ("the drain should start gently, was 99"), both focus-pose cases, the fade-rise, the fade-sink ("the panel sank 0 px"), the bloom, both dissolve cases ("a dissolve, not a cut: 1"), the registry case, `ScreenSceneStyleTests` ("scenes/ui/Credits.tscn:12") and `PausePanelTests`; the kept container-sort, hit-flash, paused-wipe, released-wipe and other runner cases stayed green.
- The `UrPolish2Tests` cross-edit was seen failing without its fix in the first run (a 5.6 px overlap at 1.0 and at 1.4).

### Cross-edits (standing decision 10)

- `scripts/UI/PausePanel.cs` (TG-PAUSE's in Phase B; TG-WIDGETS also cross-edits it in A3): `_Ready` duplicated the theme's panel style only when it was a `StyleBoxTexture`, so on the step-1 flat slab the pause panel lost its 30 / 20 / 28 padding. It now takes any `StyleBox`; `tests/Pause/PausePanelTests.cs` reads the style as a `StyleBox`. The padding constants (`Border` 3) are untouched: TG-PAUSE re-values them, and `GlassPanelPause` carries the 3.1 padding for them.
- `tests/Unit/UrPolish2Tests.cs`, `AFocusedButtonInAHorizontalRowClearsItsNeighboursAtBothUiScales` (TG-WIDGETS' method): it models the focus pose from each button's measured rect, but the opened confirm modal focuses Cancel, so Cancel's real slide was counted twice. The pulp pose's 1.04 scale term had hidden that (the case passed by about 1.6 px); with MOT-A's slide alone it reported a 5.6 px overlap. The case now releases focus and settles 12 frames before measuring, so the model starts from the rest slot, as intended. Nothing else changed.
- `scripts/UI/DialogueTheme.cs`: `GlassBackground` reads `UIPalette.PanelBackground` (the glass slab's flat fill) instead of the pulp `PlateFill`.

### Tokens (the generator's palette table, mirrored by `UIPalette`)

| Palette entry (= UIPalette constant) | Value | Role |
|---|---|---|
| `Backdrop` | #05071A | bg0: the dark field under art, the screen dissolve |
| `Ink` | #F3F0E6 | primary text |
| `Ink2` | #C0BAA6 | secondary text; the normal chevron label |
| `Ink3` | #8A8374 | disabled text and dimmed non-text |
| `InkInv` | #1A1204 | text on a gold fill |
| `Accent` | #F3C95B | gold accent |
| `Accent2` | #FFE39A | light gold |
| `Focus` | #FFD45C | the focused edge and marker |
| `Edge` | #F3C95B @0.55 | the standard hairline |
| `EdgeHi` | #FFD45C | the lit hairline |
| `EdgeDim` | #F3C95B @0.25 | the quiet hairline |
| `Res` | #F3C95B | Resonance gold |
| `Res2` | #FFF0B0 | Resonance bright |
| `Hp` | #8CF0B8 | HP fill, moving end |
| `Hp2` | #4FCF8A | HP fill, anchored end |
| `HpLag` | #FF5A5A | damage tracker (drawn at 0.9) |
| `Echo` | #FFF7DA | Rally echo band |
| `Meter` | #F3C95B | Ultimate meter start |
| `Meter2` | #FFB347 | Ultimate meter end |
| `Shield` | #DFE8FF | block pip |
| `Danger` | #FF5A5A | danger, low HP |
| `Boss` | #FF5A5A | boss red |
| `Boss2` | #A82A2A | boss fill start, low-HP fill start |
| `StatusDamage` | #FF7A5C | damage status glyph and ring |
| `StatusControl` | #8FC7FF | control status glyph and ring |
| `PanelShadow` | #00020C @0.6 | the soft drop shadow |
| `GlassFillTop` | #0F1834 @0.92 | panel fill, 160 degree start (build) |
| `GlassFillBottom` | #060A1A @0.95 | panel fill, 160 degree end (build) |
| `GlassFillFlat` | #0F1834 @0.93 | the 6.2 single-colour fallback of the panel fill |
| `ButtonFill` | #080D20 @0.9 | chevron and field fill (normal, selected) |
| `ButtonFillFocus` | #081026 @0.92 | chevron fill under the focus and pressed washes |
| `ButtonFillPressed` | #5A5138 @0.946 | the pressed wash (mean .33) composited over ButtonFillFocus |
| `ButtonFillDisabled` | #080D20 @0.5 | disabled chevron fill |
| `TileFillTop` | #1A264A @0.9 | select tile fill, 160 degree start |
| `TileFillBottom` | #060A1A @0.94 | select tile fill, 160 degree end |
| `LensFill` | #060A1A @0.92 | lens radial outer stop |
| `BadgeFill` | #080C1E @0.86 | badge radial outer stop |
| `DialFill` | #050918 @0.9 | Integrity dial radial outer stop |
| `PortraitFill` | #060A1A @0.95 | portrait radial outer stop |
| `TubeTrackTop` | #02050E @0.95 | glass tube inner track, top |
| `TubeTrackBottom` | #0A1024 @0.82 | glass tube inner track, bottom |
| `BossTrackTop` | #05060F | boss tube track, top (opaque) |
| `BossTrackBottom` | #120A18 | boss tube track, bottom (opaque) |
| `SweepShade` | #02050E @0.76 | cooldown sweep |
| `WedgeRemainder` | #02050E @0.55 | Integrity wedge remainder |
| `ToggleTrackFill` | #060A1A @0.9 | toggle track fill (off) |
| `TabPanelFill` | #060A1A @0.6 | the TabContainer's quiet inner panel (agent choice) |
| `SliderTrack` | #FFFFFF @0.12 | slider track |
| `ScrollTrack` | #FFFFFF @0.06 | scroll bar track |
| `TickLine` | #FFFFFF @0.13 | tube decile ticks |
| `RingTrack` | #F3C95B @0.16 | readiness and status ring remainder; tube glow |
| `FocusGlow` | #F3C95B @0.55 | the 14 px focus glow |
| `FocusWash` | #F3C95B @0.22 | a popup row's focus wash (flat mean of the button wash) |
| `TagWash` | #F3C95B @0.21 | the panel tag's wash (flat mean of .34 to .08) |
| `TabFill` | #F3C95B @0.08 | an unselected tab |
| `TabHover` | #F3C95B @0.16 | a hovered tab (agent choice) |
| `ReadyBadgeFill` | #F3C95B @0.14 | the READY badge fill |
| `ReadyBadgeGlow` | #F3C95B @0.3 | the READY badge glow |
| `ReadyBadgeIdleEdge` | #F3C95B @0.25 | the idle badge border |
| `ReadyBadgeIdleFill` | #000000 @0.25 | the idle badge fill |
| `Selection` | #F3C95B @0.4 | text selection in a field |
| `PipGlow` | #8FD8FF @0.6 | block pip glow |
| `DefyReadyGlow` | #FFE87A @0.75 | Defy-ready glow |
| `BossEdge` | #FF4D62 | boss tube edge (PAL-A's boss red, hard-coded by the mock) |
| `BossGlow` | #FF4D62 @0.3 | boss tube glow |
| `BossTracker` | #FFFFFF @0.55 | boss damage tracker |
| `TabSelectedInk` | #04141A | selected tab text; the select lock glyph |
| `ScrimFill` | #03050E | what ScrimPanel paints; every user sets SelfModulate alpha 0.70 |
| `PauseDim` | #03050E @0.7 | the build pause dim (ScrimFill at 0.70) |
| `GameOverDim` | #03050E @0.82 | the Smothered dim |
| `ScrimMenu` | #040714 @0.96 | the menu scrim's strongest stop (90 degree: .96, .84 at 26 %, .35 at 48 %, 0 at 66 %) |
| `SelectVignette` | #030510 @0.55 | the select vignette's centre stop |
| `TopShade` | #06040C @0.8 | the Fighter top shade's top stop (.80, .64 at 42 %, .30 at 78 %, 0) |
| `TitleTop` | #FFFFFF | menu title gradient, top |
| `TitleMid` | #DFE8FF | menu title gradient at 42 %; the flat fallback |
| `TitleBottom` | #8D9BC6 | menu title gradient, bottom |
| `TextFocus` | #FFFFFF | the focused, hovered and pressed label |
| `HaloOutlineS` | #000000 @0.85 | halo S rim |
| `HaloShadowS` | #000000 @0.6 | halo S shadow |
| `HaloOutlineL` | #000000 @0.7 | halo L rim |
| `HaloShadowL` | #000000 @0.5 | halo L shadow |
| `LabelDark` | #0C0A10 | legacy pulp: dark text on gold or paper |

Theme mirrors re-pointed: `PanelBackground` = `GlassFillFlat`, `PanelBorder` = `Edge`, `ButtonNormal` = `ButtonFill`,
`ButtonHover` = `ButtonFillFocus`, `ButtonPressed` = `ButtonFillPressed`, `ButtonDisabled` = `ButtonFillDisabled`,
`FocusOutline` = `Focus`, `FocusFill` = `ButtonFillFocus`, `TextOnAccent` = `TabSelectedInk` (text on a gold fill: the
selected tab), `TextPrimary` = `Ink`, `TextAccent` = `Accent`, `TextDisabled` = `Ink3` (was the pulp `DisabledLabel`),
`TrackBackground` = `TubeTrackTop` (was the pulp `Track`). New: `TextFocus` (#FFFFFF), `ButtonLabel` (`Ink2`), and the
halo constants `HaloSOutline` 2, `HaloSShadowOffsetY` 1, `HaloSShadowOutline` 4, `HaloLOutline` 3, `HaloLShadowOffsetY`
3, `HaloLShadowOutline` 8, `HaloS6ShadowOutline` 6. Unchanged: `Cyan`, `CyanDim`, `UnboundCold*`, the grammar equalities,
`P1` / `P2` / `OwnerOnDark`. Legacy (marked; the closeout retires them): `PlateFill`, `PlateRaised`, `Paper`,
`LabelDark`, `PaperInk`, `PaperInk2`, `PaperInk3`, `Track`, `SlotFrame`, `PromptCream`, `DisabledFill`, `DisabledLabel`.

### Label roles (sizes rounded to whole px; spec 2.3)

| Role | Spec | Face, weight | px | Tracking (em -> px) | Colour | Halo | tnum | line_spacing |
|---|---|---|---:|---|---|---|---|---:|
| `SmallLabel` | micro copy | Outfit 500 | 18 | 0.00 -> 0 | Ink2 | plate |  |  |
| `HeadingLabel` | section heading inside a slab (9.1) | Cinzel 700 | 26 | 0.13 -> 3 | Accent | plate |  |  |
| `EmphasisLabel` | the emphasis step of the scale | Outfit 600 | 30 | 0.00 -> 0 | Ink | plate |  |  |
| `TitleLabel` | panel title inside a slab (9.1) | Cinzel 900 | 36 | 0.05 -> 2 | Ink | plate |  |  |
| `CoverTitle` | T01 (flat fallback; TitleGradient multiplies a white face) | Cinzel 900 | 112 | 0.05 -> 6 | TitleMid | L |  |  |
| `CoverBanner` | T02 | Cinzel 700 | 38 | 0.60 -> 23 | Ink2 | plate |  |  |
| `BannerHeading` | T03 | Cinzel 900 | 56 | 0.05 -> 3 | Ink | L |  |  |
| `PauseTitle` | T04 | Cinzel 900 | 78 | 0.14 -> 11 | TextFocus | L |  |  |
| `LevelTitle` | T05 | Cinzel 500 | 26 | 0.12 -> 3 | Ink | S |  |  |
| `ObjectiveLine` | T06 | Outfit 400 | 22 | 0.00 -> 0 | Res2 | S |  |  |
| `PanelTag` | T07 | Outfit 500 | 18 | 0.16 -> 3 | Ink | plate |  |  |
| `HeroName` | T08 | Cinzel 700 | 22 | 0.13 -> 3 | Ink | plate |  |  |
| `HeroNameScene` | T09 | Cinzel 700 | 28 | 0.13 -> 4 | Ink | S |  |  |
| `PromptKey` | T12 | Outfit 700 | 19 | 0.00 -> 0 | TextFocus | plate |  |  |
| `PromptCaption` | T13 | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | S |  |  |
| `CaptionLabel` | T14 on glass, T48 | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | plate |  |  |
| `SceneCaption` | T14 on the scene | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | S |  |  |
| `VersionCaption` | T15 (17 raised to 18) | Outfit 500 | 18 | 0.16 -> 3 | Ink3 | S |  |  |
| `StatusLine` | T16 | Outfit 400 | 18 | 0.06 -> 1 | Ink2 | plate |  |  |
| `HudNumeral` | T17, the Fighter numeral (31.68) | Outfit 600 | 32 | 0.00 -> 0 | TextFocus | S | yes |  |
| `HudNumeralSmall` | T17, the Story numeral (21.6); the Fighter /max | Outfit 600 | 22 | 0.00 -> 0 | TextFocus | S | yes |  |
| `HudNumeralMax` | T17 / T32 /max (AC3; the widget dims it to .78 / .75) | Outfit 400 | 18 | 0.00 -> 0 | TextFocus | S | yes |  |
| `SlotKeyTag` | T18 in Story (a plain label) | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | plate |  |  |
| `SlotKeyTagScene` | T18 over the scene (Fighter) | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | S |  |  |
| `SlotNumber` | T19 for the 54 px lens (0.36 x lens) | Outfit 700 | 19 | 0.00 -> 0 | TextFocus | S | yes |  |
| `StatusName` | T20 | Outfit 500 | 18 | 0.16 -> 3 | Ink | S |  |  |
| `StatusTime` | T21 (20.7) | Outfit 600 | 21 | 0.00 -> 0 | Ink2 | S |  |  |
| `DefyLabel` | T22 (16.2 raised to 18) | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | plate |  |  |
| `RewindCount` | T23 (31.5) | Outfit 600 | 32 | 0.00 -> 0 | Ink | plate | yes |  |
| `StateLabel` | T25 (19.8) | Outfit 600 | 20 | 0.10 -> 2 | Ink | plate |  |  |
| `DustTotal` | T26 (37.5) | Outfit 600 | 38 | 0.02 -> 1 | Ink | plate | yes |  |
| `DustFloat` | T27 (25.5) | Outfit 600 | 26 | 0.02 -> 1 | Res2 | plate | yes |  |
| `IntegrityPercent` | T28 (23.1) | Outfit 600 | 23 | 0.00 -> 0 | Ink | S | yes |  |
| `IntegrityCaption` | T29 | Outfit 500 | 18 | 0.16 -> 3 | Ink | S6 |  | -2 |
| `BossName` | T30 | Cinzel 900 | 38 | 0.08 -> 3 | TextFocus | L |  |  |
| `BossPhaseTag` | T31 (a plain label) | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | S |  |  |
| `BossHpValue` | T32 | Outfit 400 | 24 | 0.00 -> 0 | TextFocus | S | yes |  |
| `NoteLabel` | T33 | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | S |  | 2 |
| `TimerDigits` | T34 | Outfit 600 | 60 | 0.04 -> 2 | Ink | plate | yes |  |
| `CardName` | T35 the tile name (17 raised to 18) | Outfit 600 | 18 | 0.16 -> 3 | Ink2 | plate |  |  |
| `CardDates` | T36 retired (pulp values kept until the closeout) | Outfit 500 | 18 | 0.12 -> 2 | Accent | plate |  |  |
| `CardStamp` | T37 the READY badge text | Outfit 600 | 26 | 0.20 -> 5 | Accent | plate |  |  |
| `CardQuestion` | T38 | Cinzel 700 | 40 | 0.00 -> 0 | Ink2 | plate |  |  |
| `BubbleText` | T39 retired (pulp values kept until the closeout) | Outfit 400 | 46 | 0.04 -> 2 | LabelDark | plate |  |  |
| `SideCardName` | T40 | Cinzel 900 | 48 | 0.05 -> 2 | TextFocus | L |  | -13 |
| `StatLabel` | T41 | Outfit 500 | 18 | 0.16 -> 3 | Ink2 | plate |  |  |
| `StatValue` | T42 | Outfit 600 | 24 | 0.00 -> 0 | Ink | plate | yes |  |
| `StarBadgeText` | T43 retired (pulp values kept until the closeout) | Outfit 400 | 44 | 0.00 -> 0 | LabelDark | plate |  |  |
| `HintLine` | T44 | Outfit 400 | 22 | 0.00 -> 0 | Ink2 | S |  |  |
| `HintLinePlate` | T44 on the glass tag plate (the credits skip hint) | Outfit 400 | 22 | 0.00 -> 0 | Ink2 | plate |  |  |
| `StickerLine1` | T45 the Continue card's hero name | Cinzel 700 | 24 | 0.13 -> 3 | Ink | plate |  |  |
| `StickerLine2` | T46 the Continue card's place line | Outfit 400 | 22 | 0.00 -> 0 | Ink | plate |  |  |
| `StickerLine3` | T47 the Continue card's meta line | Outfit 400 | 19 | 0.00 -> 0 | Ink2 | plate |  |  |
| `DialogueBody` | T49 | Outfit 400 | 33 | 0.00 -> 0 | Ink | plate |  | 1 |
| `SpeakerName` | T50 | Cinzel 900 | 30 | 0.10 -> 3 | Accent | plate |  |  |
| `MenuTagline` | T51 (the loading_rift line; the menu shows no tagline, R3) | Outfit 500 | 21 | 0.42 -> 9 | Accent | plate |  |  |
| `SettingsRowLabel` | T52 | Outfit 500 | 22 | 0.10 -> 2 | Ink | plate |  |  |
| `SettingsRowValue` | T53 | Outfit 600 | 26 | 0.00 -> 0 | Ink | plate | yes |  |
| `ToggleText` | T54 (17 raised to 18; ON draws white) | Outfit 700 | 18 | 0.12 -> 2 | Ink2 | plate |  |  |
| `TabLabel` | T55 | Outfit 600 | 24 | 0.13 -> 3 | Ink2 | plate |  |  |
| `AbilityName` | T56 (14 raised to 18, no tracking: D9) | Outfit 500 | 18 | 0.00 -> 0 | Ink2 | plate |  | -2 |
| `RandomWord` | T57 | Outfit 600 | 20 | 0.30 -> 6 | Ink2 | plate |  |  |
| `SelectCountdown` | T59 | Outfit 600 | 26 | 0.20 -> 5 | Accent | S |  |  |
| `UltimateReadyLabel` | T60 | Outfit 700 | 18 | 0.16 -> 3 | Res2 | S |  |  |
| `BodyLabel` | 9.1 body copy | Outfit 400 | 26 | 0.00 -> 0 | Ink | plate |  | 2 |
| `BodySmallLabel` | 9.1 small copy | Outfit 400 | 22 | 0.00 -> 0 | Ink | plate |  |  |

17 new roles: `VersionCaption`, `HudNumeralMax`, `SlotKeyTagScene`, `DustTotal`, `IntegrityPercent`,
`IntegrityCaption`, `MenuTagline`, `SettingsRowLabel`, `SettingsRowValue`, `ToggleText`, `TabLabel`, `AbilityName`,
`RandomWord`, `SelectCountdown`, `UltimateReadyLabel`, `BodyLabel`, `BodySmallLabel`. Retired, values unchanged until
the closeout: `CardDates`, `BubbleText`, `StarBadgeText`. Halo values (2.4): S = a 2 px rim #000 @0.85 and a shadow at
(0, 1) with a 4 px outline #000 @0.60 (T29: a 6 px outline); L = a 3 px rim #000 @0.70 and a shadow at (0, 3) with an
8 px outline #000 @0.50. Line spacing is set only on roles whose text wraps (a Label is never shorter than its font, so
it is moot for one line), against the faces' natural line heights from their hhea / OS/2 tables: Outfit 1.26 em,
Cinzel 1.348 em. `DialogueBodyRich` (RichTextLabel): Outfit 400, 33 px, `line_separation` 1.

### Variations

| Variation (base) | Step 1 |
|---|---|
| `PulpButton` (Button), T10 menu row 68 | glass chevron (flat): content margins 58 / 11 / 54 / 11, Cinzel 700 34 px, tracking 4; label `Ink2`, white on hover, focus and pressed, `Ink3` disabled |
| `TemporalGlassButton` (Button), the pause and list row 64 | the same, margins 58 / 9 / 54 / 9 |
| `PulpButtonCompact` (Button), T11 58 | the same, margins 52 / 13 / 40 / 13, Cinzel 700 24 px, tracking 3 |
| default `Button`, `OptionButton` (+ `arrow_margin` 14), new `GlassField` (Button) | the glass field (3.3, P8): chamfer 8, 2 px `EdgeDim`, `ButtonFill`; margins 16 / 8 / 14 / 8, Outfit 600 22 px; `Ink` text, white when lit, `Ink3` disabled |
| `LineEdit` | the glass field, margins 16 / 8 / 16 / 8, 22 px; caret `Accent`, selection `Selection`, placeholder `Ink3` |
| new `GlassTab`, `GlassTabSelected` (Button); the `TabContainer` styles | G14: `TabFill` (selected `Accent` with `TabSelectedInk` text), a 16 px bevel at the top right, content 26 / 9 / 26 / 9, Outfit 600 24 px tracking 3; tab focus = the 14 px glow |
| `Panel`, `PanelContainer`, `InkPanel`, `HudPlatePanel` | glass slab G1 (flat): `GlassFillFlat`, 2 px `Edge`, a 45 degree bevel of 18 at TL and BR, shadow (0, 14) 22 `PanelShadow`, padding 22 / 28 / 22 |
| `DialogueGlassPanel` | the slab, padding 22 / 26 / 22 |
| new `GlassPanelPause`, `GlassPanelVitals`, `GlassPanelAbilities`, `GlassPanelTimeTools`, `GlassPanelDust`, `GlassPanelTimer`, `GlassPanelStats`, `GlassPanelContinue` | the slab with the 3.1 paddings 30/20/28, 20/26/20, 22/30/14, 14/26/14, 14/24/14, 12/32/12, 20/26/18, 16/20/16 |
| `PopupPanel`, `PopupMenu` | the slab without its shadow, padding 8; menu rows 44 px (Outfit 500 22, `v_separation` 16, item padding 16); hover = `FocusWash` with a 2 px `Focus` bar |
| `PanelTagPlate`, `HintLinePlate` (Label `normal`) | glass tag G2 (flat): `TagWash` with a 2 px `EdgeHi` bar on the left, an 8 px bevel at the bottom right, content 14 / 3 / 14 / 3 |
| `ScrimPanel` | paints `ScrimFill` (3, 5, 14) **opaque**; every user multiplies it by SelfModulate alpha 0.70 (`PulpScreenKit.ScrimAlpha` 0.7 and the `self_modulate` of the three pause scenes already do), which gives the build dim rgba(3, 5, 14, .70) of 1.4. This is the self-alpha contract `MakeScrim` and the scenes follow; no code had to change. (`ScrimAlpha`'s comment, "0.85 x 0.7 reads as 0.58", is now stale: TG-WIDGETS' file.) |
| new `ReadyBadge`, `ReadyBadgeIdle` (PanelContainer) | G12 as 6.2 builds it: border 2 `Accent`, `ReadyBadgeFill`, glow 18 `ReadyBadgeGlow`; idle border `ReadyBadgeIdleEdge`, fill `ReadyBadgeIdleFill`, no glow; content 22 / 8 / 22 / 8 |
| `HSlider` / `VSlider` | a 12 px `SliderTrack` (content 6 / 6), skew 0.5, expand -3 left and -6 right (the UR-POLISH rule: the slanted cap ends under the grabber); fill `Accent` (`Focus` highlighted); grabber icons still the pulp `slider_grabber` (step 2: `glass_slider_grabber.png`); the `VSlider` without the skew |
| `VScrollBar` / `HScrollBar` | an 8 px `ScrollTrack` with radius 4; grabber `Edge` (highlight and pressed `Focus`), at least 48 long |
| `SpinBox` | up and down backgrounds empty, hovered `RingTrack`, pressed `ButtonFillPressed` (the pulp generator wrote `up_hover_background` and the like, which are not Godot's names) |
| `CheckBox` | focus = a 2 px `Focus` ring; colours as the field; the placeholder icons unchanged |
| `TooltipPanel` | a small slab: `GlassFillFlat`, 2 px `Edge`, chamfer 8, content 12 / 6 / 12 / 6 |
| `HSeparator`, `VSeparator`, the `PopupMenu` separator | a 1 px `EdgeDim` line |
| default `ProgressBar` | the tube's dark track (`TubeTrackTop`, 2 px `Edge`), fill `Accent` |
| `PulpBar`, `PulpBarH44`, `PulpBarH16`, `PulpBarH14`, `PulpBossBar` | **unchanged pulp tracks and skewed fills** (deviation 1) |
| `PaperPanel`, `BannerPanel`, `CoverBannerPanel`, `BossNameplate`, `BossPhaseTab`, `SlotPlate`, `SlotPlateReady`, `KeyTagPlate`, `CardPlate`, `CardPlatePick`, `BubblePlate`, `StampPlate` | unchanged pulp plates (plan 2.2: the closeout retires them) |

Each chevron variation also carries `styles/selected` (2 px `Edge` over `ButtonFill`). Godot never draws it by itself:
the button decor can swap it in for the current value of a list (3.2 selected), as it swaps the pulp paper plate today.

### Motion API (MOT-A, section 5)

- `PulpMotionRules`: focus `FocusInSeconds` / `FocusOutSeconds` 0.12, `FocusTranslateX` 14, `FocusTiltDegrees` 0,
  `FocusScale` 1 (named zero and one, because `PulpScreenKit.FocusRowGap` and two `UrPolish2Tests` methods read them),
  `PressTranslate` 6, `PressScale` 0.985, `PressReleaseSeconds` 0.12; panels `PanelInSeconds` 0.22, `PanelRisePixels` 24,
  `PanelOutSeconds` 0.16, `PanelSinkPixels` 12, Reduced `PanelFadeSeconds` 0.15 and `PanelFadeOutSeconds` 0.12, the band
  `PanelBandSeconds` 0.6, `PanelBandWidth` 40, `PanelBandAlpha` 0.10; `PauseDimSeconds` 0.16; tracker
  `TrackerHoldSeconds` 0.5, `TrackerDrainSeconds` 0.6, `TrackerFadeSeconds` 0.2, `HealRiseSeconds` 0.2; bloom
  `BloomSeconds` 0.6, `BloomPeakSeconds` 0.3, `BloomPeakModulate` 1.35, `BloomPeakGlow` 1.5; `LowPulseSeconds` 2; dust
  float `DustFloatInSeconds` 0.22, `DustFloatRisePixels` 12, `DustFloatOutSeconds` 0.3, `DustFloatReducedSeconds` 0.15;
  `CrossFadeSeconds` 0.12, `PhaseLabelSeconds` 0.22, `AnchorSplitSeconds` 0.6 and `AnchorSplitReducedSeconds` 0.15,
  `ListScrollSeconds` 0.12; `DissolveSeconds` 0.2. Pure curves: `TrackerValueAt(elapsed, from, to)`,
  `TrackerFadeAlphaAt(elapsed)`, `BloomAt(elapsed)` -> (modulate, glow), `PanelRiseAt(progress)`, `LowPulseAt(clock)`,
  `SineInOut`, `CubicOut`.
- `PulpMotion`: `Reduced`, `NewTween(owner)`; `PopIn(panel)` (the fade-rise; it plays `IPanelLightBand.PlayLightBand` on
  every direct child that implements it, never under Reduced); `PopOut(panel)` (the fade-sink; ends invisible, back on its
  slot); `TrackerDrop(host, from, to, apply, fadeAlpha = null)`; `HealRise(host, from, to, apply)` (null and an instant
  value under Reduced); `Bloom(target, glow = null)` (null and `glow(1)` under Reduced); `HitFlash` (legacy). A rise or a
  sink moves the panel's `Position` and survives a container sort: a position it did not write is the new slot, and it
  always ends on the exact slot.
- `IPanelLightBand` (new, `FTT.UI.Motion`): `void PlayLightBand(float seconds)`. TG-WIDGETS' `GlassSheen` is meant to
  implement it.
- `FocusPose`: new `PressOffset` and `PressScale` (defaults from the rules); `FocusOffset`, `TiltDegrees`, `PoseScale`,
  `Attach`, `Apply`, `PoseActive` and `Target` unchanged.

### The transition hook

`ScreenWipe` keeps `EnsureFor`, `BeginCover`, `BeginReveal`, `PlayThrough`, `Release`, `IsCovered`, `IsActive`,
`CoverSeconds` (now 0.2 s), `Covered`, `Revealed` and `WipeLayer` 110; it fades its `Slab` (a `ColorRect` in
`UIPalette.Backdrop`) 0 -> 1 and 1 -> 0 with Sine InOut, the same under Reduced Temporal Effects. `GameManager.LoadScene`
drives it unchanged. Its comment "the MOT-B diagonal wipe" (`scripts/Core/GameManager.cs`, read-only here) is stale: for
the closeout.

### Captures and comparison

Into `D:\Projects\ftt-scratch\tg-captures\theme\` (windowed console Godot, under the lock): `theme_labels_small`,
`theme_labels_large`, `theme_controls`, `theme_palette`, `theme_glass_components`, `main_menu_root`, and, as a smoke look at
the screens under the step-1 theme with the pulp widgets still on top, `pause_story`, `menu_cover`, `settings_audio`,
`settings_controls`, `confirm_modal`, `select_cards`, `huds_calm`, `hudf_versus`, `results_level`, `dialogue_box` and
`widgets_buttons`. Comparisons (`compare_capture.py`, beside the captures): `theme_glass_components` against the
`menu_button_column` crop, and `pause_story` against `rec_03` and the `pause_panel` crop (side by side, overlay, diff).

What they show: the chevrons (a square left end, the tip at mid-height), the lit focus edge and its glow, the
focused-and-pressed row reading as pressed, the chamfered slabs with the gold edge and the drop shadow, the tag, the
badges, tabs, the field, the slider (its cap hidden under the grabber at 100 %), the scroll bar and the halos all render;
every sampled screen still renders, with the glass slabs and chevrons under the pulp markers, badges, tags, toggles,
slots and dials. Against the crops: the mock's focused row carries a gold wash (.34 -> .07) that the step-1 overlay does
not (deviation 2); the mock's fills look lighter and warmer because the browser blurs the art behind them (1.4); the
chevron tip runs h / 2 (34 px at 68) instead of 30 (deviation 4); the markers, tags, sheen and streaks are TG-WIDGETS'.
Not seen: a pad, motion in flight (the focus slide, fade-rise, bloom and dissolve are pinned by tests only), Reduced
Temporal Effects, a live match or level, UI scale 1.4 (no theme target scales).

### Deviations and agent choices

1. **The five HUD bar variations keep their pulp tracks and skewed fills.** The plan's Phase A column (2.2) makes their
   `background` a `StyleBoxEmpty` and their `fill` a `StyleBoxFlat` with `draw_center = false`. That only works once the
   glass tube decor draws the bar: the pulp `PulpBarDecor` (TG-WIDGETS' until its step 2) draws its layers over the bar's
   own fill and reads the fill's skew, and `PulpBarDecorTests` pins the pulp track (my first cut failed it). Flipping them
   in either TG-THEME step would blank every HUD bar until A4. **For the orchestrator to decide** (below).
2. **Hover and the focus wash in the flat form.** The focus box is an overlay (plan 0.4 P5): the 2 px `Focus` edge and the
   14 px `FocusGlow`, no fill. A `StyleBoxFlat` shadow fills its own interior when the box draws its centre, so a glowing
   overlay cannot also carry the translucent focus wash without tinting the whole row: step 1 has no focus wash, and a
   focused row is the normal fill plus the lit edge and the glow. `hover` is the lit edge over `ButtonFillFocus` with no
   glow, so focused and focused-and-hovered look the same and hovered alone lacks only the glow (AC8 approximately).
   Step 2's overlay texture can bake the wash (an image glow has no interior). For exact AC8 parity the decor could take
   focus on mouse-enter (`FocusPose` already treats hover as focus).
3. **The pressed fill** is one colour, the mean wash (.33) composited over `ButtonFillFocus` (`ButtonFillPressed`, #5A5138
   @0.946); its edge is the underlying state's, so a focused row keeps the lit edge from the overlay (AC9).
4. **The chevron's run.** The flat chevron bevels its right corners with radius 35 (`CHEVRON_RADIUS`); a `StyleBoxFlat`
   shrinks two radii that exceed a side, so the tip sits at mid-height for any height up to 70 px with a run of h / 2
   (29 to 35 px) instead of the fixed 30. The nine-slice has the exact run.
5. **Tag and tab geometry in the flat form.** The tag's right slant is an 8 px bevel at the bottom right; its height is
   about 29 px (Outfit's natural 22.7 px line plus 3 + 3) with the 6.4 content margins, not 24. The tab's slanted end is a
   16 px 45 degree bevel at the top right, and a 48 px tab needs vertical content margins of 9 (Outfit 24's natural line is
   30 px), not the CSS 12.
6. **The TabContainer's inner panel** is a quiet field (`TabPanelFill`, 2 px `EdgeDim`, a 12 px bevel) keeping the pulp
   content margins 18 / 18 / 24 / 26, so the Settings layout does not move before TG-PAUSE restyles it.
7. **The READY badge's glow** is 6.2's `StyleBoxFlat` shadow, which also fills under the translucent badge, so the badge
   reads more gold than the mock (about .40 against .14). TG-SELECT may prefer `draw_center = false` and a separate fill.
8. **`TrackBackground` -> `TubeTrackTop`** (the plan's instruction) changes `RadialProgress`'s default remainder,
   including the world-space Restoration Font and Resonance Hold channel rings, from #3A3144 to #02050E @0.95 (darker,
   still near-opaque). TG-WIDGETS step 2 gives the lens rings their own `RingTrack` remainder.
9. **Fonts that grew.** The default `Button` (the field) is Outfit 600 22 (was Outfit 700 20); the tabs Outfit 600 24 at
   .13 em (was Outfit 700 20); the chevrons Cinzel 700 34 and 24 at .13 em (was Outfit 700 34 and 27). `TitleLabel` went
   40 -> 36. The UI filter and the extra suites pass at both UI scales; long labels now meet the decor's ellipsis sooner
   (`menu_cover`: "QUICK PLAY (EINSTEIN VS C..."), which TG-WIDGETS' shrink-to-fit and TG-MENU's width rule (R1, D6)
   address.
10. **`HudNumeralSmall`** carries the Story numeral (Outfit 600 22); the Fighter "/max" (spec Outfit 400 20.9) shares it,
    as the plan's table says. `HudNumeralMax` (Outfit 400 18, white) is the Story HP and boss "/max"; the widgets dim it to
    .78 and .75.
11. **`CoverTitle`'s colour is the flat fallback #DFE8FF.** Under `ui_title_gradient` the Label's colour must be white
    (6.5), so TG-WIDGETS' `TitleGradient` should set a white font colour override when it installs the material.
12. **`PromptKey`** is T12 (Outfit 700 19, white, plate); the prompt bar's letter (T13, 18 px with halo S) still shares it,
    with the colour passed explicitly by `PulpPromptGlyphs`.
13. **`TrackerDrop` under Reduced** without a `fadeAlpha` callback (today's pulp decor) snaps after 0.2 s; it used to slide
    linearly. TG-WIDGETS' tube decor should pass a fade callback.
14. **The `FocusPose` release** eases back over 120 ms; the pulp pose snapped one frame after the release. Under Reduced
    the press and the release are instant, so a same-frame click shows no press pose.

### For step 2 (after TG-ART merges; merge `main` first)

- Wire `glass_kit.json` into the generator (a glass texture helper beside the frozen pulp `kit_tex` / `tbox`):
  `glass_panel` -> every slab and preset (content margins = the paddings, the catalog's expand margins); `glass_tag` ->
  `PanelTagPlate` and `HintLinePlate`; `glass_btn_normal`, `_focus`, `_selected`, `_pressed`, `_disabled` -> the three
  chevron variations at 58-70 px (the focus texture as the overlay; decide whether `hover` composites the focus texture
  over the normal fill or stays the lit edge); `glass_tab*` -> the tab styles; `glass_field` (+ `_focus`) -> the default
  `Button`, `OptionButton`, `GlassField`, `LineEdit` and the spin box field; `glass_slider_grabber` -> the slider grabber
  icons; `chevron_down` for the option button arrow if TG-ART ships it as a texture.
- Update `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants` and `DialoguePresentationTests` for the textures (the
  `Fill` helper and the dialogue case already sample a nine-slice's centre texel; their targets may need the baked blend
  of `GlassFillTop` and `GlassFillBottom`), re-run, re-capture, compare against the crops.
- Keep the bar variations as they are unless the orchestrator rules otherwise (deviation 1).

### For the orchestrator to decide

1. **The HUD bar flip** (deviation 1): who turns `PulpBar`, `PulpBarH44`, `PulpBarH16`, `PulpBarH14` and `PulpBossBar` into
   value carriers, and when. (a) TG-WIDGETS step 2's tube decor hides the bar's own drawing on attach (for example the
   bar's `SelfModulate` alpha 0, which still lets its children draw) and reads the colour from the fill style, and the
   closeout flips the theme; (b) a short TG-THEME step 3 right after TG-WIDGETS step 2 merges; (c) a generator cross-edit
   by TG-WIDGETS step 2 for those five rows only. I recommend (a): no generator ownership moves and no bar is ever blank.
2. **Hover parity** (deviation 2): accept the approximate AC8 in the flat form; in step 2 either bake a hover texture or
   have the decor take focus on mouse-enter.
3. **The `RadialProgress` default remainder** for the world-space channel rings (deviation 8): accept the darker
   `TubeTrackTop`, or keep `TrackBackground` at the pulp `Track` until TG-WIDGETS step 2 re-themes the rings.

### Drafted doc text (for the closeout)

- AGENTS.md, UI section: "The theme is generated by `tools/ui_theme/build_ftt_theme.py` from one palette table, the Label
  roles of UI_SPEC_GLASS 2.3 (66 roles; scene text carries the 2.4 halos) and the glass styles; the generator writes the
  palette into `UIPalette.Roles.cs` (`GeneratorPalette`), and `UIThemeTests` pins every entry to the same-named
  `UIPalette` constant and the glass constants to the spec. The focus box of every chevron, field and line edit is an
  overlay (the lit edge and the 14 px glow) over state boxes that carry the fill. Motion is MOT-A (`scripts/UI/Motion/`,
  legacy name `PulpMotion`): a 14 px focus slide, panel fade-rise and fade-sink, the hold-and-drain tracker, a single
  ready bloom and a 0.2 s bg0 dissolve between scenes (`ScreenWipe`)."
- CLAUDE.md, UI captures: add `compare_capture.py` (the README's "Comparing a capture with the mockups").

## Step 2 (2026-10-27)

`main` at `90167018` (TG-ART merged, 3696) merged into `tg/theme` (`694e9a32`), `--headless --import` run in the worktree
under the lock, then TG-ART's kit wired into the generator. Commits `eaf31837` (code) and `18e1b477` (a bounds guard in
the rewritten test); this section in the next commit.

### What shipped

| File | Change |
|---|---|
| `tools/ui_theme/build_ftt_theme.py` | Reads `assets/sprites/ui/glass/glass_kit.json`: `glass_tex` (an ExtResource per stem) and `gbox` (a `StyleBoxTexture` with the catalog's texture, 6.4 nine-slice margins, expand margins and tile flag; the content margins are the padding or inset; an optional palette modulate). `slab`, `tag_plate`, `chevron_set`, `field_set` and the new `tab_set` build on it; the slider grabber icons are `glass_slider_grabber` with `center_grabber` 1; new palette entry `DimModulate`. The flat chevron, slab, tag and tab helpers and `CHEVRON_RADIUS` / `SLAB_CHAMFER` are gone; `FIELD_CHAMFER` stays for the tooltip. |
| `resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.Roles.cs` | Regenerated: 38 ext (15 glass textures), 115 sub-resources, 735 items, 66 roles, 83 palette entries. |
| `scripts/UI/UIPalette.cs` | New `GlassFillMid` (the fill `glass_panel` bakes at its centre, the midpoint of the 160 degree build gradient) and `DimModulate`; `PanelBackground` = `GlassFillMid` (was `GlassFillFlat`); `ButtonHover` and `FocusFill` = `ButtonFill` (were `ButtonFillFocus`); `TrackBackground` = the pulp `Track` again (ruling 3; was `TubeTrackTop`). |
| `tools/ui_theme/README.md` | The styles paragraph describes the nine-slice wiring. |
| Tests, in place | `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants`, `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation` (below). |

### Wiring (`glass_kit.json` to the theme)

| Kit texture (size; nine-slice L/T/R/B; expand) | Theme entries |
|---|---|
| `glass_panel` (140 x 140; 42/28/42/56; 22/8/22/36) | `Panel`, `PanelContainer`, `InkPanel`, `HudPlatePanel` (padding 22 / 28 / 22), `DialogueGlassPanel` (22 / 26 / 22), the eight 3.1 presets (their paddings), `PopupPanel` and `PopupMenu` (8) |
| `glass_tag` (40 x 24; 3/0/9/0) | `PanelTagPlate`, `HintLinePlate` (the credits hint), content 14 / 3 / 14 / 3 |
| `glass_btn_normal` (96 x 70; 2/2/32/2) | the three chevrons' `normal` and `hover` (one sub-resource, ruling 2) |
| `glass_btn_pressed` | `pressed`, `hover_pressed` |
| `glass_btn_disabled` (104 x 70; tile) | `disabled` |
| `glass_btn_focus` (124 x 98; 16/16/46/16; 14) | `focus` (the overlay) |
| `glass_btn_selected` | `selected` (swapped in by the decor) |
| `glass_field` (48 x 44; 10) | `normal`, `pressed`, `hover_pressed` and (at `DimModulate`) `disabled` of the default `Button`, `OptionButton` and `GlassField`; `LineEdit` `normal` and (at `DimModulate`) `read_only`, which the spin box's field inherits |
| `glass_field_hover` (76 x 72; 24; 14) | `hover` of `Button`, `OptionButton`, `GlassField` (ruling 2) |
| `glass_field_focus` (76 x 72; 24; 14) | `focus` of `Button`, `OptionButton`, `GlassField`, `LineEdit` |
| `glass_tab` (64 x 48; 1/0/17/0) | `GlassTab` `normal` and (at `DimModulate`) `disabled`; `TabContainer` `tab_unselected` / `tab_disabled` |
| `glass_tab_hover` (92 x 76; 15/14/31/14; 14) | `GlassTab` `hover`, `TabContainer` `tab_hovered` (ruling 2) |
| `glass_tab_selected` | `GlassTab` `pressed` / `hover_pressed`, every `GlassTabSelected` state, `TabContainer` `tab_selected` |
| `glass_tab_focus` (92 x 76; 15/14/31/14; 14) | `GlassTab` / `GlassTabSelected` `focus`, `TabContainer` `tab_focus` |
| `glass_slider_grabber` (64 x 64, icon) | `HSlider` / `VSlider` `grabber`, `grabber_highlight`, `grabber_disabled`; `center_grabber` 1 |

Stays as step 1 built it (a `StyleBoxFlat`, 6.2's form; the separators a `StyleBoxLine`): the READY badges, the scrim,
the slider track and fill, the scroll bars, the check box ring, the tooltip, the spin box button backgrounds, the
separators and the tab container's inner panel. The
five HUD bars keep their pulp tracks and fills (ruling 1); the pulp-only plates are unchanged.

### Rulings applied

1. **HUD bars:** untouched.
2. **Hover:** a chevron's `hover` is its `normal` sub-resource (`UIThemeTests` checks it is the same box), so a hovered
   chevron draws exactly what a focused one does once the decor takes focus on mouse-enter. `glass_btn_hover` stays in
   the kit, wired to nothing. Fields and tabs hover with `glass_field_hover` / `glass_tab_hover`.
3. **`TrackBackground`:** the pulp `Track` (#3A3144) again, so `RadialProgress`'s default remainder (the world-space
   Restoration Font and Resonance Hold rings) is unchanged; the tube's tracks stay `TubeTrackTop` / `TubeTrackBottom`.
4. **TG-ART's choices** (the `glass_ico_*` prefix, the P5 focus split, the Vitals gradient layout, the disabled haze):
   taken as built. `chevron_down` is not wired (deviation 1).

### Deviations and agent choices (step 2)

1. **The `OptionButton` arrow is still Godot's default.** `glass_ico_chevron_down_bold` is a 64 x 64 canvas (the
   glyph with its baked glow), and an `OptionButton` draws its `arrow` icon at the texture's own size (no theme constant
   scales it), so wiring it would draw a 64 px chevron. It needs an 18 px raster from TG-ART, or TG-WIDGETS' field decor
   draws the arrow. **For the orchestrator** (below).
2. **A slider is now 64 px tall.** A `Slider`'s minimum height is its grabber icon's, and the glass grabber's canvas is
   64 px (the 31.7 px diamond, its ring and its 16 px glow). The Settings sliders' scene minimum is 44 px, so each
   slider row grows from 44 to 64 px (Audio: four rows, +80 px; everything still fits, and the UI filter's layout cases
   pass at 1.0 and 1.4). `center_grabber` makes the diamond's centre travel the whole track (the mock's handle sits at
   x = v %); at 100 % the diamond overhangs the track's end by about half its width and covers the slanted cap. A
   tighter canvas from TG-ART would bring the rows back.
3. **Disabled fields and tabs** are `glass_field` / `glass_tab` at `DimModulate` (white at .50; agent choice, a new
   palette entry and constant): the kit has no disabled field or tab. `LineEdit` `read_only` the same.
4. **A pressed field is the field itself:** there is no pressed field texture; under its focus overlay a pressed field
   reads as the focused field.
5. **A hovered tab reads as selected:** `glass_tab_hover` is the selected gold plus the glow (TG-ART 2.1), so its text
   turns `TabSelectedInk` to stay legible (`GlassTab` `font_hover_color` and the `TabContainer`'s `font_hovered_color`,
   both `Ink` before). An unselected tab under the mouse looks selected until the mouse leaves.
6. **A field hovered and focused** draws the hover box (lit edge and glow) under the focus overlay (lit edge and glow):
   a doubled glow (TG-ART 2.1 noted it). Fields have no decor to take focus on hover.
7. **The tooltip stays a small flat slab:** `glass_panel` needs 84 px of height for its top and bottom slices against a
   tooltip's 36 to 40, so its corners would overlap.
8. **Popups** draw `glass_panel` at padding 8 with its baked shadow; whether the popup window clips the shadow outside
   its rect was not seen (no capture opens a popup). The slab's edge sits on the popup's rect either way.
9. **Step 1's deviations 2, 4 and 5 are resolved by the art:** the focus overlay now carries the focus wash (AC8 by
   ruling 2), the chevron tip has the nine-slice's exact run, and the tag and tabs have their real slants (tabs measure
   48 px in `theme_controls`).

### Test delta

No count change in step 2: 3698 = `main` 3696 + TG-THEME's +2 (the `[TestCase]` count in the worktree is 3698).

- `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants`: the texture branches. A nine-slice's centre texel is compared
  with its constant composited as the kit bakes it, at 0.03 (the kit's rounding): the slab with `PanelBackground` over
  `PanelShadow`; each chevron's `normal` and `hover` with `ButtonNormal` / `ButtonHover`, and `hover` must be the `normal`
  box; `pressed` under the focus overlay with `ButtonPressed` (0.05) and brighter than `normal` under it; `disabled` with
  `ButtonDisabled` over the averaged stripe haze rgba(glow, .15) (TG-ART 6.2), tiled, and fainter than `normal`. A focus
  overlay (default `Button`, the chevrons, `LineEdit`) expands 14 px on every side, its texel at the expand margin is the
  lit edge (`FocusOutline`) and the one outside it the glow (`FocusGlow`'s colour, alpha in (0, .55]). The slab's edge
  texel has `PanelBorder`'s hue. The default `Button`'s `hover`, `LineEdit`'s `focus`, the `TabContainer`'s `tab_hovered`
  and `GlassTab`'s `hover` draw their named kit textures. The flat branches are kept.
- `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation`: the nine-slice branch compares the centre
  with `DialogueTheme.GlassBackground` over `PanelShadow` at 0.03, and the failure message names both colours.
- `18e1b477` keeps the overlay and slab texel reads inside the image when an expand margin is zero.

### Runs

- UI filter (plan 3.2), in this worktree under the lock: **710 / 710** before the commits and again at `18e1b477`
  (708 on `main` at `90167018`: 703 + TG-ART's 5; +2 TG-THEME).
- The step 1 extra suites (the twelve named there): **74 / 74**.
- `dotnet build`: 0 errors, the two known warnings. The capture run logged no error or warning. The generator reproduces
  the committed `.tres` (only line endings differ after a regeneration). `.import` line-ending churn from the import and
  every launch reverted; none committed.

### Fail evidence (every rewritten assertion seen failing)

Mutation set C on top of `18e1b477`, one filtered GdUnit run under the lock (`UIThemeTests` and
`DialoguePresentationTests`), reverted with `git checkout -- .` (log `D:\Projects\ftt-scratch\tg-theme\s2mutC.log`): the
chevrons' `hover` the full-look `glass_btn_hover`; their `pressed` drawing `glass_btn_normal`; no expand margins on the
focus overlays; no tile flag; the field's `hover` the plain field; the tab's hover `glass_tab_selected`;
`PanelBackground` = `GlassFillTop`; `PanelBorder` = `Ink`; `ButtonDisabled` = `ButtonFill`. Result: 2 failed, 27 passed,
exactly the two rewritten cases, with every mutation reported by its own message: "PanelContainer/styles/panel paints
...", "PulpButton/styles/hover must be its normal box" (and the hover fill, for all three chevrons), "pressed under focus
paints ..." and "does not read brighter than normal under focus", "disabled paints ...", "must tile its dashes", "focus
overlay expands 0 px on side 0..3", "focus edge is ...", "focus glow is ..." (default `Button`, chevrons, `LineEdit`),
"the slab's edge is ..., not the hue of UIPalette.PanelBorder", "Button/styles/hover draws .../glass_field.png, not
glass_field_hover", "TabContainer/styles/tab_hovered" and "GlassTab/styles/hover draws .../glass_tab_selected.png", and
the dialogue case "The glass paints (0.035, 0.059, 0.133, 0.973) at its centre but DialogueTheme says (0.056, 0.090,
0.196, 0.968)". The other `UIThemeTests` cases and the dialogue suite's other cases stayed green.

### Captures and comparison

Into `D:\Projects\ftt-scratch\tg-captures\theme\` (windowed console Godot, under the lock; step 1's set moved to
`theme\step1\`): `theme_labels_small`, `theme_labels_large`, `theme_controls`, `theme_palette`, `theme_glass_components`,
`main_menu_root`, `menu_cover`, `menu_cover_scale14`, `pause_story`, `pause_story_ui14`, `settings_audio`,
`settings_display`, `settings_gameplay`, `settings_controls`, `settings_controls_ui14`, `confirm_modal`,
`two_option_prompt`, `credits`, `movelist`, `systems_card`, `resonance_grid`, `select_cards`, `huds_calm`, `hudf_versus`,
`results_level`, `dialogue_box`, `widgets_buttons`, `widgets_tagged_panel`, `drill_list`, `timeline_restart`.
Comparisons (`compare_capture.py`, beside them): `theme_glass_components` against the `menu_button_column` crop
(`--crop 30,54,614,490`), `pause_story` against `rec_03` and the `pause_panel` crop.

- **Chevrons against the menu crop** (texels at the row's centre): normal (34, 33, 37) left and (14, 17, 32) right
  against the crop's (37, 35, 38) / (14, 17, 32); focused (102, 88, 54) / (31, 30, 36) against (101, 90, 60) /
  (42, 42, 45), the wash matching on the left and about 11 darker on the right (TG-ART's note: the exact CSS without the
  browser's blur); the lit edge (216, 179, 82) over 1.5 px against the crop's (210, 174, 79) over 2 px; the glow above
  it within a few units. Geometry coincides; the markers and the A badge are TG-WIDGETS'.
- **Pause panel against `rec_03`**: the slab is the glass navy with the chamfers and the gold edge; its fill runs
  (27, 32, 50) to (15, 19, 38) down the left and about (14, 17, 32) on the right, against the crop's lighter, warmer
  (64, 62, 60) to (35, 34, 46) and (23, 26, 40): the mock's backdrop blur of bright art, which the build has no means to
  draw (1.4, by design one level darker). The build's edge is brighter and thinner ((152, 123, 59) then (86, 73, 51))
  than the render's 2 px (86, 62, 40). Layout, tag, markers and badges are TG-PAUSE's and TG-WIDGETS'.
- **Seen and right:** every chevron state (normal and hover alike, the focused overlay with its wash and glow, pressed,
  focused and pressed, selected, the dashed disabled ring with its haze), the slabs and presets at small sizes, the tag
  (also as the credits hint), the tabs (selected gold, unselected, the glow-only focus, dimmed disabled), the fields
  (normal, focus, dimmed disabled; the option button and the spin box field), the slider's diamond with its glow, and
  every sampled screen at 1.0 and the 1.4 captures. Unchanged from step 1 (not this workstream's): the pulp decor's
  selected marker overlaps the label's first letter in `widgets_buttons` (same in `step1\`), the pulp HUD bars, cover,
  cards and plates.
- **Not seen:** a hovered field or tab, a mouse, a pad, an open popup, motion in flight, Reduced Temporal Effects.

### For the orchestrator to decide

1. **The option button arrow** (deviation 1): (a) TG-ART ships an 18 px `chevron_down` raster (bold, glow baked) and the
   generator wires it as `OptionButton/icons/arrow` (one line); (b) TG-WIDGETS' field decor draws the arrow and the theme
   sets an empty arrow. I recommend (a).
2. **The 64 px slider rows** (deviation 2): accept, or TG-ART trims the grabber canvas (the glow clipped to, say, 40 px).
3. **The tab hover** (deviation 5): accept TG-ART's selected-plus-glow hover, or ask for a quieter dark hover tab.
4. **The doubled glow on a hovered, focused field** (deviation 6): accept, or have fields take focus on mouse-enter as
   the chevrons do.
5. **Disabled fields and tabs** at `DimModulate` .50 (deviation 3): accept, or TG-ART ships dedicated textures.

### Drafted doc text (step 2 addendum, for the closeout)

- AGENTS.md, UI section, after the generator sentence: "The glass styles are TG-ART's nine-slices: the generator reads
  `assets/sprites/ui/glass/glass_kit.json` for each texture's 6.4 margins, expand margins and tile flag (`gbox`). A
  chevron's hover is its normal box (the decor takes focus on mouse-enter); a field and a tab hover with their own boxes.
  The READY badges, scrim, slider track, scroll bars, check ring and tooltip stay `StyleBoxFlat`."
