# FU-STORYSHOVE - the Story hero's grounded hitstun is the same short shove as Fighter mode (2026-10-26)

Follow-up to the closed 2026-10-25 feel and UI update. User ruling: "Yes, fix it in Story too" (ledger `USER-STORY-HERO-HITSTUN-SHOVE`; closes `VERIFY-STORY-HERO-HITSTUN-GLIDE`).

## What changed
- `scripts/Combat/BasicComboRules.cs`: the three shove constants lost their `Fighter` prefix - `HitstunShoveScale` 3.0, `HitstunShoveDecay` 0.79, `HitstunShoveSnapSpeed` 0.25 (u/s). Every reference updated (`FighterKnockbackFeelRules`, docs). No value changed.
- `scripts/Characters/PlayerController.cs`: `OnHurtboxHit`'s grounded non-launching branch multiplies the hero's horizontal speed by the scale; `ProcessStunned` decays it by the decay each tick (snap below 15 px/s) when `IsOnFloor() && !_stunTumble`. The state never runs under the hero's hitstop (the early return in `_PhysicsProcess`), so the decay pauses with it.
- Unchanged: launches (tumble), the guard-break daze push (Dazed state), knockdown, airborne hits, Story mobs (S1), environmental hits (zero knockback). The Level 13 Mirror clone inherits the rule.
- Tests: new `tests/Unit/StoryHitstunShoveTests.cs` (5). `StoryCombatRulesTests.StoryKnockbackOnTheHeroIsStaticAtEveryHP` changed in place (its grounded horizontal expectation now carries the shove scale; still equal at every HP). `FighterFinisherKnockbackTests`' source scan no longer lists `HitstunShove` as a sim-only needle, because Story now reads the rulebook constants legitimately.

## Measurements (flat floor, hitstun 0.6 s = 36 ticks, no hitstop; mob-style hit, `SourceIsStandardMob`)
Before (analytic: constant `force/(1+weight) x 60` px/s for the whole stun): Einstein force 2 = 36 px (0.6 u), force 5 = 90 px (1.5 u); Lincoln 28 px to 69 px.
After:
| hero | force 2 | force 3 | force 4.5 | force 5 |
|---|---|---|---|---|
| Einstein | 10.2 px (0.17 u), stop tick 11 | 15.9 (0.27 u), 13 | 24.2 (0.40 u), 14 | 27.2 (0.45 u), 15 |
| Lincoln | 7.6 (0.13 u), 10 | 12.0 (0.20 u), 12 | 18.4 (0.31 u), 13 | 20.7 (0.34 u), 14 |
| Joan | 9.7 (0.16 u), 11 | 15.2 (0.25 u), 13 | 23.1 (0.38 u), 14 | 25.9 (0.43 u), 15 |
This is inside (just below, for the lightest hit on the heaviest hero) the Fighter's 0.2-0.6 u range, so no new constants. Elite and boss hits launch (F7) and a hazard tick carries no knockback, so neither is a shove case.

## Open
Nothing played or looked at; headless only. A human check that the hero reads planted rather than stiff.
