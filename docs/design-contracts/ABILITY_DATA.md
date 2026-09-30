# Ability data contract — A15

Design revision: 2026-09-29. The user selected Option A of ability review item A15: one table per ability family, collected from the approved GDD kit entries, the construct specifications and the resolved review decisions. It adds no new numbers. A cell reading **Unspecified** marks a value the design has not yet decided; fill it only through an explicit design decision, never by copying an unreviewed build value.

**Authority (P04).** The GDD kit entries and adopted contracts define intended behavior; this table mirrors them in one place. When a value changes, update the GDD kit entry, `characters.html` and this table in the same change. Verified `resources/Abilities/*.tres` values describe a build revision and do not override the design; record disagreements in the [design/build ledger](DESIGN_BUILD_DEVIATIONS.md).

**Shared rules.** Frames are 60 Hz. Special cooldowns sit in the 6–14 s band (M08) and receive Resonance Momentum refunds. A blocked ordinary Special costs 2 charges; a Shield-Breaker shatters (A01). Persistent-object hits count as Basic-class (1 charge) and are impulse-free. Venom deals 2 per 1.0 s tick at intensity 1.0, scaling with intensity (C02). Every movement ability shares the universal 5 s cooldown and is provisional under A07. Damage anchors: a special ≈1.5× a full string (band 1.2–1.8×), an Ultimate ≈4–5× a full string (roster band 70–84).

## Specials

