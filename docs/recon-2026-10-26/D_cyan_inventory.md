# D. The chrome-cyan inventory (2026-10-26)

Produced by UR-THEME (Phase A of the pulp UI redesign) from a scan of `scripts/` and `scenes/` at the UR-THEME branch point, **after** UR-THEME re-pointed `UIPalette.TextAccent` to the gold accent. Evidence, not authority; re-run the scan (`D:/Projects/ftt-scratch/cyan_scan.py` is the throwaway; the Phase C guard test is the lasting form) before sweeping. 51 files, 136 sites.

## Reading this

- **Chrome cyan** is cyan used as the UI accent (titles, focus, ready states, panel borders). The redesign moves chrome to the gold accent (`UIPalette.Accent`, #F3C95B).
- Cyan stays in exactly three roles: the **Player-1 ownership colour** (`UIPalette.P1`, read from `GlowPalette.SlotColor(0)`), the **Severed's cold light** (`UIPalette.UnboundCold*`, grammar), and **world-space effects** that are not UI.
- `UIPalette.TextAccent`, `FocusOutline`, `PanelBorder` and the `Button*` mirrors were **re-pointed by UR-THEME** (gold / paper / plate values), so every site that reads them already follows the new palette. `UIPalette.Cyan` and `CyanDim` keep their **old values** (they are still the P1 / legacy colours); each `Cyan` / `CyanDim` read below must be re-pointed by its owner.
- A raw literal in a `.tscn` (`theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)`) is an override: delete it and let the theme or a type variation supply the colour (a per-node override freezes the label out of the theme and out of the UI scale).
- Replacement vocabulary: **accent** = `UIPalette.Accent` / theme gold; **P1** = `UIPalette.P1`; **cold** = `UIPalette.UnboundCold`; **leave** = not chrome.

## Summary by Phase B owner

| Owner | Files | Sites |
|---|---:|---:|
| UR-HUDF | 4 | 7 |
| UR-HUDS | 3 | 10 |
| UR-MENU | 1 | 6 |
| UR-PAUSE | 11 | 24 |
| UR-SCREENS | 16 | 30 |
| UR-SELECT | 1 | 2 |
| Unassigned (world-space, leave) | 15 | 57 |

`TextAccent` readers (title / heading overrides) are counted here too; they need no recolouring after UR-THEME, only the move to type variations when their screen is rebuilt.

## UR-HUDF

### `scenes/ui/FighterHUD.tscn`

Replacement: **P1 slot colour / accent**. Line 85 and 328: decide per node. A player-identity element (name tag, star, stock) reads `UIPalette.P1` / `P2` (the slot palette); a cooldown or meter label is `Accent` / `Res2`.

| Line | Site | Text |
|---:|---|---|
| 85 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 328 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scripts/UI/FighterHUD.cs`

Replacement: **P1 slot colour / accent**. Line 228 shield pips: `UIPalette.Shield`. Lines 701 / 703 cooldown ready colour: `Res2` / `Accent`, not P1. `FighterHudModelTests` pins `Cyan` for the HP fill (lines 74-75): re-point to `UIPalette.Hp` with the HUD.

| Line | Site | Text |
|---:|---|---|
| 228 | UIPalette.Cyan | `panel.Shields = RebuildPips(panel.ShieldPips, Mathf.Max(1, maxShieldCharges), UIPalette.Cyan);` |
| 701 | UIPalette.Cyan | `icon.AddThemeColorOverride("font_color", ready ? UIPalette.Cyan : UIPalette.SlateDim);` |
| 703 | UIPalette.Cyan | `if (radial != null) radial.FillColor = ready ? UIPalette.Cyan : UIPalette.SlateDim;` |

### `scripts/UI/FighterHudModel.cs`

Replacement: **Hp**. Line 132 HP fill colour: `UIPalette.Hp`. Pinned by `FighterHudModelTests`.

| Line | Site | Text |
|---:|---|---|
| 132 | UIPalette.Cyan | `return UIPalette.Cyan;` |

### `scripts/UI/FighterOverlayModel.cs`

Replacement: **P1 slot colour**. Line 111: the P1 banner colour is player identity: `UIPalette.P1` (the slot colour), never the accent. `FighterMatchPresentationTests` pins `Cyan` at line 34.

| Line | Site | Text |
|---:|---|---|
| 111 | UIPalette.Cyan | `UIPalette.Cyan,` |

## UR-HUDS

### `scenes/ui/StoryHUD.tscn`

Replacement: **accent / Res2**. Line 52 and 530: readiness text and the sealed-dial note use `Accent` / `Res2` (HUD tokens), never cyan.

| Line | Site | Text |
|---:|---|---|
| 52 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 530 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scripts/UI/RadialProgress.cs`

Replacement: **Res2 / Accent**. Default fill: `Res2`. Callers set their own colours (HUD owners).

| Line | Site | Text |
|---:|---|---|
| 25 | UIPalette.Cyan | `private Color _fillColor = UIPalette.Cyan;` |

### `scripts/UI/StoryHUD.cs`

Replacement: **Res2 / Accent / Shield**. Line 431 and 777: generic ready colour: `Res2`. 481 / 483 Time Freeze: `Accent2` (rewind / freeze tint). 821 / 828 slot readiness: `Res2` / `Accent`. 895 block pips: `UIPalette.Shield`.

| Line | Site | Text |
|---:|---|---|
| 431 | UIPalette.Cyan | `return UIPalette.Cyan;` |
| 481 | UIPalette.Cyan | `TimeFreezeState.Active => thawWarning ? UIPalette.Warning : UIPalette.Cyan,` |
| 483 | UIPalette.Cyan | `_ => UIPalette.Cyan` |
| 777 | UIPalette.Cyan | `: UIPalette.Cyan;` |
| 821 | UIPalette.Cyan | `? UIPalette.Cyan` |
| 828 | UIPalette.TextAccent | `_indicators.IsReady(slot) ? UIPalette.TextAccent : UIPalette.TextDisabled);` |
| 895 | UIPalette.Cyan | `_blockPips = RebuildPipRow(_blockPipRow, Mathf.Max(1, capacity), BlockPipSize, UIPalette.Cyan);` |

## UR-MENU

### `scenes/menus/MainMenu.tscn`

Replacement: **accent (via theme)**. Button and title `font_color` overrides: drop them; the pulp button's colours come from the theme. The title becomes the `CoverTitle` role.

| Line | Site | Text |
|---:|---|---|
| 336 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 424 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 512 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 567 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 716 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 768 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

## UR-PAUSE

### `scenes/ui/DialogueBox.tscn`

Replacement: **accent (speaker)**. The speaker name: the `SpeakerName` role (Cinzel 900, accent).

| Line | Site | Text |
|---:|---|---|
| 53 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/MoveList.tscn`

Replacement: **accent (via theme)**. Heading override: `HeadingLabel`.

| Line | Site | Text |
|---:|---|---|
| 58 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/SystemsCard.tscn`

Replacement: **accent (via theme)**. Nine section titles: `HeadingLabel` (the card is re-skinned by UR-PAUSE; the overrides go).

| Line | Site | Text |
|---:|---|---|
| 44 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 60 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 72 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 84 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 96 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 108 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 120 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 132 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 144 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scripts/UI/BootSequenceOverlay.cs`

Replacement: **accent**. `TextAccent` is already the accent; once `TextAccent` = gold (done by UR-THEME) these sites need no code change. Check the pulsing swatch (Gold lerp) still reads.

| Line | Site | Text |
|---:|---|---|
| 228 | UIPalette.TextAccent | `Color = UIPalette.TextAccent,` |
| 274 | UIPalette.TextAccent | `_previewSwatch.Color = UIPalette.TextAccent;` |
| 278 | UIPalette.TextAccent | `_previewSwatch.Color = UIPalette.TextAccent.Lerp(UIPalette.Gold, pulse);` |
| 415 | UIPalette.TextAccent | `if (accent) label.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/ConfirmModal.cs`

Replacement: **accent**. Modal title: `TextAccent` is gold now; prefer the `HeadingLabel` variation and drop the override.

| Line | Site | Text |
|---:|---|---|
| 100 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/DialogueEmotionTreatment.cs`

Replacement: **accent / P1**. Urgent frame colour and the calm frame (`CyanDim`): `Accent` and `Ink3`. `DialogueEmotionTreatmentTests` pins `Cyan` at line 67; update it with the change.

| Line | Site | Text |
|---:|---|---|
| 72 | UIPalette.Cyan | `emotion, UIPalette.Cyan, new Color(0.9f, 1f, 1f), UrgentFrameWidth),` |
| 78 | UIPalette.CyanDim | `DialogueEmotion.Neutral, UIPalette.CyanDim, Colors.White, CalmFrameWidth)` |

### `scripts/UI/DialogueLogScreen.cs`

Replacement: **accent**. Title and speaker: `TextAccent` (gold already); prefer variations.

| Line | Site | Text |
|---:|---|---|
| 79 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |
| 171 | UIPalette.TextAccent | `speaker.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/DialogueManager.cs`

Replacement: **accent**. Speaker label: use the `SpeakerName` role.

| Line | Site | Text |
|---:|---|---|
| 229 | UIPalette.TextAccent | `_speakerLabel.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/LocalFighterPause.cs`

Replacement: **accent**. Pause title: the `PauseTitle` role.

| Line | Site | Text |
|---:|---|---|
| 194 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/MoveListScreen.cs`

Replacement: **accent**. Title: `HeadingLabel`.

| Line | Site | Text |
|---:|---|---|
| 261 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/PauseMenu.cs`

Replacement: **accent**. Pause title: the `PauseTitle` role.

| Line | Site | Text |
|---:|---|---|
| 266 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

## UR-SCREENS

### `scenes/ui/CalibrationDrillList.tscn`

Replacement: **accent (via theme)**. Heading overrides: use `HeadingLabel` / `TitleLabel` and drop the override.

| Line | Site | Text |
|---:|---|---|
| 70 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/CalibrationDrillPicker.tscn`

Replacement: **accent (via theme)**. Heading overrides: use `HeadingLabel` / `TitleLabel` and drop the override.

| Line | Site | Text |
|---:|---|---|
| 135 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/FighterLoading.tscn`

Replacement: **accent (via theme)**. Title override: `TitleLabel`.

| Line | Site | Text |
|---:|---|---|
| 79 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/HolodeckDrill.tscn`

Replacement: **accent / Ink3**. Panel tints and HUD lines: `Accent` for the live drill state, `Ink3` for dividers; the pale cyan rules become paper-ink rules.

| Line | Site | Text |
|---:|---|---|
| 34 | literal 0,0.55,0.6 | `color = Color(0, 0.55, 0.6, 0.35)` |
| 42 | literal 0,0.55,0.6 | `color = Color(0, 0.55, 0.6, 0.35)` |
| 72 | literal 0,0.9,0.9 | `color = Color(0, 0.9, 0.9, 0.7)` |
| 79 | literal 0,0.6,0.66 | `color = Color(0, 0.6, 0.66, 0.85)` |
| 86 | literal 0,0.6,0.66 | `color = Color(0, 0.6, 0.66, 0.85)` |
| 136 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/LevelResults.tscn`

Replacement: **accent (via theme)**. Heading override: `TitleLabel`.

| Line | Site | Text |
|---:|---|---|
| 38 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/LoadingScreen.tscn`

Replacement: **accent (via theme)**. Status label override: drop it (theme text).

| Line | Site | Text |
|---:|---|---|
| 42 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/ResonanceGrid.tscn`

Replacement: **accent (via theme)**. Title override: `TitleLabel`. The grid's edge and node colours are semantic (UR-SCREENS keeps them).

| Line | Site | Text |
|---:|---|---|
| 42 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scenes/ui/StoryLoading.tscn`

Replacement: **accent; Severed cold for the rift**. The portal ring (line 66) is the Warden portal: `UIPalette.WardenPortal` (warm), not cyan. Title override: `TitleLabel`.

| Line | Site | Text |
|---:|---|---|
| 55 | literal 0,0.55,0.6 | `color = Color(0, 0.55, 0.6, 0.35)` |
| 66 | literal 0,0.9,0.9 | `color = Color(0, 0.9, 0.9, 1)` |
| 78 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

### `scripts/Combat/CalibrationDrillRunner.cs`

Replacement: **accent**. Pass colour: `Res2` (success) over `Warning`; not cyan.

| Line | Site | Text |
|---:|---|---|
| 330 | UIPalette.Cyan | `"font_color", passed ? FTT.UI.UIPalette.Cyan : FTT.UI.UIPalette.Warning);` |

### `scripts/Environment/HubWorldController.cs`

Replacement: **accent / leave world-space**. Portal visual and floor line are world-space (leave). The portal label, ship title, dust and ledger labels are chrome: `Accent` / `Res2` or a theme variation; the NPC label teal is a speaker tint (leave or `Ink2`).

| Line | Site | Text |
|---:|---|---|
| 257 | literal 0.0,0.7,0.8 | `floorLine.Color = new Color(0.0f, 0.7f, 0.8f, 0.6f);` |
| 285 | literal 0.0,0.6,0.7 | `glow.Color = new Color(0.0f, 0.6f, 0.7f, 0.4f);` |
| 332 | literal 0.0,0.9,0.95 | `portalVisual.Color = new Color(0.0f, 0.9f, 0.95f, 0.6f);` |
| 348 | literal 0.0,0.9,0.9 | `portalLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.9f, 0.9f));` |
| 490 | literal 0.2,0.85,0.65 | `npcLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.85f, 0.65f));` |
| 528 | literal 0.0,0.8,0.9 | `shipTitle.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f, 0.8f));` |
| 582 | literal 0.0,0.8,0.9 | `_dustLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));` |
| 593 | literal 0.0,0.8,0.9 | `_repositoryLedgerLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));` |

### `scripts/UI/CreditsController.cs`

Replacement: **accent**. Heading lines: `UIPalette.Accent` (or the `TitleLabel` variation).

| Line | Site | Text |
|---:|---|---|
| 161 | UIPalette.Cyan | `"font_color", line.IsHeading ? UIPalette.Cyan : UIPalette.TextPrimary);` |

### `scripts/UI/LevelResultsPanel.cs`

Replacement: **accent**. Restored-resonance line: `Res2`.

| Line | Site | Text |
|---:|---|---|
| 269 | UIPalette.TextAccent | `_resonanceRestoredLabel.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/LoadingScreen.cs`

Replacement: **accent**. Fallback status label: `TextAccent` (gold now); fine.

| Line | Site | Text |
|---:|---|---|
| 176 | UIPalette.TextAccent | `status.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/MatchResults.cs`

Replacement: **accent**. Title: `TitleLabel`.

| Line | Site | Text |
|---:|---|---|
| 191 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

### `scripts/UI/ResonanceGridPanel.cs`

Replacement: **accent**. Selection border: `Accent` (the node semantic colours stay).

| Line | Site | Text |
|---:|---|---|
| 79 | UIPalette.Cyan | `BorderColor = UIPalette.Cyan` |

### `scripts/UI/TimelineRestartPanel.cs`

Replacement: **accent**. Panel title: `TitleLabel`.

| Line | Site | Text |
|---:|---|---|
| 67 | UIPalette.TextAccent | `title.AddThemeColorOverride("font_color", UIPalette.TextAccent);` |

## UR-SELECT

### `scenes/menus/CharacterSelect.tscn`

Replacement: **accent (via theme)**. Title / heading `font_color` overrides: drop the override and use the Cinzel `TitleLabel` / `HeadingLabel` variation (gold). A P1 slot label is the exception: `UIPalette.P1`.

| Line | Site | Text |
|---:|---|---|
| 72 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |
| 94 | literal 0,0.9,0.9 | `theme_override_colors/font_color = Color(0, 0.9, 0.9, 1)` |

## Unassigned (world-space, leave)

### `scenes/fighter/FighterStage_Chicago.tscn`

Replacement: **world-space; leave**. Stage scenery and hazard colours (era presentation). Not chrome: leave. They are the Severed-cold family or stage palette, outside the UI redesign.

| Line | Site | Text |
|---:|---|---|
| 90 | literal 0.2,0.9,1 | `default_color = Color(0.2, 0.9, 1, 0.4)` |
| 111 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.85)` |
| 124 | literal 0.2,0.9,1 | `default_color = Color(0.2, 0.9, 1, 0.4)` |
| 145 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.85)` |
| 154 | literal 0.2,0.9,1 | `default_color = Color(0.2, 0.9, 1, 0.55)` |
| 159 | literal 0.2,0.9,1 | `default_color = Color(0.2, 0.9, 1, 0.45)` |
| 166 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.07)` |
| 173 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.22)` |
| 227 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 1)` |
| 232 | literal 0.2,0.9,1 | `default_color = Color(0.2, 0.9, 1, 0.2)` |
| 254 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.5)` |
| 276 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.5)` |
| 299 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 1)` |
| 322 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 1)` |

