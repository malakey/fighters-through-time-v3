# UR-WIDGETS handoff (pulp UI redesign, Phase A)

Branch `ur/widgets`, from `main` at `1ead360d`. Four shared widgets for the Phase B screen agents, namespace `FTT.UI.Pulp`, files under `scripts/UI/Pulp/`. No edit to the theme, `UIPalette*`, `scripts/UI/Motion/*`, `AGENTS.md`, `CLAUDE.md` or the ledger.

## 1. API and usage

### `PulpIcon : Control`
`IconName`, `Tint` (default white), `ShadowEnabled` (true), `Flat` (fill only: no ink, no shadow). Each setter calls `QueueRedraw`. Layers: `_sh` under at (2, 2), `_fill` tinted, `_ink` over. Aspect kept, centred, `MouseFilter = Ignore`, mipmapped filter. `Create(name, tint, size)` sets min size and size. `Exists(name)`, `KitNames` (the 26), `LoadLayers(name)` (cached; null for unknown, warns once, never throws). Dim with `Modulate` / `SelfModulate`.

```csharp
row.AddChild(PulpIcon.Create("hourglass", UIPalette.Accent2, 46f));
var lockGlyph = PulpIcon.Create("dormant", UIPalette.Ink2, 28f); lockGlyph.Flat = true;
```

### `PulpButtonDecor : Node`
`PulpButtonDecor.Apply(Button button, bool compact = false)` (idempotent; returns the decor). Adds, as children of the button: a `FocusPose`, the `Marker` icon (20 x 28 at x + 26, compact 16 x 22 at x + 20, vertically centred), a `PromptBadge` and the decor node. Marker tint and shadow: normal ink3 + shadow, focus or hover #0C0A10 without shadow, selected #E0412F + shadow, held ink3 + shadow, disabled #4A4333 without shadow (precedence disabled, selected, held, focus or hover). The badge shows on the focused row only (never when disabled): the pad circle (36 px, `prompt_circle` fill in #0C0A10, no ink, live letter "A" in `PromptKey`) or a key-cap pill with the `ui_accept` key, 30 px in from the right edge. `Selected { get; set; }` follows `ButtonPressed` in ToggleMode; selected swaps `normal` and `hover` for the paper plate `btn_selected_h80/58/64` (by `ThemeTypeVariation`; margins from `pulp_kit.json`, content margins copied from the theme's `normal` box) and the five font colours to #0C0A10, and restores the previous overrides when cleared. Public: `Marker`, `Badge`, `BadgeDevice`, `Button`, `Refresh()`, and the pure statics `ComputeMarkerState`, `MarkerTint`, `MarkerHasShadow`, `SelectedPlateHeight`.

```csharp
foreach (Button b in menuButtons) PulpButtonDecor.Apply(b);        // PulpButton rows
PulpButtonDecor.Apply(tabButton, compact: true);                    // PulpButtonCompact
```

### `PulpPromptBar : HBoxContainer`
`AddPrompt(StringName action, string captionKey)`; pairs 30 px apart, glyph 34 px, 10 px to the caption (Label, `PromptCaption` role, `Uppercase = true`, `Text` = the raw key). Pad: `prompt_circle` (ink disc, cream ring) plus a live letter; keyboard: key-cap pill labelled with the action's first key, upper-case. Rebuilds on `InputManager.DeviceAssigned` (subscribed in `_EnterTree`, released in `_ExitTree`; null-safe). `Device`, `PairCount`, `GlyphAt(i)`, `CaptionAt(i)`, `Refresh()`.

```csharp
var bar = new PulpPromptBar();
bar.AddPrompt("ui_accept", "ui_prompt_select");
bar.AddPrompt("ui_cancel", "common_back");
root.AddChild(bar);   // anchor bottom-right yourself (spec: margin 70 / 40)
```

