# Package 12 — 2026-09-26 design alignment: implementation plan

**Status:** Phase 0 committed 2026-09-27; Waves 1–2 in progress.

## 0. Sources and baseline

| Input | What it is |
|---|---|
| `D:\Projects\fighters-through-time-docs-3\design-godot-v7.md` (docs-3 `e13838f`, SHA-256 `6b21f455…`) | The current design master. The repo mirror `design-godot.md` (`d2e306c6…`) is one revision behind it. |
| `docs-3\DESIGN_REVIEW_2026-09-24.md` | 26 core findings (R01–R04, H01–H05, M01–M37), the §4 Low items and G01–G15. **All were resolved in design on 2026-09-26; implementation is pending for every item.** G08 (content rating) was deferred by the user. |
| `docs-3\docs\*.md` | Contracts. Eleven differ from `docs/design-contracts/`, and `LEGACY_LEVELS.md` is new. |
| `docs/DESIGN_GAP_REVIEW_2026-09-16.md` (untracked) | The implementation-gap review GAP-01…GAP-20. Re-verified on 2026-09-27: every item is still open. |
| `AGENTS.md` "What Package 11 did NOT deliver" + the repo ledger | The Package 11 leftovers. |

**Build baseline:** `599d5b8` plus the uncommitted 2026-09-14 death-rewind work-in-progress:
- `ChronalRewindBuffer` is 300 frames.
- The `PathMovingPlatform` history is tied to the buffer capacity.
- There is no post-rewind input lock.
- `PostRewindWorldFreezeFrames = 60`.
- The matching tests are updated.

**Test baseline:** 2237, plus whatever the in-place WIP edits change. Reconcile that before Wave 1.

**Verdict on the GDD diff.** Every substantive change between the repo mirror and the master traces to a review item: an R/H/M/G ID, or an ID-less "Low-item decision, 2026-09-26". Nothing untracked was found.

## 1. Status summary (from the 2026-09-27 recon)

| Status | Items |
|---|---|
| **Already implemented** (verify and pin only) | R01 (uncommitted), H01, M03 (comment cleanup only), M09 (add pins for Conductive/zone-buff reset), M21, M37, Emancipator 40.0, Hard rewind pool 5/3/1 |
| **Partial** | R02, R03 (~30 %), R04, H03, H05/G01, M02, M04, M06, M07, M10, M11, M14, M15, M17, M18, M20, M23, M24, M25, M26, M27, M29, M35, G05, G09, G11, G15b, block-cancel on Specials, guard-break push, Tesla blink, Mozart charges, Second Glide, Spirit Strike, the Sarah hub sets, the hazard toggle, the glow stack order |
| **Missing** | H02 (the build *contradicts* it), H04, M05, M08 (mostly), M13, M16 (the build contradicts it), M19, G04, G06, G10, G12, G13, G14, G15a/d/e, Tidal Overseer rename, Ultimate beat at L4 results (the code deliberately does the opposite), Cleopatra decoy, `{HomeEraName}`/`{HeroAddressName}` |
| **Doc-only in the repo** | M12 (copy), M22 (mirror + backfill), M28, M30–M33 (asset briefs), M36, most §4 wording |
| **Older gaps still open** | GAP-01…GAP-18 (GAP-19/20 are launch/validation obligations), plus the P11 leftovers: `IsActIII` includes 4A, `ClearSlot` re-announce hack, shape-query specials vs `PersistentObject`, three 4A gate stand-ins, Eraser interim dust |

**Stale records to correct (Phase 0):**
- `VERIFY-SERPENT-NEST` is closed in the build: `cleopatra/special_1.tres`, 6 HP / 60 f.
- The AGENTS "spawn invulnerability does not read `InvulnerabilityFrames`" line is stale: `FighterSimulationDriver.cs:634` does read it.
- `VERIFY-PAR-SECONDS` says "no budgetVersion", but `StoryAttemptState.cs:170` has `BudgetVersion`.
- AGENTS still lists macOS as a target (G03 cut it).
- The AGENTS construct numbers ("nest 10 per 4 s") are stale.

## 2. Decisions needed before the named workstream starts

Each decision blocks only the workstream in brackets; everything else can proceed. Recommendations are marked **(rec)**.

