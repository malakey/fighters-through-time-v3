# Package 11 — V7.5/V7.6 design alignment: implementation plan

**Status:** Authored 2026-09-13 from the nine-dossier reconnaissance under `docs/recon-2026-09-13/`
(A_narrative, B_campaign, C_time_freeze, D_combat, E_status_save_dialogue_net, F_roster_grids,
G_enemies_bosses, H_env_ui_hud, I_fighter_audio_script — ~600 KB, read in full). Baseline at
authoring time: `main` @ `31fed14` (V7.4 stun-lock pass, 2026-08-29), **1638 passing tests**, clean
tree.

**This plan is the single brief for every implementation agent in this package.** Each workstream
dossier in §3/§4 is written to be self-contained: an agent holding this plan, its own recon dossier
(`docs/recon-2026-09-13/<letter>_*.md`), `design-godot.md` and `docs/design-contracts/` must be able
to implement without asking further questions. Where a number, constant name, file path, translation
key or test name appears here it was copied from the dossiers verbatim — do not paraphrase numbers.

**Authority order (P04, locked — see §2.1):** explicit user instruction > `design-godot.md`
(the V7.6 + F01–F24 mirror) and the adopted contracts under `docs/design-contracts/` > this plan >
existing code and `.tres` data. Where the build and the design disagree and this plan does not
explicitly close the gap, the discrepancy is recorded in
`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` — **that repo copy is the live ledger from now
on.**

**Read §9 (Deviations) before touching anything.** Append-only. Every agent appends
`### <workstream ID> — <subject> (date)` blocks in the Package 5/6 format: a bold one-sentence
claim, then the reasoning and the pinning test.

---

## 1. Scope

Package 11 closes the gap between the shipped V7.4 build and the V7.5/V7.6 design master plus its
F01–F24 resolution contracts. Nine recon dossiers enumerate roughly 120 discrete items; this plan
groups them into 20 workstreams across two parallel waves and one serial closeout.

### What this package delivers

**Story systems**
1. **Time Freeze (F03)** replaces Manual Rewind, the Stasis Anchor, the Stasis Echo and the 12 s
   manual cooldown outright (recon C). Story-only, 5 s, 45 s cooldown armed at thaw, no charges.
2. **Timeline Integrity becomes the level timer (F01)**: normalized live drain against an authored
   per-level `ParSeconds`, difficulty multipliers 2.0/1.5/1.2, collapse at zero, tier lines 50/20,
   the PreBoss freeze, and the Collapse Tremor at <20% / <10% (recon B §B1/B2).
3. **Checkpoint roles (F12)**, **F11 recovery minima**, **F16 lethal Story pits**, and the retirement
   of the abnormal-exit fee (ruling 2.B) and Chronal Rating (ruling 2.A).
4. **Act III gauntlet**: Warden Beacon, anchor charges 3/2/1, Anchor Snap, the Smothered Game Over,
   no-hub chaining for Levels 13–15, and the F10 `AttemptState` record with load routing.
5. **Legacy Unlock Schedule (V7.5)** with Dormant/Suppressed/Clear ability-slot lock states and a
   rebuilt Level 0 calibration (Grab, Hitstun Agency/DI/tech, Meter + Defy with the F13 seal, the
   Time Freeze drill).
6. **Level 4A Legacy Level**: a shared `LegacyLevelControllerBase` plus nine per-character variants
   at the campaign's "functionally complete, placeholder presentation" bar, routed as a 17th slot.

**Combat**
7. **Defensive contract D01–D04** in both modes: resolution ordering, shield lifecycle with 8 s
   granted-shield timers, Defy protected recovery (60 ticks), F15 full shatter, F17 lockout fix, the
   Rally reclaim clamp and reclaim-source corrections.
8. **Echo Step V7.6 determinism**: the 31-sample per-tick position ring, exact `t−30` destination,
   exact-or-no-teleport validation, dedicated `gameplay_grab` / `gameplay_echo_step` actions,
   protocol v3, `CharacterState.Thrown`, same-frame chord priority.
9. **Status architecture**: `StatusSlots` / `IStatusEffectTarget` / `StatusRouting`, stronger-wins
   replacement at all four sites, `StatusType.Suppression` with the ability-lock gate, and the F07
   Tesla Conductive mark with the double-stun fix.
10. **Fighter match rules**: F21 mode consolidation (stocks-lost), F22 Sudden Death contract.

**Content**
11. **Narrative V7.5**: the Unbound/Wardens renames, the retired power-fade arc, the Level 12
    knowledge boundary, the L14/L15 retheme, Mystery Thread absence beats with N03 hero-recognition
    variants and N04 captive selection.
12. **Resonance Grids V7.6**: nine unique authored topologies, `prerequisiteMode`, ability-scoped
    stat keys, nine traversal flags, eight reworked Majors, a real topology renderer.
13. **The Eraser elite (V7.6)** with Null Lance and the Siphon Snare channel, plus placements.
14. **Bosses**: the L1–L4 HP rows, display renames, the F20 Mirror Paradox profile, the First
    Unbound P2 self-rewind and Borrowed Legacies P3.
15. **Dust economy F05**, **Calibration Drills F18**, **three Open Fighter stages** with pits and
    ledges, **F19 CPU recovery**, and the **HUD/comfort/audio** contract work (F24, C01a/b/c).

### Explicitly OUT of scope (see §8 for the full list with rationale)

Package 7 native online/LAN, achievements, the retired Level Select/records surface, artist assets,
Steam SDK integration beyond a localized "Steam Remote Play Together" label, advanced Training Mode,
the H-7 gravity unification, and the deliberate sim drop-through input divergence.

---

## 2. Standing decisions (all agents — read before writing code)

These are orchestrator rulings. They are not open for re-litigation inside a workstream; a
workstream that believes one is wrong records the objection in §9 and proceeds as written.

### 2.1 P04 authority: the GDD defines intent, the build is evidence

`design-godot.md` (the V7.6 + F01–F24 mirror) and the adopted contracts under
`docs/design-contracts/` define **intended behavior**. Verified code, `.tres` data and scene
bindings at a pinned revision describe the **current build** — they are evidence about that build,
not authority to reverse an approved target.

When the two differ: **close it by changing the build**, or — only where this plan explicitly defers
it — record it in `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` with a stable deviation ID, the
intended behavior with its decision reference, the observed value with commit `31fed14` and the file
identifier, owner (`Unassigned` unless a workstream claims it), and concrete acceptance criteria.
A deviation does not become the new target merely because it shipped.

CLAUDE.md's "If a document and a `.tres` resource disagree, the resource wins" and AGENTS.md's
"Resources own the numbers… The `.tres` files remain the law if this table ever drifts" are **to be
rewritten at closeout** (Phase C). Until then, agents follow this section, not those files. The
orthogonal rule survives untouched: **never create a second canonical value** in a factory, UI
script or document — tuning still lives in one place.

### 2.2 Boss HP: apply only the V7.6 2.E rows

Apply **exactly four** `MaxHP` edits:

| Boss resource | Level | Current `.tres` | New value |
|---|---|---|---|
| `resources/Bosses/borgia_inquisitor.tres` | 1 | 500 | **350** |
| `resources/Bosses/siegemaster_duke.tres` | 2 | 540 | **520** |
| `resources/Bosses/chronal_inventor.tres` | 3 | 560 | **700** |
| `resources/Bosses/revolutionary_tribunal.tres` | 4 | 590 | **850** |

Every other boss `MaxHP` **stays as shipped**. The design table's Act II/III values (`tidal_eraser`
1050 vs 640, `vulcan_decimator` 1200 vs 660, `dread_admiral` 1350 vs 700, `jackal_priest` 1500 vs
700, `iron_chancellor` 1700 vs 780, `tragedy_king` 1850 vs 800, `siege_cannon` 2000 vs 880,
`gravity_overseer` 2200 vs 950, `archive_prime` 2500 vs 1050, `apex_eraser` 3000 vs 1200) are
recorded as **one open documentation conflict** in the deviation ledger under ID `VERIFY-BOSS-HP`,
owner Unassigned, acceptance = an explicit user ruling on which curve is intended plus a re-tuned
`BossRosterActITests` band. Do not touch those ten values. Do not "reconcile" the curve.

Consequence: `tests/ContentValidation/BossRosterActITests.cs:57-67`
(`ActIHealthPoolsStayInBandAndAscendWithTheCampaignLevel`) hardcodes `AssertThat(previous).IsEqual(500)`
for Borgia and a **500–700 band** for levels 2–7 with strictly ascending order. 350 is below the
floor and 850 above the ceiling, and 850 > 640 (`tidal_eraser`) breaks ascension. **A7b rewrites that
test** to pin the four authored V7.6 values explicitly and assert ascension only across the
unchanged Act II/III tail, and records the band retirement in §9.
`tests/ContentValidation/EnemyManifestTests.cs:36` (`AssertThat(boss.MaxHP).IsEqual(500)`) → 350.

### 2.3 Legacy identifier retention (V7.5 rule)

**Retained, never renamed:** campaign scene IDs and paths (`Level_14_NeoEarth.tscn`,
`Level_15_Alexandria.tscn`), `CampaignLevel` enum member names (`NeoEarth`, `Alexandria`), level IDs
(`level_14_neo_earth`, `level_15_alexandria`), boss and enemy resource IDs (`archive_prime`,
`apex_eraser`, `chrono_slasher`, `tidal_eraser`), ability IDs, `PresentationEventID` strings
(`boss.apex_eraser.*`, `boss.archive_prime.*`), sprite atlas paths, pool config IDs, dialogue
sequence IDs (`level_14.entrance`), and **all translation key names** (`neo_earth_*`,
`alexandria_*`, `boss_apex_eraser_name`, `speaker_archive_prime`).

**Changed:** English values in `localization/en.csv`, the non-localized `DisplayName` debug fields on
the two boss `.tres`, and prose/XML-doc comments.

`chrono_warden` / `enemy_chrono_warden_name,Chrono-Warden` is **retained** despite the Wardens now
being the player's faction — master line ~2948 rules it "a stolen title worn mockingly". Add that
lore note as a comment in `resources/Enemies/chrono_warden.tres`; no rename, no test change.
`ability_tesla_wardenclyffe_cataclysm_*` is Wardenclyffe Tower, unrelated, untouched.

This ruling is what keeps `ScenePoolConfigTests`, `StoryAudioSetContentTests`,
`RosterVfxMappingTests`, `LoadingScreenTests`, `Level14ContentTests`, `Level15ContentTests` and the
content manifest green through the entire narrative rewrite.

### 2.4 Level 4A Legacy Level: shape and routing

Build a shared **`LegacyLevelControllerBase`** (extends `StoryLevelControllerBase`) plus **nine
per-character variants**, at the campaign's existing "functionally complete flow, placeholder
presentation" bar — the same bar Levels 2–15 meet today.

- **Enum:** append `CampaignLevel.LegacyNexus = 16`. **Do not renumber** `Tutorial=0 … Alexandria=15`.
- **Routing:** `StoryManager` gains an **explicit ordered route list**
  `[0, 1, 2, 3, 4, 16, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]` replacing `CurrentLevel + 1`.
  `AdvanceToNextLevel()` steps that list; `GetLevelScenePath` resolves by ID, not array index.
- **Scene path per hero:** `res://scenes/campaign/Level_04A_<hero>.tscn` where `<hero>` is the
  lowercase character ID (`Level_04A_einstein.tscn` … `Level_04A_pocahontas.tscn`). Level ID is
  `level_04a_<hero>`.
- **Checkpoints (F12 Option A):** **exactly two** — `Entry` (`{levelID}_checkpoint_0`, self-activating
  once on fresh entry) and `PreBoss` (`{levelID}_checkpoint_1`, strike to activate). Both enabled on
  Easy, Normal and Hard. **No Middle.** Hard's "middle inactive" rule must not disable 4A PreBoss
  merely because its ID ends `_1` — enablement and the boss-clock lock read `checkpointRole`, never
  the ID suffix.
- **Nexus Resonance Source (F04)**: a puzzle-only source at the Ultimate set-piece. Armed by
  interacting after the preceding encounter is cleared, in a safe area with no enemies and no
  meter-drain hazards; the normal Ultimate input then casts the real Ultimate against the designated
  puzzle target at **any meter value including zero**, neither filling nor consuming the meter.
  Leaving the bounded cast area, death, reload or an interrupted cast clears the arming; the source
  re-arms on return, unlimited retries, no cost. Successful resolution latches the gate, disables
  the source, records completion. **Never saved armed.** Time Freeze cannot arm it or cast it, and it
  never lights the F13 Defy seal.
- **Eraser debut:** an independent authored route trigger `{levelID}_eraser_debut` between Entry and
  PreBoss, independent of checkpoint activation and difficulty; grants no checkpoint, Mending,
  rewind refill or Integrity lock.
- **Restoration Font:** exactly one, on the late approach **after** the Eraser and **before**
  PreBoss, with existing difficulty uses/potency. It is a healing object, not a third checkpoint.
- **Boss:** per-character, two phases, standard boss rules, **25 dust** as a Large physical pickup.
  A variant **may reuse an existing era boss resource with a variant HP row** — each B-wave agent
  must state in §9 exactly which resource its variant reuses and what HP override it applies via
  `BossData` duplication (`resources/Bosses/legacy/<hero>_legacy_boss.tres`), never by editing a
  shared boss resource.
- **Dust:** per `docs/design-contracts/DUST_ECONOMY.md` — **15 required-encounter + 25 boss + 10
  optional** for every variant regardless of layout or enemy count.
- **Ending:** the selected hero's one 4A counts in the N05 15-level average; the other eight are
  excluded.

### 2.5 Borrowed Legacies (First Unbound P3) is data-driven projection

At P3 encounter start, build the boss's borrowed kit by projecting **every roster character except
the active hero** through a runtime projection layer:

- Roster comes from `ContentManifest.ForCategory(ContentCategory.Character)` minus
  `GameManager.Instance.CurrentSession.SelectedCharacterID`. **Never a literal nine-element list.**
- For each borrowed character, load its **Special 1** `AbilityData` via
  `FTT.Core.AuthoredResources.Load<AbilityData>()` and project it into a runtime-constructed
  `EnemyAbilityData`:
  - **Archetype by ability shape:** projectile-shaped → `EnemyAbilityArchetype.Projectile`;
    melee-shaped → `MeleeStrike`; zone-shaped → `AreaPulse`.
  - `Damage` = the ability's `BaseDamage`.
  - `TelegraphFrames` = the ability's authored startup frames.
  - The remaining fields take the archetype's existing defaults; nothing bespoke is invented.
- Selection is **weighted-uniform** across the projected set, gated to phase 3 through the existing
  `AbilityMinPhase` mechanism.
- The projected resources are runtime-only `new EnemyAbilityData()` instances; **never write them to
  disk**, never enter them in the manifest, never pass them through `AuthoredResources`.
- A content test must prove the rule scales: for each of the nine possible active heroes, the
  borrowed set is exactly the other `rosterCount − 1`.

### 2.6 Save schema bumps ONCE to v6

`SaveSchemaMigrator.CurrentVersion` is **5** today (`scripts/Core/SaveEnvelope.cs:274`), shared by
both the story and global payloads. This package bumps it to **6 exactly once**.

Every workstream adds its fields **additively** to `StorySaveData` / `GlobalSaveData` with C# field
initializers, following the v4→v5 precedent (older payloads load with defaults, no migration step
needed for the additive half). **Only the Phase C closeout agent** flips `CurrentVersion` to 6,
writes the single v5→v6 migration step, and writes the one `SaveEnvelopeTests` v5-loads-with-v6-
defaults + v6-round-trip pair.

| Field | Payload | Owner | Notes |
|---|---|---|---|
| `TimeFreezeCooldownSeconds` (float) | `StorySaveData` | **A2** | Live freeze writes 45; explicit Save/exit ends the freeze and writes 45 |
| `UnlockedLegacyAbilities` (`Dictionary<string, List<string>>`) | `StorySaveData` | **A5** | Per character: `movement` / `special1` / `special2` / `ultimate` |
| `CheckpointIntegrityPercent` (float) | `StorySaveData` | **A3** | The paid-recovery allowance, separate from the live gauge |
| `AttemptState` (`StoryAttemptState` object) | `StorySaveData` | **A3b** | The F10 record, §4.3; the six loose V7.3 attempt fields migrate into it |
| `AnchorCharges` (int) | `StorySaveData` | **A3b** | Act III Beacon charges; zero outside Act III |
| `SeenDialogueIDs` (`HashSet<string>`) | `GlobalSaveData` | **A5** | Global across slots; union on completion **or** confirmed skip |
| `ReducedTemporalEffects` (bool, default false) | `GlobalSaveData` | **A8** | C01a |
| C01c binding flags — explicit `UnboundActions` (`HashSet<string>`) + per-device shortcut enable flags (`Dictionary<string,bool>` keyed `action\|deviceKind`) | `GlobalSaveData` | **A1c** | `InputBindingSet.Normalize()` currently **deletes empty rows**, so explicit Unbound is unrepresentable today — this is a hard blocker on the C01c shape, not just a schema bump |
| `StoryDefyHistoryUsed` (bool) | `StorySaveData` | **A1b** | F10: "once per level" means once per attempt; today `_storyDefyHistoryUsed` is a private `PlayerController` field with no persistence, so a mid-level resume restores the Defy |

Two fields are **deliberately deleted rather than added**: `RatingByLevel` (ruling 2.A retires
Chronal Rating) stays in the payload as a dead field through v6 — removing it is a breaking change
we are not taking in this package — and A8 records that in §9. `ViewedDialogueIDs` (per-slot) is
**retained** for effect/reward idempotency and as the migration source for the global set; A5 stops
writing it as the skip gate but keeps writing it as the effect ledger.

Migration rule from F10 that the closeout must honour: **never default an active legacy attempt to
full anchors / unused Defy / unused healing / unclaimed rewards.** If essential history cannot be
reconstructed, retain the original save and balances and mark
`StoryAttemptStatus.LegacyRecoveryRequired`, telling the player an explicit Restart Level is needed.

### 2.7 Klotho component IDs are assigned here and nowhere else

`FighterRuntimeComponent` (ID 305) is **exactly full at 128 bytes**. `FighterVerbComponent` (ID 310)
has **4 bytes of headroom** — 19 `int` (76 B) + 6 `FP64` (48 B) = 124 B. One more `int` fits;
nothing else. `FighterEchoRingComponent` is ID 311.

New sim state goes to **new components at these IDs, assigned once, here**:

| ID | Component | Owner | Contents |
|---|---|---|---|
| **312** | `FighterDefenseComponent` (MaxCount = 2) | **A1b** | D04 `DefyProtectionAwaitControl` (int flag), `DefyProtectionFrames` (int), proc/presentation identity, and the Aegis/barrier layer state |
| **313–317** | `FighterEchoStepRingComponent` bank (MaxCount = 2 each) | **A1c** | The 31 consecutive per-tick position samples. 31 × `FPVector2` = 62 `FP64` = 496 B, so it **cannot** fit one component; use a bank of five at 8 samples each (8 × 16 B + ints, comfortably under 128 B) plus head / `LatestTick` / `ValidCount` / `Generation` on 312. Retire 311 in the same change. |
| **318** | `FighterConductiveComponent` (MaxCount = 2) | **A1** | F07 Conductive marks: remaining frames, source player ID, per-execution chain-consumed state |
| **319** | `FighterSuddenDeathComponent` (singleton) | **A1c** | F22 phase generation, frozen regulation stocks-lost totals, `DefyDisabledByPhase` |

**Nobody else may allocate a Klotho component ID in this package.** An agent that believes it needs
new sim state and has no assigned ID stops and records the need in §9 rather than picking a number.
Note that A1's Conductive mark is Story-primary; the sim-side component 318 exists so the Tesla
finisher behaves identically in Fighter Mode — it is snapshot and hash state like everything else.

### 2.8 Enum contracts

All of these are **append-only**. Ordinals serialize into `.tres` files and into the deterministic
sim (which casts `StatusType` to `int` in ~30 places in `FighterEntitySystems.cs`). **Never reorder,
never renumber, never reuse a retired ordinal.**

| Enum | Addition | Owner | Gate |
|---|---|---|---|
| `FTT.Core.StatusType` (`scripts/Core/EventBus.cs:260`) | `Suppression` appended as the next value (**= 6**) after `Root` | **A1 only** | Other workstreams may reference `StatusType.Suppression` **only after A1 merges** — i.e. from Wave 2 onward. A Wave 1 agent that needs it stubs and records it. |
| `PlayerController.CharacterState` | `Thrown` appended **after** `Grabbing` | **A1c** | `Dashing` keeps its ordinal (reserved, never reused) |
| `ResonanceNodeType` (`scripts/Environment/ResonanceGridData.cs:5`) | `Traversal = 2` appended after `Minor, Major` | **A4** | |
| `EnemyAbilityArchetype` (`scripts/Enemies/EnemyAbilityData.cs:11-29`) | `SiphonTether = 9` appended after `PersistentFieldAtTarget = 8` | **A7a** | |
| `GameManager.MatchMode` | `Stock = 0`, `TimeLimit = 1` made **explicit**; `Hybrid = 2` **retained in the enum as reserved** and marked `[Obsolete]`, **removed from both selectors** | **A1c** | Never reuse 2; legacy saved `Mode == 2` normalizes to timed Stock |
| New `CheckpointRole` enum (`Entry \| Middle \| PreBoss`) | new type | **A3** | |
| New `StoryAttemptStatus` enum | new type, seven members per F10 | **A3b** | |
| New `PrerequisiteMode` enum (`All \| Any`) | new type, default `All` | **A4** | |
| `OrbEffect` — add `TemporalAegis`; `ShieldRestore` retained but must stop touching the shatter lockout | append | **A1b** | F17 |

`StatusType.Suppression` must be **refused by the deterministic sim**: add a guard in
`FighterEntitySystems.ApplyStatus` (~line 445) so an authored `.tres` carrying it cannot enter
Fighter Mode, plus a test. The design is explicit: "Fighter Mode is untouched — no Suppression source
exists in the sim, and none may be added without a separate ruling."

### 2.9 Cross-agent decoupling via EventBus

**Agents that need HUD presentation never edit `StoryHUD.cs` / `StoryHUD.tscn` / `FighterHUD.cs`.**
They publish typed events through additive edits to `scripts/Core/EventBus.cs`. The HUD workstream
(**A8**) subscribes. The payload names and field names below are **fixed by this plan** so the two
sides can be written in parallel without seeing each other's code.

| Event | Payload | Publisher | Fields |
|---|---|---|---|
| `OnTimelineIntegrityChanged` | `IntegrityPayload` | A3 | `float Percent`, `IntegrityTier Tier`, `int LivingExtractors`, `float DrainPerSecond`, `bool Frozen` |
| `OnCollapseTremorChanged` | `TremorPayload` | A3 | `int Level` (0 = off, 1 = <20%, 2 = <10%) |
| `OnTimeFreezeStateChanged` | `TimeFreezePayload` | A2 | `TimeFreezeState State` (`Ready \| Active \| Cooldown`), `float SecondsRemaining` |
| `OnAbilitySlotLockChanged` | `AbilitySlotLockPayload` | A5 | `AbilitySlot Slot` (`Special1 \| Special2 \| Movement \| Ultimate`), `AbilitySlotLockState State` (`Dormant \| Suppressed \| Clear`) |
| `OnAnchorChargesChanged` | `AnchorChargesPayload` | A3b | `int Charges`, `int Max` |
| `OnDefySealChanged` | `DefySealPayload` | A1b | `int PlayerIndex`, `DefySealState State` (`Building \| Ready \| Spent \| Barred`) |
| `OnRallyEchoChanged` | `RallyEchoPayload` | A1b | **Check whether this already exists before adding.** The Rally band is currently read by polling `PlayerController.EchoPool` / `verb.EchoPool`; if no event exists, add `float PoolFraction`, `bool ReclaimFlash`, `int PlayerIndex`. If one exists, extend it additively and say so in §9. |

Enums referenced above (`IntegrityTier`, `TimeFreezeState`, `AbilitySlot`, `AbilitySlotLockState`,
`DefySealState`) are declared in `EventBus.cs` alongside their payloads by the publishing agent.
`EventBus.cs` is **additive-all**: every agent may append a payload class, an enum and an event
declaration; nobody edits or reorders an existing one. Merge conflicts in that file are resolved by
union.

`StatusEffectPayload` gains **no slot discriminator** — A8 derives the slot from
`StatusController.IsDamageStatus(payload.Type)` at the HUD, which is the cheapest correct answer and
touches no raiser. `OnStatusEffectCleared` also carries `Type`, so clears route the same way.

### 2.10 Dialogue mechanisms: schema owned by A5, content authored by A6

A5 ships the mechanisms; **A6 authors every line against this exact schema** and must not invent a
different one.

1. **Hero-conditional variants.** `DialogueSequenceData` gains
   `[Export] public string HeroConditionCharacterID = "";` (empty = the default sequence). A variant
   sequence uses the ID `<baseID>@<heroID>` — e.g. `level_01.entrance@leonardo`. A new
   `DialogueSetData.FindSequence(string baseID, string activeHeroID)` prefers the hero variant when
   one exists and falls back to the base sequence otherwise. Selection is from the **saved campaign
   hero ID vs. the level's authored central-legend ID** — never from a localized name.
   `DialogueManager.StartSequence(string dialogueID)` routes through `FindSequence`.
   **Variant IDs must be distinct strings** so the F10 seen/skip rules cannot substitute one for the
   other across slots (N03 calls this out explicitly).
2. **Captive-name tokens.** Line text supports `{CaptiveName1}` and `{CaptiveName2}`, resolved at
   render time from the manifest roster minus the active hero in a deterministic order:
   the authored priority is **Leonardo (spoken name "Da Vinci"), Cleopatra, Tesla** — filter out the
   active hero, then take the first two distinct. A5 ships the resolver as
   `FTT.Core.CampaignCaptiveRoster` with `IReadOnlyList<string> For(string heroID)` and
   `(string, string) NamedExamplesFor(string heroID)`, backed by
   `ContentManifest.ForCategory(ContentCategory.Character)` — **not a hardcoded array** (A9 mandate).
   Spoken names live in a new `captive_name_*` key family in `en.csv` (A6 owns those rows), distinct
   from the existing `character_*` display names because Leonardo's spoken name differs.
3. **Hold-to-skip for every sequence.** Drop the `save.IsCompleted` gate at
   `DialogueManager.cs:301-303` entirely; `HoldToSkipSeconds = 0.75f` is unchanged.
4. **First-viewing confirm.** If the sequence ID is **not** in `GlobalSaveData.SeenDialogueIDs`, the
   hold opens one confirmation through the shared `scripts/UI/ConfirmModal.cs` — "Skip this scene?
   It won't replay." — and skips on confirm. A sequence seen on **any** slot skips with no prompt.
5. **Idempotent effect resolution.** Split effect resolution out of `EndSequence()` into
   `ResolveDialogueEffects(string sequenceID)`, keyed by a gameplay-effect ID and guarded by the
   per-attempt applied-effect set, and call it from **both** the completion and the confirmed-skip
   paths. `LastViewedDialogueID` is demoted to a presentation cursor only.

### 2.11 `localization/en.csv` policy

`en.csv` is the single highest-contention file in this package. The policy is **append-all with
owned deletes**:

- Every workstream appends its new rows under a comment marker line
  `# Package 11 <WS-ID>` (e.g. `# Package 11 A2`). Appending under your own marker makes every merge
  a trivial union.
- **Deleting or rewriting an existing row is allowed only by the workstream that owns those rows:**
  - **A4** owns every `resonance_*` node row (162 node rows replaced wholesale).
  - **A6** owns every `dlg_*`, `speaker_*`, level-title, room and objective row.
  - **A2** owns `tutorial_step_manual_rewind` (line 606), `controls_action_rewind` (line 1084),
    `hud_rewind_cooldown` (line 1146).
  - **A8** owns `results_rating` (line 1021).
  - **A1c** owns `fighter_mode_hybrid` (line 277).
- **Only the orchestrator regenerates `localization/en.en.translation`**, once per wave, with
  `--headless --import`. Agents do **not** commit a regenerated translation binary — that guarantees
  a binary merge conflict.
- Because agents cannot regenerate the compiled resource, **every test that resolves a key through
  the compiled translation will fail in an agent's worktree until the orchestrator imports.** Agents
  mark those tests as **expected-to-fail-until-import** in their handoff, listing the exact test
  names. The three gates that behave this way are `ScriptTranslationKeyTests`,
  `SceneVisibleTextTests` and `CampaignLocalizationTests`; `UnusedTranslationKeyTests` also reads the
  compiled resource.
- `UnusedTranslationKeyTests` has `RecordedOrphanCeiling = 11` and **fails on any new orphan**. A
  workstream that retires a key must either delete the row in the same change or add it to the
  recorded-orphan roster in the same change. A4's 162-row replacement is the dangerous case: leaving
  the old rows in adds 162 orphans and fails hard.
- Duplicate-key detection lives in
  `EnemyRosterContentTests.EnglishTranslationTableHasNoDuplicateKeys`.

### 2.12 Test discipline

- **Never run GdUnit concurrently across checkouts** (CLAUDE.md failure signature 5). Every agent
  polls for a clear window before running tests:
  `Get-Process testhost,Godot* -ErrorAction SilentlyContinue` must return nothing. Drain **both**
  process names; orphaned Godot children fake signatures 1 and 2 in a single checkout.
- **Agents run filtered suites only** (`--filter "FullyQualifiedName~<Suite>"`). Only the Phase C
  closeout runs the full suite, three times.
- **Read the `Total:` count, never the exit code alone.** Baseline **1638**. Several workstreams
  rewrite tests in place and some delete more than they add, so a *dropping* total is expected in
  places — compare against the exact declared delta in each dossier, never against "bigger than
  before".
- **One `[TestSuite]` class per file** (failure signature 6). GdUnit4 silently discovers and then
  never runs a second `[TestSuite]` sharing a file with a `[RequireGodotRuntime]` suite; the run
  stays green and `Total:` moves by a plausible small delta instead of the real one.
- **Never `OverrideFailureMessage("")`** on a possibly-empty error list — it throws
  `ArgumentException` precisely on the passing path. Use the repo idiom
  `if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");`.

### 2.13 Native-crash hygiene (mandatory)

Violating any of these corrupts the whole suite and the crash never points at its cause.

- **Never call `Dispose()` on a Godot `Resource`** (or any `RefCounted`), in production or test code.
  `Free()` the node; leave its resources alone.
- **Load authored `.tres` data through `FTT.Core.AuthoredResources.Load<T>()`**, never
  `ResourceLoader.Load` / `GD.Load`. Scenes, textures, audio and anything pooled or streamed must
  **not** go through the cache.
- **Never author an empty Script-typed array** in a `.tres`/`.tscn`
  (`Prop = Array[ExtResource("N")]([])` where `N` is a `.cs` script). Omit the property and let the
  C# field default apply. `tests/ContentValidation/ScriptTypedEmptyArrayTests.cs` guards this. Every
  Resonance grid ends with `Nodes = Array[ExtResource("2")]([...])` — A4 must never author an empty
  one.
- **Whoever sets `SceneTree.Paused` releases it in `_ExitTree`**; tests restore it in `finally`. A
  leaked pause stops GdUnit's transport node and hangs the whole session (failure signature 4).
- **Physics in/out signal callbacks cannot mutate physics-space state.** Signal entry points that
  can cascade wrap their body in `using var scope = FTT.Core.PhysicsCallbackGuard.Enter();`;
  monitoring/shape/process-mode writes go through the `PhysicsSafeSetters` extensions
  (`SetMonitoringSafe` / `SetMonitorableSafe` / `SetShapeDisabledSafe` / `SetProcessModeSafe`).
  This matters most to A2, whose world-freeze service writes `ProcessMode` on collision-bearing
  subtrees.
- **Dispose engine-returned Godot collections deterministically:** bind the result, then
  `using var lifetime = result.AsDisposable();` (`FTT.Core.GodotCollectionExtensions`).
- On a large partial `Total:` with a negative exit code, read
  `<user data>/Godot/app_userdata/Fighters Through Time/logs/godot.log` **first**; never bisect by
  test class on that signature; never call a subset clean on fewer than five runs.

### 2.14 Story/Fighter isolation is unchanged and absolute

Nothing in `scripts/FighterSim/` may consume Story progression: no Resonance grid values, no
`StoryAbilityPerks`, no `StoryDifficultyTuning`, no `EnemyStaggerRules`, no `TimeFreezeController`,
no `Suppression`. `CharacterFactory.CreateCharacter(..., applyStoryProgression: false)` remains the
isolation seam and the Mirror Paradox clone stays on the full normalized kit except where F20
explicitly authorizes Hard-only perk mirroring (A7b).

Conversely, the Legacy Unlock Schedule, Suppression's ability lock, and the Nexus Ultimate
authorization are **Story-only gates** — Fighter Mode, CPU matches, the hub Holodeck and the
Calibration Drills always run full normalized kits.

---

## 3. Wave 1 — parallel worktrees (merge order as listed)

Nine agents, each in its own git worktree off the commit that carries this plan. The orchestrator
merges in the order **A1 → A2 → A3 → A4 → A5 → A6 → A8 → A9 → A12**, building and running the
merging agent's filtered suites after each merge, then one full-suite run after A12. Wave 2 does not
start until Wave 1 is fully merged and green.

### A1 — Status architecture, Suppression, Conductive

**Goal.** Give the codebase one status data contract, replace newest-wins with stronger-wins at all
four sites, add `StatusType.Suppression` with its Story ability-lock gate, and split Tesla's
Conductive mark out of Static Charge — fixing the F07 double-stun defect in the process.

**Design references.** `design-godot.md` §4 Character Data Architecture (master ~1536, 1642,
1798–1842), F07 (master ~1392, 1839), Suppression (master 1812, 1842, 2928–2931), Tesla rider
(master 1033), status visual feedback F24 (`docs/design-contracts/HUD_CONTRACT.md` "Status slots").
Recon: **E §2.1–2.6**, **D §2.2 (D-24), §12.2 (D-25), §15.1 (D-31), §15.2 (D-29)**, **G §1.3(c)**.

**Current state.** The *semantics* of two slots exist everywhere but the *contract* exists nowhere.
`scripts/Combat/StatusController.cs` has private `_control` / `_damage` slots and a
`StatusController.IsDamageStatus(type)` static (Venom, RadiantBurn → damage; everything else →
control). That routing logic is re-implemented independently **three more times**:
`EnemyController.ApplyStatusEffect` (lines 1051–1102), `BossController.ApplyStatusEffect`
(620–670) and `FighterEntitySystems.ApplyStatus` (445–460). There is no `StatusSlots`, no
`StatusSlot` enum, no `StatusRouting`, and no `IStatusEffectTarget` (`grep -r IStatusEffectTarget`
returns nothing). All four sites are **newest-always-wins**:
`StatusController.ApplyStatus` calls `ClearSlot(slot, raiseEvents:false)` then assigns;
`EnemyController` / `BossController` call `ClearDamageStatusSlot()` / `ClearControlStatusSlot()` then
assign; `FighterEntitySystems.ApplyStatus` assigns unconditionally.
`FTT.Core.StatusType` (`scripts/Core/EventBus.cs:260`) has exactly
`None, TimeDilation, Venom, StaticCharge, RadiantBurn, Root`. No `Suppression`, no ability lock
anywhere in `BaseSpecial` / `PlayerController` ability input. `grep -r Conductive` hits only
`scripts/Environment/ConductiveCoil.cs`, an unrelated Level 3 beam-routing puzzle prop.
`BasicComboRules.StringProfiles["tesla"]` authors only
`FinisherStatusType = StatusType.StaticCharge, FinisherStatusFrames = 24`, and
`TeslaAbilities`' Lorentz Pulse gates its chain-lightning bonus on `TargetHasStaticCharge(hurtbox)`
— using the action-locking status as the chain marker, exactly what F07 splits apart.

**The F07 double-stun defect, precisely.** `EnemyController.TakeHit` (~line 1380) calls
`ApplyStun(hit.HitstunDuration, minimumSeconds, fromSpecial: AttackClass == Special)`, then (~1394)
calls `ApplyStatusEffect(...)`, which for `StaticCharge` calls **`ApplyStun(duration)` again** with
`minimumSeconds = 0` and `fromSpecial: false`. Consequences: the elite stagger budget is charged
**twice** (Tesla's finisher = 0.4 s hitstun + 0.4 s Static Charge = **0.8 s of budget**, precisely
the case the design forbids — "the finisher must not turn 0.4 seconds into 0.8"); `_stunTimer = stun`
**overwrites** rather than taking the max, so a resistance-scaled Static Charge can *shorten* a
floored basic-string stun; a Static Charge applied by a Special is counted as non-special so it
never diminishes and never refreshes the 4.0 s diminish window; and `ApplyStatusEffect` occupies the
control slot even when `ApplyStun` was refused by armor.

**Exact change list.**

1. **New `scripts/Combat/StatusSlots.cs`** (no `[TestSuite]` in it) declaring:
   - `public enum StatusSlot { Damage, Control }`
   - `public struct StatusEffectData { StatusType Type; float RemainingSeconds; float Intensity; }`
   - `public struct StatusSlots { StatusEffectData Damage; StatusEffectData Control; }`
   - `public static class StatusRouting` with `StatusSlot SlotOf(StatusType)` as a **compile-time
     table** — "a wrong slot assignment is a compile-time table entry, not a runtime bug". Damage
     slot: `Venom`, `RadiantBurn`. Control slot: `TimeDilation`, `StaticCharge`, `Root`,
     `Suppression`.
   - `public static bool ShouldReplace(StatusType current, float currentIntensity,
     float currentRemaining, StatusType incoming, float incomingIntensity, float incomingDuration)`
     — the stronger-wins rule.
   - `public interface IStatusEffectTarget : IDamageable { StatusSlots ActiveStatuses { get; set; }
     void ApplyStatusEffect(StatusType status, float duration, float intensity);
     void ClearStatusEffect(StatusSlot slot); void ClearAllStatusEffects(); Node2D TargetNode { get; } }`
     — confirm `IDamageable`'s existing shape is compatible before wiring the inheritance.
   Keep `StatusController.IsDamageStatus` as a **forwarder** to `StatusRouting.SlotOf` so the sim's
   hot-path signature does not change and A8 can keep deriving the HUD slot from it.
2. **Stronger-wins (replaces newest-overwrites) at all four sites.** Within a slot:
   - different type → always replaces;
   - same type → replaces **only if** `newIntensity × newDuration >= currentIntensity × currentRemaining`;
     it brings its own duration; nothing is added or refreshed;
   - a weaker same-type application **does nothing** — no replace, no refresh;
   - the other slot is **never touched**.
   In the sim the comparison must be **fixed point and rollback-safe**: use the existing
   `StatusFrames` / `DamageStatusFrames` (int) × the milli-intensity int as an `int64` product —
   exactly reproducible, no `FP64` division.
3. **Implement `IStatusEffectTarget`** on `PlayerController`, `EnemyController` and `BossController`,
   re-expressing each over `StatusSlots` / `StatusRouting`. `BossController` currently has **no
   status slots at all**; give it the two slots (the design says "the `StatusController` attaches to
   enemies and bosses too"). Add `ClearAllStatusEffects()` calls at death/respawn and Restart Level.
4. **Append `StatusType.Suppression` (= 6)** to `scripts/Core/EventBus.cs:260`. **A1 is the only
   workstream that may touch this enum.**
5. **`SuppressionStrategy`** in `StatusController` + a `StrategyFactory` entry. Control slot,
   **Story-only source**, **2.0 s at standard intensity**.
6. **The Suppression ability-lock gate** in `PlayerController`: a
   `public bool IsAbilityCastSuppressed` read consulted at the **four** cast sites (Special 1 press
   ~line 1185 / ~2610, Special 2, movement ability, ultimate cast). Locked casts are **refused
   without consuming cooldown or meter**, with a dull null-tone SFX hook.
   **Untouched by Suppression, explicitly:** basics, block, grab, Rally, DI, landing tech,
   Defy History, death rewinds and **Time Freeze** (the universal escape tool carve-out). Cooldowns
   **keep ticking** while suppressed.
7. **Sim refusal guard:** in `FighterEntitySystems.ApplyStatus` (~line 445), refuse
   `StatusType.Suppression` outright so an authored resource cannot introduce it into the
   deterministic sim. Pin with a test.
8. **Presentation hook:** `GlowPresentationController` gains an **aura-smother** channel — the
   persistent gold resonance aura desaturates to cold grey and the outline shader drops to base
   priority while suppressed. A1 ships the controller-side channel and the state read; **A8** owns
   the HUD cross-out on the three cooldown slots and the ultimate ring, driven by
   `OnAbilitySlotLockChanged` (published by **A5**, state `Suppressed`). A1 publishes nothing to the
   HUD directly.
9. **Conductive mark (F07).** A **non-slot, caster-owned mark**, not a status:
   - Story: `ConductiveFramesRemaining` + `ConductiveSourcePlayerID` (and per-execution
     chain-consumed state) on `PlayerController` / `EnemyController` / `BossController`.
   - Sim: new **Klotho component 318 `FighterConductiveComponent`** (§2.7) — remaining frames,
     source player ID, per-execution chain-consumed flag. Snapshot and hash state with **stable owner
     ordering** and **no wall-clock timer**; it must store consumed/processed attack state so a
     restored multi-hit Pulse cannot duplicate chains.
   - `BasicStringProfile` gains `FinisherMarkType` / `FinisherMarkFrames`. Tesla's finisher now
     applies **two** things: `Static Charge` (interrupt, **0.4 s / 24 frames**, unchanged) **and** a
     separate **Conductive mark at 90 frames (1.5 s) baseline**, extended to **150 frames (2.5 s)**
     only by the Story `tesla_conductive_hold` grid node (A4 authors that node; A1 reads the
     Resonance flag **at the application site**, never by editing the cross-mode `BasicComboRules`
     constant).
   - Linked **coil fence** marks stay at **90 frames** and are **not** extended by the node.
   - Conductive **causes no action lock, contributes zero stagger budget**, does not occupy a status
     slot, and may remain while the target acts in armor.
   - Re-point `TeslaAbilities`' Lorentz chain eligibility from `HasStatusEffect(StaticCharge)` to
     the Conductive mark. Static Charge stays a pure interrupt.
   - Tesla's death clears **his** Conductive marks even though his coils survive (T01a).
   - Presentation: a separate **non-pulsing circuit glyph** near the target's health display,
     owner-identified in Fighter; **never** a status HUD slot, never resembling the stun outline.
     A1 exposes the state; A8 draws it.
10. **F07 stagger accounting fix.** Make the hit **one** stun event: in `EnemyController.TakeHit`
    compute `effectiveStun = max(postResistanceHitstun, postResistanceStaticCharge)` and call
    `ApplyStun` **once** with the correct `fromSpecial`. `ApplyStatusEffect(StaticCharge)` then sets
    only the visual/slot state (route it through a `fromHitPayload` flag so it does not re-enter
    `ApplyStun`). **Refuse** the status application while `IsStaggerArmored`; **clear the lock** on
    entering armored recovery; grant getup armor **once** for the combined stun. Cover the linked
    coil-fence source (`TeslaCoilNode` fence ticks) the same way.
11. **`StatusEffectPayload` gains no slot field** (§2.9). Leave `StatusController.ClearSlot`'s
    "re-announce the survivor" hack in place — A8 deletes it once two pips exist. Note the handoff
    in §9.

**Files owned (exclusive).** `scripts/Combat/StatusSlots.cs` (new), `scripts/Combat/StatusController.cs`,
`scripts/Enemies/EnemyController.cs`, `scripts/Enemies/BossController.cs`,
`scripts/Combat/GlowPresentationController.cs`,
`scripts/FighterSim/FighterSimulationComponents.cs` (**ID 318 only**),
`scripts/Combat/BasicComboRules.cs` (**`FinisherMarkType`/`FinisherMarkFrames` additions only**),
`scripts/Characters/Abilities/TeslaAbilities.cs`, `scripts/Environment/TeslaCoilNode.cs`.

**Shared files touched additively.** `scripts/Core/EventBus.cs` (append `Suppression`),
`scripts/Characters/PlayerController.cs` (**region: status slots + the four cast-site gates +
Conductive fields only** — see §5), `scripts/FighterSim/FighterEntitySystems.cs`
(`ApplyStatus` stronger-wins + the Suppression refusal guard only), `localization/en.csv`
(`status_suppression`, the Conductive glyph tooltip, under `# Package 11 A1`).

**Tests.**
- Rewrite in place: `tests/unit/StatusControllerTests.cs` — newest-wins cases become stronger-wins
  (weaker same-type is a no-op, equal strength replaces, different type always replaces, the other
  slot is untouched). **±0.**
- Extend `tests/unit/EnemyControllerTests.cs` **+4** — the finisher charges **0.4 s** of elite
  budget not 0.8; `_stunTimer` takes the **max** rather than overwriting; a special-sourced Static
  Charge diminishes and refreshes the 4.0 s window; armored recovery **rejects** reapplication.
- Extend `tests/unit/BossControllerTests.cs` **+2** — bosses now carry status slots; boss flinch
  immunity is unchanged (`HitPayload.HitstunDuration` is still ignored).
- New `tests/unit/SuppressionTests.cs` **+6** — the four cast sites are refused without consuming
  cooldown or meter; cooldowns keep ticking; basics/block/grab/Rally/DI/tech/Defy/rewind/Time Freeze
  are unaffected; the sim refuses `Suppression`; a blocked source suppresses nothing.
- New `tests/unit/ConductiveMarkTests.cs` **+6** — 90-frame baseline, 150 with the node flag, fence
  marks at 90 un-extended, zero stagger budget, no action lock, Tesla's death clears his marks,
  Lorentz chains gate on Conductive not Static Charge.
- New `tests/Determinism/FighterStatusSlotTests.cs` **+4** — fixed-point stronger-wins in the sim,
  component 318 round-trips through snapshot/hash, no chain duplication across a rollback,
  Suppression refused.
- Extend `tests/ContentValidation/TeslaContentTests.cs` **+1**.

**Expected count delta: +23** (1638 → 1661 at A1's merge).

**Acceptance criteria.**
- `StatusRouting.SlotOf` is the only routing implementation; the three controllers and the sim all
  call it (or the `IsDamageStatus` forwarder).
- Applying a weaker same-type status changes nothing observable, at all four sites.
- Tesla's finisher consumes exactly 0.4 s of an elite's stagger budget.
- A suppressed Story player can still block, grab, Rally, tech, DI, Defy and Time Freeze.
- No path in `scripts/FighterSim/` can apply `Suppression`.
- `dotnet build` clean; the seven suites above pass filtered.

---

### A2 — Time Freeze (F03) and the retirement of Manual Rewind / Stasis Echo

**Goal.** Delete the V7.2/V7.3 manual rewind verb, the Stasis Anchor, the Stasis Echo and the 12 s
manual cooldown outright, and ship the Story-only Time Freeze escape ability in their place.

**Design references.** `design-godot.md` Time Freeze spec (master 2031–2052), the input table
(master 2189–2200), the difficulty row (master 2225–2240), the Story HUD rows (master 3256, 3396),
Legacy Unlock at Level 0 (master 296, 308–318). Contracts:
`docs/design-contracts/TEMPORAL_STATE_CONTRACT.md` ("Time Freeze, Story only" row),
`CHECKPOINT_RECOVERY.md` line 31 ("Time spent travelling during Time Freeze counts as live time"),
`CAMPAIGN_VALIDATION.md` line 102 (no puzzle progress at an adjacent-room boundary),
`COMFORT_SETTINGS.md` lines 61/77/89 (no catch-up, no audio backlog), `HUD_CONTRACT.md` line 16
(radials follow the frozen authoritative clock). Recon: **C, in full** — it is this workstream's
complete specification.

**The exact contract (copy these numbers).**

| Aspect | Requirement |
|---|---|
| Mode scope | **Story Mode, single player, only.** Never Fighter, CPU matches or Fighter training. Available from Level 0 on all three difficulties, including at **zero** death-rewind charges |
| Input | New dedicated action **`gameplay_time_freeze`**, single **press** — no hold, no scrub, no commit phase. Defaults **`R`** keyboard (physical keycode 82), **Back / Select** gamepad (`button_index 4`) |
| Eligibility | A living, controllable player. Refused during hitstun, daze, grabs, attack/ability execution, death, or scripted presentations. **Suppression does NOT lock it** |
| Duration | **5.000 s = 300 physics ticks.** The duration clock pauses in the pause menu. Ends early and cleanly on death, scripted scene transition or level exit |
| Cost | **No charges, no meter, no dust.** Never spends or refunds a death-rewind charge |
| Cooldown | **45 s, identical on all difficulties.** Starts when the freeze **ENDS**, including an early end. Advances only during live Story play; pauses in menus, dialogue and death-rewind presentations. **Cannot** be refreshed by checkpoint activation, death rewind, Anchor Snap or reloading the same level session. Fresh level entry or full Restart Level starts **Ready**. Remaining cooldown is **saved** |
| What freezes | Everything except the player, camera and UI: enemies **including bosses**, **both sides'** projectiles, hazards, moving platforms, world mechanisms. Adjacent-room simulation suspended; a newly revealed/entered room is frozen **before** its actors can act. Ordinary room crossings preserve remaining duration |
| Player may | **Run, jump, and the universal evasive roll.** Disabled for the whole freeze: basic attacks, grabs, specials, ultimate, **Echo Step**, and the character Movement Ability. Attempted attacks are **discarded, never queued** |
| Escape-only guarantee | Enemies and bosses **invulnerable throughout**, including against already-live player projectiles, constructs, zones and DoT. No damage, stagger, **Rally reclaim**, meter generation, healing, pickups, **checkpoint activation**, **puzzle interaction/progress**, or objective credit |
| Suspended timers | Block-charge regen, other ability cooldowns, and status timers are suspended. Movement and the 5 s effect clock continue |
| Thaw | All actors resume **preserved positions, velocities, attack phases and remaining timers**. **No catch-up ticks, no projectile clearing, no accumulated damage, no delayed input burst, no audio backlog** |
| Physics | Solid geometry stays solid; frozen hazards/projectiles cannot damage the player; **pits still apply their normal fall rules** (and under F16 Story pits are lethal — A3 owns that) |
| Integrity clock | **Continues draining at the current normalized rate.** Never restore Integrity on activation, expiry or load. Existing pauses (menus, dialogue, death-rewind presentation, pre-boss lock) still apply |
| Save / reload | Activation commits a **45 s reload cooldown** for any reload during that activation; background autosaves keep that conservative value while the live freeze continues. **Explicit Save or exit during an active freeze ends the freeze and stores 45 s.** After thaw, snapshots store the true remaining cooldown. **Reload never resumes or renews the effect** |
| Never a gate | Time Freeze may **never** be a required progression gate |

**Current state.** `scripts/Environment/ChronalRewindManager.cs` (636 lines) owns **both** the death
rewind and the whole manual verb: `ManualHoldSeconds = 0.5f`, `ScrubFramesPerTick = 4`,
`StasisEchoSeconds = 10f`, `ManualRewindCooldownSeconds = 12f`, `_manualCooldownRemaining`,
`ManualRewindCooldownRemaining`, `IsManualRewindOffCooldown`, `StartManualRewindCooldown()`,
`ScriptedFreeRewind`, `IsManualRewindFree` (Easy free), `TrackManualRewindHold()`,
`CanBeginManualRewind()`, `BeginScrub()`, `AdvanceScrub()`, `ScrubPreviewPosition`, `CancelScrub()`,
`CommitScrub()`, `_manualCommit`, `_spawnEchoOnComplete`, `_pendingEchoOrigin`, `IsScrubbing`,
`ScrubDepthFrames`, and the `IRewindScrubbable` fan-out. It also owns `FreezeWorldForRewind()` /
`ResumeWorldAfterRewind()` / `ClearEnemyProjectiles()` / `FrozenSimulationGroups`, the collapse beat
and the checkpoint refresh — all of which **stay**.
`scripts/Environment/StasisEcho.cs` (135 lines) is an `AnimatableBody2D` with
`BodyLayer = Environment | PersistentObject`, a static `Current` singleton, 10 s life, one-way
platform collision and a one-projectile absorb `Area2D`. `PressurePlate.cs:53-55` has a
`StasisEcho => PlayerWeight` switch arm. `SearchlightZone.cs:93,112,125` occludes its beam only when
the hit collider `is StasisEcho`. **No authored level requires the Echo** — Level 6's scale puzzle is
solved by the player standing on a pan (`Level06Controller` docs L322–334), and Levels 4/9
searchlights are timing/route puzzles with `SearchlightZone.Enabled` kill switches. **Deletion only;
no puzzle re-authoring needed.**
`project.godot` L144–149 defines `gameplay_rewind` with **exactly the Time Freeze defaults already**
(physical keycode 82, joypad button_index 4) — only the action *name* is wrong.
`StoryHUD.cs` L56–58, 337, 341–370 builds a `RewindCooldownPip` Label appended next to the rewind
counter, bound to `ManualRewindCooldownRemaining`, keyed `hud_rewind_cooldown` — exactly the pattern
the design now forbids.

**Exact change list.**

1. **New `scripts/Environment/TimeFreezeController.cs`** (`FTT.Environment`) with this surface:
   ```
   public const float  FreezeSeconds    = 5f;      // 300 ticks
   public const int    FreezeFrames     = 300;
   public const float  CooldownSeconds  = 45f;     // all difficulties
   public const string ControllerGroup  = "time_freeze_controller";
   public bool  IsFrozen { get; }
   public float FreezeSecondsRemaining { get; }
   public float CooldownRemaining { get; }
   public bool  IsReady { get; }                   // cooldown 0 && !IsFrozen
   public bool  DrillFreeFreeze { get; set; }      // Level 0 drill: ready on every retry
   public bool  TryBeginTimeFreeze();              // THE PUBLIC ENTRY POINT — A5 calls this
   internal void EndFreeze(bool early);            // arms the cooldown
   public static bool CanActivate(CharacterState state, bool isFrozen, float cooldownRemaining, bool alive);
   ```
   **`TryBeginTimeFreeze()` is a contract with A5** — the Level 0 drill step calls exactly that
   name. Implement it with that signature and do not rename it.
   Tick in `_PhysicsProcess` on `ProcessMode.Inherit` so `SceneTree.Paused` freezes both the duration
   and cooldown clocks for free. **Do not** set `ProcessMode.Always`. **Never write
   `SceneTree.Paused`** (failure signature 4).
   Register in `StorySceneBootstrapper.Attach(...)` under the same `includeRewind` flag (level-only,
   never the hub) — `scripts/Environment/StorySceneBootstrapper.cs` L66–72 plus a
   `TimeFreezeController` field on `StoryServices` (L13–16).
   Eligibility mirrors the existing `CanBeginManualRewind()` state list **minus** the charge/history
   checks, **plus** an alive check, **plus** exclusion during `ChronalRewindManager.IsRewinding` /
   `IsCollapseBeatActive` / active dialogue. **Suppression must not be consulted.**
2. **World-freeze service.** New `IStoryTimeFreezable { void SetTimeFrozen(bool frozen); }` in
   `scripts/Environment/StoryRewindPolicy.cs`, plus a `TimeFreezeScope` sweep, with a
   **`ProcessMode.Disabled` fallback** applied to a room's `Actors`/`Hazards`/`Encounter` container
   subtrees for anything without an explicit hook. **Every `ProcessMode` write goes through
   `PhysicsSafeSetters.SetProcessModeSafe`** (§2.13). Explicitly implement `IStoryTimeFreezable` —
   not `ProcessMode` — for everything that must keep a live collision shape but stop simulating:
   `PathMovingPlatform` (freeze in place; it still **reverses** during a death rewind),
   `RisingWaterZone`, `SearchlightZone`, `StageHazard`, `StoryCyclicHazard`, `CrumblingPlatform`,
   `TrapdoorPlatform`, `RotatingGear`, `RotatingPlatform`, `PendulumAnchor`, `ForcefieldBarrier`,
   `ShieldGeneratorTower`, `EscapeSequenceController`, `ChronalRiftZone`, `GravityFieldZone`,
   `MovementDampenerZone`.
   Reuse `FrozenSimulationGroups` membership for enemies/constructs/extractors but with a
   **Time-Freeze-specific variant** that (i) **omits `chronal_extractor`** — Integrity must keep
   draining — and (ii) **does not** call `ClearEnemyProjectiles()`; instead freeze `enemy_projectile`
   and player-projectile pool members **in place**.
3. **Enemy invulnerability** at the shared hit-resolution chokepoint, not per enemy: a single
   `TimeFreezeController.IsFrozen` gate that **discards** player-sourced hits — no damage, no
   stagger, no Rally echo, no meter. Gate it at `Hitbox.OnAreaEntered` / `DamageCalculator` /
   `EnemyController` intake. That handler runs inside a physics flush behind `PhysicsCallbackGuard`,
   so **the freeze read must be side-effect-free there**.
4. **Player input mask.** `PlayerController` gains a `TimeFrozen` flag that (i) allows
   Move/Jump/Down/Roll, (ii) rejects `BasicAttack` / `Special1` / `Special2` / `Ultimate` /
   `MovementAbility` / Grab / Echo Step **without buffering**, (iii) suspends block-charge regen,
   ability cooldown ticks and status timers. **Do not reuse `SetRewindSuspended`** — that suspends
   the player, the opposite of what is needed.
5. **Room scoping.** Minimum viable and conformant: freeze **level-wide** on activation, and on a
   room crossing during an active freeze freeze the destination's encounter root **before**
   `RoomTransitionTrigger.ActivateRoom()` runs its `onEntered` handler. Level-wide freezing trivially
   satisfies "suspend adjacent rooms"; the design's wording is a floor, not a cap.
6. **Input rename + migration.**
   - `project.godot`: rename the InputMap action `gameplay_rewind` → `gameplay_time_freeze`,
     **keeping both events unchanged**.
   - `scripts/Core/InputManager.cs`: replace `Actions.Rewind` with
     `Actions.TimeFreeze = "gameplay_time_freeze"`; update `RemappableActions` (L47) and
     `ActionLabelKey` (L68) → `controls_action_time_freeze`. Keep the "no `GameplayButtons` bit"
     comment — that is the Story-only guarantee.
   - `scripts/Core/InputBindings.cs` / the global payload: a migration that renames the
     `gameplay_rewind` key to `gameplay_time_freeze` inside `InputBindingSet.Actions` **without
     overriding an explicit newer Time Freeze bind**. Per `COMFORT_SETTINGS.md` line 128, do **not**
     assume v4 already covers it — inspect the actual stored format. A2 writes the migration
     *function*; the Phase C closeout wires it into the single v5→v6 step.
7. **Deletions.**
   - `ChronalRewindManager.cs`: delete the entire "Manual rewind" region (every symbol listed under
     *Current state*), the `_spawnEchoOnComplete` branch in `CompleteRewind` (L462–470), the
     `_manualCommit` "restores no HP" branch, and the `_manualCooldownRemaining` tick in
     `_PhysicsProcess` (L46–78). Delete `ScriptedFreeRewind` from the rewind manager — its only
     remaining purpose was the tutorial cooldown exemption, which now belongs to
     `TimeFreezeController.DrillFreeFreeze`. **Keep** the death-rewind path, the collapse beat, the
     checkpoint refresh, the `RefundRewind()` path used by the tutorial's scripted death demo, and
     the world-freeze helpers (refactor them into a shared service both mechanics call).
   - **Delete `scripts/Environment/StasisEcho.cs` and its `.uid`.**
   - `PressurePlate.cs`: remove the `StasisEcho => PlayerWeight` arm (L53–55) and its comment.
   - `SearchlightZone.cs`: remove the Echo occlusion path (L93, 112, 125). **Ruling: keep the public
     `IsBeamOccludedForPlayer` method and have it return false** — no authored level requires
     occlusion to progress, and keeping the method avoids touching Level 4/9 call sites. Record the
     decision in §9.
   - `scripts/Environment/IInteractable.cs`: `IRewindScrubbable` survives **only** for the death
     rewind's platform reversal. Delete `CancelRewindScrub` — a scrub can no longer be cancelled.
   - `PathMovingPlatform.cs`: drop `CancelRewindScrub`; add `IStoryTimeFreezable`.
   - `StoryHUD.cs`: delete `_rewindCooldownPip`, `RefreshRewindCooldownPip()` and the
     `_rewindManager` group lookup. **This deletion must land with the indicator** — A8 adds the new
     `TimeFreezeIndicator` node. A2 deletes the pip and publishes
     `EventBus.OnTimeFreezeStateChanged` (§2.9); A2 does **not** author the new HUD node.
8. **Tutorial hooks.** `TutorialCalibration.cs`: rename `TutorialCalibrationStep.UseManualRewind` →
   `UseTimeFreeze`, `RegisterManualRewindComplete` → `RegisterTimeFreezeComplete`,
   `SkipManualRewindLesson` → `SkipTimeFreezeLesson`. Keep `UseRewind` (the scripted death-rewind
   demo) ahead of it. **A5 owns `Level00Controller.cs`; A2 edits only the manual-rewind step** —
   specifically, A2 replaces the `ScriptedFreeRewind` handoff at L251/258 with a
   `TimeFreezeController.DrillFreeFreeze` handoff and leaves the drill's geometry, objective copy and
   step ordering to A5.
9. **Persistence.** `StoryManager`: a persisted per-attempt `TimeFreezeCooldownRemaining` alongside
   the existing attempt registries (`WriteAttemptStateToSave` / the `BeginLevelRun` restore path,
   L243–272 / L500–535). **Fresh entry and Restart Level → 0 (Ready); resume → restore the saved
   value.** Checkpoint activation, death rewind and Collapse/Snap leave it untouched.
   `StorySaveData`: add `public float TimeFreezeCooldownSeconds;` with a field initializer (§2.6).
   While a freeze is live, **write 45** — never the live remainder; on explicit Save/exit, **end the
   freeze and write 45**.
10. **Localization** under `# Package 11 A2`: **add** `controls_action_time_freeze`,
    `hud_time_freeze_ready`, `hud_time_freeze_active` (`{0}`), `hud_time_freeze_cooldown` (`{0}`),
    `hud_time_freeze_thaw`, `tutorial_step_time_freeze`. **Delete (A2 owns these rows):**
    `hud_rewind_cooldown` (1146), `tutorial_step_manual_rewind` (606), `controls_action_rewind`
    (1084). Also fix the long-stale `difficulty_easy_description` (line 330), which still claims Easy
    restores **full HP** — it has been 70% since V7.2.
11. **`CharacterState.Thrown` is NOT A2's** — it belongs to A1c (§2.8). A2 must not append it.

**Files owned (exclusive).** `scripts/Environment/TimeFreezeController.cs` (new),
`scripts/Environment/ChronalRewindManager.cs`, `scripts/Environment/StasisEcho.cs` (deleted),
`scripts/Environment/StoryRewindPolicy.cs`, `scripts/Environment/PressurePlate.cs`,
`scripts/Environment/SearchlightZone.cs`, `scripts/Environment/PathMovingPlatform.cs`,
`scripts/Environment/IInteractable.cs`, `scripts/Environment/StorySceneBootstrapper.cs`,
`scripts/Environment/TutorialCalibration.cs`, `scripts/Core/InputManager.cs`,
`scripts/Core/InputBindings.cs`, and the ~16 environment classes receiving `IStoryTimeFreezable`.

**Shared files touched additively.** `project.godot` (the one action rename),
`scripts/Core/StoryManager.cs` (**region: the Time Freeze cooldown field + its three lifecycle
hooks only**), `scripts/Core/SaveManager.cs` (one additive field),
`scripts/Characters/PlayerController.cs` (**region: the `TimeFrozen` input mask only**),
`scripts/Core/EventBus.cs` (`OnTimeFreezeStateChanged` + `TimeFreezePayload` + `TimeFreezeState`),
`scripts/UI/StoryHUD.cs` (**deletion of the pip only** — A8 owns everything else in that file),
`scripts/Environment/Level00Controller.cs` (**the manual-rewind step only**),
`localization/en.csv`.

**Tests.**
- **Delete:** `tests/unit/ManualRewindScrubTests.cs` (**−4**). **Salvage first:** case 2
  (`ThePathPlatformScrubsAlongItsOwnRecordedPath…`) also pins the death-rewind platform reversal —
  move that assertion into `ChronalRewindTests` before deleting the file.
- **Delete:** `tests/unit/StasisEchoPhysicsTests.cs` (**−3**).
- **Delete:** `tests/unit/CollisionLayerTests.cs::TheStasisEchoBodyLayerReachesTheAuthoredPressurePlateMask`
  (**−1**).
- **Rewrite in place (±0):** `TutorialCalibrationTests::TheDesignedStepOrderRunsAttackRallyBlockSpecialUltimateRewindManual`
  → `…RewindTimeFreeze`; `TutorialCalibrationTests::TheRewindStepsCompleteInSequenceWithNeverStrandFallbacks`
  → death-rewind demo then Time Freeze drill; `ChronalRewindTests::DifficultyRulesMatchDesign` keeps
  5/3/1 and 70/50/30 and **adds the pin** that Time Freeze is 5 s / 45 s on every difficulty.
  *(A5 rewrites the step **enum order** in the same two suites — A2 lands first, A5 extends.)*
- **New `tests/unit/TimeFreezeTests.cs` (+18)** — one `[TestSuite]` per file:
  1. constants pin (5 s / 300 frames / 45 s, identical across all three difficulties);
  2. cooldown arms at **thaw**, not activation, including an early end;
  3. cooldown ticks only in live play; a paused tree and an active freeze both stop it;
  4. checkpoint activation / death rewind / Collapse / reload never refresh it;
  5. fresh entry and Restart Level start Ready; a mid-level resume restores the saved value;
  6. activation costs **no** rewind charge and is legal at `RemainingRewinds == 0`;
  7. eligibility matrix — refused in `Stunned`/`Dazed`/`Dead`/`Respawning`/`Attacking`/
     `UsingSpecial`/`UsingUltimate`/`UsingMovementAbility`/`Grabbing`; **allowed under Suppression**;
  8. during freeze Move/Jump/Roll accepted; attack/grab/special/ultimate/Echo Step/movement-ability
     **discarded, not queued** — verify no burst on thaw;
  9. during freeze enemies and bosses take zero damage from a live player projectile/zone/DoT; no
     Rally echo, no meter, no stagger;
  10. during freeze no pickup collection, no checkpoint activation, no puzzle condition change;
  11. suspended timers — block-charge regen, ability cooldowns and status durations frozen; the
      effect clock and movement continue;
  12. thaw preserves enemy position/velocity/attack phase with **no catch-up tick** and no
      projectile clear;
  13. `PathMovingPlatform` freezes in place under Time Freeze and still reverses under death rewind;
  14. **Integrity keeps draining** across the whole 5 s;
  15. save behaviour — activation stores 45 s; an explicit Save during a freeze ends it and stores
      45 s; reload never resumes;
  16. Story-only guard — no Fighter path reaches the controller; `gameplay_time_freeze` carries no
      `GameplayButtons` bit;
  17. guard — no authored level gates progression on Time Freeze (sweep
      `scripts/Environment/Level*Controller.cs` for controller references in puzzle/gate paths);
  18. the controller never writes `SceneTree.Paused`.
- **New `tests/unit/TimeFreezeBindingMigrationTests.cs` (+2)** — a customized `gameplay_rewind`
  override migrates onto `gameplay_time_freeze`; an explicit newer Time Freeze bind is not
  overwritten.

**Expected count delta: −8 removed, +20 added = +12** (1661 → 1673 at A2's merge).

**Acceptance criteria.**
- `grep -r "StasisEcho\|ManualRewind\|ScrubPreviewPosition\|BeginScrub"` over `scripts/` and
  `tests/` returns nothing.
- A 5 s freeze at zero rewind charges works on all three difficulties, including on a level resumed
  mid-run.
- Integrity visibly continues to fall across a freeze (test 14 is the pin).
- Nothing in `scripts/FighterSim/` references `TimeFreezeController`.
- Headless import check passes after the `en.csv` and `project.godot` edits; the orchestrator's
  `--import` closes the compiled-translation gap.

---
### A3 — Timeline Integrity as the level timer, Collapse Tremor, checkpoint roles, lethal pits

**Goal.** Replace the V7.1/V7.3 per-Extractor siphon tally with the F01 normalized level timer,
collapse at zero, add the Collapse Tremor, convert checkpoints to authored roles with the PreBoss
freeze, implement F11 recovery minima and F16 lethal pits, retire the abnormal-exit fee, and author
Florence's three budgeted Extractors.

**Design references.** `design-godot.md` §3 Timeline Integrity (master 225–522), F01, F11, F12, F16,
ruling 2.A (Chronal Rating retired), ruling 2.B (crashes are free). Contracts:
`docs/design-contracts/CAMPAIGN_VALIDATION.md` (V01a par authoring, E01 drain feedback),
`CHECKPOINT_RECOVERY.md` (F11 recovery budgets), `LEGACY_CHECKPOINTS.md` (F12 roles),
`DUST_ECONOMY.md` (tier lines). Recon: **B §B1, §B2, §B6, §B7 (tier half)**, **H §H-38, §H-41,
§H-42, §H-43**, **C §C-14**, **E §6.3**.

**Current state.** `scripts/Core/TimelineIntegrityRules.cs` is the V7.1/V7.3 model:
`StartPercent = 100f`, `MaxSiphonSharePercent = 10f`, `SiphonGraceSeconds = 10f`,
`ExtractorDestroyRestorePercent = 3f`, `GenericSecretRestorePercent = 2f`,
`SpecialSecretRestorePercent = 5f`, `RestoredThreshold = 90f`, `StabilizedThreshold = 70f`,
`EndingThresholdPercent = 85f`, `DrainPerSecond(difficulty) => Hard ? 0.2f : 0.1f`, `Tier()`,
`TierKey()`, `DustBonusPercent()` (10/5/0, authored but **never applied**).
`scripts/Environment/ChronalExtractor.cs` lines 76–94 implement per-machine drain:
`SiphonEngageDistancePixels = 600f` proximity engage, `SiphonGraceRemaining` countdown,
`DrainedSharePercent` capped at 10, drawing through `StoryManager.DrainTimelineIntegrityAmount`.
`StoryManager` has `TimelineIntegrityPercent` (line 281), `DrainTimelineIntegrityAmount` (299),
`RestoreTimelineIntegrity` (308), `RegisterSecretFound(id, isSpecialSecret)` (319),
`LevelSecretsFound`, `LastLevelIntegrityPercent`.
**Nothing triggers a Collapse when Integrity reaches zero** — the only `BeginTimelineCollapse` call
site is `ChronalRewindManager.cs:550` (rewind pool exhausted on death). The gauge is cosmetic plus a
results number. No `parSeconds`, no difficulty time multiplier, no clock-pause plumbing, no drain
factor, no PreBoss role. `StoryLevelControllerBase.BuildCheckpoint` hardcodes
`SelfActivating = id.EndsWith("_checkpoint_0")`; `CheckpointTrigger`
(`scripts/Environment/LevelManager.cs:68`) exports only `CheckpointID`, `RespawnOffset`,
`SelfActivating` — **there is no `checkpointRole` concept at all**, and no Hard-middle-inert rule
anywhere. Story pits do not exist: no kill boundary, no fall-death path; `PlayerController.cs:1968`
carries the forward-looking comment *"(Should a true blast-zone/pit death path ever be added, it must
bypass this — falling is not a hit.)"* beside the Defy branch — that is the hook site.
`scripts/Environment/CrumblingPlatform.cs` exists (shake → collapse → respawn) but no
`FractureEligible` flag. `CameraShake` exists with the accessibility scale.
Level 1 Florence still has **no authored Extractors** (`grep BuildExtractor
scripts/Environment/Level01Controller.cs` → none).

**Exact change list.**

**1. Rewrite `scripts/Core/TimelineIntegrityRules.cs`.**
*Delete:* `MaxSiphonSharePercent`, `SiphonGraceSeconds`, `ExtractorDestroyRestorePercent`,
`GenericSecretRestorePercent`, `SpecialSecretRestorePercent`, `DrainPerSecond(Difficulty)`, and the
whole `ChronalRatingRules` class (ruling 2.A).
*Move:* `RestoredThreshold` **90f → 50f**, `StabilizedThreshold` **70f → 20f**,
`EndingThresholdPercent` **85f → 50f**.
*Add:*
```
DifficultyTimeMultiplier(Difficulty) => Easy 2.0f, Normal 1.5f, Hard 1.2f
ExtractorDrainWeight = 0.2f
SecretDrainReduction = 0.1f
MinimumRawFactor = 0.9f
InitialDrainFactor(int startingExtractors) => 1.0f + 0.2f * startingExtractors
CurrentDrainFactor(int living, bool secretFound) => max(MinimumRawFactor, 1.0f + 0.2f*living - 0.1f*(secretFound?1:0))
DrainPerSecond(float parSeconds, Difficulty d, int living, int starting, bool secretFound)
    => 100f / (parSeconds * DifficultyTimeMultiplier(d)) * CurrentDrainFactor(living, secretFound) / InitialDrainFactor(starting)
```
`DustBonusPercent()` stays 10/5/0 and is **actually applied** by A10 (§4).
**Naming ruling:** the V7.6 text says `ChronalRatingRules → IntegrityTierRules`. `TimelineIntegrityRules`
already owns `Tier` / `TierKey` / `DustBonusPercent`. **Decision: delete `ChronalRatingRules.Compute`
entirely and leave the tier helpers on `TimelineIntegrityRules`.** Do not create an `IntegrityTierRules`
type. Record this in §9.

**2. The level clock.** `StoryManager` gains `ParSecondsForCurrentLevel`, `StartingExtractorCount`,
`LivingExtractorCount`, `SecretFoundThisLevel`, `IsIntegrityClockRunning`, `IsPreBossLocked`, and
ticks the gauge in `_PhysicsProcess` gated on the pause set. Key invariants:
- `initialDrainFactor` is **fixed at level load from the authored starting population** and **never
  recalculated from survivors**, including on checkpoint resume.
- Destroying a machine subtracts 0.2 from the *current* factor; finding the secret subtracts **0.1**
  for the rest of the level. **Neither ever changes the current gauge value — only the future rate.**
- Drain is **global from level load**, seen or not. No engagement, no room entry, no grace.
- **Nothing ever adds time back.** Rewinds do not refund. Time Freeze does not pause it.
- Clamp at zero.
- At par with no detours, all machines alive and no secret, remaining gauge is **50% / 33.33% /
  16.67%** (Easy / Normal / Hard). Pin that arithmetic in a test.

**3. The pause set.** A new `IntegrityClockPause` scope used by `DialogueManager`, `PauseMenuBase`,
`ChronalRewindManager` (death-rewind presentation **and** the collapse beat), and the boss-intro
ritual in `BossEncounterController`. The clock **runs** through all other live play including the
Restoration Font channel and **including an active Time Freeze**.

**4. Strip the per-machine drain.** `ChronalExtractor.cs`: delete `SiphonEngageDistancePixels`,
`SiphonEngaged`, `SiphonGraceRemaining`, `DrainedSharePercent` and the whole drain block.
`OnDestroyed` drops its `RestoreTimelineIntegrity(+3)` call and instead decrements the living-machine
count. **Keep** `RecordExtractorDestroyed` and `IStoryRewindSimulation` (the death-rewind freeze still
pauses the discharge cycle). Delete `StoryManager.RestoreTimelineIntegrity` and every call site
(`ChronalExtractor.cs:163`, `SecretCache.cs:53`).

**5. Secret.** `SecretCache` / `StoryManager.RegisterSecretFound`: `isSpecialSecret` no longer selects
a restoration amount; a found secret simply sets `secretFound = 1` (−0.1 factor). Keep the
once-per-attempt dedupe and the results count.

**6. Per-level par authoring.** Every `scripts/Environment/LevelNNController.cs` (2–15) gets an
authored `ParSeconds` constant plus its starting-Extractor count. **The design authors no measured
values** — V01a requires per-hero median measurement that has not happened. **Ruling: A3 authors
provisional `ParSeconds` values, records every one of them in the deviation ledger under
`VERIFY-PAR-SECONDS` as provisional-pending-measurement, and pins them in a test as a table so a
later measurement pass is a single-file edit.** Provisional values: use the level's authored room
count × 90 s as the seed, rounded up to the nearest 30 s, and state the derivation in §9. Do not
present these as measured.

**7. Collapse at zero.** The clock hitting zero fires `BeginTimelineCollapse` with a `TimerCaused`
cause in Acts I–II. (Act III's `AnchorSnap` branch is **A3b's**; A3 leaves a clearly-marked hook.)
Add the era-lost Sarah variant key `dialogue_collapse_sarah_era_lost` for the timer-caused collapse
(A6 writes the English value). Time Freeze cannot bypass an Integrity-zero collapse.

**8. Collapse Tremor (new system).** New `scripts/Environment/CollapseTremorController.cs`, a stage
state machine driven by `StoryManager.TimelineIntegrityPercent`, attached by `StorySceneBootstrapper`
so the untimed Tutorial and Florence opt out.
- **Stage 1 (<20%):** continuous camera shake **0.1**; jolt **0.3 for 0.4 s every ~6–8 s**; chronal
  crack lines across background layers with cold cyan-white glow; palette desaturates inward from the
  screen edges; props time-ghost; falling debris begins; `FractureEligible` platforms become unstable.
- **Stage 2 (<10%):** jolts every **~3–4 s**; cracks reach the foreground/playfield edges;
  desaturation ≈ half the screen; the vignette pulses with the HUD clock tick; debris twice as often.
- **Falling debris:** era-flavored, spawns above the camera near the player. Telegraph **0.75 s**
  (glowing crack overhead + a landing-surface marker). Damage **5% max HP**, small knockback,
  **blockable as a Basic-class hit**, routed through `PlayerController.ApplyEnvironmentalDamage`
  (the V7.3 chokepoint — Defy and Rally accounting apply), and it **never triggers hitstop**. Cadence
  ~1 per **4 s** (Stage 1) / **2 s** (Stage 2), **never more than two airborne at once**. Never
  inside a Restoration Font radius or on an activated checkpoint. **Enemies are unaffected.**
- **Unstable platforms:** only platforms flagged `FractureEligible` crack and behave as Crumbling
  Platforms — shake on landing, collapse, **respawn after 5 s**. Never eligible: pressure plates,
  latched-switch gates, `PathMovingPlatform`, the pre-boss approach. Guard: **every one respawns**, so
  no gap becomes uncrossable — assert that.
- Audio: low rumble bed, stone/timber groan per jolt, slow music-bus detune, HUD clock tick. Ship
  these as **silent placeholders** under the standing audio rule (`resources/Audio/README.md`).
- First crossing plays a one-per-level non-blocking Sarah bark; the clock keeps running.
- Stops whenever the clock pauses; ends for good at the PreBoss checkpoint (the boss arena is always
  stable). A granted timer recovery starts ≥25%, above the threshold, but Tremor can return later.
- Accessibility: all shake scales with the Settings `screenShakeSlider` (0 disables); glow/ghost/
  vignette never flash >3 Hz; the **C01a Reduced Temporal Effects** preset (A8 ships the setting)
  removes decorative glow/vignette pulsing, ghosting and heavy desaturation while retaining static
  danger/crack markings and **unchanged Tremor timing**.
- A3 publishes `EventBus.OnCollapseTremorChanged(TremorPayload{Level})`; **A8** owns the HUD tick and
  the shared crack-glow overlay.
- New pooled `falling_debris` scene + a pool row in `resources/Pools/level_pool_configs/*` (14 level
  configs) and `scene_pool_catalog.tres`.

**9. Checkpoint roles (F12).** `CheckpointTrigger` gains
`[Export] public CheckpointRole Role` (`Entry | Middle | PreBoss`), authored **alongside** the stable
ID. `StoryLevelControllerBase.BuildCheckpoint` drives `SelfActivating` **and** the Integrity freeze
from the role, **never from the ID suffix**. Shared campaign levels keep their existing
`{levelID}_checkpoint_{0,1,2}` IDs mapped to Entry/Middle/PreBoss. Hard's "middle inactive" rule
applies **only to shared Acts I–II**; shared Act III keeps its Hard middle active (A3b enforces the
Act III half). Migrate saves **by role**, never by suffix; add a per-level ID→role alias map so a
verified existing stable ID survives.
**PreBoss activation freezes the gauge permanently** and records the level's final Integrity in the
same F10 checkpoint transaction, after the required approach objectives. Entry and Middle, and the
4A Eraser route trigger, **cannot lock the clock**. An inert Hard middle saves nothing. Reload retains
the lock and the score without refilling the gauge or replaying checkpoint benefits.
`StorySaveData` gains `CheckpointIntegrityPercent` (§2.6) — the **paid-recovery allowance**, distinct
from the live gauge.

**10. F11 recovery minima.**
```
allAliveDrainPerSecond = 100 / (parSeconds * difficultyMultiplier)
requiredRecovery       = ceil(allAliveDrainPerSecond * remainingRouteSeconds * 1.20)
recoveryMinimum        = max(25, requiredRecovery)
timerRecoveryIntegrity = max(checkpointIntegrity, recoveryMinimum)
```
A calculated minimum **above 100 is an invalid content budget** — **reject it loudly**, never clamp
to 100. Applies **only** to a timer-caused Collapse that actually grants recovery — not ordinary
load, not death rewind. `remainingRouteSeconds` is authored per level/variant/checkpoint/difficulty;
no measured values exist, so A3 authors provisional rows under the same `VERIFY-PAR-SECONDS` ledger
entry and pins the arithmetic, not the content.

**11. F16 lethal Story pits.** Crossing an authored kill boundary resolves **one non-hit fall death**
(HP → 0) regardless of HP, block, armor, one-hit shield or temporary invulnerability. Camera framing
never kills. No 25%-HP penalty, no recoverable pit, no free checkpoint teleport.
**Defy History cannot prevent it** — the used flag is unchanged, no damage-based meter, no Rally echo.
Implementation:
- Add `public void KillPlayerNonHit()` on `PlayerController` transitioning to `Dead` **without**
  touching Defy / Rally / meter, so a pit author cannot accidentally route through `ApplyDamage`.
  (The Defy branch is already hit-only by construction — `PlayerController.cs:1968`.)
- Add a `StoryKillBoundary` Area2D type and author kill boundaries in the level scenes that have
  lethal openings. **Scope ruling:** A3 authors the type, the chokepoint and the boundaries for the
  levels that already describe pits in their controller docs; levels that explicitly authored **no**
  pits (L12 "the Moon has no bottomless pits", L13 "there is no fall death here", L14, L15, and L04's
  boss pit which is "deliberately not a hazard") **stay pit-free** and A3 records that in §9.
- With a death-rewind charge: spend exactly one, then the existing grounded-history rewind with the
  difficulty HP restore (Easy 70 / Normal 50 / Hard 30) and normal post-rewind invincibility; 1→0
  completes and does not itself Collapse. Use the **deepest valid grounded-history landing** with the
  existing fallback chain, and validate landing support/clearance so recovery does not drop the hero
  back inside the boundary.
- With no charge: Acts I–II Collapse; Act III Anchor Snap or Smothered (**A3b**).
- Integrity: live fall time drains it; crossing during Time Freeze still kills and ends the effect
  under its cooldown rule; the death/recovery presentation pauses Integrity but the rewind never
  restores it. If Integrity hits zero on the same update, resolve **one timer-caused Collapse
  instead**, without also spending a charge or charging a second fee.
- Every lethal opening needs a readable edge/depth cue, **including during Time Freeze**.

**12. Retire the abnormal-exit fee (ruling 2.B).** Delete `SessionExitGuard.ApplyAbnormalExitFee`
and the `SaveManager._Ready` boot billing; delete the loc key `save_notice_abnormal_exit_fee`
(`en.csv:1153`). **Keep** the marker file and `CalculateExitRetainedDust` /
`CalculateExitWalletAfterPenalty` — the voluntary 20% is unchanged, and F10 repurposes the marker as
an **attempt-status router**, not a fee trigger. `tests/unit/SessionExitGuardTests.cs` currently pins
the crash fee and must be rewritten.

**13. E01 drain feedback.** On a **real first destruction while the clock runs**, show a brief
non-blocking "Integrity drain slowed" notice. **No** "time gained", no percentage refill, no
seconds-saved estimate, no numeric drain readout. Dust keeps its separate actual-collection `+N`
feedback. Untimed levels and a locked PreBoss clock show **no** drain notice. New key
`integrity_drain_slowed` under `# Package 11 A3`.

**14. Florence's three Extractors.** Author them in `Level01Controller.cs` — the one standing content
gap. Level 1 is **untimed** (no Integrity clock, no tier bonus), so they carry presentation and dust
only; their dust allocation is **A10's** (Level 1's whole 10 optional dust goes to its Extractors).
A3 authors the placements and wires `RecordExtractorDestroyed`; A10 wires the dust.

**Files owned (exclusive).** `scripts/Core/TimelineIntegrityRules.cs`,
`scripts/Environment/ChronalExtractor.cs`, `scripts/Environment/CollapseTremorController.cs` (new),
`scripts/Environment/SecretCache.cs`, `scripts/Environment/LevelManager.cs` (`CheckpointTrigger`),
`scripts/Environment/StoryLevelControllerBase.cs` (**A3 owns this file in Wave 1**),
`scripts/Core/SessionExitGuard.cs`, all `scripts/Environment/LevelNNController.cs` for **par + kill
boundary + `FractureEligible` flags only**, `scripts/Environment/Level01Controller.cs` (Extractors),
new `scenes/vfx/falling_debris.tscn` and its pool rows.

**Shared files touched additively.** `scripts/Core/StoryManager.cs` (**region: the Integrity clock,
the pause set, checkpoint-role plumbing, `CheckpointIntegrityPercent`** — the largest Wave 1 claim on
this file; see §5), `scripts/Core/SaveManager.cs` (one additive field),
`scripts/Characters/PlayerController.cs` (**region: `KillPlayerNonHit()` only**),
`scripts/Core/EventBus.cs` (`OnTimelineIntegrityChanged`, `OnCollapseTremorChanged` + payloads +
`IntegrityTier`), `scripts/UI/DialogueManager.cs` / `scripts/UI/PauseMenuBase.cs` /
`scripts/Environment/ChronalRewindManager.cs` / `scripts/Enemies/BossEncounterController.cs`
(**one pause-scope call each**), `resources/Pools/level_pool_configs/*`,
`resources/Pools/scene_pool_catalog.tres`, `localization/en.csv`.

**Tests.**
- **Rewrite wholesale:** `tests/unit/TimelineIntegrityTests.cs`. Case 1
  (`TheIntegrityTiersDrainRatesAndRestorationPathsMatchTheDesign`), case 2 (the Chronal Rating case),
  case 3 (`TheSiphonIsGracedCappedAtItsShareAndPausedByTheRewindFreeze`) and case 4
  (`RestorationPaysThreePerExtractorTwoPerSecretAndFiveForTheSpecial`) are all invalidated.
  Replacement cases (**net +6** over the four removed → the suite grows by 6):
  drain-equation arithmetic; the fixed denominator across a resume; the pause set; the PreBoss freeze;
  clamp-at-zero; the 50/33.33/16.67 at-par table; the 50/20 tier lines; collapse-at-zero fires;
  destroying a machine never moves the gauge; the secret subtracts 0.1 from the rate only.
- **Rewrite:** `tests/unit/SessionExitGuardTests.cs` — the crash fee is gone; the voluntary 20%
  survives. **±0.**
- New `tests/unit/CheckpointRoleTests.cs` **+6** — role drives `SelfActivating`; role drives the
  PreBoss freeze; Entry/Middle/Eraser-trigger never lock; Hard middle inert in Acts I–II only; a `_1`
  suffix with role `PreBoss` still locks; ID→role alias migration.
- New `tests/unit/CollapseTremorTests.cs` **+9** — stage thresholds 20/10; debris cadence, the
  two-airborne cap, the 0.75 s telegraph, the 5%-max-HP damage fraction and Basic-class blockability;
  enemies immune; Font/checkpoint exclusion zones; the respawn guarantee; pause and PreBoss shutdown;
  shake-slider zero disables.
- New `tests/unit/F11RecoveryBudgetTests.cs` **+4** — the arithmetic, the ≥25 floor, the ×1.20 buffer,
  the above-100 rejection.
- New `tests/unit/StoryKillBoundaryTests.cs` **+5** — crossing kills regardless of HP/armor/shield;
  Defy cannot prevent it and its used flag is unchanged; no Rally echo and no damage-meter; a charge
  spends and the rewind lands on a validated grounded sample; zero charges routes to Collapse.
- Extend `tests/ContentValidation/Level01ContentTests.cs` **+1** — three authored Extractors.
- Extend `tests/unit/StoryLevelControllerBaseTests.cs` **+2** — `FractureEligible` exclusions;
  every campaign level authors a `ParSeconds`.

**Expected count delta: −4 removed, +37 added = +33** (1673 → 1706 at A3's merge).

**Acceptance criteria.**
- `grep -rn "MaxSiphonSharePercent\|SiphonGrace\|RestoreTimelineIntegrity\|ChronalRatingRules"` over
  `scripts/` returns nothing.
- A Normal run that reaches PreBoss in exactly `ParSeconds` with all machines alive and no secret
  ends at 33.33% Integrity.
- Destroying every Extractor never raises the gauge by a single point.
- A timer-caused Collapse is reachable and distinct from a death-caused one.
- Crossing a kill boundary at 100 HP with a full shield and hyper-armor kills the player.
- A crash (marker present, no clean shutdown) charges no fee.

---

### A4 — Resonance Grid V7.6

**Goal.** Re-author all nine grids to their V7.6 topologies, add the `prerequisiteMode` /
`gatedAbilityID` / `abilityScope` / `Tier` / `LayoutPosition` schema, extend the resolver with the
Any-of branch and the new stat lanes, implement the nine traversal behaviours and the eight reworked
Majors, and replace the flat `GridContainer` with a real topology renderer.

**Design references.** `design-godot.md` §5 (master 2319–2899) plus the Resonance Grid rules
subsection (master ~428–522); F06, F07, F08. Recon: **F, in full** — §6 carries the nine complete
per-character node tables and §9 the exact node-ID scheme; **B §B9**; **D §15.5 (D-32), §2.3 (D-26),
§10.10 (D-16)**.

**Current state.** `scripts/Environment/ResonanceNodeData.cs` (21 lines) has
`SchemaVersion(=1), NodeID, DisplayName, DisplayNameKey, Description, DescriptionKey,
Type(Minor|Major), UnlockCost(=100), PrerequisiteNodeIDs, StatModifierKey, StatModifierValue,
StatModifierIsPercent, AbilityModifierKey` — **no `prerequisiteMode`, no `gatedAbilityID`, no
`abilityScope`, no `Tier`, no layout position**.
`ResonanceProgression.cs:58–59` hardcodes All-of prerequisites. `Resolve` (133–167) is a `switch` over
~22 free-form string keys. All nine `resources/Resonance/*_grid.tres` are the retired
"three linear chains of three" template. `ResonanceGridPanel.LoadActiveGrid` (192–206) iterates
`_grid.Nodes` in array order and `AddChild`s a `Button(330×150)` into a `GridContainer` with
`GridColumns = 3` as a `private const`; `ResonanceGridNavigation.Move(index, dCol, dRow, 3, count)` is
pure row-major index arithmetic with no adjacency awareness; **there is no edge rendering at all**.
`en.csv` carries 185 `resonance_*` rows = **162 node rows + 23 UI rows**.
**Good news to bank:** all 27 Major perk keys already exist as `const string …PerkKey` with live
`Owner.HasStoryPerk(...)` consumers, and `ResonanceProgression.CollectUnlockedAbilityModifiers`
already collects **any** node with a non-empty `AbilityModifierKey` — not just Majors. So the nine
traversal flags need **no new plumbing**: author them as nodes carrying an `AbilityModifierKey` and
they land in `PlayerController.StoryAbilityPerks` through the existing `CharacterFactory` path.

**Exact change list.**

**1. `ResonanceNodeData` schema (bump `SchemaVersion` to 2 on node and grid resources).** Add:
`[Export] PrerequisiteMode PrerequisiteMode = All` (new enum `{ All, Any }`);
`[Export] string GatedAbilityID = ""`; `[Export] string AbilityScope = ""`;
`[Export] int Tier` (1 / 2 / 3 — **explicit, no longer inferable**: V7.6 breaks the
"Tier 1 ⇔ zero prerequisites" inference in four grids — Joan `A1`, Leonardo `M1`, Tesla `W1`,
Lincoln `A1`/`B1`, plus Lincoln `C`);
`[Export] Vector2 LayoutPosition` (normalized 0–1 — **the single most important schema addition the
design's own snippet omits**; nine unique topologies cannot render without it).
Append `ResonanceNodeType.Traversal = 2` (§2.8).

**2. Stat keys: keep strings, add a compile-time key class.** Do **not** convert `StatModifierKey` to
a C# enum — the `.tres` authoring and the
`GetUnlockedStatTotal(grid, save, "PersistentDuration")` API both depend on strings, and converting
churns `AbilityZoneTalentTests`, `ResonanceProgressionTests` and four ability scripts for no gain.
Instead add `public static class ResonanceStatKeys` of `const string`s plus a validation test.
*New lanes:* `RallyEchoFraction` (4 uses — Einstein, Joan, Shakespeare, Lincoln),
`UltimateBuildRate` (1 use — Mozart `mozart_resonance`), `ExtractorDamage` (declared,
**no minor uses it** — the three extractor effects are Majors; author the lane for future-proofing and
do **not** invent a minor).
*New scoped lanes:* `AbilityDamage`, `AbilityRange`, `AbilityDuration`, `ConstructHP`, always paired
with `AbilityScope`.
**Implementation shape:** keep the 22 existing `StoryStatProfile` lanes for character-wide keys and
add **one dictionary** `IReadOnlyDictionary<(string key, string scope), float>` with an accessor
`GetScoped(key, scope, default: 1f)`. That avoids exploding `CharacterFactory`'s 22 hand-copied
assignments. Call sites read `player.StoryScoped("AbilityRange", "relativity_rift")`.

**3. Resolver.** `EvaluateUnlock` gains the `Any` branch (`if none satisfied → MissingPrerequisite`;
an empty list stays root-eligible) and a `GatedAbility` check returning a new
`ResonanceUnlockResult.AbilityLocked`. **One shared evaluator** serves UI availability, purchase,
respec dependency checks and save validation — `EvaluateUnlock` already is that single point.
Add authored-data validation rejecting **unknown prerequisite IDs and cycles**.
`Resolve` gains the three new character-wide keys and the scoped bucket; delete the dead-key comment
block (`BlockDurability`, `Armor`).
Note `PercentValue` (295–296) is a no-op identity function — `StatModifierIsPercent` is decorative.
**Do not "fix" it** without auditing all 54 authored values; record the observation in §9.

**4. Re-author all nine `.tres` grids** to the V7.6 topologies. **Recon F §6.1–6.9 carries the
complete nine tables** — node name, tier, prerequisites and mode, stat/effect, gate, and the delta
from the current node. Use the node-ID scheme in **recon F §9** verbatim (e.g.
`einstein_momentum`, `einstein_chalk_edge`, `einstein_mass`, `einstein_extended_float`,
`einstein_rift_range`, `einstein_rally_resonance`, `einstein_event_horizon`, `einstein_critical_mass`,
`einstein_quantum_entanglement`). Costs stay **50 (T1) / 75 (T2) / 200 (Major) = 975**.
Topology identities: Einstein mesh, Joan spine, Leonardo interlocking gear rings, Tesla two-rail
circuit, Shakespeare three-act tiers, Mozart ascending scale, Cleopatra converging delta, Lincoln
split-rail fence, Pocahontas crossing currents.
**Explicit test cases to honour:** Shakespeare's Act II nodes use `Any [T1, C1, H1]` and all three of
his Majors use `All [T2, C2, H2]` (F08 Option B → his cheapest first Major costs **475**);
Leonardo's Daedalus Wings capstone is `All-of` three; Pocahontas's Leaf Barrier is
`All-of [Second Glide, Forest Vigor]`.
Cheapest-first-Major per character: Einstein 325, Joan 350, Leonardo 325, Tesla 325, Shakespeare 475,
Mozart 350, Cleopatra 375, Lincoln 325, Pocahontas 325.
**Every root-adjacent node must modify a system available from Level 0** (basics, block, Rally, HP,
speed, meter) — otherwise the first hub visit shows an empty grid under the hidden-node rule.
**Hygiene, enforced by a validator test:** minors never touch stun or hitstun duration, never grant
rewind charges, and never modify the Integrity drain rate. Grids interact with the timer only
indirectly (e.g. `ExtractorDamage`).
**Retired keys to delete outright** (recon F §7): `Armor` (`joan_d1`), `BlockDurability`
(`einstein_d2`, `shakespeare_h1`, `cleopatra_pw2`, `lincoln_s2`), `BlockRecovery` (`pocahontas_pw2`),
`ComboSpeed` (`joan_m2`), `JumpForce` jump-height minors (`joan_r2`, `leonardo_m2`, `pocahontas_wr1`),
`StatusDuration`-used-for-stun (`tesla_p2`), `ProjectileSpeed` (`einstein_o2`, `mozart_f1`),
`ProjectileDamage` (`leonardo_e2`), `GlideSpeed` (`leonardo_m1`), `KnockbackForce`
(`shakespeare_c1`).
**⚠ Never author an empty Script-typed array** — every grid ends with
`Nodes = Array[ExtResource("2")]([...])` (§2.13).

**5. Nine traversal behaviours** (all new; author each as a Tier-2 node carrying an
`AbilityModifierKey` so the existing perk plumbing carries it):

| Character | Flag | Site | Notes |
|---|---|---|---|
| Einstein | Extended Float (+20 frames) | `EinsteinRelativityWarp` float window | window is frame-authored in `movement.tres` |
| Joan | Wings Refresh | `PlayerController.OnMeleeHitConfirmed` where `HitboxID == "combo_3"` + `JoanRighteousSmite` on-hit → reset `joan_ascendant_wings` cooldown to 0 | **once per attack execution** even on multi-target; no trigger on whiff/blocked/invulnerable/DoT/prop; does **not** restore air jumps, add glide time, cancel the action, or bypass Suppression/Time Freeze |
| Leonardo | Re-placement | `LeonardoTurretNode` pickup/re-place, **once** | interacts with `PoolManager` release |
| Tesla | Long Blink (+0.5 units) | `tesla/movement.tres` distance at cast | |
| Shakespeare | Tempest Apex Jump | `ShakespeareTheTempest` lift apex → jump refresh | **V7.6 correction:** the V7 text said "barrier can be jumped from"; no barrier exists in his kit |
| Mozart | Extra Note | `MozartSonataDrift` platform cap 1 → 2 | deploy-limit already modelled |
| Cleopatra | Vortex Step | `CleopatraDesertMirage` cast + `SandstormVortex` per-instance allowance | halves that cast's cooldown **after** other modifiers (5 s → 2.5 s); **once per vortex instance**, consumed atomically on an accepted cast even if later interrupted; a refused input spends nothing; overlapping vortices cannot stack (pick deterministically); entering after Mirage begins does not qualify; survives restoration of that same vortex; a new vortex has a fresh allowance; respec removes future eligibility without rewriting a started cooldown |
| Lincoln | Rail Breaker | `LincolnRailCharge` travel vs projectiles | destroys **one** hostile projectile authored as breakable per activation, resolved **before** it applies damage; **requires a new `IsBreakable` authoring flag on hostile projectiles that does not exist** — A4 adds it to `EnemyProjectile`/`PlaceholderProjectile`; beams, zones, hazards and unbreakable projectiles are never destroyed; no reflection, no extra damage, no blanket invulnerability, **no effect on the universal roll** |
| Pocahontas | Second Glide | `PocahontasBreezeGlide` re-entry **once per airtime** | needs an airtime latch reset on grounding and stock loss |

**6. Eight Majors needing code work** (the other 19 are unchanged):

| Character | Major | Work |
|---|---|---|
| Einstein | Event Horizon | add "projectiles crossing the rift slowed to half speed" |
| Einstein | Critical Mass | Radiant Burn = **1.25× vulnerability, 3 s, no periodic damage**; applied **after** the triggering hit resolves; skipped on projectile contact / blocked / invulnerable; damage slot, stronger-wins; coexists with the rift's Time Dilation |
| Einstein | Quantum Entanglement | verify the warp resolves **before** the 1 s daze; 5 s lockout unchanged |
| **Joan** | **Zealous Vigor** | **rewrite** — reclaims an extra **25% of the current Rally echo pool** on the finisher, on top of the damage-scaled reclaim. The V6 "heal 5% missing HP" at `PlayerController.cs:720` is retired |
| **Joan** | **Shield of Orléans** | **rewrite (F06 Option A)** — **flat +5 meter per distinct successfully blocked hostile attack** (cap 100), replacing the damage-scaled `×1.25` at `PlayerController.cs:567`. Requires ≥1 charge consumed; a shattering block still qualifies. Nothing on hold-block / whiff / behind / unblockable / invuln / zero-charge refusal. "Distinct" = **one attack execution** → track a source attack ID; each basic-string strike counts once; a multi-hit ability/volley/zone/piercing projectile grants **at most one**. **Plus:** the first successful block of a **Guard-Crush** execution refunds one block charge (cap 3), after normal consumption and any shatter; the refund does not cancel daze/shieldstun/the 5 s lockout, and the refunded charge is **unusable until the lockout ends** |
| Leonardo | Clockwork Overdrive | bolts deal **double damage to Chronal Extractors** (5 bolts already done). Fix the pre-existing copy bug: `resonance_leonardo_e3_description` says "five … instead of **three**"; design says 4→5 |
| Tesla | Resonant Overdrive | **drop** the "+5 s coil duration" clause (moved to the minor); **add** coil arcs targeting Chronal Extractors in radius (new target acquisition against `damageable_environment`) |
| Tesla | Lorentz Attraction | `AttractionRootBonusSeconds` **1.0 → 0.5** |
| Mozart | Virtuoso Drift | **display rename only** (keep the `virtuoso_dash` perk key). Note the V7 baseline "landing on a staff refunds half of Sonata Drift's cooldown" is still unimplemented — record it in §9, do not silently add it |
| **Lincoln** | **Kinetic Splitting** | **full rewrite; nothing survives.** The V6 perk restated the baseline and granted nothing (`PlayerController.cs:2377` merely makes combo hit 3 `AttackClass.Special`). Now: Splitting Strike's spike **ground-bounces** an airborne target into a **20-frame follow-up window** (a true combo into the string's launching Hit 2 — **PvE only**, and subject to the Stagger Discipline's diminishing special stun), **and** the strike deals **+50% to Chronal Extractors and enemy constructs** |

**Note the four shield Majors (Henry's Bastion, Royal Aegis, Leaf Barrier, Wardenclyffe Shield) are
A1b's**, not A4's — A4 authors their nodes and prerequisites; A1b rewrites their lifecycles.
**Cleopatra's Minor Asp Mark** scales `BasicComboRules.StringProfiles["cleopatra"]
.finisherStatusIntensityMilli = 500` (0.5 → 0.75). **`BasicComboRules` is cross-mode and read-only
for A4** — apply the Story multiplier at the hit-application site in `PlayerController`, never by
editing the rulebook constant.

**7. Grid UI topology renderer.** Replace the `GridContainer` with a `Control` + absolute placement
driven by `LayoutPosition`; draw prerequisite edges (`Line2D` or `_Draw()`), **solid for All-of,
dotted for Any-of**, glowing when satisfied; replace `ResonanceGridNavigation`'s row-major `Move`
with nearest-neighbour-in-direction over `LayoutPosition`; render gated nodes as a **dim, unlabeled
"dormant resonance" star in its true position**, gaining name/cost/tooltip on unlock; fix
`PrerequisiteName` (334–344), which returns only the *first unmet* prerequisite — wrong for Any-of
("requires any of A, B") and for Shakespeare's All-of-three. Node chips must shrink from
`CustomMinimumSize(330, 150)` to roughly 200×90 or become icon+tooltip.
**Mozart's musical-staff presentation is deferred to Package 10 art polish** — record it in §9.
`scenes/ui/ResonanceGrid.tscn` binds `Center/Panel/Layout/{Balance,Status,NodeGrid,CloseButton}` by
name and `ResonanceGridSceneTests.TheAuthoredSceneCarriesEveryControlThePanelBindsByPath` pins
`NodeGrid` as a `GridContainer` — that test is rewritten, not deleted.
**The dormant-star rendering is gated on A5's Legacy Unlock Schedule.** A4 authors `GatedAbilityID`
and the renderer; A5 merges before A4 is exercised end-to-end. If A5 has not merged when A4 needs it,
A4 stubs `IsAbilityUnlocked` as always-true with a `TODO(A5)` and says so in §9.

**8. Localization.** Delete all **162** old node rows and author 162 new ones under
`# Package 11 A4` (A4 **owns** the `resonance_*` node rows). The 23 UI rows survive unchanged. New UI
rows: `resonance_state_locked_ability`, `resonance_dormant_node`,
`resonance_state_locked_prerequisite_any`, `resonance_result_ability_locked`, and optionally
`resonance_tier_{1,2,3}`. Deleting the old rows in the same change is **mandatory** —
`UnusedTranslationKeyTests` has `RecordedOrphanCeiling = 11` and 162 new orphans fails hard.
Fix `resonance_joan_d3_description` / `resonance_joan_m3_description`, which describe the retired V6
effects.

**9. Save migration.** Every node ID changes in every grid. **Ruling: on load, drop any `GridProgress`
node ID not present in the character's current grid and refund it at its old tier price.** That
covers all nine characters, not just Shakespeare's F08 case, and is strictly simpler than a
per-character migration. `StorySaveData.GridProgress` is
`Dictionary<string, List<string>>` with no cost record, so the refund uses a retired-ID → cost table
authored by A4. A4 writes the migration *function*; Phase C wires it into the v5→v6 step.
F08's requirement is satisfied: one atomic, versioned, free respec that removes purchased
nodes/effects, refunds exactly the recorded paid costs once, preserves campaign progress and
undeposited earnings, and **never grants missing nodes for free**.

**Files owned (exclusive).** `scripts/Environment/ResonanceNodeData.cs`,
`scripts/Environment/ResonanceGridData.cs`, `scripts/Environment/ResonanceProgression.cs`,
`scripts/UI/ResonanceGridPanel.cs`, `scripts/UI/ResonanceGridNavigation.cs`,
`scenes/ui/ResonanceGrid.tscn`, all nine `resources/Resonance/*_grid.tres`,
all `scripts/Characters/Abilities/*.cs` **except** `TeslaAbilities.cs` (A1 owns that file in Wave 1 —
A4 coordinates the two Tesla Majors through A1 or defers them to a follow-up commit after A1 merges;
state which in §9).

**Shared files touched additively.** `scripts/Characters/CharacterFactory.cs` (the scoped-stat
dictionary copy), `scripts/Characters/PlayerController.cs` (**region: the scoped-stat accessor,
Zealous Vigor, Shield of Orléans, Kinetic Splitting, the Rally echo fraction term** — see §5),
`scripts/Enemies/EnemyProjectile.cs` (`IsBreakable` flag), `localization/en.csv`,
`scripts/Core/SaveManager.cs` (the migration function only).

**Tests.**
- **Rewrite wholesale:** `tests/unit/ResonanceProgressionTests.cs` (412 lines, 12 cases). Every
  structural assumption breaks: `AllCharacterGridsHaveThreeValidThreeNodeBranchesAndLocalizedCopy`
  asserts **T1 ⇔ zero prerequisites** (breaks in Joan/Leonardo/Tesla/Lincoln), **Major has exactly 1
  prerequisite** (breaks for Leonardo's capstone, all three Shakespeare Majors, Pocahontas's Leaf
  Barrier), and **T2 has exactly 1 prerequisite** (breaks for every Any-of / pair node).
  `EveryAuthoredMinorNodeKeyEitherResolvesOrIsDocumentedUnresolved` must **empty**
  `documentedUnresolvedKeys = {BlockDurability, Armor}` and add the seven new lanes; traversal nodes
  carry **no** stat key, which the current test forbids.
  `RePointedTalentMinorsCarryTheirDesignedAbilityScopedKeys`,
  `UnresolvedMinorKeysAndLockedNodesResolveNeutral`,
  `MinorCooldownPersistentAndStatusKeysResolveFromAuthoredGrid`,
  `MinorRangeComboKnockbackProjectileGlideAndRecoveryKeysResolve`,
  `UnlockedGenericAndMajorModifiersRemainCharacterScoped`,
  `CollectUnlockedAbilityModifiersReturnsOnlyUnlockedMajorPerkKeys` (**also misnamed now** — traversal
  flags are non-Major nodes carrying an `AbilityModifierKey`) all need ID and shape updates.
  Rewritten suite: **12 → 22 cases (+10)** — explicit tiers, costs 50/75/200 summing to 975,
  `prerequisiteMode` validity, no cycles, no dangling IDs, per-character expected topology, the
  Any-of matrix, Shakespeare's 475 arithmetic, the per-character cheapest-route table, Leonardo's
  All-of-3 capstone, and the gate-locked result.
- **Rewrite:** `tests/unit/ResonanceGridSceneTests.cs` (5 cases) — the `GridContainer` pin and the
  row-major population case. **±0.**
- **Rewrite or retire:** `tests/unit/ResonanceGridNavigationTests.cs` (3 cases) — row-major clamp
  becomes adjacency navigation. **±0.**
- **Update IDs:** `tests/unit/ResonanceRespecTests.cs` (4), `tests/unit/MirrorParadoxTests.cs:140`
  (the `GridProgress["einstein"]` isolation fixture), `tests/ContentValidation/LincolnContentTests.cs:59`.
  Add the **F08 migration respec** case to `ResonanceRespecTests` (**+1**).
- **Rewrite:** `tests/unit/AbilityZoneTalentTests.cs` (4 cases) — all four pin node IDs and generic
  lanes; they move to scoped keys, and Cleopatra's nest/vortex re-scope changes the asserted target.
  **±0.**
- New `tests/unit/ResonanceTraversalTests.cs` **+9** — one per traversal flag, each pinning its
  specific refusal conditions (Joan's once-per-execution, Cleopatra's once-per-vortex atomic consume,
  Lincoln's one-projectile-per-activation and the beams/zones exclusion, Pocahontas's airtime latch).
- New `tests/unit/ResonanceMajorReworkTests.cs` **+6** — Zealous Vigor's 25% pool reclaim, Shield of
  Orléans's flat +5 with attack-ID dedup and the Guard-Crush refund's lockout-bound unusability,
  Kinetic Splitting's ground-bounce window and +50% vs Extractors/constructs, Critical Mass's
  ordering and gating, Event Horizon's projectile slow, Resonant Overdrive's Extractor targeting.
- New `tests/unit/ResonanceHygieneTests.cs` **+3** — no minor touches stun/hitstun, no minor grants
  rewind charges, no minor modifies the Integrity drain rate.
- `tests/ContentValidation/UnusedTranslationKeyTests.cs` must stay green at ceiling 11.

**Expected count delta: +29** (1706 → 1735 at A4's merge).

**Acceptance criteria.**
- All nine grids total exactly 975 dust with 3/3/3 tier counts.
- Shakespeare's cheapest first Major costs exactly 475.
- Every Tier 1 node modifies a system available at Level 0.
- The authored-data validator rejects a cycle and a dangling prerequisite ID.
- A gated node renders as a dormant star in its true position and refuses purchase with
  `ResonanceUnlockResult.AbilityLocked`.
- `UnusedTranslationKeyTests` is green — all 162 old rows deleted in the same change.
- Nothing in `scripts/FighterSim/` reads a Resonance value.

---
### A5 — Legacy Unlock Schedule, ability slot locks, rebuilt Level 0, dialogue mechanisms

**Goal.** Ship the V7.5 Legacy Unlock Schedule with its Story-only ability gate and the three slot
lock states, rebuild Level 0's calibration to the V7.6 beat list, and ship the dialogue mechanisms
(hero variants, captive tokens, global seen set, first-skip confirm) that A6's content is authored
against.

**Design references.** `design-godot.md` §3 Legacy Unlock Schedule (master 289, 296, 308–318, 2115,
3241, 3400), Level 0 calibration (master 380–392), F13 Defy seal (master 1274), F04 Nexus
authorization (master 1210), §16 dialogue mechanics (master 1960–1962, 2084–2101). Contracts:
`STORY_PERSISTENCE.md` (the Dialogue section), `HUD_CONTRACT.md`. Recon: **B §B3, §B10**,
**E §4.1**, **I §8.1, §8.2**, **D §15.3 (D-30), §7.2, §8.6 (D-15)**, **H §H-07, §H-24, §H-34**,
**A §7 (A7), §8 (A8)**.

**Current state.** `grep -rn "unlockedLegacy|LegacyUnlock|AbilityUnlock"` over `scripts/` and
`tests/` returns **zero hits**. `CharacterFactory` builds the full kit unconditionally;
`PlayerController.StoryAbilityPerks` is the only Story-side gating concept and it is a perk list, not
an ability lock. `ResonanceNodeData` has no `gatedAbilityID` (A4 adds it). `StorySaveData` has no
unlock list.
`scripts/Environment/TutorialCalibration.cs` steps are
`BasicHits → RallyReclaim → Block → UseSpecial → UseUltimate → UseRewind → UseManualRewind → Done`
(alternate skip edges at 88 and 134/141). `Level00Controller` drives it. There is no grab step, no
hitstun/DI/tech step, no Defy step, no Time Freeze step; `UseSpecial`, `UseUltimate` and the
movement-ability beat all assume a full kit at Level 0.
`scripts/UI/DialogueSequenceData.cs` is flat — `DialogueID`, `SpeakerNameKeys`, `LineKeys`,
`EmotionKeys`, `SpeakerPortraits`, `PausesGameplay`, `AutoAdvance`, `AutoAdvanceDelay`. No condition,
no variant, no hero gate. `DialogueManager.StartSequence(string dialogueID)` does a flat lookup
(line 276). `DialogueManager.cs:301-303` gates hold-to-skip on
`save != null && save.IsCompleted && save.ViewedDialogueIDs != null &&
save.ViewedDialogueIDs.Contains(sequence.DialogueID)`; `HoldToSkipSeconds = 0.75f`;
`EndSequence()` → `RecordLastViewedDialogue(id)` → `EventBus.RaiseDialogueComplete(id)`, with no
effect ledger and no idempotency.
The only hero-aware hook that exists is the `speaker_player` convention (`en.csv:559`
`speaker_player,Traveler`) — that is the precedent to extend.

**Exact change list.**

**1. The schedule.** New `FTT.Core.LegacyUnlockSchedule` static:

| Milestone | Unlocked |
|---|---|
| Level 0 (calibration) | Basic 3-hit string, Block, Rally, Death Rewind, **Time Freeze**, Ultimate Meter + **Defy History** |
| Complete Level 1 | **Movement Ability** |
| Complete Level 2 | **Special 1** |
| Complete Level 3 | **Special 2** |
| Complete Level 4 | **Ultimate cast** (the meter exists from L0 so Defy and L4's dampening beams stay meaningful) |

`IsUnlocked(AbilitySlot slot, IReadOnlyCollection<string> completedLevels)`. **Milestone-gated, never
dust-gated** — the 975 grid economy is untouched.
Level 4A is the first full-kit level, and its results overlay plays the "Resonance Restored" beat for
the **Ultimate on 4A entry**, not on Level 4's exit.

**2. Save.** `StorySaveData.UnlockedLegacyAbilities` as
`Dictionary<string, List<string>>` (per character: `movement` / `special1` / `special2` / `ultimate`),
with a `Normalize()` entry. Additive (§2.6). Preserved on Restart Level.

**3. The gate.** `PlayerController.IsAbilityUnlocked(AbilitySlot)` consulted in the **same four input
branches** A1 gates for Suppression, fed by `CharacterFactory` from the active save exactly as
`StoryAbilityPerks` is today. **Story-only:** the `applyStoryProgression: false` path
(`FighterLoadoutFactory`, the hub Holodeck, the Calibration Drills, the Mirror Paradox clone) must
stay on full kits. A locked cast is refused; cooldowns are irrelevant because the ability has never
been cast.

**4. Slot lock states + the grant beat.** Publish
`EventBus.OnAbilitySlotLockChanged(AbilitySlotLockPayload{ Slot, State })` with
`State ∈ { Dormant, Suppressed, Clear }` (§2.9). `Dormant` = not yet unlocked; `Suppressed` = A1's
status is active; `Clear` = usable. **Locked slots are shown, never hidden.** A5 publishes; **A8**
renders the overlays.
`StoryManager.OnLevelComplete` grants the milestone unlock **inside the completion transaction**;
`LevelResultsPanel` plays the **"Resonance Restored"** beat; a one-line tooltip appears on first
field use.

**5. Grid gating handshake with A4.** A4 authors `ResonanceNodeData.GatedAbilityID` and the dormant
star; A5 supplies `IsAbilityUnlocked` as the upstream. If A4 merges first it stubs the call as
always-true; A5 replaces the stub at merge and both record it in §9.

**6. Level-design guard.** Levels 1–4 must be completable with the kit available at entry.
**Level 1 is authored for base jump reach.** No puzzle or traversal may require a not-yet-unlocked
ability. Level 2 Orléans is designed *around* the Movement Ability and is therefore the first level
that may legitimately use it. A5 audits Levels 1–4 route reachability and records any geometry change
in §9.

**7. Level 0 calibration rebuild.** New step enum:
`BasicHits, RallyReclaim, Block, Grab, HitstunDI, LandingTech, MeterAndDefy, DeathRewind, TimeFreeze, Done`.
**Delete `UseSpecial`, `UseUltimate`, `UseManualRewind`.** (A2 has already renamed
`UseManualRewind → UseTimeFreeze`; A5 re-orders and deletes the two ability steps.)

- **Grab Calibration (new, V7.6).** The dummy raises and **holds its own shield**; prompt
  *"A raised shield stops a blade, not a hand — hold Block and press Attack to grab."* The player must
  land **one grab and throw**; the follow-up names the rule both ways —
  *"Grabs beat blocks. Strikes beat grabs."* The dummy's block stance **persists until the grab
  lands**, so a player who keeps swinging sees everything absorbed and is re-prompted.
- **Hitstun Agency Calibration (new, V7.6).** The dummy lands one scripted **launching** hit, then two
  prompts in verb order:
  1. **DI** — during the launch's hitstop the game holds the freeze **a beat longer than normal
     (tutorial-only)**; prompt *"Hold a direction as the blow lands to steer where you fly."* Any held
     direction visibly bends the launch **±15°**; the beat proceeds even if nothing is held.
  2. **Landing tech** — prompt *"Hold Block as you land to recover on your feet"*, with a **slowed
     approach on the first attempt**. A missed tech plays the full knockdown and the scripted launch
     **repeats until one tech lands**.
  The launch is **free** — no HP, no Rally accounting, no rewind charge. Both are scripted-hit
  exemptions; tech works at **0 block charges** by the V7.3 ruling.
- **Meter & Defy History Calibration (replaces the Special and Ultimate calibrations).** Level 0 fills
  the meter to 100%, teaches *"A full meter can also refuse death itself — once."*, then the dummy
  lands one scripted **lethal-tagged** hit that **Defy History** absorbs, spending the meter.
  **F13:** show the seal **lit** before the proc and **broken** afterward; the coaching text explains
  that refilling the meter does not restore a spent Defy, and that a **dim intact** seal means unused
  but below full meter. No extra meter award, no extra Defy use. A5 publishes
  `EventBus.OnDefySealChanged` for the tutorial's forced states **only if A1b has not merged** —
  otherwise A5 consumes A1b's publisher. Wave ordering puts A1b in Wave 2, so **A5 declares the event
  and payload in `EventBus.cs` and publishes the tutorial states; A1b takes over the general publisher
  in Wave 2.** Record the handoff in §9.
- **Part 3 traversal** is authored for **base jump reach only** — the Movement Ability calibration is
  deleted (it unlocks after Level 1 with its own optional Wren drill). Ledge grab and drop-through
  calibrations are unchanged.
- **Time Freeze drill** calls **`TimeFreezeController.TryBeginTimeFreeze()`** (A2's contracted API).
  Author an **invulnerable-enemy + safe-destination** drill room, set
  `TimeFreezeController.DrillFreeFreeze = true` for the drill's duration (ready on **every retry**),
  advance on a **completed freeze** rather than on `OnRewindTriggered`, and clear the flag on
  completion. Objective key `tutorial_step_time_freeze` (A2 authored the key; A5 authors the drill).
  **Outside the drill there is no recharge exemption.** The drill consumes **no** death-rewind charge.
- **Retire the Stasis Echo plate gate** (A2 deleted the class; A5 removes the tutorial gate and
  re-authors that beat as an ordinary resource-free mechanism).
- **Part 1 presentation** (the seven-beat sequence: cold open → the beam → the playable pull → the gold
  ignition → the beam breaking → hazard evasion toward a Warden rift beside the Unbound tear → the
  Translation) is **A6b's visual-grammar item in Wave 2**. A5 authors the *phase structure and step
  ordering*; A6b fills in the two-colour treatment and the persistent gold aura. Record the split in §9.

**8. Dialogue mechanisms (§2.10, verbatim).** Implement exactly the schema in §2.10:
`DialogueSequenceData.HeroConditionCharacterID`, the `<baseID>@<heroID>` variant convention,
`DialogueSetData.FindSequence(baseID, activeHero)`, `{CaptiveName1}` / `{CaptiveName2}` token
substitution, `FTT.Core.CampaignCaptiveRoster` backed by the content manifest, hold-to-skip for every
sequence, the first-viewing `ConfirmModal`, and the split of `ResolveDialogueEffects(sequenceID)` out
of `EndSequence()`.
`GlobalSaveData.SeenDialogueIDs` (`HashSet<string>`) is **global across slots**, unioned after
completion **or confirmed skip**, never cleared by level restart or slot change, and **affects the
skip confirmation only**. Keep per-slot `StorySaveData.ViewedDialogueIDs` as the **effect/reward
idempotency ledger** — F10's "story slot effect IDs" — and write to **both** sets.
"Seeing a scene in another slot suppresses only the first-skip confirmation; it never suppresses
another slot's gameplay effects."
New keys under `# Package 11 A5`: `dialogue_skip_confirm_title`, `dialogue_skip_confirm_body`,
`dialogue_skip_confirm_ok`, `dialogue_skip_confirm_cancel`.
A5 ships the **mechanisms and the keys**; **A6 authors every line and every `.tres` sequence.**

**9. Move List dormant badges.** `MoveListScreen.Populate` gains a mode flag (Story vs Fighter); Story
entries for not-yet-unlocked abilities render **"Dormant — restored after Level N"** with **no frame
data** (suppress `movelist_ability_stats` for locked rows). Fighter Mode always shows the full kit.
New key family `movelist_dormant_until`. A5 owns `MoveListScreen.cs` in Wave 1.

**Files owned (exclusive).** `scripts/Core/LegacyUnlockSchedule.cs` (new),
`scripts/Core/CampaignCaptiveRoster.cs` (new), `scripts/Environment/Level00Controller.cs`,
`scripts/Environment/TutorialCalibration.cs` (**step enum and transitions — A2 landed the rename
first**), `scripts/UI/DialogueManager.cs`, `scripts/UI/DialogueSequenceData.cs`,
`scripts/UI/DialogueSetData.cs`, `scripts/UI/MoveListScreen.cs`, `scripts/UI/LevelResultsPanel.cs`
(**the Resonance Restored beat only** — A8 owns the rest of that file).

**Shared files touched additively.** `scripts/Characters/CharacterFactory.cs` (populate the unlock
set), `scripts/Characters/PlayerController.cs` (**region: `IsAbilityUnlocked` + the four gates +
the tutorial scripted-hit exemptions** — see §5), `scripts/Core/SaveManager.cs` (two additive
fields), `scripts/Core/StoryManager.cs` (**region: the milestone grant inside `OnLevelComplete`
only**), `scripts/Core/EventBus.cs` (`OnAbilitySlotLockChanged`, `OnDefySealChanged` + payloads +
enums), `scripts/UI/ConfirmModal.cs` (consumer only), `localization/en.csv`.

**Tests.**
- New `tests/unit/LegacyUnlockScheduleTests.cs` **+7** — the milestone table; Story-only isolation
  (the Fighter/Holodeck/Drills/Mirror paths keep full kits); a locked cast is refused; the grid's
  hidden-node gating consults the schedule; the save round-trips; Restart Level preserves unlocks;
  4A's Ultimate beat fires on 4A entry, not L4 exit.
- New `tests/unit/TutorialCalibrationV76Tests.cs` **+8** — the new step order; the grab step's
  persistent dummy shield and the re-prompt; the DI beat's extended tutorial hitstop and the ±15°
  bend; the repeating free launch until a tech lands; tech at **0** block charges; the free launch
  applies no HP loss, no Rally accounting and no rewind charge; the Defy beat spends exactly one use
  and shows lit→broken; the Time Freeze drill is ready on every retry and consumes no charge.
- **Rewrite in place:** `TutorialCalibrationTests`' two existing cases for the new step order
  (**A2 already renamed them; A5 re-orders**). **±0.**
- **Rewrite:** `tests/unit/DialoguePresentationTests.cs` — the two V7.3 hold-to-skip cases pin the
  `IsCompleted` gate and must be rewritten (**±0**), plus **+5** new: any sequence is skippable; the
  first-viewing confirm appears exactly once; a sequence seen on another slot skips with no prompt;
  effects resolve exactly once on both the watched and the skipped path; a hero variant is selected
  by saved hero ID.
- New `tests/unit/CampaignCaptiveRosterTests.cs` **+4** — exactly `rosterCount − 1` captives; the
  active hero is never present; the Leonardo → Cleopatra → Tesla priority is stable; all nine hero
  choices produce a valid pair. **Written count-agnostically** against the manifest (§2.5 / A9
  mandate), never against the literal 9.
- New `tests/unit/MoveListDormantTests.cs` **+3** — a locked Story row shows the dormant badge with
  no frame data; Fighter always shows full stats; the badge names the correct unlock level.
- Extend `tests/unit/StoryHudPresentationTests.cs` **+0** — A8 owns the HUD side.

**Expected count delta: +27** (1735 → 1762 at A5's merge).

**Acceptance criteria.**
- A fresh campaign at Level 1 cannot cast Special 1, Special 2, the Movement Ability or the Ultimate,
  and **can** use basics, block, Rally, the death rewind, Time Freeze, the meter and Defy.
- The same character in Fighter Mode, the hub Holodeck and a Calibration Drill has the full kit.
- Level 0 runs end to end through the new nine-step order with no soft-lock on any step.
- Holding confirm on a never-seen sequence opens exactly one modal; confirming skips and records the
  sequence in the **global** set.
- `CampaignCaptiveRoster` returns eight names for today's roster and would return nine for a
  ten-character roster without a code change.

---

### A6 — Narrative content (V7.5)

**Goal.** Rewrite `localization/en.csv`'s dialogue and level copy, and the `resources/Dialogue/*.tres`
sequences, to the V7.5 fiction: the Unbound / Wardens renames, the retired power-fade arc, the Level
12 knowledge boundary, the L14/L15 retheme, the Mystery Thread with N03 variants and N04 captive
names, plus the hub title, objective strings, the Steam Remote Play Together label and the
CharacterSelect footer.

**Design references.** `design-godot.md` §1–§2 (master 1–225) and §16 (master ~3960–4200).
Contracts: `docs/design-contracts/NARRATIVE_RESOLUTION.md` (N01–N05), `PRODUCTION_SCOPE.md`,
`MULTIPLAYER_DELIVERY.md` (M01). Recon: **A, in full** — it carries the complete key inventories;
**I §8.1–8.5**, which carries the **authored English text** for the rewritten sequences.

**Standing constraint (§2.3):** keys, IDs and scene paths are retained; **only English values change**
— with the single exception of the genuinely new keys listed below. This is what keeps
`ScenePoolConfigTests`, `StoryAudioSetContentTests`, `RosterVfxMappingTests`, `LoadingScreenTests`,
`Level14ContentTests` and `Level15ContentTests` green.

**Exact change list.**

**1. Faction and terminology rename (A1 in recon A).**
- **The Meridian** = the future's governing order (a new proper noun, currently absent).
- **The Unbound** replaces *Apex Archive* / *the cult* / *the Archive*.
- **The Wardens** replace *Chrono-Resistance*; the hub is the **Warden Time-Ship**, *their own vessel
  cut off from the future* — explicitly **no longer hijacked**.
- Mobs are "Unbound shock-troopers"; bosses "Unbound Overseers" (Overseer is retained).
- Elites are **Erasers**.
- **`chrono_warden` / "Chrono-Warden" is retained** (§2.3) — add the lore comment, change nothing else.

Value-only edits, keys unchanged: `boss_archive_prime_name` (145) → **The Forge Sentinel**;
`boss_apex_eraser_name` (146) → **The First Unbound**; `boss_ability_archive_purge_name` (237) →
"Forge Purge"; `boss_ability_archive_remnants_name` (243) → "Borrowed Legacies";
`hub_ship_title` (522) → **THE WARDEN TIME-SHIP**; `hub_interaction_help` (523);
`campaign_level_neo_earth` (636); `campaign_level_alexandria` (637);
`orleans_objective_towers` (661); `gettysburg_objective_arrays` (871);
`lunar_objective_reach_outpost` (892); `neo_earth_level_title` (949);
`neo_earth_objective_defeat_boss` (958) → "Shut down the Forge Sentinel";
`speaker_archive_prime` (963) → **The Forge Sentinel**; `alexandria_level_title` (974);
`alexandria_objective_defeat_boss` (983) → "Defeat the First Unbound!";
`speaker_apex_eraser` (989) → **The First Unbound**.
Also update the non-localized `DisplayName` fields in `resources/Bosses/archive_prime.tres` and
`apex_eraser.tres` to stay in sync (**A7b applies those two `.tres` edits in Wave 2** — A6 does the
`en.csv` half; say so in §9 so they do not drift).

Dialogue value rewrites for faction vocabulary — **28 keys**: `dlg_l00_intro_1`, `dlg_l00_intro_3`,
`dlg_l00_intro_5`, `dlg_l01_entrance_2`, `dlg_l01_boss_1`, `dlg_l02_entrance_2`, `dlg_l02_boss_1`,
`dlg_l03_entrance_2`, `dlg_l04_entrance_2`, `dlg_l04_boss_1`, `dlg_l05_entrance_2`, `dlg_l05_exit_4`,
`dlg_l06_entrance_2`, `dlg_l07_entrance_2`, `dlg_l08_entrance_2`, `dlg_l08_postboss_2`,
`dlg_l10_entrance_2`, `dlg_l11_entrance_2`, `dlg_l11_exit_2`, `dlg_l12_entrance_2`,
`dlg_l12_preboss_1`, `dlg_l12_exit_2`, `dlg_l12_exit_4`, `dlg_l13_entrance_2`, `dlg_l13_exit_5`,
`dlg_l14_entrance_2`, `dlg_l14_exit_4`, `dlg_l15_preboss_3`.

**2. Origin story: the First Strike (A2 in recon A).** The inciting incident is a coordinated blitz
aimed at **people** (history's legends), not at the Library of Alexandria. Extraction beams **crack the
target's nexus moment open**. **Temporal Resonance is time fighting back** — history arming its legend
as the beam cracks the nexus. Not the Unbound's tool (their cradles smother it), not the Wardens' gift
(their held beam only bought the seconds), and explicitly **not "a side effect of your displacement"**.
The Wardens reached exactly **one** strike (the player's); every other legend was taken clean and is
now cradled in the Unbound Bastion. The Warden Time-Ship was **cut off** in the same instant.
`dlg_l00_intro_3` is the canonical statement of the retired fiction and is rewritten wholesale;
`dlg_l00_intro_2` ("...Where am I? The sky... it tore apart.") **survives** — the beam does tear the
sky. `dlg_l15_entrance_2` ("the fracture every rift you have closed was torn out of") is **retired
outright**.
`level_00.intro` goes **6 lines → 7**; the authored English for all seven lines is in **recon I
§8.4**, including the new `dlg_l00_intro_7`. New `SpeakerNameKeys` = sarah, player, sarah, player,
sarah, sarah, sarah. The three parallel `PackedStringArray`s in
`resources/Dialogue/level_00_dialogue.tres` (`SpeakerNameKeys`, `LineKeys`, `EmotionKeys`) must be
edited in lockstep — `CampaignLocalizationTests` pins speakers == lines and `Level00ContentTests`
enumerates the exact list.
Note `dlg_l00_intro_2` now names the hero's own nexus ("Princeton? Orléans?") — that is a
**per-character line** and uses A5's hero-variant mechanism (`level_00.intro@<hero>`), not one shared
string.

**3. No mid-campaign power loss (A3 in recon A — the biggest single content item).**
> "The power is history's own, held in trust — and it **grows** across the campaign … There is **no
> mid-campaign power loss**; the hero surrenders the charge exactly once, by choice, at the very end."

The retired arc is a deliberate **per-level three-beat structure** in the exit dialogue of essentially
every level. **Rewrite ~40 values**: `dlg_l01_exit_3/4/5`, `dlg_l02_exit_3/4/5`, `dlg_l03_exit_3/4/5`,
`dlg_l04_exit_3/4/5`, `dlg_l06_exit_3/4/5`, `dlg_l07_exit_3/4`, `dlg_l08_postboss_3/4`,
`dlg_l09_exit_3/4/5`, `dlg_l10_exit_3/4/5`, `dlg_l11_exit_3/4/5`, `dlg_l12_preboss_2..5`,
`dlg_l12_exit_3`, `dlg_l13_boss_intro_1/2`, `dlg_l13_exit_3/4`, `dlg_l14_exit_3/4`.
`dlg_l15_ending_3` and `dlg_l15_ending_4` **survive** — they are the single end-of-game release — and
are re-worded for the total release.
**Hold line counts constant wherever possible** so the `.tres` sequences need no array edits; each
level's content test pins `sequence.LineKeys.Length == SpeakerNameKeys.Length` and the per-level
suites enumerate the exact key lists. Where recon I §8.4 authors a count change (notably
`level_01.exit` 5 → 4, retiring `dlg_l01_exit_5`), edit the arrays and the per-level content test
together.
**`dlg_l13_boss_intro_1` is structurally load-bearing** — the Mirror Paradox's entire
characterisation is "I am the resonance you shed", which the new fiction removes. Recon I §8.4
proposes: *"You are the one that got away. They modelled you a thousand times to find out how — and I
am the model that kept getting up."* The encounter code is unaffected; only the two intro lines and
`dlg_l13_exit_1/3/4` change.

**4. L14 / L15 retheme (A5 in recon A).**
- **L14 = the Unbound Bastion**, a fortress in the space between timelines containing the
  **Extraction Cradles** (the captive roster plus rows of lesser figures receding into the dark) and
  the **Anchor Forge**. The player destroys the Forge's **intake** but **cannot free the captives** —
  severing a charged cradle consumes the captive; only killing the machine releases them.
- **L15 = the Meridian Founding**, reached by **riding the Forge's own firing channel** (the channel
  exists only while it fires, which is why the fight happens *during* the firing). The First Unbound
  tries to seat the **Prime Anchor** by force. Victory shatters the half-seated Prime Anchor → total
  release.
- **The Prime Anchor's polarity has inverted.** In the repo it is history's restoration stone the hero
  fills with the Temporal Core; in V7.5 it is the *Unbound's weapon* aimed at rewriting the Meridian
  Founding, which the hero shatters. **Same words, opposite meaning — do not assume the existing copy
  is salvageable.**
- **`dlg_l14_exit_2` is a hard contradiction**: it currently says "there is nobody down there to bring
  home… no locals, no coerced crews, nothing to restore", directly against the Extraction Cradles
  canon. `dlg_l14_entrance_1` says "not one person in it who was ever taken". **Call these out in §9
  rather than softening them**, then rewrite both.
- Value rewrites, keys unchanged: `neo_earth_level_title`, `neo_earth_room_*` (4),
  `neo_earth_objective_*` (6), `neo_earth_pocket_*` (3), `alexandria_level_title`,
  `alexandria_room_*` (4), `alexandria_objective_*` (6), `alexandria_anchor_dormant`,
  `alexandria_anchor_ready`, `alexandria_interaction_insert_core`.
- **New sequence `level_14.extraction_hall`** — 5 lines, speakers player, sarah, first_unbound,
  player, first_unbound; the authored English for all five (`dlg_l14_hall_1..5`) is in recon I §8.4.
  It triggers on entering the Extraction Hall, **before** the final approach to the Forge core.
- `level_15.entrance` / `preboss` / `boss_intro` / `postboss` / `ending` are **fully rewritten**; the
  authored English for `level_15.ending`'s six lines is in recon I §8.4.
- New keys likely needed: `neo_earth_room_extraction_hall`, the cradle-sever refusal line,
  `alexandria_room_firing_channel`, and the N01 "Seal Timeline — Complete Level" prompt.
  **`Level14ContentTests.cs:149-155` and `Level15ContentTests.cs:164,183` enumerate key names** — a
  values-only rewrite leaves them green, but **adding or removing a room/objective key does not**.
  Update those two suites in the same change.

**5. Knowledge boundary (A6 in recon A).** *No dialogue line, level brief, or UI string may state any
Villain's-True-Plan fact before Level 13.* Acts I–II present only the Wardens' honest, mistaken read
("displaced like you — lost in the rifts"). **Level 12 reveals *where*** (location + captivity +
draining, **not** the Forge, the deficit or the rewrite); **Level 14 reveals *why***.
`dlg_l12_preboss_2` and `dlg_l12_preboss_4` currently state the entire **retired** endgame in advance
and are rewritten to N04's Moon reveal — authored English in recon I §8.4, including the
`{CaptiveName1}` / `{CaptiveName2}` tokens.
**Add a guard:** new `tests/ContentValidation/NarrativeKnowledgeBoundaryTests.cs` sweeping
`dlg_l0*` – `dlg_l12_*` values for the forbidden proper nouns — `Anchor Forge`, `Prime Anchor`,
`Extraction Cradle`, `Meridian Founding`, `the Landing`, `deficit`. Cheap, pure-C#, and the only
mechanical way to keep a knowledge boundary from eroding. **One `[TestSuite]` per file.**

**6. Mystery Thread absence beats + N03 recognition (A7/A8 in recon A).** Six level cards gain an
authored **Absence** line: Florence/Leonardo, Orléans/Joan, Chicago/Tesla, Alexandria/Cleopatra,
the Globe/Shakespeare, Gettysburg/Lincoln. When the active hero **is** that level's central legend,
the absence report is replaced by **brief recognition** using A5's hero-variant mechanism. The six
authored recognition lines (master 163, 169, 175, 196, 207, 213):
- Florence — **Apprentice:** *"Maestro! We need your help at the workshop."*
- Orléans — **Captain:** *"Joan! The line needs you at the banner."*
- Chicago — **Engineer:** *"Mr. Tesla! Help us bring the lights back."*
- Alexandria — **Guard:** *"Your Majesty! Your people need you."*
- The Globe — **Player (of the company):** *"Master Shakespeare! The company needs you."*
- Gettysburg — **Union officer:** *"Mr. President! We feared you would never reach us."*
Branch selection is from the **saved campaign hero ID vs the level's authored central-legend ID** —
never from localized names. Variant IDs are distinct strings (`level_01.entrance@leonardo`).
Add a `CentralLegendID` string to the level side.
**The non-roster levels (Paris, Titanic/L5, Pompeii, Nassau, Berlin, Lunar) get no pairing** — N03:
"Shared levels without a central playable-roster legend: retain the existing mission dialogue." They
take an **outsider observation** instead (recon I §8.4 proposes the Paris and Pompeii replacements).
New speaker keys: `speaker_apprentice`, `speaker_captain`, `speaker_engineer`, `speaker_guard`,
`speaker_player_company`, `speaker_union_officer`. **`DialogueChirpPitchTests` assigns a per-speaker
pitch — every new speaker needs an entry.**
`resources/Dialogue/level_0{1,2,3,8,10,11}_dialogue.tres` each gain the two sequences;
`Level02ContentTests.cs:249` asserts `set.Sequences.Length == 3` and every sibling suite does the
same — bump each count and pin both variants.
`CampaignLocalizationTests` floors (`dlg_l*` ≥ 160, sequences ≥ 48, `speaker_*` ≥ 10) are minimums —
additions are safe.

**7. N04 captive names.** New `captive_name_*` family (`captive_name_leonardo,Da Vinci`, etc.),
**distinct from** the existing `character_*` display names because Leonardo's spoken name differs.
Three return-shot keys for the ending montage. A5 ships the resolver; A6 authors the rows.

**8. Level 8 post-boss rewrite.** `level_08.postboss` is the heaviest Act II rewrite: 4 lines → 4, but
the **speakers change** from (cleopatra, player, cleopatra, player) to (player, sarah, player, sarah).
Authored English in recon I §8.4. `speaker_cleopatra` is retained but becomes reachable **only in the
N03 hero-is-Cleopatra variant**.

**9. Hub, act boundaries, Steam label.**
- `hub.sarah_briefing` grows to per-act variants plus Medic Okafor's Observation Deck lines reacting
  to the chosen character at each act boundary: new sequences `hub.sarah_act1/2/3`,
  `hub.okafor_act1/2/3`, plus the act-boundary missing-legend reports. Text is not authored in the
  master — A6 proposes and records the proposals in §9 as `[DERIVED]`.
- The act-boundary portal gate (before Level 1, after Level 5, after Level 12) is unchanged in rule;
  **verify whether `HubWorldController` implements it at all** — recon B §B11 found the portal,
  Repository and auto-deposit but **no act-boundary gate**. If absent, record it in §9 as a gap; do
  not build it here.
- **A11 Steam Remote Play Together (M01).** One new key on the Fighter local-play surface, e.g.
  `fighter_remote_play_notice,"Play online with a friend through Steam Remote Play Together — they
  join your local match from their own machine."` Placement: the `scenes/menus/CharacterSelect.tscn`
  footer, which already hosts the Move List / Systems Card buttons and a 5-entry focus chain. Store
  the **raw key** as the control's `text` per the repo convention. If a focusable control is added,
  re-run `FocusChainBuilder.Apply` and update `CharacterSelectSceneTests`' focus-chain pin. Steam
  integration proper is **out of scope** (§8) — Steamworks is not installed and the feature works
  without an in-game hook, so the entry ships as a label.

**10. Level 15 ending variants (N05).** Two endings selected by the 15-level Integrity average.
A6 authors the **second `dlg_l15_ending_6` variant** (the Prime Anchor's visible scar at <50%) and
the clean variant; **A3b owns the selection logic** and the unrounded 750-point comparison. Say so
in §9.

**Files owned (exclusive).** `localization/en.csv` **dialogue, speaker, level-title, room and
objective rows** (A6 is the only workstream permitted to delete or rewrite those rows — §2.11), all
17 `resources/Dialogue/*.tres`, `scenes/menus/CharacterSelect.tscn` (**footer label only**).

**Shared files touched additively.** `localization/en.csv` (A6's own marker block for the genuinely
new keys), the six per-level `Level0NContentTests` that enumerate sequence counts,
`Level14ContentTests` / `Level15ContentTests` key lists,
`tests/unit/DialogueChirpPitchTests.cs` (new speaker pitch entries).

**Tests.**
- New `tests/ContentValidation/NarrativeKnowledgeBoundaryTests.cs` **+1** (a single sweeping case).
- Extend `tests/ContentValidation/CampaignLocalizationTests.cs` **+2** — every `captive_name_*` and
  every new `speaker_*` resolves through the **compiled** translation; every hero-variant sequence ID
  resolves.
- Update in place: the six per-level content suites' sequence counts (3 → 5 where variants land),
  `Level00ContentTests` (6 → 7 lines), `Level14ContentTests` (+ the extraction-hall sequence),
  `Level15ContentTests`. **±0 each** except `Level14ContentTests` **+1** for the new sequence.
- Extend `tests/unit/DialogueChirpPitchTests.cs` **+1** — the six new speakers all have pitch entries.
- Extend `tests/unit/CharacterSelectSceneTests.cs` **+1** — the Remote Play notice renders its raw key
  and the focus chain is re-applied.

**Expected count delta: +6** (1762 → 1768 at A6's merge). The work is overwhelmingly value churn, not
new assertions.

**Expected-to-fail-until-import (§2.11):** `CampaignLocalizationTests`, `SceneVisibleTextTests`,
`ScriptTranslationKeyTests`, `UnusedTranslationKeyTests`, and every per-level content suite that
resolves a key. List them in the handoff.

**Acceptance criteria.**
- `grep -i "Apex Archive\|Chrono-Resistance\|the cult\|cultists"` over `localization/en.csv` returns
  nothing.
- No `dlg_l0*`–`dlg_l12_*` value contains `Anchor Forge`, `Prime Anchor`, `Extraction Cradle`,
  `Meridian Founding`, `the Landing` or `deficit` (the new guard test is the pin).
- No surviving line asserts that the hero's resonance fades mid-campaign.
- Every `.tres` sequence's three parallel arrays remain equal length.
- Every key **name** in `en.csv` that existed before this change still exists after it, except the
  five A6 explicitly retires (and those are recorded as orphans or deleted with their consumers).

---
### A8 — HUD, presentation and comfort settings

**Goal.** Rewrite the Story HUD scene to the HUD_CONTRACT hierarchy, give both HUDs two status slots,
add the Defy seal / Time Freeze indicator / anchor pips / slot lock overlays / persistent dust
counter, implement the F24 ownership-outline split, and ship C01a (Reduced Temporal Effects), C01b
(prioritized audio) and C01c's direct binding slots including explicit Unbound.

**Design references.** `design-godot.md` §7 (master 3032–3462), §8 audio snapshots, §9 shader/glow
tables, §12 settings. Contracts: `docs/design-contracts/HUD_CONTRACT.md` (F24, read in full),
`COMFORT_SETTINGS.md` (C01a/b/c). Recon: **H, in full**, **E §3, §7**, **I §4.1, §5.1, §6.1**,
**D §8.6 (D-15)**, **C §C-16**.

**Current state.** `scenes/ui/StoryHUD.tscn` is
`CanvasLayer/Root/{TopLeft,Portrait,Vitals,BossPanel,CheckpointToast}` — a single left-hand `Vitals`
VBox of `ProgressBar`s and `Label`s. **There is no `SafeArea`, no `TopLeft_Panel`, no
`TopRight_Panel`**, and four HUD elements the design names as authored nodes (the Integrity readout,
the Rally echo band, the boss intro card, the rewind-cooldown pip) are **built in code at runtime by
`StoryHUD.ResolveUI()`** with comments saying "built in code so the authored scene stays untouched".
V7.6 adds seven more named nodes. **This is a scene rewrite, not an edit, and it is the single
largest item in this workstream.**
**Both status systems already carry two slots; only the HUD collapses them.** Story:
`StatusController` has `_control` / `_damage` plus `IsDamageStatus`. Fighter:
`FighterRuntimeComponent` (`FighterSimulationComponents.cs:287-386`) bit-packs **both** slots
(`StatusType`/`StatusFrames`/`StatusIntensity` for control, `DamageStatusType`/`DamageStatusFrames`/
`DamageStatusIntensity` for damage) and exposes `PresentedStatusType` **purely as a single-pip
collapse helper**. So F24's two-slot HUD needs **zero new simulation state and no Klotho component** —
that struct is at exactly 128 bytes and cannot grow. Stop calling `PresentedStatusType`; read both
pairs.
`GlowPresentationController.SetSlotIndicator(playerIndex)` (line 212) **pushes the slot colour into
the same arbitrated outline stack** as status/armor/spawn, so any effect outranks and replaces it —
exactly what F24 Option A forbids. `assets/shaders/outline_glow.gdshader` has exactly four uniforms:
`outline_color`, `outline_thickness`, `glow_intensity`, `pulse_speed`; canvas shaders have **no
`instance uniform` support** under `gl_compatibility`.
`scripts/Core/AudioSnapshotMixer.cs` documents *"Snapshots stack **additively**: Pause plus Ultimate
ducks music by the sum. Filter cutoffs stack by **minimum**."* — exactly what C01b forbids. Its
definitions: `Pause(-8, -12, **+2**, 900, 1200, 0.20)`, `LowHealth(-2, 0, 0, 3200, ∞, 0.60)`,
`Ultimate(-12, -12, 0, ∞, ∞, 0.15)`, `Rewind(-12, 0, 0, 1800, ∞, 0.30)`. The `AudioSnapshot` enum has
only **four** members. `resources/Audio/default_bus_layout.tres` is
Master(limiter) → Music(LPF), SFX(LPF) → {Combat, Movement, Environmental}, UI — **no Critical Cues
bus, no Dialogue bus**.
`InputBindingSet.Normalize()` **drops any action row whose event list is empty**
(`if (events.Count == 0) continue;`), so **explicit Unbound is unrepresentable** — a hard blocker on
the C01c save shape (§2.6).
`GlobalSaveData` carries `ScreenShakeScale`, `HudOpacity`, `DamageNumbersVisible`, `UiScale` —
**no** `ReducedTemporalEffects`.

**Exact change list.**

**1. Rewrite `scenes/ui/StoryHUD.tscn` to the HUD_CONTRACT tree**, keeping the repo extras as named
siblings:
```
HUD_CanvasLayer
└── SafeArea
    ├── TopLeft_Panel
    │   ├── PlayerPortrait (TextureRect 64×64)
    │   ├── HealthBar_BG (200×20) → HealthBar_Fill, RallyEcho_Band
    │   ├── BlockCharges_Panel (HBox, ShieldIcon_1..3, 20×20)
    │   ├── StatusEffects_Panel (fixed slot positions)
    │   │   ├── DamageStatus_Indicator (28×28) → DamageStatus_DurationRadial
    │   │   └── ControlStatus_Indicator (28×28) → ControlStatus_DurationRadial
    │   ├── UltimateMeter_BG (40×40) → UltimateMeter_Fill (radial), UltimateMeter_LockOverlay
    │   ├── DefySeal (20×20)
    │   ├── RewindCounter (HBox: RewindIcon 32×32, RewindCountText)
    │   ├── TimeFreezeIndicator (32×32 + remaining label)
    │   ├── BeaconAnchors (HBox: BeaconIcon 24×24, up to 3 AnchorPips)   [Act III only]
    │   ├── Cooldown1_Icon (36×36) → Cooldown1_Radial, Cooldown1_LockOverlay
    │   ├── Cooldown2_Icon (36×36) → Cooldown2_Radial, Cooldown2_LockOverlay
    │   └── Cooldown3_Icon (36×36) → Cooldown3_Radial, Cooldown3_LockOverlay
    └── TopRight_Panel (VBox)
        ├── CurrencyContainer (HBox: ChronalDustIcon, ChronalDustText, DustPickup_Float)
        └── IntegrityClock (48×48) → ClockFace, ClockWedge, SiphonStreams, IntegrityText
```
**Ruling on node naming (recon H open question 1):** adopt the contract names. Renaming breaks every
scene test's paths **once**; a permanent deviation mapping is worse. A8 updates the paths in
`StoryHudPresentationTests`, `StoryHudLocalizationTests` and `SceneVisibleTextTests` in the same
change. The repo extras — `Root/TopLeft/{LevelTitle,Objective}`,
`Root/BossPanel/{BossName,BossBar/PhaseNotches}`, `Root/CheckpointToast` and the code-built
`BossIntroCard` — are **kept as named siblings under `SafeArea`** and recorded as accepted deviations
in the ledger (they are shipped behaviour the contract simply does not enumerate).
Fold the four code-built widgets out of `StoryHUD.ResolveUI()`: `EchoBand`, `IntegrityLabel` and
`BossIntroCard` become authored nodes; `RewindCooldownPip` is **already deleted by A2** — verify it is
gone and do not reintroduce it.

**2. Two status slots, both modes.** `HudAbilityIndicatorModel` currently models **one** status
(`ActiveStatus`, `StatusRemaining`, `ClearStatus()`); give it a two-slot shape keyed by
`StatusController.IsDamageStatus(payload.Type)` — **derive the slot at the HUD** (§2.9), do not add a
slot field to `StatusEffectPayload`. `OnStatusEffectCleared` also carries `Type`, so clears route the
same way. Delete `StatusController.ClearSlot`'s "re-announce the survivor" hack once two pips exist
(A1 left it in place for this handoff).
Story: two **28 × 28 px** slots below block charges, damage left / control right.
Fighter: two **24 × 24 px** slots beside each player's HP bar, damage left / control right,
**including P2 — never reverse their semantic order.**
Hide an empty slot's icon and radial while **retaining its reserved position** (a fixed-position
`StatusEffects_Panel`, so hiding one `TextureRect` does not shift the other). Show both occupied slots
simultaneously; **never** choose a single winning HUD icon based on outline priority.
Use distinct glyphs plus localized names — colour alone must not identify an effect.
The damage slot covers Venom and Radiant Burn; the control slot covers Time Dilation, Static Charge,
Root and Story-only Suppression. **Tesla's Conductive mark stays outside both slots** (A1 exposes it;
A8 draws the separate non-pulsing circuit glyph near the target's health display).

**3. Radials.** No radial widget exists anywhere in the repo — all readiness is `ProgressBar`. Use a
`TextureProgressBar` in radial fill mode or a small `_Draw()` control; the pattern exists in
`scripts/UI/BossPhaseNotchOverlay.cs` and `scripts/Environment/SequenceGlyph.cs`.
**F24 refresh rule:** radials follow **authoritative simulation clocks, including pauses and Time
Freeze**; the HUD must **not** independently count down a frozen status. `HudAbilityIndicatorModel.Tick`
currently counts down locally as a "presentation fallback" — under Time Freeze that becomes wrong.
Replace it with an authoritative remaining-duration read from `StatusController`.

**4. Subscribe to the §2.9 events.** A8 consumes, never polls:
`OnTimelineIntegrityChanged` (A3) → the Integrity clock face, wedge, tier cracks, the <20% audible
tick, the pause dim and the PreBoss seal flourish;
`OnCollapseTremorChanged` (A3) → the HUD tick and the shared crack-glow overlay;
`OnTimeFreezeStateChanged` (A2) → the `TimeFreezeIndicator`'s Ready / active 5 s countdown / 45 s
cooldown countdown, plus a brief thaw warning at ~1 s remaining. **Its readiness is independent of
the death-rewind count; never attach its cooldown to the rewind counter.**
`OnAbilitySlotLockChanged` (A5) → `Cooldown1..3_LockOverlay` and `UltimateMeter_LockOverlay`, showing
**Dormant** (dim star that ignites on the Resonance Restored beat), **Suppressed** (cold cross-out for
the status duration) or **Clear**. **Locked slots are shown, never hidden.**
`OnAnchorChargesChanged` (A3b) → `BeaconAnchors` gold pips beside the rewind counter, data-driven
count using the same pattern as the stock pips. Act III only.
`OnDefySealChanged` (A5 in Wave 1, A1b in Wave 2) → the four-state `DefySeal`, Story **and each
fighter's meter in Fighter Mode**, CPU matches included, mirrored for P2.
`OnRallyEchoChanged` (A1b, or polling if no event exists) → the `RallyEcho_Band` plus the reclaim
flash.

**5. The Defy seal (F13), exactly.** 20 × 20 px, **beside** the Ultimate meter. Four states:
**Unused/not ready** (intact unfilled outline, meter < 100); **Ready** (intact filled + steady
warm-gold, unused and meter full); **Spent** (visibly broken at any meter value after the proc —
refilling never repairs); **Unavailable** (barred; mode-disabled including Sudden Death, or the
fighter is dead — takes precedence **without clearing the spent flag**).
**Shape and fill must distinguish states, not colour alone.** No pulsing, no extra ready chime.
Localized labels `Defy: Not Ready / Ready / Spent / Unavailable` plus Systems Card copy. Scales with
the 90–140% UI setting and HUD opacity. State is **derived from authoritative meter / Defy-used /
life / mode after the complete gameplay update**, recomputed on gain, spend, drain, proc,
death/respawn, load and **rollback**; never persisted as a separate cosmetic flag; a load must never
replay the proc. Suppression and a locked/unlearned Ultimate do **not** disable Defy. F04 puzzle
authorization cannot light it at zero meter.
**A8 also adds the post-rollback recompute hook** — call the seal model after
`FighterSimulationDriver`'s post-rollback sync, not only on the event.

**6. Persistent dust counter + `+N` float.** The top-right Chronal Dust counter stays visible during
active Story-level HUD display, **including at zero and during the boss fight after the Integrity
clock hides**. Bind it to the current level's **undeposited `levelChronalDust`**; previously deposited
spendable dust stays separately identified in the Repository/Beacon — **never display a combined
balance**. Only the pooled **`+N`** pickup notification rises and fades after **one second**; the
numeric balance never fades. Update the balance from **committed wallet changes** on pickup, fee,
restart, recovery, load and completion; a mirrored event or a rollback must not add dust again.
Pickup notifications are cosmetic and cannot drive the balance.
The trigger already exists: `ChronalDustPickup.Collect()` is the single wallet-commit point and
already raises `OnDustAwardCollected`.
Current repo state to correct: dust lives in the **left** `Vitals` VBox as `Root/Vitals/DustLabel`
using the wrong key family (`hub_carried_dust`) from `StoryManager.ChronalDustCollected`, with no icon
and no float.

**7. Rewind counter.** Icon + `×N`, **red pulse at 1**, **empty hourglass at 0 with no alarm**.
`hud_rewinds` stays.

**8. Fighter HUD.** Two 24 × 24 status slots (above). `DefySeal` beside each meter, mirrored on P2.
**F21 stock display:** in **Stock** mode keep the finite pips; in **Time** mode replace them with a
localized **"Stocks lost: N"** label under each HP bar, both counters starting at 0 — **never** label
it KOs scored / points / remaining lives. New key `fighter_hud_stocks_lost,Stocks lost: {0}`.
`MatchResults` shows both final regulation totals.
**Timer:** `00:10` red pulse + chime; in Sudden Death the timer area reads
**"Sudden Death — next death loses"** instead of a running clock — a `FighterHUD.ApplyMatchState`
change plus one new key. The existing `FighterOverlayModel` `SuddenDeathStamp` banner
(`fighter_sudden_death_stamp`) stays.
Node naming: `Root` → `SafeArea`, `Root/PlayerOne` → `P1_Status`, `Root/PlayerTwo` → `P2_Status`,
consistent with the Story ruling. The repo extras (`Body/Column/Cooldowns`, four code-built 56×34
plates) stay and are recorded.

**9. F24 ownership outline.** Add **three new shader uniforms** to
`assets/shaders/outline_glow.gdshader`: `_OwnerOutlineColor`, `_OwnerOutlineThickness` (float, 1.0,
independent of effect thickness and pulse) and `_OwnerOutlineEnabled`, plus a **second composite pass
that draws the owner edge AFTER the effect edge**. P1 **#00f0ff**, P2 **#ff3366**, one-pixel reference
thickness, static intensity.
Split `GlowPresentationController` into (a) an **ownership channel** set once from match slot
identity, re-applied on spawn and rollback, **never popped**, and (b) the **effect stack** with
priority **spawn protection → armor → control status → damage status**. Re-point `SetSlotIndicator`
at the ownership channel. Damage, statuses, armor, invulnerability, ability decoys and an expiring
effect **cannot recolor, pulse, disable or replace** the ownership edge; do not paint a second opaque
effect-coloured outline over it. `PointLight2D` uses the **secondary** effect colour/intensity and
never changes the ownership edge.
Move the spawn-invulnerability glow onto `FighterStateComponent.InvulnerabilityFrames` rather than a
presentation timer, and make it **not** fire in Sudden Death (**F22: Sudden Death starts without spawn
protection**). D04's post-Defy protection uses the same secondary invulnerability-glow layer and its
actual remaining timer; **the F13 seal stays broken during and after that window**.
`gl_compatibility` forbids `instance uniform`, so per-actor isolation means **per-actor
`ShaderMaterial` instances** — **measure batching and memory**; the design explicitly stops claiming
parameter changes are free. Keep each actor's visual state isolated: **do not mutate a shared material
resource so that changing one actor recolors another.**
Story entities keep their existing effect outlines; Story Suppression still smothers the gold aura
(A1's channel).

**10. C01a Reduced Temporal Effects.** One toggle in **Settings → Gameplay, beside Screen Shake**,
default **Off**, help text *"Reduces distortion, after-images and screen flashes. Keeps gameplay cues
and timing unchanged."*
`GlobalSaveData.ReducedTemporalEffects` (bool, default false) + `Normalize()`; a missing legacy value
defaults Off **without** overwriting an explicit value or inferring from Screen Shake.
Add `Gameplay/ReducedEffectsToggle` to `scenes/ui/Settings.tscn` and wire it in `SettingsMenu.cs`
(the existing Gameplay tab binds `HapticToggle`, `HapticSlider`, `DamageNumbersToggle`,
`HudOpacitySlider`, `ScreenShakeSlider`, `UiScaleSlider`; apply/read at lines ~478–503).
Add a single static read point `FTT.Core.ComfortSettings.ReducedTemporalEffects` consumed by
`RewindPresentationOverlay`, `GlowPresentationController`, `VfxEmitter`, `LoadingScreen`,
`FighterPresentationOverlay`, `CameraShake` and the Relativity Rift zone visual.
**Applied before the first loading/portal effect** — `ViewportEnforcer` is the last autoload and
already applies display settings, but the loading screen is built by `GameManager.LoadScene`, so the
read must be available there.
Applies **live**: clear already-emitted decorative after-images on enable; switching Off resumes only
the current event's remaining presentation and must not replay old flashes. No bright auto-preview.
Reduced treatment: kill chromatic aberration / RGB split / lens warp / high-frequency scanlines
(**including Relativity Rift, Death Rewind and portal/loading**); remove decorative ghost trails and
after-images (**preserve the single fixed Echo Step destination indicator** and real decoys); replace
full-screen KO/Ultimate/rewind/transition flashes with a restrained stable overlay or smooth fade
(**never another full-screen flash**); disable full-scene desaturation and pulsing vignette/flicker
(**preserve readable crack lines, platform warnings and static danger areas**); steady glow with a
smooth expiry fade for armor/status/spawn/Defy feedback (**keep the Fighter ownership outlines, both
status slots and the spent Defy seal**); retain attack-class glyphs, timing and the Special-class
projectile signature; clean portal shape and a static image for loading and cinematics.
**The preset takes precedence over per-ability presentation requests** ("maximum chromatic
aberration", full-screen flash) for all nine kits, bosses and items; no Ultimate or cutscene bypasses
it. Graphics quality settings must respect it rather than re-enabling an expensive pass.
**Hard invariants:** gameplay timing, hitstop, slow motion, camera framing, warning durations and
resource clocks unchanged; Defy's 60-tick protection and Time Freeze's 5 s / 45 s untouched; Screen
Shake stays independent (0 still disables); **stored and applied locally, outside match
snapshots/hashes** — it cannot influence hit eligibility, targeting, random draws or timers; reconcile
reduced visuals after rollback **without regenerating suppressed effects**.
New keys `settings_reduced_temporal_effects` and `settings_reduced_temporal_effects_help`.

**11. C01b one prioritized background audio treatment.** Convert `AudioSnapshotMixer` from an
**additive stack** to a **priority selector**: keep the request set, resolve the single
highest-priority active profile, blend gain/filter/pitch targets to **that profile alone**, and
**re-evaluate active requests when a profile ends** rather than blindly restoring Normal.
Priority, highest first: **Paused → Death Rewind / Collapse / Anchor Snap → Ultimate cinematic →
Defy / boss phase / scripted → Time Freeze → Low Integrity / Collapse Tremor → Low health →
Underwater → NormalGameplay.**
"Ultimate over LowHealth uses the Ultimate treatment alone; a prior 12 dB rewind duck cannot make it
24 dB."
**Remove the Pause `+2 dB` UI boost** — explicitly forbidden ("UI/dialogue stays clear at the user's
configured level, **without automatic boost**").
Extend the enum with `DefyOrScripted`, `TimeFreeze`, `LowIntegrityTremor`, `Underwater`,
`AnchorSnap` — a priority row with no authored treatment adds none, so empty definitions are legal
placeholders.
Bus layout: add a **CriticalCues** bus under SFX (a sibling of Combat/Movement/Environmental, **not**
carrying the background LPF) and a **Dialogue** bus under UI. Route attack-class and hazard warnings,
impending-platform-danger cues, freeze/thaw and recovery/phase cues, timer-danger cues and confirmed
result announcements onto Critical Cues; route the dialogue chirp
(`placeholder_sfx_dialogue_chirp.tres`) to Dialogue. Update `AudioBuses` constants.
Shared local play uses **one global background selection** (two low-health fighters cannot double the
duck). Select or blend **one** environmental reverb send, never stack. Do not restart the music clock
or stems on a priority change. Derive requests from authoritative state, apply **locally outside
snapshots and hashes**, and on load/scene-change/rollback reconcile **without replaying profile-entry
stingers**.
All new cues ship as **digital silence** under the standing placeholder rule
(`resources/Audio/README.md`) — do not "fix" the silence by adding tones.

**12. C01c direct binding slots including Unbound.** The remapping screen lists **every** gameplay
action — Left, Right, Up, Down, Jump, Roll, Block, Basic Attack, Special 1, Special 2, Movement
Ability, **Ultimate**, Interact, **Grab**, **Echo Step**, **Time Freeze** (labelled Story-only),
Pause — **17 rows**, with **one editable direct-binding override per device kind** (keyboard/mouse
separate from gamepad), plus **On/Off flags for the fixed preset shortcuts**.

| Action | Direct default | Optional preset shortcut (On/Off per device kind) |
|---|---|---|
| Ultimate | Keyboard `U`; gamepad direct slot **Unbound** | Gamepad MovementAbility + Special2 (LB+RB at defaults), **on by default**; no new keyboard Ultimate chord |
| Grab | Direct slots **Unbound** (retain an existing explicit user bind) | Block held + BasicAttack press, on for keyboard and gamepad |
| Echo Step | Direct slots **Unbound** | Block + Roll under the existing timing/recovery rule, on for both |
| Time Freeze | `R` / Back-or-Select, preserving **migrated** user overrides | none; Story-only label |

**A8's half is the save shape and the screen**; **A1c owns the two new InputMap actions, the wire
bits and protocol v3.** A8 must:
- Make `InputBindingSet` able to express **explicit Unbound** — `Normalize()` currently deletes empty
  rows, so add either a sentinel event kind or a parallel `HashSet<string> UnboundActions` that
  survives normalization — plus per-device shortcut flags
  (`Dictionary<string, bool>` keyed `action|deviceKind`).
- Move `Ultimate` from `InputManager.ReadOnlyActions` into `RemappableActions`.
- Add the three new rows to `InputBindingService.RestorableActions` (built from `RemappableActions`,
  so a missed addition silently drops restores).
- Rules: a direct bind and an enabled shortcut **coexist**; disabling a shortcut leaves its components
  working normally; assigning a direct bind must **not** auto-disable the shortcut. Conflicts reject
  duplicates between simultaneously available actions on the same device, **naming** the conflicting
  action; a preset's intentional sharing with its components is **not** a duplicate error; never
  accept an ambiguous duplicate because an action is currently locked in the campaign. A device
  profile must retain a reachable route for required actions — explain and keep the last valid
  configuration rather than leaving Ultimate/Grab/Echo Step unreachable.
  Locked / not-yet-unlocked actions are **shown for configuration** without granting the ability.
- Per-action reset restores that action's direct default **and** its preset flags for the selected
  device; global reset restores the whole profile; validate the whole proposed profile before
  committing.
- Legacy missing flags **reproduce the existing shortcuts** (default On).
- Prompts: prefer the **direct binding** in a single-action prompt, otherwise the enabled preset's
  full current chord; describe Echo Step and Grab **by action name**, never hard-coded
  "Block+Roll"/"Block+Attack" text. `DialogueManager.DescribeInteractBinding()` is the precedent.
  New keys: `controls_action_grab`, `controls_action_echo_step`, `controls_shortcut_*`,
  `controls_unreachable_action`.

**Files owned (exclusive).** `scenes/ui/StoryHUD.tscn`, `scripts/UI/StoryHUD.cs`,
`scenes/ui/FighterHUD.tscn`, `scripts/UI/FighterHUD.cs`, `scripts/UI/FighterHudModel.cs`,
`scripts/UI/HudAbilityIndicatorModel.cs`, `scripts/UI/MatchResults.cs`,
`scripts/UI/LevelResultsPanel.cs` (**everything except A5's Resonance Restored beat**),
`scripts/UI/SettingsMenu.cs`, `scenes/ui/Settings.tscn`, `scripts/Core/ComfortSettings.cs` (new),
`scripts/Core/AudioSnapshotMixer.cs`, `scripts/Core/AudioBuses.cs`,
`resources/Audio/default_bus_layout.tres`, `assets/shaders/outline_glow.gdshader`,
`scripts/UI/UIPalette.cs`, `scripts/Core/InputBindings.cs` (**the Unbound + shortcut-flag shape only;
A2 landed the action rename first, A1c adds actions in Wave 2**).

**Shared files touched additively.** `scripts/Core/SaveManager.cs` (`ReducedTemporalEffects` +
the C01c binding flags), `scripts/Core/EventBus.cs` (consumer only), `localization/en.csv`
(~30 new keys under `# Package 11 A8`; **A8 owns the deletion of `results_rating` (line 1021)**),
`scripts/Combat/GlowPresentationController.cs` (**A1 owns this file in Wave 1** — A8's F24 split lands
as a follow-up commit after A1 merges; state the ordering in §9),
`scripts/Core/StoryManager.cs` (**region: the Chronal Rating removal only** —
`LastLevelChronalRating` at lines 270/289 and the `save.RatingByLevel[levelID]` write at 619).

**Chronal Rating retirement (ruling 2.A).** Remove `RatingLine` from `LevelResultsPanel` (lines
171–187), the `results_rating` key, `StoryManager.LastLevelChronalRating` and the `RatingByLevel`
write. **Leave the `StorySaveData.RatingByLevel` field in the payload as dead data** — removing it is
a breaking change this package is not taking (§2.6). `ChronalRatingRules` itself is deleted by **A3**.

**Results itemisation (H-29, F05).** Six categories against the repo's three: required enemies, boss,
Extractors, secret/other optional allocations, losses, and the **tier bonus**. A8 widens
`ShowResults` and adds the keys; **A10 supplies the values** from its ledger. The Integrity line must
show the value **frozen at PreBoss activation** (A3's freeze) with the tier bonus applied.

**Tests.**
- **Rewrite:** `tests/unit/StoryHudPresentationTests.cs` —
  `TheAuthoredSceneSuppliesEveryWidgetTheControllersDriveAndAdoptsTheTheme` (node paths),
  `TheStatusPipFollowsTheRisingAndFallingEdgesOnTheBus` (now two slots). The block-pip, cooldown-row,
  HUD-opacity and boss-notch cases survive with path updates. **±0.**
- **Rewrite:** `tests/unit/FighterHudSceneTests.cs` —
  `TheStatusPipAppearsOnlyWhileAStatusIsActive` (two slots),
  `PipCapacityFollowsTheAuthoredShieldChargesAndMatchStockRule` (Stock vs Time),
  `PlayerPanelsAnchorToTheBottomAndTheClockStaysTopCenter` (renames),
  `TheMatchClockFollowsTheTimerFlagAndFormatsTimedModes` (Sudden Death text). **±0.**
- **Rewrite:** `tests/unit/HudAbilityIndicatorModelTests.cs` (second slot), **±0**.
- **Rewrite:** `tests/unit/AudioSnapshotMixerTests.cs` and `tests/unit/AudioSnapshotTriggerTests.cs` —
  both currently pin additive stacking and minimum-cutoff stacking. **±0.**
- **Rewrite:** `tests/unit/LevelResultsStatsTests.cs` — rating removal + F05 itemisation. **±0.**
- **Rewrite:** `tests/unit/GlowStateStackTests.cs` and `tests/unit/GlowPresentationControllerTests.cs`
  — the slot indicator leaves the effect stack. Verify `EnemyGlowRoutingTests` is unaffected (Story
  keeps effect-as-outline). **±0.**
- New `tests/unit/StoryHudContractTests.cs` **+3** — node-path conformance against HUD_CONTRACT;
  every contract node exists; the repo extras are present and named.
- New `tests/unit/StatusSlotHudTests.cs` **+6** — both modes, both slots, fixed positions, P2 **not**
  reversed, an empty slot keeps its position, radials follow the authoritative clock under Time Freeze.
- New `tests/unit/DefySealTests.cs` **+5** — the four states, spent survives a refill, Unavailable
  takes precedence without clearing spent, recompute after rollback, F04 authorization never lights it.
- New `tests/unit/DustCounterPersistenceTests.cs` **+3** — visible at zero and during the boss fight;
  only `+N` fades; a mirrored event or rollback adds nothing.
- New `tests/unit/IntegrityClockTests.cs` **+3** — the clock face reads the event, the pause dim, the
  PreBoss seal flourish.
- New `tests/unit/StockDisplayModeTests.cs` **+2** — pips in Stock, "Stocks lost: N" in Time.
- New `tests/unit/ReducedTemporalEffectsSettingTests.cs` **+5** — persistence; the missing-legacy
  default without inferring from Screen Shake; live apply with no timer reset; applied before the
  first loading effect; **a determinism pin that the same match produces an identical gameplay hash
  with the toggle On and Off**.
- New `tests/unit/AudioPriorityMixerTests.cs` **+5** — the priority ladder; no cumulative ducking;
  re-selection on release rather than restoring Normal; the Critical Cues bus bypasses the background
  LPF; the Pause UI boost is gone.
- New `tests/unit/OwnershipOutlineTests.cs` **+4** — the owner edge survives a status start, overlap
  and expiry; P1/P2 colours; per-actor material isolation; no owner edge in Sudden Death spawn
  protection because there is none.
- Extend `tests/unit/InputBindingSchemaTests.cs` **+4** — explicit Unbound round-trips through
  `Normalize()`; shortcut flags round-trip; a legacy payload defaults shortcuts On; the reachability
  validator rejects an unreachable Ultimate.
- Extend `tests/unit/SettingsMenuSceneTests.cs` **+2** — the 17-row Controls tab and the new Gameplay
  toggle both join the focus chain.

**Expected count delta: +42** (1768 → 1810 at A8's merge).

**Acceptance criteria.**
- Every node named in HUD_CONTRACT exists in `StoryHUD.tscn` at its contract path.
- Two statuses render simultaneously in both modes, at fixed positions, with P2 unreversed.
- Under Time Freeze a status radial does **not** advance.
- The Fighter ownership edge is still visible and correctly coloured while spawn protection, armor
  and both status glows are active, and immediately after each expires.
- Reduced Temporal Effects On and Off produce **identical** gameplay hashes for the same input stream.
- The audio mixer never applies two ducks at once.
- A user can bind Grab to a single key, disable the Block+Attack shortcut, and still grab.

---

### A9 — Three Open Fighter stages

**Goal.** Give `FighterStageGeometry` a floor-segment concept and author real pits with grabbable main-
floor ledges on Paris Bastille, Vesuvius Caldera and Nassau Flagship, with per-stage respawn anchors
and pit-aware DI.

**Design references.** `design-godot.md` §10 "Floor Segments, Pits & Ledges" (V7 pillar decision,
Option A, resolves audit H-11), V7.6 ruling 2.D. Recon: **I §1 in full** — it carries the proposed
geometry, the pixel conversions and the complete change list.

**The V7.6 2.D sequencing rule governs this workstream:** the three Open stages **ship before any
further Fighter balance pass**. Every Fighter number touched before that is provisional. A9 ships
geometry and re-verification infrastructure; **it must not retune knockback, DI, tech or ledge
numbers.**

**Current state.** `scripts/FighterSim/FighterStageGeometry.cs` (402 lines) has
`FighterStagePlatform(CenterX, SurfaceY, HalfWidth)` for one-way platforms only, and
`FighterStageGeometry` fields `StageID, LeftWall, RightWall, Ceiling, BottomBlastZone, SpawnDistance,
Platforms[], HazardAnchorXs[], OrbAnchors[]` — **there is no floor field at all**. `TryFindLedge`
iterates **platforms only**; the anchor encoding is `platformIndex * 2 + side`.
In `FighterSimulationSystems.cs` the floor is implicit and global:
line 468 `bool groundIsSolid = _geometry.Platforms.Length > 0;`
line 469 `if ((groundIsSolid || fighter.DropThroughFrames <= 0) && fighter.Position.y <= FP64.Zero) { ... }`
— **a wall-to-wall floor snap at y = 0**;
line 448 `if (IsGrounded != 0 && Position.y > FP64.Zero && !HasPlatformSupport(...)) IsGrounded = 0;`
— walking off an edge is only checked **above** y = 0;
line 461 the blast-zone check runs *before* the ground snap (deliberate — this already works the
moment a fighter can get below y = 0);
line 749 `bool onSolidBaseFloor = groundIsSolid && Position.y <= FP64.Zero;` gates drop-through off the
main floor — the "no drop-through on the floor" rule is already correct in shape and just needs a
per-x lookup. `HasPlatformSupport` (602) and `TryLandOnPlatform` (611) iterate `Platforms` only.
Current geometry (identical to the locked dossier in `tests/Determinism/FighterStageGeometryTests.cs`
lines 44–55):

| Stage | Walls | Platforms (cx, surfaceY, halfWidth) | HazardAnchorXs | OrbAnchors |
|---|---|---|---|---|
| `paris_bastille` | ±9 | (−4, 2.6, 2.0), (4, 2.6, 2.0) | −6, 0, 6 | (−4, 3.1), (4, 3.1), (0, 0.5) |
| `vesuvius_caldera` | ±8 | (−4.5, 2.0, 1.1), (3.0, 3.5, 1.1) | −6, −2, 2, 6 | (−4.5, 2.5), (3.0, 4.0), (0, 0.5) |
| `nassau_flagship` | ±9 | (−4, 2.8, 1.8), (4, 2.8, 1.8) | −5, 0, 5 | (−4, 3.3), (4, 3.3), (0, 0.5) |

Shared: `Ceiling = 9`, `BottomBlastZone = −5`, `SpawnDistance = 4` (spawns at x = ∓4, y = 0).
All three scenes author a **single full-width** `Geometry/Ground` `StaticBody2D` on layer 64 at pixel
(950, 700) with a `RectangleShape2D` of width 1125 px (Paris/Nassau, ±9) / 1000 px (Vesuvius, ±8),
collision offset `(0, 24)`.
**`FighterStage_Paris.tscn` already paints a *fake* pit** — `Presentation/CourtyardPit` (a `ColorRect`
at pixel x 700→1200, y 640→700) plus `CourtyardStepLeft/Right` and `CourtyardRail`. In world units
that is x −4 → +4, y 0 → −0.96, i.e. a painted trench spanning exactly the two spawn points.
**It is decoration over solid floor and its span is incompatible with any real pit — re-author it, do
not reuse it.**
`tests/ContentValidation/FighterStageConformance.cs` `ValidateGroundAndWalls` (158–185) demands
exactly one `Geometry/Ground` `StaticBody2D`, one `RectangleShape2D`, a top edge at world y = 0, and
**width == `RightWall − LeftWall`** — a segmented floor fails outright.
`FighterMatchFlowRules.RespawnPlatformPosition = (0, +3)` is a **global constant** consumed by
`FighterSimulationRules.ApplyStockLoss` (2462) and `ProcessRespawnPlatform` (581) — on a Paris centre
pit the respawning fighter drops straight back into the hole.

**Authored geometry (locked by this plan; recon I §1.3 derivation).** Constraints solved against:
spawns at x = ∓4 must be on solid floor; walls stay solid; gaps must be survivable with the
post-2026-08-10 double jump (double-jump ceiling ≈ **4.21 units**); the ledge capture box is **±0.5
wide and 1.2 deep** below the surface (`FighterLedgeRules.CaptureHalfWidth` / `CaptureDepth`); the
blast zone is 5 units below the floor.

| Stage | Floor segments (world x) | Pit span | True main-floor ledges |
|---|---|---|---|
| **Paris Bastille** | `[−9, −2.5]`, `[2.5, 9]` | **−2.5 → +2.5** (5.0 wide, centred) | x = −2.5 and x = +2.5 |
| **Vesuvius Caldera** | `[−8, 5.5]` | **5.5 → 8** (2.5 wide, against the right wall) | x = +5.5 only |
| **Nassau Flagship** | `[−9, 6]` | **6 → 9** (3.0 wide, against the stern) | x = +6 only |

Paris's one-way walkways at (∓4, 2.6, hw 2.0) span ∓6…∓2, so each **overhangs the pit by 0.5** — it
reads as a drawbridge over the gap, and the spawns at ∓4 sit on solid floor underneath.
Vesuvius's right wall stays solid above its gap, so a knocked fighter is pinned between wall and
ledge: one true ledge, one high-tension recovery. Nassau is the cheapest — every existing hazard
anchor (−5, 0, 5) and orb anchor stays over floor.

**Required anchor changes.**
- **Paris:** orb anchor `(0, 0.5)` sits over the new pit and would fail
  `EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface`. Move it to **`(−7, 0.5)`** (or go to four
  symmetric anchors: `(−7, 0.5)`, `(7, 0.5)`). Hazard anchors {−6, 0, 6} **stay** — the Neural
  Dampening Beam is a tall sweeping column and conformance compares hazard anchors on the **X axis
  only** (`ValidateMarkerSet(..., compareYAxis: false)`).
- **Vesuvius:** hazard anchor `+6` sits over the new gap and the Rockfall's ground residue pool
  (halfWidth 1, 180 frames) would hang in mid-air. Move to **`+4.5`** → `{−6, −2, 2, 4.5}`. Orb
  anchors unchanged.
- **Nassau:** no anchor changes.

**Exact change list.**

**A. `FighterStageGeometry.cs`**
1. Reuse `FighterStagePlatform` with `SurfaceY = 0` for floor segments — fewer new types, and
   `HangPosition` / `IsInCaptureBox` come for free.
2. Add `public readonly FighterStagePlatform[] FloorSegments;` plus a `NoFloorSegments` sentinel
   meaning "unbroken floor wall to wall" (**all seven Sealed stages keep the empty array, so zero
   behaviour change for them**).
3. Add `public bool HasFloorSupport(FP64 x)` → `FloorSegments.Length == 0 ? true : any segment
   Supports(x)`.
4. Add `public bool IsOpenStage => FloorSegments.Length > 0;`.
5. Add `public readonly FPVector2 RespawnPlatformPosition;` defaulting to `(0, 3)`, overridden for
   Paris to **`(−4, 3)`** (over the left walkway/floor).
6. Extend `TryFindLedge` to search floor segments **after** platforms, continuing the anchor
   encoding: `anchor = (Platforms.Length + segmentIndex) * 2 + side`. `TryGetHangPosition` resolves
   the same way. Search order stays deterministic (platform index ascending, left edge before right,
   then segments). **`LedgeAnchor` is already an `int` — this adds no snapshot field.**
   `FighterRuntimeComponent` is exactly full at 128 bytes; **do not add sim state here.**
7. Author the three Open geometries; leave the other seven untouched.

**B. `FighterSimulationSystems.cs` (`FighterMovementSystem`)**
8. Line 468: replace `Platforms.Length > 0` with `_geometry.HasFloorSupport(fighter.Position.x)` for
   the **snap** decision, and keep a separate
   `stageHasSolidFloorRule = _geometry.Platforms.Length > 0 || _geometry.IsOpenStage` for the
   legacy-arena drop-through behaviour.
9. Line 469: the snap fires **only** when `HasFloorSupport(x)` — otherwise the fighter keeps falling
   toward the blast zone (already resolved first at line 461, so the ordering is right).
10. Line 448: change the `Position.y > FP64.Zero` guard so a grounded fighter **at y == 0** with
    `!HasFloorSupport(x)` also loses support. `HasPlatformSupport` compares `Position.y ==
    platform.SurfaceY` with exact fixed-point equality; extend it to treat y == 0 as the floor plane
    and consult `HasFloorSupport`.
11. Lines 401 / 749: pass the per-x result so **"drop inputs are ignored on the main floor"** holds on
    floor segments and is not accidentally re-enabled over a pit (there is nothing to drop through
    there — the branch must simply not fire).
12. `TryGrabLedge` (543) needs no change once `TryFindLedge` covers segments, but confirm
    `FighterLedgeRules.CanGrab` gating behaves at y ≈ 0: the capture band is
    `SurfaceY − 1.2 … SurfaceY`, i.e. **y −1.2 … 0** for a floor ledge, comfortably above the −5
    blast zone.
13. `ResolveLedgeTrump` (507) needs no change — it compares anchors, which now include floor ledges.
    **Verify** the V7.3 trump rule reads sensibly when both fighters hang off the same *floor* ledge.

**C. Respawn anchor**
14. `FighterMatchFlowRules.RespawnPlatformPosition` (a `static readonly FPVector2` at `(0, 3)`) is
    consumed at `ApplyStockLoss` (2462) and `ProcessRespawnPlatform` (581). Both must read the
    **geometry's** anchor. `ApplyStockLoss` is `static` and has no geometry parameter — thread it
    through (cheapest: an `FPVector2 respawnPosition` argument; it is called from
    `FighterMovementSystem`, which already holds `_geometry`, and from the hit pipeline).
    **Check every call site:** `grep ApplyStockLoss` → `FighterSimulationSystems.cs:462` and the hit
    pipeline.
15. Verify the drop from the respawn platform lands on floor for all three Open stages.

**D. Pit-aware DI.** `FighterCpuController.ApplyHitstunDefense` (233–236) holds toward
`(LeftWall+RightWall)/2` with an explicit comment that pit-aware "hold up" DI is deliberately
unimplemented "because the authored stages run solid floors". **That premise dies here.** A9 adds the
floor-topology fields to `CpuDecisionObservation` (see E below) and rewrites the comment; the DI
*policy* change is **A9b's** — A9 ships the observation and the corrected comment only.

**E. `CpuDecisionObservation`.** Add floor topology — nearest floor-segment edge X, whether the
fighter's current X has floor support, the gap span, and whether the launch trajectory heads over a
gap. **Keep the struct mode-neutral:** Story's `MirrorParadoxDecisionAdapter.Observe` must fill
sentinels exactly as `HasStageBounds` does today, and
`tests/unit/MirrorParadoxTests.cs` pins frame-identical parity — it must stay green.

**F. Scenes.** Replace the single `Geometry/Ground` body with **one `StaticBody2D` per floor segment**,
named `Geometry/GroundLeft` / `Geometry/GroundRight` (Paris) and `Geometry/Ground`
(Vesuvius/Nassau, one shifted segment). Keep layer 64, keep the `(0, 24)` collision offset
convention, keep the top edge at world y = 0 (pixel y 700). Conversion is
`pixel = (950 + x·62.5, 700 − y·62.5)`:
- **Paris left:** centre world x = (−9 + −2.5)/2 = **−5.75** → pixel **590.625**, width
  6.5 × 62.5 = **406.25 px**. Right segment mirrored at pixel **1309.375**.
- **Vesuvius:** segment `[−8, 5.5]`, centre **−1.25** → pixel **871.875**, width
  13.5 × 62.5 = **843.75 px**.
- **Nassau:** segment `[−9, 6]`, centre **−1.5** → pixel **856.25**, width 15 × 62.5 = **937.5 px**.
Delete and re-author Paris's decorative `Presentation/CourtyardPit` + `CourtyardStepLeft/Right` +
`CourtyardRail` so the painted pit matches the real one (x −2.5…+2.5 = pixel 793.75…1106.25) and add a
visible depth/edge treatment. Vesuvius and Nassau need new pit art (collapsed shelf / open water below
the stern). Add readable **ledge/edge cues** at each true ledge. Check `FighterCamera` framing and
`StageLightingRig` placement against the new hole.

**G. Conformance validator.** Rewrite
`tests/ContentValidation/FighterStageConformance.cs::ValidateGroundAndWalls` (158–185): collect
**all** layer-64 bodies under `Geometry` whose name is not `WallLeft`/`WallRight`, match each against
a `FloorSegments` entry by centre + half-width (the same matching loop shape as `ValidatePlatforms`),
assert the top edge is y = 0 on each, and assert the union equals wall-to-wall **only when
`FloorSegments` is empty**.

**H. Catalog label (I-13).** `scripts/Environment/FighterStageData.cs` gains
`[Export] public bool IsOpenStage;`. Set `true` on the three entries in
`resources/FighterStages/stage_catalog.tres` and **assert in a test that the catalog flag matches
`FighterStageGeometry.ForStage(id).IsOpenStage`** — a drift there is a silent lie in stage select.
New keys `stage_layout_open` / `stage_layout_sealed`; render the badge in `CharacterSelectScreen`'s
stage phase and in `HolodeckConsolePanel`'s stage row. Reword the three existing `stage_*_layout`
descriptions (en.csv lines 493, 497, 501) to mention the pit.

**Files owned (exclusive).** `scripts/FighterSim/FighterStageGeometry.cs`,
`scripts/FighterSim/CpuDecisionObservation.cs`, `scripts/Environment/FighterStageData.cs`,
`resources/FighterStages/stage_catalog.tres`, `scenes/fighter/FighterStage_Paris.tscn`,
`FighterStage_Vesuvius.tscn`, `FighterStage_Nassau.tscn`,
`tests/ContentValidation/FighterStageConformance.cs`.

**Shared files touched additively.** `scripts/FighterSim/FighterSimulationSystems.cs`
(**region: `FighterMovementSystem` floor handling + the `ApplyStockLoss` respawn argument only** —
A1c owns `FighterMatchSystem` in Wave 2; see §5),
`scripts/FighterSim/FighterCpuController.cs` (**the DI comment + observation fill only**),
`scripts/UI/CharacterSelectScreen.cs` / `scripts/UI/HolodeckConsolePanel.cs` (the badge),
`localization/en.csv`, `docs/PACKAGE6_FIGHTER_PLAN.md` §4 (a FloorSegments column) and §9.

**Tests.**
- Extend `tests/Determinism/FighterStageGeometryTests.cs` — add a `FloorSegments` field to
  `StageDossier`, update the three Open rows, extend
  `EveryAuthoredStageSharesTheCommonBoundsContract` with "segments lie inside the walls, never
  overlap, and always support both spawn points", and make
  `EveryOrbAnchorSitsHalfAUnitAboveItsSupportingSurface` consult floor support. **+2** new cases:
  **every pit is escapable** (drop a fighter into each gap and prove a jump + ledge grab returns
  them — run it per character, following `RollbackReadinessKitShapeTests`' enumeration pattern), and
  the Open/Sealed catalog↔geometry cross-check.
- Extend `tests/Determinism/FighterLedgeTests.cs` **+4** — floor-segment ledge capture; the trump rule
  on a floor ledge; the regrab budget on a floor ledge;
  `TheLegacyFlatArenaHasNoPlatformsAndThereforeNoLedges` must stay true.
- Extend `tests/Determinism/FighterMatchFlowTests.cs` **+4** — per-Open-stage "walked into the pit
  costs a stock"; "knocked into the pit costs a stock"; the respawn platform lands on floor on all
  three; `MatchEndsOnABottomBlastZoneFall` (line 298) no longer has to manufacture the fall.
- **Rewrite in place:** `FighterStageParisTests` / `FighterStageVesuviusTests` /
  `FighterStageNassauTests` — each `SceneMirrorsItsAuthoredFixedPointGeometry` case fails until the
  scene is re-authored. **Update, do not delete. ±0.**
- New `tests/ContentValidation/FighterStageConformanceNegativeTests.cs` **+1** — a stage whose scene
  floor spans the pit must fail.
- Re-run `tests/Determinism/RollbackReadinessTests.cs` across all ten stages — the **anchor encoding
  change touches `LedgeAnchor` semantics, which IS snapshotted**, so a mid-match re-encode would
  desync. Land it as one atomic change.

**Expected count delta: +11** (1810 → 1821 at A9's merge).

**Acceptance criteria.**
- A fighter who walks off x = −2.5 on Paris falls and reaches the blast zone.
- A fighter can grab, hang from and climb back onto every authored floor ledge, on every character.
- Drop inputs on the main floor are still ignored on all ten stages.
- The seven Sealed stages behave bit-identically to before (empty `FloorSegments`).
- `RollbackReadinessTests` passes across all ten stages after the anchor re-encode.
- Stage select shows Open/Sealed and the label matches the geometry.

---

### A12 — Level 4A framework, routing, and the Einstein exemplar

**Goal.** Build `LegacyLevelControllerBase`, the 17-slot route table, the Nexus Resonance Source, the
Eraser debut trigger type, and **one complete variant (Einstein)** as the worked exemplar the three
B-wave agents copy.

**Design references.** §2.4 of this plan (the locked shape), `design-godot.md` §3 Level 4A,
F04 (master 1210), F12. Contracts: `docs/design-contracts/LEGACY_CHECKPOINTS.md` (read in full),
`DUST_ECONOMY.md` (the 4A row). Recon: **B §B5**, **G §1.3(h)**, **D §6.4**.

**Current state.** Nothing. `CampaignLevel` (`StoryManager.cs:6–23`) is a contiguous
`Tutorial=0 … Alexandria=15`; `AdvanceToNextLevel()` does `CurrentLevel + 1` and caps at 15;
`LevelScenePaths` is a **16-entry array indexed by the enum value** and `GetLevelScenePath` indexes it
directly. `resources/Content/content_manifest.csv` rows 4–19 are the sixteen `StoryLevel` rows.
`tests/integration/CampaignRouteTests.cs` asserts all sixteen resolve and that a sequential 0→15
advance lands on a real scene at every step. `MainMenu`'s developer level select is a **sixteen-tile**
grid. `StoryLevelControllerBase.BuildCheckpoint` hardcodes
`SelfActivating = id.EndsWith("_checkpoint_0")` — **A3 has already replaced that with roles by the
time A12 merges.**

**Exact change list.**

1. **Routing.** Append `CampaignLevel.LegacyNexus = 16` (**do not renumber**). Replace the
   array-index model with an **explicit ordered route list**
   `[Tutorial, Florence, Orleans, Chicago, Paris, LegacyNexus, Titanic, Pompeii, Nassau, Alexandria,
   Berlin, Globe, Gettysburg, Lunar, ChronalVoid, NeoEarth, Alexandria15]` — in enum terms
   `0,1,2,3,4,16,5,6,7,8,9,10,11,12,13,14,15`. `AdvanceToNextLevel()` steps that list;
   `GetLevelScenePath` resolves **by ID**, not by index. `ResumeCampaign`'s path→index scan must be
   re-expressed as a path→ID lookup.
2. **Per-hero scene path.** `res://scenes/campaign/Level_04A_<hero>.tscn`, level ID
   `level_04a_<hero>`. `GetLevelScenePath(CampaignLevel.LegacyNexus)` reads
   `GameManager.Instance.CurrentSession.SelectedCharacterID` and substitutes. Guard: if the session has
   no character (a developer direct launch), fall back to the save's locked character; if there is
   none, refuse the route rather than loading a missing scene — `StoryManager` already validates a
   route before changing scene.
3. **`LegacyLevelControllerBase`** (`scripts/Environment/LegacyLevelControllerBase.cs`), extending
   `StoryLevelControllerBase`, supplying:
   - exactly **two** checkpoints with authored roles `Entry` / `PreBoss` (A3's `CheckpointRole`),
     both enabled on all three difficulties, default IDs `{levelID}_checkpoint_0` / `_1`;
   - the **required-route ordering gate**: Entry → required kit gates and the independent Eraser
     encounter → late Font approach → PreBoss → boss. **All mandatory pre-boss objectives — including
     the F04 Nexus puzzle and the Eraser — must be complete before PreBoss can activate.**
   - one **Restoration Font** placed on the late approach after the Eraser and before PreBoss, with
     existing difficulty uses/potency;
   - the boss arena hook;
   - the dust hooks for 15 required / 25 boss / 10 optional (A10 supplies the allocator);
   - a `ParSeconds` slot and its own starting-Extractor count (A3's timer);
   - the F11 Entry recovery budget slot per difficulty.
   Variants override: geometry, the four kit gates, the era theme, the boss resource, the dialogue set.
4. **`NexusResonanceSource`** (`scripts/Environment/NexusResonanceSource.cs`), an `IInteractable`
   puzzle prop implementing F04 exactly as §2.4 states. The **combat-side constraint** is the meter
   chokepoint: a puzzle cast must **not** call `Consume()` and must neither earn nor drain meter.
   `PlayerController` gains a narrow Ultimate-input interception for the armed state — **Story-only,
   never in `scripts/FighterSim/`**. Never saved armed: an unsolved gate restores an **available**
   source; a solved gate keeps it **disabled**. **Time Freeze cannot arm the source or cast the
   Ultimate.** It never lights the F13 seal. Local prompt "Nexus-powered Ultimate"; no change to the
   meter or Defy indicators.
5. **Eraser debut trigger.** A route-trigger encounter type keyed `{levelID}_eraser_debut`,
   independent of checkpoint activation and difficulty, granting **no** checkpoint, Mending, rewind
   refill or Integrity lock. **Idempotent reconstruction:** a checkpoint reload rebuilds the ambush
   under the F10 encounter baseline; if the encounter respawns, its route trigger must remain
   functional; a persistent "trigger seen" flag must **not** suppress required enemies and strand the
   gate; already-collected rewards remain claimed; repeated crossings never spawn duplicate live
   waves. After PreBoss, the saved baseline treats all required approach encounters as completed.
   **First-view presentation flags stay separate from encounter state.** Time Freeze cannot activate it.
   **A12 ships the trigger type and the hook; A7a (Wave 2) supplies the Eraser enemy itself** — A12
   wires the hook against a placeholder elite (`chrono_guard_elite`) and B3 re-points all nine
   variants once A7a merges. Record the handoff in §9.
6. **Einstein exemplar** — `scenes/campaign/Level_04A_einstein.tscn` +
   `scripts/Environment/Level04AEinsteinController.cs` + `resources/Dialogue/level_04a_einstein_dialogue.tres`
   + `resources/Pools/level_pool_configs/level_04a_einstein_pool_config.tres` + a manifest row +
   `resources/Bosses/legacy/einstein_legacy_boss.tres`.
   The level must **require the whole kit once each**: one traversal gate for Relativity Warp, one
   puzzle for Mass-Energy Conversion (Special 1), one puzzle for Relativity Rift (Special 2), and one
   set-piece The Cosmological Constant (Ultimate) resolves through the Nexus source.
   **This is the only level allowed to require a specific character's abilities.**
   Boss: state in §9 exactly which existing era boss resource is duplicated and what HP row the variant
   uses (§2.4).
7. **Manifest + pools + dev select.** Nine `StoryLevel` rows in `content_manifest.csv` (A12 adds
   Einstein's; B1–B3 add theirs). `MainMenu`'s developer level select grows from 16 tiles to **17**,
   with the 4A tile labelled `04A  <campaign_level_legacy_nexus>`; `MainMenuSceneTests` pins the tile
   count and must be updated.
8. **Localization** under `# Package 11 A12`: `campaign_level_legacy_nexus`, the 4A level title/room/
   objective family for Einstein, the Nexus prompt, the Eraser bark key (A6/A7a author the bark's
   English).

**Files owned (exclusive).** `scripts/Environment/LegacyLevelControllerBase.cs` (new),
`scripts/Environment/NexusResonanceSource.cs` (new),
`scripts/Environment/Level04AEinsteinController.cs` (new),
`scenes/campaign/Level_04A_einstein.tscn`, `resources/Dialogue/level_04a_einstein_dialogue.tres`,
`resources/Bosses/legacy/einstein_legacy_boss.tres`,
`resources/Pools/level_pool_configs/level_04a_einstein_pool_config.tres`.

**Shared files touched additively.** `scripts/Core/StoryManager.cs` (**region: the `CampaignLevel`
append, the route list and `GetLevelScenePath`** — the last Wave 1 claim on this file; see §5),
`scripts/Environment/StoryLevelControllerBase.cs` (**additive only** — A3 owns it),
`resources/Content/content_manifest.csv` (append), `resources/Pools/scene_pool_catalog.tres`
(append), `scripts/UI/MainMenu.cs` (the 17th dev tile), `localization/en.csv`,
`scripts/Characters/PlayerController.cs` (**region: the Nexus Ultimate interception only**).

**Tests.**
- **Rewrite:** `tests/integration/CampaignRouteTests.cs` — the sequential 0→15 advance becomes the
  explicit 17-entry route; all seventeen routes resolve on disk for the active character; the route
  caps at 15; 4A sits between 4 and 5. **+3.**
- New `tests/unit/LegacyCheckpointContractTests.cs` **+7** — exactly two roles on all three
  difficulties; no Middle; Entry self-activates once; PreBoss requires every mandatory pre-boss
  objective; no timer lock at Entry or at the Eraser trigger; the Font sits after the Eraser and
  before PreBoss; Hard's middle-inactive rule cannot disable a role-`PreBoss` checkpoint whose ID ends
  `_1`.
- New `tests/unit/NexusResonanceSourceTests.cs` **+7** — arms only in a safe cleared area; casts at
  zero meter; neither fills nor consumes meter; leaving the area / death / reload / an interrupted
  cast clears the arming; unlimited free retries; a solved gate disables the source; **never saved
  armed**; Time Freeze cannot arm or cast; it never lights the Defy seal.
- New `tests/unit/EraserDebutTriggerTests.cs` **+4** — grants no checkpoint/Mending/refill/lock;
  reconstruction rebuilds without duplicate live waves; claimed rewards stay claimed; presentation
  flags are separate from encounter state.
- New `tests/ContentValidation/Level04AEinsteinContentTests.cs` **+1** (following the
  `LevelNNContentTests` pattern: level ID, two checkpoint IDs and roles, dialogue set and line keys,
  encounter counts against the locked 15/25/10 economy, one Font, boss arena width against the
  authored boss resource's ranged band).
- Extend `tests/unit/MainMenuSceneTests.cs` **+1** — 17 dev-select tiles.

**Expected count delta: +23** (1821 → 1844 at A12's merge, and the Wave 1 close).

**Acceptance criteria.**
- A campaign advancing from Level 4 lands on the active hero's 4A, and advancing from 4A lands on
  Level 5.
- No existing level renumbers; `CampaignRouteTests` proves all seventeen routes resolve.
- The Einstein variant is completable end to end at the campaign's placeholder-presentation bar, with
  all four kit gates and the Nexus set-piece required.
- Saving inside the Nexus cast area and reloading restores an **available** source, never an armed one.
- The three B-wave agents can copy `Level04AEinsteinController.cs` structurally without reading any
  other file.

---
## 4. Wave 2 — after Wave 1 merges

Eleven agents. A1b, A1c, A3b, A6b, A7a, A7b, A9b, A10 and A11 run in parallel worktrees off the
post-Wave-1 `main`; B1, B2 and B3 start once A12's exemplar is on `main` (i.e. immediately, since A12
closes Wave 1). Merge order: **A1b → A1c → A3b → A6b → A7a → A7b → A9b → A10 → A11 → B1 → B2 → B3.**

### A1b — Defensive contract D01–D04, both modes

**Goal.** Land the D01/D02/D03/D04 resolution contract in both modes: one ordered resolution
chokepoint, a shield instance with an 8 s lifetime and grant identity, Defy protected recovery, the
F15 full shatter, the F17 lockout fix, the Rally clamp bug and the D03g/D03h source rules.

**Design references.** `docs/design-contracts/DEFENSIVE_EFFECTS.md` **in full** (D01–D04);
`design-godot.md` master 1200–1291 (meter, Rally, Defy), 1367 (F15), 1373 (F17), 1259/1272 (D04),
1274 (F13). Recon: **D §6, §8, §10 in full**, **E §1.3**, **I §2.1, §2.2**.

**D01/D02b resolution order (the spine of this workstream).** Per distinct eligible hit contact:
**hit eligibility/invulnerability → projectile immunity → Temporal Aegis → HP barriers → ordinary
block.** Stop at the first layer that fully prevents the hit.
Full absorption spends **no** block charge, causes **no** block response (no shieldstun, shatter, daze
or lockout) and awards **no** successful-block perk — "holding Block does not turn a shield absorption
into a successful block". Partial absorption passes **only the remainder** to a legal block, charged
at the **original attack classification** (Basic 1, Story Guard-Crush 2, player Special = all
remaining). Henry's Bastion is granted only **after a real block** and **cannot absorb its triggering
hit**. One attack → one resolution: no overflow duplicate, no replayed damage, no duplicate collision
callback.

**Current state.** Sim `ApplyFighterHit` order is invulnerability/stocks → **Aegis** → block: Aegis
before block ✓, no projectile-immunity layer, **no HP-barrier layer at all**. One defect: the Aegis
branch fires for **any** hit including zero-damage/zero-impulse pulses, so a 0-damage expiry knockback
or a warning tick can eat the bubble — D03c says zero-damage warnings, non-damaging statuses and
harmless overlaps consume **no** Aegis.
Story order is roll i-frames → **projectile immunity** (`HasStoryProjectileImmunity &&
HitboxID == "projectile"`) ✓ → **block** (`BlockSystem.ResolveHit`) → `ApplyDamage`, with the HP
barrier (`StoryShieldPoints`) drained **inside `ApplyDamage`, after block** — **exactly backwards**.
A blocking Shakespeare with a live Bastion currently spends a block charge (and can shatter) on a hit
the guard should have absorbed for free.

**Exact change list.**

**1. One Story chokepoint.** Extract `PlayerController.ResolveIncomingHit(hit)` implementing the D01
order: invuln → projectile immunity → Aegis → `StoryShieldPoints` → block → HP. On **full**
absorption return early with shield-impact feedback and **no** `BlockSystem.ResolveHit` call, **no**
`GrantHenrysBastion`, no shieldstun. On **partial** absorption subtract, then call `ResolveHit` with
the **original** `AttackClass` / `BlockChargeCost` and the reduced damage. Preserve one contact
identity (the existing `PhysicsCallbackGuard` + `Hitbox` dedupe already helps).
Mirror the ordering guarantee in the sim's `ApplyFighterHit`, and add the **zero-damage guard** to the
Aegis branch in both modes.

**2. D02a same-shield refresh.** A repeat grant of the **same** shield sets remaining absorption to the
effect's normal cap and **restarts** its authored duration; it never adds capacity or duration. One
instance per recipient + shield-effect ID. A full shield may refresh its duration. Broken/expired →
a fresh instance on the next legitimate grant. **Unique grant-event identity:** a refused input,
polling the condition, duplicate callbacks, recreated visuals and restored state grant nothing.
Introduce `StoryShieldInstance { EffectId, Points, Capacity, RemainingFrames, GrantEventId }` on
`PlayerController` (still one active) with the grant-identity guard.
**Cleopatra's decoy is a separate recipient** — route Royal Aegis to the decoy node
(`CleopatraAbilities:327–331` currently grants to Cleopatra). **The sand decoy does not exist** — it
is a V7 baseline rule that was never implemented; A1b builds it (1 s decoy) because Royal Aegis
depends on it.
An accepted **Second Glide re-entry** counts as a new entry, but merely continuing or holding the
glide does not (`PocahontasAbilities:256` currently re-grants on any `ApplyLeafBarrier()` call).
Wardenclyffe's coil-radius membership is **not** a repeat grant.

**3. D02c eight-second granted shields.** Henry's Bastion, Royal Aegis and Leaf Barrier each last
**8 seconds of live gameplay = 480 active ticks at 60 Hz** or until depletion. Repeat grants reset to
8 s, never add. An absorbed hit reduces capacity but **does not change the timer**. A broken shield
disappears immediately. Expiry discards remaining absorption with **no heal, meter, block event or
expiry proc**. It uses the **suspended-effect clock**: Time Freeze, menus, world-frozen recovery and
boss-rewind presentations do not consume lifetime; **no wall-clock substitution, no catch-up ticks**.
Royal Aegis on a decoy has its own recipient lifetime and can end early with the decoy; no transfer of
unused capacity or time. Generic movement/ability-duration upgrades do **not** extend these timers.
**Does not apply to** Wardenclyffe, Mozart's conditional projectile protection, or Temporal Aegis.
Today `StoryShieldPoints` has **no timer of any kind** — `BlockSystem.GrantHenrysBastion`'s comment
even records the approximation.

**4. D02d Wardenclyffe (three shipped defects).** 15% max-HP cap unchanged. Both **protection and
recharge** require Tesla inside the authored radius of at least one of his **own** active coils (a
linked fence is not an extra radius; overlapping coils give one shield and one rate).
**Damage delay: 3 live seconds (180 ticks)** whenever damage actually reduces Tesla's HP **or**
Wardenclyffe absorption; it counts down in or out of range; new qualifying damage restarts it; hits
fully rejected by invulnerability/projectile immunity/Aegis, or blocked without reducing shield or HP,
do **not** restart it; one contact reducing both starts one delay.
**Recharge: 2.5% of current max HP per live second**, capped at 15%, fractional progress retained
(empty→full = **6 s**; after a damaging hit that empties it: 3 s delay + 6 s). Damage resolves before
recharge in the same update.
**Leaving range immediately disables absorption and recharge but preserves stored charge**; returning
re-enables only the retained charge. Coil destruction/expiry/replacement/Ultimate detonation
recomputes eligibility. **Initialize to zero charge** with no delay on fresh entry / first acquisition.
T01a and F10 discard stored charge but **preserve the remaining damage delay** across same-attempt
recovery/load; respec removes it and reacquisition cannot clear an existing same-attempt delay.
Freeze-aware. Presentation must not show a protective bubble for a dormant out-of-range charge.
Repo today (`TeslaAbilities.cs:95–111`): the cap is right; recharge is a **flat
`ShieldRechargePerSecond = 2f` HP/s** (design: 2.5% of max HP/s); **no damage delay**; **absorption is
never disabled out of range** (the shared `StoryShieldPoints` drains in `ApplyDamage` regardless of
coil proximity — only recharge is gated); no persistence of the delay.

**5. D02e Temporal Aegis, and Story has none.** Temporal Aegis has **no time expiry** and **at most one
active per recipient**; a pickup while already protected is **consumed with no second charge, reserve,
duration, HP, meter or replacement reward**. One bubble, no stacked pips, no timer. Commit the pickup
claim and the active flag together through the existing pickup transaction; duplicate callbacks and
loading an already-claimed pickup grant nothing. Represent it as an **active flag tied to
source/instance identity**, not a growing charge count. Story ordinary load discards the shield but
preserves the consumed claim; T01a Death Rewind and F22 Sudden Death cleanup still remove it.
Sim: rename `FighterRuntimeComponent.AegisHits` semantics to a flag (or pin the cap at 1) and add the
zero-damage exclusion. **Story has no Aegis at all** — `OrbEffect` is
`{ HPRestore, MeterBoost, SpeedBuff, DamageBoost, ShieldRestore }` against the design's four orbs
(Temporal Restoration / Chronal Haste / Tectonic Uplift / **Temporal Aegis**). Add
`OrbEffect.TemporalAegis` and a one-hit absorb consulted in `ResolveIncomingHit`.

**6. D03a full absorption suppresses attached effects.** An eligible damaging hit **fully absorbed by
an HP barrier** applies **no** hitstun, stagger, knockback/launch, status or mark — **including when it
exactly depletes the barrier** (remove the depleted shield, but do not then apply the hit's effects).
No queued effects for later. Overflow to HP follows normal eligibility including hyper-armor and the
lethal/Defy ordering. Partial absorption does **not** proportionally shorten a status or grant extra
immunity. Existing statuses are untouched; existing poison keeps ticking. Tesla's D02d delay still
starts when capacity is actually lost, even with no HP loss and no attached effect. Emit
shield-impact/break feedback **without** a victim hurt/launch reaction.
*(Today this is accidentally satisfied in one case — `ApplyDamage` returns 0 and `OnHurtboxHit`
early-returns at `if (damageApplied <= 0) return 0f;` — but once ordering moves before block the
absorb must suppress effects **explicitly**, and the partial case must pass the remainder through with
full effects.)*

**7. D03b Unblockable vs barriers/Aegis.** Unblockable (player Ultimates + authored boss unblockables)
bypasses the **ordinary charge-based block only**; eligible Aegis and HP barriers still absorb in the
D02b order, and only the remainder reaches HP. **No** block charges spent, **no** shieldstun/shatter,
**no** successful-block perk, whether fully absorbed or not. The separate **player-Ultimate bypass of
enemy damage-reduction defenses** (Tech Enforcer bubble, Shock-Shield Legionnaire
`FrontalDamageReduction`) is retained and is **not** the same thing — that bypass targets *reduction*
fields, never finite HP barriers or Aegis. The red/X Unblockable telegraph and the attack's class
identity survive every layer — **do not relabel an absorbed hit to Basic/Guard-Crush.**

**8. D03c shields absorb DoT and hazard ticks (Option B).** Actual HP-damaging ticks from an
already-applied status (e.g. Venom) **and** ordinary external damaging hazards (spikes, lava,
stage-hazard ticks) **can be absorbed** by Aegis and active HP barriers. Route each scheduled
occurrence through the shared resolver **once**, with its source/status instance ID and tick index. A
status tick is **not a projectile** — projectile-only immunity does not reject it. An absorbed tick
**still counts as that scheduled occurrence**: no cleanse, no duration refresh, no delayed damage
debt, no immediate retry. Unabsorbed status-tick damage reaches HP under the normal resolver;
**external stage-hazard** remainder keeps its **Basic-class block rule (1 charge)**; finite-shield
coverage does **not** create a block opportunity against an already-attached DoT. Ticks keep **zero
hitstop**. Zero-damage warnings, non-damaging statuses and harmless overlaps consume **no** Aegis or
barrier HP. **Radiant Burn remains a vulnerability modifier with no damage ticks — do not invent a DoT
for it.** Pit kill boundaries, Integrity loss, the Siphon Snare and other non-HP drains never spend
shields. Tesla: a tick reducing Wardenclyffe charge or HP restarts the 3 s delay; a tick consumed by
an earlier Aegis does not.
**Also fix `FighterEntitySystems.cs:1464–1472`** — the Paris Neural Dampening Beam currently spends an
Aegis charge on a **pure meter drain**. D03c excludes non-HP drains: the beam must not consume Aegis;
the drain proceeds subject to the existing invulnerability/stocks gates.

**9. D03d primary throws bypass finite shields.** A legal grab is not prevented by HP barriers or
Aegis, and the resulting **primary throw deals normal damage and applies its normal launch without
consuming those protections** — skip Aegis and HP absorption **as well as** ordinary block at the
throw's scheduled damage/launch event. Do not decrement capacity, trigger an absorption/break, grant a
block perk, or treat the skipped protection as partial reduction. Retain 1.0× `BasicAttackDamage`,
authored impulses, weight and low-HP scaling, no DI, no statuses, and the existing hold/animation/
throw-immunity timings. Normal HP damage, eligible **Defy**, death and Rally/meter accounting resolve
once. A surviving victim keeps unspent protection at its normally elapsed lifetime — no refresh, no
pause because the actor was grabbed. Throw HP damage **does** restart Tesla's D02d delay even though
capacity was not spent. Mark the contact as the primary hit of a validated paired grab/throw event
(paired actor IDs + the existing damage-applied guard). **The Story secondary thrown-mob collision is
a separate projectile hit with normal defenses** and does not inherit the bypass.
Implementation: add a `bypassesFiniteShields` (or `isPrimaryThrow`) parameter to `ApplyFighterHit`,
skipping the Aegis branch and any barrier layer while leaving block untouched. **This is a real
shipped bug** — `ResolveThrow` calls `ApplyFighterHit(..., BasicAttackClass, ...)` today, so an active
Aegis **absorbs the throw entirely**.

**10. D03f / D03g / the Rally clamp bug.**
`creditedDamage = max(0, hpBeforeHit − hpAfterHit)`, evaluated **after defences and any Defy result,
before healing/recovery**; it excludes shield-absorbed damage, blocked damage, overkill below zero,
and the lethal portion Defy prevented. Both modes already compute this correctly for the **meter**.
**The reclaim is defective in both modes.** Design: `reclaimedHP = min(pool, creditedDamage × 2.0,
missingHP)`, and **subtract only the HP actually reclaimed from the pool**.
Sim (`FighterEntitySystems.cs:214–231`) computes `reclaimAmount = min(EchoPool, actualDamage × 2.0)`,
heals with an `attacker.MaxHP` clamp, then does `attackerVerb.EchoPool -= reclaimAmount` — **debiting
the full amount even when HP was capped**, with **no `missingHP` clamp**. Story
(`PlayerController.cs:2016–2030`) has the identical shape. Fix both: compute
`missing = MaxHP − CurrentHP`, clamp to `min(pool, credited × 2.0, missing)`, heal, then debit
**exactly the healed amount** (re-read HP before and after).
**D03g reclaim source eligibility.** Only **direct** damaging hits reclaim: basic/directional strikes,
direct Special or Ultimate impacts, **player-fired projectiles** (including delayed ones landing after
the firing animation), **primary throws**, and the **secondary Story thrown-mob collision** (at its own
actual HP loss, with normal shield coverage). **Excluded:** persistent zone ticks **including
Relativity Rift chip and every Sandstorm Vortex tick, explicitly including the Vortex's final
launching tick**; autonomous construct/persistent-object damage even when delivered through a
projectile; coil-fence ticks; attached DoT ticks; stage-hazard ticks. "A separately authored direct
cast impact may qualify; a first zone tick does **not** qualify merely because it occurs on the
casting frame." Blocked/fully-absorbed hits reclaim zero. Multi-target hits resolve each eligible
contact once against the remaining pool and missing HP.
**The shipped gap:** sim zone pulses (~`FighterEntitySystems:2012`) pass only `appliesHitstop: false`,
so **Relativity Rift and Sandstorm Vortex ticks currently reclaim Rally**; the expiry launches
(`:2077`, `:2133`, `:2164`) omit it too (0-damage today, but make it explicit). Story
`PlaceholderZone.cs:197` calls `AddInfluenceFromDamageDealt(damageApplied)` with `collectsEcho`
defaulting to **true** — every Story zone tick reclaims. Pass `collectsEcho: false` at every zone-tick
site in both modes. **Fix the `PlayerController` doc comment**, which enshrines the wrong rule
("a landed direct hit (melee, directional, special, ultimate, projectile, **zone pulse**) also
reclaims").

**11. D03h Ultimate-origin damage earns the caster zero meter.** An ordinary Ultimate still costs 100
on accepted activation. **All damage originating from that Ultimate awards the caster 0 damage-dealt
meter**, regardless of HP removed, target count, or when it lands. The exclusion follows the source
execution into direct hits, projectiles, chained bursts, summoned attacks, persistent damage zones and
attached damaging statuses — named cases: **Wardenclyffe Cataclysm's coil explosions** and
**Wrath of the Nile's poison ticks**. It is a *source* rule, not a global meter lock: independent
non-Ultimate attacks and pre-existing ordinary construct/status damage keep their eligibility. Victim
damage-taken meter and echo are unaffected; direct-hit Rally reclaim from Ultimate impacts still works.
Sim: thread an `ultimateOrigin` flag down every ultimate-spawned entity —
`FighterProjectileComponent`, `FighterZoneComponent`, `FighterPersistentObjectComponent`, and the
status application — and pass `creditInfluence: false` at `ApplyFighterHit`. Attribution must survive
the cinematic ending, owner interruption and rollback restore (**it is snapshot state**).
`FighterZoneComponent` already distinguishes the ultimate slot by `ZoneTypeID % 10`, a usable free
signal for zones; projectiles and statuses need a new field.
Story: add the flag to `AddInfluenceFromDamageDealt(dealt, collectsEcho, ultimateOrigin)` (or a
`BaseSpecial.IsUltimateSlot` check inside the helper) and set it on all nine ultimate scripts plus the
`PlaceholderZone` / `PlaceholderProjectile` instances an ultimate spawns, plus Nile's Venom ticks.
*(Story poison is accidentally compliant today because it routes via `ApplyPersistentDamage`, which
credits nobody — confirm per kit.)*
**Explicit non-regression:** an ordinary Tesla coil tick during a Cataclysm still earns meter.

**12. D04 Defy protected recovery.** On a successful Defy:
1. Still spend the full 100 meter, survive at 1 HP, generate no echo, consume the once-per-match /
   once-per-Story-attempt use (unchanged).
2. **Immediately** enable hit invulnerability covering the Defy presentation.
3. **Release** the survivor from the triggering hit's forced hitstun/stagger/knockback/launch **and
   capture** (including a paired throw or cinematic trap); clear its forced-motion contribution; keep
   the current legal position (no teleport, no granted jumps/cooldowns/resources); the defied hit may
   not impose a new attached control/status effect; end only that victim's capture link (the attack is
   not cancelled for other targets; projectiles and zones survive).
4. After the presentation, grant **60 active gameplay ticks (1 s)** of hit invulnerability **starting
   at the first resumed normal-control tick**. Ticks 1–60 protected, **tick 61 unprotected**. Attacking
   does not cancel or refresh it. The countdown **pauses** during global hitstop, menus, Time Freeze
   and suspended-combat presentations.
5. The gate rejects subsequent damaging hits **before** Aegis / HP barriers / block — covering all
   attackers, ongoing damaging-status ticks and damaging stage hazards. Rejected contacts spend no
   shield capacity or block charges, apply no hitstun/launch/status, and grant **no** damage, Rally or
   block rewards. Existing statuses are **not** cleansed; their timers continue and protected ticks are
   discarded, not banked. New grab/capture attempts fail under invulnerable-target eligibility.
6. Not immunity to non-hit deaths (pits, Integrity zero). No healing beyond 1 HP, no meter refund, no
   rewind spend, no status cleanse, no reset of the spent flag.
7. Dual lethal trades: both procs resolve and consume their uses; installing one survivor's protection
   cannot erase the other already-committed outcome.
8. **New snapshot state:** `DefyProtectionAwaitControl` (flag) and `DefyProtectionFrames` (int) plus
   the proc/presentation identity — on **Klotho component 312 `FighterDefenseComponent`** (§2.7).
9. Presentation: the existing meter-crack / desaturation cue is retained; show the remaining
   protection with the **existing secondary invulnerability glow**, fading at actual expiry; the F13
   seal stays **broken** throughout and after. C01a swaps full-scene desaturation for a steady local
   protection cue plus the meter crack, preserving slow-motion timing and the full post-control second.
10. Story F10 checkpoint reconstruction clears the transient protection and pending presentation while
    retaining committed HP/meter/spent use; loading never replays the proc or grants a fresh second.
    Death/stock loss, Death Rewind cleanup, scene reconstruction and F22 Sudden Death clear the window.
**Shipped state:** sim (`FighterEntitySystems.cs:162–172`) sets `DefyHistoryUsed`, zeroes `Influence`,
sets `CurrentHP = 1` and applies a 12-frame hitstop — then **falls through to the normal impulse and
hitstun block**, so the survivor is launched and stunned by the very hit they defied, with **no**
invulnerability window. Story (`PlayerController.cs:1968–1982`) is the same shape.
Implementation: add the release step (zero `HitstunFrames`, clear `PendingLaunchActive`/`Tumble`, zero
the knockback write for this hit, release the grab via `FighterGrabRules.ReleaseHeldVictim` and clear
`BeingHeld`/`GrabPhase` on the partner), set `DefyProtectionAwaitControl = 1`, and start the 60-tick
countdown at the first actionable tick, pausing on `HitstopFrames > 0` and match-flow freezes. Reject
incoming hits at the **top** of `ApplyFighterHit`, **before** the Aegis branch. Mirror on
`PlayerController` (`_defyProtectionAwaitControl` / `_defyProtectionFrames`), rejecting at the top of
`OnHurtboxHit` **and** `ApplyDamage` / `ApplyEnvironmentalDamage` (hazard ticks), releasing the grab
via the existing `ReleaseGrabState`, and suppressing the stun/knockback branch for the defied hit.
The pending-presentation identity must be **deduplicated across resimulation**.

**13. Defy persistence and the Sudden Death split.**
- **F10:** "once per level" means **once per attempt**. `_storyDefyHistoryUsed` is a private
  `PlayerController` field with **no save persistence** today, so a mid-level quit/resume or a death
  rewind that rebuilds the player restores the Defy. Add `StoryDefyHistoryUsed` to the per-attempt
  registry (`StoryManager.WriteAttemptStateToSave` / `ApplyResumedAttemptState`) and to
  `StorySaveData` (§2.6); commit `defyHistoryUsed`, HP and meter **together**; preserve through death
  rewind, Collapse, Snap, checkpoints, hub visits and load; **only fresh entry / full Restart Level
  resets it.**
- **F22:** `FighterSimulationSystems:2354` pre-marks `verb.DefyHistoryUsed = 1` on both fighters on
  Sudden Death entry — behaviourally right but it **conflates Unavailable with Spent**. F13 requires
  them distinguishable **without clearing the underlying spent flag**. Keep a separate
  `SuddenDeathDefyDisabled` read (derive it from `match.SuddenDeathActive` at the read site rather
  than storing it per fighter — recon I §3.2's recommendation) instead of overwriting the flag.

**14. F15 Divine Piercing / The Emancipator = full shatter.** Both use **ordinary Special-class full
shatter** in Story **and** Fighter against charge-based shields: on a valid blocked contact consume
**all remaining charges (1, 2 or 3 → 0)** and apply the **existing Special shatter response**
(shatter-freeze, daze, lockout). They are **not** two-charge Guard-Crush exceptions. At zero charges no
stance exists; rear/unblocked contacts use normal hit resolution without consuming charges. For
**Divine Piercing's multi-hit execution the first absorbed contact shatters once**; duplicate callbacks
cannot repeat the shatter or the perk award; later distinct contacts follow normal hit eligibility with
no extra absorption or invulnerability. **Damage, hit count, cooldown and startup are unchanged.** Use
the **full-shatter Special visual signature**, not the two-charge Guard-Crush diamond. The F06 Shield
of Orléans distinct-block meter reward still applies, but its **Guard-Crush charge refund does not
trigger** from these Special-class attacks.
The retired rule is authored in four places: `scripts/Combat/BlockSystem.cs:158–178`
(`DepleteCharges(int)`, whose doc comment names both moves), `JoanAbilities.cs:159`,
`LincolnAbilities.cs:143`, and `FighterSimulationSystems.cs:1494`
`DivinePiercingBlockChargeCost = 2` + `:2155`
`attacker.CharacterID == FighterCharacterID.Joan ? DivinePiercingBlockChargeCost : 0`.
*(Lincoln's S1 in the sim already passes 0 → the two modes disagree today.)*
Delete both Story call sites, delete `DivinePiercingBlockChargeCost` and the Joan branch, remove
`DepleteCharges` and its `BlockChargeDepletion` constants if nothing else needs them, add a
once-per-execution shatter guard for Divine Piercing's multi-hit, and swap the telegraph/VFX
signature. Fix `ApplyFighterHit`'s comment, which enshrines the retired rule.

**15. F17 Aegis must not end the shatter lockout.** Temporal Aegis is a **separate one-hit shield**;
collecting it **does not restore block charges, end the shatter lockout, or change the block
regeneration countdown**. The only specified block-recovery rules are normal regeneration and the
existing perk exceptions (Shield of Orléans can refund one charge after a Guard-Crush shatter, but
that charge **stays unusable until the 5 s lockout ends**). The prior V7.3 sentence — *"A Chronal
Shield-Restore orb ends the lockout along with restoring charges"* — is **deleted**.
Remove the `_lockoutTimer = 0f` line from `BlockSystem.RestoreAllCharges()` and rewrite its doc
comment. `ChronalOrbItem.OnPickedUp` calls it for `OrbEffect.ShieldRestore`; **retain
`ShieldRestore`** (it is shipped behaviour) but stop it touching the lockout.
**Also update `AGENTS.md`**, which currently records the retired rule verbatim ("a Story orb
shield-restore ends the lockout") — Phase C does the edit; A1b lists it in the handoff.

**16. F13 seal read-model.** Expose a single derived read-model
`DefyState { NotReady, Ready, Spent, Unavailable }` computed at the end of the gameplay update in both
modes and published via `EventBus.OnDefySealChanged` (§2.9 — A5 declared the event in Wave 1; A1b
takes over as the general publisher). **Never persisted as a separate cosmetic flag**; recomputed on
gain, spend, drain, proc, death/respawn, load and **rollback**. A8 renders it.

**17. Hyper-armor ordering.** "Ultimate attacks bypass hyper-armor **when their hit reaches normal
hit-response resolution**. A fully HP-barrier-absorbed contact causes no interrupt under D03a." One
ordering guarantee: **absorption resolves before the armor/hit-response branch.**

**Files owned (exclusive).** `scripts/Combat/BlockSystem.cs`, `scripts/Combat/ChronalOrbItem.cs`,
`scripts/FighterSim/FighterEntitySystems.cs` (**`FighterDamageRules.ApplyFighterHit` and the zone/
projectile credit flags** — the single sim chokepoint), `scripts/FighterSim/FighterSimulationComponents.cs`
(**ID 312 only**), `scripts/Characters/Abilities/JoanAbilities.cs`, `LincolnAbilities.cs`,
`CleopatraAbilities.cs`, `PocahontasAbilities.cs`, `ShakespeareAbilities.cs`, `TeslaAbilities.cs`
(**the Wardenclyffe rewrite**), `scripts/Environment/PlaceholderZone.cs`.

**Shared files touched additively.** `scripts/Characters/PlayerController.cs` (**region:
`ResolveIncomingHit`, the shield instance, the Defy protection fields, the Rally clamp, the
`ultimateOrigin` parameter** — the largest Wave 2 claim on this file; see §5),
`scripts/FighterSim/FighterSimulationSystems.cs` (**the F15 constants and the Sudden Death Defy split
only**), `scripts/Core/StoryManager.cs` (**the Defy attempt-registry entry only**),
`scripts/Core/SaveManager.cs` (one additive field), `scripts/Core/EventBus.cs`
(`OnDefySealChanged` publisher + `OnRallyEchoChanged` if it does not exist), `localization/en.csv`
(the four `Defy: …` state labels + the Systems Card section, under `# Package 11 A1b`).

**Tests.**
- New `tests/unit/DefenceOrderingTests.cs` **+8** — the DEFENSIVE_EFFECTS "Pending validation" matrix:
  full / partial / exact absorption × front / rear × grounded / airborne × 0/1/2/3 charges × lockout ×
  Basic / Guard-Crush / Special / block-bypassing.
- New `tests/unit/ShieldLifecycleTests.cs` **+9** — refresh 4/10 → 10/10 with no additive duration;
  a duplicate grant rejected by grant identity; Henry's valid block versus a full absorption; the
  decoy as a separate recipient; Second Glide re-entry versus held input; expiry at exactly 480 active
  ticks; early depletion; the suspended clock (no advance under Time Freeze / pause / boss rewind);
  unchanged by duration modifiers.
- New `tests/unit/WardenclyffeShieldTests.cs` **+10** — the D02d validation list: 3 s after qualifying
  damage; 6 s empty→full; the delay restart rules; partial charge across exit and re-entry; dormant
  protection out of range; overlapping and foreign coils; Ultimate destruction; freeze; first
  acquisition at zero; respec.
- New `tests/unit/DefyProtectedRecoveryTests.cs` **+14** (sim + Story) — immediate and same-frame
  follow-up protection; both dual-Defy outcomes; throw release; grounded and airborne control return;
  exactly 60 ticks after the presentation; pause/freeze; attacking does not cancel; unrelated statuses
  keep running; poison and hazard ticks rejected; a hit at tick 61 lands; pits and Integrity still
  kill; preserved Rally and shields; no reward for rejected contacts; the snapshot remainder;
  checkpoint-load cleanup without a regrant.
- New `tests/unit/UltimateMeterOriginTests.cs` **+4** — an Ultimate removing 70 HP from each of two
  enemies earns **0**, not 100; an independent 8-HP projectile still earns 8; a Cataclysm coil
  explosion earns 0 while an ordinary coil tick during it still earns; Nile's poison ticks earn 0.
- Extend `tests/Determinism/FighterHitPipelineTests.cs` **+4** — the zero-damage Aegis guard; the
  throw bypass ("a throw against an active Aegis deals full damage and leaves the bubble"); the F15
  1/2/3-charge cases all ending at 0 with daze + lockout; the Unblockable class label surviving
  absorption.
- Extend `tests/unit/BlockShatterLockoutTests.cs` **+1** — a charge restore during lockout leaves the
  stance unavailable until 300 frames elapse.
- Extend `tests/Determinism/FighterVerbLayerTests.cs` / new `tests/unit/StoryRallyTests.cs` **+4** —
  a full-HP attacker reclaims 0 and keeps the whole pool; a 1-HP-missing attacker reclaims 1 and the
  pool drops by 1; a 6-HP zone tick reclaims 0 while a 6-HP direct hit reclaims up to 12.
- Extend `tests/unit/FighterGrabTests.cs` **+1**; `tests/ContentValidation/JoanContentTests.cs` and
  `LincolnContentTests.cs` **+1 each**.
- New `tests/unit/DefySealStateTests.cs` **+4** — the read-model's four states, recompute after
  rollback, Unavailable in Sudden Death without clearing Spent, persistence across a Story attempt.

**Expected count delta: +60.**

**Acceptance criteria.**
- A blocking Shakespeare with a live Bastion spends **no** block charge on a fully absorbed hit.
- A granted shield expires at exactly 480 active ticks and does not advance during Time Freeze.
- A defied hit leaves the survivor free, invulnerable for exactly 60 resumed control ticks, and
  vulnerable on tick 61.
- Joan's Divine Piercing takes a 3-charge shield to 0 in both modes.
- A Rally reclaim at full HP costs nothing from the pool.
- An Ultimate earns its caster zero meter through every delivery vehicle.
- A Story Defy survives a mid-level quit and resume as spent.

---

### A1c — Echo Step determinism, dedicated inputs, protocol v3, F21/F22

**Goal.** Replace the approximate Echo Step ring with the V7.6 31-sample per-tick ring and exact
`t−30` destination, add the two dedicated input actions and protocol v3, append `CharacterState.Thrown`,
author the same-frame chord priority, and land the F21 mode consolidation and the F22 Sudden Death
contract.

**Design references.** `design-godot.md` master 1281–1291 (Echo Step, Resonance Momentum, Overtime),
1451–1452 (chord priority, F23 states), 2189–2200 (input table). Contracts:
`docs/design-contracts/FIGHTER_MATCH_RULES.md` (F21/F22, read in full),
`ROLLBACK_STATE_CONTRACT.md` (S01), `TEMPORAL_STATE_CONTRACT.md` (Echo Step destination policy),
`COMFORT_SETTINGS.md` (C01c). Recon: **D §9.1–9.4, §14.2**, **E §1.4, §6.6, §7.3**, **I §3.1, §3.2**.

**Current state.** `FighterEchoRingComponent` (Klotho ID 311, 88 B) holds **5 samples** written every
**6 frames** (`EchoRingSampleIntervalFrames = 6`, `EchoRingSampleCount = 5`), and the destination is
`OldestRingSample()` — i.e. somewhere **24–30 frames back depending on phase**. No valid-count, no
generation, no spawn reset, no activation tick. Arm/destination/windup/cooldown live on
`FighterVerbComponent` (`EchoStepCooldownFrames`, `EchoStepWindupFrames`, `EchoStepDestX/Y`); there is
no armed flag (windup > 0 stands in). Story mirrors it exactly
(`PlayerController._echoStepRing[5]`, `_echoStepSampleCountdown = 6`).
**No destination validation at all:** `TryStartEchoStep` spends meter and arms the cooldown
unconditionally; `AdvanceEchoStep` writes `fighter.Position = dest` with only a
`y > 0 → IsGrounded = 0` fix-up. A destination inside terrain or outside stage bounds teleports there.
`project.godot` has 14 `gameplay_*` actions; neither `gameplay_grab` nor `gameplay_echo_step` exists.
The sim reads the chords from button bits: `FighterGrabRules.ChordPressed`
(`BasicButton | BlockButton` held) and `TryStartEchoStep` (`BlockButton && RollButton`).
`GameplayButtons : ushort` uses bits 0–11 (11 = the reserved `Dash`); **bits 12–15 are free**.
Networking is protocol **v2** (`RollbackInputPacket`, 47 B, one `PlayerInputFrame` of 12 B).
`CharacterState` ends at `Grabbing` — **`Thrown` is absent**.
`MatchMode { Stock, TimeLimit, Hybrid }` (`GameManager.cs:12`) has **implicit** values and Hybrid is
offered in `CharacterSelectScreen.cs:620–622` and `HolodeckConsolePanel.cs:99+`;
`FighterAudioRules.cs:20` treats Stock **or Hybrid** as stock-based;
`FighterMatchSystem.Update` (2280–2289) has a `_ =>` default arm that silently handles Hybrid.
`FighterMatchComponent.PlayerOneKOs` / `PlayerTwoKOs` count **KOs scored by** that player
(`TrackKnockouts` at 2380–2400 credits the *opponent*) and `FighterMatchSystem` picks
`PlayerOneKOs > PlayerTwoKOs ? 0 : 1` — i.e. **most KOs scored wins**, the opposite bookkeeping.
`FighterRuntimeComponent.KnockoutsSuffered` is the per-victim count and **is already snapshot state**,
so the right quantity exists; only the comparison, naming and HUD are wrong.
`FighterSimulationRules.ApplyStockLoss` (2429) is the single victim-side chokepoint and already
increments `KnockoutsSuffered` for **every** cause — the right hook.
`EnterSuddenDeath` (2309–2360) sets `CurrentHP = 1` for both, `TimerEnabled = 0`, clears orbs and
**forces hazards on** (`HazardsEnabled = 1`, frequency 3 if 0) even when house rules disabled them,
sets `DefyHistoryUsed = 1`, resets position/hitstun/daze/respawn/invuln/echo/hitstop/tumble/
echo-step-windup/shieldstun/block-lockout/ledge counters, and grants a phantom stock at 0.

**Exact change list.**

**1. The 31-sample ring.** Keep **31 consecutive position samples, one per authoritative 60 Hz
simulation tick**, recording frame `t` **before action input and movement**, and read the **exact
`t − 30` sample — no nearest-sample approximation**.
31 × `FPVector2` = 62 `FP64` = **496 B**, which cannot fit one 128-byte component. **Retire ID 311 and
author the bank at IDs 313–317** (five components, 8 samples each — §2.7), with
`PositionRingHead`, `PositionRingLatestTick`, `PositionRingValidCount`, `PositionHistoryGeneration`,
`EchoStepArmed` and `EchoStepActivationTick` on **component 312** (A1b's `FighterDefenseComponent`,
which A1c extends after A1b merges — coordinate the field additions and record it in §9).
**Reset history at spawn and recovery anchors**; filled spawn slots are **not** valid historical
frames, so **require 30 subsequent ticks before use** (`ValidCount >= 31`). Snapshot the **ring, head,
latest tick, valid count, generation, armed destination, activation tick, remaining wind-up and
`EchoStepCooldownFrames` together**; commit the **30 meter** and the **120-frame cooldown** once at
accepted activation. Ordinary movement teleports keep continuous history; pause adds no samples.
Mirror all of it in Story (`PlayerController`), and mirror the generation reset on death rewind and
checkpoint reconstruction. `tests/Determinism/` rollback-convergence suites must include the ring.

**2. Exact-or-no-teleport destination policy.** **Validate full-body clearance and kill/room bounds
before spending**; reject an invalid or unavailable target **without meter or cooldown cost**.
**Recheck the same target at wind-up completion**; if it became blocked, **cancel the teleport with
the committed cost retained**. No nearby substitute, no added immunity. Airborne destinations remain
legal.
Sim: a clearance predicate against `FighterStageGeometry` (walls, ceiling, blast zone, solid floor —
**including A9's floor segments** — and one-way platforms) using the fighter half-extents, called (a)
at activation → refuse with no cost, (b) at wind-up completion → cancel, cost retained.
Story: the equivalent via a `PhysicsShapeQueryParameters2D` test against `Environment` + room bounds,
respecting the `PhysicsCallbackGuard` rule if reached from a signal.

**3. Dedicated actions (C01c).** Add `gameplay_grab` and `gameplay_echo_step` to `project.godot` with
**direct slots Unbound by default** per device kind (A8 shipped the Unbound representation in Wave 1).
Add `InputManager.Actions.Grab` / `.EchoStep` and the two rows to `RemappableActions` **and** to
`InputBindingService.RestorableActions`.
`GameplayButtons`: **`EchoStep = 1 << 12`, `Grab = 1 << 13`** — these fit without changing
`PlayerInputFrame.SerializedSize` (12 B) or `RollbackInputPacket.SerializedSize` (47 B). A
direct-vs-preset **origin** flag needs bit 14 or a parallel byte. **The reserved Dash bit (11) is never
reused.**
The sim initiates grab and Echo Step from `Pressed(Grab)` / `Pressed(EchoStep)` **OR** the existing
enabled chord, **deduplicated once per actor per tick**, **never synthesizing component presses**.
Both routes "request the same verb once". Disabling a shortcut restores normal component behaviour;
assigning a direct bind must not auto-disable the shortcut. A refused direct action does nothing and
spends nothing. A direct binding grants **no** extra priority, cancel, leniency or resource bypass.
**Protocol v3:** bump `RollbackProtocol.ProtocolVersion` when the origin flag lands; carry
direct-vs-preset origin and the component candidates for the illegal-chord fallback so peers replay
normalized input and **never re-recognize a remote chord with their own shortcut settings**.
Serialize the resulting **logical action requests**, not local key codes.

**4. Same-frame chord priority.** One shared resolver — `BasicComboRules.SelectRecoveryVerb(blockHeld,
blockPressed, rollPressed, basicPressed, grabLegal, echoLegal)` — consumed by **both** modes so the
table cannot drift. In **attack recovery**, where Echo Step and the block-cancel are both legal:
- same-frame **Block + Roll** → **Echo Step** (the priced verb wins over the free stance);
- **Block alone** → the block-cancel;
- same-frame **Block + BasicAttack** in recovery → a **grab attempt** where grabs are legal, otherwise
  the block-cancel.
One rule: **the chord beats the single input, and the priced verb beats the free one.**
*(Today ordering is incidental: `FighterMovementSystem` calls `TryStartEchoStep(...)` at `:371` then
evaluates roll with `allowRoll: !attacking && verb.EchoStepWindupFrames == 0 && !shieldStunned` at
`:386`, so Echo Step wins over Roll by accident; the grab chord is evaluated independently in
`FighterCombatSystem` at `:1635`. Story calls `TryStartEchoStep()` from `PlayerController:1218` and
`:1410` and `TryStartGrab()` from a third site.)*

**5. `CharacterState.Thrown` (F23).** Append **after** `Grabbing`; `Dashing` keeps its ordinal.
Add the victim transition (grab-connect → throw launch). Add `GrabPartnerPlayerID` and a
`ThrowDamageApplied` once-only flag to the sim's grab state — the partner is resolved positionally
today by the two-fighter systems, and there is no explicit once-only damage guard beyond the phase
machine. Update the canonical-enum documentation.
Add a pin that `IsBlockStance` is **false for the entire grab including whiff recovery**.

**6. F21 mode consolidation.** Make the enum values **explicit**: `Stock = 0`, `TimeLimit = 1`,
`Hybrid = 2` marked `[Obsolete]` / reserved (§2.8). **Remove Hybrid from both selectors**
(`CharacterSelectScreen`, `HolodeckConsolePanel`), fix `FighterAudioRules.cs:20`, and replace the
`_ =>` default arm at `FighterMatchSystem.Update` (2280–2289) with an **explicit Stock fallback**.
**Time mode = fewest stocks lost.** Each player gets a nonnegative `stocksLost` counter starting at 0;
**+1 on every actual stock loss regardless of cause** (opponent damage, pit, hazard, self-KO, DoT, a
surviving projectile or construct). **No attacker credit, no ownership window, no self-KO penalty.**
A hit prevented by Defy or invulnerability is **not** a loss. Co-occurring lethal damage and a
blast-zone crossing is **one** increment. Applied **atomically with the living→KO transition, before
respawn**; duplicate callbacks cannot repeat it. The counter is **rollback snapshot and hash state**;
resimulation replaces it rather than adding to an external counter. Respawning never resets it; a new
match or rematch starts at 0. Resolve all KOs in a regulation tick before comparing; the final active
tick settles both players' losses first. **Fewest wins; equal totals (including 0–0) enter Sudden
Death with no HP tiebreak.**
Implementation: rename the match fields to victim-side `PlayerOneStocksLost` / `PlayerTwoStocksLost`
(a snapshot field rename — re-run the rollback hashing suites) fed from `KnockoutsSuffered`, and
**freeze them at regulation end**.
**Stock mode** keeps: limited stocks (default 3, range 1–5); the timer default 480 s, configurable
**including Off**; expiry compares remaining stocks, then remaining HP **as a fraction of that
fighter's max HP** with unreclaimed Rally echo excluded; a true tie → Sudden Death, as does losing both
final stocks in the same regulation frame.
**`TimeLimit` seconds: 0 means Off in Stock only.** Time **requires** a positive timer and Off is
unavailable; switching untimed Stock → Time selects **480 s** and shows it before launch; characters,
stage, items and hazards are unchanged when switching. Label the second choice **Time** while retaining
the internal `TimeLimit` identifier.
**Migration:** `SavedMatchSettings.Mode` is a raw int in the global payload. A recognized saved Hybrid
normalizes to **timed Stock** preserving a valid positive timer and stock count; a Hybrid timer that is
Off/missing/nonfinite becomes **480 s**, shown repaired before launch; an invalid/missing stock count
clamps to 1–5 defaulting to **3**; unknown mode values are **not** Hybrid (preserve the source and
require a valid selection). Normalization is **idempotent** and runs before displaying loaded settings
or creating the next match, and **never changes a running match**.
Retire `fighter_mode_hybrid` (`en.csv:277`) through the recorded-orphan process (A1c owns that row).

**7. F22 Sudden Death.** Rewrite `EnterSuddenDeath` as a shared **round-reset** routine (the countdown
path reuses it). The full table:

| Aspect | Required |
|---|---|
| Living fighter HP | **Preserve exact current positive HP**, including unequal HP in a Time tie. No heal, no normalization, **no clamp to 1** |
| Dead / respawning fighter | **Normal Fighter respawn HP**, resolved once from committed end-of-regulation life state; no extra stock loss, no change to frozen totals, no new spawn protection |
| Position / motion | Authored **match spawn points**, zero velocity, released ledge and grab attachments |
| Action state | Cancel attacks, grabs, throws, hitstun, daze, hitstop and the armed Echo Step; neutral with **empty input buffers**; no queued attack fires after the restart |
| Meter and cooldowns | **Meter 0**; Special, movement and Echo Step cooldowns **ready**; no pending Ultimate |
| Block | Refill baseline charges; clear shatter lockout, shieldstun and the regen countdown |
| Movement resources | Reset jumps and ledge regrab; **initialize a new Echo Step history generation at the spawn with one valid sample** and require **30 subsequent ticks** before use; never teleport into regulation history |
| Status / items | Clear **both** status slots, marks, Root/tethers, timed orb buffs and **Aegis** |
| Rally | Clear the echo pool and pending echo-meter accounting; ordinary rules at retained HP, **no Overtime multiplier** |
| Defy | **Disabled throughout the phase**, meter irrelevant; **preserve its spent flag**; the HUD seal shows Unavailable; **no new use consumed on entry** (A1b's derived `SuddenDeathDefyDisabled`) |
| Projectiles / constructs | **Remove every** regulation projectile, zone, summon, construct and temporary ability platform, including orphans; clear pending damage and events |
| Arena and hazards | Restore authored round-start state. **Respect the match hazard toggle — Off stays Off.** If On: the first full warning after play resumes, **twice-normal cadence by halving idle and recovery** while retaining warning and active durations, and **never multiplied again** by regulation Overtime |
| Orbs | Remove existing, disable spawning and collection *(already correct — `FighterEntitySystems.cs:1726–1728`)* |
| Respawn protection | Clear prior platforms and invulnerability and old attack/roll invulnerability; a common ready countdown freezes both; **no new spawn invulnerability once play resumes** |
| Match state | No timer, no stock pool; one decisive life each; **freeze regulation stocks-lost totals for results**; reset CPU action plans and opportunity tracking **without changing difficulty**; the match PRNG continues |
| Ending | After each active tick resolve all real deaths; one dead fighter loses; two in the same tick is the existing Draw; damage, falls, hazards and self-KOs are equivalent; a nonlethal hit, a legal block or an invulnerable contact does not finish it; **no respawn of a Sudden Death victim** |
| Presentation | A common match-start ready countdown showing **"Sudden Death — next death loses"** and the retained HP bars; controls and sim clocks frozen during the transition and countdown, resuming together |

Add a `SuddenDeathCountdownFrames` phase reusing `FighterMatchFlowRules.CountdownFrames`, entity
sweeps for projectiles/zones/constructs, and the frozen regulation totals plumbed into
`FighterMatchResult` and `MatchResults`. **Commit the phase transition and match state together**
in snapshots and hashes with **phase and life generations and result identity**; discard late
regulation events **by phase identity**; log the result exactly once.
**Byte budget:** frozen totals and the phase generation go on **component 319
`FighterSuddenDeathComponent`** (§2.7) or `FighterMatchComponent` (which has room); the Defy disable
is **derived** from `match.SuddenDeathActive` at the read site rather than stored per fighter.
`FighterRuntimeComponent` is exactly full — add nothing there.

**Files owned (exclusive).** `scripts/FighterSim/FighterSimulationSystems.cs` (**`FighterMatchSystem`,
`FighterCombatSystem`, the Echo Step paths — A9's `FighterMovementSystem` floor work landed in Wave 1**),
`scripts/FighterSim/FighterSimulationComponents.cs` (**IDs 313–317, 319, and the 312 extensions after
A1b**), `scripts/Core/GameManager.cs` (`MatchMode`), `scripts/Core/PlayerInputFrame.cs`,
`scripts/Networking/RollbackProtocol.cs`, `scripts/UI/MatchResults.cs` (**the totals lines only** —
A8 owns the rest), `scripts/Core/FighterAudioRules.cs`.

**Shared files touched additively.** `project.godot` (two new actions),
`scripts/Core/InputManager.cs`, `scripts/Core/InputBindings.cs`,
`scripts/Characters/PlayerController.cs` (**region: `CharacterState.Thrown`, the Story Echo Step ring
and destination validation, the chord resolver call** — see §5),
`scripts/Combat/BasicComboRules.cs` (`SelectRecoveryVerb`),
`scripts/UI/CharacterSelectScreen.cs` / `scripts/UI/HolodeckConsolePanel.cs` (drop Hybrid),
`scripts/Core/SaveManager.cs` (the `SavedMatchSettings` normalizer), `localization/en.csv`.

**Tests.**
- **Rewrite:** `tests/Determinism/FighterMatchFlowTests.cs` — `TimerExpiryWithIdenticalStateEntersSuddenDeathNotADraw`
  (364), `SimultaneousFinalStockKOEntersSuddenDeath` (520), `SuddenDeathSpawnsAndAwardsNoOrbs` (627),
  `ATimeLimitKnockoutRaisesTheKnockoutCounterWithoutTouchingStocks` (393) and
  `MatchSettingsSurviveTheMappingIntoDeterministicRules` (741) all pin retired behaviour. **±0**, plus
  **+12** new F22 cases from the verification matrix (unequal 20-vs-70 HP retained; a 0–0 Time tie;
  hazards Off stays Off; hazards On gets a full first warning at twice cadence with no stacked
  acceleration; pending projectiles, constructs and statuses cleared; a full and a used Defy both show
  Unavailable with the spent flag intact; old respawn invulnerability cleared; simultaneous deaths draw;
  rollback across entry; frozen totals reach the results screen; meter 0 and cooldowns ready; the
  new Echo Step generation requires 30 ticks).
- **Rewrite:** `tests/unit/MatchSettingsTests.cs`, `tests/unit/CharacterSelectSceneTests.cs`,
  `tests/unit/HolodeckConsoleTests.cs`, `tests/unit/FighterAudioRulesTests.cs` — all pin Hybrid. **±0**,
  plus **+4** normalization cases (Hybrid → timed Stock; an Off/nonfinite Hybrid timer → 480 shown
  repaired; an invalid stock count → 3; unknown values are not Hybrid).
- New `tests/Determinism/EchoStepRingTests.cs` **+8** — 31 consecutive per-tick samples; the sample is
  taken **before** input and movement; the destination is the **exact** `t−30`; `ValidCount` gates
  activation; the generation resets at spawn/respawn/stock-loss/match-start; pause adds no samples;
  the ring round-trips through snapshot and hash; rollback convergence with the ring included.
- New `tests/unit/EchoStepDestinationTests.cs` **+5** — an invalid destination at activation refuses
  with **no** meter or cooldown cost; a destination that becomes blocked at wind-up **cancels with the
  cost retained**; no nearby substitute; airborne destinations are legal; a floor-segment pit
  destination is rejected.
- New `tests/unit/RecoveryChordPriorityTests.cs` **+4** (sim + Story) — all four same-frame
  combinations with grabs legal and illegal.
- Extend `tests/unit/FighterGrabTests.cs` **+3** — the direct `gameplay_grab` bit and the chord
  produce one request; `CharacterState.Thrown` and its transition; `IsBlockStance` false for the whole
  grab.
- Extend `tests/unit/InputBindingSchemaTests.cs` **+2** — the two new actions round-trip and restore.
- New `tests/unit/StocksLostTests.cs` **+6** — every cause increments; Defy and invulnerability do
  not; a co-occurring lethal hit and blast-zone crossing counts once; duplicate callbacks cannot
  repeat; fewest wins with no HP tiebreak; 0–0 enters Sudden Death.
- Re-run `tests/Determinism/RollbackReadinessTests.cs` — the component retirement and the field rename
  change every hash. **No test pins a golden hash** (they compare run-to-run), so this is safe; any
  agent who finds a hash literal reports it in §9 rather than updating it silently.

**Expected count delta: +44.**

**Acceptance criteria.**
- Echo Step lands on the position the fighter occupied **exactly** 30 ticks earlier, every time.
- Echo Step never teleports into terrain or outside stage bounds, and a refused activation costs
  nothing.
- Grab and Echo Step each work from a direct bind with the chord disabled, and from the chord with the
  direct bind Unbound, without ever double-firing.
- Hybrid is unselectable and a saved Hybrid loads as timed Stock with a valid timer.
- Sudden Death starts with both fighters at their real HP, hazards off if the house rules said off.
- `RollbackReadinessTests` passes across all ten stages after the component changes.

---
### A3b — Act III gauntlet, attempt state, load routing

**Goal.** Ship the Act III gauntlet — the Resonance Hold clock, the Warden Beacon, anchor charges,
Anchor Snap, the Smothered Game Over and no-hub chaining — on top of the F10 `AttemptState` record with
status-driven load routing, plus the N05 ending selection.

**Design references.** `design-godot.md` §3 Act III (master 225–522). Contracts:
`docs/design-contracts/STORY_PERSISTENCE.md` **in full** (F10),
`CHECKPOINT_RECOVERY.md` (F11), `NARRATIVE_RESOLUTION.md` (N05), `DUST_ECONOMY.md` (F02).
Recon: **B §B4, §B7 (ending half), §B8**, **E §4.3, §4.5, §4.6, §6.4, §6.5**, **H §H-32, §H-33**.

**Current state.** `StoryManager.BeginTimelineCollapse(checkpointID)` (498) applies the dust penalty,
refills rewinds, saves, and calls **`ReturnToHub()`**; `RestartCollapsedLevel(bool
resumeFromTimelineAnchor)` (520) drives `TimelineRestartPanel` in the hub. That whole hub-extraction
flow is **Acts I–II only** under the new design. `SessionExitGuard.CalculateExitRetainedDust` is
`×8/10` (the 20% fee) — **the number is right**; A3 already retired its abnormal-exit application.
Levels 13/14/15 each build **three** checkpoints and call `BuildExtractors(ExtractorPlacements)` with
real `ChronalExtractor` instances, not conduits/valves/pylons. **No Hard-specific checkpoint gating
exists anywhere** (grep for "Middle" finds only Level 10 geometry names), so "Hard's middle checkpoint
is inert" is also unimplemented in Acts I–II — **A3 landed the role machinery in Wave 1; A3b enforces
the Act III exception.** No Beacon, no anchor charges, no Snap, no Game Over screen; `scenes/ui/` has
no Game Over surface. `AdvanceToNextLevel()` + `ReturnToHub()` are the only inter-level path.
`StorySaveData` per-attempt state is a **flat scatter of root fields** added in V7.3:
`ActivatedCheckpointIDs`, `FontUsesConsumed`, `DestroyedExtractorIDs`, `FoundSecretIDs`,
`LevelIntegrityPercent`, `ViewedDialogueIDs`, `HasSeenCollapseBeat`, plus `LastCheckpointID`,
`CurrentHP`, `CurrentLives`, `CurrentUltimateMeter`. `StoryManager` mirrors them in in-memory
`HashSet`s with `WriteAttemptStateToSave` (378) / `RestoreAttemptStateFromSave` (389) /
`ClearLevelAttemptState` (368). Resume detection is `IsMidLevelResume` (218): an active save parked on
this scene path with a non-empty `LastCheckpointID`. **There is no attempt ID, no status enum, no
revision counter, no reward-source ledger, no recovery-event record.**
`StoryLevelControllerBase.RestoreSavedCheckpoint` (419) restores **checkpoint-time** HP and meter
(`Player.RestoreStoryCheckpoint(position, save.CurrentHP, save.CurrentUltimateMeter)`) — exactly the
behaviour F10 supersedes. Writes are **synchronous**, with no `revision` field and no failure gate on
scene transitions. `EndingThresholdPercent` has **no consumer at all**; `StorySaveData.IntegrityByLevel`
is written by `StoryManager.RecordLevelResultToSave` (611–622) but never read for an ending.

**Exact change list.**

**1. `StoryAttemptState` (F10).** New `scripts/Core/StoryAttemptState.cs` + `StoryAttemptStatus` enum
(`Active | AwaitingHubResume | RecoveryPending | Smothered | CompletionPending | Completed |
LegacyRecoveryRequired`). Fields: `attemptID`, `levelID`, `revision`, `status`,
`anchorChargesRemaining` (0..cap, zero outside Act III), `defyHistoryUsed`, `checkpointRecord`
(anchor ID + **role**, baseline encounter IDs + version, activated checkpoint IDs, granted-benefit
IDs), `currentIntegrity` + **immutable** `startingExtractorCount`, `destroyedExtractorIDs`,
`foundSecretIDs`, `puzzleStateByID`, `preBossLocked` + `finalGateIntegrity`, `fontUsesByID`,
`collectedHealingIDs`, `pendingHealing` (source ID, uncredited amount, remaining duration),
`rewardSources` (unissued / spawned-uncollected / collected + quantity + pickup type/location + the
fixed random-drop outcome), `playerResourceTimers` (block charges, regen/lockout, ability and Echo Step
cooldowns, Rally uncredited meter, **Time Freeze remaining cooldown**, the D02d Wardenclyffe recharge
delay), `recoveryEvent` (unique ID, cause, resolved HP/rewind/anchor/wallet/Integrity outcome, plus the
F11 checkpoint/role, difficulty, budget version and resolved minimum), `sealReadiness`,
`completionTransaction`, `appliedScriptEffectIDs` + `pendingGlobalSeenIDs`, `presentationFlags`.
Migrate the seven loose V7.3 root fields into it — they are the trustworthy existing records F10 says
to derive from. **Never default an active legacy attempt to full anchors / unused Defy / unused
healing / unclaimed rewards**; if essential history cannot be reconstructed, retain the original save
and balances and mark `LegacyRecoveryRequired`, telling the player an explicit Restart Level is needed.
`attemptID` is minted **only** on fresh entry and full Restart Level; `status` transitions are
**committed before presentation**; `revision` is monotonic.

**2. Durability and ordering.** Ordinary load restores the **latest durable** HP / meter / Integrity,
**not** checkpoint values — change `StoryLevelControllerBase.RestoreSavedCheckpoint` accordingly.
`checkpointIntegrity` (A3's field) is **only** the paid-recovery allowance. A granted **timer**
Collapse or Snap applies `max(checkpointIntegrity, recoveryMinimum)` **once**. A locked boss clock
stays locked on reload. Restart Level resets the gauge to 100%. **Crash recovery charges nothing**
(A3 already removed the boot fee). Smothered persists **before** its presentation. Critical events
commit before being acknowledged. **One ordered asynchronous writer per slot with revision checks**:
ignore obsolete revisions; on write failure retain the prior valid revision, report the failure and
**prevent a transition from pretending it saved**; add a `SaveFailed` notice key.
Add a **per-second continuous snapshot** of `playerResourceTimers` + HP/meter — between checkpoints
those are not durable today.
Wire the missing autosave triggers as **coupled transactions**: resource expenditure, Defy, death
recovery / Snap / Smothered, pickup and healing consumption, Extractor/secret/puzzle changes, grid
purchase and respec, voluntary exit, completion. Only three are wired today
(`OnCheckpointCommitted → SaveCheckpoint`, `OnLevelComplete → SaveLevelCompletion`,
`OnTalentNodeUnlocked → SaveTalentUnlock`, `SaveManager._Ready` 371–374).

**3. Load routing by status.** Drive the load destination from `attemptState.status`:
**Active** resumes at its checkpoint with spent resources intact; **AwaitingHubResume** loads the hub
in Acts I–II; **RecoveryPending** completes the pending recovery **once**; **Smothered** loads the
Game Over screen **without** HP/charge refill or a replayed failure fee; **CompletionPending** routes
forward. **None of these loads triggers fresh-entry resource initialization.** Quitting from an
already-settled failure or results screen does not charge again.

**4. Act III chaining.** Levels 13–15 play back to back with **no return to the Time-Ship**. The
level-results overlay and post-boss dialogue still play; there is simply no portal trip. Rewinds,
checkpoints and difficulty pools are unchanged. **On Hard all three Act III checkpoints are active** —
the inert-middle rule is suspended for the gauntlet. Steps 5–6 (Return Prompt, Hub Spawn) **do not
run** for Levels 13–14; the next level begins in-level and the completion auto-deposit is the only Act
III banking event. **The hub remains unreachable until campaign completion.**

**5. Warden Beacon (F02).** New `scripts/Environment/WardenBeacon.cs`, handed over at Level 13 entry
and interactable at **any activated Act III checkpoint**. It opens the **existing Repository screen**
(`ResonanceGridPanel`) in a **deposit-disabled mode** — **no new UI**. It **cannot deposit or spend
the active level's `levelChronalDust`**; the spendable balance excludes those earnings and the
checkpoint UI offers **no Deposit action**. `ResonanceProgression` needs a `spendableBalance` that
excludes `LevelChronalDust`. Current-level dust banks **once at level completion** ("sealed through
the Beacon") — L13 earnings become spendable at L14's Beacon, L14's at L15's. Restarting clears the
active level's undeposited dust under the existing rule. Free respec refunds only previously spent
**deposited** dust.

**6. Anchor charges.** The Beacon holds **Easy 3 / Normal 2 / Hard 1 per Act III level**. Initialized
**only** on fresh level entry or explicit full Restart Level — **never** on scene reload, hub return,
checkpoint activation, or an Anchor Snap. `StorySaveData.AnchorCharges` (§2.6). Publish
`EventBus.OnAnchorChargesChanged` (§2.9); **A8** renders the gold pips on the Beacon icon beside the
rewind counter.

**7. Anchor Snap.** Replaces Timeline Collapse's hub extraction in Act III. Triggered by an unprevented
lethal event with no death-rewind charge, **or** the Resonance Hold reaching zero (spending the final
rewind charge still completes that rewind). The hero snaps back to the last activated checkpoint **in
place** — rewind pool reset, full HP, the **same 20% undeposited-dust fee**. **Each Snap spends one
anchor charge; 1 → 0 still completes the Snap.** Presentation ~3 s, skippable after the first viewing;
Sarah: *"Your anchor held. Get up."*

**8. Smothered.** A collapse with **no charge left** → a ~5 s beat then a **Game Over screen** with
**Restart Level** (standard Restart rules: undeposited level dust cleared, charges refilled) and
**Quit to Menu**. Deposited dust — including previously completed Act III levels sealed through the
Beacon — is **always safe**. **F10: persist this terminal attempt state *before* its presentation.**
Quit and Load return to this same Game Over **without HP or charge refill and without a replayed
failure fee**. Only an explicit Restart Level creates a fresh attempt.
New `scenes/ui/GameOver.tscn` + `scripts/UI/GameOverScreen.cs`, themed with
`resources/UI/ftt_theme.tres`, focus-authored via `FocusChainBuilder.Apply`, raw translation keys as
the controls' `text`.

**9. Resonance Hold (the Act III clock).** Same gauge, rules, HUD and Tremor assets; the clock now
measures how long the hero holds their charge against the Unbound field. **Candle rule:** the full kit
at 1% exactly as at 100% — no mid-campaign power loss. Drain stand-ins each subtract **0.2 from the raw
factor exactly like an Extractor**: **L13 severed conduits**, **L14 cradle intake valves**,
**L15 firing-channel anchor pylons**. Replace the `ChronalExtractor` placements in Levels 13/14/15 with
these variants (same drain weight and the same F05 optional-dust allocation, different presentation and
localization). The PreBoss freeze still applies in L15. Tiers and the ending read identically.
**Act III Tremor variant:** the same effect in a different language — the hero's gold aura gutters
(the Suppression smother shader at partial strength, **never locking anything**), cold Unbound light
inward from the edges, conduit debris.

**10. N05 ending selection.** An equal-weight average of **15 timed levels — Levels 2–15 plus the
selected hero's one 4A**. Exclude untimed Levels 0 and 1 and the other eight 4A variants. Use each
level's **unrounded** PreBoss-locked final Integrity:
`endingAverage = sum(finalIntegrity over the 15 required level IDs) / 15`, and compare the
**unrounded sum against 750 percentage points**; display rounding cannot change the result. At ≥50%
the clean ending; below, the scarred ending. Identical on every difficulty.
Read from `StorySaveData.IntegrityByLevel` (write it from the PreBoss lock, not from completion) and
consume it in `CampaignCompletionSequence` / `Level15Controller`, committed **atomically with the L15
sealing transaction**. **A6 authored both ending variants' copy** — A3b selects between them.

**11. Baseline encounter IDs.** `checkpointRecord.baselineEncounterIDs` must be **authored per
checkpoint** across Levels 2–15 — enemy persistence rebuilds from the checkpoint's authored baseline,
then overlays broken Extractors, found secrets, solved gates, spent healing and reward claims.
Encounters beyond the baseline may respawn but **pay no claimed reward again**. **Do not infer baseline
membership from enemy position at death.** This is a content task across fourteen level controllers;
A3b authors the IDs mechanically from each controller's existing spawn tables and records the approach
in §9.

**12. Healing-loop persistence (F10 deltas).**
- **Checkpoint Mending:** commit the checkpoint's **granted-benefit ID** with the HP/rewind changes;
  loading or a paid recovery never grants it again. (`TryActivateCheckpoint` gates once-per-attempt in
  spirit today but stores only activated IDs, not granted-benefit IDs.)
- **Restoration Font:** persist the use decrement **and the pending heal** together at channel
  completion; an ordinary reload resumes only **uncredited** healing; a death recovery that replaces HP
  **cancels pending healing without refunding** the use. (`FontUsesConsumed` exists; `pendingHealing`
  does not.)
- **Chronal Feast / Salve:** persist the **pickup ID** and the HP benefit together; **issued
  random-drop outcomes are retained across reload rather than rerolled**. (`collectedHealingIDs` and
  the fixed drop outcome do not exist.)

**13. Paid recovery on Collapse (Acts I–II).** → hub with status **AwaitingHubResume**, the **same
attempt**, a paid recovery (not a fresh attempt); the hub portal resumes at the last checkpoint with
the already-committed recovery; full Restart Level stays a separate explicit option. Player health on a
**paid** recovery only: full HP + the full difficulty rewind pool committed once, retaining meter,
Defy-used, healing uses, anchors and cooldowns; an ordinary quit/crash load retains the latest durable
HP and rewind count instead. Integrity: a paid **non-timer** recovery restores `checkpointIntegrity`;
a granted **timer** recovery uses `max(checkpointIntegrity, recoveryMinimum)` with the ≥25 floor.
Dust: `floor(levelChronalDust * 0.20)` subtracted **once** for the uniquely identified failure or
live-play exit; reopening a settled screen does not repeat it; claims stay spent even when dust is lost.

**Files owned (exclusive).** `scripts/Core/StoryAttemptState.cs` (new),
`scripts/Environment/WardenBeacon.cs` (new), `scenes/ui/GameOver.tscn` + `scripts/UI/GameOverScreen.cs`
(new), `scripts/UI/TimelineRestartPanel.cs` (Acts I–II only now),
`scripts/Environment/Level13Controller.cs` / `Level14Controller.cs` / `Level15Controller.cs`
(**the conduit/valve/pylon variants, the Hard three-checkpoint rule, the Act III tail**),
`scripts/UI/CampaignCompletionSequence.cs`.

**Shared files touched additively.** `scripts/Core/StoryManager.cs` (**the largest single claim in the
package — A3b effectively owns this file in Wave 2; see §5**), `scripts/Core/SaveManager.cs` +
`scripts/Core/SaveEnvelope.cs` (the attempt record, the async writer, the revision guard),
`scripts/Environment/StoryLevelControllerBase.cs` (**durable-resource restore + baseline encounter
IDs**), `scripts/Core/EventBus.cs` (`OnAnchorChargesChanged`),
`scripts/Environment/ResonanceProgression.cs` (`spendableBalance`), `localization/en.csv`
(the Beacon prompt, the Snap line, the Smothered copy, the Game Over buttons, the Act III clock and
conduit strings, `save_notice_write_failed`, under `# Package 11 A3b`).

**Tests.**
- **Rewrite:** `tests/unit/LevelAttemptPersistenceTests.cs` (4 cases) onto the attempt record. **±0.**
- New `tests/unit/StoryAttemptStateTests.cs` **+8** — the attempt ID is minted only on fresh entry and
  Restart; status transitions commit before presentation; the revision is monotonic and obsolete
  revisions are ignored; a write failure retains the prior revision and blocks the transition; the
  per-second snapshot; ordinary load restores latest-durable rather than checkpoint values; the
  migration to `LegacyRecoveryRequired`; **never** defaulting an active legacy attempt to full anchors.
- New `tests/unit/AnchorChargeTests.cs` **+5** — initialized only on fresh entry and Restart; a Snap
  spends exactly one including 1 → 0; a scene reload, hub return, checkpoint activation and Snap never
  refresh; Easy 3 / Normal 2 / Hard 1.
- New `tests/unit/SmotheredGameOverTests.cs` **+4** — persisted **before** presentation;
  reload-stable; Quit and Load return to the same Game Over with no refill and no repeated fee; only
  Restart Level creates a fresh attempt.
- New `tests/unit/WardenBeaconTests.cs` **+4** — excludes the current level's dust from the spendable
  balance; offers no Deposit action; the free respec refunds only deposited dust; reachable at any
  activated Act III checkpoint.
- New `tests/unit/ActThreeChainingTests.cs` **+4** — L13 → L14 → L15 with no hub trip; all three
  checkpoints active on Hard in Act III; the hub is unreachable until completion; the conduit/valve/
  pylon variants carry the Extractor drain weight.
- New `tests/unit/EndingSelectionTests.cs` **+4** — the 15 required level IDs; the unrounded 750-point
  comparison; the other eight 4A variants excluded; identical on every difficulty.
- New `tests/unit/RewardSourcePersistenceTests.cs` **+5** — collected claims survive death rewind,
  checkpoint resume, Anchor Snap, quit/resume and crash recovery; a restored enemy whose reward was
  collected awards zero; an uncollected pickup restores without duplicating.
- Extend `tests/unit/StoryHealingLoopTests.cs` **+4** — the granted-benefit ID; pending healing
  persisted and resumed uncredited; a death recovery cancels pending healing without refunding the use;
  a fixed drop outcome survives reload.
- Extend `tests/unit/TimelineCollapseBeatTests.cs` **+2** — the timer-caused collapse cause and its
  Sarah variant; the Act III branch routes to Snap rather than the hub.

**Expected count delta: +40.**

**Acceptance criteria.**
- A Level 13 death with no rewind charges snaps in place, spends one anchor, and charges the 20% fee
  once.
- A Level 14 death at zero anchors reaches a Game Over that survives a quit and reload without
  refilling anything.
- L13 → L14 → L15 never loads the hub.
- A Level 13 Beacon cannot spend Level 13's earnings.
- The ending selection reads exactly fifteen level IDs and compares an unrounded sum against 750.
- A crash mid-level loses no claimed rewards and charges no fee.

---

### A6b — Roster de-hardcoding and the two-colour visual grammar

**Goal.** Remove every load-bearing hardcoded roster enumeration except the deliberately deferred
`FighterCharacterID`, and codify the Unbound-cold / resonance-gold visual grammar in code with the
Level 0 ignition beat and the persistent hero aura.

**Design references.** `design-godot.md` §2 roster mandate ("the roster will grow — narrative and spec
text must never hardcode roster size or enumerate the cast in load-bearing ways"), §2 visual grammar.
Contracts: `NARRATIVE_RESOLUTION.md` N04/N05, `PRODUCTION_SCOPE.md` P03. Recon: **A §9 (A9), §10
(A10)**, **I §8.1 (the roster-agnostic scope wording)**.

**Current state — the load-bearing hardcodes.** All five are identical nine-element arrays
(`einstein, joan, leonardo, lincoln, cleopatra, tesla, shakespeare, mozart, pocahontas`):

| File:line | Symbol | Load-bearing? |
|---|---|---|
| `scripts/UI/MainMenu.cs:41-45` | `RosterIDs` — the comment literally says *"The locked nine-character roster"* | **yes** — drives the character-select grid |
| `scripts/UI/CharacterSelectScreen.cs:63-66` | `_characterIDs` | **yes** — Fighter select tiles |
| `scripts/UI/HolodeckConsolePanel.cs:29-32` | `RosterIDs` | **yes** — the CPU opponent list |
| `scripts/Core/SaveManager.cs:117-120` | `GlobalSaveData.UnlockedCharacters` default | **yes** — the persisted unlock set |
| `scripts/Combat/AbilityVisualLibrary.cs:14-17` | `RosterIDs` HashSet | yes — the VFX resolution gate |
| `scripts/FighterSim/FighterSimulationComponents.cs:7-17` | `enum FighterCharacterID { Einstein=0 … Pocahontas=8 }` | **yes, and hardest** — the deterministic sim's character identity; `FighterLoadoutFactory.cs:81+` maps strings to it and `FighterEntitySystems.cs` encodes `zoneTypeID = (int)FighterCharacterID.X * 10 + slot` in **~20 places** |

Benign: `MainMenu.cs:314` `session.SelectedCharacterID = "einstein"` and seven `?? "einstein"` fallbacks
(`StoryLevelControllerBase:344`, `Level00Controller`, `Level01Controller`, `HubWorldController:429`,
`FighterStageController:23`, `TestArenaController:12`). Inherent: `CharacterFactory.cs:18,229,264`'s
per-character colour map, ability switch and hitbox names — per-character behaviour, but it should key
off data rather than a `switch`.
**Tests that pin the number 9** (these block a roster addition):
`tests/ContentValidation/CharacterManifestTests.cs:15,22,23` (`InitialRoster.Length == 9`,
`ids.Count == 9`); `CharacterPresentationTests.cs:15,136,137,160` (`== 9` three times);
`ContentManifestTests.cs:20,22` (`Character` rows `== 9`, `ResonanceGrid` rows `== 9`); plus duplicated
roster arrays in `AbilityVfxAssignmentTests.cs:26`, `DialogueChirpPitchTests.cs:31`,
`DustEconomyTests.cs:20`, `Level12ContentTests.cs:38`, `Level13ContentTests.cs:44`,
`Level14ContentTests.cs:39`, `Level15ContentTests.cs:41`, and ~20 "the nine …" XML doc comments.
**The data side is already fine:** `resources/Content/content_manifest.csv` rows 32–40 are the
authoritative roster, queryable via `ContentManifest.ForCategory(ContentCategory.Character)`. That is
the seam.

**Exact change list.**

1. **One canonical runtime roster source.** `FTT.Core.CharacterRoster.IDs`, backed by the manifest and
   cached like `AuthoredResources`. Re-point the five load-bearing arrays at it.
   `SaveManager.GlobalSaveData.UnlockedCharacters` default becomes `new(CharacterRoster.IDs)`.
2. **Tests become count-agnostic.** Convert `== 9` to `== CharacterRoster.IDs.Count` or to
   cross-checks against the manifest, and replace the seven duplicated test arrays with the same
   source. **Keep exactly one test** that pins the manifest row count so an accidental deletion still
   fails — but phrase it as "the manifest and the code agree", not "there are nine".
3. **`FighterCharacterID` is a recorded deferral.** Do **not** de-enum it in this pass; it is the
   deterministic sim's identity and touching it risks rollback and protocol work. Write its contract
   into `AGENTS.md` (Phase C applies the edit) and open a ledger entry `DEFER-ROSTER-ENUM`:
   *append-only; a new member takes the next ordinal; `zoneTypeID = id*10 + slot` must stay unique;
   adding a tenth character is a protocol-affecting change.*
4. **`CharacterFactory`'s per-character `switch`** should key off data where cheap (the colour map is
   the obvious one — move it to `CharacterData`). The ability switch is inherent per-character
   behaviour; leave it and note it in §9.
5. **Visual grammar, codified in code not prose.** Add `UIPalette.UnboundCold` and
   `UIPalette.ResonanceGold` (or a small `FTT.Combat.ResonanceGrammar` static) and re-point
   `ChronalExtractor.DischargeColor` (currently `Color(0.2, 0.95, 1, 0.85)`, commented "Apex Archive
   cyan" — **cold = enemy machine is already correct, just mislabelled**), the Dust tier tints
   (`UIPalette.GoldBright`'s doc comment already says "used by the Dust tiers and hyper-armor shell",
   so Dust is accidentally on-grammar), the hero aura, and the rift/portal builders.
   Add a `UIThemeTests`-style pin: **no siphon / extractor / beam site defines its own cold colour
   literal.** That pin is the only thing that will keep this contract alive.
   Existing pigments: `UIPalette.cs:30,33,36,42` — `Cyan (0,0.9,0.9)`, `CyanDim`,
   `Gold (0.95,0.8,0.3)`, `GoldBright (1,0.92,0.35)`.
6. **Level 0 ignition beat (two-colour).** `Level00Controller.BuildFracturePresentation()` (line 525)
   currently builds a **violet/purple** `ChronalFracture` at `Color(0.5, 0.2, 0.9, 0.85)` with a
   `Color(0.4,0.1,0.8,0.25)` halo and a `Color(0.7,0.4,1)` label — a generic rift, not "the Unbound's
   beam cracking your nexus", with **no gold ignition** and **no side-by-side Warden portal**.
   Rebuild the beat sequence: **cold beam cracking the nexus → gold ignition on the hero → the beam
   breaking → the Warden portal (clean steady geometry) beside the Unbound rift (a jagged tear)**.
   A5 authored the phase structure in Wave 1; A6b fills in the treatment.
   **Two kinds of door** is the contract: Unbound rifts are jagged cold tears; Warden portals are clean
   steady geometry — established side by side in Level 0's final beat.
7. **Persistent hero aura.** A **warm-gold aura the hero keeps for the entire game**, implemented as a
   **fourth arbitrated channel or a base tint** in `GlowPresentationController` — **not** an override a
   status effect would clobber. The controller's existing three-channel split (base tint, tint
   override, arbitrated outline stack) is designed for exactly this, and A8's F24 split in Wave 1 has
   already separated ownership from effects. Suppression's smother (A1) desaturates this channel;
   the Act III Tremor variant (A3b) gutters it at partial strength.
8. **Recurrence contract.** Every Chronal Extractor is Level 0 in miniature; every Dust pickup restates
   the origin; level-seal vignettes recolour the era warm; L14's Extraction Hall shows thin gold
   threads drawn off cold from each cradle. **A6b implements the Extractor and Dust halves** (they are
   colour re-points); the seal vignette and the Extraction Hall are **scene content A6/B-wave
   territory** — record the split in §9.
9. **Comment sweep.** ~50 XML-doc and inline comments say "Apex Archive" / "the cult" —
   `ChronalExtractor.cs:198`, `Level02Controller.cs:12,135`, `Level03Controller.cs:81`,
   `Level05Controller.cs:12,35`, `Level06Controller.cs:33,131`, `Level07Controller.cs:12,44`,
   `Level08Controller.cs:134,193`, `Level09Controller.cs:83,98`, `Level10Controller.cs:11,173`,
   `Level11Controller.cs:185,265`, `Level12Controller.cs:13,193,211`, `Level13Controller.cs:14,254,276`,
   `Level14Controller.cs` (7 hits), `Level15Controller.cs` (18 hits),
   `scripts/UI/RewindPresentationOverlay.cs` (2 hits). Cosmetic but cheap. The `CultistEnemyID`
   constants in `Level07/09/12/13Controller` point at `chrono_slasher` — **the ID stays**; only the C#
   const name and its doc read "Cultist". Rename only if cheap.

**Files owned (exclusive).** `scripts/Core/CharacterRoster.cs` (new), `scripts/UI/UIPalette.cs`,
`scripts/UI/MainMenu.cs`, `scripts/UI/CharacterSelectScreen.cs`, `scripts/UI/HolodeckConsolePanel.cs`,
`scripts/Combat/AbilityVisualLibrary.cs`, `scripts/Environment/Level00Controller.cs`
(**the Part 1 presentation only — A5 owns the calibration steps; A6b lands after A5**).

**Shared files touched additively.** `scripts/Core/SaveManager.cs` (the `UnlockedCharacters` default),
`scripts/Combat/GlowPresentationController.cs` (the aura channel — **after A8's F24 split**),
`scripts/Environment/ChronalExtractor.cs` (the colour re-point), `scripts/Characters/CharacterFactory.cs`
(the colour map), the ~15 level controllers (comments only), `localization/en.csv` (none expected).

**Tests.**
- **Rewrite:** `CharacterManifestTests`, `CharacterPresentationTests`, `ContentManifestTests` and the
  seven suites with duplicated roster arrays — all become count-agnostic. **±0.**
- New `tests/unit/CharacterRosterTests.cs` **+3** — the roster comes from the manifest; the five
  consumers agree with it; adding a manifest row would flow through without a code change (simulate
  with a test manifest).
- New `tests/ContentValidation/ResonanceGrammarTests.cs` **+3** — no siphon/extractor/beam site
  defines its own cold colour literal; Dust tiers use the gold constant; the Level 0 beat uses both
  constants and no violet.
- Extend `tests/unit/GlowPresentationControllerTests.cs` **+2** — the hero aura survives a status
  start and expiry; Suppression smothers it and restores it.

**Expected count delta: +8.**

**Acceptance criteria.**
- `grep -n '"einstein", *"joan"' scripts/` returns nothing outside `CharacterRoster` and the sim enum.
- No test asserts the literal number 9 except the single manifest-count pin.
- Level 0's opening reads cold-beam → gold-ignition → two doors, with no violet.
- The hero's gold aura is visible at Level 15 and survives every status.

---

### A7a — The Eraser elite

**Goal.** Author the Eraser (Story-only Future Cultist elite) with the Null Lance and the Siphon Snare
channel, the Basic-class override the Lance needs, and its placements.

**Design references.** `design-godot.md` §6 (master 2899–3032, especially 2926–2931 and 2949), F14
(master 1209 + 2928). Contracts: `docs/design-contracts/LEGACY_CHECKPOINTS.md` (the 4A debut),
`DUST_ECONOMY.md` (no flat award). Recon: **G §1, §2 in full**, **D §6.3 (D-28)**, **I §5.3**.

**Identity.** Future Cultist **elite**, **Story-only**. **~190 HP**, **stun resistance 0.5**, behaviour
**Chase**. **"Ignores siphon-defence positioning": its aggro radius is the whole room** — unique among
elites. Enemy Stagger Discipline (V7.4) applies as to any elite. It "draws its authored share of the
required encounter budget (F05); **no flat 20-dust award**". Two elite abilities, alternating (Null
Lance / Siphon Snare). Visual: lean, visored, null-lance, with a **cold-white** lance tip in the
two-colour grammar.

**Current state.** Nothing named Eraser exists as an enemy; `grep -rni eraser` hits only the two
**bosses** `tidal_eraser` (L5) and `apex_eraser` (L15) — an unrelated name collision that must not be
conflated. The **Chrono-Warden is the exact worked template**:
`resources/Enemies/chrono_warden.tres`, `resources/Enemies/Abilities/chrono_warden/dilation_field.tres`
(`Archetype = 8` `PersistentFieldAtTarget`), `phase_skip.tres` (`Archetype = 7` `Teleport`),
`chrono_bolt.tres` (`Archetype = 1` `Projectile`), manifest row 115, keys at `en.csv:193,194,195,196`,
the contract test `tests/unit/TimelineIntegrityTests.cs:200-224`
(`TheChronoWardenMatchesItsAuthoredEliteContract`), and behaviour cases at
`tests/unit/EnemyControllerTests.cs:229, 259`. The Warden **reuses**
`resources/SpriteFrames/Enemies/chrono_guard_elite_frames.tres` for its body art but has its own
`chrono_warden_ability_vfx_frames.tres` — the Eraser may do the same.
Pools use **generic tier pools** (`standard_enemy`, `elite_enemy`), so **no new pool ID is needed** —
only `WarmUpCount` on `elite_enemy` may need a bump. Current warm counts: L02–L04 = 3, L05 = 4,
L06 = 3, L07 = 4, L08 = 3, L09 = 4, L10 = 3, L11 = 4, L12 = 5, L13 = 3, L14 = 5, L15 = 6;
`MaxCapacity` is 16 everywhere.

**Blockers, and how this plan clears them.**
- **`DefaultBehavior` has no `Chase` value** — `scripts/Enemies/EnemyData.cs:11` is
  `{ Ground, Flying, StandGuard }`. The design's "Chase" is the *opposite* of `StandGuard` and is
  already the default `Ground` behaviour plus aggro. **Ruling: author `Behavior = 0` (Ground) with a
  room-scale `AggroRadius ≈ 3000` / `DeAggroRadius ≈ 4000` px** (against the Warden's 640/940). Do not
  append an enum value.
- **Elite signature abilities are force-flagged Guard-Crush**, which breaks the Null Lance.
  `EnemyController.BeginAttack` (632–641) passes
  `guardCrush: _lastAttackWasElite || (ability?.IsGuardCrushing ?? false)`, and
  `EnemyAbilityData.cs:66-72` documents "Elite-tier signature abilities are Guard-Crush **implicitly
  regardless of this flag**". The design says the Null Lance is **Basic-class vs block** ("blocking is
  never a trap"). **Add an explicit `ForcesBasicBlockClass` export on `EnemyAbilityData`** that
  overrides the elite implicit, threaded through `BeginAttack` **and** `TryReactivePhaseSkip` (line
  700, which also hardcodes `guardCrush: true`). It also flips the telegraph tint and glyph
  (`EnemyAbilityExecutor.TelegraphColor` / `ResolveGlyphShape`, 387–396) from orange/diamond to
  white-yellow/circle — exactly the design's intent.
- **`Suppression` is A1's** and landed in Wave 1. `StatusController.IsDamageStatus` already routes
  anything non-Venom/non-RadiantBurn to the **control slot**, so Suppression lands there with no edit.
  **Enemies ignore Suppression** — decide it explicitly rather than silently; add the no-op arm.
- **Time Freeze is A2's** and landed in Wave 1; the Snare's freeze-awareness uses
  `IStoryTimeFreezable`.
- **Dust:** `EnemyRosterContentTests.TierDustRewardsConformToTheEconomyAcrossTheWholeRoster` asserts
  elites are **exactly 10**. F05 (A10) retires that model. **A7a authors `ChronalDustDrop = 10` as an
  interim** so the existing gate passes, and **A10 re-points it at the reward manifest in the same
  wave**. Record the handoff in §9.

**Exact change list.**

**Resources.**
1. `resources/Enemies/unbound_eraser.tres` — **ID `unbound_eraser`**, chosen over bare `eraser` because
   the latter collides conceptually with the bosses `tidal_eraser` / `apex_eraser` and would muddy
   greps and the VFX-library parse. `Tier = 1` (Elite), `MaxHP = 190`, `StunResistance = 0.5`,
   `Behavior = 0`, `AggroRadius = 3000`, `DeAggroRadius = 4000`, `EliteAbilityCooldown` per the two
   abilities' CDs, `ChronalDustDrop = 10` (interim), `DisplayNameKey = "enemy_unbound_eraser_name"`,
   `SpriteFramesResource` → reuse `tech_enforcer_frames.tres` until art exists, `PrimaryAttack` →
   a new `lance_jab`, `EliteAbilities = [null_lance, siphon_snare]`.
2. `resources/Enemies/Abilities/unbound_eraser/null_lance.tres` — `Archetype = 1` (Projectile),
   `RangeClass = 1` (Ranged), `TelegraphFrames = 30`, `CooldownSeconds = 8.0`,
   `AppliedStatus = Suppression`, `StatusDuration = 2.0`, `StatusIntensity = 1.0`, a slow
   `ProjectileSpeed` ("a slow, visible bolt"), `TelegraphTint` cold-white,
   **`ForcesBasicBlockClass = true`**. **A blocked lance suppresses nothing.**
3. `resources/Enemies/Abilities/unbound_eraser/siphon_snare.tres` — `TelegraphFrames = 45`,
   `PulseRadius = 180` (3 units × 60 px/unit), `CooldownSeconds = 10.0`,
   `FieldDurationSeconds = 3.0` (the channel cap), `Damage = 0`, `HitstunDuration = 0`,
   `KnockbackForce = (0,0)`, `MeterDrainPerSecond = 10`, `MeterDrainCap = 30`.
4. `resources/Enemies/Abilities/unbound_eraser/lance_jab.tres`.
5. Optional `resources/SpriteFrames/Enemies/unbound_eraser_ability_vfx_frames.tres`.

**Code.**
6. `scripts/Enemies/EnemyAbilityData.cs` — the `ForcesBasicBlockClass` export; the Siphon drain fields
   (`MeterDrainPerSecond = 10f`, `MeterDrainCap = 30f`); **append `EnemyAbilityArchetype.SiphonTether = 9`**
   (§2.8) with its `HasValidArchetypeFields` case
   (`PulseRadius > 0 && FieldDurationSeconds > 0 && MeterDrainPerSecond > 0`).
7. `scripts/Enemies/EnemyController.cs` — honour the Basic-class override in `BeginAttack` **and**
   `TryReactivePhaseSkip`; a "channelling" gate in `ProcessAttacking` (only `ChargeDash` gets special
   treatment today, line 616) so the Eraser cannot move or select another attack while maintaining;
   wire the "successful stun/stagger interrupt" break, **distinguishing an armor-rejected hit**
   (`IsStaggerArmored`, line 235) from a real interrupt.
8. `scripts/Enemies/EnemyAbilityExecutor.cs` — the new archetype branch performing the **single
   attachment check** at active-start and constructing the channel node, plus an `IsChannelling` state.
9. New `scripts/Enemies/SiphonTetherChannel.cs`, modelled on `scripts/Enemies/DilationFieldZone.cs`:
   an `Area2D` in the `"persistent_construct"` group (so the freeze sweeps reach it), implementing
   `IStoryRewindSimulation` **and** `IStoryTimeFreezable`, with an owner reference, a target reference,
   a per-live-tick drain via `PlayerController.DrainUltimateMeter`, the accumulated-drain cap and the
   six break conditions. Use the group-sweep-by-distance pattern (deterministic under headless direct
   calls where no physics flush happens).
10. A per-player tether registry for the one-tether rule and the stable-ID tiebreak.
11. A **non-damaging block-absorption entry point** on `PlayerController`: one charge, shieldstun,
    shatter-if-last, **no HP chip**, distinct-block perk once. `BlockSystem.ResolveHit` assumes a
    damaging hit today.
12. `scripts/Combat/EnemyAbilityVisualLibrary.cs:15-28` — add `unbound_eraser` to `EnemyIDs`.
13. `scripts/Combat/RosterVfxMap.cs:69-99` — **`snare` and `siphon` are not in the verb vocabulary**;
    `enemy.unbound_eraser.siphon_snare` would fall through to the generic default and **fail**
    `RosterVfxMappingTests.EveryAuthoredPresentationEventResolvesToAnExplicitFamily`. Add
    `"snare"` / `"siphon"` / `"tether"` under the **Beam** family. `null_lance` already matches
    `"lance"` → Slash, which is arguably wrong for a projectile — add `"null"` under Beam **ahead of**
    Slash.
14. `scripts/Enemies/TelegraphGlyph.cs` has exactly three shapes (`Basic`/`GuardCrush`/`Unblockable`)
    resolved from the class flags. The Snare wants a **tether/drain glyph beside the Basic cue** — an
    **additive second glyph channel** — plus a drawn 3-unit boundary ring. The boundary and glyph must
    read **without colour or flashing**.

**The Siphon Snare contract (F14 Option A) — copy these numbers.**
Not a hitbox: a **cast → attachment check → channel**.
45-frame / 0.75 s telegraph · 3-unit (180 px) radius · 10 s cooldown · ≤ 3 s / 180 live frames of
tether · drain `min(currentMeter, 10 × liveDeltaSeconds)` per live tick **preserving fractions** ·
**≤ 30 points per cast** · **no** initial lump, no HP damage, no hitstun/knockback/movement lock, no
status-slot entry, no Rally echo, no damage-based meter, no hitstop. Transfers nothing to the Eraser.
**Attachment:** a single check at telegraph end; the living player within 3 units **centre-to-centre**
with **unobstructed line of sight through authored solid cover** (reuse `SearchlightZone`'s
`DirectSpaceState.IntersectRay` pattern against `PersistentObject` bodies with headless-safe null
guards). **Refused** if the player is Suppressed, at zero meter, already tethered, or invulnerable.
A miss or block creates **no lingering pulse**.
**Block:** a legal **grounded, front-facing** block at attachment prevents all drain and consumes
**one block charge** with the ordinary Basic block response (shieldstun; normal shatter if it was the
last charge). Zero-charge / locked / airborne block **cannot** absorb. **No HP chip.** Distinct-block
perks may trigger once. Raising block **after** attachment does not sever.
**Channel:** the Eraser is **stationary and cannot attack or use the Null Lance** while maintaining.
The player may act freely. Ends at zero meter.
**Break conditions, evaluated BEFORE each drain tick:** range > 3 units; blocked LoS; player
invulnerability **including active roll i-frames**; either actor's death or removal; a **successful**
stun/stagger interrupt of the Eraser (**an armor-rejected hit does not count**); or Suppression newly
applied to the player. A broken tether never reattaches during that cast.
**Overlap:** **one active Siphon tether per player**; simultaneous eligible attachments resolve by
**stable encounter/actor ID**; losers fail without drain. No stacked rate or duration.
**Cooldown:** starts on **accepted cast commitment** and runs 10 live seconds even after a miss, block
or interrupt; **no early refund**. No new commitment against a zero-meter / Suppressed / invulnerable /
already-tethered target.
**Freeze and recovery:** pause the channel, drain, cast and cooldown timers during Time Freeze and
other world freezes; on thaw re-evaluate break conditions **before** any drain; **no catch-up ticks**.
F10 checkpoint reconstruction ends the transient tether and retains the latest durable meter; a load
never repeats a tick or grants meter back. Level or room departure ends the tether.
**F13:** the seal becomes **Not Ready** when an unused full meter is drained; **Ready** again only on a
legitimate refill; an already-spent seal stays **Spent**. It can **disarm an unused full-meter Defy but
never consumes or resets the once-only flag.**
**Authored environmental drains survive unchanged:** the Extractor discharge **−20 points** and the
Paris Neural Dampening Beam **5 points/s**.
Alternation with the Null Lance is retained. **Multiple Erasers may not combine active Suppression
with an ongoing tether.**

**Manifest / localization / pools.**
15. `resources/Content/content_manifest.csv` — one `Enemy` row after line 115:
    `Enemy,unbound_eraser,6,res://resources/Enemies/unbound_eraser.tres,EnemyFactory,Implemented,ReadyForReplacement,Valid,true`.
16. `localization/en.csv` under `# Package 11 A7a`: `enemy_unbound_eraser_name`,
    `enemy_ability_null_lance_name`, `enemy_ability_siphon_snare_name`, `enemy_ability_lance_jab_name`,
    plus the tether glyph tooltip. **A6 owns the two Sarah ambush bark lines** (4A and L13).
17. `resources/Pools/level_pool_configs/level_{04a_*,07..15}_pool_config.tres` — bump the
    `elite_enemy` `WarmUpCount` where a placement pushes concurrency past the current warm count.

**Placements.**
18. `scripts/Environment/Level13Controller.cs` — add **two** Eraser entries to `SpawnTable` (currently
    `PhantomEnemyID` / `CultistEnemyID` / `EliteEnemyID = chrono_guard_elite`, lines 253–297) behind an
    authored ambush trigger, with the naming line.
19. **Salt Levels 7–15.** `Level07/09/12/14/15Controller` already carry `EliteEnemyID` constants;
    08, 10 and 11 build their spawn tables differently — inspect each individually.
    **Rule: never in the same room as a Chrono-Warden until Act III.** There is no encounter-composition
    validator today — add one as a content test over the level controllers' spawn tables.
20. **Close G8 in the same pass: the Chrono-Warden has never been placed in any level.**
    `grep -rln chrono_warden scenes/ scripts/Environment/ resources/Pools/` returns **nothing**; the
    only non-test consumer is `EnemyAbilityVisualLibrary`. V7.1 design law says "first appearance
    Level 6, then salted through Levels 7–15 alongside the Tech-Enforcer". Place it, so the two elites'
    placement rules are authored and tested together.
21. **4A debut:** A12 shipped the `{levelID}_eraser_debut` trigger type wired against a placeholder
    elite; A7a re-points it at `unbound_eraser`, and **B3 applies it across all nine variants**.

**Files owned (exclusive).** `resources/Enemies/unbound_eraser.tres` and its three ability resources,
`scripts/Enemies/SiphonTetherChannel.cs` (new), `scripts/Enemies/EnemyAbilityData.cs`,
`scripts/Enemies/EnemyAbilityExecutor.cs`, `scripts/Enemies/TelegraphGlyph.cs`,
`scripts/Combat/EnemyAbilityVisualLibrary.cs`, `scripts/Combat/RosterVfxMap.cs`.

**Shared files touched additively.** `scripts/Enemies/EnemyController.cs` (**A1 owned it in Wave 1;
A7a's channelling gate and Basic-class override are additive**),
`scripts/Characters/PlayerController.cs` (**region: the non-damaging block entry point only**),
`resources/Content/content_manifest.csv` (append), `localization/en.csv`,
the `Level07/08/09/10/11/12/13/14/15Controller` spawn tables, the pool configs.

**Tests.**
- New `tests/unit/EraserContentTests.cs` **+1** (mirroring
  `TheChronoWardenMatchesItsAuthoredEliteContract`): tier, HP, stun resistance, two elite abilities,
  archetypes, the Suppression status, telegraph frames, cooldowns.
- Extend `tests/unit/EnemyAbilityExecutorTests.cs` **+3** — the Null Lance applies Suppression on hit;
  applies nothing when blocked; is Basic-class (1 charge, circle glyph, white-yellow tint).
- New `tests/unit/SiphonSnareTests.cs` **+16** — transcribe the F14 validation matrix 1:1:
  front / rear / zero-charge / final-charge blocks; roll i-frames, cover and range boundaries;
  interruption versus armor rejection; starting meter 0 (the cast is rejected), 5 (drains to 0), 100
  (ends at 70 if uninterrupted); two Erasers resolving by stable ID; the Suppression break; Time
  Freeze pausing with no catch-up; checkpoint load ending the tether; Defy and Ultimate spend ordering;
  **absence from Fighter Mode**.
- **Counter updates that fail the moment the resource lands:**
  `tests/ContentValidation/EnemyRosterContentTests.cs:205` `AssertThat(standards + elites).IsEqual(28)`
  → **29**; `:131` `checkedKeys >= 118` → raise; `:41` `rows.Length >= 26` still passes but review;
  `tests/ContentValidation/EnemyRetroSpriteAssetTests.cs:56` `AssertThat(inspected).IsEqual(28)` →
  **29** (the Eraser's `SpriteFramesResource` must carry the six animations × 3 frames —
  `idle, patrol, attack, elite_attack, hitstun, death` — which reusing an existing sheet satisfies);
  `tests/ContentValidation/RosterVfxMappingTests.cs:64` `events.Count > 70` still passes but the
  explicit-family sweep is the real gate;
  `tests/ContentValidation/Level13ContentTests.cs:202-213` `EliteEnemyCount == 1` and
  `SpawnTable.Length == 9` break with two Erasers added (→ **3** / **11**).
- New `tests/ContentValidation/EncounterCompositionTests.cs` **+2** — no room pairs an Eraser with a
  Chrono-Warden before Act III; the Chrono-Warden appears in at least one room from Level 6 onward.

**Expected count delta: +22.**

**Acceptance criteria.**
- A blocked Null Lance costs one charge and applies **no** Suppression.
- A Siphon Snare against a 100-meter player who does nothing ends the player at exactly 70 meter.
- Rolling through the tether's i-frames breaks it before the next drain tick.
- The Eraser cannot move or attack while channelling.
- Nothing in `scripts/FighterSim/` can spawn an Eraser or apply Suppression.
- The Chrono-Warden appears in the campaign for the first time.

---
### A7b — Bosses: HP rows, renames, Mirror Paradox F20, First Unbound P2/P3

**Goal.** Apply the four V7.6 boss HP rows, land the display renames, give the Mirror Paradox its F20
campaign CPU profile, and build the First Unbound's Phase-2 self-rewind and Phase-3 Borrowed Legacies.

**Design references.** `design-godot.md` §6 (master 2899–3032) and the boss stat table (master
1611–1631); F20, T01b, V7.5 Borrowed Legacies. Contracts:
`docs/design-contracts/MIRROR_PARADOX.md`, `TEMPORAL_STATE_CONTRACT.md#first-unbound-capped-historical-recovery-t01b--option-a`,
`CPU_COMBAT_POLICY.md`. Recon: **G §3, §4, §5**, **D §16**, **A §4 (A4)**.

**1. Boss HP — apply only §2.2's four rows.** `borgia_inquisitor` 500 → **350**,
`siegemaster_duke` 540 → **520**, `chronal_inventor` 560 → **700**,
`revolutionary_tribunal` 590 → **850**. **Nothing else.** Rewrite
`tests/ContentValidation/BossRosterActITests.cs:57-67` per §2.2 and update
`tests/ContentValidation/EnemyManifestTests.cs:36` (500 → 350). Open the ledger entry
`VERIFY-BOSS-HP` with the full fifteen-row table from recon D §16.2 / G §5.

**2. Display renames (values only — §2.3).** `resources/Bosses/archive_prime.tres`
`DisplayName` → **The Forge Sentinel**; `apex_eraser.tres` `DisplayName` → **The First Unbound**.
`BossID`, `DisplayNameKey`, file names, manifest rows 131/132, sprite atlas paths,
`EnemyAbilityVisualLibrary.cs:29-30`'s `BossIDs` set, the five `apex_eraser` ability `AbilityID` /
`PresentationEventID` strings, and the boss-intro seen-set keys persisted in saves
(`StoryManager.RecordBossIntroSeen`, `scripts/Core/StoryManager.cs:484-489`) **all stay**.
**A6 already changed the `en.csv` values** — A7b only syncs the two non-localized debug fields.
The `BossData.bossName` doc example changes to "Unbound Overseer Vex".
Run the roster suites after the orchestrator's `--import`: `BossRosterActIIandIIITests` and
`CampaignLocalizationTests` resolve names through the **compiled** translation.

**3. F20 Mirror Paradox campaign CPU profile.** Most of the numbers are **already correct by
accident**; only the wiring is wrong.

| F20 parameter | Design | Repo today | Action |
|---|---|---|---|
| Reaction delay E/N/H | 30–45 / 15–20 / 4–8 frames | `FighterCpuController.GetReactionDelayBounds` (362–376) already matches | wire the tier |
| Block attempt E/N/H | 10 / 40 / 80 % | `CpuBandTuning.BlockPercent` already 10/40/80 | none |
| Block-through-hitstun, once per instance | 10 / 45 / 85 % | `HitstunDefensePercent` already 10/45/85 | none |
| Launching-hit DI | none / 40 / 80 % | `DiPercent` already 0/40/80 | none |
| HP from a 1000 base | **700 / 1000 / 1500** | `MirrorParadoxController.SpawnMirror:161` → `StoryDifficultyTuning.ScaleEnemyHP(1000, difficulty)` with 0.7 / 1.0 / 1.5 = **exactly 700/1000/1500** | none |
| Outgoing damage ×0.5 / 1.0 / 1.5 | required | `GetEnemyDamageMultiplier` = 0.5/1.0/1.5, but the clone is a `PlayerController` — **unclear whether it is applied at all** | **verify and wire** |
| CPU difficulty from **Story** difficulty | required | `MirrorParadoxDecisionAdapter`'s ctor hardcodes `new FighterCpuController(CpuDifficulty.Hard, seed)` (`MirrorParadoxController.cs:360`) | **fix** |
| **Full core kit on every difficulty** incl. Easy | required; "do not inherit Easy practice-CPU kit restrictions" | `CpuBandTuning.Easy` zeroes `SpecialOneClosePercent`, `SpecialOneRangedPercent`, `SpecialTwoPercent`, `MovementAbilityPercent`, `UltimatePercent` | **boss-override `CpuBandTuning` through the existing `CpuBandTuning? tuningOverride` ctor seam (line 113)** |
| Easy/Normal Ultimate at the first legal opportunity at full meter | required | `Normal.UltimatePercent = 100, RequiresUltimateSetup = false`; **Easy = 0** | fix Easy |
| Grab / Echo Step tiering | Easy none / Medium occasional / Hard tactical | **A9b's item**, not A7b's | cross-ref |
| Purchased grid perks mirrored **on Hard only** | required | `CreateCharacter(..., applyStoryProgression: false)` always — perks **never** mirrored | **fix**: on Hard call with `applyStoryProgression: true` **and** populate `StoryAbilityPerks` from the save's purchased nodes, while still **not** copying wallet, current HP, cooldowns or spent flags. **Confirm F20's "single application of stat modifiers"** against `EncounterMaxHPOverride` (line 171), which *replaces* the HP pool — a perk `MaxHP` bonus must not stack a second base pool |
| The Holodeck setting must not change it | required | already true | none |
| One phase, existing knockback setting, once-only physical dust | required | `PhaseCount => 1`, `mirror_paradox.tres PhaseThresholds = []`, 50 dust via `MirrorParadoxEncounterController` | **dust becomes 25 and physical — A10 owns it** |

`mirror_paradox.tres`'s melee/ranged thresholds (repo 2.0/5.0 vs the design's 3.0/8.0) and
`WeightedRandom` vs `DistanceBased` are **cosmetic** — the design says that metadata is "generic …
the F20 campaign CPU profile governs decisions", and `EnemyRosterContentTests` already exempts the
mirror from the distance-band rule. Leave them and record it.

**4. First Unbound P2 self-rewind (T01b Option A).** Once per encounter, on a **nonlethal** crossing of
66% HP: rewind its position by **3 s**, recover toward the sampled HP **capped at 20% of
difficulty-scaled max HP** (provisional), use the oldest real history if younger, validate
**historical position → current position → a safe authored anchor**, **suspend combat for 1.5 s**
preserving remaining timers and world state, spend **no player charge**, latch phase progression,
retain cooldowns and statuses, and resume with a **full attack telegraph**. **Lethal damage wins.**
A nonlethal crossing of 33% **queues P3 even if healing raises HP**. No discarded damage, no phase
regression, no repeat rewind.
Nothing exists: `BossController` has `CheckPhaseTransition` (552), `PhaseTransitioning` invincibility
(`IsPhaseInvincible`, 89), and `IStoryRewindable`/`IStoryRewindSimulation` **only** for the player's
rewind (`CaptureCheckpointState` 831, `ApplyStoryRewind` 838 — a checkpoint snap, not a rolling
buffer). There is **no position/HP history ring**, **no heal path** (`ApplyBossDamage` only subtracts),
and **no combat-suspend** other than the phase-transition state.
Build: a bounded position/HP ring on `BossController` (3 s = **180 frames**; the player's equivalent
`ChronalRewindBuffer` could be generalized), a once-per-encounter latch, a heal-with-cap path, the
three-tier destination validation, a 1.5 s suspend **distinct from `PhaseTransitioning`** that
preserves timers, and the 33%-queues-P3-regardless rule in `CheckPhaseTransition`.

**5. Borrowed Legacies P3 (§2.5).** Implement exactly §2.5's data-driven projection. The existing
phase-3 slot is already authored: `apex_eraser.tres` carries
`AbilityMinPhase = Array[int]([0, 0, 1, 0, 2])`, and **the only phase-2-gated ability is
`archive_remnants`** (`resources/Bosses/Abilities/apex_eraser/archive_remnants.tres`), a
`SummonMinions` archetype summoning `chrono_slasher` — **that is the current P3 identity and it is not
Borrowed Legacies.** Replace or augment it.
`BossData.BossAbilities : EnemyAbilityData[]` + `AbilityMinPhase : int[]`
(`scripts/Enemies/BossData.cs:43-47`) is consumed by `BossController.SelectAbilityIndex` (433) and is
a **static authored array**; nothing in `scripts/Enemies/` reads `CharacterData` or
`FTT.Combat.AbilityData` today. Add a **runtime composite kit** on `BossController` that appends the
dynamically-built projections when P3 opens, selected from the manifest roster minus
`GameManager.CurrentSession.SelectedCharacterID`.
Per-move cradle VFX: `AbilityVisualLibrary` already resolves every canonical ability ID for the Story
cast/projectile/zone paths — that is the asset-reuse lever. Each borrowed move should visually tear
from a specific cradle.
`en.csv` value for `boss_ability_archive_remnants_name` is already "Borrowed Legacies" (A6).

**Files owned (exclusive).** `resources/Bosses/*.tres` (the four HP rows and the two `DisplayName`
fields), `scripts/Enemies/BossController.cs` (**A1 owned it in Wave 1 for status slots; A7b's ring,
heal path, suspend and composite kit are additive**), `scripts/Enemies/BossData.cs`,
`scripts/Enemies/MirrorParadoxController.cs`, `scripts/Enemies/MirrorParadoxEncounterController.cs`,
`resources/Bosses/Abilities/apex_eraser/archive_remnants.tres`.

**Shared files touched additively.** `scripts/FighterSim/FighterCpuController.cs` (**the boss-override
`CpuBandTuning` only** — A9b owns the recovery planner), `scripts/Characters/CharacterFactory.cs`
(the Hard-only perk path), `docs/PACKAGE4_ROSTER_PLAN.md` §4.1 + a new §8 deviation entry.

**Tests.**
- **Rewrite:** `tests/ContentValidation/BossRosterActITests.cs` per §2.2. **±0.**
- Update `tests/ContentValidation/EnemyManifestTests.cs:36`. **±0.**
- Extend `tests/unit/MirrorParadoxTests.cs` **+6** — a per-difficulty matrix: reaction bounds, block
  rate, HP 700/1000/1500, the full kit on Easy, Hard-only perks with no double HP pool, independence
  from the Holodeck setting.
- New `tests/unit/FirstUnboundPhaseTwoTests.cs` **+8** — once per encounter; nonlethal 66% only;
  lethal damage wins; the 3 s / 180-frame ring; the ≤20% heal cap; the three-tier destination
  validation; the 1.5 s suspend preserving timers; 33% queues P3 even if healing raises HP.
- New `tests/unit/BorrowedLegaciesTests.cs` **+5** — for each of the nine possible active heroes the
  borrowed set is exactly the other `rosterCount − 1`; the archetype mapping by ability shape; damage
  from `BaseDamage` and telegraph from the authored startup frames; weighted-uniform selection; the
  projections are never written to disk or entered in the manifest.
- Extend `tests/ContentValidation/BossRosterActIIandIIITests.cs` **+1** — the two renamed display
  names resolve through the compiled translation.

**Expected count delta: +20.**

**Acceptance criteria.**
- Exactly four boss `MaxHP` values changed; `git diff resources/Bosses/` shows nothing else numeric.
- The Mirror Paradox on Easy fights with both Specials, its movement ability and its Ultimate.
- The Mirror Paradox on Hard mirrors purchased perks without stacking a second HP pool.
- The First Unbound rewinds once at 66% and never twice.
- Playing as Einstein, P3's borrowed set contains the other eight characters' Special 1 projections
  and never Einstein's.

---

### A9b — Fighter CPU: recovery profiles and verb policy

**Goal.** Replace the inverted recovery percentage ladder with the F19 tiered planner, author the nine
per-character recovery profiles, add CPU grabs and Echo Step per band, and make DI pit-aware now that
A9's pits exist.

**Design references.** `design-godot.md` §10 CPU AI. Contracts:
`docs/design-contracts/CPU_RECOVERY.md`, `CPU_COMBAT_POLICY.md`. Recon: **I §2.3–2.6**, **G §4.1**.

**Current state — and it contradicts on every tier.** `FighterCpuController.DecideRecovery`
(429–453) is a three-branch percentage ladder over `RecoveryJumpPercent` → `RecoveryMovementPercent` →
`RecoverySpecialTwoPercent`, with band values at `CpuBandTuning` 780–782 / 815–817 / 853–855:

| Tier | RecoveryJump% | RecoveryMovement% | RecoverySpecialTwo% | Design says |
|---|---|---|---|---|
| Easy | 60 | **0** | **55** | the movement ability **allowed once per episode**; **Specials forbidden** |
| Normal | 90 | **0** | **85** | one suitable ability, the movement ability is the **default**; Special 2 is *not* universal |
| Hard | 100 | 90 | 100 | route comparison, no fixed script |

**Easy and Medium are exactly inverted relative to F19** — they use Special 2 heavily and never use the
movement ability. `CpuBandTuning.Easy`'s doc comment ("Special 2 exists only as the off-stage recovery
button, exactly as the design states") is **stale**. There is no episode concept, no per-character
profile, no route feasibility check, and no "recovery beats orb pursuit" ordering beyond `IsOffStage`
being tested first in `Decide` (407) — which is correct in shape.
`IsOffStage` (422–427) is `x` outside the walls **or** `y < 0 && !grounded`. With A9's real pits this
becomes reachable for the first time, but it still cannot tell "I am over a pit at y = +2 with nothing
under me" — **A9 added the floor-topology fields to `CpuDecisionObservation`; A9b consumes them.**
`FighterCpuController` contains **no `Grab` and no `EchoStep` at all**.
`ApplyHitstunDefense` (200–238) deliberately bypasses the schedule — that is a *self-state* reflex,
which the new wording explicitly permits ("current self-state may be checked for legality, while
decisions use delayed opponent observations"). **Compliant; add a comment citing the new sentence.**
It also makes the Block hold **exclusive** precisely so a scheduled attack edge cannot misread as the
grab chord (228–232) — **adding CPU grabs must not regress that.**

**Exact change list.**

**1. Recovery detection.** Replace `IsOffStage`'s
`SelfPositionYRaw < 0 && IsGrounded == 0` with "**unsupported over a gap**" using A9's floor fields;
keep the outside-the-walls branch. **It must return false on Sealed stages in every ordinary
situation** — `CPU_RECOVERY.md` explicitly asks to check Closed stages for false recovery triggers.
Recovery = returning from an unsupported position over a **stage gap** to a reachable platform or legal
ledge before the blast zone. **Detect it from authored floor segments, ledges, fighter position and
velocity — not from camera bounds or a universal main-platform Y threshold.**
**Recovery planning takes priority over optional offense and orb pursuit** while return is at risk.
"Closed stages and ordinary supported traversal must not trigger emergency casts."

**2. The offstage episode.** An **episode** latch (input-side state, not sim state — the controller
already owns `_hitstunHoldBlockActive` and friends outside the snapshot) with a per-episode
movement-ability activation counter for Easy and Medium. An episode **ends on a stable landing or a
legal ledge capture, or a KO**. A jump refund or a temporary staff-platform landing alone does **not**
reset the one-activation limit.

**3. The tiered planner.** Rewrite `DecideRecovery`:
- **Easy:** steer toward the nearest plausible legal ledge or landing; use remaining jumps; **at most
  one activation of the character's movement ability per offstage episode**; **Specials remain
  disabled on Easy** — this *replaces* the old no-recovery rule. Set
  `RecoverySpecialTwoPercent = 0`.
- **Medium:** estimate reachable landings; remaining jumps plus **one** suitable recovery ability —
  normally the movement ability, or an **explicitly validated** mobility Special. No multi-ability or
  platform/refund chains. **"Special 2 is not a universal recovery move."**
- **Hard:** compare feasible routes, plan longer legal sequences (jumps, movement abilities, validated
  mobility Specials, temporary platforms); re-evaluate after an interruption; **no fixed
  jump → movement → Special 2 script; no indefinite offstage stalls.**
Read distances, durations and trajectories from the **same normalized Fighter resources and
simulation** as humans — no duplicated tuning constants, no CPU-only jump resets, cooldown refunds,
invulnerability or teleport reach.

**4. Per-character recovery profiles** — **code-owned constants, not resources** (Fighter stays
competitively normalized; `CpuBandTuning`'s class doc is the precedent). Key off canonical ability IDs
and read reach from the normalized loadout (`FighterLoadoutFactory`); **never duplicate numbers**.

| Character | Primary movement ability | Planning constraint |
|---|---|---|
| Einstein | Relativity Warp (+ the reduced-gravity float) | **"Relativity Rift is not a directional recovery Special"** |
| Joan | Ascendant Wings (3 s glide) | **no Wings Refresh in Fighter** |
| Leonardo | Ornithopter Flight | **"a turret is not a stepping platform"** |
| Tesla | Lightning Blink | **"Lorentz Pulse does not move Tesla back"** |
| Shakespeare | Prospero's Flight | — |
| Mozart | Sonata Drift (apex staff platform, 3 s) | Hard may plan a valid staff landing **plus** the existing half-cooldown refund |
| Cleopatra | Desert Mirage | **"Sandstorm Vortex is not a recovery launch"** |
| Lincoln | Rail Charge | **"armor does not prevent pit KOs"** |
| Pocahontas | Breeze Glide + the double-jump reset | Spirit Strike is a *candidate* mobility Special, **only after confirming aerial legality and the implemented trajectory** |

**5. Pit-aware DI.** A9 corrected the stale comment at `FighterCpuController.cs:190–194` and supplied
the observation; A9b implements the policy — the launch trajectory heading over a gap changes the DI
target from "toward stage centre" to the pit-aware read. The V7.4 design rows' deferral flag
(master 3869, §10 Medium row) **expires here**.

**6. CPU grabs and Echo Step (F19 / `CPU_COMBAT_POLICY.md`).**
- **Easy: disabled** — pin a test.
- **Medium: 25% admission per eligible opportunity** — grab when the opponent is blocking, grounded and
  in reach; Echo Step on a vulnerable whiff during the opponent's attack recovery.
- **Hard:** score both tactically, including the **30-meter Echo Step spend against Ultimate setup and
  an unused eligible Defy**.
- **Roll admission once per opportunity**, not per 3-frame evaluation. An opportunity begins when the
  delayed observation first satisfies the conditions and ends when they cease or another action
  commits; **a failed roll does not reopen the same continuing opportunity.** Echo Step admission keys
  to the CPU's **attack execution ID**.
- Grab uses the canonical **0.8-unit reach** and **10/4/24 frames sourced from the shared rules**; the
  grabber drops block; **never** target airborne, rolling, invulnerable, hitstun, daze or shieldstun
  victims.
- Echo Step requires the CPU's own attack recovery, **≥30 meter**, the **120-frame cooldown ready**,
  all canonical state restrictions, the **8-frame wind-up**, the **exact position from 30 frames
  prior**, **31 exact frame samples**, rejection of an invalid destination **before spending**, and a
  **recheck at completion with no refund** (A1c's machinery). **"Check the resolved historical
  destination against current geometry and perceived danger before selection, including pit risk."**
- **C01c interaction:** with A1c's direct binds in place, the CPU emits the **logical verb request**,
  not a synthesized chord — `CPU_COMBAT_POLICY.md`: "generate ordinary player inputs through the shared
  action resolver, including chord priority; never call an ability directly."
- **Do not regress `ApplyHitstunDefense`'s exclusive Block hold.**

**7. Difficulty-matrix wording deltas.** Medium Ultimate: "at the first **legal** opportunity … if both
it and Echo Step are legal, **Ultimate takes priority**" — `UltimatePercent = 100`,
`RequiresUltimateSetup = false` is already compliant; add the priority rule against Echo Step.
Medium hitstun-defense prose is now "A randomized CPU attempt rate does **not** prove any pressure
sequence is escapable" — **doc-only, but it invalidates using `FighterCpuHitstunDefenseTests` as
escape-proof evidence.** Note it in §9.

**8. RNG note, for the ledger not the code.** The controller's RNG is a private xorshift `_randomState`
seeded from the constructor seed and deliberately **outside** the rollback snapshot (class doc lines
19–24: "determinism comes from the seed plus the observation sequence"). `CPU_COMBAT_POLICY.md`
requires admission rolls to use "the seeded **match** PRNG" and to "snapshot the admission result,
opportunity/attack IDs and PRNG state for deterministic resimulation". **Those two statements are in
tension with the shipped architecture. Recommendation: keep the input-source model** (it is what makes
CPU frames recordable and replayable exactly like human pads —
`FighterCpuBehaviorTests.TheExpandedTableStaysBitIdenticalForTheSameSeedAndObservationStream` is the
pin), derive `_randomState` from the per-match seed (already done via `FighterMatchSeedTests`), and
satisfy the doc's intent by recording opportunity IDs **in the produced input frame**, not in the sim
snapshot. **Open a deviation-ledger entry `DEFER-CPU-SNAPSHOT` rather than silently re-architecting.**

**Files owned (exclusive).** `scripts/FighterSim/FighterCpuController.cs`,
`scripts/FighterSim/CpuBandTuning` (in the same file).

**Shared files touched additively.** `scripts/FighterSim/CpuDecisionObservation.cs` (**A9 owned it in
Wave 1** — any field A9b adds must have a Story-side "absent" sentinel filled by
`MirrorParadoxDecisionAdapter.Observe`, exactly as `HasStageBounds` does;
`tests/unit/MirrorParadoxTests.cs` pins frame-identical parity and must stay green),
`docs/PACKAGE6_FIGHTER_PLAN.md` §9.

**Tests.**
- **Rewrite:** `tests/Determinism/FighterCpuBehaviorTests.cs::EasyUsesSpecialTwoOffStageAndNeverInNeutral`
  and `::OnlyHardChainsAnUpwardMovementAbilityIntoItsRecovery` — both pin the old, inverted rules.
  **±0.**
- New `tests/Determinism/CpuRecoveryProfileTests.cs` **+9** — one per character, each proving the
  authored primary movement ability is used and its named exclusion is respected.
- New `tests/Determinism/CpuRecoveryMatrixTests.cs` **+9** — the doc demands "all nine characters on
  every authored Open stage at all three CPU difficulties"; structure it as three cases per stage
  sweeping the nine kits, plus **false-trigger checks on Sealed stages**.
- New `tests/Determinism/CpuVerbPolicyTests.cs` **+7** — Easy never grabs or Echo Steps; Medium admits
  at 25% **once per opportunity** and a failed roll does not reopen it; Hard weighs the 30-meter spend
  against Ultimate setup and an unused Defy; the grab never targets an illegal victim; the Echo Step
  gate honours meter, cooldown, wind-up and destination validity; Ultimate takes priority over Echo
  Step on Medium; the exclusive hitstun Block hold is not regressed.
- Extend `tests/unit/MirrorParadoxTests.cs` **+1** — the Story sentinel fill still produces
  frame-identical decisions.

**Expected count delta: +26.**

**Acceptance criteria.**
- An Easy CPU knocked into Paris's centre pit uses jumps and at most one movement ability, and never a
  Special.
- A Hard CPU recovers from the Vesuvius shelf without a fixed script and does not stall offstage.
- No CPU triggers a recovery cast on a Sealed stage during ordinary play.
- A Medium CPU grabs a blocking grounded opponent roughly a quarter of the time, once per opportunity.
- `MirrorParadoxTests` stays frame-identical.

---

### A10 — Dust economy F05

**Goal.** Replace the shipped drop model with the F05 authored-budget allocator, set every boss award to
25 as a physical pickup, apply the tier bonus, replace `docs/DUST_ECONOMY.md` with the contract, and
rewrite `DustEconomyTests`.

**Design references.** `docs/design-contracts/DUST_ECONOMY.md` **in full** (it is the ledger), F02,
E01, N01. Recon: **B §B7**, **H §H-29, §H-44, §H-45**, **G §1.3(f)**.

**Current state.** `TimelineIntegrityRules.DustBonusPercent` returns 10/5/0 but **no caller applies
it** — the comment at lines 57–58 says so. `BossData.ChronalDustDrop = 50` by default and **all 15 boss
`.tres` carry 50** (including `mirror_paradox.tres:25`). `EnemyData.ChronalDustDrop = 10` by default;
standards are authored 2 and elites 10. `ChronalExtractor.DustReward = 15` (the documented deferred
value; `scripts/Environment/ChronalExtractor.cs:19` even comments that design targets 25 but the ledger
says 15). The in-repo `docs/DUST_ECONOMY.md` totals **1,787 required / 1,416 typical Normal** — roughly
**2× the new F05 envelope**. `tests/ContentValidation/DustEconomyTests.cs` hardcodes
`EliteDustReward = 10`, `BossDustReward = 50`, `ExtractorDustReward = 15`, `FullGridCost = 975`, and
`CampaignModelFundsTheGridOnTheDocumentedSchedule` models the old curve.
`DustEconomyTests.AllNineGridsShareTheDocumentedCostCurve` infers cost from prerequisite **count**
(`PrerequisiteNodeIDs.Length == 0 ? 50 : 75`) — **A4 broke that inference**, so it must be re-expressed
against A4's explicit `Tier` field.
`StoryDropSystem` + `StoryDropTable` currently **guarantee** a kill drop.

**Exact change list.**

**1. Reward manifests.** A per-level-and-difficulty **stable-source reward table** (a new resource
family under `resources/Content/` or per-level `.tres`). Give **each finite reward source a stable ID**:
every mandatory enemy/wave, elite, boss, Extractor, secret, scripted award and optional encounter.
**A source must have exactly one budget category.** Repeated phases of one boss share one boss reward.
**The allocator:** reserve the boss's 25; allocate the required-encounter pool across its finite enemy
source IDs **before play**, with default relative weights **standard = 1, elite/Eraser = 5**
(**allocation ratios, not flat drop amounts**); scripted mandatory encounter rewards **replace** an
equivalent allocation rather than adding to it. For an integer pool `P`,
`quota[i] = P × weight[i] / sum(weights)`; assign `floor(quota[i])`, then give the remaining units to
the **largest fractional remainders, breaking ties by stable source ID**. The sum equals the pool
exactly.
**Zero allocations are legal and spawn no currency pickup. The former "every enemy guarantees dust"
rule is retired.** A positive allocation always spawns **exactly one physical pickup** at that source's
defeat. Health/buff drop chances remain a separate system. **Do not randomize the dust quantity** or
reallocate it after a player skips a source.
Each difficulty compiles its own authored source list against the **same** level pool — more enemies on
Hard change the distribution, **never the total**. Reinforcements and summons that can repeat
indefinitely have **zero** dust; finite scripted waves draw from the existing allocation.

**2. The per-level budget table** (copy verbatim from the contract):

| Level | Required encounters | Boss | Required total | Optional total |
|---|---:|---:|---:|---:|
| 1 | 25 | 25 | 50 | 10 |
| 2 | 15 | 25 | 40 | 10 |
| 3 | 15 | 25 | 40 | 10 |
| 4 | 15 | 25 | 40 | 20 |
| **4A** | **15** | **25** | **40** | **10** |
| 5 | 15 | 25 | 40 | 20 |
| 6 | 15 | 25 | 40 | 20 |
| 7 | 15 | 25 | 40 | 20 |
| 8 | 15 | 25 | 40 | 20 |
| 9 | 15 | 25 | 40 | 20 |
| 10 | 15 | 25 | 40 | 20 |
| 11 | 15 | 25 | 40 | 20 |
| 12 | 25 | 25 | 50 | 20 |
| 13 | 35 | 25 | 60 | 20 |
| 14 | 35 | 25 | 60 | 20 |
| 15 | 35 | 25 | 60 | 20 |
| **Total** | **320** | **400** | **720** | **280** |

Targets: the required route pays **720 base** (720–787 after Integrity bonuses); full optional
collection adds **280** (thorough **1,000 base**, 1,000–1,094 after bonuses). The grid stays **975**
(50/75/200). **Level 0 and training award no persistent dust.** A run includes Levels 1–15 plus exactly
one character's 4A, not all nine. **All nine 4A variants use 15 / 25 / 10 regardless of layout or enemy
count.** Act III conduits, valves and pylons use the **Extractor allocation** — their narrative names
create no extra currency.

**3. Optional allocations.** Level 1 assigns **all 10** to its Extractors (A3 authored them) and adds
no secret before the existing Level 2 introduction. Other levels assign **half** the optional pool to
all Extractors combined and **half** to the designated secret/discovery reward, splitting the Extractor
half evenly by the same integer/remainder rule. With a 20-dust pool: two Extractors pay 5 each or three
pay 4/3/3, discovery pays 10. With a 10-dust pool: two pay 3/2 or three pay 2/2/1, discovery pays 5.
**If an Extractor is also the designated secret it owns the sum of both shares, paid once as one
pickup**, and the discovery flag must not trigger a second payout. A secret containing a Story item
still carries its budgeted dust share. Optional guards draw no additional dust by default; if their
kills carry some of the discovery reward, **explicitly transfer** that amount from the same pool and
record each source. **There is never an extra universal elite award.**
Puzzles, the Nexus Resonance Source, tutorial enemies, training and repeatable spawns award **no** extra
dust; any bespoke bonus must be funded from an existing envelope.

**4. Boss awards.** Set **all 16** to 25: the 15 boss `.tres`, the `BossData` default, and
`MirrorParadoxController.cs:300` — and **remove the Mirror's wallet-direct path** (it becomes the same
25-dust physical pickup). Every boss still produces a **Large** pickup at the arena centre.

**5. Sprite tiers.** Unchanged thresholds: **Small 1–5, Medium 6–24, Large 25+**. An ordinary Extractor
uses its **actual** quantity icon, **not a forced Large** — drop the forced-Large flag from
`StoryDropSystem.SpawnDustAward` for the Extractor source.

**6. The tier bonus, applied at last.** On successful completion, exactly once:
`bonus = floor(retainedBaseDust × tierRate)` at **Restored ≥50%: 10% / Stabilized ≥20%: 5% /
Fractured <20%: 0%**. `retainedBaseDust` is the current level's collected, undeposited base dust
remaining after any applied loss penalties; it **excludes** deposited dust, respec refunds and any
prior tier bonus. Add the bonus to that level's wallet and then **auto-deposit once** using the
level-completion transaction. **No compounding, no checkpoint payout, no payment for a failed attempt.**
**Level 1 has no tier bonus** (untimed). All-Restored maxima are **787 required** and **1,094
thorough**, not 792/1,100, precisely because Level 1 receives none.
Apply it in `StoryManager.OnLevelComplete` / `RecordLevelResultToSave`, itemized on
`LevelResultsPanel` (A8 widened `ShowResults`; A10 supplies the values) across the six F05 categories:
required enemies, boss, Extractors, secret/other optional allocations, losses, and the tier bonus.

**7. Retries and persistence.** Track each source as **unissued, spawned/uncollected, or collected**,
together with the pending pickup state and the retained level wallet. **Collection commits the source
claim and the wallet increment together.** Death rewind, checkpoint resume, Anchor Snap, quit/resume
and crash recovery **preserve collected source claims**; a restored enemy whose reward was collected
may fight again and awards **zero**; an uncollected source/pickup restores consistently **without
creating two copies**. Collapse and voluntary exit apply the existing **20% undeposited-dust** rule and
never unclaim collected sources; lost dust cannot be recovered by killing the same source again;
crashes keep their no-fee rule (A3 removed the boot fee). A full **Restart Level** clears the entire
active level wallet, the pending bonus/completion state, the pickup state and that level's source
claims together, then resets the authored level; previously deposited dust remains safe.
**N01:** the accepted post-boss sealing interaction commits actually collected/retained rewards, the
tier bonus, banking and the completion marker **exactly once**. Boss defeat still spawns its physical
pickup and does **not** auto-credit or deposit it. Remaining local Extractors shutting down at sealing
grant **no** destruction loot, secret claim or higher Integrity tier. Completed levels cannot be
replayed; reloading a completion overlay cannot deposit twice.
**This ledger's persistence half rides on A3b's `rewardSources` record** — A10 authors the allocator
and the sources; A3b owns the storage. Coordinate and record the split in §9.

**8. F02.** Only level completion banks current-level earnings; Act III Beacons spend previously
deposited dust only (A3b).

**9. Docs.** **Replace `docs/DUST_ECONOMY.md` with a pointer to
`docs/design-contracts/DUST_ECONOMY.md`** — do not maintain two ledgers. CLAUDE.md's authority map
names the old path; Phase C updates it.

**Files owned (exclusive).** `resources/Content/<the new reward-manifest family>`,
`scripts/Environment/StoryDropSystem.cs`, `scripts/Environment/StoryDropTable.cs`,
`scripts/Environment/ChronalDustPickup.cs`, `scripts/Environment/ChronalExtractor.cs`
(**`DustReward` only — A3 owned the drain strip in Wave 1**), all 15 `resources/Bosses/*.tres`
`ChronalDustDrop` fields, `docs/DUST_ECONOMY.md` (replaced with a pointer).

**Shared files touched additively.** `scripts/Enemies/EnemyController.cs` and
`scripts/Enemies/BossEncounterController.cs` (**read the manifest instead of
`EnemyData.ChronalDustDrop` / `BossData.ChronalDustDrop`**), `scripts/Core/StoryManager.cs`
(**region: the tier bonus inside the completion transaction only**),
`scripts/UI/LevelResultsPanel.cs` (**the six itemized values only** — A8 owns the layout),
`scripts/Enemies/MirrorParadoxController.cs` (the wallet-direct removal),
`resources/Enemies/*.tres` (`ChronalDustDrop` becomes advisory), `localization/en.csv`
(the six results-category keys under `# Package 11 A10`).

**Tests.**
- **Rewrite wholesale:** `tests/ContentValidation/DustEconomyTests.cs`. New shape: the per-level budget
  table; the allocator's floor-plus-largest-remainder arithmetic preserving each pool exactly for every
  difficulty; zero allocations spawning nothing; the 16 × 25 boss total of 400; the 320 required and
  280 optional totals; base totals 720/1,000; all-Restored maxima 787/1,094 (**with Level 1 excluded
  from the bonus**); the grid at 975; `AllNineGridsShareTheDocumentedCostCurve` re-expressed against
  A4's explicit `Tier`. **Net +6 over the existing suite.**
- New `tests/unit/DustAllocatorTests.cs` **+6** — the worked example (15 dust across ten standards and
  two elites gives quotas 0.75 and 3.75 and allocates exactly 15); tie-breaking by stable source ID;
  Hard's extra enemies redistribute without increasing the total; a repeatable spawn draws zero; a
  fixed authored share distributes only the remainder; the Extractor-is-the-secret sum paid once.
- New `tests/unit/TierBonusTests.cs` **+4** — `floor(retained × rate)` at each tier; exactly once;
  excluded from deposited dust, respec refunds and prior bonuses; Level 1 gets none.
- Extend `tests/unit/StoryDropsAndRewindTests.cs` **+3** — an Extractor uses its actual quantity icon;
  the boss pickup is Large at the arena centre; the Mirror pays a physical 25.
- Extend `tests/ContentValidation/EnemyRosterContentTests.cs` — **rewrite**
  `TierDustRewardsConformToTheEconomyAcrossTheWholeRoster` (it asserts elites are exactly 10 and bosses
  50). **±0.**

**Expected count delta: +19.**

**Acceptance criteria.**
- Every level's authored sources sum **exactly** to its budget row, on all three difficulties.
- A skipped enemy's dust is not redistributed to anything else.
- A completed Normal required route deposits 720 base dust ±0.
- A boss drops one Large physical pickup worth 25, including the Mirror Paradox.
- Killing a restored enemy whose reward was already collected awards nothing.

---

### A11 — Calibration Drills (F18)

**Goal.** Ship the standalone main-menu Calibration Drills route: a picker, a drill list, six drills in
a shared Holodeck scene, and the retained hub console entry.

**Design references.** `design-godot.md` §7 Fighter Onboarding (F18 Option B). Recon: **H §4, §H-23**.

**Requirements, in full.**
- A new **main-menu entry "Calibration Drills"**, available on first boot **with no Story save**.
- Flow: Main Menu → a **single-player character picker** (all nine, full **normalized Fighter kits**,
  reusing the roster data, portraits, controls and Move List, **without** the two-player Ready gate, the
  3 s countdown or stage select) → a **drill list** → the shared Holodeck drill scene.
- Back from the drill list → the picker; Back from the picker and "Exit Calibration" → the Main Menu.
- **Retain** the Story hub console's Calibration Drills entry: the same drill list and content with the
  campaign character's **normalized Fighter kit**; "Exit Calibration" returns to the originating hub.
- **Neither route grants** Story grid perks, Time Freeze, death rewinds or campaign rewards.
- Drill HP, meter, cooldowns and temporary effects are **isolated from Story and match state**; each
  drill supplies and resets **only** the resources its scripted lesson needs, on start **and** on retry.
- The hub entry preserves the active campaign attempt and its resources; **drill exit applies no
  campaign exit fee.** (`SessionExitGuard` bills a 20% undeposited-dust fee on a voluntary exit — the
  drill route must not trip it.)
- **Six drills**, each pass/fail with one line of coaching:
  1. block the string then escape after Hit 2;
  2. tech a launch;
  3. DI a finisher;
  4. grab a blocking dummy;
  5. Echo Step a whiffed special;
  6. reclaim a Rally echo.
- Pass/fail feedback offers **Retry / Next Drill (when available) / Drill List / Exit Calibration**;
  the final drill offers list or exit only.
- **Quitting or relaunching never resumes a drill as a Story attempt.**
- Honour remapped controls, gamepad and keyboard focus, and localized coaching.

**Current state.** Nothing named `Drill` exists. The nearest neighbours:
`scripts/UI/HolodeckConsolePanel.cs` is a **code-built in-hub CPU practice-bout configurator**
(CPU difficulty / character / stage / mode / stocks / time / items / hazards →
`GameManager.LoadScene(stage.ScenePath)`), reached from `HubWorldController.BuildHolodeck()` /
`OpenHolodeckConsole()` — **add a second entry to this panel rather than replacing it**.
`scripts/Environment/TutorialCalibration.cs` is the Level 0 scripting the design says to reuse.
`scripts/UI/CharacterSelectScreen.cs` (839 lines) is the two-token select with the Ready gate,
countdown and stage phase — **the picker must not reuse it wholesale**, but its tile grid, portrait/stat
preview and `ButtonRow/{MoveListButton,SystemsCardButton}` footer are the reuse targets.
`FighterLoadoutFactory` already accepts **normalized base resources only**, which is exactly the
"no Story grid perks" requirement.
`scenes/menus/MainMenu.tscn`'s `RootScreen/Center/Panel/Layout` children are, in order:
`TitleFlourishTop, Title, Subtitle, TitleFlourishBottom, NoticeSlot, TopSeparator, QuickPlayButton,
StoryButton, LevelSelectButton, FighterButton, SettingsButton, BottomSeparator, QuitButton` —
`CalibrationDrillsButton` and its handler in `MainMenu.BindRootScreen()` (132–155) are missing.
**The repo extras `QuickPlayButton` and `LevelSelectButton` stay** (deliberate dev affordances);
record them as accepted deviations rather than "fixing" the menu to match the design's options list.

**Exact change list.**
1. New `scripts/UI/CalibrationDrillPicker.cs` + `scenes/ui/CalibrationDrillPicker.tscn` — a
   single-player character picker over `CharacterRoster.IDs` (A6b's source), reusing the tile grid,
   portrait/stat preview and the Move List / Systems Card footer.
2. New `scripts/UI/CalibrationDrillList.cs` + `scenes/ui/CalibrationDrillList.tscn`.
3. New `scenes/ui/HolodeckDrill.tscn` + a `DrillDefinition` / `DrillRunner` pair driving the existing
   `FighterSimulation` with a scripted dummy.
4. A pass/fail result panel with the four-button set.
5. A new session field for the **return destination** (MainMenu vs the originating hub).
6. `MainMenu`: the button, its handler, and a screen-stack entry.
7. `HolodeckConsolePanel`: a second entry alongside the CPU practice bout.
8. ~25–35 new translation keys under `# Package 11 A11`: drill names, lessons, coaching, pass/fail,
   buttons.
9. All screens adopt `resources/UI/ftt_theme.tres` on their root `Control`, use
   `FocusChainBuilder.Apply` (rebuilt whenever a surface shows or hides options), and store **raw
   translation keys** as control `text`.

**Files owned (exclusive).** The four new scripts and three new scenes,
`scripts/UI/HolodeckConsolePanel.cs` (**the drills entry — A6b owned its roster array in the same
wave; keep the edits disjoint**).

**Shared files touched additively.** `scripts/UI/MainMenu.cs` (**A6b owns its roster array and A12
added the 17th dev tile** — A11's button is additive), `scripts/Core/GameManager.cs` (the return
destination field), `localization/en.csv`.

**Tests.**
- New `tests/unit/CalibrationDrillFlowTests.cs` **+8** — the main-menu route works with **no Story
  save**; the picker has no Ready gate, countdown or stage select; Back walks picker → menu and list →
  picker; the hub route returns to the hub; **no** Story perks, Time Freeze, rewinds or rewards in
  either route; drill state is isolated from Story and match state; each drill resets its resources on
  start and on retry; exiting a drill charges **no** campaign exit fee.
- New `tests/unit/CalibrationDrillContentTests.cs` **+2** — six drills exist with their coaching keys;
  the final drill offers list/exit only.
- Extend `tests/unit/MainMenuSceneTests.cs` — `TheAuthoredSceneCarriesEveryScreenAndControlTheScriptBindsByPath`,
  `EveryInteractiveControlOnEveryScreenJoinsItsFocusChain`, `ExactlyOneScreenIsVisibleAtATime` and
  `CancelWalksTheScreenStackBackAndStopsAtTheRoot` all move. **Rewrite in place, ±0**, plus **+1** for
  the new button.
- Extend `tests/unit/HolodeckConsoleTests.cs` **+1** — the drills entry.

**Expected count delta: +12.**

**Acceptance criteria.**
- A first boot with no save reaches a drill and completes it.
- The hub route returns to the hub with the campaign attempt and its resources intact and no fee.
- A drill never grants a Story perk, a rewind charge, Time Freeze or dust.
- Every drill screen is fully keyboard- and gamepad-navigable.

---

### B1 / B2 / B3 — the nine Level 4A variants

Three agents, each owning three heroes, all copying `Level04AEinsteinController.cs` structurally:

- **B1:** `joan`, `leonardo`, `lincoln`
- **B2:** `cleopatra`, `tesla`, `shakespeare`
- **B3:** `mozart`, `pocahontas` — **plus the Eraser debut wiring across all nine variants** once A7a
  has merged (A12 wired Einstein's against a placeholder elite; B3 re-points all nine at
  `unbound_eraser` and pins the composition rule).

**Goal per hero.** One authored `Level_04A_<hero>.tscn` + controller + dialogue set + pool config +
manifest row + legacy boss resource, at the campaign's functionally-complete / placeholder-presentation
bar, conforming to §2.4 and `docs/design-contracts/LEGACY_CHECKPOINTS.md`.

**Non-negotiables for every variant.**
- **Exactly two checkpoints**, roles `Entry` and `PreBoss`, both enabled on all three difficulties.
  No Middle. No extra hidden recovery anchor.
- **The whole kit required once each:** one traversal gate for the Movement Ability, one puzzle for
  Special 1, one puzzle for Special 2, one set-piece the Ultimate resolves through the Nexus
  Resonance Source. **This is the only level allowed to require a specific character's abilities.**
  V01c: preserve each authored gate's specific ability/event as a **narrow, declared exception** to
  shared puzzle-prop eligibility. It grants no general construct/decoy shortcut elsewhere and
  **requires no purchased perk**.
- **Route order:** Entry → required kit gates and the independent Eraser encounter → the late Font
  approach → PreBoss → boss. **Every mandatory pre-boss objective must be complete before PreBoss can
  activate.** Optional detours stay optional.
- **The Eraser debut** uses the authored route trigger `{levelID}_eraser_debut` in a suitable encounter
  space between Entry and PreBoss; crossing it while gameplay permits begins the encounter and its
  Sarah bark; **it grants no checkpoint, Mending, rewind refill or Integrity lock**, and its timing
  never depends on a middle checkpoint or difficulty. Keep the **single-Eraser** composition.
  Reconstruction must not spawn duplicate live waves or reissue claimed rewards; a persistent
  "trigger seen" flag must not suppress required enemies and strand the gate; first-view presentation
  flags stay separate from encounter state.
- **One Restoration Font** with the existing difficulty uses/potency, on the late approach **after**
  the Eraser and **before** PreBoss. It is a healing object, **not a third checkpoint**. Keep ordinary
  placed Feasts.
- **Boss:** per-character, **two phases**, standard boss rules, **25 dust** as a Large physical pickup.
  A variant **may reuse an existing era boss resource with a variant HP row** — duplicate it to
  `resources/Bosses/legacy/<hero>_legacy_boss.tres` and **state in §9 exactly which resource was reused
  and what HP the variant uses.** Never edit a shared boss resource.
- **Dust:** exactly **15 required-encounter + 25 boss + 10 optional** (A10's allocator).
- **Integrity:** its own authored `ParSeconds` (provisional, per A3's `VERIFY-PAR-SECONDS` rule — each
  4A route gets **its own** par; **do not pool different character routes**), its own starting-Extractor
  count, and its own F11 Entry recovery budget per difficulty.
- **P01 Option A:** retain all nine variants, **reuse era art and themes**, and add only the assets
  needed for distinct nexus outcomes, kit puzzles and bosses.
- Localization under `# Package 11 B<n>`: the level title, room names, objectives, the Nexus prompt and
  the four gate prompts per hero. **A6 owns the dialogue lines**; B-agents author the dialogue
  **structure** (`resources/Dialogue/level_04a_<hero>_dialogue.tres` with entrance / boss_intro /
  post-boss / exit sequences) and hand the line keys to A6 — or, if A6 has already merged, author the
  values under their own marker and say so in §9.

**Files owned per agent (exclusive).** Only their three heroes' scenes, controllers, dialogue sets,
pool configs, boss resources and content tests. **No B agent touches** `StoryManager.cs`,
`LegacyLevelControllerBase.cs`, `StoryLevelControllerBase.cs`, `PlayerController.cs`,
`scripts/FighterSim/**`, another hero's files, or the Einstein exemplar.

**Shared files touched additively.** `resources/Content/content_manifest.csv` (append three rows),
`resources/Pools/scene_pool_catalog.tres` (append three rows), `localization/en.csv` (append under
their own marker). Merges are conflict-free by construction except §9 appends, resolved by union.

**Tests.** One `tests/ContentValidation/Level04A<Hero>ContentTests.cs` per hero, following the
`LevelNNContentTests` pattern: the level ID; the two checkpoint IDs **and roles**; the dialogue set and
line keys; encounter counts against the locked 15/25/10 economy; exactly one Font, placed after the
Eraser and before PreBoss; the four kit gates present and required; the boss arena width against the
authored boss resource's ranged band (`BossController.PixelsPerUnit = 60`; the per-level suites assert
`RangedRangeThreshold * 60 < arenaWidth`).
B3 additionally extends `tests/ContentValidation/EncounterCompositionTests.cs` **+1** — all nine
variants trigger exactly one Eraser.

**Expected count delta: +3 per agent (B1 +3, B2 +3, B3 +4) = +10.**

**Acceptance criteria per hero.**
- The level is completable end to end on Normal with the full kit and nothing else.
- Every one of the four kit gates blocks progress until its ability is used.
- PreBoss cannot be struck until the Nexus puzzle and the Eraser encounter are both complete.
- Reloading at Entry rebuilds the Eraser ambush exactly once with no duplicate claims.
- `CampaignRouteTests` resolves this hero's 4A path.

---
## 5. File-ownership matrix

**The rule:** a file with a named **exclusive owner** is edited by that agent alone in that wave.
A file marked **additive-all** may be appended to by anyone, and conflicts are resolved by union at
merge. Where two agents in the same wave must both touch a file, the **regions** below say which part
each may edit — an agent that needs to change a region it does not own stops and asks the orchestrator
rather than editing it.

### 5.1 High-collision files

| File | Wave 1 | Wave 2 | Region split |
|---|---|---|---|
| `scripts/Characters/PlayerController.cs` (2,867 lines; touched by ~16 items) | **No single owner — region-split, and the orchestrator merges by region.** | same | **A1:** the status-slot implementation, the four cast-site Suppression gates, the Conductive fields. **A2:** the `TimeFrozen` input mask. **A3:** `KillPlayerNonHit()`. **A4:** the scoped-stat accessor, Zealous Vigor, Shield of Orléans, Kinetic Splitting, the Rally echo-fraction term. **A5:** `IsAbilityUnlocked` + the four unlock gates + the tutorial scripted-hit exemptions. **A12:** the Nexus Ultimate interception. **A1b (W2):** `ResolveIncomingHit`, the shield instance, the Defy protection fields, the Rally clamp, the `ultimateOrigin` parameter. **A1c (W2):** `CharacterState.Thrown`, the Story Echo Step ring and destination validation, the chord-resolver call. **A7a (W2):** the non-damaging block entry point. Merge order inside each wave is the wave's merge order; every agent keeps its edit **localized and additive**, never reformatting or reordering neighbouring members. |
| `scripts/Core/StoryManager.cs` | **A3 is the primary owner**; A2, A5 and A12 make declared narrow edits | **A3b effectively owns it** | **W1 — A2:** the Time Freeze cooldown field + its three lifecycle hooks. **A3:** the Integrity clock, the pause set, checkpoint-role plumbing, `CheckpointIntegrityPercent`. **A5:** the milestone grant inside `OnLevelComplete`. **A12:** the `CampaignLevel` append, the route list, `GetLevelScenePath`. **W2 — A3b:** everything else (attempt state, status machine, anchors, recovery events, the ending resolver). **A1b:** the Defy attempt-registry entry. **A8:** the Chronal Rating removal. **A10:** the tier bonus inside the completion transaction. |
| `scripts/FighterSim/FighterSimulationSystems.cs` | **A9** (`FighterMovementSystem` floor handling + the `ApplyStockLoss` respawn argument) | **A1c** (`FighterMatchSystem`, `FighterCombatSystem`, the Echo Step paths) | A1b touches only the F15 constants (`DivinePiercingBlockChargeCost` at 1494, the Joan branch at 2155) and the Sudden Death Defy split at 2354. Sequence A9 (W1) → A1b → A1c. |
| `scripts/FighterSim/FighterSimulationComponents.cs` | **A1** (ID 318 only) | **A1b** (ID 312), then **A1c** (IDs 313–317, 319, and the 312 field extensions) | Nobody else allocates an ID (§2.7). Hash baselines shift; **no test pins a golden hash** — any agent who finds a hash literal reports it in §9 rather than updating it silently. |
| `scripts/FighterSim/FighterEntitySystems.cs` | **A1** (`ApplyStatus` stronger-wins + the Suppression refusal) | **A1b** (`FighterDamageRules.ApplyFighterHit` — the single sim chokepoint — plus the zone/projectile credit flags and the Paris beam Aegis fix) | |
| `scripts/Core/SaveManager.cs` / `SaveEnvelope.cs` | additive fields only (A2, A3, A5, A8) | additive fields (A1b), the attempt record + the async writer + the revision guard (**A3b**), the `SavedMatchSettings` normalizer (A1c), the grid migration function (A4, W1) | **Only Phase C flips `CurrentVersion` to 6** and writes the single migration step (§2.6). |
| `scripts/UI/StoryHUD.cs` + `scenes/ui/StoryHUD.tscn` | **A8 owns both** | A8 | **A2 deletes the rewind-cooldown pip and nothing else.** Every other workstream reaches the HUD through EventBus (§2.9) and edits neither file. |
| `scripts/UI/FighterHUD.cs` / `FighterHudModel.cs` / `MatchResults.cs` | **A8** | A8; **A1c** adds the stocks-lost totals lines to `MatchResults` only | |
| `scripts/Core/EventBus.cs` | **additive-all** | additive-all | Append a payload class, an enum and an event declaration. **Never edit or reorder an existing one.** Conflicts resolved by union. A1's `StatusType.Suppression` append is the one exception that changes an existing type — **A1 only** (§2.8). |
| `localization/en.csv` | **append-all under `# Package 11 <WS-ID>` markers; owned deletes only** (§2.11) | same | Owned deletes: **A4** the 162 `resonance_*` node rows; **A6** every `dlg_*` / `speaker_*` / level-title / room / objective row; **A2** `tutorial_step_manual_rewind`, `controls_action_rewind`, `hud_rewind_cooldown`; **A8** `results_rating`; **A1c** `fighter_mode_hybrid`. **Only the orchestrator regenerates `en.en.translation`.** |
| `resources/Content/content_manifest.csv` | **append-all** | append-all | A12 (1 row), A7a (1 row), B1/B2/B3 (3 rows each). No status flips without saying so in §9. Regenerated by the orchestrator's `--import` alongside `en.csv`. |
| `scripts/Environment/Level00Controller.cs` | **A5 owns it** | **A6b** (the Part 1 presentation only, after A5) | **A2 edits only the manual-rewind step** (the `ScriptedFreeRewind` → `DrillFreeFreeze` handoff at L251/258). |
| `scripts/Environment/StoryLevelControllerBase.cs` | **A3 owns it** | **A3b** (durable-resource restore + baseline encounter IDs) | **A12 additive only** (the Legacy subclass hooks). A8's sealing-anchor split is **not in this package** (§8). |
| `project.godot` | **A2** renames `gameplay_rewind` → `gameplay_time_freeze` | **A1c** adds `gameplay_grab` and `gameplay_echo_step` | Additive; two disjoint edits in two waves. |
| `scripts/Core/InputManager.cs` / `InputBindings.cs` | **A2** (the action rename + the binding migration function), **A8** (the Unbound shape + shortcut flags) | **A1c** (the two new actions, the wire bits) | A2 lands first, A8 second, A1c in Wave 2. |
| `scripts/Combat/BasicComboRules.cs` | **A1** (`FinisherMarkType` / `FinisherMarkFrames`) | **A1c** (`SelectRecoveryVerb`) | **Read-only for A4** — it is cross-mode; Story scaling happens at the application site. Both edits are additive. |
| `scripts/Combat/BlockSystem.cs` | — | **A1b owns it** | A4's Henry's Bastion node authoring is data-only; the lifecycle rewrite is A1b's. |
| `scripts/Combat/GlowPresentationController.cs` | **A1** (the aura-smother channel) | **A8** (the F24 ownership split, as a follow-up commit after A1 merges), then **A6b** (the persistent hero aura channel, after A8) | Strict ordering: A1 → A8 → A6b. Each states the ordering in §9. |
| `scripts/Enemies/EnemyController.cs` | **A1 owns it** | **A7a** (the channelling gate + the Basic-class override), **A10** (the manifest dust read) | Additive in Wave 2. |
| `scripts/Enemies/BossController.cs` | **A1** (status slots) | **A7b** (the ring, heal path, suspend, composite kit) | |
| `scripts/FighterSim/FighterCpuController.cs` / `CpuDecisionObservation.cs` | **A9** (the observation fields + the DI comment) | **A9b owns the controller**; **A7b** adds only the boss-override `CpuBandTuning` | Any observation field needs a Story-side sentinel in `MirrorParadoxDecisionAdapter.Observe`; `MirrorParadoxTests` pins frame-identical parity. |
| `scripts/UI/MainMenu.cs` | **A12** (the 17th dev-select tile) | **A6b** (the roster array), **A11** (the Calibration Drills button) | Three disjoint edits. |
| `scripts/UI/LevelResultsPanel.cs` | **A5** (the Resonance Restored beat), **A8** (everything else) | **A10** (the six itemized values) | |
| `scripts/Characters/CharacterFactory.cs` | **A4** (the scoped-stat copy), **A5** (the unlock set) | **A6b** (the colour map), **A7b** (the Hard-only perk path) | Additive. |
| `resources/Bosses/*.tres` | — | **A7b** (the four HP rows + two `DisplayName` fields), **A10** (`ChronalDustDrop` → 25) | Two disjoint field sets. |
| `AGENTS.md` / `CLAUDE.md` | **nobody** | **nobody** | **Phase C only** (§6). Agents list required edits in their handoff instead. |
| `docs/PACKAGE11_V7_6_ALIGNMENT_PLAN.md` §9 | **append-all** | append-all | Every agent appends its own block; union merge. |

### 5.2 Exclusive owners, by workstream (summary)

| Workstream | Headline exclusive files |
|---|---|
| **A1** | `StatusSlots.cs` (new), `StatusController.cs`, `EnemyController.cs`, `BossController.cs`, `GlowPresentationController.cs`, `TeslaAbilities.cs`, `TeslaCoilNode.cs`, component 318 |
| **A2** | `TimeFreezeController.cs` (new), `ChronalRewindManager.cs`, `StasisEcho.cs` (deleted), `StoryRewindPolicy.cs`, `PressurePlate.cs`, `SearchlightZone.cs`, `PathMovingPlatform.cs`, `IInteractable.cs`, `StorySceneBootstrapper.cs`, `TutorialCalibration.cs`, the ~16 `IStoryTimeFreezable` classes |
| **A3** | `TimelineIntegrityRules.cs`, `ChronalExtractor.cs`, `CollapseTremorController.cs` (new), `SecretCache.cs`, `LevelManager.cs`, `StoryLevelControllerBase.cs`, `SessionExitGuard.cs`, `falling_debris.tscn` |
| **A4** | `ResonanceNodeData.cs`, `ResonanceGridData.cs`, `ResonanceProgression.cs`, `ResonanceGridPanel.cs`, `ResonanceGridNavigation.cs`, `ResonanceGrid.tscn`, the nine `*_grid.tres`, `scripts/Characters/Abilities/*` except `TeslaAbilities.cs` |
| **A5** | `LegacyUnlockSchedule.cs` (new), `CampaignCaptiveRoster.cs` (new), `Level00Controller.cs`, `DialogueManager.cs`, `DialogueSequenceData.cs`, `DialogueSetData.cs`, `MoveListScreen.cs` |
| **A6** | the `en.csv` narrative rows, all 17 `resources/Dialogue/*.tres`, the CharacterSelect footer label |
| **A8** | both HUD scenes and scripts, `HudAbilityIndicatorModel.cs`, `SettingsMenu.cs` + `Settings.tscn`, `ComfortSettings.cs` (new), `AudioSnapshotMixer.cs`, `AudioBuses.cs`, `default_bus_layout.tres`, `outline_glow.gdshader`, `UIPalette.cs` |
| **A9** | `FighterStageGeometry.cs`, `CpuDecisionObservation.cs`, `FighterStageData.cs`, `stage_catalog.tres`, the three Open stage scenes, `FighterStageConformance.cs` |
| **A12** | `LegacyLevelControllerBase.cs` (new), `NexusResonanceSource.cs` (new), the Einstein 4A scene/controller/dialogue/boss/pool |
| **A1b** | `BlockSystem.cs`, `ChronalOrbItem.cs`, `FighterEntitySystems.cs`, component 312, six ability scripts, `PlaceholderZone.cs` |
| **A1c** | `FighterSimulationSystems.cs`, components 313–317 + 319, `GameManager.cs`, `PlayerInputFrame.cs`, `RollbackProtocol.cs`, `FighterAudioRules.cs` |
| **A3b** | `StoryAttemptState.cs` (new), `WardenBeacon.cs` (new), `GameOver.tscn` + `GameOverScreen.cs` (new), `TimelineRestartPanel.cs`, the three Act III level controllers, `CampaignCompletionSequence.cs` |
| **A6b** | `CharacterRoster.cs` (new), `UIPalette.cs` (after A8), `MainMenu.cs` roster, `CharacterSelectScreen.cs` roster, `HolodeckConsolePanel.cs` roster, `AbilityVisualLibrary.cs` |
| **A7a** | the four Eraser resources, `SiphonTetherChannel.cs` (new), `EnemyAbilityData.cs`, `EnemyAbilityExecutor.cs`, `TelegraphGlyph.cs`, `EnemyAbilityVisualLibrary.cs`, `RosterVfxMap.cs` |
| **A7b** | the boss `.tres` HP/name fields, `BossData.cs`, `MirrorParadoxController.cs`, `MirrorParadoxEncounterController.cs`, `archive_remnants.tres` |
| **A9b** | `FighterCpuController.cs` + `CpuBandTuning` |
| **A10** | the reward-manifest family, `StoryDropSystem.cs`, `StoryDropTable.cs`, `ChronalDustPickup.cs`, `docs/DUST_ECONOMY.md` |
| **A11** | the four drill scripts and three drill scenes, `HolodeckConsolePanel.cs` drills entry |
| **B1/B2/B3** | only their three heroes' scenes, controllers, dialogue sets, pool configs, boss resources and content tests |

---

## 6. Phase C — closeout (serial, main checkout)

One agent, in the main checkout, after every Wave 2 branch has merged and built green.

1. **Flip the save schema to v6.** Set `SaveSchemaMigrator.CurrentVersion = 6`
   (`scripts/Core/SaveEnvelope.cs:274`) and write the **single** v5 → v6 migration step, composing the
   migration functions the workstreams wrote:
   - **story:** create `attemptState` from the six loose v5 fields (A3b's shape);
     `checkpointIntegrity` seeded from `LevelIntegrityPercent` with **no invention**;
     `unlockedLegacyAbilities` derived from `completedLevels` per the Legacy Unlock Schedule, else
     `LegacyRecoveryRequired`; `levelIntegrity` re-scoped from `IntegrityByLevel` with the PreBoss
     caveat recorded; A4's drop-unknown-grid-node refund; A1b's `StoryDefyHistoryUsed`;
     A2's `TimeFreezeCooldownSeconds`; A3b's `AnchorCharges`.
   - **global:** `SeenDialogueIDs` = the union of every slot's `ViewedDialogueIDs`;
     `ReducedTemporalEffects = false`; the C01c shortcut flags default **On** (reproducing today's
     chords); the `gameplay_rewind` override migrated to `gameplay_time_freeze` **without** overriding
     an explicit newer Time Freeze bind; `SavedMatchSettings.Mode == 2` (Hybrid) normalized to timed
     Stock with a valid positive timer.
   - the `LegacyRecoveryRequired` path plus its notice key.
   **Never default an active legacy attempt to full anchors / unused Defy / unused healing / unclaimed
   rewards** (§2.6).
   `CurrentVersion` is **shared by both payloads**, so a global-only bump also bumps story payloads.
   **Ruling: accept the coupled bump.** Splitting the two counters is a larger change than this package
   is taking; record the recommendation in §9 for a future package.
   Write the single `SaveEnvelopeTests` pair: **v5 loads with v6 defaults** and a **v6 round trip**.
   **+2 tests.**
2. **Run `--headless --import`** and commit the regenerated `localization/en.en.translation` **and**
   `resources/Content/content_manifest.1.translation`. Then re-run every suite the agents flagged as
   expected-to-fail-until-import.
3. **Rewrite `AGENTS.md` and `CLAUDE.md`.** Minimum list, assembled from the workstream handoffs:
   - **The authority rule (§2.1).** Replace CLAUDE.md's "If a document and a `.tres` resource disagree,
     the resource wins" and AGENTS.md's "Resources own the numbers… the `.tres` files remain the law"
     with the P04 formulation: *the GDD and its adopted contracts define intended behaviour; verified
     code and `.tres` data are build evidence; record divergence in
     `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`.* Keep the "no second canonical value" rule.
   - **Autoloads and new systems:** `TimeFreezeController` (scene-scoped, not an autoload),
     `CollapseTremorController`, `WardenBeacon`, `StoryAttemptState`, `LegacyUnlockSchedule`,
     `CharacterRoster`, `CampaignCaptiveRoster`, `ComfortSettings`, `SiphonTetherChannel`,
     `NexusResonanceSource`, `LegacyLevelControllerBase`.
   - **Retired systems:** manual rewind, the Stasis Anchor, the Stasis Echo, the 12 s cooldown,
     the abnormal-exit fee, Chronal Rating, the siphon share/grace/restoration model, the 85% ending
     threshold, Hybrid as a selectable mode, the 1-HP Sudden Death, the two-charge Guard-Crush
     specials, "a Story orb shield-restore ends the lockout", "one active status at a time",
     "every defeated enemy drops dust".
   - **Changed contracts:** the 17-slot campaign route with `LegacyNexus = 16`; checkpoint roles;
     Timeline Integrity as the level timer with the 2.0/1.5/1.2 multipliers and 50/20 tiers; the
     Legacy Unlock Schedule; stronger-wins statuses; the two status slots; Suppression; the Conductive
     mark; the D01–D04 defensive order; the 31-sample Echo Step ring; protocol v3;
     `CharacterState.Thrown`; F21 stocks-lost; F22 Sudden Death; the three Open stages and floor
     segments; save schema v6; the F05 dust ledger.
   - **The Klotho component inventory:** 312, 313–317, 318, 319 and the retirement of 311;
     `FighterRuntimeComponent` still full at 128 B; `FighterVerbComponent` (310) now at its limit.
   - **The test baseline** (item 5).
   - Close the known-gap bullets this package resolved: the bottom-blast-zone pillar vs. the
     solid-floor stage dossiers (audit H-11), Florence's unauthored Extractors, the Chrono-Warden's
     never-placed status, `DustBonusPercent` never applied, the retired `speaker_player` "Traveler"
     assumption.
4. **Update the ledgers.** `IMPLEMENTATION_STATUS.md` (root) and `docs/IMPLEMENTATION_STATUS.md` —
   both are current and neither supersedes the other. Replace the repo `docs/DUST_ECONOMY.md` with a
   pointer to the contract (A10 did the content; Phase C fixes CLAUDE.md's authority map, which still
   names the old path). Add `docs/PACKAGE11_V7_6_ALIGNMENT_PLAN.md` to the authority map.
5. **Seed `docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`.** Its "Confirmed current-build
   deviations" section currently reads *"None verified in this docs workspace"* — nine dossiers of
   first-hand evidence at `31fed14` are exactly what it asks for. Seed it with every entry this package
   opened or deferred, each with a stable ID, the intended behavior with its decision reference, the
   observed value with the pinned commit and file identifier, owner (`Unassigned` unless claimed), and
   concrete acceptance criteria. At minimum:
   `VERIFY-BOSS-HP` (the ten unreconciled Act II/III rows, §2.2),
   `VERIFY-PAR-SECONDS` (every provisional `ParSeconds` and F11 `remainingRouteSeconds`, A3/B-wave),
   `DEFER-ROSTER-ENUM` (`FighterCharacterID`, A6b),
   `DEFER-CPU-SNAPSHOT` (the CPU RNG outside the snapshot, A9b),
   `DEFER-SEALING-ANCHOR` (N01's per-level sealing interaction, §8),
   `DEFER-STEAM-RPT` (Remote Play Together ships as a label only, §8),
   `VERIFY-SERPENT-NEST` (the design says 6 HP / 1.0 s; AGENTS.md records the shipped `.tres` at
   10 HP / 4.0 s — recon C §7.3 flagged it as a candidate confirmed deviation, and **this package does
   not touch it**),
   plus `VERIFY-01` … `VERIFY-04`, which this package's evidence **closes or converts** from
   "awaiting verification" into confirmed-and-now-resolved entries. **Retain the record after closure
   rather than deleting its history.**
6. **Three consecutive full-suite runs**, each behind a `Get-Process testhost,Godot*` clear-window
   poll, reading the **`Total:`** against the expected number (item 7). A green exit code alone is not
   evidence.
7. **Reconcile the baseline.** Expected arithmetic:

   | Stage | Delta | Running total |
   |---|---:|---:|
   | Baseline at `31fed14` | — | **1638** |
   | A1 | +23 | 1661 |
   | A2 | −8 / +20 = +12 | 1673 |
   | A3 | −4 / +37 = +33 | 1706 |
   | A4 | +29 | 1735 |
   | A5 | +27 | 1762 |
   | A6 | +6 | 1768 |
   | A8 | +42 | 1810 |
   | A9 | +11 | 1821 |
   | A12 | +23 | **1844** (Wave 1 close) |
   | A1b | +60 | 1904 |
   | A1c | +44 | 1948 |
   | A3b | +40 | 1988 |
   | A6b | +8 | 1996 |
   | A7a | +22 | 2018 |
   | A7b | +20 | 2038 |
   | A9b | +26 | 2064 |
   | A10 | +19 | 2083 |
   | A11 | +12 | 2095 |
   | B1 / B2 / B3 | +3 / +3 / +4 | 2105 |
   | Phase C (v6 migration pair) | +2 | **2107** |

   **The expected closing baseline is 2107.** Any deviation is investigated, not accepted. Several
   families go **down** before they go up (A2 removes 8; A3 removes 4) — a dropping `Total:` inside a
   wave is expected and is **not** a regression signature.
8. **Write the honest not-delivered list** into §9's closeout block, in the Package 6 C1 format —
   everything in §8 plus anything a workstream deferred in its own §9 entry, plus the standing truth
   that **nobody has visually judged any of the new presentation work**: no automated gate can say
   whether the Integrity clock reads at gameplay scale, whether the Collapse Tremor is legible,
   whether the two-colour grammar lands, or whether a 4A variant is fun. Treat the whole visual layer
   as unreviewed until a human opens the editor.

---

## 7. Validation gates (every phase)

1. **`dotnet build FightersThroughTime.csproj --nologo`** — no new warnings. One pre-existing vendored
   warning is expected (`CS8632` in `addons/gdunit4/src/dotnet/GdUnit4CSharpApi.cs`).
2. **Filtered test runs per agent; the full suite only at closeout.**
   `dotnet test FightersThroughTime.csproj --settings .runsettings --filter "FullyQualifiedName~<Suite>"`,
   behind a `Get-Process testhost,Godot*` clear-window poll (§2.12). Read the **`Total:`** against the
   dossier's declared delta, never against "bigger than before". A green exit code alone is not
   evidence (CLAUDE.md failure signatures 1, 2, 4, 5, 6).
3. **Headless import check** after any scene, resource or CSV edit:
   `"…Godot_v4.7.1-stable_mono_win64_console.exe" --headless --path "<repo>" --quit`.
   After an `en.csv` or manifest edit the **orchestrator** runs `--import` (not `--quit`) and commits
   the regenerated translation artifacts (§2.11).
4. **Headless smoke** (`--quit-after 300`) for every authored scene: the three re-authored Open stages,
   the nine 4A variants, the Game Over screen, the three Calibration Drill screens, the rewritten
   Story HUD.
5. **Story/Fighter isolation.** No Story progression value enters a normalized Fighter loadout; no
   `Suppression`, `TimeFreezeController`, `LegacyUnlockSchedule` or Resonance value is reachable from
   `scripts/FighterSim/`. Each of A1, A2, A5 and A4 ships the pinning test.
6. **Determinism.** Every new sim field is fixed-point, snapshot-complete and hash-visible.
   `tests/Determinism/RollbackReadinessTests.cs` passes across all ten stages after A9's anchor
   re-encode and again after A1c's component changes. The C01a toggle produces **identical** gameplay
   hashes On and Off.
7. **Localization.** `ScriptTranslationKeyTests`, `SceneVisibleTextTests`, `UnusedTranslationKeyTests`
   (ceiling 11) and `CampaignLocalizationTests` all green **after** the orchestrator's `--import`.
8. **Crash hygiene.** §2.13 in full. On a large partial `Total:` with a negative exit code, read
   `godot.log` first.

---

## 8. Explicitly out of scope

Recorded here so nobody closes one of these silently, and so the closeout's honesty list is already
written.

1. **Package 7 native online / LAN.** The match-start barrier, desync halt UI, input redundancy
   (N ≥ 4 frames per packet), the negotiated content/build hash, negotiated input delay 0–3 frames,
   synced pause, Steam Networking Sockets and the relay fallback, the public queue, the forfeit
   mechanism (3 s Pause hold), and full-state resync all stay deferred. `scripts/Networking/`,
   `NetworkSelectScreen.cs` and `scenes/menus/NetworkSelect.tscn` remain in tree as Package 7's
   starting point — **the design instructs keeping them.** A1c's protocol v3 bump is the only
   networking change in this package.
2. **Achievements.** Not in the design's current scope; nothing in this package touches them.
3. **Level Select and records surfaces** retired by ruling 2.A. The **developer** level select stays
   (a debug-only affordance, now 17 tiles) and is a recorded deviation, not a design feature.
4. **Artist assets (Package 10).** Everything visual and audible this package adds is
   contract-conformant placeholder: silent audio cues under the standing rule
   (`resources/Audio/README.md` — **do not "fix" the silence by adding tones**), placeholder sprites,
   reused sheets. V7.6 ruling 2.G: the AI-generated assets are development placeholders, all shipped
   art is replaced with artist-produced content before release under the same atlas contract, so
   Steam's AI-content disclosure does not apply to the release build and **no per-atlas consistency
   gate is needed for placeholder art**. Record that ruling; do not build such a gate.
   §17's production-scope obligations — 24 authored non-tutorial boss encounter slots (15 shared +
   9 Legacy), music coverage for the same 24 slots plus Level 0, the versus arenas and the hub,
   the Collapse Tremor VFX set, the Eraser art set, and the F13 Defy seal art — are
   `docs/design-contracts/PRODUCTION_SCOPE.md` ledger work, not code tasks.
5. **Steam SDK integration.** Steamworks is not installed and `NetworkManager.cs:107` has
   `SteamTransportAvailable => false`. Remote Play Together needs **no netcode** — it is host-side
   streamed local multiplayer, so the existing local shared-screen 1v1 path *is* the feature. This
   package ships **only the localized label and copy** (A6). M01's "hide the Steam entry when Steam
   integration is unavailable" means the entry would always be hidden today, so it ships as a notice on
   the local-play surface instead. Ledger entry `DEFER-STEAM-RPT`.
6. **Advanced Training Mode.** F18's Calibration Drills (A11) are launch scope; a full Training Mode
   with hitbox display, frame data overlays, save states and recording is not.
7. **H-7 gravity unification.** Story runs weight-coupled 18-base gravity with fall and short-hop
   multipliers; the sim runs a flat −30 with fixed-height jumps and no short hop. Closing it risks
   stage-geometry and campaign retunes across every authored level and needs its own pass.
8. **The sim drop-through input divergence (M-18).** Sim Down+Jump vs. Story/design double-tap-Down
   stays open **by deliberate decision** — `FighterRuntimeComponent` is exactly full at 128 bytes, and
   the 2026-08-10 batch explicitly skipped closing it for that reason.
9. **N01's per-level sealing anchor.** The design generalises Level 15's `TemporalCoreAnchor` to every
   boss level: boss defeat → defeat dialogue → regain control beside the boss's physical pickup and a
   marked sealing anchor → a single Interact "Seal Timeline — Complete Level" → one atomic transaction
   → restoration vignette → results, with `AwaitingSeal` **persisted**. That contradicts
   `TemporalCoreAnchor`'s deliberate "armed state is derived, never persisted" decision and needs a
   design/saves ruling. **A10 implements the once-only commit half of the transaction; the anchor
   interaction itself is deferred** — ledger entry `DEFER-SEALING-ANCHOR`.
10. **F09 frame-advantage validation and V02 controlled stall validation.** No gameplay constants
    change without a separately approved fix, and V02 is **blocked on the three Open stages being
    playable** — which A9 delivers, so V02 becomes runnable but is not run here. Consequence for the
    whole package: **nothing in it should be described as "balanced" on completion.** A randomized CPU
    attempt rate does not prove any pressure sequence is escapable.
11. **The full S01 rollback field manifest and CPU snapshot migration.** Recon E's headline #6 —
    `FighterCpuController`'s `_randomState`, `_schedule[128]`, `_sustainedHeld`, `_activeEdges`,
    `_pendingEdges`, `_edgeFramesRemaining`, `_edgeGapFramesRemaining`, `_lastObservedHitstunFrames`,
    `_escapeStanceLingerRemaining`, `_hitstunHoldBlockActive` and `_hitstunDiActive` are entirely
    outside the ECS, so a rollback in a live CPU match silently diverges CPU behaviour. This package
    records `DEFER-CPU-SNAPSHOT` and keeps the input-source model.
12. **Cross-platform determinism validation.** `docs/PERFORMANCE_BASELINE.md`'s 0.058 ms median /
    0.104 ms p95 resim and 2,699-byte snapshot were measured on one Windows Debug machine against a
    partial component set. This package **grows** that set substantially; re-measurement is Package 7's
    entry work.
13. **Illustrated-still plot cinematics.** §7's full-screen hand-drawn stills under the dialogue box,
    including the nine per-character Level 0 nexus stills, are content-heavy Package 10 work. The
    design explicitly forbids `VideoStreamPlayer`.
14. **V01b Reset Puzzle and V01c dedicated puzzle props.** The nearby "Reset Puzzle" interaction and
    the per-mechanism prop-eligibility declarations are real design requirements with no
    implementation; only 4A's narrowly authored gates (A12/B-wave) are delivered here.
15. **`docs/PACKAGE5_CAMPAIGN_PLAN.md` recovery.** The docs workspace reports it missing;
    **it is present in this repo**. Tell the user: the "unavailable source" flag in the master and in
    `DESIGN_BUILD_DEVIATIONS.md`'s source table is wrong from the repo's side. Phase C records that
    correction; no recovery work is needed.
16. **A human end-to-end playthrough.** Levels 0–15 plus a 4A at each difficulty, and all ten Fighter
    stages, remain unplayed by a person. No automated gate discharges this.

---

## 9. Deviations (append-only)

*(Every agent appends a `### <workstream ID> — <subject> (date)` block here in the Package 5/6 format:
a **bold one-sentence claim**, then the reasoning and the pinning test. The orchestrator appends an
integration block per wave. Phase C appends the final closeout block including the honest
"What Package 11 did NOT deliver" list. Nothing in this section is ever edited or removed — only
appended.)*

---

### A3 — Timeline Integrity as the level timer, Collapse Tremor, checkpoint roles, lethal pits (2026-09-13)

**The V7.1/V7.3 siphon model is gone, not retuned: Timeline Integrity is now a normalized level clock
that drains globally from level load, that nothing anywhere can add a point back to, and that fires a
Timeline Collapse at zero on every difficulty.** `grep -rn "MaxSiphonSharePercent\|SiphonGrace\|RestoreTimelineIntegrity\|ChronalRatingRules"`
over `scripts/` returns nothing. The 600 px engagement check, the 10 s grace, the 10% per-machine
share and the +3/+2/+5 restoration paths are all deleted; a living Extractor's only remaining
contribution to the clock is the +0.2 it adds to the drain factor. Pinned by the rewritten
`TimelineIntegrityTests` (12 cases; four invalidated cases removed, ten added).

**`ChronalRatingRules` is deleted outright rather than renamed to `IntegrityTierRules`,** per the §3
A3 naming ruling: `TimelineIntegrityRules` already owned `Tier`/`TierKey`/`DustBonusPercent`, so a
second tier type would have been the "second canonical value" §2.1 forbids. `StoryManager` keeps
`LastLevelChronalRating` as a dead property writing `""`, and `StorySaveData.RatingByLevel` stays a
dead field through v6 — **A8 owns deleting both, plus the `results_rating` row in `en.csv` and the
results line**. A3 deliberately left that row in place.

**The Integrity clock ticks on `StoryManager._PhysicsProcess`, not on a level controller.** The
autoload survives room transitions and scene-local teardown, and 60 Hz is where a gameplay resource
is spent. The pause set (`IntegrityClockPause`, a `[Flags]` mask) is deliberately a **mask, not a
counter**: an unbalanced release can never strand the clock, which matters because every one of its
four call sites is in a file A3 does not own. `DialogueManager`, `PauseMenuBase`,
`ChronalRewindManager` and `BossEncounterController` each take exactly one scope, and each releases
it on teardown as well as on the normal path.

**The Collapse Tremor attaches from `StoryLevelControllerBase`, not from `StorySceneBootstrapper`.**
The dossier suggested the bootstrapper, but A2 owns that file this wave, and the bootstrapper is
shared by the hub, the Tutorial and Florence — so gating it there would have needed a special case.
The Tutorial and Florence do not extend the base controller at all, so attaching from the base
produces exactly the required "untimed levels opt out" behavior with no gate. The same reasoning arms
the clock there.

**No `ParSeconds` value in this change is measured, and none may be quoted as if it were.** V01a
requires a per-hero median Normal required-route measurement that has not happened. A3 seeds every
par as `authored room count x 90 s` rounded up to the nearest 30 s (room count = the level's
`BuildRoomTransition` calls), and seeds F11 remaining-route budgets as fractions of par
(Entry 0.90 — par is a full-run median including detours, while F11 measures the mandatory route
only; Middle 0.45; PreBoss 0.00, which falls to the 25-point floor). Both tables are recorded in
`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md` under **`VERIFY-PAR-SECONDS`** and pinned as a
table in `StoryLevelControllerBaseTests`, so a measurement pass is a single-file edit. Entry at 0.90
also keeps Hard's Entry budget at 90 points rather than the exactly-100 a 1.00 fraction produces —
legal, but uncomfortably on the invalid-budget boundary.

**`RequiredRecovery` computes in `double` with a 1e-6 rounding epsilon.** The single-precision rate
made the contract's own worked example (par 600, route 240, Easy) come out at 25 instead of 24: a
recovery minimum that drifts by a point between releases is not a budget anyone can validate. An
above-100 requirement **throws** and is never clamped; `StoryManager.ResolveTimerRecoveryIntegrity`
catches it, reports it with `GD.PushError`, and falls back to the banked checkpoint gauge so an
authoring error leaves the game playable while staying loud. Pinned by `F11RecoveryBudgetTests` (4).

**F16 ships complete but authors zero kill boundaries, because the campaign has no lethal opening to
attach one to.** A sweep of all sixteen controllers found every gap documented as deliberately
non-lethal — L04's boss pit "deliberately not a hazard", L05's flood "never becomes a kill floor",
L07's sea "not a rising water zone and not a kill plane" (it rescues the player onto the deck they
launched from), L06's lava front "never a kill", and L12/L13/L14/L15 each stating they have no pits.
L10's "Yard" is a recessed crowd area with a floor, not a void. The §3 A3 scope ruling excludes
exactly this case, so A3 shipped the chokepoint (`PlayerController.KillPlayerNonHit()`), the
`StoryKillBoundary` type and the `BuildKillBoundary` authoring helper with its edge cue, and recorded
the content gap as **`VERIFY-STORY-PITS`**. Authoring a boundary is now content work, not code work.
`StoryKillBoundaryTests` (5) pins the behavior, including the F16 edge case where Integrity hits zero
on the same update: the timer collapse resolves instead, and no rewind charge is spent.

**A real bug the pins caught:** `StoryKillBoundary.Resolve` originally counted a kill and returned
true even against an already-dead body, so a hero falling through a wide boundary would have
re-raised `OnPlayerDied` and spent a second rewind charge per overlapping trigger. It now refuses
`Dead`/`Respawning` outright.

**Debris is blockable through the player's own `BlockSystem` node path, not through a new
`PlayerController` method.** A7a owns the shared non-damaging block entry point in Wave 2; until then
`FallingDebris` resolves a Basic-class `HitPayload` against
`player.GetNodeOrNull<BlockSystem>("BlockSystem")`, which is exactly how `PlayerController` resolves
it itself. That call site should move onto A7a's entry point when it lands.

**`FractureEligible` is opt-in, and the Tremor's exclusions are enforced by omission.** Pressure
plates, latched-switch gates, `PathMovingPlatform` and the pre-boss approach are excluded by simply
never carrying the flag — a rule a reader can verify by grep and a test can assert, rather than a
type blacklist that silently rots. Every flagged platform keeps its 5 s respawn, so no gap can become
uncrossable; `CollapseTremorTests` asserts the full Shaking -> Collapsing -> Disabled -> **Solid**
cycle rather than only the collapse.

**Florence's three Extractors are authored, closing the one content gap `docs/DUST_ECONOMY.md` §6
left open.** Level 1 predates `StoryLevelControllerBase` and was not retrofitted, so
`Level01Controller` grew its own `BuildExtractor` mirroring the base helper — dust value still
resource-owned, never overridden from level code. Level 1 is **untimed**, so these carry presentation
and dust only; **the per-machine dust allocation is A10's**. New `Level01ContentTests` (+1).

**The abnormal-exit fee is deleted; the marker survives.** `SessionExitGuard.ApplyAbnormalExitFee` is
gone and `SaveManager.ApplyAbnormalExitFeeIfMarked` became `ConsumeAbnormalExitMarker`, which reads
and clears the marker and charges nothing — a power cut is not a player decision. The voluntary 20%
`CalculateExit*` helpers are untouched, and the `save_notice_abnormal_exit_fee` row was deleted in
the same change so `UnusedTranslationKeyTests` sees no new orphan. **A3b inherits the marker as F10's
attempt-status router.**

**`BuildCheckpoint` now requires an explicit `CheckpointRole`,** which rewrote all 42 call sites
across Levels 02-15. That was deliberate rather than adding a suffix-inferring overload: F12's whole
point is that no code path may infer a role from an ID, and leaving an inferring overload in place
would have been the exact footgun that strands Level 4A's `_checkpoint_1` PreBoss anchor under Hard's
middle rule. `StoryManager.GetCheckpointRole` returns **Entry** for an unregistered ID — the safe
default, because Entry cannot lock the boss clock.

**Test delta: +33** (1673 -> 1706 at A3's merge, on the plan's own arithmetic).
`TimelineIntegrityTests` 6 -> 12 (**+6**, four cases removed, ten added); `SessionExitGuardTests`
rewritten **±0**; new `CheckpointRoleTests` **+6**, `CollapseTremorTests` **+9**,
`F11RecoveryBudgetTests` **+4**, `StoryKillBoundaryTests` **+5**; new `Level01ContentTests` **+1**;
`StoryLevelControllerBaseTests` 15 -> 17 **+2**.

**Not delivered, and why.** (1) No authored kill boundaries — see `VERIFY-STORY-PITS` above. (2) The
Tremor's decorative half (chronal crack lines across background layers, inward palette desaturation,
prop time-ghosting, the vignette pulse) is **not** implemented: A8 owns the shared crack-glow overlay
and the C01a Reduced Temporal Effects preset, and A3 publishes
`EventBus.OnCollapseTremorChanged(TremorPayload{Level})` for it to drive. The Tremor's *gameplay* —
stages, shake, jolts, debris, unstable platforms — is complete. (3) Tremor audio (rumble bed, stone
groan, music-bus detune, HUD clock tick) is not wired; under the standing silent-placeholder rule
there is nothing to add until Package 10 supplies stems. (4) `DustBonusPercent` is still authored and
unapplied — **A10** applies it. (5) The Act III branch of the zero-Integrity collapse is a clearly
marked hook (`StoryManager.ActIIICollapseOverride`), left null for **A3b**.
