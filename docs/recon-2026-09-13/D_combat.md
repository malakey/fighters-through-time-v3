# Gap dossier — Section D: Combat Mechanics (master lines 988–1647)

Scope: §4 Combat Mechanics (hitstop, basic string + per-character profiles, riders, aerials,
directional attacks, specials/cooldowns, hyper-armor, AbilityData/MovementAbilityData schemas,
Ultimate & Influence Meter, knockback/DI/tech/stage boundaries, damage formulas, Recoverable Health
(Rally / Desperation / Defy), Time Systems (Echo Step, Resonance Momentum, Overtime), hitbox
geometry, pushboxes, Defense/Blocking, Enemy Attack Classification, Enemy Stagger Discipline,
gravity/ledge/drop-through/persistent-object summaries, combo & cancel rules, Story Death Rewind
combat-state rules, Grabs & Throws, Character Data Architecture, Enemy & Boss Data Architecture).

Sources read in full: `D_combat.patch` (326 lines), master §4 lines 988–1647,
`docs/DEFENSIVE_EFFECTS.md`, `docs/COMBAT_VALIDATION.md`, `docs/DESIGN_BUILD_DEVIATIONS.md`, plus
the cross-referenced Suppression / Eraser / StatModifier passages (master 153, 470–506, 1795–1842,
3190, 3400, 2925–2931).

Repo revision inspected: `main` @ `31fed14` (V7.4 stun-lock pass), clean tree.

---

## 0. Executive summary

| # | Item | Status | Size |
|---|---|---|---|
| D-01 | D01/D02b defence ordering (protection before block) | **partial** (sim ordering ~right, Story inverted) | M |
| D-02 | D02a same-shield refresh without stacking | partial | S |
| D-03 | D02c 8-second granted-shield lifetimes | **missing** | M |
| D-04 | D02d Wardenclyffe range/delay/rate | **partial** (wrong rate, no delay, absorbs out of range) | M |
| D-05 | D02e Temporal Aegis one-active/no-timer + Story has no Aegis at all | partial / **missing (Story)** | M |
| D-06 | D03a HP-barrier full absorption suppresses attached effects | **missing** | M |
| D-07 | D03b Unblockable bypasses block but not barriers/Aegis | partial | S |
| D-08 | D03c shields absorb DoT + hazard ticks | **missing** | M |
| D-09 | D03d primary throws bypass barriers/Aegis | **missing** (sim: Aegis eats throws) | S |
| D-10 | D03e Aegis prevents attached effects | implemented (sim) / n/a (Story) | — |
| D-11 | D03f credit actual HP removed | mostly implemented; **Rally reclaim pool-debit bug** | S |
| D-12 | D03g direct hits reclaim; ticks do not | **partial** (zones reclaim in both modes) | S |
| D-13 | D03h Ultimate-origin damage earns caster zero meter | **missing** | M |
| D-14 | D04 Defy protected recovery (release + 60 ticks) | **missing** | M |
| D-15 | F13 Defy resonance seal read-model | **missing** (gameplay-side state source) | S (combat) |
| D-16 | F15 Divine Piercing / Emancipator = full shatter | **retired rule still shipped** | S |
| D-17 | F17 Aegis/orb must not end shatter lockout | **defect shipped** | S |
| D-18 | F09 shieldstun/No-Juggling/aerial: validation only | no code change; **test-harness work** | M |
| D-19 | Echo Step V7.6 determinism (31 per-tick samples, exact t−30) | **partial** (5-sample/6-frame ring) | L |
| D-20 | Echo Step destination policy (exact or no teleport) | **missing** | M |
| D-21 | C01c dedicated `gameplay_echo_step` + `gameplay_grab` actions | **missing** | M |
| D-22 | V7.6 same-frame chord priority (Echo Step / grab vs block-cancel) | **missing/ambiguous** | S |
| D-23 | F23 `Grabbing`/`Thrown` canonical FSM states + snapshot fields | partial (`Thrown` absent) | S |
| D-24 | Tesla rider: `Conductive` mark separate from Static Charge | **missing** | M |
| D-25 | F07 Static Charge stagger accounting (concurrent, greater-once) | **defect** (sums budget, overwrites stun) | S |
| D-26 | F06 Joan Wings refresh gated on Story node | **missing** | S |
| D-27 | F06 Shield of Orléans = flat 5 meter per distinct blocked attack | **divergent** (damage-scaled ×1.25) | S |
| D-28 | F14 Siphon Snare meter drain (Story-only) | **missing** (no Eraser) | M |
| D-29 | `Suppression` status + ability-lock gate | **missing** | M |
| D-30 | Legacy Unlock lock states on ability slots (Dormant/Suppressed/Clear) | **missing** (gameplay gate) | M |
| D-31 | `ActiveStatuses : StatusSlots` on player/enemy/boss + `StatusRouting` | partial (ad-hoc two slots) | S |
| D-32 | StatModifier `abilityScope` + new StatType keys (AbilityDamage/Range/Duration, ConstructHP, RallyEchoFraction, ExtractorDamage) | **missing** | M |
| D-33 | V7.6 boss HP table (L1–L4) + repo-wide boss HP divergence | **defect + open ledger** | S (values) / M (balance) |
| D-34 | Boss renames: Archive Prime → Forge Sentinel; Apex Eraser → First Unbound; Vex → "Unbound Overseer Vex" | **missing** | S |
| D-35 | F20 Mirror Paradox 700/1000/1500 by difficulty | **missing** | S |
| D-36 | F16 Story pits lethal, non-defiable | partial (Defy already hit-only) | S |
| D-37 | T01a Death-Rewind combat-state cleanup | **partial** (freeze exists, cleanup list absent) | M |
| D-38 | P04 authority rule (GDD over `.tres`) + deviation ledger | **process change** | S |

Shared-file hot spots across the whole list: `scripts/Characters/PlayerController.cs` (2,867 lines,
touched by ~14 items), `scripts/FighterSim/FighterEntitySystems.cs::FighterDamageRules.ApplyFighterHit`
(the single sim chokepoint, ~10 items), `scripts/Combat/BasicComboRules.cs`,
`scripts/Combat/BlockSystem.cs`, `localization/en.csv`, `AGENTS.md`, `project.godot` (InputMap).
**`FighterRuntimeComponent` is full at 128 bytes; `FighterVerbComponent` is Klotho ID 310 and
`FighterEchoRingComponent` is ID 311 — the next free IDs are 312+.**

---

## 1. Hitstop / hitlag

**Design (master 991–994).** Unchanged numerically. Only editorial: the exemption list now says
"zone and DoT ticks (such as Venom)" instead of "(Venom, burn)" — because Radiant Burn is a
vulnerability modifier with no damage ticks (DEFENSIVE_EFFECTS D03c "Boundary cases").

**Repo.** `BasicComboRules.HitstopMinFrames=3 / Max=8 / Blocked=2`, `HitstopFrames(damage)` linear;
sim `FighterVerbRules.ApplyHitstop` max-assign; Story `PlayerController.ApplyHitstop`. Exemption
carried by `HitPayload.ExemptFromHitstop` (Story) and `appliesHitstop:` (sim). `RadiantBurnStrategy`
is a damage-taken multiplier only — no ticks. Lethal hits skip hitstop in both modes (repo rule, not
contradicted by the doc).

**Status: implemented.** No change. One nuance worth a test pin under D03c/D04: absorbed ticks must
still produce **no** hitstop (DEFENSIVE_EFFECTS D03c/D03e), which falls out of the existing flags.

---

## 2. Basic string, per-character profiles and riders

### 2.1 String profiles (master 1008–1027)
**Design.** Table unchanged (Joan 5/14 … Lincoln 8/17; damage tenths sum 33; reach 85–120%).
Wording softened: "the *intended* Hit 1 → Hit 2 link and the *conditional* Hit 2 → Finisher block
escape … validate under F09".

**Repo.** `BasicComboRules.StringProfiles` matches the table exactly (verified row by row);
`TemplateStringProfile` = Tesla. **Status: implemented**, no numeric change. The escape-conditions
wording change is D-18 (validation only).

### 2.2 Tesla rider — `Conductive` (D-24) — **missing**
**Design (master 1033).** Tesla's finisher now applies **two** things:
* `Static Charge` — interrupt, **0.4 s / 24 frames** (unchanged), and
* a **separate `Conductive` mark** — **1.5 s baseline, 2.5 s with the Story node**, which
  "enables Lorentz chains without disabling actions". Master 1839: Conductive uses a **separate
  non-pulsing circuit glyph**, **does not occupy a status slot**. Master 1392 (F07): Conductive
  "causes no action lock, contributes **zero stagger budget**, and may remain while the target acts
  in armor". Master 1442 (T01a): Tesla's death clears his Conductive marks even though coils survive.

**Repo.** `BasicComboRules.StringProfiles["tesla"]` authors only `FinisherStatusType =
StatusType.StaticCharge, FinisherStatusFrames = 24`. No `Conductive` concept anywhere.
`TeslaAbilities` Lorentz Pulse currently keys its chain-lightning bonus on **StaticCharge**
("Targets primed with StaticCharge (checked before the Root replaces it)"), i.e. it uses the
action-locking status as the chain marker — exactly what F07 splits apart.

**Change list.**
* Add a non-slot mark: a `ConductiveFramesRemaining` (Story: `PlayerController`/`EnemyController`;
  sim: new component field — `FighterRuntimeComponent` is full, so it rides the new 312 component or
  a spare `FighterVerbComponent` int) + `ConductiveSourcePlayerID` for the Tesla-death clear.
* `BasicStringProfile`: add `FinisherMarkType`/`FinisherMarkFrames` (baseline 90 frames) and a
  Story-node extension to 150 frames keyed off a Resonance flag.
* Re-point `TeslaAbilities` Lorentz chain eligibility from `HasStatusEffect(StaticCharge)` to the
  Conductive mark; keep StaticCharge as pure interrupt.
* Presentation: separate non-pulsing circuit glyph (shared with the VFX agent).
* Tests: `TeslaContentTests`, `BasicStringProfileTests`, `EnemyControllerTests` (zero stagger budget),
  new `ConductiveMarkTests`.

**Size: M.** Shared: `BasicComboRules.cs`, `PlayerController.cs`, `EnemyController.cs`, sim component
(new ID 312+), `en.csv` (status name/glyph label).

### 2.3 Joan's Wings refresh now node-gated (D-26) — **missing**
**Design (master 1038).** "Joan's finisher-based Wings refresh requires her purchased Story grid
node (F06 Option C)." Previously the doc claimed it already lived on the movement-ability table.

**Repo.** No finisher→movement-cooldown refresh exists in either mode.
`PlayerController.OnMeleeHitConfirmed` handles `combo_3` only for Resonance Momentum
(`ApplyMomentumRefund`) and Joan's `ZealousVigorPerkKey` heal.

**Change list.** New Story-only perk key (e.g. `ascendant_momentum`) consumed at
`PlayerController.OnMeleeHitConfirmed` where `HitboxID == "combo_3"`, zeroing
`MovementAbilityCooldownTimer`. Must **not** exist in `scripts/FighterSim/`. Tests: `JoanContentTests`,
`ResonanceGridTests`. **Size: S.** Shared with the roster/Resonance workstream (node authoring,
`resources/Resonance/joan_grid.tres`, `en.csv`).

### 2.4 Cleopatra / Lincoln / Mozart riders
Unchanged by the diff, and shipped: `cleopatra` finisher Venom 120f @ 0.5 intensity; `lincoln`
`hit2VerticalLaunchTenths: 20`; `mozart` `finisherKnockbackTenths: 55`. **Implemented.**

---

## 3. Aerials and No Juggling — F09 validation (D-18)

**Design (master 1048, 1438; `docs/COMBAT_VALIDATION.md` in full).** No rule change: zero landing
lag, zero jump squat, no dash, 8-frame shieldstun, existing shatter rules all **retained**. What
changed is the *epistemic* status — the doc no longer asserts attacker advantage on block, no longer
asserts every loop is escapable, and defines:
* `advantage = defenderActionFrame - attackerActionFrame` measured from a common contact frame,
  accounting for remaining active/recovery frames, legal cancels, landing cancellation, shared
  hitstop, charges/cooldowns, statuses and shatter;
* a required **on-block outcome table** (6 contact groups × per-character/variant/contact-frame rows)
  — every row currently "Pending";
* eight **escape/commitment scenarios** (aerial whiff & landing pressure, hit-2 escape, zero-charge
  pressure, repeated launchers, landing tech, control statuses incl. F07 Conductive, construct-assisted
  pressure, Story protection) with required variations;
* **V02 controlled stall validation** (2026-09-13, Option A): diagnose-then-propose; **no automatic
  cooldown reduction**; requires the three Open stages playable first — and those are not authored
  (repo ships ten sealed stages), so V02 is **blocked coverage**.

