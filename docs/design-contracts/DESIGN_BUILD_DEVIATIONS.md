# Design/build deviation ledger — P04

Decision: 2026-09-13. User selected **Option A: intended design authority plus a small deviation ledger**.

## Authority and change control

The [GDD](../design-godot-v7.md) and its explicitly adopted decision contracts define **intended behavior**. More specific, explicitly adopted resolutions supersede the historical rule they identify. A silent discrepancy between two current design sources is unresolved documentation, not permission to pick either version; reconcile it with the accepted decision and update the mirrors.

Verified code, resource data and scene/configuration bindings at a **pinned game revision/build** describe current implementation. They are evidence about that build, not authority to reverse an approved target design. Reading a resource proves its stored value, not that the game loads or applies it correctly; implementation and runtime claims need corresponding evidence.

When target and verified build differ, record the discrepancy below, implement/validate the accepted behavior or obtain an explicit design change. An intentional deviation stays open or explicitly deferred with its reason and acceptance condition; it does not become the new target merely because it shipped. Unknown/unavailable implementation stays **unverified**, not automatically compliant or defective.

Where the target deliberately leaves a number provisional or unspecified, record that fact. Do not invent a requirement or silently promote an unverified resource into the intended design. Later approval updates the GDD, contracts, mirrors and relevant [consistency checks](../scripts/README.md) together.

## Confirmed current-build deviations

**None verified in this docs workspace.** No pinned game source/build or current runtime evidence was available. This is not a claim that a build conforms. The older implementation reports are historical leads only.

For each future confirmed entry, record:
- Stable deviation ID, affected system/hero/level/mode and current state: confirmed-open, explicitly deferred, or resolved.
- Intended behavior with accepted decision/date and document reference.
- Actual observed behavior/value with pinned game commit/build, file/scene/resource identifiers and dated reproduction/test evidence.
- Owner when assigned; otherwise **Unassigned**. Do not invent a person, date or delivery commitment.
- Resolution path and concrete acceptance criteria, including content coverage/migration/balance where required; link explicit approval for any target change.
- Closing build/evidence and remaining exclusions. Retain the record after closure rather than deleting its history.