### `scenes/fighter/FighterStage_Nassau.tscn`

Replacement: **world-space; leave**. Stage scenery and hazard colours (era presentation). Not chrome: leave. They are the Severed-cold family or stage palette, outside the UI redesign.

| Line | Site | Text |
|---:|---|---|
| 75 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |
| 175 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.75)` |
| 181 | literal 0.12,0.72,0.86 | `default_color = Color(0.12, 0.72, 0.86, 0.6)` |
| 277 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.14)` |
| 284 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 291 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 298 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.05)` |
| 308 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.14)` |
| 315 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 322 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 329 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.05)` |
| 339 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.14)` |
| 346 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 353 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.5)` |
| 360 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.05)` |
| 368 | literal 0.2,0.76,0.9 | `FillColor = Color(0.2, 0.76, 0.9, 1)` |
| 414 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |
| 421 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 0.95)` |
| 447 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |
| 469 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |
| 492 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |
| 527 | literal 0.12,0.72,0.86 | `color = Color(0.12, 0.72, 0.86, 1)` |

### `scenes/templates/BeamEmitterTemplate.tscn`

Replacement: **world-space; leave**. Puzzle and beam templates (Severed cold or puzzle teal). Not chrome: leave.

| Line | Site | Text |
|---:|---|---|
| 18 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.75)` |

