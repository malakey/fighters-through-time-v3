# Temporal Glass UI rebuild (2026-10-26 ruling): Direction A's glass panels, chevron buttons, glass tubes, lens slots and hairline icons in Resonance Gold with Cinzel + Outfit, restyled over every redesigned screen

Status: **planned** (written 2026-10-27; nothing started). Base: `main` at `221e0fa7`. Test baseline **3691**; planned end **3720** (§2.14). Workstream IDs `TG-*`, branches `tg/<ws>`, worktrees `D:/Projects/ftt-wt/tg-<ws>`, handoffs `docs/handoffs/TG_<WS>.md`. The structure follows `docs/UI_REDESIGN_2026-10-26_PLAN.md`, the pulp package this one replaces.

Why: having seen the Retro Pulp build in the game, the user rejected it on 2026-10-26 ("I want to redo all of these elements that we did. I don't like this design.") and chose Direction A: "Go back and implement the original A option (temporal glass)", and build it now (`DECISIONS.md`, round 5). Every screen the pulp package redesigned is restyled; its layouts, UI-scale and focus fixes and their tests stay.

## 0. Sources, authority and rulings

### 0.1 Sources

| What | Where | Use |
|---|---|---|
| **Look authority** | `D:\Projects\fighters-through-time-docs-2\mockups\ui_redesign_2026-10-25\UI_SPEC_GLASS.md` | Every value: palette §1, type roles T01–T60 and the halos §2, components §3, screen layouts §4, motion §5, the Godot mapping §6, assets §7, agent choices §8.1, undrawn screens §9, differences from the pulp build §10. Section numbers in this plan are the spec's. |
| Spec assets | same folder, `glass_spec_assets\icons\` (52 SVGs), `glass_spec_assets\crops\` (18 1:1 crops of the `rec_*` renders) | Icon sources (TG-ART copies them into the repo); side-by-side references. The crops show the browser's backdrop blur, which the build cannot have (spec 1.4). |
| Visual references | same folder, `screens\1080p\rec_01` … `rec_06` (main menu, select, pause, Story HUD, Story HUD stress with the boss bar, Fighter HUD) | The selected mix at 1:1. The `dir-a_*` renders are Direction A's own page (PAL-A, HUD-F1) and are not the target. |
| Rulings | same folder, `DECISIONS.md` round 5 | §0.2. |
| Mockup source | same folder, `_build\css\*.css` and `_build\*.py` | What the spec was extracted from. Read it when the spec is silent or ambiguous, and record the answer. |
| Recon (read-only, HEAD `221e0fa7`) | `docs/recon-2026-10-26-glass/B_build_inventory.md`, `C_glass_heritage.md` | Every pulp file, variation, screen user and UI suite, classified keep / restyle / delete; the hot spots; how deep "Pulp" goes; the old glass assets on disk and the renderer's limits. |
| The pulp build | `docs/UI_REDESIGN_2026-10-26_PLAN.md` §1, §2, §9, §10; `docs/handoffs/UR_*.md`; `AGENTS.md`'s "pulp UI redesign" bullet | What exists, how it was built, every fix it took. |
| Conventions | `CLAUDE.md`, `AGENTS.md` | Toolchain, failure signatures, the GdUnit lock, localization, the generator rule, the capture tool, engine rules. |

### 0.2 The selection

```
PAL-B  TYPE-A  PNL-A  BTN-A  GAUGE-A  ICN-A  HUD-S1  HUD-F2  MENU-A  CARD-A  PAUSE-A  BOSS-A  MOT-A
```

| Element | Ruling | ID |
|---|---|---|
| Panels, buttons, gauges, icons | **Temporal Glass** (was Retro Pulp) | `PNL-A` `BTN-A` `GAUGE-A` `ICN-A` |
| Main menu, character select, boss bar, motion | **the glass family's own** (were the pulp family's) | `MENU-A` `CARD-A` `BOSS-A` `MOT-A` |
| Palette, type, Story HUD, Fighter HUD, pause | unchanged: Resonance Gold, Cinzel + Outfit, corner cluster, top versus bars, centred panel | `PAL-B` `TYPE-A` `HUD-S1` `HUD-F2` `PAUSE-A` |
| Standing rulings kept | HUD sizes as drawn ("bigger", already in `HUD_CONTRACT.md`); the Fighter Ultimate meter a bar under each HP bar; the Fighter HUD at the top; block-success effect colours as built | rounds 3–4 |

### 0.3 Orchestrator rulings on UI_SPEC_GLASS 8.2

Provisional and reversible. The closeout records each in the ledger and lists them for the user.

| # | Question (8.2) | Ruling | Built by |
|---|---|---|---|
| R1 | Quick Play row | Keep the build's behaviour: release builds show "Quick Play (Einstein vs CPU)", debug builds add Level Select. The label shrinks to fit the 700 px column cap (3.2). `VERIFY-UI-QUICK-PLAY-ROW` stays open. | TG-MENU |
| R2 | Title split | The two existing keys only, laid out as A: line 1 `menu_cover_title_word`; lines 2 and 3 from `menu_cover_title_banner` split at its first space (a value without a space gives one middle line; an empty value hides it). No new key. | TG-MENU |
| R3 | "TEMPORAL RESONANCE" tagline | Omitted: no key, no new copy. | TG-MENU |
| R4 | Select "CHOOSING…" state | The existing key's text (`fighter_select_choosing`, drawn "HMM..."). | TG-SELECT |
| R5 | Continue card lines | Existing keys and data only: tag `boot_continue`; hero `character_<id>_name`; place = the built short place (`MainMenuContinue.PlaceKey`, cut by `ShortPlace`); meta = the difficulty name (`difficulty_*`) · `menu_continue_chapter`, or `save_completed` for a completed save. No playtime, relative save time or "Slot N". | TG-MENU |
| R6 | P2 HP tube | Mirrored: P2's tube leans "\", its fill, tracker and echo anchor at the outer right, its gradient runs from the anchored end to the moving end, its cap sits on the moving edge, its value on the inner end. **Planner extension (check):** P2's Ultimate bar mirrors the same way. | TG-WIDGETS step 2 (the mode), TG-HUDF (use) |
| R7 | Pause status strip | The built line, `pause_status_level` (difficulty · level clock · Integrity). | TG-PAUSE |
| R8 | Integrity wedge below 20 % | Red (`Danger`) below the Collapse Tremor's first threshold, read from `TimelineIntegrityRules`, as built (AC16). | TG-HUDS |
| R9 | Fighter lens key labels | Kept: S1 / S2 / MOV under each lens (AC15). | TG-HUDF |
| R10 | Resonance grid's satisfied edge | Gold (`UIPalette.Res2`); `ResonanceEdgeLayer.cs` leaves the cyan allow-list. | TG-SCREENS |
| R11 | Gold chrome; top Fighter HUD | As decided (PAL-B, HUD-F2), recorded as one-line switches: spec 0.4 states what PAL-A or HUD-F1 would change. No runtime toggle is built; the generator keeps the palette in one table so a PAL-A swap stays a table change. | TG-CLOSE records |

### 0.4 Spec agent choices adopted, and the planner's resolutions

The spec's §8.1 agent choices AC1–AC28 are adopted as written, except where §0.3 rules otherwise; the closeout records them. The planner resolved the following where the spec is silent, inconsistent with itself or collides with a kept test. **Orchestrator: check these.**

- **P1 Row gaps.** Spec 9.1 floors a horizontal button row at 34 px; 3.7 draws the select footer's 290 px buttons 20 px apart, and 34 px gaps do not fit between AC4's 460 px side panels. Ruling: `FocusRowGap` = max(34, the derivation in TG-WIDGETS step 1); the select footer buttons are 280 wide with 34 px gaps, their labels shrinking toward 4.2's 18 px floor.
- **P2 Pause panel position.** 4.3 hangs the panel from y 92; `MenuLayoutBoundsTests` (kept) requires the largest panel centred within 2 px. The panel stays vertically centred, as the pulp build did; recorded as a deviation.
- **P3 Screen streak on the results screens.** AC19's list names "results", its reason forbids a static line over gameplay. Level and match results sit over gameplay, so they get no streak.
- **P4 Beacon at zero charges.** `HUD_CONTRACT.md` requires a hollow outline at zero charges; the spec authors no such glyph. TG-ART adds `beacon_anchor_hollow` (regular and bold).
- **P5 Pressed under focus.** Godot draws the `focus` box over the state box, which hid the pulp pressed look (`PROVISIONAL-UI-PULP-FOCUS-HIDES-PRESSED`). The glass focus box is an overlay (lit edge, gold wash, glow, no dark base) over state boxes that carry the fill, so a focused, pressed row reads as pressed, and hovered, focused and both read the same (AC8). This departs from 6.4's description of `glass_btn_focus`; TG-ART and TG-THEME may choose another split that meets the same acceptance.
- **P6 The second dialogue speaker.** 3.9's silhouette frame is drawn only when the sequence names a second speaker.
- **P7 No hit flash.** MOT-A defines none (its damage cue is the tracker). TG-HUDF removes the Fighter HUD's call; the closeout deletes `PulpMotion.HitFlash` and `HitFlashLimiter`.
- **P8 Utility buttons.** The default `Button`, `OptionButton` and `SpinBox` field wear the glass field of 3.3 (chamfer 8; binding cells and option buttons); the chevrons are the three button variations.
- **P9 Select ability lenses.** Three, as the mockup draws them: Special 1, Special 2, Movement (`_build/screens_select.py`). The pulp card showed four.
- **P10 The compact marker.** Not a conflict: the CSS puts the compact marker's left edge at 22 px (`css/40_buttons.css`), so its centre is at 29 px, as 3.2 says.

### 0.5 Out of scope

No gameplay, Fighter simulation, save (v8), protocol (v4) or Klotho component change. No new screens; no text in a texture. World-space labels (the hub, the `RestorationFont` and `ResonanceHoldNode` channel displays) and `TestArenaHUD` get only what the theme gives them. No backdrop blur (spec 6.5's optional path is not attempted; 1.4's fills are the design). No Steam Deck or pseudo-locale sweep (`VERIFY-PSEUDO-LOCALE-SCREENS` stays open).

## 1. Standing decisions (every workstream)

1. **Authority.** `UI_SPEC_GLASS.md` is the look authority. `DECISIONS.md` round 5 and the user's earlier standing rulings (§0.2) win over it, then the orchestrator's rulings (§0.3, §0.4). `HUD_CONTRACT.md` as amended on 2026-10-26 stays the authority for HUD sizes and placement; the closeout amends only the wording that names the pulp look (spec 8.5). `AGENTS.md`'s authority rule holds: where build and design differ, change the build or record it in the ledger.
2. **Rulings bind.** §0.3 and §0.4 are not re-argued inside a workstream. A workstream that finds one unworkable stops and reports to the orchestrator.
3. **Naming.** C# identifiers keep the legacy name "Pulp": namespace `FTT.UI.Pulp`, the `Pulp*` classes and enums, `scripts/UI/Pulp/`, `StoryHUD.Pulp.cs`, `PulpMotion` / `PulpMotionRules`, `tests/Pulp/`, the scene `ext_resource` paths that point at them. This follows the repository's legacy-identifier precedent (`TemporalGlassButton` wore pulp art for a day); a rename would touch about 100 files and collide with every workstream. The closeout records "Pulp" as the kit's legacy name in `AGENTS.md`. New art lives in `assets/sprites/ui/glass/` with the catalog `glass_kit.json`, built by `tools/asset_src/glass/`; `PulpKit` reads it. Theme names follow §2.2. New suites are named for what they test (`Glass*Tests`). Capture target names are kept, so the before gallery (`D:/Projects/ftt-scratch/ur-captures/main-6a840a3f/`, 76 PNGs) and the after gallery pair 1:1; new states get new targets.
4. **No gameplay change.** Presentation only (§0.5). `scripts/FighterSim/`, `scripts/Core/`, the save payloads and the protocol are read-only; `scripts/Combat/CalibrationDrillRunner.cs` is edited for presentation only.
5. **Localization.** No new visible copy is expected: §0.3 maps every drawn string to an existing key or omits it (spec Appendix B). A key a workstream proves unavoidable goes in a block at the end of `localization/en.csv` headed by a comma-less `# TG <WS>` comment row, with its raw key in the `.tscn` `text`, tested after a local `--headless --import`. Only the orchestrator commits `localization/en.en.translation`, regenerated in the integration worktree after each merge that touched `en.csv` (touch the CSV first: a plain `--import` skips an unchanged one). No row is deleted without the orchestrator; `UnusedTranslationKeyTests`, `SceneVisibleTextTests` and `ScriptTranslationKeyTests` stay green.
6. **Tests.**
   - Keep every layout, scale, focus, no-override and source-scan test (item 8). Update a test only where it names a renamed variation or a moved node path, or pins a number the spec changes; rewrite or delete a test that pins a pulp-only look; add tests for the new glass behaviour so coverage does not drop.
   - Every workstream declares its exact `[TestCase]` delta (§2.14) with the arithmetic, every test changed in place and why, and every deletion and why. Agents count with the attribute count, which equals the suite total (3691 today): `(Get-ChildItem tests -Recurse -Filter *.cs | Select-String -Pattern '^\s*\[TestCase\b').Count`. A delta that differs from §2.14 is explained case by case.
   - **A test must be able to fail.** For each new or rewritten case the handoff says how it was seen failing: against the pre-change code, or with a temporary mutation that was then reverted.
   - One `[TestSuite]` per file (signature 6). `[RequireGodotRuntime]` on any suite that touches a Godot type (signature 7). Never an empty `OverrideFailureMessage` (use the repository idiom). Restore `GameManager.CurrentSession` and `SceneTree.Paused` and free added nodes in `finally`; main-menu tests use the existing seams (`MainMenu.SaveSlotsOverrideForTesting`, `ResumeOverrideForTesting`), so no real save is read (signature 8).
