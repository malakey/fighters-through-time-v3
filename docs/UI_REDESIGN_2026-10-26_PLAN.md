# Pulp UI redesign (2026-10-26): the Retro Pulp component kit in Resonance Gold with Cinzel + Outfit, the pulp-cover main menu, trading-card select, corner-cluster Story HUD, top-versus-bars Fighter HUD and pulp nameplate boss bar

Status: **in progress** (orchestrated 2026-10-26). Base: `main` at `68eb32fc`, test baseline **3546**.

Workstream IDs are `UR-*`; handoffs are `docs/handoffs/UR_<WS>.md`. This package implements item 7 of the user's 2026-10-25 request ("a redesign of the menus and UI HUD to give a better look that matches the design of our game"), after the mockups and four decision rounds.

## 0. Sources and authority

- **The look authority for this package:** `D:\Projects\fighters-through-time-docs-2\mockups\ui_redesign_2026-10-25\UI_SPEC.md` — the exact spec extracted from the mockup generator for the user's selection (palette tokens, type roles, component geometry, screen layouts at 1920 × 1080, motion, asset list). The rendered mockups beside it (`screens\1080p\`, `elements.html` with its mixer, `OPTIONS.md`) and the mixer renders of the selection (`D:\Projects\ftt-scratch\mix-preview\mix_*.png`) are the visual references. `DECISIONS.md` in the same folder records every ruling.
- Recon dossiers (read-only, code at `c12149cd`): `docs/recon-2026-10-26/A_theme_fonts_pipeline.md`, `B_huds.md`, `C_menus_overlays.md`.
- The authority rule still holds: where this package changes a contract or a design-doc line (HUD sizes, the Fighter HUD's placement, its Ultimate meter shape), the user's ruling is the new intent and the closeout amends `HUD_CONTRACT.md` and `design-godot.md` (master and mirror).

### 0.1 Rulings (2026-10-26; DECISIONS.md rounds 1-4)

| Element | Ruling | ID |
|---|---|---|
| Component family (panels, buttons, gauges, icons) | **Retro Pulp** (chosen over the recommended Temporal Glass) | `PNL-B` `BTN-B` `GAUGE-B` `ICN-B` |
| Palette | **Resonance Gold**: gold chrome and focus; cold cyan reserved for player 1's ownership colour and the Severed | `PAL-B` |
| Typography | **Cinzel** (titles, headings, speaker names) + **Outfit** (body, buttons, HUD numerals), both SIL OFL | `TYPE-A` |
| Story HUD | **Corner cluster**: vitals, abilities, time tools top-left; dust and the Integrity dial top-right | `HUD-S1` |
| Fighter HUD | **Top versus bars** (amends the design doc's "bottom of screen") | `HUD-F2` |
| Main menu | **Pulp cover** | `MENU-B` |
| Character select | **Trading cards**, 5 × 2 | `CARD-B` |
| Pause | **Centred panel** | `PAUSE-A` |
| Boss bar | **Pulp nameplate** | `BOSS-B` |
| Motion | **Pulp pop** (stable fades under Reduced Temporal Effects) | `MOT-B` |
| HUD sizes | **Bigger** — the mockups' sizes (about 1.5-2× the HUD contract); amend the contract and the design doc | — |
| Fighter Ultimate meter | **Bar** under each HP bar (amend the contract's 48 px circle) | — |
| Block-success effect colours | **Keep as built** (cyan arc / gold double ring / red shards) | — |
| Next step | **Build it as the next package** | — |

The palette ruling also settles the grammar question the recon raised: the interface wears the Wardens' gold; `UIPalette.ResonanceGold` / `ResonanceGoldBright` keep their values and their equality with `Gold` / `GoldBright` (`ResonanceGrammarTests`), the Severed keep `UnboundCold*`, and player 1 keeps its cyan through `GlowPalette.SlotColor` / `PlayerSlotPalettes`, never through the new accent.

### 0.2 Out of scope

No gameplay, Fighter simulation, save-schema or protocol change; no new Klotho component. The hub's world-space labels (`HubWorldController`) and the de-scoped network select get only what the theme gives them for free. No new screens; no text baked into textures (one logo per language is not acceptable — the title stays live text).

## 1. Standing decisions (every workstream)

1. **Docs.** Implementers do not edit `AGENTS.md`, `CLAUDE.md`, the ledger, `design-godot.md` (or its master), the contracts or the status docs. Each writes `docs/handoffs/UR_<WS>.md`: what shipped (files, types, theme variations, textures), the exact `[TestCase]` delta with arithmetic, every test changed in place and why, every provisional value, what did not ship, the captures it took and how they compare with the mockups, and drafted text for each doc change.
2. **Theme ownership.** Phase A owns `resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.cs`, `assets/fonts/`, the kit textures and the shared motion helpers. Phase B workstreams do not edit those files; if a screen needs a theme variation the kit lacks, the workstream uses a scene-local style (in its own `.tscn`) and lists the gap in its handoff for the closeout to fold into the theme.
3. **Re-skin, don't rename.** Keep the existing variation names (`TemporalGlassButton`, `DialogueGlassPanel`, the Label variations) and re-skin them to pulp, so their ~133 references change look without scene edits; add new variations for new roles. Keep `UIPalette`'s type-scale constants and variation-name constants in sync with the theme (`UIThemeTests`).
4. **Palette.** Add semantic constants (accent / focus gold, ink, panel and paper fills, text inks, HP and tracker, danger, halftone, …) with the values in UI_SPEC §1, and re-point call sites explicitly. Do not change the value of `Cyan` or of any grammar constant; chrome that used `Cyan` / `TextAccent` / `CyanDim` moves to the new accent, player-1 identity moves to the slot colour, Severed effects stay on `UnboundCold*`. The closeout adds a source-scan guard against chrome cyan in UI scripts and scenes.
5. **Fonts.** Ship full Cinzel and Outfit TTFs (the weights UI_SPEC names) with their `OFL.txt` under `assets/fonts/<family>/`; the default font is Outfit; Cinzel on the title, heading and speaker roles; leave the reserved CJK fallback slot empty; add licence rows to `ThirdPartyLicenses`; rewrite `UIThemeTests.ThemeWorksWithoutAFontResource`; add a guard that every character in the compiled English translation is covered by the shipped fonts. Downloaded font files are untrusted data: put each download in its own new directory, only copy/read them.
6. **Icons replace glyphs.** Cinzel and Outfit do not contain the HUDs' Unicode glyphs (◑ ☠ ⚡ ✸ ⟊ ✧ ✕ ⧗ ❄ ✦ ▲ ●), so the ICN-B icon set ships as textures in Phase A and the HUDs show them. Keep the existing `Label` nodes and their `Text` (tests and accessibility read them) and add a `TextureRect` icon (hiding the glyph with `VisibleCharacters = 0` or equivalent), unless a workstream moves a test seam to the model, which it documents. Icons stay distinct per status and Defy state (shape carries state).
7. **Motion (MOT-B).** Shared helpers in Phase A: a focus "pop" for buttons (tilt/scale on an inner visual, never on a container-managed control, which a container sort resets), a stepped number "thunk", panel pop-in, and the diagonal wipe between screens on the transition layer under `GameManager` (`ProcessMode.Always`). Every effect reads `FTT.Core.ComfortSettings` and falls back to a stable fade; tweens that must run while the tree is paused use a process pause mode. Nothing strobes.
8. **Node paths.** Keep names, paths and types wherever the screen allows; the HUD scripts bind by path and fail silently. Rewrite a test only for an intentional structural change (the Fighter HUD's top anchors, the 5 × 2 card grid, the pulp-cover root, bigger pinned sizes such as the Beacon's 24 / 8 px), and list each.
9. **Sizes and fit.** Build to UI_SPEC's sizes; check every surface at UI scale 1.0 and 1.4 (`MenuLayoutBoundsTests` for menus; a new bounds check for the HUD clusters: inside the safe area, no overlap with the boss bar, the level title or each other at 1.4).
10. **Localization.** New copy needs keys (life dates `character_<id>_dates`, the pause status line, the Continue sticker, the PHASE tab, the card stat line, …) appended to `localization/en.csv` in a per-workstream block headed by a comma-less `# UR <WS>` comment row; run `--headless --import` locally; never commit `localization/en.en.translation` (the orchestrator regenerates it once per phase). Keep raw keys in `.tscn` text (`game_title` stays the title's text).
11. **Visual checks.** Each workstream captures its screens at 1920 × 1080 with the Phase A capture tool (a windowed console-Godot run, never `--headless`, holding the lock, kept brief) into `D:\Projects\ftt-scratch\ur-captures\<ws>\`, views them, compares them with the mockup PNGs, fixes what differs and reports the comparison honestly in the handoff.
12. **Tests, lock, worktrees.** One `[TestSuite]` per file; `[RequireGodotRuntime]` wherever the engine is touched; the failure-message idiom; restore session state and free nodes in `finally`. Every Godot launch holds `D:/Projects/.ftt-gdunit-lock` (mkdir to acquire; retry; stale after 20 min with no `testhost` / `*_console`; rmdir after). Never kill a non-console Godot (the user's editor). Worktrees under `D:/Projects/ftt-wt/ur-<ws>` with `.godot/` copied from the main checkout; never commit `.godot/`, the compiled translation or `.import` line-ending churn. Commit on your branch; no merge, no push.
13. **Nobody plays the build.** Captures are the visual evidence; say what was not seen.
14. **Orchestrator rulings on UI_SPEC §8 (provisional; recorded for the closeout's ledger):**
    - *Q1, the undefined `--red`:* the decorative plates that used it (the cover's THROUGH TIME banner, the card READY stamp) keep the look the user approved in the mixer preview — the dark ink plate and the paper bubble — so no "pulp red" palette token; functional damage cues (the HP damage tracker, the Integrity dial's danger wedge) use the `Danger` token #FF5A5A.
    - *Q2:* HP fills stay anchored at the outer edges (P1 left, P2 right), the empty part growing toward the timer — the drawing and today's build.
    - *Q3:* the hit flash is rate-limited to once per second per bar.
    - *Mockup defects (§8.1):* build the intended values — the Rally echo band and the HP tracker visible (§3.3.1), the star badges with their ink shadow, the Integrity caption centred on the dial, a 3 px ink outline on scene captions (the version string, P1 / P2 labels), the wipe's ink edge.
    - *Overflows (§8.2):* a right inset of at least 34 px on button stacks (the focus pose never pokes through a panel), the panel tag clear of the objective line, the Continue sticker auto-fits its text and hides when no save exists, the version string legible (outline or off the burst), the debug-build rows at a pitch that fits, HUD clusters scaled per corner so nothing overlaps at 140 % (the boss bar top-centre with a maximum width; Fighter HP bars shrink before they meet the timer), the card grid grows with the roster (never a hard-coded 10; compact scale when it outgrows two rows), and a key-cap prompt variant for keyboard and mouse.
    - *Data (§8.3):* new keys for life dates (`character_<id>_dates`), card names (reuse `hero_name_*` where it fits, else new keys), stat labels, panel tags, the compact Time Freeze / Integrity / PHASE / Objective / mode / prompt captions, "Chapter N" and short place names; card stat-bar maxima from the roster's real maxima; the Story Ultimate-ready state = a full gold ring **plus** a READY tag (`common_ready`), never hue alone; one siphon stream per living Extractor with a fan layout; the Act III Beacon icon and pips drawn in the pulp style; ability-slot icons cropped from each ability's VFX atlas cell (as the mockup did), reproducibly, with a small lookup; the title stays live text (stacked labels for the extrusion).
    - *Accessibility (§8.4):* every label at least 18 px at 1080p (slot key tags, the Defy label, the Time Freeze sub-label, card dates), contrast at least 4.5:1 for text (3:1 for large display text) — fix the disabled label, the dormant key and paper-on-red; square cooldown slots with a pie sweep are accepted as the readiness radial (the contract wording is amended); Fighter cooldown slots keep small S1 / S2 / MOV key tags.
    - *Spec rulings (§8.5) accepted:* hover = focus, pressed / disabled per the specimen sheet, the agent-default motion timings, stable fades under Reduced Temporal Effects, tracker #FF5A5A and echo #FFF7DA, Cinzel 500 / 700 / 900 and Outfit 400 / 500 / 600 / 700 (variable TTFs pinned by `FontVariation` are fine), ink outline width = CSS stroke / 2.

## 2. Workstreams

### Phase A — the kit (two parallel agents, then a short wiring step)

- **UR-ART** — the pulp art kit through the SVG pipeline (`tools/generate_assets.gd`, `tools/asset_src/jobs.json`, a builder script under `tools/asset_src/pulp/`): every texture in UI_SPEC's asset list (panel and paper-panel 9-slices with the hard offset shadow and halftone corner, button states and focus, banner, gauge frame and fill, radial slot frames, the boss nameplate and PHASE tab, the trading card frame, the speech bubble, the starburst, the Continue sticker, a halftone tile) and the ICN-B icon set (every status, lock state, Defy seal state, block pip, stock shape per slot, dust, rewind, Time Freeze states, Beacon anchor and pip, ability-slot frame). Import settings per recon A §5. A contact sheet of everything. No theme or script edits.
- **UR-THEME** — palette constants, fonts, type variations (UI_SPEC §2), the re-skinned and new variations wired to flat placeholders first, the motion helpers and the wipe, the UI capture tool (`tools/capture_ui_screens.gd` or equivalent: a list of scenes/states → PNGs), the `UIThemeTests` / font-test rewrite and the glyph-coverage guard, and a **cyan inventory** (every chrome-cyan site in scripts and scenes, each mapped to its Phase B owner and replacement). Then, once UR-ART is merged, it wires the textures into the theme's styleboxes (the `ProgressBar` fill stays a `StyleBoxFlat` so `FighterHUD.TintFill` keeps working).

### Phase B — the screens (six parallel agents, cut from post-Phase-A `main`)

- **UR-MENU** — the pulp-cover main menu (title as outlined live text, three hero poses baked from the retro v2 atlases into small PNGs rather than loading three ~45 MB atlases, the list on the right, the Continue sticker = the most recent save → `StoryManager.ResumeCampaign`), and the slot, character, difficulty, level-select, extras and licences screens (strip their per-button texture overrides).
- **UR-SELECT** — the Fighter select as 5 × 2 trading cards (the `StyleTile` rewrite, portrait, name, life dates from new `character_<id>_dates` keys, the speech-bubble READY, paper stat cards with a reduced stat line), the stage phase, the Holodeck console panel.
- **UR-HUDS** — the Story HUD corner cluster at the new sizes with the icons and the GAUGE-B Integrity dial, plus the pulp nameplate boss bar with a PHASE tab (one new key).
- **UR-HUDF** — the Fighter HUD as top versus bars (re-anchored panels, the Ultimate bar, three cooldown radials and status chips under each portrait, the centre timer), icons, the new sizes; rewrite the anchor test.
- **UR-PAUSE** — the Story pause (both build paths) and the Fighter pause as centred pulp panels with a status line, Settings, Move List, Systems Card, the confirm modal / two-option prompt, the dialogue box and log, the first-use tooltip, the boot overlay, the controller-disconnect modal.
- **UR-SCREENS** — level results, match results, the loading screens (with the wipe), Game Over, Credits, the Resonance grid (keep its semantic edge colours), the Calibration Drill picker / list / HUD, the timeline restart panel, Fighter play options, network select.

### Phase C — closeout (one agent, the main tree)

The chrome-cyan source-scan guard; `HUD_CONTRACT.md` amendments (sizes, Fighter placement, the Ultimate bar), `design-godot.md` master and mirror (Fighter HUD placement, sizes, typography, the UI layout section), `AGENTS.md` (the UI section, the theme and kit, the baseline), `CLAUDE.md` (baseline, authority map), the ledger section "Confirmed open deviations — pulp UI redesign, 2026-10-26"; three full-suite runs; a capture gallery of every redesigned screen for the user; the cross-provider review (with the pending FU-STORYSHOVE review).

## 3. File ownership

| File or area | Owner | Others |
|---|---|---|
| `resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.cs`, `assets/fonts/`, `scripts/UI/Motion/` (or wherever the helpers land), the capture tool | UR-THEME | — |
| `tools/asset_src/pulp/`, `assets/sprites/ui/pulp/`, the icon textures, `jobs.json` entries | UR-ART | — |
| `scenes/menus/MainMenu.tscn`, `scripts/UI/MainMenu*.cs`, baked hero poses | UR-MENU | — |
| `scenes/menus/CharacterSelect.tscn`, `CharacterSelectScreen*.cs`, `HolodeckConsolePanel.cs` | UR-SELECT | — |
| `scenes/ui/StoryHUD.tscn`, `StoryHUD*.cs`, `HudAbilityIndicatorModel.cs`, `DefySealModel.cs`, `RadialProgress.cs`, `BossPhaseNotchOverlay.cs`, `BeaconAnchorIcon.cs` | UR-HUDS | UR-HUDF may read the shared models; edits to shared HUD helpers go through UR-HUDS |
| `scenes/ui/FighterHUD.tscn`, `FighterHUD*.cs`, `FighterHudModel.cs` | UR-HUDF | — |
| Pause, Settings, Move List, Systems Card, dialogue, confirm modal, tooltip, boot overlay scenes/scripts | UR-PAUSE | — |
| Results, loading, Game Over, Credits, Resonance grid, drills, timeline restart, play options, network select | UR-SCREENS | — |
| `localization/en.csv` | shared | per-workstream `# UR <WS>` blocks |
| `localization/en.en.translation` | orchestrator | — |
| `tests/**` | each owns its new files | shared edits minimal and listed |

## 4. Merge order and validation

- Per workstream: `dotnet build` (0 errors, no new warnings), filtered runs of the touched suites, one full run (expected total stated), captures.
- Phase A: merge UR-ART, then UR-THEME (after its wiring step); regenerate the translation if needed; one full run on `main`.
- Phase B: merge UR-HUDS → UR-HUDF → UR-SELECT → UR-MENU → UR-PAUSE → UR-SCREENS; regenerate the translation once; three full runs whose total equals 3546 + every declared delta.
- Phase C as above.

## 9. Deviation log (append-only)

- **2026-10-26, Phase A landed.** UR-ART merged at `d1e9e2a0` (3552 = 3546 + 6); UR-THEME (both steps) merged at `a76ca292`, the compiled translation regenerated at `1ead360d`; one full run on `main`: `Failed: 0, Passed: 3574, Total: 3574` (3552 + 22). The headless `GameManager.LoadScene` "Parse Error" UR-THEME saw on `MainMenu.tscn` / `HubWorld.tscn` reproduces at `68eb32fc` (pre-UR); not this package's.
- **2026-10-26, orchestrator: a shared-widget step (UR-WIDGETS) between Phase A and Phase B.** Writing the Phase B briefs showed that four or more screens each need the same parts: the three-layer ICN-B icon, the banner button's marker / focus pose / prompt badge / selected plate, the device-aware prompt bar, the halftone panel corner, and the GAUGE-B parts (the HP / meter / stat bar overlays, the square and Ultimate ability slot, the status chip, the portrait roundel, a tagged panel). Six parallel agents would build divergent copies, so one agent builds them under `scripts/UI/Pulp/` (namespace `FTT.UI.Pulp`, handoff `docs/handoffs/UR_WIDGETS.md`) in two steps: step 1 the icon, button decor, prompt bar and halftone corner; step 2 the gauge parts. UR-MENU starts after step 1 (it needs nothing from step 2); the other five Phase B workstreams start after step 2. Phase B owns none of `scripts/UI/Pulp/`; a gap there goes in the handoff for the closeout.
- **2026-10-26, orchestrator: the theme stays Phase A's.** `UR_THEME.md` §2.6 invites a Phase B workstream to edit the generator; standing decision 2 governs instead (six branches regenerating one `.tres` cannot be merged). A screen that needs a role or plate the kit lacks uses the nearest existing role, or a scene-local `StyleBoxTexture` over a kit PNG (never a font-size override), and lists the gap; the closeout folds the gaps into the generator.
- **2026-10-26, UR-WIDGETS step 1 landed** at `d4038890` (`PulpIcon`, `PulpButtonDecor`, `PulpPromptBar`, `HalftoneCorner`; +14, 3588 on its branch), the translation regenerated at `c5c9c838` (`ui_prompt_select`). UR-MENU started from `c5c9c838`; UR-WIDGETS step 2 (the gauge parts) is in flight, and the other five Phase B workstreams start from its merge.
- **2026-10-26, orchestrator: the cover title is two live keys.** The cover sets "FIGHTERS" (T01) apart from the "THROUGH TIME" banner (T02), so item 10's "`game_title` stays the title's text" is amended: the cover reads `menu_cover_title_word` and `menu_cover_title_banner`, and hides the banner when its translated text is empty, so a locale can put the whole title in the first key. `game_title` stays for the other screens. Still live text, still no text baked into a texture.
- **2026-10-26, UR-WIDGETS step 2** (the GAUGE-B parts and `PulpToggleDecor`, +14) merged at `a37ff42d`; Phase B's other five workstreams were cut from `22219329`.
- **2026-10-26, orchestrator: decorated button labels are drawn UPPER-case by `PulpButtonDecor`** (UR-WIDGETS steps 3 and 3b). UI_SPEC renders every label upper-case while translation values stay mixed-case, and a Godot `Button` has no upper-case property, so the decor hides the button's own glyphs with colour overrides and draws `Tr(Text)` upper-cased in the button's own theme font and state colour, wrapping multi-line labels and ellipsizing before the focused row's badge; `Button.Text` stays the raw key. Labels use `Label.Uppercase`; no screen upper-cases a translated string in code. Step 3b fixed a re-entrant colour capture that drew unfocused rows in transparent ink, and widened the main menu's delete and difficulty rows.
- **2026-10-26, merges and verification.** UR-MENU `a4259226`; UR-SCREENS `d9bb011c` and UR-WIDGETS 3/3b `06c202e0`; UR-HUDF `228c4694` (3637, one run); UR-SELECT `eba201ec` (3648, one run). A run cut to `Total: 518` with exit code 100 was cross-worktree contention (signature 5), discarded and re-run; the agents were reminded of the lock rule.
- **2026-10-26, orchestrator: the Integrity dial's wedge is gold.** The spec draws the remaining-Integrity wedge in an undefined `pulp_red`, which renders as a plain face in the approved mix; a red wedge at 64 % reads as alarm. The wedge is `UIPalette.Res` and turns `Danger` below the Collapse Tremor's first threshold (read from its rulebook). This refines item 14's Q1 ruling for the dial.
- **2026-10-26, kept for a user ruling: the cover's Quick Play row.** The mock draws six root rows; the build shows "Quick Play (Einstein vs CPU)" in release builds (it is not debug-gated), so the cover has seven (eight in debug), and the long upper-case label is ellipsized. Hiding it, debug-gating it or shortening its label is the user's call.
- **2026-10-26, UR-SELECT at 140 %:** card names, dates and the side-card name shrink to fit their boxes by node scale (never below their 1.0 size, never a font-size override; `CardTextFit`), and the footer buttons widen with the UI scale.

## 10. Closeout

(pending)
