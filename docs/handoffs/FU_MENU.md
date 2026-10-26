# FU-MENU handoff: every menu centred and on screen (2026-10-25)

## 1. Summary
Four code-built panels (hub Holodeck console and the Versus CPU stage phase, the Fighter pause, the LAN select, the hub collapse choice) put their top-left corner on the screen centre, so they sat off-centre and the Holodeck console ran 89 px off the bottom. The Resonance grid (hub Repository terminal and Warden Beacons) had the same family of bug and was worse than the recon thought: its own root control was 0 x 0, so the whole grid was centred on the screen's top-left corner. All are now centred under a full-rect `CenterContainer`, and a small shared helper shrinks any centred panel that would not fit (the 1.4 UI scale) instead of clipping it.

## 2. What shipped
- `scripts/UI/MenuSafeAreaFit.cs` (new, `MenuSafeAreaFit : Node`): the shared fit mechanism. `Attach(panel)` parks a node beside the panel (under its centring parent, so the panel's child order is unchanged). `ScaleFor(size, view)` is the pure rule: uniform scale at most 1 so the panel's combined minimum size fits the viewport minus `MarginPixels` (24) per side, floored at `MinScale` (0.5). A `Container` resets its children's scale on every sort, so when the panel's parent is a container the scale is written to that parent (grow Both, pivot at its centre = screen centre). Re-applies on resize, minimum-size change, visibility change and viewport resize. Chosen over a `ScrollContainer` because it keeps node paths, focus chains and authored layouts intact; no menu here is a long list that shrinking would hurt except the Calibration Drill list (9 rows), which at 1.4 shrinks to about 0.9.
- `HolodeckConsolePanel.cs`, `TimelineRestartPanel.cs` (hard-coded `Position` removed), `LocalFighterPause.cs`, `NetworkSelectScreen.cs`: panel under a full-rect `CenterContainer` (new node `Center`, or `PanelCenter` for the pause), fit attached. Paths change: `Panel/Layout/...` is now `Center/Panel/Layout/...` for the Holodeck console and the restart panel; the pause's `PausePanel` is found by name.
- `ResonanceGridPanel.cs:62`, `BossPhaseNotchOverlay.cs:40`: `SetAnchorsPreset(FullRect)` in `_Ready` while in the tree recomputes offsets to keep the old (0 x 0) rect; now `SetAnchorsAndOffsetsPreset`.
- `FirstUseTooltipPresenter.cs`: top-centre anchors (offsets -400/+400, top 150, grow both) instead of fixed `Position (560, 150)`.
- `CalibrationDrillList.cs`: one line attaching the fit to `List/Root` (nine rows, 1151 px at 1.4).
- Tests updated for the moved paths: `HolodeckConsoleTests`, `TimelineRestartPanelTests`, `CharacterSelectSceneTests` (one null-check path).

## 3. Tests
New `tests/Unit/MenuLayoutBoundsTests.cs` (one suite, 4 cases): the sweep (MainMenu x7 screens, CharacterSelect, FighterPlayOptions, NetworkSelect, both Calibration Drill scenes, Settings, MoveList, SystemsCard, ResonanceGrid, GameOver, LevelResults, MatchResults, LocalFighterPause, StoryPause, Holodeck console both modes, TimelineRestartPanel, ConfirmModal, DialogueLog, the boot overlay, the tooltip, at UI scale 1.0 and 1.4), the pure fit rule, a real shrink case, and the `SetAnchorsPreset(Center...)` source scan. Allow-list: `MainMenu/RootScreen` and `FirstUseTooltip` only. Arithmetic: 3529 + 4 = 3533; counted `[TestCase]` attributes under `tests/` = 3533; full run `Passed: 3533, Total: 3533`, 1 m 48 s. No case changed except the path edits above. Confirmed against unfixed code: with the five UI files reverted, the sweep and the source scan fail (the scan names all four `SetAnchorsPreset` lines; the sweep reports the off-screen panels).

## 4. Provisional numbers
Margin 24 px per side and floor scale 0.5: agent-chosen; the margin keeps a panel's border off the bezel, the floor stops text becoming unreadable (nothing here needs it today). Centre tolerance 2 px, bounds tolerance 1.5 px.

## 5. Shared files / merge notes
Touches `CalibrationDrillList.cs` (one line) and moves three test paths; no scene, `project.godot`, localization, HUD or sim file. Other agents' edits to Move List / Systems Card / Settings do not conflict. New `.uid` sidecars are committed with their scripts.

## 6. Not delivered / risks
- Sub-panels opened from the pause (Settings, Move List) are measured as standalone surfaces, not nested under the pause. `CreditsController` and `RewindPresentationOverlay` `CenterBottom` labels were reviewed, not measured. Hub-embedded placements (HubHUD layer) are not instantiated; the same classes are measured.
- The recon's "MainMenu root panel 886 px at 1.0" passes at 1.4 in the sweep, so no fit was added there.
- Nothing was seen by a human; headless geometry only.

## 7. Drafted doc changes
- AGENTS.md, UI section, add after the "Author focus..." bullet: "**Centred panels sit under a full-rect `CenterContainer`, never `SetAnchorsPreset(Center)`** (FU-MENU, 2026-10-25). That call leaves offsets at 0 and parks the control's top-left corner on the screen centre; `SetAnchorsPreset(FullRect)` on a node already in the tree recomputes offsets to keep a 0 x 0 rect (use `SetAnchorsAndOffsetsPreset`). A panel that may exceed the screen (UI scale 1.4) calls `MenuSafeAreaFit.Attach(panel)`, which shrinks its centring parent about the screen centre. `MenuLayoutBoundsTests` sweeps every menu surface at 1.0 and 1.4 and scans for the bad call."
- Ledger: none required (no deviation left open). Optional row `PROVISIONAL-MENU-SAFE-AREA-FIT`: margin 24 px, floor scale 0.5, owner Unassigned, acceptance: a human pass at UI scale 1.4 on the Holodeck console, Fighter pause and Drill list.
