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
| Grab / Echo Step | Neither | Occasional, under F19 Medium rules | Tactical, under F19 Hard rules |
| Mirrored purchased grid perks | None | None | Player's purchased/unlocked perks only |
| HP from 1,000 boss base HP | 700 | 1,000 | 1,500 |
| Outgoing campaign damage multiplier | 0.5× | 1.0× | 1.5× |

Rates are attempt probabilities, not automatic blocks, escape guarantees or added immunity. Apply ordinary eligibility and shared charge/lockout rules; if the clone's current state cannot legally perform a defense, the probability cannot make it legal. Use F19's seeded decisions and delayed opponent observations; Hard has no zero-delay input-reading exception. The boss override enables Specials, movement and Ultimate even on Easy; it does not enable Easy grabs or Echo Step.

The existing campaign HP/damage multipliers apply once. The listed HP values are the difficulty-scaled boss baseline; any applicable copied Hard perk uses its existing modifier rules once, without copying the player's current HP or stacking a second character base-HP pool. Preserve the existing one-phase encounter and knockback setting. Boss-table generic pattern metadata must not override this explicit CPU profile; resource-to-controller wiring needs verification.

## Mirror identity and boundaries

Always mirror the chosen character and equipped ability VFX. Core-kit access is independent of Story ability locks at encounter time. On Hard, initialize the clone's copied perk set from the player's purchased/unlocked grid nodes when the boss encounter is constructed; do not grant every possible node or copy the player's wallet, active cooldowns, spent flags or attempt resources. Use the current F06–F08/F15 perk definitions. Rebuilding the encounter follows normal checkpoint reconstruction; do not dynamically copy player resource changes during combat.

Use character-specific ability roles and recovery from [CPU recovery](CPU_RECOVERY.md). Offensive Specials must fit the actual kit; a slot number alone does not establish projectile, anti-air or mobility behavior. The full kit is available, but legal cooldowns, meter costs, cast restrictions and telegraphs still govern each use.

The [CPU combat policy](CPU_COMBAT_POLICY.md) supplies grab/Echo Step eligibility, opportunity sampling, destination checks and meter ordering. Its Ultimate priority also applies to Easy's boss override. Only assign Defy reserve value if the clone has an explicitly supported, unused and eligible Defy mechanic; borrowing the AI does not itself grant player survival systems.

The encounter remains Story: retain existing boss immunities and Enemy Stagger Discipline, and respect Time Freeze, Story death recovery and Act III rules. Do not add or remove an immunity merely to match a Fighter preset. The clone does not receive the player's Time Freeze or death rewinds. Perk mirroring does not change normalized Fighter matches.

## Required validation

Exercise every roster mirror on Easy, Normal and Hard with baseline and representative affordable Level 13 builds. Verify that changing Holodeck CPU difficulty cannot change this encounter. Check reaction ranges, defense attempt rates and legality, full-kit use on Easy, Normal's occasional verbs, Hard-only copied perks and single application of stat modifiers.

Measure fight duration, player damage taken, recovery/anchor use, repeatable pressure and reachable punish opportunities. Test Time Freeze, interrupted casts, shatter lockout, checkpoint reconstruction and all supported cloned perk effects. Confirm one-phase completion and the existing once-only physical dust reward. These are pending runtime checks, not evidence that 700/1,000/1,500 HP or the selected behavior rates are already balanced.
