# Package 3 kit audit: 36 ability slots vs design Section 5

Audit date: 2026-08-06. Source of truth: `design-godot.md` Section 5 (lines ~1787-2283), Section 4 stat/persistent-object tables, and Section 2 Resonance economy. Current behavior read from `scripts/Characters/Abilities/`, `scripts/Combat/BaseSpecial.cs`, `scripts/FighterSim/`, and `resources/Abilities/`.

Status legend per slot:

- `canonical` - both Story and Fighter execute the character's designed mechanics.
- `story-canonical` - Story matches design; Fighter still uses the generic loadout path.
- `sketch` - character-specific Story code exists but diverges from design numbers/rules or silently no-ops; Fighter generic.
- `generic` - behaves as a generic melee/projectile/zone approximation in both modes.

## Cross-cutting gaps (apply to many slots)

| ID | Gap | Resolution |
|---|---|---|
| X1 | `BaseSpecial` ignores `AbilityData.StartupFrames/ActiveFrames/RecoveryFrames`; every subclass hardcodes `PhaseTimer` constants, so `.tres` timing is dead data. | Read authored frames in `TryExecute`/phase advance; subclasses override only when design requires phase-specific logic. Done 2026-08-06. |
| X2 | Movement executors hardcode cooldowns/distances instead of `MovementAbilityData.CooldownDuration/DistanceMoved/MovementDuration/MovementSpeed`. | Consume data fields during each character's kit pass. |
| X3 | No `.tres` sets `ProjectileScene`/`PersistentObjectScene`; Tesla Coil, Leonardo Turret, and Mozart Sonata Drift silently no-op in Story. | Author placeholder construct scenes per Package 0 contract and wire them into resources. |
| X4 | Fighter sim has no Area/zone entity: `AbilityExecutionType.Area` (Rift, Golden Ratio, Vortex, Lorentz, Tempest, Emancipator) degrades to a melee-range hit. | Done 2026-08-06: `FighterZoneComponent`/`FighterZoneSystem` with impulse-free pulses (no knockback/hitstun/shield drain), snapshot/rollback coverage, and driver proxies; all Area specials route through it. Per-character behaviors beyond damage/status/owner-speed (vortex pull, tempest lift) land in their kit passes. |
| X5 | Fighter movement abilities collapse to 3 crude behaviors (upward velocity, horizontal burst, position shift); no glide duration, no jump reset verification, no warp float. | Extend `FighterAbilityEntitySystem.ApplyMovement` with per-`MovementType` deterministic rules during kit passes. Warp done 2026-08-06: input-directional teleport (including vertical) plus 60-frame reduced-gravity float. |
| X6 | Zero of the 27 Resonance major perks execute; `ResonanceProgression.HasUnlockedAbilityModifier` is never called. Minor keys beyond the 6 resolved stat keys also do nothing. | Story-only perk hook on `PlayerController` (done 2026-08-06); wire each perk in its character pass; extend `Resolve()` for remaining minor keys where behavior exists. |
| X7 | Ultimates are melee-range generic hits in Fighter; no cinematic/multi-hit structure. | Acceptable interim; revisit after all specials are canonical (ultimate presentation is Package 8). |

## Per-character slot audit

### Einstein (`einstein`) - pattern-setting vertical slice, converted 2026-08-06

| Slot | Canonical design | Pre-audit state | Status |
|---|---|---|---|
| S1 Mass-Energy Conversion | Heavy projectile, wind-up, contact damage then radiant burst | Story: placeholder projectile, no burst. Fighter: generic projectile. | canonical (Story: 1/3 contact + full-damage AoE burst on impact; Fighter: single full-damage deterministic projectile, equivalent total in 1v1) |
| S2 Relativity Rift | Zone: TimeDilation -50% speed/jump/anim, 1.5 dmg per 0.5 s tick, 3 s; Einstein inside gains +25% move speed in BOTH modes | Story: zone + status, no self-buff. Fighter: melee-range hit. | canonical |
| Move Relativity Warp | 0.2 s warp, cancel into 1.0 s float, 3 s cap, air ok | Story: hardcoded 150 px velocity warp, no float. Fighter: upward velocity. | canonical |
| Ult Cosmological Constant | Screen-clear multi-hit black hole, launch | Story: multi-hit sketch. Fighter: generic. | sketch (see X7) |

Perks: Event Horizon (+20% E=mc2 burst damage vs TimeDilation-afflicted targets; the enemy-projectile-slow clause is deferred until zones interact with projectiles), Critical Mass (E=mc2 burst applies RadiantBurn 3 s), Quantum Entanglement (teleport backward on block break, replaces the shove). Wired Story-only 2026-08-06 via `PlayerController.HasStoryPerk`.

