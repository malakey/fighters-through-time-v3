# UI theme tools (TG-THEME, Temporal Glass rebuild 2026-10-27; first written by UR-THEME)

The look authority is `UI_SPEC_GLASS.md` in
`D:\Projects\fighters-through-time-docs-2\mockups\ui_redesign_2026-10-25\` (section numbers below are its own);
the plan is `docs/UI_GLASS_2026-10-26_PLAN.md`.

## The theme generator

`resources/UI/ftt_theme.tres` and `scripts/UI/UIPalette.Roles.cs` are **generated** by one script, so every
value lives in one table: the palette (spec section 1), the Label roles (2.3, T01-T60, with the scene halos of 2.4)
and the styles (3.0-3.3, 6.2).

```powershell
py tools/ui_theme/build_ftt_theme.py     # from the repository root
```

Edit `build_ftt_theme.py`, never the `.tres`. Run `--import` afterwards
(`& $env:GODOT_BIN --headless --path . --import`, under the GdUnit lock) so Godot re-reads the theme.

- **The palette** is one table, `PALETTE`, keyed by the name of the `UIPalette` constant that mirrors each colour
  (plan ruling R11: a PAL-A swap is a change to this table). Every style and role reads its colours from it, and
  the generator writes it into `UIPalette.Roles.cs` as `UIPalette.GeneratorPalette`.
  `UIThemeTests.TheGlassPaletteConstantsMatchTheSpecAndTheGenerator` fails when an entry has no same-named
  `UIPalette` constant of the same value, or when a glass constant leaves the spec's value.
- **The roles** are `ROLES`: face, drawn weight, size (rounded to whole pixels), tracking (em, written as
  `FontVariation.spacing_glyph` = em x size), colour, halo tier (`None` plate, `"S"`, `"S6"` for T29, `"L"`),
  tabular figures and, for roles whose text wraps, the 2.3 line height (as `line_spacing`, against the faces'
  natural 1.26 / 1.348 em). `UIPalette.LabelRoleTable` carries name, size, face, weight, tracking and halo.
- **The styles** are TG-ART's nine-slices, read from `assets/sprites/ui/glass/glass_kit.json` (`gbox`: the
  texture, its 6.4 nine-slice and expand margins and the tile flag come from the catalog; the content margins are
  the panel padding or the label inset): the slab G1 (`glass_panel`) for every slab, preset and popup, the tag G2
  (`glass_tag`) for `PanelTagPlate` and `HintLinePlate`, the chevron states (`glass_btn_*`), the tabs
  (`glass_tab*`), the field (`glass_field*`) for the default `Button`, `OptionButton`, `GlassField`, `LineEdit` and
  so the spin box, and the slider grabber icon (`glass_slider_grabber`, with `center_grabber`). The focus box of a
  chevron, field, line edit and tab is an **overlay** with no dark base (plan 0.4 P5), drawn over state boxes that
  carry the fill: the chevron's is the lit edge, the focus wash and the 14 px glow, the field's the lit edge and the
  glow, the tab's the glow only. A chevron's `hover` is its normal box (the decor takes focus
  on mouse-enter); a field and a tab hover with their own full-look boxes. What 6.2 builds flat stays a
  `StyleBoxFlat`: the READY badges, the scrim, the slider track and fill, the scroll bars, the check box ring, the
  tooltip, the tab container's inner panel. The `OptionButton` arrow is `glass_option_arrow`, `chevron_down`'s bold
  weight baked at its 18 px display size, and a spin box's up and down arrows are its vertical flip
  (`glass_spin_up`) and the same arrow, in every state; all three stay 18 px at every UI scale. The five HUD bar
  variations are value carriers: an empty background and a fill that holds its colour but draws nothing, under the
  glass tube decor (`PulpBarDecor`) that draws the whole tube. TG-CLOSE retired the pulp kit's plates, roles and
  palette entries, and `UiLegacyKitReferenceScanTests` fails on any reference to them.
- **Variations and presets** are listed in `VARIATIONS` (written to `UIPalette.Variations`), including the eight
  3.1 slab presets (`GlassPanelPause` ... `GlassPanelContinue`, content margins = their paddings), `ReadyBadge` /
  `ReadyBadgeIdle`, `GlassField`, `GlassTab` / `GlassTabSelected`. A chevron variation also carries a `selected`
  style (not drawn by Godot itself: the button decor swaps it in for the current value of a list).

## The UI capture tool

Takes 1920 x 1080 screenshots of screens for review and for the closeout gallery. Each target renders into a
`SubViewport`, so the PNG is exactly 1920 x 1080 whatever the window size.

It needs a **window**: run the console executable *without* `--headless`, briefly, **holding the GdUnit lock**
(create `D:\Projects\.ftt-gdunit-lock`, run, remove it; CLAUDE.md has the rules).

```powershell
$env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
& $env:GODOT_BIN --path . --resolution 640x360 res://scenes/diagnostics/UiCaptureRunner.tscn -- --list
& $env:GODOT_BIN --path . --resolution 640x360 res://scenes/diagnostics/UiCaptureRunner.tscn -- `
    --capture=theme_glass_components,main_menu_root --out=D:/Projects/ftt-scratch/tg-captures/theme
