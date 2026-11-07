# CPU grabs, Specials and Defy — F19, Option A (Echo Step removed 2026-10-25; Specials and Defy by band 2026-10-31)

Decision: 2026-09-12; Echo Step, the other F19 action, was removed with the verb on 2026-10-25 (feel and UI update), leaving the grab. Easy does not grab; Medium introduces occasional use; Hard uses it tactically. This complements [CPU recovery](CPU_RECOVERY.md). These are design rules; implementation and combat validation remain pending.

## Selection and perception

Add Grab to the existing utility action set (EchoStep was added with it and removed on 2026-10-25). Generate ordinary player inputs through the shared action resolver, including chord priority; never call an ability directly to bypass eligibility. Continue evaluating every three frames. Opponent reads use each tier's existing reaction delay, including Hard's 4–8 frames. Frame-perfect execution of an already chosen string does not imply zero-delay perception of a new opponent action.

Use current self-state for legality and delayed opponent state for decisions. Self-state includes the CPU's own Special cast (user decision 9.3, 2026-10-16: **"Leave for now — only adapt it to the new Special timing (1.2) so it stays functional, including the Level 13 Mirror."**). The CPU observes its cast's phase and remaining frames, schedules no grab while it casts, decides a Special or Ultimate only when its cast will be over by delivery, and drops at delivery a Special or Ultimate press that the cast still holds, so it does not press into its own cast. Its grab legality also refuses during its landing-tech lock, its knockdown and its own Ultimate. These are input-side checks with no PRNG draw and no snapshot state. Ruling 9.3 adds no kit awareness: the CPU does not reach-check a melee Special before pressing it, cancel a landed Special on purpose (D3), use the ledge get-ups or time a perfect block deliberately (a reactive Block raise can still be perfect). Contact resolves against the actual current world, so an opponent who stops blocking, jumps or rolls can make a planned grab miss. Do not consult future inputs or outcomes. A missed decision window produces no retroactive cancel or refund.

## Difficulty policy

| Tier | Grab |
|---|---|
| Easy | Disabled. |
| Medium | Occasional attempt against an observed blocking, grounded opponent within plausible grab reach. Initial tuning target: 25% admission chance per eligible blocking opportunity, then compare with other legal actions. |
| Hard | Score an anti-block grab against attacks, movement and defense using observed stance, range, exposure during startup/whiff, and useful legal throw direction. No automatic grab on every block. |

The Medium percentages are provisional tuning values, not measured balance results. Roll admission once per opportunity using the seeded match PRNG, never once per three-frame evaluation. A grab opportunity begins when the delayed observation first satisfies the blocking/grounded/range conditions; it ends when those conditions cease or the CPU commits another action. Do not reopen the same continuing opportunity just because the admission roll failed. Losing eligibility or taking a different action never refunds or rerolls that opportunity. Snapshot the admission result, the opportunity ID and PRNG state for deterministic resimulation.

## Grab execution

Use the canonical grounded grab: 0.8-unit reach and 10/4/24 startup/active/whiff-recovery frames, sourced from shared rules rather than duplicated AI constants. The grabber drops block for the attempt. Do not intentionally target victims known to be airborne, rolling, invulnerable, in hitstun, daze or shieldstun. Shared rules still determine actual contact, clash, armor bypass and any immunity. A grab answers the block stance; it is not a tick-throw or guaranteed follow-up through shieldstun.

Medium uses a simple legal directional throw toward the nearer useful stage edge, with a deterministic fallback when tied. Hard compares the existing legal throws for positioning, follow-up potential and visible hazard exposure. Neither tier gets guaranteed damage or an invented throw/cancel.

## Specials need a Momentum pip (2026-10-31)

The Special rungs of the utility set test one predicate where they used to test the slot's cooldown: the CPU's own Momentum gauge holds at least one whole pip, and the slot's 2 s reuse delay will be clear by delivery (within the band's minimum reaction delay). An observation that does not carry the gauge (a hand-built one, the legacy default) ignores the pip test and checks the reuse delay alone. Every seeded percentage draw stays where it was, behind the ready test, so Easy (which casts no Special) is unchanged and no draw moves for a ready Special. The CPU stays kit-blind (decision 9.3). The Level 13 Mirror clone reads its Story gauge through the same predicate.

## Defy History by band (user decision 4.4, 2026-10-30; built 2026-10-31)