A partial fix remains partial. A configuration edit alone does not close missing scene placement, save migration or runtime validation. Status promotion follows [P02's evidence rules](PRODUCTION_SCOPE.md#p02--current-status-and-evidence).

## Confirmed open deviations — Package 11

| ID / area | Intended behavior (decision) | Observed build value (revision) | Owner | Acceptance criteria |
|---|---|---|---|---|
| **VERIFY-PAR-SECONDS** — F01 level par and F11 remaining-route budgets | V01a: shared-level `parSeconds` is the **slowest eligible hero's median Normal required-route time** with the normally unlocked kit and no purchased upgrades, rounded upward; each distinct Level 4A route gets its own benchmark. F11 additionally requires a **separately measured** `remainingRouteSeconds` per level/variant/checkpoint/difficulty, plus its depleted-resource baseline and a `budgetVersion`. | Package 11 A3 (branch `p11/A3`) authors **provisional** values, because no measurement pass has happened. Par is seeded as `authored room count x 90 s`, rounded up to the nearest 30 s: L02 360, L03 540, L04 360, L05 270, L06 360, L07 630, L08 360, L09 360, L10 360, L11 360, L12 540, L13 450, L14 540, L15 540. Remaining-route budgets are seeded as fractions of par in `StoryLevelControllerBase.RemainingRouteFractionFor`: **Entry 0.90, Middle 0.45, PreBoss 0.00** (PreBoss has no timed route, so it falls to the 25-point floor). No `budgetVersion` field exists yet. | Unassigned | A measurement pass replaces every par with a median Normal required-route time and every route fraction with a per-anchor, per-difficulty measured value, adds `budgetVersion`, and re-validates that each enabled anchor reaches the pre-boss lock with positive Integrity. The table is pinned in `StoryLevelControllerBaseTests.EveryTimedCampaignLevelAuthorsAParSecondsAndTheClockArmsFromIt`, so the replacement is a single-file edit. **These numbers must never be quoted as measured.** |
| **VERIFY-STORY-PITS** — F16 lethal openings | F16 defines lethal Story pits: crossing an authored kill boundary resolves one non-hit fall death that ignores HP, block, armor, one-hit shields and temporary invulnerability, and cannot be prevented by Defy History. | The machinery ships complete in Package 11 A3 — `PlayerController.KillPlayerNonHit()`, the `StoryKillBoundary` Area2D, and the `StoryLevelControllerBase.BuildKillBoundary` authoring helper with its edge cue — but **zero boundaries are authored**, because a sweep of all sixteen campaign controllers at `p11/A3` found no lethal opening to attach one to. Every gap is documented as deliberately non-lethal: L04's boss pit is "deliberately not a hazard" (`Level04Controller.cs:296`), L05's flood "never becomes a kill floor" (`Level05Controller.cs:435`), L07's sea is "not a rising water zone and not a kill plane" and rescues the player onto the deck they launched from (`Level07Controller.cs:437`), L06's lava front is "heavy damage and forward knockback, never a kill" (`Level06Controller.cs`), and L12/L13/L14/L15 each state they have no pits. L10's "Yard" is a recessed crowd area with a floor, not a void. | Unassigned | Either a design pass authors genuine lethal openings (and calls `BuildKillBoundary` for each), or the campaign's non-lethal-gap stance is ratified and F16 is scoped to Level 4A and the Open Fighter stages. The chokepoint's behavior is already pinned by `StoryKillBoundaryTests` (5 cases), so authoring a boundary is content work, not code work. |

## Historical leads awaiting verification

These are **verification candidates, not confirmed current defects**. For every row: current game revision/evidence **unavailable**, owner **Unassigned**, status **Awaiting verification**. The lead source is the [dated August 26 report](../design-godot-v7.md#historical-revision-and-implementation-notes), not a current test result.

| ID / area | Historical lead and current target | Acceptance / disposition |
|---|---|---|
| VERIFY-01 — Time mechanics | The report describes manual rewind, Stasis Echo and a 12-second cooldown. F03 now specifies Story-only escape Time Freeze for 5 seconds, 45-second cooldown after thaw, no charges and continued Integrity drain; death rewind remains separate. | Verify the current build against the Time Freeze section and [temporal contract](TEMPORAL_STATE_CONTRACT.md), including controls, restrictions and reload behavior. If it differs, open a confirmed deviation; close only with the required behavior demonstrated. |
| VERIFY-02 — Integrity and recovery | The report includes siphon shares/restoration paths that F01 retired. Current design uses normalized live drain, fixed starting denominator, no time refill, role-based PreBoss lock and F10/F11 recovery. | Verify actual timer/retry paths and persistence against [campaign validation](CAMPAIGN_VALIDATION.md), [checkpoint budgets](CHECKPOINT_RECOVERY.md) and [Story persistence](STORY_PERSISTENCE.md); record measured route gaps separately from code conformity. |
| VERIFY-03 — Dust and reward commits | The report names 15-dust Extractors, unapplied tier bonuses and a wallet-direct Mirror award. F05 now defines budgeted finite rewards, physical boss pickups and once-only completion banking. | Verify source manifests and collection/restart/completion behavior against [F05](DUST_ECONOMY.md), including overlapping secret claims and N01's pending boss pickup. No historic award is an implicit exception. |
| VERIFY-04 — Content and onboarding | The report describes deferred Open stages/drills and missing placements; current decisions require launch drills, Open stages before Fighter tuning, and 24 campaign level/boss slots including nine Legacy routes. | Inspect actual stage/scene coverage and standalone drill entry against the GDD and [production scope](PRODUCTION_SCOPE.md). A class/prefab or one completed prototype does not establish all-level coverage. |

Other current systems remain under the central status table's unverified/pending labels. This short backlog does not claim to enumerate every possible implementation difference.

## Source availability

| Source | Current availability and role | Follow-up |
|---|---|---|
| Main GDD, adopted contracts and HTML mirrors | Available intended-design documents, including current edits. | Commit/pin the reviewed revision when establishing a build comparison; this task does not create a commit or pretend working-tree edits are already pinned. |
| [F05 dust economy](DUST_ECONOMY.md) | Available current replacement specification with per-level budgets. It is not a recovered copy of the older Package 5 economy and not proof of placed pickups. | Compare against verified game reward manifests when available. |
| `docs/PACKAGE5_CAMPAIGN_PLAN.md` | Missing from this checkout; searching available Git history for that exact path found no source to recover. Earlier claims of authored Levels 2–15 cannot be verified here. | P05 user decision (2026-09-13): flag the missing reference and move on. Recovery and replacement/index authoring are deferred; no new dossier index is created. Retain this availability flag and provenance requirements if the source is revisited later. |
| Game code/resources, scenes, builds and test reports | Not available here as current verifiable implementation evidence. Referenced resource paths alone are not proof. | Supply a game repository/build and exact revision before promoting candidates into confirmed deviations or current status passes. |

If an original document is later recovered, retain its repository/archive location and immutable commit/release or content hash plus date. Reconcile it against current accepted rules before using it as current authority. Do not revive old reward budgets, checkpoints, time mechanics or retired content just because they appear in a pinned historical file.

P04 delivers the authority rule and ledger structure. Build verification, source recovery and the listed implementation/content work remain pending.
