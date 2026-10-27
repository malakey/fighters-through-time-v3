# TG-MENU handoff: MENU-A and the main-menu sub-screens

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.7, Phase B group 1). Look authority:
`UI_SPEC_GLASS.md` 3.8, 4.1, 9.1, 9.2 and 10.2 (section numbers below are its own); rulings R1, R2, R3 and R5. Branch
`tg/menu`, worktree `D:\Projects\ftt-wt\tg-menu`, cut from `main` at `aec29b3b` (TG-WIDGETS step 1 and its ruling-5
follow-up, 3711 green). Code commit `6452876a`; this handoff in the next commit.

## What shipped

| File | Change |
|---|---|
| `scenes/menus/MainMenu.tscn` | Rewritten as MENU-A. `Background` (bg0) holds `TemporalMainArt` (`temporal_glass_main_menu_background.png`) and `TemporalSubmenuArt` (`temporal_glass_submenu_background.png`), both covering. `RootScreen/TitleBlock` at (110, 64) holds `Title` (T01, `menu_cover_title_word`), `Middle` (T02) and `Last` (T01). The root column (`RootScreen/Center/Panel`, a clear plate anchored bottom-left at left 96, bottom 983) keeps its eight buttons and `NoticeSlot`. `RootScreen/ContinueCard` (a flat focusable `Button`) holds `Plate` (`GlassPanelContinue`) / `Row` / `Lines` (`Hero` T45, `Place` T46, `Meta`: `Difficulty`, `Dot`, `Progress` T47) and `BadgeRoom`. The six sub-screens are `<Screen>/Center/Panel/Layout/...` glass slabs (below). `VersionLabel` is T15 at left 110, bottom 38; `PromptBar` is the kit's bar at right 70, bottom 44. No font-size override, no texture plate, no pulp art, no cyan. |
| `scripts/UI/MainMenu.Cover.cs` | Rewritten as the MENU-A root (the file keeps its pulp-era name). The art and the kit's menu scrim (`PulpScreenKit.AddMenuScrim`, root only); the title lock-up (a `GlassFlourish` row, `TitleGradient` on "FIGHTERS" and "TIME", the "THROUGH" rule) from R2's split (`SplitBanner`); the bottom-anchored column with the 4.1 fit sequence (`ComputeRootFit`, `ColumnCapAt`); the D6 stack width (`PulpButtonDecor.ApplyStackWidth`, 560 to 700 x s, and clear of the card); the Continue card (R5) with its focus frame, A prompt, sideways links and press; the prompt bar; the screen streak; one deferred, coalesced layout pass on a UI-scale, locale or size change. Seams kept: `SaveSlotsOverrideForTesting`, `ResumeOverrideForTesting`. Test surfaces: `ContinueCard`, `ContinueSlot`, `LastRootFit`, `MetaSeparator` and the geometry constants. |
| `scripts/UI/MainMenu.SubScreens.cs` (new partial of `MainMenu`) | The six sub-screens (9.2): every slab dressed by `PulpScreenKit.DressPanel` (sheen, tag, 14 px inset) under `MenuSafeAreaFit`; the panel title between fitted flourishes (`MainMenu.HeadingFlourishes`); Back a compact chevron without a prompt; the slot slabs (portrait frame, name, summary lines, a dashed empty slab, the focus frame `MainMenu.SlabFrame`); the glass character tiles (`GlassTileDecor` with the portrait, the card name fitted by `CardTextFit`) and the hero side slab (portrait frame 112, the D10 name fit, the style as the tag); the difficulty description under the column; the level grid's shared width; every size x the UI scale. Test surfaces: `ShownHeroID`, `SlabFrame` (`Mode`, `OutlineColors`, `GeometryBuilds`), `HeadingFlourishes.NodeName`. |
| `scripts/UI/MainMenu.cs` | Glue only: the slot rows paint their slabs (`Text` keeps the whole summary); the character grid is read from `Layout/Body/Grid`; the difficulty rows are their name keys and the focused row's description shows under the column; level tiles read "NN  short place"; `ShowScreen` switches the art and the scrim and refreshes the card at the root; the save notice takes `HintLine`; `CharacterDataFor` is shared with the card. The pulp pitch rule (`ApplyRootRowSeparation`) is gone. |
| `scripts/UI/MainMenuContinue.cs` | `DifficultyKey(Difficulty)` (shared by the card and the slot summary). `PickMostRecent`, `ChapterNumber`, `PlaceKey` and `ShortPlace` unchanged. |
| Deleted | `scripts/UI/MainMenuCoverInk.cs`, `tests/Unit/MainMenuCoverInkTests.cs` (each with its `.uid`). |
| Captures | `scripts/Diagnostics/UiCapture/UiCaptureTargets.Menu.cs` rewritten: UR-MENU's ten target names kept (so the pulp gallery pairs 1:1) plus `menu_cover_rec01` (rec_01's state: Einstein's save, the release rows, pad prompts), `menu_slots_140`, `menu_character_140`. Every seam a target sets is cleared when its menu leaves the tree. |
| Not touched | `ThirdPartyLicenses.cs` (its rows stay flat buttons on the licences slab); `localization/en.csv` (no new copy); `MainMenuExtrasTests`, `CalibrationDrillsRouteTests`, `MainMenuContinueRulesTests` pass unchanged. No cross-edit outside the ownership matrix. |

