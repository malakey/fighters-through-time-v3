# Rollback state and replay contract — S01

Decision: 2026-09-12. User selected **Option A: define the complete gameplay-state inventory and replay rules now; code examples remain illustrative and online implementation stays deferred to Package 7**.

This is the required design inventory for the currently authored Fighter rules. It is not a verified serializer, shipped networking capability, measured performance result or final binary layout. Binding each requirement to actual runtime fields, including physics-engine state, remains implementation work. A newly authored mechanic must extend this inventory before rollback sign-off.

Related authority: [main design](../design-godot-v7.md), [defensive effects](DEFENSIVE_EFFECTS.md), [temporal state](TEMPORAL_STATE_CONTRACT.md), [Fighter match rules](FIGHTER_MATCH_RULES.md), [CPU recovery](CPU_RECOVERY.md), [CPU combat policy](CPU_COMBAT_POLICY.md) and [HUD](HUD_CONTRACT.md). Preserve their gameplay decisions; serialization does not grant new actions, rewards or recovery benefits.

## Scope and snapshot boundary

Rollback restores **Fighter simulation state**, not a Story checkpoint save, Death Rewind or player-triggered Echo Step. Keep Story-only Time Freeze, Integrity, campaign puzzle state, grids and Story recovery outside the online Fighter payload. Shared systems still obey their mode-specific eligibility. Deterministic local test/replay coverage may exercise CPU controllers without adding CPU participants to an unsupported online mode.

Use the existing end-of-tick convention: `S_T` is the complete state after tick T and its damage, rewards, deaths and phase transitions have resolved. Correcting input for T restores `S_(T-1)`, discards the superseded simulation branch and replays T through the current tick with corrected inputs. Retain an initial pre-first-tick state. The Echo Step ring remains a separate 31-entry history of start-of-tick positions; an end-of-tick snapshot includes the ring/head/validity after that tick's sampling. Do not reinterpret it as post-movement sampling.

At a snapshot boundary, either resolve a queued gameplay operation or include all data required to resume it in deterministic order. Never capture HP from before a hit and its meter/KO flags from afterward. Round transitions, Defy, throw damage, orb claims and effect expiration must be internally consistent.

Every state item must be classified as:
- **Mutable authoritative state:** serialize, restore and include in the canonical gameplay hash.
- **Immutable match content/configuration:** bind through validated stable IDs and matching rules/content versions; include its identity in match compatibility and hash inputs.
- **Derived state:** omit only with an explicit deterministic reconstruction recipe from the first two classes and tests showing identical future outcomes.
- **Presentation or transport state:** keep outside the gameplay hash and prohibit it from deciding gameplay.

Do not call a timer, cached target, random seed, physics contact or pending callback derived merely because it is inconvenient to serialize.

## Required authoritative inventory

The rows are requirements across all active actors/entities, not just Player1/Player2. Optional systems need an explicit inactive/empty state when disabled.

