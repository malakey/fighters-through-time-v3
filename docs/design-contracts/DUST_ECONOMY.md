# Chronal Dust Economy — F05 Option A

Design revision: 2026-09-12; **amended 2026-09-28 (S27): the Legacy Level (4A) is retired and its row folds into Level 5 — every total, maximum and pacing threshold below is unchanged.** User-selected direction: retain the **975-dust Resonance Grid** and meaningful upgrade choices. This ledger specifies the replacement reward budgets; it is not a measurement of shipped levels. Scene reward manifests, resource values, and gameplay validation still need implementation.

This file is the economy design ledger referenced by [the main design](../design-godot-v7.md). Its budgets replace the former universal 1–2 dust per standard enemy, 20 per elite, 50 per boss, and 25 per Extractor. The historical shipped Extractor value of 15 and the Mirror Paradox wallet-direct exception are not exceptions to this new design.

## Targets and accounting basis

- Grid prices remain 50 / 75 / 200 for three Tier 1 minors, three Tier 2 minors, and three Majors: **975 total**. Do not change grid prerequisites merely to force these income targets. F08 Option B explicitly simplifies Shakespeare's Majors to require all three Act II nodes; its later first-Major timing is accepted without increasing rewards.
- The required route pays **720 base dust**, or **720–787 after Integrity bonuses**.
- Fully collecting optional content adds **280 base dust**. A thorough run pays **1,000 base**, or **1,000–1,094 after Integrity bonuses**.
- These figures assume completion of each source once, collection of all drops on the chosen route, and no dust-loss penalties. Missed pickups, skipped encounters, and the established collapse/exit penalties can reduce income; the target is not a guaranteed wallet floor or automatic compensation.
- Level 0 and training award no persistent dust. A run includes Levels 1–15.
- Level 1 has no Integrity clock and grants no Integrity bonus. Levels 2–15 use their existing tier rules. All difficulties use the same base envelopes.
- “Required route” means all authored mandatory combat and its boss, excluding optional detours. “Thorough” adds all Extractors, the designated secret, and any other optional rewards; no optional reward is added outside the envelope.

## Per-level budgets

Boss rewards are **25 dust each** (15 bosses = 375). The required encounter column includes every other mandatory reward, including elite/Eraser ambushes; Level 5's larger pool absorbs the retired 4A row (its 15 encounter + 25 boss dust, plus 10 optional). The optional column is a total for the level, never a reward per object. Cumulative columns exclude bonuses.

| Level | Required encounters | Boss | Required total | Optional total | Required cumulative | Thorough cumulative |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 25 | 25 | 50 | 10 | 50 | 60 |
| 2 | 15 | 25 | 40 | 10 | 90 | 110 |
| 3 | 15 | 25 | 40 | 10 | 130 | 160 |
| 4 | 15 | 25 | 40 | 20 | 170 | 220 |
| 5 | 55 | 25 | 80 | 30 | 250 | 330 |
| 6 | 15 | 25 | 40 | 20 | 290 | 390 |
| 7 | 15 | 25 | 40 | 20 | 330 | 450 |
| 8 | 15 | 25 | 40 | 20 | 370 | 510 |
| 9 | 15 | 25 | 40 | 20 | 410 | 570 |
| 10 | 15 | 25 | 40 | 20 | 450 | 630 |
| 11 | 15 | 25 | 40 | 20 | 490 | 690 |
| 12 | 25 | 25 | 50 | 20 | 540 | 760 |
| 13 | 35 | 25 | 60 | 20 | 600 | 840 |
| 14 | 35 | 25 | 60 | 20 | 660 | 920 |
| 15 | 35 | 25 | 60 | 20 | 720 | 1000 |
| **Total** | **345** | **375** | **720** | **280** | **720** | **1,000** |

Level 1's optional allocation belongs to its Extractors; it does not add a secret before the existing Level 2 introduction. Act III conduits, valves, and pylons use the Extractor allocation; their narrative names do not create extra currency. Under M16 (2026-09-26) they are 3-second Interact channels rather than combat machines; completion pays the same allocation once as a physical pickup.

## Turning budgets into drops

These are authored budgets, not an invisible runtime cap that stops paying after arbitrary kills.