### `scenes/templates/ChronalExtractorTemplate.tscn`

Replacement: **world-space; leave**. Puzzle and beam templates (Severed cold or puzzle teal). Not chrome: leave.

| Line | Site | Text |
|---:|---|---|
| 32 | literal 0,0.9,0.9 | `color = Color(0, 0.9, 0.9, 0.55)` |

### `scenes/templates/ChronalRiftZoneTemplate.tscn`

Replacement: **world-space; leave**. Puzzle and beam templates (Severed cold or puzzle teal). Not chrome: leave.

| Line | Site | Text |
|---:|---|---|
| 19 | literal 0.2,0.86,1 | `color = Color(0.2, 0.86, 1, 0.32)` |

### `scenes/templates/ConductiveCoilTemplate.tscn`

Replacement: **world-space; leave**. Puzzle and beam templates (Severed cold or puzzle teal). Not chrome: leave.

| Line | Site | Text |
|---:|---|---|
| 19 | literal 0.2,0.9,1 | `color = Color(0.2, 0.9, 1, 0.4)` |

### `scenes/templates/PressurePlateTemplate.tscn`

Replacement: **world-space; leave**. Puzzle and beam templates (Severed cold or puzzle teal). Not chrome: leave.

| Line | Site | Text |
|---:|---|---|
| 18 | literal 0.18,0.72,0.8 | `color = Color(0.18, 0.72, 0.8, 1)` |