| Category | State that must be restored or deterministically reconstructed |
|---|---|
| Match identity and configuration | Match/session generation; snapshot/protocol/rules/content versions; stage ID and variant; selected fighters and slot/owner mapping; normalized Stock/Time rules, stock preference, timer, orb/hazard toggles and any supported mode-specific modifiers. Local-only handicap must not leak into online. Lock configuration for the match. |
| Match progression | Simulation tick; regulation/countdown/SuddenDeath/finished phase and phase generation; phase clocks and remaining timer; Overtime state or its documented derivation; stocks remaining, stocks lost, frozen regulation totals, living/KO/respawn state, winner/draw and result event identity. Include transition-in-progress, ready/countdown barriers and any confirmed tick-scheduled pause state affecting simulation. |
| Stable identities and lifecycle | Entity IDs/generations, owner/target/parent relationships, source ability/execution IDs, life/stock generations, spawn/despawn requests and ordering, next-ID allocation state and any pool/free-list state that affects future IDs or update order. No live Node reference, memory address or renderer instance ID is a wire identity. |
| Inputs and action recognition | Effective inputs for the tick and each actor's previous held state; pressed/released-edge history, direction/facing decisions, buffered actions and expiry, chord arbitration, double-tap/tap-window state, charged/held input duration and consumed opportunities. Restore input-derived state together with the actor; never reuse later-frame edge detection after a rollback. |
| Body and traversal | Position, linear/other authored motion, facing, grounded/support identity, floor/platform-relative motion, jumps remaining and kit allowances; jump hold/cut state, coyote/input buffers where authored, drop-through exclusions, collision masks, ledge anchor/hang/lockout, roll state/timing/invulnerability, glide/float/blink/charge travel phases and attachment offsets. Geometry or a camera-region parameter used for hit eligibility must be authoritative, not read from local rendering. |
| Physics world | All gameplay colliders/body types/transforms, enabled state, velocities, authored constraints and moving platforms, deterministic collision/filter/order state, and solver/contact/overlap/sleep/warm-start information if it affects later results. An engine cache may be rebuilt only under a tested reconstruction rule. Restoring visible transforms alone is insufficient evidence. |
| Actor state and timing | FSM state and elapsed/remaining phase timers, hitstop, hitstun, shieldstun, daze, getup/armor/invulnerability windows, action locks and cancel availability. Include recovery/landing transitions, pending interrupts and their ordering. An animation player or wall-clock callback cannot be the only owner of an attack's phase. **M05 binding (Package 12 W3b):** the knockdown and get-up sub-phase of `Stunned` is Klotho component **320** `FighterKnockdownComponent` (MaxCount 2; `KnockdownFrames`, `GetUpKind`, `GetUpFrames`, `GetUpDirection`, 16 B, all-zero inactive), snapshot and hash state; the knockdown's invulnerability rides the existing `InvulnerabilityFrames` field. |
| Attacks and ability executions | Ability/content ID, execution generation, combo stage, startup/active/recovery frame, aim/direction/charge, emitted-hitbox/projectile flags, hitbox phase and transforms, resource expenditure and cooldown-start flags, pending chained strikes and per-execution target/contact records. Retain all kit-specific phases, allowances, selected summon modes and per-instance use counters that influence later execution. |
| Grab and throw | Grab attempt/hold/escape/throw phase, counterpart IDs, offsets/facing, phase/hold timers, selected direction, queued release, paired damage-applied and release-applied flags, capture protections and post-release state. Secondary thrown-mob behavior is Story-only; serialize only supported Fighter throw behavior without importing that feature. |
| HP, meter and Rally | Current HP and any mutable effective max HP; Influence Meter and charge/cooldown resources; Echo Pool, drain state, outstanding uncredited victim-damage records and settlement status. Retain damage provenance, actual HP-loss credit and committed amounts so D03f rewards can be recomputed once on the corrected branch. Derived Desperation reads the same authoritative inputs. |
| Defenses and Defy | Block charges, shatter/regen/lockout state and eligible block-event IDs; active defensive effect identity, capacity/hit flag, remaining lifetime, source/type filters and consumed-contact state for effects legally available in the mode. Defy-used, proc identity, awaiting-control phase and remaining D04 protection ticks; associated pending presentation/control-return state. Story-only shield perks remain excluded from Fighter. |
| Statuses, marks and modifiers | Both status slots' type, intensity, duration, source/owner/execution, next tick/cadence, reapplication/slot-generation and cleanup flags; Conductive marks, source/target references and remaining time; any authored stacking/diminishing-stun history or vulnerability modifiers. Include Ultimate-origin tags on descendant damage/status ticks under D03h. |
| Echo Step and combat allowances | All 31 positions, ring head, latest sampled tick, valid count and history generation; armed flag, exact locked destination, activation tick, remaining windup and cooldown; movement/action continuation rules. Include Resonance Momentum refund counts/current cooldown-cycle IDs and any other authored per-life/per-execution resource allowances. |
| Projectiles, zones and summons | Stable type/entity/owner/source IDs; position/motion/aim, phase and lifetime, collision eligibility, hit/re-hit records, pierce/bounce/remaining ammunition, pending emissions and attachments. Preserve whether damage is direct, persistent or Ultimate-origin independently of appearance or the owner's current animation. |
| Persistent combat objects | HP/lifetime, deploy limits and ownership collections, current target and target-selection phase, fire/tick timers, ammo, pending shots, orientation/motion, contact records, paired fence endpoints/network relationships and destruction/expiry progress. Include temporary ability platforms, nests/traps/decoys and their actual authored per-instance allowances. A target cache that changes the next shot is state. |
| Orbs and arena | Orb entity/type/position/lifetime, spawn schedule, pending selections, pickup eligibility and claimed/consumed IDs; stage geometry/destructible state, moving platform/mechanism phase, active/warning/recovery hazard clocks, hit eligibility and per-target tick records. Overtime/SuddenDeath cadence uses the existing rules; phase restoration must not double the rate again. |
| Randomness and CPU | Negotiated initial seed plus each gameplay PRNG stream's current internal state, stable stream identity and draw order; the seed alone cannot resume a used stream. Where a CPU is supported, include controller difficulty/profile, reaction/perception history, next evaluation tick, target/plan/path stage, opportunity IDs and consumed probability checks, action budget and recovery commitment. |
| Event queues and ledgers | Ordered pending damage/healing/spawn/despawn/status/expiry/phase events, future scheduled execution ticks, unique identities, source/target references and payloads; processed-contact/reward/claim flags still needed by live effects. Include any partially completed transaction or eliminate it before the snapshot boundary. |
| Phase reset and respawn | Authoritative respawn timing, position/HP grant applied flags, spawn platform/invulnerability, input cleanup and object cleanup generation. Snapshot F22's entire retained-HP transition, frozen totals, disabled Defy/orbs, fresh action/resources, reset arena and continuing PRNG state atomically. |

