# Combat validation — F09 Option A

Design decision: 2026-09-12. Keep current combat rules and validate them before choosing balance changes. This is a specification and pending test record, not a report of runtime results.

Authority: [main design, Combat Mechanics](../design-godot-v7.md). Existing shared frame data, character profiles, cancel rules, action locks, and mode restrictions remain authoritative. Preserve zero landing lag, zero jump squat, eight-frame basic shieldstun, and the existing shatter rules. This decision adds neither a landing-recovery penalty nor a universal combo breaker. Story Enemy Stagger Discipline stays separate from Fighter Mode.

## Contact and action timing

For each named attacker/defender action pair, use a common simulation-frame origin at contact. Record the earliest frame each action can legally start after all current restrictions. Define:

`advantage = defenderActionFrame - attackerActionFrame`

Positive means the attacker can start the recorded action first; negative means the defender can; zero means simultaneous availability. Unavailable actions are recorded as unavailable, not assigned a fabricated frame. Being first to act is distinct from landing first, having a guaranteed punish, or making an attack safe.

Account for remaining active/recovery frames, permitted attack/special/block/jump/roll cancels, landing cancellation, shared hitstop, current charges and cooldowns, ongoing statuses, and shatter. A legal cancel may start an action before ordinary recovery ends. Include follow-up startup, reach, collision, invulnerability, and target-state legality when classifying the outcome. Grabs cannot connect against targets still in shieldstun. Evaluate block breaks separately from non-shattering blocks.

Direct blocked hits currently have two shared hitstop frames; include them consistently for both actors. Construct/persistent tick exemptions remain. A miss generates no hitstop. Ground contact still cancels an aerial attack immediately with zero landing recovery, including startup and active frames; do not silently add a frame penalty through the measurement convention.

## On-block outcome record

Create one result row per character, attack variant, tested contact frame/height, defender, and follow-up pair. Expand the groups below into individual records. Required fields: build/rules revision; setup/input replay; stage and positions; remaining HP, charges, cooldowns, meter, and statuses; active frame of contact; landing frame if applicable; both named actions and first legal frames; advantage; follow-up collision frame; spacing; observed outcome; pass/fail/blocked and evidence.

| Contact group | Cases | Attacker/defender action frames | Outcome |
|---|---|---|---|
| Ground string | Each hit; early/late contact; every startup profile; legal cancels | Pending | Pending |
| Aerial string | Each hit; high contact and contact just before landing; fast-fall | Pending | Pending |
| Directional attacks | Ground/air up-attack; late landing down-air; early/late contact | Pending | Pending |
| Specials/projectiles | Blockable variants; distance-dependent arrival; eligible cancels | Pending | Pending |
| Persistent constructs | Single tick and overlapping sources; no-hitstop exemption | Pending | Pending |
| Shatter | Basic final-charge block; special shatter; applicable Story Guard-Crush | Pending | Pending |

No row is currently a measured result. An unimplemented move or stage is blocked coverage, not a passing test. An illustrative frame calculation cannot substitute for this table.

## Escape and commitment scenarios

Run mechanical escape checks with deterministic defender inputs that explore legal timing/directions, independently of CPU difficulty probabilities. A failed CPU defense roll is not evidence of an unavoidable loop; a successful escape in one setup does not establish coverage of the roster. Then separately verify CPU attempts obey the same eligibility and its existing reaction/probability policy.

