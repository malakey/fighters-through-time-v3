# Workstream B — Campaign Structure & Progression (§3) gap dossier

Scope: master lines 225–522 of `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md` (§3 Campaign Structure & Progression) plus contract docs `CHECKPOINT_RECOVERY.md` (F11), `CAMPAIGN_VALIDATION.md` (V01/E01), `STORY_PERSISTENCE.md` (F10), `LEGACY_CHECKPOINTS.md` (F12), `DUST_ECONOMY.md` (F05), `DESIGN_BUILD_DEVIATIONS.md` (P04).
Repo revision inspected: `main` @ `31fed14` (V7.4 stun-lock pass), working tree clean.

Legend for **Status**: `MISSING` (nothing exists), `PARTIAL` (some scaffolding, wrong rules), `RETIRE` (shipped code the new design deletes), `RENAME` (identifier change), `OK` (already conformant).

---

## B0. Executive summary of the shape of this workstream

The single largest change in §3 is that **Timeline Integrity stops being a per-Extractor siphon tally and becomes the level's countdown timer** (V7.6, F01). Every V7.3 constant in `FTT.Core.TimelineIntegrityRules` — the 10% siphon share, the 10 s grace, the +3/+2/+5 restoration paths, the 90/70 tier lines, the 0.1/0.2 %/s drain, the 85% ending threshold — is retired and replaced by a normalized live drain equation with per-level authored `parSeconds` and difficulty multipliers 2.0/1.5/1.2. Four more systems land on top: the **Legacy Unlock Schedule** (kit earned across Act I — nothing exists), the **Level 4A Legacy Level** (a 17th campaign slot per character — nothing exists), the **Act III Gauntlet** (Warden Beacon, anchor charges, Anchor Snap, Smothered Game Over — nothing exists), and the **Collapse Tremor** (<20% environmental degradation — nothing exists). Two shipped subsystems are deleted outright (`ChronalRatingRules`, `SessionExitGuard`'s abnormal-exit fee). The save schema grows from v5 to a large `attemptState` record (F10) — this is the heaviest shared-file risk in the whole rework.

Rough total: **3 L items, 7 M items, ~9 S items**, and effectively every campaign level controller is touched at least by the Integrity/checkpoint-role change.

---

## B1. Timeline Integrity → the level timer (F01) — **the core change**

### Design requirement

* Level opens at **100%**. Budget to zero with every Extractor alive is `parSeconds × difficultyMultiplier`, **Easy 2.0 / Normal 1.5 / Hard 1.2**.
* `parSeconds` is authored per level (V01a): measure every eligible hero on Normal, normally-unlocked kit, no purchased grid; take each hero's **median** required-route time (entry → PreBoss activation), then `sharedParSeconds = ceil(max(heroMedians))`. Each 4A route gets its own par. Same par on all three difficulties.
* Drain weights: `initialDrainFactor = 1.0 + 0.2 × startingExtractorCount`, **fixed at level load from the authored starting population**, never recalculated from survivors (including on checkpoint resume). Destroying a machine subtracts 0.2 from the *current* factor. Finding the secret subtracts **0.1** from the current factor for the rest of the level.
* Canonical equation:
  `currentDrainFactor = 1.0 + 0.2 × livingExtractorCount − 0.1 × secretFound`
  `drainPerSecond = 100 / (parSeconds × difficultyMultiplier) × currentDrainFactor / initialDrainFactor` (percentage points per **live** second).
  Minimum raw factor **0.9**; minimum normalized rate `0.9 / initialDrainFactor` (0.5625× all-alive for 3 machines). Clamp Integrity at zero. **Destroying a machine or finding a secret never changes the current gauge value** — only the future rate.
* Drain is **global from level load**, seen or not — there is no "engagement", no room entry, no grace.
* **Nothing ever adds time back.** Rewinds do not refund. Time Freeze does not pause the clock (design Section 4 — cross-workstream).
* Clock **pauses** for: dialogue, cutscenes, pause menu, the death-rewind presentation, and the boss intro ritual. It runs through all other live play including the Restoration Font channel.
* **Pre-boss freeze:** activating the checkpoint whose `checkpointRole == PreBoss` (never a numeric ID or array index) freezes the gauge permanently; that value is the level's final Integrity.
* At par with no detours / all alive / no secret, remaining gauge is **50% / 33.33% / 16.67%** (E/N/H).

### Repo today

* `scripts/Core/TimelineIntegrityRules.cs` — V7.1/V7.3 model. Constants: `StartPercent = 100f`, `MaxSiphonSharePercent = 10f`, `SiphonGraceSeconds = 10f`, `ExtractorDestroyRestorePercent = 3f`, `GenericSecretRestorePercent = 2f`, `SpecialSecretRestorePercent = 5f`, `RestoredThreshold = 90f`, `StabilizedThreshold = 70f`, `EndingThresholdPercent = 85f`, `DrainPerSecond(difficulty) => Hard ? 0.2f : 0.1f`, `Tier()`, `TierKey()`, `DustBonusPercent()` (10/5/0, authored but never applied).
* `scripts/Environment/ChronalExtractor.cs` lines 76–94 — per-machine drain: `SiphonEngageDistancePixels = 600f` proximity engage, `SiphonGraceRemaining` countdown, `DrainedSharePercent` capped at 10, draws through `StoryManager.DrainTimelineIntegrityAmount`. Freeze via `IStoryRewindSimulation.SetStoryRewindFrozen`.
* `scripts/Core/StoryManager.cs` — `TimelineIntegrityPercent` (line 281), `DrainTimelineIntegrityAmount` (299), `RestoreTimelineIntegrity` (308), `RegisterSecretFound(id, isSpecialSecret)` (319), `LevelSecretsFound`, `LastLevelIntegrityPercent`.
* `scripts/UI/StoryHUD.cs` `RefreshIntegrity()` (165–178) — `hud_integrity` label, red tint when the value fell since last frame.
* **Nothing triggers a Collapse when Integrity reaches zero.** The only `BeginTimelineCollapse` call site is `ChronalRewindManager.cs:550` (rewind pool exhausted on death). The gauge is currently cosmetic + a results number.
* No `parSeconds`, no difficulty time multiplier, no clock-pause plumbing, no drain factor, no PreBoss role.

### Status

`PARTIAL` for the gauge/HUD/save plumbing; `RETIRE` for siphon share + grace + restoration; `MISSING` for the entire timer model.

### Change list