# --capture=all captures every registered target
```

Options (after `--`): `--capture=a,b|all`, `--out=<dir>` (default `user://ui_captures`), `--list`.
Exit code 0 ok, 2 bad argument or unknown target, 3 a target failed.

### Comparing a capture with the mockups

`compare_capture.py` puts a capture beside a mockup render or a spec crop at 1:1 and blends the two 50 %,
writing the results beside the capture (Pillow is installed for `py`):

```powershell
$M = "D:/Projects/fighters-through-time-docs-2/mockups/ui_redesign_2026-10-25"
py tools/ui_theme/compare_capture.py D:/Projects/ftt-scratch/tg-captures/theme/pause_story.png `
    "$M/screens/1080p/rec_03_pause.png" "$M/glass_spec_assets/crops/pause_panel__from_03_640_66.png" --diff
# a component on a specimen page sits elsewhere than in the mock: pick its region in the capture
py tools/ui_theme/compare_capture.py D:/Projects/ftt-scratch/tg-captures/theme/theme_glass_components.png `
    "$M/glass_spec_assets/crops/menu_button_column__from_01_86_505.png" --crop 30,54,614,490
```

Per reference it writes `<capture>__vs__<reference>__side.png` (capture left, reference right),
`__overlay.png` (50 % blend) and, with `--diff`, `__diff.png` (the absolute difference, brightened). A full render
(`rec_*`, 1920 x 1080) is compared region for region with the capture; a crop is matched to the region its file
name names (`__from_04_20_14` = `rec_04` at x 20, y 14) or to `--crop x,y,w,h`. Nothing is resized. The renders and
crops include the browser's backdrop blur, which the build cannot have (spec 1.4): read the fills accordingly.

### Adding a target

Create your own `partial` file so no shared file is edited:

```csharp
// scripts/Diagnostics/UiCapture/UiCaptureTargets.Hud.cs
namespace FTT.Diagnostics {
    public static partial class UiCaptureTargets {
        [UiCaptureTarget("story_hud_full", SettleFrames = 12)]
        public static Node StoryHudFull(UiCaptureContext context) {
            var hud = ResourceLoader.Load<PackedScene>("res://scenes/ui/StoryHUD.tscn").Instantiate();
            // Optional: runs after the node settled, then waits SettleFrames again.
            context.AfterReady = node => { /* open a tab, grab focus, push events */ };
            return hud;
        }
    }
}
```

The method returns the node to show (it is stretched to the frame). The runner discovers every
`[UiCaptureTarget]` method by reflection; names must be unique (a duplicate is logged and skipped).
Screens adopt their own theme; the theme specimen targets show how to apply it
(`UIPalette.ApplyTheme(root)`). Target names are kept across the glass rebuild, so the pulp gallery
(`D:/Projects/ftt-scratch/ur-captures/main-6a840a3f/`) and the glass one (`D:/Projects/ftt-scratch/tg-captures/final/`,
whose `compare/pulp` holds the pairs) pair 1:1.

Theme targets: `theme_labels_small` (every Label role at 30 px and under) and `theme_labels_large` (over 30 px), plate
roles on a glass slab and scene roles on a bright art swatch; `theme_controls` (the button variations in four states,
every panel variation, fields, sliders, bars, tabs, a scroll bar); `theme_palette` (every generator palette entry over
white, then P1 / P2, the grammar colours `UnboundCold` and `ResonanceGold`, and `Cyan`, kept for player 1's default and
the Severed); `theme_glass_components` (the glass components at their
spec sizes: the 68 / 64 / 58 px chevrons in every state including focused-and-pressed, a slab with its tag, the READY
badges, tabs, fields, a slider, the bars, the halos on bright art); `main_menu_root`. A static capture can focus one
control only, so the specimens show the hover, focus, focused-and-pressed and selected looks by drawing the theme's own
boxes over a plain button.
