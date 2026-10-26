# Recon A — theme, palette, fonts and the art pipeline (2026-10-26)

Read-only subagent report at `c12149cd`, code reading only. Evidence, not authority; re-check line numbers before editing.

## 1. `resources/UI/ftt_theme.tres` (421 lines)
- **Font slot** (334-339): `default_font = FontVariation "PlaceholderDefaultFont"`, no `base_font`, empty `fallbacks` (the reserved CJK slot); `default_font_size = 18`. One font slot only.
- **Label variations** (base `Label`): `SmallLabel` 14 (393), `HeadingLabel` 22 (364), `EmphasisLabel` 26 (358), `TitleLabel` 28 (416). Plain `Label` 18, colour (0.7, 0.75, 0.85) (366-367).
- **Flat styleboxes**, radius 4 / 2 px borders, hard-wired cyan (0, 0.9, 0.9) or cyan-dim (0, 0.55, 0.6): Button `BtnNormal` / `BtnHover` / `BtnPressed` / `BtnDisabled` (9-91), `BtnFocus` (25-43: `draw_center=false`, 3 px cyan border, expand margins 3); `PanelBG` (177, Panel + PanelContainer); `PopupBG` (193, PopupMenu + PopupPanel); `LineNormal` / `LineFocus` (145-175); `SliderTrack`, `GrabberArea`, `GrabberHighlight` (115-143); `ProgressBG` / `ProgressFill` (209-226, cyan fill); tabs `TabPanel` / `TabHovered` / `TabSelected` / `TabUnselected` / `TabBarBG` (228-281); `SeparatorLine` (111); CheckBox and OptionButton reuse the Button boxes.
- **Not themed** (engine defaults): ScrollContainer, V/HScrollBar, Tree, ItemList, SpinBox, CheckBox box icons.
- **Variations:** `TemporalGlassButton` (404-415, font 20; `StyleBoxTexture`s 283-332 over `glass_button_{normal,focus,pressed,disabled,focus_ring}.png`, margins L60/T20/R34/B20, content 42/8/32/8) — ~31 uses in `MainMenu.tscn` plus StoryPause, Settings, CharacterSelect, FighterPlayOptions, HolodeckDrill, GameOver, MoveList, SystemsCard and more; `DialogueGlassPanel` (356; `DialogueGlass` at 93: navy 0.72α, cyan border 0.55α, radius 10, shadow 12) — DialogueBox, `DialogueTheme.cs`. `TemporalGlassButton` + `DialogueGlassPanel` + `ThemeTypeVariation` occur 133 times in 41 files (MainMenu.tscn 31, StoryPause.tscn 9, HolodeckDrill.tscn 6).
- **Art:** `assets/sprites/ui/glass_button_*.png` (480 × 64 from `tools/asset_src/menu/glass_button_*.svg`), `title_flourish.png`, `card_frame{,_p1,_p2}.png` (generated, unwired).