1. Give each finite reward source a stable ID in a level-and-difficulty reward manifest. Include every mandatory enemy/wave, elite, boss, Extractor, secret, scripted award, and optional encounter. A source must have exactly one budget category. Repeated phases of one boss share one boss reward.
2. Reserve the boss's 25. Allocate the required encounter pool across its finite enemy source IDs **before play**. Default relative weights are standard enemy = 1 and elite/Eraser = 5; scripted mandatory encounter rewards must replace an equivalent allocation, not add to it.
3. For an integer pool `P`, calculate `quota[i] = P × weight[i] / sum(weights)`. Assign each source `floor(quota[i])`, then give the remaining units to the largest fractional remainders, breaking ties by stable source ID. This guarantees the sum equals the pool. These weights are allocation ratios, not flat drop amounts.
4. Zero allocations are legal and spawn no currency pickup. **The former “every enemy guarantees dust” rule is retired.** Positive allocations always spawn one physical pickup at that source's defeat. Health/buff drop chances remain a separate system. Do not randomize the dust quantity or reallocate it after a player skips a source.
5. Each difficulty compiles its authored source list against the same level pool. More enemies on Hard change the distribution, not the total. Reinforcements/summons that can repeat indefinitely have zero dust; finite scripted waves draw from the existing allocation. Changing a layout requires regenerating and validating its manifest before shipping.
6. Optional allocations: Level 1 assigns all 10 to its Extractors. Other levels assign half their optional pool to all Extractors combined, and half to the designated secret/discovery reward. Split the Extractor half evenly by the same integer/remainder rule. With a 20-dust optional pool, two Extractors pay 5 each or three pay 4/3/3, and discovery pays 10. With a 10-dust optional pool, two pay 3/2 or three pay 2/2/1, and discovery pays 5.
7. If an Extractor is also the designated secret, it owns the sum of its machine share and the discovery share, paid once as one pickup; the discovery flag must not trigger a second payout. A secret containing a Story item still carries its budgeted dust share. Optional guards draw no additional dust by default; if their kills carry some of the discovery reward, explicitly transfer that amount from the same pool and record each source. There is never an extra universal elite award.
8. Puzzles, tutorial enemies, training, and repeatable spawns award no extra dust. Any bespoke bonus must be funded by an existing envelope. The Mirror Paradox boss follows the same 25-dust physical-pickup rule; remove its wallet-direct path.
9. Keep the existing quantity-based sprite thresholds: Small 1–5, Medium 6–24, Large 25+. Every boss still produces a Large pickup at the arena center. An ordinary Extractor produces a Small or Medium icon according to its actual award, not a forced Large icon. E01 keeps the actual-collection +N dust feedback separate from destruction's brief future-drain notice; it neither credits an uncollected pickup nor changes this allocation. See [Extractor feedback](CAMPAIGN_VALIDATION.md#e01--readable-extractor-detours).

For example, allocating 15 encounter dust to ten standard enemies and two elites gives quotas of 0.75 for each standard and 3.75 for each elite. Integer allocation pays the exact 15 across that authored list; it does not promise 1–2 per standard plus 20 per elite. If an author wants a particular elite to carry more of the pool, author a fixed share and distribute only the remainder; validate the unchanged total.

## Integrity bonus and banking

The existing tiers remain **Restored ≥50%: +10%**, **Stabilized ≥20%: +5%**, **Fractured <20%: +0%**. Calculate exactly once on successful level completion:

`bonus = floor(retainedBaseDust × tierRate)`

Here `retainedBaseDust` is the current level's collected, undeposited base dust remaining after any applied loss penalties; it excludes deposited dust, respec refunds, and any prior tier bonus. Add the bonus to that level's wallet and then auto-deposit once using the level-completion transaction. Do not compound bonuses, pay them at checkpoints, or pay for failed attempts. The results screen itemizes required enemies, boss, Extractors, secret/other optional allocations, losses, and the tier bonus.

All-Restored maxima are **787 required** and **1,094 thorough**, not 792/1,100: Level 1 receives no tier bonus. Mixed tiers and per-level integer rounding stay between the listed base and maximum values. These are completed-run totals, not spendable balances before the final boss.

F02 still applies: only level completion banks current-level earnings. Act III Beacons spend previously deposited dust only. Level 13 dust becomes usable in Level 14; Level 14 dust becomes usable in Level 15. Level 15 rewards arrive after the final encounter and cannot fund that fight.

## Retries and persistence

- Track source state as unissued, spawned/uncollected, or collected, together with pending pickup state and the retained level wallet. Collection commits the source claim and wallet increment together.
- Death rewind, checkpoint resume, Anchor Snap, quit/resume, and crash recovery preserve collected source claims. A restored enemy whose reward was collected may fight again but awards zero additional dust. Restore an uncollected source/pickup consistently without creating two copies.
- Collapse and voluntary exit apply the existing 20% undeposited-dust rule; they never unclaim collected sources. Lost dust cannot be recovered by killing the same source again. Crashes keep their existing no-fee rule.
- A full Restart Level clears the entire active level wallet, pending bonus/completion state, pickup state, and that level's source claims together, then resets the authored level. Previously deposited dust remains safe. This permits a fresh attempt without stacking income from discarded attempts.
- N01's accepted post-boss sealing interaction commits actually collected/retained rewards, the tier bonus, banking and the completion marker exactly once. Boss defeat still spawns its existing physical pickup and does not auto-credit or deposit it. Remaining local Extractors shutting down at sealing grant no destruction loot, secret claim or higher Integrity tier. Preserve pending pickup/ready-anchor state before sealing and resume committed completion afterward under [narrative resolution](NARRATIVE_RESOLUTION.md). Completed levels cannot be replayed. Reloading a completion overlay cannot deposit twice.
- These reward-specific requirements are supplemented by the selected [F10 checkpoint/persistent-attempt contract](STORY_PERSISTENCE.md), including state ownership, crash transactions, terminal Smothered, and completion routing. Runtime validation remains pending.

