# TG-PAUSE handoff: the pause family and the overlays in Temporal Glass

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.11). Look authority: `UI_SPEC_GLASS.md` (section
numbers below are its own: 3.1-3.9, 4.3, 9.1, 9.2). Branch `tg/pause`, worktree `D:\Projects\ftt-wt\tg-pause`, cut from
`main` at `8612b571`, fast-forwarded to `aec29b3b` (the decor's row widening, 3711) before any edit, and merged with
`main` at `67a891d9` (TG-WIDGETS step 2, TG-MENU, TG-SCREENS; 3722) before the final captures. Commits: the code
`36df1d2b`, the merge `dd4eaf96`, then the follow-up (title box, scene-authored slab variation, the shared prompt
clearance, the boot toggle row) and this handoff.

## What shipped

| File | Change |
|---|---|
| `scripts/UI/PausePanel.cs` | The glass slab: `GlassPanelPause` (30 / 20 / 28, `Border` 0) as the default variation only (a scene that authors its own, Settings / Move List / Systems Card `InkPanel`, keeps it: the constructor used to overwrite it, because a `.tscn` sets the built-in properties written before `script = ...` first); the sheen at `_Ready`; `PlayAppear(dim)` / `StopAppear(dim)` (MOT-A row 14: the dim 0 to .70 over 0.16 s, linear; row 4: `PulpMotion.PopIn`; both in the process pause mode; Reduced: fades only); `ApplyRowWidths` (D6: one width for the stack, at least 560, at most 700 x the UI scale, re-run every time the panel shows); `TrimTitleBox` (below); `AppearTween` / `DimTween` test surfaces. |
| `scenes/ui/StoryPause.tscn`, `scripts/UI/PauseMenu.cs` | One decorate path for both builds: 64 px chevron rows (`StylePauseButton`), the D6 width, the 14 px stack inset, the sheen, the short level title tag, the R7 status strip (`pause_status_level`), a 6 px `ListGap`, the title box trimmed, `MenuSafeAreaFit` clear of the prompt bar, the bottom-centre prompt bar (Select / Resume). Opening fades the dim in and the slab up; closing is immediate (`StoryPauseTests` pins the root hidden at once). One slab over one dim: Settings, the Move List, the Dialogue Log and every confirmation open with `UsesHostDim` and the pause slab and its prompt bar stand down until they close; focus returns to the calling row. Hub and Act III variants only hide or show rows; the stack width follows. |
| `scripts/UI/LocalFighterPause.cs` | The same slab; tag = the stage's name (`FighterStageCatalog`, else `menu_fighter_mode`), status = the match mode (`FighterHudModel.ModeLabelKey`); `RowCapAtOne` 760 so "MOVE LIST - JOAN OF ARC" keeps its size. The disconnect modal as the confirm modal: a 760 `InkPanel` slab tagged `pause_tag_controller` over its own .70 dim (the pause's dim and prompt bar stand down: its B would not resume), the message in `BodyLabel` (680), two stacked chevron rows sharing a D6 width, and **its first row now takes focus** (nothing in it had focus before, so a still-connected pad could not answer it). |
| `scripts/UI/ConfirmModal.cs` (the two-option prompt rides it) | 760 slab (default padding, untagged), the title in `TitleLabel` (9.1 item 2), the message in `BodyLabel` 680, two compact chevrons at least 260 wide, 34 apart (`SeparateFocusRow`), `GrowToFit` (labels wrap, never trim), the bottom prompt bar with the cancel caption, `UsesHostDim`, the slab's fade-rise. A workaround for kit gap 1 re-runs the decor's own grow rule when the prompt badge changes size. |
| `scenes/ui/Settings.tscn`, `scripts/UI/SettingsMenu.cs`, `scripts/UI/SettingsRowLayout.cs` (new) | A centred slab (`InkPanel`, 1520 x 860 minimum; the Controls table makes it 1612 at 1.0) over its own .70 `Shade` (or the host's); the title upper-case `TitleLabel`; glass tabs with upper-case titles (`SettingsMenu.TabTitle`, T55); a quiet tab panel (24 / 14 padding). Every tab is a `SettingsRowLayout`, a container that lays out its own direct children as 3.3 rows (62 tall, items 22 apart, the T52 label column 360, a 360 slider or a 300 x 44 glass field, the T53 value column 90, toggles 360 + 22 + the 132 track), so every bound path (`Tabs/Gameplay/UiScaleSlider`, `.../PlayerSlotPaletteRow`, `Controls/Options/HapticToggle` ...) is unchanged. Audio, Display and Gameplay **scroll in place** (`Scrolls`): the layout offsets and clips its rows, keeps a glass `VScrollBar` as an internal child, scrolls the focused row into view and takes the mouse wheel. Toggles are `PulpToggleDecor` rows whose label is a `RowLabel` child drawn in T52 (`SkinToggle`, now internal and reused by the boot cards); every slider has a `<Name>Value` column (`hud_percent`, live) and the glass field's focus look; the Controls options run two to a line (label 330, controls 280, value 72); the remap list is 62 px rows: the T52 action label, keyboard and pad binding cells as glass fields (listening: the focus look, steady, `res2`; conflict text in `danger` in the status line), Unbind and Reset compact chevrons, the column model kept (UR-POLISH2's widths, measured on the drawn upper-case labels, x the UI scale); Back and Reset All compact chevrons; the bottom prompt bar. |
| `scenes/ui/MoveList.tscn`, `scripts/UI/MoveListScreen.cs` | 1240 x 900 slab tagged with the hero's style; a 96 px G5 portrait frame beside the title `movelist_title_for` (36 px); sections as 9.1 headings over a fading `edge` rule; rows: slot name (`CaptionLabel`, accent) over the ability name (`StickerLine1`, Cinzel 700 24), stats `BodySmallLabel` in ink2, the 56 px ability icon (dimmed while dormant), the `dormant` glyph; compact footer chevrons (D6, no badge) clear of the scroll area; the Systems Card opens on the same dim and the list's slab stands down. |
| `scenes/ui/SystemsCard.tscn`, `scripts/UI/SystemsCardScreen.cs` | 1240 x 900 slab, the title, eight section headings with their rules, body copy in `BodyLabel`, a compact Back clear of the scroll area, `UsesHostDim`. |
| `scripts/UI/DialogueLogScreen.cs` | 1200 x 860 slab, `TitleLabel`, entries 14 apart: a 64 px G5 frame, the speaker in `HeroName` (Cinzel 700 22) recoloured accent, the line in `BodyLabel`, an `HSeparator` between two speakers; compact Back; `UsesHostDim`. |
| `scripts/UI/DialogueManager.cs`, `DialogueTheme.cs`, `DialogueSilhouetteFrame.cs` (new) | The 1500 slab with the sheen, row gaps 26, the name-over-line column 10; the emotion frame chamfered like G5 (22 % TL / BR, corner detail 1); the other speaker's frame (`Panel/Layout/OtherSpeakerFrame`: the G5 frame at .4 with the 3.9 silhouette in ink3) while the sequence names a second speaker (`HasSecondSpeaker`, P6); the solid `hourglass_caret` 34 px in res2 at right 26 / bottom 18. The line sets the box's height (`FitContent`, laid out whole from its first letter, `CharsAfterShaping`), and the box grows upward, so a long line at UI scale 1.4 is read whole instead of scrolling out of reach (see deviation 7). Typewriter speed, holds, hold-to-skip and padding unchanged. |
| `scripts/UI/FirstUseTooltipPresenter.cs` | A G2 plate (the theme's tag nine-slice, padded 14 / 22 / 10) over a dark glass base in the same slanted shape, top-centre at y 140 (x the UI scale), as wide as its line up to 900, wrapping beyond; `BodySmallLabel` (Outfit 400 22, ink); fade-rise in (`PopIn`; Reduced: 150 ms fade); `PresentForPreview` capture seam. Node `TooltipPanel` kept; `TooltipText` now sits in `TooltipPanel/TooltipWash`. |
| `scripts/UI/BootSequenceOverlay.cs` | `bg0` at .92 (a full-rect `Backdrop` panel, so the slab can fade-rise and the bounds test still measures a centred dim); an 1100 `InkPanel` slab with the sheen and `MenuSafeAreaFit`; titles `TitleLabel` upper-case; body `BodyLabel`; first-run sections as 9.1 headings with rules; the notice's Continue a full chevron (no explicit width), the rest compact chevrons in centred rows; the comfort toggle as a settings toggle row (its own `SettingsRowLayout`, so the two-line T52 label never spills onto the copy below at 1.4); the first step fades and rises in; `ShowStepForPreview` capture seam. |
| `scripts/Diagnostics/UiCapture/UiCaptureTargets.Pause.cs` | Every target on the warm stage, `PauseSettle` 30 frames (the motion ends inside it), a 1.4 twin for every surface, `boot_first_run`, `boot_first_run_ui14`, `boot_crash_prompt`; the tooltip through the real present path with a shipped key. |

### The pause title box (`PausePanel.TrimTitleBox`)

T04 stands on a line height of 1 (its 78 px) with a 16 px padding below (4.3 row 3b), but a Godot label's box is the
font's full height: Cinzel's ascent and descent run about 31 px past 78. That left the title 15 px, and the status strip
and every row 27 px, lower in the slab than the mock (TG-WIDGETS saw the same in its replica). The title now carries its
own copy of its role's `FontVariation` whose negative `spacing_top` / `spacing_bottom` trim half the excess above and,
below, half the excess less what the 9 px separation leaves of the 16 px padding; it is recomputed on every theme change
(a UI-scale change) and keeps the role's size, colour and halo. After it: title glyphs 35-91 px below the slab's top
edge (mock 36-92), the status strip 127-139 (mock 124-136), the focused row's text from 183 (mock 180).

## Node paths

- Settings: `Backdrop` (ColorRect) is now `Shade` (PanelContainer, the dim); `Margin` is a `CenterContainer`; `Tabs/Audio`, `Tabs/Display`, `Tabs/Gameplay` and `Tabs/Controls/Options` are `SettingsRowLayout`s (were `VBoxContainer` / `GridContainer`); new `<Name>Value` labels beside the ten sliders; every other path kept. `SettingsMenuSceneTests.TheDeadDifficultyDropdownIsGone` reads `Gameplay` as `Container`. Nothing else binds the renamed `Backdrop`.
- `StoryPause.tscn`: new `Root/Center/Panel/Layout/ListGap`.
- Dialogue box: new `Panel/Layout/OtherSpeakerFrame`; `CaretHost/AdvanceCaret` keeps its path (its icon is `hourglass_caret`).
- First-use tooltip: `TooltipPanel/TooltipBase`, `TooltipPanel/TooltipWash/TooltipText` (the text was a direct child).
- Boot overlay: `Root/Backdrop` is a PanelContainer (was a ColorRect); `Content/ComfortRow/ToggleRow/ReducedEffectsToggle`.

## Test delta

**+2**, as declared. Branch: 3711 -> 3713; merged with `main` (3722): **3724** `[TestCase]` attributes.

- New: `PauseSkinTests.ThePauseDimIsSeventyPercentAndThePanelFadeRisesWhileTheTreeIsPaused` (the dim and the panel start from nothing, both move while the tree is paused, the dim ends at .70, the panel at alpha 1 having risen into its slot, closing is immediate); `OverlaySkinTests.SettingsTabsFieldsSlidersAndScrollBarsWearTheGlassLooks` (upper-case tab titles on the theme's glass tabs; every option button a glass field at least 300 x 44; every slider 360 wide with the glass field's focus look and a 90 px T53 value showing `hud_percent`, the three on one centre line; toggle rows 62; Audio / Display / Gameplay scroll behind the theme's `VScrollBar`; focusing the last Gameplay row scrolls it fully into view; the remap list's bar is the glass bar).
- In place: `PauseSkinTests.TheStoryPauseSkinsTheSameNodesOnBothBuildPaths` (the slab is `GlassPanelPause`; rows share one D6 width in [560, 700 x scale], 64 x scale tall); `OverlaySkinTests` (a kit widget's own text, now including the prompt bar's key-caps, is exempt from the no-override rule; the caret is `hourglass_caret`; the log speaker is `HeroName`); `ConfirmModalTests.TheTitleUsesTheHeadingVariationWithNoFontSizeOverride` renamed `...ThePanelTitleVariation...` (`TitleLabel`); `LocalFighterPauseTests.TitleAndDisconnectLabelUseThemeVariationsWithNoFontSizeOverride` (the message is `BodyLabel`); `SettingsMenuSceneTests` (tab titles upper-case and not the node names; `Gameplay` as `Container`).
- Mine and unchanged: `UrPolishTests.TheModalAndResultsButtonLabelsShowWhole...` failed after the 760 modal ("CancelButton@1: a word is wider than its line", the drawn "CALIBRATIO") and is green with the badge re-measure (kit gap 1). `UrPolish2Tests`' two TG-PAUSE methods passed untouched.

## Runs (this worktree, the lock held for every launch)

Filter: the plan's UI filter (3.2) plus `AudioBootVolume`, `FighterMatchFlowContent`, `HubDifficultyLowering`,
`InputDeviceFollow`, `Level00FlowScene`, `LevelStartResourceReset`, `PauseFocusLoss`, `SessionExitGuard`,
`StoryCombatRules`, `StoryHealingLoop`, `StoryMeterGainScale`, `PlayerSlotPalette`, `MoveListDormant` (every suite that
builds one of these screens).

| Run | Tree | Result |
|---|---|---|
| 1 | restyle, tests not yet updated | 812: 9 failed (the eight pins above, and `UrPolishTests` on the modal) |
| 2 | + updated and new tests | 814: 1 failed (my new Settings case measured a row by its label's box; it now checks the centre line and the toggle rows) |
| 3 | + badge re-measure, the case fixed | **814 / 814** |
| 4 | merged with `main` (`dd4eaf96`) + title trim, scene-authored variation | **825 / 825** (the filter now also matches `main`'s new suites) |
| 5 | + TG-SCREENS' scaled prompt clearance | 825: 1 failed, `UrPolish2Tests.AFocusedButtonInAHorizontalRow...` ("1.4 movelist footer ... 33.06 px apart, under the spec's 34"): the scaled clearance (56 x 1.4) shrank the 900 px Move List at 1.4 to 0.97, and the fit's scale shrinks the footer's 34 px gap on screen |
| 6 | + the unscaled shared clearance (final code) | **825 / 825** |

## Fail evidence (every new or rewritten case seen failing)

Two mutation runs (`mutate.py` in the scratch folder: back up, mutate, run the cases, restore; md5 checked after):

| Mutation | Case | Failure |
|---|---|---|
| `PauseMenu`: `PlayAppear` -> `StopAppear` | `ThePauseDimIs...` | "The dim fades in from nothing." |
| `StoryPause.tscn`: `GlassPanelPause` -> `InkPanel` (after the constructor fix) | `TheStoryPauseSkins...` | expected `GlassPanelPause` but is `InkPanel` |
| `Settings.tscn`: Gameplay `Scrolls` removed | `SettingsTabsFields...` | "Gameplay does not scroll" |
| `ConfirmModal` title -> `HeadingLabel` | `TheTitleUsesThePanelTitle...` | expected `TitleLabel` but is `HeadingLabel` |
| `SettingsMenu.TabTitle` -> plain translation | `TabTitlesAndControlsCopy...` (and the new Settings case) | expected `AUDIO` but is `Audio` |
| a `font_color` override on the Settings title | `EverySettingsSwitch...` | "Title" |
| `CaretIcon` -> `hourglass` | `TheDialogueBoxShows...` | expected `hourglass_caret` but is `hourglass` |
| disconnect message -> `EmphasisLabel` | `TitleAndDisconnectLabel...` | expected `BodyLabel` but is `EmphasisLabel` |
| log speaker -> `SpeakerName` | `TheDialogueLogRows...` | expected `HeroName` but is `SpeakerName` |

The first run showed the slab-variation mutation passing: that is how the constructor overwriting a scene's variation
was found (fixed, then re-run red). The caret pin now names the spec's icon instead of the constant it would follow.

## Captures

`D:\Projects\ftt-scratch\tg-captures\pause\final\` (34 PNGs, from the merged branch): `pause_story`,
`pause_story_fallback`, `pause_fighter`, `disconnect_modal`, `settings_audio` / `_display` / `_gameplay` / `_controls`,
`movelist`, `systems_card`, `confirm_modal`, `two_option_prompt`, `dialogue_box`, `dialogue_log`, `first_use_tooltip`,
`boot_overlay`, `boot_first_run`, `boot_crash_prompt`, and a `_ui14` twin of each (the crash prompt excepted). The
pre-change set is in `...\pause\before\`.

`pause_story` against `rec_03_pause` (whole, `--diff`) and `pause_panel__from_03_640_66` (`--crop 608,162,642,810`,
aligned on the slab's top-left corner):

- Match: the slab (chamfer, edge, sheen streaks), the tag straddling the top edge 26 px in, the title, the status strip,
  the row pitch (74 against 73), the chevrons, the diamond markers, the focused row (gold fill, white outline, glow, the
  14 px slide, the badge), the prompt bar bottom-centre. After the title trim every row sits within 3 px of the mock's.
- Differences, all expected: the slab is vertically centred (P2; the mock hangs it at y 92); it is 664 wide against 598
  because D6 sizes the rows from the widest label plus the real keyboard key-cap ("ENTER", 80 px) to 610 (the mock's
  558 rows have no badge room); the status reads R7's line (difficulty, clock, Integrity) where the mock shows rewinds;
  the capture has no save context, so there is no Lower Difficulty row; the badge is the keyboard key-cap, not (A).
- Row labels draw a little narrower than the mock ("QUIT TO MAIN MENU" 395 px against 454; "SETTINGS" 200 against
  210): the theme's tracking (TG-WIDGETS measured the same, an integer `spacing_glyph`).

## Deviations and why

1. **Settings is 1612 x 860 at 1.0** (spec 1280 x 860, an agent choice): the remap table keeps UR-POLISH2's column model with every default binding whole, and the spec's own parts widen it (T52 upper-case action labels, compact chevrons for Unbind / Reset, 34 px focus gaps): about 1510 px of table. At UI scale 1.4 `MenuSafeAreaFit` then shrinks the slab to the screen, so Settings text grows only ~1.16x (the title's caps 25 -> 29 px) where the rest of the UI grows 1.4x (decision 1).
2. **Settings scrolls inside `SettingsRowLayout`**, not a `ScrollContainer`: a scroll container would move every bound path (`UiScaleTests`, `PlayerSlotPaletteTests` and the screen bind them); the layout's own scroll keeps them and still shows the glass bar.
3. **Slider rows are 64** (the grabber's canvas; accepted by the orchestrator); toggle and field rows 62. "REDUCED TEMPORAL EFFECTS" wraps to two lines in the 360 label column at 1.0.
4. **Secondary small copy is `SmallLabel` (Outfit 500 18, ink2)**, not Outfit 400 22 ink2: `BodySmallLabel` is ink, and a colour override on a Settings label breaks `OverlaySkinTests`' no-override pin (kit gap 4). Body copy is `BodyLabel` 26 where 9.2 says 24 (Systems Card, the log line, the boot notice): there is no 24 role.
5. **The disconnect modal stacks its two actions as full chevrons** ("BIND CONNECTED CONTROLLER" and "FORFEIT AND EXIT" need about 940 px as a row of compact buttons in a 760 slab); it focuses its first row (a behaviour fix: nothing in it took focus).
6. **The pause closes at once** (no MOT-A row 5 sink): `StoryPauseTests` pins the root hidden the moment the pause is released, and a resume should hand control back immediately.
7. **The dialogue line is laid out whole from its first letter** (`CharsAfterShaping`; the reveal no longer re-wraps a word mid-line) and the box grows upward when the line needs it: before, a three-line line at 1.4 showed one line and a scroll bar a pad cannot reach (decision 3).
8. **The first-use tooltip has a dark base** under the G2 wash: G2 alone is a tag meant to straddle a dark panel, and over a bright level its 8-34 % gold wash leaves ink text unreadable.
9. **The Move List** tags its slab with the hero's style (the caption that carried it stands down), titles itself `movelist_title_for`, sets ability names in `StickerLine1` and slot names in `CaptionLabel` recoloured accent (the nearest roles to 9.2's Cinzel 700 24 / Outfit 600 18 .16em accent).
10. **The Fighter pause's D6 cap is 760**, not 700: "MOVE LIST - JOAN OF ARC" would otherwise shrink.
11. **The boot notice's 8 px timer stays a plain `ProgressBar`** (the theme's look), not the GAUGE-A tube that TG-WIDGETS step 2 brought: a 3-second timer under a warning is not a meter. The crash prompt keeps its focus order (the first button, "Don't ask again", as before; 9.1 item 4 would focus the primary).
12. **One clearance constant, unscaled**: the pause family keeps its slabs clear of the bottom prompt bar with TG-SCREENS' `GlassScreenDressing.OverlayPromptClearance` (56; the 58 I had added to `PausePanel` is gone), not its `ScaledOverlayPromptClearance` (56 x the UI scale): the bar's 34 px margin does not scale and its key-caps measure 35 px at 1.4, so 56 still leaves 10-20 px at 1.4 (`pause_story_ui14`: the slab ends at about 998, the bar starts at 1011), while 78 would shrink the 900 px Move List and Systems Card slabs at 1.4 and pull their footer gaps under 34 on screen (run 5). TG-SCREENS' screens read the scaled value (question 7).

## Kit gaps (for the closeout's fold)

1. **`PulpButtonDecor.GrowToFit` caches its word-fit pass** on text, width, font size and scale, but measures the room against the prompt badge's size at that moment; the keyboard key-cap settles wider later, so the focused row's last word was cut ("CALIBRATIO" in the two-option prompt; `UrPolishTests` caught it). `ConfirmModal` re-runs the decor's own rule when the badge resizes (`decor.Badge.Resized` -> toggle `GrowToFit`); the fix belongs in the decor (the badge width in the cache key). Remove the workaround with it.
2. **The decor's badge reserve (36 + 16) is the pad ring's**; the keyboard key-cap with its gap is about 96, so on a keyboard a focused auto-widened compact row shrinks then ellipsizes: the boot crash prompt reads "DON'T ASK AGA..." (`boot_crash_prompt.png`). Same as TG-WIDGETS' open question 1; no width added here, per the ruling.
3. **No slider focus look in the theme**: Settings borrows the `GlassField` focus box for its sliders.
4. **Missing roles**: Outfit 400 22 ink2 (secondary small copy), Outfit 400 24 (9.2's body), Cinzel 700 22 accent (the log's speaker).
5. **`PulpToggleDecor` draws a fixed 132 x 44 track at every UI scale** (the row grows around it); `SettingsRowLayout.ToggleRowWidth` keeps the track on the control column accordingly.
6. **No public way to stop a panel motion and settle it**: `PulpMotion.TakeOver` is private, so `PausePanel.StopAppear` kills its tween and the next `PopIn` settles the panel.
7. **Display roles carry the font's full line height** (about 0.2 em above and below a Cinzel cap line), where the spec's CSS sets line height 1. `TrimTitleBox` fixes the pause title; a role-level spacing in the theme would fix every T0x title at once.

## Not seen

Motion in flight (captures settle 30 frames; the new pause case measures it), Reduced Temporal Effects, pad glyphs
(every capture is on a keyboard), the hub and Act III pause variants (no capture context), a live UI-scale change inside
Settings, listening and conflict states of the remap cells, the tooltip over a real level, locales other than English.

## For the orchestrator

1. **Settings at 1.4** (deviation 1): accept the ~1.16x growth, or make Unbind / Reset glass fields (the table would be about 1200 px, the slab about 1300, and 1.4 would grow ~1.3x), or another cut?
2. **The disconnect modal**: stacked chevrons (deviation 5), or a row of two compact buttons with wrapped labels?
3. **The dialogue reveal** laid out whole from the first letter, and the box growing at 1.4 (deviation 7): keep?
4. **The tooltip's dark base** (deviation 8): keep?
5. **Kit gaps 1 and 2** to TG-WIDGETS: the badge width in `GrowToFit`'s cache and in the reserve; my `ConfirmModal` workaround goes when 1 lands.
6. **The crash prompt's default focus**: keep the first button, or focus "Not now"?
7. **The overlay prompt clearance**: one rule for every overlay, the unscaled 56 (the pause family) or the scaled one (TG-SCREENS' results, Game Over, the drill card)? The scaled one shrinks a 900 px slab at 1.4.

## Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the UI section's redesign bullet, a pause sentence: "Since the 2026-10-27 Temporal Glass rebuild (TG-PAUSE) the pause family and the overlays are glass: `PausePanel` is the `GlassPanelPause` slab (30 / 20 / 28; a scene-authored variation is kept) with the sheen, `PlayAppear` (the .70 dim over 0.16 s and the panel's fade-rise, both in the process pause mode), the D6 row width (`ApplyRowWidths`, 560 to 700 x the UI scale) and the title box trimmed to T04's line height (`TrimTitleBox`); a child screen opened over a pause sits on its dim (`UsesHostDim`) while the pause slab stands down; Settings lays its rows out with `SettingsRowLayout` (62 px 3.3 rows, the bound paths unchanged, Audio / Display / Gameplay scrolling in place behind the glass scroll bar); the dialogue box shows the other speaker's silhouette frame and grows with its line; the first-use tooltip is a G2 plate at y 140; the boot overlay an 1100 slab on `bg0` at .92."
- Ledger, under the glass section: `PROVISIONAL-UI-GLASS-PAUSE` (deviations 1-12 and the numbers: Settings 1520 x 860 minimum, the Controls options 330 / 280 / 72 / 40, the Fighter cap 760, the tooltip padding 14 / 22 / 10 and its 8 px slant, the boot slab gaps 16 / 10, the title trim's 16 px padding) and `VERIFY-UI-SETTINGS-UI14-GROWTH` (decision 1).