| Item | File / key |
|---|---|
| Rewrite `TimelineIntegrityRules` | `scripts/Core/TimelineIntegrityRules.cs`: delete `MaxSiphonSharePercent`, `SiphonGraceSeconds`, `ExtractorDestroyRestorePercent`, `GenericSecretRestorePercent`, `SpecialSecretRestorePercent`, `DrainPerSecond(Difficulty)`, `EndingThresholdPercent = 85f`. Add `DifficultyTimeMultiplier(Difficulty)` = 2.0/1.5/1.2, `ExtractorDrainWeight = 0.2f`, `SecretDrainReduction = 0.1f`, `MinimumRawFactor = 0.9f`, `InitialDrainFactor(int startingExtractors)`, `CurrentDrainFactor(int living, bool secretFound)`, `DrainPerSecond(float parSeconds, Difficulty, int living, int starting, bool secretFound)` |
| Move tier lines | `RestoredThreshold 90f → 50f`, `StabilizedThreshold 70f → 20f` |
| Move ending threshold | `EndingThresholdPercent 85f → 50f`, and it is now compared as `sum(finalIntegrity over 15 required level IDs) ≥ 750` unrounded (see B7) |
| Level clock owner | `StoryManager`: add `ParSecondsForCurrentLevel`, `StartingExtractorCount`, `LivingExtractorCount`, `SecretFoundThisLevel`, `IsIntegrityClockRunning`, `IsPreBossLocked`; tick in `_Process`/`_PhysicsProcess` gated on the pause set |
| Pause set | New `IntegrityClockPause` scope used by `DialogueManager`, `PauseMenuBase`, `ChronalRewindManager` (death-rewind presentation + collapse beat), boss-intro ritual in `BossEncounterController` |
| Strip per-machine drain | `ChronalExtractor.cs`: delete `SiphonEngageDistancePixels`, `SiphonEngaged`, `SiphonGraceRemaining`, `DrainedSharePercent` and the whole drain block; `OnDestroyed` drops the `RestoreTimelineIntegrity(+3)` call and instead decrements the living-machine count. Keep `RecordExtractorDestroyed`, keep `IStoryRewindSimulation` (freeze still pauses the discharge cycle) |
| Secret | `SecretCache.cs` / `StoryManager.RegisterSecretFound`: `isSpecialSecret` no longer selects a restoration amount; the found secret sets `secretFound = 1` (−0.1 factor). Keep the once-per-attempt dedupe and the results count |
| Per-level par authoring | Every `scripts/Environment/LevelNNController.cs` (2–15) needs an authored `ParSeconds` constant + starting-Extractor count; recorded per the V01a evidence matrix (all values currently **pending measurement** — the docs supply none) |
| Collapse at zero | New: clock hitting zero fires `BeginTimelineCollapse` in Acts I–II / `AnchorSnap` in Act III (see B4) |
| Localization | `hud_integrity` stays; add the "Integrity drain slowed" cue key (E01), first-Tremor Sarah bark key, Beacon keys (B4) |
| Tests | `tests/unit/TimelineIntegrityTests.cs` — case 1 (`TheIntegrityTiersDrainRatesAndRestorationPathsMatchTheDesign`), case 3 (`TheSiphonIsGracedCappedAtItsShareAndPausedByTheRewindFreeze`) and case 4 (`RestorationPaysThreePerExtractorTwoPerSecretAndFiveForTheSpecial`) are all invalidated. New suites: drain-equation arithmetic, fixed denominator across resume, pause set, PreBoss freeze, clamp-at-zero |

**Size: L.** **Shared files:** `StoryManager.cs` (every workstream), `DialogueManager.cs`, `PauseMenuBase.cs`, `ChronalRewindManager.cs`, all 14 `LevelNNController.cs`.

---

## B2. Collapse Tremor (<20%) — **new**

### Design requirement

* Trips when the gauge falls **below 20%** (the Fractured line). Two stages: **Stage 1 < 20%**, **Stage 2 < 10%**.
* Stage 1: continuous camera shake **0.1**, jolt **0.3 for 0.4 s every ~6–8 s**; chronal crack lines across background layers with cold cyan-white glow; palette desaturates inward from screen edges; props time-ghost; falling debris begins; `FractureEligible` platforms become unstable.
* Stage 2: jolts every **~3–4 s**; cracks reach foreground/playfield edges; desaturation ≈ half the screen; vignette pulses with the HUD clock tick; debris twice as often.
* **Falling debris:** era-flavored, spawns above camera near player. Telegraph **0.75 s** (glowing crack overhead + landing-surface marker). Damage **5% max HP**, small knockback, **blockable as a Basic-class hit**, routed through the environmental-damage chokepoint (Defy + Rally accounting apply), **never triggers hitstop**. Cadence ~1 per **4 s** (Stage 1) / **2 s** (Stage 2), **never more than two airborne at once**. Never inside a Restoration Font radius or on an activated checkpoint. **Enemies are unaffected.**
* **Unstable platforms:** only platforms flagged `FractureEligible`; below 20% they crack and behave as Crumbling Platforms — shake on landing, collapse, **respawn after 5 s**. Never eligible: pressure plates, latched-switch gates, `PathMovingPlatform`, the pre-boss approach. Guard: every one respawns, so no gap becomes uncrossable.
* Audio: low rumble bed, stone/timber groan per jolt, slow music-bus detune, HUD clock tick.
* First crossing plays a one-per-level non-blocking Sarah bark; clock keeps running.
* Stops whenever the clock pauses; ends for good at the PreBoss checkpoint (boss arena always stable). A granted timer recovery starts ≥25%, above the threshold, but Tremor can return later and route validation must include its delays.
* Accessibility: all shake scales with the Settings `screenShakeSlider` (0 disables); glow/ghost/vignette never flash >3 Hz; **C01a Reduced Temporal Effects** preset removes decorative glow/vignette pulsing, ghosting and heavy desaturation while retaining static danger/crack markings and unchanged Tremor timing.
* **Act III variant:** same effect, different language — hero's gold aura gutters (Suppression smother shader at partial strength, never locking anything), cold Unbound light inward from edges, conduit debris.

### Repo today

* `scripts/Environment/CrumblingPlatform.cs` exists (the behaviour primitive). No `FractureEligible` flag anywhere.
* `scripts/Core/CameraShake.cs` exists with the accessibility scale.
* `scripts/Environment/StageHazard.cs` + `PlayerController.ApplyEnvironmentalDamage` exist (the V7.3 chokepoint) — debris can ride them.
* `RewindPresentationOverlay` has the monochrome/crack treatment for the collapse beat — reusable asset language.
* No tremor controller, no debris spawner, no desaturation-from-edges shader path, no reduced-effects preset.

### Status `MISSING` (primitives exist, system does not).

### Change list

* New `scripts/Environment/CollapseTremorController.cs` (stage state machine driven by `StoryManager.TimelineIntegrityPercent`; attached by `StorySceneBootstrapper` so Tutorial/Florence stay opt-out since they are untimed).
* New pooled `falling_debris` scene + pool row in `resources/Pools/level_pool_configs/*` and `scene_pool_catalog.tres` (14 level configs).
* `OneWayPlatform` / platform builders in `StoryLevelControllerBase`: add `FractureEligible` export + the builder plumbing; exclusion assertions for plates/`PathMovingPlatform`/pre-boss approach.
* `CameraShake` — jolt cadence helper; `AudioSnapshotMixer` — rumble/detune snapshot.
* Settings: `Reduced Temporal Effects` toggle → global save payload (schema bump, coordinate with the UI/settings workstream), `UIPalette`/shader guards.
* Localization: first-Tremor bark key (`tremor_first_warning` or similar) + the settings label.
* Tests: stage thresholds, debris cadence/cap/telegraph/damage-fraction, enemy-immunity, Font/checkpoint exclusion zones, respawn guarantee, pause/PreBoss shutdown, shake-slider zero.

**Size: M–L.** **Shared:** `StoryLevelControllerBase`, `StorySceneBootstrapper`, pool catalog, settings payload.

---

## B3. Legacy Unlock Schedule (V7.5) — **new**

### Design requirement

