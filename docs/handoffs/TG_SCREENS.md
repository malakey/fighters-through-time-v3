# TG-SCREENS handoff: the remaining screens

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.12). Look authority: `UI_SPEC_GLASS.md` 9.1, 9.2 and
10.2 (no mockup draws these screens). Branch `tg/screens`, worktree `D:\Projects\ftt-wt\tg-screens`, cut from `main` at
`aec29b3b` (3711 green). Code commit `c049be97`; `main` merged at `cfaf0a51` (TG-MENU and TG-WIDGETS step 2, 3719) in
`2d2cb592`, no conflict; this handoff in the next commit.

## What shipped

| File | Change |
|---|---|
| `scripts/UI/GlassScreenDressing.cs` (new) | The two screen types of 9.1 built from the kit: `AddMenuArt` (the sub-screen art, cropped, never stretched), `AddScreenHeading` (a T03 heading between flourishes, top-centre at y 40, held in a `CenterContainer` the layout sweep does not mistake for a panel), `AddOverlayPromptBar` (the kit's prompt bar moved bottom-centre at 34), `FadeIn` (MOT-A row 4 without the rise: alpha over 0.22 s and the sheen's light band; Reduced a 0.15 s fade), `ApplySceneHalo` (halo S / L copied from the theme's `SceneCaption` / `BannerHeading`), `AddDim`, `FlankTitle` + `TitleFlourishes` (flourishes placed around an existing centred title without moving it, so a bound node path survives; hidden when the title leaves no 24 px rule), `ResultRow` + `SplitRow` (a results row drawn over a line whose own `Text` stays the one formatted string the tests read: caption left in `StatLabel`, value right in `SettingsRowValue`), and the UI-scale-aware numbers `HeadingReserve` 100 / `ScaledHeadingReserve`, `OverlayPromptClearance` 56 / `ScaledOverlayPromptClearance`, `ScaledStackCap`. No font size is set anywhere. |
| `LevelResults.tscn`, `LevelResultsPanel.cs` | An 880 slab over the .70 dim, the short-title tag (the built `ShortTitle` rule), `results_level_complete` in `BannerHeading` between flourishes, every line a caption / value row, a 1 px rule (`TotalRule`) over the dust total in `Res`, a gap, the stats rows, the resonance line, one chevron (`results_return_hub`, D6 560 / 700, `GrowToFit`), the prompt bar bottom-centre. The itemization, `SetItemizedLine` and every bound label are unchanged; the leading pulp dust icons are gone. |
| `MatchResults.cs` | The .70 dim, the heading `fighter_results_title` between flourishes at y 40, the K.O. stamp (role and colours pinned, halo L; **hidden on a true tie**), the outcome line led by the winner's slot shape with a 14 px glow in the slot colour, two 600 px glass slabs (`Fighter1` / `Fighter2`, the loser still dimmed), each a 110 px G5 frame, the slot shape, the name (T09 `HeroNameScene`, wraps), the winner's star and the stocks-lost row; on a draw "Draw" in `Ink`; the buttons as compact chevrons at their D6 width (290, no badge: the prompt bar carries it) and the derived row gap. |
| `LoadingScreen.tscn`, `StoryLoading.tscn`, `FighterLoading.tscn`, `LoadingScreen.cs` | All three on `bg0` with the streak. Plain: the 64 px `hourglass` in `Accent2` with a glow disc breathing at 0.5 Hz (static .6 under Reduced). Story portal: the destination title (`campaign_level_<slug>` when it translates, else the built title key) in `BannerHeading` between flourishes, `loading_rift` in T51 (`MenuTagline`). Fighter VS: two 320 px frames (P1 x 280, P2 x 1320, y 300) with a 2 px edge and a 14 px glow in the slot colour, the hero names (`SideCardName`, from `DisplayNameKey`), the player label after its slot shape, "VS" in `CoverTitle` with the title gradient and halo L, the stage name (T14) and the preview on a small slab, the three in one centred column `Middle` so 1.4 pushes them down together. |
| `Credits.tscn`, `CreditsController.cs` | The column is 1100 wide; `credits_title` in `PauseTitle` (78) with the title gradient **on one line** (see deviations), role headings `HeadingLabel` upper-case, names `BodyLabel`, `credits_the_end` in `BannerHeading`, the skip hint on the `HintLinePlate` bottom-right (70 / 44) in its band, the streak. |
| `GameOver.tscn`, `GameOverScreen.cs` | The .82 dim, a 760 slab, `game_over_title` in `PauseTitle`, a cold rule with a cold diamond (`UnboundCold`, `GlassFlourish.RuleDiamondRule`), the body in `Ink2`, the dust notice in `Res2`, two stacked chevrons (D6 560 / 680), the prompt bar bottom-centre, the fade-in. |
| `ResonanceGrid.tscn`, `ResonanceGridPanel.cs`, `ResonanceEdgeLayer.cs`, `ResonanceTierMark.cs` | Full-screen: the sub-screen art under a .55 scrim, the slab with tag `resonance_grid_tag`, the balance in `Res`. Chips are `GlassTileDecor` tiles (chamfer 12, `ShowBehindParent`) over empty chip boxes with the built margins: owned `Lit` (the .20 wash), available `Standard`, locked `Dim`, focused `Focus`; text colours per state (`ChipTextFor`); dormant chips the `dormant` glyph in `Ink3` and no name (the built rule). Edges as built (solid All-of, dashed Any-of) with **the satisfied edge gold** (`Res2` .95 and an 11 px `Res2` .22 glow, R10); the black pulp ink stroke is gone; seams `ColorFor` / `WidthFor` / `GlowWidthFor` / `IsDotted`. Tier marks keep `PointsFor`, cache their polygon and drop the black rim. Respec and Back are one compact row (D6 290, no badge). The confirmation modals sit above the streak (`ZIndex` 11). The chip layout, separation, scale and fit rules are untouched. |
| `CalibrationDrillPicker.tscn`, `.cs` | Full-screen: the art, the heading `drill_picker_title`, the hint, a 3 x 3 grid of glass select tiles (164, pitch 180, `SelectChamfer` 16, the portrait, the card name fitted by `CardTextFit` in 156 px, 11 px from the bottom), the hero preview on a slab (`PreviewPanel`), the footer as compact chevrons (D6 290, no badge, the derived gap), the prompt bar bottom-right, the streak; the block fits under the heading at every scale. |
| `CalibrationDrillList.tscn`, `.cs` | Full-screen as above (`drill_list_title`); the drills as 68 px chevron rows (D6 560 / 700) with the focused drill's objective under the column (`Objective`); **each drill's last result this session at its row's right** (`check` in `Hp`, the `cross` in `Ink2`, a tooltip naming it), recorded by `CalibrationDrillList.NoteOutcome` (process memory, never persisted); the compact footer. |
| `HolodeckDrill.tscn`, `scripts/Combat/CalibrationDrillRunner.cs` (presentation only) | The drill HUD a 900 glass slab (drill name `HeadingLabel`, objective `BodySmallLabel`, progress `CaptionLabel`) with the misses as three `cross` glyphs (`Ink3`, `Danger` once spent; tints written only on a change, the progress text only when the drill changes). The result is an overlay: a `ResultDim` (.70) under a 680 slab (`MenuSafeAreaFit` clear of the prompt bar), the title in `BannerHeading` (`Res2` pass, `Ink2` fail), the coaching, four stacked chevrons, a prompt bar bottom-centre shown with the card, the HUD slab hidden while it shows. The runner calls `NoteOutcome` when a drill ends; `ShowResultCardForPreview` is the capture tool's seam. Nothing reads or writes simulation state. |
| `TimelineRestartPanel.cs` | The .70 dim, a 760 slab, the title, three stacked chevrons (D6 560 / 680) that **wrap instead of trimming** (`GrowToFit`, the confirm modal's rule), the prompt bar bottom-centre (select, back), the fade-in. |
| `FighterPlayOptions.tscn`, `FighterPlayOptionsScreen.cs` | Full-screen: the art, the heading `fighter_play_options_title`, the column (D6 560 / 700) and the Remote Play notice on one **centred** slab (see deviations), the prompt bar, the streak. |
| `NetworkSelectScreen.cs` | Light pass: the art, one slab, compact chevrons, the prompt bar, the streak. |
| `UiCaptureTargets.Screens.cs` | Every existing target kept and re-pointed; new `results_match_draw`, `results_match_140`, `loading_story_140`, `loading_fighter_140`, `game_over_140`, `credits_140`, `resonance_grid_140`, `drill_picker_140`, `drill_list_140`, `drill_list_results`, `drill_result`, `drill_result_fail`, `drill_result_140`, `timeline_restart_140`, `fighter_play_options_140`. |
| `tests/ContentValidation/UiChromeCyanSourceScanTests.cs` | The `ResonanceEdgeLayer.cs` allow-list row removed (R10), nothing else. |

**Node paths changed on purpose** (every binding resolves by name or was updated): `FighterPlayOptions` lost `Center/Panel/Layout/Title` (the heading is the screen's now); `CalibrationDrillPicker` lost `Select/Root/Title` and its preview moved to `Select/Root/PreviewPanel/Preview/{PreviewName,PreviewStats}`; `CalibrationDrillList` lost `List/Root/Title` and gained `List/Root/Objective`; `HolodeckDrill` gained `DrillLayer/Overlay/ResultDim`; the three loading scenes lost the pulp plates and bursts (`Plate`, `Layout`, `VersusMark`, `VersusBurst`, `RiftFrame` and its children): `LoadingScreen` is `Root/Center/Stack/{HourglassHost,StatusLabel}`, `StoryLoading` `Root/Center/Stack/{TitleLabel,RiftLabel,StatusLabel}`, `FighterLoading` `Root/{PlayerColumn,OpponentColumn,Middle,StatusLabel}` with `VersusLabel`, `StageNameLabel` and `StageFrame/StagePreview` under `Middle`. `LoadingScreen` resolves its nodes by name, as before.

## Test delta

`[TestCase]` attribute count **3711 -> 3714 (+3)** on my base; with TG-MENU merged **3719 -> 3722**. Declared +2; the third
case is the list's result glyphs, which needed a record nobody kept (deviation 6), so it carries its own test.

- `ResonanceChipTests` 2 -> 3: new `TheSatisfiedEdgeIsGoldAndTheOtherEdgesKeepTheirMeaning` (satisfied = `Res2`, alpha >= .9, a glow wider than the line; unsatisfied = the dormant grey, darker than gold, no glow, narrower; dashed iff Any-of).
- `ScreenTypeDressingTests` new, 1: `EveryFullScreenMenuCarriesItsArtAndTheStreakAndEveryOverlayTheDimAndNoStreak` (the play options, network select, picker, list and grid carry the sub-screen art covering the screen, credits and the three loading screens the `bg0` field, all of them the streak; the level and match results, Game Over and the restart choice a covering .70 / .82 dim and no streak; the drill result's dim under its card, no streak). The session and the tree pause are restored in `finally`.
- `DrillSceneSmokeTests` 4 -> 5: new `TheListShowsEachDrillsLastResultAtItsRowsRight` (pass = `check` in `Hp`, fail = `cross` in `Ink2`, a later result replaces an earlier one, nothing for an unfinished drill, the glyph right of the chevron after layout). The record is forgotten in `finally`.
- No existing case changed. `MatchResultsPulpTests`, `LoadingRoundelTests`, `ResonanceChipTests`' two older cases, `ResonanceGridFitTests` and the TG-SCREENS methods of `UrPolishTests` / `UrPolish2Tests` pass unchanged (the chip boxes keep their margins; the winner's star, the loser's dimming and the roundel frames are kept).

## Runs (this worktree, the lock held for every launch)

- `dotnet build`: 0 errors, the two known warnings (`CS8632`, `KLSG_ECS004`). Headless `--quit` import check: exit 0, no error.
- UI filter (plan 3.2, plus `FullyQualifiedName~ScreenType`) together with the neighbours that build these screens (`StoryLevelControllerBase`, `CompletionOnlyBanking`, `CampaignCompletion`, `EpilogueFlow`, `FighterFlowRoutes`, `HubDifficultyLowering`, `PauseFocusLoss`, `BootSequence`, `StoryHealingLoop`, `SceneSmoke`, `MenuSceneContent`, `GameOver`, `NetworkSelect`, `FighterPlayOptions`, `Credits`, `HubArrival`, `ActIII`, `AnchorSnap`, `CollapseBeat`, `TimelineCollapse`): **827 / 827** at the merge with `main` (`2d2cb592`); **819 / 819** at `c049be97` before it; the UI filter alone **725 / 725** after the first round of capture fixes. The plan's screens filter (`Results`, `Loading`, `Resonance`, `Timeline`, `Smothered`, `Drill`, `Calibration`, `Credits`, `GameOver`, `UiChrome`) is inside these runs: 192 of those cases plus `CampaignCompletionTests` 13, all green.
- `.import` and `default_bus_layout.tres` line-ending churn reverted, not committed.

## Fail evidence (every new case seen failing)

- `ResonanceChipTests.TheSatisfiedEdgeIsGoldAndTheOtherEdgesKeepTheirMeaning` against the seams returning the old values: "a satisfied edge draws (0.3, 0.95, 0.92, 0.95), not Resonance gold (1, 0.9411765, 0.6901961, 1)".
- `ScreenTypeDressingTests` against the pre-dressing screens: 19 issues, every surface named ("FighterPlayOptions: no SubmenuArt covering the screen | ... | Credits: no screen streak | LoadingGeneric: no screen streak | ... | LevelResults: no (0.0118, 0.0196, 0.0549, 0.7) dim covering the screen | ... | GameOver: no (... 0.82) dim ... | HolodeckDrill: the result card has no .70 dim").
- `DrillSceneSmokeTests.TheListShowsEachDrillsLastResultAtItsRowsRight` against the record with no glyph drawn: "row 0 shows no Passed glyph | row 1 shows no Passed glyph | row 2 shows no Failed glyph"; and with the glyph planted inside the chevron: "row 0's glyph sits at x 504, inside its 668 px chevron, not at its right" (rows 0-2). Both probes reverted.
- Logs: `D:\Projects\ftt-scratch\tg-screens\before_new.log`, `before_glyph.log`, `probe_glyph_pos.log`.

## Captures

`D:\Projects\ftt-scratch\tg-captures\screens\` (31 PNGs and `index.txt`, the windowed console build under the lock, on the
merged tree), every target of `UiCaptureTargets.Screens.cs` at 1.0 and the scaling ones at 1.4; `pass1\` and `pass2\` keep the
two review passes. No drawn reference exists for any of these screens (9.2), so the components were compared with the spec
crops through `compare_capture.py` (`compare\`):

| Capture | Crop | Result |
|---|---|---|
| `timeline_restart`, `game_over`, `drill_result` | `pause_panel__from_03_640_66` | The slab (chamfer, gold hairline, sheen streaks), the chevron rows, the diamond markers and the focused row (gold fill, white outline, glow, badge) match the pause panel. Differences: the restart rows wrap to two lines (the no-trim rule; the pause rows are one line), the row gap is the panel's 14 against the mock's 9, the keyboard key-cap is wider than the pad ring (known). |
| `drill_list`, `fighter_play_options` | `menu_button_column__from_01_86_505` | Row height 68, pitch 80 against 79, marker and label offsets, the focused slide and badge match. |
| `drill_picker` | `select_tile_grid__from_02_688_186` | Tiles, chamfers, art crop, names and the focus edge match the select grid at pitch 180; the picker has no Random tile and no owner badges (one player). |
| `drill_picker` | `select_title__from_02_270_25`, `menu_prompt_bar__from_01_1580_995`, `select_footer__from_02_492_866` | The heading and its flourishes match the select title (about 8 px lower: the heading's top is at 40, 9.1); the prompt bar is the kit's (key-caps on keyboard); the footer's compact chevrons match, a little wider (D6 at full label size where the mock shrinks "SYSTEMS CARD" into 290). |

Looked at without a crop: the results rows and total, the draw, the 1.4 match row (it spans 36-1895 px once the fit scales
the block), the three loading screens at both scales, the credits title (1365 px at 1.0, 1816 at 1.4), the grid at both
scales (its slab ends 4 px above the prompt bar at 1.4: the built fit), the picker and list at 1.4 (the hint clear of the
heading), the result glyphs, the drill HUD, the pass / fail / 1.4 cards, the play options and the network select.

## Deviations and why

1. **The drill HUD stays at the bottom centre** (9.2 says top-centre): the Fighter HUD it runs under holds the top band (HUD-F2), and a 900 px slab at the top would cover the timer and the inner ends of both HP bars.
2. **The Fighter play options keep a centred slab** (9.2: the MENU-A left column at (96, 520) and a notice slab on the right): `MenuLayoutBoundsTests` sweeps this screen as a centred panel, and its allow-list and `FighterFlowRoutesTests`' bound `Center/Panel/Layout/*` paths are not mine. The heading is centred to match. A ruling and an allow-list entry would let the left column in.
3. **The match winner line stays `HeadingLabel`** (9.2: T40, 48 px): `FighterMatchPresentationTests` pins the outcome label's role. A draw shows one "Draw" in `Ink`; the slate stamp, which only repeated it, is hidden (its colour override is still set, as the test reads).
4. **The credits title keeps one line and widens the column** past 1100 when it is wider (1365 px at 1.0, 1816 of 1920 at 1.4): `TitleGradient` spans one line box, so a second line drew flat in the gradient's last colour. A longer title or translation would run off the screen at 1.4; the real fix is the kit's (kit gap 2).
5. **Overlay panels fade in without the 24 px rise**: `MenuLayoutBoundsTests` measures the results, Game Over and restart panels centred a few frames after they open, and a panel caught mid-rise reads as off-centre.
6. **The list's pass / fail glyphs read a record I added** (`CalibrationDrillList.NoteOutcome`, a static, process-lifetime, never persisted, fed by the runner's outcome site): no drill result was kept anywhere, and `SessionData` is Core's. +1 test.
7. **Stack caps and the reserves follow the UI scale** (`ScaledStackCap`, `ScaledHeadingReserve`, `ScaledOverlayPromptClearance`): with the kit's unscaled 680 cap the restart rows broke over three and four lines at 1.4; with fixed reserves the picker's hint touched its heading and the results slab sat on the prompt bar. Minimum widths stay unscaled.
8. **The restart rows wrap** (`GrowToFit`) at 1.0 too: "Restart Level from Beginning" needs about 900 px on one line, over the 680 cap. The orchestrator asked for no compensating widths; this is the confirm modal's built no-trim rule.
9. **The drill HUD slab hides while the result card shows**, so the result's bottom-centre prompt bar does not sit on it.
10. **The picker block scales to about 0.97 at 1.0**: hint, 3 x 3 tiles at pitch 180, preview and footer are 856 px against the 832 left under the heading reserve. Invisible in practice; a shorter hint or a smaller preview would remove it.
11. **The results' Return chevron wraps at 1.4** ("RETURN TO / TIME-SHIP"), the no-trim rule again.

## Kit gaps (for the closeout's fold)

1. `GlassScreenDressing` itself: the screen heading for full-screen menus, the bottom-centre overlay prompt bar, the rise-less fade, `FlankTitle` (flourishes around an existing label), `ResultRow` (caption / value over a bound line), the scene-halo copy and the scaled reserves and cap belong in `PulpScreenKit`.
2. `TitleGradient` spans one line box; a wrapped title draws its second line flat (deviation 4).
3. `ApplyStackWidth` leaves its cap unscaled (deviation 7).
4. No 30 px Cinzel 700 role for the credits' role headings (9.2 says 30; `HeadingLabel` is 26) and no T40 role on the match outcome (deviation 3).
5. A `StyleBoxFlat` glow that does not draw its centre leaves the shadow's inside empty; the hourglass glow now draws its (transparent) centre. Worth a line in the kit's glow notes.

## Not seen

Motion in flight (fade-ins, the light band and the hourglass breath only at rest), Reduced Temporal Effects, pad glyphs,
locales other than English, a real level completion, match end, drill attempt or collapse (all captures are hand-built
states), the grid with a Warden Beacon behind it, the network select connected.

## For the orchestrator

1. **Fighter play options**: allow the MENU-A left column (a `MenuLayoutBoundsTests` allow-list entry and new paths), or keep the centred slab (deviation 2)?
2. **The drill HUD**: keep it at the bottom centre under the Fighter HUD (deviation 1)?
3. **The match winner line**: re-pin `FighterMatchPresentationTests` to a T40-sized role, or keep `HeadingLabel` (deviation 3)?
4. **The drill result record** (deviation 6): keep it in the list's static, or move it to `SessionData`?
5. **Stale copy**: `drill_list_hint` (`localization/en.csv` line 1268) still says "Six short lessons"; there are five drills since FU-ECHO (2026-10-25). Not changed (outside my files, no new copy); also the test name `CalibrationDrillsRouteTests.TheDrillListBuildsTheCatalogsSixDrillsAndIsFullyFocusAuthored`.
6. **Credits title** at 1.4 or in a longer locale: kit gap 2, or a non-scaling display role (the main menu's title lock-up never grows above 100 %, 6.7)?
7. **The 1.4 match button row** spans almost the full width (four D6 chevrons at "CHANGE FIGHTERS"'s width); per-button widths in a horizontal row would be narrower, but 3.2 says one width per stack.

## Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the UI section's redesign bullet, the screens sentence: "Since the 2026-10-27 Temporal Glass rebuild (TG-SCREENS) the remaining screens follow 9.1's two types through `GlassScreenDressing`: full-screen menus (the Fighter play options, the network select, the Calibration picker and list, the Resonance grid, credits and the three loading screens) carry the sub-screen art or `bg0`, the screen streak, a T03 heading with flourishes and the prompt bar bottom-right; overlays (the level and match results, Game Over, the restart choice, the drill result) carry the .70 dim (Game Over .82), one glass slab kept clear of the bottom-centre prompt bar and no streak. The Resonance grid's satisfied edge is gold (R10). The drill list shows each drill's last result this session (`CalibrationDrillList.NoteOutcome`, never persisted)."
- Ledger, under the glass section: `PROVISIONAL-UI-GLASS-SCREENS` (deviations 1-11, the numbers `HeadingReserve` 100, `OverlayPromptClearance` 56, the result glyph 28 / 16, the hourglass glow .35 / 22, the grid scrim .55, the VS column top 345) and `VERIFY-DRILL-LIST-HINT-COUNT` (question 5).
