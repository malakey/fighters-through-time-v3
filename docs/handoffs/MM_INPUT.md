# MM-INPUT handoff (meter and Defy update, Phase B1)

Branch `mm/input` (worktree `D:/Projects/ftt-wt/mm-input`), base `9cba2b9a`.

## 1. Shipped (exact names)
- `project.godot`: new action `gameplay_defy` (physical key F = 70, `InputEventJoypadButton` 8, device -1), placed after `ui_pause`; `ui_dialogue_log` pad event 8 -> 4 (Tab kept).
- `InputManager.Actions.Defy = "gameplay_defy"`; in `RemappableActions` after `TimeFreeze`, before `Pause`; `ActionLabelKey` -> `controls_action_defy`; the device frame builder sets `GameplayButtons.Defy` via `ReadActionPressed(Actions.Defy, ...)`; `ToButton` maps it (so `IsActionJustPressed(Actions.Defy, p)` works).
- `InputBindingSet.RepairDefyPadDefaultCollision(InputBindingSet projectDefaults)` (+ `InputBindingSet.DefyPadButton` = 8): returns true when Defy has no saved override, its pad slot is not already Unbound, and another action's resolved pad half holds button 8; then `SetUnbound(Defy, Joypad)`. Idempotent.
- `SaveManager.ApplySavedInputBindings`: calls it after `RepairGodot3ShoulderRows` (before `RestoreUnreachableUltimateChord`) and raises `SetNotice("save_notice_defy_pad_unbound")`.
- `en.csv` `# MM-INPUT block`: `controls_action_defy`, `save_notice_defy_pad_unbound`.

## 2. Decisions
- The repair runs before the Ultimate-chord restore; it is independent of both.
- No `ShortcutEnabled`, recipe or `RequiredReachableActions` entry (single button).
- The Controls row needs no code: `BindableActions()` lists `RemappableActions`.

## 3. Deviations / cross-edits
None. Files touched: `project.godot`, `InputManager.cs`, `InputBindings.cs`, `SaveManager.cs` (call + notice), `en.csv`, tests.
`InputBindingSchemaTests` and `UrPolish2Tests` needed no edit (no hard-coded action counts; the Controls rows are enumerated); both ran green.

## 4. Test delta: +7 (plan 4.1: +7)
- `DefyBindingTests` (new, 3): `KeyboardFAndPadR3EachSetTheDefyBitOnTheirOwnDevice`, `DefyIsRemappableAndListedWithItsControlsLabel`, `ResetToDefaultRestoresFAndR3`.
- `GamepadDefaultBindingTests.DefyDefaultsToR3AndTheDialogueLogToBack` (+1).
- `GamepadOverrideRepairTests.ASavedRowHoldingR3KeepsItAndDefysPadHalfGoesUnbound`, `WithoutACollisionOrOnceRepairedTheDefyRepairChangesNothing` (+2).
- `TimeFreezeBindingMigrationTests.ASavedTimeFreezeRowOnBackIsKeptByEveryRepair` (+1).
- Seen failing: every case references `Actions.Defy` / `RepairDefyPadDefaultCollision` / the `gameplay_defy` project action, none of which exist on the base commit (compile error / missing action); the Dialogue-Log half fails against the old button 8.

## 5. Commands
- `dotnet build FightersThroughTime.csproj --nologo`: 0 errors.
- `--headless --import` once, then `dotnet test --filter` over DefyBinding, Gamepad, InputBindingSchema, TimeFreezeBindingMigration, PadMenu, InputDeviceFollow, UrPolish2, SettingsMenu, TranslationCsvShape: `Failed: 0, Passed: 101, Total: 101` (lock held; no full suite).

## 6. For the docs
`design-godot.md` Controls table: add Defy History (F / R3); Dialogue Log pad half is Back. Ledger: `USER-DEFY-OWN-BUTTON`, `PROVISIONAL-DEFY-INPUT`, `VERIFY-TIME-FREEZE-ON-BACK-SHARED` (a saved Time Freeze row on Back now shares it with the Dialogue Log; no repair), `DEFER-INPUT-WIRE-FULL`.

Nobody played the build.