For every row, implementation must provide a field-level manifest: component/resource owner, runtime field and type, serializer location, initial/inactive value, hash participation, reconstruction recipe if omitted, version/migration rule and verification case. Audit every value read by the next simulation tick. A missing mapping is blocked coverage, not a default zero or proof that the table is already implemented.

The main-document C# structs are **partial illustrative sketches**. They do not establish a complete binary format, bounded allocations, final component packing or framework API compatibility. Immutable strings/content IDs in a sketch must be mapped to a validated representation; choose layout/capacity limits against actual runtime evidence rather than inventing them here.

## Input history and deterministic replay

Keep received input history and transport receipt/confirmation metadata separate from the restored gameplay state. A rollback must not forget an already received real input. Rebuild the affected effective/predicted input sequence using the existing repeat-previous-input policy and replay with the same deterministic simulation rules. C01c normalizes device bindings/preset recognition into explicit logical action requests, retaining direct-versus-preset origin and component candidates needed for existing fallback. Serialize that input capability in a versioned format; a peer must not apply its own shortcut preferences to another actor's received inputs. Include direct Echo Step without reusing the reserved Dash bit. Do not insert live keyboard/controller samples, wall-clock timers, new CPU random draws outside the simulation, or new network arrival order into replayed ticks.

Use a fixed deterministic order for entities, contacts and queued events. Serialize allocation/order state when it determines that order; unordered collection iteration cannot choose a target, first hit or reward recipient. Physics and script-emitted hits use one consistent contact/event identity scheme.

The current design retains 60 Hz, a maximum seven-frame rollback and a 30-frame input history. Retain enough snapshot states to restore before the earliest replayable input, including boundary/initial states; the 31-position Echo Step ring is independent. Inputs older than available/allowed rollback history must use the authored Package 7 failure/recovery path, never silently clamp to a different frame. The final buffer implementation must demonstrate coverage at wraparound and negotiated input delay.

## Rewards and side effects during replay

Restore gameplay ledgers to the same snapshot as their resources. Discard the superseded branch's later events and recompute them during replay. Do **not** retain an eternal already-processed gameplay set that suppresses a legitimate hit on the corrected branch, and do not add replayed rewards on top of unreverted HP/meter/stocks. A fully restored branch produces the same resource result exactly once within that branch.

Separate simulation events from cosmetic presentation and durable external effects:

| Effect class | Replay rule |
|---|---|
| Gameplay damage, shields, statuses, meter, Rally, orb collection and KO counters | Re-run on the restored branch through normal deterministic rules. Use restored event/contact ledgers to prevent duplicates within that branch. |
| Persistent visual state | Reconcile from the corrected current state: HUD values, statuses, shield/glow presence, actor pose, projectile/zone existence and looping effects. Remove visuals belonging only to discarded events. |
| Short predicted sounds, hit sparks, camera shake and controller rumble | They may present responsively on predicted events, keyed by stable logical event identity. Replaying an unchanged event must not replay its burst, shake, sound or rumble. Stop/correct surviving loops or false visuals when the event disappears; an already heard sound cannot be undone. Changed payloads under an ID require reconciliation, not a fresh gameplay event. |
| Major one-shot announcements and persistent records | Confirm the authoritative event before an irreversible notification or durable write: match result, career counters, achievements and external reporting. Commit once per match/result event identity. A provisional KO may animate, but cannot permanently record a win. Confirmed correction/recovery must not issue a second write. |

