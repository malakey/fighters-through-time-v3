# C. Glass heritage (recon 2026-10-26)

Recon for the Temporal Glass rebuild of the menus and HUD. Everything comes from git and file inspection at HEAD `221e0fa7`; nothing was launched. Items marked "inferred" or "not checked" were not verified.

## Summary
1. Every glass-era asset still exists on disk at HEAD, but nothing in `scenes/`, `scripts/` or `resources/` references the old files. Exceptions: a string check in `tests/Pulp/ScreenSceneStyleTests.cs:30` (it flags the `temporal_glass_` literal) and the tool sources under `tools/asset_src/`. The redesign deleted only the old per-player card frames, which had moved under `pulp/`.
2. The glass theme names `TemporalGlassButton` and `DialogueGlassPanel` survive in `ftt_theme.tres`, but they are now pulp plates under the old names. The glass 9-slices behind them are gone from the theme.
3. All three `bg_*_glass` mockup images map one-to-one to existing repo PNGs.
4. `gl_compatibility` can do most of the glass look cheaply. The exception is backdrop blur, which it cannot do (section 3).
5. `UIPalette.ApplyUiScale` rescales font sizes only, not geometry. Hairlines and chamfers must be built to survive scale themselves.

## 1. Git archaeology

**Glass era and redesign**
- Glass button 9-slices, `DialogueGlassPanel`, `TemporalGlassButton`, the card frames, the menu background plates and the title flourish were added or committed together in `8729913e` ("Update assets and scene configurations for character portraits and environments"). `4938bd92` ("Package 8 B3: dialogue glass panel…") introduced `DialogueGlassPanel` / `DialogueGlass`.
- The original glass theme entries are visible at `daef1285:resources/UI/ftt_theme.tres`, the last commit before the redesign: `:3-7` the glass button texture ext_resources; `:93` the `DialogueGlass` StyleBoxFlat; `:283-328` the `TemporalGlassBtn*` StyleBoxTextures; `:356` `DialogueGlassPanel`; `:404-415` `TemporalGlassButton`.
- Redesign commits, in order: `e477997d` plan and recon dossiers; `3b3a342b` / `d1e9e2a0` UR-ART kit; `26e955a0` / `a76ca292` UR-THEME; `8a913fc5` UR-WIDGETS; `fb376bea` UR-MENU, the new MainMenu scene; `94d9bc5e` closeout. 57 commits between `daef1285` and HEAD.
- The pulp tooling lives in `scripts/UI/Pulp`, `scripts/UI/Motion`, `assets/sprites/ui/pulp/`, `tools/asset_src/pulp/` and `tools/ui_theme/`.

**What the redesign changed**

| Item | At HEAD | Referenced? | Restore from |
|---|---|---|---|
| `assets/backgrounds/menu/temporal_glass_{main_menu,fighter_select,submenu}_background.png` | exists | no, except the string check in `ScreenSceneStyleTests.cs:30` | still in tree; old wiring `daef1285:scenes/menus/MainMenu.tscn:13-14` |
| `assets/backgrounds/menu/menu_starfield.png`, `menu_glass_shards.png` | exist | no (the old MainMenu used them at `:5-6`) | in tree |
| `assets/sprites/ui/glass_button_{normal,focus,pressed,disabled,focus_ring}.png` | exist | no (old theme and MainMenu only) | in tree; `daef1285:resources/UI/ftt_theme.tres` for the StyleBoxTexture margins |
| `assets/sprites/ui/card_frame.png`, `card_frame_p1.png`, `card_frame_p2.png`, `title_flourish.png` | exist | no scene, script or theme reference found | in tree |
| `assets/sprites/ui/pulp/card_frame_p1.png`, `card_frame_p2.png` (+ `tools/asset_src/pulp/svg/card_frame_p2.svg`) | deleted by `94d9bc5e` | n/a | `94d9bc5e^`; replaced by one white-ring frame tinted at runtime |
| Old MainMenu layout (starfield, shards, glass buttons, Temporal backgrounds) | replaced | n/a | `daef1285:scenes/menus/MainMenu.tscn`; the pulp cover came in `fb376bea` |
| `TemporalGlassButton`, `DialogueGlassPanel` | names kept; contents are pulp, generated at `tools/ui_theme/build_ftt_theme.py:318` (a 26 px pulp plate) and `:363` (`panel_dark` plate). The old teal-ish colours became PAL-B (`ftt_theme.tres:1181`, `:1495-1508`). | yes | old glass look: `daef1285:resources/UI/ftt_theme.tres` |
| SVG sources `tools/asset_src/menu/` (`glass_button_*.svg`, `glass_shards.svg`, `card_frame*.svg`, `clock_etch.svg`, `title_flourish.svg`, `build_menu_svgs.py`) and `tools/asset_src/jobs.json` | exist | `jobs.json` still references them | in tree |

