# Combat validation — F09 Option A

Design decision: 2026-09-12. Keep current combat rules and validate them before choosing balance changes. This is a specification and pending test record, not a report of runtime results.

Authority: [main design, Combat Mechanics](../design-godot-v7.md). Existing shared frame data, character profiles, cancel rules, action locks, and mode restrictions remain authoritative. Preserve zero landing lag, zero jump squat and the existing shatter rules. Shieldstun now scales with the blocked damage, `6 + damage ÷ 4` frames within 6–16, in both modes (user decisions 4.1 and 11.2, 2026-10-16), replacing the eight-frame basic shieldstun; a perfect block (decision 3.4) sets none. This decision adds neither a landing-recovery penalty nor a universal combo breaker. Story Enemy Stagger Discipline stays separate from Fighter Mode.

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
| Directional attacks | Ground/air up-attack; late landing down-air; down-air slam spike offstage, forced ground bounce on floor and one-way platforms (A12); early/late contact | Pending | Pending |
| Specials/projectiles | Blockable variants; distance-dependent arrival; eligible cancels; each Special's authored startup, active and recovery in both modes (decision 1.2, 2026-10-16), a whiff's full recovery with the Block cancel from recovery frame 0, and the landed-hit recovery cancel (D3) in both modes (decision 11.1) | Pending | Pending |
| Persistent constructs | Single tick and overlapping sources; no-hitstop exemption | Pending | Pending |
| Shatter | Basic final-charge block; 2-charge Special at 3 and at 1–2 charges; Shield-Breaker shatter (A01) after its authored startup (decision 1.3), inside the perfect-block window too (Q3); applicable Story Guard-Crush | Pending | Pending |
| Ultimate activation (A02) | Fighter activation strike: roll-through, jump-over and out-of-range whiffs; whiff-recovery punish; Aegis/barrier-absorbed contact | Pending | Pending |
| Air dodge (A03) | Invulnerable-frame coverage vs aerial strings and up-attack; once-per-airtime refresh on landing, a ledge grab and being hit (decision 5.3); late-dodge grounded recovery punish | Pending | Pending |
| Grab reach (A13) | Grab connects at pushbox contact for every character pair, including Lincoln and Cleopatra hurtbox overrides; maximum 1.2-unit pivot distance | Pending | Pending |
| Perfect block (3.4) | Contact 0–4 and 5 frames after the stance goes live, for basics, ordinary Specials, Shield-Breakers, a Story Guard-Crush and hazard ticks; a held raise, a hit-2 escape raise, re-raises after 19 and 20 lowered frames; both modes | Pending | Pending |

No row is currently a measured result. An unimplemented move or stage is blocked coverage, not a passing test. An illustrative frame calculation cannot substitute for this table.

**Package 12 W3b (2026-09-27): baseline captures only.** The two M05/M07 scenarios below now have deterministic flat-ground captures in `tests/Determinism/FighterKnockdownTests.cs` (and the Story mirror in `tests/Unit/StoryKnockdownTests.cs`): Lincoln's hit 2 launching a full-HP Joan, DI away versus no DI (angle bends, magnitude unchanged) and a held-Block tech; and a missed tech's 30-frame invulnerable knockdown followed by the automatic 10-frame neutral and the 14-frame roll get-up in each direction, both without invulnerability. They prove the mechanics exist and replay bit-identically; they are **not** the required variations (weights, low HP, walls, ledges, ceilings, meaty timing, Story mobs), so both rows stay pending.

## Escape and commitment scenarios

Run mechanical escape checks with deterministic defender inputs that explore legal timing/directions, independently of CPU difficulty probabilities. A failed CPU defense roll is not evidence of an unavoidable loop; a successful escape in one setup does not establish coverage of the roster. Then separately verify CPU attempts obey the same eligibility and its existing reaction/probability policy.

