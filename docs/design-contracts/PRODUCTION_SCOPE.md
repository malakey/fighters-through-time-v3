# Production scope and evidence — P01

Decision: 2026-09-12. User selected **Option A: retain nine Legacy Levels, reuse suitable era art and musical themes, add necessary variants, and prototype one representative 4A before accepting production estimates**. This is a design/budgeting contract; it does not supply finished assets, a playable prototype or measured costs.

## Count authored content separately from original assets

| Scope | Authored coverage |
|---|---|
| Shared non-tutorial campaign | 15 levels and 15 boss encounter slots, Levels 1–15 |
| Per-character Legacy content | 9 distinct 4A levels and 9 boss encounter slots, one for each current roster hero |
| Total non-tutorial production | **24 level/boss slots** |
| One campaign run | 15 shared levels + the chosen 4A = **16 non-tutorial levels**, plus Level 0 |
| Separate coverage | Level 0 and its per-hero opening variants, Time-Ship hub, Fighter arenas and drills; not extra non-tutorial boss slots |

An encounter slot is a required implementation/presentation binding, not an automatic new atlas, rig, music composition or boss archetype. Mirror Paradox retains its existing fighter-kit reuse. Reused assets still incur integration, layout, telegraph, performance and validation work. Count shared source assets once and per-level adaptation work separately.

Keep every 4A's distinct route, threatened nexus outcome, full-kit puzzles, Eraser encounter and per-character boss. Shared environments cannot turn a Legacy Level into a replay of a sealed shared mission; N02's separate unresolved wound remains. Preserve F04's zero-meter puzzle authorization, F12's Entry/PreBoss checkpoints, F05's common reward budget and N05's equal ending contribution. This decision changes no mechanics or roster commitment.

## Legacy art and music coverage

The following are **candidate reuse families**, not verified asset IDs or claims that files exist. Confirm fit against each authored nexus moment. Every row needs its own level/encounter record even when several point to one asset.

| Legacy hero | Candidate environment reuse | Candidate music reuse / missing-work obligation |
|---|---|---|
| Leonardo da Vinci | Florence workshop/era kit, adapted to the unresolved nexus outcome | Florence theme and compatible exploration/combat/climax stems |
| Joan of Arc | Orléans-era kit adapted to the separate vanguard/nexus setting | Orléans theme/stems with any necessary arena transition |
| Nikola Tesla | Chicago-era workshop/exhibition materials as appropriate to the nexus | Chicago theme/stems; validate puzzle and combat arrangement |
| Cleopatra | Alexandria-era palace/material family | Alexandria theme/stems; validate distinct homecoming/boss delivery |
| William Shakespeare | Globe-era theatre materials and props | Globe theme/stems; preserve puzzle cues and dialogue clarity |
| Abraham Lincoln | Gettysburg-era materials where the authored nexus supports them | Gettysburg theme/stems when suitable; no forced battlefield treatment for a different nexus |
| Albert Einstein | Review compatible existing period/interior assets against his authored nexus; source mapping pending | No shared home-era score is assumed. Select suitable existing material or budget a targeted arrangement/new material |
| Wolfgang Amadeus Mozart | Review existing period/interior assets against his authored nexus; source mapping pending | Select a suitable musical theme/stem family or budget missing material; character identity does not remove composition/arrangement work |
| Pocahontas | Review existing landscape/nature materials against her authored nexus; source mapping pending | No shared home-era score is assumed. Select suitable existing material or budget targeted new material |

For each variant, record stable level/hero and boss encounter IDs, source asset/music IDs, reuse-versus-adaptation-versus-new work, and pending authoring/validation. Do not manufacture an ID/path for an unavailable resource. Period, place, personal story and puzzle readability take precedence over reuse; a superficially similar asset/theme is not automatically suitable.

Art coverage includes tiles/props/backgrounds, the puzzle's readable operating surfaces, boss portrait/animation/attack-phase cues and defeat/sealing presentation. Reuse existing systems/templates and compatible assets; explicitly budget any missing geometry, poses, effects or illustrations. A reused boss rig still needs the authored encounter's mechanics and readable tells; a tint alone does not establish a distinct encounter.

Each 4A needs explicit exploration, combat and climax/boss music bindings using the existing three synchronized stem layers. Prefer suitable existing themes and compatible stem sets; add a limited arrangement, transition or missing material when needed. Record loop lengths, synchronization and transitions into the Eraser/boss/sealing beats. Shared composition does not mean missing coverage; nine coverage rows do not mean nine commissioned tracks. C01's mix priority and clear warning/dialogue paths still apply. New assets ultimately follow the existing artist-produced release-content requirement.

## Representative prototype before estimates

Use **Joan's 4A** as the first baseline prototype because its existing vanguard/nexus example and Orléans asset family permit a concrete reuse comparison. This is work sequencing, not a claim that her route or boss is authored. Keep her accepted full-kit puzzle requirements, Eraser encounter, Entry/PreBoss pair, per-character boss and sealing flow in the prototype. Do not replace missing boss design with a fabricated dossier.

Take the prototype through a complete playable route with representative art/music integration and a recorded list of placeholders or unfinished presentation. Exercise zero normal meter at the F04 source, failed/retried puzzles, local reset, depleted legal resources, all difficulties, checkpoint/reload, boss/PreBoss clock lock and final sealing. Record actual authoring/integration/iteration effort, new versus reused assets/stems and outstanding polish. Placeholder-only work cannot establish the full production cost.

