# Kit pass: lingering abilities (2026-10-31)

The user's 28 decisions are in `docs/recon-2026-10-31/DECISIONS_kit_lingering.md` (rounds 1â€“7). The recon dossiers are in `docs/recon-2026-10-31-kit/`: A constructs, B zones and statuses, C combo rewards, D tests and docs. All of it was measured at `main` `ce813449`, baseline **3879**. Read the decisions, your workstream's dossier, and Â§1â€“Â§3 here before you edit anything. This plan does not repeat what the dossiers say. Where this plan and a dossier disagree, this plan wins. Where this plan and the user's decisions disagree, the decisions win; stop and report it.

## Â§0 What this package does

Every lingering ability gets a job: **hold**, **cash out** or **deny**.
- **Constructs** (Tesla coil, Leonardo turret, Cleopatra serpent nest) keep a weak, slow attack of their own. They gain **charges**: a strong **synced strike** fires each time the hero lands a hit on a target in the construct's reach. A construct fizzles when its charges or its lifetime run out.
- **Tesla's Lorentz Pulse** discharges his coils.
- **The Vortex** pulls harder than anyone can run. Only a roll or an air dodge escapes.
- **The Rift** is bigger, and Einstein's finisher collapses it.
- **Golden Ratio** pulls its target in.
- **The slow** is heavier and lingers.
- **Venom** hits harder and blocks healing.

Both modes get the same mechanics, with numbers tunable per mode. Ultimates and instant Specials are out of scope. Mozart's staff is unchanged.

## Â§1 Standing decisions (orchestrator; provisional where marked)

1. **"Counts as combo."** D1a stays the only definition of "landed": damage > 0, landed or absorbed.
   - A new orthogonal marker carries the rest: Story `HitPayload.CountsAsCombo` (bool), and in the sim a bool on the hit contract or intent passed into hit resolution.
   - It is set only on: synced construct strikes, the Lorentz discharge burst, the Rift collapse hit, the Vortex final launch, and Golden Ratio ticks.
   - The marker gates exactly two things:
     - **D3 arming**, only when it falls inside the hero's own Special cast and is attributed to that cast (the existing cast-attribution rules: `SpecialCastId`, `ExecutionSerial`).
     - **A new Momentum payout.**
   - It does NOT set the D1 swing latch C. It does NOT give Rally echo. It adds no hitstop beyond what the delivery already has. It does not change `HitDelivery`.
   - Ordinary zone ticks and a construct's own attack never carry it.
2. **Momentum payout (provisional).** Use the existing unit scale (7200 = 1 pip). Pay once per event, never per body.

   | Event | Pay | Units | Guard |
   |---|---|---|---|
   | Detonation (Lorentz discharge, Rift collapse, Vortex final launch) | 0.2 pip | 1440 | once per detonation |
   | Synced construct strike | 0.05 pip | 360 | once per strike |
   | Golden Ratio | 0.05 pip | 360 | once per cast |

   Bosses count, as basic swings do. A blocked, refused, warded or zero-damage contact pays nothing.
3. **Synced strikes.**
   - **Trigger:** the owner's landed (D1a) **basic hit** (string, air strike, Up-Attack, Down-Air, ledge attack) or **direct Special hit** (`DirectHit` delivery, not Ultimate-origin), on a target within the construct's synced reach.
   - **Never triggers a strike:** construct hits, ticks, other strikes, throws, Ultimates, the Lorentz discharge. This rules out loops.
   - **Count:** once per landed hit, per construct. Two coils in reach both fire. Each strike spends one charge.
   - **Cost:** a strike carries `CountsAsCombo`, so it earns 0.05 Momentum (Â§1.2).
   - **Damage routing:** goes through the Story damage composition (`StoryAbilityDamage*`). `KitAlignmentContractTests` must pass.
   - **Hitstun and launches:** real hitstun, applied with `max(current, strike)`. It never cancels or replaces a launch or a tumble (dossier A Â§5 risk).
   - **Knockback:** none. **Hitstop:** none (the triggering hit already froze both parties).
   - **Story mobs:** exempt from S2 chain proration (no dim) and from standard poise. The elite V7.4 budget still charges. Bosses ignore hitstun as they do now.
4. **Charges are spent only by synced strikes, the Lorentz discharge and the turret's glide bolt.** Own attacks never spend them.
   - At 0 charges the construct fizzles, through the existing destroy path.
   - Tesla's Cataclysm Ultimate still detonates every coil, whatever its charges.
   - Charges persist through Time Freeze and the death rewind (constructs are not rewound) and reset on pool reuse.
