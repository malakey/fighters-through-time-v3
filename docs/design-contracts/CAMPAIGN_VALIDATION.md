# Campaign route validation — V01

> **Amended 2026-09-28 (story review S27):** the per-character Legacy Level (4A) is retired, and with it F04 (the Nexus Resonance Source) and F12's two-checkpoint 4A structure. Every rule below that applies only to 4A, F04 or the Legacy checkpoint contract is history, not design of record; the shared-level rules are unchanged. The ending counts Levels 2–15 (14 IDs).

Design revision: 2026-09-12. V01a is resolved by user-selected **Option A: one shared par per level, calibrated to the slowest eligible hero's typical required-route time on Normal; each distinct 4A route has its own par**. V01b is resolved by user-selected **Option A: a local Reset Puzzle interaction restores only the unsolved puzzle's props/mechanisms, with no dust cost or combat-resource reset**. V01c is resolved by user-selected **Option A: dedicated puzzle props and explicitly intended player actions operate mechanisms; combat constructs/decoys cannot substitute, with authored 4A ability puzzles preserved**. Current V01 design choices are resolved; measurement and implementation remain pending. No measured route times, approved numeric pars or runtime pass results are supplied.

Authority: [Timeline Integrity in the main design](../design-godot-v7.md), [checkpoint recovery budgets](CHECKPOINT_RECOVERY.md), [4A checkpoints](LEGACY_CHECKPOINTS.md) and [Story persistence](STORY_PERSISTENCE.md). Keep F01's drain equation and Easy/Normal/Hard budget multipliers of 2.0/1.5/1.2, current tier/ending thresholds and the pre-boss clock lock.

## V01a — Shared par from the slowest hero's typical pace

For each shared timed level, measure every eligible roster hero on **Normal** using the normally unlocked kit and no purchased grid upgrades. Respect unlocks that happen during the route; never give a later ability early. Use the ordinary fresh-entry resource baseline, not infinite developer meter, replenished consumables or a checkpoint recovery grant. Testers should be competent with each kit and familiar with the route/puzzle rules; this is representative play, not a speedrun or first-time puzzle-discovery benchmark.

Collect repeated valid runs with comparable player familiarity and document the distribution, failures and exclusions. Use each hero's median valid required-route time as its typical pace, then author:

```text
heroTypicalSeconds = median(valid measured Normal required-route times for that hero)
sharedParSeconds = ceil(max(heroTypicalSeconds across all eligible heroes))
```

Round the final shared par upward to a whole second. Take the maximum of hero medians, not the slowest individual attempt, roster-wide pooled median, fastest run or a favored character's time. A slow run is not invalid merely because it is slow; deliberate idling, route deviations or tool-assisted execution must be identified explicitly. Report failed/collapsed runs and completion frequency alongside the valid timing samples so a median of rare successes cannot hide a route problem. Missing hero coverage or inadequate representative evidence leaves the par provisional.

Each distinct character-specific Level 4A route uses its own hero's median under the same Normal/no-upgrade method, rounded upward. Do not pool nine different Legacy routes into one par. The authored shared-level par does not change with selected hero, purchased grid, current HP, run performance or difficulty. This adds no adaptive timer or character-specific timer on shared levels.

## What the timed measurement includes

Use F01's live Integrity clock from entry through successful PreBoss activation after required approach objectives. Include mandatory combat, traversal, puzzles, mechanism waits, required activation/channel time and applicable Tremor delays. Time Freeze movement counts because Integrity keeps draining. Exclude the boss, optional detours and paused menus/dialogue/presentations.

Keep every starting Extractor alive and find no optional secret in the calibration route; their drain reductions cannot subsidize the benchmark. Mark accidental destruction/secret discovery as a different route sample, not a qualifying all-alive sample. Do not subtract ordinary execution mistakes from a completed route's live time. Death/recovery and checkpoint-retry runs belong to the separate recovery coverage; do not mix paid recovery allowances into fresh-entry par calibration.

