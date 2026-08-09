# Package 8 — Product-wide UI, audio, visuals, controls, localization: implementation plan

**Status:** Authored 2026-08-08 from a four-way reconnaissance (UI surfaces, audio, visuals,
controls/localization). Baseline at authoring: **907 passing tests**, Package 6 closed, Package 7
(online) **deferred by user decision** — every online-dependent Package 8 item is explicitly out of
scope here (see §1). Everything in this package is placeholder-first: reusable systems plus
contract-conformant placeholder content; production assets remain Package 10.

**Authority order:** explicit user instruction > `design-godot.md` (shader spec ~2992–3045, audio
~2788–2920, dialogue ~2788, settings/§11) > this plan > existing code — EXCEPT numbers: authored
`.tres` resources beat prose everywhere they exist.

**Read §9 (Deviations) before touching any presentation surface.** Append-only; Package 5/6 format
(`**<WS>: one-sentence claim.** reasoning + pinning test`).

---

## 1. Scope

From `IMPLEMENTATION_PLAN.md` Package 8, minus the Package-7-dependent items:

**In scope:** shared UI theme and reusable screens replacing controller-built production UI;
Story/Fighter HUD completion (boss phase notches, conditional enemy bars, cooldown/status
indicators, production Fighter HUD, level results, loading treatments, credits/completion
theming); dialogue glass treatment, portraits/emotions, text chirps; controller/keyboard focus,
back/cancel, modal ownership, pause variants, HUD opacity, safe resolutions; complete InputMap
binding override persistence + remap UI + conflict UX + reset-to-default; settings verification
(display persistence, boot-time application, haptics/shake in both modes); the audio framework
(bus hierarchy with sub-buses, synchronized stem director, intensity transitions, last-stock
climax, KO stinger, snapshots/ducking/crossfades, footsteps, ability cast/impact hooks,
environmental/hazard sounds, dialogue chirps, pooled voice management); the visual framework
(shared outline/glow shader + arbiter + PointLight2D, status indicators, hyper-armor shell, hit
feedback, complete rewind treatment, era VFX emitters, orb/extractor visuals, stage lighting,
particle caps, off-screen suspension); the `Parallax2D` migration (Package 6 handoff); English
localization completion + missing/unused-key validation tooling.

**Explicitly OUT (Package 7 deferral):** `network_select`, `network_error`, `online_pause` UI
(manifest rows stay `Planned` — they also keep `PlannedResourcesRemainVisibleUntilTheyAreAuthored`
fed), the Chronal Jitter warning, online lobby flows, and reverb tuning for netplay. Also out:
production art/audio (P10), performance gates (P9 — but P8 must not knowingly regress budgets),
additional languages (deferred by the plan of record).

## 2. Standing decisions (all agents)

1. **Locked shared paths** (referenced across parallel worktrees; creator noted):
   `resources/UI/ftt_theme.tres` (A1) — the single shared `Theme`; `scripts/UI/UIPalette.cs` (A1)
   — palette constants replacing the ~15-file ad-hoc palette (cyan `(0,0.9,0.9)`, gold
   `(0.95,0.8,0.3)`, boss red, slate, navy, shade); `assets/shaders/outline_glow.gdshader` +
   `scripts/Combat/GlowPresentationController.cs` (A3); `scenes/ui/Settings.tscn` (A4);
   `scenes/ui/StoryPause.tscn`, `scenes/ui/LoadingScreen.tscn` (A1). Agents in other worktrees
   reference these by path with a graceful fallback; everything resolves at merge.
2. **Shader/batching decision (explicit trade-off).** `gl_compatibility` canvas shaders do not
   support `instance uniform`s, so the design's "shared material + per-instance SetShaderParameter"
   is not literally achievable. Decision: **one authored base `ShaderMaterial`, duplicated
   per entity by the glow arbiter** at attach time. Bounded (<70 concurrent character/enemy
   sprites per the budget); Package 9 re-measures draw batches. Uniforms per
   `design-godot.md:3010-3013` (`outline_color`, `outline_thickness` 0–5, `glow_intensity`
   0.5–3.0, `pulse_speed`); priority arbiter per `:3031-3042` (hyper-armor/spawn-invuln > status >
   slot color).
3. **The glow arbiter is the single owner of sprite modulate/material state** for players and
   enemies. Existing direct `Modulate` writers (enemy `PlaceholderTint`, telegraph tint, dead-dim)
   route through it or are layered under it deliberately; a fighter tint must survive a status
   ending. Fighter-side status/armor presentation reads component state in the driver's
   `SyncPlayer` diff — presentation-only, nothing reads back into `scripts/FighterSim/`.
4. **Audio architecture:** buses authored in a committed `default_bus_layout.tres` — Master →
   Music, SFX (→ Combat, Movement, Environmental sub-buses), UI; the orphaned runtime `Ambient`
   bus is removed. A new `StemDirector` inside `AudioManager` consumes `StageAudioSet` (the
   existing type; Story level sets reuse it — `ClimaxStem` serves as the boss/climax layer in both
   modes) with synchronized starts, volume-crossfade intensity switching, and a tweened
   snapshot/duck layer (pause, low-health, ultimate, rewind — rewind's raw −12 dB duck in
   `RewindPresentationOverlay.cs:59-70` migrates onto it). Saved volumes apply at boot
   (`AudioManager._Ready` reads `GlobalData`; autoload order SaveManager→AudioManager already
   permits it). The 24-voice pool with oldest-steal stays and gains tests.
5. **Input bindings schema:** the dead `GlobalSaveData.InputBindings` `Dictionary<string,string>`
   (`SaveManager.cs:73`) is **replaced** by a structure representing multi-event bindings
   (physical keycode / mouse button / joy button / joy axis+sign per event, per action). This adds
   the first global-payload migration branch (schema v3 → v4 for the global payload) plus a
   `SaveEnvelopeTests` case. Restore path: `SaveManager` pushes bindings into `InputMap` after
   global load, before gameplay (autoload order: InputManager loads before SaveManager, so the
   push lives on the SaveManager side). `InputManager.ReadActionStrength` polls
   `InputMap.ActionGetEvents` live, so restored overrides flow through automatically.
   Reset-to-default = `InputMap.LoadFromProjectSettings()` + clearing the saved overrides. The
   `gameplay_ultimate` chord and derived dash gesture are represented read-only in the remap UI.
6. **Settings ownership:** A4 owns the entire `SettingsMenu` rework (authored `Settings.tscn`
   using the A1 theme, remap tab, display persistence + boot apply, dead-difficulty-dropdown
   removal, and the pause-discipline fix — closing Settings must return to whoever opened it and
   never touch `SceneTree.Paused` itself). All three openers (MainMenu, StoryPause,
   LocalFighterPause) embed the one scene.
7. **`Parallax2D` migration is one workstream (B7), all ten stages + their ten test suites + the
   opaque-backdrop guard rewrite together** — per the Package 6 closeout decision
   (`PACKAGE6_FIGHTER_PLAN.md` §9 C1). No other agent touches stage scenes.
8. **en.csv discipline:** every workstream appends its keys under a trailing comment marker
   (`# Package 8 <workstream>`), never edits existing lines, and **does NOT commit
   `en.en.translation`** — the orchestrator regenerates it once per integration wave
   (`--headless --import`) and C1 finalizes. Compiled-translation tests may fail inside a lone
   worktree until its local import; run the import locally but leave the artifact uncommitted.
9. **Determinism boundary unchanged:** nothing under `scripts/FighterSim/` may change except
   `FighterSimulationDriver` presentation code. Off-screen suspension applies to presentation
   nodes only. Existing hash/convergence suites are the proof and must stay green untouched.
10. **Every new surface honors the accessibility settings** — `HudOpacity` (live, not one-shot),
    `ScreenShakeScale`, `HapticsEnabled/Intensity`, `DamageNumbersVisible`. StoryHUD's current
    opacity ignorance is a bug to fix, not a precedent.
11. **Crash hygiene per CLAUDE.md** (never `Dispose()` a Godot `Resource`; `AuthoredResources`
    for authored data; no empty Script-typed arrays; pause released in `_ExitTree` and test
    `finally`), plus the Package 6 additions: never run GdUnit concurrently across checkouts
    (drain `testhost` first; a partial `Total:` with 0 failures is contention), ONE `[TestSuite]`
    per file, never pass an empty string to `OverrideFailureMessage`.
12. **Manifest flips happen at C1 only.** Phase A/B agents leave `content_manifest.csv` alone.

## 3. Phase A — frameworks (four parallel worktree agents; merge order A1→A2→A3→A4)

### A1 — UI theme, focus, pause, loading (owner: ui-core)
Theme resource + `UIPalette` + a `FocusChainBuilder` utility (authors `focus_neighbor`/initial
`GrabFocus` — today the repo has exactly two `GrabFocus` calls) + a shared confirm/modal helper
(replacing ad-hoc ColorRect modals) + `PauseMenuBase` extracted from the duplicated
`PauseMenu`/`LocalFighterPause` contracts with an authored `scenes/ui/StoryPause.tscn` (adds exit
confirmation; manifest row exists) + a themed `LoadingScreen.tscn` system replacing `GameManager`'s
code-built overlay, with a Story portal variant and a Fighter VS variant (portraits + stage name
from `SessionData`; scenes at the reserved `StoryLoading`/`FighterLoading` paths — GameManager
picks by destination). Keeps the 2 s minimum. Files: new UI scenes/scripts, `GameManager` loading
region, `PauseMenu.cs`. Does NOT touch SettingsMenu (A4), MainMenu/CharacterSelect (B4).

### A2 — Audio framework (owner: audio-core)
Per §2.4: authored bus layout + sub-buses; `StemDirector` (synchronized `StageAudioSet` playback,
ambient/combat/climax intensity switching with crossfades, per-scene set registration); tweened
snapshot/duck layer with pause/low-health/ultimate/rewind snapshots (migrate the rewind duck);
boot-time volume application; dialogue-chirp and footstep playback entry points
(`PlayChirp(pitch)`, `PlayFootstep(surface)`) and an `OnEnemyPresentation` → SFX binding point
(`PresentationEventID` suffix conventions: `.telegraph/.active/.recovery/.death`); voice-pool
tests (steal-oldest, exhaustion). Authors the placeholder KO stinger / victory fanfare / countdown
blip assets under `audio/sfx/**` so `FighterSimulationDriver`'s existing cues stop no-oping (paths
at `FighterSimulationDriver.cs:83-84`). Files: `AudioManager.cs`, new bus layout resource, new
audio assets, `RewindPresentationOverlay.cs` (duck migration only), tests. Does NOT touch level
controllers (B5) or DialogueManager (B3).

### A3 — Visual framework (owner: visual-core)
The `outline_glow.gdshader` per `design-godot.md:2992-3045` (verified under `gl_compatibility`) +
`GlowPresentationController` arbiter (priority per §2.2/§2.3) attached by `CharacterFactory` and
the enemy/boss controllers, with child `PointLight2D` (textured — the repo's one existing light is
inert for lack of texture); status-effect states subscribed to `OnStatusEffectApplied` (zero
subscribers today) and Fighter-side driver diffing of `StatusType`/`HyperArmorFrames`/respawn
invulnerability; hyper-armor shell replacing the gold `ChronalArmorOverlay` ColorRect
(`CharacterFactory.cs:110-117`); hit flash on players and enemies; Fighter-mode camera shake +
haptics (driver HP-delta/KO wiring; fix `HapticFeedbackManager` device-0 hardcode and
player-index-as-device bug via `InputManager.GetDeviceForPlayer`; wire or delete the dead
`OnHitConfirm`/`OnUltimateActivation`); post-rewind invulnerability aura; the pooled-VFX emitter
service that finally consumes `AbilityData.CastVFXScene/ImpactVFXScene` and binds
`PresentationEventID` to the warmed-but-never-spawned `combat_vfx`/`environment_vfx` pools, with
simple procedural particle children added to the pooled templates + a particle-cap registry
(500 budget); `VisibleOnScreenNotifier2D` off-screen suspension for presentation nodes. Files:
new shader/arbiter, `CharacterFactory.cs`, `PlayerController.cs` presentation methods,
`EnemyController.cs`/`BossController.cs` presentation, `FighterSimulationDriver.cs` presentation
sync, `EventBus.cs` payload additions, pooled templates, tests. Coordinates: B5 also edits
`PlayerController` (footsteps) and the driver (climax) — keep edits additive.

### A4 — Input remapping + settings persistence (owner: controls)
Per §2.5/§2.6: binding serialization schema + global-payload migration branch + restore-before-
gameplay; authored `Settings.tscn` (A1 theme by path) with the remap tab (listen-for-input capture,
conflict detection with localized explanation, per-action and global reset-to-default, chord/dash
shown read-only); display persistence (`Resolution`/`WindowMode`/`VSyncEnabled` GlobalData fields,
clamps, boot apply via `ViewportEnforcer` or `GameManager`); localized tab titles (raw English
today at `SettingsMenu.cs:79/97/124`); dead difficulty dropdown removed; pause-discipline fix;
surface `SaveManager.LastLoadNotice` (localized) on the main menu when a recovery/migration
occurred. Tests: settings + bindings round-trip (none exist today), migration case, restore-order
integration, conflict rules. Files: `SettingsMenu.cs`→scene+script, `SaveManager.cs`,
`SaveEnvelope.cs`, `InputManager.cs` (apply/capture helpers), `MainMenu.cs` (notice line only —
coordinate with B4; keep additive), en.csv keys.

## 4. Phase B — application (seven parallel worktree agents, after Phase A merges)

Every B agent: applies the A1 theme + focus authoring to its surfaces, honors §2.10 settings,
appends en.csv keys per §2.8, one `[TestSuite]` per file, §9 block.

