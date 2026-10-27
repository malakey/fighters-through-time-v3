# TG-ART handoff: the Temporal Glass art kit and its pipeline

Branch `tg/art`, cut from `main` at `196d821c`. Phase A, A1 of `docs/UI_GLASS_2026-10-26_PLAN.md` (section 2.3), in parallel with TG-THEME step 1. No theme, palette, font, scene or screen-script edit; the only C# is `scripts/UI/Pulp/PulpKit.cs` (TG-ART's file) and the new suite. Nothing another workstream uses was removed or changed in meaning (standing decision 10): `PulpKit` still resolves every pulp stem, and `pulp_kit.json` and `assets/sprites/ui/pulp/` are untouched.

## 1. Summary

One reproducible chain, `tools/asset_src/glass/build_all.ps1`, builds:

- **18 kit textures** under `assets/sprites/ui/glass/`: every nine-slice of UI_SPEC_GLASS 6.4 (`glass_panel`, `glass_tag`, the chevron button in normal / selected / pressed / focus / disabled, `glass_tab` / `_selected` / `_focus`, `glass_field` / `_focus`), the `glass_slider_grabber` and the two diamond glows, plus three full-look **hover** boxes (`glass_btn_hover`, `glass_tab_hover`, `glass_field_hover`) that the plan's 0.4 P5 focus split needs (section 2.1).
- **34 ICN-A icons x 2 weights = 68 textures** under `.../icons/` (64 x 64, white, glow baked, tinted by modulate): the spec's 52 sources, `beacon_anchor_hollow` (regular and bold, plan 0.4 P4), a derived bold for the 12 glyphs the spec draws in one weight, and `shield_off` (the block pip with no glow).
- **7 `GradientTexture2D` resources** (7.3): `glass_lens_radial`, `glass_badge_radial`, `glass_dial_radial`, `glass_portrait_radial`, `menu_scrim`, `select_vignette`, `fighter_top_shade` (engine resources, no script).
- **36 select hero frames** under `.../select_heroes/`: the four retro v2 idle frames of every roster member (the roster read from `content_manifest.csv`), trimmed to one box per hero symmetric about the pivot, with `select_heroes.json` (pivot, feet line, 6 fps), so the select never loads a hero's 41-51 MiB of atlases.
- `assets/sprites/ui/glass/glass_kit.json`: the catalog in the shape of `pulp_kit.json` (132 entries; file, size, nine-slice margins, expand margins, content-margin hints, axis stretch, mipmaps, what it serves, the 6.4 row it implements and any deviation). Every PNG imports with mipmaps.
- `PulpKit.cs` reads `glass_kit.json` first and `pulp_kit.json` second, and gains allocation-free glass icon lookup (section 3).

**Reproducible and deterministic.** A second full run of `build_all.ps1` rewrote all 341 outputs (PNGs, sidecars, `.tres`, JSON, SVG sources) and `jobs.json` byte for byte (md5, 0 mismatches). The raster jobs render every SVG at 4x and `build_glass_images.py` downsamples 4:1 in **premultiplied** space (exact area coverage), so a 1.5 px gold hairline keeps its colour instead of picking up the dark fringe a straight-alpha Lanczos resize gives.

## 2. The catalog (TG-THEME step 2 wires from this)

Files are relative to `res://assets/sprites/ui/glass/`. Margins are `texture_margin_left/top/right/bottom`, expand `expand_margin_*`, texture pixels at 1x. Every stem carries a glass prefix (`glass_`, `glass_ico_`, `hero_`); none collides with a `pulp_kit.json` stem (`write_catalog.py` refuses to write a collision, and the suite checks it).

### 2.1 How to use the textures

