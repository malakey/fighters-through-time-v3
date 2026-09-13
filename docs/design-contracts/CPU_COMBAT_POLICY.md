# CPU grabs and Echo Step — F19, Option A

Decision: 2026-09-12. Easy uses neither action; Medium introduces occasional use; Hard uses both tactically. This complements [CPU recovery](CPU_RECOVERY.md). These are design rules; implementation and combat validation remain pending.

## Selection and perception

Add Grab and EchoStep to the existing utility action set. Generate ordinary player inputs through the shared action resolver, including chord priority; never call an ability directly to bypass eligibility. Continue evaluating every three frames. Opponent reads use each tier's existing reaction delay, including Hard's 4–8 frames. Frame-perfect execution of an already chosen string does not imply zero-delay perception of a new opponent action.

Use current self-state for legality and delayed opponent state for decisions. Contact resolves against the actual current world, so an opponent who stops blocking, jumps or rolls can make a planned grab miss. Do not consult future inputs or outcomes. A missed decision window produces no retroactive cancel or refund.

## Difficulty policy

| Tier | Grab | Echo Step |
|---|---|---|
| Easy | Disabled. | Disabled. |
| Medium | Occasional attempt against an observed blocking, grounded opponent within plausible grab reach. Initial tuning target: 25% admission chance per eligible blocking opportunity, then compare with other legal actions. | Occasional undo of a vulnerable whiff during eligible attack recovery, when the historical destination improves safety. Initial tuning target: 25% admission chance per eligible attack execution. |
| Hard | Score an anti-block grab against attacks, movement and defense using observed stance, range, exposure during startup/whiff, and useful legal throw direction. No automatic grab on every block. | Score a legal recovery cancel against staying in recovery, legal block-cancel/movement and retaining meter for Ultimate or unused Defy. Favor meaningful reductions in punish risk or improved positioning; reject a clearly worse destination. No automatic cancel of every whiff. |

The Medium percentages are provisional tuning values, not measured balance results. Roll admission once per opportunity using the seeded match PRNG, never once per three-frame evaluation. A grab opportunity begins when the delayed observation first satisfies the blocking/grounded/range conditions; it ends when those conditions cease or the CPU commits another action. Do not reopen the same continuing opportunity just because the admission roll failed. Echo Step admission is keyed to the CPU's attack execution ID. Losing eligibility or taking a different action never refunds or rerolls that opportunity. Snapshot the admission result, opportunity/attack IDs and PRNG state for deterministic resimulation.

## Grab execution

Use the canonical grounded grab: 0.8-unit reach and 10/4/24 startup/active/whiff-recovery frames, sourced from shared rules rather than duplicated AI constants. The grabber drops block for the attempt. Do not intentionally target victims known to be airborne, rolling, invulnerable, in hitstun, daze or shieldstun. Shared rules still determine actual contact, clash, armor bypass and any immunity. A grab answers the block stance; it is not a tick-throw or guaranteed follow-up through shieldstun.

Medium uses a simple legal directional throw toward the nearer useful stage edge, with a deterministic fallback when tied. Hard compares the existing legal throws for positioning, follow-up potential and visible hazard exposure. Neither tier gets guaranteed damage or an invented throw/cancel. Grab, held, thrown and grab-whiff states remain ineligible for Echo Step.

## Echo Step execution and meter

Require the recovery frames of the CPU's own basic, directional or Special attack, at least 30 meter, an available 120-frame cooldown, and every canonical state restriction. The shared action resolves the 8-frame wind-up and the exact position from 30 frames before accepted activation, locked throughout the wind-up; facing remains and arrival velocity becomes zero. No CPU-specific destination, extra immunity, healing, cooldown rewind or resource restoration. Follow the [temporal contract](TEMPORAL_STATE_CONTRACT.md): 31 exact frame samples, sufficient real history, reject an invalid destination before spending, and recheck at completion with no refund if it became blocked. Never choose a nearby substitute.

Medium's whiff candidate is an attack execution with no hit or block contact, using the completed contact result once available. If delayed threat recognition leaves no legal recovery window, do not use Echo Step. Hard may also choose other legal recovery cancels when their utility warrants the cost.

Check the resolved historical destination against current geometry and perceived danger before selection, including pit risk; a teleport toward a worse or unsupported location is not a defensive improvement. Do not treat Echo Step as general offstage recovery or an escape from hitstun, daze, shieldstun, block stance, ledge hang, hitstop, Ultimate, an active respawn platform or any grab state. An interrupted armed wind-up follows the canonical spent-meter/cooldown rule without a refund.

Medium retains its existing immediate-Ultimate policy at the first legal opportunity when meter is full. If both actions are legal in one decision, that policy takes priority; being at full meter alone does not make an otherwise illegal Ultimate cast legal. Hard values a viable Ultimate setup and retaining full meter for an unused, eligible Defy; spent or mode-disabled Defy has no reserve value. This is a utility tradeoff, not a mandatory meter floor: Hard may spend to avoid a credible punish. Resolve one accepted action and one meter debit per decision; recheck meter and eligibility on commitment.

## Validation before sign-off

Verify Easy emits neither action. Cover Medium's once-per-opportunity sampling and Hard's tactical selection using seeded replays, all nine kits and the canonical [combat validation scenarios](COMBAT_VALIDATION.md). Include stationary block, stance release, shielding at zero charges, target shieldstun, airborne/rolling targets, grab clash, whiff recovery, Echo Step at 29/30/100 meter, cooldown active/ready, all forbidden states, unsafe historical destinations, interrupted wind-up and expired decision windows. Check Medium's Ultimate priority, Hard's unused/spent/disabled Defy valuation, and single-action/meter accounting. Compare replayed decision traces and resource state; do not infer combat escape guarantees from CPU success rates.

F20 Option A resolves Mirror Paradox through its explicit [campaign profile](MIRROR_PARADOX.md): Easy/Normal/Hard use the corresponding reaction, defense and grab/Echo Step policies, but the boss retains its full core kit on every difficulty. Its Easy and Normal full-meter Ultimate policy takes priority at a legal opportunity. Holodeck settings do not control boss difficulty.