5. **Sim timing.**
   - Hit resolution records an owed strike on the attacker (310 `SpareInt1`, or grow 303; never allocate a component ID). The target is the opponent in 1v1.
   - The persistent-object system consumes it at the top of its own update, the same tick. Zone-pulse hits (Golden Ratio) resolve the next tick, which is acceptable.
   - Strike presentation in the Fighter driver is edge-detected on a short forward step of a counter, with identity dedupe (the Defy pattern), so a rollback never replays it.
   - **310 `SpareInt0` is reserved for another session's grab WIP. Do not use it.**
   - No float under `scripts/FighterSim/`. A `KLOTHO_DET002` warning is a bug.
6. **Synced reach is a 2D circle in both modes**, as a fixed-point squared distance in the sim. Today the sim's coil and turret reach tests x only; the synced strike uses the circle. Own attacks may keep their current rule.
7. **The slow (decisions 8, 21, 23).**
   - Hero-kit Time Dilation sources use **intensity 1.2**, which gives Ã—0.4 move and jump through the existing formula `max(0.1, 1 âˆ’ 0.5Â·I)`. The formula is unchanged.
   - Enemy ability slows keep their authored intensities.
   - **One intensity for every hero slow.** Only the duration varies, so a refresh is never refused by the stronger-wins rule.
   - **Linger (provisional): 1.75 s** after the source stops refreshing. The Rift refreshes every frame inside it with duration 1.75 s. The Vortex applies it once, when a victim leaves its area or the Vortex ends. Yorick applies 1.75 s on hit.
   - **Bosses (provisional):** a Time Dilation slow on a boss is capped at Ã—0.7 move (multiplier floor 0.7), in `BossController` only.
8. **Venom (decisions 7, 11, 28).**
   - **Damage (provisional):** 3 HP Ã— intensity per second, from one constant. Today four literal copies hold 2 (dossier B Â§3); replace them all.
   - **Healing block:** while Venom is active, block every heal except Defy's. In Story: `HealStory` and its callers, Rally reclaim, healing orbs, the Restoration Font, the checkpoint Mending heal.
     - A blocked Rally reclaim burns no pool. Order the burns after the gate (Zealous Vigor, the reclaim).
     - A Font channel completed while poisoned keeps its pending heal and applies it once Venom ends. Record that as provisional.
     - In the sim: Rally reclaim and the orb heal are blocked; the Defy heal is not.
   - The block applies to every Venom source.