Compare that measured scope with a shared level at a comparable completion/quality stage. Until such evidence exists, **“half a shared level's cost” is an unverified hypothesis**, not a multiplier, schedule promise or accepted estimate. No staff-hour totals or completion dates are supplied here.

After the baseline, estimate each remaining variant from its actual route/kit, boss, art and audio requirements. Joan's result does not prove the cost of construct/summon-heavy puzzles or heroes without reusable home-era assets. Identify such exceptions and use targeted follow-up prototypes where uncertainty materially affects the estimate. Keep all nine in scope while revising estimates; do not silently cut puzzle gates, checkpoint roles, bosses or personal story to match a hypothetical cost.

## Authority and pending work

See [main design](../design-godot-v7.md), [Legacy Level template (M22)](LEGACY_LEVELS.md), [campaign validation](CAMPAIGN_VALIDATION.md), [Legacy checkpoints](LEGACY_CHECKPOINTS.md), [narrative decisions](NARRATIVE_RESOLUTION.md) and [Mirror Paradox](MIRROR_PARADOX.md). Asset paths, final music selections, dossiers, prototype execution, measured estimates and runtime/content acceptance remain pending. This documentation update is not production completion.

## P02 — Current status and evidence

User selected Option A on 2026-09-12: keep one central current table near the top of the [main GDD](../design-godot-v7.md#current-delivery-status--2026-09-12-p02), mirrored on the [docs home page](index.html#current-status). Keep dated revision/implementation notes as history; do not erase old results or present them as validation of newer rules.

The initial table is a documentation assessment of the current design contracts. This workspace provides no game source, current build or executable test report for verifying the latest implementations. Historical claims such as the August suite count remain attributed to that dated report. They cannot promote today's Time Freeze, timer, economy, defensive rules or other changed requirements.

Track four independent columns:
- **Designed:** accepted behavior and constraints exist, with missing dossiers, field/scene mappings, tuning or authoring details stated. Specified does not mean all production details or balance are final.
- **Implemented:** relevant behavior is checked in a pinned game revision/build. Mark Unverified while that evidence is unavailable; use Partial with named gaps when only some current requirements are verified.
- **Content placed:** the required levels, characters, arenas and assets/configurations actually bind and exercise the system. A manager class, prefab or single prototype does not establish campaign-wide coverage. Use N/A only for a scope without scene/content placement, not to hide missing integration.
- **Validated:** dated results exercise the current requirements in the named build and content scope. Keep Pending without these results, Partial for bounded coverage, and record failures/limitations rather than describing a broad suite count as blanket success.

For every promotion, retain an evidence record linked from the central row: game commit/build identifier, inspected source/scene/asset IDs, current contract/revision covered, test or playtest report with date and environment, covered heroes/levels/difficulties/modes as applicable, and remaining gaps. Record the reviewer/owner when known; do not invent a responsible person or a completion date. References to design contracts remain labelled as design/pending checks until actual implementation evidence is added.

Broad rows are summaries: one passing subfeature cannot mark the whole group implemented, placed or validated. Link detailed per-feature coverage when needed and label the summary Partial. Deferred native online remains outside launch scope regardless of verified infrastructure. Asset/route/migration work and runtime balance do not become complete because Markdown links or HTML tags pass.

When evidence or design changes, update the main table, its date, relevant evidence record and home-page mirror in the same revision. A rule change requires reassessing affected evidence; keep unaffected verified coverage without falsely carrying forward a whole-row pass. Preserve historical reports and their original build/date. Do not mark unknown work missing merely because this repository is documentation-only.

P02's table and status definitions are delivered. Gathering game-build evidence, filling asset/scene mappings and running the listed validation remain pending.

## P03 — Targeted source/mirror checks

User selected Option A on 2026-09-12: retain the hand-maintained HTML pages and add a runnable check rather than replacing the publishing pipeline. Run `pwsh -NoProfile -File scripts/Test-DesignConsistency.ps1` from the repository root; see [checker usage](../scripts/README.md) and [the rule registry](../scripts/design-consistency-rules.json).

The main design and mirrors must be updated together. The initial registry covers 15 key-rule groups, with additional status-table/date parity and historical-label checks. It catches selected numeric/wording drift in explicitly anchored current passages. Missing/ambiguous anchors fail; dated history is preserved. It does not certify all prose, full hyperlink integrity, runtime behavior or evidence behind a status label.

P03 also removes the fixed 450-line narrative total from the campaign mirror, makes roster scaling explicit in the narrative mirror, and aligns the remaining Alexandria scene note with N03's brief recognition and larger 4A payoff. Update manifest expectations only for accepted design changes; do not bypass a failed rule without resolving its cause. CI integration remains unconfigured.

## P04 — Intended design versus build evidence

User selected Option A on 2026-09-13. The GDD and adopted contracts define intended behavior; verified versioned code/resources describe a build and cannot silently override approved changes. Use the [design/build deviation ledger](DESIGN_BUILD_DEVIATIONS.md) for actual differences, evidence, ownership when known and acceptance criteria.

No current-build deviations have been verified in this docs workspace. Historical reports are verification candidates; missing implementation evidence stays unverified. The ledger also identifies the unavailable Package 5 dossier source and the available F05 replacement economy, with P05 recorded as flagged-only by user decision on 2026-09-13: source recovery and replacement/index authoring are deferred.

## Deferred pre-release tasks (2026-09-24 review)

- **Content rating and sensitivity (G08, user chose Defer on 2026-09-26):** age rating (e.g. via IARC) and any cultural/historical sensitivity review are pre-release tasks with no target rating yet. Revisit before content lock; relevant material includes combat involving real historical figures, historical tragedies and brainwashed civilians.