## Upgrade pacing

Affordability means total deposited earnings sufficient for a legal purchase path, with no losses and saving toward it (or using the existing free respec). Spending on a different path changes the timing. Ability unlock requirements still apply. “After Level X” means at the next hub visit or eligible Act III Beacon.

| Threshold | Required, no bonus | Required, all Restored | Thorough, no bonus | Thorough, all Restored |
|---|---|---|---|---|
| 50 dust | After 1 | After 1 | After 1 | After 1 |
| 325 dust | After 7 | After 7 | After 5 | After 5 |
| 350 dust | After 8 | After 7 | After 6 | After 5 |
| 375 dust | After 9 | After 8 | After 6 | After 6 |
| 400 dust | After 9 | After 8 | After 7 | After 6 |
| 475 dust (Shakespeare: first Major) | After 11 | After 10 | After 8 | After 7 |
| 600 dust | After 13 | After 13 | After 10 | After 9 |
| 650 dust | After 14 | After 13 | After 11 | After 10 |
| 675 dust (Shakespeare: two Majors) | After 15 | After 14 | After 11 | After 10 |
| 775 dust | Not reached | After 15 | After 13 | After 12 |
| 875 dust (Shakespeare: three Majors) | Not reached | Not reached | After 14 | After 13 |
| 975 dust | Not reached | Not reached | After 15 | After 14 |

- **First minor:** affordable after Level 1 even without optional collection or a performance bonus.
- **First Major:** minimum paths now range from 325 to **475** depending on character. Required-route affordability spans Levels **7–11**; thorough affordability spans Levels **5–8**. Under **F08 Option B**, Shakespeare needs one Act I node (50), all three Act II nodes (225), and a Major (200): **475** total, after Level **10–11 required** or **7–8 thorough**. Act II retains Any-of Act I prerequisites. This deliberately places his first Major later than the former 400-dust path.
- **Second Major:** use the 600/650/675/775 thresholds as budget checkpoints, not universal prices for two Majors. Validate the actual prerequisite sets for every character. Shared prerequisites can make a pair cheaper; Shakespeare needs **675** for two Majors and **875** for three (one Act I + all Act II + the chosen Majors). Buying all six minors plus two Majors costs 775. Shakespeare cannot afford all three Majors on the required route; thorough collection reaches 875 after Level 13–14. Required-route funding of his second Major may arrive only after the final encounter, when those rewards cannot improve that fight. The required route does not promise two complete branches or every desired pair under all tiers.
- **Full grid:** a thorough, all-Restored run reaches 1,006 deposited dust after Level 14, so it can complete the grid at Level 15's entry Beacon. A thorough run without bonuses reaches 1,000 only after Level 15; it cannot use those final rewards against the final boss. The required route never funds the full grid.
- Pure budget thresholds do not prove encounter balance or the value of grid nodes. Playtest pacing on every character; fix duplicate/undefined perks through F06 without silently moving these budgets.

## Validation and implementation handoff

Verified design arithmetic (rechecked after S27): 15 boss awards total 375; required encounter budgets total 345; optional allocations total 280; base totals are 720/1,000; all-Restored maxima are 787/1,094; the full grid remains 975. Integer source allocation must preserve each pool for every difficulty.

Still required before gameplay sign-off:

- Author actual stable source IDs and per-source values for all 15 visited levels and each difficulty; this checkout supplies design budgets, not validated scene inventories.
- Verify source sums, overlapping secret/Extractor ownership, finite ambushes, zero-reward repeatable spawns, and all bespoke awards against the ledger.
- Exercise reward persistence with collected and uncollected drops across death rewind, collapse, Anchor Snap, quit, crash, restart, and completion reload.
- Check representative legal first/second-Major routes in all nine grids and playtest affordability against encounter difficulty. Review poor-performance/loss-heavy runs separately; never inflate the “required-route” claim to include compensation that does not exist.
- Update the runtime resources and reward plumbing together. The historical shipped values remain a known implementation mismatch until that work is completed.
