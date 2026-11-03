# GR-TRAITS handoff (gameplay review 2026-11-03, rulings 2b and 2b+; plan §1.12)

**Nobody has played this.** Every claim below is a headless suite or a bot run. Not one person has seen the gold glow, the diamond telegraphs, a counter or a rising strike.

## What shipped

Three soft-counter traits on Standard mobs, read from the new `EnemyData.Trait`.

| Trait | Carrier | Rule as built | Answer |
|---|---|---|---|
| Armoured (1) | `shock_shield_legionnaire` (its `FrontalDamageReduction` is now 0.0) | ×0.5 on Basic and Special hits, inside the one intake rounding, until `LaunchThrown`. Gold hyper-armour glow while intact. | Throw |
| Catcher (2) | `cyber_guard` | `combo_1` and `combo_2` on a mob that is not stunned deal damage with no stun, knockback, launch, poise charge or Static Charge lock. `combo_2`, off cooldown and with no attack in flight, commits a counter. | Throw or Special |
| Anti-air (3) | `plasma_spear_ward` | Hero `Airborne` or an aerial swing, off the floor, descending, in the trigger band, with no Environment between them: a rising strike. A miss costs 40 frames of recovery. | Grounded pressure |

Unreduced by the armour: Ultimates (by class or origin), throw damage, bowling, the wall impact (reckoned from the intake before the armour), status ticks, and hazard deliveries. GR-HAZ's impact goes through the private `TakeDamage`, so it passes whole too.

## API

- `FTT.Combat.EnemyTrait { None, Armoured, Catcher, AntiAir }`, `EnemyData.Trait`.
- `FTT.Combat.EnemyTraitRules` (pure): `ArmouredDamageScale`, `ArmourReduces`, `ArmouredIntakeScale`, `HeroIsJumpingIn`, `AntiAirInReach`, `AntiAirFires`, and the `AntiAir*` constants.
- `FTT.Combat.EnemyTraitTally` (static, observation only; a playtest is one run per process, so it has no reset).
- `StoryEnemyCombatRules`, new final region: `CatcherCounter*` constants, `CatcherCatchesHit`, `CatcherCounterFires`.
- `EnemyController.Traits.cs`: `ActiveTrait`, `IsTraitArmourIntact`, `IsArmourGlowShowing`, `TraitCooldownFramesRemaining`, `TelegraphGlyphNode`, `CatcherCounterAbility`, `AntiAirStrikeAbility`; private `ResetTraitState`, `StripTraitArmour`, `TraitIntakeScale`, `ApplyTraitIntake`, `TraitUnreducedIntake`, `CatchStringHit`, `AnswerCaughtHit`, `TickTraits`, `AntiAirTriggered`, `CommitTraitStrike`, `NoteTraitWindowEnded`.
- Bot report: a new top-level `enemy_traits` block (`armour_strips`, `armour_damage_prevented`, `catcher_catches`, `catcher_counters`, `catcher_counters_connected`, `anti_air_strikes`, `anti_air_strikes_connected`). `compare_runs.py` does not print it yet.

## Provisional numbers (`PROVISIONAL-ENEMY-TRAIT-RULES`)

Stated by the plan: ×0.5, the 12-frame counter telegraph, the 150-frame counter cooldown, the 40-frame miss recovery. Everything else is mine:

- **Counter:** active 8, recovery 22, hitstun 0.3 s, damage `AttackDamage`, knockback (`AttackKnockback`, −1.5), box 96 × 56 at (52, −30), which reaches a hero 120 px away.
- **Rising strike:** telegraph 10, active 8, hit recovery 16, cooldown 150, hitstun 0.3 s, damage `AttackDamage`, knockback (`AttackKnockback`, −3), box 110 × 130 at (30, −135). Its lower edge is 70 px up, so a hero standing on the mob's floor is under it.
- **Trigger:** |dx| ≤ 110 px, the hero's feet 130–280 px over the mob's feet. The lower limit is what the 10-frame telegraph needs against the 12 u/s fall cap.
- **Content sweep:** 180 px workable from the floor, 320 px mob cover.

## Tests: declared +23, actual +23