7. **Process.**
   - Phase 0 (orchestrator) → Phase A (foundation, declared merge order) → Phase B (six screen agents in two groups) → Phase C (closeout); §2.0.
   - Each agent works in its own worktree `D:/Projects/ftt-wt/tg-<ws>` on `tg/<ws>`, cut from the commit §2.0 names, with `.godot/` copied from the main checkout. It commits on its branch and never merges or pushes.
   - Agents run **filtered** GdUnit runs only (the UI filter in §3.2 plus their own suites), never the full suite, and hold `D:/Projects/.ftt-gdunit-lock` for **every** Godot launch: test, import, capture, asset build. Acquire with `mkdir`; retry while it exists; treat it as stale only when it is older than 20 minutes and no `testhost` or `*_console` process runs; `rmdir` only a lock you created, right after the run, also on failure. Drain `testhost` first. Never stop a non-console Godot (the user's editor).
   - One detached integration worktree, `D:\Projects\ftt-wt\fu-integ` (at `221e0fa7` today; re-pointed at each new head), runs every orchestrator import, build, full suite and capture. Nothing launches Godot in the main checkout, where the user's editor is open.
   - Each agent writes `docs/handoffs/TG_<WS>.md`: what shipped (files, types, variations, textures), the delta arithmetic, every test changed and why, the fail evidence, every provisional value, its cross-edits (item 10), kit gaps, what did not ship, the captures and how they compare, and drafted doc text.
   - Code review goes to Codex through the orchestrator (the `cross-review` skill); no agent reviews its own work and no Claude subagent reviews. A finding goes back to the owning agent or to a fix step.
8. **Keep the pulp package's fixes.** They are style-neutral and stay with their tests, re-numbered where the glass geometry changes their inputs: `MenuSafeAreaFit` on every overlay; `CardTextFit`; the tag clamp (`PulpTagPlate.Place`, `RightMargin` 24); `FocusRowGap` / `SeparateFocusRow` (re-derived, §0.4 P1); the Settings Controls column model (`SettingsMenu.ApplyColumnWidths`); the boss name / phase drop rule (glass numbers, spec D11); the Move List and Systems Card footer gap; `PulpButtonDecor.GrowToFit` on the modal, results and grid buttons; the Resonance chip fit and separation (`ResonanceChipLayout`); the credits hint band; the dialogue box padding (re-valued); the capture runner's theme hold and `ResetScale`; `UiScaleBinder`'s HUD scaling; the HUD bounds rules (`StoryHudBoundsTests`, `FighterHudTopBandTests`); `MainMenuContinueRules`; the upper-case button label the decor draws while `Button.Text` stays the raw key (spec 2.5).
9. **Shared files and locked paths.** §2.1 is the matrix: one owner per file per phase.
   - The generated files (`resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.Roles.cs`) are written only by `py tools/ui_theme/build_ftt_theme.py`, and only by TG-THEME (Phase A) and TG-CLOSE (Phase C). After a merge they are regenerated, never hand-merged.
   - Phase B edits none of: the generator and its outputs, `UIPalette.cs`, `scripts/UI/Pulp/`, `scripts/UI/Motion/`, `RadialProgress.cs`, `NotYetFlash.cs`, the glass art and pipeline, `assets/fonts/`, `scripts/Core/`.
   - A Phase B screen that needs a role, plate or widget the kit lacks uses the nearest role, or a scene-local `StyleBoxFlat` / `StyleBoxTexture` over a glass kit PNG (never a font-size override), and lists the gap; the closeout folds it into the generator. A blocking widget bug goes to the orchestrator, who may open a TG-WIDGETS fix step (as UR-WIDGETS steps 3 and 3b did).
   - Shared test files have method owners (§2.1). A workstream that breaks a method it does not own reports it instead of editing it.
   - `localization/en.csv` is shared, append-only by block.
10. **Transition safety: two-stage retirement.** Phase B is cut after Phase A merges and restyles screen by screen, so between merges every screen must still compile, pass its tests and render.
    - Phase A **removes nothing another workstream's file uses**: no `UIPalette` constant, no `UIPalette.Variations.*` or `LabelRoles.*` name, no theme variation, no public member of a `Pulp*` widget, no `PulpMotionRules` constant used outside `Motion/`, no `PulpMotion.HitFlash` / `HitFlashLimiter`, no pulp texture, catalog entry or icon name. A widget member that loses its meaning becomes a documented no-op.
    - `PulpKit` reads `glass_kit.json` first and the frozen `pulp_kit.json` second, so a pulp stem still resolves. Glass stems are prefixed so that none collides with a pulp stem. Pulp-only variations keep their pulp plates until the closeout.
    - Phase B removes every use of a retired name (§2.2), pulp stem, icon alias and `HitFlash` from its own files.
    - The closeout deletes them after a reference scan proves nothing uses them (§2.13).
    - Exceptions, deleted in Phase A because their only users are rewritten in the same step: `PulpMotion.Thunk`, `PulpMotionRules.ThunkValues` / `Stepped` (TG-THEME; no caller); `HalftoneCorner` with `halftone_corner.gdshader`, `ui_circle_mask.gdshader` and `pulp_bar_layers.gdshader` (TG-WIDGETS).
    - **Cross-edits.** A Phase A workstream may edit a file that Phase B will own only to keep the build compiling or a test passing at its merge: the smallest change, no restyle, listed under "cross-edits" in its handoff. Phase B inherits the result.
11. **Engine rules.**
    - Theme styleboxes are engine-native only (`StyleBoxTexture`, `StyleBoxFlat`, `StyleBoxEmpty`, `StyleBoxLine`), never a C#-scripted `StyleBox` (signature 2). Gradients are engine `GradientTexture2D` resources. Geometry the theme cannot express is drawn by C# `Control`s in `_Draw` (spec 6.1–6.2).
    - **Draw and per-frame paths allocate nothing.** Polygons, colour arrays, UVs and strings are cached and rebuilt only when the size, state or text changes; shader parameters use cached `StringName`s and are written only on change; no `GetChildren()` / `GetNodesInGroup()` in a recurring path, and every engine collection is disposed (`AsDisposable`); `QueueRedraw` only on a change. (These were the two defect classes the pulp review found.)
    - Never `Dispose()` a `Resource`. Authored data `.tres` (for example `CharacterData` for a portrait) load through `AuthoredResources.Load`; textures and gradients do not.
    - Shaders multiply the incoming `COLOR` (`UiShaderModulationScanTests`), use no `instance uniform`, never pass `TEXTURE` as an argument, and take one `ShaderMaterial` per instance where parameters differ (spec 6.7).
    - Every motion reads `FTT.Core.ComfortSettings` and falls back to a stable cue under Reduced Temporal Effects; tweens that run while the tree is paused use the process pause mode; a transform tween goes on an inner visual or a draw offset, never on a container-managed control. Nothing new writes `SceneTree.Paused`; its existing writers (`PauseMenuBase`, `DialogueManager`, `NearCaptureBeat`) keep their rules. Nothing flashes faster than once a second.
    - Text over art, a fill or a lens carries the scene halo (spec 2.4); text on glass carries none.
12. **Theme rule.** Never hand-edit `ftt_theme.tres`. Every theme value lives once, in the generator; `UIPalette.cs` mirrors the palette and `UIThemeTests` pins the two together.
13. **Docs.** Implementers do not edit `AGENTS.md`, `CLAUDE.md`, the ledger, the contracts, `design-godot.md` or its master; their handoffs carry drafted text. The orchestrator appends §9; the closeout writes the docs and §10.
14. **Nobody plays the build.** Captures are the visual evidence. Every handoff says what was not seen: a pad, motion, Reduced Temporal Effects, a live match or level, UI scale 1.4 where not captured.
15. **Evidence.** Each workstream captures every surface it touched at 1920 × 1080 with the existing capture tool (a windowed console-Godot run, never `--headless`, under the lock, kept brief), at UI scale 1.0 and, where the surface scales, 1.4, into `D:\Projects\ftt-scratch\tg-captures\<ws>\`. It looks at every PNG, compares each drawn screen with its `rec_*` render and spec crops through `tools/ui_theme/compare_capture.py` (TG-THEME), fixes what differs in its own files, and reports the comparison honestly. The closeout builds the final gallery.

## 2. Workstreams

### 2.0 Shape and order

```
Phase 0  Orchestrator: commit this plan and docs/recon-2026-10-26-glass/; check fu-integ is at main (221e0fa7);
         one baseline full run (3691); run the owed Codex review of UR-POLISH2 (REVIEW-UI-PULP-CODEX-3,
         brief D:\Projects\ftt-scratch\cross-review-ur-polish2.md) and put each valid finding into the brief
         of the TG workstream that owns the file (no separate fix round); brief Phase A.
Phase A  A1  TG-ART  ||  TG-THEME step 1                      (both cut from main after Phase 0)
         A2  merge TG-ART; TG-THEME merges main, does step 2 (wires the nine-slices); merge TG-THEME
         A3  TG-WIDGETS step 1 (cut after A2); merge          -> Phase B group 1 is cut here
         A4  TG-WIDGETS step 2 (main merged in); merge        -> Phase B group 2 is cut here
         Codex review 1 covers A1-A3 as soon as A3 merges; its findings are fixed on the TG-WIDGETS branch
         (with step 2, or a short fix step) before group 2 is cut; group 1 merges main when the fix lands.
Phase B  group 1: TG-MENU, TG-PAUSE, TG-SCREENS              (need step 1 only)
         group 2: TG-HUDS, TG-HUDF, TG-SELECT                (need the gauges of step 2)
         merge in readiness order; ties: TG-PAUSE, TG-SCREENS, TG-MENU, TG-HUDS, TG-HUDF, TG-SELECT.
         Codex review 2 on the Phase B diff after the last merge; findings to the owning agents.
Phase C  TG-CLOSE: C1 fold the gaps, purge the pulp layer, add the guards; C2 docs, three full runs,
         the final gallery, Codex review 3, the open questions.
```

**Why this split.** It keeps yesterday's three-agent Phase A and six-agent Phase B, with five changes.

1. Phase B is cut in two groups, because only the HUDs and the select use the gauges; the menu, pause and screens start one step earlier.
2. TG-WIDGETS step 1 carries the portrait frame and a tile decor besides the buttons, because the menu (the Continue card, the character tiles), the pause family (the dialogue box) and the screens (results, the versus loading screen, the grid chips, the drill tiles) need them.
3. `RadialProgress` and `NotYetFlash` move to TG-WIDGETS step 2: both HUDs and the lens use them, and `RadialProgress` also draws world-space channel displays, so one owner keeps its existing modes intact.
4. `PulpKit` belongs to TG-ART, as the reader of its catalog.
5. The closeout gains the purge, because deleting the pulp layer any earlier would break screens Phase B has not restyled yet (standing decision 10).

Each Phase B agent merges `main` into its branch before handing back; only `en.csv` blocks and assigned test methods should conflict.

### 2.1 File-ownership matrix

"—" means nobody edits it in that phase. Cross-edits under standing decision 10 are the only exception and are listed in the handoffs.

| Path | Phase A | Phase B | Phase C |
|---|---|---|---|
| `tools/asset_src/glass/**` (new); `assets/sprites/ui/glass/**` (new: PNGs and their `.import` sidecars, `glass_kit.json`, gradient `.tres`, `select_heroes/`) | TG-ART | — | TG-CLOSE |
| `tools/asset_src/jobs.json` | TG-ART (appends groups `glass`, `glass_icons_raw`; other groups untouched) | — | TG-CLOSE (removes `pulp`, `pulp_icons_raw`) |
| `scripts/UI/Pulp/PulpKit.cs` | TG-ART | — | TG-CLOSE (drops the pulp fallback) |
| `assets/sprites/ui/pulp/**`, `pulp_kit.json`, `tools/asset_src/pulp/**` | frozen | frozen | TG-CLOSE (moves `ability_icons/`, deletes the rest) |
| `scripts/UI/AbilityIconLibrary.cs` | — | — | TG-CLOSE (the new icon path) |
| `assets/backgrounds/menu/temporal_glass_*_background.png` | read-only | used by TG-MENU, TG-SELECT, TG-SCREENS | — |
| Pre-pulp leftovers: `assets/sprites/ui/glass_button_*`, `card_frame*.png`, `title_flourish.png`, `assets/backgrounds/menu/menu_starfield.png`, `menu_glass_shards.png`, `tools/asset_src/menu/` | — | — | — (left alone; a question for the user, §2.13) |
| `tools/ui_theme/build_ftt_theme.py`; generated `resources/UI/ftt_theme.tres` and `scripts/UI/UIPalette.Roles.cs`; `scripts/UI/UIPalette.cs`; `tools/ui_theme/README.md`; `tools/ui_theme/compare_capture.py` (new) | TG-THEME | — | TG-CLOSE |
| `scripts/UI/Motion/**` | TG-THEME (A1–A2); TG-WIDGETS additive only (A3–A4) | — | TG-CLOSE (deletes `HitFlash`, `HitFlashLimiter`) |
| `scenes/diagnostics/UiCaptureRunner.tscn`; `scripts/Diagnostics/UiCapture/UiCaptureRunner.cs`, `UiCaptureTargets.cs`, `UiCaptureTargets.Theme.cs` | TG-THEME | — | TG-CLOSE |
| `scripts/UI/Pulp/**` except `PulpKit.cs`. Step 1: `PulpButtonDecor`, `PulpIcon`, `PulpPromptBar`, `PulpPromptGlyphs`, `PulpTagPlate`, `PulpTaggedPanel`, `PanelTagAnchor`, `PulpToggleDecor`, `PulpPortraitRoundel`, `PulpScreenKit` (both parts), new `GlassGeometry`, `GlassSheen`, `ScreenStreak`, `GlassFlourish`, `TitleGradient`, `GlassTileDecor`; deletes `HalftoneCorner`. Step 2: `PulpBarDecor`, `PulpAbilitySlot`, `PulpStatusChip` | TG-WIDGETS | — | TG-CLOSE (no-op members, icon aliases) |
| `scripts/UI/RadialProgress.cs`, `scripts/UI/NotYetFlash.cs` | TG-WIDGETS step 2 (additive; `RadialProgress`'s existing modes and defaults stay, because `RestorationFont` and `ResonanceHoldNode` use them) | — | — |
| `assets/shaders/ui_title_gradient.gdshader` (new), `ui_desaturate.gdshader` (kept); deletes `halftone_corner`, `ui_circle_mask`, `pulp_bar_layers` | TG-WIDGETS | — | — |
| `UiCaptureTargets.Widgets.cs`, `UiCaptureTargets.WidgetsGauges.cs`, `tests/Pulp/**` | TG-WIDGETS | — | TG-CLOSE |
| `scenes/menus/MainMenu.tscn`; `scripts/UI/MainMenu.cs`, `MainMenu.Cover.cs`, `MainMenuContinue.cs`, `ThirdPartyLicenses.cs`; deletes `MainMenuCoverInk.cs`; `UiCaptureTargets.Menu.cs`; tests `MainMenuCoverTests`, `MainMenuCoverInkTests` (deleted), `MainMenuContinueRulesTests`; in place `MainMenuSceneTests`, `MainMenuExtrasTests`, `CalibrationDrillsRouteTests` | — | TG-MENU | — |
| `scenes/menus/CharacterSelect.tscn`; `CharacterSelectScreen.cs`, `.Cards.cs`, `.PadMenu.cs`, `CardGridRules.cs`, `CardTextFit.cs`, `HolodeckConsolePanel.cs`; `UiCaptureTargets.Select.cs`; tests `CharacterSelectCardTests`, `CardGridRulesTests`, `CardTextFitTests`; in place `CharacterSelectSceneTests`, `HolodeckConsoleTests`, `PadMenuFighterSelectTests` | — | TG-SELECT | — |
| `scenes/ui/StoryHUD.tscn`; `StoryHUD.cs`, `StoryHUD.Pulp.cs`, `HudBadge.cs`, `SiphonStreamsLayer.cs`, `IntegrityDialLayer.cs`, `AnchorPip.cs`, `BeaconAnchorIcon.cs`, `BossPhaseNotchOverlay.cs`; `HudAbilityIndicatorModel.cs` and `DefySealModel.cs` (additive; TG-HUDF reads them); `UiCaptureTargets.Huds.cs`; tests `StoryHudPulpTests`, `StoryHudBoundsTests`; in place `StoryHudContractTests`, `StoryHudPresentationTests`, `StoryHudBeaconAnchorTests`, `IntegrityClockTests`, `DustCounterPersistenceTests`, `BossBarPhaseNotchTests`, `StoryHudLocalizationTests` | — | TG-HUDS | — |
| `scenes/ui/FighterHUD.tscn`; `FighterHUD.cs`, `FighterHudModel.cs`, `FighterOverlayModel.cs` and `FighterPresentationOverlay.cs` (only where needed); `UiCaptureTargets.Hudf.cs`; tests `FighterHudTopBandTests`, `FighterHudLayoutRulesTests`; in place `FighterHudSceneTests`, `FighterHudModelTests`, `FighterHudContentTests`, `FighterMatchPresentationTests` | — | TG-HUDF | — |
| `scenes/ui/StoryPause.tscn`, `LocalFighterPause.tscn`, `Settings.tscn`, `MoveList.tscn`, `SystemsCard.tscn`, `DialogueBox.tscn`; `PauseMenu.cs`, `LocalFighterPause.cs`, `PausePanel.cs`, `SettingsMenu.cs`, `PlayerSlotPaletteRow.cs`, `MoveListScreen.cs`, `SystemsCardScreen.cs`, `ConfirmModal.cs`, `TwoOptionPrompt.cs`, `DialogueManager.cs`, `DialogueLogScreen.cs`, `DialogueTheme.cs`, `DialogueEmotionTreatment.cs`, `FirstUseTooltipPresenter.cs`, `BootSequenceOverlay.cs`; `UiCaptureTargets.Pause.cs`; tests `tests/Pause/**`; in place `StoryPauseTests`, `LocalFighterPauseTests`, `SettingsMenuSceneTests`, `SettingsFrontEndSceneTests`, `DialoguePresentationTests`, `DialogueEmotionTreatmentTests`, `ConfirmModalTests`, `TwoOptionPromptTests`, `MoveListScreenTests`, `SystemsCardContentTests`, `PadMenuConfirmCancelTests`, `PauseFocusLossTests` | `PausePanel.cs` and `PausePanelTests` cross-edit by TG-WIDGETS (halftone → sheen); `DialogueTheme.cs` cross-edit by TG-THEME if `DialoguePresentationTests` needs it | TG-PAUSE | — |
| `scenes/ui/LevelResults.tscn`, `MatchResults.tscn`, `LoadingScreen.tscn`, `StoryLoading.tscn`, `FighterLoading.tscn`, `GameOver.tscn`, `Credits.tscn`, `ResonanceGrid.tscn`, `CalibrationDrillList.tscn`, `CalibrationDrillPicker.tscn`, `HolodeckDrill.tscn`, `scenes/menus/FighterPlayOptions.tscn`, `NetworkSelect.tscn`; `LevelResultsPanel.cs`, `MatchResults.cs`, `LoadingScreen.cs`, `GameOverScreen.cs`, `CreditsController.cs`, `ResonanceGridPanel.cs`, `ResonanceChipLayout.cs`, `ResonanceEdgeLayer.cs`, `ResonanceTierMark.cs`, `ResonanceGridNavigation.cs`, `CalibrationDrillList.cs`, `CalibrationDrillPicker.cs`, `TimelineRestartPanel.cs`, `FighterPlayOptionsScreen.cs`, `NetworkSelectScreen.cs`, `scripts/Combat/CalibrationDrillRunner.cs` (presentation only); `UiCaptureTargets.Screens.cs`; tests `MatchResultsPulpTests`, `LoadingRoundelTests`, `ResonanceChipTests`, new `ScreenTypeDressingTests`; in place `ResonanceGridFitTests`, `ResonanceGridSceneTests`, `LoadingScreenTests`, `TimelineRestartPanelTests`, `SmotheredGameOverTests`, `DrillSceneSmokeTests`, `CalibrationDrillScriptTests`, `LevelResultsStatsTests` | — | TG-SCREENS | — |
| `tests/ContentValidation/UIThemeTests.cs` | TG-THEME | — | TG-CLOSE |
| `tests/ContentValidation/UiShaderModulationScanTests.cs` | TG-WIDGETS | — | — |
| `tests/ContentValidation/UiChromeCyanSourceScanTests.cs` | — | TG-SCREENS (removes the `ResonanceEdgeLayer.cs` row only) | TG-CLOSE |
| `tests/Pulp/ScreenSceneStyleTests.cs` | TG-THEME (drops the `temporal_glass_` ban) | — | TG-CLOSE |
| `tests/ContentValidation/UnusedTranslationKeyTests.cs` | — | TG-SELECT (the `character_` prefix row, a recorded orphan) | — |
| `tests/ContentValidation/PulpKitContentTests.cs` | — | — | TG-CLOSE (deletes) |
| `tests/Unit/MenuLayoutBoundsTests.cs` | — (its allow-list changes only through the orchestrator) | — | — |
| `tests/Unit/UrPolishTests.cs`, owners by method (`TheMenuFitKeepsAnExtraVerticalReserve…` has no owner: everyone keeps it green) | `TheDialoguePanelGivesTheSpeakerItsPadding…AndTheSliderTrackEndsUnderTheGrabber`, `AScaleAppliedWhileTheCaptureRunnerHoldsTheTheme…`: TG-THEME | `TheModalAndResultsButtonLabelsShowWhole…`: TG-PAUSE (TG-SCREENS keeps its results half green); `TheCreditsSkipHintHasItsOwnBand…`: TG-SCREENS | — |
| `tests/Unit/UrPolish2Tests.cs`, owners by method (`TheQuitConfirmationNamesTheRealExitFee` has no owner) | `AnOverlongTagClampsInsideItsPanel…`, `AFocusedButtonInAHorizontalRowClearsItsNeighbours…`: TG-WIDGETS | `TheResultsTagShowsTheShortLevelName…`: TG-SCREENS; `TheControlsColumnsLineUp…`, `TheFocusedFooterPoseClearsTheScrollViewport…`: TG-PAUSE; `EveryBossNameAndThePhaseTabStayApart…`: TG-HUDS | — |
| `localization/en.csv` | blocks `# TG <WS>`, only if unavoidable | same | same |
| `localization/en.en.translation` | orchestrator | orchestrator | orchestrator |
| `AGENTS.md`, `CLAUDE.md`, the ledger, `HUD_CONTRACT.md`, `design-godot.md` and its master, this plan's §10 | — | — | TG-CLOSE (§9: the orchestrator) |
| **Read-only for everyone** (a needed change goes to the orchestrator): `scripts/Core/**` (including `GameManager.cs`: the new transition stays behind `ScreenWipe`'s API), `scripts/FighterSim/**`, `scripts/UI/MenuSafeAreaFit.cs`, `FocusChainBuilder.cs`, `PadMenuInput.cs`, `UiScaleBinder.cs`, `HudOpacityBinder.cs`, `PauseMenuBase.cs`, `FighterFlowRoutes.cs`, `assets/fonts/**`, `project.godot`, `.runsettings`, `.godot/` | | | |

### 2.2 Theme variations and Label roles: old → new

TG-THEME implements the Phase A column; TG-CLOSE the closeout column after the reference scan. The table is the minimum: TG-THEME may add a role or preset it needs and lists it in its handoff.

**Non-Label variations (26 today, plus the new ones).**

| Variation (base) | Pulp today | Glass role | Phase A (TG-THEME) | Closeout |
|---|---|---|---|---|
| `PulpButton` (Button) | T10, 80 px banner plate | T10 menu chevron row, 68 px (main menu, results, Game Over, stage buttons) | re-skin: `glass_btn_*`, content margins left 58 / right 54, Cinzel 700 34 px .13em; colours normal `Ink2`, hover / focus / pressed #FFFFFF, disabled `Ink3` | keep |
| `PulpButtonCompact` (Button) | T11, 58 px | T11 compact chevron, 58 px | re-skin: left 52 / right 40, Cinzel 700 24 px | keep |
| `TemporalGlassButton` (Button) | legacy name, 64 px pulp plate | the pause / list chevron row, 64 px | re-skin (left 58 / right 54, T10) | keep (the name is honest again) |
| default `Button`, `OptionButton`, `SpinBox` field, `LineEdit` | 58 px pulp plate, flats | the glass field (3.3, chamfer 8) | re-skin (§0.4 P8) | keep |
| `InkPanel` (also the default `Panel` / `PanelContainer`) | ink plate | glass slab G1, default padding 22 / 28 / 22 | re-skin (`glass_panel`) | keep |
| `DialogueGlassPanel` | ink plate (legacy name) | glass slab, dialogue padding 22 / 26 / 22 | re-skin | keep |
| `HudPlatePanel` | ink plate (no scene user) | glass slab | re-skin | retire (the HUD uses the presets below) |
| **new** `GlassPanelPause`, `GlassPanelVitals`, `GlassPanelAbilities`, `GlassPanelTimeTools`, `GlassPanelDust`, `GlassPanelTimer`, `GlassPanelStats`, `GlassPanelContinue` | — | glass slab with the 3.1 padding of each preset (30/20/28, 20/26/20, 22/30/14, 14/26/14, 14/24/14, 12/32/12, 20/26/18, 16/20/16) | add | keep |
| `PanelTagPlate` | gold tag | glass tag G2 | re-skin (`glass_tag`, content 14 / 3 / 14 / 3; label role `PanelTag`) | keep |
| `ScrimPanel` | 0.85 scrim × self-alpha 0.7 | the pause dim: rgba(3,5,14,.70) as drawn | re-skin; state the self-alpha contract TG-WIDGETS' `MakeScrim` and the scenes must follow | keep |
| `PulpBar`, `PulpBarH44`, `PulpBarH16`, `PulpBarH14`, `PulpBossBar` (ProgressBar) | pulp track textures, skewed flat fill | value carriers under the glass tube decor: Story HP 30, Fighter HP 44, Fighter Ultimate 16, stat 14, boss 34 | `background` → `StyleBoxEmpty`; `fill` → a `StyleBoxFlat` with `draw_center = false` that holds the fill colour, so `FighterHUD.TintFill` keeps working and the decor reads it | keep |
| `DialogueBodyRich` (RichTextLabel) | T49, Outfit 500 | T49, Outfit 400 33 px | re-value | keep |
| **new** `ReadyBadge`, `ReadyBadgeIdle` (PanelContainer) | — | G12 (6.2 `StyleBoxFlat`) | add | keep |
| **new** tab styles (`GlassTab`, `GlassTabSelected`, or the `TabContainer`'s own styles) | pulp tabs | G14 | add | keep |
| `PaperPanel`, `BannerPanel`, `CoverBannerPanel`, `BossNameplate`, `BossPhaseTab`, `SlotPlate`, `SlotPlateReady`, `KeyTagPlate`, `CardPlate`, `CardPlatePick`, `BubblePlate`, `StampPlate` | pulp-only plates | none | unchanged (still on the pulp art, so unrestyled screens render) | retire |

**Label roles (49 today).** Sizes are the spec's, rounded to the nearest whole pixel (21.6 → 22, 31.68 → 32, 37.5 → 38, 23.1 → 23, 19.8 → 20, 25.5 → 26, 20.7 → 21, 24.48 → 24); TG-THEME lists each. The halo tiers of 2.4 live on the roles: halo L on T01, T03, T04, T30, T40; halo S on the scene-text roles (T05, T06, T09, T13, T14 on the scene, T15, T17, T18 in Fighter, T19, T20, T21, T28, T29, T31, T32, T33, T44, T59, T60); none on the plate roles. Widgets that draw their own text (button labels, lens numbers, tube values) take the role's font, size, colour, outline and shadow from the theme at draw time.

| Roles | Phase A | Closeout |
|---|---|---|
| **Kept, glass values** (role unchanged): `SmallLabel` (micro, Outfit 500 18 `Ink2`); `HeadingLabel` (section heading inside a slab, 9.1: Cinzel 700 26 .13em `Accent`); `EmphasisLabel` (the scale role); `TitleLabel` (panel title inside a slab, 9.1: Cinzel 900 36 .05em `Ink`); `CoverTitle` T01; `CoverBanner` T02; `BannerHeading` T03; `PauseTitle` T04; `LevelTitle` T05; `ObjectiveLine` T06; `PanelTag` T07; `HeroName` T08; `HeroNameScene` T09; `PromptKey` T12; `PromptCaption` T13; `CaptionLabel` T14 on glass; `SceneCaption` T14 on the scene; `StatusLine` T16; `HudNumeral` T17 (the Fighter numeral, 32); `HudNumeralSmall` T17 (the Story numeral 22, the Fighter "/max" 21); `SlotKeyTag` T18 (now a plain label); `SlotNumber` T19 (19 px for the 54 px lens; the slot draws 0.36 × its own lens size); `StatusName` T20; `StatusTime` T21; `DefyLabel` T22; `RewindCount` T23; `StateLabel` T25; `DustFloat` T27; `BossName` T30; `BossPhaseTag` T31 (now a plain label); `BossHpValue` T32; `NoteLabel` T33; `TimerDigits` T34; `CardName` T35 (the tile name); `CardStamp` T37 (the READY badge text); `CardQuestion` T38; `SideCardName` T40; `StatLabel` T41; `StatValue` T42; `HintLine` T44; `HintLinePlate` T44 on the glass tag plate (the credits skip hint); `StickerLine1` / `2` / `3` T45 / T46 / T47 (the Continue card's lines); `DialogueBody` T49 (Outfit 400); `SpeakerName` T50 | re-value to spec 2.3 | keep |
| **Retired** (pulp-only): `CardDates` T36, `BubbleText` T39, `StarBadgeText` T43 | unchanged (TG-SELECT stops using them) | delete |
| **New**: `VersionCaption` T15; `DustTotal` T26; `IntegrityPercent` T28; `IntegrityCaption` T29 (halo S with shadow outline 6); `HudNumeralMax` (the 18 px "/max" of the Story HP and the boss value, AC3); `SlotKeyTagScene` (T18 over the scene, Fighter); `MenuTagline` T51 (used by `loading_rift`, 9.2; the menu shows no tagline, R3); `SettingsRowLabel` T52; `SettingsRowValue` T53; `ToggleText` T54; `TabLabel` T55; `AbilityName` T56; `RandomWord` T57; `SelectCountdown` T59; `UltimateReadyLabel` T60; `BodyLabel` (9.1 body: Outfit 400 26, line height 1.35); `BodySmallLabel` (9.1 small copy: Outfit 400 22) | add | keep |
| Not created: T24 (= `CaptionLabel`), T48 (= `CaptionLabel` / `SceneCaption`), T58 (no key; not shown) | — | — |

**Theme types** (TG-THEME): `HSlider` / `VSlider`, `ScrollBar`, `TabContainer`, `PopupMenu` (a glass slab with 44 px rows), `TooltipPanel`, `CheckBox` focus, the separators: per spec 3.3 and 6.2 (AC22, AC26, AC27). `CheckButton` stays unthemed (`PulpToggleDecor` draws it). The slider keeps UR-POLISH's rule that the track's slanted end hides under the grabber at the maximum value.

### 2.3 TG-ART — the glass art kit and pipeline (Phase A, A1)

**Goal.** Every texture, icon and gradient the spec calls an asset, built reproducibly from sources in the repo, catalogued for the generator and `PulpKit`; plus baked idle frames for the select's hero sprites.

**Spec.** 0.3, 1.3–1.4 (the build fills baked into the nine-slices), 3.0, 3.5, 6.3 (glows), 6.4, 6.6 (minimum strokes), §7, Appendix A.

**Depends on.** Nothing: cut from `main` after Phase 0, in parallel with TG-THEME step 1; merges first. **Shared files.** `tools/asset_src/jobs.json` (appends its two groups only).

**Build.**
- `tools/asset_src/glass/icons_src/`: copy the 52 SVGs from `glass_spec_assets\icons\` as committed sources; author `beacon_anchor_hollow` (regular and bold, §0.4 P4).
- `glass_svg.py`: SVG sources for every 6.4 nine-slice (`glass_panel`, `glass_tag`, `glass_btn_normal` / `_focus` / `_selected` / `_pressed` / `_disabled`, `glass_tab`, `glass_tab_selected` and a focus variant, `glass_field` and `glass_field_focus`, `glass_slider_grabber`, `glass_diamond_11`, `glass_diamond_12`) in the ThorVG-safe subset (paths, linear and radial gradients, opacity), with margins computed from the geometry; jobs appended to `tools/asset_src/jobs.json` (groups `glass`, `glass_icons_raw`), idempotent. The focus box follows §0.4 P5.
- `build_glass_images.py` (PIL): the soft parts ThorVG cannot draw (the panel shadow, Gaussian σ 11, #00020C @0.60 at (0, 14); the 14 px focus glow; the grabber and diamond glows; each icon's glow, a blurred copy of its alpha at σ 2.5 bake px and 60 %, with the coloured block-pip and Defy-ready glows of 7.2); the `GradientTexture2D` resources of 7.3 (`glass_lens_radial`, `glass_badge_radial`, `glass_dial_radial`, `glass_portrait_radial`, `menu_scrim`, `select_vignette`, `fighter_top_shade`): engine resources, no script.
- Icons: one white texture per glyph and weight at 64 × 64, regular and `_bold` (7.2), glow baked, tinted by `modulate`.
- `build_select_heroes.py`: the four idle frames of every roster member (the roster read from `resources/Content/content_manifest.csv`, never listed), cut from the retro v2 atlases through `tools/retro_sprites/manifests/<id>.json`, trimmed, with each frame's feet line and pivot and the idle frame rate in `select_heroes.json`, mipmapped. Reason: loading a hero's `SpriteFrames` loads all six of its atlases (41–51 MiB a hero).
- `write_catalog.py` → `assets/sprites/ui/glass/glass_kit.json` in the shape of `pulp_kit.json` (file, size, nine-slice margins, expand margins, mipmaps, axis stretch, what it serves). Every glass stem carries a prefix (`glass_`, `ico_`, `hero_`) so none collides with a pulp stem.
- `apply_import_settings.py` (mipmaps on every glass PNG, 6.7 item 5; two `--import` passes, as the pulp chain does); `build_contact_sheet.py` (a review sheet, and the nine-slices stretched to the spec's sizes: a 560 × 68 chevron in every state, a 620 × 214 and a 600 × 768 slab, tags) into `D:\Projects\ftt-scratch\tg-captures\art\`; `build_all.ps1` (the chain, with the lock note).
- `PulpKit.cs`: reads `glass_kit.json` first and the frozen `pulp_kit.json` second; `Has`, `Plate`, `Texture`, `Expand`, `SharedPlate` keep their signatures.
- No theme, scene or screen-script edit. Commit the sources, PNGs, their new `.import` sidecars (mipmaps), the catalog and the `.tres`; never the `.import` line-ending churn of other files.

**Tests (declared delta +5).** New `tests/ContentValidation/GlassKitContentTests.cs` (`[RequireGodotRuntime]`): (1) every catalog file loads at its stated size, its sidecar has mipmaps on, and no glass stem collides with a pulp stem; (2) every nine-slice leaves a centre slice and carries its 6.4 margins or a recorded deviation; (3) every icon name has a regular and a bold 64 × 64 texture; (4) every icon name the HUD models produce (`HudAbilityIndicatorModel.StatusIconName`, `DefySealModel.IconName`, `FighterHudModel.StatusIconName` / `DefyIconName` for every state) exists in the glass set; (5) every roster member has four baked idle frames and a feet line. `PulpKitContentTests` and `AbilityIconLibraryTests` are untouched.

**Acceptance.** Build clean; `GlassKit`, `PulpKit`, `AbilityIcon` and the UI filter green; a second run of `build_all.ps1` reproduces the outputs; the contact sheets compared against the crops and Appendix A and the differences reported; every 6.6 minimum stroke met.

**Handoff.** `docs/handoffs/TG_ART.md` (the catalog table, the icon list with tints, how to rerun, deviations from 6.4).

### 2.4 TG-THEME — palette, roles, styles, motion, the transition, the capture tool (Phase A, A1 and A2)

**Goal.** The generated theme carries the glass look and every role of §2.2; the palette carries the glass constants; motion runs MOT-A; the screen transition is the dissolve; the capture tool can compare against the mockups.

**Spec.** §1 (palette), §2 (typography, halos, conversions), 3.2–3.3 (button states, settings widgets as styles), §5, 6.1–6.3, 6.7, 6.8.

**Depends on.** Step 1 on nothing (parallel with TG-ART); step 2 on TG-ART's merge. Merges second. **Shared files.** Only cross-edits (`DialogueTheme.cs` if needed); `UIThemeTests`, `ScreenSceneStyleTests` and two `UrPolishTests` methods are its own in Phase A.

**Step 1 (parallel with TG-ART).**
- `UIPalette.cs`: add the glass constants of spec 1.6 with their exact values (`Edge`, `EdgeHi`, `EdgeDim`, `Focus`, `Hp2`, `GlassFillTop`, `GlassFillBottom`, `ButtonFill`, `ButtonFillFocus`, `TileFillTop` / `Bottom`, `LensFill`, `BadgeFill`, `DialFill`, `TubeTrackTop` / `Bottom`, `SweepShade`, `TickLine`, `PanelShadow`, `PipGlow`, `DefyReadyGlow`, `BossEdge`, `BossTracker`, `TabSelectedInk`, `ScrimMenu`, `TopShade`, `PauseDim` and the others listed); keep every existing constant (the pulp-only ones marked legacy, retired by the closeout); move the theme mirrors (`PanelBackground`, `PanelBorder`, `Button*`, `FocusOutline`, `TextPrimary`, `TextAccent`, `TextDisabled`, `TrackBackground`) to glass values. `Cyan`, `CyanDim`, `UnboundCold*` and the grammar equalities are unchanged.
- The generator: the palette block, the roles of §2.2 at the 2.3 values (face, drawn weight, tracking as `spacing_glyph`, colour, halo tier as outline and shadow constants, `tnum` on the 2.5 numeral roles, line spacing where 2.3's line height needs it), the variations of §2.2, and every style in the 6.2 `StyleBoxFlat` fallback form (bevels by `corner_detail = 1`, parallelograms by `skew`), so screens stay usable before the art lands. Hover = focus (AC8); the focus overlay (§0.4 P5). The palette lives in one table (R11).
- `scripts/UI/Motion/`: MOT-A in `PulpMotionRules` (focus +14 px over 120 ms, Quad Out, no tilt, no scale; pressed +6 px and × 0.985 about the centre; panel in: alpha and a 24 px rise over 220 ms Cubic Out with a hook for the light band; panel out: 160 ms Quad In and a 12 px sink; Reduced: 150 / 120 ms fades; tracker: hold 500 ms, drain 600 ms Sine InOut, healing rise 200 ms, Reduced: fade 200 ms; ready bloom: modulate 1.35 and the glow at 1.5 × by 300 ms, settled by 600 ms, once; the low-HP pulse 2 s; the dust float; the pause dim 160 ms; select cross-fades 120 ms; the phase label 220 ms; the anchor split 600 / 150 ms; list scroll 120 ms). `FocusPose` keeps its mechanism (inner visual, re-applied after a container sort, exact identity on release, a snap under Reduced). `PulpMotion.PopIn` / `PopOut` keep their names (callers) and become the fade-rise and fade-sink; `TrackerDrop` keeps its name and drains smoothly; new `Bloom`; delete `Thunk`, `ThunkValues`, `Stepped` (no caller); keep `HitFlash` and `HitFlashLimiter` until the closeout.
- `ScreenWipe` keeps its class, node name and API (`EnsureFor`, `BeginCover`, `BeginReveal`, `PlayThrough`, `Release`, `IsCovered`, `IsActive`, `CoverSeconds`, the events) and becomes MOT-A row 6: a full-screen `Backdrop` (#05071A) layer fading 0 → 1 over 200 ms and 1 → 0 over 200 ms (Sine InOut), the same under Reduced Temporal Effects. `GameManager.cs` is not edited.
- The capture tool: glass specimens in `UiCaptureTargets.Theme.cs` (the existing five targets, `theme_labels_small`, `theme_labels_large`, `theme_controls`, `theme_palette` and `main_menu_root`, keep their names; add `theme_glass_components`); new `tools/ui_theme/compare_capture.py` (a capture against a `rec_*` render or crop: side by side and a 50 % overlay at 1:1, written beside the capture); `tools/ui_theme/README.md` updated.
- `ScreenSceneStyleTests` drops its `temporal_glass_` ban (Phase B wires that art). Cross-edit `DialogueTheme`'s constants if `DialoguePresentationTests` needs it.

**Step 2 (after TG-ART merges; merge `main` first).** Wire the `glass_kit.json` nine-slices into every style (6.4 margins and expand margins; content margins = the paddings; one chevron texture per state serving all three button variations at 58–70 px), regenerate, re-run.

**Tests (declared delta +2).**
- `UIThemeTests` 12 → 14. Keep `EveryKeyboardNavigableTypeDrawsAFocusStyle`, `TheShippedFontsAreTheDefaultAndTheFallbackSlotStaysEmpty`, `OwnershipColoursReadTheCombatSlotPaletteAndChromeIsGold`, `BothShippedFontsCoverEveryCharacterTheEnglishStringsUse`, `ApplyThemeAdoptsTheSharedThemeWithoutOverwritingALocalOne`. Update `ThemeCarriesTheStyleEntriesScreensDependOn`, `AuthoredColoursMatchTheUIPaletteConstants`, `LabelTypeVariationsMatchTheUIPaletteTypeScale`, `EveryLabelRolePinsItsFaceAndWeight`, `EveryPulpVariationExistsOnItsBaseType`, `TheThemesTextPairsClearTheContrastFloor` (the 1.5 pairs composited over white behind the build fills). Rewrite `TheFoldedKitGapsCarryTheirSpecSizesAndPlates` as the glass panel presets carrying their 3.1 paddings. Add `EverySceneTextRoleCarriesItsHaloTier` and `TheGlassPaletteConstantsMatchTheSpecAndTheGenerator`.
- `PulpMotionRulesTests` 4 → 3: delete `ANumberThunkIsThreeEqualSteps…` (`Thunk` deleted); rewrite `SteppedProgressKeepsItsEndpoints…` as the tracker hold-and-drain curve with the Reduced fade; rewrite `TheTimingsAreTheSpecs…` with the MOT-A values (the once-a-second intent kept); keep `TheHitFlashLimiter…` until the closeout.
- `PulpMotionTests` 10 → 11: update both focus-pose cases (14 px, 120 ms, no tilt or scale, the pressed transform); keep `AContainerSortDoesNotLoseTheFocusPose`, the paused and the released cases, and the hit-flash case (until the closeout); rewrite the panel pop case as the fade-rise (Reduced: alpha only); rewrite the two wipe cases as the dissolve (covers, raises `Covered` while covered, reveals; the same stable fade under Reduced); delete the thunk case; add `APanelFadesOutSinkingTwelvePixels` and `TheReadyBloomRisesOnceAndSettlesWithNoLoop`.
- In place, no count change: `UiCaptureRunnerTests`, `ScreenSceneStyleTests`, `UrPolishTests`' two TG-THEME methods, `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation`.

**Acceptance.** Build clean; the UI filter green; the generator is the only writer of its two outputs; contrast and halo pins can fail (show it); the `theme_*` captures compared with 3.0–3.3 and the crops.

**Handoff.** `docs/handoffs/TG_THEME.md` (the token table, the role table with rounded sizes, the variation list, the motion API, the transition hook, the capture and compare commands).

### 2.5 TG-WIDGETS — the widget kit in glass (Phase A, A3 and A4)

**Goal.** Restyle every shared widget to the glass spec with its API, node paths, caching and disposal fixes intact; add the shared glass draw parts; delete the halftone corner.

**Spec.** 0.3, 3.1–3.6, 3.7 (the tile), 6.1–6.3, 6.5 (the title shader), 6.6, 10.1.

**Depends on.** Step 1 on TG-THEME's merge; step 2 on its own step-1 merge, and it carries Codex review 1's fixes. **Shared files.** `scripts/UI/Motion/` additively; the cross-edits in `PausePanel.cs` and `PausePanelTests`; `UiShaderModulationScanTests` and two `UrPolish2Tests` methods are its own.

**Step 1 (cut after A2): what every screen needs.**
- `GlassGeometry` (new, pure static): the chamfer polygon (N at top-left and bottom-right, with 3.1's inner-edge adjustment), the chevron (a fixed 30 px run at any height), the parallelogram (run R), a fill quad clipped to a parallelogram (slanted anchored end, vertical moving end; mirrored), a circle polygon with UVs, the CSS angle-gradient `t` of 0.2 and two-stop vertex colours.
- `GlassSheen` (new decor): the sheen and the two streak bands on the 112° line, clipped to the inner chamfer polygon and sized from the panel's own rect (3.1); implements the panel-in light band hook.
- `ScreenStreak` (new): the two `dir-a` bands of 1.4, for full-screen menus only. `GlassFlourish` (new): rule, diamond, rule (3.7, 3.8). `TitleGradient` (new) with `assets/shaders/ui_title_gradient.gdshader` (6.5): one `ShaderMaterial` per label, uniforms from the label's font metrics and the UI scale, re-applied on a theme change, flat #DFE8FF fallback.
- `GlassTileDecor` (new): the tile slab for the select, the stage phase, the drill picker, the character sub-screen and the Resonance chips: chamfer N, the fill gradient, the edge per state (normal, focus / hover, owner P1 / P2 / both from `UIPalette.P1` / `P2` / `OwnerOnDark`, locked) and the glow behind; the content is the screen's.
- `PulpButtonDecor` → BTN-A (3.2): the diamond marker (outline; filled with a white outline, a 4 px ring and a 16 px glow when focused; filled `Accent` when selected; dashed at 0.5 when disabled); the label at x + 58 (compact + 52) ending at W − 54 (compact − 40), drawn upper-case (the kept mechanism); the prompt badge (a 36 px circle with a 2 px border in the label colour and the confirm glyph, or the 3.4.4 key-cap) on the focused row only; per-state label colours; the selected look from `glass_btn_selected`; shrink-to-fit (floors 26 / 20 px) then ellipsis; `GrowToFit` kept; the D6 stack-width rule as a helper; heights scaled with the UI scale (6.7 item 8); hover = focus.
- `PulpIcon` → ICN-A: one white texture per glyph tinted by `Tint`; the bold weight below 30 px (AC23); legacy-name aliases for the transition (`anchor` → `beacon_anchor`, `anchor_hollow` → `beacon_anchor_hollow`); a name with neither a glass icon nor an alias (`anchor_pip` and `anchor_pip_cracked` until TG-HUDS draws its pips; `marker` and `prompt_circle`, which the decor and the prompt bar now draw in code) still draws its pulp layers through `PulpKit`'s fallback, so no screen loses an icon before Phase B reaches it. The closeout deletes the aliases and that fallback. `ShadowEnabled` and `Flat` apply only to a pulp fallback draw and are removed by the closeout.
- `PulpPromptBar` / `PulpPromptGlyphs` → the glass glyphs of 3.4.4 (device switching unchanged). `PulpTagPlate`, `PulpTaggedPanel`, `PanelTagAnchor` → the glass tag at (26, −11), no tilt, the clamp rule kept. `PulpToggleDecor` → G13 (3.3, 6.2). `PulpPortraitRoundel` → the G5 chamfered frame at any size (112 / 110 / 86 / 150 / 320), the art at 142 % offset (−21 %, −6.8 %) clipped by a UV polygon; delete `ui_circle_mask.gdshader`.
- `PulpScreenKit` (both parts): `DressPanel` attaches the sheen (it attached the halftone); a 14 px button-stack inset (D13); `AddPromptBar` (full-screen bottom-right at 70 / 44; overlays bottom-centre at 34); `MakeScrim` (the pause dim, matching `ScrimPanel`); `StylePauseButton` (a 560 × 64 chevron); `FocusRowGap` = max(34, `FocusTranslateX` + (`FocusScale` − 1) / 2 × `RowButtonMaxWidth` + the focus plate's right expand margin from `glass_kit.json` + 2), derived, never typed (the pulp formula's left expand is dropped: the slide moves right, so the left glow never reaches a neighbour); `SeparateFocusRow` kept; new `AddScreenStreak`, `AddMenuScrim(mirrored)`, `AddFlourishHeading`.
- Delete `HalftoneCorner.cs` and `halftone_corner.gdshader` (and their `.uid`s); cross-edits in `PausePanel.cs` and `PausePanelTests` (halftone → sheen).
- `UiShaderModulationScanTests`: discover every `assets/shaders/*.gdshader` except `outline_glow.gdshader` (combat, named with its reason) instead of a fixed list.
- Captures (`UiCaptureTargets.Widgets.cs`): buttons in five states plus focused-pressed, on keyboard and pad; icons in both weights; tags; toggles; slabs with the sheen; tiles in every state; portrait frames; the title gradient; the streak.

**Step 2 (merge `main` first): the gauges.**
- `PulpBarDecor` → the glass tube (3.4.1, 3.6): the track (edge and inner gradient), the tracker, the Rally echo band (D1: 22 % inset), the fill as a vertex gradient from the anchored to the moving end, the top-46 % highlight, the 3 px cap with its soft glow, the decile ticks. Styles: HP (and low HP), Meter (full: `Res` → `Res2` with its glow), Stat (no glow), Boss (the boss edge, an opaque track, a white tracker, no cap or ticks). Mirrored by the bar's `FillMode` (`EndToBegin` mirrors the lean and everything else, R6). The low-HP pulse is the 7 px `Danger` glow at 0.5 Hz, steady under Reduced. The fill colour is read from the bar's fill `StyleBoxFlat.bg_color` (`TintFill`). `DropLagTo` uses TG-THEME's tracker motion. Delete `pulp_bar_layers.gdshader`.
- `PulpAbilitySlot` → the lens (3.4.2): art inset 9 % and clipped to the circle; the sweep; the readiness ring (3 px with its outer edge 6 px out; the Ultimate's 5 px, 9 px out, with the gradient arc and the `Res2` / `Res` / `Res2` ready ring); the state table (ready, cooling, dormant, suppressed, Ultimate charging, ready, suppressed); the centre text (`FormatSeconds` kept, the percent, READY in T60); the key label T18 as plain text; the lock overlays; the single 600 ms bloom on entering ready. `ui_desaturate.gdshader` stays for saturation and grayscale.
- `PulpStatusChip` → the G7 badge (44 / 40) with the bold status glyph at 62 % in the status colour, the 3 px duration ring 5 px out and the text column; empty = the G8 ring with the text column at 0.35.
- `RadialProgress`: additive only (what the lens needs, for example a vertex-gradient arc). `NotYetFlash`: a circle mode (a contracting 2 px `Danger` circle around a lens, 0.25 s) beside the frame mode.
- Captures (`UiCaptureTargets.WidgetsGauges.cs`): tubes in every style and both directions with low HP and the echo; lenses in every state at 54 / 56 / 74 / 92; chips; the not-yet circle.

**Tests (declared delta: step 1 +9, step 2 +5).**
- Step 1. `PulpButtonDecorTests` 12 → 13: rewrite `TheMarkerTintAndShadowFollowTheSpecTable` (the diamond per state) and `SelectedSwapsToThePaperPlate…` (the glass selected look); update `CompactButtonsGetTheSmallMarker`, `TheBadgeShowsOnlyOnTheFocusedRow…`, `ADisabledButtonDimsItsMarker…`, `TheButtonsOwnGlyphsAreTransparent…` (glass colours), `AFocusedLongLabelReservesTheBadge…` (shrink, then ellipsis); keep the other five; add `AStackSharesItsWidestRequiredWidthUpToItsCap`. `PulpIconTests` 3 → 5: rewrite the two layer cases for the regular and bold textures; keep `AnUnknownNameIsSafe…`; add `ALegacyNameResolvesThroughItsAlias`, `BelowThirtyPixelsTheBoldWeightIsDrawn`. Update in place: `PulpPromptBarTests` (3), `PulpTaggedPanelTests` (the tag case), `PulpToggleDecorTests` (2), `PulpScreenKitTests` (rewrite the halftone case as the sheen added once; update the button-stack inset case), `PulpPortraitRoundelTests` (rewrite: the chamfered frame), `UiShaderModulationScanTests`, `UrPolish2Tests`' two TG-WIDGETS methods (the tag clamp without tilt; the posed rect = slide and glow), `PausePanelTests` (cross-edit). Delete `HalftoneCornerTests` (2). New `tests/Pulp/GlassGeometryTests.cs` (4: the chamfer, the chevron's fixed run, a clipped fill's vertical moving edge and its mirror, the angle-gradient mapping). New `tests/Pulp/GlassDecorTests.cs` (4: the sheen and streak bands scale with the panel; the screen streak's bands at 1.4's positions; one title-gradient material per label following the UI scale; a redraw without a change rebuilds no geometry in any step-1 draw widget). +1 +2 −2 +4 +4 = **+9**.
- Step 2. `PulpBarDecorTests` 3 → 6: keep `TheFractionsAreClampedToTheBarsRange` and `TheDecorOverlaysTheBarWithoutTouchingItsFill`; update the low-glow case (0.5 Hz, steady under Reduced); add `TheTubeLeansAndAMirroredBarMirrorsItsLeanAnchorGradientAndCap`, `TheEchoBandRunsFromTheFillToHpPlusEchoInsetTwentyTwoPercent`, `TheBossTubeDrawsTheBossEdgeAndNoCapOrTicks`. `PulpAbilitySlotTests` 4 → 6: update the state table and the built-parts cases; rewrite `TheCentreNumberAndTheArtInsetFollowTheSpec` (9 %, 0.36 × lens); keep the countdown case; add `TheReadinessRingSitsOutsideTheLensAtItsSpecWidth`, `AReadyStateBloomsOnceAndARedrawWithoutAChangeRebuildsNothing`. Update `PulpStatusChipTests` (2) in place. **+5**.

**Acceptance.** Build clean; the `Pulp*`, `Glass*`, `Pause*`, `UiShader*` suites and the UI filter green; every widget's public API source-compatible (existing callers compile untouched, apart from the listed cross-edits); draw paths allocate nothing (the cache cases plus Codex review 1); captures compared against the crops and 3.0–3.4.

**Handoff.** `docs/handoffs/TG_WIDGETS.md`, one section per step (API and snippets as `UR_WIDGETS.md` gave them, the derived `FocusRowGap` with its numbers).

### 2.6 Common to every Phase B workstream

- Restyle every surface you own to the spec sections named in your brief. Keep every node name, path and type a test or script binds to; an intentional structural change is listed together with its test change.
- Keep the layout, UI-scale and focus behaviour and the tests of standing decision 8.
- Use §2.2's variations and the TG-WIDGETS widgets; no font-size override anywhere; a kit gap is filled scene-locally and listed (standing decision 9).
- Remove from your files every use of a retired variation or role (§2.2), a pulp stem, an icon alias, a legacy `UIPalette` constant and `PulpMotion.HitFlash`; the closeout's purge depends on it.
- Upper-case through `Label.Uppercase` and the decor; halos through the roles. Glass slabs carry the sheen (`PulpScreenKit.DressPanel`). Full-screen menus carry their art (`temporal_glass_*_background.png`), their scrim and the screen streak; overlays carry the 0.70 dim and no streak (9.1, §0.4 P3).
- Motion through `PulpMotion` and `FocusPose` (MOT-A); no tween on a container-managed control.
- Captures (standing decision 15): your existing target names re-pointed at the glass screen, new targets for new states, 1.0 and 1.4.
- **Common acceptance**, on top of each workstream's own: the agent gate of §3.1; no font-size override in your scenes or scripts (the existing no-override pins and scans stay green); no allocation, `GetChildren()` or undisposed engine collection in your draw and per-frame paths (standing decision 11); no new chrome cyan (`UiChromeCyanSourceScanTests`); every surface you own captured and compared; the handoff lists what was not seen.

### 2.7 TG-MENU — MENU-A and the main-menu sub-screens (Phase B, group 1)

**Goal.** The main menu as MENU-A (`rec_01`) and its sub-screens in the glass parts, with the root's names, focus chain and Continue rules intact.

**Spec.** 3.8, 4.1, 9.1, 9.2 ("Main menu sub-screens"), 10.2.

**Depends on.** TG-WIDGETS step 1 (cut from its merge); third in the tie order. **Shared files.** An `en.csv` block only if unavoidable; nothing else shared.

**Build.**
- Root: the art (`temporal_glass_main_menu_background.png`, cover), the menu scrim (`menu_scrim.tres`), the title lock-up at (110, 64) (flourish; "FIGHTERS" T01 with the title gradient; the middle word T02 and its rule; the last word T01; R2, R3), the chevron column bottom-left (left 96, bottom 983; rows 68, pitch 79; width by the D6 rule, cap 700; R1), the version T15 bottom-left, the Continue card bottom-right (`GlassPanelContinue`, 600 wide, R5; 86 px portrait frame; focusable: Right from Story Mode, Left back; resumes through `StoryManager.ResumeCampaign`; hidden with no save; the seams `SaveSlotsOverrideForTesting` / `ResumeOverrideForTesting`), the prompt bar, the streak. The UI-scale fit sequence of 4.1 (the title never above 100 %, then steps 1–5). The eight root button names and paths stay.
- Remove the pulp cover (art, rays, halftone, burst, heroes, sticker, banner); delete `MainMenuCoverInk.cs`.
- Sub-screens: slots (focusable slabs 1000 × 140 with portrait frames; `SlotRow{0-2}/SlotButton|DeleteButton` kept), character (3 × 3 glass tiles and a side slab), difficulty (chevron rows, the description under the column), level select (4 × 4 compact chevrons), extras, licences (one slab, scrolling text). Each sub-screen's largest panel stays centred (`MenuLayoutBoundsTests`; only the root is allow-listed).

**Tests (declared delta −1).** `MainMenuCoverTests` 11 → 12: rewrite the four sticker cases as Continue card cases (hidden with no save and out of the chain; hero, place and meta of the most recent save; pressing it resumes; a completed save and an unknown scene path); rewrite the title case (three lines from two keys, the split rule, the gradient material on both title words); rewrite the pitch case (the bottom-anchored column and the fit sequence keep it 24 px under the title with six, seven and eight rows at 1.0 and 1.4); update the root-button, sub-screen decor, prompt-bar and scene-scan cases (no pulp art reference; the glass background); delete `TheThreeHeroesAreInkedPoses…`; add `TheContinueCardIsReachedWithRightFromStoryModeAndLeftReturns`, `TheMenuArtCarriesTheScrimAndTheScreenStreak`. Delete `MainMenuCoverInkTests` (2). In place: `MainMenuSceneTests` (title paths), `CalibrationDrillsRouteTests` (variation unchanged, so likely untouched). +1 −2 = **−1**.

**Acceptance.** The menu filter (`MainMenu`, `MenuLayoutBounds`, `CalibrationDrillsRoute`, `PadMenu`) and the UI filter green; `menu_cover` compared with `rec_01` and the four `menu_*` crops; the sub-screens checked against 9.2; 1.4 captures of the root and the slots.

**Handoff.** `docs/handoffs/TG_MENU.md`.

### 2.8 TG-SELECT — CARD-A, the stage phase and the Holodeck console (Phase B, group 2)

**Goal.** The Fighter select as CARD-A (`rec_02`), with its hero sprites, its stage phase and the Holodeck console, keeping the per-player cursors, the footer-only focus chain, join prompts, countdown and mirror picks.

**Spec.** 3.7, 4.2, 9.2 (the Holodeck console and the stage phase), 10.2.

**Depends on.** TG-WIDGETS step 2 (the tubes and lenses) and TG-ART's baked idle frames; last in the tie order. **Shared files.** One row of `UnusedTranslationKeyTests`; an `en.csv` block only if unavoidable.

**Build.**
- The grid: 3 columns and a full-width Random tile (`CardGridRules.Columns` 3; a fourth row moves Random down by 180 and starts the grid higher; past four rows the tiles take the compact 140 / 152; never hard-code nine). Tiles through `GlassTileDecor`: the portrait window (art at 150 %, offset (−25 %, −4 %)), the name T35 fitted by `CardTextFit` in 156 px, the P1 / P2 / both badges and edge from the active slot palette (`OwnerOnDark`), the lock mark with `check_solid`. Tiles stay cursor-driven, `FocusMode.None`, in the skip group.
- Side heads: the slot shape, the player label, the hero name T40 with the D10 fit (shrink to 36 px, then two lines at 48), the READY badge (`ReadyBadge` with `fighter_ready_banner`; `ReadyBadgeIdle` with `fighter_select_choosing`, R4). Press-to-join prompts as built.
- Side stat panels 460 wide (`GlassPanelStats`, the style tag, three stat tubes 170 × 14 against the roster's real maxima, three 56 px lenses for Special 1, Special 2 and Movement with their names in T56, §0.4 P9). The hidden `P{n}Stats` label with `fighter_stats_card` stays (screen readers and tests).
- Hero sprites (layer 3) from TG-ART's baked idle frames: scale 2.05, feet on y 700, the idle at its own rate, the hovered or locked hero per side, P2 flipped, the soft shadow. This closes `VERIFY-UI-SELECT-HERO-SPRITES`.
- The title T03 with flourishes; the footer's three compact chevrons 280 wide, 34 px apart (§0.4 P1); the hint T44; the prompt bar; the countdown T59 at y 820; the art (`temporal_glass_fighter_select_background.png`) with the vignette and the streak.
- The stage phase: stage preview tiles 260 × 180 (`GlassTileDecor`), the Open / Sealed tag, the rules as settings rows, Back and Fight (Fight focused first). The Holodeck console as an overlay slab with settings rows.
- The `character_<id>_dates` keys and `fighter_random_mystery` stay in `en.csv` (spec T36): re-pin the `character_` dynamic prefix in `UnusedTranslationKeyTests` to the card-name builder, and record a key that becomes a true orphan on that test's roster with its reason. No row is deleted.
- Remove the pulp cards, bubbles, stars, paper cards, stamp and split art.

**Tests (declared delta +3).** `CardGridRulesTests` (3): rewrite all three (3 columns and Random; the fourth-row rule; the compact size past four rows). `CharacterSelectCardTests` 7 → 10: rewrite the grid case and the dates case (now: every tile shows its card name, Random shows "?" and the random word); update the stat case (tubes in the side panel), the mirror case (both badges, the P1 → P2 edge, both READY badges), the palette case, the text-fit case (156 px) and the stage / console case; add `TheSideHeadsFitLongNamesAndShowTheReadyOrIdleBadge`, `TheSidePanelsShowThreeStatTubesAndThreeAbilityLensesWithNames`, `TheHeroSpritesStandOnTheFeetLineFromTheBakedIdleFrames`. In place: `CharacterSelectSceneTests` (tile internals, `P{n}Name` paths), `HolodeckConsoleTests`, `PadMenuFighterSelectTests`, `UnusedTranslationKeyTests` (one row). **+3**.

**Acceptance.** The select filter (`CharacterSelect`, `Card`, `Holodeck`, `PadMenu`, `UnusedTranslation`) and the UI filter green; `select_cards` compared with `rec_02` and the five `select_*` crops; mirror pick, join prompt, countdown, stage phase and 1.4 captured.

**Handoff.** `docs/handoffs/TG_SELECT.md`.

### 2.9 TG-HUDS — HUD-S1 and BOSS-A (Phase B, group 2)

**Goal.** The Story HUD as the glass corner cluster (`rec_04`) and the boss bar as BOSS-A (`rec_05`), at the sizes already in `HUD_CONTRACT.md`, with every bound node path and the bounds rule intact.

**Spec.** 3.4, 3.6, 4.4, 9.2 (the checkpoint toast, the boss intro card), 10.2.

**Depends on.** TG-WIDGETS step 2; fourth in the tie order (before TG-HUDF, which reads the shared HUD models). **Shared files.** The `UrPolish2Tests` boss method; `HudAbilityIndicatorModel.cs` and `DefySealModel.cs` additively (TG-HUDF asks TG-HUDS, through the orchestrator, for any addition it needs).

**Build.**
- Three glass panels (`GlassPanelVitals`, `GlassPanelAbilities`, `GlassPanelTimeTools`) with light tags (`hud_vitals`, `hud_abilities`; none on time tools) at 4.4's rects. Vitals: the 112 portrait frame, the name T08, the HP tube 380 × 30 (HP style; the value T17 plus "/max" at 18 with the halo), `shield` pips at 30, the status chips 44 px, 30 apart. Abilities: the Ultimate lens 92 (ring outer 110) with READY in T60, the Defy seal 34 with its label, S1 / S2 / MOV lenses 74 at pitch 104 with key labels. Time tools: the rewind and Time Freeze badges 46 (`HudBadge` re-skinned to G7), the Beacon anchors (`beacon_anchor`, `beacon_anchor_hollow` at zero; diamond pips drawn by `AnchorPip`, the 600 / 150 ms split).
- The dust plate (`GlassPanelDust`; glyph 38; total T26; the "+N" T27 with MOT-A row 13). The thin-ring Integrity dial (G9: the wedge annulus gold, `Danger` below the tremor threshold (R8), `Ink2` @0.6 while frozen; 12 ticks; the caption centred on the dial (D3); the percent; the siphon streams as `Line2D` with a glow line under each; the sealed note).
- Scaling: the left stack as one unit about (32, 24); the right cluster about the top-right; `StoryHudBoundsTests`' rule stands.
- The boss bar: a boss-red diamond, the name T30, "PHASE N" T31 (one-based, a plain label), the glass boss tube 840 × 34, the white notch with its gold diamond (`BossPhaseNotchOverlay`), the value T32. Width = min(840 s, 1920 − 1008 s). The D11 fit: the name shrinks to 0.6 of 38 px, then the phase label drops under the name, right-aligned. The level title and objective hide while it shows.
- The checkpoint toast and the boss intro card (9.2). The not-yet flash: `NotYetFlash`'s circle mode around the lens.
- Remove every use of `BossNameplate`, `BossPhaseTab`, `KeyTagPlate`, `SlotPlate*`, the stopwatch dial art and pulp stems.

**Tests (declared delta +2).** `StoryHudPulpTests` 12 → 14: update the cluster, slot, not-yet, chip, Defy, HP, time-tools, siphon and objective cases; keep `TheFreezeStringsAreRebuiltOnly…` and `TheDialWedgeIsGoldAboveTheTremorThresholdAndDangerBelowIt` (R8); rewrite `TheBossBarIsAPulpNameplate…` as BOSS-A (the diamond-led name, the glass tube, a plain one-based phase label driven by a real `BossController` across its first threshold, the white notch with its gold diamond); add `TheSealedDialGivesWayToTheNoteAndAFrozenWedgeTurnsInkTwo`, `TheCheckpointToastAndTheBossIntroCardWearTheGlassPlateAndFlourish`. In place: `StoryHudBoundsTests` (the band rule's numbers), `UrPolish2Tests.EveryBossNameAndThePhaseTabStayApart…` (the D11 fit with the glass numbers; only "The Thunderbomb General" may still be cut at 1.4, if it is), and paths only in `StoryHudContractTests`, `StoryHudPresentationTests`, `StoryHudBeaconAnchorTests` (46 / 18 kept), `IntegrityClockTests`, `DustCounterPersistenceTests`. **+2**.

**Acceptance.** The HUD filter (`StoryHud`, `Integrity`, `DustCounter`, `Beacon`, `BossBar`, `UrPolish2`) and the UI filter green; `huds_calm` against `rec_04` and the `hud_story_*` crops, `huds_stress` and `huds_boss` against `rec_05` and `boss_bar`; 1.4, the sealed dial, the low pulse at its first frame.

**Handoff.** `docs/handoffs/TG_HUDS.md` (with drafted `HUD_CONTRACT.md` wording).

### 2.10 TG-HUDF — HUD-F2 (Phase B, group 2)

**Goal.** The Fighter HUD as the glass top versus bars (`rec_06`), with the top placement, the sizes, the Ultimate bar, the unmirrored status pair, the 1.4 clamp and `TintFill` intact; presentation only.

**Spec.** 3.4, 4.5, 10.2.

**Depends on.** TG-WIDGETS step 2 (the mirrored tube); fifth in the tie order. **Shared files.** None; it reads the Story HUD models without editing them.

**Build.**
- Clusters at the top corners (36, 22): the 110 portrait frame; the header (slot shape 30, the name T09 with the halo, "P1" T48); the HP tube 510 × 44 (P1 anchored left; P2 mirrored, R6); row 2 (stocks `star4` / `star4_filled` 28 in the slot colour, pips `shield_bold` 24, Defy 32); the Ultimate bar 390 × 16 (Meter style, full gold with its glow, no text; P2 mirrored).
- Lens rows at (46, 192), mirrored with S1 on the outer edge: 54 px lenses at pitch 74 with key labels (R9); the status pair not mirrored (damage left), badges 40, 18 apart.
- The timer plate (`GlassPanelTimer`, T34 digits, the T14 mode label; Sudden Death widens it to fit its text); the top shade (`fighter_top_shade.tres`); the HP width `clamp(510 s, 300, (960 − 117 s − 24) − (36 + 130 s))`, the Ultimate bar keeping 390 / 510 of it (`FighterHudModel`).
- No hit flash (§0.4 P7): remove the `PulpMotion.HitFlash` call and the `HitFlashLimiter` field. Remove the full-meter `FullGradient` `TextureRect` (the tube draws gradients) and every pulp stem.

**Tests (declared delta +2).** `FighterHudTopBandTests` 3 → 5: update the sizes case (the glass widgets) and the bounds case (the clamp); rewrite `TheHitFlashPlaysOnceASecondPerBar` as `AHitHoldsTheTrackerThenDrainsItAndPlaysNoFlash`; add `ThePlayerTwoBarsMirrorLeanAnchorGradientAndCap`, `TheTimerPlateWidensForSuddenDeathAndTheTopShadeSitsBehindTheBand`. `FighterHudLayoutRulesTests`: update the clamp case to the glass formula; keep the icon case. Paths only, if any, in `FighterHudSceneTests`, `FighterHudModelTests`, `FighterHudContentTests`, `FighterMatchPresentationTests`. **+2**.

**Acceptance.** The Fighter HUD filter (`FighterHud`, `FighterMatchPresentation`) and the UI filter green; nothing under `scripts/FighterSim/` touched and no `KLOTHO_DET002`; `hudf_versus` against `rec_06` and the three `hud_fighter_*` crops; `hudf_140`, Sudden Death, KO.

**Handoff.** `docs/handoffs/TG_HUDF.md`.

### 2.11 TG-PAUSE — PAUSE-A and the overlay family (Phase B, group 1)

**Goal.** Every pause surface as PAUSE-A (`rec_03`) and the overlay family in glass, with pause ownership, `ui_cancel` handling, hold-to-skip and every layout fix of standing decision 8 intact.

**Spec.** 3.3, 3.9, 4.3, 9.1, 9.2 (Settings and Controls, the dialogue box and log, Move List, Systems Card, the confirm modal and two-option prompt, the first-use tooltip, the boot overlays, the pause variants), 10.2.

**Depends on.** TG-WIDGETS step 1; first in the tie order, because other screens open its modal and panel. **Shared files.** Its three `UrPolishTests` / `UrPolish2Tests` methods; an `en.csv` block only if unavoidable.

**Build.**
- The Story pause on both build paths (one decorate path), the hub and Act III variants, the Fighter pause (tag = the stage name; status = the match mode) and the disconnect modal: the 0.70 dim with MOT-A row 14 under the process pause mode; `PausePanel` as the glass slab (`GlassPanelPause`, 30 / 20 / 28, 680 wide by the D6 rule, vertically centred, §0.4 P2); the title T04; the status strip T16 (R7); chevron rows 64 tall at pitch 73 with the 14 px inset; the glass tag with the short level title; the prompt bar bottom-centre. `PauseMenuBase`'s pause ownership is untouched.
- Settings (a 1280 × 860 overlay slab): glass tabs; settings rows 62 tall (T52 label in 360, the control, the T53 value in 90); toggles; sliders; option buttons as glass fields with the `chevron_down` glyph; the glass scroll bar; the Controls tab's binding cells as glass fields with steady listening and conflict text (the column model kept); Reset All as a compact chevron.
- Move List and Systems Card (1240 × 900 slabs, section headings, footers clear of the scroll area); the confirm modal and two-option prompt (760 wide; compact chevrons at least 260 wide and 34 px apart; labels never trimmed); the dialogue box (3.9: a 1500-wide slab bottom-centre, the 150 px frame, T50 / T49, the 34 px `hourglass_caret` in `Res2`, the silhouette per §0.4 P6; typewriter, hold-to-skip and padding unchanged); the dialogue log (1200 × 860); the first-use tooltip (a glass tag-style plate at y 140); the boot overlays (`bg0` at 0.92, an 1100 slab).

**Tests (declared delta +2).** Update in place `PauseSkinTests` (2), `PausePanelTests` (1), `OverlaySkinTests` (5), and where a pin moves `StoryPauseTests`, `LocalFighterPauseTests`, `SettingsMenuSceneTests` (structural row changes listed), `DialoguePresentationTests`, `DialogueEmotionTreatmentTests`, `ConfirmModalTests`; the three TG-PAUSE methods of `UrPolishTests` / `UrPolish2Tests` only if they fail. Add `PauseSkinTests.ThePauseDimIsSeventyPercentAndThePanelFadeRisesWhileTheTreeIsPaused` and `OverlaySkinTests.SettingsTabsFieldsSlidersAndScrollBarsWearTheGlassLooks`. **+2**.

**Acceptance.** The pause filter (`Pause`, `Settings`, `Dialogue`, `ConfirmModal`, `TwoOption`, `MoveList`, `SystemsCard`, `PadMenu`, `FirstUse`, `UrPolish`) and the UI filter green; no `SceneTree.Paused` write added; `pause_story` against `rec_03` and `pause_panel`; every overlay captured at 1.0 and the Story pause and Settings Controls at 1.4.

**Handoff.** `docs/handoffs/TG_PAUSE.md`.

### 2.12 TG-SCREENS — results, loading, credits, Game Over, the grid, the drills and the rest (Phase B, group 1)

**Goal.** Every remaining redesigned screen restyled from the spec's 9.2 rules (no mockup draws them), with the itemization, fit and flow rules intact.

**Spec.** 9.1, 9.2 (each row named below), 10.2.

**Depends on.** TG-WIDGETS step 1; second in the tie order. **Shared files.** One row of `UiChromeCyanSourceScanTests`; its two `UrPolishTests` / `UrPolish2Tests` methods; an `en.csv` block only if unavoidable.

**Build.**
- Level results (an 880 overlay slab, the short-title tag, a T03-size title with flourishes inside, the rows, the rule, the dust total in `Res`, one chevron); match results (the 0.70 dim, the heading with flourishes, the winner line T40 with the slot shape and its glow, two 600 slabs with 110 frames, compact buttons 34 px apart).
- Loading: the Story portal (`bg0`, the streak, the destination title with flourishes, `loading_rift` in T51); the Fighter VS (320 frames with a slot-colour edge and glow, "VS" in T01 with the title gradient, names T40); the plain screen (the 64 px hourglass breathing, static under Reduced). The two-second minimum is unchanged.
- Credits (`bg0`, the streak, a 1100 column, the title in T01 style at 78, headings, names, the skip hint on the glass tag plate in its band); Game Over (a 0.82 dim, a 760 slab, T04, the cold rule with a cold diamond, the body, two chevrons).
- The Resonance grid (a 1560 × 760 slab; chips through `GlassTileDecor` with chamfer 12 and an edge per state; the dormant glyph; edges as built with the satisfied edge gold (R10); the chip layout and fit rules kept); remove the `ResonanceEdgeLayer.cs` row from `UiChromeCyanSourceScanTests`.
- Calibration picker (3 × 3 tiles), list (chevrons with pass / fail glyphs), drill HUD (a 900 top slab, misses as `cross` glyphs), result slab: `CalibrationDrillRunner.cs` for presentation only. The timeline restart panel (the modal with two chevron rows); Fighter play options (the MENU-A left column and a notice slab); the network select (light pass).
- The streak on full-screen menus only (not on results, §0.4 P3, nor on overlays).

**Tests (declared delta +2).** Update in place `MatchResultsPulpTests`, `LoadingRoundelTests`, `ResonanceChipTests` (2), and, only if the chip chrome changes their margins, `ResonanceGridFitTests` (explained); the TG-SCREENS methods of `UrPolishTests` / `UrPolish2Tests` if they fail. Add `ResonanceChipTests.TheSatisfiedEdgeIsGoldAndTheOtherEdgesKeepTheirMeaning` and a new `tests/Unit/ScreenTypeDressingTests.cs` (1: every full-screen menu screen carries its art and the streak, and every overlay the dim and no streak). **+2**.

**Acceptance.** The screens filter (`Results`, `Loading`, `Resonance`, `Timeline`, `Smothered`, `Drill`, `Calibration`, `Credits`, `GameOver`, `UiChrome`) and the UI filter green; every target of `UiCaptureTargets.Screens.cs` captured and checked against its 9.2 row; the grid at 1.0 and 1.4.

**Handoff.** `docs/handoffs/TG_SCREENS.md`.

### 2.13 TG-CLOSE — fold, purge, guards, docs, runs, gallery (Phase C)

One agent in two ordered parts (the orchestrator may run them as two agents).

**C1, code.**
1. Fold every Phase B kit gap into the generator (never a hand edit) and remove the scene-local stand-ins it replaces.
2. Reference scan, then purge the pulp layer: the retired variations and roles (§2.2), the legacy `UIPalette` constants, `PulpKit`'s pulp fallback, `PulpIcon`'s aliases and no-op members, `PulpMotionRules`' pulp-only constants (for example `CardLift`, `CardTiltDegrees`), `PulpMotion.HitFlash` and `HitFlashLimiter`; move `assets/sprites/ui/pulp/ability_icons/` to `assets/sprites/ui/ability_icons/` (keeping the `.import` uids; `AbilityIconLibrary` and the builder follow, the builder into `tools/asset_src/glass/`; `AbilityIconLibraryTests` in place if it pins the folder); delete `assets/sprites/ui/pulp/`, `tools/asset_src/pulp/` and the `pulp` / `pulp_icons_raw` jobs. A `--headless --import` in the integration worktree reports no missing dependency. The main checkout carries `.import` line-ending churn on pulp sidecars; before `main` advances past the purge, run `git checkout -- assets` there (CLAUDE.md: that churn is never a change).
3. Guards: new `tests/ContentValidation/UiLegacyKitReferenceScanTests.cs` (2: nothing in `scripts/`, `scenes/`, `resources/`, `tests/` or `tools/` names `ui/pulp/`, `pulp_kit`, a retired variation, role, palette constant or icon alias, except the guard files that hold the retired list; the detector sees a planted reference and leaves the glass names alone); `UiChromeCyanSourceScanTests` gains `EveryAllowListedFileStillNeedsItsEntry` (the allow-list cannot go stale); `UIThemeTests` updated in place (the retired names absent, the folded gaps present). Delete `PulpKitContentTests` (3) and the two hit-flash cases.

**C2, docs, runs, evidence, review.**
- `HUD_CONTRACT.md` (spec 8.5): round lenses with readiness rings plus the sweep replace the square slots; the Story Ultimate ring outer 110 (pulp 116); glass panels with light tags; BOSS-A (a diamond-led name over a glass boss tube, a plain phase label, a white notch); the thin-ring dial; the empty status slot as a quiet ring; everything else of the 2026-10-26 amendment stands.
- `design-godot.md`, in the repository mirror **and** the master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`: the UI look lines the pulp closeout wrote (main menu, character select, panels, buttons, motion, both pause menus, the dialogue box, the Story HUD element lines that say "ink panels", "square slots", "stopwatch") become Temporal Glass; typography, placement and sizes are unchanged; the `DOC-MASTER-UNBOUND-BINDING` lines are not touched (diff before any re-sync).
- `AGENTS.md`: the pulp UI bullet replaced by a glass bullet that records "Pulp" as the kit's legacy name; the repository map (`assets/sprites/ui/glass/`, `tools/asset_src/glass/`, the shaders); the UI section; the baseline paragraph; a known-gaps subsection for this package.
- `CLAUDE.md`: the baseline; the authority map rows (`UI_SPEC_GLASS.md` as the look authority, this plan, `docs/handoffs/TG_*.md`, the recon); the UI captures section (gallery path).
- The ledger: a section "Confirmed open deviations — Temporal Glass rebuild, 2026-10-26": `USER-UI-GLASS-REBUILD` (round 5) superseding `USER-UI-PULP-REDESIGN`; the R1–R11 rulings and P1–P10 resolutions; `PROVISIONAL-UI-GLASS-SPEC-CHOICES` (AC1–AC28); one `PROVISIONAL-UI-GLASS-<WS>` row per workstream; `VERIFY-UI-GLASS-HUMAN-REVIEW`; `REVIEW-UI-GLASS-CODEX-1` … `-3`; closures (`VERIFY-UI-SELECT-HERO-SPRITES`, `PROVISIONAL-UI-PULP-FOCUS-HIDES-PRESSED` if met, `REVIEW-UI-PULP-CODEX-3`); `VERIFY-UI-KIT-GAPS-OPEN` re-evaluated; the pulp `PROVISIONAL-UI-PULP-*` rows marked superseded; the index rows.
- This plan's §10. Three consecutive full runs at the exact total. The final gallery (below). Codex review 3 of the closeout diff. The open questions for the user: R1–R11 and P1–P10 as reversible choices, `VERIFY-UI-QUICK-PLAY-ROW`, `VERIFY-UI-OWNER-ON-DARK`, whether the unreferenced pre-pulp glass leftovers and their `tools/asset_src/menu/` sources should go, whether the unused `character_<id>_dates` keys should go, and the hands-on pass itself.

**Tests (declared delta −2).** −3 (`PulpKitContentTests`) −2 (hit-flash cases) +2 (`UiLegacyKitReferenceScanTests`) +1 (`EveryAllowListedFileStillNeedsItsEntry`) = **−2**.

**The final gallery.** Every capture target at the closeout head, windowed under the lock, into `D:\Projects\ftt-scratch\tg-captures\final\` with an `index.txt`, plus `final\compare\`: side-by-sides of the six drawn screens against `rec_01` … `rec_06` and of each component against its crop (`compare_capture.py`), and a before / after pairing with `D:\Projects\ftt-scratch\ur-captures\main-6a840a3f\` by target name. Every PNG is looked at; what is still wrong is listed in §10, not hidden.

### 2.14 Test reconciliation

Declared `[TestCase]` deltas against the 3691 baseline; running totals in the default merge order.

| Step | Changes (count effect) | Delta | Running total |
|---|---|---:|---:|
| TG-ART | `GlassKitContentTests` +5 | +5 | 3696 |
| TG-THEME | `UIThemeTests` +2; `PulpMotionRulesTests` −1 (thunk); `PulpMotionTests` −1 (thunk) +2 (panel out, bloom) | +2 | 3698 |
| TG-WIDGETS step 1 | `PulpButtonDecorTests` +1; `PulpIconTests` +2; `HalftoneCornerTests` −2; `GlassGeometryTests` +4; `GlassDecorTests` +4 | +9 | 3707 |
| TG-WIDGETS step 2 | `PulpBarDecorTests` +3; `PulpAbilitySlotTests` +2 | +5 | **3712** (end of Phase A) |
| TG-PAUSE | `PauseSkinTests` +1; `OverlaySkinTests` +1 | +2 | 3714 |
| TG-SCREENS | `ResonanceChipTests` +1; `ScreenTypeDressingTests` +1 | +2 | 3716 |
| TG-MENU | `MainMenuCoverTests` −1 +2; `MainMenuCoverInkTests` −2 | −1 | 3715 |
| TG-HUDS | `StoryHudPulpTests` +2 | +2 | 3717 |
| TG-HUDF | `FighterHudTopBandTests` +2 | +2 | 3719 |
| TG-SELECT | `CharacterSelectCardTests` +3 | +3 | **3722** (end of Phase B) |
| TG-CLOSE | `PulpKitContentTests` −3; hit-flash cases −2; `UiLegacyKitReferenceScanTests` +2; `UiChromeCyanSourceScanTests` +1 | −2 | **3720** (end of Phase C) |
| **Total** | | **+29** | **3720** |

Rewrites in place (count-neutral) are listed in each workstream. If Phase B merges in another order, only the intermediate totals move; each merge's expected total is the previous total plus that workstream's declared delta.

## 3. Validation gates

### 3.1 Agent gate (every workstream, before handing back)

1. `dotnet build FightersThroughTime.csproj --nologo`: 0 errors and only the two known warnings (`CS8632` in the vendored GdUnit file, Klotho's `KLSG_ECS004`); a `KLOTHO_DET002` is never acceptable.
2. Filtered GdUnit runs (§3.2 plus the workstream's own filter), under the lock, all green.
3. The headless project-load check after any scene or resource edit; `--headless --import` after an `en.csv` edit or new art (the binary translation is not committed).
4. The attribute count matches the declared delta.
5. Captures taken, looked at and compared (standing decision 15).
6. The handoff written; the branch committed; no `.godot/`, no compiled translation, no unrelated `.import` churn; `main` merged into the branch.

### 3.2 The UI filter

Run it in every Phase A and Phase B agent gate, together with the workstream's own suites. It is broad on purpose: Phase A changes the theme under every screen.

```powershell
$env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
$UiFilter = "FullyQualifiedName~FTT.Tests.Pulp|FullyQualifiedName~FTT.Tests.Pause|FullyQualifiedName~UITheme|FullyQualifiedName~UiScale|FullyQualifiedName~UiChrome|FullyQualifiedName~UiShader|FullyQualifiedName~UiCapture|FullyQualifiedName~UrPolish|FullyQualifiedName~MenuLayoutBounds|FullyQualifiedName~MainMenu|FullyQualifiedName~CharacterSelect|FullyQualifiedName~Card|FullyQualifiedName~Holodeck|FullyQualifiedName~StoryHud|FullyQualifiedName~FighterHud|FullyQualifiedName~StoryPause|FullyQualifiedName~LocalFighterPause|FullyQualifiedName~Settings|FullyQualifiedName~Dialogue|FullyQualifiedName~ConfirmModal|FullyQualifiedName~TwoOption|FullyQualifiedName~MoveList|FullyQualifiedName~SystemsCard|FullyQualifiedName~Loading|FullyQualifiedName~Resonance|FullyQualifiedName~MatchResults|FullyQualifiedName~LevelResults|FullyQualifiedName~Timeline|FullyQualifiedName~Smothered|FullyQualifiedName~Calibration|FullyQualifiedName~Drill|FullyQualifiedName~PadMenu|FullyQualifiedName~FocusChain|FullyQualifiedName~Integrity|FullyQualifiedName~DustCounter|FullyQualifiedName~Beacon|FullyQualifiedName~BossBar|FullyQualifiedName~FirstUse|FullyQualifiedName~Glass|FullyQualifiedName~PulpKit|FullyQualifiedName~PulpMotion|FullyQualifiedName~AbilityIcon|FullyQualifiedName~UnusedTranslation|FullyQualifiedName~SceneVisibleText|FullyQualifiedName~ScriptTranslation|FullyQualifiedName~FighterMatchPresentation|FullyQualifiedName~ResonanceGrammar|FullyQualifiedName~ScreenType"
dotnet test FightersThroughTime.csproj --settings .runsettings --filter $UiFilter
```

### 3.3 Merge gate (orchestrator, in `fu-integ`)

Merge the branch into `main`; check the integration worktree out at the merge; `--headless --import` (touch `en.csv` first if it changed) and commit the regenerated `en.en.translation` if it moved; build; **one full run whose `Total:` equals the expected running total of §2.14** (a different total is read against CLAUDE.md's failure signatures first: a short total is signature 1, 2, 4, 5 or 7, never a pass); capture the touched targets for a smoke look; append §9.

### 3.4 Phase gates

- **End of Phase A** (3712): one full run; Codex review 1 (A1–A3, run when A3 merges) closed before group 2 is cut.
- **End of Phase B** (3722): three consecutive full runs at the exact total, orphans drained, lock held; every capture target captured from `main`; Codex review 2 closed.
- **End of Phase C** (3720): three consecutive full runs at the exact total; the final gallery; Codex review 3 closed; §10 written.

## 4. Risks

| # | Risk | Mitigation |
|---|---|---|
| 1 | Phase A breaks screens Phase B has not reached: a removed variation constant, palette constant or widget member stops the build; a removed pulp stem or icon name draws nothing. | Standing decision 10 (two-stage retirement, the pulp catalog fallback, aliases, documented no-ops, cross-edits); the merge gate runs the full suite. |
| 2 | Two branches regenerate `ftt_theme.tres` and the merge corrupts it. | One generator owner per phase; regenerate after a merge, never hand-merge; Phase B never edits the theme. |
| 3 | Heap corruption (signature 2): a scripted `StyleBox` in the theme, a disposed `Resource`, an empty script-typed array in a `.tres`. | Engine-native styleboxes and `GradientTexture2D` only; never `Dispose()` a `Resource`; omit empty script arrays (`ScriptTypedEmptyArrayTests`); `AuthoredResources` for authored data. |
| 4 | Glass draws many polygons; per-frame allocation or a redraw every frame returns (both pulp review findings). | Standing decision 11; the cache cases in `GlassDecorTests` and `PulpAbilitySlotTests`; Codex review 1 on the widgets before six screens copy the patterns. |
| 5 | Without blur, text over bright art fails contrast; a missing halo is invisible in review. | 1.4's darker fills; halo roles; `EverySceneTextRoleCarriesItsHaloTier`; `TheThemesTextPairsClearTheContrastFloor` composited over white. |
| 6 | Hairlines blur or vanish: 9-slice art at 1:1 on menus, transform-scaled on the HUDs, a Steam Deck at 0.667. | Spec 6.6 minimum strokes in the art; mipmaps on every glass PNG; the bold icon weight below 30 px; the Deck noted as not verified. |
| 7 | Cinzel 700 at 34 px with .13em tracking is far wider than pulp's Outfit labels; labels trim or rows overflow, worse at 1.4 and in a longer locale. | The D6 width rule, shrink-to-fit, `GrowToFit`; 1.4 captures; `VERIFY-PSEUDO-LOCALE-SCREENS` kept open. |
| 8 | `RadialProgress` also draws world-space channels (`RestorationFont`, `ResonanceHoldNode`); a changed default would change gameplay displays. | Additive changes only, owned by one workstream. |
| 9 | A restyle touches pause ownership or drill logic. | `PauseMenuBase` read-only; tweens under the process pause mode (signature 4); `CalibrationDrillRunner.cs` presentation-only; the drill and pause suites in the UI filter. |
| 10 | Cross-worktree contention or a stolen lock truncates a run (signature 5; an agent removed a lock it did not own on 2026-10-26). | Filtered runs only for agents; the lock protocol of standing decision 7; full runs only in `fu-integ`; never accept a total that is not the exact number. |
| 11 | The user's open editor on the main checkout writes stale settings, or a merge collides with its `.import` churn. | No Godot launch in the main checkout; `git checkout -- assets` there before the purge reaches it; CLAUDE.md's editor note. |
| 12 | Tests pass vacuously, or the counts drift. | Fail evidence for every new or rewritten case; the attribute count per branch; the exact total at every merge. |
| 13 | A leaked save slot or session from the menu tests (signature 8). | The existing seams; session restored in `finally`. |
| 14 | Loading roster `SpriteFrames` for the select's hero layer costs about 400 MiB. | Baked idle frames only (TG-ART); a test that every roster member has them. |
| 15 | The spec is silent or inconsistent in places, and agents guess differently. | §0.4's resolutions; the CSS as the tiebreaker, recorded in the handoff. |
| 16 | Scope creep into new screens or new copy. | §0.5; Appendix B key mapping; no new keys without proof. |
| 17 | Codex usage limits delay a review (as for UR-POLISH2). | Three scheduled reviews, the first at A3 while Phase B group 1 already runs; a missed review is recorded as owed, never skipped silently. |

## 9. Deviation log (append-only)

**2026-10-27, orchestrator, Phase 0.** Plan and recon committed (`196d821c`). Baseline full run in `fu-integ` at `196d821c`: `Failed: 0, Passed: 3691, Total: 3691` (2 m 5 s, lock held). The owed Codex review of UR-POLISH2 (`REVIEW-UI-PULP-CODEX-3`, gpt-6-astra, high) returned **CHANGES REQUESTED** with two findings, both verified against the code and routed to TG-WIDGETS step 1 (no separate fix round): (1) major, `PulpScreenKit.FocusRowGap` hard-codes plate expand margins 6 / 10 while the `TemporalGlassButton` focus plate expands 12 / 23, and `UrPolish2Tests.PosedRect` repeats the constants and measures the neighbour unexpanded, so it cannot fail; the glass version reads the real styleboxes' expand margins, transforms the expanded plate by the real pose with any overshoot, and its test checks both buttons' drawn bounds; (2) minor, `PanelTagAnchor` (and `PulpTaggedPanel`, `PausePanel`) place the tag inside the parent-first theme notification, before the label's cached font size updates, so a tag that crosses the clip threshold on a live UI-scale change keeps stale metrics; the fix coalesces one deferred placement after propagation, with a test that rescales the same anchor across the threshold both ways.

**2026-10-27, orchestrator, A1-A2.** TG-ART merged (`90167018`): full run `3696 / 3696` (1 m 58 s). Rulings on TG-ART's questions: the icon prefix `glass_ico_*` (the plan's `ico_` collided with pulp stems); the P5 focus split as built (overlays; pressed solved under them); the Vitals-panel gradient layout (mean edge-alpha error 0.066 against 6.4's 0.189); the disabled haze for stripes; `chevron_down` drawn in the bold weight at 18 px. Rulings on TG-THEME's step 1 questions: the five HUD bar variations stay on their pulp art until TG-WIDGETS step 2's tube decor hides the bar's own drawing on attach, and TG-CLOSE flips the theme to the empty value carriers; the chevrons' hover box is their normal box because `PulpButtonDecor` takes focus on mouse-enter (TG-WIDGETS step 1), while tabs and fields use the kit's full-look hover boxes; `TrackBackground` keeps its pulp value so the world-space Restoration Font and Resonance Hold rings do not change, and TG-WIDGETS step 2 sets `RadialProgress`'s default track explicitly. TG-THEME merged (`ce23a813`) after its step 2. Accepted from step 2 as built: the doubled glow on a hovered and focused field (mouse only), disabled fields and tabs at `DimModulate`. **Kit gaps for TG-CLOSE:** an 18 px `chevron_down` raster wired as the `OptionButton` arrow (still Godot's default); a trimmed slider-grabber canvas so a Settings slider row returns from 64 to 44 px; a quieter `glass_tab_hover` (a hovered tab now reads as selected).

## 10. Closeout

*(placeholder: TG-CLOSE writes this section)*

- **Delivered.**
- **Tests.** (the arithmetic against §2.14, every deviation from a declared delta)
- **Runs.**
- **Not delivered.**
- **What nobody has seen.**
- **The capture gallery.**
- **Open questions for the user.**