### `HalftoneCorner : Control`
`HalftoneCorner.Attach(Control panel, bool paper = false)` inserts it as the panel's first child. Shader `assets/shaders/halftone_corner.gdshader` (plain uniforms, `gl_compatibility` safe): pitch 10, solid r 1.5 fading to 2.0, bottom-right 42 % x 52 % of its rect, diagonal mask `1 - clamp(t / 0.82, 0, 1)`. Dark dots white 0.17, paper black 0.16 (`Paper` property). `MouseFilter = Ignore`. In a `PanelContainer` the rect is the padding box.

### Shared: `PulpPromptGlyphs`
`PromptDevice { Keyboard, Pad }`, `CurrentDevice()` (player 1's slot: pad when `GetDeviceForPlayer(0) >= 0`), `PadButtonLabel(int)` (0-3 A B X Y, 9 LB, 10 RB, else `controls_binding_joy_button`), `PadLabelFor(action)`, `KeyLabelFor(action)`, `LabelFor`. Test seam `internal static PromptDevice? DeviceOverrideForTesting` (restore in `finally`).

## 2. Files
New: `scripts/UI/Pulp/{PulpIcon,PulpButtonDecor,PulpPromptBar,PulpPromptGlyphs,HalftoneCorner}.cs` (+ `.uid`), `assets/shaders/halftone_corner.gdshader` (+ `.uid`), `scripts/Diagnostics/UiCapture/UiCaptureTargets.Widgets.cs`, `tests/Pulp/{PulpIconTests,PulpButtonDecorTests,PulpPromptBarTests,HalftoneCornerTests}.cs`. Changed: `localization/en.csv` (block `# UR WIDGETS`, key `ui_prompt_select,Select`). **`localization/en.en.translation` is not committed: regenerate it with `--import` after merging.**

## 3. Tests
`[TestCase]` delta **+14**: `PulpIconTests` 3, `PulpButtonDecorTests` 6, `PulpPromptBarTests` 3, `HalftoneCornerTests` 2. 3574 + 14 = **3588**. Full run: `Failed: 0, Passed: 3588, Total: 3588`, 2 m 11 s, lock held. Build 0 errors, no new warnings.

## 4. Provisional choices
- Captions: `Uppercase = true` on the Label (the English value keeps its case in `en.csv`).
- The keyboard key-cap label is cream (#FFF3D8) in the focused-row badge too, because the pill is black.
- The marker's "pressed" state is a held non-toggle button; a toggled-on button reads as Selected.
- The decor polls `Button.Disabled` each frame (no signal exists). The selected look overrides only `normal` and `hover`; the `focus` box still draws on top, so a selected, focused row reads as focus.
- Key labels come from the physical keycode string, so non-US layouts show the US name.

## 5. Not matching the spec yet / limits
- Button label alignment is the screen's job: the spec left-aligns the label at x + 64 (`HorizontalAlignment = Left`); the captures show the default centred label.
- A hand-held pressed button (`+7,+7`) was not captured (no input injection); toggled-on buttons show the selected look instead.
- The pad circle in the prompt bar reads slightly smaller than the mock because the art sits inside its 64-unit box.
- Halftone dots are faint at 0.17; they match the spec value but I did not compare against the mock's rendering pixel for pixel.
- `FocusPose` leaves a `PulpButtonDecor` button that is inside a `Container` posed by the container's sort rules (UR_THEME 2.4); not exercised here.

## 6. Capture comparison
`D:\Projects\ftt-scratch\ur-captures\widgets\`: `widgets_buttons.png` and `widgets_buttons_kb.png` (640 x 80 PulpButtons: normal, focus + badge, selected, toggle, disabled, compact, glass), `widgets_icons.png` (26 icons at 28 and 46 plus the flat `dormant` / `cross`), `widgets_prompts_pad.png` and `widgets_prompts_kb.png` (prompt bar plus dark and paper panels with the halftone corner). Against `mix_menu.png` / UI_SPEC 3.2: marker position, tint and shadow rule, the tilted gold focus plate, the 36 px A badge at the right edge, the paper selected plate with the red marker, the dormant hatch plate and the prompt bar's circle-letter-caption rhythm match; centred labels are the only visible difference (section 5). Capture target names: `widgets_buttons`, `widgets_buttons_kb`, `widgets_icons`, `widgets_prompts_pad`, `widgets_prompts_kb`.