### `scripts/Combat/GlowStateModel.cs`

Replacement: **leave (it is the ownership source)**. The P1 slot colour #00f0ff. `UIPalette.P1` reads this; keep.

| Line | Site | Text |
|---:|---|---|
| 176 | literal 0,0.941,1 | `public static readonly Color SpawnInvulnerabilityColor = new(0f, 0.941f, 1f); // #00f0ff` |
| 193 | literal 0,0.941,1 | `new(0f, 0.941f, 1f),      // P1 #00f0ff` |

### `scripts/Environment/Level00Controller.cs`

Replacement: **leave (world-space)**. Fracture presentation, grid lines and the calibration title: world-space tutorial dressing. The title overlay (line 1732) is chrome: `Accent`.

| Line | Site | Text |
|---:|---|---|
| 1210 | literal 0,0.9,0.9 | `Color = new Color(0f, 0.9f, 0.9f, 0.5f),` |
| 1668 | literal 0.0,0.6,0.8 | `gridLine.Color = new Color(0.0f, 0.6f, 0.8f, 0.4f);` |
| 1696 | literal 0.0,0.7,0.9 | `glow.Color = new Color(0.0f, 0.7f, 0.9f, 0.3f);` |
| 1732 | literal 0.0,0.8,0.9 | `title.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f, 0.7f));` |