| # | Question | Options | Blocks |
|---|---|---|---|
| D1 | **M01 fast-fall 20 u/s "snaps to terminal".** Story's terminal is 10 u/s (600 px/s); the sim has no terminal at all; AGENTS lists H-7 gravity unification as deferred. | (a) **(rec)** Fast-fall = 20 in both modes, and add a stateless sim terminal clamp at 20. Story raises its terminal only on the fast-fall path, and H-7 stays deferred. (b) Full H-7 unification now. (c) Record a deviation. | W3 |
| D2 | **M04** names Echo Step wind-up a sub-phase of `UsingMovementAbility` and tech a sub-phase of `Rolling`. Taken literally, this collides with Suppression's movement lock, `CanActivate`, the roll pushbox and invulnerability. | (a) **(rec)** Keep them as flags; document the mapping as a deviation. (b) Literal state moves. | W3 |
| D3 | **R03 frozen set.** The design says "same as Time Freeze, including Extractors", but Time Freeze deliberately excludes `chronal_extractor`. Also: does a Collapse resume reached through a quit/reload get a hold? | (a) **(rec)** The union of both sweeps; the hold is granted only on the in-session placement, never on reload. (b) Other. | W1 |
| D4 | **H05/G01 Versus CPU.** CPU matches already work through the select-screen toggle. Does that toggle survive the new Fighter Play Options menu? | (a) **(rec)** Remove it; Versus CPU becomes its own entry. (b) Keep both. | W5 |
| D5 | **M24 Resonance Surge.** The design says "rulesets where meter pickups are wanted", but no such ruleset exists. Adding a fifth type changes every seeded orb schedule. | (a) Add it to the default draw. (b) **(rec)** Add it behind an Items sub-toggle, off by default. (c) Defer. | W5 |
| D6 | **M35 variant keys.** The design wants `KEY__heroid`; the repo uses `_heroid`, and AGENTS forbids renaming keys. | (a) **(rec)** Keep `_heroid` for existing keys, use `__` for new ones only, and record the deviation. (b) Rename everything. | W7 |
| D7 | **Steam scope.** G02 Cloud, the G11 overlay pause, G15c rich presence and RPT all need Steamworks, which the P11 plan put out of scope. | (a) **(rec)** Keep Steam out of this package: build the non-Steam halves now and implement the Steam hooks as no-op interfaces. (b) Integrate Steamworks now. | W6 |
| D8 | **G13 Toggle Block exit rules** (charge depletion, jump, roll). | (a) **(rec)** A pure input-layer latch: released by the player's own Jump/Roll/Block presses, while depletion is left to the sim's existing empty-shield refusal. No sim state. (b) A sim latch, which needs a new component. | W6 |
| D9 | **G09 crash reports.** There is no upload endpoint, and the About page's location is unclear (M27 says four tabs). | (a) **(rec)** Local dump plus "open folder" only, no upload; About is a section inside the Gameplay tab. (b) Stand up an endpoint. | W6 |
| D10 | **`VERIFY-BOSS-HP`.** Ten boss rows conflict with the design table. | Apply one ruling to all 15 rows. | W9 |
| D11 | **`VERIFY-STORY-PITS`.** Author lethal pits, or ratify that every gap is non-lethal. | — | W8 |
| D12 | **`DEFER-SEALING-ANCHOR` / GAP-05.** Is `AwaitingSeal` persisted or derived? This matters more now that M12's copy promises sealing. | (a) **(rec)** Persisted in the F10 attempt record. (b) Derived. | W8 |
| D13 | **Level 4A content approval.** `LEGACY_LEVELS.md` marks all nine rows "To author", but all nine variants ship. Three questions: backfill the rows from the build and approve them, whether Joan's Tourelles setting violates N02, and what the Princeton/Vienna/Tidewater mob rosters are. The 4A bosses are a flat 700 HP with no L4/L5-derived band. | Backfill and approve / re-author. | W9 (4A bosses), W7 (M22 doc) |
| D14 | **GAP-09 hazard block layer.** D03c's Basic-class 1-charge block is sim-only; adding it to Story changes hazard survivability in every level. | Add it / record a deviation. | W3 |
| D15 | Small values: the Eraser `ChronalDustDrop` (interim 10), the `VERIFY-PARIS-DEEP-PIT-RECOVERY` ruling, the `DEFER-CPU-SNAPSHOT` ruling, and whether `level_15_extractor_2` "on the firestorm route" must move off the critical path (M16). | — | W8/W9 |

### 2.1 Adopted for this run (2026-09-27)