## 2. `scripts/UI/UIPalette.cs`
- Brand (30-73): `Cyan` (0, .9, .9), `CyanDim` (0, .55, .6), `Gold` (.95, .8, .3), `TemporalViolet`, `GoldBright` (1, .92, .35), `BossRed`, `Slate` (.7, .75, .85), `SlateDim`, `DamageNumberReduced`, `Navy` (.06, .06, .12), `NavyDeep`, `Shade`, `Warning` (1, .75, .3).
- Grammar (100-148): `UnboundCold` (.2, .95, 1), `UnboundColdDischarge`, `UnboundColdDim`, `ResonanceGold` (== `Gold`), `ResonanceGoldBright` (== `GoldBright`), `ResonanceAura`, `WardenPortal`.
- Theme mirrors (152-171): `PanelBackground`, `PanelBorder` (cyan-dim), `ButtonNormal/Hover/Pressed/Disabled`, `FocusOutline` (= Cyan), `TextPrimary` (= Slate), `TextAccent` (= Cyan), `TextDisabled`, `TrackBackground`.
- Type scale (182-194) 14/18/22/26/28 + variation-name constants; layout (198-202) `ButtonMinWidth` 320, `ButtonMinHeight` 44, `PanelSeparation` 14, `CornerRadius` 4, `BorderWidth` 2.
- `ApplyUiScale` (255-268, `CaptureAuthoredSizes` 287) rescales every per-type `font_size` in place (variations follow; overrides do not). `NewUnscaledTheme()` (275) is what the HUDs use; they scale their layout through `UiScaleBinder` (`root.Scale`).
- Readers (approx.): `Cyan` ~20 (StoryHUD 9, FighterHUD 5, FighterHudModel, FighterOverlayModel, RadialProgress, ResonanceGridPanel, CalibrationDrillRunner, DialogueEmotionTreatment, CreditsController); `TextAccent` ~12 titles; `GoldBright` ~15; `Gold` ~8; `BossRed` ~12; `Slate` 7; `SlateDim` 9; `UnboundCold*` ~15 (Level00, NearCapture, ChronalExtractor, EncounterSpawnTear, ExtractionBeam, Boss, EnemyController.Encounter); `FocusOutline` 0 in scripts.
- Raw overrides: ~111 `AddThemeColorOverride` / hard-coded cyan in 42 script files (CharacterSelect 13, HubWorld 14, FighterHUD 10, StoryHUD 9); 37 `.tscn` hard-code cyan / cyan-dim / UnboundCold (SystemsCard 9, MainMenu 6, HolodeckDrill 4, StoryLoading 3, …). **A palette change needs an explicit sweep.**
- Player ownership colours are separate: `GlowPalette.SlotColor` (`scripts/Combat/GlowStateModel.cs:192-200`: P1 #00f0ff, P2 #ff3366, P3 gold, P4 green), `PlayerSlotPalettes` (`scripts/Combat/PlayerSlotPalettes.cs:33-40`, glyphs ▲ / ●, BlueOrange / HighContrast palettes), `GlowPalette.SpawnInvulnerabilityColor` (#00f0ff). The HUD uses `UIPalette.Cyan` for P1 identity in places (`FighterHUD.cs:701-703`, `FighterOverlayModel.cs:111`) — re-point those to the slot colour, not the new accent.

## 3. Tests that pin theme and palette
- `tests/ContentValidation/UIThemeTests.cs`: required styleboxes (33-60); a focus stylebox on Button, CheckBox, OptionButton, LineEdit (63-74); `AuthoredColoursMatchTheUIPaletteConstants` (77-116) compares `Button/font_color` = `TextPrimary`, `Button/font_disabled_color` = `TextDisabled`, `Button/font_focus_color` = `FocusOutline`, `Label/font_color` = `TextPrimary`, `ProgressBar/font_color` = `TextPrimary`, `TabContainer/font_selected_color` = `TextAccent`, the PanelContainer **`StyleBoxFlat`** `BgColor` = `PanelBackground` and `BorderColor` = `PanelBorder`, the Button `focus` **`StyleBoxFlat`** `BorderColor` = `FocusOutline` (it casts with `as StyleBoxFlat`, so swapping those two to `StyleBoxTexture` fails the assert); variation sizes / base types = `UIPalette` constants (126-150); **`ThemeWorksWithoutAFontResource` (153-177) asserts `BaseFont == null`, `Fallbacks.Count == 0`, `DefaultFontSize == 18` — must be rewritten when fonts ship**; `ApplyTheme` must not overwrite a local theme.
- `tests/ContentValidation/ResonanceGrammarTests.cs`: scans only `ChronalExtractor.cs` and `Level00Controller.BuildFracturePresentation()` (56-59) for cold-family `new Color` literals (b ≥ .7, g ≥ .5, r ≤ .45) and violet in the Level 0 beat; asserts `ResonanceGold == Gold`, `ResonanceGoldBright == GoldBright` (165-170), cold R ≤ gold R, gold B ≤ cold B, `ChronalExtractor.DischargeColor == UnboundColdDischarge`. It does not scan UI scripts. Keep both gold pairs equal if gold is retuned; never remove or rename the grammar constants.
- `tests/Unit/UiScaleTests.cs`: scale rounding, default size and variation scaling at 1.4, `NewUnscaledTheme` keeps authored sizes.
- Per-screen "no font-size override" pins (~9 suites: ConfirmModal, StoryPause, LocalFighterPause, DialoguePresentation, CharacterSelectScene, TimelineRestartPanel, FighterMatchPresentation, MoveListScreen, UiScale).
- Palette-pinned runtime colours: `FighterHudModelTests.cs:74-75` (HP fill = `UIPalette.Cyan`), `FighterMatchPresentationTests.cs:34` (`overlay.BannerColor == UIPalette.Cyan`), `DialogueEmotionTreatmentTests.cs:67`. Prefer new constants and explicit re-pointing over changing `Cyan`'s value.
- `SceneVisibleTextTests` (visible `.tscn` text must be a key or glyph-only), `OutlineGlowShaderTests` (thickness 0-10).

## 4. Fonts
- `assets/fonts/` holds only `.gitkeep`.
- Recipe: TTF into `assets/fonts/`; import with antialiasing on, hinting none/light, subpixel auto, MSDF off (UI scales by re-rasterising through theme sizes), mipmaps off; set `base_font` on `PlaceholderDefaultFont` (`ftt_theme.tres:334`).
- `ApplyUiScale` changes only `font_size` entries, so fonts scale cleanly — except the HUDs, scaled by `Control.Scale` (`UiScaleBinder.cs:39`), which scales rasterised glyphs: give HUD text a real native size for 1.5-2x rather than relying on scale.
- Two families: the theme has one default font; give `TitleLabel` / `HeadingLabel` / `EmphasisLabel` (and a speaker-name variation) `Label/fonts/font` = Cinzel, `Button/fonts/font` as chosen; `FontVariation` for weights.
- Never fill the reserved CJK `fallbacks` slot with Cinzel/Outfit. Add OFL rows to the Third-Party Licenses screen (`scripts/UI/ThirdPartyLicenses.cs:24`; `MainMenuExtrasTests` checks the row count against `Notices.Length`).
- **Glyph coverage:** Cinzel / Outfit lack ◑ ☠ ⚡ ✸ ⟊ ✧ ✕ ⧗ ❄ ✦ ▲ ●, which the HUDs render as Label text today (engine fallback). Once a real base font is set, check they still render — icon textures fix it.

## 5. Art pipeline (SVG → PNG)
- Run: `"<console exe>" --headless --path "<repo>" --script res://tools/generate_assets.gd -- group=<name>` (`tools/generate_assets.gd:1-27`), reading `res://tools/asset_src/jobs.json` (40 jobs); sources under `tools/asset_src/` (behind `.gdignore`).
- Job types (71-85): `svg` (90-108: `Image.load_svg_from_string(text, supersample)` → Lanczos to `width` × `height`), `svg_dir` (113), `svg_atlas` (164: `cell_dir`/`prefix`/`rows`/`columns`/`cell_width`/`cell_height`), `atmosphere` (198: seeded gradient, noise, particles, vignette — usable for a halftone), `blend` (331).
- ThorVG-safe SVG: paths, linear/radial gradients, opacity, transforms only — no filters, masks, `<text>` or patterns. Halftone = many small circles (or an `atmosphere` job); a hard drop shadow = a second offset path.
- UI PNG import: `compress/mode=0`, `mipmaps/generate=false`, `fix_alpha_border=true` (`glass_button_normal.png.import:18-34`). Rotated/tilted controls degrade 9-slice edges unless mipmaps/linear filtering are set (the project's default 2D filter is unverified).
- Example 9-slice job (group "pulp"; author the SVG with a builder script like `tools/asset_src/menu/build_menu_svgs.py`):
  ```json
  { "type": "svg", "group": "pulp", "src": "res://tools/asset_src/pulp/ink_panel.svg",
    "out": "res://assets/sprites/ui/pulp/ink_panel.png", "width": 384, "height": 256, "supersample": 3 }
  ```
  Draw order: the hard offset shadow (the same path translated +10, +10, black), the body, then a 4 px black outline; reserve 10 px right/bottom for the shadow; in the theme a `StyleBoxTexture` with `texture_margin_*` ≈ 40, `draw_center` on, content margins that allow for the shadow. Skewed shapes need asymmetric left/right margins that contain the whole slant.
- Icons: none exist; use `svg_dir` (one PNG per icon) or `svg_atlas`.
- After generating: run `--import` before `dotnet test`; `.png.import` sidecars churn (CLAUDE.md).

## 6. Motion and transitions
- `GameManager.LoadScene` instances a themed `LoadingScreen` (CanvasLayer, layer 100, `ProcessMode.Always`, `scripts/UI/LoadingScreen.cs:38,140-141`) parented to the autoload, 2 s minimum. Scenes `LoadingScreen.tscn`, `StoryLoading.tscn`, `FighterLoading.tscn` are the hook for a diagonal wipe; a wipe layer must also run `ProcessMode.Always` and live under `GameManager` (or a CanvasLayer autoload).
- No shared panel-in / screen-in helper; tweens exist in NotYetFlash, ResonanceGridPanel, FighterHUD, DialogueManager, CalibrationDrillPicker.
- `FTT.Core.ComfortSettings` (`scripts/Core/ComfortSettings.cs`): `ReducedTemporalEffects` (40), `Changed` (47), `DistortionAllowed`, `GhostTrailsAllowed`, `FullScreenFlashAllowed` (104), `ScreenTintPulseAllowed`, `ResolvePulseSpeed` (118). Gate the wipe and overshoot on it (fade fallback). Never read from `scripts/FighterSim/`.
- `FocusChainBuilder` only authors neighbours and initial focus; a focus tilt/scale needs `focus_entered`/`focus_exited` (and `mouse_entered`) hooks per Button. A Container resets a child's scale/rotation when it sorts, so tilt an inner visual child, not the Button in a container; set pivots on resize.
- Shaders: only `assets/shaders/outline_glow.gdshader`; `gl_compatibility` limits (no `instance uniform`, `TEXTURE` not as a `sampler2D` argument); screen-texture reads work but force a back-buffer copy (none in the repo yet). Pause-time tweens need `ProcessMode.Always` / a tween pause mode that runs while paused.

## 7. Glyph icons today
`HudAbilityIndicatorModel.StatusGlyph` (`scripts/UI/HudAbilityIndicatorModel.cs:347`: ◑ TimeDilation, ☠ Venom, ⚡ StaticCharge, ✸ RadiantBurn, ⟊ Root), `LockGlyph` (367: ✧ Dormant, ✕ Suppressed), `DefySealModel.Glyph` (`DefySealModel.cs:67`), `FighterHudModel.CooldownGlyphKey` (161; Fighter cooldown icon text from a key via `Tr`, `FighterHUD.cs:655`), `PlayerSlotPalettes` ▲ / ● (33, 35). Call sites setting `.Text`: `StoryHUD.cs:360, 556, 849`; `FighterHUD.cs:317, 365, 655, 748`. The contract requires a distinct shape plus a localized name (`StoryHUD.cs:358-360`). `RadialProgress`, `AnchorPip`, `BeaconAnchorIcon` are code-drawn.

## 8. Risks (ranked)
1. Cyan is baked in three levels (theme ~40 entries, `UIPalette`, ~111 script overrides + 37 scenes): theme-only changes leave a half-gold UI. Plan a sweep and a source-scan guard that bans cyan in UI scripts outside P1 ownership, the Severed effects and the grammar constants.
2. Gold as UI chrome vs the grammar (gold = history's resonance): keep P1 cyan from `GlowPalette.SlotColor`, keep the Severed cold, keep the grammar constants untouched.
3. `UIThemeTests` coupling: theme and `UIPalette` change together; decide the `StyleBoxTexture` strategy for `PanelContainer/panel` and `Button/focus` up front (update the test, or keep flat defaults and put textured pulp skins on variations).
4. The font test must be rewritten; Cinzel's wide caps can overflow fixed-width buttons (`ButtonMinWidth` 320) — test at 140 %.
5. HUD growth meets the node-path tests and `UiScaleBinder` on top.
6. Per-screen font-size pins: new sizes through variations only.
7. Localization: new text needs keys, `--import`, the key tests; check Cinzel/Outfit cover every glyph in the translation (accents, …, —).
8. Glyph icons vs the contract (see 4 and 7).
9. Reduced Temporal Effects for the wipe, overshoot and thunks; pause tweens must run while paused.
10. Texture memory and import churn.
