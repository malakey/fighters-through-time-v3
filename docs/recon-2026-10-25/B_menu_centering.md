# Recon B — off-centre and off-screen menus (2026-10-25)

Read-only subagent report at `0eba848`, with a headless `--script` probe (autoloads load in `--script` mode; root sized 1920 × 1080; lock held). Probe scripts: `D:\Projects\ftt-scratch\menu-probe\probe.gd`, `probe2.gd`.

## Root cause (measured)
Four code-built panels call `SetAnchorsPreset(LayoutPreset.Center)`. That moves only the anchors to 0.5 / 0.5 and leaves the offsets at 0 (`keep_offsets` defaults to false), so the control's top-left corner sits on the screen centre and, with the default `GROW_DIRECTION_END`, it grows right and down. Measured top-left was exactly (960, 540) every time.

| Surface | Code | Measured rect | Result |
|---|---|---|---|
| Holodeck console (hub Holodeck; the Versus CPU stage phase in `CharacterSelectScreen`) | `scripts/UI/HolodeckConsolePanel.cs:94-95` | (960, 540), 640 × 629; centre off by (+320, +314) | **Broken: bottom edge y 1169, off-screen** |
| Fighter pause panel | `scripts/UI/LocalFighterPause.cs:176-177` | (960, 540), 368 × 365; centre off by (+184, +182) | **Broken: off-centre** |
| Network select layout | `scripts/UI/NetworkSelectScreen.cs:28` | (960, 540), 420 × 434; centre off by (+210, +217) | **Broken** (LAN de-scoped; the scene exists) |
| Timeline restart panel (hub Collapse choice) | `scripts/UI/TimelineRestartPanel.cs:50-52` | (650, 390), 620 × 300, centred | OK only because it also sets `panel.Position = (-310, -150)`, a hard-coded half of its minimum size; grows down/right if content grows |

The Fighter pause's Settings and Move List sub-panels were not measured.

## Fine
- **No world-space UI.** Every menu and overlay is on a screen-space `CanvasLayer`; no `follow_viewport` anywhere in `scenes/` or `scripts/`. The hub parents `HolodeckConsolePanel` and `TimelineRestartPanel` under `HubHUD` (`CanvasLayer` layer 10, `HubWorldController.cs:510-518, 799-807`); `WardenBeacon` puts the Resonance grid on layer 90 (`WardenBeacon.cs:89`); other layers: `DialogueBox` 90, `StoryPause` 99, `GameOver` 99, `Credits` 98, `LevelResults` 95, `LocalFighterPause` 95, `MatchResults` 90, plus the boot, dialogue-log and tooltip layers. `project.godot` has `stretch/mode=canvas_items` and no aspect key (default `keep`).
- **Measured centred (offset (0, 0)):** `FighterPlayOptions`, `GameOver`, `LevelResults` (anchors 0.5 with offsets and grow BOTH), `MoveList`, `ResonanceGrid`, `SystemsCard`, `BootSequenceOverlay`, `Settings` (a `MarginContainer` with 200 / 80 margins).
- Structurally safe (full-rect `CenterContainer`): `ConfirmModal`, `PauseMenu`'s fallback, `StoryPause`, `CharacterSelect`, the `CalibrationDrill*` screens. Not individually measured (0 panels or unlaid-out rects in the sweep): `CharacterSelect`, the two Calibration scenes, `NetworkSelect`, `Credits`, `MatchResults`, `DialogueLogScreen`. Hidden sub-screens read (0, 0) in the probe; ignore those.

## Likely issues (static analysis, not measured)
1. **Tall content at the maximum UI scale 1.4** (`GlobalSaveData.MaxUiScale`, `SaveManager.cs:397`; `ApplyUiScale` scales theme font sizes in place). The MainMenu root panel (`MainMenu.tscn` `RootScreen/Center/Panel`) is already 886 px tall at 1.0; at 1.4 it would exceed 1080 (a `CenterContainer` keeps it centred but clips top and bottom). The Holodeck console (629 px), the pause panels and the Resonance grid (867 px) carry the same risk. None is in a `ScrollContainer`. `Settings` has a 444 × 709 minimum and relies on its tab content staying inside the 920 px margin box. `UIPalette.ApplyUiScale` is a static C# method GDScript cannot call; a GdUnit test can measure it.
2. `FirstUseTooltipPresenter.cs:62` places its panel at a fixed `Position = (560, 150)`, width 800 — centred only at exactly 1920 wide, grows down/right. Screen-space; low risk.
3. `CreditsController.cs:202` uses `SetAnchorsPreset(CenterBottom)` on the skip hint (size not set in code; `Credits.tscn` authors offsets, probably fine). `RewindPresentationOverlay.cs:120,132` `CenterBottom` labels: same pattern, unmeasured.

## Recommended fix
1. Parent each broken panel under a full-rect `CenterContainer` (the `ConfirmModal` / `StoryPause` / `MoveList` convention): e.g. for `HolodeckConsolePanel`, a `Center` `CenterContainer` with `SetAnchorsAndOffsetsPreset(FullRect)` holding `Panel`; the same for `LocalFighterPause`'s `PausePanel` and `NetworkSelect`'s layout; for `TimelineRestartPanel`, drop the hard-coded `Position`. Cheaper alternative: `SetAnchorsAndOffsetsPreset(LayoutPreset.Center)` plus `GrowHorizontal = GrowVertical = GrowDirection.Both`.
2. Scroll or cap tall panels (Holodeck console rows, pause panel, MainMenu root panel) for the 1.4 scale.
3. A source-scan lint forbidding `SetAnchorsPreset(LayoutPreset.Center*)` without a matching offsets call.

## Regression-test design (GdUnit, `[RequireGodotRuntime]`, one suite per file)
`tests/Unit/MenuLayoutBoundsTests.cs`, modelled on `MainMenuSceneTests`, `UiScaleTests`, `StoryPauseTests`, `LocalFighterPauseTests`, `ConfirmModalTests`, `HolodeckConsoleTests`, `TimelineRestartPanelTests` (all `tests/Unit/`):
- Instantiate each menu scene, and each code-built panel parented to a `CanvasLayer`, at 1920 × 1080: `MainMenu` (cycling screens), `CharacterSelect`, `FighterPlayOptions`, `NetworkSelect`, `Settings`, `MoveList`, `SystemsCard`, `ResonanceGrid`, `GameOver`, `LevelResults`, `MatchResults`, `LocalFighterPause`, `StoryPause`; code-built: `HolodeckConsolePanel` (both modes), `TimelineRestartPanel`, `ConfirmModal`, `DialogueLogScreen`, `BootSequenceOverlay`, `CalibrationDrillPicker` / `CalibrationDrillList`.
- Await a few `process_frame`s; open pause surfaces first.
- For every visible main `PanelContainer` / `Panel`: `GetGlobalRect()` inside `Rect2(0, 0, 1920, 1080)`; centre within ~2 px of (960, 540) where centred by design (allow-list the deliberately left-aligned MainMenu root screen, 0.44 width).
- Sweep `UIPalette.ApplyUiScale` at 1.0 and 1.4 (and 0.9 if useful); restore in `finally` as `UiScaleTests` does.
- Failure idiom: `if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");`.
- Restore `GameManager.CurrentSession` and save slots in `finally`; free grouped nodes.
- A source scan that fails on `SetAnchorsPreset(LayoutPreset.Center` without a following offsets call or a `CenterContainer` parent under `scripts/UI`.