| Character | Slot | Ability | Damage | Startup / recovery | Cooldown | Block class | Effect / notes |
|---|---|---|---|---|---|---|---|
| Einstein | S1 | Mass-Energy Conversion (E=mc²) | ≈27 (7 contact + 20 burst in a 1.2-unit radius) | Startup 14, recovery 20 (E05) | 11 s | Special | Straight shot at 12 units/s with no range limit — crosses the stage until it strikes a fighter, terrain or wall; item rotates each use (apple, pocket watch, pipe; cosmetic). Burst inside his own Rift triggers Rift Collapse: Rift ends, targets inside are pulled to its centre (6 frames) and launched upward, 0 damage (E01) |
| Einstein | S2 | Relativity Rift | 3 per 0.5 s tick over 3.0 s (≈18) | Unspecified | 8 s | Special (zone-tick class Unspecified) | Cast up to 5 units ahead (ground-snapped, or mid-air if airborne), 1.5-unit radius, stationary 3 s; TimeDilation 50% (move, jump, animation) while inside, lingering 0.5 s after leaving (E03); Einstein +25% move speed inside; ticks never reclaim Rally (D03g) |
| Joan | S1 | Righteous Smite | 28 | Startup 12, recovery 20 (J02) | 10 s | Special | Ground shockwave rolls ~2.5 units forward, grounded targets only; low knockback, 30 frames hitstun; Radiant Burn +25% damage taken for 3.0 s (damage slot) |
| Joan | S2 | Divine Piercing | 3 thrusts × 8 = 24 | Startup 13, recovery 20 (J03) | 11 s | **Shield-Breaker** | Lunges ~3 units forward during the thrusts — her gap-closer (J01); first absorbed contact shatters once |
| Leonardo | S1 | Golden Ratio | 8 × 3 = 24 | Startup 12, recovery 18 (L02) | 8 s | Special | Self-centred spiral expands to a 2.0-unit radius over 3 ticks 0.3 s apart; light outward knockback; space-clearing burst |
| Leonardo | S2 | Clockwork Turret | Bolts 6 × up to 4 = 24 | Unspecified | 10 s | Construct bolts: Basic | Placed at his feet; 20 HP; straight bolts at 12 units/s at the nearest target in line of sight, one per 2.0 s while a target is in range; 4 bolts (the glide bonus bolt spends one) or a 15 s idle cap; max 1 active (L03) |
| Tesla | S1 | Tesla Coil / Chain Lightning | Arc 5 per 2.0 s; linked fence 4 per 1.0 s | Unspecified | 8 s | Construct: Basic | Placed at his feet; arcs reach 4 units (T02); 25 HP, 30 s life, max 2; link range 8.0 units; fence applies Static Charge + 1.5 s Conductive (F07) |
| Tesla | S2 | Lorentz Pulse | 20 | Startup 14, recovery 20 (T02) | 11 s | Special | 2.5-unit radial pulse around him; Root 1.0 s (A05); on a Conductive-marked target, each of his coils within 8 units fires an 8-damage chain arc at it (max 16 — T01) |
| Shakespeare | S1 | Yorick's Lament | ≈21 (5 contact + 16 wave) | Startup 10, recovery 14 (poke band) | 7 s | Special | Skull thrown straight at 10 units/s up to 8 units, bursting on a fighter, terrain or max range into a 1.5-unit wave; hits airborne targets (S03); TimeDilation 30% for 2.5 s, applied once |
| Shakespeare | S2 | The Tempest | 0 | Startup 8, recovery 16 (S02) | 7 s | Windbox — not a hit; no block interaction (S01) | 2.0-unit radius; pushes opponents ~3 units outward over 12 frames; lifts Shakespeare ~2.5 units; no invulnerability |
| Mozart | S1 | Requiem Chord | 16 total (contact 4 + 3 pulses × 4) | Startup 10, recovery 14 (poke band) | 7 s | Special | Straight shot at 14 units/s with no range limit, bursting on a fighter, terrain or wall into 3 pulses in a 1.2-unit radius over 0.3 s; hits airborne (M03); a hit shortens Fortissimo Wave's remaining cooldown by 2 s, once per execution (M02) |
| Mozart | S2 | Fortissimo Wave | 24 | Startup 16; recovery in the 20–24 band, exact Unspecified | 12 s | Special | Straight wall of sound 1.5 units tall, 4 units/s along the ground for 6 units (M01); roster's heaviest horizontal pushback |
| Cleopatra | S1 | Serpent Nest | Bite 6 per 1.0 s, plus Venom 2 per tick × 4 s = 8 (C02) | Unspecified | 10 s | Construct: Basic | Placed at her feet, 2.0 units wide (C01); 15 HP, 12 s life, max 1; Venom 4 s (damage slot) plus hitstun |
| Cleopatra | S2 | Sandstorm Vortex | 4 per 0.4 s tick × 5 = 20 | Unspecified | 9 s | Special (zone-tick class Unspecified) | Cast up to 5 units ahead, ground-snapped; 1.8-unit radius; pulls toward its centre at 3 units/s (C01); TimeDilation 40% for 2.0 s; final tick launches; no tick reclaims Rally (D03g) |
| Lincoln | S1 | The Emancipator | 40 | Startup 16; recovery in the 20–24 band, exact Unspecified | 13 s | **Shield-Breaker** | Ground shockwave travels 5 units at 10 units/s, grounded targets only (LN03); launches upward; roster's biggest single hit |
| Lincoln | S2 | Splitting Strike | 36 | Startup 14; recovery in the 20–24 band, exact Unspecified | 12 s | **Shield-Breaker** | 2.2-unit overhead arc hitting grounded and airborne targets (LN03); spikes airborne targets (Story Kinetic Splitting adds a ground bounce) |
| Tubman | S1 | Conductor's Call | 18 | Startup 12, recovery 18 | 9 s | Special | Spectral Union scouts rush 5 units along the ground; push; grounded targets |
| Tubman | S2 | Foresight | Counter strike 20 | Startup 4, 20-frame window, whiff recovery 24 | 10 s | Special (counter strike) | Nullifies one strike or projectile and sidesteps; answers within 2.5 units with a launching strike; grabs and Ultimates beat it |

## Movement abilities

All share the 5 s cooldown; every value is provisional under A07.