Defy is an input the CPU emits like any other. It requires 50 or more Influence and is never emitted while dazed, captured, already Defying or inside the CPU's own Ultimate. **Easy** never Defies. **Medium** rolls once per hitstun instance (35 %, drawn only when the gate passes) once the string against it has reached 3 hits. **Hard** rolls nothing and Defies when the string reaches 2 hits, when it is cornered with the target on the open side within 3.0 units, or when the target is in an Ultimate wind-up within 3.0 units (the press must still leave the blast on a wind-up frame); otherwise it banks its meter for the Ultimate. A "string" is consecutive hitstun instances with at most 8 actionable frames between them, counted input-side. The string reflex is emitted exclusively on the hitstun-instance edge (like the V7.4 Block hold, and it wins that tick over it); the neutral uses go through the band's reaction delay on the schedule ring. Since 2026-11-03 Hard keeps a meter reserve for its Ultimate ("Hard's meter reserve", below); before that it reserved none and could spend its bar on Defies and starve its Ultimate. The Level 13 Mirror clone follows its campaign band (whose Hard band keeps no reserve, 2026-11-03); the hero has no Ultimate wind-up and Story has no stage walls, so only the string trigger fires there. These percentages and ranges are provisional and code-owned (`CpuBandTuning`), never resources.

## Ultimate use
Medium retains its immediate-Ultimate policy at the first legal opportunity when meter is full; being at full meter alone does not make an otherwise illegal Ultimate cast legal. Hard holds the Ultimate for a confirmed kill setup (see the main design) and competes with Defy for the same meter (above). Resolve one accepted action and one meter debit per decision; recheck meter and eligibility on commitment.

## Validation before sign-off

Verify Easy emits no grab. Cover Medium's once-per-opportunity sampling and Hard's tactical selection using seeded replays, all nine kits and the canonical [combat validation scenarios](COMBAT_VALIDATION.md). Include stationary block, stance release, shielding at zero charges, target shieldstun, airborne/rolling targets, grab clash, whiff recovery, all forbidden states and expired decision windows. Check Medium's immediate-Ultimate policy and single-action/meter accounting. Since 2026-10-16 also check that no Special, Ultimate or grab press reaches the CPU's own Special cast, and that its grab refuses during its tech lock, its knockdown and its own Ultimate. Compare replayed decision traces and resource state; do not infer combat escape guarantees from CPU success rates.

F20 Option A resolves Mirror Paradox through its explicit [campaign profile](MIRROR_PARADOX.md): Easy/Normal/Hard use the corresponding reaction, defense and grab policies, but the boss retains its full core kit on every difficulty. Its Easy and Normal full-meter Ultimate policy takes priority at a legal opportunity. Holodeck settings do not control boss difficulty.

## Hard's meter reserve (ruling 9d, 2026-11-03)

From the user's gameplay-review rulings of 2026-11-03 (`docs/recon-2026-11-03/DECISIONS_gameplay_review.md`; plan `docs/GAMEPLAY_REVIEW_2026-11-03_PLAN.md`): "Save for the Ultimate when close."

- At **70 Influence or more**, the Hard CPU Defies only when the target is in an Ultimate activation wind-up within 3.0 units (the same range test as the neutral trigger) or when its own HP is at or below **25 %** of its max. A string or corner Defy is held back while the reserve holds, so it banks the meter for its Ultimate. Below 70 nothing changes.
- The reserve is tested before any roll, so no seeded draw moves. Only the Hard band carries it; Easy and Medium are unchanged.
- **The Level 13 Mirror clone is exempt** (ruling 9d+): its Hard band, `CpuBandTuning.HardBoss`, is Hard with the reserve off ([Mirror Paradox](MIRROR_PARADOX.md)).
- The 70 and 25 % are provisional (the ruling said "about 70" and "near death") and code-owned (`CpuBandTuning.DefyReserveInfluence`, `DefyNearDeathHPPercent`), never resources. Hard may still bank to 100 and not convert; no time cap is built, and that risk stays open for play.
- The CPU stays kit-blind. It gets a running attack only when it happens to press at a run; its melee band still reads string hit 1.
- The matrix tool used for the gameplay review (ruling 8b) is kept as a gated test, `tests/Tools/FighterMatrixMeasureTests.cs`, which runs only when `FTT_MATRIX` names a config file (`tools/fighter_matrix/README.md`). Hard CPU matrices from before and after this change are not comparable.
