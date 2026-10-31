# Meter and Defy update: the Momentum gauge, the active Defy History and the meter rates (2026-10-31)

**Status:** planned 2026-10-31, built and closed 2026-10-31 (section 10; the total is 3879, not the planned 3863). Base commit: `56031750` (the smooth-idle push) plus this plan's commit. Baseline **3771** passing tests (`[TestCase]` attribute count under `tests/` re-counted at `56031750`: 3771). Planned end **3863** (§4). Workstream IDs `MM-*`, branches `mm/<ws>`, worktrees `D:/Projects/ftt-wt/mm-<ws>`, handoffs `docs/handoffs/MM_<WS>.md`.

**Pre-flight (orchestrator, before the plan commit).**
- The main checkout carries line-ending churn only: `project.godot` (empty diff), 260+ `*.png.import` / font `.import` sidecars and `tools/asset_src/glass/svg/*.svg`. CLAUDE.md: never commit it; `git checkout -- assets` and the svg paths, or leave it, but no worktree branches from a commit that contains it.
- `docs/recon-2026-10-31/` (the decisions file and the four dossiers) is untracked. Commit it with this plan, so every agent reads the same rulings.
- Re-point the detached integration worktree `D:/Projects/ftt-wt/fu-integ` at the plan commit. Every orchestrator import, build, full run and capture runs there; nothing launches Godot in the main checkout (the user's editor may be open).

---

## 0. Sources and scope

### 0.1 The request

The user, 2026-10-30: "Defy history seems worthless and unremarkable, you dont really notice it happen and you usually just immediately die right after. I think we should probably remove it OR do a rework of how it works and what it does. List out for me all of the special functions that affect ultimate meter … and then lets take a pass over all of these for a rework." On 2026-10-31 the user added replacing Special cooldowns with a meter (ruled: two gauges), and directed: "I want to update both the design docs and the implemented code with these changes."

### 0.2 Sources and authority

| Source | Role |
|---|---|
| `docs/recon-2026-10-31/DECISIONS_meter.md` | **Locked.** User rounds 1–7, gauge rounds G1–G5, recon follow-up R1–R4, orchestrator rulings O1–O27. Cited by ID below. Nothing here re-opens them. |
| `docs/recon-2026-10-31/A_fighter_sim.md` (A), `B_story.md` (B), `C_input_ui_bots.md` (C), `D_design_docs.md` (D) | File:line maps at `56031750`. Cited as `A§2.2`, `B:PC:5387` and so on. Line numbers drift: re-read before editing. |
| `docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` | Model for structure; its §5 pad checklist is folded into §5.3. |
| `docs/UI_GLASS_2026-10-26_PLAN.md` §1 | Model for the integration worktree, two-stage retirement and filtered-only agent runs. |
| `AGENTS.md`, `CLAUDE.md` | Architecture, determinism boundary, isolation, component-ID rule, GdUnit lock, failure signatures, `--import` rule, theme rule, legacy identifiers, one canonical value. |

Where a ruling leaves a number or detail open, this plan picks it and marks it **Provisional** (a `PROVISIONAL-*` ledger row at closeout). §0.6 answers every open question the dossiers raised that the rulings do not.

### 0.3 In scope

1. The **Momentum gauge** (G1–G5, O3–O8, O19–O22): four pips shared by Special 1 and 2, replacing every Special cooldown in both modes; the 2 s per-slot reuse delay; the lockouts kept.
2. The **active Defy History** (rounds 1–5, R2–R4, O9–O18): its own button, 50 Influence, invulnerable startup, an unblockable push-and-stagger blast, an echo cash-in heal; the passive lethal save deleted.
3. **Meter rates** (round 6, O27): Story gain ×0.35 → ×0.5, Story flat effects scaled, Fighter stock-loss retention 3/4 → 1/2.
4. HUD, presentation, input, CPU, Mirror, bots, Level 0 lesson, text, grid nodes, Move List and Systems Card for the above.
5. Design master + mirror, the contracts (both copies), the ledger, AGENTS.md, CLAUDE.md (O26).

### 0.4 Out of scope

- **The character-ability kit pass** (G5.4 "before the kit pass"): per-kit Momentum costs or gains, the Requiem → Fortissimo redesign, Shield of Orléans (7.3), Mozart's ×1.1 build rate, Splitting-Strike reach awareness.
- **Balance.** Every number not stated by the user is provisional and judged in the §5.3 session.
- **Package 10** art and audio: the Defy cue is a silent slot; the ring and pulse are code-drawn placeholders; no new sprite pose is generated.
- **LAN / Package 7.** The protocol is bumped to v5 (O2) but no transport work.
- A save schema bump, a new Klotho component ID, Training Mode, device glyphs.

### 0.5 Rulings this package amends, supersedes or closes

Each gets a closeout ledger amendment (struck through when superseded; the record stays).

| Row / rule | By | How |
|---|---|---|
| `USER-PER-HIT-MOMENTUM` (D2) | G1, G5.2, O3 | **Superseded.** The per-hit cooldown refund is deleted; its name passes to the gauge. |
| `PROVISIONAL-HIT-CONFIRM-RULES` (the 120-frame refund cap, the per-cycle counters) | O3 | Amended: the cap and counters are gone. D1/D1a and the freeze-only buffer stand. |
| `USER-ABILITY-LOCKOUT`, `PROVISIONAL-ABILITY-LOCKOUT-RULES`, `USER-HIT-CONFIRM-CANCEL`, `USER-FREEZE-SPECIAL-BUFFER` | G2.2 | Kept; cross-referenced. Defy neither reads nor arms them (5.4). |
| `USER-NOT-YET-FLASH` (D10) | R2 | Extended: a 0-pip Special press flashes too. |
| D04 (`DEFENSIVE_EFFECTS.md` :237–257), `USER-DEFY-ENDS-ON-OFFENCE` (decision 4.3) | 1.1, O16 | **Superseded.** No passive save, no 60-tick window. |
| `VERIFY-DEFY-CANCELS-OWN-ROLL`, `FIX-SIM-VENOM-DEFY-BARRED`, `FIX-CPU-DEFY-RESERVE-IN-SUDDEN-DEATH` | O16, 5.2 | Closed as moot (the passive Defy and `DefyBarred` are gone). |
| `USER-STORY-METER-SCALE` (D5 ×0.35), `PROVISIONAL-STORY-METER-SCALE-RULES` ("flats unscaled"), `PROVISIONAL-STORY-METER-GAIN-SCOPE` | 6.1, 6.2, O27 | Amended to ×0.5 with flats scaled by `FlatEffectScale`. |
| M09 Death Carryover (`design-godot.md` :1298, "25 % penalty") | 6.4 | Amended: 50 % kept. |
| F22 Sudden Death (`FIGHTER_MATCH_RULES.md`; "cooldowns ready, Defy disabled") | 5.2, O22 | Amended: Momentum set to 2 pips, Defy allowed if 50 is earned. |
| A02 activation ("hyper-armoured wind-up") | 3.3 + R3 | Amended: a Defy blast cancels an activation in its wind-up; Defy is refused once the hold has connected. |
| F14 Siphon Snare's "can disarm an unused full-meter Defy" clause | carried default | Deleted from the design; the drain follows 6.2. |
| `FIX-GRID-SCOPED-COOLDOWN-INVERTED` | G4.3, O6 | Note: the three minors are now `MomentumRegen`. |
| Package 13 Requiem → Fortissimo shave (ABILITY_DATA :25), `KitReachRules.RequiemFortissimoShaveSeconds` | G4.2 + R1, O7 | Disabled until Mozart's kit pass (`DEFER-REQUIEM-FORTISSIMO-REDESIGN`). Cleopatra's Vortex Step is **kept** (R1: it acts on Desert Mirage, a movement ability). |
| `USER-STORY-HUD-COMPACT` / `PROVISIONAL-STORY-HUD-COMPACT` (the 28 px Defy badge, 484 × 182 slab) | 4.2, G5.1, O19 | Amended: badge retired; the slab grows by the pip row. |
| F13 Defy seal (`HUD_CONTRACT.md`), the `defy_*` kit icons | 4.2, O16 | Retired. |
| `TEMPORAL_STATE_CONTRACT.md` Echo Step leftovers | "clean-ups" | Removed (owed since 2026-10-25). |

### 0.6 Planner resolutions (provisional; each becomes a ledger row or a sentence in one)

Each answers an open question a dossier raised (A§7, B§9, C, D§5) or a gap found while planning.

| # | Question (source) | Resolution | Reason |
|---|---|---|---|
| P1 | `AbilityData.CooldownDuration` defaults to **10f** (`scripts/Combat/AbilityData.cs:99`); "removing it from the .tres" (O8) would silently restore 10 s | The 18 Special `.tres` set `CooldownDuration = 0.0` explicitly (like the nine Ultimates); a content scan pins `== 0` for every Special-slot ability. | Deleting the line brings a cooldown back through the class default. |
| P2 | Regen during the pre-match countdown (A§7.10) | **No regen while `MatchState != InProgress`** (countdown, results). Every match and every Sudden Death starts at exactly 2 pips at GO. Elsewhere regen runs wherever `TickCounters` ran (O4): captured, knockdown, respawn platform. | O4 copied the cooldown tick sites, but cooldowns started at 0 so the countdown never mattered to them; a 180-frame head start (+0.25 pip) would falsify G3.1. §0.7 Q3. |
| P3 | Story regen vs `StoryCooldownMultiplier` (B§1.2) | Story regen = `dt × (1/12 pip/s) × MomentumRegenMultiplier`; the reuse delay ticks with plain `dt`; only the movement timer keeps `cooldownDt`. | No grid node feeds the multiplier; the gauge is not a cooldown. |
| P4 | Same-tick blast vs strike (A§7.7) | Sim: the blast resolves in `FighterCombatSystem` **before** the intents; the Defier is still invulnerable on frame 6, so a same-tick strike on it whiffs. Story: the blast resolves in the Defier's own `_PhysicsProcess` on frame 6. | The invulnerable frame covers the blast tick. |
| P5 | Two Defies (not raised) | Each Defier is invulnerable through its own frame 6, so **simultaneous Defies blast nothing** (both pay 50). A Defy started 1 or more ticks later blasts the earlier Defier in its recovery. `VERIFY-DEFY-MUTUAL`. | Falls out of the frame table. |
| P6 | What the blast cancels on its victim (A§7.5) | The stagger is hitstun, so it interrupts the victim's swing, Special cast (`FighterSpecialCastRules.IsInterrupted`), grab startup and hold, Foresight stance (the blast is not a strike and never triggers the counter) and an A02 activation in **Windup or Active** (R3; the caster's meter stays spent). An A02 Cinematic cannot occur (the Defier would be captured, so refused). | R3 + the existing interrupt rules. |
| P7 | Blast push (O12 "planner picks") | Assigned, not weight-scaled: sim `(±6, +6) u/s` (away, up); the victim becomes airborne non-launching and flies about 2.4 u in about 24 ticks, landing inside its stagger. Story `(±360, −360) px/s` on the hero, clone and mobs; bosses take the same vector through `BossController.ApplyKnockback` unless `IsKnockbackImmune`. | A grounded push decays ×0.79 a tick under the FU-KNOCK shove (about 0.6 u); a small air pop reads as "blasted away". |
| P8 | Blast vs edges, walls, pits (not raised) | Story mob blasts obey the mob's knockback edge rule (S1, cancelled at a drop) and the encounter-lock clamp; the blast never arms D9 or the Fighter wall bounce (no launch, no damage). A Fighter victim pushed over an Open-stage pit can lose a stock to the blast zone. | "Cannot kill" = no damage; ring-out is positional. §0.7 Q2. |
| P9 | Who a blast affects (R4, O13) | Characters hostile to the Defier within 2.5 u of its body centre: the opposing fighter; standard and elite mobs not dormant and not `DrillInvulnerable` (summons included); bosses not arena-warded; the Mirror clone (for the hero) and the hero (for the clone). Never projectiles, constructs, zones, props, strike surfaces or allies. An invulnerable target (roll i-frames, its own Defy startup, respawn protection, post-rewind) is untouched. | O13 + R4; the ward and invulnerability already refuse hits. |
| P10 | Heal timing, empty pool (A§7.9, B§9.10) | On the blast frame: `heal = min(missingHP, max(round(echoPool), ceil(MaxHP / 10)))`; then `echoPool = 0`, drain 0, uncredited damage-taken meter forfeited. An empty pool still heals 10 %. | O14 verbatim. |
| P11 | Frame clock | Sim 323 `DefyFrames` counts 1…30 (0 idle): 1–6 invulnerable, blast on 6, 7–30 recovery, 0 on the tick it would reach 31. Story `CharacterState.Defying` (appended after `Thrown`) with `_defyFrame` 1…30. Both clocks pause in the Defier's own hitstop, so the 8-frame blast freeze delays its recovery: the Defier is free 38 frames after the press, its victims at 6 + 8 + 30 = 44, so the Defier is **+6**. | One table for both modes. |
| P12 | Sim buffer (O10) | 323 `ActionFlags` bit 6 `ActionFlagDefyBuffered` holds a press made on a hitstop tick; the Defy begins on the thaw tick (the last freeze tick the systems read, the freeze-buffer precedent) and overrides that hit's pending launch, hitstun and knockdown. | O1 reserves bits 5 and 12–15; bit 6 is another free zero-byte bit. |
| P13 | Gauge on the Fighter proxy (B§9.13) | The Story gauge runs on every `PlayerController` that is not a Fighter presentation proxy (campaign hero, Mirror clone, drills). The driver feeds the proxy's reuse timers as today; nothing reads a proxy's gauge. | The Fighter HUD reads the sim through the driver. |
| P14 | No-pip vs reuse-delay feedback (B§9.14) | Flash (`NotYetFlash`) when the slot is unlocked, not Suppressed, not time-frozen, its reuse delay is clear, and the press was refused by the lockout **or** by 0 pips. A reuse-delay refusal does not flash (its sweep shows). Story checks the lockout first, then pips. | R2 names only 0 pips. |
| P15 | Lens precedence (C§2.4) | `Dormant > Suppressed > Starved` (dimmed; a running reuse sweep still draws) `> Cooling` (reuse or lockout) `> Ready`. | A resource state and a time state can both show. |
| P16 | Grid minors' scope (B§9.2, 9.3) | `MomentumRegen` is **character-wide**: the three nodes drop `AbilityScope`, keep `GatedAbilityID = "special2"` (dormant until Special 2 returns), value 0.1 → regen ×1.1 via `CharacterFactory.ApplyActiveStoryProgression`. `CooldownReduction` stays for its unscoped reader (`StoryCooldownMultiplier`, the movement timer). | O6; the dormant gate stays truthful. |
| P17 | Requiem constant (O7) | `ShaveFortissimo` becomes a documented no-op in both modes; `KitReachRules.RequiemFortissimoShaveSeconds` (2.0) is deleted and its value recorded in the DEFER row. | An unread constant is a second value in waiting. |
| P18 | 306 `Special*CooldownFrames`, `AttackIntent.CooldownFrames` (A§1.1) | Deleted by MM-SIM. | Dead hashed state. |
| P19 | 310 freed ints | `MomentumRefundedFramesSlotOne` → `MomentumUnits`; `…SlotTwo` → `SpareInt0`; `DefyHistoryUsed` dead until MM-SWEEP renames it `SpareInt1`. 310 stays 128 B with 8 B spare. | Stable layout; every reader compiles between merges. |
| P20 | Story persistence key (B§5) | `AbilityCooldowns["momentum"]` (pips, float). Absent → 2.0. Restored `special1` / `special2` clamp to at most 2.0 s. | O21, additive with a legacy-meaning default. |
| P21 | Story Defy refusals (O9 "scripted pauses") | Refused when Dead or `CurrentHP <= 0`; `TimeFrozen`; `ChronalRewindManager.IsWorldHeld`; during the death-rewind sequence; during the hero's own Ultimate (any phase, the Story startup armour included); while `Level00HoldFight` runs; during a boss-intro or world-suspension freeze of the hero; while already `Defying`; below 50 meter. The tree pause needs no rule. MM-SDEFY maps each to its existing predicate and lists them in the handoff. | O9 made concrete. |
| P22 | Blast vs V7.4 armour (B§9.7) | `EnemyController.ApplyDefyBlast` bypasses `IsStaggerArmored` and the elite budget, charges no standard poise and no D4 chain step. | O12. |
| P23 | Boss window (B§9.8) | `BossController.ApplyDefyBlast`: `CancelTelegraphIntoRecovery()` when the executor is in telegraph **or active**, then push unless immune; no stun, damage or phase change. | O13. |
| P24 | Hand-built CPU observation (A§7.14) | New `CpuDecisionObservation.MomentumKnown` (default false): false → the gauge is ignored and only the reuse fields gate, so every hand-built observation behaves as today. The real `Observe` and the Mirror adapter set it true. | 0 units is a real state, so 0 cannot also mean "unknown". |
| P25 | CPU Defy numbers (O23) | Easy never. Medium: `DefyStringPercent` 35, one roll per hitstun instance once the string reaches 3 hits, drawn only at Influence 50+. Hard: no roll; Defies at Influence 50+ when the string reaches 2 hits, or when cornered (`CorneredMargin`, target on the open side), or when the target is in A02 Windup within 3.0 u; otherwise never. "String" = hitstun instances with at most 8 actionable frames between them, counted input-side. | O23's triggers; the draw gate keeps Easy's RNG stream. §0.7 Q4. |
| P26 | Bots beyond O24 | `play`: Influence 50+, in hitstun within a string (2+ hits in 60 frames), 2+ enemies within 250 px, 300+ frames since its last Defy. `spam`, `basics`, `bypass`, `feel`: never. | Probes stay comparable. |
| P27 | First-use tooltip | New `FirstUseTooltips.DefyButton = "defy_button"`, key `tooltip_first_use_defy_button`, on the first tick the meter reaches 50 in a level (skipped-calibration players, as today). `defy` and `tooltip_first_use_defy` retire. | O25. |
| P28 | Kit icons | All four `defy_*` icons and their bold twins are deleted (O16); the text names the button. | O16 is locked. |
| P29 | Cues | Add `CoreMechanicCueIDs.DefyBurst = "defy_burst"` (Critical, silent); retire `DefySealShatter` and `DefyToll`. | Nothing raises `DefyToll` after O16. |
| P30 | Pose | Both modes show `ultimate`'s first key pose for frames 1–6 and `idle` through recovery. | No new art (`PROVISIONAL-DEFY-POSE`). |
| P31 | Design catch-up (D§5.7) | Full catch-up **inside the spans rewritten** (D2 history, ×0.5, L3 Time Freeze, the Echo Step leftovers); owed lines elsewhere stay owed. | O26. |
| P32 | Save derivation (D§5.9) | None (P20); the Defy fields are dead. `STORY_PERSISTENCE.md` records it. | No bump. |
| P33 | Snapshot size | About **−40 B per snapshot**: 323 +8, 312 −32, 306 −16. (The orchestrator's "+8 B" is 323 alone.) If 306 drops to 128 B or less, the pre-existing `KLSG_ECS004` warning goes; CLAUDE.md is updated either way. | §1.5 arithmetic; the closeout re-measures. |

### 0.7 Questions the rulings cannot answer (answered 2026-10-31: every default confirmed by the user; Q2 and Q4 explicitly)

| Q | Question | Default |
|---|---|---|
| Q1 | The name stays "Defy History" (assumed in round 5). | Kept. |
| Q2 | Is a blast ring-out over an Open-stage pit a "kill"? | Allowed (P8); `VERIFY-DEFY-BLAST-RINGOUT`. |
| Q3 | P2 departs from O4's literal tick sites for the countdown. | No regen before GO. |
| Q4 | The Hard CPU Defies on every qualifying trigger (P25) and may starve its Ultimate. | Deterministic; §5.3 row 11. |

---

## 1. Standing decisions (every workstream)

### 1.1 Authority and docs
- Implementation workstreams do **not** edit `design-godot.md`, the master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md`, `docs/design-contracts/*.md` or their docs-3 masters, the ledger, `AGENTS.md` or `CLAUDE.md`. Phase D writes them. Each handoff lists the lines owed under "For the docs" (design line numbers at `56031750`).
- Rulings bind. A workstream that finds one unworkable stops and reports.

### 1.2 Mode scope and Story/Fighter isolation
- Both modes: gauge, reuse delay, lockouts, Defy action, blast, heal, cost (1.3, G3.4).
- Fighter only: stock loss 1/2, the Sudden Death values, protocol v5. Story only: ×0.5, `FlatEffectScale`, `MomentumRegen`, the Dormant overlay, persistence, Level 0, the Time Freeze refusal.
- **Nothing under `scripts/FighterSim/` names** `StoryCombatRules`, `MeterGainScale`, `FlatEffectScale`, `FlatMeterScale`, `ScaleStoryFlatMeter`, `StoryMeterGainFactor`, `MomentumRegen`, `MomentumRegenMultiplier`, a Resonance type or `LegacyUnlockSchedule`. MM-SWEEP extends `InfluenceMeterSourceScanTests` to guard it.
- Shared numbers live in `BasicComboRules` (Godot-free); the sim reads them only through `FighterMomentumRules` / `FighterDefyRules` integer or `static readonly FP64` views.

### 1.3 Determinism
- Integer momentum units only in the sim. The blast radius test is a fixed-point squared distance (`dx² + dy² <= 6.25`), never `Sqrt`, never float. The heal rounds with the existing `(raw + One/2) / One` idiom.
- The CPU draws only through `NextPercent` on the recorded-input path, and only where P25 allows.
- A `KLOTHO_DET002` warning is a bug. Allowed: CS8632, and `KLSG_ECS004` on `FighterTuningComponent` (may vanish, P33).
- Fighter Defy presentation fires only on a **short forward step** of the 323 Defy counter (`FighterSimulationDriver.IsForwardCounterStep`, the FU-KNOCK precedent), so a rollback correction never replays it.

### 1.4 One canonical value: the constants regions

MM-RULES writes both regions in `scripts/Combat/BasicComboRules.cs`, anchored **after the ability-lockout region** (about :851–900). The D2 region (:766–830) stays untouched until MM-SWEEP deletes it.

**Region "Momentum gauge"** (G1–G3; `USER-MOMENTUM-GAUGE`)

| Constant | Value | Notes / readers |
|---|---|---|
| `MomentumUnitsPerPip` | 7200 | the sim unit; Story converts by `/ 7200f` |
| `MomentumMaxPips` / `MomentumMaxUnits` | 4 / 28 800 | clamp |
| `MomentumStartPips` / `MomentumStartUnits` | 2 / 14 400 | spawn, fresh Story attempt, Sudden Death, an absent save key |
| `MomentumSpecialCostUnits` | 7200 | 1 pip for every Special (G1.2) |
| `MomentumRegenFramesPerPip` | 720 | 12 s (G1.3) |
| `MomentumRegenUnitsPerFrame` | 10 | `= 7200 / 720`, pinned by a test; Story's ×1.1 lane gives 11 per frame (exactly 1 pip per 654.5 frames) |
| `MomentumStringHitGainUnits` | 360 | string hits 1–2, 0.05 pip |
| `MomentumDirectionalGainUnits` | 360 | Up-Attack, Down-Air, air strike, ledge attack (O5) |
| `MomentumFinisherGainUnits` | 1440 | hit 3, 0.2 pip; a full landed string is 2160 = 0.3 pip |
| `SpecialReuseDelayFrames` | 120 | 2 s per slot (G2.3) |
| `MomentumGainUnitsFor(int variant, int comboStep)` | — | the sim (`OnSwingResolved`) |
| `MomentumGainUnitsForHitbox(string hitboxID)` | — | Story (`NoteSwingLanded`) |

**Region "Defy History"** (rounds 1–5; `USER-DEFY-ACTIVE-BURST`)

| Constant | Value | Provisional? |
|---|---|---|
| `DefyInfluenceCost` | 50 | no (1.2, G2.1) |
| `DefyInvulnerableFrames` | 6 | yes (3.1) |
| `DefyBlastFrame` | 6 | yes |
| `DefyRecoveryFrames` | 24 | yes |
| `DefyTotalFrames` | 30 (6 + 24) | derived |
| `DefyBlastRadiusUnits` | 2.5 (double; Story 150 px at 60 px/u; the Fighter ring 156.25 px at 62.5) | yes |
| `DefyBlastStaggerFrames` | 30 | yes |
| `DefyBlastHitstopFrames` | 8 | yes (4.1 "about 8 frames") |
| `DefyBlastPushXUnitsPerSecond` / `DefyBlastPushYUnitsPerSecond` | 6 / 6 | yes (P7) |
| `DefyHealFloorNumerator` / `DefyHealFloorDenominator` | 1 / 10 | yes (2.4) |
| `DefyHealPoints(int missingHP, int echoPoolRounded, int maxHP)` | the P10 formula, integer | — |

**Elsewhere (one place each)**

| Where | Change | Owner |
|---|---|---|
| `InfluenceMeterRules.StockLossRetentionNumerator/Denominator` (`scripts/Combat/InfluenceMeterRules.cs:39-40`) | 3/4 → **1/2** (80 → 40) | MM-RULES |
| `StoryCombatRules.MeterGainScale` (`:38`) | 0.35f → **0.5f** | MM-RULES |
| `StoryCombatRules.FlatEffectScale` (new) | `= MeterGainScale` (the D5 scale alone, never Mozart's lane, O27), plus `FlatMeterScale(bool storyScaled)` | MM-RULES |
| `ResonanceStatKeys.MomentumRegen` (new) | stat key | MM-SMOM |
| `scripts/FighterSim/FighterMomentumRules.cs` (new, pure) | `CanAfford`, `Spend`, `Gain`, `Regen`, `Clamp`, `StartUnits`, `SuddenDeathUnits`: integer views of the region | MM-RULES |
| `scripts/FighterSim/FighterDefyRules.cs` (new) | `CostRaw` (`static readonly FP64`), `RadiusSquaredRaw`, `PushXRaw`/`PushYRaw`, `CanBegin`, `Begin`, `Advance`, `IsInvulnerable`, `BlastFiresThisTick` | MM-SIM |

### 1.5 Fighter state layout (O1): no new component ID

Verified at `56031750` (`FighterSimulationComponents.cs`, `FighterSpecialCast.cs`). The highest ID stays **323**.

| Component | Field change | Bytes per fighter | Owner |
|---|---|---|---|
| 305 `FighterRuntimeComponent` | `SpecialOneCooldownFrames` → **`SpecialOneReuseFrames`**, `SpecialTwoCooldownFrames` → **`SpecialTwoReuseFrames`**, armed to 120 at acceptance and ticked by the existing `TickCounters` lines (SS:1477-1478). `MovementCooldownFrames` unchanged. | 128 (full, unchanged) | MM-RULES (mechanical rename) |
| 310 `FighterVerbComponent` | `MomentumRefundedFramesSlotOne` → **`MomentumUnits`**; `…SlotTwo` → `SpareInt0`; `DefyHistoryUsed` dead (never written) until MM-SWEEP renames it `SpareInt1` | 128 (full, 8 B spare) | MM-SIM; MM-SWEEP |
| 312 `FighterDefenseComponent` | **delete** `DefyProtectionAwaitControl`, `DefyProtectionFrames`, `DefyProcIdentity`, `DefyBarred`; the Aegis and barrier fields stay | 36 → **20** | MM-SIM |
| 323 `FighterActionStateComponent` | append **`DefyFrames`** (0 idle, 1…30); `ActionFlags` bit 5 `ActionFlagDefyBlastFired`, bit 6 `ActionFlagDefyBuffered` (P12), bits 12–15 the wrapping Defy count (`DefyCounterShift` 12, `DefyCounterMask` 0xF000) | 36 → **40** | MM-RULES (declared, inert); MM-SIM (behaviour) |
| 306 `FighterTuningComponent` | delete `SpecialOneCooldownFrames` / `SpecialTwoCooldownFrames` (P18) | −8 | MM-SIM |
| `FighterActionKind` | `Defy = 13`, not offensive (ordinals are not serialized) | — | MM-RULES |

- 323 is zeroed at the respawn-platform hold and the Sudden Death setup, which is right for Defy. The **gauge is not in 323** because it must survive stock loss (G3.2).
- `MomentumUnits` is set to `MomentumStartUnits` in `FighterWorldSystem.SpawnFighter` (SS:78-200) and by the Sudden Death setup; stock loss leaves it.

### 1.6 Protocol v5 (O2)
- `GameplayButtons.Defy = 1 << 15` (`scripts/Core/PlayerInputFrame.cs`; the ":51 15 free" comment becomes "no free bit"). `PlayerInputFrame.SerializedSize` stays 12 B, `RollbackInputPacket.SerializedSize` 47 B.
- `RollbackInputPacket.ProtocolVersion` 4 → **5** (`scripts/Networking/RollbackProtocol.cs:37`): a v4 peer would read bit 15 as nothing and desync silently. Pins: `RosterSwapProtocolTests` (:42), `RollbackProtocolTests`.
- After this the wire has **no free `GameplayButtons` bit**: the next Fighter verb forces a packet-size change (`DEFER-INPUT-WIRE-FULL`).

### 1.7 Saves: schema v8 stays
- The Story gauge is key `"momentum"` in `StoryPlayerResourceTimers.AbilityCooldowns` (P20). The `special1` / `special2` keys keep their names and now hold the reuse delay.
- Dead data, still deserialized, never written: `StorySaveData.StoryDefyHistoryUsed` (SaveManager.cs:76), `StoryAttemptState.DefyHistoryUsed` (:272); `EchoStepCooldownFrames` already is. `StoryManager.StoryDefyHistoryUsed` / `DefyHistoryUsed` (not payload) are deleted. Payload classes stay engine-free (signature 7): the start fill comes from the pure `BasicComboRules`.

### 1.8 Legacy identifiers: rename or keep

| Identifier | Decision | Reason |
|---|---|---|
| 305 `SpecialOne/TwoCooldownFrames` | **Rename** `…ReuseFrames` | Sim fields; compile errors enumerate every stale reader. |
| 310 `MomentumRefundedFramesSlotOne/Two` | **Rename** `MomentumUnits` / `SpareInt0` | Same. |
| 306 Special cooldown frames | **Delete** | Dead hashed state. |
| Story `PlayerController.SpecialOneCooldownTimer` / `SpecialTwoCooldownTimer` | **Keep, re-document** as the 2 s reuse-delay timers | They keep their exact tick and pause semantics; the F10 keys, the Fighter-proxy feed, the HUD sync, the Mirror projection and about 127 references in 17 test files owned by five workstreams read them. New readers use `SpecialReuseRemainingSeconds(slot)` / `IsSpecialReady(slot)`. |
| Keys `systems_card_momentum_*`, `controls_*`, `tutorial_step_defy*`; dialogue ID `dlg_l00_station_defy` | **Keep** names, rewrite values | Translation-key rule. |
| `StoryDefyHistoryUsed`, `DefyHistoryUsed` (payloads) | **Keep** as dead data | The `RatingByLevel` precedent. |
| `FirstUseTooltips.Defy`, `CoreMechanicCueIDs.DefySealShatter` / `DefyToll` | **Retire** | P27, P29. |
| `OnCooldownStarted` / `OnCooldownComplete` | **Keep** (movement timer; the reuse delay raises them as today) | The HUD polls anyway. |
| Stat key `CooldownReduction` | **Keep** (unscoped reader only) | P16. |

### 1.9 Two-stage retirement
Phase B runs in parallel, so between merges every file must compile and pass its tests. **A Phase B workstream removes no type, member, key or asset that another workstream's file still uses.** It removes its own uses; MM-SWEEP (Phase C) deletes the shared remains after a reference scan:
- the D2 region of `BasicComboRules` (`MomentumRefundFramesFor`, `MomentumRefundFramesForHitbox`, `MomentumGrant` and the four refund constants);
- `DefySealModel`, `DefySealTests`, `EventBus.DefySealState` / `DefySealPayload` / `OnDefySealChanged` / `RaiseDefySealChanged`;
- `PlayerController.StoryDefyHistoryUsed` (a dead getter returning false after MM-SDEFY); 310 `DefyHistoryUsed`;
- the `hud_defy_*` keys `DefySealModel` reads; the theme role `DefyLabel` and palette entry `DefyReadyGlow`; the `defy_*` icons;
- the Special `.tres` cooldown values and the content pins that read them (P1); `KitReachRules.RequiemFortissimoShaveSeconds` (P17).

A member that loses its meaning in Phase B becomes a documented no-op. **Cross-edits:** a workstream may touch another's file only to keep the build compiling or a test passing at its merge (smallest change, no behaviour), listed under "cross-edits" in its handoff.

### 1.10 Tests
- One `[TestSuite]` per file (signature 6). Pure-C# suites carry no `[RequireGodotRuntime]` and touch no engine API (signature 7). Restore `GameManager.CurrentSession`, `SceneTree.Paused` and `PlayerController.ScriptedHPFloor`, and free grouped nodes, in `finally` (signatures 4, 8). Use the repository idiom, never `OverrideFailureMessage("")`.
- **A test must be able to fail**: the handoff says how each new or rewritten case was seen failing (pre-change code, or a reverted mutation).
- Hand-built sim frames set `Held = Pressed`; the Defy press and its buffer are also pinned with `PlayerInputFrame.Create`-built input.
- The MM-SWEEP retirement scan bans retired identifiers in code **and test names**: never name a case with `DefyBarred`, `DefySeal`, `MomentumRefund` and so on (the TimeFreeze retirement precedent).
- **Seams.** Sim: `FighterSimulation.SeedFighterMomentumForTest(int playerID, int units)`; `SeedFighterSpecialReuseForTest(int playerID, int slotOne, int slotTwo)` (renamed from `SeedFighterSpecialCooldownsForTest`); `SeedFighterDefyForTest(int playerID, int defyFrames)` (replaces `SeedFighterDefyProtectionForTest`); the non-suite helper `tests/Determinism/MomentumTestKit.cs` (`KeepFull(sim, playerID)` re-seeds 28 800 and clears the reuse delay before a scripted press). Story: `PlayerController.RestoreMomentumPips(float)`, which the restore path also uses.
- No suite pins an absolute Fighter hash: pin replay identity and snapshot round trips.
- Count with `(Get-ChildItem tests -Recurse -Filter *.cs | Select-String -Pattern '^\s*\[TestCase\b').Count`.

### 1.11 GdUnit, Godot launches, the integration worktree
- Hold `D:/Projects/.ftt-gdunit-lock` for **every** Godot launch (test, import, load check, smoke, capture, bot run, kit build). Drain `testhost` and `Godot*_console` processes (never the non-console editor), create the lock with `mkdir`, retry every 15–30 s, treat it as stale only when older than 20 minutes with no such process running, and remove only a lock you made, right after the run, also on failure.
- Agents run `dotnet build`, their **filtered** suites (§5.2) and the load check. MM-RULES, MM-SIM, MM-SMOM and MM-SDEFY may also run **one** full suite at the end. The orchestrator runs a full suite in `D:/Projects/ftt-wt/fu-integ` after every merge and reconciles `Total:` exactly against §4. A short total is read against signatures 1, 2, 4, 5 and 7 first, never as a pass; a full total with Story cast cases stuck in Idle is signature 8.

### 1.12 Worktrees, commits, handoffs
- `D:/Projects/ftt-wt/mm-<ws>` on `mm/<ws>`, cut from the commit §3.1 names. Seed `.godot/` by copying the main checkout's `.godot` without its `mono` folder (robocopy `/E /XD mono`, CLAUDE.md), then run one headless import under the lock; export `GODOT_BIN`.
- `git checkout -- assets` before each commit. Commit new `.uid` sidecars. Never commit `.godot/` or `localization/en.en.translation`.
- Commit messages `MM-<WS>: <summary>`, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Never merge or push.
- Write `docs/handoffs/MM_<WS>.md` (§6) and commit it with the work; report the branch, final SHA and worktree path.
- Review goes to Codex through the orchestrator (`/cross-review`). No agent reviews its own work; no Claude subagent reviews.

### 1.13 Shared files (conflict policy)
- One owner per file per phase (§3.3). In a hub file (`PlayerController.cs`, `FighterSimulationSystems.cs`, `FighterEntitySystems.cs`, `FighterSimulationDriver.cs`, `EnemyController.cs`, `BossController.cs`, `CharacterFactory.cs`) a non-owner adds only **call lines at the declared site**; bodies go in its own partial file.
- `BasicComboRules.cs`, `StoryCombatRules.cs`, `InfluenceMeterRules.cs`: MM-RULES (Phase A) and MM-SWEEP (Phase C) only.
- `project.godot`: MM-INPUT only. The theme generator `tools/ui_theme/build_ftt_theme.py` and its outputs: MM-SWEEP only (MM-HUD only if a new role is unavoidable, and then it alone regenerates; `ftt_theme.tres` is never hand-edited).
- Shared **test** files have method owners: add your cases as new methods, never rewrite another workstream's case, and name every case you touched; the later merge resolves the union.

### 1.14 Localization
- New visible text = a key in `localization/en.csv`, in a block at the end of the file headed by a comma-less comment row `# MM-<WS> block`, one block per workstream. Quote any value with a comma (`TranslationCsvShapeTests`; `""` is a literal quote).
- In-place value rewrites of existing rows only for the rows the workstream's §2 dossier lists.
- A key is deleted in the **same commit** as its last reader, by whoever removes that reader (`UnusedTranslationKeyTests`, orphan ceiling 12). Keys whose reader survives to Phase C (`hud_defy_*`) belong to MM-SWEEP.
- Agents import locally for their tests; **only the orchestrator commits `en.en.translation`**, regenerated in the integration worktree after each merge that touched `en.csv` (touch the CSV first; the load check does not reimport).

### 1.15 Provisional numbers; nobody plays the build
Every number the user did not state is *Provisional* here; an agent's own choice goes in its handoff's "Decisions" with one line of reasoning. Headless tests, smoke runs and bot runs are the evidence, and each handoff says "nobody played the build". §5.3 is the hands-on checklist (G5.4).

---

## 2. Per-decision dossiers

Each gives the rule, the numbers, the code sites (from the dossiers; re-read before editing), the owner and the ledger IDs. "Both" = both modes, same rule (G3.4).

### 2.1 The Momentum gauge · G1.1–G1.3, G1.5, G2.4, G3.4, G5.2, O3–O5 · Both · MM-RULES / MM-SIM / MM-SMOM

**Rule.** One gauge of four pips, shared by Special 1 and Special 2, filling continuously within each pip. Every accepted Special costs one pip at acceptance (the site that armed the cooldown). Passive regen 1 pip per 12 s, no pause after a spend (G1.4). Landed basic swings add, once per swing on its first D1a-landed contact (bosses included): string hits 1–2 and the Up-Attack, Down-Air, air strike and ledge attack +0.05 pip, the finisher +0.2. Throws, Specials, Ultimates, constructs, zones, a blocked, refused, warded or zero-damage contact pay nothing. The movement ability keeps its own 5 s cooldown (G1.5). A refused press spends and arms nothing.

**Sim (integer units, §1.4).** 7200 units = 1 pip; max 28 800; start 14 400; cost 7200; regen 10 per frame; gains 360 / 360 / 1440 (a full landed string 2160).
- Gate and spend: `FighterEntitySystems` press read (ES:1382-1397) becomes `FighterMomentumRules.CanAfford(verb.MomentumUnits) && runtime.SpecialOneReuseFrames <= 0`; on acceptance `Spend` and arm the reuse delay to `SpecialReuseDelayFrames` (replacing the cooldown arm and the D2 counter reset).
- Regen: one line in `TickCounters` beside SS:1477 (`verb.MomentumUnits = FighterMomentumRules.Regen(…)`), skipped when `MatchState != InProgress` (P2).
- Gains: `OnSwingResolved` (SS:2444-2477) keeps its landed predicate (`Landed || Absorbed`, damage > 0, the `NoteSwingLanded` 0 → 1 edge) and calls `FighterMomentumRules.Gain(ref verb, BasicComboRules.MomentumGainUnitsFor(variant, step))` instead of `ApplyMomentumRefund`. Callers unchanged: ledge attack SS:2313, air strike SS:2327, directional SS:2341, chain SS:2423.
- Deleted (MM-SIM): `ApplyMomentumRefund`, the per-cycle counters' writes, the intent-time cooldown args (SS:3096, 3127, 3142), `AttackIntent.CooldownFrames` (SS:3230, 3249), the 306 tuning fields and their `FighterLoadoutFactory` projection (`:32-33, 120`) and spawn copy (SS:113-114).

**Story (float pips).** `PlayerController.Momentum.cs`: `MomentumPips` (0…4), `RestoreMomentumPips(float)`, `IsSpecialReady(AbilitySlot)`, `SpecialReuseRemainingSeconds(AbilitySlot)`, `MomentumRegenMultiplier` (1.0, or 1.1 with a `MomentumRegen` node).
- Gate and spend in `CheckSpecialInput` (B:PC:5387, 5400): `MomentumPips >= 1 && SpecialOneCooldownTimer <= 0`; spend before `TryExecute` so a failed `Validate()` leaves no half-state (refund on a false return).
- `BaseSpecial.StartCooldown` (`scripts/Combat/BaseSpecial.cs:237-270`): the Special branches set the reuse timer to `SpecialReuseDelayFrames / 60f` and stop reading `CooldownDuration`, `StoryScoped(CooldownReduction, …)` and `OnSpecialCooldownArmed` (deleted with `RefundMomentum`); the movement branch is unchanged (Vortex Step's `CooldownScaleForThisCast` stays, R1).
- Regen in `UpdateCooldowns` (B:PC:5970, inside `!TimeFrozen`, after the hitstop return): `dt / 12 × MomentumRegenMultiplier` (P3).
- Gains in `NoteSwingLanded` (B:PC:3019) via `MomentumGainUnitsForHitbox(id) / 7200f`, replacing `ApplyMomentumRefund` / `RefundMomentum` (:3036-3054).
- A Dormant slot never spends (the gate is after `IsAbilityUnlocked`); the gauge still fills (G4.1).

**Ledger.** `USER-MOMENTUM-GAUGE` (G1.1–G1.3, G3.4, G5.2), `USER-MOMENTUM-BASICS-ONLY` (G2.4), `USER-MOVEMENT-KEEPS-COOLDOWN` (G1.5), `PROVISIONAL-MOMENTUM-RULES` (O3–O5, the unit, the countdown, P2, P3). Supersedes `USER-PER-HIT-MOMENTUM`; amends `PROVISIONAL-HIT-CONFIRM-RULES`.

### 2.2 Reuse delay, lockouts and refusal feedback · G2.2, G2.3, R2 · Both · MM-SIM / MM-SMOM / MM-HUD

**Rule.** Using Special 1 locks Special 1 alone for 2 s (120 frames); Special 2 stays free, and vice versa. The 30-frame ability lockout A with the D1 hit-confirm release, the 30-frame Special → Special lockout S, the swing-landed latch C and the freeze-only Special buffer are all unchanged. The reuse delay ticks where the cooldown ticked (pauses in the fighter's own hitstop, under Time Freeze and the tree pause). A press refused by 0 pips or by the lockout plays the not-yet flash (R2, P14).

**Sites.** Sim: 305 reuse fields (§1.5); `FighterLockoutRefusalRules` (`:28-52`, `:121-125`) replaces "slot off cooldown at read time" by "reuse clear and either the lockout alone refused or the gauge is under one pip", so the driver's existing `FlashLockoutRefusal` call covers both. Story: `SpecialPressIsReady` (B:PC:1006-1012) and `ReportReadySpecialRefusals` raise `EventBus.OnAbilityLockoutRefused` for a 0-pip refusal too; the HUD needs no new path.

**Ledger.** `USER-MOMENTUM-LOCKOUTS-KEPT` (G2.2), `USER-MOMENTUM-REUSE-DELAY` (G2.3), `USER-MOMENTUM-NOT-YET-FLASH` (R2; amends `USER-NOT-YET-FLASH`), `PROVISIONAL-MOMENTUM-REFUSAL-FEEDBACK` (P14, P15).

### 2.3 Gauge lifecycle · G3.1–G3.3, G4.1, O20–O22, P2, P20 · MM-SIM / MM-SMOM

| Event | Fighter (sim) | Story |
|---|---|---|
| Match start / fresh level entry / full Restart (pause, hub, Game Over) | `SpawnFighter`: 14 400 units, reuse 0 | factory-fresh hero: 2.0 pips (the fresh-attempt latch keeps it, `LevelStartResourceResetTests`) |
| Pre-match countdown | no regen (P2) | — |
| Stock loss | gauge and reuse kept (G3.2); regen runs on the respawn platform | — |
| Sudden Death | 14 400, reuse 0, movement cooldown 0 (O22); Influence 0 unchanged | — |
| Death rewind | — | untouched (same `PlayerController`) |
| Ordinary load, mid-level resume, Collapse resume, Anchor Snap | — | restored from `AbilityCooldowns["momentum"]`; absent → 2.0; reuse keys clamp to 2.0 s (P20) |
| Levels 0–2 (Special 1 Dormant) | — | the gauge is shown dimmed with the Dormant overlay and fills normally (G4.1); spending is impossible because both slots are Dormant |

**Sites.** Sim: SS:78-200 spawn, SS:3493-3504 Sudden Death (delete the D2 counter resets; set units and reuse). Story: `CaptureStoryResourceTimers` / `RestoreStoryResourceTimers` (B:PC:6000-6050).
**Ledger.** `USER-MOMENTUM-LIFECYCLE` (G3.1–G3.3), `USER-MOMENTUM-STORY-DORMANT` (G4.1), `PROVISIONAL-MOMENTUM-STORY-PERSISTENCE` (O21, P20), `PROVISIONAL-MOMENTUM-SUDDEN-DEATH` (the carried default).

### 2.4 Defy: the action · 1.1, 1.3, 2.1–2.3, 3.1, 3.3 + R3, 5.2–5.4, O9–O11, O15 · Both · MM-SIM / MM-SDEFY

**Legal** from any state (O9): idle, run, air; mid-swing (the string is cancelled; the swing's lockout keeps running); mid-Special cast (cancelled; what the cast put into the world, its pip and its reuse delay stand); hitstun, tumble, knockdown and get-up, tech lock; block stance and shieldstun; held by a grab (the grabber is released); grabbing (own grab cancelled, victim released); ledge hang (hang cleared); the movement ability; the roll and air dodge (cancelled).
**Refused:** dead, out of stocks or on the respawn platform; captured by an A02 hold; during the fighter's own Ultimate in any phase (Fighter A02 windup / active / whiff recovery / cinematic; Story cast); Time Freeze and the Post-Landing Hold (Story); the pre-match countdown, scripted pauses and Level 0's unlosable hold (P21); already Defying; Influence below 50. Ignores the ability lockout, the Special lockout, Suppression and the Legacy Unlock Schedule (never gated, 5.3), and arms none of them (5.4).

**Frames (P11).** Frame 1 = the accepting tick.

| Defy frame | State |
|---|---|
| 1–6 | fully invulnerable; velocity zeroed at frame 1; no gravity |
| 6 | the blast (§2.5) and the heal (§2.6) resolve; 8 frames of hitstop on the Defier and every target |
| 7–30 | vulnerable recovery, action-locked; normal gravity; a hit interrupts it normally |
| 31 | free (`DefyFrames` back to 0, Story back to Idle or Airborne) |

**Cost.** 50 Influence spent at acceptance, never scaled (O15): sim `FighterDefyRules.CostRaw` against `Influence` (exact FP64); Story `DrainUltimateMeter(BasicComboRules.DefyInfluenceCost)`.
**Claims its tick** over every other verb (O11): no Special, Ultimate, movement ability, grab or swing starts the same tick.
**In the fighter's own hitstop** (O10): the press is held one deep and starts on the thaw tick, overriding the hit's hitstun, launch and knockdown (P12).
**Versus an A02 Ultimate** (3.3 + R3): legal during the opponent's 20-frame wind-up; the invulnerable frames dodge the strike and the blast cancels the activation (P6); refused once the hold has connected.

**Sim implementation (MM-SIM).** New `FighterMovementSystem.Defy.cs`: the press is read at the top of the per-fighter loop in `FighterMovementSystem.Update`, after `UpdateJumpLatch` (SS:306) and before the hitstop gate (SS:312), so one site covers every early-`continue` branch (A§2.2.2). `FighterDefyRules.Begin` spends, sets `InvulnerabilityFrames` to 6, clears hitstun, daze, pending launch, tumble, shieldstun, `HitstunBlockCancelBlocked`, tech lockout, knockdown (`FighterKnockdownRules.Clear`), Foresight (322), the Special cast and D3 buffer (323), universal movement, the string, the ledge hang and the freeze buffer; releases a grab on either side; calls `FighterActionRules.OnActionAccepted(…, FighterActionKind.Defy)`; and steps the wrapping Defy counter. While `DefyFrames > 0` the fighter's buttons are discarded each tick (the cast-lock pattern) and its clock advances in that branch, decrementing invulnerability itself as the tech branch does.

**Story implementation (MM-SDEFY).** `CharacterState.Defying` (appended after `Thrown`; MM-RULES declares it). `TryBeginDefy()` runs once per tick before the state `switch` (B:PC:2190); a press in the hitstop branch (B:PC:2085-2110) is buffered and begins on the first unfrozen tick, dropping `_hasPendingLaunch` and the DI read. The release path copies B:PC:4273-4279 plus `CancelActiveAttack()`, `InterruptActiveAbilities()`, the ledge exit, the roll and air-dodge exit and the Dazed exit. The startup invulnerability reuses the `IsDefyProtected` gate sites (B:PC:1580, 4127, 4152, 5513; `EnvironmentalBlock.cs:66`; `EinsteinRelativityRift.cs:309`), now true exactly on frames 1–6.

**Ledger.** `USER-DEFY-ACTIVE-BURST` (1.1, 1.3, 2.3, 5.3, 5.4), `USER-DEFY-COST-50` (1.2, G2.1), `USER-DEFY-ANY-TIME` (2.2), `USER-DEFY-FRAMES` (3.1), `USER-DEFY-VS-ULTIMATE` (3.3 + R3), `USER-DEFY-SUDDEN-DEATH` (5.2), `PROVISIONAL-DEFY-LEGALITY` (O9, O10, P12, P21), `PROVISIONAL-DEFY-FRAMES` (O11, P11), `VERIFY-DEFY-MUTUAL` (P5).

### 2.5 Defy: the blast · 1.4, 3.2, 4.3, R3, R4, O12, O13, P4–P9, P22, P23 · Both · MM-SIM / MM-SDEFY

**Rule.** On Defy frame 6: every hostile character within 2.5 u of the Defier's body centre (P9) takes an assigned push away from the Defier (P7), a 30-frame non-launching stagger and 8 frames of hitstop. Unblockable (block stance and shield charges untouched), no damage, cannot kill (P8), no meter either side, no Rally echo, no D1a landed signal, no Momentum, no poise or elite budget (P22). It ignores hyper-armour, the V7.4 getup and Armored Recovery windows and the A02 wind-up armour; an A02 caster in Windup or Active has its activation cancelled and its meter stays spent (R3, P6). Characters only (R4). The Defier also takes the 8-frame hitstop (the "world freeze", sim state through `HitstopFrames`).

| Target | Effect |
|---|---|
| Opposing fighter (sim) | push `(±6, +6) u/s` assigned, airborne non-launching; `HitstunFrames = 30`; hitstop 8; its swing, cast, grab, Foresight stance, A02 Windup/Active cancelled (P6) |
| Standard / elite mob | `EnemyController.ApplyDefyBlast(Vector2 push, int staggerFrames, int hitstopFrames)`: stun 30 through every armour window, push `(±360, −360) px/s` respecting the edge rule (P8), hitstop 8; no damage number |
| Dormant lock member, `DrillInvulnerable` body | skipped |
| Boss | `BossController.ApplyDefyBlast`: telegraph or active window → `CancelTelegraphIntoRecovery()`; pushed unless `IsKnockbackImmune` (the default true); never stunned (4.3); arena-warded → untouched (P23) |
| Mirror clone (hero's blast) / hero (clone's blast) | `PlayerController.ApplyDefyBlast`: as the opposing fighter, through Story physics |
| Invulnerable target | untouched (P9) |
| Projectiles, constructs, zones, props, strike surfaces | untouched (R4) |

**Sim (MM-SIM).** New `FighterCombatSystem.DefyBlast.cs`, called from `FighterCombatSystem` (the only system with both fighters, SS:~1850-1910) **before** the intents are applied (SS:1875-1876, P4). Distance test: `dx² + dy² <= FighterDefyRules.RadiusSquaredRaw` on FP64 body centres. The blast does **not** go through `ApplyFighterHit` (A§2.2.7: zero damage with hitstun reaches the block branch); a dedicated `FighterDefyBlastRules.Apply` writes hitstun, velocity and hitstop and calls `FighterUltimateActivationRules` to cancel an activation. `ActionFlagDefyBlastFired` guards a second fire.
**Story (MM-SDEFY).** New `PlayerController.DefyBlast.cs`: on frame 6 iterate the `Enemies` group and the Mirror group by distance (dispose wrappers with `AsDisposable()`), apply the table above, `ApplyHitstop(8)` on the hero.

**Ledger.** `USER-DEFY-SHOCKWAVE-HEAL` (1.4, 3.2), `USER-DEFY-BOSS-INTERRUPT` (4.3), `USER-DEFY-CHARACTERS-ONLY` (R4), `PROVISIONAL-DEFY-BLAST` (O12, O13, P6–P9, P22, P23), `VERIFY-DEFY-BLAST-RINGOUT` (Q2).

### 2.6 Defy: heal and cost · 1.2, 2.4, O14, O15, P10 · Both · MM-SIM / MM-SDEFY

Heal on the blast frame: `min(missingHP, max(round(echoPool), ceil(MaxHP / 10)))` through `BasicComboRules.DefyHealPoints`; then the echo pool and its drain are zeroed and its uncredited damage-taken meter is forfeited. Sim: `FighterVerbComponent.EchoPool` / `EchoDrainPerFrame`, rounding `(raw + One/2) / One`. Story: new `CashInEchoPool()` over `_echoPool` / `_echoDrainPerFrame` (B:PC:867-868) through `HealStory` (HP only, no meter). Cost: §2.4. **Ledger:** `USER-DEFY-SHOCKWAVE-HEAL`, `PROVISIONAL-DEFY-HEAL` (the floor, the forfeit, P10).

### 2.7 The passive Defy is deleted · O16 · Both · MM-SIM / MM-SDEFY (+ MM-HUD, MM-FX, MM-TEXT, MM-SWEEP)

| Mode | Delete | Owner |
|---|---|---|
| Sim | the lethal branch in `ApplyFighterHit` (ES:800-837) and the DoT branch in `ApplyUnattributedDamage` (ES:1060-1076); `CanDefy`, `IsDefyUnspentAndUnbarred`, `BeginDefyProtection`, `Tick`, `EndOnOffence`, `Seal`, `DefyProtectionTicks` (ES:239-365) and the `EndOnOffence` call (FighterActionRules.cs:95); the `defied` echo and meter special cases (ES:856, 867); the protection gates (ES:673, 1039; FighterKitRules.cs:285, 353; FighterUltimateActivation.cs:300 — the Defy startup now rides `InvulnerabilityFrames`; SS:2817); the Sudden Death bar (SS:3535-3538); the 312 fields; `SeedFighterDefyProtectionForTest` | MM-SIM |
| Story | the passive block in `ApplyDamage` (B:PC:4237-4281); the 60-frame window (`TickStoryDefense` :694-704, `ClearDefyProtection` :602 and its calls :934, :943, :3327, :4641, `StoryDefenseRules.DefyProtectionFrames`); `_storyDefyHistoryUsed`, `SetStoryDefyHistoryUsed`; `DefySeal` / `PublishDefySealIfChanged` and its call (:2152); `StoryManager` Defy members (:1005, :1018, :1039, :1326, :1964-1972); the CharacterFactory spent-Defy seeding (:174-178); Level 0's scripted lethal hit and seal raise (Level00Controller.cs:864-927) | MM-SDEFY |
| HUD | both seal widgets and scene nodes, the `hud_defy_label` key | MM-HUD |
| Cues / tooltips | the seal listeners in `CoreMechanicCuePresenter` (:45, 156, 207-212) and `FirstUseTooltipPresenter` (:121, 327-331) | MM-FX / MM-TEXT |
| Shared remains | §1.9 list | MM-SWEEP |

`StoryDefyHistoryUsed` / `DefyHistoryUsed` stay as dead save data (§1.7). **Ledger:** `USER-DEFY-ACTIVE-BURST` (it replaces the save), closes or supersedes the rows in §0.5.

### 2.8 Meter rates · 6.1–6.4, 7.1–7.4, O22, O27 · MM-RULES / MM-SMOM / MM-SIM

**Story gain ×0.35 → ×0.5** (6.1): one constant, `StoryCombatRules.MeterGainScale` (MM-RULES). At ×0.5 a Defy takes about 100 damage dealt and an Ultimate about 200. Read by `UltimateMeter.GainFactor` (UltimateMeter.cs:40-41) and `PlayerController.RallyUncreditedMeter` (B:PC:5996), unchanged. A save written under ×0.35 carries `RallyUncreditedMeter` in old-rate units and mis-credits once on resume (negligible, recorded).

**Story flat effects** (6.2, O27): scaled by `StoryCombatRules.FlatEffectScale` (the D5 scale alone, never Mozart's lane) at the **call sites**, never inside `DrainUltimateMeter` (the Ultimate spend and the Level 0 hold drain use it). Helper `PlayerController.ScaleStoryFlatMeter(float amount) => amount × StoryCombatRules.FlatMeterScale(AppliesStoryMeterGainScale)`, so the Mirror clone and non-campaign builds stay unscaled (MM-SMOM).

| Effect | Site | Before → after |
|---|---|---|
| Extractor discharge (and the Resonance Hold stand-ins that inherit the field; verify none overrides it) | `ChronalExtractor.cs:161-176` | −20 → −10 |
| Searchlight (Levels 4 and 9) | `SearchlightZone.cs:91-109`; `Level_04_Paris.tscn` overrides the export three times, so scale at drain time, never the data | −5/s → −2.5/s |
| Siphon Snare | `SiphonTetherChannel.cs:258-279` (`wanted` :267, cap :268-270) | −10/s cap 30 → −5/s cap 15 |
| Shield of Orléans | `PlayerController.ApplyShieldOfOrleans` (B:PC:2035-2054; `ShieldOfOrleansMeterPerBlock` :68 stays 5) | +5 → +2.5 |
| `MeterBoost` orb (never spawned, kept per 7.2) | `ChronalOrbItem.cs:126-129` | scaled |
| Direct sets (the Level 0 lesson), spends (Ultimate 100, Defy 50), the Level 0 hold drain, the Paris Dampening Beam (Fighter) | — | unchanged |

**Fighter stock loss 3/4 → 1/2** (6.4): `InfluenceMeterRules` numerator/denominator (MM-RULES); `FighterMeterRules.RetainOnStockLoss` (FighterMeterRules.cs:41-42, 67-68) is unchanged code and moves hashes. A full bar becomes exactly one Defy. The dead Story path `PlayerController.ApplyStockLossMeterRetention` (B:PC:4478) / `UltimateMeter.ApplyStockLossRetention` (UltimateMeter.cs:53) and its pin are deleted (MM-RULES).
**Sudden Death** (5.2, O22): Influence 0 (unchanged, SS:3469), `DefyBarred` gone, Defy legal once 50 is earned.
**Unchanged** (7.1–7.4 and the carried defaults): the Resonance Surge orb (+15, Fighter, off by default); damage taken 0.25 per HP; Ultimate-origin damage earns 0 (D03h); Mozart's ×1.1 build rate (kit pass); the Paris beam.
**Ledger.** `USER-STORY-METER-SCALE-HALF` (6.1; amends `USER-STORY-METER-SCALE`), `USER-STORY-FLAT-EFFECTS-SCALED` (6.2), `USER-STOCK-LOSS-RETENTION-HALF` (6.4), `USER-METER-PICKUPS-AND-ORIGIN-KEPT` (7.1–7.4), `PROVISIONAL-STORY-FLAT-SCALE-SITES` (O27).

### 2.9 Input and bindings · 2.1, 3.4, O18 · MM-RULES (bit) / MM-INPUT

- `project.godot`: `gameplay_defy` = physical key F (70) + `InputEventJoypadButton` 8 (R3, device −1). `ui_dialogue_log`'s pad event 8 → **4** (Back / View); Tab stays. Pad 4 and F carry no action today (C§1.1).
- `InputManager`: `Actions.Defy = "gameplay_defy"`; `RemappableActions` gains it after `TimeFreeze`, before `Pause` (a single button: no chord recipe, no `ShortcutEnabled`, not in `RequiredReachableActions`); `ActionLabelKey` → `controls_action_defy`; the frame builder (`InputManager.cs:1081-1092`) adds `AddIfHeld(ref held, GameplayButtons.Defy, ReadActionPressed(Actions.Defy, …))`; `ToButton` (:1247-1261) maps it. The device poll picks it up through `RemappableActions` (C§1.1). Never the global `Input.IsActionJustPressed` (the Time Freeze read is the anti-pattern).
- The Controls tab row is automatic from `InputBindingService.BindableActions()` (`SettingsMenu.BuildControlsTab`).
- **Load repair** `InputBindingSet.RepairDefyPadDefaultCollision(projectDefaults)`, called from `SaveManager.ApplySavedInputBindings` (SaveManager.cs:1128-1146) after `RepairGodot3ShoulderRows`: if `gameplay_defy` has no saved override and another action's saved joypad half holds button 8, mark `gameplay_defy|Joypad` Unbound in `UnboundActions`, keep the player's row, and raise the notice `save_notice_defy_pad_unbound`. Pure and idempotent. A saved Time Freeze row still on Back now shares it with the Dialogue Log: no repair, a ledger note (`VERIFY-TIME-FREEZE-ON-BACK-SHARED`).
- en.csv `# MM-INPUT block`: `controls_action_defy` ("Defy History"), `save_notice_defy_pad_unbound`.
- **Ledger.** `USER-DEFY-OWN-BUTTON` (2.1, 3.4), `PROVISIONAL-DEFY-INPUT` (O18, the repair), `DEFER-INPUT-WIRE-FULL` (§1.6).

### 2.10 HUD, both modes · 4.2, G4.1, G5.1, R2, O19 · MM-HUD

**Poll model.** Both HUDs read the authoritative value every frame (the Story HUD's `SyncCooldownAuthority` pattern, StoryHUD.cs:350-367); no new EventBus payload.

| Element | Story (compact HUD) | Fighter (top bars) |
|---|---|---|
| Momentum pips | new `FTT.UI.Pulp.MomentumPips` (code-drawn, cached polygons, zero allocation per draw, continuous fill within a pip, a ready bloom gated by `ComfortSettings`) in a row under `SpecialsCapsule`, spanning the capsule's width; the slab grows only by that row (about 12 px provisional, so 484 × 182 → about 484 × 194), and the title and objective move with it | the same widget under each player's two Special lenses, P2 mirrored |
| Pip source | `PlayerController.MomentumPips` | `FighterSimulationDriver.TryGetMomentum(playerIndex, out int units)` / 7200 |
| Dormant | the pip row dims with the Dormant overlay while both Special slots are Dormant (Levels 0–2, G4.1) | — |
| Special lenses | the sweep shows the 2 s reuse delay (`Progress` = remaining / 2 s) and the lockout; new `PulpSlotState.Starved` (dimmed icon, `Ink3` ring) at under one pip; precedence P15 | the same; `FighterHUD.UpdateCooldowns` (`:1219-1260`) reads `SpecialOneReuseFrames` / `SpecialTwoReuseFrames` with a 120-frame denominator |
| Movement lens | keeps its 5 s sweep | keeps its sweep |
| 50 notch | a short radial tick at the half-way angle on the Ultimate lens ring (`RadialProgress.NotchFraction` / `NotchColor`, or drawn by `PulpAbilitySlot`; check the start angle and direction); gold (`UIPalette.GoldBright`) when meter is 50+ **and** `gameplay_defy` is bound for the player's current device kind, `Ink3` otherwise; the ring drops at once on a Defy | `PulpBarDecor.DefyNotchFraction` (0.5) and `DefyNotchLit`, drawn in gold, distinct from the white boss `PhaseNotches` |
| Defy seal | removed: `StoryHUD.Pulp.cs` badge (:97, :403-408), `StoryHUD.cs` (:96-97, 179, 248, 256, 282, 670-688, 1238), `StoryHUD.tscn` `DefySeal` and `DefyLabel` nodes (:250, :261) | removed: `FighterHUD.cs` (:62-69, 100-110, 146-147, 258, 329-335, 681, 801-820, 1088-1090), `FighterHudModel.cs` (:301-312), `FighterHUD.tscn` (:166, :379) |
| Not-yet flash | unchanged path; it now also fires on a 0-pip refusal (§2.2) | unchanged path (the classifier, §2.2) |

- "Bound" is read through `InputMap.ActionGetEvents("gameplay_defy")` for the device kind, refreshed when `InputBindingService.Generation` changes.
- `HudAbilityIndicatorModel` gains `SyncReuseDelay(slot, seconds)` and `SyncMomentum(pips)`; `SyncCooldownAuthority` keeps the movement half.
- Theme: no new Label role is expected (reuse `Res2`, `Accent`, `Ink3`, `GoldBright`). A role proven necessary goes through `tools/ui_theme/build_ftt_theme.py` only (§1.13). `DefyLabel` and `DefyReadyGlow` retire in MM-SWEEP.
- Captures (`scripts/Diagnostics/UiCapture/UiCaptureTargets.Huds.cs`, `UiCaptureTargets.Hudf.cs`): remove the seal seeds (:85-88, :113, :245), keep every target name, add `huds_momentum`, `huds_defy_notch` (bound and unbound), `hudf_momentum`. Captured in the integration worktree after the merge (a window and the lock).
- en.csv `# MM-HUD block`: `hud_momentum_pips` (accessible name); delete `hud_defy_label` with its node.
- **Ledger.** `USER-MOMENTUM-HUD` (G5.1), `USER-DEFY-HUD-NOTCH` (4.2), `PROVISIONAL-MOMENTUM-HUD-LAYOUT` (O19, the row size, the slab growth, P15); amends `USER-STORY-HUD-COMPACT` / `PROVISIONAL-STORY-HUD-COMPACT` and `HUD_CONTRACT.md`.

### 2.11 Presentation · 4.1, O17, P29, P30 · MM-FX

The "full time-break" (4.1):
- **World freeze**: the 8-frame blast hitstop is sim / Story state (§2.5), not presentation.
- **Ring**: new pooled, code-drawn `scripts/Combat/DefyBurstEffect.cs` on the `BlockSparkEffect` pattern (`Show(position, parent)` registers its own pool): a gold ring expanding to the blast radius (150 px Story, 156.25 px Fighter) over 0.4 s; bright fills follow `ComfortSettings.FullScreenFlashAllowed`.
- **Pulse**: new `scripts/UI/DefyScreenPulse.cs`, a short `CanvasLayer` + `ColorRect` with `assets/shaders/ui_desaturate.gdshader`, gated by `ComfortSettings.ScreenTintPulseAllowed` (Reduced Temporal Effects plays only the ring). `CameraShake` small (accessibility scale applies).
- **"DEFIED" label**: new `FloatingDamageNumber.ShowText(string text, Vector2 position, Node parent, Color tint, float scale)`, `UIPalette.GoldBright` at 1.25×, shown even with damage numbers off; key `defy_world_label`. Replaces any damage number on the triggering frame.
- **Meter visibly losing half**: the HUD (§2.10).
- **Audio**: new `CoreMechanicCueIDs.DefyBurst` ("defy_burst", Critical, silent stream) in `CoreMechanicCueCatalog.cs` and `resources/Audio/core_mechanic_cues.tres`; retire `DefySealShatter` and `DefyToll` (P29); `AudioSnapshot.DefyOrScripted` requested for the freeze window (`AudioSnapshotMixer.cs:34, 135, 154`).
- **Glow**: the documented post-Defy aura (`GlowStateModel.cs:43`) during the invulnerable frames 1–6.
- **Pose**: P30.
- **Story wiring**: `CoreMechanicCuePresenter` subscribes to `EventBus.OnDefyExecuted` (the label, ring, pulse, cue, snapshot); the glow and pose ride the partial hooks `PlayerController.PresentDefyStart()` / `PresentDefyBlast(in DefyExecutedPayload)` that MM-RULES declares and MM-FX implements in new `PlayerController.DefyFeedback.cs`.
- **Fighter wiring**: new `FighterSimulationDriver.DefyFeedback.cs` edge-detects the 323 Defy counter (bits 12–15) and the blast-fired bit on a short forward step only, with one call line in `SyncPresentationFeedback`; pose and label cases in `ResolvePresentationAnimation` / `GetStateLabel`.
- **Ledger.** `USER-DEFY-TIME-BREAK-PRESENTATION` (4.1), `PROVISIONAL-DEFY-PRESENTATION` (O17, timings and sizes), `PROVISIONAL-DEFY-POSE` (P30).

### 2.12 CPU · 4.4, G4.4, O23, P24, P25 · Fighter and the Mirror · MM-CPU

**Specials (G4.4).** One predicate replaces the four cooldown tests (CPU:1225-1226, 1630, 1634, 1652): `SpecialPipReady(slot)` = `(!obs.MomentumKnown || obs.SelfMomentumUnits >= MomentumSpecialCostUnits) && obs.SelfSpecialReuseFrames(slot) <= bandMinDelay`. The `NextPercent()` draws (`SpecialOneClosePercent`, `SpecialTwoPercent`, `SpecialOneRangedPercent`) stay in the same order and stay behind the ready test, so Easy (zero percents) is unchanged and no draw moves for a ready Special. It stays kit-blind (9.3). `MobilitySpecialReady` (dormant) reads the same predicate.

**Observation fields** (`CpuDecisionObservation.cs`, filled by `Observe`, CPU:700-726, and the Mirror adapter): `MomentumKnown` (P24), `SelfMomentumUnits`, the reuse fields renamed `SelfSpecialOneReuseFrames` / `SelfSpecialTwoReuseFrames`, `SelfDefyFrames`, `TargetUltimateActivationPhase` and `TargetUltimateActivationPhaseFrames` (through `ICpuWorldObserver.TryGetVerbState` / `CpuVerbState`, which already carries self `UltimateCasterBusy`).

**Defy (O23, P25).** `CpuBandTuning` (code-owned, never a resource) gains `DefyStringPercent`, `DefyMinStringHits`, `DefyCornerEscape`, `DefyBeatsUltimateWindup`:

| Band | `DefyStringPercent` | `DefyMinStringHits` | Corner | Wind-up | Otherwise |
|---|---|---|---|---|---|
| Easy (and `EasyBoss`) | 0 | — | no | no | never |
| Medium | 35 (rolled once per hitstun instance) | 3 | no | no | never |
| Hard | no roll (always on trigger) | 2 | yes | yes | banks its meter for the Ultimate |

- Reflex path: a branch in `ApplyHitstunDefense` (CPU:509-568) beside the Block hold, on the existing `newInstance` edge (CPU:512-518); it writes Defy into `held` so `PlayerInputFrame.Create` derives the edge. A press never needs the schedule ring there.
- Neutral uses (corner, wind-up): Defy joins `EdgeButtons` (CPU:71-74) and is scheduled with the band's reaction delay.
- Gates: Influence 50+ (`InfluenceRaw`), not already Defying, not captured, the target within 3.0 u for the wind-up trigger. The draw happens only when the gate passes (P25).
- String counter: CPU-local and input-side (the `_hitstunHoldBlockActive` precedent): consecutive hitstun instances with at most 8 actionable frames between them.

**Mirror clone** follows its band (`CpuBandTuning.BossOverride`): `MirrorParadoxController.Observe()` (`:654-710`) fills the new fields from `PlayerController.MomentumPips`, `SpecialReuseRemainingSeconds`, `IsDefying` and the hero's activation phase (the hero has none; 0). The clone emits Defy through `InputManager.SetInputSource`; its cost is unscaled (it is not the campaign hero).
**Seeded expectations move** for Medium and Hard (`FighterCpuBehaviorTests`, `CpuVerbPolicyTests`, `RollbackReadinessTests` CPU matches, `FighterCpuSpecialTimingSmokeTests`): re-time or re-record, never weaken. **Ledger.** `USER-CPU-PIPS-LIKE-COOLDOWNS` (G4.4), `USER-DEFY-CPU-BY-BAND` (4.4), `PROVISIONAL-CPU-DEFY-REFLEX` (O23, P24, P25).

### 2.13 Bots and telemetry · O24, P26 · Story diagnostics · MM-BOTS

- **Ready reads.** `PlaytestBots.TryConfirmSpecial` (PlaytestBots.cs:804-822), the Specials phase of the `feel` probe (:1061-1095; `PlaytestFeel.cs:125, 128`) and `PlaytestTelemetry` (:800-822) read `IsSpecialReady(slot)` and `MomentumPips` through new getters in `PlaytestWorld`'s "Hero gates" region. Audit the `feel` probe's lockout segment (frames 702–715): the 2-pip start covers its two presses.
- **Policy** (P26): `play` presses Defy (via the `GameplayButtons.Defy` bit through `SetInputSource`) at Influence 50+, in hitstun within a string, with 2+ enemies within 250 px, at most once per 300 frames. `spam`, `basics`, `bypass` and `feel` never do.
- **Telemetry** (`PlaytestCombatStats.cs`, `PlaytestTelemetry.cs`): a `defy` block (executed, frame, meter before, targets blasted, HP healed, hits taken in the 180 frames before and after, `died_within_3s`, refused by reason) replacing `defy_procs` (`MeterTracker.DefyProcs`, :808-880; the report key at :1567); a `momentum` block (pips at level start, which should read 2.0 like `meter0`; gained by source; spent per slot; starved presses; frames at 0 and at 4 pips); `RefusalLabels` (:106) drop `cooldown` and gain `no_momentum` and `reuse_delay`. `PLAYTEST_RESULT`, `run_playtests.ps1`'s CSV (`defies`, `defy_died3s`, `starved`), `tools/playtest/compare_runs.py` (:268-323, :609) and `trace.py` (`mo=`) follow.
- **Comparability.** Bot batches from before and after this package are not comparable (the gauge, the 0.5 scale, the active Defy). The reference pair runs in a clean worktree (§5.2).
- **Ledger.** `PROVISIONAL-BOT-DEFY-POLICY` (O24, P26), amends `USER-BOT-COMBAT-TELEMETRY`.

### 2.14 The Level 0 lesson and the Story text · 5.1, O25, P27 · MM-SDEFY (lesson) / MM-TEXT (copy)

**Lesson (MM-SDEFY).** `Level00Controller.BeginMeterAndDefyLesson` / `ProcessMeterAndDefyLesson` (Level00Controller.cs:864-927) and `TutorialCalibration` (`TutorialCalibration.cs:36, 177-250`):
1. `SetUltimateMeter(BasicComboRules.DefyInfluenceCost)` (a direct set, unscaled; it was 100).
2. A drone swarm (three of the lesson's existing drones, provisional) traps the hero in a string; `ScriptedHPFloor = 1` for the step so it cannot be lost (restored on every exit).
3. The step completes only on `EventBus.OnDefyExecuted` from the hero (`TutorialCalibration.RegisterDefyExecuted`, replacing `RegisterDefyProc`; `SkipDefyLesson` and the `StoryDefyHistoryUsed` resume fallback go).
4. The objective names the bound button (`tutorial_step_defy`, rewritten with a `{0}` argument filled from the binding label of `gameplay_defy`); the done line `tutorial_step_defy_spent` is rewritten to report the blast and the heal.
5. The scripted lethal hit and the seal raise (:883, :921-925) are deleted.

**Copy (MM-TEXT), rows rewritten in place.** Drafts below are provisional; quote any value with a comma.

| Key | Draft value |
|---|---|
| `systems_card_defy_title` | Defy History |
| `systems_card_defy_body` | "Press Defy with at least half a bar of Influence (50) to break free of almost anything. You are untouchable for an instant, then a shockwave blasts nearby enemies away and staggers them, and you reclaim your Rally echo (at least a tenth of your health). It shares the bar with your Ultimate." |
| `systems_card_momentum_title` | Momentum |
| `systems_card_momentum_body` | "Land hits to build Momentum, spend it on Specials. You hold up to four pips and every Special costs one. Momentum refills slowly on its own and faster when your basic attacks land. Each Special then rests for 2 seconds before it can be used again." |
| `dlg_l00_station_defy` (Level 0 station line) | "When they pin you down, Defy. Half a bar breaks history's grip on you, and buys back what they took." |
| `status_suppression_tooltip` | drop "Cooldowns keep running." |
| `resonance_joan_piercing_cooldown_*`, `resonance_mozart_wave_cooldown_*`, `resonance_lincoln_splitting_cooldown_*` (names and descriptions) | "+10% Momentum regeneration" (G4.3 placeholder) |
| the Requiem Chord description (find its `resonance_` / ability description row) | drop the Fortissimo shave clause |
| the Siphon Snare text, if any row states the Defy-disarm clause | drop the clause |

**New keys (MM-TEXT):** `tooltip_first_use_defy_button` (P27; names the button through its label), `movelist_ability_momentum` (a Special's cost line, quoted: "Damage {0}, costs {1} Momentum, rests {2} s"), `movelist_defy_title` / `movelist_defy_body`. `tooltip_first_use_defy` is deleted with its reader.
**Tooltip wiring (MM-TEXT).** `FirstUseTooltips.DefyButton`; `FirstUseTooltipPresenter` drops its seal listener (:121, 327-331) and shows the tooltip on the first tick the hero's meter reaches 50 in a level.
**Ledger.** `USER-DEFY-LEVEL0-LESSON` (5.1), `PROVISIONAL-DEFY-TEXT` (O25, every draft above, P27). The design master's Sarah line (D:4324) follows MM-TEXT's final value.

### 2.15 Grid nodes and kit rules · G4.2 + R1, G4.3, O6–O8, P1, P16, P17 · MM-SMOM / MM-SIM / MM-SWEEP

| Rule | Change | Owner |
|---|---|---|
| `joan_piercing_cooldown`, `mozart_wave_cooldown`, `lincoln_splitting_cooldown` (`resources/Resonance/joan_grid.tres:80-91`, `mozart_grid.tres:86`, `lincoln_grid.tres:85`) | stat key `CooldownReduction` → `MomentumRegen`, `AbilityScope` removed, `GatedAbilityID = "special2"` kept, value 0.1 (P16) | MM-SMOM |
| `ResonanceProgression` (:399-468) | resolves `MomentumRegen` character-wide to `1 + sum`; the scoped-cooldown branch is deleted if nothing else authors it | MM-SMOM |
| `CharacterFactory.ApplyActiveStoryProgression` | copies `MomentumRegenMultiplier` (the one path, live re-resolve included) | MM-SMOM |
| `ResonanceReaderCoverageTests` | the new key's reader; no authored scoped cooldown remains | MM-SMOM |
| Requiem → Fortissimo shave | `MozartAbilities.ShaveFortissimo` (:119-124) and the sim `FighterKitMotion.Reach.cs:547-550` become no-ops (MM-SMOM Story, MM-SIM sim); `KitReachRules.RequiemFortissimoShaveSeconds` deleted once neither reads it (P17) | MM-SMOM, MM-SIM; MM-SWEEP deletes the constant |
| Vortex Step (Desert Mirage, movement) | **kept** (R1) | — |
| Mozart's staff refund, Extra Note, Joan's Wings Refresh | movement only, unchanged | — |
| The 18 Special `.tres` | `CooldownDuration = 0.0` explicitly (P1) | MM-SWEEP |

**Ledger.** `USER-GRID-COOLDOWN-MINORS-REGEN` (G4.3), `USER-REQUIEM-SHAVE-DISABLED` (G4.2 as amended by R1), `USER-VORTEX-STEP-KEPT` (R1), `PROVISIONAL-GRID-MOMENTUM-REGEN` (O6, P16), `DEFER-REQUIEM-FORTISSIMO-REDESIGN` (O7, P17: the 2.0 s value recorded), `DEFER-KIT-PASS-MOMENTUM` (per-kit costs and gains, Shield of Orléans, Mozart's lane).

### 2.16 Move List and Systems Card · O8, O25 · MM-TEXT

- `MoveListScreen.cs:422` formats `BaseDamage, CooldownDuration` for every row: Specials use `movelist_ability_momentum` (damage, `MomentumSpecialCostUnits / MomentumUnitsPerPip`, `SpecialReuseDelayFrames / 60`, all read live from `BasicComboRules`); the movement ability keeps `movelist_ability_stats`; a universal Defy row in both `MoveListMode`s (never Dormant, 5.3) shows the cost from `DefyInfluenceCost`.
- `scenes/ui/SystemsCard.tscn` (:117-137) keeps its nodes; only the values change (§2.14). `SystemsCardContentTests` and `MoveListScreenTests` pin the single-source rule.

---

## 3. Workstreams, phases and file ownership

### 3.0 Order of work

1. **Plan commit** on `main` (this file and `docs/recon-2026-10-31/`), from a tree without the line-ending churn.
2. **Before bot batch** in a dedicated clean worktree `D:/Projects/ftt-wt/mm-bots` at the plan commit (§5.2, tags `mmbefore0` / `mmbefore1`).
3. **Phase A (serial): MM-RULES**, one agent, from the plan commit. Its commit order is a contract (§3.2). Tag `mm-rules`, merge, import, full run (**3778**), a short Codex review of the interfaces; fix before anything branches.
4. **Phase B1 (parallel, seven agents)** branch from the merged, reviewed `mm-rules`: MM-SIM, MM-SMOM, MM-SDEFY, MM-INPUT, MM-HUD, MM-FX, MM-TEXT. Stagger the HUD/FX/TEXT starts if the lock queue grows; they are the lightest.
   - **Merge order: MM-INPUT → MM-TEXT → MM-FX → MM-HUD → MM-SMOM → MM-SDEFY → MM-SIM.** Small first, the two hub-heavy Story streams next, the sim last (its diff is the largest and its few shared hunks resolve best against the rest).
   - Each later branch rebases onto `main` before its merge and re-runs its filtered suites; MM-SDEFY rebases onto MM-SMOM (they share `PlayerController.cs` at declared sites).
   - After each merge the orchestrator regenerates `en.en.translation`, imports, builds, runs one full suite and reconciles against §4.2; captures after MM-HUD and after MM-FX.
5. **Review round B1** (Codex through `/cross-review`, large/risky triage), fix steps `MM-FIXB-<area>` as needed.
6. **Phase B2 (parallel, two agents)** branch from the reviewed B1 merge: MM-BOTS, MM-CPU. Merge **MM-BOTS → MM-CPU**.
7. **Phase C:** MM-SWEEP (one agent) from the B2 merge; then a Codex review of B2 + C with fix steps; the rollback re-measure, the smoke runs, the CPU smoke and the after bot batch (§5.2).
8. **Phase D (parallel, two agents)** from `main` after C: MM-DOCS-DESIGN and MM-DOCS-REPO.
9. **Closeout (orchestrator):** three consecutive green full runs, the baseline written into AGENTS.md / CLAUDE.md / the ledger index, §9 and §10 here, prune the `mm-*` worktrees, branches and the `mm-rules` tag.

### 3.1 File-ownership matrix (one owner per file per phase)

"Call lines" = a non-owner may insert only the named call lines in that file; bodies live in the non-owner's own partial.

| File / area | A | B1 owner | B1 call lines by others | B2 | C |
|---|---|---|---|---|---|
| `scripts/Combat/BasicComboRules.cs`, `StoryCombatRules.cs`, `InfluenceMeterRules.cs` | MM-RULES | — (read-only) | — | — | MM-SWEEP (D2 region) |
| `scripts/Core/PlayerInputFrame.cs`, `scripts/Networking/RollbackProtocol.cs` | MM-RULES | — | — | — | — |
| `scripts/Core/EventBus.cs` | MM-RULES (`OnDefyExecuted`) | — | — | — | MM-SWEEP (seal members) |
| `scripts/FighterSim/FighterSimulationComponents.cs`, `FighterSpecialCast.cs` (323 fields) | MM-RULES (renames, declarations) | MM-SIM | — | — | MM-SWEEP (310 spare rename) |
| `FighterSimulationSystems.cs`, `FighterEntitySystems.cs`, `FighterSimulation.cs`, `FighterLoadoutFactory.cs`, `FighterKitRules.cs`, `FighterKitMotion.Reach.cs`, `FighterUltimateActivation.cs`, `FighterActionRules.cs`, `FighterLockoutRefusalRules.cs`, `FighterMeterRules.cs`, new `FighterDefyRules.cs` / `FighterDefyBlastRules.cs` / `FighterMovementSystem.Defy.cs` / `FighterCombatSystem.DefyBlast.cs` | MM-RULES (mechanical rename, `Defy = 13`) | MM-SIM | — | — | — |
| `scripts/FighterSim/FighterMomentumRules.cs` (new) | MM-RULES | — | — | — | — |
| `FighterSimulationDriver.cs` | MM-RULES (rename only) | MM-SIM (proxy-feed lines :966-967) | MM-FX: one line in `SyncPresentationFeedback`, the Defy cases in `ResolvePresentationAnimation` / `GetStateLabel` | — | — |
| `FighterSimulationDriver.Momentum.cs` (new) | MM-RULES (stub) | MM-SIM (body) | — | — | — |
| `FighterSimulationDriver.DefyFeedback.cs` (new) | — | MM-FX | — | — | — |
| `FighterCpuController.cs`, `CpuDecisionObservation.cs` | MM-RULES (rename only) | — | — | MM-CPU | — |
| `scripts/Characters/PlayerController.cs` | MM-RULES (enum line, dead stock-loss path) | **MM-SDEFY** (owner) | MM-SMOM: `UpdateCooldowns`, `CheckSpecialInput`, `SpecialPressIsReady` / `ReportReadySpecialRefusals`, `NoteSwingLanded` / `ApplyMomentumRefund` / `RefundMomentum` / `OnSpecialCooldownArmed`, `ApplyShieldOfOrleans`, `CaptureStoryResourceTimers` / `RestoreStoryResourceTimers` | — | MM-SWEEP (dead getter) |
| `PlayerController.Momentum.cs` (new) | MM-RULES (stub) | MM-SMOM | — | — | — |
| `PlayerController.Defy.cs`, `PlayerController.DefyBlast.cs` (new) | MM-RULES (stub `Defy.cs`) | MM-SDEFY | — | — | — |
| `PlayerController.DefyFeedback.cs` (new) | — | MM-FX | — | — | — |
| `BaseSpecial.cs`, `MozartAbilities.cs`, `KitReachRules.cs` (no-op only), `ResonanceProgression.cs`, `ResonanceStatKeys`, the three grid `.tres`, `ChronalExtractor.cs`, `SearchlightZone.cs`, `SiphonTetherChannel.cs`, `ChronalOrbItem.cs`, `UltimateMeter.cs` | MM-RULES (`UltimateMeter` dead path) | MM-SMOM | — | — | MM-SWEEP (`KitReachRules` constant) |
| `CharacterFactory.cs` | — | MM-SMOM (`ApplyActiveStoryProgression`) | MM-SDEFY: delete :174-178 | — | — |
| `EnemyController.cs` (+ new `EnemyController.DefyBlast.cs`), `BossController.cs`, `StoryDefense.cs`, `StoryManager.cs` (Defy members), `PlayerController.EnvironmentalBlock.cs`, `EinsteinRelativityRift.cs`, `Level00Controller.cs` (lesson region), `TutorialCalibration.cs` | — | MM-SDEFY | — | — | — |
| `MirrorParadoxController.cs` | — | — (read-only) | — | MM-CPU (the adapter's `Observe` and Defy emission) | — |
| `scripts/Core/InputManager.cs`, `InputBindings.cs`, `project.godot`, `SaveManager.cs` (`ApplySavedInputBindings` call line and notice) | — | MM-INPUT | — | — | — |
| `StoryHUD.cs`, `StoryHUD.Pulp.cs`, `StoryHUD.tscn`, `FighterHUD.cs`, `FighterHUD.tscn`, `FighterHudModel.cs`, `HudAbilityIndicatorModel.cs`, `scripts/UI/Pulp/MomentumPips.cs` (new), `PulpAbilitySlot.cs`, `PulpBarDecor.cs`, `RadialProgress.cs`, `HudCapsule.cs`, `UiCaptureTargets.Huds.cs`, `UiCaptureTargets.Hudf.cs` | — | MM-HUD | — | — | — |
| `DefyBurstEffect.cs`, `DefyScreenPulse.cs` (new), `FloatingDamageNumber.cs`, `CoreMechanicCueCatalog.cs`, `core_mechanic_cues.tres`, `CoreMechanicCuePresenter.cs`, `AudioSnapshotMixer.cs`, `GlowStateModel.cs` / `GlowPresentationController.cs` (post-Defy channel only) | — | MM-FX | — | — | — |
| `MoveListScreen.cs`, `FirstUseTooltips.cs`, `FirstUseTooltipPresenter.cs` | — | MM-TEXT | — | — | — |
| `scripts/Diagnostics/*`, `tools/playtest/*` | — | — | — | MM-BOTS | — |
| `DefySealModel.cs`, theme generator + outputs, `assets/sprites/ui/glass/` + `glass_kit.json` + `tools/asset_src/glass/`, `UiCaptureTargets.Widgets.cs`, the 18 Special `.tres`, per-character content tests | — | — | — | — | MM-SWEEP |
| `localization/en.csv` | — | blocks: MM-INPUT, MM-HUD, MM-FX, MM-TEXT, MM-SDEFY (Level 0 keys) | in-place rows per §2 only | — | MM-SWEEP (deletions) |
| `en.en.translation` | orchestrator only, every phase |||||

In `PlayerController.cs`, MM-SMOM edits **inside the methods it names** (not only call lines); MM-SDEFY owns everything else in the file. Each puts new bodies in its own partials.

### 3.2 Workstream briefs

Each brief: goal · inputs · provides (exact names other streams consume) · tests · gate · handoff. §2 holds the rules; §4 the per-suite arithmetic.

#### MM-RULES — the rulebook and the interfaces (Phase A, serial) · `docs/handoffs/MM_RULES.md`
- **Goal.** Land every shared constant, the mechanical renames, the wire bit and inert stubs, so seven Phase B streams compile against the same names from one commit.
- **Commit contract.**

| # | Content | Fighter hash |
|---|---|---|
| 1 | The "Momentum gauge" and "Defy History" regions (§1.4), `FighterMomentumRules` (pure), `MeterDefyRulesTests` | identical |
| 2 | The 305 rename (`SpecialOne/TwoReuseFrames`) across every reader, behaviour unchanged (still armed from the authored cooldown) | identical (record a seeded replay hash before and after) |
| 3 | `InfluenceMeterRules` 1/2; `MeterGainScale` 0.5; `FlatEffectScale` / `FlatMeterScale`; delete the dead Story stock-loss path; rewrite their pins | moves |
| 4 | `GameplayButtons.Defy = 1 << 15`; `ProtocolVersion = 5`; `PlayerInputFrameTests` +1; the version pin | moves (protocol only) |
| 5 | Inert stubs: 323 `DefyFrames` and the flag constants; `FighterActionKind.Defy = 13`; `CharacterState.Defying`; `EventBus.DefyExecutedPayload` / `OnDefyExecuted` / `RaiseDefyExecuted`; `PlayerController.Momentum.cs` (`MomentumPips` = 2.0, `RestoreMomentumPips`, `IsSpecialReady`, `SpecialReuseRemainingSeconds`, `MomentumRegenMultiplier` = 1); `PlayerController.Defy.cs` (`IsDefying` = false, `DefyExecutionCount`, `CanDefyNow` = false, `partial void PresentDefyStart()`, `partial void PresentDefyBlast(in DefyExecutedPayload)`); `FighterSimulationDriver.Momentum.cs` (`TryGetMomentum` returning `MomentumStartUnits`). Tag **`mm-rules`**. | may move (323 storage) |

- **Provides.** Every name in commit 1 and 5; `DefyExecutedPayload { int PlayerIndex; Vector2 Position; float MeterBefore; int TargetsBlasted; int Healed; ulong Frame; }`.
- **Tests.** +7: `MeterDefyRulesTests` 7 (pure), `PlayerInputFrameTests` +1, `UltimateMeterTests` −1. In place: `InfluenceMeterRulesTests`, `FighterSimulationTests.StockLossRetainsSeventyFivePercentOfInfluence` → `StockLossRetainsHalfOfInfluence`, `StoryMeterGainScaleTests` and `StoryMeterGainFactorTests` (0.35 → 0.5), every Story suite with a 0.35 literal (B§4.1: `StoryRallyTests`, `StoryDefenceOrderTests`, `PlaceholderZoneTests`, `PuzzleEnvironmentToolkitTests`, `Level09ContentTests`, `ResonanceMajorReworkTests`, `MirrorParadoxTests`), `RosterSwapProtocolTests` (5), `FighterActionStateTests` (40 B), every 305-name reference, `InfluenceMeterSourceScanTests` (the reader list).
- **Gate.** Build with only the allowed warnings; one full run **3778**, reconciled; the rollback trio (`RollbackReadinessTests`, `FighterSimulationTests`, `RollbackProtocolTests`).

#### MM-SIM — the Fighter gauge and the active Defy (B1) · `docs/handoffs/MM_SIM.md`
- **Goal.** §2.1–2.7 and the sim half of §2.8 and §2.15, in this commit order: (1) the gauge, reuse delay, regen (P2), gains, Sudden Death, stock loss, the D10 classifier, the 306 and intent deletions, the Requiem no-op; (2) the passive Defy deleted (312 fields, all gates, `DefyBarred`); (3) the active Defy (start, buffer, frames, refusals, claim); (4) the blast and heal.
- **Inputs.** `mm-rules`; A§1–§2.
- **Provides (for B2).** `FighterVerbComponent.MomentumUnits`; `FighterRuntimeComponent.SpecialOneReuseFrames` / `SpecialTwoReuseFrames`; `FighterActionStateComponent.DefyFrames` and its flags; `FighterDefyRules.IsDefying / IsInvulnerable / CanBegin`; the seams `SeedFighterMomentumForTest`, `SeedFighterSpecialReuseForTest`, `SeedFighterDefyForTest`; `MomentumTestKit`; the driver's real `TryGetMomentum`.
- **Tests.** +18 (§4.1). Re-time every sim suite that presses Specials back to back (A§5: 14 suites, the kits, `RollbackReadinessTests`, `RollbackProtocolTests`) with `MomentumTestKit` or the seeds, assertions unchanged; the CPU suites stay green by re-timing only (MM-CPU adapts them in B2).
- **Gate.** No `KLOTHO_DET002`; the rollback trio; filtered §5.2; one full run reconciled; record the snapshot size and a replay hash after each commit.

#### MM-SMOM — Story Momentum, reuse delay and meter rates (B1) · `docs/handoffs/MM_SMOM.md`
- **Goal.** §2.1–2.3 (Story), §2.8 flats, §2.15 grid and Requiem (Story).
- **Inputs.** `mm-rules`; B§1, §2, §4, §5.
- **Provides.** Real `MomentumPips`, `RestoreMomentumPips`, `IsSpecialReady`, `SpecialReuseRemainingSeconds`, `MomentumRegenMultiplier`; `PlayerController.ScaleStoryFlatMeter(float)`; `ResonanceStatKeys.MomentumRegen`.
- **Tests.** +17: `StoryMomentumGaugeTests` 10, `StoryFlatMeterScaleTests` 4, `StoryResourceTimerPersistenceTests` +1, `LevelStartResourceResetTests` +1, `ResonanceProgressionTests` +1. In place: `StoryHitConfirmTests` (the refund cases become gauge gains), `SuppressionTests` (gauge untouched), `SiphonSnareTests` (5/s, cap 15), the extractor cases of `StoryEnvironmentalDamageTests`, `Level04ContentTests` (the searchlight drain), `StoryReachKitTests.TheRequiemChordShavesFortissimoOncePerExecution` → `TheRequiemChordLeavesFortissimoUntouched`, `ResonanceReaderCoverageTests`, the Shield of Orléans pins.
- **Gate.** Filtered §5.2; a headless Level 2 smoke; one full run reconciled.

#### MM-SDEFY — the Story active Defy and the Level 0 lesson (B1) · `docs/handoffs/MM_SDEFY.md`
- **Goal.** §2.4–2.7 (Story) and the §2.14 lesson. Commit order: (1) the passive Defy deleted with its tests (the dead `StoryDefyHistoryUsed` getter stays, returning false); (2) `Defying`, `TryBeginDefy`, the hitstop buffer, the refusals (P21); (3) the blast on mobs, bosses and the clone, the heal; (4) the Level 0 lesson.
- **Inputs.** `mm-rules`; B§3, C§8.4; rebases onto MM-SMOM before merging.
- **Provides.** Real `IsDefying`, `CanDefyNow`, `DefyExecutionCount`; `EventBus.RaiseDefyExecuted` from the hero and the clone; `EnemyController.ApplyDefyBlast`, `BossController.ApplyDefyBlast`, `PlayerController.ApplyDefyBlast`; `PlayerController.CashInEchoPool()`; `TutorialCalibration.RegisterDefyExecuted`. Calls the partial hooks `PresentDefyStart` / `PresentDefyBlast` (MM-FX implements them).
- **Tests.** +12: `StoryDefyTests` 11, `StoryDefyBlastTests` 9, `Level00FlowSceneTests` +1; deleted `StoryDefenceOrderTests` ×2, two Defy cases of `StoryEnvironmentalDamageTests`, `DefySealStateTests` (4), `StoryMeterGainScaleTests.TheMeterAndDefyLessonsLethalHitIsDefiedOnTheScaledCampaignHero`. In place: `TutorialCalibrationV76Tests` (`TheDefyBeatSpendsExactlyOneUseAndLeavesTheSealBroken` → the button lesson), `TutorialCalibrationTests`, `StoryTumbleTests` ("a Defy ends a tumble"), `FeelPassStoryTests`, `StoryRallyTests`, `TimeFreezeTests` (:658, "Defy is refused under Time Freeze").
- **Gate.** Filtered §5.2; headless smokes of `Level_00_Tutorial.tscn` and `Level_13_*.tscn`; one full run reconciled.

#### MM-INPUT — the Defy action and bindings (B1) · `docs/handoffs/MM_INPUT.md`
- **Goal.** §2.9.
- **Provides.** `InputManager.Actions.Defy`; the bit set on every device-driven frame; `InputBindingSet.RepairDefyPadDefaultCollision`.
- **Tests.** +7: `GamepadDefaultBindingTests.DefyDefaultsToR3AndTheDialogueLogToBack` (+1), `GamepadOverrideRepairTests` +2 (a collision unbinds Defy's pad half and raises the notice; no collision changes nothing, run twice), new `DefyBindingTests` 3 (keyboard F and pad R3 set the bit per device; the action is remappable, listed with `controls_action_defy`; reset to default restores F / R3), `TimeFreezeBindingMigrationTests` +1 (a Time Freeze row on Back keeps it, no repair). In place: `InputBindingSchemaTests` (action counts), `GamepadDefaultBindingTests` (the action-count floor), `UrPolish2Tests` (the Controls rows at 1.0 / 1.4 gain one row).
- **Gate.** Filtered §5.2; the load check after the `project.godot` edit; never open the editor.

#### MM-HUD — pips, lenses, notches (B1) · `docs/handoffs/MM_HUD.md`
- **Goal.** §2.10 in both HUDs; the seal widgets and nodes removed; the capture targets.
- **Inputs.** The MM-RULES stubs. Story cases drive the HUD through `RestoreMomentumPips`; Fighter cases feed the widget through a pure model seam (`FighterHudModel.MomentumPipFill(int units)`) because the driver stub returns the start value until MM-SIM merges. The orchestrator re-runs the Fighter HUD suites after the MM-SIM merge against the real gauge.
- **Tests.** +9: `MomentumPipsTests` 3, `DefyNotchTests` 3 (Story ring lit at 50+ and bound only; Fighter tube notch distinct from boss notches; unbound stays dim), `StoryHudCompactTests` +1 (the pip row under the Specials capsule, its size), `StoryHudPulpTests` −2 seal cases +1 (`TheSpecialLensesShowTheReuseDelayAndDimAtZeroPips`), `HudAbilityIndicatorModelTests` +1, `FighterHudSceneTests` +1, `PulpAbilitySlotTests` +1 (Starved). In place: `StoryHudCompactTests` size pins (the slab height), `StoryHudBoundsTests`, `StoryHudContractTests`, `FighterHudTopBandTests`, `FighterHudLayoutRulesTests`, `FighterHudModelTests`, `PulpBarDecorTests`, `MenuLayoutBoundsTests` if moved.
- **Gate.** Filtered §5.2; the orchestrator captures `huds_*` / `hudf_*` after the merge and compares with `D:/Projects/ftt-scratch/hud-compact/captures/`.

#### MM-FX — the Defy presentation (B1) · `docs/handoffs/MM_FX.md`
- **Goal.** §2.11.
- **Inputs.** `OnDefyExecuted`, the partial hooks, the 323 counter constants (all from MM-RULES); tests raise the event and step the counter directly.
- **Tests.** +7: `DefyBurstEffectTests` 2 (pooled reuse; fills follow `FullScreenFlashAllowed`), `DefyPresentationTests` 4 (Story: one event shows the ring and DEFIED even with damage numbers off and requests the snapshot and cue; the pulse is skipped under Reduced Temporal Effects; Fighter: a counter forward step spawns one ring, an unchanged or lowered counter spawns none), `CoreMechanicCueCatalogTests` +1 (`defy_burst` is Critical; the retired IDs are absent). In place: `CoreMechanicCueCatalogTests` (the H04 list).
- **Gate.** Filtered §5.2; a `TestArena.tscn` smoke.

#### MM-TEXT — Move List, Systems Card, tooltips, copy (B1) · `docs/handoffs/MM_TEXT.md`
- **Goal.** §2.14 copy and §2.16.
- **Tests.** +3: `FirstUseTooltipsTests` +1 (`defy_button` fires at the first 50 and never from the old seal), `MoveListScreenTests` +1 (Specials show the Momentum line from the rulebook, no cooldown; a Defy row in both modes), `SystemsCardContentTests` +1 (the Momentum and Defy bodies resolve and the scene keeps zero font overrides). In place: `SystemsCardContentTests` (the momentum stem), `CampaignLocalizationTests`, `NarrativeKnowledgeBoundaryTests` (run), `UnusedTranslationKeyTests` (run).
- **Gate.** Filtered §5.2; local import; `TranslationCsvShapeTests` green.

#### MM-CPU — the CPU and the Mirror (B2) · `docs/handoffs/MM_CPU.md`
- **Goal.** §2.12, against the real sim gauge and Defy.
- **Tests.** +10: new `CpuDefyPolicyTests` 6 (Easy never and draws nothing; Medium rolls once per instance at 3+ hits and only at 50+; Hard on a 2-hit string, in a corner, against a wind-up within 3.0 u; Hard banks otherwise; `EasyBoss` never; a full-sim case where a Hard CPU's Defy cancels an A02 wind-up), `CpuVerbPolicyTests` +2 (no Special under one pip; a hand-built observation ignores the gauge, P24), `FighterCpuHitstunDefenseTests` +1 (the Defy reflex wins over the Block hold on its tick), `MirrorParadoxTests` +1 (a Hard clone Defies through `SetInputSource` and its blast staggers the hero). In place: the projection cases of `MirrorParadoxTests`, `FighterCpuBehaviorTests`, `CpuSelfActionStateTests`, `CpuMeleeBandTests`, `CpuRecoveryMatrixTests` (re-run on Paris, Vesuvius, Nassau), `FighterCpuSpecialTimingSmokeTests` (records Specials and Defies per stage).
- **Gate.** Filtered §5.2; a headless `Level_13_*.tscn --quit-after 600`.

#### MM-BOTS — bots and telemetry (B2) · `docs/handoffs/MM_BOTS.md`
- **Goal.** §2.13.
- **Tests.** +4: `PlaytestCombatStatsTests` +2 (the `defy` tracker counts an execution, hits before and after and `died_within_3s`; the `momentum` block starts at 2.0 and books gains by source), new `PlaytestDefyPolicyTests` 2 (`play` presses only under its conditions and spacing; `spam` never). In place: `PlaytestCombatStatsTests` (the refusal labels), `PlaytestEncounterTelemetryTests` (run).
- **Gate.** Filtered §5.2; one `play` run on Level 2 (Joan, Normal) under the lock showing the `defy` and `momentum` blocks.

#### MM-SWEEP — retirements, data, scans (Phase C) · `docs/handoffs/MM_SWEEP.md`
- **Goal.** Delete §1.9's list after a repo-wide reference scan; zero the Special cooldown data (P1); add the scans.
- **Steps.**
  1. `rg` every retired identifier; nothing outside §1.9's list may remain.
  2. Delete the D2 region, `DefySealModel` and `DefySealTests`, the EventBus seal members, the dead `PlayerController.StoryDefyHistoryUsed` getter and `KitReachRules.RequiemFortissimoShaveSeconds`; rename 310 `DefyHistoryUsed` → `SpareInt1`.
  3. Remove `DefyLabel` and `DefyReadyGlow` from `tools/ui_theme/build_ftt_theme.py` and regenerate with `py tools/ui_theme/build_ftt_theme.py` (role count 70 → 69).
  4. Delete the `defy_*` icons from `tools/asset_src/glass/` (`glass_svg.py:409-436`, `build_contact_sheet.py:392-393`); rebuild with `powershell -NoProfile -File tools/asset_src/glass/build_all.ps1` (it takes the lock itself); revert the unrelated sidecar churn; drop the seeds in `UiCaptureTargets.Widgets.cs:194-196`.
  5. Delete the `hud_defy_*` keys.
  6. Set `CooldownDuration = 0.0` in the 18 Special `.tres`; rewrite the per-character content pins (`EinsteinContentTests`, `JoanContentTests`, `LeonardoContentTests`, `LincolnContentTests`, `CleopatraContentTests`, `TeslaContentTests`, `ShakespeareContentTests`, `MozartContentTests`, `TubmanContentTests`, `KitAlignmentContractTests`, and any Determinism suite that reads a Special's `CooldownDuration`).
- **Tests.** −2: `DefySealTests` −5; new `DefySealRetirementScanTests` 1 (no retired identifier under `scripts/`, `scenes/`, `resources/`, `tests/`, `tools/`, comments stripped, tokens assembled so the scan does not match itself); new `SpecialCooldownDataTests` 1 (pure, `System.IO`: every Special-slot ability authors `CooldownDuration = 0`); `InfluenceMeterSourceScanTests` +1 (nothing under `scripts/FighterSim/` names a §1.2 Story identifier). In place: `UIThemeTests` (69 roles, palette parity), `GlassKitContentTests`, `UnusedTranslationKeyTests`, the content pins above.
- **Gate.** Build; the load check and an import with no missing dependency; filtered §5.2 plus the content suites; the orchestrator's full run.

#### MM-DOCS-DESIGN — design master, mirror and contracts (Phase D) · `docs/handoffs/MM_DOCS_DESIGN.md`
- **Goal.** O26. Apply each hunk to the master `D:/Projects/fighters-through-time-docs-3/design-godot-v7.md` and the same hunk to `design-godot.md`, then `diff --strip-trailing-cr` the two. The **only** allowed differences are the three `DOC-MASTER-UNBOUND-BINDING` lines (`:2312` Ultimate row, `:2314` Grab row, `:4173` the C01c bullet, at `56031750`): each copy keeps its own Severed / Unbound wording and is edited by hand. The new Defy row sits beside them in the Controls table and is identical in both.
- **Design spans** (D§1, lines at `56031750`): the preamble revision block; :102; :196; :299, :305, :309; :341; :389, :392; :400-401 (the Level 0 lesson); :475-537 (grid stat types); :740-828 (FSM: Special input "if a pip and the reuse delay allow", a Defy row, the passive clause deleted); the data tables :913, :1635-1637, :1767-1769, :1789, :1791; :1123-1132 (rewritten as the Momentum gauge); :1150-1199 (AbilityData); :1275-1285 (casting); :1294-1301 (the meter table: Story ×0.5, flats, Defy, M09 50 %, F14, A02); :1334-1367 (Recoverable Health: item 3 replaced, the Rally cash-in); :1369-1374; :1454; :1529; :1981-1984 (EventBus); :2096-2148 (rewind, Collapse, Snap); :2171; :2214-2220 (save table); :2296-2336 (Controls: the Defy row, the log on Back, Time Freeze on L3, the Special rows); :3040; :3189-3190; :3357-3365 (onboarding); the HUD spans :3376-3538; audio :3757, :3763, :4971; CPU :4009-4040; :4146-4147 (Sudden Death); Sarah :4324 (MM-TEXT's final copy); :4920; sprites :4938, :4949-4951; the grid entries :2570-2581, :2809-2822, :2930-2941; :2789-2790 (Requiem disabled); :2884 (Vortex Step kept, R1); :2587.
- **Contracts**, the same hunk in the repo copy and the docs-3 master, then diffed (first record which pairs already differ, as the 2026-10-16 plan §1.1 did; preserve each file's line endings): `DEFENSIVE_EFFECTS` (D04 rewritten as the active burst; D03f / D03h Defy clauses; mentions at :3, :11, :47, :117, :123, :164); `ABILITY_DATA` (the cooldown column becomes "1 pip, 2 s reuse"; Requiem disabled; a Defy universal-verb row); `HUD_CONTRACT` (a dated amendment: pips, the Starved lens, the 50 notch, the seal retired, the slab height); `COMFORT_SETTINGS` (C01c gains the Defy direct action; the pulse under Reduced Temporal Effects; mind its Unbound lines); `FIGHTER_MATCH_RULES` (stock loss 50 %, Sudden Death 2 pips, Defy allowed); `STORY_PERSISTENCE` (the `momentum` key, the Defy dead data, no derivation, P32); `TEMPORAL_STATE_CONTRACT` (Echo Step leftovers removed; Defy and Momentum under Time Freeze and rewind); `CPU_COMBAT_POLICY` (pips, Defy by band, the dead Echo Step section); `MIRROR_PARADOX` (a Defy row); `ROLLBACK_STATE_CONTRACT` (:40-43, :49, :71, :97, :107); `CAMPAIGN_VALIDATION` (:49, :72); `COMBAT_VALIDATION` (:95, :123, :127); `CPU_RECOVERY` (:13).
- Do not commit the docs-3 repository; report each hunk per file.

#### MM-DOCS-REPO — ledger, AGENTS.md, CLAUDE.md (Phase D) · `docs/handoffs/MM_DOCS_REPO.md`
- **Ledger** (`docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md`): a new section **"Confirmed open deviations — Momentum gauge, active Defy and meter rates, 2026-10-31"** after "The 2026-10-27 compact Story HUD — index and design lines edited" (about :723) and before "Historical leads — dispositioned by Package 11": every `USER-*` row (quoting its ruling verbatim with its ID), every `PROVISIONAL-*` / `VERIFY-*` / `DEFER-*` row of §2 and the handoffs, and a row for each §0.6 resolution that is not already a sentence in one; then an index section "The 2026-10-31 meter and Defy update — index and design lines edited". Amend or strike the §0.5 rows and the compact-HUD rows (:714-727).
- **AGENTS.md**: the Klotho inventory (305, 306, 310, 312, 323; highest ID 323); the Combat bullets (the lockout and D1 kept; per-hit Momentum → the gauge; D01–D04 → the active Defy; Rally; the meter bullet: ×0.5, flats, 1/2); the Ultimate bullet (A02 vs Defy); the input bullet (Defy, R3, the log on Back, the repair); the HUD and glass bullets (pips, notch, seal gone); the CPU and Mirror bullets; the Story progression bullets (F10, Level 0); the protocol line (v5, no free bit); signature 8's "spent Defy seeding" note; a new known-gaps subsection for this package; the baseline paragraph (placeholders the closeout fills).
- **CLAUDE.md**: the baseline paragraph (filled at closeout); the bot notes (the `defy` and `momentum` blocks, the refusal labels, comparability); the "Where to start" rows (Character ability, Ultimate, Fighter simulation, Fighter CPU, Input remapping, HUD, Saves); the build-warning sentence if `KLSG_ECS004` went (P33).
- `docs/PERFORMANCE_BASELINE.md`: the re-measure (§5.2).

### 3.3 Shared-file map

Code (distinct regions, resolved by the merge order):
- `PlayerController.cs`: MM-SDEFY (owner) and MM-SMOM (inside its named methods) in B1; MM-RULES before; MM-SWEEP after.
- `FighterSimulationDriver.cs`: MM-SIM (proxy-feed lines) and MM-FX (one call line, the Defy pose and label cases).
- `CharacterFactory.cs`: MM-SMOM (`ApplyActiveStoryProgression`) and MM-SDEFY (deletes :174-178).
- `en.csv`: blocks by MM-INPUT, MM-HUD, MM-FX, MM-TEXT and MM-SDEFY; in-place rows by MM-TEXT and MM-SDEFY (`tutorial_step_defy*`) only.

Test files (method owners; the later merge resolves):
- `StoryEnvironmentalDamageTests`: MM-SMOM (extractor values, in place), MM-SDEFY (deletes two Defy cases).
- `StoryMeterGainScaleTests`: MM-RULES (0.5), MM-SDEFY (deletes the lesson case).
- `StoryDefenceOrderTests`: MM-RULES (a 0.35 literal, if any), MM-SDEFY (two deletions).
- `MirrorParadoxTests`: MM-RULES (0.35 literals), MM-CPU (+1, projections).
- `FighterSimulationTests`: MM-RULES (stock loss), MM-SIM (re-timing).
- `InfluenceMeterSourceScanTests`: MM-RULES (reader list), MM-SWEEP (+1).
- `GlassKitContentTests`, `UIThemeTests`: MM-SWEEP only.

---

## 4. Test reconciliation

Counts are `[TestCase]` attributes at `56031750` (re-counted for the suites named). A delta that differs from the table is explained case by case in the handoff.

### 4.1 Per workstream

| WS | New suites / cases | Deleted | Extended | Rewritten in place (0 delta) | Delta |
|---|---|---|---|---|---|
| MM-RULES | `MeterDefyRulesTests` 7 (pure) | `UltimateMeterTests.StockLossRetainsSeventyFivePercent` −1 | `PlayerInputFrameTests` +1 | `InfluenceMeterRulesTests`, `FighterSimulationTests` (stock loss), `StoryMeterGainScaleTests`, `StoryMeterGainFactorTests`, the 0.35-literal Story suites, `RosterSwapProtocolTests`, `FighterActionStateTests`, every 305-name reference, `InfluenceMeterSourceScanTests` | **+7** |
| MM-INPUT | `DefyBindingTests` 3 | — | `GamepadDefaultBindingTests` +1, `GamepadOverrideRepairTests` +2, `TimeFreezeBindingMigrationTests` +1 | `InputBindingSchemaTests`, `UrPolish2Tests` | **+7** |
| MM-TEXT | — | — | `FirstUseTooltipsTests` +1, `MoveListScreenTests` +1, `SystemsCardContentTests` +1 | `CampaignLocalizationTests` | **+3** |
| MM-FX | `DefyBurstEffectTests` 2, `DefyPresentationTests` 4 | — | `CoreMechanicCueCatalogTests` +1 | `CoreMechanicCueCatalogTests` (the list) | **+7** |
| MM-HUD | `MomentumPipsTests` 3, `DefyNotchTests` 3 | `StoryHudPulpTests` seal cases −2 | `StoryHudCompactTests` +1, `StoryHudPulpTests` +1, `HudAbilityIndicatorModelTests` +1, `FighterHudSceneTests` +1, `PulpAbilitySlotTests` +1 | `StoryHudCompactTests` (sizes), `StoryHudBoundsTests`, `StoryHudContractTests`, `FighterHudTopBandTests`, `FighterHudLayoutRulesTests`, `FighterHudModelTests`, `PulpBarDecorTests` | **+9** |
| MM-SMOM | `StoryMomentumGaugeTests` 10, `StoryFlatMeterScaleTests` 4 | — | `StoryResourceTimerPersistenceTests` +1, `LevelStartResourceResetTests` +1, `ResonanceProgressionTests` +1 | `StoryHitConfirmTests` (the refund cases), `SuppressionTests`, `SiphonSnareTests`, `StoryEnvironmentalDamageTests` (extractor), `Level04ContentTests`, `StoryReachKitTests`, `ResonanceReaderCoverageTests`, `StoryAbilityLockoutTests`, `StoryLockoutRefusalEventTests` | **+17** |
| MM-SDEFY | `StoryDefyTests` 11, `StoryDefyBlastTests` 9 | `DefySealStateTests` −4, `StoryDefenceOrderTests` −2, `StoryEnvironmentalDamageTests` −2, `StoryMeterGainScaleTests` −1 | `Level00FlowSceneTests` +1 | `TutorialCalibrationV76Tests`, `TutorialCalibrationTests`, `TutorialCalibrationContentTests`, `StoryTumbleTests`, `FeelPassStoryTests`, `StoryRallyTests`, `TimeFreezeTests` | **+12** |
| MM-SIM | `FighterDefyTests` 12, `FighterDefyBlastTests` 10, `FighterMomentumGaugeTests` 11, `FighterDefenseLayerTests` 2 (the shield-expiry and KO-clears-barrier cases moved out of the deleted suite) | `DefyProtectedRecoveryTests` −12, `FighterTimeSystemsTests` (two refund cases) −2, `RequiemCooldownShaveTests` (the shave case) −1, `FighterHitPipelineTests` (the Sudden Death Defy-bar case) −1, `FighterMatchFlowTests` (the dual lethal Defy trade) −1, `FighterVerbLayerTests` (defied hit, no echo) −1 | `FighterLockoutRefusalRulesTests` +1 | `FighterHitConfirmTests` (refunds → gauge gains), `FighterHitPipelineTests` (Venom at full meter now kills), `FighterMatchFlowTests` (Sudden Death: 2 pips, Defy allowed), `RequiemCooldownShaveTests` (no reuse written), `FighterAbilityLockoutTests`, `FighterLedgeGetUpTests`, `FighterCombatContractW3Tests`, and every Special-spacing suite (A§5, via `MomentumTestKit`) | **+18** |
| MM-BOTS | `PlaytestDefyPolicyTests` 2 | — | `PlaytestCombatStatsTests` +2 | `PlaytestCombatStatsTests` (labels) | **+4** |
| MM-CPU | `CpuDefyPolicyTests` 6 | — | `CpuVerbPolicyTests` +2, `FighterCpuHitstunDefenseTests` +1, `MirrorParadoxTests` +1 | `MirrorParadoxTests` (projections), `FighterCpuBehaviorTests`, `CpuSelfActionStateTests`, `CpuMeleeBandTests`, `CpuRecoveryMatrixTests`, `FighterCpuSpecialTimingSmokeTests` | **+10** |
| MM-SWEEP | `DefySealRetirementScanTests` 1, `SpecialCooldownDataTests` 1 | `DefySealTests` −5 | `InfluenceMeterSourceScanTests` +1 | `UIThemeTests`, `GlassKitContentTests`, `UnusedTranslationKeyTests`, the nine character content suites, `KitAlignmentContractTests` | **−2** |

**Total +92: 3771 → 3863.** Review-fix rounds add their own declared deltas.

### 4.2 Running totals at each merge (the orchestrator reconciles every full run against these)

| After | Total |
|---|---|
| MM-RULES | 3778 |
| MM-INPUT | 3785 |
| MM-TEXT | 3788 |
| MM-FX | 3795 |
| MM-HUD | 3804 |
| MM-SMOM | 3821 |
| MM-SDEFY | 3833 |
| MM-SIM | 3851 |
| MM-BOTS | 3855 |
| MM-CPU | 3865 |
| MM-SWEEP | **3863** |

### 4.3 Seams, defaults and scans (the cross-cutting test infrastructure)
- **Gauge seed** (MM-SIM): `SeedFighterMomentumForTest`, `SeedFighterSpecialReuseForTest`, `SeedFighterDefyForTest` and `tests/Determinism/MomentumTestKit.cs` (non-suite). The 14 sim suites that press Specials 30 ticks apart (the 2026-10-08 note) and the kit suites would starve on the 2-pip start or hit the 2 s reuse delay: they call `MomentumTestKit.KeepFull` before each scripted press, or re-time a same-slot press past 120 frames, with assertions unchanged. Story suites start at 2.0 pips and call `RestoreMomentumPips(4f)` where they cast more than twice.
- **Hand-built CPU observation** (MM-CPU): `MomentumKnown = false` by default, so every existing hand-built observation ignores the gauge (P24).
- **Source scans** (MM-SWEEP): no Special authors a cooldown (`SpecialCooldownDataTests`); nothing under `scripts/FighterSim/` names a Story scale or Momentum-regen identifier (`InfluenceMeterSourceScanTests`); no retired Defy-seal or D2 identifier anywhere (`DefySealRetirementScanTests`, its list: `DefySealModel`, `DefySealState`, `DefySealPayload`, `OnDefySealChanged`, `RaiseDefySealChanged`, `DefyProtectionAwaitControl`, `DefyProtectionFrames`, `DefyProcIdentity`, `DefyBarred`, `IsDefyUnspentAndUnbarred`, `BeginDefyProtection`, `EndOnOffence`, `DefySealShatter`, `DefyToll`, `SeedFighterDefyProtectionForTest`, `MomentumRefundFramesFor`, `MomentumGrant`, `MomentumRefundCapFramesPerCycle`, `RefundMomentum`, `ApplyMomentumRefund`, `MomentumRefundedFrames`, `RequiemFortissimoShaveSeconds`, the icon stems `defy_ready` / `defy_notready` / `defy_spent` / `defy_barred`, `hud_defy_`, `DefyReadyGlow`, the `DefyLabel` role).

---

## 5. Risks and validation

### 5.1 Risk register

| # | Risk | Mitigation |
|---|---|---|
| 1 | **Determinism and rollback.** Every Fighter hash moves (the gauge, reuse, Defy, 1/2 retention, Sudden Death, the 306 / 312 deletions). The Defy start sits above the hitstop gate in `FighterMovementSystem.Update`, the hub of every early-`continue` branch; a branch that skips the Defy clock would desync on resimulation. | Integer units, FP64 squared distance, no float (§1.3). MM-SIM records a seeded replay hash after each commit and runs the rollback trio; `FighterDefyTests` replays a Defy across a depth-7 correction and asserts hash equality. The closeout re-measures `RollbackReadinessTests` (worst depth-7 median, p95, breaches, the largest snapshot, expected about −40 B, P33). |
| 2 | **Presentation replay.** A ring, label, pulse or cue keyed to a sim value that a correction lowers would replay. | Short forward step of the 323 Defy counter only (`IsForwardCounterStep`), pinned by `DefyPresentationTests`. Reasoned, not exercised under a real rollback (LAN de-scoped). |
| 3 | **Signature-8-style leaks.** New suites set the meter, `ScriptedHPFloor`, the active save slot, a frozen tree or EventBus subscriptions; a leaked `ScriptedHPFloor` would make later Story deaths impossible, a leaked subscription double-count Defies. | §1.10: restore in `finally`; Level 0 lesson cases drive the scene through its own seams; subscribers unsubscribe in `_ExitTree`. A full run with a cluster of Story failures is read against signature 8 first. |
| 4 | **Two-stage retirement breaks a merge.** A Phase B stream deletes something another still uses. | §1.9 list; cross-edits listed; the orchestrator's build after each merge; MM-SWEEP's reference scan. |
| 5 | **Seven parallel agents on one lock.** | Filtered runs only for most streams (§1.11); HUD / FX / TEXT may start later; the orchestrator owns full runs. Signature 5 (pipe contention) reads as a partial total with 0 failures and exit 100: never accept it. |
| 6 | **The Story hub file.** `PlayerController.cs` is edited by MM-SDEFY and MM-SMOM at once. | Declared methods (§3.1), bodies in partials, MM-SDEFY rebases onto MM-SMOM. |
| 7 | **CPU RNG shifts.** Medium and Hard seeded expectations move; a Defy reflex in the hitstun layer can starve the Block hold. | The draw gate (P25); `FighterCpuHitstunDefenseTests` +1; re-record, never weaken; the closeout CPU smoke. |
| 8 | **Content pins on authored cooldowns.** Nine content suites and the kit suites read `CooldownDuration`. | P1 and MM-SWEEP after every reader is gone. |
| 9 | **The compact HUD is pixel-pinned** and the slab grows. | MM-HUD rewrites the size pins and re-runs the bounds suites at UI scale 1.0 and 1.4; captures after merge. |
| 10 | **Bots are not comparable** across the package. | One clean before / after pair (§5.2); compare pooled seeded batches; a change is real only when per-batch ranges do not overlap. |
| 11 | **Nobody plays it.** Every layer here (gauge pace, Defy timing, the blast, the heal, the HUD, the lesson, the CPU) is unplayed. | §5.3, run after the package and before the kit pass (G5.4). |
| 12 | **The wire is full** after bit 15. | `DEFER-INPUT-WIRE-FULL`; the next Fighter verb plans a packet-size change. |

### 5.2 Validation gates
- **Every workstream:** `dotnet build FightersThroughTime.csproj --nologo` (allowed warnings only); its filtered suites (below); the headless load check (an import after any `en.csv` edit); its smoke runs (`--quit-after 300`, exit 0, no script error; compare "resources still in use" and `Handle is not initialized` against a control scene); the handoff states the commands, their totals and "nobody played the build".

| WS | Filtered suites (at minimum; `--filter "FullyQualifiedName~<name>"`) |
|---|---|
| MM-RULES | `MeterDefyRules`, `InfluenceMeter`, `StoryMeterGain`, `UltimateMeter`, `PlayerInputFrame`, `RosterSwapProtocol`, `RollbackProtocol`, `RollbackReadiness`, `FighterSimulation`, `FighterActionState`, `MirrorParadox`, `StoryRally` |
| MM-SIM | `FighterDefy`, `FighterMomentumGauge`, `FighterDefenseLayer`, `FighterHitConfirm`, `FighterAbilityLockout`, `FighterLockoutRefusal`, `FighterTimeSystems`, `FighterHitPipeline`, `FighterMatchFlow`, `FighterVerbLayer`, `FighterLedge`, `FighterSpecial`, `UltimateActivation`, `RequiemCooldownShave`, `*KitTests`, `RollbackReadiness`, `RollbackProtocol`, `FighterSimulation`, `FighterCpu`, `CpuRecoveryMatrix` |
| MM-SMOM | `StoryMomentumGauge`, `StoryFlatMeterScale`, `StoryHitConfirm`, `StoryAbilityLockout`, `StoryLockoutRefusal`, `Suppression`, `SiphonSnare`, `StoryEnvironmentalDamage`, `Level04Content`, `StoryReachKit`, `ResonanceReaderCoverage`, `ResonanceProgression`, `ResonanceLiveApply`, `StoryResourceTimerPersistence`, `LevelStartResourceReset`, `MirrorParadox` |
| MM-SDEFY | `StoryDefy`, `StoryDefenceOrder`, `StoryEnvironmentalDamage`, `StoryTumble`, `FeelPassStory`, `StoryRally`, `TimeFreeze`, `TutorialCalibration`, `Level00FlowScene`, `EnemyController`, `BossController`, `EncounterLock`, `MirrorParadox`, `StoryMeterGainScale` |
| MM-INPUT | `DefyBinding`, `Gamepad`, `InputBindingSchema`, `TimeFreezeBindingMigration`, `PadMenu`, `InputDeviceFollow`, `UrPolish2`, `SettingsMenu` |
| MM-HUD | `MomentumPips`, `DefyNotch`, `StoryHud`, `FighterHud`, `HudAbilityIndicatorModel`, `PulpAbilitySlot`, `PulpBarDecor`, `MenuLayoutBounds`, `UIThemeTests` |
| MM-FX | `DefyBurstEffect`, `DefyPresentation`, `CoreMechanicCue`, `BlockSparkEffect`, `GlowPresentationController`, `FighterPresentationSync` |
| MM-TEXT | `FirstUseTooltips`, `MoveListScreen`, `SystemsCardContent`, `CampaignLocalization`, `UnusedTranslationKey`, `ScriptTranslationKey`, `SceneVisibleText`, `TranslationCsvShape`, `NarrativeKnowledgeBoundary` |
| MM-CPU | `CpuDefyPolicy`, `CpuVerbPolicy`, `FighterCpu`, `CpuSelfActionState`, `CpuMeleeBand`, `CpuRecoveryMatrix`, `MirrorParadox`, `RollbackReadiness` |
| MM-BOTS | `PlaytestCombatStats`, `PlaytestDefyPolicy`, `PlaytestEncounterTelemetry`, `PlaytestFlightModel` |
| MM-SWEEP | `DefySealRetirementScan`, `SpecialCooldownData`, `InfluenceMeterSourceScan`, `UIThemeTests`, `GlassKit`, `UnusedTranslationKey`, `*ContentTests`, `KitAlignmentContract` |

- **Orchestrator at each merge:** regenerate the translation, import, build, one full run reconciled against §4.2.
- **Closeout gates:** (1) three consecutive green full runs at the final commit, orphans drained and the lock held, each `Failed: 0, Passed: 3863, Total: 3863` (plus fix-round deltas); (2) the rollback re-measure (`--filter "FullyQualifiedName~RollbackReadinessTests" --logger "console;verbosity=detailed"`) into `docs/PERFORMANCE_BASELINE.md`; (3) smokes: `MainMenu.tscn`, `HubWorld.tscn`, `Level_00_Tutorial.tscn`, `Level_02_*.tscn`, `Level_13_*.tscn`, `TestArena.tscn --quit-after 1800`, `FighterStage_Paris*.tscn`; (4) the CPU smoke (`FighterCpuSpecialTimingSmokeTests` with its recorded Special and Defy counts; `CpuRecoveryMatrixTests` on the Open stages); (5) the **after bot batch** in `D:/Projects/ftt-wt/mm-bots` checked out at the closeout commit: Levels 2, 6, 7, 12, 13; Joan and Lincoln; `play`; Easy and Normal; seeds 0–1; `-MaxSeconds 0`; tags `mmafter0` / `mmafter1` against `mmbefore0` / `mmbefore1` (`compare_runs.py "mmbefore*" "mmafter*"`): completions, deaths, damage by source, Specials cast per minute, the after batch's `defy` table (executed, `died_within_3s`, the user's original complaint) and `momentum` table; then remove the worktree; (6) the captures (`huds_*`, `hudf_*`, the new targets) compared with the compact-HUD set.

### 5.3 The hands-on session (G5.4: after this package, before the kit pass)

**Setup.** Record the build commit, the stage or level, the heroes, the CPU difficulty, Block mode, Hazards, the device and the Reduced Temporal Effects setting. For each line write what was seen and whether it calls for a ruling. Then, in the same session, run the **owed 2026-10-16 pad checklist** (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` §5, rows 1–25), re-reading row 7 (comeback, now with the active Defy and 50 % retention) and row 10 (D3 routes, now gauge-limited).

| # | Topic | Setup | Look at | A ruling is needed if … | Row |
|---|---|---|---|---|---|
| 1 | Defy out of a string (Story) | Normal, a mob crowd (Level 2 or 6) | Is the moment noticed (freeze, ring, DEFIED, the pulse, the meter halving)? Does the hero now survive the next 3 s? | It still goes unnoticed, or the hero still dies right after | `USER-DEFY-TIME-BREAK-PRESENTATION` |
| 2 | Defy as a Fighter reversal | Two pads, neutral and pressure | Is an invulnerable reversal on 50 meter too strong? Is the 24-frame recovery punishable after a whiffed Defy? The Defier is +6 on blasted victims (P11) | It is a free escape, or never worth it | `PROVISIONAL-DEFY-FRAMES` |
| 3 | Defy against an Ultimate wind-up | Each kit's Ultimate | The blast cancels the activation; the caster's meter stays spent; Defy refused once held | It feels like a coin flip, or the Ultimate becomes useless | `USER-DEFY-VS-ULTIMATE` |
| 4 | Blast numbers | Crowds, a boss, the Mirror; Open stages | 2.5 u radius, the push (P7), the 30-frame stagger; a boss's telegraph cancelled, no stun; pit ring-outs (Q2) | Too small to read, too big, or a ring-out feels wrong | `PROVISIONAL-DEFY-BLAST`, `VERIFY-DEFY-BLAST-RINGOUT` |
| 5 | Heal | An empty and a full echo pool | Is a free 10 % with an empty pool too generous? | Defy becomes a heal button | `PROVISIONAL-DEFY-HEAL` |
| 6 | Two Defies | Both players press inside a few frames | Mutual Defies blast nothing; a later Defy wins (P5) | It reads as a bug | `VERIFY-DEFY-MUTUAL` |
| 7 | The meter economy | Story Normal and a 3-stock match | 50 Defy against 100 Ultimate on one bar; Story ×0.5; 50 % kept on stock loss | Ultimates never happen, or Defy is always up | `USER-STORY-METER-SCALE-HALF`, `USER-STOCK-LOSS-RETENTION-HALF` |
| 8 | Momentum pace | Each kit, idle and pressing | About one Special per 12 s idle and one per 5 s while hitting; the 2-pip start; Specials starved or spammed | Specials feel rationed out of the kit, or still spammable | `USER-MOMENTUM-GAUGE`, `PROVISIONAL-MOMENTUM-RULES` |
| 9 | Reuse delay | Same-Special repeats | 2 s per slot | Repeats feel locked out, or alternating S1/S2 is degenerate | `USER-MOMENTUM-REUSE-DELAY` |
| 10 | HUD | Story at UI scale 1.0 and 1.4; Fighter both players | The pips, the Starved lens, the 50 notch (ring and tube), the slab growth; the dormant pips in Levels 0–2 | Unreadable mid-fight | `PROVISIONAL-MOMENTUM-HUD-LAYOUT`, `USER-DEFY-HUD-NOTCH` |
| 11 | CPU Defy | Easy, Medium, Hard; the Level 13 Mirror on each | Easy never; Medium on long strings; Hard on strings, corners and wind-ups; whether Hard starves its Ultimate (Q4) | The CPU spends Defy robotically or never | `PROVISIONAL-CPU-DEFY-REFLEX` |
| 12 | Level 0 lesson | A fresh slot, Full calibration and Skip | The lesson completes only on the press; the tooltip names the button; the copy reads | The step stalls or confuses | `USER-DEFY-LEVEL0-LESSON`, `PROVISIONAL-DEFY-TEXT` |
| 13 | Input | Keyboard F, pad R3; the Dialogue Log on Back; a saved R3 rebind | Reachability; the collision notice | A press is lost or the notice misleads | `PROVISIONAL-DEFY-INPUT` |
| 14 | Story flat effects | Levels 4 and 9 searchlights, an Extractor, Level 12+ Siphon Snare | −2.5/s, −10, −5/s cap 15 | Hazards become harmless to the meter | `USER-STORY-FLAT-EFFECTS-SCALED` |
| 15 | Grid minors | Joan, Mozart or Lincoln with the node | +10 % regen is noticeable | It reads as nothing | `PROVISIONAL-GRID-MOMENTUM-REGEN` |
| 16 | Bot numbers | The after batch | `died_within_3s` after a Defy against the before batch's deaths after a passive proc | The rework did not change what the user complained about | `PROVISIONAL-BOT-DEFY-POLICY` |

---

## 6. Handoff format (`docs/handoffs/MM_<WS>.md`)

1. **Shipped:** what changed, with exact API names and constants.
2. **Files changed:** including cross-edits outside your ownership, each with its reason.
3. **Decisions:** every provisional number or behaviour you chose, one line of reasoning each; name the ruling or resolution you built against.
4. **Deviations from this plan:** what and why.
5. **Tests:** new suites and cases (+N); deleted cases (−N); rewritten cases named before → after; shared test files touched; how each new or rewritten case was seen failing; the suites run with totals; your `Total:` reconciled against §4.2.
6. **Measurements:** replay hashes per commit (MM-RULES, MM-SIM), the snapshot size, the Defier's frame advantage, the CPU smoke counts, the HUD slab size, a bot run's `defy` / `momentum` blocks.
7. **Not delivered / open:** the honest list, and "nobody played the build".
8. **For the docs:** design-master lines (at `56031750`, flagging any `DOC-MASTER-UNBOUND-BINDING` line), contract lines, ledger rows to write or amend, AGENTS.md / CLAUDE.md bullets.

---

## 9. Deviation log (append-only, filled from the handoffs and the orchestrator's merge record)

Each entry lists only what departed from §1–§5 and why, the workstream's test delta and its commits; the handoff is the full record. Every agent choice also has a ledger row (MM-DOCS-REPO).

Entry format:

```
#### MM-<WS> — <title> (`<branch tip>`, merged `<merge commit>`)
- Delta: declared +N, actual +M (<reason for any difference>); Total at merge <T>.
- Deviation: <what> — <why> — <ledger row>.
- Cross-edits: <file: change, reason>.
```

### Phase A

#### MM-RULES — constants, rulebooks and inert stubs (tip `9cba2b9a`, merged as `19df9808` plus the review fix `9cba2b9a`)
- Delta: declared +7, actual +7; Total at merge 3778.
- Deviation: none. The commit 3 stock-loss pins also changed `FighterSimulationTests` (18.75 to 12.5, renamed `StockLossRetainsHalfOfInfluence`) and `InfluenceMeterRulesTests`; a pin the plan's list missed, in `FighterCombatContractW3Tests` (80 retains 40), failed the first full run (`Failed: 1, Passed: 3777`) and was fixed in a follow-up commit.
- Cross-edits: 305's Special cooldown fields renamed `SpecialOne/TwoReuseFrames` across every reader (commit 2, behaviour unchanged); 323 grown 36 to 40 B (`DefyFrames`, `ActionFlagDefyBlastFired`, `ActionFlagDefyBuffered`, the counter bits); `GameplayButtons.Defy = 1 << 15`, protocol v5; `FighterActionKind.Defy = 13`, `CharacterState.Defying`.
- Review: Codex Phase A round 1, approve with nits (two overflow nits): the Momentum and Defy heal helpers now saturate instead of overflowing (`9cba2b9a`).

### Phase B1

Seven streams, merged into `mm/integ-b1` in the order input, text, fx, hud, smom, sdefy, sim (`--no-ff`): MM-INPUT `1d02f904`, MM-TEXT `c01eff87`, MM-FX `b9ab6fc9`, MM-HUD `4a4c364f`, MM-SMOM `cf3be892`, MM-SDEFY `4a3221c8`, MM-SIM `ca86b390`; the integration commits `ab532365` (the ruling below), `d3d00fd7` (the regenerated translation) and `62bb4e7e` (two cross-stream fixes), and the record `88ff3ad0` (`MM_INTEG_B1.md`). Total at the end of B1: 3851 (run 1 `Failed: 2`, run 3 `Failed: 0, Passed: 3851, Total: 3851`, 2 m 13 s).

#### MM-INPUT — the Defy action and its bindings (`e2050a6e`, merged `1d02f904`)
- Delta: declared +7, actual +7; Total at merge 3785 (summed).
- Deviation: none. Files: `project.godot`, `InputManager.cs`, `InputBindings.cs`, `SaveManager.cs` (a call and a notice), `en.csv`, tests. `InputBindingSchemaTests` and `UrPolish2Tests` needed no edit.

#### MM-TEXT — copy, tooltips, Move List and Systems Card (`eeae2775`, merged `c01eff87`)
- Delta: declared +3, actual +3; Total at merge 3788 (summed).
- Deviation: the first-use Defy tooltip names the button by the word "Defy", not a binding label: `gameplay_defy` did not exist in this branch (INPUT merges first; a follow-up may add a `{0}` argument). `FirstUseTooltips.DefyButton` (`defy_button`) replaces `Defy` (`defy`); an old `defy` id in a save's `ShownFirstUseTooltips` is dead data. The three grid-minor names were rewritten here and by MM-SMOM; the merge kept TEXT's. Ledger: `PROVISIONAL-DEFY-TEXT`.
- Cross-edits: `en.csv` rows named in the plan only.

#### MM-FX — the Defy presentation (`7219ab7b`, merged `b9ab6fc9`)
- Delta: declared +7, actual +7; Total at merge 3795 (summed).
- Deviation: the Fighter ring's timing (section 2 of the handoff); the orchestrator then ruled that the ring fires on the blast frame (`ab532365`, below). `OnDefySealChanged` / `DefySealPayload` stayed on `EventBus` for MM-SWEEP (two-stage retirement). Ledger: `PROVISIONAL-DEFY-PRESENTATION`.

#### MM-HUD — pips, the Starved lens and the 50 notch (`ccda9d00`, merged `4a4c364f`)
- Delta: declared +9, actual +10 (the plan counted two seal cases in `StoryHudPulpTests`; the file had one, the other lived in `DefySealStateTests` / `DefySealTests`); Total at merge 3805 (summed).
- Deviation: the Story slab is 484 x 189 (was 182) and the cluster bound 270 (was 260), tests renamed in place; the 0-pip flash has no new HUD code (the existing event path, pinned by `TheNotYetFlashPlaysTheCircleRoundTheLensRing`). Ledger: `PROVISIONAL-MOMENTUM-HUD-LAYOUT`.

#### MM-SMOM — the Story Momentum gauge and the flat-effect scale (`0e8fcca6`, merged `cf3be892`)
- Delta: declared +17, actual +17; Total at merge 3822 (summed).
- Deviation: the scoped `CooldownReduction` resolver branch was kept (plan 2.15 deleted it "if nothing else authors it"): `BaseSpecial` still reads `StoryScoped(CooldownReduction, ...)` for the movement ability, and a synthetic case exercises it. `SpecialPressIsReady` changed only its doc. Ledger: `PROVISIONAL-GRID-MOMENTUM-REGEN`.
- Cross-edits: three `en.csv` rows rewritten by both TEXT and SMOM; the merge kept TEXT's.

#### MM-SDEFY — the Story Defy and the Level 0 lesson (`8953f554`, merged `4a3221c8`)
- Delta: declared +12, actual +12; Total at merge 3834 (summed).
- Deviation: the first three commits are not independently green (`PlayerController.cs` hooks landed with the deletions; `IsDefyProtected` needs `_defyFrame`), only the tip is verified. `StoryAttemptState.cs:366` (legacy recovery sets `DefyHistoryUsed = true`) was left for MM-SWEEP because `SaveEnvelopeTests` pins it. `tutorial_step_defy` / `tutorial_step_defy_spent` were rewritten in place with no new keys. Ledger: `PROVISIONAL-DEFY-TEXT`.

#### MM-SIM — the Fighter Momentum gauge and Defy (`6d7ae048`, merged `ca86b390`)
- Delta: declared +18, actual +17 (`FighterVerbLayerTests` also lost `DefyHistorySavesExactlyOneLethalHitAtFullMeter`, a second passive-save case the plan did not list); Total at merge 3851.
- Deviation: **P5 was wrong in one detail.** A Defy begun later does not catch the earlier Defier in its recovery from "1 or more ticks later": the earlier Defier holds frame 6 and one invulnerable frame through its own 8-frame freeze, so it is invulnerable through press + 13; a later Defy catches it only if its blast lands on press + 14 or later, that is from a press 9 or more ticks after the earlier one. Simultaneous Defies still blast nothing. `TwoDefiesOnTheSameFrameTouchNeither...` pins both halves. The snapshot is smaller than P33's estimate in 312 and 306 (-16 B and -8 B a fighter) while 323's +4 B is per fighter (40 B), about -40 B a snapshot in all, as planned. Ledger: `VERIFY-DEFY-MUTUAL`, `PROVISIONAL-FIGHTER-SNAPSHOT-SIZE`.

#### MM-INTEG-B1 — orchestrator rulings and cross-stream fixes (`ab532365`, `d3d00fd7`, `62bb4e7e`, record `88ff3ad0`)
- Ruling: **the Fighter Defy ring fires on the blast frame**, so the 323 counter steps at frame 6, not in `FighterDefyRules.Begin` (new `FighterDefyRules.StepCounter`, called where `ActionFlagDefyBlastFired` is set); the pose still reads `DefyFrames`; `FighterDefyTests` follow.
- Ruling: the three grid minors (`resonance_{joan_piercing,mozart_wave,lincoln_splitting}_cooldown_name`) all read "Minor Momentum"; Mozart's chip had been set to "Minor Wave Regen" so `ResonanceGridFitTests` fit. Both superseded by the "Minor Recharge" ruling below.
- Cross-stream fixes (run 1 failed 2 of 3851): the Mozart chip width above, and `StoryDefyBlastTests.TheMobIsPushed...`, which asserted `MomentumPips` unchanged while SMOM's passive regen runs during the Defy frames; it now asserts the gain is under 12 frames of regen.

### Review round B1 and fix steps

Codex review of Phase B1: round 1 changes requested (3 major, 2 minor); round 2 changes requested (finding 4 reworked); round 3 approve with nits (two test nits, carried to MM-SWEEP).

#### MM-FIXB1 — the five findings (`b4a848e8` / `e8355b19`, based on `88ff3ad0`)
- Delta: declared +9, actual +9 (+6 to 3857, then the rollback-dedupe redo +3); Total at merge 3860.
- Fixes: (1) a Defy replaces the cancelled action's invulnerability with its own 6 frames unless respawn protection is longer; (2) the blast stagger cannot be Block-cancelled; (3) a buffered launch no longer overwrites the push; (4) the rollback replay of the burst is deduplicated; (5) a boss takes the blast's 8-frame hitstop.
- Deviation: finding 4's first fix (a 30-sim-tick guard) swallowed a legitimate second Defy; round 2 replaced it with identity and lifecycle dedupe by counter value (`FighterSimulationDriver.DefyFeedback.cs`, presentation only, reset on respawn-platform frames and at the first Sudden Death frame). **Known limit:** after a correction that removes a Defy entirely, a later genuine Defy that lands on the same counter value is suppressed once. Ledger: `PROVISIONAL-DEFY-PRESENTATION`.

### Phase B2

#### MM-BOTS — bots and telemetry (`64d35398`, merged `1eb66b9d`)
- Delta: declared +4, actual +4; Total at merge 3864.
- Deviation: none from the plan. `MinEnemiesNear` was 2 in the handoff; **the orchestrator ruled it 1** (`632c393b`) so the after batch actually exercises Defy (with 2 the unmodified runs pressed none). `MM_BOTS.md` still says 2. Ledger: `PROVISIONAL-BOT-DEFY-POLICY`.

#### MM-CPU — the CPU and the Mirror (`03a413dc`, merged `ad2ae3fe`)
- Delta: declared +10, actual +10; Total at merge 3874.
- Deviation: the observation keeps the names `SpecialOne/TwoCooldownFrames` (they hold the reuse delay; MM-SWEEP renamed them to `...ReuseFrames`); the corner and wind-up Defies need the target within 3.0 u (the blast is 2.5 u), where the plan listed the range only for the wind-up; the Mirror projects a hitstun from the clone's damage events, because a Story body stays `Stunned` straight through a chain (not in the plan). Ledger: `PROVISIONAL-CPU-DEFY-REFLEX`.

### Phase C

#### MM-SWEEP — retirements, data and scans (merged `544ce84c`)
- Delta: declared -2, actual -1 (`DefySealTests` -5; `DefySealRetirementScanTests` +1, `SpecialCooldownDataTests` +1, `InfluenceMeterSourceScanTests` +1, and the round-3 nit that replaced one `DefyPresentationTests` case with two, +1); Total at merge 3873.
- Deviation: the generator reports 72 theme roles (the plan said 70 to 69; other workstreams added roles and no test pins a count); the four `defy_*` icons were deleted and the kit rebuilt (30 icons); 310's `DefyHistoryUsed` became `SpareInt1` (310 still 128 B); `KitReachRules.RequiemFortissimoShaveSeconds` (value 2.0) was deleted and recorded for the Requiem redesign row; the scoped cooldown branch stays (movement ability); the Special `.tres` author `CooldownDuration = 0.0` explicitly (P1). Ledger: `DEFER-REQUIEM-FORTISSIMO-REDESIGN`, `PROVISIONAL-SPECIAL-COOLDOWN-DATA-ZERO`.
- Nits (Codex B1 round 3) done here: the stock-reset presentation case now runs through the driver's sync path for both players and for Sudden Death, and five cases assert the `InputManager` prerequisite instead of passing silently.
- Finding for the orchestrator: with a 120-frame reuse delay Einstein S1, Leonardo S1, Tesla S2 and Mozart S1 can re-cast while their earlier shot or zone lives, so a late hit could arm D3 for the wrong cast in the sim. The orchestrator ruled that the sim attribute a DirectHit to the cast that fired it, as Story does since R12, and put the fix in the B2+C round (`FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION`).

### Phase D

#### MM-DOCS-DESIGN and MM-DOCS-REPO — documentation (`3bef3346` / `48ad34f0`, merged `b1f8dbc5` / `5ec89171`)
- Delta: none (documentation only); Total unchanged at 3873.
- Deviation: the ledger index's design-line column points at `MM_DOCS_DESIGN.md` (the brief allowed it; the file did not yet exist in the repo worktree); `docs/PERFORMANCE_BASELINE.md` carried a marked-owed section instead of a table (filled at the closeout).

#### Review round B2+C and MM-FIXB2C (`0d426f25`, `6f3eb7c9`, `f269b611`)
- Codex B2+C: round 1 changes requested (1 major, 3 minor); round 2 approve with nits (1 test nit, fixed in `f269b611`).
- Delta: round 1's fixes +5, round 2's nit +1; Total 3879.
- Fixes: (1, major) the CPU's Defy legality rejected daze while the sim allows Defy in daze: `FighterDefyRules.CanBeginCore` is the shared predicate; (2) the Mirror counted periodic damage as a string hit; (3) the 3.0 u corner and wind-up triggers measured the horizontal axis only; (4) Defy-window deaths were booked on `Frame`, not `ObservationFrame()`; (5, ruling) the sim stamps cast identity: 323 gained `SpecialCastId` (40 to 44 B a fighter), projectiles (301) and zones (309) a `CastId`, and D3 arms only for the cast in progress; the tripwire `OnlyTheKnownDirectHitSlotsCanOutliveTheReuseDelay` became `OnlyAContactFromTheCurrentCastArmsTheLandedDirectHitFlag` plus `NoteLandedArmsOnlyForTheCastInProgress`, and the nit added `TwoRealCastsGetDistinctIdsAndOnlyTheCurrentCastsHitArmsD3` (a snapshot across the overlap); (6, ruling) the three grid minors were renamed "Minor Recharge" in `en.csv`, `en.en.translation`, the design mirror and the master (the diagram labels, three table rows and the Section 4 sentence), superseding "Minor Momentum".
- Cross-edits: `AGENTS.md` row 323 (eleven ints, 44 B); `design-godot-v7.md` in docs-3 edited (not staged there); no new component ID, no protocol or save change; every Fighter hash moved. `FIX-SIM-DIRECT-HIT-CAST-ATTRIBUTION` closed.

### Plan text the build no longer matches

- **0.6 P5 (two Defies):** a Defy begun later catches the earlier Defier only from a press 9 or more ticks later (the earlier Defier is invulnerable through press + 13), not "1 or more ticks later".
- **2.11 / MM-FX (the Fighter ring):** the Fighter Defy ring and the 323 counter step fire on the blast frame (6), not at the press.
- **2.13 / P26 (the bot):** the `play` bot's Defy needs 1 or more foes within 250 px, not 2 or more (`632c393b`).
- **2.15 / 3.2 MM-SWEEP (theme roles):** the generator reports 72 Label roles after the `DefyLabel` role went (the plan said 70 to 69).
- **2.15 / O8 (the grid minor names):** the three minors read "Minor Recharge", not "Minor Momentum" or "Minor Wave Regen".
- **2.1 / 1.5 (323 size):** 323 is 44 B a fighter (eleven ints) after `SpecialCastId`, not 40; projectiles (301) and zones (309) gained a `CastId` each.
- **1.3 / 5.2 (warnings):** a full rebuild now shows only `CS8632`; `KLSG_ECS004` on `FighterTuningComponent` is gone because 306 is under 128 B.
- **4 (totals):** the build is +108 (3879) against the plan's +92 (3863): HUD +1, SIM -1, FIXB1 +9, SWEEP +1 and FIXB2C +6.
- **4.2 (running totals):** B1 merged at 3851, FIXB1 at 3860, B2 at 3874, SWEEP at 3873, FIXB2C at 3879.

---

## 10. Closeout report

- **Status:** closed 2026-10-31 at `f269b611` (main plus the merges listed in section 9; the closeout docs are the commit on `mm/closeout`).
- **Merge record:** MM-RULES `19df9808` plus its review fix `9cba2b9a`; Phase B1 `1d02f904` to `ca86b390`, ruling `ab532365`, record `88ff3ad0`; MM-FIXB1 `b4a848e8` / `e8355b19`; B2 `1eb66b9d` / `ad2ae3fe`, the bot ruling `632c393b`; MM-SWEEP `544ce84c`; docs `b1f8dbc5` / `5ec89171`; MM-FIXB2C `0d426f25` / `6f3eb7c9` / `f269b611`.
- **What shipped:** the four-pip Momentum gauge shared by Special 1 and 2 with a 2 s per-slot reuse delay, in both modes (the Special cooldowns are gone; the movement ability keeps its cooldown); the active Defy History on its own button for 50 Influence, with an invulnerable startup, an unblockable push-and-stagger blast and an echo cash-in heal, in both modes, and the passive lethal save deleted; Story meter gain x0.5 with scaled flat effects, Fighter stock-loss retention 1/2; the HUD pips, the Starved lens and the 50 notch; the Defy presentation (freeze, ring, pulse, a silent cue slot); the CPU by band and the Mirror reading the gauge and Defying; bots and telemetry (`defy` and `momentum` blocks); the Level 0 lesson and the Story text; the grid minors on Momentum regen; protocol v5, no save schema change and no new component ID (323 grew to 44 B, 301 and 309 by an int).
- **Full suite:** `Failed: 0, Passed: 3879, Total: 3879`, three consecutive runs at `f269b611` (2 m 17 s, 2 m 13 s and 2 m 16 s), orphans drained, lock held; reconciled as 3771 + RULES 7 = 3778; + INPUT 7, TEXT 3, FX 7, HUD 10, SMOM 17, SDEFY 12, SIM 17 = 3851; + FIXB1 9 = 3860; + BOTS 4, CPU 10 = 3874; SWEEP -1 = 3873; FIXB2C +6 = 3879 (+108 against the plan's +92). The `[TestCase]` attribute count under `tests/` is 3879. Build: 0 errors, only `CS8632` (`KLSG_ECS004` is gone).
- **Rollback re-measure** (`docs/PERFORMANCE_BASELINE.md`): worst depth-7 median 0.079 ms (`globe_theatre`), worst p95 0.126 ms (`orleans_vanguard`), 0 breaches against the 8 ms budget, largest snapshot 3,004 B (`chicago_exposition`); the per-stage table is in that file. The harness changed (a two-pip start starves the spam scripts), so the figures are not strictly comparable with 2026-10-25's 0.090 / 0.137 ms and 3,277 B.
- **Smokes and CPU smoke:** `MainMenu`, `HubWorld`, `Level_00_Tutorial`, `Level_02_Orleans`, `Level_13_ChronalVoid`, `TestArena` (1800 frames) and `FighterStage_Paris` all exited 0 with no script errors; only the known noise appeared ("Invalid polygon data" from `HudCapsule._Draw` and the `PulpAbilitySlot` `SweepLayer._Draw`, which a control level also prints; resources still in use; leaked ObjectDB). The CPU smoke (`FighterCpuSpecialTimingSmokeTests`, 1800 ticks a stage) counted Defies per fighter: florence_workshop einstein 0 / cleopatra 0; orleans_vanguard joan 1 / tesla 0; chicago_exposition leonardo 1 / shakespeare 1; paris_bastille lincoln 0 / mozart 0; vesuvius_caldera cleopatra 1 / tubman 0; nassau_flagship tesla 0 / einstein 2; alexandria_chambers shakespeare 1 / joan 1; berlin_wall mozart 1 / leonardo 1; globe_theatre tubman 0 / lincoln 0; gettysburg_ridge einstein 1 / cleopatra 1. That is 12 Defies over ten matches; every fighter cast 3 to 5 Specials and none pressed into its own cast.
- **Bot pair** (`mmbefore0` / `mmbefore1` at `56dbc3e4` against `mmafter0` / `mmafter1` at `f269b611`; Levels 2, 6, 7, 12 and 13, Joan and Lincoln, `play`, Easy and Normal, seeds 0-1, `-MaxSeconds 0`, 40 runs a side; scripts `D:/Projects/meter_rework_2026-10-30/mm_bots_before.ps1` and `mm_bots_after.ps1`, table `bots_compare.txt` beside them): completions 30 to 31, deaths per run 2.0 to 1.98, damage taken per run 295 to 286, Ultimates per run 2.33 to 3.38, meter gain per minute 98.2 to 131.2, standard burst kills 32 % to 40 %. Defy: 0.6 presses and 0.57 Defies a run (the passive Defy fired 0.25 a run before), hits taken in the 3 s before / after a Defy 2.61 / 0.74, **died within 3 s: 1 of 23 (4.3 %)**, 12.2 HP healed and 1.39 targets blasted per Defy. Momentum: 4.93 Specials cast a minute (11.68 and 3.90 a run), 15.46 pips gained a run (regen 9.38), 24.8 % of tracked time at 0 pips, 34.3 % full, no starved press. Most other combat metrics are not comparable across the update, and two batches a side are thin evidence.
- **Captures:** `D:/Projects/ftt-scratch/mm-captures/final` (the `huds_*` and `hudf_*` targets, taken at `f269b611`); hand-built states, evidence of layout only.
- **Docs:** the design master and mirror and the contracts in both copies (`docs/handoffs/MM_DOCS_DESIGN.md`; the three Unbound lines by hand), the ledger section and index (`MM_DOCS_REPO.md`; the closeout amended `PROVISIONAL-BOT-DEFY-POLICY`, `USER-BOT-COMBAT-TELEMETRY` and the index baseline), `AGENTS.md` and `CLAUDE.md` (baseline 3879, the bot pair), `docs/PERFORMANCE_BASELINE.md`.
- **Review history** (Codex):

| Scope | Round | Verdict | Findings |
|---|---|---|---|
| Phase A | 1 | approve with nits | 2 overflow nits, fixed |
| B1 | 1 | changes requested | 3 major, 2 minor |
| B1 | 2 | changes requested | finding 4 reworked |
| B1 | 3 | approve with nits | 2 test nits, done in SWEEP |
| B2+C | 1 | changes requested | 1 major, 3 minor |
| B2+C | 2 | approve with nits | 1 test nit, fixed in `f269b611` |

- **Questions answered or still open:** Q1 (the name stays "Defy History"), Q2 (a ring-out over an Open-stage pit is allowed, `VERIFY-DEFY-BLAST-RINGOUT`), Q3 (no regen before GO) and Q4 (the Hard CPU Defies on every qualifying trigger and may starve its Ultimate) were answered on 2026-10-31; Q4's effect is unobserved (below).
- **Not delivered / open:**
  - **Nobody has played or looked at any of it.** The hands-on session in section 5.3 (sixteen rows) is owed before the kit pass, together with the owed 2026-10-16 pad checklist (`docs/COMBAT_MOVEMENT_UPDATE_2026-10-16_PLAN.md` section 5, rows 1-25). Every acceptance claim is a headless suite, a smoke, a probe or a bot run.
  - Every number is provisional (the Momentum rates and costs, the 2 s reuse delay, the Defy frames, radius, push, stagger and heal, the CPU's thresholds, the bot's policy, the HUD sizes): see the `PROVISIONAL-*` rows.
  - The HTML mirrors in docs-3 are not updated, and the docs-3 `HUD_CONTRACT` needs a re-sync.
  - The Level 0 objective label uses the word "Defy", not glyphs, and the first-use Defy tooltip has no binding label (`gameplay_defy` can be added as a `{0}` argument).
  - The Fighter driver's Defy dedupe has a known limit: a correction that removes a Defy entirely suppresses the next one landing on the same counter value, once. It was reasoned, not exercised under a real rollback (LAN is de-scoped).
  - The Tesla Lorentz Pulse attribution stamp (the cast id on its zone pulse) has no dedicated test.
  - The Hard CPU may starve its own Ultimate by spending Influence on Defy (Q4; `VERIFY-CPU-DEFY-ULTIMATE-STARVE`, pad session row 11).
  - The `MeterBoost` orb is still unspawned.
  - `DEFER-INPUT-WIRE-FULL` stands.
  - The Requiem Chord's shave of Fortissimo is disabled, not redesigned; the redesign is owed to Mozart's kit pass (`DEFER-REQUIEM-FORTISSIMO-REDESIGN`, the removed value was 2.0 s), as are per-kit Momentum costs (`DEFER-KIT-PASS-MOMENTUM`).
  - `MM_BOTS.md` still says two foes for the bot's Defy; the ledger and CLAUDE.md say one.
- **Nobody has played or looked at any of it**; section 5.3 is owed before the kit pass.