1. `StyleBoxTexture` with the file, margins and expand margins, `draw_center = true`; content margins = the panel paddings (one variation per preset; `content_margins_hint_LTRB` gives the default slab's 22 / 28 / 22 and the tag's 14 / 3 / 14 / 3). Never a scripted StyleBox. `PulpKit.Plate(stem)` builds one in code.
2. **Focus is an overlay (plan 0.4 P5).** Godot draws `focus` (Button, OptionButton, LineEdit) and `tab_focus` (TabBar) **over** the state box, so `glass_btn_focus`, `glass_field_focus` and `glass_tab_focus` carry only what focus adds: the lit edge (100 degrees, #FFD45C to rgba(glow,.55)), the focus wash (rgba(glow,.34) to .07 at 75 %, no dark base; buttons only) and the 14 px glow (sigma 7) **outside** the shape. Measured on the 4x rasters:
   - **pressed under focus** composes into the CSS focused-pressed fill exactly (interior max error 0.00/255): `glass_btn_pressed`'s interior is solved against the overlay, its edge band stays edge-dim. A pressed button is (almost always) focused, so this is the case that matters; pressed alone reads as a dimmer pressed wash.
   - **normal under focus** lands within 14.8/255 (mean 13.4/255, premultiplied) of the CSS focus fill; the difference is mostly alpha (the CSS focused interior is ~0.98 opaque because the bright focus edge layer sits under its 0.92 fill; normal + overlay is ~0.94), colours within a few units. Making the overlay reach it exactly makes the overlay ~0.64-1.0 opaque, which hides the pressed box (the pulp defect P5 removes), so I did not.
3. **Hover = focus (AC8).** Recommended wiring: `hover` and `normal` = `glass_btn_normal`, `hover_pressed` and `pressed` = `glass_btn_pressed`, `focus` = `glass_btn_focus`, and the decor grabs focus on mouse-enter, so hovered, focused and both draw normal + overlay, identically. The **hover boxes** are the full CSS focus look as one box, for a `hover` (or `tab_hovered`, which replaces the tab's style) that cannot rely on focus-on-hover; hovered **and** focused then draws the overlay over it (a doubled wash and glow). TG-THEME / TG-WIDGETS choose.
4. **Selected** (the current value in a list): the decor swaps `glass_btn_selected` in for `normal`, as the pulp decor swapped its paper plate.
5. **Disabled** tiles: `axis_stretch_horizontal = TILE` (`"axis_stretch": "tile"`; `PulpKit.Plate` sets it for glass entries). The centre slice is 70 px, seven 10 px dash periods.
6. One chevron texture per state serves 58-70 px buttons (stretch keeps the 30 px arrow run and the tip at mid-height). The washes and the focus edge are laid out for a 560 x 68 button through the nine-slice stretch (they follow the drawn width, as the CSS gradient follows the element's box); a 290 px compact button lands within 3 % of its own CSS stop.
7. The panel's sheen and two streak lines are **not** baked (GlassSheen, TG-WIDGETS); everything else of G1 is (edge, fill, drop shadow).

### 2.2 Textures

| file | size | nine-slice L/T/R/B | expand | stretch | 6.4 | serves |
|---|---|---|---|---|---|---|
| `glass_panel.png` | 140x140 | 42/28/42/56 | 22/8/22/36 | stretch | as 6.4 (gradient layout differs, section 6) | G1 slab: every panel preset, default `Panel` / `PanelContainer`; content hint 28/22/28/22 |
| `glass_tag.png` | 40x24 | 3/0/9/0 | - | stretch | as 6.4 | G2 tag; text T07; content hint 14/3/14/3; the wash laid out for a 130 px tag |
| `glass_btn_normal.png` | 96x70 | 2/2/32/2 | - | stretch | as 6.4 | G3 normal (and hover with focus-on-hover) |
| `glass_btn_selected.png` | 96x70 | 2/2/32/2 | - | stretch | as 6.4 | G3 selected / toggled |
| `glass_btn_pressed.png` | 96x70 | 2/2/32/2 | - | stretch | as 6.4; interior solved (P5) | G3 pressed / hover_pressed |
| `glass_btn_focus.png` | 124x98 | 16/16/46/16 | 14/14/14/14 | stretch | as 6.4; an overlay (P5) | G3 focus overlay |
| `glass_btn_hover.png` | 124x98 | 16/16/46/16 | 14/14/14/14 | stretch | addition | G3 the full focus look as one box |
| `glass_btn_disabled.png` | 104x70 | 2/2/32/2 | - | tile | canvas 104 (6.4: 100) | G3 disabled: dashed ring, averaged haze |
| `glass_tab.png` | 64x48 | 1/0/17/0 | - | stretch | as 6.4 | G14 tab_unselected / tab_disabled; text T55 |
| `glass_tab_selected.png` | 64x48 | 1/0/17/0 | - | stretch | as 6.4 | G14 tab_selected; text #04141A; laid out for a 180 px tab |
| `glass_tab_focus.png` | 92x76 | 15/14/31/14 | 14/14/14/14 | stretch | as 6.4; an overlay (the glow only) | G14 tab_focus |
| `glass_tab_hover.png` | 92x76 | 15/14/31/14 | 14/14/14/14 | stretch | addition | G14 tab_hovered: selected + glow |
| `glass_field.png` | 48x44 | 10/10/10/10 | - | stretch | as 6.4 | glass field (P8): Button, OptionButton, SpinBox / LineEdit, binding cells |
| `glass_field_focus.png` | 76x72 | 24/24/24/24 | 14/14/14/14 | stretch | as 6.4; an overlay (lit edge + glow) | field focus; the edge laid out for a 300 px field |
| `glass_field_hover.png` | 76x72 | 24/24/24/24 | 14/14/14/14 | stretch | addition | the full focused field (OptionButton hover) |
| `glass_slider_grabber.png` | 64x64 | icon | | | as 6.4 | HSlider / VSlider grabber: 31.7 px white diamond, accent ring (2.4 px drawn), accent glow sigma 6.4; not tinted |
| `glass_diamond_11.png` | 48x48 | icon | | | as 6.4 | flourish diamonds (menu glow 14, sigma 7; the select's glow 12 reuses it); white, tint #F3C95B |
| `glass_diamond_12.png` | 48x48 | icon | | | as 6.4 | boss name diamond (tint #FF5A5A) and phase-notch diamond (glow 10 reuses the 12); white |

### 2.3 Icons

Stems `glass_ico_<name>` and `glass_ico_<name>_bold` (files `icons/<name>.png`, `icons/<name>_bold.png`); the `glass_icons` entry indexes the 34 names. **Bold below 30 px display size, regular at 30 px and above** (3.5, AC23). One white texture per glyph and weight, tinted by `modulate`; the glow is baked in and follows the CSS: the generic `drop-shadow(0 0 3px currentColor @60%)` as a white Gaussian (sigma 2.5 bake px) at 0.60 under the art; coloured glows are stored divided by the glyph's intended tint, so the tint's modulate lands them on the spec colour.

| name | tint | shown at | glow |
|---|---|---|---|
| `dilation`, `static`, `root` | #8FC7FF (control) | 27.3 Story / 24.8 Fighter (bold) | generic |
| `venom`, `burn` | #FF7A5C (damage) | 27.3 / 24.8 (bold) | generic |
| `suppress` | #38F2FF cold | 27.3 / 24.8 (bold) | generic |
| `shield` | #DFE8FF | 30 Story (regular) / 24 Fighter (bold) | the pip's 4 px rgba(143,216,255,.6) (sigma 4.27 / 5.33) |
| `shield_off` | #DFE8FF at .28, grayscale | 30 / 24 | none (the CSS off pip has `filter: none`) |
| `hourglass`, `freeze` | #FFE39A (accent2); rewind at 1 or 0 #FF5A5A | 28.5 in a 46 badge (bold) | generic |
| `beacon_anchor` | #FFE39A | 28.5 (bold) | generic |
| `beacon_anchor_hollow` | the steady last-stand tint | 28.5 (bold) | generic; the diamond outline only (no fill, no cross): it differs by shape |
| `dust` | #F3C95B (res) | 38 (regular) | generic |
| `star4`, `star4_filled` | the slot colour (off at .22) | 28 (bold) | generic |
| `flag` | #FFF0B0 (res2) | 22 (bold) | generic |
| `defy_ready` | #FFF0B0 | 34 Story / 32 Fighter | the warm 7 px rgba(255,232,122,.75) (sigma 6.59 / 8.0) |
| `defy_notready`, `defy_spent` | #8A8374 (ink3) | 34 / 32 | generic |
| `defy_barred` | #FF5A5A | 34 / 32 | generic |
| `lock_dormant` | #8A8374 at .8 | 68 % of the lens (50.3 / 62.6 / 36.7) | none |
| `lock_suppressed` | #38F2FF cold | 68 % of the lens | the 6 px cold glow (full alpha, sigma 3.82) |
| `slot_p1`, `slot_p2` | `UIPalette.P1` / `P2` | 22 in a 30 box (the 20-unit box fills the texture) | none (the CSS slot shape is not an `.ico`) |
| `check_solid` | #04141A on the slot-colour square | 20 | none (the CSS lock mark is not an `.ico`) |
| `hourglass_caret` | #FFF0B0; its 1-unit black stroke never tints | 34 | none (the CSS caret is not an `.ico`) |
| `chevron_down` | #C0BAA6 (ink2) | 18 (bold) | generic |
| `dormant`, `cross` | (the plain glyphs; an ability slot uses `lock_dormant` / `lock_suppressed`) | - | generic |
| `gear`, `bolt`, `check`, `clock`, `spark` | defined, unused on the six screens | - | generic |

The HUD models' names (`HudAbilityIndicatorModel.StatusIconName`, `DefySealModel.IconName`, `FighterHudModel.StatusIconName` / `DefyIconName`) are all glass names already; the pulp-only names (`anchor`, `anchor_hollow`, `anchor_pip*`, `marker`, `prompt_circle`) are not, which is what TG-WIDGETS' aliases cover (`anchor` -> `beacon_anchor`, `anchor_hollow` -> `beacon_anchor_hollow`).

### 2.4 Gradient resources

| stem | file | fill | from -> to | use |
|---|---|---|---|---|
| `glass_lens_radial` | `glass_lens_radial.tres` (128) | radial | (0.32, 0.24) r 0.693 | lens fill: a circle polygon's UVs over the lens square |
| `glass_badge_radial` | `glass_badge_radial.tres` (128) | radial | (0.32, 0.24) r 0.632 | status / rewind / Time Freeze / Beacon badge |
| `glass_dial_radial` | `glass_dial_radial.tres` (128) | radial | (0.34, 0.26) r 0.694 | Integrity dial face |
| `glass_portrait_radial` | `glass_portrait_radial.tres` (128) | radial | (0.5, 0.3) r 0.671 | portrait frame's inner fill |
| `glass_menu_scrim` | `menu_scrim.tres` (512 x 4) | linear 90 deg | .96 / .84 26 % / .35 48 % / 0 66 % | full-screen TextureRect, stretch Scale; FlipH mirrors it |
| `glass_select_vignette` | `select_vignette.tres` (256 x 168) | radial | (0.5, 0.457) r 0.6 | **full-screen TextureRect with stretch `KeepAspectCovered`**: the texture's 256:168 aspect is the CSS ellipse's 1152:756, so its circle lands as `radial-gradient(60% 70% at 50% 45%)`. A GradientTexture2D's radial is a circle in UV space; stretched with Scale the x extent stays exact and y is 14 % short |
| `glass_fighter_top_shade` | `fighter_top_shade.tres` (4 x 256) | linear 180 deg | .80 / .64 42 % / .30 78 % / 0 | TextureRect 1920 x 300 at the top, stretch Scale |

Load with `GD.Load` / `PulpKit.Texture(stem)` (a `GradientTexture2D` is a `Texture2D`), not `AuthoredResources`. The CSS interpolates stops in premultiplied space and Godot's `Gradient` in straight RGBA, so the `.tres` carry premultiplied sub-stops wherever colour and alpha both change (the four radials).

### 2.5 Select hero frames

`select_heroes/<id>_idle_<n>.png` (n = 0..3) and `select_heroes/select_heroes.json`; catalog stems `hero_<id>_idle_<n>` and `hero_select_index`. Per hero: `fps` 6, `loop`, `texel_scale` 2, and per frame `file`, `size`, `pivot_x`, `feet_y` (image px from the top-left; the sole sits exactly on `feet_y`). The four frames share one crop, symmetric about the pivot, so the loop stays registered and a horizontal flip keeps the pivot. **Draw** (the mock's `spr()`): scale by 2.05, put `(pivot_x, feet_y)` on (the side's centre x, 700), flip P2. Sizes 124-210 x 218-238 px; 1.6 MB of PNGs for the nine.

## 3. `PulpKit.cs`

Unchanged signatures: `TryEntry`, `Texture`, `Expand`, `SharedPlate`, `Plate`, `Directory`. Added: `Has(stem)`, `IsGlass(stem)`, `PathOf(stem)`, `GlassDirectory`, `GlassIconStem(name, bold)`, `HasGlassIcon(name)` (allocates; not for draw paths) and `GlassIconTexture(name, bold)` (cached per name and weight: allocation-free after the first call, null when the set has no such icon). A glass stem's texture loads from its catalog `file` (PNG or `.tres`); a pulp stem from `Directory + stem + ".png"` as before. `Plate` sets `AxisStretchHorizontal = Tile` for a glass entry marked `"tile"` only, so no pulp plate changes behaviour.

## 4. Scripts and how to rerun

`tools/asset_src/glass/`:

| file | does |
|---|---|
| `icons_src/` | the 52 spec SVGs, copied verbatim, plus `beacon_anchor_hollow(.svg, _bold.svg)` (all 103 Appendix A paths verified present) |
| `glass_svg.py` | the kit's definition table (`KIT`, `ICON_TABLE`, `GRADIENTS`, the palette); writes the layered nine-slice SVGs and the normalized 4x icon copies (deriving the 12 bold weights) into `svg/`; appends groups `glass` / `glass_icons_raw` to `tools/asset_src/jobs.json`, replacing exactly those groups and keeping every other byte |
| `build_glass_images.py` | 4:1 premultiplied downsample; the panel shadow, focus glows, grabber and diamond glows, icon glows (true separable Gaussians); the pressed solve; the `.tres` gradients |
| `build_select_heroes.py` | the hero idle frames and `select_heroes.json` (roster from the content manifest) |
| `write_catalog.py` | `glass_kit.json`; refuses a size mismatch, an unprefixed stem or a pulp collision |
| `apply_import_settings.py` | `mipmaps/generate=true` in every glass PNG sidecar |
| `build_contact_sheet.py` | review sheets and comparisons into `D:/Projects/ftt-scratch/tg-captures/art/` (not shipped) |
| `build_all.ps1` | the chain; takes `D:/Projects/.ftt-gdunit-lock` itself (`-LockHeld` if the caller holds it), two `--import` passes |

Rerun from the repository root with `$env:GODOT_BIN` set to the console Godot: `powershell -NoProfile -File tools/asset_src/glass/build_all.ps1`. Then `git checkout --` the unrelated `*.png.import` sidecars every import rewrites with LF endings (verified line-ending-only: `git diff --ignore-cr-at-eol` empty) and commit only the glass kit's own new sidecars. `raw/` and `__pycache__/` are git-ignored.

## 5. Tests and the `[TestCase]` arithmetic

New `tests/ContentValidation/GlassKitContentTests.cs` (one `[TestSuite]`, `[RequireGodotRuntime]`), **+5**, as declared in plan 2.14: 3691 + 5 = **3696** (`[TestCase]` attribute count in this worktree: 3696).

| case | checks |
|---|---|
| `EveryCatalogFileLoadsAtItsStatedSizeWithMipmapsAndNoStemShadowsAPulpOne` | every PNG loads at its catalog size, its sidecar generates mipmaps, and `PulpKit.Texture(stem)` resolves it; every `.tres` is a `GradientTexture2D` of its size; every stem is glass-prefixed and absent from `pulp_kit.json`; every pulp stem still resolves through `PulpKit` to the pulp catalog |
| `EveryNineSliceKeepsACentreSliceAndItsSpecMarginsOrARecordedDeparture` | every nine-slice leaves a centre slice and equals UI_SPEC_GLASS 6.4's row (written into the test, not read from the catalog), or one of the four departures recorded both in the test and in the entry's `deviation`; every 6.4 row exists; `PulpKit.Plate` carries the margins and the tile flag |
| `EveryIconNameHasARegularAndABoldSixtyFourPixelTexture` | every icon in the catalog and in the `glass_icons` index has both weights at 64 x 64 (through `PulpKit.GlassIconTexture`), keyed `glass_ico_<name>[_bold]` |
| `EveryIconNameTheHudModelsProduceExistsInTheGlassSet` | every `StatusType` and `DefySealState` through the four model methods maps to a glass icon in both weights |
| `EveryRosterMemberHasFourBakedIdleFramesAndAFeetLine` | every `CharacterRoster.IDs` member has four mipmapped frames, a frame rate, and a feet line and pivot inside each frame, and its frames are catalog stems |

**Seen failing** (one run with five temporary mutations, each breaking only its own case, then reverted and checksummed back): `Failed: 5, Passed: 0, Total: 5`. 1: `glass_tag.png.import` with mipmaps off ("glass_tag: glass_tag.png.import does not generate mipmaps"); 2: `glass_panel`'s left margin 41 ("140,140,41,... where 6.4 says 140,140,42,..."); 3: `glass_ico_gear_bold` removed from the catalog (failed; its message line was cut by my output filter); 4: both `defy_spent` weights and its index name removed ("no glass icon for: defy_spent"); 5: Tubman's fourth frame removed from `select_heroes.json` ("tubman: 3 idle frames, not 4").

**Filtered runs** (lock held, no `testhost` running): `GlassKit|PulpKit|AbilityIcon` `Failed: 0, Passed: 11, Total: 11`; the plan's UI filter (3.2) `Failed: 0, Passed: 708, Total: 708` (exit 0, 20 s). `PulpKitContentTests` and `AbilityIconLibraryTests` unchanged. Build: 0 errors, the two known warnings (`CS8632`, `KLSG_ECS004`). Headless `--quit`: exit 0, no error.

## 6. Deviations from the spec and provisional choices

Each nine-slice deviation is also in its catalog entry's `deviation`.

1. **Panel gradients laid out for the 620 x 214 Vitals panel** (6.4 suggests stops at the slice boundaries). Measured over the 17 panel sizes the screens use, the edge hairline's mean alpha error against the CSS is 0.066 (worst 0.144, the 680 x 769 pause panel) against 0.189 (worst 0.271, the 1500 x 200 dialogue box) for the slice-boundary placement, which left the Vitals panel's left edge at alpha .44 where the CSS draws .78. Each of the 9 slice cells maps affinely onto the reference, with its own gradient; the cell boundaries fall on whole pixels (no seam, checked). After the change the Vitals panel's edges match the crop within 1-2 units.
2. **The disabled chevron**: canvas 104 x 70 (6.4: 100 x 70, whose 66 px centre is not a whole number of 10 px periods, so TILE would break the dashes at every seam). Its interior is the fill over the **averaged** stripe haze rgba(glow,.15), not hard stripes: in the CSS the stripes run under the fill's `backdrop-filter: blur(10px)`, which averages a 10 px pattern to a uniform haze (0.7 % residual), so only the ring shows dashes (as the element sheet's render does). Baked literally they ran as hard bars across the label.
3. **The focus split (plan 0.4 P5)**: overlays and a solved pressed box (2.1); three hover boxes added.
4. **Icon stems are `glass_ico_*`, not `ico_*`** (the plan's 2.3 lists `ico_` among the prefixes, but `pulp_kit.json` already keys its 26 icons `ico_<name>`, so `ico_venom` would collide, which the plan's own rule and test 1 forbid). Asset files are `icons/<name>.png`.
5. **Bold weights derived** for the 12 glyphs the spec draws in one weight (strokes x1.5: F 1.5 -> 2.25, D 1.35 -> 2.0, Defy 1.6 -> 2.4, the slot-lock cross 2.6 -> 3.9; a solid glyph's bold is its regular art), so every name has both weights (plan test 3) and a widget's "bold below 30 px" rule needs no exceptions. Only shown below 30 px; none of these 12 is, on the six screens.
6. **Glows per the CSS, not per 3.5's blanket sentence**: no glow on the slot shapes, `check_solid` and `hourglass_caret` (the CSS draws them outside `.ico`, so `.ico .ic`'s drop-shadow does not reach them), on `lock_dormant` and on the off pip (`shield_off`, the CSS `filter: none`). The pip and Defy-ready glows are coloured; the suppressed cross's is cold at full alpha. Sigmas: generic 2.5 bake px (7.2); the rest from CSS B / 2 at the display size.
7. **Two diamond textures for four uses**, as 6.4 lists: glow 14 (menu) also serves the select's glow 12; glow 12 (boss name) also serves the notch's glow 10.
8. **Gradient files keep the spec names** (`menu_scrim.tres` ...), their stems the glass prefix (`glass_menu_scrim` ...). `select_vignette.tres` is laid out for `KeepAspectCovered` (2.4).
9. The washes and focus edges are laid out for reference sizes (buttons 560 x 68, tabs 180, tags 130, fields 300); 6.4 does not say which size a stretched gradient should match.
10. The grabber's ring is 2.4 px drawn (the CSS 3 px under `scale(.8)`), its glow sigma 6.4.

## 7. Minimum strokes (6.6)

| element | at its smallest 100 % display | 6.6 floor | met |
|---|---|---|---|
| glass edges (panel, button, tab n/a, field) | 1.5 px (1.41 px along a 45-degree chamfer, the spec's own definition) | 1.5 | yes |
| tag bar | 2 px | 2 | yes |
| icons, regular (F 1.5 units) | 1.41 px at 30 px | 1.41 | yes (D 1.35 units: 1.27 px at 30; 6.6 counts F) |
| icons, bold (F 2.25 units) | 1.55 px at 22 px (the flag), 1.74 at 24.8 | 1.39 at 90 % | yes |
| slot-lock cross (2.6 units) | 2.98 px at 36.7 px (Fighter), 4.1-5.1 at 50-63 | - | yes |
| Defy seal (1.6 units) | 1.6 px at 32 px | - | yes |
| **`chevron_down` bold (D 2.0 units)** | **1.13 px at the spec's 18 px** | 1.4 | **no**: the spec puts this D-only glyph at 18 px (7.2); at 22 px it would be 1.38 |

## 8. Captures and comparison

`D:\Projects\ftt-scratch\tg-captures\art\`: `glass_kit_sheet.png` (every texture 1:1 and 3x), `glass_ninesliced.png` (the nine-slices drawn as StyleBoxTexture draws them at the spec sizes, over the menu art and the Pompeii scene), `glass_icons_sheet.png` (both weights, then tinted at 18-50 px on dark and on the eruption core), `glass_gradients.png` (the `.tres` parsed back and filled as GradientTexture2D does, then in context), `glass_heroes_sheet.png`, and three side-by-sides with the spec crops (build, crop, 50 % overlay): `compare_menu_column.png`, `compare_pause_panel.png`, `compare_hud_left_column.png`. Labels, markers, the A prompt and the sheen in those are approximations drawn by the sheet script (TG-WIDGETS draws the real ones).

- **Menu column** (`rec_01`): chevron geometry coincides in the overlay; the lit focus edge matches the crop (210,174,78 vs 210,174,79), the normal fill too (14,17,32 vs 13,17,32); the focused fill is about 5 units darker than the exact CSS (section 2.1) and about 11 darker than the browser render, which adds its backdrop blur.
- **Pause panel** (`rec_03`): geometry, tag, edge brightness and rows coincide; the build panel reads darker and bluer than the crop, by design (1.4: the build fill .92 / .95 replaces the blurred .80 / .88).
- **Story HUD left column** (`rec_04`, over the mock's own scene): the three panels and tags coincide; edges within 1-2 units; the fill darker and bluer, as above.
- **Not seen**: anything in the engine (no theme, scene or widget uses the kit yet; TG-THEME step 2 wires it), filtering at UI scale 1.4 or on a Steam Deck, the focus slide, motion, a pad, Reduced Temporal Effects.

## 9. Cross-edits, gaps, what did not ship

- **Cross-edits**: none. Shared file: `tools/asset_src/jobs.json` (two groups appended; nothing else touched).
- **Kit gaps / notes for consumers**: TG-THEME: the hover wiring (2.1 items 2-3), the tile flag, content hints, file paths relative to the glass directory (`build_ftt_theme.py`'s `kit_tex` joins `KIT_DIR` and the entry's `file`). TG-WIDGETS: `PulpKit.GlassIconTexture(name, bold)` for `PulpIcon`; `PulpKit.Expand("glass_btn_focus")[2]` = 14 for the `FocusRowGap` derivation; the radials' UV centres and radii (2.4). TG-SELECT: `select_heroes.json` and `select_vignette` with `KeepAspectCovered`.
- **Did not ship**: the code-drawn parts of 6.2 (lens, badge, dial, tube, portrait frame, tile, toggle, READY badge, scroll bar, streak, sheen, title shader) by design; no pulp asset was moved or deleted (the closeout does that); the old pre-pulp glass leftovers are untouched.

## 10. Questions for the orchestrator

1. Accept `glass_ico_*` as the icon prefix (6.4 / plan 2.3 said `ico_`, which collides)?
2. Accept the P5 split as built (overlay focus, solved pressed, three hover boxes) and the focus-on-hover recommendation for TG-THEME / TG-WIDGETS?
3. Accept the panel gradients laid out for the Vitals panel instead of 6.4's slice-boundary placement (measured, section 6.1)?
4. Accept the disabled interior as the averaged haze rather than literal stripes (section 6.2)?
5. `chevron_down` at 18 px draws a 1.13 px stroke; raise its display size to 22 px, or accept?

## 11. Drafted doc text

**AGENTS.md**, repository map: `assets/sprites/ui/glass/  The Temporal Glass UI kit (TG-ART): nine-slices (slab, tag, chevron button states, tabs, fields), the slider grabber and diamond glows, 34 ICN-A icons in two weights (glow baked, tinted by modulate), seven GradientTexture2D resources, the select heroes' baked idle frames, and glass_kit.json, its catalog; built by tools/asset_src/glass/build_all.ps1`, and `tools/asset_src/glass/  The glass kit's sources (icons_src/, the generated svg/) and its chain (glass_svg.py, build_glass_images.py, build_select_heroes.py, write_catalog.py, apply_import_settings.py, build_contact_sheet.py, build_all.ps1)`.

**AGENTS.md**, the glass UI bullet (closeout): "The kit's art is built reproducibly from `tools/asset_src/glass/` (the spec's SVG sources rendered at 4x through `generate_assets.gd`, downsampled in premultiplied space, glows and shadows composited in PIL); `PulpKit` reads `glass_kit.json` first. Focus textures are overlays drawn over the state box (plan 0.4 P5); `GlassKitContentTests` pins the catalog, the 6.4 margins, both icon weights, the HUD models' icon names and the select hero frames."

**CLAUDE.md**: baseline 3696 after TG-ART (+5); the glass kit rebuild (`build_all.ps1`) takes the GdUnit lock itself and its `--import` passes leave LF churn on unrelated sidecars (revert them).

**Ledger** (`PROVISIONAL-UI-GLASS-ART`): the choices of section 6 (and the hover boxes), the focus deviation numbers of 2.1, the `chevron_down` stroke; hands-on acceptance owed.