- **B1 — Story HUD & results (owner: story-hud):** boss bar phase notches (phase data from the
  authored `BossData` via `BossEncounterController` — today only HP reaches the HUD); conditional
  enemy overhead bars (hidden until damaged, fade after quiet period; bosses keep the big bar);
  player cooldown + active-status indicators on `StoryHUD`; live `HudOpacity` compliance;
  `LevelResults` stats polish (dust + completion time + rewinds used — data available in
  `StoryManager`); `TimelineRestartPanel` theming; delete the `BuildFallbackUI` duplicate where
  the authored scene is now guaranteed. Files: `StoryHUD.*`, `LevelResultsPanel.*`,
  `EnemyController` bar logic, `BossEncounterController` HUD wiring, `TimelineRestartPanel.cs`.
- **B2 — Fighter HUD & match presentation (owner: fighter-hud):** a production Fighter HUD scene
  (per-player HP/meter bars, stock pips, portraits, block charges, cooldown icons, match timer,
  status icons) attached by the driver for all ten stages, replacing `TestArenaHUD`'s debug labels
  (debug text stays behind a toggle); retire or absorb the orphaned `HUDController` (empty
  `UpdateStocks` stub); theme `FighterPresentationOverlay` banners (3-2-1-GO/KO/DRAW/winner) and
  `MatchResults`; adopt A1's VS loading variant data needs. Files: new FighterHUD scene/script,
  `TestArenaHUD.cs`, `FighterPresentationOverlay.cs`, `MatchResults.cs`,
  `FighterSimulationDriver.cs` UI-attach block only.
- **B3 — Dialogue & narrative presentation (owner: dialogue):** glass/translucent panel treatment
  (StyleBox via theme); portrait emotion presentation (tint/frame per emotion replacing the
  literal "(emotion)" label); typewriter chirps via A2's `PlayChirp` with per-character pitch
  (`design-godot.md:2788`; pitch field on `CharacterData` or a chirp map); box in/out animation;
  reveal punctuation pacing (comma/period pauses); `Credits` + campaign-completion theming.
  Files: `DialogueManager.cs`, `DialogueBox.tscn`, `CreditsController.cs`, `CharacterData.cs`
  (pitch field only), dialogue tests.
- **B4 — Menus (owner: menus):** convert `MainMenu` and `CharacterSelectScreen` from code-built
  shells to authored, themed, focus-authored scenes (slot select, character grid as real focusable
  buttons, difficulty, stage select + preview, rules); convert `ResonanceGridPanel` to an authored
  scene at the reserved `ResonanceGrid.tscn` path (keep the tested `ResonanceGridNavigation`
  math); fix `MainMenu.CharacterName()`'s raw `GD.Load` (`MainMenu.cs:307` → `AuthoredResources`);
  remove `Player.tscn`'s visible "Player" debug label text and delete unreferenced
  `scenes/TestScene.tscn`; uniform back/cancel handling via A1 utilities. Files: menus scenes/
  scripts, `ResonanceGridPanel.cs`, `Player.tscn`, en.csv keys.
- **B5 — Audio content (owner: audio-content):** author the 17 Planned level/hub/tutorial audio
  sets (`resources/Audio/{tutorial,hub,level_01..15}_audio.tres`, `StageAudioSet` type, placeholder
  stems) at the exact manifest paths; wire `StorySceneBootstrapper`/`StoryLevelControllerBase` +
  `HubWorldController` + `BossEncounterController` to the A2 `StemDirector` (ambient ↔ combat on
  encounter activation, climax on boss, level-complete release); Fighter: stage set registration in
  `FighterStageController`/`TestArenaController` + last-stock climax trigger + countdown/KO cue
  wiring via the A2 assets; hazard/pickup/destruction/toolkit SFX via the A2 enemy-presentation
  binding and environment controllers; footstep emission from `PlayerController` movement (surface
  parameter plumbed, single placeholder sound). Files: audio `.tres` content, level/hub/stage
  controller audio wiring, `PlayerController` footstep block (additive; A3 merged first),
  `FighterSimulationDriver` audio block (additive), tests (every set validates, transitions fire).
- **B6 — VFX content (owner: vfx-content):** assign `CastVFXScene`/`ImpactVFXScene` on the 36
  ability resources (shared placeholder VFX scenes, per-character accent colors); bind the
  authored roster `PresentationEventID` strings to concrete pooled VFX through A3's emitter
  (telegraph/active/death for the 27 enemies + 15 bosses at placeholder fidelity — reusable
  mappings, not 42 bespoke scenes); Story orb/pickup visual upgrade (use `ChronalOrbData.Icon`,
  glow via A3); extractor damaged/destroyed dressing in its template; complete the three dead
  rewind payload fields (ghost trail from the existing per-player `TemporalPositionHistory`,
  reverse sweep cue, clock tick cue — audio via A2); Fighter driver proxy polish (orb/hazard
  proxies get shape/pulse identity beyond flat ColorRects, within the existing proxy pool
  discipline). Files: ability/orb/extractor resources + presentation scripts,
  `RewindPresentationOverlay.cs`, driver proxy code (additive), tests.
- **B7 — Stage lighting & Parallax2D migration (owner: stages-visual):** migrate all ten Fighter
  stage scenes `ParallaxBackground`→`Parallax2D` in one pass, update the ten per-stage suites'
  untyped parallax assertions to typed (killing the CS0618 workaround), and **rewrite** the
  `FighterStagePresentationTests` opaque-backdrop guard for the new z-order semantics (per the
  Package 6 §9 C1 decision — rewrite, not delete); add per-stage lighting rigs (`CanvasModulate`
  ambient tone + 2–3 textured `PointLight2D`s per era palette) and a reusable
  `StageLightingRig` template also applied to the hub and two exemplar campaign levels (2, 6) as
  the pattern for later content; verify smokes for all ten stages. Files: ten stage scenes, their
  suites, `FighterStagePresentationTests.cs`, lighting template, hub/level 2/6 scenes.

## 5. Phase C — closeout (C1, serial)

1. Manifest flips: `story_hud`, `dialogue_box`, `settings`, `story_pause`, `story_loading`,
   `fighter_loading`, `resonance_grid` → `Implemented/Placeholder/Valid` (plus `fighter_hud` →
   its new scene path, `main_menu`/`fighter_character_select`/`stage_select`/`match_settings`
   rows updated to Implemented where B4 converted them); the 17 level AudioSet rows →
   `Implemented/Placeholder/Valid`; `visual_shaders` row → the real shader path. Leave the three
   network rows Planned (P7). Re-verify `PlannedResourcesRemainVisibleUntilTheyAreAuthored`
   feeders; retire honestly if truly starved (three network UIScreen rows should keep it alive).
2. Localization tooling (new tests): every `Tr("...")` literal in `scripts/` resolves through the
   compiled translation; unused-key report (keys never referenced in scripts/scenes/resources —
   warn-level list, fail on egregious count growth); `.tscn` visible-`text` scanner rejecting
   non-key literals (two known offenders fixed by B4 prove it). Final `--headless --import` and
   commit both compiled translation artifacts.
3. Full validation per §8 of the P6 plan pattern: build (no new warnings — B7 should have removed
   the CS0618 pragmas), import, **three consecutive full-suite runs** with exact `Total:`
   reported, headless smokes: MainMenu, CharacterSelect, TestArena, all ten fighter stages,
   HubWorld, Tutorial, Florence, Level_02, Level_06, Level_15.
4. Ledgers in one commit: `AGENTS.md`, `CLAUDE.md` (baseline), `IMPLEMENTATION_PLAN.md` Package 8
   checkboxes (parenthetical: network items deferred with P7), root `IMPLEMENTATION_STATUS.md`
   (§11/12/13/17/18/19), `docs/IMPLEMENTATION_STATUS.md` (milestone rows, closure paragraph,
   validation record), this plan's §9 closeout + `#### What Package 8 did NOT deliver`.

## 6. File-ownership / conflict matrix

| File | Phase A owner | Phase B owner(s) | Rule |
|---|---|---|---|
| `SettingsMenu` / `Settings.tscn` | A4 | — | A4 exclusive |
| `GameManager.cs` (loading) | A1 | — | A1 exclusive |
| `AudioManager.cs` | A2 | — | B5 consumes API only |
| `PlayerController.cs` | A3 (presentation) | B5 (footstep block) | additive; merge A3 first |
| `FighterSimulationDriver.cs` | A3 (sync/feedback) | B2 (UI attach), B5 (audio) | additive blocks; merge order B2→B5 |
| `EventBus.cs` | A3 | — | others subscribe only |
| `EnemyController.cs`/`BossController.cs` | A3 (glow attach) | B1 (bar logic) | additive; A3 first |
| `CharacterFactory.cs` | A3 | — | exclusive |
| `CharacterData.cs` | — | B3 (chirp pitch field only) | exclusive |
| `MainMenu.cs` | A4 (notice line) | B4 (conversion) | B4 rebases A4's line into the new scene |
| Ten stage scenes + suites | — | B7 | exclusive |
| `RewindPresentationOverlay.cs` | A2 (duck migration) | B6 (ghost/sweep/tick) | additive; A2 first |
| `localization/en.csv` | all | all | §2.8 append-only markers; orchestrator unions |
| `content_manifest.csv`, `SceneSmokeTests.cs` | — | — | C1 only |

## 7. Sequencing

1. Plan committed to `main`; Phase A branches from it (agents: merge `main` into a stale worktree
   base first — Package 6 showed worktrees can snapshot early).
2. Merge A1→A2→A3→A4 with build + owned-test verification after each, one full serial suite after
   A4. §9 conflicts union-resolved.
3. Phase B seven agents branch from post-A `main`; merge as they complete (any order; B2 before
   B5 if both are ready simultaneously); orchestrator runs `--headless --import` + full suite
   after the last merge.
4. C1 serial in the main checkout.

## 8. Validation gates

Identical to Package 6 §8 (build with no new warnings; full suite `Total:` against baseline
907 + declared deltas, never trusting exit codes; `--import` after CSV/manifest edits with
committed artifacts per §2.8; scene smokes for touched scenes; Story/Fighter isolation intact —
the untouched determinism suites are the proof), plus: every agent states its exact expected test
delta in its report, and any suite whose surface it re-themed must still pass unmodified unless the
change is logged in §9.

## 9. Deviations (append-only)

*(Agents append `### <WS> — <subject> (date)` blocks; orchestrator appends integration blocks; C1
appends the closeout and the "What Package 8 did NOT deliver" list.)*

### A1 — UI theme, focus, pause, loading (2026-08-08)

**A1: The theme carries no font resource, deliberately.** `assets/fonts/` is empty, so requiring a
face would make the theme unloadable. `ftt_theme.tres` sets the full type scale
(`default_font_size` plus per-type sizes) against Godot's built-in default face; dropping in a
production font later is a one-line `default_font` addition, not a retheme. Pinned by
`UIThemeTests.ThemeWorksWithoutAFontResource`, which asserts `DefaultFont` is null *on purpose* —
if a later agent adds a face, that test is the place to record the decision changing.

**A1: `UIPalette` and the theme are pinned to each other rather than one generating the other.**
A Theme cannot express a `ColorRect` fill or a runtime tint, so both a resource and C# constants
have to exist; the failure mode is silent drift producing a half-and-half UI that reads as
sloppiness rather than as a bug. `UIThemeTests.AuthoredColoursMatchTheUIPaletteConstants` compares
the authored type colours *and* the painted `StyleBoxFlat` bg/border/focus-ring colours against the
constants, so changing one side alone fails.

**A1: `PauseMenuBase` owns `SceneTree.Paused`; subclasses may layer presentation but cannot skip
the tree write.** The extraction merged two independent implementations of the project's most
dangerous invariant. `SetPaused` is virtual for presentation only, `_ExitTree` performs the
handback and subclasses that override it must call `base._ExitTree()` (`LocalFighterPause` does,
for its `InputManager` unsubscribe). The pause press is consumed even when `CanTogglePause()`
refuses it, so it can never fall through to gameplay under a menu that is deliberately holding the
player. Pinned on both sides: the pre-existing `LocalFighterPauseTests` stayed green unmodified,
and `StoryPauseTests` mirrors its two pause-handback cases for the Story surface.

**A1: `LocalFighterPause`'s exit confirmation moved onto the shared `ConfirmModal`; its
disconnect modal did not.** The exit prompt is a confirm/cancel question and was one of the three
ad-hoc patterns the helper exists to retire. The disconnect modal is a blocking "rebind or forfeit"
state with no safe cancel, so forcing it into a confirm/cancel shape would have misrepresented it.
`CanTogglePause()` now also refuses while the exit confirmation is open, which the previous
implementation did not — an open confirmation could be resumed out from under.

**A1: Loading-variant selection is pure string logic on the destination path, with the Test Arena
named explicitly.** No caller states what kind of transition it is; `GameManager.LoadScene` passes
a path and gets a treatment. `scenes/arenas/TestArena.tscn` lives outside `scenes/fighter/` for
historical reasons but is a Fighter destination to the player, so it is matched by name rather than
by directory. Pinned by `LoadingScreenTests` across all ten stage paths, all sixteen campaign
paths, the hub, the menus, and null/blank input.

**A1: campaign level titles are derived from the scene filename's era token, not from a level
index.** The sixteen title keys were authored per level with a slug (`orleans_level_title`, not
`level_02_title`), so `Level_13_ChronalVoid.tscn` → `chronal_void_level_title` via a
PascalCase→snake_case pass; `HubWorld.tscn` maps to `hub_ship_title`. This is the fragile half of
the loading work — a wrong derivation shows a raw key on a full-screen portal — so
`EveryCampaignDestinationResolvesATitleKeyThatExists` resolves every one through the **compiled**
translation rather than merely checking the key looks plausible.

**A1: the loading screen is built per transition and freed on arrival, not created once at boot.**
The variant depends on the destination, so a single persistent overlay cannot serve all three. It
is parented to the `GameManager` autoload so it survives `ChangeSceneToPacked`, and runs
`ProcessMode.Always` so a paused tree cannot freeze it. The 2 s `MinLoadingDisplayTime` and the
threaded-load polling are unchanged.