Use stable logical event IDs incorporating match/phase generation, simulation tick, source execution, target where applicable and a deterministic event ordinal. Branch corrections can remove/change events; presentation deduplication lives outside rollback gameplay ledgers and cannot decide hit eligibility. Retain presentation identity through the rollback horizon and discard it safely when no longer needed. All audio/VFX subscribers must tolerate replay and teardown without generating new gameplay.

Effects and animations follow simulation decisions. They cannot call gameplay activation a second time from an animation-frame callback during visual restoration. UI settings, resolution, frame rate, audio state and cosmetic PRNG streams are local and excluded from the gameplay hash; they must not influence target selection, hitboxes, RNG draws or timers.

## Serialization, hashing and compatibility

Serialize a canonical bounded representation with explicit field ordering, numeric encoding, array/entity ordering and content IDs. Include every authoritative mutable field and negotiated immutable identity required to reproduce play. Exclude the checksum field itself, presentation/transport metadata and proven derived caches from the gameplay hash. Hash matching simulation ticks under identical rules/content, not current wall-clock state or unrelated prediction points.

Snapshot version, rules/content hashes, schema capabilities and match configuration must pass the existing fail-closed compatibility check before online play. Unknown entity/effect IDs, invalid references, unsupported schema or incomplete payloads must not be guessed into valid gameplay. Validate the whole restoration before replacing live state; no half-loaded scene.

A full-state resync payload, if Package 7 implements it, must cover this same authoritative inventory plus the tick/input context needed to continue. Preserve the existing minimum desync behavior: halt with a visible error rather than continuing silently. Recovery transport, final payload layout and safe bounds remain Package 7 implementation work; this contract adds no online feature to launch.

The main design's serialization target below 0.1 ms per tick and seven-frame replay target within 8 ms are **unmeasured targets for this inventory**. Fixed-point arithmetic and a framework name do not prove whole-game determinism or these costs. Measure snapshot size, save/load/replay time and allocation behavior on supported hardware and cross-platform runs; do not omit necessary state to make a benchmark pass.

## Required verification and completion gate

Compare an uninterrupted reference simulation using the final input stream with executions that repeatedly save, restore and replay corrected predictions. Compare canonical gameplay hashes and future behavior, not screenshots alone. Test every rollback depth through seven frames, ring wrap, multiple corrections, first tick, maximum authored entity counts and effect lifetimes crossing phase boundaries.

Required coverage includes:
- All nine core kits, their multi-stage/multi-hit attacks, emitted objects, recovery/movement states and supported orb effects; disabled Story-only effects remain absent.
- Late corrected press/release/chord/charge inputs, action-buffer expiry, jump/roll/ledge/Echo Step boundaries and body/platform contacts.
- Partial/full defenses, Aegis consumption, poison cadence, damage overkill/Defy, D04 release/protection expiry, D03h delayed Ultimate damage, Rally reclaim/expiry and no double rewards.
- Grabs/throws before and after capture, paired damage and release; stale hit callbacks and overlapping targets.
- Construct targeting/fire/ammo/fence relationships, projectile bounce/pierce/contacts and owner death while surviving objects remain.
- Orb/hazard randomness, warning/tick schedules, repeat replay with identical PRNG state and supported CPU reaction/opportunity history.
- Final regulation tick, simultaneous KOs, unlimited-stock totals, Stock timeout, Overtime, F22 retained-HP Sudden Death and exactly-once confirmed result recording.
- Predicted effects that repeat, disappear or change under correction; no duplicate sounds/rumble/announcements or replay-triggered gameplay.
- Invalid/incompatible snapshots and incomplete resyncs fail visibly without partial installation or extra benefits.

Complete the field manifest and canonical serialization/hash tests before claiming the snapshot complete. Then demonstrate supported-platform deterministic replay and measure budgets. Record build/content revisions, input streams/seeds, affected fields, depth/tick, expected and observed hashes, performance distributions and pending/blocked/pass/fail outcomes. Current status: design inventory specified; all field binding, implementation, runtime determinism and performance validation pending.
