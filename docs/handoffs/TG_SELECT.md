# TG-SELECT handoff: CARD-A, the stage phase and the Holodeck console

Temporal Glass rebuild (`docs/UI_GLASS_2026-10-26_PLAN.md` section 2.8). Look authority: `UI_SPEC_GLASS.md` 3.7, 4.2, 9.2,
10.2 and `rec_02`. Branch `tg/select`, worktree `D:\Projects\ftt-wt\tg-select`, cut from `main` at `f086f3e0` and
fast-forwarded to `67a891d9` (TG-MENU and TG-SCREENS, 3722) before any edit. Code commit `00b48dca`; `main` merged at
`4994867b` (TG-PAUSE, TG-HUDF, the TG-WIDGETS fix step and TG-HUDS, 3729) in `8e5edc00`, no conflict and no overlapping
file; this handoff in the next commit.

## What shipped

| File | Change |
|---|---|
| `scenes/menus/CharacterSelect.tscn` | Rebuilt on 4.2's anchor model. `Background/SelectArt` (`temporal_glass_fighter_select_background.png`) under `Background/Vignette` (`assets/sprites/ui/glass/select_vignette.tres`); `SelectPhase` and `StagePhase` are full-rect `Control`s (were `CenterContainer`s); `SelectPhase/HeroLayer` (new, before `Root`, so every text draws over the heroes); `SelectPhase/Root` a full-rect `Control` (was a `VBoxContainer`) holding `PlayersRow`, a full-rect `HBoxContainer` (`LeftMargin` 40, `P1Panel` 460, `GapLeft`, `Grid`, `GapRight`, `P2Panel` 460, `RightMargin` 40), so the grid is centred on the screen, then `Title`, `CountdownLabel`, `ButtonRow`, `HintLabel`, `RemotePlayNotice`, each placed from the top by `LayoutSelect`. `Grid` has 3 columns. The stage phase gains `StagePhase/Root/StageGrid` (6 columns, 16 / 24 apart) and the rules as settings rows (`RulesRow/ModeLabel`, `ItemsLabel` moved before `ItemFrequency`); Back and Fight are compact chevrons. |
| `scripts/UI/CardGridRules.cs` | Rewritten: `Columns` 3, the Random tile alone on the last row; `GridLayout` (tile, pitch, rows, top; gap, width, Random top, block height, bottom, compact) and `LayoutFor(roster)`: up to 3 rows the drawn layout (164 / 180 from y 196, Random 524 x 72, bottom 808); 4 rows and more keep the bottom on 808 and grow upward inside the band 112 to 808, at the compact 140 / 152 while it fits, then shrinking at the compact proportions, never under half the full pitch. `Step(cursor, dx, dy, roster)`: horizontal steps wrap within the cursor's row, vertical steps wrap through the roster rows and the Random row. `CardStatMaxima` kept. Nothing names a roster size. |
| `scripts/UI/CharacterSelectScreen.Cards.cs` | Rewritten. Tiles: a `Button` per manifest member plus the Random tile, each a `GlassTileDecor` (`SelectChamfer`, the portrait, `Owners` from both cursors, `LockedBy` for the lock mark) with a `CharacterName` label (T35, `character_<id>_card_name`, fitted to 156 px, 11 px from the bottom) and the hidden `TokenBadge` glyph label; the Random tile is "?" (T38) and the random word (T57) as one centred row 22 px apart, repositioned on the grid's `SortChildren` to span the grid. Side heads (built under `P{n}Panel`): `TopMargin`, `Head` (`Shape/SlotShape`, a `PulpIcon` in `UIPalette.OwnerOnDark(n)`, and `PlayerLabel`), `NameBox/P{n}Name` (T40, D10 fit), `BadgeGap`, `P{n}Ready` (`ReadyBadge` plate, its 18 px glow drawn by a `ReadyGlow` ring behind it, fading in over `CrossFadeSeconds`), `P{n}Thinking` (`ReadyBadgeIdle`, `fighter_select_choosing`), the join prompt, a spacer, `P{n}Card` (`GlassPanelStats` with the sheen and the style tag: `Col/P{n}Stats` hidden, `CpuNote`, `Rows/Row{0-2}/{Label,Bar,Value}` with stat tubes against `CardMaxima`, `Lenses/Cell{0-2}/{Lens,NameBox/AbilityName}` for Special 1, Special 2 and Movement), `BottomMargin`. `P{n}Hero` (`SelectHeroSprite`) in `HeroLayer`. `LayoutSelect` places the title row (centre y 68), the hero feet (260 / 1660, 700), and the bottom stack from the screen's foot up (notice, prompt bar, hint, footer; gaps 20, tightened toward 6 only when 1.4 would push the footer into the grid), the countdown between the Random tile and the footer or under the title when they leave no room; it runs deferred after a resize or a theme change. Chrome: `FlankTitle` on both titles, the halo S on the notice, the footer (P1: three compact chevrons 280 wide, 34 apart, no prompt badge, the label's right padding 30), the prompt bar, the screen streak. The stage phase: a glass tile per stage in the field's order (`Stage{id}`, `RandomStage` last; `Window/Preview` the plate, `StageName` T35 fitted, `LayoutTag` with its `stage_layout_*` tooltip), the selected one `Lit`; a click selects it; out of the focus chain. |
| `scripts/UI/CharacterSelectScreen.cs` | The cursor steps through `CardGridRules.Step`; entering the stage phase focuses Fight; `UpdateStagePreview` paints the stage tiles and keeps the single `StagePreview` hidden (still bound, it keeps the plate and its tooltip); the CPU panel draws above the streak (`ZIndex` 11); `GetCharacterPortrait` removed. |
| `scripts/UI/SelectHeroSprite.cs` (new, + `.uid`) | Draws TG-ART's baked idle frames (`select_heroes/<id>_idle_<n>.png`, catalog `hero_<id>_idle_<n>` and `hero_select_index`), never the roster `SpriteFrames`: scale 2.05, the frame's pivot and feet on `FeetPoint`, P2 mirrored, the idle at the catalog's 6 fps, over a soft shadow (nine black taps, 8 px down, 3 px apart, alpha .075). Textures cached per hero; no allocation per frame. Closes `VERIFY-UI-SELECT-HERO-SPRITES`. |
| `scripts/UI/CardTextFit.cs` | Kept `ScaleFor` / `TextWidth` / `Fit` (`ScaleFor` and `Fit` gain an optional `minScaleAtOne`, default 1, unchanged behaviour); added the D10 head rule (`HeroNameShrinkFloor` .75, `HeroNameFit`, `FitHeroName`: one line shrunk to .75 placed by its real width, else two lines measured untrimmed, shrinking to `HeroNameShrinkFloorAt(uiScale)` = .75 / scale), `FitWrappedName` (a word-wrapped name in a box: shrinks at a large UI scale until its widest word fits, never below its 1.0 size, with a cap so a row shares one size), `WidestWordWidth`, `StageNameShrinkFloor` .75. |
| `scripts/UI/HolodeckConsolePanel.cs` | One glass slab: the `PauseDim` shade, the untagged `PulpTaggedPanel`, `DressPanel` (sheen, the stack inset), the title inside (`TitleLabel`, upper-case), settings rows (label column 360 in `SettingsRowLabel`, 22 px, the control column 380 wide filling the row, 44 tall, 62 apart), Stocks in `RulesRow` and the timer in a new `TimerRow`, the toggles as rows in `TogglesRow` (now a `VBoxContainer`), the actions as pause chevrons (D6 560 / 680, Launch focused), the overlay prompt bar, the fade-in; `MenuSafeAreaFit` with the overlay prompt clearance. `TagClearance` removed. |
| `scripts/Diagnostics/UiCapture/UiCaptureTargets.Select.cs` | Rewritten: the six targets kept (`select_cards`, `select_ready`, `select_mirror`, `select_stage`, `select_holodeck`, `select_140`) plus `select_p1_ready` (the `rec_02` state), `select_join`, `select_versus_cpu`, `select_stage_140`, `select_holodeck_frontend`, `select_holodeck_140`, `select_ready_140` and one per player-slot palette. |
| `tests/ContentValidation/UnusedTranslationKeyTests.cs` | The `character_` prefix re-pinned to the card-name builder; `fighter_random_mystery` recorded as an orphan with its reason; the ceiling 12 -> 13. No `en.csv` row added or deleted. |

The pulp cards, bubbles, stars, paper cards, stamp, split art and halftone are gone. No `en.csv` change, no font size set
anywhere, no new chrome cyan, no theme or generator change.

**Node paths changed on purpose** (tests updated with them): `Background/SplitArt` -> `SelectArt`, `Background/Halftone` ->
`Vignette`; `SelectPhase/Root/Title` moved after `PlayersRow` (same path); `P{n}Name` from
`P{n}Panel/P{n}Card/Paper/Inset/Col/NameBox/P{n}Name` to `P{n}Panel/NameBox/P{n}Name`; `P{n}Stats` from
`P{n}Panel/P{n}Card/Paper/Inset/Col/P{n}Stats` to `P{n}Panel/P{n}Card/Col/P{n}Stats`; a tile's `Portrait` / `Silhouette` are
gone (the `GlassTileDecor` draws the portrait); the Holodeck's `Layout/TagClearance` is gone, `Layout/Title` is new and
`TimeLimit` moved from `RulesRow` to `TimerRow` (`RulesRow/StockCount` kept). Every other bound path is unchanged
(`PlayersRow/Grid/CharacterButton0..n`, `RandomTile`, `P{n}Panel`, `P{n}Ready`, the join prompts, `ButtonRow/*`,
`HintLabel`, `RemotePlayNotice`, `CountdownLabel`, `StageRow/StageSelect`, `RulesRow/*`, `StageButtonRow/*`, the console's
`Center/Tagged/Panel/Layout/*`).

## Test delta

`[TestCase]` count **3722 -> 3725 (+3)** on my base, as declared; **3729 -> 3732** with `main` merged.

- `CardGridRulesTests` 3 -> 3, all rewritten: `TheGridIsThreeColumnsAndTheRandomTileHasAFullWidthRowOfItsOwn` (the drawn layout, the cursor steps, the stat maxima), `AFourthRowStartsTheGridHigherAtTheCompactSizeAndKeepsTheRandomTileOnItsLine`, `PastTheCompactSizeTheTilesShrinkToKeepTheBlockInsideItsBand`.
- `CharacterSelectCardTests` 7 -> 10: the seven rewritten (`TheGridIsThreeColumnsOfGlassTilesWithTheRandomTileAloneOnAFullWidthRow`, `EveryTileShowsItsCardNameAndTheRandomTileAQuestionMarkAndTheRandomWord`, `TheStatTubesFillAgainstTheRostersRealMaximaInsideTheGlassStatPanel`, `ATileBothPlayersHoldShowsBothBadgesTheP1ToP2EdgeAndBothReadyBadges`, `TheTilesAndHeadsWearTheActiveSlotPaletteInEveryPalette`, `TileTextShrinksToItsBoxAtLargeUiScaleButNeverBelowItsSizeAtScaleOne`, `TheStagePhaseShowsAGlassTilePerStageAndFocusesFightAndTheConsoleIsAGlassSlabWithSettingsRows`: the stage tiles and their names on the tile, Fight focused, the console's slab, and at 1.0 and 1.4 one field width and the slab clear of the prompt bar) and three new (`TheSideHeadsFitLongNamesAndShowTheReadyOrIdleBadge`: both heads at 1.0 and 1.4, the D10 rule, every line drawn, the name inside its head and on the screen, the 6a box and badge line at 1.0, the badges; `TheSidePanelsShowThreeStatTubesAndThreeAbilityLensesWithNames`: tubes, lenses at 91 / 230 / 369, names, and for every hero at 1.0 and 1.4 no broken word, every line drawn, one size per row; `TheHeroSpritesStandOnTheFeetLineFromTheBakedIdleFrames`). UI-scale and session state restored in `finally`.
- In place: `CharacterSelectSceneTests` (the `P{n}Name` / `P{n}Stats` paths, the tile internals through `GlassTileDecor` and the Random tile's "?", Fight focused on entering the stage phase); `UnusedTranslationKeyTests` (the row and the orphan above).
- `HolodeckConsoleTests` and `PadMenuFighterSelectTests` needed no change (the plan listed them as in-place candidates); both pass.

## Runs (this worktree, the lock held for every launch, `testhost` drained first)

- `dotnet build`: 0 errors, the two known warnings. Headless `--quit`: exit 0, no error, before and after the merge.
- Select filter (`CharacterSelect`, `Card`, `Holodeck`, `MenuLayoutBounds`): **66 / 66** on every iteration (the last before the 6a box check was added; the UI filter run below covers it).
- UI filter (plan 3.2, which includes `PadMenu` and `UnusedTranslation`): **737 / 737** at `00b48dca`'s tree; **744 / 744** on the merged tree (`8e5edc00`).
- `.import` and `default_bus_layout.tres` line-ending churn checked EOL-only and reverted after every launch; never committed.

## Fail evidence (every new or rewritten case seen failing)

Three temporary mutation batches (`D:\Projects\ftt-scratch\tg-select\mutate.py`, applied, built, run, restored and checked
by SHA-256), logs `mutA.log`, `mutB.log`, `mutC.log` beside it:

- Batch A (19 failures, each for its own reason): `CardGridRulesTests` - a step right on Random leaving it ("expected 9 but is 0"), `MaxFullRows` 4 ("expected 140 but is 164"), the shrink off the compact proportion (0.021 > 0.0001). `CharacterSelectCardTests` - the Random tile 10 px low ("Random tile at (0, 550), not (0, 540)"), the full name for the card name ("leonardo: name 'Da Vinci', not the card name 'Leonardo'"), the tube scale 10 % over the maximum ("less than 0.001 but is 13"), no lock mark ("expected 0 but is -1"), the head shape in gold (all six palette/player rows), tile names never fitted ("tile 6: 204 px is neither fitted to 156 nor at the floor"), stage names not shrunk at 1.0 ("Stage6's name runs over x -5-265 of its 260 px tile"), the head name placed by the box width and measured while trimming ("einstein P2 @1: drawn over x 1490-1930, past its head 1440-1880 | leonardo P1 @1: 1 of 2 lines show ..."), the lens cells 19 apart plus no shared name scale ("lens 1 centred at 240, not 230 | einstein name 1 @1.4: scale 0.94, the first name's 0.72 ..."), the feet 30 px low ("P1 feet at (260, 730), not (260, 700)"). `CharacterSelectSceneTests` - no portrait ("Tile 0 portrait texture is missing"), Fight not focused, the head box renamed ("Missing authored control .../P1Panel/NameBox/P1Name"). `UnusedTranslationKeyTests` - main's marker and orphan list ("Unused: fighter_random_mystery"; "no longer contains `$\"character_{_characterIDs[index]}_dates\"`").
- Batch B: the console fields without `ExpandFill` and without the prompt clearance ("@1.4: StageSelectRow/StageSelect is 497 wide, CpuDifficultyRow/CpuDifficulty 380 | @1.4: the slab ends at y 1056, under the prompt bar from 1011"); ability names never shrunk ("einstein name 0 @1.4: 4 lines | ... a 175 px word in a 130 px cell breaks").
- Batch C: the head box at Godot's font height ("Joan's name box is y 162.0 + 65.0, not 162 + 51.8 | the idle badge's top is y 239.0, not 225.8").
- Before those, the new 1.4 checks caught two real defects in my own intermediate code (`run4.log`): "mozart P1 @1.4: 3 lines | 2 of 3 lines show" and "einstein name 0 @1.4: 3 lines" (fixed: the scaled two-line floor and the wrapped-name loop).

## Captures

`D:\Projects\ftt-scratch\tg-captures\select\` (16 PNGs and `index.txt`; windowed console build under the lock, on the merged
tree, keyboard device): both choosing, P1 ready (the `rec_02` state), both ready with the countdown, a mirror pick, the join
prompt, Versus CPU (the two-line Shakespeare name), the stage phase at 1.0 and 1.4, the Holodeck console (hub, front end,
1.4), the select at 1.4 and the countdown at 1.4, and every player-slot palette. Compared through `compare_capture.py`
(`compare\`; mean absolute difference per channel, 0-255):

| Reference | `select_p1_ready` | `select_cards` | Reading |
|---|---|---|---|
| `rec_02` whole | 13.0 | 13.1 | Layout coincides; the differences are those below. |
| `select_title` crop | 8.4 | 8.4 | Overlays within about 3 px. |
| `select_tile_grid` crop | 8.9 | 9.2 | Tiles, edges, badges and the Random row coincide; names 1-3 px off. |
| `select_side_p1` crop | 21.9 | 22.2 | Head name and READY badge on the mock's lines (badge top 225.8); the hero about 4 % smaller; the stat panel 460 wide (drawn 440 in the mock) and 9 px taller; ability names 18 px (the mock 13). |
| `select_side_p2` crop | 20.3 | 20.3 | As P1; the idle badge reads "HMM..." (R4) where the mock draws "CHOOSING...". |
| `select_footer` crop | 15.1 | 15.1 | Buttons, hint and prompt bar on the mock's lines; "SYSTEMS CARD" at the 20 px shrink where the mock's overflows its chevron; keyboard caps for the mock's pad discs. |

## Provisional values

Everything 4.2 / 3.7 states is used as stated; my own numbers: the bottom stack's gaps 20 tightened to no less than 6 at a
large UI scale (`StackGap`, `MinStackGap`), the notice 8 px from the foot and 8 under the prompt bar, its 12 px padding; the
footer label's right padding 30; `StageNameShrinkFloor` .75; `WordFitSlack` 2 px; the two-line hero name floor .75 / UI
scale; the hero shadow's taps (8 px down, 3 px apart, alpha .075); the console's control column 380; the grid's band (from
y 112) and `MinPitch` 90 for rosters past four rows.

## Deviations and why

1. **A fourth row takes the compact size** (8.3 keeps full tiles and moves Random down 180): four full rows and Random are 792 px against the 696 px band under the title, so the block would reach y 898, through the countdown and the footer at 878. From the fourth row the tiles are 140 / 152 and the block grows upward from 808; the class summary of `CardGridRules` records it.
2. **The Remote Play notice** has no slot in 4.2; it sits at the foot, centred, `SmallLabel` with halo S, as wide as its sentence so its focus frame hugs it (it is focusable for pad players).
3. **The countdown** sits under the title when the Random tile and the footer leave it no room (1.4).
4. **The console's control column is 380, not 300** (3.3), so "Chicago Exposition - Sealed" reads whole; the fields fill the row, so one width holds at 1.4.
5. **`StagePreview` is hidden** (the stage tiles show every plate); it stays bound with its tooltip.
6. **Fight is focused first** in the stage phase (9.2); `CharacterSelectSceneTests` asserted the stage field before.
7. **The READY glow is a separate ring** behind the badge (the theme's shadow would fill the translucent plate: TG-THEME deviation 7).
8. **The head name's box is T40's line box** (51.8, from the role's line spacing) with the label hanging half the difference above it, so the badge lands at 225.8 as 6a draws it; Godot's label alone is 65 tall and put the badge at 239.
9. **Ability names**: a row shares the smallest fit and shrinks at 1.4 so no word breaks; stage names shrink to .75 at 1.0 ("ALEXANDRIA CHAMBERS" is about 268 px in a 244 px box); a two-line hero name at 1.4 may shrink to the 1.0 floor's 36 px ("WOLFGANG AMADEUS MOZART" needs three lines at .75 there).
10. **Stat panels 9 px taller than the 275.8 box** (top 726, not 736): Godot's font heights for the 24 px values (T42) and two 18 px name lines (T56) exceed the CSS line boxes (1.2, 1.15). Not forced: the rows and names would have to overflow their boxes.
11. **Heroes about 4 % smaller than the mock** at TG-ART's documented draw rule (2.05, feet on 700); I did not rescale TG-ART's frames.
12. **The footer at 1.0** shows "SYSTEMS CARD" at about 20 px beside 24 px "MOVE LIST" and "BACK" (each label fits on its own; 280 px is P1's width).

## Kit gaps (for the closeout's fold)

1. `PulpButtonDecor` compact at UI scale 1.4: the 20 px floor and the paddings both scale with the UI scale, so a 280 px chevron keeps about 165 px of label: "MOVE LIST" and "SYSTEMS CARD" ellipsize at 1.4 (4.2 says "labels shrink to fit, floor 18 px"). Nothing in TG-SELECT can widen them: the footer must fit between the side panels.
2. A compact chevron variation with a 30 px right padding: the select overrides `normal`'s right margin per button (`FooterLabelRightMargin`); the decor reads only `normal`.
3. `Label` has no `focus` style in the theme: the focusable Remote Play notice shows Godot's default frame.
4. The toggle text (`CheckButton`) is mixed case in the console and the stage rules ("Meter Pickups"); T52's upper case and role are not applied to a `CheckButton`'s own text.
5. A path-bound title needs `FlankTitle` (TG-SCREENS' helper); the kit's `AddFlourishHeading` builds a new label.
6. Role line heights: T42 / T56 in Godot are taller than the CSS line boxes (deviation 10); T40's line spacing reproduces its box only through the box arithmetic in `FitHeadName`.

## Not seen

A pad (the prompt bar's pad glyphs, the join by pad), any motion (the READY fade, the hero idle loop beyond a still, the
tile focus), Reduced Temporal Effects, UI scale 0.9, a mouse hover, a live match after the select, a roster past nine in the
scene (the rule is pinned by pure tests only), a Steam Deck.

## For the orchestrator

1. Accept deviation 1 (compact from the fourth row) or rule another fourth-row layout.
2. The footer at 1.4 (kit gap 1): accept the ellipsis, or change the decor's floor to 4.2's 18 px, or rule a narrower layout.
3. A two-line name (Shakespeare) puts the idle badge over a tall hero's raised hand: the text draws over the sprite and reads; move the hero, or accept.
4. `fighter_random_mystery` and the nine `character_<id>_dates` rows stay in `en.csv`, unused (recorded orphan; the `character_` prefix exempts the dates); deleting them is yours.
5. `StageNameShrinkFloor` .75 is my value; the spec does not say how a long stage name fits its 244 px box.

## Drafted text (not applied: AGENTS.md, CLAUDE.md and the ledger are the closeout's)

- AGENTS.md, the pulp/glass UI bullet's select sentence: "the select is CARD-A: a 3-column grid of glass tiles (`CardGridRules`, `GlassTileDecor`) with the Random tile alone on a full-width row (a fourth row of fighters takes the compact 140 / 152 tiles), side heads with the D10 name fit and READY / idle badges, glass stat panels with tubes against the roster's maxima and three ability lenses, and each side's hero standing from TG-ART's baked idle frames (`SelectHeroSprite`, never the roster `SpriteFrames`)."
- Ledger: `VERIFY-UI-SELECT-HERO-SPRITES` closed by TG-SELECT (`SelectHeroSprite`, `CharacterSelectCardTests.TheHeroSpritesStandOnTheFeetLineFromTheBakedIdleFrames`); new `PROVISIONAL-UI-GLASS-SELECT` listing the provisional values above and deviations 1, 9 and 10.