**A1: visible copy in the new scenes is stored as raw translation keys and resolved by Godot's
automatic control translation**, rather than assigned through `Tr()` in `_Ready`. This is what lets
a language change follow without rebuilding a surface, and it is the pattern C1's planned
`.tscn` visible-`text` scanner expects. Tests therefore assert the raw key (`Text == "menu_paused"`),
not the English string.

**A1 handoff:** `resources/UI/ftt_theme.tres`, `scripts/UI/UIPalette.cs`,
`scripts/UI/FocusChainBuilder.cs`, `scripts/UI/ConfirmModal.cs` and `scripts/UI/PauseMenuBase.cs`
are on `main` at the §2.1 paths for B agents. Adopt the theme by setting `Theme` on a screen's root
`Control` (Godot propagates); author focus with `FocusChainBuilder.Apply(container)` and rebuild it
whenever a surface shows or hides options. Test delta **+30** (907 → 937).

### A3 — visual framework (2026-08-08)

**A3: `gl_compatibility` also forbids passing `TEXTURE` as a `sampler2D` function
argument, so every ring sample in the shader is written inline.** The plan's §2.2
already covered the missing `instance uniform` support; this is a *second*
compatibility constraint, discovered by the headless TestArena smoke rather than by
the build. A first draft factored the 8-directional sampling into a
`ring_alpha(sampler2D tex, ...)` helper, which compiles as far as resource load and
then aborts at `ShaderMaterial.Shader = shader` with
`Condition "!actions.custom_samplers.has(function->arguments[j].tex_builtin)" is
true` (`servers/rendering/shader_compiler.cpp:1329`) — once per material, so it also
scales with the per-entity duplication decision. The three rings are therefore
unrolled in `fragment()`. `OutlineGlowShaderTests.TheShaderIsACanvasItemShaderWith
TheAuthoredRanges` asserts the source contains no `sampler2D tex` parameter, because
a well-meaning refactor back into a helper would only fail at material-assignment
time, in whichever scene happened to load first.

**A3: a headless `--quit-after` smoke does compile shaders far enough to catch
this, but does not exercise the fragment program.** Godot's headless renderer runs
the shader *parser and compiler front end* (that is where the error above
surfaced), so syntax and semantic errors are caught. It does not link or run GLSL,
so the visual result — outline width, falloff weighting, pulse range, and the
sprite-over-glow compositing — is unverified by any automated gate and needs a human
look in the editor. Third known constraint, noted in the shader header: a
`CanvasItem` shader cannot draw outside its own quad, so a thick outline is clipped
unless the sprite art carries a few texels of transparent padding. Production art
(P10) must budget that padding.

**A3: `OnStatusEffectApplied` alone could not drive the arbiter; `StatusController`
gained a falling edge.** The bus had a rising edge only, so a status outline could
be pushed but never cleared. `EventBus.OnStatusEffectCleared` is new and
`StatusController.ClearStatus` raises it (with `StatusType.None`) whenever a status
was actually active — including the implicit clear inside `ApplyStatus`, so a
replacement fires cleared-then-applied in that order. Nothing gameplay-side reads
it; B1's HUD status pips want the same edge.

**A3: the arbiter owns three separate channels, not one `Modulate` value.** §2.3
requires that "a fighter tint must survive a status ending". Layering all three
sources onto `Modulate` cannot satisfy that, so `GlowPresentationController` keeps
base tint (`SetBaseTint`), tint override (`SetTintOverride`/`ClearTintOverride`/
`FlashHit`) and the arbitrated outline stack (`PushState`/`ClearState`) independent:
statuses and hyper-armor drive the shader, telegraphs and hit flashes drive the
override, and the authored `PlaceholderTint` is the base. `EnemyAbilityExecutor`
writes `Modulate` directly only when no arbiter is bound, which is what keeps
`EnemyAbilityExecutorTests.TelegraphTintsTheSpriteAndRestoresItWhenTheHitboxGoesLive`
green unmodified; `EnemyGlowRoutingTests` pins the arbiter-bound path beside it.

**A3: the hyper-armor shell uses the design's gold `#d4af37`, not a gold-cyan
blend.** `design-godot.md:3033` names two hex values for one outline colour
("Golden-cyan `#d4af37` / `#00f0ff`") without saying how they combine. Gold alone
was chosen because it preserves the retired `ChronalArmorOverlay` ColorRect's
identity and stays distinct from the cyan spawn-invulnerability aura directly below
it in the priority table. Venom's authored two-colour gradient *is* implemented, as
a per-frame lerp in the arbiter's `_Process`. Pinned in `GlowStateStackTests`.

**A3: the Fighter driver derives a guard-break beat from the daze edge.** The
deterministic simulation has no block-broken event, only `DazeFrames`. The driver
treats a 0→positive daze transition as the guard break for haptics only. If a future
change makes daze reachable by another route, the feedback will fire there too;
nothing gameplay-side depends on it.

**A3: `HapticFeedbackManager` had two device bugs, both now routed through
`InputManager.GetDeviceForPlayer`.** Damage feedback vibrated hardcoded device 0
(so in local 1v1 player two's damage buzzed player one's pad), and guard-break
feedback passed the *player index* to `Input.StartJoyVibration` as a device id.
`ResolveDevice` is now the single mapping, `Vibrate` rejects any device below zero
(covering both the keyboard `-1` and unassigned `-2` sentinels), and the previously
dead `OnHitConfirm`/`OnUltimateActivation` entry points are wired to two new bus
events rather than deleted. `HapticDeviceRoutingTests` pins it.

**A3: the off-screen suspender and the glow arbiter bind their signals in
`_EnterTree`, not `_Ready`.** Pooled owners re-enter the tree many times while
`_Ready` runs once, so a `_Ready`/`_ExitTree` pair unsubscribes on the first release
and then spams `Attempt to disconnect a nonexistent connection` on every later one —
seen immediately in the Level 01 smoke. Both classes now guard an `_EnterTree`/
`_ExitTree` pair with an explicit bound flag.

**A3: the particle cap steals from the oldest emitter rather than refusing the
newest.** `ParticleBudgetRegistry` supports both policies; the shared 500-particle
registry uses `StealOldest` so the newest gameplay feedback always renders and a
stale ambient emitter goes quiet instead. A single request larger than the whole
budget is refused outright (no amount of eviction could seat it) and its instance
simply spawns without particles. `PooledPlaceholder` reserves on `OnSpawn` and
releases on `OnDespawn`, keyed on its instance id.

**A3: `VfxPresentationBinder` self-installs from the enemy/boss controllers.** The
`OnEnemyPresentation` → pooled-VFX binding needs a scene-scoped subscriber, and A3
does not own the level controllers or `StorySceneBootstrapper`. `EnemyController`
and `BossController` call `VfxPresentationBinder.EnsureInstalled(this)` in `_Ready`,
which adds at most one binder to the current scene. B6 refines the per-
`PresentationEventID` mappings on that same surface; if B5/B6 would rather host the
binder from the bootstrapper, the `EnsureInstalled` call sites are the only change.

**A3 API surface for B1/B2/B6.** `FTT.Combat.GlowPresentationController`:
`AttachTo(owner, sprite, ownerPlayerIndex, subscribeToStoryEvents)`, `SetBaseTint`,
`SetTintOverride`/`ClearTintOverride`, `FlashHit(color, seconds)`, `PushState`/
`ClearState`/`ClearAllStates`, `SetStatus`/`SetHyperArmor`/`SetSpawnInvulnerability`/
`SetSlotIndicator`, and read-only `Target`/`GlowMaterial`/`Light`/`BaseTint`/
`IsGlowing`/`ResolvedState`/`IsLayerActive`. `PlayerController.Glow` exposes it on
players. `FTT.Combat.GlowPalette` owns every authored colour and
`GlowPalette.Status(StatusType)`. `FTT.Combat.VfxEmitter`: `EmitCombat`,
`EmitEnvironment`, `Emit(poolID, …)`, `EmitScene(PackedScene, …)`,
`EmitForPresentationEvent(id, phase, position, parent)` — all null-safe, all
returning `Node2D` or null. `FTT.Combat.ParticleBudget.Shared` is the 500-particle
registry; pooled emitters declare `PooledPlaceholder.ParticleBudgetCost`.
`FTT.Combat.PresentationVisibilitySuspender.AttachTo(owner, presentationRoot)` adds
off-screen suspension. New bus events: `OnStatusEffectCleared`, `OnHitConfirm`
(`HitConfirmPayload`), `OnUltimateActivation` (`UltimateActivationPayload`).

**A3 validation.** Build clean (only the pre-existing vendored `CS8632`); full suite
**956 = 907 + 49** across two consecutive runs; `--headless --import`; headless
smokes clean for `TestArena`, `FighterStage_Florence`, `Level_01_Florence`, and
`Level_00_Tutorial`. No existing suite was modified. Not delivered by A3 and left to
later workstreams: the post-rewind ghost trail / reverse sweep / clock tick payload
fields (B6), authored `CastVFXScene`/`ImpactVFXScene` resources (B6), the rewind
music duck migration (A2), and any visual confirmation of the shader's rendered
output, which no headless gate can discharge.

### A4 — input remapping, settings persistence, save notices (2026-08-08)

**A4: the binding schema is a per-action list of typed events, not a string map.** `InputBindingEvent`
carries `{Kind, Code, AxisSign}` where `Kind ∈ {Key, MouseButton, JoyButton, JoyAxis}`, and
`InputBindingSet` is `Dictionary<string, List<InputBindingEvent>>`. The dead
`Dictionary<string,string>` it replaces could not express a single real action — `gameplay_move_left`
alone ships two keys, a joypad axis and a joypad button. Keys are stored as **physical** keycodes,
matching `InputManager.ReadActionStrength`, which prefers `PhysicalKeycode`, so a remap survives a
keyboard-layout change. Pinned by `tests/Unit/InputBindingSchemaTests.cs`
(`MultiEventBindingsRoundTripThroughTheEncryptedGlobalEnvelope`,
`NormalizeClampsEveryEventShapeAndDropsUnusableRows`).

**A4: `SaveSchemaMigrator.CurrentVersion` moved 3 → 4 and the story chain gained no step.** The
version is shared by both payloads, so bumping it for the global binding change also renumbers story
saves; `DeserializeStory` has no v3→v4 work, which is correct — nothing in `StorySaveData` changed.
The one consequence is that `SaveEnvelopeTests.VersionTwoStorySaveMigratesPuzzleCompletionCollection`
asserted the literal `3`; it now asserts `SaveSchemaMigrator.CurrentVersion`. That is the only
pre-existing test A4 modified. Pinned by
`tests/Unit/DisplaySettingsPersistenceTests.cs::GlobalVersionThreeMigratesTheDeadStringBindingMapToTheStructuredShape`.

**A4: v3→v4 drops the old binding field rather than trying to interpret it.** The pre-v4
`InputBindings` map was written by no code path in the repository's history, so there is no real data
to preserve and any "interpretation" would be inventing player intent. The branch replaces it with an
empty structured set; the player keeps project defaults and can rebind. A payload that already
carries the structured shape passes through untouched
(`GlobalVersionFourKeepsAnAlreadyStructuredBindingSet`).

**A4: only actions that differ from project.godot are persisted.** `InputBindingService` snapshots
the InputMap once at boot — in `SaveManager._Ready`, *before* `LoadGlobalData` — and
`BuildOverrides` diffs the Settings tab's working map against that snapshot. Storing the whole map
would freeze today's defaults into every save and stop a later project.godot change from ever
reaching a player who never touched that action. Pinned by
`OnlyActionsThatDifferFromProjectDefaultsArePersisted`.

**A4: conflicts block, they do not swap.** One physical event drives at most one action in the local
action space. A swap would move a binding the player never asked to change, and would have to invent
a slot whenever the two actions hold different event counts. A refused capture leaves the working set
untouched and shows `controls_conflict` naming the action that already owns the input. Re-pressing an
event the same action already holds also reports that action, so the UI explains the no-op instead of
silently duplicating the event. Pinned by
`ConflictDetectionBlocksAnInputAlreadyOwnedByAnotherAction` and
`SettingsMenuSceneTests::ACaptureThatCollidesWithAnotherActionIsRefused`.

**A4: remap rows carry one slot per device kind, and a capture replaces only its own kind.** Each row
is `label | keyboard-or-mouse slot | joypad slot | reset`. Binding a key never clears the joypad
binding and vice versa, which is what makes the shipped mixed defaults (keys + axis + pad button on
one action) survive a partial remap. Pinned by `AFreeCaptureReplacesOnlyItsOwnDeviceKind`.

**A4: `gameplay_ultimate` and Dash are read-only in the Controls tab.** Ultimate is authored as an
LB+RB chord that `InputManager.ReadUltimatePressed` evaluates as a *conjunction* of the action's
joypad events — a per-event remap row cannot express "both at once" without redesigning the polling
rule, which is out of A4's scope. Dash has no InputMap action at all; it is a derived double-tap /
analog-flick gesture. Both are stated in `controls_ultimate_readonly` / `controls_dash_readonly`.
Pinned by `TheUltimateChordAndDashGestureAreExcludedFromRemapping`.

**A4: `Settings.tscn`'s root is a script-less `Control`, and `SettingsMenu` stays a `CanvasLayer`
that instantiates it.** All three openers construct the class directly (`new SettingsMenu()`), and
those call sites live in files owned by A1 (`PauseMenu`, `LocalFighterPause`) and B4 (`MainMenu`).
Putting the script on the scene root would have required editing all three in parallel worktrees for
no user-visible gain. The result still satisfies §2.6 — one authored scene, embedded by all three —
with zero edits to the openers. Pinned by `SettingsMenuSceneTests::TheAuthoredSceneInstantiatesWithAllFourTabs`.

**A4: closing Settings never writes `SceneTree.Paused`.** `SettingsMenu.Close()` saves, hides, and
raises `Closed`; `_ExitTree` clears only its own listening state. The old `OnClosePressed` set
`GetTree().Paused = false` unconditionally, which resumed gameplay behind a still-open pause menu
whenever a player opened Settings from a pause. Pinned by `ClosingTheScreenLeavesThePauseOwnerInCharge`
and `LeavingTheTreeDoesNotStealThePause`.

