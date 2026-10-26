# UR-SCREENS handoff (pulp UI redesign, Phase B)

Branch `ur/screens`, from `main` at `22219329`. Build: 0 errors, no new warnings. Full run: `Failed: 0, Passed: 3611, Total: 3611` (2 m 14 s, lock held).

## 1. What shipped, per screen

Shared helper (new): `scripts/UI/PulpScreenKit.cs` (+ `PanelTagAnchor.cs`): `AddTag` (panel tag that adds a plain child to the panel, so the panel keeps its node path and parent), `DressPanel(panel, tagKey, halftone, buttonInset)`, `InsetRight` (a private copy of the plate with a 40 px right margin so a focused banner button's pose stays inside), `AddPromptBar` (bottom-right, 70 / 40), `DecorateButtons`, `ReserveBadgeSpace` (keeps wrapping row text clear of the prompt badge), `AddLeadingIcon` (kit icon at the left of a Label whose `Text` stays one string).

- **Level results.** Ink panel with halftone and a tag carrying the level name (set in `ShowResults`); title is the `TitleLabel` stamp. Dust ledger left-aligned: total on `HudNumeral`, the categories on `HudNumeralSmall`, each with the kit `dust` icon (losses grey); the code-built lines were moved so the ledger reads together before time and rewinds. Texts unchanged (itemisation still adds up). `PulpButton` return button with decor, prompt bar.
- **Match results.** Panel tag, KO stamp (colour pinned by test), outcome line, two fighter columns: `PulpPortraitRoundel` (140), slot shape (`slot_p1` triangle / `slot_p2` circle) in the slot colour, hero name, stocks-lost line (same Labels and texts), a `star4` on the winner and the loser dimmed (shape, not colour alone). Buttons are `PulpButtonCompact` with decor; `FighterFlowRoutes` behaviour untouched. `MenuSafeAreaFit` attached.
- **Loading.** Plain: ink plate with a `HeadingLabel` status. Story: ink plate, warm Warden-portal slit (gold, `WardenPortal` family) over a faded `burst_star.png`, `TitleLabel` era title, `ObjectiveLine` rift line, `StatusLine`. Fighter VS: two `PulpPortraitRoundel` (220) named `PlayerPortrait` / `OpponentPortrait`, `burst_star.png` with a live `loading_versus` label, slot labels, the stage preview in a paper frame (hidden with the preview), stage name. `LoadingScreen` now types the portraits as `Control` (a roundel or a `TextureRect`). Min display time and variant resolution untouched; no second transition added.
- **Game Over.** Danger-toned backdrop, ink panel with halftone, `Danger` title stamp, `ObjectiveLine` dust notice, two `PulpButton`s with decor, prompt bar. The old glass art is gone.
- **Credits.** `Backdrop`, accent Cinzel headings (upper-case), ink body lines, no colour overrides; skip hint on `HintLine` over its own ink plate. Scroll untouched.
- **Resonance grid.** Ink panel with halftone and a `CONSTELLATION` tag (`resonance_grid_tag`), `TitleLabel`, `HudNumeralSmall` balance, `StatusLine` hint. Node chips are kit plates: paper (`panel_paper`) when owned, ink otherwise, dimmed when locked and darker when dormant, a gold focus ring, and a tier mark (circle / diamond / four-point star, black rim; `ResonanceTierMark`). Edges keep their semantic colours (solid / dotted / glowing teal) and gain a black ink stroke and stop at each chip's border. Respec and Back are compact banner buttons; the respec confirm modal is used as-is. Centring and `MenuSafeAreaFit` kept.
- **Calibration drills.** Picker and list: plain `Backdrop` (glass art removed), upper-case `TitleLabel`, `StatusLine` hints, compact banner buttons with decor and prompt bar; picker tile names turn dark on the gold focused tile and tiles are 140 high; list rows reserve space for the badge. Drill HUD: ink panels (`InkPanel`), the hologram stage moved from chrome cyan to the accent / ink greys, result buttons compact banner buttons, pass title in `Accent` (`CalibrationDrillRunner`).
- **Timeline restart.** Ink panel (halftone, inset), upper-case title, `PulpButton` rows with decor, prompt bar. Paths unchanged.
- **Fighter play options.** Ink panel, `PulpButton`s with decor, prompt bar; glass art removed.
- **Network select (light pass).** Backdrop, ink panel around the stack (new `Panel` node between `Center` and `Layout`), compact banner buttons, `TitleLabel` title.

## 2. Tests

`[TestCase]` delta **+9** (3602 → 3611): `PulpScreenKitTests` 4, `ScreenSceneStyleTests` 1 (no font-size override, no chrome cyan, no glass art in 13 scenes), `MatchResultsPulpTests` 1, `LoadingRoundelTests` 1, `ResonanceChipTests` 2.
Changed in place: none (the existing results, loading, grid, timeline, drill and `MenuLayoutBoundsTests` suites pass unchanged).

## 3. Provisional values

Roundel diameters 140 (match results) / 220 (VS); stocks and slot icons 30 px; button right inset 40 px; badge reserve 84 px; tier shapes (circle / diamond / star); chip content margins 12 / 38 (room for the mark); owned chip = paper; dim 0.78 locked, 0.55 dormant; the winner star colour `Accent`; the loser dimmed to 0.62 alpha; the hologram stage greys and gold.

## 4. Kit gaps (for the closeout)

- No Label stylebox role for "text on an ink plate": the credits skip hint uses a scene-local `StyleBoxFlat`.
- A decor button cannot hide its prompt badge, so wrapping rows need `ReserveBadgeSpace`.
- Loading plates carry no tag (no copy for one); the drill HUD top panel has no tag.
- `PulpTaggedPanel` would change node paths; `PanelTagAnchor` is the path-preserving variant and could move into `scripts/UI/Pulp/`.
- Button labels are mixed-case until UR-WIDGETS step 3 (orchestrator note).

## 5. Capture comparison

`D:\Projects\ftt-scratch\ur-captures\screens\`: `results_level`, `results_level_scale14`, `results_match`, `loading_story`, `loading_fighter`, `loading_plain`, `game_over`, `credits`, `resonance_grid` (Shakespeare: dotted satisfied, solid unsatisfied, owned, available, locked, dormant), `resonance_grid_anyof` (Einstein), `drill_picker`, `drill_list`, `drill_hud`, `timeline_restart`, `fighter_play_options`, `network_select`. Against `mix_pause.png` / `mix_menu.png`: ink panels with paper rim and hard shadow, gold tags, tilted gold focus banner with marker and badge, Cinzel accent titles and the prompt bar match. Differences: the prompt bar shows the keyboard key-cap in the capture runner; the tag text of the capture reads a raw key because the capture's level key is not a translation key; the drill HUD panel still overlaps the pre-redesign Fighter HUD at the bottom (UR-HUDF moves that). The final captures of `drill_picker` / `drill_list` were retaken after the focus-tile and badge fixes; `loading_*`, `drill_hud`, `fighter_play_options` are from the first capture run (the later fixes did not touch them, except the right inset on the panels with button stacks).

## 6. Not seen

Nothing in play; a hand-held pressed button; the Resonance unlock flash on a chip; the grid at UI scale 1.4 (the bounds test passes, no capture); drill result card; the Story and Fighter loading screens through the real wipe; pad-device prompt glyphs on these screens; credits at the end of the roll; the network select with a live host status.

## 7. Drafted doc lines

- AGENTS.md UI section: "The remaining screens (level and match results, loading, Game Over, Credits, the Resonance grid, the Calibration Drills, the timeline restart panel, Fighter play options, network select) wear the pulp language; `FTT.UI.PulpScreenKit` holds the shared helpers. Resonance node chips are paper (owned) or ink plates with a tier shape; edge colours remain semantic."
- Baseline: 3602 + 9 = 3611.
- Ledger: `PROVISIONAL-SCREENS-PULP-VALUES` (section 3).