Calibration must use the playable combat, enemy and puzzle rules. If an instrumented timing capture is needed beyond an unapproved provisional deadline, mark it calibration-only and retain elapsed live time and relevant Tremor/clock effects. It cannot count as a successful completion under the eventual authored budget.

## Apply and verify the budget

All heroes on that shared level receive the same authored par multiplied by the selected difficulty's existing 2.0/1.5/1.2 multiplier. For an illustrative shared par of 600 seconds, budgets remain 1,200/900/720 seconds (20/15/12 minutes). This is arithmetic, not a measured level result. Faster heroes may have more spare time by design.

Normal par calibration is not proof that Hard is feasible: Hard's enemy HP/count, healing/Rally and checkpoint changes can alter actual completion time. Validate each level/eligible-hero/difficulty combination with the actual difficulty rules and intended unlock stage. Validate each 4A variant on all three difficulties. Untimed levels have no Integrity par requirement but retain progression/kit checks.

Use distinct records for:
- Fresh-entry required route with no purchased upgrades, all Extractors alive and no secret.
- Each actually enabled checkpoint retry under F10/F11, including depleted resources, the longer Hard route where Middle is disabled, mechanism waits and F12's two-anchor 4A structure.
- Optional exploration/Extractor/secret routes and representative legal grid builds, assessing time, dust, tier and ending consequences separately from the required-route benchmark.

Required-route acceptance must be checked against the authored budget; recovery retains F11's positive-Integrity requirement and its separately measured per-checkpoint/per-difficulty remaining-route time plus provisional 20% margin. Do not replace those recovery measurements with the Normal median or assume multiplication proves success. A route that fails coverage needs evidence and a proposed content/budget change; V01a pre-approves no enemy tuning, multiplier changes, tier changes, extra time grants or optional upgrade requirements.

## Authoring and evidence record