**A4: display persistence lives on `ViewportEnforcer`, and the fields are
`ResolutionWidth`/`ResolutionHeight` rather than a single `Resolution`.** `ViewportEnforcer` is the
last autoload, so `SaveManager`'s global payload is already loaded when it runs, and it already owns
every other window-level concern; `GameManager` owns scene flow and would have been the wrong home.
Two ints beat a packed string or a `Vector2I` because `Normalize()` can snap them onto the supported
table (`GlobalSaveData.SupportedResolutions`) — a corrupt payload otherwise asks the engine for a
0x0 window. `WindowMode` is a three-value enum (windowed / fullscreen / borderless), replacing the
old boolean fullscreen toggle. The DisplayServer calls are skipped when `DisplayServer.GetName()` is
`headless`, or every test session would log window errors. Pinned by
`DisplaySettingsPersistenceTests`.

**A4: the dead difficulty dropdown is removed, not wired up.** Campaign difficulty is chosen per save
slot at creation and locked for the playthrough (`SaveManager.CreateStorySlot`); a global "default
difficulty" has no consumer and would be a second canonical value. `settings_difficulty` stays in
`en.csv` as an orphaned key for C1's unused-key sweep to report. Pinned by
`TheDeadDifficultyDropdownIsGone`.

**A4: `SaveManager.LastLoadNotice` became a key + args pair and is surfaced on the main menu.** The
eight raw-English notice strings are now `save_notice_*` keys; `LastLoadNoticeKey` /
`LastLoadNoticeArgs` hold the pair and `LastLoadNotice` resolves it through the translation server, so
the existing property name and shape still work. `MainMenu.AddSaveLoadNotice` is a single additive
static method plus one call — **B4 must carry this line into the authored menu** when it converts
`MainMenu`. Exception text (schema/JSON failures) rides the pass-through key `save_notice_error`
(`{0}`), because engine exception messages are not localizable. Pinned by
`SaveLoadNoticesResolveThroughTheTranslationTable`.

**A4: the `# Package 8 A4` marker in `en.csv` is a bare comma-less line.** Godot's CSV translation
importer and every test-side parser in the repository skip a line with no delimiter, so the marker
imports cleanly and never becomes a key. Verified by `--headless --import` (no CSV warnings) and by
`EnemyRosterContentTests.EnglishTranslationTableHasNoDuplicateKeys`, whose parser requires a comma.

### A2 — Audio framework (2026-08-08)

**A2: the stem mix is additive, not "one layer audible at a time".** §3 A2's parenthetical
("muted except active layer") describes the *mechanism* — all three stems run and only volumes
move — but `design-godot.md`'s Vertical Layering says combat "**Adds** drums, heavy percussion, and
basslines" and climax "**Adds** lead guitars", and `StageAudioSet.AmbientStem` is documented as
"always audible while the match is live". So ambient holds 1.0 at every intensity, combat layers on
from Combat, climax on from Climax. `StemDirector.TargetMix` is the single table;
`StemDirectorTests.TheMixIsAdditiveAcrossIntensities` and
`ClimaxAddsOnTopAndFallingBackToAmbientDropsBothUpperLayers` pin it. B5 should treat the intensity
as "how much is layered on", not "which track is playing".

**A2: fades are a pumped interpolation, not `Tween` nodes.** Both the stem crossfade and the
snapshot blend advance through `AudioManager.AdvanceFades(delta)`, called once from `_Process`.
Reasons: one pump point for the whole framework, no per-transition node churn during gameplay, and
— decisively — a deterministic seam tests can step without waiting on engine frames. The curve is
still what the design specifies (linear amplitude interpolation over the authored
`CrossfadeSeconds`). `StemDirectorTests.SwitchingIntensityCrossfadesOverTheAuthoredDurationRatherThanSnapping`
checks the half-way point, not just the endpoints. The single-track `PlayMusic` fade does use a
`Tween`, because it is a one-off with no test surface worth a seam.

**A2: the authored layout carries low-pass filters on Music and SFX, enabled at a transparent
cutoff.** The design's pause/low-health snapshots muffle rather than only duck, so the effects have
to exist before a snapshot can drive them. Authoring them *disabled* would mean toggling an effect
mid-tween (an audible click); authoring them at 20500 Hz costs a pass that changes nothing until a
snapshot lowers the cutoff. `AudioSnapshotMixer` writes cutoffs by minimum across active snapshots,
not by sum — cutoffs cannot add. `AudioBusLayoutTests.MusicAndSfxCarryALowPassFilterAndMasterCarriesALimiter`
and `AudioSnapshotMixerTests.FilterCutoffsStackByMinimumAndRestoreTheNextMostAggressive` pin both
halves. The mixer degrades to volume-only if a bus has no filter, so a stripped layout cannot throw.

**A2: no snapshot offsets Master.** Master carries a base volume (the master slider) but is outside
`AudioSnapshotMixer.MixedBuses`, because ducking Master would duck the UI bus the pause snapshot is
simultaneously boosting to keep prompts legible. `AudioBootVolumeTests` asserts the Master base
lands on the bus; `AudioSnapshotMixerTests` never sees Master move.

**A2: the rewind duck is now an offset over a mixer-owned base, and that is a real bug fix.**
`RewindPresentationOverlay` used to read the Music bus's absolute dB on entry and write it back on
exit, so a volume change made during a rewind was silently reverted when the rewind ended. The
authored `MusicDuckDecibels` still wins — it is passed through
`ApplySnapshot(AudioSnapshot.Rewind, decibels)` rather than the snapshot table's default, so
`ChronalRewindManager` keeps owning the number.
`AudioSnapshotMixerTests.ASettingsChangeDuringAnActiveSnapshotSurvivesItsRelease` is the regression
guard. B6 adds the ghost/sweep/tick on top of this same overlay — the duck is done, do not re-touch it.

**A2: the `Recovery` presentation phase has no generic placeholder sound.** Telegraph, Active and
Death get generic cues; recovery fires on every ability of every enemy several times a second and a
default there is noise, not information. An explicit `RegisterPresentationSound(id + ".recovery", …)`
still plays. `AudioPresentationBindingTests.RecoveryHasNoGenericSoundButAcceptsAnExplicitOne` pins
it, and `AnEventIDThatAlreadyCarriesItsPhaseSuffixIsNotDoubleSuffixed` guards the one real trap in
the key scheme: `EnemyController` already emits `{enemyID}.death`, so the suffix must not be appended twice.

**A2: placeholder cue assets are Ogg Vorbis generated by a committed script under a new `tools/`
directory.** The paths are `.ogg` because `FighterSimulationDriver` (owned by A3/B2/B5) already
holds them as string constants, and A2 must not edit that file — so the assets had to match the
callers, not the other way round. Godot cannot encode Vorbis, hence
`tools/generate_placeholder_audio.py` (numpy + soundfile); the `.ogg` files and their `.import`
sidecars are committed, the script documents its own dependencies, and production audio replaces
the files in place with no code change. `PlaceholderCueAssetTests` pins the exact paths against the
`AudioManager` constants, because `PlayCue` resolves through `ResourceLoader.Exists` and a moved
file goes silent rather than erroring — which is how these cues had been no-oping since Package 6.

**A2: two dead APIs removed, one revived.** `TransitionToCombatStem`/`TransitionToAmbientStem` were
instant `VolumeDb` writes on players that never received a stream and had zero callers; the
`StemDirector` replaces them. The orphaned runtime `Ambient` bus is gone
(`AudioBusLayoutTests.NoOrphanedAmbientBusRemains`). `PlayMusic`'s `fadeDuration` parameter, which
was accepted and ignored, now actually fades. `ReleaseAllVoices()` is new — scene changes should not
leak a half-played hit from the level you just left.

**A2: the bus layout lives at `resources/Audio/default_bus_layout.tres`, not the engine's default
`res://default_bus_layout.tres`.** Registered explicitly under `audio/buses/default_bus_layout` so
the routing is visible in project settings and the audio content stays in one directory.
`AudioBusLayoutTests` asserts the project setting and the resource path agree; without that pairing
a rename would silently fall back to a bare Master bus and every sub-bus routing decision in the
framework would collapse onto it.

**A2 → B5/B3 API surface.** `AudioManager.RegisterStageAudio(StageAudioSet)` /
`SetIntensity(StemIntensity.Ambient|Combat|Climax)` / `ReleaseStageAudio()`;
`ApplySnapshot(AudioSnapshot)` / `ApplySnapshot(snapshot, musicOffsetDb)` / `ReleaseSnapshot` /
`ReleaseAllSnapshots` / `IsSnapshotActive`; `PlayChirp(pitchScale)` (UI bus),
`PlayFootstep(surfaceId)` (Movement bus), `PlayUISound(stream, pitchScale)`,
`PlayCountdownBlip(pitchScale)`, `PlaySFX(stream)` (SFX bus), `PlayOneShot(stream, bus, pitch,
volumeDb)`; `RegisterPresentationSound(key, stream)` / `ClearPresentationSounds()` /
`RegisterFootstepSound(surfaceId, stream)`; `ApplySavedVolumes()`, `ReleaseAllVoices()`,
`AdvanceFades(delta)`. Cue path constants: `AudioManager.KnockoutStingerPath`,
`VictoryFanfarePath`, `CountdownBlipPath`. Bus names: `AudioBuses.{Master,Music,SFX,UI,Combat,
Movement,Environmental}`. Nothing in A2 subscribes to gameplay events except
`EventBus.OnEnemyPresentation`, so B5 owns every other trigger point.

### ORCHESTRATOR — Phase A integration (2026-08-08)

