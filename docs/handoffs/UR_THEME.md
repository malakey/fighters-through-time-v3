# UR-THEME handoff (pulp UI redesign, Phase A)

Branch `ur/theme`, cut from `main` at `ae830298`. **Both steps are done.** Step 1: palette, fonts, generated theme, motion, capture tool, cyan inventory. Step 2: `main` (UR-ART's kit, `d1e9e2a0`) merged and wired into the theme through the generator (section 2.7).

## 1. Summary

- **Palette.** `scripts/UI/UIPalette.cs` carries the PAL-B tokens (`PlateFill`, `Ink`, `Ink2`, `Ink3`, `Accent`, `Accent2`, `Res`, `Res2`, `Meter`, `Meter2`, `Hp`, `HpLag`, `Echo`, `Shield`, `Danger`, `Boss`, `Paper`, `P1`, `P2` and the UI_SPEC 1.2 literals). There is **no `PulpRed`** (ruling Q1). `P1` / `P2` are properties that read `GlowPalette.SlotColor(0/1)`. The theme mirrors (`PanelBackground`, `PanelBorder`, `Button*`, `FocusOutline`, `TextPrimary`, `TextAccent`, `TextDisabled`, `TrackBackground`) now hold pulp values. `Cyan`, `CyanDim`, `Gold`, `GoldBright` and every grammar constant (`UnboundCold*`, `ResonanceGold*`, `ResonanceAura`, `WardenPortal`) are **unchanged**.
- **Fonts.** Cinzel and Outfit (variable TTFs from `google/fonts` `ofl/`, SIL OFL 1.1) in `assets/fonts/cinzel/` and `assets/fonts/outfit/` with their `OFL.txt`; imported with antialiasing on, light hinting, no MSDF, no mipmaps. The default theme font is Outfit 400 in the existing default `FontVariation` slot (`base_font` set, `fallbacks` still empty: the reserved CJK slot). Weights are pinned per variation through `variation_opentype` (`wght`). Two rows added to the Third-Party Licenses screen (`license_name_ofl`).
- **Theme.** `resources/UI/ftt_theme.tres` is **generated** by `tools/ui_theme/build_ftt_theme.py` (also writes `scripts/UI/UIPalette.Roles.cs`); the button, panel, tab, slider, bar-track and plate styles are the UR-ART kit's `StyleBoxTexture`s (margins and expand margins read from `assets/sprites/ui/pulp/pulp_kit.json`); the inputs, scroll bars, popups, tooltips, separators, the default ProgressBar and every ProgressBar **fill** stay `StyleBoxFlat` in the pulp palette. 48 Label roles, 3 button variations, 17 panel variations, 2 bar variations. Existing names `TemporalGlassButton` and `DialogueGlassPanel` kept (re-skinned); `SmallLabel` / `HeadingLabel` / `EmphasisLabel` / `TitleLabel` re-pointed.
- **Motion** (`scripts/UI/Motion/`): `PulpMotionRules` (pure), `PulpMotion` (panel pop, thunk, tracker drop, hit flash), `FocusPose` (button pose), `ScreenWipe` (the diagonal wipe, hooked into `GameManager.LoadScene`).
- **Capture tool** (`scenes/diagnostics/UiCaptureRunner.tscn`, `scripts/Diagnostics/UiCapture/`, README in `tools/ui_theme/README.md`): five shipped targets, captured to `D:\Projects\ftt-scratch\ur-captures\theme\`.
- **Cyan inventory:** `docs/recon-2026-10-26/D_cyan_inventory.md` (51 files, 136 sites, each with its Phase B owner and replacement).

## 2. The kit catalog for Phase B

### 2.1 Palette tokens (`FTT.UI.UIPalette`)

| Token | Value | Use |
|---|---|---|
| `PlateFill` | #090C1E @0.80 | dark ink plate fill |
| `PlateRaised` | #1E1C34 @0.78 | settings tab fill |
| `Backdrop` | #05071A | screen backdrop under art |
| `Ink` / `Ink2` / `Ink3` | #F3F0E6 / #C0BAA6 / #8A8374 | text 1 / captions / secondary (never the only cue) |
| `Accent` / `Accent2` | #F3C95B / #FFE39A | chrome accent (focus fill, tags, banners) / pressed, rewind and Time Freeze tint |
| `Res` / `Res2` | #F3C95B / #FFF0B0 | dust, ring start / Defy ready, objective, full meter |
| `Meter` / `Meter2` | #F3C95B / #FFB347 | Ultimate arc start / end and the Fighter meter fill |
| `Hp` / `HpLag` / `Echo` | #8CF0B8 / #FF5A5A / #FFF7DA | HP fill / damage tracker / Rally echo band |
| `Shield` | #DFE8FF | block pip |
| `Danger` / `Boss` | #FF5A5A | low HP, warnings / boss red |
| `Paper` | #F3F0E6 | panel border, selected button, bubble |
| `P1` / `P2` | read from `GlowPalette.SlotColor` | ownership (#00F0FF / #FF3366) |
| `InkBlack` `LabelDark` `PaperInk` `PaperInk2` `PaperInk3` `Track` `SlotFrame` `PromptCream` `StatusDamage` `StatusControl` `DisabledFill` | #000, #0C0A10, #14101A, #3A3144, #6B6150, #3A3144, #2A2434, #FFF3D8, #FF7A5C, #8FC7FF, #2C2733 | UI_SPEC 1.2 literals |
| `DisabledLabel` | #A0987F | disabled text (the spec's #7A705A is 2.97:1; this clears 4.5:1) |
| `FocusFill`, `TextOnAccent` | `Accent`, `LabelDark` | a focused / hovered / pressed button's fill and its label colour |
| `FocusOutline` | `Accent` | ring-style focus (line edit, check box, tab) |

`UIPalette.MinLabelFontSize` (18) is the floor `UIThemeTests` enforces on every Label role. Type-scale constants moved to `SmallFontSize` 18, `BodyFontSize` 20, `HeadingFontSize` 26, `EmphasisFontSize` 30, `TitleFontSize` 40 (the theme default font size is 20).

### 2.2 Label variations (set `ThemeTypeVariation`; never a per-node size or font override)

Names are `UIPalette.LabelRoles.<Name>` constants; `UIPalette.LabelRoleTable` lists size, face and weight. Tracking is baked into the role's `FontVariation` (`spacing_glyph`); `tnum` roles carry the tabular-figures feature. Use `Label.Uppercase = true` where the spec says UPPER. Outlines are black `outline_size` (= CSS stroke / 2); shadows are the single offset copy `StyleBox` text can do.

| Variation | Face / wght | px | Tracking | Colour | Outline | Spec |
|---|---|---:|---:|---|---:|---|
| `SmallLabel` | Outfit 500 | 18 | 0.00 em (0 px) | #C0BAA6 | - | micro 18 px |
| `HeadingLabel` | Cinzel 700 | 26 | 0.06 em (2 px) | #F3F0E6 | - | heading, Cinzel 700 |
| `EmphasisLabel` | Outfit 600 | 30 | 0.00 em (0 px) | #F3F0E6 | - | emphasis, Outfit 600 |
| `TitleLabel` | Cinzel 700 | 40 | 0.08 em (3 px) | #F3C95B | - | screen title, Cinzel 700 |
| `CoverTitle` | Cinzel 500 | 170 | 0.01 em (2 px) | #F3C95B | 7 | T01; shadow (2, 2) |
| `CoverBanner` | Outfit 700 | 70 | 0.10 em (7 px) | #F3F0E6 | 3 | T02 |
| `BannerHeading` | Cinzel 500 | 58 | 0.03 em (2 px) | #0C0A10 | - | T03 |
| `PauseTitle` | Cinzel 900 | 78 | 0.14 em (11 px) | #FFFFFF | - | T04; shadow (0, 4) |
| `LevelTitle` | Cinzel 500 | 26 | 0.12 em (3 px) | #F3F0E6 | - | T05; shadow (0, 2) |
| `ObjectiveLine` | Outfit 400 | 22 | 0.00 em (0 px) | #FFF0B0 | - | T06; shadow (0, 2) |
| `PanelTag` | Outfit 600 | 21 | 0.16 em (3 px) | #0C0A10 | - | T07 |
| `HeroName` | Cinzel 700 | 22 | 0.13 em (3 px) | #F3F0E6 | - | T08 |
| `HeroNameScene` | Cinzel 700 | 28 | 0.13 em (4 px) | #F3F0E6 | 3 | T09 |
| `CaptionLabel` | Outfit 500 | 18 | 0.16 em (3 px) | #C0BAA6 | - | T14, T15, T48 |
| `SceneCaption` | Outfit 500 | 18 | 0.16 em (3 px) | #F3F0E6 | 3 | T24, T29, scene captions |
| `StatusLine` | Outfit 400 | 18 | 0.06 em (1 px) | #C0BAA6 | - | T16 |
| `PromptCaption` | Outfit 500 | 18 | 0.16 em (3 px) | #FFF3D8 | 2 | T13 |
| `PromptKey` | Outfit 700 | 20 | 0.00 em (0 px) | #0C0A10 | - | T12 |
| `HintLine` | Outfit 500 | 22 | 0.00 em (0 px) | #FFF3D8 | 3 | T44 |
| `HudNumeral` | Outfit 400 tnum | 30 | 0.02 em (1 px) | #F3F0E6 | 3 | T17, T26, T28 numerals |
| `HudNumeralSmall` | Outfit 400 tnum | 22 | 0.02 em (0 px) | #F3F0E6 | 3 | T17 at bar height 30 |
| `SlotKeyTag` | Outfit 700 | 18 | 0.06 em (1 px) | #0C0A10 | - | T18 (15 raised to the 18 px floor) |
| `SlotNumber` | Outfit 400 tnum | 20 | 0.00 em (0 px) | #FFFFFF | 2 | T19; shadow (0, 2) |
| `StatusName` | Outfit 500 | 18 | 0.16 em (3 px) | #F3F0E6 | - | T20 |
| `StatusTime` | Outfit 600 | 21 | 0.00 em (0 px) | #C0BAA6 | - | T21 |
| `DefyLabel` | Outfit 500 | 18 | 0.16 em (3 px) | #C0BAA6 | - | T22 (16.2 raised to 18) |
| `RewindCount` | Outfit 600 | 32 | 0.00 em (0 px) | #F3F0E6 | 3 | T23 |
| `StateLabel` | Outfit 600 | 20 | 0.10 em (2 px) | #F3F0E6 | 3 | T25 |
| `DustFloat` | Outfit 400 | 26 | 0.02 em (1 px) | #FFF0B0 | 2 | T27 |
| `BossName` | Cinzel 900 | 40 | 0.08 em (3 px) | #F3F0E6 | 3 | T30; shadow (0, 3) |
| `BossPhaseTag` | Outfit 700 | 18 | 0.16 em (3 px) | #0C0A10 | - | T31 |
| `BossHpValue` | Outfit 400 tnum | 24 | 0.00 em (0 px) | #FFFFFF | 3 | T32; shadow (0, 1) |
| `NoteLabel` | Outfit 500 | 18 | 0.16 em (3 px) | #C0BAA6 | - | T33; shadow (0, 2) |
| `TimerDigits` | Outfit 600 tnum | 60 | 0.04 em (2 px) | #F3F0E6 | - | T34 |
| `CardName` | Outfit 700 | 25 | 0.06 em (2 px) | #F3F0E6 | - | T35 |
| `CardDates` | Outfit 500 | 18 | 0.12 em (2 px) | #F3C95B | - | T36 (17 raised to 18) |
| `CardStamp` | Outfit 400 | 36 | 0.06 em (2 px) | #F3F0E6 | - | T37 |
| `CardQuestion` | Outfit 400 | 110 | 0.00 em (0 px) | #F3C95B | 3 | T38 |
| `BubbleText` | Outfit 400 | 46 | 0.04 em (2 px) | #0C0A10 | - | T39 |
| `SideCardName` | Cinzel 900 | 31 | 0.05 em (2 px) | #0C0A10 | - | T40 |
| `StatLabel` | Outfit 500 | 18 | 0.16 em (3 px) | #3A3144 | - | T41 |
| `StatValue` | Outfit 600 | 24 | 0.00 em (0 px) | #14101A | - | T42 |
| `StarBadgeText` | Outfit 400 | 44 | 0.00 em (0 px) | #0C0A10 | - | T43 |
| `StickerLine1` | Outfit 400 | 46 | 0.04 em (2 px) | #0C0A10 | - | T45 |
| `StickerLine2` | Outfit 700 | 30 | 0.08 em (2 px) | #7A1F19 | - | T46 |
| `StickerLine3` | Outfit 600 | 20 | 0.06 em (1 px) | #0C0A10 | - | T47 |
| `DialogueBody` | Outfit 500 | 33 | 0.00 em (0 px) | #F3F0E6 | - | T49 |
| `SpeakerName` | Cinzel 900 | 30 | 0.10 em (3 px) | #F3C95B | - | T50 |

The four legacy names keep their meaning: `SmallLabel` (micro, caption size), `HeadingLabel` (Cinzel 700), `EmphasisLabel`, `TitleLabel` (Cinzel 700, accent). The plain `Label` is Outfit 400, 20 px, `Ink`.

### 2.3 Button, panel and bar variations (`UIPalette.Variations.<Name>`)

| Variation | Base | Role | Notes |
|---|---|---|---|
| (default) `Button`, `OptionButton` | Button | utility buttons | Outfit 700, 20 px; the 58 px plate squeezed to a ~42 px control (the slanted end slices stretch a little); normal / hover = focus / pressed / disabled per UI_SPEC 3.2, the disabled stripes tile |
| `PulpButton` | Button | T10 menu / pause / footer banner button | Outfit 700, 34 px, the 80 px plate (`btn_*_h80`), left margin 64 (room for the marker), content margins make the control 80 high |
| `PulpButtonCompact` | Button | T11 compact | 27 px, the 58 px plate, margins 54 / 24 |
| `TemporalGlassButton` | Button | legacy name, same look | 26 px, the 64 px plate; the old glass textures are no longer referenced by the theme (MainMenu's per-button overrides still use them) |
| `InkPanel` (also default `Panel`, `PanelContainer`) | PanelContainer | plate 1 dark ink panel | paper border 3, hard shadow 9 |
| `PaperPanel` | PanelContainer | plate 2 | paper fill, black border; text colours `PaperInk*` |
| `HudPlatePanel` | PanelContainer | HUD plate | |
| `DialogueGlassPanel` | PanelContainer | legacy name: the dialogue box plate | translucent; `DialogueTheme` constants re-pointed to match |
| `BannerPanel` | PanelContainer | plate 24 gold banner | skew 0.1405, border 7 |
| `PanelTagPlate` | PanelContainer | plate 3 panel tag | skew 0.1763; use label role `PanelTag` |
| `BossNameplate`, `BossPhaseTab` | PanelContainer | plates 11, 12 | `BossName`, `BossPhaseTag` roles |
| `SlotPlate`, `SlotPlateReady`, `KeyTagPlate` | PanelContainer | plates 13, 14, 16 | square slot (radius 0 placeholder; the spec's 6 px radius waits for step 2) |
| `CardPlate`, `CardPlateP1`, `CardPlateP2` | PanelContainer | plates 20-22 | P1 / P2 borders are the slot colours as literals pinned by nothing but the generator: **re-read `UIPalette.P1` / `P2` if the slot palette ever changes** |
| `BubblePlate`, `StampPlate` | PanelContainer | plates 27, 26 | |
| `ScrimPanel` | PanelContainer | translucent scrim | |
| `PulpBar` | ProgressBar | plate 9: skewed HP / meter track | **fill is a `StyleBoxFlat`**, so `FighterHUD.TintFill` keeps working |
| `PulpBossBar` | ProgressBar | plate 10 boss bar | |

Also themed: LineEdit, HSlider / VSlider, VScrollBar / HScrollBar, SpinBox steppers, ProgressBar, TabContainer, PopupMenu / PopupPanel, tooltips, separators, CheckBox (placeholder icons in `assets/ui/theme_placeholder/`). **Not themed:** ItemList, Tree, ScrollContainer panel, CheckButton, MenuButton popups beyond PopupMenu.

**Textured plates and layout.** Every plate draws outside its control rect by the kit's expand margins (hard shadows), so give neighbours at least ~12 px right / bottom clearance, as the spec's overflow rulings say. Plates flagged `mip` in the kit have mipmaps on; a tilted or scaled control (the focus pose, banners, tags, cards) looks cleaner with `texture_filter = LINEAR_WITH_MIPMAPS` on that control. The theme does not scale plates with the UI scale (a `StyleBoxTexture` cannot): at 140 % the 1x art is drawn 1:1 and only the middle slices stretch, so the HUD owners keep scaling through `UiScaleBinder` as before. Kit plates the theme does not use yet but Phase B can wire per screen: `btn_selected_h*` (paper, the toggled / selected look), `banner_cover`, `bar_track_h14/h16/h44`, `slot_ult*`, `toggle_*`, `portrait_roundel`, `star_badge_*`, `sticker_continue`, `dial_*` and the icons (see `UR_ART.md`).

**A button's pressed look is hidden while it has focus.** Godot draws the state box (`pressed`) and then the `focus` box on top; focus is the opaque accent plate (the spec's focus look), so a focused, pressed button reads as focus. The spec's pressed travel (+7,+7) is done by `FocusPose`, not the stylebox. A toggled-on button therefore shows the `pressed` look only while not focused.

### 2.4 Motion API (`FTT.UI.Motion`)

All of it reads `ComfortSettings.ReducedTemporalEffects` when it runs and uses `Tween.TweenPauseMode.Process`, so a paused tree does not strand it.

| API | Behaviour |
|---|---|
| `FocusPose.Attach(Control host, Control visual = null)` | Hooks `FocusEntered` / `FocusExited` / `MouseEntered` / `MouseExited` and the button's down / up. Pose: +16 px x, -1.2 deg, x1.04 about the centre, 0.14 s `TRANS_BACK` in, 0.10 s out; pressed adds +7,+7 with no tween; Z raised. Reduced: instant, no overshoot. Re-applies itself after a `Container` sort (a container resets child position / rotation / scale). Pass `visual` (a child that mirrors the plate) when the host must not be touched. `Apply(t)`, `PoseActive`, `Target` are public. Writes identity exactly on release. Disabled buttons do not hover-pose. |
| `PulpMotion.PopIn(Control)` / `PopOut(Control)` | scale 0.96 to 1 with alpha 0 to 1 over 0.1 s; Reduced: 0.15 s alpha only. Returns the tween. |
| `PulpMotion.Thunk(Node host, int from, int to, Action<int> apply)` | three equal steps on consecutive frames, the last exactly `to`; Reduced or no change: `apply(to)` at once and returns null. |
| `PulpMotion.TrackerDrop(Node host, float from, float to, Action<float> apply)` | hold 0.5 s, then 6 steps over 0.6 s; Reduced: linear 0.2 s. |
| `PulpMotion.HitFlash(CanvasItem plate, HitFlashLimiter limiter, double nowSeconds)` | `SelfModulate` to black for one frame, **once per second per bar** through the per-bar `HitFlashLimiter`; Reduced: never. Returns whether it flashed. |
| `PulpMotionRules` | the constants and pure helpers (`ThunkValues`, `Stepped`, `WipeOffsetCover`, `WipeOffsetReveal`). |
| `ScreenWipe` (`CanvasLayer` 110, `ProcessMode.Always`, node `GameManager/ScreenWipe`) | `EnsureFor(host)`, `BeginCover()`, `BeginReveal()`, `PlayThrough()`, `Release()`, `IsCovered`, `IsActive`, `CoverSeconds`, `Covered` / `Revealed` events. Slab: accent fill, slanted trailing edge at 88 %, 6 px ink edge, **8 stepped positions in 0.26 s per direction**. Reduced: an ink fade of 0.2 s each way. |

**Hook in `GameManager`:** `LoadScene` plays the wipe through (cover then reveal) over the loading screen; the cover starts `CoverSeconds` before the two-second minimum display ends, the swap waits for `IsCovered` (or the minimum + 1 s), and the reveal starts after `ChangeSceneToPacked`. The minimum display time is not lengthened (a headless smoke run swapped at 2.0 s). `AbortLoad` releases the wipe.

### 2.5 The capture tool

See `tools/ui_theme/README.md`. Usage: console exe **without `--headless`**, under the GdUnit lock, `res://scenes/diagnostics/UiCaptureRunner.tscn -- --capture=<names|all> --out=<dir>` (`--list` prints targets). Add a target by writing `[UiCaptureTarget("name")] public static Node X(UiCaptureContext c)` in your own `partial class UiCaptureTargets` file (`UiCaptureTargets.<Workstream>.cs`); `context.AfterReady` runs setup after the node settled. Shipped targets: `theme_labels_small`, `theme_labels_large`, `theme_controls`, `theme_palette`, `main_menu_root`.

### 2.6 The generator

`py tools/ui_theme/build_ftt_theme.py` regenerates `ftt_theme.tres` and `UIPalette.Roles.cs`. A Phase B workstream that needs a new Label role or plate edits the script's `ROLES` / `build()` tables and regenerates (small, mergeable additions; coordinate through the orchestrator if two workstreams add at once, because the `.tres` is one generated file: **regenerate after merging, never merge two generated `.tres` by hand**).

## 3. Tests (arithmetic from 3546)

| Suite | Change | Cases |
|---|---|---:|
| `UIThemeTests` | `ThemeWorksWithoutAFontResource` rewritten as `TheShippedFontsAreTheDefaultAndTheFallbackSlotStaysEmpty` (-1 +1); the colour and label-variation cases changed in place (the button focus / hover / pressed fills, the line-edit ring, `TextOnAccent`, the tab's selected colour, every Label role at its size and the 18 px floor); new `EveryLabelRolePinsItsFaceAndWeight`, `EveryPulpVariationExistsOnItsBaseType`, `OwnershipColoursReadTheCombatSlotPaletteAndChromeIsGold`, `TheThemesTextPairsClearTheContrastFloor`, `BothShippedFontsCoverEveryCharacterTheEnglishStringsUse` (the glyph-coverage guard: every character of `localization/en.csv` outside `#` comment lines is in both fonts; the allow-list is empty) | 6 to 11 (+5) |
| `PulpMotionRulesTests` (new, pure C#) | thunk values, stepping and wipe offsets, the timings, the hit-flash limiter | +4 |
| `PulpMotionTests` (new, runtime) | focus pose (Reduced instant; tweened; survives a container sort), panel pop (standard and Reduced), thunk, hit flash, wipe (covers and reveals, Reduced fade, runs under a paused tree, release never leaves the screen covered or blocking) | +10 |
| `UiCaptureRunnerTests` (new) | argument parsing, registry discovery, every `theme_*` target builds and frees | +3 |

**Step 1: 3546 + 5 + 4 + 10 + 3 = 3568** (UR-ART's +6 arrives with the merge: main is 3552, so the merged total is **3574**, observed: `Failed: 0, Passed: 3574, Total: 3574`, 2 m 13 s, lock held).

**Step 2 changed in place (no count change):** `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants` no longer casts `PanelContainer/panel` and `Button/focus` to `StyleBoxFlat`; it casts to `StyleBoxTexture` and compares the **centre pixel** of the plate texture with the palette token (`PanelBackground`, `FocusFill`, `ButtonHover`, `ButtonPressed`, `ButtonNormal`, tolerance 0.02), asserts hover and focus share one texture and that the disabled plate tiles. `DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation` (UR-PAUSE's file, one case) casts the glass panel to `StyleBoxTexture` and checks the translucent centre pixel against `DialogueTheme.GlassBackground`; its border, shadow, radius and "heavier than the utility panel" assertions are gone because the art bakes them. 

## 4. Breaks found and left for Phase B (and one deliberate edit outside my files)

- **`scripts/UI/DialogueTheme.cs`** (UR-PAUSE's file): I changed only its six glass-panel constants (`GlassBackground` = `PlateFill`, `GlassBorder` = `Paper`, `GlassShadow` = `InkBlack`, radius 4, border 3, shadow size 1) so `DialoguePresentationTests` keeps pinning the theme. UR-PAUSE should expect them changed.
- **`MainMenu.tscn` and every screen with per-button `StyleBoxTexture` overrides** still draw the glass textures, but their labels now take the theme's `font_focus_color` (`LabelDark`) and hover colour, so the **focused row is dark text on the dark glass texture** (`main_menu_root.png`: "Quick Play" is unreadable while focused). Phase B (UR-MENU) strips those overrides; until then the transitional main menu has that defect.
- **Hard-coded cyan** in the 51 files of the inventory still draws cyan; `TextAccent` readers turned gold automatically.
- **Layouts at the new type scale** were not walked screen by screen: default Label 18 to 20, `TitleLabel` 28 to 40, `HeadingLabel` 22 to 26, `SmallLabel` 14 to 18, Button 18 to 20 Outfit 700 with tracking. Fixed-width buttons (`ButtonMinWidth` 320) and fixed-height rows may now overflow; Phase B tests at 140 % UI scale.
- **Glyph icons**: the HUDs render status glyphs (◑ ☠ ⚡ ✸ ⟊ ✧ ✕ ▲ ●) as `Label` text. These are code points in `HudAbilityIndicatorModel`, `DefySealModel` and `PlayerSlotPalettes`, not in `en.csv`, so the glyph guard does not see them; Cinzel and Outfit lack them and `allow_system_fallback` is on, so they fall back to the engine face until UR-ART's icon textures replace them.
- **`AGENTS.md` / `CLAUDE.md` rows that mention `UIPalette` colour values** (the theme-mirror constants) are stale; the drafted replacements are in section 7.
- **The headless `LoadScene` "Parse Error" predates this branch.** A scratch scene that calls `GameManager.LoadScene` on `MainMenu.tscn` (error at `MainMenu.tscn:23`) or `HubWorld.tscn` (error at `HubWorld.tscn:11`) logs `Parse Error` in the threaded load, reproduced identically in `D:\Projects\ftt-wt\fu-integ` at `68eb32fc` (pre-UR), under the lock. It is a headless threaded-load artefact, not a regression from UR-THEME or UR-ART. Cause not investigated.

## 5. Provisional choices (ledger `PROVISIONAL-UI-PULP-KIT`)

Type scale (Small 18, Body 20, Heading 26, Emphasis 30, Title 40); the role to variation mapping and names; sizes raised to the 18 px floor (slot key tag 15 to 18, Defy label 16.2 to 18, card dates 17 to 18, Time Freeze sub-label 17.1 to 18); button label default 20 px; flat placeholders for plates that need rings (the spec's paper ring + black ring become one border); the wipe's per-direction 0.26 s and its Reduced fade 0.2 s; the focus pose peak by `TRANS_BACK`; `HitFlash` through `SelfModulate`; check-box placeholder icons; `DisabledLabel` #A0987F; focus = filled accent plate for buttons and a ring for inputs; `P1` / `P2` card borders baked as literals.

## 6. Not delivered (step 1)

The spec's `InkPlate` for the ring stacks the kit already bakes; `InkPlate` as a custom `StyleBox` (the spec's route A, deliberately not built: no scripted `StyleBox` resources, crash class); the halftone corner shader; the extruded cover title (`ExtrudedTitle`); the backdrop blur; icons; ItemList / Tree / CheckButton theming; a Noto fallback face; the wipe's destination title card; a theme specimen of hover (identical to focus by spec).

## 7. Drafted doc lines

- **AGENTS.md (UI section):** "The shared theme `resources/UI/ftt_theme.tres` is generated by `tools/ui_theme/build_ftt_theme.py` (with `scripts/UI/UIPalette.Roles.cs`) and carries the pulp kit (2026-10-26): PAL-B gold chrome (`UIPalette.Accent` #F3C95B; cyan survives only as the Player-1 ownership colour and the Severed's cold light), Cinzel and Outfit (SIL OFL, `assets/fonts/`, the default-font fallback slot still reserved for a CJK face), 48 Label roles (UI_SPEC T01-T50) as theme variations at 18 px or more, plate variations for panels, bars and banner buttons, and the MOT-B motion helpers in `scripts/UI/Motion/` (button focus pose, panel pop, thunk, hit flash limited to once per second per bar, the `ScreenWipe` that `GameManager.LoadScene` plays), every effect falling back to a stable fade under Reduced Temporal Effects. `UiCaptureRunner.tscn` photographs screens at 1920 x 1080 for review."
- **CLAUDE.md (commands):** the capture command from section 2.5 under "Verified commands", with the window and lock caveat; the baseline 3568 once the branch merges.
- **Ledger rows** (section "Confirmed open deviations: pulp UI redesign, 2026-10-26"): `USER-UI-PULP-REDESIGN` (the user's selection PAL-B / TYPE-A / PNL-B / BTN-B / GAUGE-B / ICN-B / HUD-S1 / HUD-F2 / MENU-B / CARD-B / PAUSE-A / BOSS-B / MOT-B and the two rulings); `PROVISIONAL-UI-PULP-KIT` (section 5); `PROVISIONAL-UI-PULP-FOCUS-HIDES-PRESSED` (section 2.3); `VERIFY-UI-PULP-HUMAN-REVIEW` (nobody has looked at it in play, on a pad, at 140 %); `VERIFY-UI-PULP-GLYPH-ICONS` (section 4).
- **Font licences:** Cinzel, Copyright 2020 The Cinzel Project Authors (https://github.com/NDISCOVER/Cinzel); Outfit, Copyright 2021 The Outfit Project Authors (https://github.com/Outfitio/Outfit-Fonts); both SIL Open Font License 1.1, texts in `assets/fonts/*/OFL.txt`, `THIRD_PARTY_NOTICES.md` and the Third-Party Licenses screen.

## 8. Step 2 (done): how the kit is wired

`tools/ui_theme/build_ftt_theme.py` loads `pulp_kit.json` and builds each textured style with `tbox(prefix, kit_stem, content_margins)`: the nine-slice and expand margins come from the catalog, `axis_stretch: tile` sets `axis_stretch_horizontal = TILE` (the disabled stripes), and the content margins are the theme's own (label padding, the 80 / 64 / 58 px heights). Buttons use `btn_<state>_h<height>` (hover = focus plate); panels `panel_dark` / `panel_paper`; the tags and plates their own stems; sliders `slider_track` with `slider_fill` as the grabber area and `slider_grabber` as the grabber icon; tabs `tab_selected` / `tab_normal`; `PulpBar` `bar_track_h30` and `PulpBossBar` `bar_track_boss` (content margins 3 and 5, so the flat fill sits inside the border). The ProgressBar **fill** stays a `StyleBoxFlat` (skewed to match). To retarget a style, change its stem in the script and regenerate; to add a plate, add the kit PNG to the catalog (UR-ART's `write_catalog.py`) first.

Captures: `D:\Projects\ftt-scratch\ur-captures\theme\` (`theme_controls.png` is the one to compare with `ur-captures\art\demo_pause.png` and the mockup's `screens\`): the buttons, tags, banner, nameplate, ink and paper panels, tabs, bars and slider match the mockup's plates; the check-box icons, line edit, scroll bars and popups are still the flat placeholders.