**Repo.** No frame-advantage harness exists. The closest assets are `FighterHitPipelineTests`,
`FighterBasicStringParityTests`, `StoryBlockModelTests`, `BlockShatterLockoutTests`,
`FighterCpuHitstunDefenseTests`. The GdUnit suite can drive the deterministic sim directly, which is
the natural harness.

**Change list.** New `tests/Determinism/FighterFrameAdvantageHarness.cs` producing the table rows
programmatically from the sim (contact frame → earliest legal action frame per actor), plus a
scenario suite for the eight escape checks. Deterministic defender inputs, not CPU probability rolls
(the doc is explicit that a failed CPU roll is not evidence). Record output into
`docs/COMBAT_VALIDATION.md`-shaped rows.
**Size: M–L** (harness + sweep). **No gameplay constants change without a separate approved fix.**
Note for planners: F09/V02 gate *Fighter balance sign-off*, so nothing else in this dossier should be
described as "balanced" on completion.

---

## 4. Hyper-armor (master 1077–1081)

**Design.** "Ultimate attacks … bypass hyper-armor **when their hit reaches normal hit-response
resolution**. A fully HP-barrier-absorbed contact causes no interrupt under D03a."

**Repo.** Sim: `if (carriesImpulse && (target.HyperArmorFrames <= 0 || attackClass ==
UltimateAttackClass))` — ultimate pierces armor ✓. Story: `HasActiveHyperArmorAgainst(hit.AttackClass)`
at `OnHurtboxHit` ✓. Neither has an HP barrier in the pre-response path (see D-06), so the new clause
is unreachable until D03a lands.

**Status: partial (blocked on D-06).** Change is one ordering guarantee: absorption resolves **before**
the armor/hit-response branch. **Size: S** once D-06's barrier layer exists.

---

## 5. AbilityData schema + P04 authority (D-38)

**Design (master 1116–1117).** Schema table unchanged. The `[!TIP]` was rewritten: `.tres` files are
**evidence about a verified build revision**, not authority; "the historical count of 36 ability
resources is not current file/behavior verification"; mismatches go in
`docs/DESIGN_BUILD_DEVIATIONS.md`; **do not infer a new design value from an unverified resource**.

**Repo/process impact.** This inverts the long-standing repo rule in `CLAUDE.md`
("If a document and a `.tres` resource disagree, the resource wins") and `AGENTS.md`
("Resources own the numbers… The `.tres` files remain the law if this table ever drifts").

**Change list.** Amend `CLAUDE.md` "Documentation authority map" and the `AGENTS.md` coding-conventions
bullet to: *GDD + adopted contracts define intended behaviour; `.tres` is build evidence; record
divergence in the ledger*. Keep the "no second canonical value in code" rule (that is orthogonal).
Create/port a repo-side deviation ledger (or point at the docs-repo one) and seed it with D-33.
**Size: S** but **high blast radius** — it changes how every other workstream resolves conflicts.
Shared: `CLAUDE.md`, `AGENTS.md`.

---

## 6. Influence Meter (master 1200–1210)

### 6.1 D03f — credit actual HP removed (D-11)
**Design.** `creditedDamage = max(0, hpBeforeHit − hpAfterHit)` evaluated **after defences and any
Defy result, before healing/recovery**; excludes shield-absorbed damage, blocked damage, overkill
below zero, and the lethal portion Defy prevented. Used for: attacker damage-dealt meter (1.0/HP),
victim damage-taken meter (0.25/HP, permanent/echo split retained), victim echo generation, and
attacker Rally reclaim cap. Worked example: 30-damage hit, 20-HP barrier, victim at 6 HP →
creditedDamage **6**, attacker gains 6 meter, reclaim ≤ 12 and ≤ missing HP.

**Repo — sim.** `ApplyFighterHit` computes `int actualDamage = previousHP - target.CurrentHP` **after**
the HP clamp at 0 **and after** the Defy branch raises HP to 1 → overkill excluded ✓, Defy portion
excluded ✓ (6→1 credits 5 ✓, 1→1 credits 0 ✓). Aegis returns before any credit ✓.
**Repo — Story.** `ApplyDamage` returns `previousHP - CurrentHP` with the same Defy re-computation
(`damageApplied = previousHP - CurrentHP` after setting `CurrentHP = 1`) ✓; every ability call site
passes the **returned applied damage** (`if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt)`) ✓.
`StoryShieldPoints` absorption happens inside `ApplyDamage` before the HP subtraction, so absorbed
damage already earns nothing ✓.

**Status: implemented** for the meter, **defective for reclaim** — see D-12/§8.2. No change needed to
the meter rates themselves.

### 6.2 D03h — Ultimate-origin damage earns the caster zero meter (D-13) — **missing**
**Design (master 1204, 1210, 1269; DEFENSIVE_EFFECTS D03h).** An ordinary Ultimate still costs 100 on
accepted activation. **All damage originating from that Ultimate awards the caster 0 damage-dealt
meter**, regardless of HP removed, target count, or *when* it lands. The exclusion follows the source
execution into direct hits, projectiles, chained bursts, summoned attacks, persistent damage zones and
attached damaging statuses — named cases: **Wardenclyffe Cataclysm's coil explosions** and **Wrath of
the Nile's poison ticks**. It is a *source* rule, not a global meter lock: independent non-Ultimate
attacks and pre-existing ordinary construct/status damage keep their eligibility. Victim damage-taken
meter and echo are unaffected; direct-hit Rally reclaim from Ultimate impacts still works (D03g).

**Repo.** Nothing implements this. Sim: every ultimate path calls `ApplyFighterHit` with
`creditInfluence` defaulting to **true**, including `FighterUltimateRules` line ~256 (the Cataclysm
coil chain, which already passes `collectsEcho: false` but still credits meter) and the ultimate-slot
zone pulses in `FighterEntitySystems` (`ZoneTypeID % 10 == UltimateSlot` → `UltimateAttackClass`).
Story: `EinsteinUltimate.cs:141` and every other ultimate calls `AddInfluenceFromDamageDealt(dealt)`.

**Change list.**
* Sim: thread an `ultimateOrigin` flag down every ultimate-spawned entity —
  `FighterProjectileComponent`, `FighterZoneComponent`, `FighterPersistentObjectComponent`, and the
  status application — and pass `creditInfluence: false` at `ApplyFighterHit`. Attribution must
  survive the cinematic ending, owner interruption and rollback restore (the flag is snapshot state).
  Note `FighterZoneComponent` already distinguishes the ultimate slot by `ZoneTypeID % 10`, which is
  a usable free signal for zones; projectiles and statuses need a new field.
* Story: add `AddInfluenceFromDamageDealt(dealt, collectsEcho: true, ultimateOrigin: true)` (or a
  `BaseSpecial.IsUltimateSlot` check inside the helper) and set it on all nine ultimate scripts plus
  `PlaceholderZone`/`PlaceholderProjectile` instances spawned by an ultimate, plus Nile's Venom ticks
  (`VenomStrategy` needs a source-origin field to suppress caster meter — today it routes via
  `ApplyPersistentDamage`, which credits nobody, so Story poison is accidentally compliant; confirm
  per-kit).
* Explicit non-regression: an ordinary Tesla coil tick during a Cataclysm still earns meter.
* Tests: new `UltimateMeterOriginTests` (sim + Story), cases from DEFENSIVE_EFFECTS D03h
  ("Ultimate removing 70 HP from each of two enemies earns 0, not 100"; independent 8-HP projectile
  still earns 8).

**Size: M.** Shared: `ApplyFighterHit` chokepoint, all nine ultimate ability scripts, sim components
(possible new field on 312).

### 6.3 F14 — meter drains, the Siphon Snare exception (D-28) — **missing**
**Design (master 1209 + the Eraser entry at master 2928).** Fighter Mode: opponent attacks/statuses
can never drain/steal/freeze meter. Story keeps that default with **one** exception, the Eraser's
**Siphon Snare**: `min(currentMeter, 10 × liveDeltaSeconds)` per live tick, fractional preserved,
for at most **180 live frames / 3 s** → at most **30 points** per cast; **no** initial lump, no HP
damage, no hitstun/knockback/movement lock, no status slot, no Rally echo, no damage-based meter, no
hitstop; transfers nothing to the Eraser. Attachment: single check at telegraph end (45 frames /
0.75 s), 3-unit radius, unobstructed LoS; refused if the player is Suppressed, at zero meter, already
tethered, or invulnerable. A legal grounded front-facing block at attachment prevents all drain and
costs **1 charge** with the ordinary Basic block response (shieldstun; shatter if it was the last
charge); zero-charge/locked/airborne block cannot absorb it; no HP chip. Break conditions checked
**before each drain tick**: range > 3 units, blocked LoS, player invulnerability (incl. roll i-frames),
either actor's death, a successful stun/stagger interrupt of the Eraser (armor-rejected hits do not
count), or newly applied Suppression. One tether per player; 10 s cooldown from cast commitment with
no refund. Freeze-aware. It can **disarm an unused full-meter Defy but never consumes or resets the
once-only flag**. Authored environmental drains survive: Extractor discharge **−20 points**, Paris
Neural Dampening Beam **5 points/s**.

**Repo.** No Eraser enemy, no `Suppression`, no Siphon Snare. Existing drains: extractor and Paris
beam both route through `PlayerController.DrainUltimateMeter` (V7.3 pass) ✓.

**Change list.** New `EnemyAbilityData` archetype or a bespoke channel behaviour in
`EnemyAbilityExecutor`; a tether runtime with per-tick break checks; block interaction via
`BlockSystem.ResolveHit` with a zero-damage Basic-class payload (needs a "control-only" payload path,
since `ResolveHit` today assumes a damaging hit and the zero-HP case must not chip); `DrainUltimateMeter`
call at 10/s. **Size: M.** Primary owner is the roster/Story-enemy workstream — listed here because the
meter-protection rule and its exception live in my section. Shared: `PlayerController`, `BlockSystem`,
`en.csv`, manifest, pool budgets.

### 6.4 F04 Nexus puzzle Ultimate authorization
**Design (master 1210).** An armed Nexus Resonance Source authorises **only** its designated puzzle
Ultimate at **any** meter value; that cast leaves the meter unchanged, affects only the puzzle, cannot
become combat power; all ordinary casts still require and consume 100; authorization never bypasses
action-state restrictions or the Time Freeze action lock, and (F13) never lights the Defy seal.

**Repo.** No such authorization path; ultimate input is gated on `IsFull` only.
**Status: missing.** Primary owner: Story puzzle workstream (Section 3). My section's constraint is
the **meter chokepoint**: a puzzle cast must not call `Consume()` and must not earn/drain meter.
**Size: S** on the combat side.

---

## 7. Knockback, DI, tech, stage boundaries

### 7.1 Unchanged and implemented
Weight mitigation `k/(1+w)` ✓ (`DamageCalculator.CalculateKnockback`), low-HP scale
`×(1+missingHP)` ✓ (`BasicComboRules.LowHealthKnockbackScale`, both chokepoints), knockback replaces
velocity with the zero-knockback exception ✓, authored launch angles ✓ (per-hit vertical components +
`verticalKnockbackScale`), DI ±15° quantized ✓ (`FighterVerbRules.ResolvePendingLaunch` +
`BasicComboRules.ResolveDirectionalInfluence`), landing tech 12 frames locked-and-invulnerable and
charge-independent ✓ (`TryLandingTech` reads the raw button; Story `_techLockoutSeconds` /
`_techInvulnerabilitySeconds`).

### 7.2 Landing tech tutorial hook (master 1221)
**Design (V7.6).** "Level 0's **Hitstun Agency Calibration** stages one scripted launch the player must
tech, with the DI prompt on the same launch." (Previously a vague "when the design's tutorial pass next
revisits Level 0".)
**Repo.** `Level00Controller` has six calibration steps (basics, block, specials, forced-meter ultimate,
movement ability, scripted rewind) — **no** hitstun-agency step.
**Status: missing.** Owner: tutorial/Story workstream; the combat-side requirement is a deterministic
scripted launch + a public `IsInTechLockout` read (already exposed). **Size: M** (tutorial), **S** here.

### 7.3 F16 — Story bottomless pits are lethal (D-36)
**Design (master 1224, 1260).** **All** Story bottomless pits/voids are **lethal on crossing their
authored kill boundary** — no partial-HP fall penalty, no nonlethal checkpoint return. An ordinary
drop to a lower playable area or a room transition is not a pit kill. Falls are **non-hit deaths and
cannot trigger Defy History**. Available death-rewind charges resolve normally; otherwise Acts I–II
Collapse or Act III Anchor Snap/Smothered.