**INTEGRATION-A: all four framework branches merged (order A1→A3→A4→A2 by completion; the declared
A1→A2→A3→A4 order proved unnecessary as no declared overlap materialized into a code conflict).**
Only `docs/PACKAGE8_PRESENTATION_PLAN.md` §9 and `localization/en.csv` marker blocks conflicted;
both union-resolved. The compiled translation was regenerated and committed at the wave boundary
per §2.8 (A4's compiled-translation assertions require it).

**INTEGRATION-A: validation.** Build clean (pre-existing vendored `CS8632` only); `--import` clean;
full suite **1060 passed / 0 failed / Total 1060** across two consecutive serial runs — exactly
907 baseline + 30 (A1) + 49 (A3) + 28 (A4) + 46 (A2), no cross-workstream loss.

**INTEGRATION-A: notes for Phase B, accumulated from §9 blocks:**
1. Theme adoption = set `Theme` on the screen root; focus via `FocusChainBuilder.Apply`; loading
   copy uses raw keys + control auto-translation (the pattern C1's scanner will expect).
2. The stem mix is ADDITIVE (ambient always audible; combat/climax layer on top) — B5 reads
   `SetIntensity` as "how much is layered on". Full A2 API list is in its §9 block.
3. gl_compatibility shaders: `TEXTURE` cannot be passed as a `sampler2D` argument (A3 inlined all
   sampling; a test guards the regression). Pooled nodes bind signals in `_EnterTree`, not
   `_Ready`.
4. A4's `MainMenu.cs` touch is exactly one call + one static method (`AddSaveLoadNotice`) — B4
   carries it into the authored menu.
5. `settings_difficulty` is a deliberately orphaned key feeding C1's unused-key sweep.

### B2 — Fighter HUD & match presentation (2026-08-08)

**B2: `TestArenaHUD` was demoted, not absorbed, and it hides itself rather than being removed from
the eleven scenes that carry it.** The plan left the choice open. Absorbing the debug text into
`FighterHUD` would have meant deleting the `HUDLayer` node from `scenes/arenas/TestArena.tscn` and
all ten `scenes/fighter/FighterStage_*.tscn` — and B7 owns those ten scenes exclusively (§2.7), so
a parallel worktree would have had to edit them. Instead the script alone changed: `TestArenaHUD`
sets `Visible = false` in `_Ready` and toggles on `F3`. Zero stage scenes touched, and the
state/tick/hash line survives, which is the only in-game view of deterministic simulation state and
the thing you want during a desync. Pinned by
`tests/Unit/FighterDebugOverlayTests.cs::TheDebugOverlayIsHiddenOnLoad` — the regression it guards
is invisible in review and glaring in a build.

**B2: the debug toggle is a raw `Key.F3`, deliberately not an InputMap action.** Every InputMap
action now shows up as a rebindable row in A4's Controls tab, so adding `debug_hud_toggle` would
advertise a developer affordance as a player feature and force A4 to special-case it the way it
already special-cases the ultimate chord and the dash gesture.
`TheToggleKeyIsNotAnInputMapActionAndCannotBeRebound` walks every authored action and fails if a
future binding collides with F3 — that is the real risk, since a collision would silently flash the
debug layer during play. The consequence: the overlay is undiscoverable in game. That is intended;
it is documented here and in the class summary, not surfaced in the UI.

**B2: the HUD's stock and shield pips and its four cooldown slots are built in code inside an
otherwise authored scene.** Their counts are data-driven — stock rules come from `MatchSettings`,
shield capacity from each character's authored `MaxBlockCharges` — so authoring a fixed number of
pip nodes would either cap the HUD at today's defaults or leave dead nodes hidden in the scene.
Everything with a fixed shape (panels, portraits, bars, labels, the clock) is authored.
`FighterHudContentTests.TheAuthoredSceneCarriesEveryNodeTheScriptBinds` lists all eighteen bound
node paths, because the script resolves widgets by path and a rename would produce a HUD that
renders nothing rather than one that errors.

**B2: the HUD takes component values as parameters instead of reading the simulation.**
`ApplyPlayerState(playerIndex, in state, in runtime, tickRate)` and
`ApplyMatchState(mode, frames, tickRate)` are the whole input surface; `_Process` pulls from the
driver and calls them. This is what lets `FighterHudSceneTests` drive every widget from hand-built
structs without standing up a match, and it is also the structural reason the HUD cannot become a
second route into `scripts/FighterSim/`.

**B2: the ultimate cooldown slot is meter-gated, and that is not a cosmetic choice.** There is no
ultimate cooldown field — `FighterRuntimeComponent` carries basic/special1/special2/movement only,
and `FighterSimulationSystems` gates the ultimate on `Influence >= 100`. A slot that showed a
cooldown there would be inventing state.
`CooldownSlotsReadReadyOrRemainingTimeAndTheUltimateIsMeterGated` pins the asymmetry so a later
refactor does not "fix" it into a fourth cooldown.

**B2: the clock rounds *up* and is hidden in Stock mode.** `FighterMatchComponent.RemainingFrames`
counts down in every mode, including Stock, where it decides nothing; showing it would be a timer
that does not matter. And flooring the seconds would display `0:00` for a full second while the
fighters are still playing, which is the single most confusing thing a match clock can do.
`FighterHudModelTests.TheClockRoundsUpSoALiveMatchNeverReadsZero`.

**B2: every per-frame widget write is diffed, and the cooldown cache is a tri-state.** The HUD
refreshes at 60 Hz, so unconditional `AddThemeColorOverride` / `StyleBoxFlat.Duplicate()` calls
would allocate on every frame of every match against a budget that forbids gameplay-time GC spikes.
The trap found in testing: a plain `bool` "presented ready" cache cannot express *never presented*,
so the first refresh of a slot that is already cooling diffs as unchanged and leaves the plate
showing the available treatment. The cache is `int` (-1 unknown / 0 cooling / 1 ready).

**B2: `HUDController.cs` is deleted.** It was an orphaned code-built bars HUD with an empty
`UpdateStocks()` stub and zero references anywhere in the repository; leaving it would leave a
second, uncoordinated Fighter HUD for someone to wire up by mistake. The build is the proof that
nothing referenced it, and `FighterHudContentTests.TheRetiredLegacyHudControllerIsGone` stops it
coming back. **Two notes for C1:** the `story_hud` manifest row names `HUDController` as its owner
and now points at a deleted class — B1 owns `StoryHUD`, so that row's owner needs correcting
alongside the planned status flip; and the `fighter_hud` row still points at
`res://scenes/arenas/TestArena.tscn` with owner `TestArenaHUD`, which should become
`res://scenes/ui/FighterHUD.tscn` / `FighterHUD`.

**B2: the presentation overlay's scrim is owned by exactly two phases, and the rest return "no
opinion".** `FighterOverlayModel.DimAlphaFor` returns a negative value for every phase except
`Spotlight` and `Results`. The KO sequence raises `Spotlight` and the stamp in the same frame, so a
phase that returned `0f` meaning "no dim" would clear the dim the spotlight is holding.
`ASpotlightDimIsNotClearedByALaterBannerBeat` walks the real KO order.

**B2: the overlay gained a `Present(payload)` seam and the tests use it instead of the bus.** The
`EventBus` handler is a one-line forward. Driving the surface directly means a failure names the
phase that broke rather than a subscription that did not fire, and the suite cannot leave a stray
subscriber on the autoload if a test aborts mid-way.

**B2: countdown blips rise across 3-2-1 and peak on GO** (0.9 / 1.0 / 1.1 / 1.45 through A2's
`AudioManager.PlayCountdownBlip`), so the start of a match is audible without reading the banner.
The call is null-safe: `AudioManager.Instance` is absent in a bare test tree.

**B2: `MatchResults` grabs focus in `ShowResult`, not in `_Ready`.** `FocusChainBuilder.Collect`
skips invisible subtrees by design, so a chain built while the panel is still hidden collects
nothing and a controller player lands on nothing with no way off the screen.
`TheResultsPanelStampsTheOutcomeAndTakesFocus` asserts the viewport's focus owner is a Button.

**B2: driver edits are four lines in the UI-attach block.** One field, one `InstantiateUI` call and
one `Bind` call inside `AttachMatchFlowUI`, using `FighterHUD.ScenePath`. A3's presentation-sync
code and B5's future audio block are untouched; `Bind` is called there because that method is the
only place holding both `PlayerController`s and therefore both `CharacterData` portraits.

**B2 validation.** Build clean (pre-existing vendored `CS8632` only); local `--headless --import`
run and the compiled translation left **uncommitted** per §2.8; full suite
**1103 passed / 0 failed / Total 1103 = 1060 + 43**; headless smokes clean for `TestArena`,
`FighterStage_Florence`, `FighterStage_Berlin` and `CharacterSelect`.
`FighterMatchFlowContentTests` and `LocalFighterPauseTests` passed unmodified. Test delta **+43**.
Not delivered by B2: A1's VS loading variant already reads what it needs from `SessionData`, so
nothing was added for it; production HUD art, animated bar interpolation, and cinematic KO
presentation remain later work.

### B7 — Parallax2D migration and stage lighting (2026-08-08)

**B7: the `ParallaxBackground` container became a plain `Node2D` at `z_index = -200`, not a
`CanvasLayer`.** `Parallax2D` replaces `ParallaxLayer` one-for-one, but nothing replaces the
*container* — one `Parallax2D` **is** one layer. Keeping a negative `CanvasLayer` around them would
have preserved the old z-order for free and thrown away the reason to migrate: content in a
negative canvas layer is invisible to the `CanvasModulate` and the `PointLight2D`s this same
workstream adds, because 2D lighting and canvas modulation act **per canvas**, and a `CanvasLayer`
owns its own. Post-migration every stage's parallax sits in canvas layer 0 and is lit with the rest
of the world. The group node carries the depth and its children step up from it (`z_index` 1 and 2
→ effective −199 / −198, with any non-scrolling `BackdropBase` left at −200 beneath them), which is
well under every authored tint (−30 … −100) and prop (−5 … −25), so each scene's *relative* layering
is unchanged.

**B7: the container node is now named `Parallax` on all nine stages.** Seven were named
`ParallaxBackground` — the class name of a node type that is no longer there. Renaming was free
(only the nine test files referenced the paths) and the alternative was a `Node2D` called
`ParallaxBackground`, which is exactly the kind of thing a later agent "fixes" by changing the type
back. `motion_scale` → `scroll_scale` and `motion_mirroring` → `repeat_size` are literal transfers;
`repeat_times` is new and had to be set to 3 alongside every `repeat_size`, because it defaults to 1
and a repeating layer that draws a single tile leaves a hard edge mid-pan.
`FighterStageParallaxMigrationTests.EveryRepeatingLayerCarriesBothRepeatSizeAndARepeatCount` pins
the pair; five layers across Alexandria, Chicago and Nassau are affected.

**B7: the migration changes one thing a headless gate cannot see — the parallax now zooms with the
camera.** `ParallaxBackground` drew in canvas-layer space, so `FighterCamera`'s 1.0–1.4 zoom band
never touched it; `Parallax2D` is world-space and scales with everything else. This is the normal
behaviour for the successor node and arguably the better look, but it *is* a visual change on all
nine stages and it belongs to the same "no automated gate can discharge this" bucket as A3's shader
output. The scroll *rate* is unchanged: `Parallax2D` positions itself at
`−screen_offset · scroll_scale` in world space, which produces the identical screen displacement the
old `motion_scale` did.

**B7: the opaque-backdrop guard was rebuilt around effective draw order rather than around a tint's
alpha, and it is a stronger test than the one it replaces.** Package 6 C1's version encoded the old
semantics directly — "a stage may pair a `ParallaxBackground` with `Presentation/BackdropTint` only
if the tint is translucent" — which under the new node type is both too strict (an opaque rect below
the parallax is fine, and five stages deliberately author one) and too weak (it only ever looked at
one hard-coded node path). `NoStageHidesItsParallaxBehindAnOpaqueFullBleedRect` now walks the whole
scene accumulating each canvas item's real `(canvas layer, z_index)` with `z_as_relative`
compounding, and fails if **any** opaque full-bleed `ColorRect` outside the parallax subtree sorts at
or above the shallowest parallax content. It carries two vacuity guards, not one: at least nine
stages must be found carrying a parallax, and at least six opaque full-bleed rects must be *detected*
somewhere — without the second, a `Control.GetRect()` that stopped resolving outside the tree would
turn the whole sweep into a no-op that still passed.

**B7: Florence is deliberately not given a parallax.** It predates Package 6 and dresses its
distance with a single static `Sprite2D`; the invariant this workstream owns is "authored parallax is
visible", not "every stage must have one". Its opaque `BackdropTint` is therefore still legal and
still detected by the guard's full-bleed pass (it just has no parallax to hide). Both the guard and
the migration sweep state the floor as nine rather than ten for exactly this reason.

**B7: `StageLightingRig` drives everything from exports on its root, with property setters that
apply immediately.** A stage tunes its era in one node-override block; nothing reaches into the
instanced scene's children with `index=` blocks. The setters call `Apply()` (guarded on null
children) so an instanced override lands as the scene is built, and `_Ready` calls it again for a
rig constructed in code — which is what lets `StageLightingRigTests` read the resolved child state
without ever entering the tree.

**B7: every ambient tone is held above 0.55 on every channel, and that is a hard floor, not taste.**
A `CanvasModulate` multiplies the whole canvas layer including the fighters, and this project's
characters are flat placeholder silhouettes with no rim art to survive a crush. Era identity
therefore comes from the channel *ratio* and from the light colours. The authored tones, brightest
to darkest: Gettysburg `(0.86,0.84,0.76)` dusty daylight, Alexandria `(0.86,0.80,0.68)`,
Florence `(0.86,0.79,0.70)`, Vesuvius `(0.88,0.74,0.66)`, Paris `(0.84,0.76,0.76)`,
Globe `(0.80,0.74,0.84)`, Chicago `(0.78,0.84,0.88)`, Nassau `(0.76,0.82,0.86)`,
Orléans `(0.72,0.75,0.86)`, Berlin `(0.70,0.74,0.80)` — the coldest, as the searchlight stage should
be. Hub `(0.80,0.84,0.90)`, Level 02 reuses Orléans', Level 06 reuses Vesuvius'.
`StageLightingRig.MinimumAmbientChannel` is the constant and
`EveryLitSceneInstancesTheRigWithAReadableAmbientTone` sweeps all thirteen scenes against it. The
test also refuses an ambient alpha below 1, which fades the entire layer rather than tinting it.

**B7: key-light colours follow each stage's catalog `AccentColor` where the era reads as a light
source, and deliberately diverge where it does not.** Chicago's coil cyan, Vesuvius' ember, Globe's
purple rim and Alexandria's gold are the accent; Berlin's accent is the wall's red stripe, which is
paint and not illumination, so its key/fill are cold searchlight white-blue from the two authored
tower masts and the accent red is demoted to the rim. Nassau splits the difference — a warm lantern
key on deck with the accent cyan as the sea-side fill.
`EveryFighterStageTunesTheRigToItsOwnEra` requires nine distinct tones and eight distinct key
colours across the ten stages, because ten scenes that instanced the template without overriding it
would satisfy every other assertion in the suite while shipping one look for the whole catalog.

**B7: campaign levels get `FollowsCamera`, Fighter stages do not.** A level is thousands of pixels
wide and builds its geometry at runtime, so three fixed lights would only ever dress the opening
room; with the flag the rig glides to the active `Camera2D` each frame and the key/fill pair stays
meaningful everywhere. A Fighter stage is one authored screen and its lights are placed against real
geometry (Chicago's two coil towers at x 575/1325, Berlin's mast tops at 700/950, Vesuvius' lava
seams), so following the camera there would flatten the very placement that gives it identity. The
suite asserts no stage sets it. The hub and levels 2/6 also disable the rim light: a third light with
nothing authored to rim is cost without an image.

**B7: `StoryItemPickupTemplate`'s `Glow` was still an inert texture-less `PointLight2D`; A3 had not
fixed it.** A `PointLight2D` with no texture emits nothing at all, so that light had been dead since
it was authored and was the repository's only Light2D before this pass. It now carries A3's
`glow_light_gradient.tres` at `texture_scale` 0.9 with the pickup's cyan.
`TheStoryPickupGlowIsNoLongerAnInertTexturelessLight` pins it, and the same texture-presence
assertion covers all three rig lights — the failure mode is identical and neither version errors.

**B7: `--headless --import` rewrote eight `addons/gdUnit4` `.import` sidecars with real content
changes, not the usual line-ending churn.** The worktree path resolves the addon directory as
`gdunit4` where the repository has `gdUnit4`, so the importer rewrote `source_file` and the
`.godot/imported` hashes. Unrelated to this workstream and discarded, along with the line-ending-only
rewrites of the other ~70 sidecars and `resources/Audio/default_bus_layout.tres`. `en.csv` was not
touched, so no compiled translation artifact moved.

**B7 validation.** `dotnet build` clean — the single pre-existing vendored `CS8632`, and **the two
`#pragma warning disable CS0618` blocks and six untyped-`GetClass()` workarounds are gone**, which
was the point of doing the migration with the suites in one change. Full suite **1067 passed / 0
failed / Total 1067** across two consecutive serial runs — exactly the post-Phase-A 1060 plus 7
(`FighterStageParallaxMigrationTests` ×2, `StageLightingRigTests` ×5); the nine per-stage suites and
`FighterStagePresentationTests` kept their case counts, and `FighterStageConformance` was not
touched and stayed green for all ten stages. `--headless --import` clean. Headless `--quit-after 300`
smokes: all ten Fighter stages plus `HubWorld`, `Level_02_Orleans` and `Level_06_Pompeii` — thirteen
for thirteen, exit 0, zero `ERROR`/`SCRIPT ERROR` lines. One contention run was discarded first
(exit 100 with `Failed to connect: Connection timeout`, CLAUDE.md failure signature 5).

**B7: not delivered.** Nobody has *looked* at any of this. A headless gate proves the scenes load,
the node types are right, the scroll factors are distinct and the ambient tones clear the floor; it
cannot tell you whether the parallax reads at 1.4× zoom, whether the key light lands where the era
wants it, or whether Berlin is now too cold. Also out: `LightOccluder2D` and any shadow casting (no
occluder geometry is authored anywhere), normal maps, per-room level lighting beyond the two
exemplars, and the remaining thirteen campaign scenes — the rig is the pattern, and applying it is
per-level content work.

