# CPU recovery contract — F19, Option A

Design decision: 2026-09-12. Easy attempts basic recovery; Medium and Hard use progressively more capable planning. This contract resolves the recovery portion of F19. The companion [CPU combat policy](CPU_COMBAT_POLICY.md) resolves grabs and Echo Step under the user's second Option A selection. Implementation, ability-resource mapping and stage validation are pending.

## Shared rules

Recovery means returning from an unsupported position over a stage gap to a reachable platform or legal ledge before the bottom blast zone. Detect it from authored floor segments, ledges, fighter position and velocity, not from camera bounds or a universal main-platform Y threshold. A routine jump above supported ground is not an offstage episode.

Use the existing three-frame decision cadence and reaction windows: Easy 30–45 frames, Medium 15–20, Hard 4–8. Geometry and the CPU's own current state may be checked directly for input legality; opponent information still passes through the reaction buffer. Recovery does not grant immediate reactions to new threats. Commit a recovery plan after the tier's delayed recognition of the situation; do not add the full reaction delay anew to every input in that plan. Replanning uses the same tier's perception rules.

Recovery planning takes priority over optional offense and orb pursuit while return is at risk, subject to normal action locks. Check remaining jumps, ability cooldowns, allowed aerial use, steering, collision, ledge eligibility/regrab limits, and the predicted landing or ledge capture before issuing input. No action cancels hitstun, daze, grabs or another locked state unless the shared combat rules explicitly allow it. Existing DI and tech probabilities remain separate. Revalidate legality immediately before each planned action.

No CPU-only jump resets, cooldown refunds, invulnerability, teleport reach or Story grid perks. Read movement distances, durations and trajectories from the same normalized Fighter resources and simulation used by human players; do not duplicate tuning constants. Closed stages and ordinary supported traversal must not trigger emergency casts. An unreachable route may still end in a KO; no tier promises successful or optimal recovery.

## Difficulty behavior

| Tier | Recovery policy |
|---|---|
| Easy | Steer toward the nearest plausible legal ledge or landing, use remaining jumps and at most one activation of the character's movement ability per offstage episode. Use a simple jump-then-movement sequence when it can improve the return; omit unnecessary or unavailable actions. Hold/steer a glide or directed move as its normal controls require. Do not optimize cooldown cycles, construct hops or Special chains. Specials remain disabled on Easy. Its slow reactions and weak combat remain. |
| Medium | Estimate reachable nearby landings/ledges and select a basic route using remaining jumps plus one suitable recovery ability activation. The movement ability is the default; a validated mobility Special may substitute where its trajectory fits better. Account for ascent versus horizontal distance and avoid obviously blocked routes. No multi-ability or deliberate platform/refund chains. |
| Hard | Compare feasible routes and plan longer legal sequences using remaining jumps, movement abilities and explicitly validated mobility Specials or temporary platforms. Account for available resources, platform lifetime and visible threats through the reaction buffer. Re-evaluate after interruption or changed geometry; do not execute a fixed jump → movement → Special 2 script. Every action must make progress toward returning, not create indefinite offstage cooldown/ledge stalls. |

An offstage episode ends on a stable landing on an authored stage platform or successful legal stage-ledge capture, or on KO. A jump refund or temporary staff-platform landing alone does not reset Easy/Medium's one-activation planning limit. Baseline ability effects still occur normally, including Pocahontas's jump reset and Mozart's platform/cooldown behavior; the limit constrains AI planning, not the character's kit.

## Per-character recovery profiles

All profiles include universal movement and legal ledge capture. These are design capabilities, not measured reach guarantees.

| Character | Primary movement ability | Planning constraints |
|---|---|---|
| Einstein | Relativity Warp | Aim horizontal, vertical or diagonal translation; use the authored reduced-gravity float window when useful. Fast-fall cancels that float. Relativity Rift is not a directional recovery Special. |
| Joan | Ascendant Wings | Rising leap followed by held glide, up to its normal three seconds. No purchased Wings Refresh in Fighter. |
| Leonardo | Ornithopter Flight | Vertical boost followed by horizontal glide, up to its normal three seconds. A turret is not a solid stepping platform or prerequisite for recovery. |
| Tesla | Lightning Blink | Aim the short directional blink at a legal return route; respect translation/collision rules and authored duration. Lorentz Pulse does not move Tesla back to a ledge. |
| Shakespeare | Prospero's Flight | Forward/upward gust followed by horizontal glide. No retired teleport, facing swap or Story Midsummer Glide bonus. |
| Mozart | Sonata Drift | Steer the rising glissando; its apex staff platform lasts three seconds. Hard may plan a valid staff landing and use the existing half-cooldown refund, accounting for expiry and return progress; do not treat it as permanent geometry. |
| Cleopatra | Desert Mirage | Use its actual directional travel and collision behavior. No Story Vortex Step discount; Sandstorm Vortex is not a recovery launch. |
| Lincoln | Rail Charge | Horizontal armored travel; obtain necessary height through legal jumps. Armor does not prevent damage or pit KOs. No Story Rail Breaker benefit. |
| Pocahontas | Breeze Glide | Horizontal glide and the baseline double-jump reset; the reset can supply another legal jump within the same plan. Spirit Strike (Special 1) is a candidate mobility Special for Medium/Hard because its design includes a forced diagonal-up dash, but enable it only after confirming aerial legality and the implemented trajectory. Vine Snare is not a recovery move. |

Author a CPU recovery profile per character, linked to canonical ability identifiers. Each usable action needs its input/steering policy, aerial eligibility, resource prerequisites and a deterministic trajectory/landing check using shared movement rules. Reject missing or unvalidated optional Special mappings; fall back to verified jumps/movement instead of treating a slot number as a capability. Do not invent a new movement effect to satisfy an AI profile.

## Validation required before sign-off

Exercise all nine characters on every authored Open stage at all three CPU difficulties. Cover shallow/deep knockback, both gap edges, exhausted jumps, unavailable abilities, Suppression, action locks, blocked blink destinations, moving/expiring supports, interrupted plans, and ledge regrab limits. Also check Closed stages for false recovery triggers.

Record the chosen route, reaction/commit frames, action sequence, resource use and result. Verify Easy actually attempts its simple recovery and keeps its normal reaction delay; verify Medium/Hard never cast a non-mobility Special solely because it occupies slot 2. Check normalized kits and the special cases above, plus seeded replay consistency and sustained-play stall behavior. Keep failures visible for ability/geometry tuning; this document does not claim those tests have run.
