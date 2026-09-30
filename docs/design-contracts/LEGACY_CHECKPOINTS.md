# Legacy Level checkpoints — F12 Option A

> **RETIRED 2026-09-28 (story review S27).** The per-character Legacy Level (4A) was dropped from the game by user decision. This contract is kept as history only and is not design of record. See [STORY_REVIEW_2026-09-27.md](../STORY_REVIEW_2026-09-27.md).


Decision: 2026-09-12. Every character's Level 4A has **exactly two checkpoints: Entry and PreBoss**, both enabled on Easy, Normal and Hard. There is no Middle checkpoint or extra hidden recovery anchor. This shared contract applies to every `Level_04A_Legacy_{characterID}` variant and supplements its future scene/dossier manifest.

## Roles and authored identifiers

| Level family | Role | Default authored ID | Difficulty behavior |
|---|---|---|---|
| Level 4A, each character variant | Entry | `{levelID}_checkpoint_0` | Enabled on all difficulties; self-activates once on fresh entry |
| Level 4A, each character variant | PreBoss | `{levelID}_checkpoint_1` | Enabled on all difficulties; strike to activate |
| Shared campaign levels | Entry / Middle / PreBoss | Existing `{levelID}_checkpoint_0 / _1 / _2` | Existing rules: Middle inactive on Hard in Acts I–II, active on Hard in Act III |

Here `levelID` is the existing full scene/level identity, including the character suffix for 4A. These are authoring defaults, not claims that scene files already use them. Preserve a verified existing stable ID through an explicit manifest mapping/alias where required; never infer a role from its numeric suffix. If an older 4A save actually names a different checkpoint ID, migrate only through that variant's verified mapping under F10. Do not reinterpret a saved `_1` blindly as PreBoss or grant a boss-clock lock during migration.

Each checkpoint exports `checkpointRole: Entry | Middle | PreBoss` alongside its stable ID. The Integrity system responds to successful activation of **PreBoss**, not the literal name `checkpoint_2`, array position, or checkpoint count. Freeze the gauge and save final Integrity, the active anchor, and the completed encounter/puzzle baseline in one F10 checkpoint transaction. Reload retains the lock and score without refilling the gauge or replaying checkpoint benefits.

## Route contract

Order the required route as: Entry → required kit gates and the independent Eraser encounter → late Font approach → PreBoss → boss. A character's kit-gate order may vary, but every mandatory pre-boss objective, including the F04 Nexus Ultimate puzzle and Eraser encounter, must be complete before PreBoss can activate. **V01c:** preserve each authored 4A gate's specific ability/event as a narrow exception to shared puzzle-prop eligibility; declare it per gate. This grants no general construct/decoy shortcut elsewhere and requires no purchased perk. F04's designated Nexus authorization remains unchanged; see [puzzle source eligibility](CAMPAIGN_VALIDATION.md#v01c--dedicated-puzzle-props-and-intended-player-actions). Optional detours remain optional.

The single Eraser debut uses an authored route trigger, `{levelID}_eraser_debut`, in a suitable encounter space between Entry and PreBoss. Crossing that trigger while gameplay permits begins the encounter and its existing Sarah bark; it grants no checkpoint, Mending, rewind refill, or Integrity lock. Its timing never depends on a middle-checkpoint activation or difficulty. Keep the existing single-Eraser composition and F05 reward allocation.

Maintain one encounter instance per reconstruction. Before PreBoss, a checkpoint reload rebuilds the ambush under F10's encounter baseline; if the encounter respawns, its route trigger must remain functional. A persistent “trigger seen” flag must not suppress required enemies and strand an encounter gate. Already-collected rewards remain claimed; repeated crossings never spawn duplicate live waves. After PreBoss, the saved baseline treats all required approach encounters as completed. First-view presentation flags remain separate from encounter state. Time Freeze cannot grant objective progress or activate the encounter while the world is suspended.

Retain one Restoration Font for 4A, with the existing difficulty uses/potency. Place it on the late approach after the Eraser encounter and before PreBoss; this replaces the generic “between mid checkpoint and boss” placement for 4A. It is a healing object, not a third checkpoint. Retain ordinary placed Feasts and the unchanged F05 dust envelope.

## Recovery and validation

Before PreBoss, checkpoint reconstruction returns to Entry on every difficulty. Normal death rewind still uses its grounded-history landing; the two-checkpoint rule does not convert every death into an entrance reset. After PreBoss, recovery returns to that anchor with the boss clock already locked. Existing difficulty rewind refresh, one-time checkpoint Mending, and F10 attempt-state persistence apply. Hard's “middle inactive” rule cannot disable 4A PreBoss merely because its default ID ends in `_1`.

Under F11, author an Entry recovery budget per 4A variant/difficulty covering the full mandatory approach and reconstructed encounters. V01a assigns each distinct 4A route its own Normal/no-purchased-upgrade median par, rounded upward; do not pool different character routes. Validate all three difficulties separately under [campaign route validation](CAMPAIGN_VALIDATION.md), retaining F11's actual per-difficulty retry measurements. PreBoss has no remaining timed route. Do not invent or budget a 4A middle anchor.

Every variant dossier/scene manifest must supply actual checkpoint IDs/roles/positions, migration aliases if any, Eraser trigger/encounter IDs, required pre-boss objective IDs, Font placement, and F11 measurements. No per-character scene manifests are supplied here; mapping and runtime validation remain pending.

Acceptance: verify exactly two enabled roles on all difficulties, correct entry activation and pre-boss strike, no timer lock at Entry/Eraser, no boss entry before the lock, all required kit gates before PreBoss, a functional reconstructed ambush without duplicate claims, safe Font/pickup access, correct Entry versus PreBoss reload, and verified legacy-ID migration without new rewards or premature boss locks. Shared-level checkpoint behavior must remain unchanged.