| Scenario | Required variations | Evidence sought |
|---|---|---|
| Aerial whiff and landing pressure | String and down-air; startup/active/recovery landing; high/low height; fast-fall; close/tip spacing | Actual available punish or reset windows, including cases with no punish; no whiff hitstop |
| Hit-2 escape | Grounded and airborne targets; charges 0/1/full; lockout active/expired; conflicting action locks | Stance only becomes available when legal; identify the actual escape input and frame |
| Lincoln Hit 2 launch (M07) | Every defender weight; full/low HP; each DI direction; tech held/missed; near walls, ledges and ceilings | Whether Lincoln's airborne Hit 2 → Finisher is escapable through DI and tech in place of the grounded escape, and at which HP/weight it becomes a true combo |
| Zero-charge pressure | Repeated nonlaunching hits, shatter daze, and pressure through charge regeneration | Whether a legal defense exists before repetition/KO; no assumed shield access during the baseline eight-second return to a usable charge |
| Repeated launchers | Up-attack/down-air/launching character riders; full/low HP; weights; walls and ceilings | Which DI directions or landing opportunities actually break the sequence |
| Landing tech | Tumble contact with charges 0/full, including shatter lockout; missed and held input | Existing charge-independent 12-frame invulnerable, action-locked recovery; distinguish a genuine ground contact from airborne pressure |
| Knockdown and get-up (M05) | Missed tech on flat ground, near ledges and walls; neutral vs roll get-up each direction; attacker meaty timing; Story mobs under stagger accounting | 30-frame invulnerable knockdown prevents grounded hits; measure whether 10-frame neutral / 14-frame roll get-ups (no invulnerability) are reliably punishable or escapable, and whether the authored `Launches` set is complete |
| Control statuses | Root, Static Charge, their reapplication/overlap with hitstun; Tesla Conductive present/absent | Legal actions under each status; mark duration must not be treated as stun; F07 rules respected |
| Construct-assisted pressure | Each damaging construct; overlaps up to authored caps; melee follow-ups; owner death while objects persist | Effect of cadence, remaining lifetime, destruction opportunities, and resource limits on repetition |
| Story protection | Standard enemies, elites, and flinchable bosses under basic/special/Static pressure | Existing resistance, getup protection, diminishing returns, and stagger budget apply; do not use them as evidence for PvP escape |

Cover every character's relevant attacker kit and both mirror and contrasting defender weights/recovery kits. Sweep attack contact/landing boundaries and record tested ranges. Reproduce issues on open ground, walls, ceilings, platforms, and ledges where authored. Deferred pits/open-stage geometry remains blocked coverage until implemented. Check Fighter baseline first; test relevant Story grid effects separately, including F07's mark extension. Do not import Story Time Freeze, purchased perks, or enemy armor into Fighter results.

Use no optional pickup, ready Ultimate, or spare Echo Step meter as an assumed universal escape. Test resources absent and present where legal. Time Freeze and Echo Step cannot activate during their specified disabling states. Random hazards/orbs should first be disabled to isolate a sequence, then enabled in separate supported-rules scenarios.

## V02 — Controlled stall validation before tuning

User selected **Option A on 2026-09-13**: **diagnose the cause**, then **propose a targeted change for user selection**; **no automatic cooldown reduction**. Keep current combat values, Stock/Time scoring, Overtime and Sudden Death rules. This replaces the old blanket instruction to shorten the Special cooldown band first if the stall gate fails.

### What is being tested

The design intent is that repeatedly avoiding meaningful engagement just to wait for Specials should not be a generally dominant strategy against informed pressure. The original “staller loses more often than not” goal applies to controlled comparisons starting from normal, comparable match states. It is not a rule that every defensive character must lose every matchup.

Do not classify ordinary zoning, baiting, spacing, necessary recovery, forced hitstun, or protecting an earned lead as a failure by themselves. A pressure-role player uses competent, safe approaches, cover, grabs, defense and edge pressure as appropriate; do not script reckless pursuit that gifts hits. An avoidance-role player uses legal movement/defense and waits to exploit available Specials. Both can adapt within their assigned approach. Record the actual behavior shown, not just the role label.

This is a developer playtest protocol, not a new player-facing anti-stall meter, inactivity penalty, movement restriction or forced-engagement rule.

### Conditions and coverage

Run after the three Open stages are playable and their recovery routes are verified, and before Fighter tuning is locked. Missing geometry remains blocked coverage; sealed-stage results cannot stand in for Open stages.

Use similarly skilled, kit-familiar human players. Swap players between avoidance/pressure roles, swap characters and P1/P2 sides, and repeat with both assignments. Include ordinary unrestricted-play controls to distinguish a generally disadvantaged matchup or pilot from a strategy effect. CPU behavior alone does not establish player strategy balance.

Record the game/content revision, current rule versions, stage, character pairing, player/role/side assignment and seed for every run. Use matched seeds/settings for paired comparisons and varied seeds across repeat sets. Keep builds and tuning fixed within a comparison; new changes begin a separately labelled run set.

Cover every roster hero in both roles, including mirrors and contrasting range/recovery/weight matchups. Include the actual Open stages and representative geometry across the Sealed stages; identify untested stages/matchups explicitly. Separate results by:
- **Mode:** Time, timed Stock and untimed Stock where avoidance can prolong play. Use their actual stock/time settings and scoring; do not import a KO-points rule into Time.
- **Geometry:** Open versus Sealed, with stage-specific ledges, platforms, corners and recovery routes retained.
- **Hazards/items:** begin with both off to isolate character/stage interactions, then test supported enabled settings separately. Never pool an off/on difference into one unexplained win rate.
- **Match state:** normal fresh starts for the primary strategy comparison; lead/trailing, depleted-resource and late-regulation scenarios as separate diagnostics. Legitimately defending a lead need not lose to satisfy the gate.