| Character | Ability | Travel | Distinguishing rule |
|---|---|---|---|
| Einstein | Relativity Warp | Spacetime fold: 10-frame startup, instant relocation up to 4 units, no travel (E04) | Crosses anything in between; destination shortens to the farthest valid point; then a float window steered with Up/Down; fast-fall cancels it |
| Joan | Ascendant Wings | Rising slash-leap; height Unspecified | Held Wing-Dive up to 1 s; Attack cancels it into her aerial string (A08) |
| Leonardo | Ornithopter Flight | Vertical boost, then glide up to 3 s | Can fire one turret bolt mid-glide if a turret is deployed (spends one turret bolt — L03) |
| Tesla | Lightning Blink | 6 startup / 12-frame translation over 3.0 units / 10 recovery | Passes through projectiles during the translation |
| Shakespeare | Prospero's Flight | Gust burst ≈4 units forward, 2.5 up over 20 frames | No sustained glide (A08); no teleport or invulnerability (F15) |
| Mozart | Sonata Drift | Directable rising glissando, ~3 units (M04) | Leaves a 3 s, 2.0-unit-wide staff platform; landing on his own refunds half the cooldown, once per airtime (M04) |
| Cleopatra | Desert Mirage | Sand rush 4 units in 8 directions over 15 frames, passing through opponents (C03) | Leaves a 1 s sand decoy that enemies and the CPU target |
| Lincoln | Rail Charge | Armored charge 5 units over 30 frames; contact 6 damage, moderate horizontal knockback, stops (LN02) | Armored only during travel; projectile breaking is Story-only (Rail Breaker) |
| Tubman | North Star Leap | Guided leap 4 units in 8 directions over 18 frames | Snaps to ledges from 0.5 units farther than normal |

## Ultimates

Every Ultimate costs 100 meter, is unblockable (eligible Temporal Aegis and HP barriers still absorb — D03b), bypasses enemy damage reduction and hyper-armor, and earns its caster no meter (D03h). In Fighter Mode each opens with an avoidable activation strike (A02; provisional 20 wind-up / 10 active / 45 whiff recovery); the cinematic plays only on contact. Story Mode keeps the screen-clearing versions.

| Character | Ultimate | Total damage | Fighter activation strike | Notes |
|---|---|---|---|---|
| Einstein | The Cosmological Constant | 78 (6 × 10 pull + 18 launch — E06) | Chalk singularity orb flicked straight ahead, 6-unit range | Black hole pulls in, multi-hit, final launch toward the blast zone |
| Joan | The Grand Crusade | 76 (8 × 7 trample + 20 final charge — J05) | Banner charge ~5 units forward, lance-style | Directional cavalry stampede; carries victims toward the blast zone |
| Leonardo | The Vitruvian Matrix | 74 (5 × 10 bombardment + 24 explosion — L04) | Geometric sphere thrown straight ahead, 7-unit range | Traps the victim in a blueprint dimension; final explosion |
| Tesla | Wardenclyffe Cataclysm | 70 (5 × 10 column + 20) + 5 per detonated coil, max 80 (T03) | Straight teleforce beam, 6-unit range | Pulls toward the center; AC column; detonates active coils |
| Shakespeare | All the World's a Stage | 76 (3 × 14 phantom strikes + 34 Hamlet finale — S04) | Straight ink stroke from his quill, 6-unit range | Globe Theatre phantoms deliver sequential strikes |
| Mozart | Symphony of Sorrow | 80 (8 × 7 keys + 24 finale — M05) | Straight baton bolt of sound, 6-unit range | Piano keys rain like meteors |
| Cleopatra | Wrath of the Nile | 78 (12 slam + 6 × 9 cobras + 12 Venom at intensity 2.0 for 3 s — C04) | Spectral asp lunging straight 6 units | Sarcophagus and cobra swarm; heavy poison, whose ticks earn no meter |
| Lincoln | Union Indestructible | 5 × 14 = 70 | Fence-line wave racing 5 units along the ground, grounded targets only (LN05) | Fence barriers trap, then a leaping ground smash with heavy knockback |
| Tubman | The Freedom Line | 76 (7 × 8 + 20 final rush) | Straight lantern beam, 6-unit range | Spectral train of lantern light carries opponents along its line |

## Open values

Resolve these before the post-Open-stage Fighter tuning pass (every Ultimate total and activation strike is now set): startup and recovery for every special not listed; the block class of zone ticks (Relativity Rift, Sandstorm Vortex); and the leap height of Ascendant Wings.
