# UI theme tools (UR-THEME, pulp UI redesign 2026-10-26)

## The theme generator

`resources/UI/ftt_theme.tres` and `scripts/UI/UIPalette.Roles.cs` are **generated** by one script, so every
variation's numbers (UI_SPEC section 2 roles T01-T50, section 3.0 plates) live in one table.

```powershell
py tools/ui_theme/build_ftt_theme.py     # from the repository root
```

Edit `build_ftt_theme.py`, not the `.tres`. The palette hex values in the script mirror
`scripts/UI/UIPalette.cs`; `UIThemeTests` pins the two together. Run `--import` afterwards
(`& $env:GODOT_BIN --headless --path . --import`) so Godot re-reads the theme.

## The UI capture tool

Takes 1920 x 1080 screenshots of screens for review and for the Phase B closeout gallery. Each target
renders into a `SubViewport`, so the PNG is exactly 1920 x 1080 whatever the window size.

It needs a **window**: run the console executable *without* `--headless`, briefly, **holding the
GdUnit lock** (create `D:\Projects\.ftt-gdunit-lock`, run, remove it; CLAUDE.md has the rules).

```powershell
$env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
& $env:GODOT_BIN --path . --resolution 640x360 res://scenes/diagnostics/UiCaptureRunner.tscn -- --list
& $env:GODOT_BIN --path . --resolution 640x360 res://scenes/diagnostics/UiCaptureRunner.tscn -- `
    --capture=theme_controls,main_menu_root --out=D:/Projects/ftt-scratch/ur-captures/theme
# --capture=all captures every registered target
```

Options (after `--`): `--capture=a,b|all`, `--out=<dir>` (default `user://ui_captures`), `--list`.
Exit code 0 ok, 2 bad argument or unknown target, 3 a target failed.

### Adding a target (Phase B)

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
(`UIPalette.ApplyTheme(root)`).

Shipped targets: `theme_labels_small`, `theme_labels_large` (every Label role), `theme_controls`
(button states, panels, inputs, bars, tabs), `theme_palette` (every PAL-B token), `main_menu_root`.