### Evidence and stopping rules

Declare the comparison groups, planned repeated runs and review criteria before collecting results. Include all completed runs and explain invalid/aborted ones. Do not stop after a favorable streak, omit ties or pool away a repeatable stage/matchup problem. If evidence is sparse or inconsistent, mark it **inconclusive** and state the missing coverage rather than inventing a pass.

Record wins/losses/draws, regulation timeouts, Sudden Death entries, match duration and voluntary disengagement time. Annotate waiting/retreat periods separately from forced recovery, hitstun and paused presentations; identify whether they track Special availability. Preserve replay/input evidence or dated observer notes with the classification criteria.

Also record damage sources, meter gain/spending, actual Rally HP reclaimed, normal-versus-Special pressure, construct uptime, edge/ledge patterns and KO cause. Use existing authoritative event/replay data or developer observation where available; this does not require a new public replay/records feature.

Untimed Stock runs ended by a predeclared observation limit are censored/incomplete tests, not regulation timeouts or invented winners. Report nontermination/abort frequency as evidence; many unresolved games cannot be discarded to claim that the avoidance strategy loses most decisive games.

### Overtime check

Keep the last-60-seconds rule and existing cadence/Rally values. Evaluate timed modes before and during Overtime, including **hazards off**: doubled hazard cadence cannot help when hazards are disabled, while stronger Rally can increase healing. Report timeout/Sudden Death rates and time to the next KO alongside damage and reclaimed HP. Control for the different HP, stock and meter states at late entry when interpreting results; a raw before/after comparison alone is not proof of causation.

“Faster endings” is an intention to test, not a guarantee that ties cannot occur. Do not alter F22's separate Sudden Death rules or reapply Overtime there.

### Gate outcome and response

Report **pass for the tested scope**, **problem reproduced**, or **inconclusive/blocked**, with the evidence and exclusions. A pass needs repeated, role-balanced evidence supporting the strategy goal without concealing a consistent failure in a particular setting or matchup. A single aggregate win rate is insufficient; untested launch-critical geometry/behavior prevents a blanket Fighter balance sign-off.

When a problem repeats, isolate its cause: for example stage access/escape loops, safe ranged pressure, cooldown availability, shield interactions, construct coverage, or Rally/meter incentives. Bring back a concrete proposal showing the triggering scenario, measured behavior, smallest relevant adjustment, expected tradeoff and affected tests for the user to select. Shorter cooldowns remain one possible proposal, with no automatic priority or approval. Stage, move or defensive changes likewise require a selected fix.

After an approved adjustment, replay the failure and affected neighboring matchups/settings. Keep unrelated values unchanged. Record the chosen change and tested build in the [design/build ledger](DESIGN_BUILD_DEVIATIONS.md) rather than treating an experiment as the accepted design.

**V02 design status:** protocol specified; players, builds, actual measurements, outcomes and any tuning proposal remain pending. Documentation checks do not establish a stall-balance pass.

## Acceptance and next action

- A finite confirmed combo is allowed. For repeated sequences, record the cycle, resource/lifetime changes, legal defense windows and required inputs, and whether repetition can continue without a defensive opportunity until KO. A KO ending the recording is not itself evidence that a loop is acceptably bounded.
- A suspected unavoidable loop needs a reproducible setup and a sweep of relevant legal defense inputs/timings. Do not call one failed attempt proof. Mark unresolved coverage pending and retain the reproduction.
- Every tested on-block claim must match the measured action pair and contact case. Do not generalize one advantage value to an entire move, all contact heights, or the whole roster.
- Balance sign-off requires completion of the applicable matrix, evidence for reported outcomes, and resolution of reproduced unavoidable repeat-pressure problems. Test results support only their recorded coverage; they do not prove that no undiscovered loop exists.
- If a problem is found, bring back a concrete change for selection: the smallest relevant adjustment to commitment, reach, cancel timing, status interaction, or escape availability. No numerical tuning or new defense mechanic is pre-approved by F09.
- After an approved fix, replay the failure and affected neighboring cases. Avoid changing the responsive landing model without evidence tying the problem to that rule.

Current status: all runtime measurements and gameplay balance validation pending. Documentation wording and cross-references can be checked now; the deferred public Training Mode is not required to record developer simulation/input-replay evidence.
