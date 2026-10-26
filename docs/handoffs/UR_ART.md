# UR-ART handoff: the Retro Pulp art kit, icons, ability icons and cover poses

Branch `ur/art` (from `main` at `ae830298`). Phase A of `docs/UI_REDESIGN_2026-10-26_PLAN.md`. No theme, palette, font, scene or screen-script edit; the only C# is `scripts/UI/AbilityIconLibrary.cs` and two test suites.

## 1. Summary

Everything UI_SPEC section 7 asks for, plus every component section 6.2 calls a texture, is built from sources by one chain (`tools/asset_src/pulp/build_all.ps1`):

- **~70 kit PNGs** under `assets/sprites/ui/pulp/`: panels, card frames (neutral / P1 / P2), bubble and tail, 15 button plates (normal / focus / selected / pressed / disabled x heights 80 / 64 / 58), bar tracks (Story HP 30, Fighter HP 44, meter 16, stat 14, boss 44), tabs, toggle and slider pieces, slot frames (square, ready, Ultimate circle, Ultimate ready), boss nameplate and PHASE tab, panel tag, select and cover banners, READY stamp, key tag, **key-cap pill (keyboard / mouse prompt)**, portrait roundel back, Integrity dial back and ticks, badge disc, burst star, Continue sticker, P1 / P2 star badge (3 layers), the four hatch tiles, three dot tiles, rays, the full-screen halftone, the select split, the card photo field and halftones, the posterized cover art, and `burst.tres` / `card_field.tres` (`GradientTexture2D`, no script).
- **26 ICN-B icons x 3 layers** under `.../icons/` (the 21 the spec lists, the Act III Beacon `anchor`, `anchor_hollow`, `anchor_pip`, `anchor_pip_cracked`, and the button `marker`).
- **36 ability-slot icons** (9 characters x 4 slots, 144 x 144) under `.../ability_icons/`, `scripts/UI/AbilityIconLibrary.cs`, and its suite.
- **3 cover hero poses** (Joan, Einstein, Cleopatra `victory`) under `.../cover_heroes/` with their feet anchors.
- `assets/sprites/ui/pulp/pulp_kit.json`: the machine-readable catalog (size, nine-slice margins, expand margins, mipmap flag, what each file serves). UR-THEME can read margins from it; `PulpKitContentTests` pins it.
- Reproducible: SVG sources are written by `build_pulp_svgs.py` (ThorVG-safe: paths, gradients, opacity only; the hard shadow and rings are even-odd paths with the border box cut out, as CSS clips them), rendered by `generate_assets.gd` (groups `pulp`, `pulp_icons_raw`), and the PIL scripts do the image-derived and per-pixel work.

**Look check.** `D:\Projects\ftt-scratch\ur-captures\art\`: `sheet_plates.png`, `sheet_sprites.png`, `sheet_icons.png`, `sheet_ability_heroes.png` (contact sheets), and `demo_cover.png`, `demo_select.png`, `demo_story.png`, `demo_fighter.png`, `demo_pause.png` (compositions of the shipped PNGs with PIL placeholder text, built by `build_kit_demo.py`, a stand-in for the Phase B screens). Compared against `mix_menu` / `mix_select` / `mix_story`: the plates, ink rings, hard shadows, button focus pose, trading cards (field, halftone, P1 / P2 rings), banner, stat card, bubble, stars, sticker, burst, rays and posterized cover read as the mockup. Differences I saw: the posterized sky banding is a little coarser than the mock's (same posterize, no browser filter chain); the demo's text is Arial / Georgia, not Cinzel / Outfit; the demo does not draw the HP fills' hatch, the Rally echo or the Ultimate ring (code, not textures); the disabled button's stripes show a wavy stretch in the demo because the demo stretches the centre, while the engine must tile it (section 2.2). **Not seen:** anything in the engine (no scene uses these yet), the focus tilt on a real control, 140 % UI scale.

## 2. The asset catalog (UR-THEME wires from this)

Paths are under `res://assets/sprites/ui/pulp/`. "Margins" = `StyleBoxTexture.texture_margin_left/top/right/bottom`; "expand" = `expand_margin_*` (how far the art extends outside the CSS border box, so the control rect equals the spec's layout box). All values are texture pixels at the 1x design scale.

### 2.1 How to use a plate

