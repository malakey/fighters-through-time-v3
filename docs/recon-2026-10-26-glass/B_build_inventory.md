# Recon B: build inventory of the pulp UI layer (for a "Temporal Glass" restyle)

Repo `D:\Projects\Fighters Through Time - V3`, branch main, HEAD `221e0fa7`, baseline 3691 (`[TestCase\b` count under `tests/` = 3691 in 552 files, verified by script). Read-only recon: nothing run in Godot, no source touched. Counts are from greps/scripts on the working tree; "approximate" is stated where a suite is mixed and I classified by case titles, not bodies.

Up-front finding: **a "Temporal Glass" look already existed and was replaced.** `assets/sprites/ui/glass_button_{normal,focus,focus_ring,pressed,disabled}.png`, `card_frame*.png`, `title_flourish.png` and `assets/backgrounds/menu/temporal_glass_*.png`, `menu_glass_shards.png`, `menu_starfield.png` are still on disk and unreferenced (grep of `backgrounds/menu` and `ui/card_frame` over scripts/scenes/tests: 0 hits). Two theme variations keep the old name over pulp art: `TemporalGlassButton` (5 scenes, 5 scripts, 2 test files) and `DialogueGlassPanel` (1 scene, 2 scripts, 2 tests). A sibling dossier `C_glass_heritage.md` is already in this folder.

Legend: **K** keep (behaviour/layout logic any style needs), **R** restyle (logic stays, art/drawing changes), **D** delete (pulp-only).

---

## 1. File inventory and classification

### 1.1 `scripts/UI/Pulp/` (17 .cs, ~2419 lines incl. 1-line .uid stubs; namespace `FTT.UI.Pulp`)