Maintain records keyed by level/variant, hero, difficulty, route class and applicable checkpoint. Include:
- Build, scene/route and rules revisions; tester familiarity, sample count, individual live times, failures/exclusion reasons and the hero median.
- Entry/unlock kit, purchased grid state, HP/meter/charges/cooldowns/uses and starting Extractor/secret state.
- Required path and puzzle assumptions, mechanism/Tremor/channel waits, checkpoint roles and any recovery transaction.
- Selected shared par or 4A par, determining hero, rounding, difficulty budget and authored budget revision.
- Actual final Integrity, completion outcome, claimed optional rewards/tier/ending contribution where relevant, replay/evidence and pending/blocked/pass/fail status. N05 gives each of Levels 2–15 one equal ending contribution; exclude 0/1, retain stored precision and keep the 50% threshold. Par and duration never weight the ending. Measure threshold attainability across full campaigns and difficulties; no runtime balance pass is implied. See [ending rules](NARRATIVE_RESOLUTION.md#n05--equal-weight-ending-average).

Revalidate affected hero medians and coverage when kit, route, encounter, puzzle or clock rules change. Publish an explicit authored revision; do not dynamically recompute a live player's par or refill their gauge. F10/F11 committed recovery outcomes retain their saved result and budget version; any migration affecting active attempts must be explicit and cannot replay benefits.

## V01b — Local reset for an unsolved puzzle

User selected Option A on 2026-09-12. Each Story puzzle that can lose required movable props or enter an unusable arrangement provides a nearby, clearly labelled **Reset Puzzle** interaction using the existing remappable Interact action. It requires no dust, meter, consumable, purchased perk or character-specific ability. Accept one reset per deliberate input activation; held input and duplicated callbacks cannot repeatedly reset it.

Author the control on stable playable ground reachable from every legal unsolved arrangement, outside the props/platforms it moves. It must remain usable when the player has lost the very object needed to solve the puzzle. This is a local world interaction, not a pause-menu level restart. Use ordinary alive/action-state/range restrictions; Time Freeze, death recovery and suspended puzzle interactions cannot activate or queue it. No added confirmation dialog or reset cooldown is required.

### Reset scope and access safety

For the selected unsolved puzzle ID only, return its registered resettable props and mechanisms to the authored starting arrangement: restore missing required apples/branches, coil positions/orientations, gear/platform orientations and local unsolved sequence/occupancy state as applicable. Clear residual prop motion and stale contacts, then recompute plate/beam/lever state from the restored arrangement. Use stable prop IDs; replace missing instances without duplicating surviving ones.

Overlay already committed solved gates, latches and prerequisite access facts. Never close a permanently solved gate, remove its required access mechanism or undo another puzzle's progress. Disable/hide Reset Puzzle once its puzzle is solved. Recheck solved state when committing a reset; a simultaneous committed completion wins, so the reset cannot undo it. F04's unsolved Nexus puzzle may clear its local armed authorization and restore its available source under the existing rule, without changing normal meter; a solved Nexus gate/source stays solved/disabled.

Validate the complete replacement geometry before applying it. Never move, teleport, crush, damage or enclose the player/enemies to make a reset fit. The reset station and authored layouts must provide clear restoration volumes; if an actor temporarily obstructs one, refuse the whole reset with a brief obstruction cue and allow another attempt once clear. Do not leave a partially reset room. Validate access and escape from every authored intermediate arrangement, rather than treating repeated rejection or a checkpoint reload as an acceptable permanent solution.

### Preserve the live attempt

Keep enemies and encounters, player HP/meter/Rally, block charges, ability/Time Freeze cooldowns, rewind/anchor counts, Defy use/protection, healing uses, rewards, dust, unrelated player constructs, external hazard schedules and checkpoint state unchanged. Resetting puzzle props grants no damage credit, meter, healing, drops, completion signal or repeat reward. Do not trigger on-destruction attacks or rewards when retiring an old prop instance.

Integrity continues its existing live drain through interaction/reset feedback; do not pause it, refill it, rebase the level clock or reset the current Extractor/secret facts. Normal pre-boss/menu/presentation clock rules remain authoritative. Time spent returning to the control and repeating the puzzle is still real route time. The action grants no invulnerability or combat escape and does not rewind the world.

Reset locally in one simulation transaction, keyed by puzzle ID and reset execution/generation. Invalidate old prop contacts and delayed unsolved mechanism callbacks so they cannot solve, reward or repopulate the reset room afterward. Reset-specific generation/instance identity must survive restoration of that same live instance. Ordinary F10 load still reconstructs unsolved transient arrangements with persistent solved/access facts; it does not replay the Reset Puzzle interaction or create an exact-position puzzle save. Reset must not modify persistent reward claims or solved/latch facts.

### Pending verification

Verify lost/unreachable apples, displaced/rotated coils, every authored gear arrangement, repeated reset, no-op initial reset, separate adjacent puzzles, solved/partially latched access, reset/completion ordering and the F04 source. Test every eligible entry kit and difficulty, including no upgrades, empty meter, spent healing and unavailable cooldowns. Verify safe control access, obstructed restoration refusal, no actor displacement or softlock, stable object counts, stale callback rejection and no rewards/resources/clock refill. Check Time Freeze rejection, live Integrity drain, ordinary load and same-instance restoration. Record representative reset/retry time in the V01 route matrix and F11 remaining-route assumptions where relevant; this decision invents no measured allowance.

## V01c — Dedicated puzzle props and intended player actions

User selected Option A on 2026-09-12. Story puzzle mechanisms accept only their designated puzzle props and explicitly authored player actions. Similar appearance, physical overlap, combat damage, elemental theme or common ownership does not grant puzzle eligibility. Combat constructs, decoys, enemies/corpses and their emitted effects cannot substitute for puzzle weights, beam relays or mechanism operators.

Author each mechanism's puzzle ID, accepted prop IDs/types, accepted player actions and intended condition. Required shared-level solutions must work with every eligible hero's normally unlocked kit and no purchased upgrade. Do not infer new ability shortcuts from physics contacts. Preserve ordinary movement/platforming as authored and the existing 4A exceptions below.

| Mechanism | Accepted sources | Exclusions and authoring requirements |
|---|---|---|
| Orchard pressure plates | Designated apples/weights belonging to that puzzle; the hero only if the plate explicitly calls for player occupancy | No combat constructs, decoys, mobs/corpses, projectiles or incidental loose objects. Use authored puzzle weights, not combat character weight; a player-operated plate treats eligible heroes equivalently. |
| Wardenclyffe beam routing | That puzzle's central power node, designated movable conductive props, authored obstacles and receiver | Tesla's summoned combat coils/fences and lightning do not conduct, power or redirect the puzzle beam. Combat bodies/effects are not implicit beam-routing obstacles. |
| Branches, levers and gear controls | Explicitly intended ordinary player interaction or direct attack from the currently available kit | A strike-operated control must work with the ordinary basic attack. Only explicitly accepted direct player attack variants may also trigger it; autonomous attacks, zones, DoT and enemy hits cannot do so. |
| Local Reset Puzzle | The nearby living hero using the existing Interact action under V01b | No remote projectile/construct activation, accidental contact, Time Freeze activation or queued reset. |
| Character-specific 4A gate | Its explicitly authored hero ability/event, or ordinary traversal condition | A narrow gate exception, not blanket permission for that ability's effects to operate every mechanism. Preserve F04 Nexus authorization for the required Ultimate puzzle. |

Use intended puzzle contacts/conditions independently of combat damage. A branch or lever reacting to a permitted strike does not grant damage-based meter, Rally, a combat kill or repeat drops. An Ultimate cannot brute-force an ordinary shared-level gate just by dealing more damage. Acceptance depends on the declared action/source and condition, not raw damage or an arbitrary collision-body class.

### Contact ownership, feedback and state

Give props a stable identity and owning puzzle ID; a prop from another room cannot satisfy a mechanism merely because its type matches. V01b reset restores those identities and ownership. Count each valid plate occupant once, remove it when it leaves/is removed, and recompute occupancy after reset or load. Do not count multiple collider contacts as extra weight.

Resolve each accepted lever/branch action once by source execution/contact identity and mechanism ID; duplicate callbacks, sustained hitbox overlap and ticks cannot retrigger rotation or object release. A newly executed allowed action can act again if the puzzle remains unsolved and its normal mechanism state permits it. Preserve authored sequence/latch behavior without inventing a generic cooldown or new timing window.

For beam routing, evaluate the authored puzzle network and current prop transforms. Recompute its valid connection after movement/rotation/reset; a lingering particle beam is not proof of power. No puzzle progress can occur during Time Freeze, including at an adjacent-room boundary. Reject stale mechanism callbacks across a V01b reset generation.

Visually distinguish designated weights, conductive relays and operating surfaces with consistent puzzle markings and matching receiver/control cues; do not rely on color alone. A summoned Tesla coil should not look like an interchangeable routing prop. Show eligible interaction prompts and brief valid-action feedback; an ineligible contact causes no progress, penalty or resource award. Keep implementation identifiers out of player-facing prompts.

A completed gate and required access state still commit together under F10, once; repeated accepted inputs or restoring a live instance cannot duplicate completion/rewards. Ordinary checkpoint load reconstructs the unsolved puzzle from its authored props overlaid with persistent solved/latch/access facts. This changes no combat collision/damage rules outside the puzzle-eligibility checks.

### 4A exceptions and pending verification

Keep each 4A's already-authored ability gates and the scheduled kit unlocks they require. Register the intended ability/event specifically for that gate, including any explicitly required ability-produced object, without giving that object general plate/beam eligibility elsewhere. The F04 Nexus Source authorizes only its designated puzzle Ultimate; it spends no normal meter and grants no combat power/reward. This choice adds no purchased-perk requirement or new character-specific gate to shared levels.

Verify every required shared puzzle with all eligible entry kits and difficulties; test each 4A variant separately. Include accepted apples/relays/basic strikes/Interact, explicit hero-occupancy plates with different combat weights, and rejection of Tesla coils/fences, Cleopatra decoys, turrets, enemy/corpse/projectile contacts, zone/poison ticks and cross-puzzle props. Test multiple colliders, multiple sources, lever duplicate callbacks, moving beam relays, reset generations, Time Freeze/room transitions, solved/load persistence, clear markings and the narrow 4A/F04 exceptions. Missing scene declarations or measurements remain pending coverage.

## E01 — Readable Extractor detours

User selected Option A on 2026-09-12: environmental cues and brief feedback, retaining F01's timer, F05's rewards and the existing clock/streams HUD. The opportunity is slower **future** drain plus a separate dust pickup; an optional detour does not guarantee a higher final Integrity.

### Placement and teaching

Keep machines off the required route. For early non-secret Extractors, frame the machine or its recognizable silhouette and the detour entrance from the main path before the player passes the choice. Cables, siphon connections and readable discharge warnings can connect the machine to its effect without an extra map, waypoint or compulsory fight. Use shape/motion and placement as well as color; preserve danger footprints under Reduced Temporal Effects. Do not expose a designated secret's exact location merely to advertise this benefit: retain its authored discovery clues.

Use the existing short, paused Level 2 Sarah introduction to teach: “Destroying Extractors slows future Integrity loss. It cannot restore what is already lost.” This is part of that introduction, not a new tutorial interruption or pause on destruction. Level 1 remains untimed; its machines teach their existing hazard and dust reward.

Early placement should offer a plausible drain-saving opportunity in measured routes. Late Extractors may remain deliberate dust-versus-Integrity choices; do not label them as guaranteed time gains or require them for completion. Keep required-route par calibration all-alive/no-secret under V01a. Actual placement and detour payoff still require measurements; this decision does not certify existing layouts.

### Feedback and reward separation

On a real, first destruction while the Integrity clock is running, use a brief nonblocking “Integrity drain slowed” notice through the existing feedback system, with the existing machine shutdown/siphon-stream change. The gauge retains its current value. Do not show “time gained,” a healing animation, a percentage refill or a seconds-saved estimate. No new persistent counter, numerical drain readout or menu is added.

Dust is the separate physical reward already allocated by F05. Its actual collection produces the existing +N dust feedback; destruction alone does not credit the wallet or imply that an uncollected pickup was banked. A secret/Extractor overlap retains its combined once-only reward and existing discovery feedback without duplicate dust or drain notices.

Untimed levels and a locked PreBoss clock do not display “drain slowed”; destruction/reward cues may still play normally. N01's remaining-machine shutdown at sealing grants no destruction or dust feedback. Reconstructing an already-destroyed machine on load does not replay the notice or reward. These presentation rules do not change persistent destruction/secret facts, drain arithmetic or the fixed starting denominator.

Apply the same distinction to Act III conduits, intake valves and anchor pylons, using the current resonance-clock terminology and the existing narrative knowledge boundary. Do not reveal the Forge before Level 14. Feedback never covers required hazard warnings, the Integrity gauge or critical prompts, and never pauses live Integrity.

### Route validation

Compare required-route and optional-detour runs using the same hero, difficulty and relevant starting state. Record added live detour time, the destruction point, remaining live route to PreBoss, actual final Integrity and collected dust. Include early/late choices, all eligible heroes, secret overlap, depleted kits, reduced effects, skip/reload and untimed/locked-clock cases. Validate both readability and measured payoff; do not infer balance from the cue text.

For an isolated ordinary Extractor, let D be added live detour seconds spent at the old raw drain factor s, and T the remaining live route after destruction. If all other drain events are identical and the gauge does not hit zero, net Integrity improves only when 0.2 × T > s × D. The common normalization factor cancels. With raw s = 1.6 and D = 10 seconds, more than 80 seconds of live route must remain to break even; 1.6 is a raw factor, not 1.6 times the normalized all-alive rate. Secret reductions, different event ordering and recovery require the full F01 equation and actual route records. This calculation is authoring guidance, not player-facing UI.

E01 is resolved in design; per-level placement, final localized copy, feedback implementation and runtime/balance validation remain pending.

## Validation status

- Carry forward F03's suspended adjacent-room Time Freeze rule, F04's resource-independent Nexus puzzle authorization and F10/F11 exhausted-resource recovery. Do not reopen those approved decisions by assumption.

All runtime measurement, per-level authoring and gameplay/balance sign-off remain pending. This document supplies no fabricated matrix passes and does not expand launch scope.
