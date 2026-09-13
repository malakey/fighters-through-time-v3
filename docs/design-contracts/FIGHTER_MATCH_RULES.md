# Fighter match rules — F21

## Mode consolidation — Option A, 2026-09-12

The selectable modes are **Stock** and **Time**. Retire the separate Stock + Time / Hybrid menu choice. F21 is resolved: the user chose to decide Time mode by the fewest stocks lost, with unlimited respawns. Implementation and migration validation are pending.

| Setting / outcome | Stock | Time |
|---|---|---|
| Lives | Limited stocks, default 3, adjustable 1–5 | Unlimited respawns; stock count does not determine elimination or winner |
| Timer | Default 8:00; configurable, including Off | Default 8:00; a valid positive duration is required; Off unavailable |
| Regulation end | Opponent loses all stocks, or enabled timer expires | Timer expires |
| Timer-expiry comparison | Remaining stocks, then remaining HP as a fraction of that fighter's maximum HP | Fewest stocks lost; no HP or attacker-credit tiebreak |
| True tie | Sudden Death; also applies when both final stocks are lost in the same regulation frame | Sudden Death |

Unreclaimed Rally echo is excluded from HP comparisons. Untimed Stock has no timeout or Overtime. The existing last-60-seconds Overtime applies to timed Stock and Time, excluding Sudden Death. V02 tests its effect on match duration and ties, including hazards off and actual Rally healing; faster endings are not guaranteed. See [controlled stall validation](COMBAT_VALIDATION.md#v02--controlled-stall-validation-before-tuning). F22 below defines the next-death decider with retained living HP and the selected hazard setting; the regulation Overtime multiplier does not carry into that phase.

## Time-mode stocks lost — user decision, 2026-09-12

Each player has unlimited respawns during regulation and a nonnegative `stocksLost` counter starting at 0. Add exactly 1 when that player actually loses a stock. Opponent damage, bottom-blast-zone falls, hazards, self-KOs, damage over time and surviving projectiles/constructs all use the same victim-side rule. No attacker credit, ownership window, assist score or additional self-KO penalty is required to determine the winner.

An attack prevented by Defy or invulnerability is not a stock loss. Co-occurring lethal damage and a blast-zone crossing produce one death and one increment, not two. Apply the increment atomically with the authoritative living-to-KO transition, before respawn; duplicate callbacks cannot repeat it. The life/death state, counter, match phase and timer are rollback snapshot/hash state. Resimulation replaces restored state rather than adding to an external counter; HUD and results read the authoritative totals. Respawning never resets the match total. A new match/rematch starts both totals at 0.

Resolve all KOs in a regulation tick before evaluating the winner. In the final active tick (timer moves from one tick remaining to zero), settle damage/falls and both players' losses, then compare totals. A simultaneous KO adds one to each. After regulation ends, no delayed hit, old projectile callback or respawn presentation can change its totals; do not begin another regulation respawn at timeout.

The smaller total wins. For example, P1 losing 2 stocks and P2 losing 5 makes P1 the winner, irrespective of who or what caused those losses. Equal totals, including 0–0, enter the existing Sudden Death flow without comparing HP. Preserve regulation totals for the results display; Sudden Death determines the tied match result under its own rules rather than reopening the regulation loss counter. F22 below defines the transition: living fighters retain current HP; fighters already dead/respawning receive their normal Fighter respawn HP.

Stock mode still decrements its finite pool and uses its existing elimination/timeout rules. Time's stock-loss total is match-local state, separate from StockCount settings, Story rewinds and global career wins/losses.

## Settings and presentation

Use the same two choices and shared validation in Fighter lobbies, the Holodeck CPU panel, house-rule persistence, rematch and new-stage flows. Label the second choice Time while retaining the internal TimeLimit identifier for compatibility.

Stock exposes stock count and timer controls. Time disables/hides stock count as a gameplay rule and never decrements a finite life pool; a stored stock preference may remain for returning to Stock. Time's HUD replaces finite stock icons with a localized **Stocks lost: N** label under each player's HP bar. Initialize both counters to 0, increase only on actual stock loss and show the final totals in the existing simple results view; lower is better. Do not label it KOs scored, points or remaining lives.

In the authored MatchSettings schema, TimeLimit is seconds: 0 means Off in Stock only; a positive finite supported duration means timed play. Validate timer input against the same duration options across menus and match launch. Switching from untimed Stock to Time selects the default 480 seconds and shows it before launch. Do not carry Off into Time. Keep selected characters, stage, items and hazards unchanged when switching mode.

## Compatibility and migration

The documented enum previously assigned Stock = 0, TimeLimit = 1 and Hybrid = 2 implicitly. Make Stock = 0 and TimeLimit = 1 explicit; reserve 2 for legacy decoding, never reuse it or list it as a third playable mode. Verify actual shipped serialization/schema versions before implementing a converter; do not assume all saved formats match this design example.

On reading a recognized legacy Hybrid value or label, normalize to Stock with the same valid positive timer and stock count, preserving all other house rules. A legacy Hybrid timer that is Off, missing, nonfinite or otherwise invalid becomes the default 480 seconds, with the repaired value visible in the settings summary before launch. Validate an invalid/missing stock count using the existing 1–5 range and default 3; keep valid values. Do not turn a valid timed Hybrid into untimed Stock.

Existing valid Stock settings, including timer Off, and valid Time settings retain their meanings. Handle a known older Off sentinel in its versioned decoder before normalization. Unknown mode values are not Hybrid: preserve the source settings and require a valid selection before match launch rather than guessing a winner rule.

Run this idempotent normalization before displaying loaded settings or creating the next match. Persist the normalized mode and settings together through the existing save writer; do not change a running match. For future network sessions, negotiate compatible rules versions and hash the normalized rules; mixed semantics must fail the existing content/version check rather than interpreting enum 2 differently.

## Sudden Death — F22

User amendment, 2026-09-12: respect the selected hazard setting, keep living players at their current HP, and make the next actual death the loss. The old 1-HP / first-hit rule is retired. Other fresh-round reset rules from the selected Option A remain.

**Dead-entry resolution — Option A (2026-09-12):** if a fighter is already dead or in the normal respawn sequence when regulation ties, complete that fighter's HP restoration using the normal Fighter respawn rule. Living fighters retain their current positive HP. Resolve eligibility from the committed end-of-regulation life state, not presentation timing. If normal respawn HP has already been restored, use that resolved value without granting a second restoration. Do not add a stock loss, modify the frozen regulation total, or treat the regulation death as a Sudden Death loss. Both players then use the common reset/countdown below; this HP exception grants no respawn platform or spawn-invulnerability period. F22's design choices are settled; implementation and validation remain pending.

### Transition state

Capture after resolving the entire final regulation tick. Enter a separate SuddenDeath phase and freeze its transition while both combatants are prepared.

| State | Sudden Death entry rule |
|---|---|
| Living fighter HP | Preserve exact current positive HP, including unequal HP in a Time-mode tie. No heal, normalization or clamp to 1. |
| Dead/respawning fighter HP | Normal Fighter respawn HP, resolved once under Option A above. Applies independently to either or both fighters; no restoration to a living opponent, extra stock loss or new spawn protection. |
| Position and motion | Place both at the authored match spawn points; zero velocity and release ledge/grab attachments. Existing unsafe/spawn-overlap geometry is a validation failure. |
| Action state | Cancel attacks, grabs, throws, hitstun, daze, hitstop and armed Echo Step; begin neutral with empty input buffers. No queued regulation attack fires after the restart. |
| Meter and cooldowns | Set meter to 0; both Special and movement cooldowns and Echo Step cooldown ready. No ongoing Ultimate or pending activation. |
| Block | Refill the baseline charge capacity; clear shatter lockout, shieldstun and regeneration countdown. |
| Movement resources | Reset jumps and ledge regrab state to a fresh grounded spawn. Initialize a new Echo Step history generation at the spawn with one valid sample; storage filled with that coordinate is not fabricated history. Require 30 subsequent simulation ticks before use; never teleport into regulation history. See [temporal contract](TEMPORAL_STATE_CONTRACT.md). |
| Status and item effects | Clear both status slots, marks, Root/tethers, timed orb buffs and Aegis; no carried regulation effect can damage or protect either fighter. |
| Rally and Desperation | Clear the old echo pool and pending echo-meter accounting. New combat uses ordinary Rally/Desperation rules at retained HP, with no Overtime multiplier. Legal healing from the core kit is not disabled by this choice. |
| Defy History | Disabled throughout the phase, regardless of meter. Preserve its spent flag; the HUD seal shows unavailable. Do not consume a new use merely on entry. |
| Projectiles and constructs | Remove every regulation projectile, attack zone, summon, construct and temporary ability platform, including objects whose owner died. Clear pending damage/events associated with them. |
| Arena and hazards | Restore the arena's authored round-start geometry and mechanism state. Respect the match's hazard toggle. If Off, no hazard warnings or damage. If On, start the first full warning after play resumes; use twice-normal cadence by halving idle/recovery durations while retaining warning and active durations. Do not multiply again by regulation Overtime. |
| Orbs | Remove existing orbs; disable spawning and collection throughout Sudden Death. |
| Respawn protection | Clear prior respawn platforms and invulnerability, plus old attack/roll invulnerability. The common ready countdown freezes combat for both players; once play resumes, no new spawn-invulnerability period is granted. Ordinary abilities retain their authored protection. |
| Match state | No timer or further stock pool; one decisive life each. Freeze regulation stocks-lost totals for results. Reset CPU action plans and opportunity tracking for the new phase without changing difficulty; continue the match's deterministic PRNG state. |

Use the common match-start ready countdown, showing **Sudden Death — next death loses** and the retained HP bars. Controls and simulation clocks remain frozen during the transition/countdown; resume both players together. This is a fresh combat-state setup with retained living HP, not continuous attacks across the timeout boundary.

### Ending and verification

After each active tick, resolve all real deaths before choosing the result. One dead fighter loses; two dying in the same tick produce the existing Draw. Damage, falls, hazards and self-KOs are equivalent. A nonlethal hit, legal block or invulnerable contact does not finish the match. Do not respawn a Sudden Death victim or increment frozen regulation stocks-lost totals.

Commit the phase transition and resulting match state together in rollback snapshots/hashes. S01's [state and replay contract](ROLLBACK_STATE_CONTRACT.md) includes phase/life generations, frozen regulation totals, pending cleanup/HP grants, continuing PRNG state and result identity; restore them atomically. Recompute superseded gameplay events on the corrected branch, while durable results wait for confirmation and commit once. Duplicate callbacks cannot transition twice, restore HP twice or report multiple results. Discard late regulation events by phase identity; only a death after the decider begins determines its outcome.

Validate unequal living HP (for example 20 versus 70), low HP, a Time tie at 0–0 losses, hazards Off and On, warning duration, no stacked acceleration, pending projectiles/constructs/statuses, Rally and core-kit healing, full/used Defy, old respawn invulnerability, simultaneous deaths, rollback across entry, and exactly-once result logging. Also test a living 20-HP fighter versus a dead opponent, both dead on their final Stock-mode lives, a death on Time's final tick, and a normal respawn whose HP restoration already completed. Verify normal respawn HP only for the eligible fighter, exactly-once restoration, unchanged regulation totals and no additional spawn protection. Implementation and all runtime checks remain pending.

## Required checks

Verify fresh defaults, Stock with timer on/off, Time with unlimited respawns, invalid Time/Off input, and mode switching in both lobby surfaces. Exercise legacy Hybrid with valid custom settings and each timer repair case; confirm unchanged items/hazards/characters/stage, stable enum values, atomic persistence and no second migration change on reload.

Check rematch/new-stage settings retention, Stock elimination and HP-percentage timeout comparison, simultaneous final-stock ties, Time timeout ties and Overtime only during timed regulation. Also verify Time counters at 0–0 and unequal totals; every KO cause; simultaneous and duplicate KO callbacks; prevented lethal hits; final-tick losses; post-timeout callbacks; respawn retention; rematch reset; and rollback restoration without duplicate increments. Confirm HUD/result labels show stocks lost and require no attacker attribution. These are pending runtime and migration checks; documentation review does not prove their implementation.