**Repo.** Defy is already reachable only through `ApplyDamage`, and `PlayerController` comments call
out "Should a true blast-zone/pit death path ever be added, it must bypass this" — so the *hit-only*
half is satisfied by construction. The **lethal-on-crossing** half belongs to Story hazards
(`scripts/Environment/`), which is another agent's section.
**Status: partial.** My change: add an explicit `KillPlayerNonHit()` entry point on `PlayerController`
that transitions to `Dead` **without** touching Defy/Rally/meter, so pit authors cannot accidentally
route through `ApplyDamage`. Tests: `StoryEnvironmentalDamageTests` (+ pit case). **Size: S.**

---

## 8. Recoverable Health (Rally / Desperation / Defy)

### 8.1 Rally accrual — unchanged
`echoFraction = 0.20 + 0.30 × missingHP` after the hit ✓, 150-frame linear drain restarted per hit ✓,
blocked hits never generate echo ✓, hazard/environmental damage does ✓ (`ApplyEnvironmentalDamage`),
KO clears the pool ✓, deferred victim meter on drained echo ✓ (`TickCounters` / Story `_PhysicsProcess`),
Story difficulty scaling ✓ (`StoryDifficultyTuning.GetRallyEchoMultiplier`). Overtime ×1.5 cap 0.60 ✓
(`OvertimeEchoMultiplier`/`OvertimeEchoCap`).

### 8.2 Reclaim formula gains a `missingHP` term + pool-debit bug (D-11/D-12) — **defect**
**Design (master 1247).** `reclaimedHP = min(pool, creditedDamage × 2.0, **missingHP**)`, and
"**subtract only HP actually reclaimed from the pool**" (D03f/D03g).

**Repo — sim** (`FighterEntitySystems.cs:214–231`):
```csharp
FP64 reclaimAmount = FP64.Min(attackerVerb.EchoPool, FP64.FromInt(actualDamage) * RallyReclaimMultiplier);
int reclaim = (int)((reclaimAmount.RawValue + FP64.One.RawValue/2) / FP64.One.RawValue);
if (reclaim > 0) attacker.CurrentHP = attacker.CurrentHP + reclaim > attacker.MaxHP ? attacker.MaxHP : ...;
attackerVerb.EchoPool -= reclaimAmount;   // <-- debits the FULL amount even when HP was capped
```
**Repo — Story** (`PlayerController.cs:2016–2030`): identical shape — `_echoPool -= reclaimAmount;`
then `HealStory(reclaim)`, and `HealStory` clamps at `MaximumHP`. Both burn pool that was never
converted to HP. There is no `missingHP` clamp in either mode.

**Change list.** In both chokepoints: compute `missing = MaxHP − CurrentHP`, clamp
`reclaimAmount = min(pool, credited × 2.0, missing)`, heal, then debit **exactly the healed amount**
(re-read HP before/after). Tests: `FighterHitPipelineTests` / `StoryRallyTests` — full-HP attacker
reclaims 0 and keeps the whole pool; a 1-HP-missing attacker reclaims 1 and the pool drops by 1.
**Size: S.** Shared: both damage chokepoints.

### 8.3 D03g — reclaim source eligibility (D-12) — **partial**
**Design.** Only **direct** damaging hits reclaim: basic/directional strikes, direct Special or
Ultimate impacts, **player-fired projectiles** (including delayed ones landing after the firing
animation), **primary throws**, and the **secondary Story thrown-mob collision** (at its own actual HP
loss, with normal shield coverage). Excluded: persistent zone ticks **including Relativity Rift chip
and every Sandstorm Vortex tick — explicitly including the Vortex's final launching tick**, autonomous
construct/persistent-object damage (even when delivered through a projectile), coil-fence ticks,
attached DoT ticks, stage-hazard ticks. "A separately authored direct cast impact may qualify; a first
zone tick does **not** qualify merely because it occurs on the casting frame." Blocked/fully-absorbed
hits reclaim zero. Multi-target hits resolve each eligible contact once against the remaining
pool/missing HP.

**Repo — sim.** Gate is `collectsEcho && attackClass != HazardAttackClass`. Construct paths pass
`collectsEcho: false` (coil arc `:1110`, coil fence `:1182`, hazard `:433`, Cataclysm chain
`FighterUltimateRules:260`). **Zone pulses do not** — `FighterEntitySystems` ~`:2012` passes only
`appliesHitstop: false`, so **Relativity Rift and Sandstorm Vortex ticks currently reclaim Rally**.
The Vortex/Spiral/Matrix/Cosmological expiry launches (`:2077`, `:2133`, `:2164`) also omit it
(they are 0-damage, so harmless today, but should be explicit).
**Repo — Story.** `PlaceholderZone.cs:197` calls `AddInfluenceFromDamageDealt(damageApplied)` with
`collectsEcho` defaulting to **true** — every Story zone tick reclaims. The four construct nodes
(`LeonardoTurretNode:183`, `SerpentNestNode:201`, `TeslaCoilNode:271`, `VineSnareNode:221`) correctly
pass `false`. The Story `PlayerController` doc-comment even enshrines the wrong rule: *"a landed
direct hit (melee, directional, special, ultimate, projectile, **zone pulse**) also reclaims"*.

**Change list.** Pass `collectsEcho: false` at every zone-tick site in both modes (sim zone pulse +
all four expiry pulses; Story `PlaceholderZone`); fix the `PlayerController` doc comment; keep throws
and player-fired projectiles eligible; make the Story thrown-mob secondary collision eligible at its
own applied damage. Tests: extend `FighterVerbLayerTests` / `StoryRallyTests` with "a 6-HP zone tick
reclaims 0 while a 6-HP direct hit reclaims up to 12". **Size: S.**

### 8.4 Defy History — D04 protected recovery (D-14) — **missing**
**Design (master 1259; DEFENSIVE_EFFECTS D04 in full).** On a successful Defy:
1. Still spend the full 100 meter, survive at 1 HP, no echo, consume the once-per-match /
   once-per-Story-attempt use (unchanged).
2. **Immediately** enable hit invulnerability covering the Defy presentation.
3. **Release** the survivor from the triggering hit's forced hitstun/stagger/knockback/launch **and
   capture** (including a paired throw or cinematic trap); clear its forced-motion contribution;
   keep the current legal position (no teleport, no granted jumps/cooldowns/resources); the defied hit
   may not impose a new attached control/status effect; end only that victim's capture link (the
   attack is not cancelled for other targets, projectiles/zones survive).
4. After the presentation, grant **60 active gameplay ticks (1 s)** of hit invulnerability **starting
   at the first resumed normal-control tick**. Ticks 1–60 protected, tick 61 unprotected. Attacking
   does not cancel or refresh it. Countdown **pauses** during global hitstop, menus, Time Freeze and
   suspended-combat presentations.
5. The gate rejects subsequent damaging hits **before** Aegis/HP barriers/block — covering all
   attackers, ongoing damaging-status ticks and damaging stage hazards. Rejected contacts spend no
   shield capacity or block charges, apply no hitstun/launch/status, and grant **no** damage, Rally or
   block rewards. Existing statuses are **not** cleansed; their timers continue and protected ticks are
   discarded, not banked. New grab/capture attempts fail under invulnerable-target eligibility.
6. Not immunity to non-hit deaths (pits, Integrity zero). No healing beyond 1 HP, no meter refund,
   no rewind spend, no status cleanse, no reset of the spent flag.
7. Dual lethal trades: both procs resolve and consume their uses; installing one survivor's protection
   cannot erase the other already-committed outcome.
8. New snapshot state: **`DefyProtectionAwaitControl` (flag)** and **`DefyProtectionFrames` (int)**
   plus the proc/presentation identity (master 1272).
9. Presentation: existing meter-crack/desaturation cue retained; show the remaining protection with the
   **existing secondary invulnerability glow**, fading at actual expiry; the F13 seal stays broken
   throughout and after. C01a (Reduced Temporal Effects) swaps full-scene desaturation for a steady
   local protection cue + the meter crack, preserving slow-motion timing and the full post-control
   second.
10. Story F10 checkpoint reconstruction clears the transient protection and pending presentation while
    retaining committed HP/meter/spent use; loading never replays the proc or grants a fresh second.
    Death/stock loss, Death Rewind cleanup, scene reconstruction and F22 Sudden Death clear the window.

**Repo — sim** (`FighterEntitySystems.cs:162–172`): sets `DefyHistoryUsed`, zeroes `Influence`, sets
`CurrentHP = 1`, applies a 12-frame hitstop. **It does not release hitstun/launch/capture** — the
code then falls through to the normal impulse/hitstun block, so the survivor is launched and stunned
by the very hit they defied — and grants **no** invulnerability window.
**Repo — Story** (`PlayerController.cs:1968–1982`): same shape; sets `_defyFiredThisHit`, 12-frame
hitstop, camera shake; `OnHurtboxHit` then continues into knockback + `ApplyStun`.

**Change list.**
* Sim: two new snapshot fields. `FighterVerbComponent` (ID 310) still has room on paper but is the
  V7.1/V7.2 catch-all — safest is to put **D04 + Suppression + Conductive + the Echo Step ring rework**
  together on a **new component, ID 312** (see D-19). Add the release step (zero `HitstunFrames`,
  clear `PendingLaunchActive`/`Tumble`, zero the knockback write for this hit, release grab via
  `FighterGrabRules.ReleaseHeldVictim` + clear `BeingHeld`/`GrabPhase` on the partner), set
  `DefyProtectionAwaitControl = 1`, and start the 60-tick countdown at the first actionable tick,
  pausing on `HitstopFrames > 0` and match-flow freezes. Reject incoming hits at the **top** of
  `ApplyFighterHit`, before the Aegis branch.
* Story: mirror on `PlayerController` (`_defyProtectionAwaitControl` / `_defyProtectionFrames`), reject
  at the top of `OnHurtboxHit` **and** `ApplyDamage`/`ApplyEnvironmentalDamage` (hazard ticks), release
  grab (`ReleaseGrabState` already exists), suppress the stun/knockback branch for the defied hit.
* Rollback: the pending-presentation identity must be deduplicated across resimulation.
* Tests: new `DefyProtectedRecoveryTests` (sim + Story) covering the DEFENSIVE_EFFECTS D04 validation
  list — immediate and same-frame follow-up protection, both dual-Defy outcomes, throw release,
  grounded/airborne control return, exactly 60 ticks after presentation, pause/freeze, attacking does
  not cancel, unrelated statuses keep running, poison/hazard ticks rejected, hit at tick 61 lands,
  pit/Integrity still kill, preserved Rally/shields, no reward for rejected contacts, snapshot
  remainder, checkpoint-load cleanup without regrant.
**Size: M** (L if it forces the new component alone). Shared: `ApplyFighterHit`, `PlayerController`,
new Klotho component, `FighterSimulationDriver` (presentation), HUD (F13).

### 8.5 Defy — other clauses
* **F10 persistence (Story)**: "once per level" = **once per attempt**; commit `defyHistoryUsed`, HP and
  meter **together**; preserve through death rewind, Collapse, Snap, checkpoints, hub visits and load;
  only fresh entry / full Restart Level resets. **Repo:** `_storyDefyHistoryUsed` is a private
  `PlayerController` field with **no save persistence** and no `StoryManager` attempt-registry entry —
  a mid-level quit/resume or a death rewind that rebuilds the player restores the Defy. **Status:
  missing.** Change: add to the V7.3 per-attempt registry (`StoryManager.WriteAttemptStateToSave` /
  `ApplyResumedAttemptState`, save schema v5 → additive field). **Size: S**, shared with the
  Story-persistence agent.
* **Sudden Death (F22)**: "the next actual death decides the match; there is no Defy survival proc".
  **Repo:** `FighterSimulationSystems:2354` pre-marks `verb.DefyHistoryUsed = 1` on both fighters on
  Sudden Death entry ✓ behaviourally, but F13 requires **Unavailable** (barred seal) to be
  distinguishable from **Spent** (broken seal) "without clearing the underlying spent flag" — the
  pre-mark conflates them. Change: keep a separate `SuddenDeathDefyDisabled` read rather than
  overwriting the spent flag. **Size: S.**
