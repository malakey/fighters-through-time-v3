# Recon C — menus and overlays (2026-10-26)

Read-only subagent report at `c12149cd`, code reading only (test bodies mostly not opened). Evidence, not authority.

## Headlines
1. No fonts ship (`assets/fonts/` empty; `ftt_theme.tres:334-339` FontVariation without `base_font`, pinned by `UIThemeTests.ThemeWorksWithoutAFontResource`). Cinzel/Outfit need OFL rows in `ThirdPartyLicenses.Notices` (`scripts/UI/ThirdPartyLicenses.cs:24`; `MainMenuExtrasTests.TheLicensesScreenListsEveryBundledComponentWithItsLicense` checks the row count against `Notices.Length`).
2. Gold as the UI accent touches the visual grammar (gold = Warden / Resonance; `UnboundCold` = the Severed). The user's palette choice (Resonance Gold: "gold chrome and focus, with cold cyan reserved for the Severed and player 1") is the ruling that the interface wears the Wardens' gold.
3. A theme swap alone reaches about half the screens; many scenes and scripts carry local overrides (§5-6). `DialogueTheme.cs:34-40` holds a second glass palette (`GlassBackground`, `GlassBorder`, `GlassShadow`).
4. No life dates exist anywhere (§3).

## 1. Main menu (`scenes/menus/MainMenu.tscn`, `scripts/UI/MainMenu.cs` 707 lines)
- Seven sibling screens (`MainMenuScreen`, `MainMenu.cs:8-19`): `RootScreen`, `SlotScreen`, `CharacterScreen`, `DifficultyScreen`, `LevelSelectScreen`, `ExtrasScreen`, `LicensesScreen`; plus `Background` (`ColorRect`, `Starfield`, `GlassShards`, `TemporalMainArt`, `TemporalSubmenuArt`) and `VersionLabel`. `_stack` drives `GoBack()`; `ShowScreen` (`:369`) rebuilds focus with `FocusChainBuilder.Apply`.
- Root: `RootScreen/Center` (CenterContainer, `anchor_right = 0.44` — on `MenuLayoutBoundsTests`' `LeftAligned` allow-list) → `Panel` (`StyleBoxEmpty`) → `Layout` (VBox): flourishes, `Title` (46 px override), `Subtitle`, `NoticeSlot`, buttons QuickPlay, Story, LevelSelect (dev), Fighter, CalibrationDrills, Settings, Extras, Quit. Each button repeats five `StyleBoxTexture` overrides (`GlassBtn*`, `:16-67`) + `font_size = 22`; 79 `theme_override` lines in the scene.
- **No Continue button.** Story → `SlotScreen` (`SlotRow{0-2}` with `SlotButton` + `DeleteButton`); text from `SaveSlotSummary` (`:591`; `save_slot_summary`, `LastSavedTimestamp` (`SaveManager.cs:26`), `LocationText`, lowest-difficulty line). A Continue sticker needs "most recent slot" logic (max `LastSavedTimestamp` over `SaveManager.Instance.SaveSlots`) and must call `StoryManager.ResumeCampaign(slot, save)` as `OnSlotPressed` does (`:545`).
- Story character screen: `CharacterScreen/.../Grid` with `CharacterButton0-8` (9 tiles, no Random); `BindCharacterScreen` (`:293`) uses `GetNodeOrNull`. Tile and level buttons force `ThemeTypeVariation = "TemporalGlassButton"` in code (`:307`, `:351`).
- Tests (`MainMenuSceneTests`, `MainMenuExtrasTests`, `MenuLayoutBoundsTests`): `TheAuthoredSceneCarriesEveryScreenAndControlTheScriptBindsByPath` pins `Background`, the seven screens, the eight root button names under `RootScreen/Center/Panel/Layout/` (`MainMenuSceneTests.cs:56-62`), `SlotRow{0-2}/SlotButton|DeleteButton`, Story `Grid` child count == `CharacterRoster.Count` (no Random there), level grid 16, `NoticeSlot`; `StaticCopyIsStoredAsRawTranslationKeys…` (`Title` = `game_title`, `StoryButton` = `menu_story_mode` — an outlined live-text title keeps the raw key); `EveryInteractiveControlOnEveryScreenJoinsItsFocusChain`; the dev level select hidden outside debug; `CalibrationDrillsSitsBetweenFighterAndSettings`, `ExtrasSitsBetweenSettingsAndQuit`; `TheRootScreenCarriesNoLanButton`; `TheVersionCornerShowsTheCanonicalBuildVersion`; session snapshot/restore; `HostedMenusNeverRaiseTheBootSequence`.
- The pulp cover changes the root's node layout (cover art, hero sprites, Continue sticker); keep the `Layout/*Button` paths or move the tests with it. A theme restyle covers the sub-screens once the per-button texture overrides are removed.

## 2. Fighter select (`scenes/menus/CharacterSelect.tscn`, `scripts/UI/CharacterSelectScreen.cs` 1182 lines + `.PadMenu.cs`)
- Tiles are `Button`s with `FocusMode = None` in the skip group (`:541-542`), driven by per-player cursors (`TryMoveCursor`, `:325`) and the mouse (`OnTilePressed`, `:642`). `FocusChain` (`:485`) holds only the footer in the select phase (`RemotePlayNotice`, `MoveListButton`, `SystemsCardButton`, `BackButton`) and the rules controls + Back/Fight in the stage phase.
- Grid: `SelectPhase/Root/PlayersRow/Grid` with `CharacterButton0-8` + `RandomTile` (10), `columns = 3` in the scene and `GridColumns = 3` in code (`:63`); `TryMoveCursor` wraps with `rows = ceil(TileCount / GridColumns)` and clamps. **5 × 2 = one constant + one scene property** (10 tiles fit). Test helper `MoveCursorTo` (`CharacterSelectSceneTests.cs:1044`) reads `columns`; specific-step assertions (lines ~287-418, 456, 921) must be re-checked by running the file.
- Tile content is code-built: `AddPortraitLayer` (72 × 72 TextureRect + name Label), `AddTokenBadge` (▲ / ●); no life-dates label. `StyleTile` (`:674-759`) writes per-tile `normal` / `hover` / `pressed` styleboxes and four font colours (border width 4 = hover, 5 = locked) — the tile look is code, not theme; a pulp card rewrites it. Container sorting resets a child's rotation/scale (`MenuSafeAreaFit.cs:15-19`), so any card tilt goes on an inner child.
- READY: `P1Ready` / `P2Ready` Labels (`fighter_ready_banner` "READY!"); countdown `CountdownLabel` (3 s, `:64`). Join prompts `P1JoinPrompt` / `P2JoinPrompt` inside `P1Panel` / `P2Panel` (`RefreshPanels` `:788-800`), pinned by `PadMenuFighterSelectTests`. Stats: `P1Stats` / `P2Stats` from `StatsText()` (`:960`, format `fighter_stats_summary`, nine fields — too dense for a card).
- Stage phase: `StagePhase` CenterContainer (`StageSelect` OptionButton, `StagePreview`, `MatchMode`, `StockCount`, `TimeLimit` SpinBox, `ItemFrequency`, `MeterPickups`, `StageHazards`, Back/Fight); theme restyle covers it except the Fight button's gold override (`.tscn:362`). Random stage via `RandomStageItemID` 1000; Random fighter via `FighterRandomPick.Index(seed, salt, len)` (`:1064`); `AddRandomSilhouette` is a placeholder "?".
- Overrides: 30 `theme_override` lines in the scene, 19 sites in code; title 42 px override.
- Tests: `CharacterSelectSceneTests` (`TheAuthoredSceneCarriesEveryControlTheScriptBindsByPath` incl. `PlayersRow/Grid/RandomTile` `:51`; tiles cursor-driven and outside the focus chain; per-phase focus chains; the Remote Play notice; join and pad tests; raw translation keys), `PadMenuFighterSelectTests`. Keep the `Grid` path, `RandomTile`, `CharacterButton{n}` names, the skip group and `FocusMode.None`.

## 3. Trading-card data (`scripts/Characters/CharacterData.cs`)
- Available: `DisplayNameKey` (`character_einstein_name`), `CharacterPortrait` (512 × 512), `SpriteFramesResource`, `Style`, `MaxHP`, `Weight`, `MaxBlockCharges`, `MaxMoveSpeed`, `MaxJumpForce`, `MaxJumpCount`, `BasicAttackDamage`, `BasicAttackKnockback`, `Placeholder{Body,Accent,Detail}Color`.
- Localized hero data (`localization/en.csv:1313-1348`): `hero_name_*`, `hero_address_*`, `hero_possessive_*`, `hero_home_era_*` (`hero_name_tubman` at `:1643`; `hero_home_era_tubman` unchecked).
- **Life dates: none** in `en.csv` or `design-godot.md`. Best fit: a localized key family `character_<id>_dates` (a locale may format dates or the separator; `.tres` holds tuning, not copy), satisfying the key tests and `--import`. Values are agent-authored general knowledge (e.g. Tubman c. 1822-1913; Cleopatra 69-30 BC and Joan c. 1412-1431 are approximate). A reduced card stat set (HP, Speed, Weight, Style) is a new format key.

## 4. Pause
- Story: `scenes/ui/StoryPause.tscn`, `PauseMenu.cs` (564), `PauseMenuBase.cs` (200). Already a centred panel (`Root/Center/Panel`, `DialogueGlassPanel`; `Title` `TitleLabel`; `ControllerNotice`; rows Resume, Settings, MoveList, Log, Save, Restart, LowerDifficulty (hidden), Quit). **Dual build path:** the authored scene, else `BuildFallbackUI` (`PauseMenu.cs:229`) with the same node names; `StoryPauseTests` pins `Root/Center/Panel/Layout/{Resume,Settings,Save,Restart,Quit}Button`, `Title` = `menu_paused`, and `TheTitleUsesTheTitleVariationWithNoFontSizeOverrideOnBothBuildPaths` — change both paths together.
- Status line: no node; data available — `StoryManager.CurrentLevel` (`:72`), `LevelElapsedSeconds` (`:89`), difficulty via `CampaignDifficultyService.DifficultyNameKey`, Integrity in `StoryAttemptState.CurrentIntegrity` (`:276`) / `SaveManager.LevelIntegrityPercent`; level names via `LoadingScreen.LevelTitleKeyForScene` or `campaign_level_*`. Needs new localized format text.
- Fighter: `scenes/ui/LocalFighterPause.tscn` (script only); the panel is code-built (`LocalFighterPause.cs:160-225`: Resume, Settings, MoveListP1, MoveListP2, Exit, `DisconnectModal`), `MenuSafeAreaFit.Attach`, 11 palette sites, 4 overrides; `LocalFighterPauseTests.TitleAndDisconnectLabelUseThemeVariationsWithNoFontSizeOverride`. Pad pins: `PadMenuConfirmCancelTests`, `PauseFocusLossTests`.

## 5. Other screens (restyle reach)
| Screen | Notes | Theme-only? |
|---|---|---|
| Settings (`Settings.tscn`, `SettingsMenu.cs`) | `DialogueGlassPanel` + tabs; 20 scene overrides (40 px title, gold/grey labels); `SettingsMenuSceneTests` pins 4 tabs | Mostly; override cleanup |
| Move List (`MoveList.tscn`, `MoveListScreen.cs`) | 6 scene overrides; 7 palette + 4 override sites in code | Partly |
| Systems Card (`SystemsCard.tscn`) | 11 scene overrides; `SystemsCardContentTests` pins no font-size overrides | Mostly |
| Dialogue box (`DialogueBox.tscn`, `DialogueManager.cs`) | `DialogueGlassPanel`; cyan speaker override `:53`; `DialogueTheme.cs` palette copy | Partly |
| Dialogue Log (`DialogueLogScreen.cs`) | code-built: 13 controls, 9 overrides, 9 palette sites | No |
| Level Results (`LevelResults.tscn`, `LevelResultsPanel.cs`) | 15 scene overrides | Partly |
| Match Results (`MatchResults.tscn`, `MatchResults.cs`) | 7 controls, 5 overrides, 11 palette sites | No |
| Loading (`LoadingScreen` / `StoryLoading` / `FighterLoading.tscn`, `LoadingScreen.cs`) | 3 / 7 / 8 overrides; 3 palette sites | Partly |
| Game Over (`GameOver.tscn`) | 3 overrides | Mostly |
| Credits (`Credits.tscn`, `CreditsController.cs`) | 2 scene + 3 code overrides | Mostly |
| Resonance grid (`ResonanceGrid.tscn`, `ResonanceGridPanel.cs`) | 5 scene overrides; solid / dotted / glow edges are semantic colour | Partly; with care |
| Holodeck console (`HolodeckConsolePanel.cs`) | code-built: 11 controls, 11 palette sites | No |
| Calibration picker / list / HUD (`CalibrationDrill*.tscn`, `HolodeckDrill.tscn`) | 9 / 6 / 7 scene overrides; `MenuSafeAreaFit` on the list | Partly |
| `ConfirmModal` (+ `TwoOptionPrompt`) | 9 controls, 8 palette sites; `ConfirmModalTests` (no font-size override) | No (one change point) |
| First-use tooltip (`FirstUseTooltipPresenter.cs`) | 1 variation, 1 palette site; allow-listed | Mostly |
| Boot overlay (`BootSequenceOverlay.cs`) | 10 controls, 19 palette sites | No |
| Timeline restart (`TimelineRestartPanel.cs`) | 5 controls, `MenuSafeAreaFit`; `TimelineRestartPanelTests` | Partly |
| Fighter play options (`FighterPlayOptions.tscn`, `FighterPlayOptionsScreen.cs`) | 1 scene override | Yes |
| Network select (`NetworkSelect.tscn`, `NetworkSelectScreen.cs`) | code-built: 6 controls; de-scoped | Low priority |
| Hub (`HubWorldController.cs`) | 28 `AddThemeColorOverride` on world-space labels (portal, repository, holodeck, NPC) | Out of scope unless restyled |
| Disconnect modal (`LocalFighterPause.BuildDisconnectModal`) | hard-coded warning colour and ColorRect | No |

## 6. Hard-coded styling counts
Script lines matching `AddThemeColorOverride|AddThemeFontSizeOverride|AddThemeStyleboxOverride|Modulate|UIPalette.` (upper bound): StoryHUD.cs 33, HubWorldController.cs 28, FighterHUD.cs 22, CharacterSelectScreen.cs 19, BootSequenceOverlay.cs 19, MatchResults.cs 12, LocalFighterPause.cs 11, HolodeckConsolePanel.cs 11, DialogueManager.cs 10, PauseMenu.cs 8. Scene `theme_override_*` lines: MainMenu 79, StoryHUD 47, FighterHUD 42, CharacterSelect 30, Settings 20, LevelResults 15, SystemsCard 11. UI-scale pins forbid `AddThemeFontSizeOverride` / scene `font_size` overrides on pinned screens; use theme variations.

## 7. Retro v2 sprites in menus
No precedent (`scripts/UI/` uses no `AnimatedSprite2D`, `SubViewport` or `AtlasTexture`; menus use `CharacterData.CharacterPortrait` in a `TextureRect`). Access: `AuthoredResources.Load<CharacterData>("res://resources/Characters/<id>_data.tres")` → `SpriteFramesResource.GetFrameTexture("idle", i)` (an `AtlasTexture` region of the 2x atlas `assets/sprites/characters/<id>/retro/*_locomotion_atlas.png`; idle has 4 frames; metadata `retro_texel_scale` 2.0, `retro_baked_scale`, `retro_feet_gaps` via `FTT.Combat.RetroSpriteMetadata`). A `TextureRect` swapped by a `Timer` is container-friendly; `AnimatedSprite2D` in a Control needs manual pivots. **Each character's atlases are 41-51 MiB uncompressed**, so three menu heroes load three atlases early — prefer baked static pose PNGs, or load asynchronously. Never `ResourceLoader.Load` a scripted `.tres` (crash signature 2).

## Top risks
1. Font and palette assets and rulings (glyph coverage; gold vs the grammar).
2. Dual build paths (`PauseMenu` scene + fallback; code-built `LocalFighterPause`, `HolodeckConsolePanel`): change both in step (`…OnBothBuildPaths` pins).
3. Tile styling is code (`StyleTile`); tilt goes on an inner child.
4. `MenuLayoutBoundsTests` at UI scale 1.0 and 1.4: a new cover layout needs centring or an allow-list entry; skewed buttons must fit at 1.4.
5. Focus tilt vs hit-testing; two interaction models (focus-driven root buttons vs cursor-driven fighter tiles).
6. Localization of all new copy (life dates, status line, Continue sticker, stat card, READY bubble); keep `game_title` raw.
7. `MainMenuSceneTests`' Story grid count must not gain a Random tile.
8. Sprite memory (§7).