| Milestone | Unlocked |
|---|---|
| Level 0 (calibration) | Basic 3-hit string, Block, Rally, Death Rewind, **Time Freeze**, Ultimate Meter + **Defy History** |
| Complete Level 1 | **Movement Ability** |
| Complete Level 2 | **Special 1** |
| Complete Level 3 | **Special 2** |
| Complete Level 4 | **Ultimate cast** (meter exists from L0 so Defy and L4's dampening beams stay meaningful) |

* Milestone-gated, **never dust-gated**; the 975-dust grid economy is untouched.
* Delivery: a **"Resonance Restored"** beat on the level-results screen + a one-line tooltip on first field use; Chief Engineer Wren hosts an **optional** Training Wing calibration drill per unlock (optional so the portal never stalls).
* **Grid gating:** nodes that modify a locked ability stay **hidden** — rendered as a dim, unlabeled "dormant resonance" star in its true position, gaining name/cost/tooltip on unlock.
* **Level-design guard:** Levels 1–4 must be completable with the kit available at entry; Level 1 authored for base jump reach; no puzzle/traversal may require a not-yet-unlocked ability.
* **Save:** per-character `unlockedLegacyAbilities` list (F10 lists it under Story root progression, preserved on Restart Level).
* **Fighter Mode untouched** — full kits always, including the hub Holodeck.
* Level 4A is the first full-kit level and its results overlay plays the "Resonance Restored" beat for the Ultimate **on 4A entry**, not on Level 4's exit.

### Repo today

**Nothing.** `grep -rn "unlockedLegacy|LegacyUnlock|AbilityUnlock"` over `scripts/` and `tests/` returns zero hits. `CharacterFactory` builds the full kit unconditionally; `PlayerController.StoryAbilityPerks` is the only Story-side gating concept and it is a perk list, not an ability lock. `ResonanceNodeData` has no `gatedAbilityID`. `StorySaveData` (`scripts/Core/SaveManager.cs:9–76`) has no unlock list.

### Status `MISSING`.

### Change list

* `StorySaveData`: add `List<string> UnlockedLegacyAbilities = new()` + `Normalize()` entry (schema bump — see B8).
* New `FTT.Core.LegacyUnlockSchedule` static: milestone → ability-slot map, `IsUnlocked(slot, completedLevels)`.
* `CharacterFactory.CreateCharacter` / `PlayerController`: gate Movement Ability, Special 1, Special 2, Ultimate **cast** by the schedule in Story only (`applyStoryProgression: true` path); the meter, Defy History, Rally, Block, rewind stay on from L0. Fighter path (`FighterLoadoutFactory`) must remain untouched — this is the Story/Fighter isolation pillar.
* `StoryManager.OnLevelComplete` → grant the milestone unlock inside the completion transaction; `LevelResultsPanel` → "Resonance Restored" beat.
* `ResonanceNodeData`: add `[Export] public string GatedAbilityID = ""`; `ResonanceGridPanel` renders dormant-star state; `ResonanceProgression.TryUnlock` refuses a gated node.
* Hub: Wren optional drill entry point (`HubWorldController` Training Wing room) — coordinate with the Holodeck/hub workstream.
* Level-design guard: audit Levels 1–4 route reachability without the locked kit (Level 2 Orléans is designed *around* the Movement Ability, so it is the first level that may legitimately use it).
* Localization: `resonance_restored_*` beat lines, per-ability first-use tooltips, dormant-node tooltip.
* Tests: new `LegacyUnlockScheduleTests` (milestone table, Story-only isolation, Fighter/Holodeck full kit, grid hidden-node gating, save round-trip).

**Size: L.** **Shared:** `CharacterFactory`, `PlayerController`, `SaveManager`, `ResonanceGridPanel`, `LevelResultsPanel`, hub.

---

## B4. Act III Gauntlet — Beacon, anchor charges, Anchor Snap, Smothered, Resonance Hold — **new**

### Design requirement

* **Levels 13–15 play back to back with no return to the Time-Ship.** Level-results overlay and post-boss dialogue still play; there is simply no portal trip. Rewinds/checkpoints/difficulty pools unchanged. **On Hard all three Act III checkpoints are active** (the inert-middle rule is suspended for the gauntlet).
* **Warden Beacon:** handed over at Level 13 entry. Interacting at **any activated Act III checkpoint** opens the existing Repository screen for grid purchases and free respec **using previously deposited dust only**. **F02:** it cannot deposit or spend the active level's `levelChronalDust`; spendable balance excludes those earnings; the checkpoint UI offers **no Deposit action**. Current-level dust banks once at level completion ("sealed through the Beacon") — L13 earnings become spendable at L14's Beacon. Restarting clears the active level's undeposited dust under the existing rule. Free respec refunds only previously spent deposited dust. **No new UI** — the existing screen with deposit unavailable.
* **Anchor charges:** the Beacon holds **Easy 3 / Normal 2 / Hard 1 per Act III level**. Initialized **only** on fresh level entry or explicit full Restart Level — never on scene reload, hub return, checkpoint activation, or Anchor Snap (F10). HUD shows gold pips on the Beacon icon beside the rewind counter.
* **Anchor Snap** replaces Timeline Collapse's hub extraction in Act III: triggered by an unprevented lethal event with no death-rewind charge, **or** the Resonance Hold reaching zero (spending the final rewind charge still completes that rewind). Hero snaps back to the last activated checkpoint **in place** — rewind pool reset, full HP, same **20% undeposited-dust fee**. **Each Snap spends one anchor charge**; 1 → 0 still completes the Snap. Presentation ~3 s, skippable after the first viewing; Sarah: *"Your anchor held. Get up."*
* **Smothered:** a collapse with **no charge left** → ~5 s beat then a **Game Over screen** with **Restart Level** (standard Restart rules: undeposited level dust cleared, charges refilled) and **Quit to Menu**. Deposited dust — including previously completed Act III levels sealed through the Beacon — is always safe. **F10:** persist this terminal attempt state *before* its presentation; Quit and Load return to this same Game Over without HP/charge refill or replayed failure fee. Only explicit Restart Level creates a fresh attempt. **The hub remains unreachable until campaign completion.**
* **Quitting:** a deliberate exit from active gameplay pays its 20% fee once; **crash recovery is free**. Load routes by saved `attemptState.status`: active play resumes at its checkpoint with spent resources intact; a pending Snap completes once; Smothered returns to Game Over. Quit from an already-settled failure/results screen does not charge again.
* **Resonance Hold (Act III clock):** same gauge/rules/HUD/Tremor assets; the clock now measures how long the hero holds their charge against the Unbound field. **Candle rule** — full kit at 1% exactly as at 100%; no mid-campaign power loss. Drain stand-ins that each subtract **0.2 from the raw factor** exactly like an Extractor: **L13 severed conduits**, **L14 cradle intake valves**, **L15 firing-channel anchor pylons**. The PreBoss freeze still applies in L15. Tiers/ending read identically.

### Repo today

* `StoryManager.BeginTimelineCollapse(checkpointID)` (498) → dust penalty, rewind refill, save, **`ReturnToHub()`**; `RestartCollapsedLevel(bool resumeFromTimelineAnchor)` (520) drives `TimelineRestartPanel` in the hub. That whole hub-extraction flow is Acts I–II only under the new design.
* `SessionExitGuard.CalculateExitRetainedDust` = `×8/10` (20% fee) — the number is right; its abnormal-exit application is retired (B6).
* Levels 13/14/15 controllers each build **three** checkpoints (`_checkpoint_0/_1/_2`) and call `BuildExtractors(ExtractorPlacements)` — real `ChronalExtractor` instances, not conduits/valves/pylons. No Hard-specific checkpoint gating exists anywhere in the repo (grep for "Middle" finds only Level 10 geometry names), so "Hard's middle checkpoint is inert" is **also unimplemented** in Acts I–II.
* No Beacon, no anchor charges, no Snap, no Game Over screen. `scenes/ui/` has no Game Over surface.
* `AdvanceToNextLevel()` + `ReturnToHub()` are the only inter-level path; Act III's no-hub chaining does not exist.

### Status `MISSING` end to end; `PARTIAL` for the collapse/fee primitives it reuses.

### Change list

* `StoryManager`: split collapse into `BeginTimelineCollapse` (Acts I–II, hub) and `BeginAnchorSnap` (Act III, in place); add `AnchorChargesRemaining`, `InitializeAnchorCharges(Difficulty)` (3/2/1), `SpendAnchorCharge()`, `EnterSmothered()`; Act III completion routes to the next level rather than the hub.
* New `scripts/Environment/WardenBeacon.cs` (interactable at activated Act III checkpoints) opening `ResonanceGridPanel` in a deposit-disabled mode; `ResonanceProgression` needs a `spendableBalance` that excludes `LevelChronalDust`.
* New `scenes/ui/GameOver.tscn` + `scripts/UI/GameOverScreen.cs` (Restart Level / Quit to Menu), themed, focus-authored.
* `StoryHUD`: gold anchor pips beside the rewind counter (data-driven count, same pattern as stock pips).
* Level 13/14/15 controllers: replace `ChronalExtractor` placements with conduit/valve/pylon variants (same drain weight + F05 optional-dust allocation, different presentation/localization); enable all three checkpoints on Hard.
* `SaveManager`/`StorySaveData`: `attemptState.status`, `anchorChargesRemaining`, `recoveryEvent` (see B8).
* Localization: Beacon prompt, Snap line, Smothered copy, Game Over buttons, Act III clock/conduit strings.
* Tests: anchor init-only-on-fresh/restart, Snap spends once incl. 1→0, Smothered persisted before presentation and reload-stable, Beacon excludes current-level dust and offers no deposit, Hard three-checkpoint rule in Act III, no-hub chaining.

**Size: L.** **Shared:** `StoryManager`, `SaveManager`, `StoryHUD`, `ResonanceGridPanel`, `GameManager.LoadScene`, `TimelineRestartPanel` (Acts I–II only now).

---

## B5. Level 4A — the per-character Legacy Level — **new**

### Design requirement

* Slot ID `Level_04A_Legacy_{characterID}`; numbered 4A **so no existing level, scene ID, table, or budget renumbers**. Played between Level 4 and Level 5. One run visits **16 levels plus Level 0**; production covers **15 shared + 9 Legacy = 24 authored non-tutorial level/boss slots**.
* First full-kit level: must **require the whole kit once each** — one traversal gate for the Movement Ability, one puzzle per Special, one set-piece the Ultimate resolves. **The only level allowed to require a specific character's abilities.**
* **F04 Nexus Resonance Source:** a puzzle-only source at the Ultimate set-piece. Armed by interacting after the preceding encounter is cleared and setup complete (safe area, no enemies, no meter-drain hazards). Then the normal Ultimate input casts the real Ultimate against the designated puzzle target. Works at **any meter value including zero**, takes priority over ordinary meter spending while armed, and **neither fills nor consumes the normal meter**. Only the designated mechanism is affected — no damage to actors, no healing, no meter, no rewards, no Defy/Echo Step supply. Not a carried charge: leaving the bounded cast area, death, reload, or an interrupted cast clears it; the source becomes available again after returning to safe setup, unlimited retries, no cost. Successful resolution latches the gate, disables the source, records completion with that gate's saved puzzle state. **Never saved armed** (F10): unsolved gate restores an available source; solved gate keeps it disabled. **Time Freeze cannot activate the source or cast the Ultimate.** Integrity is never refilled; setup/retry time counts as live play. Local "Nexus-powered Ultimate" prompt, no change to the meter/Defy indicators.
* **Checkpoints (F12 Option A):** **exactly two — Entry and PreBoss — both active on Easy/Normal/Hard.** No Middle. Defaults `{levelID}_checkpoint_0` = Entry, `{levelID}_checkpoint_1` = PreBoss. **Roles are authored separately from stable IDs**; the Integrity system responds to `checkpointRole = PreBoss`, never to the literal name `checkpoint_2`, array position, or count. Hard's "middle inactive" rule cannot disable 4A PreBoss merely because its ID ends `_1`. Migrate saves by role, never by suffix.
* **Route order:** Entry → required kit gates and the independent Eraser encounter → late Font approach → PreBoss → boss. All mandatory pre-boss objectives (incl. the F04 Nexus puzzle and the Eraser) must be complete before PreBoss can activate.
* **Eraser debut (F12):** single Eraser on its own authored route trigger `{levelID}_eraser_debut` between Entry and PreBoss, independent of checkpoint activation and difficulty. Grants **no** checkpoint, Mending, rewind refill, or Integrity lock. A reconstructed encounter must remain triggerable without duplicate live waves or reissued claimed rewards; narrative viewing flags separate from encounter state; Time Freeze cannot activate it.
* **Restoration Font:** exactly one per 4A, existing difficulty uses/potency, placed on the **late approach after the Eraser and before PreBoss** (replaces the generic "between mid checkpoint and boss" placement). It is a healing object, not a third checkpoint.
* **Boss:** per-character, two phases, standard boss rules, **25 dust** as a Large physical pickup.
* **Economy:** identical ledger row for every variant — **15 required-encounter + 25 boss + 10 optional** dust (F05). Carries Timeline Integrity and its secret. Its own par (V01a, own hero/route benchmark, own F11 Entry recovery budget per difficulty).
* **Ending:** the **selected hero's one 4A** counts in the 15-level ending average; the other eight variants are excluded (N05).
* **P01 Option A:** retain all nine variants, reuse era art/themes, add only assets needed for distinct nexus outcomes/kit puzzles/bosses. Prototype one representative 4A before accepting cost estimates.

### Repo today

**Nothing.** `CampaignLevel` enum (`StoryManager.cs:6–23`) is a contiguous `Tutorial=0 … Alexandria=15` with `AdvanceToNextLevel()` doing `CurrentLevel + 1` and capping at 15; `LevelScenePaths` is a 16-entry array indexed by the enum value; `GetLevelScenePath` indexes it directly. `resources/Content/content_manifest.csv` rows 4–19 are the sixteen `StoryLevel` rows. `tests/integration/CampaignRouteTests.cs` asserts all sixteen resolve and that a sequential 0→15 advance lands on a real scene at every step. `StoryLevelControllerBase.BuildCheckpoint` hardcodes `SelfActivating = id.EndsWith("_checkpoint_0")` and there is **no `checkpointRole` concept at all** — `CheckpointTrigger` (in `scripts/Environment/LevelManager.cs:68`) exports only `CheckpointID`, `RespawnOffset`, `SelfActivating`.

### Status `MISSING` (and the level-index model actively blocks it).

### Change list

* `CampaignLevel`: the contiguous-int + array-index model must become an ID-keyed route table (or a dedicated `Legacy4A` member with a character-suffixed path resolver). This is the single most invasive small change — it touches `StoryManager.LevelScenePaths`, `GetLevelScenePath`, `AdvanceToNextLevel`, `ResumeCampaign`'s path→index scan, `MainMenu`'s developer level select (16 tiles), and `CampaignRouteTests`.
* Nine scenes `scenes/campaign/Level_04A_Legacy_{characterID}.tscn` + nine controllers + nine dialogue sets + nine pool configs + nine manifest rows + nine boss `.tres` (+ localization families).
* `CheckpointTrigger` / `BuildCheckpoint`: add `[Export] public CheckpointRole Role` (`Entry | Middle | PreBoss`) and drive `SelfActivating` and the Integrity freeze from the role, not the suffix. Add a per-variant ID→role manifest/alias for migration.
* New `NexusResonanceSource` puzzle prop + `PlayerController` Ultimate-input interception (Story-only, never in `scripts/FighterSim/`).
* New route-trigger encounter type for `{levelID}_eraser_debut` (idempotent reconstruction, claimed-reward safe).
* `tests/`: nine per-variant content suites in the existing `LevelNNContentTests` pattern + `LegacyCheckpointContractTests` (exactly two roles on all difficulties, PreBoss strike after all gates, no timer lock at Entry/Eraser, Font placement, reconstruction without duplicate claims).

**Size: L** (arguably the largest single content item in the project). **Shared:** `StoryManager`, `CheckpointTrigger`/`LevelManager`, manifest, pool catalog, `MainMenu` dev select, `CampaignRouteTests`.

---

## B6. Retired code to delete

| # | What | Repo location | Design authority |
|---|---|---|---|
| 1 | **Siphon share + grace** (`MaxSiphonSharePercent = 10f`, `SiphonGraceSeconds = 10f`, `SiphonEngageDistancePixels = 600f`, `DrainedSharePercent`, `SiphonEngaged`, `SiphonGraceRemaining`) | `TimelineIntegrityRules.cs:22,26`; `ChronalExtractor.cs:33,36,46,49,76–94` | V7.6 F01 replaces the whole model |
| 2 | **Restoration paths** (+3% / +2% / +5%) and `StoryManager.RestoreTimelineIntegrity` | `TimelineIntegrityRules.cs:29–31`; `StoryManager.cs:306–326`; `ChronalExtractor.cs:163`; `SecretCache.cs:53` | "Nothing ever adds time back" |
| 3 | **90/70 tier lines** | `TimelineIntegrityRules.cs:33–34` | now 50/20 |
| 4 | **85% ending threshold** | `TimelineIntegrityRules.cs:41` `EndingThresholdPercent = 85f` | N05: **≥50%**, compared as an unrounded 750-point sum |
| 5 | **Hard-doubles-drain** `DrainPerSecond(Difficulty) => Hard ? 0.2f : 0.1f` | `TimelineIntegrityRules.cs:44–45` | difficulty now lives in the 2.0/1.5/1.2 *time budget* |
| 6 | **`ChronalRatingRules`** — the whole S/A/B/C stamp | `TimelineIntegrityRules.cs:72–92`; `StoryManager.cs:270–275, 289, 619`; `LevelResultsPanel.cs:144–145, 176`; `StorySaveData.RatingByLevel` (`SaveManager.cs:31, 63`); loc key `results_rating` (`en.csv:1021`); `TimelineIntegrityTests` case 2 | V7.6 ruling 2.A: Chronal Rating retired, `ChronalRatingRules` → `IntegrityTierRules` |
| 7 | **Abnormal-exit fee** — `SessionExitGuard.ApplyAbnormalExitFee` and the `SaveManager._Ready` boot billing | `SessionExitGuard.cs:90–96`; `SaveManager._Ready`; loc key `save_notice_abnormal_exit_fee` (`en.csv:1153`); `tests/unit/SessionExitGuardTests.cs` | V7.6 ruling 2.B: crashes are free. *Keep* the marker file + `CalculateExitRetainedDust`/`CalculateExitWalletAfterPenalty` (the voluntary 20% is unchanged); F10 repurposes the marker as an attempt-status router, not a fee trigger |
| 8 | **Stasis Echo tutorial gate** | `TutorialCalibration` `UseManualRewind` step; `StasisEcho.cs` usage in Level 00 | "Retire the former Stasis Echo plate gate; required progression uses ordinary resource-free mechanisms" (the `StasisEcho` *class* itself is Section 4's call) |

**Note on #6 naming:** the ruling writes `ChronalRatingRules → IntegrityTierRules`. `TimelineIntegrityRules` already owns `Tier`/`TierKey`/`DustBonusPercent`. Recommended reading: **delete** `ChronalRatingRules.Compute` entirely and either (a) leave the tier helpers on `TimelineIntegrityRules`, or (b) rename the file's second class to `IntegrityTierRules` and move the tier helpers into it. Flag the ambiguity rather than picking silently.

**Size: S** per item, **M** in aggregate (touches save schema, results UI, localization, three test suites).

---

## B7. Tier bonus, ending average, dust economy (F05 / N05)

### Design requirement

* **Tiers at the pre-boss lock:** **Restored ≥50% / Stabilized ≥20% / Fractured <20%**.
* **F05 tier bonus is now actually applied:** on successful completion, `bonus = floor(retainedBaseDust × tierRate)` at **10% / 5% / 0%**, **exactly once**, then auto-deposit. `retainedBaseDust` = current level's collected dust remaining after losses, **excluding** deposited dust, respec refunds and prior bonuses. No compounding, no checkpoint payout. **Level 1 has no tier bonus** (untimed).
* **Ending (N05):** equal-weight average of **15 timed levels — Levels 2–15 plus the selected hero's one 4A**. Exclude untimed Levels 0/1 and the other eight 4A variants. Use each level's **unrounded** PreBoss-locked final Integrity: `endingAverage = sum(finalIntegrity over the 15 required level IDs) / 15`; compare the **unrounded sum against 750 percentage points**; display rounding cannot change the result. At ≥50% the clean ending; below, the scarred ending. Identical on every difficulty.
* **Economy budgets (F05 Option A):** grid stays **975** (50/75/200). Required route **720 base** (720–787 after Integrity bonuses); optional adds **280** (thorough 1,000 base / 1,000–1,094). **16 bosses × 25 = 400**; other mandatory encounters share **320**; optional total 280. Universal per-kill drops retired: standard weight **1**, elite/Eraser weight **5**, allocated by `floor(quota)` + largest-remainder over stable source IDs, **zero allocations legal**. Positive allocation spawns exactly one physical pickup. Reinforcements/repeatable spawns award **zero**. Hard's extra enemies redistribute, never increase, the level pool.
* **Extractors:** drop their authored share of the level's optional budget, one pickup. Typical 20-dust optional pool → 10 to all Extractors (5/5 or 4/3/3) + 10 to discovery; 10-dust pool → 3/2 or 2/2/1 + 5. A secret Extractor owns the combined share, paid once. Level 1's whole 10 optional goes to its Extractors. **The flat 25 and the shipped 15 are both superseded.**
* **Sprite tiers unchanged:** Small 1–5, Medium 6–24, Large 25+. Every boss produces a Large pickup at the arena center; an ordinary Extractor uses its **actual** quantity icon, not a forced Large.
* **Mirror Paradox:** same 25-dust physical pickup; **remove its wallet-direct path**.
* **E01 feedback:** on a real first destruction while the clock runs, a brief non-blocking "Integrity drain slowed" notice; **no** "time gained", no percentage refill, no seconds-saved estimate, no numeric drain readout. Dust keeps its separate actual-collection +N feedback. Untimed levels and a locked PreBoss clock show no drain notice. Teaching line goes in the existing paused Level 2 Sarah introduction.
* **L1–L4 boss HP cut (ruling 2.E):** **350 / 520 / 700 / 850**.

### Repo today

* `TimelineIntegrityRules.DustBonusPercent` returns 10/5/0 but **no caller applies it** — the comment at lines 57–58 says so explicitly.
* `EndingThresholdPercent = 85f` with **no consumer at all**; `StorySaveData.IntegrityByLevel` is written by `StoryManager.RecordLevelResultToSave` (611–622) but never read for an ending.
* `BossData.ChronalDustDrop = 50` default; **all 15 boss `.tres` carry `ChronalDustDrop = 50`** (incl. `mirror_paradox.tres:25`). `EnemyData.ChronalDustDrop = 10` default; standards authored 2, elites 10.
* `ChronalExtractor.DustReward = 15` (the documented deferred value).
* `docs/DUST_ECONOMY.md` in-repo ledger totals **1,787 required / 1,416 typical Normal** — roughly 2× the new F05 envelope. `tests/ContentValidation/DustEconomyTests.cs` hardcodes `EliteDustReward = 10`, `BossDustReward = 50`, `ExtractorDustReward = 15`, `FullGridCost = 975`, and `CampaignModelFundsTheGridOnTheDocumentedSchedule` models the old curve.
* `DustEconomyTests.AllNineGridsShareTheDocumentedCostCurve` asserts cost from prerequisite *count* (`PrerequisiteNodeIDs.Length == 0 ? 50 : 75`) — a coupling that must be re-expressed as a tier field once Any-of topologies land (B9).
* Boss HP in repo: `borgia_inquisitor 500`, `siegemaster_duke 540`, `chronal_inventor 560`, `revolutionary_tribunal 590`. The master's "(V7.6: was …)" annotations read 500/650/800/900, so **L2–L4 already diverge from the doc's pre-V7.6 baseline** — a pre-existing mismatch worth recording under P04 as well as applying the cut.
* Level 1 Florence still has **no authored Extractors** (`grep BuildExtractor scripts/Environment/Level01Controller.cs` → none) — the one open content gap the old ledger named, and F05 now assigns Florence's whole 10-dust optional pool to them.

### Status

`PARTIAL` (tier + bonus authored, never applied), `MISSING` (ending computation, F05 allocation model, drain-slowed feedback), `RETIRE` (old flat drops + old ledger).

### Change list

* Apply the tier bonus in the completion transaction (`StoryManager.OnLevelComplete` / `RecordLevelResultToSave`), itemized on `LevelResultsPanel` alongside mob/extractor/boss lines that already exist.
* New ending resolver reading the 15 required level IDs from `IntegrityByLevel` with the 750-point unrounded comparison; consumed by `CampaignCompletionSequence` / `Level15Controller`.
* Reward manifests: a per-level-and-difficulty stable-source reward table (new resource family under `resources/Content/` or per-level `.tres`), the largest-remainder allocator, and wiring in `StoryDropSystem`/`EnemyController`/`BossEncounterController`/`ChronalExtractor` so drops read the manifest instead of `EnemyData.ChronalDustDrop`/`BossData.ChronalDustDrop`/`ChronalExtractor.DustReward`.
* Set all 16 boss awards to **25** (15 `.tres` + `BossData` default + `MirrorParadoxController.cs:300`, and remove the Mirror wallet-direct path).
* `ChronalDustPickup` tier selection: Extractors use actual quantity (drop the forced-Large flag from `StoryDropSystem.SpawnDustAward` for the Extractor source).
* Boss HP: `borgia_inquisitor → 350`, `siegemaster_duke → 520`, `chronal_inventor → 700`, `revolutionary_tribunal → 850`. *(Cross-workstream: the enemy/boss section owns the table; listed here because F05/ruling 2.E carries it.)*
* Author Florence's three Extractors (the standing content gap) with their 10-dust optional split.
* Replace `docs/DUST_ECONOMY.md` with the F05 ledger; rewrite `tests/ContentValidation/DustEconomyTests.cs` wholesale.
* E01: "Integrity drain slowed" cue key + a suppression guard for untimed levels and the locked clock; Level 2's Sarah intro line update.

**Size: L** (economy re-plumbing) / the ending resolver alone is **S–M**. **Shared:** `StoryDropSystem`, `EnemyController`, `BossEncounterController`, `LevelResultsPanel`, all 15 boss resources, all enemy resources, the content manifest, `DustEconomyTests`.

---

## B8. Save schema & persistence (F10) — the shared-file hot spot

### Design requirement

Root Story fields keep `currentHP`, `currentUltimateMeter`, `currentLives`, `checkpointIntegrity`, `levelChronalDust`, `depositedChronalDust`, `gridProgress`, `completedLevels`, `levelIntegrity`, **`unlockedLegacyAbilities`**, `isCompleted`; global gains `seenDialogueIDs`. Everything per-attempt moves into a new root `attemptState: StoryAttemptState`:

`attemptID`/`levelID`/`revision`; `status` ∈ {Active, AwaitingHubResume, RecoveryPending, Smothered, CompletionPending, Completed, LegacyRecoveryRequired}; `anchorChargesRemaining` (0..cap, zero outside Act III); `defyHistoryUsed`; `checkpointRecord` (anchor ID + **role**, baseline encounter IDs/version, activated checkpoint IDs, granted-benefit IDs); `currentIntegrity` + **immutable** `startingExtractorCount`; `destroyedExtractorIDs`; `foundSecretIDs`; `puzzleStateByID`; `preBossLocked` + `finalGateIntegrity`; `fontUsesByID`; `collectedHealingIDs`; `pendingHealing`; `rewardSources` (unissued / spawned-uncollected / collected + quantity + pickup type/location); `playerResourceTimers`; `recoveryEvent` (incl. F11 checkpoint/role, difficulty, budget version, resolved minimum); `sealReadiness`; `completionTransaction`; `appliedScriptEffectIDs` + `pendingGlobalSeenIDs`; `presentationFlags`.

Behavioural rules that differ from today: ordinary load restores the **latest durable** HP/meter/Integrity, **not** checkpoint values; `checkpointIntegrity` is only the paid-recovery allowance; a granted **timer** Collapse/Snap applies `max(checkpointIntegrity, recoveryMinimum)` once; a locked boss clock stays locked on reload; Restart Level resets the gauge to 100%; **crash recovery charges nothing**; Smothered persists before its presentation; critical events commit before being acknowledged; one ordered async writer per slot with revision ordering; never mix a newer wallet with older claims.

**F11 recovery minimum:**
`allAliveDrainPerSecond = 100 / (parSeconds × difficultyMultiplier)`
`requiredRecovery = ceil(allAliveDrainPerSecond × remainingRouteSeconds × (1 + 0.20))`
`recoveryMinimum = max(25, requiredRecovery)`
`timerRecoveryIntegrity = max(checkpointIntegrity, recoveryMinimum)`
A calculated minimum **above 100 is an invalid content budget**, never clamped to 100. Applies **only** to a timer-caused Collapse that actually grants recovery — not ordinary load, not death rewind. Per enabled level/variant/checkpoint/difficulty authoring row required; **no measured values exist yet**.

### Repo today

* `StorySaveData` (`scripts/Core/SaveManager.cs:9–76`), **schema v5** (`SaveSchemaMigrator.CurrentVersion`). Per-attempt state is a flat scatter of root fields added in V7.3: `ActivatedCheckpointIDs`, `FontUsesConsumed`, `DestroyedExtractorIDs`, `FoundSecretIDs`, `LevelIntegrityPercent`, `ViewedDialogueIDs`, `HasSeenCollapseBeat`, plus `LastCheckpointID`, `CurrentHP`, `CurrentLives`, `CurrentUltimateMeter`.
* `StoryManager` mirrors those in in-memory `HashSet`s (`_activatedCheckpoints`, `_destroyedExtractors`, `_foundSecrets`, `_fontUsesConsumed`, `_bossIntrosSeen`) with `WriteAttemptStateToSave` (378) / `RestoreAttemptStateFromSave` (389) / `ClearLevelAttemptState` (368).
* Resume detection is `IsMidLevelResume` (218): active save parked on this scene path with a non-empty `LastCheckpointID`. There is **no attempt ID, no status enum, no revision counter, no reward-source ledger, no recovery-event record**.
* `RestoreSavedCheckpoint` (`StoryLevelControllerBase.cs:419`) restores **checkpoint-time** HP/meter (`Player.RestoreStoryCheckpoint(position, save.CurrentHP, save.CurrentUltimateMeter)`) — exactly the behaviour F10 explicitly supersedes.
* Envelope is AES-256-CBC + encrypt-then-HMAC with atomic write/backup (keep); the writer is synchronous, single-shot per call.

### Status `PARTIAL` — the V7.3 registry family is the right *shape* for about a third of `attemptState`; everything else is `MISSING`.

### Change list

* New `StoryAttemptState` class + root field; migrate the seven V7.3 root fields into it (schema **v6**, with a real migration, not field-initializer defaults — F10 forbids "just default every new field" and requires `LegacyRecoveryRequired` when history cannot be reconstructed).
* `StoryManager`: attempt lifecycle (`attemptID` minted only on fresh entry / Restart Level), `status` transitions committed before presentation, `revision` monotonic.
* `StoryLevelControllerBase.RestoreSavedCheckpoint`: stop restoring checkpoint HP/meter; restore latest durable values.
* New `F11RecoveryBudget` resource/table + the `recoveryMinimum` resolver; reject above-100 budgets loudly.
* Ordered async slot writer + revision guard in `SaveManager`.
* Tests: `LevelAttemptPersistenceTests` (existing, 4 cases) rewritten; new suites for status routing, recovery-event idempotence, reward-source claims across rewind/snap/quit/crash/restart, F11 arithmetic + invalid-budget rejection, migration to `LegacyRecoveryRequired`.

**Size: L.** **Shared:** `SaveManager.cs` / `SaveEnvelope.cs` (every workstream that adds a field), `StoryManager.cs`, `StoryLevelControllerBase.cs`, `tests/unit/SaveEnvelopeTests.cs`.

---

## B9. Resonance Grid (V7.6 layouts, prerequisiteMode, ability-scoped stats, free respec)

### Design requirement

* Node count unchanged: **9 nodes = 3 Tier 1 minors (50) + 3 Tier 2 minors (75) + 3 Majors (200) = 975**. The **"three straight branches of three" template is retired**; topology is authored per character (Einstein mesh, Joan spine, Leonardo interlocking gear rings, Tesla two-rail circuit, Shakespeare three-act tiers, Mozart ascending scale, Cleopatra converging delta, Lincoln fence behind one shared post, Pocahontas two crossing currents).
* **Every root-adjacent node must modify a system available from Level 0** (basics, block, Rally, HP, speed, meter) — otherwise the first hub visit shows an empty grid under the hidden-node rule.
* **Hidden nodes keep their silhouette:** a node gated on a locked ability renders as a dim, unlabeled "dormant resonance" star **in its true position**; it gains name/cost/tooltip on unlock.
* **`prerequisiteMode`:** `All` (every listed node) or `Any` (at least one). `All` drawn solid, `Any` dotted, with explicit requirement text in the tooltip. Empty list = root-eligible. **Reject missing prerequisite IDs and cycles in authored data.** Use one shared node/ability eligibility evaluator for UI availability, purchase, respec dependency checks and save validation; only purchases test the wallet. **Shakespeare's Act II nodes use `Any [T1, C1, H1]`; all his Majors use `All [T2, C2, H2]`** (F08 Option B → his first Major costs **475**).
* Schema additions: `ResonanceNode.prerequisiteMode` (default `All`), `ResonanceNode.gatedAbilityID`, `StatModifier.abilityScope` (ability/construct/status ID, null = character-wide). New `StatType` keys: ability-scoped `AbilityDamage`, `AbilityRange`, `AbilityDuration`, `ConstructHP` (always paired with `abilityScope`); character-wide `RallyEchoFraction`, `ExtractorDamage`. **Traversal and Major nodes carry no `statModifiers`** — they are rule flags read by ability scripts (`ResonanceFlags.Has("einstein_float_plus20")`).
* **Stat-node hygiene:** minors never touch stun/hitstun duration, never grant rewind charges, never modify the Integrity drain rate. Indirect interaction only — e.g. bonus damage to Chronal Extractors (`ExtractorDamage`).
* **Traversal nodes are Tier 2 (75 dust), hidden until the ability they modify unlocks.** Named revisions: Joan **Wings Refresh** (reset Ascendant Wings cooldown on a direct Hit 3 or Righteous Smite hit; purchased, not baseline), Lincoln **Rail Breaker** (destroys one breakable hostile projectile per Rail Charge; not baseline, not a universal roll property), Cleopatra **Vortex Step** (halves Desert Mirage cooldown when activated inside her own Sandstorm Vortex, once per vortex), Shakespeare — the traversal verb moves off the non-existent "barrier" onto **The Tempest's lift, jumpable at its apex**.
* For effects requiring both Movement and Special 2 (Quicksand Grip, Vortex Step) set `gatedAbilityID` to **Special 2** — the fixed schedule guarantees Movement is already unlocked.
* Cheapest route to a first Major ranges **325–475** dust; **no universal promise of two complete branches**.
* **Respec:** free, always available, full refund, standard confirmation modal. Act III: the **Beacon** offers the same grid + free respec using deposited dust only.
* **Access/exit:** Repository terminal in the hub, or an Act III activated checkpoint's Beacon; Cancel returns to the access point (hub navigation, or the same Act III checkpoint).

### Repo today

* `scripts/Environment/ResonanceNodeData.cs` — `NodeID`, `DisplayName(Key)`, `Description(Key)`, `Type`, `UnlockCost`, `string[] PrerequisiteNodeIDs`, `StatModifierKey`/`Value`/`IsPercent` (a **single** modifier, not an array), `AbilityModifierKey`. **No `prerequisiteMode`, no `gatedAbilityID`, no `abilityScope`.**
* `ResonanceProgression.cs:58–59` — prerequisites are hardcoded **All** (`foreach … if (!unlocked.Contains(prerequisite)) return MissingPrerequisite`). `RespecAll` (88) exists and is free. Stat resolution (135–161) is a `switch` on `StatModifierKey` with ~22 string keys: `MaxHP`, `BlockCharges`, `MoveSpeed`, `JumpForce`, `BasicAttackDamage`, `SpecialDamage`, `CooldownReduction`, `AttackRange`, `ComboSpeed`, `BlockRecovery`, `KnockbackForce`, `ProjectileSpeed`, `ProjectileDamage`, `GlideSpeed`, `GlideDuration`, `ZoneRadius`, `ZoneDuration`, `PersistentDuration`, `PersistentRange`, `PersistentHealth`, `StatusDuration`, `StatusDamage`. **No `RallyEchoFraction`, no `ExtractorDamage`, no ability-scoped keys.**
* All nine `resources/Resonance/*_grid.tres` are the retired template: three linear chains (`{id}_u1 → u2 → u3`, `o1 → o2 → o3`, `p1 → p2 → p3`), each Tier 1 with `PrerequisiteNodeIDs = PackedStringArray()` and each later node naming exactly one predecessor.
* `ResonanceGridPanel.cs` — respec button + confirm modal present; `MissingPrerequisite` messaging present; **no dormant-star rendering, no dotted-line drawing, no deposit-disabled mode**.
* `tests/ContentValidation/DustEconomyTests.cs:39` infers tier from prerequisite count — breaks the moment a Tier 2 node has an empty `Any` set or a Tier 1 node gains a link.

### Status `PARTIAL` (economy + respec correct; schema, topology, gating and scoped stats all missing).

### Change list

* `ResonanceNodeData`: add `[Export] PrerequisiteMode PrerequisiteMode = All` (new enum), `[Export] string GatedAbilityID = ""`, `[Export] string AbilityScope = ""`, and an explicit `[Export] int Tier` so cost/tier stop being inferred from prerequisite count.
* `ResonanceProgression`: single shared eligibility evaluator honouring `PrerequisiteMode` + ability gate, used by UI/purchase/respec/save-validation; authored-data validation rejecting unknown prerequisite IDs and cycles; new stat keys `RallyEchoFraction`, `ExtractorDamage`, and the ability-scoped `AbilityDamage`/`AbilityRange`/`AbilityDuration`/`ConstructHP` (scoped resolution needs a per-ability query, not the flat `StoryStatProfile` accumulators).
* Re-author all nine `resources/Resonance/*_grid.tres` to their V7.6 topologies (Section 5 has the layouts — cross-workstream with the character section); Shakespeare's `Any [T1,C1,H1]` / `All [T2,C2,H2]` is the explicit test case.
* `ResonanceGridPanel`: dormant-star rendering, dotted `Any` connections, requirement text in tooltips, deposit-disabled Beacon mode, exit-to-access-point.
* Rewrite the traversal minors for Joan/Lincoln/Cleopatra/Shakespeare per the V7.6 corrections (each a Tier 2 flag node, hidden until its ability unlocks).
* Hygiene guard test: no minor touches stun/hitstun, rewind charges, or the Integrity drain rate.
* Localization: dormant-node tooltip, `Any`-prerequisite requirement text, renamed traversal nodes.
* Tests: `ResonanceProgressionTests` (Any/All matrix, cycle rejection, gated-node refusal), `ResonanceGridSceneTests` (dormant star, dotted lines), `DustEconomyTests` grid-cost assertion re-expressed against `Tier`, root-adjacency rule (every Tier 1 node modifies a Level-0 system).

**Size: M–L.** **Shared:** nine grid resources (character workstream), `ResonanceGridPanel` (UI workstream), `DustEconomyTests`.

---

## B10. Level 0 tutorial — new calibrations

### Design requirement (the beats that changed)

* **Part 1 is now an authored 7-beat sequence:** cold open (normal palette, 2–3 per-character opening lines) → the beam (cold column, everything it touches **desaturates**) → **the pull (playable)**: a directional pull force drags the character toward the beam while the movement tutorial runs, prompt *"Hold away from the light"* alongside the standard move/jump prompts → **the ignition**: warm gold streams from the legend's world into the character, who gains the **persistent golden resonance aura kept for the entire game** (this frame is the per-character illustrated still) → the beam breaks → hazard evasion toward a **Warden rift** (clean/steady/geometric) beside the Unbound's jagged tear → the Translation.
* **Grab Calibration (new, V7.6):** the dummy raises and holds its own shield; prompt *"A raised shield stops a blade, not a hand — hold Block and press Attack to grab."* Player must land **one grab and throw**; follow-up names the rule both ways — *"Grabs beat blocks. Strikes beat grabs."* The dummy's block stance **persists until the grab lands**, so a player who keeps swinging sees everything absorbed and is re-prompted.
* **Hitstun Agency Calibration (new, V7.6):** the dummy lands one scripted **launching** hit, then two prompts in verb order — (1) **DI**: during the launch's hitstop the game holds the freeze **a beat longer than normal (tutorial-only)**, prompt *"Hold a direction as the blow lands to steer where you fly."*; any held direction visibly bends the launch **±15°**; the beat proceeds even if nothing is held. (2) **Landing tech**: prompt *"Hold Block as you land to recover on your feet"*, with a **slowed approach on the first attempt**; a missed tech plays the full knockdown and the scripted launch **repeats until one tech lands**. The launch is **free** — no HP, no Rally accounting, no rewind charge. Both are scripted-hit exemptions; tech works at **0 block charges** by the V7.3 ruling.
* **Meter & Defy History Calibration (replaces the Special and Ultimate calibrations):** Specials, the Movement Ability and the Ultimate **cast** are no longer calibrated in Level 0 — they unlock across Act I. Level 0 still fills the meter to 100%, teaches *"A full meter can also refuse death itself — once."*, then the dummy lands one scripted **lethal-tagged** hit that **Defy History** absorbs, spending the meter. **F13:** show the seal **lit** before the proc and **broken** afterward; the coaching text/HUD guide explains that refilling the meter does not restore a spent Defy, and that a **dim intact** seal means unused but below full meter. No extra meter award, no extra Defy use.
* **Part 3 traversal:** the vertical section is authored for **base jump reach only** — the Movement Ability calibration is deleted (it unlocks after Level 1 with its own optional Wren drill). Ledge grab and drop-through calibrations unchanged.
* **Rewind + Time Freeze calibration (2026-09-11 revision):** keep the scripted lethal hit and the full automatic death rewind (0.75 s hold, continuous playback, grounded landing) and explain the finite pool, including that spending the last charge still saves the hero. Then teach **Time Freeze** on `gameplay_time_freeze` (**R / Back / Select**): freeze **5 s** while moving to safety; enemies invulnerable, offensive inputs unavailable; each drill retry starts with the ability ready; the scripted death demo consumes no persistent charge; explain the separate long cooldown. **Retire the Stasis Echo plate gate** — required progression uses ordinary resource-free mechanisms.

### Repo today

`scripts/Environment/TutorialCalibration.cs` step enum and transitions:
`BasicHits → RallyReclaim → Block → UseSpecial → UseUltimate → UseRewind → UseManualRewind → Done` (with alternate skip edges at 88 and 134/141). `Level00Controller` drives it. No grab step, no hitstun/DI/tech step, no Defy step, no Time Freeze step; `UseSpecial`, `UseUltimate` and the movement-ability beat all still assume a full kit at Level 0; the manual-rewind + Stasis Echo plate gate is the final step.

### Status `PARTIAL` (framework and prompt plumbing exist; four steps wrong, two missing).

### Change list

* `TutorialCalibrationStep`: `BasicHits, RallyReclaim, Block, **Grab**, **HitstunDI**, **LandingTech**, **MeterAndDefy**, DeathRewind, **TimeFreeze**, Done`. Delete `UseSpecial`, `UseUltimate`, `UseManualRewind`.
* `Level00Controller`: scripted dummy shield-hold, scripted launcher with the extended tutorial hitstop and ±15° DI read, repeating free launch until a tech lands, scripted lethal-tagged hit for Defy, Time Freeze drill; remove the Movement-Ability gap in Part 3 (re-author for base jump reach) and the Stasis Echo plate gate.
* HUD: Defy seal lit/broken/dim-intact states (F13) — `StoryHUD` ultimate indicator.
* Part 1: pull force + desaturation + ignition aura + two-rift presentation (heavy presentation work; the persistent gold aura is a `GlowPresentationController` channel that must survive the whole game).
* Localization: grab prompt + follow-up, DI prompt, landing-tech prompt, Sarah's tech line, Defy tip + seal explanation, "Hold away from the light", Time Freeze prompts; retire the manual-rewind/Stasis-Echo tutorial keys.
* Tests: `ScriptedRewindTests` + a new `TutorialCalibrationTests` for the step order, the free-launch exemptions (no HP/Rally/rewind accounting), tech at 0 charges, Defy once.

**Size: M** (mechanics) + **M** (Part 1 presentation). **Shared:** `PlayerController` (grab/DI/tech exemptions), `StoryHUD`, Time Freeze (Section 4 workstream owns the ability itself).

---

## B11. Hub, act-boundary gate, NPC dialogue (mostly naming + small additions)

* **Renames throughout §3:** "Archive Time-Ship" → **Warden Time-Ship**; "Chrono-Resistance" → **Wardens**; "Apex Archive / cult / Archive" → **Unbound**; Level 14 "Neo-Earth / Far Future" → **The Unbound Bastion** (*scene ID `Level_14_NeoEarth` retained in code*); Level 15 "Library of Alexandria Restoration" → **The Meridian Founding** (*scene ID `Level_15_Alexandria` retained*). Repo impact is **localization + dialogue resources only** — `localization/en.csv` level titles/objectives/dialogue lines, `resources/Dialogue/*.tres`, and prose comments. Scene paths, `CampaignLevel` members and manifest IDs stay. **Size: S** but wide; `CampaignLocalizationTests` + `UnusedTranslationKeyTests` will police it.
* **Act-boundary portal gate** (before Level 1, after Level 5, after Level 12) is unchanged in rule; its Sarah conversations now carry the Mystery Thread. Repo: `HubWorldController` has the portal + Repository + auto-deposit (`AutoDepositCarriedDust`, line 63) but **no act-boundary gate implementation** was found — verify against the dialogue workstream; if absent it is `MISSING`, **S**.
* **Observation Deck** gains the diegetic shard-search framing (Acts I–II) — dialogue content only.
* **Wren** additionally hosts the optional Legacy Unlock drills (B3).
* **Sarah's act-boundary speech before Level 13** is the gauntlet setup; Act III comms stay live throughout (no extraction).

---

## B12. Cross-cutting notes for the merge

**Shared files other workstreams will also touch** (ranked by collision risk):

1. `scripts/Core/StoryManager.cs` — touched by B1, B3, B4, B5, B7, B8. Effectively rewritten. **Serialize this one.**
2. `scripts/Core/SaveManager.cs` (`StorySaveData` + `SaveSchemaMigrator`) — B3, B4, B6, B8, plus the settings/UI workstream's Reduced Temporal Effects toggle. One schema bump, one migration, coordinated.
3. `scripts/Core/TimelineIntegrityRules.cs` — B1, B6, B7 (all mine; safe to own).
4. `scripts/Environment/StoryLevelControllerBase.cs` — B1 (par/clock), B2 (`FractureEligible`), B5 (checkpoint roles), B8 (durable-resource restore).
5. `scripts/Environment/LevelManager.cs` (`CheckpointTrigger`) — B5 role export; also read by Acts I–II Hard-middle rule work.
6. `scripts/UI/StoryHUD.cs` — B1 (clock face), B2 (tick), B4 (anchor pips), B10 (Defy seal). HUD contract doc (`HUD_CONTRACT.md`) is another workstream's.
7. `scripts/UI/LevelResultsPanel.cs` — B3 (Resonance Restored), B6 (rating removal), B7 (tier bonus itemization).
8. `localization/en.csv` — every item; expect heavy churn and a `--headless --import` regeneration of `en.en.translation` (per CLAUDE.md, the CSV alone is not enough).
9. `resources/Content/content_manifest.csv` — B5 (nine 4A rows), B7 (boss/reward rows).
10. `tests/unit/TimelineIntegrityTests.cs`, `tests/ContentValidation/DustEconomyTests.cs`, `tests/unit/LevelAttemptPersistenceTests.cs`, `tests/unit/SessionExitGuardTests.cs`, `tests/integration/CampaignRouteTests.cs` — all materially rewritten; the 1638 baseline will move substantially and must be reconciled deliberately, not "bigger than before" (CLAUDE.md failure signature 6).

**Explicitly pending in the design (do not invent numbers):** every `parSeconds`, every F11 `remainingRouteSeconds`/`recoveryMinimum`, all per-source F05 reward IDs and values, all nine 4A scene manifests, and the difficulty multipliers themselves are flagged as playtest targets. `DESIGN_BUILD_DEVIATIONS.md` (P04) requires any implementation-vs-target gap to be recorded with a pinned build rather than silently adopted — this dossier is the input to that ledger, and P04's "Confirmed current-build deviations: none verified" section is empty precisely because the docs workspace had no repo access.