The user asked to start work on all items without ruling on §2 individually. Workstreams therefore apply every **(rec)** option above:
- D1(a), D2(a), D3(a), D4(a), D5(b), D6(a), D7(a), D8(a), D9(a), D12(a).
- D13 in part only: backfill the `LEGACY_LEVELS.md` rows from the shipped build and mark them **Proposed, awaiting approval**. No 4A content changes.

Each adoption is recorded in §9 so it can be reversed.

**Held until the user rules; workstreams must not touch these:**
- D10: boss HP rows.
- D11: lethal Story pits.
- D13: 4A boss HP bands, the Princeton/Vienna/Tidewater mob rosters, Joan's setting.
- D14: the Story hazard block layer.
- D15: the Eraser dust value, the Paris pit, the CPU snapshot, the Level 15 stand-in placement.

Each held item stays open in the ledger.

## 3. Standing decisions

1. **Authority.** Section 2.1 of the Package 11 plan applies unchanged: the GDD and contracts define intent, and the build is evidence. Every item closed here either changes the build or gets a ledger entry.
2. **Save schema bumps once, to v7, in Phase C.** New fields are additive, with field initializers. Workstreams declare their derivations, and Phase C composes them into the single v6→v7 step. The derivations:
   - Hazard `HazardRate` → `StageHazardsEnabled`: Off→false, anything else→true.
   - `lowestDifficultyUsed` is seeded from `Difficulty`.
   - H02 reconciliation: a Completed attempt with `LevelChronalDust > 0` deposits once.
   - The global comfort, input and dialogue fields.
   Save payloads stay engine-free (failure signature 7).
3. **Klotho components.** 305 and 310 are full, 311 is retired, and 312–319 are taken. This plan allocates **320 = `FighterKnockdownComponent`** (MaxCount 2) for M05. No other workstream may allocate an ID. The Tesla projectile pass-through must *derive* from the existing movement-ability phase frames; if it cannot, record the need and do not take an ID.
4. **Deterministic hash changes are expected.** M01, M05, M23, M24, the hazard toggle, Tesla blink and block-cancel on Specials all move hashes. Every workstream that moves a hash reruns the rollback-readiness and per-stage hash suites, and lists which ones changed in its §9 entry.
5. **Story/Fighter isolation is unchanged.** The Post-Landing Hold, the dust banking, G14 and the Mozart charges are Story-only. G12 palettes, G13 deadzones and the toggle latch are local input/presentation and stay out of snapshots and hashes, like Reduced Temporal Effects.
6. **Legacy identifiers are retained.** The rename to "The Tidal Overseer" changes the English value and `DisplayName` only; `tidal_eraser` and its keys stay. `hub_calibration_bay` stays a key.
7. **Presentation stays placeholder.** H03 grab/throw frames, H04 cues, the M24 sprites, the G05 font and the G12 glyphs ship as contract-conformant placeholders. Audio stays digitally silent, per the 2026-08-10 directive. Real assets are Package 10.
8. **`en.csv` policy, merge discipline and crash hygiene** follow Package 11 sections 2.11–2.13:
   - `en.csv` uses per-workstream `# Package 12 <WS>` markers.
   - Only the orchestrator regenerates `en.en.translation`, with `--import`.
   - GdUnit runs are serialized across checkouts.
   - Every `Total:` is compared against the exact expected delta.

9. **Shared-file policy.** Three files are touched by more than one workstream:
   - **`PlayerController.cs`:** W1 owns the revive/protection region, W2 the resource-publish/restore region, W3 the hit-intake region, W4 the ability-specific hooks.
   - **`StoryManager.cs`:** W2 owns completion/attempt; W7 owns the unlock beat.
   - **`MainMenu`:** W5 owns the Fighter entry; W6 owns the rest.

   Edits to any of these are **additive and region-local**. An agent that needs another region records the need in its handoff rather than editing it. `en.csv` is append-only under the workstream marker, plus value edits to keys the workstream owns.
10. **Test discipline.** Build before every commit.
    - Before running GdUnit, poll `Get-Process testhost,Godot_v4*`, wait for a clear window, and never run while another checkout is running.
    - Run filtered suites for the touched areas, then one full run immediately before hand-off.
    - Declare the exact test delta in the handoff.
    - One `[TestSuite]` class per file.
11. **Handoff.** Each workstream writes `docs/handoffs/P12_<WS>.md` and appends its §9 entry. The handoff states what shipped, what did not, the test delta and any hash changes.

## 4. Phase 0 — prep (serial, main checkout) — DONE 2026-09-27

