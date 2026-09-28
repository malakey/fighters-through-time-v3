# Legacy Level (4A) per-hero specification template — M22

Decision: 2026-09-26 (user selected "Template only"). This file is the per-hero dossier index that the main design's Level 4A section refers to. It defines the required columns and acceptance rules for each of the nine Legacy Levels. **Every row is intentionally unfilled:** content is authored during level design and must be approved before the row's status changes from *To author*. Nothing here claims that any 4A scene, puzzle or boss exists.

Governing rules: [main design — Level 4A](../design-godot-v7.md), [Legacy checkpoints (F12)](LEGACY_CHECKPOINTS.md), [campaign validation (V01, E01)](CAMPAIGN_VALIDATION.md), [narrative decisions (N02, N03)](NARRATIVE_RESOLUTION.md), [dust ledger (F05)](DUST_ECONOMY.md) and [production scope (P01)](PRODUCTION_SCOPE.md).

## Per-hero table

| Hero | Kit reference (Movement / S1 / S2 / Ultimate) | Nexus setting and threatened outcome (N02) | Movement gate | S1 puzzle | S2 puzzle | Ultimate set-piece target (F04) | Boss: name, origin, 2 phases, HP band | Standard/elite mob roster | Status |
|---|---|---|---|---|---|---|---|---|---|
| Joan of Arc | Ascendant Wings / Righteous Smite / Divine Piercing / The Grand Crusade | Vanguard field (not Orléans' battlements) | — | — | — | — | — | — | To author (first prototype per P01) |
| Leonardo da Vinci | Ornithopter Flight / Golden Ratio / Clockwork Turret / The Vitruvian Matrix | Florence nexus wound separate from Level 1 | — | — | — | — | — | — | To author |
| Nikola Tesla | Lightning Blink / Tesla Coil (Chain Lightning) / Lorentz Pulse / Wardenclyffe Cataclysm | Chicago-era nexus wound separate from Level 3 | — | — | — | — | — | — | To author |
| William Shakespeare | Prospero's Flight / Yorick's Lament / The Tempest / All the World's a Stage | Globe-era nexus wound separate from Level 10 | — | — | — | — | — | — | To author |
| Albert Einstein | Relativity Warp / E=mc² / Relativity Rift / The Cosmological Constant | Princeton after the beam | — | — | — | — | — | — | To author |
| Wolfgang Amadeus Mozart | Sonata Drift / Requiem Chord / Fortissimo Wave / Symphony of Sorrow | Vienna concert hall | — | — | — | — | — | — | To author |
| Cleopatra | Desert Mirage / Serpent Nest / Sandstorm Vortex / Wrath of the Nile | Alexandria nexus wound separate from Level 8 | — | — | — | — | — | — | To author |
| Abraham Lincoln | Rail Charge / The Emancipator / Splitting Strike / Union Indestructible | Gettysburg-era nexus wound separate from Level 11 | — | — | — | — | — | — | To author |
| Pocahontas | Breeze Glide / Spirit Strike / Vine Snare / Tidewater Tempest | Tidewater | — | — | — | — | — | — | To author |

## Column requirements

- **Movement gate:** exactly one required traversal gate that the hero's *baseline* movement ability clears. It must never require a purchased grid node (Wings Refresh, Rail Breaker, Vortex Step and similar are Story purchases), and must not depend on meter.
- **S1 / S2 puzzles:** one required puzzle per Special. Declare the accepted puzzle props and actions under V01c (designated props, plate weights, beam networks). Zero-damage or non-projectile Specials need a non-damage target: for example, The Tempest acts on a movable or wind-driven prop, and Divine Piercing's in-place thrust needs a target placed within its reach. Constructs may operate only mechanisms that explicitly accept them.
- **Ultimate set-piece:** the designated target resolved through the Nexus Resonance Source (F04) at any meter value, reward-free, retryable, and disabled once solved.
- **Boss:** either a figure from the hero's own history bent by the Unbound, or an Unbound Overseer wielding a stolen artefact of the hero's era. Two phases under the standard boss rules and the "never just a speed multiplier" phase rule; `phaseTrigger` per M19. State an HP band relative to the Level 4 and Level 5 bosses, before difficulty scaling.
- **Mob roster:** reuse the shared-era roster where the home era is a shared level. For Princeton, Vienna and Tidewater, name the era roster to author or an approved reuse. The single scripted Eraser encounter (F12) is additional.
- **Status values:** *To author* → *Drafted* → *Approved* (user decision recorded with date) → *Prototyped* → *Validated*. Only *Approved* rows are design of record.

## Acceptance rules for every row

1. The route order follows F12: Entry → kit gates and the independent Eraser encounter → late Font approach → PreBoss → boss. Exactly two checkpoints.
2. Each of the four kit requirements is used once and is completable with zero meter and any legal grid allocation. Spending meter or recoveries before a gate cannot soft-lock it (F04, V01).
3. The nexus outcome, wound and siphon mapping are distinct from the shared level's sealed outcome (N02). The larger personal homecoming lives here, not in the shared level (N03).
4. Economy uses the fixed 4A ledger row: 15 required encounter dust, 25 boss dust, 10 optional dust (F05). Integrity par follows V01a with its own benchmark per variant.
5. Production records its reuse and new work in the P01 ledger before an estimate is accepted.