### B1 — Story HUD, results, enemy bars, HUD opacity (2026-08-08)

**B1: the boss bar's notches are the authored `PhaseThresholds` array, passed through rather than
re-derived.** `BossBarPhaseNotches.Normalized` returns the threshold values themselves as bar
fractions, because `BossController.CheckPhaseTransition` advances when the remaining HP fraction is
`<=` the next entry — so the notch and the transition are the same number by construction, not by
agreement. `BossEncounterController.Reveal` forwards `Data?.PhaseThresholds` into a new fourth
`ShowBossBar` parameter (the three-argument overload still exists and draws a plain bar). The sweep
in `BossBarPhaseNotchTests` walks all fifteen authored bosses rather than a representative one: the
roster is eight single-threshold bosses, six with two, and the single-phase Mirror Paradox with an
empty array, so a hardcoded "thirds" would have looked correct on six of fifteen. `PhaseAt` is
pinned against the `<=` boundary specifically, because a notch on the other side of that comparison
lights up one hit late for an entire fight and no smoke test would catch it.

**B1: unusable thresholds are dropped, not clamped.** A hand-edited `0`, `1`, negative, NaN or
duplicate entry yields *fewer* notches rather than a stripe pinned under the bar's own border, where
it reads as a rendering bug rather than as bad data. Pinned by
`UnusableThresholdsAreDroppedRatherThanClampedOntoTheBarEnds`.

**B1: `HudOpacity` is polled, not evented, and the decision is deliberate.** There is no
settings-changed signal anywhere in the project, and `SettingsMenu` — the only writer — belongs to
A4 this package, so adding one would have meant editing a file B1 does not own. `HudOpacityBinder`
compares a float against the autoload field once per frame and writes only on change; that also
picks up a change made by a save load or migration, which a settings-only event would miss.
`StoryHudPresentationTests.HudOpacityIsAppliedAtReadyAndAgainWheneverTheSettingChanges` drives
`_Process` directly and asserts the value moves twice after `_Ready`, because a one-shot read (the
pre-existing `TestArenaHUD` pattern, and the §2.10 bug) passes any test that only checks
construction. Only the alpha channel is written, so the boss bar's red and the meter's blue survive.

**B1: enemy overhead bars are hidden until first damage and fade after four quiet seconds; the
reset lives on both halves of the pool cycle.** `OverheadBarVisibility` is a pure model owned by
`EnemyController`, reset in `OnSpawn`, `OnDespawn` *and* `ApplyStoryRewind`. `OnDespawn` also writes
the widgets, not just the flag — a released body can still render for a frame, and a pool bug of
this shape only shows up once a level happens to recycle a body the player already fought. Bosses
are untouched: `Boss.tscn` has no overhead widgets at all and uses the HUD's big bar, so "bosses
keep the big bar" needed no code. A zero-damage hit does not reveal the bar (nothing landed), and a
Venom tick does (the player caused it). Pinned by `OverheadBarVisibilityTests` (state machine,
degenerate configuration, non-positive delta) and `EnemyOverheadBarTests` (both authored scenes,
pool cycle, rewind, live opacity).

**B1: the ultimate indicator is meter-gated, not cooldown-gated, and the model refuses a cooldown
on that slot.** Every kit raises `OnCooldownStarted` for Special 1/2 and the movement ability;
nothing raises it for the ultimate, which is gated purely by the Influence meter reaching 100. A
model that treated all four slots alike produced an ultimate icon that never lit up, which is how
this was found. `HudAbilityIndicatorModel.StartCooldown` ignores `AbilitySlot.Ultimate` outright so
a stray payload cannot blank an icon the player can legitimately press;
`ReadinessFraction(Ultimate)` reports the meter fraction, which is the same "how close am I"
reading. `EventBus.OnCooldownComplete` has no raiser anywhere in the repository — it is subscribed
anyway, as a zero-duration release, so a future raiser works without a HUD change.

**B1: the status pip reads `GlowPalette`, not `UIPalette`.** §4 B1 says "icon/tint per status via
UIPalette", but `UIPalette` carries no status colours and A3 already owns the authored five in
`GlowPalette.Status`. Adding them to `UIPalette` would have created a second canonical palette for
the same five statuses, and the visible failure mode — a character outlined in one colour with a HUD
pip in another — is exactly the drift the palette work exists to stop. The key family is reused for
the same reason: `HudAbilityIndicatorModel.StatusLabelKey` builds the existing `status_*` keys by
lowercasing the enum name, and `StoryHudLocalizationTests` sweeps every `StatusType` value against
`en.csv` so a new status cannot ship rendering a raw key.

**B1: `StoryManager` gained the two run statistics the results overlay needs; neither existed and
one could not be derived.** Completion time was tracked nowhere. Rewinds *used* cannot be
differenced out of `ChronalRewindsRemaining`, because Easy and Normal refill that pool at every new
checkpoint — differencing under-reports precisely the runs where the player used the most rewinds.
So the count is taken from `EventBus.OnRewindTriggered` and the clock from a `_Process` accumulator,
both on the autoload rather than on a level controller: the Tutorial and Florence deliberately do
not extend `StoryLevelControllerBase`, and a statistic authored twice would drift between the two
families. `BeginLevelRun` fires from `LoadCurrentLevel` (so a Timeline Collapse restart is a fresh
attempt, not a continuation), `ReturnToHub` stops the clock, and `OnLevelComplete` freezes
`LastLevelCompletionSeconds`/`LastLevelRewindsUsed` — which is the pair `LevelResultsPanel` reads,
frozen *before* `PresentCompletion` runs. No level controller was edited. Pinned by
`LevelResultsStatsTests`.

**B1: `FormatDuration` is not a translation key.** `M:SS` (growing an `H:` field past an hour) is
digits and colons, which carry across every language this project plans to ship; the localised half
is the surrounding sentence (`results_completion_time`). A negative or unstarted clock floors to
`0:00` rather than rendering a negative time.

**B1: both `BuildFallbackUI` duplicates are deleted, and the authored scenes had already drifted.**
`StoryHUD` and `LevelResultsPanel` are constructed only through their own `CreateDefault`, both
authored scenes are committed and are now asserted to exist by their suites, so the code-built
copies were pure duplication — and the proof that duplication was already costing something is that
`LevelResults.tscn`'s Return button carried *no text at all* while the fallback set it, so anyone
who saw the authored panel saw a blank button. `CreateDefault` now reports a missing scene through
`GD.PushError` and returns a bare instance rather than null, because level controllers call into it
unconditionally; every accessor is null-guarded. `TimelineRestartPanel` stays code-built — it is a
single hub-owned panel with no authored scene to converge on, and authoring one was not in scope.

**B1: `LevelResults.tscn`'s button text is now a raw translation key, following A1's pattern.** The
authored control resolves it through Godot's automatic control translation, so a language change
follows without rebuilding the surface, and the test asserts the raw key (`"results_return_hub"`)
rather than the English string.

**B1: the Timeline Collapse full restart is confirmed; resuming from the anchor is not.** The two
options sit one row apart and only one of them discards the Timeline Anchor the player already
reached, so the destructive one goes through A1's `ConfirmModal`
(`timeline_restart_level_confirm`). The confirmation is parented beside the button column rather
than inside it, so its hidden buttons never join the panel's focus chain — pinned by
`TheConfirmationSitsOutsideTheFocusChainItGuards`, which counts the chain at exactly three.

**B1 validation.** Build clean (pre-existing vendored `CS8632` only); `--headless --import` clean;
headless smokes clean for `HubWorld`, `Level_02_Orleans` and `Level_15_Alexandria`. Full suite
**1108 passed / 0 failed / Total 1108** — exactly 1060 + the declared **+48**, no cross-workstream
loss. No existing suite was modified. `localization/en.csv` gained seven keys under the
`# Package 8 B1` marker; per §2.8 the regenerated `en.en.translation` is **not** committed, so
`StoryHudLocalizationTests` needs the orchestrator's wave import (or a local one) to go green.
Contention note for later agents: the first three attempts at this suite returned the plausible
green `Total: 15` (this workstream's pure-C# subset) with exit code 100 — signature 5, not a
regression; polling `Get-Process testhost`/`Godot*` to zero before launching is what cleared it.

### B3 — Dialogue & narrative presentation (2026-08-08)

**B3: the emotion label was replaced, not restyled, and the localized string survives as the
portrait's tooltip.** The old presentation printed `(Determined)` beside the speaker name — stage
direction typed onto the page, which no shipping dialogue system does. Emotion now colours the
portrait: a frame in the emotion's `UIPalette` accent (neutral cyan-dim, determined gold, shocked
cyan, confused slate-dim, injured boss-red) plus a near-white tint over the texture. Keeping the
localized string as `TooltipText` does two jobs: the five `emotion_*` keys stay referenced so C1's
unused-key sweep does not report them as dead, and the information stays reachable for a player who
cannot read colour. Tints deliberately hold every channel at >= 0.8 — the portrait is the character's
face, and a heavier wash recolours their skin rather than suggesting a mood, a bug that would only
become visible once production art lands. Pinned by `DialogueEmotionTreatmentTests` (mapping, palette
provenance, unknown-key fallback, tint ceiling) and
`DialoguePresentationTests.ThePortraitFrameAndTintFollowEachLinesAuthoredEmotion`, which walks the
real `level_08.postboss` injured → neutral → confused run.

**B3: "glass" under `gl_compatibility` is a translucent fill plus a drop shadow, not a blur.** A
frosted panel needs a backbuffer copy and a blur pass, and A3 already recorded how narrow the shader
envelope is on this renderer. The treatment is therefore everything a `StyleBoxFlat` can do: a navy
fill at 0.72 alpha so the scene reads through, a soft shadow that lifts the box off the world, a
translucent cyan accent border, and a 10 px corner radius against the utility panel's 4 px. It costs
one draw call and no shader. It ships as a `DialogueGlassPanel` **theme type variation** on the
shared A1 theme rather than a standalone `.tres`, so the box adopts it with one
`theme_type_variation` line and nothing else in the UI changes; the numbers live in
`scripts/UI/DialogueTheme.cs` and
`DialoguePresentationTests.TheSharedThemeCarriesTheDialogueGlassVariation` pins the two together, the
same contract A1 established between `UIPalette` and `ftt_theme.tres`. This is the only B3 edit to
`ftt_theme.tres` and it is purely additive — no existing entry changed, and `UIThemeTests` passed
unmodified.

**B3: the reveal became a pure model, and the punctuation hold collapses runs of marks.** The old
reveal was `revealed += 30 * delta`, which cannot express a pause and cannot be tested without engine
frames. `DialogueRevealModel` (no Godot dependency) holds 0.26 s after `. ! ?` and 0.12 s after
`, ; :`. A **run** of marks holds once, at its end: twenty-two authored lines use `...`, and three
stacked sentence pauses there read as a stall rather than as a beat. Pumped rather than
`Tween`-driven for the same reasons A2 recorded for its crossfades — one advance point, no node
churn, and a seam a test can step by exact deltas. Pinned by `DialogueRevealPacingTests`.

**B3: chirps fire every third non-whitespace character, and a frame hitch cannot burst them.** One
chirp per character at 30 cps is thirty voices a second — it machine-guns the pooled voices A2 sized
and reads as noise rather than as speech. Every third character lands at a steady ten per second
while text is moving, and stops dead during a punctuation hold, which is what makes the hold audible
instead of merely visible. Whitespace is silent. Separately, `Advance` clamps its own step to 0.25 s:
a scene load or pool warm-up used to dump a whole line in one frame, and with chirps attached that is
also a burst of voices in one frame. Confirm-to-complete lands the line **silently** — the player
asked to stop listening to the reveal. Pinned by
`ChirpsFireEveryThirdNonWhitespaceCharacterAndStopWhileHolding` and
`AFrameHitchCannotFlushTheWholeLineOrBurstItsChirps`.

**B3: only `speaker_player` chirps at a character pitch; Sarah, the bosses and narration are all
neutral.** `CharacterData.DialogueChirpPitch` (the single B3 edit to that file, default 1.0) carries
nine authored values spread 0.72 → 1.34, with Lincoln lowest and Mozart highest exactly as
`design-godot.md`'s Dialogue Presentation section names them by example. Giving each of the eighteen
named NPC speakers its own pitch would have meant a second table of tuning numbers with no resource
behind it, which is what the repository's "resources own the numbers" rule exists to prevent — so
every non-player speaker uses `DialogueManager.NeutralChirpPitch`. One placeholder sample is
pitch-shifted rather than nine samples authored, so production audio replaces the sample without
retuning the roster. Pinned by `DialogueChirpPitchTests` (band, pairwise separation >= 0.04, the
Lincoln/Mozart ordering, and the field default) and
`DialoguePresentationTests.ChirpPitchFollowsTheLockedCharacterAndGoesNeutralForEveryOtherSpeaker`.

**B3: the box slide is applied to the `CanvasLayer`, not to the panel's `Position`.** The panel is
anchored to the bottom of the viewport; writing `Position` on an anchored `Control` rewrites its
offsets, and the layout then fights the animation on every resize notification — and the resting
position sampled at `_Ready` is not yet the laid-out one, so the box would snap to the top of the
screen on the first frame. `CanvasLayer.Offset` shifts the whole layer and touches no layout. The
fade rides the panel's `Modulate` alpha with an ease-out over 0.18 s.

**B3: the gameplay pause is taken on the start frame, ahead of the open animation.** Deferring
`SceneTree.Paused` until the box finished sliding in would leave a ~0.18 s window in which gameplay
still ran under a dialogue box that is already on screen.
`TheGameplayPauseIsTakenOnTheStartFrameNotWhenTheBoxFinishesOpening` asserts both halves — that the
tree is paused *and* that the box had not finished opening, so the case cannot silently stop proving
anything. The close animation runs after `EndSequence`, which means the layer stays `Visible` for
0.18 s after the sequence ends; the pause is released immediately regardless. Both pre-existing
pause-handback cases in `CampaignCompletionTests` stayed green unmodified.