The rewind WIP was committed as `21b0747` (58/58 filtered). The design mirror, eleven contracts and `LEGACY_LEVELS.md` were synced. VERIFY-05 was added. VERIFY-SERPENT-NEST was closed as stale. The PAR-SECONDS, AGENTS macOS, construct and spawn-invulnerability records were corrected. The `ChronalRewindBuffer.cs` comment was re-read and is already correct.


1. Commit the 2026-09-14 rewind work-in-progress (R01) on its own, with a green filtered run of `ChronalRewind|DeathTriggeredRewind|LevelDeathRewindDiagnostic|MirrorParadox|StoryCombatRules`.
2. Re-copy `design-godot.md` from the master.
3. Re-copy the eleven changed contracts into `docs/design-contracts/`, and add `LEGACY_LEVELS.md`. **Do not overwrite the repo ledger** (the master's ledger is behind it); add only `VERIFY-05` (R04) to the repo copy.
4. Correct the stale records listed in §1, and fix the stale `ChronalRewindBuffer.cs:26` comment.
5. Decide what to do with `docs/DESIGN_GAP_REVIEW_2026-09-16.md` (commit it as evidence, or fold it into this plan).
6. Commit this plan.

## 5. Wave 1 — parallel worktrees (merge order as listed)

### W1 — Story recovery and world suspension
**Items:** R02, R03, M17, GAP-06, GAP-11.
- **R02.** Add an explicit Story revive edge, `Dead` → `Idle`/`Airborne`, so that no `Respawning` enter/exit fires. `Respawning` stays Fighter-only.
- **R03 Post-Landing Hold.** Applies to the death rewind, the Collapse resume, the Anchor Snap and the Level 0 scripted rewind.
  - One shared `IsWorldHeld` query: the freeze gate at `Hitbox.OnAreaEntered` extends to the hold, so frozen actors are invulnerable and give no credit.
  - The union sweep (D3), using the Time Freeze suspend path rather than the attack-cancelling rewind freeze.
  - Blocked during the hold: `TimeFreezeController.CanActivate`, checkpoint strikes, pickups, the Font, puzzles and sealing.
  - The 2 s protection is armed at the thaw.
  - New events `OnRecoveryLanded` / `OnRecoveryWorldThawed`.
  - A thaw cue through `PlayCriticalCue`; the music duck ends at the thaw.
  - Boss transitions are deferred to the thaw.
  - The hold is never persisted.
- **M17.** No extraction wording in Act III: suppress `collapse_transmission_line` on the Act III beat, and sync the era-lost value.
- **GAP-06.** Subscribe to `OnBossHistoricalRecovery` for the world-wide 90-frame suspension, and author the L15 `HistoricalRecoveryAnchor`.
- **GAP-11.** The Level 10 audience respects Time Freeze.
- **Owns:** `ChronalRewindManager`, `TimeFreezeController`, `Hitbox`'s freeze gate, `RewindPresentationOverlay`, the revive and protection region of `PlayerController`.
- **Tests:** one case per R03 decision (8), plus the R02 never-enters-`Respawning` pin, the L10 freeze, and the L15 boss suspension. Close `VERIFY-05`.

### W2 — Dust banking, attempt resources, reward sources
**Items:** H02, GAP-01, GAP-02, GAP-04, GAP-13, Eraser dust value (D15).
- **H02.**
  - Deposit only in `CommitCompletionTransaction`, for all levels.
  - Delete the hub `_Ready` and Repository deposits.
  - The attempt wallet survives hub returns; Restart clears it.
  - The Repository shows the held attempt dust (new key); `hub_dust_deposited_toast` is reworded.
  - Declare the v7 reconciliation derivation.
- **GAP-01.** Publish block charges, the regen/lockout timers, ability and Echo Step cooldowns, the uncredited Rally, and the D02d delay; restore them in `RestoreStoryCheckpoint`. Add a real-player round-trip test covering repeated reload and Snap.
- **GAP-02.** The nine 4A manifests use `unbound_eraser`. `RewardManifestTests` reads the trigger's live ID. Add a 15-dust collection test.
- **GAP-04.** Summon provenance on `EnemyController` and `EnemyKilledPayload`; summons draw no award.
- **GAP-13.** An explicit per-controller encounter-baseline map, plus a resume reader.
- **Owns:** `HubWorldController`'s deposit region, `StoryManager`'s completion/attempt writers, `LevelRewardDirectory`, the reward manifests, `StoryAttemptState`.

### W3 — Combat data contract (both modes, part 1)
**Items:** M08, M06, M09 pins, block-cancel on Specials, guard-break push, M01 (after D1), M04 (per D2), GAP-09 (per D14).
- **M08.**
  - New `AbilityData` fields: `HitstunFrames`, `BlockClass`, `Launches`, `Delivery`, `Origin`. Author all 36 `.tres`.
  - `HitPayload` gains the source actor, contact ID, `Launches`, `Delivery` and `Origin`.
  - Introduce `IDamageable.TakeDamage(in HitPayload)`, and make `IStatusEffectTarget` coexist with it.
  - Replace the slot-derived ultimate-origin and zone-tick flags with the authored `Origin`/`Delivery`.
  - Project the fields into `FighterLoadout` (loadout only, not snapshot).
- **M06.** Block-escape excludes tumble.
- **Block-cancel on Specials.** Recovery only, both modes, with a parity test.
- **Guard-break push.** Story assigns rather than adds; X is derived from the attacker's side in both modes.
- **Owns:** `AbilityData`, `HitboxSystem`, `Hitbox`, `Hurtbox`, `BasicComboRules`, `FighterDamageRules`, the hit-intake region of `PlayerController`, `BlockSystem`.

### W5 — Fighter mode flow
**Items:** hazard On/Off toggle with per-stage cadence, M23, M24 (per D5), M25, H05/G01 (per D4), M26, G15a, G15b, G12.
- **Hazard toggle.** Per-stage authored cadence in the stage hazard specs. Retire `HazardTriggerFrequency` from the rules, the match component and the selectors. Overtime doubles the cadence. Declare the v7 normalization.
- **M23.** The PRNG picks spawn timing inside the window, with a 600-frame floor and an occupied-anchor skip. All draws come from `match.RandomState`.
- **M25.** Four 20 px radials (S1/S2/Movement/Echo Step), with Echo Step dimmed below 30 meter.
- **H05/M26.**
  - A Fighter Play Options screen.
  - A P1-only select, then `HolodeckConsolePanel` as a front-end screen.
  - A `FighterMatchOrigin` value on `SessionData`, never persisted.
  - Holodeck Reconfigure, and returning to the hub at the console marker.
- **G15a.** Random tiles seeded from the per-match seed.
- **G15b.** A CPU "Medium" label key, with the enum ordinals unchanged.
- **G12.** P1 ▲ / P2 ● shapes and three slot palettes. Local only.
- **Owns:** `FighterEntitySystems`' orb/hazard regions, `FighterStageGeometry`'s hazard specs, `MatchSettings`/`GameManager`, `CharacterSelectScreen`, `FighterHUD`, `LocalFighterPause`, `MatchResults`, `MainMenu`'s Fighter entry.

### W6 — Front end, settings, accessibility
**Items:** G04, G05, G06, G09 (per D9), G10, G11 (non-Steam half, per D7), G13 (per D8), M27, G14, G15d, G15e, the D7 Steam no-op interfaces.
- **G04.** `application/run/max_fps=60`; physics interpolation stays off.
- **G05.** A default font plus a reserved fallback slot in `ftt_theme.tres`, using a placeholder face.
- **G06.** A photosensitivity notice on every launch (skippable after the first), and a first-run setup: Reduced Temporal Effects preview, UI Scale, controller detection.
- **G09.** Crash-marker detection (the `SessionExitGuard` pattern), an Ask/Always/Never setting, and About & Privacy.
- **G10.** Text Speed 20/30/60/Instant replaces the fixed `DialogueRevealPacing` 30 cps. A session-only 100-line Dialogue Log, fed by both the completion path and the skip path.
- **G11.** Controller loss and focus loss auto-pause Story and the hub (Fighter already does). Mute When Unfocused defaults On. Pause hand-back rules apply.
- **G13.** Block Hold/Toggle, plus per-device deadzone and down-threshold sliders, applied before quantization.
- **M27.** A "UI & Dialogue" slider driving both buses, per-category mutes that keep `PlayCriticalCue` intact, and the Vibration rows moved to Controls.
- **G14.** Lower difficulty one step from the hub: never raise it, never mid-level, never in Act III. `lowestDifficultyUsed` is shown in the slot summary.
- **G15d.** `application/config/version` shown on the menu and logged at boot.
- **G15e.** Extras menu: a Credits replay that must **not** call `MarkCampaignCompleted`, and Third-Party Licenses (Godot, Klotho, K4os LZ4, Newtonsoft.Json, LiteNetLib).
- **Owns:** `SettingsMenu`/`Settings.tscn`, `InputManager`'s processing, `DialogueRevealPacing`/`DialogueManager`'s log, `PauseMenuBase`'s focus handling, `MainMenu` apart from the Fighter entry, `ViewportEnforcer`, `project.godot`'s app settings, and `GlobalSaveData` fields (additive).

### W7 — Narrative, dialogue, hub
**Items:** M12, M13, M14, M35 (per D6), M11, the Tidal Overseer rename, the Sarah hub sets with GAP-15, the Ultimate beat at L4 results, M22 backfill (per D13).
- **Copy rewrites** (values only; key names stay): `dlg_l00_intro_5`, `dlg_l15_ending_*`, `dlg_l12_exit_*`.
- **M13.** Split Level 12 into a pre-boss setup and a new post-boss trace scene on the post-boss tail; de-duplicate `exit_4`.
- **M14.** Add `level_08.postboss@leonardo` and `@joan`, `level_03.exit@leonardo` and `@joan`, and an absence-list rule test.
- **M35.**
  - Add `{HomeEraName}` and `{HeroAddressName}` tokens, and the nine-hero name table (display, address and possessive forms).
  - Rewrite the "traveler" lines.
  - Add a pseudo-locale test (+40 %, HUD at 90–140 % UI Scale).
- **M11.** A Bridge arrival anchor.
- **GAP-15.** A hub act-boundary selector with once-only tracking. Sets: prologue / Act I / Act II / post-campaign epilogue. Retarget `hub.sarah_act3`.
- **Ultimate beat.** Remove the Ultimate exclusion from the L4 results beat, and give 4A a flavour Nexus opening.
- **Mandatory gates:** `NarrativeKnowledgeBoundaryTests` and `CampaignLocalizationTests`.
- **Owns:** `resources/Dialogue/`, `CampaignCaptiveRoster` (tokens), the arrival/NPC region of `HubWorldController`, `StoryManager`'s unlock-beat region, `en.csv` values.

## 6. Wave 2 — after Wave 1 merges

### W3b — Launches and knockdown (both modes, part 2)
**Items:** M05, M07.
- An authored `Launches` flag replaces the "every knockback hit launches" rule. Non-launch hits get grounded knockback.
- A missed tech leads to a 30-frame invulnerable knockdown, then a neutral (10 f) or roll (14 f) get-up.
- The sim uses **component 320**; update the snapshot, hash, and `ROLLBACK_STATE_CONTRACT` inventory.
- Lincoln's Hit 2 gets `Launches`.
- Add the two new `COMBAT_VALIDATION` scenarios.
- **Expect large test churn:** parity, CPU hitstun defense, tech, and DI. Retune the CPU hitstun-defense linger if escapes change.

### W4 — Kit alignment
**Items:** Tesla blink, Mozart Sonata Drift charges, Pocahontas Second Glide, Spirit Strike vector, Cleopatra sand decoy (GAP-10c), GAP-10a, GAP-10b, M03 cleanup, GAP-14.
- **Tesla blink.** 6/12/10 f over 3.0 units (3.5 with Long Blink; confirm the perk is wired). Projectile pass-through during the translation, in both modes, derived from the phase.
- **Mozart.** Two charges recharging one at a time on the 5 s cooldown; landing on his own platform halves the remaining recharge. Story only.
- **Second Glide.** Must not restart the cooldown.
- **Spirit Strike.** Pocahontas rises about 3 units at 45° over 15 f while the eagle hitbox dives. Parity check against the sim.
- **Cleopatra sand decoy.** A 1 s decoy, which Royal Aegis depends on.
- **GAP-10a.** A multi-hit Lorentz Pulse consumer in the sim that uses `TryConsumeChain`.
- **GAP-10b.** Spirit Strike becomes a sim mobility special; flip `MobilitySpecialFor` and close `VERIFY-CPU-MOBILITY-SPECIALS`.
- **GAP-14.** A Nexus-origin fence using W3's `Origin`.
- **Owns:** `scripts/Characters/Abilities/*`, `resources/Abilities/*`, `CpuRecoveryProfile`, the sim ability dispatch.

### W8 — Campaign mechanics
**Items:** M16, GAP-03, GAP-08, GAP-05 (per D12), M02, M10, GAP-16 (per D11), the P11 4A gate stand-ins, the shape-query specials.
- **M16.** `ResonanceHoldNode` becomes a 180-frame held-Interact channel (the Font channel is the precedent). It has no HP and no discharge, is reset by damage, range or release, and is blocked in Time Freeze and the hold. On completion: −0.2 drain, an Extractor-allocation pickup, and the destroyed-extractor registry for persistence.
- **GAP-03.** Place a `SecretCache` at every reserved `{level}.secret`, Levels 1–15 plus all nine 4A variants (through a hook in the sealed `BuildLevel`). Add a full-collection walk test reaching 1,000.
- **GAP-08.** A Reset Puzzle station, owned `WeightedObject`s and a plate allowlist.
- **GAP-05.** A reusable sealing anchor on the boss levels and 4A.
- **M02.** Drop `PersistentObject` from both body masks, and move `MovableWeight` to `Environment` so the Level 6 puzzle keeps working.
- **M10.** Orléans volley cover (an occluder on `StoryCyclicHazard`), or a recorded acceptance.
- **4A gate stand-ins.** A fifth `LegacyGateMode` (cast-watch) plus an `EnemyHurtbox` option on the strike surface; retire `LegacyCastGateWatcher`, `VineSnareGateResolver` and `LegacyResonantEffigy`.
- **Shape-query specials.** They also hit `PersistentObject` environment hurtboxes.
- **Owns:** `StoryLevelControllerBase`'s content hooks, `LegacyLevelControllerBase`, `ResonanceHoldNode`, `SecretCache`, the puzzle toolkit, `CollisionLayers`, level controllers.

### W9 — Enemies and bosses
**Items:** M18, M19, M20, GAP-07, D10, the D13 4A bosses and rosters, D15.
- **M18 Borgia.** No speed multiplier; an `after_image_dash` in phase 2; the scaffolding burns and the arena shrinks.
- **M19.**
  - Add `BossPhaseTrigger { HpThreshold, MemberDefeat }`.
  - A multi-body squad path in `BossEncounterController`: one bar, one 25-dust award, ability hand-off to the survivor.
  - Tribunal becomes 2 × 425 HP, with the Level 4 centre-pit collapse.
  - Also fix `IsKnockbackImmune` and the rest-cooldown drift.
- **M20.** Give the Rift Phantom a Teleport line, which needs a standard-enemy secondary-ability path. The SummonMinions drone-carrier line is a new enemy, pending a design row.
- **GAP-07.** Jackal Priest P2 decoy, the Tragedy King actor-completion invulnerability plus trapdoors, the Chronal Inventor's destroyable coils, and a per-boss phase-rule matrix test over all 15.
- **Audit.** Check every boss that uses speed-only phase multipliers against "Rule 1".

### W10 — Presentation hooks
**Items:** H03, H04, M15, glow stack order, `StatusController.ClearSlot`.
- **H03.**
  - Extend the contract to 30 names.
  - `down_attack` → `down_air` in the builder, the nine `.tres`, the normalizer and both call sites.
  - Placeholder `grab`/`throw` rows.
  - The driver maps `Grabbing`/`Thrown`.
- **H04.**
  - A single cue-catalog resource with silent streams.
  - Subscriptions to the existing `EventBus` events.
  - Per-stage hazard warning and impact slots in `StageAudioSet`.
  - Information-bearing cues on `CriticalCues`.
  - Fighter cues stay driver-side.
- **M15.** 8 px diamond pips, a crack on Snap, the hollow last-stand icon, and an accessible label. Check the reorder against `HUD_CONTRACT`.
- **Glow order.** Spawn > armor > control > damage, splitting the single status layer in two.
- **`ClearSlot`.** A per-slot event payload replaces the re-announce hack.

## 7. Phase C — closeout (serial, main checkout)

1. The single v7 save migration, composing the declared derivations. Tests for the v6→v7 pair.
2. Regenerate `en.en.translation` with `--import` and commit it. Retire orphan keys onto the roster.
3. Delete `StoryLevelControllerBase.IsActIII`.
4. Rewrite AGENTS.md and CLAUDE.md in the same change. At minimum:
   - Hub dust auto-deposit → completion-only banking.
   - `Respawning` → Fighter-only.
   - The Post-Landing Hold.
   - Knockdown and component 320.
   - The hazard toggle.
   - Versus CPU.
   - Settings and accessibility.
   - macOS dropped.
   - The new test baseline.
5. Ledger: close VERIFY-05, SERPENT-NEST, SECRET-CACHE, CPU-MOBILITY-SPECIALS, STORY-PITS, BOSS-HP and SEALING-ANCHOR as each lands. Add deviation entries for D2, D6 and anything else recorded.
6. Three consecutive full-suite runs in verified clear windows. Report the exact total and reconcile it against every workstream's declared delta.
7. An honest §10 "not delivered" list.

## 8. Out of scope for this package

| Item | Where it belongs |
|---|---|
| G02 Steam Cloud, G11 overlay pause, G15c rich presence, RPT (GAP-19) | Unless D7(b) is chosen. The non-Steam halves ship. |
| M34 online disconnect, public queue, the native online/LAN flows | Package 7 |
| GAP-12 par measurement, GAP-20 V01/F09/V02/S01 validation, platform and hardware runs, balance | Package 9, which needs human playtests. **Nothing in this package should be called "balanced".** |
| GAP-18 and the art/audio halves of H03, H04, M24, M29–M33, G05, G12 | Package 10 |
| G08 content rating | Deferred by the user |
| H-7 full gravity unification (unless D1(b)), M-18 drop-through, `DEFER-ROSTER-ENUM`, `DEFER-SAVE-VERSION-SPLIT` | Deferred |

## 9. Deviations (append-only)

*(Empty. Each workstream appends its entry here at merge.)*

### W5 — Fighter mode flow (branch `p12/W5`, handoff `docs/handoffs/P12_W5.md`)

- **Adopted D4(a):** the CPU toggle and CPU difficulty are removed from the Local Versus select. Versus CPU is its own Fighter-menu entry (`scenes/menus/FighterPlayOptions.tscn`): a P1-only select, then `HolodeckConsolePanel` in `FrontEnd` mode, then the match. Reversal: re-add `ModeRow` and `TokenDrivenBy`'s CPU branch.
- **Adopted D5(b):** Resonance Surge (orb type 4, +15 meter) is behind the Items sub-toggle "Meter pickups", **off by default**. Default orb schedules draw from the same four types as before. It adds one int (`MeterPickupsEnabled`) to `FighterMatchComponent` (301, ~108/128 B); no new component ID.
- **Hazard toggle.** `HazardTriggerFrequency` is deleted. `FighterHazardCadence.AuthoredFrames` holds the per-stage cadence, which is 2700 frames for all ten stages, seeded from the retired Medium value. `FighterMatchComponent.HazardFrequency` became `HazardCadenceFrames` in the same slot. Deviation: the cadence lives in a code table in `FighterEntitySystems.cs`, keyed by hazard type, not in `FighterStageGeometry` (hazard type and stage are 1:1). A non-zero cadence below 60 frames throws. This is a guard against stale frequency ordinals.
- **Declared v7 derivation:** `SavedMatchSettings.DeriveStageHazardsEnabled(legacyHazardRate)` maps Off→false and anything else→true. Until Phase C, `Normalize()` applies it only when the new nullable `StageHazardsEnabled` is null. No version bump. The payload stays engine-free.
- **M23 timing windows** (not specified numerically by the design) are Low 3000–4200, Medium 1200–1800 and High 600–720 frames. They are centred on the old fixed intervals and floored at 600. A spawn attempt with every anchor occupied spawns nothing. Anchor safety against hazard footprints is not checked at runtime; it relies on authoring.
- **Moved hashes:** every items-on match, because the first orb gap is now drawn at world init and each spawn draws its gap. Items-off and default-hazard matches are unchanged. The High-cadence suites now pass an explicit 1800-frame override, so their timings are unchanged. No golden hash is pinned anywhere.
- **Pre-existing bug fixed in passing:** `FighterProxyStyle.OrbColor` cast the sim orb ordinal onto Story's `OrbEffect`, so Chronal Haste rendered gold. The mapping is now explicit (`PaletteEffectFor`).
- **Orphaned keys recorded** on `UnusedTranslationKeyTests.RecordedOrphans` for Phase C to delete: `fighter_local_human`, `fighter_cpu_selection`, `fighter_pick_cpu_prompt`, `fighter_hud_cooldown_ultimate`. The ceiling moved from 12 to 16.
- **Settings:** exactly one new Gameplay row (`PlayerSlotPaletteRow`, self-wiring). `SettingsMenu.cs` is untouched.
- **Not done:** Random stage "Open-only / Sealed-only" option; any per-stage cadence tuning; visual review of every new surface.