### `scripts/Environment/Level01Controller.cs`

Replacement: **leave (world-space)**. World-space sign and visual.

| Line | Site | Text |
|---:|---|---|
| 749 | literal 0.0,0.7,0.9 | `visual.Color = new Color(0.0f, 0.7f, 0.9f, 0.6f);` |
| 758 | literal 0.0,0.8,0.9 | `label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));` |

### `scripts/Environment/Level14Controller.cs`

Replacement: **leave (Severed cold)**. Security Core floor edge and a notice tint: the Severed's cold family. Keep, or use `UIPalette.UnboundCold`.

| Line | Site | Text |
|---:|---|---|
| 517 | literal 0.24,0.86,0.94 | `protected override Color FloorEdgeColor => new(0.24f, 0.86f, 0.94f);` |
| 897 | literal 0.24,0.86,0.94 | `EnvironmentNotice.Post(HoldRevealNoticeKey, Player, 5f, new Color(0.24f, 0.86f, 0.94f));` |

### `scripts/Environment/ResonanceHoldNode.cs`

Replacement: **leave (Severed cold)**. Cradle valve stand-in light: Severed family.

| Line | Site | Text |
|---:|---|---|
| 104 | literal 0.24,0.86,0.94 | `ResonanceHoldVariant.CradleIntakeValve => new Color(0.24f, 0.86f, 0.94f, 0.6f),` |