**B3: Credits theming is colour and type only — no flow change whatsoever.** The roll adopts the
shared theme and takes its backdrop, heading, body and skip-hint colours from `UIPalette` instead of
four hardcoded literals. `CampaignCompletionSequence` has no visuals of its own, so
"campaign-completion theming" is exactly the credits. `CampaignCompletionTests` passed unmodified,
including the completion-flag-at-credits-start case.

**B3: no new translation keys, so there is no `# Package 8 B3` marker in `en.csv`.** Everything B3
presents was already authored — the five `emotion_*` keys, `dialogue_continue`, the eighteen credit
keys. The one new user-visible channel is the portrait tooltip, which reuses the emotion keys.
`en.csv` and the compiled `en.en.translation` are untouched by this workstream.

**B3 validation.** Build clean (pre-existing vendored `CS8632` only); `--headless --import` and the
load check clean; full suite **1084 = 1060 + 24** in one clean serial run (two earlier attempts hit
CLAUDE.md failure signature 5 — partial `Total: 90`, exit code 100, three sibling Godot children
alive; the number only landed once the window was actually clear). Headless smokes clean for
`Level_00_Tutorial`, `HubWorld`, and `Level_15_Alexandria`. No existing test file was modified. Not
delivered by B3 and left open: the glass panel's rendered result is unverified by any automated gate
and needs a human look (the same limit A3 recorded for its shader), and the chirp still plays A2's
single placeholder sample — the design's per-character *waveforms* (square for Lincoln, triangle for
Mozart) are production audio, P10.

### B5 — Audio content (2026-08-08)

**B5: the seventeen sets exist at the reserved paths; the manifest rows stay `Planned` for C1.**
`resources/Audio/{tutorial,hub,level_01..15}_audio.tres` are authored `StageAudioSet` resources on
the three shared placeholder stems, exactly as the ten Fighter stage sets are, and every one passes
the shared `StageAudioSetTests.Validate`. Per §2.12 no manifest row was touched.
`StoryAudioSetContentTests.EverySeventeenStorySetExistsAtItsManifestPathAndSatisfiesTheStemContract`
reads the reserved `ResourcePath` out of the manifest and validates what it finds there, so it will
keep passing across C1's flip without an edit.

**B5: the level→set mapping is derived, not authored, and lives in one class.** Neither
`FighterStageData` nor any campaign resource carries an audio-set field, and adding one would have
meant editing ten catalog rows plus the ten stage scenes B7 owns. `AudioSetPaths.ForStoryLevel`
derives `level_NN_audio.tres` from the level id's own two-digit slot (so a later era-token rename
keeps the level's music), maps slot 0 to `tutorial_audio.tres`, and returns "" for anything that is
not a real campaign slot — "" means "attach no director", which is what a bare test fixture gets.
`ForFighterStage` takes the era token that is the first segment of every catalog `StageID`. Both
halves are pinned against the authored files rather than against a duplicate table, including a
negative case, because a wrong derivation is completely silent: the scene simply plays nothing.

**B5: `StageID` on a Story set is the level id, and the hub's is `hub_ship`.** The field is
documented as "must match a catalog StageID", which only exists for Fighter stages. Reusing it for
the campaign identity keeps one schema rather than forking `StageAudioSet` for seventeen rows that
differ in no other way. The hub has no level id at all, so `AudioSetPaths.HubStageID` names the
value once and the content test asserts it.

**B5: combat intensity is driven by engaged enemies, not by living ones.** Campaign levels spawn
their opening room's enemies during `BuildLevel`, so "any enemy alive" would pin every level to the
combat layer from the first frame and the ambient bed would never be heard at all.
`StoryAudioDirector` counts living hostiles within `EngagementRadius` (1000 px, a little over half
the reference viewport) of the `StoryPlayer`, at 4 Hz — there is no enemy-spawned event on the bus,
and a wave trigger seeds several enemies in one frame, so polling is both simpler and sufficient
against a 2–3 s crossfade. The policy itself is `StoryCombatIntensityModel`, pure C# with no Godot
types, which is what makes the timing edges (a staggered wave, a boss dying with adds still up)
testable as arithmetic instead of as seconds of engine time.

**B5: falling back to ambient is held for four seconds; rising is immediate.** Without the hold the
gap between one wave dying and the next trigger firing — routinely under a second — makes the mix
flap audibly. Leaving a boss fight also lands on Combat for the hold rather than on silence,
because the post-boss dialogue beat plays over an arena that is usually still populated.
`StoryCombatIntensityTests` pins both directions and the refill.

**B5: the boss climax is wired to the encounter, not to `OnBossDefeated`.**
`StoryLevelControllerBase.OnBossDefeated` is virtual and its doc comment anticipates subclasses
overriding it; a level that does so without calling base would silently lose the music transition
while keeping everything else. `BuildBossEncounter` therefore adds a second, separate subscription
to `BossRevealed`/`BossDefeated` alongside the one that calls the virtuals. Level 15's completion
override is the case that would have broken.

**B5: the director releases the stage audio unconditionally in `_ExitTree`.** The stem director is
an autoload child with a process-lifetime registration, so a scene that registers and never releases
leaves the level the player just left playing under the hub. Same discipline as the project's
`SceneTree.Paused` rule, and `StoryAudioDirectorTests.LeavingTheSceneReleasesTheStageAudio` is the
guard. Level completion is deliberately *not* a release: it drops to ambient and keeps the set
registered, so the results overlay plays over music rather than over silence.

**B5: `EnvironmentAudioCues` exists because of an A2 API gap, and should collapse into A2 later.**
`AudioManager` already loads the placeholder hazard and pickup streams but keeps them private behind
`ResolvePresentationCue`; the only public entry point for an arbitrary world sound is
`PlaySFX(stream)`/`PlayOneShot(stream, …)`, which needs the caller to hold the stream. B5 must not
refactor A2's file (§6), so the two streams are resolved once in a small static and the call sites
stay one-liners. If A2's surface ever grows a `PlayEnvironmentCue(id)`, this class collapses into it
with no call-site edits. Loaded through `ResourceLoader`, not `AuthoredResources` — audio is
streamed content and must stay out of the authored-data cache.

**B5: hazards are wired through the one `OnHazardStateChanged` payload; pickups are wired at their
collection sites.** All nine toolkit hazards, the escape sequence, and the extractor discharge
already publish that payload, so one subscriber covers every authored hazard without touching a
single template. `Cooldown` is silent, matching A2's rule that a recovery beat firing several times
a second is noise rather than information. Pickups went the other way deliberately:
`OnChronalDustCollected` is *also* re-raised for every enemy kill and every extractor break, so
subscribing to it would have doubled the pickup cue on top of the death and destruction cues instead
of marking a pickup. `StoryPickup.Collect` and `ChronalDustPickup`'s magnet collection call directly.

**B5: footsteps are distance-based, and the first step is immediate.** A fixed frame interval makes
a status-slowed walk keep a sprinter's cadence and makes a dash sound like a walk; accumulating
travelled distance scales the rhythm with speed for free. `FootstepCadence` primes on every
ineligible frame, so starting to run or landing plants a foot at once — without that, short hops
between close platforms are completely silent and read as missing audio. A frame that banks several
strides sounds once and drops the backlog (modulo, not repeated subtraction) rather than
machine-gunning it out over the following frames. Rolling is silent on purpose. Pinned by
`FootstepCadenceTests` at speeds that are exact multiples of the 60 Hz step, so the assertions test
the cadence rather than float accumulation error.

**B5: Fighter Mode gets no footsteps, and that is a real gap.** `FighterSimulationDriver` sets
`ProcessMode.Disabled` on both presentation bodies, so `PlayerController._PhysicsProcess` — where
the emission lives — never runs in a match. Emitting them would mean diffing grounded state and
velocity in the driver's `SyncPlayer`, which is A3/B2 territory this workstream was told to keep
additive. Left for Package 10's audio pass alongside the real footstep set.

**B5: the Fighter climax is one-way and keyed on the mode, not just the stocks.** A pure time-limit
match never removes a stock, so a stock-keyed climax could never fire there; its own final-seconds
climax is a separate rule and is not implemented. Zero stocks is deliberately not a climax — that
fighter is already out and the driver's KO sequence owns the moment. A one-stock match *is* a climax
from the opening frame, which is correct for a sudden-death rule set rather than a case to suppress.
The rule lives in `FTT.Core.FighterAudioRules` rather than in the driver precisely so it is testable
without simulating a match to a knockout; the driver's whole audio block is additive and reads only
deterministic fields.