| Scenario | Required variations | Evidence sought |
|---|---|---|
| Aerial whiff and landing pressure | String and down-air; startup/active/recovery landing; high/low height; fast-fall; close/tip spacing | Actual available punish or reset windows, including cases with no punish; no whiff hitstop |
| Hit-2 escape | Grounded and airborne targets; charges 0/1/full; lockout active/expired; conflicting action locks | Stance only becomes available when legal; identify the actual escape input and frame |
| Lincoln Hit 2 launch (M07) | Every defender weight; full/low HP; each DI direction; tech held/missed; near walls, ledges and ceilings | Whether Lincoln's airborne Hit 2 → Finisher is escapable through DI and tech in place of the grounded escape, and at which HP/weight it becomes a true combo |
| Zero-charge pressure | Repeated nonlaunching hits, shatter daze, and pressure through charge regeneration | Whether a legal defense exists before repetition/KO; no assumed shield access during the baseline eight-second return to a usable charge |
| Repeated launchers | Up-attack/down-air/launching character riders; full/low HP; weights; walls and ceilings | Which DI directions or landing opportunities actually break the sequence |
| Landing tech | Tumble contact with charges 0/full, including shatter lockout; missed and held input; a tumble that outlives its hitstun (decision 3.2, 2026-10-16), landed passively and left by an exit action (jump, air dodge, attack); a non-launching hit on a tumbling victim | Existing charge-independent 12-frame invulnerable, action-locked recovery; distinguish a genuine ground contact from airborne pressure; a passive post-hitstun landing still techs or knocks down and an exit action lands plainly; a non-launching hit ends the tumble, so its victim lands in that hit's hitstun with no tech or knockdown, and whether that opens a juggle (F09 No Juggling) |
| Knockdown and get-up (M05) | Missed tech on flat ground, near ledges and walls, including a passive post-hitstun tumble landing (3.2); neutral vs roll get-up each direction; attacker meaty timing; presses on the landing tick and on a hitstop thaw tick inside the knockdown; Story mobs under stagger accounting | 30-frame invulnerable knockdown prevents grounded hits; measure whether 10-frame neutral / 14-frame roll get-ups (no invulnerability) are reliably punishable or escapable, and whether the authored `Launches` set is complete |
| Ledge get-ups (5.2) | Open-stage ledges; an airtime's first grab and a regrab; the neutral climb, ledge roll, ledge attack and climb jump at their earliest legal frame and late; an edge-guarding attacker | Whether the first grab's 30 intangible frames (ending when the hang ends) or any get-up makes ledge play unpunishable; each option's vulnerable frames |
| Wall pin (3.3) | Launchers into each Sealed stage's walls and corners; every DI direction; tech held and missed | Whether a launched fighter can be re-hit against a wall with no legal answer; kept by user decision 3.3 and judged by hand in the pad session (decision 9.4) |
| Control statuses | Root, Static Charge, their reapplication/overlap with hitstun; Tesla Conductive present/absent | Legal actions under each status; mark duration must not be treated as stun; F07 rules respected |
| Construct-assisted pressure | Each damaging construct; overlaps up to authored caps; melee follow-ups; Story owner death while objects persist; Fighter stock-loss despawn on the same tick (A04) | Effect of cadence, remaining lifetime, destruction opportunities, and resource limits on repetition |
| Story protection | Standard enemies, elites, and flinchable bosses under basic/special/Static pressure | Existing resistance, getup protection, diminishing returns, and stagger budget apply; do not use them as evidence for PvP escape |

Cover every character's relevant attacker kit and both mirror and contrasting defender weights/recovery kits. Sweep attack contact/landing boundaries and record tested ranges. Reproduce issues on open ground, walls, ceilings, platforms, and ledges where authored. Deferred pits/open-stage geometry remains blocked coverage until implemented. Check Fighter baseline first; test relevant Story grid effects separately, including F07's mark extension. Do not import Story Time Freeze, purchased perks, or enemy armor into Fighter results.

Use no optional pickup, ready Ultimate or spare meter as an assumed universal escape. Test resources absent and present where legal. Time Freeze cannot activate during its specified disabling states. Random hazards/orbs should first be disabled to isolate a sequence, then enabled in separate supported-rules scenarios.

## V02 — Controlled stall validation before tuning

User selected **Option A on 2026-09-13**: **diagnose the cause**, then **propose a targeted change for user selection**; **no automatic cooldown reduction or faster Momentum regeneration**. Keep current combat values, Stock/Time scoring, Overtime and Sudden Death rules. This replaces the old blanket instruction to shorten the Special cooldown band first if the stall gate fails.

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

Also record damage sources, meter and Momentum gain/spending, actual Rally HP reclaimed, normal-versus-Special pressure, construct uptime, edge/ledge patterns and KO cause. Use existing authoritative event/replay data or developer observation where available; this does not require a new public replay/records feature.

Untimed Stock runs ended by a predeclared observation limit are censored/incomplete tests, not regulation timeouts or invented winners. Report nontermination/abort frequency as evidence; many unresolved games cannot be discarded to claim that the avoidance strategy loses most decisive games.

### Overtime check

Keep the last-60-seconds rule and existing cadence/Rally values. Evaluate timed modes before and during Overtime, including **hazards off**: doubled hazard cadence cannot help when hazards are disabled, while stronger Rally can increase healing. Report timeout/Sudden Death rates and time to the next KO alongside damage and reclaimed HP. Control for the different HP, stock and meter states at late entry when interpreting results; a raw before/after comparison alone is not proof of causation.

“Faster endings” is an intention to test, not a guarantee that ties cannot occur. Do not alter F22's separate Sudden Death rules or reapply Overtime there.

### Gate outcome and response