New: `EnemyTraitRulesTests` 8 (pure C#), `EnemyControllerTraitTests` 12, `EnemyTraitContentTests` 3.

Changed in place, no count change:
- `EnemyRosterActIIWestTests`: the legionnaire pin now reads the trait and a frontal reduction of 0.
- `Level06ContentTests.EveryLegionnairePostIsFlankableFromAbove`: its pin and comment.
- `StoryBlockClassificationTests.PlayerUltimateClassDamageIgnoresEnemyFrontalReduction`: clears `Trait` on its duplicate, so it still tests the frontal path.

Seen failing: 17 of the 23 against rules mutated to defaults with no hooks wired. The other 6 (negative cases and the content sweep) under a second mutation pass: the impact from the reduced intake, an armour that cuts everything, a workable height of −2000, and a fourth carrier. The mutations are reverted.

Hashes and saves: no Fighter hash moves. Nothing under `scripts/FighterSim/` names a trait. Save schema v8, protocol v5, no component ID, no `en.csv` key.

## Bot smoke runs (one run each, not evidence of balance)

Joan, `play`, Normal, on this branch without the other workstreams:

| Level | End | Deaths | Trait counters |
|---|---|---|---|
| 1 | Collapse by death (4 deaths, 219 s) | 4 | 30 catches, 8 counters, all 8 connected |
| 6 | Complete | 1 | 294 damage prevented, 0 strips (bots never grab) |
| 8 | Complete | 2 | 0 anti-air strikes |

Two things to read in that table. The Level 1 collapse has no control run beside it, but all 8 counters connected and this branch lacks GR-SCHED, so the bot had no Special there. Watch Level 1 at gate A. The rising strike has only ever fired in the unit suite: the `play` bot rarely descends within 110 px of a ward.

## Deviations from the plan

1. **"Behind" had to be defined.** Reading it as "the mob's spawn x lies between the lock's entry and the objective" fails on shipped content (Level 1's print shop gears, Level 8's vault glyphs), and Phase A may not edit levels. Members wake and chase on engage and no mob body blocks the hero, so such a mob holds nothing. The sweep instead fails an objective the hero must leave the floor to work (more than 180 px over the lock's floor) within 320 px of a trait mob's post. It also fails if it ever goes vacuous. This is narrower than the plan's sentence, and the user may want to see it.
2. **Hook lines beyond the four named.** `EnemyController.cs` also carries call lines in `ProcessMobTick` (`TickTraits`), `RefreshArmorPresentation`, `ApplyData`, `OnSpawn`, `ApplyStoryRewind` and `OnExecutorActiveWindowEnded`. The matrix's "call lines only" allows them, and a tick, a glow and pool hygiene need them. `BeginHeld` needs **no** hook: its existing `Executor.Cancel()` ends a counter, and the armour falls at `LaunchThrown` as ruled.
3. **The diamond means Guard-Crush class.** The tint and the glyph are read from one class flag and may not disagree, so both trait strikes are blockable for two charges rather than one. A blocked one opens the D8 Punish window.
4. **A summoned body carries no trait** (`ActiveTrait`). Level 0's hold spawns `cyber_guard` for Leonardo and `plasma_spear_ward` for Cleopatra as summons, and its lesson teaches the plain string. No boss summons a carrier today.
5. **The catch still holds on the cooldown.** The cooldown gates only the counter, the literal reading of §1.12.
6. **A hit recovery of 16 frames** exists beside the ruled miss recovery of 40.

## Merge notes

- The telemetry hunk is its own commit, `512f1ef5`, one block in `PlaytestTelemetry.cs`: **merge it after GR-CROWD**.
- `EnemyData.Trait` sits right after `FrontalDamageReduction`, away from GR-CROWD's appended exports.
- GR-RUN: the Catcher catches only `combo_1` and `combo_2`, so a run attack stuns it. Check that this is the wanted answer when `VariantRunAttack` lands.
- GR-HAZ: call lines in `EnemyController.cs` are disjoint from these.

## Did not ship

- `compare_runs.py` has no trait table.
- No new art or pose: the catch reuses the hit flash and the existing `attack` reel; the rising strike has no upward stance.
- No localized text explains a trait.
- The bots do not grab, so Armoured strips and throws read 0 in every bot run.
- The CPU, the Mirror clone and bosses have no traits.

## What the docs owe (GR-DOCS)

- **AGENTS.md:** a Combat bullet for the traits, this suite delta, and the "behind" definition.
- **Ledger:** `PROVISIONAL-ENEMY-TRAIT-RULES`, plus the user rows for 2b and 2b+.
- **Stale comment:** `scripts/Environment/Level06Controller.cs:32-34` still describes the frontal shield. That file is GR-LV2's, and the comment is not touched here.
- **Design master:** the legionnaire's frontal shield line, and the trait rules.