1. `StyleBoxTexture` with the file, the margins, the expand margins, `draw_center = true`. Do **not** author scripted StyleBox resources.
2. Slanted plates (buttons, tracks, tabs, banners, nameplate) carry the slant in their end slices; the middle column stretches horizontally only, so use a texture for the control's height (buttons exist at 80 / 64 / 58, tracks at 14 / 16 / 30 / 44).
3. Rotated or scaled plates (card frames, focus buttons, banners, boss plates, stamp, tag) are flagged `mip` in the table; their `.png.import` has `mipmaps/generate=true`, so give them linear-with-mipmaps filtering where they tilt.
4. The disabled button's centre carries diagonal stripes: set `axis_stretch_horizontal = TILE` (the centre column is a whole multiple of the 17 px stripe period for every height; also in `pulp_kit.json` as `axis_stretch`).
5. Translucent plates (`panel_dark` centre #090C1E @0.80, `btn_normal`) are cut out under the box exactly like CSS, so the scene shows through; the ring and the shadow are opaque.
6. Fixed-size sprites (slot Ultimate frames, dial, roundel, sticker, star badge, toggle knob, slider grabber) are placed by their expand margins: the CSS box origin inside the texture is (expand L, expand T). The 2x bakes (`portrait_roundel`, `dial_back`, `dial_ticks`, `sticker_continue`, `star_badge_*`) list their expand in **1x** units; draw them at half the texture size.

### 2.2 Textures (generated: `build_pulp_svgs.py`, `build_pulp_images.py`)

| file | size px | nine-slice L/T/R/B | expand L/T/R/B | import | serves |
|---|---|---|---|---|---|
| `badge_disc.png` | 64x64 |  |  | mip | plate 18: status / HUD sticker disc (radial at 35% 30%: #FFF7DE, paper 60%, #D8C690); the 3px ink ring and the 3,4 shadow are drawn in code (their ... |
| `banner_cover.png` | 66x110 | 23/14/35/21 | 7/0/19/12 | mip | plate 25: cover banner THROUGH TIME as the dark ink plate (orchestrator ruling Q1: no pulp red) |
| `banner_select.png` | 65x101 | 23/13/34/20 | 7/0/18/11 | mip | plate 24: select banner (gold), 90 high, skew 0.1405, border 7, shadow 11,11; rotate -1deg |
| `bar_track_boss.png` | 54x58 | 19/10/27/18 | 8/3/16/11 | mip | plate 10: boss bar track, 44 high, skew 0.1763, border 5 + paper ring 3 + shadow 8,8; inner area #3A3144 (tracker accent, fill #FF5A5A) |
| `bar_track_h14.png` | 35x24 | 11/8/16/13 | 4/2/9/8 | mip | plate 9: HP / meter / stat track, 14 px high (Story HP 30, Fighter HP 44, Fighter meter 16, stat bar 14); centre #3A3144 |
| `bar_track_h16.png` | 37x26 | 12/8/17/13 | 5/2/10/8 | mip | plate 9: HP / meter / stat track, 16 px high (Story HP 30, Fighter HP 44, Fighter meter 16, stat bar 14); centre #3A3144 |
| `bar_track_h30.png` | 43x40 | 15/8/20/13 | 6/2/11/8 | mip | plate 9: HP / meter / stat track, 30 px high (Story HP 30, Fighter HP 44, Fighter meter 16, stat bar 14); centre #3A3144 |
| `bar_track_h44.png` | 49x54 | 18/8/23/13 | 8/2/13/8 | mip | plate 9: HP / meter / stat track, 44 px high (Story HP 30, Fighter HP 44, Fighter meter 16, stat bar 14); centre #3A3144 |
| `boss_nameplate.png` | 49x69 | 17/9/24/14 | 5/0/12/7 | mip | plate 11: boss nameplate, #FF5A5A, 62 high (687 wide in HUD-S1); rotate -1deg |
| `boss_phase_tab.png` | 24x39 | 6/6/10/10 | 0/0/4/4 | mip | plate 12: boss PHASE tab, accent, 35 high; rotate +2deg |
| `btn_disabled_h58.png` | 57x68 | 18/8/22/12 | 8/3/12/7 | mip | BTN-B disabled plate, 58 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_disabled_h64.png` | 57x74 | 18/8/22/12 | 8/3/12/7 | mip | BTN-B disabled plate, 64 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_disabled_h80.png` | 63x90 | 21/8/25/12 | 10/3/14/7 | mip | BTN-B disabled plate, 80 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_focus_h58.png` | 61x81 | 21/13/32/22 | 11/6/22/17 | mip | BTN-B focus plate, 58 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_focus_h64.png` | 63x87 | 22/13/33/22 | 12/6/23/17 | mip | BTN-B focus plate, 64 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_focus_h80.png` | 67x103 | 24/13/35/22 | 13/6/24/17 | mip | BTN-B focus plate, 80 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_normal_h58.png` | 51x71 | 18/9/25/15 | 8/3/15/10 | mip | BTN-B normal plate, 58 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_normal_h64.png` | 51x77 | 18/9/25/15 | 8/3/15/10 | mip | BTN-B normal plate, 64 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_normal_h80.png` | 57x93 | 21/9/28/15 | 10/3/17/10 | mip | BTN-B normal plate, 80 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_pressed_h58.png` | 44x64 | 18/8/18/8 | 8/3/8/3 | mip | BTN-B pressed plate, 58 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_pressed_h64.png` | 44x70 | 18/8/18/8 | 8/3/8/3 | mip | BTN-B pressed plate, 64 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_pressed_h80.png` | 50x86 | 21/8/21/8 | 10/3/10/3 | mip | BTN-B pressed plate, 80 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_selected_h58.png` | 51x71 | 18/9/25/15 | 8/3/15/10 | mip | BTN-B selected plate, 58 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_selected_h64.png` | 51x77 | 18/9/25/15 | 8/3/15/10 | mip | BTN-B selected plate, 64 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `btn_selected_h80.png` | 57x93 | 21/9/28/15 | 10/3/17/10 | mip | BTN-B selected plate, 80 px high (plate rows 4-8). Hover = focus. Skew 0.1405 baked. |
| `bubble.png` | 78x78 | 32/32/38/38 | 0/0/6/6 | mip | plate 27: speech bubble body (tail is bubble_tail.png); idle variant = modulate #D8CDB2 |
| `bubble_tail.png` | 40x28 |  |  | mip | speech bubble tail: black triangle 40x28, apex 6px from the left; sits on the bubble's bottom edge at left 34, overlapping the bubble border by 5px |
| `burst_star.png` | 1000x1000 |  |  | mip | menu burst (MENU-B): 18-point star, 1000x1000 at (60,300); runtime opacity 0.92. The same gradient is burst.tres |
| `burst.tres` | x |  |  |  | GradientTexture2D: the menu burst's radial fill (stops 0 / .537 / .99 of the 500 px radius); burst_star.png is the baked star |
| `card_field.png` | 188x204 |  |  |  | CARD-B photo window field (188x204): radial gradient at (50%,118%) #FFE08A / accent 42% / #C1752B, bottom 4px black rule |
| `card_field.tres` | x |  |  |  | GradientTexture2D: card photo field, radial from (50%,118%); card_field.png is the exact bake |
| `card_frame.png` | 33x35 | 9/10/16/17 | 3/3/10/11 | mip | plate 20: trading card frame (CARD-B). centre #0C0A10; paper ring 3 + border 4 + shadow 7,8 |
| `card_frame_p1.png` | 45x46 | 14/14/23/24 | 8/8/17/18 | mip | plate 21: card, P1 pick (cyan ring 4 + black 4, shadow 9,10) |
| `card_frame_p2.png` | 45x46 | 14/14/23/24 | 8/8/17/18 | mip | plate 22: card, P2 pick (red ring) |
| `card_halftone.png` | 188x204 |  |  |  | card photo halftone (188x204): black dots pitch 8, alpha .26, 200deg fade; draw over the field |
| `cover_art.png` | 1920x1080 |  |  |  | MENU-B background: the Level 6 Pompeii scene (assets/environments/pompeii/*) posterized to 9 levels per channel (0 .13 .26 .40 .54 .68 .82 .92 1), ... |
| `dial_back.png` | 250x276 |  | 4/16/9/10 | mip | plate 28: Integrity stopwatch back: crown (34x24, shadow 3,3), black ring 4, shadow 5,6, paper face. 2x bake; the 112 face's box origin is (4,16) i... |
| `dial_ticks.png` | 250x276 |  | 4/16/9/10 | mip | Integrity dial overlay: 12 ticks (every 30deg, 2deg wide, 9 long, ring inset 2%) and the 3px black ring around the wedge disc (r 43-46). Same canva... |
| `dot_10.png` | 10x10 |  |  | tile | halftone corner of dark / paper panels: modulate rgba(255,255,255,.17) (dark) / rgba(0,0,0,.16) (paper); mask diagonal in a shader |
| `dot_8.png` | 8x8 |  |  | tile | halftone of the card photo window (pitch 8): modulate rgba(0,0,0,.26) |
| `dot_9.png` | 9x9 |  |  | tile | halftone for the cover / portraits / select split (pitch 9): modulate rgba(0,0,0,.34) / .28 |
| `halftone_screen.png` | 1920x1080 |  |  |  | full-screen halftone for the cover (layer opacity 0.55) and the select split (0.80): black dots pitch 9 r1.7-2.2, alpha .34 x vertical mask .2 (top... |
| `hatch_boss.png` | 20x20 |  |  | tile | boss fill overlay: 135deg stripes, rgba(0,0,0,.20) (alpha baked) |
| `hatch_disabled.png` | 17x17 |  |  | tile | disabled button stripes #2C2733 / #221E29, 45deg (spec 12px tile is a perpendicular period; 17px is the horizontal period) |
| `hatch_hp.png` | 16x16 |  |  | tile | HP / meter / stat fill overlay: 135deg stripes, rgba(0,0,0,.22) (alpha baked); clip to the fill, tile in pre-shear space |
| `hatch_random.png` | 34x34 |  |  | tile | Random card photo window hatch #2A2434 / #1B1724, 45deg |
| `key_tag.png` | 18x22 | 5/5/5/5 | 0/0/0/0 | mip | plate 16: slot key tag (S1 S2 MOV ULT), paper, border 3; rotate -4deg at (-10,-12) of the slot |
| `keycap.png` | 48x38 | 21/19/21/19 | 2/2/2/2 |  | key-cap prompt (keyboard / mouse): black pill 34 high, cream border 2 + black ring 2 (UI_SPEC 3.7 prompt bar glyphs, wide variant); label #FFF3D8 c... |
| `panel_dark.png` | 36x36 | 11/11/17/17 | 3/3/12/12 |  | plate 1: dark ink panel (PNL-B). centre = bg1 @0.80; ring 3 black + paper border 3 + hard shadow slab 9,9 |
| `panel_paper.png` | 33x33 | 11/11/14/14 | 0/0/9/9 |  | plate 2: paper panel (stat cards): #000 border 3, paper centre, shadow slab 9,9 |
| `portrait_halftone.png` | 112x112 |  |  |  | portrait roundel halftone (112x112 square, clip to the disc): black dots pitch 9 alpha .28, 200deg fade; scale with the roundel diameter / 112 |
| `portrait_roundel.png` | 278x278 |  | 10/10/17/17 | mip | plate 17: portrait roundel back (shadow circle (6,7), black 4 + paper 3 + black 3 rings, gold disc with a #FFE39A core). 2x bake; the 112 disc's bo... |
| `rays.png` | 1920x1080 |  |  |  | cover rays: 30 rays, 4deg lit / 8deg clear from (614,713), rgba(244,182,59,.22) baked at .17 so an ADD blend matches the mock Screen blend; blend A... |
| `select_split.png` | 1920x1080 |  |  |  | CARD-B background: 108deg split #138A94 (P1) / #CF3A2C (P2), 1% black divider, vignette; the halftone_screen goes over it at 0.80 |
| `slider_fill.png` | 16x12 | 5/1/5/1 | 2/0/2/0 | mip | slider fill: accent skewed bar, 12 high; lay it inside the black body of slider_track (2px inset), cut its right end at the value |
| `slider_grabber.png` | 31x31 |  |  | mip | slider handle: 28 px circle, paper, border 4, shadow 3,3,0 |
| `slider_track.png` | 29x25 | 9/5/12/8 | 5/3/8/6 | mip | slider track 16 high, black body, paper ring 3, shadow 3,3,3, skew 0.1763 |
| `slot_frame.png` | 33x34 | 10/10/15/16 | 2/2/7/8 | mip | plate 13: square ability slot frame (Story 74, Fighter 54, select 50); centre #2A2434 |
| `slot_frame_ready.png` | 37x38 | 12/12/17/18 | 4/4/9/10 | mip | plate 14: ready slot frame (2 accent + 2 black rings, shadow 5,6,4) |
| `slot_ult.png` | 101x102 |  | 2/2/7/8 | mip | plate 15: Ultimate slot frame, circle 92 (not stretchable) |
| `slot_ult_ready.png` | 105x106 |  | 4/4/9/10 | mip | plate 15 ready: accent ring + black ring, shadow 5,6,4 |
| `stamp_ready.png` | 26x59 | 7/7/11/11 | 0/0/5/5 | mip | plate 26: card READY stamp as the dark ink plate (orchestrator ruling Q1); rotate -10deg |
| `sticker_continue.png` | 580x584 |  |  | mip | plate 29: Continue sticker, 14-point star (outer 50% / inner 40% of 270), accent fill, 5px ink outline, shadow 10,12. 2x bake; the star's box origi... |
| `tab_normal.png` | 44x60 | 16/8/20/12 | 6/2/10/6 | mip | Settings tab, normal (Appendix B): bg2 / accent fill, border 4, ring 2 paper, shadow 4,4,2, skew 0.1405, 52 high (agent default height) |
| `tab_selected.png` | 44x60 | 16/8/20/12 | 6/2/10/6 | mip | Settings tab, selected (Appendix B): bg2 / accent fill, border 4, ring 2 paper, shadow 4,4,2, skew 0.1405, 52 high (agent default height) |
| `tag_panel.png` | 38x42 | 13/6/17/9 | 4/0/8/4 | mip | plate 3: panel title tag (VITALS, ABILITIES, pause location, card style); text un-skewed; rotate -1.2deg |
| `toggle_knob.png` | 40x34 |  |  | mip | toggle knob 34x34, paper, border 4, skewed 0.1763 (at top 6, left 5 off / right 5 on) |
| `toggle_track_off.png` | 150x52 | 17/8/21/12 | 7/2/11/6 | mip | toggle track off (132x44 border box): fill #3A3144; text and knob are separate |
| `toggle_track_on.png` | 150x52 | 17/8/21/12 | 7/2/11/6 | mip | toggle track on (132x44 border box): fill #8CF0B8; text and knob are separate |

Notes on the table:
- **Margins differ from UI_SPEC section 7.1 on purpose.** The spec's `panel_dark` margins (6/6/15/15) put the start of the hard shadow (3 px below the box top on the right edge) outside the corner slices, so the stretched edge slice would smear it; mine (11/11/17/17) keep it inside. The same reasoning gives `card_frame` 9/10/16/17 and a bubble 32/32/38/38 (the radius-30 arc must be inside the corner). Sizes are therefore smaller than the spec's 48 / 64 / 112 x 96 (the centre slice is 8 px, so the texture is the corners plus 8).
- `bar_track_*`: the CSS track is `w - 8` wide with `margin-left: 8`; the track plate draws from the border box (border 3, paper ring 2, shadow 5,6,2), the inner area (box inset 3) is #3A3144 and takes the fill, tracker, echo, hatch, highlight and the 2 px decile ticks, all drawn in code.
- Button plates: the plate is the whole parallelogram; the label sits at x + 64 (compact x + 54), the marker at x + 26. The focus pose (translate +16, rotate -1.2 deg, scale 1.04) is a tween, not baked; swap normal / focus art on focus. `btn_pressed_*` has no shadow (the pose moves by +7,+7).
- `tag_panel`, `banner_*`, `boss_*`, `stamp_ready`, `key_tag`: the rotations / skews in UI_SPEC 3.0 that are control rotations (-1.2, -1, +2, -10, -4 deg) are **not** baked; the slants (skew) are.
- `rays.png` is baked at alpha .17 (spec .22 is for a Screen blend, which Compatibility does not have; Add at .22 over-lightens).
- `halftone_screen.png`: dots (black) with the vertical mask baked; apply layer opacity 0.55 (cover) / 0.80 (select) by `modulate`. The select split uses it too.
- `cover_art.png` has no vignette (the mockup's cover shows none); its banding is the spec's 9-level posterize.
- `slider_*`, `toggle_*`, `tab_*` come from UI_SPEC Appendix B (never drawn in the mock); the tab height 52 is my default.
- `badge_disc.png` is the disc only: the 3 px ink ring, the (3,4) shadow and the dashed empty ring are drawn in code because their thickness must not scale with the badge.

### 2.3 Layer files

- `star_badge_{fill,ink,sh}.png` (2x, 262 x 264): `sh` (black silhouette, the (5,6) shadow offset already baked) under, `fill` (white star, tint P1 #00F0FF / P2 #FF3366) in the middle, `ink` (the 4 px outline ring, outside only) over. The star box origin is (4,4) in 1x units.
- `cover_heroes/<id>_victory.png` + `cover_heroes.json`: `size`, `feet_px_from_bottom`, `pivot_x` (image centre) per pose. Joan 174 x 335 (feet 15), Einstein 250 x 257 (feet 14), Cleopatra 170 x 304 (feet 14). The mock's scale (Joan 1.62, Einstein 1.55, Cleopatra 1.50) applies to these pixels directly, feet on y = 1020 (so the image bottom is at 1020 + feet x scale). These are 2x-texel retro v2 frames **without** the ink outline and shadow; the outline shader / a black offset copy is UR-MENU's. Mipmapped.

## 3. The icon list and tint guidance

Every glyph is three 64 x 64 files (the marker 40 x 56): `ico_<name>_fill.png` (white; tint by `modulate`), `ico_<name>_ink.png` (black outline ring outside the fill, plus the details and the paper extras; **draw it over the fill, never tinted**) and `ico_<name>_sh.png` (black silhouette; draw it **under** the fill at a (2,2) **screen**-pixel offset, the spec's drop shadow). The spec's "two layers" needed this third file: with only fill and ink the ink's outline has to be both under the fill (outline) and over it (details), and a shadow made from the ink alone is hollow. Use `fill + ink` if you want no shadow. The ink layer is the full 2.6-unit outline minus the fill, so the visible outline is the spec's 1.3 units; glyphs are clipped to the 32-unit box like the mock's svgs.

| name | what | tint (modulate of `_fill`) | shown at |
|---|---|---|---|
| `dilation`, `static`, `root` | control statuses (Time Dilation, Static Charge, Root) | #8FC7FF | 25-28 in a 40-44 badge |
| `venom`, `burn` | damage statuses (Venom, Radiant Burn) | #FF7A5C | same |
| `suppress` | Suppression status / ghost | #38F2FF (cold; the Severed) | 24-27 |
| `shield` | block pip | #DFE8FF (off: alpha .28 + grayscale) | 30 Story / 24 Fighter |
| `hourglass` | rewind counter; dialogue caret | #FFE39A (low: #FF5A5A); caret #FFF0B0 | 29 in a 46 badge; 34 |
| `freeze` | Time Freeze | #FFE39A | 29 in a 46 badge |
| `dust` | Chronal Dust | #F3C95B | 38 |
| `star4` | Fighter stock | P1 #00F0FF / P2 #FF3366 (off: alpha .22) | 28 |
| `flag` | objective | #FFF0B0 | 22 |
| `dormant` | dormant Legacy slot | #8A8374 @0.9 | 16 % inset of the slot; flat (no ink / shadow worth drawing) |
| `cross` | suppressed slot | #38F2FF + a 6 px glow | same; flat |
| `defy_ready` / `defy_notready` / `defy_spent` / `defy_barred` | the four Defy seal states: ring + core + four rays / bare ring / three arcs / ring + slash | #FFF0B0 / #8A8374 / #8A8374 / #FF5A5A | 34 Story, 32 Fighter. Shape carries state; `defy_ready`'s `_ink` is the black core ring |
| `slot_p1` / `slot_p2` | player slot shapes (triangle / circle), flat | #00F0FF / #FF3366 | 30 box, glyph 22; 28 in card badges |
| `prompt_circle` | A / B prompt circle | `_fill` ring #FFF3D8 (or the label colour inside a focused button); `_ink` = black disc + outer ring (skip it inside a focused button) | 34-36; the letter is a live Label |
| `anchor` | Act III Beacon anchor (agent design) | #F3C95B | 24 |
| `anchor_hollow` | the same at zero charges (the `_fill` is an outline stroke, so it is see-through) | #8A8374 or #FF5A5A | 24 |
| `anchor_pip` / `anchor_pip_cracked` | the Snap pips (diamond / diamond with a crack) | #F3C95B, fade by alpha | 8-12 |
| `marker` | the button marker triangle (20 x 28 at 2x) | #8A8374 normal, #0C0A10 focus, #E0412F selected, #4A4333 disabled; `_sh` at (2,2) | 20 x 28 (compact 16 x 22) |

The ICN-B status glyphs stay distinct per status and the four Defy seals differ in shape, not hue (HUD contract G12).

## 4. Scripts and how to rerun

All from the repository root, `py` launcher, Godot console binary, **holding `D:/Projects/.ftt-gdunit-lock` for every Godot launch**:

```powershell
$env:GODOT_BIN = "D:\Projects\Godot_v4.7.1-stable_mono_win64_console.exe"
./tools/asset_src/pulp/build_all.ps1     # the whole chain, ends with the contact sheets
```

| script | does |
|---|---|
| `pulp_svg.py`, `icons_geometry.py` | shared geometry: `Plate` (border / rings / hard shadow / skew / radius, cut out under the box, margins computed), `Box`, `star`, `clip_convex`, `diagonal_bands`; the Appendix A icon paths and the extra glyphs |
| `build_pulp_svgs.py` | writes `tools/asset_src/pulp/svg/*.svg`, appends the jobs (groups `pulp`, `pulp_icons_raw`) to `tools/asset_src/jobs.json` (idempotent, purely additive), `catalog_svg.json` |
| `build_pulp_images.py` | PIL: `cover_art`, `rays`, `halftone_screen`, `select_split`, `card_field`, `card_halftone`, `portrait_halftone`, the two `.tres` |
| `finish_pulp_icons.py` | composites `tools/asset_src/pulp/raw/*_raw.png` (git-ignored) into the three icon layers and the star badge layers |
| `build_ability_icons.py [id ...]` | the 144 px ability icons from `assets/sprites/vfx/<id>/<id>_ability_vfx_atlas.png`: rows special_1 / special_2 / movement / ultimate, the middle frame (or the fullest if the middle is nearly empty), trimmed, 6 % padding. The roster is read from `content_manifest.csv`, never listed |
| `build_cover_heroes.py [id[:anim[:frame]] ...]` | bakes poses from the retro v2 manifests |
| `write_catalog.py` | writes `pulp_kit.json` (assets) and `catalog.json` (tools) |
| `apply_import_settings.py` | sets `mipmaps/generate=true` in the sidecars of every file the catalog flags `mipmaps` (run between two `--import`s) |
| `build_contact_sheet.py`, `build_kit_demo.py` | the review PNGs |

A new roster character: add the manifest row and its VFX atlas, rerun `build_ability_icons.py` and `write_catalog.py`, import; `AbilityIconLibraryTests` fails until all four slots exist.

## 5. Tests and the `[TestCase]` arithmetic from 3546

| suite (own file, `[RequireGodotRuntime]`) | cases | what |
|---|---|---|
| `tests/ContentValidation/AbilityIconLibraryTests.cs` | 3 | every roster character has all four 144 x 144 slot icons (roster from `CharacterRoster.IDs`); an unknown / empty / null ID gives `null`; the slot-name mapping |
| `tests/ContentValidation/PulpKitContentTests.cs` | 3 | every file in `pulp_kit.json` loads as a texture of the stated size (icon layers: the stated size; star badge / ability icons / hero poses by file); every nine-slice leaves a centre column; every icon has its three layers |

3546 + 3 + 3 = **3552**. Observed full run (orphans none, lock held): `Failed: 0, Passed: 3552, Total: 3552, Duration: 2 m 10 s`. Build: 0 errors, the two known warnings (`CS8632` in the vendored GdUnit file, Klotho `KLSG_ECS004`). No existing test changed. The suite caught a real defect in my first catalog (the key-cap pill's margins overran its height); fixed in `Plate.margins()` (a pill stretches horizontally only).

## 6. Provisional choices

- Margins and texture sizes differ from the spec's as explained in 2.2.
- One button texture per height and state (15 files) instead of a stretchable texture: the slant makes a height-independent 3-slice wrong.
- Cover banner and READY stamp use the dark ink plate (#0C0A10 @0.86), orchestrator ruling Q1; the HP tracker colour is not a texture.
- The `_sh` third layer per icon (section 3). Anchor icon / pips and the marker triangle are my designs.
- Rays baked at alpha .17 for an Add blend; the cover art has no vignette; the posterize is nearest-of-nine-levels, then saturate 1.2 and contrast 1.08.
- Ability icon frame: the middle frame of each atlas row (or the fullest). The mock hand-picked some cells (frame 0 for Einstein's S1 and Ultimate, and Joan's S1); I did not copy those picks, so a few icons differ from the mockup (Einstein S1 and Ultimate, Joan S1, possibly others). Reviewable in `sheet_ability_heroes.png`; `build_ability_icons.py` takes the column from `col = 1 if ...`, a per-icon override table is the place to add a hand pick.
- Hero poses are `victory` frames for Joan (0), Einstein (2), Cleopatra (0), as the mock.
- Tab height 52, toggle / slider box sizes from Appendix B.
- The select-card name plate, stat card, stat bars' fill and the Ultimate ring are code: no texture.

## 7. What did not ship / open questions

- **No scene, theme or screen edit** (by brief): nothing displays these yet; engine appearance (filtering at 1.4 UI scale, the focus tilt, mipmap behaviour of the tilted 9-slices, `axis_stretch` TILE on the disabled button) is unverified.
- `StyleBoxTexture` cannot scale its art, so at UI scale 140 % the 1x plates are drawn 1:1 and stretched in the middle only (corners, rings and shadows stay 1x px); if the theme scales the whole UI by `content_scale` this is fine, if it scales per control it is not. UR-THEME decides.
- The soft pulse glow of the low-HP track, the siphon streams, the HP fill / tracker / echo / hatch / highlight / ticks, the Ultimate ring, the status duration rings, the dashed empty badge ring and the wipe are code by design (UI_SPEC 6.1).
- A texture for the fill of the Fighter / Story HP bars was not made (the plan keeps a `StyleBoxFlat` skew fill; the `hatch_hp` tile is the overlay).
- No baked text anywhere (the title extrusion, labels, key letters are live).
- Hero poses carry no ink outline or shadow (UR-MENU's shader / copies).
- `badge_disc` has no cover for the Act III Beacon pip frame; the pips are bare icons.
- Question for the closeout: do you want the cover hero poses for all nine characters (the select's idle heroes load SpriteFrames normally today)? `build_cover_heroes.py` bakes any `id:anim:frame` in seconds.

## 8. Drafted doc lines

**AGENTS.md** (asset-pass / UI bullet, Package "pulp UI redesign" Phase A):

> **The pulp UI kit art (2026-10-26, UR-ART).** `assets/sprites/ui/pulp/` holds the Retro Pulp kit, built by `tools/asset_src/pulp/build_all.ps1` (SVG sources through `generate_assets.gd` groups `pulp` / `pulp_icons_raw`, PIL for the image-derived rasters): nine-slice plates (panels, card frames, bubble, buttons at heights 80 / 64 / 58 in five states, bar tracks, tabs, toggle and slider pieces, slot frames, boss nameplate and PHASE tab, banners, stamp, key tag, the keyboard key-cap pill), fixed sprites (portrait roundel, Integrity dial, Continue sticker, star badge, burst star), tiles (hatches, dots), the posterized cover art, rays, the halftone, the select split, 26 ICN-B icons in three layers (`_fill` tinted, `_ink` over, `_sh` shadow under), 36 ability-slot icons cropped from the retro v2 VFX atlases (`FTT.UI.AbilityIconLibrary`, roster-driven, `null` when missing) and three baked cover poses. `assets/sprites/ui/pulp/pulp_kit.json` is the catalog (sizes, nine-slice and expand margins, mipmap flags); `PulpKitContentTests` and `AbilityIconLibraryTests` pin it. The 2026-10-18 retro v2 atlases are unchanged. Nobody has seen the kit in the engine.

**CLAUDE.md**: baseline 3552 after Phase A UR-ART (+6); add a note that the pulp kit rebuild needs the GdUnit lock for every Godot launch (`build_all.ps1`), and that `--import` rewrites sidecars with LF churn (revert unrelated ones).

**Ledger** (`PROVISIONAL-PULP-KIT-ART`): the kit's agent choices in section 6; hands-on acceptance owed.