Notes:
- `daef1285`'s glass button import had `mipmaps/generate=false`.
- The old `DialogueGlass` was a plain `StyleBoxFlat` (`daef1285:resources/UI/ftt_theme.tres:93`), the nearest precedent for a shader-free translucent fill.

## 2. Mockup art sources
`D:\Projects\fighters-through-time-docs-2\mockups\ui_redesign_2026-10-25\_build\prep_assets.py:133-135` (cache keys in `cache\assets.json` → `images`):
- `bg_menu_glass` → `assets/backgrounds/menu/temporal_glass_main_menu_background.png` (fit width 1600).
- `bg_select_glass` → `assets/backgrounds/menu/temporal_glass_fighter_select_background.png` (1440).
- `bg_submenu_glass` → `assets/backgrounds/menu/temporal_glass_submenu_background.png` (1440).

Other cached images the glass layouts could use:
- `bg_timeship_far` / `bg_timeship_mid` → `assets/environments/time_ship/time_ship_{far,mid}_layer.png`.
- `bg_holodeck_far` → `assets/environments/holodeck/holodeck_far_layer.png`.
- `bg_florence_far` → `assets/environments/florence/florence_far_background.png`.
- `scene_pompeii` is built from the `assets/environments/pompeii/pompeii_{sky,vesuvius,skyline,ruins}.png` layers.
- `por_<id>` → `assets/sprites/characters/portraits/<id>_portrait.png`.
- `spr_*` → `assets/sprites/characters/<id>/retro/<id>_<sheet>_atlas.png` plus `tools/retro_sprites/manifests/<id>.json`.
- `ab_*` → `assets/sprites/vfx/<id>/<id>_ability_vfx_atlas.png`.