**B5: the match going live is the combat cue, and the countdown stays on ambient.** The stage
controller registers the set (which starts on Ambient by A2's contract), and the driver layers combat
in on the same edge that raises the `MatchStart` banner. KO/fanfare cues already play through A2's
assets and were not touched; the countdown blip is B2's.

**B5 validation.** Build clean (pre-existing vendored `CS8632` only); `--headless --import` clean
(seventeen new `.tres`, no CSV edits so no compiled-translation artifact); full suite
**1092 = 1060 + 32** across two consecutive serial runs; headless smokes clean and byte-identical to
the pre-change baseline for `HubWorld`, `Level_00_Tutorial`, `Level_01_Florence`, `Level_02_Orleans`,
`Level_05_Titanic`, `TestArena`, and `FighterStage_Florence` (the "6 ObjectDB instances leaked /
3 resources still in use at exit" lines are pre-existing — verified by re-running the hub smoke with
B5's script changes stashed). No existing suite was modified. Several early runs failed to launch the
GdUnit child (exit 100 / connection timeout, no FATAL in `godot.log`) — CLAUDE.md failure signature 5,
cross-worktree contention; only runs matching the exact expected total are reported here.

### B4 — menus (2026-08-08)

**B4: the four main-menu screens are authored siblings that are shown and hidden, not
panels that are built and freed.** The old flow instantiated a `PanelContainer` per step,
called `HideMenuControls()` (which iterated *every* child and hid it), and then either
`QueueFree`d the panel or flipped `Visible` on a node it looked up by name — three
different unwind mechanisms across three transitions, with no single place that knew where
"back" went. `MainMenu.tscn` now carries `RootScreen`/`SlotScreen`/`CharacterScreen`/
`DifficultyScreen` as authored siblings and the script owns one `List<MainMenuScreen>`
stack; `GoBack()` pops one entry, `ui_cancel` calls it, and the root is the floor.
`ExactlyOneScreenIsVisibleAtATime` and `CancelWalksTheScreenStackBackAndStopsAtTheRoot`
pin both halves — the second specifically asserts that cancel at the root does *not* quit,
unwind past the floor, or leave every screen hidden.

**B4: the three story slot rows are authored, not generated.** `SaveManager.SaveSlots` is a
fixed three-element array, so the rows are a contract rather than data; `RefreshSlotRows()`
repaints text and toggles each delete button's visibility in place. A slot with no save
**hides** its delete button rather than disabling it, because `FocusChainBuilder.Collect`
skips hidden subtrees but would happily stop the chain on a disabled control that can never
do anything. `AFilledSlotShowsItsDeleteButtonAndAnEmptyOneHidesIt` asserts the chain grows
from 4 to 5 when a slot fills.

**B4: `SpinBox` keeps its editable `LineEdit` as an *internal* child, so
`FocusChainBuilder.Collect` cannot see it.** The collector walks `GetChild`, which excludes
internal nodes, so the character-select stock-count and time-limit fields would have been
silently unreachable by keyboard and controller — the exact class of gap this workstream
exists to close. `CharacterSelectScreen` therefore assembles its chain explicitly
(`FocusChain` → `FocusChainBuilder.Chain`) and splices in `SpinBox.GetLineEdit()`. This is
not a defect in A1's utility: a generic collector should not reach into another control's
internals. `EveryInteractiveControlJoinsOneFocusChainAndTheRosterTakesInitialFocus` asserts
both numbers — 21 in the authored chain, 19 from the generic collector — so a later
refactor back onto `Apply` fails loudly instead of quietly dropping two controls.

**B4: the nine character tiles are `Button`s with a per-tile stylebox override, and
`focus` is deliberately never overridden.** The tiles were `PanelContainer`s driven by a
`GuiInput` mouse handler, so a controller player could not select a fighter at all. They
are now real focusable Buttons, but they still have to carry the character colour identity,
which a Theme cannot express per instance. `StyleTile` overrides `normal`/`hover`/`pressed`
only; the theme's `focus` stylebox draws the ring on top, so selection (thick cyan border +
shadow) and focus (the ring) stay visually distinct states. Font colours are overridden per
tile against the tile's own luminance, because the theme's slate body colour is unreadable
on Mozart's near-white.

**B4: `CharacterSelectScreen.ApplySelectionToSession()` was split out of `OnFight` purely
as a test seam, and that is load-bearing.** A test that pressed Fight for real would run
`GameManager.LoadScene`, which — after A1's 2 s minimum loading treatment — calls
`ChangeSceneToPacked` and tears the GdUnit runner's own scene out from under the suite.
The split leaves `OnFight` as two lines (apply, then route) and lets
`TheSelectionWritesTheWholeSessionAndMatchSettingsRoundTrip` prove every session and
`MatchSettings` field survived the conversion, including that `Off` clears `ItemsEnabled`
as well as the band.

**B4: `ResonanceGrid.tscn`'s root is script-less and `ResonanceGridPanel` instantiates it,
following A4's `Settings.tscn` precedent.** The single opener, `HubWorldController`,
constructs the class directly (`new ResonanceGridPanel()`) and lives in a file two other
Package 8 workstreams are editing. Putting the script on the scene root would have forced a
cross-worktree edit for no user-visible gain. The manifest row (owner `ResonanceGridPanel`,
path `res://scenes/ui/ResonanceGrid.tscn`) is satisfied either way; C1 flips it.
`ResonanceGridPanel.ScenePath` is the constant the test asserts against the reserved path.

**B4: the grid's node buttons stay code-built, and the navigation math is untouched.** Their
count and copy come from the character's authored `ResonanceGridData`, so authoring them in
the scene would duplicate content and break the moment a grid changes shape. The authored
scene supplies the shell (shade, panel, title, balance, three-column `NodeGrid`, status,
hint, close). The bespoke `_UnhandledInput` D-pad walk over `ResonanceGridNavigation.Move`
is unchanged — a spatial talent tree is not a linear menu — and
`ResonanceGridNavigationTests` passed unmodified.

**B4: both destructive confirmations moved onto A1's `ConfirmModal`, and both pass resolved
copy rather than a raw key.** Slot deletion and node purchase each need runtime arguments
(slot number + character; node name + cost), which automatic control translation cannot
supply. `SetPromptKey` is given an already-formatted sentence; Godot's control translation
leaves an unknown string untouched, so this is safe and is the documented way to use the
helper with arguments. Both native dialogs are gone —
`typeof(Window).IsAssignableFrom(...)` is asserted false on each, because a
`ConfirmationDialog` is a `Window`: an OS-level popup that ignores the theme and traps no
focus. `CancelClosesAnOpenConfirmationRatherThanWalkingTheScreenStack` pins the precedence
rule: the modal absorbs the first cancel and the screen stack does not move.

**B4: the deletion test picks a slot the environment has proven empty.** Every other
save-touching test in the repository swaps `SaveSlots[n]` in memory, but
`SaveManager.DeleteStorySlot` reaches disk through `AtomicSaveStore.DeleteAllCandidates`.
`ConfirmingDeletionClearsTheSlotAndRepaintsTheRow` therefore records which slots were
originally `null` — meaning no file exists — and runs against the first of those, so a
developer's real campaign can never be destroyed by a test run. It skips if all three slots
are filled.

**B4: `scenes/TestScene.tscn` deleted, `Player.tscn`'s `DebugLabel` node removed
outright.** Both were unreferenced by any script or scene (only `docs/development-plan.md`
and a status-ledger row mention TestScene, as history). The label was not referenced by
name anywhere, so it was removed rather than blanked. These are the two visible-`text`
offenders C1's planned `.tscn` scanner is meant to prove itself against;
`MenuSceneContentTests` pins them now so the scanner inherits a clean tree.

**B4: `MainMenu.CharacterName()`'s `GD.Load` is gone, and the guard is a regex, not a
substring.** The first version of the guard tested `source.Contains("GD.Load<")` and failed
on the doc comment that *explains* the fix — `<c>GD.Load</c>` contains that substring.
`MenuSceneContentTests` matches `GD\.Load<[^>]+>\s*\(` so it catches a call and not prose.

**B4 validation.** Build clean (pre-existing vendored `CS8632` only); `--headless --import`
clean; owned suites 32/32; full suite **1089 passed / 0 failed / Total 1089** — exactly
1060 + 29 — in a verified-clear window (two earlier attempts hit CLAUDE.md failure signature
5, exit code 100 with `Total: 3`, against three concurrent sibling `testhost` processes; the
reliable remedy was polling for five consecutive zero readings of both `testhost` and
`Godot*`). Headless smokes clean for `MainMenu`, `CharacterSelect`, and `HubWorld`. No
pre-existing suite was modified. Test delta **+29** (1060 → 1089). Per §2.8 `en.csv` gains
one key (`fighter_cpu_difficulty`) under `# Package 8 B4` and `en.en.translation` is
**not** committed — the compiled-translation assertions in `MenuSceneContentTests` and both
scene suites need the orchestrator's wave-boundary `--import`.

### B6 — VFX content (2026-08-08)

**B6: six reusable effect scenes carry all 78 hooks, and the identity lives in the tint.** The
brief for the roster was explicitly "reusable mappings, not 42 bespoke scenes"; the same argument
applies to the 36 abilities, so `scenes/vfx/` holds one scene per `VfxEffectFamily` — `Burst,
Slash, Beam, Shockwave, Summon, Impact` — and nothing else. What separates Joan's cast from
Tesla's is `VfxAccentPalette`, which reads `CharacterFactory.GetCharacterColor` rather than
authoring a second per-character colour table, and lifts it 35% toward white because several of
the nine body colours (Lincoln's near-black navy) are invisible as an additive spark.
`AbilityVfxAssignmentTests.TheAssignmentExercisesEveryFamilyRatherThanCollapsingToOne` and
`VfxTaxonomyTests.EveryFamilyFadesOutAndNoTwoShareASilhouette` guard against this decaying into
six copies of one puff.

**B6: the 36 assignments are derived from `ExecutionType`, not hand-picked per ability.**
`Melee→Slash`, `Projectile→Beam`, `Area→Shockwave`, `PersistentObject→Summon`,
`Cinematic→Shockwave`, and movement abilities split by `MovementType`
(`Blink/Teleport/Warp→Burst`, `Glide/Float→Summon`, `Dash→Beam`). Impacts are `Impact` except for
`Area`, which reuses `Shockwave` because an area ability's "impact" is the field pulsing, not a
contact spark. A mechanical rule means a future ability lands on a sensible effect without a
content decision, and it is reviewable at a glance. Only the two VFX fields were touched in
`resources/Abilities/**`; the canonical tuning numbers are untouched.

**B6: `VfxEffect` subclasses `PooledPlaceholder` instead of reimplementing pooling.** Lifetime,
the shared particle-budget reservation, the off-screen suspension handshake and the Story rewind
contract all come for free, and the subclass adds only the per-family scale/alpha/rotation curve.
It re-declares `IPoolable` so the pool's `node is IPoolable` dispatch reaches the overrides, which
then chain to the base. The curve deliberately animates the `Visual`/`Particles` children's
`SelfModulate` and transform and never the root `Modulate` — the root is where `VfxEmitter` writes
the caller's accent *after* `OnSpawn` returns, so a curve that owned it would erase the character
identity every frame.
`VfxTaxonomyTests.ASpawnedEffectRunsItsLifetimeAndReturnsToThePool` pins both halves.

**B6: the roster mapping matches verb tokens, and family priority beats token order.**
`RosterVfxMap` splits the final dot segment of a `PresentationEventID` on underscores and matches
whole tokens against an ordered vocabulary (`Summon > Beam > Slash > Shockwave > Burst`). Priority
has to dominate token order because several authored names carry two verbs: `pulse_flintlock` is a
gun, not a pulse; `shell_burst` is a shell; `cross_slash_dash` is a slash that happens to move;
`lance_lunge` is a lance. A first-token-wins pass got `pulse_flintlock` wrong, which is why the
loop is nested family-outer. All 76 authored kits plus all 27 `{enemyID}.death` events resolve to
an explicit keyword, and
`RosterVfxMappingTests.EveryAuthoredPresentationEventResolvesToAnExplicitFamily` fails with the
offending `.tres` path when a future roster entry introduces a verb outside the vocabulary, rather
than letting it ship a silently generic puff.

**B6: the `Recovery` phase deliberately draws nothing, mirroring A2's call on sound.** Recovery
fires several times a second on every ability of every enemy; an effect there is particle churn,
not information. `Mapping.HasEffect` is false for exactly that phase and
`RosterVfxMappingTests.RecoveryDeliberatelyDrawsNothing` pins it beside a proof that the other
three phases do draw. This is the same reasoning A2 recorded for `RegisterPresentationSound`, so
the two systems now agree.

**B6: A3's `VfxEmitter` and `ParticleBudget` were consumed unmodified; only the binder changed.**
A3's §9 named `VfxPresentationBinder` as the surface B6 refines, so `OnEnemyPresentation` now
resolves through `RosterVfxMap` + `VfxLibrary` and falls back to A3's generic
`EmitForPresentationEvent` when a taxonomy scene will not load.
`VfxEmitter.EmitForPresentationEvent` still carries its `_ = presentationEventID` line: it is now
the fallback path rather than the primary one, and rewriting it would have been an edit to A3's
framework for no behavioural gain.

**B6: `BaseSpecial`'s two emit calls now pass a tint — the one A3-authored file touched, and only
to fill an argument A3 provided.** `EmitScene(scene, position, parent, tint)` already took a tint
and both call sites passed none, which is precisely the "assign per-character accent" half of the
deliverable. Two lines, no signature change, and no behavioural change when the fields are
unassigned.

**B6: the three dead rewind payload fields are implemented as one state model plus a world-space
trail.** `RewindCueState` is pure C# with no Godot dependency (same reasoning as A2's pumped
fades: the cadence is assertable without an engine frame). It owns the edges the payload cannot
express by itself — `ChronalRewindManager` republishes `Playback` every frame, so a naive consumer
would have retriggered the reverse sweep sixty times a second. `RewindGhostTrail` is a `Node2D` on
the running scene rather than a child of the overlay's `CanvasLayer`, because ghosts belong with
the level geometry under the tint, not over it; it reads the per-player
`TemporalPositionHistory` from the outside and never writes to it or to `CharacterFactory`. Ghosts
are offset by the sprite's own local position, or they draw at the character's feet — the history
samples the body root and the placeholder sprite hangs 64 px above it.

**B6: `StoryDropsAndRewindTests` already asserted the manager *sets* those three fields, which is
why they looked covered while nothing consumed them.** Worth recording as a testing lesson: a
producer-side assertion on a payload field proves nothing about whether anything reads it. The new
`RewindCuePresentationTests` covers the consumer side, and that older suite was left unmodified.

**B6: two new placeholder cues, and the generator is not byte-reproducible.**
`tools/generate_placeholder_audio.py` gained `rewind_sweep.ogg` and `clock_tick.ogg` under
`audio/sfx/chronal/`, following A2's conventions (quiet, obviously synthetic, `.ogg` with committed
`.import` sidecars). Re-running the script rewrote A2's three existing files with byte-different
but audibly identical output — the Vorbis encoder is not deterministic across runs — so those
three were restored from git and only the two new assets are committed. Anyone extending `ASSETS`
again should expect the same and check `git status` before committing.

**B6: the Fighter proxy polish is a pure function of a presentation-owned frame counter.**
`FighterProxyStyle` gives orbs a 45° diamond silhouette with a breathing scale, warning-phase
hazards a fast blink instead of a flat low alpha, two hazard identities a continuous spin, and
zones a slow swell. `_proxyStyleFrame` advances in the driver's sync pass and is read by nothing
else; no styling function reads simulation state beyond the type ids `ConfigureProxy` was already
given. `ConfigureProxy` gained optional rotation/scale arguments and `ReleaseMissing` now resets
`Rotation`/`PivotOffset`, because a recycled proxy inheriting the previous entity's silhouette is
exactly the kind of pool bug that only shows up after a stock loss. No file under
`scripts/FighterSim/` other than the driver's presentation code and the new style class was
touched, and no existing determinism suite was modified.

**B6: orb colour is now one canonical value shared by both modes.** The driver carried an
anonymous `switch` on the orb effect index and the Story `ChronalOrbItem` had no colour at all;
`ChronalOrbItem.EffectColor` is now the single source and `FighterProxyStyle.OrbColor` layers only
the pulse on top of it. The five effects produce four distinct colours — `DamageBoost` and
`ShieldRestore` share the catch-all purple, which is the pre-existing driver behaviour preserved
deliberately rather than a new gap.

**B6: `ChronalOrbItem` had no authored scene at all.** `ChronalOrbData.Icon` was an exported
texture with no reader anywhere in the project, and the orb script bobbed an invisible node — there
is no `.tscn` for it in the repository. `scenes/templates/ChronalOrbTemplate.tscn` now gives it a
`Glow`/`Icon` pair. It is deliberately **not** wired into any pool config: the `chronal_orb` pool
ID belongs to the ten Fighter stage configs and the Test Arena, all of which render orbs through
the driver's proxies, and repointing those rows is stage/pool content this workstream does not own.
The template is authored, tested, and ready for whoever adds Story orb drops.

**B6: no en.csv keys.** Nothing in this workstream renders text, so there is no `# Package 8 B6`
marker block and `en.en.translation` was not touched.

**B6 API surface for later workstreams.** `FTT.Combat.VfxEffectFamily`;
`VfxLibrary.PathFor`/`Load`/`AllPaths`; `VfxAccentPalette.ForCharacter`/`ForAbility`/`ForRoster`;
`RosterVfxMap.Resolve(eventID, phase)` → `{Family, PoolID, MatchedKeyword, IsExplicit, HasEffect}`
plus `IsBossEvent` and `VerbTokens`; `ChronalOrbItem.EffectColor(OrbEffect)`;
`ChronalExtractor.DischargeColor`; `FTT.UI.RewindCueState` and
`RewindGhostTrail.EnsureInstalled`; `FTT.FighterSim.FighterProxyStyle`.

**B6 validation.** Build clean (pre-existing vendored `CS8632` only); `--import` clean; full suite
**1102 = 1060 + 42** (5 ability assignment + 6 taxonomy + 8 roster mapping + 8 rewind cue + 3
extractor + 7 proxy style + 5 pickup), plus a filtered run of the seven new suites at 42/42;
headless smokes clean for `TestArena`, `Level_01_Florence`, `Level_06_Pompeii` and
`FighterStage_Vesuvius`. No existing suite was modified. Not delivered by B6 and left to later
work: production VFX art and shaders (P10), a human look at the rendered result (no headless gate
can discharge it), Story orb pool wiring, and cinematic boss presentation.

**B6 note for the orchestrator: running Godot in a worktree rewrites ~68 unrelated `.import`
files.** Every launch rewrites the `gdunit4`/`gdUnit4` case in `source_file`/`dest_files` paths and
reflows line endings across the asset `.import` sidecars and
`resources/Audio/default_bus_layout.tres`. `git diff --numstat` on them is empty — it is pure
churn, not content — and they were restored with `git checkout` before committing. Any Phase B
agent that commits a wide `.import` diff has committed this artifact, not a real reimport.