### Joan (`joan`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Righteous Smite | 14 dmg ground shockwave, RadiantBurn 3 s | Story: placeholder projectile w/ status. Fighter: generic. | sketch |
| S2 Divine Piercing | 12 total rapid thrusts, depletes 2 block charges | Story: bespoke melee + block depletion. Fighter: generic special. | story-canonical |
| Move Ascendant Wings | Rising leap, hold for 3 s glide | Story: bespoke leap+glide. Fighter: crude. | story-canonical (X5) |
| Ult Grand Crusade | Directional cavalry multi-hit | Sketch both modes. | sketch |

Perks: Unstoppable Crusade (hyper-armor on Smite active + 1.5 s), Zealous Vigor (final cleave heals 5% missing HP), Shield of Orleans (blocked damage builds meter 25% faster). Not wired.

### Leonardo (`leonardo`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Golden Ratio | Expanding spiral, 10 dmg x 3 ticks, radial knockback | Story: placeholder zone. Fighter: melee-range (X4). | sketch |
| S2 Clockwork Turret | 20 HP, 15 s or 3 bolts, max 1, bolts 5 dmg / 2 s, range 30 | Story: NO-OPs without scene (X3). Fighter: persistent type 2 works. | sketch |
| Move Ornithopter Flight | Boost + 3 s glide | Story bespoke; Fighter crude (X5). | story-canonical |
| Ult Vitruvian Matrix | Trap + bombardment multi-hit | Sketch both modes. | sketch |

Perks: Master Stroke (+15% spiral dmg, pull toward center), Clockwork Overdrive (5 bolts), Daedalus Wings (steam trail + glide-cancel dive). Not wired.

### Lincoln (`lincoln`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 The Emancipator | Forward floor shockwave, knock up, depletes 2 block charges | Story: placeholder area. Fighter: melee-range (X4). | sketch |
| S2 Splitting Strike | 18 dmg overhead, spikes airborne down, shatters shields instantly | Story: bespoke. Fighter: generic. | story-canonical |
| Move Rail Charge | 3 s armored forward charge (damage yes, hitstun no) | Story: bespoke w/ hyper-armor stub. Fighter: crude (X5). | sketch |
| Ult Union Indestructible | Fence trap + smash | Sketch both modes. | sketch |

Perks: Executive Order (shockwave +50% travel, +20% dmg), Homestead Bulwark (charge hit grants 3 s hyper-armor), Kinetic Splitting (combo hit 3 shatters shields). Not wired.

### Cleopatra (`cleopatra`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Serpent Nest | 12 s / 15 HP / max 1 nest; bite = Root 1 s + Venom 4 s | Story: placeholder zone. Fighter: persistent type 3 generic spec. | sketch |
| S2 Sandstorm Vortex | Pull to center, 2 dmg x 5 ticks / 0.4 s, TimeDilation -40% 2 s | Story: bespoke pull+ticks. Fighter: melee-range (X4). | story-canonical |
| Move Desert Mirage | Sand rush/teleport, air ok, 3 s cap | Story: instant teleport. Fighter: position shift. | sketch |
| Ult Wrath of the Nile | Sandstorm multi-hit + heavy Venom | Sketch both modes. | sketch |

Perks: Asp's Bite (Venom x2 vs airborne), Quicksand Grip (vortex targets rooted 1 s on Mirage cast), Royal Aegis (10% max-HP shield on Mirage). Not wired.

### Tesla (`tesla`) - converted 2026-08-06

| Slot | Canonical design | Pre-conversion state | Status |
|---|---|---|---|
| S1 Tesla Coil | 25 HP, 30 s, max 2; arcs 5 dmg / 2 s; linked fence 8 dmg / 0.5 s within 8 units + StaticCharge | Story: NO-OPed without scene (X3). Fighter: persistent type 1, no linking. | canonical (authored `scenes/constructs/TeslaCoil.tscn` construct: damageable 25 HP, instant-strike arcs, fence link at 480 px; Fighter: deterministic fence pass in `FighterPersistentObjectSystem`, 8 dmg/0.5 s + StaticCharge, snapshot/rollback-safe via `LinkTickFramesRemaining`) |
| S2 Lorentz Pulse | Radial Root 2 s; chains lightning to coils if target has StaticCharge | Story: bespoke, players-only status loop. Fighter: melee-range (X4). | canonical (Story: hurtbox-query radial pulse hitting enemies, StaticCharge read before Root replaces it, per-coil chain strikes; Fighter: owner-centered one-pulse zone type 52 with +5/coil chain damage vs primed targets) |
| Move Lightning Blink | Blink, <= 1 s, air recovery | Story: hardcoded velocity blink. Fighter: crude. | canonical (Story consumes authored MovementAbilityData with 1 s cap; Fighter input-directional blink already data-driven, covered by tests) |
| Ult Wardenclyffe Cataclysm | Pull + column + all coils explode | Sketch both modes. | sketch (see X7; coil chain-explosion works against the authored construct) |

