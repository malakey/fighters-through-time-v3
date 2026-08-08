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
