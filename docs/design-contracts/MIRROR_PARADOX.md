# Mirror Paradox campaign AI — F20, Option A

> **Fiction (story review S45, 2026-09-29):** the Mirror Paradox is the Void's reflection — rift-space throws the hero's own resonance back as an echo. No one built it.

Decision: 2026-09-12. Mirror Paradox remains the mandatory Level 13 mirror duel with one phase and the active character's full core kit. Its reactions and defense now scale with Story difficulty. This replaces the blanket Hard-CPU assignment. Implementation and encounter validation are pending.

## Campaign profile

Select this boss profile from the active Story difficulty, independently of the saved Holodeck CPU setting. Reuse the CPU utility engine with explicit boss overrides; do not load a complete practice-CPU preset and accidentally disable the boss's signature abilities.

| Parameter | Easy Story | Normal Story | Hard Story |
|---|---|---|---|
| Reaction delay | 30–45 frames | 15–20 frames | 4–8 frames |
| Block attempt rate | 10% | 40% | 80% |
| Eligible block-through-hitstun attempt, once per instance | 10% | 45% | 85% |
| Eligible launching-hit DI attempt | None | 40% | 80% |
| Core kit available | Basics, directional attacks, both Specials, movement and Ultimate | Same | Same |
| Attack selection | Simple sequences, limited follow-ups; use suitable Specials rather than only basics | Intermediate spacing and follow-ups | Strongest tactical selection and legal sequences |
| Ultimate choice | First legal opportunity at full meter | First legal opportunity at full meter | Seek a useful confirm/setup under the existing Hard policy |
| Grab | Neither | Occasional, under F19 Medium rules | Tactical, under F19 Hard rules |
| Defy History (2026-10-31) | Never | At 50+ Influence, a 35 % roll once per hitstun instance after a 3-hit string | At 50+ Influence, without a roll, on a 2-hit string (the hero has no Ultimate wind-up and Story has no walls, so only the string trigger applies) |
| Mirrored purchased grid perks | None | None | Player's purchased/unlocked perks only |
| HP from 1,000 boss base HP | 700 | 1,000 | 1,500 |
| Outgoing campaign damage multiplier | 0.5× | 1.0× | 1.5× |

Rates are attempt probabilities, not automatic blocks, escape guarantees or added immunity. Apply ordinary eligibility and shared charge/lockout rules; if the clone's current state cannot legally perform a defense, the probability cannot make it legal. Use F19's seeded decisions and delayed opponent observations; Hard has no zero-delay input-reading exception. The boss override enables Specials, movement and Ultimate even on Easy; it does not enable Easy grabs.

The existing campaign HP/damage multipliers apply once. The listed HP values are the difficulty-scaled boss baseline; any applicable copied Hard perk uses its existing modifier rules once, without copying the player's current HP or stacking a second character base-HP pool. Preserve the existing one-phase encounter and knockback setting. Boss-table generic pattern metadata must not override this explicit CPU profile; resource-to-controller wiring needs verification.

## Mirror identity and boundaries

Always mirror the chosen character and equipped ability VFX. Core-kit access is independent of Story ability locks at encounter time. On Hard, initialize the clone's copied perk set from the player's purchased/unlocked grid nodes when the boss encounter is constructed; do not grant every possible node or copy the player's wallet, active cooldowns, spent flags or attempt resources. Use the current F06–F08/F15 perk definitions. Rebuilding the encounter follows normal checkpoint reconstruction; do not dynamically copy player resource changes during combat.

Use character-specific ability roles and recovery from [CPU recovery](CPU_RECOVERY.md). Offensive Specials must fit the actual kit; a slot number alone does not establish projectile, anti-air or mobility behavior. The full kit is available, but legal Momentum pips and reuse delays, meter costs, cast restrictions and telegraphs still govern each use.

**Combat and movement update (2026-10-16).** The clone's activation strike shows the wind-up telegraph (user decision 2.1: **"Both modes now: red flash + the Unblockable X glyph on the 20-frame wind-up, in Fighter and on the Level 13 Mirror; a critical-cue row ready for audio."**): a steady Unblockable-red tint, never warmed by a Hard clone's hero aura, and the Unblockable X above the clone for exactly the wind-up, with the Critical wind-up cue. Leaving the Ultimate by any edge (a stun, a death, a grab) clears it, and the hero's own Story Ultimate never draws it. It is built steady rather than flashing, so Reduced Temporal Effects needs no variant of it; whether that matches the ruling's "red flash" is open for the user, as is whether a Hard clone should wear the hero aura at all. The clone's post-contact Story Ultimate is armoured through its startup, as the hero's is (decision 2.4, plan question Q2). The CPU observes the clone's executing Special through its phases (decision 9.3), so it holds Special and Ultimate presses until the cast ends. The clone holds Jump for a full hop (decision 1.1(5)). The clone is a Story body, so it follows the Story hero's tumble rule (decision 3.2: after a launch's hitstun it may act out of the tumble, and a passive landing techs or knocks down) and the Story block rules (the perfect block and the damage-scaled shieldstun). Echo Step, which it once paid for at the full 30 meter, was removed from both modes on 2026-10-25.

The [CPU combat policy](CPU_COMBAT_POLICY.md) supplies grab eligibility and opportunity sampling. Its Ultimate priority also applies to Easy's boss override. The clone uses Defy History by its band (table above); it pays the Story cost of 50 Influence unscaled and reserves no meter for it. Borrowing the AI still grants no player-only systems such as Time Freeze or death rewinds.

The encounter remains Story: retain existing boss immunities and Enemy Stagger Discipline, and respect Time Freeze, Story death recovery and Act III rules. Do not add or remove an immunity merely to match a Fighter preset. The clone does not receive the player's Time Freeze or death rewinds. Perk mirroring does not change normalized Fighter matches.

## Required validation

Exercise every roster mirror on Easy, Normal and Hard with baseline and representative affordable Level 13 builds. Verify that changing Holodeck CPU difficulty cannot change this encounter. Check reaction ranges, defense attempt rates and legality, full-kit use on Easy, Normal's occasional verbs, Hard-only copied perks and single application of stat modifiers.

Measure fight duration, player damage taken, recovery/anchor use, repeatable pressure and reachable punish opportunities. Test Time Freeze, interrupted casts, shatter lockout, checkpoint reconstruction and all supported cloned perk effects. Since 2026-10-16 also check the wind-up telegraph at the Story camera's zoom (the clone can start off screen), the clone's full-height jumps, its Ultimate startup armour, its tumble landings and presses during its own Special cast. Confirm one-phase completion and the existing once-only physical dust reward. These are pending runtime checks, not evidence that 700/1,000/1,500 HP or the selected behavior rates are already balanced.

**Feel and UI update (2026-10-25).** Echo Step was removed from both modes, so the clone has no such verb and the CPU engine it borrows no longer decides one. The clone follows the Story rules unchanged: its finisher, wall behaviour and knockback are Story's, not the Fighter knockback feel of the same update.

**Meter and Defy update (2026-10-31).** The clone has its own Momentum gauge (2 pips at the start; its Special rungs read the same pip-and-reuse-delay predicate as the Fighter CPU) and a Defy by its band (table above). Its gains and costs are not scaled by the Story hero's ×0.5 (it is not the campaign hero). The hero's Defy blast staggers the clone, and the clone's staggers the hero, as an opposing fighter would be (a Defier's invulnerable startup frames make either blast whiff on the other). The clone's CPU sees its own string through a projected hitstun, because a Story body stays in one continuous stun through a chain.