| File (lines) | Role | Class |
|---|---|---|
| `HalftoneCorner.cs` (58) | Dot-grid corner overlay on panels via `halftone_corner.gdshader` (paper panels black dots 0.16, dark white 0.17); panel's first child | **D** |
| `PanelTagAnchor.cs` (44) | Plain control fitted to a PanelContainer's content area holding the tag plate (keeps scene node paths) | **R** (drop the tilted tag if glass has none) |
| `PulpAbilitySlot.cs` (350) | HUD ability slot: `PulpSlotState` {Ready, Cooling, Charging, Dormant, Suppressed}, `PulpRingMode`, pure `PulpSlotLook` state table, frame/ring/countdown/key tag drawing, `ui_desaturate` for dormant | **R** (state table, countdown logic K) |
| `PulpBarDecor.cs` (219) | Overlay on a `ProgressBar`: damage tracker, Rally echo band, hatch, decile ticks, low-HP glow, boss notches (`pulp_bar_layers.gdshader`); pure `Fractions` clamp | **R** (hatch **D**; fractions/tracker/echo/low-glow K) |
| `PulpButtonDecor.cs` (558) | Biggest widget: marker, `FocusPose`, prompt badge, **upper-case drawn label** (child layer; `Button.Text` stays the raw key), selected paper-plate swap, per-state label colours, long-label trim + badge reserve, multi-line wrap; `Apply` calls `PulpScreenKit.SeparateFocusRow` | **R** (badge/trim/wrap/left-align/idempotence K; upper-case draw **D**) |
| `PulpIcon.cs` (88) | Three-layer (fill/ink/shadow) icon control, 26 names | **R** |
| `PulpKit.cs` (91) | Reader of `pulp_kit.json`, cached texture lookup by stem, `Has`, `SharedPlate` | **K** (catalog path R) |
| `PulpPortraitRoundel.cs` (83) | Round portrait, art at 142 % offset masked by `ui_circle_mask.gdshader`; 112/110/86 px | **R** |
| `PulpPromptBar.cs` (117) | Prompt row: pad circles A/B or keycaps with bound keys; switches live | **R** (device switching K) |
| `PulpPromptGlyphs.cs` (113) | `PromptDevice` {Keyboard, Pad}, pad button -> letter, test seam | **K** |
| `PulpScreenKit.cs` (160) | Screen helpers: panel dressing (halftone + tag), `FocusRowGap`, `SeparateFocusRow`, `TagFromPanelCorner`, `RowButtonMaxWidth` 480, `FocusPlateExpandRight/Left` 10/6, prompt bar, leading icon | **R** (gap derivation K, halftone dressing **D**) |
| `PulpScreenKit.Pause.cs` (93) | Pause row 560 x 64, scrim alpha 0.7, `ShortTitle`, level clock | **K** |
| `PulpStatusChip.cs` (129) | Status chip: `PulpChipKind` colours (#FF7A5C, #8FC7FF, #38F2FF), ring share, empty slot keeps size | **R** |
| `PulpTaggedPanel.cs` (62) | Container with a panel tag (`TagOffset`, `PaddingBoxInset`, `TagDegrees` -1.2) | **R** |
| `PulpTagPlate.cs` (64) | `Create` + `Place(tag, position, maxRight)`: `RightMargin` 24, natural-width measure, ellipsis clamp, -1.2 deg tilt about bottom-left | **R** (clamp/measure K, tilt **D**) |
| `PulpToggleDecor.cs` (174) | Toggle track/knob decor over a CheckButton | **R** |

### 1.2 `scripts/UI/Motion/` (4 .cs, 605 lines)

| File | Role | Class |
|---|---|---|
| `FocusPose.cs` (177) | Focus pose: +16 px x, -1.2 deg tilt, scale 1.04, 0.14 s in/0.10 s out, writes Position/Rotation/Scale only, snaps under Reduced Temporal Effects | **R** (mechanism K; tilt **D**) |
| `PulpMotion.cs` (124) | Tween helpers that run while paused: panel pop-in, number thunk, hit flash | **K** |
| `PulpMotionRules.cs` (122) | Pure constants: `FocusTranslateX` 16, `FocusTiltDegrees` -1.2, `FocusScale` 1.04, `PressTranslate` 7, `CardLift` -10, `CardTiltDegrees` 1.6, panel pop 0.10 s/0.96, `HitFlashLimiter` | **K** (values R) |
| `ScreenWipe.cs` (178) | Layer-110 CanvasLayer: accent slab with slanted edge + 6 px ink edge, 8 steps in/8 out (0.26 s); Reduced Effects = ink cross-fade 0.2 s; `GameManager.LoadScene` uses it (2 refs) | **R** (cover/reveal/paused-tree/not-stranded logic K) |

### 1.3 `assets/sprites/ui/pulp/` (catalog `pulp_kit.json`, ~113 entries; 72 top-level PNG/tres + 37 `ability_icons/` + 4 `cover_heroes/` + 78 `icons/`, excluding .import)

| Group | Files | Class |
|---|---|---|
| Button plates `btn_{normal,focus,pressed,selected,disabled}_h{58,64,80}` | 15 | **R** (same 9-slice contract; expand margins 10/6 feed FocusRowGap) |
| Panels `panel_dark`, `panel_paper`, `tag_panel`, `banner_cover`, `banner_select`, `bubble`, `bubble_tail`, `stamp_ready`, `key_tag`, `keycap`, `tab_normal`, `tab_selected` | 12 | **R**; paper/tag/bubble/stamp/tab possibly **D** |
| HUD `slot_frame(_ready)`, `slot_ult(_ready)`, `bar_track_{boss,h14,h16,h30,h44}`, `boss_nameplate`, `boss_phase_tab`, `dial_back`, `dial_ticks`, `badge_disc`, `portrait_roundel` | 15 | **R** |
| Cards/select `card_frame`, `card_frame_pick`, `card_field(.tres)`, `card_halftone`, `select_split`, `star_badge_{fill,ink,sh}`, `sticker_continue`, `rays`, `burst_star`, `burst.tres` | 12 | **R**; halftone/burst **D** |
| Patterns `halftone_screen`, `portrait_halftone`, `dot_{8,9,10}`, `hatch_{boss,disabled,hp,random}` | 9 | **D** |
| Sliders/toggles `slider_{fill,grabber,track}`, `toggle_{knob,track_on,track_off}` | 6 | **R** |
| `cover_art`, `cover_heroes/*` (3 poses + json) | 5 | **R** (poses are ink-outlined by `MainMenuCoverInk`) |
| `icons/` 26 icons x 3 layers (`ico_X`, `_ink`, `_sh`): anchor, anchor_hollow, anchor_pip, anchor_pip_cracked, burn, cross, defy_{barred,notready,ready,spent}, dilation, dormant, dust, flag, freeze, hourglass, marker, prompt_circle, root, shield, slot_p1, slot_p2, star4, static, suppress, venom | 78 | **R** (3-layer contract pinned by PulpKitContentTests/PulpIconTests) |
| `ability_icons/` 36 crops (9 heroes x 4 slots) + json | 37 | **K** (crops of retro v2 sheets via `AbilityIconLibrary`) |

### 1.4 `tools/asset_src/pulp/` (13 .py/.ps1, 2 catalog json, `svg/` with 81 SVG sources)

`build_all.ps1` (16) orchestrates `build_pulp_svgs.py` (385; helpers `pulp_svg.py` 279), `build_pulp_images.py` (261, PIL rasters incl. halftone/hatch/dots), `icons_geometry.py` (110, ICN-B 32x32), `finish_pulp_icons.py` (67), `build_ability_icons.py` (78), `build_cover_heroes.py` (65), `write_catalog.py` (74, merges `catalog.json`+`catalog_svg.json` into `pulp_kit.json`), `apply_import_settings.py` (32, mipmaps), `build_contact_sheet.py` (160), `build_kit_demo.py` (350). **K**: build_all, write_catalog, apply_import_settings, pulp_svg, build_ability_icons, build_cover_heroes. **R**: build_pulp_svgs, icons_geometry, finish_pulp_icons, build_kit_demo, build_contact_sheet. **D**: the halftone/hatch/dot parts of build_pulp_images. The 81 SVGs: 78 are icon layers (`_fill/_sil/_top_raw`), the rest plate sources (**R**).

### 1.5 `tools/ui_theme/`

`build_ftt_theme.py` (544): sole generator of `resources/UI/ftt_theme.tres` (1531 lines, 94 sub_resources) and `scripts/UI/UIPalette.Roles.cs` (215). Palette constants l.25-42 (PLATE 090C1E@0.80, PAPER F3F0E6, LABEL_DARK 0C0A10, TRACK 3A3144, SLOT 2A2434, DANGER FF5A5A, HP 8CF0B8, BACKDROP 05071A, PROMPT FFF3D8), skewed `flat(...skew...)` helper l.72, `kit_tex`/`tbox` l.108-140 (StyleBoxTexture from `pulp_kit.json` 9-slice data), `ROLES` l.197-252, `button_set(..., skew=0.1405)` l.274, `build()` l.310-457, `VARIATIONS` l.483-510. **R**: the main restyle lever (mechanics K). `README.md` (60) documents generator + capture tool: **R** (text).

### 1.6 Shaders (`assets/shaders/`)

| Shader (lines) | Used by | Class |
|---|---|---|
| `halftone_corner.gdshader` (24) | `HalftoneCorner.cs` only | **D** |
| `pulp_bar_layers.gdshader` (95) | `PulpBarDecor.cs` only: tracker, echo, fill hatch/highlight, decile ticks, boss notches, skew compensation | **R** (skew/hatch **D**; tracker/echo/notches K) |
| `ui_circle_mask.gdshader` (16) | `PulpPortraitRoundel.cs` | **K** |
| `ui_desaturate.gdshader` (12) | `PulpAbilitySlot.cs` | **K** |

All four must keep the incoming modulate (`UiShaderModulationScanTests`, 1 case). `outline_glow.gdshader` is combat, out of scope.
---

## 2. Theme variations and Label roles (`build_ftt_theme.py`, mirrored in `UIPalette.Roles.cs`)

**49 Label roles** (`ROLES`) + **26 non-Label variations** (`VARIATIONS`) = 75 type variations. `UIPalette.LabelRoles` is at Roles.cs:5, `UIPalette.Variations` at Roles.cs:160 on. Counts = files naming the variation (scripts exclude `UIPalette.Roles.cs`).

### 2.1 Label roles (49)

- Existing four, re-pointed: `SmallLabel` (7 scn: FighterPlayOptions, MainMenu, DialogueBox, FighterHUD, MoveList, Settings, StoryPause; 2 cs; 2 tests), `HeadingLabel` (7 scn: CharacterSelect, CalibrationDrillList/Picker, HolodeckDrill, LoadingScreen, Settings, SystemsCard; 3 cs), `EmphasisLabel` (CharacterSelect), `TitleLabel` (12 scn; 4 cs).
- Cover/banners/captions: `CoverTitle` (MainMenu x8 nodes), `CoverBanner` (MainMenu), `BannerHeading` (CharacterSelect x2), `PauseTitle` (StoryPause; LocalFighterPause.cs, PauseMenu.cs; 3 tests), `LevelTitle` (StoryHUD; Level00Controller, StoryHUD.cs), `ObjectiveLine` (FighterLoading, GameOver, StoryHUD, StoryLoading; LevelResultsPanel), `PanelTag` (MainMenu x6; PausePanel, PulpTaggedPanel, PulpTagPlate), `HeroName` (FighterHUD, FighterLoading, StoryHUD; 5 cs), `HeroNameScene` (0 users), `CaptionLabel` (FighterHUD), `SceneCaption` (CharacterSelect, MainMenu, StoryHUD), `StatusLine` (7 scn), `PromptCaption` and `PromptKey` (PulpPromptBar only; "PromptKey" also string-matches 15 gameplay scripts by name collision), `HintLine` (CharacterSelect, Settings; FirstUseTooltipPresenter), `HintLinePlate` (Credits; CreditsController).
- HUD: `HudNumeral`, `HudNumeralSmall` (LevelResults, ResonanceGrid, StoryHUD; FighterHUD.cs, LevelResultsPanel), `SlotKeyTag`, `SlotNumber` (PulpAbilitySlot), `StatusName`, `StatusTime` (PulpStatusChip), `DefyLabel`, `RewindCount`, `StateLabel` (14 scn incl. the Fighter stage scenes and TestArena; TemporalCoreAnchor, FighterHUD, TestArenaHUD), `DustFloat`, `BossName`, `BossPhaseTag`, `BossHpValue` (StoryHUD scene+cs), `NoteLabel` (StoryHUD.Pulp.cs), `TimerDigits` (FighterHUD).
- Cards/select: `CardName`, `CardDates`, `CardStamp`, `CardQuestion`, `SideCardName`, `StatLabel`, `StatValue`, `StarBadgeText` (only `CharacterSelectScreen.Cards.cs`), `BubbleText` (CharacterSelect.tscn + Cards.cs), `StickerLine1/2/3` (MainMenu; FighterLoading uses Line1).
- Dialogue: `DialogueBody` (0 direct users), `SpeakerName` (DialogueBox; DialogueManager, DialogueLogScreen).
- Pulp-specific looks (outline px, shadow, Cinzel caps) most likely to restyle: CoverTitle (170 px, outline 7, shadow 2,2), CoverBanner, BannerHeading, PauseTitle (Cinzel 900, 78 px), PanelTag, Sticker*, StarBadgeText, CardQuestion, BubbleText, SideCardName. The rest are the type scale (K).

### 2.2 Non-Label variations (26)

| Variation (base) | Scenes | Scripts | Tests |
|---|---|---|---|
| `PulpButton` (Button, T10) | CharacterSelect x2, FighterPlayOptions x3, MainMenu x10, GameOver x2, LevelResults (5 scn) | MainMenu.Cover, TimelineRestartPanel, PulpButtonDecor, 2 capture files | 4 |
| `PulpButtonCompact` (T11) | 7 scn (MainMenu x34 nodes, CharacterSelect x3, drills, ResonanceGrid, Settings) | 10 cs (BootSequenceOverlay, CalibrationDrillList, ConfirmModal, HolodeckConsolePanel, MainMenu.Cover, MoveListScreen, ...) | 3 |
| `TemporalGlassButton` (legacy name, 26 px pulp plate) | MainMenu x6, MoveList x2, Settings, StoryPause x8, SystemsCard | PauseMenu, PulpButtonDecor, PulpScreenKit.Pause, 2 capture | 2 |
| `InkPanel` (plate 1) | 11 scn (MainMenu x6, FighterPlayOptions, FighterLoading, GameOver, HolodeckDrill x2, LoadingScreen, MoveList, Settings, StoryLoading, StoryPause, SystemsCard) | 8 cs (FirstUseTooltipPresenter, HolodeckConsolePanel, NetworkSelectScreen, ...) | 4 |
| `PaperPanel` (plate 2) | FighterLoading | CharacterSelectScreen.Cards + captures | 1 |
| `HudPlatePanel`, `BannerPanel`, `BossNameplate`, `SlotPlate`, `SlotPlateReady`, `BubblePlate` | none | captures only (BossNameplate etc. are built in code or unused) | 1 each |
| `DialogueGlassPanel` (legacy name) | DialogueBox | DialogueTheme, captures | 2 |
| `PanelTagPlate` (plate 3) | MainMenu x6 | PausePanel, PulpScreenKit, PulpTaggedPanel, PulpTagPlate | 1 |
| `BossPhaseTab` | StoryHUD | StoryHUD.cs | 4 |
| `KeyTagPlate` | none | PulpAbilitySlot | 1 |
| `CardPlate`, `CardPlatePick`, `StampPlate` | none | CharacterSelectScreen.Cards | 2, 2, 1 |
| `ScrimPanel` | MoveList, StoryPause, SystemsCard | PulpScreenKit.Pause | 3 |
| `PulpBar` (30 px) | StoryHUD | captures | 2 |
| `PulpBarH44` (Fighter HP), `PulpBarH16` (meter) | FighterHUD x2 each | FighterHUD.cs | 1 each |
| `PulpBarH14` (select stats) | none | Cards.cs | 1 |
| `CoverBannerPanel` | MainMenu | none | 2 |
| `DialogueBodyRich` (RichTextLabel) | DialogueBox | DialogueManager | 1 |
| `PulpBossBar` | StoryHUD | captures | 1 |

Other theme content: default font Outfit 400 in a `FontVariation` (empty `fallbacks` = reserved CJK slot), Cinzel for headings, four check/radio textures from `assets/ui/theme_placeholder/`, focus styles, Tab/Slider/ScrollBar styles. `UIPalette.cs` (423) holds the PAL-B tokens (Accent F3C95B, Ink F3F0E6, PlateFill 090C1E@0.8, ...), `P1/P2/OwnerOnDark`, `ApplyTheme`, `ApplyUiScale`, `AppliedUiScale`; `Cyan` (0,0.9,0.9), `CyanDim` and `UnboundCold*` still exist as constants (a glass look with a cyan accent collides with `UiChromeCyanSourceScanTests`, see 4).

---

## 3. Who uses the pulp layer, by screen

Every authored scene below adopts `res://resources/UI/ftt_theme.tres` on its root (20 of 24 UI/menus scenes; LevelResults, StoryHUD, LocalFighterPause, MatchResults, NetworkSelect and FloatingDamageNumber get it from code or `UIPalette.ApplyTheme`). Scenes hold direct `ui/pulp` art references only in MainMenu (8), CharacterSelect (2), FighterLoading (1), StoryLoading (1); everything else reaches pulp art through the theme or `PulpKit` in code.

| Screen | Scene(s) | Scripts and pulp pieces |
|---|---|---|
| Main menu cover + sub-screens (root, slot, character, difficulty, extras) | `scenes/menus/MainMenu.tscn` (151 nodes: CoverTitle x8, PulpButton x10, PulpButtonCompact x34, TemporalGlassButton x6, InkPanel x6, PanelTagPlate/PanelTag x6, CoverBannerPanel, Sticker*, TitleLabel x6) | `MainMenu.cs` (714), `MainMenu.Cover.cs` (329: PulpButtonDecor, PulpMotion, PulpPromptBar), `MainMenuContinue.cs`, `MainMenuCoverInk.cs`, `BootSequenceOverlay.cs` (PulpButtonDecor, PulpToggleDecor), `ThirdPartyLicenses.cs`; art: cover_art, cover_heroes, sticker_continue, banner_cover, rays, burst |
| Character select (card grid + stage phase) | `scenes/menus/CharacterSelect.tscn` (44) | `CharacterSelectScreen.cs/.Cards.cs` (586: PulpAbilitySlot, PulpBarDecor, PulpButtonDecor, PulpIcon, PulpKit, PulpPromptBar, PulpTaggedPanel, PulpToggleDecor), `CardGridRules`, `CardTextFit`, `.PadMenu.cs`; art: card_frame*, card_field, card_halftone, select_split, star_badge_*, stamp_ready, bubble*, ability icons |
| Stage select | same scene (stage phase) + `HolodeckConsolePanel` stage phase | as above |
| Fighter play options | `FighterPlayOptions.tscn` (10) | `FighterPlayOptionsScreen.cs` (58; PulpScreenKit x3) |
| Network select (LAN, de-scoped) | `NetworkSelect.tscn` (1) | `NetworkSelectScreen.cs` (PulpButtonDecor, PulpScreenKit) |
| Story HUD (corner cluster, dust plate, Integrity dial, time tools) | `StoryHUD.tscn` (80) | `StoryHUD.cs` (1129), `StoryHUD.Pulp.cs` (448: PulpAbilitySlot x9, PulpBarDecor x6, PulpIcon, PulpKit, PulpPortraitRoundel, PulpStatusChip x7, PulpTaggedPanel x6), `IntegrityDialLayer.cs` (dial_back, dial_ticks), `HudBadge`, `AnchorPip`, `BeaconAnchorIcon`, `HudAbilityIndicatorModel` (K), `RadialProgress` |
| Boss bar (nameplate, PHASE tab, notches) | inside `StoryHUD.tscn` (`SafeArea/BossPanel`) | `StoryHUD.Pulp.cs` `LayoutBossPanel` (l.~360-435), `BossPhaseNotchOverlay.cs` (PulpBarDecor), `BossBarPhaseNotches.cs` (pure); art boss_nameplate, boss_phase_tab, bar_track_boss, hatch_boss |
| Fighter HUD | `FighterHUD.tscn` (68) | `FighterHUD.cs` (1057: PulpAbilitySlot x7, PulpBarDecor x4, PulpIcon x6, PulpMotion, PulpPortraitRoundel x2, PulpStatusChip x5), `FighterHudModel.cs` (pure fit; `RoundelColumnWidth` 130), `FighterOverlayModel`, `FighterPresentationOverlay` |
| Story pause | `StoryPause.tscn` (17: TemporalGlassButton x8, PauseTitle, InkPanel, ScrimPanel) | `PauseMenu.cs` (612; PulpScreenKit x8), `PauseMenuBase`, `PausePanel.cs` (57: HalftoneCorner, PulpTagPlate, PulpTaggedPanel) |
| Fighter pause | `LocalFighterPause.tscn` (1 node, code-built) | `LocalFighterPause.cs` (PulpScreenKit x5) |
| Settings (Audio, Display, Gameplay, Controls tabs; UI scale slider) | `Settings.tscn` (76: HeadingLabel x2, SmallLabel x4, TemporalGlassButton, PulpButtonCompact, HintLine, InkPanel) | `SettingsMenu.cs` (898: PulpScreenKit, PulpToggleDecor; Controls consts l.496, `ApplyColumnWidths` l.616), `PausePanel.cs`, `PlayerSlotPaletteRow.cs`; art slider_*, toggle_* |
| Level results | `LevelResults.tscn` (12) | `LevelResultsPanel.cs` (368: PulpButtonDecor, PulpScreenKit x7) |
| Match results | `MatchResults.tscn` (1, code-built) | `MatchResults.cs` (363: PulpButtonDecor, PulpIcon x4, PulpPortraitRoundel x4, PulpScreenKit x2) |
| Dialogue box + log | `DialogueBox.tscn` (10) | `DialogueManager.cs` (992: PulpIcon, PulpPortraitRoundel), `DialogueLogScreen.cs`, `DialogueTheme.cs`, `DialogueEmotionTreatment`; art bubble, ico_hourglass caret |
| Loading (Story portal, Fighter VS, plain) | `StoryLoading.tscn` (14), `FighterLoading.tscn` (20; PulpPortraitRoundel script), `LoadingScreen.tscn` (7) | `LoadingScreen.cs` (256: PulpPortraitRoundel x2, PulpScreenKit); `ScreenWipe` via `GameManager.cs` |
| Credits | `Credits.tscn` (5; HintLinePlate) | `CreditsController.cs` |
| Game Over | `GameOver.tscn` (10) | `GameOverScreen.cs` (PulpButtonDecor x2, PulpScreenKit x2) |
| Confirm modal / two-option prompt | code-built | `ConfirmModal.cs` (198: PulpButtonDecor, PulpScreenKit x2, explicit `SeparateFocusRow`), `TwoOptionPrompt.cs` |
| Resonance grid | `ResonanceGrid.tscn` (11) | `ResonanceGridPanel.cs` (598: PulpButtonDecor, PulpKit, PulpScreenKit), `ResonanceChipLayout.cs` (67, pure), `ResonanceEdgeLayer`, `ResonanceTierMark`, `ResonanceGridNavigation` |
| Move List / Systems Card | `MoveList.tscn` (15), `SystemsCard.tscn` (26) | `MoveListScreen.cs` (342), `SystemsCardScreen.cs` (76), `PausePanel.cs` as root script; `Center/Panel/Layout` separation 36 |
| Calibration Drills | `CalibrationDrillList.tscn` (11), `CalibrationDrillPicker.tscn` (23), `HolodeckDrill.tscn` (32) | `CalibrationDrillList.cs`, `CalibrationDrillPicker.cs`, `Combat/CalibrationDrillRunner.cs` (PulpButtonDecor, PulpScreenKit) |
| Hub screens | code-built Holodeck console, Repository terminal, Warden Beacon | `HolodeckConsolePanel.cs` (410: PulpButtonDecor x3, PulpTaggedPanel, PulpToggleDecor x2), `ResonanceGridPanel`, `DialogueManager`, `TimelineRestartPanel.cs` (PulpScreenKit x3), `FirstUseTooltipPresenter.cs` (InkPanel, HintLine) |
| Other | `FloatingDamageNumber.tscn`, Fighter stage scenes (only `StateLabel`) | not pulp art |
---

## 4. Test suites touching the UI layer

Assumption for every "keep": the plan does **not** rename `Pulp*` classes or variations. A rename turns every suite that names them into "update" (section 7). Method: a script over `tests/` (552 files, 3691 `[TestCase`) plus case titles of the UR-era suites; mixed suites are split with approximate case counts (class by title, not by body).

### 4.1 UR-era and directly affected suites: 34 suites, 138 cases

| Suite (folder) | Cases | Pins | Keep | Update | Rewrite/delete |
|---|---|---|---|---|---|
| PulpMotionRulesTests (Unit) | 4 | number thunk, stepped progress, timings, hit-flash limiter | 4 | | |
| PulpMotionTests (Unit) | 10 | focus pose snap/tween/identity, panel pop, wipe covers/reveals/paused/not stranded | 10 | | |
| UiCaptureRunnerTests (Unit) | 3 | arg parse, registry discovery, theme specimens free cleanly | 3 | | |
| UiShaderModulationScanTests (ContentValidation) | 1 | every pulp shader keeps modulate | 1 | | |
| AbilityIconLibraryTests (ContentValidation) | 3 | four icons per hero, slot names = ability files | 3 | | |
| PulpBarDecorTests (Pulp) | 3 | fraction clamp, low glow static when reduced, overlay leaves fill alone | 3 | | |
| PauseScreenKitTests (Pause) | 2 | tag takes name before first dash, level clock | 2 | | |
| UrPolishTests (Unit) | 5 | modal/results labels whole at 1.0/1.4, credits hint band, dialogue padding + slider track under grabber, capture scale reset, menu fit reserve | 4 | 1 (slider/grabber) | |
| UrPolish2Tests (Unit) | 7 | short level tag, tag clamp, Controls columns, footer pose clears scroll, boss name vs PHASE tab, focused button in row clears neighbours, quit text 20 % | 3 (short name, columns, quit) | 4 (tag clamp, footer pose, boss/tab, focus row: use FocusScale/FocusTilt, plate expand, tab rotation) | |
| FighterHudTopBandTests (Unit) | 3 | widgets at spec sizes, hit flash once a second, top band on screen at both scales | 2 | 1 | |
| CharacterSelectCardTests (Unit) | 7 | 5-column grid, dates, stat bars, both-pick frames + READY stamp, slot palettes, text shrink, stage toggles | 3 | 4 | |
| PulpAbilitySlotTests (Pulp) | 4 | state table, countdown order, centre number + art inset, parts per state | 2 | 1 | 1 (inset spec numbers) |
| PulpStatusChipTests (Pulp) | 2 | glyph colour + ring share, empty slot keeps size | 1 | 1 | |
| MainMenuCoverTests (Unit) | 11 | sticker x4, title live keys, banner decor, prompt bar, three inked hero poses, column pitch, no overrides/cyan | 4 | 6 | 1 (hero ink poses) |
| UIThemeTests (ContentValidation) | 12 | theme entries, focus styles, palette match, role scale, fonts, role face/weight, every variation exists, folded kit gaps, ownership colours + gold chrome, contrast floor, glyph coverage, ApplyTheme | 3 (focus style, glyph coverage, ApplyTheme) | 7 | 2 (OwnershipColours..ChromeIsGold, FoldedKitGaps) |
| PulpButtonDecorTests (Pulp) | 12 | marker tint/shadow table, apply-once, compact marker, badge on focused row only, disabled dim, selected paper swap, left-align, upper-case drawn label, transparent button glyphs + per-state colours, long-label trim, double-decor colour, multi-line wrap | | 8 | 4 (marker table, paper swap, per-state draw, upper-case) |
| PulpScreenKitTests (Pulp) | 4 | tag only with key, halftone behind content once, leading icon one string, button stack keeps focus pose in plate | | 3 | 1 (halftone) |
| StoryHudPulpTests (Unit) | 12 | corner cluster widgets, slot model, not-yet flash, chips, Defy seal shapes, HP tracker, freeze strings, time tools, Integrity dial, gold wedge, nameplate + PHASE tab + notches, objective flag | | 10 | 2 (wedge gold, nameplate/tab) |
| PulpIconTests (Pulp) | 3 | names load three layers, kit list = files on disk, unknown safe | | 3 | |
| PulpPromptBarTests (Pulp) | 3 | pad circles A/B, keycaps live, pad letters | | 3 | |
| PulpPortraitRoundelTests (Pulp) | 1 | 142 % offset + disc mask | | 1 | |
| PulpTaggedPanelTests (Pulp) | 2 | fill + min size, tag tilted / hidden when empty | | 2 | |
| PulpToggleDecorTests (Pulp) | 2 | knob slide, idempotent | | 2 | |
| ScreenSceneStyleTests (Pulp) | 1 | no restyled scene overrides font size or paints chrome cyan | | 1 | |
| OverlaySkinTests (Pause) | 5 | switches = toggle decor, Move List/Systems Card on ink panel, modal on ink panel, dialogue roundel + hourglass caret, log rows roundels | | 5 | |
| PauseSkinTests (Pause) | 2 | pause skin both build paths, Fighter pause + disconnect modal | | 2 | |
| PausePanelTests (Pause) | 1 | padding 30/20/20/28, tag out of flow | | 1 | |
| MatchResultsPulpTests (Unit) | 1 | winner star, loser dim | | 1 | |
| LoadingRoundelTests (Unit) | 1 | VS screen fills both roundels | | 1 | |
| ResonanceChipTests (Unit) | 2 | tier shapes, edge leaves chip at border | | 2 | |
| PulpKitContentTests (ContentValidation) | 3 | catalog files load at stated size, nine-slices leave a centre, icons have 3 layers | | 3 | |
| HalftoneCornerTests (Pulp) | 2 | corner first child + shader + ignores mouse; dot colours | | | 2 (delete) |
| MainMenuCoverInkTests (Unit) | 2 | ink outline + shadow pixel, half-transparent edge | | | 2 (delete or rewrite) |
| UiChromeCyanSourceScanTests (ContentValidation) | 2 | no UI code/scene reads `UIPalette.Cyan` or a cyan literal outside 3 allow-listed files; detector self-test | | | 2 (rewrite if glass chrome is cyan; keep if chrome stays gold) |
| **Totals (34 suites)** | **138** | | **48** | **73** | **17** |

Of the 17 rewrite/delete, about 7 are pure deletes with no replacement (HalftoneCorner 2, CoverInk 2, upper-case label 1, halftone dressing 1, hero ink poses 1); about 10 are one-for-one rewrites (count neutral). The 145 UR-era cases in the baseline arithmetic and these 138 overlap but are not identical (UIThemeTests has 7 pre-UR cases, UrPolish2/UrPolish counted by suite).

### 4.2 Pre-existing suites that only name a role, variation or node path: 10 suites, 79 cases, keep unchanged

CharacterSelectSceneTests 24 (`CardName`), DialoguePresentationTests 15 (`SpeakerName`), StoryPauseTests 9 (`PauseTitle`), CalibrationDrillsRouteTests 8 (`PulpButton` once), StoryHudPresentationTests 8 (`LevelTitle`), LocalFighterPauseTests 7 (`PauseTitle`), StoryHudContractTests 3 (`BossName`, `LevelTitle`), ResonanceGridFitTests 2 (chip fit at both scales; its tolerance depends on chip chrome, may need a nudge), FighterHudLayoutRulesTests 2, StoryHudBoundsTests 1 (`BossPhaseTab` rect). They change only on a rename or node-path change. About 100 further UI-adjacent files (UiScaleTests 7, FocusChainBuilderTests 9, MenuLayoutBoundsTests 4, MainMenuSceneTests 17, SettingsMenuSceneTests 12, ConfirmModalTests 6, PadMenu* 23, LoadingScreenTests 7, MoveListScreenTests 3, CardGridRulesTests 3, CardTextFitTests 2, MainMenuContinueRulesTests 3, ...) pin layout bounds, scale 1.0/1.4 fit, focus chains and behaviour: **keep unchanged** while node paths survive.

### 4.3 Predicting the delta against 3691

- Art/theme-only re-skin, nothing deleted: 0 count change, about 73 suite edits and about 10 rewrites.
- Deleting the pulp-only pieces: about -7, i.e. 3684 before any new test. Dropping every rewrite/delete candidate instead of rewriting: up to -17 (3674).
- New glass tests (a glass theme-pin case, a glass kit catalog test, new widgets) are additive, none planned yet. The plan should name, per suite, whether it takes the -7 or -17 end.

---

## 5. Shared hot spots a parallel build would collide on

1. **Theme generator and outputs.** `tools/ui_theme/build_ftt_theme.py` -> `resources/UI/ftt_theme.tres` (1531 lines, 94 sub_resources) and `scripts/UI/UIPalette.Roles.cs` (215, auto-generated). Every screen workstream that needs a role or variation edits the same script, and the two outputs conflict on every line. CLAUDE.md: never hand-edit the .tres. One owner, or split into per-area tables merged at build.
2. **`scripts/UI/UIPalette.cs`** (423): PAL-B tokens mirrored by hex in the generator and pinned by `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants`; a palette change edits both plus the test. `ApplyTheme/ApplyUiScale` are used by the capture runner.
3. **`localization/en.csv` + `en.en.translation`.** The redesign added **50 rows** since `daef1285`: `ui_prompt_select`, `ui_toggle_on/off`; 10 `hud_*` (vitals, abilities, defy_label, integrity_caption, integrity_sealed, time_freeze_name, seconds_short, percent, rewind_count, boss_phase); 9 `character_*_dates`; 9 `character_*_card_name`; `fighter_stat_max_hp/speed/weight`, `fighter_stats_card`, `fighter_select_choosing`, `fighter_random_mystery`; `menu_cover_title_word`, `menu_cover_title_banner`, `menu_continue_chapter`; `fighter_results_player`, `loading_player_one/two`, `resonance_grid_tag`; `fighter_hud_mode_stock/time`; `pause_status_level`, `pause_tag_controller`; `license_name_ofl`. Removed `fighter_player_selection`, `menu_subtitle`; changed `pause_quit_confirm` (20 %). After any edit run `--import` and commit the regenerated binary `en.en.translation` (regenerate after a merge, never merge it).
4. **Shared scenes.** `MainMenu.tscn` (151 nodes, 53 pulp refs; cover + 4 sub-screens + extras) is the biggest collision; then `StoryHUD.tscn` (80, boss bar inside), `Settings.tscn` (76), `FighterHUD.tscn` (68), `CharacterSelect.tscn` (44). `MoveList`, `SystemsCard`, `StoryPause` and `Settings` share `PausePanel.cs` (57 lines: dressing, padding 30/20/20/28, tag) and the footer separation 36.
5. **Shared scripts every screen calls:** `PulpButtonDecor.cs` (558, 10+ screens), `PulpScreenKit.cs` / `.Pause.cs`, `PulpKit.cs`, `FocusPose.cs`, `PulpTagPlate.cs`.
6. **`AGENTS.md`** repo map l.146-168 (`assets/shaders`, `assets/fonts`, `assets/sprites/ui/pulp`, `tools/ui_theme`, `scripts/UI/Pulp`, `scripts/UI/Motion`, `scripts/Diagnostics/UiCapture`), UI bullets l.424-428 (shared theme, type-scale roles, Story HUD, the pulp redesign bullet), baseline paragraphs from l.493, the known-gaps subsection l.623-627. **`CLAUDE.md`**: the "UI captures" section, the authority-map rows for the redesign, the UI / HUD rows of "Where to start". Both edit the baseline paragraph, so parallel workstreams always conflict there.
7. **UI capture tool** `scripts/Diagnostics/UiCapture/` (11 files, 1544 lines): runner plus 9 partials with **76 `[UiCaptureTarget]` methods** (Hudf 5, Huds 5, Menu 10, Pause 18, Screens 16, Select 6, Theme 5, Widgets 5, WidgetsGauges 6). `Theme`, `Widgets`, `WidgetsGauges` name variations/widgets directly (compile break on rename). The reference gallery is outside the repo (`D:/Projects/ftt-scratch/ur-captures/main-6a840a3f/`, 76 PNGs).
8. **`pulp_kit.json`** is generated by `write_catalog.py`; `.png.import` sidecars are rewritten by Godot on each launch (the working tree already shows hundreds of unrelated `.import` modifications; never commit them).
9. **Tests:** `tests/Pulp/` (11 files) and the 34 suites of 4.1.

---

## 6. UR-POLISH / UR-POLISH2 fixes that depend on pulp geometry

| Fix | Where | Depends on | Glass implication |
|---|---|---|---|
| Tag plate width clamp (`Place(tag, position, maxRight)`, `RightMargin` 24, ellipsis only when over, round trip restores size) | `PulpTagPlate.cs`; callers `PanelTagAnchor`, `PausePanel`, `PulpTaggedPanel`; `UrPolish2Tests.AnOverlongTagClamps...` | natural width from label font + label/plate stylebox margins; -1.2 deg tilt about bottom-left; `TagOffset`/`PaddingBoxInset` | Clamp logic stays if glass keeps tags; margins and tilt change so natural width and fixtures change. If glass has no tags, delete with PanelTagPlate / HalftoneCorner dressing (4 code panels + the main menu's 6 scene-authored `TagHolder/Tag` nodes) |
| `FocusRowGap` ~43.6 px = `FocusTranslateX` 16 + (`FocusScale` 1.04 - 1)/2 x `RowButtonMaxWidth` 480 + expand 10 + 6 + 2; `SeparateFocusRow` via `PulpButtonDecor.Apply` (HBox of 2+ buttons), `ConfirmModal`, `ResonanceGridPanel` | `PulpScreenKit.cs` l.25-34, `PulpMotionRules` focus numbers, `btn_focus_*` expand margins | Derived, so it follows a new pose automatically. A glow focus with no slide or scale shrinks it to the margins and rows could tighten. Keep the derivation, update `FocusPlateExpand*` to the glass plate. Test `AFocusedButtonInAHorizontalRow...` stays valid |
| Footer gap 14 -> 36 in `MoveList.tscn` / `SystemsCard.tscn` | scene separation; `TheFocusedFooterPoseClearsTheScrollViewportInBothScreens` (28 px floor + posed rect from FocusScale, FocusTilt, plate expand) | focus pose | Keep the gap; update the test's pose arithmetic if the pose changes |
| Boss PHASE tab drop rule (name beside tab with 0.5 shrink floor, else full width and tab 6 px below the scaled name plate, bar 6 px below the tab, ellipsis only past the floor at full width) | `StoryHUD.Pulp.cs` `LayoutBossPanel` l.~360-435: `PlateHeight` 62, `TabHeight` 35, `BarHeight` 44, `TabWidth` 128/104, name rotation -1 deg, tab +2 deg, 687 max name width; `EveryBossNameAndThePhaseTabStay...`, `StoryHudBoundsTests`, `StoryHudPulpTests` | tilted nameplate geometry (rotation-aware rects), plate heights | The rule is style-neutral layout; its constants come from the pulp nameplate art. An untilted glass plate removes the tilt allowances (+2 / -1 deg, 16 px growth) |
| Controls column widths (300/260/340/140/140, grown to widest text, caps 380/360/520/200/200, re-applied on UI-scale change in `_Process`) | `SettingsMenu.cs` l.496 and l.616-640 | font metrics + cell stylebox minimum width + 28 px | Style-neutral and measured at run time: K (the glass button chrome changes the measured width, no code change) |
| Resonance grid chip fit (chip sizes from own text, separation, no word breaks, whole buttons, prompt bar clears) | `ResonanceChipLayout.cs` (67), `ResonanceGridPanel.cs`; `ResonanceGridFitTests` | chip chrome margins, `ResonanceTierMark` shapes, `PulpButtonCompact` | Logic K; chip chrome numbers change with glass chips, test tolerances may need an update |
| Modal/results labels whole, credits hint band, dialogue padding, slider track under grabber, capture scale reset, menu fit reserve | `UrPolishTests` | only the slider case depends on `slider_track` / `slider_grabber` geometry | one case to update |
| Quit text 20 %; Level Complete short name | `en.csv`; `PulpScreenKit.ShortTitle` | none | K |

Also style-neutral and to keep: `MenuSafeAreaFit` (scales a centring parent about the centre, floor 0.5) and `UiScaleBinder`.

---

## 7. Naming: how deep "Pulp" goes

- File/dir names containing `pulp`: `scripts/UI/Pulp/` (17 .cs), `scripts/UI/StoryHUD.Pulp.cs`, `scripts/UI/Motion/PulpMotion.cs` and `PulpMotionRules.cs`, `assets/sprites/ui/pulp/`, `assets/shaders/pulp_bar_layers.gdshader`, `tools/asset_src/pulp/`, `pulp_kit.json`, `tests/Pulp/` (11 files), `tests/Unit/{PulpMotionRulesTests,PulpMotionTests,MatchResultsPulpTests,StoryHudPulpTests}.cs`, `tests/ContentValidation/PulpKitContentTests.cs`: about 52 files plus 3 trees.
- Content containing `Pulp` (case-sensitive): **scripts 59 files (590 occurrences)**, **tests 32 files (427 occurrences)**, **scenes 13 files**, resources 1 (`ftt_theme.tres`), tools 2 (24 case-insensitive), docs 14 .md; `localization` 0, `project.godot` 0.
- Namespace: `namespace FTT.UI.Pulp` in 13 files; `using FTT.UI.Pulp` in 41 files (scripts + tests).
- Names a rename touches: classes `Pulp{AbilitySlot,BarDecor,ButtonDecor,Icon,Kit,PortraitRoundel,PromptBar,PromptGlyphs,ScreenKit,StatusChip,TaggedPanel,TagPlate,ToggleDecor}`, `PulpMotion/PulpMotionRules`, enums `PulpSlotState/PulpRingMode/PulpBarStyle/PulpChipKind`; theme variations `PulpButton`, `PulpButtonCompact`, `PulpBar`, `PulpBarH44/H16/H14`, `PulpBossBar` (generator, `UIPalette.Variations`, scenes, tests); `res://assets/sprites/ui/pulp/` (6 script, 5 scene/resource, 1 test, 7 tool references); shader and catalog names; capture target names; `docs/handoffs/UR_*`.
- Legacy-named variations already misleading: `TemporalGlassButton`, `DialogueGlassPanel`.
- Rename cost: mechanical but wide (about 100 files, scene `ext_resource` paths in `StoryHUD.tscn`, `MainMenu.tscn`, `FighterLoading.tscn` that point at `scripts/UI/Pulp/*.cs`, plus `.uid` and `.import` renames). Options: (a) keep `Pulp*` identifiers and re-skin (the repo already has the "legacy name kept" precedent), (b) rename only theme variation strings and the art folder, (c) full rename as one serial step before any parallel work, because it collides with every item in section 5.