* **Dual lethal trade** ✓ implemented (pre-frame intents; `FighterHitPipelineTests` pins it). The doc
  softened the meter nuance ("can re-earn meter from an *otherwise eligible* hit … D03h excludes
  Ultimate-origin damage") — no code change beyond D-13.
* **No echo on a defied hit** ✓ implemented in both modes.

### 8.6 F13 Defy resonance seal — combat-side state source (D-15)
**Design (master 1274).** 20×20 px persistent seal **beside** the Ultimate meter (Story HUD and
**each** fighter's meter in Fighter Mode, CPU matches included, mirrored for P2). Four states —
**Unused/not ready** (intact unfilled outline, meter < 100), **Ready** (intact filled + steady warm-gold,
unused and meter full), **Spent** (visibly broken at any meter value after the proc; refilling never
repairs), **Unavailable** (barred; mode-disabled incl. Sudden Death, or the fighter is dead — takes
precedence without clearing the spent flag). Shape/fill must distinguish states, not colour alone; no
pulsing, no extra ready chime. Localized labels `Defy: Not Ready / Ready / Spent / Unavailable` with
Systems Card copy. Scales with the 90–140% UI setting and HUD opacity. **State derived from
authoritative meter/Defy-used/life/mode after the complete gameplay update**, recomputed on gain,
spend, drain, proc, death/respawn, load and **rollback**; never persisted as a separate cosmetic flag;
never replay the proc because the HUD reloaded. Suppression and a locked/unlearned Ultimate do **not**
disable Defy. F04 puzzle authorization cannot light it at zero meter.

**Repo.** Sim exposes `verb.DefyHistoryUsed`; Story exposes `PlayerController.StoryDefyHistoryUsed`.
No HUD element, no localization keys, no event.
**Status: missing.** My slice: expose a single derived read-model (`DefyState { NotReady, Ready, Spent,
Unavailable }`) computed at the end of the gameplay update in both modes and published via `EventBus`;
UI implementation belongs to the presentation agent. **Size: S** (combat) / M (UI). Shared: `EventBus.cs`,
`en.csv` (4 keys + Systems Card section), `FighterHUD`, `StoryHUD`.

---

## 9. Time Systems

### 9.1 Echo Step determinism (D-19) — **partial, and the most invasive item in this section**
**Design (master 1286, V7.6).** Keep **31 consecutive position samples, one per authoritative 60 Hz
simulation tick**, recording frame `t` **before action input/movement**, and read the **exact `t − 30`
sample — no nearest-sample approximation**. **Reset history at spawn/recovery anchors**; filled spawn
slots are **not** valid historical frames, so **require 30 subsequent ticks before use**. Snapshot the
**ring, head, latest tick, valid count, generation, armed destination, activation tick, remaining
wind-up and `EchoStepCooldownFrames` together**; commit the 30 meter and the 120-frame cooldown once at
accepted activation. Ordinary movement teleports keep continuous history; pause adds no samples.

**Repo.** `FighterEchoRingComponent` (Klotho ID 311) holds **5 samples** written every **6 frames**
(`EchoRingSampleIntervalFrames = 6`, `EchoRingSampleCount = 5`), and the destination is
`OldestRingSample()` — i.e. somewhere in 24–30 frames back depending on phase. No valid-count, no
generation, no spawn reset, no activation tick. Story mirrors it exactly
(`PlayerController._echoStepRing[5]`, `_echoStepSampleCountdown = 6`).

**Change list.** Replace the 5-slot ring with a **31-entry** per-tick ring. 31 × `FPVector2` = 62 FP64
fields; that will not fit the existing component layout pattern, so either widen component **311**
(if Klotho permits a size change on an existing ID — verify) or **retire 311 and author 312** with the
full ring + head + `LatestTick` + `ValidCount` + `Generation`. Record the sample **before** input/
movement in the tick order. Add `ResetHistory()` at spawn/respawn/stock-loss/match-start and gate
activation on `ValidCount >= 31`. Mirror all of it in Story (`PlayerController`). Snapshot/hash
coverage: `tests/Determinism/` rollback-convergence suites must include the ring.
**Size: L.** Shared: new Klotho component ID, `FighterSimulation` snapshot/hash, rollback tests,
`PlayerController`, `AGENTS.md` (component inventory).

### 9.2 Echo Step destination policy (D-20) — **missing**
**Design (master 1282).** "Exact or no teleport": **validate full-body clearance and kill/room bounds
before spending**; reject an invalid or unavailable target **without meter/cooldown cost**. **Recheck
the same target at wind-up completion**; if it became blocked, **cancel the teleport with the committed
cost retained**. No nearby substitute, no added immunity. Airborne destinations remain legal.

**Repo.** No validation at all. `TryStartEchoStep` spends meter and arms the cooldown unconditionally;
`AdvanceEchoStep` writes `fighter.Position = dest` with only a `y > 0 → IsGrounded = 0` fix-up (Story:
`GlobalPosition = _echoStepDestination`, no check). A destination inside terrain or outside stage bounds
teleports there.

**Change list.** Add a clearance predicate against `FighterStageGeometry` (walls, ceiling, blast zone,
solid floor, one-way platforms) using the fighter half-extents, called (a) at activation → refuse with
no cost, (b) at wind-up completion → cancel, cost retained. Story equivalent via a
`PhysicsShapeQueryParameters2D` test against `Environment` + room bounds (respecting the
`PhysicsCallbackGuard` rule if reached from a signal). Tests: `FighterVerbLayerTests` + new
`EchoStepDestinationTests`. **Size: M.** Shared: `FighterStageGeometry`, `PlayerController`,
`TEMPORAL_STATE_CONTRACT.md` (owned by the time-systems agent — coordinate).

### 9.3 C01c input: dedicated actions (D-21) — **missing**
**Design (master 1281, 1451).** A **direct remappable `gameplay_echo_step` action** and a **dedicated
remappable `gameplay_grab` action**; the **Block+Roll** and **Block+BasicAttack** chords become
*presets* that can be disabled. Both paths "request the same verb once, with no synthetic component
presses"; disabling the shortcut restores normal component behaviour; **input/protocol representation
must be versioned for direct intents**, and the **reserved Dash bit is not reused**.

**Repo.** `project.godot` has 14 `gameplay_*` actions; neither exists. `InputManager.Actions` mirrors
them. Sim reads the chords directly from button bits: `FighterGrabRules.ChordPressed` (`BasicButton |
BlockButton held`) and `TryStartEchoStep` (`BlockButton && RollButton`). Networking is protocol **v2**
with a reserved Dash bit.

**Change list.** Two new InputMap actions + defaults per device kind; `InputManager.Actions` constants;
`InputBindings` schema (global save payload is **schema v4** — adding two remappable rows is additive
but the settings screen's conflict/reset lists must include them); two new wire bits → **protocol v3**
with a version bump and negotiation note; sim reads the direct bit **or** the enabled chord; a settings
toggle for "chord presets". Tests: `InputBindingTests`, `FighterGrabTests`, `FighterVerbLayerTests`,
`SettingsMenu` focus-chain pins. **Size: M.** Shared: `project.godot`, `InputManager.cs`,
`InputBindings.cs`, `SettingsMenu`, `scripts/Networking/` protocol, `en.csv`.

### 9.4 Same-frame chord priority (D-22) — **missing / currently ambiguous**
**Design (master 1451, V7.6 — both chords ruled together).** In **attack recovery**, where Echo Step
and the block-cancel are both legal:
* same-frame **Block + Roll** → **Echo Step** (the priced verb wins over the free stance);
* **Block alone** → the block-cancel;
* same-frame **Block + BasicAttack** in recovery → a **grab attempt** where grabs are legal, otherwise
  the block-cancel.
One rule: **the chord beats the single input, and the priced verb beats the free one.**

**Repo.** Ordering is incidental. In `FighterMovementSystem` the tick does
`TryStartEchoStep(...)` at `:371` and then evaluates roll with
`allowRoll: !attacking && verb.EchoStepWindupFrames == 0 && !shieldStunned` at `:386` — Echo Step wins
over Roll by accident, but there is no explicit rule for Block-alone-vs-chord in recovery, and the
grab chord is evaluated in `FighterCombatSystem` (`:1635`) independently of the block-cancel path.
Story: `PlayerController:1218` / `:1410` call `TryStartEchoStep()` from two sites and `TryStartGrab()`
from a third; the block-cancel is a separate branch.

**Change list.** Author one resolver — e.g. `BasicComboRules.SelectRecoveryVerb(blockHeld/pressed,
rollPressed, basicPressed, grabLegal, echoLegal)` — consumed by both modes, so the priority table
cannot drift. Tests: new `RecoveryChordPriorityTests` (sim + Story) covering all four same-frame
combinations with grabs legal and illegal. **Size: S.** Shared: `BasicComboRules.cs`,
`FighterSimulationSystems.cs`, `PlayerController.cs`.

### 9.5 Resonance Momentum — unchanged, implemented
60-frame refund on a **connecting** Hit 3 only, cap 2 per cooldown cycle per slot:
`BasicComboRules.MomentumRefundFrames/MomentumRefundCapPerCycle`,
`FighterVerbComponent.MomentumRefundsSlotOne/Two`, Story `ApplyMomentumRefund()` keyed on
`HitboxID == "combo_3"`. ✓ **No change.**

### 9.6 Overtime — F21 retires Hybrid
**Design (master 1291).** Trigger reworded to "timed Stock or Time; **F21 retires Hybrid**". Numbers
unchanged (3,600 frames; hazard idle/recovery halved with the 1.5 s warning intact; echo ×1.5 cap 0.60).
**Repo.** `MatchMode.Hybrid` exists (`GameManager.cs:15`) and is offered in
`CharacterSelectScreen.cs:622` and `HolodeckConsolePanel.cs:102`; `FighterAudioRules:20` branches on it.
**Status:** Overtime itself is implemented ✓; Hybrid retirement is **Section 11 (match flow)** — flagged
here only because the Overtime trigger sentence lives in my section. Cross-ref, no work in D.

---

## 10. Defense: Blocking

### 10.1 D01 / D02b defence ordering (D-01) — **partial; Story is inverted**
**Design.** Order per distinct eligible hit contact: hit eligibility/invulnerability → **projectile
immunity** → **Temporal Aegis** → **HP barriers** → **ordinary block**; stop at the first layer that
fully prevents the hit. Full absorption spends **no** block charge, causes **no** block response
(no shieldstun/shatter/daze/lockout) and awards **no** successful-block perk; "holding Block does not
turn a shield absorption into a successful block". Partial absorption passes **only the remainder** to a
legal block, charged at the **original attack classification** (Basic 1, Story Guard-Crush 2, player
Special = all remaining). Henry's Bastion is granted only **after** a real block and **cannot absorb its
triggering hit**. One attack → one resolution (no overflow duplicate, no replayed damage, no duplicate
collision callback).

**Repo — sim.** `ApplyFighterHit` order is: invulnerability/stocks → **Aegis** → block. So Aegis before
block ✓; no projectile-immunity layer; **no HP-barrier layer at all** (the sim has no equivalent of
`StoryShieldPoints`, correctly — those are Story grid perks — but D02b's ordering must still be authored
for any future sim shield, and the Aegis branch must learn the throw exception, D-09).
One defect: the Aegis branch fires for **any** hit including zero-damage/zero-impulse pulses, so a
0-damage expiry knockback or a warning tick can eat the bubble. D03c's "zero-damage warnings,
non-damaging statuses and harmless overlaps consume no Aegis" makes that a defect.
**Repo — Story.** Order is: roll i-frames → **projectile immunity** (`HasStoryProjectileImmunity &&
HitboxID == "projectile"`) ✓ → **block** (`BlockSystem.ResolveHit`) → `ApplyDamage`, and the HP barrier
(`StoryShieldPoints`) is drained **inside `ApplyDamage`, after block**. That is **exactly backwards**
under D01: a blocking Shakespeare with a live Bastion spends a block charge (and can shatter) on a hit
the guard should have absorbed for free.

**Change list.**
* Extract a shared resolution order into one Story chokepoint, e.g.
  `PlayerController.ResolveIncomingHit(hit)`: invuln → projectile immunity → Aegis (once Story gets one,
  D-05) → `StoryShieldPoints` → block → HP.
* On full absorption: return early with shield-impact feedback, **no** `BlockSystem.ResolveHit` call,
  **no** `GrantHenrysBastion`, no shieldstun.
* On partial absorption: subtract, then call `ResolveHit` with the **original** `AttackClass`/
  `BlockChargeCost` and the reduced damage.
* Preserve one contact identity (the existing `PhysicsCallbackGuard` + `Hitbox` dedupe already helps).
* Tests: new `DefenceOrderingTests` covering the DEFENSIVE_EFFECTS "Pending validation" matrix —
  full/partial/exact absorption × front/rear × grounded/airborne × 0/1/2/3 charges × lockout ×
  Basic/Guard-Crush/Special/block-bypassing.
**Size: M.** Shared: `PlayerController.cs`, `BlockSystem.cs`, `ApplyFighterHit`.

### 10.2 D02a same-shield refresh (D-02) — **partial**
**Design.** A repeat grant of the **same** shield sets remaining absorption to the effect's normal cap
and **restarts** its authored duration; never adds capacity or duration; one instance per recipient +
shield-effect ID; a full shield may refresh its duration; broken/expired → fresh instance on the next
legitimate grant; **unique grant-event identity** (refused input, polling the condition, duplicate
callbacks, recreated visuals and restored state grant nothing). Cleopatra's **decoy is a separate
recipient**; an accepted **Second Glide re-entry** counts as a new entry but merely continuing/holding
the glide does not; Wardenclyffe's coil-radius membership is **not** a repeat grant.

**Repo.** `PlayerController.ConfigureStoryShield(capacity)` + `RechargeStoryShield(capacity)` is
refresh-to-cap ✓ and non-stacking ✓ (single scalar pool). But: one **shared** pool for all shield types
(fine today since each character owns exactly one perk, but it silently implements the "different
HP-barrier types" case the doc explicitly leaves **unauthored** — see DEFENSIVE_EFFECTS
"Different HP-barrier types: current authoring boundary"); **no duration** (D-03); **no grant-event
identity**; Cleopatra's Royal Aegis grants to **Cleopatra**, not the decoy (`CleopatraAbilities:327–331`);
Pocahontas's Leaf Barrier re-grants on any `ApplyLeafBarrier()` call (`PocahontasAbilities:256`) with no
Second-Glide/held-input distinction.

**Change list.** Introduce `StoryShieldInstance { EffectId, Points, Capacity, RemainingFrames,
GrantEventId }` on `PlayerController` (still one active); add the grant-identity guard; route Royal
Aegis to the decoy node. Tests: `ShieldRefreshTests` (4/10 → 10/10, no additive duration, duplicate
grant rejected, Henry's valid block vs full absorption, decoy separate recipient, Second Glide vs held
input). **Size: S–M.** Shared with the roster agent (four kit scripts).

### 10.3 D02c eight-second granted shields (D-03) — **missing**
**Design.** Henry's Bastion, Royal Aegis and Leaf Barrier each last **8 seconds of live gameplay
(480 active ticks at 60 Hz)** or until depletion; repeat grants reset to 8 s (never add); an absorbed
hit reduces capacity but does not change the timer; a broken shield disappears immediately; expiry
discards remaining absorption with **no heal, meter, block event or expiry proc**. Uses the **suspended-
effect clock** — Time Freeze, menus, world-frozen recovery and boss-rewind presentations do not consume
lifetime; no wall-clock substitution, no catch-up ticks. Royal Aegis on a decoy has its own recipient
lifetime and can end early with the decoy; no transfer of unused capacity/time. Generic
movement/ability-duration upgrades do **not** extend these timers. **Does not** apply to Wardenclyffe,
Mozart's conditional projectile protection, or Temporal Aegis.

**Repo.** `StoryShieldPoints` has **no timer of any kind** — `BlockSystem.GrantHenrysBastion`'s comment
even records the approximation ("the design's 'temporary' guard lapses by absorbing damage rather than
on a timer"). **Status: missing.**

**Change list.** Add `RemainingFrames` to the shield instance (480), tick it in `_PhysicsProcess`
**gated on the same suspension the hitstop/Time-Freeze paths already use** (`IsInHitstop`, the
`IStoryRewindSimulation` freeze sweep, pause), and clear at zero without any proc. Tests: expiry at
exactly 480 active ticks, early depletion, refresh at partial and full capacity, suspended clock, no
offline advance, decoy independence, unchanged by duration modifiers. **Size: M.** Shared:
`PlayerController.cs`, `BlockSystem.cs`, the four kit scripts, the freeze sweep.

### 10.4 D02d Wardenclyffe (D-04) — **partial, three defects**
**Design.** 15% max-HP cap (unchanged). Both **protection and recharge require Tesla inside the
authored radius of at least one of his **own** active coils** (a linked fence is not an extra radius;
overlapping coils give one shield and one rate). **Damage delay: 3 live seconds (180 ticks)** whenever
damage actually reduces Tesla's HP **or** Wardenclyffe absorption; counts down in or out of range; new
qualifying damage restarts it; hits fully rejected by invulnerability/projectile immunity/Aegis, or
blocked without reducing shield/HP, do **not** restart it; one contact reducing both starts one delay.
**Recharge: 2.5% of current max HP per live second**, capped at 15%, fractional progress retained
(empty→full = **6 s** of eligible recharge; after a damaging hit that empties it: 3 s delay + 6 s).
Damage resolves before recharge in the same update. **Leaving range immediately disables absorption and
recharge but preserves stored charge**; returning re-enables only the retained charge. Coil
destruction/expiry/replacement/Ultimate detonation recomputes eligibility. Initialize to **zero** charge
with no delay on fresh entry/first acquisition; T01a and F10 discard stored charge but **preserve the
remaining damage delay** across same-attempt recovery/load; respec removes it and reacquisition cannot
clear an existing same-attempt delay. Freeze-aware. Presentation must not show a protective bubble for a
dormant out-of-range charge.

**Repo** (`TeslaAbilities.cs:95–111`): `ConfigureStoryShield(0.15f * MaximumHP)` ✓ cap; recharge is a
**flat `ShieldRechargePerSecond = 2f` HP/s** (design: 2.5% of max HP/s — at 90 max HP that is 2.25/s, so
the flat 2 is close for Tesla but wrong by construction and wrong under MaxHP modifiers); **no damage
delay**; **absorption is never disabled out of range** (the shared `StoryShieldPoints` drains in
`ApplyDamage` regardless of coil proximity — only recharge is gated); no fractional retention concerns
(float, fine); no persistence of the delay; the code comment admits "Recharge rate is a placeholder…
the design specifies 'slow-recharging' without a number" — **the number now exists**.

**Change list.** Rate → `0.025f * Owner.MaximumHP * dt`; add `_rechargeDelaySeconds` (3.0) set from the
shield/HP-damage chokepoint; gate **absorption** on in-range (requires the shield instance to carry an
`Active` predicate consulted by `ResolveIncomingHit`); recompute on coil destruction; zero-init;
persist the delay in the per-attempt registry. Tests: the D02d validation list (3 s after qualifying
damage, 6 s empty→full, delay restart rules, partial charge across exit/re-entry, dormant protection
out of range, overlapping/foreign coils, Ultimate destruction, freeze, first acquisition, respec).
**Size: M.** Shared: `TeslaAbilities.cs`, `PlayerController.cs`, `StoryManager` attempt registry.

### 10.5 D02e / Story has no Temporal Aegis (D-05) — **partial / missing**
**Design.** Temporal Aegis: **no time expiry**, **at most one active per recipient**; a pickup while
already protected is **consumed with no second charge, reserve, duration, HP, meter or replacement
reward**; one bubble, no stacked pips or timer; commit the pickup claim and the active flag together
through the existing pickup transaction; duplicate callbacks / loading an already-claimed pickup grant
nothing; represent it as an **active flag tied to source/instance identity, not a growing charge
count**. Story ordinary load discards the shield but preserves the consumed claim; T01a Death Rewind
and F22 Sudden Death cleanup still remove it.

**Repo — sim.** `FighterRuntimeComponent.AegisHits` (an **int count**) set to `1` at the orb pickup
(`FighterEntitySystems:1830`) — idempotent in practice ✓, but the type invites stacking and the beam
path decrements it separately (`:1467`). Consumption prevents everything ✓ (D03e-compliant by early
return).
**Repo — Story.** There is **no Aegis at all**. `OrbEffect` is
`{ HPRestore, MeterBoost, SpeedBuff, DamageBoost, ShieldRestore }` — the design's four orbs are
Temporal Restoration / Chronal Haste / Tectonic Uplift / **Temporal Aegis** (master 3190, 3831).

**Change list.** Sim: rename to a flag (`AegisActive`) or pin the cap at 1 and add the zero-damage
exclusion (§10.1). Story: add `OrbEffect.TemporalAegis` and a one-hit absorb consulted in the new
`ResolveIncomingHit` chokepoint; reconcile the orb roster with the design (see D-17). Tests: survives
past 8 s, one bubble after repeated pickups, duplicate consumed with no benefit, new pickup after
consumption, no pickup during Time Freeze, death/load cleanup with consumed claims preserved.
**Size: M.** Shared with the items/Fighter-stage agent (`ChronalOrbItem.cs`, orb resources,
`FighterSimulationDriver` orb proxies, `en.csv`).

### 10.6 D03a — full HP-barrier absorption suppresses attached effects (D-06) — **missing**
**Design.** An eligible damaging hit **fully absorbed by an HP barrier** applies **no** hitstun,
stagger, knockback/launch, status or mark — **including when it exactly depletes the barrier**
(remove the depleted shield, but do not then apply the hit's effects). No queued effects for later.
Overflow to HP follows normal eligibility (including hyper-armor and the lethal/Defy ordering).
Partial absorption does **not** proportionally shorten a status or grant extra immunity. Existing
statuses are untouched; existing poison keeps ticking. Tesla's D02d delay still starts when capacity is
actually lost, even with no HP loss or attached effect. Emit shield-impact/break feedback without a
victim hurt/launch reaction.

**Repo.** `StoryShieldPoints` is drained inside `ApplyDamage`, *after* `OnHurtboxHit` has already
decided to apply knockback/hitstun/status — those branches key off `hit.Knockback`/`hit.HitstunDuration`
and `damageApplied > 0`. With a full absorb, `ApplyDamage` returns 0 and `OnHurtboxHit` returns early
at `if (damageApplied <= 0) return 0f;` — so **today a fully absorbed hit accidentally applies nothing**
✓ — but it also **never reaches the status/knockback code for the partial case correctly** and, more
importantly, the *exact-depletion* case returns 0 and is therefore also silent ✓. The real gap is
structural: once ordering moves before block (D-01), the absorb must explicitly suppress effects rather
than relying on an early return, and the partial case must pass the remainder through with full effects.
**Status: missing (as an authored rule); accidentally satisfied in one case today.**
**Change list.** Fold into D-01's `ResolveIncomingHit`: `absorbed == damage` → early return with shield
feedback; `absorbed < damage` → continue with `damage - absorbed` and full effects. Tests: the D03a
example set (20-damage poisoned hit vs 30/20/10-HP barriers). **Size: M** (rides D-01).

### 10.7 D03b — Unblockable vs barriers/Aegis (D-07) — **partial**
**Design.** Unblockable (player Ultimates + authored boss unblockables) bypasses the **ordinary
charge-based block only**; eligible Aegis and HP barriers still absorb in the D02b order; only the
remainder reaches HP; **no** block charges spent, **no** shieldstun/shatter, **no** successful-block
perk, whether fully absorbed or not. The separate **player-Ultimate bypass of enemy damage-reduction
defenses** (Tech Enforcer bubble, Shock-Shield Legionnaire `FrontalDamageReduction`) is retained and is
**not** the same thing. The red/X Unblockable telegraph and the attack's class identity survive every
layer.

**Repo.** Sim: Aegis is checked before the block branch and applies to all classes ✓; the block branch
excludes `UltimateAttackClass` ✓; no HP barrier exists. Story: `BlockSystem.ResolveHit` returns
`NotBlocked` for `hit.Unblockable || BypassesBlock(Ultimate)` ✓, but `StoryShieldPoints` sits after
block, so an unblockable *does* currently hit the barrier — order-correct by accident, effect-wrong once
D-01 lands. Enemy damage-reduction bypass: verify `FrontalDamageReduction` handling is Ultimate-exempt
(EnemyController) — present per AGENTS, unchanged by this diff.
**Change list.** Assert the rule inside `ResolveIncomingHit`; ensure the class label is preserved through
absorption (no relabelling to Basic/Guard-Crush). Tests: `StoryBlockClassificationTests`,
`FighterHitPipelineTests`. **Size: S.**

### 10.8 D03c — shields absorb DoT and hazard ticks (D-08) — **missing**
**Design (Option B).** Actual HP-damaging ticks from an already-applied status (e.g. Venom) **and**
ordinary external damaging hazards (spikes, lava, stage-hazard ticks) **can be absorbed** by Aegis and
active HP barriers. Route each scheduled occurrence through the shared damage resolver **once**, with
its source/status instance ID and tick index. A status tick is **not** a projectile (projectile-only
immunity does not reject it). An absorbed tick **still counts as that scheduled occurrence** — no
cleanse, no duration refresh, no delayed damage debt, no immediate retry. Unabsorbed status-tick damage
reaches HP under the normal resolver; **external stage-hazard** remainder keeps its **Basic-class block
rule (1 charge)**; finite-shield coverage does **not** create a block opportunity against an
already-attached DoT. Ticks keep **zero hitstop**. Zero-damage warnings, non-damaging statuses and
harmless overlaps consume **no** Aegis or barrier HP. **Radiant Burn remains a vulnerability modifier
with no damage ticks — do not invent a DoT for it.** Pit kill boundaries, Integrity loss, Siphon Snare
and other non-HP drains never spend shields. Tesla: a tick reducing Wardenclyffe charge or HP restarts
the 3 s delay; a tick consumed by earlier Aegis does not.

**Repo.** Story Venom (`VenomStrategy.OnTick`) calls `PlayerController.ApplyPersistentDamage` →
`ApplyDamage(..., ignoreRollInvulnerability: true)`, which **does** drain `StoryShieldPoints` ✓ by
accident, but bypasses the new ordering (no Aegis layer, no per-tick identity). Sim: `ApplyFighterHit`'s
Aegis branch fires on tick pulses ✓ but also on 0-damage pulses ✗. Hazard ticks in the sim pass
`HazardAttackClass` with `HazardBlockChargeCost = 1` ✓ (matches the Basic-class block rule) and
`appliesHitstop: false` ✓.
**Change list.** Give tick sources a stable `(sourceInstanceId, tickIndex)` and route them through the
same resolver; add the zero-damage guard to the Aegis branch in both modes; keep the hazard remainder's
1-charge block. Tests: D03c's list (Aegis eats one poison tick, the next tick hits the barrier, status
lifetime unchanged, no DoT block, hazard overflow 1 charge, Radiant Burn produces no tick, zero hitstop,
Tesla delay reset). **Size: M.** Shared: `StatusController.cs`, `StageHazard`, `ApplyFighterHit`.

### 10.9 D03d — primary throws bypass finite shields (D-09) — **missing**
**Design.** A legal grab is not prevented by HP barriers or Aegis, and the resulting **primary throw
deals normal damage and applies its normal launch without consuming those protections** — skip Aegis
and HP absorption **as well as** ordinary block at the throw's scheduled damage/launch event. Do not
decrement capacity, trigger an absorption/break, grant a block perk, or treat the skipped protection as
partial reduction. Retain 1.0× BasicAttackDamage, authored impulses, weight and low-HP scaling, no DI,
no statuses, existing hold/animation/throw-immunity timings. Normal HP damage, eligible **Defy**, death
and Rally/meter accounting resolve once. A surviving victim keeps unspent protection at its normally
elapsed lifetime (no refresh, no pause because the actor was grabbed). Throw HP damage **does** restart
Tesla's D02d delay even though capacity was not spent. Mark the contact as the primary hit of a
validated paired grab/throw event (paired actor IDs + the existing damage-applied guard); **the Story
secondary thrown-mob collision is a separate projectile hit with normal defenses** and does not inherit
the bypass.

**Repo — sim.** `ResolveThrow` calls `ApplyFighterHit(..., BasicAttackClass, ...)` — so an active
`AegisHits` **absorbs the throw entirely** (early `return false`), which is precisely the case D03d
forbids. It does clear `victim.InvulnerabilityFrames = 0` first ✓ and suppresses DI ✓.
**Repo — Story.** `StoryGrabTests` cover the mob path; a mob has no Aegis/barrier, so no current defect,
but the rule needs authoring for symmetry.
**Change list.** Add a `bypassesFiniteShields` (or `isPrimaryThrow`) parameter to `ApplyFighterHit`,
skipping the Aegis branch and any future barrier layer while leaving block untouched (the victim is
held, so block is already unreachable). Mirror in the Story throw path. Tests: `FighterGrabTests` —
"throw against an active Aegis deals full damage and leaves the bubble". **Size: S.**

### 10.10 F15 — Divine Piercing / The Emancipator are full-shatter Specials (D-16) — **retired rule still shipped**
**Design (master 1367).** Both use **ordinary Special-class full shatter** in Story **and** Fighter
against charge-based shields: on a valid blocked contact consume **all remaining charges (1, 2 or 3 → 0)**
and apply the **existing Special shatter response** (shatter-freeze, daze, lockout). They are **not**
two-charge Guard-Crush exceptions. At zero charges no stance exists; rear/unblocked contacts use normal
hit resolution without consuming charges. For **Divine Piercing's multi-hit execution the first absorbed
contact shatters once**; duplicate callbacks cannot repeat the shatter or the perk award; later distinct
contacts follow normal hit eligibility with no extra absorption/invulnerability. **Damage, hit count,
cooldown and startup unchanged.** Use the **full-shatter Special visual signature**, not the two-charge
Guard-Crush diamond. The F06 Shield of Orléans distinct-block meter reward still applies, but its
**Guard-Crush charge refund does not trigger** from these Special-class attacks.

**Repo.** The retired rule is authored in four places:
* `scripts/Combat/BlockSystem.cs:158–178` — `DepleteCharges(int)` with the doc comment naming both moves.
* `scripts/Characters/Abilities/JoanAbilities.cs:159` — `blockSystem.DepleteCharges(BlockChargeDepletion)`.
* `scripts/Characters/Abilities/LincolnAbilities.cs:143` — same.
* `scripts/FighterSim/FighterSimulationSystems.cs:1494` `DivinePiercingBlockChargeCost = 2` and `:2155`
  `attacker.CharacterID == FighterCharacterID.Joan ? DivinePiercingBlockChargeCost : 0`.
  (Lincoln's S1 in the sim already passes 0 → already full shatter, so the two modes disagree today.)
`ApplyFighterHit`'s comment likewise enshrines it ("shield-shredding specials like Joan's Divine
Piercing deplete exactly 2 charges").

**Change list.** Delete the two Story `DepleteCharges` call sites (let the normal `ResolveHit` path run
with `AttackClass.Special`), delete `DivinePiercingBlockChargeCost` and the Joan branch in the sim,
keep `DepleteCharges` only if something else needs it (nothing does — consider removing it and its
`BlockChargeDepletion` constants), add a once-per-execution shatter guard for Divine Piercing's multi-hit,
and swap the telegraph/VFX signature. Tests: `StoryBlockModelTests`, `JoanContentTests`,
`LincolnContentTests`, `FighterHitPipelineTests` — 1/2/3-charge cases all end at 0 with daze + lockout;
multi-hit dedupe. **Size: S.** Shared with the roster agent (VFX signature) and `en.csv` (Move List copy
via `MoveListScreen`, which sources live numbers).

### 10.11 F17 — Aegis must not end the shatter lockout (D-17) — **defect shipped**
**Design (master 1373, 3190, 3831).** Temporal Aegis is a **separate one-hit shield**; collecting it
**does not restore block charges, end shatter lockout, or change the block-regeneration countdown**.
Normal regeneration and **the existing perk exceptions** (Shield of Orléans can refund one charge after
a Guard-Crush shatter, but that charge **stays unusable until the 5 s lockout ends**) are the only
specified block-recovery rules. The prior V7.3 sentence — *"A Chronal Shield-Restore orb ends the
lockout along with restoring charges (the orb is the authored fast exit)"* — is **deleted**.

**Repo.** `BlockSystem.RestoreAllCharges()` explicitly zeroes `_lockoutTimer` with the now-retired
rationale in its doc comment, and `ChronalOrbItem.OnPickedUp` calls it for `OrbEffect.ShieldRestore`.
The whole `ShieldRestore` orb is also absent from the design's four-orb roster.

**Change list.** Remove the `_lockoutTimer = 0f` line from `RestoreAllCharges()` and rewrite its comment;
retire `OrbEffect.ShieldRestore` (and `MeterBoost`/`DamageBoost`) in favour of the authored four, or at
minimum stop it from touching the lockout. Tests: `BlockShatterLockoutTests` — "a charge restore during
lockout leaves the stance unavailable until 300 frames elapse". **Size: S.** Shared: `ChronalOrbItem.cs`,
orb `.tres` resources, the items agent, `AGENTS.md` (which currently records the retired rule verbatim:
"a Story orb shield-restore ends the lockout").

### 10.12 Grab chord drops the stance (F23 clause)
**Design (master 1363).** "Ordinary attack and ability inputs are ignored while the stance is up; **an
accepted Grab chord or dedicated Grab input drops the stance and enters `Grabbing`** under the shared
grounded grab gate (F23)."
**Repo.** Sim: `FighterGrabRules.CanStartGrab` requires `ShieldStunFrames <= 0`, grounded, etc.; the
stance is not an explicit precondition and the grab sets `GrabPhase`, with the block stance derived from
`IsBlockStance` — confirm the stance actually drops for the whole grab (the V7.3 "a grabbing player has
no block shield" clarification). Story: `TryStartGrab` excludes `Grabbing`/`Rolling` states and
transitions to `CharacterState.Grabbing` ✓ (which is not `Blocking`, so the stance drops ✓).
**Status: implemented**, pending the dedicated-input variant (D-21). Add a pin that `IsBlockStance` is
false for the entire grab including whiff recovery.

### 10.13 Block-cancels-hitstun conditions
**Design (master 1375).** Unchanged mechanically; now spells out that the Hit 2 → Finisher escape
requires a **grounded victim with at least one usable block charge, no shatter lockout, and no other
action lock preventing the stance**; "holding Block at zero charges does not grant an escape".
**Repo.** Sim: `FighterBasicAttackRules.IsBlockStance` gates on charges + `BlockLockoutFrames`;
`HitstunBlockCancelBlocked` arms on `combo_1` ✓. Story: `BlockSystem.CanRaiseStance` (charges > 0 &&
no lockout) ✓ and `_hitstunBlockCancelBlocked = hit.HitboxID == "combo_1"` ✓. Grab states kill the block
input ✓ (V7.3).
**Status: implemented.** Only the F09 measurement obligation remains (D-18).

### 10.14 Recharge, shieldstun, shatter, zero-charge stance
All unchanged and implemented: `BlockChargeRegenFrames = 180` in both modes ✓; regen in all states
except Blocking and Dead ✓ (`BlockSystem._PhysicsProcess` returns while `_isBlocking`; sim
`BlockRegenFrames`); `ShieldstunFrames = 8` ✓; `ShatterFreezeFrames = 16` ✓; 1.0 s daze
(`DazeFrames = 60`) ✓; `BlockShatterLockoutFrames = 300` with regen held ✓; zero charges → no stance ✓.

---

## 11. Enemy Attack Classification vs Block

**Design (master 1377–1386).** Standard/elite mob attacks are Basic-class (1 charge) ✓;
**Guard-Crush (2 charges)** is now explicitly "a separate **Story enemy** classification" and F15
removes the two player specials from it; **Unblockable (boss-only)** gains the D03b clause (eligible
temporary protection still absorbs; the remainder bypasses block); telegraph colour + glyph dual channel
unchanged; PvP Special-class visual signature unchanged; "no single enemy hit ever full-shatters"
unchanged; the player-Ultimate-vs-enemy-damage-reduction companion ruling gains the D03b distinction
(that bypass targets `FrontalDamageReduction`-style **reduction** fields, **not** finite HP barriers or
Aegis).

**Repo.** `EnemyAbilityData.IsGuardCrushing` / `IsUnblockable` ✓; `EnemyAbilityExecutor:285` and
`EnemyProjectile:81` set `BlockChargeCost = guardCrush ? 2 : 0` ✓; `TelegraphGlyph.cs` draws
circle/diamond/X ✓; `BlockRules.ChargeCost` handles Basic/Hazard = 1, Special = all ✓.
**Status: implemented** apart from the D03b/D-16 consequences already listed. **No new work** beyond
D-07 and D-16.

---

## 12. Enemy Stagger Discipline

### 12.1 Core four rules — implemented
`EnemyStaggerRules` constants all match the design (`GetupArmorFrames 36`, `EliteStaggerBudgetSeconds
2.0`, `BossStaggerBudgetSeconds 1.5`, `StaggerDecayPerSecond 1.0`, `EliteArmoredRecoverySeconds 1.5`,
`BossArmoredRecoverySeconds 1.0`, `SpecialStunDiminishWindowSeconds 4.0`, `SpecialStunDiminishFactor
0.5`) ✓. `EnemyController.ApplyStun(duration, minimumSeconds, fromSpecial)` implements armor denial,
diminishing special stun, elite budget → `BeginArmoredRecovery()`, and the pressure-exit rule ✓.
Bosses are flinch-proof by construction (`BossController` has no Stunned state) ✓.
Scope guard holds: nothing in `scripts/FighterSim/` references `EnemyStaggerRules` ✓.

### 12.2 F07 Static Charge accounting (D-25) — **defect**
**Design (master 1392, new).** Static Charge's **action lock counts as stun in PvE**, including when
applied by a **linked coil fence**. Route it through the **same resistance, source-appropriate
special-stun diminishing returns, armored-getup immunity, and elite/boss stagger-budget rules as
hitstun**. **If one hit applies both normal hitstun and Static Charge, run them concurrently and count
the greater resulting post-resistance duration once for that hit, not their sum — "the finisher must not
turn 0.4 seconds into 0.8."** Static Charge **cannot persist as a separate input lock after stagger
protection ends the effective stun**: entering armored recovery ends its lock and **rejects
reapplication during that protection**; naturally ending the combined stun grants the normal getup
recovery **once**. **Conductive causes no action lock and contributes zero stagger budget**, and may
remain while the target acts in armor.

**Repo** (`EnemyController.cs`): `TakeHit` (~`:1380`) calls `ApplyStun(hit.HitstunDuration,
minimumSeconds, fromSpecial: AttackClass == Special)`, then (~`:1394`) calls `ApplyStatusEffect(...)`,
which for `StaticCharge` calls **`ApplyStun(duration)`** — i.e. a *second* `ApplyStun` with
`minimumSeconds = 0` and **`fromSpecial: false`**. Consequences:
* The **elite stagger budget is charged twice** (sum, not max). Tesla's finisher: 0.4 s hitstun +
  0.4 s Static Charge = **0.8 s of budget** — precisely the case the design forbids.
* `_stunTimer = stun` **overwrites** rather than taking the max, so a resistance-scaled Static Charge
  can *shorten* a floored basic-string stun.
* A Static Charge applied by a **special** is counted as non-special, so it never diminishes and never
  refreshes the diminish window.
* `ApplyStatusEffect` occupies the control slot even when `ApplyStun` is refused by armor, leaving a
  StaticCharge status with no lock (cosmetically wrong; the design wants reapplication rejected during
  protection).

**Change list.** Make the hit a single stun event: compute `effectiveStun = max(postResistanceHitstun,
postResistanceStaticCharge)` in `TakeHit` and call `ApplyStun` **once** with the correct `fromSpecial`;
have `ApplyStatusEffect(StaticCharge)` set only the visual/slot state (or route through a
`fromHitPayload` flag so it does not re-enter `ApplyStun`); refuse the status application while
`IsStaggerArmored`; clear the lock on entering armored recovery; grant getup armor once for the combined
stun. Also cover the **linked coil fence** source (`TeslaCoilNode` fence ticks). Tests:
`EnemyControllerTests` (+4: finisher budget = 0.4 not 0.8; max-not-overwrite; special-sourced Static
Charge diminishes; armored recovery rejects reapplication), `EnemyStaggerRulesTests`,
`TeslaContentTests`. **Size: S.** Shared: `EnemyController.cs`, `BasicComboRules` (Tesla rider),
`TeslaAbilities.cs`.

### 12.3 No Juggling scope wording
Master 1389/1438 now says Fighter Mode "retains its current escape verbs, whose coverage remains subject
to F09 validation" (was "keeps the escape verbs as its entire answer"). No code change; it is D-18.

---

## 13. Combo & cancel rules, Story Death Rewind combat state

### 13.1 Cancels — unchanged, implemented
24-frame buffer ✓, special/ultimate cancel a swing at any point ✓, block cancels only in recovery ✓,
no combo counter overlay ✓.

### 13.2 T01a Death Rewind combat-state cleanup (D-37) — **partial**
**Design (master 1440–1444, rewritten and retitled from "Rewind Enemy & Projectile Rules").**
* **World suspension:** all enemies **and surviving combat effects** freeze during the presentation —
  pause AI/FSM, **statuses, lifetimes and attack/tick schedules**; resume without catch-up damage,
  healing or shots. `PathMovingPlatform` reversal remains the spatial exception.
* **World effects (Option A):** clear active on-screen **enemy** projectiles at initiation. **Surviving
  constructs, independent zones, already-emitted player projectiles and effects on surviving enemies
  retain current state and remaining lifetimes**; preserve ammo, hit records and per-instance spent
  allowances (never refresh or duplicate). Existing exceptions: Tesla's death clears his **Conductive**
  marks though coils survive; Siphon tethers break; Nexus puzzle authorization/effects clear. Enemies
  stay at their positions.
* **Hero effects (Option A):** remove temporary buffs/shields, debuffs and marks; **cancel attacks/casts,
  the armed Echo Step, grabs/throws and attached effects**. **Retain** permanent kit/grid benefits,
  actual block charges/lockout and remaining cooldowns. **Clear Rally without HP**; settle only
  **still-uncredited damage-taken meter** once at its normal rate/cap in the F10 recovery transaction,
  with no retroactive Defy. Cancel pending healing without refund. Cleanup grants no attack,
  shield-break/expiry proc or reward. Then the existing 2 s recovery protection.
* 2.0 s spawn invincibility on landing (unchanged).

**Repo.** The freeze sweep exists (`ChronalRewindManager` + `IStoryRewindSimulation`, group-based,
covering enemies, extractors, constructs, the Dilation Field) and enemy projectiles are cleared ✓.
Nothing implements the **hero-effects cleanup list**: `StoryShieldPoints`, statuses, the armed Echo Step
wind-up, an in-progress grab, `_echoPool`, and pending healing all survive a rewind today; the
"uncredited echo meter settled once" transaction does not exist.
**Change list.** Add `PlayerController.ApplyDeathRewindCleanup()` invoked from `ChronalRewindManager`
before the landing: clear shield instance, both status slots, `_echoPool`/`_echoDrainPerFrame` (settling
uncredited damage-taken meter once), `_echoStepWindupFrames`, `ReleaseGrabState()`,
`CancelActiveAttack()` + `InterruptActiveAbilities()`, cancel pending heals; **keep** block charges,
lockout and cooldowns. Tests: new `DeathRewindCombatCleanupTests`. **Size: M.** Shared with the
time-systems/Story agent (`ChronalRewindManager`, `TEMPORAL_STATE_CONTRACT.md`).

---

## 14. Grabs & Throws

### 14.1 Numbers — unchanged, implemented
10/4/24 frames ✓, reach 0.8 units ✓ (`GrabReachUnits`; Story `GrabReachPixels = 0.8 × 62.5`), grab
clash 8-frame bounce ✓, 30-frame decision window ✓, 12-frame invulnerable throw animation ✓, 20-frame
throw immunity ✓, throw damage 1.0× ✓, forward/back 3.0× and up 2.5× ✓, no DI on throws ✓, whiffs vs
hitstun/daze/**shieldstun**/airborne/rolling/invulnerable ✓, unblockable and ignores hyper armor ✓,
grounded only ✓, Story crowd-bowling 0.5× ✓, elites/bosses grab-immune ✓, no enemy grabs ✓.

### 14.2 F23 FSM states (D-23) — **partial**
**Design (master 1452).** `Grabbing` **and `Thrown`** belong to the **canonical enum and transition
table**; their **phases, paired actor IDs, timers and once-only release/damage state** are required in
Fighter snapshots on the ID-310+ component.
**Repo.** `CharacterState` has `Grabbing` (`PlayerController.cs:33`) but **no `Thrown`**. The sim carries
`GrabPhase`, `GrabPhaseFrames`, `ThrowDirection`, `BeingHeld`, `ThrowImmunityFrames` on
`FighterVerbComponent` ✓ — but **no paired actor ID** (the partner is resolved positionally by the
two-fighter systems) and no explicit once-only damage guard beyond the phase machine.
**Change list.** Add `CharacterState.Thrown` and its transitions (victim: grab-connect → throw launch),
add `GrabPartnerPlayerID` + a `ThrowDamageApplied` flag to the sim component, update the canonical-enum
docs. Tests: `StoryGrabTests`, `FighterGrabTests`, any state-enum pin. **Size: S.** Shared:
`PlayerController.cs` (enum is read by animation/HUD code), sim component, `AGENTS.md` (state list).

### 14.3 D03d throw bypass — see D-09 (§10.9).

---

## 15. Character Data Architecture

### 15.1 `ActiveStatuses : StatusSlots` (D-31) — **partial**
**Design (master 1536, 1642, V7.6).** The runtime row is now `ActiveStatuses : StatusSlots` — a
two-slot struct (`Damage`, `Control`), each a `StatusEffectData` (type, remaining duration, intensity) —
**replacing the single-slot `ActiveStatus`**, and the **same struct on enemies and bosses** ("the
`StatusController` attaches to enemies and bosses too"). Master 1798–1823 authors `StatusSlots`,
`StatusSlot`, and a `StatusRouting.SlotOf(StatusType)` table ("a wrong slot assignment is a compile-time
table entry, not a runtime bug"), and states `PlayerSnapshot` carries it "as six FP64/int fields".

**Repo.** The **semantics** exist: `StatusController` has `_control`/`_damage` slots and
`StatusSnapshot { Control, Damage }`; `EnemyController` has ad-hoc `ControlStatusType`/`DamageStatusType`
+ timers + intensities and its own switch; the sim packs both slots into `FighterRuntimeComponent`
properties (`StatusType`/`DamageStatusType`/`StatusFrames`/`DamageStatusFrames`/`StatusIntensity`/
`DamageStatusIntensity`). `BossController` has **no** status slots at all. There is no shared
`StatusSlots` struct and no `StatusRouting` table (`StatusController.IsDamageStatus(type)` is the
de-facto router, and `EnemyController` calls it ✓).
**Change list.** Author `StatusSlots`/`StatusEffectData`/`StatusRouting` in `FTT.Core`, re-express
`StatusController`, `EnemyController` and the sim accessors over them, attach status slots to
`BossController` (currently statuses cannot land on bosses at all — worth confirming whether that is
deliberate). **Size: S** (mechanical) but touches three controllers; the **stronger-wins overwrite** rule
(master 1828–1832, replacing newest-wins) is the **status agent's** item and must land in the same pass
or the two will disagree. Shared: `StatusController.cs`, `EnemyController.cs`, `BossController.cs`,
`FighterSimulationComponents.cs`, `StatusControllerTests`.

### 15.2 `Suppression` status + ability lock (D-29) — **missing**
**Design (master 1812, 1842, 2928).** New **control-slot** `StatusType.Suppression`, **Story-only
source** (the Eraser's Null Lance), **2.0 s at standard intensity**. It **locks Special 1, Special 2,
the Movement Ability and the Ultimate cast** (cooldowns keep ticking; inputs refused with a dull
null-tone); the **persistent gold aura desaturates to cold grey** and the outline shader drops to base
priority; the three cooldown slots and the ultimate ring show a **cold cross-out**. **Basics, block,
grab, Rally, DI, landing tech, Defy History, death rewinds and Time Freeze are untouched.** Obeys the
two-slot rule (replaces Root/Time Dilation; never touches Venom/Radiant Burn). The Null Lance bolt is
**Basic-class vs block — a blocked lance suppresses nothing**. **Fighter Mode is untouched — no
Suppression source exists in the sim and none may be added without a separate ruling.** Explicitly
required (master 2931): a `StatusType` entry, a `SuppressionEffect` strategy class, **the ability-lock
check in `BaseSpecial`/movement/ultimate input handling**, aura-smother and HUD lock visuals.

**Repo.** `FTT.Core.StatusType` (`EventBus.cs:260`) has six members, no `Suppression`. No lock check
anywhere in `BaseSpecial`/`PlayerController` ability input.
**Change list.** Enum member (append — the sim serializes status as an int, so **appending is safe** but
any authored `.tres` using numeric status indices must be re-checked); `SuppressionStrategy`;
`PlayerController.IsLegacySuppressed` consulted in the Special1/Special2/Movement/Ultimate input
branches; `GlowPresentationController` aura-smother channel; HUD cross-out (UI agent). Tests:
`StatusControllerTests`, new `SuppressionTests` (basics/block/grab/tech/Defy/rewind unaffected;
cooldowns keep ticking; blocked lance suppresses nothing). **Size: M.** Shared: `EventBus.cs`,
`StatusController.cs`, `PlayerController.cs`, `GlowPresentationController.cs`, `en.csv`.

### 15.3 Legacy Unlock ability lock states (D-30) — **missing**
**Design (master 289, 2115, 3241, 3400).** `unlockedLegacyAbilities :
Dictionary<string, List<string>>` on the save (per character: movement / special1 / special2 /
ultimate); basics, block, Rally, Death Rewind, Time Freeze and Defy History are always present from
Level 0. Each cooldown slot and the ultimate ring shows **Dormant** (not yet unlocked — dim star that
ignites on the Resonance Restored beat), **Suppressed** (cold cross-out for the status duration), or
**Clear**; **locked slots are shown, never hidden**. Move List entries for not-yet-unlocked abilities
render a **"Dormant — restored after Level N"** badge with **no frame data** (Fighter Mode always shows
the full kit). Resonance nodes gated by a still-locked ability are **hidden** (`gatedAbilityID`).

**Repo.** Nothing: no save field, no gate, no HUD state. Story ability inputs are gated only on cooldown.
**Change list (combat slice).** A `PlayerController.IsAbilityUnlocked(slot)` gate in the same input
branches as Suppression, fed by `CharacterFactory` from the save (exactly as `StoryAbilityPerks` is
today); **must be Story-only** (`CharacterFactory.CreateCharacter(..., applyStoryProgression: false)`
already exists as the isolation seam, and the Mirror Paradox clone must stay on the full kit).
**Size: M** overall; **S** for the combat gate. Primary owner: Story-progression/save agent
(save schema, `Level0` beats) and the UI agent (slot states, Move List badges). Shared:
`PlayerController.cs`, `CharacterFactory.cs`, `SaveManager`, `MoveListScreen.cs`, `en.csv`.

### 15.4 Character base stats — unchanged
The V7 stat table (master 1514–1524) is unchanged by this diff and the nine `.tres` files match it
(per AGENTS' record of the 2026-08-10 retune + 2026-08-15 jump compression). **No work.**

### 15.5 StatModifier scoping + new StatType keys (D-32) — **missing**
**Design (master 480–495, V7.6).** `StatModifier` gains **`string abilityScope`** ("Optional
ability/construct/status ID the modifier is scoped to, e.g. `relativity_rift`; null = character-wide").
`StatType` gains **ability-scoped keys — `AbilityDamage`, `AbilityRange`, `AbilityDuration`,
`ConstructHP`** (always paired with `abilityScope`) — and **character-wide V7.1 keys `RallyEchoFraction`
and `ExtractorDamage`**. `ResonanceNodeData` also gains `prerequisiteMode` (All/Any) and `gatedAbilityID`.

**Repo.** `ResonanceNodeData` (`scripts/Environment/ResonanceNodeData.cs`) has a free-form
`StatModifierKey : string` + `StatModifierValue` + `StatModifierIsPercent` + `AbilityModifierKey`, with
**no typed `StatType` enum, no `abilityScope`, no `prerequisiteMode`, no `gatedAbilityID`**.
Combat-side consumers of the two new character-wide keys do not exist: the Rally echo fraction is
computed from `BasicComboRules.EchoFractionBase/Slope × StoryDifficultyTuning` with no perk term, and
`ChronalExtractor` takes ordinary attack damage with no extractor-specific multiplier (its
`HazardDamage = 20` is the *discharge damage to the player*, a different number).
**Change list (combat slice).** Add a `RallyEchoFraction` multiplier term at the **Story** echo-accrual
site in `PlayerController.OnHurtboxHit`/`ApplyEnvironmentalDamage` only (never in the sim — Story
progression must not leak), and an `ExtractorDamage` scalar consulted where the player's hit resolves
against `ChronalExtractor`/`DamageableEnvironmentObject`. The schema work itself (typed enum, scope,
prerequisite mode, ability gating) belongs to the Resonance agent. **Size: M** overall, **S** for the two
combat consumers. Shared: `ResonanceNodeData.cs`, `ResonanceProgression.cs`, all nine grid `.tres`,
`PlayerController.cs`, `ChronalExtractor.cs`.

---

## 16. Enemy & Boss Data Architecture

### 16.1 `EnemyData` / `BossData` schemas — no new fields
The only `EnemyData` change is the `weight` description ("no juggle-decay system is authored; escape
coverage requires F09 validation"). `BossData`'s only change is the example name
("Archive Overseer Vex" → **"Unbound Overseer Vex"**). The `ActiveStatuses` row is D-31. **No schema
work** beyond D-31.

### 16.2 Boss HP table (D-33) — **V7.6 change + a much larger pre-existing divergence**
**Design (master 1616–1630).** V7.6 lowers four rows: **Borgia Inquisitor 500 → 350**, **Siegemaster
Duke 650 → 520**, **Chronal Inventor 800 → 700**, **Revolutionary Tribunal 900 → 850**. All other rows
unchanged.

**Repo — `resources/Bosses/*.tres` `MaxHP`, measured:**

| Boss | Design (V7.6) | Repo `.tres` | Delta |
|---|---|---|---|
| Borgia Inquisitor | **350** | 500 | +150 |
| Siegemaster Duke | **520** | 540 | +20 |
| Chronal Inventor | **700** | 560 | −140 |
| Revolutionary Tribunal | **850** | 590 | −260 |
| Tidal Eraser | 1050 | 640 | −410 |
| Vulcan Decimator | 1200 | 660 | −540 |
| Dread Admiral | 1350 | 700 | −650 |
| Jackal Priest | 1500 | 700 | −800 |
| Iron Chancellor | 1700 | 780 | −920 |
| Tragedy King | 1850 | 800 | −1050 |
| Siege Cannon | 2000 | 880 | −1120 |
| Gravity Overseer | 2200 | 950 | −1250 |
| Mirror Paradox | 1000 (F20: 700/1000/1500) | 1000 | ✓ base |
| Archive Prime / **Forge Sentinel** | 2500 | 1050 | −1450 |
| Apex Eraser / **First Unbound** | 3000 | 1200 | −1800 |

Only the Mirror Paradox matches. This is not a V7.6 regression — the shipped roster was tuned to a
different curve (recorded in the now-missing `docs/PACKAGE5_CAMPAIGN_PLAN.md` §4.1, which
`DESIGN_BUILD_DEVIATIONS.md` flags as unrecoverable in the docs workspace). **Under P04 the GDD is now
intended design and the `.tres` values are build evidence**, so this is a **confirmed design/build
deviation** — arguably the largest single one in the project — and it cannot be closed by editing 15
numbers: boss HP drives encounter pacing, the dust economy, and every per-level content test that pins
arena widths against boss ranged bands.

**Change list.** (a) Open a ledger entry `VERIFY-BOSS-HP` with the table above. (b) Ask the user which
curve is intended before touching resources — the design table implies roughly a **2.1×–2.5× HP
increase** across Acts II–III. (c) If adopted: update 15 `.tres` files, re-check
`BossRosterActITests` / `BossRosterActIIandIIITests`, the per-level content suites' boss assertions,
`DustEconomyTests`, and the difficulty HP multipliers. **Size: S to edit, L to validate.** Shared with
the campaign/roster agents. **Do not silently apply.**

### 16.3 Boss renames (D-34) — **missing**
**Design.** `archive_prime` → **"The Forge Sentinel"** (L14, now "Bastion"); `apex_eraser` →
**"The First Unbound"** (L15, now "Founding"); `BossData.bossName` example → "Unbound Overseer Vex".
**Repo.** `resources/Bosses/archive_prime.tres` (`BossID = "archive_prime"`,
`DisplayNameKey = "boss_archive_prime_name"`) and `apex_eraser.tres` likewise;
`localization/en.csv:145–146` carry `boss_archive_prime_name,Archive Prime` and
`boss_apex_eraser_name,The Apex Eraser`.
**Change list.** Change the **English strings only** (keep `BossID`/`DisplayNameKey`/file names stable —
they are referenced by `content_manifest.csv`, pool configs and level controllers; renaming IDs is
gratuitous churn). Run `--headless --import` afterwards so `en.en.translation` is regenerated (the
roster suites assert names resolve through the **compiled** translation). Tests:
`BossRosterActIIandIIITests`, `CampaignLocalizationTests`. **Size: S.** Shared with the narrative agent
(the L14/L15 place names "Bastion"/"Founding" belong to them).

### 16.4 F20 Mirror Paradox difficulty HP (D-35) — **missing**
**Design (master 1628).** 1000 is the **base**; F20 authors **700 / 1000 / 1500 by difficulty before
applicable Hard perks**, and the `DistanceBased` pattern row is now "generic metadata; the **F20 campaign
CPU profile governs decisions**".
**Repo.** `mirror_paradox.tres` `MaxHP = 1000`; `MirrorParadoxController` applies it through
`PlayerController.EncounterMaxHPOverride` and drives the clone with the **Hard** Fighter CPU table via
`MirrorParadoxDecisionAdapter` — i.e. a fixed Hard profile regardless of campaign difficulty, and no HP
scaling. (Note `StoryDifficultyTuning` scales *enemy* HP at spawn, but the clone is a `PlayerController`,
so it is outside that path.)
**Change list.** Select the override from `StoryDifficultyTuning.CurrentStoryDifficulty`
(Easy 700 / Normal 1000 / Hard 1500) and take the CPU band from the F20 profile rather than hardcoded
Hard. Tests: `MirrorParadoxTests`, `Level13ContentTests`. **Size: S.** Shared with the campaign agent
(F20 is a Section 5/campaign ruling — confirm the band mapping there).

---

## 17. Rollback architecture header (context only)

Master 1648 replaces "Full technical specification…" with **S01 Option A**: `docs/ROLLBACK_STATE_CONTRACT.md`
is the required design inventory for authoritative Fighter state, input history, deterministic
reconstruction, hashing and replay-safe effects; the C# snippets are "partial illustrative sketches, not
a complete serializer or proof of determinism/performance"; implementation must map **every inventory
requirement to actual runtime fields**. Online stays deferred to Package 7.
**Consequence for this section:** every new snapshot field listed above (D04's two fields, the 31-sample
Echo Step ring + head/valid/generation/activation tick, Conductive frames, Suppression frames, grab
partner ID / once-only throw flag, Ultimate-origin attribution) must be enumerated against that contract
and included in the deterministic hash. That is one **new Klotho component (ID 312)** worth of state; do
not attempt to pack it into `FighterRuntimeComponent` (full at 128 bytes) or to quietly widen 310/311
without checking Klotho's tolerance for layout changes on an existing ID.

---

## 18. Suggested grouping for implementation

1. **Chokepoint pass (do first — everything else depends on it).** D-01, D-06, D-07, D-08, D-09, D-11,
   D-12: one `ResolveIncomingHit` in Story, one reordered `ApplyFighterHit` in the sim, plus the Rally
   pool-debit and reclaim-source fixes. M.
2. **Meter pass.** D-13 (Ultimate origin) + D-15 (Defy read-model) + F04 chokepoint guard. M.
3. **Defy pass.** D-14 (D04 protected recovery) + Defy F10 persistence + the Sudden-Death
   Unavailable/Spent split. M, needs component 312.
4. **New Klotho component 312 + Echo Step determinism.** D-19, D-20, and the snapshot fields for
   passes 3, 6 and 7. L — schedule as its own workstream; it blocks three other passes.
5. **Block-rule corrections (cheap, independent).** D-16 (F15 full shatter), D-17 (F17 lockout),
   D-25 (F07 stagger accounting), D-22 (chord priority). All S; good first landings.
6. **Shield lifecycle pass.** D-02, D-03, D-04, D-05 — one shield instance type with duration, range
   and grant identity, plus Story Aegis. M, overlaps the roster agent's four kit scripts.
7. **Status/data-architecture pass.** D-31, D-24 (Conductive), D-29 (Suppression), D-30 (Legacy gate),
   D-32 (stat keys). M, overlaps the status, Resonance and Story-progression agents.
8. **Input pass.** D-21 (two actions, protocol v3) — coordinate with the settings/networking agents. M.
9. **Data/ledger pass.** D-33 (boss HP — **ask the user first**), D-34 (renames), D-35 (Mirror
   difficulty), D-38 (authority rule in `CLAUDE.md`/`AGENTS.md`).
10. **Validation pass (gates sign-off, not code).** D-18 (F09 harness + V02 protocol), blocked on the
    three Open stages for V02.

## 19. Cross-workstream contact points

* `scripts/Characters/PlayerController.cs` — touched by D-01, D-03/04/05, D-06, D-08, D-11, D-12, D-14,
  D-20, D-22, D-23, D-24, D-26, D-29, D-30, D-32, D-36, D-37. **Highest collision risk in the repo;
  serialize edits or split by region.**
* `FighterDamageRules.ApplyFighterHit` — D-01, D-06, D-07, D-08, D-09, D-11, D-12, D-13, D-14.
* `scripts/Combat/BasicComboRules.cs` — D-22 (new resolver), D-24 (rider fields). Both additive.
* `scripts/Combat/BlockSystem.cs` — D-01, D-02, D-03, D-16, D-17, D-27.
* New Klotho component **312** — D-14, D-19, D-23, D-24, D-29 (if Fighter ever needs it — it must not),
  D-13 attribution. One author, one PR.
* `localization/en.csv` (+ `--headless --import` and the regenerated `en.en.translation`) — D-15
  (4 Defy-seal keys + Systems Card), D-24, D-29, D-34, D-21 (2 action labels).
* `AGENTS.md` — component inventory (312), the retired shield-restore-ends-lockout sentence (D-17), the
  `.tres`-is-law rule (D-38), `CharacterState` list (D-23), test baseline (currently **1638**).
* Roster/Section-5 agent — D-16 (Joan/Lincoln specials), D-24 (Tesla), D-26 (Joan node), D-27 (Shield of
  Orléans), D-02/03/04/05 (four shield kits), D-33/34/35 (boss data).
* Story/campaign agent — D-28 (Eraser), D-30 (Legacy unlock), D-36 (pits), D-37 (rewind cleanup), Defy
  F10 persistence.
* UI agent — D-15 (F13 seal), D-29/D-30 (slot lock states), D-16 (Special visual signature).
* Networking agent — D-21 (protocol v2 → v3; the reserved Dash bit must stay reserved).