9. **Numbers live once**, in a new pure rulebook `scripts/Combat/LingeringKitRules.cs`: no Godot types, ints and doubles only, every value commented as provisional. The sim reads it through `scripts/FighterSim/FighterLingeringKitRules.cs`, which converts to FP64 with integer rationals, the `FighterMeterRules` pattern. Authored per-ability values stay in their `.tres`, and `.tres` values are shared by both modes. A per-mode difference is a named pair in the rulebook (`â€¦Story`, `â€¦Sim`).
10. **Retire no translation key**, and keep every ability ID. Update the English description values (Â§4 KL-CLOSE). Run `--import` and commit `en.en.translation` once, in the closeout only.
11. **Test discipline.**
    - Every new or changed rule gets a case that fails on the old code.
    - Changed pins are rewritten in place and listed in your handoff with the reason.
    - Report your exact `Total:` delta.
    - Hold the GdUnit lock (`mkdir "D:/Projects/.ftt-gdunit-lock"`; retry while it exists; `rmdir` after the run, even on failure). Drain `testhost` / `*_console` Godot orphans first.
    - Never run GdUnit without the lock: another session is testing in the main checkout.
    - Read `Total:` against the baseline plus your declared delta (CLAUDE.md's failure signatures).
12. **Never touch** `.godot/`, the main checkout's working tree, or `scripts/FighterSim/FighterSimulationComponents.cs` beyond the minimal field change Â§1.5 allows. Since 2026-10-31 another session has had uncommitted grab and throw work in the main checkout: `BasicComboRules`, `PlayerController`, `EnemyController`, `FighterGrabRules`, `FighterSimulationComponents`, `FighterSimulationDriver`, `FighterSimulationSystems`, `en.csv`. Keep your edits to those files small and additive, in clearly separated regions, so the eventual merge with that work stays mechanical. List every hunk you made in them in your handoff.

## Â§2 Provisional numbers (all in `LingeringKitRules` unless marked `.tres`)

| Item | Story | Sim | Notes |
|---|---|---|---|
| **Charges** | coil 3, turret 4 (+1 with Clockwork Overdrive), nest 3 | same | |
| **Coil own arc** | unchanged: 5 dmg / 120 f, 4 u, 0.15 s hitstun | unchanged | own attack; fence unchanged |
| **Coil synced arc** | 8 dmg, 20 f hitstun, reach 4 u | same | |
| **Lorentz discharge** | each own coil within 8 u of Tesla: one burst, 6 dmg Ã— remaining charges, 20 f hitstun, on the nearest enemy within the coil's 4 u, or a Conductive-marked enemy within 8 u of the coil; the coil then fizzles | same | replaces the 8-dmg mark chain (`TeslaLorentzChain*` behaviour) |
| **Turret own shot** | 4 dmg every 180 f, existing targeting, straight shot | same | no longer self-destructs at 4; lifetime 15 s |
| **Turret synced bolt** | 8 dmg, 20 f hitstun, fast straight shot at fire time (24 u/s), reach 10 u | same | the Story reach was 30 u for the own shot (keep that) |
| **Glide bonus bolt** | stays, spends a charge | same | |
| **Clockwork Overdrive** | +1 charge, own shot Ã—0.5 interval vs Extractors only | n/a (Story perk) | |
| **Nest own nip** | 3 dmg every 60 f, 0.15 s hitstun, no Venom, 60 px reach | same | |
| **Nest synced lunge** | 6 dmg, 60 f hold, Venom (intensity 1, 4 s), reach 2.5 u | same | |
| **Venom** | 3 Ã— intensity HP/s | same | |
| **Hero slow** | intensity 1.2 (Ã—0.4), linger 1.75 s; boss floor Ã—0.7 | same (no bosses) | `.tres` intensities for Rift, Vortex, Yorick |
| **Rift** | radius 2.5 u; ticks unchanged; collapse on an E=mcÂ² burst or the owner's landed `combo_3` on a target inside; 8 dmg per target pulled | same | `KitMotionRules` radius |
| **Vortex** | radius 2.5 u; pull 7.0 u/s toward the centre (420 px/s); roll travel and air-dodge frames ignore the pull; no slow inside; slow on exit or end; final launch unchanged | pull 7.0 u/s | knockback-immune bosses and dormant lock members are not pulled (Story) |
| **Golden Ratio** | each tick pulls targets inside toward the spiral's centre (provisional: by 50 % of the remaining distance per tick, capped 1.5 u/tick); tick 1 normal hitstun | same | |

## Â§3 Workstreams, file ownership and order

Work on branches named `kl/<ws>` in git worktrees. The integration branch `kl/integ` holds this plan, the decisions and the recon. **Merge `kl/integ` into your worktree first.** If this file is missing in your worktree, run `git fetch` / `git merge kl/integ`. Commit on your branch only. Never commit to `main` and never push.

**Phase A: KL-RULES**, alone and first. Phase B branches from it.
- Owns:
  - new `scripts/Combat/LingeringKitRules.cs` and `scripts/FighterSim/FighterLingeringKitRules.cs`
  - `HitPayload.CountsAsCombo`
  - the Momentum payout helper, in `LingeringKitRules`, reading `BasicComboRules` unit constants without editing them
  - the Story seam `PlayerController.OwnerHitLanded` event (or a small static registry keyed by owner), raised from `Hitbox.OnAreaEntered` after the `ContactLanded` stamp and from `BaseSpecial.Credit`, carrying target, payload, landed flag and position
  - the sim owed-strike field (310 `SpareInt1`), with a writer at basic and DirectHit Special landed sites and a no-op reader, documented
  - the slow intensities in the `.tres` (Rift, Vortex, Yorick, and their durations)
  - `KitMotionRules` / `KitReachRules` radii
  - the boss slow floor
  - the Venom constant unification across its four sites
  - the Venom heal block, both modes, every site in dossier B Â§3
- Tests: rulebook pins, the heal block, the boss floor, the slow intensity, Venom damage.

**Phase B, in parallel, in three worktrees.** Merge order: SIM, then SCON, then SZONE.
- **KL-SIM** owns every Fighter-sim change under `scripts/FighterSim/`:
  - constructs: charges (reuse 303 `RemainingAttacks` or grow 303), synced strikes, the turret no longer self-destructing, the turret's own shot, the nest's nip and lunge, the glide bolt spending a charge
  - the Lorentz discharge, replacing the chain consumer
  - the Rift: radius, collapse on the finisher, collapse damage
  - the Vortex: radius, pull, roll and air-dodge exemption, slow on exit
  - Golden Ratio pull
  - the `CountsAsCombo` â†’ D3 arming and Momentum in the sim
  - driver presentation: strike edges, and a charge pip row on the persistent proxy HP bar
  - sim tests
  - Must not touch Story files.
- **KL-SCON** owns Story constructs:
  - `TeslaCoilNode`, `LeonardoTurretNode`, `SerpentNestNode` (find its file)
  - `TeslaAbilities.cs` (the Lorentz discharge, replacing the Story chain)
  - the turret glide bolt and Clockwork Overdrive / Re-placement code wherever it lives. If that is in `LeonardoAbilities.cs`, edit only the turret and glide regions.
  - `scenes/constructs/*.tscn` (a charge pip row)
  - Story construct tests
  - Subscribes to the Phase A seam.
- **KL-SZONE** owns Story zones:
  - `CleopatraAbilities.cs` (the Vortex only; the nest node belongs to SCON)
  - `EinsteinRelativityRift.cs`, `EinsteinEmc2Blast.cs` (collapse on E=mcÂ² or the owner's `combo_3` via the seam, collapse damage)
  - `LeonardoAbilities.cs` (Golden Ratio region only)
  - `ShakespeareAbilities.cs` (Yorick's slow if it needs code beyond the `.tres`)
  - `PlaceholderZone.cs` if needed
  - the Story `CountsAsCombo` â†’ D3 / Momentum wiring for zone detonations (`BaseSpecial` if needed; coordinate: SCON uses it read-only)
  - Story zone tests

**Phase C: KL-CLOSE**, serial, in the integration worktree.
- `localization/en.csv` description values for the eight changed abilities and `status_venom`, then `--import` and commit `en.en.translation`.
- `docs/design-contracts/ABILITY_DATA.md` rows.
- `design-godot.md` mirror and the master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`, same edits. Mind `DOC-MASTER-UNBOUND-BINDING`.
- The ledger: one `USER-*` row per decision group, plus `PROVISIONAL-LINGERING-KIT-RULES` listing Â§1â€“Â§2. Update `DEFER-KIT-PASS-MOMENTUM` (still open for the instant Specials) and close `USER-MOMENTUM-HANDS-ON-BEFORE-KIT-PASS` as reversed by decision 20.
- AGENTS.md (the Ability implementation bullets, Status effects, the baseline) and CLAUDE.md (the baseline).
- Three consecutive green full runs against the reconciled total.
- The honest not-delivered list.

## Â§4 Handoffs

Each workstream writes `docs/handoffs/KL_<WS>.md`:
- exact API names
- every file touched, and the hunks in the Â§1.12 shared files
- the test delta, with each new case named, the cases changed in place, and fail-first evidence
- provisional values
- the dossier risks it hit
- what did not ship

## Â§9 Deviation log (append-only; one entry per workstream)

**KL-RULES (Phase A, `kl/rules`, 3907 = 3879 + 28).** Shipped the rulebooks, `CountsAsCombo`, the `OwnerHitLanded` seam, the sim owed-strike field, the .tres hero slow (1.2, 1.75 s), 2.5 u radii, the boss floor, Venom 3/s and the Venom heal block in both modes (`docs/handoffs/KL_RULES.md`). Deviations: Venom 3/s lifted Wrath of the Nile from 78 to 84 (pins moved, authored intensity kept; orchestrator decision owed); `FighterCombatSystem.OnSwingResolved` is now `internal` and two test seams were added to `FighterSimulation`; the Font trickle waits while poisoned; `RelativityRiftLingerFrames` 30 to 105. The Vortex pull and Golden Ratio constants are in the rulebook but not yet read.

**KL-SIM (Phase B, `kl/sim`, 3932 = 3907 + 25).** Shipped the sim constructs (charges in 303 `RemainingAttacks`, synced strikes, nip / lunge, turret lifetime, glide bolt), the Lorentz discharge (the mark chain deleted), the Rift finisher collapse with its 8 damage, the Vortex 7 u/s pull with the roll / air-dodge exemption and slow on exit, the Golden Ratio pull and the driver presentation (`docs/handoffs/KL_SIM.md`). Deviations: a coil with no recipient neither discharges nor fizzles; the sim Vortex has no final launch (the marker rides its last pulse); Vortex pull is horizontal; Golden Ratio tick 1 hitstun 6 f is an agent choice; the strike hitstun uses a new `preserveStun` flag on `ResolveFighterHit`.

**KL-SCON (Phase B, `kl/scon`).** Shipped the Story constructs (`docs/handoffs/KL_SCON.md`): charges (coil 3, turret 4 / 5 with Overdrive, nest 3), the synced strikes through `OwnerHitLanded`, the nest nip and lunge, the turret's own shot at 4 / 180 f with no self-destruct, the glide bolt spending a charge, Overdrive as +1 charge and an Extractor-only faster shot, charge pips on the construct scenes, and the Lorentz discharge replacing the Story mark chain. Deviations: the own-shot numbers are read from `LingeringKitRules` while the `.tres` still author the old ones (left for KL-CLOSE); the discharge does not consume the Conductive mark; the strike exemptions (S2 proration, standard poise, max-hitstun on a running stun, the clone's launch) are keyed on `CountsAsCombo` + Construct (`ConstructStrikes.IsStrike`) with small hunks in `EnemyController` and `PlayerController`; `StoryLorentzChainTests.OnlyACoilWithinEightUnitsChains...` was deleted with the chain.

**KL-SZONE (Phase B, `kl/szone`, +14 cases, full run 3921 = 3907 + 14; `docs/handoffs/KL_SZONE.md`).** Shipped the Story zones: the Rift collapses on the owner's landed `combo_3` as well as on E=mc2, deals 8 and pays Detonation once (D3 armed inside the E=mc2 cast); the Vortex pulls at 7 u/s with roll / air-dodge / immune-boss / dormant-member exemptions, slows once on exit or at its end and pays Detonation on its final launch; Golden Ratio pulls half-way in per tick (cap 1.5 u) and pays HoldCast once; Wrath of the Nile kept at 78 by cutting its Venom to 2 s (the sim reads the same `.tres`). Deviations: Master Stroke's old pull was removed (the base pull subsumes it); Golden Ratio reuses the Vortex exemptions; `EnemyController` / `BossController` do not implement `IStatusEffectTarget` (only the player does), so the Rift's per-frame refresh never reached mobs until the new `LingeringZoneStatus` helper; `PlayerController` itself was not edited (a new partial adds `IsRollTravelOrAirDodge`).

### Integration (kl/integ)

- **One value for a construct's own attack.** Damage, interval and (nest) hitstun are authored in the `.tres` and read by both modes: tesla/special_1 5 / 120, leonardo/special_2 4 / 180 (`HitCount` 1, `IsMultiHit` false, since the 4 charges live only in `LingeringKitRules.TurretCharges`), cleopatra/special_1 3 / 60, hitstun 9. The rulebook constants `CoilOwnArc*`, `TurretOwnShot*`, `TurretLifetimeSeconds` and `NestOwnNip*` were removed. The sim gets them through `SpawnPersistent(..., tickIntervalFrames)` and, for the nest nip's hitstun, `FighterPersistentObjectSystem(contracts)`. Test seam `SpawnPersistentForTest` now takes the own damage and interval (`tests/Determinism/ConstructOwnAttack.cs` reads the `.tres`).
- **Discharge parity (verified, no change):** no recipient keeps the charges; a recipient spends all and fizzles even when the burst is refused or blocked and pays no Detonation. Tests added in both modes.
- **Synced-strike Momentum (verified, no change):** paid once when the strike itself lands (D1a, damage > 0), never on the triggering hit or for a blocked, refused or warded strike. Story and sim refused-strike tests added.
- **Golden Ratio tick 1:** `LingeringKitRules.GoldenRatioFirstTickHitstunFrames` (6) is the one source for `FighterSpiralRules.FirstTickHitstunFrames` and Story's `ChurnTickHitstunFrames`. Difference kept: Story gives that hitstun to every non-final tick, the sim to tick 1 only.
- **Recorded differences:** the sim Vortex has no final launch (the Detonation marker rides its last pulse) while Story launches; the sim pull is horizontal only, and Story's Vortex pull is also horizontal only (`PullThroughCollision(new Vector2(travel, 0))`), so the pull axes agree.
- **IStatusEffectTarget:** only `PlayerController` declares it; `EnemyController` and `BossController` expose the two-slot view without the interface (AGENTS.md is wrong there).
