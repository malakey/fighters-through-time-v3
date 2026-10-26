# UR-PAUSE handoff (pulp UI redesign, Phase B)

Branch `ur/pause`, cut from `main` at `22219329`. Restyle only: `PauseMenuBase`'s pause ownership, the pad B / `ui_cancel` rules, the dialogue's hold-to-skip and every "whoever pauses hands it back" path are untouched.

## 1. What shipped

New shared pieces (mine, `scripts/UI/`):
- **`PausePanel`** (`PanelContainer`, `InkPanel`): the PAUSE-A panel. Pause padding 30 / 20 / 28 inside the 3 px border (a duplicated plate with its own content margins), the `HalftoneCorner` behind its content, and the level tag (`PanelTagPlate` + upper-case `PanelTag` label, -1.2 degrees, at the padding box's (-14, -24)) laid out of flow by an override of the sort. `TagText` takes already-resolved text; empty hides the tag. It is a `PanelContainer` rather than a `PulpTaggedPanel` so the pinned paths `Root/Center/Panel/Layout/...` do not gain a node.
- **`PulpScreenKit`**: `MakeScrim()` (the `ScrimPanel` plate at self-alpha 0.7), `StylePauseButton(button, width = 560)` (the `TemporalGlassButton` 64 px plate, a 64 px label inset so the label clears the marker, then `PulpButtonDecor`), `AddBottomPromptBar` (A SELECT, B <caption>, bottom centre, 34 px margin), `ShortTitle` (the level name before " — "), `FormatClock`.
- **`UiCaptureTargets.Pause.cs`**: the 17 capture targets.

Surfaces:
- **Story pause** (`StoryPause.tscn` + `PauseMenu.BuildFallbackUI`, identical node names): scrim, centred `PausePanel` tagged with the level (`HubWorldController.CampaignLevelNameKey`, first segment; the ship's title at the hub), `PauseTitle` role, a `StatusLine` label (`pause_status_level`: difficulty, level clock, Integrity; the hub shows the difficulty alone; upper-case), eight 560 x 64 decorated rows and the prompt bar (`Root/PromptBar`). A shared `DecorateForPulp()` skins both build paths, so the two cannot drift. `ControllerNotice` and the confirmations are kept.
- **Fighter pause and disconnect modal**: the same language (tag `menu_fighter_mode`, `Backdrop` control holding the scrim and the prompt bar, `MenuSafeAreaFit` kept). The disconnect modal is a scrim plus a tagged `DisconnectPanel` (`pause_tag_controller`) with the warning message and two banner rows.
- **Settings**: scene on `InkPanel` + `PausePanel`, no art backdrop, `TitleLabel` title, `SmallLabel` / `HintLine` for the hint and status (the colour and size overrides are gone; the 16 left are spacing constants), sliders 360 x 44 and not stretched, every `CheckButton` (scene and the code-built shortcut rows) a `PulpToggleDecor` 560 wide, Back a 320 x 64 banner row, remap buttons 42 high. Tabs are the theme's.
- **Move List / Systems Card**: scrim, `InkPanel` + `PausePanel` (the Move List's tag reads "Move List"), cyan overrides stripped (section titles ride `HeadingLabel`, character name `TitleLabel`), banner footer rows; code-built ability stats use `Accent` / `Ink3`.
- **Confirm modal / two-option prompt**: scrim, untagged `PausePanel`, `HeadingLabel` title with no colour override, two `PulpButtonCompact` rows (300 x 58, compact decor). `TwoOptionPrompt` inherits it (no change).
- **Dialogue box**: 1500 wide, centred (`DialogueTheme.BoxWidth`), `SpeakerName` role (cyan override gone), a 150 px `PulpPortraitRoundel` inside the emotion ring, `ContinueHint` kept (it names the live bound key) beside a 34 px `hourglass` caret in `Res2` at right 26 / bottom 18 that shows with the hint. The emotion frame is now a **ring** (a transparent circle `StyleBoxFlat` that only carries colour and width); emotion colours are `Accent` (shocked), `Accent2` (determined), `Ink2` (confused), `BossRed` (injured), `Ink3` (neutral). The old square `Portrait` `TextureRect` stays in the frame, hidden, as the state carrier (texture, tint, tooltip); the roundel mirrors its texture and tint. `DialogueTheme` lost its border, shadow, radius and portrait-background constants (art bakes them).
- **Dialogue log**: scrim, `PausePanel`, `TitleLabel` title, 64 px roundels, `SpeakerName` speakers, banner Back row.
- **First-use tooltip**: `InkPanel` with a `HintLine` label (still top-centre; allow-listed left aligned in `MenuLayoutBoundsTests`).
- **Boot overlay**: `Backdrop` colour, `PausePanel` 1000 wide, `PulpButtonCompact` rows, a `PulpToggleDecor` on the Reduced Temporal Effects switch, the preview swatch pulsing `Accent` to `Accent2`, no accent overrides.

Localization (block `# UR PAUSE`, end of `en.csv`): `pause_status_level` = `{0} · {1} · Integrity {2}%`, `pause_tag_controller` = `Controller`. `--import` was run locally; `en.en.translation` is not committed.

## 2. Tests

`[TestCase]` delta from 3602: **+10**, all in new files under `tests/Pause/` (one suite per file): `PulpScreenKitTests` 2 (pure), `PausePanelTests` 1, `PauseSkinTests` 2 (Story pause on both build paths; Fighter pause and disconnect modal), `OverlaySkinTests` 5 (Settings toggles and overrides, Move List and Systems Card, confirm modal, dialogue box roundel and caret, dialogue log). Full run on the final test tree (clean lock): `Failed: 0, Passed: 3612, Total: 3612`, 2 m 21 s. (Two earlier runs read 520 with `Failed to connect: Connection timeout`, CLAUDE.md signature 5 from another checkout's GdUnit run, and were discarded.) The Settings scene's last spacing trim (Body separation 4, Controls 2) came after that run and touched no test.

Changed in place (no count change), each deliberate:
- `StoryPauseTests.TheTitleUsesTheTitleVariationWithNoFontSizeOverrideOnBothBuildPaths`: the role is now `PauseTitle`; the no-override and both-paths intent is kept.
- `LocalFighterPauseTests.TitleAndDisconnectLabelUseThemeVariationsWithNoFontSizeOverride`: the title role is `PauseTitle`, and the layout is read by name (`GetNode("Layout")`) because the panel's first child is now the halftone corner.
- `DialoguePresentationTests.TheBoxLabelsUseThemeVariationsWithNoFontSizeOverrides`: the speaker role is `SpeakerName`. `ThePortraitFrameAndTintFollowEachLinesAuthoredEmotion`: the Confused frame colour is `Ink2`.
- `DialogueEmotionTreatmentTests.EachEmotionCarriesItsOwnFrameColourDrawnFromTheSharedPalette`: the allowed palette list is `Accent`, `Accent2`, `Ink2`, `Ink3`, `BossRed`, `Warning`.

## 3. Provisional values (agent choices)
Scrim self-alpha 0.7 (the theme's 0.85 x 0.7 is about the mock's 0.58 dim); pause panel vertically **centred** (the mock hangs it at y 92; `MenuLayoutBoundsTests` demands the centre); a 64 px row label inset; row heights 64 (the `TemporalGlassButton` plate) in place of the spec's list; tag text is the level name up to its first " — "; the status strip's format; modal 700 wide with 300 x 58 compact rows; boot panel 1000 wide; Settings margins 24 and a 110 px minimum for the remap scroll; the emotion ring's colours and its 4 px inset.

## 4. Kit gaps (nothing edited; recommendations)
- **`DialogueBody` for `RichTextLabel`**: the role is a Label variation, so the dialogue text (a `RichTextLabel`) rides the theme default (Outfit 400, 20 px), not the spec's 33 px Outfit 500. A `RichTextLabel` variation in the generator (normal font and size) would close it; the existing pin forbids a `normal_font_size` override.
- **Button labels are mixed-case** until UR-WIDGETS step 3 draws them upper-case (orchestrator note).
- **Sub-line** (Appendix B): the dialogue box has no separate sub-line; the continue hint is the closest thing and keeps `SmallLabel` (a pin).
- **Settings row layout** (label 360 wide, value 90 right-aligned): not restructured; the rows keep their authored layout (labels above sliders in Audio and Gameplay).
- **Backdrop blur** is not available (`gl_compatibility`); the scrim is a plain dim.
- The `Portrait` carrier node in the dialogue box is a hidden state holder; a later pass could move tint and tooltip onto the roundel and retire it.

## 5. Capture comparison
`D:\Projects\ftt-scratch\ur-captures\pause\` (windowed, lock held): `pause_story`, `pause_story_fallback`, `pause_story_ui14`, `pause_fighter`, `disconnect_modal`, `settings_audio`, `settings_display`, `settings_gameplay`, `settings_controls`, `movelist`, `systems_card`, `confirm_modal`, `two_option_prompt`, `dialogue_box`, `dialogue_log`, `first_use_tooltip`, `boot_overlay`. Against `mix_pause.png`: the tagged ink panel, tilted level tag, the white `PAUSED` title, the status strip, the focused gold banner row with its marker and badge, the dark rows with ink3 markers, the halftone corner and the bottom prompt bar match. Differences: the panel is centred rather than at y 92; button labels are mixed-case (above); the mix's list has the Lower Difficulty row (hidden here outside the hub, as before); the background is a flat stand-in for gameplay, with no blur or saturation drop; badges show key caps ("ENTER") because the capture has no pad. At UI scale 1.4 the Story pause fits. The Controls tab overflowed the screen vertically at first; margins 24, a 110 px scroll minimum and tighter spacing now fit it (final `settings_controls.png`), but the remap list shows only about two rows at a time, so it is cramped and wants a design pass (a taller panel or a split tab).

## 6. Not seen / not done
- Nobody has looked at any of it in play, on a pad, or with a real level behind the pause. The tag on a campaign level was not captured (the capture has no level scene; the Chronal Integration tag is the Tutorial default).
- The Controls tab's remap rows (a scroll area) were not reviewed row by row, and Controls at UI scale 1.4 was not captured.
- Two-option prompt, systems card, dialogue log, tooltip and boot cards 2 and 3 (first-run, crash) were captured only in their first state.
- Hub, Act III and Fighter pause variants were not captured with a real session.

## 7. Drafted doc lines
- **AGENTS.md (UI section):** "Pause and overlay surfaces (2026-10-26, UR-PAUSE) use the PAUSE-A language: `ScrimPanel` scrim, `PausePanel` (the `InkPanel` with the 30 / 20 / 28 pause padding, a halftone corner and an out-of-flow level tag), `PauseTitle` / `StatusLine` roles, 560 x 64 `PulpButtonDecor` rows (`PulpScreenKit.StylePauseButton`) and a `PulpPromptBar`; Settings switches are `PulpToggleDecor`; the dialogue box is 1500 wide with a 150 px `PulpPortraitRoundel` inside the emotion ring and an `hourglass` caret."
- **Ledger:** `PROVISIONAL-UI-PAUSE-LAYOUT` (section 3); `VERIFY-UI-DIALOGUE-BODY-RICHTEXT` (section 4, the 33 px body).