### The root in numbers (design pixels at UI scale 1.0)

- **Title (R2, R3):** line 1 is the raw key `menu_cover_title_word`; lines 2 and 3 are `menu_cover_title_banner` split at its first space (trimmed; no space gives one middle line, an empty value hides both). The lock-up is 300.56 tall with three lines and no tagline: the 11 px flourish row (rule-diamond-rule, 700 wide), 22 px, "FIGHTERS" on a 105.3 line box, "THROUGH" on 57 (indented 6, its rule starting 22 px after the word and ending at 700), "TIME" on 105.3. Each word is centred on its line box, so the gradient's `line_top` / `line_height` stay the kit's. The labels are scaled as nodes (never a font size): the lock-up never grows above 100 % and follows the UI scale below 1.0; the rules are laid on whole pixels.
- **Column:** the clear plate stands on y 983, rows 68 at gap 11, the D6 width capped at 700 x s and at whatever keeps the focused row (its 14 x s slide included) 24 px left of the card (870.4 at 1.4). A row is never shorter than its own text line and paddings (69 px at 1.0, 88 at 1.4), so the fit reads the real height.
- **Fit (4.1):** the first configuration that keeps the column 24 px under the title wins, the title as large as it allows: (0) as authored; (1) the title down to 0.7; (2) the gap to 6; (3) the rows to 58; (4) the title down to 0.5; (5) the column scales about its bottom-left (floor 0.5). At 1.0: 7 release rows need nothing, the 8 debug rows put the title at 0.885 (step 1). At 1.4: 7 rows reach step 2 (title 0.76), 8 rows step 5 (title 0.5, column 0.976).
- **Continue card (R5):** 600 wide, its height its content's (the crop's 600 x 118 at 1.0), 70 / 120 from the bottom-right corner, scaling about that corner; tag `boot_continue`; an 86 px portrait frame; the hero's `character_<id>_name` (T45, upper case); `ShortPlace(PlaceKey)` (T46); "difficulty · chapter" (T47, `difficulty_*` and `menu_continue_chapter`), or "difficulty · `save_completed`" for a completed save. Hidden with no save. Focusable but outside the vertical chain: Right from Story Mode reaches it, Left (and Tab / Shift+Tab) return, Up, Down and Right hold. Focused it draws the button focus edge along its hairline with the 14 px glow and the 36 px A prompt at its top-right inside the padding; it does not slide; hover takes focus; pressing it resumes the most recent save through `StoryManager.ResumeCampaign`.
- **Version, prompts, streak:** T15 at (110, bottom 38); the prompt bar pairs `ui_accept` / `ui_prompt_select` and `ui_cancel` / `common_back` at right 70, bottom 44; the screen streak over every menu screen.

