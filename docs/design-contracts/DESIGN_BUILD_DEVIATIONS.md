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
