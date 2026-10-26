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

# Step 2: the GAUGE-B parts, the tagged panel and the toggle

Same branch, after `ur/widgets` merged into main (`c5c9c838`). Files: `scripts/UI/Pulp/{PulpKit,PulpBarDecor,PulpAbilitySlot,PulpStatusChip,PulpPortraitRoundel,PulpTaggedPanel,PulpToggleDecor}.cs`, shaders `assets/shaders/{pulp_bar_layers,ui_desaturate,ui_circle_mask}.gdshader`, capture file `UiCaptureTargets.WidgetsGauges.cs` (plus the existing `.Widgets.cs`), `tests/Pulp/*` (six new suites), `localization/en.csv` (`ui_toggle_on`, `ui_toggle_off` in the `# UR WIDGETS` block). **`en.en.translation` is not committed; regenerate it with `--import`.** Every part is `MouseFilter = Ignore`, independent of its parent's type, and written by its owner (nothing ticks).

## API and snippets

**`PulpKit`** (static): `Plate(stem)` builds a nine-slice `StyleBoxTexture` from `pulp_kit.json`, `Texture(stem)`, `Expand(stem)`. `PulpButtonDecor` now uses it (its private JSON reader is gone).

**`PulpBarDecor : Control`**, over a `ProgressBar`. `PulpBarDecor.Attach(bar, PulpBarStyle.Hp | Boss)`; idempotent. Properties `LagValue`, `EchoValue` (the end of the band, `HP + echo`, both in the bar's units), `Low`, `PhaseNotches` (fractions, boss), `Style`. `DropLagTo(float)` raises at once and drops through `PulpMotion.TrackerDrop`. Pure statics `FractionsFor(...)`, `GlowStrengthAt(low, seconds, reduced)`. One shader draws the tracker, the echo (22 % inset, 2 px black edges), the 135 degree hatch, the highlight and the decile ticks inside the inner area (the bar rect inset by its `background` content margins), sheared by the fill's `StyleBoxFlat.Skew.X`; `FillMode = EndToBegin` mirrors it. The bar's `fill` stylebox is never touched, so `TintFill` keeps working (a test pins it). The boss style uses the accent tracker, the 6 / 8 hatch and a 4 / 6 highlight and no ticks. `Low` runs a 2 s sine glow of soft rings around the track (starts at full strength; steady 0.6 under Reduced Temporal Effects). The decor reads the value in `_Draw`; connect nothing else.
```csharp
var decor = PulpBarDecor.Attach(hpBar);          // after the bar is themed
decor.EchoValue = hp + echoPool; decor.Low = hp / max <= 0.18f;
decor.DropLagTo(hp);                              // on a hit
```

**`PulpAbilitySlot : Control`**. `SlotSize` 74 / 54 / 50, `Ultimate` (92 frame inside a 116 ring; the control is 116), `State` (`Ready | Cooling | Charging | Dormant | Suppressed`), `Progress` (elapsed share, or the meter share), `SecondsRemaining`, `Art`, `KeyText` (raw key, blank hides the tag). `PulpAbilitySlot.Derive(state, ultimate, p, seconds)` returns a `PulpSlotLook` (saturation, brightness, sweep share, centre text, glyph, ring mode, READY tag, key dim, ready frame): the pure rule table the tests pin. An Ultimate "Cooling" reads as charging and a square "Charging" as cooling. The READY tag is a `KeyTagPlate` with `common_ready` upper-cased (never hue alone).
```csharp
var s = new PulpAbilitySlot { SlotSize = 74f, KeyText = "hud_slot_special1", Art = AbilityIconLibrary.Load(id, AbilitySlot.Special1) };
s.State = PulpSlotState.Cooling; s.Progress = elapsed; s.SecondsRemaining = left;
```

**`PulpStatusChip : Control`**. `BadgeSize` 44 / 40, `Kind` (Damage / Control / Suppression), `GlyphName`, `Remaining`, `ShowRing`, `Empty`, `ShowText`, `NameText`, `TimeText` (raw keys auto-translate; the name is upper-cased). Empty keeps the size, draws the dashed ring, hides the glyph and fades the text to 0.35. Minimum size = badge (+ 12 + the text column).

**`PulpPortraitRoundel : Control`**. `Diameter` 112 / 110 / 86, `Portrait` (square art). Back plate, 142 % portrait at (-21 %, -6.8 %) masked by `ui_circle_mask`, halftone over it.

**`PulpTaggedPanel : Container`**. `TagKey` (raw key; empty hides). Add the panel as a child; it fills the rect, the minimum size is its own, the tag is a `PanelTagPlate` with an upper-case `PanelTag` label at the padding box (-14, -24), rotated -1.2 degrees, `ZIndex` 1.
```csharp
var tp = new PulpTaggedPanel { TagKey = "hud_vitals" }; tp.AddChild(innerPanelContainer); vbox.AddChild(tp);
```

**`PulpToggleDecor : Node`**. `PulpToggleDecor.Apply(CheckButton)` (idempotent): the eight switch icons become 1 px placeholder textures, the banner plates become `StyleBoxEmpty`, the font colours become ink, and a layer draws the 132 x 44 track at the right end of the button (`toggle_track_off/on`, 9-slice), the knob (0.1 s `Quad` slide, instant under Reduced Temporal Effects), a live ON / OFF label (`SlotKeyTag` role, 18 px, `ui_toggle_on` / `ui_toggle_off`, upper-case) and an accent focus outline; disabled dims to 0.45. Give the button enough width; the button's own text stays on the left. Minimum height is raised to 52.

**Step 6.** `PulpButtonDecor.Apply(button, compact = false, alignLeft = true)` now left-aligns the label.

## Tests
Delta **+14**: `PulpAbilitySlotTests` 3, `PulpBarDecorTests` 3, `PulpStatusChipTests` 2, `PulpPortraitRoundelTests` 1, `PulpTaggedPanelTests` 2, `PulpToggleDecorTests` 2, and one case in `PulpButtonDecorTests` (`alignLeft`). 3588 + 14 = **3602**; full run `Failed: 0, Passed: 3602, Total: 3602`, 2 m 10 s, lock held.

## Provisional choices
- The track and bar geometry assume Godot's skewed `StyleBoxFlat` slants "/" with the bounding box equal to the rect; the capture shows the overlays lining up with the fill, so I kept it.
- The low glow is drawn as seven polyline rings, not a blurred shadow; the pulse starts at full strength.
- The art inset is derived from the spec's three sizes (a 4 px border, 9 % of the padding box, art 82 % of it).
- The sweep is a pie of the padding box (square corners, not rounded).
- The READY tag overlaps the ring's bottom edge (6 px below the 116 box, so the slot's drawn rect extends 6 px).
- The status ring is drawn over the ink ring (outer edge 1 px outside the badge), as the spec's "inset -1 px, 5 px wide" reads.
- The toggle removes the inherited Button plate and sets ink font colours itself; the theme still does not style `CheckButton`.
- Portrait art is stretched into a square.

## Not matching or not seen
- The mix draws ability numbers larger than `SlotNumber` (20 px); the Ultimate "64%" uses the same role.
- The sweep's rounded square corners and the ring's shadow on the READY state were not compared to the mock pixel for pixel.
- The low-HP glow was only seen at its first frame in the capture (static); I did not capture it mid-pulse or under Reduced Temporal Effects.
- Nothing was seen over a real Story / Fighter HUD scene; the bars were captured on a plain backdrop.
- Captures (`D:\Projects\ftt-scratch\ur-captures\widgets\`): `widgets_gauges`, `widgets_slots`, `widgets_chips`, `widgets_roundel`, `widgets_tagged_panel`, `widgets_toggles`. They match `mix_story.png`, `mix_fight.png` and `demo_*` in plate shapes, hatch, ticks, tracker, echo, ring and sticker placement; the differences are listed above.