### The sub-screens (9.2)

| Screen | What it is |
|---|---|
| Slots | Three focusable glass slabs 1000 x 140 (height x s): portrait frame 86, the hero's name (T45), the summary's lines (T46 / T47, `save_slot_lowest_difficulty` among them), each with a compact Delete. An empty slot is a quiet slab (`ButtonFillDisabled`, no sheen) with the dashed `EdgeDim` edge and `save_slot_empty` in ink2. Focus: the focus edge and glow. `SlotRow{0-2}/SlotButton|DeleteButton` kept; the button's own glyphs are transparent and its `Text` keeps the whole summary. |
| Character | The 3 x 3 glass tiles (164 x 164, portrait window, card name T35 fitted to 156 px, white when focused) beside a side slab (min 440 wide) for the focused hero: portrait frame 112, the name in T40 with the D10 fit (one line, else shrink to 36 / 48, else two lines), the style as the tag (`movelist_style_*`). The grid moved into `Layout/Body`. |
| Difficulty | Three chevron rows (the difficulty name; D6 width) and the focused row's `difficulty_*_description` under the column (two lines reserved). |
| Level select (debug) | A 4 x 4 grid of compact chevrons, "NN  short place", sharing their widest label's width (at least 290, at most what keeps four columns inside the safe area); a label still too long shrinks, then ellipsizes. |
| Extras | A chevron column (Credits, Third-Party Licenses). |
| Licences | One slab: the intro, the scrolling rows. |

Every slab: centred, `MenuSafeAreaFit`, the sheen, the tag (`menu_story_mode`, `menu_level_select` or `game_title`, the keys the pulp tags used), the panel title (Cinzel 900 36, upper case) between flourishes that shorten to the room and hide below 32 px of rule, chevron rows with the BTN-A decor, and a compact Back without a prompt. The sub-screens show the submenu art and the streak, no scrim (their text sits on slabs).

### Node paths

Kept: the eight root buttons and `NoticeSlot` under `RootScreen/Center/Panel/Layout`, `RootScreen/TitleBlock/Title`,
every `<Screen>/Center/Panel/Layout/...` node the script and tests bind (titles, Back buttons, the slot rows, the level
grid, the extras and licences nodes), `VersionLabel`, `PromptBar`. Changed on purpose, each with its test change:

- `RootScreen/TitleBlock/Banner/BannerLabel` -> `RootScreen/TitleBlock/Middle` and `Last` (R2's three lines). `MainMenuSceneTests`' static-copy case asserts "Through" and "Time".
- `CharacterScreen/Center/Panel/Layout/Grid` -> `.../Layout/Body/Grid` (the tiles beside the hero slab). `MainMenuSceneTests`' `CharacterLayout` constant (six uses in five cases).
- `RootScreen/ContinueSticker` (`Visual/Star/Line1-3`) -> `RootScreen/ContinueCard` (`Plate/Row/{Portrait, Lines/{Hero, Place, Meta/{Difficulty, Dot, Progress}}, BadgeRoom}`, `FocusFrame`, `PromptBadge`). `MainMenuCoverTests`, rewritten.
- Removed with the pulp cover: `Background/CoverArt|CoverRays|CoverHalftone|CoverBurst|SubScrim`, `TitleBlock/Extrude1-7`, `RootScreen/Heroes`, each sub-screen's pulp tag (`Panel/TagHolder`, `Layout/TagClearance`; the kit's `PanelTagAnchor` replaces it) and the character screen's two separators. Nothing outside the menu bound them (the UI filter and the extra suites below pass).

## Test delta