### `scripts/Environment/StoryLevelControllerBase.cs`

Replacement: **leave (world-space)**. World-space sign visual and its label; the label (line 1256) could take `Accent`.

| Line | Site | Text |
|---:|---|---|
| 1243 | literal 0.0,0.7,0.9 | `Color = new Color(0.0f, 0.7f, 0.9f, 0.6f),` |
| 1256 | literal 0.0,0.8,0.9 | `label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));` |

### `scripts/UI/ResonanceEdgeLayer.cs`

Replacement: **leave (semantic edge)**. A satisfied edge in the grid: semantic colour UR-SCREENS keeps. Teal, not the chrome cyan.

| Line | Site | Text |
|---:|---|---|
| 29 | literal 0.30,0.95,0.92 | `private static readonly Color SatisfiedColor = new(0.30f, 0.95f, 0.92f, 0.95f);` |

### `scripts/UI/RewindPresentationOverlay.cs`

Replacement: **leave (world-space tint)**. Rewind screen tint: presentation of the rewind, not chrome. Leave.

| Line | Site | Text |
|---:|---|---|
| 35 | literal 0,0.85,0.9 | `_tint = new ColorRect { Name = "Tint", Color = new Color(0f, 0.85f, 0.9f, 0.18f) };` |
| 45 | literal 0,0.9,0.95 | `Color = new Color(0f, 0.9f, 0.95f, 0.08f),` |

## Not found by the scan (check by hand)

- Colours built from components (`new Color(r, g, b)` with a variable), `Colors.Cyan`, and hex strings (`Color.FromHtml`) are not matched.
- Shaders and `.tres` resources (`resources/**`) were not scanned; the dialogue emotion frames and ability presentations carry their own palettes.
- `UIPalette.Cyan` readers outside `scripts/UI/` that the scan listed above are the complete named-constant set at this commit.

## Guard (Phase C)

A source-scan test should ban `UIPalette.Cyan`, `UIPalette.CyanDim` and cyan-family literals in `scripts/UI/` and `scenes/ui/` / `scenes/menus/` except `UIPalette.cs` and the Severed / ownership constants. Sites listed as *leave* above live outside those folders.