Report **pass for the tested scope**, **problem reproduced**, or **inconclusive/blocked**, with the evidence and exclusions. A pass needs repeated, role-balanced evidence supporting the strategy goal without concealing a consistent failure in a particular setting or matchup. A single aggregate win rate is insufficient; untested launch-critical geometry/behavior prevents a blanket Fighter balance sign-off.

When a problem repeats, isolate its cause: for example stage access/escape loops, safe ranged pressure, Special availability (the Momentum gauge and reuse delays), shield interactions, construct coverage, or Rally/meter incentives. Bring back a concrete proposal showing the triggering scenario, measured behavior, smallest relevant adjustment, expected tradeoff and affected tests for the user to select. Faster Momentum regeneration or a cheaper Special remains one possible proposal, with no automatic priority or approval. Stage, move or defensive changes likewise require a selected fix.

After an approved adjustment, replay the failure and affected neighboring matchups/settings. Keep unrelated values unchanged. Record the chosen change and tested build in the [design/build ledger](DESIGN_BUILD_DEVIATIONS.md) rather than treating an experiment as the accepted design.

**V02 design status:** protocol specified; players, builds, actual measurements, outcomes and any tuning proposal remain pending. Documentation checks do not establish a stall-balance pass.

## Acceptance and next action

- A finite confirmed combo is allowed. For repeated sequences, record the cycle, resource/lifetime changes, legal defense windows and required inputs, and whether repetition can continue without a defensive opportunity until KO. A KO ending the recording is not itself evidence that a loop is acceptably bounded.
- A suspected unavoidable loop needs a reproducible setup and a sweep of relevant legal defense inputs/timings. Do not call one failed attempt proof. Mark unresolved coverage pending and retain the reproduction.
- Every tested on-block claim must match the measured action pair and contact case. Do not generalize one advantage value to an entire move, all contact heights, or the whole roster.
- Balance sign-off requires completion of the applicable matrix, evidence for reported outcomes, and resolution of reproduced unavoidable repeat-pressure problems. Test results support only their recorded coverage; they do not prove that no undiscovered loop exists.
- If a problem is found, bring back a concrete change for selection: the smallest relevant adjustment to commitment, reach, cancel timing, status interaction, or escape availability. No numerical tuning or new defense mechanic is pre-approved by F09.
- After an approved fix, replay the failure and affected neighboring cases. Avoid changing the responsive landing model without evidence tying the problem to that rule.
- **Combat and movement update (2026-10-16).** Headless suites pin the mechanics it added (Fighter Special phases and Shield-Breaker timing, D3 in both modes, the perfect block and the shieldstun table, tumble after hitstun, the air-dodge refresh, ledge get-ups, the sim short hop and lip pop, and respawn protection ending on offence) and replay them bit-identically under rollback. They prove the mechanisms exist, not any outcome in the tables above. User decision 9.4 (**"Pad session only: no frame tables, combo enumerator or CPU-vs-CPU batch."**) makes that update plan's two-player pad-session checklist the first evidence for its rows, Ultimate damage, wall pinning, 1v1 pace and hitstop included; until it runs, every row stays Pending.

Current status: all runtime measurements and gameplay balance validation pending. Documentation wording and cross-references can be checked now; the deferred public Training Mode is not required to record developer simulation/input-replay evidence.

**Feel and UI update (2026-10-25).** Echo Step scenarios are retired with the verb. Fighter Mode knockback feel adds validations (Fighter only; Story's finisher carry and mob wall bounce are separate and unchanged): the finisher's horizontal launch speed is twice its vertical one at the same airtime (travel about 2.0–2.1x on every kit that does not reach a wall); a launched fighter clamped by a side wall at 5 u/s or more rebounds at half speed once per launch with a 6-frame freeze, no damage and no meter change, a new launch re-arms it, and grounded slides never bounce; a grounded non-launching hit shoves 0.2–0.4 u and is planted for the rest of the stun while launches, airborne non-launchers and the guard-break push keep their motion; and a depth-7 rollback replays a bounce hash-identically. The block-success effect is presentation only and has no validation scenario beyond being shown once per successful block. All numbers are provisional.

**Meter and Defy update (2026-10-31).** Headless suites and a bot batch pin the Momentum gauge (the four-pip cap, the 2-pip start, regeneration and landed-swing gains, the 2 s reuse delay, the kept lockouts, the empty-gauge flash, stock loss and Sudden Death, rollback replay) and the active Defy (legality and refusals from every state, the six invulnerable frames, the blast's push, stagger and freeze on each target class, the cancels, the heal, the Ultimate wind-up beat, two simultaneous Defies, the CPU and Mirror triggers and the presentation counter's rollback safety). They prove the mechanisms exist, not any outcome in the tables above: whether a Defy is noticed, whether Hard CPUs starve their own Ultimates, how often the `play` bot's conservative policy fires it, and the feel of the gauge are judged in the two-player pad session, which stays the first evidence for these rows. The bot batches from before and after the change do not compare.