`[TestCase]` attribute count 3711 -> **3710** (-1, the plan's -1).

- `MainMenuCoverTests` 11 -> 12 (+1). Rewritten: `TheStickerIsHiddenWithNoSaveAndOutOfTheFocusChain` -> `TheContinueCardIsHiddenWithNoSaveAndOutOfTheFocusChain`; `TheStickerShowsTheChapterAndPlaceOfTheMostRecentSaveAndJoinsTheFocusChain` -> `TheContinueCardShowsTheHeroPlaceAndMetaOfTheMostRecentSave` (the card stays out of the vertical chain); `PressingTheStickerResumes...` -> `PressingTheContinueCardResumesTheMostRecentSaveThroughResumeCampaign`; `ACompletedSaveShowsTheCompletionLine...` -> `ACompletedSaveShowsCampaignCompleteAndAnUnknownScenePathReadsAsTheTutorial` (with `DifficultyKey`); `TheTitleIsTwoLiveKeysAndTheBannerHidesWhenItsKeyIsEmpty` -> `TheTitleIsThreeLinesFromTwoKeysWithTheGradientOnBothTitleWords` (the split rule, one material per title word, the line centres, the flourish and the rule at 1.0); `TheRootColumnTightensItsPitchSoEightRowsEndAboveThePromptBar` -> `TheBottomAnchoredColumnKeepsTwentyFourPixelsUnderTheTitleWithSixSevenAndEightRows` (the pure fit at 1.0 and 1.4, the natural row height, the width cap, and the live menu with eight rows and a save at 1.0 and 1.4). Updated: `EveryRootButtonIsAPulpBannerWithTheDecorAndALeftAlignedLabel` -> `EveryRootButtonIsAGlassChevronWithTheDecorSharingTheD6Width`; `EverySubScreenBannerButtonCarriesTheDecor` -> `EverySubScreenIsAGlassSlabWithItsTagTitleFlourishesAndDecoratedChevrons` (sheen, tag, flourishes, decor, slot slabs, tiles, hero slab, level grid, the side slab following focus; it shows the character screen directly, because pressing a slot row would read this machine's saves); `ThePromptBarPairsSelectAndBackAndSitsBottomRight` -> `...AtSeventyAndFortyFour` (and the version line); `TheSceneCarriesNoPerButtonTextureOrFontSizeOverrideAndNoChromeCyan` -> `TheSceneAndScriptsCarryNoPulpArtNoFontSizeOverrideAndNoChromeCyan` (the glass arts present; no retired pulp variation or role named in the scene or the three scripts, word-bounded). Deleted: `TheThreeHeroesAreInkedPosesStandingOnTheFloorLine`. New: `TheContinueCardIsReachedWithRightFromStoryModeAndLeftReturns`, `TheMenuArtCarriesTheScrimAndTheScreenStreak`.
- `MainMenuCoverInkTests` deleted (-2) with `MainMenuCoverInk.cs`.
- In place, no count change: `MainMenuSceneTests` (the title paths; `CharacterLayout`). `CalibrationDrillsRouteTests`, `MainMenuExtrasTests` and `MainMenuContinueRulesTests` needed no change.
- 3711 + 1 - 2 = **3710**.

## Runs (this worktree, the lock held for every launch)

- `dotnet build`: 0 errors, the two known warnings. Headless `--import`: exit 0, no error (it wrote `MainMenu.SubScreens.cs.uid`).
- UI filter (plan 3.2): **722 / 722** (723 at `aec29b3b`, less one). It contains the plan's menu filter (`MainMenu` 43, `MenuLayoutBounds`, `CalibrationDrillsRoute`, `PadMenu`: 79). The first menu-filter run read 77 / 79, both failures in my own tests (a GdUnit `AssertThat` on a nested enum, and the retired-name scan matching "SlotPlate" inside a method name; the scan is now word-bounded and the method renamed).
- The suites outside the UI filter that build the main menu (`SceneSmoke`, `MenuSceneContent`, `EpilogueFlow`, `CampaignCompletion`, `Level15Content`, `BootSequence`, `HubDifficultyLowering`, `FighterPlayOptions`, `NetworkSelect`, `PauseFocusLoss`, `FighterFlowRoutes`, `ScriptTypedEmptyArray`, `SceneAssignedResourcePin`, `CampaignLocalization`): **81 / 81**.
- After merging `main` (`Already up to date` at `aec29b3b`), one launch of the UI filter and those suites together: **790 / 790** (722 + 81, 13 cases in both; the menu filter's 79 and the `MainMenu` filter's 43 among them, from the TRX).
- `.import` and `default_bus_layout.tres` line-ending churn (629 files after each launch) reverted, never committed. GdUnit's orphan-node warning appeared on some runs; it is not a failure.

## Fail evidence (every new or rewritten case seen failing)

Two mutation runs of the `MainMenu` filter (43 cases), each mutation reverted afterwards with `git checkout` of the touched
files (the tree clean against `6452876a`, rebuilt). Script `D:\Projects\ftt-scratch\tg-menu\mutate.py`, logs `mutA.log`,
`mutB.log` beside it.

- **Run A** (the card shown with no save; no sideways links; the press does nothing; the banner split at its last space; the prompt bar 40 from the bottom; the fit ignoring the rows' natural height; a script naming `SlotPlate`; `MainMenuSceneTests`' `CharacterLayout` back to the pre-TG-MENU path): **12 failed, 31 passed**, each at its own assertion: card `Visible` "Expecting False but is True"; resumed slot "'0' but is '-1'"; "Right from Story Mode misses the card"; the split `"Durch|die Zeit"` but `"Durch  die|Zeit"`; `OffsetBottom` "'-44' but is '-40'"; `RowPixels` "'88' but is '81.2'"; "MainMenu.SubScreens.cs names the retired SlotPlate"; and the five `MainMenuSceneTests` cases that use the grid path (a null node: the updated path is load-bearing).
- **Run B** (the place as the whole mission name; a completed save showing its chapter; no 700 cap; the scrim kept on the sub-screens; no heading flourishes; the banner's words swapped): **8 failed, 35 passed**: "Pompeii 79 AD" but "Pompeii 79 AD — Eruption of Vesuvius"; "Campaign Complete" but "Chapter 15"; "the stack is 835 wide"; "The sub-screen's art is wrong"; "<each screen>'s title has no flourishes"; `MainMenuSceneTests.StaticCopy...` "Through" but "Time". Two were collateral of the same mutations: the title case (the swap) and the fit case's live half ("live at 1.4: the column 1214.8 crowds the card 1010", the missing cap).

## Captures

`D:\Projects\ftt-scratch\tg-captures\menu\`, the windowed console build under the lock, 13 targets: `menu_cover` (a first
boot, no card), `menu_cover_continue`, `menu_cover_debug` (eight rows, the card focused), `menu_cover_rec01`,
`menu_cover_scale14` (1.4, eight rows), `menu_slots`, `menu_slots_140`, `menu_character`, `menu_character_140`,
`menu_difficulty`, `menu_level_select`, `menu_extras`, `menu_licenses`. Earlier drafts are in `draft1\` and `draft2\`.
Compared with `py tools/ui_theme/compare_capture.py` (the `__vs__` side-by-sides, overlays and diffs sit beside them):

| Capture | Reference | Result |
|---|---|---|
| `menu_cover_rec01` | `rec_01_main-menu` | Art, title lock-up, card, prompt bar and version coincide; the column stands on the same left and bottom. Differences are rulings and data: no "TEMPORAL RESONANCE" line (R3); seven rows with Quick Play (R1), so the column is 700 wide (Quick Play's label shrinks to fit) where the mock draws 560; the card's copy (R5: no "· SLOT 1", no "Level 6 —", no playtime or "Saved 2 min ago"); the real version string. |
| `menu_cover_rec01` | `menu_title_lockup__from_01_100_50` | The flourish, "FIGHTERS" with the gradient, "THROUGH" with its rule and "TIME" overlay to within a pixel; the mock's tagline is absent (R3). |
| `menu_cover_rec01` | `menu_button_column__from_01_86_505` | Same marker, label x, fills, focus outline, glow and badge. The build's rows are 69 tall (their Cinzel line), pitch 80 against 79, so with the bottom fixed at 983 Fighter Mode to Quit Game sit within 1-5 px of the mock and Story Mode 6 px higher; the 700 width puts the focused row's A badge outside the crop. |
| `menu_cover_rec01` | `menu_continue_card__from_01_1238_818` | The card coincides (600 x 118 at (1250, 842)): tag, frame, portrait, name and lines. The slab reads a little darker (the mock's backdrop blur, 1.4) and the lines are R5's. |
| `menu_cover_rec01` | `menu_prompt_bar__from_01_1580_995` | Identical position, rings and captions. |
| `menu_cover_scale14` | `rec_01_main-menu` | The 4.1 fit at step 5: title 0.5, rows 88, the column (870 wide, the 1.4 cap) scaled 0.976 about its bottom-left, the card 840 wide on its corner. |
| `menu_cover` | `rec_01_main-menu` | A first boot: no card; otherwise as above with key-caps. |

The sub-screens have no mock or crop; each was checked against 9.1 / 9.2: slab, tag, sheen, the flourished title,
chevrons and the compact Back on the submenu art with the streak and the prompt bar. At 1.0 everything fits; the slot
summary's Orléans line runs long but whole. At 1.4: the slots fit (the slab stays 1000 wide; that summary line
ellipsizes), the character tiles' names shrink to fit and the hero's name wraps to two lines, and the character and
difficulty titles leave no room for flourishes, so they hide. The level grid ellipsizes four long names at 1.0
("CHRONAL INTEGRAT…", "GETTYSBURG, JULY 1…", "THE SEVERED BASTI…", "THE MERIDIAN FOUN…").

## Deviations and why

1. **The sub-screen heading is the slab's panel title, with flourishes.** 9.1 puts a T03 screen heading with flourishes top-centre at y 40, and 9.2 says "the same title block position as MENU-A". The title node is `<Screen>/Center/Panel/Layout/Title`, bound by `MainMenuSceneTests` (`save_select_title`), the plan wants each sub-screen's largest panel centred (`MenuLayoutBoundsTests`), and the brief keeps every bound path. So the title stays in the slab at 9.1's panel-title size (Cinzel 900 36) and carries the screen heading's flourishes, which shorten to the room and hide below 32 px. Moving it out means a new screen-level node and a test change: the orchestrator's call.
2. **Sub-screens show the submenu art and no scrim.** 9.1 names `temporal_glass_submenu_background.png` for full-screen menus and a scrim "where text sits on art"; their text sits on slabs. The root keeps the main art under the menu scrim.
3. **The column is 700 wide, not the mock's 560**, because R1 keeps Quick Play and D6 sizes the stack by its widest label (capped at 700, the label shrinking to fit). TG-WIDGETS reported the same.
4. **Rows are 69 px, pitch 80**: a button is never shorter than its Cinzel 34 line plus paddings. The fit uses that real height (so 1.4 with eight rows reaches step 5, where the spec's arithmetic says step 4).
5. **The title measures 300.56 with no tagline (R3)**, so the spec's 1.4 example (six rows, title 0.735) reads 0.82 here.
6. **The streak is z 0, not the kit's 10**, so the confirm modals the menu adds later cover it ("overlays never show the screen streak", 9.1).
7. **Card focus:** Up, Down and Right hold on the card, and Tab / Shift+Tab from it return to Story Mode. Only Story Mode links to it; the other root rows have no explicit Right, so Godot's geometric search can reach the card from them too (Left then returns to Story Mode).
8. **`ColumnCapAt` reserves the 14 px slide x s** (870.4 at 1.4, the spec's "870").
9. **Level tiles show the short place** ("01  Florence 1503"), not "NN  title", because the 4 x 4 compact grid has no room for the subtitles.
10. **The slot slab stays 1000 wide at 1.4** (its height, gaps, paddings and fonts scale): 1400 plus the Delete column does not fit 1920.
11. **The completed-save meta is "difficulty · Campaign Complete"** (R5 names `save_completed` in place of the chapter; the difficulty stays).
12. **Hover is focus** on the card, the slot slabs and the tiles, as the kit's chevrons do (AC8).
13. **The difficulty rows are one line (the name)**; the description moved under the column (9.2). It used to be the row's second line.
14. **A new file**, `MainMenu.SubScreens.cs`: a partial of `MainMenu`, so inside the matrix's `MainMenu.cs` row.

## Kit gaps

1. **No Label role for 9.2's difficulty description** (Outfit 400 24 ink2). It uses `BodySmallLabel` (Outfit 400 22) with an `Ink2` colour override; the generator could add the role.
2. **No disabled (empty) slab variation**: the empty slot's quiet fill (`ButtonFillDisabled`, chamfer 18) is a scene-local `StyleBoxFlat` built in code.
3. **No kit widget for a focusable slab's focus edge and dashed edge**: `MainMenu.SlabFrame` draws the button focus edge along a slab's chamfer, the 14 px glow and the 6 / 4 dashed `EdgeDim` edge, with cached geometry. TG-SELECT or TG-SCREENS may want it in `scripts/UI/Pulp/`.
4. **`PulpScreenKit.AddFlourishHeading` builds a new label in a new row with fixed 240 px rules**, so it cannot dress an existing, path-bound title inside a narrow slab. `MainMenu.HeadingFlourishes` attaches to the existing label and fits the rules to the room.

## Provisional values

The card's row gap 18, line gap 2, meta gap 16 and badge room (52 - 18) x s; the slot slab's row gap 18 and the empty
slab's fill; the hero slab's minimum width 440 and the D10 floor 0.75; the tile name box 156 at 11 from the bottom; the
flourishes' 32 px minimum rule; the level tiles' 34 / 14 gaps; the description's two-line reserve; the version line at
bottom 38. Each is the closest reading of the spec or the mock, none measured in play.

## Not seen

A pad or any device in hand (pad prompts only through the capture seam), motion in flight (the card's pop-in, the
chevrons' slide), Reduced Temporal Effects, any locale but English, a save-load notice in the column, a real save on disk
(the captures use in-memory saves), the root at 1.4 with the release rows, the boot overlay over the new root.

## For the orchestrator

1. **The sub-screen heading** (deviation 1): keep the in-slab flourished title, or move to 9.1's screen heading at y 40 (a new node outside the slab, `MainMenuSceneTests`' title path changing)?
2. **R1 and D6**: with Quick Play in release builds the root column is 700 wide and Quick Play's label shrinks; `VERIFY-UI-QUICK-PLAY-ROW` stays open.
3. **The slot summary** repeats the hero's name (the slab's name line, then `save_slot_summary`'s "Slot 1: Joan of Arc — ..."), and `orleans_level_title`'s value is upper-case in `en.csv`, so that line shouts. Both are existing copy; R5-style trimming would need a ruling.
4. **Kit gaps 1-4**: generator role and slab variation (TG-THEME), and whether `SlabFrame` / `HeadingFlourishes` should move into `scripts/UI/Pulp/`.

## Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the UI section's redesign bullet, replace "the cover title is two live keys (`menu_cover_title_word`, `menu_cover_title_banner`); the Continue sticker resumes the most recent save;" with: "the main menu is MENU-A (TG-MENU): the temporal-glass art under the menu scrim, the title lock-up at (110, 64) from two keys (`menu_cover_title_word`, and `menu_cover_title_banner` split at its first space, R2; no tagline, R3), the chevron column standing on y 983 with the 4.1 fit sequence (`MainMenu.ComputeRootFit`), and a focusable Continue card bottom-right (R5: hero, short place, difficulty · chapter; Right from Story Mode, Left back) that resumes the most recent save; its six sub-screens are centred glass slabs (`MainMenu.SubScreens.cs`);".
- Ledger, under the glass section: `PROVISIONAL-UI-GLASS-MENU` (the provisional values above, deviations 4-13), a row for deviation 1 if the orchestrator keeps it, `VERIFY-UI-QUICK-PLAY-ROW` still open.