## 3. Renderer feasibility on `gl_compatibility`
From knowledge of Godot 4.x and the repo files; not tested here.
- **Translucent fill, no blur.** `gl_compatibility` has no reliable, cheap `hint_screen_texture` mipmap blur. Fallback: a `StyleBoxFlat` with alpha about 0.55-0.75 over the dark plate colour, plus a faint top-to-bottom gradient (a `GradientTexture2D` StyleBoxTexture or a canvas_item shader) and a brighter inner top hairline. The old `DialogueGlass` StyleBoxFlat (`daef1285`) shows this works. The shipped `temporal_glass_*_background.png` plates already contain baked glass shapes.
- **Chamfer.** `StyleBoxFlat` has only rounded corners (`corner_radius_*`, `corner_detail`), so no true chamfer. A chamfer needs a 9-slice texture (the `glass_button_*` pattern, with `texture_margin`) or a `_Draw()` polygon / canvas_item shader. The repo already draws with `_Draw()` (`HalftoneCorner`, `BlockSparkEffect`); the shader route is `halftone_corner.gdshader`, which masks by shape. A 9-slice stretches its edges, so chamfer size is fixed in texels while the panel grows.
- **Hairlines (1-2 px) at UI scale 0.9-1.4.** `ApplyUiScale` (`scripts/UI/UIPalette.cs:376-389`) rescales only theme font sizes, clamped to `GlobalSaveData.MinUiScale` / `MaxUiScale`. Geometry follows the 1920x1080 canvas and does not scale with it, so hairlines are constant canvas pixels and stretch with the window only. `StyleBoxFlat` `border_width` is crisp at integer px; `_Draw()` lines stay crisp at non-integer window scales if aligned to 0.5. A hairline baked into a 9-slice texture blurs unless the import uses mipmaps and linear filtering. Pulp imports use `mipmaps/generate=true` (for example `assets/sprites/ui/pulp/badge_disc.png.import:26`); the old glass buttons used `false`. Not verified at 0.9 or 1.4.
- **Focus glow.** `StyleBoxFlat` `shadow_color` / `shadow_size` with a gold colour and zero offset gives a cheap outer glow; otherwise a drawn ring (the old `glass_button_focus_ring.png`) or `FocusPose` in `scripts/UI/Motion/`.
- **Gradient wordmark.** A canvas_item shader on a Label can multiply a `uv.y` gradient into `COLOR`. A Label's UV spans its glyph quad, so it is per label, not per glyph. It must keep the incoming colour and alpha. The gold chevron focus marker is a drawn polygon.
- **Drop shadows.** Label `font_shadow_*` theme items or `StyleBoxFlat` shadow. No blur, so soft shadows need a baked texture.
- **Keeping the incoming modulate.** `halftone_corner.gdshader:22-23` does `COLOR = vec4(dot_color.rgb * COLOR.rgb, … * COLOR.a)`; new glass shaders must follow this pattern, and `UiShaderModulationScanTests` enforces it (its body was not read). `UiChromeCyanSourceScanTests` fails on any `UIPalette.Cyan` read or cyan literal under `scripts/UI/` and the UI scenes. The old glass assets were cyan/violet, so reusing them as-is would likely trip the scan (inferred: the glass PNGs and the test's allow-list were not opened). `ScreenSceneStyleTests.cs:30` also flags the `temporal_glass_` literal in scenes; wiring the old plates back into a scene would likely break that test unless they are re-tinted or the test is updated.

## 4. Provided by the pulp build, reusable as-is
- Generated theme pipeline: `tools/ui_theme/build_ftt_theme.py` writes `resources/UI/ftt_theme.tres` and `scripts/UI/UIPalette.Roles.cs`. Never hand-edit the `.tres`.
- Fonts: Cinzel and Outfit in `assets/fonts/`, with a glyph-coverage guard in `UIThemeTests`.
- Motion: `scripts/UI/Motion/` (`PulpMotion.cs`, `FocusPose.cs`, `PulpMotionRules.cs`); all of it reads `ComfortSettings` and falls back to a stable fade.
- Screen wipe: `scripts/UI/Motion/ScreenWipe.cs`, played by `GameManager.LoadScene`.
- Capture tool: `scenes/diagnostics/UiCaptureRunner.tscn`, targets in `scripts/Diagnostics/UiCapture/` (windowed 1920x1080 PNGs; run without `--headless`, hold the GdUnit lock).
- UI-scale 1.4 fit rules: `scripts/UI/MenuSafeAreaFit.cs` (shrinks a tall panel to the viewport minus 24 px a side, floored at 0.5); `CardTextFit.cs`; `PulpTagPlate` (clamps a tag at the panel edge); `PulpScreenKit.FocusRowGap` (keeps horizontal button rows apart); `MenuLayoutBoundsTests` (sweeps every menu surface at scale 1.0 and 1.4).
- Widgets in `scripts/UI/Pulp/`: `PulpButtonDecor` (focus pose, upper-case label drawn in a child layer), `PulpPromptBar`, `HalftoneCorner`, `PulpBarDecor`, `PulpIcon`, `PulpKit` (reads `assets/sprites/ui/pulp/pulp_kit.json`).
- Shaders: `assets/shaders/` (`halftone_corner`, `pulp_bar_layers`, `ui_desaturate`, `ui_circle_mask`; all keep modulate).

## Not checked
- The contents of the old glass PNGs (colours).
- The `UiShaderModulationScanTests` and `UiChromeCyanSourceScanTests` allow-lists.
- Texture import filtering at runtime.
- Whether `jobs.json` still builds cleanly.