Perks: Resonant Overdrive (+5 s coil lifetime, 25% faster arcs), Lorentz Attraction (pull-then-root, +1 s), Wardenclyffe Shield (recharging 15% max-HP shield near a live coil, via the new `PlayerController` Story-shield support). All three wired Story-only 2026-08-06 through `HasStoryPerk`.

Note: design Section 5 lists the coil arc `damageTickInterval` as 0.5 s while the dedicated Section 4 coil specification says 5 HP every 2.0 s; the conversion follows the Section 4 spec (the 0.5 s interval applies to the linked fence), consistent with this audit's original reading.

### Shakespeare (`shakespeare`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Yorick's Lament | Rolling skull, sonic wave, TimeDilation 30% 2.5 s | Story: placeholder projectile + impact AoE. Fighter: generic projectile. | sketch |
| S2 The Tempest | Wind push away + self-lift | Story: bespoke push/lift. Fighter: melee-range zero-damage (X4). | story-canonical |
| Move Prospero's Flight | Gust forward+up, 3 s glide | Story bespoke; Fighter crude (X5). | story-canonical |
| Ult All the World's a Stage | Sequential phantom strikes | Sketch both modes. | sketch |

Perks: Macbeth's Curse (Lament also applies Venom 1 s tick / 3 s), Midsummer Glide (8 dmg glide-through, +20% glide speed), Henry's Bastion (block summons 10% max-HP phantom shield). Not wired.

### Mozart (`mozart`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Requiem Chord | Note projectile bursting into multi-hit shockwave | Story: projectile + impact shockwave sketch. Fighter: generic projectile. | sketch |
| S2 Fortissimo Wave | 12 dmg full-screen forward wave, heavy pushback | Story: placeholder projectile. Fighter: generic projectile. | sketch |
| Move Sonata Drift | Deploys 3 s staff platform to run on | Story: NO-OPs without scene (X3). Fighter: persistent type 5 spawn. | sketch |
| Ult Symphony of Sorrow | Hover + piano-key meteors | Sketch both modes. | sketch |

Perks: Virtuoso Dash (+20% speed on staff, ranged-projectile immunity), Requiem Crescendo (second 50% shockwave), Rest Shield (1.5 s still/block bubble absorbs physical projectiles). Not wired.

### Pocahontas (`pocahontas`)

| Slot | Canonical design | Current state | Status |
|---|---|---|---|
| S1 Spirit Strike | 14 dmg spectral eagle diagonal swoop, stagger | Story: bespoke melee swoop. Fighter: generic. | story-canonical |
| S2 Vine Snare | Pod -> vines; 10 s / 15 HP / max 2; Root 1.5 s + light dmg | Story: placeholder zone. Fighter: persistent type 4. | sketch |
| Move Breeze Glide | Dash + 3 s glide, resets double jump | Story bespoke incl. jump reset; Fighter crude (X5). | story-canonical |
| Ult Tidewater Tempest | Spirit storm multi-hit | Sketch both modes. | sketch |

Perks: Thorn Snare (rooted enemies take continuous damage), Tornado Lift (glide start launches nearby enemies), Leaf Barrier (10% max-HP shield on glide start). Not wired.

## Conversion order

Einstein is the pattern-setting slice (complete). Tesla is the construct-pattern slice (complete 2026-08-06): `TeslaCoilNode` + `scenes/constructs/TeslaCoil.tscn` set the authored-construct pattern (contract-conformant scene, damageable HP, instant-strike attacks via hurtbox queries, pooled reset, rewind freeze), and the deterministic fence/chain passes set the Fighter-side pattern for construct interactions. Remaining passes, one character each, Story + Fighter + 3 perks + tests per pass:

1. ~~Tesla~~ (complete: persistent constructs, linking, StaticCharge synergies; X3 unblocked for coils)
2. Leonardo (turret construct, spiral zone)
3. Cleopatra (nest construct, vortex pull zone)
4. Pocahontas (snare construct, glide rules)
5. Joan (melee/hyper-armor perks)
6. Lincoln (armored charge, shield-shatter rules)
7. Shakespeare (skull projectile, tempest zone, block perk)
8. Mozart (platform construct, projectile shockwaves, projectile-immunity perk)

Ultimates stay as structured sketches until all specials are canonical (X7), then get a dedicated pass.
